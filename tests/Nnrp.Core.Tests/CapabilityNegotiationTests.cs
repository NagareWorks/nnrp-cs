using Nnrp.Core;
using Xunit;

namespace Nnrp.Core.Tests
{
    public sealed class CapabilityNegotiationTests
    {
        [Fact]
        public void NegotiationAcceptsCommonCapabilitiesUsingServerPreference()
        {
            var clientCapabilities = CreateClientCapabilities(
                supportedCodecs: new[] { CodecId.Raw, CodecId.Lz4 },
                supportedDTypes: new[] { DTypeId.UInt8, DTypeId.Float16 },
                supportedTensorLayouts: new[] { TensorLayoutId.Nchw, TensorLayoutId.Nhwc },
                maxViews: 2,
                maxCacheEntries: 512);
            var serverCapabilities = CreateServerCapabilities(
                acceptedCodecs: new[] { CodecId.Lz4, CodecId.Raw },
                acceptedDTypes: new[] { DTypeId.Float16, DTypeId.UInt8 },
                acceptedTensorLayouts: new[] { TensorLayoutId.Nhwc, TensorLayoutId.Nchw },
                maxViews: 4,
                maxCacheEntries: 128);

            var result = NnrpCapabilityNegotiator.Negotiate(clientCapabilities, serverCapabilities);

            Assert.True(result.IsAccepted);
            Assert.Equal(CapabilityRejectionReason.None, result.RejectionReason);
            Assert.Equal(CodecId.Lz4, result.Selection.Codec);
            Assert.Equal(DTypeId.Float16, result.Selection.DType);
            Assert.Equal(TensorLayoutId.Nhwc, result.Selection.TensorLayout);
            Assert.Equal((uint)(PayloadKind.Tensor | PayloadKind.TokenChunk), result.Selection.PayloadKindBitmap);
            Assert.Equal(ControlMetadataBitmaps.LowFrequencyObjectBitmap, result.Selection.CacheObjectBitmap);
            Assert.Equal(BudgetPolicy.AllowPartial | BudgetPolicy.AllowDegraded, result.Selection.DegradePolicies);
            Assert.Equal(2, result.Selection.MaxViews);
            Assert.True(result.Selection.EnableCache);
            Assert.Equal(128, result.Selection.MaxCacheEntries);
            Assert.Equal(3, result.Selection.MaxConcurrentFrames);
            Assert.Equal(1024 * 1024, result.Selection.MaxBodyBytes);
            Assert.Equal(8, result.Selection.MaxSectionCount);
            Assert.Equal(4096, result.Selection.MaxTileCount);
            Assert.Equal(60, result.Selection.TokenTtlSeconds);
            Assert.True(result.Selection.AllowSessionRenewal);
        }

        [Fact]
        public void NegotiationDisablesCacheWhenEitherSideDisablesCache()
        {
            var clientCapabilities = CreateClientCapabilities(enableCache: false, maxCacheEntries: 0);
            var serverCapabilities = CreateServerCapabilities();

            var result = NnrpCapabilityNegotiator.Negotiate(clientCapabilities, serverCapabilities);

            Assert.True(result.IsAccepted);
            Assert.False(result.Selection.EnableCache);
            Assert.Equal(0, result.Selection.MaxCacheEntries);
        }

        [Theory]
        [InlineData("codec")]
        [InlineData("dtype")]
        [InlineData("layout")]
        [InlineData("payload")]
        public void NegotiationRejectsUnsupportedCapabilities(string mismatchKind)
        {
            var clientCapabilities = CreateClientCapabilities(
                supportedCodecs: mismatchKind == "codec" ? new[] { CodecId.Raw } : new[] { CodecId.Lz4 },
                supportedDTypes: mismatchKind == "dtype" ? new[] { DTypeId.UInt8 } : new[] { DTypeId.Float16 },
                supportedTensorLayouts: mismatchKind == "layout" ? new[] { TensorLayoutId.Nchw } : new[] { TensorLayoutId.Nhwc },
                supportedPayloadKindBitmap: mismatchKind == "payload" ? (uint)PayloadKind.AudioChunk : (uint)(PayloadKind.Tensor | PayloadKind.TokenChunk));
            var serverCapabilities = CreateServerCapabilities(
                acceptedCodecs: new[] { CodecId.Lz4 },
                acceptedDTypes: new[] { DTypeId.Float16 },
                acceptedTensorLayouts: new[] { TensorLayoutId.Nhwc },
                acceptedPayloadKindBitmap: (uint)PayloadKind.Tensor);

            var result = NnrpCapabilityNegotiator.Negotiate(clientCapabilities, serverCapabilities);

            Assert.False(result.IsAccepted);
            Assert.Equal(ErrorCode.UnsupportedCapability, result.ErrorCode);
            Assert.False(string.IsNullOrEmpty(result.RejectionMessage));

            if (mismatchKind == "codec")
            {
                Assert.Equal(CapabilityRejectionReason.NoCommonCodec, result.RejectionReason);
            }
            else if (mismatchKind == "dtype")
            {
                Assert.Equal(CapabilityRejectionReason.NoCommonDType, result.RejectionReason);
            }
            else if (mismatchKind == "payload")
            {
                Assert.Equal(CapabilityRejectionReason.NoCommonPayloadKind, result.RejectionReason);
            }
            else
            {
                Assert.Equal(CapabilityRejectionReason.NoCommonTensorLayout, result.RejectionReason);
            }
        }

