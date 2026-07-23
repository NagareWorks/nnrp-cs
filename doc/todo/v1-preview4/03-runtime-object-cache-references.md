# 03 - Runtime Object And Cache References

## Runtime Object Types

- [x] Add managed runtime object descriptor.
- [x] Add managed runtime object reference.
- [x] Add managed runtime object delta.
- [x] Add managed runtime object release.
- [x] Add object kind enum.
- [x] Add owner and lifetime metadata.
- [x] Add compute and memory cost metadata.

## NativeBridge Object Bindings

- [x] Bind native object descriptor creation.
- [x] Bind native object descriptor parsing.
- [x] Bind native object release.
- [ ] Bind native object delta helpers.
- [x] Bind native-owned metadata buffer release.
- [x] Add SafeHandle wrappers for native-owned object metadata.
- [x] Add disposal and use-after-close tests.

## Cache Reference Types

- [x] Add cache reference record.
- [x] Add cache miss record.
- [x] Add cache invalidate record.
- [x] Add cache lease metadata.
- [ ] Add cache policy options.
- [x] Keep cache reference behavior explicit per request/profile.
- [x] Add tests for typed cache miss diagnostics.

## Copy And Lifetime Rules

- [ ] Expose result/event payload snapshots as `ReadOnlyMemory<byte>`.
- [ ] Expose borrowed native views only behind safe lifetime guards.
- [ ] Keep Unity-safe copied snapshot behavior explicit.
- [ ] Document copy behavior for runtime objects and partial results.
- [ ] Add benchmarks for copied snapshots and borrowed views.
