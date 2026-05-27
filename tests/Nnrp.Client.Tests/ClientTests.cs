using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nnrp.Client;
using Nnrp.Core;
using Xunit;

namespace Nnrp.Client.Tests
{
    public sealed class ClientTests
    {
        [Fact]
        public async Task ConnectSubmitAndCloseUseTypedMessages()
        {
            var transport = new QueueTransport(
                CreateServerHelloAck(sessionId: 41, wireFormat: NnrpHeader.CurrentWireFormat).ToFramedMessage(),
                CreateResultPush(sessionId: 41, frameId: 303).ToFramedMessage());
            var client = new NnrpClient(new ClientProfile(), transport);

            var connectResult = await client.ConnectAsync(requestedSessionId: 41, cancellationToken: CancellationToken.None);
            var submitMessage = SmokePackets.CreateSmokeFrameSubmitMessage(sessionId: 41, frameId: 303);
            var result = await client.SubmitAsync(submitMessage, CancellationToken.None);
            var closeFailure = await client.CloseAsync("done", cancellationToken: CancellationToken.None);

            Assert.True(connectResult.IsConnected);
            Assert.Equal(41u, client.NegotiatedSessionId);
            Assert.Equal(3, transport.Sent.Count);

            Assert.True(ClientHelloMessage.TryParse(transport.Sent[0].ToArray(), out var hello, out var helloError));
            Assert.Equal(NnrpParseError.None, helloError);
            Assert.Equal(MessageType.FrameSubmit, transport.Sent[1].Header.MessageType);
            Assert.Equal(303u, result.Header.FrameId);
            Assert.Equal(MessageType.Close, transport.Sent[2].Header.MessageType);
            Assert.Equal(NnrpProtocolFailure.None, closeFailure);
            Assert.Equal(NnrpSessionState.Draining, client.Session.State);
        }

        [Fact]
        public async Task SubmitAsyncBuildsSubmitRequestWithoutWireMessageAssembly()
        {
            var transport = new QueueTransport(
                CreateServerHelloAck(sessionId: 41, wireFormat: NnrpHeader.CurrentWireFormat).ToFramedMessage(),
                CreateResultPush(sessionId: 41, frameId: 303).ToFramedMessage());
            var client = new NnrpClient(new ClientProfile(), transport);
            await client.ConnectAsync(requestedSessionId: 41, cancellationToken: CancellationToken.None);

            var result = await client.SubmitAsync(CreateSubmitRequest(frameId: 303), CancellationToken.None);

            Assert.Equal(41u, result.SessionId);
            Assert.Equal(303u, result.FrameId);
            Assert.Equal(ResultStatusCode.Success, result.StatusCode);
            Assert.Equal(new ushort[] { 0, 1, 2 }, result.TileIds.ToArray());
            Assert.Single(result.Sections.ToArray());
            Assert.True(NnrpFramedMessage.TryParse(transport.Sent[1].ToArray(), NnrpHeaderParseOptions.Strict, out var submit, out var parseError));
            Assert.Equal(NnrpParseError.None, parseError);
            Assert.Equal(41u, submit.Header.SessionId);
            Assert.Equal(303u, submit.Header.FrameId);
            if (submit.Header.WireFormat == NnrpHeader.CurrentWireFormat)
            {
                Assert.Equal<uint>((uint)FrameSubmitMetadata.MetadataLength, submit.Header.MetaLength);
                Assert.True(FrameSubmitMetadata.TryParse(submit.Metadata.Span, strict: true, out var submitMetadata, out parseError));
                Assert.Equal(NnrpParseError.None, parseError);
                Assert.Equal(SubmitMode.Inline, submitMetadata.SubmitMode);
                Assert.Equal((ushort)3, submitMetadata.TileCount);
            }
            else
            {
                Assert.Equal(NnrpHeader.CurrentWireFormat, submit.Header.WireFormat);
                Assert.True(FrameSubmitMessage.TryParse(transport.Sent[1], out var currentSubmit, out parseError));
                Assert.Equal(NnrpParseError.None, parseError);
                Assert.Equal(3, currentSubmit.TileIds.Length);
            }
        }

        [Fact]
        public async Task SubmitAsyncPreservesPartialResultMetadata()
        {
            var transport = new QueueTransport(
                CreateServerHelloAck(sessionId: 41, wireFormat: NnrpHeader.CurrentWireFormat).ToFramedMessage(),
                CreateResultPush(
                    sessionId: 41,
                    frameId: 303,
                    wireFormat: NnrpHeader.CurrentWireFormat,
                    resultClass: ResultClass.Partial,
                    resultFlags: ResultFlags.Partial,
                    appliedBudgetPolicy: BudgetPolicy.AllowPartial | BudgetPolicy.AllowDrop,
                    coveredTileCount: 2,
                    droppedTileCount: 1).ToFramedMessage());
            var client = new NnrpClient(new ClientProfile(), transport);
            await client.ConnectAsync(requestedSessionId: 41, cancellationToken: CancellationToken.None);

            var result = await client.SubmitAsync(CreateSubmitRequest(frameId: 303), CancellationToken.None);

            Assert.Equal(ResultClass.Partial, result.ResultClass);
            Assert.Equal(ResultFlags.Partial, result.ResultFlags);
            Assert.Equal(BudgetPolicy.AllowPartial | BudgetPolicy.AllowDrop, result.AppliedBudgetPolicy);
            Assert.Equal<ushort>(2, result.CoveredTileCount);
            Assert.Equal<ushort>(1, result.DroppedTileCount);
            Assert.Equal(PayloadKind.Tensor, result.PayloadKindBitmap);
            Assert.Equal<ushort>(0, result.PayloadFrameCount);
        }

