using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Nnrp.Core;

namespace Nnrp.NativeBridge
{
    public static class NnrpNativeQuicClient
    {
        private const string LibraryName = "nnrp_quic_bridge";

        internal delegate int OpenInvoker(
            string host,
            ushort port,
            string tlsServerName,
            string requestedModel,
            uint requestedSessionId,
            byte requestedWireFormat,
            byte certificateVerificationMode,
            string caCertificatePath,
            out ulong handle,
            out uint negotiatedSessionId,
            out byte negotiatedWireFormat,
            out IntPtr activeModelNamePointer,
            out IntPtr errorPointer);

        internal delegate int SubmitInvoker(
            ulong handle,
            byte[] submitPacket,
            int submitPacketLength,
            out IntPtr resultPacketPointer,
            out int resultPacketLength,
            out double openSubmitStreamMilliseconds,
            out double writeSubmitPacketMilliseconds,
            out double finishSubmitStreamMilliseconds,
            out double acceptResultStreamMilliseconds,
            out double readResultPacketMilliseconds,
            out double readResultHeaderMilliseconds,
            out double readResultPayloadMilliseconds,
            out double quicRttBeforeMilliseconds,
            out double quicRttAfterAcceptMilliseconds,
            out double quicRttAfterReadMilliseconds,
            out ulong quicCwndBeforeBytes,
            out ulong quicCwndAfterAcceptBytes,
            out ulong quicCwndAfterReadBytes,
            out ulong quicSentPacketsDuringAccept,
            out ulong quicLostPacketsDuringAccept,
            out ulong quicCongestionEventsDuringAccept,
            out ulong quicSentPacketsTotal,
            out ulong quicLostPacketsTotal,
            out ulong quicCongestionEventsTotal,
            out IntPtr errorPointer);

        internal delegate int BeginSubmitInvoker(
            ulong handle,
            byte[] submitPacket,
            int submitPacketLength,
            out IntPtr errorPointer);

        internal delegate int ReceiveResultInvoker(
            ulong handle,
            out IntPtr resultPacketPointer,
            out int resultPacketLength,
            out IntPtr errorPointer);

        internal delegate int PingInvoker(
            ulong handle,
            byte[] pingPacket,
            int pingPacketLength,
            out IntPtr pongPacketPointer,
            out int pongPacketLength,
            out IntPtr errorPointer);

        internal delegate int ProbeInvoker(
            string host,
            ushort port,
            string tlsServerName,
            byte[] probePacket,
            int probePacketLength,
            byte requestedWireFormat,
            byte certificateVerificationMode,
            string caCertificatePath,
            out IntPtr responsePacketPointer,
            out int responsePacketLength,
            out IntPtr errorPointer);

        internal delegate int CancelInvoker(
            ulong handle,
            byte[] cancelPacket,
            int cancelPacketLength,
            out IntPtr errorPointer);

        internal delegate int CloseInvoker(
            ulong handle,
            out IntPtr errorPointer);

        public readonly struct OpenResult
        {
            public OpenResult(ulong handle, uint negotiatedSessionId, string activeModelName)
            {
                Handle = handle;
                NegotiatedSessionId = negotiatedSessionId;
                ActiveModelName = activeModelName ?? string.Empty;
            }

            public ulong Handle { get; }

            public uint NegotiatedSessionId { get; }

            public byte NegotiatedWireFormat => NnrpHeader.CurrentWireFormat;

            public string ActiveModelName { get; }
        }

