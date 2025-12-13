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

- [x] **T-425**: Implement audio_sketch_hash (PCM-window hash)
  - Status: Done
  - Completed: 2025-12-10
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Decode short PCM windows from arbitrary audio files, downsample to mono 4 kHz, hash with xxHash64.

- [x] **T-426**: Implement cross-codec deduplication logic
  - Status: Done
  - Completed: 2025-12-10
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Query variants by MB Recording ID + audio_sketch_hash, deduplicate across codec boundaries. Debug API endpoint.

- [x] **T-427**: Implement analyzer version migration
  - Status: Done
  - Completed: 2025-12-10
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Background job to detect stale analyzer_version, recompute quality scores from raw features. CLI command.

- [x] **T-428**: Update CanonicalStatsService with codec-specific logic
  - Status: Done
  - Completed: 2025-12-10
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Use codec-specific stream hashes for deduplication, prefer lossless over lossy explicitly, use audio_sketch_hash.

- [x] **T-429**: Add codec-specific stats to Library Health
  - Status: Done
  - Completed: 2025-12-10
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Detect codec mismatches, flag transcodes using new analyzer results, suggest codec-specific canonical replacements.

- [x] **T-430**: Unit tests for codec analyzers
  - Status: Done
  - Completed: 2025-12-10
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

- [x] **T-500**: Build MB artist release graph service
  - Status: Done
  - Completed: 2025-12-10
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Fetch and cache Release Groups for MB Artist ID. Fetch Releases under each group (albums, EPs, etc.) from MusicBrainz API.

 - [x] **T-501**: Define discography profiles
  - Status: Done
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Core/extended/all profiles mapped to release-group types (albums, EPs, live, singles, comps, soundtracks, remixes).

- [x] **T-502**: Implement discography job type
  - Status: Done
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Discography job creates per-release sub-jobs, persists progress in HashDb, aggregates status.

#### Phase 3B: Label Crate Mode

- [x] **T-503**: Build label presence aggregation
  - Status: Done
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Aggregate label counts from AlbumTargets; exposed via HashDb helpers.

- [x] **T-504**: Implement label crate job type
  - Status: Done
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Label crate jobs with per-release sub-jobs; API endpoints to create and query.

#### Phase 3C: Local-Only Peer Reputation

- [x] **T-505**: Implement peer reputation metric collection
  - Status: Done
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Peer metrics persisted in HashDb; collects success/fail/timeout/corrupt events with EMAs.

- [x] **T-506**: Build reputation scoring algorithm
  - Status: Done
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Reputation score with decay (half-life), weighted updates per outcome, stored with metrics.

- [x] **T-507**: Integrate reputation into swarm scheduling
  - Status: Done
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Chunk scheduler filters low-rep peers and uses reputation-weighted cost function.

#### Phase 3D: Mesh-Level Fairness Governor

- [x] **T-508**: Implement traffic accounting
  - Status: Done
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: TrafficStats table + HashDb API + service to accumulate overlay/Soulseek upload/download totals.

- [x] **T-509**: Build fairness constraint enforcement
  - Status: Done
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: FairnessGuard computes ratios and throttle decisions from traffic totals; DI-registered.

- [x] **T-510**: Add contribution summary API/UI (optional)
  - Status: Done
  - Priority: P3
  - Branch: experimental/brainz
  - Notes: /api/v0/fairness/summary returns throttle flag, ratios, and traffic totals for display.

---

### Phase 4: Job Manifests, Session Traces & Advanced Features

> **Branch**: `experimental/brainz`  
> **Timeline**: Phase 4 (6-8 weeks)

#### Phase 4A: YAML Job Manifests

- [x] **T-600**: Define YAML job manifest schema
  - Status: Done
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Schema + C# models/validator for mb_release, discography, label_crate, multi-source manifests.

- [x] **T-601**: Implement job manifest export
  - Status: Done
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Manifest service writes YAML to jobs/active or jobs/completed with validation.

- [x] **T-602**: Build job manifest import
  - Status: Done
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Manifest service imports/validates YAML manifests via validator.

#### Phase 4B: Session Traces / Swarm Debugging

- [x] **T-603**: Define swarm event model
  - Status: Done
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Event model with type/source/backend, job/track/variant/peer, chunk and byte context, timestamp/error fields.

- [x] **T-604**: Implement event persistence and rotation
  - Status: Done
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: File-based SwarmEventStore with JSONL per job, rotation at 5MB, TTL 7d, max 200 jobs.

- [x] **T-605**: Build session trace summaries
  - Status: Done
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Trace summarizer + API endpoint; per-job bytes by source/backend, event counts, peer contributions, rescue flag.

#### Phase 4C: Warm Cache Nodes (Optional)

- [x] **T-606**: Implement warm cache configuration
  - Status: Done
  - Priority: P3
  - Branch: experimental/brainz
  - Notes: WarmCache options added with enabled/max_storage_gb/min_popularity_threshold validation; ready for caching logic wiring.

- [x] **T-607**: Build popularity detection for caching
  - Status: Done
  - Priority: P3
  - Branch: experimental/brainz
  - Notes: Warm cache popularity store + service; counts per contentId with top-popular query hook.

- [x] **T-608**: Add cache fetch, serve, evict logic
  - Status: Done (scaffold)
  - Priority: P3
  - Branch: experimental/brainz
  - Notes: Warm cache entry store + service with register/touch/list and eviction scaffold (LRU, capacity via MaxStorageGb, pinned support). Actual fetch/serve wiring still pending future tasks.

#### Phase 4D: Playback-Aware Swarming (Optional)

- [x] **T-609**: Implement playback feedback API
  - Status: Done (experimental)
  - Priority: P3
  - Branch: experimental/brainz
  - Notes: POST /api/v0/playback/feedback accepts jobId/trackId/position/buffer; stores feedback.

- [x] **T-610**: Build priority zones and playback-aware scheduling
  - Status: Done (hints)
  - Priority: P3
  - Branch: experimental/brainz
  - Notes: Priority hint service (high/mid/low) derived from feedback; returned in feedback response. Scheduler wiring TBD.

- [x] **T-611**: Add streaming diagnostics
  - Status: Done (scaffold)
  - Priority: P3
  - Branch: experimental/brainz
  - Notes: Playback diagnostics endpoint returns latest feedback + priority; future work to include peer/underrun detail.

---

### Phase 8: MeshCore & MediaCore Foundation (Research)

> **Branch**: TBD (research phase)  
> **Docs**: `FORK_VISION.md` (Phase 8)  
> **Dependencies**: Phases 1-7  
> **Timeline**: Research phase

#### Phase 8A: Overlay DHT Foundation

- [ ] **T-900**: Design DHT architecture and key patterns
  - Status: Not started
  - Priority: P4 (research)
  - Branch: TBD
  - Notes: Kademlia-style DHT, define key patterns for pod metadata/membership/shadow index/scenes.

- [ ] **T-901**: Implement Ed25519 signed identity system
  - Status: Not started
  - Priority: P4 (research)
  - Branch: TBD
  - Notes: Keypair generation, PeerId from public key fingerprint, signature/verification for DHT entries.

- [ ] **T-902**: Build DHT node and routing table
  - Status: Not started
  - Priority: P4 (research)
  - Branch: TBD
  - Notes: K-bucket routing, node distance, FIND_NODE/STORE/FIND_VALUE RPCs.

- [ ] **T-903**: Implement DHT storage with TTL and signatures
  - Status: Not started
  - Priority: P4 (research)
  - Branch: TBD
  - Notes: Key-value storage, TTL-based expiry, signature validation on store.

- [ ] **T-904**: Add DHT bootstrap and discovery
  - Status: Not started
  - Priority: P4 (research)
  - Branch: TBD
  - Notes: Bootstrap nodes, peer discovery, NAT traversal considerations.

#### Phase 8B: Multi-Backend Transfer Architecture

- [ ] **T-905**: Define transfer backend abstraction interface
  - Status: Not started
  - Priority: P4 (research)
  - Branch: TBD
  - Notes: ITransferBackend with Init/Download/Upload/GetPeers methods.

- [ ] **T-906**: Implement native mesh protocol backend
  - Status: Not started
  - Priority: P4 (research)
  - Branch: TBD
  - Notes: Direct peer-to-peer over overlay network.

- [ ] **T-907**: Implement HTTP/WebDAV/S3 backend
  - Status: Not started
  - Priority: P4 (research)
  - Branch: TBD
  - Notes: Cloud storage integration for hybrid scenarios.

- [ ] **T-908**: Implement private BitTorrent backend
  - Status: Not started
  - Priority: P4 (research)
  - Branch: TBD
  - Notes: BT fallback only between known mesh peers, no public DHT/trackers.

