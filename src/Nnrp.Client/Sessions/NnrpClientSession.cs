using System;
using System.Threading;
using System.Threading.Tasks;
using Nnrp.Core;

namespace Nnrp.Client
{
    public sealed class NnrpClientSession : INnrpClientSession
    {
        private readonly NnrpSessionStateMachine stateMachine = new NnrpSessionStateMachine();

        public NnrpClientSession(ClientProfile profile)
        {
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
        }

        public ClientProfile Profile { get; }

        public NnrpSessionState State => stateMachine.State;

        public NnrpCapabilitySelection NegotiatedCapabilities { get; private set; }

        public NnrpCapabilityNegotiationResult LastNegotiationResult { get; private set; }

        public NnrpProtocolFailure LastFailure => stateMachine.LastFailure;

        public ValueTask<NnrpClientConnectResult> ConnectAsync(
            NnrpServerCapabilities serverCapabilities,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!stateMachine.TryBeginNegotiation(out var failure))
            {
                stateMachine.ApplyFailure(failure);
                return new ValueTask<NnrpClientConnectResult>(NnrpClientConnectResult.Failed(failure));
            }

            var negotiationResult = NnrpCapabilityNegotiator.Negotiate(Profile.ToCapabilities(), serverCapabilities);
            LastNegotiationResult = negotiationResult;
            if (!negotiationResult.IsAccepted)
            {
                var negotiationFailure = ToProtocolFailure(negotiationResult);
                stateMachine.TryFailNegotiation(negotiationFailure, out _);
                return new ValueTask<NnrpClientConnectResult>(
                    NnrpClientConnectResult.Rejected(negotiationResult, negotiationFailure));
            }

            if (!stateMachine.TryActivate(out failure))
            {
                stateMachine.ApplyFailure(failure);
                return new ValueTask<NnrpClientConnectResult>(NnrpClientConnectResult.Failed(failure));
            }

            NegotiatedCapabilities = negotiationResult.Selection;
            return new ValueTask<NnrpClientConnectResult>(NnrpClientConnectResult.Connected(negotiationResult));
        }

        public ValueTask<NnrpProtocolFailure> CloseAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!stateMachine.TryClose(out var failure))
            {
                stateMachine.ApplyFailure(failure);
                return new ValueTask<NnrpProtocolFailure>(failure);
            }

            return new ValueTask<NnrpProtocolFailure>(NnrpProtocolFailure.None);
        }

        public bool TryAcceptFrameSubmit(out NnrpProtocolFailure failure)
        {
            return stateMachine.TryAcceptFrameSubmit(out failure);
        }

        private static NnrpProtocolFailure ToProtocolFailure(NnrpCapabilityNegotiationResult negotiationResult)
        {
            var message = string.IsNullOrEmpty(negotiationResult.RejectionMessage)
                ? $"Capability negotiation rejected: {negotiationResult.RejectionReason}."
                : negotiationResult.RejectionMessage;

            if (negotiationResult.ErrorCode == ErrorCode.LimitExceeded)
            {
                return NnrpProtocolFailure.LimitExceeded(NnrpErrorScope.Session, message, isFatal: true);
            }

            return NnrpProtocolFailure.UnsupportedCapability(message);
        }
    }
}
