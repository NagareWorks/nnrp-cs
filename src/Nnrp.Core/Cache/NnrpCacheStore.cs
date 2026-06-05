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
        private readonly ConcurrentDictionary<NnrpCacheKey, NnrpCacheEntry> entries = new ConcurrentDictionary<NnrpCacheKey, NnrpCacheEntry>();
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

        public NnrpCacheResult TryPut(NnrpCacheKey key, ReadOnlyMemory<byte> objectBytes, int ttlSeconds)
        {
            if (objectBytes.Length > MaxObjectBytes)
            {
                return NnrpCacheResult.LimitExceeded(
                    $"Cache object size {objectBytes.Length} exceeds maximum {MaxObjectBytes}.");
            }

            if (entries.Count >= MaxEntries && !entries.ContainsKey(key))
            {
                return NnrpCacheResult.LimitExceeded(
                    $"Cache is full ({entries.Count} entries, max {MaxEntries}).");
            }

            var entry = new NnrpCacheEntry(key, objectBytes, ttlSeconds);
            entries[key] = entry;
            return NnrpCacheResult.Stored(entry);
        }

        public NnrpCacheResult TryGet(NnrpCacheKey key)
        {
            if (!entries.TryGetValue(key, out var entry))
            {
                return NnrpCacheResult.Miss(key);
            }

            if (entry.IsExpired())
            {
                entries.TryRemove(key, out _);
                return NnrpCacheResult.Miss(key);
            }

            return NnrpCacheResult.Hit(entry);
        }

        public bool TryInvalidate(NnrpCacheKey key)
        {
            return entries.TryRemove(key, out _);
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
