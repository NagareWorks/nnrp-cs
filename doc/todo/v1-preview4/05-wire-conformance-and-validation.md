# 05 - Wire Conformance And Validation

## Target Manifest Generation

- [ ] Add managed command to emit wire target manifests.
- [ ] Include implementation name.
- [ ] Include protocol version.
- [ ] Include suite version.
- [ ] Include supported modes.
  - [ ] Suite as client.
  - [ ] Suite as server.
  - [ ] Suite as proxy where harness support exists.
- [ ] Include supported transports.
  - [ ] TCP.
  - [ ] QUIC.
  - [ ] IPC.
  - [ ] WebSocket.
- [ ] Include capabilities and limits.

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

- [ ] Reject target manifests that declare unsupported transports.
- [ ] Reject missing expected frames.
- [ ] Reject terminal state mismatches.
- [ ] Reject duplicate scenario result IDs.
- [ ] Preserve skipped outcomes for unavailable optional transports.
