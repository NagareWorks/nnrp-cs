using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nnrp.Core;
using Nnrp.NativeBridge;

namespace Nnrp.Server
{
    internal delegate ValueTask<INnrpServerTransportListener> NnrpServerTransportBinder(
        INnrpNativeTransportProvider provider,
        NnrpTransportListenOptions listenOptions,
        NnrpServerOptions serverOptions,
        CancellationToken cancellationToken);

    internal interface INnrpServerTransportListener : IAsyncDisposable
    {
        TransportId TransportId { get; }

        NnrpProviderEndpoint BoundEndpoint { get; }

        ValueTask<NnrpAcceptedServerTransportSession> AcceptAsync(
            NnrpServerAcceptOptions options,
            uint pollTimeoutMilliseconds,
            CancellationToken cancellationToken);

        bool ReleasePendingAccept();
    }

    internal sealed class NnrpAcceptedServerTransportSession : IDisposable
    {
        private Action? close;

        internal NnrpAcceptedServerTransportSession(
            TransportId activeTransportId,
            NnrpNativeRuntimeServerSession? nativeSession,
            Action close)
        {
            if (activeTransportId == TransportId.Unspecified
                || !Enum.IsDefined(typeof(TransportId), activeTransportId))
            {
                throw new ArgumentOutOfRangeException(nameof(activeTransportId));
            }

            ActiveTransportId = activeTransportId;
            NativeSession = nativeSession;
            this.close = close ?? throw new ArgumentNullException(nameof(close));
        }

        internal TransportId ActiveTransportId { get; }

        internal NnrpNativeRuntimeServerSession? NativeSession { get; }

        internal bool IsClosed => close == null;

        public void Dispose()
        {
            Interlocked.Exchange(ref close, null)?.Invoke();
        }
    }

    internal sealed class NnrpServerTransportListenerSet : IDisposable, IAsyncDisposable
    {
        private const uint PollTimeoutMilliseconds = 10;

        private readonly SemaphoreSlim acceptGate = new SemaphoreSlim(1, 1);
        private readonly List<NnrpAcceptedServerTransportSession> acceptedSessions =
            new List<NnrpAcceptedServerTransportSession>();
        private readonly INnrpServerTransportListener[] listeners;

        internal NnrpServerTransportListenerSet(IEnumerable<INnrpServerTransportListener> listeners)
        {
            if (listeners == null)
            {
                throw new ArgumentNullException(nameof(listeners));
            }

            this.listeners = listeners.ToArray();
            if (this.listeners.Length == 0 || this.listeners.Any(value => value == null))
            {
                throw new ArgumentException("The server listener set must contain at least one listener.", nameof(listeners));
            }

            if (this.listeners.Select(value => value.TransportId).Distinct().Count() != this.listeners.Length)
            {
                throw new ArgumentException("The server listener set must not contain duplicate transports.", nameof(listeners));
            }

            BoundProviderEndpoints = new ReadOnlyDictionary<TransportId, NnrpProviderEndpoint>(
                this.listeners.ToDictionary(value => value.TransportId, value => value.BoundEndpoint));
        }

        internal IReadOnlyDictionary<TransportId, NnrpProviderEndpoint> BoundProviderEndpoints { get; }

        internal bool IsClosed { get; private set; }

