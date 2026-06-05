using System;

namespace Nnrp.Core
{
    public readonly struct ClientTransportPolicyExtension : IEquatable<ClientTransportPolicyExtension>
    {
        public const int PayloadLength = 8;

        public ClientTransportPolicyExtension(TransportPolicy transportPolicy, TransportId preferredTransportId)
        {
            TransportPolicy = transportPolicy;
            PreferredTransportId = preferredTransportId;
        }

        public TransportPolicy TransportPolicy { get; }

        public TransportId PreferredTransportId { get; }

        public ControlExtensionBlock ToControlExtension()
        {
            var payload = new byte[PayloadLength];
            payload[0] = (byte)TransportPolicy;
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(4), (uint)PreferredTransportId);
            return new ControlExtensionBlock(ControlExtensionType.ClientTransportPolicy, payload);
        }

        public static bool TryParse(ControlExtensionBlock block, out ClientTransportPolicyExtension extension, out NnrpParseError error)
        {
            extension = default;
            error = NnrpParseError.None;
            if (block.TypeCode != (ushort)ControlExtensionType.ClientTransportPolicy || block.Value.Length != PayloadLength)
            {
                error = NnrpParseError.InvalidMessageLayout;
                return false;
            }

            var span = block.Value.Span;
            extension = new ClientTransportPolicyExtension(
                (TransportPolicy)span[0],
                (TransportId)System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(4)));
            return true;
        }

        public bool Equals(ClientTransportPolicyExtension other)
        {
            return TransportPolicy == other.TransportPolicy
                && PreferredTransportId == other.PreferredTransportId;
        }

        public override bool Equals(object obj)
        {
            return obj is ClientTransportPolicyExtension other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)TransportPolicy * 397) ^ (int)PreferredTransportId;
            }
        }
    }
}
