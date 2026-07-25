# 02 - Managed Runtime Control API

## Core Metadata And Native Bindings

- [x] Add immutable metadata records for runtime control requests and events.
  - [x] Cancel and abort metadata.
  - [x] Priority, deadline, expire-at, and supersede metadata.
  - [x] Compute, memory, bandwidth, and token budget metadata.
  - [x] Progress and partial-result metadata.
  - [x] Backpressure and credit metadata.
  - [x] Capability cost and limit metadata.
  - [x] Route, execution, and trace metadata.
  - [x] Result-drop, recoverable-error, and retry metadata.
- [x] Bind the coarse Rust runtime-control entrypoints in `Nnrp.NativeBridge`.
  - [x] Client cancellation, scheduling, budget, capability, route, execution, and trace sends.
  - [x] Server progress, partial-result, pressure, drop, trace, recovery, and retry sends.
  - [x] Runtime event receive and owned snapshot conversion.
- [x] Add unit tests for metadata encoding and native-entrypoint routing.

## Client Role Controls

- [ ] Add typed runtime-control methods to the production `NnrpClientSession`.
  - [ ] `CancelAsync(ControlRequestMetadata, ReadOnlyMemory<byte>, CancellationToken)`.
  - [ ] `AbortAsync(ControlRequestMetadata, ReadOnlyMemory<byte>, CancellationToken)`.
  - [ ] `UpdatePriorityAsync(SchedulingMetadata, CancellationToken)`.
  - [ ] `UpdateDeadlineAsync(SchedulingMetadata, CancellationToken)`.
  - [ ] `ExpireAtAsync(SchedulingMetadata, CancellationToken)`.
  - [ ] `SupersedeAsync(SupersedeMetadata, ReadOnlyMemory<byte>, CancellationToken)`.
  - [ ] `UpdateBudgetAsync(BudgetMetadata, CancellationToken)`.
  - [ ] `NegotiateCapabilitiesAsync(CapabilityMetadata, ReadOnlyMemory<byte>, CancellationToken)`.
  - [ ] `DegradeProfileAsync(CapabilityMetadata, ReadOnlyMemory<byte>, CancellationToken)`.
  - [ ] `SendRouteHintAsync(RouteHintMetadata, ReadOnlyMemory<byte>, CancellationToken)`.
  - [ ] `SendExecutionHintAsync(RouteHintMetadata, ReadOnlyMemory<byte>, CancellationToken)`.
  - [ ] `SendTraceContextAsync(TraceContextMetadata, ReadOnlyMemory<byte>, CancellationToken)`.
  - [ ] `SendControlAsync(MessageType, IRuntimeControlMetadata, ReadOnlyMemory<byte>, CancellationToken)` with typed message/metadata validation.
- [ ] Add production result and event iteration.
  - [ ] `NextResultAsync(CancellationToken)`.
  - [ ] `NextEventAsync(CancellationToken)` returning `NnrpRuntimeEvent` in wire order.
  - [ ] Suppress late `RESULT_PUSH` after cancel or abort reaches terminal state.
  - [ ] Suppress late `PARTIAL_RESULT` after cancel or abort reaches terminal state.
  - [ ] Keep `RESULT_DROP_REASON` observable after late-result suppression.

## Server Role Controls

- [ ] Add typed runtime-control sends to the production `NnrpServerSession` and `NnrpServerOperation`.
  - [ ] `SendProgressAsync(ProgressMetadata, ReadOnlyMemory<byte>, CancellationToken)`.
  - [ ] `SendPartialResultAsync(PartialResultMetadata, ReadOnlyMemory<byte>, CancellationToken)`.
  - [ ] `SendBackpressureAsync(PressureMetadata, CancellationToken)`.
  - [ ] `SendCreditUpdateAsync(PressureMetadata, CancellationToken)`.
  - [ ] `SendResultDropReasonAsync(ResultDropReasonMetadata, ReadOnlyMemory<byte>, CancellationToken)`.
  - [ ] `SendTraceContextAsync(TraceContextMetadata, ReadOnlyMemory<byte>, CancellationToken)`.
  - [ ] `SendRecoverableErrorAsync(RecoverableErrorMetadata, ReadOnlyMemory<byte>, CancellationToken)`.
  - [ ] `SendRetryAfterAsync(RetryAfterMetadata, ReadOnlyMemory<byte>, CancellationToken)`.
  - [ ] `SendControlAsync(MessageType, IRuntimeControlMetadata, ReadOnlyMemory<byte>, CancellationToken)` with typed message/metadata validation.
- [ ] Add `NextEventAsync(CancellationToken)` for incoming client controls.
- [ ] Enforce one terminal result or drop send per `NnrpServerOperation`.

## Client And Server Object/Cache Methods

- [ ] Add the frozen typed object methods to both production role sessions.
  - [ ] `DeclareObjectAsync`.
  - [ ] `ReferenceObjectAsync`.
  - [ ] `ReleaseObjectAsync`.
  - [ ] `PatchObjectAsync`.
  - [ ] `SendObjectDeltaAsync`.
- [ ] Add the frozen typed cache methods to both production role sessions.
  - [ ] `ReferenceCacheAsync`.
  - [ ] `ReportCacheMissAsync`.
  - [ ] `InvalidateCacheAsync`.
- [ ] Keep object/cache calls as coarse native operations without JSON serialization or implicit lookup.

## Managed Event And Error Model

- [ ] Add the role-neutral `NnrpRuntimeEvent` projection.
  - [ ] Preserve `RuntimeFrameHeader` and typed metadata.
  - [ ] Expose diagnostic, body, capability-entry, hint, trace, object-metadata, delta, and cache-metadata tails by semantic name.
  - [ ] Return owned memory or lifetime-guarded borrowed memory without exposing native buffers.
- [ ] Add the owned `DecodedRuntimeFrame` projection used by WebSocket and conformance decoders.
  - [ ] Expose `RuntimeFrameHeader`, owned metadata, and owned body regions.
  - [ ] Reject truncated headers, length mismatches, trailing bytes, and batch limits before returning a frame.
- [ ] Replace raw drop-reason integers with `NnrpResultDropReasonCode`.
  - [ ] Use the enum in `SupersedeMetadata`.
  - [ ] Use the enum in `ResultDropReasonMetadata`.
  - [ ] Reject reserved `0x000a..0x7fff` values while preserving private `0x8000..0xffff` values.
- [ ] Add `NnrpResultTerminalState` and the frozen operation-to-terminal mapping.
- [ ] Preserve Rust error identity in managed exceptions.
  - [ ] Preserve `NnrpErrorFamily`.
  - [ ] Preserve the numeric error code.
  - [ ] Preserve the FFI status code.
  - [ ] Preserve retry and recovery diagnostics without string parsing.

## Validation

- [ ] Add public API compile tests for every frozen client and server method.
- [ ] Add unit tests for drop-reason reserved and private ranges.
- [ ] Add unit tests for operation-to-terminal mapping and duplicate terminal sends.
- [ ] Add native-backed integration tests against `1.0.0-preview.4.17` artifacts.
  - [ ] Client control send and server event receive.
  - [ ] Server progress, partial, drop, and trace send and client event receive.
  - [ ] Object/cache send and receive in both directions.
  - [ ] Error family, code, and FFI status preservation.
  - [ ] Late-result suppression after cancellation.
