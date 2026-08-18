using System.Diagnostics.CodeAnalysis;
using Nnrp.Client;
using Nnrp.Core;
using Nnrp.NativeBridge;
using Nnrp.Runtime;
using Nnrp.Server;
using Nnrp.Transport.Ipc;
using Nnrp.Transport.Quic;
using Nnrp.Transport.Tcp;
using Nnrp.Transport.WebSocket;

namespace Nnrp.WireConformance;

[ExcludeFromCodeCoverage]
internal sealed class WireHostRouteDriver
{
    private const ulong MaxPacketBytes = 16 * 1024 * 1024;
    private const uint TransportTerminalStateDetail = 105;
    private const uint TransportIoDetail = 106;
    private const string ReadySchema =
        "https://github.com/NagareWorks/nnrp-conformance/schemas/wire-host-route-ready.schema.json";

    internal async Task<WireHostRouteCaseResult> RunAsync(
        WireHostRouteScenario scenario,
        WireHostRouteScenario resolved,
        WireHostRouteCommandOptions options,
        CancellationToken cancellationToken)
    {
        ValidateScenarioPair(scenario, resolved);
        return scenario.HostRoute!.Role switch
        {
            "client" => await RunClientAsync(scenario, resolved, options, cancellationToken)
                .ConfigureAwait(false),
            "server" => await RunServerAsync(scenario, resolved, options, cancellationToken)
                .ConfigureAwait(false),
            _ => throw new InvalidOperationException(
                $"Unsupported host-route role: {scenario.HostRoute.Role}"),
        };
    }

    private static async Task<WireHostRouteCaseResult> RunClientAsync(
        WireHostRouteScenario scenario,
        WireHostRouteScenario resolved,
        WireHostRouteCommandOptions options,
        CancellationToken cancellationToken)
    {
        WireHostRouteFixture fixture = scenario.HostRoute!;
        IReadOnlyDictionary<TransportId, NnrpClientProviderRoute> routes = fixture.Routes
            .Zip(resolved.HostRoute!.Routes)
            .ToDictionary(
                pair => TransportIdOf(pair.First.Transport),
                pair => ClientRoute(pair.First, pair.Second, options.ArtifactsPath));
        IReadOnlyList<INnrpNativeTransportProvider> providers = fixture.Routes
            .Select(ClientProvider)
            .ToArray();
        NnrpClientOptions clientOptions = new(
            NnrpEndpoint.Parse(fixture.ApplicationEndpoint),
            routes,
            TransportPolicy.Auto,
            providers);
        NnrpClient? client = null;
        NnrpClientSession? session = null;
        try
        {
            client = await NnrpClient.ConnectAsync(
                clientOptions,
                cancellationToken).ConfigureAwait(false);
            session = await client.OpenSessionAsync(
                cancellationToken: cancellationToken).ConfigureAwait(false);
            return WireHostRouteCommand.Passed(
                scenario.Id,
                "success",
                ClientEvidence(fixture, client.Selection.Candidates, client.Selection.SelectedProvider.Metadata.Id),
                "independent C# target executed the public multi-route client API");
        }
        catch (NnrpTransportSelectionException error)
        {
            return WireHostRouteCommand.Passed(
                scenario.Id,
                "error",
                ClientEvidence(fixture, error.Candidates, null),
                error.Diagnostic);
        }
        finally
        {
            await DisposeAfterPeerCloseAsync(session).ConfigureAwait(false);
            await DisposeAfterPeerCloseAsync(client).ConfigureAwait(false);
        }
    }

