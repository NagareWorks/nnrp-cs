using System;

namespace Nnrp.Core
{
    public readonly struct CacheAckMetadata : IEquatable<CacheAckMetadata>
    {
        public const int MetadataLength = 40;

        public CacheAckMetadata(
            uint cacheNamespace,
            ulong cacheKeyHigh,
            ulong cacheKeyLow,
            CacheAckStatus status,
            uint acceptedTtlMilliseconds,
            uint maxObjectBytes,
            uint detailCode)
        {
            if (!Enum.IsDefined(typeof(CacheAckStatus), status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }

            CacheNamespace = cacheNamespace;
            CacheKeyHigh = cacheKeyHigh;
            CacheKeyLow = cacheKeyLow;
            Status = status;
            AcceptedTtlMilliseconds = acceptedTtlMilliseconds;
            MaxObjectBytes = maxObjectBytes;
            DetailCode = detailCode;
        }

        public uint CacheNamespace { get; }
        public ulong CacheKeyHigh { get; }
        public ulong CacheKeyLow { get; }
        public CacheAckStatus Status { get; }
        public uint AcceptedTtlMilliseconds { get; }
        public uint MaxObjectBytes { get; }
        public uint DetailCode { get; }

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
            if (!writer.TryWriteUInt32(CacheNamespace)
                || !writer.TryWriteUInt32((uint)Status)
                || !writer.TryWriteUInt64(CacheKeyHigh)
                || !writer.TryWriteUInt64(CacheKeyLow)
                || !writer.TryWriteUInt32(AcceptedTtlMilliseconds)
                || !writer.TryWriteUInt32(MaxObjectBytes)
                || !writer.TryWriteUInt32(DetailCode)
                || !writer.TryWriteUInt32(0))
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

        public static bool TryParse(ReadOnlySpan<byte> source, out CacheAckMetadata metadata)
        {
            return TryParse(source, out metadata, out _);
        }

        public static bool TryParse(ReadOnlySpan<byte> source, out CacheAckMetadata metadata, out NnrpParseError error)
        {
            metadata = default;
            error = NnrpParseError.None;
            if (source.Length < MetadataLength)
            {
                error = NnrpParseError.SourceTooShort;
                return false;
            }

            var reader = new FixedBinaryReader(source);
            if (!reader.TryReadUInt32(out var cacheNamespace)
                || !reader.TryReadUInt32(out var status)
                || !reader.TryReadUInt64(out var cacheKeyHigh)
                || !reader.TryReadUInt64(out var cacheKeyLow)
                || !reader.TryReadUInt32(out var acceptedTtlMilliseconds)
                || !reader.TryReadUInt32(out var maxObjectBytes)
                || !reader.TryReadUInt32(out var detailCode)
                || !reader.TryReadUInt32(out var reserved))
            {
                error = NnrpParseError.SourceTooShort;
                return false;
            }

            if (!Enum.IsDefined(typeof(CacheAckStatus), status) || reserved != 0)
            {
                error = reserved != 0
                    ? NnrpParseError.NonZeroReservedField
                    : NnrpParseError.InvalidMessageLayout;
                return false;
            }

            metadata = new CacheAckMetadata(
                cacheNamespace,
                cacheKeyHigh,
                cacheKeyLow,
                (CacheAckStatus)status,
                acceptedTtlMilliseconds,
                maxObjectBytes,
                detailCode);
            return true;
        }

        public bool Equals(CacheAckMetadata other)
        {
            return CacheNamespace == other.CacheNamespace
                && CacheKeyHigh == other.CacheKeyHigh
                && CacheKeyLow == other.CacheKeyLow
                && Status == other.Status
                && AcceptedTtlMilliseconds == other.AcceptedTtlMilliseconds
                && MaxObjectBytes == other.MaxObjectBytes
                && DetailCode == other.DetailCode;
        }

        public override bool Equals(object obj)
        {
            return obj is CacheAckMetadata other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = CacheNamespace.GetHashCode();
                hash = (hash * 397) ^ CacheKeyHigh.GetHashCode();
                hash = (hash * 397) ^ CacheKeyLow.GetHashCode();
                hash = (hash * 397) ^ Status.GetHashCode();
                hash = (hash * 397) ^ AcceptedTtlMilliseconds.GetHashCode();
                hash = (hash * 397) ^ MaxObjectBytes.GetHashCode();
                hash = (hash * 397) ^ DetailCode.GetHashCode();
                return hash;
            }
        }
    }
}
