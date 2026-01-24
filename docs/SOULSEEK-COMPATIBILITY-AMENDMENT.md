# SOULSEEK COMPATIBILITY & TURBO BEHAVIOR AMENDMENT

**Applies to**: ALL service fabric implementation briefs (T-SF01 through T-SF07)  
**Created**: December 11, 2025  
**Status**: 🔒 Mandatory - Must be integrated into all tasks  
**Priority**: P0 - Overrides all other considerations

> **Project Note**: This is a fork of [slskd](https://github.com/slskd/slskd). See [README.md](README.md#acknowledgments) for attribution.

---

## CRITICAL AMENDMENT

**This document amends ALL existing service fabric briefs with mandatory Soulseek compatibility rules.**

These rules apply globally to T-SF01 through T-SF07 and must be followed strictly.

---

## 1. GLOBAL "SOULSEEK COMPATIBILITY, ETIQUETTE, AND TURBO RULES"

**Add this as a mandatory section to every task brief.**

### 1.1. Do Not Change Soulseek Wire Semantics

**RULE**: All communication with the official Soulseek server and Soulseek peers must remain fully compliant with existing protocol(s).

✅ **Allowed**:
- Service fabric, DHT, mesh, and gateway sit *beside* Soulseek protocol
- Additional non-Soulseek transports (mesh overlay, BitTorrent, HTTP)
- Augmenting Soulseek with alternative paths

❌ **Forbidden**:
- Injecting experimental frames into Soulseek messages
- Adding extra fields to Soulseek messages
- Modifying Soulseek wire format
- Breaking compatibility with official clients/server

### 1.2. Turbo Behavior Is Allowed, But NOT Via Soulseek

**RULE**: "Turbo client" behavior is ONLY allowed over non-Soulseek transports.

✅ **Allowed "turbo" over**:
- Mesh overlay
- DHT service fabric
- BitTorrent
- HTTP or any other non-Soulseek transport
- Local caches and indexes

❌ **Forbidden "turbo" via Soulseek**:
- Excessive search/browse spam to Soulseek peers
- Slot/queue abuse
- Abnormal reconnect/retry storms
- Automated Soulseek scraping
- Multiplicative amplification of Soulseek traffic

**PRINCIPLE**: If a turbo feature accelerates discovery or downloads, its extra load must be carried by:
- Mesh peers
- Multi-swarm / BT peers
- Your own index / caches

**NOT** by increasing traffic against the Soulseek server or random Soulseek clients.

### 1.3. Soulseek Is Not a Second-Class Citizen

**RULE**: The Soulseek protocol path must remain fully supported, correctly implemented, and never degraded.

✅ **Requirements**:
- Soulseek protocol fully supported
- Correctly implemented
- Never silently degraded or starved
- If Soulseek is the only path available, behave like a good Soulseek client

**PRINCIPLE**: Turbo and mesh features are an *augmentation*, not a replacement:
- If additional non-Soulseek paths are available, use them to offload and accelerate
- If Soulseek is the only path, behave as a respectful Soulseek client

### 1.4. Respect Slot/Queue Semantics and Fairness (On Soulseek)

**RULE**: Upstream slskd fairness/queueing semantics must not be bypassed.

✅ **Allowed**:
- Add more *non-Soulseek* sources
- Prefer offloading to mesh/BT when possible

❌ **Forbidden**:
- Lying about available slots
- Circumventing Soulseek queues
- Bypassing fairness mechanisms

### 1.5. One Identity = One Honest Soulseek Participant

**RULE**: The Soulseek account used by slskdn must behave as a single honest client.

✅ **Allowed**:
- One Soulseek account per slskdn instance
- Mesh/DHT identities are additional and separate
- Clear separation in code and data structures

❌ **Forbidden**:
- Fanning out into fake multiple Soulseek users
- Cycling random usernames as a feature
- Presenting as multiple Soulseek clients
- Identity spoofing or confusion

### 1.6. Do Not Leak Soulseek User Data Into the DHT

**RULE**: Soulseek usernames, room names, file paths, and IPs of other clients must NOT be stored verbatim in DHT records or service descriptors.

✅ **Allowed**:
- Use local mapping tables: `soulseek_username → internal_peer_id` (stored locally only)
- Publish internal peer IDs and content IDs to DHT
- Abstract identities before DHT publication

❌ **Forbidden**:
- Storing raw Soulseek usernames in DHT
- Storing Soulseek room names in DHT
- Storing other users' file paths in DHT
- Storing other users' IP addresses in DHT
- Any verbatim Soulseek identity leakage

**IMPLEMENTATION**: If mapping Soulseek world → mesh world:
```csharp
// Local mapping table (NEVER published to DHT)
private readonly Dictionary<string, string> _soulseekToMeshId = new();

// Only publish mesh IDs to DHT
public async Task PublishContentAsync(string localFilePath, string soulseekUsername)
{
    // Map to internal ID
    var meshPeerId = GetOrCreateMeshId(soulseekUsername);
    var contentId = HashContent(localFilePath);
    
    // Publish ONLY mesh IDs, NEVER Soulseek usernames
    await PublishToDht(contentId, meshPeerId);
}
```

### 1.7. Graceful Degradation: Fallback to "Plain slskd-like" Behavior

**RULE**: If DHT, mesh, or service fabric components are disabled or broken, slskdn must continue as a valid, non-turbo Soulseek client.

✅ **Requirements**:
- Graceful degradation to Soulseek-only mode
- No half-configured mesh state
- Clean fallback when components unavailable

❌ **Forbidden**:
- Leaving Soulseek in weird/broken state when mesh fails
- Requiring mesh for basic Soulseek functionality
- Partial failures that corrupt Soulseek behavior

### 1.8. Opt-In for Advanced Features

**RULE**: All advanced mesh/DHT features are clearly separated from core Soulseek config and opt-in.

✅ **Requirements**:
- Clear separation from Soulseek config
- Off by default OR clearly marked experimental
- Documented opt-in process
- Can be fully disabled without affecting Soulseek

**Configuration example**:
```jsonc
{
  "Soulseek": {
    "Username": "myuser",
    "Password": "...",
    // Standard Soulseek config
  },
  "MeshServices": {
    "Enabled": false,  // OFF by default
    "Experimental": true,
    // Mesh-specific config
  }
}
```

### 1.9. Reduce, Don't Amplify, Harmful Load on Soulseek

**RULE**: Use mesh/DHT/index capabilities to *cache and offload*, not to amplify Soulseek traffic.

✅ **Best practices**:
- Cache and reuse Soulseek results
- Use alternate transports instead of hammering Soulseek
- Rate-limit any mesh service that triggers Soulseek activity
- Net Soulseek traffic stays in "heavy but reasonable user" range

❌ **Anti-patterns**:
- Multiplicative amplification (1 mesh request → N Soulseek requests)
- Uncached repeated Soulseek queries
- Mesh services that spam Soulseek searches
- Resource exhaustion attacks via Soulseek

**IMPLEMENTATION**: For anything that indirectly triggers Soulseek activity:
```csharp
// Example: Mesh service that might trigger Soulseek search
public async Task<ServiceReply> HandleSearchAsync(ServiceCall call, ...)
{
    // 1. Check cache first
    var cached = await _cache.GetAsync(searchTerm);
    if (cached != null) return cached;
    
    // 2. Try mesh/DHT sources
    var meshResults = await SearchMeshAsync(searchTerm);
    if (meshResults.Any()) return meshResults;
    
    // 3. Only as last resort, hit Soulseek (with rate limit)
    if (!await _soulseekRateLimiter.TryAcquireAsync())
    {
        return ServiceReply.Error(429, "Soulseek rate limited");
    }
    
    var soulseekResults = await SearchSoulseekAsync(searchTerm);
    await _cache.SetAsync(searchTerm, soulseekResults, TimeSpan.FromMinutes(30));
    return soulseekResults;
}
```

---

## 2. CONFIGURATION MODES

### 2.1. LegacySoulseekMode (Hard Off for Mesh/Turbo)

Add to all config schemas:

```jsonc
{
  "LegacySoulseekMode": false  // Default: false (mesh enabled)
}
```

**When `LegacySoulseekMode = true`**:
- ✅ Disable ALL service fabric components
- ✅ Disable mesh pods
- ✅ Disable DHT extras
- ✅ Disable HTTP gateway
- ✅ Disable multi-source/turbo features
- ✅ Behave as close as possible to upstream slskd
- ✅ Plain Soulseek client, no turbo

**IMPLEMENTATION**: All new components must check this flag:
```csharp
public class MeshServiceRouter
{
    public MeshServiceRouter(IOptions<GlobalConfig> config, ...)
    {
        if (config.Value.LegacySoulseekMode)
        {
            _logger.LogInformation("Legacy Soulseek mode enabled - mesh services disabled");
            return; // Don't start any mesh components
        }
        
        // Normal mesh initialization
    }
}
```

### 2.2. MeshTurboEnabled (Opt-In for Turbo on Non-Soulseek Paths)

Add to all config schemas:

```jsonc
{
  "MeshTurboEnabled": false  // Default: false (conservative)
}
```

**When `MeshTurboEnabled = true`**:
- ✅ Allows aggressive multi-source/turbo behavior over mesh/DHT/BT
- ✅ Subject to their own limits and rate controls
- ❌ Must NOT loosen any Soulseek-layer limits
- ✅ Soulseek remains normalized and well-behaved

**When `MeshTurboEnabled = false`**:
- ✅ Conservative mesh behavior
- ✅ Limited concurrent connections
- ✅ Standard rate limits

**IMPLEMENTATION**:
```csharp
public class MultiSourceDownloader
{
    public async Task DownloadAsync(...)
    {
        if (!_config.MeshTurboEnabled)
        {
            // Conservative: max 3 concurrent sources
            maxConcurrentSources = 3;
        }
        else
        {
            // Turbo: max 20 concurrent sources (mesh/BT only)
            maxConcurrentSources = 20;
        }
        
        // But ALWAYS respect Soulseek limits
        maxSoulseekConnections = 2; // Never increase this for turbo
    }
}
```

---

## 3. PER-TASK AMENDMENTS

### 3.1. T-SF01, T-SF02, T-SF03 (Fabric + Services) Amendments

#### DHT / Service Descriptors

**Add explicit rule**:

```csharp
/// <summary>
/// MeshServiceDescriptor MUST NOT contain raw Soulseek identifiers for other users.
/// </summary>
public class MeshServiceDescriptor
{
    // ✅ ALLOWED:
    public string ServiceName { get; init; }      // "pods", "shadow-index"
    public string OwnerPeerId { get; init; }      // Internal mesh peer ID
    public Dictionary<string, string> Metadata { get; init; }
    
    // ❌ FORBIDDEN:
    // public string SoulseekUsername { get; init; }  // NO!
    // public string SoulseekRoom { get; init; }      // NO!
    // public string SoulseekIp { get; init; }        // NO!
}
```

**Metadata can signal turbo capabilities**:
```jsonc
{
  "metadata": {
    "supportsMultiSource": "true",      // ✅ OK - mesh capability
    "supportsTorrentBridge": "true",    // ✅ OK - BT capability
    "supportsMeshTurbo": "true"         // ✅ OK - mesh capability
    // But capabilities are always about NON-Soulseek paths
  }
}
```

#### Pods / Chat Service

**Add clarification**:

```csharp
/// <summary>
/// Pods are a MESH concept, not a Soulseek concept.
/// </summary>
public class PodsMeshService : IMeshService
{
    // Pods run on mesh overlay ONLY
    // If bridging from Soulseek rooms:
    // - Must be OPT-IN
    // - Must NOT rebroadcast all Soulseek room activity into DHT
    // - Must NOT leak Soulseek usernames
}
```

#### VirtualSoulfind / MBID Index

**Add clarification**:

```csharp
/// <summary>
/// The index captures what THIS NODE can provide (and mesh peers).
/// It MUST NOT become "a public DB of everyone on Soulseek with track X".
/// </summary>
public class VirtualSoulfindMeshService : IMeshService
{
    public async Task HandleRegisterAsync(...)
    {
        // ✅ ALLOWED:
        // - Register tracks from this node
        // - Register tracks from mesh peers (with their mesh IDs)
        
        // ❌ FORBIDDEN:
        // - Scraping Soulseek and publishing other users' data
        // - Creating a "shadow DB" of Soulseek users
        
        // If using Soulseek downloads/searches as input:
        // - That's fine for local knowledge
        // - But abstract away other Soulseek users' identities before DHT publication
    }
}
```

**Turbo allowances at this layer**:

✅ **Acceptable**:
- Aggregate many mesh or BT sources
- Serve rapid multi-source fetching
- Heavy mesh/DHT usage

❌ **Not acceptable**:
- Drive explosion of Soulseek search/browse traffic
- Use Soulseek as primary path for turbo features

### 3.2. T-SF04 (HTTP Gateway) Amendments

**Add to gateway brief**:

> **CRITICAL RULE**: The gateway is an API into this node's mesh and local features, NOT an API to programmatically attack or scrape Soulseek.

**Add explicit rule for gateway docs**:

```markdown
## Gateway Usage Rules

You must NOT use the HTTP gateway to push raw, unthrottled Soulseek commands.

All turbo-style functionality exposed through the gateway must work by:
- Talking to mesh services
- Leveraging DHT/index data
- Leveraging non-Soulseek transports (e.g., BT)

The gateway must NOT behave as an automated Soulseek spammer.
```

**Implementation requirement**:

```csharp
[HttpPost("/mesh/http/{serviceName}/{**path}")]
public async Task<IActionResult> InvokeService(string serviceName, ...)
{
    // Gateway can build "turbo" operations (multi-source, fast lookups)
    // But operations must be satisfied by NON-Soulseek channels when possible
    
    if (serviceName == "soulseek-spam-bot") // Example of forbidden service
    {
        return Forbid(); // DO NOT allow services that spam Soulseek
    }
    
    // Only allow services that use mesh/DHT/BT, not Soulseek abuse
}
```

### 3.3. T-SF05 (Hardening) Amendments

**Add explicit checks/principles**:

#### Separate Soulseek Rate Limits

```csharp
public class RateLimiterConfig
{
    // Mesh/DHT limits (can be higher for turbo)
    public int MaxMeshCallsPerMinute { get; set; } = 200;
    public int MaxDhtQueriesPerMinute { get; set; } = 100;
    
    // Soulseek limits (ALWAYS conservative, NEVER increased for turbo)
    public int MaxSoulseekSearchesPerMinute { get; set; } = 10;
    public int MaxSoulseekBrowsesPerMinute { get; set; } = 5;
    public int MaxSoulseekConnectionsPerMinute { get; set; } = 20;
}
```

#### Throttle Soulseek Fallbacks

```csharp
public async Task<ServiceReply> HandleMeshServiceWithSoulseekFallback(...)
{
    // For any mesh/service call that INTERNALLY hits Soulseek:
    // - Add throttle on Soulseek layer
    // - Prevent multiplicative amplification
    
    if (requiresSoulseekFallback)
    {
        // Check Soulseek-specific rate limit
        if (!await _soulseekRateLimiter.TryAcquireAsync())
        {
            _logger.LogWarning("Soulseek fallback rate limited");
            return ServiceReply.Error(429, "Fallback rate limited");
        }
    }
}
```

#### Abuse Logic Clarification

```csharp
// Misbehavior in DHT/mesh context:
// - Handle locally (ban, unweight, quarantine in mesh)
// - DO NOT reflect abuse back into Soulseek
// - NO retaliatory behavior
// - NO weird feedback loops

public async Task HandleMeshAbuseAsync(string meshPeerId, ViolationType violation)
{
    // ✅ Ban in mesh context
    await _meshSecurityCore.BanPeerAsync(meshPeerId);
    
    // ❌ DO NOT ban their Soulseek account (if known)
    // ❌ DO NOT trigger Soulseek retaliation
}
```

### 3.4. T-SF06 (Documentation) Amendments

**Add section to docs**:

```markdown
## Soulseek Friendliness & Turbo Behavior

slskdn aims to:
- Be a good Soulseek citizen
- Use additional transports/mesh to offload work, not abuse the central network

### "Turbo" Features

✅ **Allowed and encouraged** when running over:
- Mesh overlay
- DHT-based services
- BitTorrent or other networks
- Local caches and indexes

❌ **Not allowed** to be implemented as:
- Soulseek spam bots
- Automated Soulseek scrapers
- Slot/queue abuse
- Abnormal Soulseek traffic patterns

### Soulseek Compatibility

slskdn maintains full compatibility with:
- Official Soulseek server
- Official Soulseek clients
- Soulseek wire protocol

Advanced features augment, never replace or degrade, Soulseek functionality.

### Configuration Modes

**Legacy Soulseek Mode**: Disables all mesh/turbo features, behaves as plain Soulseek client

**Mesh Turbo Mode**: Enables aggressive optimization, but ONLY over non-Soulseek paths
```

### 3.5. T-SF07 (Metrics) Amendments

**Extend metrics list** to explicitly include Soulseek-side metrics:

```csharp
// Add Soulseek-specific metrics to detect abuse
private static readonly Counter<long> _soulseekSearchRequestsTotal = Meter.CreateCounter<long>(
    "soulseek.search_requests.total",
    description: "Total Soulseek search requests per minute/hour");

private static readonly Counter<long> _soulseekBrowseRequestsTotal = Meter.CreateCounter<long>(
    "soulseek.browse_requests.total",
    description: "Total Soulseek browse requests");

private static readonly Counter<long> _soulseekConnectAttemptsTotal = Meter.CreateCounter<long>(
    "soulseek.connect_attempts.total",
    description: "Total Soulseek connection attempts (success/failure)");

// Ratio metrics to detect amplification
private static readonly Counter<long> _meshToSoulseekRatio = Meter.CreateCounter<long>(
    "mesh.soulseek_amplification_ratio",
    description: "Ratio of mesh requests to Soulseek requests (should be >> 1)");
```

**Add note in brief**:

> **CRITICAL**: These metrics are specifically to detect whether new mesh/turbo features are accidentally driving up Soulseek traffic.
>
> If you see Soulseek search/browse volume spike when enabling a feature, you MUST throttle or adjust that feature.
>
> **Target**: Mesh activity should be 10-100x Soulseek activity (offloading), not 1:1 (amplification).

**Implementation example**:

```csharp
public async Task HandleMeshSearchAsync(string term)
{
    _meshSearchTotal.Add(1);
    
    // Try mesh first
    var meshResults = await SearchMeshAsync(term);
    if (meshResults.Any())
    {
        // Success without Soulseek
        _meshToSoulseekRatio.Add(1, new TagList { { "source", "mesh_only" } });
        return meshResults;
    }
    
    // Fallback to Soulseek (rate limited)
    if (await _soulseekRateLimiter.TryAcquireAsync())
    {
        _soulseekSearchRequestsTotal.Add(1);
        _meshToSoulseekRatio.Add(1, new TagList { { "source", "soulseek_fallback" } });
        return await SearchSoulseekAsync(term);
    }
    
    return ServiceReply.Error(429, "Rate limited");
}
```

---

## 4. ANTI-SLOP CHECKLIST ADDITIONS

Add to every task's anti-slop checklist:

### Soulseek Compatibility Checklist

- [x] No changes to Soulseek wire protocol
- [x] No injection of experimental frames into Soulseek messages
- [x] Turbo features ONLY use non-Soulseek transports (mesh/DHT/BT)
- [x] Soulseek rate limits are separate and never loosened for turbo
- [x] No Soulseek usernames in DHT/service descriptors
- [x] No Soulseek room names in DHT
- [x] No other users' IPs or paths in DHT
- [x] Graceful degradation to Soulseek-only mode works
- [x] `LegacySoulseekMode` flag respected (all mesh features disabled)
- [x] `MeshTurboEnabled` flag only affects non-Soulseek paths
- [x] Soulseek metrics tracked separately
- [x] No multiplicative amplification of Soulseek traffic
- [x] Fallback to Soulseek is throttled and rate-limited
- [x] One identity = one honest Soulseek participant
- [x] Slot/queue semantics not bypassed
- [x] Soulseek treated as first-class, never degraded

---

## 5. TESTING REQUIREMENTS

Add to all test suites:

### Soulseek Compatibility Tests

```csharp
[Test]
public void MeshService_WhenEnabled_DoesNotModifySoulseekProtocol()
{
    // Verify Soulseek message formats unchanged
}

[Test]
public void TurboFeature_UsesOnlyMeshTransport()
{
    // Verify no Soulseek traffic increase when turbo enabled
}

[Test]
public void LegacyMode_DisablesAllMeshFeatures()
{
    config.LegacySoulseekMode = true;
    
    // Verify all mesh components disabled
    Assert.IsFalse(meshRouter.IsRunning);
    Assert.IsFalse(dhtDirectory.IsRunning);
    Assert.IsFalse(httpGateway.IsRunning);
}

[Test]
public void SoulseekRateLimit_NotAffectedByTurboMode()
{
    config.MeshTurboEnabled = true;
    
    // Soulseek limits unchanged
    Assert.AreEqual(10, rateLimiter.MaxSoulseekSearchesPerMinute);
    
    config.MeshTurboEnabled = false;
    
    // Still unchanged
    Assert.AreEqual(10, rateLimiter.MaxSoulseekSearchesPerMinute);
}

[Test]
public void ServiceDescriptor_DoesNotContainSoulseekUsernames()
{
    var descriptor = CreateTestDescriptor();
    var serialized = JsonSerializer.Serialize(descriptor);
    
    // Verify no Soulseek identifiers
    Assert.IsFalse(serialized.Contains("soulseek"));
    Assert.IsFalse(serialized.Contains("username"));
}

[Test]
public async Task MeshSearch_WithSoulseekFallback_IsRateLimited()
{
    // Make N mesh searches that require Soulseek fallback
    for (int i = 0; i < 20; i++)
    {
        await meshService.SearchAsync("term" + i);
    }
    
    // Verify Soulseek searches capped
    Assert.LessOrEqual(soulseekSearchCount, 10); // Max 10/min
}
```

---

## 6. NET EFFECT

With these amendments:

✅ **Backwards compatibility**: Soulseek behavior preserved, can snap back via `LegacySoulseekMode`

✅ **Good network behavior**: Soulseek treated as first-class but non-abused

✅ **Turbo where it belongs**: All optimization aimed at mesh/DHT/BT, not Soulseek

✅ **Privacy maintained**: No leaking Soulseek identities into DHT

✅ **Measurable**: Metrics prove Soulseek traffic stays reasonable

✅ **Testable**: Explicit tests verify compatibility

---

## 7. INTEGRATION INSTRUCTIONS

**For each existing brief (T-SF01 through T-SF07)**:

1. Add "Soulseek Compatibility Rules" section at the top
2. Add `LegacySoulseekMode` and `MeshTurboEnabled` config requirements
3. Add task-specific amendments from Section 3
4. Add Soulseek compatibility items to anti-slop checklist
5. Add Soulseek compatibility tests to test requirements

**When implementing any task**:

1. Check this amendment document FIRST
2. Ensure all Soulseek compatibility rules followed
3. Implement both config modes
4. Add required metrics
5. Add required tests
6. Verify anti-slop checklist satisfied

---

## 8. COMMIT MESSAGE TEMPLATE

When committing Soulseek compatibility work:

```
feat: implement Soulseek compatibility safeguards (Amendment)

Ensures service fabric remains compatible with Soulseek protocol:

Compatibility Rules:
- No changes to Soulseek wire protocol
- Turbo features ONLY over non-Soulseek transports
- Soulseek treated as first-class, never degraded
- No Soulseek user data leaked into DHT

Configuration Modes:
- LegacySoulseekMode: disables all mesh/turbo (Soulseek-only)
- MeshTurboEnabled: enables aggressive optimization on mesh/DHT/BT only

Rate Limiting:
- Separate Soulseek rate limits (never loosened)
- Mesh/DHT rate limits (can be higher for turbo)
- Throttled fallbacks prevent amplification

Metrics:
- soulseek.search_requests.total
- soulseek.browse_requests.total
- soulseek.connect_attempts.total
- mesh.soulseek_amplification_ratio

Testing:
- Protocol compatibility verified
- Turbo uses only mesh transport
- Legacy mode disables mesh features
- Rate limits independent of turbo mode

Ensures slskdn is a good Soulseek citizen while enabling turbo mesh features.
```

---

**END OF AMENDMENT**

*Last Updated: December 11, 2025*  
*Status: 🔒 Mandatory - Must be applied to all tasks*  
*Priority: P0 - Security and compatibility critical*
