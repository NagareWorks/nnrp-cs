using System;
using Nnrp.Client;
using Nnrp.Core;

namespace Nnrp.NativeBridge
{
    public readonly struct NnrpQuicClientOptions
    {
        public NnrpQuicClientOptions(
            string host,
            ushort port,
            string tlsServerName,
            string requestedModel,
            uint requestedSessionId = 11)
        {
            if (string.IsNullOrWhiteSpace(host))
            {
                throw new ArgumentException("Host must not be empty.", nameof(host));
            }

            if (string.IsNullOrWhiteSpace(tlsServerName))
            {
                throw new ArgumentException("TLS server name must not be empty.", nameof(tlsServerName));
            }

            if (string.IsNullOrWhiteSpace(requestedModel))
            {
                throw new ArgumentException("Requested model must not be empty.", nameof(requestedModel));
            }

            Host = host;
            Port = port;
            TlsServerName = tlsServerName;
            RequestedModel = requestedModel;
            RequestedSessionId = requestedSessionId;
        }

        public string Host { get; }

        public ushort Port { get; }

        public string TlsServerName { get; }

        public string RequestedModel { get; }

        public uint RequestedSessionId { get; }

        public byte RequestedWireFormat => NnrpHeader.CurrentWireFormat;
    }

    public sealed class NnrpQuicClient : IDisposable
    {
        private readonly Func<string, ushort, string, string, uint, NnrpNativeQuicClient.OpenResult> openConnection;
        private readonly Func<ulong, FrameSubmitMessage, ResultPushMessage> submitFrame;
        private readonly Func<ulong, byte[], byte[]> submitOutcomeBytes;
        private readonly Action<ulong, byte[]> beginSubmitPacket;
        private readonly Func<ulong, byte[]> receiveResultPacket;
        private readonly Func<ulong, byte[]> receiveSessionPacket;
        private readonly Func<ulong, PingMessage, PongMessage> pingRoundTrip;
        private readonly Action<ulong, FrameCancelMessage> cancelFrame;
        private readonly Action<ulong> closeConnection;
        private bool disposed;

        public NnrpQuicClient(NnrpQuicClientOptions options)
            : this(
                options,
                NnrpNativeQuicClient.Open,
                NnrpNativeQuicClient.Submit,
                NnrpNativeQuicClient.Submit,
                NnrpNativeQuicClient.BeginSubmit,
                NnrpNativeQuicClient.ReceiveResult,
                NnrpNativeQuicClient.ReceiveSessionPacket,
                NnrpNativeQuicClient.Ping,
                NnrpNativeQuicClient.Cancel,
                NnrpNativeQuicClient.Close)
        {
        }

        public NnrpQuicClient(ClientProfile profile, NnrpQuicClientOptions options)
            : this(
            profile,
            options,
            NnrpNativeQuicClient.Open,
            NnrpNativeQuicClient.Submit,
            NnrpNativeQuicClient.Submit,
            NnrpNativeQuicClient.BeginSubmit,
            NnrpNativeQuicClient.ReceiveResult,
            NnrpNativeQuicClient.ReceiveSessionPacket,
            NnrpNativeQuicClient.Ping,
            NnrpNativeQuicClient.Cancel,
            NnrpNativeQuicClient.Close)
        {
        }

        internal NnrpQuicClient(
            NnrpQuicClientOptions options,
            Func<string, ushort, string, string, uint, NnrpNativeQuicClient.OpenResult> openConnection,
            Func<ulong, FrameSubmitMessage, ResultPushMessage> submitFrame,
            Func<ulong, byte[], byte[]> submitOutcomeBytes,
            Func<ulong, PingMessage, PongMessage> pingRoundTrip,
            Action<ulong, FrameCancelMessage> cancelFrame,
            Action<ulong> closeConnection)
            : this(
                options,
                openConnection,
                submitFrame,
                submitOutcomeBytes,
                (_, __) => throw new NotSupportedException("Background submit/result receive is not configured for this QUIC client."),
                _ => throw new NotSupportedException("Background result receive is not configured for this QUIC client."),
                _ => throw new NotSupportedException("Background session receive is not configured for this QUIC client."),
                pingRoundTrip,
                cancelFrame,
                closeConnection)
        {
        }

