# Tasks (Source of Truth)

> This file is the canonical task list for slskdN development.  
> AI agents should add/update tasks here, not invent ephemeral todos in chat.

---

## Active Development

### MusicBrainz Integration (experimental/brainz)

> **Branch**: `experimental/brainz`  
> **Issue**: #53  
> **Docs**: `docs/MUSICBRAINZ_INTEGRATION.md`  
> **Timeline**: Phase 1 (4-6 weeks)

#### Phase 1A: MusicBrainz API Integration

 - [x] **T-300**: Create MusicBrainzClient service
  - Status: Done
  - Priority: P0
  - Branch: experimental/brainz
  - Notes: HTTP client for MusicBrainz API v2, release/recording lookups, models, options, and DI registration

 - [x] **T-301**: Implement AlbumTarget data model
  - Status: Done
  - Priority: P0
  - Branch: experimental/brainz
  - Notes: Added AlbumTarget, TrackTarget, ReleaseMetadata models used by MusicBrainz client

 - [x] **T-302**: Add UI for MBID input
  - Status: Done
  - Priority: P0
  - Branch: experimental/brainz
  - Notes: Added MusicBrainz/Discogs lookup segment in search UI with backend connection

 - [x] **T-303**: Store album targets in SQLite
  - Status: Done
  - Priority: P0
  - Branch: experimental/brainz
  - Notes: New tables for AlbumTargets, extend HashDb schema

#### Phase 1B: Chromaprint Integration

- [x] **T-304**: Add Chromaprint native library
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Added Chromaprint service (SafeHandle wrapper, DllImports, options, DI)

 - [x] **T-305**: Implement fingerprint extraction service
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Added ffmpeg-powered service to decode samples and feed Chromaprint

 - [x] **T-306**: Integrate AcoustID API client
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Added AcoustID client, stores recording IDs alongside fingerprints

 - [x] **T-307**: Add fingerprint column to HashDb
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Schema already includes `audio_fingerprint` and `musicbrainz_id`; HashDbService writes to both

- [x] **T-308**: Build auto-tagging pipeline
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Fingerprint → AcoustID → MusicBrainz track metadata written via TagLib and HashDb

#### Phase 1C: ID-Aware Multi-Swarm

 - [x] **T-309**: Extend MultiSourceDownloadJob with MBID fields
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Source + chunk metadata now carries MusicBrainz recording IDs/fingerprints

 - [x] **T-310**: Implement semantic swarm grouping logic
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Semantic grouping now clusters by MBID/fingerprint within the download request

 - [x] **T-311**: Add fingerprint verification to download pipeline
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Final-file fingerprint & MBID verification occurs after swarm download

- [x] **T-312**: Build album completion UI
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Progress display for album downloads, missing track warnings

- [x] **T-313**: Unit tests + integration tests
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Test coverage for Phase 1 features

---

### Phase 2: Mesh Integration & Advanced Swarm Features

> **Branch**: `experimental/multi-swarm`  
> **Parent Docs**: `docs/multi-swarm-architecture.md`, `docs/multi-swarm-roadmap.md`  
> **Timeline**: Phase 2 (6-8 weeks)

#### Phase 2A: Canonical Edition Scoring

- [x] **T-400**: Implement local quality scoring for AudioVariant
  - Status: Done
  - Completed: 2025-12-10
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Define AudioVariant model with codec/container, sample rate, bit depth, channels, duration, bitrate, file size, hash. Implement quality_score (0..1) and transcode_suspect heuristics.

- [x] **T-401**: Build canonical stats aggregation per recording/release
  - Status: Done
  - Completed: 2025-12-10
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Aggregate per (MB Recording ID, codec profile): count, avg quality_score, % transcode_suspect, codec/bitrate distributions. Provide GetCanonicalVariantCandidates() function.

- [x] **T-402**: Integrate canonical-aware download selection
  - Status: Done
  - Completed: 2025-12-10
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Prefer canonical variants by default in multi-source swarms. Only download new variant if it scores higher than existing local variant.

#### Phase 2B: Collection Doctor / Library Health

- [x] **T-403**: Implement library scan service
  - Status: Done
  - Completed: 2025-12-10
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Background/on-demand job to walk library paths, resolve MB IDs via fingerprints, compare to canonical stats. Emit issues: suspected transcodes, non-canonical variants, track not in tagged release, missing tracks.

- [x] **T-404**: Build library health UI/API
  - Status: Done
  - Completed: 2025-12-10
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Provide aggregate and per-issue views (by type, artist, release). Allow marking issues as "ignored" or "resolved".

- [x] **T-405**: Add "Fix via multi-swarm" actions
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Completed: 2025-12-10
  - Notes: From library health issues, allow "Download missing track(s)" or "Replace with canonical variant" by spawning MB Release/Recording jobs. Link job completion back to originating issue. Remediation service implemented with UI fix buttons and bulk operations.

#### Phase 2-Extended: Advanced AudioVariant Fingerprinting (Codec-Specific)

> **Docs**: `docs/phase2-advanced-fingerprinting-design.md`  
> **Tasks**: T-420 through T-430

- [x] **T-420**: Extend AudioVariant model with codec-specific fields
  - Status: Done
  - Completed: 2025-12-10
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Add FLAC streaminfo hash, MP3 stream hash, Opus/AAC hashes, spectral features, encoder metadata. HashDb migration version 7.

- [x] **T-421**: Implement FLAC analyzer
  - Status: Done
  - Completed: 2025-12-10
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: STREAMINFO parser (42-byte hash, PCM MD5), quality scoring, transcode detection via spectral analysis.

- [x] **T-422**: Implement MP3 analyzer
  - Status: Done
  - Completed: 2025-12-10
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Tag-stripped stream hash, encoder detection (LAME), spectral features (bandwidth, flatness), transcode detection.

