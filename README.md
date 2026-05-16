# nnrp-cs

C# SDK scaffold for NNRP.

This repository keeps a neutral protocol-level name because the shared wire contract is useful on both client and server sides. The immediate target is Unity 2022, so the library target is `netstandard2.1`.

## Unity Package Download

If you want the Unity-style package, open the latest GitHub Release for this repository and download `com.nnrp.client-<version>.zip` from the release assets.

That zip is the CI-produced Unity package bundle for the current version.

## Contributors

<a href="https://github.com/NagareWorks/nnrp-cs/graphs/contributors" title="Open the contributors graph for individual GitHub profiles and IDs.">
    <img src="https://contrib.rocks/image?repo=NagareWorks/nnrp-cs" alt="Contributors" />
</a>

The avatar wall above is updated automatically from the repository contributor list.

GitHub README rendering does not support per-avatar dynamic tooltips for an auto-generated contributor wall, so use the linked contributors graph if you want individual profile pages and account IDs.

## Public SDK Direction

The current public session contract for this repository is the active `NNRP/1` wire format. The primary managed entry points are the current client/session helpers and the native-bridge wrappers used by Unity-facing callers.

Low-level retained wire primitives are still available in `Nnrp.Core` for codec coverage, golden-vector parity, and transport-neutral parser tests. They are no longer the recommended first-line application surface.

## Current NNRP/1 Session Contract

The active `NNRP/1` session contract is an async-session lightweight real-time AI application protocol. The first backend target is still neural rendering, but the wire contract is intentionally broader: token streaming, multimodal chunks, structured events, tool deltas, and transport-control messages all share the same fixed-layout framing model.

The authoritative current-wire design document lives in `nnrp-doc/docs/en/design/v1-preview2.md`, and the remaining implementation checklist for this repository stays in [doc/todo/v1-preview2/implementation-todo.md](./doc/todo/v1-preview2/implementation-todo.md).

## Status

| Area | Status |
| --- | --- |
| Common 40-byte header | Implemented |
| Strict header parse diagnostics | Implemented |
| Legacy and current wire-format enum value sets | Implemented |
| 8-byte alignment and checked arithmetic helpers | Implemented |
| Fixed binary reader/writer helpers | Implemented |
| `FrameSubmitMetadata` codec | Implemented |
| `ResultPushMetadata` codec | Implemented |
| `TensorSectionDescriptor` codec | Implemented |
| Client/server profile validation | Implemented |
| Capability negotiation | Implemented |
| Session and frame state machines | Implemented |
| Protocol failure result model | Implemented |
| Transport-neutral framed message abstractions | Implemented |
| Transport-neutral client session facade | Implemented |
| Cross-language primitive golden vector tests | Implemented |
| Per-package line coverage gate | Implemented at 90% |
| Full `FRAME_SUBMIT` message body codec | Implemented |
| Full `RESULT_PUSH` message body codec | Implemented |
| Control message codecs | Implemented |
| TLV control extension helpers | Implemented |
| Unity-compatible QUIC transport binding | Planned |
| Rust-native QUIC smoke bridge | In progress |

## Layout

- `src/Nnrp.Core/`: shared protocol enums, wire primitives, state machines, and transport-neutral framed message abstractions
- `src/Nnrp.Client/`: client-facing helpers and transport-neutral session facade
- `src/Nnrp.Server/`: server-facing helpers
- `src/Nnrp.NativeBridge/`: Unity-compatible managed glue for native transport bridges
- `src/Nnrp.Transport.Tcp/`: TCP transport adapter for framed NNRP messages
- `native/nnrp_quic_bridge/`: Rust cdylib for immediate QUIC/native smoke experiments
- `tests/`: xUnit test projects for the SDK packages

## Responsibilities

- `nnrp-cs` owns the Unity-compatible C# wire codec, profile validation, capability negotiation, session facade, and optional transport adapter boundaries.
- `nnrp-py` owns the Python-side protocol/runtime SDK, QUIC listener/client implementation, replay/export helpers, and runtime-facing integration surface.
- Cross-language wire alignment is validated by C# tests that import nnrp-py golden vectors for the common header, `CLIENT_HELLO`, `SESSION_PATCH`, `SESSION_PATCH_ACK`, `CACHE_*`, `FRAME_SUBMIT`, and `RESULT_PUSH`, then assert stable parse/re-emit behavior for the shared wire shape.

