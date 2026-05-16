using System;
using Nnrp.Core;
using Xunit;

namespace Nnrp.Core.Tests
{
    public sealed class ControlExtensionBlockTests
    {
        [Fact]
        public void RoundTripSingleOptionalExtension()
        {
            var value = new byte[] { 0xAA, 0xBB, 0xCC };
            var block = new ControlExtensionBlock(ControlExtensionType.ScheduleHint, value);
            var bytes = block.ToArray();

            Assert.True(ControlExtensionBlock.TryParse(bytes, out var parsed, out var consumed, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(bytes.Length, consumed);
            Assert.Equal(ControlExtensionType.ScheduleHint, parsed.TypedType);
            Assert.False(parsed.IsCritical);
            Assert.Equal(value, parsed.Value.ToArray());
        }

        [Fact]
        public void RoundTripCriticalExtension()
        {
            var value = new byte[] { 0x01 };
            var type = (ControlExtensionType)((ushort)ControlExtensionType.SessionRekey | ControlExtensionBlock.CriticalFlag);
            var block = new ControlExtensionBlock(type, value);
            var bytes = block.ToArray();

            Assert.True(ControlExtensionBlock.TryParse(bytes, out var parsed, out var consumed, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.True(parsed.IsCritical);
            Assert.Equal((ushort)ControlExtensionType.SessionRekey, parsed.TypeCode);
        }

        [Fact]
        public void TryParseFailsOnTruncatedHeader()
        {
            var source = new byte[] { 0x01 }; // only 1 byte, need 6
            Assert.False(ControlExtensionBlock.TryParse(source, out _, out _, out var error));
            Assert.Equal(NnrpParseError.SourceTooShort, error);
        }

        [Fact]
        public void TryParseFailsOnTruncatedValue()
        {
            var source = new byte[8]; // header 8, but claims 4 value bytes and provides none
            System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(source, 0x0001);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(source.AsSpan(4), 4);
            Assert.False(ControlExtensionBlock.TryParse(source, out _, out _, out var error));
            Assert.Equal(NnrpParseError.SourceTooShort, error);
        }

        [Fact]
        public void ParserIgnoresUnknownOptional()
        {
            var value = new byte[] { 0x42 };
            // Use an unknown optional type (not in our known set)
            var block = new ControlExtensionBlock((ControlExtensionType)0x00FF, value);
            var source = block.ToArray();

            var knownOptional = new[] { ControlExtensionType.ScheduleHint };
            var knownCritical = Array.Empty<ControlExtensionType>();
            var seen = 0;

            Assert.True(ControlExtensionParser.TryParseExtensions(
                source, knownOptional, knownCritical, _ => seen++, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(0, seen); // ignored
        }

        [Fact]
        public void ParserInvokesKnownOptional()
        {
            var value = new byte[] { 0x77 };
            var block = new ControlExtensionBlock(ControlExtensionType.ScheduleHint, value);
            var source = block.ToArray();

            var knownOptional = new[] { ControlExtensionType.ScheduleHint };
            var knownCritical = Array.Empty<ControlExtensionType>();
            ControlExtensionBlock? observed = null;

            Assert.True(ControlExtensionParser.TryParseExtensions(
                source, knownOptional, knownCritical, b => observed = b, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.NotNull(observed);
            Assert.Equal(ControlExtensionType.ScheduleHint, observed.Value.TypedType);
        }

        [Fact]
        public void ParserFailsOnUnknownCritical()
        {
            // Unknown critical type with critical flag set
            var type = (ControlExtensionType)(0x00FF | ControlExtensionBlock.CriticalFlag);
            var block = new ControlExtensionBlock(type, new byte[] { 0x99 });
            var source = block.ToArray();

            var knownOptional = Array.Empty<ControlExtensionType>();
            var knownCritical = Array.Empty<ControlExtensionType>();

            Assert.False(ControlExtensionParser.TryParseExtensions(
                source, knownOptional, knownCritical, _ => { }, out var error));
            Assert.Equal(NnrpParseError.UnsupportedExtension, error);
        }

        [Fact]
        public void ParserInvokesKnownCritical()
        {
            var type = (ControlExtensionType)((ushort)ControlExtensionType.ModelHotSwap | ControlExtensionBlock.CriticalFlag);
            var block = new ControlExtensionBlock(type, new byte[] { 0x88 });
            var source = block.ToArray();

            var knownOptional = Array.Empty<ControlExtensionType>();
            var knownCritical = new[] { ControlExtensionType.ModelHotSwap };
            ControlExtensionBlock? observed = null;

            Assert.True(ControlExtensionParser.TryParseExtensions(
                source, knownOptional, knownCritical, b => observed = b, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.NotNull(observed);
            Assert.Equal((ushort)ControlExtensionType.ModelHotSwap, observed.Value.TypeCode);
            Assert.True(observed.Value.IsCritical);
        }

        [Fact]
        public void ParserSkipsUnknownOptionalThenFailsOnUnknownCritical()
        {
            var optBlock = new ControlExtensionBlock((ControlExtensionType)0x00AA, new byte[] { 0x11 });
            var critBlock = new ControlExtensionBlock(
                (ControlExtensionType)(0x00BB | ControlExtensionBlock.CriticalFlag),
                new byte[] { 0x22 });

            var source = new byte[optBlock.TotalLength + critBlock.TotalLength];
            optBlock.WriteTo(source);
            critBlock.WriteTo(source.AsSpan(optBlock.TotalLength));

            var knownOptional = Array.Empty<ControlExtensionType>();
            var knownCritical = Array.Empty<ControlExtensionType>();

            Assert.False(ControlExtensionParser.TryParseExtensions(
                source, knownOptional, knownCritical, _ => { }, out var error));
            Assert.Equal(NnrpParseError.UnsupportedExtension, error);
        }

        [Fact]
        public void ParserHandlesMultipleExtensions()
        {
            var block1 = new ControlExtensionBlock(ControlExtensionType.ScheduleHint, new byte[] { 1 });
            var block2 = new ControlExtensionBlock(ControlExtensionType.CachePreload, new byte[] { 2, 3 });
            var block3 = new ControlExtensionBlock(ControlExtensionType.ScheduleHint, new byte[] { 4 });

            var source = new byte[block1.TotalLength + block2.TotalLength + block3.TotalLength];
            block1.WriteTo(source);
            block2.WriteTo(source.AsSpan(block1.TotalLength));
            block3.WriteTo(source.AsSpan(block1.TotalLength + block2.TotalLength));

            var knownOptional = new[] { ControlExtensionType.ScheduleHint, ControlExtensionType.CachePreload };
            var knownCritical = Array.Empty<ControlExtensionType>();
            var count = 0;

            Assert.True(ControlExtensionParser.TryParseExtensions(
                source, knownOptional, knownCritical, _ => count++, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(3, count);
        }

        [Fact]
        public void EmptySourceParsesSuccessfully()
        {
            Assert.True(ControlExtensionParser.TryParseExtensions(
                Array.Empty<byte>(),
                Array.Empty<ControlExtensionType>(),
                Array.Empty<ControlExtensionType>(),
                _ => { },
                out var error));
            Assert.Equal(NnrpParseError.None, error);
        }

        [Fact]
        public void WriteToThrowsOnTooSmallDestination()
        {
            var block = new ControlExtensionBlock(ControlExtensionType.ScheduleHint, new byte[] { 1, 2 });
            Assert.Throws<ArgumentException>(() => block.WriteTo(new byte[block.TotalLength - 1]));
        }
    }
}
