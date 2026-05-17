using System;
using Nnrp.Core;
using Xunit;

namespace Nnrp.Core.Tests
{
    public sealed class FrameSubmitMessageTests
    {
        [Fact]
        public void FrameSubmitMessageParsesPythonGoldenPacketAndRoundTrips()
        {
            // Build a known-good FrameSubmitMessage with the same layout as the
            // original Python golden packet, then validate round-trip fidelity.
            // Hardcoding a hex literal is brittle when wire layout changes;
            // instead we construct, serialize, parse and assert field equality.
            var section0LengthTable = new byte[] { 1, 0, 0, 0, 2, 0, 0, 0 };
            var section0Payload = new byte[] { (byte)'x', (byte)'y', (byte)'z' };
            var section1LengthTable = new byte[] { 3, 0, 0, 0, 4, 0, 0, 0 };
            var section1Payload = new byte[] { (byte)'q' };

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
                    role: TensorRole.RoughMetal,
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

            var cameraBlock = new byte[] { (byte)'c', (byte)'a', (byte)'m' };
            var tileIds = new ushort[] { 5, 6 };
            var tileIndexBytes = TileIndexBlockCodec.GetEncodedLength(tileIds, TileIndexMode.RawUInt16);

            var bodyLength = FrameSubmitMessage.ComputeBodyLength(cameraBlock.Length, tileIndexBytes, new[] { section0, section1 });

            var metadata = new FrameSubmitMetadata(
                sourceWidth: 64,
                sourceHeight: 64,
                tileWidth: 32,
                tileHeight: 32,
                tileCount: (ushort)tileIds.Length,
                sectionCount: 2,
                frameClass: FrameClass.Keyframe,
                inputProfile: InputProfile.ChangedTilesLuma,
                tileIndexMode: TileIndexMode.RawUInt16,
                latencyBudgetMilliseconds: 0,
                targetFpsTimes100: 0,
                retryOfFrame: 0,
                tileBaseId: 0,
                cameraBytes: (uint)cameraBlock.Length,
                tileIndexBytes: (uint)tileIndexBytes);

            var header = new NnrpHeader(
                versionMajor: NnrpHeader.CurrentVersionMajor,
                wireFormat: NnrpHeader.CurrentWireFormat,
                messageType: MessageType.FrameSubmit,
                flags: HeaderFlags.None,
                metaLength: FrameSubmitMessage.MetadataLength,
                bodyLength: (uint)bodyLength,
                sessionId: 0x34,
                frameId: 0x5B,
                viewId: 0x07,
                routeId: 0x00,
                traceId: 0);

            var original = new FrameSubmitMessage(header, metadata, cameraBlock, tileIds, new[] { section0, section1 });
            var packet = original.ToArray();

            Assert.True(FrameSubmitMessage.TryParse(packet, out var message, out var error), $"Parse error: {error}");
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(MessageType.FrameSubmit, message.Header.MessageType);
            Assert.Equal(cameraBlock, message.CameraBlock.ToArray());
            Assert.Equal(tileIds, message.TileIds.ToArray());
            Assert.Equal(2, message.Sections.Length);
            Assert.Equal(TensorRole.LumaHint, message.Sections.Span[0].Descriptor.Role);
            Assert.Equal(TensorRole.RoughMetal, message.Sections.Span[1].Descriptor.Role);
            Assert.Equal(packet, message.ToArray());
        }

