using System;

namespace Nnrp.Core
{
    public readonly struct SessionOpenAckMetadata : IEquatable<SessionOpenAckMetadata>
    {
        public const int MetadataLength = 56;

        private const SessionAckFlags AllowedFlags = SessionAckFlags.ResumeEnabled
            | SessionAckFlags.BackgroundResultsEnabled
            | SessionAckFlags.CacheLeasesEnabled
            | SessionAckFlags.SchemaOverrideEnabled
            | SessionAckFlags.PriorityDowngraded;

        public SessionOpenAckMetadata(
            uint sessionId,
            ushort acceptedProfileId,
            SessionPriorityClass acceptedPriorityClass,
            SessionStatus sessionStatus,
            uint schemaId,
            uint schemaVersion,
            ushort grantedOperationCredit,
            ushort maxInFlightOperations,
            uint leaseTtlMilliseconds,
            uint resumeWindowMilliseconds,
            uint resumeTokenBytes,
            uint sessionExtensionBytes,
            ulong serverSessionTag,
            uint routeScopeId,
            SessionErrorCode sessionErrorCode,
            SessionAckFlags sessionFlagsAck)
        {
            if (!TryValidate(acceptedPriorityClass, sessionStatus, sessionErrorCode, sessionFlagsAck, out _))
            {
                ThrowValidation(acceptedPriorityClass, sessionStatus, sessionErrorCode, sessionFlagsAck);
            }

            SessionId = sessionId;
            AcceptedProfileId = acceptedProfileId;
            AcceptedPriorityClass = acceptedPriorityClass;
            SessionStatus = sessionStatus;
            SchemaId = schemaId;
            SchemaVersion = schemaVersion;
            GrantedOperationCredit = grantedOperationCredit;
            MaxInFlightOperations = maxInFlightOperations;
            LeaseTtlMilliseconds = leaseTtlMilliseconds;
            ResumeWindowMilliseconds = resumeWindowMilliseconds;
            ResumeTokenBytes = resumeTokenBytes;
            SessionExtensionBytes = sessionExtensionBytes;
            ServerSessionTag = serverSessionTag;
            RouteScopeId = routeScopeId;
            SessionErrorCode = sessionErrorCode;
            SessionFlagsAck = sessionFlagsAck;
        }

        public uint SessionId { get; }

        public ushort AcceptedProfileId { get; }

        public SessionPriorityClass AcceptedPriorityClass { get; }

        public SessionStatus SessionStatus { get; }

        public uint SchemaId { get; }

        public uint SchemaVersion { get; }

        public ushort GrantedOperationCredit { get; }

        public ushort MaxInFlightOperations { get; }

        public uint LeaseTtlMilliseconds { get; }

        public uint ResumeWindowMilliseconds { get; }

        public uint ResumeTokenBytes { get; }

        public uint SessionExtensionBytes { get; }

        public ulong ServerSessionTag { get; }

        public uint RouteScopeId { get; }

        public SessionErrorCode SessionErrorCode { get; }

        public SessionAckFlags SessionFlagsAck { get; }

        public SessionOpenDiagnostic Diagnostic => SessionOpenDiagnostic.FromAck(this);

        public uint BodyLength => checked(ResumeTokenBytes + SessionExtensionBytes);

        public SessionOpenDiagnostic GetDiagnostic(SessionOpenMetadata request)
        {
            return SessionOpenDiagnostic.FromAck(request, this);
        }

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
            if (!writer.TryWriteUInt32(SessionId)
                || !writer.TryWriteUInt16(AcceptedProfileId)
                || !writer.TryWriteByte((byte)AcceptedPriorityClass)
                || !writer.TryWriteByte((byte)SessionStatus)
                || !writer.TryWriteUInt32(SchemaId)
                || !writer.TryWriteUInt32(SchemaVersion)
                || !writer.TryWriteUInt16(GrantedOperationCredit)
                || !writer.TryWriteUInt16(MaxInFlightOperations)
                || !writer.TryWriteUInt32(LeaseTtlMilliseconds)
                || !writer.TryWriteUInt32(ResumeWindowMilliseconds)
                || !writer.TryWriteUInt32(ResumeTokenBytes)
                || !writer.TryWriteUInt32(SessionExtensionBytes)
                || !writer.TryWriteUInt64(ServerSessionTag)
                || !writer.TryWriteUInt32(RouteScopeId)
                || !writer.TryWriteUInt32((uint)SessionErrorCode)
                || !writer.TryWriteUInt32((uint)SessionFlagsAck))
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

        public static bool TryParse(ReadOnlySpan<byte> source, out SessionOpenAckMetadata metadata)
        {
            return TryParse(source, out metadata, out _);
        }

        public static bool TryParse(ReadOnlySpan<byte> source, out SessionOpenAckMetadata metadata, out NnrpParseError error)
        {
            metadata = default;
            error = NnrpParseError.None;
            if (source.Length < MetadataLength)
            {
                error = NnrpParseError.SourceTooShort;
                return false;
            }

            var reader = new FixedBinaryReader(source);
            if (!reader.TryReadUInt32(out var sessionId)
                || !reader.TryReadUInt16(out var acceptedProfileId)
                || !reader.TryReadByte(out var acceptedPriorityClass)
                || !reader.TryReadByte(out var sessionStatus)
                || !reader.TryReadUInt32(out var schemaId)
                || !reader.TryReadUInt32(out var schemaVersion)
                || !reader.TryReadUInt16(out var grantedOperationCredit)
                || !reader.TryReadUInt16(out var maxInFlightOperations)
                || !reader.TryReadUInt32(out var leaseTtlMilliseconds)
                || !reader.TryReadUInt32(out var resumeWindowMilliseconds)
                || !reader.TryReadUInt32(out var resumeTokenBytes)
                || !reader.TryReadUInt32(out var sessionExtensionBytes)
                || !reader.TryReadUInt64(out var serverSessionTag)
                || !reader.TryReadUInt32(out var routeScopeId)
                || !reader.TryReadUInt32(out var sessionErrorCode)
                || !reader.TryReadUInt32(out var sessionFlagsAck))
            {
                error = NnrpParseError.SourceTooShort;
                return false;
            }

            if (!TryValidate(
                (SessionPriorityClass)acceptedPriorityClass,
                (SessionStatus)sessionStatus,
                (SessionErrorCode)sessionErrorCode,
                (SessionAckFlags)sessionFlagsAck,
                out error))
            {
                return false;
            }

            metadata = new SessionOpenAckMetadata(
                sessionId,
                acceptedProfileId,
                (SessionPriorityClass)acceptedPriorityClass,
                (SessionStatus)sessionStatus,
                schemaId,
                schemaVersion,
                grantedOperationCredit,
                maxInFlightOperations,
                leaseTtlMilliseconds,
                resumeWindowMilliseconds,
                resumeTokenBytes,
                sessionExtensionBytes,
                serverSessionTag,
                routeScopeId,
                (SessionErrorCode)sessionErrorCode,
                (SessionAckFlags)sessionFlagsAck);
            return true;
        }

        public bool Equals(SessionOpenAckMetadata other)
        {
            return SessionId == other.SessionId
                && AcceptedProfileId == other.AcceptedProfileId
                && AcceptedPriorityClass == other.AcceptedPriorityClass
                && SessionStatus == other.SessionStatus
                && SchemaId == other.SchemaId
                && SchemaVersion == other.SchemaVersion
                && GrantedOperationCredit == other.GrantedOperationCredit
                && MaxInFlightOperations == other.MaxInFlightOperations
                && LeaseTtlMilliseconds == other.LeaseTtlMilliseconds
                && ResumeWindowMilliseconds == other.ResumeWindowMilliseconds
                && ResumeTokenBytes == other.ResumeTokenBytes
                && SessionExtensionBytes == other.SessionExtensionBytes
                && ServerSessionTag == other.ServerSessionTag
                && RouteScopeId == other.RouteScopeId
                && SessionErrorCode == other.SessionErrorCode
                && SessionFlagsAck == other.SessionFlagsAck;
        }

        public override bool Equals(object obj)
        {
            return obj is SessionOpenAckMetadata other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = SessionId.GetHashCode();
                hash = (hash * 397) ^ AcceptedProfileId.GetHashCode();
                hash = (hash * 397) ^ AcceptedPriorityClass.GetHashCode();
                hash = (hash * 397) ^ SessionStatus.GetHashCode();
                hash = (hash * 397) ^ SchemaId.GetHashCode();
                hash = (hash * 397) ^ SchemaVersion.GetHashCode();
                hash = (hash * 397) ^ GrantedOperationCredit.GetHashCode();
                hash = (hash * 397) ^ MaxInFlightOperations.GetHashCode();
                hash = (hash * 397) ^ LeaseTtlMilliseconds.GetHashCode();
                hash = (hash * 397) ^ ResumeWindowMilliseconds.GetHashCode();
                hash = (hash * 397) ^ ResumeTokenBytes.GetHashCode();
                hash = (hash * 397) ^ SessionExtensionBytes.GetHashCode();
                hash = (hash * 397) ^ ServerSessionTag.GetHashCode();
                hash = (hash * 397) ^ RouteScopeId.GetHashCode();
                hash = (hash * 397) ^ SessionErrorCode.GetHashCode();
                hash = (hash * 397) ^ SessionFlagsAck.GetHashCode();
                return hash;
            }
        }

