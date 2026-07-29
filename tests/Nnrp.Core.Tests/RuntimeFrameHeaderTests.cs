using System;
using System.Linq;
using Nnrp.Core;
using Nnrp.Runtime;
using Xunit;

namespace Nnrp.Core.Tests
{
    public sealed class RuntimeFrameHeaderTests
    {
        [Fact]
        public void PublicProjectionContainsExactlyTheCallerControlledFields()
        {
            var properties = typeof(RuntimeFrameHeader)
                .GetProperties()
                .Select(property => (property.Name, property.PropertyType))
                .OrderBy(property => property.Name, StringComparer.Ordinal)
                .ToArray();

            var expected = new[]
            {
                (nameof(RuntimeFrameHeader.Flags), typeof(HeaderFlags)),
                (nameof(RuntimeFrameHeader.FrameId), typeof(uint)),
                (nameof(RuntimeFrameHeader.MessageType), typeof(MessageType)),
                (nameof(RuntimeFrameHeader.RouteId), typeof(ushort)),
                (nameof(RuntimeFrameHeader.SessionId), typeof(uint)),
                (nameof(RuntimeFrameHeader.TraceId), typeof(ulong)),
                (nameof(RuntimeFrameHeader.VersionMajor), typeof(byte)),
                (nameof(RuntimeFrameHeader.ViewId), typeof(ushort)),
                (nameof(RuntimeFrameHeader.WireFormat), typeof(byte)),
            };

            Assert.Equal(expected, properties);
            Assert.DoesNotContain(properties, property => property.Name == "Generation");
        }

        [Fact]
        public void DefaultsUseTheCurrentProtocolHeaderValues()
        {
            var header = new RuntimeFrameHeader(MessageType.Ping);

            Assert.Equal(MessageType.Ping, header.MessageType);
            Assert.Equal(HeaderFlags.None, header.Flags);
            Assert.Equal(0u, header.SessionId);
            Assert.Equal(0u, header.FrameId);
            Assert.Equal((ushort)0, header.ViewId);
            Assert.Equal((ushort)0, header.RouteId);
            Assert.Equal(0ul, header.TraceId);
            Assert.Equal(NnrpHeader.CurrentVersionMajor, header.VersionMajor);
            Assert.Equal(NnrpHeader.CurrentWireFormat, header.WireFormat);
        }

        [Fact]
        public void ProjectionReconstructsTheFixedHeaderGoldenVector()
        {
            var projection = new RuntimeFrameHeader(
                MessageType.FrameSubmit,
                HeaderFlags.AckRequired | HeaderFlags.Keyframe,
                SessionId: 0x11223344,
                FrameId: 0x55667788,
                ViewId: 0x99AA,
                RouteId: 0xBBCC,
                TraceId: 0x0102030405060708);
            var header = new NnrpHeader(
                projection.VersionMajor,
                projection.MessageType,
                projection.Flags,
                metaLength: 0x00010203,
                bodyLength: 0x00040506,
                projection.SessionId,
                projection.FrameId,
                projection.ViewId,
                projection.RouteId,
                projection.TraceId,
                projection.WireFormat);

            Assert.Equal(
                "4e4e5250010010282100000003020100060504004433221188776655aa99ccbb0807060504030201",
                Convert.ToHexString(header.ToArray()).ToLowerInvariant());
        }
    }
}
