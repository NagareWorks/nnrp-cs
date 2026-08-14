using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nnrp.Core;

namespace Nnrp.NativeBridge
{
    public sealed class NnrpNativeTransportRegistry
    {
        private readonly List<INnrpNativeTransportProvider> providers =
            new List<INnrpNativeTransportProvider>();

        public NnrpNativeTransportRegistry()
        {
        }

        public NnrpNativeTransportRegistry(IEnumerable<INnrpNativeTransportProvider> providers)
        {
            if (providers == null)
            {
                throw new ArgumentNullException(nameof(providers));
            }

            foreach (var provider in providers)
            {
                Register(provider);
            }
        }

        public void Register(INnrpNativeTransportProvider provider)
        {
            if (provider == null)
            {
                throw new ArgumentNullException(nameof(provider));
            }

            var descriptor = provider.Descriptor
                ?? throw new ArgumentException("Transport provider must expose a descriptor.", nameof(provider));
            if (providers.Any(value => value.Descriptor.TransportId == descriptor.TransportId))
            {
                throw new ArgumentException(
                    $"Transport {descriptor.TransportId} is already registered.",
                    nameof(provider));
            }

            if (providers.Any(value => string.Equals(
                value.Descriptor.Metadata.Id,
                descriptor.Metadata.Id,
                StringComparison.Ordinal)))
            {
                throw new ArgumentException(
                    $"Provider id {descriptor.Metadata.Id} is already registered.",
                    nameof(provider));
            }

            providers.Add(provider);
        }

        public IReadOnlyList<INnrpNativeTransportProvider> Snapshot()
        {
            return Array.AsReadOnly(providers
                .OrderBy(value => (uint)value.Descriptor.TransportId)
                .ThenBy(value => value.Descriptor.Metadata.Id, StringComparer.Ordinal)
                .ToArray());
        }

        public NnrpTransportSelection Resolve(NnrpTransportSelectionOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            var snapshot = Snapshot();
            var readiness = ValidateEvidence(snapshot, options);
            var observations = options.ProbeObservations.ToDictionary(
                value => (value.TransportId, value.ProviderId),
                value => value);
            var candidates = snapshot.Select(provider =>
            {
                var descriptor = provider.Descriptor;
                var key = (descriptor.TransportId, descriptor.Metadata.Id);
                var route = readiness[key];
                var peerSupported = options.PeerSupportedTransports.Contains(descriptor.TransportId);
                var withinLimits = !options.RequestedMaxFrameBytes.HasValue
                    || options.RequestedMaxFrameBytes.Value <= descriptor.Metadata.Limits.MaxFrameBytes;
                var rejection = RejectionReason(
                    descriptor,
                    route,
                    peerSupported,
                    withinLimits,
                    options.Policy);
                return new CandidateState(
                    provider,
                    new NnrpTransportCandidate(
                        descriptor.TransportId,
                        descriptor.Metadata,
                        descriptor.Available,
                        peerSupported,
                        withinLimits,
                        NnrpTransportProbeState.NotRun,
                        rejectionReason: rejection,
                        diagnostic: route.Diagnostic ?? descriptor.Diagnostic));
            }).ToList();

            var eligible = candidates.Where(value => !value.Candidate.RejectionReason.HasValue).ToList();
            if (eligible.Count == 1)
            {
                var selected = eligible[0];
                selected.Candidate = CopyCandidate(
                    selected.Candidate,
                    NnrpTransportProbeState.NotRun,
                    selectionRank: 0);
                return Selection(selected.Provider, candidates, options.Policy, "Single eligible transport selected directly.");
            }

            foreach (var state in eligible)
            {
                var descriptor = state.Provider.Descriptor;
                if (!observations.TryGetValue((descriptor.TransportId, descriptor.Metadata.Id), out var observation))
                {
                    state.Candidate = CopyCandidate(
                        state.Candidate,
                        NnrpTransportProbeState.Missing,
                        rejectionReason: NnrpTransportRejectionReason.ProbeMissing);
                }
                else if (observation.State == NnrpTransportProbeState.Failed)
                {
                    state.Candidate = CopyCandidate(
                        state.Candidate,
                        NnrpTransportProbeState.Failed,
                        rejectionReason: NnrpTransportRejectionReason.ProbeFailed,
                        diagnostic: observation.Diagnostic);
                }
                else
                {
                    state.Candidate = CopyCandidate(
                        state.Candidate,
                        NnrpTransportProbeState.Succeeded,
                        probe: observation.Metrics,
                        diagnostic: observation.Diagnostic);
                }
            }

            var successful = eligible
                .Where(value => value.Candidate.ProbeState == NnrpTransportProbeState.Succeeded)
                .ToList();
            successful.Sort((left, right) => Compare(left, right, options.Policy));
            for (var index = 0; index < successful.Count; index++)
            {
                successful[index].Candidate = CopyCandidate(
                    successful[index].Candidate,
                    NnrpTransportProbeState.Succeeded,
                    successful[index].Candidate.Probe,
                    checked((uint)index),
                    diagnostic: successful[index].Candidate.Diagnostic);
            }

            if (successful.Count == 0)
            {
                throw SelectionFailure(options.Policy, OrderedCandidates(candidates));
            }

            return Selection(
                successful[0].Provider,
                candidates,
                options.Policy,
                "Transport selected by deterministic probe ordering.");
        }

