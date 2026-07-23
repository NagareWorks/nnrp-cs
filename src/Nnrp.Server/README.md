# Nnrp.Server

Nnrp.Server provides server-side helpers for the current NNRP/1 session contract.

Use this package when implementing server-side session logic, fixture inspection, diagnostics, or unsupported-runtime helpers on top of the shared NNRP/1 wire model. For Preview4 production-style Rust-backed runtime integration, prefer `Nnrp.NativeBridge` and its native server/session facade.

This package depends on Nnrp.Core.

Install:

```powershell
dotnet add package Nnrp.Server --version <published-version>
```

Repository and full SDK documentation:

- https://github.com/NagareWorks/nnrp-cs
- https://nagareworks.github.io/nnrp-doc/
