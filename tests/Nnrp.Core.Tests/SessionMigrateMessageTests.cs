using Nnrp.Core;
using Xunit;

namespace Nnrp.Core.Tests
{
    public sealed class SessionMigrateMessageTests
    {
        [Fact]
        public void SessionMigrateMetadataRoundTripsThroughBytes()
        {
            var metadata = new SessionMigrateMetadata(
                oldTransportId: TransportId.Quic,
                newTransportId: TransportId.Tcp,
                lastResultFrameId: 108,
                clientMigrateTimestampMicroseconds: 123456789UL);

            var payload = metadata.ToArray();

            Assert.Equal(SessionMigrateMetadata.MetadataLength, payload.Length);
            Assert.True(SessionMigrateMetadata.TryParse(payload, out var parsed, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(metadata, parsed);
        }

        [Fact]
        public void SessionMigrateMetadataRejectsUnspecifiedTransportIds()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                new SessionMigrateMetadata(
                    oldTransportId: TransportId.Unspecified,
                    newTransportId: TransportId.Tcp,
                    lastResultFrameId: 64,
                    clientMigrateTimestampMicroseconds: 123456000UL));

            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                new SessionMigrateMetadata(
                    oldTransportId: TransportId.Quic,
                    newTransportId: TransportId.Unspecified,
                    lastResultFrameId: 64,
                    clientMigrateTimestampMicroseconds: 123456000UL));
        }

        [Fact]
        public void SessionMigrateAckMetadataRoundTripsThroughBytes()
        {
            var metadata = new SessionMigrateAckMetadata(
                acceptCode: 0,
                resumeFromFrameId: 120,
                graceWindowMilliseconds: 250,
                serverMigrateTimestampMicroseconds: 123457000UL);

            var payload = metadata.ToArray();

            Assert.Equal(SessionMigrateAckMetadata.MetadataLength, payload.Length);
            Assert.True(SessionMigrateAckMetadata.TryParse(payload, out var parsed, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(metadata, parsed);
        }

        [Fact]
        public void SessionMigrateAckMetadataUsesFrozenLayout()
        {
            var metadata = new SessionMigrateAckMetadata(
                acceptCode: 3,
                resumeFromFrameId: 91,
                graceWindowMilliseconds: 200,
                serverMigrateTimestampMicroseconds: 123458000UL);

            var payload = metadata.ToArray();

            Assert.Equal(SessionMigrateAckMetadata.MetadataLength, payload.Length);
            Assert.True(SessionMigrateAckMetadata.TryParse(payload, strict: true, out var parsed, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(metadata, parsed);
        }

        [Fact]
        public void SessionMigrateMessageRoundTripsFixedMetadata()
        {
            var metadata = new SessionMigrateMetadata(TransportId.Quic, TransportId.Tcp, 77, 123456789UL);
            var message = new SessionMigrateMessage(
                new NnrpHeader(
                    NnrpHeader.CurrentVersionMajor,
                    NnrpHeader.CurrentWireFormat,
                    MessageType.SessionMigrate,
                    HeaderFlags.AckRequired,
                    SessionMigrateMetadata.MetadataLength,
                    bodyLength: 0,
                    sessionId: 41,
                    frameId: 0,
                    viewId: 0,
                    routeId: 0,
                    traceId: 501),
                metadata);

            Assert.True(SessionMigrateMessage.TryParse(message.ToArray(), out var parsed, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(metadata, parsed.Metadata);
            Assert.Equal(41u, parsed.Header.SessionId);
        }

        [Fact]
        public void SessionMigrateAckMessageRoundTripsFixedMetadata()
        {
            var metadata = new SessionMigrateAckMetadata(0, 77, 250, 123456999UL);
            var message = new SessionMigrateAckMessage(
                new NnrpHeader(
                    NnrpHeader.CurrentVersionMajor,
                    NnrpHeader.CurrentWireFormat,
                    MessageType.SessionMigrateAck,
                    HeaderFlags.None,
                    SessionMigrateAckMetadata.MetadataLength,
                    bodyLength: 0,
                    sessionId: 41,
                    frameId: 0,
                    viewId: 0,
                    routeId: 0,
                    traceId: 502),
                metadata);

            Assert.True(SessionMigrateAckMessage.TryParse(message.ToArray(), out var parsed, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(metadata, parsed.Metadata);
            Assert.Equal(41u, parsed.Header.SessionId);
        }

        [Fact]
        public void SessionMigrateMessagesRejectUnknownWireFormat()
        {
            var metadata = new SessionMigrateMetadata(TransportId.Quic, TransportId.Tcp, 77, 123456789UL);
            var framed = new SessionMigrateMessage(
                new NnrpHeader(
                    NnrpHeader.CurrentVersionMajor,
                    MessageType.SessionMigrate,
                    HeaderFlags.AckRequired,
                    SessionMigrateMetadata.MetadataLength,
                    bodyLength: 0,
                    sessionId: 41,
                    frameId: 0,
                    viewId: 0,
                    routeId: 0,
                    traceId: 503),
                metadata).ToArray();
            framed[5] = 0x7F;

            Assert.False(SessionMigrateMessage.TryParse(framed, out _, out var error));
            Assert.Equal(NnrpParseError.UnknownWireFormat, error);
        }
    }
}
