# C# Preview3 Foundation And Contract

## Canonical Ownership And Surface Policy

- [ ] Lock the C# preview3 rollout onto the frozen Rust-owned protocol contract rather than reviving a second managed hot path.
- [ ] Finalize which preview3 surfaces stay as managed convenience models versus native-handle-backed wrappers.
- [ ] Finalize the preview3 public C# surface as the next in-place `NNRP/1` update without retaining superseded preview-era shims.

## FFI Consumption

- [ ] Consume the frozen handle families for connection, session, operation, schema, and buffer views.
- [ ] Implement callback, polling, and event-queue adapters according to the frozen Rust binding contract.
- [ ] Map stable preview3 error families into managed exception/result surfaces without collapsing family/code information.
- [ ] Enforce buffer ownership and bounded-copy rules on the managed side.

## Protocol Contract Adoption

- [ ] Implement `SESSION_OPEN` / `SESSION_OPEN_ACK`, explicit session-close, and recovery semantics exactly as frozen in `nnrp-doc`.
  - [x] Implement fixed `SESSION_OPEN` / `SESSION_OPEN_ACK` metadata and message roundtrip support.
  - [x] Implement fixed `SESSION_CLOSE` / `SESSION_CLOSE_ACK` metadata and message roundtrip support.
  - [ ] Implement recovery semantics exactly as frozen in `nnrp-doc`.
- [ ] Implement session priority classes, operation lifecycle states, cancellation scopes, and `FLOW_UPDATE` semantics from frozen protocol enums and metadata tables.
- [ ] Implement cache lease, schema registry, and typed payload descriptor wrappers against the frozen 32B / 24B layouts and standard error behavior.
- [ ] Consume Rust-generated conformance fixtures as the only canonical preview3 protocol baseline.

## Packaging Strategy

- [ ] Replace repo-staged Unity/DLL distribution assumptions with CI-published package definitions.
- [ ] Split package outputs into NuGet-style server dependency, NuGet-style client dependency, and Unity-style client dependency.
- [ ] Keep GitHub Packages as the first distribution target while leaving room for later NuGet / UPM registry rollout.
- [ ] Add a deterministic CI-owned Unity `.meta` generation step so package trees do not depend on committed Unity editor output.
- [ ] Freeze the current common-platform native package baseline as Windows + macOS + Linux + Android + iOS binaries in one Unity-style client package.
- [ ] Keep the preview3 package scope limited to those common platforms.
