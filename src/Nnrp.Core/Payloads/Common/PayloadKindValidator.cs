namespace Nnrp.Core
{
    public static class PayloadKindValidator
    {
        public const uint AllowedPayloadKindBits =
            (uint)(PayloadKind.Tensor
            | PayloadKind.TokenChunk
            | PayloadKind.AudioChunk
            | PayloadKind.VideoChunk
            | PayloadKind.StructuredEvent
            | PayloadKind.ToolDelta
            | PayloadKind.OpaqueBytes);

        public static bool IsDefinedBitmap(PayloadKind payloadKindBitmap)
        {
            return ((uint)payloadKindBitmap & ~AllowedPayloadKindBits) == 0;
        }

        public static bool IsSingleDefinedKind(PayloadKind payloadKind)
        {
            var raw = (uint)payloadKind;
            return raw != 0
                && (raw & ~AllowedPayloadKindBits) == 0
                && (raw & (raw - 1)) == 0;
        }
    }
}
