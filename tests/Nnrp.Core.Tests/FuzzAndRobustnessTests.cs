using System;
using Nnrp.Core;
using Xunit;

namespace Nnrp.Core.Tests
{
    public sealed class FuzzAndRobustnessTests
    {
        [Fact]
        public void HeaderRejectsTruncatedPayloads()
        {
            for (var length = 0; length < NnrpHeader.HeaderLength; length++)
            {
                var truncated = new byte[length];
                Assert.False(NnrpHeader.TryParse(truncated, NnrpHeaderParseOptions.Strict, out _, out var error));
                Assert.Equal(NnrpParseError.SourceTooShort, error);
            }
        }

        [Fact]
        public void HeaderRejectsOversizedPayloadWithoutCrash()
        {
            var oversized = new byte[NnrpHeader.HeaderLength + 64 * 1024];
            oversized[0] = (byte)'N';
            oversized[1] = (byte)'N';
            oversized[2] = (byte)'R';
            oversized[3] = (byte)'P';
            oversized[4] = NnrpHeader.CurrentVersionMajor;
            oversized[5] = NnrpHeader.CurrentWireFormat;
            oversized[7] = NnrpHeader.HeaderLength;
            Assert.True(NnrpHeader.TryParse(oversized, NnrpHeaderParseOptions.Default, out _, out _));
        }

        [Fact]
        public void HeaderRejectsMalformedMagicBytes()
        {
            var header = CreateValidHeaderBytes();
            header[0] = 0x00;
            Assert.False(NnrpHeader.TryParse(header, NnrpHeaderParseOptions.Strict, out _, out var error));
            Assert.Equal(NnrpParseError.InvalidMagic, error);

            header[3] = 0xFF;
            Assert.False(NnrpHeader.TryParse(header, NnrpHeaderParseOptions.Strict, out _, out error));
            Assert.Equal(NnrpParseError.InvalidMagic, error);
        }

        [Fact]
        public void HeaderRejectsMalformedEnumValues()
        {
            var header = CreateValidHeaderBytes();

            // Invalid version_major
            var mutated = (byte[])header.Clone();
            mutated[4] = 99;
            Assert.False(NnrpHeader.TryParse(mutated, NnrpHeaderParseOptions.Strict, out _, out var error));
            Assert.Equal(NnrpParseError.UnsupportedVersion, error);

            // Invalid version_stage
            mutated = (byte[])header.Clone();
            mutated[5] = 99;
            Assert.False(NnrpHeader.TryParse(mutated, NnrpHeaderParseOptions.Strict, out _, out error));
            Assert.Equal(NnrpParseError.UnknownWireFormat, error);

            // Invalid msg_type
            mutated = (byte[])header.Clone();
            mutated[6] = 99;
            Assert.False(NnrpHeader.TryParse(mutated, NnrpHeaderParseOptions.Strict, out _, out error));
            Assert.Equal(NnrpParseError.UnknownMessageType, error);
        }

        [Fact]
        public void HeaderRejectsReservedFlagsInStrictMode()
        {
            var header = CreateValidHeaderBytes();
            header[9] = 0x80; // set an undefined high flag bit
            Assert.False(NnrpHeader.TryParse(header, NnrpHeaderParseOptions.Strict, out _, out var error));
            Assert.Equal(NnrpParseError.ReservedFlagsSet, error);
        }

