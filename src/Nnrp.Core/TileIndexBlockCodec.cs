using System;
using System.Buffers.Binary;

namespace Nnrp.Core
{
    public static class TileIndexBlockCodec
    {
        private const int TileIdLength = 2;

        public static byte[] Encode(ReadOnlySpan<ushort> tileIds, TileIndexMode mode, uint tileBaseId = 0)
        {
            var payload = new byte[GetEncodedLength(tileIds, mode)];
            if (!TryWrite(tileIds, mode, payload, out var bytesWritten, tileBaseId))
            {
                throw new InvalidOperationException("Destination length calculation for tile index block was inconsistent.");
            }

            if (bytesWritten != payload.Length)
            {
                throw new InvalidOperationException("Tile index block writer did not consume the full destination.");
            }

            return payload;
        }

        public static int GetEncodedLength(ReadOnlySpan<ushort> tileIds, TileIndexMode mode)
        {
            switch (mode)
            {
                case TileIndexMode.DenseRange:
                    return 0;
                case TileIndexMode.RawUInt16:
                case TileIndexMode.DeltaUInt16:
                    return checked(tileIds.Length * TileIdLength);
                case TileIndexMode.Bitset:
                    if (tileIds.Length == 0)
                    {
                        return 0;
                    }

                    return (tileIds[tileIds.Length - 1] / 8) + 1;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported tile index mode.");
            }
        }

        public static bool TryWrite(
            ReadOnlySpan<ushort> tileIds,
            TileIndexMode mode,
            Span<byte> destination,
            out int bytesWritten,
            uint tileBaseId = 0)
        {
            bytesWritten = 0;
            ValidateTileIds(tileIds, mode, tileBaseId);

            var requiredLength = GetEncodedLength(tileIds, mode);
            if (destination.Length < requiredLength)
            {
                return false;
            }

            switch (mode)
            {
                case TileIndexMode.DenseRange:
                    bytesWritten = 0;
                    return true;
                case TileIndexMode.RawUInt16:
                    for (var index = 0; index < tileIds.Length; index++)
                    {
                        BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(index * TileIdLength, TileIdLength), tileIds[index]);
                    }

                    bytesWritten = requiredLength;
                    return true;
                case TileIndexMode.DeltaUInt16:
                    ushort previousTileId = 0;
                    for (var index = 0; index < tileIds.Length; index++)
                    {
                        var value = index == 0 ? tileIds[index] : checked((ushort)(tileIds[index] - previousTileId));
                        BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(index * TileIdLength, TileIdLength), value);
                        previousTileId = tileIds[index];
                    }

                    bytesWritten = requiredLength;
                    return true;
                case TileIndexMode.Bitset:
                    destination.Slice(0, requiredLength).Clear();
                    for (var index = 0; index < tileIds.Length; index++)
                    {
                        var tileId = tileIds[index];
                        destination[tileId / 8] |= (byte)(1 << (tileId % 8));
                    }

