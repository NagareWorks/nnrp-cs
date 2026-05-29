using System;
using Nnrp.Core;

namespace Nnrp.Client
{
    public sealed class ClientProfile
    {
        public NnrpTransportProfile TransportProfile { get; set; } = NnrpTransportProfile.ControlEvidence;

        public TransportPolicy TransportPolicy { get; set; } = TransportPolicy.Auto;

        public LossTolerance SessionLossTolerance { get; set; } = LossTolerance.Strict;

        public int MaxViews { get; set; } = 1;

        public bool EnableCache { get; set; } = true;

        public int MaxCacheEntries { get; set; } = 256;

        public CodecId[] SupportedCodecs { get; set; } = { CodecId.Raw };

        public DTypeId[] SupportedDTypes { get; set; } = { DTypeId.UInt8, DTypeId.Float16 };

        public TensorLayoutId[] SupportedTensorLayouts { get; set; } = { TensorLayoutId.Nhwc };

        public PayloadKind SupportedPayloadKinds { get; set; } = PayloadKind.Tensor;

        public uint SupportedObjectKinds { get; set; } = ControlMetadataBitmaps.LowFrequencyObjectBitmap;

        public BudgetPolicy SupportedDegradePolicies { get; set; } =
            BudgetPolicy.AllowPartial | BudgetPolicy.AllowStaleReuse | BudgetPolicy.AllowDegraded | BudgetPolicy.AllowDrop;

        public ushort PreferredTileWidth { get; set; } = 32;

        public ushort PreferredTileHeight { get; set; } = 32;

        public ushort MinSourceWidth { get; set; } = 1;

        public ushort MaxSourceWidth { get; set; } = 8192;

        public ushort MinSourceHeight { get; set; } = 1;

        public ushort MaxSourceHeight { get; set; } = 8192;

        public ushort MinTargetFpsTimes100 { get; set; } = 100;

        public ushort MaxTargetFpsTimes100 { get; set; } = 24000;

        public ushort LatencyBudgetMilliseconds { get; set; } = 100;

        public Func<byte[]>? AuthBlockProvider { get; set; }

        public NnrpClientCapabilities ToCapabilities()
        {
            return new NnrpClientCapabilities(
                SupportedCodecs ?? Array.Empty<CodecId>(),
                SupportedDTypes ?? Array.Empty<DTypeId>(),
                SupportedTensorLayouts ?? Array.Empty<TensorLayoutId>(),
                (uint)SupportedPayloadKinds,
                SupportedObjectKinds,
                SupportedDegradePolicies,
                MaxViews,
                EnableCache,
                MaxCacheEntries,
                PreferredTileWidth,
                PreferredTileHeight,
                MinSourceWidth,
                MaxSourceWidth,
                MinSourceHeight,
                MaxSourceHeight,
                MinTargetFpsTimes100,
                MaxTargetFpsTimes100,
                LatencyBudgetMilliseconds);
        }

        public bool TryValidate(out string validationError)
        {
            return ToCapabilities().TryValidate(out validationError);
        }

        public byte[] CreateAuthBlock()
        {
            var provider = AuthBlockProvider;
            if (provider == null)
            {
                return Array.Empty<byte>();
            }

            var authBlock = provider();
            return authBlock == null ? Array.Empty<byte>() : (byte[])authBlock.Clone();
        }

        public ClientHelloMessage CreateClientHello(uint requestedSessionId = 0, ulong traceId = 0)
        {
            var authBlock = CreateAuthBlock();
            var metadata = new ClientHelloMetadata(
                minVersionMajor: NnrpHeader.CurrentVersionMajor,
                maxVersionMajor: NnrpHeader.CurrentVersionMajor,
                supportedWireFormatBitmap: ControlMetadataBitmaps.EncodeCurrentWireFormatBitmap(),
                supportedProfileBitmap: ControlMetadataBitmaps.TensorProfileBitmap,
                supportedPayloadKindBitmap: (uint)SupportedPayloadKinds,
                supportedCodecBitmap: ControlMetadataBitmaps.EncodeCodecBitmap(SupportedCodecs ?? Array.Empty<CodecId>()),
                supportedCompressionBitmap: ControlMetadataBitmaps.EncodeCodecBitmap(SupportedCodecs ?? Array.Empty<CodecId>()),
                supportedDTypeBitmap: ControlMetadataBitmaps.EncodeDTypeBitmap(SupportedDTypes ?? Array.Empty<DTypeId>()),
                supportedLayoutBitmap: ControlMetadataBitmaps.EncodeTensorLayoutBitmap(SupportedTensorLayouts ?? Array.Empty<TensorLayoutId>()),
                cacheDigestBitmap: EnableCache ? ControlMetadataBitmaps.CacheDigestBitmap : 0u,
                cacheObjectBitmap: EnableCache ? SupportedObjectKinds : 0u,
                cacheNamespaceCount: EnableCache ? 1u : 0u,
                maxLaneCount: checked((uint)MaxViews),
                maxCacheEntries: checked((uint)MaxCacheEntries),
                maxCacheBytes: EnableCache ? ControlMetadataBitmaps.DefaultCacheBytes : 0u,
                targetCadenceX100: MaxTargetFpsTimes100,
                latencyBudgetMilliseconds: LatencyBudgetMilliseconds,
                qualityTier: ControlMetadataBitmaps.DefaultQualityTier,
                degradePolicy: (byte)SupportedDegradePolicies,
                requestedSessionId: requestedSessionId,
                authBytes: checked((uint)authBlock.Length),
                controlExtensionBytes: 0);
            var header = new NnrpHeader(
                versionMajor: NnrpHeader.CurrentVersionMajor,
                messageType: MessageType.ClientHello,
                flags: HeaderFlags.AckRequired,
                metaLength: ClientHelloMetadata.MetadataLength,
                bodyLength: checked((uint)authBlock.Length),
                sessionId: 0,
                frameId: 0,
                viewId: 0,
                routeId: 0,
                traceId: traceId);
            return new ClientHelloMessage(header, metadata, authBlock);
        }