        private static Dictionary<(TransportId, string), NnrpTransportCandidateReadiness> ValidateEvidence(
            IReadOnlyList<INnrpNativeTransportProvider> providers,
            NnrpTransportSelectionOptions options)
        {
            var providerKeys = providers
                .Select(value => (value.Descriptor.TransportId, value.Descriptor.Metadata.Id))
                .ToHashSet();
            var readiness = new Dictionary<(TransportId, string), NnrpTransportCandidateReadiness>();
            foreach (var record in options.CandidateReadiness)
            {
                var key = (record.TransportId, record.ProviderId);
                if (!providerKeys.Contains(key))
                {
                    throw InvalidEvidence("Candidate readiness contains an unmatched provider identity.");
                }

                if (!readiness.TryAdd(key, record))
                {
                    throw InvalidEvidence("Candidate readiness contains a duplicate provider identity.");
                }
            }

            if (readiness.Count != providerKeys.Count)
            {
                throw InvalidEvidence("Candidate readiness must contain exactly one record for every provider.");
            }

            var observationKeys = new HashSet<(TransportId, string)>();
            foreach (var observation in options.ProbeObservations)
            {
                var key = (observation.TransportId, observation.ProviderId);
                if (!providerKeys.Contains(key))
                {
                    throw InvalidEvidence("Probe observations contain an unmatched provider identity.");
                }

                if (!observationKeys.Add(key))
                {
                    throw InvalidEvidence("Probe observations contain a duplicate provider identity.");
                }
            }

            return readiness;
        }

        private static NnrpTransportRejectionReason? RejectionReason(
            NnrpTransportProviderDescriptor provider,
            NnrpTransportCandidateReadiness readiness,
            bool peerSupported,
            bool withinLimits,
            TransportPolicy policy)
        {
            if (!PolicyAllows(policy, provider.TransportId))
            {
                return NnrpTransportRejectionReason.PolicyDisallowed;
            }

            if (!provider.Available)
            {
                return NnrpTransportRejectionReason.LocalUnavailable;
            }

            if (!peerSupported)
            {
                return NnrpTransportRejectionReason.PeerUnsupported;
            }

            if (!withinLimits)
            {
                return NnrpTransportRejectionReason.LimitExceeded;
            }

            if (!readiness.RouteResolved)
            {
                return NnrpTransportRejectionReason.RouteUnresolved;
            }

            return readiness.SecuritySatisfied
                ? (NnrpTransportRejectionReason?)null
                : NnrpTransportRejectionReason.SecurityUnsatisfied;
        }

        private static NnrpTransportSelection Selection(
            INnrpNativeTransportProvider selected,
            IEnumerable<CandidateState> candidates,
            TransportPolicy policy,
            string diagnostic)
        {
            return new NnrpTransportSelection(
                selected.Descriptor,
                OrderedCandidates(candidates),
                policy,
                diagnostic);
        }

        private static NnrpTransportCandidate[] OrderedCandidates(IEnumerable<CandidateState> candidates)
        {
            return candidates
                .Select(value => value.Candidate)
                .OrderBy(value => value.SelectionRank.HasValue ? 0 : 1)
                .ThenBy(value => value.SelectionRank ?? uint.MaxValue)
                .ThenBy(value => (uint)value.TransportId)
                .ThenBy(value => value.Provider.Id, StringComparer.Ordinal)
                .ToArray();
        }

        private static int Compare(CandidateState left, CandidateState right, TransportPolicy policy)
        {
            var leftProbe = left.Candidate.Probe!.Value;
            var rightProbe = right.Candidate.Probe!.Value;
            var comparison = rightProbe.SuccessCount.CompareTo(leftProbe.SuccessCount);
            if (comparison != 0) return comparison;
            comparison = rightProbe.MedianThroughputBytesPerSecond.CompareTo(leftProbe.MedianThroughputBytesPerSecond);
            if (comparison != 0) return comparison;
            comparison = leftProbe.MedianRttMicroseconds.CompareTo(rightProbe.MedianRttMicroseconds);
            if (comparison != 0) return comparison;

            var leftMetadata = left.Provider.Descriptor.Metadata;
            var rightMetadata = right.Provider.Descriptor.Metadata;
            if (leftMetadata.Cost.ModelId != 0 && leftMetadata.Cost.ModelId == rightMetadata.Cost.ModelId)
            {
                comparison = leftMetadata.Cost.Units.CompareTo(rightMetadata.Cost.Units);
                if (comparison != 0) return comparison;
            }

            comparison = PreferredRank(policy, left.Provider.Descriptor.TransportId)
                .CompareTo(PreferredRank(policy, right.Provider.Descriptor.TransportId));
            if (comparison != 0) return comparison;
            comparison = leftMetadata.PreferenceRank.CompareTo(rightMetadata.PreferenceRank);
            if (comparison != 0) return comparison;
            comparison = ((uint)left.Provider.Descriptor.TransportId)
                .CompareTo((uint)right.Provider.Descriptor.TransportId);
            return comparison != 0
                ? comparison
                : string.CompareOrdinal(leftMetadata.Id, rightMetadata.Id);
        }

