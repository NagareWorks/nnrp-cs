using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nnrp.Core;
using Xunit;

namespace Nnrp.NativeBridge.Tests
{
    public sealed class NnrpNativeTransportRegistryTests
    {
        [Fact]
        public void RegistryRejectsDuplicateTransportAndProviderIdentity()
        {
            var tcp = Provider(TransportId.Tcp, "provider.tcp");
            var registry = new NnrpNativeTransportRegistry(new[] { tcp });

            Assert.Throws<ArgumentNullException>(() => registry.Register(null!));
            Assert.Throws<ArgumentException>(() => registry.Register(Provider(TransportId.Tcp, "provider.tcp.other")));
            Assert.Throws<ArgumentException>(() => registry.Register(Provider(TransportId.Quic, "provider.tcp")));
            Assert.Same(tcp, Assert.Single(registry.Snapshot()));
        }

        [Fact]
        public void UnavailableProviderOwnsTypedEvidenceAndCannotOpenNativePaths()
        {
            const ulong maxFrameBytes = 16 * 1024 * 1024;
            var provider = new NnrpUnavailableTransportProvider(TransportId.WebSocket, maxFrameBytes);
            var endpoint = NnrpEndpoint.Parse("nnrp://runtime.example:7443");
            var providerEndpoint = NnrpProviderEndpoint.Parse("ws://runtime.example:7443/nnrp");
            var connect = new NnrpTransportConnectOptions(
                endpoint,
                providerEndpoint,
                security: null,
                maxPacketBytes: maxFrameBytes);
            var listen = new NnrpTransportListenOptions(
                endpoint,
                providerEndpoint,
                security: null,
                maxPacketBytes: maxFrameBytes);
            var probe = new NnrpTransportProbeOptions(
                endpoint,
                providerEndpoint,
                sampleCount: 1,
                payloadBytes: 1,
                security: null,
                maxPacketBytes: maxFrameBytes,
                includeWarmup: false);

            Assert.Equal(TransportId.WebSocket, provider.Descriptor.TransportId);
            Assert.False(provider.Descriptor.Available);
            Assert.Equal(maxFrameBytes, provider.Descriptor.Metadata.Limits.MaxFrameBytes);
            Assert.Equal(
                "A route is configured but its transport provider package is not installed.",
                provider.Descriptor.Diagnostic);
            Assert.Throws<InvalidOperationException>(() => provider.ConnectAsync(connect));
            Assert.Throws<InvalidOperationException>(() => provider.ListenAsync(listen));
            Assert.Throws<InvalidOperationException>(() => provider.ProbeAsync(probe));
        }

        [Fact]
        public void SnapshotIsImmutableAndUsesStableTransportOrder()
        {
            var registry = new NnrpNativeTransportRegistry(new[]
            {
                Provider(TransportId.WebSocket, "provider.websocket"),
                Provider(TransportId.Quic, "provider.quic"),
                Provider(TransportId.Ipc, "provider.ipc"),
                Provider(TransportId.Tcp, "provider.tcp"),
            });

            var snapshot = registry.Snapshot();

            Assert.Equal(
                new[] { TransportId.Quic, TransportId.Tcp, TransportId.Ipc, TransportId.WebSocket },
                snapshot.Select(value => value.Descriptor.TransportId));
            Assert.IsAssignableFrom<IReadOnlyList<INnrpNativeTransportProvider>>(snapshot);
            Assert.Throws<NotSupportedException>(() => ((IList<INnrpNativeTransportProvider>)snapshot).Clear());
        }

        [Fact]
        public void ResolveSelectsSingleEligibleProviderWithoutProbe()
        {
            var tcp = Provider(TransportId.Tcp, "provider.tcp");
            var registry = new NnrpNativeTransportRegistry(new[] { tcp });
            var selection = registry.Resolve(Options(
                new[] { tcp },
                policy: TransportPolicy.Auto,
                observations: Array.Empty<NnrpTransportProbeObservation>()));

            Assert.Same(tcp.Descriptor, selection.SelectedProvider);
            var candidate = Assert.Single(selection.Candidates);
            Assert.Equal(NnrpTransportProbeState.NotRun, candidate.ProbeState);
            Assert.Equal((uint)0, candidate.SelectionRank);
            Assert.Null(candidate.Probe);
        }

