using System;

namespace Nnrp.Core
{
    public enum NnrpCacheHostAction : byte
    {
        Query = 0,
        Touch = 1,
        Prefetch = 2,
        Release = 3,
    }

    public readonly struct NnrpCacheHostCommand : IEquatable<NnrpCacheHostCommand>
    {
        private NnrpCacheHostCommand(
            NnrpCacheHostAction action,
            NnrpCacheObjectId objectId,
            ulong expectedObjectVersion,
            ulong leaseId,
            CacheLeaseOwnerScope ownerScope,
            ulong ownerId,
            uint leaseTtlHintMilliseconds,
            uint objectBytes,
            uint codecBitmap,
            CachePutFlags putFlags,
            uint releaseReasonCode)
        {
            if (!Enum.IsDefined(typeof(NnrpCacheHostAction), action))
            {
                throw new ArgumentOutOfRangeException(nameof(action));
            }

            if (!Enum.IsDefined(typeof(CacheLeaseOwnerScope), ownerScope))
            {
                throw new ArgumentOutOfRangeException(nameof(ownerScope));
            }

            if (((uint)putFlags & ~((uint)CachePutFlags.Pinned | (uint)CachePutFlags.Reusable)) != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(putFlags));
            }

            Action = action;
            ObjectId = objectId;
            ExpectedObjectVersion = expectedObjectVersion;
            LeaseId = leaseId;
            OwnerScope = ownerScope;
            OwnerId = ownerId;
            LeaseTtlHintMilliseconds = leaseTtlHintMilliseconds;
            ObjectBytes = objectBytes;
            CodecBitmap = codecBitmap;
            PutFlags = putFlags;
            ReleaseReasonCode = releaseReasonCode;
        }

        public NnrpCacheHostAction Action { get; }

        public NnrpCacheObjectId ObjectId { get; }

        public ulong ExpectedObjectVersion { get; }

        public ulong LeaseId { get; }

        public CacheLeaseOwnerScope OwnerScope { get; }

        public ulong OwnerId { get; }

        public uint LeaseTtlHintMilliseconds { get; }

        public uint ObjectBytes { get; }

        public uint CodecBitmap { get; }

        public CachePutFlags PutFlags { get; }

        public uint ReleaseReasonCode { get; }

        public bool CarriesLeaseIdentity => LeaseId != 0 || ExpectedObjectVersion != 0;

        public static NnrpCacheHostCommand Query(NnrpCacheObjectId objectId, ulong expectedObjectVersion = 0)
        {
            return new NnrpCacheHostCommand(
                NnrpCacheHostAction.Query,
                objectId,
                expectedObjectVersion,
                leaseId: 0,
                ownerScope: CacheLeaseOwnerScope.Connection,
                ownerId: 0,
                leaseTtlHintMilliseconds: 0,
                objectBytes: 0,
                codecBitmap: 0,
                putFlags: CachePutFlags.None,
                releaseReasonCode: 0);
        }

        public static NnrpCacheHostCommand Query(NnrpCacheLease lease)
        {
            return new NnrpCacheHostCommand(
                NnrpCacheHostAction.Query,
                lease.ObjectId,
                lease.ObjectVersion,
                lease.LeaseId,
                lease.OwnerScope,
                lease.OwnerId,
                leaseTtlHintMilliseconds: 0,
                objectBytes: 0,
                codecBitmap: 0,
                putFlags: CachePutFlags.None,
                releaseReasonCode: 0);
        }

        public static NnrpCacheHostCommand Touch(NnrpCacheLease lease, uint leaseTtlHintMilliseconds)
        {
            return new NnrpCacheHostCommand(
                NnrpCacheHostAction.Touch,
                lease.ObjectId,
                lease.ObjectVersion,
                lease.LeaseId,
                lease.OwnerScope,
                lease.OwnerId,
                leaseTtlHintMilliseconds: leaseTtlHintMilliseconds,
                objectBytes: 0,
                codecBitmap: 0,
                putFlags: CachePutFlags.None,
                releaseReasonCode: 0);
        }

        public static NnrpCacheHostCommand Prefetch(
            NnrpCacheObjectId objectId,
            uint objectBytes,
            uint leaseTtlHintMilliseconds,
            uint codecBitmap = 0,
            CachePutFlags putFlags = CachePutFlags.Reusable)
        {
            return new NnrpCacheHostCommand(
                NnrpCacheHostAction.Prefetch,
                objectId,
                expectedObjectVersion: 0,
                leaseId: 0,
                ownerScope: CacheLeaseOwnerScope.Connection,
                ownerId: 0,
                leaseTtlHintMilliseconds: leaseTtlHintMilliseconds,
                objectBytes: objectBytes,
                codecBitmap: codecBitmap,
                putFlags: putFlags,
                releaseReasonCode: 0);
        }

