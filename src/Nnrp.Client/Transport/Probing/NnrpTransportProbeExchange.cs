using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Nnrp.Core;

namespace Nnrp.Client
{
    public static class NnrpTransportProbeExchange
    {
        public static async ValueTask<NnrpTransportProbeSampleResult> ProbeAsync(
            TransportId transportId,
            string bindingName,
            INnrpMessageTransport transport,
            NnrpTransportProbeRequest request,
            ulong traceId = 0,
            CancellationToken cancellationToken = default)
        {
            if (transportId == TransportId.Unspecified)
            {
                throw new ArgumentOutOfRangeException(nameof(transportId));
            }

            if (string.IsNullOrWhiteSpace(bindingName))
            {
                throw new ArgumentException("Binding name must not be null or empty.", nameof(bindingName));
            }

            if (transport == null)
            {
                throw new ArgumentNullException(nameof(transport));
            }

            cancellationToken.ThrowIfCancellationRequested();

            var payload = new byte[request.PayloadBytes];
            for (var i = 0; i < payload.Length; i++)
            {
                payload[i] = unchecked((byte)(request.SampleIndex + i));
            }

            var probeId = checked((uint)request.SampleIndex);
            var clientSendTimestampMicroseconds = GetUtcMicroseconds();
            var probe = new TransportProbeMessage(
                new NnrpHeader(
                    versionMajor: NnrpHeader.CurrentVersionMajor,
                    messageType: MessageType.TransportProbe,
                    flags: HeaderFlags.AckRequired,
                    metaLength: TransportProbeMetadata.MetadataLength,
                    bodyLength: (uint)payload.Length,
                    sessionId: 0,
                    frameId: 0,
                    viewId: 0,
                    routeId: 0,
                    traceId: traceId),
                new TransportProbeMetadata(
                    probeId,
                    (uint)payload.Length,
                    clientSendTimestampMicroseconds),
                payload);

            var stopwatch = Stopwatch.StartNew();
            await transport.SendAsync(probe.ToFramedMessage(), cancellationToken).ConfigureAwait(false);
            var response = await transport.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();

            if (!TransportProbeAckMessage.TryParse(response, out var ack, out var parseError))
            {
                throw new InvalidOperationException(
                    $"Expected TRANSPORT_PROBE_ACK after TRANSPORT_PROBE, received {response.Header.MessageType} ({parseError}).");
            }

            if (ack.Metadata.ProbeId != probeId)
            {
                throw new InvalidOperationException("TRANSPORT_PROBE_ACK correlation mismatch.");
            }

            return new NnrpTransportProbeSampleResult(
                transportId,
                bindingName,
                isSuccess: true,
                payloadBytes: payload.Length,
                roundTripMicroseconds: ToMicroseconds(stopwatch.Elapsed));
        }

        public static NnrpTransportConnectBinding CreateConnectionBinding(
            TransportId transportId,
            string bindingName,
            Func<CancellationToken, ValueTask<INnrpMessageTransport>> connectAsync,
            ulong traceId = 0)
        {
            if (connectAsync == null)
            {
                throw new ArgumentNullException(nameof(connectAsync));
            }

            var probeBinding = new NnrpTransportProbeBinding(
                transportId,
                bindingName,
                async (request, cancellationToken) =>
                {
                    var transport = await connectAsync(cancellationToken).ConfigureAwait(false);
                    try
                    {
                        return await ProbeAsync(transportId, bindingName, transport, request, traceId, cancellationToken).ConfigureAwait(false);
                    }
                    finally
                    {
                        await DisposeTransportAsync(transport).ConfigureAwait(false);
                    }
                });

            return new NnrpTransportConnectBinding(probeBinding, connectAsync);
        }

        internal static async ValueTask DisposeTransportAsync(INnrpMessageTransport transport)
        {
            if (transport is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                return;
            }

            if (transport is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        private static long ToMicroseconds(TimeSpan elapsed)
        {
            return elapsed.Ticks / (TimeSpan.TicksPerMillisecond / 1000);
        }

        private static ulong GetUtcMicroseconds()
        {
            var now = DateTimeOffset.UtcNow;
            var microseconds = (ulong)now.ToUnixTimeMilliseconds() * 1000UL;
            microseconds += (ulong)((now.Ticks % TimeSpan.TicksPerMillisecond) / 10);
            return microseconds;
        }
    }
}
