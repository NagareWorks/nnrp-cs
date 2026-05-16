using System;

namespace Nnrp.Core
{
    public readonly struct InlineObjectBlockHeader : IEquatable<InlineObjectBlockHeader>
    {
        public const int HeaderLength = 16;

        public InlineObjectBlockHeader(
            CacheObjectKind objectKind,
            ushort objectFlags,
            ushort profileId,
            ushort reserved0,
            uint objectBytes,
            uint reserved1 = 0)
        {
            ObjectKind = objectKind;
            ObjectFlags = objectFlags;
            ProfileId = profileId;
            Reserved0 = reserved0;
            ObjectBytes = objectBytes;
            Reserved1 = reserved1;
        }

        public CacheObjectKind ObjectKind { get; }

        public ushort ObjectFlags { get; }

        public ushort ProfileId { get; }

        public ushort Reserved0 { get; }

        public uint ObjectBytes { get; }

        public uint Reserved1 { get; }

        public uint GetAlignedBlockLength()
        {
            return checked((uint)BinaryAlignment.AlignUp(checked(HeaderLength + (int)ObjectBytes)));
        }

        public void Write(Span<byte> destination)
        {
            if (!TryWrite(destination, out _))
            {
                throw new ArgumentException($"Destination must be at least {HeaderLength} bytes.", nameof(destination));
            }
        }

        public bool TryWrite(Span<byte> destination, out int bytesWritten)
        {
            bytesWritten = 0;
            if (destination.Length < HeaderLength)
            {
                return false;
            }

            var writer = new FixedBinaryWriter(destination);
            if (!writer.TryWriteUInt16(checked((ushort)ObjectKind))
                || !writer.TryWriteUInt16(ObjectFlags)
                || !writer.TryWriteUInt16(ProfileId)
                || !writer.TryWriteUInt16(Reserved0)
                || !writer.TryWriteUInt32(ObjectBytes)
                || !writer.TryWriteUInt32(Reserved1))
            {
                return false;
            }

            bytesWritten = writer.Offset;
            return bytesWritten == HeaderLength;
        }

        public byte[] ToArray()
        {
            var payload = new byte[HeaderLength];
            Write(payload);
            return payload;
        }

        public static bool TryParse(ReadOnlySpan<byte> source, out InlineObjectBlockHeader header)
        {
            return TryParse(source, strict: false, out header, out _);
        }

        public static bool TryParse(ReadOnlySpan<byte> source, bool strict, out InlineObjectBlockHeader header, out NnrpParseError error)
        {
            header = default;
            error = NnrpParseError.None;
            if (source.Length < HeaderLength)
            {
                error = NnrpParseError.SourceTooShort;
                return false;
            }

            var reader = new FixedBinaryReader(source);
            if (!reader.TryReadUInt16(out var objectKind)
                || !reader.TryReadUInt16(out var objectFlags)
                || !reader.TryReadUInt16(out var profileId)
                || !reader.TryReadUInt16(out var reserved0)
                || !reader.TryReadUInt32(out var objectBytes)
                || !reader.TryReadUInt32(out var reserved1))
            {
                error = NnrpParseError.SourceTooShort;
                return false;
            }

            if (strict && (objectFlags != 0 || reserved0 != 0 || reserved1 != 0))
            {
                error = NnrpParseError.NonZeroReservedField;
                return false;
            }

            header = new InlineObjectBlockHeader((CacheObjectKind)objectKind, objectFlags, profileId, reserved0, objectBytes, reserved1);
            return true;
        }

        public bool Equals(InlineObjectBlockHeader other)
        {
            return ObjectKind == other.ObjectKind
                && ObjectFlags == other.ObjectFlags
                && ProfileId == other.ProfileId
                && Reserved0 == other.Reserved0
                && ObjectBytes == other.ObjectBytes
                && Reserved1 == other.Reserved1;
        }

        public override bool Equals(object obj)
        {
            return obj is InlineObjectBlockHeader other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = ObjectKind.GetHashCode();
                hash = (hash * 397) ^ ObjectFlags.GetHashCode();
                hash = (hash * 397) ^ ProfileId.GetHashCode();
                hash = (hash * 397) ^ Reserved0.GetHashCode();
                hash = (hash * 397) ^ ObjectBytes.GetHashCode();
                hash = (hash * 397) ^ Reserved1.GetHashCode();
                return hash;
            }
        }
    }
}
