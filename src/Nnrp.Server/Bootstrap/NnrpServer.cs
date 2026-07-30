using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nnrp.Core;

namespace Nnrp.Server
{
    public sealed class NnrpServer : IAsyncDisposable
    {
        private readonly object gate = new object();
        private readonly NnrpServerTransportListenerSet listeners;
        private readonly List<NnrpServerSession> sessions = new List<NnrpServerSession>();

        internal NnrpServer(NnrpServerOptions options, NnrpServerTransportListenerSet listeners)
        {
            Options = options;
            this.listeners = listeners;
        }

        public NnrpServerOptions Options { get; }

        public IReadOnlyDictionary<TransportId, NnrpProviderEndpoint> BoundProviderEndpoints =>
            listeners.BoundProviderEndpoints;

        public bool IsClosed { get; private set; }

        public static async ValueTask<NnrpServer> ListenAsync(
            NnrpServerOptions options,
            CancellationToken cancellationToken = default)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            var listenerSet = await NnrpServerTransportOrchestrator.ListenAsync(options, cancellationToken)
                .ConfigureAwait(false);
            return new NnrpServer(options, listenerSet);
        }

        public async ValueTask<NnrpServerSession> AcceptAsync(
            NnrpServerAcceptOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            EnsureOpen();
            var accepted = await listeners.AcceptAsync(options, cancellationToken).ConfigureAwait(false);
            if (accepted.NativeSession == null)
            {
                accepted.Dispose();
                throw new InvalidOperationException(
                    "A production server listener must return a Rust-backed accepted session.");
            }

            var session = new NnrpServerSession(this, accepted, accepted.NativeSession);
            lock (gate)
            {
                EnsureOpen();
                sessions.Add(session);
            }

            return session;
        }

        public async ValueTask DisposeAsync()
        {
            NnrpServerSession[] ownedSessions;
            lock (gate)
            {
                if (IsClosed)
                {
                    return;
                }

                IsClosed = true;
                ownedSessions = sessions.ToArray();
                sessions.Clear();
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
                await listeners.DisposeAsync().ConfigureAwait(false);
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

        internal void RemoveSession(NnrpServerSession session)
        {
            lock (gate)
            {
                sessions.Remove(session);
            }
        }

        private void EnsureOpen()
        {
            if (IsClosed)
            {
                throw new ObjectDisposedException(nameof(NnrpServer));
            }
        }
    }
}
