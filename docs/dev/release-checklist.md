# Release Checklist

## Purpose

This is the minimum release-readiness checklist for `slskdn`.

Green local validation means the tree is a viable release candidate. It does not replace the tag-triggered CI build/publish path.

## Minimum local release bar

Run this from repo root:

```bash
bash packaging/scripts/run-release-gate.sh
bash ./bin/lint
```

If the release claims to fix a reported bug, also run the reproduce-first workflow in [bugfix-verification-checklist.md](bugfix-verification-checklist.md). Green generic validation is not enough on its own.

The release gate now covers:

- packaging metadata validation
- frontend unit tests
- frontend production build
- built-web output verification
- served-under-subpath web smoke (`/slskd/`)
- backend unit tests
- backend smoke/regression tests
- targeted backend integration smoke tests:
  - `LoadTests`
  - `DisasterModeIntegrationTests`
  - `SoulbeetAdvancedModeTests`
  - `CanonicalSelectionTests`
  - `LibraryHealthTests`

## What green local validation proves

- the repo builds
- the release gate passes on the current tree
- critical packaged-web and startup/API smoke paths are covered
- a small release-surface integration slice is working

## What it does not prove

- tag-triggered packaging/publish workflows succeeded
- every platform package installed cleanly
- every slow or environment-sensitive E2E path is green
- a tester-reported bug is fixed unless the same reported path was re-run successfully

## Release flow

1. Get the tree green locally:
   - `bash packaging/scripts/run-release-gate.sh`
   - `bash ./bin/lint`
2. For any build that claims to fix a reported issue:
   - capture the repro contract
   - split multi-symptom reports into separate acceptance checks
   - re-run the same path after the patch
   - identify any symptoms that remain unverified
3. Push the code branch normally if needed.
4. Only trigger build/release by creating the appropriate tag when explicitly desired.

Do not rely on a normal branch push to validate packaging or publish artifacts. This repo builds releases on tags.

## Recommended extra checks for risky changes

Run these when the change touches the relevant surface:

- Packaging/workflow changes:
  - `bash packaging/scripts/validate-packaging-metadata.sh`
  - `bash packaging/scripts/run-nix-package-smoke.sh`
- Frontend hosting/base-path changes:
  - `npm --prefix src/web run build`
  - `node src/web/scripts/verify-build-output.mjs`
  - `node src/web/scripts/smoke-subpath-build.mjs`
- Browser/user-journey changes:
  - use the existing Playwright workflow or local E2E smoke
- Packaging or distro-specific changes:
  - perform the relevant platform install smoke before tagging

## Release decision rule

Do not call a build "release-ready" unless:

- local release gate is green
- lint is green
- any claimed bugfix has passed its issue-specific repro/acceptance checks, or is clearly labeled as an unverified mitigation
- no known release-blocking packaging issue is open for the touched platform
- the tag-triggered build is the next intended step
