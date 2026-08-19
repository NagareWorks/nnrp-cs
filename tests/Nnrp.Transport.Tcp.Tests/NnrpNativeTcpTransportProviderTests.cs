using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Nnrp.Core;
using Nnrp.NativeBridge;
using Nnrp.Runtime;
using Nnrp.Transport.Tcp;
using Xunit;

namespace Nnrp.Transport.Tcp.Tests
{
    public sealed class NnrpNativeTcpTransportProviderTests
    {
        [Fact]
        public void NativeTcpProviderOwnsTcpRuntimeBinding()
        {
            var provider = NnrpNativeTcpTransportProvider.Instance;

            Assert.Equal(TransportId.Tcp, provider.Descriptor.TransportId);
            Assert.Equal("tcp", provider.Descriptor.Name);
            Assert.Equal("nnrp.transport.tcp.native", provider.Descriptor.Metadata.Id);
            Assert.Equal((ushort)2, provider.Descriptor.Metadata.PreferenceRank);
            Assert.Contains(
                NnrpTransportProviderLimitation.RequiresTcp,
                provider.Descriptor.Metadata.Limitations);
        }

        [LiveNativeTransportFact]
        public async Task NativeTcpProviderOpensRealListenerAndConnection()
        {
            var artifactPath = RequiredArtifactPath();
            var provider = new NnrpNativeTcpTransportProvider(artifactPath);
            var endpoint = NnrpEndpoint.Parse("nnrp://127.0.0.1:0");
            await using var listener = await provider.ListenAsync(
                new NnrpTransportListenOptions(
                    endpoint,
                    NnrpProviderEndpoint.Parse("tcp://127.0.0.1:0")));

            Assert.Equal(TransportId.Tcp, listener.TransportId);
            Assert.NotEqual(0, new Uri(listener.BoundEndpoint.ToString()).Port);

            await using var connection = await provider.ConnectAsync(
                new NnrpTransportConnectOptions(endpoint, listener.BoundEndpoint));
            Assert.Equal(TransportId.Tcp, connection.TransportId);
        }

        [LiveNativeTransportFact]
        public async Task NativeTcpProviderCarriesRealClientServerSessionLifecycle()
        {
            var provider = new NnrpNativeTcpTransportProvider(RequiredArtifactPath());
            var endpoint = NnrpEndpoint.Parse("nnrp://127.0.0.1:0");
            await using var listener = await provider.ListenAsync(
                new NnrpTransportListenOptions(
                    endpoint,
                    NnrpProviderEndpoint.Parse("tcp://127.0.0.1:0")));
            using var server = NnrpNativeRuntimeServer.Bind(
                listener,
                new NnrpNativeRuntimeServerHostOptions(50, 1));
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var acceptStarted = new ManualResetEventSlim();
            var acceptTask = Task.Factory.StartNew(
                () => AcceptSession(server, timeout.Token, acceptStarted),
                timeout.Token,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);

            Assert.True(acceptStarted.Wait(TimeSpan.FromSeconds(5)));
            await using var transport = await provider.ConnectAsync(
                new NnrpTransportConnectOptions(endpoint, listener.BoundEndpoint));
            using var client = NnrpNativeRuntimeConnectionHost.Open(
                transport,
                new NnrpNativeRuntimeConnectionHostOptions(60, 1));
            var clientSession = client.OpenSession(
                new NnrpNativeRuntimeSessionOptions(70, 1, 2, 0x1001, 3));
            var serverSession = await acceptTask;

            clientSession.SendTraceContext(
                0,
                new TraceContextMetadata(101, 201, 301, 1, 0, 1),
                new byte[] { 11 });
            var runtimeEvent = Assert.Single(
                serverSession.AwaitEvents(timeoutMilliseconds: 5_000));
            Assert.Equal((uint)MessageType.TraceContext, runtimeEvent.MessageType);
            Assert.Equal((uint)0, runtimeEvent.FrameId);
            var decoded = NnrpRuntimeControl.Decode(
                MessageType.TraceContext,
                runtimeEvent.PayloadSpan);
            Assert.Equal((ulong)101, decoded.GetMetadata<TraceContextMetadata>().TraceId);
            Assert.Equal(new byte[] { 11 }, decoded.Tail.ToArray());

            var clientClose = Task.Factory.StartNew(
                () => Assert.True(client.CloseSession(70)),
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
            var closeEvent = Assert.Single(
                serverSession.AwaitEvents(timeoutMilliseconds: 5_000));
            Assert.Equal((uint)MessageType.SessionClose, closeEvent.MessageType);
            serverSession.Close();
            await clientClose;
        }

        private static string RequiredArtifactPath()
        {
            var artifactPath = Environment.GetEnvironmentVariable(
                LiveNativeTransportFactAttribute.ArtifactPathVariableName);
            Assert.False(string.IsNullOrWhiteSpace(artifactPath));
            Assert.True(File.Exists(artifactPath));
            return artifactPath!;
        }

        private static NnrpNativeRuntimeServerSession AcceptSession(
            NnrpNativeRuntimeServer server,
            CancellationToken cancellationToken,
            ManualResetEventSlim started)
        {
            started.Set();
            cancellationToken.ThrowIfCancellationRequested();
            return server.AcceptSession(71, 1, timeoutMilliseconds: 10_000);
        }
    }
}
