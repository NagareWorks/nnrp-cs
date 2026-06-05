# Nnrp.NativeBridge

Nnrp.NativeBridge provides the Rust-backed preview3 runtime entry point for C# hosts.

Use this package when you want client connection/session bootstrap, submit/result polling, cancellation, and control paths to run through the packaged `nnrp-rs` native artifacts. `NnrpNativeRuntimeSessionHost` is the recommended high-level facade when an application wants one native-backed session surface instead of manually assembling handles.

The default preview3 path is native-backed and fails fast when the packaged artifact is missing or incompatible. Managed fallback backends are explicit diagnostic or unsupported-runtime hooks; set `FallbackPolicy = NnrpNativeRuntimeFallbackPolicy.UseFallbackForDiagnostics` only for tests, fixture inspection, or hosts that intentionally run without native artifacts.

This package depends on Nnrp.Core, Nnrp.Client, and Nnrp.Transport.Tcp, and may include runtime-specific native binaries when they are present during packing.

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

Repository and full SDK documentation:

- https://github.com/NagareWorks/nnrp-cs
- https://nagareworks.github.io/nnrp-doc/
