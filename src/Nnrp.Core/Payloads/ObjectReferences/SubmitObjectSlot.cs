using System;

namespace Nnrp.Core
{
    [Flags]
    public enum SubmitObjectSlot : uint
    {
        None = 0,
        CameraBlock = 1u << 0,
        TileIndexBlock = 1u << 1,
        TensorSectionTable = 1u << 2,
        PayloadLayoutTemplate = 1u << 3,
    }
}
