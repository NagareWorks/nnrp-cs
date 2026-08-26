using System;
using Nnrp.Core;
using Nnrp.Runtime;
using Xunit;

namespace Nnrp.Core.Tests
{
    public sealed class RuntimeEventTests
    {
        [Fact]
        public void ClientEventIsAClosedRuntimeOrLifecycleUnion()
        {
            var runtime = NnrpRuntimeEvent.Decode(
                new RuntimeFrameHeader(MessageType.Progress),
                NnrpRuntimeControl.Encode(
                    MessageType.Progress,
                    new ProgressMetadata(81, 1, 2, 3000, 0, 0)));
            var lifecycle = new NnrpOperationLifecycleEvent(81, NnrpOperationState.Running);
            var runtimeEvent = NnrpClientEvent.FromRuntime(runtime);
            var lifecycleEvent = NnrpClientEvent.FromLifecycle(lifecycle);

            Assert.Equal(NnrpClientEventKind.Runtime, runtimeEvent.Kind);
            Assert.Same(runtime, runtimeEvent.Match(value => value, _ => null!));
            Assert.Equal(NnrpClientEventKind.Lifecycle, lifecycleEvent.Kind);
            Assert.Same(lifecycle, lifecycleEvent.Match(_ => null!, value => value));
            Assert.Throws<ArgumentNullException>(() => NnrpClientEvent.FromRuntime(null!));
            Assert.Throws<ArgumentNullException>(() => NnrpClientEvent.FromLifecycle(null!));
            Assert.Throws<ArgumentNullException>(() => runtimeEvent.Match<NnrpRuntimeEvent>(null!, _ => null!));
            Assert.Throws<ArgumentNullException>(() => runtimeEvent.Match<NnrpRuntimeEvent>(value => value, null!));
        }

        [Fact]
        public void DecodePreservesFullHeaderAndOwnsControlTail()
        {
            var metadata = new ProgressMetadata(81, 82, 5, 5000, 3, 3);
            var payload = NnrpRuntimeControl.Encode(MessageType.Progress, metadata, new byte[] { 7, 8, 9 });
            var header = new RuntimeFrameHeader(
                MessageType.Progress,
                HeaderFlags.AckRequired,
                SessionId: 41,
                FrameId: 42,
                ViewId: 43,
                RouteId: 44,
                TraceId: 45);

            var @event = NnrpRuntimeEvent.Decode(header, payload);
            payload[payload.Length - 1] = 0;

            Assert.Equal(header, @event.Header);
            Assert.Equal(NnrpRuntimeEventMetadataKind.Progress, @event.Metadata.Kind);
            Assert.Equal(metadata, @event.Metadata.Get<ProgressMetadata>());
            Assert.Equal(NnrpRuntimeEventTailKind.Body, @event.Tail.Kind);
            Assert.Equal(new byte[] { 7, 8, 9 }, BodyOf(@event).ToArray());
            Assert.Throws<InvalidOperationException>(() => @event.Metadata.Get<PressureMetadata>());
        }

        [Fact]
        public void DecodeSplitsObjectDeltaTailByDeclaredMetadataLength()
        {
            var metadata = new ObjectDeltaMetadata(1, 2, 3, 4, 2, 1, 3);
            var payload = NnrpRuntimeObject.Encode(
                MessageType.ObjectDelta,
                metadata,
                new byte[] { 10, 11, 12, 20, 21 });

            var @event = NnrpRuntimeEvent.Decode(
                new RuntimeFrameHeader(MessageType.ObjectDelta, FrameId: 7),
                payload);

            Assert.Equal(NnrpRuntimeEventMetadataKind.ObjectDelta, @event.Metadata.Kind);
            Assert.Equal(metadata, @event.Metadata.Get<ObjectDeltaMetadata>());
            Assert.Equal(NnrpRuntimeEventTailKind.MetadataBodyAndDelta, @event.Tail.Kind);
            var parts = DeltaOf(@event);
            Assert.Equal(new byte[] { 10, 11, 12 }, parts.MetadataBody.ToArray());
            Assert.Equal(new byte[] { 20, 21 }, parts.Delta.ToArray());
        }

