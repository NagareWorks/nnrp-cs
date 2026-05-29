namespace Nnrp.Core
{
    public readonly struct NnrpCapabilitySelection
    {
        public NnrpCapabilitySelection(
            CodecId codec,
            DTypeId dtype,
            TensorLayoutId tensorLayout,
            uint payloadKindBitmap,
            uint cacheObjectBitmap,
            BudgetPolicy degradePolicies,
            int maxViews,
            bool enableCache,
            int maxCacheEntries,
            int maxConcurrentFrames,
            int maxBodyBytes,
            int maxSectionCount,
            int maxTileCount,
            int tokenTtlSeconds,
            bool allowSessionRenewal)
        {
            Codec = codec;
            DType = dtype;
            TensorLayout = tensorLayout;
            PayloadKindBitmap = payloadKindBitmap;
            CacheObjectBitmap = cacheObjectBitmap;
            DegradePolicies = degradePolicies;
            MaxViews = maxViews;
            EnableCache = enableCache;
            MaxCacheEntries = maxCacheEntries;
            MaxConcurrentFrames = maxConcurrentFrames;
            MaxBodyBytes = maxBodyBytes;
            MaxSectionCount = maxSectionCount;
            MaxTileCount = maxTileCount;
            TokenTtlSeconds = tokenTtlSeconds;
            AllowSessionRenewal = allowSessionRenewal;
        }

        public CodecId Codec { get; }

        public DTypeId DType { get; }

        public TensorLayoutId TensorLayout { get; }

        public uint PayloadKindBitmap { get; }

        public uint CacheObjectBitmap { get; }

        public BudgetPolicy DegradePolicies { get; }

        public int MaxViews { get; }

        public bool EnableCache { get; }

        public int MaxCacheEntries { get; }

        public int MaxConcurrentFrames { get; }

        public int MaxBodyBytes { get; }

        public int MaxSectionCount { get; }

        public int MaxTileCount { get; }

        public int TokenTtlSeconds { get; }

        public bool AllowSessionRenewal { get; }
    }
}
