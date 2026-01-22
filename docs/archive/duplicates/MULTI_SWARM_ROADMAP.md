# Multi-Swarm + MBID Intelligence Roadmap

> **Status**: Planning Document  
> **Branch**: `experimental/multi-source-swarm` → `experimental/brainz`  
> **Last Updated**: 2025-12-09

This document defines the complete feature roadmap for slskdN's multi-source swarm downloads combined with MusicBrainz/Discogs intelligence, acoustic fingerprinting, and mesh overlay capabilities.

**Core Philosophy**: Soulseek remains the canonical social/search layer and data origin. MusicBrainz/Discogs/fingerprints add semantic intelligence on top, while the DHT mesh becomes a content-aware overlay that understands real-world music identity.

---

## Implementation Sequence

Recommended order to avoid drowning in complexity:

1. **Phase 0: Foundations** - Stable primitives (mostly complete)
2. **Phase 1: Library Intelligence** - Canonical scoring, Collection Doctor, Swarm scheduler, Rescue mode
3. **Phase 2: Discovery & Fairness** - Release-graph, Label crate, Reputation, Fairness governor
4. **Phase 3: UX & Observability** - Job manifests, Session trace
5. **Phase 4: Advanced Opt-In** - Warm cache nodes, Playback-aware swarming

---

## Phase 0: Foundations (Prerequisites)

Before the fancy stuff, stable primitives must exist:

### 0.1 Acoustic Fingerprinting + MB/Discogs Integration

**Status**: Planned for Phase 1 of MUSICBRAINZ_INTEGRATION.md

**Requirements**:
- Local Chromaprint wrapper (CLI or C library)
- Background scanner service to:
  - Fingerprint files
  - Call AcoustID/MusicBrainz APIs
  - Cache mappings: `(file_hash/path) → (recording MBID, release MBID(s), acoustid)`
- Basic persistence in SQLite `audio_metadata` table or extend `HashDb`

### 0.2 MBID-Aware Data Model

**Status**: Schema defined in HASHDB_SCHEMA.md, migrations pending

**Types/Tables**:
- `Recording` (MB Recording ID + attributes)
- `Release` (MB Release ID, Release Group ID, label, date, country)
- `TrackInRelease` (release ID + track no + recording ID + duration)
- Extend `HashDb` with `mb_recording_id`, `discogs_release_id`, `audio_fingerprint`, `codec_profile`

### 0.3 Mesh Overlay + Chunk Protocol

**Status**: Base implementation complete (DHT_RENDEZVOUS_DESIGN.md, MULTI_SOURCE_DOWNLOADS.md)

**Components**:
- Overlay handshake + feature flags: `mesh`, `swarm`, `flac_hash`, etc.
- **Control-plane messages** (defined in BRAINZ_PROTOCOL_SPEC.md):
  - `mbid_swarm_descriptor`
  - `fingerprint_bundle_advert`
  - `mesh_cache_job`
- **Data-plane messages** (defined in BRAINZ_CHUNK_TRANSFER.md):
  - `chunk_request`
  - `chunk_response`
  - `chunk_cancel`
- **State machines** (defined in BRAINZ_STATE_MACHINES.md):
  - Downloader: `D_NEW` → `D_NEGOTIATING` → `D_READY` → `D_REQUESTING` → `D_DRAINING` → `D_VERIFYING` → `D_COMPLETED`
  - Uploader: `U_IDLE` → `U_PENDING` → `U_SERVING` → `U_THROTTLED` → `U_TEARDOWN` → `U_DONE`

---

## Phase 1: Library Intelligence + "Boring but Powerful" Features

### 1.1 Canonical-Edition Scoring

**Goal**: For each recording and release, know which actual audio variant is "best".

**Dependencies**: Fingerprinting, MBID model

**Implementation**:

**Data Model**:
- Extend `AudioVariant` (per file) with:
  - `quality_score` (float 0–1)
  - `transcode_suspect` flag (boolean)
  - Technical metrics: `bitrate`, `dynamic_range`, `loudness_lufs`, `has_clipping`

**Local Scoring**:
- Implement `ComputeLocalQualityScore(AudioVariant)`:
  - **+score** for: Lossless codec, sane DR (8-14 for most genres), expected sample rates (44.1/48 kHz), clean encoder signature
  - **−score** for: Suspected transcodes (FLAC with DR < 6), weird sample rates (22.05 kHz, etc.), clipping, extreme loudness

