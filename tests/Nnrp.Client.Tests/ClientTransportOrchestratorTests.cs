using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nnrp.Core;
using Nnrp.NativeBridge;
using Xunit;

namespace Nnrp.Client.Tests
{
    public sealed class ClientTransportOrchestratorTests
    {
        [Fact]
        public void OptionsOwnRoutesProvidersAndSessionDefaults()
        {
            var providers = new List<INnrpNativeTransportProvider>
            {
                Provider(TransportId.Tcp, "tcp", throughput: 1_000, rtt: 100),
            };
            var routes = new Dictionary<TransportId, NnrpClientProviderRoute>();
            var defaults = new NnrpClientSessionOptions(schemaId: 7, schemaVersion: 2);
            var options = new NnrpClientOptions(
                NnrpEndpoint.Parse("nnrp://localhost:7000"),
                routes,
                TransportPolicy.PreferTcp,
                providers,
                defaults);

            routes[TransportId.WebSocket] = new NnrpClientProviderRoute();
            providers.Clear();

            Assert.Empty(options.ProviderRoutes);
            Assert.Single(options.Transports!);
            Assert.Same(defaults, options.SessionDefaults);
            Assert.Equal(TransportPolicy.PreferTcp, options.TransportPolicy);
            Assert.Equal((uint)0, defaults.SessionId);
            Assert.Equal((uint)1, defaults.SessionGeneration);
            Assert.Equal((ushort)0, defaults.ProfileId);
            Assert.Equal((uint)7, defaults.SchemaId);
            Assert.Equal((uint)2, defaults.SchemaVersion);
            Assert.Throws<ArgumentOutOfRangeException>(() => new NnrpClientSessionOptions(sessionGeneration: 0));
            Assert.Throws<ArgumentException>(() => new NnrpClientSessionOptions(schemaId: 7));
            Assert.Throws<ArgumentNullException>(() => new NnrpClientOptions(null!));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NnrpClientOptions(
                NnrpEndpoint.Parse("nnrp://localhost:7000"),
                transportPolicy: (TransportPolicy)255));
            Assert.Throws<ArgumentException>(() => new NnrpClientOptions(
                NnrpEndpoint.Parse("nnrp://localhost:7000"),
                transports: new INnrpNativeTransportProvider[] { null! }));
        }

        [Fact]
        public async Task AutoProbesEveryViableProviderAndSelectsByFrozenComparator()
        {
            var tcp = Provider(TransportId.Tcp, "tcp", throughput: 1_000, rtt: 100);
            var websocket = Provider(TransportId.WebSocket, "websocket", throughput: 2_000, rtt: 80);
            var options = Options(new[] { tcp, websocket });

            var plan = await NnrpClientTransportOrchestrator.ResolveAsync(options);

            Assert.Same(websocket, plan.Provider);
            Assert.Equal(1, tcp.ProbeCount);
            Assert.Equal(1, websocket.ProbeCount);
            Assert.Equal(TransportId.WebSocket, plan.ConnectOptions.ProviderEndpoint.MatchesTransport(TransportId.WebSocket)
                ? TransportId.WebSocket
                : TransportId.Unspecified);
            Assert.Equal(2, plan.Selection.Candidates.Count);
            Assert.All(plan.Selection.Candidates, candidate => Assert.Equal(NnrpTransportProbeState.Succeeded, candidate.ProbeState));
        }

        [Fact]
        public async Task SingleEligibleProviderSkipsProbe()
        {
            var tcp = Provider(TransportId.Tcp, "tcp", throughput: 1_000, rtt: 100);

            var plan = await NnrpClientTransportOrchestrator.ResolveAsync(Options(new[] { tcp }));

            Assert.Same(tcp, plan.Provider);
            Assert.Equal(0, tcp.ProbeCount);
            Assert.Equal(NnrpTransportProbeState.NotRun, plan.Selection.Candidates[0].ProbeState);
        }

