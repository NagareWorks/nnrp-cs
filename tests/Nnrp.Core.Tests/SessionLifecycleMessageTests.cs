using System;
using Xunit;

namespace Nnrp.Core.Tests
{
    public sealed class SessionLifecycleMessageTests
    {
        [Fact]
        public void SessionOpenMetadataRoundTripsThroughGoldenBytes()
        {
            var metadata = GoldenSessionOpenMetadata();
            var expected = new byte[]
            {
                0x2A, 0x00, 0x00, 0x00, 0x02, 0x00, 0x01, 0x05,
                0x01, 0x10, 0x00, 0x00, 0x03, 0x00, 0x00, 0x00,
                0xF4, 0x01, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00,
                0x30, 0x75, 0x00, 0x00, 0x10, 0x00, 0x00, 0x00,
                0x20, 0x00, 0x00, 0x00, 0x08, 0x00, 0x00, 0x00,
                0xEF, 0xCD, 0xAB, 0x89, 0x67, 0x45, 0x23, 0x01,
            };

            Assert.Equal((uint)56, metadata.BodyLength);
            Assert.Equal(expected, metadata.ToArray());
            Assert.True(metadata.TryWrite(new byte[SessionOpenMetadata.MetadataLength], out var bytesWritten));
            Assert.Equal(SessionOpenMetadata.MetadataLength, bytesWritten);
            Assert.False(metadata.TryWrite(new byte[SessionOpenMetadata.MetadataLength - 1], out bytesWritten));
            Assert.Equal(0, bytesWritten);
            Assert.Throws<ArgumentException>(() => metadata.Write(new byte[SessionOpenMetadata.MetadataLength - 1]));
            Assert.False(SessionOpenMetadata.TryParse(new byte[SessionOpenMetadata.MetadataLength - 1], strict: true, out _, out var shortError));
            Assert.Equal(NnrpParseError.SourceTooShort, shortError);
            Assert.True(SessionOpenMetadata.TryParse(expected, strict: true, out var parsed, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.True(SessionOpenMetadata.TryParse(expected, out var parsedWithoutError));
            Assert.Equal(metadata, parsedWithoutError);
            Assert.True(SessionOpenMetadata.TryParse(expected, out var parsedWithError, out error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(metadata, parsedWithError);
            Assert.True(parsed.Equals(metadata));
            Assert.True(parsed.Equals((object)metadata));
            Assert.False(parsed.Equals("not metadata"));
            Assert.Equal(metadata.GetHashCode(), parsed.GetHashCode());
        }

        [Fact]
        public void SessionOpenAckMetadataRoundTripsThroughGoldenBytes()
        {
            var metadata = GoldenSessionOpenAckMetadata();
            var expected = new byte[]
            {
                0x2A, 0x00, 0x00, 0x00, 0x02, 0x00, 0x01, 0x00,
                0x01, 0x10, 0x00, 0x00, 0x03, 0x00, 0x00, 0x00,
                0x02, 0x00, 0x04, 0x00, 0x30, 0x75, 0x00, 0x00,
                0xC0, 0xD4, 0x01, 0x00, 0x10, 0x00, 0x00, 0x00,
                0x08, 0x00, 0x00, 0x00, 0x21, 0x43, 0x65, 0x87,
                0xA9, 0xCB, 0xED, 0x0F, 0x07, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x05, 0x00, 0x00, 0x00,
            };

            Assert.Equal((uint)24, metadata.BodyLength);
            Assert.Equal(expected, metadata.ToArray());
            Assert.True(metadata.TryWrite(new byte[SessionOpenAckMetadata.MetadataLength], out var bytesWritten));
            Assert.Equal(SessionOpenAckMetadata.MetadataLength, bytesWritten);
            Assert.False(metadata.TryWrite(new byte[SessionOpenAckMetadata.MetadataLength - 1], out bytesWritten));
            Assert.Equal(0, bytesWritten);
            Assert.Throws<ArgumentException>(() => metadata.Write(new byte[SessionOpenAckMetadata.MetadataLength - 1]));
            Assert.False(SessionOpenAckMetadata.TryParse(new byte[SessionOpenAckMetadata.MetadataLength - 1], out _, out var shortError));
            Assert.Equal(NnrpParseError.SourceTooShort, shortError);
            Assert.True(SessionOpenAckMetadata.TryParse(expected, out var parsed, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.True(SessionOpenAckMetadata.TryParse(expected, out var parsedWithoutError));
            Assert.Equal(metadata, parsedWithoutError);
            Assert.True(parsed.Equals(metadata));
            Assert.True(parsed.Equals((object)metadata));
            Assert.False(parsed.Equals("not metadata"));
            Assert.Equal(metadata.GetHashCode(), parsed.GetHashCode());
        }

        [Fact]
        public void SessionCloseMetadataRoundTripsThroughGoldenBytes()
        {
            var metadata = GoldenSessionCloseMetadata();
            var expected = new byte[]
            {
                0x01, 0x00, 0x00, 0x00, 0xE8, 0x03, 0x00, 0x00,
                0x63, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x44, 0x33, 0x22, 0x11,
            };

            Assert.Equal(expected, metadata.ToArray());
            Assert.True(metadata.TryWrite(new byte[SessionCloseMetadata.MetadataLength], out var bytesWritten));
            Assert.Equal(SessionCloseMetadata.MetadataLength, bytesWritten);
            Assert.False(metadata.TryWrite(new byte[SessionCloseMetadata.MetadataLength - 1], out bytesWritten));
            Assert.Equal(0, bytesWritten);
            Assert.Throws<ArgumentException>(() => metadata.Write(new byte[SessionCloseMetadata.MetadataLength - 1]));
            Assert.False(SessionCloseMetadata.TryParse(new byte[SessionCloseMetadata.MetadataLength - 1], strict: true, out _, out var shortError));
            Assert.Equal(NnrpParseError.SourceTooShort, shortError);
            Assert.True(SessionCloseMetadata.TryParse(expected, strict: true, out var parsed, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.True(SessionCloseMetadata.TryParse(expected, out var parsedWithoutError));
            Assert.Equal(metadata, parsedWithoutError);
            Assert.True(SessionCloseMetadata.TryParse(expected, out var parsedWithError, out error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(metadata, parsedWithError);
            Assert.True(parsed.Equals(metadata));
            Assert.True(parsed.Equals((object)metadata));
            Assert.False(parsed.Equals("not metadata"));
            Assert.Equal(metadata.GetHashCode(), parsed.GetHashCode());
        }

        [Fact]
        public void SessionCloseAckMetadataRoundTripsThroughGoldenBytes()
        {
            var metadata = GoldenSessionCloseAckMetadata();
            var expected = new byte[]
            {
                0x01, 0x00, 0x00, 0x00, 0x63, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            };

            Assert.Equal(expected, metadata.ToArray());
            Assert.True(metadata.TryWrite(new byte[SessionCloseAckMetadata.MetadataLength], out var bytesWritten));
            Assert.Equal(SessionCloseAckMetadata.MetadataLength, bytesWritten);
            Assert.False(metadata.TryWrite(new byte[SessionCloseAckMetadata.MetadataLength - 1], out bytesWritten));
            Assert.Equal(0, bytesWritten);
            Assert.Throws<ArgumentException>(() => metadata.Write(new byte[SessionCloseAckMetadata.MetadataLength - 1]));
            Assert.False(SessionCloseAckMetadata.TryParse(new byte[SessionCloseAckMetadata.MetadataLength - 1], strict: true, out _, out var shortError));
            Assert.Equal(NnrpParseError.SourceTooShort, shortError);
            Assert.True(SessionCloseAckMetadata.TryParse(expected, strict: true, out var parsed, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.True(SessionCloseAckMetadata.TryParse(expected, out var parsedWithoutError));
            Assert.Equal(metadata, parsedWithoutError);
            Assert.True(SessionCloseAckMetadata.TryParse(expected, out var parsedWithError, out error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(metadata, parsedWithError);
            Assert.True(parsed.Equals(metadata));
            Assert.True(parsed.Equals((object)metadata));
            Assert.False(parsed.Equals("not metadata"));
            Assert.Equal(metadata.GetHashCode(), parsed.GetHashCode());
        }

        [Fact]
        public void SessionLifecycleMetadataRejectsReservedAndInvalidValues()
        {
            var openBytes = GoldenSessionOpenMetadata().ToArray();
            openBytes[22] = 1;
            Assert.False(SessionOpenMetadata.TryParse(openBytes, strict: true, out _, out var openReservedError));
            Assert.Equal(NnrpParseError.NonZeroReservedField, openReservedError);
            Assert.True(SessionOpenMetadata.TryParse(openBytes, strict: false, out _, out _));

            openBytes = GoldenSessionOpenMetadata().ToArray();
            openBytes[6] = 0xFF;
            Assert.False(SessionOpenMetadata.TryParse(openBytes, strict: true, out _, out var openPriorityError));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, openPriorityError);

            openBytes = GoldenSessionOpenMetadata().ToArray();
            openBytes[7] = 0x80;
            Assert.False(SessionOpenMetadata.TryParse(openBytes, strict: true, out _, out var openFlagsError));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, openFlagsError);

            var openAckBytes = GoldenSessionOpenAckMetadata().ToArray();
            openAckBytes[6] = 0xFF;
            Assert.False(SessionOpenAckMetadata.TryParse(openAckBytes, out _, out var ackPriorityError));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, ackPriorityError);

            openAckBytes = GoldenSessionOpenAckMetadata().ToArray();
            openAckBytes[7] = 0xFF;
            Assert.False(SessionOpenAckMetadata.TryParse(openAckBytes, out _, out var ackStatusError));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, ackStatusError);

            openAckBytes = GoldenSessionOpenAckMetadata().ToArray();
            openAckBytes[48] = 0xFE;
            openAckBytes[49] = 0xFF;
            Assert.False(SessionOpenAckMetadata.TryParse(openAckBytes, out _, out var ackErrorCodeError));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, ackErrorCodeError);

            openAckBytes = GoldenSessionOpenAckMetadata().ToArray();
            openAckBytes[52] = 0x80;
            Assert.False(SessionOpenAckMetadata.TryParse(openAckBytes, out _, out var ackFlagsError));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, ackFlagsError);

            var closeBytes = GoldenSessionCloseMetadata().ToArray();
            closeBytes[3] = 1;
            Assert.False(SessionCloseMetadata.TryParse(closeBytes, strict: true, out _, out var closeReservedError));
            Assert.Equal(NnrpParseError.NonZeroReservedField, closeReservedError);
            Assert.True(SessionCloseMetadata.TryParse(closeBytes, strict: false, out _, out _));

            closeBytes = GoldenSessionCloseMetadata().ToArray();
            closeBytes[0] = 0xFF;
            Assert.False(SessionCloseMetadata.TryParse(closeBytes, strict: true, out _, out var closeReasonError));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, closeReasonError);

            closeBytes = GoldenSessionCloseMetadata().ToArray();
            closeBytes[2] = 0xFF;
            Assert.False(SessionCloseMetadata.TryParse(closeBytes, strict: true, out _, out var policyError));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, policyError);

            closeBytes = GoldenSessionCloseMetadata().ToArray();
            closeBytes[16] = 0xFE;
            closeBytes[17] = 0xFF;
            Assert.False(SessionCloseMetadata.TryParse(closeBytes, strict: true, out _, out var closeErrorCodeError));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, closeErrorCodeError);

            var closeAckBytes = GoldenSessionCloseAckMetadata().ToArray();
            closeAckBytes[1] = 1;
            Assert.False(SessionCloseAckMetadata.TryParse(closeAckBytes, strict: true, out _, out var closeAckReservedError));
            Assert.Equal(NnrpParseError.NonZeroReservedField, closeAckReservedError);
            Assert.True(SessionCloseAckMetadata.TryParse(closeAckBytes, strict: false, out _, out _));

            closeAckBytes = GoldenSessionCloseAckMetadata().ToArray();
            closeAckBytes[0] = 0xFF;
            Assert.False(SessionCloseAckMetadata.TryParse(closeAckBytes, strict: true, out _, out var closeAckStatusError));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, closeAckStatusError);

            closeAckBytes = GoldenSessionCloseAckMetadata().ToArray();
            closeAckBytes[12] = 0xFE;
            closeAckBytes[13] = 0xFF;
            Assert.False(SessionCloseAckMetadata.TryParse(closeAckBytes, strict: true, out _, out var closeAckErrorCodeError));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, closeAckErrorCodeError);
        }

        [Fact]
        public void SessionLifecycleConstructorsRejectInvalidValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SessionOpenMetadata(0, 0, (SessionPriorityClass)0xFF, SessionFlags.None, 0, 0, 0, 0, 0, 0, 0, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SessionOpenMetadata(0, 0, SessionPriorityClass.Interactive, (SessionFlags)0x80, 0, 0, 0, 0, 0, 0, 0, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SessionOpenAckMetadata(0, 0, (SessionPriorityClass)0xFF, SessionStatus.Opened, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, SessionErrorCode.None, SessionAckFlags.None));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SessionOpenAckMetadata(0, 0, SessionPriorityClass.Interactive, (SessionStatus)0xFF, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, SessionErrorCode.None, SessionAckFlags.None));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SessionOpenAckMetadata(0, 0, SessionPriorityClass.Interactive, SessionStatus.Opened, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, (SessionErrorCode)0xFFFE, SessionAckFlags.None));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SessionOpenAckMetadata(0, 0, SessionPriorityClass.Interactive, SessionStatus.Opened, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, SessionErrorCode.None, (SessionAckFlags)0x80));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SessionCloseMetadata((SessionCloseReason)0xFF, InFlightPolicy.Drain, 0, 0, SessionErrorCode.None, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SessionCloseMetadata(SessionCloseReason.Normal, (InFlightPolicy)0xFF, 0, 0, SessionErrorCode.None, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SessionCloseMetadata(SessionCloseReason.Normal, InFlightPolicy.Drain, 0, 0, (SessionErrorCode)0xFFFE, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SessionCloseAckMetadata((SessionCloseStatus)0xFF, 0, SessionErrorCode.None));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SessionCloseAckMetadata(SessionCloseStatus.Closed, 0, (SessionErrorCode)0xFFFE));
        }

        [Fact]
        public void SessionLifecycleMessagesRoundTripAndRejectInvalidLayouts()
        {
            Assert.True(MessageType.SessionOpen.IsSessionLifecycle());
            Assert.True(MessageType.SessionOpenAck.IsSessionLifecycle());
            Assert.True(MessageType.SessionClose.IsSessionLifecycle());
            Assert.True(MessageType.SessionCloseAck.IsSessionLifecycle());
            Assert.False(MessageType.FrameSubmit.IsSessionLifecycle());

            var openMetadata = GoldenSessionOpenMetadata();
            var open = new SessionOpenMessage(
                Header(MessageType.SessionOpen, SessionOpenMetadata.MetadataLength, openMetadata.BodyLength, 0, 0x11),
                openMetadata,
                new byte[openMetadata.BodyLength]);
            Assert.True(SessionOpenMessage.TryParse(open.ToArray(), out var parsedOpen, out var openError));
            Assert.Equal(NnrpParseError.None, openError);
            Assert.Equal(openMetadata, parsedOpen.Metadata);
            Assert.Equal(open.ToArray(), open.ToFramedMessage().ToArray());
            Assert.False(SessionOpenMessage.TryParse(new byte[1], out _, out openError));

            var badOpenPacket = new NnrpFramedMessage(
                Header(MessageType.SessionOpen, SessionOpenMetadata.MetadataLength, openMetadata.BodyLength - 1, 0, 0x11),
                openMetadata.ToArray(),
                new byte[openMetadata.BodyLength - 1]).ToArray();
            Assert.False(SessionOpenMessage.TryParse(badOpenPacket, out _, out var badOpenError));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, badOpenError);
            Assert.Throws<ArgumentException>(() => new SessionOpenMessage(Header(MessageType.SessionOpenAck, SessionOpenMetadata.MetadataLength, openMetadata.BodyLength, 0, 0x11), openMetadata, new byte[openMetadata.BodyLength]));
            Assert.Throws<ArgumentException>(() => new SessionOpenMessage(Header(MessageType.SessionOpen, SessionOpenMetadata.MetadataLength, openMetadata.BodyLength, 0, 0x11), openMetadata, new byte[openMetadata.BodyLength - 1]));

            var ackMetadata = GoldenSessionOpenAckMetadata();
            var ack = new SessionOpenAckMessage(
                Header(MessageType.SessionOpenAck, SessionOpenAckMetadata.MetadataLength, ackMetadata.BodyLength, ackMetadata.SessionId, 0x11),
                ackMetadata,
                new byte[ackMetadata.BodyLength]);
            Assert.True(SessionOpenAckMessage.TryParse(ack.ToArray(), out var parsedAck, out var ackError));
            Assert.Equal(NnrpParseError.None, ackError);
            Assert.Equal(ackMetadata, parsedAck.Metadata);
            Assert.Equal(ack.ToArray(), ack.ToFramedMessage().ToArray());
            Assert.False(SessionOpenAckMessage.TryParse(new byte[1], out _, out ackError));

            var badAckPacket = new NnrpFramedMessage(
                Header(MessageType.SessionOpenAck, SessionOpenAckMetadata.MetadataLength, ackMetadata.BodyLength + 1, ackMetadata.SessionId, 0x11),
                ackMetadata.ToArray(),
                new byte[ackMetadata.BodyLength + 1]).ToArray();
            Assert.False(SessionOpenAckMessage.TryParse(badAckPacket, out _, out var badAckError));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, badAckError);
            Assert.Throws<ArgumentException>(() => new SessionOpenAckMessage(Header(MessageType.SessionOpen, SessionOpenAckMetadata.MetadataLength, ackMetadata.BodyLength, ackMetadata.SessionId, 0x11), ackMetadata, new byte[ackMetadata.BodyLength]));
            Assert.Throws<ArgumentException>(() => new SessionOpenAckMessage(Header(MessageType.SessionOpenAck, SessionOpenAckMetadata.MetadataLength, ackMetadata.BodyLength, ackMetadata.SessionId, 0x11), ackMetadata, new byte[ackMetadata.BodyLength - 1]));

