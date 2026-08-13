using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Nnrp.Client;
using Nnrp.Core;
using Nnrp.NativeBridge;
using Nnrp.Runtime;
using Nnrp.WireConformance;
using Xunit;

namespace Nnrp.Core.Tests;

public sealed class WireTargetHostTests
{
    [Fact]
    public async Task HostRejectsRootedScenarioManifestPaths()
    {
        using TemporaryDirectory temporary = new();
        string externalManifest = Path.Combine(Path.GetTempPath(), $"nnrp-cases-{Guid.NewGuid():N}.json");
        string suitePath = WriteSuiteManifest(temporary.Path, externalManifest);

        await Assert.ThrowsAsync<InvalidDataException>(() => new WireTargetHost(new FakeWireTargetSdk(0)).RunAsync(
            new WireTargetHostOptions(
                Path.Combine(temporary.Path, "target.json"),
                Path.Combine(temporary.Path, "native-root"),
                suitePath)));
    }

    [Fact]
    public async Task HostRejectsScenarioManifestTraversal()
    {
        using TemporaryDirectory temporary = new();
        string suiteDirectory = Path.Combine(temporary.Path, "suite");
        Directory.CreateDirectory(suiteDirectory);
        string suitePath = WriteSuiteManifest(suiteDirectory, "../outside.json");

        await Assert.ThrowsAsync<InvalidDataException>(() => new WireTargetHost(new FakeWireTargetSdk(0)).RunAsync(
            new WireTargetHostOptions(
                Path.Combine(temporary.Path, "target.json"),
                Path.Combine(temporary.Path, "native-root"),
                suitePath)));
    }

    [Fact]
    public async Task HostRunsAllFrozenFrameScenariosAndWritesAtomicManifest()
    {
        using TemporaryDirectory temporary = new();
        string artifactRoot = Path.Combine(temporary.Path, "native-root");
        string manifestPath = Path.Combine(temporary.Path, "target.json");
        string suitePath = WriteSuite(temporary.Path, includeDeadlineBeforeSubmit: true);
        FakeWireTargetSdk sdk = new(connectFailures: 1);

        await new WireTargetHost(sdk).RunAsync(
            new WireTargetHostOptions(manifestPath, artifactRoot, suitePath));

        Assert.Equal(artifactRoot, sdk.ValidatedArtifactRoot);
        Assert.Equal(
            new[] { TransportId.Tcp, TransportId.Quic, TransportId.Ipc },
            sdk.ListenTransports);
        Assert.Equal(
            new[] { TransportId.Tcp, TransportId.Tcp, TransportId.WebSocket },
            sdk.ConnectAttempts);
        Assert.All(sdk.Operations, operation => Assert.True(operation.TerminalSent));
        Assert.Equal(
            new[]
            {
                NnrpResultDropReasonCode.PeerCancelled,
                NnrpResultDropReasonCode.Superseded,
                NnrpResultDropReasonCode.PeerCancelled,
            },
            sdk.Operations
                .Where(operation => operation.DropReasonCode.HasValue)
                .Select(operation => operation.DropReasonCode!.Value));
        Assert.All(sdk.ServerSessions, session => Assert.True(session.Disposed));
        Assert.All(sdk.ClientSessions, session => Assert.True(session.Disposed));
        Assert.False(File.Exists($"{manifestPath}.tmp"));
        Assert.True(File.Exists(Path.Combine(temporary.Path, "certs", "server.der")));
        Assert.True(File.Exists(Path.Combine(temporary.Path, "certs", "server-key.der")));

        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        JsonElement wire = manifest.RootElement.GetProperty("wire_conformance");
        Assert.Equal(3, wire.GetProperty("modes").GetArrayLength());
        Assert.Equal(4, wire.GetProperty("transports").GetArrayLength());
        Assert.Contains(
            wire.GetProperty("capabilities").EnumerateArray().Select(value => value.GetString()),
            value => value == NnrpPreview4CapabilityTokens.ControlResultDropReason);
    }

