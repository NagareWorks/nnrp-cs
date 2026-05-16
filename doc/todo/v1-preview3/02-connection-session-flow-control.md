# C# Preview3 Connection, Session, And Flow Control

## Connection And Session Surface

- [ ] Add connection bootstrap helpers distinct from session-open helpers.
- [ ] Add explicit `SessionOpen` / `SessionOpenAck` managed surface once upstream fixed metadata is frozen.
- [ ] Add multi-session routing support so one connection can host multiple live sessions without Unity hosts building private registries.
- [ ] Add explicit session-close helpers separate from connection shutdown.
- [ ] Replace preview2 single-session helpers with the preview3 connection/session model in place.

## Scheduling And Credits

- [ ] Add managed wrappers for session priority class and operation-scoped scheduling hints.
- [ ] Add managed models for operation lifecycle state and cancel scope using upstream frozen enums.
- [ ] Surface connection/session/operation credit updates without redefining scheduler semantics in C#.
- [ ] Surface downgrade and retry reasons as managed diagnostics instead of ad hoc string parsing.

## Control Events

- [ ] Expose `FLOW_UPDATE`, `RESULT_HINT`, and result/event pump behavior through one consistent preview3 event model.
- [ ] Keep background result/event pumps aligned with native Rust semantics rather than inventing a second managed session pump contract.
- [ ] Add observability hooks for session routing, priority downgrade, and backpressure transitions.

## Recovery

- [ ] Add resume/recovery helpers only after the recovery object boundary is frozen upstream.
- [ ] Keep recovery tokens and resume windows as opaque native-core-owned data on the managed surface.