using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Nnrp.Core;

namespace Nnrp.Server
{
    public sealed class NnrpServerSession : INnrpServerSession
    {
        private readonly NnrpFrameLifecycle frameLifecycle = new NnrpFrameLifecycle();
        private readonly Func<uint, uint> sessionIdAllocator;
        private readonly NnrpSessionStateMachine stateMachine = new NnrpSessionStateMachine();
        private readonly INnrpMessageTransport transport;
        private readonly NnrpCacheStore? cacheStore;

        public NnrpServerSession(
            ServerProfile profile,
            INnrpMessageTransport transport,
            Func<uint, uint>? sessionIdAllocator = null,
            NnrpCacheStore? cacheStore = null)
        {
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
            this.sessionIdAllocator = sessionIdAllocator ?? DefaultSessionIdAllocator;
            this.cacheStore = cacheStore;
        }

        public ServerProfile Profile { get; }

        public NnrpSessionState State => stateMachine.State;

        public uint SessionId { get; private set; }

        public byte NegotiatedWireFormat => NnrpHeader.CurrentWireFormat;

        public NnrpCapabilitySelection NegotiatedCapabilities { get; private set; }

        public NnrpCapabilityNegotiationResult LastNegotiationResult { get; private set; }

        public NnrpProtocolFailure LastFailure => stateMachine.LastFailure;

        public async ValueTask<NnrpProtocolFailure> AcceptAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!stateMachine.TryBeginNegotiation(out var failure))
            {
                stateMachine.ApplyFailure(failure);
                return failure;
            }

            var inbound = await transport.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            if (!ClientHelloMessage.TryParse(inbound, out var hello, out var parseError))
            {
                var parseFailure = new NnrpProtocolFailure(
                    ErrorCode.MalformedBody,
                    NnrpErrorScope.Connection,
                    $"Expected CLIENT_HELLO during accept, received {inbound.Header.MessageType} ({parseError}).",
                    isFatal: true,
                    parseError: parseError);
                stateMachine.TryFailNegotiation(parseFailure, out _);
                await transport.SendAsync(
                    ErrorMessage.FromProtocolFailure(parseFailure, traceId: inbound.Header.TraceId).ToFramedMessage(),
                    cancellationToken).ConfigureAwait(false);
                return parseFailure;
            }

            if ((hello.Metadata.SupportedWireFormatBitmap & ControlMetadataBitmaps.CurrentWireFormatBitmap) == 0)
            {
                var rejectionFailure = new NnrpProtocolFailure(
                    ErrorCode.UnsupportedVersion,
                    NnrpErrorScope.Connection,
                    "Client must advertise the current NNRP/1 wire format.",
                    isFatal: true);
                stateMachine.TryFailNegotiation(rejectionFailure, out _);
                await transport.SendAsync(
                    ErrorMessage.FromProtocolFailure(rejectionFailure, traceId: hello.Header.TraceId).ToFramedMessage(),
                    cancellationToken).ConfigureAwait(false);
                return rejectionFailure;
            }

            var negotiationResult = NnrpCapabilityNegotiator.Negotiate(hello.ToCapabilities(), Profile.ToCapabilities());
            LastNegotiationResult = negotiationResult;
            if (!negotiationResult.IsAccepted)
            {
                var rejectionFailure = ToProtocolFailure(negotiationResult);
                stateMachine.TryFailNegotiation(rejectionFailure, out _);
                await transport.SendAsync(
                    ErrorMessage.FromProtocolFailure(rejectionFailure, traceId: hello.Header.TraceId).ToFramedMessage(),
                    cancellationToken).ConfigureAwait(false);
                return rejectionFailure;
            }

            SessionId = sessionIdAllocator(hello.Metadata.RequestedSessionId);
            NegotiatedCapabilities = negotiationResult.Selection;
            var ack = Profile.CreateServerHelloAck(SessionId, negotiationResult, hello.Header.TraceId);
            ack = AttachTransportPolicyAckExtension(hello, ack);
            await transport.SendAsync(ack.ToFramedMessage(), cancellationToken).ConfigureAwait(false);

            if (!stateMachine.TryActivate(out failure))
            {
                stateMachine.ApplyFailure(failure);
                return failure;
            }

