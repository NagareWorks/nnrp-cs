using System;
using Nnrp.Core;

namespace Nnrp.Client
{
    public enum NnrpTransportMigrationTriggerMetric
    {
        None = 0,
        FailureRegression = 1,
        Throughput = 2,
        RoundTripTime = 3,
        Jitter = 4,
    }

    public readonly struct NnrpTransportMigrationDecision
    {
        public NnrpTransportMigrationDecision(
            bool shouldMigrate,
            TransportId currentTransportId,
            string currentBindingName,
            TransportId targetTransportId,
            string targetBindingName,
            NnrpTransportMigrationTriggerMetric triggerMetric)
        {
            if (currentTransportId == TransportId.Unspecified)
            {
                throw new ArgumentOutOfRangeException(nameof(currentTransportId));
            }

            if (targetTransportId == TransportId.Unspecified)
            {
                throw new ArgumentOutOfRangeException(nameof(targetTransportId));
            }

            if (string.IsNullOrWhiteSpace(currentBindingName))
            {
                throw new ArgumentException("Current binding name must not be null or empty.", nameof(currentBindingName));
            }

            if (string.IsNullOrWhiteSpace(targetBindingName))
            {
                throw new ArgumentException("Target binding name must not be null or empty.", nameof(targetBindingName));
            }

            ShouldMigrate = shouldMigrate;
            CurrentTransportId = currentTransportId;
            CurrentBindingName = currentBindingName;
            TargetTransportId = targetTransportId;
            TargetBindingName = targetBindingName;
            TriggerMetric = triggerMetric;
        }

        public bool ShouldMigrate { get; }

        public TransportId CurrentTransportId { get; }

        public string CurrentBindingName { get; }

        public TransportId TargetTransportId { get; }

        public string TargetBindingName { get; }

        public NnrpTransportMigrationTriggerMetric TriggerMetric { get; }
    }

    public readonly struct NnrpClientMigrationResult
    {
        public NnrpClientMigrationResult(
            NnrpTransportMigrationDecision decision,
            SessionMigrateAckMessage ackMessage,
            bool wasMigrated)
        {
            Decision = decision;
            AckMessage = ackMessage;
            WasMigrated = wasMigrated;
        }

        public NnrpTransportMigrationDecision Decision { get; }

        public SessionMigrateAckMessage AckMessage { get; }

        public bool WasMigrated { get; }
    }
}