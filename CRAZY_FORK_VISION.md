# CRAZY FORK VISION: Service Fabric & DHT Internet Mode

**Branch**: `experimental/whatAmIThinking`  
**Created**: December 11, 2025  
**Status**: 🔬 Research → Implementation Planning

---

## Executive Summary

**What We're Building**: A generic, privacy-conscious **service fabric** layer on top of the existing DHT and mesh overlay infrastructure. Think "DHT internet mode" - a decentralized service discovery and routing system that enables:

1. **Service Discovery**: Peers publish and discover services via DHT
2. **Service Routing**: Generic RPC/streaming calls routed over mesh overlay
3. **HTTP Gateway**: External apps can use mesh services via localhost HTTP API
4. **Backward Compatibility**: All existing Soulseek/mesh/pod functionality keeps working

**Why This Matters**: 
- Turns slskdn from a music-focused client into a **generic p2p service platform**
- Enables external apps to leverage the mesh without embedding the client
- Foundation for future domain-specific apps (music, video, books, etc.)
- Preserves privacy/security hardening from multi-source-swarm branch

**Hard Constraints**:
- Security and privacy are first-class, not bolted-on
- No breaking changes to existing functionality
- No AI slop: quality, testability, maintainability enforced
- Minimal new dependencies

---

## The Problem

**Current State** (multi-source-swarm branch):
- ✅ DHT for peer discovery, content mapping, pods, rendezvous
- ✅ Mesh overlay with TLS-encrypted P2P connections
- ✅ Security: guard, violations, reputation, ban lists
- ✅ Multi-source chunked downloads with rescue mode
- ✅ Shadow index for disaster mode fallback

**What's Missing**:
- No generic service abstraction - everything is hard-coded
- Pod chat, VirtualSoulfind, stats are all separate systems
- External apps must embed slskdn or use limited Soulseek-compat API
- No way to add new service types without core changes

---

## The Vision: Service Fabric Layer

### Core Concepts

#### 1. MeshServiceDescriptor

Services are first-class entities published to DHT:

```csharp
public sealed class MeshServiceDescriptor
{
    public string ServiceId { get; init; }         // hash("svc:" + Name + ":" + Owner)
    public string ServiceName { get; init; }       // "pod-chat", "shadow-index", "stats"
    public string Version { get; init; }           // "1.0.0"
    public string OwnerPeerId { get; init; }       // existing peer identity
    
    public MeshServiceEndpoint Endpoint { get; init; }
    public IReadOnlyDictionary<string, string> Metadata { get; init; }
    
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    
    public byte[] Signature { get; init; }         // Ed25519 signature
}
```

**Privacy/Security Constraints**:
- ServiceId: deterministic, stable hash
- ServiceName: functional label, no PII
- Metadata: capped size (10 entries, 4KB max), no PII
- Signature: validates ownership, prevents spoofing
- Time window: prevent replay attacks

#### 2. Service Discovery via DHT

Services published to DHT with pattern `svc:<ServiceName>`:

```
DHT Key: svc:pod-chat
DHT Value: [
  MeshServiceDescriptor { ServiceId: "abc123...", OwnerPeerId: "peer:xyz...", ... },
  MeshServiceDescriptor { ServiceId: "def456...", OwnerPeerId: "peer:uvw...", ... }
]
```

**Hardening**:
- Max descriptors per result: 20
- Signature validation required
- Timestamp validation (no expired/future records)
- Ban list integration (filter out bad peers)
- Reputation scoring (prefer high-rep peers)

#### 3. Service Interface & Routing

Generic service interface:

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

**Abuse Controls**:
- Per-peer rate limits (100 calls/min default)
- Per-service rate limits (configurable)
- Max payload size (1MB default)
- Max concurrent calls (100 per peer default)
- Signature validation on all calls
- Integration with SecurityCore for violations

#### 4. Local HTTP/WebSocket Gateway

External apps access mesh services via localhost:

```
GET  /mesh/http/pod-chat/channels
POST /mesh/http/shadow-index/lookup?mbid=<mbid>
GET  /mesh/ws/pod-chat/general (WebSocket)
```

**Security Gating**:
- Bind to localhost only (default)
- Enable/disable per config
- Service name whitelist
- No request body logging (opt-in verbose mode)

---

## Architecture Layers

