using System;

namespace Nnrp.Core
{
    public readonly struct TypedPayloadDescriptor : IEquatable<TypedPayloadDescriptor>
    {
        public const int DescriptorLength = 16;

        public TypedPayloadDescriptor(
            PayloadKind payloadKind,
            byte descriptorFlags,
            ushort profileId,
            uint payloadOffset,
            uint payloadLength,
            uint reserved)
        {
            PayloadKind = payloadKind;
            DescriptorFlags = descriptorFlags;
            ProfileId = profileId;
            PayloadOffset = payloadOffset;
            PayloadLength = payloadLength;
            Reserved = reserved;
        }

        public PayloadKind PayloadKind { get; }

        public byte DescriptorFlags { get; }

        public ushort ProfileId { get; }

        public uint PayloadOffset { get; }

        public uint PayloadLength { get; }

        public uint Reserved { get; }

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
            if (!writer.TryWriteByte((byte)PayloadKind)
                || !writer.TryWriteByte(DescriptorFlags)
                || !writer.TryWriteUInt16(ProfileId)
                || !writer.TryWriteUInt32(PayloadOffset)
                || !writer.TryWriteUInt32(PayloadLength)
                || !writer.TryWriteUInt32(Reserved))
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

        public static bool TryParse(ReadOnlySpan<byte> source, out TypedPayloadDescriptor descriptor)
        {
            return TryParse(source, strict: false, out descriptor, out _);
        }

        public static bool TryParse(ReadOnlySpan<byte> source, bool strict, out TypedPayloadDescriptor descriptor, out NnrpParseError error)
        {
            descriptor = default;
            error = NnrpParseError.None;
            if (source.Length < DescriptorLength)
            {
                error = NnrpParseError.SourceTooShort;
                return false;
            }

            var reader = new FixedBinaryReader(source);
            if (!reader.TryReadByte(out var payloadKind)
                || !reader.TryReadByte(out var descriptorFlags)
                || !reader.TryReadUInt16(out var profileId)
                || !reader.TryReadUInt32(out var payloadOffset)
                || !reader.TryReadUInt32(out var payloadLength)
                || !reader.TryReadUInt32(out var reserved))
            {
                error = NnrpParseError.SourceTooShort;
                return false;
            }

            if (strict && (descriptorFlags != 0 || reserved != 0))
            {
                error = NnrpParseError.NonZeroReservedField;
                return false;
            }

            descriptor = new TypedPayloadDescriptor((PayloadKind)payloadKind, descriptorFlags, profileId, payloadOffset, payloadLength, reserved);
            return true;
        }

        public bool Equals(TypedPayloadDescriptor other)
        {
            return PayloadKind == other.PayloadKind
                && DescriptorFlags == other.DescriptorFlags
                && ProfileId == other.ProfileId
                && PayloadOffset == other.PayloadOffset
                && PayloadLength == other.PayloadLength
                && Reserved == other.Reserved;
        }

        public override bool Equals(object obj)
        {
            return obj is TypedPayloadDescriptor other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = PayloadKind.GetHashCode();
                hash = (hash * 397) ^ DescriptorFlags.GetHashCode();
                hash = (hash * 397) ^ ProfileId.GetHashCode();
                hash = (hash * 397) ^ PayloadOffset.GetHashCode();
                hash = (hash * 397) ^ PayloadLength.GetHashCode();
                hash = (hash * 397) ^ Reserved.GetHashCode();
                return hash;
            }
        }
    }
}
