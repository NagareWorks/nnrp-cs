using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Nnrp.Core;

namespace Nnrp.Client
{
    public sealed class NnrpClient
    {
        private readonly object gate = new object();
        private readonly Dictionary<NnrpFrameKey, ResultPushMessage> bufferedResults = new Dictionary<NnrpFrameKey, ResultPushMessage>();
        private readonly Queue<FlowUpdateMessage> bufferedFlowUpdates = new Queue<FlowUpdateMessage>();
        private readonly HashSet<NnrpFrameKey> inFlightFrames = new HashSet<NnrpFrameKey>();
        private ulong resumeFromFrameIdFloor;
        private readonly NnrpClientSession session;
        private readonly INnrpMessageTransport transport;

        public NnrpClient(ClientProfile profile, INnrpMessageTransport transport)
        {
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
            session = new NnrpClientSession(profile ?? throw new ArgumentNullException(nameof(profile)));
        }

        public ClientProfile Profile => session.Profile;

        public INnrpClientSession Session => session;

        public uint NegotiatedSessionId { get; private set; }

        public byte NegotiatedWireFormat => NnrpHeader.CurrentWireFormat;

        public int InFlightFrameCount
        {
            get
            {
                lock (gate)
                {
                    return inFlightFrames.Count;
                }
            }
        }

        public int BufferedFlowUpdateCount
        {
            get
            {
                lock (gate)
                {
                    return bufferedFlowUpdates.Count;
                }
            }
        }

        public async ValueTask<NnrpClientConnectResult> ConnectAsync(
            uint requestedSessionId = 0,
            ulong traceId = 0,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var hello = Profile.CreateClientHello(
                requestedSessionId,
                traceId,
                Profile.TransportPolicy,
                preferredTransportId: TransportId.Unspecified);
            return await ConnectAsync(hello, expectedActiveTransportId: null, cancellationToken).ConfigureAwait(false);
        }

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public async ValueTask<NnrpClientConnectResult> ConnectAsync(
            ClientHelloMessage hello,
            TransportId? expectedActiveTransportId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await transport.SendAsync(hello.ToFramedMessage(), cancellationToken).ConfigureAwait(false);

            var response = await transport.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            if (response.Header.MessageType == MessageType.Error)
            {
                var failure = ParseErrorFailure(response);
                return NnrpClientConnectResult.Failed(failure);
            }

            if (!ServerHelloAckMessage.TryParse(response, out var ack, out var parseError))
            {
                return NnrpClientConnectResult.Failed(
                    new NnrpProtocolFailure(
                        ErrorCode.MalformedBody,
                        NnrpErrorScope.Connection,
                        $"Expected SERVER_HELLO_ACK during connect, received {response.Header.MessageType} ({parseError}).",
                        isFatal: true,
                        parseError: parseError));
            }

            if (!ValidateAckWireFormat(hello, ack, out var wireFormatValidationFailure))
            {
                return NnrpClientConnectResult.Failed(wireFormatValidationFailure);
            }

            var result = await session.ConnectAsync(ToServerCapabilities(ack), cancellationToken).ConfigureAwait(false);
            if (result.IsConnected)
            {
                if (!ValidateActiveTransportEcho(ack, expectedActiveTransportId, out var validationFailure))
                {
                    return NnrpClientConnectResult.Failed(validationFailure);
                }

                NegotiatedSessionId = ack.Metadata.SessionId;
                lock (gate)
                {
                    resumeFromFrameIdFloor = 0;
                    inFlightFrames.Clear();
                    bufferedResults.Clear();
                    bufferedFlowUpdates.Clear();
                }
            }

            return result;
        }

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public async ValueTask<ResultPushMessage> SubmitAsync(
            FrameSubmitMessage submitMessage,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await SendSubmitAsync(submitMessage, cancellationToken).ConfigureAwait(false);
            return await ReceiveResultAsync(submitMessage.Header.FrameId, submitMessage.Header.ViewId, cancellationToken).ConfigureAwait(false);
        }

