# C# Preview3 Managed Host Surface

- [ ] Replace managed preview3 wire/session entry points with Rust-backed native bridge entry points.
  - [x] Add Rust-backed connection/session/submit/result/control facades.
  - [ ] Redirect existing public preview helper call sites to the Rust-backed facades.
    - [ ] Redirect client connection/session bootstrap helpers.
    - [ ] Redirect submit/result helper paths.
    - [ ] Redirect cancellation/control helper paths.
    - [x] Redirect adapter conformance execution from placeholder reports to native-backed smoke case execution.
  - [ ] Remove or quarantine superseded managed hot-path wire/session implementations.
    - [ ] Keep managed codecs only for fixture inspection, diagnostics, and unsupported runtime combinations.
    - [ ] Add explicit fallback selection so native-backed paths are the default when artifacts are present.
- [ ] Keep any remaining managed codec logic limited to migration-time diagnostics while the preview3 Rust-backed path replaces the prior managed preview surface.
  - [x] Keep native bridge backend selection separate from managed fixture/test paths.
  - [ ] Audit preview3 runtime call sites and move remaining managed codec helpers behind fixture/diagnostic paths.
- [x] Add host-facing submit/result/control helpers that compose native preview3 handles rather than rebuilding packet logic in C#.
- [x] Preserve distinctions among `partial`, `degraded`, `stale_reuse`, `cancelled`, `failed`, and `completed` on host-facing APIs.
