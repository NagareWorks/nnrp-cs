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

        public NnrpNativeRuntimeFallbackPolicy FallbackPolicy { get; set; } =
            NnrpNativeRuntimeFallbackPolicy.RequireNative;
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
                options.FallbackPolicy,
                options.TransportId,
                NnrpNativeArtifact.TransportScopeFromTransportId(options.TransportId));
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

        public NnrpNativeRuntimeOperation SubmitOperation(
            ulong operationId,
            uint frameId,
            ReadOnlyMemory<byte> payload,
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

        public NnrpNativeRuntimeOperation SubmitOperation(
            ulong operationId,
            uint frameId,
            NnrpNativeBuffer payload,
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

        public NnrpNativeRuntimeResult SubmitAndPollResult(
            ulong operationId,
            uint frameId,
            ReadOnlyMemory<byte> payload,
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

        public NnrpNativeRuntimeResult SubmitAndPollResult(
            ulong operationId,
            uint frameId,
            NnrpNativeBuffer payload,
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

        public ulong SubmitResultCompactBatch(
            ulong operationIdStart,
            uint frameIdStart,
            uint frameIdStride,
            ReadOnlyMemory<byte> submitPayload,
            ReadOnlyMemory<byte> resultPayload,
            int maxEvents,
            int iterations)
        {
            EnsureOpen();
            return Session.SubmitResultCompactBatch(
                operationIdStart,
                frameIdStart,
                frameIdStride,
                submitPayload,
                resultPayload,
                maxEvents,
                iterations);
        }

        public NnrpNativeRuntimeResult PollResult(
            NnrpNativeRuntimeOperation operation,
            NnrpNativeOperationLifecycle? state = null,
            int maxEvents = 0)
        {
            EnsureOpen();
            return Session.PollResult(operation, state, maxEvents);
        }

        public NnrpCacheLeaseResult QueryCacheLease(
            NnrpCacheObjectId objectId,
            ulong expectedVersion,
            ulong nowMilliseconds,
            uint ttlMilliseconds)
        {
            EnsureOpen();
            return Session.QueryCacheLease(
                objectId,
                expectedVersion,
                nowMilliseconds,
                ttlMilliseconds);
        }

        public NnrpCacheLeaseResult TouchCacheLease(
            NnrpCacheObjectId objectId,
            ulong expectedVersion,
            ulong nowMilliseconds,
            uint ttlMilliseconds)
        {
            EnsureOpen();
            return Session.TouchCacheLease(
                objectId,
                expectedVersion,
                nowMilliseconds,
                ttlMilliseconds);
        }

        public NnrpCacheLeaseResult[] PrefetchCacheLeases(
            NnrpCacheObjectId[] objects,
            ulong nowMilliseconds,
            uint ttlMilliseconds)
        {
            EnsureOpen();
            return Session.PrefetchCacheLeases(objects, nowMilliseconds, ttlMilliseconds);
        }

        public NnrpCacheLeaseResult ReleaseCacheLease(NnrpCacheLeaseHandle lease)
        {
            EnsureOpen();
            return Session.ReleaseCacheLease(lease);
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

        public void Control(uint controlCode, ReadOnlyMemory<byte> payload)
        {
            EnsureOpen();
            Session.Control(controlCode, payload);
        }

        public void Control(uint controlCode, NnrpNativeBuffer payload)
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

        public NnrpNativeRuntimeFallbackPolicy FallbackPolicy { get; set; } =
            NnrpNativeRuntimeFallbackPolicy.RequireNative;
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
                options.FallbackPolicy,
                options.TransportId,
                NnrpNativeArtifact.TransportScopeFromTransportId(options.TransportId));
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

        public NnrpNativeRuntimeOperation SubmitOperation(
            uint sessionId,
            ulong operationId,
            uint frameId,
            ReadOnlyMemory<byte> payload,
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

        public NnrpNativeRuntimeOperation SubmitOperation(
            uint sessionId,
            ulong operationId,
            uint frameId,
            NnrpNativeBuffer payload,
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

        public NnrpNativeRuntimeResult SubmitAndPollResult(
            uint sessionId,
            ulong operationId,
            uint frameId,
            ReadOnlyMemory<byte> payload,
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

        public NnrpNativeRuntimeResult SubmitAndPollResult(
            uint sessionId,
            ulong operationId,
            uint frameId,
            NnrpNativeBuffer payload,
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

        public NnrpNativeSchemaRegistry CreateSchemaRegistry()
        {
            EnsureOpen();
            return NnrpNativeSchemaRegistry.Create(Connection.Entrypoints);
        }

        public NnrpCacheLeaseResult QueryCacheLease(
            uint sessionId,
            NnrpCacheObjectId objectId,
            ulong expectedVersion,
            ulong nowMilliseconds,
            uint ttlMilliseconds)
        {
            return GetSession(sessionId).QueryCacheLease(
                objectId,
                expectedVersion,
                nowMilliseconds,
                ttlMilliseconds);
        }

        public NnrpCacheLeaseResult TouchCacheLease(
            uint sessionId,
            NnrpCacheObjectId objectId,
            ulong expectedVersion,
            ulong nowMilliseconds,
            uint ttlMilliseconds)
        {
            return GetSession(sessionId).TouchCacheLease(
                objectId,
                expectedVersion,
                nowMilliseconds,
                ttlMilliseconds);
        }

        public NnrpCacheLeaseResult[] PrefetchCacheLeases(
            uint sessionId,
            NnrpCacheObjectId[] objects,
            ulong nowMilliseconds,
            uint ttlMilliseconds)
        {
            return GetSession(sessionId).PrefetchCacheLeases(objects, nowMilliseconds, ttlMilliseconds);
        }

        public NnrpCacheLeaseResult ReleaseCacheLease(NnrpCacheLeaseHandle lease)
        {
            EnsureOpen();
            return new NnrpNativeCacheLeases(Connection.Entrypoints).Release(lease);
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

        public void Control(uint sessionId, uint controlCode, ReadOnlyMemory<byte> payload)
        {
            GetSession(sessionId).Control(controlCode, payload);
        }

        public void Control(uint sessionId, uint controlCode, NnrpNativeBuffer payload)
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

    public sealed class NnrpNativeRuntimeServerHostOptions
    {
        public NnrpNativeRuntimeServerHostOptions(
            ulong serverId,
            uint serverGeneration,
            uint transportId)
        {
            ServerId = serverId;
            ServerGeneration = serverGeneration;
            TransportId = transportId;
        }

        public ulong ServerId { get; }

        public uint ServerGeneration { get; }

        public uint TransportId { get; }

        public string? ArtifactPath { get; set; }

        public string? ArtifactRoot { get; set; }

        public NnrpNativePlatform? Platform { get; set; }
    }

    public sealed class NnrpNativeRuntimeServerHost : IDisposable
    {
        private readonly Dictionary<uint, NnrpNativeRuntimeServerSession> sessions =
            new Dictionary<uint, NnrpNativeRuntimeServerSession>();

        private NnrpNativeRuntimeServerHost(
            NnrpNativeRuntimeEntrypoints entrypoints,
            NnrpNativeRuntimeServerHostOptions options,
            NnrpNativeRuntimeServer server)
        {
            Entrypoints = entrypoints;
            Options = options;
            Server = server;
        }

        public NnrpNativeRuntimeEntrypoints Entrypoints { get; }

        public NnrpNativeRuntimeServerHostOptions Options { get; }

        public NnrpNativeRuntimeServer Server { get; }

        public IReadOnlyDictionary<uint, NnrpNativeRuntimeServerSession> Sessions => sessions;

        public bool IsClosed { get; private set; }

        public static NnrpNativeRuntimeServerHost Open(NnrpNativeRuntimeServerHostOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            var entrypoints = NnrpNativeRuntimeEntrypoints.Load(
                options.ArtifactPath,
                options.ArtifactRoot,
                options.Platform,
                options.TransportId,
                NnrpNativeArtifact.TransportScopeFromTransportId(options.TransportId));
            return Open(entrypoints, options);
        }

        public static NnrpNativeRuntimeServerHost Open(
            NnrpNativeRuntimeEntrypoints entrypoints,
            NnrpNativeRuntimeServerHostOptions options)
        {
            if (entrypoints == null)
            {
                throw new ArgumentNullException(nameof(entrypoints));
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            var server = NnrpNativeRuntimeServer.Bind(
                entrypoints,
                options.ServerId,
                options.ServerGeneration,
                options.TransportId);
            return new NnrpNativeRuntimeServerHost(entrypoints, options, server);
        }

        public NnrpNativeRuntimeServerSession AcceptSession(NnrpNativeRuntimeSessionOptions options)
        {
            EnsureOpen();
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (sessions.ContainsKey(options.SessionId))
            {
                throw new InvalidOperationException("A server session with the same id is already registered.");
            }

            var session = Server.AcceptSession(
                options.SessionId,
                options.SessionGeneration,
                options.ProfileId,
                options.SchemaId,
                options.SchemaVersion);
            sessions.Add(options.SessionId, session);
            return session;
        }

        public bool TryGetSession(uint sessionId, out NnrpNativeRuntimeServerSession? session)
        {
            EnsureOpen();
            return sessions.TryGetValue(sessionId, out session);
        }

        public NnrpNativeRuntimeServerSession GetSession(uint sessionId)
        {
            EnsureOpen();
            if (!sessions.TryGetValue(sessionId, out var session))
            {
                throw new KeyNotFoundException("No registered server session matches the requested id.");
            }

            return session;
        }

        public NnrpNativeRuntimeOperation ReceiveSubmit(
            uint sessionId,
            ulong operationId,
            uint frameId,
            byte[]? payload = null)
        {
            return GetSession(sessionId).ReceiveSubmit(operationId, frameId, payload);
        }

        public NnrpNativeRuntimeOperation ReceiveSubmit(
            uint sessionId,
            ulong operationId,
            uint frameId,
            ReadOnlyMemory<byte> payload)
        {
            return GetSession(sessionId).ReceiveSubmit(operationId, frameId, payload);
        }

        public NnrpNativeRuntimeOperation ReceiveSubmit(
            uint sessionId,
            ulong operationId,
            uint frameId,
            NnrpNativeBuffer payload)
        {
            return GetSession(sessionId).ReceiveSubmit(operationId, frameId, payload);
        }

        public void SendResult(uint sessionId, NnrpNativeRuntimeOperation operation, byte[]? payload = null)
        {
            GetSession(sessionId).SendResult(operation, payload);
        }

        public void SendResult(uint sessionId, NnrpNativeRuntimeOperation operation, ReadOnlyMemory<byte> payload)
        {
            GetSession(sessionId).SendResult(operation, payload);
        }

        public void SendResult(uint sessionId, NnrpNativeRuntimeOperation operation, NnrpNativeBuffer payload)
        {
            GetSession(sessionId).SendResult(operation, payload);
        }

        public void SendFlowUpdate(uint sessionId, uint frameId)
        {
            GetSession(sessionId).SendFlowUpdate(frameId);
        }

        public NnrpNativeSchemaRegistry CreateSchemaRegistry()
        {
            EnsureOpen();
            return NnrpNativeSchemaRegistry.Create(Entrypoints);
        }

        public NnrpCacheLeaseResult QueryCacheLease(
            uint sessionId,
            NnrpCacheObjectId objectId,
            ulong expectedVersion,
            ulong nowMilliseconds,
            uint ttlMilliseconds)
        {
            return GetSession(sessionId).QueryCacheLease(
                objectId,
                expectedVersion,
                nowMilliseconds,
                ttlMilliseconds);
        }

        public NnrpCacheLeaseResult TouchCacheLease(
            uint sessionId,
            NnrpCacheObjectId objectId,
            ulong expectedVersion,
            ulong nowMilliseconds,
            uint ttlMilliseconds)
        {
            return GetSession(sessionId).TouchCacheLease(
                objectId,
                expectedVersion,
                nowMilliseconds,
                ttlMilliseconds);
        }

        public NnrpCacheLeaseResult[] PrefetchCacheLeases(
            uint sessionId,
            NnrpCacheObjectId[] objects,
            ulong nowMilliseconds,
            uint ttlMilliseconds)
        {
            return GetSession(sessionId).PrefetchCacheLeases(objects, nowMilliseconds, ttlMilliseconds);
        }

        public NnrpCacheLeaseResult ReleaseCacheLease(NnrpCacheLeaseHandle lease)
        {
            EnsureOpen();
            return new NnrpNativeCacheLeases(Entrypoints).Release(lease);
        }

        public void Control(uint sessionId, uint controlCode, byte[]? payload = null)
        {
            GetSession(sessionId).Control(controlCode, payload);
        }

        public void Control(uint sessionId, uint controlCode, ReadOnlyMemory<byte> payload)
        {
            GetSession(sessionId).Control(controlCode, payload);
        }

        public void Control(uint sessionId, uint controlCode, NnrpNativeBuffer payload)
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
            foreach (var session in new List<NnrpNativeRuntimeServerSession>(sessions.Values))
            {
                if (!session.IsClosed)
                {
                    session.Close();
                }
            }

            sessions.Clear();
            Server.Close();
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
