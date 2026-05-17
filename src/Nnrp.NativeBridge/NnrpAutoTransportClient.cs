using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Nnrp.Client;
using Nnrp.Core;
using Nnrp.Transport.Tcp;

namespace Nnrp.NativeBridge
{
    internal interface INnrpAutoTransportRuntime
    {
        NnrpQuicClient CreateQuicClient(NnrpQuicClientOptions options);

        byte[] ProbeQuic(string host, ushort port, string tlsServerName, byte[] probePacket);
    }

    internal sealed class NnrpAutoTransportRuntime : INnrpAutoTransportRuntime
    {
        public static NnrpAutoTransportRuntime Instance { get; } = new NnrpAutoTransportRuntime();

        private NnrpAutoTransportRuntime()
        {
        }

        public NnrpQuicClient CreateQuicClient(NnrpQuicClientOptions options)
        {
            return new NnrpQuicClient(options);
        }

        public byte[] ProbeQuic(string host, ushort port, string tlsServerName, byte[] probePacket)
        {
            return NnrpNativeQuicClient.Probe(host, port, tlsServerName, probePacket);
        }
    }

    public readonly struct NnrpAutoTransportClientOptions
    {
        public NnrpAutoTransportClientOptions(
            string host,
            ushort port,
            string tlsServerName,
            string requestedModel,
            uint requestedSessionId = 11,
            ushort tcpPort = 0)
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
            TcpPort = tcpPort == 0 ? port : tcpPort;
            TlsServerName = tlsServerName;
            RequestedModel = requestedModel;
            RequestedSessionId = requestedSessionId;
        }

        public string Host { get; }

        public ushort Port { get; }

        public ushort TcpPort { get; }

        public string TlsServerName { get; }

        public string RequestedModel { get; }

        public uint RequestedSessionId { get; }

