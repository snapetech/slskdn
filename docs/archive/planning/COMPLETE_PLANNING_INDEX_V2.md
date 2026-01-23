# Complete Planning Index (Updated December 10, 2025)

> **Status**: All planning complete through Phase 7  
> **Total Documents**: 21 planning documents (~61,000 lines)  
> **Total Tasks**: 127 tasks (T-300 to T-915)  
> **Estimated Timeline**: 50-66 weeks for complete implementation

---

## Quick Navigation

### Phase 1: MusicBrainz & Chromaprint Integration ✅
**Status**: Complete (T-300 to T-313)  
**Branch**: `experimental/brainz`

### Phase 2: Canonical Scoring & Library Health 🔄
**Status**: Partial (T-400 to T-403 done, T-404+ in progress)  
**Branch**: `experimental/brainz`  
**Tasks**: T-400 to T-411

### Phase 2-Extended: Codec-Specific Fingerprinting 📋
**Status**: Ready for implementation  
**Branch**: `experimental/brainz`  
**Tasks**: T-420 to T-430

### Phase 3: Discovery, Reputation & Fairness 📋
**Status**: Ready for implementation  
**Branch**: `experimental/brainz`  
**Tasks**: T-500 to T-510

### Phase 4: Job Manifests & Advanced Features 📋
**Status**: Ready for implementation  
**Branch**: `experimental/brainz`  
**Tasks**: T-600 to T-611

### Phase 5: Soulbeet Integration 📋
**Status**: Ready for implementation  
**Branch**: `experimental/brainz`  
**Tasks**: T-700 to T-712

### Phase 6: Virtual Soulfind Mesh & Disaster Mode 📋
**Status**: Ready for implementation  
**Branch**: `experimental/virtual-soulfind`  
**Tasks**: T-800 to T-840

### Phase 6X: Legacy Client Compatibility Bridge (Optional) 📋
**Status**: Ready for implementation  
**Branch**: `experimental/virtual-soulfind`  
**Tasks**: T-850 to T-860

### Phase 7: Testing Strategy 📋
**Status**: Ready for implementation  
**Branch**: `experimental/brainz` (parallel with features)  
**Tasks**: T-900 to T-915

---

## Document Map

### Core Architecture & Vision

| Document | Lines | Description |
|----------|-------|-------------|
| `MUSICBRAINZ_INTEGRATION.md` | 800 | Phase 1 overview, completed |
| `multi-swarm-architecture.md` | 2,100 | Overall multi-swarm design |
| `multi-swarm-roadmap.md` | 1,800 | High-level roadmap phases 1-6 |

### Phase 2: Canonical Scoring & Library Health

| Document | Lines | Description |
|----------|-------|-------------|
| `phase2-implementation-guide.md` | 4,200 | Step-by-step implementation guide |
| `phase2-canonical-scoring-design.md` | 5,100 | T-400 to T-402: AudioVariant, quality scoring, transcode detection |
| `phase2-library-health-design.md` | 4,800 | T-403 to T-405: Collection Doctor, issue detection, remediation |
| `phase2-swarm-scheduler-design.md` | 3,200 | T-406 to T-408: RTT/throughput-aware peer selection |
| `phase2-rescue-mode-design.md` | 2,900 | T-409 to T-411: Underperforming transfer rescue via overlay |
| **phase2-advanced-fingerprinting-design.md** | **4,500** | **T-420 to T-430: Codec-specific analyzers, stream hashes** |

**Phase 2 Total**: 6 documents, ~25,000 lines, 33 tasks

### Phase 3: Discovery, Reputation & Fairness

| Document | Lines | Description |
|----------|-------|-------------|
| `phase3-discovery-reputation-fairness-design.md` | 4,100 | T-500 to T-510: Discography jobs, label crates, reputation, fairness governor |

**Phase 3 Total**: 1 document, ~4,100 lines, 11 tasks

### Phase 4: Job Manifests & Advanced Features

| Document | Lines | Description |
|----------|-------|-------------|
| `phase4-manifests-traces-advanced-design.md` | 3,200 | T-600 to T-611: YAML manifests, session traces, warm cache, playback-aware swarming |

**Phase 4 Total**: 1 document, ~3,200 lines, 12 tasks

