using System;

namespace Nnrp.Core
{
    public readonly struct TensorSectionBlock
    {
        public TensorSectionBlock(
            TensorSectionDescriptor descriptor,
            ReadOnlyMemory<byte> codecTable,
            ReadOnlyMemory<byte> lengthTable,
            ReadOnlyMemory<byte> payload)
        {
            if ((uint)codecTable.Length != descriptor.CodecTableBytes)
            {
                throw new ArgumentException("Codec table length must match the descriptor.", nameof(codecTable));
            }

            if ((uint)lengthTable.Length != descriptor.LengthTableBytes)
            {
                throw new ArgumentException("Length table length must match the descriptor.", nameof(lengthTable));
            }

            if ((uint)payload.Length != descriptor.PayloadBytes)
            {
                throw new ArgumentException("Payload length must match the descriptor.", nameof(payload));
            }

            if ((lengthTable.Length % sizeof(uint)) != 0)
            {
                throw new ArgumentException("Length table must be a multiple of 4 bytes.", nameof(lengthTable));
            }

            Descriptor = descriptor;
            CodecTable = codecTable;
            LengthTable = lengthTable;
            Payload = payload;
        }

        public TensorSectionDescriptor Descriptor { get; }

        public ReadOnlyMemory<byte> CodecTable { get; }

        public ReadOnlyMemory<byte> LengthTable { get; }

        public ReadOnlyMemory<byte> Payload { get; }

        public int TileCount => LengthTable.Length / sizeof(uint);

        public int TotalLength
        {
            get
            {
                if (!TryGetTotalLength(out var totalLength))
                {
                    throw new InvalidOperationException("Tensor section length exceeds Int32.MaxValue.");
                }

                return totalLength;
            }
        }

        public bool TryCopyTo(Span<byte> destination, out int bytesWritten)
        {
            bytesWritten = 0;
            if (!TryGetTotalLength(out var totalLength) || destination.Length < totalLength)
            {
                return false;
            }

            if (!Descriptor.TryWrite(destination, out var descriptorBytes))
            {
                return false;
            }

            CodecTable.Span.CopyTo(destination.Slice(descriptorBytes, CodecTable.Length));
            LengthTable.Span.CopyTo(destination.Slice(descriptorBytes + CodecTable.Length, LengthTable.Length));
            Payload.Span.CopyTo(destination.Slice(descriptorBytes + CodecTable.Length + LengthTable.Length, Payload.Length));
            bytesWritten = totalLength;
            return true;
        }

        public byte[] ToArray()
        {
            var payload = new byte[TotalLength];
            if (!TryCopyTo(payload, out _))
            {
                throw new InvalidOperationException("Tensor section could not be serialized.");
            }

            return payload;
        }

        public static bool TryParse(
            ReadOnlyMemory<byte> source,
            int expectedTileCount,
            out TensorSectionBlock block,
            out int bytesConsumed,
            out NnrpParseError error)
        {
            block = default;
            bytesConsumed = 0;
            error = NnrpParseError.None;

            if (!TensorSectionDescriptor.TryParse(source.Span, strict: true, out var descriptor, out error))
            {
                return false;
            }

            if ((descriptor.LengthTableBytes % sizeof(uint)) != 0)
            {
                error = NnrpParseError.InconsistentSectionDescriptor;
                return false;
            }

            if ((descriptor.LengthTableBytes / sizeof(uint)) != (uint)expectedTileCount)
            {
                error = NnrpParseError.InconsistentSectionDescriptor;
                return false;
            }

            if (!TryGetTotalLength(descriptor, out var totalLength))
            {
                error = NnrpParseError.MessageTooLarge;
                return false;
            }

            if (source.Length < totalLength)
            {
                error = NnrpParseError.SourceTooShort;
                return false;
            }

            var section = source.Slice(TensorSectionDescriptor.DescriptorLength, totalLength - TensorSectionDescriptor.DescriptorLength);
            var codecTableLength = checked((int)descriptor.CodecTableBytes);
            var lengthTableLength = checked((int)descriptor.LengthTableBytes);
            var payloadLength = checked((int)descriptor.PayloadBytes);
            block = new TensorSectionBlock(
                descriptor,
                section.Slice(0, codecTableLength),
                section.Slice(codecTableLength, lengthTableLength),
                section.Slice(codecTableLength + lengthTableLength, payloadLength));
            bytesConsumed = totalLength;
            error = NnrpParseError.None;
            return true;
        }

        private bool TryGetTotalLength(out int totalLength)
        {
            return TryGetTotalLength(Descriptor, out totalLength);
        }

        private static bool TryGetTotalLength(TensorSectionDescriptor descriptor, out int totalLength)
        {
            totalLength = 0;
            if (!CheckedArithmetic.TryAdd((ulong)TensorSectionDescriptor.DescriptorLength, descriptor.CodecTableBytes, out var running)
                || !CheckedArithmetic.TryAdd(running, descriptor.LengthTableBytes, out running)
                || !CheckedArithmetic.TryAdd(running, descriptor.PayloadBytes, out running)
                || running > int.MaxValue)
            {
                return false;
            }

            totalLength = (int)running;
            return true;
        }
    }
}
