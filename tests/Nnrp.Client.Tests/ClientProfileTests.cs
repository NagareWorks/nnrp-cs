using Nnrp.Client;
using Nnrp.Core;
using Xunit;

namespace Nnrp.Client.Tests
{
    public sealed class ClientProfileTests
    {
        [Fact]
        public void DefaultsMatchCurrentSingleViewBaseline()
        {
            var profile = new ClientProfile();

            Assert.Equal(NnrpTransportProfile.ControlEvidence, profile.TransportProfile);
            Assert.Equal(LossTolerance.Strict, profile.SessionLossTolerance);
            Assert.Equal(1, profile.MaxViews);
            Assert.True(profile.EnableCache);
            Assert.Equal(256, profile.MaxCacheEntries);
            Assert.Equal(new[] { CodecId.Raw }, profile.SupportedCodecs);
            Assert.Equal(new[] { DTypeId.UInt8, DTypeId.Float16 }, profile.SupportedDTypes);
            Assert.Equal(new[] { TensorLayoutId.Nhwc }, profile.SupportedTensorLayouts);
            Assert.Equal(PayloadKind.Tensor, profile.SupportedPayloadKinds);
            Assert.Equal(ControlMetadataBitmaps.LowFrequencyObjectBitmap, profile.SupportedObjectKinds);
            Assert.Equal(
                BudgetPolicy.AllowPartial | BudgetPolicy.AllowStaleReuse | BudgetPolicy.AllowDegraded | BudgetPolicy.AllowDrop,
                profile.SupportedDegradePolicies);
            Assert.Equal(32, profile.PreferredTileWidth);
            Assert.Equal(32, profile.PreferredTileHeight);
            Assert.Equal(1, profile.MinSourceWidth);
            Assert.Equal(8192, profile.MaxSourceWidth);
            Assert.Equal(1, profile.MinSourceHeight);
            Assert.Equal(8192, profile.MaxSourceHeight);
            Assert.Equal(100, profile.MinTargetFpsTimes100);
            Assert.Equal(24000, profile.MaxTargetFpsTimes100);
            Assert.Equal(100, profile.LatencyBudgetMilliseconds);
            Assert.True(profile.TryValidate(out var validationError));
            Assert.Equal(string.Empty, validationError);
        }

        [Fact]
        public void ToCapabilitiesCopiesNegotiationFields()
        {
            var profile = new ClientProfile
            {
                MaxViews = 2,
                EnableCache = false,
                MaxCacheEntries = 0,
                SupportedCodecs = new[] { CodecId.Lz4, CodecId.Raw },
                SupportedDTypes = new[] { DTypeId.Float16 },
                SupportedTensorLayouts = new[] { TensorLayoutId.Nhwc, TensorLayoutId.Nchw },
                SupportedPayloadKinds = PayloadKind.Tensor | PayloadKind.StructuredEvent,
                SupportedObjectKinds = ControlMetadataBitmaps.BuildCacheObjectBitmap(CacheObjectKind.CameraBlock, CacheObjectKind.PayloadLayoutTemplate),
                SupportedDegradePolicies = BudgetPolicy.AllowPartial | BudgetPolicy.AllowDegraded,
                PreferredTileWidth = 64,
                PreferredTileHeight = 32,
                MinSourceWidth = 320,
                MaxSourceWidth = 3840,
                MinSourceHeight = 180,
                MaxSourceHeight = 2160,
                MinTargetFpsTimes100 = 3000,
                MaxTargetFpsTimes100 = 12000,
                LatencyBudgetMilliseconds = 80,
            };

            var capabilities = profile.ToCapabilities();

            Assert.True(capabilities.TryValidate(out _));
            Assert.Equal(profile.MaxViews, capabilities.MaxViews);
            Assert.False(capabilities.EnableCache);
            Assert.Equal(0, capabilities.MaxCacheEntries);
            Assert.Equal(profile.SupportedCodecs, capabilities.SupportedCodecs);
            Assert.Equal(profile.SupportedDTypes, capabilities.SupportedDTypes);
            Assert.Equal(profile.SupportedTensorLayouts, capabilities.SupportedTensorLayouts);
            Assert.Equal((uint)(PayloadKind.Tensor | PayloadKind.StructuredEvent), capabilities.SupportedPayloadKindBitmap);
            Assert.Equal(ControlMetadataBitmaps.BuildCacheObjectBitmap(CacheObjectKind.CameraBlock, CacheObjectKind.PayloadLayoutTemplate), capabilities.SupportedCacheObjectBitmap);
            Assert.Equal(BudgetPolicy.AllowPartial | BudgetPolicy.AllowDegraded, capabilities.SupportedDegradePolicies);
            Assert.Equal(64, capabilities.PreferredTileWidth);
            Assert.Equal(80, capabilities.LatencyBudgetMilliseconds);
        }

        [Fact]
        public void TryValidateRejectsInvalidClientProfileValues()
        {
            var profile = new ClientProfile
            {
                MaxViews = 0,
            };

            Assert.False(profile.TryValidate(out var validationError));
            Assert.Contains(nameof(ClientProfile.MaxViews), validationError);

            profile.MaxViews = 1;
            profile.SupportedCodecs = new[] { CodecId.Raw, CodecId.Raw };
            Assert.False(profile.TryValidate(out validationError));
            Assert.Contains("duplicate", validationError);
        }

