using System;
using System.Collections.Generic;
using Nnrp.Core;
using Xunit;

namespace Nnrp.Core.Tests
{
    public sealed class TransportRouteContractTests
    {
        [Fact]
        public void ApplicationEndpointPreservesFrozenComponents()
        {
            var endpoint = NnrpEndpoint.Parse("nnrps://runtime.example:4433/session/default?model=1");

            Assert.Equal("runtime.example:4433", endpoint.Authority);
            Assert.Equal("/session/default?model=1", endpoint.PathAndQuery);
            Assert.True(endpoint.IsSecure);
            Assert.Equal("nnrps://runtime.example:4433/session/default?model=1", endpoint.ToString());
            Assert.Equal(endpoint, NnrpEndpoint.Parse(endpoint.ToString()));
            Assert.Equal(endpoint.GetHashCode(), NnrpEndpoint.Parse(endpoint.ToString()).GetHashCode());
            Assert.False(endpoint.Equals(null));
            Assert.False(endpoint.Equals(new object()));
        }

        [Theory]
        [InlineData("")]
        [InlineData("runtime.example")]
        [InlineData("tcp://runtime.example:4433")]
        [InlineData("nnrp:///session")]
        [InlineData("nnrp://user@runtime.example/session")]
        [InlineData("nnrp://runtime.example/session#fragment")]
        public void ApplicationEndpointRejectsNonApplicationUris(string value)
        {
            Assert.Throws<FormatException>(() => NnrpEndpoint.Parse(value));
        }

        [Fact]
        public void ApplicationEndpointDefaultsPathAndTracksPlainScheme()
        {
            var endpoint = NnrpEndpoint.Parse("NNRP://runtime.example");

            Assert.Equal("/", endpoint.PathAndQuery);
            Assert.False(endpoint.IsSecure);
        }

        [Theory]
        [InlineData("tcp://127.0.0.1:7443", TransportId.Tcp, false)]
        [InlineData("quic://runtime.example:4433", TransportId.Quic, false)]
        [InlineData("unix:///tmp/nnrp.sock", TransportId.Ipc, false)]
        [InlineData("npipe://nnrp-runtime", TransportId.Ipc, false)]
        [InlineData("ws://runtime.example/nnrp", TransportId.WebSocket, false)]
        [InlineData("wss://runtime.example/nnrp", TransportId.WebSocket, true)]
        public void ProviderEndpointPreservesCarrierLocator(
            string value,
            TransportId transportId,
            bool secure)
        {
            var endpoint = NnrpProviderEndpoint.Parse(value);

            Assert.True(endpoint.MatchesTransport(transportId));
            Assert.Equal(secure, endpoint.IsSecure);
            Assert.Equal(value, endpoint.ToString());
            Assert.Equal(endpoint, NnrpProviderEndpoint.Parse(value));
            Assert.Equal(endpoint.GetHashCode(), NnrpProviderEndpoint.Parse(value).GetHashCode());
            Assert.False(endpoint.MatchesTransport(TransportId.Unspecified));
            Assert.False(endpoint.Equals(null));
            Assert.False(endpoint.Equals(new object()));
        }

        [Theory]
        [InlineData("")]
        [InlineData("udp://runtime.example:4433")]
        [InlineData("unix://")]
        [InlineData("unix:///")]
        [InlineData("unix:///tmp/nnrp.sock?x=1")]
        [InlineData("npipe://user@pipe")]
        [InlineData("tcp://user@runtime.example:4433")]
        [InlineData("ws:///nnrp")]
        [InlineData("wss://runtime.example/nnrp#fragment")]
        public void ProviderEndpointRejectsMalformedLocators(string value)
        {
            Assert.Throws<FormatException>(() => NnrpProviderEndpoint.Parse(value));
        }

        [Fact]
        public void SecurityValuesOwnCertificateBuffers()
        {
            var trusted = new byte[] { 1, 2 };
            var certificate = new byte[] { 3, 4 };
            var privateKey = new byte[] { 5, 6 };
            var client = new NnrpTransportClientSecurity("runtime.example", trusted);
            var server = new NnrpTransportServerSecurity(certificate, privateKey);

            trusted[0] = 9;
            certificate[0] = 9;
            privateKey[0] = 9;

            Assert.Equal("runtime.example", client.ServerName);
            Assert.Equal(new byte[] { 1, 2 }, client.TrustedCertificateDer.ToArray());
            Assert.Equal(new byte[] { 3, 4 }, server.CertificateDer.ToArray());
            Assert.Equal(new byte[] { 5, 6 }, server.PrivateKeyPkcs8Der.ToArray());
        }

        [Fact]
        public void SecurityValuesRejectMissingFields()
        {
            Assert.Throws<ArgumentException>(() => new NnrpTransportClientSecurity("", new byte[] { 1 }));
            Assert.Throws<ArgumentException>(() => new NnrpTransportClientSecurity("runtime.example", Array.Empty<byte>()));
            Assert.Throws<ArgumentException>(() => new NnrpTransportServerSecurity(Array.Empty<byte>(), new byte[] { 1 }));
            Assert.Throws<ArgumentException>(() => new NnrpTransportServerSecurity(new byte[] { 1 }, Array.Empty<byte>()));
        }

