using System;
using Nnrp.Core;
using Xunit;

namespace Nnrp.NativeBridge.Tests
{
    public sealed class NnrpNativeTransportResolverTests
    {
        [Fact]
        public void ResolveSelectsHighestPriorityInstalledProviderForAutoPolicy()
        {
            var resolution = NnrpNativeTransportResolver.Resolve(
                Probe(
                    NnrpNativeArtifact.TransportSlotTcp
                    | NnrpNativeArtifact.TransportSlotQuic
                    | NnrpNativeArtifact.TransportSlotIpc
                    | NnrpNativeArtifact.TransportSlotWebSocket),
                new INnrpNativeTransportProvider[]
                {
                    Provider(TransportId.Tcp, "tcp", NnrpNativeArtifact.TransportSlotTcp, priority: 10),
                    Provider(TransportId.Quic, "quic", NnrpNativeArtifact.TransportSlotQuic, priority: 20),
                });

            Assert.Equal(TransportId.Quic, resolution.SelectedProvider.TransportId);
            Assert.True(resolution.ShouldProbe);
            Assert.Equal((uint)TransportId.Quic, resolution.TransportId);
            Assert.Equal(2, resolution.AvailableProviders.Length);
        }

        [Theory]
        [InlineData(TransportPolicy.PreferTcp, TransportId.Tcp, true)]
        [InlineData(TransportPolicy.PreferQuic, TransportId.Quic, true)]
        [InlineData(TransportPolicy.PreferIpc, TransportId.Ipc, true)]
        [InlineData(TransportPolicy.PreferWebSocket, TransportId.WebSocket, true)]
        [InlineData(TransportPolicy.ForceTcp, TransportId.Tcp, false)]
        [InlineData(TransportPolicy.ForceQuic, TransportId.Quic, false)]
        [InlineData(TransportPolicy.ForceIpc, TransportId.Ipc, false)]
        [InlineData(TransportPolicy.ForceWebSocket, TransportId.WebSocket, false)]
        public void ResolveHonorsExplicitTransportPolicy(
            TransportPolicy policy,
            TransportId expectedTransportId,
            bool expectedProbe)
        {
            var resolution = NnrpNativeTransportResolver.Resolve(
                Probe(
                    NnrpNativeArtifact.TransportSlotTcp
                    | NnrpNativeArtifact.TransportSlotQuic
                    | NnrpNativeArtifact.TransportSlotIpc
                    | NnrpNativeArtifact.TransportSlotWebSocket),
                new INnrpNativeTransportProvider[]
                {
                    Provider(TransportId.Tcp, "tcp", NnrpNativeArtifact.TransportSlotTcp, priority: 10),
                    Provider(TransportId.Quic, "quic", NnrpNativeArtifact.TransportSlotQuic, priority: 20),
                    Provider(TransportId.Ipc, "ipc", NnrpNativeArtifact.TransportSlotIpc, priority: 30),
                    Provider(TransportId.WebSocket, "websocket", NnrpNativeArtifact.TransportSlotWebSocket, priority: 5),
                },
                policy);

            Assert.Equal(expectedTransportId, resolution.SelectedProvider.TransportId);
            Assert.Equal(expectedProbe, resolution.ShouldProbe);
        }

        [Fact]
        public void ResolveFiltersProvidersByArtifactSlots()
        {
            var resolution = NnrpNativeTransportResolver.Resolve(
                Probe(NnrpNativeArtifact.TransportSlotTcp),
                new INnrpNativeTransportProvider[]
                {
                    Provider(TransportId.Tcp, "tcp", NnrpNativeArtifact.TransportSlotTcp, priority: 10),
                    Provider(TransportId.Quic, "quic", NnrpNativeArtifact.TransportSlotQuic, priority: 20),
                });

            Assert.Equal(TransportId.Tcp, resolution.SelectedProvider.TransportId);
            Assert.False(resolution.ShouldProbe);
            Assert.Single(resolution.AvailableProviders);
        }

