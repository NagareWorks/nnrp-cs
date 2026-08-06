# Preview4 Runtime And Transport Benchmarks

## Purpose

This run records the C# Preview4 release candidate against the published Rust `1.0.0-preview.4.22` provider artifacts and FFI ABI `4.4.0`. The native scenarios execute the production provider, client, server, control-frame, buffer, and event-polling paths. They do not use a managed transport fallback or a benchmark-only FFI entrypoint.

## Environment

| Date | SDK revision | Rust artifact | Runtime | OS/arch | Concurrency |
| --- | --- | --- | --- | --- | ---: |
| 2026-08-06 | Preview4 benchmark closure revision | `1.0.0-preview.4.22` | .NET 8.0.27 | Windows/x64 | 1 |

## Results

| Scenario | Payload | p50 | p95 | p99 | Throughput | Allocated bytes/op |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Runtime-control metadata encode/decode | metadata only | 0.4 us | 0.6 us | 3.3 us | - | 216.0 |
| Runtime-object lifecycle/delta encode/decode | metadata only | 2.6 us | 7.9 us | 21.1 us | - | 1,688.3 |
| Native snapshot copy | 1,024 B | 0.2 us | 1.2 us | 1.6 us | - | 1,232.0 |
| Native borrowed view | 1,024 B | 0.0 us | 0.1 us | 0.1 us | - | 0.0 |
| Runtime-control trace-context IPC round trip | 64 B | 30,710.0 us | 32,005.9 us | 32,390.1 us | 32.5 ops/s | 2,864.0 |
| TCP submit/result round trip | 1,024 B | 46,299.7 us | 51,388.7 us | 63,190.5 us | 21.3 ops/s | 12,518.1 |
| QUIC submit/result round trip | 1,024 B | 46,146.8 us | 47,682.4 us | 47,741.6 us | 21.6 ops/s | 12,515.5 |
| IPC submit/result round trip | 1,024 B | 46,257.8 us | 62,291.6 us | 63,020.2 us | 20.7 ops/s | 12,516.2 |
| WebSocket submit/result round trip | 1,024 B | 46,082.4 us | 47,253.9 us | 47,772.9 us | 21.7 ops/s | 12,519.4 |

The pure codec and native-buffer rows show that metadata and ownership helpers are not the dominant cost. The role round trips include provider I/O, managed orchestration, Rust role processing, and event polling. Their current latency is release evidence, not a claim that Preview4 improves small single-operation round trips.

## Cross-SDK Comparison

The comparable transport rows below use a 1,024-byte submit and a 1,024-byte terminal result at concurrency one. The C# and Python rows ran on the same host against Rust `1.0.0-preview.4.22`. Python used the published `nnrp-py` `1.0.0rc4.post14` Windows x64 wheel at commit `ff8a9b2`; its machine-readable evidence is checked in beside this document.

| SDK | Runtime path | Carrier | Payload | p50 | Throughput |
| --- | --- | --- | ---: | ---: | ---: |
| C# | Native client/server roles | IPC | 1,024 B | 46,257.8 us | 20.7 ops/s |
| Python | Native client/server roles | IPC | 1,024 B | not emitted by runner | 64.7 ops/s |
| C# | Native client/server roles | WebSocket | 1,024 B | 46,082.4 us | 21.7 ops/s |
| Python | Native client/server roles | WebSocket | 1,024 B | not emitted by runner | 65.0 ops/s |
| JavaScript | Browser WASM client and native server roles | WebSocket | 1,024 B | 509.9 us | 1,725.3 ops/s |

The JavaScript row is a reference from its checked-in Preview4 baseline on Rust `1.0.0-preview.4.16`, not an artifact-matched comparison. It also crosses a browser WASM boundary rather than the C# and Python native-host boundary, so its delta must not be attributed to language overhead.

The 1,024-byte buffer rows are also intentionally not reduced to a ratio. C# reuses an acquired Rust buffer and measures event snapshot creation or view borrowing. Python acquires and releases a Rust buffer inside every measured operation before copying or borrowing it. Those are public-SDK ownership boundaries with the same payload size, but they do not time the same amount of work.

The current Rust baseline is excluded from the numeric 1,024-byte matrix because its checked-in transport runner uses a five-byte request and two-byte result under the Cargo dev profile. The JavaScript native carrier rows are likewise excluded because they echo a 40-byte packet. Their published figures remain useful implementation-local smoke baselines, but presenting them beside the role-level 1,024-byte rows would be misleading.

The comparison therefore supports two release conclusions: all measured SDK paths reach real Rust-owned carriers and roles, and the current single-operation results are dominated by each SDK's orchestration and polling policy. It does not establish a cross-language performance ranking.

## Reproduction

Download the four Windows x64 provider artifacts, then point each benchmark variable at its transport-scoped library:

```powershell
python scripts/download_nnrp_rs_artifacts.py `
  --version 1.0.0-preview.4.22 `
  --require-abi-version 4.4.0 `
  --rid win-x64 `
  --transport tcp --transport quic --transport ipc --transport websocket `
  --output artifacts/native

$env:NNRP_BENCHMARK_NATIVE_ARTIFACT_PATH = (Resolve-Path artifacts/native/transport-tcp/win-x64/nnrp_ffi.dll).Path
$env:NNRP_BENCHMARK_NATIVE_TCP_ARTIFACT_PATH = $env:NNRP_BENCHMARK_NATIVE_ARTIFACT_PATH
$env:NNRP_BENCHMARK_NATIVE_QUIC_ARTIFACT_PATH = (Resolve-Path artifacts/native/transport-quic/win-x64/nnrp_ffi.dll).Path
$env:NNRP_BENCHMARK_NATIVE_IPC_ARTIFACT_PATH = (Resolve-Path artifacts/native/transport-ipc/win-x64/nnrp_ffi.dll).Path
$env:NNRP_BENCHMARK_NATIVE_WEBSOCKET_ARTIFACT_PATH = (Resolve-Path artifacts/native/transport-websocket/win-x64/nnrp_ffi.dll).Path

dotnet run --project tools/Nnrp.BenchmarkAdapter/Nnrp.BenchmarkAdapter.csproj `
  --configuration Release -- `
  --plan doc/benchmarks/preview4-release-plan.json `
  --output artifacts/preview4-benchmark-results.json
```

The JSON result under `artifacts` is generated evidence. This document preserves the reviewed milestone without checking machine-specific paths into source control.

The Python comparison plan and result are stored as `preview4-python-comparison-plan.json` and `preview4-python-comparison-windows-x64-2026-08-06.json`. It uses 100,000 copy/borrow iterations and three-second IPC/WebSocket runs after 100 warmups. Run it from an environment containing the published Windows x64 wheel:

```powershell
python -m nnrp.tools.benchmark `
  --plan doc/benchmarks/preview4-python-comparison-plan.json `
  --output artifacts/python-comparison-results.json
```

The downloaded wheel and temporary virtual environment are machine-local artifacts and are not checked into source control.
