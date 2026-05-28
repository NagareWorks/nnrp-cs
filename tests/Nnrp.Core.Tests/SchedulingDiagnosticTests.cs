using System;
using Nnrp.Core;
using Xunit;

namespace Nnrp.Core.Tests
{
    public sealed class SchedulingDiagnosticTests
    {
        [Fact]
        public void SessionOpenDiagnosticSurfacesDowngradeAndRequestContext()
        {
            var request = new SessionOpenMetadata(
                requestedSessionId: 41,
                profileId: 2,
                priorityClass: SessionPriorityClass.Interactive,
                sessionFlags: SessionFlags.AllowBackgroundResults,
                schemaId: 7,
                schemaVersion: 1,
                defaultDeadlineMilliseconds: 500,
                maxInFlightOperations: 4,
                leaseTtlHintMilliseconds: 1000,
                resumeTokenBytes: 0,
                authBytes: 0,
                sessionExtensionBytes: 0,
                clientSessionTag: 99);
            var ack = CreateSessionOpenAck(
                sessionStatus: SessionStatus.Opened,
                acceptedPriorityClass: SessionPriorityClass.Balanced,
                sessionErrorCode: SessionErrorCode.None,
                sessionFlagsAck: SessionAckFlags.BackgroundResultsEnabled | SessionAckFlags.PriorityDowngraded);

            var diagnostic = ack.GetDiagnostic(request);

            Assert.True(diagnostic.HasDiagnostic);
            Assert.True(diagnostic.IsPriorityDowngraded);
            Assert.False(diagnostic.ShouldRetryLater);
            Assert.False(diagnostic.IsRejected);
            Assert.False(diagnostic.HasSessionError);
            Assert.True(diagnostic.HasRequestedPriority);
            Assert.Equal(SessionPriorityClass.Interactive, diagnostic.RequestedPriorityClass);
            Assert.Equal(SessionPriorityClass.Balanced, diagnostic.AcceptedPriorityClass);
            Assert.Equal(41u, diagnostic.SessionId);
            Assert.Equal(9u, diagnostic.RouteScopeId);
            Assert.Equal(diagnostic, SessionOpenDiagnostic.FromAck(request, ack));
            Assert.Equal(diagnostic.GetHashCode(), SessionOpenDiagnostic.FromAck(request, ack).GetHashCode());
            Assert.True(diagnostic.Equals((object)SessionOpenDiagnostic.FromAck(request, ack)));
        }

        [Fact]
        public void SessionOpenDiagnosticSurfacesRetryAndErrorStates()
        {
            var retryAck = CreateSessionOpenAck(
                sessionStatus: SessionStatus.RetryLater,
                acceptedPriorityClass: SessionPriorityClass.Balanced,
                sessionErrorCode: SessionErrorCode.SessionLimitReached,
                sessionFlagsAck: SessionAckFlags.None);
            var rejectedAck = CreateSessionOpenAck(
                sessionStatus: SessionStatus.Rejected,
                acceptedPriorityClass: SessionPriorityClass.Balanced,
                sessionErrorCode: SessionErrorCode.PriorityRejected,
                sessionFlagsAck: SessionAckFlags.None);
            var cleanAck = CreateSessionOpenAck(
                sessionStatus: SessionStatus.Opened,
                acceptedPriorityClass: SessionPriorityClass.Balanced,
                sessionErrorCode: SessionErrorCode.None,
                sessionFlagsAck: SessionAckFlags.None);

            var retryDiagnostic = SessionOpenDiagnostic.FromAck(retryAck);
            var rejectedDiagnostic = rejectedAck.Diagnostic;
            var cleanDiagnostic = cleanAck.Diagnostic;

            Assert.True(retryDiagnostic.HasDiagnostic);
            Assert.True(retryDiagnostic.ShouldRetryLater);
            Assert.True(retryDiagnostic.HasSessionError);
            Assert.False(retryDiagnostic.HasRequestedPriority);
            Assert.Equal(default(SessionPriorityClass), retryDiagnostic.RequestedPriorityClass);
            Assert.True(rejectedDiagnostic.HasDiagnostic);
            Assert.True(rejectedDiagnostic.IsRejected);
            Assert.True(rejectedDiagnostic.HasSessionError);
            Assert.False(cleanDiagnostic.HasDiagnostic);
            Assert.False(cleanDiagnostic.IsPriorityDowngraded);
            Assert.False(cleanDiagnostic.ShouldRetryLater);
            Assert.False(cleanDiagnostic.HasSessionError);
            Assert.False(cleanDiagnostic.Equals("not-session-diagnostic"));
        }

