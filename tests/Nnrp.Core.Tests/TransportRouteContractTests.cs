using System;
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
    }
}