- [x] **T-423**: Implement Opus analyzer
  - Status: Done
  - Completed: 2025-12-10
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Ogg Opus stream hash, bitrate/bandwidth mode extraction, quality scoring tuned for Opus.

- [x] **T-424**: Implement AAC analyzer
  - Status: Done
  - Completed: 2025-12-10
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: AAC stream hash (MP4/ADTS), profile detection (LC/HE/HEv2), SBR/PS flags, transcode detection.

- [ ] **T-425**: Implement audio_sketch_hash (PCM-window hash)
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Decode short PCM windows from arbitrary audio files, downsample to mono 4 kHz, hash with xxHash64.

- [ ] **T-426**: Implement cross-codec deduplication logic
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Query variants by MB Recording ID + audio_sketch_hash, deduplicate across codec boundaries. Debug API endpoint.

- [ ] **T-427**: Implement analyzer version migration
  - Status: Not started
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Background job to detect stale analyzer_version, recompute quality scores from raw features. CLI command.

- [ ] **T-428**: Update CanonicalStatsService with codec-specific logic
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Use codec-specific stream hashes for deduplication, prefer lossless over lossy explicitly, use audio_sketch_hash.

- [ ] **T-429**: Add codec-specific stats to Library Health
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Detect codec mismatches, flag transcodes using new analyzer results, suggest codec-specific canonical replacements.

- [ ] **T-430**: Unit tests for codec analyzers
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Test fixtures for FLAC/MP3/Opus/AAC, quality score computation, transcode detection, stream hash stability, cross-codec deduplication.

#### Phase 2C: RTT + Throughput-Aware Swarm Scheduler

- [x] **T-406**: Implement per-peer metrics collection
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Completed: 2025-12-10
  - Notes: PeerPerformanceMetrics model with EMA tracking for RTT, throughput, reliability; PeerMetricsService with sliding windows; HashDb placeholder methods (schema + persistence pending full implementation)

- [x] **T-407**: Build configurable cost function for peer ranking
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Completed: 2025-12-10
  - Notes: PeerCostFunction with 5 components (inverse throughput, error rate penalty, timeout rate penalty, RTT penalty, variance penalty); configurable weights (α,β,γ,δ); RankPeers method; integrated into PeerMetricsService

- [x] **T-408**: Integrate cost-based scheduling into swarm manager
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Completed: 2025-12-10
  - Notes: ChunkScheduler with IChunkScheduler interface; AssignChunkAsync (single), AssignMultipleChunksAsync (batch with priority); HandlePeerDegradationAsync for adaptive shifting; ChunkRequest, ChunkAssignment, DegradationReason models; registered as singleton in DI; enableCostBasedScheduling flag (default: true)

#### Phase 2D: Rescue Mode for Underperforming Soulseek Transfers

- [x] **T-409**: Implement transfer underperformance detection
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Completed: 2025-12-10
  - Notes: Instrumented DownloadService with PeerMetricsService; tracks throughput samples in progressUpdated callback (delta bytes / delta time); records ChunkCompletionResult.Success on completion, TimedOut on TimeoutException, Failed on other exceptions; optional injection (nullable) for backward compat

- [x] **T-410**: Build overlay rescue logic
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Completed: 2025-12-10
  - Notes: RescueService with IRescueService interface; ActivateRescueModeAsync (resolve MBID, discover overlay peers, compute missing ranges, create rescue job); RescueJob, ByteRange, OverlayPeerInfo, UnderperformanceReason models; ResolveRecordingIdAsync (3 strategies: HashDb, fingerprinting, filename parsing); DiscoverOverlayPeersAsync placeholder; ComputeMissingRanges (simple end-missing logic); PLACEHOLDER implementation - full integration pending (see backfill TODOs)

- [x] **T-411**: Add Soulseek-primary guardrails
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Completed: 2025-12-10
  - Notes: RescueGuardrailService with IRescueGuardrailService interface; CheckRescueAllowedAsync (global enable check, Soulseek origin requirement); CheckMultiSourceJobAllowedAsync (overlay-only prohibition, overlay/Soulseek ratio limit, min Soulseek peer count); RescueGuardrailConfig with defaults (RequireSoulseekOrigin=true, AllowOverlayOnly=false, MaxOverlayRatio=0.5, MinSoulseekPeers=1); Integrated into RescueService activation flow; Ensures Soulseek remains primary network

---

### Phase 3: Discovery, Reputation, and Fairness

> **Branch**: `experimental/multi-swarm`  
> **Timeline**: Phase 3 (8-10 weeks)

#### Phase 3A: Release-Graph Guided Discovery (Discographies)

- [ ] **T-500**: Build MB artist release graph service
  - Status: Not started
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Fetch and cache Release Groups for MB Artist ID. Fetch Releases under each group (albums, EPs, etc.) from MusicBrainz API.

- [ ] **T-501**: Define discography profiles
  - Status: Not started
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Define profiles: core_discography (main studio albums), extended_discography (core + selected EPs/live), all_releases (everything). Represent as lists of MB Release IDs.

- [ ] **T-502**: Implement discography job type
  - Status: Not started
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Create discography job: input MB Artist ID + profile, create sub-jobs (one mb_release job per release), aggregate progress across sub-jobs.

#### Phase 3B: Label Crate Mode

- [ ] **T-503**: Build label presence aggregation
  - Status: Not started
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: From overlay metadata, count releases per label per mesh view. Maintain local popularity metrics per label.

- [ ] **T-504**: Implement label crate job type
  - Status: Not started
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Create label_crate job: input label name/MB Label ID + limit, select top N releases by popularity, spawn mb_release sub-jobs, provide progress across the crate.

#### Phase 3C: Local-Only Peer Reputation

- [ ] **T-505**: Implement peer reputation metric collection
  - Status: Not started
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Track per-peer: successful chunks, failed/corrupt chunks, timeouts, peer-initiated cancellations.

