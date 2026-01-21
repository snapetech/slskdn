# Service Fabric Testing Strategy

**Status**: Planning Phase  
**Created**: December 11, 2025  
**Priority**: Deferred until after T-SF05, T-SF06, T-SF07, H-02, H-08

> **Project Note**: This is a fork of [slskd](https://github.com/slskd/slskd). See [README.md](README.md#acknowledgments) for attribution.

---

## Overview

Comprehensive testing strategy to validate the mesh service fabric under realistic network conditions, from low load to abusive scenarios. Tests will validate security boundaries, performance characteristics, and resilience to various attack patterns.

---

## Test Categories

### 1. Network Condition Simulation (T-TEST-01)

**Goal**: Simulate realistic network conditions from ideal to degraded.

**Components**:
- NetworkConditionSimulator:
  - Latency injection (0ms to 2000ms)
  - Packet loss simulation (0% to 50%)
  - Bandwidth throttling (56kbps to 1Gbps)
  - Jitter simulation (variance in latency)
  - Connection drops and reconnects

**Test Scenarios**:
- Ideal: LAN conditions (0ms, 0% loss, 1Gbps)
- Good: Normal internet (20ms, 0.1% loss, 100Mbps)
- Mediocre: Mobile 4G (50ms, 1% loss, 10Mbps)
- Poor: Mobile 3G (200ms, 5% loss, 1Mbps)
- Terrible: Satellite (600ms, 10% loss, 512kbps)
- Chaotic: Random spikes and drops

**Metrics**:
- Request success rate
- Average response time
- P50/P95/P99 latency
- Timeout rate
- Retry behavior

---

### 2. Load Pattern Simulation (T-TEST-02)

**Goal**: Test behavior under various load patterns.

**Load Profiles**:

#### Low Load (Well-Behaved Client)
- 1-5 requests per minute
- Small payloads (<1KB)
- Reasonable timeouts
- Proper error handling
- **Expected**: All requests succeed, low latency

#### Normal Load (Active Client)
- 10-30 requests per minute
- Mixed payload sizes (1KB-100KB)
- Concurrent requests (2-5)
- **Expected**: All requests succeed, acceptable latency

#### High Load (Heavy Client)
- 50-100 requests per minute
- Large payloads (up to MaxRequestBodyBytes)
- Higher concurrency (10-20)
- **Expected**: Most requests succeed, some queuing, no crashes

#### Burst Load (Legitimate Spike)
- Sudden spike from 5 to 100 requests
- Mixed patterns
- **Expected**: Graceful degradation, queue management, no crashes

#### Abusive Load (Denial of Service)
- 500+ requests per minute
- Oversized payloads (>MaxRequestBodyBytes)
- Max concurrency (100+)
- Invalid payloads
- **Expected**: Rate limiting kicks in, violators tracked, service remains available

---

### 3. Service-Specific Tests (T-TEST-03)

#### PodsMeshService
- **Normal**: Join, post messages, leave
- **Heavy**: Rapid joins/leaves, message flooding
- **Abusive**: Oversized messages (>4KB), banned users, invalid pod IDs

#### VirtualSoulfindMeshService
- **Normal**: Single MBID lookups, small batches
- **Heavy**: Max batch size (20 MBIDs), rapid queries
- **Abusive**: Oversized batches (>20), invalid MBIDs, path traversal attempts

#### MeshIntrospectionService
- **Normal**: Periodic status checks
- **Heavy**: Rapid polling
- **Abusive**: Attempting to extract PII, injection attacks

---

### 4. Security Boundary Tests (T-TEST-04)

**Goal**: Validate all security gates and hardening measures.

#### Gateway Auth Tests (H-01)
- ✅ Already covered (11 tests passing)
- Missing API key
- Invalid API key
- Missing CSRF token
- Invalid CSRF token
- Origin header bypass attempts

#### Rate Limiting Tests
- Single IP exceeding limits
- Distributed attack simulation
- Burst vs sustained patterns
- Per-service vs global limits

#### Payload Security Tests
- Oversized payloads (1MB+, 10MB+, 100MB+)
- Malformed JSON
- Binary payloads
- Null bytes, special characters
- JSON bombs (deeply nested)
- Compression bombs (if compression added)

#### Service Isolation Tests
- Calling non-allowed services
- Method injection attempts
- ServiceId spoofing
- Descriptor tampering
- Signature forgery

---

### 5. Abuse Scenario Tests (T-TEST-05)

**Goal**: Validate protection against real-world attack patterns.

#### Resource Exhaustion
- Memory exhaustion via large payloads
- Connection exhaustion via socket flooding
- CPU exhaustion via expensive operations
- DHT pollution via descriptor spam

#### Soulseek-Specific Abuse (H-08)
- Excessive search generation
- Browse flooding
- Download queue exhaustion
- Reputation poisoning
- Ban evasion attempts

#### Work Budget Violations (H-02)
- Single call triggering excessive downstream work
- Chained service calls amplifying load
- Soulseek search amplification
- BT metadata fetch amplification

#### Privacy Attacks
- Attempting to extract peer IPs
- Attempting to extract Soulseek usernames
- Attempting to enumerate users
- Attempting to profile node behavior

---

### 6. Chaos Engineering Tests (T-TEST-06)

**Goal**: Validate resilience to unexpected failures.

**Scenarios**:
- Service provider disappearing mid-call
- DHT lookup returning stale/invalid data
- Network partition during service call
- Timeout cascades
- Circular service dependencies
- Memory pressure
- CPU starvation
- Disk I/O failures

---

### 7. Integration Tests (T-TEST-07)

**Goal**: End-to-end validation of complete workflows.

**Workflows**:
1. **Pod Chat Flow**:
   - List pods → Get specific pod → Join → Post message → Get messages → Leave
   - Validate: Auth, ordering, timestamps, signatures

2. **Music Discovery Flow**:
   - Query MBID → Get peer hints → (Future: initiate transfer)
   - Validate: Privacy (no PII), accuracy, caching

3. **Mesh Introspection Flow**:
   - Get status → Get capabilities → Get services → Correlate data
   - Validate: No sensitive data, consistency

4. **Gateway Flow**:
   - Generate key → Configure → Start gateway → Make calls → Monitor metrics
   - Validate: Auth enforcement, logging, error handling

---

## Test Harness Architecture

### Core Components

```
tests/slskd.Tests.LoadTest/
├── Harness/
│   ├── MeshServiceTestHarness.cs       # Base test orchestration
│   ├── NetworkConditionSimulator.cs     # Latency, loss, bandwidth
│   ├── LoadGenerator.cs                 # Request pattern generation
│   └── MetricsCollector.cs              # Results aggregation
├── Scenarios/
│   ├── LowLoadScenario.cs
│   ├── NormalLoadScenario.cs
│   ├── HighLoadScenario.cs
│   ├── BurstLoadScenario.cs
│   └── AbusiveLoadScenario.cs
├── Services/
│   ├── PodsServiceTests.cs
│   ├── VirtualSoulfindServiceTests.cs
│   └── IntrospectionServiceTests.cs
├── Security/
│   ├── RateLimitTests.cs
│   ├── PayloadBombTests.cs
│   └── IsolationTests.cs
└── Reports/
    └── TestResultReporter.cs            # Markdown/JSON output
```

---

## Metrics to Collect

### Performance Metrics
- Requests per second (successful)
- Requests per second (failed)
- Average latency (ms)
- P50/P95/P99/P999 latency
- Timeout rate
- Error rate by type
- Throughput (bytes/sec)

### Security Metrics
- Rate limit violations
- Auth failures
- Oversized payload rejections
- Invalid service/method calls
- Signature validation failures
- Origin validation failures

### Resource Metrics
- Memory usage (MB)
- CPU usage (%)
- Connection count
- Thread count
- GC pressure
- DHT query count

### Service-Specific Metrics
- Pods: joins, leaves, messages, active pods
- Shadow Index: queries, cache hits, peer hint counts
- Introspection: status polls, capability queries

---

## Test Execution Plan

### Phase 1: Unit Tests (Current)
- ✅ Service fabric core (47 tests)
- ✅ Gateway auth (11 tests)
- Total: 58 passing

### Phase 2: Integration Tests (After T-SF05-07)
- Service registration and discovery
- End-to-end service calls
- Gateway integration
- Auth middleware integration

### Phase 3: Load Tests (After H-02, H-08)
- Low/Normal/High load scenarios
- Network condition variations
- Resource limit validation

### Phase 4: Security Tests (After H-02, H-08)
- Rate limiting enforcement
- Payload bomb detection
- Work budget enforcement
- Soulseek caps enforcement

### Phase 5: Chaos Tests (Optional, Later)
- Failure injection
- Cascading failure detection
- Recovery validation

---

## Success Criteria

### Low Load (Must Pass)
- ✅ 100% success rate
- ✅ < 100ms average latency
- ✅ 0% timeouts

### Normal Load (Must Pass)
- ✅ 99.9% success rate
- ✅ < 500ms average latency
- ✅ < 1% timeouts

### High Load (Should Pass)
- ✅ 95% success rate
- ✅ < 2000ms average latency
- ✅ < 5% timeouts
- ✅ No crashes

### Abusive Load (Must Reject)
- ✅ Rate limiting triggers within 10 seconds
- ✅ Violation tracking increments
- ✅ Service remains available for legitimate clients
- ✅ No resource exhaustion
- ✅ No crashes

---

## Implementation Priority

1. **After T-SF05-07**: Basic integration tests
2. **After H-02**: Work budget validation tests
3. **After H-08**: Soulseek safety tests
4. **Later**: Full load and chaos testing

---

## Notes

- Tests should be runnable in CI/CD
- Load tests should be opt-in (slow, resource-intensive)
- Chaos tests should be manual only (dangerous)
- All tests must clean up resources
- Tests should not require external services (mock DHT, Soulseek, etc.)
- Security tests should validate both prevention AND logging

