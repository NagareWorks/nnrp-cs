using System;
using System.Collections.Generic;

namespace Nnrp.NativeBridge
{
    public sealed class NnrpNativeRuntimeSessionHostOptions
    {
        public NnrpNativeRuntimeSessionHostOptions(
            ulong connectionId,
            uint connectionGeneration,
            uint transportId,
            uint sessionId,
            uint sessionGeneration,
            ushort profileId,
            uint schemaId,
            uint schemaVersion)
        {
            ConnectionId = connectionId;
            ConnectionGeneration = connectionGeneration;
            TransportId = transportId;
            SessionId = sessionId;
            SessionGeneration = sessionGeneration;
            ProfileId = profileId;
            SchemaId = schemaId;
            SchemaVersion = schemaVersion;
        }

        public ulong ConnectionId { get; }

        public uint ConnectionGeneration { get; }

        public uint TransportId { get; }

        public uint SessionId { get; }

        public uint SessionGeneration { get; }

        public ushort ProfileId { get; }

        public uint SchemaId { get; }

        public uint SchemaVersion { get; }

        public bool BootstrapConnection { get; set; }

        public string? ArtifactPath { get; set; }

        public string? ArtifactRoot { get; set; }

        public NnrpNativePlatform? Platform { get; set; }

        public INnrpNativeRuntimeBackend? FallbackBackend { get; set; }

        public bool RequireNative { get; set; }
    }

    public sealed class NnrpNativeRuntimeSessionHost : IDisposable
    {
        private NnrpNativeRuntimeSessionHost(
            INnrpNativeRuntimeBackend backend,
            NnrpNativeRuntimeSessionHostOptions options,
            NnrpNativeRuntimeConnection connection,
            NnrpNativeRuntimeSession session)
        {
            Backend = backend;
            Options = options;
            Connection = connection;
            Session = session;
        }

        public INnrpNativeRuntimeBackend Backend { get; }

        public NnrpNativeRuntimeSessionHostOptions Options { get; }

        public NnrpNativeRuntimeConnection Connection { get; }

        public NnrpNativeRuntimeSession Session { get; }

        public bool IsClosed { get; private set; }

        public static NnrpNativeRuntimeSessionHost Open(NnrpNativeRuntimeSessionHostOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            var backend = NnrpNativeRuntimeBackendSelector.Select(
                options.ArtifactPath,
                options.ArtifactRoot,
                options.Platform,
                options.FallbackBackend,
                options.RequireNative);
            return Open(backend, options);
        }

        public static NnrpNativeRuntimeSessionHost Open(
            INnrpNativeRuntimeBackend backend,
            NnrpNativeRuntimeSessionHostOptions options)
        {
            if (backend == null)
            {
                throw new ArgumentNullException(nameof(backend));
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            var connection = options.BootstrapConnection
                ? backend.BootstrapConnection(options.ConnectionId, options.ConnectionGeneration, options.TransportId)
                : backend.Connect(options.ConnectionId, options.ConnectionGeneration, options.TransportId);
            var session = connection.OpenSession(
                options.SessionId,
                options.SessionGeneration,
                options.ProfileId,
                options.SchemaId,
                options.SchemaVersion);
            return new NnrpNativeRuntimeSessionHost(backend, options, connection, session);
        }

        public NnrpNativeRuntimeOperation SubmitOperation(
            ulong operationId,
            uint frameId,
            byte[]? payload = null,
            ulong? parentOperationId = null,
            ulong? operationGroupId = null)
        {
            EnsureOpen();
            return Session.SubmitOperation(
                operationId,
                frameId,
                payload,
                parentOperationId,
                operationGroupId);
        }

        public NnrpNativeRuntimeResult SubmitAndPollResult(
            ulong operationId,
            uint frameId,
            byte[]? payload = null,
            ulong? parentOperationId = null,
            ulong? operationGroupId = null,
            NnrpNativeOperationLifecycle? state = null,
            int maxEvents = 0)
        {
            EnsureOpen();
            return Session.SubmitAndPollResult(
                operationId,
                frameId,
                payload,
                parentOperationId,
                operationGroupId,
                state,
                maxEvents);
        }

        public NnrpNativeRuntimeResult PollResult(
            NnrpNativeRuntimeOperation operation,
            NnrpNativeOperationLifecycle? state = null,
            int maxEvents = 0)
        {
            EnsureOpen();
            return Session.PollResult(operation, state, maxEvents);
        }

        public IReadOnlyList<NnrpNativeRuntimeEvent> PollAvailableEvents(int maxEvents = 0)
        {
            EnsureOpen();
            return Connection.PollAvailableEvents(maxEvents);
        }

        public void Cancel(uint frameId)
        {
            EnsureOpen();
            Session.Cancel(frameId);
        }

        public void Control(uint controlCode, byte[]? payload = null)
        {
            EnsureOpen();
            Session.Control(controlCode, payload);
        }

        public void Close()
        {
            EnsureOpen();
            Session.Close();
            Connection.Close();
            IsClosed = true;
        }

        public void Dispose()
        {
            if (IsClosed)
            {
                return;
            }

            Close();
        }

        private void EnsureOpen()
        {
            if (IsClosed)
            {
                throw new NnrpNativeInvalidStateException(new NnrpFfiStatus(NnrpFfiStatusCode.InvalidState));
            }
        }
    }

