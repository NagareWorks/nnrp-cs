using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Nnrp.Core;
using Nnrp.NativeBridge;

namespace Nnrp.TestSupport
{
    internal sealed class RuntimeEntrypointHarness : IDisposable
    {
        private readonly Queue<NnrpEvent> clientEvents = new Queue<NnrpEvent>();
        private readonly Queue<NnrpEvent[]> serverEventBatches = new Queue<NnrpEvent[]>();
        private readonly List<GCHandle> payloadPins = new List<GCHandle>();

        internal RuntimeEntrypointHarness()
        {
            Entrypoints = new NnrpNativeRuntimeEntrypoints(
                CurrentProtocolVersion,
                RuntimeCapabilities,
                ConnectionBootstrap,
                ClientConnect,
                SessionOpen,
                SessionOpen,
                Submit,
                Submit,
                HandleStatus,
                HandleStatus,
                ClientCancel,
                AwaitClientEvent,
                ServerBind,
                ServerAcceptBegin,
                ServerAcceptWait,
                ServerAcceptClaim,
                HandleStatus,
                ServerReceiveSubmit,
                ServerSendResult,
                ServerSendFlowUpdate,
                HandleStatus,
                Control,
                PollEmpty,
                DispatchEvent,
                connectionClose: HandleStatus,
                clientCloseConnection: HandleStatus,
                clientResumeSession: ClientResumeSession,
                bufferRelease: ReleaseBuffer,
                runtimeFrameSend: SendRuntimeFrame,
                clientAwaitEvents: AwaitClientEvents,
                serverAwaitEvents: AwaitServerEvents,
                serverDropStaleResult: DropStaleResult,
                clientSessionRecoveryTicket: ClientSessionRecoveryTicket);
        }

        internal NnrpNativeRuntimeEntrypoints Entrypoints { get; }

        internal List<NnrpFfiSubmitRequest> SubmitRequests { get; } = new List<NnrpFfiSubmitRequest>();

        internal List<NnrpSessionOpenRequest> SessionOpenRequests { get; } = new List<NnrpSessionOpenRequest>();

        internal List<NnrpSessionResumeRequest> SessionResumeRequests { get; } = new List<NnrpSessionResumeRequest>();

        internal List<byte[]> SubmittedRecoveryTickets { get; } = new List<byte[]>();

        internal List<NnrpHandle> ReleasedBuffers { get; } = new List<NnrpHandle>();

        internal byte[]? IssuedRecoveryTicket { get; set; }

        internal List<(NnrpRuntimeFrameSendRequest Request, byte[] Payload)> RuntimeFrames { get; } =
            new List<(NnrpRuntimeFrameSendRequest, byte[])>();

        internal List<byte[]> ServerResults { get; } = new List<byte[]>();

        internal List<(NnrpServerDropStaleResultRequest Request, byte[] Diagnostic)> ServerDrops { get; } =
            new List<(NnrpServerDropStaleResultRequest, byte[])>();

        internal NnrpFfiStatus NextServerResultStatus { get; set; } = NnrpFfiStatus.Ok;

        internal NnrpFfiStatus NextServerDropStatus { get; set; } = NnrpFfiStatus.Ok;

        internal void QueueClientEvent(
            MessageType messageType,
            uint frameId,
            ulong operationId,
            byte[] payload,
            uint sessionId = 41,
            uint kind = 1)
        {
            clientEvents.Enqueue(CreateEvent(messageType, frameId, operationId, payload, sessionId, kind));
        }

        internal void QueueClientLifecycleEvent(
            uint kind,
            ulong operationId,
            NnrpFfiStatus status = default,
            uint sessionId = 41)
        {
            if (operationId == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(operationId));
            }

            clientEvents.Enqueue(new NnrpEvent(
                kind,
                new NnrpFfiRuntimeFrameHeader(0, 0, present: 0),
                new NnrpHandle(NnrpHandleKind.Connection, 1, 1),
                new NnrpHandle(NnrpHandleKind.Session, sessionId, 1),
                new NnrpHandle(NnrpHandleKind.Operation, checked(operationId + 10_000), 1),
                NnrpHandle.Invalid,
                NnrpBufferView.Empty,
                new NnrpFfiDiagnostic(status, relatedOperationId: operationId)));
        }

