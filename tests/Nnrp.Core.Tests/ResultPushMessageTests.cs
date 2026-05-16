using System;
using Nnrp.Core;
using Xunit;

namespace Nnrp.Core.Tests
{
    public sealed class ResultPushMessageTests
    {
        private const string PythonCurrentResultPushPacketHex = "4e4e5250010012280000000040000000a0000000c9b8fa272f0100000000000000000000000000000000000002000300000000000a0000000a00000000000000060000000000000000000000000000000000000000000000000000000300000001000000000000000000010002000000640000050000000000000000000000000c00000018000000000000000000000008000000080000000800000074696c653a302c3074696c653a312c3074696c653a322c3000000000650000050000000000000000000000000c0000002400000000000000000000000c0000000c0000000c0000006465636f6465643a313032346465636f6465643a313032346465636f6465643a31303234";

        [Fact]
        public void ResultPushMessageParsesPythonGoldenPacketAndRoundTrips()
        {
            // Recreate the same logical layout as the original Python golden
            // packet: 2 tiles via RawUInt16, 2 sections, Stale|Partial flags.
            // Sections must have strictly increasing roles and per-tile length tables.
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

            // Aligned body: tensor_result_block→tiles→pad→s0→pad→s1
            var bodyLength = BinaryAlignment.AlignUp(TensorResultBlock.BlockLength + tileIndexBytes, 8)
                + section0.TotalLength;
            bodyLength = BinaryAlignment.AlignUp(bodyLength, 8)
                + section1.TotalLength;

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

            var original = new ResultPushMessage(header, metadata, tileIds, new[] { section0, section1 });
            var packet = original.ToArray();

            Assert.True(ResultPushMessage.TryParse(packet, out var message, out var error), $"Parse error: {error}");
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(MessageType.ResultPush, message.Header.MessageType);
            Assert.Equal(TileIndexMode.RawUInt16, message.TileIndexMode);
            Assert.Equal(2, message.TileIds.Length);
            Assert.Equal(2, message.Sections.Length);
            Assert.Equal(ResultFlags.Stale | ResultFlags.Partial, message.Metadata.ResultFlags);
            Assert.Equal(packet, message.ToArray());
        }

        [Fact]
        public void ResultPushMessageParsesPythonGoldenPacket()
        {
            var packet = Convert.FromHexString(PythonCurrentResultPushPacketHex);

            Assert.True(
                NnrpFramedMessage.TryParse(packet, NnrpHeaderParseOptions.Strict, out var framed, out var framedError),
                $"Framed parse error: {framedError}");
            Assert.Equal(NnrpParseError.None, framedError);
            Assert.Equal(NnrpHeader.CurrentWireFormat, framed.Header.WireFormat);
            Assert.Equal<uint>((uint)ResultPushMetadata.CurrentMetadataLength, framed.Header.MetaLength);
            Assert.Equal<uint>(160U, framed.Header.BodyLength);

            Assert.True(
                ResultPushMetadata.TryParse(framed.Metadata.Span, strict: true, out var metadata, out var metadataError),
                $"Metadata parse error: {metadataError}");
            Assert.Equal(NnrpParseError.None, metadataError);
            Assert.Equal((ushort)2, metadata.SectionCount);
            Assert.Equal((ushort)3, metadata.TileCount);
            Assert.Equal<uint>(6U, metadata.TileIndexBytes);
            Assert.Equal(ResultClass.Complete, metadata.ResultClass);
            Assert.Equal(PayloadKind.Tensor, metadata.PayloadKindBitmap);

            var tileIds = new ushort[metadata.TileCount];
            var tileIndexBlock = framed.Body.Slice(0, (int)metadata.TileIndexBytes);
            Assert.True(
                TileIndexBlockCodec.TryDecode(
                    tileIndexBlock.Span,
                    TileIndexMode.RawUInt16,
                    metadata.TileCount,
                    tileIds,
                    out var tileIdsWritten,
                    out var tileIndexError,
                    metadata.TileBaseId),
                $"Tile index decode error: {tileIndexError}");
            Assert.Equal(NnrpParseError.None, tileIndexError);
            Assert.Equal(3, tileIdsWritten);
            Assert.Equal(new ushort[] { 0, 1, 2 }, tileIds);

            var firstSectionOffset = BinaryAlignment.AlignUp((int)metadata.TileIndexBytes, 8);
            Assert.Equal(new byte[] { 0, 0 }, framed.Body.Slice((int)metadata.TileIndexBytes, firstSectionOffset - (int)metadata.TileIndexBytes).ToArray());
            Assert.True(
                TensorSectionBlock.TryParse(
                    framed.Body.Slice(firstSectionOffset),
                    metadata.TileCount,
                    out var section0,
                    out var section0Bytes,
                    out var section0Error),
                $"Section 0 parse error: {section0Error}");
            Assert.Equal(NnrpParseError.None, section0Error);
            Assert.Equal((TensorRole)100, section0.Descriptor.Role);
            Assert.Equal(68, section0Bytes);

            var secondSectionPaddingOffset = firstSectionOffset + section0Bytes;
            var secondSectionOffset = BinaryAlignment.AlignUp(secondSectionPaddingOffset, 8);
            Assert.Equal(new byte[] { 0, 0, 0, 0 }, framed.Body.Slice(secondSectionPaddingOffset, secondSectionOffset - secondSectionPaddingOffset).ToArray());
            Assert.True(
                TensorSectionBlock.TryParse(
                    framed.Body.Slice(secondSectionOffset),
                    metadata.TileCount,
                    out var section1,
                    out var section1Bytes,
                    out var section1Error),
                $"Section 1 parse error: {section1Error}");
            Assert.Equal(NnrpParseError.None, section1Error);
            Assert.Equal((TensorRole)101, section1.Descriptor.Role);
            Assert.Equal(80, section1Bytes);
            Assert.Equal(framed.Body.Length, secondSectionOffset + section1Bytes);

            Assert.True(ResultPushMessage.TryParse(packet, out var message, out var error), $"Parse error: {error}");
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(TileIndexMode.RawUInt16, message.TileIndexMode);
            Assert.Equal(new ushort[] { 0, 1, 2 }, message.TileIds.ToArray());
            Assert.Equal((TensorRole)100, message.Sections.Span[0].Descriptor.Role);
            Assert.Equal((TensorRole)101, message.Sections.Span[1].Descriptor.Role);
            Assert.Equal(packet, message.ToArray());
        }