        internal NnrpQuicClient(
            NnrpQuicClientOptions options,
            Func<string, ushort, string, string, uint, NnrpNativeQuicClient.OpenResult> openConnection,
            Func<ulong, FrameSubmitMessage, ResultPushMessage> submitFrame,
            Func<ulong, byte[], byte[]> submitOutcomeBytes,
            Action<ulong, byte[]> beginSubmitPacket,
            Func<ulong, byte[]> receiveResultPacket,
            Func<ulong, byte[]> receiveSessionPacket,
            Func<ulong, PingMessage, PongMessage> pingRoundTrip,
            Action<ulong, FrameCancelMessage> cancelFrame,
            Action<ulong> closeConnection)
        {
            Options = options;
            this.openConnection = openConnection ?? throw new ArgumentNullException(nameof(openConnection));
            this.submitFrame = submitFrame ?? throw new ArgumentNullException(nameof(submitFrame));
            this.submitOutcomeBytes = submitOutcomeBytes ?? throw new ArgumentNullException(nameof(submitOutcomeBytes));
            this.beginSubmitPacket = beginSubmitPacket ?? throw new ArgumentNullException(nameof(beginSubmitPacket));
            this.receiveResultPacket = receiveResultPacket ?? throw new ArgumentNullException(nameof(receiveResultPacket));
            this.receiveSessionPacket = receiveSessionPacket ?? throw new ArgumentNullException(nameof(receiveSessionPacket));
            this.pingRoundTrip = pingRoundTrip ?? throw new ArgumentNullException(nameof(pingRoundTrip));
            this.cancelFrame = cancelFrame ?? throw new ArgumentNullException(nameof(cancelFrame));
            this.closeConnection = closeConnection ?? throw new ArgumentNullException(nameof(closeConnection));
            ActiveModelName = string.Empty;
        }

        internal NnrpQuicClient(
            NnrpQuicClientOptions options,
            Func<string, ushort, string, string, uint, NnrpNativeQuicClient.OpenResult> openConnection,
            Func<ulong, FrameSubmitMessage, ResultPushMessage> submitFrame,
            Func<ulong, byte[], byte[]> submitOutcomeBytes,
            Action<ulong, byte[]> beginSubmitPacket,
            Func<ulong, byte[]> receiveResultPacket,
            Func<ulong, PingMessage, PongMessage> pingRoundTrip,
            Action<ulong, FrameCancelMessage> cancelFrame,
            Action<ulong> closeConnection)
            : this(
                options,
                openConnection,
                submitFrame,
                submitOutcomeBytes,
                beginSubmitPacket,
                receiveResultPacket,
                receiveResultPacket,
                pingRoundTrip,
                cancelFrame,
                closeConnection)
        {
        }

        internal NnrpQuicClient(
            ClientProfile profile,
            NnrpQuicClientOptions options,
            Func<string, ushort, string, string, uint, NnrpNativeQuicClient.OpenResult> openConnection,
            Func<ulong, FrameSubmitMessage, ResultPushMessage> submitFrame,
            Func<ulong, byte[], byte[]> submitOutcomeBytes,
            Func<ulong, PingMessage, PongMessage> pingRoundTrip,
            Action<ulong, FrameCancelMessage> cancelFrame,
            Action<ulong> closeConnection)
            : this(
                profile,
                options,
                openConnection,
                submitFrame,
                submitOutcomeBytes,
                (_, __) => throw new NotSupportedException("Background submit/result receive is not configured for this QUIC client."),
                _ => throw new NotSupportedException("Background result receive is not configured for this QUIC client."),
                _ => throw new NotSupportedException("Background session receive is not configured for this QUIC client."),
                pingRoundTrip,
                cancelFrame,
                closeConnection)
        {
        }

        internal NnrpQuicClient(
            ClientProfile profile,
            NnrpQuicClientOptions options,
            Func<string, ushort, string, string, uint, NnrpNativeQuicClient.OpenResult> openConnection,
            Func<ulong, FrameSubmitMessage, ResultPushMessage> submitFrame,
            Func<ulong, byte[], byte[]> submitOutcomeBytes,
            Action<ulong, byte[]> beginSubmitPacket,
            Func<ulong, byte[]> receiveResultPacket,
            Func<ulong, byte[]> receiveSessionPacket,
            Func<ulong, PingMessage, PongMessage> pingRoundTrip,
            Action<ulong, FrameCancelMessage> cancelFrame,
            Action<ulong> closeConnection)
            : this(options, openConnection, submitFrame, submitOutcomeBytes, beginSubmitPacket, receiveResultPacket, receiveSessionPacket, pingRoundTrip, cancelFrame, closeConnection)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            if (profile.TransportProfile != NnrpTransportProfile.Quic)
            {
                throw new InvalidOperationException(
                    $"ClientProfile.TransportProfile must be {NnrpTransportProfile.Quic} to create {nameof(NnrpQuicClient)}.");
            }

            Profile = profile;
        }

