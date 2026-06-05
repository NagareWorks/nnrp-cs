using System.Collections.Generic;

namespace Nnrp.Core
{
    public sealed class NnrpClientCapabilities
    {
        private readonly CodecId[] supportedCodecs;
        private readonly DTypeId[] supportedDTypes;
        private readonly TensorLayoutId[] supportedTensorLayouts;

        public NnrpClientCapabilities(
            IEnumerable<CodecId> supportedCodecs,
            IEnumerable<DTypeId> supportedDTypes,
            IEnumerable<TensorLayoutId> supportedTensorLayouts,
            uint supportedPayloadKindBitmap,
            uint supportedCacheObjectBitmap,
            BudgetPolicy supportedDegradePolicies,
            int maxViews,
            bool enableCache,
            int maxCacheEntries,
            ushort preferredTileWidth,
            ushort preferredTileHeight,
            ushort minSourceWidth,
            ushort maxSourceWidth,
            ushort minSourceHeight,
            ushort maxSourceHeight,
            ushort minTargetFpsTimes100,
            ushort maxTargetFpsTimes100,
            ushort latencyBudgetMilliseconds)
        {
            this.supportedCodecs = NnrpCapabilityValidation.CopyValues(supportedCodecs, nameof(supportedCodecs));
            this.supportedDTypes = NnrpCapabilityValidation.CopyValues(supportedDTypes, nameof(supportedDTypes));
            this.supportedTensorLayouts = NnrpCapabilityValidation.CopyValues(supportedTensorLayouts, nameof(supportedTensorLayouts));
            SupportedCodecs = System.Array.AsReadOnly(this.supportedCodecs);
            SupportedDTypes = System.Array.AsReadOnly(this.supportedDTypes);
            SupportedTensorLayouts = System.Array.AsReadOnly(this.supportedTensorLayouts);
            SupportedPayloadKindBitmap = supportedPayloadKindBitmap;
            SupportedCacheObjectBitmap = supportedCacheObjectBitmap;
            SupportedDegradePolicies = supportedDegradePolicies;
            MaxViews = maxViews;
            EnableCache = enableCache;
            MaxCacheEntries = maxCacheEntries;
            PreferredTileWidth = preferredTileWidth;
            PreferredTileHeight = preferredTileHeight;
            MinSourceWidth = minSourceWidth;
            MaxSourceWidth = maxSourceWidth;
            MinSourceHeight = minSourceHeight;
            MaxSourceHeight = maxSourceHeight;
            MinTargetFpsTimes100 = minTargetFpsTimes100;
            MaxTargetFpsTimes100 = maxTargetFpsTimes100;
            LatencyBudgetMilliseconds = latencyBudgetMilliseconds;
        }

        public IReadOnlyList<CodecId> SupportedCodecs { get; }

        public IReadOnlyList<DTypeId> SupportedDTypes { get; }

        public IReadOnlyList<TensorLayoutId> SupportedTensorLayouts { get; }

        public uint SupportedPayloadKindBitmap { get; }

        public uint SupportedCacheObjectBitmap { get; }

        public BudgetPolicy SupportedDegradePolicies { get; }

        public int MaxViews { get; }

        public bool EnableCache { get; }

        public int MaxCacheEntries { get; }

        public ushort PreferredTileWidth { get; }

        public ushort PreferredTileHeight { get; }

        public ushort MinSourceWidth { get; }

        public ushort MaxSourceWidth { get; }

        public ushort MinSourceHeight { get; }

        public ushort MaxSourceHeight { get; }

        public ushort MinTargetFpsTimes100 { get; }

        public ushort MaxTargetFpsTimes100 { get; }

        public ushort LatencyBudgetMilliseconds { get; }

        public bool TryValidate(out string validationError)
        {
            if (!NnrpCapabilityValidation.TryValidateEnumSet(SupportedCodecs, nameof(SupportedCodecs), out validationError)
                || !NnrpCapabilityValidation.TryValidateEnumSet(SupportedDTypes, nameof(SupportedDTypes), out validationError)
                || !NnrpCapabilityValidation.TryValidateEnumSet(SupportedTensorLayouts, nameof(SupportedTensorLayouts), out validationError))
            {
                return false;
            }

            if (SupportedPayloadKindBitmap == 0
                || (SupportedPayloadKindBitmap & ~PayloadKindValidator.AllowedPayloadKindBits) != 0)
            {
                validationError = $"{nameof(SupportedPayloadKindBitmap)} must contain at least one defined payload kind.";
                return false;
            }

            if ((SupportedCacheObjectBitmap & ~ControlMetadataBitmaps.LowFrequencyObjectBitmap) != 0)
            {
                validationError = $"{nameof(SupportedCacheObjectBitmap)} contains unsupported object-kind bits.";
                return false;
            }

            if (MaxViews <= 0)
            {
                validationError = $"{nameof(MaxViews)} must be greater than zero.";
                return false;
            }

            if (MaxCacheEntries < 0 || (EnableCache && MaxCacheEntries == 0))
            {
                validationError = $"{nameof(MaxCacheEntries)} must be positive when cache support is enabled.";
                return false;
            }

            if (PreferredTileWidth == 0 || PreferredTileHeight == 0)
            {
                validationError = "Preferred tile dimensions must be greater than zero.";
                return false;
            }

            if (MinSourceWidth == 0 || MaxSourceWidth < MinSourceWidth)
            {
                validationError = "Source width range is invalid.";
                return false;
            }

            if (MinSourceHeight == 0 || MaxSourceHeight < MinSourceHeight)
            {
                validationError = "Source height range is invalid.";
                return false;
            }

            if (MinTargetFpsTimes100 == 0 || MaxTargetFpsTimes100 < MinTargetFpsTimes100)
            {
                validationError = "Target FPS range is invalid.";
                return false;
            }

            if (LatencyBudgetMilliseconds == 0)
            {
                validationError = $"{nameof(LatencyBudgetMilliseconds)} must be greater than zero.";
                return false;
            }

            validationError = string.Empty;
            return true;
        }
    }
}
