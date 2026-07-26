using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Nnrp.Core;
using Xunit;

namespace Nnrp.NativeBridge.Tests
{
    public sealed class NnrpNativeTransportProviderTests
    {
        private static readonly ConcurrentDictionary<ulong, IntPtr> EndpointBuffers =
            new ConcurrentDictionary<ulong, IntPtr>();
        private static long endpointBufferId;

        [Fact]
        public async Task ProviderUsesCoarseNativeConnectListenAndProbeOperations()
        {
            var closed = new List<NnrpHandle>();
            var probeRequests = new List<NnrpTransportProbeRequest>();
            var releasedBuffers = 0;
            var provider = CreateProvider(() =>
                CreateTransportEntrypoints(
                    transportConnect: (NnrpTransportOpenRequest request, out NnrpHandle handle) =>
                    {
                        Assert.Equal((uint)TransportId.Tcp, request.TransportId);
                        Assert.Equal((ulong)4096, request.MaxPacketBytes);
                        handle = new NnrpHandle(NnrpHandleKind.TransportConnection, 10, 1);
                        return NnrpFfiStatus.Ok;
                    },
                    transportListen: (NnrpTransportOpenRequest request, out NnrpHandle handle) =>
                    {
                        Assert.Equal((uint)20, request.TimeoutMilliseconds);
                        handle = new NnrpHandle(NnrpHandleKind.TransportListener, 11, 1);
                        return NnrpFfiStatus.Ok;
                    },
                    transportProbe: (NnrpTransportProbeRequest request, out NnrpTransportProbeResult result) =>
                    {
                        probeRequests.Add(request);
                        result = new NnrpTransportProbeResult(
                            request.SampleCount,
                            request.SampleCount,
                            2_000_000,
                            75);
                        return NnrpFfiStatus.Ok;
                    },
                    transportClose: handle =>
                    {
                        closed.Add(handle);
                        return NnrpFfiStatus.Ok;
                    },
                    bufferRelease: _ =>
                    {
                        releasedBuffers++;
                        return NnrpFfiStatus.Ok;
                    }));
            var endpoint = NnrpEndpoint.Parse("nnrp://127.0.0.1:7443");
            var providerEndpoint = NnrpProviderEndpoint.Parse("tcp://127.0.0.1:7443");

            await using (var connection = await provider.ConnectAsync(
                new NnrpTransportConnectOptions(endpoint, providerEndpoint, maxPacketBytes: 4096)))
            {
                Assert.Equal(TransportId.Tcp, connection.TransportId);
            }

            await using (var listener = await provider.ListenAsync(
                new NnrpTransportListenOptions(endpoint, providerEndpoint, timeoutMilliseconds: 20)))
            {
                Assert.Equal(providerEndpoint, listener.BoundEndpoint);
                Assert.Equal(TransportId.Tcp, listener.TransportId);
            }

            var metrics = await provider.ProbeAsync(
                new NnrpTransportProbeOptions(
                    endpoint,
                    providerEndpoint,
                    sampleCount: 3,
                    payloadBytes: 64,
                    includeWarmup: true));

            Assert.Equal((uint)3, metrics.SampleCount);
            Assert.Equal((ulong)2_000_000, metrics.MedianThroughputBytesPerSecond);
            Assert.Equal(new uint[] { 1, 3 }, probeRequests.ConvertAll(value => value.SampleCount));
            Assert.Contains(closed, value => value.Kind == NnrpHandleKind.TransportConnection);
            Assert.Contains(closed, value => value.Kind == NnrpHandleKind.TransportListener);
            Assert.Equal(1, releasedBuffers);
        }

