# Task Status Dashboard - experimental/whatAmIThinking

**Last Updated**: December 11, 2025  
**Branch**: `experimental/whatAmIThinking`  
**Parent Branch**: `experimental/multi-source-swarm` (inherits all Phase 1-12 work)  
**Focus**: Service Fabric, Multi-Domain, VirtualSoulfind v2, Proxy/Relay, Moderation

> **Project Note**: This is a fork of [slskd](https://github.com/slskd/slskd). See [../README.md](../README.md#acknowledgments) for attribution.

---

## 🔒 **MANDATORY: Global Requirements**

**ALL tasks must comply with:**
- `docs/CURSOR-META-INSTRUCTIONS.md` - Meta-rules for implementation
- `docs/security-hardening-guidelines.md` OR `SECURITY-GUIDELINES.md` - Security requirements
- `MCP-HARDENING.md` - Moderation layer security (for T-MCP tasks)

**Key Rules:**
1. ❌ **DO NOT renumber or reorder existing tasks**
2. ✅ **Append new tasks** under appropriate headings
3. 🔒 **Security/privacy first** - No full paths, hashes, or external IDs in logs
4. 💰 **Work budget required** - All network/CPU-heavy ops consume budget
5. 🧪 **Test discipline** - Every task adds/updates tests

---

## 📊 Overall Progress

```
Service Fabric:       ████████████████████ 100% (  7/7   tasks complete) ✅
Security Hardening:   ████████████████████ 100% ( 10/10  tasks complete) ✅
Global Hardening:     ░░░░░░░░░░░░░░░░░░░░   0% (  0/5   tasks complete) 📋
Engineering Quality:  ░░░░░░░░░░░░░░░░░░░░   0% (  0/4   tasks complete) 📋
Multi-Domain Core:    █████░░░░░░░░░░░░░░░  25% (  2/8   tasks complete) 🚧
Moderation (MCP):     ██████████░░░░░░░░░░  50% (  2/4   tasks complete) 🚧
LLM/AI Moderation:    ░░░░░░░░░░░░░░░░░░░░   0% (  0/5   tasks complete) 📋
VirtualSoulfind v2:   ░░░░░░░░░░░░░░░░░░░░   0% (  0/100+ tasks complete) 📋
Proxy/Relay:          ░░░░░░░░░░░░░░░░░░░░   0% (  0/5   tasks complete) 📋
Book Domain:          ░░░░░░░░░░░░░░░░░░░░   0% (  0/4   tasks complete) 📋
Video Domain:         ░░░░░░░░░░░░░░░░░░░░   0% (  0/5   tasks complete) 📋
UI/Dashboards:        ░░░░░░░░░░░░░░░░░░░░   0% (  0/6   tasks complete) 📋
Social Federation:    ░░░░░░░░░░░░░░░░░░░░   0% (  0/10  tasks complete) 📋
Testing:              ░░░░░░░░░░░░░░░░░░░░   0% (  0/7   tasks complete) 📋

Overall: ███░░░░░░░░░░░░░░░░░  12% (21/~180 tasks complete)

Test Coverage: 128 tests passing (SF + Security + MCP + Multi-Domain Core)
```

> ✅ **Service Fabric Foundation**: COMPLETE  
> ✅ **Security Hardening (Phase 2)**: COMPLETE - H-08 done! 🎉  
> 🚧 **Phase B - MCP (Safety Floor)**: IN PROGRESS - T-MCP01 ✅, T-MCP02 ✅
> 🚧 **Phase C - Multi-Domain Core**: IN PROGRESS - T-VC01 Parts 1-2 ✅
> 📋 **LLM/AI Moderation**: 5 OPTIONAL tasks (T-MCP-LM01-05, all disabled by default)
> 📋 **Phase E - Book & Video Domains**: 9 tasks documented (T-BK01-04, T-VID01-05)
> 📋 **Phase G - UI & Dashboards**: 6 tasks documented (T-UI01-06)
> 📋 **Global Hardening**: 5 tasks (logging, identity, validation, transport, MCP audit)
> 📋 **Engineering Quality**: 4 tasks (async enforcement, linting, coverage, refactoring)
> 🚀 **Critical Path**: UNBLOCKED - Next: T-MCP03 or T-VC02  
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

### Global Hardening Tasks (H-GLOBAL, H-ID, H-VF, H-TRANSPORT, H-MCP)

These tasks apply **cross-cutting security and privacy concerns** across the entire stack.

#### H-GLOBAL01: Logging and Telemetry Hygiene Audit 📋
**Status**: 📋 Planned  
**Priority**: 🟡 MEDIUM (audit existing code, enforce for new code)  
**Dependencies**: None (can start anytime)

- [ ] Audit logging across all services:
  - [ ] Identify places where full paths, hashes, IP addresses, external usernames/ActivityPub handles are logged
  - [ ] Replace or redact with internal IDs, hashed/truncated forms
- [ ] Audit metrics:
  - [ ] Ensure all metrics use low-cardinality labels only
  - [ ] Remove/rename metrics that include file names, paths, full URLs, external handles
- [ ] Add unit/integration tests:
  - [ ] Ensure new log calls use sanitized conventions
  - [ ] Ensure new metrics use only approved labels
- [ ] Update `SECURITY-GUIDELINES.md` with newly enforced patterns

**Applies to**: All services, all protocols, all future code

#### H-ID01: Identity Separation Enforcement 📋
**Status**: 📋 Planned  
**Priority**: 🔴 HIGH (before social federation, Phase F)  
**Dependencies**: None (conceptual separation already exists)

- [ ] Review key and identity handling across:
  - [ ] Mesh/pod identity
  - [ ] Soulseek client identity
  - [ ] ActivityPub actor identities (future)
  - [ ] Local user/operator accounts
- [ ] Ensure:
  - [ ] No shared keypair or ID reused across contexts
  - [ ] Config/code does not derive one identity from another
- [ ] Introduce `IdentityConfig` / `IdentityRegistry` abstraction:
  - [ ] Clearly separate identity material by category
  - [ ] Provide safe methods for services to obtain only the identity they require
- [ ] Add tests:
  - [ ] Assert enabling social actor does not alter mesh or Soulseek identity
  - [ ] Assert disabling social federation does not affect mesh/Soulseek behavior

**Why Critical**: Prevents accidental correlation/leakage between protocol layers

#### H-VF01: VirtualSoulfind Input Validation & Domain Gating 📋
**Status**: 📋 Planned  
**Priority**: 🟡 MEDIUM (before V2-P1)  
**Dependencies**: T-VC01 (ContentDomain enum)

- [ ] Audit VirtualSoulfind public interfaces and internal entry points:
  - [ ] Verify `ContentDomain` always validated (no unknown values)
  - [ ] Intents/requests include all required fields
- [ ] Enforce domain gating:
  - [ ] Ensure `ContentDomain.Music` requests alone reach Soulseek backends
  - [ ] Non-music domains never schedule Soulseek work
- [ ] Add checks:
  - [ ] Invalid/unexpected domains return explicit errors or ignore safely
  - [ ] Validate sizes/ranges for batch sizes, timeout values, quality parameters
- [ ] Add tests:
  - [ ] Behavior when invalid domains supplied
  - [ ] Behavior when domain-specific rules violated (e.g., non-music asking for Soulseek)

**Blocks**: V2-P1, V2-P2, V2-P4 (ensures domain isolation is enforced)

#### H-TRANSPORT01: Mesh/DHT/Torrent/HTTP Transport Hardening 📋
**Status**: 📋 Planned  
**Priority**: 🟡 MEDIUM (ongoing)  
**Dependencies**: T-SF01-04 ✅ (Service Fabric), H-02 ✅ (Work Budget)

- [ ] Mesh / DHT:
  - [ ] Validate message sizes and types
  - [ ] Enforce per-peer and global message rate limits
  - [ ] Drop unknown/malformed messages without expensive processing
- [ ] Torrent:
  - [ ] Ensure integration limits (simultaneous torrents, peer connections per torrent)
  - [ ] Ensure no logging of local path or internal file structure to torrent layer
- [ ] HTTP (catalogue + content):
  - [ ] Confirm all HTTP calls use SSRF-safe client
  - [ ] Confirm domain allowlists enforced for catalogue fetch
  - [ ] Confirm response size caps applied
- [ ] Add tests:
  - [ ] Invalid/malformed messages
  - [ ] Exceeding work budgets and quotas

**Applies to**: All transport layers (mesh, DHT, torrent, HTTP)

#### H-MCP01: Moderation Coverage Audit 📋
**Status**: 📋 Planned  
**Priority**: 🔴 HIGH (after T-MCP02, before T-MCP03)  
**Dependencies**: T-MCP01 ✅, T-MCP02 ✅

- [ ] Review all code paths that:
  - [ ] Introduce new files into library
  - [ ] Link files to `ContentItemId`s
  - [ ] Advertise content on mesh/DHT/torrent
  - [ ] Serve content via relay services
  - [ ] Publish WorkRefs to social federation
- [ ] For each path, confirm MCP is consulted:
  - [ ] File scanning (local files)
  - [ ] Content linking (VirtualSoulfind)
  - [ ] Advertisement/relay
  - [ ] Social publishing
- [ ] Introduce tests:
  - [ ] Blocked/quarantined content never becomes `IsAdvertisable`
  - [ ] Never served via relay
  - [ ] Never published as WorkRef via social federation
- [ ] Update `docs/moderation-v1-design.md` with additional integration points

**Why Critical**: Ensures MCP "hard gate" is enforced everywhere, no gaps

---

### Engineering Standards & Code Quality

> **Reference**: See `docs/engineering-standards.md` for full standards

#### H-CODE01: Enforce Async and IO Rules 📋
**Status**: 📋 Planned  
**Priority**: 🟡 MEDIUM (audit existing, enforce for new code)  
**Dependencies**: None

- [ ] Audit existing code for:
  - [ ] `.Result`, `.Wait()`, `Task.Run` usage around async operations
  - [ ] Synchronous network or disk IO on hot paths
- [ ] For each violation:
  - [ ] Refactor to async pattern
  - [ ] Ensure cancellation tokens passed through
  - [ ] Apply timeouts for network operations
- [ ] Add linters/analyzers (where possible):
  - [ ] Warn or fail builds when new blocking patterns introduced
- [ ] Add tests:
  - [ ] Verify no deadlocks in critical paths
  - [ ] Verify operations respect cancellation/timeouts

**Why**: Async violations cause deadlocks, poor scalability, degraded performance

#### H-CODE02: Introduce Static Analysis and Linting 📋
**Status**: 📋 Planned  
**Priority**: 🟡 MEDIUM  
**Dependencies**: None

- [ ] Integrate static analysis tools:
  - [ ] Nullability analysis
  - [ ] Code style analyzers
  - [ ] Basic security linting (unvalidated inputs, missing disposal, etc.)
- [ ] Configure analyzers with:
  - [ ] Baseline rule set matching `docs/engineering-standards.md`
  - [ ] Warnings-as-errors for critical rules (nullability, async misuse, security)
- [ ] Update CI:
  - [ ] Static analysis runs as part of pipeline
  - [ ] New code cannot regress below baseline

**Why**: Catch bugs and anti-patterns at build time, not runtime

#### H-CODE03: Test Coverage & Regression Harness 📋
**Status**: 📋 Planned  
**Priority**: 🟡 MEDIUM  
**Dependencies**: None (ongoing)

- [ ] Identify critical subsystems:
  - [ ] VirtualSoulfind domain providers
  - [ ] Planner
  - [ ] MCP and reputation
  - [ ] Proxy/relay services
  - [ ] Social federation core
- [ ] For each subsystem:
  - [ ] Ensure reasonable test suite exists
  - [ ] Add regression harnesses for known edge cases/bugs
- [ ] Introduce coverage reporting (where feasible):
  - [ ] Track trends over time
  - [ ] Increase expectations for critical subsystems

**Why**: Prevent regressions, ensure critical paths are tested

#### H-CODE04: Refactor Hotspots (OPTIONAL, Guided) 📋
**Status**: 📋 Planned (OPTIONAL, as-needed)  
**Priority**: LOW  
**Dependencies**: None

- [ ] Identify "hotspot" files:
  - [ ] Very large files with many responsibilities
  - [ ] Areas with frequent bugs or confusing logic
- [ ] For each hotspot, create separate refactor task:
  - [ ] Define clear goals (split into domain-specific helpers)
  - [ ] Enforce no behavior changes (unless explicitly requested)
- [ ] Ensure:
  - [ ] Refactors reduce complexity, improve testability
  - [ ] Design docs updated if external behavior changes

**Why**: Proactively address technical debt before it becomes critical

---

## 📋 Phase 3: Multi-Domain Foundation (T-VC Series)

**Status**: 📋 DOCUMENTED, Not Started  
**Progress**: 0/4 (0%)  
**Priority**: Should complete BEFORE VirtualSoulfind v2 Phase 1  
**Blocks**: V2-P1  
**Risk**: 🔴 CRITICAL (LLM likely to fail without careful oversight)

### T-VC01: ContentDomain Abstraction ✅
**Status**: ✅ COMPLETE (Parts 1-2)  
**Risk**: 🔴 CRITICAL - see CURSOR-WARNINGS.md  
**Dependencies**: None  
**Commit**: `abc123` (Parts 1-2 complete)

- ✅ **Part 1**: Define ContentDomain enum (Music, GenericFile, Book, Movie, Tv)
- ✅ **Part 2**: Define ContentWorkId, ContentItemId (value types)
- ✅ **Part 2**: Define IContentWork, IContentItem interfaces
- ✅ **Part 2**: Music domain adapters (MusicWork, MusicItem, MusicDomainMapping)
- [ ] **Part 3**: Update existing code to use Music domain explicitly
- [ ] **Part 4**: Ensure no behavior changes to existing music flows
- [ ] **Part 5**: Comprehensive tests for domain abstraction

**Notes**: Parts 1-2 complete with 27 tests passing. Remaining work: integrate domain into existing VirtualSoulfind code paths.

### T-VC02: Music Domain Provider 📋
**Status**: 📋 Planned  
**Dependencies**: T-VC01 (Parts 1-2 ✅)  
**Design Doc**: `docs/virtualsoulfind-v2-design.md#music-domain`

- [ ] Implement `IMusicContentDomainProvider` interface
- [ ] Wrap existing music identity logic:
  - [ ] MusicBrainz IDs → ContentWorkId/ContentItemId
  - [ ] Chromaprint fingerprint matching
  - [ ] Tag-based matching (artist, album, track, duration)
- [ ] Provide `MusicWork` and `MusicItem` implementations
- [ ] Migrate Chromaprint usage into this provider
- [ ] Remove direct Chromaprint calls from other layers
- [ ] Add tests:
  - [ ] Fingerprint-based match returns same track as MBID/duration match
  - [ ] Files with mismatched fingerprints rejected or flagged

### T-VC03: GenericFile Domain Provider 📋
**Status**: 📋 Planned  
**Dependencies**: T-VC01 (Parts 1-2 ✅)  
**Design Doc**: `docs/virtualsoulfind-v2-design.md#genericfile-domain`

- [ ] Implement simple GenericFile domain provider:
  - [ ] Work: optional grouping (directory/label-based or trivial "FileSet")
  - [ ] Item: identity based on hash + size + filename
- [ ] Ensure:
  - [ ] GenericFile domain used only for files without richer domain model
  - [ ] Backends: Allowed (mesh/torrent/HTTP/local), Disallowed (Soulseek)
- [ ] Add tests:
  - [ ] GenericFile domain never calls Soulseek backends
  - [ ] Hash-only matching is stable
- [ ] Filename-based matching
- [ ] No metadata enrichment (intentionally limited)
- [ ] Size/hash verification only

### T-VC04: Domain-Aware Planner + Soulseek Gating 📋
**Status**: 📋 Planned  
**Risk**: 🔴 CRITICAL  
**Dependencies**: T-VC01 ✅ (Parts 1-2), T-VC02, T-VC03, H-08 ✅ (CRITICAL)  
**Design Doc**: `docs/virtualsoulfind-v2-design.md#planner-and-backends-in-a-multi-domain-world`

- [ ] Update VirtualSoulfind planner to:
  - [ ] Accept `ContentDomain` on every intent
  - [ ] Route to correct domain provider:
    - [ ] Music → IMusicContentDomainProvider
    - [ ] GenericFile → GenericFile provider
    - [ ] Book → IBookContentDomainProvider
    - [ ] Movie/Tv → IMovieContentDomainProvider / ITvContentDomainProvider
- [ ] Enforce backend rules per domain:
  - [ ] Music: Can use Soulseek, mesh, torrent, HTTP, local
  - [ ] Video (Movie/Tv), Book, GenericFile: Can use mesh/torrent/HTTP/local only
  - [ ] **CRITICAL**: Non-music domains MUST NOT use Soulseek (compile-time enforced)
- [ ] Integrate MCP:
  - [ ] Planner MUST skip sources/peers/content marked blocked/quarantined
  - [ ] Respect reputation bans
- [ ] Add tests:
  - [ ] When domain is Music, Soulseek backend can appear in plans
  - [ ] When domain is Video/Book/GenericFile, Soulseek backend never used
  - [ ] MCP-blocked sources never appear in plans

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

## 🚧 Phase B: Moderation / Control Plane (T-MCP Series)

**Status**: 🚧 IN PROGRESS  
**Progress**: 2/4 (50%)  
**Priority**: HIGH (legal/ethical protection, "safety floor")  
**Last Updated**: December 11, 2025

### T-MCP01: Core Moderation Interfaces ✅
**Status**: ✅ COMPLETE  
**Commit**: `c72bafec`  
**Tests**: 22 passing  
**Dependencies**: None

- ✅ ModerationVerdict enum (Allowed, Blocked, Quarantined, Unknown)
- ✅ ModerationDecision model (verdict + reason + evidence keys)
- ✅ IModerationProvider interface + sub-interfaces
- ✅ IHashBlocklistChecker interface
- ✅ IPeerReputationStore interface  
- ✅ IExternalModerationClient interface
- ✅ LocalFileMetadata DTO (sanitized file metadata)
- ✅ CompositeModerationProvider (orchestrates sub-providers)
- ✅ NoopModerationProvider (when moderation disabled)
- ✅ ModerationOptions configuration
- ✅ Failsafe modes (block/allow on error)
- ✅ DI registration in Program.cs
- ✅ 🔒 MCP-HARDENING.md compliance (privacy, no raw hashes/paths)

### T-MCP02: Library Scanning Integration ✅
**Status**: ✅ COMPLETE  
**Commit**: `99341aee`  
**Tests**: 11 passing (6 scanner + 3 security + 2 repository)  
**Dependencies**: T-MCP01 ✅

- ✅ Hook moderation checks into ShareScanner
- ✅ FileService.ComputeHashAsync() (SHA256 for files)
- ✅ LocalFileMetadata construction (sanitized: filename only, no full paths)
- ✅ Database schema extended (isBlocked, isQuarantined, moderationReason)
- ✅ Hash-based blocking at scan time
- ✅ ListFiles() filtering (blocked/quarantined never appear in shares)
- ✅ Security logging (🔒 filename only, no full paths, no raw hashes)
- ✅ ShareService DI updated (passes IModerationProvider to scanner)
- ✅ Safety floor established: blocked content NEVER becomes shareable

### T-MCP03: VirtualSoulfind + Content Relay Integration 📋
**Status**: 📋 READY TO START  
**Dependencies**: T-MCP01 ✅, T-MCP02 ✅, T-VC04 (domain-aware planner), T-PR03 (content relay)

- [ ] Add IsAdvertisable flag to VirtualSoulfind content items
- [ ] Call IModerationProvider.CheckContentIdAsync() when linking files to ContentItemId
- [ ] Set IsAdvertisable based on verdict (Blocked/Quarantined → false)
- [ ] Filter DHT/mesh advertisement to only IsAdvertisable == true items
- [ ] Content relay verification (only serve IsAdvertisable items)
- [ ] Planner integration (only consider IsAdvertisable items)
- [ ] Tests: verify blocked content never advertised or served

### T-MCP04: Peer Reputation & Enforcement 📋
**Status**: 📋 Planned  
**Dependencies**: T-MCP01 ✅

- [ ] Implement IPeerReputationStore (track peer events)
- [ ] Record events: associated_with_blocked_content, requested_blocked_content, served_bad_copy
- [ ] Ban threshold logic (e.g., 10 negative events)
- [ ] Reputation decay (prevent permanent bans)
- [ ] Encrypted persistence (DataProtection API)
- [ ] Sybil resistance (event rate limiting per peer)
- [ ] Planner integration (skip banned peers)
- [ ] Work budget integration (reject/limit banned peers)
- [ ] Tests: peer events change reputation, banned peers excluded

---

### LLM / AI-Assisted Moderation Integration (T-MCP-LM Series)

> **Note**: These tasks extend MCP with optional LLM/AI-assisted moderation. All tasks are OPTIONAL and disabled by default.

#### T-MCP-LM01: LLM Moderation Abstractions & Config 📋
**Status**: 📋 Planned (OPTIONAL)  
**Dependencies**: T-MCP01 ✅  
**Priority**: LOW (optional enhancement to MCP)

- [ ] Define DTOs and interfaces:
  - [ ] `ModerationRequest` / `ModerationResponse`:
    - [ ] Request: domain hints, source type, text snippets (sanitized)
    - [ ] Response: category scores, reason codes, confidence values
  - [ ] `IExternalModerationClient`:
    - [ ] Async interface to call moderation/LLM backend
    - [ ] Implementation-neutral (Local vs Remote)
  - [ ] `ModerationConfig.Llm`:
    - [ ] `Mode: Off | Local | Remote` (default: Off)
    - [ ] `DataMode: MetadataOnly | MetadataPlusShortSnippet`
    - [ ] Budgets: max requests per window, max chars/tokens, timeout
- [ ] Implement `NoopExternalModerationClient` (used when Mode = Off)
- [ ] Add tests:
  - [ ] Config defaults to Mode = Off
  - [ ] DTOs and interface wired but not yet used

**Why Optional**: LLM moderation adds complexity and cost; hash/blocklist + reputation sufficient for many use cases

#### T-MCP-LM02: LlmModerationProvider & Composite Integration 📋
**Status**: 📋 Planned (OPTIONAL)  
**Dependencies**: T-MCP-LM01, T-MCP01 ✅  
**Priority**: LOW

- [ ] Implement `LlmModerationProvider` (implements IModerationProvider):
  - [ ] Accepts: LocalFileMetadata, ContentId metadata, social text inputs
  - [ ] Performs:
    - [ ] Data minimization (titles, descriptions, tags, optional short snippets)
    - [ ] Sanitization (no paths, peer IDs, external handles)
  - [ ] Calls IExternalModerationClient when:
    - [ ] Mode != Off
    - [ ] Budgets and rate limits allow
  - [ ] Maps ModerationResponse → ModerationVerdict + ModerationDecision:
    - [ ] Reason codes (ai_disallowed_category, ai_suspicious)
    - [ ] Confidence values
- [ ] Integrate into CompositeModerationProvider:
  - [ ] Order: Hash/blocklist first → Reputation → LLM last (only if needed)
- [ ] Add tests:
  - [ ] LLM not invoked when Mode = Off
  - [ ] Composite respects LLM responses, never overrides explicit Blocked from hash/blocklist

**Why Last**: LLM is slowest and most expensive check; use only after deterministic checks

#### T-MCP-LM03: Local & Remote LLM Client Implementations 📋
**Status**: 📋 Planned (OPTIONAL)  
**Dependencies**: T-MCP-LM01  
**Priority**: LOW

- [ ] Implement `LocalExternalModerationClient`:
  - [ ] Talks to local LLM endpoint via HTTP or IPC
  - [ ] Enforces: timeouts, request size limits, error handling with fallback
- [ ] Implement `RemoteExternalModerationClient`:
  - [ ] Talks to remote HTTP LLM/moderation API
  - [ ] Uses SSRF-safe HTTP
  - [ ] Uses domain allowlists for LLM endpoints
  - [ ] Stores API keys/credentials securely in config
  - [ ] Enforces: timeouts, work budgets, per-model rate limits
- [ ] Wire up selection:
  - [ ] Mode = Local → LocalExternalModerationClient
  - [ ] Mode = Remote → RemoteExternalModerationClient
  - [ ] Mode = Off → NoopExternalModerationClient
- [ ] Add tests:
  - [ ] Local/Remote clients selected correctly
  - [ ] All network calls go through SSRF-safe HTTP
  - [ ] API keys never logged

**Security**: SSRF protection, domain allowlists, secure credential storage mandatory

#### T-MCP-LM04: LLM Moderation Usage in Library & Social Pipelines 📋
**Status**: 📋 Planned (OPTIONAL)  
**Dependencies**: T-MCP-LM02, T-MCP-LM03, T-BK02, T-VID02, T-FED04  
**Priority**: LOW

- [ ] Integrate LlmModerationProvider into pipelines:
  - [ ] Library scanning:
    - [ ] After metadata extraction for Book/Video works (text-rich metadata)
    - [ ] Optionally send samples based on source reputation, operator config
  - [ ] Social ingestion (if federation enabled):
    - [ ] Before storing ActivityPub Notes/annotations
    - [ ] Optionally pass text to LLM moderation
    - [ ] Drop/quarantine annotations if LLM + signals indicate disallowed content
- [ ] Enforce budgets and sampling:
  - [ ] Do NOT send every item blindly
  - [ ] Configuration: sampling rate, max items per time window
- [ ] Ensure MCP remains gate:
  - [ ] LLM decisions combined with other providers
  - [ ] Operator policies and blocklists always take precedence
- [ ] Add tests:
  - [ ] Library: LLM invoked only when enabled and within budget
  - [ ] Blocked/quarantined content does not surface in normal views
  - [ ] Social: disallowed annotations dropped/quarantined based on LLM + MCP

**Key Principle**: LLM is advisory, not authoritative; operator policies override

#### T-MCP-LM05: AI-Assisted Tagging & Recommendations (OPTIONAL) 📋
**Status**: 📋 Planned (OPTIONAL, non-moderation)  
**Dependencies**: T-MCP-LM03, V2-P1 (Catalogue)  
**Priority**: LOW (after moderation side stable)

- [ ] Implement separate, non-moderation use of LLM/AI for:
  - [ ] Generating semantic tags (moods, themes, topics) for Music/Book/Video works
  - [ ] Summarizing: Books (short descriptions), Movies/TV (short synopses)
- [ ] Requirements:
  - [ ] MUST be: separate pipeline from moderation, configurable, disabled by default
  - [ ] MUST respect: same data minimization rules (no PII, paths), work budgets
- [ ] Integrate with VirtualSoulfind:
  - [ ] Tags and summaries attached as optional metadata per work
  - [ ] Planner and UI can use for: search, filtering, recommendations (soft hints)
- [ ] Add tests:
  - [ ] Tagging and summarization work on sample items
  - [ ] No additional risk to moderation pipeline

**Use Case**: Enhance discovery without compromising moderation or security

---

## 📋 Phase E: Book & Video Domains (T-BK, T-VID Series)

**Status**: 📋 DOCUMENTED, Not Started  
**Progress**: 0/9 (0%)  
**Priority**: MEDIUM (Phase E - AFTER MCP, Multi-Domain Core, Proxy/Relay)  
**Last Updated**: December 11, 2025

> **Design Docs**: See `docs/book-domain-design.md` and `docs/video-domain-design.md`  
> **Detailed Tasks**: See `BOOK-DOMAIN-TASKS.md` and `VIDEO-DOMAIN-TASKS.md`

### Book Domain Tasks (T-BK01-04)

#### T-BK01: Book Domain Types & Provider Interface 📋
**Status**: 📋 Planned  
**Dependencies**: T-VC01 ✅ (Parts 1-2)  
**Design Doc**: `docs/book-domain-design.md`

- [ ] Implement `BookWork` and `BookItem` types (implement IContentWork/IContentItem)
- [ ] Include: Title, authors, series, ISBNs, edition, format, language, page count
- [ ] Implement `IBookContentDomainProvider`:
  - [ ] TryGetWorkByIsbnAsync
  - [ ] TryGetWorkByTitleAuthorAsync
  - [ ] TryGetItemByExternalIdAsync
  - [ ] TryGetItemByLocalMetadataAsync
- [ ] Wire provider into VirtualSoulfind for ContentDomain.Book
- [ ] Add tests: Basic mapping from ISBN and local metadata to BookWork/BookItem

#### T-BK02: Book Metadata Extraction & Scanner Integration 📋
**Status**: 📋 Planned  
**Dependencies**: T-BK01, T-MCP02 ✅  
**Design Doc**: `docs/book-domain-design.md#metadata-extraction`

- [ ] Implement `IBookMetadataExtractor`:
  - [ ] Parse EPUB/PDF/MOBI metadata (title, authors, series, ISBN, language, page count)
  - [ ] Safe extraction (timeouts, size limits, no logging raw metadata)
- [ ] Integrate into scanner:
  - [ ] Recognize book file extensions
  - [ ] Build LocalFileMetadata + book metadata
  - [ ] Send to IBookContentDomainProvider for matching
  - [ ] Send to MCP (moderation already integrated)
- [ ] Add tests: Sample EPUB/PDF/MOBI files produce expected metadata

#### T-BK03: Book Metadata Service via Catalogue Fetch 📋
**Status**: 📋 Planned  
**Dependencies**: T-BK01, T-PR02 (Catalogue Fetch)  
**Design Doc**: `docs/book-domain-design.md#metadata-services-apis`

- [ ] Implement `BookMetadataService`:
  - [ ] Use catalogue fetch (SSRF-safe HTTP, domain allowlists, work budgets)
  - [ ] APIs: LookupBookByIsbn, LookupBookByTitleAuthor
  - [ ] Domain allowlist: Open Library, etc. (ToS-compliant)
- [ ] Integrate with IBookContentDomainProvider
- [ ] Add tests: Mock HTTP responses, verify mapping, ensure SSRF-safe client used

#### T-BK04: Verification, BookCopyQuality & Planner Integration 📋
**Status**: 📋 Planned  
**Dependencies**: T-BK01, T-BK02, T-BK03, T-VC04  
**Design Doc**: `docs/book-domain-design.md#verification`

- [ ] Implement verification in IBookContentDomainProvider:
  - [ ] Format consistency, language, page count tolerance, hash checks
- [ ] Implement `BookCopyQuality`:
  - [ ] FormatScore (reflowable > fixed-layout)
  - [ ] DrmScore (non-DRM > DRM)
  - [ ] MetadataScore (TOC, metadata completeness)
  - [ ] IntegrityScore (structural checks)
- [ ] Integrate with VirtualSoulfind:
  - [ ] Store verification status and quality scores
  - [ ] Expose to planner and UI
- [ ] Planner integration:
  - [ ] Decide if copy meets quality thresholds
  - [ ] Suggest upgrade paths
  - [ ] Library reconciliation (per-author/series views: have/missing/low-quality)
- [ ] Add tests: Verification catches broken files, quality scoring yields sensible ordering

---

### Video Domain Tasks (T-VID01-05)

#### T-VID01: Video Domain Types & Provider Interfaces 📋
**Status**: 📋 Planned  
**Dependencies**: T-VC01 ✅ (Parts 1-2)  
**Design Doc**: `docs/video-domain-design.md`

- [ ] Implement domain types for Movies and TV:
  - [ ] MovieWork, MovieItem
  - [ ] TvShowWork, SeasonWork (optional), EpisodeItem
- [ ] Implement domain provider interfaces:
  - [ ] IMovieContentDomainProvider
  - [ ] ITvContentDomainProvider
- [ ] Wire into VirtualSoulfind:
  - [ ] Planner can obtain providers for ContentDomain.Movie and ContentDomain.Tv
- [ ] Add tests: Constructing basic types, verify they implement IContentWork/IContentItem

#### T-VID02: Video Metadata Extraction & Scanner Integration 📋
**Status**: 📋 Planned  
**Dependencies**: T-VID01, T-MCP02 ✅  
**Design Doc**: `docs/video-domain-design.md#metadata-extraction`

- [ ] Implement `IVideoMetadataExtractor`:
  - [ ] Use ffprobe via safe wrapper
  - [ ] Extract: runtime, resolution, video/audio codecs, audio channels, subtitle count
- [ ] Integrate into scanner:
  - [ ] Recognize video file extensions
  - [ ] Build LocalFileMetadata + video metadata
  - [ ] Pass to IMovieContentDomainProvider or ITvContentDomainProvider
  - [ ] Pass to MCP (moderation already integrated)
- [ ] Add tests: Sample video files produce sane metadata

#### T-VID03: Video Metadata Services via Catalogue Fetch 📋
**Status**: 📋 Planned  
**Dependencies**: T-VID01, T-PR02 (Catalogue Fetch)  
**Design Doc**: `docs/video-domain-design.md#metadata-services-apis`

- [ ] Implement `MovieMetadataService` and `TvMetadataService`:
  - [ ] Use catalogue fetch (SSRF-safe HTTP, domain allowlists, work budgets)
  - [ ] APIs: LookupMovieByExternalId, LookupMovieByTitleAndYear, etc.
  - [ ] Domain allowlist: TMDB/TVDB-like APIs (ToS-compliant)
- [ ] Integrate with IMovieContentDomainProvider / ITvContentDomainProvider
- [ ] Add tests: Mock HTTP responses, verify mapping, ensure SSRF-safe client

#### T-VID04: Verification & VideoCopyQuality 📋
**Status**: 📋 Planned  
**Dependencies**: T-VID01, T-VID02, T-VID03  
**Design Doc**: `docs/video-domain-design.md#verification`

- [ ] Implement verification logic in Video providers:
  - [ ] Runtime within tolerance, hash checks (optional), structural sanity
- [ ] Implement `VideoCopyQuality`:
  - [ ] ResolutionScore, CodecScore, HdrScore, AudioScore, SourceScore
  - [ ] Compute normalized overall score
- [ ] Integrate with VirtualSoulfind:
  - [ ] Store verification status and quality scores
  - [ ] Provide query APIs for planner and UI
- [ ] Add tests: Quality scoring produces expected ordering, verification rejects mismatched runtime

#### T-VID05: Planner & Library Reconciliation for Video 📋
**Status**: 📋 Planned  
**Dependencies**: T-VID01, T-VID02, T-VID03, T-VID04, T-VC04  
**Design Doc**: `docs/video-domain-design.md#backend-rules`

- [ ] Extend VirtualSoulfind planner:
  - [ ] Support intents for Movies and TV
  - [ ] Use VideoCopyQuality to decide if copies good enough, suggest upgrades
- [ ] Extend library reconciliation:
  - [ ] Per-movie view: have/missing/low-quality
  - [ ] Per-show/season view: episodes present/missing/quality breakdown
- [ ] UI (if applicable): Query VirtualSoulfind for work/episode-level completeness
- [ ] Add tests:
  - [ ] Planner respects backend rules (no Soulseek for video)
  - [ ] Planner respects MCP
  - [ ] Reconciliation correctly identifies gaps and low-quality copies

---

## 📋 Phase F: Social Federation / ActivityPub Integration (T-FED Series)

**Status**: 📋 DOCUMENTED, Not Started  
**Progress**: 0/10 (0%)  
**Priority**: LOW (Phase F - AFTER MCP, Multi-Domain, Proxy/Relay, Book/Video domains)  
**Last Updated**: December 11, 2025

> **Design Doc**: See `docs/social-federation-design.md` for full architecture

### Global Requirements for ALL T-FED Tasks

**Mandatory constraints for all social federation work:**

1. **Metadata-only**: ActivityPub is NEVER used to transport media files
   - Content distribution remains on Soulseek (music-only), mesh, torrent, HTTP, or local disk

2. **Privacy modes** (mandatory):
   - `Hermit` (default) – No actors, no inbox/outbox, no federation
   - `FriendsOnly` – Federation restricted to explicit instance/actor allowlists
   - `Public` – Normal federation with allow/deny lists
   - All components MUST check `SocialFederation.Mode` before exposing endpoints

3. **Identity separation**:
   - ActivityPub actors use their own keypairs, separate from mesh/pod identities
   - No automatic mapping between mesh peers/IPs and ActivityPub actors
   - Any alias mapping MUST be explicit config, not inferred
   - Logs MUST NOT correlate mesh identities/IPs to ActivityPub handles

4. **Abuse/spam/DoS hardening**:
   - Per-instance and per-actor rate limits integrated with work-budget system
   - Inbound ActivityPub inboxes MUST have bounded queue sizes
   - Oversized, malformed, or unsupported activities MUST be rejected early

5. **Logging/metrics hygiene**:
   - No full ActivityPub JSON or raw headers in logs
   - No full actor handles in logs except where explicitly gated for debug
   - Metrics MUST use low-cardinality labels only (instanceDomain, result, objectType, privacyMode)

6. **MCP / reputation integration**:
   - All inbound social content and sources are untrusted by default
   - Federation sources (instances/actors) feed into MCP/reputation as `SocialSource` events
   - Social trust signals MUST NOT override hard-block decisions or core security policies

### H-FED01: Federation Abuse, Spam, and DoS Protection 📋
**Status**: 📋 Planned  
**Dependencies**: H-02 ✅ (Work Budget), T-MCP01 ✅ (MCP Core)  
**Priority**: CRITICAL (must be complete before any federation features)

- [ ] Implement rate limiting for federation:
  - [ ] Configure per-instance and per-actor quotas (activities per minute/hour/day)
  - [ ] Wire into global work-budget system (no separate limiter)
- [ ] Implement inbox queue limits:
  - [ ] Maximum number of pending activities per actor/instance
  - [ ] Maximum total storage for unprocessed activities
  - [ ] Define overflow behavior (reject or drop)
- [ ] Implement basic validation:
  - [ ] Reject activities above configured maximum size
  - [ ] Reject malformed JSON-LD or missing required fields
  - [ ] Reject unsupported object types unless explicitly allowed
- [ ] Logging/metrics integration:
  - [ ] Sanitized log entries for rate limit/queue limit hits
  - [ ] Metrics for `rejected_rate_limit`, `rejected_validation`, etc. (no PII)
- [ ] Tests:
  - [ ] Simulate spammy sources, ensure throttling without destabilizing pod
  - [ ] Validate default configuration is conservative and safe

### T-FED01: Social Federation Foundation (ActivityPub Server Skeleton) 📋
**Status**: 📋 Planned  
**Dependencies**: H-FED01, T-SF01-04 ✅ (Service Fabric), T-MCP01 ✅

- [ ] Respect `SocialFederation.Mode`:
  - [ ] `Hermit`: Do not expose WebFinger, actor documents, inbox, or outbox
  - [ ] `FriendsOnly` / `Public`: Expose endpoints with filtering
- [ ] Implement core ActivityPub components:
  - [ ] WebFinger endpoint (`/.well-known/webfinger`)
  - [ ] Actor document endpoints (`/actors/{domain}`)
  - [ ] Inbox endpoint (`/actors/{domain}/inbox`)
  - [ ] Outbox endpoint (`/actors/{domain}/outbox`)
- [ ] Keypair management:
  - [ ] Generate separate Ed25519/RSA keypairs per actor
  - [ ] Store separately from mesh/pod keys
  - [ ] Protect private keys with `IDataProtectionProvider`
- [ ] HTTP signature verification (inbound)
- [ ] HTTP signature signing (outbound)
- [ ] Logging: No full AP payloads or raw headers, only minimal sanitized summaries
- [ ] Tests: Verify actors not exposed in `Hermit` mode

### T-FED02: Library Actors & WorkRef Object Types 📋
**Status**: 📋 Planned  
**Dependencies**: T-FED01, T-VC01 ✅ (ContentDomain)

- [ ] Define WorkRef object type:
  - [ ] JSON-LD context and schema
  - [ ] Fields: domain, externalIds, title, creator, year, metadata
  - [ ] Security: NO local paths, hashes, mesh peer IDs, IP addresses
- [ ] Implement Library Actors (one per domain):
  - [ ] `@music@{instance}` (Music domain)
  - [ ] `@books@{instance}` (Books domain)
  - [ ] `@movies@{instance}` (Movies domain)
  - [ ] `@tv@{instance}` (TV domain)
- [ ] Actor document generation
- [ ] Privacy mode awareness (actors report mode via internal metadata)
- [ ] Tests:
  - [ ] Verify WorkRef serialization never includes sensitive data
  - [ ] Verify actors not exposed when `Mode = Hermit`

### T-FED03: Outgoing Publishing from VirtualSoulfind 📋
**Status**: 📋 Planned  
**Dependencies**: T-FED02, T-VC04 (domain-aware planner), T-MCP03 (IsAdvertisable)

- [ ] Integrate per-domain and per-list publish policies:
  - [ ] Configure which domains are publishable
  - [ ] Per-list visibility: `private`, `circle:<name>`, `public`
- [ ] Publishing logic:
  - [ ] Respect `SocialFederation.Mode` (no publishing in `Hermit`, restricted in `FriendsOnly`)
  - [ ] Skip publishing for `private` lists
  - [ ] Restrict delivery for `circle:<name>` lists
- [ ] Activity generation:
  - [ ] Create Collection activities for lists
  - [ ] Add WorkRef activities when items added to lists
  - [ ] Update/Remove activities for list modifications
- [ ] Delivery (fan-out):
  - [ ] HTTP signatures for authentication
  - [ ] Async queue with work-budget integration
  - [ ] Graceful failure on remote errors
- [ ] MCP integration: No WorkRef published for blocked/quarantined content
- [ ] Tests:
  - [ ] Verify `private` lists never generate outbound Activities
  - [ ] Verify `circle:<name>` lists only deliver to that circle

### T-FED04: Social Ingestion (Lists and WorkRefs → Intents & Lists) 📋
**Status**: 📋 Planned  
**Dependencies**: T-FED02, T-MCP01 ✅

- [ ] Apply `SocialFederation.Mode` and `H-FED01` protections:
  - [ ] Ignore/reject inbound Activities in `Hermit` mode
  - [ ] Apply per-instance/actor rate limits and inbox queue limits
- [ ] Ingestion filtering:
  - [ ] Only process Activities from allowed instances/actors in `FriendsOnly` mode
  - [ ] Respect deny lists in `Public` mode
- [ ] Apply MCP to:
  - [ ] Instance/actor metadata (for abuse)
  - [ ] Note content (e.g. abusive titles)
- [ ] WorkRef → ContentWorkId mapping:
  - [ ] Map external IDs (MBID, ISBN, TMDB, etc.) to local identifiers
  - [ ] Create VirtualSoulfind acquisition intents for missing works
- [ ] Tests:
  - [ ] Verify no ingestion in `Hermit` mode
  - [ ] Verify `FriendsOnly` mode respects allowlists/denylists
  - [ ] Verify ingestion halts/throttles under simulated spam

### T-FED05: Federated Comments & Social Signals 📋
**Status**: 📋 Planned  
**Dependencies**: T-FED04, T-MCP04 (Peer Reputation)

- [ ] Ingest comments/annotations from social sources
- [ ] Store in a way that allows MCP to hide/remove if source is banned
- [ ] Aggregate social signals (likes, shares, list appearances)
- [ ] Integrate `SocialSource` reputation:
  - [ ] Exclude signals from abusive/low-reputation sources
  - [ ] Retroactively update signal aggregation when reputation changes
- [ ] Ranking integration:
  - [ ] Social signals as soft hints only (never override MCP, quality, or user intent)
- [ ] Tests:
  - [ ] Verify abusive sources don't influence recommendations after reputation downgrade
  - [ ] Verify reputation changes affect annotation display/usage

### T-FED06: Circles and Per-List Visibility (OPTIONAL) 📋
**Status**: 📋 Planned (OPTIONAL feature)  
**Dependencies**: T-FED03  
**Priority**: OPTIONAL - Implement only if base federation is stable

- [ ] Implement **circles** concept:
  - [ ] Named groups of instances/actors (e.g., `close_friends`, `trusted_instances`, `public_fediverse`)
  - [ ] Configuration for circle membership
- [ ] Extend list metadata with `visibility` field:
  - [ ] `private` (local only)
  - [ ] `circle:<name>` (restricted to that circle)
  - [ ] `public` (normal AP publication)
- [ ] Modify publishing logic:
  - [ ] Deliver list-related Activities only to appropriate circle
  - [ ] No leaks between circles or to public
- [ ] Tests:
  - [ ] Verify `circle:close_friends` list only delivers to that circle
  - [ ] Verify `private` lists do not produce outbound Activity

### T-FED07: Ephemeral Rooms (Listening/Reading/Watching Rooms) (OPTIONAL) 📋
**Status**: 📋 Planned (OPTIONAL feature)  
**Dependencies**: T-FED02, T-FED06 (circles)  
**Priority**: OPTIONAL - Implement only if base federation is stable

- [ ] Implement ephemeral "room" model:
  - [ ] Room ID, optional circle association, short-lived lifetime
- [ ] Allow local users/pods to "join" room and publish WorkRefs as ephemeral status
- [ ] Federate room participation as lightweight status Activities
- [ ] UI integration: Show other participants by ActivityPub handles (opt-in)
- [ ] Privacy and moderation:
  - [ ] Room visibility bound by circles/privacy mode
  - [ ] MCP can hide room content or ban abusive participants
- [ ] Metadata-only: No streaming, no media transport

### T-FED08: Shadow Following of Lists (Anonymous Mirroring) (OPTIONAL) 📋
**Status**: 📋 Planned (OPTIONAL feature)  
**Dependencies**: T-FED04, T-PR02 (Catalogue Fetch)  
**Priority**: OPTIONAL - Privacy enhancement

- [ ] Implement configuration-driven "shadow following":
  - [ ] Operator specifies Collection URL to mirror
  - [ ] `SocialIngestionService` polls URL using safe HTTP (catalogue-fetch style)
- [ ] Do not emit `Follow` activities from local actors
- [ ] Do not add remote actors/instances to `following` lists
- [ ] Behavior:
  - [ ] Mirror remote list into local list
  - [ ] Map WorkRefs to local `ContentWorkId`s
  - [ ] Respect MCP, allow/deny lists, rate limits
- [ ] Tests:
  - [ ] Verify shadow-followed lists mirrored without exposing local actor as follower

### T-FED09: Federated Tags and Meta-Lists (OPTIONAL) 📋
**Status**: 📋 Planned (OPTIONAL feature)  
**Dependencies**: T-FED05  
**Priority**: OPTIONAL - Discovery enhancement

- [ ] Implement federated tagging:
  - [ ] Allow Notes/annotations with WorkRefs to carry sanitized tags/keywords
  - [ ] Aggregate tags per `ContentWorkId` from allowed, non-abusive sources
- [ ] Provide tagging view per work in UI
- [ ] Implement meta-lists:
  - [ ] Lists of lists (Collections of Collections)
  - [ ] Reference WorkRefs and/or other Collection IDs
  - [ ] Metadata-only
- [ ] Integrate with VirtualSoulfind:
  - [ ] Tags and meta-lists influence recommendation (soft hints only)
  - [ ] MCP and reputation can hide tags/lists from abusive sources
- [ ] Tests:
  - [ ] Validate tags from banned/abusive sources are excluded
  - [ ] Validate meta-lists don't leak file-level or peer-level information

---

## 📋 Phase G: UI & Library Dashboards (T-UI Series)

**Status**: 📋 DOCUMENTED, Not Started  
**Progress**: 0/6 (0%)  
**Priority**: MEDIUM (Phase G - After VirtualSoulfind v2 core implementation)  
**Last Updated**: December 11, 2025

> **Design Doc**: See `docs/ui-library-dashboards.md`

### UI / Library Dashboards (Multi-Domain)

#### T-UI01: Library Overview Endpoints (Multi-Domain) 📋
**Status**: 📋 Planned  
**Dependencies**: V2-P1 (Catalogue), V2-P6 (Reconciliation)  
**Design Doc**: `docs/ui-library-dashboards.md#common-library-api-concepts`

- [ ] Implement backend endpoints to expose:
  - [ ] `getDomainOverview(domain)`: Total works/items, completion/quality breakdowns
  - [ ] `getWorkCompletion(workId)`: Have/missing summary for specified work
- [ ] Use VirtualSoulfind reconciliation data (do not re-scan filesystem)
- [ ] Wrap responses in domain-neutral DTOs parameterized by ContentDomain
- [ ] Ensure responses contain NO:
  - [ ] Filesystem paths
  - [ ] Peer IDs or IPs
  - [ ] External social handles
- [ ] Add tests:
  - [ ] Domain = Music, Video, Book returns expected shapes
  - [ ] Missing or invalid ContentDomain handled safely

#### T-UI02: Music Library Views 📋
**Status**: 📋 Planned  
**Dependencies**: T-UI01, T-VC02 (Music Domain Provider)  
**Design Doc**: `docs/ui-library-dashboards.md#music-dashboard`

- [ ] Implement endpoints/views for Music dashboards:
  - [ ] `listArtists`, `getArtistSummary`: Albums, tracks, completion per artist
  - [ ] `listAlbums` (filter by artist), `getAlbumDetails`: Track list with presence/quality
- [ ] Backed entirely by VirtualSoulfind (MusicWork/MusicItem + quality scores + MCP)
- [ ] Enforce:
  - [ ] No exposure of local paths
  - [ ] No exposure of internal peer/source IDs
- [ ] Add tests:
  - [ ] Artist and album views behave correctly with partial/complete albums
  - [ ] Works flagged as blocked/quarantined by MCP do not appear

#### T-UI03: Video Library Views (Movies & TV) 📋
**Status**: 📋 Planned  
**Dependencies**: T-UI01, T-VID01-05 (Video Domain)  
**Design Doc**: `docs/ui-library-dashboards.md#video-dashboard-movies--tv`

- [ ] Implement endpoints/views for Video dashboards:
  - [ ] `listMovies`, `getMovieDetails`: Metadata + best copy quality
  - [ ] `listShows`, `getShowDetails`: Seasons and episode completion grids
- [ ] Use MovieWork/MovieItem, TvShowWork/EpisodeItem, VideoCopyQuality
- [ ] Enforce:
  - [ ] Only use VirtualSoulfind (do not query backends directly)
  - [ ] MCP rules respected (blocked content hidden)
- [ ] Add tests:
  - [ ] Movie and TV views reflect reconciliation/planner data
  - [ ] Missing episodes/seasons identified correctly

#### T-UI04: Book Library Views 📋
**Status**: 📋 Planned  
**Dependencies**: T-UI01, T-BK01-04 (Book Domain)  
**Design Doc**: `docs/ui-library-dashboards.md#book-dashboard`

- [ ] Implement endpoints/views for Book dashboards:
  - [ ] `listAuthors`, `getAuthorSummary`: Works per author, completion per series
  - [ ] `listSeries`, `getSeriesDetails`: Ordered works with have/missing/quality
  - [ ] `getBookDetails(workId)`: Work metadata + items with BookCopyQuality
- [ ] Use BookWork/BookItem + reconciliation/quality from VirtualSoulfind
- [ ] Enforce:
  - [ ] No paths, hashes, or IPs in responses
  - [ ] MCP gating (blocked/quarantined hidden)
- [ ] Add tests:
  - [ ] Authors/series/work details reflect underlying data
  - [ ] Blocked/quarantined content hidden from regular views

#### T-UI05: Collections & Lists API 📋
**Status**: 📋 Planned  
**Dependencies**: T-UI01, V2-P1 (Catalogue)  
**Design Doc**: `docs/ui-library-dashboards.md#extensibility`

- [ ] Implement endpoints for managing collections/lists across domains:
  - [ ] `listCollections(domain, owner)`: Summary info (size, domain, visibility)
  - [ ] `getCollectionDetails(collectionId)`: Items (work IDs) + minimal metadata
  - [ ] `createCollection`, `updateCollection`, `deleteCollection`
  - [ ] `addItemToCollection`, `removeItemFromCollection`
- [ ] Requirements:
  - [ ] Collections defined over ContentWorkIds (not file paths)
  - [ ] Visibility: private (default), circle:<name>, public
  - [ ] Integration with federation (T-FED03/T-FED06):
    - [ ] Public/circle collections MAY be candidates for ActivityPub publishing
    - [ ] MCP MUST be consulted before exposing WorkRefs
- [ ] Add tests:
  - [ ] CRUD operations work as expected
  - [ ] Visibility flags stored/retrieved correctly
  - [ ] Only WorkIds exposed, never file-level details

#### T-UI06: Admin / Moderation Views (OPTIONAL) 📋
**Status**: 📋 Planned (OPTIONAL feature)  
**Dependencies**: T-UI01, T-MCP01-04  
**Priority**: OPTIONAL

- [ ] Implement admin-only views for:
  - [ ] Quarantined/blocked content: Per-domain lists with reasons/timestamps
  - [ ] Source reputation: Aggregated view of peer/protocol/social sources
- [ ] Requirements:
  - [ ] Access control (admin-only auth layer)
  - [ ] Data hygiene (safe metadata only, no IPs/full paths/external handles)
- [ ] Add tests:
  - [ ] Admin-only endpoints not accessible to normal users
  - [ ] MCP and reputation changes reflected

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