            var closeMetadata = GoldenSessionCloseMetadata();
            var close = new SessionCloseMessage(
                Header(MessageType.SessionClose, SessionCloseMetadata.MetadataLength, 0, ackMetadata.SessionId, 0x22),
                closeMetadata);
            Assert.True(SessionCloseMessage.TryParse(close.ToArray(), out var parsedClose, out var closeError));
            Assert.Equal(NnrpParseError.None, closeError);
            Assert.Equal(closeMetadata, parsedClose.Metadata);
            Assert.Equal(close.ToArray(), close.ToFramedMessage().ToArray());
            Assert.False(SessionCloseMessage.TryParse(new byte[1], out _, out closeError));

            var badClosePacket = new NnrpFramedMessage(
                Header(MessageType.SessionClose, SessionCloseMetadata.MetadataLength, 1, ackMetadata.SessionId, 0x22),
                closeMetadata.ToArray(),
                new byte[1]).ToArray();
            Assert.False(SessionCloseMessage.TryParse(badClosePacket, out _, out var badCloseError));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, badCloseError);
            Assert.Throws<ArgumentException>(() => new SessionCloseMessage(Header(MessageType.SessionCloseAck, SessionCloseMetadata.MetadataLength, 0, ackMetadata.SessionId, 0x22), closeMetadata));

