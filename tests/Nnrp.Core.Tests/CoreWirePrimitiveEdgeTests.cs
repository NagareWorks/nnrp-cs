using System;
using Nnrp.Core;
using Xunit;

namespace Nnrp.Core.Tests
{
    public sealed class CoreWirePrimitiveEdgeTests
    {
        [Fact]
        public void AlignmentHelpersRejectInvalidArgumentsAndOverflow()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => BinaryAlignment.IsAligned(8, alignment: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => BinaryAlignment.GetPadding(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => BinaryAlignment.GetPadding(1, alignment: 0));
            Assert.Equal(0, BinaryAlignment.GetPadding(16));
            Assert.False(BinaryAlignment.TryAlignUp(-1, out _));
            Assert.False(BinaryAlignment.TryAlignUp(1, out _, alignment: 0));
            Assert.False(BinaryAlignment.TryAlignUp(int.MaxValue - 2, out _));
        }

        [Fact]
        public void CheckedArithmeticCoversIntegerSizes()
        {
            Assert.True(CheckedArithmetic.TryAdd(1U, 2U, out var uintResult));
            Assert.Equal(3U, uintResult);
            Assert.False(CheckedArithmetic.TryAdd(uint.MaxValue, 1U, out _));

            Assert.True(CheckedArithmetic.TryAdd(1UL, 2UL, out var ulongResult));
            Assert.Equal(3UL, ulongResult);
            Assert.False(CheckedArithmetic.TryAdd(ulong.MaxValue, 1UL, out _));
        }

        [Fact]
        public void FixedBinaryReaderReportsShortReadsAndSkipFailures()
        {
            var emptyReader = new FixedBinaryReader(Array.Empty<byte>());
            Assert.False(emptyReader.TryReadByte(out var byteValue));
            Assert.Equal(0, byteValue);

            var oneByteReader = new FixedBinaryReader(new byte[] { 1 });
            Assert.False(oneByteReader.TryReadUInt16(out var ushortValue));
            Assert.Equal(0, ushortValue);

            var threeByteReader = new FixedBinaryReader(new byte[] { 1, 2, 3 });
            Assert.False(threeByteReader.TryReadUInt32(out var uintValue));
            Assert.Equal(0U, uintValue);

            var sevenByteReader = new FixedBinaryReader(new byte[] { 1, 2, 3, 4, 5, 6, 7 });
            Assert.False(sevenByteReader.TryReadUInt64(out var ulongValue));
            Assert.Equal(0UL, ulongValue);

            var skipReader = new FixedBinaryReader(new byte[] { 1, 2, 3 });
            Assert.True(skipReader.TrySkip(2));
            Assert.Equal(2, skipReader.Offset);
            Assert.Equal(1, skipReader.Remaining);
            Assert.False(skipReader.TrySkip(2));
            Assert.False(skipReader.TrySkip(-1));
        }

        [Fact]
        public void FixedBinaryWriterReportsShortWritesAndClearsZeroes()
        {
            var emptyWriter = new FixedBinaryWriter(Array.Empty<byte>());
            Assert.False(emptyWriter.TryWriteByte(1));

            var oneByteWriter = new FixedBinaryWriter(new byte[1]);
            Assert.False(oneByteWriter.TryWriteUInt16(1));

            var threeByteWriter = new FixedBinaryWriter(new byte[3]);
            Assert.False(threeByteWriter.TryWriteUInt32(1));

            var sevenByteWriter = new FixedBinaryWriter(new byte[7]);
            Assert.False(sevenByteWriter.TryWriteUInt64(1));

            var buffer = new byte[] { 1, 1, 1 };
            var zeroWriter = new FixedBinaryWriter(buffer);
            Assert.True(zeroWriter.TryWriteZeroes(2));
            Assert.Equal(2, zeroWriter.Offset);
            Assert.Equal(new byte[] { 0, 0, 1 }, buffer);
            Assert.False(zeroWriter.TryWriteZeroes(2));
            Assert.False(zeroWriter.TryWriteZeroes(-1));
        }

