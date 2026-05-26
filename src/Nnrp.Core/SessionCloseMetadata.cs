using System;

namespace Nnrp.Core
{
    public readonly struct SessionCloseMetadata : IEquatable<SessionCloseMetadata>
    {
        public const int MetadataLength = 24;

        public SessionCloseMetadata(
            SessionCloseReason closeReason,
            InFlightPolicy inFlightPolicy,
            uint drainTimeoutMilliseconds,
            ulong lastOperationId,
            SessionErrorCode sessionErrorCode,
            uint sessionCloseTag)
        {
            if (!TryValidate(closeReason, inFlightPolicy, sessionErrorCode, out _))
            {
                ThrowValidation(closeReason, inFlightPolicy, sessionErrorCode);
            }

            CloseReason = closeReason;
            InFlightPolicy = inFlightPolicy;
            DrainTimeoutMilliseconds = drainTimeoutMilliseconds;
            LastOperationId = lastOperationId;
            SessionErrorCode = sessionErrorCode;
            SessionCloseTag = sessionCloseTag;
        }

        public SessionCloseReason CloseReason { get; }

        public InFlightPolicy InFlightPolicy { get; }

        public uint DrainTimeoutMilliseconds { get; }

        public ulong LastOperationId { get; }

        public SessionErrorCode SessionErrorCode { get; }

        public uint SessionCloseTag { get; }

        public void Write(Span<byte> destination)
        {
            if (!TryWrite(destination, out _))
            {
                throw new ArgumentException($"Destination must be at least {MetadataLength} bytes.", nameof(destination));
            }
        }

        public bool TryWrite(Span<byte> destination, out int bytesWritten)
        {
            bytesWritten = 0;
            if (destination.Length < MetadataLength)
            {
                return false;
            }

            var writer = new FixedBinaryWriter(destination);
            if (!writer.TryWriteUInt16((ushort)CloseReason)
                || !writer.TryWriteByte((byte)InFlightPolicy)
                || !writer.TryWriteByte(0)
                || !writer.TryWriteUInt32(DrainTimeoutMilliseconds)
                || !writer.TryWriteUInt64(LastOperationId)
                || !writer.TryWriteUInt32((uint)SessionErrorCode)
                || !writer.TryWriteUInt32(SessionCloseTag))
            {
                return false;
            }

            bytesWritten = writer.Offset;
            return bytesWritten == MetadataLength;
        }

        public byte[] ToArray()
        {
            var payload = new byte[MetadataLength];
            Write(payload);
            return payload;
        }

        public static bool TryParse(ReadOnlySpan<byte> source, out SessionCloseMetadata metadata)
        {
            return TryParse(source, strict: false, out metadata, out _);
        }

        public static bool TryParse(ReadOnlySpan<byte> source, out SessionCloseMetadata metadata, out NnrpParseError error)
        {
            return TryParse(source, strict: false, out metadata, out error);
        }

        public static bool TryParse(ReadOnlySpan<byte> source, bool strict, out SessionCloseMetadata metadata, out NnrpParseError error)
        {
            metadata = default;
            error = NnrpParseError.None;
            if (source.Length < MetadataLength)
            {
                error = NnrpParseError.SourceTooShort;
                return false;
            }

            var reader = new FixedBinaryReader(source);
            if (!reader.TryReadUInt16(out var closeReason)
                || !reader.TryReadByte(out var inFlightPolicy)
                || !reader.TryReadByte(out var reserved0)
                || !reader.TryReadUInt32(out var drainTimeoutMilliseconds)
                || !reader.TryReadUInt64(out var lastOperationId)
                || !reader.TryReadUInt32(out var sessionErrorCode)
                || !reader.TryReadUInt32(out var sessionCloseTag))
            {
                error = NnrpParseError.SourceTooShort;
                return false;
            }

            if (strict && reserved0 != 0)
            {
                error = NnrpParseError.NonZeroReservedField;
                return false;
            }

            if (!TryValidate((SessionCloseReason)closeReason, (InFlightPolicy)inFlightPolicy, (SessionErrorCode)sessionErrorCode, out error))
            {
                return false;
            }

            metadata = new SessionCloseMetadata(
                (SessionCloseReason)closeReason,
                (InFlightPolicy)inFlightPolicy,
                drainTimeoutMilliseconds,
                lastOperationId,
                (SessionErrorCode)sessionErrorCode,
                sessionCloseTag);
            return true;
        }

        public bool Equals(SessionCloseMetadata other)
        {
            return CloseReason == other.CloseReason
                && InFlightPolicy == other.InFlightPolicy
                && DrainTimeoutMilliseconds == other.DrainTimeoutMilliseconds
                && LastOperationId == other.LastOperationId
                && SessionErrorCode == other.SessionErrorCode
                && SessionCloseTag == other.SessionCloseTag;
        }

        public override bool Equals(object obj)
        {
            return obj is SessionCloseMetadata other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = CloseReason.GetHashCode();
                hash = (hash * 397) ^ InFlightPolicy.GetHashCode();
                hash = (hash * 397) ^ DrainTimeoutMilliseconds.GetHashCode();
                hash = (hash * 397) ^ LastOperationId.GetHashCode();
                hash = (hash * 397) ^ SessionErrorCode.GetHashCode();
                hash = (hash * 397) ^ SessionCloseTag.GetHashCode();
                return hash;
            }
        }

        private static bool TryValidate(SessionCloseReason closeReason, InFlightPolicy inFlightPolicy, SessionErrorCode sessionErrorCode, out NnrpParseError error)
        {
            error = NnrpParseError.None;
            if (!Enum.IsDefined(typeof(SessionCloseReason), closeReason)
                || !Enum.IsDefined(typeof(InFlightPolicy), inFlightPolicy)
                || !Enum.IsDefined(typeof(SessionErrorCode), sessionErrorCode))
            {
                error = NnrpParseError.InvalidMessageLayout;
                return false;
            }

            return true;
        }

        private static void ThrowValidation(SessionCloseReason closeReason, InFlightPolicy inFlightPolicy, SessionErrorCode sessionErrorCode)
        {
            if (!Enum.IsDefined(typeof(SessionCloseReason), closeReason))
            {
                throw new ArgumentOutOfRangeException(nameof(closeReason));
            }

            if (!Enum.IsDefined(typeof(InFlightPolicy), inFlightPolicy))
            {
                throw new ArgumentOutOfRangeException(nameof(inFlightPolicy));
            }

            if (!Enum.IsDefined(typeof(SessionErrorCode), sessionErrorCode))
            {
                throw new ArgumentOutOfRangeException(nameof(sessionErrorCode));
            }
        }
    }
}
