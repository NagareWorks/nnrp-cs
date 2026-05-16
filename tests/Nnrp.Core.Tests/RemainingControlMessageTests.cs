using System;
using Nnrp.Core;
using Xunit;

namespace Nnrp.Core.Tests
{
    public sealed class RemainingControlMessageTests
    {
        [Fact]
        public void SessionPatchRoundTripsThroughFixedWidthCodec()
        {
            var metadata = new SessionPatchMetadata(
                profileId: 0,
                SessionPatchField.TargetCadence | SessionPatchField.DegradePolicy | SessionPatchField.ActiveLaneMask | SessionPatchField.PreferredCodec | SessionPatchField.ProfilePatch,
                6000,
                2,
                3,
                0x0000000000000003,
                0x3,
                0x3,
                TensorProfilePatchBlock.BlockLength);
            var profilePatchBlock = new TensorProfilePatchBlock(320, 180, 1920, 1080);
            var message = new SessionPatchMessage(
                new NnrpHeader(1, NnrpHeader.CurrentWireFormat, MessageType.SessionPatch, HeaderFlags.None, SessionPatchMetadata.MetadataLength, TensorProfilePatchBlock.BlockLength, 11, 0, 0, 0, 1),
                metadata,
                profilePatchBlock);

            Assert.True(SessionPatchMessage.TryParse(message.ToArray(), out var parsed, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(message.ToArray(), parsed.ToArray());
            Assert.True(parsed.ProfilePatchBlock.HasValue);
            Assert.Equal(profilePatchBlock, parsed.ProfilePatchBlock.Value);
        }

        [Fact]
        public void SessionPatchAckRoundTripsThroughFixedWidthCodec()
        {
            var metadata = new SessionPatchAckMetadata(
                SessionPatchAckStatus.PartiallyApplied,
                SessionPatchRejectReason.UnsupportedValue,
                SessionPatchField.TargetCadence | SessionPatchField.ProfilePatch,
                SessionPatchField.PreferredCompression,
                100,
                1,
                6000,
                2,
                1,
                0x0000000000000001,
                0x1,
                0x1,
                TensorProfilePatchAckBlock.BlockLength);
            var profilePatchAckBlock = new TensorProfilePatchAckBlock(320, 180, 1920, 1080);
            var message = new SessionPatchAckMessage(
                new NnrpHeader(1, NnrpHeader.CurrentWireFormat, MessageType.SessionPatchAck, HeaderFlags.None, SessionPatchAckMetadata.MetadataLength, TensorProfilePatchAckBlock.BlockLength, 11, 0, 0, 0, 2),
                metadata,
                profilePatchAckBlock);

            Assert.True(SessionPatchAckMessage.TryParse(message.ToArray(), out var parsed, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(message.ToArray(), parsed.ToArray());
            Assert.True(parsed.ProfilePatchAckBlock.HasValue);
            Assert.Equal(profilePatchAckBlock, parsed.ProfilePatchAckBlock.Value);
        }

        [Fact]
        public void CloseMessageRoundTripsUtf8Reason()
        {
            var message = CloseMessage.Create(7, "smoke-close");

            Assert.True(CloseMessage.TryParse(message.ToArray(), out var parsed, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal("smoke-close", parsed.Reason);
        }

        [Fact]
        public void FrameCancelMessageRoundTripsHeaderOnlyPayload()
        {
            var message = FrameCancelMessage.Create(sessionId: 7, frameId: 11, viewId: 2, traceId: 99);

            Assert.True(FrameCancelMessage.TryParse(message.ToArray(), out var parsed, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(MessageType.FrameCancel, parsed.Header.MessageType);
            Assert.Equal(HeaderFlags.CanDrop, parsed.Header.Flags);
            Assert.Equal(11u, parsed.Header.FrameId);
            Assert.Equal(2, parsed.Header.ViewId);
        }

        [Fact]
        public void PingAndPongMessagesRoundTripHeaderOnlyPayloads()
        {
            var ping = PingMessage.Create(sessionId: 7, traceId: 10);
            var pong = PongMessage.Create(sessionId: 7, traceId: 10);

            Assert.True(PingMessage.TryParse(ping.ToArray(), out var parsedPing, out var pingError));
            Assert.True(PongMessage.TryParse(pong.ToArray(), out var parsedPong, out var pongError));
            Assert.Equal(NnrpParseError.None, pingError);
            Assert.Equal(NnrpParseError.None, pongError);
            Assert.Equal(HeaderFlags.CanDrop, parsedPing.Header.Flags);
            Assert.Equal(HeaderFlags.CanDrop, parsedPong.Header.Flags);
            Assert.Equal(10ul, parsedPong.Header.TraceId);
        }

        [Fact]
        public void ErrorMessageMapsToAndFromProtocolFailure()
        {
            var failure = NnrpProtocolFailure.LimitExceeded(NnrpErrorScope.Session, "too many views", isFatal: true);
            var message = ErrorMessage.FromProtocolFailure(failure, relatedSessionId: 3, retryAfterMilliseconds: 200);

            Assert.True(ErrorMessage.TryParse(message.ToArray(), out var parsed, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(failure, parsed.ToProtocolFailure());
            Assert.Equal(3u, parsed.Metadata.RelatedSessionId);
        }

        [Fact]
        public void ErrorMessageRejectsDiagnosticLengthMismatch()
        {
            var framed = new NnrpFramedMessage(
                new NnrpHeader(1, NnrpHeader.CurrentWireFormat, MessageType.Error, HeaderFlags.None, ErrorMetadata.MetadataLength, 1, 0, 0, 0, 0, 0),
                new ErrorMetadata(ErrorCode.InternalError, NnrpErrorScope.Connection, true, 0, 0, 0, 0, 2).ToArray(),
                new byte[] { 1 });

            Assert.False(ErrorMessage.TryParse(framed.ToArray(), out _, out var error));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, error);
        }

        [Fact]
        public void CachePutRoundTripsBodyAndMetadata()
        {
            var message = new CachePutMessage(
                new NnrpHeader(1, NnrpHeader.CurrentWireFormat, MessageType.CachePut, HeaderFlags.None, CachePutMetadata.MetadataLength, 3, 5, 0, 0, 0, 0),
                new CachePutMetadata(1, 2, 3, CacheObjectKind.CameraBlock, 1000, 3, 0x1, CachePutFlags.Pinned),
                new byte[] { 9, 8, 7 });

            Assert.True(CachePutMessage.TryParse(message.ToArray(), out var parsed, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(message.ToArray(), parsed.ToArray());
        }

        [Fact]
        public void CacheAckRoundTripsThroughFixedWidthCodec()
        {
            var message = new CacheAckMessage(
                new NnrpHeader(1, NnrpHeader.CurrentWireFormat, MessageType.CacheAck, HeaderFlags.None, CacheAckMetadata.MetadataLength, 0, 5, 0, 0, 0, 0),
                new CacheAckMetadata(1, 2, 3, CacheAckStatus.Replaced, 900, 1024, 7));

            Assert.True(CacheAckMessage.TryParse(message.ToArray(), out var parsed, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(message.ToArray(), parsed.ToArray());
        }

        [Fact]
        public void CacheInvalidateRoundTripsThroughFixedWidthCodec()
        {
            var message = new CacheInvalidateMessage(
                new NnrpHeader(1, NnrpHeader.CurrentWireFormat, MessageType.CacheInvalidate, HeaderFlags.None, CacheInvalidateMetadata.MetadataLength, 0, 5, 0, 0, 0, 0),
                new CacheInvalidateMetadata(CacheInvalidateScope.Namespace, 1, 2, 3, 4));

            Assert.True(CacheInvalidateMessage.TryParse(message.ToArray(), out var parsed, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(message.ToArray(), parsed.ToArray());
        }
    }
}
