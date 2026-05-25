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
| Pre-migration baseline | TBD | TBD | N/A | TBD | TBD | TBD | TBD |
| Post-migration native | TBD | TBD | TBD | TBD | TBD | TBD | TBD |

### Latency Benchmarks

| Benchmark | Payload | Iterations | Pre p50 | Pre p95 | Pre p99 | Post p50 | Post p95 | Post p99 | Delta | Notes |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| Header encode/decode | L0 header | TBD | TBD | TBD | TBD | TBD | TBD | TBD | TBD | TBD |
| Metadata encode/decode | session open/open ack | TBD | TBD | TBD | TBD | TBD | TBD | TBD | TBD | TBD |
| Metadata encode/decode | frame submit/result push | TBD | TBD | TBD | TBD | TBD | TBD | TBD | TBD | TBD |
| Typed payload pack/unpack | tensor descriptor plus payload | TBD | TBD | TBD | TBD | TBD | TBD | TBD | TBD | TBD |
| Native probe | version plus capability query | TBD | TBD | TBD | TBD | TBD | TBD | TBD | TBD | TBD |
| Session lifecycle | open plus close loop | TBD | TBD | TBD | TBD | TBD | TBD | TBD | TBD | TBD |

### Throughput Benchmarks

| Benchmark | Payload | Duration | Pre throughput | Pre CPU | Pre GC alloc | Pre peak memory | Post throughput | Post CPU | Post GC alloc | Post peak memory | Delta | Notes |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| Submit/result loop | inline tensor payload | TBD | TBD | TBD | TBD | TBD | TBD | TBD | TBD | TBD | TBD | TBD |
| TCP loopback | request/result stream | TBD | TBD | TBD | TBD | TBD | TBD | TBD | TBD | TBD | TBD | TBD |
| QUIC loopback | request/result stream | TBD | TBD | TBD | TBD | TBD | TBD | TBD | TBD | TBD | TBD | Optional slot |

## Migration Phases

1. Capture pre-migration benchmarks and commit the results to this document.
2. Add native artifact discovery, loader validation, and ABI/protocol probes.
3. Add managed wrappers for connection, session, operation, schema, and buffer views.
4. Move preview3 hot-path encode/decode and submit/result flow behind the native backend.
5. Keep public managed APIs stable and isolate backend selection behind `Nnrp.NativeBridge`.
6. Add post-migration benchmarks and record the deltas in this document.
7. Enable conformance and package validation CI for the supported platform matrix.

## Open Decisions

1. Whether native artifacts are published inside the primary client package or split into per-platform companion packages.
2. Whether iOS should use a static native bridge artifact from the first migration PR or remain behind a later Unity package gate.
3. Which native capability probe names are considered stable enough for managed feature gating.
