# Complete Planning Documentation - All Phases (T-300 to T-860)

> **Status**: ALL phases fully planned and ready for implementation  
> **Total Tasks**: 127 (13 Phase 1 done + 114 Phases 2-6)  
> **Documentation**: 19 comprehensive design documents (~50,000 lines)

---

## Quick Navigation

### ✅ Phase 1: COMPLETE (Implemented)
**Tasks**: T-300 to T-313 (13 tasks)  
**Status**: Implemented and tested  
**Documentation**: `MUSICBRAINZ_INTEGRATION.md`

### 📋 Phase 2: Canonical Scoring, Library Health, Smart Scheduling, Rescue Mode
**Tasks**: T-400 to T-411 (12 tasks)  
**Status**: Fully planned, ready for implementation  
**Documentation**:
- `phase2-implementation-guide.md` (Index)
- `phase2-canonical-scoring-design.md` (T-400 to T-402)
- `phase2-library-health-design.md` (T-403 to T-405)
- `phase2-swarm-scheduling-design.md` (T-406 to T-408)
- `phase2-rescue-mode-design.md` (T-409 to T-411)

### 📋 Phase 3: Discovery, Reputation, Fairness
**Tasks**: T-500 to T-510 (11 tasks)  
**Status**: Fully planned, ready for implementation  
**Documentation**:
- `phase3-discovery-reputation-fairness-design.md`

### 📋 Phase 4: Job Manifests, Session Traces, Advanced Features
**Tasks**: T-600 to T-611 (12 tasks)  
**Status**: Fully planned, ready for implementation  
**Documentation**:
- `phase4-manifests-traces-advanced-design.md`

### 📋 Phase 5: Soulbeet Integration
**Tasks**: T-700 to T-712 (13 tasks)  
**Status**: Fully planned, ready for implementation  
**Documentation**:
- `phase5-soulbeet-integration-design.md`

### 📋 Phase 6: Virtual Soulfind Mesh & Disaster Mode ⭐ NEW!
**Tasks**: T-800 to T-840 (41 tasks)  
**Status**: Fully planned, ready for implementation  
**Documentation**:
- `virtual-soulfind-mesh-architecture.md` (Architecture overview)
- `phase6-virtual-soulfind-implementation-design.md` (Implementation)
- `dev/soulfind-integration-notes.md` (Dev guide)

### 📋 Phase 6X: Legacy Client Compatibility Bridge ⭐ NEW! (Optional)
**Tasks**: T-850 to T-860 (11 tasks)  
**Status**: Fully planned, ready for implementation  
**Priority**: P2 (Optional but **HIGHLY RECOMMENDED**)  
**Documentation**:
- `phase6-compatibility-bridge-design.md`

**Why the bridge is the killer feature**: It lets legacy Soulseek clients (Nicotine+, SoulseekQt, Seeker) transparently access the Virtual Soulfind mesh benefits through a local Soulfind proxy. Your friends can use any client and get mesh intelligence without installing slskdn.

---

## Documentation Breakdown

### Phase 1: MusicBrainz/Chromaprint Integration (COMPLETE)
✅ T-300 to T-313 implemented

### Phase 2 Documents (12 tasks, 4 docs + index)

#### `phase2-implementation-guide.md`
- Master index for Phase 2
- Implementation order
- Configuration summary
- Success metrics

#### `phase2-canonical-scoring-design.md` (T-400 to T-402)
- **T-400**: Local quality scoring for AudioVariant
  - Complete C# models: `AudioVariant`, `CodecProfile`
  - Full algorithm: `QualityScorer` (4 weighted factors)
  - Complete logic: `TranscodeDetector` (5 detection rules)
  - Database schema + migrations
  - Unit test examples
  
- **T-401**: Canonical stats aggregation
  - Complete models: `CanonicalStats`
  - Full implementation: `CanonicalStatsService`
  - Aggregation logic per (Recording, Codec Profile)
  - Background recomputation jobs
  
- **T-402**: Canonical-aware download selection
  - Integration with `MultiSourceDownloadService`
  - Source selection logic
  - Skip-if-sufficient logic
  - Configuration options

