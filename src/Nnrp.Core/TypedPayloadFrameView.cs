using System;

namespace Nnrp.Core
{
    public readonly struct TypedPayloadFrameView : IEquatable<TypedPayloadFrameView>
    {
        public TypedPayloadFrameView(TypedPayloadDescriptor descriptor, ReadOnlyMemory<byte> payload)
        {
            Descriptor = descriptor;
            Payload = payload;
        }

        public TypedPayloadDescriptor Descriptor { get; }

        public ReadOnlyMemory<byte> Payload { get; }

        public PayloadKind PayloadKind => Descriptor.PayloadKind;

        public ushort ProfileId => Descriptor.ProfileId;

        public bool Equals(TypedPayloadFrameView other)
        {
            return Descriptor.Equals(other.Descriptor)
                && Payload.Span.SequenceEqual(other.Payload.Span);
        }

        public override bool Equals(object obj)
        {
            return obj is TypedPayloadFrameView other && Equals(other);
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