        [Fact]
        public void NegotiationRejectsOversizedViewRequestAsLimitExceeded()
        {
            var clientCapabilities = CreateClientCapabilities(maxViews: 3);
            var serverCapabilities = CreateServerCapabilities(maxViews: 2);

            var result = NnrpCapabilityNegotiator.Negotiate(clientCapabilities, serverCapabilities);

            Assert.False(result.IsAccepted);
            Assert.Equal(ErrorCode.LimitExceeded, result.ErrorCode);
            Assert.Equal(CapabilityRejectionReason.MaxViewsExceeded, result.RejectionReason);
        }

        [Fact]
        public void NegotiationRejectsInvalidAndMissingCapabilitySets()
        {
            var invalidClient = CreateClientCapabilities(supportedCodecs: new CodecId[0]);
            var invalidServer = CreateServerCapabilities(acceptedDTypes: new[] { DTypeId.Float16, DTypeId.Float16 });

            var missingClient = NnrpCapabilityNegotiator.Negotiate(null!, CreateServerCapabilities());
            var missingServer = NnrpCapabilityNegotiator.Negotiate(CreateClientCapabilities(), null!);
            var invalidClientResult = NnrpCapabilityNegotiator.Negotiate(invalidClient, CreateServerCapabilities());
            var invalidServerResult = NnrpCapabilityNegotiator.Negotiate(CreateClientCapabilities(), invalidServer);

            Assert.Equal(CapabilityRejectionReason.InvalidClientCapabilities, missingClient.RejectionReason);
            Assert.Equal(CapabilityRejectionReason.InvalidServerCapabilities, missingServer.RejectionReason);
            Assert.Equal(CapabilityRejectionReason.InvalidClientCapabilities, invalidClientResult.RejectionReason);
            Assert.Equal(CapabilityRejectionReason.InvalidServerCapabilities, invalidServerResult.RejectionReason);
            Assert.Equal(ErrorCode.UnsupportedCapability, invalidClientResult.ErrorCode);
            Assert.Equal(ErrorCode.UnsupportedCapability, invalidServerResult.ErrorCode);
        }

        [Fact]
        public void ClientCapabilityValidationReportsRangeErrors()
        {
            AssertInvalidClient(CreateClientCapabilities(maxViews: 0), nameof(NnrpClientCapabilities.MaxViews));
            AssertInvalidClient(CreateClientCapabilities(enableCache: true, maxCacheEntries: 0), nameof(NnrpClientCapabilities.MaxCacheEntries));
            AssertInvalidClient(CreateClientCapabilities(preferredTileWidth: 0), "tile");
            AssertInvalidClient(CreateClientCapabilities(minSourceWidth: 128, maxSourceWidth: 64), "width");
            AssertInvalidClient(CreateClientCapabilities(minSourceHeight: 128, maxSourceHeight: 64), "height");
            AssertInvalidClient(CreateClientCapabilities(minTargetFpsTimes100: 12000, maxTargetFpsTimes100: 6000), "FPS");
            AssertInvalidClient(CreateClientCapabilities(latencyBudgetMilliseconds: 0), nameof(NnrpClientCapabilities.LatencyBudgetMilliseconds));
            AssertInvalidClient(CreateClientCapabilities(supportedPayloadKindBitmap: 0), nameof(NnrpClientCapabilities.SupportedPayloadKindBitmap));
            AssertInvalidClient(CreateClientCapabilities(supportedDTypes: new[] { (DTypeId)255 }), nameof(NnrpClientCapabilities.SupportedDTypes));
        }

