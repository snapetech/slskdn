# T-SF03 Implementation Brief: Wrap Existing Features as Mesh Services

**Repository**: `https://github.com/snapetech/slskdn`  
**Branch**: `experimental/multi-source-swarm`  
**Prerequisites**: T-SF01 (service descriptors + directory) AND T-SF02 (router + client) completed  
**Status**: 📋 Ready for Implementation  
**Created**: December 11, 2025

> **Project Note**: This is a fork of [slskd](https://github.com/slskd/slskd). See [README.md](README.md#acknowledgments) for attribution.

---

## CRITICAL: SCOPE CONSTRAINTS

### WHAT YOU MUST DO IN T-SF03:

1. Implement concrete `IMeshService` implementations for:
   - **Pods / Chat** (rooms/channels)
   - **VirtualSoulfind / MBID Shadow Index**
   - **Mesh Introspection / Stats**

2. Integrate these services with:
   - `MeshServiceRouter` (register services)
   - Service descriptor publishing (DHT discovery)

3. Add minimal, focused tests for these adapters

### WHAT YOU MUST NOT DO IN T-SF03:

- ❌ Implement or modify HTTP or WebSocket gateways (that's T-SF04)
- ❌ Invent new protocol semantics for pods/VirtualSoulfind (wrap what exists)
- ❌ Change underlying DHT keyspaces or storage/layout (except where strictly required)
- ❌ Remove existing overlay handlers until new path is proven
- ❌ Break existing functionality

**Your changes must compile, pass existing tests, and preserve current behavior from the outside.**

---

## 1. REQUIRED RECONNAISSANCE

**BEFORE implementing anything**, you MUST locate and understand:

### 1.1. Pod / Chat Subsystem

Find:
- Types handling pods, channels, and messages
- Storage layer for pods (SQLite / whatever is used)
- Current message flow:
  - How pod messages are sent/received across the mesh
  - Any existing Soulseek room bridging
- Any rate limiting or anti-abuse logic around pods

Questions to answer:
- What are the existing pod message types?
- How are pod members tracked?
- What security/permissions exist for pods?
- Where are messages stored?

### 1.2. VirtualSoulfind / MBID Shadow Index

Find:
- Types representing MBIDs, tracks, and "shadow index" records
- How MBID → track/peer mapping is stored and looked up
- How it uses the DHT today (key patterns, value structures)
- How Soulseek search and your index interact

Questions to answer:
- What is the current DHT key pattern for MBID data?
- How are track locations registered?
- What validation exists for MBIDs?
- Are there any rate limits on registration or queries?

### 1.3. Mesh Stats / Introspection

Find:
- Where mesh stats / health / peer summaries are generated
- Any existing HTTP endpoints or internal APIs for this
- What metrics are currently tracked

Questions to answer:
- What stats are available?
- What's safe to expose to other peers?
- What contains PII or sensitive data?

### 1.4. Service Router & Descriptor Publisher (from T-SF01/02)

Find:
- Where `IMeshService` implementations are expected to be registered
- How services are currently exposed in `MeshServiceDescriptor` and published to the DHT

Questions to answer:
- Where do I register my new services?
- How do I create and publish a descriptor?
- What's the DI pattern for services?

**DO NOT proceed until you have clear answers to these questions.**

---

## 2. DESIGN PHILOSOPHY FOR T-SF03

You're NOT designing a new protocol; you're creating **ADAPTERS**.

Each existing subsystem gets a **thin `IMeshService` wrapper**:

**The wrapper**:
- Converts `ServiceCall` + `Payload` → existing internal call(s)
- Converts results → `ServiceReply`
- Respects existing security / rate limits / logging

Think **"façade"** or **"service front-end"**, NOT **"rewrite"**.

---

## 3. POD / CHAT SERVICE

### 3.1. Define the Service Contract

**ServiceName**: Choose a stable name, e.g.:
- `"pods"` or `"mesh-pods"`

**Methods** (at minimum):
- `Join` – join a pod/room
- `Leave` – leave a pod/room
- `PostMessage` – post a message into a pod
- `FetchMessages` – fetch recent messages or since a cursor

**Payload Schemas** (conceptual, adapt to project's serializer):

```jsonc
// Join
{
  "podId": "string-or-guid",
  "options": { /* optional */ }
}

// PostMessage
{
  "podId": "string-or-guid",
  "content": "message body",
  "timestamp": "optional client timestamp"
}

// FetchMessages
{
  "podId": "string-or-guid",
  "since": "optional cursor or timestamp",
  "limit": 100
}
```

**Constraints**:
- Do NOT expose Soulseek usernames or external IDs in DHT or service descriptor metadata
- Use internal pod IDs; mapping to human names stays in existing layers

### 3.2. Implement `PodsMeshService : IMeshService`

**Responsibilities**:

```csharp
public class PodsMeshService : IMeshService
{
    public string ServiceName => "pods"; // or "mesh-pods"

    public async Task<ServiceReply> HandleCallAsync(
        ServiceCall call,
        MeshServiceContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            switch (call.Method)
            {
                case "Join":
                    return await HandleJoinAsync(call, context, cancellationToken);
                case "Leave":
                    return await HandleLeaveAsync(call, context, cancellationToken);
                case "PostMessage":
                    return await HandlePostMessageAsync(call, context, cancellationToken);
                case "FetchMessages":
                    return await HandleFetchMessagesAsync(call, context, cancellationToken);
                default:
                    return ServiceReply.Error(404, "Unknown method");
            }
        }
        catch (Exception ex)
        {
            // Log locally, return safe error
            logger.LogError(ex, "Pod service error: {Method}", call.Method);
            return ServiceReply.Error(500, "Internal error");
        }
    }

    private async Task<ServiceReply> HandleJoinAsync(...)
    {
        // 1. Deserialize call.Payload into JoinRequest
        // 2. Call existing pod subsystem: await podManager.JoinAsync(...)
        // 3. Return ServiceReply with serialized result
    }

    // Similar for other methods...
}
```

**Security & Privacy**:
- Enforce any existing pod-level permissions
- Respect message size limits; reject overly large content with appropriate error status
- Do NOT log full message content; log only: pod ID + peer ID + high-level outcome

**Integration**:
- Register `PodsMeshService` with `MeshServiceRouter`
- Ensure a matching `MeshServiceDescriptor` is published for this node

---

## 4. VIRTUALSOULFIND / MBID INDEX SERVICE

### 4.1. Define the Service Contract

**ServiceName**: Choose a stable name, e.g.:
- `"shadow-index"` or `"mbid-index"`

**Methods** (typical):
- `RegisterTrackLocation` – register/update track locations for a given MBID
- `LookupByMbId` – query tracks/locations by MBID
- `LookupByTrackId` – (if relevant) query by internal track ID

**Payload Schemas** (conceptual):

```jsonc
// RegisterTrackLocation
{
  "mbid": "MusicBrainz-ID-or-equivalent",
  "trackId": "internal-track-id-or-hash",
  "hints": {
    "quality": "320kbps",
    "format": "mp3"
  }
}

// LookupByMbId
{
  "mbid": "MusicBrainz-ID-or-equivalent",
  "limit": 50
}

// Response
{
  "results": [
    {
      "trackId": "...",
      "peerId": "...",
      "hints": { ... }
    }
  ]
}
```

**Important**:
- Do NOT introduce new DHT key schemes here unless absolutely required
- Use the same MBID and content keying you already use internally

### 4.2. Implement `VirtualSoulfindMeshService : IMeshService`

**Responsibilities**:

```csharp
public class VirtualSoulfindMeshService : IMeshService
{
    public string ServiceName => "shadow-index"; // or "mbid-index"

    public async Task<ServiceReply> HandleCallAsync(
        ServiceCall call,
        MeshServiceContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            switch (call.Method)
            {
                case "RegisterTrackLocation":
                    return await HandleRegisterAsync(call, context, cancellationToken);
                case "LookupByMbId":
                    return await HandleLookupAsync(call, context, cancellationToken);
                case "LookupByTrackId":
                    return await HandleTrackLookupAsync(call, context, cancellationToken);
                default:
                    return ServiceReply.Error(404, "Unknown method");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Shadow index service error: {Method}", call.Method);
            return ServiceReply.Error(500, "Internal error");
        }
    }

    private async Task<ServiceReply> HandleRegisterAsync(...)
    {
        // 1. Deserialize call.Payload into RegisterRequest
        // 2. Validate MBID format (reject malformed)
        // 3. Call existing VirtualSoulfind logic: await indexer.RegisterAsync(...)
        // 4. Return ServiceReply
    }

    // Similar for other methods...
}
```

**Security & Acceptance Rules**:
- Validate MBID and IDs for basic sanity before calling deeper layers:
  - Reject obviously malformed IDs
- Apply the same rate limits you would expect for search/index endpoints:
  - Per-peer query limits
  - Per-peer registration limits to avoid spam
- If there is existing anti-poisoning logic for the index, reuse it here

**Privacy**:
- Do NOT leak other peers' IPs or Soulseek usernames in the service payloads
- Use internal peer IDs or abstract location references where required

**Integration**:
- Register `VirtualSoulfindMeshService` in `MeshServiceRouter`
- Ensure a `MeshServiceDescriptor` is published so other nodes can discover index providers

---

## 5. MESH INTROSPECTION / STATS SERVICE

### 5.1. Define the Service Contract

**ServiceName**: Choose a stable name, e.g.:
- `"mesh-introspect"` or `"mesh-stats"`

**Methods**:
- `GetStatus` – high-level health summary
- `GetPeersSummary` – counts and basic peer stats
- `GetCapabilities` – what this node offers

**Payload Schemas** (conceptual):

```jsonc
// GetStatus request (empty or minimal)
{}

// GetStatus response
{
  "uptimeSeconds": 123456,
  "connectedPeers": 42,
  "services": ["pods", "shadow-index"]
}

// GetPeersSummary response
{
  "totalPeers": 42,
  "byStatus": {
    "connected": 30,
    "connecting": 5,
    "disconnected": 7
  }
}

// GetCapabilities response
{
  "services": ["pods", "shadow-index", "mesh-stats"],
  "features": ["multi-source", "rescue-mode"]
}
```

**Constraints**:
- Do NOT expose OS usernames, hostnames, filesystem paths, or public IP addresses
- Keep it generic and safe enough to hand to any peer

### 5.2. Implement `MeshIntrospectionService : IMeshService`

**Responsibilities**:

```csharp
public class MeshIntrospectionService : IMeshService
{
    public string ServiceName => "mesh-stats"; // or "mesh-introspect"

    public async Task<ServiceReply> HandleCallAsync(
        ServiceCall call,
        MeshServiceContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            switch (call.Method)
            {
                case "GetStatus":
                    return await HandleGetStatusAsync(call, context, cancellationToken);
                case "GetPeersSummary":
                    return await HandleGetPeersSummaryAsync(call, context, cancellationToken);
                case "GetCapabilities":
                    return await HandleGetCapabilitiesAsync(call, context, cancellationToken);
                default:
                    return ServiceReply.Error(404, "Unknown method");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Introspection service error: {Method}", call.Method);
            return ServiceReply.Error(500, "Internal error");
        }
    }

    private async Task<ServiceReply> HandleGetStatusAsync(...)
    {
        // 1. Gather stats from existing metrics sources
        // 2. Build safe, aggregate response (no PII)
        // 3. Serialize and return in ServiceReply
    }

    // Similar for other methods...
}
```

**Notes**:
- Use the existing statistic/metrics sources to populate responses
- Integrate with router like other services
- This service is read-only and low-risk; keep it simple

---

## 6. SERVICE REGISTRATION AND DESCRIPTOR PUBLISHING

For each new service:

### 6.1. Consistent ServiceName

Ensure there is a consistent `ServiceName` used:
- By `IMeshService.ServiceName`
- In `MeshServiceDescriptor.ServiceName`
- In the DHT key for `svc:<ServiceName>`

### 6.2. Registration in DI Container / Composition Root

Where services are constructed/registered, add:

```csharp
// Register the service implementations
services.AddSingleton<IMeshService, PodsMeshService>();
services.AddSingleton<IMeshService, VirtualSoulfindMeshService>();
services.AddSingleton<IMeshService, MeshIntrospectionService>();

// Register with router (or however your T-SF02 implementation does it)
// Example:
var router = serviceProvider.GetRequiredService<MeshServiceRouter>();
router.Register(serviceProvider.GetServices<IMeshService>());

// Publish descriptors
var publisher = serviceProvider.GetRequiredService<IMeshServiceDescriptorPublisher>();
await publisher.PublishAsync(new MeshServiceDescriptor
{
    ServiceId = DeriveServiceId("pods", myPeerId),
    ServiceName = "pods",
    Version = "1.0.0",
    OwnerPeerId = myPeerId,
    Endpoint = myEndpoint,
    Metadata = new Dictionary<string, string>
    {
        { "type", "chat" }
    },
    CreatedAt = DateTimeOffset.UtcNow,
    ExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
    Signature = Sign(...)
});
// Similar for other services...
```

### 6.3. Verification

Verify that:
- Each new service appears in the node's own published service list
- `IMeshServiceDirectory` can discover them locally via `FindByNameAsync`

**Do NOT introduce new ad-hoc DHT writes for these services; use the T-SF01 infrastructure.**

---

## 7. TRANSITION FROM LEGACY PATHS

Because you are wrapping existing behavior, you need to ensure:

### 7.1. Preserve Existing Call Paths

**Existing call paths** (direct overlay handlers, direct method invocations) must continue to work for now.

**The new services can be used in parallel.**

### 7.2. Minimal Transition Steps in T-SF03

**Option A: Route through service layer (if low-risk)**

If overlay messages were previously dispatched directly to, for example, a pod handler:
- Consider routing them through `MeshServiceRouter` instead, if it is low-risk and clearly correct

**Option B: Keep legacy paths intact (if risky)**

If routing through service layer is risky:
- Leave legacy handlers intact
- Only add service-based access for now
- Add TODO markers for future consolidation

### 7.3. TODO Markers

Add specific (NOT vague) TODO markers where a future task can cleanly remove or consolidate legacy paths once the service approach is proven:

```csharp
// TODO(T-SF05): Remove legacy pod message handler once service path is proven in production
// See: PodsMeshService.HandlePostMessageAsync for new implementation
```

**DO NOT rip out or disable existing behavior in T-SF03 unless you are absolutely sure it is safe and backwards compatible.**

---

## 8. TESTING

You must add or update tests to cover:

### 8.1. Pods Service Tests

**Unit tests**:
```csharp
[Test]
public async Task PodsMeshService_Join_CallsUnderlyingJoinMethod()
{
    // Arrange
    var mockPodManager = new Mock<IPodManager>();
    var service = new PodsMeshService(mockPodManager.Object, ...);
    var call = new ServiceCall
    {
        Method = "Join",
        Payload = Serialize(new { podId = "test-pod" })
    };

    // Act
    var reply = await service.HandleCallAsync(call, context, CancellationToken.None);

    // Assert
    Assert.AreEqual(0, reply.StatusCode);
    mockPodManager.Verify(m => m.JoinAsync("test-pod", It.IsAny<CancellationToken>()), Times.Once);
}

[Test]
public async Task PodsMeshService_PostMessage_ReturnsCorrectReply()
{
    // Similar test for PostMessage
}
```

### 8.2. VirtualSoulfind Service Tests

**Unit tests**:
```csharp
[Test]
public async Task VirtualSoulfindService_LookupByMbId_ReturnsKnownData()
{
    // Arrange
    var mockIndexer = new Mock<IVirtualSoulfindIndexer>();
    mockIndexer.Setup(m => m.LookupAsync("known-mbid", It.IsAny<CancellationToken>()))
               .ReturnsAsync(new[] { /* test data */ });
    var service = new VirtualSoulfindMeshService(mockIndexer.Object, ...);
    var call = new ServiceCall
    {
        Method = "LookupByMbId",
        Payload = Serialize(new { mbid = "known-mbid" })
    };

    // Act
    var reply = await service.HandleCallAsync(call, context, CancellationToken.None);

    // Assert
    Assert.AreEqual(0, reply.StatusCode);
    // Deserialize and verify payload contains expected data
}

[Test]
public async Task VirtualSoulfindService_RejectsMalformedMbid()
{
    // Verify that malformed MBIDs are rejected with appropriate error
}
```

### 8.3. Mesh Introspection Service Tests

**Unit tests**:
```csharp
[Test]
public async Task MeshIntrospectionService_GetStatus_ReturnsValidData()
{
    // Arrange
    var service = new MeshIntrospectionService(...);
    var call = new ServiceCall { Method = "GetStatus", Payload = Array.Empty<byte>() };

    // Act
    var reply = await service.HandleCallAsync(call, context, CancellationToken.None);

    // Assert
    Assert.AreEqual(0, reply.StatusCode);
    var status = Deserialize<StatusResponse>(reply.Payload);
    Assert.IsNotNull(status);
    Assert.Greater(status.UptimeSeconds, 0);
}

[Test]
public async Task MeshIntrospectionService_DoesNotLeakSensitiveData()
{
    // Verify that sensitive data (OS username, hostname) does not appear in payload
    var service = new MeshIntrospectionService(...);
    var call = new ServiceCall { Method = "GetStatus", Payload = Array.Empty<byte>() };
    var reply = await service.HandleCallAsync(call, context, CancellationToken.None);
    
    var payloadString = Encoding.UTF8.GetString(reply.Payload);
    Assert.IsFalse(payloadString.Contains(Environment.UserName));
    Assert.IsFalse(payloadString.Contains(Environment.MachineName));
}
```

### 8.4. Router Integration Tests (if practical)

```csharp
[Test]
public async Task Router_DispatchesToPodsService()
{
    // Arrange
    var router = new MeshServiceRouter(...);
    var podsService = new PodsMeshService(...);
    router.Register(podsService);
    
    // Simulate incoming ServiceCall envelope
    var envelope = CreateServiceCallEnvelope("pods", "Join", ...);

    // Act
    var reply = await router.HandleIncomingCallAsync(envelope);

    // Assert
    Assert.IsNotNull(reply);
    Assert.AreEqual(0, reply.StatusCode);
}
```

**Existing tests must still pass.**

Keep tests focused and minimal. Do NOT build a huge test framework; follow whatever pattern already exists in the repo.

---

## 9. ANTI-SLOP CHECKLIST FOR T-SF03

Before you consider the task done, check:

### 9.1. Scope Control

- [ ] You only changed pods/VirtualSoulfind/mesh-stats code and the new services' wiring
- [ ] You did NOT touch HTTP controllers or the core DHT service-descriptor infrastructure beyond necessary registration

### 9.2. Behavior Parity

- [ ] Existing pod/chat behavior still works the same from the user perspective
- [ ] VirtualSoulfind still uses the same DHT keyspaces and semantics
- [ ] No functional regressions

### 9.3. Security & Privacy

- [ ] No new PII in service descriptors or DHT values
- [ ] No logs that dump message contents or full MBID lists unnecessarily
- [ ] Rate limits and basic checks are in place for service calls
- [ ] Existing security checks are preserved

### 9.4. Code Quality

- [ ] `IMeshService` implementations are small, focused adapters (not bloated)
- [ ] No `.Result` / `.Wait()` on async calls
- [ ] No gratuitous large allocations or heavy LINQ in hot paths
- [ ] Error handling is robust (no crashes on malformed input)

### 9.5. Consistency

- [ ] Naming and structure match the rest of the project
- [ ] XML docs exist for new public types/methods
- [ ] Non-obvious security behavior is briefly commented
- [ ] DI patterns followed

### 9.6. Testing

- [ ] Unit tests added for each service
- [ ] Integration tests added where practical
- [ ] All existing tests still pass
- [ ] No test regressions

---

## 10. COMMIT MESSAGE

When T-SF03 is complete, commit with:

```
feat(mesh): wrap pods, VirtualSoulfind, and stats as mesh services (T-SF03)

Implements:
- PodsMeshService: Join, Leave, PostMessage, FetchMessages
- VirtualSoulfindMeshService: RegisterTrackLocation, LookupByMbId
- MeshIntrospectionService: GetStatus, GetPeersSummary, GetCapabilities
- Service registration and descriptor publishing for all three
- Unit and integration tests for service adapters

Preserves existing behavior through parallel legacy paths where needed.
Security and privacy constraints maintained (no PII leaks).

Part of service fabric initiative (T-SF03).
Depends on T-SF01 (service descriptors) and T-SF02 (router/client).
```

---

*Last Updated: December 11, 2025*  
*Branch: experimental/multi-source-swarm*  
*Status: 📋 Ready for Implementation*
