using System;
using System.Buffers.Binary;
using Nnrp.Core;
using Xunit;

namespace Nnrp.Core.Tests
{
    public sealed class PythonGoldenInteropTests
    {
        private const string HeaderGoldenHex = "4e4e525001001028210000003000000000100000070000000b0000000200000015cd5b0700000000";
        private const string ClientHelloMetadataGoldenHex =
            "0101010001000000010000000300000003000000210000000300000001000700"
            + "0100020040000000000001007017640002000000000000006000000000000000";
        private const string SessionPatchMetadataGoldenHex = "1D0000005D00000028230000680105000300000000000000050000000000000010000000";
        private const string SessionPatchAckMetadataGoldenHex =
            "0100030011000000440000000000000002000000282300006801050003000000"
            + "00000000010000000300000010000000";
        private const string CachePutMetadataGoldenHex = "01000000040302010807060501000000983a0000000800000300000003000000";
        private const string CacheAckMetadataGoldenHex = "01000000040302010807060500000000983a00000020000000000000";
        private const string CacheInvalidateMetadataGoldenHex = "0000000001000000040302010807060502000000";
        private const string FrameSubmitPacketGoldenHex = "4e4e525001001028010000002000000084000000070000002a0000000300630000000000000000000200010000000000320070170000000023000000500000000600000000000000800268012000200002000200000000000500000003000000000000000000000063616d00000000000100000500000000000000000000000008000000020000000000000000000000020000000000000061610000000000000500000500000000000000000000000008000000040000000000000000000000030000000100000078797a71";
        private const string ResultPushPacketGoldenHex = "4e4e5250010012280000000020000000720000002c0000005b000000070054007b000000000000000000050000000100110002001300000014000000520000000500000000000000020002000100000000000000040000000500060000000000640001050000010000000000020000000800000003000000000000000000000001000100000002000000414243000000650000050000000000000000000000000800000002000000000000000000000001000000010000007a7a";