        [Theory]
        [InlineData(MessageType.CapabilityNegotiation)]
        [InlineData(MessageType.DegradeProfile)]
        public void DecodePreservesCapabilityBodyTail(MessageType messageType)
        {
            byte[] entries = NnrpCapabilityTokenBodyCodec.Encode(
                new[] { NnrpPreview4CapabilityTokens.ControlCapabilityCosts });
            var payload = NnrpRuntimeControl.Encode(
                messageType,
                new CapabilityMetadata(1, 1, 2, 3, 4, 5, (uint)entries.Length, 0),
                entries);

            var @event = NnrpRuntimeEvent.Decode(new RuntimeFrameHeader(messageType), payload);

            Assert.Equal(entries, BodyOf(@event).ToArray());
        }

        [Fact]
        public void DecodePreservesHintTraceObjectAndCacheBodyTails()
        {
            var bytes = new byte[] { 4, 5, 6 };
            var hint = NnrpRuntimeEvent.Decode(
                new RuntimeFrameHeader(MessageType.RouteHint),
                NnrpRuntimeControl.Encode(
                    MessageType.RouteHint,
                    new RouteHintMetadata(1, 2, 3, 4, 5, 3, 0),
                    bytes));
            var trace = NnrpRuntimeEvent.Decode(
                new RuntimeFrameHeader(MessageType.TraceContext),
                NnrpRuntimeControl.Encode(
                    MessageType.TraceContext,
                    new TraceContextMetadata(1, 2, 3, 4, 0, 3),
                    bytes));
            var declared = NnrpRuntimeEvent.Decode(
                new RuntimeFrameHeader(MessageType.ObjectDeclare),
                NnrpRuntimeObject.Encode(
                    MessageType.ObjectDeclare,
                    new ObjectDescriptorMetadata(
                        1,
                        RuntimeObjectKind.Tensor,
                        RuntimeRole.Client,
                        RuntimeRole.Server,
                        2,
                        3,
                        4,
                        MemoryLocationHint.HostMemory,
                        OwnershipHint.TransferOnRef,
                        5,
                        3),
                    bytes));
            var cached = NnrpRuntimeEvent.Decode(
                new RuntimeFrameHeader(MessageType.CacheReference),
                NnrpRuntimeObject.Encode(
                    MessageType.CacheReference,
                    new CacheReferenceMetadata(1, 2, 3, 4, CacheReuseScope.Session, 5, 6, 7, 3, 0),
                    bytes));

            Assert.Equal(bytes, BodyOf(hint).ToArray());
            Assert.Equal(bytes, BodyOf(trace).ToArray());
            Assert.Equal(bytes, BodyOf(declared).ToArray());
            Assert.Equal(bytes, BodyOf(cached).ToArray());
        }

