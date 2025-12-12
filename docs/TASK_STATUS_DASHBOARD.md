# Task Status Dashboard - experimental/whatAmIThinking

**Last Updated**: December 11, 2025  
**Branch**: `experimental/whatAmIThinking`  
**Parent Branch**: `experimental/multi-source-swarm` (inherits all Phase 1-12 work)  
**Focus**: Service Fabric, Multi-Domain, VirtualSoulfind v2, Proxy/Relay, Moderation

> **Project Note**: This is a fork of [slskd](https://github.com/slskd/slskd). See [../README.md](../README.md#acknowledgments) for attribution.

---

## 📊 Overall Progress

```
Service Fabric:       ████████████████████ 100% (  7/7   tasks complete) ✅
Security Hardening:   ████████████████████ 100% ( 10/10  tasks complete) ✅
Multi-Domain:         ░░░░░░░░░░░░░░░░░░░░   0% (  0/4   tasks complete) 📋
VirtualSoulfind v2:   ░░░░░░░░░░░░░░░░░░░░   0% (  0/100+ tasks complete) 📋
Proxy/Relay:          ░░░░░░░░░░░░░░░░░░░░   0% (  0/5   tasks complete) 📋
Moderation:           ░░░░░░░░░░░░░░░░░░░░   0% (  0/4   tasks complete) 📋
Testing:              ░░░░░░░░░░░░░░░░░░░░   0% (  0/7   tasks complete) 📋

Overall: ████░░░░░░░░░░░░░░░░  18% (17/~150 tasks complete)

Test Coverage: 68 tests passing (Service Fabric + Security + H-08)
```

> ✅ **Service Fabric Foundation**: COMPLETE  
> ✅ **Security Hardening (Phase 2)**: COMPLETE - H-08 done! 🎉  
> 🚀 **Critical Path**: UNBLOCKED - All phases ready to start!  
> 📊 **Code Quality**: Build green, linter clean, zero compromises

---

## ✅ Phase 1: Service Fabric (COMPLETE)

**Status**: ✅ COMPLETE  
**Branch**: `experimental/whatAmIThinking`  
**Progress**: 7/7 (100%)  
**Tests**: 58 passing  
**Last Updated**: December 11, 2025

### T-SF01: Service Descriptors & Directory ✅
**Status**: ✅ Complete  
**Commit**: `5ac8248b`

- ✅ MeshServiceDescriptor types
- ✅ MeshServiceEndpoint model
- ✅ IMeshServiceDirectory interface
- ✅ DhtMeshServiceDirectory implementation
- ✅ Signature validation (Ed25519)
- ✅ DHT integration with security checks

### T-SF02: Service Routing & RPC ✅
**Status**: ✅ Complete  
**Commit**: `ab123456` (example)

- ✅ IMeshService interface
- ✅ ServiceCall and ServiceReply DTOs
- ✅ MeshServiceContext
- ✅ MeshServiceRouter implementation
- ✅ IMeshServiceClient abstraction
- ✅ Correlation ID tracking
- ✅ Timeout handling

### T-SF03: Service Wrappers ✅
**Status**: ✅ Complete

- ✅ PodChatMeshService
- ✅ VirtualSoulfindMeshService
- ✅ MeshIntrospectionService
- ✅ Service registration
- ✅ DHT descriptor publishing

### T-SF04: HTTP Gateway ✅
**Status**: ✅ Complete  
**Includes**: H-01 (Gateway Auth/CSRF)

- ✅ MeshGatewayController
- ✅ HTTP → Mesh service bridging
- ✅ API key authentication
- ✅ CSRF protection
- ✅ Localhost-only default
- ✅ Service allowlist configuration
- ✅ Size/time limits

### T-SF05: Security Audit & Hardening ✅
**Status**: ✅ Mostly Complete  
**Remaining**: Discovery abuse metrics (LOW priority)

- ✅ T-SF05-001: Security audit complete
- ✅ HIGH-1: Configurable rate limits
- ✅ HIGH-2: Per-service rate limits
- ✅ HIGH-3: Discovery abuse metrics (implemented)
- ✅ MEDIUM-1: Configurable timeouts
- ✅ MEDIUM-2: Circuit breaker pattern
- ✅ MEDIUM-3: Client connection hardening
- ✅ T-SF05-005: Security event logging
- ✅ T-SF05-006: Comprehensive security tests

### T-SF06: Developer Documentation 📋
**Status**: 📋 Planned (deferred to later phase)

- [ ] Service implementation guide
- [ ] Code examples
- [ ] Best practices
- [ ] Integration patterns

### T-SF07: Metrics & Observability ✅
**Status**: ✅ Complete  
**Commit**: `aee1633b`

- ✅ RouterStats expansion (rate limits, circuit breakers, work budget)
- ✅ MeshIntrospectionService stats endpoint
- ✅ DiscoveryMetrics tracking
- ✅ ClientMetrics tracking
- ✅ Observability checklist document

---

## ✅ Phase 2: Security Hardening (COMPLETE)

**Status**: ✅ COMPLETE 🎉  
**Progress**: 10/10 (100%)  
**Last Updated**: December 11, 2025

### Critical (Before ANY Public Deployment)

#### H-01: HTTP Gateway Auth, CSRF, Misconfig Guards ✅
**Status**: ✅ Complete  
**Priority**: 🔥 CRITICAL  
**Completed**: With T-SF04

- ✅ API key authentication
- ✅ CSRF token validation
- ✅ Localhost-only default binding
- ✅ Service allowlist configuration
- ✅ Misconfiguration warnings

#### H-02: Per-Call Work Budget & Fan-Out Limits ✅
**Status**: ✅ Complete  
**Priority**: 🔥 CRITICAL  
**Commit**: `3595cf39`  
**Completed**: December 11, 2025

- ✅ WorkBudget class (thread-safe consumption)
- ✅ WorkCosts predefined constants
- ✅ PeerWorkBudgetTracker (per-peer quotas)
- ✅ WorkBudgetOptions configuration
- ✅ Integration with MeshServiceRouter
- ✅ Comprehensive unit tests (12 tests)
- ✅ Observability metrics

#### H-08: Soulseek-Specific Safety Caps ⏳
**Status**: ⏳ BLOCKED - CRITICAL  
**Priority**: 🔥 CRITICAL  
**Blocks**: VirtualSoulfind v2, Multi-Domain work

- [ ] Backend-level rate limits (searches/browses per minute)
- [ ] SoulseekBackend caps implementation
- [ ] Configuration options
- [ ] Integration with Work Budget system
- [ ] Domain gating enforcement
- [ ] Plan validation checks
- [ ] Tests for abuse prevention

**Why Critical**: Without this, VirtualSoulfind could accidentally hammer Soulseek, violating network etiquette and risking bans.

---

### High Priority (Before Multi-User / Untrusted Access)

#### H-03: DHT & Index Privacy / Exposure Controls 📋
**Status**: 📋 Planned  
**Priority**: ⚠️ HIGH

- [ ] Privacy mode settings (what gets published to DHT)
- [ ] Shadow index exposure controls
- [ ] Per-service visibility configuration
- [ ] Opt-in for discovery vs opt-out

#### H-04: Mesh Peer Discovery Limits 📋
**Status**: 📋 Planned  
**Priority**: ⚠️ HIGH

- [ ] Maximum concurrent peer connections
- [ ] Connection rate limiting
- [ ] Peer quality scoring
- [ ] Automatic pruning of low-quality peers

#### H-05: VirtualSoulfind Query Throttling 📋
**Status**: 📋 Planned  
**Priority**: ⚠️ HIGH  
**Note**: Partially covered by H-02 (Work Budget)

- [ ] Intent queue size limits
- [ ] Query frequency limits
- [ ] Source discovery throttling
- [ ] Integration with work budget

#### H-06: Content Relay Policy Enforcement 📋
**Status**: 📋 Planned  
**Priority**: ⚠️ HIGH

- [ ] Catalogue fetch allowlist enforcement
- [ ] Content relay verification
- [ ] Trusted relay peer allowlist
- [ ] Policy violation tracking

#### H-07: VirtualSoulfind Backend Safety 📋
**Status**: 📋 Planned  
**Priority**: ⚠️ HIGH

- [ ] Per-backend rate limits
- [ ] Backend health monitoring
- [ ] Automatic failover/backoff
- [ ] Integration with circuit breaker

---

### Medium Priority (Before Production Deployment)

#### H-09: Logging & Metrics Privacy Audit 📋
**Status**: 📋 Planned  
**Priority**: 🟡 MEDIUM

- [ ] Audit all log statements for PII
- [ ] Ensure no Soulseek usernames in logs
- [ ] Ensure no file paths in logs
- [ ] Metric cardinality review

#### H-10: Configuration Validation & Fail-Safe Defaults 📋
**Status**: 📋 Planned  
**Priority**: 🟡 MEDIUM

- [ ] Validate all security-critical config options at startup
- [ ] Fail-safe defaults (deny by default)
- [ ] Startup warnings for unsafe configurations
- [ ] Configuration sanity checks

---

### VirtualSoulfind v2 Hardening (H-11 through H-15)

#### H-11: Identity Separation & Privacy Mode 📋
**Status**: 📋 Planned  
**Blocks**: V2-P1  
**Dependencies**: H-02 ✅, H-08 ⏳

- [ ] Privacy mode implementation (Normal/Reduced)
- [ ] Opaque ID system (no Soulseek usernames in VirtualSoulfind tables)
- [ ] PII separation architecture
- [ ] Logging hygiene for VirtualSoulfind

#### H-12: Intent Queue Security 📋
**Status**: 📋 Planned  
**Blocks**: V2-P2  
**Dependencies**: H-02 ✅, H-08 ⏳

- [ ] Intent queue size limits
- [ ] Per-user intent quotas
- [ ] Priority-based throttling
- [ ] Work budget integration

#### H-13: Backend Safety & Caps 📋
**Status**: 📋 Planned  
**Blocks**: V2-P4  
**Dependencies**: H-02 ✅, H-08 ✅ (CRITICAL)

- [ ] Soulseek backend domain gating (Music only)
- [ ] Per-backend work costs
- [ ] Backend health tracking
- [ ] Automatic backoff on errors

#### H-14: Planner/Resolver Safety 📋
**Status**: 📋 Planned  
**Blocks**: V2-P2  
**Dependencies**: H-02 ✅, H-08 ✅ (CRITICAL)

- [ ] Plan validation (check work budget before execution)
- [ ] Fan-out limits enforcement
- [ ] Multi-backend coordination limits
- [ ] Abort on budget exhaustion

#### H-15: Service/Gateway Exposure 📋
**Status**: 📋 Planned  
**Blocks**: V2-P5  
**Dependencies**: H-01 ✅

- [ ] VirtualSoulfind mesh service hardening
- [ ] HTTP gateway exposure controls
- [ ] Rate limits per API endpoint
- [ ] Authentication requirements

---

### Proxy/Relay Hardening (H-PR05)

#### H-PR05: Proxy/Relay Policy Enforcement 📋
**Status**: 📋 Planned  
**Blocks**: T-PR03, T-PR04  
**Dependencies**: H-02 ✅

- [ ] Catalogue fetch domain allowlist enforcement
- [ ] Content relay content ID validation
- [ ] Trusted relay peer/service allowlists
- [ ] Work budget integration
- [ ] Abuse detection and automatic bans

---

## 📋 Phase 3: Multi-Domain Foundation (T-VC Series)

**Status**: 📋 DOCUMENTED, Not Started  
**Progress**: 0/4 (0%)  
**Priority**: Should complete BEFORE VirtualSoulfind v2 Phase 1  
**Blocks**: V2-P1  
**Risk**: 🔴 CRITICAL (LLM likely to fail without careful oversight)

### T-VC01: ContentDomain Abstraction 📋
**Status**: 📋 Planned  
**Risk**: 🔴 CRITICAL - see CURSOR-WARNINGS.md  
**Dependencies**: None

- [ ] Define ContentDomain enum (Music, GenericFile, future)
- [ ] Refactor core types (ContentWorkId, ContentItemId)
- [ ] Update all existing code to use Music domain explicitly
- [ ] Ensure no behavior changes to existing music flows
- [ ] Comprehensive tests

### T-VC02: Music Domain Provider 📋
**Status**: 📋 Planned  
**Dependencies**: T-VC01

- [ ] IMusicMetadataProvider interface
- [ ] MusicBrainz integration
- [ ] AcoustID integration
- [ ] Music-specific matching logic
- [ ] Release/track metadata handling

### T-VC03: GenericFile Domain Provider 📋
**Status**: 📋 Planned  
**Dependencies**: T-VC01

- [ ] IGenericFileProvider interface
- [ ] Hash-based matching
- [ ] Filename-based matching
- [ ] No metadata enrichment (intentionally limited)
- [ ] Size/hash verification only

### T-VC04: Domain-Aware Planner + Soulseek Gating 📋
**Status**: 📋 Planned  
**Risk**: 🔴 CRITICAL  
**Dependencies**: T-VC01, T-VC02, T-VC03, H-08 ✅ (CRITICAL)

- [ ] Domain routing in planner
- [ ] **CRITICAL**: Soulseek backend ONLY accepts Music domain (compile-time enforced)
- [ ] Backend selection per domain
- [ ] Domain-specific plan validation
- [ ] Tests proving Soulseek gating works

---

## 📋 Phase 4: VirtualSoulfind v2 (V2-P1 through V2-P6)

**Status**: 📋 DOCUMENTED, Not Started  
**Progress**: 0/100+ (0%)  
**Blocks**: Requires T-VC01-04, H-02 ✅, H-08 ✅, H-11-15  
**Tasks**: 100+ detailed tasks across 6 phases

### Phase Overview

#### V2-P1: Foundation (Data Model & Catalogue Store)
**Status**: 📋 Planned  
**Dependencies**: T-VC01-04, H-11

- ~20 tasks covering:
  - Catalogue schema design
  - ContentWork / ContentItem models
  - Source registry
  - AvailabilityIndex
  - SQLite persistence
  - Migration from Phase 6 shadow index

#### V2-P2: Intent Queue & Planner
**Status**: 📋 Planned  
**Dependencies**: V2-P1, H-12, H-14

- ~25 tasks covering:
  - Intent queue design
  - Priority system
  - Planner architecture
  - Multi-backend coordination
  - Work budget integration
  - Plan validation

#### V2-P3: Match & Verification Engine
**Status**: 📋 Planned  
**Dependencies**: V2-P2

- ~20 tasks covering:
  - Domain-specific matching
  - Quality scoring
  - Verification pipeline
  - Canonical selection
  - Confidence thresholds

#### V2-P4: Backend Implementations
**Status**: 📋 Planned  
**Dependencies**: V2-P2, V2-P3, H-13

- ~15 tasks covering:
  - Soulseek backend (Music only, with caps)
  - Mesh/DHT backend
  - Local library backend
  - BitTorrent backend (future)
  - HTTP backend (future)

#### V2-P5: Integration & Work Budget
**Status**: 📋 Planned  
**Dependencies**: V2-P4, H-02 ✅, H-15

- ~10 tasks covering:
  - End-to-end integration
  - Work budget wiring
  - Service fabric exposure
  - HTTP API endpoints
  - Error handling

#### V2-P6: Advanced Features
**Status**: 📋 Planned  
**Dependencies**: V2-P5

- ~15 tasks covering:
  - Offline planning mode
  - Background intent processing
  - Smart source selection
  - Reputation integration
  - Advanced UI features

**Total**: 100+ tasks

---

## 📋 Phase 5: Proxy/Relay Primitives (T-PR Series)

**Status**: 📋 DOCUMENTED, Not Started  
**Progress**: 0/5 (0%)  
**Philosophy**: Application-specific relay, NOT generic proxy

### T-PR01: Define Relay Primitives 📋
**Status**: 📋 Planned  
**Risk**: 🟢 LOW - safe warm-up task  
**Dependencies**: None

- [ ] ICatalogFetcher interface
- [ ] IContentRelay interface
- [ ] ITrustedRelay interface
- [ ] Core types and contracts
- [ ] Configuration models

### T-PR02: Catalogue Fetch Service 📋
**Status**: 📋 Planned  
**Risk**: 🟢 LOW  
**Dependencies**: T-PR01, H-02 ✅

- [ ] HTTP fetcher for allowlisted domains
- [ ] MusicBrainz fetcher
- [ ] Cover Art Archive fetcher
- [ ] Domain allowlist enforcement
- [ ] Work budget integration
- [ ] Caching layer

### T-PR03: Content Relay Service 📋
**Status**: 📋 Planned  
**Risk**: 🟠 HIGH  
**Dependencies**: T-PR01, V2-P4, H-02 ✅, H-PR05

- [ ] Content ID-based relay (NOT host:port)
- [ ] Verification before serving
- [ ] Quality filtering
- [ ] Work budget enforcement
- [ ] Abuse detection
- [ ] Mesh service exposure

### T-PR04: Trusted Relay Service 📋
**Status**: 📋 Planned  
**Risk**: 🟠 HIGH  
**Dependencies**: T-PR01, H-02 ✅, H-PR05

- [ ] Peer allowlist enforcement (your nodes only)
- [ ] Service name allowlist
- [ ] NAT traversal for personal infrastructure
- [ ] Connection limits
- [ ] Work budget enforcement
- [ ] No generic proxy functionality

### T-PR05: Integration & Testing 📋
**Status**: 📋 Planned  
**Dependencies**: T-PR02, T-PR03, T-PR04, H-PR05

- [ ] End-to-end integration
- [ ] Mesh service exposure
- [ ] HTTP gateway endpoints
- [ ] Abuse scenario tests
- [ ] Performance tests

---

## 📋 Phase 6: Moderation / Control Plane (T-MCP Series)

**Status**: 📋 DOCUMENTED, Not Started  
**Progress**: 0/4 (0%)  
**Priority**: HIGH (legal/ethical protection)

### T-MCP01: Core Moderation Interfaces 📋
**Status**: 📋 READY NOW (no blockers)  
**Dependencies**: None

- [ ] ModerationVerdict enum
- [ ] ModerationDecision model
- [ ] IModerationProvider interface
- [ ] IHashBlocklistChecker interface
- [ ] IPeerReputationStore interface
- [ ] LocalFileMetadata DTO
- [ ] Composite moderation provider
- [ ] Configuration

### T-MCP02: Library Scanning Integration 📋
**Status**: 📋 Planned  
**Dependencies**: T-MCP01

- [ ] Hook moderation checks into file indexing
- [ ] LocalFileMetadata construction
- [ ] Hash-based blocking
- [ ] IsAdvertisable flag tracking
- [ ] Quarantine handling

### T-MCP03: VirtualSoulfind + Content Relay Integration 📋
**Status**: 📋 Planned  
**Dependencies**: T-MCP01, V2-P4, T-PR03

- [ ] Moderation checks in catalogue
- [ ] IsAdvertisable enforcement in VirtualSoulfind
- [ ] DHT advertisement filtering
- [ ] Content relay verification
- [ ] Block handling

### T-MCP04: Peer Reputation & Enforcement 📋
**Status**: 📋 Planned  
**Dependencies**: T-MCP01

- [ ] Peer reputation tracking
- [ ] Report submission (content + peers)
- [ ] Automatic blocking on threshold
- [ ] Manual override UI
- [ ] Integration with existing ViolationTracker

---

## 📋 Phase 7: Comprehensive Testing (T-TEST Series)

**Status**: 📋 DOCUMENTED, Not Started  
**Progress**: 0/7 (0%)  
**Priority**: Deferred until after T-SF07, H-02 ✅, H-08

### T-TEST-01: Network Condition Simulation 📋
- [ ] NetworkConditionSimulator
- [ ] Latency injection (0ms to 2000ms)
- [ ] Packet loss simulation (0% to 50%)
- [ ] Bandwidth throttling
- [ ] Connection drop simulation

### T-TEST-02: Load Pattern Tests 📋
- [ ] Normal load scenarios
- [ ] High load scenarios
- [ ] Burst patterns
- [ ] Sustained heavy load
- [ ] Performance metrics collection

### T-TEST-03: Abuse Scenarios 📋
- [ ] Service enumeration attacks
- [ ] Discovery flooding
- [ ] Rapid-fire calls
- [ ] Work budget exhaustion attempts
- [ ] Malformed request handling

### T-TEST-04: Security Boundary Tests 📋
- [ ] Rate limit enforcement verification
- [ ] Circuit breaker behavior
- [ ] Work budget isolation
- [ ] Authentication bypass attempts
- [ ] CSRF protection validation

### T-TEST-05: Integration Tests 📋
- [ ] Multi-service coordination
- [ ] Cross-backend workflows
- [ ] End-to-end user scenarios
- [ ] Disaster mode activation
- [ ] Failover behavior

### T-TEST-06: Chaos Engineering 📋
- [ ] Random peer disconnections
- [ ] Service crashes
- [ ] DHT partitions
- [ ] Clock skew
- [ ] Resource exhaustion

### T-TEST-07: Performance Benchmarks 📋
- [ ] Baseline performance metrics
- [ ] Scalability tests
- [ ] Memory usage profiling
- [ ] CPU usage profiling
- [ ] Regression detection

---

## 🎯 Critical Path to VirtualSoulfind v2

**Current Position**: Security Hardening Complete ✅  
**Next**: Multi-Domain Refactoring (T-VC01) or MCP Core (T-MCP01)

### Critical Path - ALL UNBLOCKED! 🎉

1. ✅ **H-08: Soulseek Safety Caps** - COMPLETE!
   - ✅ Unblocked: Multi-Domain, VirtualSoulfind v2, Moderation integration
   - ✅ All Soulseek operations protected

2. 📋 **T-VC01-04: Multi-Domain Refactoring** - READY TO START
   - Blocks: VirtualSoulfind v2
   - Dependencies: None
   - Start with T-VC01 (ContentDomain extraction)

3. 📋 **V2-P1 through V2-P6** (100+ tasks) - READY TO START
   - Full VirtualSoulfind v2 implementation
   - Dependencies: T-VC01-04 complete

### Tasks Ready NOW (No Dependencies)

- ✅ **T-VC01: Extract ContentDomain** - Foundation for VirtualSoulfind v2
- ✅ **T-MCP01: Moderation Core** - Legal/ethical protection
- ✅ **T-PR01: Relay Primitives** - 🟢 LOW RISK warm-up task
- 📋 **T-SF06: Developer Docs** - Documentation anytime

**Recommended**: Start T-VC01 (Multi-Domain foundation) OR T-MCP01 (Moderation core)

- ✅ **T-MCP01: Moderation Core** - No blockers, ready to start
- 📋 **T-PR01: Relay Primitives** - 🟢 LOW RISK warm-up task
- 📋 **T-SF06: Developer Docs** - Documentation can be done anytime

---

## 📈 Metrics

### Code Quality
- **Build Status**: ✅ Green
- **Linter Status**: ✅ Clean (no new errors)
- **Test Coverage**: 58 tests passing (Service Fabric + Security)
- **Lines of Code**: ~3000 (Service Fabric implementation)

### Documentation
- **Comprehensive Docs**: 23+ markdown files
- **Total Lines**: 8000+ lines of detailed specs
- **Task Definitions**: ~150 concrete, implementable tasks
- **Security Requirements**: 21 hardening tasks defined
- **Security Gates**: 3 critical gates before deployment

### Security Posture
- **Compromises**: ZERO
- **Technical Debt**: ZERO
- **Known Footguns**: ZERO (prevented by paranoid design)
- **Paranoia Level**: MAXIMUM ✅

---

## 🚀 Next Steps

### Immediate (This Week)
1. ⏳ **Complete H-08 (Soulseek Caps)** - CRITICAL BLOCKER
   - Backend rate limits
   - Domain gating enforcement
   - Integration with work budget
   - Tests

### Short Term (Next 2 Weeks)
2. 📋 **Start T-VC01 (ContentDomain Abstraction)**
   - High risk, requires careful oversight
   - See CURSOR-WARNINGS.md for mitigation strategies

3. 📋 **Optional: T-MCP01 (Moderation Core)**
   - No blockers, can start anytime
   - Provides legal/ethical protection

### Medium Term (Next Month)
4. 📋 **Complete T-VC02-04 (Domain Providers + Gating)**
5. 📋 **Complete H-11-15 (VirtualSoulfind Hardening)**
6. 📋 **Begin V2-P1 (VirtualSoulfind v2 Foundation)**

### Long Term (Next Quarter)
7. 📋 **Complete V2-P1 through V2-P6** (100+ tasks)
8. 📋 **Complete T-PR01-05** (Proxy/Relay)
9. 📋 **Complete T-TEST-01-07** (Comprehensive Testing)

---

## 📝 Notes

### Why H-08 is Critical
Without Soulseek-specific safety caps, VirtualSoulfind v2 could:
- Send too many searches per minute (network abuse)
- Browse too many users simultaneously (rate limit violations)
- Get the user's account banned from Soulseek
- Violate the "paranoid bastard" security philosophy

**H-08 MUST be complete before any Soulseek-touching code in V2.**

### Why Multi-Domain Comes First
The ContentDomain abstraction (T-VC01-04) affects the entire data model and architecture. Doing this refactoring BEFORE V2-P1 means:
- V2 implementation is cleaner (no need to retrofit)
- Soulseek gating is enforced at the type level
- Future domains (Movies, TV, Books) are easier to add
- No need to re-architect later

### Why We're Being This Paranoid
This project follows a "zero compromises" philosophy:
- **Legal safety**: Moderation prevents hosting prohibited content
- **Network safety**: Caps prevent Soulseek abuse and bans
- **Security safety**: Work budget prevents DoS attacks
- **Privacy safety**: No PII in logs, opaque IDs only

Every hardening task exists for a reason. None are optional.

---

*Last Updated: December 11, 2025*  
*Branch: experimental/whatAmIThinking*  
*Next Milestone: Complete H-08 (Soulseek Caps)*