    private static async Task<WireHostRouteCaseResult> RunServerAsync(
        WireHostRouteScenario scenario,
        WireHostRouteScenario resolved,
        WireHostRouteCommandOptions commandOptions,
        CancellationToken cancellationToken)
    {
        WireHostRouteFixture fixture = scenario.HostRoute!;
        bool bindFailure = fixture.Routes.Any(route => HasFailure(route, "bind_failure"));
        WireHostRoute? terminalFailure = fixture.Routes.FirstOrDefault(
            route => HasFailure(route, "terminal_listener_failure"));
        IReadOnlyDictionary<TransportId, NnrpServerProviderRoute> routes = fixture.Routes
            .Zip(resolved.HostRoute!.Routes)
            .ToDictionary(
                pair => TransportIdOf(pair.First.Transport),
                pair => ServerRoute(pair.First, pair.Second, commandOptions.ArtifactsPath));
        IReadOnlyList<INnrpNativeTransportProvider> providers = fixture.Routes
            .Select(ServerProvider)
            .ToArray();
        NnrpServerOptions serverOptions = new(
            NnrpEndpoint.Parse(fixture.ApplicationEndpoint),
            routes,
            bindFailure ? PreferOpenedRoute(fixture.Routes) : TransportPolicy.Auto,
            providers);

        if (bindFailure || terminalFailure is not null)
        {
            return await RunInjectedServerAsync(
                scenario,
                serverOptions,
                bindFailure,
                terminalFailure,
                commandOptions.ReadyOutputPath,
                cancellationToken).ConfigureAwait(false);
        }

        await using NnrpServer server = await NnrpServer.ListenAsync(
            serverOptions,
            cancellationToken).ConfigureAwait(false);
        WriteReady(scenario, fixture, server.BoundProviderEndpoints, commandOptions.ReadyOutputPath);
        List<TransportId> accepted = [];
        for (int index = 0; index < fixture.Routes.Count; index++)
        {
            await using NnrpServerSession session = await server.AcceptAsync(
                new NnrpServerAcceptOptions(timeoutMilliseconds: 15_000),
                cancellationToken).ConfigureAwait(false);
            accepted.Add(session.ActiveTransportId);
            await WaitForPeerCloseAsync(session, cancellationToken).ConfigureAwait(false);
        }

        return WireHostRouteCommand.Passed(
            scenario.Id,
            "success",
            ServerEvidence(fixture, server.BoundProviderEndpoints, accepted, "accepted"),
            "independent C# target executed the public multi-listener server API");
    }

    private static async Task WaitForPeerCloseAsync(
        NnrpServerSession session,
        CancellationToken cancellationToken)
    {
        NnrpRuntimeEvent close = (await session.NextEventAsync(cancellationToken).ConfigureAwait(false)).Match(
            _ => throw new InvalidOperationException("Expected SESSION_CLOSE, not a submit event."),
            runtime => runtime,
            _ => throw new InvalidOperationException("Expected SESSION_CLOSE, not a lifecycle event."));
        if (close.Header.MessageType != MessageType.SessionClose)
        {
            throw new InvalidOperationException(
                $"Expected SESSION_CLOSE after host-route acceptance, got {close.Header.MessageType}.");
        }
    }

    private static async Task<WireHostRouteCaseResult> RunInjectedServerAsync(
        WireHostRouteScenario scenario,
        NnrpServerOptions options,
        bool bindFailure,
        WireHostRoute? terminalFailure,
        string readyOutputPath,
        CancellationToken cancellationToken)
    {
        WireHostRouteFixture fixture = scenario.HostRoute!;
        try
        {
            NnrpServerTransportListenerSet listeners = await NnrpServerTransportOrchestrator.ListenAsync(
                options,
                cancellationToken,
                InjectingBinder(fixture)).ConfigureAwait(false);
            await using NnrpServer server = new(options, listeners);
            if (bindFailure)
            {
                throw new InvalidOperationException("Injected bind failure did not fail the listener set.");
            }

            WriteReady(scenario, fixture, server.BoundProviderEndpoints, readyOutputPath);
            Exception failure;
            try
            {
                await using NnrpServerSession session = await server.AcceptAsync(
                    new NnrpServerAcceptOptions(timeoutMilliseconds: 15_000),
                    cancellationToken).ConfigureAwait(false);
                throw new InvalidOperationException("Injected terminal listener accepted a session.");
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                failure = error;
            }

            if (!server.IsClosed)
            {
                throw new InvalidOperationException(
                    "Terminal listener failure did not close the logical listener set.");
            }

            return WireHostRouteCommand.Passed(
                scenario.Id,
                "error",
                ServerEvidence(
                    fixture,
                    server.BoundProviderEndpoints,
                    [],
                    "closed",
                    logicalSetClosed: true,
                    terminalFailure: terminalFailure!.ProviderId),
                $"listener set closed after terminal failure: {failure.Message}");
        }
        catch (InjectedBindFailureException error) when (bindFailure)
        {
            return WireHostRouteCommand.Passed(
                scenario.Id,
                "error",
                RollbackEvidence(fixture),
                $"listener bind failed and prior listeners rolled back: {error.Message}");
        }
    }