        [Fact]
        public void ResolveUsesFrozenProbeComparatorAndPreservesRejectedCandidates()
        {
            var quic = Provider(
                TransportId.Quic,
                "provider.quic",
                preferenceRank: 10,
                costModelId: 7,
                costUnits: 20);
            var tcp = Provider(
                TransportId.Tcp,
                "provider.tcp",
                preferenceRank: 1,
                costModelId: 7,
                costUnits: 10);
            var ipc = Provider(TransportId.Ipc, "provider.ipc", available: false);
            var providers = new[] { tcp, ipc, quic };
            var registry = new NnrpNativeTransportRegistry(providers);
            var observations = new[]
            {
                Succeeded(tcp, successCount: 2, throughput: 1000, rtt: 50),
                Succeeded(quic, successCount: 3, throughput: 500, rtt: 100),
            };

            var selection = registry.Resolve(Options(providers, observations: observations));

            Assert.Same(quic.Descriptor, selection.SelectedProvider);
            Assert.Equal(new[] { TransportId.Quic, TransportId.Tcp, TransportId.Ipc },
                selection.Candidates.Select(value => value.TransportId));
            Assert.Equal(new uint?[] { 0, 1, null }, selection.Candidates.Select(value => value.SelectionRank));
            Assert.Equal(NnrpTransportRejectionReason.LocalUnavailable, selection.Candidates[2].RejectionReason);
        }

        [Fact]
        public void ResolveDistinguishesFailedAndMissingProbeEvidence()
        {
            var quic = Provider(TransportId.Quic, "provider.quic");
            var tcp = Provider(TransportId.Tcp, "provider.tcp");
            var registry = new NnrpNativeTransportRegistry(new[] { quic, tcp });

            var error = Assert.Throws<NnrpTransportSelectionException>(() => registry.Resolve(Options(
                new[] { quic, tcp },
                observations: new[]
                {
                    new NnrpTransportProbeObservation(
                        TransportId.Quic,
                        quic.Descriptor.Metadata.Id,
                        NnrpTransportProbeState.Failed,
                        diagnostic: "probe refused"),
                })));

            Assert.Equal(NnrpTransportSelectionErrorCode.NoViableTransport, error.Code);
            Assert.Equal(NnrpTransportRejectionReason.ProbeFailed,
                error.Candidates.Single(value => value.TransportId == TransportId.Quic).RejectionReason);
            Assert.Equal("probe refused",
                error.Candidates.Single(value => value.TransportId == TransportId.Quic).Diagnostic);
            Assert.Equal(NnrpTransportRejectionReason.ProbeMissing,
                error.Candidates.Single(value => value.TransportId == TransportId.Tcp).RejectionReason);
        }

        [Fact]
        public void ResolveAppliesFrozenRejectionPrecedence()
        {
            var tcp = Provider(TransportId.Tcp, "provider.tcp", available: false, maxFrameBytes: 64);
            var registry = new NnrpNativeTransportRegistry(new[] { tcp });
            var options = new NnrpTransportSelectionOptions(
                Array.Empty<TransportId>(),
                new[]
                {
                    new NnrpTransportCandidateReadiness(
                        TransportId.Tcp,
                        tcp.Descriptor.Metadata.Id,
                        routeResolved: false,
                        securitySatisfied: false),
                },
                TransportPolicy.ForceQuic,
                requestedMaxFrameBytes: 128);

            var error = Assert.Throws<NnrpTransportSelectionException>(() => registry.Resolve(options));

            Assert.Equal(NnrpTransportSelectionErrorCode.ForcedTransportUnavailable, error.Code);
            Assert.Equal(NnrpTransportRejectionReason.PolicyDisallowed, Assert.Single(error.Candidates).RejectionReason);
            Assert.Equal(TransportPolicy.ForceQuic, error.Policy);
        }

        [Fact]
        public void ResolveRejectsIncompleteDuplicateAndUnmatchedEvidenceBeforeSelection()
        {
            var tcp = Provider(TransportId.Tcp, "provider.tcp");
            var registry = new NnrpNativeTransportRegistry(new[] { tcp });
            var ready = Ready(tcp);
            var cases = new[]
            {
                new NnrpTransportSelectionOptions(new[] { TransportId.Tcp }, Array.Empty<NnrpTransportCandidateReadiness>()),
                new NnrpTransportSelectionOptions(new[] { TransportId.Tcp }, new[] { ready, ready }),
                new NnrpTransportSelectionOptions(
                    new[] { TransportId.Tcp },
                    new[]
                    {
                        new NnrpTransportCandidateReadiness(TransportId.Quic, "provider.quic", true, true),
                    }),
                new NnrpTransportSelectionOptions(
                    new[] { TransportId.Tcp },
                    new[] { ready },
                    probeObservations: new[]
                    {
                        new NnrpTransportProbeObservation(
                            TransportId.Quic,
                            "provider.quic",
                            NnrpTransportProbeState.Failed),
                    }),
                new NnrpTransportSelectionOptions(
                    new[] { TransportId.Tcp },
                    new[] { ready },
                    probeObservations: new[]
                    {
                        new NnrpTransportProbeObservation(
                            TransportId.Tcp,
                            tcp.Descriptor.Metadata.Id,
                            NnrpTransportProbeState.Failed),
                        new NnrpTransportProbeObservation(
                            TransportId.Tcp,
                            tcp.Descriptor.Metadata.Id,
                            NnrpTransportProbeState.Failed),
                    }),
            };

            foreach (var options in cases)
            {
                var error = Assert.Throws<NnrpTransportSelectionException>(() => registry.Resolve(options));
                Assert.Equal(NnrpTransportSelectionErrorCode.InvalidEvidence, error.Code);
                Assert.Null(error.Policy);
                Assert.Empty(error.Candidates);
            }
        }

