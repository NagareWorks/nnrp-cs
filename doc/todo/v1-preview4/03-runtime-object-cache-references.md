# 03 - Runtime Object And Cache References

## Runtime Object Types

- [ ] Add managed runtime object descriptor.
- [ ] Add managed runtime object reference.
- [ ] Add managed runtime object delta.
- [ ] Add managed runtime object release.
- [ ] Add object kind enum.
- [ ] Add owner and lifetime metadata.
- [ ] Add compute and memory cost metadata.

## NativeBridge Object Bindings

- [ ] Bind native object descriptor creation.
- [ ] Bind native object descriptor parsing.
- [ ] Bind native object release.
- [ ] Bind native object delta helpers.
- [ ] Bind native-owned metadata buffer release.
- [ ] Add SafeHandle wrappers for native-owned object metadata.
- [ ] Add disposal and use-after-close tests.

## Cache Reference Types

- [ ] Add cache reference record.
- [ ] Add cache miss record.
- [ ] Add cache invalidate record.
- [ ] Add cache lease metadata.
- [ ] Add cache policy options.
- [ ] Keep cache reference behavior explicit per request/profile.
- [ ] Add tests for typed cache miss diagnostics.

## Copy And Lifetime Rules

- [ ] Expose result/event payload snapshots as `ReadOnlyMemory<byte>`.
- [ ] Expose borrowed native views only behind safe lifetime guards.
- [ ] Keep Unity-safe copied fallback behavior.
- [ ] Document copy behavior for runtime objects and partial results.
- [ ] Add benchmarks for copied snapshots and borrowed views.
