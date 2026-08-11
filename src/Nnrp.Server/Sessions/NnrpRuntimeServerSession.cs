using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nnrp.Core;
using Nnrp.NativeBridge;
using Nnrp.Runtime;

namespace Nnrp.Server
{
    public sealed class NnrpServerOperation
    {
        private readonly NnrpNativeRuntimeOperation operation;
        private readonly NnrpNativeRuntimeServerSession session;
        private int terminal;

        internal NnrpServerOperation(
            NnrpNativeRuntimeServerSession session,
            NnrpNativeRuntimeOperation operation,
            NnrpRuntimeEvent submit)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.operation = operation ?? throw new ArgumentNullException(nameof(operation));
            if (submit == null || submit.Metadata.Kind != NnrpRuntimeEventMetadataKind.FrameSubmit)
            {
                throw new ArgumentException("Server operations require a decoded FRAME_SUBMIT event.", nameof(submit));
            }

            Submit = submit;
        }

        public ulong OperationId => operation.OperationId;

        public uint FrameId => operation.FrameId;

        public FrameSubmitMetadata Metadata => Submit.Metadata.Get<FrameSubmitMetadata>();

        public ReadOnlyMemory<byte> Body => Submit.Tail.Match(
            none: () => ReadOnlyMemory<byte>.Empty,
            body: value => value,
            diagnostic: _ => throw new InvalidOperationException("FRAME_SUBMIT cannot carry a diagnostic tail."),
            metadataBodyAndDelta: (_, _) => throw new InvalidOperationException("FRAME_SUBMIT cannot carry a delta tail."));

        public ulong TraceId => Submit.Header.TraceId;

        public NnrpRuntimeEvent Submit { get; }

        public ValueTask SendResultAsync(
            ResultPushMetadata metadata,
            ReadOnlyMemory<byte> body = default,
            CancellationToken cancellationToken = default)
        {
            BeginTerminal(cancellationToken);
            try
            {
                session.SendResult(operation, Join(metadata.ToArray(), body));
                return default;
            }
            catch
            {
                Interlocked.Exchange(ref terminal, 0);
                throw;
            }
        }

        public ValueTask SendResultDropAsync(
            ResultDropReasonMetadata metadata,
            ReadOnlyMemory<byte> diagnostic = default,
            CancellationToken cancellationToken = default)
        {
            BeginTerminal(cancellationToken);
            try
            {
                session.DropResult(operation, metadata, diagnostic);
                return default;
            }
            catch
            {
                Interlocked.Exchange(ref terminal, 0);
                throw;
            }
        }

        public ValueTask SendProgressAsync(
            ProgressMetadata metadata,
            ReadOnlyMemory<byte> body = default,
            CancellationToken cancellationToken = default)
        {
            EnsureReplyAllowed(metadata.OperationId, nameof(metadata), cancellationToken);
            session.SendProgress(operation, metadata, body);
            return default;
        }

        public ValueTask SendPartialResultAsync(
            PartialResultMetadata metadata,
            ReadOnlyMemory<byte> body = default,
            CancellationToken cancellationToken = default)
        {
            EnsureReplyAllowed(metadata.OperationId, nameof(metadata), cancellationToken);
            session.SendPartialResult(operation, metadata, body);
            return default;
        }

        private void EnsureReplyAllowed(
            ulong operationId,
            string parameterName,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (operationId != OperationId)
            {
                throw new ArgumentException(
                    "Reply operation id does not match the accepted operation.",
                    parameterName);
            }

            if (Volatile.Read(ref terminal) != 0)
            {
                throw new NnrpNativeInvalidStateException(
                    new NnrpFfiStatus(NnrpFfiStatusCode.InvalidState, NnrpErrorFamily.Operation));
            }
        }