        [Fact]
        public void HeaderMatchesPythonGoldenHex()
        {
            var bytes = HexToBytes(HeaderGoldenHex);

            Assert.True(NnrpHeader.TryParse(bytes, NnrpHeaderParseOptions.Default, out var header, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(MessageType.FrameSubmit, header.MessageType);
            Assert.Equal(HeaderFlags.AckRequired | HeaderFlags.Keyframe, header.Flags);
            Assert.Equal(48u, header.MetaLength);
            Assert.Equal(4096u, header.BodyLength);
            Assert.Equal(bytes, header.ToArray());
        }

        [Fact]
        public void ClientHelloMetadataMatchesPythonGoldenHex()
        {
            AssertMetadataRoundTrip(
                ClientHelloMetadataGoldenHex,
                (ReadOnlySpan<byte> source, out ClientHelloMetadata metadata, out NnrpParseError error) => ClientHelloMetadata.TryParse(source, out metadata, out error),
                metadata => metadata.ToArray(),
                metadata =>
                {
                    Assert.Equal(2u, metadata.MaxLaneCount);
                    Assert.Equal(96u, metadata.AuthBytes);
                    Assert.Equal(6000u, metadata.TargetCadenceX100);
                });
        }

        [Fact]
        public void SessionPatchMetadataMatchesPythonGoldenHex()
        {
            AssertMetadataRoundTrip(
                SessionPatchMetadataGoldenHex,
                (ReadOnlySpan<byte> source, out SessionPatchMetadata metadata, out NnrpParseError error) => SessionPatchMetadata.TryParse(source, strict: false, out metadata, out error),
                metadata => metadata.ToArray(),
                metadata =>
                {
                    Assert.Equal(29, metadata.ProfileId);
                    Assert.Equal(SessionPatchField.TargetCadence | SessionPatchField.DegradePolicy | SessionPatchField.ActiveLaneMask | SessionPatchField.PreferredCodec | SessionPatchField.ProfilePatch, metadata.PatchMask);
                    Assert.Equal(9000u, metadata.TargetFpsTimes100);
                    Assert.Equal(3u, metadata.ActiveViewMaskLow);
                    Assert.Equal(16u, metadata.ProfilePatchBytes);
                });
        }

        [Fact]
        public void SessionPatchAckMetadataMatchesPythonGoldenHex()
        {
            AssertMetadataRoundTrip(
                SessionPatchAckMetadataGoldenHex,
                (ReadOnlySpan<byte> source, out SessionPatchAckMetadata metadata, out NnrpParseError error) => SessionPatchAckMetadata.TryParse(source, out metadata, out error),
                metadata => metadata.ToArray(),
                metadata =>
                {
                    Assert.Equal(SessionPatchAckStatus.PartiallyApplied, metadata.AckStatus);
                    Assert.Equal(SessionPatchRejectReason.UnsupportedStrategy, metadata.RejectReason);
                    Assert.Equal(2u, metadata.EffectiveProfileId);
                    Assert.Equal(9000u, metadata.TargetFpsTimes100);
                    Assert.Equal(16u, metadata.ProfilePatchAckBytes);
                });
        }

        [Fact]
        public void CachePutMetadataMatchesPythonGoldenHex()
        {
            AssertMetadataRoundTrip(
                CachePutMetadataGoldenHex,
                (ReadOnlySpan<byte> source, out CachePutMetadata metadata, out NnrpParseError error) => CachePutMetadata.TryParse(source, out metadata, out error),
                metadata => metadata.ToArray(),
                metadata =>
                {
                    Assert.Equal(CacheObjectKind.CameraBlock, metadata.ObjectKind);
                    Assert.Equal(15000u, metadata.TtlMilliseconds);
                    Assert.Equal(2048u, metadata.ObjectBytes);
                });
        }

        [Fact]
        public void CacheAckMetadataMatchesPythonGoldenHex()
        {
            AssertMetadataRoundTrip(
                CacheAckMetadataGoldenHex,
                (ReadOnlySpan<byte> source, out CacheAckMetadata metadata, out NnrpParseError error) => CacheAckMetadata.TryParse(source, out metadata, out error),
                metadata => metadata.ToArray(),
                metadata =>
                {
                    Assert.Equal(CacheAckStatus.Accepted, metadata.Status);
                    Assert.Equal(8192u, metadata.MaxObjectBytes);
                    Assert.Equal(0u, metadata.DetailCode);
                });
        }

        [Fact]
        public void CacheInvalidateMetadataMatchesPythonGoldenHex()
        {
            AssertMetadataRoundTrip(
                CacheInvalidateMetadataGoldenHex,
                (ReadOnlySpan<byte> source, out CacheInvalidateMetadata metadata, out NnrpParseError error) => CacheInvalidateMetadata.TryParse(source, out metadata, out error),
                metadata => metadata.ToArray(),
                metadata =>
                {
                    Assert.Equal(CacheInvalidateScope.Session, metadata.InvalidateScope);
                    Assert.Equal(1u, metadata.CacheNamespace);
                    Assert.Equal(0x05060708u, metadata.CacheKeyLow);
                });
        }

        [Fact]
        public void FrameSubmitPacketMatchesPythonGoldenPacket()
        {
            var bytes = HexToBytes(FrameSubmitPacketGoldenHex);

            Assert.True(FrameSubmitMessage.TryParse(bytes, out var message, out var error), $"Parse error: {error}");
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(HeaderFlags.AckRequired, message.Header.Flags);
            Assert.Equal(InputProfile.DenseLumaFrame, message.Metadata.InputProfile);
            Assert.Equal(TileIndexMode.DenseRange, message.Metadata.TileIndexMode);
            Assert.Equal(new byte[] { (byte)'c', (byte)'a', (byte)'m' }, message.CameraBlock.ToArray());
            Assert.Equal(new ushort[] { 5, 6 }, message.TileIds.ToArray());
            Assert.Equal(2, message.Sections.Length);
            Assert.Equal((TensorRole)1, message.Sections.Span[0].Descriptor.Role);
            Assert.Equal((TensorRole)5, message.Sections.Span[1].Descriptor.Role);
            Assert.Equal(new uint[] { 2, 0 }, ReadLengthTable(message.Sections.Span[0].LengthTable.Span));
            Assert.Equal(new uint[] { 3, 1 }, ReadLengthTable(message.Sections.Span[1].LengthTable.Span));
            Assert.Equal(bytes, message.ToArray());
        }

        [Fact]
        public void ResultPushPacketMatchesPythonGoldenPacket()
        {
            var bytes = BuildCurrentResultPushPacket();

            Assert.True(ResultPushMessage.TryParse(bytes, out var message, out var error), $"Parse error: {error}");
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(ResultFlags.Stale | ResultFlags.Partial, message.Metadata.ResultFlags);
            Assert.Equal(TileIndexMode.RawUInt16, message.TileIndexMode);
            Assert.Equal(new ushort[] { 5, 6 }, message.TileIds.ToArray());
            Assert.Equal(2, message.Sections.Length);
            Assert.Equal((TensorRole)100, message.Sections.Span[0].Descriptor.Role);
            Assert.Equal((TensorRole)101, message.Sections.Span[1].Descriptor.Role);
            Assert.Equal(new uint[] { 1, 2 }, ReadLengthTable(message.Sections.Span[0].LengthTable.Span));
            Assert.Equal(new uint[] { 1, 1 }, ReadLengthTable(message.Sections.Span[1].LengthTable.Span));
            Assert.Equal(bytes, message.ToArray());
        }

        private static byte[] BuildCurrentResultPushPacket()
        {
            var section0 = new TensorSectionBlock(
                new TensorSectionDescriptor(
                    role: (TensorRole)100,
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
                new byte[] { 1, 0, 0, 0, 2, 0, 0, 0 },
                new byte[] { 0x41, 0x42 });
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

        private delegate bool TryParseMetadata<T>(ReadOnlySpan<byte> source, out T metadata, out NnrpParseError error);

        private static void AssertMetadataRoundTrip<T>(
            string expectedHex,
            TryParseMetadata<T> parser,
            Func<T, byte[]> serializer,
            Action<T> assertParsed)
            where T : struct
        {
            var bytes = HexToBytes(expectedHex);

            Assert.True(parser(bytes, out var metadata, out var error));
            Assert.Equal(NnrpParseError.None, error);
            assertParsed(metadata);
            Assert.Equal(bytes, serializer(metadata));
        }

        private static byte[] HexToBytes(string hex)
        {
            var bytes = new byte[hex.Length / 2];
            for (var index = 0; index < bytes.Length; index++)
            {
                bytes[index] = Convert.ToByte(hex.Substring(index * 2, 2), 16);
            }

            return bytes;
        }

        private static uint[] ReadLengthTable(ReadOnlySpan<byte> lengthTable)
        {
            var result = new uint[lengthTable.Length / sizeof(uint)];
            for (var index = 0; index < result.Length; index++)
            {
                result[index] = BinaryPrimitives.ReadUInt32LittleEndian(lengthTable.Slice(index * sizeof(uint), sizeof(uint)));
            }

            return result;
        }
    }
}
