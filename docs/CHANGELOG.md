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

- Bumped the remaining open dependency-update backlog on `main`: `YamlDotNet 17.0.1`, `dotNetRDF 3.5.1`, `OpenTelemetry`/console/OTLP/hosting `1.15.2`, and the web test/build toolchain updates from the open Dependabot PR set (`follow-redirects 1.16.0`, `vite 8.0.8`, `vitest 4.1.4`, `jsdom 29.0.2`, `@types/node 25.6.0`, `@vitest/coverage-v8 4.1.4`, `@microsoft/signalr 7.0.14`).
- Completed the held major-version upgrade work instead of suppressing it: the web app now runs on React 18, React Router 7, ESLint 9 flat config, and the current `@uiw/react-codemirror` line, while the backend and test projects are aligned to `.NET 10`.
- Fixed the migration fallout from that dependency work by updating router usage, restoring passing web lint/build/test runs, and tightening the integration harness so missing external `soulfind` prerequisites fail fast instead of leaving the full-solution test run hanging.
- Rejected loopback `Soulseek.ListenIpAddress` binds for live clients so slskd fails fast instead of logging in successfully while all peer-facing operations (`info`, `browse`, transfers) silently break behind an unreachable advertised endpoint. `Flags.NoConnect = true` still permits loopback for offline/testing scenarios.
- Fixed the real root causes behind the persistent tester reports on `#200` and `#201`: the web service worker was cache-first on navigations and pre-cached the app shell, serving a stale `index.html` that pointed at asset bundle hashes no longer on disk after every rebuild (blank new tabs, 404s on `/assets/*`); it is now network-first for HTML, never caches `/assets/*`, and the shell cache name is bumped so old versions are purged on activate.
- Removed `listenIPAddress` from the startup `SoulseekClientOptionsPatch`. It is already applied via `CreateInitialSoulseekClientOptions`; re-applying it through `ReconfigureOptionsAsync` at startup tore down the `TcpListener` mid-accept and raced `Listener.ListenContinuouslyAsync`, producing the `Not listening. You must call the Start() method before calling this method.` exception and leaving the listener stopped so every inbound peer connection was refused and all transfers failed.
- Wired the existing `GET api/jobs` / `GET api/v{version}/jobs` endpoint to a real production data source. `slskd.API.Native.JobsController` depended on `IJobServiceWithList`, which had no production registration — only a test-harness one — so in production the endpoint always returned zero jobs, which is what the `System/Jobs` Web UI renders as "doesn't load." Added `HashDbJobServiceListAdapter` backed by new `ListDiscographyJobsAsync` / `ListLabelCrateJobsAsync` methods on `IHashDbService`, and registered it in DI.

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
