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

- [ ] Pin Rust artifact `1.0.0-preview.4.13` in build metadata.
- [ ] Probe protocol version.
- [ ] Probe ABI version.
- [ ] Probe enabled transport slots.
- [ ] Probe runtime-control support.
- [ ] Probe runtime-object support.
- [ ] Reject mismatched artifacts with deterministic managed diagnostics.

## API Surface Policy

- [ ] Replace earlier preview entrypoints with the frozen Preview4 names and semantics.
- [ ] Add preview4 options and types for new behavior.
- [ ] Remove preview-era managed hot-path implementations from default runtime routes.
- [ ] Keep managed-only packet builders under diagnostics or tests.
- [ ] Document explicit native artifact requirements for preview4 runtime behavior.

## Capability Token Catalog

- [ ] Mirror the Rust preview4 control capability token names exactly.
  - [ ] `control.cancel_abort`.
  - [ ] `control.supersede`.
  - [ ] `control.priority_update`.
  - [ ] `control.deadline_expire`.
  - [ ] `control.progress_partial`.
  - [ ] `control.credit_backpressure`.
  - [ ] `control.capability_costs`.
  - [ ] `control.route_execution_hint`.
  - [ ] `control.trace_context`.
  - [ ] `control.result_drop_reason`.
  - [ ] `control.degrade_profile`.
  - [ ] `control.budget_update`.
  - [ ] `control.recoverable_error`.
- [ ] Mirror the Rust preview4 runtime-object and cache capability token names exactly.
  - [ ] `object.lifecycle`.
  - [ ] `object.delta`.
  - [ ] `object.cost`.
  - [ ] `object.ownership`.
  - [ ] `cache.reference`.
- [ ] Mirror the Rust preview4 transport names exactly.
  - [ ] `tcp`.
  - [ ] `quic`.
  - [ ] `ipc`.
  - [ ] `websocket`.

## Ownership Split

- [ ] NativeBridge owns loading and probing.
- [ ] Transport packages own provider registration.
- [ ] Client/server packages own role-specific orchestration.
- [ ] Unity package owns Unity plugin metadata and platform import layout.
