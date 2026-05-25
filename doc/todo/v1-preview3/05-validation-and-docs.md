# C# Preview3 Validation And Docs

## Validation

- [x] Freeze the preview3 adapter command contract as a C#-owned wrapper over the suite-owned plan/result JSON: `dotnet run --project tools/Nnrp.ConformanceAdapter/Nnrp.ConformanceAdapter.csproj -- --plan <path> --output <path>`.
- [x] Freeze that `nnrp-conformance` owns only the execution-plan/result JSON and selected-case semantics; `nnrp-cs` owns the project path, extra flags, native bridge bootstrap, and Unity/.NET host plumbing around the adapter wrapper.
- [x] Add the initial `tools/Nnrp.ConformanceAdapter/Nnrp.ConformanceAdapter.csproj` wrapper so it can read the suite-owned execution plan and emit a schema-valid explicit `not_implemented` case-result report.
- [ ] Implement real preview3 adapter case execution inside `tools/Nnrp.ConformanceAdapter/Nnrp.ConformanceAdapter.csproj` so selected cases stop returning placeholder `not_implemented` results.
- [ ] Add a preview3 conformance exporter and wire it into the suite-owned conformance action against the Rust canonical vectors.
- [ ] Add native smoke coverage for multi-session preview3 flows on one live connection.
- [ ] Add validation for cache lease expiry, schema mismatch, operation cancellation, priority-aware flow updates, and resume behavior.
- [ ] Keep `dotnet test Nnrp.sln` green while preview3 bindings are staged.
- [ ] Add allocation-focused smoke checks so managed preview3 hot paths do not silently copy payloads by default.

## Documentation And Rollout

- [ ] Document the C# SDK as a Rust-backed preview3 binding plus Unity/.NET host integration layer.
- [ ] Keep `doc/rs-native-artifacts-migration.md` updated with the native artifact plan, supported platform matrix, and pre/post migration benchmark results.
- [ ] Document the current connection/session model and how it replaces the earlier preview-era assumptions.
- [ ] Document cache lease, schema registry, profile neutrality, and operation/workflow lifecycle semantics for hosts.
- [ ] Document how the Rust-backed APIs replace the prior helper surface within `NNRP/1`, without reintroducing parallel helper families.
- [ ] Document the CI-first package strategy so reviewers reject repo-staged DLL and Unity-package regressions.
- [ ] Document the Unity `.meta` generation policy and the required plugin directory layout for a single multi-platform Unity package.
- [ ] Document the supported common-platform scope as Windows, macOS, Linux, Android, and iOS.