                    bytesWritten = requiredLength;
                    return true;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported tile index mode.");
            }
        }

        public static ushort[] Decode(ReadOnlySpan<byte> payload, TileIndexMode mode, int tileCount, uint tileBaseId = 0)
        {
            if (tileCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tileCount), tileCount, "Tile count must be non-negative.");
            }

            var tileIds = tileCount == 0 ? Array.Empty<ushort>() : new ushort[tileCount];
            if (!TryDecode(payload, mode, tileCount, tileIds, out var tileIdsWritten, out var error, tileBaseId))
            {
                throw new ArgumentException($"Failed to decode tile index block: {error}", nameof(payload));
            }

            if (tileIdsWritten == tileIds.Length)
            {
                return tileIds;
            }

            var result = new ushort[tileIdsWritten];
            tileIds.AsSpan(0, tileIdsWritten).CopyTo(result);
            return result;
        }

        public static bool TryDecode(
            ReadOnlySpan<byte> payload,
            TileIndexMode mode,
            int tileCount,
            Span<ushort> destination,
            out int tileIdsWritten,
            out NnrpParseError error,
            uint tileBaseId = 0)
        {
            tileIdsWritten = 0;
            error = NnrpParseError.None;

            if (tileCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tileCount), tileCount, "Tile count must be non-negative.");
            }

            if (destination.Length < tileCount)
            {
                error = NnrpParseError.DestinationTooShort;
                return false;
            }

            switch (mode)
            {
                case TileIndexMode.DenseRange:
                    if (!payload.IsEmpty)
                    {
                        error = NnrpParseError.InvalidTileIndexBlock;
                        return false;
                    }

                    for (var index = 0; index < tileCount; index++)
                    {
                        destination[index] = checked((ushort)(tileBaseId + (uint)index));
                    }

                    tileIdsWritten = tileCount;
                    return true;
                case TileIndexMode.RawUInt16:
                    return TryDecodeRawUInt16(payload, tileCount, destination, out tileIdsWritten, out error);
                case TileIndexMode.DeltaUInt16:
                    return TryDecodeDeltaUInt16(payload, tileCount, destination, out tileIdsWritten, out error);
                case TileIndexMode.Bitset:
                    return TryDecodeBitset(payload, tileCount, destination, out tileIdsWritten, out error);
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported tile index mode.");
            }
        }

        private static bool TryDecodeRawUInt16(
            ReadOnlySpan<byte> payload,
            int tileCount,
            Span<ushort> destination,
            out int tileIdsWritten,
            out NnrpParseError error)
        {
            tileIdsWritten = 0;
            error = NnrpParseError.None;

            var expectedLength = checked(tileCount * TileIdLength);
            if (payload.Length != expectedLength)
            {
                error = payload.Length < expectedLength ? NnrpParseError.SourceTooShort : NnrpParseError.InvalidTileIndexBlock;
                return false;
            }

            for (var index = 0; index < tileCount; index++)
            {
                destination[index] = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(index * TileIdLength, TileIdLength));
            }

            tileIdsWritten = tileCount;
            return true;
        }

        private static bool TryDecodeDeltaUInt16(
            ReadOnlySpan<byte> payload,
            int tileCount,
            Span<ushort> destination,
            out int tileIdsWritten,
            out NnrpParseError error)
        {
            tileIdsWritten = 0;
            error = NnrpParseError.None;

            var expectedLength = checked(tileCount * TileIdLength);
            if (payload.Length != expectedLength)
            {
                error = payload.Length < expectedLength ? NnrpParseError.SourceTooShort : NnrpParseError.InvalidTileIndexBlock;
                return false;
            }

            ushort previousTileId = 0;
            for (var index = 0; index < tileCount; index++)
            {
                var value = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(index * TileIdLength, TileIdLength));
                destination[index] = index == 0 ? value : checked((ushort)(previousTileId + value));
                previousTileId = destination[index];
            }

            tileIdsWritten = tileCount;
            return true;
        }

        private static bool TryDecodeBitset(
            ReadOnlySpan<byte> payload,
            int tileCount,
            Span<ushort> destination,
            out int tileIdsWritten,
            out NnrpParseError error)
        {
            tileIdsWritten = 0;
            error = NnrpParseError.None;

            for (var byteIndex = 0; byteIndex < payload.Length; byteIndex++)
            {
                var value = payload[byteIndex];
                for (var bitOffset = 0; bitOffset < 8; bitOffset++)
                {
                    if ((value & (1 << bitOffset)) == 0)
                    {
                        continue;
                    }

                    if (tileIdsWritten >= tileCount)
                    {
                        error = NnrpParseError.InvalidTileIndexBlock;
                        return false;
                    }

                    destination[tileIdsWritten] = checked((ushort)(byteIndex * 8 + bitOffset));
                    tileIdsWritten++;
                }
            }

            if (tileIdsWritten != tileCount)
            {
                error = NnrpParseError.InvalidTileIndexBlock;
                tileIdsWritten = 0;
                return false;
            }

            return true;
        }

        private static void ValidateTileIds(ReadOnlySpan<ushort> tileIds, TileIndexMode mode, uint tileBaseId)
        {
            switch (mode)
            {
                case TileIndexMode.DenseRange:
                    for (var index = 0; index < tileIds.Length; index++)
                    {
                        if (tileIds[index] != checked((ushort)(tileBaseId + (uint)index)))
                        {
                            throw new ArgumentException(
                                $"DenseRange tile ids must be contiguous and start at tileBaseId={tileBaseId}.",
                                nameof(tileIds));
                        }
                    }

                    return;
                case TileIndexMode.RawUInt16:
                    return;
                case TileIndexMode.DeltaUInt16:
                case TileIndexMode.Bitset:
                    for (var index = 1; index < tileIds.Length; index++)
                    {
                        if (tileIds[index] <= tileIds[index - 1])
                        {
                            throw new ArgumentException(
                                "Tile ids must be strictly increasing for this tile index mode.",
                                nameof(tileIds));
                        }
                    }

                    return;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported tile index mode.");
            }
        }
    }
}