    [Fact]
    public void HostOptionsRequireManifestAndNormalizePaths()
    {
        using TemporaryDirectory temporary = new();
        WireTargetHostOptions options = WireTargetHostCommand.Parse(
            [
                "--manifest", Path.Combine(temporary.Path, "target.json"),
                "--artifact-root", temporary.Path,
                "--suite", Path.Combine(temporary.Path, "suite.json"),
            ]);

        Assert.Equal(Path.Combine(temporary.Path, "target.json"), options.ManifestPath);
        Assert.Equal(temporary.Path, options.ArtifactRoot);
        Assert.Equal(Path.Combine(temporary.Path, "suite.json"), options.SuitePath);
        Assert.Throws<ArgumentException>(() => WireTargetHostCommand.Parse([]));
        Assert.Throws<ArgumentException>(() => WireTargetHostCommand.Parse(["--manifest"]));
        Assert.Throws<ArgumentException>(() => WireTargetHostCommand.Parse(["--other", "value"]));
        Assert.Throws<ArgumentException>(() => WireTargetHostCommand.Parse(
            ["--manifest", "one.json", "--manifest", "two.json"]));
        ArgumentException positional = Assert.Throws<ArgumentException>(
            () => WireTargetHostCommand.Parse(["target.json", "value"]));
        Assert.Contains("Expected an option", positional.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CommandDispatchReportsInvalidLiveTargetArguments()
    {
        StringWriter error = new();
        int result = await WireConformanceCommand.RunAsync(
            ["serve-target", "--artifact-root", "."],
            TextWriter.Null,
            error);

        Assert.Equal(2, result);
        Assert.Contains("--manifest", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void OperationEventsExposeExactlyOneVariant()
    {
        WireTargetReceivedEvent runtimeEvent = new(MessageType.SessionClose);
        WireTargetLifecycleEvent lifecycleEvent = new(41, NnrpOperationState.Cancelled);

        WireTargetOperationEvent runtime = WireTargetOperationEvent.FromRuntime(runtimeEvent);
        WireTargetOperationEvent lifecycle = WireTargetOperationEvent.FromLifecycle(lifecycleEvent);

        Assert.Same(runtimeEvent, runtime.Runtime);
        Assert.Null(runtime.Lifecycle);
        Assert.Null(lifecycle.Runtime);
        Assert.Equal(lifecycleEvent, lifecycle.Lifecycle);
        Assert.Throws<ArgumentNullException>(() => WireTargetOperationEvent.FromRuntime(null!));
    }

    [Fact]
    public async Task CancelHandlerRejectsAnotherOperationIdentity()
    {
        FakeOperation operation = new(41, 4);
        FakeServerSession session = new(
            operation,
            [],
            operationEvents:
            [
                WireTargetOperationEvent.FromRuntime(
                    new WireTargetReceivedEvent(
                        MessageType.Cancel,
                        new ControlRequestMetadata(42, 1, 1, RuntimeRole.Client, 0, 0))),
                WireTargetOperationEvent.FromLifecycle(
                    new WireTargetLifecycleEvent(41, NnrpOperationState.Cancelled)),
            ]);

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => WireTargetHost.HandleCancelAsync(session, CancellationToken.None));

        Assert.Contains("another operation", error.Message, StringComparison.Ordinal);
        Assert.True(session.Disposed);
    }

    [Fact]
    public async Task CancelHandlerReportsObservedLifecycleIdentityAndState()
    {
        FakeOperation operation = new(41, 4);
        FakeServerSession session = new(
            operation,
            [],
            operationEvents:
            [
                WireTargetOperationEvent.FromRuntime(
                    new WireTargetReceivedEvent(
                        MessageType.Cancel,
                        new ControlRequestMetadata(41, 1, 1, RuntimeRole.Client, 0, 0))),
                WireTargetOperationEvent.FromLifecycle(
                    new WireTargetLifecycleEvent(42, NnrpOperationState.Completed)),
            ]);

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => WireTargetHost.HandleCancelAsync(session, CancellationToken.None));

        Assert.Contains("expected Cancelled for operation 41", error.Message, StringComparison.Ordinal);
        Assert.Contains("received Completed for operation 42", error.Message, StringComparison.Ordinal);
        Assert.True(session.Disposed);
    }

    [Fact]
    public async Task PriorityHandlerRejectsNonCanonicalExpiration()
    {
        FakeOperation operation = new(51, 5);
        FakeServerSession session = new(
            operation,
            [
                new WireTargetReceivedEvent(
                    MessageType.PriorityUpdate,
                    new SchedulingMetadata(51, 1, 10, 0, 0, 0)),
                new WireTargetReceivedEvent(
                    MessageType.ExpireAt,
                    new SchedulingMetadata(51, 2, 10, 0, 2, 0)),
            ]);

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => WireTargetHost.HandlePriorityAsync(session, CancellationToken.None));

        Assert.Contains("Priority/deadline", error.Message, StringComparison.Ordinal);
        Assert.True(session.Disposed);
    }

    [Fact]
    public void NativeSdkRejectsMissingTransportArtifactsBeforeManifestPublication()
    {
        using TemporaryDirectory temporary = new();

        Assert.Throws<NnrpNativeArtifactException>(
            () => new NnrpWireTargetSdk().ValidateArtifacts(temporary.Path));
    }

    [Fact]
    public async Task ProgressClientDoesNotRetryPermanentConfigurationFailure()
    {
        InvalidOperationException expected = new("invalid route configuration");
        FakeWireTargetSdk sdk = new(connectFailures: 1, connectFailure: expected);

        InvalidOperationException actual = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new WireTargetHost(sdk).HandleProgressClientAsync(
                TransportId.Tcp,
                NnrpEndpoint.Parse("nnrp://127.0.0.1:1/session/default"),
                NnrpProviderEndpoint.Parse("tcp://127.0.0.1:1"),
                null,
                CancellationToken.None));

        Assert.Same(expected, actual);
        Assert.Single(sdk.ConnectAttempts);
    }