        [Fact]
        public void ResultPushMessageRoundTripsFreshDenseRangePayload()
        {
            var section = CreateSection(
                role: TensorRole.SrResidual,
                lengthTable: new byte[] { 1, 0, 0, 0, 1, 0, 0, 0 },
                payload: new byte[] { 0x10, 0x20 });
            var metadata = new ResultPushMetadata(
                statusCode: ResultStatusCode.Success,
                resultFlags: ResultFlags.None,
                sectionCount: 1,
                tileCount: 2,
                activeProfileId: 3,
                inferenceMilliseconds: 8,
                queueMilliseconds: 1,
                serverTotalMilliseconds: 10,
                tileBaseId: 5,
                tileIndexBytes: 0);
            // DenseRange with tileCount=2, tileBaseId=5 → tileIds [5,6]
            // Body: tensor_result_block + section.
            var bodyLength = TensorResultBlock.BlockLength + section.TotalLength;
            var header = new NnrpHeader(
                versionMajor: NnrpHeader.CurrentVersionMajor,
                wireFormat: NnrpHeader.CurrentWireFormat,
                messageType: MessageType.ResultPush,
                flags: HeaderFlags.None,
                metaLength: ResultPushMetadata.MetadataLength,
                bodyLength: (uint)bodyLength,
                sessionId: 2,
                frameId: 3,
                viewId: 0,
                routeId: 0,
                traceId: 4);
            var original = new ResultPushMessage(header, metadata, new ushort[] { 5, 6 }, new[] { section });

            var packet = original.ToArray();

            Assert.True(ResultPushMessage.TryParse(packet, out var parsed, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(TileIndexMode.DenseRange, parsed.TileIndexMode);
            Assert.Equal(new ushort[] { 5, 6 }, parsed.TileIds.ToArray());
            Assert.Equal(packet, parsed.ToArray());
        }

        [Fact]
        public void ResultPushMessageRoundTripsMetadataPayload()
        {
            var section = CreateSection(
                role: TensorRole.SrResidual,
                lengthTable: new byte[] { 2, 0, 0, 0, 2, 0, 0, 0 },
                payload: new byte[] { 0x10, 0x11, 0x20, 0x21 });
            var metadata = new ResultPushMetadata(
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
                appliedBudgetPolicy: BudgetPolicy.AllowPartial | BudgetPolicy.AllowDrop,
                reusedFrameId: 0,
                coveredTileCount: 2,
                droppedTileCount: 0,
                payloadKindBitmap: PayloadKind.Tensor,
                payloadFrameCount: 0);
            var bodyLengthBytes = section.TotalLength;
            var header = new NnrpHeader(
                versionMajor: NnrpHeader.CurrentVersionMajor,
                wireFormat: NnrpHeader.CurrentWireFormat,
                messageType: MessageType.ResultPush,
                flags: HeaderFlags.None,
                metaLength: ResultPushMetadata.CurrentMetadataLength,
                bodyLength: (uint)bodyLengthBytes,
                sessionId: 12,
                frameId: 13,
                viewId: 0,
                routeId: 0,
                traceId: 14);
            var original = new ResultPushMessage(header, metadata, new ushort[] { 10, 11 }, new[] { section });

            var packet = original.ToArray();

            Assert.True(ResultPushMessage.TryParse(packet, out var parsed, out var error), $"Parse error: {error}");
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal<uint>((uint)ResultPushMetadata.CurrentMetadataLength, parsed.Header.MetaLength);
            Assert.Equal(ResultClass.Complete, parsed.Metadata.ResultClass);
            Assert.Equal(BudgetPolicy.AllowPartial | BudgetPolicy.AllowDrop, parsed.Metadata.AppliedBudgetPolicy);
            Assert.Equal<uint>(0U, parsed.Metadata.ReusedFrameId);
            Assert.Equal<ushort>(2, parsed.Metadata.CoveredTileCount);
            Assert.Equal<ushort>(0, parsed.Metadata.DroppedTileCount);
            Assert.Equal(PayloadKind.Tensor, parsed.Metadata.PayloadKindBitmap);
            Assert.Equal<ushort>(0, parsed.Metadata.PayloadFrameCount);
            Assert.Equal(packet, parsed.ToArray());
        }

        [Fact]
        public void ResultPushMessageRoundTripsStaleReuseMetadataPayload()
        {
            var section = CreateSection(
                role: TensorRole.SrResidual,
                lengthTable: new byte[] { 2, 0, 0, 0, 2, 0, 0, 0 },
                payload: new byte[] { 0x10, 0x11, 0x20, 0x21 });
            var metadata = new ResultPushMetadata(
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
                reusedFrameId: 99,
                coveredTileCount: 2,
                droppedTileCount: 0,
                payloadKindBitmap: PayloadKind.Tensor,
                payloadFrameCount: 0);
            var header = new NnrpHeader(
                versionMajor: NnrpHeader.CurrentVersionMajor,
                wireFormat: NnrpHeader.CurrentWireFormat,
                messageType: MessageType.ResultPush,
                flags: HeaderFlags.None,
                metaLength: ResultPushMetadata.CurrentMetadataLength,
                bodyLength: (uint)section.TotalLength,
                sessionId: 12,
                frameId: 13,
                viewId: 0,
                routeId: 0,
                traceId: 14);
            var original = new ResultPushMessage(header, metadata, new ushort[] { 10, 11 }, new[] { section });

            var packet = original.ToArray();

            Assert.True(ResultPushMessage.TryParse(packet, out var parsed, out var error), $"Parse error: {error}");
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(ResultClass.StaleReuse, parsed.Metadata.ResultClass);
            Assert.Equal<uint>(99U, parsed.Metadata.ReusedFrameId);
            Assert.Equal(ResultFlags.Stale, parsed.Metadata.ResultFlags);
            Assert.Equal(packet, parsed.ToArray());
        }

        [Fact]
        public void ResultPushMessageParsesDenseRangeBodyWithoutTileIndexObject()
        {
            var section = CreateSection(
                role: TensorRole.SrResidual,
                lengthTable: new byte[] { 2, 0, 0, 0, 2, 0, 0, 0 },
                payload: new byte[] { 0x10, 0x11, 0x20, 0x21 });
            var sectionTable = section.ToArray();
            var inlineObjectRegion = BodyCodec.PackInlineObjectRegion(
                BodyCodec.BuildInlineObjectBlock(CacheObjectKind.TensorSectionTable, sectionTable));
            var typedDescriptor = new TypedPayloadDescriptor(
                PayloadKind.ToolDelta,
                descriptorFlags: 0,
                profileId: 9,
                payloadOffset: 0,
                payloadLength: 3,
                reserved: 0);
            var descriptorRegion = typedDescriptor.ToArray();
            var typedPayload = new byte[] { 0x41, 0x42, 0x43 };
            var body = BodyCodec.Pack(
                inlineObjectRegion: inlineObjectRegion,
                typedPayloadDescriptorRegion: descriptorRegion,
                typedPayloadFrameRegion: typedPayload);
            var metadata = new ResultPushMetadata(
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
                coveredTileCount: 2,
                droppedTileCount: 0,
                payloadKindBitmap: PayloadKind.Tensor | PayloadKind.ToolDelta,
                payloadFrameCount: 1);
            var framed = new NnrpFramedMessage(
                new NnrpHeader(
                    versionMajor: NnrpHeader.CurrentVersionMajor,
                    wireFormat: NnrpHeader.CurrentWireFormat,
                    messageType: MessageType.ResultPush,
                    flags: HeaderFlags.None,
                    metaLength: ResultPushMetadata.CurrentMetadataLength,
                    bodyLength: (uint)body.Length,
                    sessionId: 12,
                    frameId: 13,
                    viewId: 0,
                    routeId: 0,
                    traceId: 14),
                metadata.ToArray(),
                body);

            Assert.True(ResultPushMessage.TryParse(framed, out var parsed, out var error), $"Parse error: {error}");
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(new ushort[] { 10, 11 }, parsed.TileIds.ToArray());
            Assert.Single(parsed.Sections.ToArray());
            Assert.Equal(new[] { typedDescriptor }, parsed.TypedPayloadDescriptors.ToArray());
            Assert.Equal(typedPayload, parsed.TypedPayloadFrameRegion.ToArray());
            Assert.Single(parsed.TypedPayloadFrames.ToArray());
            Assert.Equal(PayloadKind.ToolDelta, parsed.TypedPayloadFrames.Span[0].PayloadKind);
            Assert.Equal((ushort)9, parsed.TypedPayloadFrames.Span[0].ProfileId);
            Assert.Equal(typedPayload, parsed.TypedPayloadFrames.Span[0].Payload.ToArray());
            Assert.Equal(
                new[] { new TypedPayloadProfileCoverage(PayloadKind.ToolDelta, 9, 1, 3) },
                parsed.TypedPayloadCoverages.ToArray());

            var roundTrip = parsed.ToArray();
            Assert.True(ResultPushMessage.TryParse(roundTrip, out var reparsed, out error), $"Round-trip parse error: {error}");
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(new ushort[] { 10, 11 }, reparsed.TileIds.ToArray());
            Assert.Equal(typedPayload, reparsed.TypedPayloadFrameRegion.ToArray());
        }

        [Fact]
        public void ResultPushMessageRejectsCompositeBodiesWithUndeclaredTypedPayloadCoverage()
        {
            var section = CreateSection(
                role: TensorRole.SrResidual,
                lengthTable: new byte[] { 2, 0, 0, 0, 2, 0, 0, 0 },
                payload: new byte[] { 0x10, 0x11, 0x20, 0x21 });
            var sectionTable = section.ToArray();
            var inlineObjectRegion = BodyCodec.PackInlineObjectRegion(
                BodyCodec.BuildInlineObjectBlock(CacheObjectKind.TensorSectionTable, sectionTable));
            var typedDescriptor = new TypedPayloadDescriptor(
                PayloadKind.ToolDelta,
                descriptorFlags: 0,
                profileId: 9,
                payloadOffset: 0,
                payloadLength: 3,
                reserved: 0);
            var body = BodyCodec.Pack(
                inlineObjectRegion: inlineObjectRegion,
                typedPayloadDescriptorRegion: typedDescriptor.ToArray(),
                typedPayloadFrameRegion: new byte[] { 0x41, 0x42, 0x43 });
            var metadata = new ResultPushMetadata(
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
                coveredTileCount: 2,
                droppedTileCount: 0,
                payloadKindBitmap: PayloadKind.Tensor | PayloadKind.ToolDelta | PayloadKind.StructuredEvent,
                payloadFrameCount: 1);
            var framed = new NnrpFramedMessage(
                new NnrpHeader(
                    versionMajor: NnrpHeader.CurrentVersionMajor,
                    wireFormat: NnrpHeader.CurrentWireFormat,
                    messageType: MessageType.ResultPush,
                    flags: HeaderFlags.None,
                    metaLength: ResultPushMetadata.CurrentMetadataLength,
                    bodyLength: (uint)body.Length,
                    sessionId: 12,
                    frameId: 13,
                    viewId: 0,
                    routeId: 0,
                    traceId: 14),
                metadata.ToArray(),
                body);

            Assert.False(ResultPushMessage.TryParse(framed, out _, out var error));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, error);
        }

        [Fact]
        public void ResultPushMessageRejectsObjectReferencesWithoutCacheStore()
        {
            var packet = CreateCurrentReferencedResultPacket(out _);

            Assert.False(ResultPushMessage.TryParse(packet, out _, out var error));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, error);
        }

