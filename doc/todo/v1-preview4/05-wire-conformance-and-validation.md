# 05 - Wire Conformance And Validation

## Target Manifest Generation

- [x] Add managed command to emit wire target manifests.
- [x] Include implementation name.
- [x] Include protocol version.
- [x] Include suite version.
- [ ] Include supported modes.
  - [x] Suite as client.
  - [x] Suite as server.
  - [ ] Suite as proxy where harness support exists.
- [ ] Include supported transports.
  - [x] TCP.
  - [x] QUIC.
  - [ ] IPC.
  - [ ] WebSocket.
- [x] Include capabilities and limits.

## Harness

- [ ] Add suite-as-client live endpoint harness.
- [ ] Add suite-as-server live endpoint harness.
- [ ] Add proxy harness where transport support exists.
- [ ] Record observed frames.
- [ ] Record terminal state.
- [ ] Record timing evidence.
- [ ] Write wire case results JSON.

## CI Validation

- [ ] Run adapter conformance separately.
- [ ] Run wire conformance dry-run separately.
- [ ] Run wire live endpoint tests against preview4 artifacts.
- [ ] Validate result reports through `nnrp-conformance-runner`.
- [ ] Upload evidence artifacts.

## Negative Coverage

- [x] Reject target manifests that declare unsupported transports.
- [ ] Reject missing expected frames.
- [ ] Reject terminal state mismatches.
- [ ] Reject duplicate scenario result IDs.
- [ ] Preserve skipped outcomes for unavailable optional transports.
