using System;

namespace Nnrp.Core
{
    public readonly struct ExtensionFrameDescriptor : IEquatable<ExtensionFrameDescriptor>
    {
        public const int DescriptorLength = 16;

        public ExtensionFrameDescriptor(
            ushort extensionKind,
            ushort extensionFlags,
            ushort profileId,
            ushort reserved0,
            uint payloadOffset,
            uint payloadLength)
        {
            ExtensionKind = extensionKind;
            ExtensionFlags = extensionFlags;
            ProfileId = profileId;
            Reserved0 = reserved0;
            PayloadOffset = payloadOffset;
            PayloadLength = payloadLength;
        }

        public ushort ExtensionKind { get; }

        public ushort ExtensionFlags { get; }

        public ushort ProfileId { get; }

        public ushort Reserved0 { get; }

        public uint PayloadOffset { get; }

        public uint PayloadLength { get; }

        public void Write(Span<byte> destination)
        {
            if (!TryWrite(destination, out _))
            {
                throw new ArgumentException($"Destination must be at least {DescriptorLength} bytes.", nameof(destination));
            }
        }

        public bool TryWrite(Span<byte> destination, out int bytesWritten)
        {
            bytesWritten = 0;
            if (destination.Length < DescriptorLength)
            {
                return false;
            }

            var writer = new FixedBinaryWriter(destination);
            if (!writer.TryWriteUInt16(ExtensionKind)
                || !writer.TryWriteUInt16(ExtensionFlags)
                || !writer.TryWriteUInt16(ProfileId)
                || !writer.TryWriteUInt16(Reserved0)
                || !writer.TryWriteUInt32(PayloadOffset)
                || !writer.TryWriteUInt32(PayloadLength))
            {
                return false;
            }

            bytesWritten = writer.Offset;
            return bytesWritten == DescriptorLength;
        }

        public byte[] ToArray()
        {
            var payload = new byte[DescriptorLength];
            Write(payload);
            return payload;
        }

        public static bool TryParse(ReadOnlySpan<byte> source, out ExtensionFrameDescriptor descriptor)
        {
            return TryParse(source, strict: false, out descriptor, out _);
        }

        public static bool TryParse(ReadOnlySpan<byte> source, bool strict, out ExtensionFrameDescriptor descriptor, out NnrpParseError error)
        {
            descriptor = default;
            error = NnrpParseError.None;
            if (source.Length < DescriptorLength)
            {
                error = NnrpParseError.SourceTooShort;
                return false;
            }

            var reader = new FixedBinaryReader(source);
            if (!reader.TryReadUInt16(out var extensionKind)
                || !reader.TryReadUInt16(out var extensionFlags)
                || !reader.TryReadUInt16(out var profileId)
                || !reader.TryReadUInt16(out var reserved0)
                || !reader.TryReadUInt32(out var payloadOffset)
                || !reader.TryReadUInt32(out var payloadLength))
            {
                error = NnrpParseError.SourceTooShort;
                return false;
            }

            if (strict && (reserved0 != 0 || (extensionFlags & ~0x0001) != 0))
            {
                error = NnrpParseError.NonZeroReservedField;
                return false;
            }

            descriptor = new ExtensionFrameDescriptor(extensionKind, extensionFlags, profileId, reserved0, payloadOffset, payloadLength);
            return true;
        }

        public bool Equals(ExtensionFrameDescriptor other)
        {
            return ExtensionKind == other.ExtensionKind
                && ExtensionFlags == other.ExtensionFlags
                && ProfileId == other.ProfileId
                && Reserved0 == other.Reserved0
                && PayloadOffset == other.PayloadOffset
                && PayloadLength == other.PayloadLength;
        }

        public override bool Equals(object obj)
        {
            return obj is ExtensionFrameDescriptor other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = ExtensionKind.GetHashCode();
                hash = (hash * 397) ^ ExtensionFlags.GetHashCode();
                hash = (hash * 397) ^ ProfileId.GetHashCode();
                hash = (hash * 397) ^ Reserved0.GetHashCode();
                hash = (hash * 397) ^ PayloadOffset.GetHashCode();
                hash = (hash * 397) ^ PayloadLength.GetHashCode();
                return hash;
            }
        }
    }
}