        [Fact]
        public void HeaderCoversParseFailuresAndEqualityHelpers()
        {
            var header = CreateHeader();

            Assert.Throws<ArgumentException>(() => header.Write(new byte[NnrpHeader.HeaderLength - 1]));

            var invalidWriteHeader = CreateHeader(headerLength: 24);
            Assert.False(invalidWriteHeader.TryWrite(new byte[NnrpHeader.HeaderLength], out var bytesWritten));
            Assert.Equal(0, bytesWritten);
            Assert.Throws<InvalidOperationException>(() => invalidWriteHeader.Write(new byte[NnrpHeader.HeaderLength]));

            Assert.True(NnrpHeader.TryParse(header.ToArray(), out var parsed));
            Assert.Equal(header, parsed);

            Assert.False(NnrpHeader.TryParse(Array.Empty<byte>(), NnrpHeaderParseOptions.Strict, out _, out var error));
            Assert.Equal(NnrpParseError.SourceTooShort, error);

            var payload = header.ToArray();
            payload[0] = (byte)'X';
            Assert.False(NnrpHeader.TryParse(payload, NnrpHeaderParseOptions.Strict, out _, out error));
            Assert.Equal(NnrpParseError.InvalidMagic, error);

            payload = header.ToArray();
            payload[7] = 24;
            Assert.False(NnrpHeader.TryParse(payload, NnrpHeaderParseOptions.Strict, out _, out error));
            Assert.Equal(NnrpParseError.InvalidHeaderLength, error);

            payload = header.ToArray();
            payload[5] = 0x7F;
            Assert.False(NnrpHeader.TryParse(payload, NnrpHeaderParseOptions.Strict, out _, out error));
            Assert.Equal(NnrpParseError.UnknownWireFormat, error);

            Assert.False(header.Equals(CreateHeader(frameId: 99)));
            Assert.False(header.Equals("header"));
            _ = header.GetHashCode();
        }

        [Fact]
        public void HeaderParseOptionsCompareByValue()
        {
            var first = new NnrpHeaderParseOptions(strict: true, maxMessageLength: 64);
            var second = new NnrpHeaderParseOptions(strict: true, maxMessageLength: 64);
            var third = NnrpHeaderParseOptions.Default;

            Assert.True(first.Equals(second));
            Assert.True(first.Equals((object)second));
            Assert.False(first.Equals(third));
            Assert.False(first.Equals("options"));
            Assert.NotEqual(third.GetHashCode(), first.GetHashCode());
            Assert.False(NnrpHeaderParseOptions.Strict.Equals(NnrpHeaderParseOptions.Default));
        }

