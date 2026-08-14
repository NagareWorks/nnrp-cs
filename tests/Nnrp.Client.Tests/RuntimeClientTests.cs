using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
            var terminalEvent = NnrpTerminalEvent.FromRuntime(@event);
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new NnrpResult(0, NnrpResultTerminalState.Success, terminalEvent));
            Assert.Throws<ArgumentNullException>(() =>
                new NnrpResult(1, NnrpResultTerminalState.Success, null!));
            Assert.Throws<ArgumentException>(() =>
                new NnrpResult(1, NnrpResultTerminalState.Error, terminalEvent));
            Assert.Throws<ArgumentException>(() =>
                new NnrpResult(
                    2,
                    NnrpResultTerminalState.Cancelled,
                    NnrpTerminalEvent.FromLifecycle(
                        new NnrpOperationLifecycleEvent(1, NnrpOperationState.Cancelled))));

            using var harness = new RuntimeEntrypointHarness();
            var client = CreateClient(harness);
            await client.DisposeAsync();
            await client.DisposeAsync();
            Assert.True(client.IsClosed);
            await Assert.ThrowsAsync<ObjectDisposedException>(async () => await client.OpenSessionAsync());
        }

        [Fact]
        public async Task RecoveryUsesTheOpaqueTicketAndReturnsRuntimeIssuedTickets()
        {
            using var harness = new RuntimeEntrypointHarness();
            await using var client = CreateClient(harness);
            var encoded = TicketBytes(
                sessionId: 42,
                token: new byte[] { 7, 8, 9 },
                resumeFromOperationId: 101,
                resumeWindowMilliseconds: 120_000);
            var ticket = NnrpSessionRecoveryTicket.FromBytes(encoded);
            var options = new NnrpClientSessionOptions(
                requestedSessionId: 99,
                profileId: 3,
                schemaId: 0x2001,
                schemaVersion: 4,
                priorityClass: SessionPriorityClass.Interactive,
                defaultDeadlineMilliseconds: 250,
                maxInFlightOperations: 8,
                leaseTtlHintMilliseconds: 45_000,
                allowResume: true,
                resumeTokenBytes: 2,
                cacheHints: new[] { CacheObjectKind.TensorSectionTable, CacheObjectKind.PromptSegment });

            await using var session = await client.ResumeSessionAsync(ticket, options);

            var request = Assert.Single(harness.SessionResumeRequests);
            Assert.Equal((uint)42, request.Open.RequestedSessionId);
            Assert.Equal((uint)1, request.Open.Generation);
            Assert.NotEqual((ulong)0, request.Open.SessionHandleId);
            Assert.Equal((ushort)3, request.Open.ProfileId);
            Assert.Equal((byte)SessionPriorityClass.Interactive, request.Open.PriorityClass);
            Assert.Equal((byte)1, request.Open.AllowResume);
            Assert.Equal((uint)0x2001, request.Open.SchemaId);
            Assert.Equal((uint)4, request.Open.SchemaVersion);
            Assert.Equal((uint)250, request.Open.DefaultDeadlineMilliseconds);
            Assert.Equal((ushort)8, request.Open.MaxInFlightOperations);
            Assert.Equal((uint)45_000, request.Open.LeaseTtlHintMilliseconds);
            Assert.Equal((uint)3, request.Open.ResumeTokenBytes);
            Assert.Equal(new UIntPtr(2), request.Open.CacheHints.Length);
            Assert.Equal(encoded, Assert.Single(harness.SubmittedRecoveryTickets));
            Assert.NotSame(options, session.Options);
            Assert.Equal((uint)42, session.Options.RequestedSessionId);
            Assert.Equal((uint)1, session.Options.SessionGeneration);
            Assert.Equal((ushort)3, session.Options.ProfileId);
            Assert.Equal(SessionPriorityClass.Interactive, session.Options.PriorityClass);
            Assert.True(session.Options.AllowResume);
            Assert.Equal((uint)3, session.Options.ResumeTokenBytes);
            Assert.Equal(options.CacheHints, session.Options.CacheHints);

            Assert.Null(session.GetRecoveryTicket());
            harness.IssuedRecoveryTicket = encoded;
            var invalidStatus = harness.Entrypoints.ClientSessionRecoveryTicket(
                new NnrpHandle(NnrpHandleKind.Connection, 1, 1),
                out var invalidOwner,
                out var invalidTicket);
            Assert.Equal(NnrpFfiStatusCode.InvalidHandle, invalidStatus.StatusCode);
            Assert.Equal(NnrpHandle.Invalid, invalidOwner);
            Assert.Equal(NnrpBufferView.Empty, invalidTicket);
            var issued = session.GetRecoveryTicket();
            Assert.NotNull(issued);
            Assert.Equal((uint)42, issued!.SessionId);
            Assert.Equal(new byte[] { 7, 8, 9 }, issued.ResumeToken.ToArray());
            Assert.Single(harness.ReleasedBuffers);
            Assert.Equal((ulong)900, harness.ReleasedBuffers[0].Id);

            await Assert.ThrowsAsync<ArgumentNullException>(async () => await client.ResumeSessionAsync(null!));
        }

        [Fact]
        public async Task SessionBootstrapHonorsPreDispatchCancellation()
        {
            using var harness = new RuntimeEntrypointHarness();
            await using var client = CreateClient(harness);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(async () =>
                await client.OpenSessionAsync(cancellationToken: cancellation.Token));
            Assert.Empty(harness.SessionOpenRequests);

            var ticket = NnrpSessionRecoveryTicket.FromBytes(TicketBytes(
                sessionId: 42,
                token: new byte[] { 7, 8, 9 },
                resumeFromOperationId: 101,
                resumeWindowMilliseconds: 120_000));
            await Assert.ThrowsAsync<OperationCanceledException>(async () =>
                await client.ResumeSessionAsync(ticket, cancellationToken: cancellation.Token));
            Assert.Empty(harness.SessionResumeRequests);
        }

        [Fact]
        public async Task SessionBootstrapHonorsCancellationWhileWaitingForClientGate()
        {
            using var harness = new RuntimeEntrypointHarness();
            await using var client = CreateClient(harness);
            var gate = ClientGate(client);

            using (var cancellation = new CancellationTokenSource())
            using (var started = new ManualResetEventSlim())
            {
                Task<NnrpClientSession> pending;
                lock (gate)
                {
                    pending = Task.Run(async () =>
                    {
                        started.Set();
                        return await client.OpenSessionAsync(cancellationToken: cancellation.Token);
                    });
                    Assert.True(started.Wait(TimeSpan.FromSeconds(5)));
                    cancellation.Cancel();
                }

                await Assert.ThrowsAsync<OperationCanceledException>(() => pending);
                Assert.Empty(harness.SessionOpenRequests);
            }

            var ticket = NnrpSessionRecoveryTicket.FromBytes(TicketBytes(
                sessionId: 42,
                token: new byte[] { 7, 8, 9 },
                resumeFromOperationId: 101,
                resumeWindowMilliseconds: 120_000));
            using (var cancellation = new CancellationTokenSource())
            using (var started = new ManualResetEventSlim())
            {
                Task<NnrpClientSession> pending;
                lock (gate)
                {
                    pending = Task.Run(async () =>
                    {
                        started.Set();
                        return await client.ResumeSessionAsync(ticket, cancellationToken: cancellation.Token);
                    });
                    Assert.True(started.Wait(TimeSpan.FromSeconds(5)));
                    cancellation.Cancel();
                }

                await Assert.ThrowsAsync<OperationCanceledException>(() => pending);
                Assert.Empty(harness.SessionResumeRequests);
            }
        }

        [Fact]
        public async Task SubmitRoutesDeferredResultsAndPreservesWireOrder()
        {
            using var harness = new RuntimeEntrypointHarness();
            await using var client = CreateClient(harness);
            await using var session = await client.OpenSessionAsync(new NnrpClientSessionOptions(requestedSessionId: 41));
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
            var deferredEvent = RuntimeEventOf(await session.NextEventAsync());

            Assert.Equal((ulong)201, result.OperationId);
            Assert.Equal(NnrpResultTerminalState.Success, result.TerminalState);
            var resultEvent = RuntimeEventOf(result);
            Assert.Equal(MessageType.ResultPush, resultEvent.Header.MessageType);
            Assert.Equal(NnrpRuntimeEventMetadataKind.ResultPush, resultEvent.Metadata.Kind);
            Assert.Empty(BodyOf(resultEvent).ToArray());
            Assert.Equal((uint)41, session.Options.RequestedSessionId);
            Assert.False(session.IsClosed);
            Assert.Equal(TransportId.Tcp, client.ActiveTransportId);
            Assert.Equal(
                NnrpEndpoint.Parse("nnrp://localhost/runtime/default"),
                client.Options.Endpoint);
            Assert.Equal("test-tcp", client.Selection.SelectedProvider.Name);
            Assert.Equal((ulong)202, deferredResult.OperationId);
            Assert.Equal(
                NnrpRuntimeEventMetadataKind.ResultPush,
                RuntimeEventOf(deferredResult).Metadata.Kind);
            Assert.Equal(MessageType.Progress, deferredEvent.Header.MessageType);
            Assert.Equal(progress, deferredEvent.Metadata.Get<ProgressMetadata>());
            Assert.Single(harness.SubmitRequests);
            Assert.Equal((ulong)201, harness.SubmitRequests[0].OperationId);
            Assert.Equal((uint)11, harness.SubmitRequests[0].FrameId);
        }

        [Fact]
        public async Task SubmitCancellationAfterDispatchSendsCancelAndPreservesLifecycle()
        {
            using var harness = new RuntimeEntrypointHarness();
            await using var client = CreateClient(harness);
            await using var session = await client.OpenSessionAsync(new NnrpClientSessionOptions(requestedSessionId: 41));
            await session.UpdatePriorityAsync(new SchedulingMetadata(201, 7, 1, 0, 0, 0));
            var sequenceError = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
                await session.UpdateDeadlineAsync(new SchedulingMetadata(201, 7, 0, 0, 1, 0)));
            Assert.Equal("metadata", sequenceError.ParamName);
            Assert.Single(harness.RuntimeFrames);
            using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
                await session.SubmitAsync(CreateSubmit(201, 11), cancellation.Token));

            Assert.Single(harness.SubmitRequests);
            Assert.Equal(2, harness.RuntimeFrames.Count);
            var cancel = Assert.Single(
                harness.RuntimeFrames,
                item => item.Request.MessageType == (uint)MessageType.Cancel);
            var decoded = NnrpRuntimeControl.Decode(MessageType.Cancel, cancel.Payload);
            Assert.Equal(
                new ControlRequestMetadata(201, 8, 0, RuntimeRole.Client, 0, 0),
                decoded.Metadata);

            harness.QueueClientOperationLifecycleEvent(NnrpOperationState.Cancelled, 201);
            var lifecycle = await session.NextEventAsync();
            Assert.Equal(NnrpClientEventKind.Lifecycle, lifecycle.Kind);
            var projected = lifecycle.Match(
                _ => throw new InvalidOperationException("Expected a lifecycle event."),
                value => value);
            Assert.Equal((ulong)201, projected.OperationId);
            Assert.Equal(NnrpOperationState.Cancelled, projected.State);
        }

        [Fact]
        public async Task ImplicitCancelSequencePreventsTheNextExplicitControlFromRegressing()
        {
            using var harness = new RuntimeEntrypointHarness();
            await using var client = CreateClient(harness);
            await using var session = await client.OpenSessionAsync(new NnrpClientSessionOptions(requestedSessionId: 41));
            using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
                await session.SubmitAsync(CreateSubmit(201, 11), cancellation.Token));

            var sequenceError = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
                await session.UpdatePriorityAsync(new SchedulingMetadata(201, 1, 0, 0, 0, 0)));
            Assert.Equal("metadata", sequenceError.ParamName);
            Assert.Single(harness.RuntimeFrames);
        }

        [Fact]
        public async Task SubmitCancellationBeforeDispatchEmitsNoFrames()
        {
            using var harness = new RuntimeEntrypointHarness();
            await using var client = CreateClient(harness);
            await using var session = await client.OpenSessionAsync(new NnrpClientSessionOptions(requestedSessionId: 41));
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
                await session.SubmitAsync(CreateSubmit(201, 11), cancellation.Token));

            Assert.Empty(harness.SubmitRequests);
            Assert.Empty(harness.RuntimeFrames);
        }

        [Fact]
        public async Task CancelSuppressesLatePayloadsButKeepsDropEvidence()
        {
            using var harness = new RuntimeEntrypointHarness();
            await using var client = CreateClient(harness);
            await using var session = await client.OpenSessionAsync(new NnrpClientSessionOptions(requestedSessionId: 41));
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
            var trace = RuntimeEventOf(await session.NextEventAsync());

            Assert.Equal(NnrpResultTerminalState.Dropped, result.TerminalState);
            var dropEvent = RuntimeEventOf(result);
            Assert.Equal(drop, dropEvent.Metadata.Get<ResultDropReasonMetadata>());
            Assert.Equal(new byte[] { 3, 4 }, DiagnosticOf(dropEvent).ToArray());
            Assert.Equal(MessageType.TraceContext, trace.Header.MessageType);
            Assert.Contains(harness.RuntimeFrames, frame => frame.Request.MessageType == (uint)MessageType.Cancel);
        }

        [Fact]
        public async Task NextEventPreservesHeaderlessOperationLifecycleEvents()
        {
            using var harness = new RuntimeEntrypointHarness();
            await using var client = CreateClient(harness);
            await using var session = await client.OpenSessionAsync(new NnrpClientSessionOptions(requestedSessionId: 41));
            harness.QueueClientEvent(
                MessageType.Progress,
                31,
                301,
                NnrpRuntimeControl.Encode(
                    MessageType.Progress,
                    new ProgressMetadata(301, 1, 2, 3000, 0, 0)));
            harness.QueueClientOperationLifecycleEvent(NnrpOperationState.Running, 301);

            var runtime = await session.NextEventAsync();
            var lifecycle = await session.NextEventAsync();

            Assert.Equal(NnrpClientEventKind.Runtime, runtime.Kind);
            Assert.Equal(MessageType.Progress, RuntimeEventOf(runtime).Header.MessageType);
            Assert.Equal(NnrpClientEventKind.Lifecycle, lifecycle.Kind);
            var projected = lifecycle.Match(
                _ => throw new InvalidOperationException("Expected a lifecycle event."),
                value => value);
            Assert.Equal((ulong)301, projected.OperationId);
            Assert.Equal(NnrpOperationState.Running, projected.State);
        }

        [Theory]
        [InlineData(ResultStatusCode.Success)]
        [InlineData(ResultStatusCode.Degraded)]
        [InlineData(ResultStatusCode.Rejected)]
        [InlineData(ResultStatusCode.Failed)]
        public async Task ResultPushStatusDoesNotSelectProtocolTerminalState(ResultStatusCode statusCode)
        {
            using var harness = new RuntimeEntrypointHarness();
            await using var client = CreateClient(harness);
            await using var session = await client.OpenSessionAsync(new NnrpClientSessionOptions(requestedSessionId: 41));
            harness.QueueClientEvent(MessageType.ResultPush, 11, 201, ResultPayload(statusCode));

            var result = await session.NextResultAsync();

            Assert.Equal(NnrpResultTerminalState.Success, result.TerminalState);
        }

        [Fact]
        public async Task ResultDropWithoutMetadataMapsToDropped()
        {
            using var harness = new RuntimeEntrypointHarness();
            await using var client = CreateClient(harness);
            await using var session = await client.OpenSessionAsync(new NnrpClientSessionOptions(requestedSessionId: 41));
            harness.QueueClientEvent(MessageType.ResultDrop, 11, 201, Array.Empty<byte>());

            var result = await session.NextResultAsync();

            Assert.Equal((ulong)201, result.OperationId);
            Assert.Equal(NnrpResultTerminalState.Dropped, result.TerminalState);
            Assert.Equal(MessageType.ResultDrop, RuntimeEventOf(result).Header.MessageType);
        }

        [Theory]
        [InlineData(6u, NnrpResultTerminalState.Success, NnrpOperationState.Completed)]
        [InlineData(7u, NnrpResultTerminalState.Cancelled, NnrpOperationState.Cancelled)]
        [InlineData(10u, NnrpResultTerminalState.Error, NnrpOperationState.Failed)]
        public async Task HeaderlessNativeLifecycleMapsWithoutFabricatingWireHeader(
            uint eventKind,
            NnrpResultTerminalState terminalState,
            NnrpOperationState operationState)
        {
            using var harness = new RuntimeEntrypointHarness();
            await using var client = CreateClient(harness);
            await using var session = await client.OpenSessionAsync(new NnrpClientSessionOptions(requestedSessionId: 41));
            harness.QueueClientLifecycleEvent(eventKind, 201);

            var result = await session.NextResultAsync();

            Assert.Equal((ulong)201, result.OperationId);
            Assert.Equal(terminalState, result.TerminalState);
            var lifecycle = LifecycleEventOf(result);
            Assert.Equal((ulong)201, lifecycle.OperationId);
            Assert.Equal(operationState, lifecycle.State);
        }

        [Theory]
        [InlineData(6u, NnrpResultTerminalState.Success)]
        [InlineData(7u, NnrpResultTerminalState.Cancelled)]
        [InlineData(10u, NnrpResultTerminalState.Error)]
        public async Task NextEventDefersHeaderlessTerminalEvidenceForNextResult(
            uint eventKind,
            NnrpResultTerminalState terminalState)
        {
            using var harness = new RuntimeEntrypointHarness();
            await using var client = CreateClient(harness);
            await using var session = await client.OpenSessionAsync(new NnrpClientSessionOptions(requestedSessionId: 41));
            harness.QueueClientLifecycleEvent(eventKind, 201);
            harness.QueueClientEvent(
                MessageType.Progress,
                31,
                202,
                NnrpRuntimeControl.Encode(
                    MessageType.Progress,
                    new ProgressMetadata(202, 1, 2, 3000, 0, 0)));

            var runtime = await session.NextEventAsync();
            var result = await session.NextResultAsync();

            Assert.Equal(NnrpClientEventKind.Runtime, runtime.Kind);
            Assert.Equal(MessageType.Progress, RuntimeEventOf(runtime).Header.MessageType);
            Assert.Equal((ulong)201, result.OperationId);
            Assert.Equal(terminalState, result.TerminalState);
        }

        [Fact]
        public async Task DisposeReleasesDeferredTerminalEvidence()
        {
            using var harness = new RuntimeEntrypointHarness();
            await using var client = CreateClient(harness);
            var session = await client.OpenSessionAsync(new NnrpClientSessionOptions(requestedSessionId: 41));
            harness.QueueClientLifecycleEvent(6, 201);
            harness.QueueClientEvent(
                MessageType.Progress,
                31,
                202,
                NnrpRuntimeControl.Encode(
                    MessageType.Progress,
                    new ProgressMetadata(202, 1, 2, 3000, 0, 0)));

            await session.NextEventAsync();
            Assert.Equal(1, DeferredTerminalEventCount(session));

            await session.DisposeAsync();

            Assert.Equal(0, DeferredTerminalEventCount(session));
        }

        [Fact]
        public async Task FailedNativeLifecycleStatusMapsToFailedRegardlessOfEventKind()
        {
            using var harness = new RuntimeEntrypointHarness();
            await using var client = CreateClient(harness);
            await using var session = await client.OpenSessionAsync(new NnrpClientSessionOptions(requestedSessionId: 41));
            harness.QueueClientLifecycleEvent(
                6,
                201,
                new NnrpFfiStatus(NnrpFfiStatusCode.ProtocolError, NnrpErrorFamily.Operation));

            var result = await session.NextResultAsync();

            Assert.Equal(NnrpResultTerminalState.Error, result.TerminalState);
            Assert.Equal(NnrpOperationState.Failed, LifecycleEventOf(result).State);
        }

        [Fact]
        public void TerminalEventRejectsNonTerminalEvidenceAndRequiresBothHandlers()
        {
            var progress = NnrpRuntimeEvent.Decode(
                new RuntimeFrameHeader(MessageType.Progress),
                NnrpRuntimeControl.Encode(
                    MessageType.Progress,
                    new ProgressMetadata(1, 2, 3, 4, 0, 0)));
            Assert.Throws<ArgumentException>(() => NnrpTerminalEvent.FromRuntime(progress));
            Assert.Throws<ArgumentException>(() =>
                NnrpTerminalEvent.FromLifecycle(
                    new NnrpOperationLifecycleEvent(1, NnrpOperationState.Running)));

            var terminal = NnrpTerminalEvent.FromLifecycle(
                new NnrpOperationLifecycleEvent(1, NnrpOperationState.Completed));
            Func<NnrpRuntimeEvent, int> runtime = _ => 1;
            Func<NnrpOperationLifecycleEvent, int> lifecycle = _ => 2;
            Assert.Throws<ArgumentNullException>(() => terminal.Match(null!, lifecycle));
            Assert.Throws<ArgumentNullException>(() => terminal.Match(runtime, null!));
            Assert.Equal(2, terminal.Match(runtime, lifecycle));
        }

        [Theory]
        [InlineData(NnrpOperationState.Completed, NnrpResultTerminalState.Success)]
        [InlineData(NnrpOperationState.Cancelled, NnrpResultTerminalState.Cancelled)]
        [InlineData(NnrpOperationState.Superseded, NnrpResultTerminalState.Dropped)]
        [InlineData(NnrpOperationState.Failed, NnrpResultTerminalState.Error)]
        public void LifecycleTerminalEvidenceUsesTheFrozenMapping(
            NnrpOperationState operationState,
            NnrpResultTerminalState terminalState)
        {
            var terminal = NnrpTerminalEvent.FromLifecycle(
                new NnrpOperationLifecycleEvent(1, operationState));

            var result = new NnrpResult(1, terminalState, terminal);

            Assert.Equal(terminalState, result.TerminalState);
            Assert.Equal(operationState, LifecycleEventOf(result).State);
        }

        [Fact]
        public void ResultDropReasonEvidenceRequiresMatchingOperationIdentity()
        {
            var metadata = new ResultDropReasonMetadata(
                101,
                1,
                NnrpResultDropReasonCode.Backpressure,
                RuntimeRole.Server,
                0,
                0);
            var terminal = NnrpTerminalEvent.FromRuntime(
                NnrpRuntimeEvent.Decode(
                    new RuntimeFrameHeader(MessageType.ResultDropReason),
                    NnrpRuntimeControl.Encode(MessageType.ResultDropReason, metadata)));

            Assert.Throws<ArgumentException>(() =>
                new NnrpResult(102, NnrpResultTerminalState.Dropped, terminal));
        }

        [Fact]
        public async Task TypedClientMethodsUseOneNativeFrameEach()
        {
            using var harness = new RuntimeEntrypointHarness();
            await using var client = CreateClient(harness);
            await using var session = await client.OpenSessionAsync(new NnrpClientSessionOptions(requestedSessionId: 41));
            var diagnostic = new byte[] { 1, 2 };
            var body = new byte[] { 3, 4, 5 };

            await session.AbortAsync(new ControlRequestMetadata(1, 1, 1, RuntimeRole.Client, 0, 2), diagnostic);
            await session.UpdatePriorityAsync(new SchedulingMetadata(1, 2, 3, -1, 4, 0));
            await session.UpdateDeadlineAsync(new SchedulingMetadata(1, 3, 3, 0, 4, 0));
            await session.ExpireAtAsync(new SchedulingMetadata(1, 4, 3, 1, 4, 0));
            await session.SupersedeAsync(new SupersedeMetadata(1, 5, 5, NnrpResultDropReasonCode.Superseded, 0, 2), diagnostic);
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
            await using var session = await client.OpenSessionAsync(new NnrpClientSessionOptions(requestedSessionId: 41));
            var tail = new byte[] { 1, 2 };

            await session.SendControlAsync(MessageType.Cancel, new ControlRequestMetadata(1, 1, 1, RuntimeRole.Client, 0, 2), tail);
            await session.SendControlAsync(MessageType.Abort, new ControlRequestMetadata(1, 2, 1, RuntimeRole.Client, 0, 2), tail);
            await session.SendControlAsync(MessageType.PriorityUpdate, new SchedulingMetadata(1, 3, 1, 1, 2, 0));
            await session.SendControlAsync(MessageType.Deadline, new SchedulingMetadata(1, 4, 1, 1, 2, 0));
            await session.SendControlAsync(MessageType.ExpireAt, new SchedulingMetadata(1, 5, 1, 1, 2, 0));
            await session.SendControlAsync(MessageType.Supersede, new SupersedeMetadata(1, 6, 6, NnrpResultDropReasonCode.Superseded, 0, 2), tail);
            await session.SendControlAsync(MessageType.BudgetUpdate, new BudgetMetadata(1, 7, 1, 2, 3, 0));
            await session.SendControlAsync(MessageType.CapabilityNegotiation, new CapabilityMetadata(1, 2, 3, 4, 5, 6, 2, 0), tail);
            await session.SendControlAsync(MessageType.DegradeProfile, new CapabilityMetadata(1, 2, 3, 4, 5, 6, 2, 0), tail);
            await session.SendControlAsync(MessageType.RouteHint, new RouteHintMetadata(1, 2, 3, 4, 5, 2, 0), tail);
            await session.SendControlAsync(MessageType.ExecutionHint, new RouteHintMetadata(1, 2, 3, 4, 5, 2, 0), tail);
            await session.SendControlAsync(MessageType.TraceContext, new TraceContextMetadata(1, 2, 3, 4, 0, 2), tail);

            Assert.Equal(12, harness.RuntimeFrames.Count);
        }

        private static byte[] TicketBytes(
            uint sessionId,
            byte[] token,
            ulong? resumeFromOperationId,
            uint resumeWindowMilliseconds)
        {
            var encoded = new byte[checked(28 + token.Length)];
            encoded[0] = (byte)'N';
            encoded[1] = (byte)'R';
            encoded[2] = (byte)'T';
            encoded[3] = (byte)'K';
            BinaryPrimitives.WriteUInt16LittleEndian(encoded.AsSpan(4), 1);
            BinaryPrimitives.WriteUInt16LittleEndian(encoded.AsSpan(6), resumeFromOperationId.HasValue ? (ushort)1 : (ushort)0);
            BinaryPrimitives.WriteUInt32LittleEndian(encoded.AsSpan(8), sessionId);
            BinaryPrimitives.WriteUInt32LittleEndian(encoded.AsSpan(12), checked((uint)token.Length));
            BinaryPrimitives.WriteUInt32LittleEndian(encoded.AsSpan(16), resumeWindowMilliseconds);
            BinaryPrimitives.WriteUInt64LittleEndian(encoded.AsSpan(20), resumeFromOperationId.GetValueOrDefault());
            token.CopyTo(encoded, 28);
            return encoded;
        }

        private static NnrpClient CreateClient(RuntimeEntrypointHarness harness)
        {
            var options = new NnrpClientOptions(NnrpEndpoint.Parse("nnrp://localhost/runtime/default"));
            var connection = new NnrpNativeRuntimeConnection(
                harness.Entrypoints,
                new NnrpConnectionHandle(new NnrpHandle(NnrpHandleKind.Connection, 1, 1)));
            return new NnrpClient(options, connection, CreateSelection());
        }

        private static object ClientGate(NnrpClient client)
        {
            var field = typeof(NnrpClient).GetField("gate", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            return Assert.IsType<object>(field!.GetValue(client));
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

        private static NnrpRuntimeEvent RuntimeEventOf(NnrpResult result)
        {
            return result.Event.Match(
                runtime => runtime,
                _ => throw new InvalidOperationException("Expected runtime terminal evidence."));
        }

        private static NnrpRuntimeEvent RuntimeEventOf(NnrpClientEvent @event)
        {
            return @event.Match(
                runtime => runtime,
                _ => throw new InvalidOperationException("Expected a client runtime event."));
        }

        private static NnrpOperationLifecycleEvent LifecycleEventOf(NnrpResult result)
        {
            return result.Event.Match(
                _ => throw new InvalidOperationException("Expected lifecycle terminal evidence."),
                lifecycle => lifecycle);
        }

        private static int DeferredTerminalEventCount(NnrpClientSession session)
        {
            var field = typeof(NnrpClientSession).GetField(
                "deferredTerminalEvents",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            var queue = field!.GetValue(session);
            Assert.NotNull(queue);
            var count = queue!.GetType().GetProperty("Count");
            Assert.NotNull(count);
            return Assert.IsType<int>(count!.GetValue(queue));
        }

        private static ReadOnlyMemory<byte> BodyOf(NnrpRuntimeEvent @event)
        {
            return @event.Tail.Match(
                () => throw new InvalidOperationException("Expected a body tail."),
                body => body,
                _ => throw new InvalidOperationException("Expected a body tail."),
                (_, _) => throw new InvalidOperationException("Expected a body tail."));
        }

        private static ReadOnlyMemory<byte> DiagnosticOf(NnrpRuntimeEvent @event)
        {
            return @event.Tail.Match(
                () => throw new InvalidOperationException("Expected a diagnostic tail."),
                _ => throw new InvalidOperationException("Expected a diagnostic tail."),
                diagnostic => diagnostic,
                (_, _) => throw new InvalidOperationException("Expected a diagnostic tail."));
        }
    }
}
