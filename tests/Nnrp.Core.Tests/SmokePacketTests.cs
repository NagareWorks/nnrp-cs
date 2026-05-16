using Nnrp.Core;
using Xunit;

namespace Nnrp.Core.Tests
{
    public sealed class SmokePacketTests
    {
        [Fact]
        public void BuildSmokeFrameSubmitPacketProducesParseableSubmit()
        {
            var packetBytes = SmokePackets.CreateSmokeFrameSubmitMessage(sessionId: 77, frameId: 303, viewId: 1, traceId: 9).ToArray();

            Assert.True(FrameSubmitMessage.TryParse(packetBytes, out var message, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(77u, message.Header.SessionId);
            Assert.Equal(303u, message.Header.FrameId);
            Assert.Equal(1, message.Header.ViewId);
            Assert.Equal(new ushort[] { 0, 1, 2 }, message.TileIds.ToArray());
            Assert.Single(message.Sections.ToArray());
            Assert.Equal(5, (ushort)message.Sections.Span[0].Descriptor.Role);
            Assert.Equal(32u * 32u * 3u, message.Sections.Span[0].Descriptor.PayloadBytes);
        }

        [Fact]
        public void DescribeResultPacketSummarizesResultPush()
        {
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
                    lengthTableBytes: 8,
                    payloadBytes: 2,
                    payloadStrideBytes: 0),
                System.Array.Empty<byte>(),
                new byte[] { 1, 0, 0, 0, 1, 0, 0, 0 },
                new byte[] { 0xAA, 0xBB });
            var packetBytes = new ResultPushMessage(
                new NnrpHeader(1, NnrpHeader.CurrentWireFormat, MessageType.ResultPush, HeaderFlags.None, ResultPushMetadata.MetadataLength, (uint)(TensorResultBlock.BlockLength + section.TotalLength), 41, 303, 0, 0, 0),
                new ResultPushMetadata(ResultStatusCode.Success, ResultFlags.None, 1, 2, 0, 3, 1, 4, 5, 0),
                new ushort[] { 5, 6 },
                new[] { section }).ToArray();

            var summary = SmokePackets.DescribeResultPacket(packetBytes);

            Assert.Equal("ResultPush", summary.MessageType);
            Assert.Equal(303u, summary.FrameId);
            Assert.Equal(41u, summary.SessionId);
            Assert.Equal(2, summary.TileCount);
        }

        [Fact]
        public void BuildSmokeFrameSubmitPacketProducesFramedSubmit()
        {
            var packetBytes = SmokePackets.BuildSmokeFrameSubmitPacket(sessionId: 77, frameId: 303, viewId: 1, traceId: 9);

            Assert.True(NnrpFramedMessage.TryParse(packetBytes, NnrpHeaderParseOptions.Strict, out var framed, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(NnrpHeader.CurrentWireFormat, framed.Header.WireFormat);
            Assert.Equal(MessageType.FrameSubmit, framed.Header.MessageType);
            Assert.Equal((ushort)FrameSubmitMetadata.MetadataLength, framed.Header.MetaLength);
            Assert.True(FrameSubmitMetadata.TryParse(framed.Metadata.Span, strict: true, out var metadata, out error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(SubmitMode.Inline, metadata.SubmitMode);
            Assert.Equal(PayloadKind.Tensor, metadata.PayloadKindBitmap);
            Assert.Equal(77u, framed.Header.SessionId);
            Assert.Equal(303u, framed.Header.FrameId);
            Assert.Equal((ushort)1, framed.Header.ViewId);
        }
    }
}
