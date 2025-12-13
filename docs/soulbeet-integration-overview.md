# slskdn ↔ Soulbeet Integration Overview

## 1. Goals

Soulbeet is a multi-user frontend that:

- Uses slskd's HTTP API to:
  - Search via Soulseek.
  - Create downloads to a shared downloads directory.
- Monitors that directory and uses `beet import` (Beets) to:
  - Tag and organise music into per-user libraries.

slskdn's multi-swarm branch:

- Extends slskd with:
  - MBID/fingerprint-aware jobs.
  - Multi-source swarming over an overlay mesh.
  - Canonical edition selection and library health.

Integration goals:

1. **Compat mode:** slskdn can act as a drop-in replacement for slskd for Soulbeet.
2. **Advanced mode:** Soulbeet can use slskdn-specific job APIs (`/api/jobs/*`) when available:
   - MBID-based album jobs.
   - Artist discography jobs.
   - Label crate jobs.
3. **Shared responsibilities:**
   - Soulbeet: UI, multi-user management, MB search, Beets integration.
   - slskdn: Soulseek connectivity, multi-swarm, quality/canonical logic, and mesh overlay.

---

## 2. Architecture

### 2.1. Services and volumes (Docker context)

Typical deployment:

- `slskdn` container:
  - Provides Soulseek connectivity and HTTP API (compat + slskdn-native).
  - Has its own config and downloads directory.

- `soulbeet` container:
  - Talks to `slskdn` via HTTP (using `SLSKD_URL` and `SLSKD_API_KEY`).
  - Shares:
    - Downloads directory (for completed files).
    - Music library path(s) (for Beets imports).

Example volume sharing (conceptual):

```yaml
services:
  slskdn:
    image: snapetech/slskdn:experimental-multi-swarm
    volumes:
      - ./slskd-config:/app/slskd.conf.d
      - /srv/slskd/downloads:/app/downloads

  soulbeet:
    image: soulbeet/soulbeet:latest
    environment:
      - SLSKD_URL=http://slskdn:5030
      - SLSKD_API_KEY=...
      - SLSKD_DOWNLOAD_PATH=/downloads
    volumes:
      - /srv/slskd/downloads:/downloads
      - /srv/music:/music
```

Soulbeet expects a "slskd-like" HTTP API at `SLSKD_URL`. slskdn provides:

* A **compatibility layer** for slskd endpoints.
* Additional `/api/slskdn/*` and `/api/jobs/*` endpoints.

---

## 3. Modes of Operation

### 3.1. Compat Mode (no Soulbeet changes required)

In compat mode:

* Soulbeet continues to:
  * Use slskd-style endpoints (`/api/search`, `/api/downloads`, etc.).
  * Issue track or album-level search queries.
  * Create download tasks into the shared downloads folder.
  * Run Beets imports when downloads complete.

slskdn's compat layer:

* Implements just the subset of slskd HTTP API that Soulbeet uses.
* Internally:
  * Can initially map these to classic Soulseek transfers.
  * Can later map them to MBID-aware multi-swarm jobs.

Benefits:

* Upgrading from slskd → slskdn is a matter of swapping the backend container.
* Soulbeet remains unaware of multi-swarm complexity; it sees the same interface.

### 3.2. Advanced Mode (slskdn features exposed)

When Soulbeet detects slskdn (via a capabilities endpoint), it can:

* Use **MBID-based job APIs**:
  * `/api/jobs/mb-release` for album jobs.
  * `/api/jobs/discography` for artist discographies.
  * `/api/jobs/label-crate` for label crates.

* Expose advanced options in the UI:
  * Prefer canonical masters.
  * Lossless only vs allow lossy.
  * Use overlay / Soulseek-only.

* Still rely on:
  * A standard downloads directory for completed content.
  * Beets for tagging/import.

Detection:

* Soulbeet calls:
  ```http
  GET /api/slskdn/capabilities
  ```

* If it gets a structured response (`impl = slskdn`, feature list), it enables advanced mode.
* If the endpoint is missing/404, it falls back to compat mode.

---

## 4. Conceptual Flow: Compat Mode

1. **User searches in Soulbeet UI**
   * Soulbeet queries external MB/Discogs APIs for high-quality metadata.
   * Soulbeet builds a search string from MB metadata (e.g., `"Artist Album"` or `"Artist Track"`).

2. **Soulbeet → slskdn (compat `/api/search`)**
   * slskdn performs a Soulseek search.
   * slskdn may optionally use overlay mesh metadata to:
     * Rank results.
     * De-duplicate weird variants.
   * slskdn returns results in the shape Soulbeet expects.

