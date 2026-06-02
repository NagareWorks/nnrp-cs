using System;
using System.Collections.Generic;
using System.Linq;
using Nnrp.Core;
using Xunit;

namespace Nnrp.Core.Tests
{
    public sealed class ObservabilityTests
    {
        [Fact]
        public void SessionOpenAckPublishesRouteAndPriorityDowngradeObservations()
        {
            var observed = new List<NnrpObservabilityEvent>();
            var request = CreateRequest(SessionPriorityClass.Interactive);
            var ack = CreateSessionOpenAck(
                SessionStatus.Opened,
                SessionPriorityClass.Balanced,
                SessionErrorCode.None,
                SessionAckFlags.PriorityDowngraded);
            var message = new SessionOpenAckMessage(
                CreateHeader(MessageType.SessionOpenAck, SessionOpenAckMetadata.MetadataLength, ack.BodyLength, ack.SessionId),
                ack,
                Array.Empty<byte>());

            NnrpObservability.PublishSessionOpenAck(request, message, observed.Add);

            Assert.Equal(2, observed.Count);
            Assert.Equal(NnrpObservabilityEventKind.SessionRouteOpened, observed[0].Kind);
            Assert.Equal(NnrpObservabilityEventKind.PriorityDowngraded, observed[1].Kind);
            Assert.Equal(MessageType.SessionOpenAck, observed[0].MessageType);
            Assert.Equal(41u, observed[0].SessionId);
            Assert.Equal(5u, observed[0].FrameId);
            Assert.Equal(6, observed[0].ViewId);
            Assert.Equal(7, observed[0].RouteId);
            Assert.Equal(8ul, observed[0].TraceId);
            Assert.True(observed[0].HasSessionDiagnostic);
            Assert.False(observed[0].HasFlowDiagnostic);
            Assert.False(observed[0].HasFailure);
            Assert.True(observed[0].SessionDiagnostic.HasRequestedPriority);
            Assert.Equal(SessionPriorityClass.Interactive, observed[0].SessionDiagnostic.RequestedPriorityClass);
            Assert.Equal(SessionPriorityClass.Balanced, observed[0].SessionDiagnostic.AcceptedPriorityClass);
            Assert.Equal(observed[0].SessionDiagnostic, observed[1].SessionDiagnostic);
            Assert.True(observed[0].Equals((object)Clone(observed[0])));
            Assert.Equal(observed[0].GetHashCode(), Clone(observed[0]).GetHashCode());
            Assert.False(observed[0].Equals("not-observation"));
        }

        [Fact]
        public void SessionOpenAckPublishesRejectedRouteForRetryLater()
        {
            var observed = new List<NnrpObservabilityEvent>();
            var ack = CreateSessionOpenAck(
                SessionStatus.RetryLater,
                SessionPriorityClass.Balanced,
                SessionErrorCode.SessionLimitReached,
                SessionAckFlags.None);
            var message = new SessionOpenAckMessage(
                CreateHeader(MessageType.SessionOpenAck, SessionOpenAckMetadata.MetadataLength, ack.BodyLength, ack.SessionId),
                ack,
                Array.Empty<byte>());

            NnrpObservability.PublishSessionOpenAck(message, observed.Add);

            var observation = Assert.Single(observed);
            Assert.Equal(NnrpObservabilityEventKind.SessionRouteRejected, observation.Kind);
            Assert.True(observation.HasSessionDiagnostic);
            Assert.True(observation.SessionDiagnostic.ShouldRetryLater);
            Assert.True(observation.SessionDiagnostic.HasSessionError);
            Assert.False(observation.SessionDiagnostic.HasRequestedPriority);
        }

        [Fact]
        public void FlowUpdatePublishesBackpressureTransitionObservation()
        {
            var observed = new List<NnrpObservabilityEvent>();
            var metadata = new FlowUpdateMetadata(
                FlowUpdateScopeKind.Operation,
                FlowUpdateReason.Pause,
                FlowUpdateBackpressureLevel.Hard,
                connectionCredit: 0,
                sessionCredit: 0,
                operationCredit: 0,
                operationId: 99,
                retryAfterMilliseconds: 0,
                creditEpoch: 12,
                flags: FlowUpdateFlags.CreditValid | FlowUpdateFlags.DrainInFlightOnly);
            var message = new FlowUpdateMessage(
                CreateHeader(MessageType.FlowUpdate, FlowUpdateMetadata.MetadataLength, 0, sessionId: 41),
                metadata);

            NnrpObservability.PublishFlowUpdate(message, observed.Add);

            var observation = Assert.Single(observed);
            Assert.Equal(NnrpObservabilityEventKind.BackpressureTransition, observation.Kind);
            Assert.Equal(MessageType.FlowUpdate, observation.MessageType);
            Assert.Equal(41u, observation.SessionId);
            Assert.Equal(99ul, observation.OperationId);
            Assert.True(observation.HasFlowDiagnostic);
            Assert.False(observation.HasSessionDiagnostic);
            Assert.True(observation.FlowDiagnostic.ShouldPauseNewWork);
            Assert.False(observation.FlowDiagnostic.ShouldRetryLater);
            Assert.Equal(FlowUpdateReason.Pause, observation.FlowDiagnostic.UpdateReason);
            Assert.Equal(FlowUpdateBackpressureLevel.Hard, observation.FlowDiagnostic.BackpressureLevel);
        }

