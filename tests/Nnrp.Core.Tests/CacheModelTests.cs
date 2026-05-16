using System;
using System.Threading;
using Nnrp.Core;
using Xunit;

namespace Nnrp.Core.Tests
{
    public sealed class CacheModelTests
    {
        [Fact]
        public void CacheKeyEqualityAndFromMetadata()
        {
            var key1 = new NnrpCacheKey(1, 0xCAFE, 0xBEEF);
            var key2 = new NnrpCacheKey(1, 0xCAFE, 0xBEEF);
            var key3 = new NnrpCacheKey(2, 0xCAFE, 0xBEEF);

            Assert.Equal(key1, key2);
            Assert.True(key1 == key2);
            Assert.NotEqual(key1, key3);
            Assert.Equal(key1.GetHashCode(), key2.GetHashCode());

            var putMetadata = new CachePutMetadata(
                cacheNamespace: 1,
                cacheKeyHigh: 0xCAFE,
                cacheKeyLow: 0xBEEF,
                objectKind: CacheObjectKind.CameraBlock,
                ttlMilliseconds: 5000,
                objectBytes: 16,
                codecBitmap: 0);
            Assert.Equal(key1, NnrpCacheKey.FromCachePutMetadata(putMetadata));

            var invalidateMetadata = new CacheInvalidateMetadata(
                invalidateScope: CacheInvalidateScope.Entry,
                cacheNamespace: 1,
                cacheKeyHigh: 0xCAFE,
                cacheKeyLow: 0xBEEF,
                reasonCode: 0);
            Assert.Equal(key1, NnrpCacheKey.FromCacheInvalidateMetadata(invalidateMetadata));
        }

        [Fact]
        public void CacheEntryStoresPayloadAndDetectsExpiration()
        {
            var key = new NnrpCacheKey(1, 10, 20);
            var payload = new byte[] { 1, 2, 3 };
            var entry = new NnrpCacheEntry(key, payload, ttlSeconds: 3600);

            Assert.Equal(key, entry.Key);
            Assert.Equal(payload, entry.ObjectBytes.ToArray());
            Assert.Equal(3600, entry.TtlSeconds);
            Assert.False(entry.IsExpired());

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new NnrpCacheEntry(key, payload, ttlSeconds: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new NnrpCacheEntry(key, payload, ttlSeconds: -1));
        }

        [Fact]
        public void CacheStorePutGetAndInvalidate()
        {
            var store = new NnrpCacheStore(maxEntries: 10, maxObjectBytes: 1024);
            var key = new NnrpCacheKey(1, 100, 200);
            var payload = new byte[] { 0xAA, 0xBB };

            var putResult = store.TryPut(key, payload, ttlSeconds: 60);
            Assert.True(putResult.IsSuccess);
            Assert.Equal(NnrpCacheResultCode.Stored, putResult.Code);

            var getResult = store.TryGet(key);
            Assert.True(getResult.IsSuccess);
            Assert.Equal(NnrpCacheResultCode.Hit, getResult.Code);
            Assert.Equal(payload, getResult.Entry!.ObjectBytes.ToArray());

            Assert.True(store.TryInvalidate(key));
            Assert.Equal(NnrpCacheResultCode.CacheMiss, store.TryGet(key).Code);
            Assert.False(store.TryInvalidate(key));
        }

        [Fact]
        public void CacheStoreEnforcesMaxObjectSize()
        {
            var store = new NnrpCacheStore(maxObjectBytes: 10);
            var key = new NnrpCacheKey(1, 1, 1);

            var result = store.TryPut(key, new byte[11], ttlSeconds: 60);
            Assert.False(result.IsSuccess);
            Assert.Equal(NnrpCacheResultCode.LimitExceeded, result.Code);
            Assert.Contains("exceeds maximum", result.Message);
        }

        [Fact]
        public void CacheStoreEnforcesMaxEntries()
        {
            var store = new NnrpCacheStore(maxEntries: 2, maxObjectBytes: 1024);

            Assert.True(store.TryPut(new NnrpCacheKey(1, 1, 1), new byte[1], 60).IsSuccess);
            Assert.True(store.TryPut(new NnrpCacheKey(1, 2, 2), new byte[1], 60).IsSuccess);
            Assert.False(store.TryPut(new NnrpCacheKey(1, 3, 3), new byte[1], 60).IsSuccess);
        }

