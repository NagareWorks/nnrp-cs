using System;

namespace Nnrp.Core
{
    public static class SubmitObjectRegionValidator
    {
        public static bool TryValidate(
            uint objectRefMask,
            ReadOnlyMemory<byte> inlineObjectRegion,
            ReadOnlyMemory<byte> objectReferenceRegion,
            out SubmitObjectRegionValidationResult result,
            out NnrpParseError error)
        {
            result = default;
            error = NnrpParseError.None;

            if (!BodyCodec.TrySplitInlineObjectRegion(inlineObjectRegion, out var inlineBlocks, out error))
            {
                return false;
            }

            if (!BodyCodec.TryParseObjectReferenceRegion(objectReferenceRegion, out var objectReferenceView, out error))
            {
                return false;
            }

            if (!TryValidateStandardSlotOrdering(inlineBlocks, out error)
                || !TryValidateStandardSlotOrdering(objectReferenceView.Blocks, out error))
            {
                return false;
            }

            var inlineCounts = new int[4];
            foreach (var block in inlineBlocks)
            {
                if (SubmitObjectReferenceMask.TryGetStandardSlotIndex(block.Header.ObjectKind, out var slotIndex))
                {
                    inlineCounts[slotIndex]++;
                    if (inlineCounts[slotIndex] > 1)
                    {
                        error = NnrpParseError.InvalidMessageLayout;
                        return false;
                    }
                }
            }

            var referenceCounts = new int[4];
            foreach (var block in objectReferenceView.Blocks)
            {
                if (SubmitObjectReferenceMask.TryGetStandardSlotIndex(block.ObjectKind, out var slotIndex))
                {
                    referenceCounts[slotIndex]++;
                    if (referenceCounts[slotIndex] > 1)
                    {
                        error = NnrpParseError.InvalidMessageLayout;
                        return false;
                    }
                }
            }

            for (var slotIndex = 0; slotIndex < 4; slotIndex++)
            {
                var isReferenced = (objectRefMask & (1u << slotIndex)) != 0;
                if (isReferenced)
                {
                    if (referenceCounts[slotIndex] != 1 || inlineCounts[slotIndex] != 0)
                    {
                        error = NnrpParseError.InvalidMessageLayout;
                        return false;
                    }
                }
                else if (referenceCounts[slotIndex] != 0)
                {
                    error = NnrpParseError.InvalidMessageLayout;
                    return false;
                }
            }

            result = new SubmitObjectRegionValidationResult(inlineBlocks, objectReferenceView.Blocks);
            return true;
        }

        private static bool TryValidateStandardSlotOrdering(InlineObjectBlockView[] blocks, out NnrpParseError error)
        {
            error = NnrpParseError.None;
            var lastSlotIndex = -1;
            foreach (var block in blocks)
            {
                if (!SubmitObjectReferenceMask.TryGetStandardSlotIndex(block.Header.ObjectKind, out var slotIndex))
                {
                    continue;
                }

                if (slotIndex < lastSlotIndex)
                {
                    error = NnrpParseError.InvalidMessageLayout;
                    return false;
                }

                lastSlotIndex = slotIndex;
            }

            return true;
        }

        private static bool TryValidateStandardSlotOrdering(ObjectReferenceBlock[] blocks, out NnrpParseError error)
        {
            error = NnrpParseError.None;
            var lastSlotIndex = -1;
            foreach (var block in blocks)
            {
                if (!SubmitObjectReferenceMask.TryGetStandardSlotIndex(block.ObjectKind, out var slotIndex))
                {
                    continue;
                }

                if (slotIndex < lastSlotIndex)
                {
                    error = NnrpParseError.InvalidMessageLayout;
                    return false;
                }

                lastSlotIndex = slotIndex;
            }

            return true;
        }
    }
}
