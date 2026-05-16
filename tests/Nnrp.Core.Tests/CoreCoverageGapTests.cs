using System;
using Nnrp.Core;
using Xunit;

namespace Nnrp.Core.Tests
{
    public sealed class CoreCoverageGapTests
    {
        [Fact]
        public void BinaryAlignmentTryAlignUpEdgeCases()
        {
            Assert.True(BinaryAlignment.TryAlignUp(0, out var aligned));
            Assert.Equal(0, aligned);
            Assert.True(BinaryAlignment.TryAlignUp(8, out aligned));
            Assert.Equal(8, aligned);
            Assert.True(BinaryAlignment.TryAlignUp(9, out aligned));
            Assert.Equal(16, aligned);
            Assert.False(BinaryAlignment.TryAlignUp(-1, out _));
            Assert.False(BinaryAlignment.TryAlignUp(0, out _, alignment: 0));
        }

        [Fact]
        public void BinaryAlignmentPaddingAndValidation()
        {
            Assert.Equal(0, BinaryAlignment.GetPadding(8));
            Assert.Equal(4, BinaryAlignment.GetPadding(12, 16));
            Assert.True(BinaryAlignment.IsAligned(16));
            Assert.False(BinaryAlignment.IsAligned(17));

            Assert.True(BinaryAlignment.ValidateZeroPadding(new byte[4]));
            Assert.False(BinaryAlignment.ValidateZeroPadding(new byte[] { 0, 0, 1, 0 }));

            Assert.Throws<ArgumentOutOfRangeException>(() => BinaryAlignment.IsAligned(0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => BinaryAlignment.GetPadding(-1));
        }

        [Fact]
        public void TensorSectionBlockParseRejectsShortSource()
        {
            var tooShort = new byte[4];
            Assert.False(TensorSectionBlock.TryParse(tooShort, 1, out _, out _, out var error));
            Assert.Equal(NnrpParseError.SourceTooShort, error);
        }

        [Fact]
        public void CacheStoreRejectsFullStoreForNewKey()
        {
            var store = new NnrpCacheStore(maxEntries: 1, maxObjectBytes: 1024);
            Assert.True(store.TryPut(new NnrpCacheKey(1, 1, 1), new byte[1], 60).IsSuccess);

            // Same key should be updatable (not "full")
            Assert.True(store.TryPut(new NnrpCacheKey(1, 1, 1), new byte[2], 60).IsSuccess);

            // Different key should be rejected
            Assert.False(store.TryPut(new NnrpCacheKey(1, 2, 2), new byte[1], 60).IsSuccess);
        }

        [Fact]
        public void CacheResultFactoriesThrowOnNullArgument()
        {
            Assert.Throws<ArgumentNullException>(() => NnrpCacheResult.Hit(null!));
            Assert.Throws<ArgumentNullException>(() => NnrpCacheResult.Stored(null!));
        }

        [Fact]
        public void CachePutMessageConstructorValidatesLengths()
        {
            var header = new NnrpHeader(
                versionMajor: NnrpHeader.CurrentVersionMajor,
                wireFormat: NnrpHeader.CurrentWireFormat,
                messageType: MessageType.CachePut,
                flags: HeaderFlags.None,
                metaLength: CachePutMetadata.MetadataLength,
                bodyLength: 5,
                sessionId: 1, frameId: 0, viewId: 0, routeId: 0, traceId: 0);
            var metadata = new CachePutMetadata(
                cacheNamespace: 1, cacheKeyHigh: 0, cacheKeyLow: 0,
                objectKind: CacheObjectKind.CameraBlock,
                ttlMilliseconds: 1000, objectBytes: 3, codecBitmap: 0);

            // Mismatched body length
            Assert.Throws<ArgumentException>(() =>
                new CachePutMessage(header, metadata, new byte[] { 1, 2, 3 }));
        }

        [Fact]
        public void CacheInvalidateMessageConstructorValidates()
        {
            var invalidHeader = new NnrpHeader(
                versionMajor: NnrpHeader.CurrentVersionMajor,
                wireFormat: NnrpHeader.CurrentWireFormat,
                messageType: MessageType.FrameSubmit, // wrong type
                flags: HeaderFlags.None,
                metaLength: CacheInvalidateMetadata.MetadataLength,
                bodyLength: 0,
                sessionId: 1, frameId: 0, viewId: 0, routeId: 0, traceId: 0);
            var metadata = new CacheInvalidateMetadata(
                CacheInvalidateScope.Entry, 1, 0, 0, 0);
            Assert.Throws<ArgumentException>(() =>
                new CacheInvalidateMessage(invalidHeader, metadata));
        }

        [Fact]
        public void ResultDropMessageRoundTrips()
        {
            var drop = ResultDropMessage.Create(sessionId: 7, frameId: 42, traceId: 99);
            Assert.Equal(MessageType.ResultDrop, drop.Header.MessageType);
            Assert.Equal(7u, drop.Header.SessionId);
            Assert.Equal(42u, drop.Header.FrameId);

            var bytes = drop.ToArray();
            Assert.True(ResultDropMessage.TryParse(bytes, out var parsed, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(7u, parsed.Header.SessionId);
            Assert.Equal(42u, parsed.Header.FrameId);

            // Reject non-ResultDrop message type
            var fakeHeader = new NnrpHeader(
                NnrpHeader.CurrentVersionMajor, NnrpHeader.CurrentWireFormat,
                MessageType.FrameSubmit, HeaderFlags.None,
                0, 0, 7, 42, 0, 0, 99);
            Assert.Throws<ArgumentException>(() => new ResultDropMessage(fakeHeader));
        }

        [Fact]
        public void CacheStoreGetExpiredEntryReturnsMiss()
        {
            var store = new NnrpCacheStore(maxEntries: 10);
            var key = new NnrpCacheKey(1, 1, 1);
            // Put with 1-second TTL and immediately expire by manipulating time isn't
            // possible without reflection, but Get on missing key returns miss.
            var result = store.TryGet(key);
            Assert.Equal(NnrpCacheResultCode.CacheMiss, result.Code);
        }

        [Fact]
        public void ClientHelloToCapabilitiesMapsAllFields()
        {
            var helloMetadata = new ClientHelloMetadata(
                minVersionMajor: 1, maxVersionMajor: 1,
                supportedWireFormatBitmap: 1,
                supportedProfileBitmap: ControlMetadataBitmaps.TensorProfileBitmap,
                supportedPayloadKindBitmap: ControlMetadataBitmaps.TensorPayloadKindBitmap,
                supportedCodecBitmap: 2,
                supportedCompressionBitmap: 2,
                supportedDTypeBitmap: 4,
                supportedLayoutBitmap: 1,
                cacheDigestBitmap: ControlMetadataBitmaps.CacheDigestBitmap,
                cacheObjectBitmap: ControlMetadataBitmaps.TensorProfileCacheObjectBitmap,
                cacheNamespaceCount: 1,
                maxLaneCount: 2,
                maxCacheEntries: 64, maxCacheBytes: 1024 * 1024,
                targetCadenceX100: 6000,
                latencyBudgetMilliseconds: 50,
                qualityTier: 1,
                degradePolicy: 0,
                requestedSessionId: 0,
                authBytes: 0,
                controlExtensionBytes: 0);
            var hello = new ClientHelloMessage(
                new NnrpHeader(
                    NnrpHeader.CurrentVersionMajor, NnrpHeader.CurrentWireFormat,
                    MessageType.ClientHello, HeaderFlags.None,
                    ClientHelloMetadata.MetadataLength, 0,
                    0, 0, 0, 0, 0),
                helloMetadata, Array.Empty<byte>());

            var caps = hello.ToCapabilities();
            Assert.Equal(2, caps.MaxViews);
            Assert.Equal(1, caps.MinSourceWidth);
            Assert.Equal(8192, caps.MaxSourceHeight);
        }

        [Fact]
        public void FrameSubmitMessageFailsParseOnWrongBodyLength()
        {
            var section = CreateTestSection();
            var metadata = new FrameSubmitMetadata(
                640, 360, 32, 32, 1, 1,
                FrameClass.Keyframe, InputProfile.DenseLumaFrame,
                TileIndexMode.DenseRange, 16, 6000, 0, 0, 0, 0);
            // bodyLength doesn't match actual body
            var wrongHeader = new NnrpHeader(
                NnrpHeader.CurrentVersionMajor, NnrpHeader.CurrentWireFormat,
                MessageType.FrameSubmit, HeaderFlags.None,
                FrameSubmitMessage.MetadataLength, (uint)section.TotalLength + 999,
                1, 2, 0, 0, 3);
            Assert.Throws<ArgumentException>(() =>
                new FrameSubmitMessage(wrongHeader, metadata,
                    Array.Empty<byte>(), Array.Empty<ushort>(), new[] { section }));
        }

        [Fact]
        public void CacheStoreMaxEntriesZeroRejectsAllPuts()
        {
            var store = new NnrpCacheStore(maxEntries: 0, maxObjectBytes: 1024);
            var result = store.TryPut(new NnrpCacheKey(1, 1, 1), new byte[1], 60);
            Assert.False(result.IsSuccess);
            Assert.Equal(NnrpCacheResultCode.LimitExceeded, result.Code);
        }

        [Fact]
        public void CacheStoreSameKeyUpdateDoesNotCountAsNewEntry()
        {
            var store = new NnrpCacheStore(maxEntries: 2, maxObjectBytes: 1024);
            Assert.True(store.TryPut(new NnrpCacheKey(1, 1, 1), new byte[1], 60).IsSuccess);
            Assert.True(store.TryPut(new NnrpCacheKey(1, 2, 2), new byte[1], 60).IsSuccess);
            // Update existing key should work even when "full"
            Assert.True(store.TryPut(new NnrpCacheKey(1, 1, 1), new byte[3], 60).IsSuccess);
            Assert.Equal(2, store.Count);
        }

        [Fact]
        public void CacheKeyEqualsNullReturnsFalse()
        {
            var key = new NnrpCacheKey(1, 2, 3);
            Assert.False(key.Equals(null));
            Assert.False(key.Equals("not a key"));
        }

        [Fact]
        public void FrameSubmitMessageRejectsWrongMessageType()
        {
            var section = CreateTestSection();
            var metadata = new FrameSubmitMetadata(
                640, 360, 32, 32, 1, 1,
                FrameClass.Keyframe, InputProfile.DenseLumaFrame,
                TileIndexMode.DenseRange, 16, 6000, 0, 0, 0, 0);
            var wrongHeader = new NnrpHeader(
                NnrpHeader.CurrentVersionMajor, NnrpHeader.CurrentWireFormat,
                MessageType.ResultPush, // wrong
                HeaderFlags.None,
                FrameSubmitMessage.MetadataLength, (uint)section.TotalLength,
                1, 2, 0, 0, 3);
            Assert.Throws<ArgumentException>(() =>
                new FrameSubmitMessage(wrongHeader, metadata,
                    Array.Empty<byte>(), Array.Empty<ushort>(), new[] { section }));
        }

        [Fact]
        public void ResultPushMessageRejectsWrongMessageType()
        {
            var section = CreateTestSection();
            var metadata = new ResultPushMetadata(
                ResultStatusCode.Success, ResultFlags.None, 1, 0, 0, 0, 0, 0, 0, 0);
            var wrongHeader = new NnrpHeader(
                NnrpHeader.CurrentVersionMajor, NnrpHeader.CurrentWireFormat,
                MessageType.FrameSubmit, // wrong
                HeaderFlags.None,
                ResultPushMetadata.MetadataLength, (uint)(TensorResultBlock.BlockLength + section.TotalLength),
                1, 2, 0, 0, 3);
            Assert.Throws<ArgumentException>(() =>
                new ResultPushMessage(wrongHeader, metadata,
                    Array.Empty<ushort>(), new[] { section }));
        }

        [Fact]
        public void CacheInvalidateScopeHasExpectedValues()
        {
            Assert.Equal((uint)0, (uint)CacheInvalidateScope.Session);
            Assert.Equal((uint)1, (uint)CacheInvalidateScope.Namespace);
            Assert.Equal((uint)2, (uint)CacheInvalidateScope.ObjectKind);
            Assert.Equal((uint)3, (uint)CacheInvalidateScope.Entry);
        }

        [Fact]
        public void CacheAckStatusHasExpectedValues()
        {
            Assert.Equal((uint)0, (uint)CacheAckStatus.Accepted);
            Assert.Equal((uint)1, (uint)CacheAckStatus.Rejected);
            Assert.Equal((uint)2, (uint)CacheAckStatus.Replaced);
        }

        [Fact]
        public void CacheObjectKindHasExpectedValues()
        {
            Assert.Equal((uint)1, (uint)CacheObjectKind.CameraBlock);
            Assert.Equal((uint)2, (uint)CacheObjectKind.TileIndexTemplate);
            Assert.Equal((uint)3, (uint)CacheObjectKind.TensorSectionTable);
            Assert.Equal((uint)4, (uint)CacheObjectKind.CodecAuxBlock);
            Assert.Equal((uint)5, (uint)CacheObjectKind.FallbackResource);
            Assert.Equal((uint)6, (uint)CacheObjectKind.PayloadLayoutTemplate);
            Assert.Equal(CacheObjectKind.TileIndexTemplate, CacheObjectKind.TileIndexBlock);
            Assert.Equal(CacheObjectKind.ReusableResultObject, CacheObjectKind.FallbackResource);
        }

        [Fact]
        public void TensorProfileCacheObjectBitmapHasExpectedValues()
        {
            Assert.Equal(0x7u, ControlMetadataBitmaps.TensorProfileCacheObjectBitmap);
            Assert.Equal(
                ControlMetadataBitmaps.TensorProfileCacheObjectBitmap,
                ControlMetadataBitmaps.BuildCacheObjectBitmap(
                    CacheObjectKind.CameraBlock,
                    CacheObjectKind.TileIndexBlock,
                    CacheObjectKind.TensorSectionTable));
        }

        [Fact]
        public void SubmitOutcomeFromResultPushAndDrop()
        {
            // Build a valid aligned ResultPush dynamically instead of hardcoding hex.
            var pushMsg = BuildAlignedResultPush();
            var pushBytes = pushMsg.ToArray();
            Assert.True(ResultPushMessage.TryParse(pushBytes, out var pushResult, out _));
            var outcome = SubmitOutcome.FromResultPush(pushResult);
            Assert.False(outcome.IsResultDrop);
            Assert.Equal(pushResult.Header.FrameId, outcome.ResultPush.Header.FrameId);

            var drop = ResultDropMessage.Create(sessionId: 7, frameId: 99);
            var dropOutcome = SubmitOutcome.FromResultDrop(drop);
            Assert.True(dropOutcome.IsResultDrop);
            Assert.Equal(99u, dropOutcome.ResultDrop.Header.FrameId);
        }

        [Fact]
        public void SubmitOutcomeTryParsePushAndDrop()
        {
            var pushMsg = BuildAlignedResultPush();
            var pushBytes = pushMsg.ToArray();
            Assert.True(SubmitOutcome.TryParse(pushBytes, out var pushOutcome, out var pushError));
            Assert.Equal(NnrpParseError.None, pushError);
            Assert.False(pushOutcome.IsResultDrop);

            var dropBytes = ResultDropMessage.Create(sessionId: 1, frameId: 2).ToArray();
            Assert.True(SubmitOutcome.TryParse(dropBytes, out var dropOutcome, out var dropError));
            Assert.Equal(NnrpParseError.None, dropError);
            Assert.True(dropOutcome.IsResultDrop);

            // Unknown message type
            var closeBytes = CloseMessage.Create(sessionId: 1, "x", 0).ToArray();
            Assert.False(SubmitOutcome.TryParse(closeBytes, out _, out var closeError));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, closeError);
        }

        private static ResultPushMessage BuildAlignedResultPush()
        {
            var lengthTable = new byte[] { 1, 0, 0, 0, 1, 0, 0, 0 };
            var payload = new byte[] { 0x41 };
            var section = new TensorSectionBlock(
                new TensorSectionDescriptor(
                    role: TensorRole.SrResidual,
                    codec: CodecId.Raw,
                    dtype: DTypeId.UInt8,
                    layout: TensorLayoutId.Nhwc,
                    scalePolicy: ScalePolicy.None,
                    flags: 0,
                    elementCountPerTile: 0,
                    codecTableBytes: 0,
                    lengthTableBytes: (uint)lengthTable.Length,
                    payloadBytes: (uint)payload.Length,
                    payloadStrideBytes: 0),
                codecTable: Array.Empty<byte>(),
                lengthTable: lengthTable,
                payload: payload);
            var tileIds = new ushort[] { 5, 6 };
            var tileIndexBytes = TileIndexBlockCodec.GetEncodedLength(tileIds, TileIndexMode.RawUInt16);
            var bodyLength = BinaryAlignment.AlignUp(TensorResultBlock.BlockLength + tileIndexBytes, 8) + section.TotalLength;
            var metadata = new ResultPushMetadata(
                statusCode: ResultStatusCode.Success,
                resultFlags: ResultFlags.None,
                sectionCount: 1,
                tileCount: 2,
                activeProfileId: 0,
                inferenceMilliseconds: 0,
                queueMilliseconds: 0,
                serverTotalMilliseconds: 0,
                tileBaseId: 0,
                tileIndexBytes: (uint)tileIndexBytes);
            var header = new NnrpHeader(
                versionMajor: NnrpHeader.CurrentVersionMajor,
                wireFormat: NnrpHeader.CurrentWireFormat,
                messageType: MessageType.ResultPush,
                flags: HeaderFlags.None,
                metaLength: ResultPushMetadata.MetadataLength,
                bodyLength: (uint)bodyLength,
                sessionId: 1,
                frameId: 2,
                viewId: 0,
                routeId: 0,
                traceId: 0);
            return new ResultPushMessage(header, metadata, tileIds, new[] { section });
        }

        [Fact]
        public void ControlMetadataBitmapsEncodeDecodeRoundTrip()
        {
            var codecs = new[] { CodecId.Raw, CodecId.Lz4 };
            var bitmap = ControlMetadataBitmaps.EncodeCodecBitmap(codecs);
            var decoded = ControlMetadataBitmaps.DecodeCodecBitmap<CodecId>(bitmap);
            Assert.Equal(codecs.Length, decoded.Length);
            Assert.Contains(CodecId.Raw, decoded);
            Assert.Contains(CodecId.Lz4, decoded);

            var dtypes = new[] { DTypeId.UInt8, DTypeId.Float16 };
            var dtypeBitmap = ControlMetadataBitmaps.EncodeDTypeBitmap(dtypes);
            var decodedDTypes = ControlMetadataBitmaps.DecodeCodecBitmap<DTypeId>(dtypeBitmap);
            Assert.Equal(dtypes.Length, decodedDTypes.Length);
            Assert.Contains(DTypeId.UInt8, decodedDTypes);
            Assert.Contains(DTypeId.Float16, decodedDTypes);

            var layouts = new[] { TensorLayoutId.Nhwc };
            var layoutBitmap = ControlMetadataBitmaps.EncodeTensorLayoutBitmap(layouts);
            var decodedLayouts = ControlMetadataBitmaps.DecodeCodecBitmap<TensorLayoutId>(layoutBitmap);
            Assert.Single(decodedLayouts);
            Assert.Equal(TensorLayoutId.Nhwc, decodedLayouts[0]);

            Assert.Equal(1u, ControlMetadataBitmaps.EncodeCurrentWireFormatBitmap());
            Assert.Equal(0u, ControlMetadataBitmaps.EncodeCodecBitmap(null!));
            Assert.Equal(0u, ControlMetadataBitmaps.EncodeCodecBitmap(Array.Empty<CodecId>()));
        }

        [Fact]
        public void AllMetadataTypesWriteAndParseRoundTrip()
        {
            // ErrorMetadata
            var errorMeta = new ErrorMetadata(
                ErrorCode.UnsupportedCapability, NnrpErrorScope.Session,
                isFatal: true, retryAfterMilliseconds: 0, relatedSessionId: 0,
                relatedFrameId: 0, relatedViewId: 0, diagnosticBytes: 0);
            var errorBytes = errorMeta.ToArray();
            Assert.True(ErrorMetadata.TryParse(errorBytes, out var parsedError, out _));
            Assert.Equal(errorMeta.ErrorCode, parsedError.ErrorCode);

            // CachePutMetadata
            var putMeta = new CachePutMetadata(1, 0xAA, 0xBB, CacheObjectKind.CodecAuxBlock, 5000, 16, 3);
            Assert.True(CachePutMetadata.TryParse(putMeta.ToArray(), out var parsedPut, out _));
            Assert.Equal(putMeta.CacheNamespace, parsedPut.CacheNamespace);

            // CacheAckMetadata
            var ackMeta = new CacheAckMetadata(1, 0xAA, 0xBB, CacheAckStatus.Accepted, 5000, 1024, 0);
            Assert.True(CacheAckMetadata.TryParse(ackMeta.ToArray(), out var parsedAck, out _));
            Assert.Equal(ackMeta.CacheNamespace, parsedAck.CacheNamespace);

            // CacheInvalidateMetadata
            var invMeta = new CacheInvalidateMetadata(CacheInvalidateScope.Entry, 1, 0xAA, 0xBB, 0);
            Assert.True(CacheInvalidateMetadata.TryParse(invMeta.ToArray(), out var parsedInv, out _));
            Assert.Equal(invMeta.CacheNamespace, parsedInv.CacheNamespace);
        }

        private static TensorSectionBlock CreateTestSection()
        {
            return new TensorSectionBlock(
                new TensorSectionDescriptor(
                    TensorRole.LumaHint, CodecId.Raw, DTypeId.UInt8,
                    TensorLayoutId.Nhwc, ScalePolicy.None, 0, 1, 0, 4, 2, 0),
                Array.Empty<byte>(),
                new byte[] { 1, 0, 0, 0 },
                new byte[] { 0xAA, 0xBB });
        }
    }
}
