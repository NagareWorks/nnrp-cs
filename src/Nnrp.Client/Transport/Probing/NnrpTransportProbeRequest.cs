using System;

namespace Nnrp.Client
{
    public readonly struct NnrpTransportProbeRequest
    {
        public NnrpTransportProbeRequest(int sampleIndex, int totalSampleCount, bool isWarmup, int payloadBytes, TimeSpan timeout)
        {
            if (sampleIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sampleIndex));
            }

            if (totalSampleCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(totalSampleCount));
            }

            if (payloadBytes < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(payloadBytes));
            }

            if (timeout < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(timeout));
            }

            SampleIndex = sampleIndex;
            TotalSampleCount = totalSampleCount;
            IsWarmup = isWarmup;
            PayloadBytes = payloadBytes;
            Timeout = timeout;
        }

        public int SampleIndex { get; }

        public int TotalSampleCount { get; }

        public bool IsWarmup { get; }

        public int PayloadBytes { get; }

        public TimeSpan Timeout { get; }
    }
}
