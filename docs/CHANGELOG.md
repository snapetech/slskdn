# Changelog

All notable changes to slskdN are documented here. Release pages on GitHub use
[`scripts/generate-release-notes.sh`](../scripts/generate-release-notes.sh), which prefers the matching section below, then **Unreleased**, then the commit list since the previous tag.

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

_Add release notes here while developing; move the bullets into a dated version section before tagging._

---