        public ValueTask<ResultPushMessage> SubmitAndWaitAsync(
            FrameSubmitMessage submitMessage,
            CancellationToken cancellationToken = default)
        {
            return SubmitAsync(submitMessage, cancellationToken);
        }

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public async ValueTask<NnrpSubmitResult> SubmitAsync(
            NnrpSubmitRequest submitRequest,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await SendSubmitAsync(submitRequest, cancellationToken).ConfigureAwait(false);
            var resultMessage = await ReceiveResultAsync(submitRequest.FrameId, submitRequest.ViewId, cancellationToken).ConfigureAwait(false);
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

        public ValueTask<NnrpSubmitResult> SubmitAndWaitAsync(
            NnrpSubmitRequest submitRequest,
            CancellationToken cancellationToken = default)
        {
            return SubmitAsync(submitRequest, cancellationToken);
        }

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public async ValueTask<NnrpSubmittedFrame> SendSubmitAsync(
            FrameSubmitMessage submitMessage,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var framed = submitMessage.ToFramedMessage();
            var submitted = RegisterOutgoingSubmit(
                framed.Header.SessionId,
                framed.Header.FrameId,
                framed.Header.ViewId,
                framed.Header.TraceId);

            try
            {
                await transport.SendAsync(framed, cancellationToken).ConfigureAwait(false);
                return submitted;
            }
            catch
            {
                RemoveInFlightFrame(new NnrpFrameKey(submitted.FrameId, submitted.ViewId));
                throw;
            }
        }

        public async ValueTask<NnrpSubmittedFrame> SendSubmitAsync(
            NnrpSubmitRequest submitRequest,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var submitFrame = CreateFrameSubmit(submitRequest);
            var submitted = RegisterOutgoingSubmit(
                submitFrame.Header.SessionId,
                submitFrame.Header.FrameId,
                submitFrame.Header.ViewId,
                submitFrame.Header.TraceId);

            try
            {
                await transport.SendAsync(submitFrame, cancellationToken).ConfigureAwait(false);
                return submitted;
            }
            catch
            {
                RemoveInFlightFrame(new NnrpFrameKey(submitted.FrameId, submitted.ViewId));
                throw;
            }
        }

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public async ValueTask<ResultPushMessage> ReceiveResultAsync(
            uint expectedFrameId,
            ushort expectedViewId = 0,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var frameKey = new NnrpFrameKey(expectedFrameId, expectedViewId);
            lock (gate)
            {
                if (!inFlightFrames.Contains(frameKey))
                {
                    throw new InvalidOperationException(
                        $"Cannot receive RESULT_PUSH for frame {expectedFrameId} view {expectedViewId} because it is not in flight.");
                }

                if (bufferedResults.TryGetValue(frameKey, out var bufferedResult))
                {
                    bufferedResults.Remove(frameKey);
                    inFlightFrames.Remove(frameKey);
                    return bufferedResult;
                }
            }

            while (true)
            {
                NnrpFramedMessage response;
                try
                {
                    response = await transport.ReceiveAsync(cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    RemoveInFlightFrame(frameKey);
                    throw;
                }

                if (response.Header.MessageType == MessageType.Error)
                {
                    RemoveInFlightFrame(frameKey);
                    var responseFailure = ParseErrorFailure(response);
                    throw new InvalidOperationException($"Server returned ERROR: {responseFailure}.");
                }

                if (response.Header.MessageType == MessageType.FlowUpdate)
                {
                    if (!FlowUpdateMessage.TryParse(response, out var flowUpdate, out var flowUpdateParseError))
                    {
                        RemoveInFlightFrame(frameKey);
                        throw new InvalidOperationException(
                            $"Expected valid FLOW_UPDATE while awaiting RESULT_PUSH, received malformed FLOW_UPDATE ({flowUpdateParseError}).");
                    }

                    if (NegotiatedSessionId != 0
                        && flowUpdate.Header.SessionId != 0
                        && flowUpdate.Header.SessionId != NegotiatedSessionId)
                    {
                        RemoveInFlightFrame(frameKey);
                        throw new InvalidOperationException(
                            $"FLOW_UPDATE session_id {flowUpdate.Header.SessionId} does not match negotiated session_id {NegotiatedSessionId}.");
                    }

                    lock (gate)
                    {
                        bufferedFlowUpdates.Enqueue(flowUpdate);
                    }

                    continue;
                }

                if (!ResultPushMessage.TryParse(response, out var result, out var parseError))
                {
                    RemoveInFlightFrame(frameKey);
                    throw new InvalidOperationException(
                        $"Expected RESULT_PUSH after FRAME_SUBMIT, received {response.Header.MessageType} ({parseError}).");
                }

                if (NegotiatedSessionId != 0 && result.Header.SessionId != NegotiatedSessionId)
                {
                    RemoveInFlightFrame(frameKey);
                    throw new InvalidOperationException(
                        $"RESULT_PUSH session_id {result.Header.SessionId} does not match negotiated session_id {NegotiatedSessionId}.");
                }

                var resultFrameKey = new NnrpFrameKey(result.Header.FrameId, result.Header.ViewId);
                if (resultFrameKey.Equals(frameKey))
                {
                    RemoveInFlightFrame(frameKey);
                    return result;
                }

                lock (gate)
                {
                    if (!inFlightFrames.Contains(resultFrameKey))
                    {
                        RemoveInFlightFrame(frameKey);
                        throw new InvalidOperationException(
                            $"RESULT_PUSH correlation mismatch: frame_id={result.Header.FrameId}, view_id={result.Header.ViewId}; expected frame_id={expectedFrameId}, view_id={expectedViewId}.");
                    }

                    if (bufferedResults.ContainsKey(resultFrameKey))
                    {
                        RemoveInFlightFrame(frameKey);
                        throw new InvalidOperationException(
                            $"Duplicate buffered RESULT_PUSH for frame_id={result.Header.FrameId}, view_id={result.Header.ViewId}.");
                    }

                    bufferedResults.Add(resultFrameKey, result);
                }
            }
        }

        public async ValueTask<NnrpSessionEvent> ReceiveNextEventAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            lock (gate)
            {
                if (bufferedFlowUpdates.Count > 0)
                {
                    return NnrpSessionEvent.FromFlowUpdate(bufferedFlowUpdates.Dequeue());
                }

                if (TryDequeueBufferedResultForPump(out var bufferedResult))
                {
                    return NnrpSessionEvent.FromResultPush(bufferedResult);
                }
            }

            while (true)
            {
                var response = await transport.ReceiveAsync(cancellationToken).ConfigureAwait(false);
                if (response.Header.MessageType == MessageType.Error)
                {
                    var responseFailure = ParseErrorFailure(response);
                    throw new InvalidOperationException($"Server returned ERROR: {responseFailure}.");
                }

                if (response.Header.MessageType == MessageType.FlowUpdate)
                {
                    if (!FlowUpdateMessage.TryParse(response, out var flowUpdate, out var flowUpdateParseError))
                    {
                        throw new InvalidOperationException(
                            $"Expected valid FLOW_UPDATE on the current session pump, received malformed FLOW_UPDATE ({flowUpdateParseError}).");
                    }

                    if (NegotiatedSessionId != 0
                        && flowUpdate.Header.SessionId != 0
                        && flowUpdate.Header.SessionId != NegotiatedSessionId)
                    {
                        throw new InvalidOperationException(
                            $"FLOW_UPDATE session_id {flowUpdate.Header.SessionId} does not match negotiated session_id {NegotiatedSessionId}.");
                    }

                    return NnrpSessionEvent.FromFlowUpdate(flowUpdate);
                }

                if (!ResultPushMessage.TryParse(response, out var result, out var parseError))
                {
                    throw new InvalidOperationException(
                        $"Expected FLOW_UPDATE or RESULT_PUSH on the current session pump, received {response.Header.MessageType} ({parseError}).");
                }

                if (NegotiatedSessionId != 0 && result.Header.SessionId != NegotiatedSessionId)
                {
                    throw new InvalidOperationException(
                        $"RESULT_PUSH session_id {result.Header.SessionId} does not match negotiated session_id {NegotiatedSessionId}.");
                }

                var resultFrameKey = new NnrpFrameKey(result.Header.FrameId, result.Header.ViewId);
                lock (gate)
                {
                    if (!inFlightFrames.Remove(resultFrameKey))
                    {
                        throw new InvalidOperationException(
                            $"RESULT_PUSH correlation mismatch: frame_id={result.Header.FrameId}, view_id={result.Header.ViewId} is not currently in flight.");
                    }
                }

                return NnrpSessionEvent.FromResultPush(result);
            }
        }

        public bool TryDequeueFlowUpdate(out FlowUpdateMessage flowUpdate)
        {
            lock (gate)
            {
                if (bufferedFlowUpdates.Count == 0)
                {
                    flowUpdate = default;
                    return false;
                }

                flowUpdate = bufferedFlowUpdates.Dequeue();
                return true;
            }
        }

        public NnrpInFlightFrame[] GetInFlightFrames()
        {
            lock (gate)
            {
                if (inFlightFrames.Count == 0)
                {
                    return Array.Empty<NnrpInFlightFrame>();
                }

                var frames = new NnrpInFlightFrame[inFlightFrames.Count];
                var index = 0;
                foreach (var frame in inFlightFrames)
                {
                    frames[index++] = new NnrpInFlightFrame(frame.FrameId, frame.ViewId);
                }

                return frames;
            }
        }

        public bool IsFrameInFlight(uint frameId, ushort viewId = 0)
        {
            lock (gate)
            {
                return inFlightFrames.Contains(new NnrpFrameKey(frameId, viewId));
            }
        }

        public bool TryValidateResultCorrelation(
            NnrpHeader resultHeader,
            out NnrpInFlightFrame inFlightFrame,
            out string failure)
        {
            inFlightFrame = default;
            if (resultHeader.MessageType != MessageType.ResultPush)
            {
                failure = $"Expected RESULT_PUSH for correlation, received {resultHeader.MessageType}.";
                return false;
            }

            if (NegotiatedSessionId != 0 && resultHeader.SessionId != NegotiatedSessionId)
            {
                failure = $"RESULT_PUSH session_id {resultHeader.SessionId} does not match negotiated session_id {NegotiatedSessionId}.";
                return false;
            }

            lock (gate)
            {
                var frameKey = new NnrpFrameKey(resultHeader.FrameId, resultHeader.ViewId);
                if (!inFlightFrames.Contains(frameKey))
                {
                    failure = $"RESULT_PUSH frame_id={resultHeader.FrameId}, view_id={resultHeader.ViewId} is not currently in flight.";
                    return false;
                }

                inFlightFrame = new NnrpInFlightFrame(frameKey.FrameId, frameKey.ViewId);
            }

            failure = string.Empty;
            return true;
        }

        private bool TryGetResultCorrelationFailure(NnrpHeader resultHeader, NnrpFrameKey expectedFrameKey, out string failure)
        {
            if (NegotiatedSessionId != 0 && resultHeader.SessionId != NegotiatedSessionId)
            {
                failure = $"RESULT_PUSH session_id {resultHeader.SessionId} does not match negotiated session_id {NegotiatedSessionId}.";
                return true;
            }

            if (resultHeader.FrameId != expectedFrameKey.FrameId || resultHeader.ViewId != expectedFrameKey.ViewId)
            {
                failure = $"RESULT_PUSH correlation mismatch: frame_id={resultHeader.FrameId}, view_id={resultHeader.ViewId}; expected frame_id={expectedFrameKey.FrameId}, view_id={expectedFrameKey.ViewId}.";
                return true;
            }

            failure = string.Empty;
            return false;
        }

        public async ValueTask<NnrpProtocolFailure> CancelAsync(
            uint frameId,
            ushort viewId = 0,
            ulong traceId = 0,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (session.State != NnrpSessionState.Active)
            {
                return NnrpProtocolFailure.InvalidState(
                    NnrpErrorScope.Session,
                    $"Cannot cancel frame {frameId} view {viewId} while session is {session.State}.");
            }

            var frameKey = new NnrpFrameKey(frameId, viewId);
            lock (gate)
            {
                if (!inFlightFrames.Contains(frameKey))
                {
                    return NnrpProtocolFailure.InvalidState(
                        NnrpErrorScope.Frame,
                        $"Cannot cancel frame {frameId} view {viewId} because it is not in flight.");
                }
            }

            var cancelMessage = FrameCancelMessage.Create(NegotiatedSessionId, frameId, viewId, traceId);
            await transport.SendAsync(cancelMessage.ToFramedMessage(), cancellationToken).ConfigureAwait(false);
            RemoveInFlightFrame(frameKey);
            return NnrpProtocolFailure.None;
        }

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public async ValueTask SendSessionMigrateAsync(
            SessionMigrateMessage migrateMessage,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (session.State != NnrpSessionState.Active)
            {
                throw new InvalidOperationException($"Cannot send SESSION_MIGRATE while session is {session.State}.");
            }

            if (NegotiatedSessionId != 0 && migrateMessage.Header.SessionId != NegotiatedSessionId)
            {
                throw new InvalidOperationException(
                    $"SESSION_MIGRATE session_id {migrateMessage.Header.SessionId} does not match negotiated session_id {NegotiatedSessionId}.");
            }

            await transport.SendAsync(migrateMessage.ToFramedMessage(), cancellationToken).ConfigureAwait(false);
        }

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public async ValueTask<SessionMigrateAckMessage> ReceiveSessionMigrateAckAsync(
            ulong expectedTraceId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (session.State != NnrpSessionState.Active)
            {
                throw new InvalidOperationException($"Cannot receive SESSION_MIGRATE_ACK while session is {session.State}.");
            }

            var response = await transport.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            if (response.Header.MessageType == MessageType.Error)
            {
                var failure = ParseErrorFailure(response);
                throw new InvalidOperationException($"Server returned ERROR: {failure}.");
            }

            if (!SessionMigrateAckMessage.TryParse(response, out var ack, out var parseError))
            {
                throw new InvalidOperationException(
                    $"Expected SESSION_MIGRATE_ACK after SESSION_MIGRATE, received {response.Header.MessageType} ({parseError}).");
            }

            if (ack.Header.SessionId != NegotiatedSessionId || ack.Header.TraceId != expectedTraceId)
            {
                throw new InvalidOperationException(
                    $"SESSION_MIGRATE_ACK correlation mismatch: session_id={ack.Header.SessionId}, trace_id={ack.Header.TraceId}.");
            }

            ApplyResumeFromFrameIdFloor(ack.Metadata.ResumeFromFrameId);

            return ack;
        }

        public async ValueTask<SessionMigrateAckMessage> MigrateAsync(
            TransportId oldTransportId,
            TransportId newTransportId,
            ulong lastResultFrameId,
            ulong clientMigrateTimestampMicroseconds,
            ulong traceId = 0,
            CancellationToken cancellationToken = default)
        {
            var migrate = new SessionMigrateMessage(
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
                    oldTransportId,
                    newTransportId,
                    lastResultFrameId,
                    clientMigrateTimestampMicroseconds));

            await SendSessionMigrateAsync(migrate, cancellationToken).ConfigureAwait(false);
            return await ReceiveSessionMigrateAckAsync(traceId, cancellationToken).ConfigureAwait(false);
        }

