# 04 - Transport Package Integration

## Frozen Provider Contracts

- [ ] Add immutable provider value types in `Nnrp.Core`.
  - [ ] `NnrpTransportProviderCost(ModelId, Units)`.
  - [ ] `NnrpTransportProviderLimits(MaxFrameBytes)`.
  - [ ] `NnrpTransportProviderLimitation` with every frozen limitation value.
  - [ ] `NnrpTransportProviderMetadata(Id, Cost, PreferenceRank, Limits, Limitations)`.
  - [ ] `NnrpTransportProviderDescriptor(Name, Version, TransportId, Kind, Available, LibraryPath, Metadata, Diagnostic)`.
  - [ ] `NnrpTransportProbeState`.
  - [ ] `NnrpTransportProbeMetrics(SampleCount, SuccessCount, MedianThroughputBytesPerSecond, MedianRttMicroseconds)`.
  - [ ] `NnrpTransportRejectionReason`.
  - [ ] `NnrpTransportCandidate`.
  - [ ] `NnrpTransportSelection`.
  - [ ] `NnrpTransportSelectionOptions` with policy, peer support, minimum frame bytes, and probe requirements.
- [ ] Add endpoint and security contracts in `Nnrp.Core`.
  - [x] Parse only `nnrp://` and `nnrps://` as `NnrpEndpoint` application endpoints.
  - [x] Preserve the authority, path, query, and secure intent of `NnrpEndpoint`.
  - [x] Represent explicit carrier-local locators as `NnrpProviderEndpoint`.
  - [ ] Derive TCP and QUIC host/port locators from the application authority when no override is present.
  - [ ] Require a matching explicit `unix://` or `npipe://` provider endpoint before selecting IPC.
  - [ ] Require a matching explicit `ws://` or `wss://` provider endpoint before selecting WebSocket.
  - [ ] Add `NnrpClientProviderRoute` and `NnrpServerProviderRoute` with route-local locator and security.
  - [ ] Add immutable client and server route dictionaries keyed by `TransportId`.
  - [ ] Keep the exact owned client/server security fields on each route.
  - [ ] Exclude unresolved client candidates under `Auto` and `Prefer*`; fail forced policies without fallback.
  - [ ] Treat an unresolved otherwise-eligible server route as a hard listen error.
  - [ ] Reject provider-kind mismatches and platform-incompatible IPC locators before creating native handles.
  - [ ] Reject unknown route keys and report known-but-uninstalled routes as `LocalUnavailable`.
  - [ ] Apply the exact rejection precedence when multiple checks fail.
  - [x] Add `NnrpTransportClientSecurity(ServerName, TrustedCertificateDer)`.
  - [x] Add `NnrpTransportServerSecurity(CertificateDer, PrivateKeyPkcs8Der)`.
  - [ ] Reject client credentials on listen paths and server credentials on connect paths.
  - [ ] Add `RouteUnresolved` and `SecurityUnsatisfied` rejection reasons.
  - [ ] Enforce TCP TLS, QUIC TLS, and WSS for `nnrps://`.
  - [ ] Reject IPC, plain TCP, and WS for `nnrps://`.
- [ ] Replace the slot/priority-only provider contract.
  - [ ] Expose the validated provider descriptor from `INnrpNativeTransportProvider`.
  - [ ] Add `NnrpTransportConnectOptions`.
  - [ ] Add `NnrpTransportListenOptions`.
  - [ ] Add `NnrpTransportProbeOptions`.
  - [ ] Add opaque `NnrpTransportConnection` ownership values without public FFI handles.
  - [ ] Add opaque `NnrpTransportListener` ownership values without public FFI handles.
  - [ ] Expose `ConnectAsync` from each provider.
  - [ ] Expose `ListenAsync` from each provider.
  - [ ] Expose `ProbeAsync` from each provider.
  - [ ] Reject metadata that disagrees with the artifact manifest or transport slot.

## Provider Registration And Selection

- [ ] Add `NnrpNativeTransportRegistry`.
  - [ ] Register each installed transport package exactly once.
  - [ ] Reject duplicate transport IDs and duplicate provider IDs.
  - [ ] Return immutable snapshots of registered providers.
  - [ ] Allow an explicit provider list to replace the default registry for tests and controlled deployments.
- [ ] Implement the frozen provider comparator without a C#-specific weighted score.
  - [ ] Reject policy-disallowed candidates.
  - [ ] Reject locally unavailable candidates.
  - [ ] Reject peer-unsupported candidates.
  - [ ] Reject candidates whose frame limits are insufficient.
  - [ ] Reject required probes that are missing or failed.
  - [ ] Compare cost, preference rank, probe throughput, probe RTT, and stable provider identity in frozen order.
  - [ ] Select the only valid installed provider without probing.
  - [ ] Probe and rank all valid providers when more than one remains.
  - [ ] Return every rejected candidate and its typed reason.