        public static NnrpCacheHostCommand Release(NnrpCacheObjectId objectId, uint reasonCode = 0)
        {
            return new NnrpCacheHostCommand(
                NnrpCacheHostAction.Release,
                objectId,
                expectedObjectVersion: 0,
                leaseId: 0,
                ownerScope: CacheLeaseOwnerScope.Connection,
                ownerId: 0,
                leaseTtlHintMilliseconds: 0,
                objectBytes: 0,
                codecBitmap: 0,
                putFlags: CachePutFlags.None,
                releaseReasonCode: reasonCode);
        }

        public static NnrpCacheHostCommand Release(NnrpCacheLease lease, uint reasonCode = 0)
        {
            return new NnrpCacheHostCommand(
                NnrpCacheHostAction.Release,
                lease.ObjectId,
                lease.ObjectVersion,
                lease.LeaseId,
                lease.OwnerScope,
                lease.OwnerId,
                leaseTtlHintMilliseconds: 0,
                objectBytes: 0,
                codecBitmap: 0,
                putFlags: CachePutFlags.None,
                releaseReasonCode: reasonCode);
        }

        public ObjectReferenceBlock CreateObjectReference(ushort referenceFlags = 0)
        {
            if (!TryCreateObjectReference(referenceFlags, out var block))
            {
                throw new InvalidOperationException("Only query commands can create object reference blocks.");
            }

            return block;
        }

        public bool TryCreateObjectReference(ushort referenceFlags, out ObjectReferenceBlock block)
        {
            if (Action != NnrpCacheHostAction.Query)
            {
                block = default;
                return false;
            }

            block = new ObjectReferenceBlock(
                ObjectId.ObjectKind,
                referenceFlags,
                ObjectId.CacheNamespace,
                ObjectId.CacheKeyHigh,
                ObjectId.CacheKeyLow);
            return true;
        }

        public CachePutMetadata CreatePrefetchMetadata()
        {
            if (!TryCreatePrefetchMetadata(out var metadata))
            {
                throw new InvalidOperationException("Only prefetch commands can create CACHE_PUT metadata.");
            }

            return metadata;
        }

        public bool TryCreatePrefetchMetadata(out CachePutMetadata metadata)
        {
            if (Action != NnrpCacheHostAction.Prefetch)
            {
                metadata = default;
                return false;
            }

            metadata = new CachePutMetadata(
                ObjectId.CacheNamespace,
                ObjectId.CacheKeyHigh,
                ObjectId.CacheKeyLow,
                ObjectId.ObjectKind,
                LeaseTtlHintMilliseconds,
                ObjectBytes,
                CodecBitmap,
                PutFlags);
            return true;
        }

        public CacheInvalidateMetadata CreateReleaseMetadata()
        {
            if (!TryCreateReleaseMetadata(out var metadata))
            {
                throw new InvalidOperationException("Only release commands can create CACHE_INVALIDATE metadata.");
            }

            return metadata;
        }

        public bool TryCreateReleaseMetadata(out CacheInvalidateMetadata metadata)
        {
            if (Action != NnrpCacheHostAction.Release)
            {
                metadata = default;
                return false;
            }

            metadata = new CacheInvalidateMetadata(
                CacheInvalidateScope.ObjectKey,
                ObjectId.CacheNamespace,
                ObjectId.CacheKeyHigh,
                ObjectId.CacheKeyLow,
                ReleaseReasonCode);
            return true;
        }

        public bool MatchesLease(NnrpCacheLease lease)
        {
            if (ObjectId != lease.ObjectId)
            {
                return false;
            }

            if (ExpectedObjectVersion != 0 && ExpectedObjectVersion != lease.ObjectVersion)
            {
                return false;
            }

            return LeaseId == 0 || LeaseId == lease.LeaseId;
        }

        public bool Equals(NnrpCacheHostCommand other)
        {
            return Action == other.Action
                && ObjectId == other.ObjectId
                && ExpectedObjectVersion == other.ExpectedObjectVersion
                && LeaseId == other.LeaseId
                && OwnerScope == other.OwnerScope
                && OwnerId == other.OwnerId
                && LeaseTtlHintMilliseconds == other.LeaseTtlHintMilliseconds
                && ObjectBytes == other.ObjectBytes
                && CodecBitmap == other.CodecBitmap
                && PutFlags == other.PutFlags
                && ReleaseReasonCode == other.ReleaseReasonCode;
        }

