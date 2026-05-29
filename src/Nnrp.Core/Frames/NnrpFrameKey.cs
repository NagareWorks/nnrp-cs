using System;

namespace Nnrp.Core
{
    public readonly struct NnrpFrameKey : IEquatable<NnrpFrameKey>
    {
        public NnrpFrameKey(uint frameId, ushort viewId)
        {
            FrameId = frameId;
            ViewId = viewId;
        }

        public uint FrameId { get; }

        public ushort ViewId { get; }

        public bool Equals(NnrpFrameKey other)
        {
            return FrameId == other.FrameId && ViewId == other.ViewId;
        }

        public override bool Equals(object obj)
        {
            return obj is NnrpFrameKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)FrameId * 397) ^ ViewId;
            }
        }
    }
}
