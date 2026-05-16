using System;

namespace Nnrp.Core
{
    public readonly struct TensorSectionDescriptor : IEquatable<TensorSectionDescriptor>
    {
        public const int DescriptorLength = 32;

        public TensorSectionDescriptor(
            TensorRole role,
            CodecId codec,
            DTypeId dtype,
            TensorLayoutId layout,
            ScalePolicy scalePolicy,
            ushort flags,
            uint elementCountPerTile,
            uint codecTableBytes,
            uint lengthTableBytes,
            uint payloadBytes,
            uint payloadStrideBytes)
        {
            Role = role;
            Codec = codec;
            DType = dtype;
            Layout = layout;
            ScalePolicy = scalePolicy;
            Flags = flags;
            ElementCountPerTile = elementCountPerTile;
            CodecTableBytes = codecTableBytes;
            LengthTableBytes = lengthTableBytes;
            PayloadBytes = payloadBytes;
            PayloadStrideBytes = payloadStrideBytes;
        }

        public TensorRole Role { get; }

        public CodecId Codec { get; }

        public DTypeId DType { get; }

        public TensorLayoutId Layout { get; }

        public ScalePolicy ScalePolicy { get; }

        public ushort Flags { get; }

        public uint ElementCountPerTile { get; }

        public uint CodecTableBytes { get; }

        public uint LengthTableBytes { get; }

        public uint PayloadBytes { get; }

        public uint PayloadStrideBytes { get; }

        public bool IsFixedStride => PayloadStrideBytes != 0;

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
            if (!writer.TryWriteUInt16((ushort)Role)
                || !writer.TryWriteByte((byte)Codec)
                || !writer.TryWriteByte((byte)DType)
                || !writer.TryWriteByte((byte)Layout)
                || !writer.TryWriteByte((byte)ScalePolicy)
                || !writer.TryWriteUInt16(Flags)
                || !writer.TryWriteUInt32(ElementCountPerTile)
                || !writer.TryWriteUInt32(CodecTableBytes)
                || !writer.TryWriteUInt32(LengthTableBytes)
                || !writer.TryWriteUInt32(PayloadBytes)
                || !writer.TryWriteUInt32(PayloadStrideBytes)
                || !writer.TryWriteUInt32(0))
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

        public static bool TryParse(ReadOnlySpan<byte> source, out TensorSectionDescriptor descriptor)
        {
            return TryParse(source, strict: false, out descriptor, out _);
        }

        public static bool TryParse(ReadOnlySpan<byte> source, bool strict, out TensorSectionDescriptor descriptor, out NnrpParseError error)
        {
            descriptor = default;
            error = NnrpParseError.None;
            if (source.Length < DescriptorLength)
            {
                error = NnrpParseError.SourceTooShort;
                return false;
            }

            var reader = new FixedBinaryReader(source);
            if (!reader.TryReadUInt16(out var role)
                || !reader.TryReadByte(out var codec)
                || !reader.TryReadByte(out var dtype)
                || !reader.TryReadByte(out var layout)
                || !reader.TryReadByte(out var scalePolicy)
                || !reader.TryReadUInt16(out var flags)
                || !reader.TryReadUInt32(out var elementCountPerTile)
                || !reader.TryReadUInt32(out var codecTableBytes)
                || !reader.TryReadUInt32(out var lengthTableBytes)
                || !reader.TryReadUInt32(out var payloadBytes)
                || !reader.TryReadUInt32(out var payloadStrideBytes)
                || !reader.TryReadUInt32(out var reserved))
            {
                error = NnrpParseError.SourceTooShort;
                return false;
            }

            if (strict && reserved != 0)
            {
                error = NnrpParseError.NonZeroReservedField;
                return false;
            }

            if (strict && !IsPayloadLayoutConsistent(payloadBytes, lengthTableBytes, payloadStrideBytes))
            {
                error = NnrpParseError.InconsistentSectionDescriptor;
                return false;
            }

            descriptor = new TensorSectionDescriptor(
                (TensorRole)role,
                (CodecId)codec,
                (DTypeId)dtype,
                (TensorLayoutId)layout,
                (ScalePolicy)scalePolicy,
                flags,
                elementCountPerTile,
                codecTableBytes,
                lengthTableBytes,
                payloadBytes,
                payloadStrideBytes);
            return true;
        }

        public bool Equals(TensorSectionDescriptor other)
        {
            return Role == other.Role
                && Codec == other.Codec
                && DType == other.DType
                && Layout == other.Layout
                && ScalePolicy == other.ScalePolicy
                && Flags == other.Flags
                && ElementCountPerTile == other.ElementCountPerTile
                && CodecTableBytes == other.CodecTableBytes
                && LengthTableBytes == other.LengthTableBytes
                && PayloadBytes == other.PayloadBytes
                && PayloadStrideBytes == other.PayloadStrideBytes;
        }

        public override bool Equals(object obj)
        {
            return obj is TensorSectionDescriptor other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = Role.GetHashCode();
                hash = (hash * 397) ^ Codec.GetHashCode();
                hash = (hash * 397) ^ DType.GetHashCode();
                hash = (hash * 397) ^ Layout.GetHashCode();
                hash = (hash * 397) ^ ScalePolicy.GetHashCode();
                hash = (hash * 397) ^ Flags.GetHashCode();
                hash = (hash * 397) ^ ElementCountPerTile.GetHashCode();
                hash = (hash * 397) ^ CodecTableBytes.GetHashCode();
                hash = (hash * 397) ^ LengthTableBytes.GetHashCode();
                hash = (hash * 397) ^ PayloadBytes.GetHashCode();
                hash = (hash * 397) ^ PayloadStrideBytes.GetHashCode();
                return hash;
            }
        }

        private static bool IsPayloadLayoutConsistent(uint payloadBytes, uint lengthTableBytes, uint payloadStrideBytes)
        {
            if (payloadBytes == 0)
            {
                return lengthTableBytes == 0 && payloadStrideBytes == 0;
            }

            if (lengthTableBytes != 0 && payloadStrideBytes != 0)
            {
                return false;
            }

            return lengthTableBytes != 0 || payloadStrideBytes != 0;
        }
    }
}