## Example

The primitive examples below intentionally include retained low-level legacy/base codec types from `Nnrp.Core`. For application-facing integrations, start from the current client/session helpers described later in this document.

```csharp
using Nnrp.Core;

var header = new NnrpHeader(
	versionMajor: NnrpHeader.CurrentVersionMajor,
	messageType: MessageType.FrameSubmit,
	flags: HeaderFlags.AckRequired | HeaderFlags.Keyframe,
	metaLength: FrameSubmitMetadata.MetadataLength,
	bodyLength: 128,
	sessionId: 42,
	frameId: 7,
	viewId: 1,
	routeId: 0,
	traceId: 0x0102030405060708UL);

byte[] bytes = header.ToArray();

if (!NnrpHeader.TryParse(bytes, NnrpHeaderParseOptions.Strict, out var parsed, out var error))
{
	throw new InvalidOperationException($"Invalid NNRP header: {error}");
}
```

### Frame submit encode / decode

```csharp
using Nnrp.Core;

var cameraBlock = new byte[] { 0xCA, 0xFE };
var tileIds = new ushort[] { 1, 9 };
var section = new TensorSectionBlock(
    new TensorSectionDescriptor(
        role: TensorRole.LumaHint,
        codec: CodecId.Raw,
        dtype: DTypeId.UInt8,
        layout: TensorLayoutId.Nhwc,
        scalePolicy: ScalePolicy.None,
        flags: 0,
        elementCountPerTile: 0,
        codecTableBytes: 0,
        lengthTableBytes: 8,
        payloadBytes: 3,
        payloadStrideBytes: 0),
    codecTable: Array.Empty<byte>(),
    lengthTable: new byte[] { 1, 0, 0, 0, 2, 0, 0, 0 },
    payload: new byte[] { 0xAA, 0xBB, 0xCC });

var metadata = new FrameSubmitMetadata(
    sourceWidth: 640, sourceHeight: 360,
    tileWidth: 32, tileHeight: 32,
    tileCount: 2, sectionCount: 1,
    frameClass: FrameClass.Keyframe,
    inputProfile: InputProfile.ChangedTilesLuma,
    tileIndexMode: TileIndexMode.RawUInt16,
    latencyBudgetMilliseconds: 16,
    targetFpsTimes100: 6000,
    retryOfFrame: 0, tileBaseId: 0,
    cameraBytes: (uint)cameraBlock.Length,
    tileIndexBytes: 4);

// Body length is 8-byte aligned at block boundaries: camera → pad → tile_index → pad → section
var bodyLength = (uint)(BinaryAlignment.AlignUp(
    BinaryAlignment.AlignUp(cameraBlock.Length, 8) + (int)metadata.TileIndexBytes,
    8)
    + section.TotalLength);

var header = new NnrpHeader(
    versionMajor: NnrpHeader.CurrentVersionMajor,
    messageType: MessageType.FrameSubmit,
    flags: HeaderFlags.None,
    metaLength: FrameSubmitMetadata.MetadataLength,
    bodyLength: bodyLength,
    sessionId: 1, frameId: 100,
    viewId: 0, routeId: 0, traceId: 0);

var message = new FrameSubmitMessage(header, metadata, cameraBlock, tileIds, new[] { section });
byte[] packet = message.ToArray();

// Round-trip parse
if (FrameSubmitMessage.TryParse(packet, out var parsed, out var error))
{
    Console.WriteLine($"Parsed {parsed.TileIds.Length} tiles, {parsed.Sections.Length} sections");
}
```

### Result push encode / decode

