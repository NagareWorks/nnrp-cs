# Contributing to nnrp-cs

This repository publishes SDK packages and release assets from GitHub Actions, so contribution flow needs to stay predictable.

## Branch Strategy

`main` is the protected integration branch.

Use short-lived topic branches for day-to-day work:

- `feature/<scope>-<topic>` for new capabilities
- `fix/<scope>-<topic>` for bug fixes
- `docs/<scope>-<topic>` for documentation-only changes
- `chore/<scope>-<topic>` for maintenance and tooling updates
- `release/<version>` only when stabilizing a public package release candidate

Recommended examples:

- `feature/nativebridge-auto-probe`
- `fix/core-header-validation`
- `docs/release-process`
- `chore/ci-readme-packaging`
- `release/1.0.0-preview.3`

Rules:

- Branch from the latest `main`.
- Keep topic branches focused on one slice of work.
- Rebase or merge from `main` regularly if the branch stays open.
- Merge back to `main` through a pull request.
- Do not publish packages directly from topic branches.

`release/<version>` branches are optional and should be used only when a version needs stabilization passes, packaging rehearsals, or manual workflow runs without publishing from `main`.

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
- Split unrelated changes into separate commits when practical.

## Pull Request Expectations

Every PR should:

- target `main`
- start from the closest GitHub PR template in `.github/PULL_REQUEST_TEMPLATE/`
- explain the user-facing or engineering motivation
- summarize the main code paths changed
- list the validation performed
- mention release impact when package content changes

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

## Review Guidelines

Review for:

- protocol and wire compatibility risk
- packaging and release regressions
- missing tests for changed behavior
- CI workflow correctness
- documentation drift when user-facing behavior changes

If a PR is not ready for merge, leave concrete follow-up items rather than broad requests for cleanup.