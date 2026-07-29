using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Nnrp.Core
{
    /// <summary>
    /// Thread-safe in-memory cache store for NNRP cache control messages.
    /// Validates object size against a configurable maximum and evicts expired entries.
    /// </summary>
    public sealed class NnrpCacheStore
    {
        private readonly ConcurrentDictionary<NnrpCacheObjectId, NnrpCacheEntry> entries = new ConcurrentDictionary<NnrpCacheObjectId, NnrpCacheEntry>();
        private int maxEntries;
        private long maxObjectBytes;

        public NnrpCacheStore(int maxEntries = 256, long maxObjectBytes = 16 * 1024 * 1024)
        {
            this.maxEntries = maxEntries;
            MaxObjectBytes = maxObjectBytes;
        }

        public int MaxEntries
        {
            get => maxEntries;
            set
            {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                maxEntries = value;
            }
        }

        public long MaxObjectBytes
        {
            get => Interlocked.Read(ref maxObjectBytes);
            set
            {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                Interlocked.Exchange(ref maxObjectBytes, value);
            }
        }

        public int Count => entries.Count;

        public NnrpCacheResult TryPut(NnrpCacheObjectId objectId, ReadOnlyMemory<byte> objectBytes, uint ttlMilliseconds)
        {
            if (objectBytes.Length > MaxObjectBytes)
            {
                return NnrpCacheResult.LimitExceeded(
                    objectId,
                    $"Cache object size {objectBytes.Length} exceeds maximum {MaxObjectBytes}.");
            }

            if (entries.Count >= MaxEntries && !entries.ContainsKey(objectId))
            {
                return NnrpCacheResult.LimitExceeded(
                    objectId,
                    $"Cache is full ({entries.Count} entries, max {MaxEntries}).");
            }

            var entry = new NnrpCacheEntry(objectId, objectBytes, ttlMilliseconds);
            entries[objectId] = entry;
            return NnrpCacheResult.Stored(entry);
        }

        public NnrpCacheResult TryGet(NnrpCacheObjectId objectId)
        {
            if (!entries.TryGetValue(objectId, out var entry))
            {
                return NnrpCacheResult.Miss(objectId);
            }

            if (entry.IsExpired())
            {
                entries.TryRemove(objectId, out _);
                return NnrpCacheResult.Miss(objectId);
            }

            return NnrpCacheResult.Hit(entry);
        }

        public bool TryInvalidate(NnrpCacheObjectId objectId)
        {
            return entries.TryRemove(objectId, out _);
        }

        internal int InvalidateMatching(CacheInvalidateMetadata metadata)
        {
            var removed = 0;
            foreach (var pair in entries)
            {
                if (pair.Key.MatchesInvalidate(metadata) && entries.TryRemove(pair.Key, out _))
                {
                    removed++;
                }
            }

            return removed;
        }

        public void Clear()
        {
            entries.Clear();
        }

        public void EvictExpired()
        {
            foreach (var pair in entries)
            {
                if (pair.Value.IsExpired())
                {
                    entries.TryRemove(pair.Key, out _);
                }
            }
        }
    }
}