        [Fact]
        public async Task ForceRestrictsSelectionWithoutFallbackOrProbe()
        {
            var tcp = Provider(TransportId.Tcp, "tcp", throughput: 1_000, rtt: 100);
            var websocket = Provider(TransportId.WebSocket, "websocket", throughput: 2_000, rtt: 80);

            var plan = await NnrpClientTransportOrchestrator.ResolveAsync(
                Options(new[] { tcp, websocket }, TransportPolicy.ForceTcp));

            Assert.Same(tcp, plan.Provider);
            Assert.Equal(0, tcp.ProbeCount);
            Assert.Equal(0, websocket.ProbeCount);
            Assert.Contains(
                plan.Selection.Candidates,
                candidate => candidate.TransportId == TransportId.WebSocket
                    && candidate.RejectionReason == NnrpTransportRejectionReason.PolicyDisallowed);
        }

        [Fact]
        public async Task ConfiguredButUninstalledProviderRemainsInDiagnostics()
        {
            var tcp = Provider(TransportId.Tcp, "tcp", throughput: 1_000, rtt: 100);
            var routes = new Dictionary<TransportId, NnrpClientProviderRoute>
            {
                [TransportId.WebSocket] = new NnrpClientProviderRoute
                {
                    ProviderEndpoint = NnrpProviderEndpoint.Parse("ws://localhost:9000/nnrp"),
                },
            };

            var plan = await NnrpClientTransportOrchestrator.ResolveAsync(
                new NnrpClientOptions(
                    NnrpEndpoint.Parse("nnrp://localhost:7000"),
                    routes,
                    transports: new[] { tcp }));

            Assert.Same(tcp, plan.Provider);
            Assert.Contains(
                plan.Selection.Candidates,
                candidate => candidate.TransportId == TransportId.WebSocket
                    && candidate.RejectionReason == NnrpTransportRejectionReason.LocalUnavailable);
        }

        [Fact]
        public async Task FailedProbeStaysDistinctFromMissingEvidence()
        {
            var tcp = Provider(TransportId.Tcp, "tcp", throughput: 1_000, rtt: 100);
            var websocket = Provider(TransportId.WebSocket, "websocket", throughput: 2_000, rtt: 80);
            websocket.ProbeFailure = new InvalidOperationException("probe refused");

            var plan = await NnrpClientTransportOrchestrator.ResolveAsync(Options(new[] { tcp, websocket }));

            Assert.Same(tcp, plan.Provider);
            Assert.Contains(
                plan.Selection.Candidates,
                candidate => candidate.TransportId == TransportId.WebSocket
                    && candidate.ProbeState == NnrpTransportProbeState.Failed
                    && candidate.RejectionReason == NnrpTransportRejectionReason.ProbeFailed
                    && candidate.Diagnostic == "probe refused");
        }

        [Fact]
        public async Task ConnectInvokesOnlyTheSelectedProvider()
        {
            var tcp = Provider(TransportId.Tcp, "tcp", throughput: 1_000, rtt: 100);
            var websocket = Provider(TransportId.WebSocket, "websocket", throughput: 2_000, rtt: 80);

            var error = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await NnrpClientTransportOrchestrator.ConnectAsync(Options(new[] { tcp, websocket })));

