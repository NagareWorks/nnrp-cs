using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Nnrp.Core;
using Nnrp.NativeBridge;
using Xunit;

namespace Nnrp.NativeBridge.Tests
{
    public sealed class NnrpNativeQuicClientTests
    {
        [Fact]
        public void OpenValidatesArgumentsAndInjectedCallbacks()
        {
            Assert.Throws<ArgumentException>(() => NnrpNativeQuicClient.Open("", 443, "localhost", "model", 7, OpenSuccess, _ => { }));
            Assert.Throws<ArgumentException>(() => NnrpNativeQuicClient.Open("127.0.0.1", 443, "", "model", 7, OpenSuccess, _ => { }));
            Assert.Throws<ArgumentException>(() => NnrpNativeQuicClient.Open("127.0.0.1", 443, "localhost", "", 7, OpenSuccess, _ => { }));
            Assert.Throws<ArgumentNullException>(() => NnrpNativeQuicClient.Open("127.0.0.1", 443, "localhost", "model", 7, (NnrpNativeQuicClient.OpenInvoker)null!, _ => { }));
            Assert.Throws<ArgumentNullException>(() => NnrpNativeQuicClient.Open("127.0.0.1", 443, "localhost", "model", 7, OpenSuccess, null!));
        }

        [Fact]
        public void OpenReturnsNegotiatedSessionAndFreesAllocatedStrings()
        {
            var freed = new List<string>();
            var result = NnrpNativeQuicClient.Open(
                "127.0.0.1",
                50072,
                "localhost",
                "engine-sr",
                41,
                OpenSuccess,
                pointer => FreeAnsi(pointer, freed));

            Assert.Equal((ulong)9, result.Handle);
            Assert.Equal(77u, result.NegotiatedSessionId);
            Assert.Equal(NnrpHeader.CurrentWireFormat, result.NegotiatedWireFormat);
            Assert.Equal("imdn-x2-tile32", result.ActiveModelName);
            Assert.Equal(new[] { "imdn-x2-tile32" }, freed);
        }

        [Fact]
        public void OpenSurfacesNativeErrorPayloadAndFreesErrorString()
        {
            var freed = new List<string>();

            var error = Assert.Throws<InvalidOperationException>(() =>
                NnrpNativeQuicClient.Open(
                    "127.0.0.1",
                    50072,
                    "localhost",
                    "engine-sr",
                    41,
                    OpenFailure,
                    pointer => FreeAnsi(pointer, freed)));

            Assert.Equal("native-open-failed", error.Message);
            Assert.Equal(new[] { "native-open-failed" }, freed);
        }

