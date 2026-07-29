using System;

namespace Nnrp.Core
{
    /// <summary>
    /// A cached object entry with key, payload, and expiration metadata.
    /// </summary>
    public sealed class NnrpCacheEntry
    {
        public NnrpCacheEntry(NnrpCacheObjectId objectId, ReadOnlyMemory<byte> objectBytes, uint ttlMilliseconds)
        {
            ObjectId = objectId;
            ObjectBytes = objectBytes;
            TtlMilliseconds = ttlMilliseconds;
            CreatedAt = DateTimeOffset.UtcNow;
        }

        public NnrpCacheObjectId ObjectId { get; }

        public ReadOnlyMemory<byte> ObjectBytes { get; }

        public uint TtlMilliseconds { get; }

        public DateTimeOffset CreatedAt { get; }

        public bool IsExpired()
        {
            return DateTimeOffset.UtcNow >= CreatedAt.AddMilliseconds(TtlMilliseconds);
        }
    }

    /// <summary>
    /// Outcome of a cache lookup or store operation.
    /// </summary>
    public readonly struct NnrpCacheResult
    {
        private NnrpCacheResult(
            bool isSuccess,
            NnrpCacheObjectId objectId,
            NnrpCacheEntry? entry,
            NnrpCacheResultCode code,
            string message)
        {
            IsSuccess = isSuccess;
            ObjectId = objectId;
            Entry = entry;
            Code = code;
            Message = message ?? string.Empty;
        }

        public bool IsSuccess { get; }

        public NnrpCacheObjectId ObjectId { get; }

        public NnrpCacheEntry? Entry { get; }

        public NnrpCacheResultCode Code { get; }

        public string Message { get; }

        public static NnrpCacheResult Hit(NnrpCacheEntry entry)
        {
            entry = entry ?? throw new ArgumentNullException(nameof(entry));
            return new NnrpCacheResult(true, entry.ObjectId, entry, NnrpCacheResultCode.Hit, string.Empty);
        }

        public static NnrpCacheResult Miss(NnrpCacheObjectId objectId)
        {
            return new NnrpCacheResult(
                false,
                objectId,
                null,
                NnrpCacheResultCode.CacheMiss,
                $"Cache miss for namespace {objectId.CacheNamespace}, key {objectId.CacheKeyHigh:x16}{objectId.CacheKeyLow:x16}, object kind {objectId.ObjectKind}.");
        }

        public static NnrpCacheResult LimitExceeded(NnrpCacheObjectId objectId, string message)
        {
            return new NnrpCacheResult(false, objectId, null, NnrpCacheResultCode.LimitExceeded, message);
        }

        public static NnrpCacheResult Stored(NnrpCacheEntry entry)
        {
            entry = entry ?? throw new ArgumentNullException(nameof(entry));
            return new NnrpCacheResult(true, entry.ObjectId, entry, NnrpCacheResultCode.Stored, string.Empty);
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
