## Summary

- What maintenance change is included?
- Why is it being made now?

## Change Type

- [ ] CI or workflow change
- [ ] Tooling or dependency maintenance
- [ ] Packaging metadata update
- [ ] Refactor with no intended behavior change

## Validation

- [ ] `dotnet format Nnrp.sln --verify-no-changes --verbosity minimal`
- [ ] `dotnet build Nnrp.sln --configuration Release`
- [ ] Targeted validation for the affected tooling or workflow path

Commands or workflow runs used:

```text

```

## Release Impact

- [ ] No package output change
- [ ] NuGet package contents or metadata changed
- [ ] Unity release asset contents changed
- [ ] Release workflow behavior changed

## Checklist

- [ ] Branch name matches repository conventions
- [ ] Commit messages follow Conventional Commits
- [ ] PR is squashed to one commit unless this is necessary `release/<version>` branch work
- [ ] Follow-up operational work is documented if needed
