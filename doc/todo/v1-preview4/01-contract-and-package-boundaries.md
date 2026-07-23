# 01 - Contract And Package Boundaries

## Package Boundary

- [ ] Keep shared managed contracts in the core package.
- [ ] Keep client host APIs in the client package.
- [ ] Keep server host APIs in the server package.
- [ ] Keep Unity client APIs in the Unity package.
- [ ] Keep transport providers in separate transport packages.
  - [ ] TCP.
  - [ ] QUIC.
  - [ ] IPC.
  - [ ] WebSocket.
- [ ] Keep native artifacts scoped to the package that owns the transport.
- [ ] Keep diagnostic fixture helpers outside default runtime paths.

## Rust Artifact Baseline

- [x] Pin Rust artifact `1.0.0-preview.4.15` in build metadata.
- [x] Probe protocol version.
- [x] Probe ABI version.
- [x] Probe enabled transport slots.
- [x] Probe runtime-control support.
- [x] Probe runtime-object support.
- [x] Reject mismatched artifacts with deterministic managed diagnostics.

## API Surface Policy

- [ ] Replace earlier preview entrypoints with the frozen Preview4 names and semantics.
- [ ] Add preview4 options and types for new behavior.
- [ ] Remove preview-era managed hot-path implementations from default runtime routes.
- [ ] Keep managed-only packet builders under diagnostics or tests.
- [ ] Document explicit native artifact requirements for preview4 runtime behavior.

## Capability Token Catalog

- [x] Mirror the Rust preview4 control capability token names exactly.
  - [x] `control.cancel_abort`.
  - [x] `control.supersede`.
  - [x] `control.priority_update`.
  - [x] `control.deadline_expire`.
  - [x] `control.progress_partial`.
  - [x] `control.credit_backpressure`.
  - [x] `control.capability_costs`.
  - [x] `control.route_execution_hint`.
  - [x] `control.trace_context`.
  - [x] `control.result_drop_reason`.
  - [x] `control.degrade_profile`.
  - [x] `control.budget_update`.
  - [x] `control.recoverable_error`.
- [x] Mirror the Rust preview4 runtime-object and cache capability token names exactly.
  - [x] `object.lifecycle`.
  - [x] `object.delta`.
  - [x] `object.cost`.
  - [x] `object.ownership`.
  - [x] `cache.reference`.
- [x] Mirror the Rust preview4 transport names exactly.
  - [x] `tcp`.
  - [x] `quic`.
  - [x] `ipc`.
  - [x] `websocket`.

## Ownership Split

- [ ] NativeBridge owns loading and probing.
- [ ] Transport packages own provider registration.
- [ ] Client/server packages own role-specific orchestration.
- [ ] Unity package owns Unity plugin metadata and platform import layout.
