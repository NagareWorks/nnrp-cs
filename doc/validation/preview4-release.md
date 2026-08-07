# Preview4 Release Validation

This page records the reproducible release gates for `1.0.0-preview.4`. The release consumes
`nnrp-rs 1.0.0-preview.4.22` with FFI ABI `4.4.0` and builds every package from one validated commit.

## Automated Gates

The repository CI runs the following independent paths:

- Release build, unit tests, aggregate and incremental coverage, and formatting.
- Live native client, server, TCP, QUIC, IPC, and WebSocket tests on Windows, macOS, and Linux.
- Host-route wire E2E with the suite acting as the peer and validating provider selection, security,
  atomic listener rollback, and active carrier evidence.
- Runtime-frame wire E2E with ordered positive and negative control, object, cache, result, and
  terminal-event scenarios.
- Suite-owned conformance execution through `tools/Nnrp.ConformanceAdapter`.
- NuGet boundary, RID matrix, deterministic archive, symbol, metadata, and clean-install checks.
- UPM assembly, transport plugin, importer metadata, zip, and tarball checks.

The host-route and runtime-frame suites can be reproduced after checking out `nnrp-conformance` and
the coordinated Rust artifacts:

```powershell
./scripts/run_wire_host_route_e2e.ps1 `
  -ConformanceRoot ../nnrp-conformance `
  -NativeRoot artifacts/native `
  -OutputRoot artifacts/wire-host-route-e2e

./scripts/run_wire_runtime_e2e.ps1 `
  -ConformanceRoot ../nnrp-conformance `
  -NativeRoot artifacts/native `
  -OutputRoot artifacts/wire-runtime-e2e
```

Each command writes the plan, adapter result, suite result, and target logs under its output root.
CI uploads those directories even when a scenario fails.

## Package Validation

Release packaging uses the same managed build and downloaded native tree for all eight NuGet
packages and the Unity archive. The release workflow:

1. validates the requested Rust tag, artifact manifests, hashes, and ABI;
2. builds and tests managed assemblies;
3. packs and canonicalizes the complete NuGet graph;
4. verifies package boundaries and clean client/server installs;
5. builds and verifies the Unity zip and tarball;
6. creates the repository tag only after validation;
7. publishes NuGet through trusted publishing, with the configured API-token path as an explicit
   operational fallback; and
8. publishes GitHub release assets from the same commit and package tree.

Reruns skip packages or tags that already exist and never rebuild a different payload for an
existing version.

## Public API Evidence

`FrozenRoleApiContractTests` reflects the compiled `Nnrp.Client`, `Nnrp.Server`, and `Nnrp.Core`
assemblies and checks the frozen Preview4 role signatures and property sets. The canonical bilingual
reference and language projection live in `nnrp-doc`; its SDK contract and reference-sync tests keep
the C# pages aligned with the same contract.

## Manual Unity Gate

Before publication, import a generated release archive into a clean Unity 2022.3 validation project
and verify:

- all managed assemblies import without duplicate or missing references;
- only the current editor/player platform plugin is enabled;
- TCP, QUIC, IPC, and WebSocket plugins retain distinct transport-scoped paths;
- a client can resolve an installed provider and enter the Rust-backed connect path; and
- the package contains no server assembly or NuGet `runtimes/` tree.

Record the Unity editor version, host platform, archive hash, import log, and smoke result in the
release run. This is the only Preview4 release gate that is intentionally manual.
