# C# Preview3 Control Events And Recovery

- [x] Expose result/event pump behavior through one consistent preview3 event model.
- [x] Expose `FLOW_UPDATE` and `RESULT_HINT` through the same preview3 event model.
- [x] Keep background result/event pumps aligned with native Rust semantics rather than inventing a second managed session pump contract.
- [x] Add resume/recovery helpers through the frozen native bridge entrypoints.
  - [x] Route `client_resume_session` through the native connection facade.
  - [x] Route session recovery request/ack and migration recovery validation through native bridge helpers.
  - [x] Route migration replay decisions through `nnrp_migration_should_replay_frame`.
- [x] Keep recovery tokens and resume windows as opaque native-core-owned data on the managed surface.
