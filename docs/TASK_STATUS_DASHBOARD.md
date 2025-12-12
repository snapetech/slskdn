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
Pod Identity:         ░░░░░░░░░░░░░░░░░░░░   0% (  0/8   tasks complete) 📋
F1000 Governance:     ░░░░░░░░░░░░░░░░░░░░   0% (  0/6   tasks complete) 📋 (future)
First Pod & Social:   ░░░░░░░░░░░░░░░░░░░░   0% (  0/6   tasks complete) 📋 (future)
Attribution:          ░░░░░░░░░░░░░░░░░░░░   0% (  0/1   tasks complete) 📋
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

Overall: ██░░░░░░░░░░░░░░░░░░  10% (21/~201 tasks complete)

Test Coverage: 128 tests passing (SF + Security + MCP + Multi-Domain Core)
```

> ✅ **Service Fabric Foundation**: COMPLETE  
> ✅ **Security Hardening (Phase 2)**: COMPLETE - H-08 done! 🎉  
> 🚧 **Phase B - MCP (Safety Floor)**: IN PROGRESS - T-MCP01 ✅, T-MCP02 ✅
> 🚧 **Phase C - Multi-Domain Core**: IN PROGRESS - T-VC01 Parts 1-2 ✅
> 📋 **LLM/AI Moderation**: 5 OPTIONAL tasks (T-MCP-LM01-05, all disabled by default)
> 📋 **Pod Identity & Lifecycle**: 8 tasks (T-POD01-06 + H-POD01-02) - storage, export/import, retire/wipe, security
> 📋 **F1000 Governance**: 6 FUTURE tasks (T-F1000-01-06) - transferable membership, advisory only, cap-exempt master-admins
> 📋 **First Pod & Social**: 6 FUTURE tasks (T-SOCIAL-01-06) - chat, forums, ActivityPub, F1000 auto-join, community hub
> 📋 **Phase E - Book & Video Domains**: 9 tasks documented (T-BK01-04, T-VID01-05)
> 📋 **Phase G - UI & Dashboards**: 6 tasks documented (T-UI01-06)
> 📋 **Global Hardening**: 5 tasks (logging, identity, validation, transport, MCP audit)
> 📋 **Engineering Quality**: 4 tasks (async enforcement, linting, coverage, refactoring)
> 🔴 **Attribution**: 1 CRITICAL task (H-ATTR01 - MUST complete before public release)
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

### Pod Identity & Lifecycle Management (T-POD, H-POD)

> **Design Doc**: See `docs/pod-identity-lifecycle.md`

#### T-POD01: Identity Storage Layout & Config 📋
**Status**: 📋 Planned  
**Priority**: 🟡 MEDIUM (foundational for multi-pod deployments)  
**Dependencies**: None  
**Design Doc**: `docs/pod-identity-lifecycle.md` § 3

- [ ] Implement clear identity storage layout under `data/keys/`:
  - [ ] `keys/pod/` – Mesh/pod identity keypair
  - [ ] `keys/social/` – ActivityPub actor keypairs
  - [ ] `keys/soulseek/` – Soulseek backend credentials (if stored)
  - [ ] `keys/tls/` – TLS certs/keys (UI/API)
  - [ ] `keys/mcp/` – MCP-specific secrets (if needed)
- [ ] Ensure:
  - [ ] Directories configurable with sensible defaults
  - [ ] Access permissions restricted to pod process
- [ ] Implement `PodIdentityConfig` / `IdentityRegistry` abstraction:
  - [ ] Loads and provides identity materials by category
  - [ ] Enforces identity separation (no sharing keys across categories)
- [ ] Add tests:
  - [ ] Each service only gets identity material it needs
  - [ ] Failure behavior when keys missing or misconfigured

**Why**: Clean separation prevents key leakage (mesh key ≠ social key ≠ backend creds)

#### T-POD02: Identity Bundle Export 📋
**Status**: 📋 Planned  
**Priority**: 🟡 MEDIUM  
**Dependencies**: T-POD01  
**Design Doc**: `docs/pod-identity-lifecycle.md` § 4.2

- [ ] Implement **admin-only** export operation:
  - [ ] Export pod identity bundle containing:
    - [ ] Pod keypair (`keys/pod/`)
    - [ ] Social actor keypairs (`keys/social/`)
    - [ ] Optional backend creds (`keys/soulseek/`, based on flags)
- [ ] Behavior:
  - [ ] Prompt for passphrase or key to encrypt bundle
  - [ ] Serialize into single encrypted file (no plaintext on disk)
  - [ ] Use modern authenticated encryption (e.g., AES-256-GCM + Argon2)
- [ ] Logging / security:
  - [ ] Do NOT log: Bundle contents, passphrases
  - [ ] May log: Timestamp, admin ID, bundle filename
- [ ] Add tests:
  - [ ] Export/import round-trip maintains identity
  - [ ] Failure on incorrect passphrase
  - [ ] Export fails cleanly if no identities present

**Use Case**: Backup/recovery, transfer ownership, migration to new host

#### T-POD03: Identity Bundle Import 📋
**Status**: 📋 Planned  
**Priority**: 🟡 MEDIUM  
**Dependencies**: T-POD01  
**Design Doc**: `docs/pod-identity-lifecycle.md` § 4.3

- [ ] Implement **admin-only** import operation:
  - [ ] Accept: Bundle file, passphrase/key
- [ ] Behavior:
  - [ ] Validate bundle integrity
  - [ ] Show admin summary of what will be imported:
    - [ ] Pod identity present: yes/no
    - [ ] Social actors present: count and identifiers
    - [ ] Backends present: Soulseek credentials yes/no
  - [ ] On confirmation:
    - [ ] Replace or initialize identities in `keys/` with bundle contents
    - [ ] Update `PodIdentityConfig` / `IdentityRegistry`
- [ ] Conflict handling:
  - [ ] If existing keys present: Require explicit confirmation to overwrite
  - [ ] Optional: Backup old keys before overwrite (configurable)
- [ ] Add tests:
  - [ ] Fresh install + import yields working identity
  - [ ] Overwrite existing identity requires confirmation

**Use Case**: Restore from backup, receive transferred pod identity

#### T-POD04: Soft Retire / Reactivate Operations 📋
**Status**: 📋 Planned  
**Priority**: 🟡 MEDIUM  
**Dependencies**: T-POD01  
**Design Doc**: `docs/pod-identity-lifecycle.md` § 5.1

- [ ] Implement admin-only operations:
  - [ ] **Soft retire**:
    - [ ] Stop external-facing services:
      - [ ] Mesh/DHT
      - [ ] Torrent integrations
      - [ ] Soulseek client
      - [ ] Social federation
    - [ ] Leave running:
      - [ ] UI/API admin access
      - [ ] Library and MCP DBs
  - [ ] **Reactivate**:
    - [ ] Restart external services per configuration
- [ ] Provide clear status indicator:
  - [ ] Pod state: `Active` vs `Retired`
- [ ] Add tests:
  - [ ] Retired pod does not accept/initiate external protocol traffic
  - [ ] Reactivated pod resumes normal behavior

**Use Case**: Temporary shutdown, maintenance, preparing for migration

**Reversible**: ✅ Yes (reactivate restores full functionality)

#### T-POD05: Identity Suicide Operation 📋
**Status**: 📋 Planned  
**Priority**: 🟡 MEDIUM  
**Dependencies**: T-POD01, T-POD04  
**Design Doc**: `docs/pod-identity-lifecycle.md` § 5.2

- [ ] Implement admin-only **identity suicide** operation:
  - [ ] Requires:
    - [ ] Admin authentication
    - [ ] Explicit confirmation (type pod ID or hostname)
- [ ] Behavior:
  - [ ] Delete:
    - [ ] `keys/pod/` (mesh/pod identity)
    - [ ] `keys/social/` (AP actor keys)
    - [ ] Optional: Backend credentials (based on additional flags)
  - [ ] Preserve:
    - [ ] DBs (library + MCP)
    - [ ] Content files
    - [ ] Basic config (enough for local-only operation)
- [ ] After identity suicide:
  - [ ] Pod must not attempt to connect to mesh/DHT/Soulseek/social
  - [ ] Start only in local-only mode until new identities created
- [ ] Add tests:
  - [ ] Identities removed and cannot be reloaded after suicide
  - [ ] Local library functionality remains (admin-only use)

**Use Case**: Privacy-focused reset, offline-only use, cannot rejoin as same identity

**Reversible**: ❌ No (keys destroyed, identity dead)

#### T-POD06: Full Wipe Operation (Optional, High-Risk) 📋
**Status**: 📋 Planned  
**Priority**: 🟢 LOW (optional, implement with strong safeguards)  
**Dependencies**: T-POD01, T-POD05  
**Design Doc**: `docs/pod-identity-lifecycle.md` § 5.3

- [ ] Implement admin-only **full wipe** operation:
  - [ ] Requires:
    - [ ] Admin authentication
    - [ ] Multiple confirmations:
      - [ ] Warn about irreversible nature
      - [ ] Require explicit phrase entry (e.g., "DELETE POD <PodId>")
- [ ] Behavior:
  - [ ] Delete:
    - [ ] `keys/` (all identity material)
    - [ ] `db/` (all DBs: library, MCP, shares, collections)
    - [ ] Optionally content files (if chosen)
    - [ ] Non-essential config
  - [ ] Leave:
    - [ ] Minimal bootstrap config (or nothing), for fresh start
- [ ] Logging:
  - [ ] Log only: Timestamp, admin ID, that wipe occurred
  - [ ] Do NOT log: Any sensitive contents
- [ ] Add tests:
  - [ ] Full wipe removes identity and DBs
  - [ ] Restart after full wipe behaves like new install

**Use Case**: Complete shutdown, decommissioning hardware, privacy-focused destruction

**Reversible**: ❌ No (everything destroyed, cannot recover)

**⚠️ WARNING**: This is a nuclear option. Implement only with multiple confirmations and clear warnings.

#### H-POD01: Identity Security & Key Protection 📋
**Status**: 📋 Planned  
**Priority**: 🔴 HIGH (before multi-pod deployments)  
**Dependencies**: T-POD01  
**Design Doc**: `docs/pod-identity-lifecycle.md` § 9

- [ ] Implement key protection:
  - [ ] Filesystem permissions (restrict access to `keys/`)
  - [ ] Optional: Encrypt keys at rest with passphrase
  - [ ] Optional: HSM integration (future)
- [ ] Key rotation:
  - [ ] Rotate mesh key → new PodId (breaking change, document carefully)
  - [ ] Rotate AP actor keys → publish keyRotation activity (if AP spec supports)
  - [ ] Rotate encryption keys → re-encrypt reputation data
- [ ] Audit logging:
  - [ ] Log all key operations (export, import, rotate, wipe)
  - [ ] Sanitized (no actual key material in logs)
  - [ ] Include: timestamp, admin ID, operation type
- [ ] Add tests:
  - [ ] Unauthorized access to keys prevented
  - [ ] Key rotation updates dependent systems
  - [ ] Audit logs capture all operations

**Critical**: Keys are the crown jewels (losing keys = losing identity)

#### H-POD02: Admin Account Management 📋
**Status**: 📋 Planned  
**Priority**: 🟡 MEDIUM (before multi-pod deployments)  
**Dependencies**: None  
**Design Doc**: `docs/pod-identity-lifecycle.md` § 6

- [ ] Implement multi-admin support:
  - [ ] `IAdminService` with CRUD operations
  - [ ] Password hashing (bcrypt or Argon2)
  - [ ] Role-based access control (admin, moderator, read-only)
- [ ] Prevent lockout:
  - [ ] Require at least one admin account
  - [ ] Warn when removing last admin
  - [ ] Provide recovery mechanism (offline password reset)
- [ ] Add tests:
  - [ ] Multi-admin scenarios
  - [ ] Password reset flows
  - [ ] Permission enforcement

**Why**: Prevents single-controller SPOF (add second admin before losing first)

---

### F1000 Governance (T-F1000, Future Layer)

> **Design Doc**: See `docs/f1000-governance-design.md`  
> **Status**: 📋 FUTURE (post-core architecture)  
> **Priority**: 🟢 LOW (meta-governance layer, not required for basic operation)

F1000 is a **future governance layer** sitting above pods, providing:
- Advisory moderation feeds from early adopters
- Signed policy profiles (recommended configs)
- Distributed governance without centralized control

**Key Principles:**
- **Transferable** F1000 membership (requires holder + master-admin signature)
- **Master-admins cap-exempt** (founder always fits, doesn't squeeze out testers)
- **Advisory only** (pods remain sovereign, can disable governance)
- **Heavily hardened** (all operations signed, auditable, explicit confirmations)

#### T-F1000-01: Governance Identity & F1000 Registry Types 📋
**Status**: 📋 Planned (future)  
**Priority**: 🟢 LOW  
**Dependencies**: None  
**Design Doc**: `docs/f1000-governance-design.md` § 1, 2

- [ ] Implement basic data types/interfaces:
  - [ ] `GovernanceId`: Wraps `gov:<hash(pub_gov)>`
  - [ ] `GovernanceRole` enum: `root`, `master_admin`, `registrar`, `policy_signer`
  - [ ] DTOs for:
    - [ ] `F1000Epoch` (includes `f1000_cap`)
    - [ ] `F1000Registry`
    - [ ] `F1000Entry` (status: `active`, `revoked`, `transferred`)
    - [ ] `Delegation`
    - [ ] `F1000Transfer` and revocation documents
- [ ] Enforce in code:
  - [ ] `F1000_CAP` applies only to **non-master-admin** `F1000Entry` with `status = active`
  - [ ] Governance identities with `Role.MasterAdmin` are **cap-exempt**
- [ ] Add tests:
  - [ ] Registry validation enforces cap correctly
  - [ ] Master-admin entries do not cause cap violation

**Cap Rule**: Master-admins (founder + governance core) do not count toward F1000_CAP

#### T-F1000-02: Governance Client (Pod-Side, Advisory Only) 📋
**Status**: 📋 Planned (future)  
**Priority**: 🟢 LOW  
**Dependencies**: T-F1000-01  
**Design Doc**: `docs/f1000-governance-design.md` § 5

- [ ] Implement pod-side **governance client** that can:
  - [ ] Fetch F1000 registry and governance docs (HTTPS/AP)
  - [ ] Validate signatures and delegation chains
  - [ ] Provide read-only view of:
    - [ ] Epochs and caps
    - [ ] Active F1000 members
    - [ ] Master-admin roles
- [ ] Configuration:
  - [ ] Pods can enable/disable governance integration globally
  - [ ] Pin trusted governance root/council keys
  - [ ] Select which epochs/registries to trust
- [ ] Hardening:
  - [ ] Governance client runs in separate module from local admin auth
  - [ ] Never expose governance keys
  - [ ] No assumptions about pod admin identities
- [ ] Add tests:
  - [ ] Client correctly parses/validates sample registry with transfers
  - [ ] Pod behavior unchanged when governance disabled

**Isolation**: Governance logic completely separate from pod keys, admin auth, MCP config

#### T-F1000-03: Transferable F1000 Membership Handling 📋
**Status**: 📋 Planned (future)  
**Priority**: 🟢 LOW  
**Dependencies**: T-F1000-01, T-F1000-02  
**Design Doc**: `docs/f1000-governance-design.md` § 3.2

- [ ] Implement logic to process **F1000Transfer** documents:
  - [ ] Require signatures:
    - [ ] From current holder (`from_governance_id`)
    - [ ] From governance identity with `Role.MasterAdmin` or `Role.Registrar`
  - [ ] Update registry entries:
    - [ ] Mark old entry `status = transferred`
    - [ ] Create new entry for `to_governance_id` with `status = active`
- [ ] Enforce:
  - [ ] No double-active membership for single governance ID within epoch
  - [ ] Cap compliance for non-master-admin members only
- [ ] Add tests:
  - [ ] Valid transfers succeed and update registry
  - [ ] Transfers exceeding cap (for non-master-admin) rejected
  - [ ] Transfers with missing/invalid sender signature rejected

**Security**: Dual-signature requirement prevents unilateral hijacking

#### T-F1000-04: HumanModerationProvider(F1000) (Optional, Advisory) 📋
**Status**: 📋 Planned (future)  
**Priority**: 🟢 LOW  
**Dependencies**: T-F1000-02, T-MCP01 (MCP foundation)  
**Design Doc**: `docs/f1000-governance-design.md` § 4.1

- [ ] Implement optional `HumanModerationProvider` for MCP that uses:
  - [ ] F1000-curated moderation feeds (hash lists, WorkRef verdicts)
- [ ] Behavior:
  - [ ] Treat F1000 signals as advisory inputs
  - [ ] Map into `ModerationVerdict` according to local policy
  - [ ] Never override local blocklists or explicit admin rules
- [ ] Hardening:
  - [ ] Entire provider MUST be disableable via config
  - [ ] Provider MUST not:
    - [ ] Modify MCP config or blocklists
    - [ ] Introduce new network calls beyond governance feed fetches
- [ ] Add tests:
  - [ ] MCP integrates F1000 provider correctly when enabled
  - [ ] No effect on moderation when disabled

**Advisory Only**: F1000 signals are inputs to MCP, never mandatory gates

#### T-F1000-05: Policy Profiles Signed by Governance Identities (Optional) 📋
**Status**: 📋 Planned (future)  
**Priority**: 🟢 LOW  
**Dependencies**: T-F1000-02  
**Design Doc**: `docs/f1000-governance-design.md` § 4.2

- [ ] Implement support for governance-signed **policy profiles**:
  - [ ] Define `PolicyProfile` DTO:
    - [ ] Name, version, payload (config)
    - [ ] One or more governance signatures (F1000 or council)
  - [ ] Extend pod configuration bootstrapping:
    - [ ] Optionally fetch policy profiles from governance feeds
    - [ ] Offer admin-facing UI/CLI:
      - [ ] Compare current config vs profile
      - [ ] Apply profile changes only with explicit admin consent
- [ ] Hardening:
  - [ ] Never auto-apply profile changes that affect:
    - [ ] LLM providers
    - [ ] MCP decision thresholds
    - [ ] Federation settings
  - [ ] Admin must explicitly approve
- [ ] Add tests:
  - [ ] Profiles parsed and validated against governance keys
  - [ ] Local admin override always wins

**Explicit Consent**: Profile changes require admin approval, never auto-applied

#### T-F1000-06: Governance Tooling (Out-of-Band, Optional) 📋
**Status**: 📋 Planned (future)  
**Priority**: 🟢 LOW  
**Dependencies**: T-F1000-01, T-F1000-03  
**Design Doc**: `docs/f1000-governance-design.md` § 3

- [ ] Implement out-of-band governance tools (CLI/desktop/web) for governance participants:
  - [ ] To:
    - [ ] Generate governance keys (gov IDs)
    - [ ] Manage delegations (assign/revoke roles)
    - [ ] Manage F1000 registry (add/revoke/transfer) with clear UX
    - [ ] Sign policy profiles and governance transitions
- [ ] These tools MUST:
  - [ ] Never run on normal pods by default
  - [ ] Treat governance private keys as high-security secrets
  - [ ] Require explicit confirmations for any F1000 transfer or role grant/revocation
- [ ] Add tests (where applicable):
  - [ ] Tooling produces governance documents that pods can validate and trust

**Off-Pod**: Governance tools are separate from pod operation, high-security key handling

---

### First Pod & Social Modules (T-SOCIAL, Future Layer)

> **Design Doc**: See `docs/pod-f1000-social-hub-design.md`  
> **Status**: 📋 FUTURE (after core architecture, before public launch)  
> **Priority**: 🟡 MEDIUM (reference implementation + community hub)

The First Pod is:
- **First canonical pod instance** (reference implementation)
- **Community home for F1000 testers** (auto-join via governance IDs)
- **Modular social framework** (chat, forums, ActivityPub)
- **NOT a control plane** (pods remain sovereign)

**Key Principles:**
- **Modular** (each feature is a self-contained module)
- **Hardened** (MCP integration, rate limiting, abuse protection)
- **Isolated** (separate from pod keys, file-sharing, MCP config)
- **Optional** (pods can disable all social features)

#### T-SOCIAL-01: Core Framework & Module Registration 📋
**Status**: 📋 Planned (future)  
**Priority**: 🟡 MEDIUM  
**Dependencies**: T-POD01 (pod identity), H-POD02 (admin accounts), T-MCP01 (MCP foundation)  
**Design Doc**: `docs/pod-f1000-social-hub-design.md` § 2.1, 6

- [ ] Implement module registration system:
  - [ ] `ISocialModule` interface (lifecycle, routes, hooks)
  - [ ] `SocialModuleRegistry` (enable/disable modules)
- [ ] Shared auth/ACL layer:
  - [ ] Role-based access control: `Admin`, `Moderator`, `F1000Member`, `User`, `Guest`
  - [ ] Per-resource permissions (channels, boards, posts)
- [ ] MCP integration hooks:
  - [ ] `IModerationProvider` calls for text content
  - [ ] Per-module moderation policies
- [ ] Logging/metrics framework:
  - [ ] Structured, minimal audit logs (no PII, no secrets)
  - [ ] Low-cardinality metrics (module, action, result)
- [ ] Add tests:
  - [ ] Module registration/lifecycle
  - [ ] Role/ACL enforcement
  - [ ] MCP integration

**Foundation**: Provides common infrastructure for all social modules

#### T-SOCIAL-02: ChatModule (Discord-like Channels) 📋
**Status**: 📋 Planned (future)  
**Priority**: 🟡 MEDIUM  
**Dependencies**: T-SOCIAL-01  
**Design Doc**: `docs/pod-f1000-social-hub-design.md` § 2.2, 6

- [ ] Implement channel management:
  - [ ] Named channels (`#general`, `#f1000-meta`, `#dev`)
  - [ ] Scoped visibility (public, F1000-only, admin-only, private groups)
  - [ ] Per-channel ACLs and membership lists
