# Nnrp.NativeBridge

Nnrp.NativeBridge provides the Rust-backed preview3 runtime entry point for C# hosts.

Use this package when you want client connection/session bootstrap, submit/result polling, cancellation, and control paths to run through the packaged `nnrp-rs` native artifacts. `NnrpNativeRuntimeSessionHost` is the recommended high-level facade when an application wants one native-backed session surface instead of manually assembling handles.

The default preview3 path is native-backed and fails fast when the packaged artifact is missing or incompatible. Managed fallback backends are explicit diagnostic or unsupported-runtime hooks; set `FallbackPolicy = NnrpNativeRuntimeFallbackPolicy.UseFallbackForDiagnostics` only for tests, fixture inspection, or hosts that intentionally run without native artifacts.

This package depends on `Nnrp.Core` and may include runtime-specific native binaries when they are present during packing. It does not depend on the managed client/server or managed TCP adapter packages for the preview3 runtime path.

Install:

```powershell
dotnet add package Nnrp.NativeBridge --version <published-version>
```

Basic native-backed session:

```csharp
using Nnrp.NativeBridge;

var options = new NnrpNativeRuntimeSessionHostOptions(
    connectionId: 1,
    connectionGeneration: 1,
    transportId: NnrpNativeArtifact.TransportSlotTcp,
    sessionId: 1,
    sessionGeneration: 1,
    profileId: 1,
    schemaId: 1,
    schemaVersion: 1);

using var host = NnrpNativeRuntimeSessionHost.Open(options);
var result = host.SubmitAndPollResult(operationId: 1, frameId: 1, payload: Array.Empty<byte>());
```

Use an explicit diagnostic fallback:

```csharp
options.FallbackBackend = diagnosticBackend;
options.FallbackPolicy = NnrpNativeRuntimeFallbackPolicy.UseFallbackForDiagnostics;
```

Native-backed server session:

```csharp
var serverOptions = new NnrpNativeRuntimeServerHostOptions(
    serverId: 1,
    serverGeneration: 1,
    transportId: NnrpNativeArtifact.TransportSlotTcp);

using var serverHost = NnrpNativeRuntimeServerHost.Open(serverOptions);
var session = serverHost.AcceptSession(new NnrpNativeRuntimeSessionOptions(
    sessionId: 1,
    sessionGeneration: 1,
    profileId: 1,
    schemaId: 1,
    schemaVersion: 1));

var operation = serverHost.ReceiveSubmit(sessionId: 1, operationId: 1, frameId: 1);
serverHost.SendResult(sessionId: 1, operation, payload: Array.Empty<byte>());
```

Tests and diagnostics can still inject entrypoints directly:

```csharp
using var server = NnrpNativeRuntimeServer.Bind(
    entrypoints,
    serverId: 1,
    generation: 1,
    transportId: NnrpNativeArtifact.TransportSlotTcp);

var session = server.AcceptSession(
    sessionId: 1,
    generation: 1,
    profileId: 1,
    schemaId: 1,
    schemaVersion: 1);

var operation = session.ReceiveSubmit(operationId: 1, frameId: 1);
session.SendResult(operation, payload: Array.Empty<byte>());
```

Native-backed multi-session routing:

```csharp
var connectionOptions = new NnrpNativeRuntimeConnectionHostOptions(
    connectionId: 1,
    connectionGeneration: 1,
    transportId: NnrpNativeArtifact.TransportSlotTcp);

using var connectionHost = NnrpNativeRuntimeConnectionHost.Open(connectionOptions);
connectionHost.OpenSession(new NnrpNativeRuntimeSessionOptions(1, 1, 1, 1, 1));
connectionHost.OpenSession(new NnrpNativeRuntimeSessionOptions(2, 1, 1, 1, 1));

var routed = connectionHost.SubmitAndPollResult(
    sessionId: 1,
    operationId: 10,
    frameId: 1,
    payload: Array.Empty<byte>());
```

Unity event pump and dispatch rules:

The current host surface is polling/event-queue based. Drive one polling owner per native connection, normally from a Unity `Update` loop or from a single managed worker that posts completed work back to Unity's main thread. Do not let multiple Unity behaviours independently poll the same connection; use `NnrpNativeRuntimeConnectionHost` as the shared connection/session registry and route results by session and operation id.

Use `SubmitOperation` when submit and result delivery need to be decoupled. Then call `PollResult` for an operation-specific terminal or partial result, or call `PollAvailableEvents` when the host wants to drain connection-level events and route them itself. `PollResult` buffers unrelated events on the connection so a later session or operation poll can still observe them in order.

Unity APIs must only be touched from Unity's main thread. Native event payloads are copied into managed snapshots before they are returned, so `PayloadMemory` and `PayloadSpan` are safe for read-only inspection after the native poll call returns. If a worker thread polls results, enqueue only the managed snapshot or an application-level DTO back to the main thread.

Close the session or connection after the owner loop has stopped polling. `Dispose` closes the native handles and clears buffered connection events; callbacks or queued work owned by the application should be cancelled before disposing the host facade. Native callback subscription handles are not part of the public surface yet; when they are exposed, they must follow the same ownership rule: callbacks may enqueue managed snapshots, but Unity object mutation remains main-thread dispatch only.

Repository and full SDK documentation:

- https://github.com/NagareWorks/nnrp-cs
- https://nagareworks.github.io/nnrp-doc/
