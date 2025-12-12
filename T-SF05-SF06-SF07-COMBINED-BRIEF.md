# T-SF05, T-SF06, T-SF07 Combined Implementation Brief

**Repository**: `https://github.com/snapetech/slskdn`  
**Branch**: `experimental/multi-source-swarm`  
**Prerequisites**: T-SF01 through T-SF04 completed and stable  
**Status**: 📋 Ready for Implementation  
**Created**: December 11, 2025

---

## OVERVIEW

This brief covers three related tasks that complete the service fabric implementation:

- **T-SF05**: Security & abuse review, hardening, and limits
- **T-SF06**: Developer-facing docs and code examples
- **T-SF07**: Metrics / observability for service fabric and gateway

**Work incrementally**: Complete T-SF05, commit, then T-SF06, commit, then T-SF07.

### Prerequisites (Assumed Complete):

- **T-SF01**: `MeshServiceDescriptor` + `IMeshServiceDirectory` with hardened DHT integration
- **T-SF02**: `IMeshService`, `ServiceCall`, `ServiceReply`, `MeshServiceRouter`, `IMeshServiceClient`
- **T-SF03**: Concrete services (pods/chat, VirtualSoulfind, mesh introspection)
- **T-SF04**: Local HTTP gateway with config-controlled allowlist and limits

### Global Rules (Apply to All Three Tasks):

- ✅ No big rewrites
- ✅ Do not break existing behavior
- ✅ Privacy and security are first-class
- ✅ No AI slop: small, focused diffs; no random refactors; tests updated

---

# T-SF05 – SECURITY & ABUSE REVIEW + HARDENING

## 0. SCOPE

### WHAT YOU ARE ALLOWED TO DO:

- Audit and harden all paths introduced by T-SF01–T-SF04:
  - Service descriptor publishing and DHT lookups
  - Mesh service routing and client calls
  - Concrete services (pods, VirtualSoulfind, mesh introspection)
  - Local HTTP gateway (and WS if it exists)
- Integrate more tightly with existing guard / violation / reputation systems
- Add or tighten:
  - Rate limiting
  - Payload size limits
  - Timeouts
  - Basic DoS protections

### WHAT YOU MUST NOT DO:

- ❌ Change protocol semantics in ways that break interoperability
- ❌ Expose new data in DHT or logs that worsens privacy
- ❌ Introduce heavy new dependencies for security

---

## 1. RECONNAISSANCE FOR T-SF05

**BEFORE implementing hardening**, locate and read:

### 1.1. Existing Security Components

Find:
- Guard / violation / reputation classes
- Any ban list, quarantine, or penalty logic
- Where they are invoked today (e.g., protocol violations, spammy peers)

Questions to answer:
- How do I check if a peer is banned/quarantined?
- How do I register a violation?
- What reputation signals exist?
- What are the escalation thresholds?

### 1.2. Existing Limits

Find:
- Any rate-limiter, quota, or backpressure mechanisms (HTTP, mesh, or elsewhere)
- Any per-peer limits, max message sizes, etc.

Questions to answer:
- What rate limiting infrastructure exists?
- How are limits configured?
- Where are limits enforced?

### 1.3. New Service Fabric & Gateway Code

Review:
- T-SF01: service descriptor & DHT directory
- T-SF02: router & client
- T-SF03: pods / VirtualSoulfind / introspection services
- T-SF04: HTTP gateway configuration and controllers

Questions to answer:
- Where are the current weakest points?
- What has unbounded lists?
- What has unchecked payloads?
- What's missing rate limits?

**DO NOT proceed until you understand the security landscape.**

---

## 2. HARDENING TARGETS

You must systematically harden the following areas:

### 2.1. DHT Service Descriptor Paths

**Ensure**:

✅ Max size limits on:
- Individual descriptors (config: `MaxDescriptorBytes`, default: 16KB)
- DHT value blobs (list of descriptors, config: `MaxDhtValueBytes`, default: 256KB)

✅ Signature validation for all descriptors:
- Invalid entries are ignored
- Optionally flagged to security/reputation system

✅ Timestamp validation (`CreatedAt`, `ExpiresAt`):
- Small allowable skew (config: `MaxTimestampSkewSeconds`, default: 300 = 5 min)
- Expired descriptors dropped
- Future descriptors dropped

✅ Filtering out descriptors from banned/quarantined peers:
- Integrate with existing ban list check

**Add explicit config knobs**:
```jsonc
{
  "MeshServices": {
    "Directory": {
      "MaxDescriptorsPerLookup": 20,
      "MaxDescriptorBytes": 16384,
      "MaxDhtValueBytes": 262144,
      "MaxTimestampSkewSeconds": 300,
      "ValidateDhtSignatures": true
    }
  }
}
```

**Add comments** explaining each limit and why it exists.

### 2.2. MeshServiceRouter (Incoming Service Calls)

For each incoming `ServiceCall`:

**✅ Security Checks** (in order):

1. **Peer ban/quarantine check**:
   ```csharp
   if (securityCore.IsBanned(context.PeerId) || securityCore.IsQuarantined(context.PeerId))
   {
       logger.LogWarning("Service call rejected: peer {PeerId} is banned/quarantined", context.PeerId);
       await securityCore.RegisterViolationAsync(context.PeerId, ViolationType.CallWhileBanned);
       return ServiceReply.Error(401, "Unauthorized");
   }
   ```

2. **Payload size check**:
   ```csharp
   var maxPayloadSize = GetMaxPayloadSizeForService(call.ServiceId); // Per-service or global
   if (call.Payload.Length > maxPayloadSize)
   {
       logger.LogWarning("Service call rejected: payload too large ({Size} > {Max}) from {PeerId}",
           call.Payload.Length, maxPayloadSize, context.PeerId);
       await securityCore.RegisterViolationAsync(context.PeerId, ViolationType.PayloadTooLarge);
       return ServiceReply.Error(413, "Payload too large");
   }
   ```

3. **Method validation**:
   ```csharp
   if (!service.SupportsMethod(call.Method))
   {
       logger.LogInformation("Service call rejected: unknown method {Method} for service {ServiceName}",
           call.Method, service.ServiceName);
       return ServiceReply.Error(404, "Unknown method");
   }
   ```

4. **Rate limiting**:
   ```csharp
   // Per-peer, per-service rate limit
   var rateLimitKey = $"{context.PeerId}:{call.ServiceId}";
   if (!rateLimiter.TryAcquire(rateLimitKey, maxCallsPerMinute))
   {
       logger.LogWarning("Service call rate limited: {PeerId} to {ServiceName}",
           context.PeerId, call.ServiceId);
       await securityCore.RegisterViolationAsync(context.PeerId, ViolationType.RateLimitExceeded);
       return ServiceReply.Error(429, "Rate limit exceeded");
   }
   
   // Per-peer global service-call rate (optional)
   if (!rateLimiter.TryAcquire($"global:{context.PeerId}", maxGlobalCallsPerMinute))
   {
       logger.LogWarning("Global service call rate limit exceeded: {PeerId}", context.PeerId);
       await securityCore.RegisterViolationAsync(context.PeerId, ViolationType.GlobalRateLimitExceeded);
       return ServiceReply.Error(429, "Global rate limit exceeded");
   }
   ```

**✅ Configuration**:
```jsonc
{
  "MeshServices": {
    "Router": {
      "MaxPayloadBytes": 1048576,  // 1MB default
      "MaxCallsPerPeerPerMinute": 100,
      "MaxGlobalCallsPerPeerPerMinute": 200,
      "PerServiceLimits": {
        "pods": {
          "MaxPayloadBytes": 4096,  // 4KB for chat
          "MaxCallsPerMinute": 50
        },
        "shadow-index": {
          "MaxCallsPerMinute": 30
        }
      }
    }
  }
}
```

