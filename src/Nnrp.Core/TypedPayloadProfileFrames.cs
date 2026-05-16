using System;

namespace Nnrp.Core
{
    public readonly struct TypedPayloadProfileFrames : IEquatable<TypedPayloadProfileFrames>
    {
        public TypedPayloadProfileFrames(
            PayloadKind payloadKind,
            ushort profileId,
            ReadOnlyMemory<TypedPayloadFrameView> frames)
        {
            var rawPayloadKind = (uint)payloadKind;
            if (rawPayloadKind == 0
                || (rawPayloadKind & (rawPayloadKind - 1)) != 0
                || !PayloadKindValidator.IsDefinedBitmap(payloadKind))
            {
                throw new ArgumentOutOfRangeException(nameof(payloadKind), "Typed payload profile frames require a single defined payload kind bit.");
            }

            PayloadKind = payloadKind;
            ProfileId = profileId;
            Frames = frames.IsEmpty ? ReadOnlyMemory<TypedPayloadFrameView>.Empty : frames;

            var payloadBytes = 0;
            foreach (var frame in Frames.Span)
            {
                if (frame.PayloadKind != payloadKind || frame.ProfileId != profileId)
                {
                    throw new ArgumentException("All typed payload frames must match the requested payload kind and profile id.", nameof(frames));
                }

                payloadBytes = checked(payloadBytes + frame.Payload.Length);
            }

            PayloadBytes = payloadBytes;
        }

        public PayloadKind PayloadKind { get; }

        public ushort ProfileId { get; }

        public ReadOnlyMemory<TypedPayloadFrameView> Frames { get; }

        public int FrameCount => Frames.Length;

        public int PayloadBytes { get; }

        public bool IsEmpty => Frames.IsEmpty;

        public bool Equals(TypedPayloadProfileFrames other)
        {
            return PayloadKind == other.PayloadKind
                && ProfileId == other.ProfileId
                && Frames.Span.SequenceEqual(other.Frames.Span);
        }

        public override bool Equals(object obj)
        {
            return obj is TypedPayloadProfileFrames other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = (int)PayloadKind;
                hash = (hash * 397) ^ ProfileId.GetHashCode();
                hash = (hash * 397) ^ Frames.Length.GetHashCode();
                hash = (hash * 397) ^ PayloadBytes;
                return hash;
            }
        }
    }
}