        [Fact]
        public void FrameSubmitMetadataCoversShortBuffersAndEqualityHelpers()
        {
            var metadata = CreateFrameSubmitMetadata();
            var message = CreateFrameSubmitMessage(metadata);

            Assert.True(FrameSubmitMessage.TryParse(message.ToArray(), out var parsed, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(metadata.SourceWidth, parsed.Metadata.SourceWidth);
            Assert.Equal(metadata.SourceHeight, parsed.Metadata.SourceHeight);
            Assert.Equal(metadata.TileCount, parsed.Metadata.TileCount);
            Assert.Equal(metadata.CameraBytes, parsed.Metadata.CameraBytes);
            Assert.Equal(metadata.InputProfile, parsed.Metadata.InputProfile);
            Assert.False(FrameSubmitMessage.TryParse(Array.Empty<byte>(), out _, out error));
            Assert.Equal(NnrpParseError.SourceTooShort, error);
            Assert.False(metadata.Equals(CreateFrameSubmitMetadata(cameraBytes: 32)));
            Assert.False(metadata.Equals("metadata"));
            _ = metadata.GetHashCode();
        }

        [Fact]
        public void ResultPushMetadataCoversShortBuffersAndEqualityHelpers()
        {
            var metadata = CreateResultPushMetadata();

            Assert.Throws<ArgumentException>(() => metadata.Write(new byte[ResultPushMetadata.MetadataLength - 1]));
            Assert.False(metadata.TryWrite(new byte[ResultPushMetadata.MetadataLength - 1], out var bytesWritten));
            Assert.Equal(0, bytesWritten);
            Assert.True(ResultPushMetadata.TryParse(metadata.ToArray(), out var parsed));
            Assert.Equal(metadata, parsed);
            Assert.False(ResultPushMetadata.TryParse(Array.Empty<byte>(), strict: true, out _, out var error));
            Assert.Equal(NnrpParseError.SourceTooShort, error);
            Assert.False(metadata.Equals(CreateResultPushMetadata(payloadDataBytes: 2)));
            Assert.False(metadata.Equals("metadata"));
            _ = metadata.GetHashCode();
        }

        [Fact]
        public void TensorSectionDescriptorCoversShortBuffersReservedFieldsAndLayoutBranches()
        {
            var descriptor = CreateTensorSectionDescriptor();

            Assert.True(descriptor.IsFixedStride);
            Assert.Throws<ArgumentException>(() => descriptor.Write(new byte[TensorSectionDescriptor.DescriptorLength - 1]));
            Assert.False(descriptor.TryWrite(new byte[TensorSectionDescriptor.DescriptorLength - 1], out var bytesWritten));
            Assert.Equal(0, bytesWritten);
            Assert.True(TensorSectionDescriptor.TryParse(descriptor.ToArray(), out var parsed));
            Assert.Equal(descriptor, parsed);
            Assert.False(TensorSectionDescriptor.TryParse(Array.Empty<byte>(), strict: true, out _, out var error));
            Assert.Equal(NnrpParseError.SourceTooShort, error);

            var reservedPayload = descriptor.ToArray();
            reservedPayload[TensorSectionDescriptor.DescriptorLength - 1] = 1;
            Assert.False(TensorSectionDescriptor.TryParse(reservedPayload, strict: true, out _, out error));
            Assert.Equal(NnrpParseError.NonZeroReservedField, error);

            var emptyDescriptor = CreateTensorSectionDescriptor(payloadBytes: 0, payloadStrideBytes: 0);
            Assert.False(emptyDescriptor.IsFixedStride);
            Assert.True(TensorSectionDescriptor.TryParse(emptyDescriptor.ToArray(), strict: true, out _, out error));
            Assert.Equal(NnrpParseError.None, error);

            var variableLengthDescriptor = CreateTensorSectionDescriptor(lengthTableBytes: 8, payloadBytes: 64, payloadStrideBytes: 0);
            Assert.False(variableLengthDescriptor.IsFixedStride);
            Assert.True(TensorSectionDescriptor.TryParse(variableLengthDescriptor.ToArray(), strict: true, out _, out error));
            Assert.Equal(NnrpParseError.None, error);

            Assert.False(descriptor.Equals(CreateTensorSectionDescriptor(payloadBytes: 128)));
            Assert.False(descriptor.Equals("descriptor"));
            _ = descriptor.GetHashCode();
        }

        private static NnrpHeader CreateHeader(byte headerLength = NnrpHeader.HeaderLength, uint frameId = 1)
        {
            return new NnrpHeader(
                NnrpHeader.CurrentVersionMajor,
                NnrpHeader.CurrentWireFormat,
                MessageType.FrameSubmit,
                HeaderFlags.AckRequired,
                FrameSubmitMessage.MetadataLength,
                128,
                42,
                frameId,
                1,
                0,
                0x0102030405060708UL,
                headerLength);
        }

        private static FrameSubmitMetadata CreateFrameSubmitMetadata(ushort tileCount = 1, uint cameraBytes = 64)
        {
            return new FrameSubmitMetadata(
                sourceWidth: 640,
                sourceHeight: 360,
                tileWidth: 32,
                tileHeight: 32,
                tileCount: tileCount,
                sectionCount: 0,
                frameClass: FrameClass.Keyframe,
                inputProfile: InputProfile.DenseLumaFrame,
                tileIndexMode: TileIndexMode.DenseRange,
                latencyBudgetMilliseconds: 100,
                targetFpsTimes100: 6000,
                retryOfFrame: 0,
                tileBaseId: 0,
                cameraBytes: cameraBytes,
                tileIndexBytes: 0);
        }

        private static FrameSubmitMessage CreateFrameSubmitMessage(FrameSubmitMetadata metadata)
        {
            var bodyLength = BinaryAlignment.AlignUp((int)metadata.CameraBytes, BinaryAlignment.DefaultAlignment);
            var header = new NnrpHeader(
                versionMajor: NnrpHeader.CurrentVersionMajor,
                wireFormat: NnrpHeader.CurrentWireFormat,
                messageType: MessageType.FrameSubmit,
                flags: HeaderFlags.None,
                metaLength: FrameSubmitMessage.MetadataLength,
                bodyLength: (uint)bodyLength,
                sessionId: 42,
                frameId: 1,
                viewId: 1,
                routeId: 0,
                traceId: 0x0102030405060708UL);
            return new FrameSubmitMessage(
                header,
                metadata,
                new byte[metadata.CameraBytes],
                new ushort[metadata.TileCount],
                Array.Empty<TensorSectionBlock>());
        }

        private static ResultPushMetadata CreateResultPushMetadata(uint payloadDataBytes = 64)
        {
            return new ResultPushMetadata(
                ResultStatusCode.Success,
                ResultFlags.None,
                sectionCount: 1,
                tileCount: 2,
                activeProfileId: 1,
                inferenceMilliseconds: 10,
                queueMilliseconds: 2,
                serverTotalMilliseconds: 14,
            tileBaseId: payloadDataBytes,
                tileIndexBytes: 0,
                coveredTileCount: 2);
        }

        private static TensorSectionDescriptor CreateTensorSectionDescriptor(
            uint lengthTableBytes = 0,
            uint payloadBytes = 64,
            uint payloadStrideBytes = 64)
        {
            return new TensorSectionDescriptor(
                TensorRole.LumaHint,
                CodecId.Raw,
                DTypeId.UInt8,
                TensorLayoutId.Nhwc,
                ScalePolicy.None,
                flags: 0,
                elementCountPerTile: 64,
                codecTableBytes: 0,
                lengthTableBytes: lengthTableBytes,
                payloadBytes: payloadBytes,
                payloadStrideBytes: payloadStrideBytes);
        }
    }
}
