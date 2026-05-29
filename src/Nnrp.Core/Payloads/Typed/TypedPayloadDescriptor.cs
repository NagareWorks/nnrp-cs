using System;

namespace Nnrp.Core
{
    public readonly struct TypedPayloadDescriptor : IEquatable<TypedPayloadDescriptor>
    {
        public const int DescriptorLength = 24;
        public const ushort KnownDescriptorFlagMask = 0x000F;
        public const ushort ProfileUnspecified = 0;
        public const ushort ProfileTensor = 1;
        public const ushort ProfileToken = 2;
        public const uint TokenDeltaSchemaId = 0x00001001;
        public const uint TokenDeltaSchemaVersion = 3;
        public const ushort StreamSemanticsDefault = 0;
        public const ushort StreamSemanticsSnapshot = 1;
        public const ushort StreamSemanticsAppend = 2;
        public const ushort StreamSemanticsReplace = 3;
        public const ushort StreamSemanticsEvent = 4;
        public const ushort StreamSemanticsToolUpdate = 5;

        public TypedPayloadDescriptor(
            PayloadKind payloadKind,
            byte descriptorFlags,
            ushort profileId,
            uint payloadOffset,
            uint payloadLength,
            uint reserved)
            : this(
                  payloadKind,
                  profileId,
                  descriptorFlags,
                  schemaId: 0,
                  schemaVersion: 0,
                  streamSemantics: StreamSemanticsDefault,
                  payloadOffset,
                  payloadLength,
                  checked((ushort)reserved))
        {
        }

        public TypedPayloadDescriptor(
            PayloadKind payloadKind,
            ushort profileId,
            ushort descriptorFlags,
            uint schemaId,
            uint schemaVersion,
            ushort streamSemantics,
            uint payloadOffset,
            uint payloadLength,
            ushort reserved0 = 0)
        {
            PayloadKind = payloadKind;
            DescriptorFlags = descriptorFlags;
            ProfileId = profileId;
            SchemaId = schemaId;
            SchemaVersion = schemaVersion;
            StreamSemantics = streamSemantics;
            Reserved0 = reserved0;
            PayloadOffset = payloadOffset;
            PayloadLength = payloadLength;
        }

        public PayloadKind PayloadKind { get; }

        public ushort DescriptorFlags { get; }

        public ushort ProfileId { get; }

        public uint SchemaId { get; }

        public uint SchemaVersion { get; }

        public ushort StreamSemantics { get; }

        public ushort Reserved0 { get; }

        public uint PayloadOffset { get; }

        public uint PayloadLength { get; }

        public uint Reserved => Reserved0;

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

            if ((DescriptorFlags & ~KnownDescriptorFlagMask) != 0 || Reserved0 != 0)
            {
                return false;
            }

            var writer = new FixedBinaryWriter(destination);
            if (!writer.TryWriteUInt16(ProfileId)
                || !writer.TryWriteUInt16(DescriptorFlags)
                || !writer.TryWriteUInt32(SchemaId)
                || !writer.TryWriteUInt32(SchemaVersion)
                || !writer.TryWriteUInt16(StreamSemantics)
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
            if (!reader.TryReadUInt16(out var profileId)
                || !reader.TryReadUInt16(out var descriptorFlags)
                || !reader.TryReadUInt32(out var schemaId)
                || !reader.TryReadUInt32(out var schemaVersion)
                || !reader.TryReadUInt16(out var streamSemantics)
                || !reader.TryReadUInt16(out var reserved0)
                || !reader.TryReadUInt32(out var payloadOffset)
                || !reader.TryReadUInt32(out var payloadLength))
            {
                error = NnrpParseError.SourceTooShort;
                return false;
            }

            if (strict && ((descriptorFlags & ~KnownDescriptorFlagMask) != 0 || reserved0 != 0))
            {
                error = NnrpParseError.NonZeroReservedField;
                return false;
            }

            descriptor = new TypedPayloadDescriptor(
                InferPayloadKind(profileId),
                profileId,
                descriptorFlags,
                schemaId,
                schemaVersion,
                streamSemantics,
                payloadOffset,
                payloadLength,
                reserved0);
            return true;
        }

        internal TypedPayloadDescriptor WithPayloadKind(PayloadKind payloadKind)
        {
            return new TypedPayloadDescriptor(
                payloadKind,
                ProfileId,
                DescriptorFlags,
                SchemaId,
                SchemaVersion,
                StreamSemantics,
                PayloadOffset,
                PayloadLength,
                Reserved0);
        }

        public bool Equals(TypedPayloadDescriptor other)
        {
            return PayloadKind == other.PayloadKind
                && DescriptorFlags == other.DescriptorFlags
                && ProfileId == other.ProfileId
                && SchemaId == other.SchemaId
                && SchemaVersion == other.SchemaVersion
                && StreamSemantics == other.StreamSemantics
                && Reserved0 == other.Reserved0
                && PayloadOffset == other.PayloadOffset
                && PayloadLength == other.PayloadLength;
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
                hash = (hash * 397) ^ SchemaId.GetHashCode();
                hash = (hash * 397) ^ SchemaVersion.GetHashCode();
                hash = (hash * 397) ^ StreamSemantics.GetHashCode();
                hash = (hash * 397) ^ Reserved0.GetHashCode();
                hash = (hash * 397) ^ PayloadOffset.GetHashCode();
                hash = (hash * 397) ^ PayloadLength.GetHashCode();
                return hash;
            }
        }

        private static PayloadKind InferPayloadKind(ushort profileId)
        {
            switch (profileId)
            {
                case ProfileTensor:
                    return PayloadKind.Tensor;
                case ProfileToken:
                    return PayloadKind.TokenChunk;
                default:
                    return PayloadKind.None;
            }
        }
    }
}
