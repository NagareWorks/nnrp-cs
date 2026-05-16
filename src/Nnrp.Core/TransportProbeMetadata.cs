using System;

namespace Nnrp.Core
{
    public readonly struct TransportProbeMetadata : IEquatable<TransportProbeMetadata>
    {
        public const int MetadataLength = (2 * sizeof(uint)) + sizeof(ulong);

        public TransportProbeMetadata(
            uint probeId,
            uint probePayloadBytes,
            ulong clientSendTimestampMicroseconds)
        {
            ProbeId = probeId;
            ProbePayloadBytes = probePayloadBytes;
            ClientSendTimestampMicroseconds = clientSendTimestampMicroseconds;
        }

        public uint ProbeId { get; }

        public uint ProbePayloadBytes { get; }

        public ulong ClientSendTimestampMicroseconds { get; }

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
                || !writer.TryWriteUInt32(ProbePayloadBytes)
                || !writer.TryWriteUInt64(ClientSendTimestampMicroseconds))
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

        public static bool TryParse(ReadOnlySpan<byte> source, out TransportProbeMetadata metadata)
        {
            return TryParse(source, out metadata, out _);
        }

        public static bool TryParse(ReadOnlySpan<byte> source, out TransportProbeMetadata metadata, out NnrpParseError error)
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
                || !reader.TryReadUInt32(out var probePayloadBytes)
                || !reader.TryReadUInt64(out var clientSendTimestampMicroseconds))
            {
                error = NnrpParseError.SourceTooShort;
                return false;
            }

            metadata = new TransportProbeMetadata(
                probeId,
                probePayloadBytes,
                clientSendTimestampMicroseconds);
            return true;
        }

        public bool Equals(TransportProbeMetadata other)
        {
            return ProbeId == other.ProbeId
                && ProbePayloadBytes == other.ProbePayloadBytes
                && ClientSendTimestampMicroseconds == other.ClientSendTimestampMicroseconds;
        }

        public override bool Equals(object obj)
        {
            return obj is TransportProbeMetadata other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = ProbeId.GetHashCode();
                hash = (hash * 397) ^ ProbePayloadBytes.GetHashCode();
                hash = (hash * 397) ^ ClientSendTimestampMicroseconds.GetHashCode();
                return hash;
            }
        }
    }
}
