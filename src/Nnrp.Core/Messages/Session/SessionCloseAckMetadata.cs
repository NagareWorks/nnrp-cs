using System;

namespace Nnrp.Core
{
    public readonly struct SessionCloseAckMetadata : IEquatable<SessionCloseAckMetadata>
    {
        public const int MetadataLength = 16;

        public SessionCloseAckMetadata(
            SessionCloseStatus closeStatus,
            ulong lastOperationId,
            SessionErrorCode sessionErrorCode)
        {
            if (!TryValidate(closeStatus, sessionErrorCode, out _))
            {
                ThrowValidation(closeStatus, sessionErrorCode);
            }

            CloseStatus = closeStatus;
            LastOperationId = lastOperationId;
            SessionErrorCode = sessionErrorCode;
        }

        public SessionCloseStatus CloseStatus { get; }

        public ulong LastOperationId { get; }

        public SessionErrorCode SessionErrorCode { get; }

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
            if (!writer.TryWriteByte((byte)CloseStatus)
                || !writer.TryWriteByte(0)
                || !writer.TryWriteUInt16(0)
                || !writer.TryWriteUInt64(LastOperationId)
                || !writer.TryWriteUInt32((uint)SessionErrorCode))
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

        public static bool TryParse(ReadOnlySpan<byte> source, out SessionCloseAckMetadata metadata)
        {
            return TryParse(source, strict: false, out metadata, out _);
        }

        public static bool TryParse(ReadOnlySpan<byte> source, out SessionCloseAckMetadata metadata, out NnrpParseError error)
        {
            return TryParse(source, strict: false, out metadata, out error);
        }

        public static bool TryParse(ReadOnlySpan<byte> source, bool strict, out SessionCloseAckMetadata metadata, out NnrpParseError error)
        {
            metadata = default;
            error = NnrpParseError.None;
            if (source.Length < MetadataLength)
            {
                error = NnrpParseError.SourceTooShort;
                return false;
            }

            var reader = new FixedBinaryReader(source);
            if (!reader.TryReadByte(out var closeStatus)
                || !reader.TryReadByte(out var reserved0)
                || !reader.TryReadUInt16(out var reserved1)
                || !reader.TryReadUInt64(out var lastOperationId)
                || !reader.TryReadUInt32(out var sessionErrorCode))
            {
                error = NnrpParseError.SourceTooShort;
                return false;
            }

            if (strict && (reserved0 != 0 || reserved1 != 0))
            {
                error = NnrpParseError.NonZeroReservedField;
                return false;
            }

            if (!TryValidate((SessionCloseStatus)closeStatus, (SessionErrorCode)sessionErrorCode, out error))
            {
                return false;
            }

            metadata = new SessionCloseAckMetadata(
                (SessionCloseStatus)closeStatus,
                lastOperationId,
                (SessionErrorCode)sessionErrorCode);
            return true;
        }

        public bool Equals(SessionCloseAckMetadata other)
        {
            return CloseStatus == other.CloseStatus
                && LastOperationId == other.LastOperationId
                && SessionErrorCode == other.SessionErrorCode;
        }

        public override bool Equals(object obj)
        {
            return obj is SessionCloseAckMetadata other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = CloseStatus.GetHashCode();
                hash = (hash * 397) ^ LastOperationId.GetHashCode();
                hash = (hash * 397) ^ SessionErrorCode.GetHashCode();
                return hash;
            }
        }

        private static bool TryValidate(SessionCloseStatus closeStatus, SessionErrorCode sessionErrorCode, out NnrpParseError error)
        {
            error = NnrpParseError.None;
            if (!Enum.IsDefined(typeof(SessionCloseStatus), closeStatus)
                || !Enum.IsDefined(typeof(SessionErrorCode), sessionErrorCode))
            {
                error = NnrpParseError.InvalidMessageLayout;
                return false;
            }

            return true;
        }

        private static void ThrowValidation(SessionCloseStatus closeStatus, SessionErrorCode sessionErrorCode)
        {
            if (!Enum.IsDefined(typeof(SessionCloseStatus), closeStatus))
            {
                throw new ArgumentOutOfRangeException(nameof(closeStatus));
            }

            if (!Enum.IsDefined(typeof(SessionErrorCode), sessionErrorCode))
            {
                throw new ArgumentOutOfRangeException(nameof(sessionErrorCode));
            }
        }
    }
}
