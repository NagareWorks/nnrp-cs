using System.Linq;
using Nnrp.Core;
using Xunit;

namespace Nnrp.NativeBridge.Tests
{
    public sealed class NnrpNativeTransportDefaultsTests
    {
        [Fact]
        public void DiscoverLoadsInstalledFirstPartyProviderPackages()
        {
            var providers = NnrpNativeTransportDefaults.Discover();

            Assert.Equal(
                new[] { TransportId.Quic, TransportId.Tcp, TransportId.Ipc, TransportId.WebSocket },
                providers.Select(value => value.Descriptor.TransportId).OrderBy(value => (uint)value));
            Assert.All(providers, provider => Assert.True(provider.Descriptor.Available));
        }
    }
}
