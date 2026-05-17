# NNRP/1-preview3 C# SDK Implementation Todo

## 0. Scope

1. This directory tracks the preview3 C# SDK rollout as a Rust-core-backed binding and Unity/.NET host integration layer.
2. Preview3 work here covers `Nnrp.Core`, `Nnrp.Client`, `Nnrp.NativeBridge`, and package/distribution definitions for CI-published client/server artifacts.
3. Preview3 wire semantics, state machines, cache/schema rules, and conformance baselines are owned by `nnrp-doc` plus `nnrp-rs`, not by this repository.

## 1. Shard Map

1. `01-foundation-and-contract.md`: consume the frozen preview3 contract, public-surface policy, handle/error surfaces, and package strategy.
2. `02-connection-session-flow-control.md`: ownership and dependency map for the `02a/02b/02c` connection/session shards.
3. `02a-connection-session-lifecycle.md`: connection bootstrap, session-open/close, and multi-session host shape.
4. `02b-scheduling-credits-and-diagnostics.md`: priority, lifecycle state, credit updates, downgrade, and diagnostics surfaces.
5. `02c-control-events-and-recovery.md`: `FLOW_UPDATE`/`RESULT_HINT`, result-event pumps, and recovery helpers.
6. `03-cache-schema-profile-registry.md`: cache lease, schema/profile registry, token/tensor public-surface implications.
7. `04-implementation-surface.md`: ownership and dependency map for the `04a/04b/04c` implementation-surface shards.
8. `04a-native-bridge-adoption.md`: Rust bridge contract consumption, handle wrappers, and error-code mapping.
9. `04b-managed-host-surface.md`: managed preview3 host APIs built on top of Rust-backed handles.
10. `04c-package-and-host-integration.md`: Unity/.NET host integration, threading, and package/distribution work.
11. `05-validation-and-docs.md`: conformance, smoke coverage, migration docs, packaging, and release gates.

## 2. PR Rules

1. One shard per PR by default; do not mix foundation, bridge work, and package/distribution changes in the same PR unless the diff is inseparable.
2. `main` should accept reviewed PRs only; no direct push workflow after GitHub publication.
3. If a task needs to change preview3 protocol semantics, update `nnrp-doc` first instead of locally inventing SDK-only behavior.
4. Treat `02a/02b/02c` as semantic-host-surface work and `04a/04b/04c` as bridge/implementation work; do not mix them in one PR unless the boundary truly changes.

## 3. Protocol Coverage Check

1. FFI handle families, callback/polling model, thread affinity, and error families are tracked in `01` and `04`.
2. `SESSION_OPEN` / `SESSION_OPEN_ACK`, explicit session close, multi-session routing, and recovery object semantics are tracked in `01` and `02`.
3. Priority classes, operation states, cancel scope, and `FLOW_UPDATE` 32B semantics are tracked in `01` and `02`.
4. Cache lease/version/dependency rules, schema descriptor 32B, typed payload descriptor 24B, and `descriptor_flags` are tracked in `01` and `03`.
5. `tensor` / `token` first-round standard profiles plus `structured_event` / `tool_delta` ownership boundaries are tracked in `01`, `03`, and `04`.
6. Rust conformance-first enum/message/error baselines and C# binding validation are tracked in `01` and `05`.