3. **User selects album/track(s)**
   * Soulbeet sends a compat `/api/downloads` request:
     * Includes remote paths, target download dir.

4. **slskdn handling**
   * Initially:
     * Start classic Soulseek transfers for requested paths.
   * Future:
     * Infer MBIDs from tags/fingerprints and:
       * Convert into MBID release jobs and multi-swarm downloads.

5. **Completion and import**
   * slskdn marks downloads complete in `/api/downloads` status.
   * Soulbeet detects completion and:
     * Calls `beet import` on the downloads.
     * Moves/tag files into user libraries (`/music/...`).

---

## 5. Conceptual Flow: Advanced Mode (MBID Jobs)

### 5.1. MB Release job (Album download)

1. **User chooses album in Soulbeet**
   * Soulbeet knows `mb_release_id` from its own MB query.

2. **Soulbeet → slskdn: `POST /api/jobs/mb-release`**
   * Body includes:
     * `mb_release_id`
     * `target_dir` (downloads folder for that user/session)
     * Track subset if needed.
     * Constraints (lossless, canonical, overlay).

3. **slskdn: plan & execute job**
   * Fetch MB tracklist for the release.
   * Use Soulseek search + overlay mesh to:
     * Locate suitable sources for each track/recording.
     * Prefer canonical variants.
   * Multi-swarm:
     * Use overlay chunking + rescue mode as needed.
   * Write files into `target_dir`.

4. **Soulbeet polls `/api/jobs/{id}`**
   * Shows progress to user.
   * When `status = completed`:
     * Runs Beets import for `target_dir`.

### 5.2. Discography job

1. **User selects "Download discography" for an artist**
   * Soulbeet knows `mb_artist_id`.

2. **Soulbeet → slskdn: `POST /api/jobs/discography`**
   * Includes:
     * `mb_artist_id`, `profile` (core/extended/all).
     * `target_dir`.
     * Constraints.

3. **slskdn**
   * Resolves discography profile from MB release graph.
   * Creates sub-jobs for each MB Release.
   * Tracks aggregated progress.

4. **Soulbeet**
   * Shows total progress ("X/Y releases downloaded").
   * Imports newly completed albums as they land.

### 5.3. Label crate job

1. **User enters or selects label (e.g., Warp Records)**
   * Soulbeet may display labels based on metadata from slskdn or its own sources.

2. **Soulbeet → slskdn: `POST /api/jobs/label-crate`**
   * Includes label name/MB Label ID, limit, target_dir, constraints.

3. **slskdn**
   * Computes popular releases for the label using:
     * Overlay metadata (how often MBIDs appear).
     * Possibly local preferences.
   * Spawns `mb_release` sub-jobs for top N releases.

4. **Soulbeet**
   * Shows a crate job with progress.
   * Imports as jobs complete.

---

## 6. Advanced Cross-Feeding Ideas

### 6.1. Soulbeet → slskdn: Warm cache hints

Soulbeet sees:

* What users search for and request **in real time**.

slskdn's warm cache module needs:

* A signal of which MBIDs are "hot".

Integration:

* Soulbeet can POST simple "demand hints" to slskdn:
  ```http
  POST /api/slskdn/warm-cache/hints
  {
    "mb_release_ids": ["...", "..."],
    "mb_artist_ids": ["..."],
    "mb_label_ids": ["..."]
  }
  ```

* slskdn uses hints to bump popularity scores when deciding what to warm-cache.

### 6.2. slskdn → Soulbeet: Library health

slskdn's Collection Doctor can expose:

* Issues per file/path (suspected transcodes, missing tracks).

Soulbeet:

* Already has per-user library views.
* Could query slskdn for health status:
  ```http
  GET /api/slskdn/library/health?path=/music/admin
  ```

* Show issues alongside the UI:
  * "This album is missing track N."
  * "This track is suspected to be a transcode."

From there, "Fix via multi-swarm" actions can simply call `/api/jobs/mb-release` with the right constraints.

---

## 7. Summary

* In **compat mode**, slskdn impersonates slskd just enough for Soulbeet to work unchanged.

* In **advanced mode**, Soulbeet:
  * Detects slskdn capabilities.
  * Uses MBID job APIs directly.
  * Still delegates tagging/import to Beets.

* Responsibilities:
  * Soulbeet remains the UX, user management, and metadata orchestrator.
  * slskdn remains the Soulseek-side engine, multi-swarm coordinator, and quality/canonical brain.

See `soulbeet-api-spec.md` for the concrete HTTP endpoints and payloads.

