        [Fact]
        public async Task SubmitAsyncPreservesTypedPayloadFrames()
        {
            var descriptors = new[]
            {
                new TypedPayloadDescriptor(
                    PayloadKind.ToolDelta,
                    descriptorFlags: 0,
                    profileId: 9,
                    payloadOffset: 0,
                    payloadLength: 2,
                    reserved: 0),
                new TypedPayloadDescriptor(
                    PayloadKind.ToolDelta,
                    descriptorFlags: 0,
                    profileId: 9,
                    payloadOffset: 2,
                    payloadLength: 3,
                    reserved: 0),
                new TypedPayloadDescriptor(
                    PayloadKind.ToolDelta,
                    descriptorFlags: 0,
                    profileId: 5,
                    payloadOffset: 5,
                    payloadLength: 4,
                    reserved: 0)
            };
            var payloadRegion = new byte[] { 0x41, 0x42, 0x43, 0x44, 0x45, 0x90, 0x91, 0x92, 0x93 };
            var transport = new QueueTransport(
                CreateServerHelloAck(sessionId: 41, wireFormat: NnrpHeader.CurrentWireFormat).ToFramedMessage(),
                CreateResultPush(
                    sessionId: 41,
                    frameId: 303,
                    wireFormat: NnrpHeader.CurrentWireFormat,
                    payloadKindBitmap: PayloadKind.Tensor | PayloadKind.ToolDelta,
                    payloadFrameCount: 3,
                    typedPayloadDescriptors: descriptors,
                    typedPayloadFrameRegion: payloadRegion).ToFramedMessage());
            var client = new NnrpClient(new ClientProfile(), transport);
            await client.ConnectAsync(requestedSessionId: 41, cancellationToken: CancellationToken.None);

            var result = await client.SubmitAsync(CreateSubmitRequest(frameId: 303), CancellationToken.None);

            Assert.Equal(PayloadKind.Tensor | PayloadKind.ToolDelta, result.PayloadKindBitmap);
            Assert.Equal<ushort>(3, result.PayloadFrameCount);
            Assert.Equal(3, result.TypedPayloadFrames.Length);

            var toolFrames = result.GetTypedPayloadFrames(PayloadKind.ToolDelta, 9);
            Assert.Equal(2, toolFrames.Length);
            Assert.Equal(new byte[] { 0x41, 0x42 }, toolFrames[0].Payload.ToArray());
            Assert.Equal(new byte[] { 0x43, 0x44, 0x45 }, toolFrames[1].Payload.ToArray());

            var alternateToolFrames = result.GetTypedPayloadFrames(PayloadKind.ToolDelta, 5);
            Assert.Single(alternateToolFrames);
            Assert.Equal(new byte[] { 0x90, 0x91, 0x92, 0x93 }, alternateToolFrames[0].Payload.ToArray());
            Assert.Empty(result.GetTypedPayloadFrames(PayloadKind.StructuredEvent, 5));
        }

        [Fact]
        public async Task SubmitAsyncPreservesToolDeltaAndOpaqueBytesWrappers()
        {
            var descriptors = new[]
            {
                new TypedPayloadDescriptor(
                    PayloadKind.ToolDelta,
                    descriptorFlags: 0,
                    profileId: 9,
                    payloadOffset: 0,
                    payloadLength: 2,
                    reserved: 0),
                new TypedPayloadDescriptor(
                    PayloadKind.ToolDelta,
                    descriptorFlags: 0,
                    profileId: 9,
                    payloadOffset: 2,
                    payloadLength: 3,
                    reserved: 0)
            };
            var opaqueDescriptors = new[]
            {
                new TypedPayloadDescriptor(
                    PayloadKind.OpaqueBytes,
                    descriptorFlags: 0,
                    profileId: 12,
                    payloadOffset: 0,
                    payloadLength: 4,
                    reserved: 0)
            };
            var payloadRegion = new byte[] { 0x41, 0x42, 0x43, 0x44, 0x45 };
            var opaquePayloadRegion = new byte[] { 0x90, 0x91, 0x92, 0x93 };
            var transport = new QueueTransport(
                CreateServerHelloAck(sessionId: 41, wireFormat: NnrpHeader.CurrentWireFormat).ToFramedMessage(),
                CreateResultPush(
                    sessionId: 41,
                    frameId: 303,
                    wireFormat: NnrpHeader.CurrentWireFormat,
                    payloadKindBitmap: PayloadKind.Tensor | PayloadKind.ToolDelta,
                    payloadFrameCount: 2,
                    typedPayloadDescriptors: descriptors,
                    typedPayloadFrameRegion: payloadRegion).ToFramedMessage(),
                CreateResultPush(
                    sessionId: 41,
                    frameId: 304,
                    wireFormat: NnrpHeader.CurrentWireFormat,
                    payloadKindBitmap: PayloadKind.Tensor | PayloadKind.OpaqueBytes,
                    payloadFrameCount: 1,
                    typedPayloadDescriptors: opaqueDescriptors,
                    typedPayloadFrameRegion: opaquePayloadRegion).ToFramedMessage());
            var client = new NnrpClient(new ClientProfile(), transport);
            await client.ConnectAsync(requestedSessionId: 41, cancellationToken: CancellationToken.None);

            var result = await client.SubmitAsync(CreateSubmitRequest(frameId: 303), CancellationToken.None);

            var toolFrames = result.GetToolDeltaFrames(9);
            Assert.Equal(PayloadKind.ToolDelta, toolFrames.PayloadKind);
            Assert.Equal((ushort)9, toolFrames.ProfileId);
            Assert.Equal(2, toolFrames.FrameCount);
            Assert.Equal(5, toolFrames.PayloadBytes);
            Assert.Equal(new byte[] { 0x41, 0x42 }, toolFrames.Frames.Span[0].Payload.ToArray());
            Assert.Equal(new byte[] { 0x43, 0x44, 0x45 }, toolFrames.Frames.Span[1].Payload.ToArray());

            var opaqueResult = await client.SubmitAsync(CreateSubmitRequest(frameId: 304), CancellationToken.None);

            var opaqueFrames = opaqueResult.GetOpaqueBytesFrames(12);
            Assert.Equal(PayloadKind.OpaqueBytes, opaqueFrames.PayloadKind);
            Assert.Equal((ushort)12, opaqueFrames.ProfileId);
            Assert.Equal(1, opaqueFrames.FrameCount);
            Assert.Equal(4, opaqueFrames.PayloadBytes);
            Assert.Equal(new byte[] { 0x90, 0x91, 0x92, 0x93 }, opaqueFrames.Frames.Span[0].Payload.ToArray());
        }

