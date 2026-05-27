# C# Preview3 Cache, Schema, And Profile Registry

## Cache Lease Surface

- [ ] Add managed host models for cache lease, object version, expiry, renewal, and dependency invalidation backed by native results.
- [ ] Add Unity/.NET-friendly cache query, touch, prefetch, and release helpers without re-implementing cache semantics in managed code.
- [ ] Preserve native-core ownership of lease policy and dependency validation.

## Schema And Registry Surface

- [ ] Add managed wrappers for schema/profile installation, lookup, invalidation, and version mismatch handling.
- [ ] Model schema descriptor common headers and typed payload descriptor views against the frozen 32B / 24B layouts plus the first-round standard registry assignments from `nnrp-doc`.
  - [x] Align typed payload descriptor parsing, writing, and conformance coverage with the frozen 24B layout and token schema anchor.
- [ ] Keep schema/profile interpretation native-core-owned; managed code should expose descriptors and safe wrappers only.

## Standard Profiles

- [ ] Treat `tensor` and `token` as peer first-round standard profiles on the public C# surface.
- [ ] Treat `profile_id = 0` as `unspecified` on the public C# surface rather than an implicit tensor default.
- [ ] Add host-facing token-profile wrappers against the frozen token minimum semantics and first-round registry assignments from `nnrp-doc`.
- [ ] Do not keep preview3 public APIs tensor-privileged once the Rust/profile contract is frozen.

## Payload Family Boundaries

- [ ] Surface `structured_event` and `tool_delta` as protocol-visible payload families without hard-coding their bodies into managed fixed metadata models.
- [ ] Keep profile-local payload interpretation outside C# public protocol enums unless promoted by the protocol doc.