**✅ Violation Tracking**:
- On violations, notify existing violation/reputation subsystem
- Optionally escalate to quarantine/ban based on existing rules
- All failures logged (without dumping payloads)
- Return `ServiceReply` with nonzero `StatusCode` and safe messages

### 2.3. IMeshServiceClient (Outgoing Calls)

**✅ Enforce**:

1. **Per-request timeout** (configurable):
   ```csharp
   using var cts = new CancellationTokenSource(
       TimeSpan.FromSeconds(config.RequestTimeoutSeconds));
   var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);
   ```

2. **Maximum concurrent in-flight calls to a single peer**:
   ```csharp
   var semaphore = _peerSemaphores.GetOrAdd(targetPeerId, 
       _ => new SemaphoreSlim(config.MaxConcurrentCallsPerPeer));
   
   if (!await semaphore.WaitAsync(0, cancellationToken))
   {
       throw new InvalidOperationException("Too many concurrent calls to peer");
   }
   try
   {
       // Make call
   }
   finally
   {
       semaphore.Release();
   }
   ```

3. **Reputation adjustment on failures**:
   ```csharp
   catch (TimeoutException)
   {
       logger.LogWarning("Service call timeout: {ServiceName} on {PeerId}",
           serviceName, targetPeerId);
       await reputationService.RecordFailureAsync(targetPeerId, FailureType.Timeout);
       throw;
   }
   ```

**✅ Configuration**:
```jsonc
{
  "MeshServices": {
    "Client": {
      "RequestTimeoutSeconds": 30,
      "MaxConcurrentCallsPerPeer": 10,
      "AdjustReputationOnFailure": true
    }
  }
}
```

**✅ Exception Handling**:
- All exceptions logged with full details locally
- Safe errors returned to callers (no stack traces over the wire)

### 2.4. Concrete Services

#### 2.4.1. Pods / Chat Service

**✅ Message size limits**:
```csharp
private const int MaxMessageContentBytes = 4096; // 4KB

if (Encoding.UTF8.GetByteCount(request.Content) > MaxMessageContentBytes)
{
    return ServiceReply.Error(413, "Message too large");
}
```

**✅ Rate limits**:
- Messages per peer per pod per time window (10/min default)
- Joins/Leaves per peer per time window (5/min default)

```csharp
var rateLimitKey = $"pod:{podId}:peer:{context.PeerId}:message";
if (!rateLimiter.TryAcquire(rateLimitKey, 10, TimeSpan.FromMinutes(1)))
{
    logger.LogWarning("Pod message rate limited: {PeerId} in {PodId}",
        context.PeerId, podId);
    await securityCore.RegisterViolationAsync(context.PeerId, ViolationType.PodSpam);
    return ServiceReply.Error(429, "Rate limit exceeded");
}
```

**✅ Privacy**:
- No PII in error messages or logs
- No Soulseek usernames exposed
- Use internal peer IDs only

**✅ Security hooks**:
- Use existing guard/reputation for spammy peers
- Integrate with existing pod permissions

#### 2.4.2. VirtualSoulfind / MBID Index Service

**✅ Limits**:
- Queries per peer per time window (30/min default)
- Register/update operations per peer per time window (100/hour default)

```csharp
// Query limit
var queryKey = $"shadow-index:query:{context.PeerId}";
if (!rateLimiter.TryAcquire(queryKey, 30, TimeSpan.FromMinutes(1)))
{
    return ServiceReply.Error(429, "Query rate limit exceeded");
}

// Registration limit
var registerKey = $"shadow-index:register:{context.PeerId}";
if (!rateLimiter.TryAcquire(registerKey, 100, TimeSpan.FromHours(1)))
{
    logger.LogWarning("Shadow index registration rate limited: {PeerId}", context.PeerId);
    await securityCore.RegisterViolationAsync(context.PeerId, ViolationType.IndexSpam);
    return ServiceReply.Error(429, "Registration rate limit exceeded");
}
```

**✅ Validation**:
```csharp
// MBID validation
if (!IsValidMbid(request.Mbid))
{
    logger.LogInformation("Invalid MBID rejected: {Mbid} from {PeerId}",
        request.Mbid, context.PeerId);
    return ServiceReply.Error(400, "Invalid MBID format");
}

// Track ID validation
if (string.IsNullOrWhiteSpace(request.TrackId) || request.TrackId.Length > 256)
{
    return ServiceReply.Error(400, "Invalid track ID");
}
```

**✅ Anti-poisoning**:
- Reuse existing anti-poisoning logic
- Reject garbage data
- Track suspicious patterns

**✅ Privacy**:
- Do NOT leak other peers' IPs in replies
- Do NOT leak Soulseek usernames
- Use internal peer IDs only

#### 2.4.3. Mesh Introspection Service

**✅ Privacy checks**:
```csharp
// Ensure no sensitive data in responses
public async Task<ServiceReply> HandleGetStatusAsync(...)
{
    var status = new
    {
        UptimeSeconds = (int)(DateTimeOffset.UtcNow - _startTime).TotalSeconds,
        ConnectedPeers = _peerManager.GetConnectedPeerCount(),
        Services = _router.GetRegisteredServiceNames(),
        // DO NOT include:
        // - Environment.UserName
        // - Environment.MachineName
        // - File system paths
        // - Public IP addresses
    };
    
    return ServiceReply.Success(Serialize(status));
}
```

**✅ Rate limiting** (optional, low priority):
- 60 requests/min per peer (low risk, read-only)

**✅ Responses must be**:
- Generic and safe
- Aggregate data only
- No PII or sensitive system details

### 2.5. HTTP Gateway

**✅ Confirm defaults**:
```csharp
// Verify in config
public class MeshGatewayConfig
{
    public bool Enabled { get; set; } = false;  // MUST be false by default
    public string BindAddress { get; set; } = "127.0.0.1";  // MUST be localhost
    // ... other properties
}
```

**✅ Enforce limits**:

1. **Service allowlist**:
   ```csharp
   if (!config.AllowedServices.Contains(serviceName, StringComparer.OrdinalIgnoreCase))
   {
       logger.LogWarning("Gateway: rejected non-allowed service {ServiceName} from {IP}",
           serviceName, HttpContext.Connection.RemoteIpAddress);
       return Forbid();
   }
   ```

2. **Body size**:
   ```csharp
   // Enforce at ASP.NET level
   [RequestSizeLimit(1048576)] // 1MB
   
   // Enforce again before ServiceCall
   if (body.Length > config.MaxRequestBodyBytes)
   {
       logger.LogWarning("Gateway: oversized body ({Size} > {Max}) from {IP}",
           body.Length, config.MaxRequestBodyBytes, HttpContext.Connection.RemoteIpAddress);
       return StatusCode(413, new { error = "Request body too large" });
   }
   ```

3. **Timeout**:
   ```csharp
   using var cts = new CancellationTokenSource(
       TimeSpan.FromSeconds(config.RequestTimeoutSeconds));
   ```

4. **Rate limiting**:
   ```csharp
   // Integrate with existing HTTP rate limiter if available
   if (httpRateLimiter != null && !httpRateLimiter.TryAcquire(remoteIp, "mesh-gateway"))
   {
       return StatusCode(429, new { error = "Rate limit exceeded" });
   }
   
   // Or add lightweight per-IP/per-service counters
   var rateLimitKey = $"gateway:{remoteIp}:{serviceName}";
   if (!rateLimiter.TryAcquire(rateLimitKey, config.MaxRequestsPerMinute, TimeSpan.FromMinutes(1)))
   {
       logger.LogWarning("Gateway rate limited: {IP} to {ServiceName}", remoteIp, serviceName);
       return StatusCode(429, new { error = "Rate limit exceeded" });
   }
   ```

