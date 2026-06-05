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
2. ABI version `1.6.0`.
3. Protocol version `1/0`.
4. Runtime feature flags for protocol core, client/server APIs, event polling, callback dispatch, cache/schema, recovery, typed payloads, and transport slots.
5. Transport slot bits for TCP and optional QUIC.
6. `nnrp_client_submit_result_compact_batch` for hot submit/result benchmark and SDK runtime paths.

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
| Post-migration native | 2026-06-06 | 9c8d7dd+ | v1.0.0-preview.3.8 | .NET 8.0.12 | windows/x64 | 13th Gen Intel(R) Core(TM) i7-13650HX | Official benchmark adapter routed runtime, session, submit/result, TCP, and QUIC rows through NativeBridge. TCP/QUIC rows used split transport provider artifacts. |

### Latency Benchmarks

| Benchmark | Payload | Iterations | Pre p50 | Pre p95 | Pre p99 | Post p50 | Post p95 | Post p99 | Delta | Notes |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| Header encode/decode | L0 header | 100000 | 0.4 us | 0.5 us | 0.7 us | 0.3 us | 0.3 us | 0.4 us | Host changed | Measured by `l4.header.encode_decode.latency`; fixture/diagnostic codec row. |
| Metadata encode/decode | session open/open ack | 100000 | 1.4 us | 2.2 us | 3.5 us | 0.9 us | 1.0 us | 1.1 us | Host changed | Measured by `l4.metadata.session_open_ack.latency`; fixture/diagnostic codec row. |
| Metadata encode/decode | frame submit/result push | 100000 | 0.9 us | 1.5 us | 4.5 us | 1.0 us | 1.1 us | 3.2 us | Host changed | Measured by `l4.metadata.submit_result.latency`; fixture/diagnostic codec row. |
| Typed payload pack/unpack | tensor descriptor plus payload | 100000 | 0.9 us | 2.1 us | 5.2 us | 0.5 us | 1.0 us | 1.3 us | Host changed | Measured by `l4.typed_payload.tensor_pack_unpack.latency`; fixture/diagnostic codec row. |
| Native probe | version plus capability query | 100000 | 0.2 us | 0.3 us | 1.2 us | 0.0 us | 0.1 us | 0.1 us | Host changed | Measured by `l4.runtime.probe.latency` through native version/capability entrypoints. |
| Session lifecycle | open plus close loop | 100000 | 0.5 us | 0.6 us | 1.2 us | 13.2 us | 15.4 us | 25.6 us | Host changed | Measured by `l4.session.lifecycle.latency` through NativeBridge open/close; this is no longer a managed state-machine helper. |

### Throughput Benchmarks

| Benchmark | Payload | Duration | Pre throughput | Pre CPU | Pre GC alloc | Pre peak memory | Post throughput | Post CPU | Post GC alloc | Post peak memory | Delta | Notes |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| Submit/result loop | inline tensor payload | 10 s | 584203.6 ops/s | TBD | TBD | TBD | 8256204.8 ops/s | TBD | TBD | TBD | +1313.2% | Measured by `l4.submit_result.inline_tensor.throughput` through NativeBridge compact batch. |
| TCP loopback | request/result stream | 10 s | 2250626.4 ops/s | TBD | TBD | TBD | 8275353.6 ops/s | TBD | TBD | TBD | +267.7% | Measured by `l4.transport.tcp.loopback.throughput`; post row uses the split TCP native provider artifact. |
| QUIC loopback | request/result stream | 10 s | 2210593.2 ops/s | TBD | TBD | TBD | 8292556.8 ops/s | TBD | TBD | TBD | +275.1% | Optional slot; post row uses the split QUIC native provider artifact. |

The post-migration throughput rows are on a newer machine than the pre-migration baseline, so the percentage deltas are
historical migration indicators rather than strict same-host speedups. The important current result is that split TCP and
QUIC provider artifacts stay in the same 8M ops/s class as Python cffi and JavaScript FFI benchmark paths on this host.

## Migration Phases

1. Capture pre-migration benchmarks and commit the results to `doc/benchmarks/rs-native-artifacts-migration.md`.
2. Add native artifact discovery, loader validation, and ABI/protocol probes.
3. Add managed wrappers for connection, session, operation, schema, and buffer views.
4. Move preview3 hot-path encode/decode and submit/result flow behind the native backend.
5. Keep public managed APIs stable and isolate backend selection behind `Nnrp.NativeBridge`.
6. [x] Add post-migration benchmarks and record the deltas in `doc/benchmarks/rs-native-artifacts-migration.md`.
7. Enable conformance and package validation CI for the supported platform matrix.

## Open Decisions

1. Whether iOS should use a static native bridge artifact from the first migration PR or remain behind a later Unity package gate.
2. Which native capability probe names are considered stable enough for managed feature gating.
