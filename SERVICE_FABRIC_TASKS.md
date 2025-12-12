# Service Fabric Implementation Tasks

**Branch**: `experimental/whatAmIThinking`  
**Parent Branch**: `experimental/multi-source-swarm`  
**Created**: December 11, 2025  
**Status**: In Progress - T-SF01 Complete, T-SF02 In Progress

---

## ⚠️ SECURITY GATES

**CRITICAL**: Before proceeding with certain tasks, hardening requirements must be met:

- 🔒 **GATE 1**: H-01 (Gateway Auth/CSRF) MUST be implemented BEFORE T-SF04 (HTTP Gateway)
- 🔒 **GATE 2**: H-08 (Soulseek Caps) MUST be implemented BEFORE any public deployment
- 🔒 **GATE 3**: H-02 (Work Budget) SHOULD be implemented BEFORE T-SF03 (service wrappers)

**See**: `HARDENING-TASKS.md` for detailed security hardening backlog.

---

## Overview

This document outlines the phased implementation of a **generic service fabric** layer on top of the existing DHT and mesh overlay infrastructure. The goal is to create "DHT internet mode" - a privacy-conscious, security-hardened service discovery and routing layer that maintains backward compatibility with existing Soulseek protocol behavior.

**Hard Rules**:
1. Read before writing - understand existing DHT, mesh, security subsystems
2. No big rewrites - small, focused changes only
3. Backwards compatibility required
4. Security and privacy are first-class concerns
5. Code quality is non-negotiable (no AI slop)
6. No heavy new dependencies
7. Document what matters, briefly

**Security-First Development**:
- All tasks must consider abuse scenarios
- Defense in depth at every layer
- Fail secure by default
- Privacy-aware design
- Soulseek etiquette compliance

---

## Phase 1: Core Service Fabric Types & Directory (T-SF01)

**Status**: ✅ COMPLETE  
**Priority**: P0  
**Scope**: Types, directory interface, DHT integration ONLY - no routing, no HTTP gateway  
**Completed**: December 11, 2025  
**Commit**: `5ac8248b`

### Deliverables

- ✅ **T-SF01-001**: Create `MeshServiceDescriptor` and `MeshServiceEndpoint` types
  - Priority: P0
  - Notes: 
    - ServiceId: deterministic hash (`hash("svc:" + ServiceName + ":" + OwnerPeerId)`)
    - ServiceName: opaque functional label (no PII)
    - Version: simple semver
    - OwnerPeerId: existing peer identity
    - Endpoint: overlay-level addressing
    - Metadata: capped size (max 10 entries, max 4KB serialized)
    - Signature: Ed25519, tied to owner key
    - CreatedAt/ExpiresAt: time window validation

- [ ] **T-SF01-002**: Implement `IMeshServiceDirectory` interface
  - Priority: P0
  - Notes:
    ```csharp
    public interface IMeshServiceDirectory
    {
        Task<IReadOnlyList<MeshServiceDescriptor>> FindByNameAsync(
            string serviceName,
            CancellationToken cancellationToken = default);
        
        Task<IReadOnlyList<MeshServiceDescriptor>> FindByIdAsync(
            string serviceId,
            CancellationToken cancellationToken = default);
    }
    ```

- [ ] **T-SF01-003**: Implement DHT-backed service directory
  - Priority: P0
  - Notes:
    - Use existing DHT client
    - DHT key pattern: `svc:<ServiceName>`
    - Enforce max descriptor count per result (clamp to 20)
    - Validate signatures, timestamps, sizes
    - Filter expired/invalid/banned peers
    - Integrate with existing reputation system

- [ ] **T-SF01-004**: Implement service descriptor validation
  - Priority: P0
  - Notes:
    - Signature validation (Ed25519)
    - Time window check (CreatedAt ≤ now ≤ ExpiresAt + 5min skew)
    - Metadata size limits
    - No PII in ServiceName or Metadata
    - Ban list integration from SecurityCore

- [ ] **T-SF01-005**: Implement service publisher background service
  - Priority: P0
  - Notes:
    - Periodic publishing of local services
    - TTL-based republishing
    - Rate limiting (no DHT flooding)
    - Size threshold enforcement
    - Integration with existing DHT rate limiter

- [ ] **T-SF01-006**: Add ServiceId derivation unit tests
  - Priority: P0
  - Notes:
    - Same inputs → same ID
    - Small variations → different ID
    - Collision resistance tests

- [ ] **T-SF01-007**: Add DHT directory parsing unit tests
  - Priority: P0
  - Notes:
    - Oversized descriptors dropped
    - Invalid signatures dropped
    - Expired descriptors dropped
    - Banned peer descriptors dropped