        public readonly struct SubmitPacketResult
        {
            public SubmitPacketResult(
                byte[] resultPacket,
                double nativeCallMilliseconds,
                double marshalCopyMilliseconds,
                double totalMilliseconds,
                double openSubmitStreamMilliseconds,
                double writeSubmitPacketMilliseconds,
                double finishSubmitStreamMilliseconds,
                double acceptResultStreamMilliseconds,
                double readResultPacketMilliseconds,
                double readResultHeaderMilliseconds,
                double readResultPayloadMilliseconds,
                double quicRttBeforeMilliseconds,
                double quicRttAfterAcceptMilliseconds,
                double quicRttAfterReadMilliseconds,
                ulong quicCwndBeforeBytes,
                ulong quicCwndAfterAcceptBytes,
                ulong quicCwndAfterReadBytes,
                ulong quicSentPacketsDuringAccept,
                ulong quicLostPacketsDuringAccept,
                ulong quicCongestionEventsDuringAccept,
                ulong quicSentPacketsTotal,
                ulong quicLostPacketsTotal,
                ulong quicCongestionEventsTotal)
            {
                ResultPacket = resultPacket ?? Array.Empty<byte>();
                NativeCallMilliseconds = nativeCallMilliseconds;
                MarshalCopyMilliseconds = marshalCopyMilliseconds;
                TotalMilliseconds = totalMilliseconds;
                OpenSubmitStreamMilliseconds = openSubmitStreamMilliseconds;
                WriteSubmitPacketMilliseconds = writeSubmitPacketMilliseconds;
                FinishSubmitStreamMilliseconds = finishSubmitStreamMilliseconds;
                AcceptResultStreamMilliseconds = acceptResultStreamMilliseconds;
                ReadResultPacketMilliseconds = readResultPacketMilliseconds;
                ReadResultHeaderMilliseconds = readResultHeaderMilliseconds;
                ReadResultPayloadMilliseconds = readResultPayloadMilliseconds;
                QuicRttBeforeMilliseconds = quicRttBeforeMilliseconds;
                QuicRttAfterAcceptMilliseconds = quicRttAfterAcceptMilliseconds;
                QuicRttAfterReadMilliseconds = quicRttAfterReadMilliseconds;
                QuicCwndBeforeBytes = quicCwndBeforeBytes;
                QuicCwndAfterAcceptBytes = quicCwndAfterAcceptBytes;
                QuicCwndAfterReadBytes = quicCwndAfterReadBytes;
                QuicSentPacketsDuringAccept = quicSentPacketsDuringAccept;
                QuicLostPacketsDuringAccept = quicLostPacketsDuringAccept;
                QuicCongestionEventsDuringAccept = quicCongestionEventsDuringAccept;
                QuicSentPacketsTotal = quicSentPacketsTotal;
                QuicLostPacketsTotal = quicLostPacketsTotal;
                QuicCongestionEventsTotal = quicCongestionEventsTotal;
            }

            public byte[] ResultPacket { get; }

            public double NativeCallMilliseconds { get; }

            public double MarshalCopyMilliseconds { get; }

            public double TotalMilliseconds { get; }

            public double OpenSubmitStreamMilliseconds { get; }

            public double WriteSubmitPacketMilliseconds { get; }

            public double FinishSubmitStreamMilliseconds { get; }

            public double AcceptResultStreamMilliseconds { get; }

            public double ReadResultPacketMilliseconds { get; }

            public double ReadResultHeaderMilliseconds { get; }

            public double ReadResultPayloadMilliseconds { get; }

            public double QuicRttBeforeMilliseconds { get; }

            public double QuicRttAfterAcceptMilliseconds { get; }

            public double QuicRttAfterReadMilliseconds { get; }

            public ulong QuicCwndBeforeBytes { get; }

            public ulong QuicCwndAfterAcceptBytes { get; }

            public ulong QuicCwndAfterReadBytes { get; }

            public ulong QuicSentPacketsDuringAccept { get; }

            public ulong QuicLostPacketsDuringAccept { get; }

            public ulong QuicCongestionEventsDuringAccept { get; }

            public ulong QuicSentPacketsTotal { get; }

            public ulong QuicLostPacketsTotal { get; }

            public ulong QuicCongestionEventsTotal { get; }
        }

        public static OpenResult Open(
            string host,
            ushort port,
            string tlsServerName,
            string requestedModel,
            uint requestedSessionId = 11,
            NnrpQuicCertificateVerificationMode certificateVerificationMode = NnrpQuicCertificateVerificationMode.Secure,
            string? caCertificatePath = null)
        {
            return Open(
                host,
                port,
                tlsServerName,
                requestedModel,
                requestedSessionId,
                certificateVerificationMode,
                caCertificatePath,
                nnrp_quic_client_open_with_tls,
                nnrp_quic_string_free);
        }

        internal static OpenResult Open(
            string host,
            ushort port,
            string tlsServerName,
            string requestedModel,
            uint requestedSessionId,
            OpenInvoker openInvoker,
            Action<IntPtr> freeString)
        {
            return Open(
                host,
                port,
                tlsServerName,
                requestedModel,
                requestedSessionId,
                NnrpQuicCertificateVerificationMode.Secure,
                null,
                openInvoker,
                freeString);
        }

        internal static OpenResult Open(
            string host,
            ushort port,
            string tlsServerName,
            string requestedModel,
            uint requestedSessionId,
            NnrpQuicCertificateVerificationMode certificateVerificationMode,
            string? caCertificatePath,
            OpenInvoker openInvoker,
            Action<IntPtr> freeString)
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

