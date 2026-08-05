using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Nnrp.Core;

namespace Nnrp.NativeBridge
{
    public interface INnrpNativeTransportProvider
    {
        NnrpTransportProviderDescriptor Descriptor { get; }

        ValueTask<NnrpTransportConnection> ConnectAsync(
            NnrpTransportConnectOptions options,
            CancellationToken cancellationToken = default(CancellationToken));

        ValueTask<NnrpTransportListener> ListenAsync(
            NnrpTransportListenOptions options,
            CancellationToken cancellationToken = default(CancellationToken));

        ValueTask<NnrpTransportProbeMetrics> ProbeAsync(
            NnrpTransportProbeOptions options,
            CancellationToken cancellationToken = default(CancellationToken));
    }

    internal sealed class NnrpNativeEntrypointLease : IDisposable
    {
        private sealed class SharedState
        {
            internal SharedState(NnrpNativeRuntimeEntrypoints entrypoints)
            {
                Entrypoints = entrypoints;
                References = 1;
            }

            internal NnrpNativeRuntimeEntrypoints Entrypoints { get; }

            internal int References;
        }

        private SharedState? state;

        internal NnrpNativeEntrypointLease(NnrpNativeRuntimeEntrypoints entrypoints)
        {
            state = new SharedState(entrypoints ?? throw new ArgumentNullException(nameof(entrypoints)));
        }

        private NnrpNativeEntrypointLease(SharedState state)
        {
            this.state = state;
        }

        internal NnrpNativeRuntimeEntrypoints Entrypoints =>
            state?.Entrypoints ?? throw new ObjectDisposedException(nameof(NnrpNativeEntrypointLease));

        internal NnrpNativeEntrypointLease Retain()
        {
            var current = state ?? throw new ObjectDisposedException(nameof(NnrpNativeEntrypointLease));
            lock (current)
            {
                if (current.References == 0)
                {
                    throw new ObjectDisposedException(nameof(NnrpNativeEntrypointLease));
                }

                checked
                {
                    current.References++;
                }
            }

            return new NnrpNativeEntrypointLease(current);
        }

        public void Dispose()
        {
            var current = Interlocked.Exchange(ref state, null);
            if (current == null)
            {
                return;
            }

            lock (current)
            {
                current.References--;
                if (current.References == 0)
                {
                    current.Entrypoints.Dispose();
                }
            }
        }
    }

    public abstract class NnrpNativeTransportProvider : INnrpNativeTransportProvider
    {
        private readonly string transportScope;
        private readonly uint requiredTransportSlot;
        private readonly string? artifactPath;
        private readonly string? artifactRoot;
        private readonly NnrpNativePlatform? platform;
        private readonly Func<NnrpNativeRuntimeEntrypoints>? entrypointsFactory;

        protected NnrpNativeTransportProvider(
            NnrpTransportProviderDescriptor descriptor,
            string transportScope,
            uint requiredTransportSlot,
            string? artifactPath = null,
            string? artifactRoot = null,
            NnrpNativePlatform? platform = null)
        {
            Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
            if (string.IsNullOrWhiteSpace(transportScope))
            {
                throw new ArgumentException("Transport scope must not be empty.", nameof(transportScope));
            }

            if (requiredTransportSlot == 0 || SlotFor(descriptor.TransportId) != requiredTransportSlot)
            {
                throw new ArgumentException("Provider transport metadata does not match its native artifact slot.");
            }

            this.transportScope = transportScope;
            this.requiredTransportSlot = requiredTransportSlot;
            this.artifactPath = artifactPath;
            this.artifactRoot = artifactRoot;
            this.platform = platform;
        }

        internal NnrpNativeTransportProvider(
            NnrpTransportProviderDescriptor descriptor,
            string transportScope,
            uint requiredTransportSlot,
            Func<NnrpNativeRuntimeEntrypoints> entrypointsFactory)
            : this(descriptor, transportScope, requiredTransportSlot)
        {
            this.entrypointsFactory = entrypointsFactory
                ?? throw new ArgumentNullException(nameof(entrypointsFactory));
        }