        [Fact]
        public void ResultPushMessageGetsTypedPayloadFramesByKindAndProfile()
        {
            var section = CreateSection(
                role: TensorRole.SrResidual,
                lengthTable: new byte[] { 2, 0, 0, 0, 2, 0, 0, 0 },
                payload: new byte[] { 0x10, 0x11, 0x20, 0x21 });
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
            var payloadRegion = new byte[] { 0x41, 0x42, 0x43, 0x44, 0x45, 0x90, 0x91, 0x92, 0x93 };
            var message = new ResultPushMessage(
                new NnrpHeader(
                    versionMajor: NnrpHeader.CurrentVersionMajor,
                    wireFormat: NnrpHeader.CurrentWireFormat,
                    messageType: MessageType.ResultPush,
                    flags: HeaderFlags.None,
                    metaLength: ResultPushMetadata.CurrentMetadataLength,
                    bodyLength: 0,
                    sessionId: 12,
                    frameId: 13,
                    viewId: 0,
                    routeId: 0,
                    traceId: 14),
                new ResultPushMetadata(
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
                    coveredTileCount: 2,
                    droppedTileCount: 0,
                    payloadKindBitmap: PayloadKind.Tensor | PayloadKind.ToolDelta | PayloadKind.StructuredEvent,
                    payloadFrameCount: 3),
                new ushort[] { 10, 11 },
                new[] { section },
                descriptors,
                payloadRegion);

            Assert.True(ResultPushMessage.TryParse(message.ToArray(), out var parsed, out var error), $"Parse error: {error}");
            Assert.Equal(NnrpParseError.None, error);

            var toolFrames = parsed.GetTypedPayloadFrames(PayloadKind.ToolDelta, 9);
            Assert.Equal(2, toolFrames.Length);
            Assert.Equal(new byte[] { 0x41, 0x42 }, toolFrames[0].Payload.ToArray());
            Assert.Equal(new byte[] { 0x43, 0x44, 0x45 }, toolFrames[1].Payload.ToArray());

            var structuredEventFrames = parsed.GetTypedPayloadFrames(PayloadKind.StructuredEvent, 5);
            Assert.Single(structuredEventFrames);
            Assert.Equal(new byte[] { 0x90, 0x91, 0x92, 0x93 }, structuredEventFrames[0].Payload.ToArray());

            Assert.Empty(parsed.GetTypedPayloadFrames(PayloadKind.StructuredEvent, 9));
        }

