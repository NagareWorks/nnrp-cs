# C# Preview3 Managed Host Surface

- [ ] Replace managed preview3 wire/session entry points with Rust-backed native bridge entry points.
- [ ] Keep any remaining managed codec logic limited to migration-time diagnostics while the preview3 Rust-backed path replaces the prior managed preview surface.
- [ ] Add host-facing submit/result/control helpers that compose native preview3 handles rather than rebuilding packet logic in C#.
- [ ] Preserve distinctions among `partial`, `degraded`, `stale_reuse`, `cancelled`, `failed`, and `completed` on host-facing APIs.