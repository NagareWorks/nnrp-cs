# C# Preview3 Implementation Surface

## Native Bridge

- [ ] Consume the frozen Rust-to-C# bridge contract for preview3.
- [ ] Wrap stable native handles for connection, session, operation, schema, and buffer views.
- [ ] Map stable Rust error codes into managed exception and result surfaces.
- [ ] Adapt polling, callback, or event-queue delivery primitives for Unity and plain .NET hosts.

## Managed SDK Surface

- [ ] Replace managed preview3 wire/session entry points with Rust-backed native bridge entry points.
- [ ] Keep any remaining managed codec logic limited to migration-time diagnostics while the preview3 Rust-backed path replaces the prior managed preview surface.
- [ ] Add host-facing submit/result/control helpers that compose native preview3 handles rather than rebuilding packet logic in C#.
- [ ] Preserve distinctions among `partial`, `degraded`, `stale_reuse`, `cancelled`, `failed`, and `completed` on host-facing APIs.

## Package And Host Distribution

- [ ] Define CI-owned package layouts for NuGet-style server, NuGet-style client, and Unity-style client distribution.
- [ ] Implement a deterministic package-generation step that emits Unity `.meta` files for folders, managed assemblies, and native plugin entries.
- [ ] Build native bridge artifacts in a multi-platform CI matrix for Windows, macOS, Linux, Android, and iOS.
- [ ] Assemble one Unity-style client package artifact that places all supported common-platform native binaries into the correct Unity plugin directories.
- [ ] Decide which preview3 handles stay internal and which become public Unity/.NET abstractions.
- [ ] Document Unity callback dispatch and threading rules for preview3 result/event pumps.
- [ ] Add Unity-facing guidance for multi-session orchestration, cache lease behavior, and operation cancellation semantics.