# Nnrp.NativeBridge

Nnrp.NativeBridge provides the Rust-backed Preview4 runtime entry point for C# hosts.

Use this package when you want client connection/session bootstrap, submit/result polling, cancellation, and control paths to run through the packaged `nnrp-rs` native artifacts. `NnrpNativeRuntimeSessionHost` is the recommended high-level facade when an application wants one native-backed session surface instead of manually assembling handles.

The default Preview4 path is native-backed and fails fast when the packaged artifact is missing or incompatible. Managed fallback backends are explicit diagnostic or unsupported-runtime hooks; set `FallbackPolicy = NnrpNativeRuntimeFallbackPolicy.UseFallbackForDiagnostics` only for tests, fixture inspection, or hosts that intentionally run without native artifacts.

This package depends on `Nnrp.Core` and may include runtime-specific native binaries when they are present during packing. It does not depend on the managed client/server or managed transport adapter packages for the Preview4 runtime path.

Install:

```powershell
dotnet add package Nnrp.NativeBridge --version <published-version>
```

Basic native-backed session:

```csharp
using Nnrp.NativeBridge;

var options = new NnrpNativeRuntimeSessionHostOptions(
    connectionId: 1,
    connectionGeneration: 1,
    transportId: NnrpNativeArtifact.TransportSlotTcp,
    sessionId: 1,
    sessionGeneration: 1,
    profileId: 1,
    schemaId: 1,
    schemaVersion: 1);

using var host = NnrpNativeRuntimeSessionHost.Open(options);
var result = host.SubmitAndPollResult(operationId: 1, frameId: 1, payload: Array.Empty<byte>());
```

Use an explicit diagnostic fallback:

```csharp
options.FallbackBackend = diagnosticBackend;
options.FallbackPolicy = NnrpNativeRuntimeFallbackPolicy.UseFallbackForDiagnostics;
```

Native-backed server session:

```csharp
var serverOptions = new NnrpNativeRuntimeServerHostOptions(
    serverId: 1,
    serverGeneration: 1,
    transportId: NnrpNativeArtifact.TransportSlotTcp);

using var serverHost = NnrpNativeRuntimeServerHost.Open(serverOptions);
var session = serverHost.AcceptSession(new NnrpNativeRuntimeSessionOptions(
    sessionId: 1,
    sessionGeneration: 1,
    profileId: 1,
    schemaId: 1,
    schemaVersion: 1));

var operation = serverHost.ReceiveSubmit(sessionId: 1, operationId: 1, frameId: 1);
serverHost.SendResult(sessionId: 1, operation, payload: Array.Empty<byte>());
```

Tests and diagnostics can still inject entrypoints directly:

```csharp
using var server = NnrpNativeRuntimeServer.Bind(
    entrypoints,
    serverId: 1,
    generation: 1,
    transportId: NnrpNativeArtifact.TransportSlotTcp);

var session = server.AcceptSession(
    sessionId: 1,
    generation: 1,
    profileId: 1,
    schemaId: 1,
    schemaVersion: 1);

var operation = session.ReceiveSubmit(operationId: 1, frameId: 1);
session.SendResult(operation, payload: Array.Empty<byte>());
```

Native-backed multi-session routing:

```csharp
var connectionOptions = new NnrpNativeRuntimeConnectionHostOptions(
    connectionId: 1,
    connectionGeneration: 1,
    transportId: NnrpNativeArtifact.TransportSlotTcp);

using var connectionHost = NnrpNativeRuntimeConnectionHost.Open(connectionOptions);
connectionHost.OpenSession(new NnrpNativeRuntimeSessionOptions(1, 1, 1, 1, 1));
connectionHost.OpenSession(new NnrpNativeRuntimeSessionOptions(2, 1, 1, 1, 1));

var routed = connectionHost.SubmitAndPollResult(
    sessionId: 1,
    operationId: 10,
    frameId: 1,
    payload: Array.Empty<byte>());
```

Schema and cache helpers:

Schema registry and cache lease helpers stay native-owned. The C# host facade creates safe managed wrappers over the native registry and lease operations so application code does not need to wire `NnrpNativeRuntimeEntrypoints` directly.

```csharp
using var registry = connectionHost.CreateSchemaRegistry();
registry.Install(schemaDescriptor);
registry.ValidateBinding(typedPayloadDescriptor);

var cacheObject = new NnrpCacheObjectId(
    cacheNamespace: 1,
    cacheKeyHigh: 2,
    cacheKeyLow: 3,
    objectKind: 4);

var lease = connectionHost.QueryCacheLease(
    sessionId: 1,
    objectId: cacheObject,
    expectedVersion: 9,
    nowMilliseconds: 1_000,
    ttlMilliseconds: 500);

connectionHost.TouchCacheLease(
    sessionId: 1,
    objectId: cacheObject,
    expectedVersion: lease.ObjectVersion,
    nowMilliseconds: 1_250,
    ttlMilliseconds: 500);

connectionHost.ReleaseCacheLease(new NnrpCacheLeaseHandle(lease.LeaseHandle));
```

Use the same pattern on `NnrpNativeRuntimeServerHost` when server-side hosts need schema registration or cache lease operations. Lease policy, schema binding validation, and version mismatch behavior remain delegated to the native runtime; the managed wrapper carries handles, descriptors, and result snapshots without re-implementing those policies in C#.

Public handle split:

