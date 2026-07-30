using System;
using Nnrp.Core;
using Nnrp.Runtime;
using Xunit;

namespace Nnrp.NativeBridge.Tests
{
    public sealed class NnrpNativeRuntimeEventTests
    {
        [Fact]
        public void LifecycleEventsCannotBeFabricatedIntoWireEvents()
        {
            var lifecycle = new NnrpNativeRuntimeEvent(
                1,
                new NnrpFfiRuntimeFrameHeader((byte)MessageType.Error, frameId: 7, present: 0),
                NnrpHandle.Invalid,
                NnrpHandle.Invalid,
                NnrpHandle.Invalid,
                Array.Empty<byte>(),
                new NnrpNativeRuntimeDiagnostic(NnrpFfiStatus.Ok, 0, 0, 0, 0));

            Assert.False(lifecycle.HasWireHeader);
            Assert.Throws<InvalidOperationException>(() => lifecycle.ToRuntimeEvent());

            var unknown = new NnrpNativeRuntimeEvent(
                1,
                new NnrpFfiRuntimeFrameHeader(byte.MaxValue, frameId: 7),
                NnrpHandle.Invalid,
                NnrpHandle.Invalid,
                NnrpHandle.Invalid,
                Array.Empty<byte>(),
                new NnrpNativeRuntimeDiagnostic(NnrpFfiStatus.Ok, 0, 0, 0, 0));
            Assert.Throws<InvalidOperationException>(() => unknown.ToRuntimeEvent());
        }

        [Fact]
        public void WireEventsPreserveEveryRuntimeHeaderField()
        {
            var metadata = new ProgressMetadata(10, 11, 12, 5000, 0, 0);
            var payload = Nnrp.Runtime.NnrpRuntimeControl.Encode(MessageType.Progress, metadata);
            var native = new NnrpNativeRuntimeEvent(
                1,
                new NnrpFfiRuntimeFrameHeader(
                    (byte)MessageType.Progress,
                    frameId: 22,
                    flags: (uint)HeaderFlags.AckRequired,
                    sessionId: 21,
                    viewId: 23,
                    routeId: 24,
                    traceId: 25),
                new NnrpHandle(NnrpHandleKind.Connection, 1, 1),
                new NnrpHandle(NnrpHandleKind.Session, 21, 1),
                new NnrpHandle(NnrpHandleKind.Operation, 10, 1),
                payload,
                new NnrpNativeRuntimeDiagnostic(NnrpFfiStatus.Ok, 1, 21, 10, 22));

            var @event = native.ToRuntimeEvent();

            Assert.Equal(MessageType.Progress, @event.Header.MessageType);
            Assert.Equal(HeaderFlags.AckRequired, @event.Header.Flags);
            Assert.Equal((uint)21, @event.Header.SessionId);
            Assert.Equal((uint)22, @event.Header.FrameId);
            Assert.Equal((ushort)23, @event.Header.ViewId);
            Assert.Equal((ushort)24, @event.Header.RouteId);
            Assert.Equal((ulong)25, @event.Header.TraceId);
            Assert.Equal(metadata, @event.Metadata.Get<ProgressMetadata>());
        }

        [Fact]
        public void ResultDropRequestPreservesFrozenFfiLayout()
        {
            var metadata = new ResultDropReasonMetadata(
                41,
                42,
                NnrpResultDropReasonCode.Backpressure,
                RuntimeRole.Server,
                3,
                4);
            var descriptor = new NnrpResultDropReasonDescriptor(metadata);
            var operation = new NnrpHandle(NnrpHandleKind.Operation, 41, 2);
            var diagnostics = new NnrpBufferView(new IntPtr(7), new UIntPtr(4));
            var request = new NnrpServerDropStaleResultRequest(
                operation,
                metadata,
                diagnostics,
                new UIntPtr(8));

            Assert.Equal((ulong)41, descriptor.OperationId);
            Assert.Equal((ulong)42, descriptor.ResultSequence);
            Assert.Equal((ushort)NnrpResultDropReasonCode.Backpressure, descriptor.DropReasonCode);
            Assert.Equal((byte)RuntimeRole.Server, descriptor.SourceRole);
            Assert.Equal((byte)3, descriptor.Flags);
            Assert.Equal((uint)4, descriptor.DiagnosticBytes);
            Assert.Equal(operation, request.Operation);
            Assert.Equal(descriptor.OperationId, request.DropReason.OperationId);
            Assert.Equal(diagnostics.Pointer, request.Diagnostics.Pointer);
            Assert.Equal(new UIntPtr(8), request.MaxEvents);
        }

        [Fact]
        public void RuntimeHandleAllocatorsNeverReturnReservedZero()
        {
            Assert.NotEqual((ulong)0, NnrpRuntimeHandleIdAllocator.Allocate());
            Assert.NotEqual((uint)0, NnrpRuntimeHandleIdAllocator.AllocateSession());
        }
    }
}
