# 06 - Release Packaging And Docs

## Version And Release Inputs

- [x] Set the C# package train to `1.0.0-preview.4`.
- [x] Default release workflow source to `main`.
- [x] Default Rust artifact input to the coordinated `1.0.0-preview.4.23` release with exact FFI ABI `4.4.0`.
- [x] Validate that the requested Rust release tag and all required assets exist before packing.
- [x] Reject release when any Preview4 TODO checkbox is open.

## NuGet Package Graph

- [x] Pack `Nnrp.Core`.
- [x] Pack `Nnrp.NativeBridge`.
- [x] Pack `Nnrp.Client` with production client orchestration and no transport artifact.
- [x] Pack `Nnrp.Server` with production server orchestration and no transport artifact.
- [x] Pack `Nnrp.Transport.Tcp` with only TCP provider code and artifacts.
- [x] Pack `Nnrp.Transport.Quic` with only QUIC provider code and artifacts.
- [x] Pack `Nnrp.Transport.Ipc` with only IPC provider code and artifacts.
- [x] Pack `Nnrp.Transport.WebSocket` with only WebSocket provider code and artifacts.
- [x] Validate role-first dependency direction in every generated nuspec.
- [x] Inspect every nupkg for README, symbols, license, repository metadata, tags, and deterministic file order.
- [x] Inspect every transport nupkg against the complete Windows, macOS, Linux, Android, iOS, and iOS Simulator RID matrix.
- [x] Install every package set into clean client and server smoke projects.

## Unity Package

- [x] Update `com.nnrp.client` to the Preview4 package version.
- [x] Include managed client, core, NativeBridge, and all four provider assemblies.
- [x] Include every supported transport-scoped native plugin.
- [x] Exclude server assemblies and NuGet runtime paths.
- [x] Generate deterministic `.meta` files in CI.
- [x] Inspect the generated UPM tarball and zip before publication.
- [ ] Import the generated package in a Unity validation project.

## Release Workflow

- [x] Build and test managed assemblies before downloading release artifacts.
- [x] Download and verify every transport/platform artifact.
- [x] Pack NuGet and Unity distributions once from the verified artifact tree.
- [x] Configure and validate NuGet trusted publishing with an explicit API-token fallback.
- [x] Gate the Unity package and GitHub release assets on the same validated commit and package tree.
- [x] Create the repository tag only after package validation passes.
- [x] Make reruns idempotent for already-published packages and existing tags.

## Benchmarks

- [x] Benchmark runtime-control encode/decode and native send/receive.
  - [x] Measure managed runtime-control metadata encode/decode latency and allocated bytes per operation.
  - [x] Measure a coarse native runtime-control send/receive round trip.
- [x] Benchmark object declare/ref/release and object delta.
  - [x] Measure managed object declare, reference, and release encode/decode latency and allocated bytes per operation.
  - [x] Measure managed object delta encode/decode latency and allocated bytes per operation.
  - [x] Measure managed cache reference, miss, and invalidate encode/decode latency and allocated bytes per operation.
- [x] Benchmark copied snapshots and borrowed views.
- [x] Benchmark TCP loopback throughput and latency.
  - [x] Resolve and execute the TCP transport-scoped artifact.
  - [x] Record both request/result throughput and round-trip latency.
- [x] Benchmark QUIC loopback throughput and latency.
  - [x] Resolve and execute the QUIC transport-scoped artifact.
  - [x] Record both request/result throughput and round-trip latency.
- [x] Benchmark IPC loopback throughput and latency.
  - [x] Resolve and execute the IPC transport-scoped artifact.
  - [x] Record both request/result throughput and round-trip latency.
- [x] Benchmark WebSocket loopback throughput and latency.
  - [x] Resolve and execute the WebSocket transport-scoped artifact.
  - [x] Record both request/result throughput and round-trip latency.
- [x] Record allocated bytes per operation, p50, p95, p99, throughput, and payload size.
- [x] Compare the same payload matrix with Rust, Python, and JavaScript SDK baselines.
- [x] Store reproducible commands and results under `doc/benchmarks`.

## Documentation

- [x] Update the repository README for the Preview4 role-first package graph.
- [x] Update package READMEs for client, server, core, NativeBridge, and all four transports.
- [x] Update the English and Chinese C# SDK overview and quick start.
- [x] Document `NnrpClient` connection, session, control, object, cache, result, and shutdown workflows.
- [x] Document `NnrpServer` listen, accept, operation, event, result, and shutdown workflows.
- [x] Document provider registration, automatic selection, explicit selection, costs, limits, and diagnostics.
- [x] Document TCP, QUIC, IPC, and WebSocket endpoint and security rules.
- [x] Document runtime-frame encoding and WebSocket binary framing.
- [x] Document wire conformance commands and evidence outputs.
- [x] Verify every documented public symbol and signature against compiled reference assemblies.
- [x] Verify all internal links and English/Chinese navigation entries.
