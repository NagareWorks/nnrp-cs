using System;

namespace Nnrp.Core
{
    public readonly struct FlowUpdateMetadata : IEquatable<FlowUpdateMetadata>
    {
        public const int MetadataLength = 32;

        private const FlowUpdateFlags AllowedFlags = FlowUpdateFlags.CreditValid
            | FlowUpdateFlags.RetryAfterValid
            | FlowUpdateFlags.BackgroundOnly
            | FlowUpdateFlags.DrainInFlightOnly;

        public FlowUpdateMetadata(
            FlowUpdateScopeKind scopeKind,
            FlowUpdateReason updateReason,
            FlowUpdateBackpressureLevel backpressureLevel,
            ushort connectionCredit,
            ushort sessionCredit,
            ushort operationCredit,
            ulong operationId,
            uint retryAfterMilliseconds,
            uint creditEpoch,
            FlowUpdateFlags flags)
        {
            if (!TryValidate(scopeKind, updateReason, backpressureLevel, connectionCredit, sessionCredit, operationCredit, operationId, retryAfterMilliseconds, flags, out _))
            {
                ThrowValidation(scopeKind, updateReason, backpressureLevel, connectionCredit, sessionCredit, operationCredit, operationId, retryAfterMilliseconds, flags);
            }

            ScopeKind = scopeKind;
            UpdateReason = updateReason;
            BackpressureLevel = backpressureLevel;
            ConnectionCredit = connectionCredit;
            SessionCredit = sessionCredit;
            OperationCredit = operationCredit;
            OperationId = operationId;
            RetryAfterMilliseconds = retryAfterMilliseconds;
            CreditEpoch = creditEpoch;
            Flags = flags;
        }

        public FlowUpdateScopeKind ScopeKind { get; }

        public FlowUpdateReason UpdateReason { get; }

        public FlowUpdateBackpressureLevel BackpressureLevel { get; }

        public ushort ConnectionCredit { get; }

        public ushort SessionCredit { get; }

        public ushort OperationCredit { get; }

        public ulong OperationId { get; }

        public uint RetryAfterMilliseconds { get; }

        public uint CreditEpoch { get; }

        public FlowUpdateFlags Flags { get; }

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
            if (!writer.TryWriteByte((byte)ScopeKind)
                || !writer.TryWriteByte((byte)UpdateReason)
                || !writer.TryWriteByte((byte)BackpressureLevel)
                || !writer.TryWriteByte(0)
                || !writer.TryWriteUInt16(ConnectionCredit)
                || !writer.TryWriteUInt16(SessionCredit)
                || !writer.TryWriteUInt16(OperationCredit)
                || !writer.TryWriteUInt16(0)
                || !writer.TryWriteUInt64(OperationId)
                || !writer.TryWriteUInt32(RetryAfterMilliseconds)
                || !writer.TryWriteUInt32(CreditEpoch)
                || !writer.TryWriteUInt32((uint)Flags))
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

        public static bool TryParse(ReadOnlySpan<byte> source, out FlowUpdateMetadata metadata)
        {
            return TryParse(source, strict: false, out metadata, out _);
        }

        public static bool TryParse(ReadOnlySpan<byte> source, out FlowUpdateMetadata metadata, out NnrpParseError error)
        {
            return TryParse(source, strict: false, out metadata, out error);
        }

        public static bool TryParse(ReadOnlySpan<byte> source, bool strict, out FlowUpdateMetadata metadata, out NnrpParseError error)
        {
            metadata = default;
            error = NnrpParseError.None;
            if (source.Length < MetadataLength)
            {
                error = NnrpParseError.SourceTooShort;
                return false;
            }

            var reader = new FixedBinaryReader(source);
            if (!reader.TryReadByte(out var scopeKind)
                || !reader.TryReadByte(out var updateReason)
                || !reader.TryReadByte(out var backpressureLevel)
                || !reader.TryReadByte(out var reserved0)
                || !reader.TryReadUInt16(out var connectionCredit)
                || !reader.TryReadUInt16(out var sessionCredit)
                || !reader.TryReadUInt16(out var operationCredit)
                || !reader.TryReadUInt16(out var reserved1)
                || !reader.TryReadUInt64(out var operationId)
                || !reader.TryReadUInt32(out var retryAfterMilliseconds)
                || !reader.TryReadUInt32(out var creditEpoch)
                || !reader.TryReadUInt32(out var flags))
            {
                error = NnrpParseError.SourceTooShort;
                return false;
            }

            if (strict && (reserved0 != 0 || reserved1 != 0))
            {
                error = NnrpParseError.NonZeroReservedField;
                return false;
            }

            if (!TryValidate(
                (FlowUpdateScopeKind)scopeKind,
                (FlowUpdateReason)updateReason,
                (FlowUpdateBackpressureLevel)backpressureLevel,
                connectionCredit,
                sessionCredit,
                operationCredit,
                operationId,
                retryAfterMilliseconds,
                (FlowUpdateFlags)flags,
                out error))
            {
                return false;
            }

            metadata = new FlowUpdateMetadata(
                (FlowUpdateScopeKind)scopeKind,
                (FlowUpdateReason)updateReason,
                (FlowUpdateBackpressureLevel)backpressureLevel,
                connectionCredit,
                sessionCredit,
                operationCredit,
                operationId,
                retryAfterMilliseconds,
                creditEpoch,
                (FlowUpdateFlags)flags);
            return true;
        }

        public bool Equals(FlowUpdateMetadata other)
        {
            return ScopeKind == other.ScopeKind
                && UpdateReason == other.UpdateReason
                && BackpressureLevel == other.BackpressureLevel
                && ConnectionCredit == other.ConnectionCredit
                && SessionCredit == other.SessionCredit
                && OperationCredit == other.OperationCredit
                && OperationId == other.OperationId
                && RetryAfterMilliseconds == other.RetryAfterMilliseconds
                && CreditEpoch == other.CreditEpoch
                && Flags == other.Flags;
        }

        public override bool Equals(object obj)
        {
            return obj is FlowUpdateMetadata other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = ScopeKind.GetHashCode();
                hash = (hash * 397) ^ UpdateReason.GetHashCode();
                hash = (hash * 397) ^ BackpressureLevel.GetHashCode();
                hash = (hash * 397) ^ ConnectionCredit.GetHashCode();
                hash = (hash * 397) ^ SessionCredit.GetHashCode();
                hash = (hash * 397) ^ OperationCredit.GetHashCode();
                hash = (hash * 397) ^ OperationId.GetHashCode();
                hash = (hash * 397) ^ RetryAfterMilliseconds.GetHashCode();
                hash = (hash * 397) ^ CreditEpoch.GetHashCode();
                hash = (hash * 397) ^ Flags.GetHashCode();
                return hash;
            }
        }

