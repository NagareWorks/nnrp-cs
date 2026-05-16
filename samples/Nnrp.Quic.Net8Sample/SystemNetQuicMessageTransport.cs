using System;
using System.Buffers;
using System.Collections.Generic;
using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Runtime.Versioning;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Nnrp.Core;

[assembly: SupportedOSPlatform("linux")]
[assembly: SupportedOSPlatform("macos")]
[assembly: SupportedOSPlatform("windows")]

namespace Nnrp.Quic.Net8Sample
{
    internal sealed class SystemNetQuicMessageTransport : INnrpMessageTransport, IAsyncDisposable
    {
        private static readonly SslApplicationProtocol NnrpAlpn = new SslApplicationProtocol("nnrp/1");

        private readonly Channel<NnrpFramedMessage> inboundMessages = Channel.CreateUnbounded<NnrpFramedMessage>();
        private readonly CancellationTokenSource disposeSource = new CancellationTokenSource();
        private readonly QuicConnection connection;
        private readonly QuicStream controlStream;
        private readonly Task controlReaderTask;
        private readonly Task inboundStreamReaderTask;
        private bool disposed;

        private SystemNetQuicMessageTransport(QuicConnection connection, QuicStream controlStream)
        {
            this.connection = connection;
            this.controlStream = controlStream;
            controlReaderTask = Task.Run(() => ReadControlStreamAsync(disposeSource.Token));
            inboundStreamReaderTask = Task.Run(() => AcceptInboundStreamsAsync(disposeSource.Token));
        }

        public static async Task<SystemNetQuicMessageTransport> ConnectAsync(
            string host,
            int port,
            string tlsServerName,
            bool acceptAnyServerCertificate,
            CancellationToken cancellationToken)
        {
            var connection = await QuicConnection.ConnectAsync(
                new QuicClientConnectionOptions
                {
                    RemoteEndPoint = new DnsEndPoint(host, port),
                    DefaultCloseErrorCode = 0,
                    DefaultStreamErrorCode = 0,
                    MaxInboundBidirectionalStreams = 4,
                    MaxInboundUnidirectionalStreams = 16,
                    ClientAuthenticationOptions = new SslClientAuthenticationOptions
                    {
                        TargetHost = tlsServerName,
                        ApplicationProtocols = new List<SslApplicationProtocol> { NnrpAlpn },
                        RemoteCertificateValidationCallback = acceptAnyServerCertificate
                            ? static (_, _, _, _) => true
                            : null,
                    },
                },
                cancellationToken).ConfigureAwait(false);

            var controlStream = await connection.OpenOutboundStreamAsync(QuicStreamType.Bidirectional, cancellationToken)
                .ConfigureAwait(false);
            return new SystemNetQuicMessageTransport(connection, controlStream);
        }

        public async ValueTask SendAsync(NnrpFramedMessage message, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            var payload = message.ToArray();
            if (message.Header.MessageType == MessageType.FrameSubmit)
            {
                await using var submitStream = await connection.OpenOutboundStreamAsync(QuicStreamType.Unidirectional, cancellationToken)
                    .ConfigureAwait(false);
                await submitStream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
                submitStream.CompleteWrites();
                return;
            }

            await controlStream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        }

        public async ValueTask<NnrpFramedMessage> ReceiveAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            try
            {
                return await inboundMessages.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (ChannelClosedException ex) when (ex.InnerException != null)
            {
                throw new InvalidOperationException("The QUIC receive loop terminated unexpectedly.", ex.InnerException);
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            disposeSource.Cancel();

            Exception? backgroundError = null;
            try
            {
                await Task.WhenAll(controlReaderTask, inboundStreamReaderTask).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (!IsExpectedDisposeException(ex))
                {
                    backgroundError = ex;
                }
            }

            inboundMessages.Writer.TryComplete(backgroundError);
            await DisposeQuietlyAsync(controlStream).ConfigureAwait(false);
            await DisposeQuietlyAsync(connection).ConfigureAwait(false);
            disposeSource.Dispose();

            if (backgroundError != null)
            {
                throw backgroundError;
            }
        }

        private static async ValueTask DisposeQuietlyAsync(IAsyncDisposable disposable)
        {
            try
            {
                await disposable.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex) when (IsExpectedDisposeException(ex))
            {
            }
        }

        private async Task ReadControlStreamAsync(CancellationToken cancellationToken)
        {
            await ReadPacketsFromStreamAsync(controlStream, cancellationToken).ConfigureAwait(false);
        }

        private async Task AcceptInboundStreamsAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var inboundStream = await connection.AcceptInboundStreamAsync(cancellationToken).ConfigureAwait(false);
                    _ = Task.Run(async () =>
                    {
                        await using (inboundStream.ConfigureAwait(false))
                        {
                            await ReadPacketsFromStreamAsync(inboundStream, cancellationToken).ConfigureAwait(false);
                        }
                    }, cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception ex) when (IsExpectedDisposeException(ex))
            {
            }
        }

        private async Task ReadPacketsFromStreamAsync(QuicStream stream, CancellationToken cancellationToken)
        {
            var readBuffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
            var bufferedBytes = new List<byte>();
            try
            {
                while (true)
                {
                    var bytesRead = await stream.ReadAsync(readBuffer, cancellationToken).ConfigureAwait(false);
                    if (bytesRead == 0)
                    {
                        break;
                    }

                    bufferedBytes.AddRange(new ArraySegment<byte>(readBuffer, 0, bytesRead));
                    DrainBufferedPackets(bufferedBytes);
                }

                if (bufferedBytes.Count != 0)
                {
                    throw new InvalidOperationException("QUIC stream ended with a partial NNRP packet buffered.");
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception ex) when (IsExpectedDisposeException(ex))
            {
            }
            catch (Exception ex)
            {
                inboundMessages.Writer.TryComplete(ex);
                throw;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(readBuffer);
            }
        }

        private void DrainBufferedPackets(List<byte> bufferedBytes)
        {
            while (bufferedBytes.Count >= NnrpHeader.HeaderLength)
            {
                var snapshot = bufferedBytes.ToArray();
                if (NnrpFramedMessage.TryParse(snapshot, out var message, out var error))
                {
                    bufferedBytes.RemoveRange(0, message.Length);
                    if (!inboundMessages.Writer.TryWrite(message))
                    {
                        throw new InvalidOperationException("Failed to enqueue an inbound QUIC message.");
                    }

                    continue;
                }

                if (error == NnrpParseError.SourceTooShort)
                {
                    return;
                }

                throw new InvalidOperationException($"Failed to decode NNRP message from QUIC stream ({error}).");
            }
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(SystemNetQuicMessageTransport));
            }
        }

        private static bool IsExpectedDisposeException(Exception exception)
        {
            if (exception is OperationCanceledException || exception is QuicException)
            {
                return true;
            }

            if (exception is not AggregateException aggregateException)
            {
                return false;
            }

            foreach (var innerException in aggregateException.InnerExceptions)
            {
                if (!IsExpectedDisposeException(innerException))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