        [Fact]
        public void DecodeRejectsNonEventMessagesAndMalformedPayloads()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                NnrpRuntimeEventMetadata.Create(
                    NnrpRuntimeEventMetadataKind.None,
                    new ProgressMetadata(1, 2, 3, 4, 0, 0)));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                NnrpRuntimeEvent.Decode(new RuntimeFrameHeader(MessageType.Ping), ReadOnlySpan<byte>.Empty));
            Assert.Throws<ArgumentException>(() =>
                NnrpRuntimeEvent.Decode(new RuntimeFrameHeader(MessageType.FrameCancel), new byte[] { 1 }));
            Assert.Throws<ArgumentException>(() =>
                NnrpRuntimeEvent.Decode(new RuntimeFrameHeader(MessageType.ResultPush), new byte[3]));
            Assert.Throws<ArgumentException>(() =>
                NnrpRuntimeEvent.Decode(
                    new RuntimeFrameHeader(MessageType.CacheInvalidate),
                    new byte[CacheInvalidateMetadata.MetadataLength - 1]));
            Assert.Throws<ArgumentException>(() =>
                NnrpRuntimeEvent.Decode(
                    new RuntimeFrameHeader(MessageType.ResultHint),
                    new byte[ResultHintMetadata.MetadataLength - 1]));
            Assert.Throws<ArgumentException>(() =>
                NnrpRuntimeEvent.Decode(
                    new RuntimeFrameHeader(
                        MessageType.FrameCancel,
                        VersionMajor: 9),
                    ReadOnlySpan<byte>.Empty));

            AssertInvalidFixedMetadata(MessageType.FrameSubmit, FrameSubmitMetadata.MetadataLength);
            AssertInvalidFixedMetadata(MessageType.ResultPush, ResultPushMetadata.MetadataLength);
            AssertInvalidFixedMetadata(MessageType.ResultHint, ResultHintMetadata.MetadataLength);
            AssertInvalidFixedMetadata(MessageType.FlowUpdate, FlowUpdateMetadata.MetadataLength);
            AssertInvalidFixedMetadata(MessageType.CacheInvalidate, CacheInvalidateMetadata.MetadataLength);
            AssertInvalidFixedMetadata(MessageType.SessionClose, SessionCloseMetadata.MetadataLength);
        }

        [Fact]
        public void TailMatchRequiresEveryVariantHandler()
        {
            var tail = NnrpRuntimeEvent.Decode(
                new RuntimeFrameHeader(MessageType.ResultDrop),
                Array.Empty<byte>()).Tail;
            Func<int> none = () => 0;
            Func<ReadOnlyMemory<byte>, int> body = _ => 1;
            Func<ReadOnlyMemory<byte>, int> diagnostic = _ => 2;
            Func<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>, int> delta = (_, _) => 3;

            Assert.Throws<ArgumentNullException>(() => tail.Match(null!, body, diagnostic, delta));
            Assert.Throws<ArgumentNullException>(() => tail.Match(none, null!, diagnostic, delta));
            Assert.Throws<ArgumentNullException>(() => tail.Match(none, body, null!, delta));
            Assert.Throws<ArgumentNullException>(() => tail.Match(none, body, diagnostic, null!));
            Assert.Equal(0, tail.Match(none, body, diagnostic, delta));
        }

        [Fact]
        public void DecodeCoversEveryFrozenRuntimeEventFamily()
        {
            var body = new byte[] { 1, 2 };
            var diagnostic = new byte[] { 3, 4 };
            byte[] capabilityBody = NnrpCapabilityTokenBodyCodec.Encode(
                new[]
                {
                    NnrpPreview4CapabilityTokens.ControlCapabilityCosts,
                    NnrpPreview4CapabilityTokens.ControlRouteExecutionHint,
                });

            AssertEvent(
                MessageType.FrameSubmit,
                Append(FrameSubmit(1), body),
                NnrpRuntimeEventMetadataKind.FrameSubmit,
                NnrpRuntimeEventTailKind.Body);
            AssertEvent(
                MessageType.ResultPush,
                Append(ResultPush(), body),
                NnrpRuntimeEventMetadataKind.ResultPush,
                NnrpRuntimeEventTailKind.Body);
            AssertEvent(
                MessageType.ResultHint,
                new ResultHintMetadata(
                    ResultHintBudgetPolicy.Partial,
                    ResultHintCongestionState.Elevated,
                    ResultHintReason.ServerBusy,
                    10).ToArray(),
                NnrpRuntimeEventMetadataKind.ResultHint,
                NnrpRuntimeEventTailKind.None);
            AssertEvent(
                MessageType.FlowUpdate,
                new FlowUpdateMetadata(
                    FlowUpdateScopeKind.Session,
                    FlowUpdateReason.Grant,
                    FlowUpdateBackpressureLevel.None,
                    0,
                    1,
                    0,
                    0,
                    0,
                    1,
                    FlowUpdateFlags.CreditValid).ToArray(),
                NnrpRuntimeEventMetadataKind.FlowUpdate,
                NnrpRuntimeEventTailKind.None);
            AssertEvent(
                MessageType.CacheInvalidate,
                new CacheInvalidateMetadata(CacheInvalidateScope.ObjectKey, 1, 2, 3, 4).ToArray(),
                NnrpRuntimeEventMetadataKind.CacheInvalidate,
                NnrpRuntimeEventTailKind.None);
            AssertEvent(
                MessageType.SessionClose,
                new SessionCloseMetadata(
                    SessionCloseReason.ClientShutdown,
                    InFlightPolicy.Drain,
                    10,
                    1,
                    SessionErrorCode.None,
                    2).ToArray(),
                NnrpRuntimeEventMetadataKind.SessionClose,
                NnrpRuntimeEventTailKind.None);

            AssertControl(MessageType.Cancel, new ControlRequestMetadata(1, 2, 3, RuntimeRole.Client, 0, 2), diagnostic, NnrpRuntimeEventMetadataKind.ControlRequest, NnrpRuntimeEventTailKind.Diagnostic);
            AssertControl(MessageType.Abort, new ControlRequestMetadata(1, 2, 3, RuntimeRole.Client, 0, 2), diagnostic, NnrpRuntimeEventMetadataKind.ControlRequest, NnrpRuntimeEventTailKind.Diagnostic);
            AssertControl(MessageType.PriorityUpdate, new SchedulingMetadata(1, 2, 3, 4, 5, 0), Array.Empty<byte>(), NnrpRuntimeEventMetadataKind.Scheduling, NnrpRuntimeEventTailKind.None);
            AssertControl(MessageType.Deadline, new SchedulingMetadata(1, 2, 3, 4, 5, 0), Array.Empty<byte>(), NnrpRuntimeEventMetadataKind.Scheduling, NnrpRuntimeEventTailKind.None);
            AssertControl(MessageType.ExpireAt, new SchedulingMetadata(1, 2, 3, 4, 5, 0), Array.Empty<byte>(), NnrpRuntimeEventMetadataKind.Scheduling, NnrpRuntimeEventTailKind.None);
            AssertControl(MessageType.Supersede, new SupersedeMetadata(1, 2, 3, NnrpResultDropReasonCode.Superseded, 0, 2), diagnostic, NnrpRuntimeEventMetadataKind.Supersede, NnrpRuntimeEventTailKind.Diagnostic);
            AssertControl(MessageType.BudgetUpdate, new BudgetMetadata(1, 2, 3, 4, 5, 0), Array.Empty<byte>(), NnrpRuntimeEventMetadataKind.Budget, NnrpRuntimeEventTailKind.None);
            AssertControl(MessageType.Progress, new ProgressMetadata(1, 2, 3, 4, 0, 2), body, NnrpRuntimeEventMetadataKind.Progress, NnrpRuntimeEventTailKind.Body);
            AssertControl(MessageType.PartialResult, new PartialResultMetadata(1, 2, 3, 4, 2, 0), body, NnrpRuntimeEventMetadataKind.PartialResult, NnrpRuntimeEventTailKind.Body);
            AssertControl(MessageType.Backpressure, new PressureMetadata(1, 2, 3, 4, 5, 0), Array.Empty<byte>(), NnrpRuntimeEventMetadataKind.Pressure, NnrpRuntimeEventTailKind.None);
            AssertControl(MessageType.CreditUpdate, new PressureMetadata(1, 2, 3, 4, 5, 0), Array.Empty<byte>(), NnrpRuntimeEventMetadataKind.Pressure, NnrpRuntimeEventTailKind.None);
            AssertControl(MessageType.CapabilityNegotiation, new CapabilityMetadata(1, 2, 3, 4, 5, 6, (uint)capabilityBody.Length, 0), capabilityBody, NnrpRuntimeEventMetadataKind.Capability, NnrpRuntimeEventTailKind.Body);
            AssertControl(MessageType.DegradeProfile, new CapabilityMetadata(1, 2, 3, 4, 5, 6, (uint)capabilityBody.Length, 0), capabilityBody, NnrpRuntimeEventMetadataKind.Capability, NnrpRuntimeEventTailKind.Body);
            AssertControl(MessageType.RouteHint, new RouteHintMetadata(1, 2, 3, 4, 5, 2, 0), body, NnrpRuntimeEventMetadataKind.RouteHint, NnrpRuntimeEventTailKind.Body);
            AssertControl(MessageType.ExecutionHint, new RouteHintMetadata(1, 2, 3, 4, 5, 2, 0), body, NnrpRuntimeEventMetadataKind.RouteHint, NnrpRuntimeEventTailKind.Body);
            AssertControl(MessageType.TraceContext, new TraceContextMetadata(1, 2, 3, 4, 0, 2), body, NnrpRuntimeEventMetadataKind.TraceContext, NnrpRuntimeEventTailKind.Body);
            AssertControl(MessageType.ResultDropReason, new ResultDropReasonMetadata(1, 2, NnrpResultDropReasonCode.Backpressure, RuntimeRole.Server, 0, 2), diagnostic, NnrpRuntimeEventMetadataKind.ResultDropReason, NnrpRuntimeEventTailKind.Diagnostic);
            AssertControl(MessageType.ErrorRecoverable, new RecoverableErrorMetadata(1, 2, 3, RuntimeRole.Server, 0, 4, 5, 6, 7, 2), diagnostic, NnrpRuntimeEventMetadataKind.RecoverableError, NnrpRuntimeEventTailKind.Diagnostic);
            AssertControl(MessageType.RetryAfter, new RetryAfterMetadata(1, 2, 3, 4, 5, RuntimeRole.Server, 0, 2), diagnostic, NnrpRuntimeEventMetadataKind.RetryAfter, NnrpRuntimeEventTailKind.Diagnostic);

            AssertObject(MessageType.ObjectDeclare, new ObjectDescriptorMetadata(1, RuntimeObjectKind.Tensor, RuntimeRole.Client, RuntimeRole.Server, 2, 3, 4, MemoryLocationHint.HostMemory, OwnershipHint.TransferOnRef, 5, 2), body, NnrpRuntimeEventMetadataKind.ObjectDescriptor, NnrpRuntimeEventTailKind.Body);
            AssertObject(MessageType.ObjectRef, new ObjectReferenceMetadata(1, 2, 3, 4, 5, 0, 2), body, NnrpRuntimeEventMetadataKind.ObjectReference, NnrpRuntimeEventTailKind.Body);
            AssertObject(MessageType.ObjectRelease, new ObjectReleaseMetadata(1, 2, ObjectReleaseReason.Completed, RuntimeRole.Client, 0, 2), diagnostic, NnrpRuntimeEventMetadataKind.ObjectRelease, NnrpRuntimeEventTailKind.Diagnostic);
            AssertObject(MessageType.ObjectPatch, new ObjectDeltaMetadata(1, 2, 3, 4, 1, 0, 1), body, NnrpRuntimeEventMetadataKind.ObjectDelta, NnrpRuntimeEventTailKind.MetadataBodyAndDelta);
            AssertObject(MessageType.ObjectDelta, new ObjectDeltaMetadata(1, 2, 3, 4, 1, 0, 1), body, NnrpRuntimeEventMetadataKind.ObjectDelta, NnrpRuntimeEventTailKind.MetadataBodyAndDelta);
            AssertObject(MessageType.CacheReference, new CacheReferenceMetadata(1, 2, 3, 4, CacheReuseScope.Session, 5, 6, 7, 2, 0), body, NnrpRuntimeEventMetadataKind.CacheReference, NnrpRuntimeEventTailKind.Body);
            AssertObject(MessageType.CacheMiss, new CacheMissMetadata(1, 2, 3, CacheMissReason.NotFound, 4, 2), diagnostic, NnrpRuntimeEventMetadataKind.CacheMiss, NnrpRuntimeEventTailKind.Diagnostic);

            AssertEvent(MessageType.FrameCancel, Array.Empty<byte>(), NnrpRuntimeEventMetadataKind.None, NnrpRuntimeEventTailKind.None);
            AssertEvent(MessageType.ResultDrop, Array.Empty<byte>(), NnrpRuntimeEventMetadataKind.None, NnrpRuntimeEventTailKind.None);
        }

        private static void AssertControl(
            MessageType messageType,
            IRuntimeControlMetadata metadata,
            byte[] tail,
            NnrpRuntimeEventMetadataKind kind,
            NnrpRuntimeEventTailKind tailKind)
        {
            AssertEvent(messageType, NnrpRuntimeControl.Encode(messageType, metadata, tail), kind, tailKind);
        }

        private static void AssertObject(
            MessageType messageType,
            IRuntimeObjectMetadata metadata,
            byte[] tail,
            NnrpRuntimeEventMetadataKind kind,
            NnrpRuntimeEventTailKind tailKind)
        {
            AssertEvent(messageType, NnrpRuntimeObject.Encode(messageType, metadata, tail), kind, tailKind);
        }

        private static void AssertEvent(
            MessageType messageType,
            byte[] payload,
            NnrpRuntimeEventMetadataKind kind,
            NnrpRuntimeEventTailKind tailKind)
        {
            var @event = NnrpRuntimeEvent.Decode(new RuntimeFrameHeader(messageType), payload);
            Assert.Equal(kind, @event.Metadata.Kind);
            Assert.Equal(tailKind, @event.Tail.Kind);
            Assert.Equal(tailKind, @event.Tail.Match(
                () => NnrpRuntimeEventTailKind.None,
                _ => NnrpRuntimeEventTailKind.Body,
                _ => NnrpRuntimeEventTailKind.Diagnostic,
                (_, _) => NnrpRuntimeEventTailKind.MetadataBodyAndDelta));
        }

        private static ReadOnlyMemory<byte> BodyOf(NnrpRuntimeEvent @event)
        {
            return @event.Tail.Match(
                () => throw new InvalidOperationException("Expected a body tail."),
                body => body,
                _ => throw new InvalidOperationException("Expected a body tail."),
                (_, _) => throw new InvalidOperationException("Expected a body tail."));
        }

        private static (ReadOnlyMemory<byte> MetadataBody, ReadOnlyMemory<byte> Delta) DeltaOf(
            NnrpRuntimeEvent @event)
        {
            return @event.Tail.Match(
                () => throw new InvalidOperationException("Expected a delta tail."),
                _ => throw new InvalidOperationException("Expected a delta tail."),
                _ => throw new InvalidOperationException("Expected a delta tail."),
                (metadataBody, delta) => (metadataBody, delta));
        }

        private static byte[] Append(byte[] first, byte[] second)
        {
            var combined = new byte[first.Length + second.Length];
            first.CopyTo(combined, 0);
            second.CopyTo(combined, first.Length);
            return combined;
        }

        private static void AssertInvalidFixedMetadata(MessageType messageType, int length)
        {
            var payload = new byte[length];
            Array.Fill(payload, byte.MaxValue);
            Assert.Throws<ArgumentException>(() =>
                NnrpRuntimeEvent.Decode(new RuntimeFrameHeader(messageType), payload));
        }

        private static byte[] FrameSubmit(ulong operationId)
        {
            return new FrameSubmitMetadata(
                0, 0, 0, 0, 0, 0,
                FrameClass.Keyframe,
                InputProfile.Unspecified,
                TileIndexMode.RawUInt16,
                0, 0, 0, 0, 0, 0,
                operationId,
                SubmitMode.Inline,
                BudgetPolicy.None,
                LossTolerancePolicy.InheritSession,
                0, 0,
                PayloadKind.None,
                0).ToArray();
        }

        private static byte[] ResultPush()
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
                0).ToArray();
        }

    }
}