        public NnrpTransportMigrationDecision EvaluateMigrationTrigger(
            TransportId currentTransportId,
            NnrpTransportProbeSelectionResult probeSelection,
            NnrpTransportMigrationTriggerOptions? triggerOptions = null)
        {
            return NnrpTransportMigrationTrigger.Evaluate(currentTransportId, probeSelection, triggerOptions);
        }

        public async ValueTask<NnrpClientMigrationResult> TryAutoMigrateAsync(
            TransportId currentTransportId,
            NnrpTransportProbeSelectionResult probeSelection,
            ulong lastResultFrameId,
            ulong clientMigrateTimestampMicroseconds,
            NnrpTransportMigrationTriggerOptions? triggerOptions = null,
            ulong traceId = 0,
            CancellationToken cancellationToken = default)
        {
            var decision = EvaluateMigrationTrigger(currentTransportId, probeSelection, triggerOptions);
            if (!decision.ShouldMigrate)
            {
                return new NnrpClientMigrationResult(decision, default, wasMigrated: false);
            }

            var ack = await MigrateAsync(
                currentTransportId,
                decision.TargetTransportId,
                lastResultFrameId,
                clientMigrateTimestampMicroseconds,
                traceId,
                cancellationToken).ConfigureAwait(false);
            return new NnrpClientMigrationResult(decision, ack, wasMigrated: true);
        }

