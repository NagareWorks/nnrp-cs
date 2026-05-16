using Nnrp.Server;
using Nnrp.Core;
using Xunit;

namespace Nnrp.Server.Tests
{
    public sealed class ServerProfileTests
    {
        [Fact]
        public void DefaultsMatchCurrentSerialFrameBaseline()
        {
            var profile = new ServerProfile();

            Assert.Equal(1, profile.MaxConcurrentFrames);
            Assert.True(profile.EnableCache);
            Assert.Equal(256, profile.MaxCacheEntries);
            Assert.Equal(new[] { CodecId.Raw }, profile.AcceptedCodecs);
            Assert.Equal(new[] { DTypeId.UInt8, DTypeId.Float16 }, profile.AcceptedDTypes);
            Assert.Equal(new[] { TensorLayoutId.Nhwc }, profile.AcceptedTensorLayouts);
            Assert.Equal(PayloadKind.Tensor, profile.AcceptedPayloadKinds);
            Assert.Equal(ControlMetadataBitmaps.LowFrequencyObjectBitmap, profile.AcceptedObjectKinds);
            Assert.Equal(
                BudgetPolicy.AllowPartial | BudgetPolicy.AllowStaleReuse | BudgetPolicy.AllowDegraded | BudgetPolicy.AllowDrop,
                profile.AcceptedDegradePolicies);
            Assert.Equal(16 * 1024 * 1024, profile.MaxBodyBytes);
            Assert.Equal(16, profile.MaxSectionCount);
            Assert.Equal(8192, profile.MaxTileCount);
            Assert.Equal(1, profile.MaxViews);
            Assert.Equal(300, profile.TokenTtlSeconds);
            Assert.True(profile.AllowSessionRenewal);
            Assert.True(profile.TryValidate(out var validationError));
            Assert.Equal(string.Empty, validationError);
        }

        [Fact]
        public void ToCapabilitiesCopiesServerLimits()
        {
            var profile = new ServerProfile
            {
                MaxConcurrentFrames = 4,
                EnableCache = false,
                MaxCacheEntries = 0,
                AcceptedCodecs = new[] { CodecId.Lz4, CodecId.Raw },
                AcceptedDTypes = new[] { DTypeId.Float16 },
                AcceptedTensorLayouts = new[] { TensorLayoutId.Nhwc, TensorLayoutId.Nchw },
                AcceptedPayloadKinds = PayloadKind.Tensor | PayloadKind.StructuredEvent,
                AcceptedObjectKinds = ControlMetadataBitmaps.BuildCacheObjectBitmap(CacheObjectKind.CameraBlock, CacheObjectKind.PayloadLayoutTemplate),
                AcceptedDegradePolicies = BudgetPolicy.AllowPartial | BudgetPolicy.AllowDegraded,
                MaxBodyBytes = 1024 * 1024,
                MaxSectionCount = 8,
                MaxTileCount = 2048,
                MaxViews = 2,
                TokenTtlSeconds = 120,
                AllowSessionRenewal = false,
            };

            var capabilities = profile.ToCapabilities();

            Assert.True(capabilities.TryValidate(out _));
            Assert.Equal(4, capabilities.MaxConcurrentFrames);
            Assert.False(capabilities.EnableCache);
            Assert.Equal(0, capabilities.MaxCacheEntries);
            Assert.Equal(profile.AcceptedCodecs, capabilities.AcceptedCodecs);
            Assert.Equal(profile.AcceptedDTypes, capabilities.AcceptedDTypes);
            Assert.Equal(profile.AcceptedTensorLayouts, capabilities.AcceptedTensorLayouts);
            Assert.Equal((uint)(PayloadKind.Tensor | PayloadKind.StructuredEvent), capabilities.AcceptedPayloadKindBitmap);
            Assert.Equal(ControlMetadataBitmaps.BuildCacheObjectBitmap(CacheObjectKind.CameraBlock, CacheObjectKind.PayloadLayoutTemplate), capabilities.AcceptedCacheObjectBitmap);
            Assert.Equal(BudgetPolicy.AllowPartial | BudgetPolicy.AllowDegraded, capabilities.AcceptedDegradePolicies);
            Assert.Equal(1024 * 1024, capabilities.MaxBodyBytes);
            Assert.Equal(8, capabilities.MaxSectionCount);
            Assert.Equal(2048, capabilities.MaxTileCount);
            Assert.Equal(2, capabilities.MaxViews);
            Assert.Equal(120, capabilities.TokenTtlSeconds);
            Assert.False(capabilities.AllowSessionRenewal);
        }

        [Fact]
        public void TryValidateRejectsInvalidServerProfileValues()
        {
            var profile = new ServerProfile
            {
                MaxBodyBytes = 0,
            };

            Assert.False(profile.TryValidate(out var validationError));
            Assert.Contains(nameof(ServerProfile.MaxBodyBytes), validationError);

            profile.MaxBodyBytes = 1;
            profile.AcceptedTensorLayouts = new[] { TensorLayoutId.Nhwc, TensorLayoutId.Nhwc };
            Assert.False(profile.TryValidate(out validationError));
            Assert.Contains("duplicate", validationError);
        }

