using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Nnrp.Core;
using Nnrp.NativeBridge;

namespace Nnrp.Client
{
    public sealed class NnrpClientSessionOptions
    {
        public NnrpClientSessionOptions(
            uint sessionId = 0,
            uint sessionGeneration = 1,
            ushort profileId = TypedPayloadProfileId.TokenValue,
            uint schemaId = TypedPayloadDescriptor.TokenDeltaSchemaId,
            uint schemaVersion = TypedPayloadDescriptor.TokenDeltaSchemaVersion,
            SessionPriorityClass priorityClass = SessionPriorityClass.Balanced,
            uint defaultDeadlineMilliseconds = 500,
            ushort maxInFlightOperations = 4,
            uint leaseTtlHintMilliseconds = 30_000,
            bool allowResume = false,
            uint resumeTokenBytes = 0,
            IReadOnlyList<CacheObjectKind>? cacheHints = null)
        {
            if (sessionGeneration == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sessionGeneration));
            }

            if (schemaId != 0 && schemaVersion == 0)
            {
                throw new ArgumentException(
                    "An explicit schema id requires a non-zero schema version.",
                    nameof(schemaVersion));
            }

            if (!Enum.IsDefined(typeof(SessionPriorityClass), priorityClass))
            {
                throw new ArgumentOutOfRangeException(nameof(priorityClass));
            }

            if (maxInFlightOperations == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxInFlightOperations));
            }

            if (!allowResume && resumeTokenBytes != 0)
            {
                throw new ArgumentException(
                    "Resume token capacity requires session recovery to be enabled.",
                    nameof(resumeTokenBytes));
            }

            var copiedHints = (cacheHints ?? Array.Empty<CacheObjectKind>()).ToArray();
            if (copiedHints.Any(value => !Enum.IsDefined(typeof(CacheObjectKind), value)))
            {
                throw new ArgumentException("Cache hints contain an unknown object kind.", nameof(cacheHints));
            }

            SessionId = sessionId;
            SessionGeneration = sessionGeneration;
            ProfileId = profileId;
            SchemaId = schemaId;
            SchemaVersion = schemaVersion;
            PriorityClass = priorityClass;
            DefaultDeadlineMilliseconds = defaultDeadlineMilliseconds;
            MaxInFlightOperations = maxInFlightOperations;
            LeaseTtlHintMilliseconds = leaseTtlHintMilliseconds;
            AllowResume = allowResume;
            ResumeTokenBytes = resumeTokenBytes;
            CacheHints = new ReadOnlyCollection<CacheObjectKind>(copiedHints);
        }

        public uint SessionId { get; }

        public uint SessionGeneration { get; }

        public ushort ProfileId { get; }

        public uint SchemaId { get; }

        public uint SchemaVersion { get; }

        public SessionPriorityClass PriorityClass { get; }

        public uint DefaultDeadlineMilliseconds { get; }

        public ushort MaxInFlightOperations { get; }

        public uint LeaseTtlHintMilliseconds { get; }

        public bool AllowResume { get; }

        public uint ResumeTokenBytes { get; }

        public IReadOnlyList<CacheObjectKind> CacheHints { get; }
    }

    public sealed class NnrpClientOptions
    {
        public NnrpClientOptions(
            NnrpEndpoint endpoint,
            IReadOnlyDictionary<TransportId, NnrpClientProviderRoute>? providerRoutes = null,
            TransportPolicy transportPolicy = TransportPolicy.Auto,
            IReadOnlyList<INnrpNativeTransportProvider>? transports = null,
            NnrpClientSessionOptions? sessionDefaults = null)
        {
            if (!Enum.IsDefined(typeof(TransportPolicy), transportPolicy))
            {
                throw new ArgumentOutOfRangeException(nameof(transportPolicy));
            }

            Endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
            ProviderRoutes = NnrpTransportRouteSet.CopyClient(providerRoutes);
            TransportPolicy = transportPolicy;
            SessionDefaults = sessionDefaults ?? new NnrpClientSessionOptions();
            Transports = transports == null
                ? null
                : new ReadOnlyCollection<INnrpNativeTransportProvider>(transports.ToArray());
            if (Transports != null && Transports.Any(value => value == null))
            {
                throw new ArgumentException("Transport providers must not contain null values.", nameof(transports));
            }
        }

        public NnrpEndpoint Endpoint { get; }

        public IReadOnlyDictionary<TransportId, NnrpClientProviderRoute> ProviderRoutes { get; }

        public TransportPolicy TransportPolicy { get; }

        public NnrpClientSessionOptions SessionDefaults { get; }

        public IReadOnlyList<INnrpNativeTransportProvider>? Transports { get; }
    }
}