        [Fact]
        public void ResolveRejectsUnavailableForcedProvider()
        {
            var error = Assert.Throws<InvalidOperationException>(() =>
                NnrpNativeTransportResolver.Resolve(
                    Probe(NnrpNativeArtifact.TransportSlotTcp),
                    new INnrpNativeTransportProvider[]
                    {
                        Provider(TransportId.Tcp, "tcp", NnrpNativeArtifact.TransportSlotTcp, priority: 10),
                        Provider(TransportId.Quic, "quic", NnrpNativeArtifact.TransportSlotQuic, priority: 20),
                    },
                    TransportPolicy.ForceQuic));

            Assert.Contains("Quic", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ResolveRejectsMissingAndInvalidProviders()
        {
            Assert.Throws<ArgumentNullException>(() =>
                NnrpNativeTransportResolver.Resolve(Probe(NnrpNativeArtifact.TransportSlotTcp), null!));
            Assert.Throws<ArgumentException>(() =>
                NnrpNativeTransportResolver.Resolve(Probe(NnrpNativeArtifact.TransportSlotTcp), Array.Empty<INnrpNativeTransportProvider>()));
            Assert.Throws<ArgumentException>(() =>
                NnrpNativeTransportResolver.Resolve(
                    Probe(NnrpNativeArtifact.TransportSlotTcp),
                    new INnrpNativeTransportProvider?[] { null! }!));
            Assert.Throws<ArgumentException>(() =>
                NnrpNativeTransportResolver.Resolve(
                    Probe(NnrpNativeArtifact.TransportSlotTcp),
                    new[] { Provider(TransportId.Unspecified, "tcp", NnrpNativeArtifact.TransportSlotTcp, priority: 10) }));
            Assert.Throws<ArgumentException>(() =>
                NnrpNativeTransportResolver.Resolve(
                    Probe(NnrpNativeArtifact.TransportSlotTcp),
                    new[] { Provider(TransportId.Tcp, "tcp", 0, priority: 10) }));
            Assert.Throws<ArgumentException>(() =>
                NnrpNativeTransportResolver.Resolve(
                    Probe(NnrpNativeArtifact.TransportSlotTcp),
                    new[] { Provider(TransportId.Tcp, " ", NnrpNativeArtifact.TransportSlotTcp, priority: 10) }));
            Assert.Throws<ArgumentException>(() =>
                NnrpNativeTransportResolver.Resolve(
                    Probe(NnrpNativeArtifact.TransportSlotTcp),
                    new[]
                    {
                        Provider(TransportId.Tcp, "tcp-a", NnrpNativeArtifact.TransportSlotTcp, priority: 10),
                        Provider(TransportId.Tcp, "tcp-b", NnrpNativeArtifact.TransportSlotTcp, priority: 20),
                    }));
        }

        [Fact]
        public void ResolveRejectsArtifactsWithoutMatchingInstalledTransportSlots()
        {
            var error = Assert.Throws<InvalidOperationException>(() =>
                NnrpNativeTransportResolver.Resolve(
                    Probe(NnrpNativeArtifact.TransportSlotTcp),
                    new[] { Provider(TransportId.Quic, "quic", NnrpNativeArtifact.TransportSlotQuic, priority: 20) }));

            Assert.Contains("No installed native transport provider", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ResolveRejectsUnknownPolicy()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                NnrpNativeTransportResolver.Resolve(
                    Probe(NnrpNativeArtifact.TransportSlotTcp),
                    new[] { Provider(TransportId.Tcp, "tcp", NnrpNativeArtifact.TransportSlotTcp, priority: 10) },
                    unchecked((TransportPolicy)999)));
        }

        private static NnrpNativeProbeResult Probe(uint transportSlots)
        {
            return new NnrpNativeProbeResult(
                artifactPath: "fixture",
                abiMajor: NnrpNativeArtifact.ExpectedAbiMajor,
                abiMinor: NnrpNativeArtifact.ExpectedAbiMinor,
                abiPatch: 0,
                protocolMajor: NnrpNativeArtifact.ExpectedProtocolMajor,
                protocolWireFormat: NnrpNativeArtifact.ExpectedProtocolWireFormat,
                sdkMajor: 1,
                sdkMinor: 0,
                sdkPatch: 0,
                sdkChannel: 3,
                sdkRevision: 0,
                transportSlots: transportSlots,
                featureFlags: NnrpNativeArtifact.RequiredRuntimeFeatures);
        }

        private static INnrpNativeTransportProvider Provider(
            TransportId transportId,
            string bindingName,
            uint nativeTransportSlot,
            int priority)
        {
            return new StubProvider(transportId, bindingName, nativeTransportSlot, priority);
        }

        private sealed class StubProvider : INnrpNativeTransportProvider
        {
            public StubProvider(
                TransportId transportId,
                string bindingName,
                uint nativeTransportSlot,
                int probePriority)
            {
                TransportId = transportId;
                BindingName = bindingName;
                NativeTransportSlot = nativeTransportSlot;
                ProbePriority = probePriority;
            }

            public TransportId TransportId { get; }

            public string BindingName { get; }

            public uint NativeTransportSlot { get; }

            public int ProbePriority { get; }
        }
    }
}