        public override bool Equals(object obj)
        {
            return obj is NnrpCacheHostCommand other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = Action.GetHashCode();
                hash = (hash * 397) ^ ObjectId.GetHashCode();
                hash = (hash * 397) ^ ExpectedObjectVersion.GetHashCode();
                hash = (hash * 397) ^ LeaseId.GetHashCode();
                hash = (hash * 397) ^ OwnerScope.GetHashCode();
                hash = (hash * 397) ^ OwnerId.GetHashCode();
                hash = (hash * 397) ^ LeaseTtlHintMilliseconds.GetHashCode();
                hash = (hash * 397) ^ ObjectBytes.GetHashCode();
                hash = (hash * 397) ^ CodecBitmap.GetHashCode();
                hash = (hash * 397) ^ PutFlags.GetHashCode();
                hash = (hash * 397) ^ ReleaseReasonCode.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(NnrpCacheHostCommand left, NnrpCacheHostCommand right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(NnrpCacheHostCommand left, NnrpCacheHostCommand right)
        {
            return !left.Equals(right);
        }
    }

    public enum NnrpCacheHostOutcome : byte
    {
        Accepted = 0,
        Rejected = 1,
        Invalidated = 2,
    }

    public readonly struct NnrpCacheHostResult : IEquatable<NnrpCacheHostResult>
    {
        private NnrpCacheHostResult(
            NnrpCacheHostAction action,
            NnrpCacheObjectId objectId,
            NnrpCacheHostOutcome outcome,
            CacheValidationFailure failure,
            NnrpCacheLease? lease,
            uint detailCode)
        {
            if (!Enum.IsDefined(typeof(NnrpCacheHostAction), action))
            {
                throw new ArgumentOutOfRangeException(nameof(action));
            }

            if (!Enum.IsDefined(typeof(NnrpCacheHostOutcome), outcome))
            {
                throw new ArgumentOutOfRangeException(nameof(outcome));
            }

            Action = action;
            ObjectId = objectId;
            Outcome = outcome;
            Failure = failure;
            Lease = lease;
            DetailCode = detailCode;
        }

        public NnrpCacheHostAction Action { get; }

        public NnrpCacheObjectId ObjectId { get; }

        public NnrpCacheHostOutcome Outcome { get; }

        public CacheValidationFailure Failure { get; }

        public CacheErrorCode ErrorCode => Failure.ToCacheErrorCode();

        public NnrpCacheLease? Lease { get; }

        public uint DetailCode { get; }

        public bool HasLease => Lease.HasValue;

        public bool IsFailure => Failure != CacheValidationFailure.None;

        public static NnrpCacheHostResult Accepted(NnrpCacheHostCommand command, NnrpCacheLease lease, uint detailCode = 0)
        {
            if (!command.MatchesLease(lease))
            {
                throw new ArgumentException("Accepted lease does not match the cache command identity.", nameof(lease));
            }

            return new NnrpCacheHostResult(
                command.Action,
                command.ObjectId,
                NnrpCacheHostOutcome.Accepted,
                CacheValidationFailure.None,
                lease,
                detailCode);
        }

        public static NnrpCacheHostResult Rejected(
            NnrpCacheHostCommand command,
            CacheValidationFailure failure,
            uint detailCode = 0)
        {
            if (failure == CacheValidationFailure.None || !Enum.IsDefined(typeof(CacheValidationFailure), failure))
            {
                throw new ArgumentOutOfRangeException(nameof(failure));
            }

            return new NnrpCacheHostResult(
                command.Action,
                command.ObjectId,
                NnrpCacheHostOutcome.Rejected,
                failure,
                lease: null,
                detailCode);
        }

        public static NnrpCacheHostResult Invalidated(NnrpCacheHostCommand command, CacheInvalidateMetadata metadata)
        {
            if (!command.ObjectId.MatchesInvalidate(metadata))
            {
                throw new ArgumentException("Invalidation does not apply to the cache command identity.", nameof(metadata));
            }

            return new NnrpCacheHostResult(
                command.Action,
                command.ObjectId,
                NnrpCacheHostOutcome.Invalidated,
                CacheValidationFailure.None,
                lease: null,
                metadata.ReasonCode);
        }

        public bool Equals(NnrpCacheHostResult other)
        {
            return Action == other.Action
                && ObjectId == other.ObjectId
                && Outcome == other.Outcome
                && Failure == other.Failure
                && Nullable.Equals(Lease, other.Lease)
                && DetailCode == other.DetailCode;
        }

        public override bool Equals(object obj)
        {
            return obj is NnrpCacheHostResult other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = Action.GetHashCode();
                hash = (hash * 397) ^ ObjectId.GetHashCode();
                hash = (hash * 397) ^ Outcome.GetHashCode();
                hash = (hash * 397) ^ Failure.GetHashCode();
                hash = (hash * 397) ^ Lease.GetHashCode();
                hash = (hash * 397) ^ DetailCode.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(NnrpCacheHostResult left, NnrpCacheHostResult right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(NnrpCacheHostResult left, NnrpCacheHostResult right)
        {
            return !left.Equals(right);
        }
    }
}