        internal async ValueTask<NnrpAcceptedServerTransportSession> AcceptAsync(
            NnrpServerAcceptOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            options ??= new NnrpServerAcceptOptions();
            await acceptGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                EnsureOpen();
                var elapsed = Stopwatch.StartNew();
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    foreach (var listener in listeners)
                    {
                        var pollTimeout = RemainingPollTimeout(options.TimeoutMilliseconds, elapsed);
                        if (pollTimeout == 0)
                        {
                            try
                            {
                                ReleasePendingAccepts();
                            }
                            catch (Exception error)
                            {
                                await CloseAfterFailureAsync(error).ConfigureAwait(false);
                                throw;
                            }

                            throw new TimeoutException("The server accept operation timed out.");
                        }

                        try
                        {
                            var accepted = await listener.AcceptAsync(
                                options,
                                pollTimeout,
                                cancellationToken).ConfigureAwait(false);
                            if (accepted == null)
                            {
                                throw new InvalidOperationException("A server transport listener returned a null session.");
                            }

                            if (accepted.ActiveTransportId != listener.TransportId)
                            {
                                accepted.Dispose();
                                throw new InvalidOperationException(
                                    "The accepted transport does not match the listener that produced it.");
                            }

                            try
                            {
                                ReleasePendingAccepts(listener);
                            }
                            catch
                            {
                                accepted.Dispose();
                                throw;
                            }

                            acceptedSessions.Add(accepted);
                            return accepted;
                        }
                        catch (NnrpNativeWouldBlockException)
                        {
                        }
                        catch (NnrpNativeProtocolException)
                        {
                            try
                            {
                                listener.ReleasePendingAccept();
                            }
                            catch (Exception error)
                            {
                                await CloseAfterFailureAsync(error).ConfigureAwait(false);
                                throw;
                            }
                        }
                        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                        {
                            throw;
                        }
                        catch (Exception error)
                        {
                            await CloseAfterFailureAsync(error).ConfigureAwait(false);
                            throw;
                        }
                    }

                    await Task.Yield();
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                try
                {
                    ReleasePendingAccepts();
                }
                catch (Exception error)
                {
                    await CloseAfterFailureAsync(error).ConfigureAwait(false);
                    throw;
                }

                throw;
            }
            finally
            {
                acceptGate.Release();
            }
        }

        public void Dispose()
        {
            DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        public async ValueTask DisposeAsync()
        {
            await acceptGate.WaitAsync().ConfigureAwait(false);
            try
            {
                await CloseCoreAsync().ConfigureAwait(false);
            }
            finally
            {
                acceptGate.Release();
            }
        }

        private static uint RemainingPollTimeout(uint timeoutMilliseconds, Stopwatch elapsed)
        {
            if (timeoutMilliseconds == 0)
            {
                return PollTimeoutMilliseconds;
            }

            var remaining = timeoutMilliseconds - Math.Min(timeoutMilliseconds, (uint)elapsed.ElapsedMilliseconds);
            return Math.Min(PollTimeoutMilliseconds, remaining);
        }

        private void ReleasePendingAccepts(INnrpServerTransportListener? except = null)
        {
            foreach (var listener in listeners)
            {
                if (!ReferenceEquals(listener, except))
                {
                    listener.ReleasePendingAccept();
                }
            }
        }

        private async ValueTask CloseCoreAsync()
        {
            if (IsClosed)
            {
                return;
            }

            IsClosed = true;
            var errors = new List<Exception>();
            foreach (var session in acceptedSessions)
            {
                try
                {
                    session.Dispose();
                }
                catch (Exception error)
                {
                    errors.Add(error);
                }
            }

            acceptedSessions.Clear();
            foreach (var listener in listeners.AsEnumerable().Reverse())
            {
                try
                {
                    await listener.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception error)
                {
                    errors.Add(error);
                }
            }

            if (errors.Count > 0)
            {
                throw new AggregateException("One or more server transport resources failed to close.", errors);
            }
        }

        private async ValueTask CloseAfterFailureAsync(Exception originalError)
        {
            try
            {
                await CloseCoreAsync().ConfigureAwait(false);
            }
            catch (Exception closeError)
            {
                originalError.Data["NnrpServerCloseError"] = closeError;
            }
        }

        private void EnsureOpen()
        {
            if (IsClosed)
            {
                throw new ObjectDisposedException(nameof(NnrpServerTransportListenerSet));
            }
        }
    }

    internal static class NnrpServerTransportOrchestrator
    {
        private const ulong MaxPacketBytes = 16 * 1024 * 1024;

        internal static async ValueTask<NnrpServerTransportListenerSet> ListenAsync(
            NnrpServerOptions options,
            CancellationToken cancellationToken = default,
            NnrpServerTransportBinder? binder = null)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            cancellationToken.ThrowIfCancellationRequested();
            var plans = ResolvePlans(options);
            var listeners = new List<INnrpServerTransportListener>();
            try
            {
                foreach (var plan in plans)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var listener = await (binder ?? BindNativeAsync)(
                        plan.Provider,
                        plan.ListenOptions,
                        options,
                        cancellationToken).ConfigureAwait(false);
                    if (listener == null)
                    {
                        throw new InvalidOperationException("A transport binder returned a null listener.");
                    }

                    if (listener.TransportId != plan.Provider.Descriptor.TransportId)
                    {
                        await listener.DisposeAsync().ConfigureAwait(false);
                        throw new InvalidOperationException("A transport binder returned a listener for a different transport.");
                    }

                    listeners.Add(listener);
                }

