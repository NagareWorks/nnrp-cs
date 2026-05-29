using System;

namespace Nnrp.Core
{
    public static class SubmitObjectReferenceMask
    {
        public const uint AllowedBits =
            (uint)(SubmitObjectSlot.CameraBlock
            | SubmitObjectSlot.TileIndexBlock
            | SubmitObjectSlot.TensorSectionTable
            | SubmitObjectSlot.PayloadLayoutTemplate);

        private static readonly CacheObjectKind[] StandardSlotKinds =
        {
            CacheObjectKind.CameraBlock,
            CacheObjectKind.TileIndexBlock,
            CacheObjectKind.TensorSectionTable,
            CacheObjectKind.PayloadLayoutTemplate,
        };

        public static uint Build(params SubmitObjectSlot[] slots)
        {
            uint mask = 0;
            if (slots == null)
            {
                return 0;
            }

            foreach (var slot in slots)
            {
                mask |= (uint)slot;
            }

            return mask;
        }

        public static bool IsDefined(uint mask)
        {
            return (mask & ~AllowedBits) == 0;
        }

        public static bool Contains(uint mask, SubmitObjectSlot slot)
        {
            return ((SubmitObjectSlot)mask & slot) == slot;
        }

        public static CacheObjectKind[] GetReferencedObjectKinds(uint mask)
        {
            if (mask == 0)
            {
                return Array.Empty<CacheObjectKind>();
            }

            var kinds = new System.Collections.Generic.List<CacheObjectKind>(4);
            for (var index = 0; index < StandardSlotKinds.Length; index++)
            {
                if ((mask & (1u << index)) != 0)
                {
                    kinds.Add(StandardSlotKinds[index]);
                }
            }

            return kinds.ToArray();
        }

        public static CacheObjectKind[] GetStandardObjectKinds()
        {
            var kinds = new CacheObjectKind[StandardSlotKinds.Length];
            Array.Copy(StandardSlotKinds, kinds, kinds.Length);
            return kinds;
        }

        public static bool TryGetStandardSlotIndex(CacheObjectKind objectKind, out int slotIndex)
        {
            for (var index = 0; index < StandardSlotKinds.Length; index++)
            {
                if (StandardSlotKinds[index] == objectKind)
                {
                    slotIndex = index;
                    return true;
                }
            }

            slotIndex = -1;
            return false;
        }

        public static bool TryValidateForSubmitMode(SubmitMode submitMode, uint mask, out NnrpParseError error)
        {
            error = NnrpParseError.None;
            if (!IsDefined(mask))
            {
                error = NnrpParseError.InvalidMessageLayout;
                return false;
            }

            switch (submitMode)
            {
                case SubmitMode.Inline:
                    if (mask != 0)
                    {
                        error = NnrpParseError.InvalidMessageLayout;
                        return false;
                    }

                    return true;
                case SubmitMode.Reference:
                case SubmitMode.Mixed:
                    if (mask == 0)
                    {
                        error = NnrpParseError.InvalidMessageLayout;
                        return false;
                    }

                    return true;
                default:
                    error = NnrpParseError.InvalidMessageLayout;
                    return false;
            }
        }
    }
}
