using System;
using System.Linq;
using Xunit;

namespace Nnrp.Core.Tests
{
    public sealed class ConnectionLifecycleTests
    {
        [Fact]
        public void OpenAcknowledgementsInstallIndependentOrderedSessionsAndSnapshots()
        {
            var connection = new NnrpConnectionLifecycle();

            Assert.True(connection.TryApplySessionOpenAck(OpenAck(43), out var failure));
            Assert.Equal(NnrpProtocolFailure.None, failure);
            Assert.True(connection.TryApplySessionOpenAck(OpenAck(42, SessionStatus.Resumed), out failure));
            Assert.Equal(new uint[] { 42, 43 }, connection.Sessions.Select(session => session.SessionId));
            Assert.Equal(2, connection.SessionCount);
            Assert.True(connection.TryGetSession(42, out var resumed));
            Assert.Equal(NnrpSessionLifecycleState.Resumed, resumed!.State);
            Assert.True(resumed.AcceptsNewOperations);
            Assert.True(resumed.AcceptsSessionScopedMessages);
            Assert.False(connection.TryGetSession(99, out var missing));
            Assert.Null(missing);

            var snapshot = connection.Snapshot();
            Assert.True(connection.TryBeginSessionClose(
                Header(MessageType.SessionClose, 42, SessionCloseMetadata.MetadataLength),
                Close(lastOperationId: 7),
                out failure));
            Assert.Equal(NnrpSessionLifecycleState.Resumed, snapshot.Sessions[0].State);
            Assert.Equal(NnrpSessionLifecycleState.Closing, connection.Sessions[0].State);
            Assert.False(connection.Sessions[0].AcceptsNewOperations);
            Assert.True(connection.Sessions[0].AcceptsSessionScopedMessages);
        }

        [Fact]
        public void OpenAcknowledgementsRejectInvalidCombinationsAndDuplicates()
        {
            var connection = new NnrpConnectionLifecycle();

            Assert.False(connection.TryApplySessionOpenAck(OpenAck(0), out var failure));
            AssertFailure(failure, NnrpErrorScope.Session);
            Assert.True(connection.TryApplySessionOpenAck(OpenAck(42), out failure));
            Assert.False(connection.TryApplySessionOpenAck(OpenAck(42), out failure));
            AssertFailure(failure, NnrpErrorScope.Session);

            Assert.True(connection.TryApplySessionOpenAck(OpenAck(0, SessionStatus.Rejected), out failure));
            Assert.Equal(1, connection.SessionCount);
            Assert.True(connection.TryApplySessionOpenAck(OpenAck(0, SessionStatus.RetryLater), out failure));
            Assert.False(connection.TryApplySessionOpenAck(OpenAck(9, SessionStatus.Rejected), out failure));
            AssertFailure(failure, NnrpErrorScope.Session);
        }

        [Fact]
        public void CloseAcknowledgementsMoveOnlyTheTargetSessionAcrossEveryState()
        {
            var connection = OpenConnection(42, 43);
            var closeHeader = Header(MessageType.SessionClose, 42, SessionCloseMetadata.MetadataLength);

            Assert.True(connection.TryBeginSessionClose(closeHeader, Close(7), out var failure));
            Assert.Equal((ulong)7, connection.Sessions[0].LastOperationId);
            Assert.Equal(NnrpSessionLifecycleState.Open, connection.Sessions[1].State);

            var ackHeader = Header(MessageType.SessionCloseAck, 42, SessionCloseAckMetadata.MetadataLength);
            Assert.True(connection.TryApplySessionCloseAck(
                ackHeader,
                CloseAck(SessionCloseStatus.Draining, 8),
                out failure));
            Assert.Equal(NnrpSessionLifecycleState.Draining, connection.Sessions[0].State);
            Assert.True(connection.TryApplySessionCloseAck(
                ackHeader,
                CloseAck(SessionCloseStatus.Rejected, 9),
                out failure));
            Assert.Equal(NnrpSessionLifecycleState.Open, connection.Sessions[0].State);
            Assert.True(connection.TryApplySessionCloseAck(
                ackHeader,
                CloseAck(SessionCloseStatus.Acknowledged, 10),
                out failure));
            Assert.Equal(NnrpSessionLifecycleState.Closing, connection.Sessions[0].State);
            Assert.True(connection.TryApplySessionCloseAck(
                ackHeader,
                CloseAck(SessionCloseStatus.Closed, 11),
                out failure));
            Assert.Equal(NnrpSessionLifecycleState.Closed, connection.Sessions[0].State);
            Assert.Equal((ulong)11, connection.Sessions[0].LastOperationId);
            Assert.False(connection.Sessions[0].AcceptsSessionScopedMessages);
        }

