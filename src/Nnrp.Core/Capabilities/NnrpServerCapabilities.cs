using System.Collections.Generic;

namespace Nnrp.Core
{
    public sealed class NnrpServerCapabilities
    {
        private readonly CodecId[] acceptedCodecs;
        private readonly DTypeId[] acceptedDTypes;
        private readonly TensorLayoutId[] acceptedTensorLayouts;

        public NnrpServerCapabilities(
            IEnumerable<CodecId> acceptedCodecs,
            IEnumerable<DTypeId> acceptedDTypes,
            IEnumerable<TensorLayoutId> acceptedTensorLayouts,
            uint acceptedPayloadKindBitmap,
            uint acceptedCacheObjectBitmap,
            BudgetPolicy acceptedDegradePolicies,
            int maxConcurrentFrames,
            bool enableCache,
            int maxCacheEntries,
            int maxBodyBytes,
            int maxSectionCount,
            int maxTileCount,
            int maxViews,
            int tokenTtlSeconds,
            bool allowSessionRenewal)
        {
            this.acceptedCodecs = NnrpCapabilityValidation.CopyValues(acceptedCodecs, nameof(acceptedCodecs));
            this.acceptedDTypes = NnrpCapabilityValidation.CopyValues(acceptedDTypes, nameof(acceptedDTypes));
            this.acceptedTensorLayouts = NnrpCapabilityValidation.CopyValues(acceptedTensorLayouts, nameof(acceptedTensorLayouts));
            AcceptedCodecs = System.Array.AsReadOnly(this.acceptedCodecs);
            AcceptedDTypes = System.Array.AsReadOnly(this.acceptedDTypes);
            AcceptedTensorLayouts = System.Array.AsReadOnly(this.acceptedTensorLayouts);
            AcceptedPayloadKindBitmap = acceptedPayloadKindBitmap;
            AcceptedCacheObjectBitmap = acceptedCacheObjectBitmap;
            AcceptedDegradePolicies = acceptedDegradePolicies;
            MaxConcurrentFrames = maxConcurrentFrames;
            EnableCache = enableCache;
            MaxCacheEntries = maxCacheEntries;
            MaxBodyBytes = maxBodyBytes;
            MaxSectionCount = maxSectionCount;
            MaxTileCount = maxTileCount;
            MaxViews = maxViews;
            TokenTtlSeconds = tokenTtlSeconds;
            AllowSessionRenewal = allowSessionRenewal;
        }

        public IReadOnlyList<CodecId> AcceptedCodecs { get; }

        public IReadOnlyList<DTypeId> AcceptedDTypes { get; }

        public IReadOnlyList<TensorLayoutId> AcceptedTensorLayouts { get; }

        public uint AcceptedPayloadKindBitmap { get; }

        public uint AcceptedCacheObjectBitmap { get; }

        public BudgetPolicy AcceptedDegradePolicies { get; }

        public int MaxConcurrentFrames { get; }

        public bool EnableCache { get; }

        public int MaxCacheEntries { get; }

        public int MaxBodyBytes { get; }

        public int MaxSectionCount { get; }

        public int MaxTileCount { get; }

        public int MaxViews { get; }

        public int TokenTtlSeconds { get; }

        public bool AllowSessionRenewal { get; }

        public bool TryValidate(out string validationError)
        {
            if (!NnrpCapabilityValidation.TryValidateEnumSet(AcceptedCodecs, nameof(AcceptedCodecs), out validationError)
                || !NnrpCapabilityValidation.TryValidateEnumSet(AcceptedDTypes, nameof(AcceptedDTypes), out validationError)
                || !NnrpCapabilityValidation.TryValidateEnumSet(AcceptedTensorLayouts, nameof(AcceptedTensorLayouts), out validationError))
            {
                return false;
            }

            if (AcceptedPayloadKindBitmap == 0
                || (AcceptedPayloadKindBitmap & ~PayloadKindValidator.AllowedPayloadKindBits) != 0)
            {
                validationError = $"{nameof(AcceptedPayloadKindBitmap)} must contain at least one defined payload kind.";
                return false;
            }

            if ((AcceptedCacheObjectBitmap & ~ControlMetadataBitmaps.LowFrequencyObjectBitmap) != 0)
            {
                validationError = $"{nameof(AcceptedCacheObjectBitmap)} contains unsupported object-kind bits.";
                return false;
            }

            if (MaxConcurrentFrames <= 0)
            {
                validationError = $"{nameof(MaxConcurrentFrames)} must be greater than zero.";
                return false;
            }

            if (MaxCacheEntries < 0 || (EnableCache && MaxCacheEntries == 0))
            {
                validationError = $"{nameof(MaxCacheEntries)} must be positive when cache support is enabled.";
                return false;
            }

            if (MaxBodyBytes <= 0)
            {
                validationError = $"{nameof(MaxBodyBytes)} must be greater than zero.";
                return false;
            }

            if (MaxSectionCount <= 0)
            {
                validationError = $"{nameof(MaxSectionCount)} must be greater than zero.";
                return false;
            }

            if (MaxTileCount <= 0)
            {
                validationError = $"{nameof(MaxTileCount)} must be greater than zero.";
                return false;
            }

            if (MaxViews <= 0)
            {
                validationError = $"{nameof(MaxViews)} must be greater than zero.";
                return false;
            }

            if (TokenTtlSeconds <= 0)
            {
                validationError = $"{nameof(TokenTtlSeconds)} must be greater than zero.";
                return false;
            }

            validationError = string.Empty;
            return true;
        }
    }
}
