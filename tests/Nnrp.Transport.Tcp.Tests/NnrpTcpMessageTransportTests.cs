using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Nnrp.Core;
using Nnrp.Transport.Tcp;
using Xunit;

namespace Nnrp.Transport.Tcp.Tests
{
    public sealed class NnrpTcpMessageTransportTests
    {
        [Fact]
        public void ConstructorRejectsNullAndUnconnectedClients()
        {
            Assert.Throws<ArgumentNullException>(() => new NnrpTcpMessageTransport(null!));
            Assert.Throws<ArgumentException>(() => new NnrpTcpMessageTransport(new TcpClient()));
        }

        [Fact]
        public async Task TransportReportsTcpIdentity()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var endpoint = (IPEndPoint)listener.LocalEndpoint;
            var acceptTask = listener.AcceptTcpClientAsync();

            await using var transport = await NnrpTcpMessageTransport.ConnectAsync(
                IPAddress.Loopback.ToString(),
                endpoint.Port,
                timeout.Token);
            using var accepted = await acceptTask;

            var identity = Assert.IsAssignableFrom<INnrpTransportIdentity>(transport);
            Assert.Equal(TransportId.Tcp, identity.TransportId);
        }

        [Fact]
        public async Task ConnectAsyncRejectsInvalidArgumentsAndConnectionFailures()
        {
            await Assert.ThrowsAsync<ArgumentException>(async () =>
                await NnrpTcpMessageTransport.ConnectAsync(string.Empty, 5000, CancellationToken.None));
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
                await NnrpTcpMessageTransport.ConnectAsync("127.0.0.1", 0, CancellationToken.None));
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
                await NnrpTcpMessageTransport.ConnectAsync("127.0.0.1", 70000, CancellationToken.None));

            var unusedPort = FindUnusedLocalPort();
            await Assert.ThrowsAnyAsync<Exception>(async () =>
                await NnrpTcpMessageTransport.ConnectAsync("127.0.0.1", unusedPort, CancellationToken.None));
        }

        [Fact]
        public async Task ConnectAsyncCancelsPendingDial()
        {
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<Exception>(async () =>
                await NnrpTcpMessageTransport.ConnectAsync("127.0.0.1", 65000, cancellation.Token));
        }

        [Fact]
        public async Task SendAsyncWritesFullPacketToStream()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var endpoint = (IPEndPoint)listener.LocalEndpoint;
            var expected = CreateMessage(MessageType.Ping, metadata: new byte[] { 1, 2, 3 }, body: new byte[] { 4, 5 });

            var serverTask = Task.Run(async () =>
            {
                using var accepted = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
                using var serverStream = accepted.GetStream();
                return await ReadFrameAsync(serverStream, timeout.Token).ConfigureAwait(false);
            }, timeout.Token);

            await using var transport = await NnrpTcpMessageTransport.ConnectAsync(
                IPAddress.Loopback.ToString(),
                endpoint.Port,
                timeout.Token);

            await transport.SendAsync(expected, timeout.Token);

            var received = await serverTask;
            Assert.Equal(expected.Header, received.Header);
            Assert.Equal(expected.Metadata.ToArray(), received.Metadata.ToArray());
            Assert.Equal(expected.Body.ToArray(), received.Body.ToArray());
        }

        [Fact]
        public async Task ReceiveAsyncReadsFullPacketFromStream()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var endpoint = (IPEndPoint)listener.LocalEndpoint;
            var expected = CreateMessage(MessageType.Pong, metadata: new byte[] { 9, 8 }, body: new byte[] { 7, 6, 5 });

            var serverTask = Task.Run(async () =>
            {
                using var accepted = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
                using var serverStream = accepted.GetStream();
                var packet = expected.ToArray();
                await serverStream.WriteAsync(packet, 0, packet.Length, timeout.Token).ConfigureAwait(false);
                await serverStream.FlushAsync(timeout.Token).ConfigureAwait(false);
            }, timeout.Token);

            await using var transport = await NnrpTcpMessageTransport.ConnectAsync(
                IPAddress.Loopback.ToString(),
                endpoint.Port,
                timeout.Token);

            var received = await transport.ReceiveAsync(timeout.Token);
            await serverTask;

            Assert.Equal(expected.Header, received.Header);
            Assert.Equal(expected.Metadata.ToArray(), received.Metadata.ToArray());
            Assert.Equal(expected.Body.ToArray(), received.Body.ToArray());
        }

        [Fact]
        public async Task ReceiveAsyncRejectsMalformedHeaders()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var endpoint = (IPEndPoint)listener.LocalEndpoint;

            var serverTask = Task.Run(async () =>
            {
                using var accepted = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
                using var serverStream = accepted.GetStream();
                var malformed = new byte[NnrpHeader.HeaderLength];
                malformed[0] = (byte)'B';
                malformed[1] = (byte)'A';
                malformed[2] = (byte)'D';
                malformed[3] = (byte)'!';
                await serverStream.WriteAsync(malformed, 0, malformed.Length, timeout.Token).ConfigureAwait(false);
                await serverStream.FlushAsync(timeout.Token).ConfigureAwait(false);
            }, timeout.Token);

            await using var transport = await NnrpTcpMessageTransport.ConnectAsync(
                IPAddress.Loopback.ToString(),
                endpoint.Port,
                timeout.Token);

            var error = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await transport.ReceiveAsync(timeout.Token));

            Assert.Contains("malformed NNRP header", error.Message, StringComparison.Ordinal);
            await serverTask;
        }

        [Fact]
        public async Task ReceiveAsyncRejectsPrematureEndOfStream()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var endpoint = (IPEndPoint)listener.LocalEndpoint;
            var message = CreateMessage(MessageType.FrameSubmit, metadata: new byte[] { 1, 2 }, body: new byte[] { 3, 4, 5, 6 });
            var packet = message.ToArray();

            var serverTask = Task.Run(async () =>
            {
                using var accepted = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
                using var serverStream = accepted.GetStream();
                await serverStream.WriteAsync(packet, 0, packet.Length - 2, timeout.Token).ConfigureAwait(false);
                await serverStream.FlushAsync(timeout.Token).ConfigureAwait(false);
                accepted.Client.Shutdown(SocketShutdown.Send);
            }, timeout.Token);

            await using var transport = await NnrpTcpMessageTransport.ConnectAsync(
                IPAddress.Loopback.ToString(),
                endpoint.Port,
                timeout.Token);

            await Assert.ThrowsAsync<EndOfStreamException>(async () =>
                await transport.ReceiveAsync(timeout.Token));
            await serverTask;
        }

        [Fact]
        public async Task DisposedTransportRejectsSendAndReceive()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var endpoint = (IPEndPoint)listener.LocalEndpoint;

            var serverTask = listener.AcceptTcpClientAsync();
            var client = await NnrpTcpMessageTransport.ConnectAsync(IPAddress.Loopback.ToString(), endpoint.Port, timeout.Token);
            using var accepted = await serverTask;

            client.Dispose();
            client.Dispose();
            await client.DisposeAsync();

            await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
                await client.SendAsync(CreateMessage(MessageType.Ping), timeout.Token));
            await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
                await client.ReceiveAsync(timeout.Token));
        }

        private static async Task<NnrpFramedMessage> ReadFrameAsync(NetworkStream stream, CancellationToken cancellationToken)
        {
            var headerBytes = new byte[NnrpHeader.HeaderLength];
            await ReadExactlyAsync(stream, headerBytes, cancellationToken).ConfigureAwait(false);
            Assert.True(NnrpHeader.TryParse(headerBytes, NnrpHeaderParseOptions.Strict, out var header, out var error));
            Assert.Equal(NnrpParseError.None, error);

            var metadata = new byte[header.MetaLength];
            var body = new byte[header.BodyLength];
            await ReadExactlyAsync(stream, metadata, cancellationToken).ConfigureAwait(false);
            await ReadExactlyAsync(stream, body, cancellationToken).ConfigureAwait(false);
            return new NnrpFramedMessage(header, metadata, body);
        }

        private static async Task ReadExactlyAsync(NetworkStream stream, byte[] buffer, CancellationToken cancellationToken)
        {
            var offset = 0;
            while (offset < buffer.Length)
            {
                var bytesRead = await stream.ReadAsync(buffer, offset, buffer.Length - offset, cancellationToken).ConfigureAwait(false);
                if (bytesRead == 0)
                {
                    throw new EndOfStreamException();
                }

                offset += bytesRead;
            }
        }

        private static NnrpFramedMessage CreateMessage(
            MessageType messageType,
            byte[]? metadata = null,
            byte[]? body = null)
        {
            metadata ??= Array.Empty<byte>();
            body ??= Array.Empty<byte>();
            var header = new NnrpHeader(
                versionMajor: NnrpHeader.CurrentVersionMajor,
                wireFormat: NnrpHeader.CurrentWireFormat,
                messageType: messageType,
                flags: HeaderFlags.None,
                metaLength: (uint)metadata.Length,
                bodyLength: (uint)body.Length,
                sessionId: 1,
                frameId: 2,
                viewId: 0,
                routeId: 0,
                traceId: 3);
            return new NnrpFramedMessage(header, metadata, body);
        }

        private static int FindUnusedLocalPort()
        {
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }
    }
}
