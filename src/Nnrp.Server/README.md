# Nnrp.Server

Nnrp.Server provides the production server role for the current NNRP/1 contract.

`NnrpServer.ListenAsync` atomically binds the allowed installed transport providers and exposes accepted `NnrpServerSession` and `NnrpServerOperation` instances. Runtime control, object, cache, progress, partial-result, and terminal-result operations use coarse Rust FFI calls; this package does not contain a managed protocol fallback.

Install one or more transport packages alongside this package. The server owns the complete listener set selected by `TransportPolicy` and reports the actual bound endpoint and active transport for each accepted session.

This package depends on `Nnrp.Core` and `Nnrp.NativeBridge`, but carries no native transport artifact itself.

Install:

```powershell
dotnet add package Nnrp.Server --version <published-version>
```

Repository and full SDK documentation:

- https://github.com/NagareWorks/nnrp-cs
- https://nagareworks.github.io/nnrp-doc/
