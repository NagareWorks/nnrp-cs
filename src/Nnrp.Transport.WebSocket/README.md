# Nnrp.Transport.WebSocket

`Nnrp.Transport.WebSocket` owns the Preview4 WebSocket provider, binary frame codec, and WebSocket-scoped Rust
artifacts.

Install this package when an NNRP client or server uses an explicit `ws://` or `wss://` provider route. Public
application endpoints remain `nnrp://` or `nnrps://`; the WebSocket locator is a carrier-local route owned by this
package.

```powershell
dotnet add package Nnrp.Transport.WebSocket --version 1.0.0-preview.4
```

Repository and full SDK documentation:

- https://github.com/NagareWorks/nnrp-cs
- https://nagareworks.github.io/nnrp-doc/en/sdk/csharp/api/transport
