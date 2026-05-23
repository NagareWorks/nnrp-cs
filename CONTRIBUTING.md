# Contributing to nnrp-cs

This repository publishes SDK packages and release assets from GitHub Actions, so contribution flow needs to stay predictable.

## Branch Strategy

`main` is the stable branch for released or release-ready SDK state.

`develop` is the version integration branch for active preview work. Preview feature, fix, documentation, and maintenance branches should merge into `develop` first. For the preview3 line, `develop` carries the work that used to live on `release/1.0.0-preview.3`.

Use short-lived topic branches for day-to-day work:

- `feature/<scope>-<topic>` for new capabilities
- `fix/<scope>-<topic>` for bug fixes
- `docs/<scope>-<topic>` for documentation-only changes
- `chore/<scope>-<topic>` for maintenance and tooling updates
- `release/<version>` only after `develop` is ready to freeze into a public package release candidate

Recommended examples:

- `feature/nativebridge-auto-probe`
- `fix/core-header-validation`
- `docs/release-process`
- `chore/ci-readme-packaging`
- `release/1.0.0-preview.3`

Rules:

- Branch from the latest `develop` for active preview work.
- Branch from `main` only for hotfixes against already released stable state.
- Keep topic branches focused on one slice of work.
- Rebase or merge from `develop` regularly if the branch stays open.
- Merge normal preview work back to `develop` through a pull request.
- Do not push directly to `main`; enforce this with a GitHub ruleset or branch protection rule.
- Do not push directly to `develop`; enforce this with a GitHub ruleset or branch protection rule when the repository is public.
- Do not publish packages directly from topic branches.

`release/<version>` branches are freeze branches. Cut them from `develop` only when the version is feature-complete enough for stabilization passes, packaging rehearsals, or manual workflow runs. Keep release branches short-lived unless a published line needs explicit long-term maintenance.

After a release branch is cut:

- accept only release-blocking fixes, version metadata, package metadata, and release documentation on that branch
- merge accepted fixes back to `develop`
- tag the final release from the release branch or from the merged stable state, according to the release workflow
- delete the release branch after publication unless it represents an explicitly maintained LTS line

Manual `Release` workflow runs should leave external publishing disabled unless you intentionally enable `create_tag`; package publication from an untagged ref is not allowed.

## Commit Message Convention

Use Conventional Commits.

Preferred forms:

- `feat: add transport probe selection summary`
- `fix: correct result push body alignment`
- `docs: clarify Unity package download path`
- `chore: tighten release workflow permissions`
- `test: add frame submit edge coverage`
- `refactor: simplify native bridge setup`

Rules:

- Keep the subject line imperative.
- Keep the first line concise.
- Use a scope only when it adds clarity, for example `fix(core): reject invalid tile counts`.
- You can use multiple local commits while iterating, but normal PRs from `feature/*`, `fix/*`, `docs/*`, or `chore/*` branches must be squashed to exactly one commit before review.
- Only version-maintenance PRs that target or originate from `release/<version>` branches may keep multiple commits when that history is actually needed.

## Pull Request Expectations

Every PR should:

- target `develop` for normal preview work, `main` for stable hotfixes, or `release/<version>` only during an active release freeze
- use the default GitHub PR template that auto-loads on the PR page; specialized reference variants remain in `.github/PULL_REQUEST_TEMPLATE/` when you need to adapt the structure
- explain the user-facing or engineering motivation
- summarize the main code paths changed
- list the validation performed
- mention release impact when package content changes
- contain exactly one commit before review unless it is a necessary `release/<version>` branch PR
- pass the `required-checks` GitHub Actions job before merge

PRs that violate the normal one-commit rule are not reviewed until they are squashed.

If a change affects package output, call that out explicitly:

- NuGet package metadata or contents changed
- Unity release asset contents changed
- native runtime payload layout changed
- release workflow behavior changed

## Validation Expectations

Before opening or merging a PR, prefer the narrowest validation that proves the touched slice:

- `dotnet format Nnrp.sln --verify-no-changes --verbosity minimal`
- `dotnet build Nnrp.sln --configuration Release`
- targeted `dotnet test` commands for the affected projects
- `dotnet pack Nnrp.sln -c Release` when package metadata or pack contents changed

PRs that affect CI, packaging, or release assets should include the exact command or workflow path used for validation.

## Versioning and Release Notes

Do not reuse a published NuGet version. If package contents change after publication, create a new version.

When preparing a release PR:

- update the version source intentionally
- confirm package metadata is correct
- confirm release assets have the expected names
- note any manual steps required on package registries

Public release publication is gated through the `Release` workflow and should only happen from a short release tag or an explicit manual dispatch.

- `Release` runs on pushed `v*` tags and on manual `workflow_dispatch`; normal branch pushes must not publish GitHub releases or package registries.
- Use the `release` GitHub environment for any publish-capable job.
- Set `GITHUB_PACKAGES_PUBLISH_MODE` on the `release` environment to `disabled` or `enabled`.
- Set `NUGET_PUBLISH_MODE` on the `release` environment to `disabled`, `trusted`, or `token`.
- If you use Trusted Publishing for nuget.org, provide `NUGET_USER` as an environment variable on `release`.
- If you use token-based publishing for nuget.org, store `NUGET_API_KEY` as an environment secret on `release`.

## Review Guidelines

Review for:

- protocol and wire compatibility risk
- packaging and release regressions
- missing tests for changed behavior
- CI workflow correctness
- documentation drift when user-facing behavior changes

Do not start normal feature, fix, docs, or maintenance review while the PR still carries multiple commits.

If a PR is not ready for merge, leave concrete follow-up items rather than broad requests for cleanup.
