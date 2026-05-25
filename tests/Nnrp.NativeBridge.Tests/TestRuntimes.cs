using System;
using Nnrp.Core;
using Nnrp.NativeBridge;

namespace Nnrp.NativeBridge.Tests
{
    internal sealed class TestQuicRuntime : INnrpQuicRuntime
    {
        private readonly Func<string, ushort, string, string, uint, NnrpQuicCertificateVerificationMode, string?, NnrpNativeQuicClient.OpenResult> openConnection;
        private readonly Func<ulong, FrameSubmitMessage, ResultPushMessage> submitFrame;
        private readonly Func<ulong, byte[], byte[]> submitPacket;
        private readonly Action<ulong, byte[]> beginSubmitPacket;
        private readonly Func<ulong, byte[]> receiveResultPacket;
        private readonly Func<ulong, byte[]> receiveSessionPacket;
        private readonly Func<ulong, PingMessage, PongMessage> pingRoundTrip;
        private readonly Action<ulong, FrameCancelMessage> cancelFrame;
        private readonly Action<ulong> closeConnection;

        public TestQuicRuntime(
            Func<string, ushort, string, string, uint, NnrpNativeQuicClient.OpenResult>? openConnection = null,
            Func<string, ushort, string, string, uint, NnrpQuicCertificateVerificationMode, string?, NnrpNativeQuicClient.OpenResult>? openConnectionWithCertificateOptions = null,
            Func<ulong, FrameSubmitMessage, ResultPushMessage>? submitFrame = null,
            Func<ulong, byte[], byte[]>? submitPacket = null,
            Action<ulong, byte[]>? beginSubmitPacket = null,
            Func<ulong, byte[]>? receiveResultPacket = null,
            Func<ulong, byte[]>? receiveSessionPacket = null,
            Func<ulong, PingMessage, PongMessage>? pingRoundTrip = null,
            Action<ulong, FrameCancelMessage>? cancelFrame = null,
            Action<ulong>? closeConnection = null)
        {
            var fallbackOpenConnection = openConnection ?? ((_, _, _, _, _) => throw new NotSupportedException("Open was not configured for this test runtime."));
            this.openConnection = openConnectionWithCertificateOptions ?? ((host, port, tlsServerName, requestedModel, requestedSessionId, _, _) => fallbackOpenConnection(host, port, tlsServerName, requestedModel, requestedSessionId));
            this.submitFrame = submitFrame ?? ((_, _) => throw new NotSupportedException("Submit(FrameSubmitMessage) was not configured for this test runtime."));
            this.submitPacket = submitPacket ?? ((_, _) => throw new NotSupportedException("Submit(byte[]) was not configured for this test runtime."));
            this.beginSubmitPacket = beginSubmitPacket ?? ((_, _) => throw new NotSupportedException("BeginSubmitPacket was not configured for this test runtime."));
            this.receiveResultPacket = receiveResultPacket ?? (_ => throw new NotSupportedException("ReceiveResultPacket was not configured for this test runtime."));
            this.receiveSessionPacket = receiveSessionPacket ?? this.receiveResultPacket;
            this.pingRoundTrip = pingRoundTrip ?? ((_, _) => throw new NotSupportedException("Ping was not configured for this test runtime."));
            this.cancelFrame = cancelFrame ?? ((_, _) => throw new NotSupportedException("Cancel was not configured for this test runtime."));
            this.closeConnection = closeConnection ?? (_ => { });
        }

        public NnrpNativeQuicClient.OpenResult Open(
            string host,
            ushort port,
            string tlsServerName,
            string requestedModel,
            uint requestedSessionId,
            NnrpQuicCertificateVerificationMode certificateVerificationMode,
            string? caCertificatePath)
        {
            return openConnection(
                host,
                port,
                tlsServerName,
                requestedModel,
                requestedSessionId,
                certificateVerificationMode,
                caCertificatePath);
        }

        public ResultPushMessage Submit(ulong handle, FrameSubmitMessage submitMessage)
        {
            return submitFrame(handle, submitMessage);
        }

        public byte[] SubmitPacket(ulong handle, byte[] packet)
        {
            return submitPacket(handle, packet);
        }

        public void BeginSubmitPacket(ulong handle, byte[] packet)
        {
            beginSubmitPacket(handle, packet);
        }

        public byte[] ReceiveResultPacket(ulong handle)
        {
            return receiveResultPacket(handle);
        }

        public byte[] ReceiveSessionPacket(ulong handle)
        {
            return receiveSessionPacket(handle);
        }

        public PongMessage Ping(ulong handle, PingMessage ping)
        {
            return pingRoundTrip(handle, ping);
        }

        public void Cancel(ulong handle, FrameCancelMessage cancelMessage)
        {
            cancelFrame(handle, cancelMessage);
        }

        public void Close(ulong handle)
        {
            closeConnection(handle);
        }
    }

    internal sealed class TestAutoTransportRuntime : INnrpAutoTransportRuntime
    {
        private readonly Func<NnrpQuicClientOptions, NnrpQuicClient> quicClientFactory;
        private readonly Func<string, ushort, string, byte[], NnrpQuicCertificateVerificationMode, string?, byte[]> quicProbe;

        public TestAutoTransportRuntime(
            Func<NnrpQuicClientOptions, NnrpQuicClient>? quicClientFactory = null,
            Func<string, ushort, string, byte[], byte[]>? quicProbe = null,
            Func<string, ushort, string, byte[], NnrpQuicCertificateVerificationMode, string?, byte[]>? quicProbeWithCertificateOptions = null)
        {
            this.quicClientFactory = quicClientFactory ?? (_ => throw new NotSupportedException("CreateQuicClient was not configured for this test runtime."));
            var fallbackQuicProbe = quicProbe ?? ((_, _, _, _) => throw new NotSupportedException("ProbeQuic was not configured for this test runtime."));
            this.quicProbe = quicProbeWithCertificateOptions ?? ((host, port, tlsServerName, probePacket, _, _) => fallbackQuicProbe(host, port, tlsServerName, probePacket));
        }

        public NnrpQuicClient CreateQuicClient(NnrpQuicClientOptions options)
        {
            return quicClientFactory(options);
        }

        public byte[] ProbeQuic(
            string host,
            ushort port,
            string tlsServerName,
            byte[] probePacket,
            NnrpQuicCertificateVerificationMode certificateVerificationMode,
            string? caCertificatePath)
        {
            return quicProbe(host, port, tlsServerName, probePacket, certificateVerificationMode, caCertificatePath);
        }
    }
}
