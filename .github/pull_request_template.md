## Summary

- PR type: feature / bugfix / docs / maintenance / release
- What changed?
- Why is it needed now?

## Implementation

- Main code paths changed:
- Protocol, packaging, or release assumptions introduced:
- Follow-up work, if any:

## Validation

- [ ] `dotnet format Nnrp.sln --verify-no-changes --verbosity minimal`
- [ ] `dotnet build Nnrp.sln --configuration Release`
- [ ] Targeted tests for the affected slice
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
- [ ] PR is squashed to one commit unless this is necessary `release/<version>` branch work
- [ ] Documentation was updated when behavior changed

## Notes

- Specialized reference templates still live in `.github/PULL_REQUEST_TEMPLATE/`.
- GitHub does not show an automatic chooser for those files on the standard PR page, so this file acts as the default template.