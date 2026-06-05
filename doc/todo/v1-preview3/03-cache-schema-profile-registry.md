# C# Preview3 Cache, Schema, And Profile Registry

## Cache Lease Surface

- [x] Add managed host models for cache lease, object version, expiry, renewal, and dependency invalidation backed by native results.
- [x] Add Unity/.NET-friendly cache query, touch, prefetch, and release helpers without re-implementing cache semantics in managed code.
- [ ] Preserve native-core ownership of lease policy and dependency validation.
  - [x] Keep host APIs shaped as managed wrappers over native-backed cache query/touch/prefetch/release results.
  - [ ] Route lease policy evaluation and dependency validation through native bridge operations once the bridge exposes those operations.

## Schema And Registry Surface

- [x] Add managed wrappers for schema/profile installation, lookup, invalidation, and version mismatch handling.
- [x] Model schema descriptor common headers and typed payload descriptor views against the frozen 32B / 24B layouts plus the first-round standard registry assignments from `nnrp-doc`.
  - [x] Align typed payload descriptor parsing, writing, and conformance coverage with the frozen 24B layout and token schema anchor.
- [ ] Keep schema/profile interpretation native-core-owned; managed code should expose descriptors and safe wrappers only.
  - [x] Expose schema descriptors, typed payload descriptor views, and registry wrappers without embedding profile-local bodies in fixed protocol metadata.
  - [ ] Move remaining schema/profile interpretation decisions behind native-owned registry/policy helpers when the bridge exposes them.

## Standard Profiles

- [x] Treat `tensor` and `token` as peer first-round standard profiles on the public C# surface.
- [x] Treat `profile_id = 0` as `unspecified` on the public C# surface rather than an implicit tensor default.
- [x] Add host-facing token-profile wrappers against the frozen token minimum semantics and first-round registry assignments from `nnrp-doc`.
- [x] Do not keep preview3 public APIs tensor-privileged once the Rust/profile contract is frozen.
  - [x] Keep `tensor`, `token`, and `unspecified` visible as peer public profile identities.
  - [x] Audit client/server profile defaults and helper names that still imply tensor-only behavior before final surface freeze.

## Payload Family Boundaries

- [x] Surface `structured_event` and `tool_delta` as protocol-visible payload families without hard-coding their bodies into managed fixed metadata models.
- [ ] Keep profile-local payload interpretation outside C# public protocol enums unless promoted by the protocol doc.
  - [x] Keep `structured_event` and `tool_delta` exposed as payload-family identifiers rather than fixed C# metadata bodies.
  - [ ] Audit remaining public enums and documentation for profile-local body semantics before final surface freeze.
