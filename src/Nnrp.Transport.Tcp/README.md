# Nnrp.Transport.Tcp

Nnrp.Transport.Tcp owns the TCP entry surface for NNRP Preview4 hosts plus managed TCP helpers for framed NNRP messages in
fixture inspection, diagnostics, and unsupported-runtime flows.

Install this package when the host should expose TCP as an installed transport candidate. The package provides
transport-specific native runtime options and factory methods that pin the TCP transport id before calling into
`Nnrp.NativeBridge`. Install only `Nnrp.Transport.Tcp` to expose TCP, install only `Nnrp.Transport.Quic` to expose QUIC, or
install both packages when the host should probe and select between installed transport candidates.

Use the managed TCP implementation when you need a diagnostic implementation that works with the NNRP core framing model
outside the Preview4 native-backed hot path. For Preview4 production-style connection/session bootstrap, submit/result
polling, cancellation, and control paths, prefer `NnrpNativeTcpRuntime`.

This package depends on Nnrp.Core and Nnrp.NativeBridge.

Install:

```powershell
dotnet add package Nnrp.Transport.Tcp --version <published-version>
```

Repository and full SDK documentation:

- https://github.com/NagareWorks/nnrp-cs
- https://nagareworks.github.io/nnrp-doc/
