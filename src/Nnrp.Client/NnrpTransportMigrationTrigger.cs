using System;
using System.Linq;
using Nnrp.Core;

namespace Nnrp.Client
{
    public static class NnrpTransportMigrationTrigger
    {
        public static NnrpTransportMigrationDecision Evaluate(
            TransportId currentTransportId,
            NnrpTransportProbeSelectionResult probeSelection,
            NnrpTransportMigrationTriggerOptions? options = null)
        {
            if (currentTransportId == TransportId.Unspecified)
            {
                throw new ArgumentOutOfRangeException(nameof(currentTransportId));
            }

            options ??= new NnrpTransportMigrationTriggerOptions();
            options.Validate();

            var current = FindSummary(probeSelection, currentTransportId, nameof(currentTransportId));
            var target = FindSummary(probeSelection, probeSelection.SelectedTransportId, nameof(probeSelection));
            if (target.TransportId == currentTransportId)
            {
                return new NnrpTransportMigrationDecision(
                    shouldMigrate: false,
                    currentTransportId: current.TransportId,
                    currentBindingName: current.BindingName,
                    targetTransportId: target.TransportId,
                    targetBindingName: target.BindingName,
                    triggerMetric: NnrpTransportMigrationTriggerMetric.None);
            }

            if (options.RequireCandidateToMatchOrExceedSuccessCount && target.SuccessCount < current.SuccessCount)
            {
                return NoMigration(current, target);
            }

            if (options.TriggerOnFailureRegression && target.FailureCount < current.FailureCount)
            {
                return Migrate(current, target, NnrpTransportMigrationTriggerMetric.FailureRegression);
            }

            if (current.SuccessCount < options.MinimumComparableSuccessCount || target.SuccessCount < options.MinimumComparableSuccessCount)
            {
                return NoMigration(current, target);
            }

            if (current.MedianThroughputBytesPerSecond > 0d
                && target.MedianThroughputBytesPerSecond >= current.MedianThroughputBytesPerSecond * options.MinimumThroughputGainRatio)
            {
                return Migrate(current, target, NnrpTransportMigrationTriggerMetric.Throughput);
            }

            if (current.MedianRttMicroseconds > 0
                && target.MedianRttMicroseconds > 0
                && target.MedianRttMicroseconds <= (long)Math.Floor(current.MedianRttMicroseconds * options.MaximumRttRatio))
            {
                return Migrate(current, target, NnrpTransportMigrationTriggerMetric.RoundTripTime);
            }

            if (current.MedianJitterMicroseconds > 0
                && target.MedianJitterMicroseconds <= (long)Math.Floor(current.MedianJitterMicroseconds * options.MaximumJitterRatio))
            {
                return Migrate(current, target, NnrpTransportMigrationTriggerMetric.Jitter);
            }

            return NoMigration(current, target);
        }

        private static NnrpTransportProbeBindingSummary FindSummary(
            NnrpTransportProbeSelectionResult probeSelection,
            TransportId transportId,
            string paramName)
        {
            var match = probeSelection.Summaries.FirstOrDefault(summary => summary.TransportId == transportId);
            if (match.TransportId == TransportId.Unspecified)
            {
                throw new ArgumentException($"Probe selection does not include summary for transport {transportId}.", paramName);
            }

            return match;
        }

        private static NnrpTransportMigrationDecision NoMigration(
            NnrpTransportProbeBindingSummary current,
            NnrpTransportProbeBindingSummary target)
        {
            return new NnrpTransportMigrationDecision(
                shouldMigrate: false,
                currentTransportId: current.TransportId,
                currentBindingName: current.BindingName,
                targetTransportId: target.TransportId,
                targetBindingName: target.BindingName,
                triggerMetric: NnrpTransportMigrationTriggerMetric.None);
        }

        private static NnrpTransportMigrationDecision Migrate(
            NnrpTransportProbeBindingSummary current,
            NnrpTransportProbeBindingSummary target,
            NnrpTransportMigrationTriggerMetric metric)
        {
            return new NnrpTransportMigrationDecision(
                shouldMigrate: true,
                currentTransportId: current.TransportId,
                currentBindingName: current.BindingName,
                targetTransportId: target.TransportId,
                targetBindingName: target.BindingName,
                triggerMetric: metric);
        }
    }
}