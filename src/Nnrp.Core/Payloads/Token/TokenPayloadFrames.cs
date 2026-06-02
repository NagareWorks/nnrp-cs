using System;

namespace Nnrp.Core
{
    public readonly struct TokenPayloadFrames : IEquatable<TokenPayloadFrames>
    {
        public TokenPayloadFrames(ReadOnlyMemory<TokenPayloadFrameView> frames)
        {
            Frames = frames.IsEmpty ? ReadOnlyMemory<TokenPayloadFrameView>.Empty : frames;

            var payloadBytes = 0;
            foreach (var frame in Frames.Span)
            {
                if (!TokenPayloadDescriptor.IsTokenDescriptor(frame.TypedDescriptor)
                    || frame.PayloadLength != frame.Payload.Length)
                {
                    throw new ArgumentException("Token payload frame sets require valid token payload frames.", nameof(frames));
                }

                payloadBytes = checked(payloadBytes + frame.Payload.Length);
            }

            PayloadBytes = payloadBytes;
        }

        public ReadOnlyMemory<TokenPayloadFrameView> Frames { get; }

        public int FrameCount => Frames.Length;

        public int PayloadBytes { get; }

        public bool IsEmpty => Frames.IsEmpty;

        public static TokenPayloadFrames FromTypedPayloadFrames(TypedPayloadProfileFrames frames)
        {
            if (frames.PayloadKind != PayloadKind.TokenChunk || !frames.Profile.Equals(TypedPayloadProfileId.Token))
            {
                throw new ArgumentException("Token payload frames require token profile and token payload kind.", nameof(frames));
            }

            if (frames.Frames.IsEmpty)
            {
                return new TokenPayloadFrames(ReadOnlyMemory<TokenPayloadFrameView>.Empty);
            }

            var tokenFrames = new TokenPayloadFrameView[frames.Frames.Length];
            for (var index = 0; index < tokenFrames.Length; index++)
            {
                tokenFrames[index] = new TokenPayloadFrameView(frames.Frames.Span[index]);
            }

            return new TokenPayloadFrames(tokenFrames);
        }

        public bool Equals(TokenPayloadFrames other)
        {
            return Frames.Span.SequenceEqual(other.Frames.Span);
        }

        public override bool Equals(object obj)
        {
            return obj is TokenPayloadFrames other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = Frames.Length.GetHashCode();
                hash = (hash * 397) ^ PayloadBytes.GetHashCode();
                return hash;
            }
        }
    }
}