                return new NnrpServerTransportListenerSet(listeners);
            }
            catch (Exception error)
            {
                var rollbackErrors = await DisposeListenersAsync(listeners).ConfigureAwait(false);
                if (rollbackErrors.Count > 0)
                {
                    error.Data["NnrpServerListenerRollbackErrors"] = rollbackErrors;
                }

                throw;
            }
        }

        private static IReadOnlyList<ListenerPlan> ResolvePlans(NnrpServerOptions options)
        {
            var providers = options.Transports ?? NnrpNativeTransportDefaults.Discover();
            var registry = new NnrpNativeTransportRegistry(providers);
            var snapshot = registry.Snapshot();
            var forced = ForcedTransport(options.TransportPolicy);
            var allowed = snapshot
                .Where(provider => !forced.HasValue || provider.Descriptor.TransportId == forced.Value)
                .ToArray();
            if (allowed.Length == 0)
            {
                throw new NnrpTransportSelectionException(
                    forced.HasValue
                        ? NnrpTransportSelectionErrorCode.ForcedTransportUnavailable
                        : NnrpTransportSelectionErrorCode.NoViableTransport,
                    forced.HasValue
                        ? $"Forced transport is not installed: {forced.Value}."
                        : "No transport provider is installed for the server listener set.",
                    options.TransportPolicy);
            }

            var plans = new List<ListenerPlan>(allowed.Length);
            foreach (var provider in allowed)
            {
                var descriptor = provider.Descriptor;
                if (!descriptor.Available)
                {
                    throw ListenerConfigurationError(
                        options.TransportPolicy,
                        snapshot,
                        descriptor,
                        NnrpTransportRejectionReason.LocalUnavailable,
                        descriptor.Diagnostic ?? $"Transport provider {descriptor.Metadata.Id} is not available.");
                }

                if (descriptor.Metadata.Limits.MaxFrameBytes < MaxPacketBytes)
                {
                    throw ListenerConfigurationError(
                        options.TransportPolicy,
                        snapshot,
                        descriptor,
                        NnrpTransportRejectionReason.LimitExceeded,
                        $"Transport provider {descriptor.Metadata.Id} cannot accept the server frame limit.");
                }

                options.ProviderRoutes.TryGetValue(descriptor.TransportId, out var route);
                if (!NnrpTransportRouteResolver.TryResolveServer(
                    options.Endpoint,
                    descriptor.TransportId,
                    route,
                    out var providerEndpoint,
                    out var rejectionReason,
                    out var diagnostic))
                {
                    throw ListenerConfigurationError(
                        options.TransportPolicy,
                        snapshot,
                        descriptor,
                        rejectionReason!.Value,
                        diagnostic ?? $"Transport route for {descriptor.TransportId} is invalid.");
                }

                plans.Add(new ListenerPlan(
                    provider,
                    new NnrpTransportListenOptions(
                        options.Endpoint,
                        providerEndpoint!,
                        route?.Security,
                        MaxPacketBytes)));
            }

            return plans;
        }

        private static NnrpTransportSelectionException ListenerConfigurationError(
            TransportPolicy policy,
            IReadOnlyList<INnrpNativeTransportProvider> providers,
            NnrpTransportProviderDescriptor rejected,
            NnrpTransportRejectionReason reason,
            string diagnostic)
        {
            var candidates = providers.Select(provider => new NnrpTransportCandidate(
                provider.Descriptor.TransportId,
                provider.Descriptor.Metadata,
                provider.Descriptor.Available,
                peerSupported: true,
                withinLimits: provider.Descriptor.Metadata.Limits.MaxFrameBytes >= MaxPacketBytes,
                NnrpTransportProbeState.NotRun,
                rejectionReason: ReferenceEquals(provider.Descriptor, rejected) ? reason : null,
                diagnostic: ReferenceEquals(provider.Descriptor, rejected) ? diagnostic : provider.Descriptor.Diagnostic));
            return new NnrpTransportSelectionException(
                ForcedTransport(policy).HasValue
                    ? NnrpTransportSelectionErrorCode.ForcedTransportUnavailable
                    : NnrpTransportSelectionErrorCode.InvalidEvidence,
                diagnostic,
                policy,
                candidates);
        }

        [ExcludeFromCodeCoverage]
        private static async ValueTask<INnrpServerTransportListener> BindNativeAsync(
            INnrpNativeTransportProvider provider,
            NnrpTransportListenOptions listenOptions,
            NnrpServerOptions serverOptions,
            CancellationToken cancellationToken)
        {
            NnrpTransportListener? listener = null;
            try
            {
                listener = await provider.ListenAsync(listenOptions, cancellationToken).ConfigureAwait(false);
                if (listener == null)
                {
                    throw new InvalidOperationException("A transport provider returned a null listener.");
                }

                var boundEndpoint = listener.BoundEndpoint;
                var host = NnrpNativeRuntimeServerHost.Open(
                    listener,
                    new NnrpNativeRuntimeServerHostOptions(
                        serverOptions.ServerId,
                        serverOptions.ServerGeneration));
                listener = null;
                return new NativeListener(provider.Descriptor.TransportId, boundEndpoint, host);
            }
            catch
            {
                listener?.Dispose();
                throw;
            }
        }

        private static async ValueTask<IReadOnlyList<Exception>> DisposeListenersAsync(
            IEnumerable<INnrpServerTransportListener> listeners)
        {
            var errors = new List<Exception>();
            foreach (var listener in listeners.Reverse())
            {
                try
                {
                    await listener.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception error)
                {
                    errors.Add(error);
                }
            }

            return errors;
        }

        private static TransportId? ForcedTransport(TransportPolicy policy)
        {
            return policy switch
            {
                TransportPolicy.ForceTcp => TransportId.Tcp,
                TransportPolicy.ForceQuic => TransportId.Quic,
                TransportPolicy.ForceIpc => TransportId.Ipc,
                TransportPolicy.ForceWebSocket => TransportId.WebSocket,
                _ => null,
            };
        }

        private sealed class ListenerPlan
        {
            internal ListenerPlan(
                INnrpNativeTransportProvider provider,
                NnrpTransportListenOptions listenOptions)
            {
                Provider = provider;
                ListenOptions = listenOptions;
            }

            internal INnrpNativeTransportProvider Provider { get; }

            internal NnrpTransportListenOptions ListenOptions { get; }
        }

        [ExcludeFromCodeCoverage]
        private sealed class NativeListener : INnrpServerTransportListener
        {
            private NnrpNativeRuntimeServerHost? host;

            internal NativeListener(
                TransportId transportId,
                NnrpProviderEndpoint boundEndpoint,
                NnrpNativeRuntimeServerHost host)
            {
                TransportId = transportId;
                BoundEndpoint = boundEndpoint;
                this.host = host;
            }

            public TransportId TransportId { get; }

            public NnrpProviderEndpoint BoundEndpoint { get; }

            public ValueTask<NnrpAcceptedServerTransportSession> AcceptAsync(
                NnrpServerAcceptOptions options,
                uint pollTimeoutMilliseconds,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var current = host ?? throw new ObjectDisposedException(nameof(NativeListener));
                var session = current.Server.AcceptSession(
                    options.SessionId,
                    options.SessionGeneration,
                    pollTimeoutMilliseconds);
                return new ValueTask<NnrpAcceptedServerTransportSession>(
                    new NnrpAcceptedServerTransportSession(
                        session.ActiveTransportId,
                        session,
                        () =>
                        {
                            if (!session.IsClosed)
                            {
                                session.Close();
                            }
                        }));
            }

            public bool ReleasePendingAccept()
            {
                var current = host;
                return current != null
                    && !current.IsClosed
                    && current.Server.ReleasePendingAccept();
            }

            public ValueTask DisposeAsync()
            {
                Interlocked.Exchange(ref host, null)?.Dispose();
                return default;
            }
        }
    }
}