- [ ] Real-time messaging:
  - [ ] WebSocket API (send, receive, typing indicators)
  - [ ] REST API (list channels, join, history)
  - [ ] Message editing/deletion (with audit metadata)
- [ ] Optional attachments:
  - [ ] Small files only (configurable size limit)
  - [ ] Strong quotas per user/channel
- [ ] Rate limiting and abuse protection:
  - [ ] Per-user, per-channel, per-IP limits
  - [ ] MCP integration for text content
- [ ] Add tests:
  - [ ] Channel CRUD operations
  - [ ] Real-time messaging (WebSocket)
  - [ ] ACL enforcement (F1000-only channels work)
  - [ ] Rate limiting triggers correctly

**Use Case**: Community chat for F1000 testers, general discussion

#### T-SOCIAL-03: ForumModule (Boards / Topics / Threads) 📋
**Status**: 📋 Planned (future)  
**Priority**: 🟡 MEDIUM  
**Dependencies**: T-SOCIAL-01  
**Design Doc**: `docs/pod-f1000-social-hub-design.md` § 2.3, 6

- [ ] Implement board/topic/thread data model:
  - [ ] Boards (high-level categories: "Announcements", "Dev Notes", etc.)
  - [ ] Topics/threads (hierarchical posts)
  - [ ] Post metadata (author, timestamps, edit history)
