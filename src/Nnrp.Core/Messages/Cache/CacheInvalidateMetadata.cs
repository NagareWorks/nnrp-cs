using System;

namespace Nnrp.Core
{
    public readonly struct CacheInvalidateMetadata : IEquatable<CacheInvalidateMetadata>
    {
        public const int MetadataLength = 32;

        public CacheInvalidateMetadata(
            CacheInvalidateScope invalidateScope,
            uint cacheNamespace,
            ulong cacheKeyHigh,
            ulong cacheKeyLow,
            uint reasonCode)
        {
            if (!Enum.IsDefined(typeof(CacheInvalidateScope), invalidateScope))
            {
                throw new ArgumentOutOfRangeException(nameof(invalidateScope));
            }

            if (!HasValidIdentityForScope(invalidateScope, cacheNamespace, cacheKeyHigh, cacheKeyLow))
            {
                throw new ArgumentException("Cache identity fields must match invalidateScope.", nameof(invalidateScope));
            }

            InvalidateScope = invalidateScope;
            CacheNamespace = cacheNamespace;
            CacheKeyHigh = cacheKeyHigh;
            CacheKeyLow = cacheKeyLow;
            ReasonCode = reasonCode;
        }

        public CacheInvalidateScope InvalidateScope { get; }
        public uint CacheNamespace { get; }
        public ulong CacheKeyHigh { get; }
        public ulong CacheKeyLow { get; }
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

            var writer = new FixedBinaryWriter(destination);
            if (!writer.TryWriteUInt32((uint)InvalidateScope)
                || !writer.TryWriteUInt32(CacheNamespace)
                || !writer.TryWriteUInt64(CacheKeyHigh)
                || !writer.TryWriteUInt64(CacheKeyLow)
                || !writer.TryWriteUInt32(ReasonCode)
                || !writer.TryWriteUInt32(0))
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
                || !reader.TryReadUInt64(out var cacheKeyHigh)
                || !reader.TryReadUInt64(out var cacheKeyLow)
                || !reader.TryReadUInt32(out var reasonCode)
                || !reader.TryReadUInt32(out var reserved))
            {
                error = NnrpParseError.SourceTooShort;
                return false;
            }

            if (!TryGetInvalidateScopeFromWire(invalidateScope, out var parsedInvalidateScope))
            {
                error = NnrpParseError.InvalidMessageLayout;
                return false;
            }

            if (reserved != 0)
            {
                error = NnrpParseError.NonZeroReservedField;
                return false;
            }

            if (!HasValidIdentityForScope(parsedInvalidateScope, cacheNamespace, cacheKeyHigh, cacheKeyLow))
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

        private static bool HasValidIdentityForScope(
            CacheInvalidateScope invalidateScope,
            uint cacheNamespace,
            ulong cacheKeyHigh,
            ulong cacheKeyLow)
        {
            if (invalidateScope == CacheInvalidateScope.WholeSession)
            {
                return cacheNamespace == 0 && cacheKeyHigh == 0 && cacheKeyLow == 0;
            }

            if (invalidateScope == CacheInvalidateScope.Namespace)
            {
                return cacheKeyHigh == 0 && cacheKeyLow == 0;
            }

            if (invalidateScope == CacheInvalidateScope.ObjectKind)
            {
                return cacheKeyHigh <= uint.MaxValue && cacheKeyLow == 0;
            }

            return true;
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
