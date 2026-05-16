using System;
using Nnrp.Core;

namespace Nnrp.Server
{
    public readonly struct NnrpFrameSubmit
    {
        public NnrpFrameSubmit(
            uint sessionId,
            uint frameId,
            ushort viewId,
            ulong traceId,
            ushort sourceWidth,
            ushort sourceHeight,
            ushort tileWidth,
            ushort tileHeight,
            ReadOnlyMemory<byte> cameraBlock,
            ReadOnlyMemory<ushort> tileIds,
            ReadOnlyMemory<TensorSectionBlock> sections,
            FrameClass frameClass,
            InputProfile inputProfile,
            TileIndexMode tileIndexMode,
            ushort latencyBudgetMilliseconds,
            ushort cadenceHintX100,
            uint dependencyFrameId,
            uint tileBaseId)
        {
            SessionId = sessionId;
            FrameId = frameId;
            ViewId = viewId;
            TraceId = traceId;
            SourceWidth = sourceWidth;
            SourceHeight = sourceHeight;
            TileWidth = tileWidth;
            TileHeight = tileHeight;
            CameraBlock = cameraBlock;
            TileIds = tileIds;
            Sections = sections;
            FrameClass = frameClass;
            InputProfile = inputProfile;
            TileIndexMode = tileIndexMode;
            LatencyBudgetMilliseconds = latencyBudgetMilliseconds;
            CadenceHintX100 = cadenceHintX100;
            DependencyFrameId = dependencyFrameId;
            TileBaseId = tileBaseId;
        }

        public uint SessionId { get; }

        public uint FrameId { get; }

        public ushort ViewId { get; }

        public ulong TraceId { get; }

        public ushort SourceWidth { get; }

        public ushort SourceHeight { get; }

        public ushort TileWidth { get; }

        public ushort TileHeight { get; }

        public ReadOnlyMemory<byte> CameraBlock { get; }

        public ReadOnlyMemory<ushort> TileIds { get; }

        public ReadOnlyMemory<TensorSectionBlock> Sections { get; }

        public FrameClass FrameClass { get; }

        public InputProfile InputProfile { get; }

        public TileIndexMode TileIndexMode { get; }

        public ushort LatencyBudgetMilliseconds { get; }

        public ushort CadenceHintX100 { get; }

        public uint DependencyFrameId { get; }

        public uint TileBaseId { get; }
    }
}