- [ ] **T-506**: Build reputation scoring algorithm
  - Status: Not started
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Compute reputation_score (0..1) from metrics. Add decay over time so old behaviour doesn't dominate.

- [ ] **T-507**: Integrate reputation into swarm scheduling
  - Status: Not started
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Integrate reputation into peer selection: down-weight or quarantine low-score peers. Keep reputation strictly local (no sharing).

#### Phase 3D: Mesh-Level Fairness Governor

- [ ] **T-508**: Implement traffic accounting
  - Status: Not started
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Track overlay_upload_bytes, overlay_download_bytes, soulseek_upload_bytes, soulseek_download_bytes globally.

- [ ] **T-509**: Build fairness constraint enforcement
  - Status: Not started
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Implement configurable invariants: minimum overlay upload/download ratio, maximum overlay-to-Soulseek upload ratio. If violated: throttle overlay downloads, increase Soulseek preference.

- [ ] **T-510**: Add contribution summary UI (optional)
  - Status: Not started
  - Priority: P3
  - Branch: experimental/brainz
  - Notes: Provide per-time-window summary: overlay vs Soulseek bytes and ratios. Informational only; logic remains in fairness governor.

---

### Phase 4: Job Manifests, Session Traces & Advanced Features

> **Branch**: `experimental/multi-swarm`  
> **Timeline**: Phase 4 (6-8 weeks)

#### Phase 4A: YAML Job Manifests

- [ ] **T-600**: Define YAML job manifest schema
  - Status: Not started
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Define YAML schema for mb_release, discography, label_crate jobs. Include job ID, type, MB IDs, target_dir, constraints, created_at, manifest_version.

- [ ] **T-601**: Implement job manifest export
  - Status: Not started
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: On job creation write manifest to jobs/active/. On completion move to jobs/completed/ or update in place.

- [ ] **T-602**: Build job manifest import
  - Status: Not started
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: CLI/API to import manifest: validate schema/version, create job from manifest. Handle collisions and invalid manifests.

#### Phase 4B: Session Traces / Swarm Debugging

- [ ] **T-603**: Define swarm event model
  - Status: Not started
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Define structured events: job/track/variant/peer IDs, timestamps, action (chunk_request, chunk_received, error, rescue_invoked, etc.), source (Soulseek vs overlay).

- [ ] **T-604**: Implement event persistence and rotation
  - Status: Not started
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Store events per job in DB or log files (logs/sessions/<job_id>.log). Add configurable retention: max jobs, max size, TTL.

- [ ] **T-605**: Build session trace summaries
  - Status: Not started
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: CLI/API to summarise per-job: peer contributions, overlay vs Soulseek split, key events (rescue mode, peer failures). For power users and debugging.

#### Phase 4C: Warm Cache Nodes (Optional)

- [ ] **T-606**: Implement warm cache configuration
  - Status: Not started
  - Priority: P3
  - Branch: experimental/brainz
  - Notes: Add config: warm_cache.enabled, warm_cache.max_storage_gb, warm_cache.min_popularity_threshold. Track which MBIDs are cached and space usage.

- [ ] **T-607**: Build popularity detection for caching
  - Status: Not started
  - Priority: P3
  - Branch: experimental/brainz
  - Notes: Compute MBID popularity from local jobs and mesh adverts. Use to decide what to prefetch.

- [ ] **T-608**: Add cache fetch, serve, evict logic
  - Status: Not started
  - Priority: P3
  - Branch: experimental/brainz
  - Notes: Fetch popular MBIDs via multi-swarm jobs. Advertise cached content in overlay descriptors. Serve overlay chunks within fairness limits. Evict based on popularity/LRU to honour capacity.

#### Phase 4D: Playback-Aware Swarming (Optional)

- [ ] **T-609**: Implement playback feedback API
  - Status: Not started
  - Priority: P3
  - Branch: experimental/brainz
  - Notes: API from hypothetical player: current playback position, desired buffer ahead.

- [ ] **T-610**: Build priority zones and playback-aware scheduling
  - Status: Not started
  - Priority: P3
  - Branch: experimental/brainz
  - Notes: Define high/mid/low priority zones around playback head. Scheduler assigns high-priority to best peers with smaller chunk sizes, fills rest opportunistically.

- [ ] **T-611**: Add streaming diagnostics
  - Status: Not started
  - Priority: P3
  - Branch: experimental/brainz
  - Notes: CLI/API for buffer ahead, peers serving current buffer, recent underruns.

---

### Phase 5: Soulbeet Integration

> **Branch**: `experimental/multi-swarm`  
> **Docs**: `docs/soulbeet-integration-overview.md`, `docs/soulbeet-api-spec.md`  
> **Timeline**: Phase 5 (4-6 weeks)

#### Phase 5A: slskd Compatibility Layer

- [ ] **T-700**: Implement GET /api/info compatibility endpoint
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Return basic info/health in slskd format with impl=slskdn marker.

- [ ] **T-701**: Implement POST /api/search compatibility endpoint
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Accept slskd search format, perform Soulseek search, optionally enrich with MBID/fingerprint data, return results in slskd format.

- [ ] **T-702**: Implement POST /api/downloads compatibility endpoint
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Accept slskd download format, map to internal transfers/jobs, return download IDs in slskd format.

- [ ] **T-703**: Implement GET /api/downloads compatibility endpoint
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: List active/known downloads in slskd format. Map internal job/transfer states to slskd status fields.

#### Phase 5B: slskdn-Native Job APIs

- [ ] **T-704**: Implement GET /api/slskdn/capabilities endpoint
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Return impl=slskdn, version, and feature list for client detection.

- [ ] **T-705**: Implement POST /api/jobs/mb-release endpoint
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Accept MB Release ID, target_dir, tracks, constraints. Fetch MB tracklist, plan per-track multi-swarm downloads, write to target_dir.

