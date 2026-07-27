using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Nnrp.Core;
using Nnrp.NativeBridge;

namespace Nnrp.Server
{
    public sealed class NnrpServerAcceptOptions
    {
        public NnrpServerAcceptOptions(
            uint sessionId = 0,
            uint sessionGeneration = 1,
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

        public uint SessionId { get; }

        public uint SessionGeneration { get; }

        public uint TimeoutMilliseconds { get; }
    }

    public sealed class NnrpServerOptions
    {
        public NnrpServerOptions(
            NnrpEndpoint endpoint,
            IReadOnlyDictionary<TransportId, NnrpServerProviderRoute>? providerRoutes = null,
            TransportPolicy transportPolicy = TransportPolicy.Auto,
            IReadOnlyList<INnrpNativeTransportProvider>? transports = null,
            ulong serverId = 0,
            uint serverGeneration = 1)
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
            Transports = transports == null
                ? null
                : new ReadOnlyCollection<INnrpNativeTransportProvider>(transports.ToArray());
            if (Transports != null && Transports.Any(value => value == null))
            {
                throw new ArgumentException("Transport providers must not contain null values.", nameof(transports));
            }

            ServerId = serverId;
            ServerGeneration = serverGeneration;
        }

        public NnrpEndpoint Endpoint { get; }

        public IReadOnlyDictionary<TransportId, NnrpServerProviderRoute> ProviderRoutes { get; }

        public TransportPolicy TransportPolicy { get; }

        public IReadOnlyList<INnrpNativeTransportProvider>? Transports { get; }

        public ulong ServerId { get; }

        public uint ServerGeneration { get; }
    }
}
