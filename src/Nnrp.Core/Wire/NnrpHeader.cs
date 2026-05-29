using System;
using System.Buffers.Binary;

namespace Nnrp.Core
{
    public readonly struct NnrpHeader : IEquatable<NnrpHeader>
    {
        public const int HeaderLength = 40;

        public const byte CurrentVersionMajor = 1;

        public const byte CurrentWireFormat = 0;

        private const HeaderFlags KnownHeaderFlags = HeaderFlags.AckRequired
            | HeaderFlags.CanDrop
            | HeaderFlags.Stale
            | HeaderFlags.Eos
            | HeaderFlags.Retransmit
            | HeaderFlags.Keyframe;

        public NnrpHeader(
            byte versionMajor,
            MessageType messageType,
            HeaderFlags flags,
            uint metaLength,
            uint bodyLength,
            uint sessionId,
            uint frameId,
            ushort viewId,
            ushort routeId,
            ulong traceId,
            byte wireFormat = CurrentWireFormat,
            byte headerLength = HeaderLength)
        {
            VersionMajor = versionMajor;
            WireFormat = wireFormat;
            MessageType = messageType;
            HeaderLengthValue = headerLength;
            Flags = flags;
            MetaLength = metaLength;
            BodyLength = bodyLength;
            SessionId = sessionId;
            FrameId = frameId;
            ViewId = viewId;
            RouteId = routeId;
            TraceId = traceId;
        }

        public NnrpHeader(
            byte versionMajor,
            byte selectedWireFormat,
            MessageType messageType,
            HeaderFlags flags,
            uint metaLength,
            uint bodyLength,
            uint sessionId,
            uint frameId,
            ushort viewId,
            ushort routeId,
            ulong traceId,
            byte headerLength = HeaderLength)
            : this(
                versionMajor,
                messageType,
                flags,
                metaLength,
                bodyLength,
                sessionId,
                frameId,
                viewId,
                routeId,
                traceId,
                selectedWireFormat,
                headerLength)
        {
        }

        public byte VersionMajor { get; }

        public byte WireFormat { get; }

        public MessageType MessageType { get; }

        public byte HeaderLengthValue { get; }

        public HeaderFlags Flags { get; }

        public uint MetaLength { get; }

        public uint BodyLength { get; }

        public uint SessionId { get; }

        public uint FrameId { get; }

        public ushort ViewId { get; }

        public ushort RouteId { get; }

        public ulong TraceId { get; }

        public void Write(Span<byte> destination)
        {
            if (destination.Length < HeaderLength)
            {
                throw new ArgumentException($"Destination must be at least {HeaderLength} bytes.", nameof(destination));
            }

            if (!TryWrite(destination, out _))
            {
                throw new InvalidOperationException($"Header length must be {HeaderLength}.");
            }
        }

        public bool TryWrite(Span<byte> destination, out int bytesWritten)
        {
            bytesWritten = 0;
            if (destination.Length < HeaderLength || HeaderLengthValue != HeaderLength)
            {
                return false;
            }

            destination[0] = (byte)'N';
            destination[1] = (byte)'N';
            destination[2] = (byte)'R';
            destination[3] = (byte)'P';
            destination[4] = VersionMajor;
            destination[5] = WireFormat;
            destination[6] = (byte)MessageType;
            destination[7] = HeaderLengthValue;
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(8, 4), (uint)Flags);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(12, 4), MetaLength);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(16, 4), BodyLength);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(20, 4), SessionId);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(24, 4), FrameId);
            BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(28, 2), ViewId);
            BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(30, 2), RouteId);
            BinaryPrimitives.WriteUInt64LittleEndian(destination.Slice(32, 8), TraceId);
            bytesWritten = HeaderLength;
            return true;
        }

        public byte[] ToArray()
        {
            var payload = new byte[HeaderLength];
            Write(payload);
            return payload;
        }

        public static bool TryParse(ReadOnlySpan<byte> source, out NnrpHeader header)
        {
            return TryParse(source, NnrpHeaderParseOptions.Default, out header, out _);
        }

        public static bool TryParse(
            ReadOnlySpan<byte> source,
            NnrpHeaderParseOptions options,
            out NnrpHeader header,
            out NnrpParseError error)
        {
            header = default;
            error = NnrpParseError.None;
            if (source.Length < HeaderLength)
            {
                error = NnrpParseError.SourceTooShort;
                return false;
            }

            if (source[0] != (byte)'N' || source[1] != (byte)'N' || source[2] != (byte)'R' || source[3] != (byte)'P')
            {
                error = NnrpParseError.InvalidMagic;
                return false;
            }

            var headerLength = source[7];
            if (headerLength != HeaderLength)
            {
                error = NnrpParseError.InvalidHeaderLength;
                return false;
            }

            var messageType = (MessageType)source[6];
            var flags = (HeaderFlags)BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(8, 4));
            var metaLength = BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(12, 4));
            var bodyLength = BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(16, 4));

            var wireFormat = source[5];
            if (wireFormat != CurrentWireFormat)
            {
                error = NnrpParseError.UnknownWireFormat;
                return false;
            }

            if (options.StrictValidation)
            {
                if (source[4] != CurrentVersionMajor)
                {
                    error = NnrpParseError.UnsupportedVersion;
                    return false;
                }

                if (!Enum.IsDefined(typeof(MessageType), messageType))
                {
                    error = NnrpParseError.UnknownMessageType;
                    return false;
                }

                if (((uint)flags & ~(uint)KnownHeaderFlags) != 0)
                {
                    error = NnrpParseError.ReservedFlagsSet;
                    return false;
                }
            }

            if (!CheckedArithmetic.TryAdd((ulong)metaLength, bodyLength, out var messageLength)
                || messageLength > options.MaxMessageLength)
            {
                error = NnrpParseError.MessageTooLarge;
                return false;
            }

            header = new NnrpHeader(
                versionMajor: source[4],
                messageType: messageType,
                flags: flags,
                metaLength: metaLength,
                bodyLength: bodyLength,
                sessionId: BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(20, 4)),
                frameId: BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(24, 4)),
                viewId: BinaryPrimitives.ReadUInt16LittleEndian(source.Slice(28, 2)),
                routeId: BinaryPrimitives.ReadUInt16LittleEndian(source.Slice(30, 2)),
                traceId: BinaryPrimitives.ReadUInt64LittleEndian(source.Slice(32, 8)),
                wireFormat: wireFormat,
                headerLength: headerLength);
            return true;
        }

        public bool Equals(NnrpHeader other)
        {
            return VersionMajor == other.VersionMajor
                && WireFormat == other.WireFormat
                && MessageType == other.MessageType
                && HeaderLengthValue == other.HeaderLengthValue
                && Flags == other.Flags
                && MetaLength == other.MetaLength
                && BodyLength == other.BodyLength
                && SessionId == other.SessionId
                && FrameId == other.FrameId
                && ViewId == other.ViewId
                && RouteId == other.RouteId
                && TraceId == other.TraceId;
        }

        public override bool Equals(object obj)
        {
            return obj is NnrpHeader other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = VersionMajor;
                hash = (hash * 397) ^ WireFormat;
                hash = (hash * 397) ^ (byte)MessageType;
                hash = (hash * 397) ^ HeaderLengthValue;
                hash = (hash * 397) ^ (int)Flags;
                hash = (hash * 397) ^ (int)MetaLength;
                hash = (hash * 397) ^ (int)BodyLength;
                hash = (hash * 397) ^ (int)SessionId;
                hash = (hash * 397) ^ (int)FrameId;
                hash = (hash * 397) ^ ViewId;
                hash = (hash * 397) ^ RouteId;
                hash = (hash * 397) ^ TraceId.GetHashCode();
                return hash;
            }
        }
    }
}