        [Fact]
        public void ServerCapabilityValidationReportsLimitErrors()
        {
            AssertInvalidServer(CreateServerCapabilities(maxConcurrentFrames: 0), nameof(NnrpServerCapabilities.MaxConcurrentFrames));
            AssertInvalidServer(CreateServerCapabilities(enableCache: true, maxCacheEntries: 0), nameof(NnrpServerCapabilities.MaxCacheEntries));
            AssertInvalidServer(CreateServerCapabilities(maxBodyBytes: 0), nameof(NnrpServerCapabilities.MaxBodyBytes));
            AssertInvalidServer(CreateServerCapabilities(maxSectionCount: 0), nameof(NnrpServerCapabilities.MaxSectionCount));
            AssertInvalidServer(CreateServerCapabilities(maxTileCount: 0), nameof(NnrpServerCapabilities.MaxTileCount));
            AssertInvalidServer(CreateServerCapabilities(maxViews: 0), nameof(NnrpServerCapabilities.MaxViews));
            AssertInvalidServer(CreateServerCapabilities(tokenTtlSeconds: 0), nameof(NnrpServerCapabilities.TokenTtlSeconds));
            AssertInvalidServer(CreateServerCapabilities(acceptedPayloadKindBitmap: 0), nameof(NnrpServerCapabilities.AcceptedPayloadKindBitmap));
            AssertInvalidServer(CreateServerCapabilities(acceptedCodecs: new[] { (CodecId)255 }), nameof(NnrpServerCapabilities.AcceptedCodecs));
        }

        private static void AssertInvalidClient(NnrpClientCapabilities capabilities, string expectedMessagePart)
        {
            Assert.False(capabilities.TryValidate(out var validationError));
            Assert.Contains(expectedMessagePart, validationError);
        }

        private static void AssertInvalidServer(NnrpServerCapabilities capabilities, string expectedMessagePart)
        {
            Assert.False(capabilities.TryValidate(out var validationError));
            Assert.Contains(expectedMessagePart, validationError);
        }

        private static NnrpClientCapabilities CreateClientCapabilities(
            CodecId[]? supportedCodecs = null,
            DTypeId[]? supportedDTypes = null,
            TensorLayoutId[]? supportedTensorLayouts = null,
            uint supportedPayloadKindBitmap = (uint)(PayloadKind.Tensor | PayloadKind.TokenChunk),
            uint supportedCacheObjectBitmap = ControlMetadataBitmaps.LowFrequencyObjectBitmap,
            BudgetPolicy supportedDegradePolicies = BudgetPolicy.AllowPartial | BudgetPolicy.AllowDegraded,
            int maxViews = 1,
            bool enableCache = true,
            int maxCacheEntries = 256,
            ushort preferredTileWidth = 32,
            ushort preferredTileHeight = 32,
            ushort minSourceWidth = 1,
            ushort maxSourceWidth = 3840,
            ushort minSourceHeight = 1,
            ushort maxSourceHeight = 2160,
            ushort minTargetFpsTimes100 = 3000,
            ushort maxTargetFpsTimes100 = 12000,
            ushort latencyBudgetMilliseconds = 100)
        {
            return new NnrpClientCapabilities(
                supportedCodecs ?? new[] { CodecId.Lz4 },
                supportedDTypes ?? new[] { DTypeId.Float16 },
                supportedTensorLayouts ?? new[] { TensorLayoutId.Nhwc },
                supportedPayloadKindBitmap,
                supportedCacheObjectBitmap,
                supportedDegradePolicies,
                maxViews,
                enableCache,
                maxCacheEntries,
                preferredTileWidth,
                preferredTileHeight,
                minSourceWidth,
                maxSourceWidth,
                minSourceHeight,
                maxSourceHeight,
                minTargetFpsTimes100,
                maxTargetFpsTimes100,
                latencyBudgetMilliseconds);
        }

        private static NnrpServerCapabilities CreateServerCapabilities(
            CodecId[]? acceptedCodecs = null,
            DTypeId[]? acceptedDTypes = null,
            TensorLayoutId[]? acceptedTensorLayouts = null,
            uint acceptedPayloadKindBitmap = (uint)(PayloadKind.Tensor | PayloadKind.TokenChunk),
            uint acceptedCacheObjectBitmap = ControlMetadataBitmaps.LowFrequencyObjectBitmap,
            BudgetPolicy acceptedDegradePolicies = BudgetPolicy.AllowPartial | BudgetPolicy.AllowDegraded,
            int maxConcurrentFrames = 3,
            bool enableCache = true,
            int maxCacheEntries = 256,
            int maxBodyBytes = 1024 * 1024,
            int maxSectionCount = 8,
            int maxTileCount = 4096,
            int maxViews = 1,
            int tokenTtlSeconds = 60,
            bool allowSessionRenewal = true)
        {
            return new NnrpServerCapabilities(
                acceptedCodecs ?? new[] { CodecId.Lz4 },
                acceptedDTypes ?? new[] { DTypeId.Float16 },
                acceptedTensorLayouts ?? new[] { TensorLayoutId.Nhwc },
                acceptedPayloadKindBitmap,
                acceptedCacheObjectBitmap,
                acceptedDegradePolicies,
                maxConcurrentFrames,
                enableCache,
                maxCacheEntries,
                maxBodyBytes,
                maxSectionCount,
                maxTileCount,
                maxViews,
                tokenTtlSeconds,
                allowSessionRenewal);
        }
    }
}