        [Fact]
        public void PreferPolicyBreaksOtherwiseEqualProbeTies()
        {
            var quic = Provider(TransportId.Quic, "provider.quic", preferenceRank: 0);
            var tcp = Provider(TransportId.Tcp, "provider.tcp", preferenceRank: 0);
            var providers = new[] { quic, tcp };
            var observations = providers.Select(value => Succeeded(value, 1, 1000, 50)).ToArray();
            var registry = new NnrpNativeTransportRegistry(providers);

            var selection = registry.Resolve(Options(
                providers,
                TransportPolicy.PreferTcp,
                observations));

            Assert.Equal(TransportId.Tcp, selection.SelectedProvider.TransportId);
        }

        [Theory]
        [InlineData(4u, 3u, 1_000ul, 2_000ul, 50ul, 25ul, TransportId.Quic)]
        [InlineData(3u, 3u, 2_000ul, 1_000ul, 50ul, 25ul, TransportId.Quic)]
        [InlineData(3u, 3u, 1_000ul, 1_000ul, 25ul, 50ul, TransportId.Quic)]
        public void ResolveOrdersProbeMetricsBeforeProviderMetadata(
            uint quicSuccessCount,
            uint tcpSuccessCount,
            ulong quicThroughput,
            ulong tcpThroughput,
            ulong quicRtt,
            ulong tcpRtt,
            TransportId expected)
        {
            var quic = Provider(
                TransportId.Quic,
                "provider.quic",
                preferenceRank: ushort.MaxValue,
                costModelId: 7,
                costUnits: ulong.MaxValue);
            var tcp = Provider(
                TransportId.Tcp,
                "provider.tcp",
                preferenceRank: 0,
                costModelId: 7,
                costUnits: 0);
            var providers = new[] { quic, tcp };
            var registry = new NnrpNativeTransportRegistry(providers);

            var selection = registry.Resolve(Options(
                providers,
                observations: new[]
                {
                    Succeeded(quic, quicSuccessCount, quicThroughput, quicRtt),
                    Succeeded(tcp, tcpSuccessCount, tcpThroughput, tcpRtt),
                }));

            Assert.Equal(expected, selection.SelectedProvider.TransportId);
        }

        [Fact]
        public void ResolveComparesCostOnlyForTheSameNonZeroModel()
        {
            var quic = Provider(
                TransportId.Quic,
                "provider.quic",
                preferenceRank: 20,
                costModelId: 7,
                costUnits: 10);
            var tcp = Provider(
                TransportId.Tcp,
                "provider.tcp",
                preferenceRank: 0,
                costModelId: 7,
                costUnits: 20);

            Assert.Equal(
                TransportId.Quic,
                ResolveTiedProviders(quic, tcp).SelectedProvider.TransportId);

            quic = Provider(
                TransportId.Quic,
                "provider.quic",
                preferenceRank: 20,
                costModelId: 7,
                costUnits: 10);
            tcp = Provider(
                TransportId.Tcp,
                "provider.tcp",
                preferenceRank: 0,
                costModelId: 8,
                costUnits: 20);

            Assert.Equal(
                TransportId.Tcp,
                ResolveTiedProviders(quic, tcp).SelectedProvider.TransportId);
        }

        [Fact]
        public void ResolveUsesPreferenceRankThenStableTransportIdentity()
        {
            var quic = Provider(TransportId.Quic, "provider.z", preferenceRank: 10);
            var tcp = Provider(TransportId.Tcp, "provider.a", preferenceRank: 20);

            Assert.Equal(
                TransportId.Quic,
                ResolveTiedProviders(quic, tcp).SelectedProvider.TransportId);

            tcp = Provider(TransportId.Tcp, "provider.a", preferenceRank: 10);

            Assert.Equal(
                TransportId.Quic,
                ResolveTiedProviders(quic, tcp).SelectedProvider.TransportId);
        }