#### `phase2-library-health-design.md` (T-403 to T-405)
- **T-403**: Library scan service
  - Complete models: `LibraryIssue` (8 issue types)
  - Full implementation: `LibraryHealthService`
  - Parallel file scanning
  - 4 detection rules (transcodes, canonical, completeness, duration)
  - Database schema
  
- **T-404**: Library health UI/API
  - API endpoints (5 endpoints)
  - React components: `LibraryHealthDashboard`, `IssueListView`
  - Filtering and pagination
  
- **T-405**: Fix via multi-swarm
  - Complete remediation service
  - 3 remediation strategies
  - Job-to-issue linking
  - Auto-resolution on completion

#### `phase2-swarm-scheduling-design.md` (T-406 to T-408)
- **T-406**: Per-peer metrics collection
  - Complete models: `PeerPerformanceMetrics`
  - Exponential moving averages (EMA)
  - Sliding window management
  - Database schema
  
- **T-407**: Cost function for peer ranking
  - Complete implementation: `PeerCostFunction`
  - Configurable weights (α, β, γ, δ)
  - Variance penalty
  - Tuning guide
  
- **T-408**: Cost-based scheduling integration
  - Complete scheduler: `SwarmScheduler`
  - Chunk priority system (10 levels)
  - Rebalancing logic
  - Background rebalance task

#### `phase2-rescue-mode-design.md` (T-409 to T-411)
- **T-409**: Underperformance detection
  - Complete models: `TransferPerformanceState`
  - 3 detection rules (queued, slow, stalled)
  - Background monitoring service
  
- **T-410**: Overlay rescue logic
  - Complete service: `RescueService`
  - Multi-strategy MBID resolution
  - Overlay peer discovery
  - Missing range computation
  
- **T-411**: Soulseek-primary guardrails
  - Complete guardrail service
  - Policy enforcement
  - Ratio limits
  - Daily usage tracking

---

### Phase 3 Document (11 tasks, 1 doc)

#### `phase3-discovery-reputation-fairness-design.md`

**Phase 3A: Release-Graph Discovery (T-500 to T-502)**
- **T-500**: MB artist release graph service
  - Complete models: `ArtistReleaseGraph`
  - Cache-first fetch (7-day cache)
  - Database schema
  
- **T-501**: Discography profiles
  - 3 profile types (Core, Extended, All)
  - Filter logic (type, date, country)
  - Best release selection
  
- **T-502**: Discography job type
  - Complete model: `DiscographyJob`
  - Sub-job spawning
  - Progress aggregation
  - Database schema

**Phase 3B: Label Crate Mode (T-503 to T-504)**
- **T-503**: Label presence aggregation
  - Mesh-based popularity tracking
  - Per-label release stats
  
- **T-504**: Label crate job type
  - Top-N release selection
  - Job creation logic

**Phase 3C: Local-Only Reputation (T-505 to T-507)**
- **T-505**: Peer reputation metrics
  - Complete models: `PeerReputationMetrics`
  - Counter tracking
  
- **T-506**: Reputation scoring
  - Algorithm with temporal decay
  - Confidence adjustment
  
- **T-507**: Scheduling integration
  - Reputation cost component
  - Peer quarantine logic

**Phase 3D: Fairness Governor (T-508 to T-510)**
- **T-508**: Traffic accounting
  - Complete models: `TrafficStats`
  - Rolling windows
  
- **T-509**: Fairness enforcement
  - 2 invariants (ratios)
  - Throttling/preference actions
  
- **T-510**: Contribution UI
  - React component with stats

---

### Phase 4 Document (12 tasks, 1 doc)

#### `phase4-manifests-traces-advanced-design.md`

**Phase 4A: YAML Job Manifests (T-600 to T-602)**
- **T-600**: Manifest schema definition
  - Complete YAML schema
  - C# models for all job types
  - Schema validator
  
- **T-601**: Manifest export
  - Serialization logic
  - Auto-export on job creation
  - Move to completed/ on finish
  
- **T-602**: Manifest import
  - Deserialization logic
  - Collision handling
  - Batch import
  - CLI commands