        [Fact]
        public void CacheStoreExpiresAndEvictsEntries()
        {
            var store = new NnrpCacheStore(maxEntries: 10);

            var key = new NnrpCacheKey(1, 1, 1);
            Assert.True(store.TryPut(key, new byte[1], ttlSeconds: 3600).IsSuccess);

            // Entry with a long TTL should be found.
            var result = store.TryGet(key);
            Assert.Equal(NnrpCacheResultCode.Hit, result.Code);
            Assert.Equal(1, store.Count);

            // Evict all expired entries (none should be expired yet).
            store.EvictExpired();
            Assert.Equal(1, store.Count);

            // Invalidate and verify removal.
            Assert.True(store.TryInvalidate(key));
            Assert.Equal(0, store.Count);
            Assert.Equal(NnrpCacheResultCode.CacheMiss, store.TryGet(key).Code);
        }

        [Fact]
        public void CacheResultFactoriesValidateAndProduceCorrectCodes()
        {
            var key = new NnrpCacheKey(1, 2, 3);
            var entry = new NnrpCacheEntry(key, new byte[4], 60);

            var hit = NnrpCacheResult.Hit(entry);
            Assert.True(hit.IsSuccess);
            Assert.Equal(NnrpCacheResultCode.Hit, hit.Code);

            var miss = NnrpCacheResult.Miss(key);
            Assert.False(miss.IsSuccess);
            Assert.Equal(NnrpCacheResultCode.CacheMiss, miss.Code);

            var limit = NnrpCacheResult.LimitExceeded("too big");
            Assert.False(limit.IsSuccess);
            Assert.Equal(NnrpCacheResultCode.LimitExceeded, limit.Code);

            var stored = NnrpCacheResult.Stored(entry);
            Assert.True(stored.IsSuccess);
            Assert.Equal(NnrpCacheResultCode.Stored, stored.Code);
        }

        [Fact]
        public void CacheStoreClearAndEvictExpired()
        {
            var store = new NnrpCacheStore(maxEntries: 10);
            store.TryPut(new NnrpCacheKey(1, 1, 1), new byte[1], 3600);
            store.TryPut(new NnrpCacheKey(1, 2, 2), new byte[1], 3600);
            Assert.Equal(2, store.Count);

            store.EvictExpired();
            Assert.Equal(2, store.Count);

            store.Clear();
            Assert.Equal(0, store.Count);
        }

        [Fact]
        public void CacheStoreSettersUpdateLimits()
        {
            var store = new NnrpCacheStore(maxEntries: 5, maxObjectBytes: 100);
            Assert.Equal(5, store.MaxEntries);
            Assert.Equal(100L, store.MaxObjectBytes);

            store.MaxEntries = 10;
            Assert.Equal(10, store.MaxEntries);

            store.MaxObjectBytes = 200;
            Assert.Equal(200L, store.MaxObjectBytes);

            Assert.Throws<ArgumentOutOfRangeException>(() => store.MaxEntries = -1);
            Assert.Throws<ArgumentOutOfRangeException>(() => store.MaxObjectBytes = -1);
        }

        [Fact]
        public void CacheKeyInequalityAndHashCode()
        {
            var key1 = new NnrpCacheKey(1, 0xAA, 0xBB);
            var key2 = new NnrpCacheKey(2, 0xAA, 0xBB);
            Assert.True(key1 != key2);
            Assert.False(key1.Equals(null));
            Assert.NotEqual(key1.GetHashCode(), key2.GetHashCode());
        }

        [Fact]
        public void CacheEntryExpirationForFutureTtl()
        {
            var key = new NnrpCacheKey(1, 1, 1);
            var entry = new NnrpCacheEntry(key, new byte[1], ttlSeconds: 86400);
            Assert.False(entry.IsExpired());
        }