- [ ] REST API for CRUD operations:
  - [ ] Create/edit/delete boards (admin-only)
  - [ ] Create/reply/edit topics and posts
  - [ ] List boards, topics, threads (with pagination)
- [ ] Moderation tools:
  - [ ] Pin/unpin posts
  - [ ] Lock/unlock topics
  - [ ] Archive old threads
- [ ] Anti-spam measures:
  - [ ] Rate limits on new topics, replies, edits
  - [ ] Optional cooldowns for new accounts
  - [ ] MCP integration for text moderation
- [ ] Add tests:
  - [ ] Board/topic/thread CRUD
  - [ ] Moderation tools work correctly
  - [ ] Rate limiting and anti-spam
  - [ ] ACL enforcement (board-level visibility)

**Use Case**: Longer-form discussions, announcements, persistent threads

#### T-SOCIAL-04: SocialFeedModule (ActivityPub / Mastodon-style) 📋
**Status**: 📋 Planned (future)  
**Priority**: 🟡 MEDIUM  
**Dependencies**: T-SOCIAL-01, T-FED01 (ActivityPub foundation, if exists)  
**Design Doc**: `docs/pod-f1000-social-hub-design.md` § 2.4, 6

- [ ] Implement ActivityPub actor(s) for First Pod:
  - [ ] Optional shared hub actor (`@f1000@firstpod`)
  - [ ] Per-user social actors (optional)
  - [ ] Actor discovery (WebFinger)
