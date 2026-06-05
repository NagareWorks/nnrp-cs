using System;

namespace Nnrp.Core
{
    public static class BodyCodec
    {
        public static byte[] PackInlineObjectRegion(params byte[][] blocks)
        {
            if (blocks == null || blocks.Length == 0)
            {
                return Array.Empty<byte>();
            }

            var totalBytes = 0;
            for (var index = 0; index < blocks.Length; index++)
            {
                var block = blocks[index];
                if (block == null || block.Length == 0)
                {
                    continue;
                }

                totalBytes = checked(totalBytes + block.Length);
            }

            if (totalBytes == 0)
            {
                return Array.Empty<byte>();
            }

            var payload = new byte[totalBytes];
            var cursor = 0;
            for (var index = 0; index < blocks.Length; index++)
            {
                var block = blocks[index];
                if (block == null || block.Length == 0)
                {
                    continue;
                }

                Buffer.BlockCopy(block, 0, payload, cursor, block.Length);
                cursor += block.Length;
            }

            return payload;
        }

        public static byte[] BuildInlineObjectBlock(CacheObjectKind objectKind, byte[] payload, ushort profileId = 0)
        {
            payload ??= Array.Empty<byte>();
            var header = new InlineObjectBlockHeader(
                objectKind,
                objectFlags: 0,
                profileId: profileId,
                reserved0: 0,
                objectBytes: checked((uint)payload.Length),
                reserved1: 0);

            var blockLength = checked((int)header.GetAlignedBlockLength());
            var block = new byte[blockLength];
            header.Write(block);
            Buffer.BlockCopy(payload, 0, block, InlineObjectBlockHeader.HeaderLength, payload.Length);
            return block;
        }

        public static bool TrySplitInlineObjectRegion(ReadOnlyMemory<byte> region, out InlineObjectBlockView[] blocks, out NnrpParseError error)
        {
            blocks = Array.Empty<InlineObjectBlockView>();
            error = NnrpParseError.None;
            if (region.Length == 0)
            {
                return true;
            }

            var parsedBlocks = new System.Collections.Generic.List<InlineObjectBlockView>();
            var cursor = 0;
            while (cursor < region.Length)
            {
                if (region.Length - cursor < InlineObjectBlockHeader.HeaderLength)
                {
                    error = NnrpParseError.InvalidMessageLayout;
                    return false;
                }

                if (!InlineObjectBlockHeader.TryParse(region.Slice(cursor, InlineObjectBlockHeader.HeaderLength).Span, strict: true, out var header, out error))
                {
                    return false;
                }

                var blockLength = checked((int)header.GetAlignedBlockLength());
                var end = checked(cursor + blockLength);
                if (end > region.Length)
                {
                    error = NnrpParseError.InvalidMessageLayout;
                    return false;
                }

                var payloadOffset = checked(cursor + InlineObjectBlockHeader.HeaderLength);
                var payloadLength = checked((int)header.ObjectBytes);
                var alignedPayloadEnd = checked(cursor + blockLength);
                if (!BinaryAlignment.ValidateZeroPadding(region.Slice(payloadOffset + payloadLength, alignedPayloadEnd - (payloadOffset + payloadLength)).Span))
                {
                    error = NnrpParseError.InvalidMessageLayout;
                    return false;
                }

                parsedBlocks.Add(new InlineObjectBlockView(header, region.Slice(payloadOffset, payloadLength)));
                cursor = end;
            }

            blocks = parsedBlocks.ToArray();
            return true;
        }

        public static byte[] PackObjectReferenceRegion(params ObjectReferenceBlock[] blocks)
        {
            if (blocks == null || blocks.Length == 0)
            {
                return Array.Empty<byte>();
            }

            var payload = new byte[checked(blocks.Length * ObjectReferenceBlock.BlockLength)];
            for (var index = 0; index < blocks.Length; index++)
            {
                blocks[index].Write(payload.AsSpan(index * ObjectReferenceBlock.BlockLength, ObjectReferenceBlock.BlockLength));
            }

            return payload;
        }

        public static ObjectReferenceBlock BuildObjectReferenceBlock(
            CacheObjectKind objectKind,
            uint cacheNamespace,
            uint cacheKeyHigh,
            uint cacheKeyLow)
        {
            return new ObjectReferenceBlock(
                objectKind,
                referenceFlags: 0,
                cacheNamespace: cacheNamespace,
                cacheKeyHigh: cacheKeyHigh,
                cacheKeyLow: cacheKeyLow);
        }