    private sealed class FakeWireTargetSdk : IWireTargetSdk
    {
        private readonly int connectFailures;
        private readonly Exception? connectFailure;
        private int observedConnectFailures;

        internal FakeWireTargetSdk(int connectFailures, Exception? connectFailure = null)
        {
            this.connectFailures = connectFailures;
            this.connectFailure = connectFailure;
        }

        internal string? ValidatedArtifactRoot { get; private set; }

        internal List<TransportId> ListenTransports { get; } = [];

        internal List<TransportId> ConnectAttempts { get; } = [];

        internal List<FakeOperation> Operations { get; } = [];

        internal List<FakeServerSession> ServerSessions { get; } = [];

        internal List<FakeClientSession> ClientSessions { get; } = [];

        public void ValidateArtifacts(string artifactRoot)
        {
            ValidatedArtifactRoot = artifactRoot;
        }

        public ValueTask<IWireTargetServer> ListenAsync(
            TransportId transportId,
            NnrpEndpoint endpoint,
            NnrpProviderEndpoint providerEndpoint,
            NnrpTransportServerSecurity? security,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ListenTransports.Add(transportId);
            Queue<IWireTargetServerSession> sessions = transportId switch
            {
                TransportId.Tcp => new Queue<IWireTargetServerSession>(
                    [CreateCancelSession(101), CreateDeadlineSession()]),
                TransportId.Quic => new Queue<IWireTargetServerSession>(
                    [CreatePrioritySession(201), CreateCacheSession(202)]),
                TransportId.Ipc => new Queue<IWireTargetServerSession>([CreateCancelSession(301)]),
                _ => throw new ArgumentOutOfRangeException(nameof(transportId)),
            };
            return ValueTask.FromResult<IWireTargetServer>(
                new FakeServer(providerEndpoint, sessions));
        }

        public ValueTask<IWireTargetClient> ConnectAsync(
            TransportId transportId,
            NnrpEndpoint endpoint,
            NnrpProviderEndpoint providerEndpoint,
            NnrpTransportClientSecurity? security,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ConnectAttempts.Add(transportId);
            if (observedConnectFailures++ < connectFailures)
            {
                throw connectFailure ?? new IOException("suite listener is not ready");
            }

            FakeClientSession session = CreateProgressSession();
            ClientSessions.Add(session);
            return ValueTask.FromResult<IWireTargetClient>(new FakeClient(session));
        }

        private FakeServerSession CreateCancelSession(ulong operationId)
        {
            FakeOperation operation = Track(new FakeOperation(operationId, (uint)operationId));
            WireTargetOperationEvent runtime = WireTargetOperationEvent.FromRuntime(
                new WireTargetReceivedEvent(
                    MessageType.Cancel,
                    new ControlRequestMetadata(operationId, 1, 1, RuntimeRole.Client, 0, 0)));
            WireTargetOperationEvent lifecycle = WireTargetOperationEvent.FromLifecycle(
                new WireTargetLifecycleEvent(operationId, NnrpOperationState.Cancelled));
            return Track(new FakeServerSession(
                operation,
                [new WireTargetReceivedEvent(MessageType.SessionClose)],
                operationEvents: operationId == 101
                    ? [runtime, lifecycle]
                    : [lifecycle, runtime]));
        }

