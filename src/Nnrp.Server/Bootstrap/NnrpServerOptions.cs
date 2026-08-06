using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Nnrp.Core;
using Nnrp.NativeBridge;

namespace Nnrp.Server
{
    public sealed class NnrpServerSessionPolicyDecision
    {
        private NnrpServerSessionPolicyDecision(bool accepted, SessionErrorCode sessionErrorCode, string? diagnostic)
        {
            Accepted = accepted;
            SessionErrorCode = sessionErrorCode;
            Diagnostic = diagnostic;
        }

        public bool Accepted { get; }

        public SessionErrorCode SessionErrorCode { get; }

        public string? Diagnostic { get; }

        public static NnrpServerSessionPolicyDecision Accept() =>
            new NnrpServerSessionPolicyDecision(true, SessionErrorCode.None, null);

        public static NnrpServerSessionPolicyDecision Reject(
            SessionErrorCode sessionErrorCode,
            string? diagnostic = null)
        {
            if (sessionErrorCode == SessionErrorCode.None)
            {
                throw new ArgumentException("A rejected session requires a session error.", nameof(sessionErrorCode));
            }

            return new NnrpServerSessionPolicyDecision(false, sessionErrorCode, diagnostic);
        }
    }

    public interface INnrpServerSessionPolicy
    {
        ValueTask<NnrpServerSessionPolicyDecision> EvaluateAsync(SessionOpenMetadata open);
    }

    internal sealed class NnrpAcceptValidServerSessionPolicy : INnrpServerSessionPolicy
    {
        internal static NnrpAcceptValidServerSessionPolicy Instance { get; } =
            new NnrpAcceptValidServerSessionPolicy();

        private NnrpAcceptValidServerSessionPolicy()
        {
        }

        public ValueTask<NnrpServerSessionPolicyDecision> EvaluateAsync(SessionOpenMetadata open) =>
            new ValueTask<NnrpServerSessionPolicyDecision>(NnrpServerSessionPolicyDecision.Accept());
    }

    public sealed class NnrpServerSessionOptions
    {
        public NnrpServerSessionOptions(
            IReadOnlyList<ushort>? supportedProfiles = null,
            IReadOnlyList<CacheObjectKind>? supportedCacheObjects = null,
            ulong maxCacheObjects = 0,
            uint maxCacheObjectBytes = 0,
            NnrpSchemaRegistry? schemaRegistry = null,
            uint resumeTokenBytes = 24,
            ushort maxInFlightOperations = 4,
            ushort grantedOperationCredit = 2,
            uint leaseTtlMilliseconds = 30_000,
            uint resumeWindowMilliseconds = 120_000,
            INnrpServerSessionPolicy? applicationPolicy = null)
        {
            var profiles = (supportedProfiles ?? new[] { TypedPayloadProfileId.TokenValue }).ToArray();
            if (profiles.Length == 0)
            {
                throw new ArgumentException("At least one supported profile is required.", nameof(supportedProfiles));
            }

            var cacheObjects = (supportedCacheObjects ?? Array.Empty<CacheObjectKind>()).ToArray();
            if (cacheObjects.Any(value => !Enum.IsDefined(typeof(CacheObjectKind), value)))
            {
                throw new ArgumentException("Supported cache objects contain an unknown object kind.", nameof(supportedCacheObjects));
            }

            if (maxInFlightOperations == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxInFlightOperations));
            }

            if (grantedOperationCredit > maxInFlightOperations)
            {
                throw new ArgumentOutOfRangeException(nameof(grantedOperationCredit));
            }

