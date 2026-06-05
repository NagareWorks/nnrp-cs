using System;

namespace Nnrp.Core
{
    public readonly struct NnrpProtocolFailure : IEquatable<NnrpProtocolFailure>
    {
        public static NnrpProtocolFailure None => default;

        public NnrpProtocolFailure(
            ErrorCode errorCode,
            NnrpErrorScope scope,
            string message,
            bool isFatal,
            NnrpParseError parseError = NnrpParseError.None)
        {
            ErrorCode = errorCode;
            Scope = scope;
            Message = message ?? string.Empty;
            IsFatal = isFatal;
            ParseError = parseError;
        }

        public bool IsFailure => ErrorCode != 0;

        public ErrorCode ErrorCode { get; }

        public NnrpErrorScope Scope { get; }

        public string Message { get; }

        public bool IsFatal { get; }

        public NnrpParseError ParseError { get; }

        public static NnrpProtocolFailure FromHeaderParseError(NnrpParseError parseError, string? message = null)
        {
            if (parseError == NnrpParseError.None)
            {
                return None;
            }

            ErrorCode errorCode;
            switch (parseError)
            {
                case NnrpParseError.UnsupportedVersion:
                    errorCode = ErrorCode.UnsupportedVersion;
                    break;
                case NnrpParseError.MessageTooLarge:
                    errorCode = ErrorCode.LimitExceeded;
                    break;
                default:
                    errorCode = ErrorCode.MalformedHeader;
                    break;
            }

            return new NnrpProtocolFailure(
                errorCode,
                NnrpErrorScope.Connection,
                message ?? $"Malformed NNRP header: {parseError}.",
                isFatal: true,
                parseError: parseError);
        }

        public static NnrpProtocolFailure FromBodyParseError(NnrpParseError parseError, string? message = null)
        {
            if (parseError == NnrpParseError.None)
            {
                return None;
            }

            var errorCode = parseError == NnrpParseError.MessageTooLarge
                ? ErrorCode.LimitExceeded
                : ErrorCode.MalformedBody;

            return new NnrpProtocolFailure(
                errorCode,
                NnrpErrorScope.Frame,
                message ?? $"Malformed NNRP body: {parseError}.",
                isFatal: false,
                parseError: parseError);
        }

        public static NnrpProtocolFailure InvalidState(NnrpErrorScope scope, string message, bool isFatal = false)
        {
            return new NnrpProtocolFailure(ErrorCode.InvalidState, scope, message, isFatal);
        }

        public static NnrpProtocolFailure UnsupportedCapability(string message, bool isFatal = true)
        {
            return new NnrpProtocolFailure(ErrorCode.UnsupportedCapability, NnrpErrorScope.Session, message, isFatal);
        }

        public static NnrpProtocolFailure LimitExceeded(NnrpErrorScope scope, string message, bool isFatal = false)
        {
            return new NnrpProtocolFailure(ErrorCode.LimitExceeded, scope, message, isFatal);
        }

        public bool Equals(NnrpProtocolFailure other)
        {
            return ErrorCode == other.ErrorCode
                && Scope == other.Scope
                && string.Equals(Message, other.Message, StringComparison.Ordinal)
                && IsFatal == other.IsFatal
                && ParseError == other.ParseError;
        }

        public override bool Equals(object obj)
        {
            return obj is NnrpProtocolFailure other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = (int)ErrorCode;
                hash = (hash * 397) ^ (int)Scope;
                hash = (hash * 397) ^ (Message == null ? 0 : Message.GetHashCode());
                hash = (hash * 397) ^ IsFatal.GetHashCode();
                hash = (hash * 397) ^ (int)ParseError;
                return hash;
            }
        }
    }
}