        private FakeServerSession CreatePrioritySession(ulong operationId)
        {
            FakeOperation operation = Track(new FakeOperation(operationId, (uint)operationId));
            return Track(new FakeServerSession(
                operation,
                [
                    new WireTargetReceivedEvent(
                        MessageType.PriorityUpdate,
                        new SchedulingMetadata(operationId, 1, 10, 0, 0, 0)),
                    new WireTargetReceivedEvent(
                        MessageType.ExpireAt,
                        new SchedulingMetadata(operationId, 2, 10, 0, 1, 0)),
                    new WireTargetReceivedEvent(MessageType.SessionClose),
                ],
                [new WireTargetLifecycleEvent(operationId, NnrpOperationState.Superseded)],
                lifecycleExpectedAfterTerminal: true));
        }

        private FakeServerSession CreateDeadlineSession()
        {
            FakeOperation operation = Track(new FakeOperation(151, 1));
            return Track(new FakeServerSession(
                operation,
                [
                    new WireTargetReceivedEvent(
                        MessageType.Deadline,
                        new SchedulingMetadata(151, 1, 0, 0, 4_000_000_000_000, 0)),
                    new WireTargetReceivedEvent(MessageType.SessionClose),
                ],
                [new WireTargetLifecycleEvent(151, NnrpOperationState.Completed)],
                lifecycleExpectedAfterTerminal: true));
        }

        private FakeServerSession CreateCacheSession(ulong operationId)
        {
            FakeOperation operation = Track(new FakeOperation(operationId, (uint)operationId));
            return Track(new FakeServerSession(
                operation,
                [
                    new WireTargetReceivedEvent(
                        MessageType.CapabilityNegotiation,
                        new CapabilityMetadata(2, 1, 1, 1, 1, 1, 0, 0)),
                    new WireTargetReceivedEvent(
                        MessageType.RouteHint,
                        new RouteHintMetadata(operationId, 1, 1, 1, 0, 0, 0)),
                    new WireTargetReceivedEvent(
                        MessageType.CacheReference,
                        new CacheReferenceMetadata(
                            1,
                            1_234_605_616_436_508_552,
                            11_072_869_122_414_935_808,
                            2,
                            CacheReuseScope.Session,
                            0,
                            0,
                            0,
                            0,
                            0)),
                    new WireTargetReceivedEvent(MessageType.SessionClose),
                ],
                [new WireTargetLifecycleEvent(operationId, NnrpOperationState.Completed)],
                lifecycleExpectedAfterTerminal: true));
        }

        private static FakeClientSession CreateProgressSession() => new(
            [
                new WireTargetReceivedEvent(
                    MessageType.Progress,
                    new ProgressMetadata(301, 1, 1, 2500, 0, 0)),
                new WireTargetReceivedEvent(
                    MessageType.CreditUpdate,
                    new PressureMetadata(301, 1, 1, 1, 0, 0)),
                new WireTargetReceivedEvent(
                    MessageType.PartialResult,
                    new PartialResultMetadata(301, 1, 1, 1, 0, 0)),
            ],
            new WireTargetTerminalResult(
                301,
                NnrpResultTerminalState.Success,
                new WireTargetReceivedEvent(
                    MessageType.ResultPush,
                    body: Encoding.UTF8.GetBytes("wire-external-result"))));

        private FakeOperation Track(FakeOperation operation)
        {
            Operations.Add(operation);
            return operation;
        }

        private FakeServerSession Track(FakeServerSession session)
        {
            ServerSessions.Add(session);
            return session;
        }
    }

    private sealed class FakeServer(
        NnrpProviderEndpoint boundProviderEndpoint,
        Queue<IWireTargetServerSession> sessions) : IWireTargetServer
    {
        public NnrpProviderEndpoint BoundProviderEndpoint { get; } = boundProviderEndpoint;

        public ValueTask<IWireTargetServerSession> AcceptAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(sessions.Dequeue());
        }

