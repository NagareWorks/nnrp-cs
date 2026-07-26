using Nnrp.Core;
using Nnrp.NativeBridge;

namespace Nnrp.NativeBridge.Tests
{
    internal static class NnrpNativeRuntimeTestExtensions
    {
        internal static NnrpNativeRuntimeConnection Connect(
            this NnrpNativeRuntimeClient client,
            ulong connectionId,
            uint generation,
            uint transportId)
        {
            var transport = new NnrpTransportConnection(
                new NnrpNativeEntrypointLease(client.Entrypoints),
                ToTransportId(transportId),
                new NnrpHandle(NnrpHandleKind.TransportConnection, connectionId, generation));
            return client.Connect(connectionId, generation, transport);
        }

        private static TransportId ToTransportId(uint transportId)
        {
            return transportId switch
            {
                NnrpNativeArtifact.TransportSlotTcp => TransportId.Tcp,
                NnrpNativeArtifact.TransportSlotQuic => TransportId.Quic,
                NnrpNativeArtifact.TransportSlotIpc => TransportId.Ipc,
                NnrpNativeArtifact.TransportSlotWebSocket => TransportId.WebSocket,
                _ => throw new System.ArgumentOutOfRangeException(nameof(transportId)),
            };
        }
    }
}
