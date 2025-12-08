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

