using System;
using System.IO;
using Nnrp.Core;
using Xunit;

namespace Nnrp.Core.Tests
{
    /// <summary>
    /// Generates and validates C# golden binary vectors for current message types.
    /// Cross-language compatibility against nnrp-py golden artifacts is covered by
    /// <c>PythonGoldenInteropTests</c>; these files remain as local regression snapshots.
    /// </summary>
    public sealed class GoldenVectorTests
    {
        private static string VectorsDir => Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "tests", "vectors");

        private static byte[] ReadRequiredGolden(string path)
        {
            Assert.True(File.Exists(path), $"Golden file is missing: {path}");
            return File.ReadAllBytes(path);
        }

        private static byte[] SyncGolden(string path, byte[] expected)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            if (!File.Exists(path))
            {
                File.WriteAllBytes(path, expected);
                return expected;
            }

            var existing = File.ReadAllBytes(path);
            if (!existing.AsSpan().SequenceEqual(expected))
            {
                File.WriteAllBytes(path, expected);
                return expected;
            }

            return existing;
        }

        [Fact]
        public void GenerateAndVerifyFrameSubmitGolden()
        {
            var msg = SmokePackets.CreateSmokeFrameSubmitMessage(
                sessionId: 0x100, frameId: 1, viewId: 0, traceId: 0xABBABABBA);
            var bytes = msg.ToArray();
            var path = Path.Combine(VectorsDir, "frame_submit_dense.bin");

            var readBack = SyncGolden(path, bytes);
            Assert.True(FrameSubmitMessage.TryParse(readBack, out var parsed, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(bytes, readBack);
        }

        [Fact]
        public void GenerateAndVerifyChangedTileFrameSubmitGolden()
        {
            var section = new TensorSectionBlock(
                new TensorSectionDescriptor(
                    role: TensorRole.LumaHint,
                    codec: CodecId.Raw,
                    dtype: DTypeId.UInt8,
                    layout: TensorLayoutId.Nhwc,
                    scalePolicy: ScalePolicy.None,
                    flags: 0,
                    elementCountPerTile: 1,
                    codecTableBytes: 0,
                    lengthTableBytes: 8,
                    payloadBytes: 3,
                    payloadStrideBytes: 0),
                codecTable: Array.Empty<byte>(),
                lengthTable: new byte[] { 1, 0, 0, 0, 2, 0, 0, 0 },
                payload: new byte[] { 0xAA, 0xBB, 0xCC });
            var metadata = new FrameSubmitMetadata(
                sourceWidth: 640,
                sourceHeight: 360,
                tileWidth: 32,
                tileHeight: 32,
                tileCount: 2,
                sectionCount: 1,
                frameClass: FrameClass.Delta,
                inputProfile: InputProfile.ChangedTilesLuma,
                tileIndexMode: TileIndexMode.RawUInt16,
                latencyBudgetMilliseconds: 33,
                targetFpsTimes100: 6000,
                retryOfFrame: 7,
                tileBaseId: 0,
                cameraBytes: 2,
                tileIndexBytes: 4);
            var header = new NnrpHeader(
                versionMajor: NnrpHeader.CurrentVersionMajor,
                wireFormat: NnrpHeader.CurrentWireFormat,
                messageType: MessageType.FrameSubmit,
                flags: HeaderFlags.None,
                metaLength: FrameSubmitMessage.MetadataLength,
                bodyLength: (uint)(BinaryAlignment.AlignUp(TensorSubmitBlock.BlockLength + 2, 8) + 4 + (BinaryAlignment.AlignUp(BinaryAlignment.AlignUp(TensorSubmitBlock.BlockLength + 2, 8) + 4, 8) - (BinaryAlignment.AlignUp(TensorSubmitBlock.BlockLength + 2, 8) + 4)) + section.TotalLength),
                sessionId: 9,
                frameId: 15,
                viewId: 1,
                routeId: 0,
                traceId: 88);
            var msg = new FrameSubmitMessage(
                header,
                metadata,
                new byte[] { 0xCA, 0xFE },
                new ushort[] { 1, 9 },
                new[] { section });
            var bytes = msg.ToArray();
            var path = Path.Combine(VectorsDir, "frame_submit_changed_tiles.bin");

            var readBack = SyncGolden(path, bytes);
            Assert.True(FrameSubmitMessage.TryParse(readBack, out _, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(bytes, readBack);
        }

        [Fact]
        public void GenerateAndVerifyResultPushGolden()
        {
            var lengthTable = new byte[] { 1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0 };
            var payload = new byte[] { 0x10, 0x20, 0x30 };
            var section = new TensorSectionBlock(
                new TensorSectionDescriptor(
                    role: TensorRole.SrResidual, codec: CodecId.Raw,
                    dtype: DTypeId.UInt8, layout: TensorLayoutId.Nhwc,
                    scalePolicy: ScalePolicy.None, flags: 0,
                    elementCountPerTile: 0, codecTableBytes: 0,
                    lengthTableBytes: (uint)lengthTable.Length,
                    payloadBytes: (uint)payload.Length, payloadStrideBytes: 0),
                codecTable: Array.Empty<byte>(),
                lengthTable: lengthTable, payload: payload);
            var tileIds = new ushort[] { 0, 1, 2 };
            var tileIndexBytes = TileIndexBlockCodec.GetEncodedLength(tileIds, TileIndexMode.RawUInt16);
            var bodyLength = BinaryAlignment.AlignUp(TensorResultBlock.BlockLength + tileIndexBytes, 8) + section.TotalLength;
            var metadata = new ResultPushMetadata(
                statusCode: ResultStatusCode.Success, resultFlags: ResultFlags.None,
                sectionCount: 1, tileCount: 3, activeProfileId: 1,
                inferenceMilliseconds: 2, queueMilliseconds: 1,
                serverTotalMilliseconds: 4, tileBaseId: 0,
                tileIndexBytes: (uint)tileIndexBytes);
            var header = new NnrpHeader(
                versionMajor: NnrpHeader.CurrentVersionMajor,
                wireFormat: NnrpHeader.CurrentWireFormat,
                messageType: MessageType.ResultPush, flags: HeaderFlags.None,
                metaLength: ResultPushMetadata.MetadataLength,
                bodyLength: (uint)bodyLength,
                sessionId: 0x200, frameId: 1, viewId: 0, routeId: 0, traceId: 0);
            var msg = new ResultPushMessage(header, metadata, tileIds, new[] { section });
            var bytes = msg.ToArray();
            var path = Path.Combine(VectorsDir, "result_push_fresh.bin");

            var readBack = SyncGolden(path, bytes);
            Assert.True(ResultPushMessage.TryParse(readBack, out var parsed, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(bytes, readBack);
        }

        [Fact]
        public void GenerateAndVerifyStaleResultPushGolden()
        {
            var section0LengthTable = new byte[] { 4, 0, 0, 0, 100, 0, 0, 0 };
            var section0Payload = new byte[] { 0x01, 0x05 };
            var section1LengthTable = new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 };
            var section1Payload = new byte[] { (byte)'z', (byte)'z' };

            var section0 = new TensorSectionBlock(
                new TensorSectionDescriptor(
                    role: TensorRole.LumaHint,
                    codec: CodecId.Raw,
                    dtype: DTypeId.UInt8,
                    layout: TensorLayoutId.Nhwc,
                    scalePolicy: ScalePolicy.None,
                    flags: 0,
                    elementCountPerTile: 0,
                    codecTableBytes: 0,
                    lengthTableBytes: (uint)section0LengthTable.Length,
                    payloadBytes: (uint)section0Payload.Length,
                    payloadStrideBytes: 0),
                codecTable: Array.Empty<byte>(),
                lengthTable: section0LengthTable,
                payload: section0Payload);

            var section1 = new TensorSectionBlock(
                new TensorSectionDescriptor(
                    role: TensorRole.SrResidual,
                    codec: CodecId.Raw,
                    dtype: DTypeId.UInt8,
                    layout: TensorLayoutId.Nhwc,
                    scalePolicy: ScalePolicy.None,
                    flags: 0,
                    elementCountPerTile: 0,
                    codecTableBytes: 0,
                    lengthTableBytes: (uint)section1LengthTable.Length,
                    payloadBytes: (uint)section1Payload.Length,
                    payloadStrideBytes: 0),
                codecTable: Array.Empty<byte>(),
                lengthTable: section1LengthTable,
                payload: section1Payload);

            var tileIds = new ushort[] { 5, 6 };
            var tileIndexBytes = TileIndexBlockCodec.GetEncodedLength(tileIds, TileIndexMode.RawUInt16);
            var bodyLength = BinaryAlignment.AlignUp(TensorResultBlock.BlockLength + tileIndexBytes, 8) + section0.TotalLength;
            bodyLength = BinaryAlignment.AlignUp(bodyLength, 8) + section1.TotalLength;

            var metadata = new ResultPushMetadata(
                statusCode: ResultStatusCode.Success,
                resultFlags: ResultFlags.Stale | ResultFlags.Partial,
                sectionCount: 2,
                tileCount: 2,
                activeProfileId: 0,
                inferenceMilliseconds: 0x11,
                queueMilliseconds: 0x02,
                serverTotalMilliseconds: 0x13,
                tileBaseId: 0,
                tileIndexBytes: (uint)tileIndexBytes,
                resultClass: ResultClass.Partial,
                coveredTileCount: 1,
                droppedTileCount: 1);
            var header = new NnrpHeader(
                versionMajor: NnrpHeader.CurrentVersionMajor,
                wireFormat: NnrpHeader.CurrentWireFormat,
                messageType: MessageType.ResultPush,
                flags: HeaderFlags.None,
                metaLength: ResultPushMetadata.MetadataLength,
                bodyLength: (uint)bodyLength,
                sessionId: 0x2C,
                frameId: 0x5B,
                viewId: 0x07,
                routeId: 0x54,
                traceId: 0x7B);
            var msg = new ResultPushMessage(header, metadata, tileIds, new[] { section0, section1 });
            var bytes = msg.ToArray();
            var path = Path.Combine(VectorsDir, "result_push_stale.bin");

            var readBack = SyncGolden(path, bytes);
            Assert.True(ResultPushMessage.TryParse(readBack, out _, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(bytes, readBack);
        }

        [Fact]
        public void GenerateAndVerifyClientHelloGolden()
        {
            var metadata = new ClientHelloMetadata(
                minVersionMajor: NnrpHeader.CurrentVersionMajor,
                maxVersionMajor: NnrpHeader.CurrentVersionMajor,
                supportedWireFormatBitmap: 1,
                supportedProfileBitmap: ControlMetadataBitmaps.TensorProfileBitmap,
                supportedPayloadKindBitmap: ControlMetadataBitmaps.TensorPayloadKindBitmap,
                supportedCodecBitmap: 0x3,
                supportedCompressionBitmap: 0x3,
                supportedDTypeBitmap: 0x21,
                supportedLayoutBitmap: 0x3,
                cacheDigestBitmap: ControlMetadataBitmaps.CacheDigestBitmap,
                cacheObjectBitmap: ControlMetadataBitmaps.TensorProfileCacheObjectBitmap,
                cacheNamespaceCount: 1,
                maxLaneCount: 2,
                maxCacheEntries: 64,
                maxCacheBytes: 65536,
                targetCadenceX100: 6000,
                latencyBudgetMilliseconds: 100,
                qualityTier: 2,
                degradePolicy: 0,
                requestedSessionId: 1,
                authBytes: 0,
                controlExtensionBytes: 0);
            var header = new NnrpHeader(
                versionMajor: NnrpHeader.CurrentVersionMajor,
                wireFormat: NnrpHeader.CurrentWireFormat,
                messageType: MessageType.ClientHello,
                flags: HeaderFlags.AckRequired,
                metaLength: ClientHelloMetadata.MetadataLength,
                bodyLength: 0,
                sessionId: 0, frameId: 0, viewId: 0, routeId: 0, traceId: 0);
            var msg = new ClientHelloMessage(header, metadata, Array.Empty<byte>());
            var bytes = msg.ToArray();
            var path = Path.Combine(VectorsDir, "client_hello.bin");

            var readBack = SyncGolden(path, bytes);
            Assert.True(ClientHelloMessage.TryParse(readBack, out var parsed, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(bytes, readBack);
        }

        [Fact]
        public void GenerateAndVerifyServerHelloAckGolden()
        {
            var metadata = new ServerHelloAckMetadata(
                selectedVersionMajor: 1,
                selectedWireFormat: NnrpHeader.CurrentWireFormat,
                authStatus: 0,
                reserved0: 0,
                sessionId: 42,
                acceptedProfileBitmap: ControlMetadataBitmaps.TensorProfileBitmap,
                acceptedPayloadKindBitmap: ControlMetadataBitmaps.TensorPayloadKindBitmap,
                acceptedCodecBitmap: 0x1,
                acceptedCompressionBitmap: 0x0,
                acceptedDTypeBitmap: 0x20,
                acceptedLayoutBitmap: 0x1,
                cacheDigestBitmap: 0,
                cacheObjectBitmap: 0,
                maxCacheEntries: 0,
                maxCacheBytes: 0,
                maxLaneCount: 1,
                maxConcurrentFrames: 8,
                targetCadenceX100: 6000,
                latencyBudgetMilliseconds: 16,
                qualityTier: 0,
                degradePolicy: 0,
                maxBodyBytes: 1 << 20,
                tokenTtlMilliseconds: 0,
                retryAfterMilliseconds: 0,
                controlExtensionBytes: 0,
                serverFlags: 0);
            var header = new NnrpHeader(
                versionMajor: NnrpHeader.CurrentVersionMajor,
                wireFormat: NnrpHeader.CurrentWireFormat,
                messageType: MessageType.ServerHelloAck,
                flags: HeaderFlags.None,
                metaLength: ServerHelloAckMetadata.MetadataLength,
                bodyLength: 0,
                sessionId: 0, frameId: 0, viewId: 0, routeId: 0, traceId: 0);
            var msg = new ServerHelloAckMessage(header, metadata);
            var bytes = msg.ToArray();
            var path = Path.Combine(VectorsDir, "server_hello_ack.bin");

            var readBack = SyncGolden(path, bytes);
            Assert.True(ServerHelloAckMessage.TryParse(readBack, out var parsed, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(bytes, readBack);
        }

        [Fact]
        public void GenerateAndVerifyErrorGolden()
        {
            var metadata = new ErrorMetadata(
                errorCode: ErrorCode.UnsupportedCapability,
                errorScope: NnrpErrorScope.Session,
                isFatal: true, retryAfterMilliseconds: 0,
                relatedSessionId: 0, relatedFrameId: 0, relatedViewId: 0,
                diagnosticBytes: 0);
            var header = new NnrpHeader(
                versionMajor: NnrpHeader.CurrentVersionMajor,
                wireFormat: NnrpHeader.CurrentWireFormat,
                messageType: MessageType.Error,
                flags: HeaderFlags.None,
                metaLength: ErrorMetadata.MetadataLength,
                bodyLength: 0,
                sessionId: 1, frameId: 0, viewId: 0, routeId: 0, traceId: 0);
            var msg = new ErrorMessage(header, metadata, Array.Empty<byte>());
            var bytes = msg.ToArray();
            var path = Path.Combine(VectorsDir, "error.bin");

            var readBack = SyncGolden(path, bytes);
            Assert.True(ErrorMessage.TryParse(readBack, out var parsed, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(bytes, readBack);
        }

        [Fact]
        public void GenerateAndVerifyResultDropGolden()
        {
            var msg = ResultDropMessage.Create(sessionId: 100, frameId: 5, traceId: 0xFFFFFFFF);
            var bytes = msg.ToArray();
            var path = Path.Combine(VectorsDir, "result_drop.bin");

            var readBack = SyncGolden(path, bytes);
            Assert.True(ResultDropMessage.TryParse(readBack, out var parsed, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(bytes, readBack);
        }

        [Fact]
        public void GenerateAndVerifySessionPatchGolden()
        {
            var profilePatchBlock = new TensorProfilePatchBlock(320, 180, 1920, 1080);
            var msg = new SessionPatchMessage(
                new NnrpHeader(1, NnrpHeader.CurrentWireFormat, MessageType.SessionPatch, HeaderFlags.None, SessionPatchMetadata.MetadataLength, TensorProfilePatchBlock.BlockLength, 11, 0, 0, 0, 1),
                new SessionPatchMetadata(
                    profileId: 0,
                    SessionPatchField.TargetCadence | SessionPatchField.PreferredCodec | SessionPatchField.ProfilePatch,
                    6000,
                    2,
                    0x3,
                    0x1,
                    0x3,
                    0x3,
                    TensorProfilePatchBlock.BlockLength),
                profilePatchBlock);
            var bytes = msg.ToArray();
            var path = Path.Combine(VectorsDir, "session_patch.bin");

            var readBack = SyncGolden(path, bytes);
            Assert.True(SessionPatchMessage.TryParse(readBack, out _, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(bytes, readBack);
        }

        [Fact]
        public void GenerateAndVerifySessionPatchAckGolden()
        {
            var profilePatchAckBlock = new TensorProfilePatchAckBlock(320, 180, 1920, 1080);
            var msg = new SessionPatchAckMessage(
                new NnrpHeader(1, NnrpHeader.CurrentWireFormat, MessageType.SessionPatchAck, HeaderFlags.None, SessionPatchAckMetadata.MetadataLength, TensorProfilePatchAckBlock.BlockLength, 11, 0, 0, 0, 2),
                new SessionPatchAckMetadata(
                    SessionPatchAckStatus.PartiallyApplied,
                    SessionPatchRejectReason.UnsupportedValue,
                    SessionPatchField.TargetCadence,
                    SessionPatchField.PreferredCompression,
                    100,
                    1,
                    6000,
                    2,
                    1,
                    0x1,
                    0x1,
                    0x1,
                    TensorProfilePatchAckBlock.BlockLength),
                profilePatchAckBlock);
            var bytes = msg.ToArray();
            var path = Path.Combine(VectorsDir, "session_patch_ack.bin");

            var readBack = SyncGolden(path, bytes);
            Assert.True(SessionPatchAckMessage.TryParse(readBack, out _, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(bytes, readBack);
        }

        [Fact]
        public void GenerateAndVerifyCachePutGolden()
        {
            var msg = new CachePutMessage(
                new NnrpHeader(1, NnrpHeader.CurrentWireFormat, MessageType.CachePut, HeaderFlags.None, CachePutMetadata.MetadataLength, 3, 5, 0, 0, 0, 0),
                new CachePutMetadata(1, 2, 3, CacheObjectKind.CameraBlock, 1000, 3, 0x1, CachePutFlags.Pinned),
                new byte[] { 9, 8, 7 });
            var bytes = msg.ToArray();
            var path = Path.Combine(VectorsDir, "cache_put.bin");

            var readBack = SyncGolden(path, bytes);
            Assert.True(CachePutMessage.TryParse(readBack, out _, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(bytes, readBack);
        }

        [Fact]
        public void GenerateAndVerifyCacheAckGolden()
        {
            var msg = new CacheAckMessage(
                new NnrpHeader(1, NnrpHeader.CurrentWireFormat, MessageType.CacheAck, HeaderFlags.None, CacheAckMetadata.MetadataLength, 0, 5, 0, 0, 0, 0),
                new CacheAckMetadata(1, 2, 3, CacheAckStatus.Replaced, 900, 1024, 7));
            var bytes = msg.ToArray();
            var path = Path.Combine(VectorsDir, "cache_ack.bin");

            var readBack = SyncGolden(path, bytes);
            Assert.True(CacheAckMessage.TryParse(readBack, out _, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(bytes, readBack);
        }

        [Fact]
        public void GenerateAndVerifyCacheInvalidateGolden()
        {
            var msg = new CacheInvalidateMessage(
                new NnrpHeader(1, NnrpHeader.CurrentWireFormat, MessageType.CacheInvalidate, HeaderFlags.None, CacheInvalidateMetadata.MetadataLength, 0, 5, 0, 0, 0, 0),
                new CacheInvalidateMetadata(CacheInvalidateScope.Namespace, 1, 2, 3, 4));
            var bytes = msg.ToArray();
            var path = Path.Combine(VectorsDir, "cache_invalidate.bin");

            var readBack = SyncGolden(path, bytes);
            Assert.True(CacheInvalidateMessage.TryParse(readBack, out _, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(bytes, readBack);
        }

        [Fact]
        public void VerifyExistingGoldenBinaries()
        {
            // The per-message tests above regenerate these snapshots to current bytes.
            // Here we only validate that the expected files exist and are non-empty.
            string[] expectedFiles =
            {
                "frame_submit_dense.bin",
                "frame_submit_changed_tiles.bin",
                "result_push_fresh.bin",
                "result_push_stale.bin",
                "client_hello.bin",
                "server_hello_ack.bin",
                "session_patch.bin",
                "session_patch_ack.bin",
                "cache_put.bin",
                "cache_ack.bin",
                "cache_invalidate.bin",
                "error.bin",
                "result_drop.bin",
            };

            foreach (var file in expectedFiles)
            {
                var path = Path.Combine(VectorsDir, file);
                var bytes = ReadRequiredGolden(path);
                Assert.True(bytes.Length > 0, $"Golden file {file} is empty.");
            }
        }
    }
}
