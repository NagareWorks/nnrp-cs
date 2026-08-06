# 01 - Contract And Package Boundaries

## Role-First Package Graph

- [x] Complete the role-first package graph.
  - [x] Put shared protocol, endpoint, runtime metadata, and provider-selection contracts in `Nnrp.Core`.
  - [x] Put the production client host and session APIs in `Nnrp.Client`.
  - [x] Put the production server host, accepted session, and operation APIs in `Nnrp.Server`.
  - [x] Put native loading, ABI probing, SafeHandle types, and coarse FFI calls in `Nnrp.NativeBridge`.
  - [x] Put Unity client APIs and plugin metadata in `com.nnrp.client`.
- [x] Complete the transport package graph.
  - [x] Keep TCP behavior and artifacts in `Nnrp.Transport.Tcp`.
  - [x] Keep QUIC behavior and artifacts in `Nnrp.Transport.Quic`.
  - [x] Add `Nnrp.Transport.Ipc` with IPC behavior and artifacts.
  - [x] Add `Nnrp.Transport.WebSocket` with WebSocket behavior and artifacts.
  - [x] Make every transport package depend on `Nnrp.Core` and `Nnrp.NativeBridge`, not on client or server roles.
  - [x] Scope every native artifact to the package that owns that transport.
  - [x] Keep client and server packages free of transport artifacts.
- [x] Enforce the low-level wire/tooling boundary.
  - [x] Keep transport-neutral message structs and packet codecs in `Nnrp.Core` for providers, diagnostics, conformance, and protocol tooling.
  - [x] Move managed loopback transports and fixture-only helpers out of production client/server projects.
  - [x] Prevent production client/server public signatures from accepting managed packet pumps.
  - [x] Add architecture tests that reject managed packet-loop fallbacks from production role paths.

## Rust Artifact Baseline

- [x] Pin the coordinated Rust artifact `1.0.0-preview.4.22` and exact FFI ABI `4.4.0` in build and release metadata.
- [x] Validate the TCP, QUIC, IPC, and WebSocket artifact manifests from `1.0.0-preview.4.22`.
- [x] Probe protocol version.
- [x] Probe ABI version.
  - [x] Require ABI `4.4.0` and bind the persistent server accept ticket, session recovery, asynchronous server policy, and runtime shutdown entrypoints.
  - [x] Remove the legacy one-shot `nnrp_server_accept` binding.
- [x] Probe enabled transport slots.
- [x] Probe runtime-control support.
- [x] Probe runtime-object support.
- [x] Reject mismatched artifacts with deterministic managed diagnostics.

## API Surface Policy

- [x] Replace earlier preview entrypoints with the frozen Preview4 names and semantics.
  - [x] Add `NnrpEndpoint` for application-facing `nnrp://` and `nnrps://` endpoints.
  - [x] Add `NnrpProviderEndpoint` for explicit carrier-local overrides.
  - [x] Add `NnrpTransportClientSecurity` and `NnrpTransportServerSecurity`.
  - [x] Add `NnrpClientProviderRoute` and an immutable `TransportId`-keyed client route dictionary.
  - [x] Add `NnrpServerProviderRoute` and an immutable `TransportId`-keyed server route dictionary.
  - [x] Add `NnrpClientOptions` and `NnrpClientSessionOptions` with the frozen endpoint, route set, policy, and session fields.
  - [x] Add `NnrpClient.ConnectAsync(NnrpClientOptions, CancellationToken)`.
  - [x] Add `NnrpClient.OpenSession(NnrpClientSessionOptions)`.
  - [x] Add `NnrpServerOptions` with the frozen endpoint, route set, policy, and session defaults, plus `NnrpServerAcceptOptions` with only the public timeout field; keep native handles and generations internal.
  - [x] Add `NnrpServer.ListenAsync(NnrpServerOptions, CancellationToken)`.
  - [x] Add `NnrpServer.AcceptAsync(NnrpServerAcceptOptions, CancellationToken)`.
  - [x] Add the frozen connection/session lifecycle projections and production client session, server session, and server operation surfaces.
    - [x] Add `NnrpClientSession` with owned connection/session lifetime and typed runtime operations.
    - [x] Add `NnrpServerSession` with owned accepted-session lifetime and typed control/cache operations.
    - [x] Add `NnrpServerOperation` with owned request values, operation identity, trace context, and terminal-state enforcement.
- [x] Remove Preview1, Preview2, and Preview3 public entrypoints rather than forwarding or aliasing them.
- [x] Remove managed hot-path implementations from default runtime routes.
- [x] Keep low-level message builders available to provider/tooling code but out of production role signatures and default runtime routes.
- [x] Add `RuntimeFrameHeader` as the shared immutable runtime-frame header projection.
- [x] Document the native artifact and installed-provider requirements for every production entrypoint.
- [x] Reject singular production-role provider endpoint and role-wide security options.
- [x] Keep singular endpoint/security values only on low-level one-provider connect/listen options.

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

- [x] Make NativeBridge own loading, ABI probing, native handles, and coarse FFI calls.
- [x] Make transport packages own registration, provider metadata, connect/listen behavior, and native artifacts.
- [x] Make client/server packages own role-specific orchestration without implementing protocol hot paths in C#.
- [x] Make the Unity package own Unity plugin metadata and platform import layout.
- [x] Add architecture tests that reject role-to-role dependencies and transport artifacts outside transport packages.
