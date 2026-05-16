# NNRP v1-preview2 C# SDK Todo

## 0. Usage Notes

### 0.1 Scope

1. This checklist covers the C# SDK work required to implement `NNRP/1-preview2` as a Unity-compatible wire-contract package.
2. This checklist covers `Nnrp.Core`, `Nnrp.Client`, `Nnrp.NativeBridge`, and the host/package surfaces that need preview2 support.
3. This checklist does not define preview2 wire semantics on its own; the protocol design document in `nnrp-doc` remains authoritative.
4. Repository policy has since moved away from checked-in Unity/DLL staging; any historical checklist item mentioning `unity/com.nnrp.client` or `stage_unity_package.ps1` refers to a retired distribution path rather than the current packaging baseline.

### 0.2 Preview2 Boundaries

1. Treat preview2 as the in-place update for the active `NNRP/1` C# surface; evolve existing models in place instead of preserving parallel preview1/preview2 public type families.
2. Keep the hot path binary and fixed-layout.
3. Treat native QUIC and Unity worker/bridge changes as adapter work, not core wire-contract work, unless a semantic must become protocol-visible.
4. Treat preview2 as the version that must expose a real async submit/result session model to hosts; do not postpone that semantic to preview3 when the current wire shape already supports it.
5. During preview stages inside `NNRP/1`, replace superseded public helpers and types in place rather than retaining older preview-era surfaces.

### 0.3 Remaining Work Buckets

1. `Protocol/preview1 follow-up`: items such as `SessionPatch` inheritance and future standard extension-frame kind assignment remain gated by preview1/common-model evolution or later protocol freezes in `nnrp-doc`; the C# SDK must not invent private wire for them.
2. `Core C# completion`: remaining open codec/runtime items are payload-family-specific content models, hot-path `Span<byte>` write preservation, stricter parse coverage, and a native/managed background result pump.
3. `Unity/package/docs/process`: remaining Unity surface decisions, package/distribution policy, repository-local host guidance, and solution-wide green-test enforcement are SDK delivery work rather than protocol-freeze gaps.

## 1. Current Baseline

- [x] `NNRP/1-preview1` fixed header, metadata primitives, transport-neutral state machine, and native smoke bridge scaffolding already exist.
- [x] The native bridge has already moved away from the most naive synchronous registry-lock path and can be used as the baseline for preview2 transport work.
- [x] Preview2 body codecs, typed-payload descriptor/frame-region handling, object-reference submit semantics, and tensor/partial-result body handling are implemented on the C# wire surface; the remaining gaps are payload-family-specific content models and host/package integration.
- [x] Current client-facing preview helpers now expose the canonical async session shape explicitly; `submit -> await RESULT_PUSH` remains only a convenience path.
- [x] Preview1 redesign follow-ups no longer block preview2 body/object-reference implementation slices here; the remaining work is C# SDK completion and todo cleanup against `nnrp-doc`.

## 1.1 Preview1 Redesign Dependency

- [x] Rebase preview2 C# wire-model work on top of the redesigned preview1 common core instead of the legacy render-first submit/result tables.
- [x] Revisit preview2 capability negotiation once preview1 `ClientHello / ServerHelloAck` common fields stop carrying render-specific topology assumptions.
	- [x] Rebase preview2 handshake extensions on top of the new preview1 64B `ClientHello` and 80B `ServerHelloAck` fixed metadata tables.
	- [x] Reconfirm which preview2 negotiation fields remain fixed metadata additions and which must remain control extensions after preview1 moves profile-local topology out of common metadata.
