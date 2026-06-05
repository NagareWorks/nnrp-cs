# C# Preview3 Validation And Docs

## Validation

- [x] Freeze the preview3 adapter command contract as a C#-owned wrapper over the suite-owned plan/result JSON: `dotnet run --project tools/Nnrp.ConformanceAdapter/Nnrp.ConformanceAdapter.csproj -- --plan <path> --output <path>`.
- [x] Freeze that `nnrp-conformance` owns only the execution-plan/result JSON and selected-case semantics; `nnrp-cs` owns the project path, extra flags, native bridge bootstrap, and Unity/.NET host plumbing around the adapter wrapper.
- [x] Add the initial `tools/Nnrp.ConformanceAdapter/Nnrp.ConformanceAdapter.csproj` wrapper so it can read the suite-owned execution plan and emit a schema-valid case-result report.
- [x] Implement SDK-local adapter smoke execution inside `tools/Nnrp.ConformanceAdapter/Nnrp.ConformanceAdapter.csproj` so selected core cases stop returning placeholder results.
- [x] Extend adapter execution from SDK-local smoke coverage to full suite-selected case behavior.
  - [x] Accept the full suite execution-plan shape, including `suite_version`, `implementation_name`, artifact paths, and selected-case metadata.
  - [x] Validate selected case metadata before execution so malformed suite plans fail clearly instead of silently degrading to local smoke behavior.
  - [x] Allow `artifacts.results_path` and `artifacts.evidence_dir` to drive adapter output paths when the suite invokes the adapter without an explicit `--output`.
- [x] Keep conformance integration adapter-first: C# declares capabilities and executes suite-owned plans rather than maintaining an SDK vector exporter.
- [x] Add native smoke coverage for multiple preview3 sessions on one live connection facade.
- [x] Add native smoke coverage for routed multi-session preview3 result delivery on one live connection.
- [ ] Add validation for cache lease expiry, schema mismatch, operation cancellation, priority-aware flow updates, and resume behavior.
  - [x] Add native bridge unit coverage for resume, recovery validators, schema descriptor helpers, and buffer acquire/view/release routing.
  - [x] Add integration coverage for resume behavior against a live native artifact.
- [x] Keep `dotnet test Nnrp.sln` green while preview3 bindings are staged.
- [x] Add allocation-focused smoke checks so managed preview3 hot paths do not silently copy payloads by default.
  - [x] Add smoke coverage that native event/result payload snapshots can be inspected through read-only memory/span views.
  - [x] Add allocation-focused hot-path coverage for submit/result loops once borrowed or pooled submit payload lifetimes are explicit.

## Documentation And Rollout

- [x] Document the C# SDK as a Rust-backed preview3 binding plus Unity/.NET host integration layer.
  - [x] Document that `Nnrp.NativeBridge` is the preview3 native-backed host facade and that managed fallback is diagnostic or unsupported-runtime only.
  - [x] Document native-backed server/session usage through `Nnrp.NativeBridge`.
- [ ] Keep `doc/benchmarks/rs-native-artifacts-migration.md` updated with the native artifact plan, supported platform matrix, and pre/post migration benchmark results.
- [x] Document the current connection/session model and how it replaces the earlier preview-era assumptions.
- [x] Document cache lease, schema registry, profile neutrality, and operation/workflow lifecycle semantics for hosts.
  - [x] Document host-facing native schema registry and cache lease usage through `Nnrp.NativeBridge`.
  - [x] Document profile neutrality and operation/workflow lifecycle semantics for hosts.
- [x] Document how the Rust-backed APIs replace the prior helper surface within `NNRP/1`, without reintroducing parallel helper families.
- [x] Document the CI-first package strategy so reviewers reject repo-staged DLL and Unity-package regressions.
- [x] Document the Unity `.meta` generation policy and the required plugin directory layout for a single multi-platform Unity package.
- [x] Document the supported common-platform scope as Windows, macOS, Linux, Android, and iOS.
