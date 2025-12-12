# Observability & Stats Tracking - Implementation Checklist

**Purpose**: Ensure all new systems have proper metrics/stats endpoints for monitoring and debugging.

**Last Updated**: December 11, 2025

---

## Completed (T-SF05 + H-02)

### ✅ MeshServiceRouter
- **Stats Object**: `RouterStats`
- **Metrics Included**:
  - Basic: RegisteredServiceCount, TrackedPeerCount
  - Rate Limiting: ActivePeersLastMinute, PerServiceTrackedPeers
  - Circuit Breakers: CircuitBreakers list, OpenCircuitCount
  - Work Budget: WorkBudgetMetrics (via PeerWorkBudgetTracker)
- **Access**: `router.GetStats()`
- **Exposed Via**: MeshIntrospectionService (mesh-introspect service)

### ✅ PeerWorkBudgetTracker
- **Stats Object**: `WorkBudgetMetrics`
- **Metrics Included**:
  - TotalPeersTracked
  - ActivePeersLastMinute
  - TotalWorkUnitsConsumedLastMinute
  - PeersNearQuota (>80% of quota)
- **Access**: `tracker.GetMetrics()`
- **Exposed Via**: Included in RouterStats

### ✅ DhtMeshServiceDirectory
- **Stats Object**: `DiscoveryMetrics`
- **Metrics Included**:
  - TotalPeersTracked
  - ActivePeersLastMinute
  - TotalQueriesLastMinute
  - SuspiciousPeers (enumeration/scanning detected)
- **Access**: `directory.GetDiscoveryMetrics()`
- **Exposed Via**: MeshIntrospectionService (optional, if directory is DhtMeshServiceDirectory)

### ✅ MeshServiceClient
- **Stats Object**: `ClientMetrics`
- **Metrics Included**:
  - TotalPendingCalls
  - PeersWithPendingCalls
  - MaxConcurrentCallsPerPeer
  - MaxTotalPendingCalls
- **Access**: `client.GetMetrics()`
- **Exposed Via**: Not yet exposed (TODO for T-SF07)

### ✅ MeshIntrospectionService
- **Endpoint**: `mesh-introspect` service, `GetStatus` method
- **Response Format**: JSON with nested objects
- **Sections**:
  - Status: healthy/unhealthy, uptime
  - Service stats
  - Circuit breaker summary
  - Work budget summary
  - Discovery abuse summary (if available)
- **Privacy**: No hostname, username, IPs, or filesystem paths

---

## TODO - Service Fabric Phase (T-SF06, T-SF07)

### 📋 T-SF07: Metrics and Observability
**Goal**: Expose all internal metrics via HTTP API and/or Prometheus

#### HTTP API Endpoint (Native)
**Controller**: `MeshServiceFabricController` (NEW)
**Route**: `/api/v0/mesh/fabric/stats`

**Endpoints to Add**:
```csharp
[HttpGet("stats")]
public IActionResult GetFabricStats()
{
    var routerStats = _router.GetStats();
    var clientMetrics = _client.GetMetrics();
    var discoveryMetrics = _directory.GetDiscoveryMetrics();
    
    return Ok(new
    {
        router = routerStats,
        client = clientMetrics,
        discovery = discoveryMetrics
    });
}

[HttpGet("circuit-breakers")]
public IActionResult GetCircuitBreakers()
{
    var stats = _router.GetStats();
    return Ok(stats.CircuitBreakers);
}

[HttpGet("work-budget")]
public IActionResult GetWorkBudgetStats()
{
    var stats = _router.GetStats();
    return Ok(stats.WorkBudgetMetrics);
}
```

**Security**: 
- Requires `[Authorize]` attribute
- Only expose to authenticated users
- DO NOT expose peer IDs in public endpoints (hash or truncate)

#### Prometheus Metrics (Optional)
If Prometheus integration exists, add:
```
# Rate limiting
slskd_mesh_rate_limit_active_peers
slskd_mesh_rate_limit_violations_total

# Circuit breakers
slskd_mesh_circuit_breaker_open{service="service-name"}
slskd_mesh_circuit_breaker_failures_total{service="service-name"}

# Work budget
slskd_mesh_work_budget_consumed_total
slskd_mesh_work_budget_peers_near_quota

# Discovery
slskd_mesh_discovery_queries_total
slskd_mesh_discovery_suspicious_peers
```

---

## TODO - VirtualSoulfind v2 Phase (V2-P1 through V2-P6)

### 📋 VirtualSoulfind Stats Requirements

When implementing VirtualSoulfind v2, ensure stats tracking for:

#### Intent System (V2-P1, V2-P2)
**Stats Object**: `IntentStats` (NEW)
**Metrics**:
- Total intents created
- Intents by origin (UserLocal, LocalAutomation, RemoteMesh, RemoteGateway)
- Intents by status (Pending, InProgress, Completed, Failed)
- Intents by domain (Music, GenericFile, etc.)
- Average completion time
- Failed intent reasons

**Access**: `intentService.GetStats()`

#### Content Catalogue (V2-P1)
**Stats Object**: `CatalogueStats` (NEW)
**Metrics**:
- Total content items
- Items by domain
- Items by verification status
- Items by source (Soulseek, Mesh, Local, Torrent)
- Recently added items count
- Missing items count

**Access**: `catalogue.GetStats()`

#### Plan Execution (V2-P3, V2-P5)
**Stats Object**: `PlannerStats` (NEW)
**Metrics**:
- Total plans created
- Plans by mode (SoulseekFriendly, MeshOnly, OfflinePlanning)
- Plans by domain
- Average plan complexity (step count)
- Work budget consumed per plan (histogram)
- Failed plans by reason

