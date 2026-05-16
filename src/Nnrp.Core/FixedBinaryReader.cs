using System;
using System.Buffers.Binary;

namespace Nnrp.Core
{
    public ref struct FixedBinaryReader
    {
        private readonly ReadOnlySpan<byte> _source;
        private int _offset;

        public FixedBinaryReader(ReadOnlySpan<byte> source)
        {
            _source = source;
            _offset = 0;
        }

        public int Offset => _offset;

        public int Remaining => _source.Length - _offset;

        public bool TryReadByte(out byte value)
        {
            value = 0;
            if (Remaining < 1)
            {
                return false;
            }

            value = _source[_offset];
            _offset++;
            return true;
        }

        public bool TryReadUInt16(out ushort value)
        {
            value = 0;
            if (Remaining < 2)
            {
                return false;
            }

            value = BinaryPrimitives.ReadUInt16LittleEndian(_source.Slice(_offset, 2));
            _offset += 2;
            return true;
        }

        public bool TryReadUInt32(out uint value)
        {
            value = 0;
            if (Remaining < 4)
            {
                return false;
            }

            value = BinaryPrimitives.ReadUInt32LittleEndian(_source.Slice(_offset, 4));
            _offset += 4;
            return true;
        }

        public bool TryReadUInt64(out ulong value)
        {
            value = 0;
            if (Remaining < 8)
            {
                return false;
            }

            value = BinaryPrimitives.ReadUInt64LittleEndian(_source.Slice(_offset, 8));
            _offset += 8;
            return true;
        }

        public bool TrySkip(int byteCount)
        {
            if (byteCount < 0 || Remaining < byteCount)
            {
                return false;
            }

            _offset += byteCount;
            return true;
        }
    }
}
