using System;
using System.Globalization;

namespace Nnrp.Core
{
    public readonly struct TypedPayloadProfileId : IEquatable<TypedPayloadProfileId>
    {
        public const ushort UnspecifiedValue = 0;
        public const ushort TensorValue = 1;
        public const ushort TokenValue = 2;

        public TypedPayloadProfileId(ushort value)
        {
            Value = value;
        }

        public static TypedPayloadProfileId Unspecified => new TypedPayloadProfileId(UnspecifiedValue);

        public static TypedPayloadProfileId Tensor => new TypedPayloadProfileId(TensorValue);

        public static TypedPayloadProfileId Token => new TypedPayloadProfileId(TokenValue);

        public ushort Value { get; }

        public bool IsUnspecified => Value == UnspecifiedValue;

        public bool IsTensor => Value == TensorValue;

        public bool IsToken => Value == TokenValue;

        public bool IsKnown => IsUnspecified || IsTensor || IsToken;

        public bool IsStandardProfile => IsTensor || IsToken;

        public bool IsExtension => !IsKnown;

        public PayloadKind PayloadKind
        {
            get
            {
                if (IsTensor)
                {
                    return PayloadKind.Tensor;
                }

                if (IsToken)
                {
                    return PayloadKind.TokenChunk;
                }

                return PayloadKind.None;
            }
        }

        public string Name
        {
            get
            {
                if (IsUnspecified)
                {
                    return "unspecified";
                }

                if (IsTensor)
                {
                    return "tensor";
                }

                if (IsToken)
                {
                    return "token";
                }

                return Value.ToString(CultureInfo.InvariantCulture);
            }
        }

        public static TypedPayloadProfileId FromValue(ushort value)
        {
            return new TypedPayloadProfileId(value);
        }

        public static bool TryFromPayloadKind(PayloadKind payloadKind, out TypedPayloadProfileId profile)
        {
            if (payloadKind == PayloadKind.Tensor)
            {
                profile = Tensor;
                return true;
            }

            if (payloadKind == PayloadKind.TokenChunk)
            {
                profile = Token;
                return true;
            }

            profile = Unspecified;
            return false;
        }

        public bool Equals(TypedPayloadProfileId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is TypedPayloadProfileId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
