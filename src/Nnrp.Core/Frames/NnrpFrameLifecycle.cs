using System.Collections.Generic;

namespace Nnrp.Core
{
    public sealed class NnrpFrameLifecycle
    {
        private readonly Dictionary<NnrpFrameKey, NnrpFrameState> states = new Dictionary<NnrpFrameKey, NnrpFrameState>();

        public int Count => states.Count;

        public bool TryGetState(uint frameId, ushort viewId, out NnrpFrameState state)
        {
            return states.TryGetValue(new NnrpFrameKey(frameId, viewId), out state);
        }

        public bool TryAnnounce(uint frameId, ushort viewId, out NnrpProtocolFailure failure)
        {
            var key = new NnrpFrameKey(frameId, viewId);
            if (states.ContainsKey(key))
            {
                failure = InvalidFrameState(key, "announce an already tracked frame");
                return false;
            }

            states.Add(key, NnrpFrameState.Announced);
            failure = NnrpProtocolFailure.None;
            return true;
        }

        public bool TrySubmit(uint frameId, ushort viewId, uint retryOfFrame, out NnrpProtocolFailure failure)
        {
            var key = new NnrpFrameKey(frameId, viewId);
            if (!ValidateRetryReference(key, retryOfFrame, out failure))
            {
                return false;
            }

            if (!states.TryGetValue(key, out var state))
            {
                states.Add(key, NnrpFrameState.Submitted);
                failure = NnrpProtocolFailure.None;
                return true;
            }

            if (state != NnrpFrameState.Announced)
            {
                failure = InvalidFrameState(key, $"submit a frame in {state} state");
                return false;
            }

            states[key] = NnrpFrameState.Submitted;
            failure = NnrpProtocolFailure.None;
            return true;
        }

        public bool TryStartProcessing(uint frameId, ushort viewId, out NnrpProtocolFailure failure)
        {
            var key = new NnrpFrameKey(frameId, viewId);
            if (!states.TryGetValue(key, out var state) || state != NnrpFrameState.Submitted)
            {
                failure = InvalidFrameState(key, "start processing before SUBMITTED");
                return false;
            }

            states[key] = NnrpFrameState.Processing;
            failure = NnrpProtocolFailure.None;
            return true;
        }

        public bool TryMarkReady(uint frameId, ushort viewId, out NnrpProtocolFailure failure)
        {
            var key = new NnrpFrameKey(frameId, viewId);
            if (!states.TryGetValue(key, out var state) || state != NnrpFrameState.Processing)
            {
                failure = InvalidFrameState(key, "mark ready before PROCESSING");
                return false;
            }

            states[key] = NnrpFrameState.Ready;
            failure = NnrpProtocolFailure.None;
            return true;
        }

        public bool TryDeliver(uint frameId, ushort viewId, out NnrpProtocolFailure failure)
        {
            var key = new NnrpFrameKey(frameId, viewId);
            if (!states.TryGetValue(key, out var state) || state != NnrpFrameState.Ready)
            {
                failure = InvalidFrameState(key, "deliver before READY");
                return false;
            }

            states[key] = NnrpFrameState.Delivered;
            failure = NnrpProtocolFailure.None;
            return true;
        }

        public bool TryDrop(uint frameId, ushort viewId, out NnrpProtocolFailure failure)
        {
            return TryMoveToTerminal(frameId, viewId, NnrpFrameState.Dropped, "drop", out failure);
        }

        public bool TryCancel(uint frameId, ushort viewId, out NnrpProtocolFailure failure)
        {
            var key = new NnrpFrameKey(frameId, viewId);
            if (!states.TryGetValue(key, out var state)
                || (state != NnrpFrameState.Announced
                    && state != NnrpFrameState.Submitted
                    && state != NnrpFrameState.Processing))
            {
                failure = InvalidFrameState(key, "cancel after frame can no longer be cancelled");
                return false;
            }

            states[key] = NnrpFrameState.Cancelled;
            failure = NnrpProtocolFailure.None;
            return true;
        }

        public bool TryExpire(uint frameId, ushort viewId, out NnrpProtocolFailure failure)
        {
            return TryMoveToTerminal(frameId, viewId, NnrpFrameState.Expired, "expire", out failure);
        }

        private bool TryMoveToTerminal(
            uint frameId,
            ushort viewId,
            NnrpFrameState terminalState,
            string action,
            out NnrpProtocolFailure failure)
        {
            var key = new NnrpFrameKey(frameId, viewId);
            if (!states.TryGetValue(key, out var state) || IsTerminal(state))
            {
                failure = InvalidFrameState(key, $"{action} a frame in {state} state");
                return false;
            }

            states[key] = terminalState;
            failure = NnrpProtocolFailure.None;
            return true;
        }

        private bool ValidateRetryReference(NnrpFrameKey key, uint retryOfFrame, out NnrpProtocolFailure failure)
        {
            if (retryOfFrame == 0)
            {
                failure = NnrpProtocolFailure.None;
                return true;
            }

            if (retryOfFrame == key.FrameId)
            {
                failure = InvalidFrameState(key, "submit a retry that references itself");
                return false;
            }

            var retryKey = new NnrpFrameKey(retryOfFrame, key.ViewId);
            if (!states.ContainsKey(retryKey))
            {
                failure = InvalidFrameState(key, $"submit a retry for missing frame {retryOfFrame} view {key.ViewId}");
                return false;
            }

            failure = NnrpProtocolFailure.None;
            return true;
        }

        private static bool IsTerminal(NnrpFrameState state)
        {
            return state == NnrpFrameState.Delivered
                || state == NnrpFrameState.Dropped
                || state == NnrpFrameState.Cancelled
                || state == NnrpFrameState.Expired;
        }

        private static NnrpProtocolFailure InvalidFrameState(NnrpFrameKey key, string action)
        {
            return NnrpProtocolFailure.InvalidState(
                NnrpErrorScope.Frame,
                $"Cannot {action} for frame {key.FrameId} view {key.ViewId}.");
        }
    }
}