        [Fact]
        public void ResultPushMessageExposesPayloadFamilyFrameSetsAndCoverage()
        {
            var section = CreateSection(
                role: TensorRole.SrResidual,
                lengthTable: new byte[] { 2, 0, 0, 0, 2, 0, 0, 0 },
                payload: new byte[] { 0x10, 0x11, 0x20, 0x21 });
            var descriptors = new[]
            {
                new TypedPayloadDescriptor(
                    PayloadKind.TokenChunk,
                    descriptorFlags: 0,
                    profileId: 3,
                    payloadOffset: 0,
                    payloadLength: 2,
                    reserved: 0),
                new TypedPayloadDescriptor(
                    PayloadKind.AudioChunk,
                    descriptorFlags: 0,
                    profileId: 4,
                    payloadOffset: 2,
                    payloadLength: 3,
                    reserved: 0),
                new TypedPayloadDescriptor(
                    PayloadKind.StructuredEvent,
                    descriptorFlags: 0,
                    profileId: 6,
                    payloadOffset: 5,
                    payloadLength: 4,
                    reserved: 0)
            };
            var payloadRegion = new byte[] { 0x31, 0x32, 0x41, 0x42, 0x43, 0x61, 0x62, 0x63, 0x64 };
            var message = new ResultPushMessage(
                new NnrpHeader(
                    versionMajor: NnrpHeader.CurrentVersionMajor,
                    wireFormat: NnrpHeader.CurrentWireFormat,
                    messageType: MessageType.ResultPush,
                    flags: HeaderFlags.None,
                    metaLength: ResultPushMetadata.CurrentMetadataLength,
                    bodyLength: 0,
                    sessionId: 12,
                    frameId: 13,
                    viewId: 0,
                    routeId: 0,
                    traceId: 14),
                new ResultPushMetadata(
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
                    coveredTileCount: 2,
                    droppedTileCount: 0,
                    payloadKindBitmap: PayloadKind.Tensor | PayloadKind.TokenChunk | PayloadKind.AudioChunk | PayloadKind.StructuredEvent,
                    payloadFrameCount: 3),
                new ushort[] { 10, 11 },
                new[] { section },
                descriptors,
                payloadRegion);

            Assert.True(ResultPushMessage.TryParse(message.ToArray(), out var parsed, out var error), $"Parse error: {error}");
            Assert.Equal(NnrpParseError.None, error);

            var tokenFrames = parsed.GetTokenChunkFrames(3);
            Assert.Equal(PayloadKind.TokenChunk, tokenFrames.PayloadKind);
            Assert.Equal((ushort)3, tokenFrames.ProfileId);
            Assert.Equal(1, tokenFrames.FrameCount);
            Assert.Equal(2, tokenFrames.PayloadBytes);
            Assert.Equal(new byte[] { 0x31, 0x32 }, tokenFrames.Frames.Span[0].Payload.ToArray());

            var audioFrames = parsed.GetAudioChunkFrames(4);
            Assert.Equal(1, audioFrames.FrameCount);
            Assert.Equal(3, audioFrames.PayloadBytes);
            Assert.Equal(new byte[] { 0x41, 0x42, 0x43 }, audioFrames.Frames.Span[0].Payload.ToArray());

            var eventFrames = parsed.GetStructuredEventFrames(6);
            Assert.Equal(1, eventFrames.FrameCount);
            Assert.Equal(4, eventFrames.PayloadBytes);
            Assert.Equal(new byte[] { 0x61, 0x62, 0x63, 0x64 }, eventFrames.Frames.Span[0].Payload.ToArray());

            Assert.True(parsed.TryGetPayloadCoverage(PayloadKind.TokenChunk, 3, out var tokenCoverage));
            Assert.Equal(new TypedPayloadProfileCoverage(PayloadKind.TokenChunk, 3, 1, 2), tokenCoverage);
            Assert.False(parsed.TryGetPayloadCoverage(PayloadKind.VideoChunk, 9, out _));
        }

