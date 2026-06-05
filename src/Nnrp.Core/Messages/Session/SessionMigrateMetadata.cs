using System;

namespace Nnrp.Core
{
    public readonly struct SessionMigrateMetadata : IEquatable<SessionMigrateMetadata>
    {
        public const int MetadataLength = (2 * sizeof(uint)) + (2 * sizeof(ulong));

        public SessionMigrateMetadata(
            TransportId oldTransportId,
            TransportId newTransportId,
            ulong lastResultFrameId,
            ulong clientMigrateTimestampMicroseconds)
        {
            if (oldTransportId == TransportId.Unspecified)
            {
                throw new ArgumentOutOfRangeException(nameof(oldTransportId));
            }

            if (newTransportId == TransportId.Unspecified)
            {
                throw new ArgumentOutOfRangeException(nameof(newTransportId));
            }

            OldTransportId = oldTransportId;
            NewTransportId = newTransportId;
            LastResultFrameId = lastResultFrameId;
            ClientMigrateTimestampMicroseconds = clientMigrateTimestampMicroseconds;
        }

        public TransportId OldTransportId { get; }

        public TransportId NewTransportId { get; }

        public ulong LastResultFrameId { get; }

        public ulong ClientMigrateTimestampMicroseconds { get; }

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
            if (!writer.TryWriteUInt32((uint)OldTransportId)
                || !writer.TryWriteUInt32((uint)NewTransportId)
                || !writer.TryWriteUInt64(LastResultFrameId)
                || !writer.TryWriteUInt64(ClientMigrateTimestampMicroseconds))
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

        public static bool TryParse(ReadOnlySpan<byte> source, out SessionMigrateMetadata metadata)
        {
            return TryParse(source, strict: false, out metadata, out _);
        }

        public static bool TryParse(ReadOnlySpan<byte> source, out SessionMigrateMetadata metadata, out NnrpParseError error)
        {
            return TryParse(source, strict: false, out metadata, out error);
        }

        public static bool TryParse(
            ReadOnlySpan<byte> source,
            bool strict,
            out SessionMigrateMetadata metadata,
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
            if (!reader.TryReadUInt32(out var oldTransportId)
                || !reader.TryReadUInt32(out var newTransportId)
                || !reader.TryReadUInt64(out var lastResultFrameId)
                || !reader.TryReadUInt64(out var clientMigrateTimestampMicroseconds))
            {
                error = NnrpParseError.SourceTooShort;
                return false;
            }

            if ((TransportId)oldTransportId == TransportId.Unspecified || (TransportId)newTransportId == TransportId.Unspecified)
            {
                error = NnrpParseError.InvalidMessageLayout;
                return false;
            }

            metadata = new SessionMigrateMetadata(
                (TransportId)oldTransportId,
                (TransportId)newTransportId,
                lastResultFrameId,
                clientMigrateTimestampMicroseconds);
            return true;
        }

        public bool Equals(SessionMigrateMetadata other)
        {
            return OldTransportId == other.OldTransportId
                && NewTransportId == other.NewTransportId
                && LastResultFrameId == other.LastResultFrameId
                && ClientMigrateTimestampMicroseconds == other.ClientMigrateTimestampMicroseconds;
        }

        public override bool Equals(object obj)
        {
            return obj is SessionMigrateMetadata other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = ((uint)OldTransportId).GetHashCode();
                hash = (hash * 397) ^ ((uint)NewTransportId).GetHashCode();
                hash = (hash * 397) ^ LastResultFrameId.GetHashCode();
                hash = (hash * 397) ^ ClientMigrateTimestampMicroseconds.GetHashCode();
                return hash;
            }
        }
    }
}
