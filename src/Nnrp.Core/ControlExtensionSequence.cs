using System;
using System.Collections.Generic;

namespace Nnrp.Core
{
    internal static class ControlExtensionSequence
    {
        public static int GetTotalLength(ReadOnlyMemory<ControlExtensionBlock> extensions)
        {
            var total = 0;
            var span = extensions.Span;
            for (var i = 0; i < span.Length; i++)
            {
                total = checked(total + span[i].TotalLength);
            }

            return total;
        }

        public static byte[] ToArray(ReadOnlyMemory<ControlExtensionBlock> extensions)
        {
            var totalLength = GetTotalLength(extensions);
            if (totalLength == 0)
            {
                return Array.Empty<byte>();
            }

            var buffer = new byte[totalLength];
            var offset = 0;
            var span = extensions.Span;
            for (var i = 0; i < span.Length; i++)
            {
                var block = span[i];
                block.WriteTo(buffer.AsSpan(offset, block.TotalLength));
                offset += block.TotalLength;
            }

            return buffer;
        }

        public static bool TryParse(ReadOnlySpan<byte> source, out ControlExtensionBlock[] extensions, out NnrpParseError error)
        {
            extensions = Array.Empty<ControlExtensionBlock>();
            error = NnrpParseError.None;
            if (source.Length == 0)
            {
                return true;
            }

            var list = new List<ControlExtensionBlock>();
            var cursor = 0;
            while (cursor < source.Length)
            {
                if (!ControlExtensionBlock.TryParse(source.Slice(cursor), out var block, out var consumed, out error))
                {
                    extensions = Array.Empty<ControlExtensionBlock>();
                    return false;
                }

                list.Add(block);
                cursor += consumed;
            }

            extensions = list.ToArray();
            return true;
        }
    }
}
