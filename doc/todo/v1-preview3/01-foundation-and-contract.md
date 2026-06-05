# C# Preview3 Foundation And Contract

## Canonical Ownership And Surface Policy

- [ ] Lock the C# preview3 rollout onto the frozen Rust-owned protocol contract rather than reviving a second managed hot path.
  - [x] Load and probe the pinned Rust native artifact before accepting native-backed preview3 entry points.
  - [x] Route connection/session/submit/result/control helpers through Rust-backed facades when artifacts are present.
  - [ ] Quarantine remaining managed hot-path wire/session helpers behind fixture, diagnostic, or explicitly unsupported-runtime paths.
- [ ] Finalize which preview3 surfaces stay as managed convenience models versus native-handle-backed wrappers.
  - [x] Keep host-facing connection/session/runtime host facades as managed wrappers over native handles.
  - [x] Keep result/event payload snapshots exposed as managed read-only views rather than raw native buffers.
  - [ ] Decide the final public/internal split for schema handles, stable borrowed buffer views, Unity callback handles, and future zero-copy result views.
- [ ] Finalize the preview3 public C# surface as the next in-place `NNRP/1` update without retaining superseded preview-era shims.
  - [x] Redirect preview helper call sites to Rust-backed facades for bootstrap, submit/result, cancel/control, and adapter smoke execution.
  - [ ] Remove or quarantine superseded preview-era helper families once the remaining managed diagnostic paths are isolated.

## FFI Consumption

- [ ] Consume the frozen handle families for connection, session, operation, schema, and buffer views.
  - [x] Wrap connection, session, operation, event pump, and buffer value handles exposed by the frozen Rust FFI.
  - [ ] Wrap schema handles and stable borrowed buffer-view handles once those handles are exposed by the bridge contract.
- [ ] Implement callback, polling, and event-queue adapters according to the frozen Rust binding contract.
  - [x] Adapt native polling/event-queue snapshots for Unity and plain .NET hosts.
  - [x] Choose event queue as the default managed delivery model for preview3.
  - [ ] Add callback-registration lifetime and dispatch rules once native callback subscription handles are exposed.
- [x] Map stable preview3 error families into managed exception/result surfaces without collapsing family/code information.
- [ ] Enforce buffer ownership and bounded-copy rules on the managed side.
  - [x] Snapshot polled native event/result payloads before returning them to callers.
  - [x] Expose native event/result payload snapshots through read-only `ReadOnlyMemory<byte>` / `ReadOnlySpan<byte>` views.
  - [ ] Replace remaining submit/control payload pinning with borrowed or pooled lifetime helpers where the Rust ABI can keep the boundary observable.
  - [ ] Define future zero-copy result/body borrowed-buffer rules.

## Protocol Contract Adoption

- [ ] Implement `SESSION_OPEN` / `SESSION_OPEN_ACK`, explicit session-close, and recovery semantics exactly as frozen in `nnrp-doc`.
  - [x] Implement fixed `SESSION_OPEN` / `SESSION_OPEN_ACK` metadata and message roundtrip support.
  - [x] Implement fixed `SESSION_CLOSE` / `SESSION_CLOSE_ACK` metadata and message roundtrip support.
  - [ ] Implement recovery semantics exactly as frozen in `nnrp-doc`.
- [x] Implement session priority classes, operation lifecycle states, cancellation scopes, and `FLOW_UPDATE` semantics from frozen protocol enums and metadata tables.
- [x] Implement minimum inline tensor `FRAME_SUBMIT` / basic `RESULT_PUSH` conformance surface.
- [ ] Implement cache lease, schema registry, and typed payload descriptor wrappers against the frozen 32B / 24B layouts and standard error behavior.
  - [x] Add cache lease, schema/profile registry, and typed payload descriptor managed wrappers.
  - [x] Align typed payload descriptor parsing, writing, and conformance coverage with the frozen 24B layout and token schema anchor.
  - [ ] Route lease policy, dependency validation, and schema/profile interpretation through native-core-owned helpers once the bridge exposes those operations.
- [ ] Consume Rust-generated conformance fixtures as the only canonical preview3 protocol baseline.

## Packaging Strategy

- [ ] Replace repo-staged Unity/DLL distribution assumptions with CI-published package definitions.
  - [x] Resolve native bridge artifacts for Windows, macOS, Linux, Android, and iOS from the pinned `nnrp-rs` release in CI.
  - [x] Generate deterministic native runtime paths and Unity plugin metadata from CI-owned package steps.
  - [ ] Move final server/client/Unity distribution outputs behind CI-published package definitions.
- [ ] Split package outputs into NuGet-style server dependency, NuGet-style client dependency, and Unity-style client dependency.
- [ ] Keep GitHub Packages as the first distribution target while leaving room for later NuGet / UPM registry rollout.
- [x] Add a deterministic CI-owned Unity `.meta` generation step so package trees do not depend on committed Unity editor output.
- [x] Freeze the current common-platform native package baseline as Windows + macOS + Linux + Android + iOS binaries in one Unity-style client package.
- [x] Keep the preview3 package scope limited to those common platforms.