**Mesh Aggregation** (optional but valuable):
- When sending `fingerprint_bundle_advert`, include:
  - `quality_score`, `transcode_suspect`, `dynamic_range`, `encoder_signature`
- On receive:
  - Maintain local aggregates per `(MB Recording ID, codec_profile)`:
    - Peer count, `avg_quality_score`, `pct_transcode_suspect`
  - Store in `FingerprintObservations` table

**Usage**:
- Multi-swarm scheduler: prefer high-score variants
- UI: Display canonical version badge: "Canonical variant (score 0.94, 80 peers agree)"

**Files to Create**:
- `src/slskd/HashDb/CanonicalScoringService.cs`
- `src/slskd/HashDb/Models/AudioVariant.cs`
- `tests/slskd.Tests.Unit/HashDb/CanonicalScoringTests.cs`

**Acceptance Criteria**:
- [ ] Local scoring algorithm implemented and unit-tested
- [ ] Mesh aggregation of quality metrics working
- [ ] UI displays canonical badges in search results
- [ ] Swarm scheduler prefers high-score variants

---

### 1.2 "Collection Doctor" / Library Health Scanner

**Goal**: Scan existing library, find problems, and optionally auto-fix via slskdN.

**Dependencies**: Canonical scoring (1.1)

**Implementation**:

**Background Job**:
- New `LibraryHealthService` (hosted service)
- Scan library paths (from config: `data.shared.directories`)
- For each file:
  - Lookup fingerprint → MB Recording/Release, quality, canonical stats
  - Identify:
    - **Suspected transcodes**: `quality_score < threshold` or `transcode_suspect = true`
    - **Mis-tagged tracks**: Recording doesn't belong to tagged release (MBID mismatch)
    - **Missing tracks**: Release has fewer tracks than MB tracklist

**UI**:
- New "Library Health" page (`src/web/src/components/System/LibraryHealth/`)
- Display:
  - "X suspected transcodes"
  - "Y releases missing tracks"
  - "Z mis-tagged tracks (recording vs album mismatch)"
- Actions:
  - Right-click → "Fix via multi-swarm":
    - Spawns MBID-based multi-swarm jobs for those releases/tracks
  - "Re-scan Library" button
  - "Dismiss False Positive" (mark file as intentionally kept)

**Files to Create**:
- `src/slskd/LibraryHealth/LibraryHealthService.cs`
- `src/slskd/LibraryHealth/Models/HealthIssue.cs`
- `src/slskd/LibraryHealth/API/LibraryHealthController.cs`
- `src/web/src/components/System/LibraryHealth/index.jsx`
- `src/web/src/lib/libraryHealth.js`

**Acceptance Criteria**:
- [ ] Background scan detects transcodes, mis-tags, missing tracks
- [ ] UI displays health report with issue counts
- [ ] One-click "Fix via multi-swarm" creates MBID jobs
- [ ] Scan can be manually triggered or scheduled

---

### 1.3 Real Swarm Scheduler (RTT + Throughput-Aware)

**Goal**: Smarter multi-sourcing instead of naive "whoever responds first".

**Dependencies**: Chunk protocol, per-transfer metrics

**Implementation**:

**Metrics Tracking**:
- For each peer:
  - `rtt_avg` (milliseconds)
  - `throughput_avg` (bytes/sec)
  - `error_rate` (% of chunks with validation errors)
  - `timeout_rate` (% of chunk requests that timed out)
- Update on every chunk:
  - RTT: time from `chunk_request` send to first `chunk_response`
  - Throughput: `(bytes_received / time_window)` per peer

**Cost Function**:
```csharp
cost(peer) = (alpha / throughput_avg) + (beta * error_rate) + (gamma * timeout_rate)
```
- Tunable constants: `alpha = 1000`, `beta = 0.5`, `gamma = 0.3` (start simple)

**Scheduler Logic**:
- **Chunk Assignment**:
  - For each missing piece, rank candidate peers by `cost(peer)` ascending
  - Assign high-priority pieces (near end of file or "needed for playback") to lowest-cost peers
  - Assign low-priority pieces to remaining peers
- **Rebalancing**:
  - Periodically (every 10 chunks) check if some peers are much faster
  - Reassign remaining chunks from slow/stalled peers to fast peers