- [ ] **T-909**: Build backend selection and failover logic
  - Status: Not started
  - Priority: P4 (research)
  - Branch: TBD
  - Notes: Try native → HTTP → private BT, adaptive selection based on availability.

#### Phase 8C: ContentID & MediaCore

- [ ] **T-910**: Design ContentID abstraction and domain model
  - Status: Not started
  - Priority: P4 (research)
  - Branch: TBD
  - Notes: ContentDomain enum (Audio, extensible), MetadataSource, ExternalId, ContentId class.

- [ ] **T-911**: Implement MediaVariant model and storage
  - Status: Not started
  - Priority: P4 (research)
  - Branch: TBD
  - Notes: Track editions/masterings/cuts, link variants to ContentId, extend HashDb schema.

- [ ] **T-912**: Build metadata facade abstraction
  - Status: Not started
  - Priority: P4 (research)
  - Branch: TBD
  - Notes: IMetadataProvider interface, implementations for MusicBrainz and extensible to other sources.

- [ ] **T-913**: Implement AudioCore domain module
  - Status: Not started
  - Priority: P4 (research)
  - Branch: TBD
  - Notes: Artist/album/track models, discography logic, MB integration via facade.

---

### Phase 9: PodCore - Taste-Based Communities (Research)

> **Branch**: TBD (research phase)  
> **Docs**: `FORK_VISION.md` (Phase 8 - PodCore)  
> **Dependencies**: Phase 8A (DHT), Phase 8C (ContentID)  
> **Timeline**: Research phase

#### Phase 9A: Pod Metadata & Membership

- [x] **T-1000**: Define pod data models (Pod, PodMember, PodRole)
  - Status: Done
  - Priority: P4 (research)
  - Branch: TBD
  - Notes: PodId, DisplayName, Visibility, FocusType, ContentId linkage, Tags.

- [x] **T-1001**: Implement pod creation and metadata publishing
  - Status: Done
  - Priority: P4 (research)
  - Branch: TBD
  - Notes: Create pod, sign metadata, publish to DHT (pod:<PodId>:meta).

- [x] **T-1002**: Build signed membership record system
  - Status: Done
  - Priority: P4 (research)
  - Branch: TBD
  - Notes: PodMembershipRecord with role/ban status/signature, publish to DHT.

- [x] **T-1003**: Implement pod join/leave flows
  - Status: Done
  - Priority: P4 (research)
  - Branch: TBD
  - Notes: Invite flows for private pods, direct join for unlisted/listed, membership record updates.

- [x] **T-1004**: Add pod discovery for listed pods
  - Status: Done
  - Priority: P4 (research)
  - Branch: TBD
  - Notes: DHT keys for discovery (pod:discover:name:<name>, pod:discover:tag:<tag>).

#### Phase 9B: Pod Chat & Messaging

- [x] **T-1005**: Define pod message data model
  - Status: Done
  - Priority: P4 (research)
  - Branch: TBD
  - Notes: PodMessage (ChannelId, MessageId, SenderPeerId, Body, Signature, Kind).

- [x] **T-1006**: Implement decentralized message routing
  - Status: Done
  - Priority: P4 (research)
  - Branch: TBD
  - Notes: Route messages via overlay to pod peers, multi-cast with deduplication.

- [x] **T-1007**: Build local message storage and backfill
  - Status: Done
  - Priority: P4 (research)
  - Branch: TBD
  - Notes: Local SQLite storage, optional peer-to-peer backfill, configurable retention.

- [x] **T-1008**: Add pod channels (general, custom)
  - Status: Done
  - Priority: P4 (research)
  - Branch: TBD
  - Notes: PodChannelId model, DHT keys for channel descriptors.

- [x] **T-1009**: Implement message validation and signature checks
  - Status: Done
  - Priority: P4 (research)
  - Branch: TBD
  - Notes: Validate sender membership, check signatures, enforce ban list.

#### Phase 9C: Content-Linked Pods & Variant Opinions

- [x] **T-1010**: Implement content-linked pod creation
  - Status: Done
  - Priority: P4 (research)
  - Branch: TBD
  - Notes: FocusType=ContentId, associate pod with artist/album/show.

- [x] **T-1011**: Build "collection vs pod" view
  - Status: Done
  - Priority: P4 (research)
  - Branch: TBD
  - Notes: Compare user's collection to pod's canonical discography, show gaps.

- [x] **T-1012**: Define PodVariantOpinion data model
  - Status: Done
  - Priority: P4 (research)
  - Branch: TBD
  - Notes: PodId, ContentId, VariantHash, Score, Note, SenderPeerId, Signature.

- [x] **T-1013**: Implement variant opinion publishing and retrieval
  - Status: Done
  - Priority: P4 (research)
  - Branch: TBD
  - Notes: Publish opinions to DHT (pod:<PodId>:opinions:<ContentId>), query for aggregation.

- [x] **T-1014**: Integrate pod opinions into canonicality engine
  - Status: Done
  - Priority: P4 (research)
  - Branch: TBD
  - Notes: Weight opinions by PodAffinity and trust, feed into QualityScorer/CanonicalStatsService.

#### Phase 9D: Pod Moderation & Security Integration

- [x] **T-1015**: Implement owner/moderator kick/ban actions
  - Status: Done
  - Priority: P4 (research)
  - Branch: TBD
  - Notes: Update membership record with IsBanned=true, sign and publish to DHT.

- [x] **T-1016**: Build PodAffinity scoring (engagement, trust)
  - Status: Done
  - Priority: P4 (research)
  - Branch: TBD
  - Notes: Track activity frequency, calculate engagement score, derive pod-specific trust.

- [x] **T-1017**: Integrate pod trust with SecurityCore
  - Status: Done
  - Priority: P4 (research)
  - Branch: TBD
  - Notes: Extend SecurityContext with PodId/PodRole/PodTrustScore, adjust policies per pod.

- [x] **T-1018**: Add global reputation feed from pod abuse
  - Status: Done
  - Priority: P4 (research)
  - Branch: TBD
  - Notes: Consistent abuse across pods → lower global trust, inform PeerMetricsService.

#### Phase 9E: Pod UI & Safety Guardrails

- [x] **T-1019**: Design pod UI mockups (list, detail, chat, collection views)
  - Status: Done
  - Priority: P4 (research)
  - Branch: TBD
  - Notes: Wireframes for pod browser, join/leave, chat interface, "collection vs pod" dashboard.

- [x] **T-1020**: Implement pod list and detail views
  - Status: Done
  - Priority: P4 (research)
  - Branch: TBD
  - Notes: Browse joined pods, view metadata/members/tags, join/leave buttons.

- [x] **T-1021**: Build pod chat UI with safety guardrails
  - Status: Done
  - Priority: P4 (research)
  - Branch: TBD
  - Notes: Message list, send box, NO auto-linkifying magnets/URLs, no "paste magnet" shortcut.

- [x] **T-1022**: Add "collection vs pod" dashboard integration
  - Status: Done
  - Priority: P4 (research)
  - Branch: TBD
  - Notes: Show gaps, pod's favorite masterings, quality recommendations, NO direct download links.

- [x] **T-1023**: Implement pod-scoped variant opinion UI
  - Status: Done
  - Priority: P4 (research)
  - Branch: TBD
  - Notes: View pod opinions per release, submit your own opinion (hash + score + note).

#### Phase 9F: Soulseek Chat Bridge (Adoption Strategy)

- [x] **T-1024**: Design external binding data model
  - Status: Done
  - Priority: P4 (research)
  - Branch: TBD
  - Notes: PodExternalBinding (Kind, Mode, Identifier), PodChannelKind (Native vs Bound).

- [x] **T-1025**: Implement ISoulseekChatBridge interface
  - Status: Done
  - Priority: P4 (research)
  - Branch: TBD
  - Notes: Join/leave rooms, subscribe to room events, send messages, PM support.

- [x] **T-1026**: Add ExternalBinding to PodMetadata
  - Status: Done
  - Priority: P4 (research)
  - Branch: TBD
  - Notes: Extend Pod data model with optional Soulseek room binding.

- [x] **T-1027**: Implement bound channel creation and mirroring
  - Status: Done
  - Priority: P4 (research)
  - Branch: TBD
  - Notes: Create "soulseek-room" bound channel, mirror messages from Soulseek → Pod (ReadOnly mode).

- [x] **T-1028**: Add two-way mirroring (Mirror mode)
  - Status: Done
  - Priority: P4 (research)
  - Branch: TBD
  - Notes: Send pod messages to Soulseek room, with clear UI indicators and safety guardrails.

- [x] **T-1029**: Build pod-from-room creation flow
  - Status: Done
  - Priority: P4 (research)
  - Branch: TBD
  - Notes: UI to create pod from existing Soulseek room, pre-fill metadata, default to ReadOnly.

