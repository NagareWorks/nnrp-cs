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
}