        [Fact]
        public void ProviderRoutesKeepRoleSpecificSecurity()
        {
            var endpoint = NnrpProviderEndpoint.Parse("wss://runtime.example/nnrp");
            var clientSecurity = new NnrpTransportClientSecurity("runtime.example", new byte[] { 1 });
            var serverSecurity = new NnrpTransportServerSecurity(new byte[] { 2 }, new byte[] { 3 });
            var client = new NnrpClientProviderRoute
            {
                ProviderEndpoint = endpoint,
                Security = clientSecurity,
            };
            var server = new NnrpServerProviderRoute
            {
                ProviderEndpoint = endpoint,
                Security = serverSecurity,
            };

            Assert.Same(endpoint, client.ProviderEndpoint);
            Assert.Same(clientSecurity, client.Security);
            Assert.Same(endpoint, server.ProviderEndpoint);
            Assert.Same(serverSecurity, server.Security);
        }

        [Fact]
        public void RouteSetsOwnStableRoleSpecificSnapshots()
        {
            var clientRoute = new NnrpClientProviderRoute
            {
                ProviderEndpoint = NnrpProviderEndpoint.Parse("tcp://127.0.0.1:7001"),
            };
            var serverRoute = new NnrpServerProviderRoute
            {
                ProviderEndpoint = NnrpProviderEndpoint.Parse("tcp://127.0.0.1:7002"),
            };
            var clientSource = new Dictionary<TransportId, NnrpClientProviderRoute>
            {
                [TransportId.Tcp] = clientRoute,
            };
            var serverSource = new Dictionary<TransportId, NnrpServerProviderRoute>
            {
                [TransportId.Tcp] = serverRoute,
            };

            var clients = NnrpTransportRouteSet.CopyClient(clientSource);
            var servers = NnrpTransportRouteSet.CopyServer(serverSource);
            clientSource.Clear();
            serverSource.Clear();

            Assert.Equal("tcp://127.0.0.1:7001", clients[TransportId.Tcp].ProviderEndpoint!.ToString());
            Assert.Equal("tcp://127.0.0.1:7002", servers[TransportId.Tcp].ProviderEndpoint!.ToString());
            Assert.Throws<NotSupportedException>(() =>
                ((IDictionary<TransportId, NnrpClientProviderRoute>)clients).Clear());
        }

        [Fact]
        public void RouteSetsRejectUnknownAndNullEntries()
        {
            Assert.Throws<ArgumentException>(() => NnrpTransportRouteSet.CopyClient(
                new Dictionary<TransportId, NnrpClientProviderRoute>
                {
                    [TransportId.Unspecified] = new NnrpClientProviderRoute(),
                }));
            Assert.Throws<ArgumentException>(() => NnrpTransportRouteSet.CopyClient(
                new Dictionary<TransportId, NnrpClientProviderRoute>
                {
                    [(TransportId)uint.MaxValue] = new NnrpClientProviderRoute(),
                }));
            Assert.Throws<ArgumentException>(() => NnrpTransportRouteSet.CopyServer(
                new Dictionary<TransportId, NnrpServerProviderRoute>
                {
                    [TransportId.Tcp] = null!,
                }));
        }

        [Theory]
        [InlineData(TransportId.Tcp, "tcp://runtime.example:7443")]
        [InlineData(TransportId.Quic, "quic://runtime.example:7443")]
        public void NetworkRoutesDeriveProviderEndpointFromApplicationAuthority(
            TransportId transportId,
            string expected)
        {
            Assert.True(NnrpTransportRouteResolver.TryResolveClient(
                NnrpEndpoint.Parse("nnrp://runtime.example:7443/runtime/default"),
                transportId,
                transportId == TransportId.Quic
                    ? new NnrpClientProviderRoute
                    {
                        Security = new NnrpTransportClientSecurity("runtime.example", new byte[] { 1 }),
                    }
                    : null,
                out var endpoint,
                out var rejection,
                out var diagnostic));
            Assert.Equal(expected, endpoint!.ToString());
            Assert.Null(rejection);
            Assert.Null(diagnostic);
        }

        [Fact]
        public void MissingExplicitRoutesAreRouteUnresolvedBeforeSecurityChecks()
        {
            Assert.False(NnrpTransportRouteResolver.TryResolveClient(
                NnrpEndpoint.Parse("nnrps://runtime.example/runtime/default"),
                TransportId.WebSocket,
                null,
                out var endpoint,
                out var rejection,
                out _));

            Assert.Null(endpoint);
            Assert.Equal(NnrpTransportRejectionReason.RouteUnresolved, rejection);
        }

        [Fact]
        public void ServerMissingExplicitRouteIsUnresolved()
        {
            Assert.False(NnrpTransportRouteResolver.TryResolveServer(
                NnrpEndpoint.Parse("nnrp://runtime.example/runtime/default"),
                TransportId.WebSocket,
                null,
                out var endpoint,
                out var rejection,
                out _));

            Assert.Null(endpoint);
            Assert.Equal(NnrpTransportRejectionReason.RouteUnresolved, rejection);
        }

