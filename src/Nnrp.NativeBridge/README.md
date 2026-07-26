# Nnrp.NativeBridge

`Nnrp.NativeBridge` exposes the Rust-backed Preview4 runtime and the common native transport-provider contract used by
the C# SDK.

Applications do not create native transport handles or select transport slots directly. An installed transport package
opens an opaque `NnrpTransportConnection` or `NnrpTransportListener`; the bridge transfers that carrier exactly once into
the Rust client or server role. Session, operation, schema, cache, cancellation, and control work then stays behind the
coarse native boundary.

The bridge package does not choose a carrier and does not provide a managed production fallback. Install one or more
transport packages and supply route-local endpoints and security through the high-level client/server options. Missing or
incompatible native artifacts fail explicitly.

Low-level host composition:

```csharp
var provider = NnrpNativeTcpTransportProvider.Instance;
var carrier = await provider.ConnectAsync(connectOptions, cancellationToken);

using var host = NnrpNativeRuntimeSessionHost.Open(
    carrier,
    new NnrpNativeRuntimeSessionHostOptions(
        connectionId: 1,
        connectionGeneration: 1,
        sessionId: 1,
        sessionGeneration: 1,
        profileId: 1,
        schemaId: 1,
        schemaVersion: 1));
```

Most applications should use `Nnrp.Client` or `Nnrp.Server`; the low-level bridge surface exists for hosts that manage
native role and session identities directly.

Repository and full SDK documentation:

- https://github.com/NagareWorks/nnrp-cs
- https://nagareworks.github.io/nnrp-doc/
