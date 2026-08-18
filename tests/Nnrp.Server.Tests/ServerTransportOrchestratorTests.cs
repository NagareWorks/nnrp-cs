using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nnrp.Core;
using Nnrp.NativeBridge;
using Xunit;

namespace Nnrp.Server.Tests
{
    public sealed class ServerTransportOrchestratorTests
    {
        [Fact]
        public void OptionsOwnFrozenFieldsAndValidateInputs()
        {
            var provider = Provider(TransportId.Tcp, "tcp");
            var providers = new List<INnrpNativeTransportProvider> { provider };
            var routes = new Dictionary<TransportId, NnrpServerProviderRoute>();
            var options = new NnrpServerOptions(
                NnrpEndpoint.Parse("nnrp://localhost:7000"),
                routes,
                TransportPolicy.PreferTcp,
                providers,
                serverId: 91,
                serverGeneration: 7);

            routes[TransportId.WebSocket] = new NnrpServerProviderRoute();
            providers.Clear();

            Assert.Empty(options.ProviderRoutes);
            Assert.Single(options.Transports!);
            Assert.Equal(TransportPolicy.PreferTcp, options.TransportPolicy);
            Assert.Equal((ulong)91, options.ServerId);
            Assert.Equal((uint)7, options.ServerGeneration);
            var accept = new NnrpServerAcceptOptions(
                sessionId: 92,
                sessionGeneration: 8,
                timeoutMilliseconds: 25);
            Assert.Equal((ulong)92, accept.SessionId);
            Assert.Equal((uint)8, accept.SessionGeneration);
            Assert.Equal((uint)25, accept.TimeoutMilliseconds);

            Assert.Throws<ArgumentNullException>(() => new NnrpServerOptions(null!));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NnrpServerOptions(
                NnrpEndpoint.Parse("nnrp://localhost:7000"),
                transportPolicy: (TransportPolicy)255));
            Assert.Throws<ArgumentException>(() => new NnrpServerOptions(
                NnrpEndpoint.Parse("nnrp://localhost:7000"),
                transports: new INnrpNativeTransportProvider[] { null! }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NnrpServerOptions(
                NnrpEndpoint.Parse("nnrp://localhost:7000"),
                serverGeneration: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NnrpServerAcceptOptions(
                sessionId: 0,
                sessionGeneration: 0));
        }

        [Fact]
        public async Task SessionOptionsAndPolicyDecisionsPreserveTheFrozenContract()
        {
            var profiles = new List<ushort> { TypedPayloadProfileId.TokenValue, TypedPayloadProfileId.TensorValue };
            var cacheObjects = new List<CacheObjectKind> { CacheObjectKind.PromptSegment };
            var registry = NnrpSchemaRegistry.WithStandardProfiles();
            var policy = new RejectingPolicy();
            var options = new NnrpServerSessionOptions(
                profiles,
                cacheObjects,
                maxCacheObjects: 11,
                maxCacheObjectBytes: 12,
                registry,
                resumeTokenBytes: 13,
                maxInFlightOperations: 14,
                grantedOperationCredit: 7,
                leaseTtlMilliseconds: 15,
                resumeWindowMilliseconds: 16,
                policy);

            profiles.Clear();
            cacheObjects.Clear();

            Assert.Equal(new[] { TypedPayloadProfileId.TokenValue, TypedPayloadProfileId.TensorValue }, options.SupportedProfiles);
            Assert.Equal(new[] { CacheObjectKind.PromptSegment }, options.SupportedCacheObjects);
            Assert.Equal((ulong)11, options.MaxCacheObjects);
            Assert.Equal((uint)12, options.MaxCacheObjectBytes);
            Assert.Same(registry, options.SchemaRegistry);
            Assert.Equal((uint)13, options.ResumeTokenBytes);
            Assert.Equal((ushort)14, options.MaxInFlightOperations);
            Assert.Equal((ushort)7, options.GrantedOperationCredit);
            Assert.Equal((uint)15, options.LeaseTtlMilliseconds);
            Assert.Equal((uint)16, options.ResumeWindowMilliseconds);
            Assert.Same(policy, options.ApplicationPolicy);

            var open = new SessionOpenMetadata(
                1,
                TypedPayloadProfileId.TokenValue,
                SessionPriorityClass.Balanced,
                SessionFlags.None,
                TypedPayloadDescriptor.TokenDeltaSchemaId,
                TypedPayloadDescriptor.TokenDeltaSchemaVersion,
                500,
                4,
                30_000,
                0,
                0,
                0,
                2);
            var rejection = await options.ApplicationPolicy.EvaluateAsync(open);
            Assert.False(rejection.Accepted);
            Assert.Equal(SessionErrorCode.ProfileUnsupported, rejection.SessionErrorCode);
            Assert.Equal("profile", rejection.Diagnostic);

            var defaults = new NnrpServerSessionOptions();
            var acceptance = await defaults.ApplicationPolicy.EvaluateAsync(open);
            Assert.True(acceptance.Accepted);
            Assert.Equal(SessionErrorCode.None, acceptance.SessionErrorCode);
            Assert.Null(acceptance.Diagnostic);
            Assert.Throws<ArgumentException>(() => NnrpServerSessionPolicyDecision.Reject(SessionErrorCode.None));
            Assert.Throws<ArgumentException>(() => new NnrpServerSessionOptions(supportedProfiles: Array.Empty<ushort>()));
            Assert.Throws<ArgumentException>(() => new NnrpServerSessionOptions(
                supportedCacheObjects: new[] { (CacheObjectKind)uint.MaxValue }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NnrpServerSessionOptions(maxInFlightOperations: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NnrpServerSessionOptions(
                maxInFlightOperations: 2,
                grantedOperationCredit: 3));
        }

        [Fact]
        public async Task AutoAndPreferBindEveryAllowedProviderAndOwnActualEndpoints()
        {
            var providers = AllProviders();
            var created = new List<FakeListener>();

            async ValueTask AssertPolicy(TransportPolicy policy, TransportId first)
            {
                created.Clear();
                await using var listeners = await NnrpServerTransportOrchestrator.ListenAsync(
                    Options(providers, policy),
                    binder: Bind(created));

                Assert.Equal(4, created.Count);
                Assert.Equal(first, created[0].TransportId);
                Assert.Equal("tcp://127.0.0.1:7100", listeners.BoundProviderEndpoints[TransportId.Tcp].ToString());
                Assert.Equal("ws://127.0.0.1:7200/nnrp", listeners.BoundProviderEndpoints[TransportId.WebSocket].ToString());
            }

            await AssertPolicy(TransportPolicy.Auto, TransportId.Quic);
            await AssertPolicy(TransportPolicy.PreferTcp, TransportId.Tcp);
            await AssertPolicy(TransportPolicy.PreferQuic, TransportId.Quic);
            await AssertPolicy(TransportPolicy.PreferIpc, TransportId.Ipc);
            await AssertPolicy(TransportPolicy.PreferWebSocket, TransportId.WebSocket);
        }

        [Fact]
        public async Task SessionDefaultsAreSharedAcrossTheLogicalListenerSet()
        {
            var options = Options(
                new[] { Provider(TransportId.Tcp, "tcp"), Provider(TransportId.WebSocket, "websocket") },
                TransportPolicy.Auto);
            var observedDefaults = new List<NnrpServerSessionOptions>();

            await using var listeners = await NnrpServerTransportOrchestrator.ListenAsync(
                options,
                binder: (provider, listen, server, cancellation) =>
                {
                    observedDefaults.Add(server.SessionDefaults);
                    return new ValueTask<INnrpServerTransportListener>(Listener(provider.Descriptor.TransportId));
                });

            Assert.Equal(2, observedDefaults.Count);
            Assert.All(observedDefaults, value => Assert.Same(options.SessionDefaults, value));
        }

        [Theory]
        [InlineData(TransportPolicy.ForceTcp, TransportId.Tcp)]
        [InlineData(TransportPolicy.ForceQuic, TransportId.Quic)]
        [InlineData(TransportPolicy.ForceIpc, TransportId.Ipc)]
        [InlineData(TransportPolicy.ForceWebSocket, TransportId.WebSocket)]
        public async Task ForceBindsOnlyTheNamedProvider(TransportPolicy policy, TransportId expected)
        {
            var providers = AllProviders();
            var created = new List<FakeListener>();

            await using var listeners = await NnrpServerTransportOrchestrator.ListenAsync(
                Options(providers, policy),
                binder: Bind(created));

            Assert.Single(created);
            Assert.Equal(expected, created[0].TransportId);
            Assert.Equal(new[] { expected }, listeners.BoundProviderEndpoints.Keys);
        }

        [Fact]
        public async Task InvalidRequiredRouteFailsBeforeAnyBind()
        {
            var bindCount = 0;
            var options = new NnrpServerOptions(
                NnrpEndpoint.Parse("nnrp://localhost:7000"),
                transports: new[] { Provider(TransportId.Tcp, "tcp"), Provider(TransportId.Ipc, "ipc") });

            var error = await Assert.ThrowsAsync<NnrpTransportSelectionException>(async () =>
                await NnrpServerTransportOrchestrator.ListenAsync(
                    options,
                    binder: (provider, listen, server, cancellation) =>
                    {
                        bindCount++;
                        return new ValueTask<INnrpServerTransportListener>(Listener(provider.Descriptor.TransportId));
                    }));

            Assert.Equal(NnrpTransportSelectionErrorCode.InvalidEvidence, error.Code);
            Assert.Contains(error.Candidates, candidate =>
                candidate.TransportId == TransportId.Ipc
                && candidate.RejectionReason == NnrpTransportRejectionReason.RouteUnresolved);
            Assert.Equal(0, bindCount);
        }

        [Fact]
        public async Task MissingForcedProviderFailsWithoutFallback()
        {
            var error = await Assert.ThrowsAsync<NnrpTransportSelectionException>(async () =>
                await NnrpServerTransportOrchestrator.ListenAsync(new NnrpServerOptions(
                    NnrpEndpoint.Parse("nnrp://localhost:7000"),
                    null,
                    TransportPolicy.ForceQuic,
                    new[] { Provider(TransportId.Tcp, "tcp") })));

            Assert.Equal(NnrpTransportSelectionErrorCode.ForcedTransportUnavailable, error.Code);
        }

        [Fact]
        public async Task ConfiguredButUninstalledProviderIsARequiredUnavailableRoute()
        {
            var tcp = Provider(TransportId.Tcp, "tcp");
            var bindCount = 0;
            var options = new NnrpServerOptions(
                NnrpEndpoint.Parse("nnrp://localhost:7000"),
                new Dictionary<TransportId, NnrpServerProviderRoute>
                {
                    [TransportId.WebSocket] = new NnrpServerProviderRoute
                    {
                        ProviderEndpoint = NnrpProviderEndpoint.Parse("ws://localhost:7200/nnrp"),
                    },
                },
                TransportPolicy.Auto,
                new[] { tcp });

            var error = await Assert.ThrowsAsync<NnrpTransportSelectionException>(async () =>
                await NnrpServerTransportOrchestrator.ListenAsync(
                    options,
                    binder: (provider, listen, server, cancellation) =>
                    {
                        bindCount++;
                        return new ValueTask<INnrpServerTransportListener>(Listener(provider.Descriptor.TransportId));
                    }));

            Assert.Equal(NnrpTransportSelectionErrorCode.InvalidEvidence, error.Code);
            Assert.Contains(error.Candidates, candidate =>
                candidate.TransportId == TransportId.WebSocket
                && candidate.RejectionReason == NnrpTransportRejectionReason.LocalUnavailable);
            Assert.Equal(0, bindCount);
        }

        [Fact]
        public async Task UnavailableAndInsufficientProvidersExposeTypedEvidence()
        {
            var unavailable = Provider(TransportId.Tcp, "tcp", available: false);
            var unavailableError = await Assert.ThrowsAsync<NnrpTransportSelectionException>(async () =>
                await NnrpServerTransportOrchestrator.ListenAsync(Options(new[] { unavailable })));
            Assert.Contains(unavailableError.Candidates, candidate =>
                candidate.RejectionReason == NnrpTransportRejectionReason.LocalUnavailable);

            var insufficient = Provider(TransportId.Tcp, "small", maxFrameBytes: 1024);
            var limitError = await Assert.ThrowsAsync<NnrpTransportSelectionException>(async () =>
                await NnrpServerTransportOrchestrator.ListenAsync(Options(new[] { insufficient })));
            Assert.Contains(limitError.Candidates, candidate =>
                candidate.RejectionReason == NnrpTransportRejectionReason.LimitExceeded);
        }

        [Fact]
        public async Task ListenRejectsNullCancellationAndNullBinderResults()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await NnrpServerTransportOrchestrator.ListenAsync(null!));

            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
                await NnrpServerTransportOrchestrator.ListenAsync(
                    Options(new[] { Provider(TransportId.Tcp, "tcp") }),
                    cancellation.Token));

            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await NnrpServerTransportOrchestrator.ListenAsync(
                    Options(new[] { Provider(TransportId.Tcp, "tcp") }),
                    binder: (provider, listen, server, token) =>
                        new ValueTask<INnrpServerTransportListener>((INnrpServerTransportListener)null!)));
        }

