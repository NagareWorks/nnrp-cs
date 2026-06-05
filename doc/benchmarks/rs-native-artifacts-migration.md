# Rust Native Artifacts Migration Plan

## Goal

Move the C# SDK preview3 runtime path onto the canonical `nnrp-rs` native implementation so the managed packages no longer maintain protocol-critical wire packing, transport framing, session state, or QUIC behavior.

The C# packages should keep idiomatic managed APIs for .NET and Unity while delegating hot-path protocol work to versioned native artifacts produced by `nnrp-rs`.

## Non-Goals

1. Do not redesign preview3 protocol semantics in this repository.
2. Do not make QUIC mandatory for every package. QUIC remains a native transport slot included only by artifacts that enable it.
3. Do not remove managed fixture helpers that are useful for tests, docs, or non-hot-path validation.

## Current Baseline

The existing C# SDK owns managed packet helpers, native bridge scaffolding, and package-specific transport behavior. Preview3 should replace the runtime path in place with Rust-backed handles while preserving the public managed package shape where practical.

## Native Artifact Strategy

1. Pin an `nnrp-rs` commit, tag, or published artifact version in the C# release notes before packaging.
2. Package native libraries under deterministic NuGet runtime identifiers and Unity plugin folders.
3. Probe the loaded artifact for ABI version, protocol version, enabled transport slots, and feature flags before accepting it.
4. Route runtime operations through the native backend when the probe passes.
5. Keep managed fallback code only for fixture inspection, diagnostics, and explicitly unsupported runtime combinations.

## Pinned Native Contract

The current preview3 binding work consumes `nnrp-rs` release `v1.0.0-preview.3.8`.

This release is the native artifact contract pin for the current C# preview3 line and includes:

1. The `nnrp_runtime_capabilities` export.
2. ABI version `1.0.0`.
3. Protocol version `1/0`.
4. Runtime feature flags for protocol core, client/server APIs, event polling, callback dispatch, cache/schema, recovery, typed payloads, and transport slots.
5. Transport slot bits for TCP and optional QUIC.

If a later `nnrp-rs` release changes exported symbol names, ABI struct layout, required feature flags, or transport-slot meanings, update this pin and rerun the pre/post migration benchmark table before accepting the new artifact.

## Target Platform Matrix

| OS | Architectures | .NET/NuGet target | Unity target | Required before GA |
| --- | --- | --- | --- | --- |
| Windows | x86, x86_64, arm64 | `runtimes/win-*/native` | Windows plugin import settings | Yes |
| macOS | x86_64, arm64 | `runtimes/osx-*/native` | macOS plugin import settings | Yes |
| Linux | x86, x86_64, arm, arm64 | `runtimes/linux-*/native` | Linux plugin import settings | Yes |
| Android | x86, x86_64, armv7, arm64 | Optional RID package | Android plugin import settings | Preview gate |
| iOS | x86_64 simulator, arm64 simulator/device | Optional RID package | iOS static/native plugin settings | Preview gate |

## Benchmark Protocol

Run the baseline benchmark before migration and record it here. After the native backend lands, run the same benchmark suite on the same machine class and add the post-migration numbers.

Rules:

1. Record commit SHA, .NET SDK or Unity version, OS, architecture, CPU model, and native artifact version.
2. Use the same iteration counts and payload shapes before and after migration.
3. Report p50, p95, and p99 latency where the operation is request-like.
4. Report throughput, CPU, GC allocations, and peak memory where the operation is stream-like.
5. Keep QUIC benchmark rows separate from TCP and in-memory rows because QUIC is a slot, not a default dependency.

### Environment

| Run | Date | SDK commit | nnrp-rs artifact | Host runtime | OS/arch | CPU | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Pre-migration baseline | 2026-05-25 | 135ca63 | N/A | .NET 8.0.27 | windows/x64 | Intel(R) Core(TM)2 Duo CPU T7700 @ 2.40GHz | Conformance benchmark runner selected and measured 9 scenarios. |
| Post-migration native | 2026-06-06 | 9c8d7dd | v1.0.0-preview.3.8 | .NET 8.0.27 | windows/x64 | AMD Ryzen 9 9955HX3D 16-Core Processor | Benchmark adapter measured the same 9 scenarios locally; CPU differs from the pre-migration baseline, so deltas are directional rather than same-machine gates. |

### Latency Benchmarks

