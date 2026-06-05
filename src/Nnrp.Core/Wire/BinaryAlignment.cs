using System;

namespace Nnrp.Core
{
    public static class BinaryAlignment
    {
        public const int DefaultAlignment = 8;

        public static bool IsAligned(int value, int alignment = DefaultAlignment)
        {
            if (alignment <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(alignment), "Alignment must be positive.");
            }

            return value % alignment == 0;
        }

        public static int GetPadding(int value, int alignment = DefaultAlignment)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Value must be non-negative.");
            }

            if (alignment <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(alignment), "Alignment must be positive.");
            }

            var remainder = value % alignment;
            return remainder == 0 ? 0 : alignment - remainder;
        }

        public static bool TryAlignUp(int value, out int alignedValue, int alignment = DefaultAlignment)
        {
            alignedValue = 0;
            if (value < 0 || alignment <= 0)
            {
                return false;
            }

            var padding = GetPadding(value, alignment);
            return CheckedArithmetic.TryAdd(value, padding, out alignedValue);
        }

        public static int AlignUp(int value, int alignment = DefaultAlignment)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Value must be non-negative.");
            }

            if (alignment <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(alignment), "Alignment must be positive.");
            }

            var padding = GetPadding(value, alignment);
            return value + padding;
        }

        public static long AlignUp(long value, int alignment = DefaultAlignment)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Value must be non-negative.");
            }

            if (alignment <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(alignment), "Alignment must be positive.");
            }

            var remainder = value % alignment;
            return remainder == 0 ? value : value + alignment - remainder;
        }

        public static bool ValidateZeroPadding(ReadOnlySpan<byte> source)
        {
            for (var index = 0; index < source.Length; index++)
            {
                if (source[index] != 0)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
