using System;
using Nnrp.Core;
using Xunit;

namespace Nnrp.Core.Tests
{
    public sealed class CrossLanguageGoldenVectorTests
    {
        private const string HeaderGoldenHex = "4e4e525001001028210000003000000000100000070000000b0000000200000015cd5b0700000000";
        private const string FrameSubmitPacketGoldenHex = "4e4e525001001028010000004800000064000000070000002a000000030063000000000000000000800268012000200002000200000200003200701700000000050000000300000000000000000000000000000000000000000000000000ff000000000000000000010000000000000063616d00000000000100000500000000000000000000000008000000020000000000000000000000020000000000000061610000000000000500000500000000000000000000000008000000040000000000000000000000030000000100000078797a71";

        private const string ResultPushPacketGoldenHex = "4e4e5250010012280000000020000000720000002c0000005b000000070054007b000000000000000000050000000100110002001300000014000000520000000500000000000000020002000100000000000000040000000500060000000000640001050000010000000000020000000800000003000000000000000000000001000100000002000000414243000000650000050000000000000000000000000800000002000000000000000000000001000000010000007a7a";

        [Fact]
        public void PythonHeaderGoldenVectorParsesAndWritesBack()
        {
            var payload = Convert.FromHexString(HeaderGoldenHex);

            Assert.True(NnrpHeader.TryParse(payload, NnrpHeaderParseOptions.Strict, out var header, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(NnrpHeader.CurrentVersionMajor, header.VersionMajor);
            Assert.Equal(NnrpHeader.CurrentWireFormat, header.WireFormat);
            Assert.Equal(MessageType.FrameSubmit, header.MessageType);
            Assert.Equal(HeaderFlags.AckRequired | HeaderFlags.Keyframe, header.Flags);
            Assert.Equal(48u, header.MetaLength);
            Assert.Equal(4096u, header.BodyLength);
            Assert.Equal(7u, header.SessionId);
            Assert.Equal(11u, header.FrameId);
            Assert.Equal(2, header.ViewId);
            Assert.Equal(0, header.RouteId);
            Assert.Equal(123456789UL, header.TraceId);
            Assert.Equal(payload, header.ToArray());
        }

        [Fact]
        public void PythonFrameSubmitGoldenPacketParsesThroughImplementedPrimitives()
        {
            var packet = Convert.FromHexString(FrameSubmitPacketGoldenHex);

            Assert.True(NnrpHeader.TryParse(packet, NnrpHeaderParseOptions.Strict, out var header, out var headerError));
            Assert.Equal(NnrpParseError.None, headerError);
            Assert.Equal(MessageType.FrameSubmit, header.MessageType);
            Assert.Equal(FrameSubmitMessage.MetadataLength, (int)header.MetaLength);
            Assert.Equal(100u, header.BodyLength);
            Assert.Equal(packet[..NnrpHeader.HeaderLength], header.ToArray());

            Assert.True(FrameSubmitMessage.TryParse(packet, out var message, out var metadataError));
            Assert.Equal(NnrpParseError.None, metadataError);
            Assert.Equal(InputProfile.DenseLumaFrame, message.Metadata.InputProfile);
            Assert.Equal(PayloadKind.Tensor, message.Metadata.PayloadKindBitmap);
            Assert.Equal(FrameClass.Keyframe, message.Metadata.FrameClass);
            Assert.Equal(640, message.Metadata.SourceWidth);
            Assert.Equal(360, message.Metadata.SourceHeight);
            Assert.Equal(32, message.Metadata.TileWidth);
            Assert.Equal(32, message.Metadata.TileHeight);
            Assert.Equal((ushort)2, message.Metadata.TileCount);
            Assert.Equal((ushort)2, message.Metadata.SectionCount);
            Assert.Equal(50, message.Metadata.LatencyBudgetMilliseconds);
            Assert.Equal(6000, message.Metadata.TargetFpsTimes100);
            Assert.Equal(0u, message.Metadata.DependencyFrameId);

            var firstSectionBodyOffset = BinaryAlignment.AlignUp(
                BinaryAlignment.AlignUp((int)message.Metadata.CameraBytes, 8)
                + (int)message.Metadata.TileIndexBytes,
                8);
            var firstDescriptorOffset = NnrpHeader.HeaderLength + FrameSubmitMessage.MetadataLength + firstSectionBodyOffset;
            var firstDescriptor = ParseDescriptor(packet, firstDescriptorOffset);
            Assert.Equal(TensorRole.LumaHint, firstDescriptor.Role);
            Assert.Equal(CodecId.Raw, firstDescriptor.Codec);
            Assert.Equal(DTypeId.UInt8, firstDescriptor.DType);
            Assert.Equal(8u, firstDescriptor.LengthTableBytes);
            Assert.Equal(2u, firstDescriptor.PayloadBytes);

            var secondSectionBodyOffset = BinaryAlignment.AlignUp(
                firstSectionBodyOffset
                + TensorSectionDescriptor.DescriptorLength
                + (int)firstDescriptor.CodecTableBytes
                + (int)firstDescriptor.LengthTableBytes
                + (int)firstDescriptor.PayloadBytes,
                8);
            var secondDescriptorOffset = NnrpHeader.HeaderLength + FrameSubmitMessage.MetadataLength + secondSectionBodyOffset;
            var secondDescriptor = ParseDescriptor(packet, secondDescriptorOffset);
            Assert.Equal(TensorRole.RoughMetal, secondDescriptor.Role);
            Assert.Equal(CodecId.Raw, secondDescriptor.Codec);
            Assert.Equal(DTypeId.UInt8, secondDescriptor.DType);
            Assert.Equal(0, secondDescriptor.Flags);
            Assert.Equal(0u, secondDescriptor.CodecTableBytes);
            Assert.Equal(8u, secondDescriptor.LengthTableBytes);
            Assert.Equal(4u, secondDescriptor.PayloadBytes);
        }

        [Fact]
        public void PythonResultPushGoldenPacketParsesThroughImplementedPrimitives()
        {
            var packet = BuildCurrentResultPushPacket();

            Assert.True(NnrpHeader.TryParse(packet, NnrpHeaderParseOptions.Strict, out var header, out var headerError));
            Assert.Equal(NnrpParseError.None, headerError);
            Assert.Equal(MessageType.ResultPush, header.MessageType);
            Assert.Equal(ResultPushMetadata.MetadataLength, (int)header.MetaLength);
            Assert.Equal(98u, header.BodyLength);
            Assert.Equal(packet[..NnrpHeader.HeaderLength], header.ToArray());

            var metadataBytes = packet.AsSpan(NnrpHeader.HeaderLength, ResultPushMetadata.MetadataLength);
            Assert.True(ResultPushMetadata.TryParse(metadataBytes, strict: true, out var metadata, out var metadataError));
            Assert.Equal(NnrpParseError.None, metadataError);
            Assert.Equal(ResultStatusCode.Success, metadata.StatusCode);
            Assert.Equal(ResultFlags.Stale | ResultFlags.Partial, metadata.ResultFlags);
            Assert.Equal(0, metadata.ActiveProfileId);
            Assert.Equal(17, metadata.InferenceMilliseconds);
            Assert.Equal(2, metadata.QueueMilliseconds);
            Assert.Equal(19, metadata.ServerTotalMilliseconds);
            Assert.Equal((ushort)2, metadata.SectionCount);
            Assert.Equal((ushort)2, metadata.TileCount);
            Assert.Equal(4u, metadata.TileIndexBytes);
            Assert.Equal(metadataBytes.ToArray(), metadata.ToArray());

            Assert.True(ResultPushMessage.TryParse(packet, out var message, out var messageError));
            Assert.Equal(NnrpParseError.None, messageError);
            Assert.Equal(TileIndexMode.RawUInt16, message.TileIndexMode);
            Assert.Equal(new ushort[] { 5, 6 }, message.TileIds.ToArray());
            Assert.Equal(2, message.Sections.Length);

            var firstDescriptor = message.Sections.Span[0].Descriptor;
            Assert.Equal(100, (ushort)firstDescriptor.Role);
            Assert.Equal(CodecId.Lz4, firstDescriptor.Codec);
            Assert.Equal(DTypeId.UInt8, firstDescriptor.DType);
            Assert.Equal(1, firstDescriptor.Flags);
            Assert.Equal(2u, firstDescriptor.CodecTableBytes);
            Assert.Equal(8u, firstDescriptor.LengthTableBytes);
            Assert.Equal(3u, firstDescriptor.PayloadBytes);

            var secondDescriptor = message.Sections.Span[1].Descriptor;
            Assert.Equal(101, (ushort)secondDescriptor.Role);
            Assert.Equal(CodecId.Raw, secondDescriptor.Codec);
            Assert.Equal(DTypeId.UInt8, secondDescriptor.DType);
            Assert.Equal(8u, secondDescriptor.LengthTableBytes);
            Assert.Equal(2u, secondDescriptor.PayloadBytes);
        }

        private static byte[] BuildCurrentResultPushPacket()
        {
            var section0 = new TensorSectionBlock(
                new TensorSectionDescriptor(
                    role: (TensorRole)100,
                    codec: CodecId.Lz4,
                    dtype: DTypeId.UInt8,
                    layout: TensorLayoutId.Nhwc,
                    scalePolicy: ScalePolicy.None,
                    flags: 1,
                    elementCountPerTile: 0,
                    codecTableBytes: 2,
                    lengthTableBytes: 8,
                    payloadBytes: 3,
                    payloadStrideBytes: 0),
                new byte[] { 0x01, 0x00 },
                new byte[] { 1, 0, 0, 0, 2, 0, 0, 0 },
                new byte[] { 0x41, 0x42, 0x43 });
            var section1 = new TensorSectionBlock(
                new TensorSectionDescriptor(
                    role: (TensorRole)101,
                    codec: CodecId.Raw,
                    dtype: DTypeId.UInt8,
                    layout: TensorLayoutId.Nhwc,
                    scalePolicy: ScalePolicy.None,
                    flags: 0,
                    elementCountPerTile: 0,
                    codecTableBytes: 0,
                    lengthTableBytes: 8,
                    payloadBytes: 2,
                    payloadStrideBytes: 0),
                Array.Empty<byte>(),
                new byte[] { 1, 0, 0, 0, 1, 0, 0, 0 },
                new byte[] { 0x7A, 0x7A });
            var tileIds = new ushort[] { 5, 6 };
            var tileIndexBytes = TileIndexBlockCodec.GetEncodedLength(tileIds, TileIndexMode.RawUInt16);
            var metadata = new ResultPushMetadata(
                statusCode: ResultStatusCode.Success,
                resultFlags: ResultFlags.Stale | ResultFlags.Partial,
                sectionCount: 2,
                tileCount: 2,
                activeProfileId: 0,
                inferenceMilliseconds: 17,
                queueMilliseconds: 2,
                serverTotalMilliseconds: 19,
                tileBaseId: 0,
                tileIndexBytes: (uint)tileIndexBytes,
                resultClass: ResultClass.Partial,
                coveredTileCount: 1,
                droppedTileCount: 1);
            var bodyLength = BinaryAlignment.AlignUp(TensorResultBlock.BlockLength + tileIndexBytes, 8) + section0.TotalLength;
            bodyLength = BinaryAlignment.AlignUp(bodyLength, 8) + section1.TotalLength;
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
            return new ResultPushMessage(header, metadata, tileIds, new[] { section0, section1 }).ToArray();
        }

        private static TensorSectionDescriptor ParseDescriptor(byte[] packet, int offset)
        {
            Assert.True(
                TensorSectionDescriptor.TryParse(
                    packet.AsSpan(offset, TensorSectionDescriptor.DescriptorLength),
                    strict: true,
                    out var descriptor,
                    out var error));
            Assert.Equal(NnrpParseError.None, error);
            return descriptor;
        }

    }
}
