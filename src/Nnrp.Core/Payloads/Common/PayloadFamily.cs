using System;
using System.Globalization;

namespace Nnrp.Core
{
    public readonly struct PayloadFamily : IEquatable<PayloadFamily>
    {
        public PayloadFamily(PayloadKind payloadKind)
        {
            if (!PayloadKindValidator.IsSingleDefinedKind(payloadKind))
            {
                throw new ArgumentOutOfRangeException(nameof(payloadKind), "Payload families require a single defined payload kind bit.");
            }

            PayloadKind = payloadKind;
        }

        public static PayloadFamily Tensor => new PayloadFamily(PayloadKind.Tensor);

        public static PayloadFamily TokenChunk => new PayloadFamily(PayloadKind.TokenChunk);

        public static PayloadFamily AudioChunk => new PayloadFamily(PayloadKind.AudioChunk);

        public static PayloadFamily VideoChunk => new PayloadFamily(PayloadKind.VideoChunk);

        public static PayloadFamily StructuredEvent => new PayloadFamily(PayloadKind.StructuredEvent);

        public static PayloadFamily ToolDelta => new PayloadFamily(PayloadKind.ToolDelta);

        public static PayloadFamily OpaqueBytes => new PayloadFamily(PayloadKind.OpaqueBytes);

        public PayloadKind PayloadKind { get; }

        public uint Bit => (uint)PayloadKind;

        public bool IsDefined => PayloadKindValidator.IsSingleDefinedKind(PayloadKind);

        public bool IsStandardProfile => PayloadKind == PayloadKind.Tensor || PayloadKind == PayloadKind.TokenChunk;

        public bool IsRegistryBoundFamily =>
            PayloadKind == PayloadKind.AudioChunk
            || PayloadKind == PayloadKind.VideoChunk
            || PayloadKind == PayloadKind.StructuredEvent
            || PayloadKind == PayloadKind.ToolDelta
            || PayloadKind == PayloadKind.OpaqueBytes;

        public bool IsStructuredEvent => PayloadKind == PayloadKind.StructuredEvent;

        public bool IsToolDelta => PayloadKind == PayloadKind.ToolDelta;

        public TypedPayloadProfileId StandardProfile
        {
            get
            {
                return TypedPayloadProfileId.TryFromPayloadKind(PayloadKind, out var profile)
                    ? profile
                    : TypedPayloadProfileId.Unspecified;
            }
        }

        public string Name
        {
            get
            {
                switch (PayloadKind)
                {
                    case PayloadKind.Tensor:
                        return "tensor";
                    case PayloadKind.TokenChunk:
                        return "token_chunk";
                    case PayloadKind.AudioChunk:
                        return "audio_chunk";
                    case PayloadKind.VideoChunk:
                        return "video_chunk";
                    case PayloadKind.StructuredEvent:
                        return "structured_event";
                    case PayloadKind.ToolDelta:
                        return "tool_delta";
                    case PayloadKind.OpaqueBytes:
                        return "opaque_bytes";
                    default:
                        return Bit.ToString(CultureInfo.InvariantCulture);
                }
            }
        }

        public static PayloadFamily FromPayloadKind(PayloadKind payloadKind)
        {
            return new PayloadFamily(payloadKind);
        }

        public static bool TryFromPayloadKind(PayloadKind payloadKind, out PayloadFamily family)
        {
            if (PayloadKindValidator.IsSingleDefinedKind(payloadKind))
            {
                family = new PayloadFamily(payloadKind);
                return true;
            }

            family = default;
            return false;
        }

        public bool Equals(PayloadFamily other)
        {
            return PayloadKind == other.PayloadKind;
        }

        public override bool Equals(object obj)
        {
            return obj is PayloadFamily other && Equals(other);
        }

        public override int GetHashCode()
        {
            return PayloadKind.GetHashCode();
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
