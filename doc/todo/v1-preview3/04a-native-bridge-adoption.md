# C# Preview3 Native Bridge Adoption

- [ ] Consume the frozen Rust-to-C# bridge contract for preview3.
- [x] Pin the exact `nnrp-rs` commit, tag, or artifact version used by the C# package.
- [ ] Replace SDK-owned hot-path wire/session behavior with the canonical `nnrp-rs` native backend.
- [x] Define native artifact names and RID mappings for Windows, macOS, Linux, Android, and iOS.
- [x] Load native artifacts through `Nnrp.NativeBridge` before exposing managed runtime entry points.
- [x] Probe ABI version, protocol version, enabled transport slots, and feature flags before accepting the native artifact.
- [x] Reject ABI/protocol mismatches with deterministic managed exceptions and actionable diagnostic text.
- [ ] Wrap stable native handles for connection, session, operation, schema, and buffer views.
- [x] Wrap the currently frozen Rust FFI value handles for connection, session, operation, event pump, and buffer views.
- [ ] Define ownership and lifetime rules for native buffers exposed as spans, arrays, or safe handles.
- [ ] Ensure callbacks or event-queue entries never outlive the native connection/session handle that owns them.
- [ ] Map stable Rust error codes into managed exception and result surfaces.
- [ ] Keep managed codec helpers limited to fixture inspection, diagnostics, and explicitly unsupported runtime combinations.
- [x] Add loader and probe tests for each supported RID using fake or fixture native artifacts where real artifacts are unavailable.
