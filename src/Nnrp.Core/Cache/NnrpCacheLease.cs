using System;

namespace Nnrp.Core
{
    public readonly struct NnrpCacheObjectId : IEquatable<NnrpCacheObjectId>
    {
        public NnrpCacheObjectId(uint cacheNamespace, ulong cacheKeyHigh, ulong cacheKeyLow, CacheObjectKind objectKind)
        {
            if (!Enum.IsDefined(typeof(CacheObjectKind), objectKind))
            {
                throw new ArgumentOutOfRangeException(nameof(objectKind));
            }

            CacheNamespace = cacheNamespace;
            CacheKeyHigh = cacheKeyHigh;
            CacheKeyLow = cacheKeyLow;
            ObjectKind = objectKind;
        }

        public uint CacheNamespace { get; }

        public ulong CacheKeyHigh { get; }

        public ulong CacheKeyLow { get; }

        public CacheObjectKind ObjectKind { get; }

        public static NnrpCacheObjectId FromCachePutMetadata(CachePutMetadata metadata)
        {
            return new NnrpCacheObjectId(
                metadata.CacheNamespace,
                metadata.CacheKeyHigh,
                metadata.CacheKeyLow,
                metadata.ObjectKind);
        }

        public bool MatchesInvalidate(CacheInvalidateMetadata metadata)
        {
            switch (metadata.InvalidateScope)
            {
                case CacheInvalidateScope.WholeSession:
                    return true;
                case CacheInvalidateScope.Namespace:
                    return CacheNamespace == metadata.CacheNamespace;
                case CacheInvalidateScope.ObjectKind:
                    return CacheNamespace == metadata.CacheNamespace
                        && (ulong)(uint)ObjectKind == metadata.CacheKeyHigh;
                case CacheInvalidateScope.ObjectKey:
                    return CacheNamespace == metadata.CacheNamespace
                        && CacheKeyHigh == metadata.CacheKeyHigh
                        && CacheKeyLow == metadata.CacheKeyLow;
                default:
                    return false;
            }
        }

        public bool Equals(NnrpCacheObjectId other)
        {
            return CacheNamespace == other.CacheNamespace
                && CacheKeyHigh == other.CacheKeyHigh
                && CacheKeyLow == other.CacheKeyLow
                && ObjectKind == other.ObjectKind;
        }

        public override bool Equals(object obj)
        {
            return obj is NnrpCacheObjectId other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = CacheNamespace.GetHashCode();
                hash = (hash * 397) ^ CacheKeyHigh.GetHashCode();
                hash = (hash * 397) ^ CacheKeyLow.GetHashCode();
                hash = (hash * 397) ^ ObjectKind.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(NnrpCacheObjectId left, NnrpCacheObjectId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(NnrpCacheObjectId left, NnrpCacheObjectId right)
        {
            return !left.Equals(right);
        }
    }

    public enum CacheLeaseOwnerScope : byte
    {
        Connection = 0,
        Session = 1,
        Operation = 2,
    }

    public readonly struct NnrpCacheLease : IEquatable<NnrpCacheLease>
    {
        public NnrpCacheLease(
            NnrpCacheObjectId objectId,
            ulong objectVersion,
            ulong leaseId,
            CacheLeaseOwnerScope ownerScope,
            ulong ownerId,
            ulong grantedAtMilliseconds,
            uint ttlMilliseconds)
        {
            if (!Enum.IsDefined(typeof(CacheLeaseOwnerScope), ownerScope))
            {
                throw new ArgumentOutOfRangeException(nameof(ownerScope));
            }

            ObjectId = objectId;
            ObjectVersion = objectVersion;
            LeaseId = leaseId;
            OwnerScope = ownerScope;
            OwnerId = ownerId;
            GrantedAtMilliseconds = grantedAtMilliseconds;
            TtlMilliseconds = ttlMilliseconds;
        }

        public NnrpCacheObjectId ObjectId { get; }

        public ulong ObjectVersion { get; }

        public ulong LeaseId { get; }

        public CacheLeaseOwnerScope OwnerScope { get; }

        public ulong OwnerId { get; }

        public ulong GrantedAtMilliseconds { get; }

        public uint TtlMilliseconds { get; }

        public ulong ExpiresAtMilliseconds
        {
            get
            {
                var ttl = (ulong)TtlMilliseconds;
                return ulong.MaxValue - GrantedAtMilliseconds < ttl
                    ? ulong.MaxValue
                    : GrantedAtMilliseconds + ttl;
            }
        }

        public bool IsExpiredAt(ulong nowMilliseconds)
        {
            return nowMilliseconds >= ExpiresAtMilliseconds;
        }

        public bool TryValidateLiveAt(ulong nowMilliseconds, out CacheValidationFailure failure)
        {
            if (IsExpiredAt(nowMilliseconds))
            {
                failure = CacheValidationFailure.LeaseExpired;
                return false;
            }

            failure = CacheValidationFailure.None;
            return true;
        }

        public bool TryValidateVersion(ulong expectedVersion, out CacheValidationFailure failure)
        {
            if (ObjectVersion != expectedVersion)
            {
                failure = CacheValidationFailure.VersionMismatch;
                return false;
            }

            failure = CacheValidationFailure.None;
            return true;
        }

        public NnrpCacheLease WithRenewedTtl(uint ttlMilliseconds)
        {
            return new NnrpCacheLease(
                ObjectId,
                ObjectVersion,
                LeaseId,
                OwnerScope,
                OwnerId,
                GrantedAtMilliseconds,
                ttlMilliseconds);
        }

        public bool Equals(NnrpCacheLease other)
        {
            return ObjectId == other.ObjectId
                && ObjectVersion == other.ObjectVersion
                && LeaseId == other.LeaseId
                && OwnerScope == other.OwnerScope
                && OwnerId == other.OwnerId
                && GrantedAtMilliseconds == other.GrantedAtMilliseconds
                && TtlMilliseconds == other.TtlMilliseconds;
        }

        public override bool Equals(object obj)
        {
            return obj is NnrpCacheLease other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = ObjectId.GetHashCode();
                hash = (hash * 397) ^ ObjectVersion.GetHashCode();
                hash = (hash * 397) ^ LeaseId.GetHashCode();
                hash = (hash * 397) ^ OwnerScope.GetHashCode();
                hash = (hash * 397) ^ OwnerId.GetHashCode();
                hash = (hash * 397) ^ GrantedAtMilliseconds.GetHashCode();
                hash = (hash * 397) ^ TtlMilliseconds.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(NnrpCacheLease left, NnrpCacheLease right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(NnrpCacheLease left, NnrpCacheLease right)
        {
            return !left.Equals(right);
        }
    }

    public readonly struct NnrpCacheObjectVersion : IEquatable<NnrpCacheObjectVersion>
    {
        public NnrpCacheObjectVersion(
            NnrpCacheObjectId objectId,
            ulong objectVersion,
            uint schemaId = 0,
            uint schemaVersion = 0)
        {
            ObjectId = objectId;
            ObjectVersion = objectVersion;
            SchemaId = schemaId;
            SchemaVersion = schemaVersion;
        }

        public NnrpCacheObjectId ObjectId { get; }

        public ulong ObjectVersion { get; }

        public uint SchemaId { get; }

        public uint SchemaVersion { get; }

        public bool Equals(NnrpCacheObjectVersion other)
        {
            return ObjectId == other.ObjectId
                && ObjectVersion == other.ObjectVersion
                && SchemaId == other.SchemaId
                && SchemaVersion == other.SchemaVersion;
        }

        public override bool Equals(object obj)
        {
            return obj is NnrpCacheObjectVersion other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = ObjectId.GetHashCode();
                hash = (hash * 397) ^ ObjectVersion.GetHashCode();
                hash = (hash * 397) ^ SchemaId.GetHashCode();
                hash = (hash * 397) ^ SchemaVersion.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(NnrpCacheObjectVersion left, NnrpCacheObjectVersion right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(NnrpCacheObjectVersion left, NnrpCacheObjectVersion right)
        {
            return !left.Equals(right);
        }
    }

    public enum NnrpCacheLeaseOutcome : byte
    {
        Valid = 0,
        Expired = 1,
        Renewed = 2,
        Released = 3,
        Missing = 4,
    }

    public sealed class NnrpCacheLeaseResult
    {
        public NnrpCacheLeaseResult(
            NnrpCacheObjectId objectId,
            NnrpCacheLeaseOutcome outcome,
            NnrpCacheLease? lease = null,
            NnrpCacheObjectVersion? objectVersion = null,
            string? diagnostic = null)
        {
            if (!Enum.IsDefined(typeof(NnrpCacheLeaseOutcome), outcome))
            {
                throw new ArgumentOutOfRangeException(nameof(outcome));
            }

            if (lease.HasValue && lease.Value.ObjectId != objectId)
            {
                throw new ArgumentException("Lease identity must match the result identity.", nameof(lease));
            }

            if (objectVersion.HasValue && objectVersion.Value.ObjectId != objectId)
            {
                throw new ArgumentException("Object version identity must match the result identity.", nameof(objectVersion));
            }

            ObjectId = objectId;
            Outcome = outcome;
            Lease = lease;
            ObjectVersion = objectVersion;
            Diagnostic = diagnostic;
        }

        public NnrpCacheObjectId ObjectId { get; }

        public NnrpCacheLeaseOutcome Outcome { get; }

        public NnrpCacheLease? Lease { get; }

        public NnrpCacheObjectVersion? ObjectVersion { get; }

        public string? Diagnostic { get; }
    }

    public enum CacheValidationFailure
    {
        None = 0,
        Miss = 1,
        LeaseExpired = 2,
        VersionMismatch = 3,
        DependencyInvalid = 4,
        SchemaMismatch = 5,
    }

    public static class CacheValidationFailureExtensions
    {
        public static CacheErrorCode ToCacheErrorCode(this CacheValidationFailure failure)
        {
            switch (failure)
            {
                case CacheValidationFailure.None:
                    return CacheErrorCode.None;
                case CacheValidationFailure.Miss:
                    return CacheErrorCode.CacheMiss;
                case CacheValidationFailure.LeaseExpired:
                    return CacheErrorCode.LeaseExpired;
                case CacheValidationFailure.VersionMismatch:
                    return CacheErrorCode.VersionMismatch;
                case CacheValidationFailure.DependencyInvalid:
                    return CacheErrorCode.DependencyInvalid;
                case CacheValidationFailure.SchemaMismatch:
                    return CacheErrorCode.SchemaMismatch;
                default:
                    throw new ArgumentOutOfRangeException(nameof(failure));
            }
        }
    }

    public readonly struct NnrpCacheDependency : IEquatable<NnrpCacheDependency>
    {
        public NnrpCacheDependency(NnrpCacheObjectId objectId, ulong requiredVersion)
        {
            ObjectId = objectId;
            RequiredVersion = requiredVersion;
        }

        public NnrpCacheObjectId ObjectId { get; }

        public ulong RequiredVersion { get; }

        public bool Equals(NnrpCacheDependency other)
        {
            return ObjectId == other.ObjectId && RequiredVersion == other.RequiredVersion;
        }

        public override bool Equals(object obj)
        {
            return obj is NnrpCacheDependency other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (ObjectId.GetHashCode() * 397) ^ RequiredVersion.GetHashCode();
            }
        }
    }

    public readonly struct NnrpCacheDependencyState : IEquatable<NnrpCacheDependencyState>
    {
        public NnrpCacheDependencyState(NnrpCacheObjectId objectId, ulong currentVersion, bool invalidated)
        {
            ObjectId = objectId;
            CurrentVersion = currentVersion;
            Invalidated = invalidated;
        }

        public NnrpCacheObjectId ObjectId { get; }

        public ulong CurrentVersion { get; }

        public bool Invalidated { get; }

        public bool Equals(NnrpCacheDependencyState other)
        {
            return ObjectId == other.ObjectId
                && CurrentVersion == other.CurrentVersion
                && Invalidated == other.Invalidated;
        }

        public override bool Equals(object obj)
        {
            return obj is NnrpCacheDependencyState other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = ObjectId.GetHashCode();
                hash = (hash * 397) ^ CurrentVersion.GetHashCode();
                hash = (hash * 397) ^ Invalidated.GetHashCode();
                return hash;
            }
        }
    }

    public static class NnrpCacheLeaseValidation
    {
        public static bool TryValidateMonotonicVersion(
            ulong currentVersion,
            ulong nextVersion,
            out CacheValidationFailure failure)
        {
            if (nextVersion <= currentVersion)
            {
                failure = CacheValidationFailure.VersionMismatch;
                return false;
            }

            failure = CacheValidationFailure.None;
            return true;
        }

        public static bool TryValidateDependencies(
            ReadOnlySpan<NnrpCacheDependency> dependencies,
            ReadOnlySpan<NnrpCacheDependencyState> states,
            out CacheValidationFailure failure)
        {
            for (var dependencyIndex = 0; dependencyIndex < dependencies.Length; dependencyIndex += 1)
            {
                var dependency = dependencies[dependencyIndex];
                if (!TryFindState(dependency.ObjectId, states, out var state)
                    || state.Invalidated
                    || state.CurrentVersion != dependency.RequiredVersion)
                {
                    failure = CacheValidationFailure.DependencyInvalid;
                    return false;
                }
            }

            failure = CacheValidationFailure.None;
            return true;
        }

        private static bool TryFindState(
            NnrpCacheObjectId objectId,
            ReadOnlySpan<NnrpCacheDependencyState> states,
            out NnrpCacheDependencyState state)
        {
            for (var index = 0; index < states.Length; index += 1)
            {
                if (states[index].ObjectId == objectId)
                {
                    state = states[index];
                    return true;
                }
            }

            state = default;
            return false;
        }
    }
}