---

## Phase 2: Service Routing & RPC Layer (T-SF02)

**Status**: 📋 Planned  
**Priority**: P1  
**Scope**: IMeshService interface, routing, abuse controls - no HTTP gateway yet  
**Dependencies**: T-SF01 complete

### Tasks

- [ ] **T-SF02-001**: Create `IMeshService` interface
  - Priority: P1
  - Notes:
    ```csharp
    public interface IMeshService
    {
        string ServiceName { get; }
        
        Task<ServiceReply> HandleCallAsync(
            ServiceCall call,
            MeshServiceContext context,
            CancellationToken cancellationToken = default);
        
        Task HandleStreamAsync(
            MeshServiceStream stream,
            MeshServiceContext context,
            CancellationToken cancellationToken = default);
    }
    ```

- [ ] **T-SF02-002**: Create `ServiceCall` and `ServiceReply` DTOs
  - Priority: P1
  - Notes:
    - ServiceCall: ServiceId, Method, CorrelationId, Payload
    - ServiceReply: CorrelationId, StatusCode, Payload
    - Enforce payload size limits (default 1MB, configurable)
    - Opaque method names (no semantic meaning)

- [ ] **T-SF02-003**: Implement central service router
  - Priority: P1
  - Notes:
    - Route incoming calls to registered IMeshService instances
    - Per-peer rate limiting
    - Per-service rate limiting
    - Concurrent call limits (default 100 per peer)
    - Payload size validation
    - Integration with existing overlay transport

- [ ] **T-SF02-004**: Implement abuse mitigation in router
  - Priority: P1
  - Notes:
    - Rate limit: 100 calls/min per peer (configurable)
    - Max payload: 1MB per call (configurable)
    - Max concurrent calls: 100 per peer (configurable)
    - Reject oversized payloads
    - Register violations with SecurityCore

- [ ] **T-SF02-005**: Implement `IMeshServiceClient` interface
  - Priority: P1
  - Notes:
    ```csharp
    public interface IMeshServiceClient
    {
        Task<ServiceReply> CallAsync(
            MeshServiceDescriptor targetService,
            string method,
            ReadOnlyMemory<byte> payload,
            CancellationToken cancellationToken = default);
    }
    ```

- [ ] **T-SF02-006**: Implement client-side service client
  - Priority: P1
  - Notes:
    - Timeout enforcement (default 30s)
    - Connection pooling via existing overlay
    - Error propagation with safe messages
    - No leaky error details to clients

- [ ] **T-SF02-007**: Add service router unit tests
  - Priority: P1
  - Notes:
    - Rate limit enforcement
    - Payload size limits
    - Concurrent call limits
    - Violation registration

- [ ] **T-SF02-008**: Add service client unit tests
  - Priority: P1
  - Notes:
    - Timeout handling
    - Error propagation
    - Call/reply round-trip

---

## Phase 3: Wrap Existing Features as Services (T-SF03)

**Status**: 📋 Planned  
**Priority**: P1  
**Scope**: Migrate pods, VirtualSoulfind, stats to service layer  
**Dependencies**: T-SF02 complete

### Tasks

- [ ] **T-SF03-001**: Wrap Pod/Chat as `IMeshService`
  - Priority: P1
  - Notes:
    - Methods: Join, Leave, PostMessage, FetchMessages
    - Redirect existing overlay handlers
    - Respect existing guard/reputation logic
    - Message size limits (4KB default)
    - Frequency limits (10 msgs/min per peer)

- [ ] **T-SF03-002**: Wrap VirtualSoulfind/Shadow Index as `IMeshService`
  - Priority: P1
  - Notes:
    - Methods: RegisterTrack, LookupByMbId, QueryShard
    - Keep existing DHT keyspace unchanged
    - Registration frequency limits (100/hour per peer)
    - MBID validation

- [ ] **T-SF03-003**: Create Mesh Stats/Introspection service
  - Priority: P2
  - Notes:
    - Methods: GetMeshStatus, GetPeersSummary
    - Read-only introspection
    - No internal hostnames/usernames leaked
    - Aggregate stats only

- [ ] **T-SF03-004**: Add integration tests for wrapped services
  - Priority: P1
  - Notes:
    - Test service discovery
    - Test pod join/leave via service layer
    - Test shadow index via service layer
    - Test stats via service layer

---

## Phase 4: Local HTTP/WebSocket Gateway (T-SF04)

**Status**: 📋 Planned  
**Priority**: P1  
**Scope**: HTTP gateway for external apps, optional WS gateway  
**Dependencies**: T-SF03 complete

