# VirtualSoulfind v2 - Task Breakdown

**Branch**: `experimental/multi-source-swarm`  
**Created**: December 11, 2025  
**Status**: Planning Phase  
**Design Doc**: `docs/virtualsoulfind-v2-design.md`

> **Project Note**: This is a fork of [slskd](https://github.com/slskd/slskd). See [README.md](README.md#acknowledgments) for attribution.

---

## Overview

This document breaks down the VirtualSoulfind v2 design into concrete, implementable tasks. Tasks are organized by phase and follow the same principles as the service fabric tasks: small, focused, reviewable changes.

**Priority**: These tasks are DEFERRED until after:
- ✅ T-SF01-04 (Service fabric foundation + HTTP gateway)
- ✅ H-01 (Gateway auth/CSRF)
- ⏳ T-SF05 (Security review)
- ⏳ T-SF06 (Developer docs)
- ⏳ T-SF07 (Metrics/observability)
- ⏳ H-02 (Work budget)
- ⏳ H-08 (Soulseek caps) - **CRITICAL DEPENDENCY**

---

## Phase 1: Foundation (V2-P1)

### V2-P1-01: Data Model & Schema Design
**Priority**: P0  
**Depends on**: None  
**Status**: 📋 Planned

**Tasks**:
- [ ] V2-P1-01-001: Design database schema for core entities (Artist, ReleaseGroup, Release, Track)
- [ ] V2-P1-01-002: Design schema for local library entities (LocalFile, VerifiedCopy)
- [ ] V2-P1-01-003: Design schema for intent entities (DesiredRelease, DesiredTrack)
- [ ] V2-P1-01-004: Design schema for source registry (SourceCandidate)
- [ ] V2-P1-01-005: Create EF migrations or SQLite schema scripts
- [ ] V2-P1-01-006: Add indices for common queries (MBID lookups, track searches)

**Deliverables**:
- Database schema definition
- Migration scripts
- Basic entity classes

---

### V2-P1-02: Backend Interface Abstractions
**Priority**: P0  
**Depends on**: None  
**Status**: 📋 Planned

**Tasks**:
- [ ] V2-P1-02-001: Define `IContentBackend` interface
- [ ] V2-P1-02-002: Define `ContentBackendType` enum
- [ ] V2-P1-02-003: Define `SourceCandidate` model
- [ ] V2-P1-02-004: Define `SourceCandidateValidationResult` model
- [ ] V2-P1-02-005: Add XML docs and usage examples

**Deliverables**:
- Core backend abstraction interfaces
- Supporting types and enums

---

### V2-P1-03: Virtual Catalogue Store
**Priority**: P0  
**Depends on**: V2-P1-01  
**Status**: 📋 Planned

**Tasks**:
- [ ] V2-P1-03-001: Implement `IVirtualCatalogueStore` interface
- [ ] V2-P1-03-002: Implement CRUD operations for Artist/Release/Track
- [ ] V2-P1-03-003: Implement MBID-based lookups
- [ ] V2-P1-03-004: Add search/filter capabilities
- [ ] V2-P1-03-005: Implement caching layer (in-memory for hot data)
- [ ] V2-P1-03-006: Add unit tests

**Deliverables**:
- Virtual catalogue store implementation
- Query methods for artists, releases, tracks
- Unit tests

---

## Phase 2: Intent & Planning (V2-P2)

### V2-P2-01: Intent Queue
**Priority**: P0  
**Depends on**: V2-P1-01, V2-P1-03  
**Status**: 📋 Planned

**Tasks**:
- [ ] V2-P2-01-001: Implement `IIntentQueue` interface
- [ ] V2-P2-01-002: Implement `DesiredRelease` CRUD operations
- [ ] V2-P2-01-003: Implement `DesiredTrack` CRUD operations
- [ ] V2-P2-01-004: Add priority-based queue retrieval
- [ ] V2-P2-01-005: Add status filtering and updates
- [ ] V2-P2-01-006: Implement persistence (SQLite)
- [ ] V2-P2-01-007: Add unit tests

**Deliverables**:
- Intent queue implementation
- Persistent storage of user intents
- Priority and status management

---

### V2-P2-02: Source Registry
**Priority**: P0  
**Depends on**: V2-P1-01, V2-P1-02  
**Status**: 📋 Planned

**Tasks**:
- [ ] V2-P2-02-001: Implement `ISourceRegistry` interface
- [ ] V2-P2-02-002: Implement `SourceCandidate` CRUD operations
- [ ] V2-P2-02-003: Add candidate discovery and registration
- [ ] V2-P2-02-004: Implement trust score updates
- [ ] V2-P2-02-005: Add candidate filtering (by backend, quality, trust)
- [ ] V2-P2-02-006: Implement candidate aging/expiration
- [ ] V2-P2-02-007: Add unit tests

**Deliverables**:
- Source registry implementation
- Candidate management with trust scores
- Filtering and expiration logic

---

### V2-P2-03: Multi-Source Planner
**Priority**: P0  
**Depends on**: V2-P2-01, V2-P2-02, V2-P1-02  
**Status**: 📋 Planned

**Tasks**:
- [ ] V2-P2-03-001: Define `IMultiSourcePlanner` interface
- [ ] V2-P2-03-002: Define `TrackAcquisitionPlan` and `PlanStep` models
- [ ] V2-P2-03-003: Define `PlanningMode` enum (Offline, SoulseekFriendly, MeshOnly)
- [ ] V2-P2-03-004: Implement planner for single track
- [ ] V2-P2-03-005: Implement planner for full release
- [ ] V2-P2-03-006: Add mode-specific logic (MeshOnly, OfflinePlanning, etc.)
- [ ] V2-P2-03-007: Integrate with source registry
- [ ] V2-P2-03-008: Implement backend priority/preference logic
- [ ] V2-P2-03-009: Add unit tests for all modes

**Deliverables**:
- Multi-source planner implementation
- Planning modes
- Integration with source registry

---

## Phase 3: Verification & Matching (V2-P3)

### V2-P3-01: Match Engine
**Priority**: P0  
**Depends on**: V2-P1-03  
**Status**: 📋 Planned

**Tasks**:
- [ ] V2-P3-01-001: Define `IMatchEngine` interface
- [ ] V2-P3-01-002: Implement MBID-based matching
- [ ] V2-P3-01-003: Implement duration-based matching (with tolerance)
- [ ] V2-P3-01-004: Implement track number/disc number matching
- [ ] V2-P3-01-005: Add configurable matching strictness
- [ ] V2-P3-01-006: Implement match scoring algorithm
- [ ] V2-P3-01-007: Add unit tests with various match scenarios

**Deliverables**:
- Match engine implementation
- Configurable matching criteria
- Scoring system

---

### V2-P3-02: Verified Copy Registry
**Priority**: P0  
**Depends on**: V2-P1-01, V2-P3-01  
**Status**: 📋 Planned

**Tasks**:
- [ ] V2-P3-02-001: Implement `IVerifiedCopyRegistry` interface
- [ ] V2-P3-02-002: Implement `VerifiedCopy` CRUD operations
- [ ] V2-P3-02-003: Add hash-based verification
- [ ] V2-P3-02-004: Add duration-based verification
- [ ] V2-P3-02-005: Implement verification confidence levels
- [ ] V2-P3-02-006: Add lookup by track ID and hash
- [ ] V2-P3-02-007: Add unit tests

**Deliverables**:
- Verified copy registry
- Hash and duration verification
- Confidence tracking

---

### V2-P3-03: Canonical Naming
**Priority**: P1  
**Depends on**: V2-P1-03  
**Status**: 📋 Planned

**Tasks**:
- [ ] V2-P3-03-001: Define `ICanonicalNamer` interface
- [ ] V2-P3-03-002: Implement naming template system
- [ ] V2-P3-03-003: Add configurable naming patterns
- [ ] V2-P3-03-004: Implement path sanitization (no invalid chars)
- [ ] V2-P3-03-005: Add collision detection and resolution
- [ ] V2-P3-03-006: Add unit tests with various metadata scenarios

**Deliverables**:
- Canonical naming implementation
- Template system
- Path sanitization

---

## Phase 4: Backend Implementations (V2-P4)

### V2-P4-01: Soulseek Backend
**Priority**: P0  
**Depends on**: V2-P1-02, H-08 (Soulseek caps)  
**Status**: 📋 Planned

**Tasks**:
- [ ] V2-P4-01-001: Implement `SoulseekContentBackend` class
- [ ] V2-P4-01-002: Integrate with existing Soulseek client
- [ ] V2-P4-01-003: Implement candidate discovery via search
- [ ] V2-P4-01-004: Implement candidate discovery via browse
- [ ] V2-P4-01-005: Enforce Soulseek caps from H-08
- [ ] V2-P4-01-006: Add rate limiting per H-08 config
- [ ] V2-P4-01-007: Implement candidate validation
- [ ] V2-P4-01-008: Add unit tests and integration tests

**Deliverables**:
- Soulseek backend implementation
- H-08 cap enforcement
- Rate limiting integration

---

### V2-P4-02: Mesh/DHT Backend
**Priority**: P0  
**Depends on**: V2-P1-02, T-SF01-03 (Service fabric)  
**Status**: 📋 Planned

**Tasks**:
- [ ] V2-P4-02-001: Implement `MeshDhtContentBackend` class
- [ ] V2-P4-02-002: Integrate with existing DHT directory
- [ ] V2-P4-02-003: Implement candidate discovery via DHT keys
- [ ] V2-P4-02-004: Add content ID → MBID mapping
- [ ] V2-P4-02-005: Implement candidate validation via mesh service calls
- [ ] V2-P4-02-006: Add privacy guards (no Soulseek usernames in DHT)
- [ ] V2-P4-02-007: Add unit tests

**Deliverables**:
- Mesh/DHT backend implementation
- Integration with service fabric
- Privacy-preserving design

---

### V2-P4-03: Local Library Backend
**Priority**: P0  
**Depends on**: V2-P1-02, V2-P3-01  
**Status**: 📋 Planned

**Tasks**:
- [ ] V2-P4-03-001: Implement `LocalLibraryBackend` class
- [ ] V2-P4-03-002: Integrate with existing file scanner
- [ ] V2-P4-03-003: Implement candidate discovery from local files
- [ ] V2-P4-03-004: Use match engine for local file → track mapping
- [ ] V2-P4-03-005: Add hash and duration caching
- [ ] V2-P4-03-006: Add unit tests

**Deliverables**:
- Local library backend
- File scanner integration
- Match engine integration

---

### V2-P4-04: Torrent Backend (Optional)
**Priority**: P2  
**Depends on**: V2-P1-02  
**Status**: 📋 Planned (Future)

**Tasks**:
- [ ] V2-P4-04-001: Implement `TorrentContentBackend` class
- [ ] V2-P4-04-002: Integrate with BT/multi-swarm infrastructure
- [ ] V2-P4-04-003: Implement candidate discovery via torrent files
- [ ] V2-P4-04-004: Add torrent metadata parsing
- [ ] V2-P4-04-005: Add unit tests

**Deliverables**:
- Torrent backend (future enhancement)

---

### V2-P4-05: HTTP/LAN Backends (Optional)
**Priority**: P2  
**Depends on**: V2-P1-02  
**Status**: 📋 Planned (Future)

**Tasks**:
- [ ] V2-P4-05-001: Implement `HttpContentBackend` class
- [ ] V2-P4-05-002: Implement `LanContentBackend` class
- [ ] V2-P4-05-003: Add URL-based candidate discovery
- [ ] V2-P4-05-004: Add LAN peer discovery
- [ ] V2-P4-05-005: Add unit tests

**Deliverables**:
- HTTP/LAN backends (future enhancement)

---

## Phase 5: Integration (V2-P5)

### V2-P5-01: Mesh Service Facade
**Priority**: P0  
**Depends on**: V2-P2-03, V2-P3-02, T-SF02 (Service routing)  
**Status**: 📋 Planned

**Tasks**:
- [ ] V2-P5-01-001: Implement `VirtualSoulfindMeshService` class
- [ ] V2-P5-01-002: Expose `GetVirtualRelease` method
- [ ] V2-P5-01-003: Expose `ListMissingTracksForRelease` method
- [ ] V2-P5-01-004: Expose `GetPlanForRelease` method
- [ ] V2-P5-01-005: Expose `RegisterVerifiedCopy` method
- [ ] V2-P5-01-006: Expose `QuerySourceCandidates` method
- [ ] V2-P5-01-007: Add security checks (no PII exposure)
- [ ] V2-P5-01-008: Integrate with work budget (H-02)
- [ ] V2-P5-01-009: Add unit tests

**Deliverables**:
- VirtualSoulfind mesh service
- Secure, privacy-preserving API
- Work budget integration

---

### V2-P5-02: HTTP Gateway Endpoints
**Priority**: P0  
**Depends on**: V2-P2-03, V2-P3-02, T-SF04 (HTTP gateway), H-01 (Gateway auth)  
**Status**: 📋 Planned

**Tasks**:
- [ ] V2-P5-02-001: Create `VirtualSoulfindGatewayController` class
- [ ] V2-P5-02-002: Add `GET /virtual/releases/{releaseId}` endpoint
- [ ] V2-P5-02-003: Add `GET /virtual/releases/{releaseId}/missing` endpoint
- [ ] V2-P5-02-004: Add `POST /virtual/intents/releases/{releaseId}` endpoint
- [ ] V2-P5-02-005: Add `GET /virtual/intents/releases` endpoint
- [ ] V2-P5-02-006: Add `POST /virtual/plan/releases/{releaseId}/execute` endpoint
- [ ] V2-P5-02-007: Add `GET /virtual/artists` search endpoint
- [ ] V2-P5-02-008: Add `GET /virtual/library/reconcile` endpoint
- [ ] V2-P5-02-009: Integrate with gateway auth (H-01)
- [ ] V2-P5-02-010: Add to gateway allowlist config
- [ ] V2-P5-02-011: Add unit tests and integration tests

**Deliverables**:
- HTTP gateway endpoints for VirtualSoulfind
- Full CRUD for intents and catalogue
- Auth integration

---

### V2-P5-03: Work Budget Integration
**Priority**: P0  
**Depends on**: V2-P2-03, H-02 (Work budget)  
**Status**: 📋 Planned

**Tasks**:
- [ ] V2-P5-03-001: Integrate planner with work budget system
- [ ] V2-P5-03-002: Add work unit tracking per backend operation
- [ ] V2-P5-03-003: Implement budget checks before backend calls
- [ ] V2-P5-03-004: Add budget exhaustion handling
- [ ] V2-P5-03-005: Add metrics for budget usage
- [ ] V2-P5-03-006: Add unit tests

**Deliverables**:
- Work budget integration
- Budget tracking and enforcement
- Metrics

---

## Phase 6: Advanced Features (V2-P6)

### V2-P6-01: Library Reconciliation
**Priority**: P1  
**Depends on**: V2-P3-01, V2-P4-03  
**Status**: 📋 Planned (Future)

**Tasks**:
- [ ] V2-P6-01-001: Implement library scanner
- [ ] V2-P6-01-002: Implement track → LocalFile matching
- [ ] V2-P6-01-003: Implement gap detection (missing tracks)
- [ ] V2-P6-01-004: Implement duplicate detection
- [ ] V2-P6-01-005: Add quality assessment
- [ ] V2-P6-01-006: Add upgrade recommendations
- [ ] V2-P6-01-007: Add unit tests

**Deliverables**:
- Library reconciliation engine
- Gap and duplicate detection
- Quality assessment

---

### V2-P6-02: Smart Prioritization
**Priority**: P2  
**Depends on**: V2-P2-03, V2-P6-01  
**Status**: 📋 Planned (Future)

**Tasks**:
- [ ] V2-P6-02-001: Implement priority scoring algorithm
- [ ] V2-P6-02-002: Add "value per GB" calculation
- [ ] V2-P6-02-003: Integrate with external data (ListenBrainz, Last.fm, optional)
- [ ] V2-P6-02-004: Add "what most improves library" advisor
- [ ] V2-P6-02-005: Add unit tests

**Deliverables**:
- Smart prioritization algorithm
- Value-based recommendations

---

### V2-P6-03: Audio Fingerprinting (Optional)
**Priority**: P2  
**Depends on**: V2-P3-01, V2-P3-02  
**Status**: 📋 Planned (Future)

**Tasks**:
- [ ] V2-P6-03-001: Evaluate fingerprint schemes (AcoustID, Chromaprint)
- [ ] V2-P6-03-002: Integrate fingerprint generation
- [ ] V2-P6-03-003: Add fingerprint-based matching
- [ ] V2-P6-03-004: Add fingerprint storage and indexing
- [ ] V2-P6-03-005: Add unit tests

**Deliverables**:
- Audio fingerprinting support (future enhancement)

---

## Dependencies Summary

**Critical Path**:
1. H-08 (Soulseek caps) MUST be complete before V2-P4-01
2. H-02 (Work budget) MUST be complete before V2-P5-03
3. T-SF05-07 SHOULD be complete before starting V2-P1
4. **H-VS01 through H-VS12** (VirtualSoulfind-specific hardening) integrated throughout phases

**External Dependencies**:
- Service fabric (T-SF01-04) ✅ Complete
- Gateway auth (H-01) ✅ Complete
- Soulseek caps (H-08) ⏳ Pending - BLOCKS V2-P4-01
- Work budget (H-02) ⏳ Pending - BLOCKS V2-P5-03

**VirtualSoulfind-Specific Hardening Dependencies** (see design doc section 17):
- H-VS01 (Privacy mode) - Required for V2-P1
- H-VS02 (Intent queue security) - Required for V2-P2
- H-VS03 (Backend work budget) - Required for V2-P4
- H-VS04 (SSRF protection) - Required for V2-P4-05 (HTTP/LAN backends)
- H-VS05 (Resolver throughput limits) - Required for V2-P5
- H-VS06 (Plan validation) - Required for V2-P2-03
- H-VS07 (Verification safety) - Required for V2-P3-02
- H-VS08 (Mesh service restrictions) - Required for V2-P5-01
- H-VS09 (Logging hygiene) - Required for V2 deployment
- H-VS10 (Gateway endpoint protection) - Required for V2-P5-02
- H-VS11 (Verified copy hints) - Optional for V2-P6
- H-VS12 (Data directory permissions) - Required for V2 deployment

---

## Implementation Order

**Recommended sequence**:
1. Complete T-SF05 (Security review)
2. Complete T-SF06 (Developer docs)
3. Complete T-SF07 (Metrics/observability)
4. **Complete H-08 (Soulseek caps)** - Critical for VirtualSoulfind v2
5. **Complete H-02 (Work budget)** - Critical for VirtualSoulfind v2
6. Start V2-P1 (Foundation)
7. Continue with V2-P2, V2-P3, V2-P4, V2-P5 in sequence
8. V2-P6 (Advanced features) as time permits

---

## VirtualSoulfind-Specific Hardening Tasks (H-VS series)

These tasks are integrated into the phases above but listed here for completeness. See design doc section 17 for full details.

### H-VS01: Privacy Mode Implementation
**Phase**: V2-P1 (Foundation)  
**Priority**: P0  
**Status**: 📋 Planned

**Tasks**:
- [ ] Add `VirtualSoulfind.PrivacyMode` config enum (Normal, Reduced)
- [ ] Implement Reduced mode logic (no external peer identifiers)
- [ ] Abstract source candidates in Reduced mode
- [ ] Add config validation and warnings
- [ ] Add unit tests for both modes
- [ ] Document trade-offs in user guide

---

### H-VS02: Intent Queue Security
**Phase**: V2-P2 (Intent & Planning)  
**Priority**: P0  
**Status**: 📋 Planned

**Tasks**:
- [ ] Add `AllowRemoteIntentManagement` config (default: false)
- [ ] Implement per-peer/per-IP rate limits for intent creation
- [ ] Add intent origin tracking (UserLocal, RemoteMesh, RemoteGateway)
- [ ] Integrate with H-01 gateway auth for HTTP endpoints
- [ ] Add unit tests for rate limiting
- [ ] Add integration tests for auth enforcement

---

### H-VS03: Backend Work Budget Enforcement
**Phase**: V2-P4 (Backend Implementations)  
**Priority**: P0  
**Status**: 📋 Planned (DEPENDS ON H-02)

**Tasks**:
- [ ] Add work budget check to IContentBackend interface
- [ ] Implement budget consumption in all backend FindCandidatesAsync methods
- [ ] Add budget exhaustion handling (fail fast, log, mark OnHold)
- [ ] Add work budget integration tests
- [ ] Verify budget enforcement for each backend type
- [ ] Add metrics for budget usage per backend

---

### H-VS04: SSRF Protection for HTTP/LAN Backends
**Phase**: V2-P4-05 (Optional backends)  
**Priority**: P0 (if HTTP/LAN backends implemented)  
**Status**: 📋 Planned (Future)

**Tasks**:
- [ ] Create outbound HTTP client wrapper with IP filtering
- [ ] Implement IP allowlist/denylist (block loopback, private subnets)
- [ ] Add URL validation (no arbitrary user-supplied URLs)
- [ ] Make SSRF protection enabled by default
- [ ] Add unit tests for IP filtering
- [ ] Add integration tests for SSRF prevention

---

### H-VS05: Resolver Throughput Limits
**Phase**: V2-P5 (Integration)  
**Priority**: P0  
**Status**: 📋 Planned

**Tasks**:
- [ ] Add `Resolver.MaxTracksPerRun` config (default: 10)
- [ ] Add `Resolver.MaxConcurrentPlans` config (default: 3)
- [ ] Implement per-origin quotas (prioritize UserLocal)
- [ ] Add throughput tracking and enforcement
- [ ] Log when limits are hit
- [ ] Add unit tests for limit enforcement

---

### H-VS06: Plan Validation and Cost Estimation
**Phase**: V2-P2-03 (Multi-Source Planner)  
**Priority**: P0  
**Status**: 📋 Planned

**Tasks**:
- [ ] Implement pre-execution cost estimation
- [ ] Add validation against per-call work budget
- [ ] Add validation against per-peer budget
- [ ] Add validation against Soulseek caps (H-08)
- [ ] Implement plan downgrade logic
- [ ] Add OnHold/Failed status with reasons
- [ ] Add unit tests for all validation scenarios

---

### H-VS07: Verification Safety Guards
**Phase**: V2-P3-02 (Verified Copy Registry)  
**Priority**: P1  
**Status**: 📋 Planned

**Tasks**:
- [ ] Make verification local-only by default
- [ ] Add opt-in config for external fingerprint services
- [ ] Implement duration tolerance validation
- [ ] Implement hash stability checks (multiple confirmations)
- [ ] Add advertising guard (only verified copies)
- [ ] Add configurable verification confidence thresholds
- [ ] Add unit tests for verification logic

---

### H-VS08: Mesh Service Method Restrictions
**Phase**: V2-P5-01 (Mesh Service Facade)  
**Priority**: P0  
**Status**: 📋 Planned

**Tasks**:
- [ ] Define read-only vs high-cost method categories
- [ ] Implement method-level access control
- [ ] Disable high-cost methods by default over mesh
- [ ] Add per-peer quotas for all methods
- [ ] Integrate with H-02 work budget
- [ ] Add unit tests for access control
- [ ] Add integration tests for mesh service security

---

### H-VS09: Logging and Metrics Hygiene
**Phase**: V2-P5 (Integration) / Deployment  
**Priority**: P1  
**Status**: 📋 Planned

**Tasks**:
- [ ] Implement path redaction (basename only)
- [ ] Implement peer identifier redaction
- [ ] Remove high-cardinality IDs from metrics
- [ ] Implement aggregated metrics per backend
- [ ] Make DEBUG/TRACE logs opt-in only
- [ ] Add structured logging with safe field selection
- [ ] Add tests verifying no PII in logs

---

### H-VS10: Gateway Endpoint Protection
**Phase**: V2-P5-02 (HTTP Gateway Endpoints)  
**Priority**: P0  
**Status**: 📋 Planned

**Tasks**:
- [ ] Integrate all endpoints with H-01 auth/CSRF
- [ ] Add API key requirement for mutating operations
- [ ] Add `AllowPlanExecution` config (default: false)
- [ ] Implement IP-based rate limiting
- [ ] Add endpoints to gateway AllowedServices config
- [ ] Add work budget check for execution endpoints
- [ ] Add integration tests for auth enforcement

---

### H-VS11: Verified Copy Hints Trust Model
**Phase**: V2-P6 (Advanced Features)  
**Priority**: P2 (Optional)  
**Status**: 📋 Planned (Future)

**Tasks**:
- [ ] Add `VerifiedCopyHints.Enabled` config (default: false)
- [ ] Add `VerifiedCopyHints.TrustPolicy` enum
- [ ] Implement trust-scoped query handling
- [ ] Implement privacy-preserving hint export (no paths, partial hashes only)
- [ ] Create dedicated mesh service for hints
- [ ] Add method-level limits to hints service
- [ ] Add unit tests for trust scoping

---

### H-VS12: Data Directory Permissions
**Phase**: V2-P1 (Foundation) / Deployment  
**Priority**: P1  
**Status**: 📋 Planned

**Tasks**:
- [ ] Define dedicated data directory structure
- [ ] Implement directory permission checks on startup
- [ ] Set restrictive permissions (0700) automatically where possible
- [ ] Integrate with H-09 dedicated user setup
- [ ] Add permission warnings to logs
- [ ] Document in deployment guide
- [ ] Add tests for permission validation

---

## Notes

- Each phase should be completed and tested before moving to the next
- Backwards compatibility must be maintained throughout
- All new code must integrate with existing security/privacy/limits infrastructure
- Testing strategy from `TESTING-STRATEGY.md` applies to all VirtualSoulfind v2 work
- Phase 6 (Advanced Features) is lower priority and can be deferred
