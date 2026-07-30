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

- [x] Add typed runtime-control methods to the production `NnrpClientSession`.
  - [x] `CancelAsync(ControlRequestMetadata, ReadOnlyMemory<byte>, CancellationToken)`.
  - [x] `AbortAsync(ControlRequestMetadata, ReadOnlyMemory<byte>, CancellationToken)`.
  - [x] `UpdatePriorityAsync(SchedulingMetadata, CancellationToken)`.
  - [x] `UpdateDeadlineAsync(SchedulingMetadata, CancellationToken)`.
  - [x] `ExpireAtAsync(SchedulingMetadata, CancellationToken)`.
  - [x] `SupersedeAsync(SupersedeMetadata, ReadOnlyMemory<byte>, CancellationToken)`.
  - [x] `UpdateBudgetAsync(BudgetMetadata, CancellationToken)`.
  - [x] `NegotiateCapabilitiesAsync(CapabilityMetadata, ReadOnlyMemory<byte>, CancellationToken)`.
  - [x] `DegradeProfileAsync(CapabilityMetadata, ReadOnlyMemory<byte>, CancellationToken)`.
  - [x] `SendRouteHintAsync(RouteHintMetadata, ReadOnlyMemory<byte>, CancellationToken)`.
  - [x] `SendExecutionHintAsync(RouteHintMetadata, ReadOnlyMemory<byte>, CancellationToken)`.
  - [x] `SendTraceContextAsync(TraceContextMetadata, ReadOnlyMemory<byte>, CancellationToken)`.
  - [x] `SendControlAsync(MessageType, IRuntimeControlMetadata, ReadOnlyMemory<byte>, CancellationToken)` with typed message/metadata validation.
- [x] Add production result and event iteration.
  - [x] `NextResultAsync(CancellationToken)`.
  - [x] `NextEventAsync(CancellationToken)` returning `NnrpRuntimeEvent` in wire order.
  - [x] Suppress late `RESULT_PUSH` after cancel or abort reaches terminal state.
  - [x] Suppress late `PARTIAL_RESULT` after cancel or abort reaches terminal state.
  - [x] Keep `RESULT_DROP_REASON` observable after late-result suppression.

## Server Role Controls

- [x] Add typed runtime-control sends to the production `NnrpServerSession` and `NnrpServerOperation`.
  - [x] `SendProgressAsync(ProgressMetadata, ReadOnlyMemory<byte>, CancellationToken)`.
  - [x] `SendPartialResultAsync(PartialResultMetadata, ReadOnlyMemory<byte>, CancellationToken)`.
  - [x] `SendBackpressureAsync(PressureMetadata, CancellationToken)`.
  - [x] `SendCreditUpdateAsync(PressureMetadata, CancellationToken)`.
  - [x] `SendResultDropReasonAsync(ResultDropReasonMetadata, ReadOnlyMemory<byte>, CancellationToken)`.
  - [x] `SendTraceContextAsync(TraceContextMetadata, ReadOnlyMemory<byte>, CancellationToken)`.
  - [x] `SendRecoverableErrorAsync(RecoverableErrorMetadata, ReadOnlyMemory<byte>, CancellationToken)`.
  - [x] `SendRetryAfterAsync(RetryAfterMetadata, ReadOnlyMemory<byte>, CancellationToken)`.
  - [x] `SendControlAsync(MessageType, IRuntimeControlMetadata, ReadOnlyMemory<byte>, CancellationToken)` with typed message/metadata validation.
- [x] Add `NextEventAsync(CancellationToken)` for incoming client controls.
- [x] Enforce one terminal result or drop send per `NnrpServerOperation`.

## Client And Server Object/Cache Methods

- [x] Add the frozen typed object methods to both production role sessions.
  - [x] `DeclareObjectAsync`.
  - [x] `ReferenceObjectAsync`.
  - [x] `ReleaseObjectAsync`.
  - [x] `PatchObjectAsync`.
  - [x] `SendObjectDeltaAsync`.
- [x] Add the frozen typed cache methods to both production role sessions.
  - [x] `ReferenceCacheAsync`.
  - [x] `ReportCacheMissAsync`.
  - [x] `InvalidateCacheAsync`.
- [x] Keep object/cache calls as coarse native operations without JSON serialization or implicit lookup.

## Managed Event And Error Model

- [x] Add the role-neutral `NnrpRuntimeEvent` projection.
  - [x] Preserve `RuntimeFrameHeader` and typed metadata.
  - [x] Expose diagnostic, body, capability-entry, hint, trace, object-metadata, delta, and cache-metadata tails by semantic name.
  - [x] Return owned memory or lifetime-guarded borrowed memory without exposing native buffers.
- [x] Add the owned `DecodedRuntimeFrame` projection used by WebSocket and conformance decoders.
  - [x] Expose `RuntimeFrameHeader`, owned metadata, and owned body regions.
  - [x] Reject truncated headers, length mismatches, trailing bytes, and batch limits before returning a frame.
- [x] Replace raw drop-reason integers with `NnrpResultDropReasonCode`.
  - [x] Use the enum in `SupersedeMetadata`.
  - [x] Use the enum in `ResultDropReasonMetadata`.
  - [x] Reject reserved `0x000a..0x7fff` values while preserving private `0x8000..0xffff` values.
- [x] Add `NnrpResultTerminalState` and the frozen operation-to-terminal mapping.
- [x] Preserve Rust error identity in managed exceptions.
  - [x] Preserve `NnrpErrorFamily`.
  - [x] Preserve the numeric error code.
  - [x] Preserve the FFI status code.
  - [x] Preserve retry and recovery diagnostics without string parsing.

## Validation

- [x] Add public API compile tests for every frozen client and server method.
- [x] Add unit tests for drop-reason reserved and private ranges.
- [x] Add unit tests for operation-to-terminal mapping and duplicate terminal sends.
- [x] Add native-backed integration tests against `1.0.0-preview.4.21` artifacts with exact FFI ABI `4.3.0`.
  - [x] Client control send and server event receive.
  - [x] Server progress, partial, drop, and trace send and client event receive.
  - [x] Object/cache send and receive in both directions.
  - [x] Error family, code, and FFI status preservation.
  - [x] Late-result suppression after cancellation.
