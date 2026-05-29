using System;
using System.Buffers.Binary;

namespace Nnrp.Core
{
    public ref struct FixedBinaryWriter
    {
        private readonly Span<byte> _destination;
        private int _offset;

        public FixedBinaryWriter(Span<byte> destination)
        {
            _destination = destination;
            _offset = 0;
        }

        public int Offset => _offset;

        public int Remaining => _destination.Length - _offset;

        public bool TryWriteByte(byte value)
        {
            if (Remaining < 1)
            {
                return false;
            }

            _destination[_offset] = value;
            _offset++;
            return true;
        }

        public bool TryWriteUInt16(ushort value)
        {
            if (Remaining < 2)
            {
                return false;
            }

            BinaryPrimitives.WriteUInt16LittleEndian(_destination.Slice(_offset, 2), value);
            _offset += 2;
            return true;
        }

        public bool TryWriteUInt32(uint value)
        {
            if (Remaining < 4)
            {
                return false;
            }

            BinaryPrimitives.WriteUInt32LittleEndian(_destination.Slice(_offset, 4), value);
            _offset += 4;
            return true;
        }

        public bool TryWriteUInt64(ulong value)
        {
            if (Remaining < 8)
            {
                return false;
            }

            BinaryPrimitives.WriteUInt64LittleEndian(_destination.Slice(_offset, 8), value);
            _offset += 8;
            return true;
        }

        public bool TryWriteZeroes(int byteCount)
        {
            if (byteCount < 0 || Remaining < byteCount)
            {
                return false;
            }

            _destination.Slice(_offset, byteCount).Clear();
            _offset += byteCount;
            return true;
        }
    }
}