**Files to Modify**:
- `src/slskd/Transfers/MultiSource/MultiSourceDownloadScheduler.cs` (add cost model)
- `src/slskd/Transfers/MultiSource/Models/SourcePeer.cs` (add metric fields)

**Acceptance Criteria**:
- [ ] Per-peer metrics tracked and updated per chunk
- [ ] Cost function implemented and configurable
- [ ] Scheduler assigns chunks based on cost ranking
- [ ] Rebalancing moves chunks from slow to fast peers
- [ ] Unit tests for cost calculation and assignment logic

---

### 1.4 "Rescue Mode" for Stalled Soulseek Transfers

**Goal**: Overlay + mesh act as fallback/accelerator when Soulseek is slow or stuck.

**Dependencies**: Canonical scoring (1.1), Swarm scheduler (1.3), Chunk protocol (0.3)

**Implementation**:

**Detection**:
- For each Soulseek transfer, track:
  - State: `queued`, `stalled`, `active`
  - Speed: bytes/sec over last 60 seconds
- Mark as `underperforming` if:
  - `queued` for > 30 minutes, OR
  - `active` with speed < 10 KB/s for > 5 minutes, OR
  - No bytes received for > 2 minutes

**Rescue Flow**:
1. For an `underperforming` track:
   - Lookup its MB Recording ID / fingerprint from `HashDb`
   - Ask mesh: "Who has this recording?" via:
     - Query DHT for `sha1(b"slskdn-mb-v1:" + mb_recording_id)`
     - Parse `fingerprint_bundle_advert` messages
2. Start overlay transfers from mesh peers for missing byte ranges
3. Keep original Soulseek transfer alive:
   - Lower priority but don't cancel
   - Remains "origin traffic" for fairness

**Guardrails**:
- **Never** allow overlay-only without Soulseek origin:
  - Require at least one active Soulseek source, OR
  - Proof that overlay peers got their copy via Soulseek (matching MBID + canonical stats)

**UI Indicator**:
```
Track 5: Downloading (Rescue Mode Active)
├─ Soulseek peer A: Stalled (2 KB/s, 12% complete)
├─ Mesh peer B: 45% (overlay)
└─ Mesh peer C: 23% (overlay)
```

**Files to Create**:
- `src/slskd/Transfers/Rescue/RescueService.cs`
- `src/slskd/Transfers/Rescue/StallDetector.cs`

**Acceptance Criteria**:
- [ ] Stall detection triggers for queued/slow/stalled transfers
- [ ] Rescue service queries DHT for MBID-based mesh peers
- [ ] Overlay swarm starts for missing byte ranges
- [ ] Original Soulseek transfer remains active (deprioritized)
- [ ] UI shows "Rescue Mode Active" badge with source breakdown

---

## Phase 2: Discovery, Selection & Fairness Layer

### 2.1 Release-Graph Guided Discovery

**Goal**: "Complete/subscription-style" discography jobs based on MB's release graph.

**Dependencies**: MB APIs, MBID data model (0.2)

**Implementation**:

**Graph Queries**:
- Given an MB Release:
  - Fetch its Release Group (album vs single vs compilation)
  - Fetch neighbor Releases:
    - Same artist, same release group
    - Other releases by the artist in certain years/labels
    - Related artists (shared personnel, labels, producers)

**Discography Tree**:
- Build hierarchy:
  - **Core discography**: Main studio albums
  - **Extended**: EPs, live, compilations
  - **Related**: Side projects, collaborations

**Job Generator**:
- For a chosen entry point (album):
  - Generate list of MB Release IDs for:
    - Core discography job
    - Extended job (adds EPs, live, etc.)
  - Spawn one MBID-based multi-swarm job per Release

**UI Workflows**:
- **"Complete Artist Discography"**:
  - User selects artist → slskdN queries MB for all studio albums → creates multi-album job
- **"Related Albums"**:
  - Sidebar in album detail view: "Also by this producer", "Same label", "Featured artist Y"
  - One-click to add to queue

**Files to Create**:
- `src/slskd/MusicBrainz/MusicBrainzGraphService.cs`
- `src/slskd/MusicBrainz/Models/DiscographyTree.cs`
- `src/slskd/MusicBrainz/API/DiscographyController.cs`
- `src/web/src/components/Discography/DiscographySidebar.jsx`

