# Nnrp.Transport.Tcp

Nnrp.Transport.Tcp provides managed TCP helpers for framed NNRP messages in fixture inspection, diagnostics, and unsupported-runtime flows.

Use this package when you need a managed TCP implementation that works with the NNRP core framing model outside the preview3 native-backed hot path. For preview3 production-style connection/session bootstrap, submit/result polling, cancellation, and control paths, prefer `Nnrp.NativeBridge` so the host surface runs through the Rust-backed native runtime facade.

Do not treat this package as the default preview3 transport when native artifacts are available.

This package depends on Nnrp.Core.

Install:

```powershell
dotnet add package Nnrp.Transport.Tcp --version <published-version>
```

Repository and full SDK documentation:

- https://github.com/NagareWorks/nnrp-cs
- https://nagareworks.github.io/nnrp-doc/
