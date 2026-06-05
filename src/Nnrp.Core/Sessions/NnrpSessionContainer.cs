using System;
using System.Collections.Generic;
using System.Linq;

namespace Nnrp.Core
{
    public sealed class NnrpSessionContainer
    {
        private readonly Dictionary<uint, NnrpSessionStateMachine> sessions = new Dictionary<uint, NnrpSessionStateMachine>();
        private readonly NnrpObservabilityHook? observabilityHook;
        private bool connectionClosed;

        public NnrpSessionContainer(NnrpObservabilityHook? observabilityHook = null)
        {
            this.observabilityHook = observabilityHook;
        }

        public int SessionCount => sessions.Count;

        public bool IsConnectionClosed => connectionClosed;

        public IReadOnlyCollection<uint> SessionIds => sessions.Keys.ToArray();

        public bool TryOpenSession(uint sessionId, out NnrpProtocolFailure failure)
        {
            if (connectionClosed)
            {
                failure = NnrpProtocolFailure.InvalidState(
                    NnrpErrorScope.Connection,
                    "Cannot open a session after the connection has closed.",
                    isFatal: true);
                PublishRoute(NnrpObservabilityEventKind.SessionRouteRejected, MessageType.SessionOpen, sessionId, failure);
                return false;
            }

            if (sessionId == 0)
            {
                failure = NnrpProtocolFailure.InvalidState(
                    NnrpErrorScope.Session,
                    "Session id must be non-zero.");
                PublishRoute(NnrpObservabilityEventKind.SessionRouteRejected, MessageType.SessionOpen, sessionId, failure);
                return false;
            }

            if (sessions.ContainsKey(sessionId))
            {
                failure = NnrpProtocolFailure.InvalidState(
                    NnrpErrorScope.Session,
                    $"Session {sessionId} is already registered.");
                PublishRoute(NnrpObservabilityEventKind.SessionRouteRejected, MessageType.SessionOpen, sessionId, failure);
                return false;
            }

            var session = new NnrpSessionStateMachine();
            if (!session.TryBeginNegotiation(out failure) || !session.TryActivate(out failure))
            {
                PublishRoute(NnrpObservabilityEventKind.SessionRouteRejected, MessageType.SessionOpen, sessionId, failure);
                return false;
            }

            sessions.Add(sessionId, session);
            failure = NnrpProtocolFailure.None;
            PublishRoute(NnrpObservabilityEventKind.SessionRouteOpened, MessageType.SessionOpen, sessionId, failure);
            return true;
        }

        public bool TryGetSessionState(uint sessionId, out NnrpSessionState state)
        {
            if (sessions.TryGetValue(sessionId, out var session))
            {
                state = session.State;
                return true;
            }

            state = default;
            return false;
        }

        public bool TryAcceptFrameSubmit(uint sessionId, out NnrpProtocolFailure failure)
        {
            var accepted = TryAcceptFrameSubmitCore(sessionId, out failure);
            PublishRoute(
                accepted ? NnrpObservabilityEventKind.SessionRouteAccepted : NnrpObservabilityEventKind.SessionRouteRejected,
                MessageType.FrameSubmit,
                sessionId,
                failure);
            return accepted;
        }

        public bool TryAcceptFrameSubmit(FrameSubmitMessage message, out NnrpProtocolFailure failure)
        {
            var accepted = TryAcceptFrameSubmitCore(message.Header.SessionId, out failure);
            PublishRoute(
                accepted ? NnrpObservabilityEventKind.SessionRouteAccepted : NnrpObservabilityEventKind.SessionRouteRejected,
                message.Header,
                failure);
            return accepted;
        }

        private bool TryAcceptFrameSubmitCore(uint sessionId, out NnrpProtocolFailure failure)
        {
            if (connectionClosed)
            {
                failure = NnrpProtocolFailure.InvalidState(
                    NnrpErrorScope.Connection,
                    "FRAME_SUBMIT cannot be accepted after the connection has closed.",
                    isFatal: true);
                return false;
            }

            if (!sessions.TryGetValue(sessionId, out var session))
            {
                failure = NnrpProtocolFailure.InvalidState(
                    NnrpErrorScope.Session,
                    $"Session {sessionId} is not registered.");
                return false;
            }

            return session.TryAcceptFrameSubmit(out failure);
        }

        public bool TryCloseSession(uint sessionId, out NnrpProtocolFailure failure)
        {
            if (!sessions.TryGetValue(sessionId, out var session))
            {
                failure = NnrpProtocolFailure.InvalidState(
                    NnrpErrorScope.Session,
                    $"Session {sessionId} is not registered.");
                PublishRoute(NnrpObservabilityEventKind.SessionRouteRejected, MessageType.SessionClose, sessionId, failure);
                return false;
            }

            if (session.State == NnrpSessionState.Closed)
            {
                failure = NnrpProtocolFailure.InvalidState(
                    NnrpErrorScope.Session,
                    $"Session {sessionId} is already closed.");
                PublishRoute(NnrpObservabilityEventKind.SessionRouteRejected, MessageType.SessionClose, sessionId, failure);
                return false;
            }

            var closed = TryCloseSession(session, out failure);
            PublishRoute(
                closed ? NnrpObservabilityEventKind.SessionRouteClosed : NnrpObservabilityEventKind.SessionRouteRejected,
                MessageType.SessionClose,
                sessionId,
                failure);
            return closed;
        }

        public IReadOnlyList<uint> CloseConnection()
        {
            if (connectionClosed)
            {
                return Array.Empty<uint>();
            }

            var closedSessionIds = new List<uint>();
            foreach (var pair in sessions.OrderBy(pair => pair.Key))
            {
                if (pair.Value.State == NnrpSessionState.Closed)
                {
                    continue;
                }

                if (TryCloseSession(pair.Value, out _))
                {
                    closedSessionIds.Add(pair.Key);
                    PublishRoute(NnrpObservabilityEventKind.SessionRouteClosed, MessageType.Close, pair.Key, NnrpProtocolFailure.None);
                }
            }

            connectionClosed = true;
            return closedSessionIds;
        }

        private static bool TryCloseSession(NnrpSessionStateMachine session, out NnrpProtocolFailure failure)
        {
            while (session.State != NnrpSessionState.Closed)
            {
                if (!session.TryClose(out failure))
                {
                    return false;
                }
            }

            failure = NnrpProtocolFailure.None;
            return true;
        }

        private void PublishRoute(
            NnrpObservabilityEventKind kind,
            MessageType messageType,
            uint sessionId,
            NnrpProtocolFailure failure)
        {
            if (observabilityHook == null)
            {
                return;
            }

            observabilityHook(new NnrpObservabilityEvent(
                kind,
                messageType,
                sessionId,
                frameId: 0,
                viewId: 0,
                routeId: 0,
                traceId: 0,
                operationId: 0,
                sessionDiagnostic: default,
                flowDiagnostic: default,
                failure: failure));
        }

        private void PublishRoute(NnrpObservabilityEventKind kind, NnrpHeader header, NnrpProtocolFailure failure)
        {
            if (observabilityHook == null)
            {
                return;
            }

            observabilityHook(new NnrpObservabilityEvent(
                kind,
                header.MessageType,
                header.SessionId,
                header.FrameId,
                header.ViewId,
                header.RouteId,
                header.TraceId,
                operationId: 0,
                sessionDiagnostic: default,
                flowDiagnostic: default,
                failure: failure));
        }
    }
}
