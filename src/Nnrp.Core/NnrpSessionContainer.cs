using System;
using System.Collections.Generic;
using System.Linq;

namespace Nnrp.Core
{
    public sealed class NnrpSessionContainer
    {
        private readonly Dictionary<uint, NnrpSessionStateMachine> sessions = new Dictionary<uint, NnrpSessionStateMachine>();
        private bool connectionClosed;

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
                return false;
            }

            if (sessionId == 0)
            {
                failure = NnrpProtocolFailure.InvalidState(
                    NnrpErrorScope.Session,
                    "Session id must be non-zero.");
                return false;
            }

            if (sessions.ContainsKey(sessionId))
            {
                failure = NnrpProtocolFailure.InvalidState(
                    NnrpErrorScope.Session,
                    $"Session {sessionId} is already registered.");
                return false;
            }

            var session = new NnrpSessionStateMachine();
            if (!session.TryBeginNegotiation(out failure) || !session.TryActivate(out failure))
            {
                return false;
            }

            sessions.Add(sessionId, session);
            failure = NnrpProtocolFailure.None;
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
                return false;
            }

            if (session.State == NnrpSessionState.Closed)
            {
                failure = NnrpProtocolFailure.InvalidState(
                    NnrpErrorScope.Session,
                    $"Session {sessionId} is already closed.");
                return false;
            }

            return TryCloseSession(session, out failure);
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
    }
}