            return NnrpProtocolFailure.None;
        }

        private ServerHelloAckMessage AttachTransportPolicyAckExtension(ClientHelloMessage hello, ServerHelloAckMessage ack)
        {
            if (!hello.TryGetClientTransportPolicyExtension(out var clientTransportPolicy, out _))
            {
                return ack;
            }

            var activeTransportId = ResolveActiveTransportId();
            if (activeTransportId == TransportId.Unspecified)
            {
                return ack;
            }

            var extensions = new[]
            {
                new ServerTransportPolicyAckExtension(
                    clientTransportPolicy.TransportPolicy,
                    clientTransportPolicy.TransportPolicy,
                    activeTransportId).ToControlExtension(),
            };
            var extensionBytes = checked((uint)extensions[0].TotalLength);
            var metadata = ack.Metadata.WithControlExtensionBytes(extensionBytes);
            var header = new NnrpHeader(
                versionMajor: ack.Header.VersionMajor,
                messageType: ack.Header.MessageType,
                flags: ack.Header.Flags,
                metaLength: ack.Header.MetaLength,
                bodyLength: extensionBytes,
                sessionId: ack.Header.SessionId,
                frameId: ack.Header.FrameId,
                viewId: ack.Header.ViewId,
                routeId: ack.Header.RouteId,
                traceId: ack.Header.TraceId);
            return new ServerHelloAckMessage(header, metadata, extensions);
        }

        private TransportId ResolveActiveTransportId()
        {
            return string.Equals(transport.GetType().FullName, "Nnrp.Transport.Tcp.NnrpTcpMessageTransport", StringComparison.Ordinal)
                ? TransportId.Tcp
                : TransportId.Unspecified;
        }

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public async ValueTask<FrameSubmitMessage> ReceiveFrameSubmitAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!stateMachine.TryAcceptFrameSubmit(out var failure))
            {
                throw new InvalidOperationException($"FRAME_SUBMIT rejected by session state: {failure}.");
            }

            var inbound = await transport.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            if (inbound.Header.MessageType == MessageType.Close)
            {
                var close = CloseMessage.TryParse(inbound.ToArray(), out var closeMessage, out var closeError)
                    ? closeMessage.Reason
                    : $"peer_close ({closeError})";
                stateMachine.TryClose(out _);
                throw new InvalidOperationException($"Peer closed session before FRAME_SUBMIT: {close}.");
            }

            if (!TryParseFrameSubmit(inbound, out var submit, out var parseError))
            {
                throw new InvalidOperationException(
                    $"Expected FRAME_SUBMIT during receive, received {inbound.Header.MessageType} ({parseError}).");
            }

            if (SessionId != 0 && submit.Header.SessionId != SessionId)
            {
                throw new InvalidOperationException(
                    $"FRAME_SUBMIT session_id {submit.Header.SessionId} does not match server session_id {SessionId}.");
            }

            if (!frameLifecycle.TrySubmit(submit.Header.FrameId, submit.Header.ViewId, submit.Metadata.RetryOfFrame, out var lifecycleFailure))
            {
                throw new InvalidOperationException($"FRAME_SUBMIT rejected by frame lifecycle: {lifecycleFailure}.");
            }

            return submit;
        }

        public async ValueTask<NnrpFrameSubmit> ReceiveSubmitAsync(CancellationToken cancellationToken)
        {
            var submit = await ReceiveFrameSubmitAsync(cancellationToken).ConfigureAwait(false);
            return new NnrpFrameSubmit(
                submit.Header.SessionId,
                submit.Header.FrameId,
                submit.Header.ViewId,
                submit.Header.TraceId,
                submit.Metadata.SourceWidth,
                submit.Metadata.SourceHeight,
                submit.Metadata.TileWidth,
                submit.Metadata.TileHeight,
                submit.CameraBlock,
                submit.TileIds,
                submit.Sections,
                submit.Metadata.FrameClass,
                submit.Metadata.InputProfile,
                submit.Metadata.TileIndexMode,
                submit.Metadata.LatencyBudgetMilliseconds,
                submit.Metadata.TargetFpsTimes100,
                submit.Metadata.DependencyFrameId,
                submit.Metadata.TileBaseId);
        }

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public async ValueTask<SessionMigrateMessage> ReceiveSessionMigrateAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (State != NnrpSessionState.Active)
            {
                throw new InvalidOperationException($"Cannot receive SESSION_MIGRATE while session is {State}.");
            }

            var inbound = await transport.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            if (inbound.Header.MessageType == MessageType.Close)
            {
                var close = CloseMessage.TryParse(inbound.ToArray(), out var closeMessage, out var closeError)
                    ? closeMessage.Reason
                    : $"peer_close ({closeError})";
                stateMachine.TryClose(out _);
                throw new InvalidOperationException($"Peer closed session before SESSION_MIGRATE: {close}.");
            }

            if (!SessionMigrateMessage.TryParse(inbound, out var migrate, out var parseError))
            {
                throw new InvalidOperationException(
                    $"Expected SESSION_MIGRATE during receive, received {inbound.Header.MessageType} ({parseError}).");
            }

            if (SessionId != 0 && migrate.Header.SessionId != SessionId)
            {
                throw new InvalidOperationException(
                    $"SESSION_MIGRATE session_id {migrate.Header.SessionId} does not match server session_id {SessionId}.");
            }

            return migrate;
        }

        public ValueTask SendResultAsync(NnrpResult result, CancellationToken cancellationToken)
        {
            return SendResultAsync(CreateResultPushMessage(result), cancellationToken);
        }

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public async ValueTask SendResultAsync(ResultPushMessage resultMessage, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (State != NnrpSessionState.Active)
            {
                throw new InvalidOperationException($"Cannot send RESULT_PUSH while session is {State}.");
            }

            if (SessionId != 0 && resultMessage.Header.SessionId != SessionId)
            {
                throw new InvalidOperationException(
                    $"RESULT_PUSH session_id {resultMessage.Header.SessionId} does not match server session_id {SessionId}.");
            }

            var processingFailure = NnrpProtocolFailure.None;
            var readyFailure = NnrpProtocolFailure.None;
            var deliverFailure = NnrpProtocolFailure.None;
            if (!frameLifecycle.TryStartProcessing(resultMessage.Header.FrameId, resultMessage.Header.ViewId, out processingFailure)
                || !frameLifecycle.TryMarkReady(resultMessage.Header.FrameId, resultMessage.Header.ViewId, out readyFailure)
                || !frameLifecycle.TryDeliver(resultMessage.Header.FrameId, resultMessage.Header.ViewId, out deliverFailure))
            {
                var failure = processingFailure.IsFailure ? processingFailure : readyFailure.IsFailure ? readyFailure : deliverFailure;
                throw new InvalidOperationException($"RESULT_PUSH rejected by frame lifecycle: {failure}.");
            }

            await transport.SendAsync(resultMessage.ToFramedMessage(), cancellationToken).ConfigureAwait(false);
        }

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public async ValueTask SendSessionMigrateAckAsync(SessionMigrateAckMessage ackMessage, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (State != NnrpSessionState.Active)
            {
                throw new InvalidOperationException($"Cannot send SESSION_MIGRATE_ACK while session is {State}.");
            }

            if (SessionId != 0 && ackMessage.Header.SessionId != SessionId)
            {
                throw new InvalidOperationException(
                    $"SESSION_MIGRATE_ACK session_id {ackMessage.Header.SessionId} does not match server session_id {SessionId}.");
            }

            await transport.SendAsync(ackMessage.ToFramedMessage(), cancellationToken).ConfigureAwait(false);
        }

        private ResultPushMessage CreateResultPushMessage(NnrpResult result)
        {
            var tileCount = checked((ushort)result.TileIds.Length);
            var tileIndexBytes = TileIndexBlockCodec.GetEncodedLength(result.TileIds.Span, TileIndexMode.RawUInt16);
            var metadata = new ResultPushMetadata(
                statusCode: result.StatusCode,
                resultFlags: result.ResultFlags,
                sectionCount: checked((ushort)result.Sections.Length),
                tileCount: tileCount,
                activeProfileId: result.ActiveProfileId,
                inferenceMilliseconds: result.InferenceMilliseconds,
                queueMilliseconds: result.QueueMilliseconds,
                serverTotalMilliseconds: result.ServerTotalMilliseconds,
                tileBaseId: result.TileBaseId,
                tileIndexBytes: checked((uint)tileIndexBytes),
                resultClass: result.ResultClass,
                appliedBudgetPolicy: result.AppliedBudgetPolicy,
                reusedFrameId: result.ReusedFrameId,
                coveredTileCount: result.CoveredTileCount,
                droppedTileCount: result.DroppedTileCount,
                payloadKindBitmap: result.PayloadKindBitmap,
                payloadFrameCount: result.PayloadFrameCount);
            var header = new NnrpHeader(
                versionMajor: NnrpHeader.CurrentVersionMajor,
                messageType: MessageType.ResultPush,
                flags: HeaderFlags.None,
                metaLength: ResultPushMetadata.MetadataLength,
                bodyLength: ComputeResultPushBodyLength(tileIndexBytes, result.Sections.Span),
                sessionId: SessionId,
                frameId: result.FrameId,
                viewId: result.ViewId,
                routeId: 0,
                traceId: result.TraceId);
            return new ResultPushMessage(header, metadata, result.TileIds, result.Sections);
        }

        private static uint ComputeResultPushBodyLength(int tileIndexBytes, ReadOnlySpan<TensorSectionBlock> sections)
        {
            var bodyLength = tileIndexBytes;

            for (var index = 0; index < sections.Length; index++)
            {
                bodyLength = BinaryAlignment.AlignUp(bodyLength, 8);
                bodyLength = checked(bodyLength + sections[index].TotalLength);
            }

            return checked((uint)bodyLength);
        }

        public async ValueTask SendResultDropAsync(ResultDropMessage dropMessage, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (State != NnrpSessionState.Active)
            {
                throw new InvalidOperationException($"Cannot send RESULT_DROP while session is {State}.");
            }

            if (SessionId != 0 && dropMessage.Header.SessionId != SessionId)
            {
                throw new InvalidOperationException(
                    $"RESULT_DROP session_id {dropMessage.Header.SessionId} does not match server session_id {SessionId}.");
            }

            if (!frameLifecycle.TryDrop(dropMessage.Header.FrameId, dropMessage.Header.ViewId, out var lifecycleFailure))
            {
                throw new InvalidOperationException($"RESULT_DROP rejected by frame lifecycle: {lifecycleFailure}.");
            }

            await transport.SendAsync(dropMessage.ToFramedMessage(), cancellationToken).ConfigureAwait(false);
        }

        public async ValueTask HandleCachePutAsync(CachePutMessage putMessage, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (cacheStore == null)
            {
                throw new InvalidOperationException("Cache store is not configured on this server session.");
            }

            if (State != NnrpSessionState.Active)
            {
                throw new InvalidOperationException($"Cannot handle CACHE_PUT while session is {State}.");
            }

            if (SessionId != 0 && putMessage.Header.SessionId != SessionId)
            {
                throw new InvalidOperationException(
                    $"CACHE_PUT session_id {putMessage.Header.SessionId} does not match server session_id {SessionId}.");
            }

            var key = NnrpCacheKey.FromCachePutMetadata(putMessage.Metadata);
            var ttlSeconds = putMessage.Metadata.TtlMilliseconds > int.MaxValue
                ? int.MaxValue
                : (int)(putMessage.Metadata.TtlMilliseconds / 1000);
            if (ttlSeconds <= 0)
            {
                ttlSeconds = 1;
            }

            var result = cacheStore.TryPut(key, putMessage.ObjectBytes, ttlSeconds);

            var ack = new CacheAckMessage(
                new NnrpHeader(
                    versionMajor: NnrpHeader.CurrentVersionMajor,
                    messageType: MessageType.CacheAck,
                    flags: HeaderFlags.None,
                    metaLength: CacheAckMetadata.MetadataLength,
                    bodyLength: 0,
                    sessionId: SessionId,
                    frameId: 0,
                    viewId: 0,
                    routeId: 0,
                    traceId: putMessage.Header.TraceId),
                new CacheAckMetadata(
                    cacheNamespace: putMessage.Metadata.CacheNamespace,
                    cacheKeyHigh: putMessage.Metadata.CacheKeyHigh,
                    cacheKeyLow: putMessage.Metadata.CacheKeyLow,
                    status: result.Code == NnrpCacheResultCode.Stored ? CacheAckStatus.Accepted : CacheAckStatus.Rejected,
                    acceptedTtlMilliseconds: result.IsSuccess ? (uint)(ttlSeconds * 1000) : 0,
                    maxObjectBytes: (uint)cacheStore.MaxObjectBytes,
                    detailCode: 0));
            await transport.SendAsync(ack.ToFramedMessage(), cancellationToken).ConfigureAwait(false);
        }

        public async ValueTask HandleCacheInvalidateAsync(CacheInvalidateMessage invalidateMessage, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (cacheStore == null)
            {
                throw new InvalidOperationException("Cache store is not configured on this server session.");
            }

            if (State != NnrpSessionState.Active)
            {
                throw new InvalidOperationException($"Cannot handle CACHE_INVALIDATE while session is {State}.");
            }

            if (SessionId != 0 && invalidateMessage.Header.SessionId != SessionId)
            {
                throw new InvalidOperationException(
                    $"CACHE_INVALIDATE session_id {invalidateMessage.Header.SessionId} does not match server session_id {SessionId}.");
            }

            var key = NnrpCacheKey.FromCacheInvalidateMetadata(invalidateMessage.Metadata);
            cacheStore.TryInvalidate(key);

            var ack = new CacheAckMessage(
                new NnrpHeader(
                    versionMajor: NnrpHeader.CurrentVersionMajor,
                    messageType: MessageType.CacheAck,
                    flags: HeaderFlags.None,
                    metaLength: CacheAckMetadata.MetadataLength,
                    bodyLength: 0,
                    sessionId: SessionId,
                    frameId: 0,
                    viewId: 0,
                    routeId: 0,
                    traceId: invalidateMessage.Header.TraceId),
                new CacheAckMetadata(
                    cacheNamespace: invalidateMessage.Metadata.CacheNamespace,
                    cacheKeyHigh: invalidateMessage.Metadata.CacheKeyHigh,
                    cacheKeyLow: invalidateMessage.Metadata.CacheKeyLow,
                    status: CacheAckStatus.Accepted,
                    acceptedTtlMilliseconds: 0,
                    maxObjectBytes: (uint)cacheStore.MaxObjectBytes,
                    detailCode: 0));
            await transport.SendAsync(ack.ToFramedMessage(), cancellationToken).ConfigureAwait(false);
        }

        public async ValueTask<NnrpProtocolFailure> CloseAsync(string reason, ulong traceId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var priorState = stateMachine.State;
            if (!stateMachine.TryClose(out var failure))
            {
                stateMachine.ApplyFailure(failure);
                return failure;
            }

            if (priorState == NnrpSessionState.Active)
            {
                await transport.SendAsync(CloseMessage.Create(SessionId, reason ?? string.Empty, traceId).ToFramedMessage(), cancellationToken)
                    .ConfigureAwait(false);
            }

            return NnrpProtocolFailure.None;
        }

        private static uint DefaultSessionIdAllocator(uint requestedSessionId)
        {
            return requestedSessionId != 0 ? requestedSessionId : 1u;
        }

        private static bool TryParseFrameSubmit(
            NnrpFramedMessage inbound,
            out FrameSubmitMessage submit,
            out NnrpParseError error)
        {
            return FrameSubmitMessage.TryParse(inbound, out submit, out error);
        }

        private static NnrpProtocolFailure ToProtocolFailure(NnrpCapabilityNegotiationResult negotiationResult)
        {
            var message = string.IsNullOrEmpty(negotiationResult.RejectionMessage)
                ? $"Capability negotiation rejected: {negotiationResult.RejectionReason}."
                : negotiationResult.RejectionMessage;

            if (negotiationResult.ErrorCode == ErrorCode.LimitExceeded)
            {
                return NnrpProtocolFailure.LimitExceeded(NnrpErrorScope.Session, message, isFatal: true);
            }

            return NnrpProtocolFailure.UnsupportedCapability(message);
        }
    }
}
