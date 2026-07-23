# Nnrp.Transport.Quic

Nnrp.Transport.Quic owns the QUIC entry surface for NNRP Preview4 hosts.

Install this package when the host should expose QUIC as an installed transport candidate. The package provides
transport-specific native runtime options and factory methods that pin the QUIC transport id before calling into
`Nnrp.NativeBridge`. Install only `Nnrp.Transport.Quic` to expose QUIC, install only `Nnrp.Transport.Tcp` to expose TCP, or
install both packages when the host should probe and select between installed transport candidates.

This package depends on Nnrp.Core and Nnrp.NativeBridge.

Install:

```powershell
dotnet add package Nnrp.Transport.Quic --version <published-version>
```

Repository and full SDK documentation:

- https://github.com/NagareWorks/nnrp-cs
- https://nagareworks.github.io/nnrp-doc/