        public NnrpTransportProviderDescriptor Descriptor { get; }

        public ValueTask<NnrpTransportConnection> ConnectAsync(
            NnrpTransportConnectOptions options,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            cancellationToken.ThrowIfCancellationRequested();
            ValidateEndpoint(options.ProviderEndpoint);
            ValidateSecurity(options.Endpoint, options.ProviderEndpoint, options.Security != null);
            var entrypoints = LoadEntrypoints();
            var handle = NnrpHandle.Invalid;
            try
            {
                WithClientSecurityConfig(
                    entrypoints,
                    options.Security,
                    config =>
                    {
                        handle = Open(
                            entrypoints.TransportConnect,
                            entrypoints,
                            options.ProviderEndpoint,
                            config,
                            options.MaxPacketBytes,
                            options.TimeoutMilliseconds,
                            NnrpHandleKind.TransportConnection);
                        return true;
                    });
                return new ValueTask<NnrpTransportConnection>(
                    new NnrpTransportConnection(
                        new NnrpNativeEntrypointLease(entrypoints),
                        Descriptor.TransportId,
                        handle));
            }
            catch
            {
                CloseAfterFailure(entrypoints, handle);
                entrypoints.Dispose();
                throw;
            }
        }

        public ValueTask<NnrpTransportListener> ListenAsync(
            NnrpTransportListenOptions options,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            cancellationToken.ThrowIfCancellationRequested();
            ValidateEndpoint(options.ProviderEndpoint);
            ValidateSecurity(options.Endpoint, options.ProviderEndpoint, options.Security != null);
            var entrypoints = LoadEntrypoints();
            var handle = NnrpHandle.Invalid;
            try
            {
                WithServerSecurityConfig(
                    entrypoints,
                    options.Security,
                    config =>
                    {
                        handle = Open(
                            entrypoints.TransportListen,
                            entrypoints,
                            options.ProviderEndpoint,
                            config,
                            options.MaxPacketBytes,
                            options.TimeoutMilliseconds,
                            NnrpHandleKind.TransportListener);
                        return true;
                    });
                var endpoint = ReadListenerEndpoint(entrypoints, handle);
                return new ValueTask<NnrpTransportListener>(
                    new NnrpTransportListener(
                        new NnrpNativeEntrypointLease(entrypoints),
                        Descriptor.TransportId,
                        endpoint,
                        handle));
            }
            catch
            {
                CloseAfterFailure(entrypoints, handle);
                entrypoints.Dispose();
                throw;
            }
        }

        public ValueTask<NnrpTransportProbeMetrics> ProbeAsync(
            NnrpTransportProbeOptions options,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            cancellationToken.ThrowIfCancellationRequested();
            ValidateEndpoint(options.ProviderEndpoint);
            ValidateSecurity(options.Endpoint, options.ProviderEndpoint, options.Security != null);
            using var entrypoints = LoadEntrypoints();
            var result = WithClientSecurityConfig(
                entrypoints,
                options.Security,
                config => WithEndpointView(
                    options.ProviderEndpoint,
                    endpointView =>
                    {
                        var open = new NnrpTransportOpenRequest(
                            Descriptor.TransportId,
                            endpointView,
                            config,
                            options.MaxPacketBytes,
                            options.TimeoutMilliseconds);
                        if (options.IncludeWarmup)
                        {
                            entrypoints.TransportProbe(
                                new NnrpTransportProbeRequest(open, 1, options.PayloadBytes),
                                out _).ThrowIfError();
                        }

                        entrypoints.TransportProbe(
                            new NnrpTransportProbeRequest(open, options.SampleCount, options.PayloadBytes),
                            out var probe).ThrowIfError();
                        return probe;
                    }));
            return new ValueTask<NnrpTransportProbeMetrics>(
                new NnrpTransportProbeMetrics(
                    result.SampleCount,
                    result.SuccessCount,
                    result.MedianThroughputBytesPerSecond,
                    result.MedianRttMicroseconds));
        }

