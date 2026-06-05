# Nnrp.NativeBridge

Nnrp.NativeBridge provides the Rust-backed preview3 runtime entry point for C# hosts.

Use this package when you want client connection/session bootstrap, submit/result polling, cancellation, and control paths to run through the packaged `nnrp-rs` native artifacts. `NnrpNativeRuntimeSessionHost` is the recommended high-level facade when an application wants one native-backed session surface instead of manually assembling handles.

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

Repository and full SDK documentation:

- https://github.com/NagareWorks/nnrp-cs
- https://nagareworks.github.io/nnrp-doc/
