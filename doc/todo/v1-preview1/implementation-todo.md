# NNRP v1-preview1 C# SDK Todo

## 0. Usage Notes

### 0.1 Scope

1. This checklist covers the C# SDK work required to converge the historical preview1 plan onto the current `NNRP/1.0` reusable wire-contract package.
2. This checklist focuses on Unity 2022-compatible `netstandard2.1` APIs, protocol codecs, validation, tests, and documentation.
3. This checklist does not cover the Python runtime implementation, neural model execution, Unity render-pass authoring, or subjective image-quality evaluation.
4. The protocol design document remains the source of truth for wire semantics when it agrees with the runtime preview roadmap; any mismatch must be resolved before this SDK extends the affected codec.

### 0.2 Checkbox Rules

1. Keep a task as `[ ]` before implementation starts.
2. Change a task to `[x]` only after code is landed and locally verified.
3. Do not check implementation tasks for discussion-only or document-only progress.
4. If a task is split into child checklist items, check the parent only after all children are complete.
5. If any same-level task is still too broad to execute directly, split it into smaller checklist items before marking it complete.

### 0.3 Preview1 Boundaries

1. Support the current `NNRP/1.0` contract only; do not silently preserve fallback behavior for older preview-stage packets.
2. Keep the hot path binary and fixed-layout; do not add JSON or Protobuf serialization to `FRAME_SUBMIT` or `RESULT_PUSH`.
3. Keep the core SDK transport-neutral where possible so Unity can consume the same wire codec.
4. Treat QUIC integration as an adapter layer, not as a dependency of the core binary codec package.
5. Preserve `netstandard2.1` compatibility unless a separate non-Unity package is introduced for newer .NET transports.
6. Reopen the preview1 freeze where current fixed metadata bakes in render-first camera/tile/view assumptions; those semantics should migrate toward tensor-profile-specific structures instead of remaining the public baseline.

### 0.4 Current Checkpoint

- [x] As of 2026-05-06, implementation has reached the fixed-layout core layer: header strict parsing, preview1 enum values, alignment helpers, fixed binary reader/writer, `FrameSubmitMetadata`, `ResultPushMetadata`, and `TensorSectionDescriptor`.
- [x] Those currently implemented preview1 metadata codecs still represent the legacy render-first baseline and now need to be replaced by the redesigned common-core/profile split.
- [x] Verification completed with `dotnet test Nnrp.sln`, `dotnet format Nnrp.sln --verify-no-changes --verbosity minimal`, and a Release `dotnet pack` into a temporary output directory.
- [x] As of 2026-05-06, client/server profile validation and transport-neutral capability negotiation are implemented with per-package line coverage gates at 90% or higher.
- [x] As of 2026-05-06, transport-neutral session state, frame lifecycle tracking, and protocol failure mapping are implemented in `Nnrp.Core` with unit coverage.
- [x] As of 2026-05-06, transport-neutral framed message abstractions are implemented in `Nnrp.Core` without QUIC or Unity engine dependencies.
- [x] As of 2026-05-08, cache model types (NnrpCacheKey, NnrpCacheEntry, NnrpCacheStore, NnrpCacheResult), server RESULT_DROP send, server cache handling (HandleCachePut/HandleCacheInvalidate), SubmitOutcome coverage, ControlMetadataBitmaps coverage, and fuzz tests are implemented with unit coverage. Nnrp.Client line coverage is at 91.59% (≥90%). Nnrp.Core and Nnrp.Server still need additional targeted tests to reach 90% line coverage.
- [x] As of 2026-05-08, 8-byte block alignment is enforced in `FrameSubmitMessage` and `ResultPushMessage` body layout (ToFramedMessage, TryParse, TryGetAlignedBodyLength). Golden vector tests updated to dynamically construct aligned packets. All 194 tests pass (1 skipped).
- [x] Release DLLs built and packed (`Nnrp.Core.0.1.0-preview1.nupkg`, `Nnrp.Client.0.1.0-preview1.nupkg`, `Nnrp.Server.0.1.0-preview1.nupkg`, `Nnrp.NativeBridge.0.1.0-preview1.nupkg`).
- [x] The remaining preview1 redesign follow-ups now take priority over new preview2 body/object-reference implementation; land those first once the cross-SDK semantics are frozen.