### Tasks

- [ ] **T-SF04-001**: Create HTTP gateway controller
  - Priority: P1
  - Notes:
    - Routes: `GET/POST /mesh/http/{serviceName}/{**path}`
    - Bind to localhost only (default)
    - Configurable enable/disable
    - Service name whitelist

- [ ] **T-SF04-002**: Implement service resolution in gateway
  - Priority: P1
  - Notes:
    - Use IMeshServiceDirectory to find services
    - Pick descriptor (random or highest reputation)
    - Return 503 if no services available

- [ ] **T-SF04-003**: Implement request/response mapping
  - Priority: P1
  - Notes:
    - HTTP method/path/headers → ServiceCall payload
    - ServiceReply → HTTP response
    - No full error stack traces exposed
    - Log full errors server-side only

- [ ] **T-SF04-004**: Add gateway security gating
  - Priority: P1
  - Notes:
    - Reject if gateway disabled
    - Reject if service not whitelisted
    - No request body logging by default (opt-in)

- [ ] **T-SF04-005**: Implement WebSocket gateway (optional)
  - Priority: P2
  - Notes:
    - Route: `GET /mesh/ws/{serviceName}/{channel}`
    - Long-lived overlay stream
    - Bidirectional frame bridging
    - Only if clean with existing infra

- [ ] **T-SF04-006**: Add gateway configuration options
  - Priority: P1
  - Notes:
    - Enable/disable gateway
    - Allowed service names (whitelist)
    - Verbose logging (opt-in, default off)
    - Port binding (default localhost:5030)

- [ ] **T-SF04-007**: Add gateway unit tests
  - Priority: P1
  - Notes:
    - Request mapping
    - Response mapping
    - Security gating
    - Service resolution

- [ ] **T-SF04-008**: Add gateway integration tests
  - Priority: P1
  - Notes:
    - End-to-end HTTP → mesh service call
    - Error handling
    - Timeout handling

---

## Phase 5: Security & Abuse Integration (T-SF05)

**Status**: 📋 Planned  
**Priority**: P0  
**Scope**: Tie service fabric into existing security subsystems  
**Dependencies**: T-SF04 complete

### Tasks

- [ ] **T-SF05-001**: Audit existing security subsystems
  - Priority: P0
  - Notes:
    - Locate guard, violation tracker, reputation storage
    - Understand ban list integration
    - Document security hook points

- [ ] **T-SF05-002**: Implement incoming call security checks
  - Priority: P0
  - Notes:
    - Check ban/quarantine status
    - Check rate limits
    - Check payload size
    - Register violations on failure

- [ ] **T-SF05-003**: Implement outgoing discovery security
  - Priority: P1
  - Notes:
    - Prefer high-reputation peers
    - Skip banned/low-reputation peers
    - Weight by reputation score

- [ ] **T-SF05-004**: Add security logging for service calls
  - Priority: P1
  - Notes:
    - Log: service name, method, peer ID, status, error category
    - No full payload logging
    - High-level security events only

- [ ] **T-SF05-005**: Add security metrics tracking
  - Priority: P2
  - Notes:
    - Rejected oversized payloads
    - Rate limit hits
    - Signature validation failures
    - Per-service violation counts

- [ ] **T-SF05-006**: Add security unit tests
  - Priority: P1
  - Notes:
    - Ban list enforcement
    - Rate limit enforcement
    - Payload size limits
    - Violation registration

---

## Phase 6: Testing & Documentation (T-SF06)

**Status**: 📋 Planned  
**Priority**: P1  
**Scope**: Comprehensive testing and documentation  
**Dependencies**: T-SF05 complete

### Tasks

- [ ] **T-SF06-001**: Add service fabric unit test suite
  - Priority: P1
  - Notes:
    - ServiceId derivation
    - Signature validation
    - DHT directory parsing
    - Rate limiting
    - Security checks

- [ ] **T-SF06-002**: Add service fabric integration tests
  - Priority: P1
  - Notes:
    - Node A publishes service
    - Node B discovers service
    - Node B calls service
    - End-to-end call flow

- [ ] **T-SF06-003**: Verify backward compatibility tests pass
  - Priority: P0
  - Notes:
    - All existing tests still pass
    - No regressions in Soulseek protocol
    - No regressions in DHT/mesh/security

- [ ] **T-SF06-004**: Write service fabric architecture doc
  - Priority: P1
  - Notes:
    - Overview of service fabric layer
    - DHT key patterns
    - Security model
    - Privacy considerations

