using System;

namespace Nnrp.Core
{
    public static class ResultPushBodyValidator
    {
        public static bool TryValidate(
            ResultPushMetadata metadata,
            BodyView bodyView,
            out InlineObjectBlockView[] inlineBlocks,
            out ObjectReferenceBlock[] objectReferenceBlocks,
            out TypedPayloadDescriptor[] typedPayloadDescriptors,
            out ExtensionFrameDescriptor[] extensionFrameDescriptors,
            out NnrpParseError error)
        {
            inlineBlocks = Array.Empty<InlineObjectBlockView>();
            objectReferenceBlocks = Array.Empty<ObjectReferenceBlock>();
            typedPayloadDescriptors = Array.Empty<TypedPayloadDescriptor>();
            extensionFrameDescriptors = Array.Empty<ExtensionFrameDescriptor>();
            error = NnrpParseError.None;

            if (!TypedPayloadRegionValidator.TryValidateTypedPayloadRegion(
                    metadata.PayloadKindBitmap,
                    metadata.PayloadFrameCount,
                    bodyView.TypedPayloadDescriptorRegion,
                    bodyView.TypedPayloadFrameRegion,
                    out typedPayloadDescriptors,
                    out error))
            {
                return false;
            }

            if (!ExtensionFrameRegionValidator.TryValidateAndCollectSkippableFrames(
                    bodyView.ExtensionDescriptorRegion,
                    bodyView.ExtensionPayloadRegion,
                    out extensionFrameDescriptors,
                    out error))
            {
                return false;
            }

            if (!BodyCodec.TrySplitInlineObjectRegion(bodyView.InlineObjectRegion, out inlineBlocks, out error))
            {
                return false;
            }

            if (!BodyCodec.TryParseObjectReferenceRegion(bodyView.ObjectReferenceRegion, out var objectReferenceView, out error))
            {
                return false;
            }

            objectReferenceBlocks = objectReferenceView.Blocks;
            return TryValidateObjectContract(metadata, inlineBlocks, objectReferenceBlocks, out error);
        }

        private static bool TryValidateObjectContract(
            ResultPushMetadata metadata,
            InlineObjectBlockView[] inlineBlocks,
            ObjectReferenceBlock[] objectReferenceBlocks,
            out NnrpParseError error)
        {
            error = NnrpParseError.None;

            var previousObjectKey = default((int objectKind, uint cacheNamespace, uint cacheKeyHigh, uint cacheKeyLow)?);
            foreach (var block in objectReferenceBlocks)
            {
                var currentKey = ((int)block.ObjectKind, block.CacheNamespace, block.CacheKeyHigh, block.CacheKeyLow);
                if (previousObjectKey.HasValue && currentKey.CompareTo(previousObjectKey.Value) <= 0)
                {
                    error = NnrpParseError.InvalidMessageLayout;
                    return false;
                }

                previousObjectKey = currentKey;
            }

            InlineObjectBlockView? tileIndexInline = null;
            InlineObjectBlockView? tensorSectionTableInline = null;
            var tileIndexReferenceCount = 0;
            var tensorSectionTableReferenceCount = 0;

            foreach (var block in inlineBlocks)
            {
                if (block.Header.ObjectKind == CacheObjectKind.TileIndexBlock)
                {
                    if (tileIndexInline.HasValue)
                    {
                        error = NnrpParseError.InvalidMessageLayout;
                        return false;
                    }

                    tileIndexInline = block;
                }
                else if (block.Header.ObjectKind == CacheObjectKind.TensorSectionTable)
                {
                    if (tensorSectionTableInline.HasValue)
                    {
                        error = NnrpParseError.InvalidMessageLayout;
                        return false;
                    }

                    tensorSectionTableInline = block;
                }
            }

            foreach (var block in objectReferenceBlocks)
            {
                if (block.ObjectKind == CacheObjectKind.TileIndexBlock)
                {
                    tileIndexReferenceCount++;
                }
                else if (block.ObjectKind == CacheObjectKind.TensorSectionTable)
                {
                    tensorSectionTableReferenceCount++;
                }
            }

            if (tileIndexReferenceCount > 1
                || tensorSectionTableReferenceCount > 1
                || (tileIndexInline.HasValue && tileIndexReferenceCount != 0)
                || (tensorSectionTableInline.HasValue && tensorSectionTableReferenceCount != 0))
            {
                error = NnrpParseError.InvalidMessageLayout;
                return false;
            }

            if (tileIndexInline.HasValue)
            {
                if (tileIndexInline.Value.Header.ObjectBytes != metadata.TileIndexBytes)
                {
                    error = NnrpParseError.InvalidMessageLayout;
                    return false;
                }

                if (!TryInferTileIndexMode(metadata, out var tileIndexMode, out error)
                    || !TileIndexBlockCodec.TryDecode(
                        tileIndexInline.Value.Payload.Span,
                        tileIndexMode,
                        metadata.TileCount,
                        new ushort[metadata.TileCount],
                        out _,
                        out error,
                        metadata.TileBaseId))
                {
                    return false;
                }
            }
            else if (metadata.TileCount > 0
                && metadata.TileIndexBytes != 0
                && tileIndexReferenceCount == 0)
            {
                error = NnrpParseError.InvalidMessageLayout;
                return false;
            }

            if (tensorSectionTableInline.HasValue)
            {
                if (!TryParseInlineTensorSectionTable(tensorSectionTableInline.Value.Payload, metadata.SectionCount, metadata.TileCount, out error))
                {
                    return false;
                }
            }
            else if (metadata.SectionCount > 0 && tensorSectionTableReferenceCount == 0)
            {
                error = NnrpParseError.InvalidMessageLayout;
                return false;
            }

            return true;
        }

