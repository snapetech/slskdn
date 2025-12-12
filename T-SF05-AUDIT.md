# T-SF05: Security Audit Results

**Date**: December 11, 2025  
**Auditor**: Security Review (T-SF05-001)  
**Scope**: T-SF01 through T-SF04 (Service Fabric + HTTP Gateway)  
**Status**: In Progress

---

## Executive Summary

**Overall Security Posture**: Good foundation, needs tightening in 6 areas

**Critical Issues**: 0  
**High Priority**: 3  
**Medium Priority**: 3  
**Low Priority**: 2  

**Recommendation**: Address High Priority items before proceeding with H-08/H-02.

---

## Audit Findings

### HIGH-1: Rate Limiting Configuration Hardcoded

**Component**: `MeshServiceRouter.CheckRateLimit()`

**Issue**:
```csharp
var maxCallsPerWindow = 100; // TODO: Make configurable
```

**Risk**: 
- No per-service customization (shadow-index needs different limits than introspection)
- No runtime adjustment (can't respond to attacks)
- Fixed window duration (1 minute) may be too long

**Recommendation**:
```csharp
// Add to MeshServiceFabricOptions
public Dictionary<string, int> PerServiceRateLimits { get; set; } = new();
public int DefaultMaxCallsPerMinute { get; set; } = 100;
public int GlobalMaxCallsPerPeer { get; set; } = 500; // Across all services

// In router:
var serviceLimit = _options.PerServiceRateLimits.GetValueOrDefault(
    call.ServiceName, 
    _options.DefaultMaxCallsPerMinute);
```

**Priority**: HIGH (needed for production)

---

### HIGH-2: No Global Per-Peer Quota Enforcement

**Component**: `MeshServiceRouter.RouteAsync()`

**Issue**: 
- Rate limiting is per-peer, but not global across all services
- Malicious peer can call 100/min to EACH service (300/min total for 3 services)
- No tracking of total resource consumption per peer

**Risk**: 
- Amplification via multiple services
- Can't enforce "you're consuming too many resources overall"

**Recommendation**:
```csharp
// Add global per-peer tracking
private readonly ConcurrentDictionary<string, GlobalPeerQuota> _globalQuotas = new();

class GlobalPeerQuota 
{
    public int CallsThisMinute;
    public int CallsThisHour;
    public long BytesReceivedThisMinute;
    public DateTimeOffset WindowStart;
}

// Check BEFORE per-service rate limit
if (!CheckGlobalQuota(remotePeerId, call))
{
    return CreateErrorReply(..., ServiceStatusCodes.QuotaExceeded, ...);
}
```

**Priority**: HIGH (blocks H-02 work budget integration)

---

### HIGH-3: Service Discovery Has No Abuse Metrics

**Component**: `DhtMeshServiceDirectory.FindByNameAsync()`

**Issue**:
- No tracking of discovery query frequency per peer
- No logging of suspicious patterns (e.g., rapid-fire discovery)
- DHT layer rate limits exist, but service layer doesn't track

**Risk**:
- Enumeration attacks (scan all service names)
- Discovery as a side-channel (timing attacks)
- No visibility into who's querying what

**Recommendation**:
```csharp
// Add discovery metrics to directory
private readonly ConcurrentDictionary<string, DiscoveryStats> _discoveryStats = new();

class DiscoveryStats
{
    public int QueriesThisMinute;
    public HashSet<string> ServiceNamesQueried; // For pattern detection
    public DateTimeOffset WindowStart;
}

// Log suspicious patterns
if (stats.ServiceNamesQueried.Count > 10) // Enumeration?
{
    _logger.LogWarning("[Discovery] Possible enumeration from {PeerId}", requestPeerId);
}
```

**Priority**: HIGH (observability requirement)

---

### MEDIUM-1: Timeout Configuration Not Per-Service

**Component**: `MeshServiceRouter.RouteAsync()` line 157

**Issue**:
```csharp
cts.CancelAfter(TimeSpan.FromSeconds(30)); // TODO: Make configurable
```

**Risk**:
- Shadow-index queries need longer timeout (complex MBID lookups)
- Introspection should be fast (5s max)
- One size doesn't fit all

**Recommendation**:
```csharp
// Add to MeshServiceFabricOptions
public Dictionary<string, int> PerServiceTimeoutSeconds { get; set; } = new()
{
    ["shadow-index"] = 60,
    ["pod-chat"] = 10,
    ["mesh-stats"] = 5
};
public int DefaultTimeoutSeconds { get; set; } = 30;

// In router:
var timeoutSeconds = _options.PerServiceTimeoutSeconds.GetValueOrDefault(
    call.ServiceName,
    _options.DefaultTimeoutSeconds);
cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
```

**Priority**: MEDIUM (quality of life)

---

### MEDIUM-2: No Circuit Breaker for Failing Services

**Component**: `MeshServiceRouter.RouteAsync()`

**Issue**:
- If a service starts throwing exceptions, router keeps invoking it
- No automatic service disabling on repeated failures
- Cascading failures possible

**Risk**:
- Resource exhaustion from failing service
- Log spam
- Caller timeouts accumulate

**Recommendation**:
```csharp
// Add circuit breaker per service
class ServiceHealthTracker
{
    public int ConsecutiveFailures;
    public DateTimeOffset? CircuitOpenedAt;
    public bool IsCircuitOpen => ConsecutiveFailures >= 5 && 
        (DateTimeOffset.UtcNow - CircuitOpenedAt) < TimeSpan.FromMinutes(5);
}

// Before invoking service:
if (_serviceHealth[serviceName].IsCircuitOpen)
{
    return CreateErrorReply(..., ServiceStatusCodes.ServiceUnavailable, "Circuit open");
}
```

**Priority**: MEDIUM (reliability)

---

### MEDIUM-3: Client Connection Pooling Not Validated

**Component**: `MeshServiceClient` (assumed implementation)

**Issue**: 
- Need to verify connection reuse is secure
- Need to verify no connection leaks
- Need to verify timeouts apply to connection establishment too

**Action Required**: 
- Read `MeshServiceClient.cs` implementation
- Verify connection pooling logic
- Check for resource leaks

**Priority**: MEDIUM (after reading implementation)

---

### LOW-1: Service Registration Not Authenticated

**Component**: `MeshServiceRouter.RegisterService()`

**Issue**:
- Any code can register any service name
- No validation that registering code owns the service
- Potential for name squatting

**Risk**: 
- Internal code only (not exposed to network)
- But could cause confusion during development

**Recommendation**:
```csharp
// Add assertion that only approved assemblies can register
if (!IsApprovedAssembly(service.GetType().Assembly))
{
    throw new SecurityException($"Assembly not approved for service registration: {assembly}");
}
```

**Priority**: LOW (defense in depth)

---

### LOW-2: No Metrics for Failed Calls

**Component**: `MeshServiceRouter.RouteAsync()`

**Issue**:
- Router logs errors but doesn't aggregate metrics
- Can't answer "how many calls failed in the last hour?"
- No per-service failure rate tracking

**Recommendation**:
```csharp
// Add to RouterStats
public Dictionary<string, ServiceMetrics> PerServiceMetrics { get; init; } = new();

class ServiceMetrics
{
    public long TotalCalls;
    public long SuccessCalls;
    public long FailedCalls;
    public long TimeoutCalls;
    public double AverageLatencyMs;
}
```

**Priority**: LOW (nice to have for T-SF07 metrics)

---

## Positive Findings (What's Already Good)

✅ **ViolationTracker Integration**: Router correctly records violations  
✅ **Payload Size Checks**: Enforced before processing  
✅ **Per-Peer Rate Limiting**: Basic implementation present  
✅ **Timeout Handling**: Distinguishes between service timeout and caller cancellation  
✅ **Exception Safety**: All exceptions caught and logged  
✅ **Logging Hygiene**: No PII in logs (peer IDs only)  
✅ **Thread Safety**: ConcurrentDictionary used correctly  
✅ **Null Checks**: Comprehensive validation  

---

## Integration Points to Review (T-SF05-002)

### With SecurityCore (`NetworkGuardPolicy`, `ReputationPolicy`)

**Current**: ViolationTracker integration only

**Needed**:
1. Check `NetworkGuardPolicy` before accepting service calls
2. Use `ReputationPolicy` to filter service discovery results
3. Integrate `PeerReputation` scores into rate limits (low rep = stricter limits)

**Code Location**: `src/slskd/Security/Policies.cs`

### With DHT Rate Limiting

**Current**: DHT has its own rate limits

**Needed**:
- Ensure service fabric limits don't conflict with DHT limits
- Coordinate quotas (DHT + service fabric = combined budget)

---

## Recommended Fixes (Prioritized)

### Phase 1: High Priority (Do Now)
1. **HIGH-1**: Make rate limits configurable per-service
2. **HIGH-2**: Add global per-peer quota tracking
3. **HIGH-3**: Add discovery abuse metrics

### Phase 2: Medium Priority (Before T-SF06)
4. **MEDIUM-1**: Per-service timeout configuration
5. **MEDIUM-2**: Circuit breaker implementation
6. **MEDIUM-3**: Audit `MeshServiceClient` connection handling

### Phase 3: Low Priority (Nice to Have)
7. **LOW-1**: Service registration authentication
8. **LOW-2**: Aggregate failure metrics

---

## Next Steps

1. ✅ Complete this audit (T-SF05-001)
2. 📋 Read `MeshServiceClient.cs` for connection handling audit
3. 📋 Implement HIGH-1, HIGH-2, HIGH-3 fixes (T-SF05-002, T-SF05-003, T-SF05-004)
4. 📋 Add security integration tests (T-SF05-006)
5. 📋 Document security model (T-SF06)

---

## Test Coverage Needed (T-SF05-006)

### Abuse Scenarios
- [ ] Peer sends 1000 calls/min (should trigger rate limit)
- [ ] Peer sends calls to 10 different services (should trigger global quota)
- [ ] Peer sends 10MB payload (should reject)
- [ ] Peer sends malformed ServiceCall (should reject safely)
- [ ] Peer enumerates all service names (should log warning)

### Failure Scenarios
- [ ] Service throws exception (should return error, not crash)
- [ ] Service times out (should cancel and return timeout error)
- [ ] Service returns null (should handle gracefully)
- [ ] ViolationTracker throws (should log warning, continue)

### Integration Scenarios
- [ ] Banned peer calls service (should reject via NetworkGuardPolicy)
- [ ] Low-reputation peer calls service (should have stricter limits)
- [ ] High-reputation peer calls service (should have looser limits)

---

**Status**: Audit complete, ready for implementation  
**Next Task**: T-SF05-002 (Integrate guard/reputation) or address HIGH-priority items first

---

*Paranoid bastard mode: ON*  
*Compromises: STILL ZERO*
