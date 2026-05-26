using System;

namespace Nnrp.Core
{
    public readonly struct SessionOpenMetadata : IEquatable<SessionOpenMetadata>
    {
        public const int MetadataLength = 48;

        private const SessionFlags AllowedFlags = SessionFlags.AllowResume
            | SessionFlags.AllowBackgroundResults
            | SessionFlags.AllowCacheLeases
            | SessionFlags.AllowSchemaOverride;

        public SessionOpenMetadata(
            uint requestedSessionId,
            ushort profileId,
            SessionPriorityClass priorityClass,
            SessionFlags sessionFlags,
            uint schemaId,
            uint schemaVersion,
            uint defaultDeadlineMilliseconds,
            ushort maxInFlightOperations,
            uint leaseTtlHintMilliseconds,
            uint resumeTokenBytes,
            uint authBytes,
            uint sessionExtensionBytes,
            ulong clientSessionTag)
        {
            if (!TryValidate(priorityClass, sessionFlags, out _))
            {
                ThrowValidation(priorityClass, sessionFlags);
            }

            RequestedSessionId = requestedSessionId;
            ProfileId = profileId;
            PriorityClass = priorityClass;
            SessionFlags = sessionFlags;
            SchemaId = schemaId;
            SchemaVersion = schemaVersion;
            DefaultDeadlineMilliseconds = defaultDeadlineMilliseconds;
            MaxInFlightOperations = maxInFlightOperations;
            LeaseTtlHintMilliseconds = leaseTtlHintMilliseconds;
            ResumeTokenBytes = resumeTokenBytes;
            AuthBytes = authBytes;
            SessionExtensionBytes = sessionExtensionBytes;
            ClientSessionTag = clientSessionTag;
        }

        public uint RequestedSessionId { get; }

        public ushort ProfileId { get; }

        public SessionPriorityClass PriorityClass { get; }

        public SessionFlags SessionFlags { get; }

        public uint SchemaId { get; }

        public uint SchemaVersion { get; }

        public uint DefaultDeadlineMilliseconds { get; }

        public ushort MaxInFlightOperations { get; }

        public uint LeaseTtlHintMilliseconds { get; }

        public uint ResumeTokenBytes { get; }

        public uint AuthBytes { get; }

        public uint SessionExtensionBytes { get; }

        public ulong ClientSessionTag { get; }

        public uint BodyLength => checked(ResumeTokenBytes + AuthBytes + SessionExtensionBytes);

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
            if (!writer.TryWriteUInt32(RequestedSessionId)
                || !writer.TryWriteUInt16(ProfileId)
                || !writer.TryWriteByte((byte)PriorityClass)
                || !writer.TryWriteByte((byte)SessionFlags)
                || !writer.TryWriteUInt32(SchemaId)
                || !writer.TryWriteUInt32(SchemaVersion)
                || !writer.TryWriteUInt32(DefaultDeadlineMilliseconds)
                || !writer.TryWriteUInt16(MaxInFlightOperations)
                || !writer.TryWriteUInt16(0)
                || !writer.TryWriteUInt32(LeaseTtlHintMilliseconds)
                || !writer.TryWriteUInt32(ResumeTokenBytes)
                || !writer.TryWriteUInt32(AuthBytes)
                || !writer.TryWriteUInt32(SessionExtensionBytes)
                || !writer.TryWriteUInt64(ClientSessionTag))
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

        public static bool TryParse(ReadOnlySpan<byte> source, out SessionOpenMetadata metadata)
        {
            return TryParse(source, strict: false, out metadata, out _);
        }

        public static bool TryParse(ReadOnlySpan<byte> source, out SessionOpenMetadata metadata, out NnrpParseError error)
        {
            return TryParse(source, strict: false, out metadata, out error);
        }

        public static bool TryParse(ReadOnlySpan<byte> source, bool strict, out SessionOpenMetadata metadata, out NnrpParseError error)
        {
            metadata = default;
            error = NnrpParseError.None;
            if (source.Length < MetadataLength)
            {
                error = NnrpParseError.SourceTooShort;
                return false;
            }

            var reader = new FixedBinaryReader(source);
            if (!reader.TryReadUInt32(out var requestedSessionId)
                || !reader.TryReadUInt16(out var profileId)
                || !reader.TryReadByte(out var priorityClass)
                || !reader.TryReadByte(out var sessionFlags)
                || !reader.TryReadUInt32(out var schemaId)
                || !reader.TryReadUInt32(out var schemaVersion)
                || !reader.TryReadUInt32(out var defaultDeadlineMilliseconds)
                || !reader.TryReadUInt16(out var maxInFlightOperations)
                || !reader.TryReadUInt16(out var reserved0)
                || !reader.TryReadUInt32(out var leaseTtlHintMilliseconds)
                || !reader.TryReadUInt32(out var resumeTokenBytes)
                || !reader.TryReadUInt32(out var authBytes)
                || !reader.TryReadUInt32(out var sessionExtensionBytes)
                || !reader.TryReadUInt64(out var clientSessionTag))
            {
                error = NnrpParseError.SourceTooShort;
                return false;
            }

            if (strict && reserved0 != 0)
            {
                error = NnrpParseError.NonZeroReservedField;
                return false;
            }

            if (!TryValidate((SessionPriorityClass)priorityClass, (SessionFlags)sessionFlags, out error))
            {
                return false;
            }

            metadata = new SessionOpenMetadata(
                requestedSessionId,
                profileId,
                (SessionPriorityClass)priorityClass,
                (SessionFlags)sessionFlags,
                schemaId,
                schemaVersion,
                defaultDeadlineMilliseconds,
                maxInFlightOperations,
                leaseTtlHintMilliseconds,
                resumeTokenBytes,
                authBytes,
                sessionExtensionBytes,
                clientSessionTag);
            return true;
        }

        public bool Equals(SessionOpenMetadata other)
        {
            return RequestedSessionId == other.RequestedSessionId
                && ProfileId == other.ProfileId
                && PriorityClass == other.PriorityClass
                && SessionFlags == other.SessionFlags
                && SchemaId == other.SchemaId
                && SchemaVersion == other.SchemaVersion
                && DefaultDeadlineMilliseconds == other.DefaultDeadlineMilliseconds
                && MaxInFlightOperations == other.MaxInFlightOperations
                && LeaseTtlHintMilliseconds == other.LeaseTtlHintMilliseconds
                && ResumeTokenBytes == other.ResumeTokenBytes
                && AuthBytes == other.AuthBytes
                && SessionExtensionBytes == other.SessionExtensionBytes
                && ClientSessionTag == other.ClientSessionTag;
        }

        public override bool Equals(object obj)
        {
            return obj is SessionOpenMetadata other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = RequestedSessionId.GetHashCode();
                hash = (hash * 397) ^ ProfileId.GetHashCode();
                hash = (hash * 397) ^ PriorityClass.GetHashCode();
                hash = (hash * 397) ^ SessionFlags.GetHashCode();
                hash = (hash * 397) ^ SchemaId.GetHashCode();
                hash = (hash * 397) ^ SchemaVersion.GetHashCode();
                hash = (hash * 397) ^ DefaultDeadlineMilliseconds.GetHashCode();
                hash = (hash * 397) ^ MaxInFlightOperations.GetHashCode();
                hash = (hash * 397) ^ LeaseTtlHintMilliseconds.GetHashCode();
                hash = (hash * 397) ^ ResumeTokenBytes.GetHashCode();
                hash = (hash * 397) ^ AuthBytes.GetHashCode();
                hash = (hash * 397) ^ SessionExtensionBytes.GetHashCode();
                hash = (hash * 397) ^ ClientSessionTag.GetHashCode();
                return hash;
            }
        }

        private static bool TryValidate(SessionPriorityClass priorityClass, SessionFlags sessionFlags, out NnrpParseError error)
        {
            error = NnrpParseError.None;
            if (!Enum.IsDefined(typeof(SessionPriorityClass), priorityClass)
                || ((byte)sessionFlags & ~(byte)AllowedFlags) != 0)
            {
                error = NnrpParseError.InvalidMessageLayout;
                return false;
            }

            return true;
        }

        private static void ThrowValidation(SessionPriorityClass priorityClass, SessionFlags sessionFlags)
        {
            if (!Enum.IsDefined(typeof(SessionPriorityClass), priorityClass))
            {
                throw new ArgumentOutOfRangeException(nameof(priorityClass));
            }

            if (((byte)sessionFlags & ~(byte)AllowedFlags) != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sessionFlags));
            }
        }
    }
}
