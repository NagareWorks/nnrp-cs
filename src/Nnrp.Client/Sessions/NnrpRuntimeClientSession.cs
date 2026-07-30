using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nnrp.Core;
using Nnrp.NativeBridge;
using Nnrp.Runtime;

namespace Nnrp.Client
{
    public sealed class NnrpResult
    {
        internal NnrpResult(
            ulong operationId,
            NnrpResultTerminalState terminalState,
            NnrpRuntimeEvent @event)
        {
            if (operationId == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(operationId));
            }

            OperationId = operationId;
            TerminalState = terminalState;
            Event = @event ?? throw new ArgumentNullException(nameof(@event));
        }

        public ulong OperationId { get; }

        public NnrpResultTerminalState TerminalState { get; }

        public NnrpRuntimeEvent Event { get; }

        public RuntimeFrameHeader Header => Event.Header;

        public ResultPushMetadata? ResultMetadata =>
            Event.Metadata.Kind == NnrpRuntimeEventMetadataKind.ResultPush
                ? Event.Metadata.Get<ResultPushMetadata>()
                : null;

        public ResultDropReasonMetadata? DropMetadata =>
            Event.Metadata.Kind == NnrpRuntimeEventMetadataKind.ResultDropReason
                ? Event.Metadata.Get<ResultDropReasonMetadata>()
                : null;

        public ReadOnlyMemory<byte> Body => Event.Tail.Body;

