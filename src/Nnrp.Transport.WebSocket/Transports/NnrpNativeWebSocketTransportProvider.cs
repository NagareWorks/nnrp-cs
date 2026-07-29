using Nnrp.Core;
using Nnrp.NativeBridge;

namespace Nnrp.Transport.WebSocket
{
    public sealed class NnrpNativeWebSocketTransportProvider : NnrpNativeTransportProvider
    {
        public static NnrpNativeWebSocketTransportProvider Instance { get; } =
            new NnrpNativeWebSocketTransportProvider();

        public NnrpNativeWebSocketTransportProvider(
            string? artifactPath = null,
            string? artifactRoot = null,
            NnrpNativePlatform? platform = null)
            : base(
                NnrpNativeWebSocketRuntime.CreateDescriptor(artifactPath),
                "websocket",
                NnrpNativeArtifact.TransportSlotWebSocket,
                artifactPath,
                artifactRoot,
                platform)
        {
        }
    }

    internal static class NnrpNativeWebSocketRuntime
    {
        internal static NnrpTransportProviderDescriptor CreateDescriptor(string? artifactPath)
        {
            return new NnrpTransportProviderDescriptor(
                "websocket",
                "1.0.0-preview.4",
                TransportId.WebSocket,
                NnrpTransportProviderKind.NativeDynamic,
                true,
                artifactPath,
                new NnrpTransportProviderMetadata(
                    "nnrp.transport.websocket.native",
                    new NnrpTransportProviderCost(0, 0),
                    3,
                    new NnrpTransportProviderLimits(67_108_864),
                    new[]
                    {
                        NnrpTransportProviderLimitation.RequiresTcp,
                        NnrpTransportProviderLimitation.NativeHostOnly,
                    }));
        }
    }
}
