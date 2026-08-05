using System;
using System.IO;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Nnrp.Core;
using Nnrp.NativeBridge;
using Xunit;

namespace Nnrp.Transport.WebSocket.Tests
{
    public sealed class NnrpNativeWebSocketTransportProviderTests
    {
        [Fact]
        public void NativeWebSocketProviderOwnsFrozenRuntimeBinding()
        {
            var provider = NnrpNativeWebSocketTransportProvider.Instance;

            Assert.Equal(TransportId.WebSocket, provider.Descriptor.TransportId);
            Assert.Equal("websocket", provider.Descriptor.Name);
            Assert.Equal("1.0.0-preview.4", provider.Descriptor.Version);
            Assert.Equal(NnrpTransportProviderKind.NativeDynamic, provider.Descriptor.Kind);
            Assert.True(provider.Descriptor.Available);
            Assert.Equal("nnrp.transport.websocket.native", provider.Descriptor.Metadata.Id);
            Assert.Equal(0UL, provider.Descriptor.Metadata.Cost.ModelId);
            Assert.Equal(0UL, provider.Descriptor.Metadata.Cost.Units);
            Assert.Equal((ushort)3, provider.Descriptor.Metadata.PreferenceRank);
            Assert.Equal(67_108_864UL, provider.Descriptor.Metadata.Limits.MaxFrameBytes);
            Assert.Equal(
                new[]
                {
                    NnrpTransportProviderLimitation.RequiresTcp,
                    NnrpTransportProviderLimitation.NativeHostOnly,
                },
                provider.Descriptor.Metadata.Limitations);
        }

        [Fact]
        public async Task NativeWebSocketProviderRejectsMismatchedRoutesAndSecurity()
        {
            var provider = new NnrpNativeWebSocketTransportProvider(artifactPath: "unused");

            await Assert.ThrowsAsync<ArgumentException>(async () =>
                await provider.ConnectAsync(new NnrpTransportConnectOptions(
                    NnrpEndpoint.Parse("nnrp://localhost"),
                    NnrpProviderEndpoint.Parse("tcp://localhost:1"))));
            await Assert.ThrowsAsync<ArgumentException>(async () =>
                await provider.ConnectAsync(new NnrpTransportConnectOptions(
                    NnrpEndpoint.Parse("nnrps://localhost"),
                    NnrpProviderEndpoint.Parse("ws://localhost:1"))));
            await Assert.ThrowsAsync<ArgumentException>(async () =>
                await provider.ConnectAsync(new NnrpTransportConnectOptions(
                    NnrpEndpoint.Parse("nnrps://localhost"),
                    NnrpProviderEndpoint.Parse("wss://localhost:1"))));
        }

        [LiveNativeTransportFact]
        public async Task NativeWebSocketProviderCarriesWsSessionLifecycle()
        {
            await RunSessionLifecycleAsync(secure: false);
        }

        [LiveNativeTransportFact]
        public async Task NativeWebSocketProviderCarriesWssSessionLifecycle()
        {
            await RunSessionLifecycleAsync(secure: true);
        }

        [LiveNativeTransportFact]
        public async Task NativeWebSocketProviderRejectsTextMessagesBeforeSessionAdoption()
        {
            var artifactPath = Environment.GetEnvironmentVariable(
                LiveNativeTransportFactAttribute.ArtifactPathVariableName);
            Assert.False(string.IsNullOrWhiteSpace(artifactPath));
            Assert.True(File.Exists(artifactPath));

            var provider = new NnrpNativeWebSocketTransportProvider(artifactPath);
            var endpoint = NnrpEndpoint.Parse("nnrp://localhost:0");
            await using var listener = await provider.ListenAsync(
                new NnrpTransportListenOptions(
                    endpoint,
                    NnrpProviderEndpoint.Parse("ws://127.0.0.1:0/nnrp")));
            using var server = NnrpNativeRuntimeServer.Bind(
                listener,
                new NnrpNativeRuntimeServerHostOptions(50, 1));
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var acceptTask = Task.Factory.StartNew(
                () => server.AcceptSession(71, 1, timeoutMilliseconds: 5_000),
                timeout.Token,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);

            using var client = new ClientWebSocket();
            await client.ConnectAsync(new Uri(listener.BoundEndpoint.ToString()), timeout.Token);
            await client.SendAsync(
                new ArraySegment<byte>(new byte[] { (byte)'n', (byte)'o', (byte)'t', (byte)'-', (byte)'n', (byte)'n', (byte)'r', (byte)'p' }),
                WebSocketMessageType.Text,
                endOfMessage: true,
                timeout.Token);

            await Assert.ThrowsAnyAsync<NnrpNativeRuntimeException>(async () => await acceptTask);
        }