- [ ] **T-706**: Implement POST /api/jobs/discography endpoint
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Accept MB Artist ID + profile. Resolve discography, spawn mb_release sub-jobs, aggregate progress.

- [ ] **T-707**: Implement POST /api/jobs/label-crate endpoint
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Accept label name/MB Label ID + limit. Compute popular releases, spawn mb_release sub-jobs.

- [ ] **T-708**: Implement GET /api/jobs and GET /api/jobs/{id} endpoints
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: List/inspect jobs with common representation: id, type, status, spec, progress, created_at, updated_at, error.

#### Phase 5C: Optional Advanced APIs

- [ ] **T-709**: Implement POST /api/slskdn/warm-cache/hints endpoint
  - Status: Not started
  - Priority: P3
  - Branch: experimental/brainz
  - Notes: Accept MB Release/Artist/Label IDs as popularity hints for warm cache module.

- [ ] **T-710**: Implement GET /api/slskdn/library/health endpoint
  - Status: Not started
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Return path-scoped library health summary with suspected transcodes, non-canonical variants, incomplete releases, and detailed issue list.

#### Phase 5D: Soulbeet Client Integration

- [ ] **T-711**: Document Soulbeet client modifications for slskdn detection
  - Status: Not started
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Document how Soulbeet should call /api/slskdn/capabilities to detect slskdn and enable advanced mode.

- [ ] **T-712**: Create Soulbeet integration test suite
  - Status: Not started
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Test compat mode (Soulbeet unchanged) and advanced mode (MBID job APIs).

---

### Phase 6: Virtual Soulfind Mesh & Disaster Mode

> **Branch**: `experimental/virtual-soulfind`  
> **Docs**: `docs/virtual-soulfind-mesh-architecture.md`, `docs/phase6-*.md`  
> **Timeline**: Phase 6 (16-20 weeks)

#### Phase 6A: Capture & Normalization Pipeline

- [ ] **T-800**: Implement Soulseek traffic observer
  - Status: Not started
  - Priority: P1
  - Branch: experimental/virtual-soulfind
  - Notes: Passively monitor Soulseek searches and transfers, extract metadata for normalization

- [ ] **T-801**: Build MBID normalization pipeline
  - Status: Not started
  - Priority: P1
  - Branch: experimental/virtual-soulfind
  - Notes: Convert observed files to AudioVariant records via fingerprinting + AcoustID + quality scoring

- [ ] **T-802**: Implement username pseudonymization
  - Status: Not started
  - Priority: P1
  - Branch: experimental/virtual-soulfind
  - Notes: Map Soulseek usernames to overlay peer IDs for privacy

- [ ] **T-803**: Create observation database schema
  - Status: Not started
  - Priority: P2
  - Branch: experimental/virtual-soulfind
  - Notes: Optional persistence of raw observations for debugging and replay

- [ ] **T-804**: Add privacy controls and data retention
  - Status: Not started
  - Priority: P1
  - Branch: experimental/virtual-soulfind
  - Notes: Configuration for anonymization levels, retention policies, opt-out

#### Phase 6B: Shadow Index Over DHT

- [ ] **T-805**: Implement DHT key derivation
  - Status: Not started
  - Priority: P1
  - Branch: experimental/virtual-soulfind
  - Notes: Map MBIDs and scenes to DHT keys with namespace prefixes

- [ ] **T-806**: Define shadow index shard format
  - Status: Not started
  - Priority: P1
  - Branch: experimental/virtual-soulfind
  - Notes: Compact MessagePack/Protobuf format for DHT values (peer hints, canonical variants)

- [ ] **T-807**: Build shadow index builder service
  - Status: Not started
  - Priority: P1
  - Branch: experimental/virtual-soulfind
  - Notes: Aggregate observations into shards per MBID

- [ ] **T-808**: Implement shard publisher
  - Status: Not started
  - Priority: P1
  - Branch: experimental/virtual-soulfind
  - Notes: Periodic background task to publish shards to DHT

- [ ] **T-809**: Implement DHT query interface
  - Status: Not started
  - Priority: P1
  - Branch: experimental/virtual-soulfind
  - Notes: Query DHT for MBID → peer hints, decode shards

- [ ] **T-810**: Add shard merging logic
  - Status: Not started
  - Priority: P1
  - Branch: experimental/virtual-soulfind
  - Notes: Combine shards from multiple DHT peers for comprehensive view

- [ ] **T-811**: Implement TTL and eviction policy
  - Status: Not started
  - Priority: P1
  - Branch: experimental/virtual-soulfind
  - Notes: Auto-expire old shards, republish active content

- [ ] **T-812**: Add DHT write rate limiting
  - Status: Not started
  - Priority: P1
  - Branch: experimental/virtual-soulfind
  - Notes: Prevent DHT spam, respect etiquette, max shards per interval

#### Phase 6C: Scenes / Micro-Networks

- [ ] **T-813**: Implement scene management service
  - Status: Not started
  - Priority: P1
  - Branch: experimental/virtual-soulfind
  - Notes: Join/leave scenes, scene metadata, DHT announcements

- [ ] **T-814**: Add scene DHT announcements
  - Status: Not started
  - Priority: P1
  - Branch: experimental/virtual-soulfind
  - Notes: Publish scene membership to DHT

- [ ] **T-815**: Build scene membership tracking
  - Status: Not started
  - Priority: P1
  - Branch: experimental/virtual-soulfind
  - Notes: Track which scenes peer participates in, update on join/leave

- [ ] **T-816**: Implement overlay pubsub for scenes
  - Status: Not started
  - Priority: P1
  - Branch: experimental/virtual-soulfind
  - Notes: Real-time scene gossip and chat over overlay connections

- [ ] **T-817**: Add scene-scoped job creation
  - Status: Not started
  - Priority: P2
  - Branch: experimental/virtual-soulfind
  - Notes: Label crate and discovery jobs scoped to specific scenes