- [x] Revisit preview2 `SessionPatch` inheritance once preview1 patch flow becomes 36B/48B common metadata plus tensor profile patch blocks.
- [x] Revisit preview2 result and payload bookkeeping so non-tensor payloads do not inherit tile-based coverage rules from the old preview1 baseline.
- [x] Revisit preview2 cache/object-kind planning now that camera/tile topology objects are explicitly tensor-profile-local in preview1.
- [x] Keep object-reference, typed-payload, and migration work aligned with the new preview1 core/profile split across `Nnrp.Core`, `Nnrp.Client`, and `Nnrp.NativeBridge`.
- [x] Freeze preview2 typed-payload semantic contract in the protocol doc before either SDK starts inventing descriptor/body interpretations.
	- [x] Freeze `payload_frame_count` as logical typed payload frame count rather than tensor section count.
	- [x] Freeze descriptor `offset / length` as byte ranges relative to the typed payload frame region.
	- [x] Freeze the rule that non-tensor payloads must not masquerade as tensor sections or tensor coverage metadata.
	- [x] Freeze the final fixed-width preview2 `BodyRegionPrelude` / `InlineObjectBlockHeader` / `ObjectReferenceBlock` / `TypedPayloadDescriptor` / `ExtensionFrameDescriptor` byte layout before starting C# body codecs.
	- [x] Freeze the scope boundary that the first preview2 slice only supports low-frequency object references plus inline typed payloads, and does not allow the C# SDK to invent a private typed-payload data-reference wire.

## 2. Core Versioning And Enums

- [x] Add `VersionStage.Preview2`.
- [x] Add preview2 message types `FLOW_UPDATE` and `RESULT_HINT`.
- [x] Add preview2 message types `TRANSPORT_PROBE` (`0x19`) and `TRANSPORT_PROBE_ACK` (`0x1a`) for Transport Probing Phase.
- [x] Add preview2 message types `SESSION_MIGRATE` (`0x1b`) and `SESSION_MIGRATE_ACK` (`0x1c`) for in-session transport fallback.
- [x] Freeze protocol numeric values for `transport_id`, `payload_kind`, `submit_mode`, `budget_policy`, `result_class`, `object_kind`, and `invalidate_scope` in the design doc.
- [x] Add preview2 enums for submit mode, object kind, payload kind, result class, and budget policy.
	- [x] Add `SubmitMode`.
	- [x] Keep `BudgetPolicy`, `ResultClass`, and `PayloadKind` aligned with the frozen preview2 design values.
	- [x] Reconcile preview2 `object_kind` / `invalidate_scope` enums against the legacy preview1 cache enums without breaking existing preview1 golden vectors.
- [x] Keep extension-frame kind table reserved until the protocol assigns standard values; do not add speculative private C# extension-kind enums.
- [x] Add tests that freeze every preview2 enum numeric value currently modeled in `Nnrp.Core`.

## 3. Core Wire Models

### 3.1 Common Header

- [x] Reconfirm preview2 keeps the 40-byte common header.
- [x] Extend strict parsing so preview1 and preview2 stage handling remains explicit.

### 3.2 Control Plane Metadata

- [x] Freeze which preview2 handshake/runtime capabilities stay in fixed metadata, which use `ClientHello / ServerHelloAck` control extensions, and which are standalone control messages.
	- [x] Keep object-reference capability / accepted reference kinds in fixed metadata via `cache_object_bitmap`.
	- [x] Keep downgrade-policy baseline in fixed metadata via `degrade_policy` rather than inventing extra handshake extensions.
	- [x] Keep runtime flow-control bounds in fixed metadata via `max_concurrent_frames`; dynamic credit and backpressure updates ride `FLOW_UPDATE`, not additional handshake extensions.
	- [x] Keep typed-payload negotiation in `0x0105 / 0x0106` while preserving `supported_payload_kind_bitmap / accepted_payload_kind_bitmap` in fixed metadata.
- [x] Add transport policy declaration to `ClientHello` and `ServerHelloAck`.
	- [x] Define `transport_policy` enum: `auto / prefer_quic / prefer_tcp / force_quic / force_tcp`.
	- [x] Add optional `preferred_transport_id` to `ClientHello` extension.
	- [x] Add `active_transport_id` and accepted/downgraded transport policy echo to `ServerHelloAck` extension.
- [x] Add session-level loss tolerance declaration to `ClientHello` and `ServerHelloAck`.
	- [x] Define `loss_tolerance` enum: `strict / best_effort / low_latency / fire_and_forget`.
	- [x] Freeze control extension type ids in protocol doc: `0x0103` for `ClientHello`, `0x0104` for `ServerHelloAck`.
	- [x] Add `session_loss_tolerance: u8` field to `ClientHello` loss-tolerance control extension (`0x0103`).
	- [x] Add `accepted_loss_tolerance: u8` field to `ServerHelloAck` loss-tolerance control extension (`0x0104`).
