using System.Threading;
using System.Threading.Tasks;
using Nnrp.Core;

namespace Nnrp.Client
{
    public interface INnrpClientSession
    {
        ClientProfile Profile { get; }

        NnrpSessionState State { get; }

        NnrpCapabilitySelection NegotiatedCapabilities { get; }

        NnrpCapabilityNegotiationResult LastNegotiationResult { get; }

        NnrpProtocolFailure LastFailure { get; }

        ValueTask<NnrpClientConnectResult> ConnectAsync(
            NnrpServerCapabilities serverCapabilities,
            CancellationToken cancellationToken);

        ValueTask<NnrpProtocolFailure> CloseAsync(CancellationToken cancellationToken);

        bool TryAcceptFrameSubmit(out NnrpProtocolFailure failure);
    }
}
