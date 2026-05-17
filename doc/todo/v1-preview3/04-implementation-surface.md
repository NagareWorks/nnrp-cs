# C# Preview3 Implementation Surface

## Scope

1. `04` owns how the C# SDK consumes the Rust bridge and packages the resulting host surface.
2. `04` does not own preview3 session semantics, operation-state meaning, or scheduler policy; those are defined in `nnrp-rs/02` and reflected in C# by `02a/02b/02c`.
3. `04` should not block `02` with packaging or Unity-host details unless the bridge contract itself changes.

## Sub-Shards

1. `04a-native-bridge-adoption.md`: Rust bridge contract consumption, handle wrappers, and error surfaces.
2. `04b-managed-host-surface.md`: Rust-backed managed entry points and host-facing submit/result/control helpers.
3. `04c-package-and-host-integration.md`: Unity/.NET dispatch rules, native artifact layout, and package/distribution work.

## Dependency Gates

1. `04a` depends directly on `nnrp-rs/04`; it should not wait on Unity host-policy work.
2. `04b` depends on `04a` plus the semantic decisions frozen in `02a/02b/02c`; it should not redesign callback threading or package layout.
3. `04c` depends on `04a/04b` artifacts and focuses on host integration and distribution only.
4. If a PR changes both `02` and `04`, the change must state explicitly whether it moves a semantic boundary or only adapts bridge wiring.