        [Fact]
        public async Task NativeBinderRejectsProviderReturningNullListener()
        {
            var provider = Provider(TransportId.Tcp, "tcp");
            provider.ReturnNullListener = true;

            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await NnrpServerTransportOrchestrator.ListenAsync(Options(new[] { provider })));
        }

        [Fact]
        public async Task BindFailureRollsBackEveryOpenedListener()
        {
            var first = Listener(TransportId.Tcp);
            var bindCount = 0;

            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await NnrpServerTransportOrchestrator.ListenAsync(
                    Options(new[] { Provider(TransportId.Tcp, "tcp"), Provider(TransportId.WebSocket, "ws") }),
                    binder: (provider, listen, server, cancellation) =>
                    {
                        bindCount++;
                        if (bindCount == 2)
                        {
                            throw new InvalidOperationException("bind failed");
                        }

                        return new ValueTask<INnrpServerTransportListener>(first);
                    }));

            Assert.Equal(1, first.DisposeCount);
        }

        [Fact]
        public async Task BinderTransportMismatchDisposesReturnedAndPriorListeners()
        {
            var first = Listener(TransportId.Tcp);
            var wrong = Listener(TransportId.Quic);
            var count = 0;

            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await NnrpServerTransportOrchestrator.ListenAsync(
                    Options(new[] { Provider(TransportId.Tcp, "tcp"), Provider(TransportId.WebSocket, "ws") }),
                    binder: (provider, listen, server, cancellation) =>
                    {
                        count++;
                        return new ValueTask<INnrpServerTransportListener>(count == 1 ? first : wrong);
                    }));

            Assert.Equal(1, first.DisposeCount);
            Assert.Equal(1, wrong.DisposeCount);
        }

        [Fact]
        public async Task RollbackFailuresStayAttachedToOriginalBindError()
        {
            var first = Listener(TransportId.Tcp);
            first.DisposeFailure = new InvalidOperationException("rollback failed");
            var count = 0;

            var error = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await NnrpServerTransportOrchestrator.ListenAsync(
                    Options(new[] { Provider(TransportId.Tcp, "tcp"), Provider(TransportId.WebSocket, "ws") }),
                    binder: (provider, listen, server, cancellation) =>
                    {
                        count++;
                        if (count == 2)
                        {
                            throw new InvalidOperationException("bind failed");
                        }

                        return new ValueTask<INnrpServerTransportListener>(first);
                    }));

            Assert.Equal("bind failed", error.Message);
            Assert.True(error.Data.Contains("NnrpServerListenerRollbackErrors"));
            Assert.Equal(1, first.DisposeCount);
        }

        [Fact]
        public async Task AcceptPollsAllListenersAndPreservesActualTransport()
        {
            var tcp = Listener(TransportId.Tcp);
            var websocket = Listener(TransportId.WebSocket);
            tcp.EnqueueWouldBlock();
            tcp.EnqueueAccepted();
            websocket.EnqueueAccepted();
            await using var listeners = new NnrpServerTransportListenerSet(new[] { tcp, websocket });

            using var accepted = await listeners.AcceptAsync(
                new NnrpServerAcceptOptions(
                    sessionId: 41,
                    sessionGeneration: 3,
                    timeoutMilliseconds: 100));

            Assert.Equal(TransportId.WebSocket, accepted.ActiveTransportId);
            Assert.Equal(1, tcp.ReleaseCount);
            Assert.Equal(0, websocket.ReleaseCount);
            Assert.Equal(1, tcp.AcceptCount);
            Assert.Equal(1, websocket.AcceptCount);
            Assert.Equal((uint)100, tcp.LastPollTimeoutMilliseconds);
            Assert.Equal((uint)100, websocket.LastPollTimeoutMilliseconds);
            Assert.Equal((ulong)41, websocket.LastAcceptOptions!.SessionId);
            Assert.Equal((uint)3, websocket.LastAcceptOptions.SessionGeneration);

            using var pendingTcp = await listeners.AcceptAsync(
                new NnrpServerAcceptOptions(
                    sessionId: 42,
                    sessionGeneration: 4,
                    timeoutMilliseconds: 100));

            Assert.Equal(TransportId.Tcp, pendingTcp.ActiveTransportId);
            Assert.Equal(2, tcp.AcceptCount);
            Assert.Equal(1, websocket.AcceptCount);
            Assert.Equal((ulong)42, tcp.LastAcceptOptions!.SessionId);
            Assert.Equal((uint)4, tcp.LastAcceptOptions.SessionGeneration);
        }

        [Fact]
        public async Task SuccessfulAcceptReleaseFailureClosesTheCompleteSet()
        {
            var tcp = Listener(TransportId.Tcp);
            var websocket = Listener(TransportId.WebSocket);
            tcp.ReleaseFailure = new InvalidOperationException("release failed");
            tcp.EnqueueWouldBlock();
            websocket.EnqueueAccepted();
            await using var listeners = new NnrpServerTransportListenerSet(new[] { tcp, websocket });

            var error = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await listeners.AcceptAsync(new NnrpServerAcceptOptions(timeoutMilliseconds: 100)));

            Assert.Equal("release failed", error.Message);
            Assert.True(listeners.IsClosed);
            Assert.Equal(1, websocket.SessionCloseCount);
        }

        [Fact]
        public async Task RejectedPeerReleasesOnlyItsTicketAndContinues()
        {
            var tcp = Listener(TransportId.Tcp);
            var websocket = Listener(TransportId.WebSocket);
            tcp.EnqueueProtocolFailure();
            websocket.EnqueueAccepted();
            await using var listeners = new NnrpServerTransportListenerSet(new[] { tcp, websocket });

            using var accepted = await listeners.AcceptAsync(new NnrpServerAcceptOptions(timeoutMilliseconds: 100));

            Assert.Equal(TransportId.WebSocket, accepted.ActiveTransportId);
            Assert.Equal(2, tcp.ReleaseCount);
            Assert.Equal(0, websocket.ReleaseCount);
        }

        [Fact]
        public async Task InvalidAcceptedSessionClosesTheCompleteSet()
        {
            var nullListener = Listener(TransportId.Tcp);
            nullListener.EnqueueNull();
            await using var nullSet = new NnrpServerTransportListenerSet(new[] { nullListener });
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await nullSet.AcceptAsync(new NnrpServerAcceptOptions(timeoutMilliseconds: 100)));
            Assert.True(nullSet.IsClosed);

            var mismatch = Listener(TransportId.Tcp);
            mismatch.AcceptedTransportOverride = TransportId.WebSocket;
            mismatch.EnqueueAccepted();
            await using var mismatchSet = new NnrpServerTransportListenerSet(new[] { mismatch });
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await mismatchSet.AcceptAsync(new NnrpServerAcceptOptions(timeoutMilliseconds: 100)));
            Assert.True(mismatchSet.IsClosed);
            Assert.Equal(1, mismatch.SessionCloseCount);
        }

        [Fact]
        public async Task TimeoutPendingAcceptReleaseFailureClosesSet()
        {
            var tcp = Listener(TransportId.Tcp);
            var websocket = Listener(TransportId.WebSocket);
            tcp.ReleaseFailure = new InvalidOperationException("release failed");
            tcp.EnqueueWouldBlock();
            websocket.EnqueueWouldBlock();
            await using var listeners = new NnrpServerTransportListenerSet(new[] { tcp, websocket });

            var error = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await listeners.AcceptAsync(new NnrpServerAcceptOptions(timeoutMilliseconds: 5)));

            Assert.Equal("release failed", error.Message);
            Assert.True(listeners.IsClosed);
            Assert.Equal(0, websocket.SessionCloseCount);
        }

        [Fact]
        public async Task RejectedPeerReleaseFailureClosesCompleteSet()
        {
            var listener = Listener(TransportId.Tcp);
            listener.ReleaseFailure = new InvalidOperationException("release failed");
            listener.EnqueueProtocolFailure();
            await using var listeners = new NnrpServerTransportListenerSet(new[] { listener });

            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await listeners.AcceptAsync(new NnrpServerAcceptOptions(timeoutMilliseconds: 100)));

            Assert.True(listeners.IsClosed);
        }

        [Fact]
        public async Task TimeoutAndCancellationReleaseTicketsWithoutClosingServer()
        {
            var listener = Listener(TransportId.Tcp);
            await using var listeners = new NnrpServerTransportListenerSet(new[] { listener });

            await Assert.ThrowsAsync<TimeoutException>(async () =>
                await listeners.AcceptAsync(new NnrpServerAcceptOptions(timeoutMilliseconds: 5)));
            Assert.False(listeners.IsClosed);
            Assert.True(listener.ReleaseCount > 0);

            using var cancellation = new CancellationTokenSource(10);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
                await listeners.AcceptAsync(cancellationToken: cancellation.Token));
            Assert.False(listeners.IsClosed);
            Assert.True(listener.ReleaseCount > 1);
        }

        [Fact]
        public async Task TerminalListenerFailureClosesSessionsAndCompleteSetExactlyOnce()
        {
            var tcp = Listener(TransportId.Tcp);
            var websocket = Listener(TransportId.WebSocket);
            tcp.EnqueueAccepted();
            websocket.EnqueueTerminalFailure();
            await using var listeners = new NnrpServerTransportListenerSet(new[] { tcp, websocket });

            var accepted = await listeners.AcceptAsync(new NnrpServerAcceptOptions(timeoutMilliseconds: 100));
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await listeners.AcceptAsync(new NnrpServerAcceptOptions(timeoutMilliseconds: 100)));

            Assert.True(listeners.IsClosed);
            Assert.True(accepted.IsClosed);
            Assert.Equal(1, tcp.SessionCloseCount);
            Assert.Equal(1, tcp.DisposeCount);
            Assert.Equal(1, websocket.DisposeCount);

            await listeners.DisposeAsync();
            Assert.Equal(1, tcp.DisposeCount);
            Assert.Equal(1, websocket.DisposeCount);
        }

        [Fact]
        public async Task CloseAggregatesSessionAndListenerFailuresAndRemainsIdempotent()
        {
            var listener = Listener(TransportId.Tcp);
            listener.SessionCloseFailure = new InvalidOperationException("session close failed");
            listener.DisposeFailure = new InvalidOperationException("listener close failed");
            listener.EnqueueAccepted();
            var listeners = new NnrpServerTransportListenerSet(new[] { listener });
            await listeners.AcceptAsync(new NnrpServerAcceptOptions(timeoutMilliseconds: 100));

            var error = await Assert.ThrowsAsync<AggregateException>(async () => await listeners.DisposeAsync());

            Assert.Equal(2, error.InnerExceptions.Count);
            Assert.True(listeners.IsClosed);
            await listeners.DisposeAsync();
            Assert.Equal(1, listener.DisposeCount);
        }

        [Fact]
        public async Task SynchronousDisposeRejectsLaterAccept()
        {
            var listener = Listener(TransportId.Tcp);
            var listeners = new NnrpServerTransportListenerSet(new[] { listener });

            listeners.Dispose();

            Assert.True(listeners.IsClosed);
            await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
                await listeners.AcceptAsync(new NnrpServerAcceptOptions(timeoutMilliseconds: 10)));
        }

        [Fact]
        public void ListenerSetRejectsInvalidMembership()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new NnrpAcceptedServerTransportSession(
                TransportId.Unspecified,
                nativeSession: null,
                close: () => { }));
            Assert.Throws<ArgumentNullException>(() => new NnrpServerTransportListenerSet(null!));
            Assert.Throws<ArgumentException>(() => new NnrpServerTransportListenerSet(Array.Empty<INnrpServerTransportListener>()));
            Assert.Throws<ArgumentException>(() => new NnrpServerTransportListenerSet(new INnrpServerTransportListener[] { null! }));
            Assert.Throws<ArgumentException>(() => new NnrpServerTransportListenerSet(new[]
            {
                Listener(TransportId.Tcp),
                Listener(TransportId.Tcp),
            }));

            var accepted = new NnrpAcceptedServerTransportSession(
                TransportId.Tcp,
                nativeSession: null,
                close: () => { });
            Assert.Null(accepted.NativeSession);
            accepted.Dispose();
        }

        private static NnrpServerOptions Options(
            IReadOnlyList<INnrpNativeTransportProvider> providers,
            TransportPolicy policy = TransportPolicy.Auto)
        {
            var installed = providers.Select(value => value.Descriptor.TransportId).ToHashSet();
            var routes = new Dictionary<TransportId, NnrpServerProviderRoute>();
            if (installed.Contains(TransportId.Quic))
            {
                routes[TransportId.Quic] = new NnrpServerProviderRoute
                {
                    Security = new NnrpTransportServerSecurity(new byte[] { 1 }, new byte[] { 2 }),
                };
            }

            if (installed.Contains(TransportId.Ipc))
            {
                routes[TransportId.Ipc] = new NnrpServerProviderRoute
                {
                    ProviderEndpoint = NnrpProviderEndpoint.Parse(
                        OperatingSystem.IsWindows() ? "npipe://nnrp-test" : "unix:///tmp/nnrp-test.sock"),
                };
            }

            if (installed.Contains(TransportId.WebSocket))
            {
                routes[TransportId.WebSocket] = new NnrpServerProviderRoute
                {
                    ProviderEndpoint = NnrpProviderEndpoint.Parse("ws://localhost:7200/nnrp"),
                };
            }

            return new NnrpServerOptions(
                NnrpEndpoint.Parse("nnrp://localhost:7000"),
                routes,
                policy,
                providers);
        }

        private static FakeProvider[] AllProviders()
        {
            return new[]
            {
                Provider(TransportId.Tcp, "tcp"),
                Provider(TransportId.Quic, "quic"),
                Provider(TransportId.Ipc, "ipc"),
                Provider(TransportId.WebSocket, "websocket"),
            };
        }

        private static NnrpServerTransportBinder Bind(List<FakeListener> created)
        {
            return (provider, listen, server, cancellation) =>
            {
                var listener = Listener(provider.Descriptor.TransportId);
                created.Add(listener);
                return new ValueTask<INnrpServerTransportListener>(listener);
            };
        }

        private static FakeProvider Provider(
            TransportId transportId,
            string id,
            bool available = true,
            ulong maxFrameBytes = 16 * 1024 * 1024)
        {
            return new FakeProvider(new NnrpTransportProviderDescriptor(
                transportId.ToString(),
                "1",
                transportId,
                NnrpTransportProviderKind.NativeDynamic,
                available,
                libraryPath: null,
                new NnrpTransportProviderMetadata(
                    id,
                    new NnrpTransportProviderCost(0, 0),
                    preferenceRank: 0,
                    new NnrpTransportProviderLimits(maxFrameBytes),
                    Array.Empty<NnrpTransportProviderLimitation>())));
        }

        private sealed class RejectingPolicy : INnrpServerSessionPolicy
        {
            public ValueTask<NnrpServerSessionPolicyDecision> EvaluateAsync(SessionOpenMetadata open) =>
                new ValueTask<NnrpServerSessionPolicyDecision>(
                    NnrpServerSessionPolicyDecision.Reject(SessionErrorCode.ProfileUnsupported, "profile"));
        }

        private static FakeListener Listener(TransportId transportId)
        {
            var endpoint = transportId switch
            {
                TransportId.Tcp => "tcp://127.0.0.1:7100",
                TransportId.Quic => "quic://127.0.0.1:7101",
                TransportId.Ipc => OperatingSystem.IsWindows() ? "npipe://nnrp-bound" : "unix:///tmp/nnrp-bound.sock",
                TransportId.WebSocket => "ws://127.0.0.1:7200/nnrp",
                _ => throw new ArgumentOutOfRangeException(nameof(transportId)),
            };
            return new FakeListener(transportId, NnrpProviderEndpoint.Parse(endpoint));
        }

        private sealed class FakeProvider : INnrpNativeTransportProvider
        {
            internal FakeProvider(NnrpTransportProviderDescriptor descriptor)
            {
                Descriptor = descriptor;
            }

            public NnrpTransportProviderDescriptor Descriptor { get; }

            internal bool ReturnNullListener { get; set; }

            public ValueTask<NnrpTransportConnection> ConnectAsync(
                NnrpTransportConnectOptions options,
                CancellationToken cancellationToken = default) => throw new NotSupportedException();

            public ValueTask<NnrpTransportListener> ListenAsync(
                NnrpTransportListenOptions options,
                CancellationToken cancellationToken = default)
            {
                if (ReturnNullListener)
                {
                    return new ValueTask<NnrpTransportListener>((NnrpTransportListener)null!);
                }

                throw new NotSupportedException();
            }

            public ValueTask<NnrpTransportProbeMetrics> ProbeAsync(
                NnrpTransportProbeOptions options,
                CancellationToken cancellationToken = default) => throw new NotSupportedException();
        }

        private sealed class FakeListener : INnrpServerTransportListener
        {
            private readonly Queue<Func<NnrpAcceptedServerTransportSession>> accepts =
                new Queue<Func<NnrpAcceptedServerTransportSession>>();

            internal FakeListener(TransportId transportId, NnrpProviderEndpoint boundEndpoint)
            {
                TransportId = transportId;
                BoundEndpoint = boundEndpoint;
            }

            public TransportId TransportId { get; }

            public NnrpProviderEndpoint BoundEndpoint { get; }

            internal int AcceptCount { get; private set; }

            internal int DisposeCount { get; private set; }

            internal int ReleaseCount { get; private set; }

            internal int SessionCloseCount { get; private set; }

            internal NnrpServerAcceptOptions? LastAcceptOptions { get; private set; }

            internal uint LastPollTimeoutMilliseconds { get; private set; }

            internal TransportId? AcceptedTransportOverride { get; set; }

            internal Exception? DisposeFailure { get; set; }

            internal Exception? ReleaseFailure { get; set; }

            internal Exception? SessionCloseFailure { get; set; }

            internal void EnqueueAccepted()
            {
                accepts.Enqueue(() => new NnrpAcceptedServerTransportSession(
                    AcceptedTransportOverride ?? TransportId,
                    nativeSession: null,
                    close: () =>
                    {
                        SessionCloseCount++;
                        if (SessionCloseFailure != null)
                        {
                            throw SessionCloseFailure;
                        }
                    }));
            }

            internal void EnqueueNull()
            {
                accepts.Enqueue(() => null!);
            }

            internal void EnqueueWouldBlock()
            {
                accepts.Enqueue(() => throw new NnrpNativeWouldBlockException(
                    new NnrpFfiStatus(NnrpFfiStatusCode.WouldBlock)));
            }

            internal void EnqueueProtocolFailure()
            {
                accepts.Enqueue(() => throw new NnrpNativeProtocolException(
                    new NnrpFfiStatus(NnrpFfiStatusCode.ProtocolError)));
            }

            internal void EnqueueTerminalFailure()
            {
                accepts.Enqueue(() => throw new InvalidOperationException("listener failed"));
            }

            public ValueTask<NnrpAcceptedServerTransportSession> AcceptAsync(
                NnrpServerAcceptOptions options,
                uint pollTimeoutMilliseconds,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                AcceptCount++;
                LastAcceptOptions = options;
                LastPollTimeoutMilliseconds = pollTimeoutMilliseconds;
                var accept = accepts.Count == 0
                    ? (Func<NnrpAcceptedServerTransportSession>)(() => throw new NnrpNativeWouldBlockException(
                        new NnrpFfiStatus(NnrpFfiStatusCode.WouldBlock)))
                    : accepts.Dequeue();
                return new ValueTask<NnrpAcceptedServerTransportSession>(accept());
            }

            public bool ReleasePendingAccept()
            {
                ReleaseCount++;
                if (ReleaseFailure != null)
                {
                    throw ReleaseFailure;
                }

                return true;
            }

            public ValueTask DisposeAsync()
            {
                DisposeCount++;
                if (DisposeFailure != null)
                {
                    throw DisposeFailure;
                }

                return default;
            }
        }
    }
}