```
┌─────────────────────────────────────────────────────────┐
│           External Apps (Soulbeet, etc.)                │
└────────────────────┬────────────────────────────────────┘
                     │ HTTP/WS (localhost)
┌────────────────────▼────────────────────────────────────┐
│              Local HTTP/WS Gateway                       │
│  • localhost:5030                                        │
│  • Service name whitelist                                │
│  • Request/response mapping                              │
└────────────────────┬────────────────────────────────────┘
                     │
┌────────────────────▼────────────────────────────────────┐
│              Service Router & RPC Layer                  │
│  • IMeshService registry                                 │
│  • Rate limiting (100 calls/min per peer)                │
│  • Payload size limits (1MB default)                     │
│  • Concurrent call limits (100 per peer)                 │
│  • Violation tracking                                    │
└─────┬──────────────────────────────────────────┬────────┘
      │                                          │
┌─────▼─────────────────┐          ┌────────────▼────────┐
│ IMeshServiceDirectory │          │  IMeshServiceClient │
│ • FindByName          │          │  • CallAsync        │
│ • FindById            │          │  • Timeout handling │
│ • Reputation filtering│          │  • Connection pool  │
└─────┬─────────────────┘          └─────────────────────┘
      │
┌─────▼─────────────────────────────────────────────────┐
│              DHT Layer (existing)                      │
│  • svc:<ServiceName> keys                              │
│  • Signature validation                                │
│  • TTL-based expiry                                    │
│  • Rate limiting                                       │
└─────┬─────────────────────────────────────────────────┘
      │
┌─────▼─────────────────────────────────────────────────┐
│         Mesh Overlay Transport (existing)              │
│  • QUIC/UDP with TLS                                   │
│  • Certificate pinning                                 │
│  • Control messages                                    │
└───────────────────────────────────────────────────────┘
```

---

## Migration Path: Existing Features → Service Layer

### Phase 3 Goals: Wrap Without Breaking

#### 3.1. Pod/Chat as Service

```csharp
public class PodChatService : IMeshService
{
    public string ServiceName => "pod-chat";
    
    public async Task<ServiceReply> HandleCallAsync(ServiceCall call, ...)
    {
        switch (call.Method)
        {
            case "Join":
                return await HandleJoinAsync(call);
            case "Leave":
                return await HandleLeaveAsync(call);
            case "PostMessage":
                return await HandlePostMessageAsync(call);
            case "FetchMessages":
                return await HandleFetchMessagesAsync(call);
            default:
                return ServiceReply.Error(400, "Unknown method");
        }
    }
}
```

**Integration**:
- Existing overlay message handlers redirect through service layer internally
- Guard/reputation logic preserved
- Message size limits enforced (4KB default)
- Frequency limits enforced (10 msgs/min per peer)

#### 3.2. VirtualSoulfind/Shadow Index as Service

```csharp
public class ShadowIndexService : IMeshService
{
    public string ServiceName => "shadow-index";
    
    public async Task<ServiceReply> HandleCallAsync(ServiceCall call, ...)
    {
        switch (call.Method)
        {
            case "RegisterTrack":
                return await HandleRegisterAsync(call);
            case "LookupByMbId":
                return await HandleLookupAsync(call);
            case "QueryShard":
                return await HandleQueryAsync(call);
            default:
                return ServiceReply.Error(400, "Unknown method");
        }
    }
}
```

**Integration**:
- Keep existing DHT keyspace and data format unchanged
- Add service facade on top
- Registration frequency limits (100/hour per peer)
- MBID validation before DHT writes

#### 3.3. Mesh Stats/Introspection Service

```csharp
public class MeshStatsService : IMeshService
{
    public string ServiceName => "mesh-stats";
    
    public async Task<ServiceReply> HandleCallAsync(ServiceCall call, ...)
    {
        switch (call.Method)
        {
            case "GetMeshStatus":
                return await GetStatusAsync();
            case "GetPeersSummary":
                return await GetPeersAsync();
            default:
                return ServiceReply.Error(400, "Unknown method");
        }
    }
}
```

**Privacy Constraints**:
- Read-only introspection
- Aggregate stats only
- No internal hostnames/OS usernames exposed
- No raw IP addresses exposed

---

## Security & Privacy Model

### Threat Model

**Adversaries**:
1. **Malicious Peers**:
   - Send malformed service descriptors
   - Spam service calls (DoS)
   - Try correlation attacks via timing/metadata
   - DHT poisoning (bad service records)

