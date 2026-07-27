using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nnrp.Core;
using Nnrp.NativeBridge;

namespace Nnrp.Client
{
    internal sealed class NnrpClientTransportConnection : IAsyncDisposable
    {
        internal NnrpClientTransportConnection(
            NnrpTransportConnection connection,
            NnrpTransportSelection selection)
        {
            Connection = connection ?? throw new ArgumentNullException(nameof(connection));
            Selection = selection ?? throw new ArgumentNullException(nameof(selection));
        }

        internal NnrpTransportConnection Connection { get; }

        internal NnrpTransportSelection Selection { get; }

        public ValueTask DisposeAsync()
        {
            return Connection.DisposeAsync();
        }
    }

    internal sealed class NnrpClientTransportPlan
    {
        internal NnrpClientTransportPlan(
            INnrpNativeTransportProvider provider,
            NnrpTransportConnectOptions connectOptions,
            NnrpTransportSelection selection)
        {
            Provider = provider ?? throw new ArgumentNullException(nameof(provider));
            ConnectOptions = connectOptions ?? throw new ArgumentNullException(nameof(connectOptions));
            Selection = selection ?? throw new ArgumentNullException(nameof(selection));
        }

        internal INnrpNativeTransportProvider Provider { get; }

        internal NnrpTransportConnectOptions ConnectOptions { get; }

        internal NnrpTransportSelection Selection { get; }
    }

    internal static class NnrpClientTransportOrchestrator
    {
        private const ulong MaxPacketBytes = 16 * 1024 * 1024;
        private const uint ProbePayloadBytes = 64 * 1024;
        private const uint ProbeSampleCount = 3;

        internal static async ValueTask<NnrpClientTransportConnection> ConnectAsync(
            NnrpClientOptions options,
            CancellationToken cancellationToken = default)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            var plan = await ResolveAsync(options, cancellationToken).ConfigureAwait(false);
            var connection = await plan.Provider.ConnectAsync(
                plan.ConnectOptions,
                cancellationToken).ConfigureAwait(false);
            if (connection == null)
            {
                throw new InvalidOperationException("The selected transport provider returned a null connection.");
            }

            return new NnrpClientTransportConnection(connection, plan.Selection);
        }

        internal static async ValueTask<NnrpClientTransportPlan> ResolveAsync(
            NnrpClientOptions options,
            CancellationToken cancellationToken = default)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            cancellationToken.ThrowIfCancellationRequested();
            var providers = ResolveProviders(options);
            var registry = new NnrpNativeTransportRegistry(providers);
            var routes = new Dictionary<(TransportId, string), ResolvedRoute>();
            var readiness = new List<NnrpTransportCandidateReadiness>();

            foreach (var provider in registry.Snapshot())
            {
                var descriptor = provider.Descriptor;
                options.ProviderRoutes.TryGetValue(descriptor.TransportId, out var configuredRoute);
                var routeResolved = NnrpTransportRouteResolver.TryResolveClient(
                    options.Endpoint,
                    descriptor.TransportId,
                    configuredRoute,
                    out var providerEndpoint,
                    out var rejectionReason,
                    out var diagnostic);
                var securitySatisfied = rejectionReason != NnrpTransportRejectionReason.SecurityUnsatisfied;
                readiness.Add(new NnrpTransportCandidateReadiness(
                    descriptor.TransportId,
                    descriptor.Metadata.Id,
                    routeResolved || rejectionReason != NnrpTransportRejectionReason.RouteUnresolved,
                    securitySatisfied,
                    diagnostic));
                routes.Add(
                    (descriptor.TransportId, descriptor.Metadata.Id),
                    new ResolvedRoute(providerEndpoint, configuredRoute?.Security, routeResolved));
            }

