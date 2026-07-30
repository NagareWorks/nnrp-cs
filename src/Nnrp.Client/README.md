# Nnrp.Client

Nnrp.Client provides the production client role for the current NNRP/1 contract.

`NnrpClient.ConnectAsync` resolves an installed transport provider, adopts its native connection, and exposes owned `NnrpClientSession` instances. Submission, runtime control, object, cache, result, and event operations use coarse Rust FFI calls; this package does not contain a managed protocol fallback.

Install one or more transport packages alongside this package. With one provider installed it is selected directly; with multiple providers installed the client probes viable routes according to `TransportPolicy`.

This package depends on `Nnrp.Core` and `Nnrp.NativeBridge`, but carries no native transport artifact itself.

Install:

```powershell
dotnet add package Nnrp.Client --version <published-version>
```

Repository and full SDK documentation:

- https://github.com/NagareWorks/nnrp-cs
- https://nagareworks.github.io/nnrp-doc/
