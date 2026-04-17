# Bugfix Verification Checklist

## Purpose

Use this checklist for any tester-reported or externally reported bug before calling a change "fixed" or cutting a release that claims to address it.

This exists because issues `#200` and `#201` were treated as fixed based on partial local signals while the tester's actual flows were still broken.

## Rule

Do not ship a build that claims to fix a reported bug unless the same failure was either:

- reproduced locally or in an equivalent environment and then disproved after the patch, or
- explicitly documented as an unverified mitigation rather than a confirmed fix

## 1. Capture The Repro Contract

Write down the exact user-visible failure before changing code.

- issue / tester reference
- exact URLs, routes, API paths, or UI clicks involved
- expected result
- actual result
- logs, HTTP status codes, screenshots, or console/network evidence
- environment details that matter:
  - config values
  - auth mode
  - reverse proxy / `web.url_base`
  - package/install path
  - OS / browser / storage / network assumptions

If the report contains multiple symptoms, split them into separate lines now. Do not treat "blank tab + 404s + jobs broken" as one bug.

## 2. Define Acceptance Before The Fix

For each symptom, define the acceptance check in observable terms:

- route returns `200` instead of `404`
- page loads without white screen
- transfer starts instead of stopping at `Connection refused`
- service worker no longer serves stale assets

Do not use internal signals like "exception disappeared" unless that is the user-visible acceptance criterion.

## 3. Reproduce The Failure First

Before tagging or saying "fixed":

- reproduce the exact failing request, page, or workflow locally when practical
- if full repro is not practical, reproduce the narrowest equivalent path and record what remains unverified
- add or update an automated regression test for the reproduced path when practical

If you cannot reproduce it, you may still ship a mitigation, but you must say "attempted mitigation" or "possible fix", not "fixed".

## 4. Verify The Same Path After The Patch

Run the same repro contract again after the change.

- same URL family
- same UI action
- same auth mode
- same packaging or hosted path assumptions
- same relevant logs or HTTP status checks

If one symptom passes and another does not, only mark the passing symptom resolved.

## 5. Release Claim Rules

You may say a bug is fixed only when:

- the repro contract was captured
- the relevant failing path was exercised after the patch
- the acceptance checks passed on that same path
- any still-unverified or still-broken sub-symptoms are called out explicitly

Use this language:

- `Fixed`: reproduced and disproved with matching acceptance checks
- `Mitigated`: one identified cause fixed, but full user path not yet proven
- `Needs confirmation`: plausible fix landed, exact reported path still not reproduced locally

## 6. Minimum Automation Follow-Up

Every bug that reaches a release tag should leave behind one of these when practical:

- focused frontend/unit/integration test for the reported route or component
- release-smoke addition covering the exact production path
- packaging or hosted-path smoke for installer / proxy / subpath behavior
- config validation that blocks the known bad setup earlier

## 7. Pre-Tag Bugfix Gate

Before cutting a bugfix build, answer all of these:

- What exact user-visible failure are we claiming to fix?
- Which exact repro path did we run ourselves?
- Which automated test now covers that path?
- Which sub-symptoms remain unresolved or unverified?

If those answers are missing, the build is not ready to be described as a fix release.
