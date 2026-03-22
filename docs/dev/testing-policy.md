# Testing Policy

## Purpose

slskdn needs one repeatable gate for release-critical behavior, not scattered one-off tests. The goal is to catch the failure classes that have actually hurt releases:

- frontend bootstrap regressions
- subpath hosting / packaged asset regressions
- backend startup and API smoke failures
- release metadata drift

## Test Layers

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

See [release-checklist.md](/home/keith/Documents/code/slskdn/docs/dev/release-checklist.md) for the operator-facing release steps and what this gate does or does not prove.

### 2. Focused Regression Tests

Every confirmed bug should leave behind one focused automated check when practical:

- frontend bugs: add/update `vitest` coverage near the affected component or lib
- backend persistence/API bugs: add unit or `tests/slskd.Tests` coverage
- legacy data compatibility bugs: add persistence/materialization tests with representative rows
- packaging/build regressions: add script validation or workflow-safe assertions

### 3. Deeper Integration / E2E

Use the existing integration and Playwright layers for broader end-to-end coverage:

- `tests/slskd.Tests.Integration`
- `src/web/e2e`
- `.github/workflows/e2e-tests.yml`

These are important, but they should complement the release gate rather than replace it.

## CI Expectations

- `ci.yml` should run the release gate on pull requests.
- `build-on-tag.yml` should run the same gate before packaging/publishing artifacts.
- E2E remains separate because it is slower and more environment-sensitive.

## Coverage Priorities

When adding tests, prioritize these surfaces first:

1. startup/auth/session/bootstrap paths
2. packaged web output and `web.url_base` compatibility
3. config validation and security boundaries
4. persistence compatibility with legacy SQLite rows
5. release workflow/script logic

## Rule of Thumb

Unstable builds can expose bugs. They should not expose the same bug class twice without the repo gaining an automated check for it.