    private static NnrpServerTransportBinder InjectingBinder(WireHostRouteFixture fixture) =>
        async (provider, listenOptions, serverOptions, cancellationToken) =>
        {
            WireHostRoute route = fixture.Routes.Single(
                candidate => candidate.ProviderId == provider.Descriptor.Metadata.Id);
            if (HasFailure(route, "bind_failure"))
            {
                throw new InjectedBindFailureException();
            }

            if (HasFailure(route, "terminal_listener_failure"))
            {
                return new TerminalFailureListener(
                    provider.Descriptor.TransportId,
                    listenOptions.ProviderEndpoint);
            }

            return await BindNativeAsync(
                provider,
                listenOptions,
                serverOptions,
                cancellationToken).ConfigureAwait(false);
        };

    private static async ValueTask<INnrpServerTransportListener> BindNativeAsync(
        INnrpNativeTransportProvider provider,
        NnrpTransportListenOptions listenOptions,
        NnrpServerOptions serverOptions,
        CancellationToken cancellationToken)
    {
        NnrpTransportListener? listener = await provider.ListenAsync(
            listenOptions,
            cancellationToken).ConfigureAwait(false);
        try
        {
            NnrpServerSessionOptions defaults = serverOptions.SessionDefaults;
            NnrpNativeRuntimeServer server = listener.AdoptServer(new NnrpNativeServerBindOptions(
                serverOptions.ServerId == 0
                    ? NnrpRuntimeHandleIdAllocator.Allocate()
                    : serverOptions.ServerId,
                serverOptions.ServerGeneration,
                defaults.SupportedProfiles,
                defaults.SupportedCacheObjects,
                defaults.MaxCacheObjects,
                defaults.MaxCacheObjectBytes,
                defaults.ResumeTokenBytes,
                defaults.MaxInFlightOperations,
                defaults.GrantedOperationCredit,
                defaults.LeaseTtlMilliseconds,
                defaults.ResumeWindowMilliseconds,
                defaults.SchemaRegistry,
                async open =>
                {
                    NnrpServerSessionPolicyDecision decision =
                        await defaults.ApplicationPolicy.EvaluateAsync(open).ConfigureAwait(false)
                        ?? throw new InvalidOperationException(
                            "The server application policy returned a null decision.");
                    return decision.Accepted
                        ? NnrpNativeServerPolicyDecision.Accept()
                        : NnrpNativeServerPolicyDecision.Reject(
                            decision.SessionErrorCode,
                            decision.Diagnostic);
                }));
            NnrpProviderEndpoint endpoint = listener.BoundEndpoint;
            listener = null;
            return new NativeHostRouteListener(provider.Descriptor.TransportId, endpoint, server);
        }
        finally
        {
            listener?.Dispose();
        }
    }

    private static NnrpClientProviderRoute ClientRoute(
        WireHostRoute route,
        WireHostRoute resolved,
        string artifactsPath) =>
        new()
        {
            ProviderEndpoint = HasFailure(route, "route_unresolved")
                ? MismatchedEndpoint(route.Transport)
                : NnrpProviderEndpoint.Parse(resolved.Locator),
            Security = HasFailure(route, "security_incompatible")
                ? null
                : ClientSecurity(route.Security.Mode, artifactsPath),
        };

    private static NnrpServerProviderRoute ServerRoute(
        WireHostRoute route,
        WireHostRoute resolved,
        string artifactsPath) =>
        new()
        {
            ProviderEndpoint = NnrpProviderEndpoint.Parse(resolved.Locator),
            Security = ServerSecurity(route.Security.Mode, artifactsPath),
        };

    private static NnrpTransportClientSecurity? ClientSecurity(string mode, string artifactsPath) =>
        IsSecure(mode)
            ? new NnrpTransportClientSecurity(
                "localhost",
                File.ReadAllBytes(Path.Combine(artifactsPath, "server.der")))
            : null;

    private static NnrpTransportServerSecurity? ServerSecurity(string mode, string artifactsPath) =>
        IsSecure(mode)
            ? new NnrpTransportServerSecurity(
                File.ReadAllBytes(Path.Combine(artifactsPath, "server.der")),
                File.ReadAllBytes(Path.Combine(artifactsPath, "server-key.der")))
            : null;

    private static bool IsSecure(string mode) => mode is "tls_server_auth" or "mutual_tls" or "wss";

    private static async ValueTask DisposeAfterPeerCloseAsync(IAsyncDisposable? resource)
    {
        if (resource is null)
        {
            return;
        }

        try
        {
            await resource.DisposeAsync().ConfigureAwait(false);
        }
        catch (NnrpNativeRuntimeException error) when (
            error.Status.ErrorFamily == NnrpErrorFamily.Transport
            && error.Status.DetailCode is TransportTerminalStateDetail or TransportIoDetail)
        {
        }
    }