- [x] **T-1030**: Add Soulseek identity mapping
  - Status: Done
  - Priority: P4 (research)
  - Branch: TBD
  - Notes: Synthetic PeerIds for Soulseek usernames (soulseek:username), optional verification/linking.

- [x] **T-1031**: Implement bound channel UI with safety indicators
  - Status: Done
  - Priority: P4 (research)
  - Branch: TBD
  - Notes: Visual distinction (badges, colors), clear send-to indicators, disable input for ReadOnly.

---

### Phase 10+: Domain-Specific Apps (Long-Term Research)

> **Branch**: TBD (research phase)  
> **Dependencies**: Phases 8-9 (MeshCore, MediaCore, PodCore)  
> **Timeline**: Long-term research

- [x] **T-1100**: Design Soulbeet (music) app architecture
  - Status: Done
  - Priority: P5 (long-term)
  - Notes: Artist discographies, album completion, pod integration, quality recommendations.

- [x] **T-1101**: Research extensibility for other media domains
  - Status: Done
  - Priority: P5 (long-term)
  - Notes: Architecture patterns for extending ContentID/MediaCore to other media types.

---

### Phase 5: Soulbeet Integration

> **Branch**: `experimental/multi-swarm`  
> **Docs**: `docs/soulbeet-integration-overview.md`, `docs/soulbeet-api-spec.md`  
> **Timeline**: Phase 5 (4-6 weeks)

#### Phase 5A: slskd Compatibility Layer

- [x] **T-700**: Implement GET /api/info compatibility endpoint
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Return basic info/health in slskd format with impl=slskdn marker.

- [x] **T-701**: Implement POST /api/search compatibility endpoint
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Accept slskd search format, perform Soulseek search, optionally enrich with MBID/fingerprint data, return results in slskd format.

- [x] **T-702**: Implement POST /api/downloads compatibility endpoint
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Accept slskd download format, map to internal transfers/jobs, return download IDs in slskd format.

- [x] **T-703**: Implement GET /api/downloads compatibility endpoint
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: List active/known downloads in slskd format. Map internal job/transfer states to slskd status fields.

#### Phase 5B: slskdn-Native Job APIs

- [x] **T-704**: Implement GET /api/slskdn/capabilities endpoint
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Return impl=slskdn, version, and feature list for client detection.

- [x] **T-705**: Implement POST /api/jobs/mb-release endpoint
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Accept MB Release ID, target_dir, tracks, constraints. Fetch MB tracklist, plan per-track multi-swarm downloads, write to target_dir.

- [x] **T-706**: Implement POST /api/jobs/discography endpoint
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Accept MB Artist ID + profile. Resolve discography, spawn mb_release sub-jobs, aggregate progress.

- [x] **T-707**: Implement POST /api/jobs/label-crate endpoint
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Accept label name/MB Label ID + limit. Compute popular releases, spawn mb_release sub-jobs.

- [x] **T-708**: Implement GET /api/jobs and GET /api/jobs/{id} endpoints
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: List/inspect jobs with common representation: id, type, status, spec, progress, created_at, updated_at, error.

#### Phase 5C: Optional Advanced APIs

- [x] **T-709**: Implement POST /api/slskdn/warm-cache/hints endpoint
  - Status: Done
  - Priority: P3
  - Branch: experimental/brainz
  - Notes: Accept MB Release/Artist/Label IDs as popularity hints for warm cache module.

- [x] **T-710**: Implement GET /api/slskdn/library/health endpoint
  - Status: Done
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Return path-scoped library health summary with suspected transcodes, non-canonical variants, incomplete releases, and detailed issue list.

#### Phase 5D: Soulbeet Client Integration

- [x] **T-711**: Document Soulbeet client modifications for slskdn detection
  - Status: Done
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Document how Soulbeet should call /api/slskdn/capabilities to detect slskdn and enable advanced mode.

- [x] **T-712**: Create Soulbeet integration test suite
  - Status: Done
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Test compat mode (Soulbeet unchanged) and advanced mode (MBID job APIs).

---

### Phase 6: Virtual Soulfind Mesh & Disaster Mode

> **Branch**: `experimental/virtual-soulfind`  
> **Docs**: `docs/virtual-soulfind-mesh-architecture.md`, `docs/phase6-*.md`  
> **Timeline**: Phase 6 (16-20 weeks)

#### Phase 6A: Capture & Normalization Pipeline

- [x] **T-800**: Implement Soulseek traffic observer
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: TrafficObserver hooks search results and transfer completions, extracts metadata from paths, feeds normalization pipeline.

- [x] **T-801**: Build MBID normalization pipeline
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: NormalizationPipeline converts observations to AudioVariant via fingerprinting + AcoustID + MB lookups, computes quality scores.

- [x] **T-802**: Implement username pseudonymization
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: UsernamePseudonymizer maps Soulseek usernames to peer IDs with HMAC-SHA256 salt, stores in Pseudonyms table.

- [x] **T-803**: Create observation database schema
  - Status: Done
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: ObservationStore interface with in-memory stub (optional persistence for debugging), migration v15 for Pseudonyms table.

- [x] **T-804**: Add privacy controls and data retention
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: PrivacyControls with anonymization levels (None/Pseudonymized/Aggregate) and retention policies, VirtualSoulfindOptions configuration.

#### Phase 6B: Shadow Index Over DHT

- [x] **T-805**: Implement DHT key derivation
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Map MBIDs and scenes to DHT keys with namespace prefixes

- [x] **T-806**: Define shadow index shard format
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Compact MessagePack/Protobuf format for DHT values (peer hints, canonical variants)

- [x] **T-807**: Build shadow index builder service
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Aggregate observations into shards per MBID

- [x] **T-808**: Implement shard publisher
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Periodic background task to publish shards to DHT

- [x] **T-809**: Implement DHT query interface
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Query DHT for MBID → peer hints, decode shards

- [x] **T-810**: Add shard merging logic
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Combine shards from multiple DHT peers for comprehensive view

- [x] **T-811**: Implement TTL and eviction policy
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Auto-expire old shards, republish active content

- [x] **T-812**: Add DHT write rate limiting
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Prevent DHT spam, respect etiquette, max shards per interval

#### Phase 6C: Scenes / Micro-Networks

- [x] **T-813**: Implement scene management service
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Join/leave scenes, scene metadata, DHT announcements

- [x] **T-814**: Add scene DHT announcements
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Publish scene membership to DHT

- [x] **T-815**: Build scene membership tracking
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Track which scenes peer participates in, update on join/leave

- [x] **T-816**: Implement overlay pubsub for scenes
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Real-time scene gossip and chat over overlay connections

- [x] **T-817**: Add scene-scoped job creation
  - Status: Done
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Label crate and discovery jobs scoped to specific scenes

- [x] **T-818**: Build scene UI
  - Status: Done
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: React components for scene list, join, leave, metadata display

- [x] **T-819**: Add scene chat (optional)
  - Status: Done
  - Priority: P3
  - Branch: experimental/brainz
  - Notes: Overlay pubsub messages for scene-based chat, signed and rate-limited

- [x] **T-820**: Implement scene moderation
  - Status: Done
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Local mute/block for scene participants, no global bans

#### Phase 6D: Disaster Mode & Failover

- [x] **T-821**: Implement Soulseek health monitor
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Track server health (healthy/degraded/unavailable), detect bans and outages

- [x] **T-822**: Build disaster mode coordinator
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Activate/deactivate disaster mode, switch resolvers to mesh-only

- [x] **T-823**: Implement mesh-only search
  - Status: Done
  - Completed: 2025-12-13
  - Priority: P1
  - Branch: experimental/whatAmIThinking
  - Notes: MBID resolution → DHT query → overlay descriptors (no Soulseek server). Implemented disaster mode integration in SearchService with mesh-only routing when Soulseek unavailable.

- [x] **T-824**: Implement mesh-only transfers
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Overlay multi-swarm only, no legacy Soulseek connections

- [x] **T-825**: Add scene-based peer discovery
  - Status: Done
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Fallback to scene queries when DHT MBID lookups sparse

- [x] **T-826**: Build disaster mode UI indicator
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Clear visual indicator when mesh-only active

- [x] **T-827**: Add disaster mode configuration
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Auto vs forced modes, thresholds, behavior toggles

- [x] **T-828**: Implement graceful degradation
  - Status: Done
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Handle partial server availability, hybrid operation

- [x] **T-829**: Add disaster mode telemetry
  - Status: Done
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Track disaster events, recovery times, mesh performance

- [x] **T-830**: Build recovery logic
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Re-enable Soulseek when server returns, smooth transition

#### Phase 6E: Integration & Polish

- [x] **T-831**: Integrate shadow index with job resolvers
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Use shadow index hints in MB Release, discography, label crate jobs