        private static int PreferredRank(TransportPolicy policy, TransportId transportId)
        {
            return PreferredTransport(policy) == transportId ? 0 : 1;
        }

        private static bool PolicyAllows(TransportPolicy policy, TransportId transportId)
        {
            var forced = ForcedTransport(policy);
            return !forced.HasValue || forced.Value == transportId;
        }

        private static TransportId? PreferredTransport(TransportPolicy policy)
        {
            switch (policy)
            {
                case TransportPolicy.PreferQuic: return TransportId.Quic;
                case TransportPolicy.PreferTcp: return TransportId.Tcp;
                case TransportPolicy.PreferIpc: return TransportId.Ipc;
                case TransportPolicy.PreferWebSocket: return TransportId.WebSocket;
                default: return null;
            }
        }

        private static TransportId? ForcedTransport(TransportPolicy policy)
        {
            switch (policy)
            {
                case TransportPolicy.ForceQuic: return TransportId.Quic;
                case TransportPolicy.ForceTcp: return TransportId.Tcp;
                case TransportPolicy.ForceIpc: return TransportId.Ipc;
                case TransportPolicy.ForceWebSocket: return TransportId.WebSocket;
                default: return null;
            }
        }

        private static NnrpTransportSelectionException SelectionFailure(
            TransportPolicy policy,
            NnrpTransportCandidate[] candidates)
        {
            var forced = ForcedTransport(policy);
            if (forced.HasValue)
            {
                var candidate = candidates.FirstOrDefault(value => value.TransportId == forced.Value);
                var diagnostic = candidate?.RejectionReason == null
                    ? $"Forced transport is not available: {forced.Value}."
                    : $"Forced transport {forced.Value} was rejected: {candidate.RejectionReason}.";
                return new NnrpTransportSelectionException(
                    NnrpTransportSelectionErrorCode.ForcedTransportUnavailable,
                    diagnostic,
                    policy,
                    transportId: forced.Value,
                    candidates: candidates);
            }

            return new NnrpTransportSelectionException(
                NnrpTransportSelectionErrorCode.NoViableTransport,
                "No viable transport provider remains after applying policy and evidence.",
                policy,
                candidates: candidates);
        }

        private static NnrpTransportSelectionException InvalidEvidence(string diagnostic)
        {
            return new NnrpTransportSelectionException(
                NnrpTransportSelectionErrorCode.InvalidEvidence,
                diagnostic);
        }

        private static NnrpTransportCandidate CopyCandidate(
            NnrpTransportCandidate source,
            NnrpTransportProbeState probeState,
            NnrpTransportProbeMetrics? probe = null,
            uint? selectionRank = null,
            NnrpTransportRejectionReason? rejectionReason = null,
            string? diagnostic = null)
        {
            return new NnrpTransportCandidate(
                source.TransportId,
                source.Provider,
                source.LocalAvailable,
                source.PeerSupported,
                source.WithinLimits,
                probeState,
                probe,
                selectionRank,
                rejectionReason,
                diagnostic ?? source.Diagnostic);
        }

        private sealed class CandidateState
        {
            internal CandidateState(INnrpNativeTransportProvider provider, NnrpTransportCandidate candidate)
            {
                Provider = provider;
                Candidate = candidate;
            }

            internal INnrpNativeTransportProvider Provider { get; }

            internal NnrpTransportCandidate Candidate { get; set; }
        }
    }

    internal sealed class NnrpUnavailableTransportProvider : INnrpNativeTransportProvider
    {
        internal NnrpUnavailableTransportProvider(TransportId transportId, ulong maxFrameBytes)
        {
            Descriptor = new NnrpTransportProviderDescriptor(
                transportId.ToString(),
                "uninstalled",
                transportId,
                NnrpTransportProviderKind.NativeDynamic,
                available: false,
                libraryPath: null,
                new NnrpTransportProviderMetadata(
                    "uninstalled-" + transportId.ToString().ToLowerInvariant(),
                    new NnrpTransportProviderCost(0, 0),
                    ushort.MaxValue,
                    new NnrpTransportProviderLimits(maxFrameBytes),
                    Array.Empty<NnrpTransportProviderLimitation>()),
                "A route is configured but its transport provider package is not installed.");
        }

        public NnrpTransportProviderDescriptor Descriptor { get; }

        public ValueTask<NnrpTransportConnection> ConnectAsync(
            NnrpTransportConnectOptions options,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(Descriptor.Diagnostic);

        public ValueTask<NnrpTransportListener> ListenAsync(
            NnrpTransportListenOptions options,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(Descriptor.Diagnostic);

        public ValueTask<NnrpTransportProbeMetrics> ProbeAsync(
            NnrpTransportProbeOptions options,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(Descriptor.Diagnostic);
    }
}
