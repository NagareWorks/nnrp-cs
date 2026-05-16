using System;

namespace Nnrp.Core
{
    public readonly struct CachePutMetadata : IEquatable<CachePutMetadata>
    {
        public const int MetadataLength = 8 * sizeof(uint);

        public CachePutMetadata(
            uint cacheNamespace,
            uint cacheKeyHigh,
            uint cacheKeyLow,
            CacheObjectKind objectKind,
            uint ttlMilliseconds,
            uint objectBytes,
            uint codecBitmap,
            CachePutFlags flags = CachePutFlags.None)
        {
            if (!Enum.IsDefined(typeof(CacheObjectKind), objectKind))
            {
                throw new ArgumentOutOfRangeException(nameof(objectKind));
            }

            if (((uint)flags & ~((uint)CachePutFlags.Pinned | (uint)CachePutFlags.Reusable)) != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(flags));
            }

            CacheNamespace = cacheNamespace;
            CacheKeyHigh = cacheKeyHigh;
            CacheKeyLow = cacheKeyLow;
            ObjectKind = objectKind;
            TtlMilliseconds = ttlMilliseconds;
            ObjectBytes = objectBytes;
            CodecBitmap = codecBitmap;
            Flags = flags;
        }

        public uint CacheNamespace { get; }
        public uint CacheKeyHigh { get; }
        public uint CacheKeyLow { get; }
        public CacheObjectKind ObjectKind { get; }
        public uint TtlMilliseconds { get; }
        public uint ObjectBytes { get; }
        public uint CodecBitmap { get; }
        public CachePutFlags Flags { get; }

        public void Write(Span<byte> destination)
        {
            if (!TryWrite(destination, out _))
            {
                throw new ArgumentException($"Destination must be at least {MetadataLength} bytes.", nameof(destination));
            }
        }

        public bool TryWrite(Span<byte> destination, out int bytesWritten)
        {
            bytesWritten = 0;
            if (destination.Length < MetadataLength)
            {
                return false;
            }

            if (!TryGetWireObjectKind(ObjectKind, out var wireObjectKind))
            {
                return false;
            }

            var writer = new FixedBinaryWriter(destination);
            if (!writer.TryWriteUInt32(CacheNamespace)
                || !writer.TryWriteUInt32(CacheKeyHigh)
                || !writer.TryWriteUInt32(CacheKeyLow)
                || !writer.TryWriteUInt32(wireObjectKind)
                || !writer.TryWriteUInt32(TtlMilliseconds)
                || !writer.TryWriteUInt32(ObjectBytes)
                || !writer.TryWriteUInt32(CodecBitmap)
                || !writer.TryWriteUInt32((uint)Flags))
            {
                return false;
            }

            bytesWritten = writer.Offset;
            return bytesWritten == MetadataLength;
        }

        public byte[] ToArray()
        {
            var payload = new byte[MetadataLength];
            Write(payload);
            return payload;
        }

        public static bool TryParse(ReadOnlySpan<byte> source, out CachePutMetadata metadata)
        {
            return TryParse(source, out metadata, out _);
        }

        public static bool TryParse(ReadOnlySpan<byte> source, out CachePutMetadata metadata, out NnrpParseError error)
        {
            metadata = default;
            error = NnrpParseError.None;
            if (source.Length < MetadataLength)
            {
                error = NnrpParseError.SourceTooShort;
                return false;
            }

            var reader = new FixedBinaryReader(source);
            if (!reader.TryReadUInt32(out var cacheNamespace)
                || !reader.TryReadUInt32(out var cacheKeyHigh)
                || !reader.TryReadUInt32(out var cacheKeyLow)
                || !reader.TryReadUInt32(out var objectKind)
                || !reader.TryReadUInt32(out var ttlMilliseconds)
                || !reader.TryReadUInt32(out var objectBytes)
                || !reader.TryReadUInt32(out var codecBitmap)
                || !reader.TryReadUInt32(out var flags))
            {
                error = NnrpParseError.SourceTooShort;
                return false;
            }

            if (!TryGetObjectKindFromWire(objectKind, out var parsedObjectKind)
                || ((uint)flags & ~((uint)CachePutFlags.Pinned | (uint)CachePutFlags.Reusable)) != 0)
            {
                error = NnrpParseError.InvalidMessageLayout;
                return false;
            }

            metadata = new CachePutMetadata(
                cacheNamespace,
                cacheKeyHigh,
                cacheKeyLow,
                parsedObjectKind,
                ttlMilliseconds,
                objectBytes,
                codecBitmap,
                (CachePutFlags)flags);
            return true;
        }

        private static bool TryGetWireObjectKind(CacheObjectKind objectKind, out uint wireObjectKind)
        {
            wireObjectKind = (uint)objectKind;
            return Enum.IsDefined(typeof(CacheObjectKind), objectKind);
        }

        private static bool TryGetObjectKindFromWire(uint wireObjectKind, out CacheObjectKind objectKind)
        {
            if (Enum.IsDefined(typeof(CacheObjectKind), wireObjectKind))
            {
                objectKind = (CacheObjectKind)wireObjectKind;
                return true;
            }

            objectKind = default;
            return false;
        }

        public bool Equals(CachePutMetadata other)
        {
            return CacheNamespace == other.CacheNamespace
                && CacheKeyHigh == other.CacheKeyHigh
                && CacheKeyLow == other.CacheKeyLow
                && ObjectKind == other.ObjectKind
                && TtlMilliseconds == other.TtlMilliseconds
                && ObjectBytes == other.ObjectBytes
                && CodecBitmap == other.CodecBitmap
                && Flags == other.Flags;
        }

        public override bool Equals(object obj)
        {
            return obj is CachePutMetadata other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = CacheNamespace.GetHashCode();
                hash = (hash * 397) ^ CacheKeyHigh.GetHashCode();
                hash = (hash * 397) ^ CacheKeyLow.GetHashCode();
                hash = (hash * 397) ^ ObjectKind.GetHashCode();
                hash = (hash * 397) ^ TtlMilliseconds.GetHashCode();
                hash = (hash * 397) ^ ObjectBytes.GetHashCode();
                hash = (hash * 397) ^ CodecBitmap.GetHashCode();
                hash = (hash * 397) ^ Flags.GetHashCode();
                return hash;
            }
        }
    }
}
