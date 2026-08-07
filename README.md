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

C# bindings and Unity/.NET host surfaces for NNRP Preview4.

The Preview4 runtime path is Rust-backed. `Nnrp.Client` and `Nnrp.Server` own role-specific orchestration, while each transport package owns its native provider implementation and artifacts. Production role packages do not contain a managed protocol fallback or transport artifact.

Full protocol and SDK documentation lives at https://nagareworks.github.io/nnrp-doc/.

## Packages

| Package | Role |
| --- | --- |
| `Nnrp.Core` | Protocol enums, fixed-layout codecs, state machines, capability negotiation, and conformance-oriented models. |
| `Nnrp.NativeBridge` | Rust-backed Preview4 FFI substrate, artifact loading, ABI probing, and raw native handle facades. |
| `Nnrp.Client` | Production client connection, session, submit, control, object, cache, result, and event APIs. |
| `Nnrp.Server` | Production multi-listener server, accepted session, operation, control, object, cache, and result APIs. |
| `Nnrp.Transport.Tcp` | TCP native provider and transport-scoped artifacts. |
| `Nnrp.Transport.Quic` | QUIC native provider and transport-scoped artifacts. |
| `Nnrp.Transport.Ipc` | Unix-domain socket and Windows named-pipe native provider. |
| `Nnrp.Transport.WebSocket` | WS/WSS native provider and binary runtime-frame codec. |

## Install

NuGet-style package publication is CI owned. Install one role package and every transport allowed by the deployment:

```powershell
dotnet add package Nnrp.Client --version 1.0.0-preview.4
dotnet add package Nnrp.Transport.Tcp --version 1.0.0-preview.4
dotnet add package Nnrp.Transport.Quic --version 1.0.0-preview.4
```

Install one transport package to select that provider directly. Install several only when the host
should apply `TransportPolicy` filtering and probe the viable installed providers. Public endpoints
remain `nnrp://` or `nnrps://`; IPC and WebSocket locators belong in provider routes.

Unity package generation is also CI owned. The Unity package is expected to contain managed assemblies plus platform-specific native plugins under Unity importer-aware plugin paths.

The common Preview4 native platform scope is Windows, macOS, Linux, Android, and iOS across the RIDs represented in the package layout.

Reviewer-facing packaging policy and CI-owned release rules are documented in [doc/packaging/ci-first-package-strategy.md](./doc/packaging/ci-first-package-strategy.md).

## Client Example

```csharp
using Nnrp.Client;
using Nnrp.Core;

await using var client = await NnrpClient.ConnectAsync(
    new NnrpClientOptions(NnrpEndpoint.Parse("nnrp://runtime.example/session/default")),
    cancellationToken);
await using var session = client.OpenSession();
var result = await session.SubmitAsync(request, cancellationToken);
```

## Server Example

```csharp
using Nnrp.Core;
using Nnrp.Server;

await using var server = await NnrpServer.ListenAsync(
    new NnrpServerOptions(NnrpEndpoint.Parse("nnrp://0.0.0.0:7700/runtime/default")),
    cancellationToken);
await using var session = await server.AcceptAsync(cancellationToken: cancellationToken);
var operation = await session.ReceiveSubmitAsync(cancellationToken);
await operation.SendResultAsync(resultMetadata, resultBody, cancellationToken);
```

## Repository Layout

- `src/Nnrp.Core/`: protocol models, fixed-width codecs, negotiation, and state machines.
- `src/Nnrp.NativeBridge/`: Preview4 Rust-backed native runtime substrate and artifact packaging.
- `src/Nnrp.Client/`: production client role orchestration.
- `src/Nnrp.Server/`: production server role orchestration.
- `src/Nnrp.Transport.*`: transport-owned native providers and artifacts.
- `tools/`: conformance and benchmark adapters.
- `tests/`: xUnit and packaging regression tests.
- `doc/todo/v1-preview4/`: Preview4 task breakdown and implementation status.

## Validation

```powershell
dotnet test Nnrp.sln --configuration Release
python tests\Packaging.Tests\test_build_upm_package.py
dotnet format Nnrp.sln --verify-no-changes --no-restore
```

CI enforces test coverage, package boundary checks, conformance adapter behavior, and packaging layout regressions.
The exact Preview4 release gates, wire-conformance commands, evidence paths, and remaining Unity
manual check are recorded in
[doc/validation/preview4-release.md](./doc/validation/preview4-release.md).

## Contributors

<a href="https://github.com/NagareWorks/nnrp-cs/graphs/contributors" title="Open the contributors graph for individual GitHub profiles and IDs.">
  <img src="https://contrib.rocks/image?repo=NagareWorks/nnrp-cs" alt="Contributors" />
</a>

The avatar wall above is updated automatically from the repository contributor list.