        public byte RequestedWireFormat => NnrpHeader.CurrentWireFormat;
    }

    public readonly struct NnrpAutoTransportConnectResult
    {
        public NnrpAutoTransportConnectResult(
            TransportId selectedTransportId,
            string selectedBindingName,
            uint negotiatedSessionId,
            string activeModelName,
            NnrpTransportProbeSelectionResult? probeSelection,
            bool wasProbed)
        {
            SelectedTransportId = selectedTransportId;
            SelectedBindingName = selectedBindingName ?? string.Empty;
            NegotiatedSessionId = negotiatedSessionId;
            ActiveModelName = activeModelName ?? string.Empty;
            ProbeSelection = probeSelection;
            WasProbed = wasProbed;
        }

        public TransportId SelectedTransportId { get; }

        public string SelectedBindingName { get; }

        public uint NegotiatedSessionId { get; }

        public byte NegotiatedWireFormat => NnrpHeader.CurrentWireFormat;

        public string ActiveModelName { get; }

        public NnrpTransportProbeSelectionResult? ProbeSelection { get; }

        public bool WasProbed { get; }
    }

    public sealed class NnrpAutoTransportClient : IDisposable, IAsyncDisposable
    {
        private readonly ClientProfile profile;
        private readonly NnrpAutoTransportClientOptions options;
        private readonly INnrpAutoTransportRuntime runtime;
        private NnrpQuicClient? quicClient;
        private NnrpTcpMessageTransport? tcpTransport;
        private bool disposed;

        public NnrpAutoTransportClient(ClientProfile profile, NnrpAutoTransportClientOptions options)
            : this(profile, options, NnrpAutoTransportRuntime.Instance)
        {
        }

        internal NnrpAutoTransportClient(
            ClientProfile profile,
            NnrpAutoTransportClientOptions options,
            INnrpAutoTransportRuntime runtime)
        {
            this.profile = CloneProfile(profile ?? throw new ArgumentNullException(nameof(profile)));
            this.options = options;
            this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            ActiveModelName = string.Empty;
            SelectedBindingName = string.Empty;
        }

        public bool IsConnected => SelectedTransportId != TransportId.Unspecified;

        public TransportId SelectedTransportId { get; private set; }

        public string SelectedBindingName { get; private set; }

        public uint NegotiatedSessionId { get; private set; }

        public byte NegotiatedWireFormat => NnrpHeader.CurrentWireFormat;

        public string ActiveModelName { get; private set; }

        public NnrpTransportProbeSelectionResult? ProbeSelection { get; private set; }

        public async ValueTask<NnrpAutoTransportConnectResult> ConnectAsync(
            NnrpTransportProbeOptions probeOptions,
            ulong traceId = 0,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (IsConnected)
            {
                throw new InvalidOperationException("Auto-transport client is already connected.");
            }

            var decision = NnrpTransportPolicyHelper.ResolveSelectionDecision(profile.TransportPolicy);
            var bindings = CreateProbeBindings(traceId);
            TransportId selectedTransportId;
            string selectedBindingName;
            NnrpTransportProbeSelectionResult? probeSelection = null;

            if (decision.ShouldProbe)
            {
                probeSelection = await NnrpTransportProbeOrchestrator.ProbeAsync(bindings, probeOptions, cancellationToken).ConfigureAwait(false);
                selectedTransportId = probeSelection.Value.SelectedTransportId;
                selectedBindingName = probeSelection.Value.SelectedBindingName;
            }
            else
            {
                selectedTransportId = decision.PreferredTransportId;
                selectedBindingName = selectedTransportId == TransportId.Quic ? "quic" : "tcp";
            }

            if (selectedTransportId == TransportId.Quic)
            {
                var quicOptions = new NnrpQuicClientOptions(
                    options.Host,
                    options.Port,
                    options.TlsServerName,
                    options.RequestedModel,
                    options.RequestedSessionId);
                quicClient = runtime.CreateQuicClient(quicOptions);
                NnrpNativeQuicClient.OpenResult openResult;
                try
                {
                    openResult = await RunBlockingNativeCallAsync(
                        () => quicClient.Connect(),
                        cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    quicClient.Dispose();
                    quicClient = null;
                    throw;
                }

                SelectedTransportId = TransportId.Quic;
                SelectedBindingName = selectedBindingName;
                NegotiatedSessionId = openResult.NegotiatedSessionId;
                ActiveModelName = string.IsNullOrWhiteSpace(openResult.ActiveModelName)
                    ? options.RequestedModel
                    : openResult.ActiveModelName;
                ProbeSelection = probeSelection;
                return new NnrpAutoTransportConnectResult(
                    SelectedTransportId,
                    SelectedBindingName,
                    NegotiatedSessionId,
                    ActiveModelName,
                    ProbeSelection,
                    wasProbed: probeSelection.HasValue);
            }

            tcpTransport = await NnrpTcpMessageTransport.ConnectAsync(options.Host, options.TcpPort, cancellationToken).ConfigureAwait(false);
            try
            {
                var hello = CreateHello(selectedTransportId, traceId);
                await tcpTransport.SendAsync(hello.ToFramedMessage(), cancellationToken).ConfigureAwait(false);
                var response = await tcpTransport.ReceiveAsync(cancellationToken).ConfigureAwait(false);
                ApplyTcpConnectResponse(response);
            }
            catch
            {
                await DisposeTcpTransportAsync().ConfigureAwait(false);
                throw;
            }

            SelectedTransportId = TransportId.Tcp;
            SelectedBindingName = selectedBindingName;
            ProbeSelection = probeSelection;
            return new NnrpAutoTransportConnectResult(
                SelectedTransportId,
                SelectedBindingName,
                NegotiatedSessionId,
                ActiveModelName,
                ProbeSelection,
                wasProbed: probeSelection.HasValue);
        }

        public async ValueTask<byte[]> SubmitAsync(byte[] submitPacket, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            EnsureConnected();
            if (submitPacket == null || submitPacket.Length == 0)
            {
                throw new ArgumentException("Submit packet must not be empty.", nameof(submitPacket));
            }

            if (SelectedTransportId == TransportId.Quic)
            {
                return quicClient!.SubmitPacket(submitPacket);
            }

            var framed = ParsePacket(submitPacket, "FRAME_SUBMIT");
            await tcpTransport!.SendAsync(framed, cancellationToken).ConfigureAwait(false);
            return (await tcpTransport.ReceiveAsync(cancellationToken).ConfigureAwait(false)).ToArray();
        }

        public async ValueTask<NnrpSubmitResult> SubmitAsync(
            NnrpSubmitRequest submitRequest,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            EnsureConnected();
            cancellationToken.ThrowIfCancellationRequested();

            var submitPacket = CreateSubmitPacket(submitRequest);
            byte[] responsePacket;
            if (SelectedTransportId == TransportId.Quic)
            {
                responsePacket = await RunBlockingNativeCallAsync(
                    () => quicClient!.SubmitPacket(submitPacket),
                    cancellationToken).ConfigureAwait(false);
            }
            else
            {
                var framed = ParsePacket(submitPacket, "FRAME_SUBMIT");
                await tcpTransport!.SendAsync(framed, cancellationToken).ConfigureAwait(false);
                responsePacket = (await tcpTransport.ReceiveAsync(cancellationToken).ConfigureAwait(false)).ToArray();
            }

            return ParseSubmitResult(responsePacket, submitRequest);
        }

        public async ValueTask<byte[]> PingAsync(byte[] pingPacket, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            EnsureConnected();
            if (pingPacket == null || pingPacket.Length == 0)
            {
                throw new ArgumentException("Ping packet must not be empty.", nameof(pingPacket));
            }

            if (SelectedTransportId == TransportId.Quic)
            {
                return quicClient!.PingPacket(pingPacket);
            }

            var framed = ParsePacket(pingPacket, "PING");
            await tcpTransport!.SendAsync(framed, cancellationToken).ConfigureAwait(false);
            return (await tcpTransport.ReceiveAsync(cancellationToken).ConfigureAwait(false)).ToArray();
        }

        public async ValueTask<FlowUpdateMessage> ReceiveFlowUpdateAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            EnsureConnected();
            cancellationToken.ThrowIfCancellationRequested();

            if (SelectedTransportId == TransportId.Quic)
            {
                throw new InvalidOperationException("Current native QUIC bridge does not expose an unsolicited control-message receive path for FLOW_UPDATE.");
            }

            var response = await tcpTransport!.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            if (!FlowUpdateMessage.TryParse(response, out var flowUpdate, out var parseError))
            {
                throw new InvalidOperationException(
                    $"Expected FLOW_UPDATE on TCP control channel, received {response.Header.MessageType} ({parseError}).");
            }

            if (NegotiatedSessionId != 0
                && flowUpdate.Header.SessionId != 0
                && flowUpdate.Header.SessionId != NegotiatedSessionId)
            {
                throw new InvalidOperationException(
                    $"FLOW_UPDATE session_id {flowUpdate.Header.SessionId} does not match negotiated session_id {NegotiatedSessionId}.");
            }

            return flowUpdate;
        }

        public async ValueTask<ResultHintMessage> ReceiveResultHintAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            EnsureConnected();
            cancellationToken.ThrowIfCancellationRequested();

            if (SelectedTransportId == TransportId.Quic)
            {
                throw new InvalidOperationException("Current native QUIC bridge does not expose an unsolicited control-message receive path for RESULT_HINT.");
            }

            var response = await tcpTransport!.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            if (!ResultHintMessage.TryParse(response, out var resultHint, out var parseError))
            {
                throw new InvalidOperationException(
                    $"Expected RESULT_HINT on TCP control channel, received {response.Header.MessageType} ({parseError}).");
            }

            if (NegotiatedSessionId != 0 && resultHint.Header.SessionId != NegotiatedSessionId)
            {
                throw new InvalidOperationException(
                    $"RESULT_HINT session_id {resultHint.Header.SessionId} does not match negotiated session_id {NegotiatedSessionId}.");
            }

            return resultHint;
        }

        public async ValueTask CancelAsync(byte[] cancelPacket, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            EnsureConnected();
            if (cancelPacket == null || cancelPacket.Length == 0)
            {
                throw new ArgumentException("Cancel packet must not be empty.", nameof(cancelPacket));
            }

            if (SelectedTransportId == TransportId.Quic)
            {
                quicClient!.CancelPacket(cancelPacket);
                return;
            }

            var framed = ParsePacket(cancelPacket, "FRAME_CANCEL");
            await tcpTransport!.SendAsync(framed, cancellationToken).ConfigureAwait(false);
        }

        public async ValueTask<SessionMigrateAckMessage> MigrateAsync(
            TransportId targetTransportId,
            ulong lastResultFrameId,
            ulong clientMigrateTimestampMicroseconds,
            ulong traceId = 0,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            EnsureConnected();
            cancellationToken.ThrowIfCancellationRequested();

            if (targetTransportId == TransportId.Unspecified)
            {
                throw new ArgumentOutOfRangeException(nameof(targetTransportId), "Target transport must be specified.");
            }

            if (targetTransportId == SelectedTransportId)
            {
                throw new InvalidOperationException("Target transport must differ from the currently selected transport.");
            }

            NnrpQuicClient? preparedQuicClient = null;
            NnrpTcpMessageTransport? preparedTcpTransport = null;
            try
            {
                if (targetTransportId == TransportId.Quic)
                {
                    preparedQuicClient = await PrepareQuicMigrationClientAsync(cancellationToken).ConfigureAwait(false);
                }
                else if (targetTransportId == TransportId.Tcp)
                {
                    preparedTcpTransport = await NnrpTcpMessageTransport.ConnectAsync(options.Host, options.TcpPort, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    throw new InvalidOperationException($"Unsupported target transport {targetTransportId}.");
                }

                var migrate = CreateSessionMigrateMessage(targetTransportId, lastResultFrameId, clientMigrateTimestampMicroseconds, traceId);
                var responsePacket = await SendMigrationPacketAsync(migrate.ToArray(), cancellationToken).ConfigureAwait(false);
                var ack = ParseSessionMigrateAck(responsePacket, traceId);
                if (ack.Metadata.AcceptCode != 0)
                {
                    throw new InvalidOperationException($"SESSION_MIGRATE rejected with accept_code {ack.Metadata.AcceptCode}.");
                }

                if (targetTransportId == TransportId.Quic)
                {
                    quicClient = preparedQuicClient;
                    preparedQuicClient = null;
                    await DisposeTcpTransportAsync().ConfigureAwait(false);
                    SelectedTransportId = TransportId.Quic;
                    SelectedBindingName = "quic";
                }
                else
                {
                    quicClient?.Dispose();
                    quicClient = null;
                    tcpTransport = preparedTcpTransport;
                    preparedTcpTransport = null;
                    SelectedTransportId = TransportId.Tcp;
                    SelectedBindingName = "tcp";
                }

                return ack;
            }
            catch
            {
                preparedQuicClient?.Dispose();
                if (preparedTcpTransport != null)
                {
                    await preparedTcpTransport.DisposeAsync().ConfigureAwait(false);
                }

                throw;
            }
        }

        public async ValueTask CloseAsync(string reason = "auto-transport-client", ulong traceId = 0, CancellationToken cancellationToken = default)
        {
            if (!IsConnected)
            {
                return;
            }

            if (SelectedTransportId == TransportId.Quic)
            {
                quicClient?.Close();
                quicClient?.Dispose();
                quicClient = null;
            }
            else if (tcpTransport != null)
            {
                var close = CloseMessage.Create(NegotiatedSessionId, reason ?? string.Empty, traceId);
                try
                {
                    await tcpTransport.SendAsync(close.ToFramedMessage(), cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    // Best-effort close for local teardown.
                }

                await DisposeTcpTransportAsync().ConfigureAwait(false);
            }

            ResetState();
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            quicClient?.Dispose();
            quicClient = null;
            tcpTransport?.Dispose();
            tcpTransport = null;
            ResetState();
        }

        public async ValueTask DisposeAsync()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            quicClient?.Dispose();
            quicClient = null;
            await DisposeTcpTransportAsync().ConfigureAwait(false);
            ResetState();
        }

        private NnrpTransportProbeBinding[] CreateProbeBindings(ulong traceId)
        {
            return new[]
            {
                new NnrpTransportProbeBinding(
                    TransportId.Quic,
                    "quic",
                    (request, cancellationToken) => ProbeQuicAsync(request, traceId, cancellationToken)),
                new NnrpTransportProbeBinding(
                    TransportId.Tcp,
                    "tcp",
                    (request, cancellationToken) => ProbeTcpAsync(request, traceId, cancellationToken)),
            };
        }

        private async ValueTask<NnrpTransportProbeSampleResult> ProbeTcpAsync(
            NnrpTransportProbeRequest request,
            ulong traceId,
            CancellationToken cancellationToken)
        {
            var transport = await NnrpTcpMessageTransport.ConnectAsync(options.Host, options.TcpPort, cancellationToken).ConfigureAwait(false);
            try
            {
                return await NnrpTransportProbeExchange.ProbeAsync(
                    TransportId.Tcp,
                    "tcp",
                    transport,
                    request,
                    traceId,
                    cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                await transport.DisposeAsync().ConfigureAwait(false);
            }
        }

        private async ValueTask<NnrpTransportProbeSampleResult> ProbeQuicAsync(
            NnrpTransportProbeRequest request,
            ulong traceId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var payload = new byte[request.PayloadBytes];
            for (var i = 0; i < payload.Length; i++)
            {
                payload[i] = unchecked((byte)(request.SampleIndex + i));
            }

            var probeId = checked((uint)request.SampleIndex);
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
                    GetUtcMicroseconds()),
                payload);

            var stopwatch = Stopwatch.StartNew();
            var response = await RunBlockingNativeCallAsync(
                    () => runtime.ProbeQuic(
                    options.Host,
                    options.Port,
                    options.TlsServerName,
                    probe.ToArray()),
                cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();

            if (!TransportProbeAckMessage.TryParse(response, out var ack, out var parseError))
            {
                throw new InvalidOperationException($"Expected TRANSPORT_PROBE_ACK after TRANSPORT_PROBE, received parse error {parseError}.");
            }

            if (ack.Metadata.ProbeId != probeId)
            {
                throw new InvalidOperationException("TRANSPORT_PROBE_ACK correlation mismatch.");
            }

            return new NnrpTransportProbeSampleResult(
                TransportId.Quic,
                "quic",
                isSuccess: true,
                payloadBytes: payload.Length,
                roundTripMicroseconds: stopwatch.Elapsed.Ticks / 10);
        }

        private ClientHelloMessage CreateHello(TransportId selectedTransportId, ulong traceId)
        {
            var helloProfile = CloneProfile(profile);
            helloProfile.AuthBlockProvider = () => Encoding.UTF8.GetBytes(options.RequestedModel);
            return helloProfile.CreateClientHello(
                options.RequestedSessionId,
                traceId,
                profile.TransportPolicy,
                selectedTransportId);
        }

        private byte[] CreateSubmitPacket(NnrpSubmitRequest submitRequest)
        {
            return CreateFrameSubmit(submitRequest).ToArray();
        }

        private NnrpSubmitResult ParseSubmitResult(byte[] responsePacket, NnrpSubmitRequest submitRequest)
        {
            if (!ResultPushMessage.TryParse(responsePacket, out var resultMessage, out var parseError))
            {
                throw new InvalidOperationException($"Failed to parse RESULT_PUSH packet ({parseError}).");
            }

            if (resultMessage.Header.SessionId != NegotiatedSessionId)
            {
                throw new InvalidOperationException(
                    $"RESULT_PUSH session_id {resultMessage.Header.SessionId} does not match negotiated session_id {NegotiatedSessionId}.");
            }

            if (resultMessage.Header.FrameId != submitRequest.FrameId || resultMessage.Header.ViewId != submitRequest.ViewId)
            {
                throw new InvalidOperationException(
                    $"RESULT_PUSH correlation mismatch: frame_id={resultMessage.Header.FrameId}, view_id={resultMessage.Header.ViewId}.");
            }

            return new NnrpSubmitResult(
                resultMessage.Header.SessionId,
                resultMessage.Header.FrameId,
                resultMessage.Header.ViewId,
                resultMessage.Metadata.StatusCode,
                resultMessage.Metadata.ResultFlags,
                resultMessage.Metadata.ActiveProfileId,
                resultMessage.Metadata.InferenceMilliseconds,
                resultMessage.Metadata.QueueMilliseconds,
                resultMessage.Metadata.ServerTotalMilliseconds,
                resultMessage.TileIds,
                resultMessage.Sections,
                resultMessage.Metadata.ResultClass,
                resultMessage.Metadata.AppliedBudgetPolicy,
                resultMessage.Metadata.ReusedFrameId,
                resultMessage.Metadata.CoveredTileCount,
                resultMessage.Metadata.DroppedTileCount,
                resultMessage.Metadata.PayloadKindBitmap,
                resultMessage.Metadata.PayloadFrameCount,
                resultMessage.TypedPayloadFrames);
        }

        private NnrpFramedMessage CreateFrameSubmit(NnrpSubmitRequest submitRequest)
        {
            var tileIndexBytes = TileIndexBlockCodec.GetEncodedLength(submitRequest.TileIds.Span, submitRequest.TileIndexMode);
            var metadata = new FrameSubmitMetadata(
                sourceWidth: submitRequest.SourceWidth,
                sourceHeight: submitRequest.SourceHeight,
                tileWidth: submitRequest.TileWidth,
                tileHeight: submitRequest.TileHeight,
                tileCount: checked((ushort)submitRequest.TileIds.Length),
                sectionCount: checked((ushort)submitRequest.Sections.Length),
                frameClass: submitRequest.FrameClass,
                inputProfile: submitRequest.InputProfile,
                tileIndexMode: submitRequest.TileIndexMode,
                reserved0: 0,
                latencyBudgetMilliseconds: submitRequest.LatencyBudgetMilliseconds,
                targetFpsTimes100: submitRequest.CadenceHintX100,
                retryOfFrame: submitRequest.DependencyFrameId,
                tileBaseId: submitRequest.TileBaseId,
                cameraBytes: checked((uint)submitRequest.CameraBlock.Length),
                tileIndexBytes: checked((uint)tileIndexBytes),
                reserved1: 0,
                reserved2: 0,
                submitMode: SubmitMode.Inline,
                budgetPolicy: BudgetPolicy.None,
                lossTolerancePolicy: 0xFF,
                reserved3: 0,
                objectRefMask: 0,
                dependencyFrameId: submitRequest.DependencyFrameId,
                payloadKindBitmap: PayloadKind.Tensor,
                payloadFrameCount: 0,
                reserved4: 0);
            var header = new NnrpHeader(
                versionMajor: NnrpHeader.CurrentVersionMajor,
                messageType: MessageType.FrameSubmit,
                flags: HeaderFlags.None,
                metaLength: FrameSubmitMetadata.MetadataLength,
                bodyLength: ComputeFrameSubmitBodyLength(
                    submitRequest.CameraBlock.Length,
                    tileIndexBytes,
                    submitRequest.Sections.Span),
                sessionId: NegotiatedSessionId,
                frameId: submitRequest.FrameId,
                viewId: submitRequest.ViewId,
                routeId: 0,
                traceId: submitRequest.TraceId);
            var body = BuildInlineTensorSubmitBody(
                submitRequest.CameraBlock.Span,
                submitRequest.TileIds.Span,
                submitRequest.TileIndexMode,
                submitRequest.TileBaseId,
                submitRequest.Sections.Span,
                checked((int)header.BodyLength));
            return new NnrpFramedMessage(header, metadata.ToArray(), body);
        }

        private static byte[] BuildInlineTensorSubmitBody(
            ReadOnlySpan<byte> cameraBlock,
            ReadOnlySpan<ushort> tileIds,
            TileIndexMode tileIndexMode,
            uint tileBaseId,
            ReadOnlySpan<TensorSectionBlock> sections,
            int bodyLength)
        {
            var tileIndexBlock = TileIndexBlockCodec.Encode(tileIds, tileIndexMode, tileBaseId);
            var body = new byte[bodyLength];
            var offset = 0;

            cameraBlock.CopyTo(body.AsSpan(offset, cameraBlock.Length));
            offset += cameraBlock.Length;
            offset = BinaryAlignment.AlignUp(offset, 8);

            tileIndexBlock.CopyTo(body.AsSpan(offset, tileIndexBlock.Length));
            offset += tileIndexBlock.Length;

            foreach (var section in sections)
            {
                offset = BinaryAlignment.AlignUp(offset, 8);
                if (!section.TryCopyTo(body.AsSpan(offset), out var sectionBytes))
                {
                    throw new InvalidOperationException("Tensor section serialization failed.");
                }

                offset += sectionBytes;
            }

            return body;
        }

        private static uint ComputeFrameSubmitBodyLength(
            int cameraBytes,
            int tileIndexBytes,
            ReadOnlySpan<TensorSectionBlock> sections)
        {
            var runningTotal = BinaryAlignment.AlignUp(TensorSubmitBlock.BlockLength + cameraBytes, BinaryAlignment.DefaultAlignment);
            runningTotal = BinaryAlignment.AlignUp(runningTotal + tileIndexBytes, BinaryAlignment.DefaultAlignment);
            foreach (var section in sections)
            {
                runningTotal = BinaryAlignment.AlignUp(runningTotal, BinaryAlignment.DefaultAlignment);
                runningTotal += section.TotalLength;
            }

            return checked((uint)runningTotal);
        }

        private SessionMigrateMessage CreateSessionMigrateMessage(
            TransportId targetTransportId,
            ulong lastResultFrameId,
            ulong clientMigrateTimestampMicroseconds,
            ulong traceId)
        {
            return new SessionMigrateMessage(
                new NnrpHeader(
                    versionMajor: NnrpHeader.CurrentVersionMajor,
                    messageType: MessageType.SessionMigrate,
                    flags: HeaderFlags.None,
                    metaLength: SessionMigrateMetadata.MetadataLength,
                    bodyLength: 0,
                    sessionId: NegotiatedSessionId,
                    frameId: 0,
                    viewId: 0,
                    routeId: 0,
                    traceId: traceId),
                new SessionMigrateMetadata(
                    SelectedTransportId,
                    targetTransportId,
                    lastResultFrameId,
                    clientMigrateTimestampMicroseconds));
        }

        private async ValueTask<NnrpQuicClient> PrepareQuicMigrationClientAsync(CancellationToken cancellationToken)
        {
            var quicOptions = new NnrpQuicClientOptions(
                options.Host,
                options.Port,
                options.TlsServerName,
                string.IsNullOrWhiteSpace(ActiveModelName) ? options.RequestedModel : ActiveModelName,
                requestedSessionId: NegotiatedSessionId);
            var preparedClient = runtime.CreateQuicClient(quicOptions);
            try
            {
                var openResult = await RunBlockingNativeCallAsync(
                    () => preparedClient.Connect(),
                    cancellationToken).ConfigureAwait(false);
                if (openResult.NegotiatedSessionId != NegotiatedSessionId)
                {
                    throw new InvalidOperationException(
                        $"Migrated QUIC connection negotiated session_id {openResult.NegotiatedSessionId} instead of existing session_id {NegotiatedSessionId}.");
                }

                return preparedClient;
            }
            catch
            {
                preparedClient.Dispose();
                throw;
            }
        }

        private async ValueTask<byte[]> SendMigrationPacketAsync(byte[] packet, CancellationToken cancellationToken)
        {
            if (SelectedTransportId == TransportId.Quic)
            {
                return quicClient!.SubmitPacket(packet);
            }

            var framed = ParsePacket(packet, "SESSION_MIGRATE");
            await tcpTransport!.SendAsync(framed, cancellationToken).ConfigureAwait(false);
            return (await tcpTransport.ReceiveAsync(cancellationToken).ConfigureAwait(false)).ToArray();
        }

        private SessionMigrateAckMessage ParseSessionMigrateAck(byte[] packet, ulong expectedTraceId)
        {
            if (!SessionMigrateAckMessage.TryParse(packet, out var ack, out var parseError))
            {
                throw new InvalidOperationException($"Failed to parse SESSION_MIGRATE_ACK packet ({parseError}).");
            }

            if (ack.Header.SessionId != NegotiatedSessionId || ack.Header.TraceId != expectedTraceId)
            {
                throw new InvalidOperationException(
                    $"SESSION_MIGRATE_ACK correlation mismatch: session_id={ack.Header.SessionId}, trace_id={ack.Header.TraceId}.");
            }

            return ack;
        }

        private void ApplyTcpConnectResponse(NnrpFramedMessage response)
        {
            if (response.Header.MessageType == MessageType.Error)
            {
                if (ErrorMessage.TryParse(response.ToArray(), out var error, out var parseError))
                {
                    throw new InvalidOperationException($"Server returned ERROR during connect: {error.DiagnosticText}");
                }

                throw new InvalidOperationException($"Failed to parse ERROR response during connect ({parseError}).");
            }

            if (response.Header.MessageType != MessageType.ServerHelloAck)
            {
                throw new InvalidOperationException($"Expected SERVER_HELLO_ACK during connect, received {response.Header.MessageType}.");
            }

            if (!ServerHelloAckMetadata.TryParse(response.Metadata.Span, out var metadata, out var parseErrorMetadata))
            {
                throw new InvalidOperationException($"Failed to parse SERVER_HELLO_ACK metadata ({parseErrorMetadata}).");
            }

            NegotiatedSessionId = metadata.SessionId;
            if (metadata.SelectedWireFormat != NnrpHeader.CurrentWireFormat)
            {
                throw new InvalidOperationException(
                    $"SERVER_HELLO_ACK selected unsupported wire format {metadata.SelectedWireFormat}.");
            }
            ActiveModelName = response.Body.IsEmpty
                ? options.RequestedModel
                : Encoding.UTF8.GetString(response.Body.Span).Trim();
            if (string.IsNullOrWhiteSpace(ActiveModelName))
            {
                ActiveModelName = options.RequestedModel;
            }
        }

        private static NnrpFramedMessage ParsePacket(byte[] packet, string operationName)
        {
            if (!NnrpFramedMessage.TryParse(packet, NnrpHeaderParseOptions.Strict, out var framed, out var parseError))
            {
                throw new InvalidOperationException($"Failed to parse {operationName} packet ({parseError}).");
            }

            return framed;
        }

        private static ClientProfile CloneProfile(ClientProfile source)
        {
            return new ClientProfile
            {
                TransportProfile = source.TransportProfile,
                TransportPolicy = source.TransportPolicy,
                SessionLossTolerance = source.SessionLossTolerance,
                MaxViews = source.MaxViews,
                EnableCache = source.EnableCache,
                MaxCacheEntries = source.MaxCacheEntries,
                SupportedCodecs = (CodecId[])(source.SupportedCodecs?.Clone() ?? Array.Empty<CodecId>()),
                SupportedDTypes = (DTypeId[])(source.SupportedDTypes?.Clone() ?? Array.Empty<DTypeId>()),
                SupportedTensorLayouts = (TensorLayoutId[])(source.SupportedTensorLayouts?.Clone() ?? Array.Empty<TensorLayoutId>()),
                PreferredTileWidth = source.PreferredTileWidth,
                PreferredTileHeight = source.PreferredTileHeight,
                MinSourceWidth = source.MinSourceWidth,
                MaxSourceWidth = source.MaxSourceWidth,
                MinSourceHeight = source.MinSourceHeight,
                MaxSourceHeight = source.MaxSourceHeight,
                MinTargetFpsTimes100 = source.MinTargetFpsTimes100,
                MaxTargetFpsTimes100 = source.MaxTargetFpsTimes100,
                LatencyBudgetMilliseconds = source.LatencyBudgetMilliseconds,
                AuthBlockProvider = source.AuthBlockProvider,
            };
        }

        private async ValueTask DisposeTcpTransportAsync()
        {
            if (tcpTransport == null)
            {
                return;
            }

            await tcpTransport.DisposeAsync().ConfigureAwait(false);
            tcpTransport = null;
        }

        private static async Task<T> RunBlockingNativeCallAsync<T>(
            Func<T> operation,
            CancellationToken cancellationToken)
        {
            if (operation == null)
            {
                throw new ArgumentNullException(nameof(operation));
            }

            cancellationToken.ThrowIfCancellationRequested();
            var task = Task.Run(operation);
            var completed = await Task.WhenAny(task, Task.Delay(Timeout.Infinite, cancellationToken)).ConfigureAwait(false);
            if (!ReferenceEquals(completed, task))
            {
                throw new OperationCanceledException(cancellationToken);
            }

            return await task.ConfigureAwait(false);
        }

        private void ResetState()
        {
            SelectedTransportId = TransportId.Unspecified;
            SelectedBindingName = string.Empty;
            NegotiatedSessionId = 0;
            ActiveModelName = string.Empty;
            ProbeSelection = null;
        }

        private void EnsureConnected()
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Auto-transport client is not connected.");
            }
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(NnrpAutoTransportClient));
            }
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
