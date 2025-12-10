# slskdn Multi-Swarm Architecture (Experimental Branch)

## 1. Objectives

The experimental/multi-swarm branch aims to turn slskdn into a "next-generation" Soulseek client that:

- Keeps **Soulseek** as the **primary network** for search, social graph, and origin traffic.

- Adds a **mesh overlay** between slskdn nodes, discovered via BitTorrent DHT, to:
  - Improve throughput via multi-source, chunk-based swarming.
  - Help in NAT/CGNAT scenarios by using public "beacon" nodes as relays.

- Becomes **content-aware** via:
  - Acoustic fingerprinting (e.g. Chromaprint/AcoustID).
  - MusicBrainz/Discogs IDs (MB Recording ID, MB Release ID, Label ID).

- Provides higher-level features:
  - Canonical-edition selection ("best version" scoring).
  - Library health / "Collection Doctor".
  - Rescue mode for stalled Soulseek transfers.
  - Optional warm caching and playback-aware swarming.

The design is explicitly **augmentative**:

> Soulseek remains the origin and authority; the overlay adds optimisation and resilience, never replaces it.

---

## 2. Layered Architecture

### 2.1. Layers

1. **Soulseek layer (unchanged protocol)**  
   - Handles:
     - Search, rooms, messaging.
     - User lists.
     - File transfer slots and queues.
   - slskdn participates as a regular Soulseek client.

2. **Overlay mesh layer (slskdn-only)**  
   - Uses BitTorrent DHT for rendezvous.
   - Establishes TLS-protected TCP connections between slskdn peers.
   - Runs a JSON-framed protocol carrying:
     - Mesh discovery and capabilities.
     - Fingerprint/MBID metadata.
     - Multi-swarm control messages.
     - Chunk transfer data (request/response/cancel).

3. **Metadata / intelligence layer**  
   - Maintains:
     - Acoustic fingerprints → MB Recording/Release IDs.
     - Canonical quality scores per recording/release.
     - Library health information.
   - Drives:
     - Source selection.
     - Discography/label-crate jobs.
     - Rescue mode decisions.

---

## 3. Core Concepts and Data Models

### 3.1. MB/Discogs aware model

Key IDs:

- `mb_recording_id` – MusicBrainz Recording ID.
- `mb_release_id` – MusicBrainz Release ID.
- `mb_release_group_id` – Release Group ID.
- `mb_label_id` – Label ID, optional.
- `acoustid` – Acoustic fingerprint ID.

Local model (conceptual):

```text
Recording           (mb_recording_id, title, artist, duration, etc.)
Release             (mb_release_id, title, artist, date, country, label, release_group_id)
TrackInRelease      (mb_release_id, track_number, mb_recording_id, duration)
AudioVariant        (local file "variant" of a recording)
```

`AudioVariant` includes:

* Codec/container (`codec`, `container`).
* Technical params (`sample_rate_hz`, `bit_depth`, `channels`, `duration_ms`, `bitrate_kbps`, `file_size_bytes`).
* Integrity (`file_hash_sha256`).
* Analysis (`quality_score`, `transcode_suspect`, DR/loudness if available).

### 3.2. Jobs, variants, transfers

Terminology:

* **Job** – high-level download unit:
  * `mb_release` job: "Get this MB Release to target_dir under constraints."
  * `discography` job: set of `mb_release` jobs for an artist.
  * `label_crate` job: set of `mb_release` jobs for a label.

* **Variant** – one concrete local file of a recording (identified by `variant_id`).

* **Transfer** – data flow for `(job_id, variant_id, peer)` over overlay; identified by `transfer_id`.

The multi-swarm engine runs **many transfers in parallel** to complete jobs.

---

## 4. Overlay Mesh Protocol

Overlay messages are line-delimited JSON with a common envelope:

```jsonc
{
  "type": "mbid_swarm_descriptor",  // or other message type
  "version": 1,
  "message_id": "8c6c43b8-65a8-4b0d-8f4d-7b02bdc1e2b1",
  "sender_id": "mesh-node-abc123",
  "timestamp": "2025-12-09T23:12:45.123Z",
  "payload": { /* type-specific */ }
}
```

### 4.1. MBID swarm descriptor

Purpose: describe what releases/recordings this peer can serve and under what policies.

Payload (simplified):