        [Fact]
        public async Task SubmitAsyncPreservesTokenTypedPayloadFrames()
        {
            var descriptors = new[]
            {
                new TypedPayloadDescriptor(
                    PayloadKind.TokenChunk,
                    TypedPayloadDescriptor.ProfileToken,
                    descriptorFlags: 0x0002,
                    schemaId: TypedPayloadDescriptor.TokenDeltaSchemaId,
                    schemaVersion: TypedPayloadDescriptor.TokenDeltaSchemaVersion,
                    streamSemantics: TypedPayloadDescriptor.StreamSemanticsAppend,
                    payloadOffset: 0,
                    payloadLength: 2),
                new TypedPayloadDescriptor(
                    PayloadKind.TokenChunk,
                    TypedPayloadDescriptor.ProfileToken,
                    descriptorFlags: 0x0002,
                    schemaId: TypedPayloadDescriptor.TokenDeltaSchemaId,
                    schemaVersion: TypedPayloadDescriptor.TokenDeltaSchemaVersion,
                    streamSemantics: TypedPayloadDescriptor.StreamSemanticsAppend,
                    payloadOffset: 2,
                    payloadLength: 3),
                new TypedPayloadDescriptor(
                    PayloadKind.TokenChunk,
                    TypedPayloadDescriptor.ProfileToken,
                    descriptorFlags: 0x0001,
                    schemaId: TypedPayloadDescriptor.TokenDeltaSchemaId,
                    schemaVersion: TypedPayloadDescriptor.TokenDeltaSchemaVersion,
                    streamSemantics: TypedPayloadDescriptor.StreamSemanticsAppend,
                    payloadOffset: 5,
                    payloadLength: 4)
            };
            var payloadRegion = new byte[] { 0x31, 0x32, 0x41, 0x42, 0x43, 0x61, 0x62, 0x63, 0x64 };
            var transport = new QueueTransport(
                CreateServerHelloAck(sessionId: 41, wireFormat: NnrpHeader.CurrentWireFormat).ToFramedMessage(),
                CreateResultPush(
                    sessionId: 41,
                    frameId: 303,
                    wireFormat: NnrpHeader.CurrentWireFormat,
                    payloadKindBitmap: PayloadKind.Tensor | PayloadKind.TokenChunk,
                    payloadFrameCount: 3,
                    typedPayloadDescriptors: descriptors,
                    typedPayloadFrameRegion: payloadRegion).ToFramedMessage());
            var client = new NnrpClient(new ClientProfile(), transport);
            await client.ConnectAsync(requestedSessionId: 41, cancellationToken: CancellationToken.None);

            var result = await client.SubmitAsync(CreateSubmitRequest(frameId: 303), CancellationToken.None);

            Assert.Equal(PayloadKind.Tensor | PayloadKind.TokenChunk, result.PayloadKindBitmap);
            Assert.Equal<ushort>(3, result.PayloadFrameCount);
            Assert.Equal(3, result.TypedPayloadFrames.Length);

            var tokenFrames = result.GetTokenChunkFrames(TypedPayloadDescriptor.ProfileToken);
            Assert.Equal(3, tokenFrames.FrameCount);
            Assert.Equal(9, tokenFrames.PayloadBytes);
            Assert.Equal(new byte[] { 0x31, 0x32 }, tokenFrames.Frames.Span[0].Payload.ToArray());
            Assert.Equal(new byte[] { 0x41, 0x42, 0x43 }, tokenFrames.Frames.Span[1].Payload.ToArray());
            Assert.Equal(new byte[] { 0x61, 0x62, 0x63, 0x64 }, tokenFrames.Frames.Span[2].Payload.ToArray());
        }

        [Fact]
        public async Task SubmitAsyncPreservesStaleReuseMetadata()
        {
            var transport = new QueueTransport(
                CreateServerHelloAck(sessionId: 41, wireFormat: NnrpHeader.CurrentWireFormat).ToFramedMessage(),
                CreateResultPush(
                    sessionId: 41,
                    frameId: 303,
                    wireFormat: NnrpHeader.CurrentWireFormat,
                    resultClass: ResultClass.StaleReuse,
                    resultFlags: ResultFlags.Stale,
                    appliedBudgetPolicy: BudgetPolicy.AllowStaleReuse,
                    reusedFrameId: 299,
                    coveredTileCount: 3,
                    droppedTileCount: 0).ToFramedMessage());
            var client = new NnrpClient(new ClientProfile(), transport);
            await client.ConnectAsync(requestedSessionId: 41, cancellationToken: CancellationToken.None);

            var result = await client.SubmitAsync(CreateSubmitRequest(frameId: 303), CancellationToken.None);

            Assert.Equal(ResultClass.StaleReuse, result.ResultClass);
            Assert.Equal(ResultFlags.Stale, result.ResultFlags);
            Assert.Equal(BudgetPolicy.AllowStaleReuse, result.AppliedBudgetPolicy);
            Assert.Equal<uint>(299U, result.ReusedFrameId);
            Assert.Equal<ushort>(3, result.CoveredTileCount);
            Assert.Equal<ushort>(0, result.DroppedTileCount);
        }

        [Fact]
        public async Task SubmitAsyncPreservesDegradedResultMetadata()
        {
            var transport = new QueueTransport(
                CreateServerHelloAck(sessionId: 41, wireFormat: NnrpHeader.CurrentWireFormat).ToFramedMessage(),
                CreateResultPush(
                    sessionId: 41,
                    frameId: 303,
                    wireFormat: NnrpHeader.CurrentWireFormat,
                    resultClass: ResultClass.Degraded,
                    appliedBudgetPolicy: BudgetPolicy.AllowDrop,
                    coveredTileCount: 3,
                    droppedTileCount: 0).ToFramedMessage());
            var client = new NnrpClient(new ClientProfile(), transport);
            await client.ConnectAsync(requestedSessionId: 41, cancellationToken: CancellationToken.None);

            var result = await client.SubmitAsync(CreateSubmitRequest(frameId: 303), CancellationToken.None);

            Assert.Equal(ResultClass.Degraded, result.ResultClass);
            Assert.Equal(ResultFlags.None, result.ResultFlags);
            Assert.Equal(BudgetPolicy.AllowDrop, result.AppliedBudgetPolicy);
            Assert.Equal<uint>(0U, result.ReusedFrameId);
            Assert.Equal<ushort>(3, result.CoveredTileCount);
            Assert.Equal<ushort>(0, result.DroppedTileCount);
        }

        [Fact]
        public async Task ConnectAsyncMapsErrorResponseToProtocolFailure()
        {
            var expected = NnrpProtocolFailure.UnsupportedCapability("No common codec.");
            var transport = new QueueTransport(ErrorMessage.FromProtocolFailure(expected).ToFramedMessage());
            var client = new NnrpClient(new ClientProfile(), transport);

            var connectResult = await client.ConnectAsync(cancellationToken: CancellationToken.None);

            Assert.False(connectResult.IsConnected);
            Assert.Equal(expected.ErrorCode, connectResult.Failure.ErrorCode);
            Assert.Equal(expected.Scope, connectResult.Failure.Scope);
            Assert.Equal(expected.Message, connectResult.Failure.Message);
        }