### 0.5 Upstream Alignment Watchlist

- [x] Confirm the authoritative current common header length across the protocol design document, runtime notes, and `nnrp-py` implementation.
- [x] Keep the C# SDK aligned with the authoritative 40-byte common header used by the protocol design document and `nnrp-py`.
- [x] Remove the stale 24-byte preview header note from the older runtime H2 todo so future C#/Unity work does not target an incompatible frame shape.
- [x] Clarify that `nnrp-py` owns the complete Python protocol/runtime SDK checklist, while this repository owns the Unity-compatible C# wire codec, client adapter, native bridge glue, and optional transport adapters.
- [x] Replace the current hardcoded cross-language primitive golden vectors with aligned C# golden files plus imported nnrp-py golden artifacts for true cross-language validation.

### 0.6 Preview1 Unfreeze Alignment

- [x] Rebase the C# preview1 codec plan on the redesigned common core plus tensor-profile split.
- [x] Revisit `ClientHello` and `ServerHelloAck` capability models so render-specific topology fields stop being the public baseline.
	- [x] Replace the current `ClientHello` fixed metadata fields that encode tile/view/shape assumptions with the new common capability table: `supported_profile_bitmap`, `supported_payload_kind_bitmap`, `cache_digest_bitmap`, `cache_object_bitmap`, `max_lane_count`, `target_cadence_x100`, `degrade_policy`, `auth_bytes`, and `control_extension_bytes`.
	- [x] Replace the current `ServerHelloAck` fixed metadata fields that encode tile/view/shape assumptions with the new common negotiation table: `accepted_profile_bitmap`, `accepted_payload_kind_bitmap`, `max_lane_count`, `target_cadence_x100`, `degrade_policy`, `max_body_bytes`, `control_extension_bytes`, and `server_flags`.
- [x] Revisit `FrameSubmitMetadata` and `ResultPushMetadata` so camera/tile topology moves into tensor-profile-specific blocks instead of common fixed metadata.
	- [x] Replace common `FrameSubmitMetadata` with the new 32-byte profile-agnostic table and add a tensor-profile `tensor_submit_block` codec.
	- [x] Replace common `ResultPushMetadata` with the new 32-byte profile-agnostic table and add a tensor-profile `tensor_result_block` codec.
- [x] Rework `SessionPatch` and `SessionPatchAck` around the new 36B/48B common metadata plus tensor profile patch blocks.
	- [x] Replace common patch fields `target_fps_x100` and `active_view_mask` with `target_cadence_x100` and `active_lane_mask`.
	- [x] Move tensor shape clamp into `tensor_profile_patch_block` / `tensor_profile_patch_ack_block` codecs.
	- [x] Keep cache object semantics aligned with tensor-profile-local `camera_block` / `tile_index_block` reuse rather than common protocol assumptions.
- [x] Remove the remaining preview1 public-surface carryovers instead of preserving legacy render-first packet semantics.
	- [x] Drop legacy `ClientHelloMetadata` / `ServerHelloAckMetadata` constructors and render-first alias properties from the handshake metadata surface.
	- [x] Add a business-facing preview1 client/server helper layer so callers stop manually composing `ClientHelloMessage` / `FrameSubmitMessage` / `ResultPushMessage` on the hot path.
	- [x] Add a preview1 client submit DTO + result DTO path on top of `NnrpPreviewClient` so the primary API accepts logical frame payload inputs instead of wire messages.
	- [x] Add a preview1 server receive/send DTO path on top of `NnrpPreviewServerSession` so host code no longer handles `FrameSubmitMessage` / `ResultPushMessage` directly in the primary flow.
	- [x] Keep raw wire-message APIs available only as advanced/escape-hatch entry points after the business-facing helper layer lands.
	- [x] Cover the helper layer with client/server preview session tests that assert business-facing request/response flows without hand-written packet metadata.
	- [x] Produce the preview1 release DLL/UPM artifacts as part of the final C# closeout pass.

