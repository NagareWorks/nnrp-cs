using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nnrp.Core;
using Nnrp.Server;
using Xunit;

namespace Nnrp.Server.Tests
{
    public sealed class ServerSessionTests
    {
        [Fact]
        public async Task AcceptAsyncNegotiatesAndSendsServerHelloAck()
        {
            var transport = new QueueTransport(CreateClientHello(requestedSessionId: 41).ToFramedMessage());
            var server = new NnrpServerSession(new ServerProfile(), transport);

            var failure = await server.AcceptAsync(CancellationToken.None);

            Assert.Equal(NnrpProtocolFailure.None, failure);
            Assert.Equal(NnrpSessionState.Active, server.State);
            Assert.Equal(41u, server.SessionId);
            Assert.Single(transport.Sent);
            Assert.True(ServerHelloAckMessage.TryParse(transport.Sent[0].ToArray(), out var ack, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(41u, ack.Metadata.SessionId);
            Assert.Equal(NnrpHeader.CurrentWireFormat, server.NegotiatedWireFormat);
        }

        [Fact]
        public async Task AcceptAsyncSelectsCurrentStageWhenClientAdvertisesIt()
        {
            var transport = new QueueTransport(
                CreateClientHello(requestedSessionId: 41, supportedWireFormatBitmap: ControlMetadataBitmaps.EncodeCurrentWireFormatBitmap()).ToFramedMessage());
            var server = new NnrpServerSession(new ServerProfile(), transport);

            var failure = await server.AcceptAsync(CancellationToken.None);

            Assert.Equal(NnrpProtocolFailure.None, failure);
            Assert.Equal(NnrpHeader.CurrentWireFormat, server.NegotiatedWireFormat);
            Assert.True(ServerHelloAckMessage.TryParse(transport.Sent[0].ToArray(), out var ack, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(NnrpHeader.CurrentWireFormat, ack.Header.WireFormat);
            Assert.Equal((uint)NnrpHeader.CurrentWireFormat, ack.Metadata.SelectedWireFormat);
        }

        [Fact]
        public async Task AcceptAsyncDoesNotInventTransportAckWithoutKnownTransport()
        {
            var transport = new QueueTransport(
                CreateClientHello(
                    requestedSessionId: 41,
                    supportedWireFormatBitmap: ControlMetadataBitmaps.EncodeCurrentWireFormatBitmap(),
                    transportPolicy: TransportPolicy.Auto,
                    preferredTransportId: TransportId.Tcp).ToFramedMessage());
            var server = new NnrpServerSession(new ServerProfile(), transport);

            var failure = await server.AcceptAsync(CancellationToken.None);

            Assert.Equal(NnrpProtocolFailure.None, failure);
            Assert.True(ServerHelloAckMessage.TryParse(transport.Sent[0].ToArray(), out var ack, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.False(ack.TryGetServerTransportPolicyAckExtension(out _, out error));
            Assert.Equal(NnrpParseError.None, error);
        }

        [Fact]
        public async Task AcceptReceiveSubmitAndSendResultUseTypedMessages()
        {
            var transport = new QueueTransport(
                CreateClientHello(requestedSessionId: 41).ToFramedMessage(),
                CreateSmokeFrameSubmit(sessionId: 41, frameId: 303));
            var server = new NnrpServerSession(new ServerProfile(), transport);
            Assert.Equal(NnrpProtocolFailure.None, await server.AcceptAsync(CancellationToken.None));

            var submit = await server.ReceiveFrameSubmitAsync(CancellationToken.None);
            await server.SendResultAsync(CreateResultPush(sessionId: 41, frameId: 303), CancellationToken.None);

            Assert.Equal(303u, submit.Header.FrameId);
            Assert.Equal(2, transport.Sent.Count);
            Assert.Equal(MessageType.ResultPush, transport.Sent[1].Header.MessageType);
        }

        [Fact]
        public async Task AcceptReceiveMigrationAndSendAckUseTypedMessages()
        {
            var transport = new QueueTransport(
                CreateClientHello(
                    requestedSessionId: 41,
                    supportedWireFormatBitmap: ControlMetadataBitmaps.EncodeCurrentWireFormatBitmap()).ToFramedMessage(),
                CreateSessionMigrate(sessionId: 41, traceId: 55).ToFramedMessage());
            var server = new NnrpServerSession(new ServerProfile(), transport);
            Assert.Equal(NnrpProtocolFailure.None, await server.AcceptAsync(CancellationToken.None));

            var migrate = await server.ReceiveSessionMigrateAsync(CancellationToken.None);
            await server.SendSessionMigrateAckAsync(CreateSessionMigrateAck(sessionId: 41, traceId: 55), CancellationToken.None);

            Assert.Equal(TransportId.Tcp, migrate.Metadata.OldTransportId);
            Assert.Equal(TransportId.Quic, migrate.Metadata.NewTransportId);
            Assert.Equal(2, transport.Sent.Count);
            Assert.True(SessionMigrateAckMessage.TryParse(transport.Sent[1], out var ack, out var parseError));
            Assert.Equal(NnrpParseError.None, parseError);
            Assert.Equal(41u, ack.Header.SessionId);
            Assert.Equal(55ul, ack.Header.TraceId);
            Assert.Equal(77ul, ack.Metadata.ResumeFromFrameId);
        }

        [Fact]
        public async Task AcceptReceiveSubmitAndSendResultUseBusinessHelpersForCurrentStage()
        {
            var transport = new QueueTransport(
                CreateClientHello(requestedSessionId: 41).ToFramedMessage(),
                CreateSmokeFrameSubmit(sessionId: 41, frameId: 303));
            var server = new NnrpServerSession(new ServerProfile(), transport);
            Assert.Equal(NnrpProtocolFailure.None, await server.AcceptAsync(CancellationToken.None));

            var submit = await server.ReceiveSubmitAsync(CancellationToken.None);
            await server.SendResultAsync(CreateResult(frameId: submit.FrameId), CancellationToken.None);

            Assert.Equal(41u, submit.SessionId);
            Assert.Equal(303u, submit.FrameId);
            Assert.Equal(3, submit.TileIds.Length);
            Assert.Equal(2, transport.Sent.Count);
            Assert.True(ResultPushMessage.TryParse(transport.Sent[1], out var result, out var parseError));
            Assert.Equal(NnrpParseError.None, parseError);
            Assert.Equal(41u, result.Header.SessionId);
            Assert.Equal(303u, result.Header.FrameId);
            Assert.Equal(3, result.TileIds.Length);
        }

        [Fact]
        public async Task AcceptReceiveSubmitAndSendResultUseBusinessHelpers()
        {
            var transport = new QueueTransport(
                CreateClientHello(
                    requestedSessionId: 41,
                    supportedWireFormatBitmap: ControlMetadataBitmaps.EncodeCurrentWireFormatBitmap()).ToFramedMessage(),
                CreateSmokeFrameSubmit(sessionId: 41, frameId: 303));
            var server = new NnrpServerSession(new ServerProfile(), transport);
            Assert.Equal(NnrpProtocolFailure.None, await server.AcceptAsync(CancellationToken.None));

            var submit = await server.ReceiveSubmitAsync(CancellationToken.None);
            await server.SendResultAsync(CreateResult(frameId: submit.FrameId), CancellationToken.None);

            Assert.Equal(41u, submit.SessionId);
            Assert.Equal(303u, submit.FrameId);
            Assert.Equal(2, transport.Sent.Count);
            Assert.True(ResultPushMessage.TryParse(transport.Sent[1], out var result, out var parseError));
            Assert.Equal(NnrpParseError.None, parseError);
            Assert.Equal(NnrpHeader.CurrentWireFormat, result.Header.WireFormat);
            Assert.Equal(3, result.TileIds.Length);
            Assert.Equal<ushort>(3, result.Metadata.CoveredTileCount);
            Assert.Equal<ushort>(0, result.Metadata.DroppedTileCount);
        }

        [Fact]
        public async Task AcceptReceiveSubmitAndSendDegradedResultUseBusinessHelpersForCurrentStage()
        {
            var transport = new QueueTransport(
                CreateClientHello(
                    requestedSessionId: 41,
                    supportedWireFormatBitmap: ControlMetadataBitmaps.EncodeCurrentWireFormatBitmap()).ToFramedMessage(),
                CreateSmokeFrameSubmit(sessionId: 41, frameId: 303));
            var server = new NnrpServerSession(new ServerProfile(), transport);
            Assert.Equal(NnrpProtocolFailure.None, await server.AcceptAsync(CancellationToken.None));

            var submit = await server.ReceiveSubmitAsync(CancellationToken.None);
            await server.SendResultAsync(
                CreateResult(
                    frameId: submit.FrameId,
                    resultClass: ResultClass.Degraded,
                    appliedBudgetPolicy: BudgetPolicy.AllowDrop,
                    coveredTileCount: 3,
                    droppedTileCount: 0),
                CancellationToken.None);

            Assert.Equal(2, transport.Sent.Count);
            Assert.True(ResultPushMessage.TryParse(transport.Sent[1], out var result, out var parseError));
            Assert.Equal(NnrpParseError.None, parseError);
            Assert.Equal(NnrpHeader.CurrentWireFormat, result.Header.WireFormat);
            Assert.Equal(ResultClass.Degraded, result.Metadata.ResultClass);
            Assert.Equal(BudgetPolicy.AllowDrop, result.Metadata.AppliedBudgetPolicy);
            Assert.Equal<ushort>(3, result.Metadata.CoveredTileCount);
            Assert.Equal<ushort>(0, result.Metadata.DroppedTileCount);
        }

        [Fact]
        public async Task AcceptAsyncRejectsUnsupportedCapabilitiesAndSendsError()
        {
            var serverProfile = new ServerProfile { AcceptedCodecs = new[] { CodecId.Lz4 } };
            var transport = new QueueTransport(CreateClientHello(requestedSessionId: 11, supportedCodecs: new[] { CodecId.Raw }).ToFramedMessage());
            var server = new NnrpServerSession(serverProfile, transport);

            var failure = await server.AcceptAsync(CancellationToken.None);

            Assert.True(failure.IsFailure);
            Assert.Equal(ErrorCode.UnsupportedCapability, failure.ErrorCode);
            Assert.Equal(NnrpSessionState.Closed, server.State);
            Assert.Single(transport.Sent);
            Assert.True(ErrorMessage.TryParse(transport.Sent[0].ToArray(), out var error, out var parseError));
            Assert.Equal(NnrpParseError.None, parseError);
            Assert.Equal(ErrorCode.UnsupportedCapability, error.Metadata.ErrorCode);
        }

        [Fact]
        public async Task CloseAsyncSendsCloseFromActiveSession()
        {
            var transport = new QueueTransport(CreateClientHello(requestedSessionId: 41).ToFramedMessage());
            var server = new NnrpServerSession(new ServerProfile(), transport);
            Assert.Equal(NnrpProtocolFailure.None, await server.AcceptAsync(CancellationToken.None));

            var failure = await server.CloseAsync("server-done", traceId: 7, cancellationToken: CancellationToken.None);

            Assert.Equal(NnrpProtocolFailure.None, failure);
            Assert.Equal(NnrpSessionState.Draining, server.State);
            Assert.Equal(2, transport.Sent.Count);
            Assert.Equal(MessageType.Close, transport.Sent[1].Header.MessageType);
        }

        [Fact]
        public async Task SendResultDropAsyncSendsHeaderOnlyDropAndMarksFrameDropped()
        {
            var transport = new QueueTransport(
                CreateClientHello(requestedSessionId: 41).ToFramedMessage(),
                CreateSmokeFrameSubmit(sessionId: 41, frameId: 303));
            var server = new NnrpServerSession(new ServerProfile(), transport);
            Assert.Equal(NnrpProtocolFailure.None, await server.AcceptAsync(CancellationToken.None));
            await server.ReceiveFrameSubmitAsync(CancellationToken.None);

            var drop = ResultDropMessage.Create(sessionId: 41, frameId: 303, traceId: 99);
            await server.SendResultDropAsync(drop, CancellationToken.None);

            Assert.Equal(2, transport.Sent.Count);
            Assert.Equal(MessageType.ResultDrop, transport.Sent[1].Header.MessageType);
            Assert.Equal(41u, transport.Sent[1].Header.SessionId);
            Assert.Equal(303u, transport.Sent[1].Header.FrameId);
            Assert.True(ResultDropMessage.TryParse(transport.Sent[1].ToArray(), out _, out var error));
            Assert.Equal(NnrpParseError.None, error);
        }

        [Fact]
        public async Task HandleCachePutStoresAndAcknowledges()
        {
            var cacheStore = new NnrpCacheStore(maxEntries: 10, maxObjectBytes: 1024);
            var transport = new QueueTransport(
                CreateClientHello(requestedSessionId: 41).ToFramedMessage());
            var server = new NnrpServerSession(new ServerProfile(), transport, cacheStore: cacheStore);
            Assert.Equal(NnrpProtocolFailure.None, await server.AcceptAsync(CancellationToken.None));

            var put = CreateCachePut(sessionId: 41, namespaceId: 1, keyHigh: 100, keyLow: 200);
            await server.HandleCachePutAsync(put, CancellationToken.None);

            Assert.Equal(2, transport.Sent.Count);
            Assert.Equal(MessageType.CacheAck, transport.Sent[1].Header.MessageType);
            Assert.True(CacheAckMessage.TryParse(transport.Sent[1].ToArray(), out var ack, out _));
            Assert.Equal(CacheAckStatus.Accepted, ack.Metadata.Status);
            Assert.Equal(1, cacheStore.Count);
        }

        [Fact]
        public async Task HandleCachePutRejectsOversizedObject()
        {
            var cacheStore = new NnrpCacheStore(maxEntries: 10, maxObjectBytes: 5);
            var transport = new QueueTransport(
                CreateClientHello(requestedSessionId: 41).ToFramedMessage());
            var server = new NnrpServerSession(new ServerProfile(), transport, cacheStore: cacheStore);
            Assert.Equal(NnrpProtocolFailure.None, await server.AcceptAsync(CancellationToken.None));

            var put = CreateCachePut(sessionId: 41, namespaceId: 1, keyHigh: 100, keyLow: 200, objectBytes: new byte[10]);
            await server.HandleCachePutAsync(put, CancellationToken.None);

            Assert.Equal(2, transport.Sent.Count);
            Assert.True(CacheAckMessage.TryParse(transport.Sent[1].ToArray(), out var ack, out _));
            Assert.Equal(CacheAckStatus.Rejected, ack.Metadata.Status);
            Assert.Equal(0, cacheStore.Count);
        }

        [Fact]
        public async Task HandleCacheInvalidateRemovesAndAcknowledges()
        {
            var cacheStore = new NnrpCacheStore(maxEntries: 10, maxObjectBytes: 1024);
            var transport = new QueueTransport(
                CreateClientHello(requestedSessionId: 41).ToFramedMessage());
            var server = new NnrpServerSession(new ServerProfile(), transport, cacheStore: cacheStore);
            Assert.Equal(NnrpProtocolFailure.None, await server.AcceptAsync(CancellationToken.None));

            // First put to have something to invalidate
            var put = CreateCachePut(sessionId: 41, namespaceId: 1, keyHigh: 100, keyLow: 200);
            await server.HandleCachePutAsync(put, CancellationToken.None);
            Assert.Equal(1, cacheStore.Count);

            var invalidate = CreateCacheInvalidate(sessionId: 41, namespaceId: 1, keyHigh: 100, keyLow: 200);
            await server.HandleCacheInvalidateAsync(invalidate, CancellationToken.None);

            Assert.Equal(3, transport.Sent.Count);
            Assert.Equal(0, cacheStore.Count);
        }

        [Fact]
        public async Task CacheHandlingRejectedWhenStoreNotConfigured()
        {
            var transport = new QueueTransport(
                CreateClientHello(requestedSessionId: 41).ToFramedMessage());
            var server = new NnrpServerSession(new ServerProfile(), transport, cacheStore: null);
            Assert.Equal(NnrpProtocolFailure.None, await server.AcceptAsync(CancellationToken.None));

            var put = CreateCachePut(sessionId: 41, namespaceId: 1, keyHigh: 100, keyLow: 200);
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await server.HandleCachePutAsync(put, CancellationToken.None));

            var invalidate = CreateCacheInvalidate(sessionId: 41, namespaceId: 1, keyHigh: 100, keyLow: 200);
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await server.HandleCacheInvalidateAsync(invalidate, CancellationToken.None));
        }

        [Fact]
        public async Task CacheOpsRejectedWhenSessionNotActive()
        {
            var cacheStore = new NnrpCacheStore();
            var transport = new QueueTransport();
            var server = new NnrpServerSession(new ServerProfile(), transport, cacheStore: cacheStore);

            var put = CreateCachePut(sessionId: 1, namespaceId: 1, keyHigh: 1, keyLow: 1);
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await server.HandleCachePutAsync(put, CancellationToken.None));
        }

        [Fact]
        public async Task CacheOpsRejectedOnSessionIdMismatch()
        {
            var cacheStore = new NnrpCacheStore();
            var transport = new QueueTransport(
                CreateClientHello(requestedSessionId: 41).ToFramedMessage());
            var server = new NnrpServerSession(new ServerProfile(), transport, cacheStore: cacheStore);
            Assert.Equal(NnrpProtocolFailure.None, await server.AcceptAsync(CancellationToken.None));

            var put = CreateCachePut(sessionId: 99, namespaceId: 1, keyHigh: 1, keyLow: 1);
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await server.HandleCachePutAsync(put, CancellationToken.None));
        }

        [Fact]
        public async Task SendResultDropRejectedOnInvalidSessionState()
        {
            var transport = new QueueTransport();
            var server = new NnrpServerSession(new ServerProfile(), transport);
            var drop = ResultDropMessage.Create(sessionId: 1, frameId: 1);
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await server.SendResultDropAsync(drop, CancellationToken.None));
        }

        [Fact]
        public async Task SendResultDropRejectedOnSessionIdMismatch()
        {
            var transport = new QueueTransport(
                CreateClientHello(requestedSessionId: 41).ToFramedMessage());
            var server = new NnrpServerSession(new ServerProfile(), transport);
            Assert.Equal(NnrpProtocolFailure.None, await server.AcceptAsync(CancellationToken.None));

            var drop = ResultDropMessage.Create(sessionId: 99, frameId: 303);
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await server.SendResultDropAsync(drop, CancellationToken.None));
        }