        [Fact]
        public void ZeroTraceIdParsesWithoutError()
        {
            var header = CreateValidHeaderBytes();
            // The trace_id is in bytes 28-35; setting to zero should parse fine
            Array.Clear(header, 28, 8);
            Assert.True(NnrpHeader.TryParse(header, NnrpHeaderParseOptions.Strict, out var parsed, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(0UL, parsed.TraceId);
        }

        [Fact]
        public void MetadataRejectsTruncatedInputs()
        {
            var header = CreateValidHeaderBytes();

            // Truncated FrameSubmitMessage metadata block
            var shortMetadata = new byte[NnrpHeader.HeaderLength + FrameSubmitMessage.MetadataLength - 1];
            Array.Copy(header, shortMetadata, NnrpHeader.HeaderLength);
            Assert.False(NnrpFramedMessage.TryParse(shortMetadata, NnrpHeaderParseOptions.Strict, out _, out var error));
            Assert.NotEqual(NnrpParseError.None, error);

            // Truncated ResultPushMetadata
            shortMetadata = new byte[NnrpHeader.HeaderLength + ResultPushMetadata.MetadataLength - 1];
            Array.Copy(header, shortMetadata, NnrpHeader.HeaderLength);
            shortMetadata[6] = (byte)MessageType.ResultPush;
            Assert.False(NnrpFramedMessage.TryParse(shortMetadata, NnrpHeaderParseOptions.Strict, out _, out error));
            Assert.NotEqual(NnrpParseError.None, error);
        }

        [Fact]
        public void TensorSectionDescriptorRejectsNonZeroReservedBytesInStrictMode()
        {
            var descriptor = new byte[TensorSectionDescriptor.DescriptorLength];
            Assert.True(TensorSectionDescriptor.TryParse(descriptor, strict: false, out _, out _));

            // Bytes 28-31 are reserved; setting one of them should fail strict parse.
            descriptor[30] = 0xFF;
            Assert.False(TensorSectionDescriptor.TryParse(descriptor, strict: true, out _, out var error));
            Assert.Equal(NnrpParseError.NonZeroReservedField, error);
        }

        [Fact]
        public void TileIndexCodecRejectsTruncatedEncodedBlocks()
        {
            var tileIds = new ushort[] { 1, 2, 3, 4, 5 };
            var encoded = TileIndexBlockCodec.Encode(tileIds, TileIndexMode.RawUInt16, 0);
            var output = new ushort[5];

            // Truncate by 1 byte
            var truncated = encoded.AsSpan(0, encoded.Length - 1);
            Assert.False(TileIndexBlockCodec.TryDecode(truncated, TileIndexMode.RawUInt16, 5, output, out _, out _, 0));
        }

        [Fact]
        public void OffsetOverflowProducesGracefulError()
        {
            var badHeader = CreateValidHeaderBytes();
            // Set meta_len to near-uint-max so meta_len + body_len overflows
            BitConverter.TryWriteBytes(badHeader.AsSpan(12), uint.MaxValue - 1);
            BitConverter.TryWriteBytes(badHeader.AsSpan(16), 2u);

            Assert.False(NnrpHeader.TryParse(badHeader, NnrpHeaderParseOptions.Strict, out _, out var error));
            Assert.NotEqual(NnrpParseError.None, error);
        }

        [Fact]
        public void ParserNeverThrowsOnRandomUntrustedInput()
        {
            var random = new Random(42);
            for (var iteration = 0; iteration < 100; iteration++)
            {
                var length = random.Next(0, 128);
                var bytes = new byte[length];
                random.NextBytes(bytes);

                // Header parse should never throw
                NnrpHeader.TryParse(bytes, NnrpHeaderParseOptions.Strict, out _, out _);
                NnrpHeader.TryParse(bytes, NnrpHeaderParseOptions.Default, out _, out _);
                NnrpFramedMessage.TryParse(bytes, NnrpHeaderParseOptions.Strict, out _, out _);
            }
        }

        private static byte[] CreateValidHeaderBytes()
        {
            return new NnrpHeader(
                versionMajor: NnrpHeader.CurrentVersionMajor,
                wireFormat: NnrpHeader.CurrentWireFormat,
                messageType: MessageType.FrameSubmit,
                flags: HeaderFlags.None,
                metaLength: FrameSubmitMessage.MetadataLength,
                bodyLength: 128,
                sessionId: 1,
                frameId: 2,
                viewId: 0,
                routeId: 0,
                traceId: 0).ToArray();
        }
    }
}
