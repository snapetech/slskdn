# slskdn Multi-Swarm Roadmap (Experimental Branch)

This document breaks the multi-swarm work into phased feature sets and concrete implementation tasks. It is intended to map directly to GitHub issues.

## Phase 0 – Foundations

### F0.1 – Acoustic fingerprinting and MBID integration

- Integrate Chromaprint (or equivalent) to fingerprint local files.
- Query AcoustID/MusicBrainz to map fingerprints to:
  - MB Recording IDs.
  - MB Release IDs (candidate releases).
- Persist mappings in a metadata store.

### F0.2 – Overlay mesh and chunk protocol

- Implement BitTorrent DHT-based rendezvous + TCP overlay.
- Add TLS on overlay connections.
- Implement JSON-framed message envelope.
- Implement:
  - `mbid_swarm_descriptor`
  - `fingerprint_bundle_advert`
  - `mesh_cache_job`
  - `chunk_request`
  - `chunk_response`
  - `chunk_cancel`
- Implement Downloader/Seeder FSMs for per-transfer state.

---

## Phase 1 – Library Intelligence and Core Swarm Features

### A. Canonical-Edition Scoring

**A1 – Local quality scoring**

- Define `AudioVariant` with:
  - Codec/container, sample rate, bit depth, channels, duration, bitrate, file size, hash.
- Implement `quality_score` (0..1) per variant.
- Implement `transcode_suspect` heuristics.
- Persist these fields.

**A2 – Canonical stats per recording/release**

- Aggregate per `(MB Recording ID, codec profile)`:
  - Count, avg quality_score, % transcode_suspect, codec/bitrate distributions.
- Provide `GetCanonicalVariantCandidates()` function.

**A3 – Canonical-aware download selection**

- When multiple sources/variants exist for a recording:
  - Prefer canonical variants by default.
- If local library already has a variant:
  - Only download a new one if it scores higher.

---

### B. "Collection Doctor" / Library Health

**B1 – Library scan**

- Background/on-demand job:
  - Walk library paths.
  - For each file, resolve MB IDs and canonical stats.
- Emit issues:
  - Suspected transcodes.
  - Non-canonical variants.
  - Track not in tagged release.
  - Missing tracks vs MB tracklist.

**B2 – Library health UI/API**

- Provide aggregate and per-issue views:
  - By type, artist, release.
- Allow marking issues as "ignored" or "resolved".

**B3 – Fix via multi-swarm**

- From an issue, allow:
  - "Download missing track(s)" → create MB Release job for missing tracks.
  - "Replace with canonical variant" → MB Release or Recording-specific job.
- Link job completion back to the originating issue for status.

---

### C. RTT + Throughput-Aware Swarm Scheduler

**C1 – Metrics collection**

- Track per-peer:
  - RTT average.
  - Throughput average (bytes/sec).
  - Error and timeout rates.
- Maintain sliding window / exponential moving averages.

**C2 – Cost function**

- Implement configurable cost function:
  - `cost(peer) = α / throughput + β * error_rate + γ * timeout_rate`.
- Provide API to rank peer candidates for a chunk.

**C3 – Integration into scheduler**

- Use cost ranking to:
  - Assign high-priority chunks to low-cost peers.
  - Gradually shift away from peers whose metrics degrade.
- Provide config to enable/disable cost-based scheduling.

---

### D. Rescue Mode for Underperforming Soulseek Transfers

**D1 – Detect underperformance**

- Track for each Soulseek transfer:
  - Time in queue.
  - Sustained throughput.
- Mark as `underperforming` when:
  - `queued` longer than threshold, or
  - `active` but below `min_speed` for a configured duration.

**D2 – Overlay rescue**

- For underperforming transfer:
  - Resolve MB Recording ID / fingerprint.
  - Discover peers via overlay that have the recording.
  - Start overlay chunk transfers for missing ranges.
- Continue original Soulseek transfer (lower priority).

**D3 – Guardrails to keep Soulseek primary**

- Enforce:
  - At least one Soulseek origin per job (by default).
  - Maximum overlay/Soulseek byte ratios per job.
- Reject or warn on overlay-only scenarios unless explicitly configured.

---

## Phase 2 – Discovery, Reputation, and Fairness

### E. Release-Graph Guided Discovery (Discographies)

**E1 – MB artist release graph**

- Fetch and cache:
  - Release Groups for an MB Artist ID.
  - Releases under each group (albums, EPs, etc.).

**E2 – Discography profiles**

- Define profiles:
  - `core_discography` (main studio albums).
  - `extended_discography` (core + selected EPs/live).
  - `all_releases` (everything).
- Represent as lists of MB Release IDs.