## Phase 0 Repository Skeleton and Baseline

### Part 0.1 Solution Layout

- [x] Create `Nnrp.sln`.
- [x] Create `src/Nnrp.Core/`.
- [x] Create `src/Nnrp.Client/`.
- [x] Create `src/Nnrp.Server/`.
- [x] Add `Nnrp.Core` to the solution.
- [x] Add `Nnrp.Client` to the solution.
- [x] Add `Nnrp.Server` to the solution.
- [x] Reference `Nnrp.Core` from `Nnrp.Client`.
- [x] Reference `Nnrp.Core` from `Nnrp.Server`.
- [x] Create `tests/`.
- [x] Create `tests/Nnrp.Core.Tests/`.
- [x] Create `tests/Nnrp.Client.Tests/`.
- [x] Create `tests/Nnrp.Server.Tests/`.
- [x] Add test projects to `Nnrp.sln`.
- [x] Add a solution-level CI build entry point.

### Part 0.2 Project Configuration

- [x] Target `netstandard2.1` in `Nnrp.Core`.
- [x] Target `netstandard2.1` in `Nnrp.Client`.
- [x] Target `netstandard2.1` in `Nnrp.Server`.
- [x] Enable nullable reference types in all current projects.
- [x] Define package metadata for `Nnrp.Core`.
- [x] Define package metadata for `Nnrp.Client`.
- [x] Define package metadata for `Nnrp.Server`.
- [x] Decide whether all packages share one version or each package versions independently.
- [x] Add deterministic build settings.
- [x] Add XML documentation generation for public APIs.
- [x] Add analyzer configuration for public SDK quality.
- [x] Add a formatting command or documented `dotnet format` workflow.

### Part 0.3 README Baseline

- [x] Add a minimal project goal to `README.md`.
- [x] Document the current package layout.
- [x] Document the current build command.
- [x] Add a preview1 protocol overview.
- [x] Add a status table for implemented vs planned SDK pieces.
- [x] Add examples for header encode/decode.
- [x] Add examples for frame submit encode/decode after those codecs exist.
- [x] Add example for result push encode/decode.
- [x] Add example for control extension TLV.
- [x] Add Unity 2022 consumption notes.
- [x] Add package publishing notes.
- [x] Document the Rust-native smoke bridge build and loading flow.

## Phase 1 Core Wire Primitives

### Part 1.1 Common Header

- [x] Define `NnrpHeader`.
- [x] Freeze header length as `40` bytes.
- [x] Reconfirm the common header layout against the current QUIC preview runtime contract before adding body codecs that depend on `NnrpHeader`.
- [x] Confirm no migration is required: the current preview contract remains a 40-byte common header.
- [x] Encode `magic` as ASCII `NNRP`.
- [x] Encode `version_major`.
- [x] Encode `version_stage`.
- [x] Encode `msg_type`.
- [x] Encode `header_len`.
- [x] Encode `flags`.
- [x] Encode `meta_len`.
- [x] Encode `body_len`.
- [x] Encode `session_id`.
- [x] Encode `frame_id`.
- [x] Encode `view_id`.
- [x] Encode `route_id`.
- [x] Encode `trace_id`.
- [x] Use little-endian integer encoding.
- [x] Add `Write(Span<byte>)`.
- [x] Add `ToArray()`.
- [x] Add `TryParse(ReadOnlySpan<byte>, out NnrpHeader)`.
- [x] Add value equality for `NnrpHeader`.
- [x] Add a stable hash implementation for `NnrpHeader`.
- [x] Add `TryWrite(Span<byte>, out int bytesWritten)` for allocation-free callers.
- [x] Add parse failure details without throwing on malformed input.
- [x] Reject unsupported `version_major` when strict validation is requested.
- [x] Reject unknown `version_stage` when strict validation is requested.
- [x] Reject unknown `msg_type` when strict validation is requested.
- [x] Reject reserved flags when strict validation is requested.
- [x] Validate `meta_len + body_len` against configurable message limits.

