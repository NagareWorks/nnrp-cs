using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Nnrp.Core;

namespace Nnrp.Transport.Tcp
{
    /// <summary>
    /// Streams framed NNRP messages over a TCP byte stream using the protocol header for message boundaries.
    /// </summary>
    public sealed class NnrpTcpMessageTransport : INnrpMessageTransport, IDisposable, IAsyncDisposable
    {
        private readonly SemaphoreSlim receiveGate = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim sendGate = new SemaphoreSlim(1, 1);
        private readonly Stream stream;
        private readonly TcpClient? tcpClient;
        private bool disposed;

        public NnrpTcpMessageTransport(TcpClient tcpClient)
        {
            this.tcpClient = tcpClient ?? throw new ArgumentNullException(nameof(tcpClient));
            if (!tcpClient.Connected)
            {
                throw new ArgumentException("TCP client must already be connected.", nameof(tcpClient));
            }

            tcpClient.NoDelay = true;
            stream = tcpClient.GetStream();
        }

        public static async ValueTask<NnrpTcpMessageTransport> ConnectAsync(
            string host,
            int port,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(host))
            {
                throw new ArgumentException("Host must not be null or empty.", nameof(host));
            }

            if (port <= 0 || port > ushort.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(port));
            }

            var tcpClient = new TcpClient();
            try
            {
                using var registration = cancellationToken.Register(static state => ((TcpClient)state!).Dispose(), tcpClient);
                await tcpClient.ConnectAsync(host, port).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                return new NnrpTcpMessageTransport(tcpClient);
            }
            catch
            {
                tcpClient.Dispose();
                throw;
            }
        }

        public async ValueTask SendAsync(NnrpFramedMessage message, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();

            await sendGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var packet = message.ToArray();
                await stream.WriteAsync(packet, 0, packet.Length, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                sendGate.Release();
            }
        }

        public async ValueTask<NnrpFramedMessage> ReceiveAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();

            await receiveGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var headerBytes = new byte[NnrpHeader.HeaderLength];
                await ReadExactlyAsync(headerBytes, cancellationToken).ConfigureAwait(false);

                if (!NnrpHeader.TryParse(headerBytes, NnrpHeaderParseOptions.Strict, out var header, out var parseError))
                {
                    throw new InvalidOperationException($"Received malformed NNRP header over TCP ({parseError}).");
                }

                var metadata = new byte[checked((int)header.MetaLength)];
                var body = new byte[checked((int)header.BodyLength)];

                if (metadata.Length > 0)
                {
                    await ReadExactlyAsync(metadata, cancellationToken).ConfigureAwait(false);
                }

                if (body.Length > 0)
                {
                    await ReadExactlyAsync(body, cancellationToken).ConfigureAwait(false);
                }

                return new NnrpFramedMessage(header, metadata, body);
            }
            finally
            {
                receiveGate.Release();
            }
        }

        public void Dispose()
        {
            DisposeCore();
            GC.SuppressFinalize(this);
        }

        public ValueTask DisposeAsync()
        {
            DisposeCore();
            GC.SuppressFinalize(this);
            return default;
        }

        private async ValueTask ReadExactlyAsync(byte[] buffer, CancellationToken cancellationToken)
        {
            var offset = 0;
            while (offset < buffer.Length)
            {
                var bytesRead = await stream.ReadAsync(buffer, offset, buffer.Length - offset, cancellationToken).ConfigureAwait(false);
                if (bytesRead == 0)
                {
                    throw new EndOfStreamException("TCP transport reached end of stream before a full NNRP frame was read.");
                }

                offset += bytesRead;
            }
        }

        private void DisposeCore()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            try
            {
                stream.Dispose();
            }
            finally
            {
                tcpClient?.Dispose();
                sendGate.Dispose();
                receiveGate.Dispose();
            }
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(NnrpTcpMessageTransport));
            }
        }
    }
}
