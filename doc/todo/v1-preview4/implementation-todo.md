# C# Preview4 Implementation Todo

Preview4 C# work adapts managed client/server/Unity surfaces to runtime control, runtime objects, transport provider packages, and wire-level conformance while keeping Rust native artifacts as the protocol implementation source.

## Workstreams

- [ ] [01 - Contract and package boundaries](01-contract-and-package-boundaries.md)
- [ ] [02 - Managed runtime control API](02-managed-runtime-control-api.md)
- [ ] [03 - Runtime object and cache references](03-runtime-object-cache-references.md)
- [ ] [04 - Transport package integration](04-transport-package-integration.md)
- [ ] [05 - Wire conformance and validation](05-wire-conformance-and-validation.md)
- [ ] [06 - Release packaging and docs](06-release-packaging-and-docs.md)

## Coordination Rules

- [ ] Keep managed APIs as host-facing wrappers over Rust-backed behavior.
- [ ] Keep transport packages owning real native artifacts and provider logic.
- [ ] Keep Unity package layout separate from NuGet client/server package layout.
- [ ] Keep adapter conformance, wire conformance, and benchmark jobs separate.
- [ ] Update this index whenever a workstream is split or completed.