        private NnrpNativeRuntimeEntrypoints LoadEntrypoints()
        {
            if (entrypointsFactory != null)
            {
                return entrypointsFactory();
            }

            return NnrpNativeRuntimeEntrypoints.Load(
                artifactPath,
                artifactRoot,
                platform,
                requiredTransportSlot,
                transportScope);
        }

        private void ValidateEndpoint(NnrpProviderEndpoint endpoint)
        {
            if (!endpoint.MatchesTransport(Descriptor.TransportId))
            {
                throw new ArgumentException(
                    "Provider endpoint does not match " + Descriptor.TransportId + ".",
                    nameof(endpoint));
            }
        }

        private void ValidateSecurity(
            NnrpEndpoint endpoint,
            NnrpProviderEndpoint providerEndpoint,
            bool hasSecurity)
        {
            if (!NnrpTransportRouteResolver.IsSecurityValid(
                endpoint,
                Descriptor.TransportId,
                providerEndpoint,
                hasSecurity))
            {
                throw new ArgumentException("Provider security does not satisfy the application endpoint.");
            }
        }

        private T WithClientSecurityConfig<T>(
            NnrpNativeRuntimeEntrypoints entrypoints,
            NnrpTransportClientSecurity? security,
            Func<NnrpHandle, T> action)
        {
            if (security == null)
            {
                return action(NnrpHandle.Invalid);
            }

            return WithUtf8View(
                security.ServerName,
                serverName => NnrpNativeRuntimeSession.WithBorrowedView(
                    security.TrustedCertificateDer,
                    certificate =>
                    {
                        entrypoints.TransportClientSecurityConfigCreate(
                            new NnrpTransportClientSecurityConfigRequest(
                                Descriptor.TransportId,
                                serverName,
                                certificate),
                            out var config).ThrowIfError();
                        try
                        {
                            config.RequireKind(NnrpHandleKind.TransportSecurityConfig);
                        }
                        catch
                        {
                            CloseAfterFailure(entrypoints, config);
                            throw;
                        }
                        Exception? failure = null;
                        try
                        {
                            return action(config);
                        }
                        catch (Exception error)
                        {
                            failure = error;
                            throw;
                        }
                        finally
                        {
                            var status = entrypoints.TransportClose(config);
                            if (failure == null)
                            {
                                status.ThrowIfError();
                            }
                        }
                    }));
        }

        private T WithServerSecurityConfig<T>(
            NnrpNativeRuntimeEntrypoints entrypoints,
            NnrpTransportServerSecurity? security,
            Func<NnrpHandle, T> action)
        {
            if (security == null)
            {
                return action(NnrpHandle.Invalid);
            }

            return NnrpNativeRuntimeSession.WithBorrowedView(
                security.CertificateDer,
                certificate => NnrpNativeRuntimeSession.WithBorrowedView(
                    security.PrivateKeyPkcs8Der,
                    privateKey =>
                    {
                        entrypoints.TransportServerSecurityConfigCreate(
                            new NnrpTransportServerSecurityConfigRequest(
                                Descriptor.TransportId,
                                certificate,
                                privateKey),
                            out var config).ThrowIfError();
                        try
                        {
                            config.RequireKind(NnrpHandleKind.TransportSecurityConfig);
                        }
                        catch
                        {
                            CloseAfterFailure(entrypoints, config);
                            throw;
                        }
                        Exception? failure = null;
                        try
                        {
                            return action(config);
                        }
                        catch (Exception error)
                        {
                            failure = error;
                            throw;
                        }
                        finally
                        {
                            var status = entrypoints.TransportClose(config);
                            if (failure == null)
                            {
                                status.ThrowIfError();
                            }
                        }
                    }));
        }

