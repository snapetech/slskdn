# slskdn ↔ Soulbeet API Specification

This document defines:

1. The **compatibility API** slskdn should expose to emulate slskd for Soulbeet.
2. The **slskdn-native APIs** for capabilities and job control.

All endpoints assume:

- Base URL: `SLSKD_URL` (e.g., `http://slskdn:5030`).
- Authentication: `X-API-Key: <SLSKD_API_KEY>` header.
- Content type: `application/json` for request/response bodies.

Field names should ultimately be aligned with actual slskd Swagger and Soulbeet's client structs; below is a concrete proposal.

---

## 1. Compatibility API

These endpoints mimic slskd's API sufficiently for Soulbeet to operate unchanged.

### 1.1. `GET /api/info`

Basic info/health.

**Request**

```http
GET /api/info
X-API-Key: <key>
```

**Response**

```json
{
  "impl": "slskdn",
  "compat": "slskd",
  "version": "0.1.0-multi-swarm",
  "soulseek": {
    "connected": true,
    "user": "your_username"
  }
}
```

---

### 1.2. `POST /api/search`

Perform a Soulseek search.

**Request**

```http
POST /api/search
X-API-Key: <key>
Content-Type: application/json
```

Example body:

```json
{
  "query": "Radiohead Paranoid Android",
  "type": "global",
  "limit": 200
}
```

**Response**

```json
{
  "search_id": "9c9a7d0c-1d53-4fb3-8c16-3d3ad9fabd5f",
  "query": "Radiohead Paranoid Android",
  "results": [
    {
      "user": "some_soulseek_user",
      "speed_kbps": 420,
      "files": [
        {
          "path": "Radiohead/OK Computer/Paranoid Android.flac",
          "size_bytes": 41234567,
          "bitrate": 900,
          "length_ms": 388000,
          "ext": "flac"
        }
      ]
    }
  ]
}
```

Implementation notes:

* Internally map `query` + `type` to Soulseek search.
* Optionally enrich/filter results using MBID/fingerprint knowledge.

---

### 1.3. `POST /api/downloads`

Create one or more downloads based on search results.

**Request**

```http
POST /api/downloads
X-API-Key: <key>
Content-Type: application/json
```

Example body:

```json
{
  "items": [
    {
      "user": "some_soulseek_user",
      "remote_path": "Radiohead/OK Computer/Paranoid Android.flac",
      "target_dir": "/app/downloads",
      "target_filename": "Radiohead - Paranoid Android.flac"
    }
  ]
}
```

**Response**

```json
{
  "download_ids": [
    "dwn_01HRQ3VJXG3E0VX6Y1D1JFGX1K"
  ]
}
```

Implementation mapping:

* Map each `item` to internal transfer(s)/job(s) in slskdn.
* Initially:
  * Basic mapping to a single Soulseek transfer per item.
* Later:
  * Optionally aggregate into MBID release jobs behind the scenes.

---

### 1.4. `GET /api/downloads`

List active/known downloads.

**Request**

```http
GET /api/downloads
X-API-Key: <key>
```

**Response**

```json
{
  "downloads": [
    {
      "id": "dwn_01HRQ3VJXG3E0VX6Y1D1JFGX1K",
      "user": "some_soulseek_user",
      "remote_path": "Radiohead/OK Computer/Paranoid Android.flac",
      "local_path": "/app/downloads/Radiohead - Paranoid Android.flac",
      "status": "completed",        // queued | running | completed | failed | cancelled
      "progress": 1.0,
      "bytes_total": 41234567,
      "bytes_transferred": 41234567,
      "error": null
    }
  ]
}
```

Optional: `GET /api/downloads/{id}` returns a single object with the same shape.

Implementation notes:

* `status` should map to internal job/transfer states.
* `local_path` must be a path visible inside Soulbeet container via volume mapping.

---

---

### 1.5. `GET /api/musicbrainz/albums/completion`

Expose album completion summaries derived from HashDb so the UI can highlight missing tracks or already-complete releases.

**Request**

```http
GET /api/musicbrainz/albums/completion
X-API-Key: <key>
```

**Response**

