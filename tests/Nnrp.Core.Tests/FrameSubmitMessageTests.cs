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

            // Aligned body length: tensor_submit_block→camera→pad→tiles→pad→s0→pad→s1
            var bodyLength = BinaryAlignment.AlignUp(TensorSubmitBlock.BlockLength + cameraBlock.Length, 8)
                + tileIndexBytes;
            bodyLength = BinaryAlignment.AlignUp(bodyLength, 8)
                + section0.TotalLength;
            bodyLength = BinaryAlignment.AlignUp(bodyLength, 8)
                + section1.TotalLength;

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
                bodyLength: (uint)(BinaryAlignment.AlignUp(TensorSubmitBlock.BlockLength + 2, 8) + 4 + (BinaryAlignment.AlignUp(BinaryAlignment.AlignUp(TensorSubmitBlock.BlockLength + 2, 8) + 4, 8) - (BinaryAlignment.AlignUp(TensorSubmitBlock.BlockLength + 2, 8) + 4)) + section.TotalLength),
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
                bodyLength: (uint)(TensorSubmitBlock.BlockLength + section.TotalLength),
                sessionId: 1,
                frameId: 2,
                viewId: 0,
                routeId: 0,
                traceId: 3);
            var packet = new FrameSubmitMessage(header, metadata, Array.Empty<byte>(), new ushort[] { 5 }, new[] { section }).ToArray();
            packet[NnrpHeader.HeaderLength + FrameSubmitMessage.MetadataLength + 10] = 2;

            Assert.False(FrameSubmitMessage.TryParse(packet, out _, out var error));
            Assert.Equal(NnrpParseError.SourceTooShort, error);
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
                bodyLength: (uint)(BinaryAlignment.AlignUp(TensorSubmitBlock.BlockLength + 2, 8) + 4 + (BinaryAlignment.AlignUp(BinaryAlignment.AlignUp(TensorSubmitBlock.BlockLength + 2, 8) + 4, 8) - (BinaryAlignment.AlignUp(TensorSubmitBlock.BlockLength + 2, 8) + 4)) + section.TotalLength),
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

            packet[NnrpHeader.HeaderLength + FrameSubmitMessage.MetadataLength + TensorSubmitBlock.BlockLength + 2] = 0x7F;

            Assert.False(FrameSubmitMessage.TryParse(packet, out _, out var error));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, error);
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
