# C# Preview3 Connection, Session, And Flow Control

## Scope

1. `02` owns the managed semantics and host-visible shape of preview3 connection/session flow control.
2. `02` does not own the Rust ABI, handle layouts, callback/polling primitives, or package layout; those belong to `04` and `nnrp-rs`.
3. `02` depends on `nnrp-rs` shard `02` for frozen state-machine and enum semantics, and on `nnrp-rs` shard `04` only for already-frozen bridge primitives.

## Sub-Shards

1. `02a-connection-session-lifecycle.md`: bootstrap, session-open/close, and multi-session host shape.
2. `02b-scheduling-credits-and-diagnostics.md`: priority, lifecycle state, credit surfaces, and downgrade diagnostics.
3. `02c-control-events-and-recovery.md`: event/result pumps, `FLOW_UPDATE`/`RESULT_HINT`, and recovery helpers.

## Dependency Gates

1. `02a` may start once `nnrp-rs/02` has frozen connection/session metadata and state-machine concepts; it must not wait on Unity packaging or callback-threading details.
2. `02b` may start once `nnrp-rs/02` has frozen priority/lifecycle/credit enums; it must not redesign scheduling semantics in C#.
3. `02c` may start once `nnrp-rs/02` has frozen control-event semantics and `nnrp-rs/04` has exposed stable event-delivery primitives; it does not own Unity/.NET dispatch plumbing.
4. `04b` consumes `02a/02b/02c`; `02` should define host semantics first, while `04` is responsible for wiring them onto the Rust-backed implementation surface.