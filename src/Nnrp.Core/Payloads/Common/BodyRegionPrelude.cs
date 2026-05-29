using System;

namespace Nnrp.Core
{
    public readonly struct BodyRegionPrelude : IEquatable<BodyRegionPrelude>
    {
        public const int PreludeLength = 8 * sizeof(uint);

        public BodyRegionPrelude(
            uint inlineObjectBytes,
            uint objectReferenceBytes,
            uint typedPayloadDescriptorBytes,
            uint typedPayloadFrameBytes,
            uint extensionDescriptorBytes,
            uint extensionPayloadBytes,
            uint bodyFlags,
            uint reserved = 0)
        {
            InlineObjectBytes = inlineObjectBytes;
            ObjectReferenceBytes = objectReferenceBytes;
            TypedPayloadDescriptorBytes = typedPayloadDescriptorBytes;
            TypedPayloadFrameBytes = typedPayloadFrameBytes;
            ExtensionDescriptorBytes = extensionDescriptorBytes;
            ExtensionPayloadBytes = extensionPayloadBytes;
            BodyFlags = bodyFlags;
            Reserved = reserved;
        }

        public uint InlineObjectBytes { get; }

        public uint ObjectReferenceBytes { get; }

        public uint TypedPayloadDescriptorBytes { get; }

        public uint TypedPayloadFrameBytes { get; }

        public uint ExtensionDescriptorBytes { get; }

        public uint ExtensionPayloadBytes { get; }

        public uint BodyFlags { get; }

        public uint Reserved { get; }

        public uint GetTotalBodyBytes()
        {
            checked
            {
                return (uint)(
                    PreludeLength
                    + InlineObjectBytes
                    + ObjectReferenceBytes
                    + TypedPayloadDescriptorBytes
                    + TypedPayloadFrameBytes
                    + ExtensionDescriptorBytes
                    + ExtensionPayloadBytes);
            }
        }

        public void Write(Span<byte> destination)
        {
            if (!TryWrite(destination, out _))
            {
                throw new ArgumentException($"Destination must be at least {PreludeLength} bytes.", nameof(destination));
            }
        }

        public bool TryWrite(Span<byte> destination, out int bytesWritten)
        {
            bytesWritten = 0;
            if (destination.Length < PreludeLength)
            {
                return false;
            }

            var writer = new FixedBinaryWriter(destination);
            if (!writer.TryWriteUInt32(InlineObjectBytes)
                || !writer.TryWriteUInt32(ObjectReferenceBytes)
                || !writer.TryWriteUInt32(TypedPayloadDescriptorBytes)
                || !writer.TryWriteUInt32(TypedPayloadFrameBytes)
                || !writer.TryWriteUInt32(ExtensionDescriptorBytes)
                || !writer.TryWriteUInt32(ExtensionPayloadBytes)
                || !writer.TryWriteUInt32(BodyFlags)
                || !writer.TryWriteUInt32(Reserved))
            {
                return false;
            }

            bytesWritten = writer.Offset;
            return bytesWritten == PreludeLength;
        }

        public byte[] ToArray()
        {
            var payload = new byte[PreludeLength];
            Write(payload);
            return payload;
        }

        public static bool TryParse(ReadOnlySpan<byte> source, out BodyRegionPrelude prelude)
        {
            return TryParse(source, strict: false, out prelude, out _);
        }

        public static bool TryParse(ReadOnlySpan<byte> source, bool strict, out BodyRegionPrelude prelude, out NnrpParseError error)
        {
            prelude = default;
            error = NnrpParseError.None;
            if (source.Length < PreludeLength)
            {
                error = NnrpParseError.SourceTooShort;
                return false;
            }

            var reader = new FixedBinaryReader(source);
            if (!reader.TryReadUInt32(out var inlineObjectBytes)
                || !reader.TryReadUInt32(out var objectReferenceBytes)
                || !reader.TryReadUInt32(out var typedPayloadDescriptorBytes)
                || !reader.TryReadUInt32(out var typedPayloadFrameBytes)
                || !reader.TryReadUInt32(out var extensionDescriptorBytes)
                || !reader.TryReadUInt32(out var extensionPayloadBytes)
                || !reader.TryReadUInt32(out var bodyFlags)
                || !reader.TryReadUInt32(out var reserved))
            {
                error = NnrpParseError.SourceTooShort;
                return false;
            }

            if (strict && reserved != 0)
            {
                error = NnrpParseError.NonZeroReservedField;
                return false;
            }

            prelude = new BodyRegionPrelude(
                inlineObjectBytes,
                objectReferenceBytes,
                typedPayloadDescriptorBytes,
                typedPayloadFrameBytes,
                extensionDescriptorBytes,
                extensionPayloadBytes,
                bodyFlags,
                reserved);
            return true;
        }

        public bool Equals(BodyRegionPrelude other)
        {
            return InlineObjectBytes == other.InlineObjectBytes
                && ObjectReferenceBytes == other.ObjectReferenceBytes
                && TypedPayloadDescriptorBytes == other.TypedPayloadDescriptorBytes
                && TypedPayloadFrameBytes == other.TypedPayloadFrameBytes
                && ExtensionDescriptorBytes == other.ExtensionDescriptorBytes
                && ExtensionPayloadBytes == other.ExtensionPayloadBytes
                && BodyFlags == other.BodyFlags
                && Reserved == other.Reserved;
        }

        public override bool Equals(object obj)
        {
            return obj is BodyRegionPrelude other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = InlineObjectBytes.GetHashCode();
                hash = (hash * 397) ^ ObjectReferenceBytes.GetHashCode();
                hash = (hash * 397) ^ TypedPayloadDescriptorBytes.GetHashCode();
                hash = (hash * 397) ^ TypedPayloadFrameBytes.GetHashCode();
                hash = (hash * 397) ^ ExtensionDescriptorBytes.GetHashCode();
                hash = (hash * 397) ^ ExtensionPayloadBytes.GetHashCode();
                hash = (hash * 397) ^ BodyFlags.GetHashCode();
                hash = (hash * 397) ^ Reserved.GetHashCode();
                return hash;
            }
        }
    }
}
