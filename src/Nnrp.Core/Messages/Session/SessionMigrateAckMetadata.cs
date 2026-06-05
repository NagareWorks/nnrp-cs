using System;

namespace Nnrp.Core
{
    public readonly struct SessionMigrateAckMetadata : IEquatable<SessionMigrateAckMetadata>
    {
        public const int MetadataLength = (2 * sizeof(uint)) + (2 * sizeof(ulong));

        public SessionMigrateAckMetadata(
            uint acceptCode,
            ulong resumeFromFrameId,
            uint graceWindowMilliseconds,
            ulong serverMigrateTimestampMicroseconds)
        {
            AcceptCode = acceptCode;
            ResumeFromFrameId = resumeFromFrameId;
            GraceWindowMilliseconds = graceWindowMilliseconds;
            ServerMigrateTimestampMicroseconds = serverMigrateTimestampMicroseconds;
        }

        public uint AcceptCode { get; }

        public ulong ResumeFromFrameId { get; }

        public uint GraceWindowMilliseconds { get; }

        public ulong ServerMigrateTimestampMicroseconds { get; }

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
            if (!writer.TryWriteUInt32(AcceptCode)
                || !writer.TryWriteUInt64(ResumeFromFrameId)
                || !writer.TryWriteUInt32(GraceWindowMilliseconds)
                || !writer.TryWriteUInt64(ServerMigrateTimestampMicroseconds))
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

        public static bool TryParse(ReadOnlySpan<byte> source, out SessionMigrateAckMetadata metadata)
        {
            return TryParse(source, strict: false, out metadata, out _);
        }

        public static bool TryParse(ReadOnlySpan<byte> source, out SessionMigrateAckMetadata metadata, out NnrpParseError error)
        {
            return TryParse(source, strict: false, out metadata, out error);
        }

        public static bool TryParse(
            ReadOnlySpan<byte> source,
            bool strict,
            out SessionMigrateAckMetadata metadata,
            out NnrpParseError error)
        {
            metadata = default;
            error = NnrpParseError.None;
            if (source.Length < MetadataLength)
            {
                error = NnrpParseError.SourceTooShort;
                return false;
            }

            var reader = new FixedBinaryReader(source);
            if (!reader.TryReadUInt32(out var acceptCode)
                || !reader.TryReadUInt64(out var resumeFromFrameId)
                || !reader.TryReadUInt32(out var graceWindowMilliseconds)
                || !reader.TryReadUInt64(out var serverMigrateTimestampMicroseconds))
            {
                error = NnrpParseError.SourceTooShort;
                return false;
            }

            metadata = new SessionMigrateAckMetadata(
                acceptCode,
                resumeFromFrameId,
                graceWindowMilliseconds,
                serverMigrateTimestampMicroseconds);
            return true;
        }

        public bool Equals(SessionMigrateAckMetadata other)
        {
            return AcceptCode == other.AcceptCode
                && ResumeFromFrameId == other.ResumeFromFrameId
                && GraceWindowMilliseconds == other.GraceWindowMilliseconds
                && ServerMigrateTimestampMicroseconds == other.ServerMigrateTimestampMicroseconds;
        }

        public override bool Equals(object obj)
        {
            return obj is SessionMigrateAckMetadata other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = AcceptCode.GetHashCode();
                hash = (hash * 397) ^ ResumeFromFrameId.GetHashCode();
                hash = (hash * 397) ^ GraceWindowMilliseconds.GetHashCode();
                hash = (hash * 397) ^ ServerMigrateTimestampMicroseconds.GetHashCode();
                return hash;
            }
        }
    }
}
