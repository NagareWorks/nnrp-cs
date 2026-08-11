# 07 - SDK Contract V15 Role Events

## Machine Contract Gate

- [x] Validate the frozen SDK API contract version and C# language projection in CI.
- [x] Validate the client event, server event, and server operation role-method mappings.
- [x] Reject public session-owned progress, partial-result, and result-drop reply methods.

## Client Event Pump

- [x] Return the closed `NnrpClientEvent` union from `NextEventAsync`.
- [x] Preserve runtime and headerless operation-lifecycle events in receive order.
- [x] Reject malformed lifecycle payloads instead of fabricating runtime headers.
- [x] Cover both event variants and callback validation with managed tests.

## Submit Wait Cancellation

- [x] Emit no submit or cancellation frame when the caller token is already cancelled.
- [x] Send `CANCEL` after an already-dispatched submit wait is cancelled.
- [x] Share the sender-wide monotonic control sequence with explicit control methods.
- [x] Keep the resulting operation-lifecycle event observable through `NextEventAsync`.
- [x] Cover pre-dispatch cancellation, post-dispatch cancellation, and lifecycle visibility.

## Server Event Pump

- [x] Return the closed `NnrpServerEvent` union from `NextEventAsync`.
- [x] Convert every `FRAME_SUBMIT` into an operation-owned submit event.
- [x] Preserve runtime and headerless operation-lifecycle events in receive order.
- [x] Keep `ReceiveSubmitAsync` selective without discarding skipped events.
- [x] Cover submit, runtime, lifecycle, ordering, and serialized consumption with managed tests.

## Operation-Owned Replies

- [x] Move progress and partial-result replies onto `NnrpServerOperation`.
- [x] Send operation-scoped frames through the native operation handle and submit frame identity.
- [x] Reject reply metadata whose operation ID differs from the accepted operation.
- [x] Reject non-terminal replies after a terminal reply has been accepted.
- [x] Remove session-owned progress, partial-result, and result-drop reply paths.
- [x] Cover operation-handle ownership, duplicate terminal replies, and metadata mismatch behavior.

## Validation

- [x] Update compiled public API assertions and exact-shape reflection tests.
- [x] Run formatting, build, managed tests, native E2E, wire E2E, package validation, and conformance gates.
- [x] Keep the Unity import validation as the only release workstream item requiring manual environment evidence.