        [Fact]
        public void CachePutAndInvalidateMessagesRoundTrip()
        {
            var header = new NnrpHeader(
                versionMajor: NnrpHeader.CurrentVersionMajor,
                wireFormat: NnrpHeader.CurrentWireFormat,
                messageType: MessageType.CachePut,
                flags: HeaderFlags.None,
                metaLength: CachePutMetadata.MetadataLength,
                bodyLength: 3,
                sessionId: 1, frameId: 0, viewId: 0, routeId: 0, traceId: 0);
            var metadata = new CachePutMetadata(
                cacheNamespace: 1,
                cacheKeyHigh: 0xCAFE,
                cacheKeyLow: 0xBEEF,
                objectKind: CacheObjectKind.CodecAuxBlock,
                ttlMilliseconds: 5000,
                objectBytes: 3,
                codecBitmap: 1);
            var put = new CachePutMessage(header, metadata, new byte[] { 1, 2, 3 });

            var bytes = put.ToArray();
            Assert.True(CachePutMessage.TryParse(bytes, out var parsed, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(metadata.CacheNamespace, parsed.Metadata.CacheNamespace);
            Assert.Equal(new byte[] { 1, 2, 3 }, parsed.ObjectBytes.ToArray());

            var invalidateMetadata = new CacheInvalidateMetadata(
                invalidateScope: CacheInvalidateScope.Entry,
                cacheNamespace: 1,
                cacheKeyHigh: 0xCAFE,
                cacheKeyLow: 0xBEEF,
                reasonCode: 0);
            var invalidateHeader = new NnrpHeader(
                versionMajor: NnrpHeader.CurrentVersionMajor,
                wireFormat: NnrpHeader.CurrentWireFormat,
                messageType: MessageType.CacheInvalidate,
                flags: HeaderFlags.None,
                metaLength: CacheInvalidateMetadata.MetadataLength,
                bodyLength: 0,
                sessionId: 1, frameId: 0, viewId: 0, routeId: 0, traceId: 0);
            var invalidate = new CacheInvalidateMessage(invalidateHeader, invalidateMetadata);

            var invalidateBytes = invalidate.ToArray();
            Assert.True(CacheInvalidateMessage.TryParse(invalidateBytes, out var parsedInvalidate, out error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(invalidateMetadata.CacheNamespace, parsedInvalidate.Metadata.CacheNamespace);
        }

        [Fact]
        public void CacheMetadataRejectsUnknownCurrentEnumValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new CachePutMetadata(1, 2, 3, (CacheObjectKind)0, 1000, 3, 1));

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new CacheInvalidateMetadata((CacheInvalidateScope)4, 1, 2, 3, 0));

            var putPayload = new byte[CachePutMetadata.MetadataLength];
            BitConverter.GetBytes(1u).CopyTo(putPayload, 0);
            BitConverter.GetBytes(2u).CopyTo(putPayload, 4);
            BitConverter.GetBytes(3u).CopyTo(putPayload, 8);
            BitConverter.GetBytes(0u).CopyTo(putPayload, 12);
            BitConverter.GetBytes(1000u).CopyTo(putPayload, 16);
            BitConverter.GetBytes(64u).CopyTo(putPayload, 20);
            BitConverter.GetBytes(1u).CopyTo(putPayload, 24);
            BitConverter.GetBytes(0u).CopyTo(putPayload, 28);

            Assert.False(CachePutMetadata.TryParse(putPayload, out _, out var putError));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, putError);

            var invalidatePayload = new byte[CacheInvalidateMetadata.MetadataLength];
            BitConverter.GetBytes(4u).CopyTo(invalidatePayload, 0);
            BitConverter.GetBytes(1u).CopyTo(invalidatePayload, 4);
            BitConverter.GetBytes(2u).CopyTo(invalidatePayload, 8);
            BitConverter.GetBytes(3u).CopyTo(invalidatePayload, 12);
            BitConverter.GetBytes(0u).CopyTo(invalidatePayload, 16);

            Assert.False(CacheInvalidateMetadata.TryParse(invalidatePayload, out _, out var invalidateError));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, invalidateError);
        }

        [Fact]
        public void CacheAckMessageRoundTrips()
        {
            var metadata = new CacheAckMetadata(
                cacheNamespace: 1,
                cacheKeyHigh: 0xCAFE,
                cacheKeyLow: 0xBEEF,
                status: CacheAckStatus.Accepted,
                acceptedTtlMilliseconds: 5000,
                maxObjectBytes: 1024,
                detailCode: 0);
            var header = new NnrpHeader(
                versionMajor: NnrpHeader.CurrentVersionMajor,
                wireFormat: NnrpHeader.CurrentWireFormat,
                messageType: MessageType.CacheAck,
                flags: HeaderFlags.None,
                metaLength: CacheAckMetadata.MetadataLength,
                bodyLength: 0,
                sessionId: 1, frameId: 0, viewId: 0, routeId: 0, traceId: 0);
            var ack = new CacheAckMessage(header, metadata);
            var bytes = ack.ToArray();
            Assert.True(CacheAckMessage.TryParse(bytes, out var parsed, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(CacheAckStatus.Accepted, parsed.Metadata.Status);
        }
    }
}
