using System;

namespace Nnrp.Core
{
    public readonly struct ObjectReferenceBlock : IEquatable<ObjectReferenceBlock>
    {
        public const int BlockLength = 24;

        public ObjectReferenceBlock(
            CacheObjectKind objectKind,
            ushort referenceFlags,
            uint cacheNamespace,
            ulong cacheKeyHigh,
            ulong cacheKeyLow)
        {
            if (!Enum.IsDefined(typeof(CacheObjectKind), objectKind))
            {
                throw new ArgumentOutOfRangeException(nameof(objectKind));
            }

            if (referenceFlags != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(referenceFlags));
            }

            ObjectKind = objectKind;
            ReferenceFlags = referenceFlags;
            CacheNamespace = cacheNamespace;
            CacheKeyHigh = cacheKeyHigh;
            CacheKeyLow = cacheKeyLow;
        }

        public CacheObjectKind ObjectKind { get; }

        public ushort ReferenceFlags { get; }

        public uint CacheNamespace { get; }

        public ulong CacheKeyHigh { get; }

        public ulong CacheKeyLow { get; }

        public void Write(Span<byte> destination)
        {
            if (!TryWrite(destination, out _))
            {
                throw new ArgumentException($"Destination must be at least {BlockLength} bytes.", nameof(destination));
            }
        }

        public bool TryWrite(Span<byte> destination, out int bytesWritten)
        {
            bytesWritten = 0;
            if (destination.Length < BlockLength)
            {
                return false;
            }

            var writer = new FixedBinaryWriter(destination);
            if (!writer.TryWriteUInt16(checked((ushort)ObjectKind))
                || !writer.TryWriteUInt16(ReferenceFlags)
                || !writer.TryWriteUInt32(CacheNamespace)
                || !writer.TryWriteUInt64(CacheKeyHigh)
                || !writer.TryWriteUInt64(CacheKeyLow))
            {
                return false;
            }

            bytesWritten = writer.Offset;
            return bytesWritten == BlockLength;
        }

        public byte[] ToArray()
        {
            var payload = new byte[BlockLength];
            Write(payload);
            return payload;
        }

        public static bool TryParse(ReadOnlySpan<byte> source, out ObjectReferenceBlock block)
        {
            return TryParse(source, out block, out _);
        }

        public static bool TryParse(ReadOnlySpan<byte> source, out ObjectReferenceBlock block, out NnrpParseError error)
        {
            block = default;
            error = NnrpParseError.None;
            if (source.Length < BlockLength)
            {
                error = NnrpParseError.SourceTooShort;
                return false;
            }

            var reader = new FixedBinaryReader(source);
            if (!reader.TryReadUInt16(out var objectKind)
                || !reader.TryReadUInt16(out var referenceFlags)
                || !reader.TryReadUInt32(out var cacheNamespace)
                || !reader.TryReadUInt64(out var cacheKeyHigh)
                || !reader.TryReadUInt64(out var cacheKeyLow))
            {
                error = NnrpParseError.SourceTooShort;
                return false;
            }

            if (referenceFlags != 0 || !Enum.IsDefined(typeof(CacheObjectKind), (uint)objectKind))
            {
                error = referenceFlags != 0
                    ? NnrpParseError.NonZeroReservedField
                    : NnrpParseError.InvalidMessageLayout;
                return false;
            }

            block = new ObjectReferenceBlock((CacheObjectKind)objectKind, referenceFlags, cacheNamespace, cacheKeyHigh, cacheKeyLow);
            return true;
        }

        public bool Equals(ObjectReferenceBlock other)
        {
            return ObjectKind == other.ObjectKind
                && ReferenceFlags == other.ReferenceFlags
                && CacheNamespace == other.CacheNamespace
                && CacheKeyHigh == other.CacheKeyHigh
                && CacheKeyLow == other.CacheKeyLow;
        }

        public override bool Equals(object obj)
        {
            return obj is ObjectReferenceBlock other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = ObjectKind.GetHashCode();
                hash = (hash * 397) ^ ReferenceFlags.GetHashCode();
                hash = (hash * 397) ^ CacheNamespace.GetHashCode();
                hash = (hash * 397) ^ CacheKeyHigh.GetHashCode();
                hash = (hash * 397) ^ CacheKeyLow.GetHashCode();
                return hash;
            }
        }
    }
}
