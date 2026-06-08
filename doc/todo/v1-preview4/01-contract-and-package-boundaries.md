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

- [ ] Pin preview4 Rust artifact version in build metadata.
- [ ] Probe protocol version.
- [ ] Probe ABI version.
- [ ] Probe enabled transport slots.
- [ ] Probe runtime-control support.
- [ ] Probe runtime-object support.
- [ ] Reject mismatched artifacts with deterministic managed diagnostics.

## API Surface Policy

- [ ] Preserve preview3 entrypoints where behavior remains identical.
- [ ] Add preview4 options and types for new behavior.
- [ ] Remove preview-era managed hot-path implementations from default runtime routes.
- [ ] Keep managed-only packet builders under diagnostics or tests.
- [ ] Document explicit native artifact requirements for preview4 runtime behavior.

## Ownership Split

- [ ] NativeBridge owns loading and probing.
- [ ] Transport packages own provider registration.
- [ ] Client/server packages own role-specific orchestration.
- [ ] Unity package owns Unity plugin metadata and platform import layout.