2. **Passive Observers**:
   - Monitor DHT queries (what services you're looking for)
   - Traffic analysis (timing, size patterns)

3. **Active Network Attackers**:
   - MITM attempts (mitigated by TLS)
   - Replay attacks (mitigated by signatures + timestamps)

### Defense Layers

#### Layer 1: DHT Validation
- ✅ Signature validation (Ed25519) on all service descriptors
- ✅ Timestamp validation (no expired/future records)
- ✅ Size limits (max 20 descriptors per DHT result)
- ✅ Ban list integration (filter bad peers)
- ✅ Rate limiting (max N DHT writes per interval)

#### Layer 2: Service Router Guards
- ✅ Per-peer rate limiting (100 calls/min default)
- ✅ Per-service rate limiting (configurable)
- ✅ Payload size limits (1MB default)
- ✅ Concurrent call limits (100 per peer)
- ✅ Violation tracking (feed SecurityCore)

#### Layer 3: Transport Security
- ✅ TLS 1.3 for all mesh connections (existing)
- ✅ Certificate pinning (existing)
- ✅ Encrypted control messages (existing)

#### Layer 4: Privacy
- ✅ No PII in service descriptors
- ✅ Opaque service names (functional, not semantic)
- ✅ Minimal metadata (capped size)
- ✅ No full payload logging (opt-in verbose mode)
- ✅ Aggregate stats only (no per-peer details exposed via stats service)

### Logging Policy

**DO Log**:
- Service name, method, peer ID, status, error category
- Security events (violations, rate limits, signature failures)
- High-level metrics (calls/sec, avg latency)

**DO NOT Log**:
- Full request/response payloads
- Private keys or secrets
- Raw IP addresses (use peer IDs)
- Internal hostnames/usernames

---

## Implementation Strategy

### Phase 1: Core Types & Directory (T-SF01)
**Scope**: Types, DHT integration ONLY - no routing yet

1. Create `MeshServiceDescriptor` and `MeshServiceEndpoint` types
2. Implement `IMeshServiceDirectory` interface
3. Build DHT-backed directory with validation
4. Add unit tests for ID derivation and validation

**Success Criteria**:
- Can publish a service descriptor to DHT
- Can query DHT for service by name
- Invalid/expired/banned descriptors filtered out
- All validation tests pass

### Phase 2: Routing & RPC (T-SF02)
**Scope**: Service routing with abuse controls - no HTTP gateway yet

1. Create `IMeshService` interface
2. Implement central service router
3. Add abuse controls (rate limits, size limits)
4. Implement `IMeshServiceClient`
5. Add unit and integration tests

**Success Criteria**:
- Node A can register a service
- Node B can discover and call that service
- Rate limits enforced correctly
- Violations tracked in SecurityCore

### Phase 3: Wrap Existing Features (T-SF03)
**Scope**: Migrate pods, VirtualSoulfind, stats to service layer

1. Wrap pod/chat as `IMeshService`
2. Wrap VirtualSoulfind/shadow index as `IMeshService`
3. Create mesh stats/introspection service
4. Verify existing functionality still works

**Success Criteria**:
- Pod join/leave works via service layer
- Shadow index lookups work via service layer
- Stats queries work via service layer
- Old code paths still functional (no breaking changes)

### Phase 4: HTTP/WS Gateway (T-SF04)
**Scope**: Local gateway for external apps

1. Create HTTP gateway controller
2. Implement service resolution
3. Build request/response mapping
4. Add security gating
5. Optionally add WebSocket gateway

**Success Criteria**:
- External app can call mesh service via HTTP
- Service resolution works
- Security gating enforced
- Integration tests pass

### Phase 5: Security Integration (T-SF05)
**Scope**: Full SecurityCore integration

1. Audit existing security subsystems
2. Implement incoming call security checks
3. Implement outgoing discovery security
4. Add security logging and metrics

**Success Criteria**:
- Ban list integration working
- Reputation-aware discovery working
- Security violations registered correctly
- All security tests pass

### Phase 6: Testing & Documentation (T-SF06)
**Scope**: Comprehensive testing and docs

1. Full unit test suite
2. Full integration test suite
3. Backward compatibility verification
4. Architecture documentation
5. Security guide

**Success Criteria**:
- All tests pass
- Backward compatibility verified
- Documentation complete
- Ready for production use

---

## Iteration Strategy: One Slice at a Time

**CRITICAL**: Do NOT ask an LLM to implement the entire vision in one go.

### Recommended Approach:

1. **Start with T-SF01 only**:
   - Paste the hardened brief
   - Add strict instructions: "Only implement T-SF01. Do not touch routing, gateway, or other subsystems."
   - Manually review diff before accepting
   - Commit as: `feat: add service descriptor and directory (T-SF01)`

2. **Then T-SF02**:
   - New agent session
   - "Only implement T-SF02. Do not touch gateway or wrapped services."
   - Review, test, commit

3. **Then T-SF03**, **T-SF04**, etc.

### Red Flags to Watch For:

**If the LLM does any of these, REJECT the diff**:
- Touches a ton of unrelated files
- Renames existing DHT/mesh/security types
- Implements HTTP gateway when you said "types only"
- Creates helper scripts or workarounds
- Has obvious bugs or incomplete error handling

### Recovery Strategy:

If diff is wrong:
```bash
git reset --hard HEAD  # Nuclear option
# Or: Copy out the good bits, reset, paste back in
```

---

## Success Metrics

### Technical Metrics
- [ ] All existing tests pass (backward compatibility)
- [ ] New service fabric tests pass (39 new tasks)
- [ ] Security policies enforced correctly
- [ ] Performance acceptable (latency < 100ms for local service calls)

### Quality Metrics
- [ ] No AI slop (random helpers, dead code, TODO trash)
- [ ] Diff sizes reasonable (< 1000 lines per task)
- [ ] Code coverage > 80% for new code
- [ ] Documentation complete (architecture, API, security)

### Functional Metrics
- [ ] External app can discover and call mesh services
- [ ] Pod chat accessible via HTTP gateway
- [ ] Shadow index accessible via HTTP gateway
- [ ] Stats/introspection accessible via HTTP gateway

---

## Future Directions

Once the service fabric is stable, this enables:

### Near-Term (Phases 7-8)
1. **Soulbeet Integration**: Music app uses mesh services via HTTP gateway
2. **More Services**: Scene chat, warm cache queries, playback-aware streaming
3. **Service Versioning**: Multiple versions of same service coexist

### Long-Term (Phases 9+)
1. **Domain-Specific Apps**: Video, books, podcasts (all using same service fabric)
2. **Federation**: Bridge to other p2p networks (IPFS, Tor hidden services)
3. **Mobile Gateway**: iOS/Android apps connect to local slskdn gateway

---

## Why This Is "Crazy" (And Why It Works)

### The Crazy Part:
- Turning a music client into a generic p2p service platform
- Building "DHT internet mode" on top of a Soulseek fork
- Aiming for production-grade security from day one

### Why It Works:
- ✅ Foundation is solid (DHT, overlay, security already battle-tested)
- ✅ Incremental approach (6 small phases, not one big rewrite)
- ✅ Backward compatibility enforced (no breaking changes)
- ✅ Security/privacy baked in (not bolted on)
- ✅ Quality enforced (anti-slop checklist per commit)

### Why It's Worth It:
- Unlocks slskdn as a **platform**, not just a client
- Enables external apps without embedding the client
- Foundation for multi-domain future (music, video, books)
- Proves the decentralized service fabric concept

---

## Call to Action

**Next Steps**:
1. ✅ Read this vision doc thoroughly
2. ✅ Read `SERVICE_FABRIC_TASKS.md` for detailed task breakdown
3. ✅ Freeze current branch: `git tag pre-service-fabric-$(date +%Y%m%d)`
4. 📋 Start with T-SF01 only (types & directory)
5. 📋 Manually review diff before accepting
6. 📋 Run tests
7. 📋 Commit: `feat: add service descriptor and directory (T-SF01)`
8. 📋 Repeat for T-SF02, T-SF03, etc.

**Remember**:
- Read before writing
- Small, focused changes
- Review diffs manually
- Run anti-slop checklist
- Security first, always

---

*Let's build the future of decentralized services, one commit at a time.*

---

*Last Updated: December 11, 2025*  
*Branch: experimental/whatAmIThinking*  
*Parent: experimental/multi-source-swarm*  
*Status: 🔬 Research → 📋 Planning → 🚀 Ready to Start*