        [Fact]
        public void RejectedCloseRestoresTheEstablishedSessionState()
        {
            var connection = new NnrpConnectionLifecycle();
            Assert.True(connection.TryApplySessionOpenAck(OpenAck(42, SessionStatus.Resumed), out var failure));
            Assert.True(connection.TryBeginSessionClose(
                Header(MessageType.SessionClose, 42, SessionCloseMetadata.MetadataLength),
                Close(7),
                out failure));

            Assert.True(connection.TryApplySessionCloseAck(
                Header(MessageType.SessionCloseAck, 42, SessionCloseAckMetadata.MetadataLength),
                CloseAck(SessionCloseStatus.Rejected, 7),
                out failure));
            Assert.Equal(NnrpSessionLifecycleState.Resumed, connection.Sessions[0].State);
            Assert.True(connection.Sessions[0].AcceptsNewOperations);
        }

        [Fact]
        public void CloseTransitionsRejectWrongHeadersUnknownSessionsAndClosedSessions()
        {
            var connection = OpenConnection(42);

            Assert.False(connection.TryBeginSessionClose(
                Header(MessageType.Ping, 42, SessionCloseMetadata.MetadataLength),
                Close(0),
                out var failure));
            AssertFailure(failure, NnrpErrorScope.Session);
            Assert.False(connection.TryBeginSessionClose(
                Header(MessageType.SessionClose, 0, SessionCloseMetadata.MetadataLength),
                Close(0),
                out failure));
            Assert.False(connection.TryBeginSessionClose(
                Header(MessageType.SessionClose, 9, SessionCloseMetadata.MetadataLength),
                Close(0),
                out failure));

            Assert.False(connection.TryApplySessionCloseAck(
                Header(MessageType.Ping, 42, SessionCloseAckMetadata.MetadataLength),
                CloseAck(SessionCloseStatus.Closed, 0),
                out failure));
            Assert.False(connection.TryApplySessionCloseAck(
                Header(MessageType.SessionCloseAck, 0, SessionCloseAckMetadata.MetadataLength),
                CloseAck(SessionCloseStatus.Closed, 0),
                out failure));
            Assert.False(connection.TryApplySessionCloseAck(
                Header(MessageType.SessionCloseAck, 9, SessionCloseAckMetadata.MetadataLength),
                CloseAck(SessionCloseStatus.Closed, 0),
                out failure));

            var ackHeader = Header(MessageType.SessionCloseAck, 42, SessionCloseAckMetadata.MetadataLength);
            Assert.True(connection.TryApplySessionCloseAck(
                ackHeader,
                CloseAck(SessionCloseStatus.Closed, 0),
                out failure));
            Assert.False(connection.TryBeginSessionClose(
                Header(MessageType.SessionClose, 42, SessionCloseMetadata.MetadataLength),
                Close(0),
                out failure));
            Assert.False(connection.TryApplySessionCloseAck(
                ackHeader,
                CloseAck(SessionCloseStatus.Closed, 0),
                out failure));
        }