        public async ValueTask<TimeSpan> PingAsync(
            ulong traceId = 0,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (session.State != NnrpSessionState.Active)
            {
                throw new InvalidOperationException($"Cannot send PING while session is {session.State}.");
            }

            var ping = PingMessage.Create(NegotiatedSessionId, traceId);
            var stopwatch = Stopwatch.StartNew();
            await transport.SendAsync(ping.ToFramedMessage(), cancellationToken).ConfigureAwait(false);

            var response = await transport.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();

            if (response.Header.MessageType == MessageType.Error)
            {
                var failure = ParseErrorFailure(response);
                throw new InvalidOperationException($"Server returned ERROR: {failure}.");
            }

            if (!PongMessage.TryParse(response, out var pong, out var parseError))
            {
                throw new InvalidOperationException(
                    $"Expected PONG after PING, received {response.Header.MessageType} ({parseError}).");
            }

            if (pong.Header.SessionId != ping.Header.SessionId || pong.Header.TraceId != ping.Header.TraceId)
            {
                throw new InvalidOperationException(
                    $"PONG correlation mismatch: session_id={pong.Header.SessionId}, trace_id={pong.Header.TraceId}.");
            }

            return stopwatch.Elapsed;
        }

        public async ValueTask<NnrpProtocolFailure> CloseAsync(
            string reason = "",
            ulong traceId = 0,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var priorState = session.State;
            var failure = await session.CloseAsync(cancellationToken).ConfigureAwait(false);
            if (failure.IsFailure)
            {
                return failure;
            }

            if (priorState == NnrpSessionState.Active)
            {
                var closeMessage = CloseMessage.Create(NegotiatedSessionId, reason, traceId);
                await transport.SendAsync(closeMessage.ToFramedMessage(), cancellationToken).ConfigureAwait(false);
            }

            lock (gate)
            {
                resumeFromFrameIdFloor = 0;
                inFlightFrames.Clear();
                bufferedResults.Clear();
            }

            return NnrpProtocolFailure.None;
        }

