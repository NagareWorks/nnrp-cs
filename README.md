<p align="center">
  <img src="assets/nnrp-readme-banner.svg" alt="NNRP - Neural Network Runtime Protocol" width="100%" />
</p>

<p align="center">
  <a href="https://github.com/NagareWorks/nnrp-cs/actions"><img alt="CI" src="https://img.shields.io/badge/CI-.NET-22c55e"></a>
  <a href="https://dotnet.microsoft.com"><img alt=".NET Standard 2.1" src="https://img.shields.io/badge/.NET%20Standard-2.1-512bd4?logo=dotnet&logoColor=white"></a>
  <a href="https://unity.com"><img alt="Unity 2022" src="https://img.shields.io/badge/Unity-2022-000000?logo=unity&logoColor=white"></a>
  <a href="https://nagareworks.github.io/nnrp-doc/"><img alt="Docs" src="https://img.shields.io/badge/docs-nnrp--doc-38bdf8"></a>
  <a href="https://github.com/NagareWorks/nnrp-cs/blob/develop/LICENSE"><img alt="Apache-2.0" src="https://img.shields.io/badge/license-Apache--2.0-64748b"></a>
</p>

# nnrp-cs

C# bindings and Unity/.NET host surfaces for NNRP preview3.

The preview3 runtime path is Rust-backed. `Nnrp.NativeBridge` loads packaged `nnrp-rs` `nnrp_ffi` artifacts and probes the ABI/protocol/feature flags. `Nnrp.Transport.Tcp` and `Nnrp.Transport.Quic` own the transport-specific native entry surfaces that pin the selected transport before opening native connection, session, server, event polling, control, and cancellation facades. Managed client/server helpers remain diagnostic or unsupported-runtime surfaces for fixture inspection, conformance support, and local host development.

Full protocol and SDK documentation lives at https://nagareworks.github.io/nnrp-doc/.

## Packages

| Package | Role |
| --- | --- |
| `Nnrp.Core` | Protocol enums, fixed-layout codecs, state machines, capability negotiation, and conformance-oriented models. |
| `Nnrp.NativeBridge` | Rust-backed preview3 FFI substrate, artifact loading, ABI probing, and raw native handle facades. |
| `Nnrp.Client` | Managed diagnostic client helpers for fixture and unsupported-runtime scenarios. |
| `Nnrp.Server` | Managed diagnostic server helpers for fixture and unsupported-runtime scenarios. |
| `Nnrp.Transport.Tcp` | TCP native transport entry surface plus managed diagnostic TCP framed transport adapter. |
| `Nnrp.Transport.Quic` | QUIC native transport entry surface. |

## Install

NuGet-style package publication is CI owned. When a package is available, install the Rust-backed host facade with:

```powershell
dotnet add package Nnrp.NativeBridge --version <published-version>
dotnet add package Nnrp.Transport.Tcp --version <published-version>
```

Unity package generation is also CI owned. The Unity package is expected to contain managed assemblies plus platform-specific native plugins under Unity importer-aware plugin paths.

The common preview3 native platform scope is Windows, macOS, Linux, Android, and iOS across the RIDs represented in the package layout.

Reviewer-facing packaging policy and CI-owned release rules are documented in [doc/packaging/ci-first-package-strategy.md](./doc/packaging/ci-first-package-strategy.md).

## Native Session Example

```csharp
using Nnrp.NativeBridge;
using Nnrp.Transport.Tcp;

var options = new NnrpNativeTcpRuntimeSessionHostOptions(
    connectionId: 1,
    connectionGeneration: 1,
    sessionId: 1,
    sessionGeneration: 1,
    profileId: 1,
    schemaId: 1,
    schemaVersion: 1);

using var host = NnrpNativeTcpRuntime.OpenSessionHost(options);
var operation = host.SubmitOperation(operationId: 1, frameId: 1, payload: Array.Empty<byte>());
var result = host.PollResult(operation);
```

By default the native facade fails fast when the artifact is missing or incompatible. Diagnostic fallback must be explicit:

```csharp
options.FallbackBackend = diagnosticBackend;
options.FallbackPolicy = NnrpNativeRuntimeFallbackPolicy.UseFallbackForDiagnostics;
```

## Native Server Example

```csharp
using Nnrp.NativeBridge;
using Nnrp.Transport.Tcp;

var serverOptions = new NnrpNativeTcpRuntimeServerHostOptions(
    serverId: 1,
    serverGeneration: 1);

using var serverHost = NnrpNativeTcpRuntime.OpenServerHost(serverOptions);
serverHost.AcceptSession(new NnrpNativeRuntimeSessionOptions(
    sessionId: 1,
    sessionGeneration: 1,
    profileId: 1,
    schemaId: 1,
    schemaVersion: 1));

var operation = serverHost.ReceiveSubmit(sessionId: 1, operationId: 1, frameId: 1);
serverHost.SendResult(sessionId: 1, operation, payload: Array.Empty<byte>());
```

## Repository Layout

- `src/Nnrp.Core/`: protocol models, fixed-width codecs, negotiation, and state machines.
- `src/Nnrp.NativeBridge/`: preview3 Rust-backed native runtime substrate and artifact packaging.
- `src/Nnrp.Client/`: managed diagnostic client helpers.
- `src/Nnrp.Server/`: managed diagnostic server helpers.
- `src/Nnrp.Transport.Tcp/`: TCP native transport entry surface and managed diagnostic TCP transport adapter.
- `src/Nnrp.Transport.Quic/`: QUIC native transport entry surface.
- `tools/`: conformance and benchmark adapters.
- `tests/`: xUnit and packaging regression tests.
- `doc/todo/v1-preview3/`: preview3 task breakdown and implementation status.

## Validation

```powershell
dotnet test Nnrp.sln --configuration Release
python tests\Packaging.Tests\test_build_upm_package.py
dotnet format Nnrp.sln --verify-no-changes --no-restore
```

CI enforces test coverage, package boundary checks, conformance adapter behavior, and packaging layout regressions.

## Contributors

<a href="https://github.com/NagareWorks/nnrp-cs/graphs/contributors" title="Open the contributors graph for individual GitHub profiles and IDs.">
  <img src="https://contrib.rocks/image?repo=NagareWorks/nnrp-cs" alt="Contributors" />
</a>

The avatar wall above is updated automatically from the repository contributor list.
