# C# Preview3 Connection And Session Lifecycle

- [x] Add connection bootstrap helpers distinct from session-open helpers.
- [x] Add explicit `SessionOpen` / `SessionOpenAck` managed surface once upstream fixed metadata is frozen.
- [x] Keep multiple opened session handles addressable from one native connection facade.
- [x] Add higher-level multi-session routing support so Unity hosts do not build private registries.
  - [x] Add a managed session container and conformance coverage for multi-session routing and close behavior.
- [x] Add explicit session-close helpers separate from connection shutdown.
- [x] Add closed-session guards for submit, result polling, cancel, control, and repeated close calls.
- [x] Replace preview2 single-session helper call sites with the preview3 connection/session model in place.
  - [x] Route one-shot session host open/submit/control/close calls through the preview3 native connection/session facade.
  - [x] Route multi-session connection host calls through registered preview3 session handles instead of private single-session state.
  - [x] Add native-buffer payload overloads to session and connection host calls so old byte-array helper paths are not the only hot-path surface.