            SupportedProfiles = new ReadOnlyCollection<ushort>(profiles);
            SupportedCacheObjects = new ReadOnlyCollection<CacheObjectKind>(cacheObjects);
            MaxCacheObjects = maxCacheObjects;
            MaxCacheObjectBytes = maxCacheObjectBytes;
            SchemaRegistry = schemaRegistry ?? NnrpSchemaRegistry.WithStandardProfiles();
            ResumeTokenBytes = resumeTokenBytes;
            MaxInFlightOperations = maxInFlightOperations;
            GrantedOperationCredit = grantedOperationCredit;
            LeaseTtlMilliseconds = leaseTtlMilliseconds;
            ResumeWindowMilliseconds = resumeWindowMilliseconds;
            ApplicationPolicy = applicationPolicy ?? NnrpAcceptValidServerSessionPolicy.Instance;
        }

        public IReadOnlyList<ushort> SupportedProfiles { get; }

        public IReadOnlyList<CacheObjectKind> SupportedCacheObjects { get; }

        public ulong MaxCacheObjects { get; }

        public uint MaxCacheObjectBytes { get; }

        public NnrpSchemaRegistry SchemaRegistry { get; }

        public uint ResumeTokenBytes { get; }

        public ushort MaxInFlightOperations { get; }

        public ushort GrantedOperationCredit { get; }

        public uint LeaseTtlMilliseconds { get; }

        public uint ResumeWindowMilliseconds { get; }

        public INnrpServerSessionPolicy ApplicationPolicy { get; }
    }

    public sealed class NnrpServerAcceptOptions
    {
        public NnrpServerAcceptOptions(uint timeoutMilliseconds = 0)
            : this(sessionId: 0, sessionGeneration: 1, timeoutMilliseconds)
        {
        }

        internal NnrpServerAcceptOptions(
            ulong sessionId,
            uint sessionGeneration,
            uint timeoutMilliseconds = 0)
        {
            if (sessionGeneration == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sessionGeneration));
            }

            SessionId = sessionId;
            SessionGeneration = sessionGeneration;
            TimeoutMilliseconds = timeoutMilliseconds;
        }

        internal ulong SessionId { get; }

        internal uint SessionGeneration { get; }

        public uint TimeoutMilliseconds { get; }
    }

    public sealed class NnrpServerOptions
    {
        public NnrpServerOptions(
            NnrpEndpoint endpoint,
            IReadOnlyDictionary<TransportId, NnrpServerProviderRoute>? providerRoutes = null,
            TransportPolicy transportPolicy = TransportPolicy.Auto,
            NnrpServerSessionOptions? sessionDefaults = null)
            : this(
                endpoint,
                providerRoutes,
                transportPolicy,
                transports: null,
                serverId: 0,
                serverGeneration: 1,
                sessionDefaults)
        {
        }

        internal NnrpServerOptions(
            NnrpEndpoint endpoint,
            IReadOnlyDictionary<TransportId, NnrpServerProviderRoute>? providerRoutes,
            TransportPolicy transportPolicy,
            IReadOnlyList<INnrpNativeTransportProvider>? transports,
            ulong serverId = 0,
            uint serverGeneration = 1,
            NnrpServerSessionOptions? sessionDefaults = null)
        {
            if (!Enum.IsDefined(typeof(TransportPolicy), transportPolicy))
            {
                throw new ArgumentOutOfRangeException(nameof(transportPolicy));
            }

            if (serverGeneration == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(serverGeneration));
            }

            Endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
            ProviderRoutes = NnrpTransportRouteSet.CopyServer(providerRoutes);
            TransportPolicy = transportPolicy;
            ServerId = serverId;
            ServerGeneration = serverGeneration;
            SessionDefaults = sessionDefaults ?? new NnrpServerSessionOptions();
            Transports = transports == null
                ? null
                : new ReadOnlyCollection<INnrpNativeTransportProvider>(transports.ToArray());
            if (Transports != null && Transports.Any(value => value == null))
            {
                throw new ArgumentException("Transport providers must not contain null values.", nameof(transports));
            }
        }

        internal NnrpServerOptions(
            NnrpEndpoint endpoint,
            IReadOnlyList<INnrpNativeTransportProvider>? transports)
            : this(
                endpoint,
                providerRoutes: null,
                TransportPolicy.Auto,
                transports,
                serverId: 0,
                serverGeneration: 1,
                sessionDefaults: null)
        {
        }

        internal NnrpServerOptions(NnrpEndpoint endpoint, uint serverGeneration)
            : this(
                endpoint,
                providerRoutes: null,
                TransportPolicy.Auto,
                transports: null,
                serverId: 0,
                serverGeneration,
                sessionDefaults: null)
        {
        }

        public NnrpEndpoint Endpoint { get; }

        public IReadOnlyDictionary<TransportId, NnrpServerProviderRoute> ProviderRoutes { get; }

        public TransportPolicy TransportPolicy { get; }

        internal IReadOnlyList<INnrpNativeTransportProvider>? Transports { get; }

        internal ulong ServerId { get; }

        internal uint ServerGeneration { get; }

        public NnrpServerSessionOptions SessionDefaults { get; }
    }
}
