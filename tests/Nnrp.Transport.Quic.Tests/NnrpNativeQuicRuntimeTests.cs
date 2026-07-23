using System;
using Nnrp.Core;
using Nnrp.NativeBridge;
using Nnrp.Transport.Quic;
using Xunit;

namespace Nnrp.Transport.Quic.Tests
{
    public sealed class NnrpNativeQuicRuntimeTests
    {
        [Fact]
        public void NativeQuicProviderOwnsQuicRuntimeBinding()
        {
            var provider = NnrpNativeQuicTransportProvider.Instance;

            Assert.Equal(TransportId.Quic, provider.TransportId);
            Assert.Equal("native-quic", provider.BindingName);
            Assert.Equal(NnrpNativeArtifact.TransportSlotQuic, provider.NativeTransportSlot);
            Assert.Equal(NnrpNativeQuicRuntime.TransportId, provider.TransportId);
            Assert.Equal(NnrpNativeQuicRuntime.BindingName, provider.BindingName);
            Assert.Equal(NnrpNativeQuicRuntime.NativeTransportSlot, provider.NativeTransportSlot);
            Assert.True(provider.ProbePriority > 0);
        }

        [Fact]
        public void NativeQuicSessionOptionsPinQuicTransportIdAndCopyRuntimeSettings()
        {
            var fallback = new CapturingBackend();
            var options = new NnrpNativeQuicRuntimeSessionHostOptions(11, 2, 41, 3, 4, 5, 6)
            {
                BootstrapConnection = true,
                ArtifactPath = "quic-runtime.dll",
                ArtifactRoot = "native-root",
                FallbackBackend = fallback,
                FallbackPolicy = NnrpNativeRuntimeFallbackPolicy.UseFallbackForDiagnostics,
            };

            var native = NnrpNativeQuicRuntime.ToNativeOptions(options);

            Assert.Equal((uint)TransportId.Quic, native.TransportId);
            Assert.Equal((ulong)11, native.ConnectionId);
            Assert.Equal((uint)2, native.ConnectionGeneration);
            Assert.Equal((uint)41, native.SessionId);
            Assert.Equal((uint)3, native.SessionGeneration);
            Assert.Equal((ushort)4, native.ProfileId);
            Assert.Equal((uint)5, native.SchemaId);
            Assert.Equal((uint)6, native.SchemaVersion);
            Assert.True(native.BootstrapConnection);
            Assert.Equal("quic-runtime.dll", native.ArtifactPath);
            Assert.Equal("native-root", native.ArtifactRoot);
            Assert.Same(fallback, native.FallbackBackend);
            Assert.Equal(NnrpNativeRuntimeFallbackPolicy.UseFallbackForDiagnostics, native.FallbackPolicy);
        }

        [Fact]
        public void NativeQuicConnectionAndServerOptionsPinQuicTransportId()
        {
            var connectionOptions = new NnrpNativeQuicRuntimeConnectionHostOptions(12, 7)
            {
                BootstrapConnection = true,
                ArtifactPath = "quic-runtime.dll",
                ArtifactRoot = "native-root",
                FallbackPolicy = NnrpNativeRuntimeFallbackPolicy.UseFallbackForDiagnostics,
            };
            var serverOptions = new NnrpNativeQuicRuntimeServerHostOptions(51, 8)
            {
                ArtifactPath = "quic-runtime.dll",
                ArtifactRoot = "native-root",
            };

            var nativeConnection = NnrpNativeQuicRuntime.ToNativeOptions(connectionOptions);
            var nativeServer = NnrpNativeQuicRuntime.ToNativeOptions(serverOptions);

            Assert.Equal((uint)TransportId.Quic, nativeConnection.TransportId);
            Assert.Equal((ulong)12, nativeConnection.ConnectionId);
            Assert.Equal((uint)7, nativeConnection.ConnectionGeneration);
            Assert.True(nativeConnection.BootstrapConnection);
            Assert.Equal("quic-runtime.dll", nativeConnection.ArtifactPath);
            Assert.Equal("native-root", nativeConnection.ArtifactRoot);
            Assert.Equal(NnrpNativeRuntimeFallbackPolicy.UseFallbackForDiagnostics, nativeConnection.FallbackPolicy);
            Assert.Equal((uint)TransportId.Quic, nativeServer.TransportId);
            Assert.Equal((ulong)51, nativeServer.ServerId);
            Assert.Equal((uint)8, nativeServer.ServerGeneration);
            Assert.Equal("quic-runtime.dll", nativeServer.ArtifactPath);
            Assert.Equal("native-root", nativeServer.ArtifactRoot);
        }

        [Fact]
        public void NativeQuicRuntimeRejectsNullOptions()
        {
            Assert.Throws<ArgumentNullException>(() =>
                NnrpNativeQuicRuntime.ToNativeOptions((NnrpNativeQuicRuntimeSessionHostOptions)null!));
            Assert.Throws<ArgumentNullException>(() =>
                NnrpNativeQuicRuntime.ToNativeOptions((NnrpNativeQuicRuntimeConnectionHostOptions)null!));
            Assert.Throws<ArgumentNullException>(() =>
                NnrpNativeQuicRuntime.ToNativeOptions((NnrpNativeQuicRuntimeServerHostOptions)null!));
            Assert.Throws<ArgumentNullException>(() =>
                NnrpNativeQuicRuntime.OpenSessionHost((NnrpNativeQuicRuntimeSessionHostOptions)null!));
            Assert.Throws<ArgumentNullException>(() =>
                NnrpNativeQuicRuntime.OpenConnectionHost((NnrpNativeQuicRuntimeConnectionHostOptions)null!));
            Assert.Throws<ArgumentNullException>(() =>
                NnrpNativeQuicRuntime.OpenServerHost((NnrpNativeQuicRuntimeServerHostOptions)null!));
        }

        [Fact]
        public void NativeTransportResolverProbesWhenTcpAndQuicAreBothInstalled()
        {
            var probeResult = new NnrpNativeProbeResult(
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
                transportSlots: NnrpNativeArtifact.TransportSlotTcp | NnrpNativeArtifact.TransportSlotQuic,
                featureFlags: NnrpNativeArtifact.RequiredRuntimeFeatures);

            var resolution = NnrpNativeTransportResolver.Resolve(
                probeResult,
                new INnrpNativeTransportProvider[]
                {
                    Nnrp.Transport.Tcp.NnrpNativeTcpTransportProvider.Instance,
                    NnrpNativeQuicTransportProvider.Instance,
                });

            Assert.True(resolution.ShouldProbe);
            Assert.Equal(2, resolution.AvailableProviders.Length);
            Assert.Equal(TransportId.Quic, resolution.SelectedProvider.TransportId);
        }

        private sealed class CapturingBackend : INnrpNativeRuntimeBackend
        {
            public NnrpNativeRuntimeConnection Connect(ulong connectionId, uint generation, uint transportId)
            {
                throw new NotSupportedException();
            }

            public NnrpNativeRuntimeConnection BootstrapConnection(ulong connectionId, uint generation, uint transportId)
            {
                throw new NotSupportedException();
            }
        }
    }
}