            Assert.Equal("The selected transport provider returned a null connection.", error.Message);
            Assert.Equal(0, tcp.ConnectCount);
            Assert.Equal(1, websocket.ConnectCount);
        }

        [Fact]
        public async Task OrchestratorRejectsNullOptionsAndCancellation()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await NnrpClientTransportOrchestrator.ConnectAsync(null!));
            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await NnrpClientTransportOrchestrator.ResolveAsync(null!));

            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
                await NnrpClientTransportOrchestrator.ResolveAsync(
                    Options(new[] { Provider(TransportId.Tcp, "tcp", 1_000, 100) }),
                    cancellation.Token));
        }

        [Theory]
        [InlineData(TransportPolicy.ForceQuic, TransportId.Quic)]
        [InlineData(TransportPolicy.ForceIpc, TransportId.Ipc)]
        [InlineData(TransportPolicy.ForceWebSocket, TransportId.WebSocket)]
        public async Task EveryForcedPolicySelectsOnlyItsNamedProvider(
            TransportPolicy policy,
            TransportId selectedTransport)
        {
            var providers = new[]
            {
                Provider(TransportId.Tcp, "tcp", 1_000, 100),
                Provider(selectedTransport, selectedTransport.ToString().ToLowerInvariant(), 2_000, 80),
            };
            var routes = new Dictionary<TransportId, NnrpClientProviderRoute>();
            if (selectedTransport == TransportId.Quic)
            {
                routes[selectedTransport] = new NnrpClientProviderRoute
                {
                    Security = new NnrpTransportClientSecurity("localhost", new byte[] { 1 }),
                };
            }
            else if (selectedTransport == TransportId.Ipc)
            {
                routes[selectedTransport] = new NnrpClientProviderRoute
                {
                    ProviderEndpoint = NnrpProviderEndpoint.Parse(
                        OperatingSystem.IsWindows() ? "npipe://nnrp-test" : "unix:///tmp/nnrp-test.sock"),
                };
            }
            else
            {
                routes[selectedTransport] = new NnrpClientProviderRoute
                {
                    ProviderEndpoint = NnrpProviderEndpoint.Parse("ws://localhost:9000/nnrp"),
                };
            }

            var plan = await NnrpClientTransportOrchestrator.ResolveAsync(
                new NnrpClientOptions(
                    NnrpEndpoint.Parse("nnrp://localhost:7000"),
                    routes,
                    policy,
                    providers));

            Assert.Equal(selectedTransport, plan.Provider.Descriptor.TransportId);
            Assert.All(providers, provider => Assert.Equal(0, provider.ProbeCount));
        }

        private static NnrpClientOptions Options(
            IReadOnlyList<INnrpNativeTransportProvider> providers,
            TransportPolicy policy = TransportPolicy.Auto)
        {
            return new NnrpClientOptions(
                NnrpEndpoint.Parse("nnrp://localhost:7000"),
                new Dictionary<TransportId, NnrpClientProviderRoute>
                {
                    [TransportId.WebSocket] = new NnrpClientProviderRoute
                    {
                        ProviderEndpoint = NnrpProviderEndpoint.Parse("ws://localhost:9000/nnrp"),
                    },
                },
                policy,
                providers);
        }

        private static FakeProvider Provider(
            TransportId transportId,
            string id,
            ulong throughput,
            ulong rtt)
        {
            return new FakeProvider(
                new NnrpTransportProviderDescriptor(
                    transportId.ToString(),
                    "1",
                    transportId,
                    NnrpTransportProviderKind.NativeDynamic,
                    available: true,
                    libraryPath: null,
                    new NnrpTransportProviderMetadata(
                        id,
                        new NnrpTransportProviderCost(0, 0),
                        preferenceRank: 0,
                        new NnrpTransportProviderLimits(16 * 1024 * 1024),
                        Array.Empty<NnrpTransportProviderLimitation>())),
                new NnrpTransportProbeMetrics(3, 3, throughput, rtt));
        }

        private sealed class FakeProvider : INnrpNativeTransportProvider
        {
            private readonly NnrpTransportProbeMetrics metrics;

            internal FakeProvider(
                NnrpTransportProviderDescriptor descriptor,
                NnrpTransportProbeMetrics metrics)
            {
                Descriptor = descriptor;
                this.metrics = metrics;
            }

            internal int ConnectCount { get; private set; }

            internal int ProbeCount { get; private set; }

            internal Exception? ProbeFailure { get; set; }

            public NnrpTransportProviderDescriptor Descriptor { get; }

            public ValueTask<NnrpTransportConnection> ConnectAsync(
                NnrpTransportConnectOptions options,
                CancellationToken cancellationToken = default)
            {
                ConnectCount++;
                return new ValueTask<NnrpTransportConnection>((NnrpTransportConnection)null!);
            }

            public ValueTask<NnrpTransportListener> ListenAsync(
                NnrpTransportListenOptions options,
                CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();

            public ValueTask<NnrpTransportProbeMetrics> ProbeAsync(
                Nnrp.Core.NnrpTransportProbeOptions options,
                CancellationToken cancellationToken = default)
            {
                ProbeCount++;
                if (ProbeFailure != null)
                {
                    throw ProbeFailure;
                }

                return new ValueTask<NnrpTransportProbeMetrics>(metrics);
            }
        }
    }
}