        [Fact]
        public void CreateServerHelloAckMapsAcceptedNegotiationIntoMetadata()
        {
            var profile = new ServerProfile
            {
                MaxConcurrentFrames = 2,
                EnableCache = true,
                MaxCacheEntries = 16,
                AcceptedCodecs = new[] { CodecId.Lz4, CodecId.Raw },
                AcceptedDTypes = new[] { DTypeId.Float16 },
                AcceptedTensorLayouts = new[] { TensorLayoutId.Nhwc },
                AcceptedPayloadKinds = PayloadKind.Tensor | PayloadKind.StructuredEvent,
                AcceptedObjectKinds = ControlMetadataBitmaps.BuildCacheObjectBitmap(CacheObjectKind.CameraBlock, CacheObjectKind.PayloadLayoutTemplate),
                AcceptedDegradePolicies = BudgetPolicy.AllowPartial | BudgetPolicy.AllowDegraded,
                MaxBodyBytes = 1024 * 1024,
                MaxSectionCount = 8,
                MaxTileCount = 2048,
                MaxViews = 1,
                TokenTtlSeconds = 120,
            };
            var negotiation = NnrpCapabilityNegotiationResult.Accepted(
                new NnrpCapabilitySelection(
                    CodecId.Lz4,
                    DTypeId.Float16,
                    TensorLayoutId.Nhwc,
                    (uint)(PayloadKind.Tensor | PayloadKind.StructuredEvent),
                    ControlMetadataBitmaps.BuildCacheObjectBitmap(CacheObjectKind.CameraBlock, CacheObjectKind.PayloadLayoutTemplate),
                    BudgetPolicy.AllowPartial | BudgetPolicy.AllowDegraded,
                    maxViews: 1,
                    enableCache: true,
                    maxCacheEntries: 8,
                    maxConcurrentFrames: 2,
                    maxBodyBytes: 1024 * 1024,
                    maxSectionCount: 8,
                    maxTileCount: 2048,
                    tokenTtlSeconds: 120,
                    allowSessionRenewal: true));

            var ack = profile.CreateServerHelloAck(sessionId: 77, negotiation, traceId: 88);

            Assert.Equal(MessageType.ServerHelloAck, ack.Header.MessageType);
            Assert.Equal(77u, ack.Metadata.SessionId);
            Assert.Equal(ControlMetadataBitmaps.TensorProfileBitmap, ack.Metadata.AcceptedProfileBitmap);
            Assert.Equal((uint)(PayloadKind.Tensor | PayloadKind.StructuredEvent), ack.Metadata.AcceptedPayloadKindBitmap);
            Assert.Equal(0x3u, ack.Metadata.AcceptedCodecBitmap);
            Assert.Equal(1u, ack.Metadata.CacheEnabled);
            Assert.Equal(ControlMetadataBitmaps.BuildCacheObjectBitmap(CacheObjectKind.CameraBlock, CacheObjectKind.PayloadLayoutTemplate), ack.Metadata.CacheObjectBitmap);
            Assert.Equal(8u, ack.Metadata.MaxCacheEntries);
            Assert.Equal(1u, ack.Metadata.MaxLaneCount);
            Assert.Equal(6000u, ack.Metadata.TargetCadenceX100);
            Assert.Equal(120000u, ack.Metadata.TokenTtlMilliseconds);
            Assert.Equal((uint)(BudgetPolicy.AllowPartial | BudgetPolicy.AllowDegraded), ack.Metadata.DegradePolicy);
        }

        [Fact]
        public void CreateServerHelloAckCanEmitCurrentStage()
        {
            var profile = new ServerProfile();
            var negotiation = NnrpCapabilityNegotiationResult.Accepted(
                new NnrpCapabilitySelection(
                    CodecId.Raw,
                    DTypeId.UInt8,
                    TensorLayoutId.Nhwc,
                    (uint)PayloadKind.Tensor,
                    ControlMetadataBitmaps.LowFrequencyObjectBitmap,
                    BudgetPolicy.AllowPartial,
                    maxViews: 1,
                    enableCache: true,
                    maxCacheEntries: 8,
                    maxConcurrentFrames: 1,
                    maxBodyBytes: 1024 * 1024,
                    maxSectionCount: 8,
                    maxTileCount: 2048,
                    tokenTtlSeconds: 120,
                    allowSessionRenewal: true));

            var ack = profile.CreateServerHelloAck(
                sessionId: 77,
                negotiationResult: negotiation,
                traceId: 88);

            Assert.Equal(NnrpHeader.CurrentWireFormat, ack.Header.WireFormat);
            Assert.Equal((uint)NnrpHeader.CurrentWireFormat, ack.Metadata.SelectedWireFormat);
        }
    }
}
