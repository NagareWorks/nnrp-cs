using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Nnrp.Core;
using Nnrp.Server;
using Nnrp.Transport.Tcp;
using Xunit;

namespace Nnrp.Client.Tests
{
    public sealed class TcpLoopbackTests
    {
        [Fact]
        public async Task TcpTransportRunsClientServerLoopback()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();

            try
            {
                var endpoint = (IPEndPoint)listener.LocalEndpoint;
                var serverTask = RunServerAsync(listener, timeout.Token);

                await using var clientTransport = await NnrpTcpMessageTransport.ConnectAsync(
                    IPAddress.Loopback.ToString(),
                    endpoint.Port,
                    timeout.Token);

                var client = new NnrpClient(new ClientProfile(), clientTransport);
                var connectResult = await client.ConnectAsync(requestedSessionId: 41, cancellationToken: timeout.Token);
                NnrpSubmitResult result;
                try
                {
                    result = await client.SubmitAsync(CreateSubmitRequest(frameId: 303), timeout.Token);
                }
                catch
                {
                    if (serverTask.IsFaulted)
                    {
                        await serverTask;
                    }

                    throw;
                }
                var pingElapsed = await client.PingAsync(traceId: 77, cancellationToken: timeout.Token);
                var closeFailure = await client.CloseAsync("tcp loopback done", traceId: 99, cancellationToken: timeout.Token);

                var serverObserved = await serverTask;

                Assert.True(connectResult.IsConnected);
                Assert.Equal(41u, client.NegotiatedSessionId);
                Assert.Equal(41u, serverObserved.SessionId);
                Assert.Equal(303u, result.FrameId);
                Assert.Equal(303u, serverObserved.FrameId);
                Assert.Equal(77ul, serverObserved.PingTraceId);
                Assert.Equal("tcp loopback done", serverObserved.CloseReason);
                Assert.False(closeFailure.IsFailure);
                Assert.True(pingElapsed >= TimeSpan.Zero);
            }
            finally
            {
                listener.Stop();
            }
        }

        [Fact]
        public async Task AutoProbeBootstrapRunsTcpLoopback()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();

