using System;

namespace Nnrp.Core
{
    public readonly struct TransportProbeAckMetadata : IEquatable<TransportProbeAckMetadata>
    {
        public const int MetadataLength = (2 * sizeof(uint)) + sizeof(ulong);

        public TransportProbeAckMetadata(
            uint probeId,
            uint reserved,
            ulong serverReceiveTimestampMicroseconds)
        {
            ProbeId = probeId;
            Reserved = reserved;
            ServerReceiveTimestampMicroseconds = serverReceiveTimestampMicroseconds;
        }

        public uint ProbeId { get; }

        public uint Reserved { get; }

        public ulong ServerReceiveTimestampMicroseconds { get; }

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
            if (!writer.TryWriteUInt32(ProbeId)
                || !writer.TryWriteUInt32(Reserved)
                || !writer.TryWriteUInt64(ServerReceiveTimestampMicroseconds))
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

        public static bool TryParse(ReadOnlySpan<byte> source, out TransportProbeAckMetadata metadata)
        {
            return TryParse(source, out metadata, out _);
        }

        public static bool TryParse(ReadOnlySpan<byte> source, out TransportProbeAckMetadata metadata, out NnrpParseError error)
        {
            metadata = default;
            error = NnrpParseError.None;
            if (source.Length < MetadataLength)
            {
                error = NnrpParseError.SourceTooShort;
                return false;
            }

            var reader = new FixedBinaryReader(source);
            if (!reader.TryReadUInt32(out var probeId)
                || !reader.TryReadUInt32(out var reserved)
                || !reader.TryReadUInt64(out var serverReceiveTimestampMicroseconds))
            {
                error = NnrpParseError.SourceTooShort;
                return false;
            }

            metadata = new TransportProbeAckMetadata(
                probeId,
                reserved,
                serverReceiveTimestampMicroseconds);
            return true;
        }

        public bool Equals(TransportProbeAckMetadata other)
        {
            return ProbeId == other.ProbeId
                && Reserved == other.Reserved
                && ServerReceiveTimestampMicroseconds == other.ServerReceiveTimestampMicroseconds;
        }

        public override bool Equals(object obj)
        {
            return obj is TransportProbeAckMetadata other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = ProbeId.GetHashCode();
                hash = (hash * 397) ^ Reserved.GetHashCode();
                hash = (hash * 397) ^ ServerReceiveTimestampMicroseconds.GetHashCode();
                return hash;
            }
        }
    }
}
