using System;
using System.Threading;
using System.Threading.Tasks;
using Nnrp.Core;
using Xunit;

namespace Nnrp.Core.Tests
{
    public sealed class TransportCoreTests
    {
        [Fact]
        public void FramedMessageCopiesHeaderMetadataAndBodyToDestination()
        {
            var metadata = new byte[] { 1, 2, 3 };
            var body = new byte[] { 4, 5, 6, 7 };
            var header = CreateHeader((uint)metadata.Length, (uint)body.Length);
            var message = new NnrpFramedMessage(header, metadata, body);
            var destination = new byte[message.Length];

            Assert.True(message.TryCopyTo(destination, out var bytesWritten));
            Assert.Equal(destination.Length, bytesWritten);
            Assert.Equal(destination, message.ToArray());
            Assert.True(NnrpHeader.TryParse(destination, NnrpHeaderParseOptions.Strict, out var parsedHeader, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(header, parsedHeader);
            Assert.Equal(metadata, destination.AsSpan(NnrpHeader.HeaderLength, metadata.Length).ToArray());
            Assert.Equal(body, destination.AsSpan(NnrpHeader.HeaderLength + metadata.Length, body.Length).ToArray());
        }

        [Fact]
        public void FramedMessageRejectsLengthMismatchesAndShortDestinations()
        {
            var header = CreateHeader(metaLength: 2, bodyLength: 1);

            Assert.Throws<ArgumentException>(() => new NnrpFramedMessage(header, new byte[] { 1 }, new byte[] { 2 }));
            Assert.Throws<ArgumentException>(() => new NnrpFramedMessage(header, new byte[] { 1, 2 }, Array.Empty<byte>()));

            var message = new NnrpFramedMessage(header, new byte[] { 1, 2 }, new byte[] { 3 });
            Assert.False(message.TryCopyTo(new byte[message.Length - 1], out var bytesWritten));
            Assert.Equal(0, bytesWritten);
            Assert.Throws<ArgumentException>(() => message.CopyTo(new byte[message.Length - 1]));

            var invalidHeaderMessage = new NnrpFramedMessage(
                CreateHeader(metaLength: 0, bodyLength: 0, headerLength: 24),
                Array.Empty<byte>(),
                Array.Empty<byte>());
            Assert.False(invalidHeaderMessage.TryCopyTo(new byte[invalidHeaderMessage.Length], out bytesWritten));
            Assert.Equal(0, bytesWritten);
            Assert.Throws<InvalidOperationException>(() => invalidHeaderMessage.CopyTo(new byte[invalidHeaderMessage.Length]));
        }

        [Fact]
        public void FramedMessageTryParseReturnsCallerOwnedMemorySlices()
        {
            var metadata = new byte[] { 10, 11 };
            var body = new byte[] { 12, 13, 14 };
            var original = new NnrpFramedMessage(CreateHeader(2, 3), metadata, body);
            var packet = original.ToArray();

            Assert.True(NnrpFramedMessage.TryParse(packet, NnrpHeaderParseOptions.Strict, out var parsed, out var error));

            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(original.Header, parsed.Header);
            Assert.Equal(metadata, parsed.Metadata.ToArray());
            Assert.Equal(body, parsed.Body.ToArray());

            packet[NnrpHeader.HeaderLength] = 99;
            Assert.Equal(99, parsed.Metadata.Span[0]);
        }

        [Fact]
        public void FramedMessageTryParseReportsMalformedFrames()
        {
            Assert.False(NnrpFramedMessage.TryParse(Array.Empty<byte>(), NnrpHeaderParseOptions.Strict, out _, out var error));
            Assert.Equal(NnrpParseError.SourceTooShort, error);

            var message = new NnrpFramedMessage(CreateHeader(1, 2), new byte[] { 1 }, new byte[] { 2, 3 });
            var truncated = message.ToArray().AsMemory(0, message.Length - 1);
            Assert.False(NnrpFramedMessage.TryParse(truncated, NnrpHeaderParseOptions.Strict, out _, out error));
            Assert.Equal(NnrpParseError.SourceTooShort, error);

            var invalidMagic = message.ToArray();
            invalidMagic[0] = 0;
            Assert.False(NnrpFramedMessage.TryParse(invalidMagic, NnrpHeaderParseOptions.Strict, out _, out error));
            Assert.Equal(NnrpParseError.InvalidMagic, error);
        }

        [Fact]
        public async Task MessageTransportInterfacesCarryMessagesAndCancellationTokens()
        {
            var cancellationToken = new CancellationTokenSource().Token;
            var inbound = new NnrpFramedMessage(CreateHeader(0, 0, MessageType.Pong), Array.Empty<byte>(), Array.Empty<byte>());
            var outbound = new NnrpFramedMessage(CreateHeader(0, 0, MessageType.Ping), Array.Empty<byte>(), Array.Empty<byte>());
            var transport = new RecordingTransport(inbound);

            await transport.SendAsync(outbound, cancellationToken);
            var received = await transport.ReceiveAsync(cancellationToken);

            Assert.Equal(outbound.Header, transport.LastSent.Header);
            Assert.Equal(cancellationToken, transport.LastSendCancellationToken);
            Assert.Equal(inbound.Header, received.Header);
            Assert.Equal(cancellationToken, transport.LastReceiveCancellationToken);
        }

        private static NnrpHeader CreateHeader(uint metaLength, uint bodyLength, MessageType messageType = MessageType.FrameSubmit, byte headerLength = NnrpHeader.HeaderLength)
        {
            return new NnrpHeader(
                NnrpHeader.CurrentVersionMajor,
                NnrpHeader.CurrentWireFormat,
                messageType,
                HeaderFlags.None,
                metaLength,
                bodyLength,
                sessionId: 1,
                frameId: 2,
                viewId: 0,
                routeId: 0,
                traceId: 3,
                headerLength: headerLength);
        }

        private sealed class RecordingTransport : INnrpMessageTransport
        {
            private readonly NnrpFramedMessage inbound;

            public RecordingTransport(NnrpFramedMessage inbound)
            {
                this.inbound = inbound;
            }

            public NnrpFramedMessage LastSent { get; private set; }

            public CancellationToken LastSendCancellationToken { get; private set; }

            public CancellationToken LastReceiveCancellationToken { get; private set; }

            public ValueTask SendAsync(NnrpFramedMessage message, CancellationToken cancellationToken)
            {
                LastSent = message;
                LastSendCancellationToken = cancellationToken;
                return default;
            }

            public ValueTask<NnrpFramedMessage> ReceiveAsync(CancellationToken cancellationToken)
            {
                LastReceiveCancellationToken = cancellationToken;
                return new ValueTask<NnrpFramedMessage>(inbound);
            }
        }
    }
}
