using System;

namespace Nnrp.Core
{
    public readonly struct CacheInvalidateMetadata : IEquatable<CacheInvalidateMetadata>
    {
        public const int MetadataLength = 5 * sizeof(uint);

        public CacheInvalidateMetadata(
            CacheInvalidateScope invalidateScope,
            uint cacheNamespace,
            uint cacheKeyHigh,
            uint cacheKeyLow,
            uint reasonCode)
        {
            if (!Enum.IsDefined(typeof(CacheInvalidateScope), invalidateScope))
            {
                throw new ArgumentOutOfRangeException(nameof(invalidateScope));
            }

            InvalidateScope = invalidateScope;
            CacheNamespace = cacheNamespace;
            CacheKeyHigh = cacheKeyHigh;
            CacheKeyLow = cacheKeyLow;
            ReasonCode = reasonCode;
        }

        public CacheInvalidateScope InvalidateScope { get; }
        public uint CacheNamespace { get; }
        public uint CacheKeyHigh { get; }
        public uint CacheKeyLow { get; }
        public uint ReasonCode { get; }

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

            if (!TryGetWireInvalidateScope(InvalidateScope, out var wireInvalidateScope))
            {
                return false;
            }

            var writer = new FixedBinaryWriter(destination);
            if (!writer.TryWriteUInt32(wireInvalidateScope)
                || !writer.TryWriteUInt32(CacheNamespace)
                || !writer.TryWriteUInt32(CacheKeyHigh)
                || !writer.TryWriteUInt32(CacheKeyLow)
                || !writer.TryWriteUInt32(ReasonCode))
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

        public static bool TryParse(ReadOnlySpan<byte> source, out CacheInvalidateMetadata metadata)
        {
            return TryParse(source, out metadata, out _);
        }

        public static bool TryParse(ReadOnlySpan<byte> source, out CacheInvalidateMetadata metadata, out NnrpParseError error)
        {
            metadata = default;
            error = NnrpParseError.None;
            if (source.Length < MetadataLength)
            {
                error = NnrpParseError.SourceTooShort;
                return false;
            }

            var reader = new FixedBinaryReader(source);
            if (!reader.TryReadUInt32(out var invalidateScope)
                || !reader.TryReadUInt32(out var cacheNamespace)
                || !reader.TryReadUInt32(out var cacheKeyHigh)
                || !reader.TryReadUInt32(out var cacheKeyLow)
                || !reader.TryReadUInt32(out var reasonCode))
            {
                error = NnrpParseError.SourceTooShort;
                return false;
            }

            if (!TryGetInvalidateScopeFromWire(invalidateScope, out var parsedInvalidateScope))
            {
                error = NnrpParseError.InvalidMessageLayout;
                return false;
            }

            metadata = new CacheInvalidateMetadata(
                parsedInvalidateScope,
                cacheNamespace,
                cacheKeyHigh,
                cacheKeyLow,
                reasonCode);
            return true;
        }

        private static bool TryGetWireInvalidateScope(CacheInvalidateScope invalidateScope, out uint wireInvalidateScope)
        {
            wireInvalidateScope = (uint)invalidateScope;
            return Enum.IsDefined(typeof(CacheInvalidateScope), invalidateScope);
        }

        private static bool TryGetInvalidateScopeFromWire(uint wireInvalidateScope, out CacheInvalidateScope invalidateScope)
        {
            if (Enum.IsDefined(typeof(CacheInvalidateScope), wireInvalidateScope))
            {
                invalidateScope = (CacheInvalidateScope)wireInvalidateScope;
                return true;
            }

            invalidateScope = default;
            return false;
        }

        public bool Equals(CacheInvalidateMetadata other)
        {
            return InvalidateScope == other.InvalidateScope
                && CacheNamespace == other.CacheNamespace
                && CacheKeyHigh == other.CacheKeyHigh
                && CacheKeyLow == other.CacheKeyLow
                && ReasonCode == other.ReasonCode;
        }

        public override bool Equals(object obj)
        {
            return obj is CacheInvalidateMetadata other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = InvalidateScope.GetHashCode();
                hash = (hash * 397) ^ CacheNamespace.GetHashCode();
                hash = (hash * 397) ^ CacheKeyHigh.GetHashCode();
                hash = (hash * 397) ^ CacheKeyLow.GetHashCode();
                hash = (hash * 397) ^ ReasonCode.GetHashCode();
                return hash;
            }
        }
    }
}
