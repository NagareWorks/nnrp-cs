using System;

namespace Nnrp.Core
{
    [Flags]
    public enum SessionPatchField : uint
    {
        None = 0,
        TargetCadence = 0x00000001,
        QualityTier = 0x00000002,
        DegradePolicy = 0x00000004,
        ActiveLaneMask = 0x00000008,
        PreferredCodec = 0x00000010,
        PreferredCompression = 0x00000020,
        ProfilePatch = 0x00000040,

        TargetFps = TargetCadence,
        ResolutionClamp = ProfilePatch,
        ActiveViewMask = ActiveLaneMask,
    }

    public enum SessionPatchAckStatus : uint
    {
        Accepted = 0,
        PartiallyApplied = 1,
        Rejected = 2,
    }

    public enum SessionPatchRejectReason : uint
    {
        None = 0,
        InvalidFieldMask = 1,
        ImmutableField = 2,
        UnsupportedValue = 3,
        OutOfRange = 4,
        ServerBusy = 5,

        UnsupportedField = InvalidFieldMask,
        InvalidRange = OutOfRange,
        UnsupportedStrategy = UnsupportedValue,
        InvalidViewMask = OutOfRange,
        RateLimited = ServerBusy,
    }

    public enum CacheObjectKind : uint
    {
        CameraBlock = 0x0001,
        TileIndexTemplate = 0x0002,
        TensorSectionTable = 0x0003,
        CodecTable = 0x0004,
        ReusableResultObject = 0x0005,
        PayloadLayoutTemplate = 0x0006,
        PromptSegment = 0x0007,
        ToolSchema = 0x0008,
        StructuredEventSchema = 0x0009,

        TileIndexBlock = TileIndexTemplate,
        CodecAuxBlock = CodecTable,
        FallbackResource = ReusableResultObject,
    }

    public enum CacheAckStatus : uint
    {
        Accepted = 0,
        Rejected = 1,
        Replaced = 2,
    }

    public enum CacheInvalidateScope : uint
    {
        WholeSession = 0,
        Namespace = 1,
        ObjectKind = 2,
        ObjectKey = 3,

        Entry = ObjectKey,
        Session = WholeSession,
    }

    [Flags]
    public enum CachePutFlags : uint
    {
        None = 0,
        Pinned = 0x00000001,
        Reusable = 0x00000002,
    }
}
