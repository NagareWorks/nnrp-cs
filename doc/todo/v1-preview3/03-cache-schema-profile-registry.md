# C# Preview3 Cache, Schema, And Profile Registry

## Cache Lease Surface

- [ ] Add managed host models for cache lease, object version, expiry, renewal, and dependency invalidation backed by native results.
- [ ] Add Unity/.NET-friendly cache query, touch, prefetch, and release helpers without re-implementing cache semantics in managed code.
- [ ] Preserve native-core ownership of lease policy and dependency validation.

## Schema And Registry Surface

- [ ] Add managed wrappers for schema/profile installation, lookup, invalidation, and version mismatch handling.
- [ ] Model schema descriptor common headers and typed payload descriptor views only after 32B / 24B layouts freeze upstream.
- [ ] Keep schema/profile interpretation native-core-owned; managed code should expose descriptors and safe wrappers only.

## Standard Profiles

- [ ] Treat `tensor` and `token` as peer first-round standard profiles on the public C# surface.
- [ ] Add host-facing token-profile wrappers only after token minimum semantics freeze upstream.
- [ ] Do not keep preview3 public APIs tensor-privileged once the Rust/profile contract is frozen.

## Payload Family Boundaries

- [ ] Surface `structured_event` and `tool_delta` as protocol-visible payload families without hard-coding their bodies into managed fixed metadata models.
- [ ] Keep profile-local payload interpretation outside C# public protocol enums unless promoted by the protocol doc.