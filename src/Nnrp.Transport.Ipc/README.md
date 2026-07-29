# Nnrp.Transport.Ipc

`Nnrp.Transport.Ipc` owns the Preview4 IPC provider and IPC-scoped Rust artifacts.

Install this package for same-host communication over Unix domain sockets or Windows named pipes. The
`NnrpNativeIpcTransportProvider` performs connect, listen, and probe through the packaged IPC native library. The
package does not enable IPC behavior hidden in a role or common package.

```powershell
dotnet add package Nnrp.Transport.Ipc --version <published-version>
```

Use `unix:///absolute/path.sock` on Unix hosts and `npipe://pipe-name` on Windows. Platform-incompatible endpoints are
rejected before native handles are created.

Repository and full SDK documentation:

- https://github.com/NagareWorks/nnrp-cs
- https://nagareworks.github.io/nnrp-doc/