        public ReadOnlyMemory<byte> Diagnostic => Event.Tail.Diagnostic;
    }

    public sealed class NnrpClientSession : IAsyncDisposable
    {
        private const int MaxCancelledOperationSuppressions = 4096;

        private readonly object stateGate = new object();
        private readonly SemaphoreSlim consumeGate = new SemaphoreSlim(1, 1);
        private readonly Queue<NnrpNativeRuntimeEvent> deferredEvents = new Queue<NnrpNativeRuntimeEvent>();
        private readonly Queue<ulong> cancelledOperationOrder = new Queue<ulong>();
        private readonly HashSet<ulong> cancelledOperations = new HashSet<ulong>();
        private readonly NnrpClient client;
        private readonly NnrpNativeRuntimeSession session;

        internal NnrpClientSession(
            NnrpClient client,
            NnrpNativeRuntimeSession session,
            NnrpClientSessionOptions options)
        {
            this.client = client ?? throw new ArgumentNullException(nameof(client));
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            Options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public NnrpClientSessionOptions Options { get; }

        public bool IsClosed { get; private set; }

        internal ulong NativeHandleId => session.Handle.Handle.Id;

        public async ValueTask<NnrpResult> SubmitAsync(
            NnrpSubmitRequest request,
            CancellationToken cancellationToken = default)
        {
            var operationId = await SubmitNoWaitAsync(request, cancellationToken).ConfigureAwait(false);
            await consumeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                while (true)
                {
                    if (TryTakeMatchingTerminal(operationId, out var deferred))
                    {
                        return ToResult(deferred!);
                    }

                    var nativeEvent = await client.NextNativeEventAsync(session, cancellationToken).ConfigureAwait(false);
                    if (ShouldSuppress(nativeEvent))
                    {
                        continue;
                    }

                    if (IsTerminalResult(nativeEvent)
                        && MatchesOperation(nativeEvent, operationId))
                    {
                        return ToResult(nativeEvent);
                    }

                    deferredEvents.Enqueue(nativeEvent);
                }
            }
            finally
            {
                consumeGate.Release();
            }
        }

        public ValueTask<ulong> SubmitNoWaitAsync(
            NnrpSubmitRequest request,
            CancellationToken cancellationToken = default)
        {
            EnsureOpen();
            cancellationToken.ThrowIfCancellationRequested();
            var header = new RuntimeFrameHeader(
                MessageType.FrameSubmit,
                request.Header.Flags,
                0,
                request.FrameId,
                request.Header.ViewId,
                request.Header.RouteId,
                request.Header.TraceId);
            var operation = session.SubmitOperation(request.OperationId, header, request.EncodePayload());
            return new ValueTask<ulong>(operation.OperationId);
        }

        public async ValueTask<NnrpResult> NextResultAsync(CancellationToken cancellationToken = default)
        {
            EnsureOpen();
            await consumeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                while (true)
                {
                    if (TryTakeNextTerminal(out var deferred))
                    {
                        return ToResult(deferred!);
                    }

                    var nativeEvent = await client.NextNativeEventAsync(session, cancellationToken).ConfigureAwait(false);
                    if (ShouldSuppress(nativeEvent))
                    {
                        continue;
                    }

                    if (IsTerminalResult(nativeEvent))
                    {
                        return ToResult(nativeEvent);
                    }

                    deferredEvents.Enqueue(nativeEvent);
                }
            }
            finally
            {
                consumeGate.Release();
            }
        }

        public async ValueTask<NnrpRuntimeEvent> NextEventAsync(CancellationToken cancellationToken = default)
        {
            EnsureOpen();
            await consumeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                while (true)
                {
                    var nativeEvent = deferredEvents.Count != 0
                        ? deferredEvents.Dequeue()
                        : await client.NextNativeEventAsync(session, cancellationToken).ConfigureAwait(false);
                    if (ShouldSuppress(nativeEvent))
                    {
                        continue;
                    }

                    if (nativeEvent.HasWireHeader)
                    {
                        return nativeEvent.ToRuntimeEvent();
                    }

                    nativeEvent.Diagnostic.Status.ThrowIfError();
                }
            }
            finally
            {
                consumeGate.Release();
            }
        }

        public ValueTask CancelAsync(ControlRequestMetadata metadata, ReadOnlyMemory<byte> diagnostic = default, CancellationToken cancellationToken = default) =>
            SendCancellation(metadata.OperationId, cancellationToken, () => session.CancelOperation(metadata, diagnostic));

        public ValueTask AbortAsync(ControlRequestMetadata metadata, ReadOnlyMemory<byte> diagnostic = default, CancellationToken cancellationToken = default) =>
            SendCancellation(metadata.OperationId, cancellationToken, () => session.AbortOperation(metadata, diagnostic));

        public ValueTask UpdatePriorityAsync(SchedulingMetadata metadata, CancellationToken cancellationToken = default) =>
            Send(cancellationToken, () => session.UpdatePriority(metadata));

        public ValueTask UpdateDeadlineAsync(SchedulingMetadata metadata, CancellationToken cancellationToken = default) =>
            Send(cancellationToken, () => session.UpdateDeadline(metadata));

        public ValueTask ExpireAtAsync(SchedulingMetadata metadata, CancellationToken cancellationToken = default) =>
            Send(cancellationToken, () => session.ExpireAt(metadata));

        public ValueTask SupersedeAsync(SupersedeMetadata metadata, ReadOnlyMemory<byte> diagnostic = default, CancellationToken cancellationToken = default) =>
            Send(cancellationToken, () => session.Supersede(metadata, diagnostic));

        public ValueTask UpdateBudgetAsync(BudgetMetadata metadata, CancellationToken cancellationToken = default) =>
            Send(cancellationToken, () => session.UpdateBudget(metadata));

        public ValueTask NegotiateCapabilitiesAsync(CapabilityMetadata metadata, ReadOnlyMemory<byte> body = default, CancellationToken cancellationToken = default) =>
            Send(cancellationToken, () => session.NegotiateCapabilities(metadata, body));

        public ValueTask DegradeProfileAsync(CapabilityMetadata metadata, ReadOnlyMemory<byte> body = default, CancellationToken cancellationToken = default) =>
            Send(cancellationToken, () => session.DegradeProfile(metadata, body));

        public ValueTask SendRouteHintAsync(RouteHintMetadata metadata, ReadOnlyMemory<byte> body = default, CancellationToken cancellationToken = default) =>
            Send(cancellationToken, () => session.SendRouteHint(metadata, body));

        public ValueTask SendExecutionHintAsync(RouteHintMetadata metadata, ReadOnlyMemory<byte> body = default, CancellationToken cancellationToken = default) =>
            Send(cancellationToken, () => session.SendExecutionHint(metadata, body));

        public ValueTask SendTraceContextAsync(TraceContextMetadata metadata, ReadOnlyMemory<byte> body = default, CancellationToken cancellationToken = default) =>
            Send(cancellationToken, () => session.SendTraceContext(metadata, body));

        public ValueTask SendControlAsync(
            MessageType messageType,
            IRuntimeControlMetadata metadata,
            ReadOnlyMemory<byte> tail = default,
            CancellationToken cancellationToken = default)
        {
            return messageType switch
            {
                MessageType.Cancel when metadata is ControlRequestMetadata value => CancelAsync(value, tail, cancellationToken),
                MessageType.Abort when metadata is ControlRequestMetadata value => AbortAsync(value, tail, cancellationToken),
                MessageType.PriorityUpdate when metadata is SchedulingMetadata value => UpdatePriorityAsync(value, cancellationToken),
                MessageType.Deadline when metadata is SchedulingMetadata value => UpdateDeadlineAsync(value, cancellationToken),
                MessageType.ExpireAt when metadata is SchedulingMetadata value => ExpireAtAsync(value, cancellationToken),
                MessageType.Supersede when metadata is SupersedeMetadata value => SupersedeAsync(value, tail, cancellationToken),
                MessageType.BudgetUpdate when metadata is BudgetMetadata value => UpdateBudgetAsync(value, cancellationToken),
                MessageType.CapabilityNegotiation when metadata is CapabilityMetadata value => NegotiateCapabilitiesAsync(value, tail, cancellationToken),
                MessageType.DegradeProfile when metadata is CapabilityMetadata value => DegradeProfileAsync(value, tail, cancellationToken),
                MessageType.RouteHint when metadata is RouteHintMetadata value => SendRouteHintAsync(value, tail, cancellationToken),
                MessageType.ExecutionHint when metadata is RouteHintMetadata value => SendExecutionHintAsync(value, tail, cancellationToken),
                MessageType.TraceContext when metadata is TraceContextMetadata value => SendTraceContextAsync(value, tail, cancellationToken),
                _ => throw new ArgumentException("Message type and runtime-control metadata do not select a client-sendable frame."),
            };
        }

        public ValueTask DeclareObjectAsync(ObjectDescriptorMetadata metadata, ReadOnlyMemory<byte> body = default, CancellationToken cancellationToken = default) =>
            SendObject(MessageType.ObjectDeclare, metadata, body, cancellationToken);

        public ValueTask ReferenceObjectAsync(ObjectReferenceMetadata metadata, ReadOnlyMemory<byte> body = default, CancellationToken cancellationToken = default) =>
            SendObject(MessageType.ObjectRef, metadata, body, cancellationToken);

        public ValueTask ReleaseObjectAsync(ObjectReleaseMetadata metadata, ReadOnlyMemory<byte> diagnostic = default, CancellationToken cancellationToken = default) =>
            SendObject(MessageType.ObjectRelease, metadata, diagnostic, cancellationToken);

        public ValueTask PatchObjectAsync(ObjectDeltaMetadata metadata, ReadOnlyMemory<byte> metadataBody, ReadOnlyMemory<byte> delta, CancellationToken cancellationToken = default) =>
            SendObject(MessageType.ObjectPatch, metadata, JoinDelta(metadata, metadataBody, delta), cancellationToken);

        public ValueTask SendObjectDeltaAsync(ObjectDeltaMetadata metadata, ReadOnlyMemory<byte> metadataBody, ReadOnlyMemory<byte> delta, CancellationToken cancellationToken = default) =>
            SendObject(MessageType.ObjectDelta, metadata, JoinDelta(metadata, metadataBody, delta), cancellationToken);

        public ValueTask ReferenceCacheAsync(CacheReferenceMetadata metadata, ReadOnlyMemory<byte> body = default, CancellationToken cancellationToken = default) =>
            SendObject(MessageType.CacheReference, metadata, body, cancellationToken);

        public ValueTask ReportCacheMissAsync(CacheMissMetadata metadata, ReadOnlyMemory<byte> diagnostic = default, CancellationToken cancellationToken = default) =>
            SendObject(MessageType.CacheMiss, metadata, diagnostic, cancellationToken);

        public ValueTask InvalidateCacheAsync(CacheInvalidateMetadata metadata, CancellationToken cancellationToken = default) =>
            Send(cancellationToken, () => session.SendCacheInvalidate(metadata));

        public async ValueTask DisposeAsync()
        {
            if (IsClosed)
            {
                return;
            }

            IsClosed = true;
            deferredEvents.Clear();
            lock (stateGate)
            {
                cancelledOperations.Clear();
                cancelledOperationOrder.Clear();
            }
            try
            {
                if (!session.IsClosed)
                {
                    await Task.Factory.StartNew(
                        session.Close,
                        CancellationToken.None,
                        TaskCreationOptions.LongRunning,
                        TaskScheduler.Default).ConfigureAwait(false);
                }
            }
            finally
            {
                client.RemoveSession(this);
            }
        }

        private ValueTask SendObject(
            MessageType messageType,
            IRuntimeObjectMetadata metadata,
            ReadOnlyMemory<byte> tail,
            CancellationToken cancellationToken)
        {
            return Send(cancellationToken, () => session.SendRuntimeObject(messageType, metadata, tail));
        }

        private ValueTask Send(CancellationToken cancellationToken, Action action)
        {
            EnsureOpen();
            cancellationToken.ThrowIfCancellationRequested();
            action();
            return default;
        }

        private ValueTask SendCancellation(
            ulong operationId,
            CancellationToken cancellationToken,
            Action action)
        {
            EnsureOpen();
            cancellationToken.ThrowIfCancellationRequested();
            action();
            RememberCancelledOperation(operationId);
            return default;
        }

        private static ReadOnlyMemory<byte> Join(ReadOnlyMemory<byte> first, ReadOnlyMemory<byte> second)
        {
            var joined = new byte[checked(first.Length + second.Length)];
            first.Span.CopyTo(joined);
            second.Span.CopyTo(joined.AsSpan(first.Length));
            return joined;
        }

        private static ReadOnlyMemory<byte> JoinDelta(
            ObjectDeltaMetadata metadata,
            ReadOnlyMemory<byte> metadataBody,
            ReadOnlyMemory<byte> delta)
        {
            if (metadata.MetadataBytes != checked((uint)metadataBody.Length))
            {
                throw new ArgumentException(
                    "Object delta metadata length does not match MetadataBytes.",
                    nameof(metadataBody));
            }

            if (metadata.DeltaBytes != checked((uint)delta.Length))
            {
                throw new ArgumentException(
                    "Object delta payload length does not match DeltaBytes.",
                    nameof(delta));
            }

            return Join(metadataBody, delta);
        }

        private static bool IsTerminalResult(NnrpNativeRuntimeEvent @event)
        {
            return @event.HasWireHeader
                && (@event.MessageType == (uint)MessageType.ResultPush
                    || @event.MessageType == (uint)MessageType.ResultDropReason);
        }

        private static bool MatchesOperation(
            NnrpNativeRuntimeEvent @event,
            ulong operationId)
        {
            return OperationIdOf(@event) == operationId;
        }

        private bool TryTakeMatchingTerminal(
            ulong operationId,
            out NnrpNativeRuntimeEvent? matched)
        {
            matched = null;
            var count = deferredEvents.Count;
            for (var index = 0; index < count; index++)
            {
                var candidate = deferredEvents.Dequeue();
                if (ShouldSuppress(candidate))
                {
                    continue;
                }

                if (matched == null && IsTerminalResult(candidate) && MatchesOperation(candidate, operationId))
                {
                    matched = candidate;
                    continue;
                }

                deferredEvents.Enqueue(candidate);
            }

            return matched != null;
        }

        private bool TryTakeNextTerminal(out NnrpNativeRuntimeEvent? matched)
        {
            matched = null;
            var count = deferredEvents.Count;
            for (var index = 0; index < count; index++)
            {
                var candidate = deferredEvents.Dequeue();
                if (ShouldSuppress(candidate))
                {
                    continue;
                }

                if (matched == null && IsTerminalResult(candidate))
                {
                    matched = candidate;
                    continue;
                }

                deferredEvents.Enqueue(candidate);
            }

            return matched != null;
        }

        private bool ShouldSuppress(NnrpNativeRuntimeEvent @event)
        {
            if (!@event.HasWireHeader)
            {
                return false;
            }

            if (@event.MessageType == (uint)MessageType.ResultDropReason)
            {
                return false;
            }

            if (@event.MessageType != (uint)MessageType.ResultPush
                && @event.MessageType != (uint)MessageType.PartialResult)
            {
                return false;
            }

            var operationId = OperationIdOf(@event);
            if (operationId == 0 && @event.MessageType == (uint)MessageType.PartialResult)
            {
                operationId = @event.ToRuntimeEvent().Metadata.Get<PartialResultMetadata>().OperationId;
            }

            lock (stateGate)
            {
                return operationId != 0 && cancelledOperations.Contains(operationId);
            }
        }

        private void RememberCancelledOperation(ulong operationId)
        {
            lock (stateGate)
            {
                if (!cancelledOperations.Add(operationId))
                {
                    return;
                }

                cancelledOperationOrder.Enqueue(operationId);
                while (cancelledOperationOrder.Count > MaxCancelledOperationSuppressions)
                {
                    cancelledOperations.Remove(cancelledOperationOrder.Dequeue());
                }
            }
        }

        private static ulong OperationIdOf(NnrpNativeRuntimeEvent @event)
        {
            if (@event.Diagnostic.RelatedOperationId != 0)
            {
                return @event.Diagnostic.RelatedOperationId;
            }

            if (@event.MessageType == (uint)MessageType.ResultDropReason && @event.HasWireHeader)
            {
                return @event.ToRuntimeEvent().Metadata.Get<ResultDropReasonMetadata>().OperationId;
            }

            return @event.Diagnostic.RelatedOperationId;
        }

        private static NnrpResult ToResult(NnrpNativeRuntimeEvent nativeEvent)
        {
            var @event = nativeEvent.ToRuntimeEvent();
            var state = @event.Header.MessageType == MessageType.ResultDropReason
                ? @event.Metadata.Get<ResultDropReasonMetadata>().DropReasonCode
                    == NnrpResultDropReasonCode.PeerCancelled
                    ? NnrpResultTerminalState.Cancelled
                    : NnrpResultTerminalState.Dropped
                : @event.Metadata.Get<ResultPushMetadata>().StatusCode == ResultStatusCode.Success
                    || @event.Metadata.Get<ResultPushMetadata>().StatusCode == ResultStatusCode.Degraded
                    ? NnrpResultTerminalState.Success
                    : NnrpResultTerminalState.Error;
            var operationId = OperationIdOf(nativeEvent);
            return new NnrpResult(operationId, state, @event);
        }

        private void EnsureOpen()
        {
            if (IsClosed)
            {
                throw new ObjectDisposedException(nameof(NnrpClientSession));
            }
        }
    }
}
