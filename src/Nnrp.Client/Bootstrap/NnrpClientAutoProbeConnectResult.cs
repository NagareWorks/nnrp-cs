using System;

namespace Nnrp.Client
{
    public readonly struct NnrpClientAutoProbeConnectResult
    {
        public NnrpClientAutoProbeConnectResult(
            NnrpClient client,
            NnrpClientConnectResult connectResult,
            NnrpTransportProbeSelectionResult? probeSelection,
            bool wasProbed)
        {
            Client = client ?? throw new ArgumentNullException(nameof(client));
            ConnectResult = connectResult;
            ProbeSelection = probeSelection;
            WasProbed = wasProbed;
        }

        public NnrpClient Client { get; }

        public NnrpClientConnectResult ConnectResult { get; }

        public NnrpTransportProbeSelectionResult? ProbeSelection { get; }

        public bool WasProbed { get; }
    }
}