        [Fact]
        public void RouteResolutionRejectsInvalidInputsAndMismatchedLocators()
        {
            Assert.Throws<ArgumentNullException>(() => NnrpTransportRouteResolver.TryResolveClient(
                null!,
                TransportId.Tcp,
                null,
                out _,
                out _,
                out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => NnrpTransportRouteResolver.TryResolveClient(
                NnrpEndpoint.Parse("nnrp://runtime.example/runtime/default"),
                TransportId.Unspecified,
                null,
                out _,
                out _,
                out _));

            Assert.False(NnrpTransportRouteResolver.TryResolveClient(
                NnrpEndpoint.Parse("nnrp://runtime.example/runtime/default"),
                TransportId.Tcp,
                new NnrpClientProviderRoute
                {
                    ProviderEndpoint = NnrpProviderEndpoint.Parse("quic://runtime.example:7443"),
                },
                out _,
                out var rejection,
                out var diagnostic));
            Assert.Equal(NnrpTransportRejectionReason.RouteUnresolved, rejection);
            Assert.Contains("does not match", diagnostic);
        }

        [Fact]
        public void RouteResolutionRejectsPlatformIncompatibleIpcLocator()
        {
            var incompatible = OperatingSystem.IsWindows()
                ? NnrpProviderEndpoint.Parse("unix:///tmp/nnrp-test.sock")
                : NnrpProviderEndpoint.Parse("npipe://nnrp-test");

            Assert.False(NnrpTransportRouteResolver.TryResolveServer(
                NnrpEndpoint.Parse("nnrp://runtime.example/runtime/default"),
                TransportId.Ipc,
                new NnrpServerProviderRoute { ProviderEndpoint = incompatible },
                out _,
                out var rejection,
                out var diagnostic));
            Assert.Equal(NnrpTransportRejectionReason.RouteUnresolved, rejection);
            Assert.Contains("not supported", diagnostic);
        }

        [Fact]
        public void SecurityMatrixRejectsUnknownTransportDefensively()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => NnrpTransportRouteResolver.IsSecurityValid(
                NnrpEndpoint.Parse("nnrp://runtime.example/runtime/default"),
                (TransportId)uint.MaxValue,
                NnrpProviderEndpoint.Parse("tcp://runtime.example:7443"),
                false));
        }

        [Fact]
        public void SecureIntentUsesTheFrozenPerCarrierMatrix()
        {
            var secure = NnrpEndpoint.Parse("nnrps://runtime.example:7443/runtime/default");
            var plain = NnrpEndpoint.Parse("nnrp://runtime.example:7443/runtime/default");
            var clientSecurity = new NnrpTransportClientSecurity("runtime.example", new byte[] { 1 });
            var serverSecurity = new NnrpTransportServerSecurity(new byte[] { 2 }, new byte[] { 3 });

            Assert.False(NnrpTransportRouteResolver.TryResolveClient(
                secure,
                TransportId.Tcp,
                null,
                out _,
                out var tcpRejection,
                out _));
            Assert.Equal(NnrpTransportRejectionReason.SecurityUnsatisfied, tcpRejection);
            Assert.True(NnrpTransportRouteResolver.TryResolveClient(
                secure,
                TransportId.Tcp,
                new NnrpClientProviderRoute { Security = clientSecurity },
                out _,
                out _,
                out _));
            Assert.False(NnrpTransportRouteResolver.TryResolveServer(
                secure,
                TransportId.Ipc,
                new NnrpServerProviderRoute
                {
                    ProviderEndpoint = PlatformIpcEndpoint(),
                },
                out _,
                out var ipcRejection,
                out _));
            Assert.Equal(NnrpTransportRejectionReason.SecurityUnsatisfied, ipcRejection);
            Assert.True(NnrpTransportRouteResolver.TryResolveServer(
                plain,
                TransportId.WebSocket,
                new NnrpServerProviderRoute
                {
                    ProviderEndpoint = NnrpProviderEndpoint.Parse("wss://runtime.example/nnrp"),
                    Security = serverSecurity,
                },
                out _,
                out _,
                out _));
            Assert.False(NnrpTransportRouteResolver.TryResolveServer(
                plain,
                TransportId.WebSocket,
                new NnrpServerProviderRoute
                {
                    ProviderEndpoint = NnrpProviderEndpoint.Parse("ws://runtime.example/nnrp"),
                    Security = serverSecurity,
                },
                out _,
                out var wsRejection,
                out _));
            Assert.Equal(NnrpTransportRejectionReason.SecurityUnsatisfied, wsRejection);
        }

        private static NnrpProviderEndpoint PlatformIpcEndpoint()
        {
            return OperatingSystem.IsWindows()
                ? NnrpProviderEndpoint.Parse("npipe://nnrp-test")
                : NnrpProviderEndpoint.Parse("unix:///tmp/nnrp-test.sock");
        }
    }
}