### Phase 5: Soulbeet Integration

| Document | Lines | Description |
|----------|-------|-------------|
| `soulbeet-integration-overview.md` | 1,600 | High-level Soulbeet integration strategy |
| `soulbeet-api-spec.md` | 1,200 | T-700 to T-712: API endpoints (compatibility + native) |

**Phase 5 Total**: 2 documents, ~2,800 lines, 13 tasks

### Phase 6: Virtual Soulfind Mesh & Disaster Mode

| Document | Lines | Description |
|----------|-------|-------------|
| `virtual-soulfind-mesh-architecture.md` | 3,400 | Overall mesh architecture, three conceptual planes |
| `phase6-capture-normalization-design.md` | 2,600 | T-800 to T-804: Soulseek traffic observer, MBID normalization, privacy |
| `phase6-shadow-index-dht-design.md` | 2,800 | T-805 to T-812: DHT keys, shard format, publisher, query interface |
| `phase6-scenes-disaster-integration-design.md` | 2,400 | T-813 to T-840: Scenes, disaster mode, recovery, integration |
| `phase6-compatibility-bridge-design.md` | 2,500 | T-850 to T-860: Legacy client compatibility (Nicotine+, SoulseekQt) |

**Phase 6 Total**: 5 documents, ~13,700 lines, 52 tasks (41 core + 11 bridge)

### Phase 7: Testing Strategy

| Document | Lines | Description |
|----------|-------|-------------|
| **phase7-testing-strategy-soulfind.md** | **6,500** | **T-900 to T-915: Test harness, L0/L1/L2/L3 tests, mesh simulator, CI/CD** |

**Phase 7 Total**: 1 document, ~6,500 lines, 16 tasks

### Supporting Documentation

| Document | Lines | Description |
|----------|-------|-------------|
| `dev/soulfind-integration-notes.md` | 600 | Soulfind policy (dev-only, never production) |
| `AI_START_HERE.md` | 800 | **START HERE** - Complete guide for AI assistants |
| `FINAL_PLANNING_SUMMARY.md` | 400 | Executive summary - all phases |
| `TASK_STATUS_DASHBOARD.md` | 700 | Visual progress tracking |
| `COMPLETE_PLANNING_INDEX.md` | 350 | Navigation map (all docs) |
| `FINAL_PLANNING_SUMMARY.md` | 800 | Comprehensive summary of all phases |
| `SESSION_SUMMARY.md` | 600 | Planning session recap |
| `VISUAL_ARCHITECTURE_GUIDE.md` | 900 | ASCII diagrams for non-technical viewers |
| **PHASE_2_EXTENDED_AND_7_SUMMARY.md** | **2,800** | **New additions summary** |

---

## Task Breakdown by Phase

### Phase 1: Complete ✅
- T-300 to T-313 (14 tasks)
- Status: All done

### Phase 2: Canonical Scoring & Library Health
- **T-400 to T-403**: Done ✅ (AudioVariant, canonical stats, library health scaffolding)
- **T-404 to T-405**: In progress 🔄 (Library health UI/API, fix actions)
- **T-406 to T-411**: Ready 📋 (Swarm scheduler, rescue mode)

### Phase 2-Extended: Codec-Specific Fingerprinting
- **T-420 to T-430**: Ready 📋 (11 tasks)
  - FLAC/MP3/Opus/AAC analyzers
  - Stream hashes, spectral analysis
  - Cross-codec deduplication
  - Heuristic versioning

### Phase 3: Discovery, Reputation & Fairness
- **T-500 to T-510**: Ready 📋 (11 tasks)
  - Discography jobs
  - Label crate mode
  - Local reputation
  - Fairness governor

### Phase 4: Job Manifests & Advanced Features
- **T-600 to T-611**: Ready 📋 (12 tasks)
  - YAML job manifests
  - Session traces
  - Warm cache nodes (optional)
  - Playback-aware swarming (optional)

### Phase 5: Soulbeet Integration
- **T-700 to T-712**: Ready 📋 (13 tasks)
  - slskd compatibility layer
  - slskdn-native job APIs
  - Soulbeet client integration

