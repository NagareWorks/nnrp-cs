# C# Preview3 Scheduling, Credits, And Diagnostics

- [x] Add managed wrappers for session priority class and operation-scoped scheduling hints.
- [x] Add managed models for operation lifecycle state and cancel scope using upstream frozen enums.
  - [x] Add managed operation lifecycle transitions and conformance coverage for public operation states.
  - [x] Add managed cancel-scope routing and conformance coverage for operation, subtree, group, and session cancellation.
- [x] Surface connection/session/operation credit updates without redefining scheduler semantics in C#.
  - [x] Add connection/session/operation `FLOW_UPDATE` conformance coverage against the existing managed wire surface.
- [x] Surface downgrade and retry reasons as managed diagnostics instead of ad hoc string parsing.
- [ ] Add observability hooks for session routing, priority downgrade, and backpressure transitions.