- [x] **T-832**: Integrate scenes with label crate jobs
  - Status: Done
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Scene-scoped crates, prioritize scene peers

- [x] **T-833**: Integrate disaster mode with rescue mode
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: In disaster mode, all transfers are "rescue" (mesh-only)

- [x] **T-834**: Perform privacy audit
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Ensure username anonymization, no path leaks, DHT privacy

- [x] **T-835**: Optimize DHT query performance
  - Status: Done
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Cache DHT lookups, batch queries, prefetch hot MBIDs

- [x] **T-836**: Build mesh configuration UI
  - Status: Done
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: React settings panel for mesh, shadow index, scenes, disaster mode

- [x] **T-837**: Add telemetry dashboard
  - Status: Done
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Shadow index stats, disaster events, scene activity

- [x] **T-838**: Write user documentation
  - Status: Done
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: User guide for disaster mode, scenes, privacy settings

- [x] **T-839**: Create integration test suite
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Full disaster mode simulation, scene coordination, shadow index accuracy

- [x] **T-840**: Perform load testing
  - Status: Done
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: DHT scalability, shard size limits, overlay throughput at scale

#### Phase 6X: Legacy Client Compatibility Bridge (Optional)

- [x] **T-850**: Implement bridge service lifecycle
  - Status: Done
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Start/stop local Soulfind instance, health checks

- [x] **T-851**: Create Soulfind proxy mode (fork/patch)
  - Status: Done
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Extend Soulfind with PROXY_MODE env var, forward operations to slskdn

- [x] **T-852**: Build bridge API endpoints
  - Status: Done
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: /api/bridge/search, /api/bridge/download, /api/bridge/rooms

- [x] **T-853**: Implement MBID resolution from legacy queries
  - Status: Done
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Parse "artist album" queries, resolve to MBIDs, query shadow index

- [x] **T-854**: Add filename synthesis from variants
  - Status: Done
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Generate friendly filenames for mesh variants shown to legacy clients

- [x] **T-855**: Implement peer ID anonymization
  - Status: Done
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Map overlay peer IDs to "mesh-peer-abc123" usernames for legacy display

- [x] **T-856**: Add room → scene mapping
  - Status: Done
  - Priority: P2
  - Branch: experimental/virtual-soulfind
  - Notes: Map legacy room names to scene DHT keys, proxy chat to overlay pubsub

- [x] **T-857**: Implement transfer progress proxying
  - Status: Done
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Show mesh multi-swarm progress in legacy client

- [x] **T-858**: Build bridge configuration UI
  - Status: Done
  - Priority: P3
  - Branch: experimental/brainz
  - Notes: Settings for bridge port, auth, client limits

- [x] **T-859**: Add bridge status dashboard
  - Status: Done
  - Priority: P3
  - Branch: experimental/brainz
  - Notes: Show connected legacy clients, proxied requests, mesh benefits

- [x] **T-860**: Create Nicotine+ integration tests
  - Status: Done
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Automated tests with real Nicotine+ client against bridge

---

### Phase 8: Code Quality & Refactoring

> **Branch**: `experimental/brainz`  
> **Docs**: `docs/phase8-refactoring-design.md`  
> **Timeline**: Phase 8 (8-12 weeks post-implementation)

#### Stage 1: Mesh APIs with Power Preserved (Weeks 1-2)

- [x] **T-1000**: Create namespace structure
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Create Slskdn.Swarm, Slskdn.Mesh (with DHT/, Overlay/, Advanced/), Slskdn.Security, Slskdn.Brainz, Slskdn.Integrations

- [x] **T-1001**: Define IMeshDirectory + IMeshAdvanced
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Dual API - IMeshDirectory for normal use, IMeshAdvanced for power features/experiments

- [x] **T-1002**: Add MeshOptions.TransportPreference
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: DhtFirst (default), Mirrored, OverlayFirst - encourage DHT usage

- [x] **T-1003**: Implement MeshTransportService with configurable preference
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Handle DHT-first, mirrored, overlay-first strategies; NAT, UPnP, TLS

#### Stage 2: Job Pipeline (Weeks 3-4)

- [x] **T-1030**: Implement IMetadataJob abstraction
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: IMetadataJob interface with AlbumBackfillJob, DiscographyJob, RepairMissionJob, NetworkStressTestJob

- [x] **T-1031**: Create MetadataJobRunner
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: BackgroundService with Channel-based job queue and status tracking

- [x] **T-1034**: Convert metadata tasks to jobs
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Replace fire-and-forget async calls with job enqueueing

- [x] **T-1035**: Add network simulation job support
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Jobs for mesh stress tests, NAT experiments, disaster mode simulations

#### Stage 3: Swarm Orchestrator (Weeks 5-6)

- [x] **T-1010**: Implement SwarmDownloadOrchestrator
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: BackgroundService with Channel-based job queue, replace scattered Task.Run

- [x] **T-1011**: Create SwarmJob model
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: SwarmJob, SwarmFile, SwarmChunk, SwarmSource with proper typing

- [x] **T-1012**: Implement IVerificationEngine
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Centralize chunk verification (hash, consensus, reputation) with caching

- [x] **T-1013**: Replace ad-hoc Task.Run
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Route all swarm operations through orchestrator

- [x] **T-1014**: Integrate with IMeshDirectory and IMeshAdvanced
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Use IMeshDirectory for normal discovery, IMeshAdvanced for advanced strategies

#### Stage 4: Security Policy Engine (Week 7)

- [x] **T-1040**: Implement ISecurityPolicyEngine
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Policy engine abstraction with SecurityContext, SecurityDecision

- [x] **T-1041**: Create CompositeSecurityPolicy
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Compose multiple policies with short-circuit evaluation

- [x] **T-1042**: Implement individual policies
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: NetworkGuardPolicy, ReputationPolicy, ConsensusPolicy, ContentSafetyPolicy, HoneypotPolicy, NatAbuseDetectionPolicy

- [x] **T-1043**: Replace inline security checks
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Remove scattered security logic from controllers, use policy engine

#### Stage 5: Typed Configuration (Week 8)

- [x] **T-1050**: Create strongly-typed options
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: SwarmOptions, MeshOptions, SecurityOptions, BrainzOptions

- [x] **T-1051**: Wire options via IOptions<T>
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: DI registration for all options classes

- [x] **T-1052**: Remove direct IConfiguration access
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Replace magic strings with typed options everywhere

#### Stage 6: Codec Analyzers (Week 9)

- [x] **T-1032**: Implement codec analyzers
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: IAudioAnalyzer with FlacAnalyzer, Mp3Analyzer, OpusAnalyzer, AacAnalyzer (from Phase 2-Extended)

- [x] **T-1033**: Create unified BrainzClient
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Single client for MB/AcoustID/Soulbeet with caching, rate limiting, backoff

#### Stage 7: Testability (Week 10)

- [x] **T-1060**: Eliminate static singletons
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Remove static state, make everything injectable

- [x] **T-1061**: Add interfaces for subsystems
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Ensure all major services have interfaces for mocking

- [x] **T-1062**: Constructor injection cleanup
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: No `new HttpClient()`, no object creation inside methods

#### Stage 8: Test Infrastructure (Week 11)

- [x] **T-1070**: Implement Soulfind test harness
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: SoulfindRunner for integration tests (from Phase 7 design)

- [x] **T-1071**: Implement MeshSimulator with DHT-first + disaster mode
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: In-process mesh simulation with DHT-first discovery, disaster mode, NAT edge case support

- [x] **T-1072**: Write integration-soulseek tests
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Multi-client tests with Alice/Bob/Carol topology

- [x] **T-1073**: Write integration-mesh tests
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: DHT-heavy tests, NAT edge cases, disaster mode continuation

#### Stage 9: Cleanup (Week 12)

- [x] **T-1080**: Remove dead code
  - Status: Done
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Kill unused enums, flags, half-implemented concepts, proto-classes

- [x] **T-1081**: Normalize naming
  - Status: Done
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Consistent vocabulary: Swarm/Mesh/Brainz/Security terms

- [x] **T-1082**: Move narrative comments to docs
  - Status: Done
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Keep code comments concise, details in docs/

- [x] **T-1083**: Collapse forwarding classes
  - Status: Done
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Remove unnecessary abstraction layers

---

### Phase 7: Testing Strategy with Soulfind & Mesh Simulator

> **Branch**: `experimental/brainz` (tests live alongside features)  
> **Docs**: `docs/phase7-testing-strategy-soulfind.md`  
> **Timeline**: Phase 7 (4-6 weeks)

#### Phase 7A: Test Harness Infrastructure

- [x] **T-900**: Implement Soulfind test harness
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: SoulfindRunner class for starting/stopping local Soulfind, binary discovery, ephemeral port allocation, readiness detection.

