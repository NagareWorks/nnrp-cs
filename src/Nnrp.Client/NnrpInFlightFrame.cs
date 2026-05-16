using System;

namespace Nnrp.Client
{
    public readonly struct NnrpInFlightFrame : IEquatable<NnrpInFlightFrame>
    {
        public NnrpInFlightFrame(uint frameId, ushort viewId)
        {
            FrameId = frameId;
            ViewId = viewId;
        }

        public uint FrameId { get; }

        public ushort ViewId { get; }

        public bool Equals(NnrpInFlightFrame other)
        {
            return FrameId == other.FrameId && ViewId == other.ViewId;
        }

        public override bool Equals(object obj)
        {
            return obj is NnrpInFlightFrame other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (FrameId.GetHashCode() * 397) ^ ViewId.GetHashCode();
            }
        }
    }
}
