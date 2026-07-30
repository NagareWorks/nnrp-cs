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
            uint schemaVersion = TypedPayloadDescriptor.TokenDeltaSchemaVersion)
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

            SessionId = sessionId;
            SessionGeneration = sessionGeneration;
            ProfileId = profileId;
            SchemaId = schemaId;
            SchemaVersion = schemaVersion;
        }

        public uint SessionId { get; }

        public uint SessionGeneration { get; }

        public ushort ProfileId { get; }

        public uint SchemaId { get; }

        public uint SchemaVersion { get; }
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
            Transports = transports == null
                ? null
                : new ReadOnlyCollection<INnrpNativeTransportProvider>(transports.ToArray());
            if (Transports != null && Transports.Any(value => value == null))
            {
                throw new ArgumentException("Transport providers must not contain null values.", nameof(transports));
            }

            SessionDefaults = sessionDefaults;
        }

        public NnrpEndpoint Endpoint { get; }

        public IReadOnlyDictionary<TransportId, NnrpClientProviderRoute> ProviderRoutes { get; }

        public TransportPolicy TransportPolicy { get; }

        public IReadOnlyList<INnrpNativeTransportProvider>? Transports { get; }

        public NnrpClientSessionOptions? SessionDefaults { get; }
    }
}