        [Fact]
        public async Task ProviderOwnsRouteLocalSecurityConfigLifetime()
        {
            var closed = new List<NnrpHandle>();
            var clientConfigs = 0;
            var serverConfigs = 0;
            var provider = CreateProvider(() =>
                CreateTransportEntrypoints(
                    transportClientSecurityConfigCreate:
                        (NnrpTransportClientSecurityConfigRequest request, out NnrpHandle config) =>
                        {
                            clientConfigs++;
                            Assert.Equal((uint)TransportId.Tcp, request.TransportId);
                            config = new NnrpHandle(NnrpHandleKind.TransportSecurityConfig, 20, 1);
                            return NnrpFfiStatus.Ok;
                        },
                    transportServerSecurityConfigCreate:
                        (NnrpTransportServerSecurityConfigRequest request, out NnrpHandle config) =>
                        {
                            serverConfigs++;
                            Assert.Equal((uint)TransportId.Tcp, request.TransportId);
                            config = new NnrpHandle(NnrpHandleKind.TransportSecurityConfig, 21, 1);
                            return NnrpFfiStatus.Ok;
                        },
                    transportClose: handle =>
                    {
                        closed.Add(handle);
                        return NnrpFfiStatus.Ok;
                    }));
            var endpoint = NnrpEndpoint.Parse("nnrps://runtime.example:7443");
            var providerEndpoint = NnrpProviderEndpoint.Parse("tcp://runtime.example:7443");

            await using (var connection = await provider.ConnectAsync(
                new NnrpTransportConnectOptions(
                    endpoint,
                    providerEndpoint,
                    new NnrpTransportClientSecurity("runtime.example", new byte[] { 1 }))))
            {
                Assert.Equal(TransportId.Tcp, connection.TransportId);
            }

            await using (var listener = await provider.ListenAsync(
                new NnrpTransportListenOptions(
                    endpoint,
                    providerEndpoint,
                    new NnrpTransportServerSecurity(new byte[] { 2 }, new byte[] { 3 }))))
            {
                Assert.Equal(
                    NnrpProviderEndpoint.Parse("tcp://127.0.0.1:7443"),
                    listener.BoundEndpoint);
            }

            Assert.Equal(1, clientConfigs);
            Assert.Equal(1, serverConfigs);
            Assert.Equal(2, closed.FindAll(value => value.Kind == NnrpHandleKind.TransportSecurityConfig).Count);
        }

        [Fact]
        public void ProviderRejectsInvalidInputsBeforeLoadingNativeCode()
        {
            var loads = 0;
            var provider = CreateProvider(() =>
            {
                loads++;
                return CreateTransportEntrypoints();
            });
            var endpoint = NnrpEndpoint.Parse("nnrp://127.0.0.1:7443");
            var secureEndpoint = NnrpEndpoint.Parse("nnrps://127.0.0.1:7443");
            var tcp = NnrpProviderEndpoint.Parse("tcp://127.0.0.1:7443");
            var quic = NnrpProviderEndpoint.Parse("quic://127.0.0.1:7443");
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();

            Assert.Throws<ArgumentNullException>(() => provider.ConnectAsync(null!));
            Assert.Throws<ArgumentNullException>(() => provider.ListenAsync(null!));
            Assert.Throws<ArgumentNullException>(() => provider.ProbeAsync(null!));
            Assert.Throws<OperationCanceledException>(() =>
                provider.ConnectAsync(new NnrpTransportConnectOptions(endpoint, tcp), cancelled.Token));
            Assert.Throws<ArgumentException>(() =>
                provider.ConnectAsync(new NnrpTransportConnectOptions(endpoint, quic)));
            Assert.Throws<ArgumentException>(() =>
                provider.ConnectAsync(new NnrpTransportConnectOptions(secureEndpoint, tcp)));
            Assert.Equal(0, loads);
        }

        [Fact]
        public void ProviderClosesNativeHandleThatFailsKindValidation()
        {
            var closed = new List<NnrpHandle>();
            var provider = CreateProvider(() =>
                CreateTransportEntrypoints(
                    transportConnect: (NnrpTransportOpenRequest _, out NnrpHandle handle) =>
                    {
                        handle = new NnrpHandle(NnrpHandleKind.Buffer, 30, 1);
                        return NnrpFfiStatus.Ok;
                    },
                    transportClose: handle =>
                    {
                        closed.Add(handle);
                        return NnrpFfiStatus.Ok;
                    }));

            Assert.Throws<ArgumentException>(() =>
                provider.ConnectAsync(
                    new NnrpTransportConnectOptions(
                        NnrpEndpoint.Parse("nnrp://127.0.0.1:7443"),
                        NnrpProviderEndpoint.Parse("tcp://127.0.0.1:7443"))));
            Assert.Single(closed);
            Assert.Equal(NnrpHandleKind.Buffer, closed[0].Kind);
        }

        [Fact]
        public void ProviderConstructorValidatesScopeAndEveryTransportSlot()
        {
            var factory = new Func<NnrpNativeRuntimeEntrypoints>(() => CreateTransportEntrypoints());
            Assert.Throws<ArgumentException>(() =>
                new TestProvider(Descriptor(TransportId.Tcp), " ", NnrpNativeArtifact.TransportSlotTcp, factory));
            Assert.Throws<ArgumentException>(() =>
                new TestProvider(Descriptor(TransportId.Tcp), "tcp", NnrpNativeArtifact.TransportSlotQuic, factory));

            Assert.Equal(
                TransportId.Quic,
                new TestProvider(Descriptor(TransportId.Quic), "quic", NnrpNativeArtifact.TransportSlotQuic, factory)
                    .Descriptor.TransportId);
            Assert.Equal(
                TransportId.Ipc,
                new TestProvider(Descriptor(TransportId.Ipc), "ipc", NnrpNativeArtifact.TransportSlotIpc, factory)
                    .Descriptor.TransportId);
            Assert.Equal(
                TransportId.WebSocket,
                new TestProvider(
                    Descriptor(TransportId.WebSocket),
                    "websocket",
                    NnrpNativeArtifact.TransportSlotWebSocket,
                    factory).Descriptor.TransportId);
        }