            ValidateCertificateOptions(certificateVerificationMode, caCertificatePath);

            if (openInvoker == null)
            {
                throw new ArgumentNullException(nameof(openInvoker));
            }

            if (freeString == null)
            {
                throw new ArgumentNullException(nameof(freeString));
            }

            IntPtr activeModelNamePointer = IntPtr.Zero;
            IntPtr errorPointer = IntPtr.Zero;
            try
            {
                int exitCode = openInvoker(
                    host,
                    port,
                    tlsServerName,
                    requestedModel,
                    requestedSessionId,
                    NnrpHeader.CurrentWireFormat,
                    (byte)certificateVerificationMode,
                    caCertificatePath ?? string.Empty,
                    out ulong handle,
                    out uint negotiatedSessionId,
                    out byte negotiatedWireFormat,
                    out activeModelNamePointer,
                    out errorPointer);
                if (exitCode != 0)
                {
                    string payload = Marshal.PtrToStringAnsi(errorPointer) ?? string.Empty;
                    throw new InvalidOperationException(string.IsNullOrWhiteSpace(payload)
                        ? $"Native QUIC open failed with exit code {exitCode}."
                        : payload);
                }

                string activeModelName = Marshal.PtrToStringAnsi(activeModelNamePointer) ?? string.Empty;
                if (negotiatedWireFormat != NnrpHeader.CurrentWireFormat)
                {
                    throw new InvalidOperationException($"Native QUIC open returned unsupported wire format {negotiatedWireFormat}.");
                }

                return new OpenResult(handle, negotiatedSessionId, activeModelName);
            }
            finally
            {
                if (activeModelNamePointer != IntPtr.Zero)
                {
                    freeString(activeModelNamePointer);
                }

                if (errorPointer != IntPtr.Zero)
                {
                    freeString(errorPointer);
                }
            }
        }

        public static byte[] Submit(ulong handle, byte[] submitPacket)
        {
            return SubmitWithTiming(handle, submitPacket).ResultPacket;
        }

        public static SubmitPacketResult SubmitWithTiming(ulong handle, byte[] submitPacket)
        {
            return SubmitWithTiming(
                handle,
                submitPacket,
                nnrp_quic_client_submit,
                nnrp_quic_buffer_free,
                nnrp_quic_string_free);
        }

        internal static SubmitPacketResult SubmitWithTiming(
            ulong handle,
            byte[] submitPacket,
            SubmitInvoker submitInvoker,
            Action<IntPtr, int> freeBuffer,
            Action<IntPtr> freeString)
        {
            if (handle == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(handle), "Handle must not be zero.");
            }

            if (submitPacket == null || submitPacket.Length == 0)
            {
                throw new ArgumentException("Submit packet must not be empty.", nameof(submitPacket));
            }

            if (submitInvoker == null)
            {
                throw new ArgumentNullException(nameof(submitInvoker));
            }

            if (freeBuffer == null)
            {
                throw new ArgumentNullException(nameof(freeBuffer));
            }

            if (freeString == null)
            {
                throw new ArgumentNullException(nameof(freeString));
            }