```csharp
var resultSection = new TensorSectionBlock(
    new TensorSectionDescriptor(
        role: TensorRole.SrResidual,
        codec: CodecId.Raw,
        dtype: DTypeId.UInt8,
        layout: TensorLayoutId.Nhwc,
        scalePolicy: ScalePolicy.None,
        flags: 0, elementCountPerTile: 0,
        codecTableBytes: 0,
        lengthTableBytes: 8, payloadBytes: 2,
        payloadStrideBytes: 0),
    codecTable: Array.Empty<byte>(),
    lengthTable: new byte[] { 1, 0, 0, 0, 1, 0, 0, 0 },
    payload: new byte[] { 0x10, 0x20 });

var resultMetadata = new ResultPushMetadata(
    statusCode: ResultStatusCode.Success,
    resultFlags: ResultFlags.None,
    sectionCount: 1, tileCount: 2,
    activeProfileId: 1,
    inferenceMilliseconds: 2,
    queueMilliseconds: 1,
    serverTotalMilliseconds: 4,
    tileBaseId: 0, tileIndexBytes: 0);

var resultBodyLength = (uint)(BinaryAlignment.AlignUp(0, 8) + resultSection.TotalLength);

var resultHeader = new NnrpHeader(
    versionMajor: NnrpHeader.CurrentVersionMajor,
    messageType: MessageType.ResultPush,
    flags: HeaderFlags.None,
    metaLength: ResultPushMetadata.MetadataLength,
    bodyLength: resultBodyLength,
    sessionId: 1, frameId: 100,
    viewId: 0, routeId: 0, traceId: 0);

var resultMessage = new ResultPushMessage(resultHeader, resultMetadata, new ushort[] { 5, 6 }, new[] { resultSection });
byte[] resultPacket = resultMessage.ToArray();

if (ResultPushMessage.TryParse(resultPacket, out var parsedResult, out var resultError))
{
    Console.WriteLine($"Parsed result: {parsedResult.Metadata.StatusCode}, {parsedResult.TileIds.Length} tiles");
}
```

### Control extension TLV

```csharp
var ext1 = new ControlExtensionBlock(ControlExtensionType.ScheduleHint, new byte[] { 0x01 });
byte[] ext1Bytes = ext1.ToArray();

if (ControlExtensionBlock.TryParse(ext1Bytes, out var parsed, out var consumed, out var tlvError))
{
    Console.WriteLine($"Parsed extension type 0x{parsed.TypeCode:X4}, critical={parsed.IsCritical}");
}
```

## Build

```powershell
dotnet build Nnrp.sln
```

## Package Distribution

Do not reference generated Unity `.csproj` files directly. Unity still consumes the `netstandard2.1` client-facing surface, but this repository no longer stages or commits a Unity package folder or checked-in DLL outputs.

The distribution baseline is now:

- NuGet-style server dependency: published by CI and consumed as a package, not from checked-in DLL folders.
- NuGet-style client dependency: published by CI and consumed as a package, not from checked-in DLL folders.
- Unity-style client dependency: published by CI as `com.nnrp.client-<version>.zip` in GitHub Release assets and workflow artifacts.

NuGet packages are published to package feeds. The Unity-style package is distributed as a release asset instead of a committed Unity package tree.

The required package baseline is:

- Keep NuGet-style server and client packages separate from the Unity-style client package.
- Generate the Unity-style package in CI from package definitions plus a deterministic `.meta` generation step; do not hand-maintain committed `.meta` and plugin trees.
- Publish one Unity-style client package that already contains the current supported desktop native bridge binaries in Unity-compatible plugin paths.
- Keep the current release workflow focused on the current supported desktop platforms.
- Prefer package layouts where Unity callers pull one package and get all supported common-platform native binaries in place, rather than selecting per-platform package variants manually.

For repository workflow, contribution rules, and branch strategy, see [CONTRIBUTING.md](./CONTRIBUTING.md).

Use the client-facing surface split as follows:

- `Nnrp.Client.NnrpClient` is the canonical managed current-session facade for `SendSubmitAsync` + `ReceiveNextEventAsync` style integrations.
- `Nnrp.NativeBridge.NnrpAutoTransportClient` is the preferred Unity/.NET auto-probe/native-bridge entry point for bring-up and smoke validation.
- `Nnrp.Core` object-reference/body-layout types remain available for advanced callers, but they are not the recommended first-line Unity API for normal gameplay integration.

For host behavior under the current multi-frame session model:

