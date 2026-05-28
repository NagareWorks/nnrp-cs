# C# Preview3 Validation And Docs

## Validation

- [x] Freeze the preview3 adapter command contract as a C#-owned wrapper over the suite-owned plan/result JSON: `dotnet run --project tools/Nnrp.ConformanceAdapter/Nnrp.ConformanceAdapter.csproj -- --plan <path> --output <path>`.
- [x] Freeze that `nnrp-conformance` owns only the execution-plan/result JSON and selected-case semantics; `nnrp-cs` owns the project path, extra flags, native bridge bootstrap, and Unity/.NET host plumbing around the adapter wrapper.
- [x] Add the initial `tools/Nnrp.ConformanceAdapter/Nnrp.ConformanceAdapter.csproj` wrapper so it can read the suite-owned execution plan and emit a schema-valid case-result report.
- [x] Implement SDK-local adapter smoke execution inside `tools/Nnrp.ConformanceAdapter/Nnrp.ConformanceAdapter.csproj` so selected core cases stop returning placeholder results.
- [ ] Extend adapter execution from SDK-local smoke coverage to full suite-selected case behavior.
- [x] Keep conformance integration adapter-first: C# declares capabilities and executes suite-owned plans rather than maintaining an SDK vector exporter.
- [x] Add native smoke coverage for multiple preview3 sessions on one live connection facade.
- [x] Add native smoke coverage for routed multi-session preview3 result delivery on one live connection.
- [ ] Add validation for cache lease expiry, schema mismatch, operation cancellation, priority-aware flow updates, and resume behavior.
- [ ] Keep `dotnet test Nnrp.sln` green while preview3 bindings are staged.
- [ ] Add allocation-focused smoke checks so managed preview3 hot paths do not silently copy payloads by default.

## Documentation And Rollout

- [ ] Document the C# SDK as a Rust-backed preview3 binding plus Unity/.NET host integration layer.
- [ ] Keep `doc/benchmarks/rs-native-artifacts-migration.md` updated with the native artifact plan, supported platform matrix, and pre/post migration benchmark results.
- [ ] Document the current connection/session model and how it replaces the earlier preview-era assumptions.
- [ ] Document cache lease, schema registry, profile neutrality, and operation/workflow lifecycle semantics for hosts.
- [ ] Document how the Rust-backed APIs replace the prior helper surface within `NNRP/1`, without reintroducing parallel helper families.
- [ ] Document the CI-first package strategy so reviewers reject repo-staged DLL and Unity-package regressions.
- [ ] Document the Unity `.meta` generation policy and the required plugin directory layout for a single multi-platform Unity package.
- [ ] Document the supported common-platform scope as Windows, macOS, Linux, Android, and iOS.
