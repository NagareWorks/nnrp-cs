using System;
using Nnrp.Core;

namespace Nnrp.Client
{
    public readonly struct NnrpTransportProbeSampleResult
    {
        public NnrpTransportProbeSampleResult(
            TransportId transportId,
            string bindingName,
            bool isSuccess,
            int payloadBytes,
            long roundTripMicroseconds,
            string failureDetail = "")
        {
            if (transportId == TransportId.Unspecified)
            {
                throw new ArgumentOutOfRangeException(nameof(transportId));
            }

            if (string.IsNullOrWhiteSpace(bindingName))
            {
                throw new ArgumentException("Binding name must not be null or empty.", nameof(bindingName));
            }

            if (payloadBytes < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(payloadBytes));
            }

            if (roundTripMicroseconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(roundTripMicroseconds));
            }

            TransportId = transportId;
            BindingName = bindingName;
            IsSuccess = isSuccess;
            PayloadBytes = payloadBytes;
            RoundTripMicroseconds = roundTripMicroseconds;
            FailureDetail = failureDetail ?? string.Empty;
        }

        public TransportId TransportId { get; }

        public string BindingName { get; }

        public bool IsSuccess { get; }

        public int PayloadBytes { get; }

        public long RoundTripMicroseconds { get; }

        public string FailureDetail { get; }

        public double ThroughputBytesPerSecond =>
            IsSuccess && RoundTripMicroseconds > 0
                ? PayloadBytes * 1_000_000d / RoundTripMicroseconds
                : 0d;
    }
}
