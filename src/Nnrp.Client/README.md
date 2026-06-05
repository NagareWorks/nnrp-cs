# Nnrp.Client

Nnrp.Client provides the managed client-facing helpers for the current NNRP/1 session contract.

Use this package when you want managed client helpers for fixture inspection, diagnostics, or runtime combinations where packaged native artifacts are not available. For preview3 production-style connection/session bootstrap, submit/result polling, cancellation, and control paths, prefer `Nnrp.NativeBridge` so the host surface runs through the Rust-backed native runtime facade.

This package depends on Nnrp.Core.

Install:

```powershell
dotnet add package Nnrp.Client --version <published-version>
```

Repository and full SDK documentation:

- https://github.com/NagareWorks/nnrp-cs
- https://nagareworks.github.io/nnrp-doc/