- [ ] **T-818**: Build scene UI
  - Status: Not started
  - Priority: P2
  - Branch: experimental/virtual-soulfind
  - Notes: React components for scene list, join, leave, metadata display

- [ ] **T-819**: Add scene chat (optional)
  - Status: Not started
  - Priority: P3
  - Branch: experimental/virtual-soulfind
  - Notes: Overlay pubsub messages for scene-based chat, signed and rate-limited

- [ ] **T-820**: Implement scene moderation
  - Status: Not started
  - Priority: P2
  - Branch: experimental/virtual-soulfind
  - Notes: Local mute/block for scene participants, no global bans

#### Phase 6D: Disaster Mode & Failover

- [ ] **T-821**: Implement Soulseek health monitor
  - Status: Not started
  - Priority: P1
  - Branch: experimental/virtual-soulfind
  - Notes: Track server health (healthy/degraded/unavailable), detect bans and outages

- [ ] **T-822**: Build disaster mode coordinator
  - Status: Not started
  - Priority: P1
  - Branch: experimental/virtual-soulfind
  - Notes: Activate/deactivate disaster mode, switch resolvers to mesh-only

- [ ] **T-823**: Implement mesh-only search
  - Status: Not started
  - Priority: P1
  - Branch: experimental/virtual-soulfind
  - Notes: MBID resolution → DHT query → overlay descriptors (no Soulseek server)

- [ ] **T-824**: Implement mesh-only transfers
  - Status: Not started
  - Priority: P1
  - Branch: experimental/virtual-soulfind
  - Notes: Overlay multi-swarm only, no legacy Soulseek connections

- [ ] **T-825**: Add scene-based peer discovery
  - Status: Not started
  - Priority: P2
  - Branch: experimental/virtual-soulfind
  - Notes: Fallback to scene queries when DHT MBID lookups sparse

- [ ] **T-826**: Build disaster mode UI indicator
  - Status: Not started
  - Priority: P1
  - Branch: experimental/virtual-soulfind
  - Notes: Clear visual indicator when mesh-only active

- [ ] **T-827**: Add disaster mode configuration
  - Status: Not started
  - Priority: P1
  - Branch: experimental/virtual-soulfind
  - Notes: Auto vs forced modes, thresholds, behavior toggles

- [ ] **T-828**: Implement graceful degradation
  - Status: Not started
  - Priority: P2
  - Branch: experimental/virtual-soulfind
  - Notes: Handle partial server availability, hybrid operation

- [ ] **T-829**: Add disaster mode telemetry
  - Status: Not started
  - Priority: P2
  - Branch: experimental/virtual-soulfind
  - Notes: Track disaster events, recovery times, mesh performance

- [ ] **T-830**: Build recovery logic
  - Status: Not started
  - Priority: P1
  - Branch: experimental/virtual-soulfind
  - Notes: Re-enable Soulseek when server returns, smooth transition

#### Phase 6E: Integration & Polish

- [ ] **T-831**: Integrate shadow index with job resolvers
  - Status: Not started
  - Priority: P1
  - Branch: experimental/virtual-soulfind
  - Notes: Use shadow index hints in MB Release, discography, label crate jobs

- [ ] **T-832**: Integrate scenes with label crate jobs
  - Status: Not started
  - Priority: P2
  - Branch: experimental/virtual-soulfind
  - Notes: Scene-scoped crates, prioritize scene peers

- [ ] **T-833**: Integrate disaster mode with rescue mode
  - Status: Not started
  - Priority: P1
  - Branch: experimental/virtual-soulfind
  - Notes: In disaster mode, all transfers are "rescue" (mesh-only)

- [ ] **T-834**: Perform privacy audit
  - Status: Not started
  - Priority: P1
  - Branch: experimental/virtual-soulfind
  - Notes: Ensure username anonymization, no path leaks, DHT privacy

- [ ] **T-835**: Optimize DHT query performance
  - Status: Not started
  - Priority: P2
  - Branch: experimental/virtual-soulfind
  - Notes: Cache DHT lookups, batch queries, prefetch hot MBIDs

- [ ] **T-836**: Build mesh configuration UI
  - Status: Not started
  - Priority: P2
  - Branch: experimental/virtual-soulfind
  - Notes: React settings panel for mesh, shadow index, scenes, disaster mode

- [ ] **T-837**: Add telemetry dashboard
  - Status: Not started
  - Priority: P2
  - Branch: experimental/virtual-soulfind
  - Notes: Shadow index stats, disaster events, scene activity

- [ ] **T-838**: Write user documentation
  - Status: Not started
  - Priority: P2
  - Branch: experimental/virtual-soulfind
  - Notes: User guide for disaster mode, scenes, privacy settings

- [ ] **T-839**: Create integration test suite
  - Status: Not started
  - Priority: P1
  - Branch: experimental/virtual-soulfind
  - Notes: Full disaster mode simulation, scene coordination, shadow index accuracy

- [ ] **T-840**: Perform load testing
  - Status: Not started
  - Priority: P2
  - Branch: experimental/virtual-soulfind
  - Notes: DHT scalability, shard size limits, overlay throughput at scale

#### Phase 6X: Legacy Client Compatibility Bridge (Optional)

- [ ] **T-850**: Implement bridge service lifecycle
  - Status: Not started
  - Priority: P2
  - Branch: experimental/virtual-soulfind
  - Notes: Start/stop local Soulfind instance, health checks

- [ ] **T-851**: Create Soulfind proxy mode (fork/patch)
  - Status: Not started
  - Priority: P2
  - Branch: experimental/virtual-soulfind
  - Notes: Extend Soulfind with PROXY_MODE env var, forward operations to slskdn

- [ ] **T-852**: Build bridge API endpoints
  - Status: Not started
  - Priority: P2
  - Branch: experimental/virtual-soulfind
  - Notes: /api/bridge/search, /api/bridge/download, /api/bridge/rooms