        [Fact]
        public void OpenRejectsUnsupportedNegotiatedWireFormat()
        {
            var error = Assert.Throws<InvalidOperationException>(() =>
                NnrpNativeQuicClient.Open(
                    "127.0.0.1",
                    50072,
                    "localhost",
                    "engine-sr",
                    41,
                    OpenSuccessWithInvalidNegotiatedStage,
                    _ => { }));

            Assert.Contains("unsupported wire format", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ProbeValidatesArgumentsAndInjectedCallbacks()
        {
            var packet = new byte[] { 0xAA };

            Assert.Throws<ArgumentException>(() => NnrpNativeQuicClient.Probe("", 443, "localhost", packet, ProbeSuccess, (_, _) => { }, _ => { }));
            Assert.Throws<ArgumentException>(() => NnrpNativeQuicClient.Probe("127.0.0.1", 443, "", packet, ProbeSuccess, (_, _) => { }, _ => { }));
            Assert.Throws<ArgumentException>(() => NnrpNativeQuicClient.Probe("127.0.0.1", 443, "localhost", Array.Empty<byte>(), ProbeSuccess, (_, _) => { }, _ => { }));
            Assert.Throws<ArgumentNullException>(() => NnrpNativeQuicClient.Probe("127.0.0.1", 443, "localhost", packet, (NnrpNativeQuicClient.ProbeInvoker)null!, (_, _) => { }, _ => { }));
            Assert.Throws<ArgumentNullException>(() => NnrpNativeQuicClient.Probe("127.0.0.1", 443, "localhost", packet, ProbeSuccess, null!, _ => { }));
            Assert.Throws<ArgumentNullException>(() => NnrpNativeQuicClient.Probe("127.0.0.1", 443, "localhost", packet, ProbeSuccess, (_, _) => { }, null!));
        }

        [Fact]
        public void ProbeCopiesNativeBufferAndFreesResources()
        {
            IntPtr freedBuffer = IntPtr.Zero;
            int freedLength = -1;
            var freedStrings = new List<string>();

            var response = NnrpNativeQuicClient.Probe(
                "127.0.0.1",
                50072,
                "localhost",
                new byte[] { 0xAA, 0xBB },
                ProbeSuccess,
                (pointer, length) =>
                {
                    freedBuffer = pointer;
                    freedLength = length;
                    Marshal.FreeHGlobal(pointer);
                },
                pointer => FreeAnsi(pointer, freedStrings));

            Assert.Equal(new byte[] { 0x10, 0x20, 0x30 }, response);
            Assert.NotEqual(IntPtr.Zero, freedBuffer);
            Assert.Equal(3, freedLength);
            Assert.Empty(freedStrings);
        }

        [Fact]
        public void ProbeSurfacesNativeErrorPayloadAndFreesErrorString()
        {
            var freedStrings = new List<string>();

            var error = Assert.Throws<InvalidOperationException>(() =>
                NnrpNativeQuicClient.Probe(
                    "127.0.0.1",
                    50072,
                    "localhost",
                    new byte[] { 0x01 },
                    ProbeFailure,
                    (_, _) => { },
                    pointer => FreeAnsi(pointer, freedStrings)));

            Assert.Equal("native-probe-failed", error.Message);
            Assert.Equal(new[] { "native-probe-failed" }, freedStrings);
        }

        [Fact]
        public void SubmitUsesTypedCodecBoundaryOverByteSubmitter()
        {
            var submitMessage = SmokePackets.CreateSmokeFrameSubmitMessage(sessionId: 41, frameId: 303, viewId: 2, traceId: 9);
            var expectedResult = CreateResultPushMessage(sessionId: 41, frameId: 303);
            byte[]? observedSubmitPacket = null;

            var result = NnrpNativeQuicClient.Submit(
                submitMessage,
                submitPacket =>
                {
                    observedSubmitPacket = submitPacket;
                    return expectedResult.ToArray();
                });

            Assert.Equal(submitMessage.ToArray(), observedSubmitPacket);
            Assert.Equal(MessageType.ResultPush, result.Header.MessageType);
            Assert.Equal(41u, result.Header.SessionId);
            Assert.Equal(303u, result.Header.FrameId);
            Assert.Equal(new ushort[] { 0, 1, 2 }, result.TileIds.ToArray());
            Assert.Single(result.Sections.ToArray());
        }

        [Fact]
        public void SubmitWithTimingPassesMixedSubmitPacketWithCacheBackedObjects()
        {
            var submitPacket = CreateMixedSubmitPacket();
            byte[]? observedSubmitPacket = null;

            int SubmitMixedPacket(
                ulong handle,
                byte[] packet,
                int packetLength,
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
                out IntPtr errorPointer)
            {
                observedSubmitPacket = packet;
                Assert.Equal(submitPacket.Length, packetLength);
                return SubmitSuccess(
                    handle,
                    packet,
                    packetLength,
                    out resultPacketPointer,
                    out resultPacketLength,
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
            }

            var result = NnrpNativeQuicClient.SubmitWithTiming(
                9,
                submitPacket,
                SubmitMixedPacket,
                (_, length) => { },
                _ => { });

            Assert.Equal(submitPacket, observedSubmitPacket);
            Assert.Equal(new byte[] { 0x44, 0x55, 0x66 }, result.ResultPacket);

            Assert.True(NnrpFramedMessage.TryParse(submitPacket, NnrpHeaderParseOptions.Strict, out var framed, out var parseError));
            Assert.Equal(NnrpParseError.None, parseError);
            Assert.Equal(NnrpHeader.CurrentWireFormat, framed.Header.WireFormat);
            Assert.Equal(MessageType.FrameSubmit, framed.Header.MessageType);

            Assert.True(FrameSubmitMetadata.TryParse(framed.Metadata.Span, strict: true, out var metadata, out parseError));
            Assert.Equal(NnrpParseError.None, parseError);
            Assert.Equal(SubmitMode.Mixed, metadata.SubmitMode);
            Assert.Equal(SubmitObjectReferenceMask.Build(SubmitObjectSlot.CameraBlock, SubmitObjectSlot.TensorSectionTable), metadata.ObjectRefMask);

            Assert.True(BodyCodec.TryParse(framed.Body, out var bodyView, out parseError));
            Assert.Equal(NnrpParseError.None, parseError);
            Assert.True(
                SubmitObjectRegionValidator.TryValidate(
                    metadata.ObjectRefMask,
                    bodyView.InlineObjectRegion,
                    bodyView.ObjectReferenceRegion,
                    out var objectRegionValidation,
                    out parseError));
            Assert.Equal(NnrpParseError.None, parseError);
            Assert.Empty(objectRegionValidation.InlineBlocks);
            Assert.Equal(2, objectRegionValidation.ObjectReferenceBlocks.Length);
            Assert.Equal(CacheObjectKind.CameraBlock, objectRegionValidation.ObjectReferenceBlocks[0].ObjectKind);
            Assert.Equal(CacheObjectKind.TensorSectionTable, objectRegionValidation.ObjectReferenceBlocks[1].ObjectKind);
        }

        [Fact]
        public void SubmitRejectsNullOrEmptyByteSubmitterResponses()
        {
            var submitMessage = SmokePackets.CreateSmokeFrameSubmitMessage(sessionId: 41, frameId: 303);

            Assert.Throws<ArgumentNullException>(() => NnrpNativeQuicClient.Submit(submitMessage, null!));
            Assert.Throws<InvalidOperationException>(() => NnrpNativeQuicClient.Submit(submitMessage, _ => Array.Empty<byte>()));
        }

        [Fact]
        public void DirectByteWrappersValidateArgumentsBeforeNativeCalls()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => NnrpNativeQuicClient.Submit(0, new byte[] { 1 }));
            Assert.Throws<ArgumentException>(() => NnrpNativeQuicClient.Submit(9, Array.Empty<byte>()));
            Assert.Throws<ArgumentOutOfRangeException>(() => NnrpNativeQuicClient.Ping(0, PingMessage.Create(41, 9)));
            Assert.Throws<ArgumentOutOfRangeException>(() => NnrpNativeQuicClient.Cancel(0, FrameCancelMessage.Create(41, 303)));
        }

        [Fact]
        public void SubmitWithTimingValidatesArgumentsAndInjectedCallbacks()
        {
            var packet = new byte[] { 1, 2, 3 };

            Assert.Throws<ArgumentOutOfRangeException>(() => NnrpNativeQuicClient.SubmitWithTiming(0, packet, SubmitSuccess, (_, _) => { }, _ => { }));
            Assert.Throws<ArgumentException>(() => NnrpNativeQuicClient.SubmitWithTiming(9, Array.Empty<byte>(), SubmitSuccess, (_, _) => { }, _ => { }));
            Assert.Throws<ArgumentNullException>(() => NnrpNativeQuicClient.SubmitWithTiming(9, packet, null!, (_, _) => { }, _ => { }));
            Assert.Throws<ArgumentNullException>(() => NnrpNativeQuicClient.SubmitWithTiming(9, packet, SubmitSuccess, null!, _ => { }));
            Assert.Throws<ArgumentNullException>(() => NnrpNativeQuicClient.SubmitWithTiming(9, packet, SubmitSuccess, (_, _) => { }, null!));
        }

        [Fact]
        public void SubmitWithTimingCopiesNativeBufferAndFreesResources()
        {
            IntPtr freedBuffer = IntPtr.Zero;
            int freedLength = -1;
            var freedStrings = new List<string>();
            var result = NnrpNativeQuicClient.SubmitWithTiming(
                9,
                new byte[] { 0x11, 0x22 },
                SubmitSuccess,
                (pointer, length) =>
                {
                    freedBuffer = pointer;
                    freedLength = length;
                    Marshal.FreeHGlobal(pointer);
                },
                pointer => FreeAnsi(pointer, freedStrings));

            Assert.Equal(new byte[] { 0x44, 0x55, 0x66 }, result.ResultPacket);
            Assert.True(result.NativeCallMilliseconds >= 0);
            Assert.True(result.MarshalCopyMilliseconds >= 0);
            Assert.True(result.TotalMilliseconds >= result.MarshalCopyMilliseconds);
            Assert.Equal(1.25, result.OpenSubmitStreamMilliseconds);
            Assert.Equal(2.5, result.WriteSubmitPacketMilliseconds);
            Assert.Equal(3.75, result.FinishSubmitStreamMilliseconds);
            Assert.Equal(4.5, result.AcceptResultStreamMilliseconds);
            Assert.Equal(5.5, result.ReadResultPacketMilliseconds);
            Assert.Equal(6.5, result.ReadResultHeaderMilliseconds);
            Assert.Equal(7.5, result.ReadResultPayloadMilliseconds);
            Assert.NotEqual(IntPtr.Zero, freedBuffer);
            Assert.Equal(3, freedLength);
            Assert.Empty(freedStrings);
        }

        [Fact]
        public void SubmitWithTimingSurfacesNativeErrorPayloadAndFreesErrorString()
        {
            IntPtr freedBuffer = IntPtr.Zero;
            int freedLength = -1;
            var freedStrings = new List<string>();

            var error = Assert.Throws<InvalidOperationException>(() =>
                NnrpNativeQuicClient.SubmitWithTiming(
                    9,
                    new byte[] { 0xAA },
                    SubmitFailure,
                    (pointer, length) =>
                    {
                        freedBuffer = pointer;
                        freedLength = length;
                    },
                    pointer => FreeAnsi(pointer, freedStrings)));

            Assert.Equal("native-submit-failed", error.Message);
            Assert.Equal(IntPtr.Zero, freedBuffer);
            Assert.Equal(-1, freedLength);
            Assert.Equal(new[] { "native-submit-failed" }, freedStrings);
        }

        [Fact]
        public void ParseResultPushRejectsEmptyAndMalformedPackets()
        {
            Assert.Throws<ArgumentException>(() => NnrpNativeQuicClient.ParseResultPush(Array.Empty<byte>()));
            Assert.Throws<ArgumentException>(() => NnrpNativeQuicClient.ParseResultPush(null!));

            var error = Assert.Throws<InvalidOperationException>(() => NnrpNativeQuicClient.ParseResultPush(new byte[] { 1, 2, 3 }));
            Assert.Contains("Failed to parse RESULT_PUSH packet", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void PingUsesTypedCodecBoundaryOverByteRoundTrip()
        {
            var pingMessage = PingMessage.Create(sessionId: 41, traceId: 99);
            byte[]? observedPingPacket = null;

            var pong = NnrpNativeQuicClient.Ping(
                pingMessage,
                pingPacket =>
                {
                    observedPingPacket = pingPacket;
                    return PongMessage.Create(sessionId: 41, traceId: 99).ToArray();
                });

            Assert.Equal(pingMessage.ToArray(), observedPingPacket);
            Assert.Equal(MessageType.Pong, pong.Header.MessageType);
            Assert.Equal(41u, pong.Header.SessionId);
            Assert.Equal(99ul, pong.Header.TraceId);
        }

        [Fact]
        public void PingRejectsNullOrEmptyRoundTripResponses()
        {
            var pingMessage = PingMessage.Create(sessionId: 41, traceId: 99);

            Assert.Throws<ArgumentNullException>(() => NnrpNativeQuicClient.Ping(pingMessage, null!));
            Assert.Throws<InvalidOperationException>(() => NnrpNativeQuicClient.Ping(pingMessage, _ => Array.Empty<byte>()));
        }

        [Fact]
        public void ParsePongRejectsEmptyAndMalformedPackets()
        {
            Assert.Throws<ArgumentException>(() => NnrpNativeQuicClient.ParsePong(Array.Empty<byte>()));
            Assert.Throws<ArgumentException>(() => NnrpNativeQuicClient.ParsePong(null!));

            var error = Assert.Throws<InvalidOperationException>(() => NnrpNativeQuicClient.ParsePong(new byte[] { 1, 2, 3 }));
            Assert.Contains("Failed to parse PONG packet", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void PingByteApiValidatesArgumentsAndInjectedCallbacks()
        {
            var packet = new byte[] { 0x99 };

            Assert.Throws<ArgumentOutOfRangeException>(() => NnrpNativeQuicClient.Ping(0, packet, PingSuccess, (_, _) => { }, _ => { }));
            Assert.Throws<ArgumentException>(() => NnrpNativeQuicClient.Ping(9, Array.Empty<byte>(), PingSuccess, (_, _) => { }, _ => { }));
            Assert.Throws<ArgumentNullException>(() => NnrpNativeQuicClient.Ping(9, packet, null!, (_, _) => { }, _ => { }));
            Assert.Throws<ArgumentNullException>(() => NnrpNativeQuicClient.Ping(9, packet, PingSuccess, null!, _ => { }));
            Assert.Throws<ArgumentNullException>(() => NnrpNativeQuicClient.Ping(9, packet, PingSuccess, (_, _) => { }, null!));
        }

        [Fact]
        public void PingByteApiCopiesNativeBufferAndFreesResources()
        {
            IntPtr freedBuffer = IntPtr.Zero;
            int freedLength = -1;
            var freedStrings = new List<string>();
            var pongBytes = NnrpNativeQuicClient.Ping(
                9,
                new byte[] { 0x01 },
                PingSuccess,
                (pointer, length) =>
                {
                    freedBuffer = pointer;
                    freedLength = length;
                    Marshal.FreeHGlobal(pointer);
                },
                pointer => FreeAnsi(pointer, freedStrings));

            Assert.Equal(PongMessage.Create(sessionId: 41, traceId: 9).ToArray(), pongBytes);
            Assert.NotEqual(IntPtr.Zero, freedBuffer);
            Assert.Equal(pongBytes.Length, freedLength);
            Assert.Empty(freedStrings);
        }

        [Fact]
        public void PingByteApiSurfacesNativeErrorPayloadAndFreesErrorString()
        {
            var freedStrings = new List<string>();

            var error = Assert.Throws<InvalidOperationException>(() =>
                NnrpNativeQuicClient.Ping(
                    9,
                    new byte[] { 0x01 },
                    PingFailure,
                    (_, _) => { },
                    pointer => FreeAnsi(pointer, freedStrings)));

            Assert.Equal("native-ping-failed", error.Message);
            Assert.Equal(new[] { "native-ping-failed" }, freedStrings);
        }

        [Fact]
        public void CancelUsesTypedCodecBoundaryOverByteSender()
        {
            var cancelMessage = FrameCancelMessage.Create(sessionId: 41, frameId: 303, viewId: 2, traceId: 7);
            byte[]? observedCancelPacket = null;

            NnrpNativeQuicClient.Cancel(
                cancelMessage,
                cancelPacket => observedCancelPacket = cancelPacket);

            Assert.Equal(cancelMessage.ToArray(), observedCancelPacket);
        }

        [Fact]
        public void CancelRejectsNullTypedSender()
        {
            var cancelMessage = FrameCancelMessage.Create(sessionId: 41, frameId: 303);

            Assert.Throws<ArgumentNullException>(() => NnrpNativeQuicClient.Cancel(cancelMessage, null!));
        }

        [Fact]
        public void CancelByteApiValidatesArgumentsAndInjectedCallbacks()
        {
            var packet = new byte[] { 0x02 };

            Assert.Throws<ArgumentOutOfRangeException>(() => NnrpNativeQuicClient.Cancel(0, packet, CancelSuccess, _ => { }));
            Assert.Throws<ArgumentException>(() => NnrpNativeQuicClient.Cancel(9, Array.Empty<byte>(), CancelSuccess, _ => { }));
            Assert.Throws<ArgumentNullException>(() => NnrpNativeQuicClient.Cancel(9, packet, null!, _ => { }));
            Assert.Throws<ArgumentNullException>(() => NnrpNativeQuicClient.Cancel(9, packet, CancelSuccess, null!));
        }

        [Fact]
        public void CancelByteApiSurfacesNativeErrorPayloadAndFreesErrorString()
        {
            var freedStrings = new List<string>();

            var error = Assert.Throws<InvalidOperationException>(() =>
                NnrpNativeQuicClient.Cancel(
                    9,
                    new byte[] { 0x02 },
                    CancelFailure,
                    pointer => FreeAnsi(pointer, freedStrings)));

            Assert.Equal("native-cancel-failed", error.Message);
            Assert.Equal(new[] { "native-cancel-failed" }, freedStrings);
        }

        [Fact]
        public void ParseResultDropAndOutcomeHandleSuccessAndFailures()
        {
            var drop = ResultDropMessage.Create(sessionId: 41, frameId: 303, traceId: 7);
            var parsedDrop = NnrpNativeQuicClient.ParseResultDrop(drop.ToArray());
            Assert.Equal(drop.Header.FrameId, parsedDrop.Header.FrameId);

            var pushOutcome = NnrpNativeQuicClient.ParseResultOutcome(CreateResultPushMessage(sessionId: 41, frameId: 303).ToArray());
            Assert.False(pushOutcome.IsResultDrop);
            Assert.Equal(303u, pushOutcome.ResultPush.Header.FrameId);

            var dropOutcome = NnrpNativeQuicClient.ParseResultOutcome(drop.ToArray());
            Assert.True(dropOutcome.IsResultDrop);
            Assert.Equal(303u, dropOutcome.ResultDrop.Header.FrameId);

            Assert.Throws<ArgumentException>(() => NnrpNativeQuicClient.ParseResultDrop(Array.Empty<byte>()));
            Assert.Throws<ArgumentException>(() => NnrpNativeQuicClient.ParseResultOutcome(Array.Empty<byte>()));
            Assert.Contains("RESULT_DROP", Assert.Throws<InvalidOperationException>(() => NnrpNativeQuicClient.ParseResultDrop(new byte[] { 1, 2, 3 })).Message, StringComparison.Ordinal);
            Assert.Contains("result outcome", Assert.Throws<InvalidOperationException>(() => NnrpNativeQuicClient.ParseResultOutcome(new byte[] { 1, 2, 3 })).Message, StringComparison.Ordinal);
        }

        [Fact]
        public void CloseValidatesInjectedCallbacksAndHandlesSuccessFailureAndZeroHandle()
        {
            Assert.Throws<ArgumentNullException>(() => NnrpNativeQuicClient.Close(9, null!, _ => { }));
            Assert.Throws<ArgumentNullException>(() => NnrpNativeQuicClient.Close(9, CloseSuccess, null!));

            var freedStrings = new List<string>();
            NnrpNativeQuicClient.Close(0, CloseSuccess, pointer => FreeAnsi(pointer, freedStrings));
            Assert.Empty(freedStrings);

            NnrpNativeQuicClient.Close(9, CloseSuccess, pointer => FreeAnsi(pointer, freedStrings));
            Assert.Empty(freedStrings);

            var error = Assert.Throws<InvalidOperationException>(() =>
                NnrpNativeQuicClient.Close(9, CloseFailure, pointer => FreeAnsi(pointer, freedStrings)));
            Assert.Equal("native-close-failed", error.Message);
            Assert.Equal(new[] { "native-close-failed" }, freedStrings);
        }

        private static ResultPushMessage CreateResultPushMessage(uint sessionId, uint frameId)
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
                activeProfileId: 7,
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

        private static byte[] CreateMixedSubmitPacket()
        {
            var objectRefMask = SubmitObjectReferenceMask.Build(
                SubmitObjectSlot.CameraBlock,
                SubmitObjectSlot.TensorSectionTable);
            var objectReferenceRegion = BodyCodec.PackObjectReferenceRegion(
                BodyCodec.BuildObjectReferenceBlock(CacheObjectKind.CameraBlock, 7, 11, 13),
                BodyCodec.BuildObjectReferenceBlock(CacheObjectKind.TensorSectionTable, 17, 19, 23));
            var body = BodyCodec.Pack(objectReferenceRegion: objectReferenceRegion);
            var metadata = new FrameSubmitMetadata(
                sourceWidth: 1920,
                sourceHeight: 1080,
                tileWidth: 128,
                tileHeight: 128,
                tileCount: 0,
                sectionCount: 0,
                frameClass: FrameClass.Keyframe,
                inputProfile: InputProfile.DenseLumaFrame,
                tileIndexMode: TileIndexMode.DenseRange,
                reserved0: 0,
                latencyBudgetMilliseconds: 16,
                targetFpsTimes100: 6000,
                retryOfFrame: 0,
                tileBaseId: 0,
                cameraBytes: 0,
                tileIndexBytes: 0,
                reserved1: 0,
                reserved2: 0,
                submitMode: SubmitMode.Mixed,
                budgetPolicy: BudgetPolicy.None,
                lossTolerancePolicy: 0xFF,
                reserved3: 0,
                objectRefMask: objectRefMask,
                dependencyFrameId: 0,
                payloadKindBitmap: PayloadKind.Tensor,
                payloadFrameCount: 0,
                reserved4: 0);
            var header = new NnrpHeader(
                versionMajor: NnrpHeader.CurrentVersionMajor,
                wireFormat: NnrpHeader.CurrentWireFormat,
                messageType: MessageType.FrameSubmit,
                flags: HeaderFlags.None,
                metaLength: FrameSubmitMetadata.MetadataLength,
                bodyLength: (uint)body.Length,
                sessionId: 41,
                frameId: 303,
                viewId: 2,
                routeId: 0,
                traceId: 9);
            return new NnrpFramedMessage(header, metadata.ToArray(), body).ToArray();
        }

        private static int OpenSuccess(
            string host,
            ushort port,
            string tlsServerName,
            string requestedModel,
            uint requestedSessionId,
            byte requestedWireFormat,
            out ulong handle,
            out uint negotiatedSessionId,
            out byte negotiatedWireFormat,
            out IntPtr activeModelNamePointer,
            out IntPtr errorPointer)
        {
            handle = 9;
            negotiatedSessionId = 77;
            negotiatedWireFormat = requestedWireFormat;
            activeModelNamePointer = AllocAnsi("imdn-x2-tile32");
            errorPointer = IntPtr.Zero;
            return 0;
        }

        private static int OpenFailure(
            string host,
            ushort port,
            string tlsServerName,
            string requestedModel,
            uint requestedSessionId,
            byte requestedWireFormat,
            out ulong handle,
            out uint negotiatedSessionId,
            out byte negotiatedWireFormat,
            out IntPtr activeModelNamePointer,
            out IntPtr errorPointer)
        {
            handle = 0;
            negotiatedSessionId = 0;
            negotiatedWireFormat = 0;
            activeModelNamePointer = IntPtr.Zero;
            errorPointer = AllocAnsi("native-open-failed");
            return 1;
        }

        private static int OpenSuccessWithInvalidNegotiatedStage(
            string host,
            ushort port,
            string tlsServerName,
            string requestedModel,
            uint requestedSessionId,
            byte requestedWireFormat,
            out ulong handle,
            out uint negotiatedSessionId,
            out byte negotiatedWireFormat,
            out IntPtr activeModelNamePointer,
            out IntPtr errorPointer)
        {
            handle = 9;
            negotiatedSessionId = 77;
            negotiatedWireFormat = 99;
            activeModelNamePointer = IntPtr.Zero;
            errorPointer = IntPtr.Zero;
            return 0;
        }

        private static int SubmitSuccess(
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
            out IntPtr errorPointer)
        {
            var buffer = new byte[] { 0x44, 0x55, 0x66 };
            resultPacketPointer = Marshal.AllocHGlobal(buffer.Length);
            Marshal.Copy(buffer, 0, resultPacketPointer, buffer.Length);
            resultPacketLength = buffer.Length;
            openSubmitStreamMilliseconds = 1.25;
            writeSubmitPacketMilliseconds = 2.5;
            finishSubmitStreamMilliseconds = 3.75;
            acceptResultStreamMilliseconds = 4.5;
            readResultPacketMilliseconds = 5.5;
            readResultHeaderMilliseconds = 6.5;
            readResultPayloadMilliseconds = 7.5;
            quicRttBeforeMilliseconds = 8.5;
            quicRttAfterAcceptMilliseconds = 9.5;
            quicRttAfterReadMilliseconds = 10.5;
            quicCwndBeforeBytes = 111;
            quicCwndAfterAcceptBytes = 222;
            quicCwndAfterReadBytes = 333;
            quicSentPacketsDuringAccept = 4;
            quicLostPacketsDuringAccept = 1;
            quicCongestionEventsDuringAccept = 2;
            quicSentPacketsTotal = 7;
            quicLostPacketsTotal = 3;
            quicCongestionEventsTotal = 5;
            errorPointer = IntPtr.Zero;
            return 0;
        }

        private static int ProbeSuccess(
            string host,
            ushort port,
            string tlsServerName,
            byte[] probePacket,
            int probePacketLength,
            byte requestedWireFormat,
            out IntPtr responsePacketPointer,
            out int responsePacketLength,
            out IntPtr errorPointer)
        {
            var buffer = new byte[] { 0x10, 0x20, 0x30 };
            responsePacketPointer = Marshal.AllocHGlobal(buffer.Length);
            Marshal.Copy(buffer, 0, responsePacketPointer, buffer.Length);
            responsePacketLength = buffer.Length;
            errorPointer = IntPtr.Zero;
            return 0;
        }

        private static int ProbeFailure(
            string host,
            ushort port,
            string tlsServerName,
            byte[] probePacket,
            int probePacketLength,
            byte requestedWireFormat,
            out IntPtr responsePacketPointer,
            out int responsePacketLength,
            out IntPtr errorPointer)
        {
            responsePacketPointer = IntPtr.Zero;
            responsePacketLength = 0;
            errorPointer = AllocAnsi("native-probe-failed");
            return 1;
        }

        private static int SubmitFailure(
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
            out IntPtr errorPointer)
        {
            resultPacketPointer = IntPtr.Zero;
            resultPacketLength = 0;
            openSubmitStreamMilliseconds = 0;
            writeSubmitPacketMilliseconds = 0;
            finishSubmitStreamMilliseconds = 0;
            acceptResultStreamMilliseconds = 0;
            readResultPacketMilliseconds = 0;
            readResultHeaderMilliseconds = 0;
            readResultPayloadMilliseconds = 0;
            quicRttBeforeMilliseconds = 0;
            quicRttAfterAcceptMilliseconds = 0;
            quicRttAfterReadMilliseconds = 0;
            quicCwndBeforeBytes = 0;
            quicCwndAfterAcceptBytes = 0;
            quicCwndAfterReadBytes = 0;
            quicSentPacketsDuringAccept = 0;
            quicLostPacketsDuringAccept = 0;
            quicCongestionEventsDuringAccept = 0;
            quicSentPacketsTotal = 0;
            quicLostPacketsTotal = 0;
            quicCongestionEventsTotal = 0;
            errorPointer = AllocAnsi("native-submit-failed");
            return 1;
        }

        private static int PingSuccess(
            ulong handle,
            byte[] pingPacket,
            int pingPacketLength,
            out IntPtr pongPacketPointer,
            out int pongPacketLength,
            out IntPtr errorPointer)
        {
            var pongBytes = PongMessage.Create(sessionId: 41, traceId: 9).ToArray();
            pongPacketPointer = Marshal.AllocHGlobal(pongBytes.Length);
            Marshal.Copy(pongBytes, 0, pongPacketPointer, pongBytes.Length);
            pongPacketLength = pongBytes.Length;
            errorPointer = IntPtr.Zero;
            return 0;
        }

        private static int PingFailure(
            ulong handle,
            byte[] pingPacket,
            int pingPacketLength,
            out IntPtr pongPacketPointer,
            out int pongPacketLength,
            out IntPtr errorPointer)
        {
            pongPacketPointer = IntPtr.Zero;
            pongPacketLength = 0;
            errorPointer = AllocAnsi("native-ping-failed");
            return 1;
        }

        private static int CancelSuccess(
            ulong handle,
            byte[] cancelPacket,
            int cancelPacketLength,
            out IntPtr errorPointer)
        {
            errorPointer = IntPtr.Zero;
            return 0;
        }

        private static int CancelFailure(
            ulong handle,
            byte[] cancelPacket,
            int cancelPacketLength,
            out IntPtr errorPointer)
        {
            errorPointer = AllocAnsi("native-cancel-failed");
            return 1;
        }

        private static int CloseSuccess(ulong handle, out IntPtr errorPointer)
        {
            errorPointer = IntPtr.Zero;
            return 0;
        }

        private static int CloseFailure(ulong handle, out IntPtr errorPointer)
        {
            errorPointer = AllocAnsi("native-close-failed");
            return 1;
        }

        private static IntPtr AllocAnsi(string value)
        {
            return Marshal.StringToHGlobalAnsi(value);
        }

        private static void FreeAnsi(IntPtr pointer, ICollection<string> freed)
        {
            freed.Add(Marshal.PtrToStringAnsi(pointer) ?? string.Empty);
            Marshal.FreeHGlobal(pointer);
        }
    }
}
