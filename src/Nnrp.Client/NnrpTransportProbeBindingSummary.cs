using Nnrp.Core;

namespace Nnrp.Client
{
    public readonly struct NnrpTransportProbeBindingSummary
    {
        public NnrpTransportProbeBindingSummary(
            TransportId transportId,
            string bindingName,
            int successCount,
            int failureCount,
            int warmupSampleCount,
            int scoredSampleCount,
            double medianThroughputBytesPerSecond,
            long medianRttMicroseconds,
            long medianJitterMicroseconds = 0)
        {
            TransportId = transportId;
            BindingName = bindingName;
            SuccessCount = successCount;
            FailureCount = failureCount;
            WarmupSampleCount = warmupSampleCount;
            ScoredSampleCount = scoredSampleCount;
            MedianThroughputBytesPerSecond = medianThroughputBytesPerSecond;
            MedianRttMicroseconds = medianRttMicroseconds;
            MedianJitterMicroseconds = medianJitterMicroseconds;
        }

        public TransportId TransportId { get; }

        public string BindingName { get; }

        public int SuccessCount { get; }

        public int FailureCount { get; }

        public int WarmupSampleCount { get; }

        public int ScoredSampleCount { get; }

        public double MedianThroughputBytesPerSecond { get; }

        public long MedianRttMicroseconds { get; }

        public long MedianJitterMicroseconds { get; }
    }
}
