using System;

namespace Nnrp.Core
{
    /// <summary>
    /// A cached object entry with key, payload, and expiration metadata.
    /// </summary>
    public sealed class NnrpCacheEntry
    {
        public NnrpCacheEntry(NnrpCacheKey key, ReadOnlyMemory<byte> objectBytes, int ttlSeconds)
        {
            if (ttlSeconds <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ttlSeconds), "TTL must be positive.");
            }

            Key = key;
            ObjectBytes = objectBytes;
            TtlSeconds = ttlSeconds;
            CreatedAt = DateTimeOffset.UtcNow;
        }

        public NnrpCacheKey Key { get; }

        public ReadOnlyMemory<byte> ObjectBytes { get; }

        public int TtlSeconds { get; }

        public DateTimeOffset CreatedAt { get; }

        public bool IsExpired()
        {
            return DateTimeOffset.UtcNow > CreatedAt.AddSeconds(TtlSeconds);
        }
    }

    /// <summary>
    /// Outcome of a cache lookup or store operation.
    /// </summary>
    public readonly struct NnrpCacheResult
    {
        private NnrpCacheResult(bool isSuccess, NnrpCacheEntry? entry, NnrpCacheResultCode code, string message)
        {
            IsSuccess = isSuccess;
            Entry = entry;
            Code = code;
            Message = message ?? string.Empty;
        }

        public bool IsSuccess { get; }

        public NnrpCacheEntry? Entry { get; }

        public NnrpCacheResultCode Code { get; }

        public string Message { get; }

        public static NnrpCacheResult Hit(NnrpCacheEntry entry)
        {
            return new NnrpCacheResult(true, entry ?? throw new ArgumentNullException(nameof(entry)), NnrpCacheResultCode.Hit, string.Empty);
        }

        public static NnrpCacheResult Miss(NnrpCacheKey key)
        {
            return new NnrpCacheResult(false, null, NnrpCacheResultCode.CacheMiss, $"Cache miss for namespace {key.NamespaceId}.");
        }

        public static NnrpCacheResult LimitExceeded(string message)
        {
            return new NnrpCacheResult(false, null, NnrpCacheResultCode.LimitExceeded, message);
        }

        public static NnrpCacheResult Stored(NnrpCacheEntry entry)
        {
            return new NnrpCacheResult(true, entry ?? throw new ArgumentNullException(nameof(entry)), NnrpCacheResultCode.Stored, string.Empty);
        }
    }

    public enum NnrpCacheResultCode : byte
    {
        Hit = 0,
        CacheMiss = 1,
        LimitExceeded = 2,
        Stored = 3,
    }
}
