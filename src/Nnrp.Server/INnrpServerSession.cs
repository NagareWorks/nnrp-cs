using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Nnrp.Core;

namespace Nnrp.Server
{
    public interface INnrpServerSession
    {
        ServerProfile Profile { get; }

        NnrpSessionState State { get; }

        uint SessionId { get; }

        NnrpCapabilitySelection NegotiatedCapabilities { get; }

        NnrpCapabilityNegotiationResult LastNegotiationResult { get; }

        NnrpProtocolFailure LastFailure { get; }

        ValueTask<NnrpProtocolFailure> AcceptAsync(CancellationToken cancellationToken);

        ValueTask<NnrpFrameSubmit> ReceiveSubmitAsync(CancellationToken cancellationToken);

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        ValueTask<FrameSubmitMessage> ReceiveFrameSubmitAsync(CancellationToken cancellationToken);

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        ValueTask<SessionMigrateMessage> ReceiveSessionMigrateAsync(CancellationToken cancellationToken);

        ValueTask SendResultAsync(NnrpResult result, CancellationToken cancellationToken);

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        ValueTask SendResultAsync(ResultPushMessage resultMessage, CancellationToken cancellationToken);

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        ValueTask SendSessionMigrateAckAsync(SessionMigrateAckMessage ackMessage, CancellationToken cancellationToken);

        ValueTask SendResultDropAsync(ResultDropMessage dropMessage, CancellationToken cancellationToken);

        ValueTask<NnrpProtocolFailure> CloseAsync(string reason, ulong traceId, CancellationToken cancellationToken);
    }
}
