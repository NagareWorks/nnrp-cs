using System;
using System.Buffers.Binary;
using System.Linq;
using Nnrp.Core;
using Nnrp.Runtime;
using Xunit;

namespace Nnrp.Transport.WebSocket.Tests
{
    public sealed class NnrpWebSocketFrameCodecTests
    {
        [Fact]
        public void EncodeAndDecodePreserveTheGoldenHeaderAndOwnPayloadRegions()
        {
            var metadata = new byte[] { 0xaa, 0xbb, 0xcc };
            var body = new byte[] { 0x10, 0x20, 0x30, 0x40 };
            var header = new RuntimeFrameHeader(
                MessageType.FrameSubmit,
                HeaderFlags.AckRequired | HeaderFlags.Keyframe,
                SessionId: 0x11223344,
                FrameId: 0x55667788,
                ViewId: 0x99aa,
                RouteId: 0xbbcc,
                TraceId: 0x0102030405060708);

            var encoded = NnrpWebSocketFrameCodec.Encode(header, metadata, body);
            Assert.Equal(
                "4e4e5250010010282100000003000000040000004433221188776655aa99ccbb0807060504030201aabbcc10203040",
                Convert.ToHexString(encoded).ToLowerInvariant());

            var decoded = NnrpWebSocketFrameCodec.Decode(encoded);
            metadata[0] = 0;
            body[0] = 0;
            encoded[NnrpHeader.HeaderLength] = 0;
            encoded[NnrpHeader.HeaderLength + 3] = 0;

            Assert.Equal(header, decoded.Header);
            Assert.Equal(new byte[] { 0xaa, 0xbb, 0xcc }, decoded.Metadata.ToArray());
            Assert.Equal(new byte[] { 0x10, 0x20, 0x30, 0x40 }, decoded.Body.ToArray());
        }

        [Fact]
        public void DecodedRuntimeFrameExposesOnlyTheFrozenProjection()
        {
            var properties = typeof(DecodedRuntimeFrame)
                .GetProperties()
                .Select(property => (property.Name, property.PropertyType))
                .OrderBy(property => property.Name, StringComparer.Ordinal)
                .ToArray();

            Assert.Equal(
                new[]
                {
                    (nameof(DecodedRuntimeFrame.Body), typeof(ReadOnlyMemory<byte>)),
                    (nameof(DecodedRuntimeFrame.Header), typeof(RuntimeFrameHeader)),
                    (nameof(DecodedRuntimeFrame.Metadata), typeof(ReadOnlyMemory<byte>)),
                },
                properties);
            Assert.Empty(typeof(DecodedRuntimeFrame).GetConstructors());
        }

        [Fact]
        public void DecodeRejectsMalformedSingleFrames()
        {
            var encoded = NnrpWebSocketFrameCodec.Encode(
                new RuntimeFrameHeader(MessageType.Ping),
                new byte[] { 1 },
                new byte[] { 2 });

            Assert.Throws<FormatException>(() => NnrpWebSocketFrameCodec.Decode(encoded.Take(39).ToArray()));

            var lengthMismatch = encoded.ToArray();
            BinaryPrimitives.WriteUInt32LittleEndian(lengthMismatch.AsSpan(12, 4), 2);
            Assert.Throws<FormatException>(() => NnrpWebSocketFrameCodec.Decode(lengthMismatch));

            var reservedFlags = encoded.ToArray();
            reservedFlags[8] = 0x40;
            Assert.Throws<FormatException>(() => NnrpWebSocketFrameCodec.Decode(reservedFlags));

            var trailing = encoded.Concat(new byte[] { 0 }).ToArray();
            Assert.Throws<FormatException>(() => NnrpWebSocketFrameCodec.Decode(trailing));
        }

        [Fact]
        public void EncodeRejectsReservedHeaderValues()
        {
            Assert.Throws<ArgumentException>(() => NnrpWebSocketFrameCodec.Encode(
                new RuntimeFrameHeader(MessageType.Ping, VersionMajor: 2)));
            Assert.Throws<ArgumentException>(() => NnrpWebSocketFrameCodec.Encode(
                new RuntimeFrameHeader(MessageType.Ping, WireFormat: 1)));
            Assert.Throws<ArgumentException>(() => NnrpWebSocketFrameCodec.Encode(
                new RuntimeFrameHeader((MessageType)0xff)));
            Assert.Throws<ArgumentException>(() => NnrpWebSocketFrameCodec.Encode(
                new RuntimeFrameHeader(MessageType.Ping, (HeaderFlags)0x40)));
        }

        [Fact]
        public void DecodeBatchEnforcesTheFrozenFrameLimit()
        {
            var first = NnrpWebSocketFrameCodec.Encode(new RuntimeFrameHeader(MessageType.Ping));
            var second = NnrpWebSocketFrameCodec.Encode(new RuntimeFrameHeader(MessageType.Pong));
            var batch = first.Concat(second).ToArray();

            Assert.Empty(NnrpWebSocketFrameCodec.DecodeBatch(Array.Empty<byte>()));
            Assert.Single(NnrpWebSocketFrameCodec.DecodeBatch(first, limit: 1));
            Assert.Equal(2, NnrpWebSocketFrameCodec.DecodeBatch(batch).Count);
            Assert.Equal(2, NnrpWebSocketFrameCodec.DecodeBatch(batch, limit: 2).Count);
            Assert.Throws<FormatException>(() => NnrpWebSocketFrameCodec.DecodeBatch(batch, limit: 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => NnrpWebSocketFrameCodec.DecodeBatch(batch, limit: -1));
            Assert.Throws<FormatException>(() => NnrpWebSocketFrameCodec.DecodeBatch(batch.Take(batch.Length - 1).ToArray()));
        }
    }
}
