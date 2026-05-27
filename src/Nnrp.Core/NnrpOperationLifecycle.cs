using System;

namespace Nnrp.Core
{
    public enum NnrpOperationState : byte
    {
        Accepted = 0,
        Running = 1,
        Partial = 2,
        WaitingTool = 3,
        Superseded = 4,
        Cancelled = 5,
        Failed = 6,
        Completed = 7,
    }

    public sealed class NnrpOperationLifecycle
    {
        public NnrpOperationLifecycle(uint operationId)
        {
            if (operationId == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(operationId));
            }

            OperationId = operationId;
            State = NnrpOperationState.Accepted;
        }

        public uint OperationId { get; }

        public NnrpOperationState State { get; private set; }

        public bool IsTerminal => IsTerminalState(State);

        public bool TryStart(out NnrpProtocolFailure failure)
        {
            return TryMove(NnrpOperationState.Running, out failure);
        }

        public bool TryMarkPartial(out NnrpProtocolFailure failure)
        {
            return TryMove(NnrpOperationState.Partial, out failure);
        }

        public bool TryWaitForTool(out NnrpProtocolFailure failure)
        {
            return TryMove(NnrpOperationState.WaitingTool, out failure);
        }

        public bool TryResumeFromTool(out NnrpProtocolFailure failure)
        {
            if (State != NnrpOperationState.WaitingTool)
            {
                failure = InvalidTransition("resume from waiting tool");
                return false;
            }

            return TryMove(NnrpOperationState.Running, out failure);
        }

        public bool TryComplete(out NnrpProtocolFailure failure)
        {
            return TryMove(NnrpOperationState.Completed, out failure);
        }

        public bool TryCancel(out NnrpProtocolFailure failure)
        {
            return TryMove(NnrpOperationState.Cancelled, out failure);
        }

        public bool TryFail(out NnrpProtocolFailure failure)
        {
            return TryMove(NnrpOperationState.Failed, out failure);
        }

        public bool TrySupersede(out NnrpProtocolFailure failure)
        {
            return TryMove(NnrpOperationState.Superseded, out failure);
        }

        public bool TryApplyResult(ResultPushMessage result, out NnrpProtocolFailure failure)
        {
            if (result.Header.FrameId != OperationId)
            {
                failure = InvalidTransition($"apply result for operation {result.Header.FrameId}");
                return false;
            }

            if (result.Metadata.ResultClass == ResultClass.Partial
                || (result.Metadata.ResultFlags & ResultFlags.Partial) != 0)
            {
                if (State == NnrpOperationState.Accepted)
                {
                    State = NnrpOperationState.Running;
                }

                return TryMarkPartial(out failure);
            }

            if (result.Metadata.StatusCode == ResultStatusCode.Success
                && result.Metadata.ResultClass == ResultClass.Complete)
            {
                return TryComplete(out failure);
            }

            return TryFail(out failure);
        }

        public bool TryApplyDrop(ResultDropMessage drop, out NnrpProtocolFailure failure)
        {
            if (drop.Header.FrameId != OperationId)
            {
                failure = InvalidTransition($"apply drop for operation {drop.Header.FrameId}");
                return false;
            }

            return TrySupersede(out failure);
        }

        public static bool IsTerminalState(NnrpOperationState state)
        {
            return state == NnrpOperationState.Superseded
                || state == NnrpOperationState.Cancelled
                || state == NnrpOperationState.Failed
                || state == NnrpOperationState.Completed;
        }

        private bool TryMove(NnrpOperationState nextState, out NnrpProtocolFailure failure)
        {
            if (!CanMove(State, nextState))
            {
                failure = InvalidTransition($"move to {nextState}");
                return false;
            }

            State = nextState;
            failure = NnrpProtocolFailure.None;
            return true;
        }

        private static bool CanMove(NnrpOperationState currentState, NnrpOperationState nextState)
        {
            if (IsTerminalState(currentState))
            {
                return false;
            }

            switch (nextState)
            {
                case NnrpOperationState.Running:
                    return currentState == NnrpOperationState.Accepted
                        || currentState == NnrpOperationState.WaitingTool;
                case NnrpOperationState.Partial:
                    return currentState == NnrpOperationState.Running
                        || currentState == NnrpOperationState.Partial
                        || currentState == NnrpOperationState.WaitingTool;
                case NnrpOperationState.WaitingTool:
                    return currentState == NnrpOperationState.Running
                        || currentState == NnrpOperationState.Partial;
                case NnrpOperationState.Superseded:
                case NnrpOperationState.Cancelled:
                case NnrpOperationState.Failed:
                case NnrpOperationState.Completed:
                    return currentState == NnrpOperationState.Accepted
                        || currentState == NnrpOperationState.Running
                        || currentState == NnrpOperationState.Partial
                        || currentState == NnrpOperationState.WaitingTool;
                default:
                    return false;
            }
        }

        private NnrpProtocolFailure InvalidTransition(string action)
        {
            return NnrpProtocolFailure.InvalidState(
                NnrpErrorScope.Frame,
                $"Cannot {action} while operation {OperationId} state is {State}.");
        }
    }
}