            var closeAckMetadata = GoldenSessionCloseAckMetadata();
            var closeAck = new SessionCloseAckMessage(
                Header(MessageType.SessionCloseAck, SessionCloseAckMetadata.MetadataLength, 0, ackMetadata.SessionId, 0x22),
                closeAckMetadata);
            Assert.True(SessionCloseAckMessage.TryParse(closeAck.ToArray(), out var parsedCloseAck, out var closeAckError));
            Assert.Equal(NnrpParseError.None, closeAckError);
            Assert.Equal(closeAckMetadata, parsedCloseAck.Metadata);
            Assert.Equal(closeAck.ToArray(), closeAck.ToFramedMessage().ToArray());
            Assert.False(SessionCloseAckMessage.TryParse(new byte[1], out _, out closeAckError));

            var badCloseAckPacket = new NnrpFramedMessage(
                Header(MessageType.SessionCloseAck, SessionCloseAckMetadata.MetadataLength, 1, ackMetadata.SessionId, 0x22),
                closeAckMetadata.ToArray(),
                new byte[1]).ToArray();
            Assert.False(SessionCloseAckMessage.TryParse(badCloseAckPacket, out _, out var badCloseAckError));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, badCloseAckError);
            Assert.Throws<ArgumentException>(() => new SessionCloseAckMessage(Header(MessageType.SessionClose, SessionCloseAckMetadata.MetadataLength, 0, ackMetadata.SessionId, 0x22), closeAckMetadata));
        }

        private static SessionOpenMetadata GoldenSessionOpenMetadata()
        {
            return new SessionOpenMetadata(
                requestedSessionId: 42,
                profileId: 2,
                priorityClass: SessionPriorityClass.Balanced,
                sessionFlags: SessionFlags.AllowResume | SessionFlags.AllowCacheLeases,
                schemaId: 4097,
                schemaVersion: 3,
                defaultDeadlineMilliseconds: 500,
                maxInFlightOperations: 4,
                leaseTtlHintMilliseconds: 30000,
                resumeTokenBytes: 16,
                authBytes: 32,
                sessionExtensionBytes: 8,
                clientSessionTag: 0x0123456789ABCDEF);
        }

        private static SessionOpenAckMetadata GoldenSessionOpenAckMetadata()
        {
            return new SessionOpenAckMetadata(
                sessionId: 42,
                acceptedProfileId: 2,
                acceptedPriorityClass: SessionPriorityClass.Balanced,
                sessionStatus: SessionStatus.Opened,
                schemaId: 4097,
                schemaVersion: 3,
                grantedOperationCredit: 2,
                maxInFlightOperations: 4,
                leaseTtlMilliseconds: 30000,
                resumeWindowMilliseconds: 120000,
                resumeTokenBytes: 16,
                sessionExtensionBytes: 8,
                serverSessionTag: 0x0FEDCBA987654321,
                routeScopeId: 7,
                sessionErrorCode: SessionErrorCode.None,
                sessionFlagsAck: SessionAckFlags.ResumeEnabled | SessionAckFlags.CacheLeasesEnabled);
        }

        private static SessionCloseMetadata GoldenSessionCloseMetadata()
        {
            return new SessionCloseMetadata(
                closeReason: SessionCloseReason.ClientShutdown,
                inFlightPolicy: InFlightPolicy.Drain,
                drainTimeoutMilliseconds: 1000,
                lastOperationId: 99,
                sessionErrorCode: SessionErrorCode.None,
                sessionCloseTag: 0x11223344);
        }

        private static SessionCloseAckMetadata GoldenSessionCloseAckMetadata()
        {
            return new SessionCloseAckMetadata(
                closeStatus: SessionCloseStatus.Draining,
                lastOperationId: 99,
                sessionErrorCode: SessionErrorCode.None);
        }

        private static NnrpHeader Header(MessageType messageType, uint metadataLength, uint bodyLength, uint sessionId, ulong traceId)
        {
            return new NnrpHeader(
                versionMajor: NnrpHeader.CurrentVersionMajor,
                messageType: messageType,
                flags: HeaderFlags.None,
                metaLength: metadataLength,
                bodyLength: bodyLength,
                sessionId: sessionId,
                frameId: 0,
                viewId: 0,
                routeId: 0,
                traceId: traceId);
        }
    }
}
