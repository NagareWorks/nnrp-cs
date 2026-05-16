using System;
using Nnrp.Client;
using Nnrp.Core;
using Nnrp.NativeBridge;
using Xunit;

namespace Nnrp.NativeBridge.Tests
{
    public sealed class NnrpQuicClientTests
    {
        [Fact]
        public void OptionsRejectInvalidValues()
        {
            Assert.Throws<ArgumentException>(() => new NnrpQuicClientOptions("", 50072, "localhost", "engine-sr"));
            Assert.Throws<ArgumentException>(() => new NnrpQuicClientOptions("127.0.0.1", 50072, "", "engine-sr"));
            Assert.Throws<ArgumentException>(() => new NnrpQuicClientOptions("127.0.0.1", 50072, "localhost", ""));
            _ = new NnrpQuicClientOptions("127.0.0.1", 50072, "localhost", "engine-sr", 11);
        }

        [Fact]
        public void OptionsDefaultToCurrentVersionStage()
        {
            var options = new NnrpQuicClientOptions("127.0.0.1", 50072, "localhost", "engine-sr");

            Assert.Equal(NnrpHeader.CurrentWireFormat, options.RequestedWireFormat);
        }

        [Fact]
        public void InternalConstructorRejectsNullDelegates()
        {
            var options = new NnrpQuicClientOptions("127.0.0.1", 50072, "localhost", "engine-sr", 41);

            Assert.Throws<ArgumentNullException>(() => new NnrpQuicClient(options, (Func<string, ushort, string, string, uint, NnrpNativeQuicClient.OpenResult>)null!, SubmitFrame, SubmitOutcomeBytes, PingRoundTrip, CancelFrame, CloseConnection));
            Assert.Throws<ArgumentNullException>(() => new NnrpQuicClient(options, OpenConnection, null!, SubmitOutcomeBytes, PingRoundTrip, CancelFrame, CloseConnection));
            Assert.Throws<ArgumentNullException>(() => new NnrpQuicClient(options, OpenConnection, SubmitFrame, null!, PingRoundTrip, CancelFrame, CloseConnection));
            Assert.Throws<ArgumentNullException>(() => new NnrpQuicClient(options, OpenConnection, SubmitFrame, SubmitOutcomeBytes, null!, CancelFrame, CloseConnection));
            Assert.Throws<ArgumentNullException>(() => new NnrpQuicClient(options, OpenConnection, SubmitFrame, SubmitOutcomeBytes, PingRoundTrip, null!, CloseConnection));
            Assert.Throws<ArgumentNullException>(() => new NnrpQuicClient(options, OpenConnection, SubmitFrame, SubmitOutcomeBytes, PingRoundTrip, CancelFrame, null!));
            Assert.Throws<ArgumentNullException>(() => new NnrpQuicClient(options, OpenConnection, SubmitFrame, SubmitOutcomeBytes, null!, ReceiveResultPacket, PingRoundTrip, CancelFrame, CloseConnection));
            Assert.Throws<ArgumentNullException>(() => new NnrpQuicClient(options, OpenConnection, SubmitFrame, SubmitOutcomeBytes, BeginSubmitPacket, null!, PingRoundTrip, CancelFrame, CloseConnection));
        }