        private NnrpHandle Open(
            NnrpNativeRuntimeEntrypoints.TransportOpenInvoker invoker,
            NnrpNativeRuntimeEntrypoints entrypoints,
            NnrpProviderEndpoint endpoint,
            NnrpHandle config,
            ulong maxPacketBytes,
            uint timeoutMilliseconds,
            NnrpHandleKind expectedKind)
        {
            return WithEndpointView(
                endpoint,
                endpointView =>
                {
                    invoker(
                        new NnrpTransportOpenRequest(
                            Descriptor.TransportId,
                            endpointView,
                            config,
                            maxPacketBytes,
                            timeoutMilliseconds),
                        out var handle).ThrowIfError();
                    try
                    {
                        handle.RequireKind(expectedKind);
                    }
                    catch
                    {
                        CloseAfterFailure(entrypoints, handle);
                        throw;
                    }

                    return handle;
                });
        }

        private NnrpProviderEndpoint ReadListenerEndpoint(
            NnrpNativeRuntimeEntrypoints entrypoints,
            NnrpHandle listener)
        {
            entrypoints.TransportListenerEndpoint(listener, out var owner, out var view).ThrowIfError();
            try
            {
                if (view.Length == UIntPtr.Zero)
                {
                    throw new NnrpNativeArtifactException("Native listener returned an empty endpoint.");
                }

                if (view.Length.ToUInt64() > int.MaxValue)
                {
                    throw new NnrpNativeArtifactException("Native listener endpoint exceeds managed limits.");
                }

                var bytes = new byte[(int)view.Length.ToUInt64()];
                Marshal.Copy(view.Pointer, bytes, 0, bytes.Length);
                return NnrpProviderEndpoint.Parse(Encoding.UTF8.GetString(bytes));
            }
            finally
            {
                if (owner.IsValid)
                {
                    owner.RequireKind(NnrpHandleKind.Buffer);
                    entrypoints.BufferRelease(owner).ThrowIfError();
                }
            }
        }

        private static T WithEndpointView<T>(NnrpProviderEndpoint endpoint, Func<NnrpBufferView, T> action)
        {
            return WithUtf8View(endpoint.ToString(), action);
        }

        private static T WithUtf8View<T>(string value, Func<NnrpBufferView, T> action)
        {
            return NnrpNativeRuntimeSession.WithBorrowedView(Encoding.UTF8.GetBytes(value), action);
        }

        private static void CloseAfterFailure(
            NnrpNativeRuntimeEntrypoints entrypoints,
            NnrpHandle handle)
        {
            if (!handle.IsValid)
            {
                return;
            }

            try
            {
                entrypoints.TransportClose(handle);
            }
            catch
            {
            }
        }

        private static uint SlotFor(TransportId transportId)
        {
            return transportId switch
            {
                TransportId.Tcp => NnrpNativeArtifact.TransportSlotTcp,
                TransportId.Quic => NnrpNativeArtifact.TransportSlotQuic,
                TransportId.Ipc => NnrpNativeArtifact.TransportSlotIpc,
                TransportId.WebSocket => NnrpNativeArtifact.TransportSlotWebSocket,
                _ => 0,
            };
        }
    }

    public sealed class NnrpTransportConnection : IDisposable, IAsyncDisposable
    {
        private readonly object gate = new object();
        private NnrpNativeEntrypointLease? lease;
        private NnrpHandle handle;

        internal NnrpTransportConnection(
            NnrpNativeEntrypointLease lease,
            TransportId transportId,
            NnrpHandle handle)
        {
            handle.RequireKind(NnrpHandleKind.TransportConnection);
            this.lease = lease ?? throw new ArgumentNullException(nameof(lease));
            TransportId = transportId;
            this.handle = handle;
        }

        internal NnrpNativeRuntimeEntrypoints Entrypoints =>
            lease?.Entrypoints ?? throw new ObjectDisposedException(nameof(NnrpTransportConnection));