```json
{
  "albums": [
    {
      "releaseId": "c0d0c0a4-4a26-4d74-9c02-67c9321b3b22",
      "title": "Loveless",
      "artist": "My Bloody Valentine",
      "releaseDate": "1991-11-04",
      "discogsReleaseId": "123456",
      "totalTracks": 11,
      "completedTracks": 8,
      "tracks": [
        {
          "position": 1,
          "title": "Only Shallow",
          "recordingId": "e2f5e9b4-5852-4cd3-b1f9-29a7a4a234bc",
          "durationMs": 242000,
          "complete": true,
          "matches": [
            {
              "flacKey": "1a2b3c4d5e6f7g8h",
              "size": 34567890,
              "useCount": 3,
              "firstSeenAt": 1700000000,
              "lastUpdatedAt": 1700000500
            }
          ]
        },
        {
          "position": 2,
          "title": "Loomer",
          "recordingId": "d1f5b9c4-1234-4edc-aabc-7a2b3c4d5e6f",
          "durationMs": 178000,
          "complete": false,
          "matches": []
        }
      ]
    }
  ]
}
```

Soulbeet can surface this data to warn about missing tracks, show completion ratios, and highlight which HashDb entries already satisfy each recording.

---

## 2. slskdn-Native API: Capabilities & Jobs

These endpoints expose slskdn's advanced features. They do not exist in vanilla slskd.

### 2.1. `GET /api/slskdn/capabilities`

Allow clients (Soulbeet) to detect slskdn and supported features.

**Request**

```http
GET /api/slskdn/capabilities
X-API-Key: <key>
```

**Response**

```json
{
  "impl": "slskdn",
  "version": "0.1.0-multi-swarm",
  "features": [
    "mbid_jobs",
    "discography_jobs",
    "label_crate_jobs",
    "canonical_scoring",
    "rescue_mode",
    "library_health",
    "warm_cache"
  ]
}
```

Client behaviour:

* If this endpoint 404s → assume slskd, use compat API only.
* If present and `impl = slskdn` → enable advanced features.

---

## 3. Job APIs

All jobs share a common representation:

### 3.1. Common Job Representation

```json
{
  "id": "job_01HRQ4GSX9K47J7M5FK7VW8B12",
  "type": "mb_release",        // mb_release | discography | label_crate | (others later)
  "status": "running",         // pending | running | completed | failed | cancelled
  "created_at": "2025-12-09T23:10:00Z",
  "updated_at": "2025-12-09T23:11:12Z",
  "spec": {                    // type-specific spec (see below)
    "...": "..."
  },
  "progress": {
    "releases_total": 1,
    "releases_done": 0,
    "tracks_total": 10,
    "tracks_done": 4,
    "bytes_total": 512000000,
    "bytes_done": 256000000
  },
  "error": null
}
```

### 3.2. `POST /api/jobs/mb-release`

Create a job to download an MB Release.

**Request**

```http
POST /api/jobs/mb-release
X-API-Key: <key>
Content-Type: application/json
```

Example body:

```json
{
  "mb_release_id": "c0d0c0a4-4a26-4d74-9c02-67c9321b3b22",
  "target_dir": "/app/downloads/queue/admin",
  "tracks": "all",                // or array of track numbers or MB recording IDs
  "constraints": {
    "preferred_codecs": ["FLAC"],
    "allow_lossy": false,
    "prefer_canonical": true,
    "use_overlay": true,
    "overlay_bandwidth_kbps": 6000
  },
  "metadata": {
    "requested_by": "soulbeet:user:admin",
    "library_hint": "/music/admin"
  }
}
```

**Response**

```json
{
  "job_id": "job_01HRQ4GSX9K47J7M5FK7VW8B12",
  "status": "pending"
}
```

Implementation notes:

* slskdn:
  * Fetches MB tracklist for `mb_release_id`.
  * Plans per-track multi-swarm downloads.
  * Writes tracks to `target_dir`.

* Soulbeet:
  * Polls `GET /api/jobs/{id}` until `status = completed`.
  * Then runs Beets import on `target_dir`.

---

### 3.3. `POST /api/jobs/discography`

Create an artist discography job.

**Request**

```http
POST /api/jobs/discography
X-API-Key: <key>
Content-Type: application/json
```

Example body:

```json
{
  "mb_artist_id": "a74b1b7f-71a5-4011-9441-d0b5e4122711",
  "profile": "core",           // core | extended | all
  "target_dir": "/app/downloads/queue/admin",
  "constraints": {
    "preferred_codecs": ["FLAC"],
    "allow_lossy": false,
    "prefer_canonical": true,
    "use_overlay": true
  }
}
```

**Response**

```json
{
  "job_id": "job_01HRQ4R5J0X2D41EXT21J6EGVE",
  "status": "pending"
}
```

