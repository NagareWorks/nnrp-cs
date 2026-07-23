# 02 - Managed Runtime Control API

## Client Controls

- [ ] Add cancellation APIs.
  - [x] Cancel by operation ID.
  - [x] Abort by operation ID.
  - [x] Cancellation reason.
  - [ ] Late result suppression.
- [x] Add scheduling APIs.
  - [x] Priority update.
  - [x] Deadline.
  - [x] Expire-at timestamp.
  - [x] Supersede operation.
  - [x] Budget update.
- [x] Add route and execution hint APIs.
  - [x] Route hint.
  - [x] Execution hint.
  - [x] Preferred profile list.
  - [x] Degrade profile event.

## Server Controls

- [x] Add progress event APIs.
  - [x] Stage.
  - [x] Percent.
  - [x] Trace context.
- [x] Add partial result APIs.
  - [x] Object reference.
  - [x] Read-only payload view.
  - [x] Completion marker.
- [x] Add result drop APIs.
  - [x] Drop reason.
  - [x] Operation ID.
  - [x] Trace context.
- [x] Add backpressure APIs.
  - [x] Credit update.
  - [x] Max in-flight.
  - [x] Pressure reason.

## Managed Type Model

- [x] Add immutable records for control requests.
- [x] Add immutable records for control events.
- [ ] Add enum mappings for drop reasons and terminal states.
- [ ] Preserve Rust error family/code in managed exceptions.
- [x] Add XML docs for public control types.

## Tests

- [x] Add unit tests for cancel/abort mapping.
- [x] Add unit tests for priority/deadline mapping.
- [x] Add unit tests for progress/partial result event order.
- [x] Add unit tests for backpressure credit updates.
- [ ] Add native-backed integration tests against preview4 artifacts.