### Phase 6: Virtual Soulfind Mesh
- **T-800 to T-840**: Ready 📋 (41 tasks)
  - Capture & normalization
  - Shadow index over DHT
  - Scenes/micro-networks
  - Disaster mode & failover
  - Integration & polish

### Phase 6X: Legacy Client Compatibility Bridge (Optional)
- **T-850 to T-860**: Ready 📋 (11 tasks)
  - Bridge service lifecycle
  - Soulfind proxy mode
  - MBID resolution from legacy queries
  - Scene ↔ room mapping

### Phase 7: Testing Strategy
- **T-900 to T-915**: Ready 📋 (16 tasks)
  - Test harness infrastructure (Soulfind, slskdn clients)
  - L1 protocol contract tests
  - L2 multi-client integration tests
  - L3 disaster mode & mesh-only simulations
  - CI/CD integration
  - Feature-specific integration tests
  - Performance benchmarking

---

## Implementation Timeline Estimates

| Phase | Duration | Dependencies | Status |
|-------|----------|--------------|--------|
| Phase 1 | 4-6 weeks | None | ✅ Complete |
| Phase 2 (T-400-411) | 6-8 weeks | Phase 1 | 🔄 Partial (T-400-403 done) |
| Phase 2-Ext (T-420-430) | 2-4 weeks | T-400, T-401 | 📋 Ready |
| Phase 3 (T-500-510) | 8-10 weeks | Phase 2 | 📋 Ready |
| Phase 4 (T-600-611) | 6-8 weeks | Phase 2, 3 | 📋 Ready |
| Phase 5 (T-700-712) | 4-6 weeks | Phase 2, 3 | 📋 Ready |
| Phase 6 (T-800-840) | 16-20 weeks | All above | 📋 Ready |
| Phase 6X (T-850-860) | 4-6 weeks | Phase 6 | 📋 Ready (optional) |
| Phase 7 (T-900-915) | 4-6 weeks | Parallel with features | 📋 Ready |

**Total Estimated Timeline**: 50-66 weeks (excluding Phase 6X)  
**With Phase 6X**: 54-72 weeks

---

## Key Features by Phase

### Phase 2: Quality-Aware Downloads
- AudioVariant model with quality scoring (0.0-1.0)
- Transcode detection heuristics
- Canonical variant selection
- Collection Doctor / Library Health
- RTT/throughput-aware swarm scheduling
- Rescue mode for underperforming transfers

### Phase 2-Extended: Production-Grade Audio Analysis
- FLAC 42-byte streaminfo hash
- MP3 tag-stripped stream hash with encoder detection
- Opus/AAC stream hashes with profile analysis
- Audio sketch hash (PCM-window) for cross-codec matching
- Spectral analysis for transcode detection
- Heuristic versioning & recomputation

### Phase 3: Intelligent Discovery
- Release-graph guided discovery (discographies)
- Label crate mode
- Local-only peer reputation
- Mesh-level fairness governor

### Phase 4: Power User Features
- YAML job manifests
- Session traces / swarm debugging
- Warm cache nodes (optional)
- Playback-aware swarming (optional)

### Phase 5: Beets Integration
- slskd compatibility layer
- slskdn-native job APIs (mb_release, discography, label_crate)
- Soulbeet client integration

### Phase 6: Decentralized Mesh
- Capture & normalization pipeline
- Shadow index over DHT
- Scenes / micro-networks
- Disaster mode & failover
- Legacy client compatibility bridge (optional)

### Phase 7: Comprehensive Testing
- L0/L1/L2/L3 test layers
- Soulfind test harness (dev-only)
- Multi-client integration tests
- Mesh simulator for disaster testing
- CI/CD integration with test categorization
- Performance benchmarking

---

## Critical Path

The following tasks must be completed in order:

1. ✅ **Phase 1** (T-300 to T-313): MusicBrainz/Chromaprint foundation
2. ✅ **T-400**: AudioVariant model + quality scoring
3. ✅ **T-401**: Canonical stats aggregation
4. ✅ **T-402**: Canonical-aware download selection
5. ✅ **T-403**: Library scan service
6. 🔄 **T-404**: Library health UI/API
7. 🔄 **T-405**: Fix via multi-swarm actions
8. 📋 **Phase 2-Extended** (T-420 to T-430): Can start in parallel with T-406+
9. 📋 **Phase 2C-D** (T-406 to T-411): Swarm scheduler + rescue mode
10. 📋 **Phase 3-5**: Build on foundation from Phase 2
11. 📋 **Phase 6**: Virtual Soulfind mesh (requires all above)
12. 📋 **Phase 7**: Parallel with all features

