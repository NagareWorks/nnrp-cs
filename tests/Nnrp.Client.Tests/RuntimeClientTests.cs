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

namespace Nnrp.Client.Tests
{
    public sealed class RuntimeClientTests
    {
        [Fact]
        public async Task ClientAndResultRejectMissingRequiredIdentity()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await NnrpClient.ConnectAsync(null!));

            var @event = NnrpRuntimeEvent.Decode(
                new RuntimeFrameHeader(MessageType.ResultPush),
                ResultPayload(ResultStatusCode.Success));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new NnrpResult(0, NnrpResultTerminalState.Success, @event));
            Assert.Throws<ArgumentNullException>(() =>
                new NnrpResult(1, NnrpResultTerminalState.Success, null!));

            using var harness = new RuntimeEntrypointHarness();
            var client = CreateClient(harness);
            await client.DisposeAsync();
            await client.DisposeAsync();
            Assert.True(client.IsClosed);
            Assert.Throws<ObjectDisposedException>(() => client.OpenSession());
        }

        [Fact]
        public async Task SubmitRoutesDeferredResultsAndPreservesWireOrder()
        {
            using var harness = new RuntimeEntrypointHarness();
            await using var client = CreateClient(harness);
            await using var session = client.OpenSession(new NnrpClientSessionOptions(sessionId: 41));
            var progress = new ProgressMetadata(201, 1, 2, 5000, 1, 1);
            harness.QueueClientEvent(
                MessageType.Progress,
                1,
                201,
                NnrpRuntimeControl.Encode(MessageType.Progress, progress, new byte[] { 9 }));
            harness.QueueClientEvent(MessageType.ResultPush, 11, 202, SuccessPayload());
            harness.QueueClientEvent(MessageType.ResultPush, 11, 201, SuccessPayload());

            var result = await session.SubmitAsync(CreateSubmit(201, 11));
            var deferredResult = await session.NextResultAsync();
            var deferredEvent = await session.NextEventAsync();

            Assert.Equal((ulong)201, result.OperationId);
            Assert.Equal(NnrpResultTerminalState.Success, result.TerminalState);
            Assert.Equal(MessageType.ResultPush, result.Header.MessageType);
            Assert.NotNull(result.ResultMetadata);
            Assert.Null(result.DropMetadata);
            Assert.Empty(result.Diagnostic.ToArray());
            Assert.Empty(result.Body.ToArray());
            Assert.Equal((uint)41, session.Options.SessionId);
            Assert.False(session.IsClosed);
            Assert.Equal(TransportId.Tcp, client.ActiveTransportId);
            Assert.Equal(
                NnrpEndpoint.Parse("nnrp://localhost/runtime/default"),
                client.Options.Endpoint);
            Assert.Equal("test-tcp", client.Selection.SelectedProvider.Name);
            Assert.Equal((ulong)202, deferredResult.OperationId);
            Assert.NotNull(deferredResult.ResultMetadata);
            Assert.Equal(MessageType.Progress, deferredEvent.Header.MessageType);
            Assert.Equal(progress, deferredEvent.Metadata.Get<ProgressMetadata>());
            Assert.Single(harness.SubmitRequests);
            Assert.Equal((ulong)201, harness.SubmitRequests[0].OperationId);
            Assert.Equal((uint)11, harness.SubmitRequests[0].FrameId);
        }

        [Fact]
        public async Task CancelSuppressesLatePayloadsButKeepsDropEvidence()
        {
            using var harness = new RuntimeEntrypointHarness();
            await using var client = CreateClient(harness);
            await using var session = client.OpenSession(new NnrpClientSessionOptions(sessionId: 41));
            var cancel = new ControlRequestMetadata(301, 1, 2, RuntimeRole.Client, 0, 2);
            await session.CancelAsync(cancel, new byte[] { 1, 2 });
            harness.QueueClientEvent(MessageType.ResultPush, 31, 301, SuccessPayload());
            harness.QueueClientEvent(
                MessageType.PartialResult,
                32,
                301,
                NnrpRuntimeControl.Encode(
                    MessageType.PartialResult,
                    new PartialResultMetadata(301, 2, 3, 4, 1, 0),
                    new byte[] { 7 }));
            harness.QueueClientEvent(
                MessageType.TraceContext,
                33,
                301,
                NnrpRuntimeControl.Encode(
                    MessageType.TraceContext,
                    new TraceContextMetadata(9, 10, 11, 1, 0, 0)));
            var drop = new ResultDropReasonMetadata(
                301,
                3,
                NnrpResultDropReasonCode.PeerCancelled,
                RuntimeRole.Server,
                0,
                2);
            harness.QueueClientEvent(
                MessageType.ResultDropReason,
                34,
                301,
                NnrpRuntimeControl.Encode(MessageType.ResultDropReason, drop, new byte[] { 3, 4 }));

            var result = await session.NextResultAsync();
            var trace = await session.NextEventAsync();

            Assert.Equal(NnrpResultTerminalState.Cancelled, result.TerminalState);
            Assert.Equal(drop, result.DropMetadata);
            Assert.Equal(new byte[] { 3, 4 }, result.Diagnostic.ToArray());
            Assert.Equal(MessageType.TraceContext, trace.Header.MessageType);
            Assert.Contains(harness.RuntimeFrames, frame => frame.Request.MessageType == (uint)MessageType.Cancel);
        }

        [Theory]
        [InlineData(ResultStatusCode.Success, NnrpResultTerminalState.Success)]
        [InlineData(ResultStatusCode.Degraded, NnrpResultTerminalState.Success)]
        [InlineData(ResultStatusCode.Rejected, NnrpResultTerminalState.Error)]
        [InlineData(ResultStatusCode.Failed, NnrpResultTerminalState.Error)]
        public async Task ResultStatusMapsToFrozenTerminalState(
            ResultStatusCode statusCode,
            NnrpResultTerminalState terminalState)
        {
            using var harness = new RuntimeEntrypointHarness();
            await using var client = CreateClient(harness);
            await using var session = client.OpenSession(new NnrpClientSessionOptions(sessionId: 41));
            harness.QueueClientEvent(MessageType.ResultPush, 11, 201, ResultPayload(statusCode));

            var result = await session.NextResultAsync();

            Assert.Equal(terminalState, result.TerminalState);
        }

        [Fact]
        public async Task TypedClientMethodsUseOneNativeFrameEach()
        {
            using var harness = new RuntimeEntrypointHarness();
            await using var client = CreateClient(harness);
            await using var session = client.OpenSession(new NnrpClientSessionOptions(sessionId: 41));
            var diagnostic = new byte[] { 1, 2 };
            var body = new byte[] { 3, 4, 5 };

            await session.AbortAsync(new ControlRequestMetadata(1, 1, 1, RuntimeRole.Client, 0, 2), diagnostic);
            await session.UpdatePriorityAsync(new SchedulingMetadata(1, 2, 3, -1, 4, 0));
            await session.UpdateDeadlineAsync(new SchedulingMetadata(1, 3, 3, 0, 4, 0));
            await session.ExpireAtAsync(new SchedulingMetadata(1, 4, 3, 1, 4, 0));
            await session.SupersedeAsync(new SupersedeMetadata(1, 5, 2, NnrpResultDropReasonCode.Superseded, 0, 2), diagnostic);
            await session.UpdateBudgetAsync(new BudgetMetadata(1, 6, 7, 8, 9, 0));
            await session.NegotiateCapabilitiesAsync(new CapabilityMetadata(1, 2, 3, 4, 5, 6, 3, 0), body);
            await session.DegradeProfileAsync(new CapabilityMetadata(1, 2, 3, 4, 5, 6, 3, 0), body);
            await session.SendRouteHintAsync(new RouteHintMetadata(1, 2, 3, 4, 5, 3, 0), body);
            await session.SendExecutionHintAsync(new RouteHintMetadata(1, 2, 3, 4, 5, 3, 0), body);
            await session.SendTraceContextAsync(new TraceContextMetadata(1, 2, 3, 4, 0, 3), body);
            await session.SendControlAsync(
                MessageType.PriorityUpdate,
                new SchedulingMetadata(1, 7, 4, 1, 8, 0));

            await session.DeclareObjectAsync(
                new ObjectDescriptorMetadata(1, RuntimeObjectKind.Tensor, RuntimeRole.Client, RuntimeRole.Server, 2, 3, 4, MemoryLocationHint.HostMemory, OwnershipHint.TransferOnRef, 5, 3),
                body);
            await session.ReferenceObjectAsync(new ObjectReferenceMetadata(1, 2, 3, 4, 5, 0, 3), body);
            await session.ReleaseObjectAsync(new ObjectReleaseMetadata(1, 2, ObjectReleaseReason.Completed, RuntimeRole.Client, 0, 2), diagnostic);
            await session.PatchObjectAsync(new ObjectDeltaMetadata(1, 2, 3, 4, 2, 0, 1), new byte[] { 8 }, new byte[] { 9, 10 });
            await session.SendObjectDeltaAsync(new ObjectDeltaMetadata(1, 3, 4, 5, 2, 0, 1), new byte[] { 8 }, new byte[] { 9, 10 });
            await session.ReferenceCacheAsync(new CacheReferenceMetadata(1, 2, 3, 4, CacheReuseScope.Session, 5, 6, 7, 3, 0), body);
            await session.ReportCacheMissAsync(new CacheMissMetadata(1, 2, 3, CacheMissReason.NotFound, 4, 2), diagnostic);
            await session.InvalidateCacheAsync(new CacheInvalidateMetadata(CacheInvalidateScope.ObjectKey, 1, 2, 3, 4));

            var messageTypes = harness.RuntimeFrames.Select(frame => (MessageType)frame.Request.MessageType).ToArray();
            Assert.Equal(
                new[]
                {
                    MessageType.Abort,
                    MessageType.PriorityUpdate,
                    MessageType.Deadline,
                    MessageType.ExpireAt,
                    MessageType.Supersede,
                    MessageType.BudgetUpdate,
                    MessageType.CapabilityNegotiation,
                    MessageType.DegradeProfile,
                    MessageType.RouteHint,
                    MessageType.ExecutionHint,
                    MessageType.TraceContext,
                    MessageType.PriorityUpdate,
                    MessageType.ObjectDeclare,
                    MessageType.ObjectRef,
                    MessageType.ObjectRelease,
                    MessageType.ObjectPatch,
                    MessageType.ObjectDelta,
                    MessageType.CacheReference,
                    MessageType.CacheMiss,
                    MessageType.CacheInvalidate,
                },
                messageTypes);
            Assert.Throws<ArgumentException>(() =>
                session.SendControlAsync(MessageType.Progress, new SchedulingMetadata(1, 1, 1, 0, 0, 0)));
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
        public async Task GenericClientControlDispatchCoversEveryFrozenSendableType()
        {
            using var harness = new RuntimeEntrypointHarness();
            await using var client = CreateClient(harness);
            await using var session = client.OpenSession(new NnrpClientSessionOptions(sessionId: 41));
            var tail = new byte[] { 1, 2 };

            await session.SendControlAsync(MessageType.Cancel, new ControlRequestMetadata(1, 1, 1, RuntimeRole.Client, 0, 2), tail);
            await session.SendControlAsync(MessageType.Abort, new ControlRequestMetadata(1, 2, 1, RuntimeRole.Client, 0, 2), tail);
            await session.SendControlAsync(MessageType.PriorityUpdate, new SchedulingMetadata(1, 3, 1, 1, 2, 0));
            await session.SendControlAsync(MessageType.Deadline, new SchedulingMetadata(1, 4, 1, 1, 2, 0));
            await session.SendControlAsync(MessageType.ExpireAt, new SchedulingMetadata(1, 5, 1, 1, 2, 0));
            await session.SendControlAsync(MessageType.Supersede, new SupersedeMetadata(1, 6, 2, NnrpResultDropReasonCode.Superseded, 0, 2), tail);
            await session.SendControlAsync(MessageType.BudgetUpdate, new BudgetMetadata(1, 7, 1, 2, 3, 0));
            await session.SendControlAsync(MessageType.CapabilityNegotiation, new CapabilityMetadata(1, 2, 3, 4, 5, 6, 2, 0), tail);
            await session.SendControlAsync(MessageType.DegradeProfile, new CapabilityMetadata(1, 2, 3, 4, 5, 6, 2, 0), tail);
            await session.SendControlAsync(MessageType.RouteHint, new RouteHintMetadata(1, 2, 3, 4, 5, 2, 0), tail);
            await session.SendControlAsync(MessageType.ExecutionHint, new RouteHintMetadata(1, 2, 3, 4, 5, 2, 0), tail);
            await session.SendControlAsync(MessageType.TraceContext, new TraceContextMetadata(1, 2, 3, 4, 0, 2), tail);

            Assert.Equal(12, harness.RuntimeFrames.Count);
        }

        private static NnrpClient CreateClient(RuntimeEntrypointHarness harness)
        {
            var options = new NnrpClientOptions(NnrpEndpoint.Parse("nnrp://localhost/runtime/default"));
            var connection = new NnrpNativeRuntimeConnection(
                harness.Entrypoints,
                new NnrpConnectionHandle(new NnrpHandle(NnrpHandleKind.Connection, 1, 1)));
            return new NnrpClient(options, connection, CreateSelection());
        }

        private static NnrpTransportSelection CreateSelection()
        {
            var metadata = new NnrpTransportProviderMetadata(
                "test.tcp",
                new NnrpTransportProviderCost(0, 0),
                0,
                new NnrpTransportProviderLimits(16 * 1024 * 1024),
                Array.Empty<NnrpTransportProviderLimitation>());
            var descriptor = new NnrpTransportProviderDescriptor(
                "test-tcp",
                "1",
                TransportId.Tcp,
                NnrpTransportProviderKind.PureRust,
                true,
                null,
                metadata);
            var candidate = new NnrpTransportCandidate(
                TransportId.Tcp,
                metadata,
                true,
                true,
                true,
                NnrpTransportProbeState.NotRun,
                selectionRank: 0);
            return new NnrpTransportSelection(descriptor, new[] { candidate }, TransportPolicy.Auto);
        }

        private static NnrpSubmitRequest CreateSubmit(ulong operationId, uint frameId)
        {
            return NnrpSubmitRequest.CreateToken(
                new NnrpTokenSubmitInput(
                    new NnrpSubmitIdentity(
                        operationId,
                        frameId,
                        new NnrpSubmitHeaderContext(HeaderFlags.AckRequired, 2, 3, 4)),
                    new NnrpSubmitPolicy(),
                    new[] { new NnrpTokenChunk(new byte[] { 1, 2, 3 }) }));
        }

        private static byte[] SuccessPayload()
        {
            return ResultPayload(ResultStatusCode.Success);
        }

        private static byte[] ResultPayload(ResultStatusCode statusCode)
        {
            return new ResultPushMetadata(
                statusCode,
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
                0).ToArray();
        }
    }
}