- [x] **T-901**: Implement slskdn test client harness
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: SlskdnTestClient class for isolated test instances, config directory isolation, share directory configuration, API wrappers.

- [x] **T-902**: Create audio test fixtures
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Small deterministic audio files (FLAC, MP3, Opus, AAC), known good and transcode variants, metadata sidecar with expected scores.

- [x] **T-903**: Create MusicBrainz stub responses
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: JSON fixtures for MB API responses, mock IMusicBrainzClient, test helper to inject mock into DI.

#### Phase 7B: Protocol & Integration Tests

- [x] **T-904**: Implement L1 protocol contract tests
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Test suite for basic Soulseek protocol: login/handshake, keepalive, search, rooms, browse.

- [x] **T-905**: Implement L2 multi-client integration tests
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Alice/Bob/Carol topology, scenarios for search/download/capture/rooms, assertions on MBID mapping and quality scores.

- [x] **T-906**: Implement mesh simulator
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: MeshSimulator class for in-process DHT/overlay, SimulatedNode with fake library, network partition/message drop simulation.

#### Phase 7C: Disaster Mode & Mesh-Only Tests

- [x] **T-907**: Implement L3 disaster mode tests
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Soulfind-assisted disaster drills, kill Soulfind mid-transfer, verify mesh takeover and disaster mode activation.

- [x] **T-908**: Implement L3 mesh-only tests
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Pure mesh simulation (no Soulfind), discography job across mesh, repair mission tests, DHT/overlay-only discovery.

- [x] **T-909**: Add CI test categorization
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Test traits/categories for L0/L1/L2/L3, CI configuration, environment variable detection for Soulfind.

- [x] **T-910**: Add test documentation
  - Status: Done
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: README for running integration tests locally, instructions for Soulfind setup, troubleshooting guide.

#### Phase 7D: Feature-Specific Integration Tests

- [x] **T-911**: Implement test result visualization
  - Status: Done
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Test report generation (HTML/Markdown), coverage reports, performance benchmarks for mesh operations.

- [x] **T-912**: Add rescue mode integration tests
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Tests for underperforming transfer detection, overlay rescue activation, mixed Soulseek+overlay completion.

- [x] **T-913**: Add canonical selection integration tests
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Verify canonical variant preference, quality score-based source selection, cross-codec deduplication with real files.

- [x] **T-914**: Add library health integration tests
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: End-to-end library scanning, issue detection validation (transcodes, non-canonical), remediation job creation/execution.

- [x] **T-915**: Performance benchmarking suite
  - Status: Done
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Benchmark tests for DHT query latency, overlay throughput, canonical stats aggregation, mesh simulation at scale (100+ nodes).

---

### Phase 12: Adversarial Resilience & Privacy Hardening

> **Branch**: `experimental/brainz`  
> **Docs**: `docs/phase12-adversarial-resilience-design.md`  
> **Dependencies**: Phases 8-11 (MeshCore, Overlay, SecurityCore)  
> **Timeline**: Phase 12 (16-20 weeks)

> **Purpose**: Optional security layers for users in adversarial environments (dissidents, journalists, activists in repressive regimes). ALL FEATURES DISABLED BY DEFAULT, configurable via WebGUI.

#### Phase 12A: Privacy Layer — Traffic Analysis Protection

- [ ] **T-1200**: Define AdversarialOptions configuration model
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Create strongly-typed options for all Phase 12 features (padding, timing, anonymity, transport, onion, relay, bridges, deniability). Wire into IOptions<T> and appsettings.yml.

- [ ] **T-1201**: Implement IPrivacyLayer interface
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Abstraction for privacy transformations (padding, timing, batching). Composable middleware pattern for message processing.

- [ ] **T-1202**: Add adversarial section to WebGUI settings
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: React settings panel skeleton: Settings → Privacy & Security. Preset selector (Standard/Enhanced/Maximum), expandable sections.

- [ ] **T-1210**: Implement BucketPadder (message padding)
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Pad messages to fixed bucket sizes (512, 1024, 2048, 4096, 8192, 16384 bytes). Use random fill bytes (not zeros) to prevent compression attacks. IMessagePadder interface.

- [ ] **T-1211**: Implement RandomJitterObfuscator (timing)
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Add configurable random delay (0-500ms default) to all outbound messages. ITimingObfuscator interface. Prevents timing correlation attacks.

- [ ] **T-1212**: Implement TimedBatcher (message batching)
  - Status: Not started
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Hold messages for configurable window (e.g., 2 seconds), send as batch. Prevents frequency analysis. IMessageBatcher interface.

- [ ] **T-1213**: Implement CoverTrafficGenerator
  - Status: Not started
  - Priority: P3
  - Branch: experimental/brainz
  - Notes: Send dummy messages when idle (configurable interval, e.g., 30 seconds). Makes traffic patterns constant regardless of actual activity.

- [ ] **T-1214**: Integrate privacy layer with overlay messaging
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Wire IPrivacyLayer into ControlDispatcher, QuicOverlayClient, UdpOverlayClient. All outbound messages pass through privacy transforms when enabled.

- [ ] **T-1215**: Add privacy layer unit tests
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Test padding bucket selection, random fill, timing distribution, batch accumulation, cover traffic intervals.

- [ ] **T-1216**: Add privacy layer integration tests
  - Status: Not started
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: End-to-end tests: message round-trip with padding enabled, timing jitter verification, batch delivery.

- [ ] **T-1217**: Write privacy layer user documentation
  - Status: Not started
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Explain each feature, tradeoffs (latency vs privacy), when to use. Include in docs/phase12-user-guide.md.

#### Phase 12B: Anonymity Layer — IP Protection

- [ ] **T-1220**: Implement TorSocksTransport
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Route mesh traffic through local Tor SOCKS5 proxy (default 127.0.0.1:9050). IAnonymityTransport interface. Health check for Tor connectivity.

- [ ] **T-1221**: Implement I2PTransport
  - Status: Not started
  - Priority: P3
  - Branch: experimental/brainz
  - Notes: Connect via I2P SAM bridge for peer-to-peer anonymity. Alternative to Tor, better for persistent connections.

- [ ] **T-1222**: Implement RelayOnlyTransport
  - Status: Not started
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Never make direct connections; always route through trusted relay nodes within mesh. User never reveals IP to destination peer.

- [ ] **T-1223**: Add Tor connectivity status to WebGUI
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Status indicator (connected/disconnected/error), circuit info, SOCKS address/port configuration fields.

- [ ] **T-1224**: Implement stream isolation
  - Status: Not started
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Use different Tor circuits per peer (via SOCKS5 auth). Prevents correlation of connections to same peer.

- [ ] **T-1225**: Add anonymity transport selection logic
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Transport priority: Direct (if not anonymity mode) → Tor → I2P → Relay. Fallback on failure.

- [ ] **T-1226**: Integrate with MeshTransportService
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Wire IAnonymityTransport into existing transport selection. Respect AdversarialOptions.Anonymity.Mode setting.

- [ ] **T-1227**: Add Tor integration tests
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Integration tests with mock Tor SOCKS5, real Tor (if available), circuit establishment, stream isolation.

- [ ] **T-1228**: Write Tor setup documentation
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: User guide: install Tor, configure slskdn, tradeoffs (latency, reliability), stream isolation, bridges.

- [ ] **T-1229**: Add I2P setup documentation
  - Status: Not started
  - Priority: P3
  - Branch: experimental/brainz
  - Notes: User guide for I2P SAM bridge setup, tunnel configuration, when to prefer I2P over Tor.

#### Phase 12C: Obfuscated Transports — Anti-DPI

- [ ] **T-1230**: Implement WebSocketTransport
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Tunnel mesh protocol over WSS connections. Looks like normal web traffic to observers. IObfuscatedTransport interface.

- [ ] **T-1231**: Implement HttpTunnelTransport
  - Status: Not started
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Encode mesh messages as HTTP POST/GET request/response bodies. Long-polling or chunked transfer for bidirectional.

- [ ] **T-1232**: Implement Obfs4Transport
  - Status: Not started
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Tor's obfuscation protocol. Uses obfs4proxy binary. Traffic looks like random noise, resists active probing.

- [ ] **T-1233**: Implement MeekTransport
  - Status: Not started
  - Priority: P3
  - Branch: experimental/brainz
  - Notes: Route through major CDNs (Azure, Cloudflare). Blocking requires blocking entire CDN. Domain fronting variant.

- [ ] **T-1234**: Add transport selection WebGUI
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Dropdown: Primary transport (QUIC/WebSocket/HTTP/obfs4/Meek). Bridge configuration textarea for obfs4.

