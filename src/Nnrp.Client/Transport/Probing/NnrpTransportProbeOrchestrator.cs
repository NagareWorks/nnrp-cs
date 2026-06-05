using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nnrp.Core;

namespace Nnrp.Client
{
    public static class NnrpTransportProbeOrchestrator
    {
        public static async ValueTask<NnrpTransportProbeSelectionResult> ProbeAsync(
            IEnumerable<NnrpTransportProbeBinding> bindings,
            NnrpTransportProbeOptions options,
            CancellationToken cancellationToken = default)
        {
            if (bindings == null)
            {
                throw new ArgumentNullException(nameof(bindings));
            }

            options = options ?? throw new ArgumentNullException(nameof(options));
            options.Validate();

            var bindingArray = bindings.ToArray();
            if (bindingArray.Length == 0)
            {
                throw new ArgumentException("At least one probe binding must be supplied.", nameof(bindings));
            }

            var seen = new HashSet<TransportId>();
            foreach (var binding in bindingArray)
            {
                if (binding == null)
                {
                    throw new ArgumentException("Probe bindings must not contain null entries.", nameof(bindings));
                }

                if (!seen.Add(binding.TransportId))
                {
                    throw new ArgumentException($"Duplicate transport binding id {binding.TransportId} is not allowed.", nameof(bindings));
                }
            }

            var tasks = bindingArray.Select(binding => ProbeBindingAsync(binding, options, cancellationToken)).ToArray();
            var summaries = await Task.WhenAll(tasks).ConfigureAwait(false);

            var selected = summaries[0];
            for (var i = 1; i < summaries.Length; i++)
            {
                if (Compare(summaries[i], selected) > 0)
                {
                    selected = summaries[i];
                }
            }

            return new NnrpTransportProbeSelectionResult(
                selected.TransportId,
                selected.BindingName,
                summaries);
        }

        private static async Task<NnrpTransportProbeBindingSummary> ProbeBindingAsync(
            NnrpTransportProbeBinding binding,
            NnrpTransportProbeOptions options,
            CancellationToken cancellationToken)
        {
            var totalSamples = options.WarmupProbeCount + options.ScoredProbeCount;
            var scoredSamples = new List<NnrpTransportProbeSampleResult>(options.ScoredProbeCount);
            var failureCount = 0;

            for (var sampleIndex = 0; sampleIndex < totalSamples; sampleIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var isWarmup = sampleIndex < options.WarmupProbeCount;
                var request = new NnrpTransportProbeRequest(
                    sampleIndex,
                    totalSamples,
                    isWarmup,
                    options.PayloadBytes,
                    options.ProbeTimeout);

                NnrpTransportProbeSampleResult sample;
                try
                {
                    sample = await binding.ProbeAsync(request, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    sample = new NnrpTransportProbeSampleResult(
                        binding.TransportId,
                        binding.BindingName,
                        isSuccess: false,
                        payloadBytes: options.PayloadBytes,
                        roundTripMicroseconds: 0,
                        failureDetail: ex.Message);
                }

                if (!isWarmup)
                {
                    scoredSamples.Add(sample);
                    if (!sample.IsSuccess)
                    {
                        failureCount++;
                    }
                }
            }

            var successfulSamples = scoredSamples.Where(sample => sample.IsSuccess).ToArray();
            return new NnrpTransportProbeBindingSummary(
                binding.TransportId,
                binding.BindingName,
                successCount: successfulSamples.Length,
                failureCount: failureCount,
                warmupSampleCount: options.WarmupProbeCount,
                scoredSampleCount: options.ScoredProbeCount,
                medianThroughputBytesPerSecond: Median(successfulSamples.Select(sample => sample.ThroughputBytesPerSecond)),
                medianRttMicroseconds: successfulSamples.Length == 0 ? 0L : (long)Math.Round(Median(successfulSamples.Select(sample => (double)sample.RoundTripMicroseconds))),
                medianJitterMicroseconds: MedianAbsoluteDelta(successfulSamples.Select(sample => sample.RoundTripMicroseconds)));
        }

        private static int Compare(NnrpTransportProbeBindingSummary left, NnrpTransportProbeBindingSummary right)
        {
            var successComparison = left.SuccessCount.CompareTo(right.SuccessCount);
            if (successComparison != 0)
            {
                return successComparison;
            }

            var throughputComparison = left.MedianThroughputBytesPerSecond.CompareTo(right.MedianThroughputBytesPerSecond);
            if (throughputComparison != 0)
            {
                return throughputComparison;
            }

            var leftComparableRtt = left.SuccessCount > 0 ? left.MedianRttMicroseconds : long.MaxValue;
            var rightComparableRtt = right.SuccessCount > 0 ? right.MedianRttMicroseconds : long.MaxValue;
            return rightComparableRtt.CompareTo(leftComparableRtt);
        }

        private static double Median(IEnumerable<double> values)
        {
            var ordered = values.OrderBy(value => value).ToArray();
            if (ordered.Length == 0)
            {
                return 0d;
            }

            var middle = ordered.Length / 2;
            if ((ordered.Length & 1) == 1)
            {
                return ordered[middle];
            }

            return (ordered[middle - 1] + ordered[middle]) / 2d;
        }

        private static long MedianAbsoluteDelta(IEnumerable<long> values)
        {
            var ordered = values.ToArray();
            if (ordered.Length < 2)
            {
                return 0L;
            }

            var deltas = new double[ordered.Length - 1];
            for (var i = 1; i < ordered.Length; i++)
            {
                deltas[i - 1] = Math.Abs(ordered[i] - ordered[i - 1]);
            }

            return (long)Math.Round(Median(deltas));
        }
    }
}