        [Fact]
        public void FlowRoutingAndConnectionCloseEnforceLifecycleScope()
        {
            var connection = OpenConnection(42);
            var connectionFlow = Flow(FlowUpdateScopeKind.Connection);
            var sessionFlow = Flow(FlowUpdateScopeKind.Session);
            var operationFlow = Flow(FlowUpdateScopeKind.Operation);

            Assert.False(connection.TryValidateFlowUpdate(
                Header(MessageType.Ping, 0, FlowUpdateMetadata.MetadataLength),
                connectionFlow,
                out var failure));
            Assert.False(connection.TryValidateFlowUpdate(
                Header(MessageType.FlowUpdate, 42, FlowUpdateMetadata.MetadataLength),
                connectionFlow,
                out failure));
            Assert.False(connection.TryValidateFlowUpdate(
                Header(MessageType.FlowUpdate, 0, FlowUpdateMetadata.MetadataLength),
                sessionFlow,
                out failure));
            Assert.True(connection.TryValidateFlowUpdate(
                Header(MessageType.FlowUpdate, 0, FlowUpdateMetadata.MetadataLength),
                connectionFlow,
                out failure));
            Assert.True(connection.TryValidateFlowUpdate(
                Header(MessageType.FlowUpdate, 42, FlowUpdateMetadata.MetadataLength),
                sessionFlow,
                out failure));
            Assert.True(connection.TryValidateFlowUpdate(
                Header(MessageType.FlowUpdate, 42, FlowUpdateMetadata.MetadataLength),
                operationFlow,
                out failure));
            Assert.False(connection.TryValidateFlowUpdate(
                Header(MessageType.FlowUpdate, 9, FlowUpdateMetadata.MetadataLength),
                sessionFlow,
                out failure));

            Assert.True(connection.TryCloseConnection(out failure));
            Assert.Equal(NnrpConnectionLifecycleState.Closed, connection.State);
            Assert.All(connection.Sessions, session => Assert.Equal(NnrpSessionLifecycleState.Closed, session.State));
            Assert.False(connection.TryCloseConnection(out failure));
            AssertFailure(failure, NnrpErrorScope.Connection);
            Assert.False(connection.TryApplySessionOpenAck(OpenAck(44), out failure));
            AssertFailure(failure, NnrpErrorScope.Connection);
            Assert.False(connection.TryValidateFlowUpdate(
                Header(MessageType.FlowUpdate, 0, FlowUpdateMetadata.MetadataLength),
                connectionFlow,
                out failure));
        }

        private static NnrpConnectionLifecycle OpenConnection(params uint[] sessionIds)
        {
            var connection = new NnrpConnectionLifecycle();
            foreach (var sessionId in sessionIds)
            {
                Assert.True(connection.TryApplySessionOpenAck(OpenAck(sessionId), out _));
            }

            return connection;
        }

        private static SessionOpenAckMetadata OpenAck(
            uint sessionId,
            SessionStatus status = SessionStatus.Opened)
        {
            return new SessionOpenAckMetadata(
                sessionId,
                acceptedProfileId: 2,
                SessionPriorityClass.Balanced,
                status,
                schemaId: 0x1001,
                schemaVersion: 3,
                grantedOperationCredit: 2,
                maxInFlightOperations: 4,
                leaseTtlMilliseconds: 30_000,
                resumeWindowMilliseconds: 120_000,
                resumeTokenBytes: 16,
                sessionExtensionBytes: 0,
                serverSessionTag: 9,
                routeScopeId: 7,
                status == SessionStatus.Rejected ? SessionErrorCode.ProfileUnsupported : SessionErrorCode.None,
                SessionAckFlags.None);
        }

        private static SessionCloseMetadata Close(ulong lastOperationId)
        {
            return new SessionCloseMetadata(
                SessionCloseReason.ClientShutdown,
                InFlightPolicy.Drain,
                drainTimeoutMilliseconds: 1_000,
                lastOperationId,
                SessionErrorCode.None,
                sessionCloseTag: 1);
        }

        private static SessionCloseAckMetadata CloseAck(SessionCloseStatus status, ulong lastOperationId)
        {
            return new SessionCloseAckMetadata(status, lastOperationId, SessionErrorCode.None);
        }

        private static FlowUpdateMetadata Flow(FlowUpdateScopeKind scope)
        {
            return new FlowUpdateMetadata(
                scope,
                FlowUpdateReason.Grant,
                FlowUpdateBackpressureLevel.None,
                connectionCredit: scope == FlowUpdateScopeKind.Connection ? (ushort)1 : (ushort)0,
                sessionCredit: scope == FlowUpdateScopeKind.Session ? (ushort)1 : (ushort)0,
                operationCredit: scope == FlowUpdateScopeKind.Operation ? (ushort)1 : (ushort)0,
                operationId: scope == FlowUpdateScopeKind.Operation ? 7ul : 0ul,
                retryAfterMilliseconds: 0,
                creditEpoch: 1,
                FlowUpdateFlags.CreditValid);
        }

        private static NnrpHeader Header(MessageType type, uint sessionId, uint metadataLength)
        {
            return new NnrpHeader(
                versionMajor: 1,
                type,
                HeaderFlags.None,
                metadataLength,
                bodyLength: 0,
                sessionId,
                frameId: 1,
                viewId: 0,
                routeId: 0,
                traceId: 1);
        }

        private static void AssertFailure(NnrpProtocolFailure failure, NnrpErrorScope scope)
        {
            Assert.True(failure.IsFailure);
            Assert.Equal(ErrorCode.InvalidState, failure.ErrorCode);
            Assert.Equal(scope, failure.Scope);
        }
    }
}