            var eligible = registry.Snapshot()
                .Where(provider => IsEligible(provider, routes, options.TransportPolicy))
                .ToArray();
            var observations = new List<NnrpTransportProbeObservation>();
            if (eligible.Length > 1)
            {
                foreach (var provider in eligible)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var descriptor = provider.Descriptor;
                    var route = routes[(descriptor.TransportId, descriptor.Metadata.Id)];
                    try
                    {
                        var metrics = await provider.ProbeAsync(
                            new Nnrp.Core.NnrpTransportProbeOptions(
                                options.Endpoint,
                                route.Endpoint!,
                                ProbeSampleCount,
                                ProbePayloadBytes,
                                route.Security,
                                MaxPacketBytes,
                                includeWarmup: true),
                            cancellationToken).ConfigureAwait(false);
                        observations.Add(new NnrpTransportProbeObservation(
                            descriptor.TransportId,
                            descriptor.Metadata.Id,
                            NnrpTransportProbeState.Succeeded,
                            metrics));
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception error)
                    {
                        observations.Add(new NnrpTransportProbeObservation(
                            descriptor.TransportId,
                            descriptor.Metadata.Id,
                            NnrpTransportProbeState.Failed,
                            diagnostic: error.Message));
                    }
                }
            }

            var selection = registry.Resolve(new NnrpTransportSelectionOptions(
                providers.Select(value => value.Descriptor.TransportId),
                readiness,
                options.TransportPolicy,
                requestedMaxFrameBytes: MaxPacketBytes,
                probeObservations: observations));
            var selected = providers.Single(value =>
                value.Descriptor.TransportId == selection.SelectedProvider.TransportId
                && string.Equals(
                    value.Descriptor.Metadata.Id,
                    selection.SelectedProvider.Metadata.Id,
                    StringComparison.Ordinal));
            var selectedRoute = routes[(
                selected.Descriptor.TransportId,
                selected.Descriptor.Metadata.Id)];
            return new NnrpClientTransportPlan(
                selected,
                new NnrpTransportConnectOptions(
                    options.Endpoint,
                    selectedRoute.Endpoint!,
                    selectedRoute.Security,
                    MaxPacketBytes),
                selection);
        }

        private static IReadOnlyList<INnrpNativeTransportProvider> ResolveProviders(NnrpClientOptions options)
        {
            var providers = (options.Transports ?? NnrpNativeTransportDefaults.Discover()).ToList();
            var installed = providers.Select(value => value.Descriptor.TransportId).ToHashSet();
            foreach (var route in options.ProviderRoutes.Keys.Where(value => !installed.Contains(value)))
            {
                providers.Add(new UnavailableProvider(route));
            }

            return providers;
        }

        private static bool IsEligible(
            INnrpNativeTransportProvider provider,
            IReadOnlyDictionary<(TransportId, string), ResolvedRoute> routes,
            TransportPolicy policy)
        {
            var descriptor = provider.Descriptor;
            return descriptor.Available
                && descriptor.Metadata.Limits.MaxFrameBytes >= MaxPacketBytes
                && PolicyAllows(policy, descriptor.TransportId)
                && routes[(descriptor.TransportId, descriptor.Metadata.Id)].IsResolved;
        }

        private static bool PolicyAllows(TransportPolicy policy, TransportId transportId)
        {
            return policy switch
            {
                TransportPolicy.ForceTcp => transportId == TransportId.Tcp,
                TransportPolicy.ForceQuic => transportId == TransportId.Quic,
                TransportPolicy.ForceIpc => transportId == TransportId.Ipc,
                TransportPolicy.ForceWebSocket => transportId == TransportId.WebSocket,
                _ => true,
            };
        }

        private sealed class ResolvedRoute
        {
            internal ResolvedRoute(
                NnrpProviderEndpoint? endpoint,
                NnrpTransportClientSecurity? security,
                bool isResolved)
            {
                Endpoint = endpoint;
                Security = security;
                IsResolved = isResolved;
            }

            internal NnrpProviderEndpoint? Endpoint { get; }

            internal NnrpTransportClientSecurity? Security { get; }

            internal bool IsResolved { get; }
        }

        private sealed class UnavailableProvider : INnrpNativeTransportProvider
        {
            internal UnavailableProvider(TransportId transportId)
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
                        new NnrpTransportProviderLimits(MaxPacketBytes),
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
                Nnrp.Core.NnrpTransportProbeOptions options,
                CancellationToken cancellationToken = default) =>
                throw new InvalidOperationException(Descriptor.Diagnostic);
        }
    }
}