**Access**: `planner.GetStats()`

#### Backend Adapters (V2-P4)
**Stats Object**: `BackendStats` (NEW per backend)
**Metrics Per Backend**:
- Total operations attempted
- Operations successful
- Operations failed (by reason)
- Work budget consumed
- Average operation time
- Rate limit hits

**Access**: `backend.GetStats()` for each backend

#### Resolution System (V2-P5)
**Stats Object**: `ResolverStats` (NEW)
**Metrics**:
- Total items resolved
- Resolution by source preference
- Work budget consumed
- Failed resolutions by reason
- Average resolution time

**Access**: `resolver.GetStats()`

### 📋 VirtualSoulfind HTTP API Endpoints

**Controller**: `VirtualSoulfindStatsController` (NEW)
**Route**: `/api/v0/virtualsoulfind/stats`

**Endpoints to Add**:
```csharp
[HttpGet("overview")]
public IActionResult GetOverview()
{
    // High-level summary
}

[HttpGet("intents")]
public IActionResult GetIntentStats()
{
    // Intent system stats
}

[HttpGet("catalogue")]
public IActionResult GetCatalogueStats()
{
    // Catalogue stats
}

[HttpGet("plans")]
public IActionResult GetPlannerStats()
{
    // Planner stats
}

[HttpGet("backends")]
public IActionResult GetBackendStats()
{
    // All backend stats
}

[HttpGet("work-budget")]
public IActionResult GetWorkBudgetUsage()
{
    // VirtualSoulfind-specific work budget consumption
}
```

---

## TODO - Proxy/Relay Phase (T-PR01 through T-PR04, H-PR05)

### 📋 Catalog Fetch Service (T-PR02)
**Stats Object**: `CatalogFetchStats` (NEW)
**Metrics**:
- Total fetches attempted
- Fetches by domain (allowed domains)
- Cache hit rate
- Work budget consumed
- Failed fetches by reason (denied, timeout, SSRF blocked)
- Average fetch time

**Access**: `catalogFetchService.GetStats()`

### 📋 Content Relay Service (T-PR03)
**Stats Object**: `ContentRelayStats` (NEW)
**Metrics**:
- Total chunks served
- Bytes transferred
- Active streams (current)
- Work budget consumed
- Failed requests by reason
- Average chunk serve time

**Access**: `contentRelayService.GetStats()`

### 📋 Trusted Relay Service (T-PR04)
**Stats Object**: `TrustedRelayStats` (NEW)
**Metrics**:
- Total relay requests
- Active tunnels (current)
- Requests by trusted peer
- Work budget consumed
- Failed requests by reason
- Average relay time

**Access**: `trustedRelayService.GetStats()`

---

## General Principles

### When Adding New Systems

**Always Include**:
1. **Stats/Metrics Object** - Dedicated record type with all relevant metrics
2. **GetStats()/GetMetrics() Method** - Public method on the service/tracker
3. **Work Budget Tracking** - If system consumes work units, track and expose
4. **Rate Limit Tracking** - If system has quotas, expose consumption
5. **Error Tracking** - Count and categorize failures
6. **Performance Metrics** - Average/p95/p99 timing where relevant

**Privacy Guidelines**:
- ✅ DO expose: Counts, percentages, averages, status codes
- ✅ DO expose: Service names, domain types, content types
- ❌ DO NOT expose: Peer IDs (hash/truncate first)
- ❌ DO NOT expose: Usernames, IPs, hostnames
- ❌ DO NOT expose: File paths, content titles
- ❌ DO NOT expose: Secrets, keys, tokens

**Performance Guidelines**:
- Keep stats collection lightweight (avoid locks)
- Use `ConcurrentDictionary` for counters
- Use `Interlocked` operations for simple increments
- Implement sliding windows for time-based metrics
- Expose sampling/aggregation, not raw events

**Testing Guidelines**:
- Add unit tests for stats collection (see `RouterStatsTests.cs`)
- Verify stats update correctly under load
- Ensure thread-safety (concurrent access tests)
- Validate stats reset/cleanup behavior

---

## Quick Reference: Where to Add Stats

**New Service Implementation Checklist**:
```
[ ] Define stats object (e.g., MyServiceStats)
[ ] Add GetStats() method to service
[ ] Update introspection service (if mesh-exposed)
[ ] Add HTTP API endpoint (if user-facing)
[ ] Add Prometheus metrics (if Prometheus enabled)
[ ] Write tests for stats collection
[ ] Document stats in this file
```

**Stats Implementation Template**:
```csharp
// Stats object
public sealed record MyServiceStats
{
    public int TotalOperations { get; init; }
    public int SuccessfulOperations { get; init; }
    public int FailedOperations { get; init; }
    public int WorkBudgetConsumed { get; init; }
}

// Service implementation
private int _totalOps;
private int _successOps;
private int _failedOps;

public MyServiceStats GetStats()
{
    return new MyServiceStats
    {
        TotalOperations = _totalOps,
        SuccessfulOperations = _successOps,
        FailedOperations = _failedOps,
        WorkBudgetConsumed = /* from tracker */
    };
}
```

---

## References

- **T-SF07**: Service Fabric Metrics/Observability (planned task)
- **V2-P6-04**: VirtualSoulfind observability integration (planned task)
- **H-PR05**: Proxy/Relay hardening (includes metrics requirements)
- **SECURITY-GUIDELINES.md**: Logging & metrics hygiene section
