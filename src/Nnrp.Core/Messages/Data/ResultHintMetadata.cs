using System;

namespace Nnrp.Core
{
    public readonly struct ResultHintMetadata : IEquatable<ResultHintMetadata>
    {
        public const int MetadataLength = 4 * sizeof(uint);

        public ResultHintMetadata(
            ResultHintBudgetPolicy appliedBudgetPolicy,
            ResultHintCongestionState congestionState,
            ResultHintReason reason,
            uint retryAfterMilliseconds)
        {
            if (!Enum.IsDefined(typeof(ResultHintBudgetPolicy), appliedBudgetPolicy))
            {
                throw new ArgumentOutOfRangeException(nameof(appliedBudgetPolicy));
            }

            if (!Enum.IsDefined(typeof(ResultHintCongestionState), congestionState))
            {
                throw new ArgumentOutOfRangeException(nameof(congestionState));
            }

            if (!Enum.IsDefined(typeof(ResultHintReason), reason))
            {
                throw new ArgumentOutOfRangeException(nameof(reason));
            }

            AppliedBudgetPolicy = appliedBudgetPolicy;
            CongestionState = congestionState;
            Reason = reason;
            RetryAfterMilliseconds = retryAfterMilliseconds;
        }

        public ResultHintBudgetPolicy AppliedBudgetPolicy { get; }

        public ResultHintCongestionState CongestionState { get; }

        public ResultHintReason Reason { get; }

        public uint RetryAfterMilliseconds { get; }

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
            if (!writer.TryWriteUInt32((uint)AppliedBudgetPolicy)
                || !writer.TryWriteUInt32((uint)CongestionState)
                || !writer.TryWriteUInt32((uint)Reason)
                || !writer.TryWriteUInt32(RetryAfterMilliseconds))
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

        public static bool TryParse(ReadOnlySpan<byte> source, out ResultHintMetadata metadata)
        {
            return TryParse(source, strict: false, out metadata, out _);
        }

        public static bool TryParse(ReadOnlySpan<byte> source, out ResultHintMetadata metadata, out NnrpParseError error)
        {
            return TryParse(source, strict: false, out metadata, out error);
        }

        public static bool TryParse(ReadOnlySpan<byte> source, bool strict, out ResultHintMetadata metadata, out NnrpParseError error)
        {
            metadata = default;
            error = NnrpParseError.None;
            if (source.Length < MetadataLength)
            {
                error = NnrpParseError.SourceTooShort;
                return false;
            }

            var reader = new FixedBinaryReader(source);
            if (!reader.TryReadUInt32(out var appliedBudgetPolicy)
                || !reader.TryReadUInt32(out var congestionState)
                || !reader.TryReadUInt32(out var reason)
                || !reader.TryReadUInt32(out var retryAfterMilliseconds))
            {
                error = NnrpParseError.SourceTooShort;
                return false;
            }

            if (!Enum.IsDefined(typeof(ResultHintBudgetPolicy), appliedBudgetPolicy)
                || !Enum.IsDefined(typeof(ResultHintCongestionState), congestionState)
                || !Enum.IsDefined(typeof(ResultHintReason), reason))
            {
                error = NnrpParseError.InvalidMessageLayout;
                return false;
            }

            metadata = new ResultHintMetadata(
                (ResultHintBudgetPolicy)appliedBudgetPolicy,
                (ResultHintCongestionState)congestionState,
                (ResultHintReason)reason,
                retryAfterMilliseconds);
            return true;
        }

        public bool Equals(ResultHintMetadata other)
        {
            return AppliedBudgetPolicy == other.AppliedBudgetPolicy
                && CongestionState == other.CongestionState
                && Reason == other.Reason
                && RetryAfterMilliseconds == other.RetryAfterMilliseconds;
        }

        public override bool Equals(object obj)
        {
            return obj is ResultHintMetadata other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = AppliedBudgetPolicy.GetHashCode();
                hash = (hash * 397) ^ CongestionState.GetHashCode();
                hash = (hash * 397) ^ Reason.GetHashCode();
                hash = (hash * 397) ^ RetryAfterMilliseconds.GetHashCode();
                return hash;
            }
        }
    }
}
