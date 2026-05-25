using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Nnrp.Client;
using Nnrp.Core;
using Nnrp.Transport.Tcp;
using Xunit;

namespace Nnrp.NativeBridge.Tests
{
    public sealed class NnrpAutoTransportClientTests
    {
        [Fact]
        public void OptionsAndDefaultRuntimeCoverCurrentWireFormatAndWrapperValidation()
        {
            var options = new NnrpAutoTransportClientOptions("127.0.0.1", 50072, "localhost", "engine-sr");
            var runtime = NnrpAutoTransportRuntime.Instance;

            Assert.Equal(NnrpHeader.CurrentWireFormat, options.RequestedWireFormat);
            Assert.Equal(NnrpQuicCertificateVerificationMode.Secure, options.CertificateVerificationMode);
            Assert.Null(options.CaCertificatePath);

            var client = runtime.CreateQuicClient(new NnrpQuicClientOptions("127.0.0.1", 50072, "localhost", "engine-sr"));
            Assert.Equal("127.0.0.1", client.Options.Host);

            Assert.Throws<ArgumentException>(() => runtime.ProbeQuic("", 50072, "localhost", new byte[] { 0x01 }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NnrpAutoTransportClientOptions("127.0.0.1", 50072, "localhost", "engine-sr", certificateVerificationMode: (NnrpQuicCertificateVerificationMode)99));
            Assert.Throws<ArgumentException>(() => new NnrpAutoTransportClientOptions("127.0.0.1", 50072, "localhost", "engine-sr", caCertificatePath: " "));
        }

        [Fact]
        public void OptionsAcceptCertificateVerificationPolicy()
        {
            var options = new NnrpAutoTransportClientOptions(
                "127.0.0.1",
                50072,
                "localhost",
                "engine-sr",
                certificateVerificationMode: NnrpQuicCertificateVerificationMode.InsecureSkipVerify,
                caCertificatePath: "certs/test-ca.pem");

            Assert.Equal(NnrpQuicCertificateVerificationMode.InsecureSkipVerify, options.CertificateVerificationMode);
            Assert.Equal("certs/test-ca.pem", options.CaCertificatePath);
        }

        [Fact]
        public async Task ConnectAsyncRunsForcedQuicOpenOffCallerThread()
        {
            var callingThread = Thread.CurrentThread.ManagedThreadId;
            var openThread = callingThread;
            ushort observedPort = 0;
            NnrpQuicCertificateVerificationMode observedCertificateVerificationMode = 0;
            string? observedCaCertificatePath = null;
            using var signal = new ManualResetEventSlim(false);

            var client = new NnrpAutoTransportClient(
                new ClientProfile { TransportPolicy = TransportPolicy.ForceQuic },
                new NnrpAutoTransportClientOptions(
                    "127.0.0.1",
                    50072,
                    "localhost",
                    "engine-sr",
                    tcpPort: 50051,
                    certificateVerificationMode: NnrpQuicCertificateVerificationMode.InsecureSkipVerify,
                    caCertificatePath: "certs/test-ca.pem"),
                new TestAutoTransportRuntime(
                    quicClientFactory: options =>
                    {
                        observedCertificateVerificationMode = options.CertificateVerificationMode;
                        observedCaCertificatePath = options.CaCertificatePath;
                        return new NnrpQuicClient(
                            options,
                            new TestQuicRuntime(
                                openConnection: (host, port, tlsServerName, requestedModel, requestedSessionId) =>
                                {
                                    openThread = Thread.CurrentThread.ManagedThreadId;
                                    observedPort = port;
                                    signal.Set();
                                    return new NnrpNativeQuicClient.OpenResult(9, 77, "engine-sr");
                                },
                                submitFrame: (_, message) => throw new NotSupportedException(),
                                submitPacket: (_, packet) => throw new NotSupportedException(),
                                pingRoundTrip: (_, ping) => throw new NotSupportedException(),
                                cancelFrame: (_, cancel) => { },
                                closeConnection: _ => { }));
                    }));

            var connectTask = client.ConnectAsync(
                new NnrpTransportProbeOptions(),
                cancellationToken: CancellationToken.None).AsTask();

            Assert.True(signal.Wait(TimeSpan.FromSeconds(2)));

            var result = await connectTask;

            Assert.Equal(TransportId.Quic, result.SelectedTransportId);
            Assert.Equal<ushort>(50072, observedPort);
            Assert.Equal(NnrpQuicCertificateVerificationMode.InsecureSkipVerify, observedCertificateVerificationMode);
            Assert.Equal("certs/test-ca.pem", observedCaCertificatePath);
            Assert.NotEqual(callingThread, openThread);
        }

        [Fact]
        public async Task ConnectAsyncUsesDedicatedTcpPortWhenForced()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();

            try
            {
                var endpoint = (IPEndPoint)listener.LocalEndpoint;
                var serverTask = RunForcedTcpServerAsync(listener, timeout.Token);

                await using var client = new NnrpAutoTransportClient(
                    new ClientProfile { TransportPolicy = TransportPolicy.ForceTcp },
                    new NnrpAutoTransportClientOptions(
                        IPAddress.Loopback.ToString(),
                        port: 65000,
                        tlsServerName: "localhost",
                        requestedModel: "engine-sr",
                        requestedSessionId: 41,
                        tcpPort: (ushort)endpoint.Port));

                var result = await client.ConnectAsync(new NnrpTransportProbeOptions(), cancellationToken: timeout.Token);
                await client.CloseAsync("forced-tcp done", cancellationToken: timeout.Token);

                var serverObservation = await serverTask;

                Assert.Equal(TransportId.Tcp, result.SelectedTransportId);
                Assert.Equal(41u, result.NegotiatedSessionId);
                Assert.Equal(TransportId.Tcp, serverObservation.PreferredTransportId);
                Assert.Equal("forced-tcp done", serverObservation.CloseReason);
            }
            finally
            {
                listener.Stop();
            }
        }

        [Fact]
        public async Task ForcedTcpClientCanReceiveFlowUpdateAndResultHintHelpers()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();

            try
            {
                var endpoint = (IPEndPoint)listener.LocalEndpoint;
                var serverTask = RunForcedTcpFlowControlServerAsync(listener, timeout.Token);

                await using var client = new NnrpAutoTransportClient(
                    new ClientProfile { TransportPolicy = TransportPolicy.ForceTcp },
                    new NnrpAutoTransportClientOptions(
                        IPAddress.Loopback.ToString(),
                        port: 65000,
                        tlsServerName: "localhost",
                        requestedModel: "engine-sr",
                        requestedSessionId: 41,
                        tcpPort: (ushort)endpoint.Port));

                var connectResult = await client.ConnectAsync(new NnrpTransportProbeOptions(), cancellationToken: timeout.Token);
                var flowUpdate = await client.ReceiveFlowUpdateAsync(timeout.Token);
                var resultHint = await client.ReceiveResultHintAsync(timeout.Token);
                await client.CloseAsync("forced-tcp-flow-control done", cancellationToken: timeout.Token);

                var serverObservation = await serverTask;

                Assert.Equal(TransportId.Tcp, connectResult.SelectedTransportId);
                Assert.Equal(FlowUpdateScopeKind.Session, flowUpdate.Metadata.ScopeKind);
                Assert.Equal(FlowUpdateReason.Congestion, flowUpdate.Metadata.UpdateReason);
                Assert.Equal(4u, flowUpdate.Metadata.RetryAfterMilliseconds);
                Assert.Equal(ResultHintBudgetPolicy.Partial, resultHint.Metadata.AppliedBudgetPolicy);
                Assert.Equal(ResultHintReason.ServerBusy, resultHint.Metadata.Reason);
                Assert.Equal("forced-tcp-flow-control done", serverObservation.CloseReason);
            }
            finally
            {
                listener.Stop();
            }
        }