- [ ] **T-1235**: Implement transport fallback logic
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Try primary transport → fallback chain. Configurable fallback order. Retry with exponential backoff.

- [ ] **T-1236**: Add obfuscated transport tests
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Unit tests for each transport. Integration tests with real obfs4proxy (if available). Traffic pattern analysis.

- [ ] **T-1237**: Write obfuscation user documentation
  - Status: Not started
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Guide for each transport: when to use, setup, tradeoffs, bridge acquisition for obfs4.

- [ ] **T-1238**: Add transport performance benchmarks
  - Status: Not started
  - Priority: P3
  - Branch: experimental/brainz
  - Notes: Benchmark latency/throughput for each transport. Help users choose appropriate tradeoffs.

#### Phase 12D: Native Onion Routing

- [ ] **T-1240**: Implement MeshCircuitBuilder
  - Status: Not started
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Build onion-routed circuits within mesh network. Select relay nodes, encrypt message in layers, each relay unwraps one layer. ICircuitBuilder interface.

- [ ] **T-1241**: Implement MeshRelayService
  - Status: Not started
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Allow mesh peers to volunteer as relay nodes. Process forwarding requests, rate limiting, bandwidth accounting, no logging of forwarded content.

- [ ] **T-1242**: Implement DiverseRelaySelector
  - Status: Not started
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Select relays for diversity: different ASNs, countries, network segments. Avoid relays controlled by same entity. Use reputation scores.

- [ ] **T-1243**: Add relay node WebGUI controls
  - Status: Not started
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Toggle: Enable relay node. Max bandwidth slider. Max circuits limit. Warning about legal implications.

- [ ] **T-1244**: Implement circuit keepalive and rotation
  - Status: Not started
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Keep circuits alive with periodic pings. Rotate circuits after configurable lifetime (e.g., 10 minutes).

- [ ] **T-1245**: Add relay bandwidth accounting
  - Status: Not started
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Track bandwidth contributed as relay. Display in dashboard. Feed into fairness governor.

- [ ] **T-1246**: Add onion routing unit tests
  - Status: Not started
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Test circuit construction, layer encryption/decryption, relay forwarding, path diversity.

- [ ] **T-1247**: Add onion routing integration tests
  - Status: Not started
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Multi-node tests with simulated relays. End-to-end circuit establishment and message delivery.

- [ ] **T-1248**: Write relay operator documentation
  - Status: Not started
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Guide for running relay node: bandwidth requirements, legal considerations, exit policy, monitoring.

- [ ] **T-1249**: Add circuit visualization to WebGUI
  - Status: Not started
  - Priority: P3
  - Branch: experimental/brainz
  - Notes: Show active circuits with hop visualization (country flags, latency per hop). Similar to Tor Browser circuit display.

#### Phase 12E: Censorship Resistance

- [ ] **T-1250**: Implement BridgeDiscovery service
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Manage unlisted entry points for users behind firewalls. Support static config, email request, web distribution. IBridgeDiscovery interface.

- [ ] **T-1251**: Implement DomainFrontedTransport
  - Status: Not started
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: TLS SNI points to CDN, Host header points to relay. Observer sees traffic to CDN, not actual destination.

- [ ] **T-1252**: Implement ImageSteganography (bridge distribution)
  - Status: Not started
  - Priority: P3
  - Branch: experimental/brainz
  - Notes: Hide bridge info in image LSBs. Can be shared on social media. Decodes to bridge configuration.

- [ ] **T-1253**: Add bridge configuration WebGUI
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Textarea for bridge lines. Request bridges via email button. QR code scanner. Bridge status indicators.

- [ ] **T-1254**: Implement bridge health checking
  - Status: Not started
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Periodic connectivity tests to configured bridges. Mark failed bridges, auto-failover to working ones.

- [ ] **T-1255**: Add bridge fallback logic
  - Status: Not started
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Try bridges in order until one works. Cache working bridges. Retry failed bridges with backoff.

- [ ] **T-1256**: Write bridge setup documentation
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: How to get bridges, configure them, troubleshoot. Include for obfs4, meek, domain-fronted bridges.

- [ ] **T-1257**: Add censorship resistance tests
  - Status: Not started
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Simulate blocked direct connections. Verify bridge fallback works. Test domain fronting.

#### Phase 12F: Plausible Deniability

- [ ] **T-1260**: Implement DeniableVolumeStorage
  - Status: Not started
  - Priority: P3
  - Branch: experimental/brainz
  - Notes: Hidden volumes with different passphrases reveal different content. Cryptographically impossible to prove hidden volume exists.

- [ ] **T-1261**: Implement DecoyPodService
  - Status: Not started
  - Priority: P3
  - Branch: experimental/brainz
  - Notes: Auto-generate/join harmless music pods. Maintain realistic activity patterns. Sensitive pods only with correct passphrase.

- [ ] **T-1262**: Add deniable storage setup wizard
  - Status: Not started
  - Priority: P3
  - Branch: experimental/brainz
  - Notes: Step-by-step wizard: create outer volume, create hidden volume, configure passphrases. Strong warnings about backup.

- [ ] **T-1263**: Implement volume passphrase handling
  - Status: Not started
  - Priority: P3
  - Branch: experimental/brainz
  - Notes: Secure passphrase entry, key derivation (Argon2), volume unlock flow. Different passphrase → different volume.

- [ ] **T-1264**: Add deniability unit tests
  - Status: Not started
  - Priority: P3
  - Branch: experimental/brainz
  - Notes: Test volume creation, different passphrase access, hidden volume undetectability.

- [ ] **T-1265**: Write deniability user documentation
  - Status: Not started
  - Priority: P3
  - Branch: experimental/brainz
  - Notes: Explain plausible deniability concept, setup process, operational security practices.

#### Phase 12G: WebGUI & Integration

- [ ] **T-1270**: Implement Privacy Settings panel
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Full React settings panel as per design doc. Collapsible sections, preset selector, individual toggles.

- [ ] **T-1271**: Implement Privacy Dashboard
  - Status: Not started
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Protection level indicator, status cards (IP hidden, traffic padded, timing jittered), recommendations.

- [ ] **T-1272**: Add security preset selector
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Radio buttons: Standard/Enhanced/Maximum. Each preset configures multiple settings at once. Custom option.

- [ ] **T-1273**: Implement real-time status indicators
  - Status: Not started
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Live Tor connection status, circuit info, bridge health, relay statistics. WebSocket updates.

- [ ] **T-1274**: Add privacy recommendations engine
  - Status: Not started
  - Priority: P3
  - Branch: experimental/brainz
  - Notes: Analyze current config, suggest improvements. "Consider enabling X for better Y" style recommendations.

- [ ] **T-1275**: Integrate all layers with existing systems
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Wire privacy/anonymity/transport layers into MeshCore, overlay, PodCore. Ensure backward compatibility.

- [ ] **T-1276**: Add end-to-end privacy tests
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Full integration tests: user with Maximum preset can communicate with user using Standard preset.

- [ ] **T-1277**: Write comprehensive user guide
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Complete Phase 12 user documentation. Threat model, feature explanations, setup guides, troubleshooting.

- [ ] **T-1278**: Create threat model documentation
  - Status: Not started
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Document adversary capabilities, what each feature protects against, limitations and residual risks.

- [ ] **T-1279**: Add privacy audit logging (opt-in)
  - Status: Not started
  - Priority: P3
  - Branch: experimental/brainz
  - Notes: Optional logging for debugging privacy features. Disabled by default. Clear warnings about privacy implications.

#### Phase 12H: Testing & Documentation

- [ ] **T-1290**: Create adversarial test scenarios
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Test scenarios: hostile ISP, national firewall, traffic analysis, Sybil attacks. Document expected behavior.

- [ ] **T-1291**: Implement traffic analysis resistance tests
  - Status: Not started
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Verify padding works (all messages same bucket size), timing jitter (no timing patterns), cover traffic (constant rate).

- [ ] **T-1292**: Add censorship simulation tests
  - Status: Not started
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Simulate: blocked ports, blocked IPs, DPI detection, blocked bootstrap nodes. Verify circumvention works.

- [ ] **T-1293**: Performance benchmarking suite
  - Status: Not started
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Benchmark each privacy level. Latency/throughput impact. Help users understand tradeoffs quantitatively.

- [ ] **T-1294**: Security review and audit
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Internal security review of all Phase 12 code. Document any limitations. Consider external audit for critical paths.

- [ ] **T-1295**: Write operator guide (relay/bridge)
  - Status: Not started
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Guide for users who want to help others: running relays, running bridges, bandwidth contribution.

- [ ] **T-1296**: Create video tutorials
  - Status: Not started
  - Priority: P3
  - Branch: experimental/brainz
  - Notes: Screen recordings: setting up Tor integration, configuring bridges, running a relay node.