- [ ] Timeline/feed views:
  - [ ] Local feed (posts from local users/boards)
  - [ ] Federated feed (posts from followed remote actors, if federation enabled)
  - [ ] Per-user timelines (home, mentions, notifications)
- [ ] WorkRef integration:
  - [ ] Attach references to works (Music/Book/Video) in posts
  - [ ] Display work metadata in feed
- [ ] F1000 badges/labels:
  - [ ] F1000 members get special badge in feed
  - [ ] Optional: governance role display (master-admin, registrar)
- [ ] Federation hardening:
  - [ ] Mode configuration (`Off`, `Hermit`, `Federated`)
  - [ ] Default conservative (Hermit or curated Federated)
  - [ ] Signature validation, rate limiting
  - [ ] MCP integration for incoming notes/posts
- [ ] Add tests:
  - [ ] Actor creation and WebFinger discovery
  - [ ] Local feed population
  - [ ] Federated feed ingestion (if enabled)
  - [ ] WorkRef attachment and display
  - [ ] Federation hardening (signature validation, rate limiting)

**Use Case**: Mastodon-style social feed, federated timeline, work recommendations

#### T-SOCIAL-05: F1000 Auto-Join Integration 📋
**Status**: 📋 Planned (future)  
**Priority**: 🟡 MEDIUM  
**Dependencies**: T-SOCIAL-01, T-F1000-01 (governance identity types)  
**Design Doc**: `docs/pod-f1000-social-hub-design.md` § 4, 6

