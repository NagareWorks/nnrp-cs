using System.Threading.Tasks;
using Nnrp.Core;
using Nnrp.NativeBridge;
using Nnrp.Runtime;
using Nnrp.Transport.Tcp;
using Xunit;

namespace Nnrp.NativeBridge.Tests
{
    public sealed class LiveNativeRuntimeObjectSmokeTests
    {
        [LiveNativeArtifactFact]
        public void RuntimeObjectDescriptorsPassAgainstConfiguredNativeArtifact()
        {
            Assert.True(
                LiveNativeArtifactFactAttribute.TryResolveArtifact(out var artifactPath, out var reason),
                reason);

            using var entrypoints = NnrpNativeRuntimeEntrypoints.Load(artifactPath);
            var objects = new NnrpNativeRuntimeObjects(entrypoints);
            var objectMetadata = new byte[] { 1, 2, 3 };
            var objectDescriptor = new ObjectDescriptorMetadata(
                301,
                RuntimeObjectKind.Tensor,
                RuntimeRole.Runtime,
                RuntimeRole.Client,
                201,
                4096,
                17,
                MemoryLocationHint.DeviceMemory,
                OwnershipHint.Borrowed,
                500,
                (uint)objectMetadata.Length);

            using (var descriptor = objects.CreateObjectDescriptor(objectDescriptor, objectMetadata))
            {
                var snapshot = descriptor.Snapshot();
                Assert.Equal(objectDescriptor, snapshot.Descriptor);
                Assert.Equal(objectMetadata, snapshot.Metadata.ToArray());
            }

            var cacheMetadata = new byte[] { 4, 5 };
            var cacheDescriptor = new CacheReferenceMetadata(
                401,
                402,
                403,
                SchemaDescriptorHeader.ProfileToken,
                CacheReuseScope.Session,
                404,
                405,
                1000,
                (uint)cacheMetadata.Length,
                1);

            using (var descriptor = objects.CreateCacheReference(cacheDescriptor, cacheMetadata))
            {
                var snapshot = descriptor.Snapshot();
                Assert.Equal(cacheDescriptor, snapshot.Descriptor);
                Assert.Equal(cacheMetadata, snapshot.Metadata.ToArray());
            }
        }

        [LiveNativeArtifactFact]
        public async Task TransportRuntimeShutdownInvalidatesLiveHandlesAndRestarts()
        {
            Assert.True(
                LiveNativeArtifactFactAttribute.TryResolveArtifact(out var artifactPath, out var reason),
                reason);

            var provider = new NnrpNativeTcpTransportProvider(artifactPath);
            var endpoint = NnrpEndpoint.Parse("nnrp://127.0.0.1:0");
            var listener = await provider.ListenAsync(
                new NnrpTransportListenOptions(
                    endpoint,
                    NnrpProviderEndpoint.Parse("tcp://127.0.0.1:0")));

            NnrpNativeRuntimeEntrypoints.ShutdownPinnedTransportRuntimesForTesting();
            Assert.Throws<NnrpNativeInvalidHandleException>(listener.Dispose);

            await using var restarted = await provider.ListenAsync(
                new NnrpTransportListenOptions(
                    endpoint,
                    NnrpProviderEndpoint.Parse("tcp://127.0.0.1:0")));
            Assert.NotEqual(0, new System.Uri(restarted.BoundEndpoint.ToString()).Port);
        }
    }
}