- [ ] **T-1297**: Add localization for privacy UI
  - Status: Not started
  - Priority: P3
  - Branch: experimental/brainz
  - Notes: Translate privacy settings/dashboard to key languages. Priority: zh, fa, ar, ru, es.

- [ ] **T-1298**: Final integration testing
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Full test pass with all Phase 12 features. Edge cases, error handling, graceful degradation.

- [ ] **T-1299**: Phase 12 release notes
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Release notes documenting all new features, configuration options, recommended usage scenarios.

---

### Phase 8-11 Gap Tasks (AUDIT FINDINGS)

> **Audit Report**: `docs/PHASE_8_11_AUDIT_REPORT.md`  
> **Status**: These tasks fill gaps where stubs/placeholders were created instead of real implementations.

#### Phase 8 Gap: MeshCore Implementation (16 tasks)

- [x] **T-1300**: Implement real STUN NAT detection
  - Status: Completed (2025-12-13)
  - Priority: P1
  - Branch: experimental/whatAmIThinking
  - Notes: Replace stub that returns Unknown. Use STUN protocol to detect NAT type (Direct, Restricted, Symmetric). Added API endpoint, logging, and proper async integration.

- [x] **T-1301**: Implement k-bucket routing table
  - Status: Completed (2025-12-13)
  - Priority: P1
  - Branch: experimental/whatAmIThinking
  - Notes: Kademlia-style k-bucket structure with k=20. XOR distance metric, bucket splitting, node eviction. Complete implementation with ping-before-evict.

- [x] **T-1302**: Implement FIND_NODE Kademlia RPC
  - Status: Completed (2025-12-13)
  - Priority: P1
  - Branch: experimental/whatAmIThinking
  - Notes: Query closest k nodes to a target ID. Iterative lookup algorithm with alpha=3 parallel requests.

- [x] **T-1303**: Implement FIND_VALUE Kademlia RPC
  - Status: Completed (2025-12-13)
  - Priority: P1
  - Branch: experimental/whatAmIThinking
  - Notes: Content lookup protocol, STORE value caching, iterative key resolution. Complete implementation with DhtService.

- [x] **T-1304**: Implement STORE Kademlia RPC
  - Status: Completed (2025-12-13)
  - Priority: P1
  - Branch: experimental/whatAmIThinking
  - Notes: Store key-value pairs on closest k nodes with Ed25519 signature verification and timestamp validation.

- [x] **T-1305**: Implement peer descriptor refresh cycle
  - Status: Completed (2025-12-13)
  - Priority: P2
  - Branch: experimental/whatAmIThinking
  - Notes: Periodic republishing of own descriptor with TTL/2 refresh interval and IP change detection.

- [x] **T-1306**: Implement UDP hole punching
  - Status: Completed (2025-12-13)
  - Priority: P2
  - Branch: experimental/whatAmIThinking
  - Notes: NAT traversal for non-symmetric NAT with rendezvous coordination via overlay mesh service.

- [x] **T-1307**: Implement relay fallback for symmetric NAT
  - Status: Completed (2025-12-13)
  - Priority: P2
  - Branch: experimental/whatAmIThinking
  - Notes: When hole punching fails, use relay nodes with NatTraversalService integration and RelayRequired=true marking.

- [x] **T-1308**: Implement MeshDirectory.FindContentByPeerAsync
  - Status: Completed (2025-12-13)
  - Priority: P1
  - Branch: experimental/whatAmIThinking
  - Notes: Fixed DHT key format mismatch between DescriptorPublisher and MeshDirectory lookups.

- [x] **T-1309**: Implement content → peer index
  - Status: Completed (2025-12-13)
  - Priority: P1
  - Branch: experimental/whatAmIThinking
  - Notes: Integrated ContentPeerHintService with ShareService for automatic content-to-peer index population after share scans.

- [x] **T-1310**: Implement MeshAdvanced route diagnostics
  - Status: Completed (2025-12-13)
  - Priority: P2
  - Branch: experimental/whatAmIThinking
  - Notes: Replaced TraceRoutesAsync placeholder with actual routing diagnostics including hop latencies and transport analysis.

- [ ] **T-1311**: Implement mesh stats collection
  - Status: Not started
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Replace hardcoded values. Track real peer count, messages sent/received, DHT ops/sec.

- [x] **T-1312**: Add mesh health monitoring
  - Status: Completed (2025-12-13)
  - Priority: P2
  - Branch: experimental/whatAmIThinking
  - Notes: Health check endpoint monitoring routing table health, bootstrap connectivity, peer churn with /health/mesh endpoint.

- [x] **T-1313**: Add mesh unit tests
  - Status: Completed (2025-12-13)
  - Priority: P1
  - Branch: experimental/whatAmIThinking
  - Notes: Comprehensive unit tests for k-bucket, Kademlia RPCs, NAT detection, hole punching, stats collection, and health monitoring.

- [x] **T-1314**: Add mesh integration tests
  - Status: Completed (2025-12-13)
  - Priority: P1
  - Branch: experimental/whatAmIThinking
  - Notes: Multi-node integration tests for DHT convergence, content discovery, NAT traversal, peer churn, and network partitioning.

- [x] **T-1315**: Add mesh WebGUI status panel
  - Status: Completed (2025-12-13)
  - Priority: P2
  - Branch: experimental/whatAmIThinking
  - Notes: Comprehensive dashboard showing peer count, routing table health, DHT operations, and network health metrics.

#### Phase 9 Gap: MediaCore Implementation (12 tasks)

- [x] **T-1320**: Implement ContentID registry
  - Status: Completed (2025-12-13)
  - Priority: P1
  - Branch: experimental/whatAmIThinking
  - Notes: Implemented IContentIdRegistry with thread-safe mapping of external IDs to internal ContentIDs, REST API, and WebGUI.

- [x] **T-1321**: Implement multi-domain content addressing
  - Status: Completed (2025-12-13)
  - Priority: P1
  - Branch: experimental/whatAmIThinking
  - Notes: Implemented content:domain:type:id format with parser, domain-specific queries, validation, and WebGUI examples.

- [x] **T-1322**: Implement IPLD content linking
  - Status: Completed (2025-12-13)
  - Priority: P2
  - Branch: experimental/whatAmIThinking
  - Notes: Implemented IPLD links, graph traversal, and content relationship management.

- [x] **T-1323**: Implement perceptual hash computation
  - Status: Completed (2025-12-13)
  - Priority: P1
  - Branch: experimental/whatAmIThinking
  - Notes: Implemented multi-algorithm perceptual hashing (ChromaPrint, pHash) with API and WebGUI.

- [x] **T-1324**: Implement cross-codec fuzzy matching (real algorithm)
  - Status: Completed (2025-12-13)
  - Priority: P1
  - Branch: experimental/whatAmIThinking
  - Notes: Replaced Jaccard placeholder with perceptual hash-based cross-codec matching using ChromaPrint/pHash algorithms.

- [x] **T-1325**: Implement metadata portability layer
  - Status: Completed (2025-12-13)
  - Priority: P2
  - Branch: experimental/whatAmIThinking
  - Notes: Implemented export/import with conflict resolution, merging strategies, and WebGUI tools.

- [x] **T-1326**: Implement content descriptor publishing
  - Status: Completed (2025-12-13)
  - Priority: P1
  - Branch: experimental/whatAmIThinking
  - Notes: Implemented signed descriptor publishing to DHT with versioning, updates, and management.

- [x] **T-1327**: Implement descriptor query/retrieval
  - Status: Completed (2025-12-13)
  - Priority: P1
  - Branch: experimental/whatAmIThinking
  - Notes: Implemented DHT querying with signature verification, freshness checking, and caching.

- [ ] **T-1328**: Add MediaCore unit tests
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Tests for ContentID, fuzzy matching, descriptor validation, perceptual hashing.

- [ ] **T-1329**: Add MediaCore integration tests
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: End-to-end tests with real audio files. Cross-codec matching accuracy.

- [ ] **T-1330**: Integrate MediaCore with swarm scheduler
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Use ContentId for swarm grouping. Fuzzy matching for variant discovery.

- [ ] **T-1331**: Add MediaCore stats/dashboard
  - Status: Not started
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Show content registry stats, descriptor cache hit rate, fuzzy match accuracy.

#### Phase 10 Gap: PodCore Implementation (24 tasks)

- [ ] **T-1340**: Implement Pod DHT publishing
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Publish pod metadata to DHT key pod:<PodId>:meta. Sign with owner key.

- [ ] **T-1341**: Implement signed membership records
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: PodMembershipRecord with Ed25519 signatures. Published to DHT.

- [ ] **T-1342**: Implement membership verification
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Verify membership signatures before accepting messages. Check ban status.