- Treat `partial`, `stale_reuse`, `degraded`, and `RESULT_DROP` as normal runtime outcomes, not as transport failures.
- Drive presentation from frame/view correlation and the session event pump, rather than assuming every submit blocks until a matching result arrives.
- Keep local fallback rendering and host-side degradation policy separate from protocol parsing.

### Current Host Integration

The canonical host shape is `connect -> send submit -> consume session events`. `SubmitAsync` or `SubmitAndWaitAsync` convenience helpers are still available, but they are intentionally non-canonical wrappers for simple call sites and smoke tests. Real hosts should treat `ReceiveNextEventAsync` as the primary result/control boundary so multiple in-flight frames, `FLOW_UPDATE`, partial results, and degraded outcomes stay visible to the application layer.

For Unity and other gameplay-facing hosts, correlate presentation and local bookkeeping by `(session_id, frame_id, view_id)` rather than by submission order. A newer frame can complete before an older one, and `FLOW_UPDATE` can arrive between submits and results without representing an error.

### Current Object-Reference Lifecycle

Current object references are wire-level cache contracts, not engine object handles. Hosts are expected to treat `cache_namespace + cache_key_hi + cache_key_lo` as the stable identity for low-frequency objects such as `camera_block`, `tile_index_block`, `tensor_section_table`, and `payload_layout_template`.

The expected Unity-side lifecycle is:

- Build low-frequency objects locally, publish them through cache/object flows when they become reusable, then reference them from submit/result bodies with `object_ref_mask` or result-side object-reference blocks.
- Keep `object_ref_mask` aligned with the actual referenced object slots; it is part of the submit contract, not a best-effort hint.
- Inline objects again whenever the referenced content changes and no longer matches the cached identity, or explicitly rotate/invalidate the cached object identity before reusing the slot.
- Keep engine object lifetime separate from protocol cache lifetime; a Unity component being destroyed does not implicitly invalidate a wire cache object on the session.

### Current Typed Payload And Extension Usage

The current typed-payload model lets one `RESULT_PUSH` carry tensor output plus application-facing non-tensor frames in a single body. The descriptor region defines ordered byte ranges into the typed-payload frame region, and callers should consume those frames by `payload_kind + profile_id` rather than by re-slicing raw payload bytes manually.

The intended payload-family split is:

- `token_chunk` for incremental text/token emission.
- `audio_chunk` and `video_chunk` for multimodal media fragments.
- `structured_event` for host-visible semantic events.
- `tool_delta` for tool/coding-agent style incremental outputs.
- `opaque_bytes` for application-defined binary payloads when no higher-level kind is appropriate.

Extension frames remain the escape hatch for optional protocol features that should not overload typed payload semantics. Unknown non-critical extension frames must be skipped, while unknown critical extension frames must fail parsing with a stable protocol error.

## Rust-Native Smoke Bridge

The immediate QUIC path is a Rust native bridge built as a loadable `cdylib`:

- `native/nnrp_quic_bridge/Cargo.toml` declares `crate-type = ["cdylib"]`.
- `cargo build --release --manifest-path native/nnrp_quic_bridge/Cargo.toml` produces `native/nnrp_quic_bridge/target/release/nnrp_quic_bridge.dll`.
- `./scripts/repro_native_bridge.ps1` loads managed assemblies from `src/Nnrp.NativeBridge/bin/Release/netstandard2.1` plus the Rust bridge from `native/nnrp_quic_bridge/target/release`, then exercises native open, ping, cancel, submit, result parsing, and close.
- `NnrpQuicClient` in `src/Nnrp.NativeBridge/` is the current Unity-compatible QUIC entry point. It wraps native open, ping, submit, cancel, and close into one stateful managed facade without pulling `System.Net.Quic` into the Unity-facing assemblies.
- `NnrpAutoTransportClient` in `src/Nnrp.NativeBridge/` is the current Unity/.NET native-bridge facade when callers want transport probing plus managed/native packaging rather than a transport-specific client.
- The public native clients explicitly request and require the current wire format and ALPN `nnrp/1`.

When documenting a secure endpoint, use `nnrps://host:port` as the URI form even when the current local smoke scripts accept separate host/port parameters.

To run an end-to-end local loopback smoke against the sibling runtime checkout:

