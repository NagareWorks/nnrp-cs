using System;

namespace Nnrp.Core
{
    public readonly struct ServerLossToleranceAckExtension : IEquatable<ServerLossToleranceAckExtension>
    {
        public const int PayloadLength = 4;

        public ServerLossToleranceAckExtension(LossTolerance acceptedLossTolerance)
        {
            AcceptedLossTolerance = acceptedLossTolerance;
        }

        public LossTolerance AcceptedLossTolerance { get; }

        public ControlExtensionBlock ToControlExtension()
        {
            var payload = new byte[PayloadLength];
            payload[0] = (byte)AcceptedLossTolerance;
            return new ControlExtensionBlock(ControlExtensionType.ServerLossToleranceAck, payload);
        }

        public static bool TryParse(ControlExtensionBlock block, out ServerLossToleranceAckExtension extension, out NnrpParseError error)
        {
            extension = default;
            error = NnrpParseError.None;
            if (block.TypeCode != (ushort)ControlExtensionType.ServerLossToleranceAck || block.Value.Length != PayloadLength)
            {
                error = NnrpParseError.InvalidMessageLayout;
                return false;
            }

            var span = block.Value.Span;
            if (span[1] != 0 || span[2] != 0 || span[3] != 0)
            {
                error = NnrpParseError.NonZeroReservedField;
                return false;
            }

            extension = new ServerLossToleranceAckExtension((LossTolerance)span[0]);
            return true;
        }

        public bool Equals(ServerLossToleranceAckExtension other)
        {
            return AcceptedLossTolerance == other.AcceptedLossTolerance;
        }

        public override bool Equals(object obj)
        {
            return obj is ServerLossToleranceAckExtension other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (int)AcceptedLossTolerance;
        }
    }
}