- [ ] **T-853**: Implement MBID resolution from legacy queries
  - Status: Not started
  - Priority: P2
  - Branch: experimental/virtual-soulfind
  - Notes: Parse "artist album" queries, resolve to MBIDs, query shadow index

- [ ] **T-854**: Add filename synthesis from variants
  - Status: Not started
  - Priority: P2
  - Branch: experimental/virtual-soulfind
  - Notes: Generate friendly filenames for mesh variants shown to legacy clients

- [ ] **T-855**: Implement peer ID anonymization
  - Status: Not started
  - Priority: P2
  - Branch: experimental/virtual-soulfind
  - Notes: Map overlay peer IDs to "mesh-peer-abc123" usernames for legacy display

- [ ] **T-856**: Add room → scene mapping
  - Status: Not started
  - Priority: P2
  - Branch: experimental/virtual-soulfind
  - Notes: Map legacy room names to scene DHT keys, proxy chat to overlay pubsub

- [ ] **T-857**: Implement transfer progress proxying
  - Status: Not started
  - Priority: P2
  - Branch: experimental/virtual-soulfind
  - Notes: Show mesh multi-swarm progress in legacy client

- [ ] **T-858**: Build bridge configuration UI
  - Status: Not started
  - Priority: P3
  - Branch: experimental/virtual-soulfind
  - Notes: Settings for bridge port, auth, client limits

- [ ] **T-859**: Add bridge status dashboard
  - Status: Not started
  - Priority: P3
  - Branch: experimental/virtual-soulfind
  - Notes: Show connected legacy clients, proxied requests, mesh benefits

- [ ] **T-860**: Create Nicotine+ integration tests
  - Status: Not started
  - Priority: P2
  - Branch: experimental/virtual-soulfind
  - Notes: Automated tests with real Nicotine+ client against bridge

---

### Phase 8: Code Quality & Refactoring

> **Branch**: `experimental/brainz`  
> **Docs**: `docs/phase8-refactoring-design.md`  
> **Timeline**: Phase 8 (8-12 weeks post-implementation)

#### Stage 1: Mesh APIs with Power Preserved (Weeks 1-2)

- [ ] **T-1000**: Create namespace structure
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Create Slskdn.Swarm, Slskdn.Mesh (with DHT/, Overlay/, Advanced/), Slskdn.Security, Slskdn.Brainz, Slskdn.Integrations

- [ ] **T-1001**: Define IMeshDirectory + IMeshAdvanced
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Dual API - IMeshDirectory for normal use, IMeshAdvanced for power features/experiments

- [ ] **T-1002**: Add MeshOptions.TransportPreference
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: DhtFirst (default), Mirrored, OverlayFirst - encourage DHT usage

- [ ] **T-1003**: Implement MeshTransportService with configurable preference
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Handle DHT-first, mirrored, overlay-first strategies; NAT, UPnP, TLS

#### Stage 2: Job Pipeline (Weeks 3-4)

- [ ] **T-1030**: Implement IMetadataJob abstraction
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: IMetadataJob interface with AlbumBackfillJob, DiscographyJob, RepairMissionJob, NetworkStressTestJob

- [ ] **T-1031**: Create MetadataJobRunner
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: BackgroundService with Channel-based job queue and status tracking

- [ ] **T-1034**: Convert metadata tasks to jobs
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Replace fire-and-forget async calls with job enqueueing

- [ ] **T-1035**: Add network simulation job support
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Jobs for mesh stress tests, NAT experiments, disaster mode simulations

#### Stage 3: Swarm Orchestrator (Weeks 5-6)

- [ ] **T-1010**: Implement SwarmDownloadOrchestrator
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: BackgroundService with Channel-based job queue, replace scattered Task.Run

- [ ] **T-1011**: Create SwarmJob model
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: SwarmJob, SwarmFile, SwarmChunk, SwarmSource with proper typing

- [ ] **T-1012**: Implement IVerificationEngine
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Centralize chunk verification (hash, consensus, reputation) with caching

- [ ] **T-1013**: Replace ad-hoc Task.Run
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Route all swarm operations through orchestrator

- [ ] **T-1014**: Integrate with IMeshDirectory and IMeshAdvanced
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Use IMeshDirectory for normal discovery, IMeshAdvanced for advanced strategies

#### Stage 4: Security Policy Engine (Week 7)

- [ ] **T-1040**: Implement ISecurityPolicyEngine
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Policy engine abstraction with SecurityContext, SecurityDecision

- [ ] **T-1041**: Create CompositeSecurityPolicy
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Compose multiple policies with short-circuit evaluation

- [ ] **T-1042**: Implement individual policies
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: NetworkGuardPolicy, ReputationPolicy, ConsensusPolicy, ContentSafetyPolicy, HoneypotPolicy, NatAbuseDetectionPolicy

- [ ] **T-1043**: Replace inline security checks
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Remove scattered security logic from controllers, use policy engine

#### Stage 5: Typed Configuration (Week 8)

- [ ] **T-1050**: Create strongly-typed options
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: SwarmOptions, MeshOptions, SecurityOptions, BrainzOptions

- [ ] **T-1051**: Wire options via IOptions<T>
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: DI registration for all options classes

- [ ] **T-1052**: Remove direct IConfiguration access
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Replace magic strings with typed options everywhere

#### Stage 6: Codec Analyzers (Week 9)

- [ ] **T-1032**: Implement codec analyzers
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: IAudioAnalyzer with FlacAnalyzer, Mp3Analyzer, OpusAnalyzer, AacAnalyzer (from Phase 2-Extended)

- [ ] **T-1033**: Create unified BrainzClient
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Single client for MB/AcoustID/Soulbeet with caching, rate limiting, backoff

#### Stage 7: Testability (Week 10)

