using System;

namespace Nnrp.Core
{
    public readonly struct TensorSubmitBlock : IEquatable<TensorSubmitBlock>
    {
        public const int BlockLength = 32;

        public TensorSubmitBlock(
            ushort sourceWidth,
            ushort sourceHeight,
            ushort tileWidth,
            ushort tileHeight,
            ushort tileCount,
            ushort sectionCount,
            TileIndexMode tileIndexMode,
            byte tensorFlags,
            ushort reserved0,
            uint tileBaseId,
            uint cameraBytes,
            uint tileIndexBytes,
            uint reserved1 = 0)
        {
            SourceWidth = sourceWidth;
            SourceHeight = sourceHeight;
            TileWidth = tileWidth;
            TileHeight = tileHeight;
            TileCount = tileCount;
            SectionCount = sectionCount;
            TileIndexMode = tileIndexMode;
            TensorFlags = tensorFlags;
            Reserved0 = reserved0;
            TileBaseId = tileBaseId;
            CameraBytes = cameraBytes;
            TileIndexBytes = tileIndexBytes;
            Reserved1 = reserved1;
        }

        public ushort SourceWidth { get; }
        public ushort SourceHeight { get; }
        public ushort TileWidth { get; }
        public ushort TileHeight { get; }
        public ushort TileCount { get; }
        public ushort SectionCount { get; }
        public TileIndexMode TileIndexMode { get; }
        public byte TensorFlags { get; }
        public ushort Reserved0 { get; }
        public uint TileBaseId { get; }
        public uint CameraBytes { get; }
        public uint TileIndexBytes { get; }
        public uint Reserved1 { get; }

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
            if (!writer.TryWriteUInt16(SourceWidth)
                || !writer.TryWriteUInt16(SourceHeight)
                || !writer.TryWriteUInt16(TileWidth)
                || !writer.TryWriteUInt16(TileHeight)
                || !writer.TryWriteUInt16(TileCount)
                || !writer.TryWriteUInt16(SectionCount)
                || !writer.TryWriteByte((byte)TileIndexMode)
                || !writer.TryWriteByte(TensorFlags)
                || !writer.TryWriteUInt16(Reserved0)
                || !writer.TryWriteUInt32(TileBaseId)
                || !writer.TryWriteUInt32(CameraBytes)
                || !writer.TryWriteUInt32(TileIndexBytes)
                || !writer.TryWriteUInt32(Reserved1))
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

        public static bool TryParse(ReadOnlySpan<byte> source, out TensorSubmitBlock block)
        {
            return TryParse(source, out block, out _);
        }

        public static bool TryParse(ReadOnlySpan<byte> source, out TensorSubmitBlock block, out NnrpParseError error)
        {
            block = default;
            error = NnrpParseError.None;
            if (source.Length < BlockLength)
            {
                error = NnrpParseError.SourceTooShort;
                return false;
            }

            var reader = new FixedBinaryReader(source);
            if (!reader.TryReadUInt16(out var sourceWidth)
                || !reader.TryReadUInt16(out var sourceHeight)
                || !reader.TryReadUInt16(out var tileWidth)
                || !reader.TryReadUInt16(out var tileHeight)
                || !reader.TryReadUInt16(out var tileCount)
                || !reader.TryReadUInt16(out var sectionCount)
                || !reader.TryReadByte(out var tileIndexMode)
                || !reader.TryReadByte(out var tensorFlags)
                || !reader.TryReadUInt16(out var reserved0)
                || !reader.TryReadUInt32(out var tileBaseId)
                || !reader.TryReadUInt32(out var cameraBytes)
                || !reader.TryReadUInt32(out var tileIndexBytes)
                || !reader.TryReadUInt32(out var reserved1))
            {
                error = NnrpParseError.SourceTooShort;
                return false;
            }

            block = new TensorSubmitBlock(
                sourceWidth,
                sourceHeight,
                tileWidth,
                tileHeight,
                tileCount,
                sectionCount,
                (TileIndexMode)tileIndexMode,
                tensorFlags,
                reserved0,
                tileBaseId,
                cameraBytes,
                tileIndexBytes,
                reserved1);
            return true;
        }

        public bool Equals(TensorSubmitBlock other)
        {
            return SourceWidth == other.SourceWidth
                && SourceHeight == other.SourceHeight
                && TileWidth == other.TileWidth
                && TileHeight == other.TileHeight
                && TileCount == other.TileCount
                && SectionCount == other.SectionCount
                && TileIndexMode == other.TileIndexMode
                && TensorFlags == other.TensorFlags
                && Reserved0 == other.Reserved0
                && TileBaseId == other.TileBaseId
                && CameraBytes == other.CameraBytes
                && TileIndexBytes == other.TileIndexBytes
                && Reserved1 == other.Reserved1;
        }

        public override bool Equals(object obj)
        {
            return obj is TensorSubmitBlock other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = SourceWidth.GetHashCode();
                hash = (hash * 397) ^ SourceHeight.GetHashCode();
                hash = (hash * 397) ^ TileWidth.GetHashCode();
                hash = (hash * 397) ^ TileHeight.GetHashCode();
                hash = (hash * 397) ^ TileCount.GetHashCode();
                hash = (hash * 397) ^ SectionCount.GetHashCode();
                hash = (hash * 397) ^ TileIndexMode.GetHashCode();
                hash = (hash * 397) ^ TensorFlags.GetHashCode();
                hash = (hash * 397) ^ Reserved0.GetHashCode();
                hash = (hash * 397) ^ TileBaseId.GetHashCode();
                hash = (hash * 397) ^ CameraBytes.GetHashCode();
                hash = (hash * 397) ^ TileIndexBytes.GetHashCode();
                hash = (hash * 397) ^ Reserved1.GetHashCode();
                return hash;
            }
        }
    }
}