- [ ] **T-1343**: Implement pod discovery (DHT keys)
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: DHT keys for listed pods: pod:discover:name:<slug>, pod:discover:tag:<tag>.

- [ ] **T-1344**: Implement pod join/leave with signatures
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Sign join requests. Owner/mod signs acceptance. Publish membership record.

- [ ] **T-1345**: Implement decentralized message routing
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Route messages via overlay to pod members. Fanout with deduplication.

- [ ] **T-1346**: Implement message signature verification
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Verify sender signature on every message. Reject unsigned/invalid.

- [ ] **T-1347**: Implement message deduplication
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Track MessageId to prevent duplicates. Bloom filter for efficiency.

- [ ] **T-1348**: Implement local message storage
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: SQLite storage for pod messages. Retention policy. Indexing for search.

- [ ] **T-1349**: Implement message backfill protocol
  - Status: Not started
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Request missed messages from peers. Sync on rejoin.

- [ ] **T-1350**: Implement pod channels (full)
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Create/delete channels. Channel metadata in DHT. Per-channel routing.

- [ ] **T-1351**: Implement content-linked pod creation
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Create pod with FocusContentId. Link to artist/album/show.

- [ ] **T-1352**: Implement PodVariantOpinion publishing
  - Status: Not started
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Publish signed opinions to DHT. Key: pod:<PodId>:opinions:<ContentId>.

- [ ] **T-1353**: Implement pod opinion aggregation
  - Status: Not started
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Aggregate opinions from pod members. Weight by affinity/trust.

- [ ] **T-1354**: Implement PodAffinity scoring
  - Status: Not started
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Track engagement (messages, activity). Compute affinity score per member.

- [ ] **T-1355**: Implement kick/ban with signed updates
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Owner/mod signs ban. Update membership record. Propagate to members.

- [ ] **T-1356**: Implement Soulseek chat bridge (ReadOnly)
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Mirror Soulseek room messages to bound pod channel. No outbound.

- [ ] **T-1357**: Implement Soulseek chat bridge (Mirror)
  - Status: Not started
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Two-way sync. Pod messages sent to Soulseek room with prefix.

- [ ] **T-1358**: Implement Soulseek identity mapping
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Map soulseek:username to synthetic PeerId. Optional verification linking.

- [ ] **T-1359**: Create Pod API endpoints
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: /api/v0/pods, /api/v0/pods/{id}, /api/v0/pods/{id}/messages, /api/v0/pods/{id}/members.

- [ ] **T-1360**: Create Pod list/detail UI
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: React components: PodList, PodDetail, PodMemberList. Join/leave buttons.

- [ ] **T-1361**: Create Pod chat UI
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: React components: PodChat, MessageList, MessageInput. Channel tabs.

- [ ] **T-1362**: Add PodCore unit tests
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Tests for membership, signatures, message routing, deduplication, storage.

- [ ] **T-1363**: Add PodCore integration tests
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Multi-node pod creation, join/leave, messaging, bridge sync.

#### Phase 11 Gap: Security Policy Implementation (8 tasks)

- [x] **T-1370**: Implement real NetworkGuardPolicy
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Implemented - checks OverlayBlocklist for username/IP, integrates with OverlayRateLimiter and NetworkGuard. Uses PeerId as username for blocklist checks.

- [x] **T-1371**: Implement real ReputationPolicy
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Implemented - checks PeerReputation service, denies peers with score <= UntrustedThreshold (20), allows unknown peers with logging.

- [x] **T-1372**: Implement real ConsensusPolicy
  - Status: Done (placeholder)
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Implemented placeholder - requires mesh consensus querying infrastructure. Currently allows all but logs that consensus check is not fully implemented. TODO: Add mesh consensus querying.

- [x] **T-1373**: Implement real ContentSafetyPolicy
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Implemented - checks ContentId against knownBadContentHashes HashSet. Accepts optional IEnumerable<string> of known bad content hashes via constructor. Denies if ContentId matches known bad content.

- [x] **T-1374**: Implement real HoneypotPolicy
  - Status: Done
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Implemented - tracks suspicious activity patterns: too many requests in short time (>50 in 5 min), rapid operation switching (>20 unique operations in 10 min). Denies after 3 suspicious patterns detected.

- [x] **T-1375**: Implement real NatAbuseDetectionPolicy
  - Status: Done (placeholder)
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Implemented placeholder - requires NAT tracking infrastructure. Currently allows all but logs that NAT abuse detection is not fully implemented. TODO: Add NAT type tracking and abuse detection logic.

- [x] **T-1376**: Complete static singleton elimination
  - Status: Done
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Audit complete - comprehensive search found zero static singletons. All services use DI. See docs/PHASE_11_CODE_QUALITY_AUDIT.md for details.

- [x] **T-1377**: Verify and complete dead code removal
  - Status: Done
  - Priority: P3
  - Branch: experimental/brainz
  - Notes: Audit complete - found 47 TODO comments across 25 files. Analysis shows remaining TODOs are intentional stubs (PodCore, VirtualSoulfind) or future enhancements, not dead code. No NotImplementedException found. See docs/PHASE_11_CODE_QUALITY_AUDIT.md.

- [x] **T-1378**: Implement SignalBus statistics tracking
  - Status: Done
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Implemented - added Interlocked counters for signalsSent, signalsReceived, duplicateSignalsDropped, expiredSignalsDropped. Added GetStatistics() method to ISignalBus and SignalBus. Updated SignalSystemController.GetStatus() to return actual statistics.

- [x] **T-1379**: Verify and complete naming normalization
  - Status: Done
  - Priority: P3
  - Branch: experimental/brainz
  - Notes: Audit complete - naming conventions are consistent: PascalCase for classes/methods/properties, camelCase for parameters, _camelCase for private fields. Abbreviations used consistently. See docs/PHASE_11_CODE_QUALITY_AUDIT.md.

- [x] **T-1380**: Add Mesh integration tests
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Implemented - created MeshIntegrationTests.cs with 10 tests covering MeshHealthService, MeshDirectory, MeshAdvanced, MeshSimulator (DHT operations, overlay transfers, network partition, message drop rate). All tests passing.

- [x] **T-1381**: Add PodCore integration tests
  - Status: Done
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Implemented - created PodCoreIntegrationTests.cs with 10 tests covering PodService (create, list, join, leave, ban), PodMessaging, SoulseekChatBridge, error handling. All tests passing.

---

### High Priority

- [x] **T-001**: Persistent Room/Chat Tabs
  - Status: Done
  - Completed: 2025-12-13
  - Priority: High
  - Branch: experimental/whatAmIThinking
  - Related: `TODO.md`, Browse tabs implementation
  - Notes: Implement tabbed interface like Browse currently has. Reuse `Browse.jsx`/`BrowseSession.jsx` patterns. Added RoomSession component and converted Rooms to functional component with tabs.

- [x] **T-002**: Scheduled Rate Limits
  - Status: Done
  - Completed: 2025-12-13
  - Priority: High
  - Branch: experimental/whatAmIThinking
  - Related: slskd #985
  - Notes: Day/night upload/download speed schedules like qBittorrent. Implemented ScheduledRateLimitService, configuration options, and UploadGovernor integration.

### Medium Priority

- [x] **T-003**: Download Queue Position Polling
  - Status: Completed (2025-12-13)
  - Priority: Medium
  - Related: slskd #921
  - Notes: Auto-refresh queue positions for queued files
  - Implementation: Added automatic queue position polling in Transfers.jsx that fetches positions for all queued downloads every 1 second

- [x] **T-004**: Visual Group Indicators
  - Status: Completed (2025-12-13)
  - Priority: Medium
  - Related: slskd #745
  - Notes: Icons in search results for users in your groups
  - Implementation: Added visual indicators (star, triangle, ban icons) next to usernames in search results based on group membership

- [x] **T-005**: Traffic Ticker
  - Status: Completed (2025-12-13)
  - Priority: Medium
  - Related: slskd discussion #547
  - Notes: Real-time upload/download activity feed in UI
  - Implementation: Added TransfersHub SignalR hub, TransferActivity model, TrafficTicker component with live feed on transfers pages

### Low Priority

- [x] **T-006**: Create Chat Rooms from UI
  - Status: Completed (2025-12-13)
  - Priority: Low
  - Related: slskd #1258
  - Notes: Create public/private rooms from web interface
  - Implementation: Added RoomCreateModal with public/private options, integrated into Rooms component

- [x] **T-007**: Predictable Search URLs
  - Status: Completed (2025-12-13)
  - Priority: Low
  - Related: slskd #1170
  - Notes: Bookmarkable search URLs for browser integration
  - Implementation: Added query parameter support for /searches?q=search+term URLs with automatic search creation

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

*Last updated: December 10, 2025*

