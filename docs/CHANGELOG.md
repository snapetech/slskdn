# Changelog

All notable changes to slskdN are documented here. GitHub release pages use
[`scripts/generate-release-notes.sh`](../scripts/generate-release-notes.sh),
which prefers the matching version section below and otherwise falls back to
the commit delta since the previous release tag. Tagged releases must never
publish the rolling `## [Unreleased]` section.

Feature and fix work belongs in `## [Unreleased]` when the commit lands. When a
release is cut, move only the shipped bullets into the new versioned section so
each release note reflects the delta from the previous release.

Use headings in this form:

```markdown
## [<version>] — YYYY-MM-DD
```

For dev or build tags, use the same logical version string embedded in the tag.

---

## [Unreleased]

- Rejected loopback `Soulseek.ListenIpAddress` binds for live clients so slskd fails fast instead of logging in successfully while all peer-facing operations (`info`, `browse`, transfers) silently break behind an unreachable advertised endpoint. `Flags.NoConnect = true` still permits loopback for offline/testing scenarios.

## [0.24.5-slskdn.125] — 2026-04-13

- Closed the remaining tester follow-up on issues `#200` and `#201` by fixing the last versioned Web UI/API route gaps, tightening MediaCore and Jobs API versioning, removing the blanket benign `Connection refused` suppression, and covering those production `/api/v0/...` paths in release smoke.
- Removed the unnecessary download enqueue peer preflight that could fail on an auxiliary `Connection refused`, and aligned startup Soulseek option patching so `incomingConnectionOptions` is configured at startup the same way later live reconfigure already does.
- Added Matrix release announcements to the tagged dev and stable release workflow.

## [0.24.5-slskdn.124] — 2026-04-09

- Updated the frontend dependency baseline to `axios 1.15.0` and locked transitive `lodash 4.18.1`, clearing the standing Dependabot bumps and returning `npm audit` in `src/web` to `0` vulnerabilities.

## [0.24.5-slskdn.123] — 2026-04-09

- Finished the earlier issues `#200` and `#201` follow-up by restoring hard-refresh support on client-side routes, versioning the Bridge and Security Web UI/API paths consistently, preserving legacy Bridge compatibility, and moving Soulseek listener bootstrap settings into the initial client options.
- Fixed the release-gate subpath smoke harness so it mirrors the backend HTML rewrite behavior for `web.url_base` deployments instead of enforcing the obsolete relative-asset build model.
- Added Discord release announcements for tagged dev and stable releases, and blocked recurring `axios` / `lodash` Dependabot churn that was reopening the same low-value dependency PRs.