        [Fact]
        public void FlowUpdatePublishesGrantObservationWithoutPromotingItToDiagnostic()
        {
            var observed = new List<NnrpObservabilityEvent>();
            var metadata = new FlowUpdateMetadata(
                FlowUpdateScopeKind.Connection,
                FlowUpdateReason.Grant,
                FlowUpdateBackpressureLevel.None,
                connectionCredit: 4,
                sessionCredit: 0,
                operationCredit: 0,
                operationId: 0,
                retryAfterMilliseconds: 0,
                creditEpoch: 13,
                flags: FlowUpdateFlags.CreditValid);
            var message = new FlowUpdateMessage(
                CreateHeader(MessageType.FlowUpdate, FlowUpdateMetadata.MetadataLength, 0, sessionId: 0),
                metadata);

            NnrpObservability.PublishFlowUpdate(message, observed.Add);

            var observation = Assert.Single(observed);
            Assert.Equal(NnrpObservabilityEventKind.BackpressureTransition, observation.Kind);
            Assert.False(observation.HasFlowDiagnostic);
            Assert.Equal(FlowUpdateReason.Grant, observation.FlowDiagnostic.UpdateReason);
            Assert.Equal(4, observation.FlowDiagnostic.Credit);
        }

        [Fact]
        public void SessionContainerPublishesRoutingObservations()
        {
            var observed = new List<NnrpObservabilityEvent>();
            var container = new NnrpSessionContainer(observed.Add);

            Assert.True(container.TryOpenSession(41, out _));
            Assert.False(container.TryOpenSession(41, out var duplicateFailure));
            var submit = SmokePackets.CreateSmokeFrameSubmitMessage(41, frameId: 303, viewId: 2, traceId: 77);
            Assert.True(container.TryAcceptFrameSubmit(submit, out _));
            Assert.True(container.TryCloseSession(41, out _));
            Assert.False(container.TryAcceptFrameSubmit(41, out var closedSubmitFailure));

            Assert.Equal(
                new[]
                {
                    NnrpObservabilityEventKind.SessionRouteOpened,
                    NnrpObservabilityEventKind.SessionRouteRejected,
                    NnrpObservabilityEventKind.SessionRouteAccepted,
                    NnrpObservabilityEventKind.SessionRouteClosed,
                    NnrpObservabilityEventKind.SessionRouteRejected,
                },
                observed.Select(observation => observation.Kind).ToArray());
            Assert.True(observed[1].HasFailure);
            Assert.Equal(duplicateFailure, observed[1].Failure);
            Assert.Equal(MessageType.FrameSubmit, observed[2].MessageType);
            Assert.Equal(303u, observed[2].FrameId);
            Assert.Equal(2, observed[2].ViewId);
            Assert.Equal(77ul, observed[2].TraceId);
            Assert.True(observed[4].HasFailure);
            Assert.Equal(closedSubmitFailure, observed[4].Failure);
        }

        [Fact]
        public void SessionContainerPublishesConnectionCloseRouteObservations()
        {
            var observed = new List<NnrpObservabilityEvent>();
            var container = new NnrpSessionContainer(observed.Add);
            Assert.True(container.TryOpenSession(41, out _));
            Assert.True(container.TryOpenSession(42, out _));
            observed.Clear();

            var closed = container.CloseConnection();

            Assert.Equal(new uint[] { 41, 42 }, closed.ToArray());
            Assert.Equal(2, observed.Count);
            Assert.All(observed, observation => Assert.Equal(NnrpObservabilityEventKind.SessionRouteClosed, observation.Kind));
            Assert.All(observed, observation => Assert.Equal(MessageType.Close, observation.MessageType));
            Assert.Equal(new uint[] { 41, 42 }, observed.Select(observation => observation.SessionId).ToArray());
        }