**✅ Logging**:
```csharp
// DO log:
logger.LogInformation(
    "Gateway: {Method} {ServiceName}/{Path} -> {Status} from {IP}",
    httpMethod, serviceName, path, statusCode, remoteIp);

// DO NOT log (unless LogBodies == true):
// - Full request/response bodies
// - Headers (except safe ones)
// - Query parameters (may contain secrets)

// If LogBodies enabled:
if (config.LogBodies)
{
    logger.LogWarning(
        "Gateway [VERBOSE/UNSAFE]: Body: {Body}",
        Encoding.UTF8.GetString(body.Take(1000).ToArray())); // Truncate
}
```

---

## 3. CONFIGURATION SURFACE

Add or centralize config for all limits:

```jsonc
{
  "MeshServices": {
    "Directory": {
      "MaxDescriptorsPerLookup": 20,
      "MaxDescriptorBytes": 16384,
      "MaxDhtValueBytes": 262144,
      "MaxTimestampSkewSeconds": 300
    },
    "Router": {
      "MaxPayloadBytes": 1048576,
      "MaxCallsPerPeerPerMinute": 100,
      "MaxGlobalCallsPerPeerPerMinute": 200,
      "PerServiceLimits": {
        "pods": {
          "MaxPayloadBytes": 4096,
          "MaxCallsPerMinute": 50,
          "MaxMessageContentBytes": 4096,
          "MaxMessagesPerPodPerMinute": 10,
          "MaxJoinLeavesPerMinute": 5
        },
        "shadow-index": {
          "MaxCallsPerMinute": 30,
          "MaxRegistrationsPerHour": 100
        },
        "mesh-introspect": {
          "MaxCallsPerMinute": 60
        }
      }
    },
    "Client": {
      "RequestTimeoutSeconds": 30,
      "MaxConcurrentCallsPerPeer": 10
    }
  },
  "MeshGateway": {
    "Enabled": false,
    "BindAddress": "127.0.0.1",
    "AllowedServices": ["pods", "shadow-index", "mesh-introspect"],
    "MaxRequestBodyBytes": 1048576,
    "RequestTimeoutSeconds": 30,
    "LogBodies": false,
    "MaxRequestsPerMinute": 100
  }
}
```

**Requirements**:
- Follow existing naming patterns
- Document where other config keys are documented
- All limits have safe defaults
- All timeouts are reasonable

---

## 4. TESTING FOR T-SF05

Add or update tests to cover:

### 4.1. Router Security Tests

```csharp
[Test]
public async Task Router_RejectsOversizedPayload()
{
    var call = new ServiceCall
    {
        ServiceId = "test-service",
        Method = "Test",
        Payload = new byte[2 * 1024 * 1024] // 2MB, over limit
    };
    
    var reply = await router.HandleCallAsync(call, context);
    
    Assert.AreEqual(413, reply.StatusCode);
}

[Test]
public async Task Router_RejectsBannedPeer()
{
    securityCore.Setup(s => s.IsBanned(bannedPeerId)).Returns(true);
    context.PeerId = bannedPeerId;
    
    var reply = await router.HandleCallAsync(call, context);
    
    Assert.AreEqual(401, reply.StatusCode);
    securityCore.Verify(s => s.RegisterViolationAsync(
        bannedPeerId, ViolationType.CallWhileBanned), Times.Once);
}

[Test]
public async Task Router_EnforcesRateLimit()
{
    // Make maxCallsPerMinute + 1 calls rapidly
    for (int i = 0; i <= maxCallsPerMinute; i++)
    {
        var reply = await router.HandleCallAsync(call, context);
        if (i < maxCallsPerMinute)
            Assert.AreEqual(0, reply.StatusCode);
        else
            Assert.AreEqual(429, reply.StatusCode); // Rate limited
    }
}
```

### 4.2. DHT Descriptor Tests

```csharp
[Test]
public async Task Directory_DropsInvalidSignature()
{
    var descriptors = new[]
    {
        CreateValidDescriptor(),
        CreateDescriptorWithInvalidSignature()
    };
    
    var results = await directory.ValidateAndFilterAsync(descriptors);
    
    Assert.AreEqual(1, results.Count);
}

[Test]
public async Task Directory_DropsExpiredDescriptor()
{
    var descriptor = new MeshServiceDescriptor
    {
        ExpiresAt = DateTimeOffset.UtcNow.AddHours(-1) // Expired
    };
    
    var results = await directory.ValidateAndFilterAsync(new[] { descriptor });
    
    Assert.AreEqual(0, results.Count);
}

[Test]
public async Task Directory_DropsDescriptorFromBannedPeer()
{
    securityCore.Setup(s => s.IsBanned(bannedPeerId)).Returns(true);
    var descriptor = CreateDescriptor(bannedPeerId);
    
    var results = await directory.ValidateAndFilterAsync(new[] { descriptor });
    
    Assert.AreEqual(0, results.Count);
}
```

### 4.3. Gateway Tests

```csharp
[Test]
public async Task Gateway_RejectsOversizedBody()
{
    var body = new byte[2 * 1024 * 1024]; // 2MB
    
    var response = await client.PostAsync("/mesh/http/pods/test", 
        new ByteArrayContent(body));
    
    Assert.AreEqual(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
}

[Test]
public async Task Gateway_RejectsWhenDisabled()
{
    config.Enabled = false;
    
    var response = await client.GetAsync("/mesh/http/pods/test");
    
    Assert.IsTrue(response.StatusCode == HttpStatusCode.NotFound || 
                  response.StatusCode == HttpStatusCode.Forbidden);
}

[Test]
public async Task Gateway_RejectsNonAllowedService()
{
    config.AllowedServices = new[] { "pods" };
    
    var response = await client.GetAsync("/mesh/http/unauthorized-service/test");
    
    Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
}
```

### 4.4. Service-Specific Tests

```csharp
[Test]
public async Task PodsService_RejectsOversizedMessage()
{
    var call = new ServiceCall
    {
        Method = "PostMessage",
        Payload = Serialize(new { 
            podId = "test", 
            content = new string('x', 10000) // Too large
        })
    };
    
    var reply = await podsService.HandleCallAsync(call, context);
    
    Assert.AreEqual(413, reply.StatusCode);
}

[Test]
public async Task ShadowIndexService_RejectsInvalidMbid()
{
    var call = new ServiceCall
    {
        Method = "LookupByMbId",
        Payload = Serialize(new { mbid = "invalid-format" })
    };
    
    var reply = await shadowIndexService.HandleCallAsync(call, context);
    
    Assert.AreEqual(400, reply.StatusCode);
}

[Test]
public async Task IntrospectionService_DoesNotLeakSensitiveData()
{
    var reply = await introspectionService.HandleCallAsync(
        new ServiceCall { Method = "GetStatus" }, context);
    
    var payload = Encoding.UTF8.GetString(reply.Payload);
    Assert.IsFalse(payload.Contains(Environment.UserName));
    Assert.IsFalse(payload.Contains(Environment.MachineName));
}
```

Keep tests minimal but targeted, aligned with existing test patterns.

---

## 5. T-SF05 ANTI-SLOP CHECKLIST

Before you consider T-SF05 done:

- [ ] You only tightened and integrated security/limits; you did not redesign protocols
- [ ] All new limits are configurable, with safe defaults
- [ ] No new PII is added to descriptors, responses, or logs
- [ ] No `.Result` / `.Wait()` in new async code
- [ ] Tests cover at least the most obvious abuse cases
- [ ] Rate limiting integrated with existing systems where possible
- [ ] Violation tracking integrated with SecurityCore
- [ ] All config keys documented
- [ ] All existing tests still pass

---

## 6. T-SF05 COMMIT MESSAGE

