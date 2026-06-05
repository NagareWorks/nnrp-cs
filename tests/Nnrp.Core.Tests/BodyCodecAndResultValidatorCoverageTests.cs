using System;
using Xunit;

namespace Nnrp.Core.Tests
{
    public sealed class BodyCodecAndResultValidatorCoverageTests
    {
        [Fact]
        public void BodyCodecHandlesEmptyPackedAndMalformedRegions()
        {
            Assert.Empty(BodyCodec.PackInlineObjectRegion(null!));
            Assert.Empty(BodyCodec.PackInlineObjectRegion(Array.Empty<byte>(), null!));

            var inlineBlock = BodyCodec.BuildInlineObjectBlock(CacheObjectKind.TileIndexBlock, new byte[] { 1, 2, 3 }, profileId: 7);
            var inlineRegion = BodyCodec.PackInlineObjectRegion(inlineBlock);
            Assert.True(BodyCodec.TrySplitInlineObjectRegion(inlineRegion, out var inlineBlocks, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Single(inlineBlocks);
            Assert.Equal(CacheObjectKind.TileIndexBlock, inlineBlocks[0].Header.ObjectKind);
            Assert.Equal((ushort)7, inlineBlocks[0].Header.ProfileId);
            Assert.Equal(new byte[] { 1, 2, 3 }, inlineBlocks[0].Payload.ToArray());

            var badInlineRegion = (byte[])inlineRegion.Clone();
            badInlineRegion[InlineObjectBlockHeader.HeaderLength + 3] = 0x7F;
            Assert.False(BodyCodec.TrySplitInlineObjectRegion(badInlineRegion, out _, out error));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, error);

            Assert.False(BodyCodec.TrySplitInlineObjectRegion(new byte[InlineObjectBlockHeader.HeaderLength - 1], out _, out error));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, error);

            var truncatedInlineRegion = inlineRegion.AsMemory(0, inlineRegion.Length - 1);
            Assert.False(BodyCodec.TrySplitInlineObjectRegion(truncatedInlineRegion, out _, out error));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, error);

            Assert.Empty(BodyCodec.PackObjectReferenceRegion(null!));
            var reference = BodyCodec.BuildObjectReferenceBlock(CacheObjectKind.TensorSectionTable, 2, 3, 4);
            var referenceRegion = BodyCodec.PackObjectReferenceRegion(reference);
            Assert.True(BodyCodec.TryParseObjectReferenceRegion(referenceRegion, out var referenceView, out error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Single(referenceView.Blocks);
            Assert.Equal(reference, referenceView.Blocks[0]);

            Assert.False(BodyCodec.TryParseObjectReferenceRegion(new byte[ObjectReferenceBlock.BlockLength - 1], out _, out error));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, error);

            var badReferenceRegion = (byte[])referenceRegion.Clone();
            badReferenceRegion[2] = 1;
            Assert.False(BodyCodec.TryParseObjectReferenceRegion(badReferenceRegion, out _, out error));
            Assert.Equal(NnrpParseError.NonZeroReservedField, error);
        }

        [Fact]
        public void BodyCodecPacksAndRejectsMalformedCompositeBodies()
        {
            var inlineRegion = BodyCodec.PackInlineObjectRegion(
                BodyCodec.BuildInlineObjectBlock(CacheObjectKind.TileIndexBlock, new byte[] { 1, 0 }));
            var referenceRegion = BodyCodec.PackObjectReferenceRegion(
                BodyCodec.BuildObjectReferenceBlock(CacheObjectKind.TensorSectionTable, 1, 2, 3));
            var descriptor = new TypedPayloadDescriptor(
                PayloadKind.ToolDelta,
                descriptorFlags: 0,
                profileId: 4,
                payloadOffset: 0,
                payloadLength: 2,
                reserved: 0);
            var extension = new ExtensionFrameDescriptor(
                extensionKind: 8,
                extensionFlags: 0,
                profileId: 0,
                reserved0: 0,
                payloadOffset: 0,
                payloadLength: 2);

            var body = BodyCodec.Pack(
                inlineObjectRegion: inlineRegion,
                objectReferenceRegion: referenceRegion,
                typedPayloadDescriptorRegion: descriptor.ToArray(),
                typedPayloadFrameRegion: new byte[] { 9, 10 },
                extensionDescriptorRegion: extension.ToArray(),
                extensionPayloadRegion: new byte[] { 11, 12 },
                bodyFlags: 3);

            Assert.True(BodyCodec.TryParse(body, out var view, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal((uint)3, view.Prelude.BodyFlags);
            Assert.Equal(inlineRegion, view.InlineObjectRegion.ToArray());
            Assert.Equal(referenceRegion, view.ObjectReferenceRegion.ToArray());
            Assert.Equal(new byte[] { 9, 10 }, view.TypedPayloadFrameRegion.ToArray());
            Assert.Equal(new byte[] { 11, 12 }, view.ExtensionPayloadRegion.ToArray());

            var truncatedBody = body.AsMemory(0, body.Length - 1);
            Assert.False(BodyCodec.TryParse(truncatedBody, out _, out error));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, error);

            var badTypedDescriptorBody = BodyCodec.Pack(typedPayloadDescriptorRegion: new byte[] { 1 });
            Assert.False(BodyCodec.TryParse(badTypedDescriptorBody, out _, out error));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, error);

            var badExtensionDescriptorBody = BodyCodec.Pack(extensionDescriptorRegion: new byte[] { 1 });
            Assert.False(BodyCodec.TryParse(badExtensionDescriptorBody, out _, out error));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, error);

