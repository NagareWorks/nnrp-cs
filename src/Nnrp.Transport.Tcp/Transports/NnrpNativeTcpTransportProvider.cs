using Nnrp.Core;
using Nnrp.NativeBridge;

namespace Nnrp.Transport.Tcp
{
    public sealed class NnrpNativeTcpTransportProvider : NnrpNativeTransportProvider
    {
        public static NnrpNativeTcpTransportProvider Instance { get; } =
            new NnrpNativeTcpTransportProvider();

        public NnrpNativeTcpTransportProvider(
            string? artifactPath = null,
            string? artifactRoot = null,
            NnrpNativePlatform? platform = null)
            : base(
                new NnrpTransportProviderDescriptor(
                    "tcp",
                    "1.0.0-preview.4",
                    TransportId.Tcp,
                    NnrpTransportProviderKind.NativeDynamic,
                    true,
                    artifactPath,
                    new NnrpTransportProviderMetadata(
                        "nnrp.transport.tcp.native",
                        new NnrpTransportProviderCost(0, 0),
                        2,
                        new NnrpTransportProviderLimits(67_108_864),
                        new[]
                        {
                            NnrpTransportProviderLimitation.RequiresTcp,
                            NnrpTransportProviderLimitation.NativeHostOnly,
                        })),
                "tcp",
                NnrpNativeArtifact.TransportSlotTcp,
                artifactPath,
                artifactRoot,
                platform)
        {
        }
    }
}