        internal NnrpQuicClient(
            ClientProfile profile,
            NnrpQuicClientOptions options,
            Func<string, ushort, string, string, uint, NnrpNativeQuicClient.OpenResult> openConnection,
            Func<ulong, FrameSubmitMessage, ResultPushMessage> submitFrame,
            Func<ulong, byte[], byte[]> submitOutcomeBytes,
            Action<ulong, byte[]> beginSubmitPacket,
            Func<ulong, byte[]> receiveResultPacket,
            Func<ulong, PingMessage, PongMessage> pingRoundTrip,
            Action<ulong, FrameCancelMessage> cancelFrame,
            Action<ulong> closeConnection)
            : this(
                profile,
                options,
                openConnection,
                submitFrame,
                submitOutcomeBytes,
                beginSubmitPacket,
                receiveResultPacket,
                receiveResultPacket,
                pingRoundTrip,
                cancelFrame,
                closeConnection)
        {
        }

        public NnrpQuicClientOptions Options { get; }

        public ClientProfile? Profile { get; }

        public bool IsConnected => Handle != 0;

        public ulong Handle { get; private set; }

        public uint NegotiatedSessionId { get; private set; }

        public byte NegotiatedWireFormat => NnrpHeader.CurrentWireFormat;

        public string ActiveModelName { get; private set; }

        public NnrpNativeQuicClient.OpenResult Connect()
        {
            ThrowIfDisposed();
            if (IsConnected)
            {
                throw new InvalidOperationException("QUIC client is already connected.");
            }

            var result = openConnection(
                Options.Host,
                Options.Port,
                Options.TlsServerName,
                Options.RequestedModel,
                Options.RequestedSessionId);
            if (result.Handle == 0)
            {
                throw new InvalidOperationException("Native QUIC open returned handle 0.");
            }

            Handle = result.Handle;
            NegotiatedSessionId = result.NegotiatedSessionId;
            ActiveModelName = result.ActiveModelName;
            return result;
        }

        public ResultPushMessage Submit(FrameSubmitMessage submitMessage)
        {
            ThrowIfDisposed();
            EnsureConnected();

            if (submitMessage.Header.SessionId != NegotiatedSessionId)
            {
                throw new InvalidOperationException(
                    $"FRAME_SUBMIT session_id {submitMessage.Header.SessionId} does not match negotiated session_id {NegotiatedSessionId}.");
            }

            var result = submitFrame(Handle, submitMessage);
            if (TryGetResultPushCorrelationFailure(submitMessage.Header, result.Header, out var correlationFailure))
            {
                throw new InvalidOperationException(correlationFailure);
            }

            return result;
        }

        public byte[] SubmitPacket(byte[] packet)
        {
            ThrowIfDisposed();
            EnsureConnected();

            if (packet == null || packet.Length == 0)
            {
                throw new ArgumentException("Packet must not be empty.", nameof(packet));
            }

            return submitOutcomeBytes(Handle, packet);
        }

        public void SendSubmitPacket(byte[] packet)
        {
            ThrowIfDisposed();
            EnsureConnected();

            if (packet == null || packet.Length == 0)
            {
                throw new ArgumentException("Packet must not be empty.", nameof(packet));
            }

            beginSubmitPacket(Handle, packet);
        }

        public byte[] ReceiveResultPacket()
        {
            ThrowIfDisposed();
            EnsureConnected();
            return receiveResultPacket(Handle);
        }

        public byte[] ReceiveSessionPacket()
        {
            ThrowIfDisposed();
            EnsureConnected();
            return receiveSessionPacket(Handle);
        }

        /// <summary>
        /// Submits a frame and returns the typed outcome, which is either a
        /// <see cref="ResultPushMessage"/> with enhanced tile data or a
        /// <see cref="ResultDropMessage"/> when the server deems the result stale or
        /// superseded.  Callers should inspect <see cref="SubmitOutcome.IsResultDrop"/>
        /// and fall back to local rendering when drop is signalled.
        /// </summary>
        public SubmitOutcome SubmitWithOutcome(FrameSubmitMessage submitMessage)
        {
            ThrowIfDisposed();
            EnsureConnected();

            if (submitMessage.Header.SessionId != NegotiatedSessionId)
            {
                throw new InvalidOperationException(
                    $"FRAME_SUBMIT session_id {submitMessage.Header.SessionId} does not match negotiated session_id {NegotiatedSessionId}.");
            }

            var rawBytes = submitOutcomeBytes(Handle, submitMessage.ToArray());
            if (!SubmitOutcome.TryParse(rawBytes, out var outcome, out var error))
            {
                throw new InvalidOperationException($"Failed to parse result outcome packet: {error}.");
            }

            if (outcome.IsResultDrop)
            {
                if (TryGetResultDropCorrelationFailure(submitMessage.Header, outcome.ResultDrop.Header, out var correlationFailure))
                {
                    throw new InvalidOperationException(correlationFailure);
                }
            }
            else if (TryGetResultPushCorrelationFailure(submitMessage.Header, outcome.ResultPush.Header, out var correlationFailure))
            {
                throw new InvalidOperationException(correlationFailure);
            }

            return outcome;
        }

