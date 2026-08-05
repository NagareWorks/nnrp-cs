using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nnrp.Core;
using Nnrp.NativeBridge;
using Nnrp.Runtime;

namespace Nnrp.Client
{
    public sealed class NnrpClient : IAsyncDisposable
    {
        private readonly object gate = new object();
        private readonly SemaphoreSlim eventReadGate = new SemaphoreSlim(1, 1);
        private readonly Dictionary<ulong, Queue<NnrpNativeRuntimeEvent>> bufferedEvents =
            new Dictionary<ulong, Queue<NnrpNativeRuntimeEvent>>();
        private readonly List<NnrpClientSession> sessions = new List<NnrpClientSession>();
        private readonly NnrpNativeRuntimeConnection connection;
        private readonly NnrpClientSessionOptions? sessionDefaults;

        internal NnrpClient(
            NnrpClientOptions options,
            NnrpNativeRuntimeConnection connection,
            NnrpTransportSelection selection)
        {
            Options = options;
            this.connection = connection;
            Selection = selection;
            sessionDefaults = options.SessionDefaults;
        }

        public NnrpClientOptions Options { get; }

        public NnrpTransportSelection Selection { get; }

        public TransportId ActiveTransportId => Selection.SelectedProvider.TransportId;

        public bool IsClosed { get; private set; }

        public static async ValueTask<NnrpClient> ConnectAsync(
            NnrpClientOptions options,
            CancellationToken cancellationToken = default)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            var transport = await NnrpClientTransportOrchestrator.ConnectAsync(options, cancellationToken)
                .ConfigureAwait(false);
            try
            {
                var connectionId = NnrpRuntimeHandleIdAllocator.Allocate();
                var nativeConnection = transport.Connection.AdoptClient(connectionId, generation: 1);
                return new NnrpClient(options, nativeConnection, transport.Selection);
            }
            catch
            {
                await transport.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }

        public NnrpClientSession OpenSession(NnrpClientSessionOptions? options = null)
        {
            lock (gate)
            {
                EnsureOpen();
                var configured = options ?? sessionDefaults ?? new NnrpClientSessionOptions();
                var nativeSession = connection.OpenSession(
                    configured.SessionId,
                    NnrpRuntimeHandleIdAllocator.Allocate(),
                    configured.SessionGeneration,
                    configured.ProfileId,
                    configured.PriorityClass,
                    configured.SchemaId,
                    configured.SchemaVersion,
                    configured.DefaultDeadlineMilliseconds,
                    configured.MaxInFlightOperations,
                    configured.LeaseTtlHintMilliseconds,
                    configured.AllowResume,
                    configured.ResumeTokenBytes,
                    configured.CacheHints);
                var session = new NnrpClientSession(this, nativeSession, configured);
                sessions.Add(session);
                return session;
            }
        }

        public NnrpClientSession ResumeSession(
            NnrpSessionRecoveryTicket ticket,
            NnrpClientSessionOptions? options = null)
        {
            if (ticket == null)
            {
                throw new ArgumentNullException(nameof(ticket));
            }

            lock (gate)
            {
                EnsureOpen();
                var configured = options ?? sessionDefaults ?? new NnrpClientSessionOptions();
                var resumeTokenBytes = Math.Max(
                    configured.ResumeTokenBytes,
                    checked((uint)ticket.ResumeToken.Length));
                var resolved = new NnrpClientSessionOptions(
                    sessionId: ticket.SessionId,
                    sessionGeneration: configured.SessionGeneration,
                    profileId: configured.ProfileId,
                    schemaId: configured.SchemaId,
                    schemaVersion: configured.SchemaVersion,
                    priorityClass: configured.PriorityClass,
                    defaultDeadlineMilliseconds: configured.DefaultDeadlineMilliseconds,
                    maxInFlightOperations: configured.MaxInFlightOperations,
                    leaseTtlHintMilliseconds: configured.LeaseTtlHintMilliseconds,
                    allowResume: true,
                    resumeTokenBytes: resumeTokenBytes,
                    cacheHints: configured.CacheHints);
                var nativeSession = connection.ResumeSession(
                    resolved.SessionId,
                    NnrpRuntimeHandleIdAllocator.Allocate(),
                    resolved.SessionGeneration,
                    resolved.ProfileId,
                    resolved.PriorityClass,
                    resolved.SchemaId,
                    resolved.SchemaVersion,
                    resolved.DefaultDeadlineMilliseconds,
                    resolved.MaxInFlightOperations,
                    resolved.LeaseTtlHintMilliseconds,
                    resolved.ResumeTokenBytes,
                    resolved.CacheHints,
                    ticket.ToBytes(),
                    out _);
                var session = new NnrpClientSession(this, nativeSession, resolved);
                sessions.Add(session);
                return session;
            }
        }

        public async ValueTask DisposeAsync()
        {
            NnrpClientSession[] ownedSessions;
            lock (gate)
            {
                if (IsClosed)
                {
                    return;
                }

                IsClosed = true;
                ownedSessions = sessions.ToArray();
                sessions.Clear();
                bufferedEvents.Clear();
            }

            Exception? firstError = null;
            foreach (var session in ownedSessions)
            {
                try
                {
                    await session.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception error)
                {
                    firstError ??= error;
                }
            }

            try
            {
                connection.Dispose();
            }
            catch (Exception error)
            {
                firstError ??= error;
            }

            if (firstError != null)
            {
                throw firstError;
            }
        }

        internal async ValueTask<NnrpNativeRuntimeEvent> NextNativeEventAsync(
            NnrpNativeRuntimeSession session,
            CancellationToken cancellationToken)
        {
            var sessionId = session.Handle.Handle.Id;
            await eventReadGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    lock (gate)
                    {
                        EnsureOpen();
                        if (bufferedEvents.TryGetValue(sessionId, out var queue) && queue.Count != 0)
                        {
                            return queue.Dequeue();
                        }
                    }

                    IReadOnlyList<NnrpNativeRuntimeEvent> events;
                    try
                    {
                        events = session.AwaitEvents(16, 10);
                    }
                    catch (NnrpNativeWouldBlockException)
                    {
                        await Task.Yield();
                        continue;
                    }

                    if (events.Count == 0)
                    {
                        await Task.Yield();
                        continue;
                    }

                    lock (gate)
                    {
                        EnsureOpen();
                        if (!bufferedEvents.TryGetValue(sessionId, out var queue))
                        {
                            queue = new Queue<NnrpNativeRuntimeEvent>();
                            bufferedEvents.Add(sessionId, queue);
                        }

                        for (var index = 1; index < events.Count; index++)
                        {
                            queue.Enqueue(events[index]);
                        }
                    }

                    return events[0];
                }
            }
            finally
            {
                eventReadGate.Release();
            }
        }

        internal void RemoveSession(NnrpClientSession session)
        {
            lock (gate)
            {
                sessions.Remove(session);
                bufferedEvents.Remove(session.NativeHandleId);
            }
        }

        private void EnsureOpen()
        {
            if (IsClosed)
            {
                throw new ObjectDisposedException(nameof(NnrpClient));
            }
        }

    }
}