        private static bool TryValidate(
            FlowUpdateScopeKind scopeKind,
            FlowUpdateReason updateReason,
            FlowUpdateBackpressureLevel backpressureLevel,
            ushort connectionCredit,
            ushort sessionCredit,
            ushort operationCredit,
            ulong operationId,
            uint retryAfterMilliseconds,
            FlowUpdateFlags flags,
            out NnrpParseError error)
        {
            error = NnrpParseError.None;

            if (!Enum.IsDefined(typeof(FlowUpdateScopeKind), scopeKind)
                || !Enum.IsDefined(typeof(FlowUpdateReason), updateReason)
                || !Enum.IsDefined(typeof(FlowUpdateBackpressureLevel), backpressureLevel)
                || ((uint)flags & ~(uint)AllowedFlags) != 0)
            {
                error = NnrpParseError.InvalidMessageLayout;
                return false;
            }

            if (retryAfterMilliseconds != 0 && (flags & FlowUpdateFlags.RetryAfterValid) == 0)
            {
                error = NnrpParseError.InvalidMessageLayout;
                return false;
            }

            switch (scopeKind)
            {
                case FlowUpdateScopeKind.Connection:
                    if (sessionCredit != 0 || operationCredit != 0 || operationId != 0)
                    {
                        error = NnrpParseError.InvalidMessageLayout;
                        return false;
                    }

                    break;
                case FlowUpdateScopeKind.Session:
                    if (connectionCredit != 0 || operationCredit != 0 || operationId != 0)
                    {
                        error = NnrpParseError.InvalidMessageLayout;
                        return false;
                    }

                    break;
                case FlowUpdateScopeKind.Operation:
                    if (connectionCredit != 0 || sessionCredit != 0 || operationId == 0)
                    {
                        error = NnrpParseError.InvalidMessageLayout;
                        return false;
                    }

                    break;
                default:
                    error = NnrpParseError.InvalidMessageLayout;
                    return false;
            }

            return true;
        }

        private static void ThrowValidation(
            FlowUpdateScopeKind scopeKind,
            FlowUpdateReason updateReason,
            FlowUpdateBackpressureLevel backpressureLevel,
            ushort connectionCredit,
            ushort sessionCredit,
            ushort operationCredit,
            ulong operationId,
            uint retryAfterMilliseconds,
            FlowUpdateFlags flags)
        {
            if (!Enum.IsDefined(typeof(FlowUpdateScopeKind), scopeKind))
            {
                throw new ArgumentOutOfRangeException(nameof(scopeKind));
            }

            if (!Enum.IsDefined(typeof(FlowUpdateReason), updateReason))
            {
                throw new ArgumentOutOfRangeException(nameof(updateReason));
            }

            if (!Enum.IsDefined(typeof(FlowUpdateBackpressureLevel), backpressureLevel))
            {
                throw new ArgumentOutOfRangeException(nameof(backpressureLevel));
            }

            if (((uint)flags & ~(uint)AllowedFlags) != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(flags));
            }

            if (retryAfterMilliseconds != 0 && (flags & FlowUpdateFlags.RetryAfterValid) == 0)
            {
                throw new ArgumentException("retryAfterMilliseconds requires FlowUpdateFlags.RetryAfterValid.", nameof(retryAfterMilliseconds));
            }

            switch (scopeKind)
            {
                case FlowUpdateScopeKind.Connection:
                    if (sessionCredit != 0 || operationCredit != 0 || operationId != 0)
                    {
                        throw new ArgumentException("Connection-scope FLOW_UPDATE must not carry session or operation credits.");
                    }

                    break;
                case FlowUpdateScopeKind.Session:
                    if (connectionCredit != 0 || operationCredit != 0 || operationId != 0)
                    {
                        throw new ArgumentException("Session-scope FLOW_UPDATE must not carry connection or operation credits.");
                    }

                    break;
                case FlowUpdateScopeKind.Operation:
                    if (connectionCredit != 0 || sessionCredit != 0)
                    {
                        throw new ArgumentException("Operation-scope FLOW_UPDATE must not carry connection or session credits.");
                    }

                    if (operationId == 0)
                    {
                        throw new ArgumentException("Operation-scope FLOW_UPDATE requires a non-zero operationId.", nameof(operationId));
                    }

                    break;
            }
        }
    }
}
