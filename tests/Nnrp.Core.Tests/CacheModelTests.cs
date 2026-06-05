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
        public void CacheErrorCodesRemainStable()
        {
            Assert.Equal(0x00030000u, (uint)CacheErrorCode.None);
            Assert.Equal(0x00030001u, (uint)CacheErrorCode.CacheMiss);
            Assert.Equal(0x00030002u, (uint)CacheErrorCode.LeaseExpired);
            Assert.Equal(0x00030003u, (uint)CacheErrorCode.VersionMismatch);
            Assert.Equal(0x00030004u, (uint)CacheErrorCode.DependencyInvalid);
            Assert.Equal(0x00030005u, (uint)CacheErrorCode.SchemaMismatch);

            Assert.Equal(CacheErrorCode.None, CacheValidationFailure.None.ToCacheErrorCode());
            Assert.Equal(CacheErrorCode.CacheMiss, CacheValidationFailure.Miss.ToCacheErrorCode());
            Assert.Equal(CacheErrorCode.LeaseExpired, CacheValidationFailure.LeaseExpired.ToCacheErrorCode());
            Assert.Equal(CacheErrorCode.VersionMismatch, CacheValidationFailure.VersionMismatch.ToCacheErrorCode());
            Assert.Equal(CacheErrorCode.DependencyInvalid, CacheValidationFailure.DependencyInvalid.ToCacheErrorCode());
            Assert.Equal(CacheErrorCode.SchemaMismatch, CacheValidationFailure.SchemaMismatch.ToCacheErrorCode());
            Assert.Throws<ArgumentOutOfRangeException>(() => ((CacheValidationFailure)255).ToCacheErrorCode());
        }

        [Fact]
        public void CacheLeaseValidatesExpiryVersionAndRenewal()
        {
            var objectId = new NnrpCacheObjectId(7, 0x11223344, 0x55667788, CacheObjectKind.PromptSegment);
            var lease = new NnrpCacheLease(
                objectId,
                objectVersion: 3,
                leaseId: 99,
                ownerScope: CacheLeaseOwnerScope.Session,
                ownerId: 42,
                grantedAtMilliseconds: 10000,
                ttlMilliseconds: 500);

            Assert.Equal(10500ul, lease.ExpiresAtMilliseconds);
            Assert.False(lease.IsExpiredAt(10499));
            Assert.True(lease.TryValidateLiveAt(10499, out var failure));
            Assert.Equal(CacheValidationFailure.None, failure);
            Assert.True(lease.IsExpiredAt(10500));
            Assert.False(lease.TryValidateLiveAt(10500, out failure));
            Assert.Equal(CacheValidationFailure.LeaseExpired, failure);

            Assert.True(lease.TryValidateVersion(3, out failure));
            Assert.Equal(CacheValidationFailure.None, failure);
            Assert.False(lease.TryValidateVersion(4, out failure));
            Assert.Equal(CacheValidationFailure.VersionMismatch, failure);

            var renewed = lease.WithRenewedTtl(1000);
            Assert.Equal(lease.LeaseId, renewed.LeaseId);
            Assert.Equal(lease.ObjectVersion, renewed.ObjectVersion);
            Assert.Equal(11000ul, renewed.ExpiresAtMilliseconds);
        }

        [Fact]
        public void CacheLeaseRejectsUnknownOwnerScopesAndSaturatesExpiry()
        {
            var objectId = new NnrpCacheObjectId(1, 2, 3, CacheObjectKind.ToolSchema);

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new NnrpCacheObjectId(1, 2, 3, (CacheObjectKind)0));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new NnrpCacheLease(objectId, 1, 2, (CacheLeaseOwnerScope)255, 3, 4, 5));

            var lease = new NnrpCacheLease(
                objectId,
                objectVersion: 1,
                leaseId: 2,
                ownerScope: CacheLeaseOwnerScope.Connection,
                ownerId: 3,
                grantedAtMilliseconds: ulong.MaxValue - 10,
                ttlMilliseconds: 20);
            Assert.Equal(ulong.MaxValue, lease.ExpiresAtMilliseconds);
        }

        [Fact]
        public void CacheObjectIdMatchesInvalidateScopes()
        {
            var putMetadata = new CachePutMetadata(
                cacheNamespace: 7,
                cacheKeyHigh: 8,
                cacheKeyLow: 9,
                objectKind: CacheObjectKind.ToolSchema,
                ttlMilliseconds: 100,
                objectBytes: 64,
                codecBitmap: 0);
            var objectId = NnrpCacheObjectId.FromCachePutMetadata(putMetadata);

            Assert.True(objectId.MatchesInvalidate(new CacheInvalidateMetadata(CacheInvalidateScope.WholeSession, 0, 0, 0, 0)));
            Assert.True(objectId.MatchesInvalidate(new CacheInvalidateMetadata(CacheInvalidateScope.Namespace, 7, 0, 0, 0)));
            Assert.True(objectId.MatchesInvalidate(new CacheInvalidateMetadata(CacheInvalidateScope.ObjectKind, 7, (uint)CacheObjectKind.ToolSchema, 0, 0)));
            Assert.True(objectId.MatchesInvalidate(new CacheInvalidateMetadata(CacheInvalidateScope.ObjectKey, 7, 8, 9, 0)));
            Assert.False(objectId.MatchesInvalidate(new CacheInvalidateMetadata(CacheInvalidateScope.Namespace, 8, 0, 0, 0)));
            Assert.False(objectId.MatchesInvalidate(new CacheInvalidateMetadata(CacheInvalidateScope.ObjectKind, 7, (uint)CacheObjectKind.PromptSegment, 0, 0)));
            Assert.False(objectId.MatchesInvalidate(new CacheInvalidateMetadata(CacheInvalidateScope.ObjectKey, 7, 8, 10, 0)));
        }

        [Fact]
        public void CacheVersionMonotonicityRejectsStaleUpdates()
        {
            Assert.True(NnrpCacheLeaseValidation.TryValidateMonotonicVersion(3, 4, out var failure));
            Assert.Equal(CacheValidationFailure.None, failure);

            Assert.False(NnrpCacheLeaseValidation.TryValidateMonotonicVersion(3, 3, out failure));
            Assert.Equal(CacheValidationFailure.VersionMismatch, failure);

            Assert.False(NnrpCacheLeaseValidation.TryValidateMonotonicVersion(4, 3, out failure));
            Assert.Equal(CacheValidationFailure.VersionMismatch, failure);
        }

        [Fact]
        public void CacheDependenciesValidateVersionsAndInvalidations()
        {
            var objectId = new NnrpCacheObjectId(1, 2, 3, CacheObjectKind.PromptSegment);
            var dependency = new NnrpCacheDependency(objectId, requiredVersion: 7);
            var state = new NnrpCacheDependencyState(objectId, currentVersion: 7, invalidated: false);

            Assert.True(NnrpCacheLeaseValidation.TryValidateDependencies(
                new[] { dependency },
                new[] { state },
                out var failure));
            Assert.Equal(CacheValidationFailure.None, failure);

            var wrongVersion = new NnrpCacheDependencyState(objectId, currentVersion: 8, invalidated: false);
            Assert.False(NnrpCacheLeaseValidation.TryValidateDependencies(
                new[] { dependency },
                new[] { wrongVersion },
                out failure));
            Assert.Equal(CacheValidationFailure.DependencyInvalid, failure);

            var invalidated = new NnrpCacheDependencyState(objectId, currentVersion: 7, invalidated: true);
            Assert.False(NnrpCacheLeaseValidation.TryValidateDependencies(
                new[] { dependency },
                new[] { invalidated },
                out failure));
            Assert.Equal(CacheValidationFailure.DependencyInvalid, failure);

            Assert.False(NnrpCacheLeaseValidation.TryValidateDependencies(
                new[] { dependency },
                Array.Empty<NnrpCacheDependencyState>(),
                out failure));
            Assert.Equal(CacheValidationFailure.DependencyInvalid, failure);

            Assert.True(NnrpCacheLeaseValidation.TryValidateDependencies(
                Array.Empty<NnrpCacheDependency>(),
                Array.Empty<NnrpCacheDependencyState>(),
                out failure));
            Assert.Equal(CacheValidationFailure.None, failure);
        }

        [Fact]
        public void CacheLeaseModelValueEqualityUsesPublicIdentityFields()
        {
            var objectId = new NnrpCacheObjectId(1, 2, 3, CacheObjectKind.PromptSegment);
            var sameObjectId = new NnrpCacheObjectId(1, 2, 3, CacheObjectKind.PromptSegment);
            var otherObjectId = new NnrpCacheObjectId(1, 2, 4, CacheObjectKind.PromptSegment);

            Assert.True(objectId == sameObjectId);
            Assert.False(objectId != sameObjectId);
            Assert.True(objectId != otherObjectId);
            Assert.True(objectId.Equals((object)sameObjectId));
            Assert.False(objectId.Equals((object)"cache"));
            Assert.Equal(objectId.GetHashCode(), sameObjectId.GetHashCode());
            Assert.NotEqual(objectId.GetHashCode(), otherObjectId.GetHashCode());

            var lease = new NnrpCacheLease(objectId, 7, 99, CacheLeaseOwnerScope.Session, 42, 1000, 500);
            var sameLease = new NnrpCacheLease(sameObjectId, 7, 99, CacheLeaseOwnerScope.Session, 42, 1000, 500);
            var otherLease = new NnrpCacheLease(objectId, 8, 99, CacheLeaseOwnerScope.Session, 42, 1000, 500);

            Assert.True(lease == sameLease);
            Assert.False(lease != sameLease);
            Assert.True(lease != otherLease);
            Assert.True(lease.Equals((object)sameLease));
            Assert.False(lease.Equals((object)"lease"));
            Assert.Equal(lease.GetHashCode(), sameLease.GetHashCode());
            Assert.NotEqual(lease.GetHashCode(), otherLease.GetHashCode());

            var dependency = new NnrpCacheDependency(objectId, 7);
            var sameDependency = new NnrpCacheDependency(sameObjectId, 7);
            var otherDependency = new NnrpCacheDependency(objectId, 8);
            Assert.True(dependency.Equals(sameDependency));
            Assert.True(dependency.Equals((object)sameDependency));
            Assert.False(dependency.Equals(otherDependency));
            Assert.False(dependency.Equals((object)"dependency"));
            Assert.Equal(dependency.GetHashCode(), sameDependency.GetHashCode());

            var state = new NnrpCacheDependencyState(objectId, 7, invalidated: false);
            var sameState = new NnrpCacheDependencyState(sameObjectId, 7, invalidated: false);
            var otherState = new NnrpCacheDependencyState(objectId, 7, invalidated: true);
            Assert.True(state.Equals(sameState));
            Assert.True(state.Equals((object)sameState));
            Assert.False(state.Equals(otherState));
            Assert.False(state.Equals((object)"state"));
            Assert.Equal(state.GetHashCode(), sameState.GetHashCode());
            Assert.NotEqual(state.GetHashCode(), otherState.GetHashCode());
        }

        [Fact]
        public void CacheHostQueryCreatesObjectReferenceWithoutPolicyDecisions()
        {
            var lease = CreateLease();
            var query = NnrpCacheHostCommand.Query(lease);

            Assert.Equal(NnrpCacheHostAction.Query, query.Action);
            Assert.True(query.CarriesLeaseIdentity);
            Assert.Equal(lease.ObjectVersion, query.ExpectedObjectVersion);
            Assert.Equal(lease.LeaseId, query.LeaseId);
            Assert.Equal(lease.OwnerScope, query.OwnerScope);
            Assert.Equal(lease.OwnerId, query.OwnerId);
            Assert.Equal(0u, query.LeaseTtlHintMilliseconds);
            Assert.True(query.MatchesLease(lease));
            Assert.False(query.MatchesLease(CreateLease(objectVersion: 8)));
            Assert.False(query.MatchesLease(CreateLease(leaseId: 100)));

            Assert.True(query.TryCreateObjectReference(referenceFlags: 7, out var block));
            Assert.Equal(lease.ObjectId.ObjectKind, block.ObjectKind);
            Assert.Equal(7, block.ReferenceFlags);
            Assert.Equal(lease.ObjectId.CacheNamespace, block.CacheNamespace);
            Assert.Equal(lease.ObjectId.CacheKeyHigh, block.CacheKeyHigh);
            Assert.Equal(lease.ObjectId.CacheKeyLow, block.CacheKeyLow);
            Assert.Equal(block, query.CreateObjectReference(referenceFlags: 7));

            Assert.False(query.TryCreatePrefetchMetadata(out _));
            Assert.False(query.TryCreateReleaseMetadata(out _));

            var versionOnlyQuery = NnrpCacheHostCommand.Query(lease.ObjectId, expectedObjectVersion: lease.ObjectVersion);
            Assert.True(versionOnlyQuery.CarriesLeaseIdentity);
            Assert.True(versionOnlyQuery.MatchesLease(lease));
            Assert.False(versionOnlyQuery.MatchesLease(CreateLease(objectVersion: 9)));

            var sameQuery = NnrpCacheHostCommand.Query(lease);
            var otherQuery = NnrpCacheHostCommand.Query(lease.ObjectId);
            Assert.True(query == sameQuery);
            Assert.False(query != sameQuery);
            Assert.NotEqual(query, otherQuery);
            Assert.True(query.Equals((object)sameQuery));
            Assert.False(query.Equals((object)"query"));
            Assert.Equal(query.GetHashCode(), sameQuery.GetHashCode());
        }

        [Fact]
        public void CacheHostTouchPreservesLeaseIdentityWithoutWireMetadata()
        {
            var lease = CreateLease();
            var touch = NnrpCacheHostCommand.Touch(lease, leaseTtlHintMilliseconds: 2000);

            Assert.Equal(NnrpCacheHostAction.Touch, touch.Action);
            Assert.True(touch.CarriesLeaseIdentity);
            Assert.Equal(lease.ObjectId, touch.ObjectId);
            Assert.Equal(lease.ObjectVersion, touch.ExpectedObjectVersion);
            Assert.Equal(lease.LeaseId, touch.LeaseId);
            Assert.Equal(CacheLeaseOwnerScope.Session, touch.OwnerScope);
            Assert.Equal(42ul, touch.OwnerId);
            Assert.Equal(2000u, touch.LeaseTtlHintMilliseconds);
            Assert.True(touch.MatchesLease(lease));
            Assert.False(touch.TryCreateObjectReference(referenceFlags: 0, out _));
            Assert.False(touch.TryCreatePrefetchMetadata(out _));
            Assert.False(touch.TryCreateReleaseMetadata(out _));

            Assert.Throws<InvalidOperationException>(() => touch.CreateObjectReference());
            Assert.Throws<InvalidOperationException>(() => touch.CreatePrefetchMetadata());
            Assert.Throws<InvalidOperationException>(() => touch.CreateReleaseMetadata());
        }

        [Fact]
        public void CacheHostPrefetchAndReleaseCreateControlMetadata()
        {
            var objectId = new NnrpCacheObjectId(9, 10, 11, CacheObjectKind.PayloadLayoutTemplate);
            var prefetch = NnrpCacheHostCommand.Prefetch(
                objectId,
                objectBytes: 64,
                leaseTtlHintMilliseconds: 5000,
                codecBitmap: 3,
                putFlags: CachePutFlags.Pinned | CachePutFlags.Reusable);

            Assert.Equal(NnrpCacheHostAction.Prefetch, prefetch.Action);
            Assert.False(prefetch.CarriesLeaseIdentity);
            Assert.False(prefetch.TryCreateObjectReference(referenceFlags: 0, out _));
            Assert.False(prefetch.TryCreateReleaseMetadata(out _));
            Assert.True(prefetch.TryCreatePrefetchMetadata(out var putMetadata));
            Assert.Equal(putMetadata, prefetch.CreatePrefetchMetadata());
            Assert.Equal(objectId.CacheNamespace, putMetadata.CacheNamespace);
            Assert.Equal(objectId.CacheKeyHigh, putMetadata.CacheKeyHigh);
            Assert.Equal(objectId.CacheKeyLow, putMetadata.CacheKeyLow);
            Assert.Equal(objectId.ObjectKind, putMetadata.ObjectKind);
            Assert.Equal(5000u, putMetadata.TtlMilliseconds);
            Assert.Equal(64u, putMetadata.ObjectBytes);
            Assert.Equal(3u, putMetadata.CodecBitmap);
            Assert.Equal(CachePutFlags.Pinned | CachePutFlags.Reusable, putMetadata.Flags);
            Assert.Throws<InvalidOperationException>(() => prefetch.CreateReleaseMetadata());
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                NnrpCacheHostCommand.Prefetch(objectId, 1, 1, putFlags: (CachePutFlags)4));

            var release = NnrpCacheHostCommand.Release(objectId, reasonCode: 77);
            Assert.Equal(NnrpCacheHostAction.Release, release.Action);
            Assert.False(release.CarriesLeaseIdentity);
            Assert.False(release.TryCreateObjectReference(referenceFlags: 0, out _));
            Assert.False(release.TryCreatePrefetchMetadata(out _));
            Assert.True(release.TryCreateReleaseMetadata(out var invalidateMetadata));
            Assert.Equal(invalidateMetadata, release.CreateReleaseMetadata());
            Assert.Equal(CacheInvalidateScope.ObjectKey, invalidateMetadata.InvalidateScope);
            Assert.Equal(objectId.CacheNamespace, invalidateMetadata.CacheNamespace);
            Assert.Equal(objectId.CacheKeyHigh, invalidateMetadata.CacheKeyHigh);
            Assert.Equal(objectId.CacheKeyLow, invalidateMetadata.CacheKeyLow);
            Assert.Equal(77u, invalidateMetadata.ReasonCode);

            var leaseRelease = NnrpCacheHostCommand.Release(CreateLease(), reasonCode: 88);
            Assert.True(leaseRelease.CarriesLeaseIdentity);
            Assert.Equal(88u, leaseRelease.ReleaseReasonCode);
        }

        [Fact]
        public void CacheHostResultProjectsNativeOutcomes()
        {
            var lease = CreateLease();
            var query = NnrpCacheHostCommand.Query(lease);
            var accepted = NnrpCacheHostResult.Accepted(query, lease, detailCode: 12);

            Assert.Equal(NnrpCacheHostOutcome.Accepted, accepted.Outcome);
            Assert.Equal(CacheValidationFailure.None, accepted.Failure);
            Assert.Equal(CacheErrorCode.None, accepted.ErrorCode);
            Assert.True(accepted.HasLease);
            Assert.False(accepted.IsFailure);
            Assert.Equal(lease, accepted.Lease.GetValueOrDefault());
            Assert.Equal(12u, accepted.DetailCode);

            Assert.Throws<ArgumentException>(() =>
                NnrpCacheHostResult.Accepted(query, CreateLease(objectVersion: 8)));

            var rejected = NnrpCacheHostResult.Rejected(
                query,
                CacheValidationFailure.LeaseExpired,
                detailCode: (uint)CacheErrorCode.LeaseExpired);
            Assert.Equal(NnrpCacheHostOutcome.Rejected, rejected.Outcome);
            Assert.Equal(CacheErrorCode.LeaseExpired, rejected.ErrorCode);
            Assert.True(rejected.IsFailure);
            Assert.False(rejected.HasLease);
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                NnrpCacheHostResult.Rejected(query, CacheValidationFailure.None));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                NnrpCacheHostResult.Rejected(query, (CacheValidationFailure)255));

            var invalidated = NnrpCacheHostResult.Invalidated(
                query,
                new CacheInvalidateMetadata(CacheInvalidateScope.Namespace, lease.ObjectId.CacheNamespace, 0, 0, 33));
            Assert.Equal(NnrpCacheHostOutcome.Invalidated, invalidated.Outcome);
            Assert.Equal(CacheValidationFailure.None, invalidated.Failure);
            Assert.False(invalidated.HasLease);
            Assert.False(invalidated.IsFailure);
            Assert.Equal(33u, invalidated.DetailCode);

            Assert.Throws<ArgumentException>(() =>
                NnrpCacheHostResult.Invalidated(
                    query,
                    new CacheInvalidateMetadata(CacheInvalidateScope.Namespace, lease.ObjectId.CacheNamespace + 1, 0, 0, 1)));

            var sameAccepted = NnrpCacheHostResult.Accepted(query, lease, detailCode: 12);
            Assert.True(accepted == sameAccepted);
            Assert.False(accepted != sameAccepted);
            Assert.NotEqual(accepted, rejected);
            Assert.True(accepted.Equals((object)sameAccepted));
            Assert.False(accepted.Equals((object)"result"));
            Assert.Equal(accepted.GetHashCode(), sameAccepted.GetHashCode());
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

        private static NnrpCacheLease CreateLease(ulong objectVersion = 7, ulong leaseId = 99)
        {
            return new NnrpCacheLease(
                new NnrpCacheObjectId(1, 2, 3, CacheObjectKind.PromptSegment),
                objectVersion,
                leaseId,
                CacheLeaseOwnerScope.Session,
                ownerId: 42,
                grantedAtMilliseconds: 1000,
                ttlMilliseconds: 5000);
        }
    }
}
