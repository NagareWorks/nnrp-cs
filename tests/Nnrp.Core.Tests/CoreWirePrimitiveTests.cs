using System;
using Nnrp.Core;
using Xunit;

namespace Nnrp.Core.Tests
{
    public sealed class CoreWirePrimitiveTests
    {
        [Fact]
        public void HeaderRoundTripsThroughBytes()
        {
            var header = new NnrpHeader(
                versionMajor: NnrpHeader.CurrentVersionMajor,
                wireFormat: NnrpHeader.CurrentWireFormat,
                messageType: MessageType.FrameSubmit,
                flags: HeaderFlags.AckRequired | HeaderFlags.Keyframe,
                metaLength: FrameSubmitMessage.MetadataLength,
                bodyLength: 128,
                sessionId: 42,
                frameId: 7,
                viewId: 1,
                routeId: 0,
                traceId: 0x0102030405060708UL);

            var payload = header.ToArray();

            Assert.Equal(NnrpHeader.HeaderLength, payload.Length);
            Assert.True(NnrpHeader.TryParse(payload, NnrpHeaderParseOptions.Strict, out var parsed, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(header, parsed);
        }

        [Fact]
        public void CurrentHeaderPreservesFixed40ByteLayout()
        {
            var header = new NnrpHeader(
                versionMajor: NnrpHeader.CurrentVersionMajor,
                wireFormat: NnrpHeader.CurrentWireFormat,
                messageType: MessageType.TransportProbe,
                flags: HeaderFlags.AckRequired,
                metaLength: 24,
                bodyLength: 16,
                sessionId: 0,
                frameId: 0,
                viewId: 0,
                routeId: 0,
                traceId: 123);

            var payload = header.ToArray();

            Assert.Equal(NnrpHeader.HeaderLength, payload.Length);
            Assert.Equal(40, payload.Length);
            Assert.True(NnrpHeader.TryParse(payload, NnrpHeaderParseOptions.Strict, out var parsed, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(NnrpHeader.CurrentWireFormat, parsed.WireFormat);
        }

        [Fact]
        public void HeaderTryWriteReportsDestinationTooShort()
        {
            var header = new NnrpHeader(
                NnrpHeader.CurrentVersionMajor,
                NnrpHeader.CurrentWireFormat,
                MessageType.Ping,
                HeaderFlags.None,
                0,
                0,
                1,
                0,
                0,
                0,
                99);

            Assert.False(header.TryWrite(new byte[NnrpHeader.HeaderLength - 1], out var bytesWritten));
            Assert.Equal(0, bytesWritten);
        }

        [Fact]
        public void StrictHeaderParseReportsSpecificFailures()
        {
            var header = new NnrpHeader(
                NnrpHeader.CurrentVersionMajor,
                NnrpHeader.CurrentWireFormat,
                MessageType.Pong,
                HeaderFlags.None,
                0,
                0,
                1,
                0,
                0,
                0,
                99);
            var payload = header.ToArray();

            payload[4] = 2;
            Assert.False(NnrpHeader.TryParse(payload, NnrpHeaderParseOptions.Strict, out _, out var error));
            Assert.Equal(NnrpParseError.UnsupportedVersion, error);

            payload = header.ToArray();
            payload[6] = 0x7F;
            Assert.False(NnrpHeader.TryParse(payload, NnrpHeaderParseOptions.Strict, out _, out error));
            Assert.Equal(NnrpParseError.UnknownMessageType, error);

            payload = header.ToArray();
            payload[8] = 0x40;
            Assert.False(NnrpHeader.TryParse(payload, NnrpHeaderParseOptions.Strict, out _, out error));
            Assert.Equal(NnrpParseError.ReservedFlagsSet, error);
        }

        [Fact]
        public void HeaderParseRejectsConfiguredMessageLengthLimit()
        {
            var header = new NnrpHeader(
                NnrpHeader.CurrentVersionMajor,
                NnrpHeader.CurrentWireFormat,
                MessageType.FrameSubmit,
                HeaderFlags.None,
                100,
                50,
                1,
                2,
                0,
                0,
                99);
            var options = new NnrpHeaderParseOptions(strict: true, maxMessageLength: 128);

            Assert.False(NnrpHeader.TryParse(header.ToArray(), options, out _, out var error));
            Assert.Equal(NnrpParseError.MessageTooLarge, error);
        }

        [Fact]
        public void ProtocolEnumValuesAreStable()
        {
            Assert.Equal(NnrpHeader.CurrentWireFormat, (byte)NnrpHeader.CurrentWireFormat);
            Assert.Equal(NnrpHeader.CurrentWireFormat, (byte)NnrpHeader.CurrentWireFormat);
            Assert.Equal(0x10, (byte)MessageType.FrameSubmit);
            Assert.Equal(0x12, (byte)MessageType.ResultPush);
            Assert.Equal(0x17, (byte)MessageType.FlowUpdate);
            Assert.Equal(0x18, (byte)MessageType.ResultHint);
            Assert.Equal(0x19, (byte)MessageType.TransportProbe);
            Assert.Equal(0x1A, (byte)MessageType.TransportProbeAck);
            Assert.Equal(0x00000020U, (uint)HeaderFlags.Keyframe);
            Assert.Equal(3, (byte)FrameClass.Discardable);
            Assert.Equal(2, (byte)InputProfile.DenseLumaFrame);
            Assert.Equal(3, (byte)TileIndexMode.Bitset);
            Assert.Equal(0x0100, (ushort)TensorRole.SrResidual);
            Assert.Equal(1, (byte)CodecId.Lz4);
            Assert.Equal(7, (byte)DTypeId.UInt16);
            Assert.Equal(1, (byte)TensorLayoutId.Nchw);
            Assert.Equal(3, (byte)ScalePolicy.PerChannel);
            Assert.Equal(3, (ushort)ResultStatusCode.Failed);
            Assert.Equal(0x0004, (ushort)ResultFlags.Partial);
            Assert.Equal(0x000C, (ushort)ErrorCode.InternalError);
            Assert.Equal(0, (byte)SubmitMode.Inline);
            Assert.Equal(1, (byte)SubmitMode.Reference);
            Assert.Equal(2, (byte)SubmitMode.Mixed);
            Assert.Equal(0x01, (byte)BudgetPolicy.AllowPartial);
            Assert.Equal(0x02, (byte)BudgetPolicy.AllowStaleReuse);
            Assert.Equal(0x04, (byte)BudgetPolicy.AllowDegraded);
            Assert.Equal(0x08, (byte)BudgetPolicy.AllowDrop);
            Assert.Equal(0, (byte)ResultClass.Complete);
            Assert.Equal(1, (byte)ResultClass.Partial);
            Assert.Equal(2, (byte)ResultClass.StaleReuse);
            Assert.Equal(3, (byte)ResultClass.Degraded);
            Assert.Equal(0x00000001U, (uint)PayloadKind.Tensor);
            Assert.Equal(0x00000002U, (uint)PayloadKind.TokenChunk);
            Assert.Equal(0x00000004U, (uint)PayloadKind.AudioChunk);
            Assert.Equal(0x00000008U, (uint)PayloadKind.VideoChunk);
            Assert.Equal(0x00000010U, (uint)PayloadKind.StructuredEvent);
            Assert.Equal(0x00000020U, (uint)PayloadKind.ToolDelta);
            Assert.Equal(0x00000040U, (uint)PayloadKind.OpaqueBytes);
            Assert.Equal(0x0000007FU, PayloadKindValidator.AllowedPayloadKindBits);
            Assert.Equal(2U, (uint)TransportId.Tcp);
            Assert.Equal(4, (byte)TransportPolicy.ForceTcp);
            Assert.Equal(3, (byte)LossTolerance.FireAndForget);
            Assert.Equal(6U, (uint)CacheObjectKind.PayloadLayoutTemplate);
            Assert.Equal(0x0000000FU, SubmitObjectReferenceMask.AllowedBits);
            Assert.Equal(0x00000001U, (uint)SubmitObjectSlot.CameraBlock);
            Assert.Equal(0x00000002U, (uint)SubmitObjectSlot.TileIndexBlock);
            Assert.Equal(0x00000004U, (uint)SubmitObjectSlot.TensorSectionTable);
            Assert.Equal(0x00000008U, (uint)SubmitObjectSlot.PayloadLayoutTemplate);
        }

        [Fact]
        public void AlignmentAndCheckedArithmeticHelpersHandleEdges()
        {
            Assert.True(BinaryAlignment.IsAligned(16));
            Assert.False(BinaryAlignment.IsAligned(18));
            Assert.Equal(6, BinaryAlignment.GetPadding(18));
            Assert.True(BinaryAlignment.TryAlignUp(18, out var aligned));
            Assert.Equal(24, aligned);
            Assert.True(BinaryAlignment.ValidateZeroPadding(new byte[] { 0, 0, 0 }));
            Assert.False(BinaryAlignment.ValidateZeroPadding(new byte[] { 0, 1, 0 }));
            Assert.False(CheckedArithmetic.TryAdd(int.MaxValue, 1, out _));
            Assert.True(CheckedArithmetic.TryAdd(10, 20, out var sum));
            Assert.Equal(30, sum);
        }

        [Fact]
        public void FixedBinaryReaderAndWriterRoundTripLittleEndianValues()
        {
            var payload = new byte[15];
            var writer = new FixedBinaryWriter(payload);

            Assert.True(writer.TryWriteByte(0xAA));
            Assert.True(writer.TryWriteUInt16(0x1122));
            Assert.True(writer.TryWriteUInt32(0x33445566));
            Assert.True(writer.TryWriteUInt64(0x778899AABBCCDDEE));
            Assert.Equal(payload.Length, writer.Offset);

            var reader = new FixedBinaryReader(payload);
            Assert.True(reader.TryReadByte(out var byteValue));
            Assert.True(reader.TryReadUInt16(out var ushortValue));
            Assert.True(reader.TryReadUInt32(out var uintValue));
            Assert.True(reader.TryReadUInt64(out var ulongValue));

            Assert.Equal(0xAA, byteValue);
            Assert.Equal(0x1122, ushortValue);
            Assert.Equal(0x33445566U, uintValue);
            Assert.Equal(0x778899AABBCCDDEEUL, ulongValue);
            Assert.Equal(0, reader.Remaining);
        }

        [Fact]
        public void CurrentFrameSubmitMetadataRoundTripsAndRejectsReservedFields()
        {
            var original = SmokePackets.CreateSmokeFrameSubmitMessage(sessionId: 1, frameId: 2);
            var payload = original.ToArray();

            Assert.True(FrameSubmitMessage.TryParse(payload, out var parsed, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(original.Metadata.SourceWidth, parsed.Metadata.SourceWidth);
            Assert.Equal(original.Metadata.SourceHeight, parsed.Metadata.SourceHeight);
            Assert.Equal(original.Metadata.TileWidth, parsed.Metadata.TileWidth);
            Assert.Equal(original.Metadata.TileHeight, parsed.Metadata.TileHeight);
            Assert.Equal(original.Metadata.FrameClass, parsed.Metadata.FrameClass);
            Assert.Equal(original.Metadata.InputProfile, parsed.Metadata.InputProfile);
            Assert.Equal(original.Metadata.TileIndexMode, parsed.Metadata.TileIndexMode);
            Assert.Equal(original.Metadata.LatencyBudgetMilliseconds, parsed.Metadata.LatencyBudgetMilliseconds);
            Assert.Equal(original.Metadata.TargetFpsTimes100, parsed.Metadata.TargetFpsTimes100);
            Assert.Equal(original.Metadata.CameraBytes, parsed.Metadata.CameraBytes);
            Assert.Equal(original.Metadata.TileIndexBytes, parsed.Metadata.TileIndexBytes);

            payload[NnrpHeader.HeaderLength + 36] = 1;
            Assert.False(FrameSubmitMessage.TryParse(payload, out _, out error));
            Assert.Equal(NnrpParseError.NonZeroReservedField, error);
        }

        [Fact]
        public void FrameSubmitMetadataRoundTripsAndRejectsReservedFields()
        {
            var metadata = new FrameSubmitMetadata(
                sourceWidth: 640,
                sourceHeight: 360,
                tileWidth: 32,
                tileHeight: 32,
                tileCount: 80,
                sectionCount: 3,
                frameClass: FrameClass.Keyframe,
                inputProfile: InputProfile.DenseLumaFrame,
                tileIndexMode: TileIndexMode.RawUInt16,
                reserved0: 0,
                latencyBudgetMilliseconds: 100,
                targetFpsTimes100: 6000,
                retryOfFrame: 7,
                tileBaseId: 11,
                cameraBytes: 96,
                tileIndexBytes: 160,
                reserved1: 0,
                reserved2: 0,
                submitMode: SubmitMode.Inline,
                budgetPolicy: BudgetPolicy.AllowStaleReuse,
                lossTolerancePolicy: 0xFF,
                reserved3: 0,
                objectRefMask: 0,
                dependencyFrameId: 5,
                payloadKindBitmap: PayloadKind.Tensor,
                payloadFrameCount: 0,
                reserved4: 0);

            var payload = metadata.ToArray();

            Assert.Equal(FrameSubmitMetadata.MetadataLength, payload.Length);
            Assert.Equal(72, payload.Length);
            Assert.True(FrameSubmitMetadata.TryParse(payload, strict: true, out var parsed, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(metadata, parsed);

            payload[70] = 1;
            Assert.False(FrameSubmitMetadata.TryParse(payload, strict: true, out _, out error));
            Assert.Equal(NnrpParseError.NonZeroReservedField, error);
        }

        [Fact]
        public void CurrentFrameSubmitMetadataStrictParseRejectsInvalidObjectReferenceMaskCombinations()
        {
            var inlineMetadata = new FrameSubmitMetadata(
                sourceWidth: 640,
                sourceHeight: 360,
                tileWidth: 32,
                tileHeight: 32,
                tileCount: 80,
                sectionCount: 3,
                frameClass: FrameClass.Keyframe,
                inputProfile: InputProfile.DenseLumaFrame,
                tileIndexMode: TileIndexMode.RawUInt16,
                reserved0: 0,
                latencyBudgetMilliseconds: 100,
                targetFpsTimes100: 6000,
                retryOfFrame: 7,
                tileBaseId: 11,
                cameraBytes: 96,
                tileIndexBytes: 160,
                reserved1: 0,
                reserved2: 0,
                submitMode: SubmitMode.Inline,
                budgetPolicy: BudgetPolicy.AllowStaleReuse,
                lossTolerancePolicy: 2,
                reserved3: 0,
                objectRefMask: (uint)SubmitObjectSlot.CameraBlock,
                dependencyFrameId: 5,
                payloadKindBitmap: PayloadKind.Tensor,
                payloadFrameCount: 0,
                reserved4: 0);

            Assert.False(FrameSubmitMetadata.TryParse(inlineMetadata.ToArray(), strict: true, out _, out var error));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, error);

            var referenceMetadata = new FrameSubmitMetadata(
                sourceWidth: 640,
                sourceHeight: 360,
                tileWidth: 32,
                tileHeight: 32,
                tileCount: 80,
                sectionCount: 3,
                frameClass: FrameClass.Keyframe,
                inputProfile: InputProfile.DenseLumaFrame,
                tileIndexMode: TileIndexMode.RawUInt16,
                reserved0: 0,
                latencyBudgetMilliseconds: 100,
                targetFpsTimes100: 6000,
                retryOfFrame: 7,
                tileBaseId: 11,
                cameraBytes: 96,
                tileIndexBytes: 160,
                reserved1: 0,
                reserved2: 0,
                submitMode: SubmitMode.Reference,
                budgetPolicy: BudgetPolicy.AllowStaleReuse,
                lossTolerancePolicy: 2,
                reserved3: 0,
                objectRefMask: 0,
                dependencyFrameId: 5,
                payloadKindBitmap: PayloadKind.Tensor,
                payloadFrameCount: 0,
                reserved4: 0);

            Assert.False(FrameSubmitMetadata.TryParse(referenceMetadata.ToArray(), strict: true, out _, out error));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, error);

            var reservedMaskMetadata = new FrameSubmitMetadata(
                sourceWidth: 640,
                sourceHeight: 360,
                tileWidth: 32,
                tileHeight: 32,
                tileCount: 80,
                sectionCount: 3,
                frameClass: FrameClass.Keyframe,
                inputProfile: InputProfile.DenseLumaFrame,
                tileIndexMode: TileIndexMode.RawUInt16,
                reserved0: 0,
                latencyBudgetMilliseconds: 100,
                targetFpsTimes100: 6000,
                retryOfFrame: 7,
                tileBaseId: 11,
                cameraBytes: 96,
                tileIndexBytes: 160,
                reserved1: 0,
                reserved2: 0,
                submitMode: SubmitMode.Mixed,
                budgetPolicy: BudgetPolicy.AllowStaleReuse,
                lossTolerancePolicy: 2,
                reserved3: 0,
                objectRefMask: 0x10,
                dependencyFrameId: 5,
                payloadKindBitmap: PayloadKind.Tensor,
                payloadFrameCount: 0,
                reserved4: 0);

            Assert.False(FrameSubmitMetadata.TryParse(reservedMaskMetadata.ToArray(), strict: true, out _, out error));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, error);
        }

        [Fact]
        public void ResultPushMetadataRoundTripsAndRejectsReservedFields()
        {
            var metadata = new ResultPushMetadata(
                statusCode: ResultStatusCode.Success,
                resultFlags: ResultFlags.Stale | ResultFlags.Partial,
                sectionCount: 1,
                tileCount: 2,
                activeProfileId: 2,
                inferenceMilliseconds: 12,
                queueMilliseconds: 3,
                serverTotalMilliseconds: 18,
                tileBaseId: 5,
                tileIndexBytes: 0,
                resultClass: ResultClass.Partial,
                coveredTileCount: 1,
                droppedTileCount: 1);

            var payload = metadata.ToArray();

            Assert.Equal(ResultPushMetadata.MetadataLength, payload.Length);
            Assert.True(ResultPushMetadata.TryParse(payload, strict: true, out var parsed, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(metadata, parsed);

            payload[7] = 1;
            Assert.False(ResultPushMetadata.TryParse(payload, strict: true, out _, out error));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, error);
        }

        [Fact]
        public void ResultPushMetadataRoundTripsAndRejectsInvalidPayloadCoverage()
        {
            var normalizedCompleteCoverage = new ResultPushMetadata(
                statusCode: ResultStatusCode.Success,
                resultFlags: ResultFlags.None,
                sectionCount: 1,
                tileCount: 2,
                activeProfileId: 7,
                inferenceMilliseconds: 12,
                queueMilliseconds: 3,
                serverTotalMilliseconds: 15,
                tileBaseId: 10,
                tileIndexBytes: 0,
                resultClass: ResultClass.Complete,
                appliedBudgetPolicy: BudgetPolicy.None,
                reusedFrameId: 0,
                coveredTileCount: 0,
                droppedTileCount: 0,
                payloadKindBitmap: PayloadKind.Tensor,
                payloadFrameCount: 0);

            Assert.Equal<ushort>(2, normalizedCompleteCoverage.CoveredTileCount);
            Assert.Equal<ushort>(0, normalizedCompleteCoverage.DroppedTileCount);

            var metadata = new ResultPushMetadata(
                statusCode: ResultStatusCode.Success,
                resultFlags: ResultFlags.Partial,
                sectionCount: 1,
                tileCount: 2,
                activeProfileId: 7,
                inferenceMilliseconds: 12,
                queueMilliseconds: 3,
                serverTotalMilliseconds: 15,
                tileBaseId: 10,
                tileIndexBytes: 0,
                resultClass: ResultClass.Partial,
                appliedBudgetPolicy: BudgetPolicy.AllowPartial | BudgetPolicy.AllowDrop,
                reusedFrameId: 0,
                coveredTileCount: 1,
                droppedTileCount: 1,
                payloadKindBitmap: PayloadKind.Tensor | PayloadKind.StructuredEvent,
                payloadFrameCount: 1);

            var payload = metadata.ToArray();

            Assert.Equal(ResultPushMetadata.CurrentMetadataLength, payload.Length);
            Assert.True(ResultPushMetadata.TryParse(payload, strict: true, out var parsed, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(metadata, parsed);

            payload[56] = 0x80;
            Assert.False(ResultPushMetadata.TryParse(payload, strict: true, out _, out error));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, error);

            var mismatchedCoverage = new ResultPushMetadata(
                statusCode: ResultStatusCode.Success,
                resultFlags: ResultFlags.Partial,
                sectionCount: 1,
                tileCount: 2,
                activeProfileId: 7,
                inferenceMilliseconds: 12,
                queueMilliseconds: 3,
                serverTotalMilliseconds: 15,
                tileBaseId: 10,
                tileIndexBytes: 0,
                resultClass: ResultClass.Partial,
                appliedBudgetPolicy: BudgetPolicy.AllowPartial | BudgetPolicy.AllowDrop,
                reusedFrameId: 0,
                coveredTileCount: 2,
                droppedTileCount: 1,
                payloadKindBitmap: PayloadKind.Tensor | PayloadKind.StructuredEvent,
                payloadFrameCount: 1);

            Assert.False(ResultPushMetadata.TryParse(mismatchedCoverage.ToArray(), strict: true, out _, out error));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, error);

            var staleReuseWithoutFrame = new ResultPushMetadata(
                statusCode: ResultStatusCode.Success,
                resultFlags: ResultFlags.Stale,
                sectionCount: 1,
                tileCount: 2,
                activeProfileId: 7,
                inferenceMilliseconds: 12,
                queueMilliseconds: 3,
                serverTotalMilliseconds: 15,
                tileBaseId: 10,
                tileIndexBytes: 0,
                resultClass: ResultClass.StaleReuse,
                appliedBudgetPolicy: BudgetPolicy.AllowStaleReuse,
                reusedFrameId: 0,
                coveredTileCount: 2,
                droppedTileCount: 0,
                payloadKindBitmap: PayloadKind.Tensor,
                payloadFrameCount: 0);

            Assert.False(ResultPushMetadata.TryParse(staleReuseWithoutFrame.ToArray(), strict: true, out _, out error));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, error);

            var nonTensorMetadata = new ResultPushMetadata(
                statusCode: ResultStatusCode.Success,
                resultFlags: ResultFlags.None,
                sectionCount: 1,
                tileCount: 0,
                activeProfileId: 1,
                inferenceMilliseconds: 1,
                queueMilliseconds: 1,
                serverTotalMilliseconds: 1,
                tileBaseId: 0,
                tileIndexBytes: 0,
                resultClass: ResultClass.Complete,
                appliedBudgetPolicy: BudgetPolicy.None,
                reusedFrameId: 0,
                coveredTileCount: 0,
                droppedTileCount: 0,
                payloadKindBitmap: PayloadKind.ToolDelta,
                payloadFrameCount: 1);

            Assert.False(ResultPushMetadata.TryParse(nonTensorMetadata.ToArray(), strict: true, out _, out error));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, error);
        }

        [Fact]
        public void BodyRegionPreludeRoundTripsAndRejectsReservedFields()
        {
            var prelude = new BodyRegionPrelude(
                inlineObjectBytes: 32,
                objectReferenceBytes: 16,
                typedPayloadDescriptorBytes: 48,
                typedPayloadFrameBytes: 256,
                extensionDescriptorBytes: 0,
                extensionPayloadBytes: 0,
                bodyFlags: 0,
                reserved: 0);

            var payload = prelude.ToArray();

            Assert.Equal(BodyRegionPrelude.PreludeLength, payload.Length);
            Assert.True(BodyRegionPrelude.TryParse(payload, strict: true, out var parsed, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(prelude, parsed);
            Assert.Equal((uint)(32 + 16 + 48 + 256 + BodyRegionPrelude.PreludeLength), prelude.GetTotalBodyBytes());

            payload[31] = 1;
            Assert.False(BodyRegionPrelude.TryParse(payload, strict: true, out _, out error));
            Assert.Equal(NnrpParseError.NonZeroReservedField, error);
        }

        [Fact]
        public void InlineObjectBlockHeaderRoundTripsAndRejectsReservedFields()
        {
            var header = new InlineObjectBlockHeader(
                CacheObjectKind.CameraBlock,
                objectFlags: 0,
                profileId: 7,
                reserved0: 0,
                objectBytes: 18,
                reserved1: 0);

            var payload = header.ToArray();

            Assert.Equal(InlineObjectBlockHeader.HeaderLength, payload.Length);
            Assert.True(InlineObjectBlockHeader.TryParse(payload, strict: true, out var parsed, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(header, parsed);
            Assert.Equal(40U, header.GetAlignedBlockLength());

            payload[2] = 1;
            Assert.False(InlineObjectBlockHeader.TryParse(payload, strict: true, out _, out error));
            Assert.Equal(NnrpParseError.NonZeroReservedField, error);
        }

        [Fact]
        public void ObjectReferenceBlockRoundTripsAndRejectsReferenceFlagsInStrictMode()
        {
            var block = new ObjectReferenceBlock(
                CacheObjectKind.TensorSectionTable,
                referenceFlags: 0,
                cacheNamespace: 3,
                cacheKeyHigh: 11,
                cacheKeyLow: 19);

            var payload = block.ToArray();

            Assert.Equal(ObjectReferenceBlock.BlockLength, payload.Length);
            Assert.True(ObjectReferenceBlock.TryParse(payload, strict: true, out var parsed, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(block, parsed);

            payload[2] = 1;
            Assert.False(ObjectReferenceBlock.TryParse(payload, strict: true, out _, out error));
            Assert.Equal(NnrpParseError.NonZeroReservedField, error);
        }

        [Fact]
        public void BodyCodecRoundTripsExplicitRegions()
        {
            var typedPayloadDescriptor = new TypedPayloadDescriptor(
                PayloadKind.Tensor,
                descriptorFlags: 0,
                profileId: 0,
                payloadOffset: 0,
                payloadLength: 4,
                reserved: 0).ToArray();
            var extensionDescriptor = new ExtensionFrameDescriptor(
                extensionKind: 1,
                extensionFlags: 0,
                profileId: 0,
                reserved0: 0,
                payloadOffset: 0,
                payloadLength: 1).ToArray();
            var body = BodyCodec.Pack(
                inlineObjectRegion: new byte[] { 0x01, 0x02 },
                objectReferenceRegion: new byte[] { 0x03, 0x04, 0x05 },
                typedPayloadDescriptorRegion: typedPayloadDescriptor,
                typedPayloadFrameRegion: new byte[] { 0x07, 0x08, 0x09, 0x0A },
                extensionDescriptorRegion: extensionDescriptor,
                extensionPayloadRegion: new byte[] { 0x0D },
                bodyFlags: 0);

            Assert.True(BodyCodec.TryParse(body, out var view, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(new byte[] { 0x01, 0x02 }, view.InlineObjectRegion.ToArray());
            Assert.Equal(new byte[] { 0x03, 0x04, 0x05 }, view.ObjectReferenceRegion.ToArray());
            Assert.Equal(typedPayloadDescriptor, view.TypedPayloadDescriptorRegion.ToArray());
            Assert.Equal(new byte[] { 0x07, 0x08, 0x09, 0x0A }, view.TypedPayloadFrameRegion.ToArray());
            Assert.Equal(extensionDescriptor, view.ExtensionDescriptorRegion.ToArray());
            Assert.Equal(new byte[] { 0x0D }, view.ExtensionPayloadRegion.ToArray());
        }

        [Fact]
        public void BodyCodecRejectsLengthMismatch()
        {
            var body = BodyCodec.Pack(inlineObjectRegion: new byte[] { 0x01, 0x02 });
            Array.Resize(ref body, body.Length - 1);

            Assert.False(BodyCodec.TryParse(body, out _, out var error));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, error);
        }

        [Fact]
        public void BodyCodecSplitsInlineObjectRegionIntoAlignedBlocks()
        {
            var block0 = BodyCodec.BuildInlineObjectBlock(CacheObjectKind.CameraBlock, new byte[] { 0xAA, 0xBB, 0xCC });
            var block1 = BodyCodec.BuildInlineObjectBlock(CacheObjectKind.TileIndexBlock, new byte[] { 0x10, 0x20 }, profileId: 3);
            var region = BodyCodec.PackInlineObjectRegion(block0, block1);

            Assert.True(BodyCodec.TrySplitInlineObjectRegion(region, out var blocks, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(2, blocks.Length);
            Assert.Equal(CacheObjectKind.CameraBlock, blocks[0].Header.ObjectKind);
            Assert.Equal(new byte[] { 0xAA, 0xBB, 0xCC }, blocks[0].Payload.ToArray());
            Assert.Equal(CacheObjectKind.TileIndexBlock, blocks[1].Header.ObjectKind);
            Assert.Equal((ushort)3, blocks[1].Header.ProfileId);
            Assert.Equal(new byte[] { 0x10, 0x20 }, blocks[1].Payload.ToArray());
        }

        [Fact]
        public void BodyCodecParsesObjectReferenceRegion()
        {
            var block0 = BodyCodec.BuildObjectReferenceBlock(CacheObjectKind.CameraBlock, 7, 11, 13);
            var block1 = BodyCodec.BuildObjectReferenceBlock(CacheObjectKind.TensorSectionTable, 17, 19, 23);
            var region = BodyCodec.PackObjectReferenceRegion(block0, block1);

            Assert.True(BodyCodec.TryParseObjectReferenceRegion(region, out var view, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(2, view.Blocks.Length);
            Assert.Equal(block0, view.Blocks[0]);
            Assert.Equal(block1, view.Blocks[1]);
        }

        [Fact]
        public void SubmitObjectReferenceMaskBuildsStableStandardSlotOrdering()
        {
            var mask = SubmitObjectReferenceMask.Build(
                SubmitObjectSlot.CameraBlock,
                SubmitObjectSlot.TensorSectionTable,
                SubmitObjectSlot.PayloadLayoutTemplate);

            Assert.Equal(0x0000000DU, mask);
            Assert.True(SubmitObjectReferenceMask.Contains(mask, SubmitObjectSlot.CameraBlock));
            Assert.False(SubmitObjectReferenceMask.Contains(mask, SubmitObjectSlot.TileIndexBlock));

            var kinds = SubmitObjectReferenceMask.GetReferencedObjectKinds(mask);
            Assert.Equal(
                new[]
                {
                    CacheObjectKind.CameraBlock,
                    CacheObjectKind.TensorSectionTable,
                    CacheObjectKind.PayloadLayoutTemplate,
                },
                kinds);
        }

        [Fact]
        public void SubmitObjectRegionValidatorAcceptsConsistentInlineAndReferencedSlots()
        {
            var inlineRegion = BodyCodec.PackInlineObjectRegion(
                BodyCodec.BuildInlineObjectBlock(CacheObjectKind.TileIndexBlock, new byte[] { 0x01, 0x02 }),
                BodyCodec.BuildInlineObjectBlock(CacheObjectKind.PayloadLayoutTemplate, new byte[] { 0xAA }));
            var objectReferenceRegion = BodyCodec.PackObjectReferenceRegion(
                BodyCodec.BuildObjectReferenceBlock(CacheObjectKind.CameraBlock, 7, 11, 13),
                BodyCodec.BuildObjectReferenceBlock(CacheObjectKind.TensorSectionTable, 17, 19, 23));
            var mask = SubmitObjectReferenceMask.Build(
                SubmitObjectSlot.CameraBlock,
                SubmitObjectSlot.TensorSectionTable);

            Assert.True(
                SubmitObjectRegionValidator.TryValidate(
                    mask,
                    inlineRegion,
                    objectReferenceRegion,
                    out var result,
                    out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(2, result.InlineBlocks.Length);
            Assert.Equal(2, result.ObjectReferenceBlocks.Length);
        }

        [Fact]
        public void SubmitObjectRegionValidatorRejectsMaskRegionMismatchAndOutOfOrderSlots()
        {
            var inlineRegion = BodyCodec.PackInlineObjectRegion(
                BodyCodec.BuildInlineObjectBlock(CacheObjectKind.CameraBlock, new byte[] { 0x01 }),
                BodyCodec.BuildInlineObjectBlock(CacheObjectKind.TileIndexBlock, new byte[] { 0x02 }));
            var objectReferenceRegion = BodyCodec.PackObjectReferenceRegion(
                BodyCodec.BuildObjectReferenceBlock(CacheObjectKind.TensorSectionTable, 17, 19, 23),
                BodyCodec.BuildObjectReferenceBlock(CacheObjectKind.CameraBlock, 7, 11, 13));
            var mask = SubmitObjectReferenceMask.Build(
                SubmitObjectSlot.CameraBlock,
                SubmitObjectSlot.TensorSectionTable);

            Assert.False(
                SubmitObjectRegionValidator.TryValidate(
                    mask,
                    inlineRegion,
                    objectReferenceRegion,
                    out _,
                    out var error));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, error);

            var duplicateReferenceRegion = BodyCodec.PackObjectReferenceRegion(
                BodyCodec.BuildObjectReferenceBlock(CacheObjectKind.CameraBlock, 1, 2, 3),
                BodyCodec.BuildObjectReferenceBlock(CacheObjectKind.CameraBlock, 4, 5, 6));

            Assert.False(
                SubmitObjectRegionValidator.TryValidate(
                    (uint)SubmitObjectSlot.CameraBlock,
                    inlineObjectRegion: ReadOnlyMemory<byte>.Empty,
                    objectReferenceRegion: duplicateReferenceRegion,
                    out _,
                    out error));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, error);
        }

        [Fact]
        public void BodyCodecRoundTripsMixedSubmitBody()
        {
            var inlineRegion = BodyCodec.PackInlineObjectRegion(
                BodyCodec.BuildInlineObjectBlock(CacheObjectKind.TileIndexBlock, new byte[] { 0x01, 0x02 }),
                BodyCodec.BuildInlineObjectBlock(CacheObjectKind.PayloadLayoutTemplate, new byte[] { 0xAA, 0xBB, 0xCC }));
            var objectReferenceRegion = BodyCodec.PackObjectReferenceRegion(
                BodyCodec.BuildObjectReferenceBlock(CacheObjectKind.CameraBlock, 7, 11, 13),
                BodyCodec.BuildObjectReferenceBlock(CacheObjectKind.TensorSectionTable, 17, 19, 23));
            var body = BodyCodec.Pack(
                inlineObjectRegion: inlineRegion,
                objectReferenceRegion: objectReferenceRegion);
            var objectRefMask = SubmitObjectReferenceMask.Build(
                SubmitObjectSlot.CameraBlock,
                SubmitObjectSlot.TensorSectionTable);

            Assert.True(BodyCodec.TryParse(body, out var bodyView, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(inlineRegion, bodyView.InlineObjectRegion.ToArray());
            Assert.Equal(objectReferenceRegion, bodyView.ObjectReferenceRegion.ToArray());
            Assert.Empty(bodyView.TypedPayloadDescriptorRegion.ToArray());
            Assert.Empty(bodyView.TypedPayloadFrameRegion.ToArray());

            Assert.True(
                SubmitObjectRegionValidator.TryValidate(
                    objectRefMask,
                    bodyView.InlineObjectRegion,
                    bodyView.ObjectReferenceRegion,
                    out var validation,
                    out error));
            Assert.Equal(NnrpParseError.None, error);

            Assert.Equal(2, validation.InlineBlocks.Length);
            Assert.Equal(CacheObjectKind.TileIndexBlock, validation.InlineBlocks[0].Header.ObjectKind);
            Assert.Equal(new byte[] { 0x01, 0x02 }, validation.InlineBlocks[0].Payload.ToArray());
            Assert.Equal(CacheObjectKind.PayloadLayoutTemplate, validation.InlineBlocks[1].Header.ObjectKind);
            Assert.Equal(new byte[] { 0xAA, 0xBB, 0xCC }, validation.InlineBlocks[1].Payload.ToArray());

            Assert.Equal(2, validation.ObjectReferenceBlocks.Length);
            Assert.Equal(CacheObjectKind.CameraBlock, validation.ObjectReferenceBlocks[0].ObjectKind);
            Assert.Equal((uint)7, validation.ObjectReferenceBlocks[0].CacheNamespace);
            Assert.Equal((ulong)11, validation.ObjectReferenceBlocks[0].CacheKeyHigh);
            Assert.Equal((ulong)13, validation.ObjectReferenceBlocks[0].CacheKeyLow);
            Assert.Equal(CacheObjectKind.TensorSectionTable, validation.ObjectReferenceBlocks[1].ObjectKind);
            Assert.Equal((uint)17, validation.ObjectReferenceBlocks[1].CacheNamespace);
            Assert.Equal((ulong)19, validation.ObjectReferenceBlocks[1].CacheKeyHigh);
            Assert.Equal((ulong)23, validation.ObjectReferenceBlocks[1].CacheKeyLow);
        }

        [Fact]
        public void TypedPayloadDescriptorRoundTripsAndRejectsReservedFields()
        {
            var descriptor = new TypedPayloadDescriptor(
                PayloadKind.TokenChunk,
                TypedPayloadDescriptor.ProfileToken,
                descriptorFlags: 0x0002,
                schemaId: TypedPayloadDescriptor.TokenDeltaSchemaId,
                schemaVersion: TypedPayloadDescriptor.TokenDeltaSchemaVersion,
                streamSemantics: TypedPayloadDescriptor.StreamSemanticsAppend,
                payloadOffset: 32,
                payloadLength: 96);

            var payload = descriptor.ToArray();

            Assert.Equal(TypedPayloadDescriptor.DescriptorLength, payload.Length);
            Assert.True(TypedPayloadDescriptor.TryParse(payload, strict: true, out var parsed, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(descriptor, parsed);

            payload[3] = 0x10;
            Assert.False(TypedPayloadDescriptor.TryParse(payload, strict: true, out _, out error));
            Assert.Equal(NnrpParseError.NonZeroReservedField, error);
        }

        [Fact]
        public void TypedPayloadDescriptorCoversProfileAndFlagEdgeBranches()
        {
            var invalidFlags = new TypedPayloadDescriptor(
                PayloadKind.TokenChunk,
                TypedPayloadDescriptor.ProfileToken,
                descriptorFlags: 0x0010,
                schemaId: TypedPayloadDescriptor.TokenDeltaSchemaId,
                schemaVersion: TypedPayloadDescriptor.TokenDeltaSchemaVersion,
                streamSemantics: TypedPayloadDescriptor.StreamSemanticsAppend,
                payloadOffset: 0,
                payloadLength: 1);
            Assert.False(invalidFlags.TryWrite(new byte[TypedPayloadDescriptor.DescriptorLength], out _));
            Assert.Equal(0u, invalidFlags.Reserved);

            var invalidReserved = new TypedPayloadDescriptor(
                PayloadKind.TokenChunk,
                TypedPayloadDescriptor.ProfileToken,
                descriptorFlags: 0,
                schemaId: TypedPayloadDescriptor.TokenDeltaSchemaId,
                schemaVersion: TypedPayloadDescriptor.TokenDeltaSchemaVersion,
                streamSemantics: TypedPayloadDescriptor.StreamSemanticsAppend,
                payloadOffset: 0,
                payloadLength: 1,
                reserved0: 1);
            Assert.False(invalidReserved.TryWrite(new byte[TypedPayloadDescriptor.DescriptorLength], out _));

            var extensionProfile = new TypedPayloadDescriptor(
                PayloadKind.OpaqueBytes,
                profileId: 99,
                descriptorFlags: 0,
                schemaId: 0x2000,
                schemaVersion: 1,
                streamSemantics: TypedPayloadDescriptor.StreamSemanticsSnapshot,
                payloadOffset: 0,
                payloadLength: 0);
            Assert.True(TypedPayloadDescriptor.TryParse(extensionProfile.ToArray(), strict: true, out var parsed, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(PayloadKind.None, parsed.PayloadKind);
        }

        [Fact]
        public void ExtensionFrameDescriptorRoundTripsAndRejectsUnknownFlagBits()
        {
            var descriptor = new ExtensionFrameDescriptor(
                extensionKind: 5,
                extensionFlags: 0x0001,
                profileId: 2,
                reserved0: 0,
                payloadOffset: 64,
                payloadLength: 12);

            var payload = descriptor.ToArray();

            Assert.Equal(ExtensionFrameDescriptor.DescriptorLength, payload.Length);
            Assert.True(ExtensionFrameDescriptor.TryParse(payload, strict: true, out var parsed, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(descriptor, parsed);

            payload[2] = 0x02;
            Assert.False(ExtensionFrameDescriptor.TryParse(payload, strict: true, out _, out error));
            Assert.Equal(NnrpParseError.NonZeroReservedField, error);
        }

        [Fact]
        public void BodyCodecRejectsMisalignedDescriptorRegions()
        {
            var body = BodyCodec.Pack(
                typedPayloadDescriptorRegion: new byte[] { 0x01 },
                extensionDescriptorRegion: new byte[ExtensionFrameDescriptor.DescriptorLength]);

            Assert.False(BodyCodec.TryParse(body, out _, out var error));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, error);
        }

        [Fact]
        public void TypedPayloadRegionValidatorAcceptsOrderedDeclaredFrames()
        {
            var descriptor0 = new TypedPayloadDescriptor(
                PayloadKind.TokenChunk,
                TypedPayloadDescriptor.ProfileToken,
                descriptorFlags: 0x0002,
                schemaId: TypedPayloadDescriptor.TokenDeltaSchemaId,
                schemaVersion: TypedPayloadDescriptor.TokenDeltaSchemaVersion,
                streamSemantics: TypedPayloadDescriptor.StreamSemanticsAppend,
                payloadOffset: 0,
                payloadLength: 4);
            var descriptor1 = new TypedPayloadDescriptor(
                PayloadKind.TokenChunk,
                TypedPayloadDescriptor.ProfileToken,
                descriptorFlags: 0x0001,
                schemaId: TypedPayloadDescriptor.TokenDeltaSchemaId,
                schemaVersion: TypedPayloadDescriptor.TokenDeltaSchemaVersion,
                streamSemantics: TypedPayloadDescriptor.StreamSemanticsAppend,
                payloadOffset: 4,
                payloadLength: 2);
            var descriptorRegion = new byte[TypedPayloadDescriptor.DescriptorLength * 2];
            descriptor0.Write(descriptorRegion.AsSpan(0, TypedPayloadDescriptor.DescriptorLength));
            descriptor1.Write(descriptorRegion.AsSpan(TypedPayloadDescriptor.DescriptorLength, TypedPayloadDescriptor.DescriptorLength));
            var payloadRegion = new byte[] { 0x10, 0x20, 0x30, 0x40, 0xAA, 0xBB };

            Assert.True(
                TypedPayloadRegionValidator.TryValidateTypedPayloadRegion(
                    PayloadKind.TokenChunk,
                    payloadFrameCount: 2,
                    descriptorRegion,
                    payloadRegion,
                    out var descriptors,
                    out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(2, descriptors.Length);
            Assert.Equal(descriptor0, descriptors[0]);
            Assert.Equal(descriptor1, descriptors[1]);

            Assert.True(TypedPayloadRegionValidator.TrySummarizeProfileCoverage(descriptors, out var coverages, out var payloadKindBitmap, out error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(PayloadKind.TokenChunk, payloadKindBitmap);
            Assert.Single(coverages);
            Assert.Equal(new TypedPayloadProfileCoverage(PayloadKind.TokenChunk, TypedPayloadDescriptor.ProfileToken, 2, 6), coverages[0]);
        }

        [Fact]
        public void TypedPayloadRegionValidatorRejectsProfileMismatchesAfterParsingAndInMemory()
        {
            var tensorProfileDescriptor = new TypedPayloadDescriptor(
                PayloadKind.Tensor,
                TypedPayloadDescriptor.ProfileTensor,
                descriptorFlags: 0,
                schemaId: 0x2000,
                schemaVersion: 1,
                streamSemantics: TypedPayloadDescriptor.StreamSemanticsSnapshot,
                payloadOffset: 0,
                payloadLength: 0);

            Assert.False(
                TypedPayloadRegionValidator.TryValidateTypedPayloadRegion(
                    PayloadKind.Tensor | PayloadKind.TokenChunk,
                    payloadFrameCount: 1,
                    tensorProfileDescriptor.ToArray(),
                    ReadOnlyMemory<byte>.Empty,
                    out _,
                    out var error));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, error);

            Assert.False(
                TypedPayloadRegionValidator.TryValidateTypedPayloadDescriptors(
                    PayloadKind.Tensor | PayloadKind.TokenChunk,
                    payloadFrameCount: 1,
                    new[] { tensorProfileDescriptor },
                    ReadOnlyMemory<byte>.Empty,
                    out error));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, error);

            var tokenPayloadWrongProfile = new TypedPayloadDescriptor(
                PayloadKind.TokenChunk,
                profileId: 99,
                descriptorFlags: 0,
                schemaId: TypedPayloadDescriptor.TokenDeltaSchemaId,
                schemaVersion: TypedPayloadDescriptor.TokenDeltaSchemaVersion,
                streamSemantics: TypedPayloadDescriptor.StreamSemanticsAppend,
                payloadOffset: 0,
                payloadLength: 0);
            Assert.False(
                TypedPayloadRegionValidator.TryValidateTypedPayloadDescriptors(
                    PayloadKind.TokenChunk,
                    payloadFrameCount: 1,
                    new[] { tokenPayloadWrongProfile },
                    ReadOnlyMemory<byte>.Empty,
                    out error));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, error);

            Assert.False(
                TypedPayloadRegionValidator.TryValidateTypedPayloadRegion(
                    PayloadKind.Tensor | PayloadKind.TokenChunk,
                    payloadFrameCount: 1,
                    tokenPayloadWrongProfile.ToArray(),
                    ReadOnlyMemory<byte>.Empty,
                    out _,
                    out error));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, error);

            var unspecifiedWithSchema = new TypedPayloadDescriptor(
                PayloadKind.None,
                TypedPayloadDescriptor.ProfileUnspecified,
                descriptorFlags: 0,
                schemaId: TypedPayloadDescriptor.TokenDeltaSchemaId,
                schemaVersion: TypedPayloadDescriptor.TokenDeltaSchemaVersion,
                streamSemantics: TypedPayloadDescriptor.StreamSemanticsAppend,
                payloadOffset: 0,
                payloadLength: 0);
            Assert.False(
                TypedPayloadRegionValidator.TryValidateTypedPayloadDescriptors(
                    PayloadKind.ToolDelta,
                    payloadFrameCount: 1,
                    new[] { unspecifiedWithSchema },
                    ReadOnlyMemory<byte>.Empty,
                    out error));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, error);
        }

        [Fact]
        public void TypedPayloadRegionValidatorResolvesSinglePayloadKindForExtensionProfile()
        {
            var descriptor = new TypedPayloadDescriptor(
                PayloadKind.OpaqueBytes,
                profileId: 99,
                descriptorFlags: 0,
                schemaId: 0x2000,
                schemaVersion: 1,
                streamSemantics: TypedPayloadDescriptor.StreamSemanticsSnapshot,
                payloadOffset: 0,
                payloadLength: 1);

            Assert.True(
                TypedPayloadRegionValidator.TryValidateTypedPayloadRegion(
                    PayloadKind.ToolDelta,
                    payloadFrameCount: 1,
                    descriptor.ToArray(),
                    new byte[] { 0x7F },
                    out var descriptors,
                    out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(PayloadKind.ToolDelta, descriptors[0].PayloadKind);
        }

        [Fact]
        public void TypedPayloadRegionValidatorRejectsCountBitmapAndRangeViolations()
        {
            var descriptor = new TypedPayloadDescriptor(
                PayloadKind.Tensor,
                descriptorFlags: 0,
                profileId: 1,
                payloadOffset: 1,
                payloadLength: 4,
                reserved: 0);
            var descriptorRegion = descriptor.ToArray();
            var payloadRegion = new byte[] { 0x10, 0x20, 0x30, 0x40 };

            Assert.False(
                TypedPayloadRegionValidator.TryValidateTypedPayloadRegion(
                    PayloadKind.Tensor,
                    payloadFrameCount: 2,
                    descriptorRegion,
                    payloadRegion,
                    out _,
                    out var error));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, error);

            Assert.False(
                TypedPayloadRegionValidator.TryValidateTypedPayloadRegion(
                    PayloadKind.ToolDelta,
                    payloadFrameCount: 1,
                    descriptorRegion,
                    payloadRegion,
                    out _,
                    out error));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, error);

            Assert.False(
                TypedPayloadRegionValidator.TryValidateTypedPayloadRegion(
                    PayloadKind.Tensor,
                    payloadFrameCount: 1,
                    descriptorRegion,
                    payloadRegion,
                    out _,
                    out error));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, error);

            descriptor = new TypedPayloadDescriptor(
                PayloadKind.ToolDelta,
                descriptorFlags: 0,
                profileId: 1,
                payloadOffset: 0,
                payloadLength: 4,
                reserved: 0);

            Assert.False(
                TypedPayloadRegionValidator.TryValidateTypedPayloadRegion(
                    PayloadKind.ToolDelta | PayloadKind.StructuredEvent,
                    payloadFrameCount: 1,
                    descriptor.ToArray(),
                    payloadRegion,
                    out _,
                    out error));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, error);
        }

        [Fact]
        public void TypedPayloadRegionValidatorAggregatesFramesPerPayloadKindAndProfile()
        {
            var descriptors = new[]
            {
                new TypedPayloadDescriptor(
                    PayloadKind.ToolDelta,
                    descriptorFlags: 0,
                    profileId: 9,
                    payloadOffset: 0,
                    payloadLength: 2,
                    reserved: 0),
                new TypedPayloadDescriptor(
                    PayloadKind.ToolDelta,
                    descriptorFlags: 0,
                    profileId: 9,
                    payloadOffset: 2,
                    payloadLength: 3,
                    reserved: 0),
                new TypedPayloadDescriptor(
                    PayloadKind.StructuredEvent,
                    descriptorFlags: 0,
                    profileId: 5,
                    payloadOffset: 5,
                    payloadLength: 4,
                    reserved: 0)
            };

            Assert.True(TypedPayloadRegionValidator.TrySummarizeProfileCoverage(descriptors, out var coverages, out var payloadKindBitmap, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(PayloadKind.ToolDelta | PayloadKind.StructuredEvent, payloadKindBitmap);
            Assert.Equal(2, coverages.Length);
            Assert.Equal(new TypedPayloadProfileCoverage(PayloadKind.ToolDelta, 9, 2, 5), coverages[0]);
            Assert.Equal(new TypedPayloadProfileCoverage(PayloadKind.StructuredEvent, 5, 1, 4), coverages[1]);
        }

        [Fact]
        public void TypedPayloadRegionValidatorProjectsFrameViews()
        {
            var descriptors = new[]
            {
                new TypedPayloadDescriptor(
                    PayloadKind.ToolDelta,
                    descriptorFlags: 0,
                    profileId: 9,
                    payloadOffset: 0,
                    payloadLength: 2,
                    reserved: 0),
                new TypedPayloadDescriptor(
                    PayloadKind.StructuredEvent,
                    descriptorFlags: 0,
                    profileId: 5,
                    payloadOffset: 2,
                    payloadLength: 4,
                    reserved: 0)
            };
            var payloadRegion = new byte[] { 0x41, 0x42, 0x90, 0x91, 0x92, 0x93 };

            Assert.True(
                TypedPayloadRegionValidator.TryProjectTypedPayloadFrames(
                    PayloadKind.ToolDelta | PayloadKind.StructuredEvent,
                    payloadFrameCount: 2,
                    descriptors,
                    payloadRegion,
                    out var frames,
                    out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(2, frames.Length);
            Assert.Equal(descriptors[0], frames[0].Descriptor);
            Assert.Equal(new byte[] { 0x41, 0x42 }, frames[0].Payload.ToArray());
            Assert.Equal(descriptors[1], frames[1].Descriptor);
            Assert.Equal(new byte[] { 0x90, 0x91, 0x92, 0x93 }, frames[1].Payload.ToArray());
        }

        [Fact]
        public void TypedPayloadRegionValidatorRejectsInvalidProjectedFrameLayout()
        {
            var descriptors = new[]
            {
                new TypedPayloadDescriptor(
                    PayloadKind.ToolDelta,
                    descriptorFlags: 0,
                    profileId: 9,
                    payloadOffset: 0,
                    payloadLength: 2,
                    reserved: 0)
            };
            var payloadRegion = new byte[] { 0x41, 0x42, 0x43 };

            Assert.False(
                TypedPayloadRegionValidator.TryProjectTypedPayloadFrames(
                    PayloadKind.ToolDelta,
                    payloadFrameCount: 1,
                    descriptors,
                    payloadRegion,
                    out _,
                    out var error));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, error);
        }

        [Fact]
        public void TypedPayloadRegionValidatorRejectsTrailingPayloadBytes()
        {
            var descriptor = new TypedPayloadDescriptor(
                PayloadKind.Tensor,
                descriptorFlags: 0,
                profileId: 1,
                payloadOffset: 0,
                payloadLength: 2,
                reserved: 0);
            var descriptorRegion = descriptor.ToArray();
            var payloadRegion = new byte[] { 0x10, 0x20, 0x30 };

            Assert.False(
                TypedPayloadRegionValidator.TryValidateTypedPayloadRegion(
                    PayloadKind.Tensor,
                    payloadFrameCount: 1,
                    descriptorRegion,
                    payloadRegion,
                    out _,
                    out var error));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, error);
        }

        [Fact]
        public void TypedPayloadRegionValidatorAcceptsAndRejectsExtensionRanges()
        {
            var descriptor0 = new ExtensionFrameDescriptor(
                extensionKind: 1,
                extensionFlags: 0,
                profileId: 0,
                reserved0: 0,
                payloadOffset: 0,
                payloadLength: 2);
            var descriptor1 = new ExtensionFrameDescriptor(
                extensionKind: 2,
                extensionFlags: 0x0001,
                profileId: 0,
                reserved0: 0,
                payloadOffset: 2,
                payloadLength: 1);
            var descriptorRegion = new byte[ExtensionFrameDescriptor.DescriptorLength * 2];
            descriptor0.Write(descriptorRegion.AsSpan(0, ExtensionFrameDescriptor.DescriptorLength));
            descriptor1.Write(descriptorRegion.AsSpan(ExtensionFrameDescriptor.DescriptorLength, ExtensionFrameDescriptor.DescriptorLength));
            var payloadRegion = new byte[] { 0x01, 0x02, 0x03 };

            Assert.True(
                TypedPayloadRegionValidator.TryValidateExtensionDescriptorRegion(
                    descriptorRegion,
                    payloadRegion,
                    out var descriptors,
                    out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(2, descriptors.Length);

            var invalidDescriptor = new ExtensionFrameDescriptor(
                extensionKind: 3,
                extensionFlags: 0,
                profileId: 0,
                reserved0: 0,
                payloadOffset: 1,
                payloadLength: 4).ToArray();

            Assert.False(
                TypedPayloadRegionValidator.TryValidateExtensionDescriptorRegion(
                    invalidDescriptor,
                    payloadRegion,
                    out _,
                    out error));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, error);

            Assert.False(
                TypedPayloadRegionValidator.TryValidateExtensionDescriptorRegion(
                    Array.Empty<byte>(),
                    payloadRegion,
                    out _,
                    out error));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, error);

            Assert.False(
                TypedPayloadRegionValidator.TryValidateExtensionDescriptorRegion(
                    descriptor0.ToArray(),
                    new byte[] { 0x01, 0x02, 0x03 },
                    out _,
                    out error));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, error);
        }

        [Fact]
        public void ExtensionFrameRegionValidatorSkipsUnknownNonCriticalFramesAndRejectsCriticalOnes()
        {
            var nonCriticalDescriptor = new ExtensionFrameDescriptor(
                extensionKind: 99,
                extensionFlags: 0,
                profileId: 0,
                reserved0: 0,
                payloadOffset: 0,
                payloadLength: 2);
            var descriptorRegion = nonCriticalDescriptor.ToArray();
            var payloadRegion = new byte[] { 0x01, 0x02 };

            Assert.True(
                ExtensionFrameRegionValidator.TryValidateAndCollectSkippableFrames(
                    descriptorRegion,
                    payloadRegion,
                    out var descriptors,
                    out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Single(descriptors);
            Assert.Equal(nonCriticalDescriptor, descriptors[0]);

            var criticalDescriptor = new ExtensionFrameDescriptor(
                extensionKind: 100,
                extensionFlags: 0x0001,
                profileId: 0,
                reserved0: 0,
                payloadOffset: 0,
                payloadLength: 2).ToArray();

            Assert.False(
                ExtensionFrameRegionValidator.TryValidateAndCollectSkippableFrames(
                    criticalDescriptor,
                    payloadRegion,
                    out _,
                    out error));
            Assert.Equal(NnrpParseError.UnsupportedExtension, error);
        }

        [Fact]
        public void SessionPatchMetadataFamiliesPreserveCurrentWireWidths()
        {
            var patchMetadata = new SessionPatchMetadata(
                profileId: 29,
                SessionPatchField.TargetCadence | SessionPatchField.DegradePolicy | SessionPatchField.ActiveLaneMask | SessionPatchField.PreferredCodec | SessionPatchField.ProfilePatch,
                9000,
                360,
                5,
                0x0000000000000003UL,
                5,
                0,
                TensorProfilePatchBlock.BlockLength);
            var patchPayload = patchMetadata.ToArray();

            Assert.Equal(36, SessionPatchMetadata.MetadataLength);
            Assert.Equal(SessionPatchMetadata.MetadataLength, patchPayload.Length);
            Assert.True(SessionPatchMetadata.TryParse(patchPayload, strict: true, out var parsedPatch, out var patchError));
            Assert.Equal(NnrpParseError.None, patchError);
            Assert.Equal(patchMetadata, parsedPatch);

            var patchAckMetadata = new SessionPatchAckMetadata(
                SessionPatchAckStatus.PartiallyApplied,
                SessionPatchRejectReason.UnsupportedStrategy,
                SessionPatchField.TargetCadence,
                SessionPatchField.ProfilePatch,
                0,
                2,
                9000,
                360,
                5,
                0x0000000000000003UL,
                5,
                0,
                TensorProfilePatchAckBlock.BlockLength);
            var patchAckPayload = patchAckMetadata.ToArray();

            Assert.Equal(48, SessionPatchAckMetadata.MetadataLength);
            Assert.Equal(SessionPatchAckMetadata.MetadataLength, patchAckPayload.Length);
            Assert.True(SessionPatchAckMetadata.TryParse(patchAckPayload, out var parsedPatchAck, out var patchAckError));
            Assert.Equal(NnrpParseError.None, patchAckError);
            Assert.Equal(patchAckMetadata, parsedPatchAck);
        }

        [Fact]
        public void TensorSectionDescriptorRoundTripsAndValidatesPayloadLayout()
        {
            var descriptor = new TensorSectionDescriptor(
                role: TensorRole.LumaHint,
                codec: CodecId.Raw,
                dtype: DTypeId.UInt8,
                layout: TensorLayoutId.Nhwc,
                scalePolicy: ScalePolicy.None,
                flags: 0,
                elementCountPerTile: 1024,
                codecTableBytes: 0,
                lengthTableBytes: 0,
                payloadBytes: 4096,
                payloadStrideBytes: 1024);

            var payload = descriptor.ToArray();

            Assert.Equal(TensorSectionDescriptor.DescriptorLength, payload.Length);
            Assert.True(TensorSectionDescriptor.TryParse(payload, strict: true, out var parsed, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(descriptor, parsed);

            var inconsistent = new TensorSectionDescriptor(
                TensorRole.LumaHint,
                CodecId.Raw,
                DTypeId.UInt8,
                TensorLayoutId.Nhwc,
                ScalePolicy.None,
                0,
                1024,
                0,
                16,
                4096,
                1024).ToArray();

            Assert.False(TensorSectionDescriptor.TryParse(inconsistent, strict: true, out _, out error));
            Assert.Equal(NnrpParseError.InconsistentSectionDescriptor, error);
        }
    }
}