        [Theory]
        [InlineData(TransportPolicy.ForceQuic, false, true, true, true, true, NnrpTransportRejectionReason.PolicyDisallowed)]
        [InlineData(TransportPolicy.Auto, false, true, true, true, true, NnrpTransportRejectionReason.LocalUnavailable)]
        [InlineData(TransportPolicy.Auto, true, false, true, true, true, NnrpTransportRejectionReason.PeerUnsupported)]
        [InlineData(TransportPolicy.Auto, true, true, false, true, true, NnrpTransportRejectionReason.LimitExceeded)]
        [InlineData(TransportPolicy.Auto, true, true, true, false, false, NnrpTransportRejectionReason.RouteUnresolved)]
        [InlineData(TransportPolicy.Auto, true, true, true, true, false, NnrpTransportRejectionReason.SecurityUnsatisfied)]
        public void ResolveUsesFrozenPreProbeRejectionOrder(
            TransportPolicy policy,
            bool available,
            bool peerSupported,
            bool withinLimits,
            bool routeResolved,
            bool securitySatisfied,
            NnrpTransportRejectionReason expected)
        {
            const ulong requestedFrameBytes = 1024;
            var tcp = Provider(
                TransportId.Tcp,
                "provider.tcp",
                available: available,
                maxFrameBytes: withinLimits ? requestedFrameBytes : requestedFrameBytes - 1);
            var registry = new NnrpNativeTransportRegistry(new[] { tcp });
            var options = new NnrpTransportSelectionOptions(
                peerSupported ? new[] { TransportId.Tcp } : Array.Empty<TransportId>(),
                new[]
                {
                    new NnrpTransportCandidateReadiness(
                        TransportId.Tcp,
                        tcp.Descriptor.Metadata.Id,
                        routeResolved,
                        securitySatisfied),
                },
                policy,
                requestedFrameBytes);

            var error = Assert.Throws<NnrpTransportSelectionException>(() => registry.Resolve(options));

            Assert.Equal(expected, Assert.Single(error.Candidates).RejectionReason);
        }

        private static NnrpTransportSelection ResolveTiedProviders(
            FakeProvider first,
            FakeProvider second,
            TransportPolicy policy = TransportPolicy.Auto)
        {
            var providers = new[] { first, second };
            return new NnrpNativeTransportRegistry(providers).Resolve(Options(
                providers,
                policy,
                providers.Select(value => Succeeded(value, 3, 1_000, 50)).ToArray()));
        }

        private static NnrpTransportSelectionOptions Options(
            IReadOnlyCollection<FakeProvider> providers,
            TransportPolicy policy = TransportPolicy.Auto,
            IReadOnlyCollection<NnrpTransportProbeObservation>? observations = null)
        {
            return new NnrpTransportSelectionOptions(
                providers.Select(value => value.Descriptor.TransportId),
                providers.Select(Ready),
                policy,
                probeObservations: observations);
        }

        private static NnrpTransportCandidateReadiness Ready(FakeProvider provider)
        {
            return new NnrpTransportCandidateReadiness(
                provider.Descriptor.TransportId,
                provider.Descriptor.Metadata.Id,
                routeResolved: true,
                securitySatisfied: true);
        }

        private static NnrpTransportProbeObservation Succeeded(
            FakeProvider provider,
            uint successCount,
            ulong throughput,
            ulong rtt)
        {
            return new NnrpTransportProbeObservation(
                provider.Descriptor.TransportId,
                provider.Descriptor.Metadata.Id,
                NnrpTransportProbeState.Succeeded,
                new NnrpTransportProbeMetrics(successCount, successCount, throughput, rtt));
        }

        private static FakeProvider Provider(
            TransportId transportId,
            string providerId,
            bool available = true,
            ushort preferenceRank = 0,
            ushort costModelId = 0,
            ulong costUnits = 0,
            ulong maxFrameBytes = 16 * 1024 * 1024)
        {
            return new FakeProvider(new NnrpTransportProviderDescriptor(
                transportId.ToString(),
                "1",
                transportId,
                NnrpTransportProviderKind.NativeDynamic,
                available,
                null,
                new NnrpTransportProviderMetadata(
                    providerId,
                    new NnrpTransportProviderCost(costModelId, costUnits),
                    preferenceRank,
                    new NnrpTransportProviderLimits(maxFrameBytes),
                    Array.Empty<NnrpTransportProviderLimitation>())));
        }

        private sealed class FakeProvider : INnrpNativeTransportProvider
        {
            internal FakeProvider(NnrpTransportProviderDescriptor descriptor)
            {
                Descriptor = descriptor;
            }

            public NnrpTransportProviderDescriptor Descriptor { get; }

            public ValueTask<NnrpTransportConnection> ConnectAsync(
                NnrpTransportConnectOptions options,
                CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public ValueTask<NnrpTransportListener> ListenAsync(
                NnrpTransportListenOptions options,
                CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public ValueTask<NnrpTransportProbeMetrics> ProbeAsync(
                NnrpTransportProbeOptions options,
                CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }
        }
    }
}