        private void BeginTerminal(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Interlocked.CompareExchange(ref terminal, 1, 0) != 0)
            {
                throw new NnrpNativeInvalidStateException(
                    new NnrpFfiStatus(NnrpFfiStatusCode.InvalidState, NnrpErrorFamily.Operation));
            }
        }

        private static byte[] Join(byte[] metadata, ReadOnlyMemory<byte> body)
        {
            var payload = new byte[checked(metadata.Length + body.Length)];
            metadata.CopyTo(payload, 0);
            body.Span.CopyTo(payload.AsSpan(metadata.Length));
            return payload;
        }
    }

    public sealed class NnrpServerSession : IAsyncDisposable
    {
        private const uint EventPollTimeoutMilliseconds = 10;

        private readonly SemaphoreSlim consumeGate = new SemaphoreSlim(1, 1);
        private readonly Queue<NnrpNativeRuntimeEvent> deferredEvents = new Queue<NnrpNativeRuntimeEvent>();
        private readonly NnrpAcceptedServerTransportSession accepted;
        private readonly NnrpServer server;
        private readonly NnrpNativeRuntimeServerSession session;

        internal NnrpServerSession(
            NnrpServer server,
            NnrpAcceptedServerTransportSession accepted,
            NnrpNativeRuntimeServerSession session)
        {
            this.server = server ?? throw new ArgumentNullException(nameof(server));
            this.accepted = accepted ?? throw new ArgumentNullException(nameof(accepted));
            this.session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public TransportId ActiveTransportId => accepted.ActiveTransportId;

        public bool IsClosed { get; private set; }

        public async ValueTask<NnrpServerOperation> ReceiveSubmitAsync(CancellationToken cancellationToken = default)
        {
            EnsureOpen();
            await consumeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                while (true)
                {
                    if (TryTakeSubmit(out var deferred))
                    {
                        return CreateOperation(deferred!);
                    }

                    var nativeEvent = await NextNativeEventAsync(cancellationToken).ConfigureAwait(false);
                    if (IsSubmit(nativeEvent))
                    {
                        return CreateOperation(nativeEvent);
                    }

                    deferredEvents.Enqueue(nativeEvent);
                }
            }
            finally
            {
                consumeGate.Release();
            }
        }

        public async ValueTask<NnrpServerEvent> NextEventAsync(CancellationToken cancellationToken = default)
        {
            EnsureOpen();
            await consumeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                while (true)
                {
                    var nativeEvent = deferredEvents.Count != 0
                        ? deferredEvents.Dequeue()
                        : await NextNativeEventAsync(cancellationToken).ConfigureAwait(false);
                    if (nativeEvent.HasWireHeader)
                    {
                        return IsSubmit(nativeEvent)
                            ? NnrpServerEvent.FromSubmit(CreateOperation(nativeEvent))
                            : NnrpServerEvent.FromRuntime(nativeEvent.ToRuntimeEvent());
                    }

                    return NnrpServerEvent.FromLifecycle(nativeEvent.ToOperationLifecycleEvent());
                }
            }
            finally
            {
                consumeGate.Release();
            }
        }

        public ValueTask SendBackpressureAsync(PressureMetadata metadata, CancellationToken cancellationToken = default) =>
            Send(cancellationToken, () => session.SendBackpressure(metadata));

        public ValueTask SendCreditUpdateAsync(PressureMetadata metadata, CancellationToken cancellationToken = default) =>
            Send(cancellationToken, () => session.SendCreditUpdate(metadata));

        public ValueTask SendTraceContextAsync(TraceContextMetadata metadata, ReadOnlyMemory<byte> body = default, CancellationToken cancellationToken = default) =>
            Send(cancellationToken, () => session.SendTraceContext(metadata, body));

        public ValueTask SendRecoverableErrorAsync(RecoverableErrorMetadata metadata, ReadOnlyMemory<byte> diagnostic = default, CancellationToken cancellationToken = default) =>
            Send(cancellationToken, () => session.SendRecoverableError(metadata, diagnostic));

        public ValueTask SendRetryAfterAsync(RetryAfterMetadata metadata, ReadOnlyMemory<byte> diagnostic = default, CancellationToken cancellationToken = default) =>
            Send(cancellationToken, () => session.SendRetryAfter(metadata, diagnostic));

        public ValueTask SendControlAsync(
            MessageType messageType,
            IRuntimeControlMetadata metadata,
            ReadOnlyMemory<byte> tail = default,
            CancellationToken cancellationToken = default)
        {
            return messageType switch
            {
                MessageType.Backpressure when metadata is PressureMetadata value => SendBackpressureAsync(value, cancellationToken),
                MessageType.CreditUpdate when metadata is PressureMetadata value => SendCreditUpdateAsync(value, cancellationToken),
                MessageType.TraceContext when metadata is TraceContextMetadata value => SendTraceContextAsync(value, tail, cancellationToken),
                MessageType.ErrorRecoverable when metadata is RecoverableErrorMetadata value => SendRecoverableErrorAsync(value, tail, cancellationToken),
                MessageType.RetryAfter when metadata is RetryAfterMetadata value => SendRetryAfterAsync(value, tail, cancellationToken),
                _ => throw new ArgumentException("Message type and runtime-control metadata do not select a server-sendable frame."),
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
                accepted.Dispose();
                server.RemoveSession(this);
            }
        }

        private async ValueTask<NnrpNativeRuntimeEvent> NextNativeEventAsync(CancellationToken cancellationToken)
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                IReadOnlyList<NnrpNativeRuntimeEvent> events;
                try
                {
                    events = session.AwaitEvents(16, EventPollTimeoutMilliseconds);
                }
                catch (NnrpNativeWouldBlockException)
                {
                    await Task.Yield();
                    continue;
                }

                if (events.Count != 0)
                {
                    for (var index = 1; index < events.Count; index++)
                    {
                        deferredEvents.Enqueue(events[index]);
                    }

                    return events[0];
                }

                await Task.Yield();
            }
        }

        private bool TryTakeSubmit(out NnrpNativeRuntimeEvent? submit)
        {
            submit = null;
            var count = deferredEvents.Count;
            for (var index = 0; index < count; index++)
            {
                var candidate = deferredEvents.Dequeue();
                if (submit == null && IsSubmit(candidate))
                {
                    submit = candidate;
                    continue;
                }

                deferredEvents.Enqueue(candidate);
            }

            return submit != null;
        }

        private NnrpServerOperation CreateOperation(NnrpNativeRuntimeEvent nativeEvent)
        {
            var submit = nativeEvent.ToRuntimeEvent();
            var metadata = submit.Metadata.Get<FrameSubmitMetadata>();
            if (!nativeEvent.Operation.IsValid
                || metadata.OperationId != nativeEvent.Diagnostic.RelatedOperationId)
            {
                throw new InvalidOperationException(
                    "Native submit event operation identity does not match FRAME_SUBMIT metadata.");
            }

            var operation = new NnrpNativeRuntimeOperation(
                session.Entrypoints,
                session.Handle,
                new NnrpOperationHandle(nativeEvent.Operation),
                metadata.OperationId,
                submit.Header.FrameId);
            return new NnrpServerOperation(session, operation, submit);
        }

        private static bool IsSubmit(NnrpNativeRuntimeEvent @event)
        {
            return @event.HasWireHeader && @event.MessageType == (uint)MessageType.FrameSubmit;
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

        private void EnsureOpen()
        {
            if (IsClosed)
            {
                throw new ObjectDisposedException(nameof(NnrpServerSession));
            }
        }
    }
}
