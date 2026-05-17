using System;
using Nnrp.Core;
using Xunit;

namespace Nnrp.Core.Tests
{
    public sealed class CoreCoverageBoostTests
    {
        [Fact]
        public void FixedWidthMetadataTypesHandleShortBuffersRoundTripAndEquality()
        {
            var cachePut = new CachePutMetadata(1, 2, 3, CacheObjectKind.CodecAuxBlock, 4000, 64, 0x7, CachePutFlags.Pinned);
            Assert.False(cachePut.TryWrite(new byte[CachePutMetadata.MetadataLength - 1], out var putWritten));
            Assert.Equal(0, putWritten);
            Assert.Throws<ArgumentException>(() => cachePut.Write(new byte[CachePutMetadata.MetadataLength - 1]));
            Assert.False(CachePutMetadata.TryParse(new byte[CachePutMetadata.MetadataLength - 1], out _, out var putError));
            Assert.Equal(NnrpParseError.SourceTooShort, putError);
            Assert.True(CachePutMetadata.TryParse(cachePut.ToArray(), out var parsedPut, out putError));
            Assert.Equal(cachePut, parsedPut);
            Assert.True(cachePut.Equals((object)parsedPut));
            Assert.False(cachePut.Equals("not-cache-put"));
            Assert.Equal(cachePut.GetHashCode(), parsedPut.GetHashCode());

            var cacheAck = new CacheAckMetadata(1, 2, 3, CacheAckStatus.Replaced, 5000, 1024, 8);
            Assert.False(cacheAck.TryWrite(new byte[CacheAckMetadata.MetadataLength - 1], out var ackWritten));
            Assert.Equal(0, ackWritten);
            Assert.Throws<ArgumentException>(() => cacheAck.Write(new byte[CacheAckMetadata.MetadataLength - 1]));
            Assert.False(CacheAckMetadata.TryParse(new byte[CacheAckMetadata.MetadataLength - 1], out _, out var ackError));
            Assert.Equal(NnrpParseError.SourceTooShort, ackError);
            Assert.True(CacheAckMetadata.TryParse(cacheAck.ToArray(), out var parsedAck, out ackError));
            Assert.Equal(cacheAck, parsedAck);
            Assert.True(cacheAck.Equals((object)parsedAck));
            Assert.False(cacheAck.Equals("not-cache-ack"));
            Assert.Equal(cacheAck.GetHashCode(), parsedAck.GetHashCode());

            var invalidate = new CacheInvalidateMetadata(CacheInvalidateScope.Namespace, 4, 5, 6, 7);
            Assert.False(invalidate.TryWrite(new byte[CacheInvalidateMetadata.MetadataLength - 1], out var invalidateWritten));
            Assert.Equal(0, invalidateWritten);
            Assert.Throws<ArgumentException>(() => invalidate.Write(new byte[CacheInvalidateMetadata.MetadataLength - 1]));
            Assert.False(CacheInvalidateMetadata.TryParse(new byte[CacheInvalidateMetadata.MetadataLength - 1], out _, out var invalidateError));
            Assert.Equal(NnrpParseError.SourceTooShort, invalidateError);
            Assert.True(CacheInvalidateMetadata.TryParse(invalidate.ToArray(), out var parsedInvalidate, out invalidateError));
            Assert.Equal(invalidate, parsedInvalidate);
            Assert.True(invalidate.Equals((object)parsedInvalidate));
            Assert.False(invalidate.Equals("not-cache-invalidate"));
            Assert.Equal(invalidate.GetHashCode(), parsedInvalidate.GetHashCode());

            var errorMetadata = new ErrorMetadata(ErrorCode.InternalError, NnrpErrorScope.Connection, true, 88, 9, 10, 11, 12);
            Assert.False(errorMetadata.TryWrite(new byte[ErrorMetadata.MetadataLength - 1], out var errorWritten));
            Assert.Equal(0, errorWritten);
            Assert.Throws<ArgumentException>(() => errorMetadata.Write(new byte[ErrorMetadata.MetadataLength - 1]));
            Assert.False(ErrorMetadata.TryParse(new byte[ErrorMetadata.MetadataLength - 1], out _, out var metadataError));
            Assert.Equal(NnrpParseError.SourceTooShort, metadataError);
            Assert.True(ErrorMetadata.TryParse(errorMetadata.ToArray(), out var parsedErrorMetadata, out metadataError));
            Assert.Equal(errorMetadata, parsedErrorMetadata);
            Assert.True(errorMetadata.Equals((object)parsedErrorMetadata));
            Assert.False(errorMetadata.Equals("not-error"));
            Assert.Equal(errorMetadata.GetHashCode(), parsedErrorMetadata.GetHashCode());

            var patch = new SessionPatchMetadata(
                profileId: 0,
                SessionPatchField.TargetCadence | SessionPatchField.PreferredCodec | SessionPatchField.ProfilePatch,
                6000,
                2,
                3,
                1,
                0x3,
                0x3,
                TensorProfilePatchBlock.BlockLength,
                reserved0: 7);
            Assert.False(patch.TryWrite(new byte[SessionPatchMetadata.MetadataLength - 1], out var patchWritten));
            Assert.Equal(0, patchWritten);
            Assert.Throws<ArgumentException>(() => patch.Write(new byte[SessionPatchMetadata.MetadataLength - 1]));
            Assert.False(SessionPatchMetadata.TryParse(new byte[SessionPatchMetadata.MetadataLength - 1], strict: false, out _, out var patchError));
            Assert.Equal(NnrpParseError.SourceTooShort, patchError);
            Assert.False(SessionPatchMetadata.TryParse(patch.ToArray(), strict: true, out _, out patchError));
            Assert.Equal(NnrpParseError.NonZeroReservedField, patchError);
            Assert.True(SessionPatchMetadata.TryParse(patch.ToArray(), strict: false, out var parsedPatch, out patchError));
            Assert.Equal(patch, parsedPatch);
            Assert.True(patch.Equals((object)parsedPatch));
            Assert.False(patch.Equals("not-patch"));
            Assert.Equal(patch.GetHashCode(), parsedPatch.GetHashCode());

            var patchAck = new SessionPatchAckMetadata(
                SessionPatchAckStatus.PartiallyApplied,
                SessionPatchRejectReason.UnsupportedValue,
                SessionPatchField.TargetCadence,
                SessionPatchField.PreferredCompression,
                100,
                1,
                6000,
                2,
                1,
                1,
                0x1,
                0x1,
                TensorProfilePatchAckBlock.BlockLength);
            Assert.False(patchAck.TryWrite(new byte[SessionPatchAckMetadata.MetadataLength - 1], out var patchAckWritten));
            Assert.Equal(0, patchAckWritten);
            Assert.Throws<ArgumentException>(() => patchAck.Write(new byte[SessionPatchAckMetadata.MetadataLength - 1]));
            Assert.False(SessionPatchAckMetadata.TryParse(new byte[SessionPatchAckMetadata.MetadataLength - 1], out _, out var patchAckError));
            Assert.Equal(NnrpParseError.SourceTooShort, patchAckError);
            Assert.True(SessionPatchAckMetadata.TryParse(patchAck.ToArray(), out var parsedPatchAck, out patchAckError));
            Assert.Equal(patchAck, parsedPatchAck);
            Assert.True(patchAck.Equals((object)parsedPatchAck));
            Assert.False(patchAck.Equals("not-patch-ack"));
            Assert.Equal(patchAck.GetHashCode(), parsedPatchAck.GetHashCode());
        }