**Acceptance Criteria**:
- [ ] MB graph queries fetch related releases and artists
- [ ] Discography tree built and cached locally
- [ ] UI allows "Complete Artist Discography" job creation
- [ ] Multi-album jobs spawn individual MBID-based jobs

---

### 2.2 Label Crate Mode

**Goal**: "Crate digging" by label within your mesh neighbourhood.

**Dependencies**: Fingerprint bundle adverts (with release info), MB data (0.2)

**Implementation**:

**Aggregation**:
- From `fingerprint_bundle_advert` / `mbid_swarm_descriptor` messages:
  - Build counts of `(label, release MBID)` seen across mesh peers
  - Store in `LabelPopularity` table: `label_name`, `mb_release_id`, `peer_count`, `avg_quality_score`

**UI**:
- New "Label Browser" page
- User picks a label (autocomplete from known labels)
- Display:
  - Most commonly seen releases for that label in mesh view
  - Sorted by peer count descending
  - Filter by quality score, codec, year

**Job Creation**:
- "Make label crate job" button:
  - Build a job with top N releases by that label (N = 10, 25, 50, all)
  - Fire them as MBID-based multi-swarm jobs

**Files to Create**:
- `src/slskd/Discovery/LabelCatalogService.cs`
- `src/slskd/Discovery/Models/LabelPopularity.cs`
- `src/slskd/Discovery/API/LabelCatalogController.cs`
- `src/web/src/components/Discovery/LabelBrowser.jsx`

**Acceptance Criteria**:
- [ ] Mesh aggregates label popularity from fingerprint adverts
- [ ] UI displays label catalog with peer counts and quality
- [ ] One-click "Make label crate job" creates multi-album job
- [ ] Label autocomplete works from known labels

---

### 2.3 Local-Only Peer Reputation

**Goal**: Avoid obviously bad or flaky peers without any global scoring system.

**Dependencies**: Metrics from scheduler (1.3), verification outcomes

**Implementation**:

**Metrics Tracking**:
- For each peer:
  - `successful_chunks`, `failed_chunks`, `timeouts`, `corrupt_chunks`
  - Compute local `reputation_score` (0–1):
    ```csharp
    reputation = 1.0 - (0.3 * timeout_rate + 0.5 * corruption_rate + 0.2 * failure_rate)
    ```

**Usage**:
- Scheduler:
  - Down-weight or entirely avoid peers with `reputation_score < 0.3`
  - Prefer high-reputation peers for critical chunks
- Persistence:
  - Save to `Peers` table with `local_reputation` field
  - Decay over time (weekly half-life) so peers can recover from bad day

**Privacy**:
- **Never** broadcast reputation scores
- Purely local, per-node decision-making

**Files to Modify**:
- `src/slskd/HashDb/Models/Peer.cs` (add `local_reputation` field)
- `src/slskd/Transfers/MultiSource/MultiSourceDownloadScheduler.cs` (use reputation in cost model)

**Acceptance Criteria**:
- [ ] Per-peer reputation tracked and persisted
- [ ] Reputation decays over time (weekly half-life)
- [ ] Scheduler avoids low-reputation peers
- [ ] Reputation never shared over mesh

---

### 2.4 Mesh-Level "Fairness Governor"

**Goal**: Ensure the mesh doesn't become a one-way leech; slskdN strengthens Soulseek's ecosystem.

**Dependencies**: Overlay bandwidth tracking (both directions)

**Implementation**:

**Counters**:
- Track per-node stats:
  - `overlay_upload_bytes`, `overlay_download_bytes`
  - `soulseek_upload_bytes`, `soulseek_download_bytes`
- Store in `HashDbState` table as rolling 7-day totals

**Policies**:
- Enforce invariants:
  - **Min contribution ratio**: `total_upload_bytes / total_download_bytes ≥ 1.0` (configurable)
  - **Overlay upload cap**: `overlay_upload_bytes ≤ 2.0 * soulseek_upload_bytes` (prevent pure overlay leeching)
  - **Daily overlay cap**: `overlay_download_bytes_today ≤ 50 GB` (configurable)
- If violating:
  - Throttle overlay downloads (reduce max connections)
  - Prioritize Soulseek sources over overlay in scheduler