- [x] Add typed-payload negotiation to `ClientHello` and `ServerHelloAck`.
	- [x] Freeze control extension type ids in protocol doc: `0x0105` for `ClientHello`, `0x0106` for `ServerHelloAck`.
	- [x] Freeze `payload_kind_bitmap:u32` bit assignments in protocol doc.
	- [x] Add `payload_capabilities` / `payload_capabilities_ack` control extensions.
	- [x] Add reserved-zero `critical_extension_frame_bitmap:u32` negotiation.
- [x] Add `FlowUpdateMetadata`.
- [x] Add `ResultHintMetadata`.
- [x] Freeze `object_kind:u16` and `invalidate_scope:u8` numeric values in protocol doc.
- [x] Extend `CACHE_*` metadata models with preview2 object-kind semantics.

### 3.3 Data Plane Metadata

- [x] Add `FrameSubmitMetadata`.
	- [x] Freeze `submit_mode:u8` and `budget_policy:u8` encoding rules in protocol doc.
	- [x] Add `object_ref_mask: u32` field and freeze submit-side standard slot bits (`camera / tile_index / tensor_section_table / payload_layout_template`).
	- [x] Add `loss_tolerance_policy: u8` field to override session default per-frame.
	- [x] Add `payload_kind_bitmap` and `payload_frame_count` fields.
	- [x] Add strict parse/validation rules for `inline / reference / mixed` mode against `object_ref_mask`.
- [x] Add preview2 result metadata updates.
	- [x] Freeze `result_class:u8` numeric values in protocol doc.
	- [x] Add `applied_budget_policy`, `reused_frame_id`, `covered_tile_count`, `dropped_tile_count`, `payload_kind_bitmap`, and `payload_frame_count` fields.
- [x] Add payload-profile coverage helpers so token/audio/video/event payloads do not have to reuse tensor tile counters.
	- [x] Add descriptor-derived `payload_kind + profile_id -> frame_count + payload_bytes` coverage summaries for typed-payload bookkeeping.
	- [x] Expose descriptor-backed typed-payload frame views and kind/profile lookup helpers on `ResultPushMessage` so host code can consume per-frame payload spans without manually re-slicing the raw payload region or re-filtering the full frame list.
	- [x] Add payload-family-specific span/coverage models for `token_chunk / audio_chunk / video_chunk / structured_event` instead of stopping at frame-count and byte-count summaries.
	- [x] Thread payload-profile coverage through host-facing client/native result surfaces once mixed-body preview2 result APIs exist.
		- [x] Preserve typed-payload frame views and kind/profile lookup on `NnrpPreviewSubmitResult` so preview2 client submit results can surface mixed-body payload frames directly.
		- [x] Keep mixed-body payload frame access available through native bridge result paths by smoke-covering preview2 typed-payload `SubmitWithOutcome` parsing on top of `ResultPushMessage`.
- [x] Preserve allocation-aware `Span<byte>` write paths for all preview2 fixed-layout metadata.
- [x] Add strict parse tests for reserved fields, illegal flag combinations, `object_ref_mask` violations, and mismatched tile coverage.

## 4. Core Body Codecs

### 4.1 Body Region Prelude And Object Blocks

- [x] Define transport-neutral models for `BodyRegionPrelude`, `InlineObjectBlockHeader`, and `ObjectReferenceBlock`.
- [x] Add `BodyRegionPrelude` validation so every region has explicit lengths and deterministic offsets.
- [x] Implement low-frequency object reference encode/decode using `cache_namespace + cache_key_hi + cache_key_lo` instead of private C# handles.
- [x] Implement submit-side standard object-slot ordering for `camera_block / tile_index_block / tensor_section_table / payload_layout_template`.
- [x] Add validation that `object_ref_mask` and object-reference region entries agree on required referenced objects and object kinds.

### 4.1A Typed Payload And Extension Frames

- [x] Define `TypedPayloadDescriptor` and `ExtensionFrameDescriptor` wire models.
- [x] Keep `TypedPayloadDescriptor` fixed at 16 bytes and `ExtensionFrameDescriptor` fixed at 16 bytes.
- [x] Implement descriptor-level typed payload envelope parsing/building and mixed-body descriptor validation for preview2 typed payload regions.
	- [x] Parse and validate ordered `TypedPayloadDescriptor[]` regions against `payload_kind_bitmap` and `payload_frame_count`.
	- [x] Summarize validated descriptors into payload-kind/profile coverage for result-side bookkeeping.
	- [x] Decode/build payload-family-specific content models for `token_chunk / audio_chunk / video_chunk / structured_event / tool_delta / opaque_bytes`.
	- [x] Wire mixed preview2 body builders so composite result bodies can be emitted, not just parsed/validated.