Implementation notes:

* slskdn:
  * Resolves discography profile from MB release graph.
  * Spawns `mb_release` sub-jobs for each release.
  * Aggregates progress into the parent discography job.

* Soulbeet:
  * Uses `GET /api/jobs/{id}` to show "X/Y releases complete".
  * Triggers Beets imports as individual releases finish.

---

### 3.4. `POST /api/jobs/label-crate`

Create a label crate job.

**Request**

```http
POST /api/jobs/label-crate
X-API-Key: <key>
Content-Type: application/json
```

Example body:

```json
{
  "label": {
    "mb_label_id": "f5bb60d4-cc90-4e30-911b-7c0cfdff1109",
    "name": "Warp Records"
  },
  "limit_releases": 20,
  "target_dir": "/app/downloads/queue/admin",
  "constraints": {
    "preferred_codecs": ["FLAC"],
    "allow_lossy": false,
    "prefer_canonical": true,
    "use_overlay": true
  }
}
```

**Response**

```json
{
  "job_id": "job_01HRQ4TA49K1350EF4KNR8MD6E",
  "status": "pending"
}
```

Implementation notes:

* slskdn:
  * Uses mesh metadata and/or MB Label API to determine popular releases on that label.
  * Spawns `mb_release` sub-jobs for the top `limit_releases`.

* Soulbeet:
  * Shows crate progress ("8/20 releases complete").
  * Imports as releases finish.

---

### 3.5. `GET /api/jobs` and `GET /api/jobs/{id}`

List or inspect jobs.

**List jobs**

```http
GET /api/jobs?type=mb_release&status=running
X-API-Key: <key>
```

**Response**

```json
{
  "jobs": [
    {
      "id": "job_01HRQ4GSX9K47J7M5FK7VW8B12",
      "type": "mb_release",
      "status": "running",
      "spec": {
        "mb_release_id": "c0d0c0a4-4a26-4d74-9c02-67c9321b3b22",
        "target_dir": "/app/downloads/queue/admin"
      },
      "progress": {
        "tracks_total": 10,
        "tracks_done": 4,
        "bytes_total": 512000000,
        "bytes_done": 256000000
      },
      "created_at": "2025-12-09T23:10:00Z",
      "updated_at": "2025-12-09T23:11:12Z",
      "error": null
    }
  ]
}
```

**Single job**

```http
GET /api/jobs/job_01HRQ4GSX9K47J7M5FK7VW8B12
X-API-Key: <key>
```

**Response**

Same object as in the list, possibly with additional detail (per-track status, etc).

---

## 4. Optional slskdn-specific APIs

These do not need to be consumed by Soulbeet immediately but are useful for advanced integrations.

### 4.1. Warm cache hints

**Request**

```http
POST /api/slskdn/warm-cache/hints
X-API-Key: <key>
Content-Type: application/json
```

Example body:

```json
{
  "mb_release_ids": [
    "c0d0c0a4-4a26-4d74-9c02-67c9321b3b22"
  ],
  "mb_artist_ids": [
    "a74b1b7f-71a5-4011-9441-d0b5e4122711"
  ],
  "mb_label_ids": [
    "f5bb60d4-cc90-4e30-911b-7c0cfdff1109"
  ]
}
```

**Response**

```json
{
  "accepted": true
}
```

slskdn uses this as a hint for warm caching; it does not guarantee behaviour.

---

### 4.2. Library health summary (path-scoped)

**Request**

```http
GET /api/slskdn/library/health?path=/music/admin
X-API-Key: <key>
```

**Response (shape example)**

```json
{
  "path": "/music/admin",
  "summary": {
    "suspected_transcodes": 143,
    "non_canonical_variants": 57,
    "incomplete_releases": 27
  },
  "issues": [
    {
      "type": "SuspectedTranscode",
      "file": "/music/admin/Artist/Album/Track01.flac",
      "mb_recording_id": "e2f5e9b4-5852-4cd3-b1f9-29a7a4a234bc",
      "reason": "Spectral analysis suggests lossy source at 128kbps"
    }
  ]
}
```

Soulbeet can surface this in its UI and offer "Fix via multi-swarm" actions that call job APIs.

---

This spec provides enough detail for:

* Implementing the slskdn HTTP layer.
* Modifying Soulbeet to detect and exploit slskdn advanced features.
* Maintaining backwards compatibility with existing slskd-expecting deployments.