```
feat(security): harden service fabric and gateway (T-SF05)

Implements comprehensive security hardening across service fabric:

DHT Descriptor Security:
- Max size limits for descriptors and DHT values
- Signature validation with ban list integration
- Timestamp validation with configurable skew
- Configuration: MaxDescriptorsPerLookup, MaxDescriptorBytes, etc.

MeshServiceRouter Hardening:
- Ban/quarantine checks before service call handling
- Payload size validation per-service and global
- Rate limiting: per-peer, per-service, and global
- Method validation and violation tracking
- Integration with SecurityCore

IMeshServiceClient Hardening:
- Per-request timeouts (configurable)
- Max concurrent calls per peer
- Reputation adjustment on failures
- Safe error handling (no stack traces)

Service-Specific Hardening:
- Pods: message size limits, rate limits, PII protection
- Shadow Index: query/registration limits, MBID validation, anti-poisoning
- Introspection: privacy checks, no sensitive data leaks

Gateway Hardening:
- Service allowlist enforcement
- Body size and timeout limits
- Rate limiting integration
- Safe logging (no bodies by default)

Configuration:
- Centralized config with safe defaults
- Per-service limit overrides
- All limits configurable

Testing:
- Router security tests (oversized, banned, rate limit)
- DHT descriptor validation tests
- Gateway security tests
- Service-specific hardening tests

Part of service fabric initiative (T-SF05).
Security and privacy hardening complete.
```

---

# T-SF06 – DEVELOPER DOCS & EXAMPLES

**Start T-SF06 only after T-SF05 is complete, committed, and stable.**

## 0. SCOPE

### WHAT YOU ARE ALLOWED TO DO:

- Add written documentation (Markdown) that explains:
  - Service fabric concepts
  - How to implement a new `IMeshService`
  - How to publish a service and discover it via DHT
  - How to call services via the HTTP gateway
- Add small, self-contained code examples:
  - Example `IMeshService` implementation
  - Example external script/client that hits the HTTP gateway

### WHAT YOU MUST NOT DO:

- ❌ Change core behavior as part of T-SF06
- ❌ Introduce sample code that contradicts real behavior
- ❌ Add massive demo applications; keep examples small and focused

---

## 1. DOCS STRUCTURE

Follow existing doc layout (e.g., `docs/`, `README`s). At minimum, add:

### 1.1. `docs/service-fabric-overview.md`

**Content**:

```markdown
# Service Fabric Overview

## What is the Service Fabric?

The service fabric is a decentralized service discovery and routing layer built on top of the existing DHT and mesh overlay infrastructure. It enables:

- **Service Discovery**: Peers publish and discover services via DHT
- **Service Routing**: Generic RPC calls routed over mesh overlay
- **External Access**: HTTP gateway for external apps (localhost only)
- **Security**: Privacy-conscious, hardened against abuse

## Core Types

### MeshServiceDescriptor

Describes a service instance:
- `ServiceId`: Deterministic hash (service name + owner peer ID)
- `ServiceName`: Stable functional label (e.g., "pods", "shadow-index")
- `Version`: Semver version string
- `OwnerPeerId`: Peer hosting the service
- `Endpoint`: Overlay-level addressing
- `Metadata`: Small key-value pairs (capped, no PII)
- `Signature`: Ed25519 signature for authenticity
- `CreatedAt/ExpiresAt`: Time window for validity

### IMeshServiceDirectory

Interface for discovering services:
- `FindByNameAsync(serviceName)`: Find all instances of a named service
- `FindByIdAsync(serviceId)`: Find specific service instance

Implementation uses DHT with pattern `svc:<ServiceName>`

### IMeshService

Interface for implementing mesh-exposed services:
- `ServiceName`: Stable name matching descriptor
- `HandleCallAsync(call, context, cancellation)`: Handle request/response
- `HandleStreamAsync(stream, context, cancellation)`: Optional streaming

### ServiceCall / ServiceReply

DTOs for service RPC:
- `ServiceCall`: ServiceId, Method, CorrelationId, Payload
- `ServiceReply`: CorrelationId, StatusCode, Payload

### MeshServiceRouter / IMeshServiceClient

- **Router**: Dispatches incoming calls to registered services
- **Client**: Makes outbound calls to remote services

## Security & Privacy

### DHT Security:
- Signature validation on all descriptors
- Timestamp validation (reject expired/future)
- Ban list integration
- Size limits enforced

### Service Call Security:
- Rate limiting (per-peer, per-service)
- Payload size limits
- Ban/quarantine checks
- Violation tracking

### Privacy Guarantees:
- No PII in service descriptors
- No PII in logs (except with explicit unsafe mode)
- Internal peer IDs only (no IPs or usernames exposed)
- Aggregate stats only (introspection service)

## How Services Are Discovered

1. Service owner creates `MeshServiceDescriptor`
2. Descriptor signed with owner's key
3. Published to DHT at key `svc:<ServiceName>`
4. Other peers query DHT to find services
5. Descriptors validated, filtered (banned peers, expired, invalid signatures)
6. Peer selects descriptor (reputation-aware optional)
7. Calls service via `IMeshServiceClient`

## Available Services

- **pods**: Pod/chat service (Join, Leave, PostMessage, FetchMessages)
- **shadow-index**: MBID index (RegisterTrackLocation, LookupByMbId)
- **mesh-introspect**: Mesh stats (GetStatus, GetPeersSummary, GetCapabilities)
```

### 1.2. `docs/service-implementation-guide.md`

**Content**:

```markdown
# Service Implementation Guide

## Step-by-Step: Creating a New Mesh Service

### 1. Create IMeshService Implementation

```csharp
public class MyCustomService : IMeshService
{
    private readonly ILogger<MyCustomService> _logger;
    private readonly IMyBusinessLogic _businessLogic;
    
    public string ServiceName => "my-custom-service";
    
    public MyCustomService(
        ILogger<MyCustomService> logger,
        IMyBusinessLogic businessLogic)
    {
        _logger = logger;
        _businessLogic = businessLogic;
    }
    
    public async Task<ServiceReply> HandleCallAsync(
        ServiceCall call,
        MeshServiceContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            switch (call.Method)
            {
                case "DoSomething":
                    return await HandleDoSomethingAsync(call, context, cancellationToken);
                case "GetInfo":
                    return await HandleGetInfoAsync(call, context, cancellationToken);
                default:
                    return ServiceReply.Error(404, "Unknown method");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Service error: {Method}", call.Method);
            return ServiceReply.Error(500, "Internal error");
        }
    }
    
    private async Task<ServiceReply> HandleDoSomethingAsync(
        ServiceCall call,
        MeshServiceContext context,
        CancellationToken cancellationToken)
    {
        // 1. Deserialize payload
        var request = _serializer.Deserialize<DoSomethingRequest>(call.Payload);
        
        // 2. Validate input
        if (string.IsNullOrEmpty(request.Parameter))
        {
            return ServiceReply.Error(400, "Invalid parameter");
        }
        
        // 3. Call business logic
        var result = await _businessLogic.DoSomethingAsync(
            request.Parameter, 
            cancellationToken);
        
        // 4. Return response
        return ServiceReply.Success(_serializer.Serialize(new 
        { 
            result = result 
        }));
    }
    
    public Task HandleStreamAsync(
        MeshServiceStream stream,
        MeshServiceContext context,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Streaming not supported");
    }
}
```

### 2. Register with DI Container

In your composition root (e.g., `Startup.cs` or `Program.cs`):

```csharp
services.AddSingleton<IMeshService, MyCustomService>();
services.AddSingleton<IMyBusinessLogic, MyBusinessLogicImpl>();
```

### 3. Register with MeshServiceRouter

The router automatically picks up all `IMeshService` instances:

```csharp
// In composition root, after building service provider:
var router = serviceProvider.GetRequiredService<MeshServiceRouter>();
var services = serviceProvider.GetServices<IMeshService>();
foreach (var service in services)
{
    router.Register(service);
}
```

### 4. Publish Service Descriptor

Create and publish a descriptor for your service:

```csharp
var descriptor = new MeshServiceDescriptor
{
    ServiceId = DeriveServiceId("my-custom-service", myPeerId),
    ServiceName = "my-custom-service",
    Version = "1.0.0",
    OwnerPeerId = myPeerId,
    Endpoint = myEndpoint,
    Metadata = new Dictionary<string, string>
    {
        { "type", "custom" },
        { "version", "1.0.0" }
    },
    CreatedAt = DateTimeOffset.UtcNow,
    ExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
    Signature = SignDescriptor(...)
};