    public sealed class NnrpNativeRuntimeConnectionHostOptions
    {
        public NnrpNativeRuntimeConnectionHostOptions(
            ulong connectionId,
            uint connectionGeneration,
            uint transportId)
        {
            ConnectionId = connectionId;
            ConnectionGeneration = connectionGeneration;
            TransportId = transportId;
        }

        public ulong ConnectionId { get; }

        public uint ConnectionGeneration { get; }

        public uint TransportId { get; }

        public bool BootstrapConnection { get; set; }

        public string? ArtifactPath { get; set; }

        public string? ArtifactRoot { get; set; }

        public NnrpNativePlatform? Platform { get; set; }

        public INnrpNativeRuntimeBackend? FallbackBackend { get; set; }

        public bool RequireNative { get; set; }
    }

    public sealed class NnrpNativeRuntimeSessionOptions
    {
        public NnrpNativeRuntimeSessionOptions(
            uint sessionId,
            uint sessionGeneration,
            ushort profileId,
            uint schemaId,
            uint schemaVersion)
        {
            SessionId = sessionId;
            SessionGeneration = sessionGeneration;
            ProfileId = profileId;
            SchemaId = schemaId;
            SchemaVersion = schemaVersion;
        }

        public uint SessionId { get; }

        public uint SessionGeneration { get; }

        public ushort ProfileId { get; }

        public uint SchemaId { get; }

        public uint SchemaVersion { get; }
    }

    public sealed class NnrpNativeRuntimeConnectionHost : IDisposable
    {
        private readonly Dictionary<uint, NnrpNativeRuntimeSession> sessions =
            new Dictionary<uint, NnrpNativeRuntimeSession>();

        private NnrpNativeRuntimeConnectionHost(
            INnrpNativeRuntimeBackend backend,
            NnrpNativeRuntimeConnectionHostOptions options,
            NnrpNativeRuntimeConnection connection)
        {
            Backend = backend;
            Options = options;
            Connection = connection;
        }

        public INnrpNativeRuntimeBackend Backend { get; }

        public NnrpNativeRuntimeConnectionHostOptions Options { get; }

        public NnrpNativeRuntimeConnection Connection { get; }

        public IReadOnlyDictionary<uint, NnrpNativeRuntimeSession> Sessions => sessions;

        public bool IsClosed { get; private set; }

        public static NnrpNativeRuntimeConnectionHost Open(NnrpNativeRuntimeConnectionHostOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            var backend = NnrpNativeRuntimeBackendSelector.Select(
                options.ArtifactPath,
                options.ArtifactRoot,
                options.Platform,
                options.FallbackBackend,
                options.RequireNative);
            return Open(backend, options);
        }