        [Fact]
        public void ProfileBoundConstructorRequiresNonNullQuicProfile()
        {
            var options = new NnrpQuicClientOptions("127.0.0.1", 50072, "localhost", "engine-sr", 41);

            Assert.Throws<ArgumentNullException>(() => new NnrpQuicClient(null!, options, OpenConnection, SubmitFrame, SubmitOutcomeBytes, PingRoundTrip, CancelFrame, CloseConnection));

            var error = Assert.Throws<InvalidOperationException>(() =>
                new NnrpQuicClient(
                    new ClientProfile { TransportProfile = NnrpTransportProfile.ControlEvidence },
                    options));
            Assert.Contains("TransportProfile must be Quic", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ProfileBoundConstructorAcceptsQuicTransportProfile()
        {
            var profile = new ClientProfile { TransportProfile = NnrpTransportProfile.Quic };
            var client = new NnrpQuicClient(
                profile,
                new NnrpQuicClientOptions("127.0.0.1", 50072, "localhost", "engine-sr", 41));

            Assert.Same(profile, client.Profile);
            Assert.Equal(NnrpTransportProfile.Quic, client.Profile?.TransportProfile);
        }

        [Fact]
        public void ConnectTracksNegotiatedSessionAndActiveModel()
        {
            string? observedHost = null;
            ushort observedPort = 0;
            string? observedTlsServerName = null;
            string? observedRequestedModel = null;
            uint observedRequestedSessionId = 0;
            var options = new NnrpQuicClientOptions("127.0.0.1", 50072, "localhost", "engine-sr", 41);
            var client = CreateClient(
                options,
                openConnection: (host, port, tlsServerName, requestedModel, requestedSessionId) =>
                {
                    observedHost = host;
                    observedPort = port;
                    observedTlsServerName = tlsServerName;
                    observedRequestedModel = requestedModel;
                    observedRequestedSessionId = requestedSessionId;
                    return new NnrpNativeQuicClient.OpenResult(9, 77, "imdn-x2-tile32");
                });

            var openResult = client.Connect();

            Assert.True(client.IsConnected);
            Assert.Equal((ulong)9, client.Handle);
            Assert.Equal(77u, client.NegotiatedSessionId);
            Assert.Equal(NnrpHeader.CurrentWireFormat, client.NegotiatedWireFormat);
            Assert.Equal("imdn-x2-tile32", client.ActiveModelName);
            Assert.Equal((ulong)9, openResult.Handle);
            Assert.Equal(NnrpHeader.CurrentWireFormat, openResult.NegotiatedWireFormat);
            Assert.Equal("127.0.0.1", observedHost);
            Assert.Equal((ushort)50072, observedPort);
            Assert.Equal("localhost", observedTlsServerName);
            Assert.Equal("engine-sr", observedRequestedModel);
            Assert.Equal(41u, observedRequestedSessionId);
            Assert.Equal(NnrpHeader.CurrentWireFormat, options.RequestedWireFormat);
        }

        [Fact]
        public void ConnectRejectsDisposedAlreadyConnectedAndZeroHandle()
        {
            var client = CreateConnectedClient();
            Assert.Throws<InvalidOperationException>(() => client.Connect());

            client.Dispose();
            var disposedError = Assert.Throws<ObjectDisposedException>(() => client.Connect());
            Assert.Equal(nameof(NnrpQuicClient), disposedError.ObjectName);

            var zeroHandleClient = CreateClient(
                new NnrpQuicClientOptions("127.0.0.1", 50072, "localhost", "engine-sr", 41),
                openConnection: (_, _, _, _, _) => new NnrpNativeQuicClient.OpenResult(0, 0, string.Empty));
            var zeroHandleError = Assert.Throws<InvalidOperationException>(() => zeroHandleClient.Connect());
            Assert.Contains("handle 0", zeroHandleError.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void OperationsRequireConnection()
        {
            var client = CreateClient(new NnrpQuicClientOptions("127.0.0.1", 50072, "localhost", "engine-sr", 41));
            var submit = SmokePackets.CreateSmokeFrameSubmitMessage(sessionId: 77, frameId: 303);

            Assert.Throws<InvalidOperationException>(() => client.Submit(submit));
            Assert.Throws<InvalidOperationException>(() => client.SubmitWithOutcome(submit));
            Assert.Throws<InvalidOperationException>(() => client.SendSubmitPacket(submit.ToArray()));
            Assert.Throws<InvalidOperationException>(() => client.ReceiveResultPacket());
            Assert.Throws<InvalidOperationException>(() => client.Ping());
            Assert.Throws<InvalidOperationException>(() => client.Cancel(frameId: 303));
        }

        [Fact]
        public void SendSubmitPacketAndReceiveResultPacketUseBackgroundDelegates()
        {
            ulong observedBeginHandle = 0;
            byte[]? observedBeginPacket = null;
            ulong observedReceiveHandle = 0;
            var client = CreateConnectedClient(
                beginSubmitPacket: (handle, packet) =>
                {
                    observedBeginHandle = handle;
                    observedBeginPacket = packet;
                },
                receiveResultPacket: handle =>
                {
                    observedReceiveHandle = handle;
                    return CreateResultPushMessage(sessionId: 77, frameId: 303, wireFormat: NnrpHeader.CurrentWireFormat).ToArray();
                });
            var submit = SmokePackets.CreateSmokeFrameSubmitMessage(sessionId: 77, frameId: 303);

            client.SendSubmitPacket(submit.ToArray());
            var resultPacket = client.ReceiveResultPacket();

            Assert.Equal((ulong)9, observedBeginHandle);
            Assert.Equal((ulong)9, observedReceiveHandle);
            Assert.NotNull(observedBeginPacket);
            Assert.True(ResultPushMessage.TryParse(resultPacket, out var parsed, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(303u, parsed.Header.FrameId);
        }

        [Fact]
        public void SubmitReturnsPushAndRejectsSessionMismatches()
        {
            ulong observedHandle = 0;
            FrameSubmitMessage observedSubmit = default;
            var client = CreateConnectedClient(
                submitFrame: (handle, submitMessage) =>
                {
                    observedHandle = handle;
                    observedSubmit = submitMessage;
                    return CreateResultPushMessage(sessionId: submitMessage.Header.SessionId, frameId: submitMessage.Header.FrameId);
                });
            var submit = SmokePackets.CreateSmokeFrameSubmitMessage(sessionId: 77, frameId: 303);

            var result = client.Submit(submit);

            Assert.Equal((ulong)9, observedHandle);
            Assert.Equal(303u, observedSubmit.Header.FrameId);
            Assert.Equal(303u, result.Header.FrameId);

            var wrongSubmit = SmokePackets.CreateSmokeFrameSubmitMessage(sessionId: 12, frameId: 303);
            Assert.Contains("negotiated session_id 77", Assert.Throws<InvalidOperationException>(() => client.Submit(wrongSubmit)).Message, StringComparison.Ordinal);

            var mismatchedResultClient = CreateConnectedClient(
                submitFrame: (_, submitMessage) => CreateResultPushMessage(sessionId: submitMessage.Header.SessionId + 1, frameId: submitMessage.Header.FrameId));
            Assert.Contains("RESULT_PUSH session_id", Assert.Throws<InvalidOperationException>(() => mismatchedResultClient.Submit(submit)).Message, StringComparison.Ordinal);

            var mismatchedFrameClient = CreateConnectedClient(
                submitFrame: (_, submitMessage) => CreateResultPushMessage(sessionId: submitMessage.Header.SessionId, frameId: submitMessage.Header.FrameId + 1));
            Assert.Contains("RESULT_PUSH correlation mismatch", Assert.Throws<InvalidOperationException>(() => mismatchedFrameClient.Submit(submit)).Message, StringComparison.Ordinal);
        }

        [Fact]
        public void SubmitWithOutcomeReturnsPushAndDropAndRejectsMalformedPayload()
        {
            var submit = SmokePackets.CreateSmokeFrameSubmitMessage(sessionId: 77, frameId: 303);

            var pushClient = CreateConnectedClient(
                submitOutcomeBytes: (_, __) => CreateResultPushMessage(sessionId: 77, frameId: 303).ToArray());
            var pushOutcome = pushClient.SubmitWithOutcome(submit);
            Assert.False(pushOutcome.IsResultDrop);
            Assert.Equal(303u, pushOutcome.ResultPush.Header.FrameId);

            var dropClient = CreateConnectedClient(
                submitOutcomeBytes: (_, __) => ResultDropMessage.Create(sessionId: 77, frameId: 303).ToArray());
            var dropOutcome = dropClient.SubmitWithOutcome(submit);
            Assert.True(dropOutcome.IsResultDrop);
            Assert.Equal(303u, dropOutcome.ResultDrop.Header.FrameId);

            var mismatchedPushClient = CreateConnectedClient(
                submitOutcomeBytes: (_, __) => CreateResultPushMessage(sessionId: 78, frameId: 303).ToArray());
            Assert.Contains("RESULT_PUSH session_id", Assert.Throws<InvalidOperationException>(() =>
                mismatchedPushClient.SubmitWithOutcome(submit)).Message, StringComparison.Ordinal);

            var mismatchedDropClient = CreateConnectedClient(
                submitOutcomeBytes: (_, __) => ResultDropMessage.Create(sessionId: 78, frameId: 303).ToArray());
            Assert.Contains("RESULT_DROP session_id", Assert.Throws<InvalidOperationException>(() =>
                mismatchedDropClient.SubmitWithOutcome(submit)).Message, StringComparison.Ordinal);

            var mismatchedPushFrameClient = CreateConnectedClient(
                submitOutcomeBytes: (_, __) => CreateResultPushMessage(sessionId: 77, frameId: 304).ToArray());
            Assert.Contains("RESULT_PUSH correlation mismatch", Assert.Throws<InvalidOperationException>(() =>
                mismatchedPushFrameClient.SubmitWithOutcome(submit)).Message, StringComparison.Ordinal);

            var mismatchedDropFrameClient = CreateConnectedClient(
                submitOutcomeBytes: (_, __) => ResultDropMessage.Create(sessionId: 77, frameId: 304).ToArray());
            Assert.Contains("RESULT_DROP correlation mismatch", Assert.Throws<InvalidOperationException>(() =>
                mismatchedDropFrameClient.SubmitWithOutcome(submit)).Message, StringComparison.Ordinal);

            Assert.Contains("negotiated session_id 77", Assert.Throws<InvalidOperationException>(() =>
                pushClient.SubmitWithOutcome(SmokePackets.CreateSmokeFrameSubmitMessage(sessionId: 12, frameId: 303))).Message, StringComparison.Ordinal);

            var malformedClient = CreateConnectedClient(
                submitOutcomeBytes: (_, __) => new byte[] { 1, 2, 3 });
            Assert.Contains("Failed to parse result outcome packet", Assert.Throws<InvalidOperationException>(() =>
                malformedClient.SubmitWithOutcome(submit)).Message, StringComparison.Ordinal);
        }

        [Fact]
        public void SubmitWithOutcomePreservesDegradedResultMetadata()
        {
            var submit = SmokePackets.CreateSmokeFrameSubmitMessage(sessionId: 77, frameId: 303);
            var client = CreateConnectedClient(
                submitOutcomeBytes: (_, __) => CreateResultPushMessage(
                    sessionId: 77,
                    frameId: 303,
                    wireFormat: NnrpHeader.CurrentWireFormat,
                    resultClass: ResultClass.Degraded,
                    appliedBudgetPolicy: BudgetPolicy.AllowDrop,
                    coveredTileCount: 3,
                    droppedTileCount: 0).ToArray());

            var outcome = client.SubmitWithOutcome(submit);

            Assert.False(outcome.IsResultDrop);
            Assert.Equal(ResultClass.Degraded, outcome.ResultPush.Metadata.ResultClass);
            Assert.Equal(BudgetPolicy.AllowDrop, outcome.ResultPush.Metadata.AppliedBudgetPolicy);
            Assert.Equal<ushort>(3, outcome.ResultPush.Metadata.CoveredTileCount);
            Assert.Equal<ushort>(0, outcome.ResultPush.Metadata.DroppedTileCount);
        }

        [Fact]
        public void SubmitWithOutcomePreservesPartialResultMetadata()
        {
            var submit = SmokePackets.CreateSmokeFrameSubmitMessage(sessionId: 77, frameId: 303);
            var client = CreateConnectedClient(
                submitOutcomeBytes: (_, __) => CreateResultPushMessage(
                    sessionId: 77,
                    frameId: 303,
                    wireFormat: NnrpHeader.CurrentWireFormat,
                    resultClass: ResultClass.Partial,
                    resultFlags: ResultFlags.Partial,
                    appliedBudgetPolicy: BudgetPolicy.AllowPartial | BudgetPolicy.AllowDrop,
                    coveredTileCount: 2,
                    droppedTileCount: 1).ToArray());

            var outcome = client.SubmitWithOutcome(submit);

            Assert.False(outcome.IsResultDrop);
            Assert.Equal(ResultClass.Partial, outcome.ResultPush.Metadata.ResultClass);
            Assert.Equal(ResultFlags.Partial, outcome.ResultPush.Metadata.ResultFlags);
            Assert.Equal(BudgetPolicy.AllowPartial | BudgetPolicy.AllowDrop, outcome.ResultPush.Metadata.AppliedBudgetPolicy);
            Assert.Equal<ushort>(2, outcome.ResultPush.Metadata.CoveredTileCount);
            Assert.Equal<ushort>(1, outcome.ResultPush.Metadata.DroppedTileCount);
        }

        [Fact]
        public void SubmitWithOutcomePreservesStaleReuseResultMetadata()
        {
            var submit = SmokePackets.CreateSmokeFrameSubmitMessage(sessionId: 77, frameId: 303);
            var client = CreateConnectedClient(
                submitOutcomeBytes: (_, __) => CreateResultPushMessage(
                    sessionId: 77,
                    frameId: 303,
                    wireFormat: NnrpHeader.CurrentWireFormat,
                    resultClass: ResultClass.StaleReuse,
                    resultFlags: ResultFlags.Stale,
                    appliedBudgetPolicy: BudgetPolicy.AllowStaleReuse,
                    reusedFrameId: 299,
                    coveredTileCount: 3,
                    droppedTileCount: 0).ToArray());

            var outcome = client.SubmitWithOutcome(submit);

            Assert.False(outcome.IsResultDrop);
            Assert.Equal(ResultClass.StaleReuse, outcome.ResultPush.Metadata.ResultClass);
            Assert.Equal(ResultFlags.Stale, outcome.ResultPush.Metadata.ResultFlags);
            Assert.Equal(BudgetPolicy.AllowStaleReuse, outcome.ResultPush.Metadata.AppliedBudgetPolicy);
            Assert.Equal<uint>(299U, outcome.ResultPush.Metadata.ReusedFrameId);
            Assert.Equal<ushort>(3, outcome.ResultPush.Metadata.CoveredTileCount);
            Assert.Equal<ushort>(0, outcome.ResultPush.Metadata.DroppedTileCount);
        }

        [Fact]
        public void SubmitWithOutcomePreservesTypedPayloadFrames()
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
                    PayloadKind.StructuredEvent,
                    descriptorFlags: 0,
                    profileId: 5,
                    payloadOffset: 5,
                    payloadLength: 4,
                    reserved: 0)
            };
            var payloadRegion = new byte[] { 0x41, 0x42, 0x43, 0x44, 0x45, 0x90, 0x91, 0x92, 0x93 };
            var submit = SmokePackets.CreateSmokeFrameSubmitMessage(sessionId: 77, frameId: 303);
            var client = CreateConnectedClient(
                submitOutcomeBytes: (_, __) => CreateResultPushMessage(
                    sessionId: 77,
                    frameId: 303,
                    wireFormat: NnrpHeader.CurrentWireFormat,
                    payloadKindBitmap: PayloadKind.Tensor | PayloadKind.ToolDelta | PayloadKind.StructuredEvent,
                    payloadFrameCount: 3,
                    typedPayloadDescriptors: descriptors,
                    typedPayloadFrameRegion: payloadRegion).ToArray());

            var outcome = client.SubmitWithOutcome(submit);

            Assert.False(outcome.IsResultDrop);
            Assert.Equal(PayloadKind.Tensor | PayloadKind.ToolDelta | PayloadKind.StructuredEvent, outcome.ResultPush.Metadata.PayloadKindBitmap);
            Assert.Equal<ushort>(3, outcome.ResultPush.Metadata.PayloadFrameCount);
            Assert.Equal(3, outcome.ResultPush.TypedPayloadFrames.Length);

            var toolFrames = outcome.ResultPush.GetTypedPayloadFrames(PayloadKind.ToolDelta, 9);
            Assert.Equal(2, toolFrames.Length);
            Assert.Equal(new byte[] { 0x41, 0x42 }, toolFrames[0].Payload.ToArray());
            Assert.Equal(new byte[] { 0x43, 0x44, 0x45 }, toolFrames[1].Payload.ToArray());

            var structuredEventFrames = outcome.ResultPush.GetTypedPayloadFrames(PayloadKind.StructuredEvent, 5);
            Assert.Single(structuredEventFrames);
            Assert.Equal(new byte[] { 0x90, 0x91, 0x92, 0x93 }, structuredEventFrames[0].Payload.ToArray());
        }

        [Fact]
        public void SubmitWithOutcomePreservesTokenAndMultimodalTypedPayloadFrames()
        {
            var descriptors = new[]
            {
                new TypedPayloadDescriptor(
                    PayloadKind.TokenChunk,
                    descriptorFlags: 0,
                    profileId: 3,
                    payloadOffset: 0,
                    payloadLength: 2,
                    reserved: 0),
                new TypedPayloadDescriptor(
                    PayloadKind.AudioChunk,
                    descriptorFlags: 0,
                    profileId: 4,
                    payloadOffset: 2,
                    payloadLength: 3,
                    reserved: 0),
                new TypedPayloadDescriptor(
                    PayloadKind.VideoChunk,
                    descriptorFlags: 0,
                    profileId: 5,
                    payloadOffset: 5,
                    payloadLength: 2,
                    reserved: 0),
                new TypedPayloadDescriptor(
                    PayloadKind.StructuredEvent,
                    descriptorFlags: 0,
                    profileId: 6,
                    payloadOffset: 7,
                    payloadLength: 4,
                    reserved: 0)
            };
            var payloadRegion = new byte[] { 0x31, 0x32, 0x41, 0x42, 0x43, 0x51, 0x52, 0x61, 0x62, 0x63, 0x64 };
            var submit = SmokePackets.CreateSmokeFrameSubmitMessage(sessionId: 77, frameId: 303);
            var client = CreateConnectedClient(
                submitOutcomeBytes: (_, __) => CreateResultPushMessage(
                    sessionId: 77,
                    frameId: 303,
                    wireFormat: NnrpHeader.CurrentWireFormat,
                    payloadKindBitmap: PayloadKind.Tensor | PayloadKind.TokenChunk | PayloadKind.AudioChunk | PayloadKind.VideoChunk | PayloadKind.StructuredEvent,
                    payloadFrameCount: 4,
                    typedPayloadDescriptors: descriptors,
                    typedPayloadFrameRegion: payloadRegion).ToArray());

            var outcome = client.SubmitWithOutcome(submit);

            Assert.False(outcome.IsResultDrop);
            Assert.Equal(PayloadKind.Tensor | PayloadKind.TokenChunk | PayloadKind.AudioChunk | PayloadKind.VideoChunk | PayloadKind.StructuredEvent, outcome.ResultPush.Metadata.PayloadKindBitmap);
            Assert.Equal<ushort>(4, outcome.ResultPush.Metadata.PayloadFrameCount);
            Assert.Equal(4, outcome.ResultPush.TypedPayloadFrames.Length);
            Assert.Equal(new byte[] { 0x31, 0x32 }, outcome.ResultPush.GetTypedPayloadFrames(PayloadKind.TokenChunk, 3)[0].Payload.ToArray());
            Assert.Equal(new byte[] { 0x41, 0x42, 0x43 }, outcome.ResultPush.GetTypedPayloadFrames(PayloadKind.AudioChunk, 4)[0].Payload.ToArray());
            Assert.Equal(new byte[] { 0x51, 0x52 }, outcome.ResultPush.GetTypedPayloadFrames(PayloadKind.VideoChunk, 5)[0].Payload.ToArray());
            Assert.Equal(new byte[] { 0x61, 0x62, 0x63, 0x64 }, outcome.ResultPush.GetTypedPayloadFrames(PayloadKind.StructuredEvent, 6)[0].Payload.ToArray());
        }

        [Fact]
        public void PingUsesNegotiatedSessionAndRejectsCorrelationMismatch()
        {
            ulong observedHandle = 0;
            PingMessage observedPing = default;
            var client = CreateConnectedClient(
                pingRoundTrip: (handle, ping) =>
                {
                    observedHandle = handle;
                    observedPing = ping;
                    return PongMessage.Create(sessionId: ping.Header.SessionId, traceId: ping.Header.TraceId);
                });

            var pong = client.Ping(traceId: 19);

            Assert.Equal((ulong)9, observedHandle);
            Assert.Equal(77u, observedPing.Header.SessionId);
            Assert.Equal(19ul, observedPing.Header.TraceId);
            Assert.Equal(77u, pong.Header.SessionId);
            Assert.Equal(19ul, pong.Header.TraceId);

            var mismatchClient = CreateConnectedClient(
                pingRoundTrip: (_, ping) => PongMessage.Create(sessionId: ping.Header.SessionId + 1, traceId: ping.Header.TraceId));
            Assert.Contains("PONG correlation mismatch", Assert.Throws<InvalidOperationException>(() => mismatchClient.Ping()).Message, StringComparison.Ordinal);
        }

        [Fact]
        public void CancelBuildsFrameCancelFromNegotiatedSession()
        {
            ulong observedHandle = 0;
            FrameCancelMessage observedCancel = default;
            var client = CreateConnectedClient(
                cancelFrame: (handle, cancel) =>
                {
                    observedHandle = handle;
                    observedCancel = cancel;
                });

            client.Cancel(frameId: 303, viewId: 2, traceId: 7);

            Assert.Equal((ulong)9, observedHandle);
            Assert.Equal(77u, observedCancel.Header.SessionId);
            Assert.Equal(303u, observedCancel.Header.FrameId);
            Assert.Equal((ushort)2, observedCancel.Header.ViewId);
            Assert.Equal(7ul, observedCancel.Header.TraceId);
        }

        [Fact]
        public void CloseIsIdempotentAndDisposeBlocksFurtherUse()
        {
            int closeCalls = 0;
            var client = CreateConnectedClient(
                closeConnection: _ => closeCalls++);

            client.Close();
            client.Close();
            client.Dispose();

            Assert.Equal(1, closeCalls);
            Assert.False(client.IsConnected);
            Assert.Equal(0u, client.NegotiatedSessionId);
            Assert.Equal(NnrpHeader.CurrentWireFormat, client.NegotiatedWireFormat);
            Assert.Equal(string.Empty, client.ActiveModelName);
            var error = Assert.Throws<ObjectDisposedException>(() => client.Ping());
            Assert.Equal(nameof(NnrpQuicClient), error.ObjectName);
        }

        private static NnrpQuicClient CreateConnectedClient(
            Func<ulong, FrameSubmitMessage, ResultPushMessage>? submitFrame = null,
            Func<ulong, byte[], byte[]>? submitOutcomeBytes = null,
            Action<ulong, byte[]>? beginSubmitPacket = null,
            Func<ulong, byte[]>? receiveResultPacket = null,
            Func<ulong, PingMessage, PongMessage>? pingRoundTrip = null,
            Action<ulong, FrameCancelMessage>? cancelFrame = null,
            Action<ulong>? closeConnection = null,
            Func<string, ushort, string, string, uint, NnrpNativeQuicClient.OpenResult>? openConnection = null)
        {
            var client = CreateClient(
                new NnrpQuicClientOptions("127.0.0.1", 50072, "localhost", "engine-sr", 41),
                submitFrame,
                submitOutcomeBytes,
                beginSubmitPacket,
                receiveResultPacket,
                pingRoundTrip,
                cancelFrame,
                closeConnection,
                openConnection);
            client.Connect();
            return client;
        }

        private static NnrpQuicClient CreateClient(
            NnrpQuicClientOptions options,
            Func<ulong, FrameSubmitMessage, ResultPushMessage>? submitFrame = null,
            Func<ulong, byte[], byte[]>? submitOutcomeBytes = null,
            Action<ulong, byte[]>? beginSubmitPacket = null,
            Func<ulong, byte[]>? receiveResultPacket = null,
            Func<ulong, PingMessage, PongMessage>? pingRoundTrip = null,
            Action<ulong, FrameCancelMessage>? cancelFrame = null,
            Action<ulong>? closeConnection = null,
            Func<string, ushort, string, string, uint, NnrpNativeQuicClient.OpenResult>? openConnection = null)
        {
            return new NnrpQuicClient(
                options,
                openConnection ?? OpenConnection,
                submitFrame ?? SubmitFrame,
                submitOutcomeBytes ?? SubmitOutcomeBytes,
                beginSubmitPacket ?? BeginSubmitPacket,
                receiveResultPacket ?? ReceiveResultPacket,
                pingRoundTrip ?? PingRoundTrip,
                cancelFrame ?? CancelFrame,
                closeConnection ?? CloseConnection);
        }

        private static NnrpNativeQuicClient.OpenResult OpenConnection(
            string host,
            ushort port,
            string tlsServerName,
            string requestedModel,
            uint requestedSessionId)
        {
            return new NnrpNativeQuicClient.OpenResult(9, 77, "imdn-x2-tile32");
        }

        private static ResultPushMessage SubmitFrame(ulong handle, FrameSubmitMessage message)
        {
            return CreateResultPushMessage(message.Header.SessionId, message.Header.FrameId);
        }

        private static byte[] SubmitOutcomeBytes(ulong handle, byte[] submitPacket)
        {
            return CreateResultPushMessage(sessionId: 77, frameId: 303).ToArray();
        }

        private static void BeginSubmitPacket(ulong handle, byte[] submitPacket)
        {
        }

        private static byte[] ReceiveResultPacket(ulong handle)
        {
            return CreateResultPushMessage(sessionId: 77, frameId: 303, wireFormat: NnrpHeader.CurrentWireFormat).ToArray();
        }

        private static PongMessage PingRoundTrip(ulong handle, PingMessage ping)
        {
            return PongMessage.Create(sessionId: ping.Header.SessionId, traceId: ping.Header.TraceId);
        }

        private static void CancelFrame(ulong handle, FrameCancelMessage cancel)
        {
        }

        private static void CloseConnection(ulong handle)
        {
        }

        private static ResultPushMessage CreateResultPushMessage(
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
                activeProfileId: 7,
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
                wireFormat: wireFormat,
                messageType: MessageType.ResultPush,
                flags: HeaderFlags.None,
                metaLength: ResultPushMetadata.MetadataLength,
                bodyLength: bodyLength,
                sessionId: sessionId,
                frameId: frameId,
                viewId: 0,
                routeId: 0,
                traceId: 0);
            return new ResultPushMessage(
                header,
                metadata,
                new ushort[] { 0, 1, 2 },
                new[] { section },
                typedPayloadDescriptors,
                typedPayloadFrameRegion);
        }
    }
}