- [x] Enforce `typed_payload_descriptor_bytes == payload_frame_count * 16` and ordered non-overlapping payload ranges.
- [x] Reject preview2 typed-payload and extension regions that leave trailing payload bytes unclaimed by descriptors or carry orphan payload bytes without descriptors.
- [x] Implement fast-skip logic for unknown non-critical extension frames.
- [x] Reject unknown critical extension frames with a stable protocol error.
- [x] Keep typed-payload data reference blocks out of scope unless the protocol doc allocates a standard region or descriptor rule for them.

### 4.2 Partial And Stale Results

- [x] Implement `RESULT_PUSH` parsing/building for `complete / partial / stale_reuse / degraded` result classes.
	- [x] Parse preview2 metadata contracts for `partial / stale_reuse / degraded` and preserve them through core/client/native/server smoke paths.
	- [x] Keep the current preview2 tensor-only path aligned with `nnrp-py`/backend reality first: support preview2 tensor-inline `RESULT_PUSH` parse/build before mixed typed-payload result bodies.
	- [x] Parse preview2 composite result bodies far enough to retain typed-payload descriptors and per-profile coverage bookkeeping on `ResultPushMessage`.
	- [x] Build preview2 composite `RESULT_PUSH` bodies with typed payload regions; canonical inline/object-expanded re-emit is now wired through `ResultPushMessage`.
- [x] Allow preview2 dense-range result bodies with `tile_index_bytes == 0` to omit tile-index object/reference blocks instead of rejecting them during object-contract validation.
- [x] Parse preview2 result bodies that carry tile-index or tensor-section-table object references into real `ResultPushMessage` outputs via a resolver-aware parse path; keep resolver-less parse from silently validating then discarding the referenced contract.
- [x] Add covered-tile and dropped-tile bookkeeping in the body codec layer.
- [x] Add tests for partial tile coverage and reused-frame metadata.
- [x] Add profile-specific span/coverage bookkeeping for token/audio/video/event payloads.
	- [x] Summarize typed payload coverage by `payload_kind + profile_id`, and reject preview2 metadata that declares non-tensor payload kinds without matching typed-payload descriptors.
	- [x] Preserve parsed typed-payload descriptors and per-profile coverage summaries on preview2 composite `ResultPushMessage` outputs.
	- [x] Preserve descriptor-backed typed-payload frame views on preview2 composite `ResultPushMessage` outputs so callers can access per-frame payload bytes directly.
- [x] Add result-side ordering/validation for low-frequency object blocks and object reference blocks carried ahead of typed payload descriptors.

## 5. Client And Native Bridge

### 5.1 Transport-Neutral Client

- [x] Extend `Nnrp.Client` session/profile models to express preview2 object-reference support, typed-payload support, and degrade-policy preferences.
- [x] Add flow-update handling hooks to the transport-neutral client facade.
- [x] Add a canonical preview2 client session surface that separates `send_submit` from result consumption instead of treating `SubmitAsync` as an implicit wait-for-result boundary.
	- [x] Switch the current request-based `SubmitAsync` convenience path to follow the negotiated version stage and emit preview2 tensor-inline submits/results when the session actually negotiated preview2.
- [x] Add explicit in-flight frame tracking and result correlation helpers so multiple preview2 submits can remain active on one live session.
	- [x] Reject `SERVER_HELLO_ACK` stage downgrades that are not present in the initiating `CLIENT_HELLO supported_stage_bitmap`; do not silently keep preview fallback behavior in preview-only connect paths.
	- [x] Buffer out-of-order `RESULT_PUSH` packets for other in-flight frames so explicit `ReceiveResultAsync(frame_id, view_id)` remains valid under multi-frame live sessions.
- [x] Keep any one-shot `submit_and_wait` helper as an opt-in convenience path only; do not make it the default host-facing preview2 usage pattern.
- [x] Add client-side helpers for token streaming, multimodal chunks, and structured event delivery on top of preview2 typed payload frames.