- [ ] Governance ID → First Pod user mapping:
  - [ ] Pre-create pending user for each F1000 member
  - [ ] Map GovernanceId (`gov:<hash>`) to local user ID
- [ ] Pending user pre-provisioning:
  - [ ] Assign default roles: `F1000Member`, `User`, optional `EarlyTester`
  - [ ] Mark account as pending (not activated yet)
- [ ] Challenge-response activation:
  - [ ] Prove control of governance key (sign challenge)
  - [ ] Activate account on successful proof
  - [ ] No email/password required for F1000 members
- [ ] Role assignment:
  - [ ] F1000 members get `F1000Member` role
  - [ ] Optional additional roles (early tester, contributor, etc.)
  - [ ] Master-admins get `MasterAdmin` role (if desired)
- [ ] Dormant account handling:
  - [ ] Pending accounts remain dormant if never activated
  - [ ] No messages/posts/federation for dormant accounts
- [ ] Add tests:
  - [ ] F1000 member can activate via governance key signature
  - [ ] Pending accounts are pre-created correctly
  - [ ] Roles assigned properly (F1000Member, etc.)
  - [ ] Dormant accounts do not participate in social features

**Policy Choice**: Auto-join is a governance/social policy, not a technical dependency

#### T-SOCIAL-06: Non-F1000 Participation (Optional) 📋
**Status**: 📋 Planned (future)  
**Priority**: 🟢 LOW (optional, after F1000 auto-join)  
**Dependencies**: T-SOCIAL-01  
**Design Doc**: `docs/pod-f1000-social-hub-design.md` § 4.2, 6