        [Fact]
        public void ObservabilityRejectsInvalidInputs()
        {
            var ack = CreateSessionOpenAck(
                SessionStatus.Opened,
                SessionPriorityClass.Balanced,
                SessionErrorCode.None,
                SessionAckFlags.None);
            var ackMessage = new SessionOpenAckMessage(
                CreateHeader(MessageType.SessionOpenAck, SessionOpenAckMetadata.MetadataLength, ack.BodyLength, ack.SessionId),
                ack,
                Array.Empty<byte>());
            var flowMessage = new FlowUpdateMessage(
                CreateHeader(MessageType.FlowUpdate, FlowUpdateMetadata.MetadataLength, 0, sessionId: 0),
                new FlowUpdateMetadata(
                    FlowUpdateScopeKind.Connection,
                    FlowUpdateReason.Grant,
                    FlowUpdateBackpressureLevel.None,
                    connectionCredit: 1,
                    sessionCredit: 0,
                    operationCredit: 0,
                    operationId: 0,
                    retryAfterMilliseconds: 0,
                    creditEpoch: 1,
                    flags: FlowUpdateFlags.CreditValid));

            Assert.Throws<ArgumentNullException>(() => NnrpObservability.PublishSessionOpenAck(ackMessage, null!));
            Assert.Throws<ArgumentNullException>(() => NnrpObservability.PublishSessionOpenAck(CreateRequest(SessionPriorityClass.Balanced), ackMessage, null!));
            Assert.Throws<ArgumentNullException>(() => NnrpObservability.PublishFlowUpdate(flowMessage, null!));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NnrpObservabilityEvent(
                (NnrpObservabilityEventKind)99,
                MessageType.FlowUpdate,
                sessionId: 0,
                frameId: 0,
                viewId: 0,
                routeId: 0,
                traceId: 0,
                operationId: 0,
                sessionDiagnostic: default,
                flowDiagnostic: default,
                failure: default));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NnrpObservabilityEvent(
                NnrpObservabilityEventKind.BackpressureTransition,
                (MessageType)0xFE,
                sessionId: 0,
                frameId: 0,
                viewId: 0,
                routeId: 0,
                traceId: 0,
                operationId: 0,
                sessionDiagnostic: default,
                flowDiagnostic: default,
                failure: default));
        }

        private static NnrpObservabilityEvent Clone(NnrpObservabilityEvent observation)
        {
            return new NnrpObservabilityEvent(
                observation.Kind,
                observation.MessageType,
                observation.SessionId,
                observation.FrameId,
                observation.ViewId,
                observation.RouteId,
                observation.TraceId,
                observation.OperationId,
                observation.SessionDiagnostic,
                observation.FlowDiagnostic,
                observation.Failure);
        }

        private static SessionOpenMetadata CreateRequest(SessionPriorityClass priorityClass)
        {
            return new SessionOpenMetadata(
                requestedSessionId: 41,
                profileId: 2,
                priorityClass: priorityClass,
                sessionFlags: SessionFlags.None,
                schemaId: 7,
                schemaVersion: 1,
                defaultDeadlineMilliseconds: 500,
                maxInFlightOperations: 4,
                leaseTtlHintMilliseconds: 1000,
                resumeTokenBytes: 0,
                authBytes: 0,
                sessionExtensionBytes: 0,
                clientSessionTag: 10);
        }

        private static SessionOpenAckMetadata CreateSessionOpenAck(
            SessionStatus status,
            SessionPriorityClass acceptedPriorityClass,
            SessionErrorCode errorCode,
            SessionAckFlags flags)
        {
            return new SessionOpenAckMetadata(
                sessionId: 41,
                acceptedProfileId: 2,
                acceptedPriorityClass: acceptedPriorityClass,
                sessionStatus: status,
                schemaId: 7,
                schemaVersion: 1,
                grantedOperationCredit: 3,
                maxInFlightOperations: 4,
                leaseTtlMilliseconds: 1000,
                resumeWindowMilliseconds: 2000,
                resumeTokenBytes: 0,
                sessionExtensionBytes: 0,
                serverSessionTag: 11,
                routeScopeId: 9,
                sessionErrorCode: errorCode,
                sessionFlagsAck: flags);
        }

        private static NnrpHeader CreateHeader(
            MessageType messageType,
            uint metaLength,
            uint bodyLength,
            uint sessionId)
        {
            return new NnrpHeader(
                versionMajor: NnrpHeader.CurrentVersionMajor,
                messageType: messageType,
                flags: HeaderFlags.None,
                metaLength: metaLength,
                bodyLength: bodyLength,
                sessionId: sessionId,
                frameId: 5,
                viewId: 6,
                routeId: 7,
                traceId: 8,
                wireFormat: NnrpHeader.CurrentWireFormat);
        }
    }
}
