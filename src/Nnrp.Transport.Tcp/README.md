# Nnrp.Transport.Tcp

`Nnrp.Transport.Tcp` owns the Preview4 TCP provider implementation and TCP-scoped Rust artifacts.

Install this package when a host should expose TCP. `NnrpNativeTcpTransportProvider` performs TCP connect, listen, and
probe operations through the packaged TCP native library and returns opaque carriers to `Nnrp.NativeBridge`. Installing
the package adds real TCP behavior; it is not a configuration switch over an implementation hidden in another package.

```powershell
dotnet add package Nnrp.Transport.Tcp --version <published-version>
```

Install TCP alone to use TCP without probing. Install multiple provider packages when Auto or Prefer policy should probe
and compare eligible carriers.

Repository and full SDK documentation:

- https://github.com/NagareWorks/nnrp-cs
- https://nagareworks.github.io/nnrp-doc/
