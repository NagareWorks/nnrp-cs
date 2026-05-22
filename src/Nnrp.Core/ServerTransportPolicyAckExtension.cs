using System;

namespace Nnrp.Core
{
    /// <summary>
    /// Optional transport identity surface for transports that can report the active NNRP binding.
    /// </summary>
    public interface INnrpTransportIdentity
    {
        TransportId TransportId { get; }
    }

    public readonly struct ServerTransportPolicyAckExtension : IEquatable<ServerTransportPolicyAckExtension>
    {
        public const int PayloadLength = 8;

        public ServerTransportPolicyAckExtension(
            TransportPolicy transportPolicy,
            TransportPolicy acceptedTransportPolicy,
            TransportId activeTransportId)
        {
            TransportPolicy = transportPolicy;
            AcceptedTransportPolicy = acceptedTransportPolicy;
            ActiveTransportId = activeTransportId;
        }

        public TransportPolicy TransportPolicy { get; }

        public TransportPolicy AcceptedTransportPolicy { get; }

        public TransportId ActiveTransportId { get; }

        public ControlExtensionBlock ToControlExtension()
        {
            var payload = new byte[PayloadLength];
            payload[0] = (byte)TransportPolicy;
            payload[1] = (byte)AcceptedTransportPolicy;
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(4), (uint)ActiveTransportId);
            return new ControlExtensionBlock(ControlExtensionType.ServerTransportPolicyAck, payload);
        }

        public static bool TryParse(ControlExtensionBlock block, out ServerTransportPolicyAckExtension extension, out NnrpParseError error)
        {
            extension = default;
            error = NnrpParseError.None;
            if (block.TypeCode != (ushort)ControlExtensionType.ServerTransportPolicyAck || block.Value.Length != PayloadLength)
            {
                error = NnrpParseError.InvalidMessageLayout;
                return false;
            }

            var span = block.Value.Span;
            extension = new ServerTransportPolicyAckExtension(
                (TransportPolicy)span[0],
                (TransportPolicy)span[1],
                (TransportId)System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(4)));
            return true;
        }

        public bool Equals(ServerTransportPolicyAckExtension other)
        {
            return TransportPolicy == other.TransportPolicy
                && AcceptedTransportPolicy == other.AcceptedTransportPolicy
                && ActiveTransportId == other.ActiveTransportId;
        }

        public override bool Equals(object obj)
        {
            return obj is ServerTransportPolicyAckExtension other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = (int)TransportPolicy;
                hash = (hash * 397) ^ (int)AcceptedTransportPolicy;
                hash = (hash * 397) ^ (int)ActiveTransportId;
                return hash;
            }
        }
    }
}