        public static NnrpNativeRuntimeConnectionHost Open(
            INnrpNativeRuntimeBackend backend,
            NnrpNativeRuntimeConnectionHostOptions options)
        {
            if (backend == null)
            {
                throw new ArgumentNullException(nameof(backend));
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            var connection = options.BootstrapConnection
                ? backend.BootstrapConnection(options.ConnectionId, options.ConnectionGeneration, options.TransportId)
                : backend.Connect(options.ConnectionId, options.ConnectionGeneration, options.TransportId);
            return new NnrpNativeRuntimeConnectionHost(backend, options, connection);
        }

        public NnrpNativeRuntimeSession OpenSession(NnrpNativeRuntimeSessionOptions options)
        {
            EnsureOpen();
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (sessions.ContainsKey(options.SessionId))
            {
                throw new InvalidOperationException("A session with the same id is already registered.");
            }

            var session = Connection.OpenSession(
                options.SessionId,
                options.SessionGeneration,
                options.ProfileId,
                options.SchemaId,
                options.SchemaVersion);
            sessions.Add(options.SessionId, session);
            return session;
        }

        public bool TryGetSession(uint sessionId, out NnrpNativeRuntimeSession? session)
        {
            EnsureOpen();
            return sessions.TryGetValue(sessionId, out session);
        }

        public NnrpNativeRuntimeSession GetSession(uint sessionId)
        {
            EnsureOpen();
            if (!sessions.TryGetValue(sessionId, out var session))
            {
                throw new KeyNotFoundException("No registered session matches the requested id.");
            }

            return session;
        }

        public NnrpNativeRuntimeOperation SubmitOperation(
            uint sessionId,
            ulong operationId,
            uint frameId,
            byte[]? payload = null,
            ulong? parentOperationId = null,
            ulong? operationGroupId = null)
        {
            return GetSession(sessionId).SubmitOperation(
                operationId,
                frameId,
                payload,
                parentOperationId,
                operationGroupId);
        }

        public NnrpNativeRuntimeResult PollResult(
            uint sessionId,
            NnrpNativeRuntimeOperation operation,
            NnrpNativeOperationLifecycle? state = null,
            int maxEvents = 0)
        {
            return GetSession(sessionId).PollResult(operation, state, maxEvents);
        }

        public NnrpNativeRuntimeResult SubmitAndPollResult(
            uint sessionId,
            ulong operationId,
            uint frameId,
            byte[]? payload = null,
            ulong? parentOperationId = null,
            ulong? operationGroupId = null,
            NnrpNativeOperationLifecycle? state = null,
            int maxEvents = 0)
        {
            return GetSession(sessionId).SubmitAndPollResult(
                operationId,
                frameId,
                payload,
                parentOperationId,
                operationGroupId,
                state,
                maxEvents);
        }

        public IReadOnlyList<NnrpNativeRuntimeEvent> PollAvailableEvents(int maxEvents = 0)
        {
            EnsureOpen();
            return Connection.PollAvailableEvents(maxEvents);
        }

        public void Cancel(uint sessionId, uint frameId)
        {
            GetSession(sessionId).Cancel(frameId);
        }

        public void Control(uint sessionId, uint controlCode, byte[]? payload = null)
        {
            GetSession(sessionId).Control(controlCode, payload);
        }

        public bool CloseSession(uint sessionId)
        {
            EnsureOpen();
            if (!sessions.TryGetValue(sessionId, out var session))
            {
                return false;
            }

            if (!session.IsClosed)
            {
                session.Close();
            }

            sessions.Remove(sessionId);
            return true;
        }

        public void Close()
        {
            EnsureOpen();
            foreach (var session in new List<NnrpNativeRuntimeSession>(sessions.Values))
            {
                if (!session.IsClosed)
                {
                    session.Close();
                }
            }

            sessions.Clear();
            Connection.Close();
            IsClosed = true;
        }

        public void Dispose()
        {
            if (IsClosed)
            {
                return;
            }

            Close();
        }

        private void EnsureOpen()
        {
            if (IsClosed)
            {
                throw new NnrpNativeInvalidStateException(new NnrpFfiStatus(NnrpFfiStatusCode.InvalidState));
            }
        }
    }
}