- [ ] **T-1060**: Eliminate static singletons
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Remove static state, make everything injectable

- [ ] **T-1061**: Add interfaces for subsystems
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Ensure all major services have interfaces for mocking

- [ ] **T-1062**: Constructor injection cleanup
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: No `new HttpClient()`, no object creation inside methods

#### Stage 8: Test Infrastructure (Week 11)

- [ ] **T-1070**: Implement Soulfind test harness
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: SoulfindRunner for integration tests (from Phase 7 design)

- [ ] **T-1071**: Implement MeshSimulator with DHT-first + disaster mode
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: In-process mesh simulation with DHT-first discovery, disaster mode, NAT edge case support

- [ ] **T-1072**: Write integration-soulseek tests
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Multi-client tests with Alice/Bob/Carol topology

- [ ] **T-1073**: Write integration-mesh tests
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: DHT-heavy tests, NAT edge cases, disaster mode continuation

#### Stage 9: Cleanup (Week 12)

- [ ] **T-1080**: Remove dead code
  - Status: Not started
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Kill unused enums, flags, half-implemented concepts, proto-classes

- [ ] **T-1081**: Normalize naming
  - Status: Not started
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Consistent vocabulary: Swarm/Mesh/Brainz/Security terms

- [ ] **T-1082**: Move narrative comments to docs
  - Status: Not started
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Keep code comments concise, details in docs/

- [ ] **T-1083**: Collapse forwarding classes
  - Status: Not started
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Remove unnecessary abstraction layers

---

### Phase 7: Testing Strategy with Soulfind & Mesh Simulator

> **Branch**: `experimental/brainz` (tests live alongside features)  
> **Docs**: `docs/phase7-testing-strategy-soulfind.md`  
> **Timeline**: Phase 7 (4-6 weeks)

#### Phase 7A: Test Harness Infrastructure

- [ ] **T-900**: Implement Soulfind test harness
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: SoulfindRunner class for starting/stopping local Soulfind, binary discovery, ephemeral port allocation, readiness detection.

- [ ] **T-901**: Implement slskdn test client harness
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: SlskdnTestClient class for isolated test instances, config directory isolation, share directory configuration, API wrappers.

- [ ] **T-902**: Create audio test fixtures
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Small deterministic audio files (FLAC, MP3, Opus, AAC), known good and transcode variants, metadata sidecar with expected scores.

- [ ] **T-903**: Create MusicBrainz stub responses
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: JSON fixtures for MB API responses, mock IMusicBrainzClient, test helper to inject mock into DI.

#### Phase 7B: Protocol & Integration Tests

- [ ] **T-904**: Implement L1 protocol contract tests
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Test suite for basic Soulseek protocol: login/handshake, keepalive, search, rooms, browse.

- [ ] **T-905**: Implement L2 multi-client integration tests
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Alice/Bob/Carol topology, scenarios for search/download/capture/rooms, assertions on MBID mapping and quality scores.

- [ ] **T-906**: Implement mesh simulator
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: MeshSimulator class for in-process DHT/overlay, SimulatedNode with fake library, network partition/message drop simulation.

#### Phase 7C: Disaster Mode & Mesh-Only Tests

- [ ] **T-907**: Implement L3 disaster mode tests
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Soulfind-assisted disaster drills, kill Soulfind mid-transfer, verify mesh takeover and disaster mode activation.

- [ ] **T-908**: Implement L3 mesh-only tests
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Pure mesh simulation (no Soulfind), discography job across mesh, repair mission tests, DHT/overlay-only discovery.

- [ ] **T-909**: Add CI test categorization
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Test traits/categories for L0/L1/L2/L3, CI configuration, environment variable detection for Soulfind.

- [ ] **T-910**: Add test documentation
  - Status: Not started
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: README for running integration tests locally, instructions for Soulfind setup, troubleshooting guide.

#### Phase 7D: Feature-Specific Integration Tests

- [ ] **T-911**: Implement test result visualization
  - Status: Not started
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Test report generation (HTML/Markdown), coverage reports, performance benchmarks for mesh operations.

- [ ] **T-912**: Add rescue mode integration tests
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Tests for underperforming transfer detection, overlay rescue activation, mixed Soulseek+overlay completion.

- [ ] **T-913**: Add canonical selection integration tests
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Verify canonical variant preference, quality score-based source selection, cross-codec deduplication with real files.

- [ ] **T-914**: Add library health integration tests
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: End-to-end library scanning, issue detection validation (transcodes, non-canonical), remediation job creation/execution.

- [ ] **T-915**: Performance benchmarking suite
  - Status: Not started
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Benchmark tests for DHT query latency, overlay throughput, canonical stats aggregation, mesh simulation at scale (100+ nodes).

---

### High Priority

- [ ] **T-001**: Persistent Room/Chat Tabs
  - Status: Not started
  - Priority: High
  - Branch: TBD
  - Related: `TODO.md`, Browse tabs implementation
  - Notes: Implement tabbed interface like Browse currently has. Reuse `Browse.jsx`/`BrowseSession.jsx` patterns.

- [ ] **T-002**: Scheduled Rate Limits
  - Status: Not started
  - Priority: High
  - Branch: TBD
  - Related: slskd #985
  - Notes: Day/night upload/download speed schedules like qBittorrent

### Medium Priority

- [ ] **T-003**: Download Queue Position Polling
  - Status: Not started
  - Priority: Medium
  - Related: slskd #921
  - Notes: Auto-refresh queue positions for queued files

- [ ] **T-004**: Visual Group Indicators
  - Status: Not started
  - Priority: Medium
  - Related: slskd #745
  - Notes: Icons in search results for users in your groups

- [ ] **T-005**: Traffic Ticker
  - Status: Not started
  - Priority: Medium
  - Related: slskd discussion #547
  - Notes: Real-time upload/download activity feed in UI

