using System;

namespace Nnrp.Core
{
    public readonly struct NnrpHeaderParseOptions : IEquatable<NnrpHeaderParseOptions>
    {
        public static readonly NnrpHeaderParseOptions Default = new NnrpHeaderParseOptions(strict: false);

        public static readonly NnrpHeaderParseOptions Strict = new NnrpHeaderParseOptions(strict: true);

        public NnrpHeaderParseOptions(bool strict, ulong maxMessageLength = uint.MaxValue)
        {
            StrictValidation = strict;
            MaxMessageLength = maxMessageLength;
        }

        public bool StrictValidation { get; }

        public ulong MaxMessageLength { get; }

        public bool Equals(NnrpHeaderParseOptions other)
        {
            return StrictValidation == other.StrictValidation
                && MaxMessageLength == other.MaxMessageLength;
        }

        public override bool Equals(object obj)
        {
            return obj is NnrpHeaderParseOptions other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((StrictValidation ? 1 : 0) * 397) ^ MaxMessageLength.GetHashCode();
            }
        }
    }
}