        private static bool TryValidate(
            SessionPriorityClass acceptedPriorityClass,
            SessionStatus sessionStatus,
            SessionErrorCode sessionErrorCode,
            SessionAckFlags sessionFlagsAck,
            out NnrpParseError error)
        {
            error = NnrpParseError.None;
            if (!Enum.IsDefined(typeof(SessionPriorityClass), acceptedPriorityClass)
                || !Enum.IsDefined(typeof(SessionStatus), sessionStatus)
                || !Enum.IsDefined(typeof(SessionErrorCode), sessionErrorCode)
                || ((uint)sessionFlagsAck & ~(uint)AllowedFlags) != 0)
            {
                error = NnrpParseError.InvalidMessageLayout;
                return false;
            }

            return true;
        }

        private static void ThrowValidation(
            SessionPriorityClass acceptedPriorityClass,
            SessionStatus sessionStatus,
            SessionErrorCode sessionErrorCode,
            SessionAckFlags sessionFlagsAck)
        {
            if (!Enum.IsDefined(typeof(SessionPriorityClass), acceptedPriorityClass))
            {
                throw new ArgumentOutOfRangeException(nameof(acceptedPriorityClass));
            }

            if (!Enum.IsDefined(typeof(SessionStatus), sessionStatus))
            {
                throw new ArgumentOutOfRangeException(nameof(sessionStatus));
            }

            if (!Enum.IsDefined(typeof(SessionErrorCode), sessionErrorCode))
            {
                throw new ArgumentOutOfRangeException(nameof(sessionErrorCode));
            }

            if (((uint)sessionFlagsAck & ~(uint)AllowedFlags) != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sessionFlagsAck));
            }
        }
    }
}
