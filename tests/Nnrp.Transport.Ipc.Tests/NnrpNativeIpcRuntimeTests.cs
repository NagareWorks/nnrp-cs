using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Nnrp.Core;
using Nnrp.NativeBridge;
using Xunit;

namespace Nnrp.Transport.Ipc.Tests
{
    public sealed class NnrpNativeIpcTransportProviderTests
    {
        [Fact]
        public void NativeIpcProviderOwnsPlatformSpecificIpcBinding()
        {
            var windows = new NnrpNativeIpcTransportProvider(
                platform: new NnrpNativePlatform("windows", "x86_64"));
            var unix = new NnrpNativeIpcTransportProvider(
                platform: new NnrpNativePlatform("linux", "x86_64"));

            Assert.Equal(TransportId.Ipc, windows.Descriptor.TransportId);
            Assert.Equal("ipc", windows.Descriptor.Name);
            Assert.Equal("nnrp.transport.ipc.native", windows.Descriptor.Metadata.Id);
            Assert.Equal((ushort)0, windows.Descriptor.Metadata.PreferenceRank);
            Assert.Contains(NnrpTransportProviderLimitation.LocalHostOnly, windows.Descriptor.Metadata.Limitations);
            Assert.Contains(NnrpTransportProviderLimitation.NativeHostOnly, windows.Descriptor.Metadata.Limitations);
            Assert.Contains(NnrpTransportProviderLimitation.WindowsNamedPipe, windows.Descriptor.Metadata.Limitations);
            Assert.DoesNotContain(NnrpTransportProviderLimitation.UnixDomainSocket, windows.Descriptor.Metadata.Limitations);
            Assert.Contains(NnrpTransportProviderLimitation.UnixDomainSocket, unix.Descriptor.Metadata.Limitations);
            Assert.DoesNotContain(NnrpTransportProviderLimitation.WindowsNamedPipe, unix.Descriptor.Metadata.Limitations);
        }

        [LiveNativeTransportFact]
        public async Task NativeIpcProviderOpensRealListenerAndConnection()
        {
            var artifactPath = Environment.GetEnvironmentVariable(
                LiveNativeTransportFactAttribute.ArtifactPathVariableName);
            Assert.False(string.IsNullOrWhiteSpace(artifactPath));
            Assert.True(File.Exists(artifactPath));

            var socketPath = Path.Combine(Path.GetTempPath(), $"nnrp-cs-{Guid.NewGuid():N}.sock");
            var providerEndpoint = OperatingSystem.IsWindows()
                ? NnrpProviderEndpoint.Parse($"npipe://nnrp-cs-{Guid.NewGuid():N}")
                : NnrpProviderEndpoint.Parse($"unix://{socketPath.Replace('\\', '/')}");
            var provider = new NnrpNativeIpcTransportProvider(artifactPath);
            var endpoint = NnrpEndpoint.Parse("nnrp://localhost");

            try
            {
                await using var listener = await provider.ListenAsync(
                    new NnrpTransportListenOptions(endpoint, providerEndpoint));
                Assert.Equal(TransportId.Ipc, listener.TransportId);
                Assert.True(listener.BoundEndpoint.MatchesTransport(TransportId.Ipc));
                if (OperatingSystem.IsWindows())
                {
                    Assert.EndsWith(
                        providerEndpoint.ToString().Substring("npipe://".Length),
                        listener.BoundEndpoint.ToString(),
                        StringComparison.Ordinal);
                }
                else
                {
                    Assert.Equal(providerEndpoint, listener.BoundEndpoint);
                }

                using var server = NnrpNativeRuntimeServer.Bind(
                    listener,
                    new NnrpNativeRuntimeServerHostOptions(50, 1));
                using var acceptCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var acceptTask = Task.Factory.StartNew(
                    () => server.AcceptSession(71, 1, timeoutMilliseconds: 10_000),
                    acceptCancellation.Token,
                    TaskCreationOptions.LongRunning,
                    TaskScheduler.Default);

                await using var connection = await provider.ConnectAsync(
                    new NnrpTransportConnectOptions(
                        endpoint,
                        listener.BoundEndpoint,
                        timeoutMilliseconds: 5_000));
                Assert.Equal(TransportId.Ipc, connection.TransportId);

                using var client = NnrpNativeRuntimeConnectionHost.Open(
                    connection,
                    new NnrpNativeRuntimeConnectionHostOptions(60, 1));
                var clientSession = client.OpenSession(
                    new NnrpNativeRuntimeSessionOptions(70, 1, 2, 0x1001, 3));
                var serverSession = await acceptTask;

                Assert.Equal(TransportId.Ipc, serverSession.ActiveTransportId);
                var clientClose = Task.Factory.StartNew(
                    () => Assert.True(client.CloseSession(70)),
                    CancellationToken.None,
                    TaskCreationOptions.LongRunning,
                    TaskScheduler.Default);
                var closeEvent = Assert.Single(serverSession.AwaitEvents(timeoutMilliseconds: 5_000));
                Assert.Equal((uint)MessageType.SessionClose, closeEvent.MessageType);
                serverSession.Close();
                await clientClose;
            }
            finally
            {
                if (!OperatingSystem.IsWindows() && File.Exists(socketPath))
                {
                    File.Delete(socketPath);
                }
            }
        }
    }
}