        public TransportId TransportId { get; }

        internal NnrpNativeRuntimeConnection AdoptClient(ulong connectionId, uint generation)
        {
            lock (gate)
            {
                EnsureOpen();
                Entrypoints.ClientConnect(
                    new NnrpClientConnectRequest(connectionId, generation, handle),
                    out var connection).ThrowIfError();
                handle = NnrpHandle.Invalid;
                var ownership = lease!;
                lease = null;
                return new NnrpNativeRuntimeConnection(
                    ownership.Entrypoints,
                    new NnrpConnectionHandle(connection),
                    ownership);
            }
        }

        public void Dispose()
        {
            lock (gate)
            {
                if (!handle.IsValid)
                {
                    return;
                }

                try
                {
                    Entrypoints.TransportClose(handle).ThrowIfError();
                }
                finally
                {
                    handle = NnrpHandle.Invalid;
                    lease?.Dispose();
                    lease = null;
                }
            }
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return default(ValueTask);
        }

        private void EnsureOpen()
        {
            if (!handle.IsValid)
            {
                throw new ObjectDisposedException(nameof(NnrpTransportConnection));
            }
        }
    }

    public sealed class NnrpTransportListener : IDisposable, IAsyncDisposable
    {
        private readonly object gate = new object();
        private NnrpNativeEntrypointLease? lease;
        private NnrpHandle handle;

        internal NnrpTransportListener(
            NnrpNativeEntrypointLease lease,
            TransportId transportId,
            NnrpProviderEndpoint endpoint,
            NnrpHandle handle)
        {
            handle.RequireKind(NnrpHandleKind.TransportListener);
            this.lease = lease ?? throw new ArgumentNullException(nameof(lease));
            TransportId = transportId;
            Endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
            this.handle = handle;
        }

        internal NnrpNativeRuntimeEntrypoints Entrypoints =>
            lease?.Entrypoints ?? throw new ObjectDisposedException(nameof(NnrpTransportListener));

        public TransportId TransportId { get; }

        public NnrpProviderEndpoint BoundEndpoint => Endpoint;

        internal NnrpProviderEndpoint Endpoint { get; }

        internal NnrpTransportConnection Accept(uint timeoutMilliseconds)
        {
            lock (gate)
            {
                EnsureOpen();
                Entrypoints.TransportAccept(
                    new NnrpTransportAcceptRequest(handle, timeoutMilliseconds),
                    out var connection).ThrowIfError();
                return new NnrpTransportConnection(lease!.Retain(), TransportId, connection);
            }
        }

