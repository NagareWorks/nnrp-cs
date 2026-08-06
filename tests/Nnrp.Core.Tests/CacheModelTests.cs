using System;
using System.Threading;
using Nnrp.Core;
using Xunit;

namespace Nnrp.Core.Tests
{
    public sealed class CacheModelTests
    {
        [Fact]
        public void CacheObjectIdEqualityAndMetadataMatching()
        {
            var objectId1 = CacheId(1, 0xCAFE, 0xBEEF, CacheObjectKind.CameraBlock);
            var objectId2 = CacheId(1, 0xCAFE, 0xBEEF, CacheObjectKind.CameraBlock);
            var objectId3 = CacheId(2, 0xCAFE, 0xBEEF, CacheObjectKind.CameraBlock);
            var objectId4 = CacheId(1, 0xCAFE, 0xBEEF, CacheObjectKind.CodecAuxBlock);

            Assert.Equal(objectId1, objectId2);
            Assert.True(objectId1 == objectId2);
            Assert.NotEqual(objectId1, objectId3);
            Assert.NotEqual(objectId1, objectId4);
            Assert.Equal(objectId1.GetHashCode(), objectId2.GetHashCode());

            var putMetadata = new CachePutMetadata(
                cacheNamespace: 1,
                cacheKeyHigh: 0xCAFE,
                cacheKeyLow: 0xBEEF,
                objectKind: CacheObjectKind.CameraBlock,
                ttlMilliseconds: 5000,
                objectBytes: 16,
                codecBitmap: 0);
            Assert.Equal(objectId1, NnrpCacheObjectId.FromCachePutMetadata(putMetadata));

            var invalidateMetadata = new CacheInvalidateMetadata(
                invalidateScope: CacheInvalidateScope.ObjectKey,
                cacheNamespace: 1,
                cacheKeyHigh: 0xCAFE,
                cacheKeyLow: 0xBEEF,
                reasonCode: 0);
            Assert.True(objectId1.MatchesInvalidate(invalidateMetadata));
            Assert.True(objectId4.MatchesInvalidate(invalidateMetadata));
        }

        [Fact]
        public void CacheEntryStoresPayloadAndDetectsExpiration()
        {
            var objectId = CacheId(1, 10, 20);
            var payload = new byte[] { 1, 2, 3 };
            var entry = new NnrpCacheEntry(objectId, payload, ttlMilliseconds: 3_600_000);

            Assert.Equal(objectId, entry.ObjectId);
            Assert.Equal(payload, entry.ObjectBytes.ToArray());
            Assert.Equal(3_600_000u, entry.TtlMilliseconds);
            Assert.False(entry.IsExpired());

            Assert.True(new NnrpCacheEntry(objectId, payload, ttlMilliseconds: 0).IsExpired());
        }

        [Fact]
        public void CacheStorePutGetAndInvalidate()
        {
            var store = new NnrpCacheStore(maxEntries: 10, maxObjectBytes: 1024);
            var objectId = CacheId(1, 100, 200);
            var payload = new byte[] { 0xAA, 0xBB };

            var putResult = store.TryPut(objectId, payload, ttlMilliseconds: 60_000);
            Assert.True(putResult.IsSuccess);
            Assert.Equal(NnrpCacheResultCode.Stored, putResult.Code);

            var getResult = store.TryGet(objectId);
            Assert.True(getResult.IsSuccess);
            Assert.Equal(NnrpCacheResultCode.Hit, getResult.Code);
            Assert.Equal(payload, getResult.Entry!.ObjectBytes.ToArray());

            Assert.True(store.TryInvalidate(objectId));
            Assert.Equal(NnrpCacheResultCode.CacheMiss, store.TryGet(objectId).Code);
            Assert.False(store.TryInvalidate(objectId));
        }

        [Fact]
        public void CacheStoreEnforcesMaxObjectSize()
        {
            var store = new NnrpCacheStore(maxObjectBytes: 10);
            var objectId = CacheId(1, 1, 1);

            var result = store.TryPut(objectId, new byte[11], ttlMilliseconds: 60_000);
            Assert.False(result.IsSuccess);
            Assert.Equal(NnrpCacheResultCode.LimitExceeded, result.Code);
            Assert.Contains("exceeds maximum", result.Message);
        }

