using System;
using Nnrp.Core;

namespace Nnrp.Server
{
    public sealed class ServerProfile
    {
        public int MaxConcurrentFrames { get; set; } = 1;

        public bool EnableCache { get; set; } = true;

        public int MaxCacheEntries { get; set; } = 256;

        public CodecId[] AcceptedCodecs { get; set; } = { CodecId.Raw };

        public DTypeId[] AcceptedDTypes { get; set; } = { DTypeId.UInt8, DTypeId.Float16 };

        public TensorLayoutId[] AcceptedTensorLayouts { get; set; } = { TensorLayoutId.Nhwc };

        public PayloadKind AcceptedPayloadKinds { get; set; } = PayloadKind.Tensor;

        public uint AcceptedObjectKinds { get; set; } = ControlMetadataBitmaps.LowFrequencyObjectBitmap;

        public BudgetPolicy AcceptedDegradePolicies { get; set; } =
            BudgetPolicy.AllowPartial | BudgetPolicy.AllowStaleReuse | BudgetPolicy.AllowDegraded | BudgetPolicy.AllowDrop;

        public int MaxBodyBytes { get; set; } = 16 * 1024 * 1024;

        public int MaxSectionCount { get; set; } = 16;

        public int MaxTileCount { get; set; } = 8192;

        public int MaxViews { get; set; } = 1;

        public int TokenTtlSeconds { get; set; } = 300;

        public bool AllowSessionRenewal { get; set; } = true;

        public NnrpServerCapabilities ToCapabilities()
        {
            return new NnrpServerCapabilities(
                AcceptedCodecs ?? Array.Empty<CodecId>(),
                AcceptedDTypes ?? Array.Empty<DTypeId>(),
                AcceptedTensorLayouts ?? Array.Empty<TensorLayoutId>(),
                (uint)AcceptedPayloadKinds,
                AcceptedObjectKinds,
                AcceptedDegradePolicies,
                MaxConcurrentFrames,
                EnableCache,
                MaxCacheEntries,
                MaxBodyBytes,
                MaxSectionCount,
                MaxTileCount,
                MaxViews,
                TokenTtlSeconds,
                AllowSessionRenewal);
        }

        public bool TryValidate(out string validationError)
        {
            return ToCapabilities().TryValidate(out validationError);
        }

        public ServerHelloAckMessage CreateServerHelloAck(
            uint sessionId,
            NnrpCapabilityNegotiationResult negotiationResult,
            ulong traceId = 0)
        {
            if (!negotiationResult.IsAccepted)
            {
                throw new ArgumentException("ServerHelloAck requires an accepted negotiation result.", nameof(negotiationResult));
            }

            var metadata = new ServerHelloAckMetadata(
                NnrpHeader.CurrentVersionMajor,
                NnrpHeader.CurrentWireFormat,
                authStatus: 0,
                reserved0: 0,
                sessionId: sessionId,
                acceptedProfileBitmap: ControlMetadataBitmaps.TensorProfileBitmap,
                acceptedPayloadKindBitmap: negotiationResult.Selection.PayloadKindBitmap,
                acceptedCodecBitmap: ControlMetadataBitmaps.EncodeCodecBitmap(AcceptedCodecs ?? Array.Empty<CodecId>()),
                acceptedCompressionBitmap: ControlMetadataBitmaps.EncodeCodecBitmap(AcceptedCodecs ?? Array.Empty<CodecId>()),
                acceptedDTypeBitmap: ControlMetadataBitmaps.EncodeDTypeBitmap(AcceptedDTypes ?? Array.Empty<DTypeId>()),
                acceptedLayoutBitmap: ControlMetadataBitmaps.EncodeTensorLayoutBitmap(AcceptedTensorLayouts ?? Array.Empty<TensorLayoutId>()),
                cacheDigestBitmap: negotiationResult.Selection.EnableCache ? ControlMetadataBitmaps.CacheDigestBitmap : 0u,
                cacheObjectBitmap: negotiationResult.Selection.EnableCache ? negotiationResult.Selection.CacheObjectBitmap : 0u,
                maxCacheEntries: checked((uint)negotiationResult.Selection.MaxCacheEntries),
                maxCacheBytes: negotiationResult.Selection.EnableCache ? ControlMetadataBitmaps.DefaultCacheBytes : 0u,
                maxLaneCount: checked((uint)negotiationResult.Selection.MaxViews),
                maxConcurrentFrames: checked((uint)MaxConcurrentFrames),
                targetCadenceX100: 6000,
                latencyBudgetMilliseconds: 16,
                qualityTier: ControlMetadataBitmaps.DefaultQualityTier,
                degradePolicy: (byte)negotiationResult.Selection.DegradePolicies,
                maxBodyBytes: checked((uint)MaxBodyBytes),
                tokenTtlMilliseconds: checked((uint)(negotiationResult.Selection.TokenTtlSeconds * 1000)),
                retryAfterMilliseconds: 0,
                controlExtensionBytes: 0,
                serverFlags: 0);
            var header = new NnrpHeader(
                versionMajor: NnrpHeader.CurrentVersionMajor,
                messageType: MessageType.ServerHelloAck,
                flags: HeaderFlags.None,
                metaLength: ServerHelloAckMetadata.MetadataLength,
                bodyLength: 0,
                sessionId: sessionId,
                frameId: 0,
                viewId: 0,
                routeId: 0,
                traceId: traceId);
            return new ServerHelloAckMessage(header, metadata);
        }
    }
}
