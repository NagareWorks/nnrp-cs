using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Nnrp.Core
{
    public enum NnrpConnectionLifecycleState : byte
    {
        Open = 0,
        Closing = 1,
        Closed = 2,
    }

    public enum NnrpSessionLifecycleState : byte
    {
        Open = 0,
        Resumed = 1,
        Closing = 2,
        Draining = 3,
        Closed = 4,
    }

    public sealed class NnrpSessionLifecycle
    {
        internal NnrpSessionLifecycle(
            uint sessionId,
            NnrpSessionLifecycleState state,
            NnrpSessionLifecycleState establishedState,
            ushort profileId,
            SessionPriorityClass priorityClass,
            uint schemaId,
            uint schemaVersion,
            ushort maxInFlightOperations,
            uint routeScopeId,
            ulong lastOperationId,
            SessionErrorCode sessionErrorCode)
        {
            SessionId = sessionId;
            State = state;
            EstablishedState = establishedState;
            ProfileId = profileId;
            PriorityClass = priorityClass;
            SchemaId = schemaId;
            SchemaVersion = schemaVersion;
            MaxInFlightOperations = maxInFlightOperations;
            RouteScopeId = routeScopeId;
            LastOperationId = lastOperationId;
            SessionErrorCode = sessionErrorCode;
        }

        public uint SessionId { get; }

        public NnrpSessionLifecycleState State { get; internal set; }

        public ushort ProfileId { get; }

        public SessionPriorityClass PriorityClass { get; }

        public uint SchemaId { get; }

        public uint SchemaVersion { get; }

        public ushort MaxInFlightOperations { get; }

        public uint RouteScopeId { get; }

        public ulong LastOperationId { get; internal set; }

        public SessionErrorCode SessionErrorCode { get; internal set; }

        public bool AcceptsSessionScopedMessages => State == NnrpSessionLifecycleState.Open
            || State == NnrpSessionLifecycleState.Resumed
            || State == NnrpSessionLifecycleState.Closing
            || State == NnrpSessionLifecycleState.Draining;

        public bool AcceptsNewOperations => State == NnrpSessionLifecycleState.Open
            || State == NnrpSessionLifecycleState.Resumed;

        internal NnrpSessionLifecycleState EstablishedState { get; }

        internal NnrpSessionLifecycle Snapshot()
        {
            return new NnrpSessionLifecycle(
                SessionId,
                State,
                EstablishedState,
                ProfileId,
                PriorityClass,
                SchemaId,
                SchemaVersion,
                MaxInFlightOperations,
                RouteScopeId,
                LastOperationId,
                SessionErrorCode);
        }
    }

    public sealed class NnrpConnectionLifecycle
    {
        private readonly SortedDictionary<uint, NnrpSessionLifecycle> sessions =
            new SortedDictionary<uint, NnrpSessionLifecycle>();

        public NnrpConnectionLifecycle()
        {
            State = NnrpConnectionLifecycleState.Open;
        }

        public NnrpConnectionLifecycleState State { get; private set; }

        public int SessionCount => sessions.Count;

        public IReadOnlyList<NnrpSessionLifecycle> Sessions =>
            new ReadOnlyCollection<NnrpSessionLifecycle>(sessions.Values.Select(value => value.Snapshot()).ToArray());

        public bool TryGetSession(uint sessionId, out NnrpSessionLifecycle? session)
        {
            if (sessions.TryGetValue(sessionId, out var value))
            {
                session = value.Snapshot();
                return true;
            }

            session = null;
            return false;
        }

        public NnrpConnectionLifecycle Snapshot()
        {
            var snapshot = new NnrpConnectionLifecycle { State = State };
            foreach (var pair in sessions)
            {
                snapshot.sessions.Add(pair.Key, pair.Value.Snapshot());
            }

            return snapshot;
        }

        public bool TryCloseConnection(out NnrpProtocolFailure failure)
        {
            if (State == NnrpConnectionLifecycleState.Closed)
            {
                failure = NnrpProtocolFailure.InvalidState(
                    NnrpErrorScope.Connection,
                    "Connection is already closed.");
                return false;
            }

            State = NnrpConnectionLifecycleState.Closed;
            foreach (var session in sessions.Values)
            {
                session.State = NnrpSessionLifecycleState.Closed;
            }

            failure = NnrpProtocolFailure.None;
            return true;
        }

        public bool TryApplySessionOpenAck(
            SessionOpenAckMetadata acknowledgement,
            out NnrpProtocolFailure failure)
        {
            if (!TryRequireOpen(out failure))
            {
                return false;
            }

            if (acknowledgement.SessionStatus == SessionStatus.Rejected
                || acknowledgement.SessionStatus == SessionStatus.RetryLater)
            {
                if (acknowledgement.SessionId != 0)
                {
                    return RejectSession("Rejected SESSION_OPEN_ACK must not install a session id.", out failure);
                }

                failure = NnrpProtocolFailure.None;
                return true;
            }

            if (acknowledgement.SessionId == 0)
            {
                return RejectSession("Successful SESSION_OPEN_ACK requires a non-zero session id.", out failure);
            }

            if (sessions.ContainsKey(acknowledgement.SessionId))
            {
                return RejectSession($"Session {acknowledgement.SessionId} already exists.", out failure);
            }

            sessions.Add(
                acknowledgement.SessionId,
                new NnrpSessionLifecycle(
                    acknowledgement.SessionId,
                    acknowledgement.SessionStatus == SessionStatus.Opened
                        ? NnrpSessionLifecycleState.Open
                        : NnrpSessionLifecycleState.Resumed,
                    acknowledgement.SessionStatus == SessionStatus.Opened
                        ? NnrpSessionLifecycleState.Open
                        : NnrpSessionLifecycleState.Resumed,
                    acknowledgement.AcceptedProfileId,
                    acknowledgement.AcceptedPriorityClass,
                    acknowledgement.SchemaId,
                    acknowledgement.SchemaVersion,
                    acknowledgement.MaxInFlightOperations,
                    acknowledgement.RouteScopeId,
                    lastOperationId: 0,
                    acknowledgement.SessionErrorCode));
            failure = NnrpProtocolFailure.None;
            return true;
        }

        public bool TryBeginSessionClose(
            NnrpHeader header,
            SessionCloseMetadata close,
            out NnrpProtocolFailure failure)
        {
            if (!TryRequireOpen(out failure))
            {
                return false;
            }

            if (header.MessageType != MessageType.SessionClose || header.SessionId == 0)
            {
                return RejectSession("SESSION_CLOSE lifecycle transition requires a session-scoped SESSION_CLOSE header.", out failure);
            }

            if (!sessions.TryGetValue(header.SessionId, out var session) || !session.AcceptsNewOperations)
            {
                return RejectSession($"Session {header.SessionId} is not open.", out failure);
            }

            session.State = NnrpSessionLifecycleState.Closing;
            session.LastOperationId = close.LastOperationId;
            session.SessionErrorCode = close.SessionErrorCode;
            failure = NnrpProtocolFailure.None;
            return true;
        }

        public bool TryApplySessionCloseAck(
            NnrpHeader header,
            SessionCloseAckMetadata acknowledgement,
            out NnrpProtocolFailure failure)
        {
            if (!TryRequireOpen(out failure))
            {
                return false;
            }

            if (header.MessageType != MessageType.SessionCloseAck || header.SessionId == 0)
            {
                return RejectSession("SESSION_CLOSE_ACK lifecycle transition requires a session-scoped SESSION_CLOSE_ACK header.", out failure);
            }

            if (!sessions.TryGetValue(header.SessionId, out var session) || !session.AcceptsSessionScopedMessages)
            {
                return RejectSession($"Session {header.SessionId} is not open.", out failure);
            }

            switch (acknowledgement.CloseStatus)
            {
                case SessionCloseStatus.Acknowledged:
                    session.State = NnrpSessionLifecycleState.Closing;
                    break;
                case SessionCloseStatus.Draining:
                    session.State = NnrpSessionLifecycleState.Draining;
                    break;
                case SessionCloseStatus.Closed:
                    session.State = NnrpSessionLifecycleState.Closed;
                    break;
                case SessionCloseStatus.Rejected:
                    session.State = session.EstablishedState;
                    break;
                default:
                    return RejectSession("SESSION_CLOSE_ACK contains an unknown close status.", out failure);
            }

            session.LastOperationId = acknowledgement.LastOperationId;
            session.SessionErrorCode = acknowledgement.SessionErrorCode;
            failure = NnrpProtocolFailure.None;
            return true;
        }

        public bool TryValidateFlowUpdate(
            NnrpHeader header,
            FlowUpdateMetadata metadata,
            out NnrpProtocolFailure failure)
        {
            if (!TryRequireOpen(out failure))
            {
                return false;
            }

            if (header.MessageType != MessageType.FlowUpdate)
            {
                return RejectSession("FLOW_UPDATE validation requires a FLOW_UPDATE header.", out failure);
            }

            if (metadata.ScopeKind == FlowUpdateScopeKind.Connection)
            {
                if (header.SessionId != 0)
                {
                    return RejectSession("Connection-scoped FLOW_UPDATE requires header.session_id == 0.", out failure);
                }

                failure = NnrpProtocolFailure.None;
                return true;
            }

            if (header.SessionId == 0)
            {
                return RejectSession("Session- or operation-scoped FLOW_UPDATE requires header.session_id != 0.", out failure);
            }

            if (sessions.TryGetValue(header.SessionId, out var session)
                && session.AcceptsSessionScopedMessages)
            {
                failure = NnrpProtocolFailure.None;
                return true;
            }

            return RejectSession($"FLOW_UPDATE references unknown session {header.SessionId}.", out failure);
        }

        private bool TryRequireOpen(out NnrpProtocolFailure failure)
        {
            if (State == NnrpConnectionLifecycleState.Open)
            {
                failure = NnrpProtocolFailure.None;
                return true;
            }

            failure = NnrpProtocolFailure.InvalidState(
                NnrpErrorScope.Connection,
                "Connection is not open.");
            return false;
        }

        private static bool RejectSession(string message, out NnrpProtocolFailure failure)
        {
            failure = NnrpProtocolFailure.InvalidState(NnrpErrorScope.Session, message);
            return false;
        }
    }
}
