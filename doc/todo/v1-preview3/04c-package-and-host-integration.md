# C# Preview3 Package And Host Integration

- [ ] Adapt polling, callback, or event-queue delivery primitives for Unity and plain .NET hosts.
- [ ] Define CI-owned package layouts for NuGet-style server, NuGet-style client, and Unity-style client distribution.
- [ ] Implement a deterministic package-generation step that emits Unity `.meta` files for folders, managed assemblies, and native plugin entries.
- [ ] Build native bridge artifacts in a multi-platform CI matrix for Windows, macOS, Linux, Android, and iOS.
- [ ] Assemble one Unity-style client package artifact that places all supported common-platform native binaries into the correct Unity plugin directories.
- [ ] Decide which preview3 handles stay internal and which become public Unity/.NET abstractions.
- [ ] Document Unity callback dispatch and threading rules for preview3 result/event pumps.
- [ ] Add Unity-facing guidance for multi-session orchestration, cache lease behavior, and operation cancellation semantics.