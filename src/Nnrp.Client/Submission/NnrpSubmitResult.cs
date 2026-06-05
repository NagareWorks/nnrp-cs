using System;
using Nnrp.Core;

namespace Nnrp.Client
{
    public readonly struct NnrpSubmitResult
    {
        public NnrpSubmitResult(
            uint sessionId,
            uint frameId,
            ushort viewId,
            ResultStatusCode statusCode,
            ResultFlags resultFlags,
            ushort activeProfileId,
            ushort inferenceMilliseconds,
            ushort queueMilliseconds,
            ushort serverTotalMilliseconds,
            ReadOnlyMemory<ushort> tileIds,
            ReadOnlyMemory<TensorSectionBlock> sections,
            ResultClass resultClass = ResultClass.Complete,
            BudgetPolicy appliedBudgetPolicy = BudgetPolicy.None,
            uint reusedFrameId = 0,
            ushort coveredTileCount = 0,
            ushort droppedTileCount = 0,
            PayloadKind payloadKindBitmap = PayloadKind.Tensor,
            ushort payloadFrameCount = 0,
            ReadOnlyMemory<TypedPayloadFrameView> typedPayloadFrames = default)
        {
            SessionId = sessionId;
            FrameId = frameId;
            ViewId = viewId;
            StatusCode = statusCode;
            ResultFlags = resultFlags;
            ActiveProfileId = activeProfileId;
            InferenceMilliseconds = inferenceMilliseconds;
            QueueMilliseconds = queueMilliseconds;
            ServerTotalMilliseconds = serverTotalMilliseconds;
            TileIds = tileIds;
            Sections = sections;
            ResultClass = resultClass;
            AppliedBudgetPolicy = appliedBudgetPolicy;
            ReusedFrameId = reusedFrameId;
            CoveredTileCount = coveredTileCount;
            DroppedTileCount = droppedTileCount;
            PayloadKindBitmap = payloadKindBitmap;
            PayloadFrameCount = payloadFrameCount;
            TypedPayloadFrames = typedPayloadFrames.IsEmpty ? ReadOnlyMemory<TypedPayloadFrameView>.Empty : typedPayloadFrames;
        }

        public uint SessionId { get; }

        public uint FrameId { get; }

        public ushort ViewId { get; }

        public ResultStatusCode StatusCode { get; }

        public ResultFlags ResultFlags { get; }

        public ushort ActiveProfileId { get; }

        public ushort InferenceMilliseconds { get; }

        public ushort QueueMilliseconds { get; }

        public ushort ServerTotalMilliseconds { get; }

        public ResultClass ResultClass { get; }

        public BudgetPolicy AppliedBudgetPolicy { get; }

        public uint ReusedFrameId { get; }

        public ushort CoveredTileCount { get; }

        public ushort DroppedTileCount { get; }

        public PayloadKind PayloadKindBitmap { get; }

        public ushort PayloadFrameCount { get; }

        public ReadOnlyMemory<TypedPayloadFrameView> TypedPayloadFrames { get; }

        public ReadOnlyMemory<ushort> TileIds { get; }

        public ReadOnlyMemory<TensorSectionBlock> Sections { get; }

        public TypedPayloadFrameView[] GetTypedPayloadFrames(PayloadKind payloadKind, ushort profileId)
        {
            var rawPayloadKind = (uint)payloadKind;
            if (rawPayloadKind == 0
                || (rawPayloadKind & (rawPayloadKind - 1)) != 0
                || !PayloadKindValidator.IsDefinedBitmap(payloadKind))
            {
                throw new ArgumentOutOfRangeException(nameof(payloadKind), "Typed payload frame lookup requires a single defined payload kind bit.");
            }

            if (TypedPayloadFrames.IsEmpty)
            {
                return Array.Empty<TypedPayloadFrameView>();
            }

            var matchCount = 0;
            foreach (var frame in TypedPayloadFrames.Span)
            {
                if (frame.PayloadKind == payloadKind && frame.ProfileId == profileId)
                {
                    matchCount++;
                }
            }

            if (matchCount == 0)
            {
                return Array.Empty<TypedPayloadFrameView>();
            }

            var matches = new TypedPayloadFrameView[matchCount];
            var nextIndex = 0;
            foreach (var frame in TypedPayloadFrames.Span)
            {
                if (frame.PayloadKind == payloadKind && frame.ProfileId == profileId)
                {
                    matches[nextIndex++] = frame;
                }
            }

            return matches;
        }

        public TypedPayloadProfileFrames GetPayloadFrames(PayloadKind payloadKind, ushort profileId)
        {
            return new TypedPayloadProfileFrames(payloadKind, profileId, GetTypedPayloadFrames(payloadKind, profileId));
        }

        public TypedPayloadProfileFrames GetTokenChunkFrames(ushort profileId)
        {
            return GetPayloadFrames(PayloadKind.TokenChunk, profileId);
        }

        public TypedPayloadProfileFrames GetAudioChunkFrames(ushort profileId)
        {
            return GetPayloadFrames(PayloadKind.AudioChunk, profileId);
        }

        public TypedPayloadProfileFrames GetVideoChunkFrames(ushort profileId)
        {
            return GetPayloadFrames(PayloadKind.VideoChunk, profileId);
        }

        public TypedPayloadProfileFrames GetStructuredEventFrames(ushort profileId)
        {
            return GetPayloadFrames(PayloadKind.StructuredEvent, profileId);
        }

        public TypedPayloadProfileFrames GetToolDeltaFrames(ushort profileId)
        {
            return GetPayloadFrames(PayloadKind.ToolDelta, profileId);
        }

        public TypedPayloadProfileFrames GetOpaqueBytesFrames(ushort profileId)
        {
            return GetPayloadFrames(PayloadKind.OpaqueBytes, profileId);
        }
    }
}
