using System;

namespace Nnrp.Core
{
    public readonly struct ClientLossToleranceExtension : IEquatable<ClientLossToleranceExtension>
    {
        public const int PayloadLength = 4;

        public ClientLossToleranceExtension(LossTolerance lossTolerance)
        {
            LossTolerance = lossTolerance;
        }

        public LossTolerance LossTolerance { get; }

        public ControlExtensionBlock ToControlExtension()
        {
            var payload = new byte[PayloadLength];
            payload[0] = (byte)LossTolerance;
            return new ControlExtensionBlock(ControlExtensionType.ClientLossTolerance, payload);
        }

        public static bool TryParse(ControlExtensionBlock block, out ClientLossToleranceExtension extension, out NnrpParseError error)
        {
            extension = default;
            error = NnrpParseError.None;
            if (block.TypeCode != (ushort)ControlExtensionType.ClientLossTolerance || block.Value.Length != PayloadLength)
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

            extension = new ClientLossToleranceExtension((LossTolerance)span[0]);
            return true;
        }

        public bool Equals(ClientLossToleranceExtension other)
        {
            return LossTolerance == other.LossTolerance;
        }

        public override bool Equals(object obj)
        {
            return obj is ClientLossToleranceExtension other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (int)LossTolerance;
        }
    }
}