        [Fact]
        public void ServerSessionRejectsNullProfile()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new NnrpServerSession(null!, new QueueTransport()));
        }

        [Fact]
        public void ServerSessionRejectsNullTransport()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new NnrpServerSession(new ServerProfile(), null!));
        }

        [Fact]
        public async Task ServerSessionUsesCustomSessionIdAllocator()
        {
            var transport = new QueueTransport(
                CreateClientHello(requestedSessionId: 99).ToFramedMessage());
            var server = new NnrpServerSession(
                new ServerProfile(),
                transport,
                sessionIdAllocator: requested => requested + 1000);
            Assert.Equal(NnrpProtocolFailure.None, await server.AcceptAsync(CancellationToken.None));
            Assert.Equal(1099u, server.SessionId);
        }

        [Fact]
        public async Task AcceptAsyncHandlesNonClientHelloFirstMessage()
        {
            var transport = new QueueTransport(
                CloseMessage.Create(sessionId: 0, "oops", traceId: 0).ToFramedMessage());
            var server = new NnrpServerSession(new ServerProfile(), transport);

            var failure = await server.AcceptAsync(CancellationToken.None);
            Assert.True(failure.IsFailure);
            Assert.Equal(ErrorCode.MalformedBody, failure.ErrorCode);
            Assert.Equal(NnrpSessionState.Closed, server.State);
            Assert.Single(transport.Sent);
        }

        [Fact]
        public async Task ReceiveFrameSubmitRejectsPeerCloseWrongTypeAndSessionMismatch()
        {
            var closeTransport = new QueueTransport(
                CreateClientHello(requestedSessionId: 41).ToFramedMessage(),
                CloseMessage.Create(sessionId: 41, "peer-closed", traceId: 7).ToFramedMessage());
            var closeServer = new NnrpServerSession(new ServerProfile(), closeTransport);
            Assert.Equal(NnrpProtocolFailure.None, await closeServer.AcceptAsync(CancellationToken.None));
            Assert.Contains("Peer closed session", (await Assert.ThrowsAsync<InvalidOperationException>(() =>
                closeServer.ReceiveFrameSubmitAsync(CancellationToken.None).AsTask())).Message, StringComparison.Ordinal);

            var wrongTypeTransport = new QueueTransport(
                CreateClientHello(requestedSessionId: 41).ToFramedMessage(),
                PingMessage.Create(sessionId: 41, traceId: 1).ToFramedMessage());
            var wrongTypeServer = new NnrpServerSession(new ServerProfile(), wrongTypeTransport);
            Assert.Equal(NnrpProtocolFailure.None, await wrongTypeServer.AcceptAsync(CancellationToken.None));
            Assert.Contains("Expected FRAME_SUBMIT", (await Assert.ThrowsAsync<InvalidOperationException>(() =>
                wrongTypeServer.ReceiveFrameSubmitAsync(CancellationToken.None).AsTask())).Message, StringComparison.Ordinal);

            var mismatchTransport = new QueueTransport(
                CreateClientHello(requestedSessionId: 41).ToFramedMessage(),
                CreateSmokeFrameSubmit(sessionId: 99, frameId: 303));
            var mismatchServer = new NnrpServerSession(new ServerProfile(), mismatchTransport);
            Assert.Equal(NnrpProtocolFailure.None, await mismatchServer.AcceptAsync(CancellationToken.None));
            Assert.Contains("does not match server session_id 41", (await Assert.ThrowsAsync<InvalidOperationException>(() =>
                mismatchServer.ReceiveFrameSubmitAsync(CancellationToken.None).AsTask())).Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task ReceiveFrameSubmitRejectsFrameLifecycleConflicts()
        {
            var transport = new QueueTransport(
                CreateClientHello(requestedSessionId: 41).ToFramedMessage(),
                CreateSmokeFrameSubmit(sessionId: 41, frameId: 303),
                CreateSmokeFrameSubmit(sessionId: 41, frameId: 303));
            var server = new NnrpServerSession(new ServerProfile(), transport);
            Assert.Equal(NnrpProtocolFailure.None, await server.AcceptAsync(CancellationToken.None));
            Assert.Equal(303u, (await server.ReceiveFrameSubmitAsync(CancellationToken.None)).Header.FrameId);

            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => server.ReceiveFrameSubmitAsync(CancellationToken.None).AsTask());
            Assert.Contains("frame lifecycle", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task SendResultAsyncRejectsInvalidStateSessionMismatchAndLifecycleConflicts()
        {
            var inactiveServer = new NnrpServerSession(new ServerProfile(), new QueueTransport());
            var push = CreateResultPush(sessionId: 41, frameId: 303);
            Assert.Contains("while session is Init", (await Assert.ThrowsAsync<InvalidOperationException>(() =>
                inactiveServer.SendResultAsync(push, CancellationToken.None).AsTask())).Message, StringComparison.Ordinal);

            var mismatchTransport = new QueueTransport(CreateClientHello(requestedSessionId: 41).ToFramedMessage());
            var mismatchServer = new NnrpServerSession(new ServerProfile(), mismatchTransport);
            Assert.Equal(NnrpProtocolFailure.None, await mismatchServer.AcceptAsync(CancellationToken.None));
            Assert.Contains("does not match server session_id 41", (await Assert.ThrowsAsync<InvalidOperationException>(() =>
                mismatchServer.SendResultAsync(CreateResultPush(sessionId: 99, frameId: 303), CancellationToken.None).AsTask())).Message, StringComparison.Ordinal);

            var lifecycleTransport = new QueueTransport(CreateClientHello(requestedSessionId: 41).ToFramedMessage());
            var lifecycleServer = new NnrpServerSession(new ServerProfile(), lifecycleTransport);
            Assert.Equal(NnrpProtocolFailure.None, await lifecycleServer.AcceptAsync(CancellationToken.None));
            Assert.Contains("frame lifecycle", (await Assert.ThrowsAsync<InvalidOperationException>(() =>
                lifecycleServer.SendResultAsync(push, CancellationToken.None).AsTask())).Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task HandleCachePutNormalizesTtlAndInvalidateChecksSessionState()
        {
            var cacheStore = new NnrpCacheStore(maxEntries: 10, maxObjectBytes: 1024);
            var transport = new QueueTransport(CreateClientHello(requestedSessionId: 41).ToFramedMessage());
            var server = new NnrpServerSession(new ServerProfile(), transport, cacheStore: cacheStore);
            Assert.Equal(NnrpProtocolFailure.None, await server.AcceptAsync(CancellationToken.None));

            var zeroTtlPut = CreateCachePut(sessionId: 41, namespaceId: 1, keyHigh: 100, keyLow: 200);
            zeroTtlPut = new CachePutMessage(
                zeroTtlPut.Header,
                new CachePutMetadata(
                    zeroTtlPut.Metadata.CacheNamespace,
                    zeroTtlPut.Metadata.CacheKeyHigh,
                    zeroTtlPut.Metadata.CacheKeyLow,
                    zeroTtlPut.Metadata.ObjectKind,
                    ttlMilliseconds: 0,
                    zeroTtlPut.Metadata.ObjectBytes,
                    zeroTtlPut.Metadata.CodecBitmap,
                    zeroTtlPut.Metadata.Flags),
                zeroTtlPut.ObjectBytes.ToArray());
            await server.HandleCachePutAsync(zeroTtlPut, CancellationToken.None);
            Assert.True(CacheAckMessage.TryParse(transport.Sent[1].ToArray(), out var ack, out _));
            Assert.Equal(1000u, ack.Metadata.AcceptedTtlMilliseconds);

            var inactiveServer = new NnrpServerSession(new ServerProfile(), new QueueTransport(), cacheStore: new NnrpCacheStore());
            var invalidate = CreateCacheInvalidate(sessionId: 41, namespaceId: 1, keyHigh: 100, keyLow: 200);
            Assert.Contains("while session is Init", (await Assert.ThrowsAsync<InvalidOperationException>(() =>
                inactiveServer.HandleCacheInvalidateAsync(invalidate, CancellationToken.None).AsTask())).Message, StringComparison.Ordinal);

            var mismatchInvalidate = CreateCacheInvalidate(sessionId: 99, namespaceId: 1, keyHigh: 100, keyLow: 200);
            Assert.Contains("does not match server session_id 41", (await Assert.ThrowsAsync<InvalidOperationException>(() =>
                server.HandleCacheInvalidateAsync(mismatchInvalidate, CancellationToken.None).AsTask())).Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task CloseAsyncOnNonActiveSessionDoesNotSendTransportMessageAndSecondCloseFails()
        {
            var negotiatingTransport = new QueueTransport();
            var negotiatingServer = new NnrpServerSession(new ServerProfile(), negotiatingTransport);

            var negotiatingFailure = await negotiatingServer.CloseAsync("no-send", traceId: 1, cancellationToken: CancellationToken.None);

            Assert.Equal(NnrpProtocolFailure.None, negotiatingFailure);
            Assert.Empty(negotiatingTransport.Sent);

            var activeTransport = new QueueTransport(CreateClientHello(requestedSessionId: 41).ToFramedMessage());
            var activeServer = new NnrpServerSession(new ServerProfile(), activeTransport);
            Assert.Equal(NnrpProtocolFailure.None, await activeServer.AcceptAsync(CancellationToken.None));
            Assert.Equal(NnrpProtocolFailure.None, await activeServer.CloseAsync("first", traceId: 1, cancellationToken: CancellationToken.None));
            Assert.Equal(NnrpProtocolFailure.None, await activeServer.CloseAsync("second", traceId: 2, cancellationToken: CancellationToken.None));

            var thirdFailure = await activeServer.CloseAsync("third", traceId: 3, cancellationToken: CancellationToken.None);
            Assert.True(thirdFailure.IsFailure);
        }

        [Fact]
        public void ServerProfileRejectsInvalidValues()
        {
            var profile = new ServerProfile { MaxConcurrentFrames = 0 };
            Assert.False(profile.TryValidate(out var error));
            Assert.Contains(nameof(ServerProfile.MaxConcurrentFrames), error, StringComparison.Ordinal);

            profile.MaxConcurrentFrames = 1;
            profile.AcceptedCodecs = new[] { CodecId.Raw, CodecId.Raw };
            Assert.False(profile.TryValidate(out error));
            Assert.Contains("duplicate", error, StringComparison.Ordinal);
        }

        [Fact]
        public void ServerProfileCreateServerHelloAckRejectsFailedNegotiation()
        {
            var profile = new ServerProfile();
            var rejected = NnrpCapabilityNegotiationResult.Rejected(
                ErrorCode.UnsupportedCapability,
                CapabilityRejectionReason.NoCommonCodec,
                "No common codec.");
            Assert.Throws<ArgumentException>(() =>
                profile.CreateServerHelloAck(sessionId: 1, rejected, traceId: 0));
        }

        private static CachePutMessage CreateCachePut(
            uint sessionId,
            ushort namespaceId,
            uint keyHigh,
            uint keyLow,
            byte[]? objectBytes = null)
        {
            objectBytes ??= new byte[] { 0xAA };
            var metadata = new CachePutMetadata(
                cacheNamespace: namespaceId,
                cacheKeyHigh: keyHigh,
                cacheKeyLow: keyLow,
                objectKind: CacheObjectKind.CodecAuxBlock,
                ttlMilliseconds: 5000,
                objectBytes: (uint)objectBytes.Length,
                codecBitmap: 0);
            var header = new NnrpHeader(
                versionMajor: NnrpHeader.CurrentVersionMajor,
                wireFormat: NnrpHeader.CurrentWireFormat,
                messageType: MessageType.CachePut,
                flags: HeaderFlags.None,
                metaLength: CachePutMetadata.MetadataLength,
                bodyLength: (uint)objectBytes.Length,
                sessionId: sessionId,
                frameId: 0, viewId: 0, routeId: 0, traceId: 0);
            return new CachePutMessage(header, metadata, objectBytes);
        }

        private static CacheInvalidateMessage CreateCacheInvalidate(
            uint sessionId,
            ushort namespaceId,
            uint keyHigh,
            uint keyLow)
        {
            var metadata = new CacheInvalidateMetadata(
                invalidateScope: CacheInvalidateScope.Entry,
                cacheNamespace: namespaceId,
                cacheKeyHigh: keyHigh,
                cacheKeyLow: keyLow,
                reasonCode: 0);
            var header = new NnrpHeader(
                versionMajor: NnrpHeader.CurrentVersionMajor,
                wireFormat: NnrpHeader.CurrentWireFormat,
                messageType: MessageType.CacheInvalidate,
                flags: HeaderFlags.None,
                metaLength: CacheInvalidateMetadata.MetadataLength,
                bodyLength: 0,
                sessionId: sessionId,
                frameId: 0, viewId: 0, routeId: 0, traceId: 0);
            return new CacheInvalidateMessage(header, metadata);
        }

        private static ClientHelloMessage CreateClientHello(
            uint requestedSessionId,
            uint? supportedWireFormatBitmap = null,
            CodecId[]? supportedCodecs = null,
            DTypeId[]? supportedDTypes = null,
            TensorLayoutId[]? supportedTensorLayouts = null,
            TransportPolicy? transportPolicy = null,
            TransportId preferredTransportId = TransportId.Unspecified)
        {
            supportedCodecs ??= new[] { CodecId.Raw };
            supportedDTypes ??= new[] { DTypeId.UInt8, DTypeId.Float16 };
            supportedTensorLayouts ??= new[] { TensorLayoutId.Nhwc };
            var extensions = transportPolicy.HasValue
                ? new[] { new ClientTransportPolicyExtension(transportPolicy.Value, preferredTransportId).ToControlExtension() }
                : Array.Empty<ControlExtensionBlock>();
            var extensionBytes = GetExtensionBodyLength(extensions);

            var metadata = new ClientHelloMetadata(
                minVersionMajor: NnrpHeader.CurrentVersionMajor,
                maxVersionMajor: NnrpHeader.CurrentVersionMajor,
                supportedWireFormatBitmap: supportedWireFormatBitmap ?? ControlMetadataBitmaps.EncodeCurrentWireFormatBitmap(),
                supportedProfileBitmap: ControlMetadataBitmaps.TensorProfileBitmap,
                supportedPayloadKindBitmap: ControlMetadataBitmaps.TensorPayloadKindBitmap,
                supportedCodecBitmap: ControlMetadataBitmaps.EncodeCodecBitmap(supportedCodecs),
                supportedCompressionBitmap: ControlMetadataBitmaps.EncodeCodecBitmap(supportedCodecs),
                supportedDTypeBitmap: ControlMetadataBitmaps.EncodeDTypeBitmap(supportedDTypes),
                supportedLayoutBitmap: ControlMetadataBitmaps.EncodeTensorLayoutBitmap(supportedTensorLayouts),
                cacheDigestBitmap: ControlMetadataBitmaps.CacheDigestBitmap,
                cacheObjectBitmap: ControlMetadataBitmaps.TensorProfileCacheObjectBitmap,
                cacheNamespaceCount: 1,
                maxLaneCount: 1,
                maxCacheEntries: 128,
                maxCacheBytes: ControlMetadataBitmaps.DefaultCacheBytes,
                targetCadenceX100: 6000,
                latencyBudgetMilliseconds: 100,
                qualityTier: ControlMetadataBitmaps.DefaultQualityTier,
                degradePolicy: 0,
                requestedSessionId: requestedSessionId,
                authBytes: 0,
                controlExtensionBytes: (uint)extensionBytes);
            var header = new NnrpHeader(
                versionMajor: NnrpHeader.CurrentVersionMajor,
                wireFormat: NnrpHeader.CurrentWireFormat,
                messageType: MessageType.ClientHello,
                flags: HeaderFlags.AckRequired,
                metaLength: ClientHelloMetadata.MetadataLength,
                bodyLength: (uint)extensionBytes,
                sessionId: 0,
                frameId: 0,
                viewId: 0,
                routeId: 0,
                traceId: 0);
            return new ClientHelloMessage(header, metadata, Array.Empty<byte>(), extensions);
        }

        private static NnrpFramedMessage CreateSmokeFrameSubmit(uint sessionId, uint frameId, ushort viewId = 0, ulong traceId = 0)
        {
            var packet = SmokePackets.BuildSmokeFrameSubmitPacket(sessionId, frameId, viewId, traceId);
            Assert.True(NnrpFramedMessage.TryParse(packet, NnrpHeaderParseOptions.Strict, out var framed, out var error));
            Assert.Equal(NnrpParseError.None, error);
            return framed;
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

        private static ResultPushMessage CreateResultPush(uint sessionId, uint frameId)
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
                resultFlags: ResultFlags.None,
                sectionCount: 1,
                tileCount: 3,
                activeProfileId: 1,
                inferenceMilliseconds: 2,
                queueMilliseconds: 1,
                serverTotalMilliseconds: 4,
                tileBaseId: 0,
                tileIndexBytes: 6);
            var header = new NnrpHeader(
                versionMajor: NnrpHeader.CurrentVersionMajor,
                wireFormat: NnrpHeader.CurrentWireFormat,
                messageType: MessageType.ResultPush,
                flags: HeaderFlags.None,
                metaLength: ResultPushMetadata.MetadataLength,
                bodyLength: (uint)(BinaryAlignment.AlignUp(TensorResultBlock.BlockLength + 6, 8) + section.TotalLength),
                sessionId: sessionId,
                frameId: frameId,
                viewId: 0,
                routeId: 0,
                traceId: 0);
            return new ResultPushMessage(header, metadata, new ushort[] { 0, 1, 2 }, new[] { section });
        }

        private static SessionMigrateMessage CreateSessionMigrate(uint sessionId, ulong traceId)
        {
            return new SessionMigrateMessage(
                new NnrpHeader(
                    versionMajor: NnrpHeader.CurrentVersionMajor,
                    wireFormat: NnrpHeader.CurrentWireFormat,
                    messageType: MessageType.SessionMigrate,
                    flags: HeaderFlags.None,
                    metaLength: SessionMigrateMetadata.MetadataLength,
                    bodyLength: 0,
                    sessionId: sessionId,
                    frameId: 0,
                    viewId: 0,
                    routeId: 0,
                    traceId: traceId),
                new SessionMigrateMetadata(TransportId.Tcp, TransportId.Quic, 77, 123456789));
        }

        private static SessionMigrateAckMessage CreateSessionMigrateAck(uint sessionId, ulong traceId)
        {
            return new SessionMigrateAckMessage(
                new NnrpHeader(
                    versionMajor: NnrpHeader.CurrentVersionMajor,
                    wireFormat: NnrpHeader.CurrentWireFormat,
                    messageType: MessageType.SessionMigrateAck,
                    flags: HeaderFlags.None,
                    metaLength: SessionMigrateAckMetadata.MetadataLength,
                    bodyLength: 0,
                    sessionId: sessionId,
                    frameId: 0,
                    viewId: 0,
                    routeId: 0,
                    traceId: traceId),
                new SessionMigrateAckMetadata(acceptCode: 0, resumeFromFrameId: 77, graceWindowMilliseconds: 250, serverMigrateTimestampMicroseconds: 123456889));
        }

        private static NnrpResult CreateResult(
            uint frameId,
            ResultClass resultClass = ResultClass.Complete,
            BudgetPolicy appliedBudgetPolicy = BudgetPolicy.None,
            ushort coveredTileCount = 0,
            ushort droppedTileCount = 0,
            ResultFlags resultFlags = ResultFlags.None,
            uint reusedFrameId = 0)
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
                resultFlags: resultFlags,
                inferenceMilliseconds: 2,
                queueMilliseconds: 1,
                serverTotalMilliseconds: 4,
                resultClass: resultClass,
                appliedBudgetPolicy: appliedBudgetPolicy,
                reusedFrameId: reusedFrameId,
                coveredTileCount: coveredTileCount,
                droppedTileCount: droppedTileCount);
        }

        private sealed class QueueTransport : INnrpMessageTransport
        {
            private readonly Queue<NnrpFramedMessage> inbound;

            public QueueTransport(params NnrpFramedMessage[] inbound)
            {
                this.inbound = new Queue<NnrpFramedMessage>(inbound ?? Array.Empty<NnrpFramedMessage>());
            }

            public List<NnrpFramedMessage> Sent { get; } = new List<NnrpFramedMessage>();

            public ValueTask SendAsync(NnrpFramedMessage message, CancellationToken cancellationToken)
            {
                Sent.Add(message);
                return default;
            }

            public ValueTask<NnrpFramedMessage> ReceiveAsync(CancellationToken cancellationToken)
            {
                if (inbound.Count == 0)
                {
                    throw new InvalidOperationException("No inbound messages queued.");
                }

                return new ValueTask<NnrpFramedMessage>(inbound.Dequeue());
            }
        }
    }
}
