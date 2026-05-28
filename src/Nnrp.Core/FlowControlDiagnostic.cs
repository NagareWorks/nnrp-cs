using System;

namespace Nnrp.Core
{
    public readonly struct FlowControlDiagnostic : IEquatable<FlowControlDiagnostic>
    {
        public FlowControlDiagnostic(FlowCreditUpdate creditUpdate)
        {
            ScopeKind = creditUpdate.ScopeKind;
            SessionId = creditUpdate.SessionId;
            OperationId = creditUpdate.OperationId;
            Credit = creditUpdate.Credit;
            UpdateReason = creditUpdate.UpdateReason;
            BackpressureLevel = creditUpdate.BackpressureLevel;
            RetryAfterMilliseconds = creditUpdate.RetryAfterMilliseconds;
            CreditEpoch = creditUpdate.CreditEpoch;
            Flags = creditUpdate.Flags;
        }

        public FlowUpdateScopeKind ScopeKind { get; }

        public uint SessionId { get; }

        public ulong OperationId { get; }

        public ushort Credit { get; }

        public FlowUpdateReason UpdateReason { get; }

        public FlowUpdateBackpressureLevel BackpressureLevel { get; }

        public uint RetryAfterMilliseconds { get; }

        public uint CreditEpoch { get; }

        public FlowUpdateFlags Flags { get; }

        public bool HasCredit => (Flags & FlowUpdateFlags.CreditValid) != 0;

        public bool HasRetryAfter => (Flags & FlowUpdateFlags.RetryAfterValid) != 0;

        public bool IsBackgroundOnly => (Flags & FlowUpdateFlags.BackgroundOnly) != 0;

        public bool IsDrainInFlightOnly => (Flags & FlowUpdateFlags.DrainInFlightOnly) != 0;

        public bool HasDiagnostic => HasRetryAfter
            || IsBackgroundOnly
            || IsDrainInFlightOnly
            || BackpressureLevel != FlowUpdateBackpressureLevel.None
            || UpdateReason == FlowUpdateReason.Reduce
            || UpdateReason == FlowUpdateReason.Pause
            || UpdateReason == FlowUpdateReason.Congestion;

        public bool ShouldPauseNewWork => IsDrainInFlightOnly
            || BackpressureLevel == FlowUpdateBackpressureLevel.Hard
            || UpdateReason == FlowUpdateReason.Pause
            || (HasCredit && Credit == 0);

        public bool ShouldRetryLater => HasRetryAfter;

        public static FlowControlDiagnostic FromCreditUpdate(FlowCreditUpdate creditUpdate)
        {
            return new FlowControlDiagnostic(creditUpdate);
        }

        public bool Equals(FlowControlDiagnostic other)
        {
            return ScopeKind == other.ScopeKind
                && SessionId == other.SessionId
                && OperationId == other.OperationId
                && Credit == other.Credit
                && UpdateReason == other.UpdateReason
                && BackpressureLevel == other.BackpressureLevel
                && RetryAfterMilliseconds == other.RetryAfterMilliseconds
                && CreditEpoch == other.CreditEpoch
                && Flags == other.Flags;
        }

        public override bool Equals(object obj)
        {
            return obj is FlowControlDiagnostic other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = ScopeKind.GetHashCode();
                hash = (hash * 397) ^ SessionId.GetHashCode();
                hash = (hash * 397) ^ OperationId.GetHashCode();
                hash = (hash * 397) ^ Credit.GetHashCode();
                hash = (hash * 397) ^ UpdateReason.GetHashCode();
                hash = (hash * 397) ^ BackpressureLevel.GetHashCode();
                hash = (hash * 397) ^ RetryAfterMilliseconds.GetHashCode();
                hash = (hash * 397) ^ CreditEpoch.GetHashCode();
                hash = (hash * 397) ^ Flags.GetHashCode();
                return hash;
            }
        }
    }
}