        private void RemoveInFlightFrame(NnrpFrameKey frameKey)
        {
            lock (gate)
            {
                inFlightFrames.Remove(frameKey);
                bufferedResults.Remove(frameKey);
            }
        }

        private void ApplyResumeFromFrameIdFloor(ulong resumeFromFrameId)
        {
            lock (gate)
            {
                if (resumeFromFrameId <= resumeFromFrameIdFloor)
                {
                    return;
                }

                resumeFromFrameIdFloor = resumeFromFrameId;

                if (inFlightFrames.Count != 0)
                {
                    var staleInFlightFrames = new List<NnrpFrameKey>();
                    foreach (var frameKey in inFlightFrames)
                    {
                        if (frameKey.FrameId < resumeFromFrameIdFloor)
                        {
                            staleInFlightFrames.Add(frameKey);
                        }
                    }

                    for (var i = 0; i < staleInFlightFrames.Count; i++)
                    {
                        inFlightFrames.Remove(staleInFlightFrames[i]);
                    }
                }

                if (bufferedResults.Count != 0)
                {
                    var staleBufferedFrames = new List<NnrpFrameKey>();
                    foreach (var entry in bufferedResults)
                    {
                        if (entry.Key.FrameId < resumeFromFrameIdFloor)
                        {
                            staleBufferedFrames.Add(entry.Key);
                        }
                    }

                    for (var i = 0; i < staleBufferedFrames.Count; i++)
                    {
                        bufferedResults.Remove(staleBufferedFrames[i]);
                    }
                }
            }
        }

