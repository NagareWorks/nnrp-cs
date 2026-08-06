using System;
using Nnrp.Runtime;

namespace Nnrp.Core
{
    public enum CachePolicyInvalidationReason : byte
    {
        Explicit = 0,
        DependencyInvalidated = 1,
        LeaseExpired = 2,
        VersionMismatch = 3,
        SchemaMismatch = 4,
    }

    public readonly struct CachePolicyOptions : IEquatable<CachePolicyOptions>
    {
        public CachePolicyOptions(
            bool enabled = false,
            CacheReuseScope? reuseScope = null,
            ulong expirationHintMilliseconds = 0,
            CachePolicyInvalidationReason invalidationReason = CachePolicyInvalidationReason.Explicit)
        {
            if (reuseScope.HasValue && !Enum.IsDefined(typeof(CacheReuseScope), reuseScope.Value))
            {
                throw new ArgumentOutOfRangeException(nameof(reuseScope));
            }

            if (!Enum.IsDefined(typeof(CachePolicyInvalidationReason), invalidationReason))
            {
                throw new ArgumentOutOfRangeException(nameof(invalidationReason));
            }

            if (enabled && !reuseScope.HasValue)
            {
                throw new ArgumentException("Enabled cache policy requires a reuse scope.", nameof(reuseScope));
            }

            if (!enabled && reuseScope.HasValue)
            {
                throw new ArgumentException("Disabled cache policy must not set a reuse scope.", nameof(reuseScope));
            }

            if (!enabled && expirationHintMilliseconds != 0)
            {
                throw new ArgumentException("Disabled cache policy must not set an expiration hint.", nameof(expirationHintMilliseconds));
            }

            Enabled = enabled;
            ReuseScope = reuseScope;
            ExpirationHintMilliseconds = expirationHintMilliseconds;
            InvalidationReason = invalidationReason;
        }

        public bool Enabled { get; }

        public CacheReuseScope? ReuseScope { get; }

        public ulong ExpirationHintMilliseconds { get; }

        public CachePolicyInvalidationReason InvalidationReason { get; }

        public bool Equals(CachePolicyOptions other)
        {
            return Enabled == other.Enabled
                && ReuseScope == other.ReuseScope
                && ExpirationHintMilliseconds == other.ExpirationHintMilliseconds
                && InvalidationReason == other.InvalidationReason;
        }

        public override bool Equals(object? obj)
        {
            return obj is CachePolicyOptions other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = Enabled.GetHashCode();
                hash = (hash * 397) ^ ReuseScope.GetHashCode();
                hash = (hash * 397) ^ ExpirationHintMilliseconds.GetHashCode();
                hash = (hash * 397) ^ InvalidationReason.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(CachePolicyOptions left, CachePolicyOptions right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(CachePolicyOptions left, CachePolicyOptions right)
        {
            return !left.Equals(right);
        }
    }
}