        [Fact]
        public void HelloMetadataAndMessagesHandleShortBuffersAndInvalidLayouts()
        {
            var helloMetadata = new ClientHelloMetadata(
                minVersionMajor: 1,
                maxVersionMajor: 1,
                supportedWireFormatBitmap: 1,
                supportedProfileBitmap: ControlMetadataBitmaps.TensorProfileBitmap,
                supportedPayloadKindBitmap: ControlMetadataBitmaps.TensorPayloadKindBitmap,
                supportedCodecBitmap: 0x3,
                supportedCompressionBitmap: 0x3,
                supportedDTypeBitmap: 0x6,
                supportedLayoutBitmap: 0x1,
                cacheDigestBitmap: ControlMetadataBitmaps.CacheDigestBitmap,
                cacheObjectBitmap: ControlMetadataBitmaps.TensorProfileCacheObjectBitmap,
                cacheNamespaceCount: 1,
                maxLaneCount: 2,
                maxCacheEntries: 64,
                maxCacheBytes: 1024,
                targetCadenceX100: 6000,
                latencyBudgetMilliseconds: 50,
                qualityTier: 1,
                degradePolicy: 0,
                requestedSessionId: 41,
                authBytes: 3,
                controlExtensionBytes: 0);
            Assert.False(helloMetadata.TryWrite(new byte[ClientHelloMetadata.MetadataLength - 1], out var helloWritten));
            Assert.Equal(0, helloWritten);
            Assert.Throws<ArgumentException>(() => helloMetadata.Write(new byte[ClientHelloMetadata.MetadataLength - 1]));
            Assert.False(ClientHelloMetadata.TryParse(new byte[ClientHelloMetadata.MetadataLength - 1], out _, out var helloMetadataError));
            Assert.Equal(NnrpParseError.SourceTooShort, helloMetadataError);
            Assert.True(ClientHelloMetadata.TryParse(helloMetadata.ToArray(), out var parsedHelloMetadata, out helloMetadataError));
            Assert.Equal(helloMetadata, parsedHelloMetadata);
            Assert.False(helloMetadata.Equals("not-hello"));
            Assert.Equal(helloMetadata.GetHashCode(), parsedHelloMetadata.GetHashCode());

            var helloHeader = new NnrpHeader(
                NnrpHeader.CurrentVersionMajor,
                NnrpHeader.CurrentWireFormat,
                MessageType.ClientHello,
                HeaderFlags.AckRequired,
                ClientHelloMetadata.MetadataLength,
                3,
                0,
                0,
                0,
                0,
                1);
            var helloMessage = new ClientHelloMessage(helloHeader, helloMetadata, new byte[] { 1, 2, 3 });
            Assert.True(ClientHelloMessage.TryParse(helloMessage.ToArray(), out var parsedHelloMessage, out var helloMessageError));
            Assert.Equal(NnrpParseError.None, helloMessageError);
            Assert.Equal(41u, parsedHelloMessage.Metadata.RequestedSessionId);

            Assert.Throws<ArgumentException>(() => new ClientHelloMessage(
                new NnrpHeader(NnrpHeader.CurrentVersionMajor, NnrpHeader.CurrentWireFormat, MessageType.Ping, HeaderFlags.None, ClientHelloMetadata.MetadataLength, 3, 0, 0, 0, 0, 0),
                helloMetadata,
                new byte[] { 1, 2, 3 }));
            Assert.Throws<ArgumentException>(() => new ClientHelloMessage(helloHeader, helloMetadata, new byte[] { 1, 2 }));

            var invalidHello = new NnrpFramedMessage(
                new NnrpHeader(
                    NnrpHeader.CurrentVersionMajor,
                    NnrpHeader.CurrentWireFormat,
                    MessageType.ClientHello,
                    HeaderFlags.AckRequired,
                    ClientHelloMetadata.MetadataLength,
                    2,
                    0,
                    0,
                    0,
                    0,
                    1),
                helloMetadata.ToArray(),
                new byte[] { 1, 2 });
            Assert.False(ClientHelloMessage.TryParse(invalidHello, out _, out helloMessageError));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, helloMessageError);

