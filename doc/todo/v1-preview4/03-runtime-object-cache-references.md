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
- [x] Bind native object delta helpers.
- [x] Bind native-owned metadata buffer release.
- [x] Add SafeHandle wrappers for native-owned object metadata.
- [x] Add disposal and use-after-close tests.

## Cache Reference Types

- [x] Add cache reference record.
- [x] Add cache miss record.
- [x] Add cache invalidate record.
- [x] Add cache lease metadata.
- [x] Add cache policy options.
- [x] Keep cache reference behavior explicit per request/profile.
- [x] Add tests for typed cache miss diagnostics.

## Canonical Cache Identity Adoption

- [ ] Replace `NnrpCacheStore` dictionary keys with `NnrpCacheObjectId`.
  - [ ] Change `TryGet`, `TryPut`, and `TryInvalidate` to accept `NnrpCacheObjectId`.
  - [ ] Change cache TTL inputs and stored expiry metadata to milliseconds.
  - [ ] Preserve `ObjectKind` throughout lookup, replacement, eviction, and miss results.
- [ ] Replace `NnrpCacheEntry.Key` and cache result identities with `NnrpCacheObjectId`.
  - [ ] Ensure a miss reports the complete namespace, key words, and object kind.
  - [ ] Ensure equal key words with different object kinds remain distinct entries.
- [ ] Update `NnrpServerSession` cache put and invalidation paths to use `NnrpCacheObjectId`.
  - [ ] Build the identity from `CachePutMetadata` without narrowing fields.
  - [ ] Apply invalidate scope and object-kind semantics without converting to a legacy key.
- [ ] Update referenced-result resolution to use `NnrpCacheObjectId`.
  - [ ] Include the referenced object kind when resolving cached tile indexes and section tables.
  - [ ] Reject kind mismatches instead of accepting an entry with matching key words.
- [ ] Remove `NnrpCacheKey` from the Preview4 public and internal model.
  - [ ] Remove constructors, conversion helpers, and documentation for the narrower key.
  - [ ] Add an API architecture test that rejects a reintroduced `NnrpCacheKey` symbol.
- [ ] Replace legacy cache-key tests with canonical identity coverage.
  - [ ] Cover store, server session, referenced results, expiry, capacity, and invalidation.
  - [ ] Cover namespace, high/low key words, and object-kind collision cases.

## Copy And Lifetime Rules

- [x] Expose result/event payload snapshots as `ReadOnlyMemory<byte>`.
- [x] Expose borrowed native views only behind safe lifetime guards.
- [x] Keep Unity-safe copied snapshot behavior explicit.
- [x] Document copy behavior for runtime objects and partial results.
- [x] Add benchmarks for copied snapshots and borrowed views.