        public static bool TryParseObjectReferenceRegion(ReadOnlyMemory<byte> region, out ObjectReferenceRegionView view, out NnrpParseError error)
        {
            view = default;
            error = NnrpParseError.None;
            if (region.Length == 0)
            {
                view = new ObjectReferenceRegionView(Array.Empty<ObjectReferenceBlock>());
                return true;
            }

            if (region.Length % ObjectReferenceBlock.BlockLength != 0)
            {
                error = NnrpParseError.InvalidMessageLayout;
                return false;
            }

            var count = region.Length / ObjectReferenceBlock.BlockLength;
            var blocks = new ObjectReferenceBlock[count];
            for (var index = 0; index < count; index++)
            {
                if (!ObjectReferenceBlock.TryParse(
                        region.Slice(index * ObjectReferenceBlock.BlockLength, ObjectReferenceBlock.BlockLength).Span,
                        strict: true,
                        out blocks[index],
                        out error))
                {
                    return false;
                }
            }

            view = new ObjectReferenceRegionView(blocks);
            return true;
        }

        public static byte[] Pack(
            byte[]? inlineObjectRegion = null,
            byte[]? objectReferenceRegion = null,
            byte[]? typedPayloadDescriptorRegion = null,
            byte[]? typedPayloadFrameRegion = null,
            byte[]? extensionDescriptorRegion = null,
            byte[]? extensionPayloadRegion = null,
            uint bodyFlags = 0)
        {
            inlineObjectRegion ??= Array.Empty<byte>();
            objectReferenceRegion ??= Array.Empty<byte>();
            typedPayloadDescriptorRegion ??= Array.Empty<byte>();
            typedPayloadFrameRegion ??= Array.Empty<byte>();
            extensionDescriptorRegion ??= Array.Empty<byte>();
            extensionPayloadRegion ??= Array.Empty<byte>();

            var prelude = new BodyRegionPrelude(
                inlineObjectBytes: checked((uint)inlineObjectRegion.Length),
                objectReferenceBytes: checked((uint)objectReferenceRegion.Length),
                typedPayloadDescriptorBytes: checked((uint)typedPayloadDescriptorRegion.Length),
                typedPayloadFrameBytes: checked((uint)typedPayloadFrameRegion.Length),
                extensionDescriptorBytes: checked((uint)extensionDescriptorRegion.Length),
                extensionPayloadBytes: checked((uint)extensionPayloadRegion.Length),
                bodyFlags: bodyFlags);

            var body = new byte[checked((int)prelude.GetTotalBodyBytes())];
            prelude.Write(body);

            var cursor = BodyRegionPrelude.PreludeLength;
            CopyRegion(inlineObjectRegion, body, ref cursor);
            CopyRegion(objectReferenceRegion, body, ref cursor);
            CopyRegion(typedPayloadDescriptorRegion, body, ref cursor);
            CopyRegion(typedPayloadFrameRegion, body, ref cursor);
            CopyRegion(extensionDescriptorRegion, body, ref cursor);
            CopyRegion(extensionPayloadRegion, body, ref cursor);
            return body;
        }

        public static bool TryParse(ReadOnlyMemory<byte> body, out BodyView view, out NnrpParseError error)
        {
            view = default;
            error = NnrpParseError.None;
            if (!BodyRegionPrelude.TryParse(body.Span, strict: true, out var prelude, out error))
            {
                return false;
            }

            var expectedLength = checked((int)prelude.GetTotalBodyBytes());
            if (body.Length != expectedLength)
            {
                error = NnrpParseError.InvalidMessageLayout;
                return false;
            }

            if (prelude.TypedPayloadDescriptorBytes % TypedPayloadDescriptor.DescriptorLength != 0
                || prelude.ExtensionDescriptorBytes % ExtensionFrameDescriptor.DescriptorLength != 0)
            {
                error = NnrpParseError.InvalidMessageLayout;
                return false;
            }

            var cursor = BodyRegionPrelude.PreludeLength;
            var inlineObjectRegion = Slice(body, ref cursor, checked((int)prelude.InlineObjectBytes));
            var objectReferenceRegion = Slice(body, ref cursor, checked((int)prelude.ObjectReferenceBytes));
            var typedPayloadDescriptorRegion = Slice(body, ref cursor, checked((int)prelude.TypedPayloadDescriptorBytes));
            var typedPayloadFrameRegion = Slice(body, ref cursor, checked((int)prelude.TypedPayloadFrameBytes));
            var extensionDescriptorRegion = Slice(body, ref cursor, checked((int)prelude.ExtensionDescriptorBytes));
            var extensionPayloadRegion = Slice(body, ref cursor, checked((int)prelude.ExtensionPayloadBytes));

            view = new BodyView(
                prelude,
                inlineObjectRegion,
                objectReferenceRegion,
                typedPayloadDescriptorRegion,
                typedPayloadFrameRegion,
                extensionDescriptorRegion,
                extensionPayloadRegion);
            return true;
        }

        private static ReadOnlyMemory<byte> Slice(ReadOnlyMemory<byte> body, ref int cursor, int length)
        {
            var region = body.Slice(cursor, length);
            cursor += length;
            return region;
        }

        private static void CopyRegion(byte[] source, byte[] destination, ref int cursor)
        {
            if (source.Length == 0)
            {
                return;
            }

            Buffer.BlockCopy(source, 0, destination, cursor, source.Length);
            cursor += source.Length;
        }
    }
}