            try
            {
                var endpoint = (IPEndPoint)listener.LocalEndpoint;
                var serverTask = RunAutoProbeServerAsync(listener, timeout.Token);
                var binding = NnrpTransportProbeExchange.CreateConnectionBinding(
                    TransportId.Tcp,
                    "tcp",
                    async cancellationToken => await NnrpTcpMessageTransport.ConnectAsync(
                        IPAddress.Loopback.ToString(),
                        endpoint.Port,
                        cancellationToken).ConfigureAwait(false));

                var result = await NnrpClientBootstrapper.ConnectWithAutoProbeAsync(
                    new ClientProfile { TransportPolicy = TransportPolicy.Auto },
                    new[] { binding },
                    new NnrpTransportProbeOptions { WarmupProbeCount = 0, ScoredProbeCount = 1, PayloadBytes = 64 },
                    requestedSessionId: 41,
                    cancellationToken: timeout.Token);

                NnrpSubmitResult submitResult;
                try
                {
                    submitResult = await result.Client.SubmitAsync(CreateSubmitRequest(frameId: 303), timeout.Token);
                }
                catch
                {
                    if (serverTask.IsFaulted)
                    {
                        await serverTask;
                    }

                    throw;
                }

                var pingElapsed = await result.Client.PingAsync(traceId: 77, cancellationToken: timeout.Token);
                var closeFailure = await result.Client.CloseAsync("tcp auto-probe loopback done", traceId: 101, cancellationToken: timeout.Token);

                var serverObserved = await serverTask;

                Assert.True(result.WasProbed);
                Assert.NotNull(result.ProbeSelection);
                Assert.Equal(TransportId.Tcp, result.ProbeSelection.Value.SelectedTransportId);
                Assert.Equal("tcp", result.ProbeSelection.Value.SelectedBindingName);
                Assert.True(result.ConnectResult.IsConnected);
                Assert.Equal(41u, result.Client.NegotiatedSessionId);
                Assert.Equal(41u, serverObserved.SessionId);
                Assert.Equal(303u, submitResult.FrameId);
                Assert.Equal(303u, serverObserved.FrameId);
                Assert.Equal(64u, serverObserved.ProbePayloadBytes);
                Assert.Equal(77ul, serverObserved.PingTraceId);
                Assert.Equal("tcp auto-probe loopback done", serverObserved.CloseReason);
                Assert.False(closeFailure.IsFailure);
                Assert.True(pingElapsed >= TimeSpan.Zero);
            }
            finally
            {
                listener.Stop();
            }
        }

        [Fact]
        public async Task TcpTransportRunsMigrationLoopback()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();

            try
            {
                var endpoint = (IPEndPoint)listener.LocalEndpoint;
                var serverTask = RunMigrationServerAsync(listener, timeout.Token);

                await using var clientTransport = await NnrpTcpMessageTransport.ConnectAsync(
                    IPAddress.Loopback.ToString(),
                    endpoint.Port,
                    timeout.Token);

                var client = new NnrpClient(new ClientProfile(), clientTransport);
                var connectResult = await client.ConnectAsync(requestedSessionId: 41, cancellationToken: timeout.Token);

                SessionMigrateAckMessage ack;
                try
                {
                    ack = await client.MigrateAsync(
                        oldTransportId: TransportId.Tcp,
                        newTransportId: TransportId.Quic,
                        lastResultFrameId: 303,
                        clientMigrateTimestampMicroseconds: 123456789,
                        traceId: 55,
                        cancellationToken: timeout.Token);
                }
                catch
                {
                    if (serverTask.IsFaulted)
                    {
                        await serverTask;
                    }

                    throw;
                }

                var closeFailure = await client.CloseAsync("tcp migration loopback done", traceId: 99, cancellationToken: timeout.Token);
                var serverObserved = await serverTask;

                Assert.True(connectResult.IsConnected);
                Assert.Equal(NnrpHeader.CurrentWireFormat, client.NegotiatedWireFormat);
                Assert.Equal(41u, ack.Header.SessionId);
                Assert.Equal(55ul, ack.Header.TraceId);
                Assert.Equal(0u, ack.Metadata.AcceptCode);
                Assert.Equal(303ul, ack.Metadata.ResumeFromFrameId);
                Assert.Equal(TransportId.Tcp, serverObserved.OldTransportId);
                Assert.Equal(TransportId.Quic, serverObserved.NewTransportId);
                Assert.Equal(303ul, serverObserved.LastResultFrameId);
                Assert.Equal(55ul, serverObserved.MigrateTraceId);
                Assert.Equal("tcp migration loopback done", serverObserved.CloseReason);
                Assert.False(closeFailure.IsFailure);
            }
            finally
            {
                listener.Stop();
            }
        }

        private static async Task<ServerObservation> RunServerAsync(TcpListener listener, CancellationToken cancellationToken)
        {
            try
            {
                using var tcpClient = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
                await using var transport = new NnrpTcpMessageTransport(tcpClient);
                return await RunSessionAsync(transport, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Server loopback failed.", ex);
            }
        }

        private static async Task<ServerObservation> RunAutoProbeServerAsync(TcpListener listener, CancellationToken cancellationToken)
        {
            try
            {
                using (var probeClient = await listener.AcceptTcpClientAsync().ConfigureAwait(false))
                {
                    await using var probeTransport = new NnrpTcpMessageTransport(probeClient);
                    var probeFrame = await probeTransport.ReceiveAsync(cancellationToken).ConfigureAwait(false);
                    Assert.True(TransportProbeMessage.TryParse(probeFrame, out var probe, out var probeError));
                    Assert.Equal(NnrpParseError.None, probeError);

                    var ack = new TransportProbeAckMessage(
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
                        new TransportProbeAckMetadata(probe.Metadata.ProbeId, 0, probe.Metadata.ClientSendTimestampMicroseconds + 100));
                    await probeTransport.SendAsync(ack.ToFramedMessage(), cancellationToken).ConfigureAwait(false);
                }

                using var tcpClient = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
                await using var transport = new NnrpTcpMessageTransport(tcpClient);
                var sessionObservation = await RunSessionAsync(transport, cancellationToken).ConfigureAwait(false);
                return new ServerObservation(
                    sessionObservation.SessionId,
                    sessionObservation.FrameId,
                    sessionObservation.PingTraceId,
                    sessionObservation.CloseReason,
                    probePayloadBytes: 64);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Auto-probe server loopback failed.", ex);
            }
        }

        private static async Task<MigrationObservation> RunMigrationServerAsync(TcpListener listener, CancellationToken cancellationToken)
        {
            try
            {
                using var tcpClient = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
                await using var transport = new NnrpTcpMessageTransport(tcpClient);
                var session = new NnrpServerSession(new ServerProfile(), transport);

                var acceptFailure = await session.AcceptAsync(cancellationToken).ConfigureAwait(false);
                Assert.False(acceptFailure.IsFailure);
                Assert.Equal(NnrpHeader.CurrentWireFormat, session.NegotiatedWireFormat);

                var migrate = await session.ReceiveSessionMigrateAsync(cancellationToken).ConfigureAwait(false);
                var ack = new SessionMigrateAckMessage(
                    new NnrpHeader(
                        NnrpHeader.CurrentVersionMajor,
                        NnrpHeader.CurrentWireFormat,
                        MessageType.SessionMigrateAck,
                        HeaderFlags.None,
                        SessionMigrateAckMetadata.MetadataLength,
                        0,
                        session.SessionId,
                        0,
                        0,
                        0,
                        migrate.Header.TraceId),
                    new SessionMigrateAckMetadata(
                        acceptCode: 0,
                        resumeFromFrameId: migrate.Metadata.LastResultFrameId,
                        graceWindowMilliseconds: 250,
                        serverMigrateTimestampMicroseconds: migrate.Metadata.ClientMigrateTimestampMicroseconds + 100));
                await session.SendSessionMigrateAckAsync(ack, cancellationToken).ConfigureAwait(false);

                var closeFrame = await transport.ReceiveAsync(cancellationToken).ConfigureAwait(false);
                Assert.True(CloseMessage.TryParse(closeFrame.ToArray(), out var close, out var closeError));
                Assert.Equal(NnrpParseError.None, closeError);

                return new MigrationObservation(
                    session.SessionId,
                    migrate.Metadata.OldTransportId,
                    migrate.Metadata.NewTransportId,
                    migrate.Metadata.LastResultFrameId,
                    migrate.Header.TraceId,
                    close.Reason);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Migration server loopback failed.", ex);
            }
        }

        private static async Task<ServerObservation> RunSessionAsync(NnrpTcpMessageTransport transport, CancellationToken cancellationToken)
        {
            var session = new NnrpServerSession(new ServerProfile(), transport);

            var acceptFailure = await session.AcceptAsync(cancellationToken).ConfigureAwait(false);
            Assert.False(acceptFailure.IsFailure);

            var submit = await session.ReceiveFrameSubmitAsync(cancellationToken).ConfigureAwait(false);
            var result = CreateResult(submit.Header.FrameId);
            await session.SendResultAsync(result, cancellationToken).ConfigureAwait(false);

            var pingFrame = await transport.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            Assert.True(PingMessage.TryParse(pingFrame.ToArray(), out var ping, out var pingError));
            Assert.Equal(NnrpParseError.None, pingError);
            await transport.SendAsync(
                PongMessage.Create(session.SessionId, ping.Header.TraceId).ToFramedMessage(),
                cancellationToken).ConfigureAwait(false);

            var closeFrame = await transport.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            Assert.True(CloseMessage.TryParse(closeFrame.ToArray(), out var close, out var closeError));
            Assert.Equal(NnrpParseError.None, closeError);

            return new ServerObservation(session.SessionId, submit.Header.FrameId, ping.Header.TraceId, close.Reason);
        }

        private static NnrpResult CreateResult(uint frameId)
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
            return new NnrpResult(
                frameId: frameId,
                tileIds: new ushort[] { 0, 1, 2 },
                sections: new[] { section },
                activeProfileId: 1,
                inferenceMilliseconds: 2,
                queueMilliseconds: 1,
                serverTotalMilliseconds: 4,
                tileBaseId: 0,
                payloadKindBitmap: PayloadKind.Tensor,
                payloadFrameCount: 0);
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

        private readonly struct ServerObservation
        {
            public ServerObservation(uint sessionId, uint frameId, ulong pingTraceId, string closeReason, uint probePayloadBytes = 0)
            {
                SessionId = sessionId;
                FrameId = frameId;
                PingTraceId = pingTraceId;
                CloseReason = closeReason;
                ProbePayloadBytes = probePayloadBytes;
            }

            public uint SessionId { get; }

            public uint FrameId { get; }

            public ulong PingTraceId { get; }

            public string CloseReason { get; }

            public uint ProbePayloadBytes { get; }
        }

        private readonly struct MigrationObservation
        {
            public MigrationObservation(
                uint sessionId,
                TransportId oldTransportId,
                TransportId newTransportId,
                ulong lastResultFrameId,
                ulong migrateTraceId,
                string closeReason)
            {
                SessionId = sessionId;
                OldTransportId = oldTransportId;
                NewTransportId = newTransportId;
                LastResultFrameId = lastResultFrameId;
                MigrateTraceId = migrateTraceId;
                CloseReason = closeReason;
            }

            public uint SessionId { get; }

            public TransportId OldTransportId { get; }

            public TransportId NewTransportId { get; }

            public ulong LastResultFrameId { get; }

            public ulong MigrateTraceId { get; }

            public string CloseReason { get; }
        }
    }
}
