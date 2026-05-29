using System;

namespace Nnrp.Core
{
    public readonly struct SessionPatchAckMetadata : IEquatable<SessionPatchAckMetadata>
    {
        public const int MetadataLength = 48;

        public SessionPatchAckMetadata(
            SessionPatchAckStatus ackStatus,
            SessionPatchRejectReason rejectReason,
            SessionPatchField appliedPatchMask,
            SessionPatchField rejectedPatchMask,
            uint retryAfterMilliseconds,
            ushort effectiveProfileId,
            uint effectiveTargetCadenceX100,
            ushort effectiveQualityTier,
            ushort effectiveDegradePolicy,
            ulong effectiveLaneMask,
            uint preferredCodecBitmap,
            uint preferredCompressionBitmap,
            uint profilePatchAckBytes,
            ushort reserved0 = 0)
        {
            AckStatus = ackStatus;
            RejectReason = rejectReason;
            AppliedPatchMask = appliedPatchMask;
            RejectedPatchMask = rejectedPatchMask;
            RetryAfterMilliseconds = retryAfterMilliseconds;
            EffectiveProfileId = effectiveProfileId;
            EffectiveTargetCadenceX100 = effectiveTargetCadenceX100;
            EffectiveQualityTier = effectiveQualityTier;
            EffectiveDegradePolicy = effectiveDegradePolicy;
            EffectiveLaneMask = effectiveLaneMask;
            PreferredCodecBitmap = preferredCodecBitmap;
            PreferredCompressionBitmap = preferredCompressionBitmap;
            ProfilePatchAckBytes = profilePatchAckBytes;
            Reserved0 = reserved0;
        }

        public SessionPatchAckStatus AckStatus { get; }
        public SessionPatchRejectReason RejectReason { get; }
        public SessionPatchField AppliedPatchMask { get; }
        public SessionPatchField RejectedPatchMask { get; }
        public uint RetryAfterMilliseconds { get; }
        public ushort EffectiveProfileId { get; }
        public uint EffectiveTargetCadenceX100 { get; }
        public ushort EffectiveQualityTier { get; }
        public ushort EffectiveDegradePolicy { get; }
        public ulong EffectiveLaneMask { get; }
        public uint PreferredCodecBitmap { get; }
        public uint PreferredCompressionBitmap { get; }
        public uint ProfilePatchAckBytes { get; }
        public ushort Reserved0 { get; }

        public uint TargetFpsTimes100 => EffectiveTargetCadenceX100;
        public uint QualityTier => EffectiveQualityTier;
        public uint ActiveViewMaskLow => unchecked((uint)(EffectiveLaneMask & 0xFFFFFFFFUL));
        public uint ActiveViewMaskHigh => unchecked((uint)(EffectiveLaneMask >> 32));

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
            if (!writer.TryWriteUInt16((ushort)AckStatus)
                || !writer.TryWriteUInt16((ushort)RejectReason)
                || !writer.TryWriteUInt32((uint)AppliedPatchMask)
                || !writer.TryWriteUInt32((uint)RejectedPatchMask)
                || !writer.TryWriteUInt32(RetryAfterMilliseconds)
                || !writer.TryWriteUInt16(EffectiveProfileId)
                || !writer.TryWriteUInt16(Reserved0)
                || !writer.TryWriteUInt32(EffectiveTargetCadenceX100)
                || !writer.TryWriteUInt16(EffectiveQualityTier)
                || !writer.TryWriteUInt16(EffectiveDegradePolicy)
                || !writer.TryWriteUInt64(EffectiveLaneMask)
                || !writer.TryWriteUInt32(PreferredCodecBitmap)
                || !writer.TryWriteUInt32(PreferredCompressionBitmap)
                || !writer.TryWriteUInt32(ProfilePatchAckBytes))
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

        public static bool TryParse(ReadOnlySpan<byte> source, out SessionPatchAckMetadata metadata)
        {
            return TryParse(source, out metadata, out _);
        }

        public static bool TryParse(ReadOnlySpan<byte> source, out SessionPatchAckMetadata metadata, out NnrpParseError error)
        {
            metadata = default;
            error = NnrpParseError.None;
            if (source.Length < MetadataLength)
            {
                error = NnrpParseError.SourceTooShort;
                return false;
            }

            var reader = new FixedBinaryReader(source);
            if (!reader.TryReadUInt16(out var ackStatus)
                || !reader.TryReadUInt16(out var rejectReason)
                || !reader.TryReadUInt32(out var appliedPatchMask)
                || !reader.TryReadUInt32(out var rejectedPatchMask)
                || !reader.TryReadUInt32(out var retryAfterMilliseconds)
                || !reader.TryReadUInt16(out var effectiveProfileId)
                || !reader.TryReadUInt16(out var reserved0)
                || !reader.TryReadUInt32(out var effectiveTargetCadenceX100)
                || !reader.TryReadUInt16(out var effectiveQualityTier)
                || !reader.TryReadUInt16(out var effectiveDegradePolicy)
                || !reader.TryReadUInt64(out var effectiveLaneMask)
                || !reader.TryReadUInt32(out var preferredCodecBitmap)
                || !reader.TryReadUInt32(out var preferredCompressionBitmap)
                || !reader.TryReadUInt32(out var profilePatchAckBytes))
            {
                error = NnrpParseError.SourceTooShort;
                return false;
            }

            metadata = new SessionPatchAckMetadata(
                (SessionPatchAckStatus)ackStatus,
                (SessionPatchRejectReason)rejectReason,
                (SessionPatchField)appliedPatchMask,
                (SessionPatchField)rejectedPatchMask,
                retryAfterMilliseconds,
                effectiveProfileId,
                effectiveTargetCadenceX100,
                effectiveQualityTier,
                effectiveDegradePolicy,
                effectiveLaneMask,
                preferredCodecBitmap,
                preferredCompressionBitmap,
                profilePatchAckBytes,
                reserved0);
            return true;
        }

        public bool Equals(SessionPatchAckMetadata other)
        {
            return AckStatus == other.AckStatus
                && RejectReason == other.RejectReason
                && AppliedPatchMask == other.AppliedPatchMask
                && RejectedPatchMask == other.RejectedPatchMask
                && RetryAfterMilliseconds == other.RetryAfterMilliseconds
                && EffectiveProfileId == other.EffectiveProfileId
                && EffectiveTargetCadenceX100 == other.EffectiveTargetCadenceX100
                && EffectiveQualityTier == other.EffectiveQualityTier
                && EffectiveDegradePolicy == other.EffectiveDegradePolicy
                && EffectiveLaneMask == other.EffectiveLaneMask
                && PreferredCodecBitmap == other.PreferredCodecBitmap
                && PreferredCompressionBitmap == other.PreferredCompressionBitmap
                && ProfilePatchAckBytes == other.ProfilePatchAckBytes
                && Reserved0 == other.Reserved0;
        }

        public override bool Equals(object obj)
        {
            return obj is SessionPatchAckMetadata other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = AckStatus.GetHashCode();
                hash = (hash * 397) ^ RejectReason.GetHashCode();
                hash = (hash * 397) ^ AppliedPatchMask.GetHashCode();
                hash = (hash * 397) ^ RejectedPatchMask.GetHashCode();
                hash = (hash * 397) ^ RetryAfterMilliseconds.GetHashCode();
                hash = (hash * 397) ^ EffectiveProfileId.GetHashCode();
                hash = (hash * 397) ^ EffectiveTargetCadenceX100.GetHashCode();
                hash = (hash * 397) ^ EffectiveQualityTier.GetHashCode();
                hash = (hash * 397) ^ EffectiveDegradePolicy.GetHashCode();
                hash = (hash * 397) ^ EffectiveLaneMask.GetHashCode();
                hash = (hash * 397) ^ PreferredCodecBitmap.GetHashCode();
                hash = (hash * 397) ^ PreferredCompressionBitmap.GetHashCode();
                hash = (hash * 397) ^ ProfilePatchAckBytes.GetHashCode();
                hash = (hash * 397) ^ Reserved0.GetHashCode();
                return hash;
            }
        }
    }
}
