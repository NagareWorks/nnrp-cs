# C# Preview3 Package And Host Integration

- [x] Adapt polling, callback, or event-queue delivery primitives for Unity and plain .NET hosts.
- [x] Choose the default managed delivery model for preview3: event queue, callback registration, or async stream.
- [x] Keep backend selection behind `Nnrp.NativeBridge` so tests can run against managed fixtures and native artifacts.
- [ ] Avoid per-frame managed allocation on the hot submit/result path; use spans, pooled buffers, or safe borrowed views where the ABI allows it.
- [x] Define cancellation and disposal behavior when a managed task is cancelled while a native operation is active.
- [ ] Define CI-owned package layouts for NuGet-style server, NuGet-style client, and Unity-style client distribution.
  - [x] Define deterministic NuGet native runtime asset paths for the native bridge package.
  - [ ] Split CI package outputs into NuGet server, NuGet client, and Unity client artifacts.
- [x] Map every supported native artifact to a deterministic NuGet `runtimes/<rid>/native` path.
- [ ] Map every supported native artifact to deterministic Unity plugin importer settings.
- [ ] Implement a deterministic package-generation step that emits Unity `.meta` files for folders, managed assemblies, and native plugin entries.
- [ ] Build native bridge artifacts in a multi-platform CI matrix for Windows, macOS, Linux, Android, and iOS.
- [ ] Assemble one Unity-style client package artifact that places all supported common-platform native binaries into the correct Unity plugin directories.
- [ ] Decide which preview3 handles stay internal and which become public Unity/.NET abstractions.
- [ ] Document Unity callback dispatch and threading rules for preview3 result/event pumps.
- [ ] Add Unity-facing guidance for multi-session orchestration, cache lease behavior, and operation cancellation semantics.
- [x] Run the pre-migration benchmark suite and record the baseline in `doc/benchmarks/rs-native-artifacts-migration.md`.
- [ ] Run the same benchmark suite after native migration and record the deltas in `doc/benchmarks/rs-native-artifacts-migration.md`.
