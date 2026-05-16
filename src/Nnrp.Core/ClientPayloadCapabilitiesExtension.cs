using System;

namespace Nnrp.Core
{
    public readonly struct ClientPayloadCapabilitiesExtension : IEquatable<ClientPayloadCapabilitiesExtension>
    {
        public const int PayloadLength = 8;

        private const PayloadKind AllowedPayloadKindMask = PayloadKind.Tensor
            | PayloadKind.TokenChunk
            | PayloadKind.AudioChunk
            | PayloadKind.VideoChunk
            | PayloadKind.StructuredEvent
            | PayloadKind.ToolDelta
            | PayloadKind.OpaqueBytes;

        public ClientPayloadCapabilitiesExtension(PayloadKind payloadKindBitmap, uint criticalExtensionFrameBitmap = 0)
        {
            if (((uint)payloadKindBitmap & ~(uint)AllowedPayloadKindMask) != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(payloadKindBitmap));
            }

            if (criticalExtensionFrameBitmap != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(criticalExtensionFrameBitmap), "The current wire contract requires criticalExtensionFrameBitmap to remain zero.");
            }

            PayloadKindBitmap = payloadKindBitmap;
            CriticalExtensionFrameBitmap = criticalExtensionFrameBitmap;
        }

        public PayloadKind PayloadKindBitmap { get; }

        public uint CriticalExtensionFrameBitmap { get; }

        public ControlExtensionBlock ToControlExtension()
        {
            var payload = new byte[PayloadLength];
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(0), (uint)PayloadKindBitmap);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(4), CriticalExtensionFrameBitmap);
            return new ControlExtensionBlock(ControlExtensionType.ClientPayloadCapabilities, payload);
        }

        public static bool TryParse(ControlExtensionBlock block, out ClientPayloadCapabilitiesExtension extension, out NnrpParseError error)
        {
            extension = default;
            error = NnrpParseError.None;
            if (block.TypeCode != (ushort)ControlExtensionType.ClientPayloadCapabilities || block.Value.Length != PayloadLength)
            {
                error = NnrpParseError.InvalidMessageLayout;
                return false;
            }

            var span = block.Value.Span;
            var payloadKindBitmap = (PayloadKind)System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(0, 4));
            var criticalExtensionFrameBitmap = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(4, 4));
            if (((uint)payloadKindBitmap & ~(uint)AllowedPayloadKindMask) != 0)
            {
                error = NnrpParseError.InvalidMessageLayout;
                return false;
            }

            if (criticalExtensionFrameBitmap != 0)
            {
                error = NnrpParseError.NonZeroReservedField;
                return false;
            }

            extension = new ClientPayloadCapabilitiesExtension(payloadKindBitmap, criticalExtensionFrameBitmap);
            return true;
        }

        public bool Equals(ClientPayloadCapabilitiesExtension other)
        {
            return PayloadKindBitmap == other.PayloadKindBitmap
                && CriticalExtensionFrameBitmap == other.CriticalExtensionFrameBitmap;
        }

        public override bool Equals(object obj)
        {
            return obj is ClientPayloadCapabilitiesExtension other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)PayloadKindBitmap * 397) ^ (int)CriticalExtensionFrameBitmap;
            }
        }
    }
}