await descriptorPublisher.PublishAsync(descriptor);
```

### 5. Security Considerations

- **Rate Limiting**: Add per-service rate limits in config
- **Payload Size**: Enforce appropriate limits for your service
- **Validation**: Validate all inputs before processing
- **Privacy**: Never log PII or full payloads
- **Error Handling**: Return safe error messages (no stack traces)

Example config:

```jsonc
{
  "MeshServices": {
    "Router": {
      "PerServiceLimits": {
        "my-custom-service": {
          "MaxPayloadBytes": 65536,
          "MaxCallsPerMinute": 50
        }
      }
    }
  }
}
```

### 6. Testing

```csharp
[Test]
public async Task MyCustomService_DoSomething_ReturnsSuccess()
{
    // Arrange
    var mockBusinessLogic = new Mock<IMyBusinessLogic>();
    mockBusinessLogic.Setup(b => b.DoSomethingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync("success");
    var service = new MyCustomService(logger, mockBusinessLogic.Object);
    var call = new ServiceCall
    {
        Method = "DoSomething",
        Payload = Serialize(new { parameter = "test" })
    };
    
    // Act
    var reply = await service.HandleCallAsync(call, context, CancellationToken.None);
    
    // Assert
    Assert.AreEqual(0, reply.StatusCode);
}
```

## Best Practices

1. **Keep services small and focused**: One service, one responsibility
2. **Validate inputs thoroughly**: Never trust incoming payloads
3. **Use existing business logic**: Services are thin adapters
4. **Respect rate limits**: Don't flood the mesh
5. **Log safely**: No PII, no full payloads (unless explicitly debugging)
6. **Test thoroughly**: Unit tests for each method
7. **Document methods**: Clear XML docs for public APIs
```

### 1.3. `docs/http-gateway-usage.md`

**Content**:

```markdown
# HTTP Gateway Usage

## Overview

The HTTP gateway allows external applications to call mesh services via HTTP/WebSocket from localhost.

**Security**: Gateway is disabled by default and localhost-only for security.

## Configuration

Enable the gateway in `appsettings.json`:

```jsonc
{
  "MeshGateway": {
    "Enabled": true,  // MUST enable explicitly
    "BindAddress": "127.0.0.1",  // Localhost only
    "Port": 0,  // Reuse existing port
    "AllowedServices": [
      "pods",
      "shadow-index",
      "mesh-introspect"
    ],
    "MaxRequestBodyBytes": 1048576,  // 1MB
    "RequestTimeoutSeconds": 30,
    "LogBodies": false  // DO NOT enable in production
  }
}
```

### Configuration Options:

- **Enabled**: Enable/disable gateway (default: false)
- **BindAddress**: Bind address (default: "127.0.0.1")
- **AllowedServices**: Whitelist of callable services
- **MaxRequestBodyBytes**: Max request body size (default: 1MB)
- **RequestTimeoutSeconds**: Request timeout (default: 30s)
- **LogBodies**: Log request/response bodies (UNSAFE, default: false)

## HTTP Endpoints

### Route Pattern

```
POST/GET /mesh/http/{serviceName}/{**path}
```

Examples:
- `POST /mesh/http/pods/join`
- `GET /mesh/http/shadow-index/lookup`
- `GET /mesh/http/mesh-introspect/status`

### Request Format

The gateway wraps your HTTP request in a `ServiceCall`:

```json
{
  "httpMethod": "POST",
  "path": "/join",
  "query": { "param": ["value"] },
  "headers": { "Content-Type": ["application/json"] },
  "body": "base64-encoded-body"
}
```

### Response Format

The gateway maps `ServiceReply` to HTTP:
- `StatusCode 0` → `HTTP 200 OK`
- `StatusCode 400` → `HTTP 400 Bad Request`
- `StatusCode 404` → `HTTP 404 Not Found`
- `StatusCode 500` → `HTTP 500 Internal Server Error`
- Other codes → `HTTP 502 Bad Gateway`

## Examples

### Example 1: Join a Pod

```bash
curl -X POST http://localhost:5030/mesh/http/pods/join \
  -H "Content-Type: application/json" \
  -d '{"podId": "general"}'
```

Response:
```json
{
  "success": true,
  "memberCount": 42
}
```

### Example 2: Post a Message

```bash
curl -X POST http://localhost:5030/mesh/http/pods/post \
  -H "Content-Type: application/json" \
  -d '{
    "podId": "general",
    "content": "Hello from external app!"
  }'
```

### Example 3: Fetch Messages

```bash
curl -X GET "http://localhost:5030/mesh/http/pods/messages?podId=general&limit=50"
```

Response:
```json
{
  "messages": [
    {
      "id": "...",
      "content": "Hello!",
      "timestamp": "2025-12-11T19:00:00Z",
      "peerId": "peer:abc123"
    }
  ]
}
```

### Example 4: Shadow Index Lookup

```bash
curl -X GET "http://localhost:5030/mesh/http/shadow-index/lookup?mbid=<musicbrainz-id>"
```

Response:
```json
{
  "results": [
    {
      "trackId": "...",
      "peerId": "peer:xyz789",
      "hints": {
        "quality": "320kbps",
        "format": "mp3"
      }
    }
  ]
}
```

### Example 5: Mesh Status

```bash
curl -X GET http://localhost:5030/mesh/http/mesh-introspect/status
```

Response:
```json
{
  "uptimeSeconds": 123456,
  "connectedPeers": 42,
  "services": ["pods", "shadow-index", "mesh-introspect"]
}
```

## Security Considerations

### Localhost Only

By default, the gateway only accepts connections from localhost (127.0.0.1). To allow remote access:

1. Change `BindAddress` in config
2. Add firewall rules
3. Consider adding authentication

**WARNING**: Exposing the gateway to the network allows any machine to call your mesh services. Only do this in trusted environments.

### Service Allowlist

Only services in `AllowedServices` can be called. To add a new service:

```jsonc
{
  "MeshGateway": {
    "AllowedServices": [
      "pods",
      "shadow-index",
      "mesh-introspect",
      "my-custom-service"  // Add here
    ]
  }
}
```

### Rate Limiting

The gateway enforces rate limits (default: 100 requests/minute per IP). Exceed this and you'll get HTTP 429.

### Body Size Limits

Requests larger than `MaxRequestBodyBytes` are rejected with HTTP 413.

### Timeouts

Requests that take longer than `RequestTimeoutSeconds` are aborted with HTTP 504.

## Troubleshooting

### "404 Not Found" on all gateway endpoints

- Check that `MeshGateway.Enabled = true` in config
- Restart the application after changing config

### "403 Forbidden" when calling a service

- Check that the service is in `AllowedServices` whitelist
- Check spelling/case of service name

### "429 Too Many Requests"

- You've hit the rate limit
- Wait a minute and try again
- Check `MaxRequestsPerMinute` config

### "504 Gateway Timeout"

- Service call took too long
- Check that target service/peer is responsive
- Increase `RequestTimeoutSeconds` if needed

### "502 Bad Gateway"

- Service call failed (network error, timeout, etc.)
- Check logs for details
- Verify target service is available via `mesh-introspect/status`
```

---

## 2. CODE EXAMPLES

### 2.1. Example Mesh Service (Optional)

If you want a dedicated example separate from pods/etc., add `samples/EchoService.cs`:

```csharp
/// <summary>
/// Example mesh service that echoes requests back to caller.
/// For demonstration purposes only.
/// </summary>
public class EchoService : IMeshService
{
    private readonly ILogger<EchoService> _logger;
    
    public string ServiceName => "echo-example";
    
    public EchoService(ILogger<EchoService> logger)
    {
        _logger = logger;
    }
    
    public Task<ServiceReply> HandleCallAsync(
        ServiceCall call,
        MeshServiceContext context,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Echo: {Method} from {PeerId}", 
            call.Method, context.PeerId);
        
        // Simply echo back the payload
        return Task.FromResult(new ServiceReply
        {
            CorrelationId = call.CorrelationId,
            StatusCode = 0,
            Payload = call.Payload
        });
    }
    
    public Task HandleStreamAsync(
        MeshServiceStream stream,
        MeshServiceContext context,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Streaming not supported");
    }
}
```

**Mark it clearly as example-only** and make it optional via config.

### 2.2. Example External Client

Add `samples/mesh-gateway-client/client.py`:

```python
#!/usr/bin/env python3
"""
Example client that calls mesh services via HTTP gateway.
Requires: python3, requests library
Install: pip install requests
"""

import requests
import json
import sys

GATEWAY_URL = "http://localhost:5030"

def call_mesh_service(service_name, path, method="GET", body=None):
    """Call a mesh service via HTTP gateway."""
    url = f"{GATEWAY_URL}/mesh/http/{service_name}/{path}"
    
    try:
        if method == "GET":
            response = requests.get(url, timeout=30)
        elif method == "POST":
            response = requests.post(url, json=body, timeout=30)
        else:
            raise ValueError(f"Unsupported method: {method}")
        
        response.raise_for_status()
        return response.json()
    except requests.exceptions.RequestException as e:
        print(f"Error calling service: {e}", file=sys.stderr)
        sys.exit(1)

def main():
    # Example 1: Get mesh status
    print("=== Mesh Status ===")
    status = call_mesh_service("mesh-introspect", "status")
    print(json.dumps(status, indent=2))
    print()
    
    # Example 2: Join a pod
    print("=== Join Pod ===")
    join_result = call_mesh_service("pods", "join", method="POST", body={
        "podId": "general"
    })
    print(json.dumps(join_result, indent=2))
    print()
    
    # Example 3: Post a message
    print("=== Post Message ===")
    post_result = call_mesh_service("pods", "post", method="POST", body={
        "podId": "general",
        "content": "Hello from Python client!"
    })
    print(json.dumps(post_result, indent=2))
    print()
    
    # Example 4: Fetch messages
    print("=== Fetch Messages ===")
    messages = call_mesh_service("pods", "messages?podId=general&limit=10")
    print(json.dumps(messages, indent=2))

if __name__ == "__main__":
    main()
```

Or add `samples/mesh-gateway-client/client.cs` for C#:

```csharp
// Simple C# client for mesh gateway
// Build: dotnet build
// Run: dotnet run

using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

class Program
{
    private static readonly HttpClient client = new HttpClient 
    { 
        BaseAddress = new Uri("http://localhost:5030") 
    };
    
    static async Task Main(string[] args)
    {
        try
        {
            // Get mesh status
            Console.WriteLine("=== Mesh Status ===");
            var status = await GetAsync("mesh-introspect/status");
            Console.WriteLine(status);
            Console.WriteLine();
            
            // Join a pod
            Console.WriteLine("=== Join Pod ===");
            var joinResult = await PostAsync("pods/join", new 
            { 
                podId = "general" 
            });
            Console.WriteLine(joinResult);
            Console.WriteLine();
            
            // Post a message
            Console.WriteLine("=== Post Message ===");
            var postResult = await PostAsync("pods/post", new 
            { 
                podId = "general",
                content = "Hello from C# client!"
            });
            Console.WriteLine(postResult);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
        }
    }
    
    static async Task<string> GetAsync(string path)
    {
        var response = await client.GetAsync($"/mesh/http/{path}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }
    
    static async Task<string> PostAsync(string path, object body)
    {
        var json = JsonSerializer.Serialize(body);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await client.PostAsync($"/mesh/http/{path}", content);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }
}
```

**Requirements for examples**:
- Must compile / run if part of solution
- Match actual API shapes and config keys
- As small and straightforward as possible
- Clearly marked as examples

---

## 3. SYNCHRONIZE WITH TASK_STATUS_DASHBOARD.md

If the project uses `TASK_STATUS_DASHBOARD.md` or similar, add entries:

```markdown
## Service Fabric Implementation (T-SF01 through T-SF07)

### Status: ✅ Complete

The service fabric layer is fully implemented and documented:

- **T-SF01**: Service descriptors and DHT directory - Complete
- **T-SF02**: Service routing and RPC layer - Complete
- **T-SF03**: Wrapped existing features (pods, VirtualSoulfind, stats) - Complete
- **T-SF04**: HTTP/WebSocket gateway - Complete
- **T-SF05**: Security and abuse hardening - Complete
- **T-SF06**: Developer documentation and examples - Complete
- **T-SF07**: Observability and metrics - Complete

### Documentation:

- [Service Fabric Overview](docs/service-fabric-overview.md)
- [Service Implementation Guide](docs/service-implementation-guide.md)
- [HTTP Gateway Usage](docs/http-gateway-usage.md)

### Examples:

- [Example Mesh Service](samples/EchoService.cs)
- [Python Gateway Client](samples/mesh-gateway-client/client.py)
- [C# Gateway Client](samples/mesh-gateway-client/client.cs)
```

---

## 4. T-SF06 ANTI-SLOP CHECKLIST

Before saying T-SF06 is done:

- [ ] Docs match actual code and config; no hand-wavey pseudo-APIs
- [ ] Examples are minimal, not entire frameworks
- [ ] No behavior changes were introduced as part of "writing docs"
- [ ] No new dependencies for examples unless absolutely trivial and justified
- [ ] Comments are clear and correct; no misleading statements
- [ ] All examples compile/run successfully
- [ ] Documentation reviewed for accuracy
- [ ] Links work and point to correct files

---

## 5. T-SF06 COMMIT MESSAGE

```
docs: add service fabric documentation and examples (T-SF06)

Adds comprehensive developer documentation:

Documentation:
- docs/service-fabric-overview.md: Architecture and concepts
- docs/service-implementation-guide.md: Step-by-step guide for new services
- docs/http-gateway-usage.md: Gateway configuration and usage examples

Code Examples:
- samples/EchoService.cs: Example mesh service implementation
- samples/mesh-gateway-client/client.py: Python HTTP gateway client
- samples/mesh-gateway-client/client.cs: C# HTTP gateway client

Updates:
- TASK_STATUS_DASHBOARD.md: Service fabric completion status and links

All examples tested and verified against actual implementation.
Documentation matches real code and config keys.

Part of service fabric initiative (T-SF06).
Developer documentation complete.
```

---

# T-SF07 – OBSERVABILITY & METRICS

**Start T-SF07 only after T-SF06 is complete, committed, and stable.**

## 0. SCOPE

### WHAT YOU ARE ALLOWED TO DO:

- Add metrics / counters / timers using whatever observability system the project already uses:
  - For service calls and replies
  - For DHT descriptor operations
  - For HTTP gateway usage
- Add minimal logging improvements where they fill gaps (not spam)

### WHAT YOU MUST NOT DO:

- ❌ Introduce a whole new metrics stack if one already exists
- ❌ Emit extremely high-volume logs or metrics that will explode cardinality

---

## 1. RECONNAISSANCE

Find:
- **Existing metrics/observability patterns**:
  - Counters: how they are defined and exported
  - Timers / histograms, if used
  - Metrics provider/library (Prometheus, App Metrics, custom, etc.)
- **Existing logging conventions**:
  - Log levels
  - Structured/log message formats
  - Logging provider (Serilog, NLog, Microsoft.Extensions.Logging, etc.)

**Use the same framework and style.**

---

## 2. METRICS TO ADD

### 2.1. Service Fabric (Router & Client)

#### Router Metrics

Define and instrument:

