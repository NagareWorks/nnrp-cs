using System;
using Nnrp.Core;

namespace Nnrp.Client
{
    public readonly struct NnrpSubmittedFrame : IEquatable<NnrpSubmittedFrame>
    {
        public NnrpSubmittedFrame(
            uint sessionId,
            uint frameId,
            ushort viewId,
            ulong traceId)
        {
            SessionId = sessionId;
            FrameId = frameId;
            ViewId = viewId;
            TraceId = traceId;
        }

        public uint SessionId { get; }

        public uint FrameId { get; }

        public ushort ViewId { get; }

        public ulong TraceId { get; }

        public byte WireFormat => NnrpHeader.CurrentWireFormat;

        public bool Equals(NnrpSubmittedFrame other)
        {
            return SessionId == other.SessionId
                && FrameId == other.FrameId
                && ViewId == other.ViewId
                && TraceId == other.TraceId;
        }

        public override bool Equals(object obj)
        {
            return obj is NnrpSubmittedFrame other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = SessionId.GetHashCode();
                hash = (hash * 397) ^ FrameId.GetHashCode();
                hash = (hash * 397) ^ ViewId.GetHashCode();
                hash = (hash * 397) ^ TraceId.GetHashCode();
                return hash;
            }
        }
    }
}