        private static async Task RunSessionLifecycleAsync(bool secure)
        {
            var artifactPath = Environment.GetEnvironmentVariable(
                LiveNativeTransportFactAttribute.ArtifactPathVariableName);
            Assert.False(string.IsNullOrWhiteSpace(artifactPath));
            Assert.True(File.Exists(artifactPath));

            using var certificate = secure ? CreateCertificate() : null;
            var certificateDer = certificate?.Export(X509ContentType.Cert);
            var privateKeyPkcs8Der = certificate?.GetRSAPrivateKey()?.ExportPkcs8PrivateKey();
            var provider = new NnrpNativeWebSocketTransportProvider(artifactPath);
            var endpoint = NnrpEndpoint.Parse(secure ? "nnrps://localhost:0" : "nnrp://localhost:0");
            var providerScheme = secure ? "wss" : "ws";
            var serverSecurity = secure
                ? new NnrpTransportServerSecurity(certificateDer!, privateKeyPkcs8Der!)
                : null;
            var clientSecurity = secure
                ? new NnrpTransportClientSecurity("localhost", certificateDer!)
                : null;

            await using var listener = await provider.ListenAsync(
                new NnrpTransportListenOptions(
                    endpoint,
                    NnrpProviderEndpoint.Parse($"{providerScheme}://127.0.0.1:0/nnrp"),
                    serverSecurity));
            Assert.Equal(TransportId.WebSocket, listener.TransportId);
            Assert.Equal(providerScheme, new Uri(listener.BoundEndpoint.ToString()).Scheme);
            var boundUri = new Uri(listener.BoundEndpoint.ToString());
            Assert.NotEqual(0, boundUri.Port);
            var connectEndpoint = secure
                ? NnrpProviderEndpoint.Parse($"wss://localhost:{boundUri.Port}{boundUri.PathAndQuery}")
                : listener.BoundEndpoint;

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
                    connectEndpoint,
                    clientSecurity,
                    timeoutMilliseconds: 5_000));
            using var client = NnrpNativeRuntimeConnectionHost.Open(
                connection,
                new NnrpNativeRuntimeConnectionHostOptions(60, 1));
            var clientSession = client.OpenSession(
                new NnrpNativeRuntimeSessionOptions(70, 1, 2, 0x1001, 3));
            var serverSession = await acceptTask;

            Assert.Equal(TransportId.WebSocket, serverSession.ActiveTransportId);
            var clientClose = Task.Factory.StartNew(
                clientSession.Close,
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
            var closeEvent = Assert.Single(serverSession.AwaitEvents(timeoutMilliseconds: 5_000));
            Assert.Equal((uint)MessageType.SessionClose, closeEvent.MessageType);
            serverSession.Close();
            await clientClose;
        }

        private static X509Certificate2 CreateCertificate()
        {
            using var rsa = RSA.Create(2048);
            var request = new CertificateRequest(
                "CN=localhost",
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
            request.CertificateExtensions.Add(
                new X509BasicConstraintsExtension(false, false, 0, true));
            request.CertificateExtensions.Add(
                new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
            var subjectAlternativeName = new SubjectAlternativeNameBuilder();
            subjectAlternativeName.AddDnsName("localhost");
            request.CertificateExtensions.Add(subjectAlternativeName.Build());
            return request.CreateSelfSigned(
                DateTimeOffset.UtcNow.AddMinutes(-1),
                DateTimeOffset.UtcNow.AddMinutes(10));
        }
    }
}