        [Fact]
        public async Task ForcedTcpClientSupportsExplicitSubmitRequests()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();

            try
            {
                var endpoint = (IPEndPoint)listener.LocalEndpoint;
                var serverTask = RunForcedTcpExplicitSubmitServerAsync(listener, timeout.Token);

                await using var client = new NnrpAutoTransportClient(
                    new ClientProfile { TransportPolicy = TransportPolicy.ForceTcp },
                    new NnrpAutoTransportClientOptions(
                        IPAddress.Loopback.ToString(),
                        port: 65000,
                        tlsServerName: "localhost",
                        requestedModel: "engine-sr",
                        requestedSessionId: 41,
                        tcpPort: (ushort)endpoint.Port));

                var connectResult = await client.ConnectAsync(new NnrpTransportProbeOptions(), cancellationToken: timeout.Token);
                var result0 = await client.SubmitAsync(CreateSubmitRequest(frameId: 303), timeout.Token);
                var result1 = await client.SubmitAsync(CreateSubmitRequest(frameId: 304), timeout.Token);
                await client.CloseAsync("forced-tcp-session-pump done", cancellationToken: timeout.Token);

                var serverObservation = await serverTask;

                Assert.Equal(TransportId.Tcp, connectResult.SelectedTransportId);
                Assert.Equal(41u, result0.SessionId);
                Assert.Equal(303u, result0.FrameId);
                Assert.Equal(304u, result1.FrameId);
                Assert.Equal(new uint[] { 303, 304 }, serverObservation.SubmittedFrameIds);
                Assert.Equal("forced-tcp-session-pump done", serverObservation.CloseReason);
            }
            finally
            {
                listener.Stop();
            }
        }

        [Fact]
        public async Task ForcedQuicClientRejectsFlowUpdateAndResultHintHelpers()
        {
            await using var client = new NnrpAutoTransportClient(
                new ClientProfile { TransportPolicy = TransportPolicy.ForceQuic },
                new NnrpAutoTransportClientOptions(
                    "127.0.0.1",
                    50072,
                    "localhost",
                    "engine-sr",
                    tcpPort: 50051),
                new TestAutoTransportRuntime(
                    quicClientFactory: _ => new NnrpQuicClient(
                        new NnrpQuicClientOptions("127.0.0.1", 50072, "localhost", "engine-sr"),
                        new TestQuicRuntime(
                            openConnection: (host, port, tlsServerName, requestedModel, requestedSessionId) =>
                                new NnrpNativeQuicClient.OpenResult(9, 41, "engine-sr"),
                            submitFrame: (_, __) => throw new NotSupportedException(),
                            submitPacket: (_, packet) => throw new NotSupportedException(),
                            beginSubmitPacket: (_, __) => throw new NotSupportedException(),
                            receiveResultPacket: _ => throw new NotSupportedException(),
                            receiveSessionPacket: _ => throw new NotSupportedException(),
                            pingRoundTrip: (_, __) => throw new NotSupportedException(),
                            cancelFrame: (_, __) => { },
                            closeConnection: _ => { }))));

            await client.ConnectAsync(new NnrpTransportProbeOptions(), cancellationToken: CancellationToken.None);

            var flowUpdateError = await Assert.ThrowsAsync<InvalidOperationException>(async () => await client.ReceiveFlowUpdateAsync(CancellationToken.None));
            var resultHintError = await Assert.ThrowsAsync<InvalidOperationException>(async () => await client.ReceiveResultHintAsync(CancellationToken.None));

            Assert.Contains("FLOW_UPDATE", flowUpdateError.Message, StringComparison.Ordinal);
            Assert.Contains("RESULT_HINT", resultHintError.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task ForcedQuicClientSupportsExplicitSubmitRequests()
        {
            var submittedPackets = new System.Collections.Generic.List<byte[]>();

            await using var client = new NnrpAutoTransportClient(
                new ClientProfile { TransportPolicy = TransportPolicy.ForceQuic },
                new NnrpAutoTransportClientOptions(
                    "127.0.0.1",
                    50072,
                    "localhost",
                    "engine-sr",
                    tcpPort: 50051),
                new TestAutoTransportRuntime(
                    quicClientFactory: _ => new NnrpQuicClient(
                        new NnrpQuicClientOptions("127.0.0.1", 50072, "localhost", "engine-sr"),
                        new TestQuicRuntime(
                            openConnection: (host, port, tlsServerName, requestedModel, requestedSessionId) =>
                                new NnrpNativeQuicClient.OpenResult(9, 41, "engine-sr"),
                            submitFrame: (_, __) => throw new NotSupportedException(),
                            submitPacket: (ulong _, byte[] packet) =>
                            {
                                submittedPackets.Add(packet);
                                Assert.True(NnrpFramedMessage.TryParse(packet, NnrpHeaderParseOptions.Strict, out var framed, out var parseError));
                                Assert.Equal(NnrpParseError.None, parseError);
                                return CreateResultPush(sessionId: 41, frameId: framed.Header.FrameId, wireFormat: NnrpHeader.CurrentWireFormat).ToArray();
                            },
                            beginSubmitPacket: (_, packet) => throw new NotSupportedException($"Unexpected background submit path: {packet.Length}"),
                            receiveResultPacket: _ => throw new NotSupportedException(),
                            receiveSessionPacket: _ => throw new NotSupportedException(),
                            pingRoundTrip: (_, __) => throw new NotSupportedException(),
                            cancelFrame: (_, __) => { },
                            closeConnection: _ => { }))));

            await client.ConnectAsync(new NnrpTransportProbeOptions(), cancellationToken: CancellationToken.None);

            var result0 = await client.SubmitAsync(CreateSubmitRequest(frameId: 303), CancellationToken.None);
            var result1 = await client.SubmitAsync(CreateSubmitRequest(frameId: 304), CancellationToken.None);

            Assert.Equal(2, submittedPackets.Count);
            Assert.Equal(303u, result0.FrameId);
            Assert.Equal(304u, result1.FrameId);
        }

        [Fact]
        public async Task ForcedQuicClientSupportsRawSubmitAndCancelPackets()
        {
            byte[]? observedSubmitPacket = null;
            FrameCancelMessage observedCancel = default;

            await using var client = new NnrpAutoTransportClient(
                new ClientProfile { TransportPolicy = TransportPolicy.ForceQuic },
                new NnrpAutoTransportClientOptions(
                    "127.0.0.1",
                    50072,
                    "localhost",
                    "engine-sr",
                    tcpPort: 50051),
                new TestAutoTransportRuntime(
                    quicClientFactory: _ => new NnrpQuicClient(
                        new NnrpQuicClientOptions("127.0.0.1", 50072, "localhost", "engine-sr"),
                        new TestQuicRuntime(
                            openConnection: (_, _, _, _, _) => new NnrpNativeQuicClient.OpenResult(9, 41, "engine-sr"),
                            submitFrame: (_, __) => throw new NotSupportedException(),
                            submitPacket: (_, packet) =>
                            {
                                observedSubmitPacket = packet;
                                Assert.True(NnrpFramedMessage.TryParse(packet, NnrpHeaderParseOptions.Strict, out var framed, out var parseError));
                                Assert.Equal(NnrpParseError.None, parseError);
                                return CreateResultPush(sessionId: 41, frameId: framed.Header.FrameId, wireFormat: NnrpHeader.CurrentWireFormat).ToArray();
                            },
                            cancelFrame: (_, cancel) => observedCancel = cancel,
                            closeConnection: _ => { }))));

            await client.ConnectAsync(new NnrpTransportProbeOptions(), cancellationToken: CancellationToken.None);

            var submitPacket = SmokePackets.CreateSmokeFrameSubmitMessage(sessionId: 41, frameId: 303).ToArray();
            var resultPacket = await client.SubmitAsync(submitPacket, CancellationToken.None);
            await client.CancelAsync(FrameCancelMessage.Create(41, 303, traceId: 7).ToArray(), CancellationToken.None);

            var submitError = await Assert.ThrowsAsync<ArgumentException>(async () => await client.SubmitAsync(Array.Empty<byte>(), CancellationToken.None));
            var cancelError = await Assert.ThrowsAsync<ArgumentException>(async () => await client.CancelAsync(Array.Empty<byte>(), CancellationToken.None));

            Assert.Equal(submitPacket, observedSubmitPacket);
            Assert.True(ResultPushMessage.TryParse(resultPacket, out var result, out var parseError));
            Assert.Equal(NnrpParseError.None, parseError);
            Assert.Equal(303u, result.Header.FrameId);
            Assert.Equal(303u, observedCancel.Header.FrameId);
            Assert.Equal(7ul, observedCancel.Header.TraceId);
            Assert.Contains("Submit packet must not be empty", submitError.Message, StringComparison.Ordinal);
            Assert.Contains("Cancel packet must not be empty", cancelError.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task ForcedTcpClientSupportsRawSubmitAndCancelPackets()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();

            try
            {
                var endpoint = (IPEndPoint)listener.LocalEndpoint;
                var serverTask = RunForcedTcpRawPacketServerAsync(listener, timeout.Token);

                await using var client = new NnrpAutoTransportClient(
                    new ClientProfile { TransportPolicy = TransportPolicy.ForceTcp },
                    new NnrpAutoTransportClientOptions(
                        IPAddress.Loopback.ToString(),
                        port: 65000,
                        tlsServerName: "localhost",
                        requestedModel: "engine-sr",
                        requestedSessionId: 41,
                        tcpPort: (ushort)endpoint.Port));

                await client.ConnectAsync(new NnrpTransportProbeOptions(), cancellationToken: timeout.Token);

                var submitPacket = SmokePackets.CreateSmokeFrameSubmitMessage(sessionId: 41, frameId: 303).ToArray();
                var resultPacket = await client.SubmitAsync(submitPacket, timeout.Token);
                await client.CancelAsync(FrameCancelMessage.Create(41, 303, traceId: 7).ToArray(), timeout.Token);
                await client.CloseAsync("forced-tcp-raw-packets done", cancellationToken: timeout.Token);

                var observation = await serverTask;

                Assert.True(ResultPushMessage.TryParse(resultPacket, out var result, out var parseError));
                Assert.Equal(NnrpParseError.None, parseError);
                Assert.Equal(303u, result.Header.FrameId);
                Assert.Equal(303u, observation.SubmittedFrameId);
                Assert.Equal(303u, observation.CanceledFrameId);
                Assert.Equal("forced-tcp-raw-packets done", observation.CloseReason);
            }
            finally
            {
                listener.Stop();
            }
        }

        [Fact]
        public async Task MigrateAsyncSwitchesTcpClientToQuicWithoutSessionTeardown()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();

            var quicPingCount = 0;
            var quicCloseCount = 0;

            try
            {
                var endpoint = (IPEndPoint)listener.LocalEndpoint;
                var serverTask = RunTcpToQuicMigrationServerAsync(listener, timeout.Token);

                await using var client = new NnrpAutoTransportClient(
                    new ClientProfile { TransportPolicy = TransportPolicy.ForceTcp },
                    new NnrpAutoTransportClientOptions(
                        IPAddress.Loopback.ToString(),
                        port: 65000,
                        tlsServerName: "localhost",
                        requestedModel: "engine-sr",
                        requestedSessionId: 41,
                        tcpPort: (ushort)endpoint.Port),
                    new TestAutoTransportRuntime(
                        quicClientFactory: _ => new NnrpQuicClient(
                            new NnrpQuicClientOptions(IPAddress.Loopback.ToString(), 65000, "localhost", "engine-sr", requestedSessionId: 41),
                            new TestQuicRuntime(
                                openConnection: (host, port, tlsServerName, requestedModel, requestedSessionId) =>
                                    new NnrpNativeQuicClient.OpenResult(9, 41, "engine-sr"),
                                submitFrame: (_, __) => throw new NotSupportedException(),
                                submitPacket: (_, packet) => throw new InvalidOperationException($"Unexpected QUIC submit packet after migration: {packet.Length}"),
                                pingRoundTrip: (_, ping) =>
                                {
                                    quicPingCount++;
                                    return PongMessage.Create(41, ping.Header.TraceId);
                                },
                                cancelFrame: (_, __) => { },
                                closeConnection: _ => quicCloseCount++))));

                await client.ConnectAsync(new NnrpTransportProbeOptions(), cancellationToken: timeout.Token);
                var ack = await client.MigrateAsync(
                    targetTransportId: TransportId.Quic,
                    lastResultFrameId: 303,
                    clientMigrateTimestampMicroseconds: 123456789,
                    traceId: 55,
                    cancellationToken: timeout.Token);

                var pongPacket = await client.PingAsync(PingMessage.Create(41, traceId: 77).ToArray(), timeout.Token);
                Assert.True(PongMessage.TryParse(pongPacket, out var pong, out var parseError));
                Assert.Equal(NnrpParseError.None, parseError);

                var serverObservation = await serverTask;

                Assert.Equal(0u, ack.Metadata.AcceptCode);
                Assert.Equal(TransportId.Quic, client.SelectedTransportId);
                Assert.Equal("quic", client.SelectedBindingName);
                Assert.Equal(TransportId.Tcp, serverObservation.OldTransportId);
                Assert.Equal(TransportId.Quic, serverObservation.NewTransportId);
                Assert.Equal(303ul, serverObservation.LastResultFrameId);
                Assert.Equal(77ul, pong.Header.TraceId);
                Assert.Equal(1, quicPingCount);

                await client.CloseAsync("migrated-tcp-to-quic", cancellationToken: timeout.Token);
                Assert.Equal(1, quicCloseCount);
            }
            finally
            {
                listener.Stop();
            }
        }

        [Fact]
        public async Task MigrateAsyncSwitchesQuicClientToTcpWithoutSessionTeardown()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();

            var quicCloseCount = 0;
            var quicMigrateSubmitCount = 0;

            try
            {
                var endpoint = (IPEndPoint)listener.LocalEndpoint;
                var serverTask = RunQuicToTcpMigrationServerAsync(listener, timeout.Token);

                await using var client = new NnrpAutoTransportClient(
                    new ClientProfile { TransportPolicy = TransportPolicy.ForceQuic },
                    new NnrpAutoTransportClientOptions(
                        IPAddress.Loopback.ToString(),
                        port: 65000,
                        tlsServerName: "localhost",
                        requestedModel: "engine-sr",
                        requestedSessionId: 41,
                        tcpPort: (ushort)endpoint.Port),
                    new TestAutoTransportRuntime(
                        quicClientFactory: _ => new NnrpQuicClient(
                            new NnrpQuicClientOptions(IPAddress.Loopback.ToString(), 65000, "localhost", "engine-sr", requestedSessionId: 41),
                            new TestQuicRuntime(
                                openConnection: (host, port, tlsServerName, requestedModel, requestedSessionId) =>
                                    new NnrpNativeQuicClient.OpenResult(9, 41, "engine-sr"),
                                submitFrame: (_, __) => throw new NotSupportedException(),
                                submitPacket: (_, packet) =>
                                {
                                    quicMigrateSubmitCount++;
                                    Assert.True(SessionMigrateMessage.TryParse(packet, out var migrate, out var parseError));
                                    Assert.Equal(NnrpParseError.None, parseError);
                                    Assert.Equal(TransportId.Quic, migrate.Metadata.OldTransportId);
                                    Assert.Equal(TransportId.Tcp, migrate.Metadata.NewTransportId);
                                    return CreateSessionMigrateAck(sessionId: 41, traceId: migrate.Header.TraceId, resumeFromFrameId: 304).ToArray();
                                },
                                pingRoundTrip: (_, __) => throw new InvalidOperationException("Ping should route over migrated TCP transport."),
                                cancelFrame: (_, __) => { },
                                closeConnection: _ => quicCloseCount++))));

                await client.ConnectAsync(new NnrpTransportProbeOptions(), cancellationToken: timeout.Token);
                var ack = await client.MigrateAsync(
                    targetTransportId: TransportId.Tcp,
                    lastResultFrameId: 303,
                    clientMigrateTimestampMicroseconds: 123456789,
                    traceId: 55,
                    cancellationToken: timeout.Token);

                var pongPacket = await client.PingAsync(PingMessage.Create(41, traceId: 77).ToArray(), timeout.Token);
                Assert.True(PongMessage.TryParse(pongPacket, out var pong, out var parseError));
                Assert.Equal(NnrpParseError.None, parseError);

                Assert.Equal(0u, ack.Metadata.AcceptCode);
                Assert.Equal(TransportId.Tcp, client.SelectedTransportId);
                Assert.Equal("tcp", client.SelectedBindingName);
                Assert.Equal(1, quicMigrateSubmitCount);
                Assert.Equal(1, quicCloseCount);
                Assert.Equal(77ul, pong.Header.TraceId);

                await client.CloseAsync("migrated-quic-to-tcp", cancellationToken: timeout.Token);
                var serverObservation = await serverTask;

                Assert.Equal("migrated-quic-to-tcp", serverObservation.CloseReason);
            }
            finally
            {
                listener.Stop();
            }
        }

        [Fact]
        public async Task ConnectAsyncCanProbeQuicThenMigrateToTcp()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();

            var quicProbeCount = 0;
            var quicMigrateSubmitCount = 0;
            var quicCloseCount = 0;
            NnrpQuicCertificateVerificationMode observedProbeCertificateVerificationMode = 0;
            string? observedProbeCaCertificatePath = null;

            try
            {
                var endpoint = (IPEndPoint)listener.LocalEndpoint;
                var serverTask = RunProbeThenQuicToTcpMigrationServerAsync(listener, timeout.Token);

                await using var client = new NnrpAutoTransportClient(
                    new ClientProfile { TransportPolicy = TransportPolicy.Auto },
                    new NnrpAutoTransportClientOptions(
                        IPAddress.Loopback.ToString(),
                        port: 65000,
                        tlsServerName: "localhost",
                        requestedModel: "engine-sr",
                        requestedSessionId: 41,
                        tcpPort: (ushort)endpoint.Port,
                        certificateVerificationMode: NnrpQuicCertificateVerificationMode.InsecureSkipVerify,
                        caCertificatePath: "certs/test-ca.pem"),
                    new TestAutoTransportRuntime(
                        quicClientFactory: _ => new NnrpQuicClient(
                            new NnrpQuicClientOptions(IPAddress.Loopback.ToString(), 65000, "localhost", "engine-sr", requestedSessionId: 41),
                            new TestQuicRuntime(
                                openConnection: (host, port, tlsServerName, requestedModel, requestedSessionId) =>
                                    new NnrpNativeQuicClient.OpenResult(9, 41, "engine-sr"),
                                submitFrame: (_, __) => throw new NotSupportedException(),
                                submitPacket: (_, packet) =>
                                {
                                    quicMigrateSubmitCount++;
                                    Assert.True(SessionMigrateMessage.TryParse(packet, out var migrate, out var parseError));
                                    Assert.Equal(NnrpParseError.None, parseError);
                                    Assert.Equal(TransportId.Quic, migrate.Metadata.OldTransportId);
                                    Assert.Equal(TransportId.Tcp, migrate.Metadata.NewTransportId);
                                    return CreateSessionMigrateAck(sessionId: 41, traceId: migrate.Header.TraceId, resumeFromFrameId: 304).ToArray();
                                },
                                pingRoundTrip: (_, __) => throw new InvalidOperationException("Ping should route over migrated TCP transport."),
                                cancelFrame: (_, __) => { },
                                closeConnection: _ => quicCloseCount++)),
                        quicProbeWithCertificateOptions: (host, port, tlsServerName, probePacket, certificateVerificationMode, caCertificatePath) =>
                        {
                            quicProbeCount++;
                            observedProbeCertificateVerificationMode = certificateVerificationMode;
                            observedProbeCaCertificatePath = caCertificatePath;
                            Assert.Equal(IPAddress.Loopback.ToString(), host);
                            Assert.Equal((ushort)65000, port);
                            Assert.Equal("localhost", tlsServerName);
                            Assert.True(TransportProbeMessage.TryParse(probePacket, out var probe, out var parseError));
                            Assert.Equal(NnrpParseError.None, parseError);
                            return new TransportProbeAckMessage(
                                new NnrpHeader(
                                    NnrpHeader.CurrentVersionMajor,
                                    NnrpHeader.CurrentWireFormat,
                                    MessageType.TransportProbeAck,
                                    HeaderFlags.None,
                                    TransportProbeAckMetadata.MetadataLength,
                                    0,
                                    0,
                                    0,
                                    0,
                                    0,
                                    probe.Header.TraceId),
                                new TransportProbeAckMetadata(
                                    probe.Metadata.ProbeId,
                                    0,
                                    probe.Metadata.ClientSendTimestampMicroseconds + 100)).ToArray();
                        }));

                var connectResult = await client.ConnectAsync(
                    new NnrpTransportProbeOptions
                    {
                        WarmupProbeCount = 0,
                        ScoredProbeCount = 1,
                        PayloadBytes = 64,
                    },
                    cancellationToken: timeout.Token);

                var ack = await client.MigrateAsync(
                    targetTransportId: TransportId.Tcp,
                    lastResultFrameId: 303,
                    clientMigrateTimestampMicroseconds: 123456789,
                    traceId: 55,
                    cancellationToken: timeout.Token);

                var pongPacket = await client.PingAsync(PingMessage.Create(41, traceId: 77).ToArray(), timeout.Token);
                Assert.True(PongMessage.TryParse(pongPacket, out var pong, out var parseError));
                Assert.Equal(NnrpParseError.None, parseError);

                Assert.True(connectResult.WasProbed);
                Assert.True(connectResult.ProbeSelection.HasValue);
                Assert.Equal(TransportId.Quic, connectResult.SelectedTransportId);
                Assert.Equal("quic", connectResult.SelectedBindingName);
                Assert.Equal(TransportId.Quic, connectResult.ProbeSelection.Value.SelectedTransportId);
                Assert.Equal(41u, connectResult.NegotiatedSessionId);
                Assert.Equal(NnrpHeader.CurrentWireFormat, connectResult.NegotiatedWireFormat);
                Assert.Equal(NnrpQuicCertificateVerificationMode.InsecureSkipVerify, observedProbeCertificateVerificationMode);
                Assert.Equal("certs/test-ca.pem", observedProbeCaCertificatePath);
                Assert.Equal(0u, ack.Metadata.AcceptCode);
                Assert.Equal(TransportId.Tcp, client.SelectedTransportId);
                Assert.Equal("tcp", client.SelectedBindingName);
                Assert.Equal(77ul, pong.Header.TraceId);
                Assert.Equal(1, quicProbeCount);
                Assert.Equal(1, quicMigrateSubmitCount);
                Assert.Equal(1, quicCloseCount);

                await client.CloseAsync("probed-quic-to-tcp", cancellationToken: timeout.Token);
                var serverObservation = await serverTask;
                Assert.Equal(64u, serverObservation.ProbePayloadBytes);
                Assert.Equal("probed-quic-to-tcp", serverObservation.CloseReason);
            }
            finally
            {
                listener.Stop();
            }
        }

        [Fact]
        public async Task ConnectAsyncCanProbeTcpThenMigrateToQuic()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();

            var quicProbeCount = 0;
            var quicPingCount = 0;
            var quicCloseCount = 0;

            try
            {
                var endpoint = (IPEndPoint)listener.LocalEndpoint;
                var serverTask = RunProbeThenTcpToQuicMigrationServerAsync(listener, timeout.Token);

                await using var client = new NnrpAutoTransportClient(
                    new ClientProfile { TransportPolicy = TransportPolicy.Auto },
                    new NnrpAutoTransportClientOptions(
                        IPAddress.Loopback.ToString(),
                        port: 65000,
                        tlsServerName: "localhost",
                        requestedModel: "engine-sr",
                        requestedSessionId: 41,
                        tcpPort: (ushort)endpoint.Port),
                    new TestAutoTransportRuntime(
                        quicClientFactory: _ => new NnrpQuicClient(
                            new NnrpQuicClientOptions(IPAddress.Loopback.ToString(), 65000, "localhost", "engine-sr", requestedSessionId: 41),
                            new TestQuicRuntime(
                                openConnection: (host, port, tlsServerName, requestedModel, requestedSessionId) =>
                                    new NnrpNativeQuicClient.OpenResult(9, 41, "engine-sr"),
                                submitFrame: (_, __) => throw new NotSupportedException(),
                                submitPacket: (_, packet) => throw new InvalidOperationException($"Unexpected QUIC submit packet after migration: {packet.Length}"),
                                pingRoundTrip: (_, ping) =>
                                {
                                    quicPingCount++;
                                    return PongMessage.Create(41, ping.Header.TraceId);
                                },
                                cancelFrame: (_, __) => { },
                                closeConnection: _ => quicCloseCount++)),
                        quicProbe: (host, port, tlsServerName, probePacket) =>
                        {
                            quicProbeCount++;
                            Assert.Equal(IPAddress.Loopback.ToString(), host);
                            Assert.Equal((ushort)65000, port);
                            Assert.Equal("localhost", tlsServerName);
                            Assert.True(TransportProbeMessage.TryParse(probePacket, out var probe, out var parseError));
                            Assert.Equal(NnrpParseError.None, parseError);
                            Thread.Sleep(TimeSpan.FromMilliseconds(75));
                            return new TransportProbeAckMessage(
                                new NnrpHeader(
                                    NnrpHeader.CurrentVersionMajor,
                                    NnrpHeader.CurrentWireFormat,
                                    MessageType.TransportProbeAck,
                                    HeaderFlags.None,
                                    TransportProbeAckMetadata.MetadataLength,
                                    0,
                                    0,
                                    0,
                                    0,
                                    0,
                                    probe.Header.TraceId),
                                new TransportProbeAckMetadata(
                                    probe.Metadata.ProbeId,
                                    0,
                                    probe.Metadata.ClientSendTimestampMicroseconds + 100)).ToArray();
                        }));

                var connectResult = await client.ConnectAsync(
                    new NnrpTransportProbeOptions
                    {
                        WarmupProbeCount = 0,
                        ScoredProbeCount = 1,
                        PayloadBytes = 64,
                    },
                    cancellationToken: timeout.Token);

                var ack = await client.MigrateAsync(
                    targetTransportId: TransportId.Quic,
                    lastResultFrameId: 303,
                    clientMigrateTimestampMicroseconds: 123456789,
                    traceId: 55,
                    cancellationToken: timeout.Token);

                var pongPacket = await client.PingAsync(PingMessage.Create(41, traceId: 77).ToArray(), timeout.Token);
                Assert.True(PongMessage.TryParse(pongPacket, out var pong, out var parseError));
                Assert.Equal(NnrpParseError.None, parseError);

                Assert.True(connectResult.WasProbed);
                Assert.True(connectResult.ProbeSelection.HasValue);
                Assert.Equal(TransportId.Tcp, connectResult.SelectedTransportId);
                Assert.Equal("tcp", connectResult.SelectedBindingName);
                Assert.Equal(TransportId.Tcp, connectResult.ProbeSelection.Value.SelectedTransportId);
                Assert.Equal(41u, connectResult.NegotiatedSessionId);
                Assert.Equal(NnrpHeader.CurrentWireFormat, connectResult.NegotiatedWireFormat);
                Assert.Equal(0u, ack.Metadata.AcceptCode);
                Assert.Equal(TransportId.Quic, client.SelectedTransportId);
                Assert.Equal("quic", client.SelectedBindingName);
                Assert.Equal(77ul, pong.Header.TraceId);
                Assert.Equal(1, quicProbeCount);
                Assert.Equal(1, quicPingCount);

                await client.CloseAsync("probed-tcp-to-quic", cancellationToken: timeout.Token);
                var serverObservation = await serverTask;
                Assert.Equal(64u, serverObservation.ProbePayloadBytes);
                Assert.Equal(TransportId.Tcp, serverObservation.OldTransportId);
                Assert.Equal(TransportId.Quic, serverObservation.NewTransportId);
                Assert.Equal(303ul, serverObservation.LastResultFrameId);
                Assert.Equal(1, quicCloseCount);
            }
            finally
            {
                listener.Stop();
            }
        }

        private static async Task<ForcedTcpServerObservation> RunForcedTcpServerAsync(TcpListener listener, CancellationToken cancellationToken)
        {
            using var tcpClient = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
            await using var transport = new NnrpTcpMessageTransport(tcpClient);

            var helloFrame = await transport.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            Assert.True(ClientHelloMessage.TryParse(helloFrame, out var hello, out var parseError));
            Assert.Equal(NnrpParseError.None, parseError);
            Assert.True(hello.TryGetClientTransportPolicyExtension(out var transportPolicy, out parseError));
            Assert.Equal(NnrpParseError.None, parseError);

            var ack = CreateServerHelloAck(sessionId: 41, traceId: hello.Header.TraceId);
            await transport.SendAsync(ack.ToFramedMessage(), cancellationToken).ConfigureAwait(false);

            var closeFrame = await transport.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            Assert.True(CloseMessage.TryParse(closeFrame.ToArray(), out var close, out parseError));
            Assert.Equal(NnrpParseError.None, parseError);

            return new ForcedTcpServerObservation(transportPolicy.PreferredTransportId, close.Reason);
        }

        private static async Task<ForcedTcpServerObservation> RunForcedTcpFlowControlServerAsync(TcpListener listener, CancellationToken cancellationToken)
        {
            using var tcpClient = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
            await using var transport = new NnrpTcpMessageTransport(tcpClient);

            var helloFrame = await transport.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            Assert.True(ClientHelloMessage.TryParse(helloFrame, out var hello, out var parseError));
            Assert.Equal(NnrpParseError.None, parseError);
            Assert.True(hello.TryGetClientTransportPolicyExtension(out var transportPolicy, out parseError));
            Assert.Equal(NnrpParseError.None, parseError);

            var ack = CreateServerHelloAck(sessionId: 41, traceId: hello.Header.TraceId);
            await transport.SendAsync(ack.ToFramedMessage(), cancellationToken).ConfigureAwait(false);
            await transport.SendAsync(CreateFlowUpdate(sessionId: 41).ToFramedMessage(), cancellationToken).ConfigureAwait(false);
            await transport.SendAsync(CreateResultHint(sessionId: 41).ToFramedMessage(), cancellationToken).ConfigureAwait(false);

            var closeFrame = await transport.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            Assert.True(CloseMessage.TryParse(closeFrame.ToArray(), out var close, out parseError));
            Assert.Equal(NnrpParseError.None, parseError);

            return new ForcedTcpServerObservation(transportPolicy.PreferredTransportId, close.Reason);
        }

        private static async Task<ForcedTcpSessionPumpObservation> RunForcedTcpExplicitSubmitServerAsync(TcpListener listener, CancellationToken cancellationToken)
        {
            using var tcpClient = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
            await using var transport = new NnrpTcpMessageTransport(tcpClient);

            var helloFrame = await transport.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            Assert.True(ClientHelloMessage.TryParse(helloFrame, out var hello, out var parseError));
            Assert.Equal(NnrpParseError.None, parseError);

            var ack = CreateServerHelloAck(sessionId: 41, traceId: hello.Header.TraceId);
            await transport.SendAsync(ack.ToFramedMessage(), cancellationToken).ConfigureAwait(false);

            var submit0 = await transport.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            Assert.Equal(MessageType.FrameSubmit, submit0.Header.MessageType);
            Assert.Equal(NnrpHeader.CurrentWireFormat, submit0.Header.WireFormat);
            Assert.Equal(303u, submit0.Header.FrameId);
            await transport.SendAsync(CreateResultPush(sessionId: 41, frameId: 303, wireFormat: NnrpHeader.CurrentWireFormat).ToFramedMessage(), cancellationToken).ConfigureAwait(false);

            var submit1 = await transport.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            Assert.Equal(MessageType.FrameSubmit, submit1.Header.MessageType);
            Assert.Equal(NnrpHeader.CurrentWireFormat, submit1.Header.WireFormat);
            Assert.Equal(304u, submit1.Header.FrameId);
            await transport.SendAsync(CreateResultPush(sessionId: 41, frameId: 304, wireFormat: NnrpHeader.CurrentWireFormat).ToFramedMessage(), cancellationToken).ConfigureAwait(false);

            var closeFrame = await transport.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            Assert.True(CloseMessage.TryParse(closeFrame.ToArray(), out var close, out parseError));
            Assert.Equal(NnrpParseError.None, parseError);

            return new ForcedTcpSessionPumpObservation(new uint[] { submit0.Header.FrameId, submit1.Header.FrameId }, close.Reason);
        }

        private static async Task<ForcedTcpRawPacketObservation> RunForcedTcpRawPacketServerAsync(TcpListener listener, CancellationToken cancellationToken)
        {
            using var tcpClient = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
            await using var transport = new NnrpTcpMessageTransport(tcpClient);

            var helloFrame = await transport.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            Assert.True(ClientHelloMessage.TryParse(helloFrame, out var hello, out var parseError));
            Assert.Equal(NnrpParseError.None, parseError);

            var ack = CreateServerHelloAck(sessionId: 41, traceId: hello.Header.TraceId);
            await transport.SendAsync(ack.ToFramedMessage(), cancellationToken).ConfigureAwait(false);

            var submitFrame = await transport.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            Assert.Equal(MessageType.FrameSubmit, submitFrame.Header.MessageType);
            await transport.SendAsync(CreateResultPush(sessionId: 41, frameId: submitFrame.Header.FrameId, wireFormat: NnrpHeader.CurrentWireFormat).ToFramedMessage(), cancellationToken).ConfigureAwait(false);

            var cancelFrame = await transport.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            Assert.True(FrameCancelMessage.TryParse(cancelFrame.ToArray(), out var cancel, out parseError));
            Assert.Equal(NnrpParseError.None, parseError);

            var closeFrame = await transport.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            Assert.True(CloseMessage.TryParse(closeFrame.ToArray(), out var close, out parseError));
            Assert.Equal(NnrpParseError.None, parseError);

            return new ForcedTcpRawPacketObservation(submitFrame.Header.FrameId, cancel.Header.FrameId, close.Reason);
        }

        private static async Task<MigrationServerObservation> RunTcpToQuicMigrationServerAsync(TcpListener listener, CancellationToken cancellationToken)
        {
            using var tcpClient = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
            await using var transport = new NnrpTcpMessageTransport(tcpClient);

            var helloFrame = await transport.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            Assert.True(ClientHelloMessage.TryParse(helloFrame, out var hello, out var parseError));
            Assert.Equal(NnrpParseError.None, parseError);

            var ack = CreateServerHelloAck(sessionId: 41, traceId: hello.Header.TraceId);
            await transport.SendAsync(ack.ToFramedMessage(), cancellationToken).ConfigureAwait(false);

            var migrateFrame = await transport.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            Assert.True(SessionMigrateMessage.TryParse(migrateFrame, out var migrate, out parseError));
            Assert.Equal(NnrpParseError.None, parseError);

            await transport.SendAsync(
                CreateSessionMigrateAck(sessionId: 41, traceId: migrate.Header.TraceId, resumeFromFrameId: 304).ToFramedMessage(),
                cancellationToken).ConfigureAwait(false);

            return new MigrationServerObservation(
                migrate.Metadata.OldTransportId,
                migrate.Metadata.NewTransportId,
                migrate.Metadata.LastResultFrameId,
                string.Empty);
        }

        private static async Task<MigrationServerObservation> RunQuicToTcpMigrationServerAsync(TcpListener listener, CancellationToken cancellationToken)
        {
            using var tcpClient = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
            await using var transport = new NnrpTcpMessageTransport(tcpClient);

            var pingFrame = await transport.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            Assert.True(PingMessage.TryParse(pingFrame.ToArray(), out var ping, out var parseError));
            Assert.Equal(NnrpParseError.None, parseError);

            await transport.SendAsync(PongMessage.Create(sessionId: 41, traceId: ping.Header.TraceId).ToFramedMessage(), cancellationToken).ConfigureAwait(false);

            var closeFrame = await transport.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            Assert.True(CloseMessage.TryParse(closeFrame.ToArray(), out var close, out parseError));
            Assert.Equal(NnrpParseError.None, parseError);

            return new MigrationServerObservation(TransportId.Quic, TransportId.Tcp, 303, close.Reason);
        }

        private static async Task<ProbeThenMigrationServerObservation> RunProbeThenQuicToTcpMigrationServerAsync(TcpListener listener, CancellationToken cancellationToken)
        {
            uint probePayloadBytes;

            using (var probeClient = await listener.AcceptTcpClientAsync().ConfigureAwait(false))
            {
                await using var probeTransport = new NnrpTcpMessageTransport(probeClient);

                var probeFrame = await probeTransport.ReceiveAsync(cancellationToken).ConfigureAwait(false);
                Assert.True(TransportProbeMessage.TryParse(probeFrame, out var probe, out var probeParseError));
                Assert.Equal(NnrpParseError.None, probeParseError);
                probePayloadBytes = probe.Metadata.ProbePayloadBytes;

                await Task.Delay(TimeSpan.FromMilliseconds(25), cancellationToken).ConfigureAwait(false);
                await probeTransport.SendAsync(
                    new TransportProbeAckMessage(
                        new NnrpHeader(
                            NnrpHeader.CurrentVersionMajor,
                            NnrpHeader.CurrentWireFormat,
                            MessageType.TransportProbeAck,
                            HeaderFlags.None,
                            TransportProbeAckMetadata.MetadataLength,
                            0,
                            0,
                            0,
                            0,
                            0,
                            probe.Header.TraceId),
                        new TransportProbeAckMetadata(
                            probe.Metadata.ProbeId,
                            0,
                            probe.Metadata.ClientSendTimestampMicroseconds + 200)).ToFramedMessage(),
                    cancellationToken).ConfigureAwait(false);
            }

            using var tcpClient = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
            await using var transport = new NnrpTcpMessageTransport(tcpClient);

            var pingFrame = await transport.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            Assert.True(PingMessage.TryParse(pingFrame.ToArray(), out var ping, out var pingError));
            Assert.Equal(NnrpParseError.None, pingError);

            await transport.SendAsync(PongMessage.Create(sessionId: 41, traceId: ping.Header.TraceId).ToFramedMessage(), cancellationToken).ConfigureAwait(false);

            var closeFrame = await transport.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            Assert.True(CloseMessage.TryParse(closeFrame.ToArray(), out var close, out var closeError));
            Assert.Equal(NnrpParseError.None, closeError);

            return new ProbeThenMigrationServerObservation(probePayloadBytes, close.Reason);
        }

        private static async Task<ProbeThenMigrationControlPlaneObservation> RunProbeThenTcpToQuicMigrationServerAsync(TcpListener listener, CancellationToken cancellationToken)
        {
            uint probePayloadBytes;

            using (var probeClient = await listener.AcceptTcpClientAsync().ConfigureAwait(false))
            {
                await using var probeTransport = new NnrpTcpMessageTransport(probeClient);

                var probeFrame = await probeTransport.ReceiveAsync(cancellationToken).ConfigureAwait(false);
                Assert.True(TransportProbeMessage.TryParse(probeFrame, out var probe, out var probeParseError));
                Assert.Equal(NnrpParseError.None, probeParseError);
                probePayloadBytes = probe.Metadata.ProbePayloadBytes;

                await probeTransport.SendAsync(
                    new TransportProbeAckMessage(
                        new NnrpHeader(
                            NnrpHeader.CurrentVersionMajor,
                            NnrpHeader.CurrentWireFormat,
                            MessageType.TransportProbeAck,
                            HeaderFlags.None,
                            TransportProbeAckMetadata.MetadataLength,
                            0,
                            0,
                            0,
                            0,
                            0,
                            probe.Header.TraceId),
                        new TransportProbeAckMetadata(
                            probe.Metadata.ProbeId,
                            0,
                            probe.Metadata.ClientSendTimestampMicroseconds + 100)).ToFramedMessage(),
                    cancellationToken).ConfigureAwait(false);
            }

            using var tcpClient = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
            await using var transport = new NnrpTcpMessageTransport(tcpClient);

            var helloFrame = await transport.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            Assert.True(ClientHelloMessage.TryParse(helloFrame, out var hello, out var parseError));
            Assert.Equal(NnrpParseError.None, parseError);

            var ack = CreateServerHelloAck(sessionId: 41, traceId: hello.Header.TraceId);
            await transport.SendAsync(ack.ToFramedMessage(), cancellationToken).ConfigureAwait(false);

            var migrateFrame = await transport.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            Assert.True(SessionMigrateMessage.TryParse(migrateFrame, out var migrate, out parseError));
            Assert.Equal(NnrpParseError.None, parseError);

            await transport.SendAsync(
                CreateSessionMigrateAck(sessionId: 41, traceId: migrate.Header.TraceId, resumeFromFrameId: 304).ToFramedMessage(),
                cancellationToken).ConfigureAwait(false);

            return new ProbeThenMigrationControlPlaneObservation(
                probePayloadBytes,
                migrate.Metadata.OldTransportId,
                migrate.Metadata.NewTransportId,
                migrate.Metadata.LastResultFrameId);
        }

        private static ServerHelloAckMessage CreateServerHelloAck(uint sessionId, ulong traceId)
        {
            var extensions = new[]
            {
                new ServerTransportPolicyAckExtension(TransportPolicy.ForceTcp, TransportPolicy.ForceTcp, TransportId.Tcp).ToControlExtension(),
            };
            var extensionBytes = GetExtensionBodyLength(extensions);
            var metadata = new ServerHelloAckMetadata(
                selectedVersionMajor: NnrpHeader.CurrentVersionMajor,
                selectedWireFormat: NnrpHeader.CurrentWireFormat,
                authStatus: 0,
                reserved0: 0,
                sessionId: sessionId,
                acceptedProfileBitmap: ControlMetadataBitmaps.TensorProfileBitmap,
                acceptedPayloadKindBitmap: ControlMetadataBitmaps.TensorPayloadKindBitmap,
                acceptedCodecBitmap: ControlMetadataBitmaps.EncodeCodecBitmap(new[] { CodecId.Raw }),
                acceptedCompressionBitmap: ControlMetadataBitmaps.EncodeCodecBitmap(new[] { CodecId.Raw }),
                acceptedDTypeBitmap: ControlMetadataBitmaps.EncodeDTypeBitmap(new[] { DTypeId.UInt8, DTypeId.Float16 }),
                acceptedLayoutBitmap: ControlMetadataBitmaps.EncodeTensorLayoutBitmap(new[] { TensorLayoutId.Nhwc }),
                cacheDigestBitmap: ControlMetadataBitmaps.CacheDigestBitmap,
                cacheObjectBitmap: ControlMetadataBitmaps.TensorProfileCacheObjectBitmap,
                maxCacheEntries: 128,
                maxCacheBytes: ControlMetadataBitmaps.DefaultCacheBytes,
                maxLaneCount: 1,
                maxConcurrentFrames: 2,
                targetCadenceX100: 6000,
                latencyBudgetMilliseconds: 16,
                qualityTier: ControlMetadataBitmaps.DefaultQualityTier,
                degradePolicy: 0,
                maxBodyBytes: 1024 * 1024,
                tokenTtlMilliseconds: 60000,
                retryAfterMilliseconds: 0,
                controlExtensionBytes: (uint)extensionBytes,
                serverFlags: 0);
            var header = new NnrpHeader(
                versionMajor: NnrpHeader.CurrentVersionMajor,
                messageType: MessageType.ServerHelloAck,
                flags: HeaderFlags.None,
                metaLength: ServerHelloAckMetadata.MetadataLength,
                bodyLength: (uint)extensionBytes,
                sessionId: sessionId,
                frameId: 0,
                viewId: 0,
                routeId: 0,
                traceId: traceId);
            return new ServerHelloAckMessage(header, metadata, extensions);
        }

        private static int GetExtensionBodyLength(ControlExtensionBlock[] extensions)
        {
            var total = 0;
            for (var i = 0; i < extensions.Length; i++)
            {
                total += extensions[i].TotalLength;
            }

            return total;
        }

        private static SessionMigrateAckMessage CreateSessionMigrateAck(uint sessionId, ulong traceId, ulong resumeFromFrameId)
        {
            var header = new NnrpHeader(
                versionMajor: NnrpHeader.CurrentVersionMajor,
                messageType: MessageType.SessionMigrateAck,
                flags: HeaderFlags.None,
                metaLength: SessionMigrateAckMetadata.MetadataLength,
                bodyLength: 0,
                sessionId: sessionId,
                frameId: 0,
                viewId: 0,
                routeId: 0,
                traceId: traceId);
            var metadata = new SessionMigrateAckMetadata(
                acceptCode: 0,
                resumeFromFrameId: resumeFromFrameId,
                graceWindowMilliseconds: 250,
                serverMigrateTimestampMicroseconds: 123456889);
            return new SessionMigrateAckMessage(header, metadata);
        }

        private static FlowUpdateMessage CreateFlowUpdate(uint sessionId)
        {
            var header = new NnrpHeader(
                versionMajor: NnrpHeader.CurrentVersionMajor,
                messageType: MessageType.FlowUpdate,
                flags: HeaderFlags.None,
                metaLength: FlowUpdateMetadata.MetadataLength,
                bodyLength: 0,
                sessionId: sessionId,
                frameId: 0,
                viewId: 0,
                routeId: 0,
                traceId: 0);
            var metadata = new FlowUpdateMetadata(
                scopeKind: FlowUpdateScopeKind.Session,
                updateReason: FlowUpdateReason.Congestion,
                backpressureLevel: FlowUpdateBackpressureLevel.Soft,
                connectionCredit: 0,
                sessionCredit: 2,
                operationCredit: 0,
                operationId: 0,
                retryAfterMilliseconds: 4,
                creditEpoch: 9,
                flags: FlowUpdateFlags.CreditValid | FlowUpdateFlags.RetryAfterValid);
            return new FlowUpdateMessage(header, metadata);
        }

        private static ResultHintMessage CreateResultHint(uint sessionId)
        {
            var header = new NnrpHeader(
                versionMajor: NnrpHeader.CurrentVersionMajor,
                messageType: MessageType.ResultHint,
                flags: HeaderFlags.None,
                metaLength: ResultHintMetadata.MetadataLength,
                bodyLength: 0,
                sessionId: sessionId,
                frameId: 0,
                viewId: 0,
                routeId: 0,
                traceId: 0);
            var metadata = new ResultHintMetadata(
                appliedBudgetPolicy: ResultHintBudgetPolicy.Partial,
                congestionState: ResultHintCongestionState.Elevated,
                reason: ResultHintReason.ServerBusy,
                retryAfterMilliseconds: 7);
            return new ResultHintMessage(header, metadata);
        }

        private static ResultPushMessage CreateResultPush(
            uint sessionId,
            uint frameId,
            byte wireFormat = NnrpHeader.CurrentWireFormat,
            ResultClass resultClass = ResultClass.Complete,
            ResultFlags resultFlags = ResultFlags.None,
            BudgetPolicy appliedBudgetPolicy = BudgetPolicy.None,
            uint reusedFrameId = 0,
            ushort coveredTileCount = 0,
            ushort droppedTileCount = 0,
            PayloadKind payloadKindBitmap = PayloadKind.Tensor,
            ushort payloadFrameCount = 0,
            ReadOnlyMemory<TypedPayloadDescriptor> typedPayloadDescriptors = default,
            ReadOnlyMemory<byte> typedPayloadFrameRegion = default)
        {
            var section = new TensorSectionBlock(
                new TensorSectionDescriptor(
                    role: TensorRole.SrResidual,
                    codec: CodecId.Raw,
                    dtype: DTypeId.UInt8,
                    layout: TensorLayoutId.Nhwc,
                    scalePolicy: ScalePolicy.None,
                    flags: 0,
                    elementCountPerTile: 0,
                    codecTableBytes: 0,
                    lengthTableBytes: 12,
                    payloadBytes: 3,
                    payloadStrideBytes: 0),
                Array.Empty<byte>(),
                new byte[] { 1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0 },
                new byte[] { 0x10, 0x20, 0x30 });
            var metadata = new ResultPushMetadata(
                statusCode: ResultStatusCode.Success,
                resultFlags: resultFlags,
                sectionCount: 1,
                tileCount: 3,
                activeProfileId: 1,
                inferenceMilliseconds: 2,
                queueMilliseconds: 1,
                serverTotalMilliseconds: 4,
                tileBaseId: 0,
                tileIndexBytes: 6,
                resultClass: resultClass,
                appliedBudgetPolicy: appliedBudgetPolicy,
                reusedFrameId: reusedFrameId,
                coveredTileCount: coveredTileCount,
                droppedTileCount: droppedTileCount,
                payloadKindBitmap: payloadKindBitmap,
                payloadFrameCount: payloadFrameCount);
            var bodyLength = (uint)(BinaryAlignment.AlignUp(TensorResultBlock.BlockLength + 6, 8) + section.TotalLength);
            var header = new NnrpHeader(
                versionMajor: NnrpHeader.CurrentVersionMajor,
                messageType: MessageType.ResultPush,
                flags: HeaderFlags.None,
                metaLength: ResultPushMetadata.MetadataLength,
                bodyLength: bodyLength,
                sessionId: sessionId,
                frameId: frameId,
                viewId: 0,
                routeId: 0,
                traceId: 0,
                wireFormat: wireFormat);
            return new ResultPushMessage(
                header,
                metadata,
                new ushort[] { 0, 1, 2 },
                new[] { section },
                typedPayloadDescriptors,
                typedPayloadFrameRegion);
        }

        private static NnrpSubmitRequest CreateSubmitRequest(uint frameId)
        {
            var section = new TensorSectionBlock(
                new TensorSectionDescriptor(
                    role: TensorRole.LumaHint,
                    codec: CodecId.Raw,
                    dtype: DTypeId.UInt8,
                    layout: TensorLayoutId.Nhwc,
                    scalePolicy: ScalePolicy.None,
                    flags: 0,
                    elementCountPerTile: 0,
                    codecTableBytes: 0,
                    lengthTableBytes: 12,
                    payloadBytes: 6,
                    payloadStrideBytes: 0),
                Array.Empty<byte>(),
                new byte[] { 2, 0, 0, 0, 2, 0, 0, 0, 2, 0, 0, 0 },
                new byte[] { 0x41, 0x42, 0x43, 0x44, 0x45, 0x46 });
            return new NnrpSubmitRequest(
                frameId: frameId,
                sourceWidth: 64,
                sourceHeight: 64,
                tileWidth: 32,
                tileHeight: 32,
                cameraBlock: new byte[] { 0x10, 0x20, 0x30 },
                tileIds: new ushort[] { 0, 1, 2 },
                sections: new[] { section },
                latencyBudgetMilliseconds: 16,
                cadenceHintX100: 6000,
                inputProfile: InputProfile.ChangedTilesLuma,
                tileIndexMode: TileIndexMode.RawUInt16);
        }

        private readonly struct ForcedTcpServerObservation
        {
            public ForcedTcpServerObservation(TransportId preferredTransportId, string closeReason)
            {
                PreferredTransportId = preferredTransportId;
                CloseReason = closeReason;
            }

            public TransportId PreferredTransportId { get; }

            public string CloseReason { get; }
        }

        private readonly struct ForcedTcpSessionPumpObservation
        {
            public ForcedTcpSessionPumpObservation(uint[] submittedFrameIds, string closeReason)
            {
                SubmittedFrameIds = submittedFrameIds ?? Array.Empty<uint>();
                CloseReason = closeReason;
            }

            public uint[] SubmittedFrameIds { get; }

            public string CloseReason { get; }
        }

        private readonly struct ForcedTcpRawPacketObservation
        {
            public ForcedTcpRawPacketObservation(uint submittedFrameId, uint canceledFrameId, string closeReason)
            {
                SubmittedFrameId = submittedFrameId;
                CanceledFrameId = canceledFrameId;
                CloseReason = closeReason;
            }

            public uint SubmittedFrameId { get; }

            public uint CanceledFrameId { get; }

            public string CloseReason { get; }
        }

        private readonly struct MigrationServerObservation
        {
            public MigrationServerObservation(TransportId oldTransportId, TransportId newTransportId, ulong lastResultFrameId, string closeReason)
            {
                OldTransportId = oldTransportId;
                NewTransportId = newTransportId;
                LastResultFrameId = lastResultFrameId;
                CloseReason = closeReason;
            }

            public TransportId OldTransportId { get; }

            public TransportId NewTransportId { get; }

            public ulong LastResultFrameId { get; }

            public string CloseReason { get; }
        }

        private readonly struct ProbeThenMigrationServerObservation
        {
            public ProbeThenMigrationServerObservation(uint probePayloadBytes, string closeReason)
            {
                ProbePayloadBytes = probePayloadBytes;
                CloseReason = closeReason;
            }

            public uint ProbePayloadBytes { get; }

            public string CloseReason { get; }
        }

        private readonly struct ProbeThenMigrationControlPlaneObservation
        {
            public ProbeThenMigrationControlPlaneObservation(uint probePayloadBytes, TransportId oldTransportId, TransportId newTransportId, ulong lastResultFrameId)
            {
                ProbePayloadBytes = probePayloadBytes;
                OldTransportId = oldTransportId;
                NewTransportId = newTransportId;
                LastResultFrameId = lastResultFrameId;
            }

            public uint ProbePayloadBytes { get; }

            public TransportId OldTransportId { get; }

            public TransportId NewTransportId { get; }

            public ulong LastResultFrameId { get; }
        }
    }
}