- [ ] **T-SF06-005**: Write service fabric API documentation
  - Priority: P2
  - Notes:
    - IMeshServiceDirectory usage
    - IMeshService implementation guide
    - HTTP gateway usage
    - Configuration options

- [ ] **T-SF06-006**: Write service fabric security guide
  - Priority: P1
  - Notes:
    - Threat model
    - Security constraints
    - Privacy guarantees
    - Recommended configurations

---

## Anti-Slop Checklist

Run this before each commit:

1. **Diff Scope**
   - [ ] Changes limited to task requirements
   - [ ] No random reformatting or renaming
   - [ ] Focused, meaningful diffs

2. **Async Correctness**
   - [ ] No `.Result` / `.Wait()` in hot paths
   - [ ] CancellationToken plumbed through
   - [ ] ConfigureAwait(false) used appropriately

3. **Error Handling & Logging**
   - [ ] Network/IO calls in try/catch
   - [ ] Clear logs, safe responses
   - [ ] No leaking stack traces outward
   - [ ] No swallowed exceptions

4. **Performance & Allocations**
   - [ ] No heavy LINQ in critical loops
   - [ ] No repeated allocations in hot paths
   - [ ] Payload size limits enforced

5. **Security & Privacy**
   - [ ] No PII leaks in descriptors/logs
   - [ ] DHT data validated (size, signature, timestamps)
   - [ ] DoS controls added for new vectors

6. **Consistency**
   - [ ] Naming/style matches project
   - [ ] DI patterns followed
   - [ ] Config patterns followed
   - [ ] Crypto/logging/serialization helpers reused

7. **Documentation**
   - [ ] New public types have XML docs
   - [ ] Security/privacy rules commented
   - [ ] New config options documented

---

## Progress Tracking

| Phase | Tasks | Complete | Status |
|-------|-------|----------|--------|
| T-SF01 | 7 | 0 | 📋 Planned |
| T-SF02 | 8 | 0 | 📋 Planned |
| T-SF03 | 4 | 0 | 📋 Planned |
| T-SF04 | 8 | 0 | 📋 Planned |
| T-SF05 | 6 | 0 | 📋 Planned |
| T-SF06 | 6 | 0 | 📋 Planned |
| **Total** | **39** | **0** | **0%** |

---

## Dependencies

### Existing Infrastructure Required
- ✅ DHT client (from multi-source-swarm branch)
- ✅ Overlay transport (QUIC/UDP with TLS)
- ✅ Ed25519 signing (for descriptors)
- ✅ SecurityCore (guard, violations, reputation, ban lists)
- ✅ PeerMetricsService (for reputation-aware discovery)

### New Infrastructure to Build
- MeshServiceDescriptor types
- IMeshServiceDirectory interface
- DHT-backed service directory
- Service router with abuse controls
- IMeshServiceClient
- HTTP/WebSocket gateway
- Security integration

---

## Success Criteria

### Phase 1 (T-SF01) Complete When:
- [ ] MeshServiceDescriptor types exist with validation
- [ ] IMeshServiceDirectory interface defined
- [ ] DHT-backed directory implementation works
- [ ] Unit tests pass for ID derivation and validation
- [ ] No existing functionality broken

### Phase 2 (T-SF02) Complete When:
- [ ] IMeshService interface defined
- [ ] Service router functional with abuse controls
- [ ] IMeshServiceClient can make calls
- [ ] Unit tests pass for routing and limits
- [ ] Integration test: Node A → Node B service call works

### Phase 3 (T-SF03) Complete When:
- [ ] Pods/chat accessible via service layer
- [ ] VirtualSoulfind accessible via service layer
- [ ] Stats/introspection service working
- [ ] Existing functionality still works via old paths
- [ ] Integration tests pass

### Phase 4 (T-SF04) Complete When:
- [ ] HTTP gateway accepts requests
- [ ] Service resolution working
- [ ] Request/response mapping correct
- [ ] Security gating enforced
- [ ] Integration test: HTTP → mesh service works

### Phase 5 (T-SF05) Complete When:
- [ ] Ban list integration working
- [ ] Rate limits enforced
- [ ] Reputation-aware discovery working
- [ ] Security violations registered correctly
- [ ] Metrics tracking implemented

### Phase 6 (T-SF06) Complete When:
- [ ] All unit tests pass
- [ ] All integration tests pass
- [ ] Backward compatibility verified
- [ ] Architecture doc written
- [ ] Security guide written
- [ ] Ready for production use

---

*Last Updated: December 11, 2025*  
*Branch: experimental/whatAmIThinking*  
*Parent: experimental/multi-source-swarm*