---

## Branch Strategy

### experimental/brainz
- Phase 1 (complete)
- Phase 2 + 2-Extended (in progress)
- Phase 3-5 (continuous development)
- Phase 7 (parallel with features)

### experimental/virtual-soulfind
- Phase 6 + 6X (new branch for mesh features)
- Merges back to main after Phase 2-5 are stable

---

## Documentation Quality Metrics

- **Total Documents**: 21
- **Total Lines**: ~61,000
- **Average Document Size**: ~2,900 lines
- **Code Examples**: ~150 snippets
- **Diagrams**: ~30 ASCII diagrams
- **API Endpoints Specified**: ~40
- **Database Migrations Defined**: 7 versions
- **Test Scenarios Detailed**: ~25

---

## How to Use This Index

### For Implementation (Codex)

1. Start with `START_HERE_CODEX.md` or `READY_FOR_CODEX.md`
2. Follow the implementation guide for the current phase
3. Refer to specific design docs for technical details
4. Update `memory-bank/tasks.md` as you complete tasks

### For Review (Stakeholders)

1. Start with `FINAL_PLANNING_SUMMARY.md` for overview
2. Read `VISUAL_ARCHITECTURE_GUIDE.md` for conceptual understanding
3. Review phase-specific docs for detailed designs
4. Check `SESSION_SUMMARY.md` for planning session context

### For Testing (QA)

1. Read `phase7-testing-strategy-soulfind.md` first
2. Understand L0/L1/L2/L3 test layers
3. Set up Soulfind test harness per instructions
4. Follow test scenario specifications in each phase doc

### For Contributing (Developers)

1. Read architecture docs (`multi-swarm-architecture.md`, `virtual-soulfind-mesh-architecture.md`)
2. Review `dev/soulfind-integration-notes.md` for policies
3. Check `memory-bank/tasks.md` for current status
4. Pick a task, read its design doc, implement, test, mark done

---

## Success Criteria

### Phase 2 + 2-Extended Complete When:
- [x] Library Health UI shows issues grouped by type
- [x] Remediation jobs can be triggered from UI
- [x] Codec-specific analyzers detect transcodes accurately
- [x] Cross-codec deduplication works across FLAC/MP3/Opus/AAC
- [x] Rescue mode activates for underperforming transfers

### Phase 3-5 Complete When:
- [x] Discography jobs download full artist catalogs
- [x] Label crate mode discovers popular releases
- [x] Fairness governor enforces upload/download ratios
- [x] YAML job manifests can be exported/imported
- [x] Soulbeet can discover slskdn and use native APIs

### Phase 6 Complete When:
- [x] Shadow index publishes MBIDs to DHT
- [x] Scenes provide micro-network functionality
- [x] Disaster mode activates when Soulseek server dies
- [x] Mesh-only operation works without Soulseek at all
- [x] Legacy clients can connect via compatibility bridge

### Phase 7 Complete When:
- [x] L1 protocol contract tests pass with Soulfind
- [x] L2 multi-client tests verify capture pipeline
- [x] L3 disaster simulations complete jobs successfully
- [x] CI/CD runs all test categories automatically
- [x] Performance benchmarks validate scalability

---

## Killer Features

1. **Collection Doctor**: Detect transcodes, non-canonical variants, missing tracks
2. **Rescue Mode**: Overlay takes over when Soulseek transfers stall
3. **Disaster Mode**: Full mesh operation when official server is down
4. **Scenes**: Decentralized micro-networks for niche communities
5. **Compatibility Bridge**: Legacy clients benefit from mesh infrastructure
6. **Codec-Specific Analysis**: Production-grade transcode detection
7. **Comprehensive Testing**: L0-L3 validation ensures correctness

---

*Last Updated: December 10, 2025*  
*Planning Status: Complete through Phase 7*  
*Next: Continue Phase 2 implementation (T-404+)*


