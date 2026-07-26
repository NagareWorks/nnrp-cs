using Nnrp.Core;
using Nnrp.NativeBridge;

namespace Nnrp.Transport.Quic
{
    public sealed class NnrpNativeQuicTransportProvider : NnrpNativeTransportProvider
    {
        public static NnrpNativeQuicTransportProvider Instance { get; } =
            new NnrpNativeQuicTransportProvider();

        public NnrpNativeQuicTransportProvider(
            string? artifactPath = null,
            string? artifactRoot = null,
            NnrpNativePlatform? platform = null)
            : base(
                new NnrpTransportProviderDescriptor(
                    "quic",
                    "1.0.0-preview.4",
                    TransportId.Quic,
                    NnrpTransportProviderKind.NativeDynamic,
                    true,
                    artifactPath,
                    new NnrpTransportProviderMetadata(
                        "nnrp.transport.quic.native",
                        new NnrpTransportProviderCost(0, 0),
                        1,
                        new NnrpTransportProviderLimits(67_108_864),
                        new[]
                        {
                            NnrpTransportProviderLimitation.RequiresUdp,
                            NnrpTransportProviderLimitation.NativeHostOnly,
                        })),
                "quic",
                NnrpNativeArtifact.TransportSlotQuic,
                artifactPath,
                artifactRoot,
                platform)
        {
        }
    }
}
