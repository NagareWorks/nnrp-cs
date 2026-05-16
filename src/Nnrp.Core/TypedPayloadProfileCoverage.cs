using System;

namespace Nnrp.Core
{
    public readonly struct TypedPayloadProfileCoverage : IEquatable<TypedPayloadProfileCoverage>
    {
        public TypedPayloadProfileCoverage(
            PayloadKind payloadKind,
            ushort profileId,
            ushort frameCount,
            uint payloadBytes)
        {
            PayloadKind = payloadKind;
            ProfileId = profileId;
            FrameCount = frameCount;
            PayloadBytes = payloadBytes;
        }

        public PayloadKind PayloadKind { get; }

        public ushort ProfileId { get; }

        public ushort FrameCount { get; }

        public uint PayloadBytes { get; }

        public bool Equals(TypedPayloadProfileCoverage other)
        {
            return PayloadKind == other.PayloadKind
                && ProfileId == other.ProfileId
                && FrameCount == other.FrameCount
                && PayloadBytes == other.PayloadBytes;
        }

        public override bool Equals(object obj)
        {
            return obj is TypedPayloadProfileCoverage other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = PayloadKind.GetHashCode();
                hash = (hash * 397) ^ ProfileId.GetHashCode();
                hash = (hash * 397) ^ FrameCount.GetHashCode();
                hash = (hash * 397) ^ PayloadBytes.GetHashCode();
                return hash;
            }
        }
    }
}