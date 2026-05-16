using System;
using Xunit;

namespace Nnrp.Core.Tests
{
    public sealed class TileIndexBlockCodecTests
    {
        [Fact]
        public void RawUInt16_RoundTrips_EmptyTileIds()
        {
            var payload = TileIndexBlockCodec.Encode(Array.Empty<ushort>(), TileIndexMode.RawUInt16);
            var decoded = TileIndexBlockCodec.Decode(payload, TileIndexMode.RawUInt16, tileCount: 0);

            Assert.Empty(payload);
            Assert.Empty(decoded);
        }

        [Fact]
        public void DenseRange_RoundTrips_WithoutPayload()
        {
            var tileIds = new ushort[] { 10, 11, 12 };

            var payload = TileIndexBlockCodec.Encode(tileIds, TileIndexMode.DenseRange, tileBaseId: 10);
            var decoded = TileIndexBlockCodec.Decode(payload, TileIndexMode.DenseRange, tileIds.Length, tileBaseId: 10);

            Assert.Empty(payload);
            Assert.Equal(tileIds, decoded);
        }

        [Fact]
        public void RawUInt16_RoundTrips_NonMonotonicTileIds()
        {
            var tileIds = new ushort[] { 7, 2, 42 };

            var payload = TileIndexBlockCodec.Encode(tileIds, TileIndexMode.RawUInt16);
            var decoded = TileIndexBlockCodec.Decode(payload, TileIndexMode.RawUInt16, tileIds.Length);

            Assert.Equal(new byte[] { 7, 0, 2, 0, 42, 0 }, payload);
            Assert.Equal(tileIds, decoded);
        }

        [Fact]
        public void DeltaUInt16_RoundTrips_StrictlyIncreasingTileIds()
        {
            var tileIds = new ushort[] { 3, 8, 12 };

            var payload = TileIndexBlockCodec.Encode(tileIds, TileIndexMode.DeltaUInt16);
            var decoded = TileIndexBlockCodec.Decode(payload, TileIndexMode.DeltaUInt16, tileIds.Length);

            Assert.Equal(new byte[] { 3, 0, 5, 0, 4, 0 }, payload);
            Assert.Equal(tileIds, decoded);
        }

        [Fact]
        public void DeltaUInt16_RoundTrips_SingleTileId()
        {
            var tileIds = new ushort[] { 37 };

            var payload = TileIndexBlockCodec.Encode(tileIds, TileIndexMode.DeltaUInt16);
            var decoded = TileIndexBlockCodec.Decode(payload, TileIndexMode.DeltaUInt16, tileIds.Length);

            Assert.Equal(new byte[] { 37, 0 }, payload);
            Assert.Equal(tileIds, decoded);
        }

        [Fact]
        public void Bitset_RoundTrips_SparseTileIds()
        {
            var tileIds = new ushort[] { 0, 2, 9 };

            var payload = TileIndexBlockCodec.Encode(tileIds, TileIndexMode.Bitset);
            var decoded = TileIndexBlockCodec.Decode(payload, TileIndexMode.Bitset, tileIds.Length);

            Assert.Equal(new byte[] { 0b0000_0101, 0b0000_0010 }, payload);
            Assert.Equal(tileIds, decoded);
        }

        [Fact]
        public void Bitset_RoundTrips_MaximumTileIdBoundary()
        {
            var tileIds = new ushort[] { ushort.MaxValue };

            var payload = TileIndexBlockCodec.Encode(tileIds, TileIndexMode.Bitset);
            var decoded = TileIndexBlockCodec.Decode(payload, TileIndexMode.Bitset, tileIds.Length);

            Assert.Equal(8192, payload.Length);
            Assert.Equal(tileIds, decoded);
        }

        [Fact]
        public void DenseRange_Rejects_NonContiguousTileIds()
        {
            var error = Assert.Throws<ArgumentException>(() =>
                TileIndexBlockCodec.Encode(new ushort[] { 10, 12 }, TileIndexMode.DenseRange, tileBaseId: 10));

            Assert.Contains("DenseRange", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void DeltaUInt16_Rejects_NonIncreasingTileIds()
        {
            var error = Assert.Throws<ArgumentException>(() =>
                TileIndexBlockCodec.Encode(new ushort[] { 3, 3, 5 }, TileIndexMode.DeltaUInt16));

            Assert.Contains("strictly increasing", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Bitset_DecodeRejects_CountMismatch()
        {
            var destination = new ushort[3];
            var succeeded = TileIndexBlockCodec.TryDecode(
                new byte[] { 0b0000_0101 },
                TileIndexMode.Bitset,
                tileCount: 3,
                destination,
                out var tileIdsWritten,
                out var error);

            Assert.False(succeeded);
            Assert.Equal(0, tileIdsWritten);
            Assert.Equal(NnrpParseError.InvalidTileIndexBlock, error);
        }
    }
}