```powershell
./scripts/run_runtime_loopback_smoke.ps1
```

That script builds the managed/native bridge outputs if needed, starts `neural-render-runtime/scripts/run_local_dev.ps1` with NNRP transport enabled, and runs the native bridge repro against local build outputs.

Without `-UseAutoTransport`, the repro covers `PING`, `FRAME_CANCEL`, and `FRAME_SUBMIT`, then verifies the runtime produced smoke output plus runtime logs.

With `-UseAutoTransport`, the runtime smoke is scoped to current transport probing plus `PING` and `FRAME_CANCEL`. It still verifies the negotiated open path and both QUIC/TCP probe summaries, while deferring `FRAME_SUBMIT -> RESULT_PUSH` coverage until the sibling runtime transport bridge decodes current submit metadata.

The repro script now prints both the requested wire format and the runtime-negotiated wire format so the loopback smoke explicitly covers the current open path rather than relying on wrapper defaults.

## Test

```powershell
dotnet test Nnrp.sln
```

Coverage is checked per package with a 90% line threshold:

```powershell
dotnet test tests/Nnrp.Core.Tests/Nnrp.Core.Tests.csproj /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura /p:Include="[Nnrp.Core]*" /p:Threshold=90 /p:ThresholdType=line /p:ThresholdStat=total
dotnet test tests/Nnrp.Client.Tests/Nnrp.Client.Tests.csproj /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura /p:Include="[Nnrp.Client]*" /p:Threshold=90 /p:ThresholdType=line /p:ThresholdStat=total
dotnet test tests/Nnrp.Server.Tests/Nnrp.Server.Tests.csproj /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura /p:Include="[Nnrp.Server]*" /p:Threshold=90 /p:ThresholdType=line /p:ThresholdStat=total
dotnet test tests/Nnrp.Transport.Tcp.Tests/Nnrp.Transport.Tcp.Tests.csproj /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura /p:Include="[Nnrp.Transport.Tcp]*" /p:Threshold=90 /p:ThresholdType=line /p:ThresholdStat=total
```

The runtime-backed QUIC loopback smoke test stays opt-in because it builds the bridge outputs, launches the sibling runtime checkout, and exercises the native bridge end to end:

```powershell
$env:NNRP_RUN_QUIC_RUNTIME_SMOKE = '1'
$env:NNRP_RUNTIME_REPO_ROOT = '..\neural-render-runtime'
dotnet test tests/Nnrp.NativeBridge.Tests/Nnrp.NativeBridge.Tests.csproj -c Release --filter FullyQualifiedName~RuntimeLoopbackSmokeTests
```

## Format

```powershell
dotnet format Nnrp.sln
```

## Transport-Neutral Core

`NnrpFramedMessage` wraps one validated `NnrpHeader` plus metadata and body buffers as `ReadOnlyMemory<byte>` without copying. Callers own those buffers and should keep them alive and stable until a send operation has completed or a receive consumer is done reading the message.

`INnrpMessageSender`, `INnrpMessageReceiver`, and `INnrpMessageTransport` define the minimal async send/receive boundary using `ValueTask` and `CancellationToken`. `Nnrp.Core` does not reference `System.Net.Quic` or Unity engine assemblies. Transport adapters decide and document their own thread-safety guarantees; callers should not assume concurrent send or receive calls are safe unless a concrete adapter says so.

## Validation Modes

- `NnrpHeaderParseOptions.Default` keeps parsing permissive enough for framing-oriented callers while still validating magic, declared lengths, and overall message size limits.
- `NnrpHeaderParseOptions.Strict` additionally rejects unsupported `version_major`, unknown enum values, and reserved flags.
- `FrameSubmitMetadata`, `ResultPushMetadata`, and `TensorSectionDescriptor` also expose strict parsing paths that reject non-zero reserved fields and layout combinations that violate the frozen shared wire contract.

## Unity Smoke Integration

Unity 2022 can start with `ClientProfile`, `NnrpClientSession`, and a fake or in-memory `INnrpMessageTransport` to validate SDK wiring before the current `ClientHello` and hot-path body codecs are finalized. The current session facade supports local profile validation, capability negotiation, ACTIVE/DRAINING/CLOSED state transitions, frame-submit gating, and cancellation-aware async calls without referencing Unity engine assemblies.