        [Fact]
        public void SessionOpenMessageExposesDiagnostic()
        {
            var ack = CreateSessionOpenAck(
                sessionStatus: SessionStatus.RetryLater,
                acceptedPriorityClass: SessionPriorityClass.Background,
                sessionErrorCode: SessionErrorCode.SessionLimitReached,
                sessionFlagsAck: SessionAckFlags.None);
            var message = new SessionOpenAckMessage(
                CreateHeader(MessageType.SessionOpenAck, SessionOpenAckMetadata.MetadataLength, ack.BodyLength, ack.SessionId),
                ack,
                Array.Empty<byte>());
            var requestedDiagnostic = message.GetDiagnostic(CreateRequest(SessionPriorityClass.Interactive));

            Assert.True(message.Diagnostic.ShouldRetryLater);
            Assert.False(message.Diagnostic.HasRequestedPriority);
            Assert.True(requestedDiagnostic.ShouldRetryLater);
            Assert.True(requestedDiagnostic.HasRequestedPriority);
        }

        [Fact]
        public void SessionOpenDiagnosticRejectsInvalidValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SessionOpenDiagnostic(
                (SessionStatus)99,
                SessionErrorCode.None,
                SessionAckFlags.None,
                SessionPriorityClass.Balanced,
                hasRequestedPriority: false,
                requestedPriorityClass: default,
                sessionId: 0,
                routeScopeId: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SessionOpenDiagnostic(
                SessionStatus.Opened,
                (SessionErrorCode)0xFFFE,
                SessionAckFlags.None,
                SessionPriorityClass.Balanced,
                hasRequestedPriority: false,
                requestedPriorityClass: default,
                sessionId: 0,
                routeScopeId: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SessionOpenDiagnostic(
                SessionStatus.Opened,
                SessionErrorCode.None,
                SessionAckFlags.None,
                (SessionPriorityClass)99,
                hasRequestedPriority: false,
                requestedPriorityClass: default,
                sessionId: 0,
                routeScopeId: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SessionOpenDiagnostic(
                SessionStatus.Opened,
                SessionErrorCode.None,
                (SessionAckFlags)0x80,
                SessionPriorityClass.Balanced,
                hasRequestedPriority: false,
                requestedPriorityClass: default,
                sessionId: 0,
                routeScopeId: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SessionOpenDiagnostic(
                SessionStatus.Opened,
                SessionErrorCode.None,
                SessionAckFlags.None,
                SessionPriorityClass.Balanced,
                hasRequestedPriority: true,
                requestedPriorityClass: (SessionPriorityClass)99,
                sessionId: 0,
                routeScopeId: 0));
        }

        [Fact]
        public void FlowControlDiagnosticSurfacesRetryAndPauseSignals()
        {
            var creditUpdate = new FlowCreditUpdate(
                FlowUpdateScopeKind.Operation,
                sessionId: 41,
                operationId: 99,
                credit: 0,
                updateReason: FlowUpdateReason.Congestion,
                backpressureLevel: FlowUpdateBackpressureLevel.Hard,
                retryAfterMilliseconds: 50,
                creditEpoch: 7,
                flags: FlowUpdateFlags.CreditValid
                    | FlowUpdateFlags.RetryAfterValid
                    | FlowUpdateFlags.DrainInFlightOnly);
            var diagnostic = creditUpdate.Diagnostic;

            Assert.True(diagnostic.HasDiagnostic);
            Assert.True(diagnostic.HasCredit);
            Assert.True(diagnostic.HasRetryAfter);
            Assert.True(diagnostic.IsDrainInFlightOnly);
            Assert.False(diagnostic.IsBackgroundOnly);
            Assert.True(diagnostic.ShouldPauseNewWork);
            Assert.True(diagnostic.ShouldRetryLater);
            Assert.Equal(FlowUpdateScopeKind.Operation, diagnostic.ScopeKind);
            Assert.Equal(41u, diagnostic.SessionId);
            Assert.Equal((ulong)99, diagnostic.OperationId);
            Assert.Equal(0, diagnostic.Credit);
            Assert.Equal(FlowUpdateReason.Congestion, diagnostic.UpdateReason);
            Assert.Equal(FlowUpdateBackpressureLevel.Hard, diagnostic.BackpressureLevel);
            Assert.Equal(50u, diagnostic.RetryAfterMilliseconds);
            Assert.Equal(7u, diagnostic.CreditEpoch);
            Assert.Equal(diagnostic, FlowControlDiagnostic.FromCreditUpdate(creditUpdate));
            Assert.Equal(diagnostic.GetHashCode(), FlowControlDiagnostic.FromCreditUpdate(creditUpdate).GetHashCode());
            Assert.True(diagnostic.Equals((object)FlowControlDiagnostic.FromCreditUpdate(creditUpdate)));
        }

        [Fact]
        public void FlowControlDiagnosticDistinguishesCleanAndAdvisoryUpdates()
        {
            var clean = new FlowCreditUpdate(
                FlowUpdateScopeKind.Connection,
                sessionId: 0,
                operationId: 0,
                credit: 4,
                updateReason: FlowUpdateReason.Grant,
                backpressureLevel: FlowUpdateBackpressureLevel.None,
                retryAfterMilliseconds: 0,
                creditEpoch: 1,
                flags: FlowUpdateFlags.CreditValid);
            var advisory = new FlowCreditUpdate(
                FlowUpdateScopeKind.Session,
                sessionId: 41,
                operationId: 0,
                credit: 2,
                updateReason: FlowUpdateReason.Reduce,
                backpressureLevel: FlowUpdateBackpressureLevel.Soft,
                retryAfterMilliseconds: 0,
                creditEpoch: 2,
                flags: FlowUpdateFlags.CreditValid | FlowUpdateFlags.BackgroundOnly);

            Assert.False(clean.Diagnostic.HasDiagnostic);
            Assert.False(clean.Diagnostic.ShouldRetryLater);
            Assert.False(clean.Diagnostic.ShouldPauseNewWork);
            Assert.True(advisory.Diagnostic.HasDiagnostic);
            Assert.True(advisory.Diagnostic.IsBackgroundOnly);
            Assert.False(advisory.Diagnostic.ShouldPauseNewWork);
            Assert.False(advisory.Diagnostic.Equals("not-flow-diagnostic"));
        }

        [Fact]
        public void FlowUpdateMessageExposesDiagnostic()
        {
            var metadata = new FlowUpdateMetadata(
                FlowUpdateScopeKind.Session,
                FlowUpdateReason.Pause,
                FlowUpdateBackpressureLevel.Hard,
                connectionCredit: 0,
                sessionCredit: 0,
                operationCredit: 0,
                operationId: 0,
                retryAfterMilliseconds: 0,
                creditEpoch: 4,
                flags: FlowUpdateFlags.CreditValid);
            var message = new FlowUpdateMessage(
                CreateHeader(MessageType.FlowUpdate, FlowUpdateMetadata.MetadataLength, 0, sessionId: 41),
                metadata);

            Assert.True(message.Diagnostic.ShouldPauseNewWork);
            Assert.False(message.Diagnostic.ShouldRetryLater);
            Assert.Equal(FlowUpdateReason.Pause, message.Diagnostic.UpdateReason);
        }

        private static SessionOpenMetadata CreateRequest(SessionPriorityClass priorityClass)
        {
            return new SessionOpenMetadata(
                requestedSessionId: 41,
                profileId: 2,
                priorityClass: priorityClass,
                sessionFlags: SessionFlags.None,
                schemaId: 0,
                schemaVersion: 0,
                defaultDeadlineMilliseconds: 500,
                maxInFlightOperations: 4,
                leaseTtlHintMilliseconds: 0,
                resumeTokenBytes: 0,
                authBytes: 0,
                sessionExtensionBytes: 0,
                clientSessionTag: 0);
        }

        private static SessionOpenAckMetadata CreateSessionOpenAck(
            SessionStatus sessionStatus,
            SessionPriorityClass acceptedPriorityClass,
            SessionErrorCode sessionErrorCode,
            SessionAckFlags sessionFlagsAck)
        {
            return new SessionOpenAckMetadata(
                sessionId: 41,
                acceptedProfileId: 2,
                acceptedPriorityClass: acceptedPriorityClass,
                sessionStatus: sessionStatus,
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
                sessionErrorCode: sessionErrorCode,
                sessionFlagsAck: sessionFlagsAck);
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
                frameId: 0,
                viewId: 0,
                routeId: 1,
                traceId: 2,
                wireFormat: NnrpHeader.CurrentWireFormat);
        }
    }
}