### Part 1.2 Protocol Enums

- [x] Define `VersionStage.Preview1`.
- [x] Define first-round message types.
- [x] Define common header flags.
- [x] Define `FrameClass`.
- [x] Define first-round error codes.
- [x] Add `InputProfile` values.
- [x] Add `TileIndexMode` values.
- [x] Add `TensorRole` values.
- [x] Add `CodecId` values.
- [x] Add `DTypeId` values.
- [x] Add `TensorLayoutId` values.
- [x] Add `ScalePolicy` values.
- [x] Add `ResultStatusCode` values.
- [x] Add `ResultFlags` values.
- [x] Document all enum numeric values in XML comments.
- [x] Add tests that lock every enum value to the protocol table.

### Part 1.3 Alignment and Binary Helpers

- [x] Define an 8-byte alignment helper.
- [x] Define padding calculation helpers.
- [x] Define zero-padding validation helpers.
- [x] Add safe checked arithmetic for offset and length calculations.
- [x] Add a small binary reader for fixed-layout structs.
- [x] Add a small binary writer for fixed-layout structs.
- [x] Keep helper APIs allocation-free where practical.
- [x] Add tests for edge offsets and overflow protection.

## Phase 2 Metadata and Section Layouts

### Part 2.1 Frame Submit Metadata

- [x] Define a fixed-layout `FrameSubmitMetadata` type.
- [x] Freeze metadata length as `52` bytes.
- [x] Encode `src_width`.
- [x] Encode `src_height`.
- [x] Encode `tile_width`.
- [x] Encode `tile_height`.
- [x] Encode `tile_count`.
- [x] Encode `section_count`.
- [x] Encode `frame_class`.
- [x] Encode `input_profile`.
- [x] Encode `tile_index_mode`.
- [x] Encode `latency_budget_ms`.
- [x] Encode `target_fps_x100`.
- [x] Encode `retry_of_frame`.
- [x] Encode `tile_base_id`.
- [x] Encode `camera_bytes`.
- [x] Encode `tile_index_bytes`.
- [x] Preserve reserved fields as zero on write.
- [x] Reject non-zero reserved fields in strict parse mode.
- [x] Add round-trip tests using protocol example values.

### Part 2.2 Result Push Metadata

- [x] Define a fixed-layout `ResultPushMetadata` type.
- [x] Freeze metadata length as `44` bytes.
- [x] Encode `status_code`.
- [x] Encode `result_flags`.
- [x] Encode `section_count`.
- [x] Encode `tile_count`.
- [x] Encode `active_profile_id`.
- [x] Encode `inference_ms`.
- [x] Encode `queue_ms`.
- [x] Encode `server_total_ms`.
- [x] Encode `tile_base_id`.
- [x] Encode `tile_index_bytes`.
- [x] Preserve reserved fields as zero on write.
- [x] Reject non-zero reserved fields in strict parse mode.
- [x] Add round-trip tests using protocol example values.

### Part 2.3 Tensor Section Descriptor

- [x] Define a fixed-layout `TensorSectionDescriptor` type.
- [x] Freeze descriptor length as `32` bytes.
- [x] Encode `role_id`.
- [x] Encode `codec_id`.
- [x] Encode `dtype_id`.
- [x] Encode `layout_id`.
- [x] Encode `scale_policy`.
- [x] Encode section `flags`.
- [x] Encode `element_count_per_tile`.
- [x] Encode `codec_table_bytes`.
- [x] Encode `length_table_bytes`.
- [x] Encode `payload_bytes`.
- [x] Encode `payload_stride_bytes`.
- [x] Preserve reserved fields as zero on write.
- [x] Reject inconsistent fixed-stride and variable-length combinations.
- [x] Add descriptor validation tests.

## Phase 3 Hot Path Message Codecs

### Part 3.0 Protocol Reconciliation Gate