        internal NnrpNativeRuntimeServer AdoptServer(NnrpNativeServerBindOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            lock (gate)
            {
                EnsureOpen();
                NnrpNativeServerPolicyDispatcher? dispatcher = null;
                NnrpNativeSchemaRegistry? schemaRegistry = null;
                GCHandle profilesOwner = default;
                GCHandle cacheObjectsOwner = default;
                try
                {
                    dispatcher = new NnrpNativeServerPolicyDispatcher(
                        Entrypoints,
                        options.ApplicationPolicy,
                        lease!.Retain());
                    schemaRegistry = NnrpNativeSchemaRegistry.Create(Entrypoints);
                    foreach (var descriptor in options.SchemaRegistry.SnapshotDescriptors())
                    {
                        schemaRegistry.Install(new NnrpSchemaDescriptorHeader(
                            descriptor.SchemaId,
                            descriptor.SchemaVersion,
                            descriptor.ProfileId,
                            descriptor.SchemaFlags,
                            descriptor.MinVersionMajor,
                            descriptor.MaxVersionMajor,
                            descriptor.BodyBytes,
                            descriptor.DependencyCount,
                            descriptor.DefaultStreamSemantics,
                            descriptor.SchemaHash));
                    }

                    var profiles = options.SupportedProfiles.ToArray();
                    var cacheObjects = options.SupportedCacheObjects.Select(value => (uint)value).ToArray();
                    profilesOwner = GCHandle.Alloc(profiles, GCHandleType.Pinned);
                    if (cacheObjects.Length != 0)
                    {
                        cacheObjectsOwner = GCHandle.Alloc(cacheObjects, GCHandleType.Pinned);
                    }

                    var profileSlice = new NnrpU16Slice(
                        profilesOwner.AddrOfPinnedObject(),
                        new UIntPtr((uint)profiles.Length));
                    var cacheObjectSlice = cacheObjects.Length == 0
                        ? NnrpU32Slice.Empty
                        : new NnrpU32Slice(
                            cacheObjectsOwner.AddrOfPinnedObject(),
                            new UIntPtr((uint)cacheObjects.Length));
                    Entrypoints.ServerBind(
                        new NnrpServerBindRequest(
                            options.ServerId,
                            options.Generation,
                            handle,
                            profileSlice,
                            cacheObjectSlice,
                            options.MaxCacheObjects,
                            options.MaxCacheObjectBytes,
                            options.ResumeTokenBytes,
                            options.MaxInFlightOperations,
                            options.GrantedOperationCredit,
                            options.LeaseTtlMilliseconds,
                            options.ResumeWindowMilliseconds,
                            schemaRegistry.Handle.Handle,
                            dispatcher.Sink),
                        out var server).ThrowIfError();
                    handle = NnrpHandle.Invalid;
                    var ownership = lease!;
                    lease = null;
                    var result = new NnrpNativeRuntimeServer(
                        ownership.Entrypoints,
                        new NnrpConnectionHandle(server),
                        dispatcher,
                        schemaRegistry,
                        ownership);
                    dispatcher = null;
                    schemaRegistry = null;
                    return result;
                }
                finally
                {
                    if (profilesOwner.IsAllocated)
                    {
                        profilesOwner.Free();
                    }

                    if (cacheObjectsOwner.IsAllocated)
                    {
                        cacheObjectsOwner.Free();
                    }

                    dispatcher?.Dispose();
                    schemaRegistry?.Dispose();
                }
            }
        }

        public void Dispose()
        {
            lock (gate)
            {
                if (!handle.IsValid)
                {
                    return;
                }

                try
                {
                    Entrypoints.TransportClose(handle).ThrowIfError();
                }
                finally
                {
                    handle = NnrpHandle.Invalid;
                    lease?.Dispose();
                    lease = null;
                }
            }
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return default(ValueTask);
        }

        private void EnsureOpen()
        {
            if (!handle.IsValid)
            {
                throw new ObjectDisposedException(nameof(NnrpTransportListener));
            }
        }
    }

    public sealed class NnrpNativeTransportResolution
    {
        internal NnrpNativeTransportResolution(
            INnrpNativeTransportProvider selectedProvider,
            INnrpNativeTransportProvider[] availableProviders,
            bool shouldProbe)
        {
            SelectedProvider = selectedProvider ?? throw new ArgumentNullException(nameof(selectedProvider));
            AvailableProviders = availableProviders ?? throw new ArgumentNullException(nameof(availableProviders));
            ShouldProbe = shouldProbe;
        }

        public INnrpNativeTransportProvider SelectedProvider { get; }

        public INnrpNativeTransportProvider[] AvailableProviders { get; }

        public bool ShouldProbe { get; }

        public uint TransportId => (uint)SelectedProvider.Descriptor.TransportId;
    }