### 5.2 Native Bridge

- [x] Extend the Rust native bridge to negotiate preview2 stage and ALPN.
- [x] Add native bridge helpers for flow-update and result-hint control messages.
- [x] Add native bridge / managed bridge support for a background result pump so Unity hosts do not have to block a submit call while waiting for `RESULT_PUSH`.
- [x] Add mixed-submit packet smoke cases using cache-backed objects.
- [x] Add result parsing smoke cases for partial/stale/degraded outputs.
	- [x] Add degraded preview2 smoke coverage on the native bridge submit/result path.
	- [x] Add partial preview2 result smoke coverage on the native bridge path.
	- [x] Add stale-reuse preview2 result smoke coverage on the native bridge path.
- [x] Add typed-payload smoke cases for token chunks, multimodal chunks, and structured event frames.

### 5.3 Unity Package Surface

- [ ] Replace the retired repo-staged Unity package path with a CI-owned Unity-style package definition.
- [ ] Add a deterministic CI step that generates Unity `.meta` files for the package tree instead of relying on committed package outputs.
- [ ] Define one Unity-style client package that contains all current common-platform native bridge binaries in the correct Unity plugin paths.
	- [ ] Include Windows native bridge binaries in the Windows plugin path.
	- [ ] Include macOS native bridge binaries in the macOS plugin path.
	- [ ] Include Linux native bridge binaries in the Linux plugin path.
	- [ ] Include Android native bridge binaries in the Android plugin path.
	- [ ] Include iOS native bridge binaries in the iOS plugin path.
- [ ] Keep NuGet-style server/client packages separate from the Unity-style client package.
- [ ] Keep the current compatibility scope limited to Windows, macOS, Linux, Android, and iOS.

- [x] Rename the staged Unity UPM package path to `unity/com.nnrp.client` and update package metadata, README examples, and staging/repro scripts to match.
- [x] Decide which preview2 object-reference helpers need to be public in the Unity package and which stay internal.
	- [x] Keep `Nnrp.Core` object-reference/body-layout primitives available for advanced callers, but do not promote them as the primary Unity gameplay-facing API.
	- [x] Treat `NnrpPreviewClient` and `NnrpAutoTransportPreviewClient` as the recommended Unity-facing preview2 entry points.
- [x] Expose a host-facing preview2 session API shaped around `submit pump + result pump + flow update`, not just per-frame `SubmitAsync`.
- [x] Add Unity-facing guidance for stale/drop/degraded handling under multi-frame in-flight conditions.
- [x] Stage updated managed/native preview2 assemblies into `unity/com.nnrp.client` or a dedicated preview2 package path once packaging policy is fixed.
	- [x] Keep `unity/com.nnrp.client` as the single staged preview package path and refresh it in place with `./scripts/stage_unity_package.ps1`.

### 5.4 Transport Probing And Migration

- [x] Add a real TCP `INnrpMessageTransport` baseline in `Nnrp.Transport.Tcp` so preview2 probing and migration work has a non-QUIC adapter to build on.
- [x] Add TCP loopback smoke coverage for the current `CLIENT_HELLO -> FRAME_SUBMIT -> RESULT_PUSH -> PING/PONG -> CLOSE` path as a pre-probe transport baseline.
- [x] Implement `TRANSPORT_PROBE` metadata encode and `TRANSPORT_PROBE_ACK` metadata decode.
- [x] Add client-side probe orchestration: send multi-sample probes concurrently on QUIC and TCP bindings, select the default path for `CLIENT_HELLO` using the preview2 scoring order.
	- [x] Treat transient jitter as a scoring concern rather than a caller policy concern: do not pick a binding from a single lucky sample.
	- [x] Keep the default ranking order aligned with the preview2 design doc: compare `success_count`, then `median_throughput`, then `median_rtt_us`.
	- [x] Allow an implementation warm-up probe, but exclude warm-up samples from the scored sample set.
	- [x] Expose the selected binding plus per-binding score summary so Unity/C# hosts can log why QUIC or TCP won.