        [Fact]
        public void CacheStoreEnforcesMaxEntries()
        {
            var store = new NnrpCacheStore(maxEntries: 2, maxObjectBytes: 1024);

            Assert.True(store.TryPut(CacheId(1, 1, 1), new byte[1], 60_000).IsSuccess);
            Assert.True(store.TryPut(CacheId(1, 2, 2), new byte[1], 60_000).IsSuccess);
            Assert.False(store.TryPut(CacheId(1, 3, 3), new byte[1], 60_000).IsSuccess);
        }

        [Fact]
        public void CacheStoreExpiresAndEvictsEntries()
        {
            var store = new NnrpCacheStore(maxEntries: 10);

            var objectId = CacheId(1, 1, 1);
            Assert.True(store.TryPut(objectId, new byte[1], ttlMilliseconds: 3_600_000).IsSuccess);

            // Entry with a long TTL should be found.
            var result = store.TryGet(objectId);
            Assert.Equal(NnrpCacheResultCode.Hit, result.Code);
            Assert.Equal(1, store.Count);

            // Evict all expired entries (none should be expired yet).
            store.EvictExpired();
            Assert.Equal(1, store.Count);

            // Invalidate and verify removal.
            Assert.True(store.TryInvalidate(objectId));
            Assert.Equal(0, store.Count);
            Assert.Equal(NnrpCacheResultCode.CacheMiss, store.TryGet(objectId).Code);
        }

