using System;

namespace Nnrp.Core
{
    public readonly struct TensorResultBlock : IEquatable<TensorResultBlock>
    {
        public const int BlockLength = 16;

        public TensorResultBlock(
            ushort sectionCount,
            ushort tileCount,
            TileIndexMode tileIndexMode,
            byte tensorFlags,
            ushort reserved0,
            uint tileBaseId,
            uint tileIndexBytes)
        {
            SectionCount = sectionCount;
            TileCount = tileCount;
            TileIndexMode = tileIndexMode;
            TensorFlags = tensorFlags;
            Reserved0 = reserved0;
            TileBaseId = tileBaseId;
            TileIndexBytes = tileIndexBytes;
        }

        public ushort SectionCount { get; }

        public ushort TileCount { get; }

        public TileIndexMode TileIndexMode { get; }

        public byte TensorFlags { get; }

        public ushort Reserved0 { get; }

        public uint TileBaseId { get; }

        public uint TileIndexBytes { get; }

        public void Write(Span<byte> destination)
        {
            if (!TryWrite(destination, out _))
            {
                throw new ArgumentException($"Destination must be at least {BlockLength} bytes.", nameof(destination));
            }
        }

        public bool TryWrite(Span<byte> destination, out int bytesWritten)
        {
            bytesWritten = 0;
            if (destination.Length < BlockLength)
            {
                return false;
            }

            var writer = new FixedBinaryWriter(destination);
            if (!writer.TryWriteUInt16(SectionCount)
                || !writer.TryWriteUInt16(TileCount)
                || !writer.TryWriteByte((byte)TileIndexMode)
                || !writer.TryWriteByte(TensorFlags)
                || !writer.TryWriteUInt16(Reserved0)
                || !writer.TryWriteUInt32(TileBaseId)
                || !writer.TryWriteUInt32(TileIndexBytes))
            {
                return false;
            }

            bytesWritten = writer.Offset;
            return bytesWritten == BlockLength;
        }

        public byte[] ToArray()
        {
            var payload = new byte[BlockLength];
            Write(payload);
            return payload;
        }

        public static bool TryParse(ReadOnlySpan<byte> source, out TensorResultBlock block)
        {
            return TryParse(source, out block, out _);
        }

        public static bool TryParse(ReadOnlySpan<byte> source, out TensorResultBlock block, out NnrpParseError error)
        {
            block = default;
            error = NnrpParseError.None;
            if (source.Length < BlockLength)
            {
                error = NnrpParseError.SourceTooShort;
                return false;
            }

            var reader = new FixedBinaryReader(source);
            if (!reader.TryReadUInt16(out var sectionCount)
                || !reader.TryReadUInt16(out var tileCount)
                || !reader.TryReadByte(out var tileIndexMode)
                || !reader.TryReadByte(out var tensorFlags)
                || !reader.TryReadUInt16(out var reserved0)
                || !reader.TryReadUInt32(out var tileBaseId)
                || !reader.TryReadUInt32(out var tileIndexBytes))
            {
                error = NnrpParseError.SourceTooShort;
                return false;
            }

            block = new TensorResultBlock(
                sectionCount,
                tileCount,
                (TileIndexMode)tileIndexMode,
                tensorFlags,
                reserved0,
                tileBaseId,
                tileIndexBytes);
            return true;
        }

        public bool Equals(TensorResultBlock other)
        {
            return SectionCount == other.SectionCount
                && TileCount == other.TileCount
                && TileIndexMode == other.TileIndexMode
                && TensorFlags == other.TensorFlags
                && Reserved0 == other.Reserved0
                && TileBaseId == other.TileBaseId
                && TileIndexBytes == other.TileIndexBytes;
        }

        public override bool Equals(object obj)
        {
            return obj is TensorResultBlock other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = SectionCount.GetHashCode();
                hash = (hash * 397) ^ TileCount.GetHashCode();
                hash = (hash * 397) ^ TileIndexMode.GetHashCode();
                hash = (hash * 397) ^ TensorFlags.GetHashCode();
                hash = (hash * 397) ^ Reserved0.GetHashCode();
                hash = (hash * 397) ^ TileBaseId.GetHashCode();
                hash = (hash * 397) ^ TileIndexBytes.GetHashCode();
                return hash;
            }
        }
    }
}