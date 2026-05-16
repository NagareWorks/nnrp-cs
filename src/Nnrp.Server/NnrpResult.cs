using System;
using Nnrp.Core;

namespace Nnrp.Server
{
    public readonly struct NnrpResult
    {
        public NnrpResult(
            uint frameId,
            ReadOnlyMemory<ushort> tileIds,
            ReadOnlyMemory<TensorSectionBlock> sections,
            ushort viewId = 0,
            ulong traceId = 0,
            ResultStatusCode statusCode = ResultStatusCode.Success,
            ResultFlags resultFlags = ResultFlags.None,
            ushort activeProfileId = 1,
            ushort inferenceMilliseconds = 0,
            ushort queueMilliseconds = 0,
            ushort serverTotalMilliseconds = 0,
            uint tileBaseId = 0,
            ResultClass resultClass = ResultClass.Complete,
            BudgetPolicy appliedBudgetPolicy = BudgetPolicy.None,
            uint reusedFrameId = 0,
            ushort coveredTileCount = 0,
            ushort droppedTileCount = 0,
            PayloadKind payloadKindBitmap = PayloadKind.Tensor,
            ushort payloadFrameCount = 0)
        {
            var normalizedCoveredTileCount = coveredTileCount;
            var normalizedDroppedTileCount = droppedTileCount;
            if ((payloadKindBitmap & PayloadKind.Tensor) != 0
                && tileIds.Length != 0
                && normalizedCoveredTileCount == 0
                && normalizedDroppedTileCount == 0
                && resultClass != ResultClass.Partial
                && (resultFlags & ResultFlags.Partial) == 0)
            {
                normalizedCoveredTileCount = checked((ushort)tileIds.Length);
            }

            FrameId = frameId;
            TileIds = tileIds;
            Sections = sections;
            ViewId = viewId;
            TraceId = traceId;
            StatusCode = statusCode;
            ResultFlags = resultFlags;
            ActiveProfileId = activeProfileId;
            InferenceMilliseconds = inferenceMilliseconds;
            QueueMilliseconds = queueMilliseconds;
            ServerTotalMilliseconds = serverTotalMilliseconds;
            TileBaseId = tileBaseId;
            ResultClass = resultClass;
            AppliedBudgetPolicy = appliedBudgetPolicy;
            ReusedFrameId = reusedFrameId;
            CoveredTileCount = normalizedCoveredTileCount;
            DroppedTileCount = normalizedDroppedTileCount;
            PayloadKindBitmap = payloadKindBitmap;
            PayloadFrameCount = payloadFrameCount;
        }

        public uint FrameId { get; }

        public ReadOnlyMemory<ushort> TileIds { get; }

        public ReadOnlyMemory<TensorSectionBlock> Sections { get; }

        public ushort ViewId { get; }

        public ulong TraceId { get; }

        public ResultStatusCode StatusCode { get; }

        public ResultFlags ResultFlags { get; }

        public ushort ActiveProfileId { get; }

        public ushort InferenceMilliseconds { get; }

        public ushort QueueMilliseconds { get; }

        public ushort ServerTotalMilliseconds { get; }

        public uint TileBaseId { get; }

        public ResultClass ResultClass { get; }

        public BudgetPolicy AppliedBudgetPolicy { get; }

        public uint ReusedFrameId { get; }

        public ushort CoveredTileCount { get; }

        public ushort DroppedTileCount { get; }

        public PayloadKind PayloadKindBitmap { get; }

        public ushort PayloadFrameCount { get; }
    }
}