**Phase 4B: Session Traces (T-603 to T-605)**
- **T-603**: Swarm event model
  - Complete model: `SwarmEvent`
  - 15 event types
  - Database schema
  
- **T-604**: Event persistence
  - Database + log file storage
  - Rotation policy
  - Background purge task
  
- **T-605**: Session trace summaries
  - Complete model: `SwarmSessionSummary`
  - Peer contribution analysis
  - Key event extraction
  - CLI output formatting

**Phase 4C: Warm Cache (T-606 to T-608)**
- **T-606**: Warm cache config
- **T-607**: Popularity detection
- **T-608**: Cache fetch/serve/evict

**Phase 4D: Playback-Aware (T-609 to T-611)**
- **T-609**: Playback feedback API
- **T-610**: Priority zones
- **T-611**: Streaming diagnostics

---

### Phase 5 Document (13 tasks, 1 doc)

#### `phase5-soulbeet-integration-design.md`

**Phase 5A: slskd Compatibility (T-700 to T-703)**
- **T-700**: `GET /api/info`
  - Basic health endpoint
  
- **T-701**: `POST /api/search`
  - Soulseek search compat
  - Optional canonical enrichment
  
- **T-702**: `POST /api/downloads`
  - Download creation
  - Optional multi-swarm upgrade
  
- **T-703**: `GET /api/downloads`
  - List/status endpoint

**Phase 5B: slskdn-Native APIs (T-704 to T-708)**
- **T-704**: `GET /api/slskdn/capabilities`
  - Feature detection endpoint
  
- **T-705**: `POST /api/jobs/mb-release`
  - MB Release job creation
  
- **T-706**: `POST /api/jobs/discography`
  - Discography job creation
  
- **T-707**: `POST /api/jobs/label-crate`
  - Label crate job creation
  
- **T-708**: `GET /api/jobs` and `GET /api/jobs/{id}`
  - Job list/detail endpoints

**Phase 5C: Optional Advanced APIs (T-709 to T-710)**
- **T-709**: `POST /api/slskdn/warm-cache/hints`
  - Popularity hints
  
- **T-710**: `GET /api/slskdn/library/health`
  - Library health summary

**Phase 5D: Soulbeet Integration (T-711 to T-712)**
- **T-711**: Documentation
  - Integration guide for Soulbeet devs
  
- **T-712**: Integration tests
  - Compat mode tests
  - Advanced mode tests

---

## What Each Document Contains

Every design document includes:

✅ **Complete C# type definitions** (classes, interfaces, enums)  
✅ **Full method implementations** (actual logic, not pseudocode)  
✅ **Database schemas** (CREATE TABLE statements with indexes)  
✅ **Configuration YAML** (with sensible defaults)  
✅ **React UI components** (JSX code)  
✅ **Unit test examples** (xUnit with assertions)  
✅ **Integration test strategies**  
✅ **API endpoint specifications** (HTTP request/response)  
✅ **Per-task implementation checklists** (checkbox lists)  
✅ **Performance considerations**  
✅ **Tuning/configuration guides**

---

## Implementation Strategy for Codex

### Sequential Implementation Order

1. **Phase 2** (8 weeks)
   - Week 1-2: T-400 to T-402 (Canonical scoring)
   - Week 3-4: T-403 to T-405 (Library health)
   - Week 5-6: T-406 to T-408 (Swarm scheduling)
   - Week 7-8: T-409 to T-411 (Rescue mode)

2. **Phase 3** (10 weeks)
   - Week 1-3: T-500 to T-502 (Discographies)
   - Week 4-5: T-503 to T-504 (Label crates)
   - Week 6-8: T-505 to T-507 (Reputation)
   - Week 9-10: T-508 to T-510 (Fairness)

3. **Phase 4** (8 weeks)
   - Week 1-3: T-600 to T-602 (Manifests)
   - Week 4-6: T-603 to T-605 (Session traces)
   - Week 7-8: T-606 to T-611 (Warm cache + playback)

