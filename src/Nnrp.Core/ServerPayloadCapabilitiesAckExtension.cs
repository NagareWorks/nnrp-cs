using System;

namespace Nnrp.Core
{
    public readonly struct ServerPayloadCapabilitiesAckExtension : IEquatable<ServerPayloadCapabilitiesAckExtension>
    {
        public const int PayloadLength = 8;

        private const PayloadKind AllowedPayloadKindMask = PayloadKind.Tensor
            | PayloadKind.TokenChunk
            | PayloadKind.AudioChunk
            | PayloadKind.VideoChunk
            | PayloadKind.StructuredEvent
            | PayloadKind.ToolDelta
            | PayloadKind.OpaqueBytes;

        public ServerPayloadCapabilitiesAckExtension(PayloadKind acceptedPayloadKindBitmap, uint acceptedCriticalExtensionFrameBitmap = 0)
        {
            if (((uint)acceptedPayloadKindBitmap & ~(uint)AllowedPayloadKindMask) != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(acceptedPayloadKindBitmap));
            }

            if (acceptedCriticalExtensionFrameBitmap != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(acceptedCriticalExtensionFrameBitmap), "The current wire contract requires acceptedCriticalExtensionFrameBitmap to remain zero.");
            }

            AcceptedPayloadKindBitmap = acceptedPayloadKindBitmap;
            AcceptedCriticalExtensionFrameBitmap = acceptedCriticalExtensionFrameBitmap;
        }

        public PayloadKind AcceptedPayloadKindBitmap { get; }

        public uint AcceptedCriticalExtensionFrameBitmap { get; }

        public ControlExtensionBlock ToControlExtension()
        {
            var payload = new byte[PayloadLength];
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(0), (uint)AcceptedPayloadKindBitmap);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(4), AcceptedCriticalExtensionFrameBitmap);
            return new ControlExtensionBlock(ControlExtensionType.ServerPayloadCapabilitiesAck, payload);
        }

        public static bool TryParse(ControlExtensionBlock block, out ServerPayloadCapabilitiesAckExtension extension, out NnrpParseError error)
        {
            extension = default;
            error = NnrpParseError.None;
            if (block.TypeCode != (ushort)ControlExtensionType.ServerPayloadCapabilitiesAck || block.Value.Length != PayloadLength)
            {
                error = NnrpParseError.InvalidMessageLayout;
                return false;
            }

            var span = block.Value.Span;
            var acceptedPayloadKindBitmap = (PayloadKind)System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(0, 4));
            var acceptedCriticalExtensionFrameBitmap = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(4, 4));
            if (((uint)acceptedPayloadKindBitmap & ~(uint)AllowedPayloadKindMask) != 0)
            {
                error = NnrpParseError.InvalidMessageLayout;
                return false;
            }

            if (acceptedCriticalExtensionFrameBitmap != 0)
            {
                error = NnrpParseError.NonZeroReservedField;
                return false;
            }

            extension = new ServerPayloadCapabilitiesAckExtension(acceptedPayloadKindBitmap, acceptedCriticalExtensionFrameBitmap);
            return true;
        }

        public bool Equals(ServerPayloadCapabilitiesAckExtension other)
        {
            return AcceptedPayloadKindBitmap == other.AcceptedPayloadKindBitmap
                && AcceptedCriticalExtensionFrameBitmap == other.AcceptedCriticalExtensionFrameBitmap;
        }

        public override bool Equals(object obj)
        {
            return obj is ServerPayloadCapabilitiesAckExtension other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)AcceptedPayloadKindBitmap * 397) ^ (int)AcceptedCriticalExtensionFrameBitmap;
            }
        }
    }
}