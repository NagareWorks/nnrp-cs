# C# Preview3 Connection And Session Lifecycle

- [ ] Add connection bootstrap helpers distinct from session-open helpers.
- [ ] Add explicit `SessionOpen` / `SessionOpenAck` managed surface once upstream fixed metadata is frozen.
- [ ] Add multi-session routing support so one connection can host multiple live sessions without Unity hosts building private registries.
- [ ] Add explicit session-close helpers separate from connection shutdown.
- [ ] Replace preview2 single-session helpers with the preview3 connection/session model in place.