**E3 – Discography jobs**

- Implement `discography` job type:
  - Input: MB Artist ID + profile.
  - Create sub-jobs: one `mb_release` job per release in profile.
- Aggregate progress across sub-jobs.

---

### F. Label Crate Mode

**F1 – Label presence aggregation**

- From overlay metadata:
  - Count releases per label, per mesh view.
- Maintain local popularity metrics per label.

**F2 – Label crate jobs**

- Implement `label_crate` job type:
  - Input: label name / MB Label ID, limit.
  - Select top N releases by popularity.
  - Spawn `mb_release` sub-jobs.
- Provide progress across the crate.

---

### G. Local-Only Peer Reputation

**G1 – Metric collection**

- Per peer:
  - Successful chunks.
  - Failed/corrupt chunks.
  - Timeouts.
  - Peer-initiated cancellations.

**G2 – Reputation scoring**

- Compute `reputation_score` (0..1) from metrics.
- Decay over time so old behaviour doesn't dominate.

**G3 – Scheduling integration**

- Integrate reputation into peer selection:
  - Down-weight or quarantine low-score peers.
- Keep reputation strictly local; no sharing.

---

### H. Mesh-Level Fairness Governor

**H1 – Traffic accounting**

- Track:
  - `overlay_upload_bytes`, `overlay_download_bytes`.
  - `soulseek_upload_bytes`, `soulseek_download_bytes`.

**H2 – Fairness constraints**

- Configurable invariants:
  - Minimum overlay upload/download ratio.
  - Maximum overlay-to-Soulseek upload ratio.
- If violated:
  - Throttle overlay downloads.
  - Increase preference for Soulseek.

**H3 – Contribution summary (optional UI)**

- Provide simple summary:
  - Per time window: overlay vs Soulseek bytes and ratios.
- This is informational; logic remains in fairness governor.

---

## Phase 3 – Job Manifests and Session Traces

### I. YAML Job Manifests

**I1 – Manifest schema**

- Define YAML schema for:
  - `mb_release` jobs.
  - `discography` / `label_crate` jobs.
- Include:
  - Job ID, type, MB IDs, target_dir, constraints, created_at, manifest_version.

**I2 – Export**

- On job creation:
  - Write manifest to `jobs/active/`.
- On job completion:
  - Move to `jobs/completed/` or update in place.

**I3 – Import**

- CLI/API to import manifest:
  - Validate schema/version.
  - Create job from manifest.
- Handle collisions and invalid manifests appropriately.

---

### J. Session Traces / Swarm Debugging

**J1 – Event model**

- Define structured swarm events:
  - Job/track/variant/peer IDs.
  - Timestamps.
  - Action (chunk_request, chunk_received, error, rescue_invoked, etc.).
  - Source (Soulseek vs overlay).

**J2 – Persistence and rotation**

- Store events per job:
  - Either in DB or log files (`logs/sessions/<job_id>.log`).
- Configurable retention:
  - Max jobs, max size, TTL.

**J3 – Summaries**

- CLI/API to summarise:
  - Per job: peer contributions, overlay vs Soulseek split.
  - Key events (rescue mode, peer failures).
- Designed for power users and debugging, not end-users.

---

## Phase 4 – Warm Cache & Playback-Aware Swarming (Optional/Advanced)

### K. Warm Cache Nodes

**K1 – Config and capacity**

- Config:
  - `warm_cache.enabled`, `warm_cache.max_storage_gb`, `warm_cache.min_popularity_threshold`.
- Tracking:
  - Which MBIDs are cached.
  - Space usage.

**K2 – Popularity detection**

- Compute popularity from:
  - Local jobs referencing MBIDs.
  - Mesh adverts referencing MBIDs.

**K3 – Cache fetch, serve, evict**

- Fetch popular MBIDs via regular multi-swarm jobs.
- Advertise cached content in overlay descriptors.
- Serve overlay chunks within fairness limits.
- Evict based on popularity and/or LRU to honour capacity.

---

### L. Playback-Aware Swarming

**L1 – Playback feedback**

- API from player:
  - Current playback position.
  - Desired buffer ahead.

**L2 – Priority zones and scheduling**

- Define high/mid/low priority zones around playback head.
- Scheduler:
  - Assign high-priority to best peers with smaller chunk sizes.
  - Fill rest opportunistically.

**L3 – Streaming diagnostics**

- Provide CLI/API:
  - Buffer ahead.
  - Peers serving current buffer.
  - Recent underruns.

---

This roadmap can be applied directly to GitHub issues. Each "Issue" block in earlier conversations maps to a concrete ticket; this file captures their grouping and sequencing.

