        private static bool TryInferTileIndexMode(ResultPushMetadata metadata, out TileIndexMode tileIndexMode, out NnrpParseError error)
        {
            tileIndexMode = TileIndexMode.DenseRange;
            error = NnrpParseError.None;
            if (metadata.TileIndexBytes == 0)
            {
                return true;
            }

            var expectedRawBytes = checked((uint)(metadata.TileCount * sizeof(ushort)));
            if (metadata.TileIndexBytes == expectedRawBytes)
            {
                tileIndexMode = TileIndexMode.RawUInt16;
                return true;
            }

            error = NnrpParseError.InvalidMessageLayout;
            return false;
        }

        private static bool TryParseInlineTensorSectionTable(
            ReadOnlyMemory<byte> payload,
            ushort sectionCount,
            ushort tileCount,
            out NnrpParseError error)
        {
            error = NnrpParseError.None;
            var cursor = 0;
            TensorRole? previousRole = null;

            for (var index = 0; index < sectionCount; index++)
            {
                if (payload.Length < cursor)
                {
                    error = NnrpParseError.SourceTooShort;
                    return false;
                }

                if (!TensorSectionBlock.TryParse(payload.Slice(cursor), tileCount, out var section, out var sectionBytes, out error))
                {
                    return false;
                }

                if (previousRole.HasValue && section.Descriptor.Role <= previousRole.Value)
                {
                    error = NnrpParseError.InconsistentSectionDescriptor;
                    return false;
                }

                previousRole = section.Descriptor.Role;
                if (!CheckedArithmetic.TryAdd(cursor, sectionBytes, out var nextOffset))
                {
                    error = NnrpParseError.MessageTooLarge;
                    return false;
                }

                if (index + 1 < sectionCount)
                {
                    if (!TryValidateZeroPadding(payload, nextOffset, out cursor, out error))
                    {
                        return false;
                    }
                }
                else
                {
                    cursor = nextOffset;
                }
            }

            if (cursor != payload.Length)
            {
                error = NnrpParseError.InvalidMessageLayout;
                return false;
            }

            return true;
        }

        private static bool TryValidateZeroPadding(ReadOnlyMemory<byte> source, int offset, out int alignedOffset, out NnrpParseError error)
        {
            alignedOffset = 0;
            error = NnrpParseError.None;

            if (!BinaryAlignment.TryAlignUp(offset, out alignedOffset))
            {
                error = NnrpParseError.MessageTooLarge;
                return false;
            }

            if (source.Length < alignedOffset)
            {
                error = NnrpParseError.SourceTooShort;
                return false;
            }

            if (!BinaryAlignment.ValidateZeroPadding(source.Span.Slice(offset, alignedOffset - offset)))
            {
                error = NnrpParseError.InvalidMessageLayout;
                return false;
            }

            return true;
        }
    }
}