        [Fact]
        public void CacheResultFactoriesValidateAndProduceCorrectCodes()
        {
            var objectId = CacheId(1, 2, 3);
            var entry = new NnrpCacheEntry(objectId, new byte[4], 60_000);

            var hit = NnrpCacheResult.Hit(entry);
            Assert.True(hit.IsSuccess);
            Assert.Equal(NnrpCacheResultCode.Hit, hit.Code);

            var miss = NnrpCacheResult.Miss(objectId);
            Assert.False(miss.IsSuccess);
            Assert.Equal(NnrpCacheResultCode.CacheMiss, miss.Code);
            Assert.Equal(objectId, miss.ObjectId);
            Assert.Contains("CameraBlock", miss.Message);

            var limit = NnrpCacheResult.LimitExceeded(objectId, "too big");
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
        public void CacheLeaseResultPreservesTheFrozenLeaseAndVersionEvidence()
        {
            var objectId = new NnrpCacheObjectId(1, 2, 3, CacheObjectKind.PromptSegment);
            var lease = new NnrpCacheLease(
                objectId,
                objectVersion: 7,
                leaseId: 8,
                CacheLeaseOwnerScope.Session,
                ownerId: 9,
                grantedAtMilliseconds: 10,
                ttlMilliseconds: 11);
            var version = new NnrpCacheObjectVersion(objectId, 7, schemaId: 12, schemaVersion: 13);
            var sameVersion = new NnrpCacheObjectVersion(objectId, 7, schemaId: 12, schemaVersion: 13);
            var otherVersion = new NnrpCacheObjectVersion(objectId, 8, schemaId: 12, schemaVersion: 13);

            var result = new NnrpCacheLeaseResult(
                objectId,
                NnrpCacheLeaseOutcome.Valid,
                lease,
                version,
                diagnostic: "local hit");

            Assert.Equal(objectId, result.ObjectId);
            Assert.Equal(NnrpCacheLeaseOutcome.Valid, result.Outcome);
            Assert.Equal(lease, result.Lease);
            Assert.Equal(version, result.ObjectVersion);
            Assert.Equal("local hit", result.Diagnostic);
            Assert.True(version.Equals(sameVersion));
            Assert.True(version == sameVersion);
            Assert.False(version != sameVersion);
            Assert.True(version.Equals((object)sameVersion));
            Assert.False(version.Equals(otherVersion));
            Assert.True(version != otherVersion);
            Assert.False(version == otherVersion);
            Assert.False(version.Equals((object)"version"));
            Assert.Equal(version.GetHashCode(), sameVersion.GetHashCode());
            Assert.NotEqual(version.GetHashCode(), otherVersion.GetHashCode());
        }

        [Fact]
        public void CacheLeaseResultRejectsUnknownOutcomesAndMismatchedEvidence()
        {
            var objectId = new NnrpCacheObjectId(1, 2, 3, CacheObjectKind.PromptSegment);
            var otherId = new NnrpCacheObjectId(1, 2, 4, CacheObjectKind.PromptSegment);
            var otherLease = new NnrpCacheLease(
                otherId,
                objectVersion: 1,
                leaseId: 2,
                CacheLeaseOwnerScope.Connection,
                ownerId: 3,
                grantedAtMilliseconds: 4,
                ttlMilliseconds: 5);
            var otherVersion = new NnrpCacheObjectVersion(otherId, 1);

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new NnrpCacheLeaseResult(objectId, (NnrpCacheLeaseOutcome)byte.MaxValue));
            Assert.Throws<ArgumentException>(() =>
                new NnrpCacheLeaseResult(objectId, NnrpCacheLeaseOutcome.Valid, otherLease));
            Assert.Throws<ArgumentException>(() =>
                new NnrpCacheLeaseResult(
                    objectId,
                    NnrpCacheLeaseOutcome.Valid,
                    objectVersion: otherVersion));
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

            Assert.True(query.TryCreateObjectReference(out var block));
            Assert.Equal(lease.ObjectId.ObjectKind, block.ObjectKind);
            Assert.Equal(0, block.ReferenceFlags);
            Assert.Equal(lease.ObjectId.CacheNamespace, block.CacheNamespace);
            Assert.Equal(lease.ObjectId.CacheKeyHigh, block.CacheKeyHigh);
            Assert.Equal(lease.ObjectId.CacheKeyLow, block.CacheKeyLow);
            Assert.Equal(block, query.CreateObjectReference());

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
            Assert.False(touch.TryCreateObjectReference(out _));
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
            Assert.False(prefetch.TryCreateObjectReference(out _));
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
            Assert.False(release.TryCreateObjectReference(out _));
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
            store.TryPut(CacheId(1, 1, 1), new byte[1], 3_600_000);
            store.TryPut(CacheId(1, 2, 2), new byte[1], 3_600_000);
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
        public void CacheObjectIdInequalityAndHashCode()
        {
            var objectId1 = CacheId(1, 0xAA, 0xBB);
            var objectId2 = CacheId(2, 0xAA, 0xBB);
            Assert.True(objectId1 != objectId2);
            Assert.False(objectId1.Equals("not a cache object ID"));
            Assert.NotEqual(objectId1.GetHashCode(), objectId2.GetHashCode());
        }

        [Fact]
        public void CacheEntryExpirationForFutureTtl()
        {
            var objectId = CacheId(1, 1, 1);
            var entry = new NnrpCacheEntry(objectId, new byte[1], ttlMilliseconds: 86_400_000);
            Assert.False(entry.IsExpired());
        }

        [Fact]
        public void CacheStoreKeepsObjectKindsDistinctAndInvalidatesByScope()
        {
            var store = new NnrpCacheStore();
            var camera = CacheId(7, 10, 20, CacheObjectKind.CameraBlock);
            var codec = CacheId(7, 10, 20, CacheObjectKind.CodecAuxBlock);
            var otherNamespace = CacheId(8, 10, 20, CacheObjectKind.CameraBlock);

            Assert.True(store.TryPut(camera, new byte[] { 1 }, 60_000).IsSuccess);
            Assert.True(store.TryPut(codec, new byte[] { 2 }, 60_000).IsSuccess);
            Assert.True(store.TryPut(otherNamespace, new byte[] { 3 }, 60_000).IsSuccess);
            Assert.Equal(3, store.Count);

            var removed = store.InvalidateMatching(new CacheInvalidateMetadata(
                CacheInvalidateScope.ObjectKind,
                cacheNamespace: 7,
                cacheKeyHigh: (uint)CacheObjectKind.CameraBlock,
                cacheKeyLow: 0,
                reasonCode: 1));

            Assert.Equal(1, removed);
            Assert.False(store.TryGet(camera).IsSuccess);
            Assert.Equal(new byte[] { 2 }, store.TryGet(codec).Entry!.ObjectBytes.ToArray());
            Assert.Equal(new byte[] { 3 }, store.TryGet(otherNamespace).Entry!.ObjectBytes.ToArray());

            Assert.Equal(1, store.InvalidateMatching(new CacheInvalidateMetadata(
                CacheInvalidateScope.Namespace, 7, 0, 0, 2)));
            Assert.Equal(1, store.Count);
            Assert.Equal(1, store.InvalidateMatching(new CacheInvalidateMetadata(
                CacheInvalidateScope.WholeSession, 0, 0, 0, 3)));
            Assert.Equal(0, store.Count);
        }

        [Fact]
        public void Preview4PublicApiDoesNotExposeLegacyCacheKey()
        {
            Assert.Null(typeof(NnrpCacheStore).Assembly.GetType("Nnrp.Core.NnrpCacheKey"));
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
                invalidateScope: CacheInvalidateScope.ObjectKey,
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

        private static NnrpCacheObjectId CacheId(
            uint cacheNamespace,
            ulong cacheKeyHigh,
            ulong cacheKeyLow,
            CacheObjectKind objectKind = CacheObjectKind.CameraBlock)
        {
            return new NnrpCacheObjectId(cacheNamespace, cacheKeyHigh, cacheKeyLow, objectKind);
        }

        [Fact]
        public void CacheMetadataRejectsUnknownCurrentEnumValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new CachePutMetadata(1, 2, 3, (CacheObjectKind)0, 1000, 3, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new CacheAckMetadata(1, 2, 3, (CacheAckStatus)3, 1000, 3, 1));

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new CacheInvalidateMetadata((CacheInvalidateScope)4, 1, 2, 3, 0));
            Assert.Throws<ArgumentException>(() =>
                new CacheInvalidateMetadata(CacheInvalidateScope.WholeSession, 1, 0, 0, 0));
            Assert.Throws<ArgumentException>(() =>
                new CacheInvalidateMetadata(CacheInvalidateScope.Namespace, 1, 2, 0, 0));
            Assert.Throws<ArgumentException>(() =>
                new CacheInvalidateMetadata(CacheInvalidateScope.ObjectKind, 1, 2, 3, 0));

            var putPayload = new byte[CachePutMetadata.MetadataLength];
            BitConverter.GetBytes(1u).CopyTo(putPayload, 0);
            BitConverter.GetBytes(0u).CopyTo(putPayload, 4);
            BitConverter.GetBytes(2ul).CopyTo(putPayload, 8);
            BitConverter.GetBytes(3ul).CopyTo(putPayload, 16);
            BitConverter.GetBytes(1000u).CopyTo(putPayload, 24);
            BitConverter.GetBytes(64u).CopyTo(putPayload, 28);
            BitConverter.GetBytes(1u).CopyTo(putPayload, 32);
            BitConverter.GetBytes(0u).CopyTo(putPayload, 36);

            Assert.False(CachePutMetadata.TryParse(putPayload, out _, out var putError));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, putError);