**UI**:
- "Contribution Report" page:
  ```
  Contribution Report (Last 7 Days)

  Uploaded:
  ├─ Soulseek: 42.3 GB
  └─ Overlay Mesh: 68.7 GB
  Total: 111.0 GB

  Downloaded:
  ├─ Soulseek: 18.5 GB
  └─ Overlay Mesh: 12.1 GB
  Total: 30.6 GB

  Ratio: 3.6:1 (you've contributed 3.6× more than you downloaded)
  Status: ✓ Healthy contributor
  ```

**Files to Create**:
- `src/slskd/Fairness/ContributionTrackingService.cs`
- `src/slskd/Fairness/FairnessGovernor.cs`
- `src/slskd/Fairness/API/ContributionController.cs`
- `src/web/src/components/System/Contribution/index.jsx`

**Acceptance Criteria**:
- [ ] Upload/download bytes tracked per source (Soulseek vs overlay)
- [ ] Fairness policies enforced (min ratio, upload cap, daily cap)
- [ ] Violations trigger throttling of overlay downloads
- [ ] UI displays contribution report with status badge

---

## Phase 3: UX & Observability

### 3.1 Download Job Manifests (.yaml recipes)

**Goal**: Make jobs portable, inspectable, and resumable outside the DB.

**Dependencies**: MBID-based jobs (1.2, 1.4, 2.1, 2.2)

**Implementation**:

**Manifest Schema**:
```yaml
job_id: 0f4de638-56f3-4a0f-b8fa-64f85c6b6a8f
type: mb_release
mb_release_id: c0d0c0a4-4a26-4d74-9c02-67c9321b3b22
title: Loveless
artist: My Bloody Valentine
tracks:
  - position: 1
    mb_recording_id: abc-123-def
    title: Only Shallow
    duration_ms: 282000
  - position: 2
    mb_recording_id: def-456-ghi
    title: Loomer
    duration_ms: 178000
constraints:
  preferred_codecs: [FLAC]
  max_lossy_tracks_per_album: 0
  prefer_canonical_variants: true
  use_overlay: true
  overlay_bandwidth_kbps: 3000
status:
  started_at: 2025-12-09T15:30:00Z
  completed_tracks: [1]
  in_progress_tracks: [2]
  pending_tracks: [3, 4, 5, 6, 7, 8, 9, 10, 11]
```

**Export/Import**:
- On job creation, write `.yaml` to:
  - `jobs/active/<job_id>.yaml`
- On completion, move to:
  - `jobs/completed/<job_id>.yaml`
- CLI / UI:
  - "Export Job" button → download YAML
  - "Import Job Manifest" → upload YAML, creates job

**Use Cases**:
- Migration to another machine
- Reproducibility / sharing with others
- Commit manifests to Git alongside library for auditable download history
- Resume jobs after database corruption

**Files to Create**:
- `src/slskd/Jobs/JobManifestService.cs`
- `src/slskd/Jobs/API/JobManifestController.cs`
- CLI: `slskdn jobs export <job_id>`, `slskdn jobs import <file>`

**Acceptance Criteria**:
- [ ] Jobs automatically export to YAML on creation
- [ ] UI allows manual export/import of job manifests
- [ ] Import recreates job from YAML (even if DB empty)
- [ ] Manifests are human-readable and editable

---

### 3.2 Session Trace / Swarm Debug Logs

**Goal**: Introspection so you can see what the swarm scheduler actually did.

**Dependencies**: Scheduler (1.3), transfer state

**Implementation**:

**Log Model**:
- For each job, timeline entries:
  - `time`, `peer_id`, `track_id`, `variant_id`
  - `chunk_offset`, `chunk_length`
  - `source` (Soulseek vs overlay)
  - `result` (ok, timeout, error, corruption)
  - `rtt_ms`, `throughput_bps`

**Storage**:
- Keep rolling logs (last 100 jobs) with configurable retention
- Store in `JobTraces` table or export to JSON file