| Benchmark | Payload | Iterations | Pre p50 | Pre p95 | Pre p99 | Post p50 | Post p95 | Post p99 | Delta | Notes |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| Header encode/decode | L0 header | 100000 | 0.4 us | 0.5 us | 0.7 us | 0.2 us | 0.2 us | 0.3 us | p50 -50.0%; p95 -60.0%; p99 -57.1% | Measured by `l4.header.encode_decode.latency`. |
| Metadata encode/decode | session open/open ack | 100000 | 1.4 us | 2.2 us | 3.5 us | 0.9 us | 1.0 us | 1.0 us | p50 -35.7%; p95 -54.5%; p99 -71.4% | Measured by `l4.metadata.session_open_ack.latency`. |
| Metadata encode/decode | frame submit/result push | 100000 | 0.9 us | 1.5 us | 4.5 us | 0.7 us | 0.8 us | 2.9 us | p50 -22.2%; p95 -46.7%; p99 -35.6% | Measured by `l4.metadata.submit_result.latency`. |
| Typed payload pack/unpack | tensor descriptor plus payload | 100000 | 0.9 us | 2.1 us | 5.2 us | 0.5 us | 0.9 us | 1.1 us | p50 -44.4%; p95 -57.1%; p99 -78.8% | Measured by `l4.typed_payload.tensor_pack_unpack.latency`. |
| Native probe | version plus capability query | 100000 | 0.2 us | 0.3 us | 1.2 us | 0.1 us | 0.1 us | 0.3 us | p50 -50.0%; p95 -66.7%; p99 -75.0% | Measured by `l4.runtime.probe.latency`. |
| Session lifecycle | open plus close loop | 100000 | 0.5 us | 0.6 us | 1.2 us | 0.2 us | 0.2 us | 0.2 us | p50 -60.0%; p95 -66.7%; p99 -83.3% | Measured by `l4.session.lifecycle.latency`. |

### Throughput Benchmarks

| Benchmark | Payload | Duration | Pre throughput | Pre CPU | Pre GC alloc | Pre peak memory | Post throughput | Post CPU | Post GC alloc | Post peak memory | Delta | Notes |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| Submit/result loop | inline tensor payload | 10 s | 584203.6 ops/s | N/A | N/A | N/A | 2029075.6 ops/s | N/A | N/A | N/A | +247.3% | Measured by `l4.submit_result.inline_tensor.throughput`. |
| TCP loopback | request/result stream | 10 s | 2250626.4 ops/s | N/A | N/A | N/A | 5299734.4 ops/s | N/A | N/A | N/A | +135.5% | Measured by `l4.transport.tcp.loopback.throughput` against the SDK local transport-probe loopback path. |
| QUIC loopback | request/result stream | 10 s | 2210593.2 ops/s | N/A | N/A | N/A | 5292433.7 ops/s | N/A | N/A | N/A | +139.4% | Optional slot; measured by `l4.transport.quic.loopback.throughput` against the SDK local transport-probe loopback path. |

The current benchmark adapter reports latency percentiles and throughput only. CPU, GC allocation, and peak memory counters were not captured in the pre- or post-migration runs.

## Migration Phases

1. Capture pre-migration benchmarks and commit the results to `doc/benchmarks/rs-native-artifacts-migration.md`.
2. Add native artifact discovery, loader validation, and ABI/protocol probes.
3. Add managed wrappers for connection, session, operation, schema, and buffer views.
4. Move preview3 hot-path encode/decode and submit/result flow behind the native backend.
5. Keep public managed APIs stable and isolate backend selection behind `Nnrp.NativeBridge`.
6. Add post-migration benchmarks and record the deltas in `doc/benchmarks/rs-native-artifacts-migration.md`.
7. Enable conformance and package validation CI for the supported platform matrix.

## Closed Decisions

1. Native artifacts for the current C# line stay in the primary native bridge/client package layout: NuGet assets use deterministic `runtimes/<rid>/native` paths, and Unity receives one common-platform client package. Per-platform companion packages remain deferred until release packaging needs them.
2. iOS remains in the common-platform Unity package baseline with importer metadata and pinned-artifact resolution. It remains a preview gate before GA rather than a separate later package family.
3. Native capability probe names are stable for the managed gates currently claimed by `Nnrp.NativeBridge`: ABI/protocol version, core runtime features, cache/schema, recovery, typed payloads, callback dispatch, event polling, schema registry handles, cache lease operations, TCP slot, and optional QUIC slot. New probe names must land in `nnrp-rs` first, then be claimed through the C# capability manifest only after adapter coverage exists.