```jsonc
{
  "mb_release_id": "c0d0c0a4-4a26-4d74-9c02-67c9321b3b22",
  "mb_release_group_id": "6f3f9b48-f1e2-4269-9b4b-2ddc93d1ff77",
  "discogs_release_id": 123456,
  "title": "Loveless",
  "artist": "My Bloody Valentine",
  "edition_profile": {
    "label": "Sire",
    "catalog_number": "9 26840-2",
    "release_country": "US",
    "release_date": "1991-11-04",
    "medium_format": "CD",
    "lossless": true,
    "codec": "FLAC",
    "sample_rate_hz": 44100,
    "bit_depth": 16,
    "channels": 2
  },
  "availability": {
    "has_full_album": true,
    "tracks": [
      {
        "track_number": 1,
        "title": "Only Shallow",
        "mb_recording_id": "e2f5e9b4-5852-4cd3-b1f9-29a7a4a234bc",
        "duration_ms": 242000,
        "file_size_bytes": 34567890,
        "codec": "FLAC",
        "bitrate_kbps": 900,
        "lossless": true,
        "quality_score": 0.96,
        "availability_state": "complete"
      }
    ]
  },
  "policies": {
    "prefer_soulseek_primary": true,
    "max_overlay_upload_slots": 2,
    "max_overlay_bandwidth_kbps": 2000,
    "max_concurrent_overlay_peers": 4,
    "allow_relay_from_soulseek": true
  }
}
```

Used in:

* Peer discovery of who can help with a given MB release.
* Enforcing peer-specific fairness and capacity.

### 4.2. Fingerprint bundle advert

Purpose: compactly describe what recordings and variants this peer has, including quality info.

Payload (simplified):

```jsonc
{
  "bundle_id": "4d8dbf6b-0a49-4d53-9e36-435b0b66391d",
  "sequence": 12,
  "full_snapshot": false,
  "recordings": [
    {
      "mb_recording_id": "e2f5e9b4-5852-4cd3-b1f9-29a7a4a234bc",
      "acoustid": "5180bfc0-93b9-4c3b-9a51-0f89f7db919b",
      "fingerprint_hash": "fp:sha1:aa12bb34...",
      "title": "Only Shallow",
      "artist": "My Bloody Valentine",
      "variants": [
        {
          "variant_id": "local-5c1a6d13a28f",
          "lossless": true,
          "codec": "FLAC",
          "container": "FLAC",
          "sample_rate_hz": 44100,
          "bit_depth": 16,
          "channels": 2,
          "duration_ms": 242000,
          "file_size_bytes": 34567890,
          "bitrate_kbps": 900,
          "file_hash_sha256": "sha256:19fefa1f...d0e3",
          "transcode_suspect": false,
          "quality_score": 0.97
        }
      ]
    }
  ]
}
```

Used for:

* Canonical scoring across peers.
* Matching candidate peers for a recording in multi-swarm and rescue mode.

### 4.3. Mesh cache job

Purpose: coordinate overlay-level caching/relay and completion for an MB release.

Payload (simplified):

```jsonc
{
  "job_id": "0f4de638-56f3-4a0f-b8fa-64f85c6b6a8f",
  "role": "request",                 // or "offer"
  "mb_release_id": "c0d0c0a4-4a26-4d74-9c02-67c9321b3b22",
  "title": "Loveless",
  "artist": "My Bloody Valentine",
  "requested_tracks": [
    {
      "track_number": 2,
      "mb_recording_id": "4863d0b0-7920-4e1d-ba55-e00e39c6bdaa",
      "priority": 10,
      "desired_profile": {
        "preferred_codecs": ["FLAC"],
        "allowed_codecs": ["FLAC", "ALAC", "WAV"],
        "min_bitrate_kbps": 800,
        "lossless_required": true
      }
    }
  ],
  "offered_tracks": [],
  "constraints": {
    "ttl_seconds": 600,
    "max_total_overlay_download_kib": 500000,
    "max_concurrent_overlay_peers": 2,
    "max_overlay_bandwidth_kbps": 3000,
    "prefer_soulseek_primary": true,
    "allow_transitive_relay": true,
    "allow_new_soulseek_fetch": true
  }
}
```

* Request side: "I want tracks X, Y, under these constraints."
* Offer side (role=`offer`): enumerates `offered_tracks` and upload constraints.

---

## 5. Overlay Chunk Transfer Messages

These are the **data-plane** messages used once peers have agreed to cooperate on a specific variant.

### 5.1. `chunk_request`

Downloader → Uploader: request a byte range for a given variant.

```jsonc
{
  "transfer_id": "c60aee88-9ac6-4726-b0cb-53cda342b11b",
  "job_id": "0f4de638-56f3-4a0f-b8fa-64f85c6b6a8f",
  "mb_recording_id": "e2f5e9b4-5852-4cd3-b1f9-29a7a4a234bc",
  "mb_release_id": "c0d0c0a4-4a26-4d74-9c02-67c9321b3b22",
  "variant_id": "local-5c1a6d13a28f",
  "offset_bytes": 0,
  "length_bytes": 262144,
  "priority": 5,
  "deadline_ms": 5000,
  "hints": {
    "allow_sparse": true,
    "prefer_contiguous": false,
    "end_of_file_known": true
  }
}
```

