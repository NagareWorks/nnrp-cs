# C# Preview4 Implementation Todo

Preview4 C# work adapts managed client/server/Unity surfaces to runtime control, runtime objects, transport provider packages, and wire-level conformance while keeping Rust native artifacts as the protocol implementation source.

## Workstreams

- [ ] [01 - Contract and package boundaries](01-contract-and-package-boundaries.md)
- [ ] [02 - Managed runtime control API](02-managed-runtime-control-api.md)
- [ ] [03 - Runtime object and cache references](03-runtime-object-cache-references.md)
- [ ] [04 - Transport package integration](04-transport-package-integration.md)
- [ ] [05 - Wire conformance and validation](05-wire-conformance-and-validation.md)
- [ ] [06 - Release packaging and docs](06-release-packaging-and-docs.md)

## Frozen Delivery Rules

- Managed role APIs are host-facing wrappers over Rust-backed behavior.
- Each transport package owns its provider implementation and its transport-scoped native artifacts.
- Client and server packages never hide transport artifacts or transport selection behind configuration flags.
- The Unity package has its own deterministic plugin layout and does not reuse the NuGet runtime layout.
- Adapter conformance, wire conformance, and benchmark jobs remain separate CI gates.
- A workstream is checked here only when every checkbox in its linked document is complete.
- Preview4 does not preserve Preview1, Preview2, or Preview3 public entrypoints.