        private bool TryGetResultPushCorrelationFailure(NnrpHeader submitHeader, NnrpHeader resultHeader, out string failure)
        {
            if (resultHeader.SessionId != NegotiatedSessionId)
            {
                failure = $"RESULT_PUSH session_id {resultHeader.SessionId} does not match negotiated session_id {NegotiatedSessionId}.";
                return true;
            }

            if (resultHeader.FrameId != submitHeader.FrameId || resultHeader.ViewId != submitHeader.ViewId)
            {
                failure = $"RESULT_PUSH correlation mismatch: frame_id={resultHeader.FrameId}, view_id={resultHeader.ViewId}; expected frame_id={submitHeader.FrameId}, view_id={submitHeader.ViewId}.";
                return true;
            }

            failure = string.Empty;
            return false;
        }

        private bool TryGetResultDropCorrelationFailure(NnrpHeader submitHeader, NnrpHeader resultHeader, out string failure)
        {
            if (resultHeader.SessionId != NegotiatedSessionId)
            {
                failure = $"RESULT_DROP session_id {resultHeader.SessionId} does not match negotiated session_id {NegotiatedSessionId}.";
                return true;
            }

            if (resultHeader.FrameId != submitHeader.FrameId || resultHeader.ViewId != submitHeader.ViewId)
            {
                failure = $"RESULT_DROP correlation mismatch: frame_id={resultHeader.FrameId}, view_id={resultHeader.ViewId}; expected frame_id={submitHeader.FrameId}, view_id={submitHeader.ViewId}.";
                return true;
            }

            failure = string.Empty;
            return false;
        }

        public PongMessage Ping(ulong traceId = 0)
        {
            ThrowIfDisposed();
            EnsureConnected();

            var ping = PingMessage.Create(NegotiatedSessionId, traceId);
            var pong = pingRoundTrip(Handle, ping);
            if (pong.Header.SessionId != ping.Header.SessionId || pong.Header.TraceId != ping.Header.TraceId)
            {
                throw new InvalidOperationException(
                    $"PONG correlation mismatch: session_id={pong.Header.SessionId}, trace_id={pong.Header.TraceId}.");
            }

            return pong;
        }

        public byte[] PingPacket(byte[] pingPacket)
        {
            ThrowIfDisposed();
            EnsureConnected();

            if (pingPacket == null || pingPacket.Length == 0)
            {
                throw new ArgumentException("Ping packet must not be empty.", nameof(pingPacket));
            }

            if (!PingMessage.TryParse(pingPacket, out var ping, out var parseError))
            {
                throw new InvalidOperationException($"Failed to parse PING packet ({parseError}).");
            }

            return pingRoundTrip(Handle, ping).ToArray();
        }

        public void Cancel(uint frameId, ushort viewId = 0, ulong traceId = 0)
        {
            ThrowIfDisposed();
            EnsureConnected();

            cancelFrame(Handle, FrameCancelMessage.Create(NegotiatedSessionId, frameId, viewId, traceId));
        }

        public void CancelPacket(byte[] cancelPacket)
        {
            ThrowIfDisposed();
            EnsureConnected();

            if (cancelPacket == null || cancelPacket.Length == 0)
            {
                throw new ArgumentException("Cancel packet must not be empty.", nameof(cancelPacket));
            }

            if (!FrameCancelMessage.TryParse(cancelPacket, out var cancelMessage, out var parseError))
            {
                throw new InvalidOperationException($"Failed to parse FRAME_CANCEL packet ({parseError}).");
            }

            cancelFrame(Handle, cancelMessage);
        }

        public void Close()
        {
            if (!IsConnected)
            {
                return;
            }

            closeConnection(Handle);
            Handle = 0;
            NegotiatedSessionId = 0;
            ActiveModelName = string.Empty;
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            try
            {
                Close();
            }
            finally
            {
                disposed = true;
            }
        }

        private void EnsureConnected()
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("QUIC client is not connected.");
            }
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(NnrpQuicClient));
            }
        }
    }
}