    private static INnrpNativeTransportProvider ClientProvider(WireHostRoute route) =>
        route.ProviderId == "example.transport.quic.uninstalled"
            ? new UnavailableHostRouteProvider(route.ProviderId, TransportIdOf(route.Transport))
            : InstalledProvider(route);

    private static INnrpNativeTransportProvider ServerProvider(WireHostRoute route) => InstalledProvider(route);

    private static INnrpNativeTransportProvider InstalledProvider(WireHostRoute route)
    {
        var artifactPath = Environment.GetEnvironmentVariable(ArtifactPathVariable(route.Transport));
        INnrpNativeTransportProvider provider = TransportIdOf(route.Transport) switch
        {
            TransportId.Tcp => new NnrpNativeTcpTransportProvider(artifactPath),
            TransportId.Quic => new NnrpNativeQuicTransportProvider(artifactPath),
            TransportId.Ipc => new NnrpNativeIpcTransportProvider(artifactPath),
            TransportId.WebSocket => new NnrpNativeWebSocketTransportProvider(artifactPath),
            _ => throw new InvalidOperationException($"Unsupported route transport: {route.Transport}"),
        };
        if (!string.Equals(provider.Descriptor.Metadata.Id, route.ProviderId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Installed provider identity does not match scenario route: {route.ProviderId}");
        }

        return provider;
    }

    private static string ArtifactPathVariable(string transport) => transport switch
    {
        "tcp" => "NNRP_NATIVE_TCP_ARTIFACT_PATH",
        "quic" => "NNRP_NATIVE_QUIC_ARTIFACT_PATH",
        "ipc" => "NNRP_NATIVE_IPC_ARTIFACT_PATH",
        "websocket" => "NNRP_NATIVE_WEBSOCKET_ARTIFACT_PATH",
        _ => throw new InvalidOperationException($"Unsupported route transport: {transport}"),
    };

    private static WireHostRouteEvidence ClientEvidence(
        WireHostRouteFixture fixture,
        IReadOnlyList<NnrpTransportCandidate> diagnostics,
        string? selectedProvider)
    {
        IReadOnlyList<WireHostRouteCandidateEvidence> candidates = fixture.Routes.Select(route =>
        {
            NnrpTransportCandidate diagnostic = diagnostics.Single(candidate =>
                candidate.TransportId == TransportIdOf(route.Transport)
                && candidate.Provider.Id == route.ProviderId);
            string? rejection = diagnostic.RejectionReason.HasValue
                ? RejectionReasonOf(diagnostic.RejectionReason.Value)
                : null;
            return new WireHostRouteCandidateEvidence(
                route.Transport,
                route.ProviderId,
                route.Locator,
                rejection != "route-unresolved",
                rejection != "security-unsatisfied",
                route.ProviderId == selectedProvider,
                rejection);
        }).ToArray();
        IReadOnlyList<WireHostRouteAcceptedSessionEvidence> accepted = selectedProvider is null
            ? []
            : fixture.Routes
                .Where(route => route.ProviderId == selectedProvider)
                .Select(route => new WireHostRouteAcceptedSessionEvidence(
                    route.Transport,
                    route.ProviderId,
                    route.Transport))
                .ToArray();
        return new WireHostRouteEvidence(
            fixture.ApplicationEndpoint,
            candidates,
            [],
            accepted,
            false,
            false);
    }

    private static WireHostRouteEvidence ServerEvidence(
        WireHostRouteFixture fixture,
        IReadOnlyDictionary<TransportId, NnrpProviderEndpoint> bound,
        IReadOnlyList<TransportId> accepted,
        string listenerState,
        bool logicalSetClosed = false,
        string? terminalFailure = null)
    {
        WireHostRouteListenerEvidence[] listeners = fixture.Routes.Select(route =>
            new WireHostRouteListenerEvidence(
                route.Transport,
                route.ProviderId,
                route.Locator,
                bound.TryGetValue(TransportIdOf(route.Transport), out NnrpProviderEndpoint? endpoint)
                    ? endpoint.ToString()
                    : null,
                listenerState)).ToArray();
        WireHostRouteAcceptedSessionEvidence[] acceptedSessions = accepted.Select(transport =>
        {
            WireHostRoute route = fixture.Routes.Single(
                candidate => TransportIdOf(candidate.Transport) == transport);
            return new WireHostRouteAcceptedSessionEvidence(
                route.Transport,
                route.ProviderId,
                route.Transport);
        }).ToArray();
        return new WireHostRouteEvidence(
            fixture.ApplicationEndpoint,
            CandidateDefaults(fixture),
            listeners,
            acceptedSessions,
            false,
            logicalSetClosed,
            terminalFailure);
    }

    private static WireHostRouteEvidence RollbackEvidence(WireHostRouteFixture fixture) =>
        new(
            fixture.ApplicationEndpoint,
            CandidateDefaults(fixture),
            fixture.Routes.Select(route => new WireHostRouteListenerEvidence(
                route.Transport,
                route.ProviderId,
                route.Locator,
                null,
                HasFailure(route, "bind_failure") ? "failed" : "rolled_back")).ToArray(),
            [],
            true,
            true);

    private static WireHostRouteCandidateEvidence[] CandidateDefaults(WireHostRouteFixture fixture) =>
        fixture.Routes.Select(route => new WireHostRouteCandidateEvidence(
            route.Transport,
            route.ProviderId,
            route.Locator,
            true,
            true,
            false)).ToArray();

    private static void WriteReady(
        WireHostRouteScenario scenario,
        WireHostRouteFixture fixture,
        IReadOnlyDictionary<TransportId, NnrpProviderEndpoint> bound,
        string outputPath)
    {
        WireHostRouteReadyListener[] listeners = bound.Select(pair =>
        {
            WireHostRoute route = fixture.Routes.Single(
                candidate => TransportIdOf(candidate.Transport) == pair.Key);
            return new WireHostRouteReadyListener(
                route.Transport,
                route.ProviderId,
                pair.Value.ToString());
        }).ToArray();
        WireHostRouteCommand.WriteJsonAtomically(
            outputPath,
            new WireHostRouteReadyReport(
                ReadySchema,
                WireHostRouteCommand.ProtocolVersion,
                scenario.Id,
                listeners),
            WireHostRouteJsonContext.Default.WireHostRouteReadyReport);
    }

    private static void ValidateScenarioPair(
        WireHostRouteScenario scenario,
        WireHostRouteScenario resolved)
    {
        WireHostRouteFixture fixture = scenario.HostRoute
            ?? throw new InvalidOperationException("Host-route scenario has no host_route fixture.");
        WireHostRouteFixture resolvedFixture = resolved.HostRoute
            ?? throw new InvalidOperationException("Resolved scenario has no host_route fixture.");
        if (scenario.Id != resolved.Id || fixture.Routes.Count != resolvedFixture.Routes.Count)
        {
            throw new InvalidOperationException("Resolved host-route scenario does not match the suite scenario.");
        }

        HashSet<TransportId> transports = [];
        HashSet<string> providerIds = new(StringComparer.Ordinal);
        for (int index = 0; index < fixture.Routes.Count; index++)
        {
            WireHostRoute route = fixture.Routes[index];
            WireHostRoute resolvedRoute = resolvedFixture.Routes[index];
            if (route.Transport != resolvedRoute.Transport || route.ProviderId != resolvedRoute.ProviderId)
            {
                throw new InvalidOperationException("Resolved host-route scenario changes provider identities.");
            }

            if (!transports.Add(TransportIdOf(route.Transport)) || !providerIds.Add(route.ProviderId))
            {
                throw new InvalidOperationException(
                    "Host-route scenarios require unique transports and provider ids.");
            }
        }
    }

    private static TransportPolicy PreferOpenedRoute(IReadOnlyList<WireHostRoute> routes)
    {
        TransportId opened = TransportIdOf(routes.First(route => !HasFailure(route, "bind_failure")).Transport);
        return opened switch
        {
            TransportId.Tcp => TransportPolicy.PreferTcp,
            TransportId.Quic => TransportPolicy.PreferQuic,
            TransportId.Ipc => TransportPolicy.PreferIpc,
            TransportId.WebSocket => TransportPolicy.PreferWebSocket,
            _ => throw new InvalidOperationException("Bind failure scenario has no valid opened route."),
        };
    }

    private static bool HasFailure(WireHostRoute route, string failure) =>
        route.InjectedFailures?.Contains(failure, StringComparer.Ordinal) == true;

    private static NnrpProviderEndpoint MismatchedEndpoint(string transport) =>
        NnrpProviderEndpoint.Parse(transport == "ipc" ? "tcp://127.0.0.1:1" : "npipe://route-unresolved");

    private static TransportId TransportIdOf(string transport) => transport switch
    {
        "tcp" => TransportId.Tcp,
        "quic" => TransportId.Quic,
        "ipc" => TransportId.Ipc,
        "websocket" => TransportId.WebSocket,
        _ => throw new InvalidOperationException($"Unsupported route transport: {transport}"),
    };

    private static string RejectionReasonOf(NnrpTransportRejectionReason reason) => reason switch
    {
        NnrpTransportRejectionReason.PolicyDisallowed => "policy-disallowed",
        NnrpTransportRejectionReason.LocalUnavailable => "local-unavailable",
        NnrpTransportRejectionReason.PeerUnsupported => "peer-unsupported",
        NnrpTransportRejectionReason.LimitExceeded => "limit-exceeded",
        NnrpTransportRejectionReason.RouteUnresolved => "route-unresolved",
        NnrpTransportRejectionReason.SecurityUnsatisfied => "security-unsatisfied",
        NnrpTransportRejectionReason.ProbeMissing => "probe-missing",
        NnrpTransportRejectionReason.ProbeFailed => "probe-failed",
        _ => throw new InvalidOperationException($"Unsupported rejection reason: {reason}"),
    };

    private sealed class UnavailableHostRouteProvider : INnrpNativeTransportProvider
    {
        internal UnavailableHostRouteProvider(string providerId, TransportId transportId)
        {
            Descriptor = new NnrpTransportProviderDescriptor(
                "host-route-uninstalled",
                "1",
                transportId,
                NnrpTransportProviderKind.NativeDynamic,
                false,
                null,
                new NnrpTransportProviderMetadata(
                    providerId,
                    new NnrpTransportProviderCost(0, 0),
                    0,
                    new NnrpTransportProviderLimits(MaxPacketBytes),
                    []),
                "provider package is not installed");
        }

        public NnrpTransportProviderDescriptor Descriptor { get; }

        public ValueTask<NnrpTransportConnection> ConnectAsync(
            NnrpTransportConnectOptions options,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("An unavailable provider must never be called.");

        public ValueTask<NnrpTransportListener> ListenAsync(
            NnrpTransportListenOptions options,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("An unavailable provider must never be called.");

        public ValueTask<NnrpTransportProbeMetrics> ProbeAsync(
            NnrpTransportProbeOptions options,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("An unavailable provider must never be called.");
    }

    private sealed class InjectedBindFailureException : IOException
    {
        internal InjectedBindFailureException()
            : base("injected provider bind failure")
        {
        }
    }

    private sealed class TerminalFailureListener(
        TransportId transportId,
        NnrpProviderEndpoint boundEndpoint) : INnrpServerTransportListener
    {
        public TransportId TransportId { get; } = transportId;

        public NnrpProviderEndpoint BoundEndpoint { get; } = boundEndpoint;

        public ValueTask<NnrpAcceptedServerTransportSession> AcceptAsync(
            NnrpServerAcceptOptions options,
            uint pollTimeoutMilliseconds,
            CancellationToken cancellationToken) =>
            throw new IOException("injected terminal listener failure");

        public bool ReleasePendingAccept() => false;

        public ValueTask DisposeAsync() => default;
    }

    private sealed class NativeHostRouteListener(
        TransportId transportId,
        NnrpProviderEndpoint boundEndpoint,
        NnrpNativeRuntimeServer server) : INnrpServerTransportListener
    {
        private NnrpNativeRuntimeServer? server = server;

        public TransportId TransportId { get; } = transportId;

        public NnrpProviderEndpoint BoundEndpoint { get; } = boundEndpoint;

        public ValueTask<NnrpAcceptedServerTransportSession> AcceptAsync(
            NnrpServerAcceptOptions options,
            uint pollTimeoutMilliseconds,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            NnrpNativeRuntimeServer current = server
                ?? throw new ObjectDisposedException(nameof(NativeHostRouteListener));
            NnrpNativeRuntimeServerSession session = current.AcceptSession(
                options.SessionId == 0 ? NnrpRuntimeHandleIdAllocator.Allocate() : options.SessionId,
                options.SessionGeneration,
                pollTimeoutMilliseconds);
            return ValueTask.FromResult(new NnrpAcceptedServerTransportSession(
                session.ActiveTransportId,
                session,
                () =>
                {
                    if (!session.IsClosed)
                    {
                        session.Close();
                    }
                }));
        }

        public bool ReleasePendingAccept()
        {
            NnrpNativeRuntimeServer? current = server;
            return current is not null && !current.IsClosed && current.ReleasePendingAccept();
        }

        public ValueTask DisposeAsync()
        {
            Interlocked.Exchange(ref server, null)?.Dispose();
            return default;
        }
    }
}
