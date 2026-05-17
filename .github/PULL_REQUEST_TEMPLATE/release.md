## Summary

- What release or release-preparation change is included?

## Versioning

- Target version:
- Why this version is needed:

## Package and Asset Impact

- [ ] NuGet package metadata changed
- [ ] NuGet package contents changed
- [ ] Unity release asset contents changed
- [ ] Native runtime payload layout changed
- [ ] Release workflow behavior changed

Describe the release-facing impact:

## Validation

- [ ] `dotnet format Nnrp.sln --verify-no-changes --verbosity minimal`
- [ ] `dotnet build Nnrp.sln --configuration Release`
- [ ] `dotnet pack Nnrp.sln -c Release`
- [ ] Release workflow assumptions were checked

Commands or workflow runs used:

```text

```

## Manual Registry Steps

- [ ] No manual registry work required
- [ ] NuGet registry state was reviewed
- [ ] GitHub Release asset expectations were reviewed

Notes:

## Checklist

- [ ] Branch name matches repository conventions
- [ ] Commit messages follow Conventional Commits
- [ ] PR is squashed to one commit unless this is necessary `release/<version>` branch work
- [ ] Release notes or docs were updated if needed