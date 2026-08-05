using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nnrp.Core;
using Nnrp.NativeBridge;
using Nnrp.Runtime;
using Nnrp.TestSupport;
using Xunit;

namespace Nnrp.Server.Tests
{
    public sealed class RuntimeServerTests
    {
        [Fact]
        public async Task ServerGuardsNullOptionsAndRepeatedDisposal()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await NnrpServer.ListenAsync(null!));

            using var harness = new RuntimeEntrypointHarness();
            var listener = new TestListener(CreateNativeSession(harness));
            var server = new NnrpServer(
                new NnrpServerOptions(NnrpEndpoint.Parse("nnrp://localhost/runtime/default")),
                new NnrpServerTransportListenerSet(new[] { listener }));

            await server.DisposeAsync();
            await server.DisposeAsync();
            Assert.True(server.IsClosed);
            await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
                await server.AcceptAsync());
        }

        [Fact]
        public async Task AcceptAndReceiveSubmitKeepEveryBatchedEvent()
        {
            using var harness = new RuntimeEntrypointHarness();
            var nativeSession = CreateNativeSession(harness);
            var listener = new TestListener(nativeSession);
            await using var server = new NnrpServer(
                new NnrpServerOptions(NnrpEndpoint.Parse("nnrp://localhost/runtime/default")),
                new NnrpServerTransportListenerSet(new[] { listener }));
            harness.QueueServerBatch(
                harness.CreateEvent(
                    MessageType.Progress,
                    1,
                    401,
                    NnrpRuntimeControl.Encode(
                        MessageType.Progress,
                        new ProgressMetadata(401, 1, 1, 5000, 0, 0))),
                harness.CreateEvent(MessageType.FrameSubmit, 41, 401, SubmitPayload(401)));

            await using var session = await server.AcceptAsync(
                new NnrpServerAcceptOptions(timeoutMilliseconds: 100));
            var operation = await session.ReceiveSubmitAsync();
            var progress = await session.NextEventAsync();

            Assert.Equal(TransportId.Tcp, session.ActiveTransportId);
            Assert.Equal(NnrpProviderEndpoint.Parse("tcp://127.0.0.1:4100"), server.BoundProviderEndpoints[TransportId.Tcp]);
            Assert.Equal((ulong)401, operation.OperationId);
            Assert.Equal((uint)41, operation.FrameId);
            Assert.Equal((ulong)0, operation.TraceId);
            Assert.Equal(NnrpRuntimeEventMetadataKind.FrameSubmit, operation.Submit.Metadata.Kind);
            Assert.Equal((ulong)401, operation.Metadata.OperationId);
            Assert.Empty(operation.Body.ToArray());
            Assert.Equal(MessageType.Progress, progress.Header.MessageType);
        }

        [Fact]
        public async Task OperationsEnforceExactlyOneSuccessfulTerminalSend()
        {
            using var harness = new RuntimeEntrypointHarness();
            var nativeSession = CreateNativeSession(harness);
            var listener = new TestListener(nativeSession);
            await using var server = new NnrpServer(
                new NnrpServerOptions(NnrpEndpoint.Parse("nnrp://localhost/runtime/default")),
                new NnrpServerTransportListenerSet(new[] { listener }));
            await using var session = await server.AcceptAsync();

            harness.QueueServerBatch(harness.CreateEvent(MessageType.FrameSubmit, 42, 402, SubmitPayload(402)));
            var resultOperation = await session.ReceiveSubmitAsync();
            harness.NextServerResultStatus = new NnrpFfiStatus(NnrpFfiStatusCode.InvalidState);
            await Assert.ThrowsAsync<NnrpNativeInvalidStateException>(async () =>
                await resultOperation.SendResultAsync(SuccessMetadata(), new byte[] { 9 }));
            await resultOperation.SendResultAsync(SuccessMetadata(), new byte[] { 9 });
            await Assert.ThrowsAsync<NnrpNativeInvalidStateException>(async () =>
                await resultOperation.SendResultAsync(SuccessMetadata()));

            harness.QueueServerBatch(harness.CreateEvent(MessageType.FrameSubmit, 43, 403, SubmitPayload(403)));
            var dropOperation = await session.ReceiveSubmitAsync();
            var drop = new ResultDropReasonMetadata(
                403,
                1,
                NnrpResultDropReasonCode.Backpressure,
                RuntimeRole.Server,
                0,
                2);
            await dropOperation.SendResultDropAsync(drop, new byte[] { 1, 2 });
            await Assert.ThrowsAsync<NnrpNativeInvalidStateException>(async () =>
                await dropOperation.SendResultDropAsync(drop));

            Assert.Equal(2, harness.ServerResults.Count);
            var dropFrame = Assert.Single(
                harness.RuntimeFrames,
                frame => frame.Request.MessageType == (uint)MessageType.ResultDropReason);
            Assert.Equal(dropOperation.OperationId + 10_000, dropFrame.Request.Handle.Id);
            var decodedDrop = NnrpRuntimeControl.Decode(
                MessageType.ResultDropReason,
                dropFrame.Payload);
            Assert.Equal(drop, decodedDrop.GetMetadata<ResultDropReasonMetadata>());
            Assert.Equal(new byte[] { 1, 2 }, decodedDrop.Tail.ToArray());
        }

        [Fact]
        public async Task TypedServerMethodsUseOneNativeFrameEach()
        {
            using var harness = new RuntimeEntrypointHarness();
            var nativeSession = CreateNativeSession(harness);
            var listener = new TestListener(nativeSession);
            await using var server = new NnrpServer(
                new NnrpServerOptions(NnrpEndpoint.Parse("nnrp://localhost/runtime/default")),
                new NnrpServerTransportListenerSet(new[] { listener }));
            await using var session = await server.AcceptAsync();
            var diagnostic = new byte[] { 1, 2 };
            var body = new byte[] { 3, 4, 5 };

            await session.SendProgressAsync(new ProgressMetadata(1, 2, 3, 4000, 4, 3), body);
            await session.SendPartialResultAsync(new PartialResultMetadata(1, 2, 3, 4, 3, 0), body);
            await session.SendBackpressureAsync(new PressureMetadata(1, 2, 3, 4, 5, 0));
            await session.SendCreditUpdateAsync(new PressureMetadata(1, 2, 3, 4, 5, 0));
            await session.SendResultDropReasonAsync(
                new ResultDropReasonMetadata(1, 2, NnrpResultDropReasonCode.Backpressure, RuntimeRole.Server, 0, 2),
                diagnostic);
            await session.SendTraceContextAsync(new TraceContextMetadata(1, 2, 3, 4, 0, 3), body);
            await session.SendRecoverableErrorAsync(
                new RecoverableErrorMetadata(1, 2, 3, RuntimeRole.Server, 0, 4, 5, 6, 7, 2),
                diagnostic);
            await session.SendRetryAfterAsync(
                new RetryAfterMetadata(1, 2, 3, 4, 5, RuntimeRole.Server, 0, 2),
                diagnostic);
            await session.SendControlAsync(
                MessageType.Progress,
                new ProgressMetadata(1, 2, 3, 4000, 4, 3),
                body);

            await session.DeclareObjectAsync(
                new ObjectDescriptorMetadata(1, RuntimeObjectKind.Tensor, RuntimeRole.Server, RuntimeRole.Client, 2, 3, 4, MemoryLocationHint.HostMemory, OwnershipHint.TransferOnRef, 5, 3),
                body);
            await session.ReferenceObjectAsync(new ObjectReferenceMetadata(1, 2, 3, 4, 5, 0, 3), body);
            await session.ReleaseObjectAsync(new ObjectReleaseMetadata(1, 2, ObjectReleaseReason.Completed, RuntimeRole.Server, 0, 2), diagnostic);
            await session.PatchObjectAsync(new ObjectDeltaMetadata(1, 2, 3, 4, 2, 0, 1), new byte[] { 8 }, new byte[] { 9, 10 });
            await session.SendObjectDeltaAsync(new ObjectDeltaMetadata(1, 3, 4, 5, 2, 0, 1), new byte[] { 8 }, new byte[] { 9, 10 });
            await session.ReferenceCacheAsync(new CacheReferenceMetadata(1, 2, 3, 4, CacheReuseScope.Session, 5, 6, 7, 3, 0), body);
            await session.ReportCacheMissAsync(new CacheMissMetadata(1, 2, 3, CacheMissReason.NotFound, 4, 2), diagnostic);
            await session.InvalidateCacheAsync(new CacheInvalidateMetadata(CacheInvalidateScope.ObjectKey, 1, 2, 3, 4));

            Assert.Equal(
                new[]
                {
                    MessageType.Progress,
                    MessageType.PartialResult,
                    MessageType.Backpressure,
                    MessageType.CreditUpdate,
                    MessageType.ResultDropReason,
                    MessageType.TraceContext,
                    MessageType.ErrorRecoverable,
                    MessageType.RetryAfter,
                    MessageType.Progress,
                    MessageType.ObjectDeclare,
                    MessageType.ObjectRef,
                    MessageType.ObjectRelease,
                    MessageType.ObjectPatch,
                    MessageType.ObjectDelta,
                    MessageType.CacheReference,
                    MessageType.CacheMiss,
                    MessageType.CacheInvalidate,
                },
                harness.RuntimeFrames.Select(frame => (MessageType)frame.Request.MessageType).ToArray());
            Assert.Throws<ArgumentException>(() =>
                session.SendControlAsync(MessageType.Cancel, new ProgressMetadata(1, 2, 3, 4, 5, 0)));
            Assert.Throws<ArgumentException>(() =>
                session.PatchObjectAsync(
                    new ObjectDeltaMetadata(1, 2, 3, 4, 2, 0, 2),
                    new byte[] { 8 },
                    new byte[] { 9, 10 }));
            Assert.Throws<ArgumentException>(() =>
                session.SendObjectDeltaAsync(
                    new ObjectDeltaMetadata(1, 2, 3, 4, 1, 0, 1),
                    new byte[] { 8 },
                    new byte[] { 9, 10 }));
        }

        [Fact]
        public async Task GenericServerControlDispatchCoversEveryFrozenSendableType()
        {
            using var harness = new RuntimeEntrypointHarness();
            var listener = new TestListener(CreateNativeSession(harness));
            await using var server = new NnrpServer(
                new NnrpServerOptions(NnrpEndpoint.Parse("nnrp://localhost/runtime/default")),
                new NnrpServerTransportListenerSet(new[] { listener }));
            await using var session = await server.AcceptAsync();
            var tail = new byte[] { 1, 2 };

            await session.SendControlAsync(MessageType.Progress, new ProgressMetadata(1, 2, 3, 4, 0, 2), tail);
            await session.SendControlAsync(MessageType.PartialResult, new PartialResultMetadata(1, 2, 3, 4, 2, 0), tail);
            await session.SendControlAsync(MessageType.Backpressure, new PressureMetadata(1, 2, 3, 4, 5, 0));
            await session.SendControlAsync(MessageType.CreditUpdate, new PressureMetadata(1, 2, 3, 4, 5, 0));
            await session.SendControlAsync(MessageType.ResultDropReason, new ResultDropReasonMetadata(1, 2, NnrpResultDropReasonCode.Backpressure, RuntimeRole.Server, 0, 2), tail);
            await session.SendControlAsync(MessageType.TraceContext, new TraceContextMetadata(1, 2, 3, 4, 0, 2), tail);
            await session.SendControlAsync(MessageType.ErrorRecoverable, new RecoverableErrorMetadata(1, 2, 3, RuntimeRole.Server, 0, 4, 5, 6, 7, 2), tail);
            await session.SendControlAsync(MessageType.RetryAfter, new RetryAfterMetadata(1, 2, 3, 4, 5, RuntimeRole.Server, 0, 2), tail);

            Assert.Equal(8, harness.RuntimeFrames.Count);
        }

        private static NnrpNativeRuntimeServerSession CreateNativeSession(RuntimeEntrypointHarness harness)
        {
            return new NnrpNativeRuntimeServerSession(
                harness.Entrypoints,
                new NnrpConnectionHandle(new NnrpHandle(NnrpHandleKind.Connection, 1, 1)),
                new NnrpSessionHandle(new NnrpHandle(NnrpHandleKind.Session, 41, 1)),
                TransportId.Tcp);
        }

        private static byte[] SubmitPayload(ulong operationId)
        {
            return new FrameSubmitMetadata(
                0,
                0,
                0,
                0,
                0,
                0,
                FrameClass.Keyframe,
                InputProfile.Unspecified,
                TileIndexMode.RawUInt16,
                0,
                0,
                0,
                0,
                0,
                0,
                operationId,
                SubmitMode.Inline,
                BudgetPolicy.None,
                LossTolerancePolicy.InheritSession,
                0,
                0,
                PayloadKind.None,
                0).ToArray();
        }

        private static ResultPushMetadata SuccessMetadata()
        {
            return new ResultPushMetadata(
                ResultStatusCode.Success,
                ResultFlags.None,
                0,
                PayloadKind.None,
                0,
                1,
                2,
                3,
                0,
                0,
                0,
                0);
        }

        private sealed class TestListener : INnrpServerTransportListener
        {
            private NnrpNativeRuntimeServerSession? session;

            internal TestListener(NnrpNativeRuntimeServerSession session)
            {
                this.session = session;
            }

            public TransportId TransportId => TransportId.Tcp;

            public NnrpProviderEndpoint BoundEndpoint { get; } =
                NnrpProviderEndpoint.Parse("tcp://127.0.0.1:4100");

            public ValueTask<NnrpAcceptedServerTransportSession> AcceptAsync(
                NnrpServerAcceptOptions options,
                uint pollTimeoutMilliseconds,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var accepted = session ?? throw new NnrpNativeWouldBlockException(
                    new NnrpFfiStatus(NnrpFfiStatusCode.WouldBlock));
                session = null;
                return new ValueTask<NnrpAcceptedServerTransportSession>(
                    new NnrpAcceptedServerTransportSession(
                        TransportId.Tcp,
                        accepted,
                        () =>
                        {
                            if (!accepted.IsClosed)
                            {
                                accepted.Close();
                            }
                        }));
            }

            public bool ReleasePendingAccept() => false;

            public ValueTask DisposeAsync()
            {
                if (session != null && !session.IsClosed)
                {
                    session.Close();
                }

                session = null;
                return default;
            }
        }
    }
}
