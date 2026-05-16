# C# Preview3 Validation And Docs

## Validation

- [ ] Add conformance tests that compare C# preview3 bindings against Rust canonical golden vectors.
- [ ] Add native smoke coverage for multi-session preview3 flows on one live connection.
- [ ] Add validation for cache lease expiry, schema mismatch, operation cancellation, priority-aware flow updates, and resume behavior.
- [ ] Keep `dotnet test Nnrp.sln` green while preview3 bindings are staged.
- [ ] Add allocation-focused smoke checks so managed preview3 hot paths do not silently copy payloads by default.

## Documentation And Rollout

- [ ] Document the C# SDK as a Rust-backed preview3 binding plus Unity/.NET host integration layer.
- [ ] Document the current connection/session model and how it replaces the earlier preview-era assumptions.
- [ ] Document cache lease, schema registry, profile neutrality, and operation/workflow lifecycle semantics for hosts.
- [ ] Document how the Rust-backed APIs replace the prior helper surface within `NNRP/1`, without reintroducing parallel helper families.
- [ ] Document the CI-first package strategy so reviewers reject repo-staged DLL and Unity-package regressions.
- [ ] Document the Unity `.meta` generation policy and the required plugin directory layout for a single multi-platform Unity package.
- [ ] Document the supported common-platform scope as Windows, macOS, Linux, Android, and iOS.