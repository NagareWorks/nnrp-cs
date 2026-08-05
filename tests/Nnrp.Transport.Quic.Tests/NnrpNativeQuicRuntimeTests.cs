using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Nnrp.Core;
using Nnrp.NativeBridge;
using Nnrp.Transport.Quic;
using Xunit;

namespace Nnrp.Transport.Quic.Tests
{
    public sealed class NnrpNativeQuicTransportProviderTests
    {
        [Fact]
        public void NativeQuicProviderOwnsQuicRuntimeBinding()
        {
            var provider = NnrpNativeQuicTransportProvider.Instance;

            Assert.Equal(TransportId.Quic, provider.Descriptor.TransportId);
            Assert.Equal("quic", provider.Descriptor.Name);
            Assert.Equal("nnrp.transport.quic.native", provider.Descriptor.Metadata.Id);
            Assert.Equal((ushort)1, provider.Descriptor.Metadata.PreferenceRank);
            Assert.Contains(
                NnrpTransportProviderLimitation.RequiresUdp,
                provider.Descriptor.Metadata.Limitations);
        }

        [LiveNativeTransportFact]
        public async Task NativeQuicProviderOpensRealSecureListenerAndConnection()
        {
            var artifactPath = Environment.GetEnvironmentVariable(
                LiveNativeTransportFactAttribute.ArtifactPathVariableName);
            Assert.False(string.IsNullOrWhiteSpace(artifactPath));
            Assert.True(File.Exists(artifactPath));

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
            using var certificate = request.CreateSelfSigned(
                DateTimeOffset.UtcNow.AddMinutes(-1),
                DateTimeOffset.UtcNow.AddMinutes(10));
            var certificateDer = certificate.Export(X509ContentType.Cert);
            var privateKeyPkcs8Der = rsa.ExportPkcs8PrivateKey();

            var provider = new NnrpNativeQuicTransportProvider(artifactPath);
            var endpoint = NnrpEndpoint.Parse("nnrps://localhost:0");
            await using var listener = await provider.ListenAsync(
                new NnrpTransportListenOptions(
                    endpoint,
                    NnrpProviderEndpoint.Parse("quic://127.0.0.1:0"),
                    new NnrpTransportServerSecurity(certificateDer, privateKeyPkcs8Der)));

            Assert.Equal(TransportId.Quic, listener.TransportId);
            Assert.NotEqual(0, new Uri(listener.BoundEndpoint.ToString()).Port);

            using var server = NnrpNativeRuntimeServer.Bind(
                listener,
                new NnrpNativeRuntimeServerHostOptions(50, 1));
            using var acceptCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var acceptStarted = new ManualResetEventSlim();
            var acceptTask = Task.Factory.StartNew(
                () => AcceptSession(server, acceptCancellation.Token, acceptStarted),
                acceptCancellation.Token,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
            try
            {
                Assert.True(acceptStarted.Wait(TimeSpan.FromSeconds(5)));
                await using var connection = await provider.ConnectAsync(
                    new NnrpTransportConnectOptions(
                        endpoint,
                        listener.BoundEndpoint,
                        new NnrpTransportClientSecurity("localhost", certificateDer),
                        timeoutMilliseconds: 5_000));

                Assert.Equal(TransportId.Quic, connection.TransportId);
                var client = NnrpNativeRuntimeConnectionHost.Open(
                    connection,
                    new NnrpNativeRuntimeConnectionHostOptions(60, 1));
                Exception? clientFailure = null;
                try
                {
                    var clientSession = client.OpenSession(
                        new NnrpNativeRuntimeSessionOptions(70, 1, 2, 0x1001, 3));
                    var serverSession = await acceptTask;

                    Assert.Equal((ulong)71, serverSession.Handle.Handle.Id);
                    var clientClose = Task.Factory.StartNew(
                        clientSession.Close,
                        CancellationToken.None,
                        TaskCreationOptions.LongRunning,
                        TaskScheduler.Default);
                    var closeEvent = Assert.Single(
                        serverSession.AwaitEvents(timeoutMilliseconds: 5_000));
                    Assert.Equal((uint)MessageType.SessionClose, closeEvent.MessageType);
                    serverSession.Close();
                    await clientClose;
                    Assert.True(clientSession.IsClosed);
                }
                catch (Exception error)
                {
                    clientFailure = error;
                    throw;
                }
                finally
                {
                    try
                    {
                        client.Dispose();
                    }
                    catch when (clientFailure != null)
                    {
                    }
                }
            }
            finally
            {
                acceptCancellation.Cancel();
                try
                {
                    await acceptTask;
                }
                catch (OperationCanceledException) when (acceptCancellation.IsCancellationRequested)
                {
                }
                catch (NnrpNativeWouldBlockException) when (acceptCancellation.IsCancellationRequested)
                {
                }
            }
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
            Assert.Equal(TransportId.Quic, resolution.SelectedProvider.Descriptor.TransportId);
        }

    }
}