        [Fact]
        public void TransportProfileCanSwitchToQuic()
        {
            var profile = new ClientProfile
            {
                TransportProfile = NnrpTransportProfile.Quic,
            };

            Assert.True(profile.TryValidate(out var validationError));
            Assert.Equal(string.Empty, validationError);
            Assert.Equal(NnrpTransportProfile.Quic, profile.TransportProfile);
        }

        [Fact]
        public void CreateAuthBlockReturnsClonedProviderBytes()
        {
            var profile = new ClientProfile();

            Assert.Empty(profile.CreateAuthBlock());

            var secretBytes = new byte[] { 1, 2, 3 };
            profile.AuthBlockProvider = () => secretBytes;

            var authBlock = profile.CreateAuthBlock();

            Assert.Equal(secretBytes, authBlock);
            authBlock[0] = 9;
            Assert.Equal(1, secretBytes[0]);
        }

        [Fact]
        public void CreateClientHelloMapsProfileFieldsIntoHandshakeMetadata()
        {
            var profile = new ClientProfile
            {
                MaxViews = 2,
                EnableCache = false,
                MaxCacheEntries = 0,
                SupportedCodecs = new[] { CodecId.Lz4, CodecId.Raw },
                SupportedDTypes = new[] { DTypeId.Float16 },
                SupportedTensorLayouts = new[] { TensorLayoutId.Nchw },
                SupportedPayloadKinds = PayloadKind.Tensor | PayloadKind.StructuredEvent,
                SupportedObjectKinds = ControlMetadataBitmaps.BuildCacheObjectBitmap(CacheObjectKind.CameraBlock, CacheObjectKind.PayloadLayoutTemplate),
                SupportedDegradePolicies = BudgetPolicy.AllowPartial | BudgetPolicy.AllowDegraded,
                MinSourceWidth = 320,
                MinSourceHeight = 180,
                MaxSourceWidth = 3840,
                MaxSourceHeight = 2160,
                MaxTargetFpsTimes100 = 12000,
                LatencyBudgetMilliseconds = 80,
                AuthBlockProvider = () => new byte[] { 7, 8 },
            };

            var hello = profile.CreateClientHello(requestedSessionId: 11, traceId: 22);

            Assert.Equal(MessageType.ClientHello, hello.Header.MessageType);
            Assert.Equal(11u, hello.Metadata.RequestedSessionId);
            Assert.Equal(ControlMetadataBitmaps.TensorProfileBitmap, hello.Metadata.SupportedProfileBitmap);
            Assert.Equal((uint)(PayloadKind.Tensor | PayloadKind.StructuredEvent), hello.Metadata.SupportedPayloadKindBitmap);
            Assert.Equal(2u, hello.Metadata.MaxLaneCount);
            Assert.Equal(0u, hello.Metadata.CacheNamespaceCount);
            Assert.Equal((uint)(BudgetPolicy.AllowPartial | BudgetPolicy.AllowDegraded), hello.Metadata.DegradePolicy);
            Assert.Equal(0x3u, hello.Metadata.SupportedCodecBitmap);
            Assert.Equal(0x1u << (int)TensorLayoutId.Nchw, hello.Metadata.SupportedLayoutBitmap);
            Assert.Equal(12000u, hello.Metadata.TargetCadenceX100);
            Assert.Equal(new byte[] { 7, 8 }, hello.AuthBlock.ToArray());
        }

        [Fact]
        public void CreateClientHelloIncludesTransportAndLossToleranceExtensions()
        {
            var profile = new ClientProfile
            {
                SessionLossTolerance = LossTolerance.LowLatency,
                SupportedPayloadKinds = PayloadKind.Tensor | PayloadKind.TokenChunk,
                SupportedDegradePolicies = BudgetPolicy.AllowPartial | BudgetPolicy.AllowStaleReuse,
                AuthBlockProvider = () => new byte[] { 1, 2, 3 },
            };

            var hello = profile.CreateClientHello(
                requestedSessionId: 9,
                traceId: 10,
                transportPolicy: TransportPolicy.ForceTcp,
                preferredTransportId: TransportId.Tcp);

            Assert.Equal(NnrpHeader.CurrentWireFormat, hello.Header.WireFormat);
            Assert.True(hello.TryGetClientLossToleranceExtension(out var lossTolerance, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(LossTolerance.LowLatency, lossTolerance.LossTolerance);
            Assert.True(hello.TryGetClientTransportPolicyExtension(out var transportPolicy, out error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(TransportPolicy.ForceTcp, transportPolicy.TransportPolicy);
            Assert.Equal(TransportId.Tcp, transportPolicy.PreferredTransportId);
            Assert.Equal((uint)(PayloadKind.Tensor | PayloadKind.TokenChunk), hello.Metadata.SupportedPayloadKindBitmap);
            Assert.Equal((uint)(BudgetPolicy.AllowPartial | BudgetPolicy.AllowStaleReuse), hello.Metadata.DegradePolicy);
        }
    }
}