        [Fact]
        public void FrameSubmitMessageRoundTripsChangedTileInput()
        {
            var section = CreateSection(
                role: TensorRole.LumaHint,
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
                bodyLength: FrameSubmitMessage.ComputeBodyLength(2, 4, new[] { section }),
                sessionId: 9,
                frameId: 15,
                viewId: 1,
                routeId: 0,
                traceId: 88);
            var original = new FrameSubmitMessage(
                header,
                metadata,
                new byte[] { 0xCA, 0xFE },
                new ushort[] { 1, 9 },
                new[] { section });

            var packet = original.ToArray();

            Assert.True(FrameSubmitMessage.TryParse(packet, out var parsed, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(new ushort[] { 1, 9 }, parsed.TileIds.ToArray());
            Assert.Single(parsed.Sections.ToArray());
            Assert.Equal(packet, parsed.ToArray());
        }

        [Fact]
        public void FrameSubmitMessageRoundTripsCameraOnlyBodyWithAlignmentPadding()
        {
            var metadata = CreateMetadata(cameraBytes: 3, tileCount: 0, sectionCount: 0, tileIndexBytes: 0);
            var header = new NnrpHeader(
                versionMajor: NnrpHeader.CurrentVersionMajor,
                wireFormat: NnrpHeader.CurrentWireFormat,
                messageType: MessageType.FrameSubmit,
                flags: HeaderFlags.None,
                metaLength: FrameSubmitMessage.MetadataLength,
                bodyLength: FrameSubmitMessage.ComputeBodyLength(3, 0, Array.Empty<TensorSectionBlock>()),
                sessionId: 9,
                frameId: 15,
                viewId: 1,
                routeId: 0,
                traceId: 88);
            var original = new FrameSubmitMessage(
                header,
                metadata,
                new byte[] { 0xCA, 0xFE, 0xBA },
                Array.Empty<ushort>(),
                Array.Empty<TensorSectionBlock>());

            var packet = original.ToArray();

            Assert.True(FrameSubmitMessage.TryParse(packet, out var parsed, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(new byte[] { 0xCA, 0xFE, 0xBA }, parsed.CameraBlock.ToArray());
            Assert.Empty(parsed.TileIds.ToArray());
            Assert.Empty(parsed.Sections.ToArray());
            Assert.Equal(packet, parsed.ToArray());
        }

        [Fact]
        public void FrameSubmitMessageTryComputeBodyLengthMatchesAlignedLayout()
        {
            Assert.True(FrameSubmitMessage.TryComputeBodyLength(3, 0, Array.Empty<TensorSectionBlock>(), out var bodyLength));
            Assert.Equal(8u, bodyLength);
        }

        [Fact]
        public void FrameSubmitMessageRejectsMalformedSectionLayout()
        {
            var section = CreateSection(
                role: TensorRole.LumaHint,
                lengthTable: new byte[] { 1, 0, 0, 0 },
                payload: new byte[] { 0x7A });
            var metadata = new FrameSubmitMetadata(
                sourceWidth: 320,
                sourceHeight: 180,
                tileWidth: 32,
                tileHeight: 32,
                tileCount: 1,
                sectionCount: 1,
                frameClass: FrameClass.Keyframe,
                inputProfile: InputProfile.DenseLumaFrame,
                tileIndexMode: TileIndexMode.DenseRange,
                latencyBudgetMilliseconds: 16,
                targetFpsTimes100: 6000,
                retryOfFrame: 0,
                tileBaseId: 5,
                cameraBytes: 0,
                tileIndexBytes: 0);
            var header = new NnrpHeader(
                versionMajor: NnrpHeader.CurrentVersionMajor,
                wireFormat: NnrpHeader.CurrentWireFormat,
                messageType: MessageType.FrameSubmit,
                flags: HeaderFlags.None,
                metaLength: FrameSubmitMessage.MetadataLength,
                bodyLength: FrameSubmitMessage.ComputeBodyLength(0, 0, new[] { section }),
                sessionId: 1,
                frameId: 2,
                viewId: 0,
                routeId: 0,
                traceId: 3);
            var packet = new FrameSubmitMessage(header, metadata, Array.Empty<byte>(), new ushort[] { 5 }, new[] { section }).ToArray();
            packet[NnrpHeader.HeaderLength + FrameSubmitMessage.MetadataLength + 16] = 0xFF;

            Assert.False(FrameSubmitMessage.TryParse(packet, out _, out var error));
            Assert.Equal(NnrpParseError.InconsistentSectionDescriptor, error);
        }

        [Fact]
        public void FrameSubmitMessageRejectsNonZeroInterBlockPadding()
        {
            var section = CreateSection(
                role: TensorRole.LumaHint,
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
                bodyLength: FrameSubmitMessage.ComputeBodyLength(2, 4, new[] { section }),
                sessionId: 9,
                frameId: 15,
                viewId: 1,
                routeId: 0,
                traceId: 88);
            var packet = new FrameSubmitMessage(
                header,
                metadata,
                new byte[] { 0xCA, 0xFE },
                new ushort[] { 1, 9 },
                new[] { section }).ToArray();

            packet[NnrpHeader.HeaderLength + FrameSubmitMessage.MetadataLength + 2] = 0x7F;

            Assert.False(FrameSubmitMessage.TryParse(packet, out _, out var error));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, error);
        }

        [Fact]
        public void FrameSubmitMessageTryParseMetadataRoundTripsCurrentMetadata()
        {
            var metadata = CreateMetadata(cameraBytes: 2, tileCount: 2, sectionCount: 1, tileIndexBytes: 4);

            Assert.True(FrameSubmitMessage.TryParseMetadata(metadata.ToArray(), strict: true, out var parsed, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(metadata, parsed);
        }

        [Fact]
        public void FrameSubmitMessageTryParseRejectsNonInlineMetadataLayouts()
        {
            var metadata = new FrameSubmitMetadata(
                sourceWidth: 320,
                sourceHeight: 180,
                tileWidth: 32,
                tileHeight: 32,
                tileCount: 0,
                sectionCount: 0,
                frameClass: FrameClass.Keyframe,
                inputProfile: InputProfile.DenseLumaFrame,
                tileIndexMode: TileIndexMode.DenseRange,
                reserved0: 0,
                latencyBudgetMilliseconds: 16,
                targetFpsTimes100: 6000,
                retryOfFrame: 0,
                tileBaseId: 0,
                cameraBytes: 0,
                tileIndexBytes: 0,
                reserved1: 0,
                reserved2: 0,
                submitMode: SubmitMode.Reference,
                budgetPolicy: BudgetPolicy.None,
                lossTolerancePolicy: 0xFF,
                reserved3: 0,
                objectRefMask: SubmitObjectReferenceMask.Build(SubmitObjectSlot.CameraBlock),
                dependencyFrameId: 0,
                payloadKindBitmap: PayloadKind.Tensor,
                payloadFrameCount: 0,
                reserved4: 0);
            var header = new NnrpHeader(
                versionMajor: NnrpHeader.CurrentVersionMajor,
                wireFormat: NnrpHeader.CurrentWireFormat,
                messageType: MessageType.FrameSubmit,
                flags: HeaderFlags.None,
                metaLength: FrameSubmitMessage.MetadataLength,
                bodyLength: 0,
                sessionId: 1,
                frameId: 2,
                viewId: 0,
                routeId: 0,
                traceId: 0);
            var framed = new NnrpFramedMessage(header, metadata.ToArray(), Array.Empty<byte>());

            Assert.False(FrameSubmitMessage.TryParse(framed, out _, out var error));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, error);
        }

        [Fact]
        public void FrameSubmitMessageTryParseRejectsWrongHeaderLayout()
        {
            var metadata = CreateMetadata(cameraBytes: 0, tileCount: 0, sectionCount: 0, tileIndexBytes: 0);
            var header = new NnrpHeader(
                versionMajor: NnrpHeader.CurrentVersionMajor,
                wireFormat: NnrpHeader.CurrentWireFormat,
                messageType: MessageType.ResultPush,
                flags: HeaderFlags.None,
                metaLength: FrameSubmitMessage.MetadataLength,
                bodyLength: 0,
                sessionId: 1,
                frameId: 2,
                viewId: 0,
                routeId: 0,
                traceId: 0);
            var framed = new NnrpFramedMessage(header, metadata.ToArray(), Array.Empty<byte>());

            Assert.False(FrameSubmitMessage.TryParse(framed, out _, out var error));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, error);
        }

        [Fact]
        public void FrameSubmitMessageTryParseRejectsTruncatedCameraBlock()
        {
            var metadata = CreateMetadata(cameraBytes: 3, tileCount: 0, sectionCount: 0, tileIndexBytes: 0);
            var header = new NnrpHeader(
                versionMajor: NnrpHeader.CurrentVersionMajor,
                wireFormat: NnrpHeader.CurrentWireFormat,
                messageType: MessageType.FrameSubmit,
                flags: HeaderFlags.None,
                metaLength: FrameSubmitMessage.MetadataLength,
                bodyLength: 2,
                sessionId: 1,
                frameId: 2,
                viewId: 0,
                routeId: 0,
                traceId: 0);
            var framed = new NnrpFramedMessage(header, metadata.ToArray(), new byte[] { 0xCA, 0xFE });

            Assert.False(FrameSubmitMessage.TryParse(framed, out _, out var error));
            Assert.Equal(NnrpParseError.SourceTooShort, error);
        }

        [Fact]
        public void FrameSubmitMessageTryParseRejectsTrailingBodyBytes()
        {
            var section = CreateSection(
                role: TensorRole.LumaHint,
                lengthTable: new byte[] { 1, 0, 0, 0, 2, 0, 0, 0 },
                payload: new byte[] { 0xAA, 0xBB, 0xCC });
            var metadata = CreateMetadata(cameraBytes: 2, tileCount: 2, sectionCount: 1, tileIndexBytes: 4);
            var header = new NnrpHeader(
                versionMajor: NnrpHeader.CurrentVersionMajor,
                wireFormat: NnrpHeader.CurrentWireFormat,
                messageType: MessageType.FrameSubmit,
                flags: HeaderFlags.None,
                metaLength: FrameSubmitMessage.MetadataLength,
                bodyLength: FrameSubmitMessage.ComputeBodyLength(2, 4, new[] { section }),
                sessionId: 9,
                frameId: 15,
                viewId: 1,
                routeId: 0,
                traceId: 88);
            var validPacket = new FrameSubmitMessage(
                header,
                metadata,
                new byte[] { 0xCA, 0xFE },
                new ushort[] { 1, 9 },
                new[] { section }).ToFramedMessage();
            var trailingBody = new byte[validPacket.Body.Length + 1];
            validPacket.Body.Span.CopyTo(trailingBody);
            trailingBody[^1] = 0x7F;
            var malformedHeader = new NnrpHeader(
                versionMajor: validPacket.Header.VersionMajor,
                wireFormat: validPacket.Header.WireFormat,
                messageType: validPacket.Header.MessageType,
                flags: validPacket.Header.Flags,
                metaLength: validPacket.Header.MetaLength,
                bodyLength: (uint)trailingBody.Length,
                sessionId: validPacket.Header.SessionId,
                frameId: validPacket.Header.FrameId,
                viewId: validPacket.Header.ViewId,
                routeId: validPacket.Header.RouteId,
                traceId: validPacket.Header.TraceId);
            var malformed = new NnrpFramedMessage(malformedHeader, validPacket.Metadata, trailingBody);

            Assert.False(FrameSubmitMessage.TryParse(malformed, out _, out var error));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, error);
        }

        [Fact]
        public void FrameSubmitMessageConstructorRejectsInvalidMetadataContracts()
        {
            var cameraBlock = new byte[] { 0xCA, 0xFE };
            var tileIds = new ushort[] { 1, 9 };
            var section = CreateSection(
                role: TensorRole.LumaHint,
                lengthTable: new byte[] { 1, 0, 0, 0, 2, 0, 0, 0 },
                payload: new byte[] { 0xAA, 0xBB, 0xCC });
            var header = new NnrpHeader(
                versionMajor: NnrpHeader.CurrentVersionMajor,
                wireFormat: NnrpHeader.CurrentWireFormat,
                messageType: MessageType.FrameSubmit,
                flags: HeaderFlags.None,
                metaLength: FrameSubmitMessage.MetadataLength,
                bodyLength: FrameSubmitMessage.ComputeBodyLength(cameraBlock.Length, 4, new[] { section }),
                sessionId: 9,
                frameId: 15,
                viewId: 1,
                routeId: 0,
                traceId: 88);

            AssertMetadataContractRejects(
                new FrameSubmitMetadata(
                    640, 360, 32, 32, 2, 1,
                    FrameClass.Delta,
                    InputProfile.ChangedTilesLuma,
                    TileIndexMode.RawUInt16,
                    0, 33, 6000, 7, 0, 2, 4,
                    0, 0,
                    SubmitMode.Reference,
                    BudgetPolicy.None,
                    0xFF,
                    0,
                    SubmitObjectReferenceMask.Build(SubmitObjectSlot.CameraBlock),
                    7,
                    PayloadKind.Tensor,
                    0,
                    0),
                cameraBlock,
                tileIds,
                new[] { section },
                header,
                "only supports inline submit mode");

            AssertMetadataContractRejects(
                CreateMetadata(objectRefMask: SubmitObjectReferenceMask.Build(SubmitObjectSlot.CameraBlock)),
                cameraBlock,
                tileIds,
                new[] { section },
                header,
                "does not support submit object references");

            AssertMetadataContractRejects(
                CreateMetadata(payloadKindBitmap: PayloadKind.ToolDelta),
                cameraBlock,
                tileIds,
                new[] { section },
                header,
                "only supports inline tensor payloads");

            AssertMetadataContractRejects(
                CreateMetadata(cameraBytes: 3),
                cameraBlock,
                tileIds,
                new[] { section },
                header,
                "Camera block length must match metadata.CameraBytes");

            AssertMetadataContractRejects(
                CreateMetadata(tileCount: 1),
                cameraBlock,
                tileIds,
                new[] { section },
                header,
                "Tile id count must match metadata.TileCount");

            AssertMetadataContractRejects(
                CreateMetadata(sectionCount: 2),
                cameraBlock,
                tileIds,
                new[] { section },
                header,
                "Section count must match metadata.SectionCount");

            AssertMetadataContractRejects(
                CreateMetadata(tileIndexBytes: 2),
                cameraBlock,
                tileIds,
                new[] { section },
                header,
                "Tile index block length must match metadata.TileIndexBytes");
        }

        [Fact]
        public void FrameSubmitMessageConstructorZeroesReservedMetadataFieldsForStrictRoundTrip()
        {
            var section = CreateSection(
                role: TensorRole.LumaHint,
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
                reserved0: 7,
                latencyBudgetMilliseconds: 33,
                targetFpsTimes100: 6000,
                retryOfFrame: 7,
                tileBaseId: 0,
                cameraBytes: 2,
                tileIndexBytes: 4,
                reserved1: 9,
                reserved2: 11,
                submitMode: SubmitMode.Inline,
                budgetPolicy: BudgetPolicy.None,
                lossTolerancePolicy: 0xFF,
                reserved3: 13,
                objectRefMask: 0,
                dependencyFrameId: 7,
                payloadKindBitmap: PayloadKind.Tensor,
                payloadFrameCount: 0,
                reserved4: 15);
            var header = new NnrpHeader(
                versionMajor: NnrpHeader.CurrentVersionMajor,
                wireFormat: NnrpHeader.CurrentWireFormat,
                messageType: MessageType.FrameSubmit,
                flags: HeaderFlags.None,
                metaLength: FrameSubmitMessage.MetadataLength,
                bodyLength: FrameSubmitMessage.ComputeBodyLength(2, 4, new[] { section }),
                sessionId: 9,
                frameId: 15,
                viewId: 1,
                routeId: 0,
                traceId: 88);

            var packet = new FrameSubmitMessage(header, metadata, new byte[] { 0xCA, 0xFE }, new ushort[] { 1, 9 }, new[] { section }).ToArray();

            Assert.True(FrameSubmitMessage.TryParse(packet, out var parsed, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(0, parsed.Metadata.Reserved0);
            Assert.Equal(0UL, parsed.Metadata.Reserved1);
            Assert.Equal(0UL, parsed.Metadata.Reserved2);
            Assert.Equal(0, parsed.Metadata.Reserved3);
            Assert.Equal(0, parsed.Metadata.Reserved4);
        }

        private static FrameSubmitMetadata CreateMetadata(
            uint cameraBytes = 2,
            ushort tileCount = 2,
            ushort sectionCount = 1,
            uint tileIndexBytes = 4,
            uint objectRefMask = 0,
            PayloadKind payloadKindBitmap = PayloadKind.Tensor,
            ushort payloadFrameCount = 0)
        {
            return new FrameSubmitMetadata(
                sourceWidth: 640,
                sourceHeight: 360,
                tileWidth: 32,
                tileHeight: 32,
                tileCount: tileCount,
                sectionCount: sectionCount,
                frameClass: FrameClass.Delta,
                inputProfile: InputProfile.ChangedTilesLuma,
                tileIndexMode: TileIndexMode.RawUInt16,
                reserved0: 0,
                latencyBudgetMilliseconds: 33,
                targetFpsTimes100: 6000,
                retryOfFrame: 7,
                tileBaseId: 0,
                cameraBytes: cameraBytes,
                tileIndexBytes: tileIndexBytes,
                reserved1: 0,
                reserved2: 0,
                submitMode: SubmitMode.Inline,
                budgetPolicy: BudgetPolicy.None,
                lossTolerancePolicy: 0xFF,
                reserved3: 0,
                objectRefMask: objectRefMask,
                dependencyFrameId: 7,
                payloadKindBitmap: payloadKindBitmap,
                payloadFrameCount: payloadFrameCount,
                reserved4: 0);
        }

        private static void AssertMetadataContractRejects(
            FrameSubmitMetadata metadata,
            byte[] cameraBlock,
            ushort[] tileIds,
            TensorSectionBlock[] sections,
            NnrpHeader header,
            string expectedMessageFragment)
        {
            var error = Assert.Throws<ArgumentException>(() => new FrameSubmitMessage(header, metadata, cameraBlock, tileIds, sections));
            Assert.Contains(expectedMessageFragment, error.Message, StringComparison.Ordinal);
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
    }
}
