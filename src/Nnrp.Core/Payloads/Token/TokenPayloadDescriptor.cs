using System;

namespace Nnrp.Core
{
    public readonly struct TokenPayloadDescriptor : IEquatable<TokenPayloadDescriptor>
    {
        public const uint DeltaSchemaId = TypedPayloadDescriptor.TokenDeltaSchemaId;
        public const uint DeltaSchemaVersion = TypedPayloadDescriptor.TokenDeltaSchemaVersion;
        public const ushort DeltaStreamSemantics = TypedPayloadDescriptor.StreamSemanticsAppend;

        public TokenPayloadDescriptor(
            uint schemaId,
            uint schemaVersion,
            ushort streamSemantics,
            uint payloadOffset,
            uint payloadLength,
            TypedPayloadDescriptorFlags flags = TypedPayloadDescriptorFlags.None)
            : this(new TypedPayloadDescriptor(
                  PayloadKind.TokenChunk,
                  TypedPayloadProfileId.Token,
                  (ushort)flags,
                  schemaId,
                  schemaVersion,
                  streamSemantics,
                  payloadOffset,
                  payloadLength))
        {
        }

        public TokenPayloadDescriptor(TypedPayloadDescriptor descriptor)
        {
            if (!IsTokenDescriptor(descriptor))
            {
                throw new ArgumentException("Token payload descriptors require token profile and token payload kind.", nameof(descriptor));
            }

            Descriptor = descriptor;
        }

        public TypedPayloadDescriptor Descriptor { get; }

        public TypedPayloadProfileId Profile => Descriptor.Profile;

        public PayloadKind PayloadKind => Descriptor.PayloadKind;

        public TypedPayloadDescriptorFlags Flags => (TypedPayloadDescriptorFlags)Descriptor.DescriptorFlags;

        public ushort DescriptorFlags => Descriptor.DescriptorFlags;

        public uint SchemaId => Descriptor.SchemaId;

        public uint SchemaVersion => Descriptor.SchemaVersion;

        public ushort StreamSemantics => Descriptor.StreamSemantics;

        public uint PayloadOffset => Descriptor.PayloadOffset;

        public uint PayloadLength => Descriptor.PayloadLength;

        public bool IsTerminal => (Flags & TypedPayloadDescriptorFlags.Terminal) != 0;

        public bool IsPartial => (Flags & TypedPayloadDescriptorFlags.Partial) != 0;

        public bool HasSchemaOverride => (Flags & TypedPayloadDescriptorFlags.SchemaOverride) != 0;

        public bool HasProfileHint => (Flags & TypedPayloadDescriptorFlags.ProfileHintPresent) != 0;

        public bool IsStandardDeltaSchema =>
            SchemaId == DeltaSchemaId
            && SchemaVersion == DeltaSchemaVersion
            && StreamSemantics == DeltaStreamSemantics;

        public static TokenPayloadDescriptor CreateDelta(
            uint payloadOffset,
            uint payloadLength,
            TypedPayloadDescriptorFlags flags = TypedPayloadDescriptorFlags.Partial)
        {
            return new TokenPayloadDescriptor(
                DeltaSchemaId,
                DeltaSchemaVersion,
                DeltaStreamSemantics,
                payloadOffset,
                payloadLength,
                flags);
        }

        public static bool TryFromDescriptor(TypedPayloadDescriptor descriptor, out TokenPayloadDescriptor tokenDescriptor)
        {
            if (IsTokenDescriptor(descriptor))
            {
                tokenDescriptor = new TokenPayloadDescriptor(descriptor);
                return true;
            }

            tokenDescriptor = default;
            return false;
        }

        public static bool IsTokenDescriptor(TypedPayloadDescriptor descriptor)
        {
            return descriptor.PayloadKind == PayloadKind.TokenChunk
                && descriptor.Profile.Equals(TypedPayloadProfileId.Token)
                && (descriptor.DescriptorFlags & ~TypedPayloadDescriptor.KnownDescriptorFlagMask) == 0
                && descriptor.Reserved0 == 0;
        }

        public bool Equals(TokenPayloadDescriptor other)
        {
            return Descriptor.Equals(other.Descriptor);
        }

        public override bool Equals(object obj)
        {
            return obj is TokenPayloadDescriptor other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Descriptor.GetHashCode();
        }
    }
}
