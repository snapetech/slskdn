# Testing Policy

## Purpose

slskdn needs one repeatable gate for release-critical behavior, not scattered one-off tests. The goal is to catch the failure classes that have actually hurt releases:

- frontend bootstrap regressions
- subpath hosting / packaged asset regressions
- backend startup and API smoke failures
- release metadata drift

## Test Layers

### 0. Reproduce-Then-Disprove Bugfix Work

For tester-reported or externally reported regressions, the release bar is not just "some tests passed." The team must capture the reported repro contract, reproduce the same failure locally or in an equivalent environment when practical, then verify the same path after the patch.

Use [bugfix-verification-checklist.md](<repo-root>/docs/dev/bugfix-verification-checklist.md) before calling a reported issue "fixed."

Rules:

- split multi-symptom reports into separate acceptance checks
- do not call a bug "fixed" based on a partial internal signal if the user-visible flow was not re-run
- if exact repro is still unavailable, describe the result as a mitigation or unverified fix, not a confirmed fix

### 1. Release Gate

Run `bash packaging/scripts/run-release-gate.sh`.

This is the minimum bar for code that is about to ship or gate CI:

- packaging metadata validation
- frontend unit tests (`vitest`)
- frontend production build
- built-web output verification for subpath-safe relative assets
- served-under-subpath web smoke for `web.url_base` deployments
- backend unit tests
- backend smoke/regression tests in `tests/slskd.Tests`
- targeted backend integration smoke in `tests/slskd.Tests.Integration`:
  - `LoadTests`
  - `DisasterModeIntegrationTests`
  - `SoulbeetAdvancedModeTests`
  - `CanonicalSelectionTests`
  - `LibraryHealthTests`

See [release-checklist.md](<repo-root>/docs/dev/release-checklist.md) for the operator-facing release steps and what this gate does or does not prove.

### 2. Focused Regression Tests

Every confirmed bug should leave behind one focused automated check when practical:

- frontend bugs: add/update `vitest` coverage near the affected component or lib
- backend persistence/API bugs: add unit or `tests/slskd.Tests` coverage
- legacy data compatibility bugs: add persistence/materialization tests with representative rows
- packaging/build regressions: add script validation or workflow-safe assertions
- tester-reported route or runtime regressions: add a regression that matches the exact production path that failed, not a nearby approximation

### 3. Deeper Integration / E2E

Use the existing integration and Playwright layers for broader end-to-end coverage:

- `tests/slskd.Tests.Integration`
- `src/web/e2e`
- `.github/workflows/e2e-tests.yml`

These are important, but they should complement the release gate rather than replace it.

### 4. Packaging-Specific Smokes

Run targeted package/channel smokes when the change touches packaging:

- `bash packaging/scripts/run-nix-package-smoke.sh`

This validates the current flake package by:

- building `.#default`
- launching the packaged `bin/slskd`
- evaluating a minimal NixOS `services.slskd` configuration with the required `domain`, `environmentFile`, and `settings.shares.directories = [ ]` inputs

## CI Expectations

- `ci.yml` should run the release gate on pull requests.
- `ci.yml` should also run the Nix package smoke on pull requests.
- `build-on-tag.yml` should run the same gate before packaging/publishing artifacts.
- `build-on-tag.yml` should also run the Nix package smoke before publish, and again after stable flake metadata updates when the main-channel package pins change.
- E2E remains separate because it is slower and more environment-sensitive.

## Coverage Priorities

When adding tests, prioritize these surfaces first:

1. startup/auth/session/bootstrap paths
2. packaged web output and `web.url_base` compatibility
3. externally reported repro paths that already escaped once
4. config validation and security boundaries
5. persistence compatibility with legacy SQLite rows
6. release workflow/script logic

## Rule of Thumb

Unstable builds can expose bugs. They should not expose the same bug class twice without the repo gaining an automated check for it.
