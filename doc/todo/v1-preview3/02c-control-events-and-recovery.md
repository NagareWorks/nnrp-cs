# C# Preview3 Control Events And Recovery

- [ ] Expose `FLOW_UPDATE`, `RESULT_HINT`, and result/event pump behavior through one consistent preview3 event model.
- [ ] Keep background result/event pumps aligned with native Rust semantics rather than inventing a second managed session pump contract.
- [ ] Add resume/recovery helpers only after the recovery object boundary is frozen upstream.
- [ ] Keep recovery tokens and resume windows as opaque native-core-owned data on the managed surface.