### 5.2. `chunk_response`

Uploader → Downloader: respond with data or an error.

```jsonc
{
  "transfer_id": "c60aee88-9ac6-4726-b0cb-53cda342b11b",
  "variant_id": "local-5c1a6d13a28f",
  "offset_bytes": 0,
  "length_bytes": 262144,
  "data_base64": "base64-encoded-bytes-here",
  "eof": false,
  "complete": false,
  "file_size_bytes": 34567890,
  "chunk_sha256": "sha256:abcd...",
  "error_code": null,
  "error_message": null
}
```

If there's an error:

```jsonc
{
  "transfer_id": "c60aee88-9ac6-4726-b0cb-53cda342b11b",
  "variant_id": "local-5c1a6d13a28f",
  "offset_bytes": 1048576,
  "length_bytes": 262144,
  "data_base64": null,
  "eof": false,
  "complete": false,
  "file_size_bytes": 34567890,
  "chunk_sha256": null,
  "error_code": "rate_limited",
  "error_message": "overlay upload bandwidth exceeded"
}
```

### 5.3. `chunk_cancel`

Downloader → Uploader: cancel a transfer or specified ranges.

```jsonc
{
  "transfer_id": "c60aee88-9ac6-4726-b0cb-53cda342b11b",
  "variant_id": "local-5c1a6d13a28f",
  "reason_code": "swarm_rebalanced",
  "reason_message": "Chunk filled by another peer.",
  "ranges": [
    { "offset_bytes": 0, "length_bytes": 524288 },
    { "offset_bytes": 1048576, "length_bytes": 262144 }
  ]
}
```

If `ranges` is empty or omitted, treat as "cancel entire transfer".

---

## 6. Transfer State Machines

### 6.1. Downloader FSM (per transfer_id)

States:

* `D_NEW` – created locally, no overlay traffic yet.
* `D_NEGOTIATING` – waiting for suitable `mesh_cache_job` offers.
* `D_READY` – chosen peer/variant, ready to request chunks.
* `D_REQUESTING` – actively issuing `chunk_request`s.
* `D_DRAINING` – no new requests, waiting for outstanding responses/cancels.
* `D_VERIFYING` – full file assembled, running verification (hash/fingerprint).
* `D_COMPLETED` – verified successfully, transfer finished.
* `D_ABORTED` – error, timeout, or user cancel.

Key transitions:

* `D_NEW → D_NEGOTIATING`: send `mesh_cache_job` (role=request).
* `D_NEGOTIATING → D_READY`: receive acceptable `mesh_cache_job` (role=offer).
* `D_READY → D_REQUESTING`: scheduler begins requesting chunks.
* `D_REQUESTING ↔ D_REQUESTING`: keep assigning new chunks as responses arrive.
* `D_REQUESTING → D_DRAINING`: no further chunks needed from this peer.
* `D_DRAINING → D_VERIFYING`: piece map shows file complete.
* `D_VERIFYING → D_COMPLETED`: verification passes; send final `chunk_cancel` and mark done.
* Any active state → `D_ABORTED`: overlay disconnect, unrecoverable error, or user cancel.

### 6.2. Uploader FSM (per transfer_id / peer)

States:

* `U_IDLE` – no state yet for this transfer.
* `U_PENDING` – job/offer known, no chunk requests yet.
* `U_SERVING` – actively reading and responding with chunks.
* `U_THROTTLED` – temporarily limited by bandwidth/slot constraints.
* `U_TEARDOWN` – cleaning up state.
* `U_DONE` – completed successfully.
* `U_REJECTED` – refused due to policy or fatal error.

Key transitions:

* `U_IDLE → U_PENDING`: receive `mesh_cache_job` (role=request) and decide to offer.
* `U_PENDING → U_SERVING`: receive first `chunk_request` for known variant.
* `U_SERVING ↔ U_THROTTLED`: depending on fairness/governor and upload limits.
* `U_SERVING → U_TEARDOWN`: receive full-transfer `chunk_cancel`, or logically complete.
* `U_TEARDOWN → U_DONE`: state cleaned; can revert to `U_IDLE`.
* Any active state → `U_REJECTED`: fatal error (file missing, policy violation).

---

## 7. Canonical Edition Scoring & Library Health

### 7.1. Quality scoring

For each `AudioVariant`:

* Compute `quality_score` (0..1), based on:
  * Lossless vs lossy.
  * Bitrate vs container.
  * Sample rate and bit depth plausibility.
  * DR/loudness and clipping if available.

* Compute `transcode_suspect` (bool) using heuristics:
  * E.g. low effective bandwidth vs reported bitrate, upsampling patterns.

Aggregate per `(MB Recording ID, codec profile)`:

* Count of variants.
* Average `quality_score`.
* Percentage suspected transcodes.

Use this to:

* Select "canonical" variants when:
  * Downloading new content.
  * Replacing non-canonical local variants.

* Provide diagnostics in UI/API.

### 7.2. Library Health / Collection Doctor

Scanning job:

* Walk configured library paths.
* For each file:
  * Resolve MB Recording/Release IDs via fingerprint + MB lookup.
  * Compare to canonical stats and MB tracklists.

* Emit issues:
  * `SuspectedTranscode(file, reason)`.
  * `NonCanonicalVariant(file, betterVariantExists)`.
  * `TrackNotInTaggedRelease(file, taggedReleaseId, likelyReleaseId)`.
  * `MissingTrackInRelease(mb_release_id, track_number)`.

These issues feed:

* UI / API for "Library Health".
* One-click "Fix via multi-swarm" actions that spawn MBID jobs to repair/complete releases.

---

## 8. Swarm Scheduling, Reputation, and Fairness

### 8.1. Swarm scheduler

Track per overlay peer:

* `rtt_avg`
* `throughput_avg`
* `error_rate`
* `timeout_rate`

Define cost function, for example:

```text
cost(peer) = α / throughput_avg + β * error_rate + γ * timeout_rate
```

Use it to:

* Rank peers per chunk assignment.
* Prefer low-cost peers for high-priority chunks.
* Rebalance away from peers whose performance degrades.

### 8.2. Local-only peer reputation

Per peer:

* Keep counters of:
  * Successful chunks.
  * Failed/corrupt chunks.
  * Timeouts.

* Compute a local `reputation_score` (0..1).

* Integrate into cost calculation or gating:
  * Avoid peers below a threshold except in "desperation" mode.

Reputation is **never shared**; it is purely local.

### 8.3. Fairness governor

Track global volumes:

* `overlay_upload_bytes`
* `overlay_download_bytes`
* `soulseek_upload_bytes`
* `soulseek_download_bytes`

Enforce invariants (configurable):

* Minimum overlay upload/download ratio.
* Maximum overlay-to-Soulseek upload ratio.

If violated:

* Throttle overlay downloads.
* Prefer Soulseek sources.
* Optionally restrict new overlay jobs.

This ensures:

* slskdn nodes remain net contributors.
* The overlay does not become a pure leech network on top of Soulseek.

---

## 9. Rescue Mode: Helping Stalled Soulseek Transfers

Rescue mode detects when a Soulseek transfer is underperforming:

* In queue for too long.
* Or active but below minimum sustained speed.

For such transfers:

1. Resolve MB Recording ID / fingerprint for the track.
2. Query mesh overlay for peers that have that recording/variant.
3. Start overlay transfers for missing byte ranges.
4. Keep original Soulseek transfer alive (lower priority).

Guardrails:

* Default: never allow pure overlay-only jobs without a Soulseek origin.
* Optionally allow overlay-only if:
  * Recording is strongly verified by fingerprints.
  * It is known to have been seen via Soulseek in the past.

Rescue mode keeps Soulseek as origin/authority while using the overlay as an accelerator and NAT/CGNAT workaround.

---

## 10. Warm Cache & Playback-Aware Extensions (Optional)

### 10.1. Warm cache nodes (opt-in)

* Nodes can enable `warm_cache.enabled`.
* Use popularity metrics (local jobs + mesh adverts) to select MBIDs to prefetch and retain.
* Fetch once from Soulseek, then:
  * Advertise high availability in `mbid_swarm_descriptor`.
  * Serve overlay chunks to others.

* Honour:
  * Capacity (`warm_cache.max_storage_gb`).
  * Fairness governor.

### 10.2. Playback-aware swarming (if built-in player exists)

* Player exposes playback position and desired buffer ahead.
* Piece map defines priority zones around playhead:
  * High: next N seconds.
  * Mid: N..M seconds.
  * Low: rest.

* Scheduler:
  * Assigns high-priority chunks to best peers with smaller chunks.
  * Fills mid/low opportunistically.

* Optionally relax fairness slightly in emergencies to avoid stutter.

---

This architecture document is the conceptual foundation. See `multi-swarm-roadmap.md` for phased implementation details and GitHub issue breakdowns.