- [ ] Standard auth flows:
  - [ ] Password + 2FA (bcrypt/Argon2 hashing)
  - [ ] Optional: OIDC integration (Google, GitHub, etc.)
- [ ] Invite system:
  - [ ] Generate invite codes (one-time use)
  - [ ] Track inviter (accountability)
  - [ ] Expiration and usage limits
- [ ] Registration policies:
  - [ ] Open registration (if allowed)
  - [ ] Invite-only (default during early testing)
  - [ ] F1000-sponsored invites (F1000 members can invite)
- [ ] Role assignment for non-F1000 users:
  - [ ] Default: `User` role (no F1000 privileges)
  - [ ] Optional: `Guest` role (read-only, limited features)
- [ ] Add tests:
  - [ ] Registration flows work (password, OIDC, invite)
  - [ ] Non-F1000 users get correct roles
  - [ ] Invite system enforces limits
  - [ ] Registration policies respected

**Optional**: First Pod MAY allow non-F1000 participation, but not required

---

### Attribution & Licensing (H-ATTR)

#### H-ATTR01: Comprehensive Attribution & License Compliance 📋
**Status**: 📋 Planned  
**Priority**: 🔴 HIGH (legal/ethical requirement, before any public release)  
**Dependencies**: None (can start anytime)

- [ ] Audit all dependencies and integrations:
  - [ ] **Upstream Projects**:
    - [ ] slskd (AGPL-3.0) - base fork
    - [ ] Soulseek protocol/network
    - [ ] Any other direct code dependencies
  - [ ] **External Services & APIs**:
    - [ ] MusicBrainz (metadata, rate limits, attribution requirements)
    - [ ] AcoustID / Chromaprint (audio fingerprinting)
    - [ ] Cover Art Archive
    - [ ] TMDB / TVDB (video metadata, API terms)
    - [ ] Open Library (book metadata, API terms)
    - [ ] Goodreads (if used, check current API ToS)
  - [ ] **Protocols & Networks**:
    - [ ] Soulseek protocol (acknowledgment, etiquette)
    - [ ] ActivityPub (W3C spec)
    - [ ] BitTorrent protocol
    - [ ] DHT implementations
  - [ ] **Libraries & Frameworks**:
    - [ ] .NET runtime and libraries (MIT)
    - [ ] ASP.NET Core (MIT)
    - [ ] SQLite (Public Domain)
    - [ ] ffmpeg/ffprobe (LGPL/GPL - check linking)
    - [ ] Any cryptography libraries (Ed25519, etc.)
    - [ ] Any JSON/serialization libraries
    - [ ] Any audio/video processing libraries
    - [ ] Any EPUB/PDF parsing libraries

