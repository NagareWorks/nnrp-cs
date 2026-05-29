using System;
using Nnrp.Core;

namespace Nnrp.Client
{
    public readonly struct NnrpSessionEvent
    {
        private NnrpSessionEvent(
            MessageType messageType,
            ResultPushMessage resultPush,
            ResultDropMessage resultDrop,
            FlowUpdateMessage flowUpdate,
            ResultHintMessage resultHint)
        {
            MessageType = messageType;
            ResultPush = resultPush;
            ResultDrop = resultDrop;
            FlowUpdate = flowUpdate;
            ResultHint = resultHint;
        }

        public MessageType MessageType { get; }

        public ResultPushMessage ResultPush { get; }

        public ResultDropMessage ResultDrop { get; }

        public FlowUpdateMessage FlowUpdate { get; }

        public ResultHintMessage ResultHint { get; }

        public bool IsResultPush => MessageType == MessageType.ResultPush;

        public bool IsResultDrop => MessageType == MessageType.ResultDrop;

        public bool IsFlowUpdate => MessageType == MessageType.FlowUpdate;

        public bool IsResultHint => MessageType == MessageType.ResultHint;

        public static NnrpSessionEvent FromResultPush(ResultPushMessage resultPush)
        {
            return new NnrpSessionEvent(MessageType.ResultPush, resultPush, default, default, default);
        }

        public static NnrpSessionEvent FromResultDrop(ResultDropMessage resultDrop)
        {
            return new NnrpSessionEvent(MessageType.ResultDrop, default, resultDrop, default, default);
        }

        public static NnrpSessionEvent FromFlowUpdate(FlowUpdateMessage flowUpdate)
        {
            return new NnrpSessionEvent(MessageType.FlowUpdate, default, default, flowUpdate, default);
        }

        public static NnrpSessionEvent FromResultHint(ResultHintMessage resultHint)
        {
            return new NnrpSessionEvent(MessageType.ResultHint, default, default, default, resultHint);
        }

        public ResultPushMessage GetResultPush()
        {
            if (!IsResultPush)
            {
                throw new InvalidOperationException($"Session event does not carry RESULT_PUSH; actual message type is {MessageType}.");
            }

            return ResultPush;
        }

        public ResultDropMessage GetResultDrop()
        {
            if (!IsResultDrop)
            {
                throw new InvalidOperationException($"Session event does not carry RESULT_DROP; actual message type is {MessageType}.");
            }

            return ResultDrop;
        }

        public FlowUpdateMessage GetFlowUpdate()
        {
            if (!IsFlowUpdate)
            {
                throw new InvalidOperationException($"Session event does not carry FLOW_UPDATE; actual message type is {MessageType}.");
            }

            return FlowUpdate;
        }

        public FlowCreditUpdate GetFlowCreditUpdate()
        {
            return GetFlowUpdate().CreditUpdate;
        }

        public ResultHintMessage GetResultHint()
        {
            if (!IsResultHint)
            {
                throw new InvalidOperationException($"Session event does not carry RESULT_HINT; actual message type is {MessageType}.");
            }

            return ResultHint;
        }
    }
}
