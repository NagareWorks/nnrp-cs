using System;
using Nnrp.Core;

namespace Nnrp.Client
{
    public readonly struct NnrpSubmitRequest
    {
        public NnrpSubmitRequest(
            uint frameId,
            ushort sourceWidth,
            ushort sourceHeight,
            ushort tileWidth,
            ushort tileHeight,
            ReadOnlyMemory<byte> cameraBlock,
            ReadOnlyMemory<ushort> tileIds,
            ReadOnlyMemory<TensorSectionBlock> sections,
            ushort viewId = 0,
            ulong traceId = 0,
            FrameClass frameClass = FrameClass.Keyframe,
            InputProfile inputProfile = InputProfile.DenseLumaFrame,
            TileIndexMode tileIndexMode = TileIndexMode.RawUInt16,
            ushort latencyBudgetMilliseconds = 16,
            ushort cadenceHintX100 = 0,
            uint dependencyFrameId = 0,
            uint tileBaseId = 0)
        {
            FrameId = frameId;
            SourceWidth = sourceWidth;
            SourceHeight = sourceHeight;
            TileWidth = tileWidth;
            TileHeight = tileHeight;
            CameraBlock = cameraBlock;
            TileIds = tileIds;
            Sections = sections;
            ViewId = viewId;
            TraceId = traceId;
            FrameClass = frameClass;
            InputProfile = inputProfile;
            TileIndexMode = tileIndexMode;
            LatencyBudgetMilliseconds = latencyBudgetMilliseconds;
            CadenceHintX100 = cadenceHintX100;
            DependencyFrameId = dependencyFrameId;
            TileBaseId = tileBaseId;
        }

        public uint FrameId { get; }

        public ushort SourceWidth { get; }

        public ushort SourceHeight { get; }

        public ushort TileWidth { get; }

        public ushort TileHeight { get; }

        public ReadOnlyMemory<byte> CameraBlock { get; }

        public ReadOnlyMemory<ushort> TileIds { get; }

        public ReadOnlyMemory<TensorSectionBlock> Sections { get; }

        public ushort ViewId { get; }

        public ulong TraceId { get; }

        public FrameClass FrameClass { get; }

        public InputProfile InputProfile { get; }

        public TileIndexMode TileIndexMode { get; }

        public ushort LatencyBudgetMilliseconds { get; }

        public ushort CadenceHintX100 { get; }

        public uint DependencyFrameId { get; }

        public uint TileBaseId { get; }
    }
}