            var reservedPreludeBody = BodyCodec.Pack();
            reservedPreludeBody[28] = 1;
            Assert.False(BodyCodec.TryParse(reservedPreludeBody, out _, out error));
            Assert.Equal(NnrpParseError.NonZeroReservedField, error);
        }

        [Fact]
        public void ResultPushBodyValidatorAcceptsInlineTensorObjects()
        {
            var tileIds = new ushort[] { 20, 21 };
            var tileIndexPayload = TileIndexBlockCodec.Encode(tileIds, TileIndexMode.RawUInt16, tileBaseId: 20);
            var section = CreateSection(TensorRole.SrResidual, new byte[] { 1, 0, 0, 0, 1, 0, 0, 0 }, new byte[] { 0x10, 0x20 });
            var body = BodyCodec.Pack(
                inlineObjectRegion: BodyCodec.PackInlineObjectRegion(
                    BodyCodec.BuildInlineObjectBlock(CacheObjectKind.TileIndexBlock, tileIndexPayload),
                    BodyCodec.BuildInlineObjectBlock(CacheObjectKind.TensorSectionTable, section.ToArray())));
            Assert.True(BodyCodec.TryParse(body, out var view, out var bodyError));
            Assert.Equal(NnrpParseError.None, bodyError);

            var metadata = ResultMetadata(sectionCount: 1, tileCount: 2, tileBaseId: 20, tileIndexBytes: (uint)tileIndexPayload.Length);
            Assert.True(ResultPushBodyValidator.TryValidate(
                metadata,
                view,
                out var inlineBlocks,
                out var referenceBlocks,
                out var typedDescriptors,
                out var extensionDescriptors,
                out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(2, inlineBlocks.Length);
            Assert.Empty(referenceBlocks);
            Assert.Empty(typedDescriptors);
            Assert.Empty(extensionDescriptors);
        }

        [Fact]
        public void ResultPushBodyValidatorRejectsInvalidObjectContracts()
        {
            AssertInvalid(
                ResultMetadata(sectionCount: 0, tileCount: 0, tileBaseId: 0, tileIndexBytes: 0),
                BodyCodec.Pack(objectReferenceRegion: BodyCodec.PackObjectReferenceRegion(
                    BodyCodec.BuildObjectReferenceBlock(CacheObjectKind.TensorSectionTable, 1, 10, 10),
                    BodyCodec.BuildObjectReferenceBlock(CacheObjectKind.TileIndexBlock, 1, 9, 9))));

            AssertInvalid(
                ResultMetadata(sectionCount: 0, tileCount: 2, tileBaseId: 0, tileIndexBytes: 4),
                BodyCodec.Pack(objectReferenceRegion: BodyCodec.PackObjectReferenceRegion(
                    BodyCodec.BuildObjectReferenceBlock(CacheObjectKind.TileIndexBlock, 1, 1, 1),
                    BodyCodec.BuildObjectReferenceBlock(CacheObjectKind.TileIndexBlock, 1, 2, 2))));

            AssertInvalid(
                ResultMetadata(sectionCount: 2, tileCount: 0, tileBaseId: 0, tileIndexBytes: 0),
                BodyCodec.Pack(objectReferenceRegion: BodyCodec.PackObjectReferenceRegion(
                    BodyCodec.BuildObjectReferenceBlock(CacheObjectKind.TensorSectionTable, 1, 1, 1),
                    BodyCodec.BuildObjectReferenceBlock(CacheObjectKind.TensorSectionTable, 1, 2, 2))));

            var tileIndexPayload = TileIndexBlockCodec.Encode(new ushort[] { 0, 1 }, TileIndexMode.RawUInt16, tileBaseId: 0);
            AssertInvalid(
                ResultMetadata(sectionCount: 0, tileCount: 2, tileBaseId: 0, tileIndexBytes: 4),
                BodyCodec.Pack(
                    inlineObjectRegion: BodyCodec.PackInlineObjectRegion(
                        BodyCodec.BuildInlineObjectBlock(CacheObjectKind.TileIndexBlock, tileIndexPayload)),
                    objectReferenceRegion: BodyCodec.PackObjectReferenceRegion(
                        BodyCodec.BuildObjectReferenceBlock(CacheObjectKind.TileIndexBlock, 1, 1, 1))));

            AssertInvalid(
                ResultMetadata(sectionCount: 0, tileCount: 2, tileBaseId: 0, tileIndexBytes: 4),
                BodyCodec.Pack(inlineObjectRegion: BodyCodec.PackInlineObjectRegion(
                    BodyCodec.BuildInlineObjectBlock(CacheObjectKind.TileIndexBlock, tileIndexPayload),
                    BodyCodec.BuildInlineObjectBlock(CacheObjectKind.TileIndexBlock, tileIndexPayload))));

            AssertInvalid(
                ResultMetadata(sectionCount: 0, tileCount: 2, tileBaseId: 0, tileIndexBytes: 8),
                BodyCodec.Pack(inlineObjectRegion: BodyCodec.PackInlineObjectRegion(
                    BodyCodec.BuildInlineObjectBlock(CacheObjectKind.TileIndexBlock, tileIndexPayload))));

            AssertInvalid(
                ResultMetadata(sectionCount: 0, tileCount: 2, tileBaseId: 0, tileIndexBytes: 3),
                BodyCodec.Pack(inlineObjectRegion: BodyCodec.PackInlineObjectRegion(
                    BodyCodec.BuildInlineObjectBlock(CacheObjectKind.TileIndexBlock, new byte[] { 0, 1, 2 }))));

            AssertInvalid(
                ResultMetadata(sectionCount: 0, tileCount: 2, tileBaseId: 0, tileIndexBytes: 4),
                BodyCodec.Pack());

            AssertInvalid(
                ResultMetadata(sectionCount: 1, tileCount: 0, tileBaseId: 0, tileIndexBytes: 0),
                BodyCodec.Pack());
        }

        [Fact]
        public void ResultPushBodyValidatorRejectsInvalidInlineSectionTables()
        {
            var first = CreateSection(TensorRole.SrResidual, Array.Empty<byte>(), new byte[] { 1 });
            var second = CreateSection(TensorRole.LumaHint, Array.Empty<byte>(), new byte[] { 2 });
            var invalidOrder = Concat(first.ToArray(), second.ToArray());

            AssertInvalid(
                ResultMetadata(sectionCount: 2, tileCount: 0, tileBaseId: 0, tileIndexBytes: 0),
                BodyCodec.Pack(inlineObjectRegion: BodyCodec.PackInlineObjectRegion(
                    BodyCodec.BuildInlineObjectBlock(CacheObjectKind.TensorSectionTable, invalidOrder))),
                NnrpParseError.InconsistentSectionDescriptor);

            var trailingByte = Concat(first.ToArray(), new byte[] { 0 });
            AssertInvalid(
                ResultMetadata(sectionCount: 1, tileCount: 0, tileBaseId: 0, tileIndexBytes: 0),
                BodyCodec.Pack(inlineObjectRegion: BodyCodec.PackInlineObjectRegion(
                    BodyCodec.BuildInlineObjectBlock(CacheObjectKind.TensorSectionTable, trailingByte))),
                NnrpParseError.InconsistentSectionDescriptor);
        }

        private static void AssertInvalid(ResultPushMetadata metadata, byte[] body, NnrpParseError expected = NnrpParseError.InvalidMessageLayout)
        {
            Assert.True(BodyCodec.TryParse(body, out var view, out var bodyError));
            Assert.Equal(NnrpParseError.None, bodyError);

            Assert.False(ResultPushBodyValidator.TryValidate(
                metadata,
                view,
                out _,
                out _,
                out _,
                out _,
                out var error));
            Assert.Equal(expected, error);
        }

        private static ResultPushMetadata ResultMetadata(ushort sectionCount, ushort tileCount, uint tileBaseId, uint tileIndexBytes)
        {
            return new ResultPushMetadata(
                statusCode: ResultStatusCode.Success,
                resultFlags: ResultFlags.None,
                sectionCount: sectionCount,
                tileCount: tileCount,
                activeProfileId: 1,
                inferenceMilliseconds: 1,
                queueMilliseconds: 1,
                serverTotalMilliseconds: 2,
                tileBaseId: tileBaseId,
                tileIndexBytes: tileIndexBytes);
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

        private static byte[] Concat(params byte[][] buffers)
        {
            var length = 0;
            foreach (var buffer in buffers)
            {
                length += buffer.Length;
            }

            var output = new byte[length];
            var offset = 0;
            foreach (var buffer in buffers)
            {
                Buffer.BlockCopy(buffer, 0, output, offset, buffer.Length);
                offset += buffer.Length;
            }

            return output;
        }
    }
}