When the QUIC path needs a real bring-up on Unity-compatible targets, use `Nnrp.NativeBridge.NnrpQuicClient` from the CI-produced Unity-style package or equivalent local package layout. It is intentionally narrower than the transport-neutral `NnrpClient`: the native-bridge facade owns the connection handle and exposes synchronous `Connect()`, `Ping()`, `Submit(...)`, `Cancel(...)`, and `Close()` calls over the packaged Rust bridge.

`ClientProfile.TransportProfile` is now the explicit switch between the default `ControlEvidence` path and `Quic`. Keep the default when wiring the transport-neutral session facade for local validation or evidence-only flows. Set it to `Quic` before constructing `NnrpQuicClient` so Unity-side configuration can choose the native QUIC path without relying on ad-hoc booleans.

## Profile Negotiation

- `ClientProfile` describes the client-side capability envelope: codecs, dtypes, tensor layouts, cache support, tile preference, source-size limits, target FPS range, latency budget, and optional auth block material.
- `ServerProfile` describes the server-side acceptance envelope: accepted codecs/layouts, concurrency limits, cache policy, body/section/tile limits, token TTL, and session-renewal policy.
- `ClientProfile.ToCapabilities()` and `ServerProfile.ToCapabilities()` normalize the mutable profile objects into validated capability snapshots.
- `NnrpClientSession.ConnectAsync(...)` performs local negotiation against `NnrpServerCapabilities`, stores the negotiated selection, and transitions the session state machine to `ACTIVE` only when the capability match is accepted.

## Unity 2022 Notes

Use the `netstandard2.1` packages from Unity-compatible code. Keep transport-specific code outside `Nnrp.Core` and `Nnrp.Client`.

Unity 2022 broad platform support is tied to `.NET Standard 2.1` or the Unity-supported `.NET Framework` profile. It should not directly depend on newer `.NET Core`-only transport assemblies such as `System.Net.Quic` from a Unity-facing package.

That means the future QUIC path must stay behind a separate transport binding or native bridge boundary:

- a Unity-compatible managed binding that itself targets the Unity-supported API surface, or
- a native QUIC plugin / bridge exposed to Unity via a thin managed wrapper.

If a newer-.NET sample based on `System.Net.Quic` is added later, it should live in a separate non-Unity sample or adapter package and must not become a dependency of the Unity-compatible packages.

That sample now lives in `samples/Nnrp.Quic.Net8Sample/`. It targets `net8.0`, uses `System.Net.Quic` directly, and keeps the Unity-facing packages transport-neutral.

The immediate bring-up path in this repository is therefore a native bridge rather than a Unity-facing `System.Net.Quic` package:

- `native/nnrp_quic_bridge/` hosts the Rust-side QUIC smoke implementation.
- `src/Nnrp.NativeBridge/` hosts the Unity-compatible managed wrapper that can load a native transport bridge.

The separate newer-.NET sample can be run directly against a runtime endpoint with NNRP transport enabled:

```powershell
dotnet run --project samples/Nnrp.Quic.Net8Sample -- --host 127.0.0.1 --port 50072 --tls-server-name localhost --requested-model engine-sr
```

The sample depends on `System.Net.Quic` platform support. On machines where the local .NET QUIC stack is unavailable, it exits with a clear platform-support message instead of affecting the Unity/native-bridge path.

This path is meant to unblock real end-to-end smoke on current Unity 2022 projects before a fully managed Unity-compatible QUIC binding exists.

## Packaging

All SDK projects share the repository version from `Directory.Build.props`. SDK packages can be produced with:

```powershell
dotnet pack Nnrp.sln -c Release
```

This only covers the current .NET package build. It does not yet generate Unity `.meta` files, does not assemble a Unity-style package artifact, and does not produce a multi-platform native-binary bundle. The intended CI packaging flow still needs:

- a deterministic Unity `.meta` generator or template-instantiation step,
- a multi-platform native build matrix covering Windows, macOS, Linux, Android, and iOS,
- one Unity-style package assembly step that places all supported common-platform native binaries into the correct Unity plugin directories.