            var ackPayload = new CacheAckMetadata(1, 2, 3, CacheAckStatus.Accepted, 1000, 64, 0).ToArray();
            BitConverter.GetBytes(3u).CopyTo(ackPayload, 4);
            Assert.False(CacheAckMetadata.TryParse(ackPayload, out _, out var ackStatusError));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, ackStatusError);

            ackPayload = new CacheAckMetadata(1, 2, 3, CacheAckStatus.Accepted, 1000, 64, 0).ToArray();
            BitConverter.GetBytes(1u).CopyTo(ackPayload, 36);
            Assert.False(CacheAckMetadata.TryParse(ackPayload, out _, out var ackReservedError));
            Assert.Equal(NnrpParseError.NonZeroReservedField, ackReservedError);

            var invalidatePayload = new byte[CacheInvalidateMetadata.MetadataLength];
            BitConverter.GetBytes(4u).CopyTo(invalidatePayload, 0);
            BitConverter.GetBytes(1u).CopyTo(invalidatePayload, 4);
            BitConverter.GetBytes(2ul).CopyTo(invalidatePayload, 8);
            BitConverter.GetBytes(3ul).CopyTo(invalidatePayload, 16);
            BitConverter.GetBytes(0u).CopyTo(invalidatePayload, 24);

            Assert.False(CacheInvalidateMetadata.TryParse(invalidatePayload, out _, out var invalidateError));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, invalidateError);

            invalidatePayload = new CacheInvalidateMetadata(CacheInvalidateScope.ObjectKey, 1, 2, 3, 0).ToArray();
            BitConverter.GetBytes(1u).CopyTo(invalidatePayload, 0);
            Assert.False(CacheInvalidateMetadata.TryParse(invalidatePayload, out _, out var invalidateIdentityError));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, invalidateIdentityError);

            invalidatePayload = new CacheInvalidateMetadata(CacheInvalidateScope.ObjectKey, 1, 2, 3, 0).ToArray();
            BitConverter.GetBytes(1u).CopyTo(invalidatePayload, 28);
            Assert.False(CacheInvalidateMetadata.TryParse(invalidatePayload, out _, out var invalidateReservedError));
            Assert.Equal(NnrpParseError.NonZeroReservedField, invalidateReservedError);
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
