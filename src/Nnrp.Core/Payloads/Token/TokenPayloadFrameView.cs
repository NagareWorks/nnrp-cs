using System;

namespace Nnrp.Core
{
    public readonly struct TokenPayloadFrameView : IEquatable<TokenPayloadFrameView>
    {
        public TokenPayloadFrameView(TokenPayloadDescriptor descriptor, ReadOnlyMemory<byte> payload)
        {
            if (descriptor.PayloadLength != payload.Length)
            {
                throw new ArgumentException("Token payload length must match the descriptor payload length.", nameof(payload));
            }

            Descriptor = descriptor;
            Payload = payload;
        }

        public TokenPayloadFrameView(TypedPayloadFrameView frame)
            : this(new TokenPayloadDescriptor(frame.Descriptor), frame.Payload)
        {
        }

        public TokenPayloadDescriptor Descriptor { get; }

        public ReadOnlyMemory<byte> Payload { get; }

        public TypedPayloadDescriptor TypedDescriptor => Descriptor.Descriptor;

        public uint PayloadOffset => Descriptor.PayloadOffset;

        public uint PayloadLength => Descriptor.PayloadLength;

        public bool IsPartial => Descriptor.IsPartial;

        public bool IsTerminal => Descriptor.IsTerminal;

        public static bool TryFromFrame(TypedPayloadFrameView frame, out TokenPayloadFrameView tokenFrame)
        {
            if (TokenPayloadDescriptor.TryFromDescriptor(frame.Descriptor, out var descriptor)
                && descriptor.PayloadLength == frame.Payload.Length)
            {
                tokenFrame = new TokenPayloadFrameView(descriptor, frame.Payload);
                return true;
            }

            tokenFrame = default;
            return false;
        }

        public TypedPayloadFrameView ToTypedPayloadFrameView()
        {
            return new TypedPayloadFrameView(Descriptor.Descriptor, Payload);
        }

        public bool Equals(TokenPayloadFrameView other)
        {
            return Descriptor.Equals(other.Descriptor)
                && Payload.Span.SequenceEqual(other.Payload.Span);
        }

        public override bool Equals(object obj)
        {
            return obj is TokenPayloadFrameView other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = Descriptor.GetHashCode();
                hash = (hash * 397) ^ Payload.Length.GetHashCode();
                return hash;
            }
        }
    }
}
