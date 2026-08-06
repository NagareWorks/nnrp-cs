using System;

namespace Nnrp.Core
{
    public readonly struct NnrpSchemaDescriptorHeader : IEquatable<NnrpSchemaDescriptorHeader>
    {
        public const int HeaderLength = 32;
        public const ushort KnownSchemaFlagMask = 0x000F;
        public const ushort SchemaFlagCacheable = 0x0001;
        public const ushort SchemaFlagCritical = 0x0002;
        public const ushort SchemaFlagDefaultBindable = 0x0004;
        public const ushort SchemaFlagHashStable = 0x0008;
        public const ushort ProfileUnspecified = TypedPayloadProfileId.UnspecifiedValue;
        public const ushort ProfileTensor = TypedPayloadProfileId.TensorValue;
        public const ushort ProfileToken = TypedPayloadProfileId.TokenValue;
        public const uint TokenDeltaSchemaId = TypedPayloadDescriptor.TokenDeltaSchemaId;
        public const uint TokenDeltaSchemaVersion = TypedPayloadDescriptor.TokenDeltaSchemaVersion;
        public const ushort TokenDeltaDefaultStreamSemantics = TypedPayloadDescriptor.StreamSemanticsAppend;

        public NnrpSchemaDescriptorHeader(
            uint schemaId,
            uint schemaVersion,
            ushort profileId,
            ushort schemaFlags,
            byte minVersionMajor,
            byte maxVersionMajor,
            uint bodyBytes,
            ushort dependencyCount,
            ushort defaultStreamSemantics,
            ulong schemaHash)
        {
            SchemaId = schemaId;
            SchemaVersion = schemaVersion;
            ProfileId = profileId;
            SchemaFlags = schemaFlags;
            MinVersionMajor = minVersionMajor;
            MaxVersionMajor = maxVersionMajor;
            BodyBytes = bodyBytes;
            DependencyCount = dependencyCount;
            DefaultStreamSemantics = defaultStreamSemantics;
            SchemaHash = schemaHash;
        }

        public uint SchemaId { get; }

        public uint SchemaVersion { get; }

        public ushort ProfileId { get; }

        public TypedPayloadProfileId Profile => TypedPayloadProfileId.FromValue(ProfileId);

        public ushort SchemaFlags { get; }

        public byte MinVersionMajor { get; }

        public byte MaxVersionMajor { get; }

        public uint BodyBytes { get; }

        public ushort DependencyCount { get; }

        public ushort DefaultStreamSemantics { get; }

        public ulong SchemaHash { get; }

        public bool IsCacheable => (SchemaFlags & SchemaFlagCacheable) != 0;

        public bool IsCritical => (SchemaFlags & SchemaFlagCritical) != 0;

        public bool IsDefaultBindable => (SchemaFlags & SchemaFlagDefaultBindable) != 0;

        public bool IsHashStable => (SchemaFlags & SchemaFlagHashStable) != 0;

        public void Write(Span<byte> destination)
        {
            if (!TryWrite(destination, out _))
            {
                throw new ArgumentException($"Destination must be at least {HeaderLength} bytes and schema flags must be known.", nameof(destination));
            }
        }

        public bool TryWrite(Span<byte> destination, out int bytesWritten)
        {
            bytesWritten = 0;
            if (destination.Length < HeaderLength || (SchemaFlags & ~KnownSchemaFlagMask) != 0)
            {
                return false;
            }

            var writer = new FixedBinaryWriter(destination);
            if (!writer.TryWriteUInt32(SchemaId)
                || !writer.TryWriteUInt32(SchemaVersion)
                || !writer.TryWriteUInt16(ProfileId)
                || !writer.TryWriteUInt16(SchemaFlags)
                || !writer.TryWriteByte(MinVersionMajor)
                || !writer.TryWriteByte(MaxVersionMajor)
                || !writer.TryWriteUInt16(0)
                || !writer.TryWriteUInt32(BodyBytes)
                || !writer.TryWriteUInt16(DependencyCount)
                || !writer.TryWriteUInt16(DefaultStreamSemantics)
                || !writer.TryWriteUInt64(SchemaHash))
            {
                return false;
            }

            bytesWritten = writer.Offset;
            return bytesWritten == HeaderLength;
        }

        public byte[] ToArray()
        {
            var payload = new byte[HeaderLength];
            Write(payload);
            return payload;
        }

        public static bool TryParse(ReadOnlySpan<byte> source, out NnrpSchemaDescriptorHeader header)
        {
            return TryParse(source, out header, out _);
        }

        public static bool TryParse(ReadOnlySpan<byte> source, out NnrpSchemaDescriptorHeader header, out NnrpParseError error)
        {
            header = default;
            error = NnrpParseError.None;
            if (source.Length < HeaderLength)
            {
                error = NnrpParseError.SourceTooShort;
                return false;
            }

            var reader = new FixedBinaryReader(source);
            if (!reader.TryReadUInt32(out var schemaId)
                || !reader.TryReadUInt32(out var schemaVersion)
                || !reader.TryReadUInt16(out var profileId)
                || !reader.TryReadUInt16(out var schemaFlags)
                || !reader.TryReadByte(out var minVersionMajor)
                || !reader.TryReadByte(out var maxVersionMajor)
                || !reader.TryReadUInt16(out var reserved0)
                || !reader.TryReadUInt32(out var bodyBytes)
                || !reader.TryReadUInt16(out var dependencyCount)
                || !reader.TryReadUInt16(out var defaultStreamSemantics)
                || !reader.TryReadUInt64(out var schemaHash))
            {
                error = NnrpParseError.SourceTooShort;
                return false;
            }

            if ((schemaFlags & ~KnownSchemaFlagMask) != 0 || reserved0 != 0)
            {
                error = NnrpParseError.NonZeroReservedField;
                return false;
            }

            header = new NnrpSchemaDescriptorHeader(
                schemaId,
                schemaVersion,
                profileId,
                schemaFlags,
                minVersionMajor,
                maxVersionMajor,
                bodyBytes,
                dependencyCount,
                defaultStreamSemantics,
                schemaHash);
            return true;
        }

        public bool Equals(NnrpSchemaDescriptorHeader other)
        {
            return SchemaId == other.SchemaId
                && SchemaVersion == other.SchemaVersion
                && ProfileId == other.ProfileId
                && SchemaFlags == other.SchemaFlags
                && MinVersionMajor == other.MinVersionMajor
                && MaxVersionMajor == other.MaxVersionMajor
                && BodyBytes == other.BodyBytes
                && DependencyCount == other.DependencyCount
                && DefaultStreamSemantics == other.DefaultStreamSemantics
                && SchemaHash == other.SchemaHash;
        }

        public override bool Equals(object obj)
        {
            return obj is NnrpSchemaDescriptorHeader other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = SchemaId.GetHashCode();
                hash = (hash * 397) ^ SchemaVersion.GetHashCode();
                hash = (hash * 397) ^ ProfileId.GetHashCode();
                hash = (hash * 397) ^ SchemaFlags.GetHashCode();
                hash = (hash * 397) ^ MinVersionMajor.GetHashCode();
                hash = (hash * 397) ^ MaxVersionMajor.GetHashCode();
                hash = (hash * 397) ^ BodyBytes.GetHashCode();
                hash = (hash * 397) ^ DependencyCount.GetHashCode();
                hash = (hash * 397) ^ DefaultStreamSemantics.GetHashCode();
                hash = (hash * 397) ^ SchemaHash.GetHashCode();
                return hash;
            }
        }
    }
}
