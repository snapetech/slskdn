# slskdn Dev Build Release Description

**Branch:** `chore/slop-reduction` (based on `experimental/multi-source-swarm`)  
**Build Timestamp:** `2025-12-08` (local smoke test)  
**Purpose:** Capture the fixes rolled into this dev build and highlight how the experimental features compare to `main`.

## Fixes since the previous dev build

1. **Multi-Source download hygiene**
   - Standardized logging and naming inside `MultiSourceDownloadService` so the injected `_logger`, `_client`, `_hashDb`, and `_meshSync` fields are used consistently for observability.
   - Added a semaphore to bound concurrent retry workers (max 10) to avoid resource contention during chunk retries.
   - Hardened speculative chunk execution and peer timeouts as documented in the multi-source pipeline.
2. **Filesystem security**
   - Ensured `FileService.DeleteDirectoriesAsync` and `DeleteFilesAsync` reject non-absolute paths and gracefully refuse attempts to delete configured roots.
   - Introduced `FileServiceSecurityTests` that assert path traversal and root-deletion attempts now raise exceptions.
3. **Backfill scheduler realism**
   - Replaced simulated header reads with actual `ContentVerificationService` calls and simplified flare logs so backfills publish hashes to the mesh and hash DB exactly like the real system.
4. **Frontend lint and status stability**
   - Cleaned the UI lint failures in `Security/index.jsx`, `UserContextMenu.jsx`, and `userNotes.js` so the dev build compiles without ESLint/Prettier errors, restoring the status/security views seen in the last release.

## Features: Dev build vs `main`

| Feature area | This dev build (`chore/slop-reduction` / `experimental/multi-source-swarm`) | Official `main` branch |
|--------------|-----------------------------------------------------------------------------|-------------------------|
| **Multi-Source Swarm** | Full chunked swarm downloads, content verification (SHA256), discovery DB integration, work-stealing, peer timeouts, status tracking, background jobs | Single-source downloads only; no swarm splitting or verification |
| **Peer discovery** | DHT rendezvous, mesh overlay, epidemic hash sync, capability detection (DHT/hash exchange), CONNECT_ASSIST signaling, backfill scheduler | Traditional Soulseek search/discovery without mesh sync or DHT overlays |
| **Security hardening** | NetworkGuard rate limits, PeerReputation scoring, PathGuard checks, ViolationTracker honeypots, entropy/fingerprint monitoring, multi-source consensus controls | Upstream security is limited to the legacy Soulseek protections |
| **UI surface** | Karma badge + status bar, security dashboard, footer links, CLI discovery tooling (`test-swarm.sh` improvements) | Standard upstream UI without these slskdn extensions |
| **Documentation** | Release-specific guide (`docs/RELEASE_DESCRIPTION.md`), detailed multi-source/security design docs, TODO cleanup | Base README and docs without slskdn sections |

> **Note:** This build is experimental and not intended for production use. Use the stable `main` branch for upstream-compatible releases.

---

## Release .19 – `dev-2025-12-09`

**Branch/tag:** `experimental/multi-source-swarm` / `dev-2025-12-09`  
**Ship date:** 2025-12-09  
**Purpose:** Deliver the HashDb/passive hash discovery work, UI polish, and documentation/migration patterns that make the mesh-enabled stack testable while keeping client UX consistent.

### Highlights

1. **HashDb maturity**
   - Passive FLAC discovery now ingests search responses plus peers who search or download us, with `PeerSearchedUsEvent`/`PeerDownloadedFromUsEvent` lifting usernames and shares into `FlacInventory`.
   - Schema migrations handled via `HashDbMigrations.cs`, plus `/hashdb/backfill/from-history` endpoint with pagination, progress tracking, reset, and schema/version/export helpers.
2. **UI and network health**
   - Sticky, fixed status bar below the nav and an opaque, colorful footer (including “built on the most excellent slskd” text) on every page (login included), plus the tooltip/button convention documented in the memory bank.
   - Network dashboard gains a backfill-from-history button with progress display and reset, and release notes now remind us to document every tagged build.
3. **Docs + memories**
   - Added `docs/HASHDB_SCHEMA.md`, reinforced the network health/UI tooltip guidance in `ADR-0002`, recorded the pre-compile gotcha in `ADR-0001`, and created a persistent memory to require release notes with each tagged build.

### Tests

- `npm run build` (frontend)
- Manual `dotnet run` restart so the rebuilt assets (copied into both `src/slskd/wwwroot` and `bin/Debug/net8.0/wwwroot`) are served by the development server.

### Notes

- Tag `dev-2025-12-09` now exists on GitHub and should display this updated `docs/RELEASE_DESCRIPTION.md` when the release is published. Run the CI release workflow to attach `/tmp/release-notes.md` if you need the automated format.