The public Unity/.NET entry surface is the high-level host facade: `NnrpNativeRuntimeSessionHost`, `NnrpNativeRuntimeConnectionHost`, `NnrpNativeRuntimeServer`, and `NnrpNativeRuntimeServerHost`. These own connection/session routing, polling, cancellation, and disposal.

Typed native handles such as `NnrpConnectionHandle`, `NnrpSessionHandle`, `NnrpOperationHandle`, `NnrpSchemaRegistryHandle`, `NnrpBufferHandle`, and `NnrpCacheLeaseHandle` stay public as value wrappers because host code may need to carry identities across diagnostics, cache/schema operations, or native interop boundaries. Application code should not manufacture arbitrary handle values; create them from the native facade result that owns the lifetime.

Borrowed `NnrpBufferView` values and callback sinks are low-level interop shapes. `NnrpNativeBuffer` is the public owner for submit/control/result hot paths that need an explicit native buffer lifetime. `NnrpCallbackSink` is public for the frozen FFI dispatch contract, but host code should use `NnrpCallbackSink.Create` and keep the sink scoped to the native owner that dispatches events.

Profile and operation semantics:

`profileId` is carried through connection/session options and native submit/result paths as a neutral protocol identity. `profileId = 0` means unspecified; it is not treated as an implicit tensor default. Tensor, token, and future standard profiles are peers at the C# boundary, while profile-local payload body interpretation remains owned by the native runtime and the profile contract. Managed hosts should pass descriptors, schema ids, and profile ids through the native facade instead of switching on profile-local body layouts in application plumbing.

Use `SubmitOperation` when an operation should survive beyond one immediate result poll. The returned `NnrpNativeRuntimeOperation` is the stable managed handle for cancellation, routed polling, workflow grouping, and diagnostics. `SubmitAndPollResult` is a convenience wrapper for request-like cases where the caller expects a result within the same host turn. Result lifecycle states preserve the native distinction between completed, partial, degraded, stale reuse, cancelled, and failed; do not collapse these into a boolean success flag in host code.

Unity multi-session orchestration:

Use one `NnrpNativeRuntimeConnectionHost` per native connection when multiple Unity systems need sessions on the same transport. Register sessions through the connection host, submit work with session ids, and poll by `(sessionId, operation)` so unrelated events can remain buffered for the owning session. Avoid per-component native connections for routine in-scene work; that makes cancellation and cache lease ownership harder to reason about.

Cache leases:

Cache lease query, touch, prefetch, and release operations are native-owned. Treat `NnrpCacheLeaseHandle` as a scoped native identity returned by the bridge, not as an application cache key. A Unity or .NET host may store the object id and version for scheduling decisions, but it should release or refresh leases through the same native host facade that acquired them. Expiry, dependency invalidation, and schema mismatch policy stay delegated to the runtime.

Cancellation:

Operation cancellation is frame-scoped on the native session facade. If a managed task is cancelled while a native operation is active, call `Cancel(frameId)` and keep polling until the native runtime reports a terminal lifecycle state or the host decides to close the session. Closing the session/connection is a stronger action than cancelling one operation and should be reserved for host shutdown, transport loss, or unrecoverable protocol state.

Unity event pump and dispatch rules:

The current host surface is polling/event-queue based. Drive one polling owner per native connection, normally from a Unity `Update` loop or from a single managed worker that posts completed work back to Unity's main thread. Do not let multiple Unity behaviours independently poll the same connection; use `NnrpNativeRuntimeConnectionHost` as the shared connection/session registry and route results by session and operation id.

Use `SubmitOperation` when submit and result delivery need to be decoupled. Then call `PollResult` for an operation-specific terminal or partial result, or call `PollAvailableEvents` when the host wants to drain connection-level events and route them itself. `PollResult` buffers unrelated events on the connection so a later session or operation poll can still observe them in order.

Unity APIs must only be touched from Unity's main thread. Native event payloads are copied into managed snapshots before they are returned, so `PayloadMemory` and `PayloadSpan` are safe for read-only inspection after the native poll call returns. If a worker thread polls results, enqueue only the managed snapshot or an application-level DTO back to the main thread.

Borrowed native buffer views are intentionally scoped to the native call or poll result that owns them. `NnrpNativeBuffer.BorrowView()` is for immediate submit/control/result calls; do not cache the returned `NnrpBufferView`, expose it through user callbacks, or enqueue it across frames. Every polled event/result body that crosses the public managed host surface is copied into a managed snapshot. This includes partial-result payloads and metadata returned by runtime object and cache-reference descriptor snapshots. Any public borrowed-view surface must use an explicit scoped owner that cannot outlive the native connection, session, operation, descriptor, or buffer handle.

Close the session or connection after the owner loop has stopped polling. `Dispose` closes the native handles and clears buffered connection events; callbacks or queued work owned by the application should be cancelled before disposing the host facade. The Preview4 FFI exposes dispatch through `NnrpCallbackSink` and `nnrp_dispatch_event`; C# must not invent an additional subscription-handle abstraction outside that frozen contract. `NnrpCallbackSink.None` is the only valid unbound sink. Managed callback consumers must create non-empty sinks with `NnrpCallbackSink.Create` and treat the sink as owned by the native handle whose events it receives. Callbacks may enqueue managed snapshots, but Unity object mutation remains main-thread dispatch only.

Repository and full SDK documentation:

- https://github.com/NagareWorks/nnrp-cs
- https://nagareworks.github.io/nnrp-doc/