```csharp
// Counters
private static readonly Counter<long> _serviceCallsTotal = Meter.CreateCounter<long>(
    "mesh.service.calls.total",
    description: "Total number of incoming service calls");

private static readonly Counter<long> _serviceCallErrorsTotal = Meter.CreateCounter<long>(
    "mesh.service.call_errors.total",
    description: "Total number of service call errors");

// Histogram
private static readonly Histogram<double> _serviceCallDuration = Meter.CreateHistogram<double>(
    "mesh.service.call_duration_ms",
    unit: "ms",
    description: "Duration of service call handling in milliseconds");
```

**Emit metrics**:

```csharp
public async Task<ServiceReply> HandleCallAsync(ServiceCall call, MeshServiceContext context)
{
    var sw = Stopwatch.StartNew();
    ServiceReply reply;
    
    try
    {
        reply = await DispatchToServiceAsync(call, context);
        
        // Success metric
        _serviceCallsTotal.Add(1, new TagList
        {
            { "service", call.ServiceId },
            { "result", "success" }
        });
    }
    catch (Exception ex)
    {
        // Error metric
        _serviceCallErrorsTotal.Add(1, new TagList
        {
            { "service", call.ServiceId },
            { "error_type", GetErrorType(ex) }
        });
        
        reply = ServiceReply.Error(500, "Internal error");
    }
    finally
    {
        sw.Stop();
        
        // Duration metric
        _serviceCallDuration.Record(sw.Elapsed.TotalMilliseconds, new TagList
        {
            { "service", call.ServiceId }
        });
    }
    
    return reply;
}
```

**Labels**:
- `service`: Service name (not full peer ID to control cardinality)
- `result`: success | error | rate_limited | banned | validation_failed
- `error_type`: timeout | network_error | validation_failed | rate_limited

#### Client Metrics

Define and instrument:

```csharp
private static readonly Counter<long> _serviceClientCallsTotal = Meter.CreateCounter<long>(
    "mesh.service.client.calls.total",
    description: "Total number of outgoing service calls");

private static readonly Histogram<double> _serviceClientCallDuration = Meter.CreateHistogram<double>(
    "mesh.service.client.call_duration_ms",
    unit: "ms",
    description: "Duration of outbound service calls in milliseconds");
```

**Emit metrics**:

```csharp
public async Task<ServiceReply> CallAsync(
    MeshServiceDescriptor target,
    string method,
    ReadOnlyMemory<byte> payload,
    CancellationToken cancellationToken)
{
    var sw = Stopwatch.StartNew();
    string outcome = "success";
    
    try
    {
        var reply = await SendAndWaitForReplyAsync(target, method, payload, cancellationToken);
        return reply;
    }
    catch (TimeoutException)
    {
        outcome = "timeout";
        throw;
    }
    catch (Exception)
    {
        outcome = "error";
        throw;
    }
    finally
    {
        sw.Stop();
        
        _serviceClientCallsTotal.Add(1, new TagList
        {
            { "service", target.ServiceName },
            { "outcome", outcome }
        });
        
        _serviceClientCallDuration.Record(sw.Elapsed.TotalMilliseconds, new TagList
        {
            { "service", target.ServiceName }
        });
    }
}
```

**Labels**:
- `service`: Service name
- `outcome`: success | timeout | network_error | error

**Keep label cardinality under control: do NOT include full peer IDs.**

### 2.2. DHT Service Descriptor Operations

Define and instrument:

```csharp
private static readonly Counter<long> _descriptorPublishTotal = Meter.CreateCounter<long>(
    "mesh.service.descriptor.publish.total",
    description: "Total number of service descriptor publishes");

private static readonly Counter<long> _descriptorLookupTotal = Meter.CreateCounter<long>(
    "mesh.service.descriptor.lookup.total",
    description: "Total number of service descriptor lookups");

private static readonly Histogram<long> _descriptorEntriesReturned = Meter.CreateHistogram<long>(
    "mesh.service.descriptor.entries_returned",
    description: "Number of descriptor entries returned per lookup");
```

**Emit metrics**:

```csharp
public async Task PublishAsync(MeshServiceDescriptor descriptor)
{
    try
    {
        await PublishToDhtAsync(descriptor);
        
        _descriptorPublishTotal.Add(1, new TagList
        {
            { "service", descriptor.ServiceName },
            { "result", "success" }
        });
    }
    catch (Exception)
    {
        _descriptorPublishTotal.Add(1, new TagList
        {
            { "service", descriptor.ServiceName },
            { "result", "error" }
        });
        throw;
    }
}

public async Task<IReadOnlyList<MeshServiceDescriptor>> FindByNameAsync(
    string serviceName,
    CancellationToken cancellationToken)
{
    IReadOnlyList<MeshServiceDescriptor> results;
    
    try
    {
        results = await LookupFromDhtAsync(serviceName, cancellationToken);
        
        _descriptorLookupTotal.Add(1, new TagList
        {
            { "service", serviceName },
            { "result", results.Any() ? "found" : "not_found" }
        });
        
        _descriptorEntriesReturned.Record(results.Count, new TagList
        {
            { "service", serviceName }
        });
    }
    catch (Exception)
    {
        _descriptorLookupTotal.Add(1, new TagList
        {
            { "service", serviceName },
            { "result", "error" }
        });
        throw;
    }
    
    return results;
}
```

**Labels**:
- `service`: Service name
- `result`: success | error | found | not_found

### 2.3. HTTP Gateway

Define and instrument:

```csharp
private static readonly Counter<long> _gatewayRequestsTotal = Meter.CreateCounter<long>(
    "mesh.gateway.requests.total",
    description: "Total number of HTTP gateway requests");

private static readonly Histogram<double> _gatewayRequestDuration = Meter.CreateHistogram<double>(
    "mesh.gateway.request_duration_ms",
    unit: "ms",
    description: "Duration of gateway requests in milliseconds");

private static readonly Histogram<long> _gatewayRequestBodyBytes = Meter.CreateHistogram<long>(
    "mesh.gateway.request_body_bytes",
    unit: "bytes",
    description: "Size of gateway request bodies");
```

**Emit metrics**:

```csharp
[HttpPost("/mesh/http/{serviceName}/{**path}")]
public async Task<IActionResult> InvokeService(string serviceName, string path)
{
    var sw = Stopwatch.StartNew();
    var result = "success";
    var statusCode = 200;
    
    try
    {
        // Read body
        var body = await ReadBodyAsync();
        _gatewayRequestBodyBytes.Record(body.Length, new TagList
        {
            { "service", serviceName }
        });
        
        // Handle request
        var reply = await CallMeshServiceAsync(serviceName, path, body);
        statusCode = MapStatusCode(reply.StatusCode);
        
        return StatusCode(statusCode, DeserializePayload(reply.Payload));
    }
    catch (TimeoutException)
    {
        result = "timeout";
        statusCode = 504;
        return StatusCode(504, new { error = "Gateway timeout" });
    }
    catch (Exception)
    {
        result = "server_error";
        statusCode = 502;
        return StatusCode(502, new { error = "Mesh call failed" });
    }
    finally
    {
        sw.Stop();
        
        _gatewayRequestsTotal.Add(1, new TagList
        {
            { "method", Request.Method },
            { "service", serviceName },
            { "result", result },
            { "status_code", statusCode.ToString() }
        });
        
        _gatewayRequestDuration.Record(sw.Elapsed.TotalMilliseconds, new TagList
        {
            { "method", Request.Method },
            { "service", serviceName }
        });
    }
}
```

**Labels**:
- `method`: HTTP method (GET, POST, etc.)
- `service`: Service name
- `result`: success | client_error | server_error | timeout | rejected_by_policy
- `status_code`: HTTP status code

**Keep label sets small and bounded.**

---

## 3. LOGGING ENHANCEMENTS (MINIMAL)

Add or adjust log lines where they add value:

### 3.1. Service Call Rejection

