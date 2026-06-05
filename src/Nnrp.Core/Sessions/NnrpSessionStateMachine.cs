namespace Nnrp.Core
{
    public sealed class NnrpSessionStateMachine
    {
        public NnrpSessionStateMachine()
        {
            State = NnrpSessionState.Init;
            LastFailure = NnrpProtocolFailure.None;
        }

        public NnrpSessionState State { get; private set; }

        public NnrpProtocolFailure LastFailure { get; private set; }

        public bool TryBeginNegotiation(out NnrpProtocolFailure failure)
        {
            if (State != NnrpSessionState.Init)
            {
                return RejectTransition("begin negotiation", out failure);
            }

            State = NnrpSessionState.Negotiating;
            failure = NnrpProtocolFailure.None;
            return true;
        }

        public bool TryActivate(out NnrpProtocolFailure failure)
        {
            if (State != NnrpSessionState.Negotiating)
            {
                return RejectTransition("activate session", out failure);
            }

            State = NnrpSessionState.Active;
            failure = NnrpProtocolFailure.None;
            return true;
        }

        public bool TryFailNegotiation(NnrpProtocolFailure reason, out NnrpProtocolFailure failure)
        {
            if (State != NnrpSessionState.Negotiating)
            {
                return RejectTransition("fail negotiation", out failure);
            }

            LastFailure = reason.IsFailure
                ? reason
                : NnrpProtocolFailure.UnsupportedCapability("Negotiation failed.");
            State = NnrpSessionState.Closed;
            failure = NnrpProtocolFailure.None;
            return true;
        }

        public bool TryBeginDraining(out NnrpProtocolFailure failure)
        {
            if (State != NnrpSessionState.Active)
            {
                return RejectTransition("begin draining", out failure);
            }

            State = NnrpSessionState.Draining;
            failure = NnrpProtocolFailure.None;
            return true;
        }

        public bool TryClose(out NnrpProtocolFailure failure)
        {
            switch (State)
            {
                case NnrpSessionState.Init:
                case NnrpSessionState.Negotiating:
                    State = NnrpSessionState.Closed;
                    failure = NnrpProtocolFailure.None;
                    return true;
                case NnrpSessionState.Active:
                    State = NnrpSessionState.Draining;
                    failure = NnrpProtocolFailure.None;
                    return true;
                case NnrpSessionState.Draining:
                    State = NnrpSessionState.Closed;
                    failure = NnrpProtocolFailure.None;
                    return true;
                default:
                    return RejectTransition("close session", out failure);
            }
        }

        public bool TryAcceptFrameSubmit(out NnrpProtocolFailure failure)
        {
            if (State == NnrpSessionState.Active)
            {
                failure = NnrpProtocolFailure.None;
                return true;
            }

            failure = NnrpProtocolFailure.InvalidState(
                NnrpErrorScope.Session,
                "FRAME_SUBMIT requires an ACTIVE session.");
            return false;
        }

        public void ApplyFailure(NnrpProtocolFailure failure)
        {
            if (!failure.IsFailure)
            {
                return;
            }

            LastFailure = failure;
            if (!failure.IsFatal)
            {
                return;
            }

            switch (State)
            {
                case NnrpSessionState.Active:
                    State = NnrpSessionState.Draining;
                    break;
                case NnrpSessionState.Draining:
                case NnrpSessionState.Closed:
                    break;
                default:
                    State = NnrpSessionState.Closed;
                    break;
            }
        }

        private bool RejectTransition(string action, out NnrpProtocolFailure failure)
        {
            failure = NnrpProtocolFailure.InvalidState(
                NnrpErrorScope.Session,
                $"Cannot {action} while session state is {State}.");
            return false;
        }
    }
}