**UI**:
- Dev-only or advanced view:
  - "Job Trace Viewer" (collapsible detail panel)
  - Graph or table per track:
    ```
    Job: Loveless - Track 7 (All Blues)

    Sources:
    ├─ Soulseek peer "user123": 60% (chunks 0-23, 40-59)
    │  └─ Avg speed: 450 KB/s, RTT: 120ms, 0 errors
    ├─ Overlay peer "abc-def-ghi": 30% (chunks 24-39)
    │  └─ Avg speed: 1.2 MB/s, RTT: 45ms, 0 errors
    └─ Overlay peer "ghi-jkl-mno": 10% (chunks 60-69, rescue mode)
       └─ Avg speed: 180 KB/s, RTT: 200ms, 2 retries

    Timeline:
    00:00 - Started, 3 sources discovered
    00:12 - Soulseek peer stalled, rescue mode triggered
    00:18 - Overlay peer C added, chunks 60-69 reassigned
    00:24 - Complete
    ```

**CLI**:
- `slskdn debug job <job_id>` → spit out text summary

**Files to Create**:
- `src/slskd/Observability/DownloadTraceService.cs`
- `src/slskd/Observability/Models/JobTrace.cs`
- `src/slskd/Observability/API/TraceController.cs`
- `src/web/src/components/Observability/JobTraceViewer.jsx`

**Acceptance Criteria**:
- [ ] All chunk assignments logged to timeline
- [ ] UI displays source breakdown per job
- [ ] Timeline shows scheduler decisions and rescue triggers
- [ ] Export trace to JSON for external analysis

---

## Phase 4: Advanced / "Opt-In Heavy" Features

### 4.1 Warm Cache Overlay Nodes

**Goal**: Opt-in nodes that behave like mini CDNs for popular MBIDs.

**Dependencies**: All of Phase 1, Phase 2, Phase 3

**Implementation**:

**Config**:
```yaml
warm_cache:
  enabled: true
  max_storage_gb: 50
  min_popularity_threshold: 10  # MBID must be wanted by 10+ peers
```

**Popularity Tracking**:
- From `mesh_cache_job` requests / `fingerprint_bundle_advert` messages:
  - Count how many jobs or peers reference an MBID
  - Store in `MbidPopularity` table

**Behavior**:
- When warm cache node sees MBID popularity > threshold:
  1. Ensure it has a canonical variant for that MBID:
     - Fetch via Soulseek normally (respects slots/queues)
     - Fingerprint + verify against mesh consensus
  2. Advertise high overlay availability via `mbid_swarm_descriptor` with `cache: true` flag
  3. Serve chunks to other slskdN users over TLS overlay

**Guardrails**:
- Respect fairness governor (2.4):
  - Don't exceed certain overlay upload/download ratio
  - Count cache uploads as overlay uploads
- Eviction policy:
  - LRU or "lowest popularity first" to stay within `max_storage_gb`
  - TTL: 90 days for unpopular, infinite for manually pinned

**Fan-Out Amplification**:
```
Scenario: 100 users want "Kind of Blue" (MB:abc-123)

Without cache:
- Original Soulseek uploader: 100 uploads × 500MB = 50GB
- Slow (slots, queues, single uploader)

With 3 cache nodes:
- Caches fetch once from Soulseek: 3 × 500MB = 1.5GB
- Caches serve 100 users via overlay: Fast TLS, no slots
- Original uploader: Only 1.5GB total
- Users: 10-50× faster downloads
```

**Files to Create**:
- `src/slskd/Cache/CacheService.cs`
- `src/slskd/Cache/Models/MbidPopularity.cs`
- `src/slskd/Cache/API/CacheController.cs`
- UI: "Cache Node Mode" settings panel

**Acceptance Criteria**:
- [ ] Cache nodes track MBID popularity from mesh
- [ ] Popular MBIDs automatically fetched and verified
- [ ] DHT announces include `cache: true` flag
- [ ] Cache serves chunks over overlay with LRU eviction
- [ ] Fairness governor limits cache upload bandwidth

---

### 4.2 Playback-Aware Swarming (if/when built-in player added)

**Goal**: Optimize swarming for streaming playback (progressive download).

**Dependencies**: Swarm scheduler (1.3), chunking, canonical scoring (1.1)

**Implementation**:

**Player Integration**:
- When a track is playing:
  - Expose current `playback_position_ms` and `buffer_needed_ms` to swarm engine
  - Example: "Currently at 1:23, need next 30 seconds buffered"

**Piece Map**:
- Mark byte ranges based on playback position:
  - **High-priority window**: Next 30-60 seconds (immediate playback needs)
  - **Mid-priority**: Next 2-3 minutes (buffer ahead)
  - **Low-priority**: Rest of file (background fill)