```csharp
// On rate limit rejection
logger.LogWarning(
    "Service call rate limited: service={ServiceName}, peer={PeerId}, limit={Limit}",
    serviceName, context.PeerId, maxCallsPerMinute);

// On banned peer rejection
logger.LogWarning(
    "Service call rejected: peer {PeerId} is banned/quarantined",
    context.PeerId);

// On oversized payload rejection
logger.LogWarning(
    "Service call rejected: payload too large ({Size} > {Max}) from {PeerId}",
    payload.Length, maxPayloadSize, context.PeerId);
```

### 3.2. DHT Descriptor Validation Failure

```csharp
// On invalid signature
logger.LogWarning(
    "DHT descriptor rejected: invalid signature for service={ServiceName}, owner={OwnerId}",
    descriptor.ServiceName, descriptor.OwnerPeerId);

// On expired descriptor
logger.LogInformation(
    "DHT descriptor expired: service={ServiceName}, expired={ExpiresAt}",
    descriptor.ServiceName, descriptor.ExpiresAt);

// On oversized descriptor
logger.LogWarning(
    "DHT descriptor rejected: size too large ({Size} > {Max}) for service={ServiceName}",
    descriptorSize, maxDescriptorSize, descriptor.ServiceName);
```

### 3.3. Gateway Errors

```csharp
// On timeout
logger.LogWarning(
    "Gateway timeout: {Method} {ServiceName}/{Path} from {IP}",
    httpMethod, serviceName, path, remoteIp);

// On oversized body
logger.LogWarning(
    "Gateway rejected oversized body: {Size} > {Max} from {IP}",
    bodySize, maxBodySize, remoteIp);

// On non-allowed service
logger.LogWarning(
    "Gateway rejected non-allowed service: {ServiceName} from {IP}",
    serviceName, remoteIp);
```

### Rules for Logging:

**DO NOT**:
- ❌ Log full bodies or large payloads
- ❌ Log secrets, tokens, or private keys
- ❌ Spam logs for frequent, expected events (rate-limited at a minimum)

**DO**:
- ✅ Log serviceName, peer ID, reason
- ✅ Log high-level outcome (success/failure)
- ✅ Log error categories, not details

---

## 4. CONFIGURATION FOR METRICS (OPTIONAL)

If the project has toggles for metrics, ensure new metrics respect them:

```jsonc
{
  "Metrics": {
    "Enabled": true,
    "Provider": "Prometheus",  // or "AppMetrics", "OpenTelemetry", etc.
    "IncludeServiceFabric": true,
    "IncludeGateway": true
  }
}
```

**If you add new config keys**:
- Document them
- Set safe defaults (`enabled = true` if project already assumes metrics, or `false` if new and potentially heavy)

---

## 5. TESTING FOR T-SF07

You do not need exhaustive tests, but:

### 5.1. Metrics Tests

```csharp
[Test]
public async Task Router_EmitsServiceCallMetric()
{
    // Arrange
    var mockMeter = new TestMeterProvider();
    var router = CreateRouterWithMetrics(mockMeter);
    
    // Act
    await router.HandleCallAsync(call, context);
    
    // Assert
    var counter = mockMeter.GetCounter("mesh.service.calls.total");
    Assert.AreEqual(1, counter.Value);
    Assert.AreEqual("test-service", counter.Tags["service"]);
    Assert.AreEqual("success", counter.Tags["result"]);
}

[Test]
public async Task Client_EmitsCallDurationMetric()
{
    // Arrange
    var mockMeter = new TestMeterProvider();
    var client = CreateClientWithMetrics(mockMeter);
    
    // Act
    await client.CallAsync(descriptor, "Test", payload);
    
    // Assert
    var histogram = mockMeter.GetHistogram("mesh.service.client.call_duration_ms");
    Assert.Greater(histogram.LastValue, 0);
}
```

### 5.2. Logging Tests

```csharp
[Test]
public async Task Router_LogsRateLimitViolation()
{
    // Arrange
    var mockLogger = new TestLogger<MeshServiceRouter>();
    var router = CreateRouter(mockLogger);
    
    // Act: Make too many calls to trigger rate limit
    for (int i = 0; i <= maxCallsPerMinute; i++)
    {
        await router.HandleCallAsync(call, context);
    }
    
    // Assert
    var warningLogs = mockLogger.GetLogs(LogLevel.Warning);
    Assert.IsTrue(warningLogs.Any(log => log.Contains("rate limited")));
}
```

Use existing patterns for testing logging/metrics if they exist; otherwise, keep it light.

---

## 6. T-SF07 ANTI-SLOP CHECKLIST

Before you consider T-SF07 done:

- [ ] Metrics use the same framework and style as existing ones
- [ ] Cardinality is controlled (no unbounded labels like full peer IDs)
- [ ] No blocking or heavy work is done in the path of metrics emission
- [ ] Logs are informative but not excessive or privacy-violating
- [ ] No behavior changes were accidentally introduced as part of adding metrics/logging
- [ ] All metrics have clear descriptions
- [ ] All metrics follow naming conventions
- [ ] Tests verify metrics are emitted correctly

---

## 7. T-SF07 COMMIT MESSAGE

```
feat(observability): add metrics and logging for service fabric (T-SF07)

Implements comprehensive observability:

Service Fabric Metrics:
- mesh.service.calls.total: Incoming service call counter
- mesh.service.call_errors.total: Service call error counter
- mesh.service.call_duration_ms: Call handling duration histogram
- mesh.service.client.calls.total: Outgoing call counter
- mesh.service.client.call_duration_ms: Outbound call duration

DHT Descriptor Metrics:
- mesh.service.descriptor.publish.total: Descriptor publish counter
- mesh.service.descriptor.lookup.total: Descriptor lookup counter
- mesh.service.descriptor.entries_returned: Entries per lookup histogram

Gateway Metrics:
- mesh.gateway.requests.total: HTTP gateway request counter
- mesh.gateway.request_duration_ms: Gateway request duration
- mesh.gateway.request_body_bytes: Request body size histogram

Logging Enhancements:
- Service call rejections (rate limit, banned peer, oversized payload)
- DHT descriptor validation failures
- Gateway errors and timeouts
- All logs respect privacy (no PII, no full payloads)

Cardinality Control:
- Labels use service names, not full peer IDs
- Bounded label sets for all metrics
- No high-cardinality dimensions

Testing:
- Metrics emission tests
- Logging tests for key scenarios

Part of service fabric initiative (T-SF07).
Observability complete.
```

---

# FINAL NOTES FOR ALL THREE TASKS

## Work Incrementally:

1. **Complete T-SF05** (Security & Hardening)
   - Commit with message from T-SF05 section
   - Verify all tests pass
   - Verify no regressions

2. **Complete T-SF06** (Documentation & Examples)
   - Commit with message from T-SF06 section
   - Verify docs are accurate
   - Verify examples compile/run

3. **Complete T-SF07** (Observability & Metrics)
   - Commit with message from T-SF07 section
   - Verify metrics are emitted
   - Verify no performance impact

## At Each Stage:

- ✅ Build passes
- ✅ Tests pass
- ✅ Existing behavior unchanged from user's perspective
- ✅ Anti-slop checklist satisfied
- ✅ Small, focused, reviewable diffs

## Final Success Criteria:

When all three tasks are complete:

- [ ] Service fabric is hardened against abuse (T-SF05)
- [ ] Developers can implement new services (T-SF06)
- [ ] Operators have visibility into fabric health (T-SF07)
- [ ] All documentation is accurate and complete
- [ ] All examples work correctly
- [ ] All metrics are useful and non-invasive
- [ ] Security, privacy, and quality maintained throughout

---

**Implement these tasks strictly according to this brief, with security, privacy, and clarity prioritized over cleverness.**

---

*Last Updated: December 11, 2025*  
*Branch: experimental/multi-source-swarm*  
*Status: 📋 Ready for Implementation*