- [ ] Add deterministic unit tests for every comparator key and rejection reason.

## Role Host Cardinality

- [ ] Implement multi-route client orchestration in `Nnrp.Client`.
  - [ ] Resolve each registered provider against its own route.
  - [ ] Probe every eligible Auto/Prefer route.
  - [ ] Preserve rejected candidates in ordered diagnostics.
  - [ ] Transfer only the selected carrier into the native client runtime.
  - [ ] Make Force fail without fallback.
- [ ] Implement an atomic multi-listener server in `Nnrp.Server`.
  - [ ] Resolve every policy-allowed registered provider route.
  - [ ] Bind every eligible Auto/Prefer listener.
  - [ ] Restrict Force to the named listener.
  - [ ] Roll back all opened listeners after any required bind or adoption failure.
  - [ ] Accept across the listener set and expose active transport per session.
  - [ ] Expose every actual bound provider endpoint, including assigned ports.
  - [ ] Expose the actual listener transport as `NnrpServerSession.ActiveTransportId`.
  - [ ] Fail and close the complete logical server after a terminal provider-listener failure.
  - [ ] Close listeners and accepted sessions exactly once.
- [ ] Add architecture tests preserving the coarse native boundary.
  - [ ] Keep one carrier per client and accepted-session handle.
  - [ ] Keep one provider listener per low-level native listener handle.
  - [x] Retain one native accept ticket per provider listener across bounded host polls.
  - [x] Release a pending accept ticket before closing its provider listener.
  - [ ] Reject per-frame managed dispatch across multiple transport libraries.

## TCP Package

- [ ] Update `Nnrp.Transport.Tcp` to the frozen provider contract.
- [ ] Validate TCP provider metadata against the TCP artifact manifest.
- [ ] Keep TCP connect, listen, and probe calls inside the TCP package.
- [ ] Package only TCP native artifacts for every supported RID.
- [ ] Add TCP client/server loopback integration tests.
- [ ] Add TCP NuGet content inspection tests.

## QUIC Package

- [ ] Update `Nnrp.Transport.Quic` to the frozen provider contract.
- [ ] Validate QUIC provider metadata against the QUIC artifact manifest.
- [ ] Keep QUIC connect, listen, and probe calls inside the QUIC package.
- [ ] Require client/server security for secure QUIC endpoints.
- [ ] Package only QUIC native artifacts for every supported RID.
- [ ] Add QUIC client/server loopback integration tests.
- [ ] Add QUIC NuGet content inspection tests.

## IPC Package

- [ ] Add the `Nnrp.Transport.Ipc` project and NuGet package.
- [ ] Add `NnrpNativeIpcTransportProvider` and `NnrpNativeIpcRuntime`.
- [ ] Validate IPC provider metadata against the IPC artifact manifest.
- [ ] Parse `unix://` provider endpoints on Unix hosts.
- [ ] Parse `npipe://` provider endpoints on Windows hosts.
- [ ] Reject `unix://` on Windows and `npipe://` on non-Windows hosts with typed diagnostics.
- [ ] Keep IPC connect, listen, and probe calls inside the IPC package.
- [ ] Package only IPC native artifacts for every supported RID.
- [ ] Add Unix-domain-socket client/server loopback integration tests.
- [ ] Add Windows named-pipe client/server loopback integration tests.
- [ ] Add IPC NuGet content inspection tests.

## WebSocket Package

- [ ] Add the `Nnrp.Transport.WebSocket` project and NuGet package.
- [ ] Add `NnrpNativeWebSocketTransportProvider` and `NnrpNativeWebSocketRuntime`.
- [ ] Validate WebSocket provider metadata against the WebSocket artifact manifest.
- [ ] Parse `ws://` and `wss://` provider endpoints.
- [ ] Require client/server security for `wss://`.
- [ ] Keep WebSocket connect, listen, and probe calls inside the WebSocket package.
- [ ] Add `NnrpWebSocketFrameCodec.Encode`.
- [ ] Add `NnrpWebSocketFrameCodec.Decode`.
- [ ] Add `NnrpWebSocketFrameCodec.DecodeBatch`.
- [ ] Reject text messages, truncated frames, length mismatches, and trailing bytes.
- [ ] Package only WebSocket native artifacts for every supported RID.
- [ ] Add `ws://` and `wss://` client/server loopback integration tests.
- [ ] Add WebSocket NuGet content inspection tests.

## Unity Packaging

- [ ] Include TCP, QUIC, IPC, and WebSocket managed provider assemblies.
- [ ] Map every downloaded transport artifact to its transport-scoped Unity plugin path.
- [ ] Generate explicit plugin import settings for Windows, macOS, Linux, Android, iOS, and iOS Simulator RIDs present in the Rust release.
- [ ] Reject duplicate plugin filenames and cross-transport artifact placement.
- [ ] Generate deterministic `.meta` files from CI.
- [ ] Add UPM inspection tests for all four transport directories and every declared platform mapping.