            var ackMetadata = new ServerHelloAckMetadata(
                selectedVersionMajor: 1,
                selectedWireFormat: 1,
                authStatus: 0,
                reserved0: 0,
                sessionId: 77,
                acceptedProfileBitmap: ControlMetadataBitmaps.TensorProfileBitmap,
                acceptedPayloadKindBitmap: ControlMetadataBitmaps.TensorPayloadKindBitmap,
                acceptedCodecBitmap: 0x3,
                acceptedCompressionBitmap: 0x3,
                acceptedDTypeBitmap: 0x6,
                acceptedLayoutBitmap: 0x1,
                cacheDigestBitmap: 0x1,
                cacheObjectBitmap: 0x1,
                maxCacheEntries: 64,
                maxCacheBytes: 1024,
                maxLaneCount: 2,
                maxConcurrentFrames: 2,
                targetCadenceX100: 6000,
                latencyBudgetMilliseconds: 16,
                qualityTier: 1,
                degradePolicy: 0,
                maxBodyBytes: 4096,
                tokenTtlMilliseconds: 1000,
                retryAfterMilliseconds: 0,
                controlExtensionBytes: 0,
                serverFlags: 0);
            Assert.False(ackMetadata.TryWrite(new byte[ServerHelloAckMetadata.MetadataLength - 1], out var ackMetadataWritten));
            Assert.Equal(0, ackMetadataWritten);
            Assert.Throws<ArgumentException>(() => ackMetadata.Write(new byte[ServerHelloAckMetadata.MetadataLength - 1]));
            Assert.False(ServerHelloAckMetadata.TryParse(new byte[ServerHelloAckMetadata.MetadataLength - 1], out _, out var ackMetadataError));
            Assert.Equal(NnrpParseError.SourceTooShort, ackMetadataError);
            Assert.True(ServerHelloAckMetadata.TryParse(ackMetadata.ToArray(), out var parsedAckMetadata, out ackMetadataError));
            Assert.Equal(ackMetadata, parsedAckMetadata);
            Assert.False(ackMetadata.Equals("not-ack"));
            Assert.Equal(ackMetadata.GetHashCode(), parsedAckMetadata.GetHashCode());