**Scheduler Strategy**:
- For **high-priority window**:
  - Use only low-cost, reliable peers (low RTT, high throughput)
  - Choose small chunk sizes for low latency
  - Fail fast on timeouts (3 second timeout)
- For **mid/low priority**:
  - Fill using slower peers and larger chunks
  - Higher timeout tolerance (30 seconds)

**Buffering Logic**:
- Start playback once first 5-10 seconds buffered
- Maintain 30-second lead buffer
- If buffer drops below 10 seconds: raise priority of next chunks, pause playback if < 5 seconds

**UI Display**:
```
Streaming: Track 5 (Blue in Green)
├─ Buffer: 28 seconds (healthy)
├─ Sources: Soulseek peer A + 2 overlay peers
└─ Speed: 850 KB/s (3× playback rate)
```

**Files to Create**:
- `src/slskd/Playback/PlaybackScheduler.cs`
- `src/slskd/Playback/BufferManager.cs`
- Integrate with hypothetical media player component

**Acceptance Criteria**:
- [ ] Playback position exposed to swarm scheduler
- [ ] High-priority window uses low-latency peers
- [ ] Buffer manager prevents stalls (<5 sec = pause)
- [ ] UI shows streaming status with buffer level

---

## Success Metrics

### Phase 1 Metrics
- **Canonical accuracy**: % of files correctly labeled canonical vs transcode
- **Library health adoption**: % of users who run Collection Doctor
- **Rescue mode effectiveness**: % of stalled transfers rescued via mesh
- **Swarm efficiency**: Avg download time reduction with smart scheduler

### Phase 2 Metrics
- **Discography completion**: % of artist discographies completed via graph discovery
- **Label crate usage**: # of label crate jobs created per month
- **Reputation impact**: % of peers flagged low-reputation, % of chunk errors avoided
- **Fairness compliance**: % of users maintaining healthy contribution ratios

### Phase 3 Metrics
- **Manifest adoption**: % of jobs exported to YAML
- **Trace usage**: % of users viewing session traces
- **Observability value**: User satisfaction with swarm debugging tools

### Phase 4 Metrics
- **Cache hit rate**: % of downloads served from warm cache nodes
- **Cache bandwidth savings**: Reduction in Soulseek upload burden
- **Playback stalls**: % of streams that never stalled (target: >95%)

---

## Implementation Priorities

### Immediate (Next Sprint)
1. **Canonical scoring** (1.1) - Foundation for all quality work
2. **Swarm scheduler** (1.3) - Improves all downloads immediately
3. **Job manifests** (3.1) - Low-hanging fruit for UX

### Short-Term (2-4 weeks)
1. **Collection Doctor** (1.2) - High user value
2. **Rescue mode** (1.4) - Solves major pain point
3. **Fairness governor** (2.4) - Critical for ecosystem health

### Medium-Term (1-2 months)
1. **Release-graph discovery** (2.1) - Power user feature
2. **Local reputation** (2.3) - Improves scheduler
3. **Session trace** (3.2) - Dev/debug tooling

### Long-Term (3-6 months)
1. **Label crate** (2.2) - Discovery tool
2. **Warm cache nodes** (4.1) - Advanced opt-in
3. **Playback-aware swarming** (4.2) - Requires player first

---

## Related Documentation

- [MUSICBRAINZ_INTEGRATION.md](./MUSICBRAINZ_INTEGRATION.md) - Phase 1-3 MBID integration design
- [BRAINZ_PROTOCOL_SPEC.md](./BRAINZ_PROTOCOL_SPEC.md) - Control-plane message formats
- [BRAINZ_CHUNK_TRANSFER.md](./BRAINZ_CHUNK_TRANSFER.md) - Data-plane chunk protocol
- [BRAINZ_STATE_MACHINES.md](./BRAINZ_STATE_MACHINES.md) - Downloader/uploader state machines
- [MULTI_SOURCE_DOWNLOADS.md](./docs/archive/duplicates/MULTI_SOURCE_DOWNLOADS.md) - Current multi-source implementation
- [DHT_RENDEZVOUS_DESIGN.md](./DHT_RENDEZVOUS_DESIGN.md) - DHT mesh overlay design
- [HASHDB_SCHEMA.md](./HASHDB_SCHEMA.md) - Database schema and migrations

---

*Last updated: 2025-12-09*

