# 05 - Wire Conformance And Validation

## Target Manifest Generation

- [x] Add managed command to emit wire target manifests.
- [x] Include implementation name.
- [x] Include protocol version.
- [x] Include suite version.
- [ ] Declare every implemented runner mode.
  - [x] `suite_as_client`.
  - [x] `suite_as_server`.
  - [ ] `suite_as_proxy`.
- [ ] Declare every implemented transport.
  - [x] TCP.
  - [x] QUIC.
  - [ ] IPC.
  - [ ] WebSocket.
- [x] Include capabilities and limits.
- [ ] Reject a release target manifest unless it declares all three modes and all four transports.

## Live Target Harness

- [ ] Add the `suite_as_client` target harness.
  - [ ] Accept the suite-owned TCP endpoint.
  - [ ] Accept the suite-owned QUIC endpoint and security material.
  - [ ] Accept the suite-owned IPC endpoint.
  - [ ] Accept the suite-owned WebSocket endpoint and security material.
- [ ] Add the `suite_as_server` target harness.
  - [ ] Listen on the declared TCP endpoint.
  - [ ] Listen on the declared QUIC endpoint with the declared security material.
  - [ ] Listen on the declared IPC endpoint.
  - [ ] Listen on the declared WebSocket endpoint with the declared security material.
- [ ] Add the `suite_as_proxy` target harness.
  - [ ] Connect the suite-owned front endpoint to the implementation-owned upstream endpoint.
  - [ ] Forward frames without changing byte order or frame boundaries.
  - [ ] Record frames observed in each direction.
  - [ ] Close both legs deterministically after terminal state.
- [ ] Record terminal state, timing evidence, and typed diagnostics for every selected case.
- [ ] Write case results that validate against `wire-conformance-case-results.schema.json`.

## Frozen Scenario Coverage

- [ ] Pass `wire.control.cancel-abort.client` on TCP, QUIC, IPC, and WebSocket.
- [ ] Pass `wire.control.priority-deadline.proxy` on TCP, QUIC, IPC, and WebSocket.
- [ ] Pass `wire.control.progress-backpressure.server` on TCP, QUIC, IPC, and WebSocket.
- [ ] Pass `wire.control.capability-route-cache.client` on TCP, QUIC, IPC, and WebSocket.
- [ ] Pass `wire.control.cancel-abort.ipc-client` on IPC.
- [ ] Pass `wire.control.progress-backpressure.websocket-server` on WebSocket.
- [ ] Pass host-route cardinality scenarios.
  - [ ] Select one carrier from at least two suite-owned client routes.
  - [ ] Reject forced unresolved and security-incompatible routes without fallback.
  - [ ] Bind at least two server listeners and accept one session through each.
  - [ ] Report every actual bound provider endpoint.
  - [ ] Report active transport identity per accepted session.
  - [ ] Roll back all listeners after an injected bind failure.
  - [ ] Close the logical set after an injected terminal listener failure.
  - [ ] Pass the route-local security matrix for TCP, QUIC, IPC, WS, and WSS.
  - [ ] Pass known-but-uninstalled route and exact rejection-precedence cases.

## Negative Coverage

- [x] Reject target manifests that declare unsupported transports.
- [ ] Reject target manifests that declare unsupported modes.
- [ ] Reject missing expected frames.
- [ ] Reject unexpected or reordered frames.
- [ ] Reject terminal state mismatches.
- [ ] Reject duplicate scenario result IDs.
- [ ] Reject malformed evidence entries and missing timing evidence.
- [ ] Treat a missing required transport artifact as a release failure, not a skipped outcome.

## CI Gates

- [ ] Run adapter conformance as an independent job.
- [ ] Run wire plan generation and dry-run as an independent job.
- [ ] Run the complete three-mode, four-transport live matrix against Preview4 artifacts.
- [ ] Validate every result report through `nnrp-conformance-runner`.
- [ ] Upload target manifests, execution plans, case results, frame evidence, and process logs.
- [ ] Fail CI when the target process exits early or remains alive after the suite completes.
- [ ] Fail CI when any selected case is skipped, missing, or not passed.