        private static bool ValidateAckWireFormat(
            ClientHelloMessage hello,
            ServerHelloAckMessage ack,
            out NnrpProtocolFailure failure)
        {
            if (hello.Metadata.SupportedWireFormatBitmap == ControlMetadataBitmaps.CurrentWireFormatBitmap
                && ack.Metadata.SelectedWireFormat == NnrpHeader.CurrentWireFormat)
            {
                failure = NnrpProtocolFailure.None;
                return true;
            }

            failure = new NnrpProtocolFailure(
                ErrorCode.UnsupportedCapability,
                NnrpErrorScope.Connection,
                $"SERVER_HELLO_ACK selected wire format {ack.Metadata.SelectedWireFormat} is not compatible with CLIENT_HELLO supported_wire_format_bitmap 0x{hello.Metadata.SupportedWireFormatBitmap:X}.",
                isFatal: true);
            return false;
        }

        private NnrpSubmittedFrame RegisterOutgoingSubmit(
            uint sessionId,
            uint frameId,
            ushort viewId,
            ulong traceId)
        {
            var frameKey = new NnrpFrameKey(frameId, viewId);

            lock (gate)
            {
                if (!session.TryAcceptFrameSubmit(out var failure))
                {
                    throw new InvalidOperationException($"FRAME_SUBMIT rejected by session state: {failure}.");
                }

                if (NegotiatedSessionId != 0 && sessionId != NegotiatedSessionId)
                {
                    throw new InvalidOperationException(
                        $"FRAME_SUBMIT session_id {sessionId} does not match negotiated session_id {NegotiatedSessionId}.");
                }

                if (frameId < resumeFromFrameIdFloor)
                {
                    throw new InvalidOperationException(
                        $"FRAME_SUBMIT frame_id {frameId} is below migration resume_from_frame_id {resumeFromFrameIdFloor}.");
                }

                if (!inFlightFrames.Add(frameKey))
                {
                    throw new InvalidOperationException(
                        $"FRAME_SUBMIT for frame {frameKey.FrameId} view {frameKey.ViewId} is already in flight.");
                }
            }

            return new NnrpSubmittedFrame(sessionId, frameId, viewId, traceId);
        }