- [x] Add local dial policy helper to skip probing when the caller already knows or wants to force the preferred binding.
- [x] Add a client-facing auto-probe bootstrap helper that can probe QUIC/TCP and immediately connect the selected control path.
- [x] Freeze `transport_id:u32` values in protocol doc: `0=unspecified`, `1=quic`, `2=tcp`.
- [x] Mirror the selected or forced binding into `ClientHello.transport_policy` / `preferred_transport_id` and validate `ServerHelloAck.active_transport_id`.
- [x] Implement `SESSION_MIGRATE` metadata encode and `SESSION_MIGRATE_ACK` metadata decode.
- [x] Add client-side migration trigger: RTT/jitter/throughput monitoring; fire migration when threshold is crossed.
- [x] Enforce `frame_id` monotonicity across migration and honor `resume_from_frame_id`.
- [x] Add native bridge support for transport path switching without tearing down the session.
- [x] Add unit tests for probe and migration metadata round-trips.
- [x] Add unit tests that freeze multi-sample probe scoring outcomes, including tie-breaks and failure penalties.
- [x] Add loopback smoke tests for probing and migration on both QUIC and TCP bindings.
	- [x] Add TCP auto-probe loopback smoke coverage for `TRANSPORT_PROBE -> CLIENT_HELLO -> FRAME_SUBMIT -> RESULT_PUSH -> PING/PONG -> CLOSE`.
	- [x] Add native runtime auto-probe smoke coverage that exercises QUIC/TCP probe summaries against the split preview/gateway ports.
	- [x] Extend the native runtime auto-probe smoke from `PING/PONG + FRAME_CANCEL + probe summaries` to `FRAME_SUBMIT -> RESULT_PUSH` once `neural-render-runtime` preview bridge decodes preview2 `FrameSubmitMetadata`.
	- [x] Add migration loopback coverage and QUIC-backed probe/migration smoke paths; the remaining gap is transport-switching coverage beyond the current TCP `SESSION_MIGRATE -> SESSION_MIGRATE_ACK` control-plane loopback.

## 6. Validation

- [x] As of 2026-05-09, keep `dotnet test Nnrp.sln` green after the TCP transport baseline slice and enforce a 90% line-coverage gate for `Nnrp.Transport.Tcp`.
- [x] Add unit tests for preview2 metadata round-trips in `Nnrp.Core.Tests`.
- [x] Add unit tests for `BodyRegionPrelude`, object-reference block parsing, mixed submit, and partial result body parsing.
	- [x] Add `BodyRegionPrelude` round-trip and strict reserved-field parsing coverage.
	- [x] Add `ObjectReferenceBlock` round-trip and strict reference-flag parsing coverage.
	- [x] Add mixed-submit body coverage.
	- [x] Add partial result body parsing coverage beyond metadata/coverage-contract validation.
	- [x] Add targeted client/server loopback coverage for the current preview2 tensor-inline submit/result path inside `nnrp-cs`.
- [x] Add unit tests for typed payload descriptor parsing, extension-frame skipping, critical-frame rejection, and descriptor-length invariants.
	- [x] Add strict parse coverage for `TypedPayloadDescriptor` and `ExtensionFrameDescriptor`.
	- [x] Add descriptor-region length/alignment invariant coverage for typed-payload and extension regions.
	- [x] Add fast-skip coverage for unknown non-critical extension frames.
	- [x] Add stable protocol-error coverage for unknown critical extension frames.
- [x] Add native smoke coverage for preview2 open/submit/result/close flows.
- Process guardrail: keep `dotnet test Nnrp.sln` green after each preview2 slice.

## 7. Documentation

- [x] Document preview2 wire differences relative to preview1 in the repository README or dedicated preview2 notes.
- [x] Document the object-reference lifecycle expected by Unity hosts.
- [x] Document NNRP as a lightweight real-time AI application protocol rather than a neural-rendering-only transport.
- [x] Document preview2 host-integration guidance for async submit/result pumps and clarify that one-shot `submit_and_wait` helpers are non-canonical convenience APIs.
- [x] Document typed-payload / extension-frame usage for token streaming, multimodal dialogue, and coding-agent events.
- [x] Document native smoke and staging workflow once preview2 package layout is decided.
- [ ] Document the CI-generated Unity package format, including `.meta` generation policy and multi-platform native plugin layout.
- [ ] Document the common-platform package matrix and plugin layout for Windows, macOS, Linux, Android, and iOS.