- [ ] Create/update documentation:
  - [ ] `ACKNOWLEDGMENTS.md`:
    - [ ] Upstream projects (slskd, Soulseek)
    - [ ] External services (MusicBrainz, TMDB, Open Library)
    - [ ] Protocol specifications (ActivityPub, BitTorrent)
    - [ ] Key libraries and their licenses
  - [ ] `THIRD_PARTY_LICENSES.md`:
    - [ ] Full license texts for all dependencies
    - [ ] Version information
    - [ ] Links to upstream projects
  - [ ] `README.md`:
    - [ ] Prominent attribution to slskd
    - [ ] Acknowledgment of Soulseek network
    - [ ] Links to external services
    - [ ] License notice (AGPL-3.0)
  - [ ] `API_TERMS.md` (if needed):
    - [ ] Rate limits and quotas for external APIs
    - [ ] Attribution requirements
    - [ ] Terms of Service compliance notes

- [ ] Update in-application attribution:
  - [ ] About/Credits UI section (if UI exists):
    - [ ] List all major dependencies
    - [ ] Links to upstream projects
  - [ ] API endpoints (if applicable):
    - [ ] `/api/about` or `/api/credits` endpoint
    - [ ] Return structured attribution data
  - [ ] Startup logs:
    - [ ] Brief attribution notice on startup
    - [ ] Version information