        public ValueTask DisposeAsync() => default;
    }

    private sealed class FakeServerSession(
        FakeOperation operation,
        IEnumerable<WireTargetReceivedEvent> events,
        IEnumerable<WireTargetLifecycleEvent>? lifecycleEvents = null,
        bool? lifecycleExpectedAfterTerminal = null,
        IEnumerable<WireTargetOperationEvent>? operationEvents = null) : IWireTargetServerSession
    {
        private readonly Queue<WireTargetReceivedEvent> events = new(events);
        private readonly Queue<WireTargetLifecycleEvent> lifecycleEvents = new(lifecycleEvents ?? []);
        private readonly Queue<WireTargetOperationEvent> operationEvents = new(operationEvents ?? []);

        internal bool Disposed { get; private set; }

        public ValueTask<IWireTargetOperation> ReceiveSubmitAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<IWireTargetOperation>(operation);
        }

        public ValueTask<WireTargetReceivedEvent> NextEventAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(events.Dequeue());
        }

        public ValueTask<WireTargetOperationEvent> NextOperationEventAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.False(operation.TerminalSent);
            return ValueTask.FromResult(operationEvents.Dequeue());
        }

        public ValueTask<WireTargetLifecycleEvent> NextLifecycleAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (lifecycleExpectedAfterTerminal is bool expected)
            {
                Assert.Equal(expected, operation.TerminalSent);
            }

            return ValueTask.FromResult(lifecycleEvents.Dequeue());
        }

        public ValueTask SendTraceContextAsync(
            TraceContextMetadata metadata,
            ReadOnlyMemory<byte> body,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.Equal((uint)body.Length, metadata.BodyBytes);
            return default;
        }

        public ValueTask ReportCacheMissAsync(
            CacheMissMetadata metadata,
            ReadOnlyMemory<byte> diagnostic,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.Equal(CacheMissReason.NotFound, metadata.MissReason);
            return default;
        }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return default;
        }
    }

    private sealed class FakeOperation(ulong operationId, uint frameId) : IWireTargetOperation
    {
        public ulong OperationId { get; } = operationId;

        public uint FrameId { get; } = frameId;

        internal bool TerminalSent { get; private set; }

        internal NnrpResultDropReasonCode? DropReasonCode { get; private set; }

        public ValueTask SendResultAsync(
            ResultPushMetadata metadata,
            ReadOnlyMemory<byte> body,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.Equal(ResultStatusCode.Success, metadata.StatusCode);
            TerminalSent = true;
            return default;
        }

        public ValueTask SendResultDropAsync(
            ResultDropReasonMetadata metadata,
            ReadOnlyMemory<byte> diagnostic,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.Equal(OperationId, metadata.OperationId);
            DropReasonCode = metadata.DropReasonCode;
            TerminalSent = true;
            return default;
        }
    }

    private sealed class FakeClient(FakeClientSession session) : IWireTargetClient
    {
        public IWireTargetClientSession OpenSession() => session;

        public ValueTask DisposeAsync() => default;
    }

    private sealed class FakeClientSession(
        IEnumerable<WireTargetReceivedEvent> events,
        WireTargetTerminalResult result) : IWireTargetClientSession
    {
        private readonly Queue<WireTargetReceivedEvent> events = new(events);

        internal bool Disposed { get; private set; }

        public ValueTask<ulong> SubmitNoWaitAsync(
            NnrpSubmitRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(request.OperationId);
        }

        public ValueTask<WireTargetReceivedEvent> NextEventAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(events.Dequeue());
        }

        public ValueTask<WireTargetTerminalResult> NextResultAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(result);
        }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return default;
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        internal TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"nnrp-cs-wire-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        internal string Path { get; }

        public void Dispose()
        {
            Directory.Delete(Path, true);
        }
    }

    private static string WriteSuite(string directory, bool includeDeadlineBeforeSubmit)
    {
        string casesPath = Path.Combine(directory, "cases.json");
        string suitePath = Path.Combine(directory, "suite.json");
        string[] ids = includeDeadlineBeforeSubmit
            ? ["wire.control.cancel-abort.client", "wire.control.deadline-before-submit.client"]
            : ["wire.control.cancel-abort.client"];
        File.WriteAllText(
            casesPath,
            JsonSerializer.Serialize(new
            {
                scenarios = ids.Select(id => new { id }).ToArray(),
            }));
        File.WriteAllText(
            suitePath,
            JsonSerializer.Serialize(new
            {
                scenario_manifests = new[] { "cases.json" },
            }));
        return suitePath;
    }

    private static string WriteSuiteManifest(string directory, string scenarioManifest)
    {
        string suitePath = Path.Combine(directory, "suite.json");
        File.WriteAllText(
            suitePath,
            JsonSerializer.Serialize(new
            {
                scenario_manifests = new[] { scenarioManifest },
            }));
        return suitePath;
    }
}
