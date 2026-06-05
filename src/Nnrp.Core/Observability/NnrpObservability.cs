using System;

namespace Nnrp.Core
{
    public static class NnrpObservability
    {
        public static void PublishSessionOpenAck(SessionOpenAckMessage message, NnrpObservabilityHook hook)
        {
            if (hook == null)
            {
                throw new ArgumentNullException(nameof(hook));
            }

            PublishSessionOpenAckCore(message, message.Diagnostic, hook);
        }

        public static void PublishSessionOpenAck(
            SessionOpenMetadata request,
            SessionOpenAckMessage message,
            NnrpObservabilityHook hook)
        {
            if (hook == null)
            {
                throw new ArgumentNullException(nameof(hook));
            }

            PublishSessionOpenAckCore(message, message.GetDiagnostic(request), hook);
        }

        public static void PublishFlowUpdate(FlowUpdateMessage message, NnrpObservabilityHook hook)
        {
            if (hook == null)
            {
                throw new ArgumentNullException(nameof(hook));
            }

            var diagnostic = message.Diagnostic;
            hook(new NnrpObservabilityEvent(
                NnrpObservabilityEventKind.BackpressureTransition,
                MessageType.FlowUpdate,
                message.Header.SessionId,
                message.Header.FrameId,
                message.Header.ViewId,
                message.Header.RouteId,
                message.Header.TraceId,
                diagnostic.OperationId,
                sessionDiagnostic: default,
                flowDiagnostic: diagnostic,
                failure: default));
        }

        private static void PublishSessionOpenAckCore(
            SessionOpenAckMessage message,
            SessionOpenDiagnostic diagnostic,
            NnrpObservabilityHook hook)
        {
            var routeKind = diagnostic.IsRejected || diagnostic.ShouldRetryLater
                ? NnrpObservabilityEventKind.SessionRouteRejected
                : NnrpObservabilityEventKind.SessionRouteOpened;

            hook(new NnrpObservabilityEvent(
                routeKind,
                MessageType.SessionOpenAck,
                message.Header.SessionId,
                message.Header.FrameId,
                message.Header.ViewId,
                message.Header.RouteId,
                message.Header.TraceId,
                operationId: 0,
                sessionDiagnostic: diagnostic,
                flowDiagnostic: default,
                failure: default));

            if (!diagnostic.IsPriorityDowngraded)
            {
                return;
            }

            hook(new NnrpObservabilityEvent(
                NnrpObservabilityEventKind.PriorityDowngraded,
                MessageType.SessionOpenAck,
                message.Header.SessionId,
                message.Header.FrameId,
                message.Header.ViewId,
                message.Header.RouteId,
                message.Header.TraceId,
                operationId: 0,
                sessionDiagnostic: diagnostic,
                flowDiagnostic: default,
                failure: default));
        }
    }
}
