# 02 - Managed Runtime Control API

## Client Controls

- [ ] Add cancellation APIs.
  - [ ] Cancel by operation ID.
  - [ ] Abort by operation ID.
  - [ ] Cancellation reason.
  - [ ] Late result suppression.
- [ ] Add scheduling APIs.
  - [ ] Priority update.
  - [ ] Deadline.
  - [ ] Expire-at timestamp.
  - [ ] Supersede operation.
  - [ ] Budget update.
- [ ] Add route and execution hint APIs.
  - [ ] Route hint.
  - [ ] Execution hint.
  - [ ] Preferred profile list.
  - [ ] Degrade profile event.

## Server Controls

- [ ] Add progress event APIs.
  - [ ] Stage.
  - [ ] Percent.
  - [ ] Trace context.
- [ ] Add partial result APIs.
  - [ ] Object reference.
  - [ ] Read-only payload view.
  - [ ] Completion marker.
- [ ] Add result drop APIs.
  - [ ] Drop reason.
  - [ ] Operation ID.
  - [ ] Trace context.
- [ ] Add backpressure APIs.
  - [ ] Credit update.
  - [ ] Max in-flight.
  - [ ] Pressure reason.

## Managed Type Model

- [ ] Add immutable records for control requests.
- [ ] Add immutable records for control events.
- [ ] Add enum mappings for drop reasons and terminal states.
- [ ] Preserve Rust error family/code in managed exceptions.
- [ ] Add XML docs for public control types.

## Tests

- [ ] Add unit tests for cancel/abort mapping.
- [ ] Add unit tests for priority/deadline mapping.
- [ ] Add unit tests for progress/partial result event order.
- [ ] Add unit tests for backpressure credit updates.
- [ ] Add native-backed integration tests against preview4 artifacts.
