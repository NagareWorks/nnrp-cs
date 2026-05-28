using System;

namespace Nnrp.Core
{
    public readonly struct FlowCreditUpdate : IEquatable<FlowCreditUpdate>
    {
        private const FlowUpdateFlags AllowedFlags = FlowUpdateFlags.CreditValid
            | FlowUpdateFlags.RetryAfterValid
            | FlowUpdateFlags.BackgroundOnly
            | FlowUpdateFlags.DrainInFlightOnly;

        public FlowCreditUpdate(
            FlowUpdateScopeKind scopeKind,
            uint sessionId,
            ulong operationId,
            ushort credit,
            FlowUpdateReason updateReason,
            FlowUpdateBackpressureLevel backpressureLevel,
            uint retryAfterMilliseconds,
            uint creditEpoch,
            FlowUpdateFlags flags)
        {
            ValidateTarget(scopeKind, sessionId, operationId);

            if (!Enum.IsDefined(typeof(FlowUpdateReason), updateReason))
            {
                throw new ArgumentOutOfRangeException(nameof(updateReason));
            }

            if (!Enum.IsDefined(typeof(FlowUpdateBackpressureLevel), backpressureLevel))
            {
                throw new ArgumentOutOfRangeException(nameof(backpressureLevel));
            }

            if (((uint)flags & ~(uint)AllowedFlags) != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(flags));
            }

            if (retryAfterMilliseconds != 0 && (flags & FlowUpdateFlags.RetryAfterValid) == 0)
            {
                throw new ArgumentException("retryAfterMilliseconds requires FlowUpdateFlags.RetryAfterValid.", nameof(retryAfterMilliseconds));
            }

            ScopeKind = scopeKind;
            SessionId = sessionId;
            OperationId = operationId;
            Credit = credit;
            UpdateReason = updateReason;
            BackpressureLevel = backpressureLevel;
            RetryAfterMilliseconds = retryAfterMilliseconds;
            CreditEpoch = creditEpoch;
            Flags = flags;
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

        public static FlowCreditUpdate FromMessage(FlowUpdateMessage message)
        {
            return FromMetadata(message.Header.SessionId, message.Metadata);
        }

        public static FlowCreditUpdate FromMetadata(uint sessionId, FlowUpdateMetadata metadata)
        {
            var operationId = metadata.ScopeKind == FlowUpdateScopeKind.Operation
                ? metadata.OperationId
                : 0ul;
            return new FlowCreditUpdate(
                metadata.ScopeKind,
                sessionId,
                operationId,
                SelectCredit(metadata),
                metadata.UpdateReason,
                metadata.BackpressureLevel,
                metadata.RetryAfterMilliseconds,
                metadata.CreditEpoch,
                metadata.Flags);
        }

        public bool Equals(FlowCreditUpdate other)
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
            return obj is FlowCreditUpdate other && Equals(other);
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

        private static ushort SelectCredit(FlowUpdateMetadata metadata)
        {
            if (metadata.ScopeKind == FlowUpdateScopeKind.Connection)
            {
                return metadata.ConnectionCredit;
            }

            if (metadata.ScopeKind == FlowUpdateScopeKind.Session)
            {
                return metadata.SessionCredit;
            }

            return metadata.OperationCredit;
        }

        private static void ValidateTarget(FlowUpdateScopeKind scopeKind, uint sessionId, ulong operationId)
        {
            if (!Enum.IsDefined(typeof(FlowUpdateScopeKind), scopeKind))
            {
                throw new ArgumentOutOfRangeException(nameof(scopeKind));
            }

            switch (scopeKind)
            {
                case FlowUpdateScopeKind.Connection:
                    if (sessionId != 0 || operationId != 0)
                    {
                        throw new ArgumentException("Connection-scope credit updates must not target a session or operation.");
                    }

                    break;
                case FlowUpdateScopeKind.Session:
                    if (sessionId == 0 || operationId != 0)
                    {
                        throw new ArgumentException("Session-scope credit updates require a session id and no operation id.");
                    }

                    break;
                case FlowUpdateScopeKind.Operation:
                    if (sessionId == 0 || operationId == 0)
                    {
                        throw new ArgumentException("Operation-scope credit updates require session and operation ids.");
                    }

                    break;
            }
        }
    }
}
