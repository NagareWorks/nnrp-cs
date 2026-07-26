# Nnrp.Transport.Quic

`Nnrp.Transport.Quic` owns the Preview4 QUIC provider implementation and QUIC-scoped Rust artifacts.

Install this package when a host should expose QUIC. `NnrpNativeQuicTransportProvider` performs QUIC connect, listen, and
probe operations through the packaged QUIC native library and returns opaque carriers to `Nnrp.NativeBridge`. Installing
the package adds real QUIC behavior; it is not a configuration switch over an implementation hidden in another package.

QUIC routes use route-local client or server security material as required by the frozen Preview4 endpoint contract.

```powershell
dotnet add package Nnrp.Transport.Quic --version <published-version>
```

Install QUIC alone to use QUIC without probing. Install multiple provider packages when Auto or Prefer policy should probe
and compare eligible carriers.

Repository and full SDK documentation:

- https://github.com/NagareWorks/nnrp-cs
- https://nagareworks.github.io/nnrp-doc/
