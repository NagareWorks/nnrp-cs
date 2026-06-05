using System;

namespace Nnrp.Client
{
    public sealed class NnrpTransportProbeOptions
    {
        public int WarmupProbeCount { get; set; } = 1;

        public int ScoredProbeCount { get; set; } = 3;

        public int PayloadBytes { get; set; } = 16 * 1024;

        public TimeSpan ProbeTimeout { get; set; } = TimeSpan.FromSeconds(2);

        internal void Validate()
        {
            if (WarmupProbeCount < 0)
            {
                throw new InvalidOperationException("WarmupProbeCount must be non-negative.");
            }

            if (ScoredProbeCount <= 0)
            {
                throw new InvalidOperationException("ScoredProbeCount must be positive.");
            }

            if (PayloadBytes < 0)
            {
                throw new InvalidOperationException("PayloadBytes must be non-negative.");
            }

            if (ProbeTimeout < TimeSpan.Zero)
            {
                throw new InvalidOperationException("ProbeTimeout must be non-negative.");
            }
        }
    }
}
