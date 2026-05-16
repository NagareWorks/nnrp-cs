using Nnrp.Core;
using Xunit;

namespace Nnrp.Core.Tests
{
    public sealed class TransportProbeMessageTests
    {
        [Fact]
        public void TransportProbeMetadataRoundTripsThroughBytes()
        {
            var metadata = new TransportProbeMetadata(
                probeId: 7,
                probePayloadBytes: 16384,
                clientSendTimestampMicroseconds: 123456789UL);

            var payload = metadata.ToArray();

            Assert.Equal(TransportProbeMetadata.MetadataLength, payload.Length);
            Assert.True(TransportProbeMetadata.TryParse(payload, out var parsed, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(metadata, parsed);
        }

        [Fact]
        public void TransportProbeAckMetadataRoundTripsThroughBytes()
        {
            var metadata = new TransportProbeAckMetadata(
                probeId: 7,
                reserved: 0,
                serverReceiveTimestampMicroseconds: 123456999UL);

            var payload = metadata.ToArray();

            Assert.Equal(TransportProbeAckMetadata.MetadataLength, payload.Length);
            Assert.True(TransportProbeAckMetadata.TryParse(payload, out var parsed, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(metadata, parsed);
        }

        [Fact]
        public void TransportProbeMessageRoundTripsPayload()
        {
            var metadata = new TransportProbeMetadata(5, 3, 123456789UL);
            var message = new TransportProbeMessage(
                new NnrpHeader(
                    NnrpHeader.CurrentVersionMajor,
                    NnrpHeader.CurrentWireFormat,
                    MessageType.TransportProbe,
                    HeaderFlags.AckRequired,
                    TransportProbeMetadata.MetadataLength,
                    bodyLength: 3,
                    sessionId: 0,
                    frameId: 0,
                    viewId: 0,
                    routeId: 0,
                    traceId: 77),
                metadata,
                new byte[] { 1, 2, 3 });

            Assert.True(TransportProbeMessage.TryParse(message.ToArray(), out var parsed, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(message.Header, parsed.Header);
            Assert.Equal(metadata, parsed.Metadata);
            Assert.Equal(new byte[] { 1, 2, 3 }, parsed.Payload.ToArray());
        }

        [Fact]
        public void TransportProbeMessageRejectsPayloadLengthMismatch()
        {
            var metadata = new TransportProbeMetadata(5, 4, 123456789UL);
            var header = new NnrpHeader(
                NnrpHeader.CurrentVersionMajor,
                NnrpHeader.CurrentWireFormat,
                MessageType.TransportProbe,
                HeaderFlags.None,
                TransportProbeMetadata.MetadataLength,
                bodyLength: 3,
                sessionId: 0,
                frameId: 0,
                viewId: 0,
                routeId: 0,
                traceId: 0);

            Assert.Throws<System.ArgumentException>(() => new TransportProbeMessage(header, metadata, new byte[] { 1, 2, 3 }));
        }

        [Fact]
        public void TransportProbeAckMessageRoundTripsFixedMetadata()
        {
            var metadata = new TransportProbeAckMetadata(5, 0, 123456999UL);
            var message = new TransportProbeAckMessage(
                new NnrpHeader(
                    NnrpHeader.CurrentVersionMajor,
                    NnrpHeader.CurrentWireFormat,
                    MessageType.TransportProbeAck,
                    HeaderFlags.None,
                    TransportProbeAckMetadata.MetadataLength,
                    bodyLength: 0,
                    sessionId: 0,
                    frameId: 0,
                    viewId: 0,
                    routeId: 0,
                    traceId: 88),
                metadata);

            Assert.True(TransportProbeAckMessage.TryParse(message.ToArray(), out var parsed, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(metadata, parsed.Metadata);
        }

        [Fact]
        public void TransportProbeMessagesRejectUnknownWireFormat()
        {
            var metadata = new TransportProbeMetadata(5, 3, 123456789UL);
            var framed = new TransportProbeMessage(
                new NnrpHeader(
                    NnrpHeader.CurrentVersionMajor,
                    MessageType.TransportProbe,
                    HeaderFlags.AckRequired,
                    TransportProbeMetadata.MetadataLength,
                    bodyLength: 3,
                    sessionId: 0,
                    frameId: 0,
                    viewId: 0,
                    routeId: 0,
                    traceId: 66),
                metadata,
                new byte[] { 1, 2, 3 }).ToArray();
            framed[5] = 0x7F;

            Assert.False(TransportProbeMessage.TryParse(framed, out _, out var error));
            Assert.Equal(NnrpParseError.UnknownWireFormat, error);
        }
    }
}
