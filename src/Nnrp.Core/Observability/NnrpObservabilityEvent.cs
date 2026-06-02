using System;

namespace Nnrp.Core
{
    public enum NnrpObservabilityEventKind
    {
        SessionRouteOpened = 0,

        SessionRouteAccepted = 1,

        SessionRouteRejected = 2,

        SessionRouteClosed = 3,

        PriorityDowngraded = 4,

        BackpressureTransition = 5,
    }

    public delegate void NnrpObservabilityHook(NnrpObservabilityEvent observation);

    public readonly struct NnrpObservabilityEvent : IEquatable<NnrpObservabilityEvent>
    {
        public NnrpObservabilityEvent(
            NnrpObservabilityEventKind kind,
            MessageType messageType,
            uint sessionId,
            uint frameId,
            ushort viewId,
            ushort routeId,
            ulong traceId,
            ulong operationId,
            SessionOpenDiagnostic sessionDiagnostic,
            FlowControlDiagnostic flowDiagnostic,
            NnrpProtocolFailure failure)
        {
            if (!Enum.IsDefined(typeof(NnrpObservabilityEventKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }

            if (!Enum.IsDefined(typeof(MessageType), messageType))
            {
                throw new ArgumentOutOfRangeException(nameof(messageType));
            }

            Kind = kind;
            MessageType = messageType;
            SessionId = sessionId;
            FrameId = frameId;
            ViewId = viewId;
            RouteId = routeId;
            TraceId = traceId;
            OperationId = operationId;
            SessionDiagnostic = sessionDiagnostic;
            FlowDiagnostic = flowDiagnostic;
            Failure = failure;
        }

        public NnrpObservabilityEventKind Kind { get; }

        public MessageType MessageType { get; }

        public uint SessionId { get; }

        public uint FrameId { get; }

        public ushort ViewId { get; }

        public ushort RouteId { get; }

        public ulong TraceId { get; }

        public ulong OperationId { get; }

        public SessionOpenDiagnostic SessionDiagnostic { get; }

        public FlowControlDiagnostic FlowDiagnostic { get; }

        public NnrpProtocolFailure Failure { get; }

        public bool HasSessionDiagnostic => SessionDiagnostic.HasDiagnostic;

        public bool HasFlowDiagnostic => FlowDiagnostic.HasDiagnostic;

        public bool HasFailure => Failure.IsFailure;

        public bool Equals(NnrpObservabilityEvent other)
        {
            return Kind == other.Kind
                && MessageType == other.MessageType
                && SessionId == other.SessionId
                && FrameId == other.FrameId
                && ViewId == other.ViewId
                && RouteId == other.RouteId
                && TraceId == other.TraceId
                && OperationId == other.OperationId
                && SessionDiagnostic.Equals(other.SessionDiagnostic)
                && FlowDiagnostic.Equals(other.FlowDiagnostic)
                && Failure.Equals(other.Failure);
        }

        public override bool Equals(object obj)
        {
            return obj is NnrpObservabilityEvent other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = Kind.GetHashCode();
                hash = (hash * 397) ^ MessageType.GetHashCode();
                hash = (hash * 397) ^ SessionId.GetHashCode();
                hash = (hash * 397) ^ FrameId.GetHashCode();
                hash = (hash * 397) ^ ViewId.GetHashCode();
                hash = (hash * 397) ^ RouteId.GetHashCode();
                hash = (hash * 397) ^ TraceId.GetHashCode();
                hash = (hash * 397) ^ OperationId.GetHashCode();
                hash = (hash * 397) ^ SessionDiagnostic.GetHashCode();
                hash = (hash * 397) ^ FlowDiagnostic.GetHashCode();
                hash = (hash * 397) ^ Failure.GetHashCode();
                return hash;
            }
        }
    }
}
