using System;

namespace Nnrp.Core
{
    public readonly struct TensorProfilePatchAckBlock : IEquatable<TensorProfilePatchAckBlock>
    {
        public const int BlockLength = 4 * sizeof(uint);

        public TensorProfilePatchAckBlock(uint minWidth, uint minHeight, uint maxWidth, uint maxHeight)
        {
            MinWidth = minWidth;
            MinHeight = minHeight;
            MaxWidth = maxWidth;
            MaxHeight = maxHeight;
        }

        public uint MinWidth { get; }
        public uint MinHeight { get; }
        public uint MaxWidth { get; }
        public uint MaxHeight { get; }

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
            if (!writer.TryWriteUInt32(MinWidth)
                || !writer.TryWriteUInt32(MinHeight)
                || !writer.TryWriteUInt32(MaxWidth)
                || !writer.TryWriteUInt32(MaxHeight))
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

        public static bool TryParse(ReadOnlySpan<byte> source, out TensorProfilePatchAckBlock block)
        {
            return TryParse(source, out block, out _);
        }

        public static bool TryParse(ReadOnlySpan<byte> source, out TensorProfilePatchAckBlock block, out NnrpParseError error)
        {
            block = default;
            error = NnrpParseError.None;
            if (source.Length < BlockLength)
            {
                error = NnrpParseError.SourceTooShort;
                return false;
            }

            var reader = new FixedBinaryReader(source);
            if (!reader.TryReadUInt32(out var minWidth)
                || !reader.TryReadUInt32(out var minHeight)
                || !reader.TryReadUInt32(out var maxWidth)
                || !reader.TryReadUInt32(out var maxHeight))
            {
                error = NnrpParseError.SourceTooShort;
                return false;
            }

            block = new TensorProfilePatchAckBlock(minWidth, minHeight, maxWidth, maxHeight);
            return true;
        }

        public bool Equals(TensorProfilePatchAckBlock other)
        {
            return MinWidth == other.MinWidth
                && MinHeight == other.MinHeight
                && MaxWidth == other.MaxWidth
                && MaxHeight == other.MaxHeight;
        }

        public override bool Equals(object obj)
        {
            return obj is TensorProfilePatchAckBlock other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = MinWidth.GetHashCode();
                hash = (hash * 397) ^ MinHeight.GetHashCode();
                hash = (hash * 397) ^ MaxWidth.GetHashCode();
                hash = (hash * 397) ^ MaxHeight.GetHashCode();
                return hash;
            }
        }
    }
}