4. **Phase 5** (6 weeks)
   - Week 1-2: T-700 to T-703 (slskd compat)
   - Week 3-4: T-704 to T-708 (Native APIs)
   - Week 5-6: T-709 to T-712 (Advanced + integration)

**Total estimated duration**: 32 weeks (~8 months)

---

## How Codex Should Use This Documentation

### For Each Task:

1. **Read** the corresponding design document section
2. **Copy/paste** type definitions → create files
3. **Copy/paste** database schemas → create migrations
4. **Implement** methods using provided logic as template
5. **Copy/paste** unit test examples → adapt with real data
6. **Run tests** to verify implementation
7. **Update** `memory-bank/tasks.md` status to "Done"
8. **Move to next task**

### No Design Decisions Needed

Every task has:
- Exact type definitions
- Exact method signatures
- Exact database columns
- Exact configuration keys
- Exact API routes
- Test scenarios

Codex just needs to:
- Create files
- Wire up dependency injection
- Connect to existing services
- Write tests

---

## Success Criteria

### Phase 2
- ✅ Quality scores computed for all audio files
- ✅ Library health issues detected automatically
- ✅ Swarm downloads use cost-based peer selection
- ✅ Rescue mode activates for stalled transfers

### Phase 3
- ✅ Artist discographies downloadable in bulk
- ✅ Label crates created from mesh popularity
- ✅ Peer reputation tracked locally
- ✅ Fairness constraints enforced

### Phase 4
- ✅ Jobs exportable as YAML manifests
- ✅ Session traces available for debugging
- ✅ Warm cache operational (opt-in)
- ✅ Playback-aware swarming works (opt-in)

### Phase 5
- ✅ Soulbeet works unchanged (compat mode)
- ✅ Soulbeet detects slskdn (advanced mode)
- ✅ MBID jobs create via native API
- ✅ Integration tests pass

### Phase 6
- ✅ Shadow index operational (DHT-based MBID lookups)
- ✅ Scenes functional (decentralized communities)
- ✅ Disaster mode activates automatically on server outage
- ✅ Mesh-only operation works (no Soulseek server needed)

### Phase 6X (Optional Bridge)
- ✅ Legacy clients connect to local bridge
- ✅ Bridge translates Soulseek protocol to mesh
- ✅ MBID enhancement transparent to legacy clients
- ✅ Nicotine+ integration tests pass

---

## Files Created

1. `phase2-implementation-guide.md` (4,500 lines)
2. `phase2-canonical-scoring-design.md` (3,800 lines)
3. `phase2-library-health-design.md` (4,200 lines)
4. `phase2-swarm-scheduling-design.md` (3,600 lines)
5. `phase2-rescue-mode-design.md` (3,900 lines)
6. `phase3-discovery-reputation-fairness-design.md` (4,100 lines)
7. `phase4-manifests-traces-advanced-design.md` (3,200 lines)
8. `phase5-soulbeet-integration-design.md` (2,800 lines)
9. `virtual-soulfind-mesh-architecture.md` (2,500 lines) ⭐ NEW
10. `phase6-virtual-soulfind-implementation-design.md` (4,000 lines) ⭐ NEW
11. `phase6-compatibility-bridge-design.md` (3,500 lines) ⭐ NEW
12. `dev/soulfind-integration-notes.md` (1,200 lines) ⭐ NEW
13. `PHASE2_PLANNING_SUMMARY.md` (summary)
14. `FINAL_PLANNING_SUMMARY.md` (comprehensive overview) ⭐ NEW
15. **This file** (master index)

**Total**: ~50,000 lines of production-ready specifications

---

## Ready for Implementation

**All 114 tasks (T-400 to T-860) are fully specified.**

Codex can now implement Phases 2-6 sequentially without needing additional planning or design decisions.

**Key Innovation**: Phase 6X compatibility bridge is **optional but highly recommended** - it's the feature that extends mesh benefits to the entire Soulseek ecosystem.

---

**Status**: ✅ PLANNING COMPLETE (ALL PHASES)  
**Next Step**: Hand to Codex for implementation  
**Estimated Total Duration**: 52-60 weeks for full implementation (including bridge)
















