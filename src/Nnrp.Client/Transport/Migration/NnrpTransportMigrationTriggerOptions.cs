using System;

namespace Nnrp.Client
{
    public sealed class NnrpTransportMigrationTriggerOptions
    {
        public int MinimumComparableSuccessCount { get; set; } = 2;

        public bool TriggerOnFailureRegression { get; set; } = true;

        public bool RequireCandidateToMatchOrExceedSuccessCount { get; set; } = true;

        public double MinimumThroughputGainRatio { get; set; } = 1.2d;

        public double MaximumRttRatio { get; set; } = 0.85d;

        public double MaximumJitterRatio { get; set; } = 0.75d;

        internal void Validate()
        {
            if (MinimumComparableSuccessCount < 1)
            {
                throw new InvalidOperationException("MinimumComparableSuccessCount must be positive.");
            }

            if (MinimumThroughputGainRatio <= 1d)
            {
                throw new InvalidOperationException("MinimumThroughputGainRatio must be greater than 1.");
            }

            if (MaximumRttRatio <= 0d || MaximumRttRatio > 1d)
            {
                throw new InvalidOperationException("MaximumRttRatio must be greater than 0 and at most 1.");
            }

            if (MaximumJitterRatio <= 0d || MaximumJitterRatio > 1d)
            {
                throw new InvalidOperationException("MaximumJitterRatio must be greater than 0 and at most 1.");
            }
        }
    }
}