### Low Priority

- [ ] **T-006**: Create Chat Rooms from UI
  - Status: Not started
  - Priority: Low
  - Related: slskd #1258
  - Notes: Create public/private rooms from web interface

- [ ] **T-007**: Predictable Search URLs
  - Status: Not started
  - Priority: Low
  - Related: slskd #1170
  - Notes: Bookmarkable search URLs for browser integration

---

## Packaging & Distribution

- [ ] **T-010**: TrueNAS SCALE Apps
  - Status: Not started
  - Priority: High
  - Notes: Helm chart or ix-chart format

- [ ] **T-011**: Synology Package Center
  - Status: Not started
  - Priority: High
  - Notes: SPK format, cross-compile for ARM/x86

- [ ] **T-012**: Homebrew Formula
  - Status: Not started
  - Priority: High
  - Notes: macOS package manager support

- [ ] **T-013**: Flatpak (Flathub)
  - Status: Not started
  - Priority: High
  - Notes: Universal Linux packaging

---

## Completed Tasks

### Multi-Source & DHT Infrastructure (experimental/multi-source-swarm)

- [x] **T-200**: Multi-Source Chunked Downloads
  - Status: Done (experimental branch)
  - Branch: experimental/multi-source-swarm
  - Notes: Parallel chunk downloads from multiple peers, content verification (SHA256), FLAC STREAMINFO parser, LimitedWriteStream for partial downloads

- [x] **T-201**: BitTorrent DHT Rendezvous Layer
  - Status: Done (experimental branch)
  - Branch: experimental/multi-source-swarm
  - Notes: Decentralized peer discovery using BitTorrent DHT, beacon/seeker model, overlay TCP connections, TLS 1.3 encrypted mesh

- [x] **T-202**: Mesh Overlay Network & Hash Sync
  - Status: Done (experimental branch)
  - Branch: experimental/multi-source-swarm
  - Notes: Epidemic sync protocol for hash database, TLS-encrypted P2P connections, certificate pinning (TOFU), SecureMessageFramer with length-prefixed framing

- [x] **T-203**: Capability Discovery System
  - Status: Done (experimental branch)
  - Branch: experimental/multi-source-swarm
  - Notes: PeerCapabilityFlags, UserInfo tag parsing, version detection, REST API endpoints

- [x] **T-204**: Local Hash Database (HashDb)
  - Status: Done (experimental branch)
  - Branch: experimental/multi-source-swarm
  - Notes: SQLite-based content-addressed hash storage, FLAC inventory, peer tracking, mesh peer state

- [x] **T-205**: Security Hardening Framework
  - Status: Done (experimental branch)
  - Branch: experimental/multi-source-swarm
  - Notes: NetworkGuard rate limiting, ViolationTracker auto-bans, PathGuard traversal prevention, PeerReputation scoring, ContentSafety magic bytes, ByzantineConsensus voting, EntropyMonitor, FingerprintDetection, Honeypots

- [x] **T-206**: Source Discovery & Verification
  - Status: Done (experimental branch)
  - Branch: experimental/multi-source-swarm
  - Notes: Automatic peer discovery for identical files, content verification service, FLAC audio MD5 matching, multi-source controller API

### HashDb & Passive Discovery (dev-2025-12-09)

- [x] **T-110**: HashDb Schema Migration System
  - Status: Done (dev-2025-12-09)
  - Branch: experimental/multi-source-swarm
  - Notes: Versioned SQLite migrations for HashDb, extends schema for full file hashes, audio fingerprints, MusicBrainz IDs, and FileSources table

- [x] **T-111**: Passive FLAC Discovery & Backfill
  - Status: Done (dev-2025-12-09)
  - Branch: experimental/multi-source-swarm
  - Notes: Passively discover FLACs from search results, peer interactions. Manual backfill UI with pagination. Network-health-first design.

- [x] **T-112**: UI Polish - Sticky Status Bar & Footer
  - Status: Done (dev-2025-12-09)
  - Branch: experimental/multi-source-swarm
  - Notes: Status bar fixed below nav, opaque colorful footer with parent project attribution, appears on all pages including login

- [x] **T-113**: Release Notes & AUR Checksum Fix
  - Status: Done (dev-2025-12-09)
  - Branch: experimental/multi-source-swarm
  - Notes: Established convention for release notes on GitHub releases. Fixed AUR PKGBUILD to keep SKIP for binary checksums to prevent yay -Syu validation failures.

### Stable Releases (main branch)

- [x] **T-100**: Auto-Replace Stuck Downloads
  - Status: Done (Release .1)
  - Notes: Finds alternatives for stuck/failed downloads

- [x] **T-101**: Wishlist/Background Search
  - Status: Done (Release .2)
  - Notes: Save searches, auto-run, auto-download

- [x] **T-102**: Smart Result Ranking
  - Status: Done (Release .4)
  - Notes: Speed, queue, slots, history weighted

- [x] **T-103**: User Download History Badge
  - Status: Done (Release .4)
  - Notes: Green/blue/orange badges

- [x] **T-104**: Advanced Search Filters
  - Status: Done (Release .5)
  - Notes: Modal with include/exclude, size, bitrate

- [x] **T-105**: Block Users from Search Results
  - Status: Done (Release .5)
  - Notes: Hide blocked users toggle

- [x] **T-106**: User Notes & Ratings
  - Status: Done (Release .6)
  - Notes: Personal notes per user

- [x] **T-107**: Multiple Destination Folders
  - Status: Done (Release .2)
  - Notes: Choose destination per download

- [x] **T-108**: Tabbed Browse Sessions
  - Status: Done (Release .10)
  - Notes: Multiple browse tabs, persistent

- [x] **T-109**: Push Notifications
  - Status: Done (Release .8)
  - Notes: Ntfy, Pushover, Pushbullet

---

*Last updated: December 9, 2025*

