# Cursor / LLM Implementation Warnings

**Status**: CRITICAL - Read Before Starting Any Task  
**Created**: December 11, 2025  
**Purpose**: Identify tasks where LLMs are most likely to introduce slop, bugs, or security issues

> **Project Note**: This is a fork of [slskd](https://github.com/slskd/slskd). See [README.md](README.md#acknowledgments) for attribution.

---

## Overview

Not all tasks are equally risky when implemented by an LLM (Cursor, Copilot, etc.). Some are straightforward; others touch **cross-cutting concerns** or **stateful systems** where LLMs predictably fail.

This document ranks tasks by **"likelihood of LLM screwup"** and provides concrete mitigation strategies.

---

## Risk Ranking: Most Dangerous Tasks

### 🔴 CRITICAL RISK: Domain Abstraction (T-VC01)

**Task**: Introduce `ContentDomain` enum and refactor VirtualSoulfind core types

**Why LLMs Fail Here**:
- Touches core types used **everywhere** (releases/tracks → `ContentWorkId`/`ContentItemId`)
- Easy to:
  - Half-convert some call sites and leave others
  - Introduce new abstraction but still leak music-specific assumptions
  - Accidentally change behavior despite "no behavior change" requirement
  - Break existing music flows

**Predictable Failure Modes**:
1. **"Phantom enums"**: New `ContentDomain` added, but switches don't handle it:
   ```csharp
   // BAD: LLM adds this but forgets exhaustive handling
   switch (item.ContentDomain)
   {
       case ContentDomain.Music:
           return ProcessMusic(item);
       // Missing: GenericFile case!
       // No default, no compilation error, runtime bugs
   }
   ```

2. **"Partial refactors"**: Old IDs and new IDs co-exist but aren't synced:
   ```csharp
   // BAD: Both exist, some code uses old, some uses new
   public string TrackId { get; set; } // Old
   public ContentItemId ItemId { get; set; } // New
   // LLM forgets to keep them in sync or migrate fully
   ```

3. **"Over-eager renames"**: LLM renames existing music types that should be left alone:
   ```csharp
   // BAD: Breaks all existing code
   // Old: public class DesiredTrack
   // New: public class DesiredContentItem // WRONG! Should be adapter pattern
   ```

**Mitigation Strategy**:

**STRICT PROMPT REQUIREMENTS**:
```
T-VC01 RULES (MANDATORY):

1. NO BEHAVIOR CHANGES:
   - All existing music tests MUST pass unchanged
   - Do NOT modify existing music-specific logic
   - Add adapters AROUND existing types, do NOT rename them

2. EXHAUSTIVE ENUM HANDLING:
   - Every switch on ContentDomain MUST have all cases
   - Add default case that throws for unknown domains
   - Use analyzer to enforce exhaustive switches

3. INCREMENTAL APPROACH:
   - Step 1: Add new types ONLY (ContentDomain, ContentWorkId, ContentItemId)
   - Step 2: Add adapters/facades AROUND existing music types
   - Step 3: Wire new types into existing code via adapters
   - Do NOT try to do all three in one pass

4. VERIFICATION:
   - After each step: compile, run ALL tests
   - Generate list of touched files with justification
   - Grep for old type names to ensure they still exist
   - No file should be "fully rewritten" - only extended

5. FORBIDDEN:
   - DO NOT rename DesiredTrack, DesiredRelease, TrackId, ReleaseId
   - DO NOT modify existing music matching logic
   - DO NOT change VirtualSoulfind DB schema yet (adapters first)
```

**Test Requirements**:
```csharp
// MUST PASS after T-VC01:
[Fact]
public void ExistingMusicWorkflowStillWorks()
{
    // All existing music intent/planner/resolver tests
    // Should pass with ZERO modifications
}

[Fact]
public void ContentDomainSwitchesAreExhaustive()
{
    // Use reflection to find all switches on ContentDomain
    // Assert all have all cases or a default
}

[Fact]
public void NewTypesCoexistWithOldTypes()
{
    // Assert DesiredTrack, DesiredRelease still exist
    // Assert ContentWorkId, ContentItemId also exist
    // Assert adapters between them exist
}
```

**Human Review Checklist**:
- [ ] Did it rename any existing music types? (REJECT if yes)
- [ ] Are all existing music tests still passing? (REJECT if no)
- [ ] Are there switches on `ContentDomain` without all cases? (REJECT if yes)
- [ ] Did it touch more than 30 files? (WARNING: likely over-eager)
- [ ] Can you grep for `DesiredTrack` and still find it? (REJECT if no)

---

### 🔴 CRITICAL RISK: Domain-Aware Planner (T-VC04 + H-14)

**Tasks**: Make planner/backends domain-aware + gate Soulseek to Music only + add work budget/caps

**Why LLMs Fail Here**:
- Planning logic has **lots of branching and policy**:
  - Modes (`SoulseekFriendly`, `MeshOnly`, `OfflinePlanning`)
  - New `ContentDomain` rules (Music vs GenericFile)
  - Work budget + caps (from H-14)
- Easy to:
  - Apply domain gating in one layer but forget another
  - Accidentally exclude Soulseek where it should be allowed
  - **Or worse**: still call Soulseek for non-music domains
  - Mis-wire mode logic so legitimate intents fail silently

**Predictable Failure Modes**:
1. **"Silent no-ops"**: Planner returns empty plans for legitimate music intents:
   ```csharp
   // BAD: Domain gating breaks music
   if (intent.ContentDomain == ContentDomain.Music && mode != Mode.SoulseekFriendly)
       return EmptyPlan(); // WRONG! Music should work in most modes
   ```

2. **"Leaky Soulseek"**: GenericFile still causes Soulseek calls in some paths:
   ```csharp
   // BAD: Forgot to check domain before Soulseek
   if (mode == Mode.SoulseekFriendly)
       await _soulseekBackend.SearchAsync(...); // Missing domain check!
   ```

3. **"Budget chaos"**: Budget checks in wrong places or double-charged:
   ```csharp
   // BAD: Double-charging budget
   await _workBudget.TryConsume(WorkCosts.Planning); // Outer
   await PlanForIntent(intent); // Calls TryConsume again internally!
   ```

**Mitigation Strategy**:

**STRICT PROMPT REQUIREMENTS**:
```
T-VC04 + H-14 RULES (MANDATORY):

1. SOULSEEK GATING (HIGHEST PRIORITY):
   - Soulseek backend MUST ONLY be called for ContentDomain.Music
   - Add assertion at Soulseek backend entry point:
     if (domain != ContentDomain.Music) throw InvalidOperationException();
   - Non-music domains (GenericFile) MUST NOT see Soulseek in backend list

2. MODE PRESERVATION:
   - Keep existing music planner behavior as reference implementation
   - Add domain logic AROUND it, do NOT rewrite from scratch
   - All existing mode tests (SoulseekFriendly, MeshOnly, Offline) MUST pass

3. WORK BUDGET:
   - Budget check at planner entry ONLY, not per-backend
   - Backends assume budget already checked by planner
   - Add clear comments: "Budget checked by caller"

4. EXPLICIT TESTS REQUIRED:
   - Test: Music + SoulseekFriendly mode → Soulseek included in plan
   - Test: GenericFile + ANY mode → Soulseek NEVER in plan
   - Test: Music + MeshOnly mode → Soulseek excluded, mesh included
   - Test: Budget exhausted → planner fails fast, no backend calls

5. INCREMENTAL:
   - Step 1: Add domain awareness to backend selection
   - Step 2: Add Soulseek gating with assertion
   - Step 3: Add work budget integration
   - Step 4: Add origin priority handling
   - Test after each step
```

**Test Requirements**:
```csharp
[Theory]
[InlineData(ContentDomain.Music, Mode.SoulseekFriendly, true)]  // Should include Soulseek
[InlineData(ContentDomain.Music, Mode.MeshOnly, false)]         // Should exclude Soulseek
[InlineData(ContentDomain.GenericFile, Mode.SoulseekFriendly, false)] // MUST exclude Soulseek
[InlineData(ContentDomain.GenericFile, Mode.MeshOnly, false)]   // MUST exclude Soulseek
public async Task Planner_RespectsDomainAndMode(ContentDomain domain, Mode mode, bool expectSoulseek)
{
    var plan = await _planner.CreatePlanAsync(intent, mode);
    var hasSoulseek = plan.Steps.Any(s => s.Backend == BackendType.Soulseek);
    Assert.Equal(expectSoulseek, hasSoulseek);
}

[Fact]
public async Task SoulseekBackend_RejectsNonMusicDomain()
{
    var intent = CreateIntent(ContentDomain.GenericFile);
    await Assert.ThrowsAsync<InvalidOperationException>(
        () => _soulseekBackend.SearchAsync(intent));
}

[Fact]
public async Task Planner_FailsFastOnBudgetExhaustion()
{
    _workBudget.ExhaustBudget();
    var result = await _planner.CreatePlanAsync(intent);
    Assert.Equal(PlanResult.QuotaExceeded, result.Status);
    Assert.Empty(result.Steps); // No backend calls attempted
}
```

**Human Review Checklist**:
- [ ] Can you find ANY path where Soulseek is called for non-music? (REJECT if yes)
- [ ] Are all existing mode tests still passing? (REJECT if no)
- [ ] Is work budget checked at planner entry? (REJECT if no)
- [ ] Are backends calling `TryConsume` again? (REJECT if yes - double charge)
- [ ] Did it rewrite the music planner from scratch? (WARNING: likely broke something)

---

### 🟠 HIGH RISK: Backend Adapter Safety (H-13)

**Task**: Add work budget + SSRF guards to all backend adapters

**Why LLMs Fail Here**:
- Backends are already complex (Soulseek, DHT, torrents, HTTP)
- Work budget and SSRF guard are **cross-cutting concerns**
- Easy to:
  - Forget budget check in some call paths
  - Accidentally bypass SSRF-safe HTTP client with raw `HttpClient`
  - Double-charge budget in some places and starve things
  - Introduce subtle race conditions in budget tracking

**Predictable Failure Modes**:
1. **"Everything is quota exceeded"**: Budget accounting wrong:
   ```csharp
   // BAD: Charges per candidate instead of per search
   foreach (var candidate in results)
       await _workBudget.TryConsume(WorkCosts.Search); // 100 candidates = 100x cost!
   ```

2. **"Budget never enforced"**: Check in wrong place:
   ```csharp
   // BAD: Budget check in helper that's never called
   private async Task<Results> InternalSearchAsync(...)
   {
       if (!_workBudget.TryConsume(...)) return Empty; // Never reached!
   }
   
   public async Task<Results> SearchAsync(...)
   {
       return await CachedSearchAsync(...); // Bypasses budget check!
   }
   ```

3. **"SSRF regression"**: Reintroduces direct HTTP calls:
   ```csharp
   // BAD: Bypasses safe client
   using var client = new HttpClient(); // FORBIDDEN!
   var response = await client.GetAsync(url); // SSRF vulnerability!
   ```

**Mitigation Strategy**:

**STRICT PROMPT REQUIREMENTS**:
```
H-13 RULES (MANDATORY):

1. SSRF PROTECTION:
   - ALL outbound HTTP MUST go through ISafeHttpClient ONLY
   - Search entire codebase for: "new HttpClient()", "HttpClient.GetAsync"
   - Remove ALL instances outside ISafeHttpClient implementation
   - ISafeHttpClient MUST block loopback and private IPs

2. WORK BUDGET:
   - Budget check at backend adapter entry point ONLY
   - One check per operation (search, download, verify)
   - Do NOT charge per-result or per-candidate
   - Clear pattern:
     ```csharp
     public async Task<Results> SearchAsync(...)
     {
         if (!_workBudget.TryConsume(WorkCosts.Search))
             return Results.QuotaExceeded();
         // ... do work
     }
     ```

3. VERIFICATION:
   - Generate report: all network call sites BEFORE changes
   - Generate report: all network call sites AFTER changes
   - Diff the two: confirm all go through safe client
   - Add tests that simulate budget exhaustion

4. INCREMENTAL:
   - Step 1: Add ISafeHttpClient if not exists
   - Step 2: Migrate HTTP backend to use it
   - Step 3: Add work budget to all backends
   - Step 4: Remove all raw HttpClient usage
   - Grep and verify after each step
```

**Test Requirements**:
```csharp
[Fact]
public async Task Backend_EnforcesBudget()
{
    _workBudget.SetRemaining(WorkCosts.Search - 1); // Just under threshold
    var result = await _backend.SearchAsync(query);
    Assert.Equal(ResultStatus.QuotaExceeded, result.Status);
}

[Fact]
public async Task HttpBackend_RejectsLoopback()
{
    var url = "http://127.0.0.1/malicious";
    await Assert.ThrowsAsync<SecurityException>(
        () => _httpBackend.FetchAsync(url));
}

[Fact]
public async Task HttpBackend_RejectsPrivateIPs()
{
    var url = "http://192.168.1.1/malicious";
    await Assert.ThrowsAsync<SecurityException>(
        () => _httpBackend.FetchAsync(url));
}

[Fact]
public void CodebaseHasNoRawHttpClient()
{
    // Use Roslyn or grep to verify
    var files = Directory.GetFiles("src", "*.cs", SearchOption.AllDirectories);
    foreach (var file in files)
    {
        var content = File.ReadAllText(file);
        if (file.Contains("ISafeHttpClient.cs")) continue; // Allow in implementation
        Assert.DoesNotContain("new HttpClient()", content);
    }
}
```

**Human Review Checklist**:
- [ ] Grep for "new HttpClient()" returns only ISafeHttpClient.cs? (REJECT if no)
- [ ] All backends have budget check at entry? (REJECT if no)
- [ ] Budget is checked per-operation, not per-result? (REJECT if wrong)
- [ ] ISafeHttpClient blocks loopback and private IPs? (REJECT if no)

---

### 🟠 HIGH RISK: Content Chunk Relay (T-PR03)

**Task**: Implement content chunk relay / CDN service

**Why LLMs Fail Here**:
- **Stateful and performance-sensitive**:
  - File I/O, offsets, chunking, concurrency limits
- **Crosses security boundaries**:
  - Mapping `ContentId` → local paths
  - Ensuring only verified content gets served
- Easy to:
  - Reach directly for filesystem paths from requests
  - Have off-by-one bugs in chunking
  - Leak concurrency counters (increment but never decrement)
  - Serve unverified or non-existent files

**Predictable Failure Modes**:
1. **"Path confusion"**: Uses path from request directly:
   ```csharp
   // BAD: Arbitrary file read!
   var filePath = request.FilePath; // FORBIDDEN!
   var data = await File.ReadAllBytesAsync(filePath);
   ```

2. **"Off-by-one / partial chunk bugs"**:
   ```csharp
   // BAD: Reads one byte too many
   var length = Math.Min(request.Length, MaxChunkBytes + 1); // Should be MaxChunkBytes!
   
   // BAD: EOF detection wrong
   if (offset + length >= fileSize)
       isLastChunk = true; // Should be > not >=
   ```

3. **"Concurrency leaks"**: Counters never decrement:
   ```csharp
   // BAD: Increment but no matching decrement
   _activeStreams.Increment();
   var data = await ReadChunkAsync(...);
   return data; // Forgot to decrement on return!
   ```

**Mitigation Strategy**:

**STRICT PROMPT REQUIREMENTS**:
```
T-PR03 RULES (MANDATORY):

1. NO ARBITRARY FILE ACCESS:
   - ABSOLUTELY NO file path from request object is EVER used
   - ALL mapping MUST go through VirtualSoulfind.ResolveContentIdAsync()
   - Pattern:
     ```csharp
     var contentItem = await _vsf.ResolveContentIdAsync(request.ContentId);
     if (contentItem == null) return Error("Not found");
     var localFile = await _library.GetFileAsync(contentItem.LocalFileId);
     if (!localFile.IsVerified || !localFile.IsAdvertisable) return Error("Not available");
     // NOW safe to read localFile.Path
     ```

2. CHUNK BOUNDARIES:
   - Clamp length: Math.Min(request.Length, MaxChunkBytes)
   - EOF detection: (offset + actualRead) >= fileSize
   - Handle edge cases: offset beyond EOF, zero-length file
   - Write unit tests for: start, middle, end, beyond-EOF

3. CONCURRENCY TRACKING:
   - Use try/finally for counter decrement:
     ```csharp
     if (!_streamTracker.TryAcquire(peerId)) return Error("Cap exceeded");
     try {
         // ... do work
     } finally {
         _streamTracker.Release(peerId);
     }
     ```

4. VERIFICATION:
   - Every file read MUST check IsVerified && IsAdvertisable
   - Log ContentId, not file path
   - Return generic error on failure (don't leak paths)

5. TESTS REQUIRED:
   - Test: Known ContentId → correct chunk returned
   - Test: Unknown ContentId → error
   - Test: Unverified content → error
   - Test: Mid-file chunk → correct data
   - Test: Last chunk → IsLastChunk = true
   - Test: Offset beyond EOF → empty + IsLastChunk = true
   - Test: Concurrent requests → caps enforced
```

**Test Requirements**:
```csharp
[Fact]
public async Task ContentRelay_RejectsArbitraryPath()
{
    // Even if we try to trick it with a ContentId that looks like a path
    var request = new ContentChunkRequest { ContentId = "/etc/passwd" };
    var response = await _relay.HandleChunkRequestAsync(request, context, ct);
    Assert.NotEqual(ServiceStatusCodes.Success, response.StatusCode);
}

[Theory]
[InlineData(0, 1024)]      // Start chunk
[InlineData(1024, 1024)]   // Middle chunk
[InlineData(9000, 1000)]   // Last chunk (file is 10000 bytes)
[InlineData(10000, 1024)]  // Beyond EOF
public async Task ContentRelay_HandlesChunkBoundaries(long offset, int length)
{
    var response = await _relay.HandleChunkRequestAsync(
        new ContentChunkRequest { ContentId = KnownId, Offset = offset, Length = length },
        context, ct);
    
    if (offset >= 10000) // Beyond EOF
    {
        Assert.Empty(response.Data);
        Assert.True(response.IsLastChunk);
    }
    else if (offset + length >= 10000) // Last chunk
    {
        Assert.True(response.IsLastChunk);
    }
    // ... more assertions
}

[Fact]
public async Task ContentRelay_EnforcesStreamCaps()
{
    var tasks = Enumerable.Range(0, 10).Select(_ =>
        _relay.HandleChunkRequestAsync(request, context, ct));
    
    var results = await Task.WhenAll(tasks);
    var capExceeded = results.Count(r => r.StatusCode == ServiceStatusCodes.QuotaExceeded);
    Assert.True(capExceeded > 0); // At least some were capped
}
```

**Human Review Checklist**:
- [ ] Grep for "File.Read" - are ALL paths from VirtualSoulfind mapping? (REJECT if no)
- [ ] Are there any request.Path or request.FilePath fields? (REJECT if yes)
- [ ] Is there a try/finally around stream counter? (REJECT if no)
- [ ] Are chunk boundary tests passing? (REJECT if no)
- [ ] Does it check IsVerified && IsAdvertisable? (REJECT if no)

---

### 🟠 HIGH RISK: Trusted Relay Mode (T-PR04)

**Task**: Implement trusted relay for own nodes / friends

**Why LLMs Fail Here**:
- **Mini-tunnel system**:
  - Stateful tunnels per peer
  - Routing by logical `TargetService`
  - Caps per peer and global
- **Very easy** for LLM to:
  - Slide into "I'll just add host:port fields"
  - Skip trust checks or do them only on `Open`
  - Forget to close tunnels on errors
  - Make it a stealth generic proxy

**Predictable Failure Modes**:
1. **"Accidental generic proxy"**: Adds host:port support:
   ```csharp
   // BAD: This is now a generic TCP proxy!
   public record TrustedRelayRequest
   {
       public string TargetService { get; set; }
       public string TargetHost { get; set; } // FORBIDDEN!
       public int TargetPort { get; set; }    // FORBIDDEN!
   }
   ```

2. **"Tunnel leaks"**: Tunnels never cleaned up:
   ```csharp
   // BAD: No cleanup on error
   _tunnels[tunnelId] = new TunnelState(...);
   await RouteDataAsync(...); // Throws exception
   // Tunnel never removed!
   ```

3. **"Trust bypasses"**: Trust only checked on Open:
   ```csharp
   // BAD: Trust check missing on Data command
   if (request.Command == TrustedRelayCommand.Open)
   {
       if (!IsTrusted(peerId)) return Error(); // Only checked here!
   }
   // ... later, Data command doesn't check trust again
   ```

**Mitigation Strategy**:

**STRICT PROMPT REQUIREMENTS**:
```
T-PR04 RULES (MANDATORY):

1. NO HOST:PORT ANYWHERE:
   - TrustedRelayRequest MUST ONLY have: TunnelId, Command, TargetService, Payload
   - NO fields for: host, port, address, endpoint, url
   - TargetService is LOGICAL NAME only ("slskdn-ui", "slskdn-api")
   - Mapping to actual endpoints is done internally, not from request

2. TRUST CHECKS ON EVERY COMMAND:
   - Check trust at START of HandleRelayAsync, before switch
   - NOT just on Open command
   - Pattern:
     ```csharp
     public async Task<Response> HandleRelayAsync(Request req, Context ctx, CT ct)
     {
         if (!_config.Enabled) return Error("Disabled");
         if (!_config.TrustedPeerIds.Contains(ctx.RemotePeerId)) return Error("Not trusted");
         if (!_config.AllowedTargetServices.Contains(req.TargetService)) return Error("Service not allowed");
         // NOW safe to process command
     }
     ```

3. TUNNEL LIFECYCLE:
   - Use try/finally or using pattern for cleanup
   - Clean up on: Close command, peer disconnect, timeout, error
   - Track: TunnelId → (PeerId, TargetService, CreatedAt)

4. DEFAULTS:
   - TrustedRelay.Enabled = false (MANDATORY)
   - TrustedPeerIds = [] (empty by default)
   - AllowedTargetServices = [] (empty by default)

5. TESTS REQUIRED:
   - Test: Untrusted peer → all commands rejected
   - Test: Trusted peer, unknown TargetService → rejected
   - Test: Trusted peer, allowed service → allowed
   - Test: Tunnel cap enforced
   - Test: Tunnel cleanup on error
```

**Test Requirements**:
```csharp
[Fact]
public async Task TrustedRelay_RejectsUntrustedPeer()
{
    _config.Enabled = true;
    _config.TrustedPeerIds = new[] { "peer-123" };
    _config.AllowedTargetServices = new[] { "slskdn-ui" };
    
    var untrustedContext = new Context { RemotePeerId = "peer-456" }; // Not in list!
    var request = new TrustedRelayRequest
    {
        Command = TrustedRelayCommand.Open,
        TargetService = "slskdn-ui"
    };
    
    var response = await _relay.HandleRelayAsync(request, untrustedContext, ct);
    Assert.False(response.Success);
    Assert.Contains("not trusted", response.ErrorMessage, StringComparison.OrdinalIgnoreCase);
}

[Fact]
public async Task TrustedRelay_RejectsDisallowedService()
{
    _config.Enabled = true;
    _config.TrustedPeerIds = new[] { "peer-123" };
    _config.AllowedTargetServices = new[] { "slskdn-ui" };
    
    var request = new TrustedRelayRequest
    {
        Command = TrustedRelayCommand.Open,
        TargetService = "evil-service" // Not in list!
    };
    
    var response = await _relay.HandleRelayAsync(request, trustedContext, ct);
    Assert.False(response.Success);
}

[Fact]
public async Task TrustedRelay_EnforcesTunnelCaps()
{
    _config.MaxTunnelsPerPeer = 2;
    
    // Open 3 tunnels from same peer
    var tunnel1 = await OpenTunnel(peerId, "service1");
    var tunnel2 = await OpenTunnel(peerId, "service2");
    var tunnel3 = await OpenTunnel(peerId, "service3");
    
    Assert.True(tunnel1.Success);
    Assert.True(tunnel2.Success);
    Assert.False(tunnel3.Success); // Should be capped
}

[Fact]
public void TrustedRelayRequest_HasNoHostPortFields()
{
    // Use reflection to verify
    var type = typeof(TrustedRelayRequest);
    var properties = type.GetProperties();
    
    Assert.DoesNotContain(properties, p => p.Name.Contains("Host"));
    Assert.DoesNotContain(properties, p => p.Name.Contains("Port"));
    Assert.DoesNotContain(properties, p => p.Name.Contains("Address"));
    Assert.DoesNotContain(properties, p => p.Name.Contains("Endpoint"));
}
```

**Human Review Checklist**:
- [ ] Does TrustedRelayRequest have ANY host/port/address fields? (REJECT if yes)
- [ ] Is trust checked on EVERY command, not just Open? (REJECT if no)
- [ ] Are tunnels cleaned up in try/finally? (REJECT if no)
- [ ] Is TrustedRelay.Enabled = false by default? (REJECT if no)
- [ ] Are TrustedPeerIds and AllowedTargetServices empty by default? (REJECT if no)

---

### 🟡 MEDIUM RISK: Mesh & HTTP Exposure (H-15 / H-PR05)

**Tasks**: Service allowlists, quotas, logging, metrics for proxy/relay services

**Why LLMs Fail Here**:
- **Lots of config + glue logic**:
  - Service allowlists
  - Route allowlists
  - Per-peer quotas
- Easy to:
  - Wire things "open" by default (simpler)
  - Forget to hook quota enforcement into some services
  - Use high-cardinality labels in metrics
  - Log sensitive data

**Predictable Failure Modes**:
1. **"Open by default"**: Allowlists not enforced:
   ```csharp
   // BAD: Default to all allowed
   public List<string> AllowedServices { get; set; } = new() { "*" }; // WRONG!
   ```

2. **"Quota defined but not enforced"**: Config exists but never checked:
   ```csharp
   // Config has MaxCatalogFetchesPerPeerPerMinute = 20
   // But service never calls quota tracker!
   ```

3. **"Metric cardinality explosion"**:
   ```csharp
   // BAD: URL as label (infinite cardinality)
   _metrics.RecordRequest("catalog_fetch", new { url = request.Url });
   ```

**Mitigation Strategy**:

**STRICT PROMPT REQUIREMENTS**:
```
H-15 / H-PR05 RULES (MANDATORY):

1. CONSERVATIVE DEFAULTS:
   - TrustedRelay.Enabled = false
   - AllowRemoteIntentManagement = false
   - AllowPlanExecution = false (via HTTP)
   - AllowedServices = [] (empty, must opt-in)

2. QUOTA ENFORCEMENT:
   - Every proxy/relay service MUST call quota tracker
   - Pattern at service entry:
     ```csharp
     if (!_quotaTracker.CheckQuota(peerId, QuotaType.CatalogFetch))
         return Error("Quota exceeded");
     ```

3. LOGGING:
   - NO full URLs (domain only)
   - NO query strings
   - NO secrets (API keys, tokens)
   - NO file paths (filename only)

4. METRICS:
   - LOW CARDINALITY ONLY:
     * backend (soulseek, mesh, local, torrent)
     * result (success, error, timeout, quota_exceeded)
     * service_type (catalog-fetch, content-relay, trusted-relay)
   - FORBIDDEN:
     * url, content_id, peer_name, track_id as labels

5. INTEGRATION TESTS:
   - Test: Toggle allowlist on/off → endpoints become usable/unusable
   - Test: Quota enforcement → repeated requests eventually fail
   - Test: Metrics don't contain high-cardinality labels
```

**Test Requirements**:
```csharp
[Fact]
public async Task Allowlist_DisablesServices()
{
    _config.AllowedServices = new[] { "catalog-fetch" }; // Only this one
    
    var catalogResult = await CallService("catalog-fetch", ...);
    Assert.True(catalogResult.Success);
    
    var relayResult = await CallService("content-relay", ...); // Not in allowlist
    Assert.False(relayResult.Success);
}

[Fact]
public async Task Quotas_AreEnforced()
{
    _config.MaxCatalogFetchesPerPeerPerMinute = 5;
    
    for (int i = 0; i < 5; i++)
        await _service.HandleFetchAsync(...); // Should succeed
    
    var result = await _service.HandleFetchAsync(...); // 6th call
    Assert.Equal(ServiceStatusCodes.QuotaExceeded, result.StatusCode);
}

[Fact]
public void Metrics_UseLowCardinalityLabels()
{
    // Call service many times with different URLs
    for (int i = 0; i < 100; i++)
        _service.HandleFetchAsync(new Request { Url = $"http://example.com/{i}" });
    
    var metrics = _metricsCollector.GetMetrics();
    var catalogFetchMetric = metrics.First(m => m.Name == "proxy_catalog_fetch_requests_total");
    
    // Should have low number of label combinations (result, from_cache)
    Assert.True(catalogFetchMetric.LabelCombinations.Count < 10);
}
```

**Human Review Checklist**:
- [ ] Are defaults conservative? (TrustedRelay.Enabled = false, etc.)
- [ ] Are quotas checked in all services? (REJECT if no)
- [ ] Do any logs contain full URLs or secrets? (REJECT if yes)
- [ ] Do any metrics use high-cardinality labels? (REJECT if yes)

---

## Safe Warm-Up Tasks

These are **lower risk** and good for building confidence with Cursor:

### 🟢 LOW RISK: Define Primitives (T-PR01)

**Why It's Safe**:
- Just defining types and stubs
- No complex logic or state
- Easy to verify (compile + basic tests)

**Still Watch For**:
- Over-engineering (unnecessary abstractions)
- Adding fields that shouldn't be there (host, port)

---

### 🟢 LOW RISK: Catalogue Fetch Service (T-PR02)

**Why It's Safer** (but not completely safe):
- Relatively isolated
- Clear inputs/outputs

**Still Watch For**:
- Domain allowlist bypass
- SSRF-safe client bypass
- Cache logic bugs

**Mitigation**: Hammer on the whitelist and SSRF protection prompts

---

### 🟢 LOW RISK: Identity Separation (H-11)

**Why It's Safer**:
- Fewer moving parts than planner
- More of a refactoring than new logic

**Still Watch For**:
- Incomplete migration (some places still use usernames)
- Breaking existing code

---

## Implementation Order Recommendation

**Phase 1: Build Confidence** (Low Risk):
1. T-PR01 (Define primitives) ✅ Safe warm-up
2. T-PR02 (Catalogue fetch) ⚠️ Watch allowlist/SSRF
3. H-11 (Identity separation) ✅ Fewer moving parts

**Phase 2: Dangerous Territory** (High Risk):
4. T-VC01 (Domain abstraction) 🔴 **STRICT PROMPTS REQUIRED**
5. H-13 (Backend safety) 🟠 Watch budget/SSRF
6. T-VC04 + H-14 (Planner + gating) 🔴 **STRICT PROMPTS REQUIRED**

**Phase 3: More Dangerous** (High Risk):
7. T-PR03 (Content relay) 🟠 Watch path confusion
8. T-PR04 (Trusted relay) 🟠 Watch generic proxy creep

**Phase 4: Polish** (Medium Risk):
9. H-15 / H-PR05 (Exposure + quotas) 🟡 Watch defaults

---

## When You Need a Tight Prompt

If you want a **very tight Cursor prompt** for one of the dangerous tasks (T-VC01, T-PR03, T-VC04, etc.), I can write one that specifies:
- Exact diff shape allowed
- Files that MUST NOT be touched
- How to prove behavior didn't change
- Step-by-step verification

Let me know which task you want to tackle first, and I'll craft the paranoid-bastard-approved prompt for it.

---

**Status**: CRITICAL REFERENCE - Read Before Starting Tasks  
**Last Updated**: December 11, 2025  
**Usage**: Check risk level before implementing, follow mitigation strategies strictly

---

*"Not all tasks are created equal. Some will bite you. Here's the map of where the mines are."*

*"The paranoid bastard's guide to not getting wrecked by LLM code generation."*
