# Multi-Swarm & Soulbeet Integration - Task Summary

> Quick reference guide for navigating the expanded task roadmap

## Task Ranges Overview

### Phase 1: MusicBrainz/Chromaprint Foundation (COMPLETE)
**Tasks**: T-300 through T-313  
**Status**: ✅ **ALL COMPLETE** (T-300 through T-313)  
**Branch**: `experimental/brainz`

Key achievements:
- MusicBrainz API integration with Release/Recording lookups
- Chromaprint/AcoustID fingerprinting pipeline
- Semantic swarm grouping by MBID/fingerprint
- Final-file verification after multi-source download
- ✅ T-312: Album completion UI (AlbumCompletionPanel.jsx, GetAlbumCompletion endpoint)
- ✅ T-313: Unit + integration tests (MusicBrainzControllerTests.cs, 2 tests passing)

---

### Phase 2: Mesh Integration & Advanced Swarm Features
**Tasks**: T-400 through T-411 (12 tasks)  
**Status**: Phase 2A complete, Phase 2B-2D pending  
**Branch**: `experimental/multi-swarm`  
**Priority**: High (P1)

#### Phase 2A: Canonical Edition Scoring (T-400 to T-402) ✅ **COMPLETE**
Build quality scoring for audio variants and prefer canonical/best versions in downloads.
- ✅ T-400: AudioVariant model, QualityScorer, TranscodeDetector, CodecProfile
- ✅ T-401: CanonicalStatsService, aggregation logic, tests
- ✅ T-402: SelectCanonicalSourcesAsync, ShouldSkipDownloadAsync in MultiSourceDownloadService

#### Phase 2B: Collection Doctor / Library Health (T-403 to T-405) ✅ **COMPLETE**
Scan library for suspected transcodes, non-canonical variants, missing tracks. One-click fix via multi-swarm.
- ✅ T-403: Deep library scanning logic implemented (LibraryHealthService.ScanFileAsync)
- ✅ T-404: LibraryHealthController API endpoints exist
- ✅ T-405: LibraryHealthRemediationService with multi-swarm integration exists

#### Phase 2C: RTT + Throughput-Aware Swarm Scheduler (T-406 to T-408)
Track per-peer metrics (RTT, throughput, error rate) and use cost function to optimize chunk assignment.

#### Phase 2D: Rescue Mode for Underperforming Soulseek Transfers (T-409 to T-411)
Detect stalled Soulseek transfers and use overlay mesh to complete them while keeping Soulseek primary.

---

### Phase 3: Discovery, Reputation, and Fairness
**Tasks**: T-500 through T-510 (11 tasks)  
**Status**: Not started  
**Branch**: `experimental/multi-swarm`  
**Priority**: Medium (P2)

#### Phase 3A: Release-Graph Guided Discovery (T-500 to T-502)
Fetch artist discographies from MusicBrainz, create multi-album download jobs with configurable profiles (core/extended/all).

#### Phase 3B: Label Crate Mode (T-503 to T-504)
Download popular releases from a label based on mesh popularity metrics.

#### Phase 3C: Local-Only Peer Reputation (T-505 to T-507)
Track per-peer success/failure rates locally (never shared) and avoid low-reputation peers.

#### Phase 3D: Mesh-Level Fairness Governor (T-508 to T-510)
Enforce upload/download ratios to ensure slskdn nodes remain net contributors to the ecosystem.

---

### Phase 4: Job Manifests, Session Traces & Advanced Features
**Tasks**: T-600 through T-611 (12 tasks)  
**Status**: Not started  
**Branch**: `experimental/multi-swarm`  
**Priority**: Medium (P2) for manifests/traces, Low (P3) for warm cache/playback

#### Phase 4A: YAML Job Manifests (T-600 to T-602)
Export/import job definitions as portable YAML files for reproducibility and sharing.

#### Phase 4B: Session Traces / Swarm Debugging (T-603 to T-605)
Structured event logging for debugging swarm behavior (peer contributions, rescue mode triggers, etc.).

#### Phase 4C: Warm Cache Nodes (T-606 to T-608) [Optional]
Opt-in prefetching and caching of popular MBIDs to amplify Soulseek uploads via fast overlay serving.

#### Phase 4D: Playback-Aware Swarming (T-609 to T-611) [Optional]
Prioritize chunks around playback head for streaming use cases.

---

### Phase 5: Soulbeet Integration
**Tasks**: T-700 through T-712 (13 tasks)  
**Status**: Not started  
**Branch**: `experimental/multi-swarm`  
**Priority**: High (P1) for compat layer, Medium (P2) for advanced APIs

#### Phase 5A: slskd Compatibility Layer (T-700 to T-703)
Implement drop-in replacement endpoints so Soulbeet works unchanged:
- `/api/info`, `/api/search`, `/api/downloads` (POST + GET)

#### Phase 5B: slskdn-Native Job APIs (T-704 to T-708)
Advanced features for MBID-aware downloads:
- `/api/slskdn/capabilities` - Feature detection
- `/api/jobs/mb-release` - Album download by MB Release ID
- `/api/jobs/discography` - Artist discography download
- `/api/jobs/label-crate` - Label catalog download
- `/api/jobs` and `/api/jobs/{id}` - Job listing/inspection

#### Phase 5C: Optional Advanced APIs (T-709 to T-710)
- Warm cache hints endpoint
- Library health summary endpoint

#### Phase 5D: Soulbeet Client Integration (T-711 to T-712)
Documentation and test suite for Soulbeet client modifications.

---

## Priority Recommendations

### Immediate (Next Sprint)
1. **Complete Phase 1**: T-312 (album UI) + T-313 (tests)
2. **Start Phase 5 Compat Layer**: T-700 to T-703 for immediate Soulbeet compatibility

### Short Term (1-2 months)
3. **Phase 5 Native APIs**: T-704 to T-708 for advanced MBID job features
4. **Phase 2 Core Features**: T-400 to T-411 (canonical scoring, library health, rescue mode)

### Medium Term (2-4 months)
5. **Phase 3**: T-500 to T-510 (discographies, label crates, reputation, fairness)
6. **Phase 4A-B**: T-600 to T-605 (job manifests, session traces)

### Long Term (Optional)
7. **Phase 4C-D**: T-606 to T-611 (warm cache, playback-aware swarming)

---

## Documentation Reference

- **Architecture**: `docs/multi-swarm-architecture.md`
- **Roadmap**: `docs/multi-swarm-roadmap.md`
- **Soulbeet Overview**: `docs/soulbeet-integration-overview.md`
- **Soulbeet API Spec**: `docs/soulbeet-api-spec.md`
- **MusicBrainz Integration**: `docs/MUSICBRAINZ_INTEGRATION.md`
- **Task List**: `memory-bank/tasks.md`

---

## Task Count Summary

- **Phase 1** (MusicBrainz/Chromaprint): 14 tasks (12 complete, 2 pending)
- **Phase 2** (Mesh Integration): 12 tasks (all pending)
- **Phase 3** (Discovery/Reputation): 11 tasks (all pending)
- **Phase 4** (Manifests/Traces): 12 tasks (all pending)
- **Phase 5** (Soulbeet Integration): 13 tasks (all pending)

**Total**: 62 tasks across 5 phases

---

*Last updated: December 10, 2025*