    public static class NnrpNativeTransportResolver
    {
        public static NnrpNativeTransportResolution Resolve(
            NnrpNativeProbeResult probeResult,
            IEnumerable<INnrpNativeTransportProvider> providers,
            TransportPolicy policy = TransportPolicy.Auto)
        {
            if (providers == null)
            {
                throw new ArgumentNullException(nameof(providers));
            }

            var available = ValidateProviders(providers)
                .Where(provider => provider.Descriptor.Available)
                .Where(provider => (probeResult.TransportSlots & SlotFor(provider.Descriptor.TransportId)) != 0)
                .OrderBy(provider => provider.Descriptor.Metadata.PreferenceRank)
                .ThenBy(provider => provider.Descriptor.Metadata.Id, StringComparer.Ordinal)
                .ToArray();
            if (available.Length == 0)
            {
                throw new InvalidOperationException("No installed native transport provider matches the native artifact transport slots.");
            }

            var preferred = ResolvePreferredTransport(policy);
            var selected = preferred == TransportId.Unspecified
                ? available[0]
                : available.FirstOrDefault(provider => provider.Descriptor.TransportId == preferred);
            if (selected == null)
            {
                throw new InvalidOperationException($"No installed native transport provider is available for {preferred}.");
            }

            return new NnrpNativeTransportResolution(
                selected,
                available,
                ShouldProbe(policy) && available.Length > 1);
        }

        private static INnrpNativeTransportProvider[] ValidateProviders(
            IEnumerable<INnrpNativeTransportProvider> providers)
        {
            var providerArray = providers.ToArray();
            if (providerArray.Length == 0)
            {
                throw new ArgumentException("At least one native transport provider must be supplied.", nameof(providers));
            }

            var transports = new HashSet<TransportId>();
            var providerIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var provider in providerArray)
            {
                if (provider == null)
                {
                    throw new ArgumentException("Native transport providers must not contain null entries.", nameof(providers));
                }

                if (!transports.Add(provider.Descriptor.TransportId))
                {
                    throw new ArgumentException(
                        $"Duplicate native transport provider id {provider.Descriptor.TransportId} is not allowed.",
                        nameof(providers));
                }

                if (!providerIds.Add(provider.Descriptor.Metadata.Id))
                {
                    throw new ArgumentException(
                        $"Duplicate native provider metadata id {provider.Descriptor.Metadata.Id} is not allowed.",
                        nameof(providers));
                }
            }

            return providerArray;
        }

        private static bool ShouldProbe(TransportPolicy policy)
        {
            return policy switch
            {
                TransportPolicy.Auto => true,
                TransportPolicy.PreferQuic => true,
                TransportPolicy.PreferTcp => true,
                TransportPolicy.PreferIpc => true,
                TransportPolicy.PreferWebSocket => true,
                TransportPolicy.ForceQuic => false,
                TransportPolicy.ForceTcp => false,
                TransportPolicy.ForceIpc => false,
                TransportPolicy.ForceWebSocket => false,
                _ => throw new ArgumentOutOfRangeException(nameof(policy), policy, "Unknown transport policy."),
            };
        }

        private static TransportId ResolvePreferredTransport(TransportPolicy policy)
        {
            return policy switch
            {
                TransportPolicy.Auto => TransportId.Unspecified,
                TransportPolicy.PreferQuic => TransportId.Quic,
                TransportPolicy.PreferTcp => TransportId.Tcp,
                TransportPolicy.PreferIpc => TransportId.Ipc,
                TransportPolicy.PreferWebSocket => TransportId.WebSocket,
                TransportPolicy.ForceQuic => TransportId.Quic,
                TransportPolicy.ForceTcp => TransportId.Tcp,
                TransportPolicy.ForceIpc => TransportId.Ipc,
                TransportPolicy.ForceWebSocket => TransportId.WebSocket,
                _ => throw new ArgumentOutOfRangeException(nameof(policy), policy, "Unknown transport policy."),
            };
        }

        private static uint SlotFor(TransportId transportId)
        {
            return transportId switch
            {
                TransportId.Tcp => NnrpNativeArtifact.TransportSlotTcp,
                TransportId.Quic => NnrpNativeArtifact.TransportSlotQuic,
                TransportId.Ipc => NnrpNativeArtifact.TransportSlotIpc,
                TransportId.WebSocket => NnrpNativeArtifact.TransportSlotWebSocket,
                _ => 0,
            };
        }
    }
}