        internal void QueueClientOperationLifecycleEvent(
            NnrpOperationState state,
            ulong operationId,
            uint sessionId = 41)
        {
            clientEvents.Enqueue(CreateOperationLifecycleEvent(state, operationId, sessionId));
        }

        internal void QueueServerBatch(params NnrpEvent[] events)
        {
            serverEventBatches.Enqueue(events ?? throw new ArgumentNullException(nameof(events)));
        }

        internal NnrpEvent CreateOperationLifecycleEvent(
            NnrpOperationState state,
            ulong operationId,
            uint sessionId = 41)
        {
            if (operationId == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(operationId));
            }

            var payload = new[] { (byte)state };
            var pin = GCHandle.Alloc(payload, GCHandleType.Pinned);
            payloadPins.Add(pin);
            return new NnrpEvent(
                14,
                new NnrpFfiRuntimeFrameHeader(0, 0, present: 0),
                new NnrpHandle(NnrpHandleKind.Connection, 1, 1),
                new NnrpHandle(NnrpHandleKind.Session, sessionId, 1),
                new NnrpHandle(NnrpHandleKind.Operation, checked(operationId + 10_000), 1),
                NnrpHandle.Invalid,
                new NnrpBufferView(pin.AddrOfPinnedObject(), new UIntPtr(1)),
                new NnrpFfiDiagnostic(NnrpFfiStatus.Ok, relatedOperationId: operationId));
        }

        internal NnrpEvent CreateEvent(
            MessageType messageType,
            uint frameId,
            ulong operationId,
            byte[] payload,
            uint sessionId = 41,
            uint kind = 1)
        {
            payload ??= Array.Empty<byte>();
            var view = NnrpBufferView.Empty;
            if (payload.Length != 0)
            {
                var pin = GCHandle.Alloc(payload, GCHandleType.Pinned);
                payloadPins.Add(pin);
                view = new NnrpBufferView(pin.AddrOfPinnedObject(), new UIntPtr((uint)payload.Length));
            }

            return new NnrpEvent(
                kind,
                (uint)messageType,
                new NnrpHandle(NnrpHandleKind.Connection, 1, 1),
                new NnrpHandle(NnrpHandleKind.Session, sessionId, 1),
                operationId == 0
                    ? NnrpHandle.Invalid
                    : new NnrpHandle(NnrpHandleKind.Operation, checked(operationId + 10_000), 1),
                frameId,
                NnrpHandle.Invalid,
                view,
                new NnrpFfiDiagnostic(NnrpFfiStatus.Ok, relatedOperationId: operationId));
        }

        public void Dispose()
        {
            foreach (var pin in payloadPins)
            {
                if (pin.IsAllocated)
                {
                    pin.Free();
                }
            }

            payloadPins.Clear();
            Entrypoints.Dispose();
        }

        private static NnrpProtocolVersion CurrentProtocolVersion() => new NnrpProtocolVersion(1, 0);

        private static NnrpRuntimeCapabilities RuntimeCapabilities() =>
            new NnrpRuntimeCapabilities(
                NnrpNativeArtifact.ExpectedAbiMajor,
                NnrpNativeArtifact.ExpectedAbiMinor,
                NnrpNativeArtifact.ExpectedAbiPatch,
                new NnrpProtocolVersion(1, 0),
                1,
                0,
                0,
                3,
                1,
                NnrpNativeArtifact.TransportSlotTcp,
                NnrpNativeArtifact.RequiredRuntimeFeatures);

        private static NnrpFfiStatus ConnectionBootstrap(
            NnrpConnectionBootstrap request,
            out NnrpHandle connection)
        {
            connection = new NnrpHandle(NnrpHandleKind.Connection, request.ConnectionId, request.Generation);
            return NnrpFfiStatus.Ok;
        }

        private static NnrpFfiStatus ClientConnect(NnrpClientConnectRequest request, out NnrpHandle connection)
        {
            connection = new NnrpHandle(NnrpHandleKind.Connection, request.ConnectionId, request.Generation);
            return NnrpFfiStatus.Ok;
        }

        private NnrpFfiStatus SessionOpen(NnrpSessionOpenRequest request, out NnrpHandle session)
        {
            SessionOpenRequests.Add(request);
            session = new NnrpHandle(NnrpHandleKind.Session, request.SessionHandleId, request.Generation);
            return NnrpFfiStatus.Ok;
        }

        private NnrpFfiStatus ClientResumeSession(
            NnrpSessionResumeRequest request,
            out NnrpHandle session,
            out NnrpSessionRecoveryOutcome outcome)
        {
            SessionResumeRequests.Add(request);
            SubmittedRecoveryTickets.Add(Copy(request.RecoveryTicket));
            session = new NnrpHandle(
                NnrpHandleKind.Session,
                request.Open.SessionHandleId,
                request.Open.Generation);
            outcome = new NnrpSessionRecoveryOutcome(1, 120_000);
            return NnrpFfiStatus.Ok;
        }

        private NnrpFfiStatus ClientSessionRecoveryTicket(
            NnrpHandle session,
            out NnrpHandle owner,
            out NnrpBufferView ticket)
        {
            owner = NnrpHandle.Invalid;
            ticket = NnrpBufferView.Empty;
            if (IssuedRecoveryTicket == null)
            {
                return new NnrpFfiStatus(
                    NnrpFfiStatusCode.InvalidArgument,
                    detailCode: 104);
            }

            if (session.Kind != NnrpHandleKind.Session)
            {
                return new NnrpFfiStatus(NnrpFfiStatusCode.InvalidHandle);
            }

            var pin = GCHandle.Alloc(IssuedRecoveryTicket, GCHandleType.Pinned);
            payloadPins.Add(pin);
            owner = new NnrpHandle(NnrpHandleKind.Buffer, 900, 1);
            ticket = new NnrpBufferView(
                pin.AddrOfPinnedObject(),
                new UIntPtr((uint)IssuedRecoveryTicket.Length));
            return NnrpFfiStatus.Ok;
        }

        private NnrpFfiStatus ReleaseBuffer(NnrpHandle handle)
        {
            ReleasedBuffers.Add(handle);
            return HandleStatus(handle);
        }

        private NnrpFfiStatus Submit(NnrpFfiSubmitRequest request, out NnrpHandle operation)
        {
            SubmitRequests.Add(request);
            operation = new NnrpHandle(NnrpHandleKind.Operation, request.OperationId, 1);
            return NnrpFfiStatus.Ok;
        }

        private static NnrpFfiStatus HandleStatus(NnrpHandle handle) =>
            handle.IsValid ? NnrpFfiStatus.Ok : new NnrpFfiStatus(NnrpFfiStatusCode.InvalidHandle);

        private static NnrpFfiStatus ClientCancel(NnrpClientCancelRequest request) =>
            request.Session.IsValid ? NnrpFfiStatus.Ok : new NnrpFfiStatus(NnrpFfiStatusCode.InvalidHandle);

        private NnrpFfiStatus AwaitClientEvent(NnrpHandle connection, out NnrpPollResult result)
        {
            if (clientEvents.Count == 0)
            {
                result = new NnrpPollResult(new NnrpFfiStatus(NnrpFfiStatusCode.WouldBlock), 0, default);
                return NnrpFfiStatus.Ok;
            }

            result = new NnrpPollResult(NnrpFfiStatus.Ok, 1, clientEvents.Dequeue());
            return NnrpFfiStatus.Ok;
        }

        private NnrpFfiStatus AwaitClientEvents(
            NnrpRoleEventPollRequest request,
            IntPtr events,
            UIntPtr eventCapacity,
            out UIntPtr eventCount)
        {
            request.Scope.RequireKind(NnrpHandleKind.Session);
            var capacity = eventCapacity.ToUInt64();
            var count = Math.Min((ulong)clientEvents.Count, capacity);
            var eventSize = Marshal.SizeOf<NnrpEvent>();
            for (ulong index = 0; index < count; index++)
            {
                Marshal.StructureToPtr(
                    clientEvents.Dequeue(),
                    IntPtr.Add(events, checked((int)index * eventSize)),
                    false);
            }

            eventCount = new UIntPtr(count);
            return count == 0
                ? new NnrpFfiStatus(NnrpFfiStatusCode.WouldBlock)
                : NnrpFfiStatus.Ok;
        }

        private static NnrpFfiStatus ServerBind(NnrpServerBindRequest request, out NnrpHandle server)
        {
            server = new NnrpHandle(NnrpHandleKind.Connection, request.ServerId == 0 ? 1UL : request.ServerId, request.Generation);
            return NnrpFfiStatus.Ok;
        }

        private static NnrpFfiStatus ServerAcceptBegin(
            NnrpServerAcceptBeginRequest request,
            out NnrpHandle accept)
        {
            accept = new NnrpHandle(NnrpHandleKind.ServerAccept, request.AcceptHandleId, request.Generation);
            return NnrpFfiStatus.Ok;
        }

        private static NnrpFfiStatus ServerAcceptWait(NnrpServerAcceptWaitRequest request) => NnrpFfiStatus.Ok;

        private static NnrpFfiStatus ServerAcceptClaim(
            NnrpServerAcceptClaimRequest request,
            out NnrpServerAcceptResult result)
        {
            result = new NnrpServerAcceptResult(
                new NnrpHandle(NnrpHandleKind.Session, request.SessionHandleId, request.Generation),
                (uint)TransportId.Tcp);
            return NnrpFfiStatus.Ok;
        }

        private static NnrpFfiStatus ServerReceiveSubmit(
            NnrpServerReceiveSubmitRequest request,
            out NnrpHandle operation)
        {
            operation = new NnrpHandle(NnrpHandleKind.Operation, request.OperationId, 1);
            return NnrpFfiStatus.Ok;
        }

        private NnrpFfiStatus ServerSendResult(NnrpServerSendResultRequest request)
        {
            ServerResults.Add(Copy(request.Payload));
            var status = NextServerResultStatus;
            NextServerResultStatus = NnrpFfiStatus.Ok;
            return status;
        }

        private static NnrpFfiStatus ServerSendFlowUpdate(NnrpServerFlowUpdateRequest request) => NnrpFfiStatus.Ok;

        private static NnrpFfiStatus Control(NnrpControlRequest request) => NnrpFfiStatus.Ok;

        private static NnrpFfiStatus PollEmpty(out NnrpPollResult result)
        {
            result = new NnrpPollResult(NnrpFfiStatus.Ok, 0, default);
            return NnrpFfiStatus.Ok;
        }

        private static NnrpFfiStatus DispatchEvent(NnrpCallbackSink sink, ref NnrpEvent @event) => NnrpFfiStatus.Ok;

        private NnrpFfiStatus SendRuntimeFrame(NnrpRuntimeFrameSendRequest request)
        {
            RuntimeFrames.Add((request, Copy(request.Payload)));
            return NnrpFfiStatus.Ok;
        }

        private NnrpFfiStatus AwaitServerEvents(
            NnrpRoleEventPollRequest request,
            IntPtr events,
            UIntPtr eventCapacity,
            out UIntPtr eventCount)
        {
            if (serverEventBatches.Count == 0)
            {
                eventCount = UIntPtr.Zero;
                return new NnrpFfiStatus(NnrpFfiStatusCode.WouldBlock);
            }

            var batch = serverEventBatches.Dequeue();
            if ((ulong)batch.Length > eventCapacity.ToUInt64())
            {
                throw new InvalidOperationException("Test event batch exceeds native capacity.");
            }

            var eventSize = Marshal.SizeOf<NnrpEvent>();
            for (var index = 0; index < batch.Length; index++)
            {
                Marshal.StructureToPtr(batch[index], IntPtr.Add(events, index * eventSize), false);
            }

            eventCount = new UIntPtr((uint)batch.Length);
            return NnrpFfiStatus.Ok;
        }

        private NnrpFfiStatus DropStaleResult(
            NnrpServerDropStaleResultRequest request,
            out NnrpPollResult result)
        {
            ServerDrops.Add((request, Copy(request.Diagnostics)));
            var status = NextServerDropStatus;
            NextServerDropStatus = NnrpFfiStatus.Ok;
            result = new NnrpPollResult(status, 0, default);
            return status;
        }

        private static byte[] Copy(NnrpBufferView view)
        {
            if (view.Length == UIntPtr.Zero)
            {
                return Array.Empty<byte>();
            }

            var bytes = new byte[checked((int)view.Length.ToUInt64())];
            Marshal.Copy(view.Pointer, bytes, 0, bytes.Length);
            return bytes;
        }
    }
}