            var ackHeader = new NnrpHeader(
                NnrpHeader.CurrentVersionMajor,
                NnrpHeader.CurrentWireFormat,
                MessageType.ServerHelloAck,
                HeaderFlags.None,
                ServerHelloAckMetadata.MetadataLength,
                0,
                77,
                0,
                0,
                0,
                2);
            var ackMessage = new ServerHelloAckMessage(ackHeader, ackMetadata);
            Assert.True(ServerHelloAckMessage.TryParse(ackMessage.ToArray(), out var parsedAckMessage, out var ackMessageError));
            Assert.Equal(NnrpParseError.None, ackMessageError);
            Assert.Equal(77u, parsedAckMessage.Metadata.SessionId);

            Assert.Throws<ArgumentException>(() => new ServerHelloAckMessage(
                new NnrpHeader(NnrpHeader.CurrentVersionMajor, NnrpHeader.CurrentWireFormat, MessageType.Pong, HeaderFlags.None, ServerHelloAckMetadata.MetadataLength, 0, 0, 0, 0, 0, 0),
                ackMetadata));
            Assert.Throws<ArgumentException>(() => new ServerHelloAckMessage(
                new NnrpHeader(NnrpHeader.CurrentVersionMajor, NnrpHeader.CurrentWireFormat, MessageType.ServerHelloAck, HeaderFlags.None, ServerHelloAckMetadata.MetadataLength, 1, 0, 0, 0, 0, 0),
                ackMetadata));

