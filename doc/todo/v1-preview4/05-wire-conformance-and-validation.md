# 05 - Wire Conformance And Validation

## Target Manifest Generation

- [x] Add managed command to emit wire target manifests.
- [x] Include implementation name.
- [x] Include protocol version.
- [x] Include suite version.
- [x] Declare every implemented runner mode.
  - [x] `suite_as_client`.
  - [x] `suite_as_server`.
  - [x] `suite_as_proxy`.
- [x] Declare every implemented transport.
  - [x] TCP.
  - [x] QUIC.
  - [x] IPC.
  - [x] WebSocket.
- [x] Include capabilities and limits.
- [x] Reject a release target manifest unless it declares all three modes and all four transports.

## Live Target Harness

- [x] Add the `suite_as_client` target harness.
  - [x] Listen for the suite client on TCP.
  - [x] Listen for the suite client on QUIC with declared security material.
  - [x] Listen for the suite client on IPC.
- [x] Add the `suite_as_server` target harness.
  - [x] Connect to the suite server on TCP.
  - [x] Connect to the suite server on WebSocket with declared security material.
- [x] Add the `suite_as_proxy` target harness.
  - [x] Accept the suite proxy request on the declared QUIC upstream endpoint.
  - [x] Observe priority and expiration frames without changing frame order.
  - [x] Close the upstream session deterministically after terminal state.
- [ ] Record terminal state, timing evidence, and typed diagnostics for every selected case.
- [ ] Write case results that validate against `wire-conformance-case-results.schema.json`.

## Frozen Scenario Coverage

- [x] Pass `wire.control.cancel-abort.client` on TCP.
- [x] Pass `wire.control.priority-deadline.proxy` on QUIC.
- [x] Pass `wire.control.progress-backpressure.server` on TCP.
- [x] Pass `wire.control.capability-route-cache.client` on QUIC.
- [x] Pass `wire.control.cancel-abort.ipc-client` on IPC.
- [x] Pass `wire.control.progress-backpressure.websocket-server` on WebSocket.
- [ ] Pass host-route cardinality scenarios.
  - [x] Select one carrier from at least two suite-owned client routes.
  - [x] Reject forced unresolved and security-incompatible routes without fallback.
  - [x] Bind at least two server listeners and accept one session through each.
  - [x] Report every actual bound provider endpoint.
  - [x] Report active transport identity per accepted session.
  - [x] Roll back all listeners after an injected bind failure.
  - [x] Close the logical set after an injected terminal listener failure.
  - [ ] Pass the route-local security matrix for TCP, QUIC, IPC, WS, and WSS.
  - [x] Pass known-but-uninstalled route and exact rejection-precedence cases.

## Negative Coverage

- [x] Reject target manifests that declare unsupported transports.
- [x] Reject target manifests that declare unsupported modes.
- [ ] Reject missing expected frames.
- [ ] Reject unexpected or reordered frames.
- [ ] Reject terminal state mismatches.
- [ ] Reject duplicate scenario result IDs.
- [ ] Reject malformed evidence entries and missing timing evidence.
- [x] Treat a missing required transport artifact as a release failure, not a skipped outcome.

## CI Gates

- [x] Run adapter conformance as an independent job.
- [x] Run wire plan generation and live host-route execution as an independent job.
- [ ] Run the complete three-mode, four-transport live matrix against Preview4 artifacts.
- [x] Validate every host-route result report through `nnrp-conformance-runner`.
- [x] Upload host-route target manifests, execution plans, case results, evidence, and process logs.
- [x] Fail host-route CI when the target process exits early or remains alive after the suite completes.
- [x] Fail host-route CI when any selected case is skipped, missing, or not passed.
