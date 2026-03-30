# Changelog

All notable changes to slskdN are documented here. Release pages on GitHub use
[`scripts/generate-release-notes.sh`](../scripts/generate-release-notes.sh), which prefers the matching section below, then **Unreleased**, then the commit list since the previous tag.

Feature/fix work belongs in `## [Unreleased]` when the commit lands, not later when a release is being cut. PR CI and the local pre-commit hook now block release-worthy changes if `## [Unreleased]` was not updated.

Use headings in this form (date optional but helps match the generator):

```markdown
## [0.24.5-slskdn.72] — 2026-03-19
```

For dev / build tags, use the same string as `needs.parse.outputs.version` (the version embedded in `build-dev-*` / `build-main-*` tags).

---

## [0.24.5-slskdn.103] — 2026-03-28

- Fixed packaged installs so the documented `/etc/slskd/slskd.yml` is actually authoritative and the default Web UI path centers on plain HTTP `:5030` instead of an ambiguous `5030`/`5031` split.
- Hardened SongID YouTube handling so missing `yt-dlp` degrades to metadata-only analysis instead of failing the run, and updated packaging so supported installs include `yt-dlp` by default.
- Restored broken SongID handoff actions like `Plan Discography` and album planning by fixing the native jobs API payload casing in the web client.
- Fixed Search page batch actions so multi-search creation retries the backend's serialized-create `429` instead of failing when several searches are queued together.
- Made every top-level Search page panel collapsible and left `Search Results` expanded by default so active searches and results stay reachable on long pages.
- Prevented SongID runs from stalling indefinitely at `artist_graph` by time-boxing deep MusicBrainz artist release-graph expansion and falling back to lightweight artist candidates.
- Tightened SongID-generated search actions so they use canonical `Artist - Track` queries instead of concatenating uploader, album, duplicate title, and other metadata noise into Soulseek searches.

## [Unreleased]

- Fixed the remaining open `SessionController` CodeQL login alert by moving admin credential verification behind the security service, and updated CodeQL to scan the live `main` branch so fixes on the release branch clear instead of lingering.
- Grouped non-breaking Dependabot updates by ecosystem to collapse the release-time dependency PR flood into a small set of batched update PRs.
- Folded the remaining safe dependency bumps directly into `main` for `Serilog.Sinks.Console`, `OpenTelemetry.Exporter.OpenTelemetryProtocol`, and `OpenTelemetry.Extensions.Hosting`, leaving only the incompatible React 17 CodeMirror update out of band.
- Reverted the breaking `Swashbuckle.AspNetCore 10.x` auto-upgrade to `6.6.2` and blocked future major-version Dependabot PRs for that package until the OpenAPI integration is migrated deliberately.
- Merged the previously detached `build-main-0.24.5-slskdn.92` through `.101` history back into `main`, restoring the missing Docker startup hardening, release/packaging fixes, runtime lifecycle fixes, and expanded unit/integration coverage that had been built on tags but never merged.
- Downgraded expected Soulseek peer and distributed-network unobserved task exceptions from fake process-fatal telemetry to warning-level noise so normal P2P timeout/refusal churn no longer looks like a daemon crash.
- Fixed the Docker image default HTTP bind address so published container ports now serve the Web UI from outside the container instead of binding Kestrel to loopback-only `127.0.0.1`.
- Synced the checked-in stable package metadata back to the latest published stable release `0.24.5-slskdn.105` so release gating and downstream package manifests stop disagreeing about the current baseline.
- Hardened Launchpad PPA uploads in the release workflows by enabling passive FTP and bounded retry so transient FTP-side failures stop breaking otherwise-valid stable/dev package publishes.
- Fixed the stable release metadata workflow so it emits the full checksum set expected by `update-stable-release-metadata.sh`, restoring the `Update Main Repo Metadata` job for tagged releases.

---