        [Fact]
        public void ResultPushMessageRejectsBitmapLookupsForTypedPayloadFrames()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => default(ResultPushMessage).GetTypedPayloadFrames(PayloadKind.ToolDelta | PayloadKind.StructuredEvent, 9));
        }

        [Fact]
        public void ResultPushMessageExposesToolDeltaAndOpaqueBytesPayloadFamilies()
        {
            var section = CreateSection(
                role: TensorRole.SrResidual,
                lengthTable: new byte[] { 2, 0, 0, 0, 2, 0, 0, 0 },
                payload: new byte[] { 0x10, 0x11, 0x20, 0x21 });
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
                    PayloadKind.OpaqueBytes,
                    descriptorFlags: 0,
                    profileId: 12,
                    payloadOffset: 5,
                    payloadLength: 4,
                    reserved: 0)
            };
            var payloadRegion = new byte[] { 0x41, 0x42, 0x43, 0x44, 0x45, 0x90, 0x91, 0x92, 0x93 };
            var message = new ResultPushMessage(
                new NnrpHeader(
                    versionMajor: NnrpHeader.CurrentVersionMajor,
                    wireFormat: NnrpHeader.CurrentWireFormat,
                    messageType: MessageType.ResultPush,
                    flags: HeaderFlags.None,
                    metaLength: ResultPushMetadata.CurrentMetadataLength,
                    bodyLength: 0,
                    sessionId: 12,
                    frameId: 13,
                    viewId: 0,
                    routeId: 0,
                    traceId: 14),
                new ResultPushMetadata(
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
                    coveredTileCount: 2,
                    droppedTileCount: 0,
                    payloadKindBitmap: PayloadKind.Tensor | PayloadKind.ToolDelta | PayloadKind.OpaqueBytes,
                    payloadFrameCount: 3),
                new ushort[] { 10, 11 },
                new[] { section },
                descriptors,
                payloadRegion);

            Assert.True(ResultPushMessage.TryParse(message.ToArray(), out var parsed, out var error), $"Parse error: {error}");
            Assert.Equal(NnrpParseError.None, error);

            var toolFrames = parsed.GetToolDeltaFrames(9);
            Assert.Equal(PayloadKind.ToolDelta, toolFrames.PayloadKind);
            Assert.Equal((ushort)9, toolFrames.ProfileId);
            Assert.Equal(2, toolFrames.FrameCount);
            Assert.Equal(5, toolFrames.PayloadBytes);
            Assert.Equal(new byte[] { 0x41, 0x42 }, toolFrames.Frames.Span[0].Payload.ToArray());
            Assert.Equal(new byte[] { 0x43, 0x44, 0x45 }, toolFrames.Frames.Span[1].Payload.ToArray());

            var opaqueFrames = parsed.GetOpaqueBytesFrames(12);
            Assert.Equal(PayloadKind.OpaqueBytes, opaqueFrames.PayloadKind);
            Assert.Equal((ushort)12, opaqueFrames.ProfileId);
            Assert.Equal(1, opaqueFrames.FrameCount);
            Assert.Equal(4, opaqueFrames.PayloadBytes);
            Assert.Equal(new byte[] { 0x90, 0x91, 0x92, 0x93 }, opaqueFrames.Frames.Span[0].Payload.ToArray());

            Assert.True(parsed.TryGetPayloadCoverage(PayloadKind.ToolDelta, 9, out var toolCoverage));
            Assert.Equal(new TypedPayloadProfileCoverage(PayloadKind.ToolDelta, 9, 2, 5), toolCoverage);
            Assert.True(parsed.TryGetPayloadCoverage(PayloadKind.OpaqueBytes, 12, out var opaqueCoverage));
            Assert.Equal(new TypedPayloadProfileCoverage(PayloadKind.OpaqueBytes, 12, 1, 4), opaqueCoverage);
        }

        [Fact]
        public void ResultPushMessageParsesObjectReferencesWithCacheStore()
        {
            var packet = CreateCurrentReferencedResultPacket(out var cacheStore);

            Assert.True(ResultPushMessage.TryParse(packet, cacheStore, out var parsed, out var error), $"Parse error: {error}");
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(new ushort[] { 20, 21 }, parsed.TileIds.ToArray());
            Assert.Single(parsed.Sections.ToArray());
            Assert.Equal(TensorRole.SrResidual, parsed.Sections.Span[0].Descriptor.Role);
            Assert.Single(parsed.TypedPayloadDescriptors.ToArray());
            Assert.Equal(PayloadKind.ToolDelta, parsed.TypedPayloadDescriptors.Span[0].PayloadKind);
            Assert.Equal(new byte[] { 0x41, 0x42, 0x43 }, parsed.TypedPayloadFrameRegion.ToArray());
            Assert.Single(parsed.TypedPayloadFrames.ToArray());
            Assert.Equal(PayloadKind.ToolDelta, parsed.TypedPayloadFrames.Span[0].PayloadKind);
            Assert.Equal((ushort)9, parsed.TypedPayloadFrames.Span[0].ProfileId);
            Assert.Equal(new byte[] { 0x41, 0x42, 0x43 }, parsed.TypedPayloadFrames.Span[0].Payload.ToArray());
            Assert.Equal(
                new[] { new TypedPayloadProfileCoverage(PayloadKind.ToolDelta, 9, 1, 3) },
                parsed.TypedPayloadCoverages.ToArray());

            var roundTrip = parsed.ToArray();
            Assert.True(ResultPushMessage.TryParse(roundTrip, out var reparsed, out error), $"Round-trip parse error: {error}");
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(new ushort[] { 20, 21 }, reparsed.TileIds.ToArray());
            Assert.Equal(new byte[] { 0x41, 0x42, 0x43 }, reparsed.TypedPayloadFrameRegion.ToArray());
        }

        [Fact]
        public void ResultPushMessageParsesPartialCompositeBodyWithObjectReferences()
        {
            var section = CreateSection(
                role: TensorRole.SrResidual,
                lengthTable: new byte[] { 1, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0, 0 },
                payload: new byte[] { 0x10, 0x30, 0x31 });
            var sectionTable = section.ToArray();
            var tileIds = new ushort[] { 40, 42, 45 };
            var tileIndexPayload = TileIndexBlockCodec.Encode(tileIds, TileIndexMode.RawUInt16, tileBaseId: 40);
            var typedDescriptor = new TypedPayloadDescriptor(
                PayloadKind.StructuredEvent,
                descriptorFlags: 0,
                profileId: 12,
                payloadOffset: 0,
                payloadLength: 4,
                reserved: 0);
            var typedPayload = new byte[] { 0x61, 0x62, 0x63, 0x64 };
            var objectReferenceRegion = BodyCodec.PackObjectReferenceRegion(
                BodyCodec.BuildObjectReferenceBlock(CacheObjectKind.TileIndexBlock, 2, 110, 210),
                BodyCodec.BuildObjectReferenceBlock(CacheObjectKind.TensorSectionTable, 2, 111, 211));
            var body = BodyCodec.Pack(
                objectReferenceRegion: objectReferenceRegion,
                typedPayloadDescriptorRegion: typedDescriptor.ToArray(),
                typedPayloadFrameRegion: typedPayload);
            var metadata = new ResultPushMetadata(
                statusCode: ResultStatusCode.Success,
                resultFlags: ResultFlags.Partial,
                sectionCount: 1,
                tileCount: 3,
                activeProfileId: 7,
                inferenceMilliseconds: 12,
                queueMilliseconds: 3,
                serverTotalMilliseconds: 15,
                tileBaseId: 40,
                tileIndexBytes: (uint)tileIndexPayload.Length,
                resultClass: ResultClass.Partial,
                appliedBudgetPolicy: BudgetPolicy.AllowPartial | BudgetPolicy.AllowDrop,
                reusedFrameId: 0,
                coveredTileCount: 2,
                droppedTileCount: 1,
                payloadKindBitmap: PayloadKind.Tensor | PayloadKind.StructuredEvent,
                payloadFrameCount: 1);
            var framed = new NnrpFramedMessage(
                new NnrpHeader(
                    versionMajor: NnrpHeader.CurrentVersionMajor,
                    wireFormat: NnrpHeader.CurrentWireFormat,
                    messageType: MessageType.ResultPush,
                    flags: HeaderFlags.None,
                    metaLength: ResultPushMetadata.CurrentMetadataLength,
                    bodyLength: (uint)body.Length,
                    sessionId: 12,
                    frameId: 23,
                    viewId: 0,
                    routeId: 0,
                    traceId: 14),
                metadata.ToArray(),
                body);

            var cacheStore = new NnrpCacheStore();
            Assert.True(cacheStore.TryPut(new NnrpCacheKey(2, 110, 210), tileIndexPayload, ttlSeconds: 60).IsSuccess);
            Assert.True(cacheStore.TryPut(new NnrpCacheKey(2, 111, 211), sectionTable, ttlSeconds: 60).IsSuccess);

            Assert.True(ResultPushMessage.TryParse(framed, cacheStore, out var parsed, out var error), $"Parse error: {error}");
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(ResultClass.Partial, parsed.Metadata.ResultClass);
            Assert.Equal(ResultFlags.Partial, parsed.Metadata.ResultFlags);
            Assert.Equal(BudgetPolicy.AllowPartial | BudgetPolicy.AllowDrop, parsed.Metadata.AppliedBudgetPolicy);
            Assert.Equal<ushort>(2, parsed.Metadata.CoveredTileCount);
            Assert.Equal<ushort>(1, parsed.Metadata.DroppedTileCount);
            Assert.Equal(tileIds, parsed.TileIds.ToArray());
            Assert.Single(parsed.Sections.ToArray());
            Assert.Equal(TensorRole.SrResidual, parsed.Sections.Span[0].Descriptor.Role);
            Assert.Equal(typedPayload, parsed.TypedPayloadFrameRegion.ToArray());
            Assert.Single(parsed.TypedPayloadDescriptors.ToArray());
            Assert.Equal(PayloadKind.StructuredEvent, parsed.TypedPayloadDescriptors.Span[0].PayloadKind);
            Assert.Single(parsed.TypedPayloadFrames.ToArray());
            Assert.Equal(PayloadKind.StructuredEvent, parsed.TypedPayloadFrames.Span[0].PayloadKind);
            Assert.Equal((ushort)12, parsed.TypedPayloadFrames.Span[0].ProfileId);
            Assert.Equal(typedPayload, parsed.TypedPayloadFrames.Span[0].Payload.ToArray());
            Assert.Equal(
                new[] { new TypedPayloadProfileCoverage(PayloadKind.StructuredEvent, 12, 1, 4) },
                parsed.TypedPayloadCoverages.ToArray());

            var roundTrip = parsed.ToArray();
            Assert.True(ResultPushMessage.TryParse(roundTrip, out var reparsed, out error), $"Round-trip parse error: {error}");
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(ResultClass.Partial, reparsed.Metadata.ResultClass);
            Assert.Equal(tileIds, reparsed.TileIds.ToArray());
            Assert.Equal(typedPayload, reparsed.TypedPayloadFrameRegion.ToArray());
        }

        [Fact]
        public void ResultPushMessageRejectsMalformedSectionLayout()
        {
            var section = CreateSection(
                role: TensorRole.DetailResidual,
                lengthTable: new byte[] { 1, 0, 0, 0 },
                payload: new byte[] { 0x99 });
            var resultBlock = new TensorResultBlock(
                sectionCount: 2,
                tileCount: 1,
                tileIndexMode: TileIndexMode.DenseRange,
                tensorFlags: 0,
                reserved0: 0,
                tileBaseId: 0,
                tileIndexBytes: 0);
            var metadata = new ResultPushMetadata(
                statusCode: ResultStatusCode.Success,
                resultFlags: ResultFlags.None,
                activeProfileId: 1,
                payloadKind: PayloadKind.Tensor,
                reserved0: 0,
                inferenceMilliseconds: 3,
                queueMilliseconds: 1,
                serverTotalMilliseconds: 4,
                reserved1: 0,
                profileBlockBytes: TensorResultBlock.BlockLength,
                payloadDescriptorBytes: (uint)(TensorSectionDescriptor.DescriptorLength + section.Descriptor.LengthTableBytes),
                payloadDataBytes: section.Descriptor.PayloadBytes);
            var body = new byte[TensorResultBlock.BlockLength + section.TotalLength];
            resultBlock.ToArray().CopyTo(body, 0);
            section.ToArray().CopyTo(body, TensorResultBlock.BlockLength);
            var framed = new NnrpFramedMessage(
                new NnrpHeader(
                    versionMajor: NnrpHeader.CurrentVersionMajor,
                    wireFormat: NnrpHeader.CurrentWireFormat,
                    messageType: MessageType.ResultPush,
                    flags: HeaderFlags.None,
                    metaLength: ResultPushMetadata.MetadataLength,
                    bodyLength: (uint)body.Length,
                    sessionId: 5,
                    frameId: 6,
                    viewId: 0,
                    routeId: 0,
                    traceId: 7),
                metadata.ToArray(),
                body);

            Assert.False(ResultPushMessage.TryParse(framed.ToArray(), out _, out var error));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, error);
        }

        [Fact]
        public void ResultPushMessageRejectsNonZeroInterBlockPadding()
        {
            var section = CreateSection(
                role: TensorRole.SrResidual,
                lengthTable: new byte[] { 1, 0, 0, 0, 1, 0, 0, 0 },
                payload: new byte[] { 0x10, 0x20 });
            var metadata = new ResultPushMetadata(
                statusCode: ResultStatusCode.Success,
                resultFlags: ResultFlags.None,
                sectionCount: 1,
                tileCount: 2,
                activeProfileId: 3,
                inferenceMilliseconds: 8,
                queueMilliseconds: 1,
                serverTotalMilliseconds: 10,
                tileBaseId: 0,
                tileIndexBytes: 4);
            var header = new NnrpHeader(
                versionMajor: NnrpHeader.CurrentVersionMajor,
                wireFormat: NnrpHeader.CurrentWireFormat,
                messageType: MessageType.ResultPush,
                flags: HeaderFlags.None,
                metaLength: ResultPushMetadata.MetadataLength,
                bodyLength: (uint)(BinaryAlignment.AlignUp(TensorResultBlock.BlockLength + 4, 8) + section.TotalLength),
                sessionId: 2,
                frameId: 3,
                viewId: 0,
                routeId: 0,
                traceId: 4);
            var packet = new ResultPushMessage(header, metadata, new ushort[] { 5, 6 }, new[] { section }).ToArray();

            packet[NnrpHeader.HeaderLength + ResultPushMetadata.MetadataLength + TensorResultBlock.BlockLength + 4] = 0x01;

            Assert.False(ResultPushMessage.TryParse(packet, out _, out var error));
            Assert.Equal(NnrpParseError.SourceTooShort, error);
        }

        private static TensorSectionBlock CreateSection(TensorRole role, byte[] lengthTable, byte[] payload)
        {
            return new TensorSectionBlock(
                new TensorSectionDescriptor(
                    role: role,
                    codec: CodecId.Raw,
                    dtype: DTypeId.UInt8,
                    layout: TensorLayoutId.Nhwc,
                    scalePolicy: ScalePolicy.None,
                    flags: 0,
                    elementCountPerTile: 1,
                    codecTableBytes: 0,
                    lengthTableBytes: (uint)lengthTable.Length,
                    payloadBytes: (uint)payload.Length,
                    payloadStrideBytes: 0),
                Array.Empty<byte>(),
                lengthTable,
                payload);
        }

        private static byte[] CreateCurrentReferencedResultPacket(out NnrpCacheStore cacheStore)
        {
            var section = CreateSection(
                role: TensorRole.SrResidual,
                lengthTable: new byte[] { 1, 0, 0, 0, 1, 0, 0, 0 },
                payload: new byte[] { 0x10, 0x20 });
            var sectionTable = section.ToArray();
            var tileIds = new ushort[] { 20, 21 };
            var tileIndexPayload = TileIndexBlockCodec.Encode(tileIds, TileIndexMode.RawUInt16, tileBaseId: 20);
            var objectReferenceRegion = BodyCodec.PackObjectReferenceRegion(
                BodyCodec.BuildObjectReferenceBlock(CacheObjectKind.TileIndexBlock, 1, 100, 200),
                BodyCodec.BuildObjectReferenceBlock(CacheObjectKind.TensorSectionTable, 1, 101, 201));
            var typedDescriptor = new TypedPayloadDescriptor(
                PayloadKind.ToolDelta,
                descriptorFlags: 0,
                profileId: 9,
                payloadOffset: 0,
                payloadLength: 3,
                reserved: 0);
            var body = BodyCodec.Pack(
                objectReferenceRegion: objectReferenceRegion,
                typedPayloadDescriptorRegion: typedDescriptor.ToArray(),
                typedPayloadFrameRegion: new byte[] { 0x41, 0x42, 0x43 });
            var metadata = new ResultPushMetadata(
                statusCode: ResultStatusCode.Success,
                resultFlags: ResultFlags.None,
                sectionCount: 1,
                tileCount: 2,
                activeProfileId: 7,
                inferenceMilliseconds: 12,
                queueMilliseconds: 3,
                serverTotalMilliseconds: 15,
                tileBaseId: 20,
                tileIndexBytes: (uint)tileIndexPayload.Length,
                resultClass: ResultClass.Complete,
                appliedBudgetPolicy: BudgetPolicy.None,
                reusedFrameId: 0,
                coveredTileCount: 2,
                droppedTileCount: 0,
                payloadKindBitmap: PayloadKind.Tensor | PayloadKind.ToolDelta,
                payloadFrameCount: 1);
            var framed = new NnrpFramedMessage(
                new NnrpHeader(
                    versionMajor: NnrpHeader.CurrentVersionMajor,
                    wireFormat: NnrpHeader.CurrentWireFormat,
                    messageType: MessageType.ResultPush,
                    flags: HeaderFlags.None,
                    metaLength: ResultPushMetadata.CurrentMetadataLength,
                    bodyLength: (uint)body.Length,
                    sessionId: 12,
                    frameId: 13,
                    viewId: 0,
                    routeId: 0,
                    traceId: 14),
                metadata.ToArray(),
                body);

            cacheStore = new NnrpCacheStore();
            Assert.True(cacheStore.TryPut(new NnrpCacheKey(1, 100, 200), tileIndexPayload, ttlSeconds: 60).IsSuccess);
            Assert.True(cacheStore.TryPut(new NnrpCacheKey(1, 101, 201), sectionTable, ttlSeconds: 60).IsSuccess);
            return framed.ToArray();
        }
    }
}