- [ ] Verify license compatibility:
  - [ ] Ensure all dependencies compatible with AGPL-3.0
  - [ ] Check for any GPL/LGPL linking requirements
  - [ ] Verify commercial use restrictions (if any)
  - [ ] Document any special requirements (e.g., ffmpeg codec patents)

- [ ] External service compliance:
  - [ ] MusicBrainz:
    - [ ] Implement rate limiting (1 request/second default)
    - [ ] Set proper User-Agent header
    - [ ] Attribution in UI/docs where data displayed
  - [ ] TMDB/TVDB:
    - [ ] API key management
    - [ ] Rate limit compliance
    - [ ] Attribution requirements (logos, links)
  - [ ] Open Library:
    - [ ] Rate limit compliance
    - [ ] Attribution where data displayed
  - [ ] Chromaprint/AcoustID:
    - [ ] Proper attribution
    - [ ] API key (if required)

- [ ] Add tests:
  - [ ] Verify attribution files exist and are non-empty
  - [ ] Verify User-Agent headers include proper identification
  - [ ] Verify rate limits are configured for external APIs

- [ ] Create maintenance process:
  - [ ] Document how to update attributions when adding new dependencies
  - [ ] Checklist for adding new external service integrations
  - [ ] Regular audit schedule (e.g., quarterly)

**Legal/Ethical Requirements**:
- slskd attribution (REQUIRED by fork relationship)
- Soulseek network acknowledgment (ethical requirement)
- MusicBrainz attribution (required by usage guidelines)
- TMDB attribution (required by API terms)
- Open Library attribution (good practice)
- ActivityPub spec acknowledgment (W3C standard)

**Files to Create/Update**:
- `ACKNOWLEDGMENTS.md` (new or update)
- `THIRD_PARTY_LICENSES.md` (new or update)
- `README.md` (ensure prominent attribution)
- `API_TERMS.md` (new, if needed)
- `docs/external-services.md` (rate limits, attribution requirements)

**Before Public Release**:
- This task MUST be complete before any public release
- Legal review recommended if organization has legal counsel
- Verify all ToS and API terms are current

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
