## Bug

- What was broken?
- How could the issue be reproduced?

## Root Cause

- What actually caused the bug?

## Fix

- What changed to fix it?
- What regression risk remains?

## Validation

- [ ] `dotnet format Nnrp.sln --verify-no-changes --verbosity minimal`
- [ ] `dotnet build Nnrp.sln --configuration Release`
- [ ] Regression test added or existing failure reproduced
- [ ] Packaging checked if package output changed

Commands or workflow runs used:

```text

```

## Release Impact

- [ ] No package output change
- [ ] NuGet package contents or metadata changed
- [ ] Unity release asset contents changed
- [ ] Native bridge payload layout changed
- [ ] Release workflow behavior changed

## Checklist

- [ ] Branch name matches repository conventions
- [ ] Commit messages follow Conventional Commits
- [ ] User-facing behavior changes are documented
