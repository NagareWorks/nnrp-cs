using System;

namespace Nnrp.Core
{
    /// <summary>
    /// Helpers for parsing and validating a sequence of control extension TLV blocks.
    /// </summary>
    public static class ControlExtensionParser
    {
        /// <summary>
        /// Parse a span of TLV blocks and invoke <paramref name="onExtension"/>
        /// for each. Returns false (and sets <paramref name="error"/>) if:
        /// <list type="bullet">
        ///   <item>The span cannot hold a complete TLV header.</item>
        ///   <item>An unknown extension has its critical flag set.</item>
        ///   <item>A TLV value length exceeds the remaining span.</item>
        /// </list>
        /// Unknown optional extensions are silently skipped.
        /// </summary>
        /// <param name="source">The raw extension bytes (may be empty).</param>
        /// <param name="knownOptional">Well-known optional type codes the caller handles.</param>
        /// <param name="knownCritical">Well-known critical type codes the caller handles.</param>
        /// <param name="onExtension">Invoked for every recognized extension.</param>
        /// <param name="error">Set on failure.</param>
        /// <returns>True when all extensions are parsed successfully.</returns>
        public static bool TryParseExtensions(
            ReadOnlySpan<byte> source,
            ReadOnlySpan<ControlExtensionType> knownOptional,
            ReadOnlySpan<ControlExtensionType> knownCritical,
            Action<ControlExtensionBlock> onExtension,
            out NnrpParseError error)
        {
            error = NnrpParseError.None;

            var cursor = 0;
            while (cursor < source.Length)
            {
                var remaining = source.Slice(cursor);
                if (!ControlExtensionBlock.TryParse(remaining, out var block, out var consumed, out error))
                {
                    return false;
                }

                if (block.IsCritical)
                {
                    if (!Contains(knownCritical, block.TypeCode))
                    {
                        error = NnrpParseError.UnsupportedExtension;
                        return false;
                    }
                }
                else
                {
                    if (!Contains(knownOptional, block.TypeCode))
                    {
                        // Ignore unknown optional — just skip.
                        cursor += consumed;
                        continue;
                    }
                }

                onExtension(block);
                cursor += consumed;
            }

            return true;
        }

        /// <summary>
        /// Returns true when <paramref name="typeCode"/> is present in <paramref name="set"/>.
        /// </summary>
        private static bool Contains(ReadOnlySpan<ControlExtensionType> set, ushort typeCode)
        {
            for (var i = 0; i < set.Length; i++)
            {
                if ((ushort)set[i] == typeCode)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
