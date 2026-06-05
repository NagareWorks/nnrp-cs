using System;
using Nnrp.Core;

namespace Nnrp.Client
{
    public readonly struct NnrpClientConnectResult
    {
        private NnrpClientConnectResult(
            bool isConnected,
            NnrpCapabilityNegotiationResult negotiationResult,
            NnrpProtocolFailure failure)
        {
            IsConnected = isConnected;
            NegotiationResult = negotiationResult;
            Failure = failure;
        }

        public bool IsConnected { get; }

        public NnrpCapabilityNegotiationResult NegotiationResult { get; }

        public NnrpProtocolFailure Failure { get; }

        public static NnrpClientConnectResult Connected(NnrpCapabilityNegotiationResult negotiationResult)
        {
            if (!negotiationResult.IsAccepted)
            {
                throw new ArgumentException("Connected results require an accepted negotiation result.", nameof(negotiationResult));
            }

            return new NnrpClientConnectResult(true, negotiationResult, NnrpProtocolFailure.None);
        }

        public static NnrpClientConnectResult Rejected(
            NnrpCapabilityNegotiationResult negotiationResult,
            NnrpProtocolFailure failure)
        {
            if (negotiationResult.IsAccepted)
            {
                throw new ArgumentException("Rejected results require a rejected negotiation result.", nameof(negotiationResult));
            }

            return new NnrpClientConnectResult(false, negotiationResult, failure);
        }

        public static NnrpClientConnectResult Failed(NnrpProtocolFailure failure)
        {
            return new NnrpClientConnectResult(false, default, failure);
        }
    }
}