            var invalidAck = new NnrpFramedMessage(
                new NnrpHeader(NnrpHeader.CurrentVersionMajor, NnrpHeader.CurrentWireFormat, MessageType.ServerHelloAck, HeaderFlags.None, ServerHelloAckMetadata.MetadataLength, 1, 77, 0, 0, 0, 2),
                ackMetadata.ToArray(),
                new byte[] { 0xFF });
            Assert.False(ServerHelloAckMessage.TryParse(invalidAck, out _, out ackMessageError));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, ackMessageError);
        }

        [Fact]
        public void BinaryAlignmentLongOverloadAndTensorSectionBlockValidationBranches()
        {
            Assert.Equal(16L, BinaryAlignment.AlignUp(9L, 8));
            Assert.Equal(16L, BinaryAlignment.AlignUp(16L, 8));
            Assert.Throws<ArgumentOutOfRangeException>(() => BinaryAlignment.AlignUp(-1L, 8));
            Assert.Throws<ArgumentOutOfRangeException>(() => BinaryAlignment.AlignUp(1L, 0));

            var descriptor = new TensorSectionDescriptor(
                TensorRole.LumaHint,
                CodecId.Raw,
                DTypeId.UInt8,
                TensorLayoutId.Nhwc,
                ScalePolicy.None,
                0,
                1,
                0,
                4,
                2,
                0);
            var section = new TensorSectionBlock(descriptor, Array.Empty<byte>(), new byte[] { 1, 0, 0, 0 }, new byte[] { 0xAA, 0xBB });
            Assert.False(section.TryCopyTo(new byte[section.TotalLength - 1], out _));

            Assert.Throws<ArgumentException>(() => new TensorSectionBlock(descriptor, new byte[] { 1 }, new byte[] { 1, 0, 0, 0 }, new byte[] { 0xAA, 0xBB }));
            Assert.Throws<ArgumentException>(() => new TensorSectionBlock(descriptor, Array.Empty<byte>(), new byte[] { 1, 0, 0 }, new byte[] { 0xAA, 0xBB }));
            Assert.Throws<ArgumentException>(() => new TensorSectionBlock(descriptor, Array.Empty<byte>(), new byte[] { 1, 0, 0, 0 }, new byte[] { 0xAA }));

            var invalidLengthDescriptor = new TensorSectionDescriptor(
                TensorRole.LumaHint,
                CodecId.Raw,
                DTypeId.UInt8,
                TensorLayoutId.Nhwc,
                ScalePolicy.None,
                0,
                1,
                0,
                6,
                2,
                0);
            var invalidLengthBytes = BuildSectionBytes(invalidLengthDescriptor, Array.Empty<byte>(), new byte[] { 1, 0, 0, 0, 2, 0 }, new byte[] { 0xAA, 0xBB });
            Assert.False(TensorSectionBlock.TryParse(invalidLengthBytes, expectedTileCount: 1, out _, out _, out var parseError));
            Assert.Equal(NnrpParseError.InconsistentSectionDescriptor, parseError);

            var mismatchedTileDescriptor = new TensorSectionDescriptor(
                TensorRole.LumaHint,
                CodecId.Raw,
                DTypeId.UInt8,
                TensorLayoutId.Nhwc,
                ScalePolicy.None,
                0,
                1,
                0,
                8,
                2,
                0);
            var mismatchedTileBytes = BuildSectionBytes(mismatchedTileDescriptor, Array.Empty<byte>(), new byte[] { 1, 0, 0, 0, 2, 0, 0, 0 }, new byte[] { 0xAA, 0xBB });
            Assert.False(TensorSectionBlock.TryParse(mismatchedTileBytes, expectedTileCount: 1, out _, out _, out parseError));
            Assert.Equal(NnrpParseError.InconsistentSectionDescriptor, parseError);
        }

        [Fact]
        public void FrameSubmitTryParseRejectsInvalidPaddingAndOutOfOrderSectionRoles()
        {
            var message = CreateFrameSubmitMessage(out var packetPaddingIndex);
            var packetBytes = message.ToArray();
            packetBytes[packetPaddingIndex] = 0x7F;
            Assert.False(FrameSubmitMessage.TryParse(packetBytes, out _, out var paddingError));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, paddingError);

            var sections = new[]
            {
                CreateSection(TensorRole.SrResidual),
                CreateSection(TensorRole.LumaHint),
            };
            var outOfOrder = CreateFrameSubmitMessage(out _, sections: sections);
            Assert.False(FrameSubmitMessage.TryParse(outOfOrder.ToArray(), out _, out var orderError));
            Assert.Equal(NnrpParseError.InconsistentSectionDescriptor, orderError);
        }

        [Fact]
        public void ResultPushTryParseRejectsInvalidPaddingAndOutOfOrderSectionRoles()
        {
            var message = CreateResultPushMessage(out var packetPaddingIndex);
            var packetBytes = message.ToArray();
            packetBytes[packetPaddingIndex] = 0x7F;
            Assert.False(ResultPushMessage.TryParse(packetBytes, out _, out var paddingError));
            Assert.Equal(NnrpParseError.SourceTooShort, paddingError);

            var sections = new[]
            {
                CreateSection(TensorRole.SrResidual),
                CreateSection(TensorRole.LumaHint),
            };
            var outOfOrder = CreateResultPushMessage(out _, sections: sections);
            Assert.False(ResultPushMessage.TryParse(outOfOrder.ToArray(), out _, out var orderError));
            Assert.Equal(NnrpParseError.InconsistentSectionDescriptor, orderError);
        }

        private static FrameSubmitMessage CreateFrameSubmitMessage(out int packetPaddingIndex, TensorSectionBlock[]? sections = null)
        {
            sections ??= new[]
            {
                CreateSection(TensorRole.LumaHint),
                CreateSection(TensorRole.SrResidual),
            };
            var cameraBlock = new byte[] { 0x10, 0x20, 0x30 };
            var tileIds = new ushort[] { 3, 5 };
            var tileIndexBytes = TileIndexBlockCodec.GetEncodedLength(tileIds, TileIndexMode.RawUInt16);
            var bodyLength = BinaryAlignment.AlignUp(cameraBlock.Length, 8);
            bodyLength += tileIndexBytes;
            bodyLength = BinaryAlignment.AlignUp(bodyLength, 8) + sections[0].TotalLength;
            bodyLength = BinaryAlignment.AlignUp(bodyLength, 8) + sections[1].TotalLength;

            var metadata = new FrameSubmitMetadata(
                sourceWidth: 640,
                sourceHeight: 360,
                tileWidth: 32,
                tileHeight: 32,
                tileCount: (ushort)tileIds.Length,
                sectionCount: (ushort)sections.Length,
                frameClass: FrameClass.Keyframe,
                inputProfile: InputProfile.DenseLumaFrame,
                tileIndexMode: TileIndexMode.RawUInt16,
                latencyBudgetMilliseconds: 16,
                targetFpsTimes100: 6000,
                retryOfFrame: 0,
                tileBaseId: 0,
                cameraBytes: (uint)cameraBlock.Length,
                tileIndexBytes: (uint)tileIndexBytes);
            var header = new NnrpHeader(
                NnrpHeader.CurrentVersionMajor,
                NnrpHeader.CurrentWireFormat,
                MessageType.FrameSubmit,
                HeaderFlags.None,
                FrameSubmitMessage.MetadataLength,
                (uint)bodyLength,
                1,
                2,
                0,
                0,
                3);
            packetPaddingIndex = NnrpHeader.HeaderLength + FrameSubmitMessage.MetadataLength + cameraBlock.Length;
            return new FrameSubmitMessage(header, metadata, cameraBlock, tileIds, sections);
        }

        private static ResultPushMessage CreateResultPushMessage(out int packetPaddingIndex, TensorSectionBlock[]? sections = null)
        {
            sections ??= new[]
            {
                CreateSection(TensorRole.LumaHint),
                CreateSection(TensorRole.SrResidual),
            };
            var tileIds = new ushort[] { 3, 5 };
            var tileIndexBytes = TileIndexBlockCodec.GetEncodedLength(tileIds, TileIndexMode.RawUInt16);
            var bodyLength = TensorResultBlock.BlockLength + tileIndexBytes;
            bodyLength = BinaryAlignment.AlignUp(bodyLength, 8) + sections[0].TotalLength;
            bodyLength = BinaryAlignment.AlignUp(bodyLength, 8) + sections[1].TotalLength;

            var metadata = new ResultPushMetadata(
                statusCode: ResultStatusCode.Success,
                resultFlags: ResultFlags.None,
                sectionCount: (ushort)sections.Length,
                tileCount: (ushort)tileIds.Length,
                activeProfileId: 1,
                inferenceMilliseconds: 2,
                queueMilliseconds: 1,
                serverTotalMilliseconds: 4,
                tileBaseId: 0,
                tileIndexBytes: (uint)tileIndexBytes);
            var header = new NnrpHeader(
                NnrpHeader.CurrentVersionMajor,
                NnrpHeader.CurrentWireFormat,
                MessageType.ResultPush,
                HeaderFlags.None,
                ResultPushMetadata.MetadataLength,
                (uint)bodyLength,
                1,
                2,
                0,
                0,
                3);
            packetPaddingIndex = NnrpHeader.HeaderLength + ResultPushMetadata.MetadataLength + TensorResultBlock.BlockLength + tileIndexBytes;
            return new ResultPushMessage(header, metadata, tileIds, sections);
        }

        private static TensorSectionBlock CreateSection(TensorRole role)
        {
            return new TensorSectionBlock(
                new TensorSectionDescriptor(
                    role,
                    CodecId.Raw,
                    DTypeId.UInt8,
                    TensorLayoutId.Nhwc,
                    ScalePolicy.None,
                    0,
                    1,
                    0,
                    8,
                    2,
                    0),
                Array.Empty<byte>(),
                new byte[] { 1, 0, 0, 0, 2, 0, 0, 0 },
                new byte[] { 0xAA, 0xBB });
        }

        private static byte[] BuildSectionBytes(
            TensorSectionDescriptor descriptor,
            byte[] codecTable,
            byte[] lengthTable,
            byte[] payload)
        {
            var bytes = new byte[TensorSectionDescriptor.DescriptorLength + codecTable.Length + lengthTable.Length + payload.Length];
            Assert.True(descriptor.TryWrite(bytes, out var descriptorBytes));
            codecTable.CopyTo(bytes, descriptorBytes);
            lengthTable.CopyTo(bytes, descriptorBytes + codecTable.Length);
            payload.CopyTo(bytes, descriptorBytes + codecTable.Length + lengthTable.Length);
            return bytes;
        }
    }
}
