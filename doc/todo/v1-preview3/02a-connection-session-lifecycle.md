# C# Preview3 Connection And Session Lifecycle

- [x] Add connection bootstrap helpers distinct from session-open helpers.
- [x] Add explicit `SessionOpen` / `SessionOpenAck` managed surface once upstream fixed metadata is frozen.
- [x] Keep multiple opened session handles addressable from one native connection facade.
- [ ] Add higher-level multi-session routing support so Unity hosts do not build private registries.
- [x] Add explicit session-close helpers separate from connection shutdown.
- [x] Add closed-session guards for submit, result polling, cancel, control, and repeated close calls.
- [ ] Replace preview2 single-session helper call sites with the preview3 connection/session model in place.