- [x] Resolve the common header question before Phase 3 implementation: preview1 currently uses the same 40-byte common header across the protocol design document, `nnrp-py`, and the C# SDK.
- [x] Capture the exact byte layout for the final preview `FRAME_SUBMIT` header and metadata pairing (frozen in C# golden vectors).
- [x] Capture the exact byte layout for the final preview `RESULT_PUSH` header and metadata pairing (frozen in C# golden vectors).
- [x] Validate C# header and metadata output against runtime/`nnrp-py` golden artifacts for the common header, `CLIENT_HELLO`, `SESSION_PATCH`, `SESSION_PATCH_ACK`, and `CACHE_*` metadata.
- [x] Re-validate all checked Phase 1 and Phase 2 fixed lengths and field offsets after importing nnrp-py golden artifacts; no preview1 contract updates were required.

### Part 3.1 Tile Index Blocks

- [x] Implement `dense_range` tile index handling.
- [x] Implement `raw_u16` tile index handling.
- [x] Implement `delta_u16` tile index handling.
- [x] Implement `bitset` tile index handling.
- [x] Validate tile count against metadata.
- [x] Validate tile index block length against metadata.
- [x] Validate monotonic ordering where the mode requires it.
- [x] Add tests for empty, one-tile, dense, sparse, and maximum-count cases.

### Part 3.2 Frame Submit Codec

- [x] Define an immutable `FrameSubmitMessage` model.
- [x] Encode `NnrpHeader` plus `FrameSubmitMetadata`.
- [x] Encode `camera_block`.
- [x] Encode `tile_index_block`.
- [x] Encode one or more tensor sections.
- [x] Preserve body order as `camera_block`, `tile_index_block`, `tensor_section[]`.
- [x] Enforce 8-byte block alignment.
- [x] Validate `meta_len` against the metadata length.
- [x] Validate `body_len` against all body blocks.
- [x] Expose allocation-free parsing for payload spans (uses `ReadOnlyMemory<byte>` / `Span<byte>`).
- [x] Add round-trip tests for dense luma frame input.
- [x] Add round-trip tests for changed tile input.
- [x] Add malformed body tests.

### Part 3.3 Result Push Codec

- [x] Define an immutable `ResultPushMessage` model.
- [x] Encode `NnrpHeader` plus `ResultPushMetadata`.
- [x] Encode `tile_index_block`.
- [x] Encode result tensor sections.
- [x] Preserve body order as `tile_index_block`, `tensor_section[]`.
- [x] Enforce 8-byte block alignment.
- [x] Validate `meta_len` against the metadata length.
- [x] Validate `body_len` against all body blocks.
- [x] Expose allocation-free parsing for payload spans (uses `ReadOnlyMemory<byte>` / `Span<byte>`).
- [x] Add round-trip tests for fresh result payloads.
- [x] Add round-trip tests for stale result payloads.
- [x] Add malformed body tests.

### Part 3.4 Control Message Codecs

- [x] Define a `ClientHello` model.
- [x] Define a `ServerHelloAck` model.
- [x] Define a `SessionPatch` model.
- [x] Define a `SessionPatchAck` model.
- [x] Define a `FrameCancel` model.
- [x] Define `Ping` and `Pong` models.
- [x] Define a `Close` model.
- [x] Define an `Error` model.
- [x] Define `CachePut`, `CacheAck`, and `CacheInvalidate` models.
- [x] Decide the preview1 binary layout for each control metadata block.
- [x] Keep `FRAME_CANCEL / PING / PONG` header-only in preview1.
- [x] Add constrained `control_extension_block` TLV helpers for low-frequency control messages.
- [x] Ignore unknown optional control extensions and fail unknown critical control extensions.
- [x] Add malformed TLV, truncation, and alignment tests for control extensions.
- [x] Keep auth material outside high-frequency frame metadata.
- [x] Add strict state and capability validation for handshake messages.
- [x] Add control message round-trip tests.

## Phase 4 Client and Server Profiles

### Part 4.1 Client Profile

- [x] Create `ClientProfile`.
- [x] Add `MaxViews`.
- [x] Add `EnableCache`.
- [x] Add `MaxCacheEntries`.
- [x] Add supported codec negotiation fields.
- [x] Add supported dtype negotiation fields.
- [x] Add supported tensor layout fields.
- [x] Add tile layout preference fields.
- [x] Add resolution range fields.
- [x] Add target FPS range fields.
- [x] Add latency or quality preference fields.
- [x] Add auth block provider hooks without storing secrets in logs.
- [x] Add validation for invalid profile values.
- [x] Add conversion from `ClientProfile` to `ClientHello`.

### Part 4.2 Server Profile

- [x] Create `ServerProfile`.
- [x] Add `MaxConcurrentFrames`.
- [x] Add `EnableCache`.
- [x] Add `MaxCacheEntries`.
- [x] Add accepted codec fields.
- [x] Add accepted dtype fields.
- [x] Add accepted tensor layout fields.
- [x] Add maximum body size.
- [x] Add maximum section count.
- [x] Add maximum tile count.
- [x] Add maximum view count.
- [x] Add token TTL or session renewal policy fields.
- [x] Add validation for invalid server profile values.
- [x] Add negotiation from `ClientHello` to `ServerHelloAck`.

### Part 4.3 Capability Negotiation

- [x] Define capability set value objects.
- [x] Define client-to-server contract checks.
- [x] Define server selection rules for codec, dtype, layout, cache, and views.
- [x] Return `unsupported_capability` for rejected combinations.
- [x] Return `limit_exceeded` for valid but oversized requests.
- [x] Add negotiation unit tests for accepted capabilities.
- [x] Add negotiation unit tests for rejected capabilities.

## Phase 5 Session and Frame State Machines

### Part 5.1 Connection and Session State

- [x] Define connection/session states.
- [x] Enforce `INIT -> NEGOTIATING`.
- [x] Enforce `NEGOTIATING -> ACTIVE`.
- [x] Enforce negotiation failure to `CLOSED`.
- [x] Enforce `ACTIVE -> DRAINING` on `CLOSE` or fatal `ERROR`.
- [x] Reject `FRAME_SUBMIT` before `ACTIVE`.
- [x] Reject new `FRAME_SUBMIT` after `DRAINING`.
- [x] Add tests for invalid transitions.

### Part 5.2 Frame Lifecycle

- [x] Define frame lifecycle states.
- [x] Track `ANNOUNCED`.
- [x] Track `SUBMITTED`.
- [x] Track `PROCESSING`.
- [x] Track `READY`.
- [x] Track `DELIVERED`.
- [x] Track `DROPPED`.
- [x] Track `CANCELLED`.
- [x] Track `EXPIRED`.
- [x] Validate retransmit frames with `retry_of_frame`.
- [x] Keep different `view_id` values independent for the same `frame_id`.
- [x] Add lifecycle tests for cancel, drop, retransmit, and expiry.

### Part 5.3 Error Model

- [x] Define a typed SDK exception or result model for protocol failures.
- [x] Preserve stable protocol `ErrorCode` values.
- [x] Add error scope: connection, session, or frame.
- [x] Map malformed header failures to `malformed_header`.
- [x] Map malformed body failures to `malformed_body`.
- [x] Map invalid state failures to `invalid_state`.
- [x] Map unsupported capabilities to `unsupported_capability`.
- [x] Map message size violations to `limit_exceeded`.
- [x] Ensure fatal errors move the session to draining or closed.
- [x] Add error mapping tests.

## Phase 6 Transport Adapters

### Part 6.1 Transport-Neutral Core

- [x] Define minimal send and receive abstractions for framed NNRP messages.
- [x] Keep the core codec independent from `System.Net.Quic`.
- [x] Keep the core codec independent from Unity engine assemblies.
- [x] Support caller-owned buffers for high-frequency payloads.
- [x] Support cancellation tokens in async APIs.
- [x] Document thread-safety expectations.

### Part 6.2 Client Adapter

- [x] Define an `INnrpClientSession` interface.
- [x] Add a transport-neutral client session facade for profile negotiation, local state transitions, frame-submit gating, cancellation-aware calls, and Unity SDK wiring smoke tests.
- [x] Implement handshake orchestration over abstract streams.
- [x] Implement `FRAME_SUBMIT` send flow.
- [x] Implement `FRAME_CANCEL` send flow.
- [x] Implement `RESULT_PUSH` receive flow.
- [x] Implement `RESULT_DROP` receive flow.
- [x] Surface `RESULT_DROP` as a typed discard or protocol outcome for callers.
- [x] Implement ping/pong latency measurement.
- [x] Add graceful close handling.
- [x] Add client-side adapter tests with fake streams.

### Part 6.3 Server Adapter

- [x] Define an `INnrpServerSession` interface.
- [x] Implement server-side handshake handling.
- [x] Implement `FRAME_SUBMIT` receive flow.
- [x] Implement `RESULT_PUSH` send flow.
- [x] Implement `RESULT_DROP` send flow.
- [x] Implement cache control message handling.
- [x] Add graceful close handling.
- [x] Add server-side adapter tests with fake streams.

### Part 6.4 QUIC Integration Boundary

- [x] Decide whether QUIC support lives in a separate package.
	- [x] Unity-compatible `netstandard2.1` packages stay transport-neutral.
	- [x] Any newer-.NET `System.Net.Quic` experiment must stay in a separate non-Unity sample or adapter package.
- [x] Select the Unity/C# QUIC library or transport binding for the preview client path.
	- [x] Do not use `System.Net.Quic` as the Unity 2022 adapter dependency.
	- [x] Choose a native QUIC bridge as the immediate bring-up path.
- [x] Add the first Unity-compatible managed glue layer for a native QUIC bridge.
- [x] Replace the Python-backed native bridge repro packet builder/parser with managed preview smoke packet helpers in `Nnrp.Core`.
- [x] Add typed `FrameSubmitMessage` and `ResultPushMessage` helpers on the managed native-bridge boundary so local smoke no longer has to treat preview packets as opaque `byte[]`.
- [x] Add typed native-bridge helpers for `PING/PONG` and `FRAME_CANCEL` so runtime preview control handling can be exercised over the real QUIC path.
- [x] Build the Rust native bridge as a loadable library for local smoke.
- [x] Add a Unity-compatible `quic_preview` client entry point if the selected QUIC stack supports the target runtime.
- [x] Support a transport profile switch between the existing control/evidence path and `quic_preview` data/result path.
- [x] Document the current ALPN values `nnrp/1` for QUIC and `nnrp/1-tcp` for TCP.
- [x] Document the secure URI scheme `nnrps://`.
- [x] Keep Unity-compatible builds free from unsupported QUIC dependencies.
- [x] Send a minimal preview frame header and tile payload to the runtime QUIC preview listener.
- [x] Receive a minimal preview result header and payload from the runtime QUIC preview listener.
- [x] Add loopback smoke coverage against the runtime preview bridge when the runtime side is available.
- [x] Add an optional newer-.NET sample for QUIC transport if feasible.
- [x] Add an integration smoke test when a supported QUIC runtime is available.
- [x] Make the native QUIC session lifetime explicit for preview bring-up.
- [x] Stop relying on the QUIC stack default idle timeout in the Rust native bridge; set keepalive / idle policy suitable for connect-first, submit-later flows.
- [x] Surface a non-Unity preview warm-up path that can connect and optionally ping before the first frame submit.
- [x] Surface `RESULT_DROP` as a first-class typed outcome in the client adapter so reorder/drop-aware consumers do not need to infer silent loss.

## Phase 7 Cache Semantics

### Part 7.1 Cache Model

- [x] Define a cache key type with stable digest bytes.
- [x] Define cache namespaces or scopes.
- [x] Define cache object metadata.
- [x] Define cache TTL and invalidation metadata.
- [x] Validate cache key length and digest algorithm.
- [x] Keep cache references optional for preview1 hot path correctness.

### Part 7.2 Cache Control Messages

- [x] Implement `CACHE_PUT` encoding.
- [x] Implement `CACHE_ACK` encoding.
- [x] Implement `CACHE_INVALIDATE` encoding.
- [x] Validate cache object size against negotiated limits.
- [x] Return `cache_miss` for missing required objects.
- [x] Return `limit_exceeded` for oversized cache objects.
- [x] Add cache control round-trip tests.

## Phase 8 Verification and Tooling

### Part 8.1 Unit Tests

- [x] Add test framework dependency.
- [x] Test header write and parse round trips.
- [x] Test malformed header rejection.
- [x] Test enum numeric stability.
- [x] Test metadata codecs.
- [x] Test tensor section descriptor codecs.
- [x] Test tile index codecs.
- [x] Test frame submit codecs.
- [x] Test result push codecs.
- [x] Test control message codecs.
- [x] Test state machines.
- [x] Test negotiation logic.

### Part 8.2 Golden Vectors

- [x] Create `tests/vectors/`.
- [x] Generate C# golden binaries for dense and changed-tile `FRAME_SUBMIT` packets.
- [x] Generate C# golden binaries for fresh and stale `RESULT_PUSH` packets.
- [x] Generate C# golden binaries for `CLIENT_HELLO`, `SERVER_HELLO_ACK`, `SESSION_PATCH`, `SESSION_PATCH_ACK`, `CACHE_PUT`, `CACHE_ACK`, `CACHE_INVALIDATE`, `RESULT_DROP`, and `ERROR`.
- [x] Verify C# round-trip against golden binaries.
- [x] Import nnrp-py golden vectors into C# tests for preview1 cross-language validation.
- [x] Verify header, metadata, tile index, and tensor section alignment against the Python implementation for `FRAME_SUBMIT` and `RESULT_PUSH` golden packets.

### Part 8.3 Fuzz and Robustness Tests

- [x] Add malformed length fuzz tests.
- [x] Add malformed enum fuzz tests.
- [x] Add random padding fuzz tests.
- [x] Add truncated payload tests.
- [x] Add oversized payload tests.
- [x] Add reserved-bit tests.
- [x] Add offset overflow tests.
- [x] Add tests for parser non-throwing behavior on untrusted input.

### Part 8.4 Build and CI

- [x] Add CI restore step.
- [x] Add CI build step.
- [x] Add CI test step.
- [x] Add CI formatting or analyzer step.
- [x] Add artifact upload for test results.
- [x] Add package build verification.

## Phase 9 Documentation and Release Readiness

### Part 9.1 SDK Documentation

- [x] Document package responsibilities.
- [x] Document the common header and explicitly note that preview1 currently fixes `header_len` at 40 bytes across the design doc, `nnrp-py`, and `nnrp-cs`.
- [x] Document metadata layouts implemented by the SDK.
- [x] Document allocation and buffer ownership rules.
- [x] Document validation modes.
- [x] Document client profile negotiation.
- [x] Document server profile negotiation.
- [x] Document transport adapter boundaries.
- [x] Document Unity compatibility constraints.

### Part 9.2 Samples

- [x] Add a header encode/decode sample (`samples/MinimalSamples.cs`).
- [x] Add a frame submit encode sample.
- [x] Add a result push decode sample (in README).
- [x] Add a fake client/server in-memory sample.
- [x] Add a Unity-oriented sample without unsupported runtime dependencies (via `nnrp-cs` managed DLLs).
- [x] Add a newer-.NET QUIC sample when that adapter exists (`samples/Nnrp.QuicPreview.Net8Sample/`).

### Part 9.3 Preview1 Acceptance Criteria

- [x] `dotnet build Nnrp.sln` succeeds locally.
- [x] Unit tests pass locally.
- [x] Header golden vectors are stable.
- [x] `FRAME_SUBMIT` golden vectors are stable.
- [x] `RESULT_PUSH` golden vectors are stable.
- [x] Strict parser rejects malformed inputs without unsafe reads.
- [x] Core package remains `netstandard2.1` compatible.
- [x] Public APIs are documented enough for Unity integration.
- [x] README clearly states current preview1 limitations.
- [x] A preview package can be produced from a clean checkout.