            IntPtr resultPointer = IntPtr.Zero;
            IntPtr errorPointer = IntPtr.Zero;
            int resultLength = 0;
            double openSubmitStreamMilliseconds = 0.0;
            double writeSubmitPacketMilliseconds = 0.0;
            double finishSubmitStreamMilliseconds = 0.0;
            double acceptResultStreamMilliseconds = 0.0;
            double readResultPacketMilliseconds = 0.0;
            double readResultHeaderMilliseconds = 0.0;
            double readResultPayloadMilliseconds = 0.0;
            double quicRttBeforeMilliseconds = 0.0;
            double quicRttAfterAcceptMilliseconds = 0.0;
            double quicRttAfterReadMilliseconds = 0.0;
            ulong quicCwndBeforeBytes = 0;
            ulong quicCwndAfterAcceptBytes = 0;
            ulong quicCwndAfterReadBytes = 0;
            ulong quicSentPacketsDuringAccept = 0;
            ulong quicLostPacketsDuringAccept = 0;
            ulong quicCongestionEventsDuringAccept = 0;
            ulong quicSentPacketsTotal = 0;
            ulong quicLostPacketsTotal = 0;
            ulong quicCongestionEventsTotal = 0;
            Stopwatch totalStopwatch = Stopwatch.StartNew();
            Stopwatch nativeCallStopwatch = Stopwatch.StartNew();
            try
            {
                int exitCode = submitInvoker(
                    handle,
                    submitPacket,
                    submitPacket.Length,
                    out resultPointer,
                    out resultLength,
                    out openSubmitStreamMilliseconds,
                    out writeSubmitPacketMilliseconds,
                    out finishSubmitStreamMilliseconds,
                    out acceptResultStreamMilliseconds,
                    out readResultPacketMilliseconds,
                    out readResultHeaderMilliseconds,
                    out readResultPayloadMilliseconds,
                    out quicRttBeforeMilliseconds,
                    out quicRttAfterAcceptMilliseconds,
                    out quicRttAfterReadMilliseconds,
                    out quicCwndBeforeBytes,
                    out quicCwndAfterAcceptBytes,
                    out quicCwndAfterReadBytes,
                    out quicSentPacketsDuringAccept,
                    out quicLostPacketsDuringAccept,
                    out quicCongestionEventsDuringAccept,
                    out quicSentPacketsTotal,
                    out quicLostPacketsTotal,
                    out quicCongestionEventsTotal,
                    out errorPointer);
                nativeCallStopwatch.Stop();
                if (exitCode != 0)
                {
                    string payload = Marshal.PtrToStringAnsi(errorPointer) ?? string.Empty;
                    throw new InvalidOperationException(string.IsNullOrWhiteSpace(payload)
                        ? $"Native QUIC submit failed with exit code {exitCode}."
                        : payload);
                }

                byte[] result = new byte[resultLength];
                Stopwatch marshalCopyStopwatch = Stopwatch.StartNew();
                Marshal.Copy(resultPointer, result, 0, resultLength);
                marshalCopyStopwatch.Stop();
                totalStopwatch.Stop();
                return new SubmitPacketResult(
                    result,
                    nativeCallStopwatch.Elapsed.TotalMilliseconds,
                    marshalCopyStopwatch.Elapsed.TotalMilliseconds,
                    totalStopwatch.Elapsed.TotalMilliseconds,
                    openSubmitStreamMilliseconds,
                    writeSubmitPacketMilliseconds,
                    finishSubmitStreamMilliseconds,
                    acceptResultStreamMilliseconds,
                    readResultPacketMilliseconds,
                    readResultHeaderMilliseconds,
                    readResultPayloadMilliseconds,
                    quicRttBeforeMilliseconds,
                    quicRttAfterAcceptMilliseconds,
                    quicRttAfterReadMilliseconds,
                    quicCwndBeforeBytes,
                    quicCwndAfterAcceptBytes,
                    quicCwndAfterReadBytes,
                    quicSentPacketsDuringAccept,
                    quicLostPacketsDuringAccept,
                    quicCongestionEventsDuringAccept,
                    quicSentPacketsTotal,
                    quicLostPacketsTotal,
                    quicCongestionEventsTotal);
            }
            finally
            {
                if (resultPointer != IntPtr.Zero)
                {
                    freeBuffer(resultPointer, resultLength);
                }

                if (errorPointer != IntPtr.Zero)
                {
                    freeString(errorPointer);
                }
            }
        }

        public static ResultPushMessage Submit(ulong handle, FrameSubmitMessage submitMessage)
        {
            return Submit(submitMessage, submitPacket => Submit(handle, submitPacket));
        }

        public static void BeginSubmit(ulong handle, byte[] submitPacket)
        {
            BeginSubmit(handle, submitPacket, nnrp_quic_client_begin_submit, nnrp_quic_string_free);
        }

        internal static void BeginSubmit(
            ulong handle,
            byte[] submitPacket,
            BeginSubmitInvoker beginSubmitInvoker,
            Action<IntPtr> freeString)
        {
            if (handle == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(handle), "Handle must not be zero.");
            }

            if (submitPacket == null || submitPacket.Length == 0)
            {
                throw new ArgumentException("Submit packet must not be empty.", nameof(submitPacket));
            }

            if (beginSubmitInvoker == null)
            {
                throw new ArgumentNullException(nameof(beginSubmitInvoker));
            }

            if (freeString == null)
            {
                throw new ArgumentNullException(nameof(freeString));
            }

            IntPtr errorPointer = IntPtr.Zero;
            try
            {
                int exitCode = beginSubmitInvoker(handle, submitPacket, submitPacket.Length, out errorPointer);
                if (exitCode != 0)
                {
                    string payload = Marshal.PtrToStringAnsi(errorPointer) ?? string.Empty;
                    throw new InvalidOperationException(string.IsNullOrWhiteSpace(payload)
                        ? $"Native QUIC begin-submit failed with exit code {exitCode}."
                        : payload);
                }
            }
            finally
            {
                if (errorPointer != IntPtr.Zero)
                {
                    freeString(errorPointer);
                }
            }
        }

        public static byte[] ReceiveResult(ulong handle)
        {
            return ReceiveResult(handle, nnrp_quic_client_receive_result, nnrp_quic_buffer_free, nnrp_quic_string_free);
        }

        public static byte[] ReceiveSessionPacket(ulong handle)
        {
            return ReceiveResult(handle, nnrp_quic_client_receive_session_packet, nnrp_quic_buffer_free, nnrp_quic_string_free);
        }

        internal static byte[] ReceiveResult(
            ulong handle,
            ReceiveResultInvoker receiveResultInvoker,
            Action<IntPtr, int> freeBuffer,
            Action<IntPtr> freeString)
        {
            if (handle == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(handle), "Handle must not be zero.");
            }

            if (receiveResultInvoker == null)
            {
                throw new ArgumentNullException(nameof(receiveResultInvoker));
            }

            if (freeBuffer == null)
            {
                throw new ArgumentNullException(nameof(freeBuffer));
            }

            if (freeString == null)
            {
                throw new ArgumentNullException(nameof(freeString));
            }

            IntPtr resultPointer = IntPtr.Zero;
            IntPtr errorPointer = IntPtr.Zero;
            int resultLength = 0;
            try
            {
                int exitCode = receiveResultInvoker(handle, out resultPointer, out resultLength, out errorPointer);
                if (exitCode != 0)
                {
                    string payload = Marshal.PtrToStringAnsi(errorPointer) ?? string.Empty;
                    throw new InvalidOperationException(string.IsNullOrWhiteSpace(payload)
                        ? $"Native QUIC receive-result failed with exit code {exitCode}."
                        : payload);
                }

                byte[] result = new byte[resultLength];
                Marshal.Copy(resultPointer, result, 0, resultLength);
                return result;
            }
            finally
            {
                if (resultPointer != IntPtr.Zero)
                {
                    freeBuffer(resultPointer, resultLength);
                }

                if (errorPointer != IntPtr.Zero)
                {
                    freeString(errorPointer);
                }
            }
        }

        public static ResultPushMessage Submit(FrameSubmitMessage submitMessage, Func<byte[], byte[]> submitPacket)
        {
            if (submitPacket == null)
            {
                throw new ArgumentNullException(nameof(submitPacket));
            }

            var resultPacket = submitPacket(submitMessage.ToArray());
            if (resultPacket == null || resultPacket.Length == 0)
            {
                throw new InvalidOperationException("Native QUIC submit returned an empty result packet.");
            }

            return ParseResultPush(resultPacket);
        }

        public static ResultPushMessage ParseResultPush(byte[] resultPacket)
        {
            if (resultPacket == null || resultPacket.Length == 0)
            {
                throw new ArgumentException("Result packet must not be empty.", nameof(resultPacket));
            }

            if (!ResultPushMessage.TryParse(resultPacket, out var message, out var error))
            {
                throw new InvalidOperationException($"Failed to parse RESULT_PUSH packet: {error}.");
            }

            return message;
        }

        public static ResultDropMessage ParseResultDrop(byte[] resultPacket)
        {
            if (resultPacket == null || resultPacket.Length == 0)
            {
                throw new ArgumentException("Result packet must not be empty.", nameof(resultPacket));
            }

            if (!ResultDropMessage.TryParse(resultPacket, out var message, out var error))
            {
                throw new InvalidOperationException($"Failed to parse RESULT_DROP packet: {error}.");
            }

            return message;
        }

        public static SubmitOutcome ParseResultOutcome(byte[] resultPacket)
        {
            if (resultPacket == null || resultPacket.Length == 0)
            {
                throw new ArgumentException("Result packet must not be empty.", nameof(resultPacket));
            }

            if (!SubmitOutcome.TryParse(resultPacket, out var outcome, out var error))
            {
                throw new InvalidOperationException($"Failed to parse result outcome packet: {error}.");
            }

            return outcome;
        }

        public static byte[] Ping(ulong handle, byte[] pingPacket)
        {
            return Ping(handle, pingPacket, nnrp_quic_client_ping, nnrp_quic_buffer_free, nnrp_quic_string_free);
        }

        public static byte[] Probe(
            string host,
            ushort port,
            string tlsServerName,
            byte[] probePacket,
            NnrpQuicCertificateVerificationMode certificateVerificationMode = NnrpQuicCertificateVerificationMode.Secure,
            string? caCertificatePath = null)
        {
            return Probe(
                host,
                port,
                tlsServerName,
                probePacket,
                certificateVerificationMode,
                caCertificatePath,
                nnrp_quic_client_probe_with_tls,
                nnrp_quic_buffer_free,
                nnrp_quic_string_free);
        }

        internal static byte[] Probe(
            string host,
            ushort port,
            string tlsServerName,
            byte[] probePacket,
            ProbeInvoker probeInvoker,
            Action<IntPtr, int> freeBuffer,
            Action<IntPtr> freeString)
        {
            return Probe(
                host,
                port,
                tlsServerName,
                probePacket,
                NnrpQuicCertificateVerificationMode.Secure,
                null,
                probeInvoker,
                freeBuffer,
                freeString);
        }

        internal static byte[] Probe(
            string host,
            ushort port,
            string tlsServerName,
            byte[] probePacket,
            NnrpQuicCertificateVerificationMode certificateVerificationMode,
            string? caCertificatePath,
            ProbeInvoker probeInvoker,
            Action<IntPtr, int> freeBuffer,
            Action<IntPtr> freeString)
        {
            if (string.IsNullOrWhiteSpace(host))
            {
                throw new ArgumentException("Host must not be empty.", nameof(host));
            }

            if (string.IsNullOrWhiteSpace(tlsServerName))
            {
                throw new ArgumentException("TLS server name must not be empty.", nameof(tlsServerName));
            }

            if (probePacket == null || probePacket.Length == 0)
            {
                throw new ArgumentException("Probe packet must not be empty.", nameof(probePacket));
            }

            ValidateCertificateOptions(certificateVerificationMode, caCertificatePath);

            if (probeInvoker == null)
            {
                throw new ArgumentNullException(nameof(probeInvoker));
            }

            if (freeBuffer == null)
            {
                throw new ArgumentNullException(nameof(freeBuffer));
            }

            if (freeString == null)
            {
                throw new ArgumentNullException(nameof(freeString));
            }

            IntPtr responsePacketPointer = IntPtr.Zero;
            IntPtr errorPointer = IntPtr.Zero;
            int responsePacketLength = 0;
            try
            {
                int exitCode = probeInvoker(
                    host,
                    port,
                    tlsServerName,
                    probePacket,
                    probePacket.Length,
                    NnrpHeader.CurrentWireFormat,
                    (byte)certificateVerificationMode,
                    caCertificatePath ?? string.Empty,
                    out responsePacketPointer,
                    out responsePacketLength,
                    out errorPointer);
                if (exitCode != 0)
                {
                    string payload = Marshal.PtrToStringAnsi(errorPointer) ?? string.Empty;
                    throw new InvalidOperationException(string.IsNullOrWhiteSpace(payload)
                        ? $"Native QUIC probe failed with exit code {exitCode}."
                        : payload);
                }

                if (responsePacketLength <= 0 || responsePacketPointer == IntPtr.Zero)
                {
                    throw new InvalidOperationException("Native QUIC probe returned an empty response packet.");
                }

                var responsePacket = new byte[responsePacketLength];
                Marshal.Copy(responsePacketPointer, responsePacket, 0, responsePacket.Length);
                return responsePacket;
            }
            finally
            {
                if (responsePacketPointer != IntPtr.Zero)
                {
                    freeBuffer(responsePacketPointer, responsePacketLength);
                }

                if (errorPointer != IntPtr.Zero)
                {
                    freeString(errorPointer);
                }
            }
        }

        internal static byte[] Ping(
            ulong handle,
            byte[] pingPacket,
            PingInvoker pingInvoker,
            Action<IntPtr, int> freeBuffer,
            Action<IntPtr> freeString)
        {
            if (handle == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(handle), "Handle must not be zero.");
            }

            if (pingPacket == null || pingPacket.Length == 0)
            {
                throw new ArgumentException("Ping packet must not be empty.", nameof(pingPacket));
            }

            if (pingInvoker == null)
            {
                throw new ArgumentNullException(nameof(pingInvoker));
            }

            if (freeBuffer == null)
            {
                throw new ArgumentNullException(nameof(freeBuffer));
            }

            if (freeString == null)
            {
                throw new ArgumentNullException(nameof(freeString));
            }

            IntPtr pongPointer = IntPtr.Zero;
            IntPtr errorPointer = IntPtr.Zero;
            int pongLength = 0;
            try
            {
                int exitCode = pingInvoker(
                    handle,
                    pingPacket,
                    pingPacket.Length,
                    out pongPointer,
                    out pongLength,
                    out errorPointer);
                if (exitCode != 0)
                {
                    string payload = Marshal.PtrToStringAnsi(errorPointer) ?? string.Empty;
                    throw new InvalidOperationException(string.IsNullOrWhiteSpace(payload)
                        ? $"Native QUIC ping failed with exit code {exitCode}."
                        : payload);
                }

                byte[] pong = new byte[pongLength];
                Marshal.Copy(pongPointer, pong, 0, pongLength);
                return pong;
            }
            finally
            {
                if (pongPointer != IntPtr.Zero)
                {
                    freeBuffer(pongPointer, pongLength);
                }

                if (errorPointer != IntPtr.Zero)
                {
                    freeString(errorPointer);
                }
            }
        }

        public static PongMessage Ping(ulong handle, PingMessage pingMessage)
        {
            return Ping(pingMessage, pingPacket => Ping(handle, pingPacket));
        }

        public static PongMessage Ping(PingMessage pingMessage, Func<byte[], byte[]> pingPacket)
        {
            if (pingPacket == null)
            {
                throw new ArgumentNullException(nameof(pingPacket));
            }

            var pongPacket = pingPacket(pingMessage.ToArray());
            if (pongPacket == null || pongPacket.Length == 0)
            {
                throw new InvalidOperationException("Native QUIC ping returned an empty pong packet.");
            }

            return ParsePong(pongPacket);
        }

        public static PongMessage ParsePong(byte[] pongPacket)
        {
            if (pongPacket == null || pongPacket.Length == 0)
            {
                throw new ArgumentException("Pong packet must not be empty.", nameof(pongPacket));
            }

            if (!PongMessage.TryParse(pongPacket, out var message, out var error))
            {
                throw new InvalidOperationException($"Failed to parse PONG packet: {error}.");
            }

            return message;
        }

        public static void Cancel(ulong handle, byte[] cancelPacket)
        {
            Cancel(handle, cancelPacket, nnrp_quic_client_cancel, nnrp_quic_string_free);
        }

        internal static void Cancel(
            ulong handle,
            byte[] cancelPacket,
            CancelInvoker cancelInvoker,
            Action<IntPtr> freeString)
        {
            if (handle == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(handle), "Handle must not be zero.");
            }

            if (cancelPacket == null || cancelPacket.Length == 0)
            {
                throw new ArgumentException("Cancel packet must not be empty.", nameof(cancelPacket));
            }

            if (cancelInvoker == null)
            {
                throw new ArgumentNullException(nameof(cancelInvoker));
            }

            if (freeString == null)
            {
                throw new ArgumentNullException(nameof(freeString));
            }

            IntPtr errorPointer = IntPtr.Zero;
            try
            {
                int exitCode = cancelInvoker(
                    handle,
                    cancelPacket,
                    cancelPacket.Length,
                    out errorPointer);
                if (exitCode != 0)
                {
                    string payload = Marshal.PtrToStringAnsi(errorPointer) ?? string.Empty;
                    throw new InvalidOperationException(string.IsNullOrWhiteSpace(payload)
                        ? $"Native QUIC cancel failed with exit code {exitCode}."
                        : payload);
                }
            }
            finally
            {
                if (errorPointer != IntPtr.Zero)
                {
                    freeString(errorPointer);
                }
            }
        }

        public static void Cancel(ulong handle, FrameCancelMessage cancelMessage)
        {
            Cancel(handle, cancelMessage.ToArray());
        }

        public static void Cancel(FrameCancelMessage cancelMessage, Action<byte[]> sendCancelPacket)
        {
            if (sendCancelPacket == null)
            {
                throw new ArgumentNullException(nameof(sendCancelPacket));
            }

            sendCancelPacket(cancelMessage.ToArray());
        }

        public static void Close(ulong handle)
        {
            if (handle == 0)
            {
                return;
            }

            Close(handle, nnrp_quic_client_close, nnrp_quic_string_free);
        }

        internal static void Close(
            ulong handle,
            CloseInvoker closeInvoker,
            Action<IntPtr> freeString)
        {
            if (handle == 0)
            {
                return;
            }

            if (closeInvoker == null)
            {
                throw new ArgumentNullException(nameof(closeInvoker));
            }

            if (freeString == null)
            {
                throw new ArgumentNullException(nameof(freeString));
            }

            IntPtr errorPointer = IntPtr.Zero;
            try
            {
                int exitCode = closeInvoker(handle, out errorPointer);
                if (exitCode != 0)
                {
                    string payload = Marshal.PtrToStringAnsi(errorPointer) ?? string.Empty;
                    throw new InvalidOperationException(string.IsNullOrWhiteSpace(payload)
                        ? $"Native QUIC close failed with exit code {exitCode}."
                        : payload);
                }
            }
            finally
            {
                if (errorPointer != IntPtr.Zero)
                {
                    freeString(errorPointer);
                }
            }
        }

        private static void ValidateCertificateOptions(
            NnrpQuicCertificateVerificationMode certificateVerificationMode,
            string? caCertificatePath)
        {
            if (!Enum.IsDefined(typeof(NnrpQuicCertificateVerificationMode), certificateVerificationMode))
            {
                throw new ArgumentOutOfRangeException(nameof(certificateVerificationMode));
            }

            if (caCertificatePath != null && string.IsNullOrWhiteSpace(caCertificatePath))
            {
                throw new ArgumentException("CA certificate path must not be empty when provided.", nameof(caCertificatePath));
            }
        }

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern int nnrp_quic_client_open_with_tls(
            string host,
            ushort port,
            string tlsServerName,
            string requestedModel,
            uint requestedSessionId,
            byte requestedWireFormat,
            byte certificateVerificationMode,
            string caCertificatePath,
            out ulong handle,
            out uint negotiatedSessionId,
            out byte negotiatedWireFormat,
            out IntPtr activeModelNamePointer,
            out IntPtr errorPointer);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int nnrp_quic_client_submit(
            ulong handle,
            byte[] submitPacket,
            int submitPacketLength,
            out IntPtr resultPacketPointer,
            out int resultPacketLength,
            out double openSubmitStreamMilliseconds,
            out double writeSubmitPacketMilliseconds,
            out double finishSubmitStreamMilliseconds,
            out double acceptResultStreamMilliseconds,
            out double readResultPacketMilliseconds,
            out double readResultHeaderMilliseconds,
            out double readResultPayloadMilliseconds,
            out double quicRttBeforeMilliseconds,
            out double quicRttAfterAcceptMilliseconds,
            out double quicRttAfterReadMilliseconds,
            out ulong quicCwndBeforeBytes,
            out ulong quicCwndAfterAcceptBytes,
            out ulong quicCwndAfterReadBytes,
            out ulong quicSentPacketsDuringAccept,
            out ulong quicLostPacketsDuringAccept,
            out ulong quicCongestionEventsDuringAccept,
            out ulong quicSentPacketsTotal,
            out ulong quicLostPacketsTotal,
            out ulong quicCongestionEventsTotal,
            out IntPtr errorPointer);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int nnrp_quic_client_begin_submit(
            ulong handle,
            byte[] submitPacket,
            int submitPacketLength,
            out IntPtr errorPointer);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int nnrp_quic_client_receive_result(
            ulong handle,
            out IntPtr resultPacketPointer,
            out int resultPacketLength,
            out IntPtr errorPointer);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int nnrp_quic_client_receive_session_packet(
            ulong handle,
            out IntPtr resultPacketPointer,
            out int resultPacketLength,
            out IntPtr errorPointer);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int nnrp_quic_client_close(
            ulong handle,
            out IntPtr errorPointer);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int nnrp_quic_client_ping(
            ulong handle,
            byte[] pingPacket,
            int pingPacketLength,
            out IntPtr pongPacketPointer,
            out int pongPacketLength,
            out IntPtr errorPointer);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern int nnrp_quic_client_probe_with_tls(
            string host,
            ushort port,
            string tlsServerName,
            byte[] probePacket,
            int probePacketLength,
            byte requestedWireFormat,
            byte certificateVerificationMode,
            string caCertificatePath,
            out IntPtr responsePacketPointer,
            out int responsePacketLength,
            out IntPtr errorPointer);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int nnrp_quic_client_cancel(
            ulong handle,
            byte[] cancelPacket,
            int cancelPacketLength,
            out IntPtr errorPointer);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void nnrp_quic_string_free(IntPtr value);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void nnrp_quic_buffer_free(IntPtr value, int length);
    }
}
