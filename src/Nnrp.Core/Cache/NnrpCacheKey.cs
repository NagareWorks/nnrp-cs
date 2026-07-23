using System;

namespace Nnrp.Core
{
    /// <summary>
    /// A cache key that matches the current cache-key wire layout.
    /// </summary>
    public readonly struct NnrpCacheKey : IEquatable<NnrpCacheKey>
    {
        public NnrpCacheKey(uint namespaceId, ulong keyHigh, ulong keyLow)
        {
            NamespaceId = namespaceId;
            KeyHigh = keyHigh;
            KeyLow = keyLow;
        }

        public uint NamespaceId { get; }
        public ulong KeyHigh { get; }
        public ulong KeyLow { get; }

        public bool Equals(NnrpCacheKey other)
        {
            return NamespaceId == other.NamespaceId
                   && KeyHigh == other.KeyHigh
                   && KeyLow == other.KeyLow;
        }

        public override bool Equals(object? obj) => obj is NnrpCacheKey other && Equals(other);

        public override int GetHashCode()
        {
            return HashCode.Combine(NamespaceId, KeyHigh, KeyLow);
        }

        public static bool operator ==(NnrpCacheKey left, NnrpCacheKey right) => left.Equals(right);

        public static bool operator !=(NnrpCacheKey left, NnrpCacheKey right) => !left.Equals(right);

        public static NnrpCacheKey FromCachePutMetadata(CachePutMetadata metadata)
        {
            return new NnrpCacheKey(metadata.CacheNamespace, metadata.CacheKeyHigh, metadata.CacheKeyLow);
        }

        public static NnrpCacheKey FromCacheInvalidateMetadata(CacheInvalidateMetadata metadata)
        {
            return new NnrpCacheKey(metadata.CacheNamespace, metadata.CacheKeyHigh, metadata.CacheKeyLow);
        }
    }
}