        public ClientHelloMessage CreateClientHello(
            uint requestedSessionId,
            ulong traceId,
            TransportPolicy transportPolicy,
            TransportId preferredTransportId)
        {
            var authBlock = CreateAuthBlock();
            var extensions = new[]
            {
                new ClientLossToleranceExtension(SessionLossTolerance).ToControlExtension(),
                new ClientTransportPolicyExtension(transportPolicy, preferredTransportId).ToControlExtension(),
            };
            var extensionBytes = GetExtensionBodyLength(extensions);
            var metadata = new ClientHelloMetadata(
                minVersionMajor: NnrpHeader.CurrentVersionMajor,
                maxVersionMajor: NnrpHeader.CurrentVersionMajor,
                supportedWireFormatBitmap: ControlMetadataBitmaps.EncodeCurrentWireFormatBitmap(),
                supportedProfileBitmap: ControlMetadataBitmaps.TensorProfileBitmap,
                supportedPayloadKindBitmap: (uint)SupportedPayloadKinds,
                supportedCodecBitmap: ControlMetadataBitmaps.EncodeCodecBitmap(SupportedCodecs ?? Array.Empty<CodecId>()),
                supportedCompressionBitmap: ControlMetadataBitmaps.EncodeCodecBitmap(SupportedCodecs ?? Array.Empty<CodecId>()),
                supportedDTypeBitmap: ControlMetadataBitmaps.EncodeDTypeBitmap(SupportedDTypes ?? Array.Empty<DTypeId>()),
                supportedLayoutBitmap: ControlMetadataBitmaps.EncodeTensorLayoutBitmap(SupportedTensorLayouts ?? Array.Empty<TensorLayoutId>()),
                cacheDigestBitmap: EnableCache ? ControlMetadataBitmaps.CacheDigestBitmap : 0u,
                cacheObjectBitmap: EnableCache ? SupportedObjectKinds : 0u,
                cacheNamespaceCount: EnableCache ? 1u : 0u,
                maxLaneCount: checked((uint)MaxViews),
                maxCacheEntries: checked((uint)MaxCacheEntries),
                maxCacheBytes: EnableCache ? ControlMetadataBitmaps.DefaultCacheBytes : 0u,
                targetCadenceX100: MaxTargetFpsTimes100,
                latencyBudgetMilliseconds: LatencyBudgetMilliseconds,
                qualityTier: ControlMetadataBitmaps.DefaultQualityTier,
                degradePolicy: (byte)SupportedDegradePolicies,
                requestedSessionId: requestedSessionId,
                authBytes: checked((uint)authBlock.Length),
                controlExtensionBytes: checked((uint)extensionBytes));
            var header = new NnrpHeader(
                versionMajor: NnrpHeader.CurrentVersionMajor,
                messageType: MessageType.ClientHello,
                flags: HeaderFlags.AckRequired,
                metaLength: ClientHelloMetadata.MetadataLength,
                bodyLength: checked((uint)(authBlock.Length + extensionBytes)),
                sessionId: 0,
                frameId: 0,
                viewId: 0,
                routeId: 0,
                traceId: traceId);
            return new ClientHelloMessage(header, metadata, authBlock, extensions);
        }

        private static int GetExtensionBodyLength(ControlExtensionBlock[] extensions)
        {
            var total = 0;
            for (var i = 0; i < extensions.Length; i++)
            {
                total += extensions[i].TotalLength;
            }

            return total;
        }
    }
}
