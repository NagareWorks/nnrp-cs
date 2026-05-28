# C# Preview3 Control Events And Recovery

- [x] Expose result/event pump behavior through one consistent preview3 event model.
- [x] Expose `FLOW_UPDATE` and `RESULT_HINT` through the same preview3 event model.
- [x] Keep background result/event pumps aligned with native Rust semantics rather than inventing a second managed session pump contract.
- [ ] Add resume/recovery helpers only after the recovery object boundary is frozen upstream.
- [ ] Keep recovery tokens and resume windows as opaque native-core-owned data on the managed surface.