        [Fact]
        public async Task ConnectAsyncRejectsAckWithMismatchedActiveTransportEcho()
        {
            var profile = new ClientProfile();
            var hello = profile.CreateClientHello(
                requestedSessionId: 41,
                traceId: 22,
                TransportPolicy.ForceTcp,
                TransportId.Tcp);
            var transport = new QueueTransport(
                CreateServerHelloAck(
                    sessionId: 41,
                    wireFormat: NnrpHeader.CurrentWireFormat,
                    echoedTransportPolicy: TransportPolicy.ForceTcp,
                    activeTransportId: TransportId.Quic).ToFramedMessage());
            var client = new NnrpClient(profile, transport);

            var connectResult = await client.ConnectAsync(
                hello,
                expectedActiveTransportId: TransportId.Tcp,
                cancellationToken: CancellationToken.None);

            Assert.False(connectResult.IsConnected);
            Assert.Equal(ErrorCode.UnsupportedCapability, connectResult.Failure.ErrorCode);
            Assert.Contains("does not match expected", connectResult.Failure.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task ConnectAsyncRejectsAckVersionStageOutsideClientHelloBitmap()
        {
            var profile = new ClientProfile();
            var hello = profile.CreateClientHello(
                requestedSessionId: 41,
                traceId: 22,
                TransportPolicy.ForceTcp,
                TransportId.Tcp);
            var transport = new QueueTransport(CreateServerHelloAck(sessionId: 41).ToFramedMessage());
            var client = new NnrpClient(profile, transport);

            var connectResult = await client.ConnectAsync(
                hello,
                expectedActiveTransportId: TransportId.Tcp,
                cancellationToken: CancellationToken.None);

            Assert.False(connectResult.IsConnected);
            Assert.Equal(ErrorCode.MalformedBody, connectResult.Failure.ErrorCode);
            Assert.Contains("transport policy", connectResult.Failure.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task SubmitAsyncRejectsMismatchedNegotiatedSessionId()
        {
            var transport = new QueueTransport(CreateServerHelloAck(sessionId: 9, wireFormat: NnrpHeader.CurrentWireFormat).ToFramedMessage());
            var client = new NnrpClient(new ClientProfile(), transport);
            await client.ConnectAsync(requestedSessionId: 9, cancellationToken: CancellationToken.None);

            var mismatch = SmokePackets.CreateSmokeFrameSubmitMessage(sessionId: 10, frameId: 5);
            var error = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await client.SubmitAsync(mismatch, CancellationToken.None));

            Assert.Contains("does not match negotiated session_id", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task SubmitAsyncRejectsMismatchedResultCorrelation()
        {
            var mismatchedSessionTransport = new QueueTransport(
                CreateServerHelloAck(sessionId: 41, wireFormat: NnrpHeader.CurrentWireFormat).ToFramedMessage(),
                CreateResultPush(sessionId: 42, frameId: 303, wireFormat: NnrpHeader.CurrentWireFormat).ToFramedMessage());
            var mismatchedSessionClient = new NnrpClient(new ClientProfile(), mismatchedSessionTransport);
            await mismatchedSessionClient.ConnectAsync(requestedSessionId: 41, cancellationToken: CancellationToken.None);

            var sessionError = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await mismatchedSessionClient.SubmitAsync(CreateSubmitRequest(frameId: 303), CancellationToken.None));
            Assert.Contains("RESULT_PUSH session_id", sessionError.Message, StringComparison.Ordinal);

            var mismatchedFrameTransport = new QueueTransport(
                CreateServerHelloAck(sessionId: 41, wireFormat: NnrpHeader.CurrentWireFormat).ToFramedMessage(),
                CreateResultPush(sessionId: 41, frameId: 304, wireFormat: NnrpHeader.CurrentWireFormat).ToFramedMessage());
            var mismatchedFrameClient = new NnrpClient(new ClientProfile(), mismatchedFrameTransport);
            await mismatchedFrameClient.ConnectAsync(requestedSessionId: 41, cancellationToken: CancellationToken.None);

            var frameError = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await mismatchedFrameClient.SubmitAsync(CreateSubmitRequest(frameId: 303), CancellationToken.None));
            Assert.Contains("RESULT_PUSH correlation mismatch", frameError.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task CancelAsyncSendsFrameCancelForInFlightSubmit()
        {
            var transport = new BlockingSubmitTransport(CreateServerHelloAck(sessionId: 41, wireFormat: NnrpHeader.CurrentWireFormat).ToFramedMessage());
            var client = new NnrpClient(new ClientProfile(), transport);
            await client.ConnectAsync(requestedSessionId: 41, cancellationToken: CancellationToken.None);

            var submitTask = client.SubmitAsync(
                SmokePackets.CreateSmokeFrameSubmitMessage(sessionId: 41, frameId: 303),
                CancellationToken.None).AsTask();
            await transport.SubmitReceiveStarted.Task;

            var cancelFailure = await client.CancelAsync(frameId: 303, cancellationToken: CancellationToken.None);

            Assert.Equal(NnrpProtocolFailure.None, cancelFailure);
            Assert.Equal(3, transport.Sent.Count);
            Assert.Equal(MessageType.FrameCancel, transport.Sent[2].Header.MessageType);

            transport.CompleteSubmit(CreateResultPush(sessionId: 41, frameId: 303).ToFramedMessage());
            var result = await submitTask;
            Assert.Equal(303u, result.Header.FrameId);

            var duplicateCancel = await client.CancelAsync(frameId: 303, cancellationToken: CancellationToken.None);
            Assert.True(duplicateCancel.IsFailure);
            Assert.Equal(ErrorCode.InvalidState, duplicateCancel.ErrorCode);
        }

        [Fact]
        public async Task PingAsyncSendsPingAndMeasuresRoundTrip()
        {
            var transport = new QueueTransport(
                CreateServerHelloAck(sessionId: 41, wireFormat: NnrpHeader.CurrentWireFormat).ToFramedMessage(),
                PongMessage.Create(sessionId: 41, traceId: 77).ToFramedMessage());
            var client = new NnrpClient(new ClientProfile(), transport);
            await client.ConnectAsync(requestedSessionId: 41, cancellationToken: CancellationToken.None);

            var elapsed = await client.PingAsync(traceId: 77, cancellationToken: CancellationToken.None);

            Assert.Equal(2, transport.Sent.Count);
            Assert.Equal(MessageType.Ping, transport.Sent[1].Header.MessageType);
            Assert.Equal(77ul, transport.Sent[1].Header.TraceId);
            Assert.True(elapsed >= TimeSpan.Zero);
        }

        [Fact]
        public async Task PingAsyncRejectsMismatchedPongCorrelation()
        {
            var transport = new QueueTransport(
                CreateServerHelloAck(sessionId: 41, wireFormat: NnrpHeader.CurrentWireFormat).ToFramedMessage(),
                PongMessage.Create(sessionId: 41, traceId: 88).ToFramedMessage());
            var client = new NnrpClient(new ClientProfile(), transport);
            await client.ConnectAsync(requestedSessionId: 41, cancellationToken: CancellationToken.None);

            var error = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await client.PingAsync(traceId: 77, cancellationToken: CancellationToken.None));

            Assert.Contains("PONG correlation mismatch", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task SubmitAsyncRejectedBeforeConnect()
        {
            var transport = new QueueTransport();
            var client = new NnrpClient(new ClientProfile(), transport);

            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await client.SubmitAsync(
                    SmokePackets.CreateSmokeFrameSubmitMessage(sessionId: 1, frameId: 1),
                    CancellationToken.None));
        }

        [Fact]
        public async Task CloseAsyncBeforeConnectSucceedsAndStaysClosed()
        {
            var transport = new QueueTransport();
            var client = new NnrpClient(new ClientProfile(), transport);

            var failure = await client.CloseAsync("early", cancellationToken: CancellationToken.None);
            // Closing from INIT is valid; session transitions to closed.
            Assert.False(failure.IsFailure);
            Assert.Equal(NnrpSessionState.Closed, client.Session.State);
        }

        [Fact]
        public void ClientExposesProfileAndTransportProfile()
        {
            var transport = new QueueTransport();
            var client = new NnrpClient(new ClientProfile { MaxViews = 3 }, transport);
            Assert.Equal(3, client.Profile.MaxViews);
            Assert.Same(client.Session, client.Session);
        }

        [Fact]
        public async Task ConnectAsyncParsesErrorResponseFromServer()
        {
            var expected = NnrpProtocolFailure.LimitExceeded(NnrpErrorScope.Session, "too many", isFatal: true);
            var transport = new QueueTransport(ErrorMessage.FromProtocolFailure(expected).ToFramedMessage());
            var client = new NnrpClient(new ClientProfile(), transport);

            var result = await client.ConnectAsync(cancellationToken: CancellationToken.None);
            Assert.False(result.IsConnected);
            Assert.Equal(ErrorCode.LimitExceeded, result.Failure.ErrorCode);
        }

        [Fact]
        public async Task SubmitAsyncReceivesErrorResponse()
        {
            var errorFailure = NnrpProtocolFailure.UnsupportedCapability("no codec");
            var transport = new QueueTransport(
                CreateServerHelloAck(sessionId: 41, wireFormat: NnrpHeader.CurrentWireFormat).ToFramedMessage(),
                ErrorMessage.FromProtocolFailure(errorFailure).ToFramedMessage());
            var client = new NnrpClient(new ClientProfile(), transport);
            await client.ConnectAsync(requestedSessionId: 41, cancellationToken: CancellationToken.None);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await client.SubmitAsync(
                    SmokePackets.CreateSmokeFrameSubmitMessage(sessionId: 41, frameId: 303),
                    CancellationToken.None));
            Assert.Contains("ERROR", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task SubmitAsyncReceivesNonResultPushResponse()
        {
            var transport = new QueueTransport(
                CreateServerHelloAck(sessionId: 41, wireFormat: NnrpHeader.CurrentWireFormat).ToFramedMessage(),
                CloseMessage.Create(sessionId: 41, "test", traceId: 0).ToFramedMessage());
            var client = new NnrpClient(new ClientProfile(), transport);
            await client.ConnectAsync(requestedSessionId: 41, cancellationToken: CancellationToken.None);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await client.SubmitAsync(
                    SmokePackets.CreateSmokeFrameSubmitMessage(sessionId: 41, frameId: 303),
                    CancellationToken.None));
            Assert.Contains("Expected RESULT_PUSH", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task CancelAsyncRejectedWhenSessionNotActive()
        {
            var transport = new QueueTransport();
            var client = new NnrpClient(new ClientProfile(), transport);

            var failure = await client.CancelAsync(frameId: 1, cancellationToken: CancellationToken.None);
            Assert.True(failure.IsFailure);
            Assert.Equal(ErrorCode.InvalidState, failure.ErrorCode);
        }

        [Fact]
        public async Task PingAsyncRejectedWhenSessionNotActive()
        {
            var transport = new QueueTransport();
            var client = new NnrpClient(new ClientProfile(), transport);

            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await client.PingAsync(cancellationToken: CancellationToken.None));
        }

        [Fact]
        public async Task PingAsyncReceivesErrorResponse()
        {
            var errorFailure = NnrpProtocolFailure.UnsupportedCapability("no ping");
            var transport = new QueueTransport(
                CreateServerHelloAck(sessionId: 41, wireFormat: NnrpHeader.CurrentWireFormat).ToFramedMessage(),
                ErrorMessage.FromProtocolFailure(errorFailure).ToFramedMessage());
            var client = new NnrpClient(new ClientProfile(), transport);
            await client.ConnectAsync(requestedSessionId: 41, cancellationToken: CancellationToken.None);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await client.PingAsync(traceId: 77, cancellationToken: CancellationToken.None));
            Assert.Contains("ERROR", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task PingAsyncReceivesNonPongResponse()
        {
            var transport = new QueueTransport(
                CreateServerHelloAck(sessionId: 41, wireFormat: NnrpHeader.CurrentWireFormat).ToFramedMessage(),
                CloseMessage.Create(sessionId: 41, "no pong", traceId: 77).ToFramedMessage());
            var client = new NnrpClient(new ClientProfile(), transport);
            await client.ConnectAsync(requestedSessionId: 41, cancellationToken: CancellationToken.None);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await client.PingAsync(traceId: 77, cancellationToken: CancellationToken.None));
            Assert.Contains("Expected PONG", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task CloseAsyncFromActiveSessionSendsCloseMessage()
        {
            var transport = new QueueTransport(
                CreateServerHelloAck(sessionId: 41, wireFormat: NnrpHeader.CurrentWireFormat).ToFramedMessage());
            var client = new NnrpClient(new ClientProfile(), transport);
            await client.ConnectAsync(requestedSessionId: 41, cancellationToken: CancellationToken.None);

            var failure = await client.CloseAsync("done", traceId: 99, cancellationToken: CancellationToken.None);
            Assert.False(failure.IsFailure);
            Assert.Equal(2, transport.Sent.Count);
            Assert.Equal(MessageType.Close, transport.Sent[1].Header.MessageType);
        }

        [Fact]
        public async Task MigrateAsyncRaisesResumeFromFrameIdFloorForFutureSubmits()
        {
            var transport = new QueueTransport(
                CreateServerHelloAck(sessionId: 41, wireFormat: NnrpHeader.CurrentWireFormat).ToFramedMessage(),
                CreateSessionMigrateAck(sessionId: 41, traceId: 55, resumeFromFrameId: 304).ToFramedMessage());
            var client = new NnrpClient(new ClientProfile(), transport);
            await client.ConnectAsync(requestedSessionId: 41, cancellationToken: CancellationToken.None);

            var ack = await client.MigrateAsync(
                oldTransportId: TransportId.Tcp,
                newTransportId: TransportId.Quic,
                lastResultFrameId: 303,
                clientMigrateTimestampMicroseconds: 123456789,
                traceId: 55,
                cancellationToken: CancellationToken.None);

            Assert.Equal(304ul, ack.Metadata.ResumeFromFrameId);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await client.SendSubmitAsync(CreateSubmitRequest(frameId: 303), CancellationToken.None));
            Assert.Contains("resume_from_frame_id 304", ex.Message, StringComparison.Ordinal);

            var submitted = await client.SendSubmitAsync(CreateSubmitRequest(frameId: 304), CancellationToken.None);
            Assert.Equal(304u, submitted.FrameId);
            Assert.True(client.IsFrameInFlight(304));
        }

        [Fact]
        public async Task MigrateAsyncPrunesInFlightFramesBelowResumeFromFrameId()
        {
            var transport = new QueueTransport(
                CreateServerHelloAck(sessionId: 41, wireFormat: NnrpHeader.CurrentWireFormat).ToFramedMessage(),
                CreateSessionMigrateAck(sessionId: 41, traceId: 55, resumeFromFrameId: 304).ToFramedMessage(),
                CreateResultPush(sessionId: 41, frameId: 304, wireFormat: NnrpHeader.CurrentWireFormat).ToFramedMessage());
            var client = new NnrpClient(new ClientProfile(), transport);
            await client.ConnectAsync(requestedSessionId: 41, cancellationToken: CancellationToken.None);

            await client.SendSubmitAsync(CreateSubmitRequest(frameId: 303), CancellationToken.None);
            await client.SendSubmitAsync(CreateSubmitRequest(frameId: 304), CancellationToken.None);

            await client.MigrateAsync(
                oldTransportId: TransportId.Tcp,
                newTransportId: TransportId.Quic,
                lastResultFrameId: 303,
                clientMigrateTimestampMicroseconds: 123456789,
                traceId: 55,
                cancellationToken: CancellationToken.None);

            Assert.False(client.IsFrameInFlight(303));
            Assert.True(client.IsFrameInFlight(304));

            var staleFrame = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await client.ReceiveResultAsync(303, cancellationToken: CancellationToken.None));
            Assert.Contains("not in flight", staleFrame.Message, StringComparison.Ordinal);

            var result = await client.ReceiveResultAsync(304, cancellationToken: CancellationToken.None);
            Assert.Equal(304u, result.Header.FrameId);
            Assert.False(client.IsFrameInFlight(304));
        }

        [Fact]
        public async Task TryAutoMigrateAsyncSendsSessionMigrateWhenTriggerFires()
        {
            var transport = new QueueTransport(
                CreateServerHelloAck(sessionId: 41, wireFormat: NnrpHeader.CurrentWireFormat).ToFramedMessage(),
                CreateSessionMigrateAck(sessionId: 41, traceId: 55, resumeFromFrameId: 304).ToFramedMessage());
            var client = new NnrpClient(new ClientProfile(), transport);
            await client.ConnectAsync(requestedSessionId: 41, cancellationToken: CancellationToken.None);

            var selection = new NnrpTransportProbeSelectionResult(
                TransportId.Quic,
                "quic",
                new[]
                {
                    new NnrpTransportProbeBindingSummary(TransportId.Tcp, "tcp", successCount: 1, failureCount: 2, warmupSampleCount: 0, scoredSampleCount: 3, medianThroughputBytesPerSecond: 400_000d, medianRttMicroseconds: 2500, medianJitterMicroseconds: 1200),
                    new NnrpTransportProbeBindingSummary(TransportId.Quic, "quic", successCount: 3, failureCount: 0, warmupSampleCount: 0, scoredSampleCount: 3, medianThroughputBytesPerSecond: 450_000d, medianRttMicroseconds: 1800, medianJitterMicroseconds: 600),
                });

            var migration = await client.TryAutoMigrateAsync(
                currentTransportId: TransportId.Tcp,
                probeSelection: selection,
                lastResultFrameId: 303,
                clientMigrateTimestampMicroseconds: 123456789,
                triggerOptions: new NnrpTransportMigrationTriggerOptions(),
                traceId: 55,
                cancellationToken: CancellationToken.None);

            Assert.True(migration.WasMigrated);
            Assert.Equal(NnrpTransportMigrationTriggerMetric.FailureRegression, migration.Decision.TriggerMetric);
            Assert.Equal(304ul, migration.AckMessage.Metadata.ResumeFromFrameId);
            Assert.Equal(2, transport.Sent.Count);
            Assert.Equal(MessageType.SessionMigrate, transport.Sent[1].Header.MessageType);
        }

        [Fact]
        public async Task SubmitAsyncRejectsDuplicateInFlightFrame()
        {
            var transport = new BlockingSubmitTransport(
                CreateServerHelloAck(sessionId: 41, wireFormat: NnrpHeader.CurrentWireFormat).ToFramedMessage());
            var client = new NnrpClient(new ClientProfile(), transport);
            await client.ConnectAsync(requestedSessionId: 41, cancellationToken: CancellationToken.None);

            var submitTask = client.SubmitAsync(
                SmokePackets.CreateSmokeFrameSubmitMessage(sessionId: 41, frameId: 303),
                CancellationToken.None).AsTask();
            await transport.SubmitReceiveStarted.Task;

            // Second submit for same frame should be rejected
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await client.SubmitAsync(
                    SmokePackets.CreateSmokeFrameSubmitMessage(sessionId: 41, frameId: 303),
                    CancellationToken.None));
            Assert.Contains("already in flight", ex.Message, StringComparison.Ordinal);

            transport.CompleteSubmit(CreateResultPush(sessionId: 41, frameId: 303).ToFramedMessage());
            await submitTask;
        }

        [Fact]
        public async Task ClientExposesInFlightFrameSnapshotsAndCorrelationHelpers()
        {
            var transport = new BlockingSubmitTransport(
                CreateServerHelloAck(sessionId: 41, wireFormat: NnrpHeader.CurrentWireFormat).ToFramedMessage());
            var client = new NnrpClient(new ClientProfile(), transport);
            await client.ConnectAsync(requestedSessionId: 41, cancellationToken: CancellationToken.None);

            var submitTask = client.SubmitAsync(
                CreateSubmitRequest(frameId: 303),
                CancellationToken.None).AsTask();
            await transport.SubmitReceiveStarted.Task;

            Assert.Equal(1, client.InFlightFrameCount);
            Assert.True(client.IsFrameInFlight(303));

            var inFlightFrames = client.GetInFlightFrames();
            Assert.Single(inFlightFrames);
            Assert.Equal(new NnrpInFlightFrame(303, 0), inFlightFrames[0]);

            var matchingHeader = CreateResultPush(sessionId: 41, frameId: 303, wireFormat: NnrpHeader.CurrentWireFormat).Header;
            Assert.True(client.TryValidateResultCorrelation(matchingHeader, out var correlatedFrame, out var failure));
            Assert.Equal(string.Empty, failure);
            Assert.Equal(new NnrpInFlightFrame(303, 0), correlatedFrame);

            var wrongFrameHeader = CreateResultPush(sessionId: 41, frameId: 304, wireFormat: NnrpHeader.CurrentWireFormat).Header;
            Assert.False(client.TryValidateResultCorrelation(wrongFrameHeader, out _, out failure));
            Assert.Contains("not currently in flight", failure, StringComparison.Ordinal);

            transport.CompleteSubmit(CreateResultPush(sessionId: 41, frameId: 303, wireFormat: NnrpHeader.CurrentWireFormat).ToFramedMessage());
            await submitTask;

            Assert.Equal(0, client.InFlightFrameCount);
            Assert.False(client.IsFrameInFlight(303));
            Assert.Empty(client.GetInFlightFrames());
        }

        [Fact]
        public async Task ClientSupportsExplicitSendSubmitAndReceiveResultFlow()
        {
            var transport = new QueueTransport(
                CreateServerHelloAck(sessionId: 41, wireFormat: NnrpHeader.CurrentWireFormat).ToFramedMessage(),
                CreateResultPush(sessionId: 41, frameId: 303, wireFormat: NnrpHeader.CurrentWireFormat).ToFramedMessage());
            var client = new NnrpClient(new ClientProfile(), transport);
            await client.ConnectAsync(requestedSessionId: 41, cancellationToken: CancellationToken.None);

            var submitted = await client.SendSubmitAsync(
                CreateSubmitRequest(frameId: 303),
                CancellationToken.None);
            Assert.Equal(41u, submitted.SessionId);
            Assert.Equal(303u, submitted.FrameId);
            Assert.Equal(NnrpHeader.CurrentWireFormat, submitted.WireFormat);
            Assert.True(client.IsFrameInFlight(303));

            var result = await client.ReceiveResultAsync(303, cancellationToken: CancellationToken.None);

            Assert.Equal(303u, result.Header.FrameId);
            Assert.False(client.IsFrameInFlight(303));
            Assert.Equal(0, client.InFlightFrameCount);
        }

        [Fact]
        public async Task ReceiveResultAsyncBuffersOutOfOrderInFlightResults()
        {
            var transport = new QueueTransport(
                CreateServerHelloAck(sessionId: 41, wireFormat: NnrpHeader.CurrentWireFormat).ToFramedMessage(),
                CreateResultPush(sessionId: 41, frameId: 304, wireFormat: NnrpHeader.CurrentWireFormat).ToFramedMessage(),
                CreateResultPush(sessionId: 41, frameId: 303, wireFormat: NnrpHeader.CurrentWireFormat).ToFramedMessage());
            var client = new NnrpClient(new ClientProfile(), transport);
            await client.ConnectAsync(requestedSessionId: 41, cancellationToken: CancellationToken.None);

            await client.SendSubmitAsync(CreateSubmitRequest(frameId: 303), CancellationToken.None);
            await client.SendSubmitAsync(CreateSubmitRequest(frameId: 304), CancellationToken.None);

            var firstResult = await client.ReceiveResultAsync(303, cancellationToken: CancellationToken.None);

            Assert.Equal(303u, firstResult.Header.FrameId);
            Assert.False(client.IsFrameInFlight(303));
            Assert.True(client.IsFrameInFlight(304));
            Assert.Equal(1, client.InFlightFrameCount);

            var secondResult = await client.ReceiveResultAsync(304, cancellationToken: CancellationToken.None);

            Assert.Equal(304u, secondResult.Header.FrameId);
            Assert.False(client.IsFrameInFlight(304));
            Assert.Equal(0, client.InFlightFrameCount);
        }

        [Fact]
        public async Task ReceiveResultAsyncBuffersFlowUpdatesWhileWaitingForResult()
        {
            var transport = new QueueTransport(
                CreateServerHelloAck(sessionId: 41, wireFormat: NnrpHeader.CurrentWireFormat).ToFramedMessage(),
                CreateFlowUpdate(sessionId: 41).ToFramedMessage(),
                CreateResultPush(sessionId: 41, frameId: 303, wireFormat: NnrpHeader.CurrentWireFormat).ToFramedMessage());
            var client = new NnrpClient(new ClientProfile(), transport);
            await client.ConnectAsync(requestedSessionId: 41, cancellationToken: CancellationToken.None);

            await client.SendSubmitAsync(CreateSubmitRequest(frameId: 303), CancellationToken.None);

            var result = await client.ReceiveResultAsync(303, cancellationToken: CancellationToken.None);

            Assert.Equal(303u, result.Header.FrameId);
            Assert.Equal(1, client.BufferedFlowUpdateCount);
            Assert.True(client.TryDequeueFlowUpdate(out var flowUpdate));
            Assert.Equal(41u, flowUpdate.Header.SessionId);
            Assert.Equal(FlowUpdateScopeKind.Session, flowUpdate.Metadata.ScopeKind);
            Assert.Equal(FlowUpdateReason.Congestion, flowUpdate.Metadata.UpdateReason);
            Assert.Equal(4u, flowUpdate.Metadata.RetryAfterMilliseconds);
            Assert.False(client.TryDequeueFlowUpdate(out _));
            Assert.Equal(0, client.BufferedFlowUpdateCount);
        }

        [Fact]
        public async Task ReceiveNextEventAsyncYieldsFlowUpdateThenResultPush()
        {
            var transport = new QueueTransport(
                CreateServerHelloAck(sessionId: 41, wireFormat: NnrpHeader.CurrentWireFormat).ToFramedMessage(),
                CreateFlowUpdate(sessionId: 41).ToFramedMessage(),
                CreateResultPush(sessionId: 41, frameId: 303, wireFormat: NnrpHeader.CurrentWireFormat).ToFramedMessage());
            var client = new NnrpClient(new ClientProfile(), transport);
            await client.ConnectAsync(requestedSessionId: 41, cancellationToken: CancellationToken.None);

            await client.SendSubmitAsync(CreateSubmitRequest(frameId: 303), CancellationToken.None);

            var flowEvent = await client.ReceiveNextEventAsync(CancellationToken.None);
            var resultEvent = await client.ReceiveNextEventAsync(CancellationToken.None);

            Assert.True(flowEvent.IsFlowUpdate);
            Assert.Equal(FlowUpdateReason.Congestion, flowEvent.GetFlowUpdate().Metadata.UpdateReason);
            Assert.True(resultEvent.IsResultPush);
            Assert.Equal(303u, resultEvent.GetResultPush().Header.FrameId);
            Assert.False(client.IsFrameInFlight(303));
            Assert.Equal(0, client.InFlightFrameCount);
        }

        [Fact]
        public async Task ReceiveNextEventAsyncYieldsOutOfOrderResultsInWireOrder()
        {
            var transport = new QueueTransport(
                CreateServerHelloAck(sessionId: 41, wireFormat: NnrpHeader.CurrentWireFormat).ToFramedMessage(),
                CreateResultPush(sessionId: 41, frameId: 304, wireFormat: NnrpHeader.CurrentWireFormat).ToFramedMessage(),
                CreateResultPush(sessionId: 41, frameId: 303, wireFormat: NnrpHeader.CurrentWireFormat).ToFramedMessage());
            var client = new NnrpClient(new ClientProfile(), transport);
            await client.ConnectAsync(requestedSessionId: 41, cancellationToken: CancellationToken.None);

            await client.SendSubmitAsync(CreateSubmitRequest(frameId: 303), CancellationToken.None);
            await client.SendSubmitAsync(CreateSubmitRequest(frameId: 304), CancellationToken.None);

            var firstEvent = await client.ReceiveNextEventAsync(CancellationToken.None);
            var secondEvent = await client.ReceiveNextEventAsync(CancellationToken.None);

            Assert.Equal(304u, firstEvent.GetResultPush().Header.FrameId);
            Assert.Equal(303u, secondEvent.GetResultPush().Header.FrameId);
            Assert.Equal(0, client.InFlightFrameCount);
        }

        [Fact]
        public async Task SubmitAndWaitAsyncRemainsConvenienceWrapperOverExplicitFlow()
        {
            var transport = new QueueTransport(
                CreateServerHelloAck(sessionId: 41).ToFramedMessage(),
                CreateResultPush(sessionId: 41, frameId: 303).ToFramedMessage());
            var client = new NnrpClient(new ClientProfile(), transport);
            await client.ConnectAsync(requestedSessionId: 41, cancellationToken: CancellationToken.None);

            var result = await client.SubmitAndWaitAsync(
                CreateSubmitRequest(frameId: 303),
                CancellationToken.None);

            Assert.Equal(303u, result.FrameId);
            Assert.Equal(NnrpHeader.CurrentWireFormat, transport.Sent[1].Header.ToArray()[5]);
            Assert.Equal(0, client.InFlightFrameCount);
        }

        [Fact]
        public void NnrpTransportProfileHasExpectedValues()
        {
            Assert.Equal(0, (int)NnrpTransportProfile.ControlEvidence);
            Assert.Equal(1, (int)NnrpTransportProfile.Quic);
        }

        private static ServerHelloAckMessage CreateServerHelloAck(
            uint sessionId,
            byte wireFormat = NnrpHeader.CurrentWireFormat,
            TransportPolicy echoedTransportPolicy = TransportPolicy.Auto,
            TransportId activeTransportId = TransportId.Unspecified)
        {
            var extensions = activeTransportId != TransportId.Unspecified
                ? new[]
                {
                    new ServerTransportPolicyAckExtension(echoedTransportPolicy, echoedTransportPolicy, activeTransportId).ToControlExtension(),
                }
                : Array.Empty<ControlExtensionBlock>();
            var metadata = new ServerHelloAckMetadata(
                selectedVersionMajor: NnrpHeader.CurrentVersionMajor,
                selectedWireFormat: wireFormat,
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
                controlExtensionBytes: (uint)GetExtensionBodyLength(extensions),
                serverFlags: 0);
            var header = new NnrpHeader(
                versionMajor: NnrpHeader.CurrentVersionMajor,
                messageType: MessageType.ServerHelloAck,
                flags: HeaderFlags.None,
                metaLength: ServerHelloAckMetadata.MetadataLength,
                bodyLength: (uint)GetExtensionBodyLength(extensions),
                sessionId: sessionId,
                frameId: 0,
                viewId: 0,
                routeId: 0,
                traceId: 0,
                wireFormat: wireFormat);
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
            var bodyLength = (uint)(BinaryAlignment.AlignUp(6, 8) + section.TotalLength);
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

        private sealed class BlockingSubmitTransport : INnrpMessageTransport
        {
            private readonly Queue<NnrpFramedMessage> inbound;
            private readonly TaskCompletionSource<NnrpFramedMessage> submitResult =
                new TaskCompletionSource<NnrpFramedMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            private int receiveCount;

            public BlockingSubmitTransport(params NnrpFramedMessage[] inbound)
            {
                this.inbound = new Queue<NnrpFramedMessage>(inbound ?? Array.Empty<NnrpFramedMessage>());
            }

            public List<NnrpFramedMessage> Sent { get; } = new List<NnrpFramedMessage>();

            public TaskCompletionSource<bool> SubmitReceiveStarted { get; } =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public ValueTask SendAsync(NnrpFramedMessage message, CancellationToken cancellationToken)
            {
                Sent.Add(message);
                return default;
            }

            public ValueTask<NnrpFramedMessage> ReceiveAsync(CancellationToken cancellationToken)
            {
                receiveCount++;
                if (receiveCount == 1)
                {
                    return new ValueTask<NnrpFramedMessage>(inbound.Dequeue());
                }

                SubmitReceiveStarted.TrySetResult(true);
                return new ValueTask<NnrpFramedMessage>(submitResult.Task);
            }

            public void CompleteSubmit(NnrpFramedMessage message)
            {
                submitResult.TrySetResult(message);
            }
        }
    }
}
