# 04 - Transport Package Integration

## Transport Package Set

- [ ] Keep TCP package.
- [ ] Keep QUIC package.
- [ ] Add IPC package.
- [ ] Add WebSocket package.
- [ ] Ensure each package owns provider registration.
- [ ] Ensure each package owns native artifacts for its transport.
- [ ] Ensure client/server packages do not hide transport artifacts.

## Provider Registry

- [ ] Add managed provider registry over NativeBridge.
- [ ] Report available transport providers.
- [ ] Report provider cost and preference metadata.
- [ ] Select one installed provider directly when only one provider is present.
- [ ] Probe multiple installed providers by policy when multiple providers are present.
- [ ] Add diagnostics for unsupported provider requests.

## IPC Transport

- [ ] Add managed endpoint model for `unix://`.
- [ ] Add managed endpoint model for `npipe://`.
- [ ] Bind native IPC provider connect.
- [ ] Bind native IPC provider listen.
- [ ] Add loopback smoke tests against preview4 IPC artifacts.
- [ ] Add package validation for IPC artifacts.

## WebSocket Transport

- [ ] Add managed endpoint model for `ws://`.
- [ ] Add managed endpoint model for `wss://`.
- [ ] Bind native WebSocket provider connect.
- [ ] Bind native WebSocket provider listen.
- [ ] Reject text-message protocol paths for data frames.
- [ ] Add loopback smoke tests against preview4 WebSocket artifacts.
- [ ] Add package validation for WebSocket artifacts.

## Unity Packaging

- [ ] Map IPC artifact import settings where supported.
- [ ] Map WebSocket artifact import settings where supported.
- [ ] Keep unsupported Unity platform diagnostics explicit.
- [ ] Regenerate deterministic `.meta` files from CI.
