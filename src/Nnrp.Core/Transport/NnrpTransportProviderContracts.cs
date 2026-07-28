using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Nnrp.Core
{
    public sealed class NnrpTransportConnectOptions
    {
        public NnrpTransportConnectOptions(
            NnrpEndpoint endpoint,
            NnrpProviderEndpoint providerEndpoint,
            NnrpTransportClientSecurity? security = null,
            ulong maxPacketBytes = 16 * 1024 * 1024,
            uint timeoutMilliseconds = 0)
        {
            if (maxPacketBytes == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxPacketBytes));
            }

            Endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
            ProviderEndpoint = providerEndpoint ?? throw new ArgumentNullException(nameof(providerEndpoint));
            Security = security;
            MaxPacketBytes = maxPacketBytes;
            TimeoutMilliseconds = timeoutMilliseconds;
        }

        public NnrpEndpoint Endpoint { get; }

        public NnrpProviderEndpoint ProviderEndpoint { get; }

        public NnrpTransportClientSecurity? Security { get; }

        public ulong MaxPacketBytes { get; }

        public uint TimeoutMilliseconds { get; }
    }

    public sealed class NnrpTransportListenOptions
    {
        public NnrpTransportListenOptions(
            NnrpEndpoint endpoint,
            NnrpProviderEndpoint providerEndpoint,
            NnrpTransportServerSecurity? security = null,
            ulong maxPacketBytes = 16 * 1024 * 1024,
            uint timeoutMilliseconds = 0)
        {
            if (maxPacketBytes == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxPacketBytes));
            }

            Endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
            ProviderEndpoint = providerEndpoint ?? throw new ArgumentNullException(nameof(providerEndpoint));
            Security = security;
            MaxPacketBytes = maxPacketBytes;
            TimeoutMilliseconds = timeoutMilliseconds;
        }

        public NnrpEndpoint Endpoint { get; }

        public NnrpProviderEndpoint ProviderEndpoint { get; }

        public NnrpTransportServerSecurity? Security { get; }

        public ulong MaxPacketBytes { get; }

        public uint TimeoutMilliseconds { get; }
    }

    public sealed class NnrpTransportProbeOptions
    {
        public NnrpTransportProbeOptions(
            NnrpEndpoint endpoint,
            NnrpProviderEndpoint providerEndpoint,
            uint sampleCount,
            uint payloadBytes,
            NnrpTransportClientSecurity? security = null,
            ulong maxPacketBytes = 16 * 1024 * 1024,
            uint timeoutMilliseconds = 0,
            bool includeWarmup = false)
        {
            if (sampleCount == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sampleCount));
            }

            if (payloadBytes == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(payloadBytes));
            }

            if (maxPacketBytes == 0 || payloadBytes > maxPacketBytes)
            {
                throw new ArgumentOutOfRangeException(nameof(maxPacketBytes));
            }

            Endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
            ProviderEndpoint = providerEndpoint ?? throw new ArgumentNullException(nameof(providerEndpoint));
            Security = security;
            MaxPacketBytes = maxPacketBytes;
            TimeoutMilliseconds = timeoutMilliseconds;
            SampleCount = sampleCount;
            PayloadBytes = payloadBytes;
            IncludeWarmup = includeWarmup;
        }

        public NnrpEndpoint Endpoint { get; }

        public NnrpProviderEndpoint ProviderEndpoint { get; }

        public NnrpTransportClientSecurity? Security { get; }

        public ulong MaxPacketBytes { get; }

        public uint TimeoutMilliseconds { get; }

        public uint SampleCount { get; }

        public uint PayloadBytes { get; }

        public bool IncludeWarmup { get; }
    }

    public enum NnrpTransportProviderKind
    {
        PureRust = 0,
        NativeDynamic = 1,
        Wasm = 2,
    }

    public readonly record struct NnrpTransportProviderCost
    {
        public NnrpTransportProviderCost(ushort modelId, ulong units)
        {
            if (modelId == 0 && units != 0)
            {
                throw new ArgumentException("Provider cost units must be zero when the model id is unspecified.", nameof(units));
            }

            ModelId = modelId;
            Units = units;
        }

        public ushort ModelId { get; }

        public ulong Units { get; }
    }

    public readonly record struct NnrpTransportProviderLimits
    {
        public NnrpTransportProviderLimits(ulong maxFrameBytes)
        {
            if (maxFrameBytes == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxFrameBytes));
            }

            MaxFrameBytes = maxFrameBytes;
        }

        public ulong MaxFrameBytes { get; }
    }

    public enum NnrpTransportProviderLimitation
    {
        RequiresUdp = 0,
        RequiresTcp = 1,
        LocalHostOnly = 2,
        NativeHostOnly = 3,
        BrowserHostOnly = 4,
        UnixDomainSocket = 5,
        WindowsNamedPipe = 6,
    }

    public sealed class NnrpTransportProviderMetadata
    {
        public NnrpTransportProviderMetadata(
            string id,
            NnrpTransportProviderCost cost,
            ushort preferenceRank,
            NnrpTransportProviderLimits limits,
            IEnumerable<NnrpTransportProviderLimitation> limitations)
        {
            ValidateProviderId(id);
            if (limits.MaxFrameBytes == 0)
            {
                throw new ArgumentException("Provider limits must declare a positive maximum frame size.", nameof(limits));
            }

            if (limitations == null)
            {
                throw new ArgumentNullException(nameof(limitations));
            }

            var ownedLimitations = limitations.ToArray();
            if (ownedLimitations.Any(value => !Enum.IsDefined(typeof(NnrpTransportProviderLimitation), value)))
            {
                throw new ArgumentException("Provider limitations contain an unknown value.", nameof(limitations));
            }

            if (ownedLimitations.Distinct().Count() != ownedLimitations.Length)
            {
                throw new ArgumentException("Provider limitations must not contain duplicates.", nameof(limitations));
            }

            Id = id;
            Cost = cost;
            PreferenceRank = preferenceRank;
            Limits = limits;
            Limitations = Array.AsReadOnly(ownedLimitations);
        }

        public string Id { get; }

        public NnrpTransportProviderCost Cost { get; }

        public ushort PreferenceRank { get; }

        public NnrpTransportProviderLimits Limits { get; }

        public IReadOnlyList<NnrpTransportProviderLimitation> Limitations { get; }

        internal static void ValidateProviderId(string id)
        {
            if (string.IsNullOrEmpty(id) || id.Any(value => value > 0x7f))
            {
                throw new ArgumentException("Provider id must be a non-empty ASCII string.", nameof(id));
            }
        }
    }

    public sealed class NnrpTransportProviderDescriptor
    {
        public NnrpTransportProviderDescriptor(
            string name,
            string version,
            TransportId transportId,
            NnrpTransportProviderKind kind,
            bool available,
            string? libraryPath,
            NnrpTransportProviderMetadata metadata,
            string? diagnostic = null)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Provider name must not be empty.", nameof(name));
            }

            if (string.IsNullOrWhiteSpace(version))
            {
                throw new ArgumentException("Provider version must not be empty.", nameof(version));
            }

            ValidateTransportId(transportId);
            if (!Enum.IsDefined(typeof(NnrpTransportProviderKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }

            Name = name;
            Version = version;
            TransportId = transportId;
            Kind = kind;
            Available = available;
            LibraryPath = libraryPath;
            Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
            Diagnostic = diagnostic;
        }

        public string Name { get; }

        public string Version { get; }

        public TransportId TransportId { get; }

        public NnrpTransportProviderKind Kind { get; }

        public bool Available { get; }

        public string? LibraryPath { get; }

        public NnrpTransportProviderMetadata Metadata { get; }

        public string? Diagnostic { get; }

        internal static void ValidateTransportId(TransportId transportId)
        {
            if (transportId == TransportId.Unspecified || !Enum.IsDefined(typeof(TransportId), transportId))
            {
                throw new ArgumentOutOfRangeException(nameof(transportId));
            }
        }
    }

    public enum NnrpTransportProbeState
    {
        NotRun = 0,
        Succeeded = 1,
        Failed = 2,
        Missing = 3,
    }

    public sealed class NnrpTransportCandidateReadiness
    {
        public NnrpTransportCandidateReadiness(
            TransportId transportId,
            string providerId,
            bool routeResolved,
            bool securitySatisfied,
            string? diagnostic = null)
        {
            NnrpTransportProviderDescriptor.ValidateTransportId(transportId);
            NnrpTransportProviderMetadata.ValidateProviderId(providerId);
            TransportId = transportId;
            ProviderId = providerId;
            RouteResolved = routeResolved;
            SecuritySatisfied = securitySatisfied;
            Diagnostic = diagnostic;
        }

        public TransportId TransportId { get; }

        public string ProviderId { get; }

        public bool RouteResolved { get; }

        public bool SecuritySatisfied { get; }

        public string? Diagnostic { get; }
    }

    public readonly record struct NnrpTransportProbeMetrics
    {
        public NnrpTransportProbeMetrics(
            uint sampleCount,
            uint successCount,
            ulong medianThroughputBytesPerSecond,
            ulong medianRttMicroseconds)
        {
            if (sampleCount == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sampleCount));
            }

            if (successCount == 0 || successCount > sampleCount)
            {
                throw new ArgumentOutOfRangeException(nameof(successCount));
            }

            SampleCount = sampleCount;
            SuccessCount = successCount;
            MedianThroughputBytesPerSecond = medianThroughputBytesPerSecond;
            MedianRttMicroseconds = medianRttMicroseconds;
        }

        public uint SampleCount { get; }

        public uint SuccessCount { get; }

        public ulong MedianThroughputBytesPerSecond { get; }

        public ulong MedianRttMicroseconds { get; }
    }

    public sealed class NnrpTransportProbeObservation
    {
        public NnrpTransportProbeObservation(
            TransportId transportId,
            string providerId,
            NnrpTransportProbeState state,
            NnrpTransportProbeMetrics? metrics = null,
            string? diagnostic = null)
        {
            NnrpTransportProviderDescriptor.ValidateTransportId(transportId);
            NnrpTransportProviderMetadata.ValidateProviderId(providerId);
            if (state != NnrpTransportProbeState.Succeeded && state != NnrpTransportProbeState.Failed)
            {
                throw new ArgumentOutOfRangeException(nameof(state));
            }

            if ((state == NnrpTransportProbeState.Succeeded) != metrics.HasValue)
            {
                throw new ArgumentException(
                    "Succeeded probe observations require metrics and failed observations forbid them.",
                    nameof(metrics));
            }

            TransportId = transportId;
            ProviderId = providerId;
            State = state;
            Metrics = metrics;
            Diagnostic = diagnostic;
        }

        public TransportId TransportId { get; }

        public string ProviderId { get; }

        public NnrpTransportProbeState State { get; }

        public NnrpTransportProbeMetrics? Metrics { get; }

        public string? Diagnostic { get; }
    }

    public enum NnrpTransportRejectionReason
    {
        PolicyDisallowed = 0,
        LocalUnavailable = 1,
        PeerUnsupported = 2,
        LimitExceeded = 3,
        RouteUnresolved = 4,
        SecurityUnsatisfied = 5,
        ProbeMissing = 6,
        ProbeFailed = 7,
    }

    public sealed class NnrpTransportCandidate
    {
        public NnrpTransportCandidate(
            TransportId transportId,
            NnrpTransportProviderMetadata provider,
            bool localAvailable,
            bool peerSupported,
            bool withinLimits,
            NnrpTransportProbeState probeState,
            NnrpTransportProbeMetrics? probe = null,
            uint? selectionRank = null,
            NnrpTransportRejectionReason? rejectionReason = null,
            string? diagnostic = null)
        {
            NnrpTransportProviderDescriptor.ValidateTransportId(transportId);
            if (!Enum.IsDefined(typeof(NnrpTransportProbeState), probeState))
            {
                throw new ArgumentOutOfRangeException(nameof(probeState));
            }

            if (rejectionReason.HasValue
                && !Enum.IsDefined(typeof(NnrpTransportRejectionReason), rejectionReason.Value))
            {
                throw new ArgumentOutOfRangeException(nameof(rejectionReason));
            }

            if ((probeState == NnrpTransportProbeState.Succeeded) != probe.HasValue)
            {
                throw new ArgumentException("Probe metrics must be present exactly when the probe succeeded.", nameof(probe));
            }

            if (rejectionReason.HasValue && selectionRank.HasValue)
            {
                throw new ArgumentException("Rejected candidates must not have a selection rank.", nameof(selectionRank));
            }

            TransportId = transportId;
            Provider = provider ?? throw new ArgumentNullException(nameof(provider));
            LocalAvailable = localAvailable;
            PeerSupported = peerSupported;
            WithinLimits = withinLimits;
            ProbeState = probeState;
            Probe = probe;
            SelectionRank = selectionRank;
            RejectionReason = rejectionReason;
            Diagnostic = diagnostic;
        }

        public TransportId TransportId { get; }

        public NnrpTransportProviderMetadata Provider { get; }

        public bool LocalAvailable { get; }

        public bool PeerSupported { get; }

        public bool WithinLimits { get; }

        public NnrpTransportProbeState ProbeState { get; }

        public NnrpTransportProbeMetrics? Probe { get; }

        public uint? SelectionRank { get; }

        public NnrpTransportRejectionReason? RejectionReason { get; }

        public string? Diagnostic { get; }
    }

    public sealed class NnrpTransportSelection
    {
        public NnrpTransportSelection(
            NnrpTransportProviderDescriptor selectedProvider,
            IEnumerable<NnrpTransportCandidate> candidates,
            TransportPolicy policy,
            string? diagnostic = null)
        {
            SelectedProvider = selectedProvider ?? throw new ArgumentNullException(nameof(selectedProvider));
            if (candidates == null)
            {
                throw new ArgumentNullException(nameof(candidates));
            }

            if (!Enum.IsDefined(typeof(TransportPolicy), policy))
            {
                throw new ArgumentOutOfRangeException(nameof(policy));
            }

            var ownedCandidates = candidates.ToArray();
            if (ownedCandidates.Any(candidate => candidate == null))
            {
                throw new ArgumentException("Selection candidates must not contain null entries.", nameof(candidates));
            }

            if (!ownedCandidates.Any(candidate => candidate.TransportId == selectedProvider.TransportId
                && string.Equals(candidate.Provider.Id, selectedProvider.Metadata.Id, StringComparison.Ordinal)
                && candidate.SelectionRank == 0
                && !candidate.RejectionReason.HasValue))
            {
                throw new ArgumentException("Selection candidates must contain the selected provider at rank zero.", nameof(candidates));
            }

            Candidates = Array.AsReadOnly(ownedCandidates);
            Policy = policy;
            Diagnostic = diagnostic;
        }

        public NnrpTransportProviderDescriptor SelectedProvider { get; }

        public IReadOnlyList<NnrpTransportCandidate> Candidates { get; }

        public TransportPolicy Policy { get; }

        public string? Diagnostic { get; }
    }

    public enum NnrpTransportSelectionErrorCode
    {
        InvalidEvidence = 0,
        ForcedTransportUnavailable = 1,
        NoViableTransport = 2,
    }

    public sealed class NnrpTransportSelectionException : InvalidOperationException
    {
        public NnrpTransportSelectionException(
            NnrpTransportSelectionErrorCode code,
            string diagnostic,
            TransportPolicy? policy = null,
            IEnumerable<NnrpTransportCandidate>? candidates = null)
            : base(diagnostic)
        {
            if (!Enum.IsDefined(typeof(NnrpTransportSelectionErrorCode), code))
            {
                throw new ArgumentOutOfRangeException(nameof(code));
            }

            if (string.IsNullOrWhiteSpace(diagnostic))
            {
                throw new ArgumentException("Selection diagnostic must not be empty.", nameof(diagnostic));
            }

            if (policy.HasValue && !Enum.IsDefined(typeof(TransportPolicy), policy.Value))
            {
                throw new ArgumentOutOfRangeException(nameof(policy));
            }

            var ownedCandidates = candidates?.ToArray() ?? Array.Empty<NnrpTransportCandidate>();
            if (ownedCandidates.Any(candidate => candidate == null))
            {
                throw new ArgumentException("Selection candidates must not contain null entries.", nameof(candidates));
            }

            Code = code;
            Policy = policy;
            Candidates = Array.AsReadOnly(ownedCandidates);
            Diagnostic = diagnostic;
        }

        public NnrpTransportSelectionErrorCode Code { get; }

        public TransportPolicy? Policy { get; }

        public IReadOnlyList<NnrpTransportCandidate> Candidates { get; }

        public string Diagnostic { get; }
    }

    public sealed class NnrpTransportSelectionOptions
    {
        public NnrpTransportSelectionOptions(
            IEnumerable<TransportId> peerSupportedTransports,
            IEnumerable<NnrpTransportCandidateReadiness> candidateReadiness,
            TransportPolicy policy = TransportPolicy.Auto,
            ulong? requestedMaxFrameBytes = null,
            IEnumerable<NnrpTransportProbeObservation>? probeObservations = null)
        {
            if (peerSupportedTransports == null)
            {
                throw new ArgumentNullException(nameof(peerSupportedTransports));
            }

            if (!Enum.IsDefined(typeof(TransportPolicy), policy))
            {
                throw new ArgumentOutOfRangeException(nameof(policy));
            }

            if (requestedMaxFrameBytes == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(requestedMaxFrameBytes));
            }

            if (candidateReadiness == null)
            {
                throw new ArgumentNullException(nameof(candidateReadiness));
            }

            var peers = peerSupportedTransports.Distinct().OrderBy(value => (uint)value).ToArray();
            foreach (var peer in peers)
            {
                NnrpTransportProviderDescriptor.ValidateTransportId(peer);
            }

            var readiness = candidateReadiness.ToArray();
            if (readiness.Any(value => value == null))
            {
                throw new ArgumentException("Candidate readiness must not contain null entries.", nameof(candidateReadiness));
            }

            var observations = probeObservations?.ToArray() ?? Array.Empty<NnrpTransportProbeObservation>();
            if (observations.Any(value => value == null))
            {
                throw new ArgumentException("Probe observations must not contain null entries.", nameof(probeObservations));
            }

            PeerSupportedTransports = Array.AsReadOnly(peers);
            Policy = policy;
            RequestedMaxFrameBytes = requestedMaxFrameBytes;
            CandidateReadiness = Array.AsReadOnly(readiness);
            ProbeObservations = Array.AsReadOnly(observations);
        }

        public IReadOnlyCollection<TransportId> PeerSupportedTransports { get; }

        public TransportPolicy Policy { get; }

        public ulong? RequestedMaxFrameBytes { get; }

        public IReadOnlyCollection<NnrpTransportCandidateReadiness> CandidateReadiness { get; }

        public IReadOnlyCollection<NnrpTransportProbeObservation> ProbeObservations { get; }
    }
}