        [Fact]
        public void EntrypointLeaseRejectsUseAfterDispose()
        {
            var lease = new NnrpNativeEntrypointLease(CreateTransportEntrypoints());

            lease.Dispose();

            Assert.Throws<ObjectDisposedException>(() => _ = lease.Entrypoints);
            Assert.Throws<ObjectDisposedException>(() => lease.Retain());
            lease.Dispose();
        }

        private static TestProvider CreateProvider(Func<NnrpNativeRuntimeEntrypoints> entrypointsFactory)
        {
            return new TestProvider(Descriptor(TransportId.Tcp), entrypointsFactory);
        }

        private static NnrpTransportProviderDescriptor Descriptor(TransportId transportId)
        {
            var id = transportId.ToString().ToLowerInvariant();
            return new NnrpTransportProviderDescriptor(
                id,
                "1",
                transportId,
                NnrpTransportProviderKind.NativeDynamic,
                true,
                null,
                new NnrpTransportProviderMetadata(
                    id,
                    default,
                    0,
                    new NnrpTransportProviderLimits(16 * 1024 * 1024),
                    Array.Empty<NnrpTransportProviderLimitation>()));
        }

        private static NnrpNativeRuntimeEntrypoints CreateTransportEntrypoints(
            NnrpNativeRuntimeEntrypoints.TransportSecurityConfigCreateInvoker? transportClientSecurityConfigCreate = null,
            NnrpNativeRuntimeEntrypoints.TransportServerSecurityConfigCreateInvoker? transportServerSecurityConfigCreate = null,
            NnrpNativeRuntimeEntrypoints.TransportOpenInvoker? transportConnect = null,
            NnrpNativeRuntimeEntrypoints.TransportOpenInvoker? transportListen = null,
            NnrpNativeRuntimeEntrypoints.TransportProbeInvoker? transportProbe = null,
            NnrpNativeRuntimeEntrypoints.HandleStatusInvoker? transportClose = null,
            NnrpNativeRuntimeEntrypoints.HandleStatusInvoker? bufferRelease = null)
        {
            return NnrpNativeArtifactTests.CreateTransportEntrypointsForTests(
                transportClientSecurityConfigCreate,
                transportServerSecurityConfigCreate,
                transportConnect,
                transportListen,
                ListenerEndpoint,
                transportProbe,
                transportClose,
                handle =>
                {
                    ReleaseEndpointBuffer(handle);
                    return bufferRelease?.Invoke(handle) ?? NnrpFfiStatus.Ok;
                });
        }

        private static NnrpFfiStatus ListenerEndpoint(
            NnrpHandle listener,
            out NnrpHandle buffer,
            out NnrpBufferView endpoint)
        {
            Assert.Equal(NnrpHandleKind.TransportListener, listener.Kind);
            var bytes = Encoding.UTF8.GetBytes("tcp://127.0.0.1:7443");
            var pointer = Marshal.AllocHGlobal(bytes.Length);
            Marshal.Copy(bytes, 0, pointer, bytes.Length);
            var id = checked((ulong)Interlocked.Increment(ref endpointBufferId));
            EndpointBuffers.TryAdd(id, pointer);
            buffer = new NnrpHandle(NnrpHandleKind.Buffer, id, 1);
            endpoint = new NnrpBufferView(pointer, new UIntPtr((uint)bytes.Length));
            return NnrpFfiStatus.Ok;
        }

        private static void ReleaseEndpointBuffer(NnrpHandle handle)
        {
            if (EndpointBuffers.TryRemove(handle.Id, out var pointer))
            {
                Marshal.FreeHGlobal(pointer);
            }
        }

        private sealed class TestProvider : NnrpNativeTransportProvider
        {
            internal TestProvider(
                NnrpTransportProviderDescriptor descriptor,
                Func<NnrpNativeRuntimeEntrypoints> entrypointsFactory)
                : base(
                    descriptor,
                    "tcp",
                    NnrpNativeArtifact.TransportSlotTcp,
                    entrypointsFactory)
            {
            }

            internal TestProvider(
                NnrpTransportProviderDescriptor descriptor,
                string transportScope,
                uint requiredTransportSlot,
                Func<NnrpNativeRuntimeEntrypoints> entrypointsFactory)
                : base(descriptor, transportScope, requiredTransportSlot, entrypointsFactory)
            {
            }
        }
    }
}
