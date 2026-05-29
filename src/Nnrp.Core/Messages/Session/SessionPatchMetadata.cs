using System;

namespace Nnrp.Core
{
    public readonly struct SessionPatchMetadata : IEquatable<SessionPatchMetadata>
    {
        public const int MetadataLength = 36;

        public SessionPatchMetadata(
            ushort profileId,
            SessionPatchField patchMask,
            uint targetCadenceX100,
            ushort qualityTier,
            ushort degradePolicy,
            ulong activeLaneMask,
            uint preferredCodecBitmap,
            uint preferredCompressionBitmap,
            uint profilePatchBytes,
            ushort reserved0 = 0)
        {
            ProfileId = profileId;
            PatchMask = patchMask;
            TargetCadenceX100 = targetCadenceX100;
            QualityTier = qualityTier;
            DegradePolicy = degradePolicy;
            ActiveLaneMask = activeLaneMask;
            PreferredCodecBitmap = preferredCodecBitmap;
            PreferredCompressionBitmap = preferredCompressionBitmap;
            ProfilePatchBytes = profilePatchBytes;
            Reserved0 = reserved0;
        }

        public ushort ProfileId { get; }
        public SessionPatchField PatchMask { get; }
        public uint TargetCadenceX100 { get; }
        public ushort QualityTier { get; }
        public ushort DegradePolicy { get; }
        public ulong ActiveLaneMask { get; }
        public uint PreferredCodecBitmap { get; }
        public uint PreferredCompressionBitmap { get; }
        public uint ProfilePatchBytes { get; }
        public ushort Reserved0 { get; }

        public uint TargetFpsTimes100 => TargetCadenceX100;
        public uint ActiveViewMaskLow => unchecked((uint)(ActiveLaneMask & 0xFFFFFFFFUL));
        public uint ActiveViewMaskHigh => unchecked((uint)(ActiveLaneMask >> 32));

        public void Write(Span<byte> destination)
        {
            if (!TryWrite(destination, out _))
            {
                throw new ArgumentException($"Destination must be at least {MetadataLength} bytes.", nameof(destination));
            }
        }

        public bool TryWrite(Span<byte> destination, out int bytesWritten)
        {
            bytesWritten = 0;
            if (destination.Length < MetadataLength)
            {
                return false;
            }

            var writer = new FixedBinaryWriter(destination);
            if (!writer.TryWriteUInt16(ProfileId)
                || !writer.TryWriteUInt16(Reserved0)
                || !writer.TryWriteUInt32((uint)PatchMask)
                || !writer.TryWriteUInt32(TargetCadenceX100)
                || !writer.TryWriteUInt16(QualityTier)
                || !writer.TryWriteUInt16(DegradePolicy)
                || !writer.TryWriteUInt64(ActiveLaneMask)
                || !writer.TryWriteUInt32(PreferredCodecBitmap)
                || !writer.TryWriteUInt32(PreferredCompressionBitmap)
                || !writer.TryWriteUInt32(ProfilePatchBytes))
            {
                return false;
            }

            bytesWritten = writer.Offset;
            return bytesWritten == MetadataLength;
        }

        public byte[] ToArray()
        {
            var payload = new byte[MetadataLength];
            Write(payload);
            return payload;
        }

        public static bool TryParse(ReadOnlySpan<byte> source, out SessionPatchMetadata metadata)
        {
            return TryParse(source, strict: false, out metadata, out _);
        }

        public static bool TryParse(ReadOnlySpan<byte> source, bool strict, out SessionPatchMetadata metadata, out NnrpParseError error)
        {
            metadata = default;
            error = NnrpParseError.None;
            if (source.Length < MetadataLength)
            {
                error = NnrpParseError.SourceTooShort;
                return false;
            }

            var reader = new FixedBinaryReader(source);
            if (!reader.TryReadUInt16(out var profileId)
                || !reader.TryReadUInt16(out var reserved0)
                || !reader.TryReadUInt32(out var patchMask)
                || !reader.TryReadUInt32(out var targetCadenceX100)
                || !reader.TryReadUInt16(out var qualityTier)
                || !reader.TryReadUInt16(out var degradePolicy)
                || !reader.TryReadUInt64(out var activeLaneMask)
                || !reader.TryReadUInt32(out var preferredCodecBitmap)
                || !reader.TryReadUInt32(out var preferredCompressionBitmap)
                || !reader.TryReadUInt32(out var profilePatchBytes))
            {
                error = NnrpParseError.SourceTooShort;
                return false;
            }

            if (strict && reserved0 != 0)
            {
                error = NnrpParseError.NonZeroReservedField;
                return false;
            }

            metadata = new SessionPatchMetadata(
                profileId,
                (SessionPatchField)patchMask,
                targetCadenceX100,
                qualityTier,
                degradePolicy,
                activeLaneMask,
                preferredCodecBitmap,
                preferredCompressionBitmap,
                profilePatchBytes,
                reserved0);
            return true;
        }

        public bool Equals(SessionPatchMetadata other)
        {
            return ProfileId == other.ProfileId
                && PatchMask == other.PatchMask
                && TargetCadenceX100 == other.TargetCadenceX100
                && QualityTier == other.QualityTier
                && DegradePolicy == other.DegradePolicy
                && ActiveLaneMask == other.ActiveLaneMask
                && PreferredCodecBitmap == other.PreferredCodecBitmap
                && PreferredCompressionBitmap == other.PreferredCompressionBitmap
                && ProfilePatchBytes == other.ProfilePatchBytes
                && Reserved0 == other.Reserved0;
        }

        public override bool Equals(object obj)
        {
            return obj is SessionPatchMetadata other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = ProfileId.GetHashCode();
                hash = (hash * 397) ^ PatchMask.GetHashCode();
                hash = (hash * 397) ^ TargetCadenceX100.GetHashCode();
                hash = (hash * 397) ^ QualityTier.GetHashCode();
                hash = (hash * 397) ^ DegradePolicy.GetHashCode();
                hash = (hash * 397) ^ ActiveLaneMask.GetHashCode();
                hash = (hash * 397) ^ PreferredCodecBitmap.GetHashCode();
                hash = (hash * 397) ^ PreferredCompressionBitmap.GetHashCode();
                hash = (hash * 397) ^ ProfilePatchBytes.GetHashCode();
                hash = (hash * 397) ^ Reserved0.GetHashCode();
                return hash;
            }
        }
    }
}