        private bool TryDequeueBufferedResultForPump(out ResultPushMessage result)
        {
            result = default;
            if (bufferedResults.Count == 0)
            {
                return false;
            }

            NnrpFrameKey selectedKey = default;
            var hasSelectedKey = false;
            foreach (var entry in bufferedResults)
            {
                if (!hasSelectedKey
                    || entry.Key.FrameId < selectedKey.FrameId
                    || (entry.Key.FrameId == selectedKey.FrameId && entry.Key.ViewId < selectedKey.ViewId))
                {
                    selectedKey = entry.Key;
                    hasSelectedKey = true;
                }
            }

            if (!hasSelectedKey)
            {
                return false;
            }

            result = bufferedResults[selectedKey];
            bufferedResults.Remove(selectedKey);
            inFlightFrames.Remove(selectedKey);
            return true;
        }

        private static NnrpProtocolFailure ParseErrorFailure(NnrpFramedMessage response)
        {
            if (!ErrorMessage.TryParse(response.ToArray(), out var error, out var parseError))
            {
                return new NnrpProtocolFailure(
                    ErrorCode.MalformedBody,
                    NnrpErrorScope.Connection,
                    $"Failed to parse ERROR response: {parseError}.",
                    isFatal: true,
                    parseError: parseError);
            }

            return error.ToProtocolFailure();
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
                bodyLength: FrameSubmitMessage.ComputeBodyLength(
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

        private static NnrpServerCapabilities ToServerCapabilities(ServerHelloAckMessage ack)
        {
            var metadata = ack.Metadata;
            return new NnrpServerCapabilities(
                ControlMetadataBitmaps.DecodeCodecBitmap<CodecId>(metadata.AcceptedCodecBitmap),
                ControlMetadataBitmaps.DecodeCodecBitmap<DTypeId>(metadata.AcceptedDTypeBitmap),
                ControlMetadataBitmaps.DecodeCodecBitmap<TensorLayoutId>(metadata.AcceptedLayoutBitmap),
                metadata.AcceptedPayloadKindBitmap,
                metadata.CacheObjectBitmap,
                (BudgetPolicy)metadata.DegradePolicy,
                checked((int)metadata.MaxConcurrentFrames),
                metadata.CacheEnabled != 0,
                checked((int)metadata.MaxCacheEntries),
                checked((int)metadata.MaxBodyBytes),
                maxSectionCount: 8,
                maxTileCount: 8192,
                checked((int)metadata.MaxLaneCount),
                checked((int)Math.Max(1u, metadata.TokenTtlMilliseconds / 1000u)),
                allowSessionRenewal: true);
        }

        private static bool ValidateActiveTransportEcho(
            ServerHelloAckMessage ack,
            TransportId? expectedActiveTransportId,
            out NnrpProtocolFailure failure)
        {
            failure = NnrpProtocolFailure.None;
            if (!expectedActiveTransportId.HasValue)
            {
                return true;
            }

            if (!ack.TryGetServerTransportPolicyAckExtension(out var extension, out var parseError))
            {
                failure = new NnrpProtocolFailure(
                    ErrorCode.MalformedBody,
                    NnrpErrorScope.Connection,
                    $"Expected ServerHelloAck transport policy extension for active transport validation ({parseError}).",
                    isFatal: true,
                    parseError: parseError);
                return false;
            }

            if (extension.ActiveTransportId != expectedActiveTransportId.Value)
            {
                failure = NnrpProtocolFailure.UnsupportedCapability(
                    $"ServerHelloAck active transport {extension.ActiveTransportId} does not match expected {expectedActiveTransportId.Value}.",
                    isFatal: true);
                return false;
            }

            return true;
        }
    }
}
