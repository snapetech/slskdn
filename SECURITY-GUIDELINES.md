# Security & Hardening Guidelines

**Status**: MANDATORY FOR ALL TASKS  
**Created**: December 11, 2025  
**Scope**: VirtualSoulfind v2, Multi-Domain, Proxy/Relay, ALL Future Work  
**Philosophy**: Paranoid Bastard Mode - Security is Non-Negotiable

> **Project Note**: This is a fork of [slskd](https://github.com/slskd/slskd). See [README.md](README.md#acknowledgments) for attribution.

---

## Overview

This document defines **mandatory security and hardening requirements** for all work in this repository.

**Applies to**:
- VirtualSoulfind v2 and multi-domain work (T-VC01–T-VC04, H-11–H-15, VirtualSoulfind design doc)
- Proxy/relay stack (T-PR01–T-PR04, H-PR05)
- Service fabric (T-SF01–T-SF07, H-01–H-10)
- **ALL future features and refactorings**

**Non-Negotiable**: Everything below is **mandatory** unless explicitly contradicted by a more specific requirement. If a task brief conflicts with these guidelines, these guidelines win.

---

## 1. Global Hardening Principles

When implementing, refactoring, or extending anything in this area, you **MUST** follow these rules:

### 1.1 Security Posture

#### Default-Deny

- Features that touch the network, expose APIs, or share data **MUST** be:
  - Disabled by default, **OR**
  - Enabled only in a safe, low-privilege mode by default

**Examples**:
- ✅ `TrustedRelay.Enabled = false` (disabled by default)
- ✅ `CatalogFetch.AllowedDomains = ["musicbrainz.org"]` (restrictive default)
- ❌ `Gateway.ApiKey = ""` (no authentication, FORBIDDEN)

#### Least Privilege

- Services **MUST** only have the minimal permissions/visibility they need:
  - File access limited to content/library directories
  - Mesh services limited to their defined methods
  - Network access limited to explicitly allowed destinations

**Examples**:
- ✅ Content relay only reads from VirtualSoulfind-managed paths
- ✅ Catalogue fetch only contacts whitelisted domains
- ❌ Service can read arbitrary filesystem paths (FORBIDDEN)

#### No Generic Proxy/Anonymizer

- Do **NOT** implement:
  - Generic SOCKS
  - Arbitrary TCP relay by host/port
  - HTTP CONNECT to arbitrary hosts
  - Raw socket tunneling for untrusted peers

- All relay/proxy behavior **MUST** be **application- and policy-specific**:
  - Catalogue fetch: Domain allowlist only
  - Content relay: Content ID mapping only
  - Trusted relay: Peer allowlist + target service allowlist only

### 1.2 Work Budget & Rate Limits

For any new network or CPU-heavy code:

**You MUST**:
1. Integrate with the global **work budget** (H-02)
2. Apply **per-peer quotas** and **global caps** where appropriate
3. Fail fast with a clear, structured error if budget/quotas exceeded

**You MUST NOT**:
- Attempt the work "just once more" when budget exhausted
- Skip budget checks for "trusted" peers (trust doesn't exempt budget)
- Use infinite retry loops

**Example**:
```csharp
if (!context.WorkBudget.TryConsume(WorkCosts.CatalogFetch))
{
    return new ServiceReply
    {
        StatusCode = ServiceStatusCodes.QuotaExceeded,
        ErrorMessage = "Work budget exhausted"
    };
}
```

### 1.3 Input Validation & Bounds

All external inputs (mesh messages, HTTP, config) **MUST** be validated:

**Length Bounds**:
- Strings: URLs, IDs, headers (enforce max length)
- Collections: Max count for lists, dictionaries
- Payloads: Max bytes for bodies, chunks

**Type Safety**:
- Strict enum parsing for domains, commands, methods
- No implicit string-to-enum conversions
- Reject unknown/invalid enum values

**Trust Model**:
- Never assume "trusted mesh peer"
- Treat all mesh input as untrusted unless explicitly cross-checked with trust config
- Validate even from "trusted" sources (defense in depth)

**Example**:
```csharp
// BAD: No validation
var url = request.Url;
await _httpClient.GetAsync(url); // SSRF vulnerability!

// GOOD: Validation + SSRF protection
if (string.IsNullOrWhiteSpace(request.Url) || request.Url.Length > 2048)
    return Error("Invalid URL");

if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var uri))
    return Error("Malformed URL");

if (!_config.AllowedDomains.Contains(uri.Host))
    return Error("Domain not allowed");

await _safeHttpClient.GetAsync(uri); // SSRF-safe client
```

### 1.4 Logging & Metrics

#### Logs MUST NOT Include

- ❌ Soulseek usernames (use opaque internal IDs)
- ❌ External IP addresses (use peer IDs)
- ❌ Full local filesystem paths (use filename only or sanitized paths)
- ❌ Secrets (API keys, tokens, passwords, CSRF tokens)
- ❌ Full URLs with query strings (redact query params by default)

#### Logs SHOULD Include

- ✅ Service name, method, operation
- ✅ Peer ID (opaque mesh identity)
- ✅ Result (success/error/timeout)
- ✅ Duration/latency (for performance)
- ✅ Work budget consumed

**Example**:
```csharp
// BAD: Leaks sensitive data
_logger.LogInformation("Fetching {Url} for user {Username} at {IpAddress}", 
    fullUrl, username, ipAddress);

// GOOD: Safe logging
_logger.LogInformation(
    "[CatalogFetch] Request from {PeerId}: {Domain} → {Result}",
    context.RemotePeerId, uri.Host, result);
```

#### Metrics MUST NOT Use

- ❌ High-cardinality labels:
  - Per-track IDs
  - Per-release IDs
  - Full URLs
  - Peer names/usernames
  - Content IDs

#### Metrics SHOULD Use

- ✅ Low-cardinality labels:
  - `backend` (soulseek, mesh, local, torrent)
  - `result` (success, error, timeout, quota_exceeded)
  - `origin` (user_local, mesh, gateway)
  - `service_type` (catalog-fetch, content-relay, trusted-relay)
  - `domain` (music, generic_file) for multi-domain

**Example**:
```csharp
// BAD: High cardinality
_metrics.RecordRequest("catalog_fetch", new { url = fullUrl }); // Infinite labels!

// GOOD: Low cardinality
_metrics.RecordRequest("catalog_fetch", new { domain = uri.Host, result = "success" });
```

### 1.5 Async & Concurrency

**You MUST**:
- Use async/await for all network and disk I/O
- Use `ConfigureAwait(false)` where appropriate (non-UI contexts)
- Use `CancellationToken` for all long-running operations

**You MUST NOT**:
- Use `.Result`, `.Wait()`, or blocking on async tasks (deadlock risk)
- Hold locks across awaited calls (deadlock risk)
- Use `Task.Run()` for I/O operations (unnecessary thread pool usage)

**For Shared State**:
- Use proper thread-safe primitives:
  - `ConcurrentDictionary<TKey, TValue>`
  - `ImmutableList<T>` / `ImmutableDictionary<TKey, TValue>`
  - `Interlocked.*` for counters
- Keep critical sections small
- Document thread-safety guarantees

**Example**:
```csharp
// BAD: Blocking async
var result = DoAsyncWork().Result; // Deadlock risk!

// BAD: Lock across await
lock (_lock)
{
    await DoAsyncWork(); // Deadlock risk!
}

// GOOD: Async all the way
var result = await DoAsyncWork(cancellationToken);

// GOOD: Lock only synchronous critical section
SomeValue value;
lock (_lock)
{
    value = _cache.GetOrAdd(key, CreateValue);
}
await ProcessAsync(value);
```

---

## 2. VirtualSoulfind & Multi-Domain (T-VC* + H-11–H-15)

These instructions apply to VirtualSoulfind v2, domain abstraction, and catalog/index operations.

### 2.1 Identity & Data Separation

**VirtualSoulfind MUST NOT persist**:
- ❌ Soulseek usernames
- ❌ Soulseek room names
- ❌ External IP addresses
- ❌ Any PII from external sources

**Soulseek-specific identifiers MUST**:
- Live in Soulseek-specific modules/tables
- Be referenced in VirtualSoulfind only via **opaque internal IDs**

**For multi-domain support**:
- Use `ContentDomain` as the only cross-cutting differentiator
- Do not special-case "music" elsewhere
- Keep domain logic in domain-specific providers

**Example**:
```csharp
// BAD: Soulseek username in VirtualSoulfind table
public class DesiredTrack
{
    public string SoulseekUsername { get; set; } // FORBIDDEN!
}

// GOOD: Opaque internal ID
public class DesiredTrack
{
    public Guid SourceCandidateId { get; set; } // References opaque source
}
```

### 2.2 Privacy Modes

Implement and respect `VirtualSoulfind.PrivacyMode`:

#### Normal Mode
- May store per-peer/per-source data, but still only opaque IDs
- Tracks which peer/source has what content
- Enables smart source selection

#### Reduced Mode
- **MUST** avoid storing per-peer level details
- Source information is aggregated or abstracted
- Example: "mesh has this" instead of "peer X has this"
- Trades features for less correlation

**Whenever you add a new table/field relevant to VirtualSoulfind, consider how it behaves under each mode.**

**Example**:
```csharp
// In Reduced mode, aggregate sources
if (_config.PrivacyMode == PrivacyMode.Reduced)
{
    // Don't store: "Peer A has track X"
    // Instead store: "Mesh backend has track X (3 sources)"
    candidate = new SourceCandidate
    {
        BackendType = "mesh",
        SourceCount = sources.Count // Aggregated
    };
}
else // Normal mode
{
    // Store per-peer info (but still opaque IDs)
    candidate = new SourceCandidate
    {
        BackendType = "mesh",
        PeerId = peerId, // Opaque mesh peer ID
        SourceMetadata = metadata
    };
}
```

### 2.3 Intent Queue & Catalogue

**Intent creation via remote paths** (mesh, HTTP gateway):
- **MUST** be:
  - Disabled by default (`AllowRemoteIntentManagement = false`)
  - Rate-limited by peer/IP if enabled
  - Logged with origin for audit trail

**All intents MUST have**:
- `Origin` tagged correctly:
  - `UserLocal` (priority 1)
  - `LocalAutomation` (priority 2)
  - `RemoteMesh` (priority 3, capped)
  - `RemoteGateway` (priority 4, capped)

**Catalog & intent DBs MUST**:
- Live under a dedicated app data directory
- Rely on OS-level filesystem permissions
- Support "run-as-dedicated-user" deployment (H-09 style)
- Never store secrets in DB (use secure key storage)

**Example**:
```csharp
// Reject remote intent creation if disabled
if (context.Origin.IsRemote() && !_config.AllowRemoteIntentManagement)
{
    return new ServiceReply
    {
        StatusCode = ServiceStatusCodes.Forbidden,
        ErrorMessage = "Remote intent management disabled"
    };
}

// Apply per-origin rate limits
if (!_quotaTracker.CheckQuota(context.RemotePeerId, context.Origin))
{
    return ServiceReply.QuotaExceeded();
}
```

### 2.4 Domain Abstraction & Soulseek Gating

**Planner and backends MUST be domain-aware**:

#### ContentDomain.Music
- **May** use Soulseek backend (subject to caps and modes)
- MBID-based matching
- Duration/bitrate matching

#### Non-Music Domains (GenericFile, future Movie/TV/Book)
- **MUST NOT** invoke Soulseek backend at all
- Use hash-based matching
- Use domain-specific metadata

**When adding new domains**:
- Implement domain provider(s) behind clear interfaces:
  - `IMusicContentDomainProvider`
  - `IGenericFileContentDomainProvider`
  - `IMovieContentDomainProvider` (future)
- Keep matching rules domain-specific and isolated
- No cross-domain logic bleeding

**Example**:
```csharp
// BAD: Domain logic scattered
if (item.IsMusicTrack)
    await _soulseekClient.SearchAsync(...); // Special case!

// GOOD: Domain-aware routing
var provider = _domainProviderFactory.GetProvider(item.ContentDomain);
var candidates = await provider.FindCandidatesAsync(item, backends);

// In MusicContentDomainProvider:
public async Task<IReadOnlyList<SourceCandidate>> FindCandidatesAsync(...)
{
    // Music domain MAY use Soulseek
    if (backends.Contains(BackendType.Soulseek))
    {
        await _soulseekBackend.SearchAsync(...); // Gated by domain!
    }
}

// In GenericFileContentDomainProvider:
public async Task<IReadOnlyList<SourceCandidate>> FindCandidatesAsync(...)
{
    // GenericFile domain MUST NOT use Soulseek
    // Only use: Mesh, BT, HTTP, Local
    // Compile-time enforced: no _soulseekBackend reference!
}
```

### 2.5 Matching, Verification & Advertisement

**Only treat content as "verified" when**:
- Duration/size matches within configured tolerance
- Hashes/fingerprints (if used) match stable, locally computed values
- File is readable and parseable (for media files)

**Only advertise content via DHT/mesh that**:
- Is marked as shared & not private
- Has passed minimum verification requirements
- Is in a shareable content domain (respect user privacy settings)

**Never store or publish**:
- ❌ 3rd-party library details (what other Soulseek users share)
- ❌ Source peer identities in public indexes
- ❌ Unverified content as "available"

**Example**:
```csharp
// Verification before advertisement
public async Task<bool> VerifyAndAdvertiseAsync(LocalFile file)
{
    // 1. Verify locally
    var metadata = await _parser.ParseAsync(file.Path);
    if (Math.Abs(metadata.Duration - file.ExpectedDuration) > TimeSpan.FromSeconds(2))
        return false; // Duration mismatch

    // 2. Mark as verified
    file.IsVerified = true;
    await _db.SaveChangesAsync();

    // 3. Advertise ONLY if shared
    if (file.IsShared && !file.IsPrivate)
    {
        await _meshDirectory.PublishContentAsync(file.ContentId);
    }

    return true;
}
```

### 2.6 Service & Gateway Exposure (VirtualSoulfind)

**Mesh services**:
- Expose **read-heavy, write-light** methods by default:
  - ✅ Metadata queries
  - ✅ Missing track summaries
  - ✅ Catalogue stats
- Mutating operations (creating intents, executing plans):
  - Either not exposed over mesh, **OR**
  - Restricted to trusted peers with quotas

**HTTP gateway**:
- Use gateway auth (API key) + CSRF header (H-01) for all mutating operations
- Drive exposure via explicit allowlists:
  - Only expose `/virtual/*` routes that you intend to support
- By default, disable plan execution from HTTP (`AllowPlanExecution = false`)
- Log all mutating operations with origin

**Example**:
```csharp
// Mesh service: read-only by default
public class VirtualSoulfindMeshService : IMeshService
{
    public async Task<ServiceReply> HandleCallAsync(ServiceCall call, ...)
    {
        switch (call.Method)
        {
            case "GetMissingTracks": // Read-only, allowed
                return await GetMissingTracksAsync(call, context);
            
            case "CreateIntent": // Mutating, check config
                if (!_config.AllowRemoteIntentManagement)
                    return ServiceReply.Forbidden("Remote intent creation disabled");
                
                if (!IsExplicitlyTrusted(context.RemotePeerId))
                    return ServiceReply.Forbidden("Not a trusted peer");
                
                return await CreateIntentAsync(call, context);
        }
    }
}

// HTTP gateway: require auth for mutating ops
[HttpPost("virtual/execute-plan")]
[RequireApiKey] // H-01 middleware
[RequireCsrf]   // H-01 middleware
public async Task<IActionResult> ExecutePlan(...)
{
    if (!_config.AllowPlanExecution)
        return StatusCode(403, "Plan execution disabled via HTTP");
    
    // Additional work budget check
    if (!_workBudget.TryConsume(WorkCosts.PlanExecution))
        return StatusCode(429, "Work budget exhausted");
    
    // ... execute plan
}
```

---

## 3. Proxy/Relay Stack (T-PR01–T-PR04 + H-PR05)

These instructions apply to:
- `catalog-fetch` service
- `content-relay` service
- `trusted-relay` service

### 3.1 No Generic Proxy Behavior

**You MUST NOT implement**:
- ❌ Arbitrary host/port tunneling
- ❌ Generic SOCKS or raw TCP streaming for untrusted peers
- ❌ HTTP CONNECT to arbitrary hosts
- ❌ DNS resolution for user-supplied hostnames
- ❌ Any feature that resembles an "exit node"

**Only the following proxy-ish capabilities are allowed, with hard scoping**:
1. Catalog/API fetch for **whitelisted domains**
2. Content chunk relay for **known content IDs**
3. Trusted relay for **your own nodes/friends** and only to **allowed internal services**

### 3.2 Catalog Fetch Service

**Only allow**:
- HTTP/HTTPS URLs (scheme validation required)
- `GET` and `HEAD` methods **ONLY**
- Absolute URLs (no relative URLs accepted)

**Enforce domain allowlist**:
- Hosts **MUST** be in `CatalogFetch.AllowedDomains`
- No wildcards unless you're very confident
- If you support subdomains, do it explicitly (e.g., `api.example.com`)
- Default allowlist: `["musicbrainz.org", "coverartarchive.org"]`

**Use SSRF-safe client**:
- No access to loopback (`127.0.0.1`, `::1`) or private IP ranges by default
- Use centralized `ISafeHttpClient`
- Do **NOT** bypass it with raw `HttpClient`
- Verify scheme is HTTP/HTTPS only

**Enforce response caps**:
- Max body size (e.g., 256 KB default)
- Request timeout (e.g., 10 seconds default)
- Set `IsTruncated = true` when truncated
- Limit header count and size

**Caching**:
- Cache only responses from allowed domains
- Treat cache as non-authoritative: always respect TTL
- Cache key: `(Url, Method, HeadersSubset)`
- Max cache size (LRU eviction)

**Example**:
```csharp
public async Task<CatalogFetchResponse> HandleFetchAsync(
    CatalogFetchRequest request,
    MeshServiceContext context,
    CancellationToken ct)
{
    // 1. Validate method
    if (request.Method != "GET" && request.Method != "HEAD")
        return Error("Only GET and HEAD allowed");
    
    // 2. Parse URL
    if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var uri))
        return Error("Invalid URL");
    
    if (uri.Scheme != "http" && uri.Scheme != "https")
        return Error("Only HTTP/HTTPS allowed");
    
    // 3. Check domain allowlist
    if (!_config.AllowedDomains.Contains(uri.Host))
        return Error($"Domain not allowed: {uri.Host}");
    
    // 4. Check cache
    var cacheKey = (request.Url, request.Method);
    if (_cache.TryGet(cacheKey, out var cached))
        return cached; // No work budget consumed for cache hit
    
    // 5. Check work budget
    if (!context.WorkBudget.TryConsume(WorkCosts.CatalogFetch))
        return Error("Work budget exhausted");
    
    // 6. Fetch via SSRF-safe client
    var response = await _safeHttpClient.FetchAsync(
        uri,
        request.Method,
        maxBytes: _config.MaxBodyBytes,
        timeout: TimeSpan.FromSeconds(_config.RequestTimeoutSeconds),
        ct);
    
    // 7. Cache result
    _cache.Set(cacheKey, response, TimeSpan.FromSeconds(_config.CacheTtlSeconds));
    
    return response;
}
```

### 3.3 Content Relay / CDN

**Content relay MUST**:
- Accept only **content IDs**, never file paths
- Resolve content IDs via VirtualSoulfind and LocalLibrary
- Serve only files that are:
  - Present on disk
  - Marked as verified/advertisable
  - In the local library catalog

**Chunking**:
- Enforce `MaxChunkBytes` cap (e.g., 64 KB default)
- Properly handle EOF (return `IsLastChunk = true`)
- Validate offset and length (no negative values, no overflow)

**Integrity**:
- If chunk hash is calculated:
  - Use a fast, deterministic hash (xxHash-like) server-side only
  - Do not expose this as a magic "authenticator" without verification at the caller
  - Optional feature, not required for all chunks

**Caps**:
- Enforce:
  - `MaxConcurrentStreamsPerPeer` (e.g., 2 default)
  - `MaxConcurrentStreamsGlobal` (e.g., 20 default)
  - Per-peer rate (chunks/minute, e.g., 200 default)

**Example**:
```csharp
public async Task<ContentChunkResponse> HandleChunkRequestAsync(
    ContentChunkRequest request,
    MeshServiceContext context,
    CancellationToken ct)
{
    // 1. Validate request
    if (string.IsNullOrWhiteSpace(request.ContentId))
        return Error("ContentId required");
    
    if (request.Offset < 0 || request.Length <= 0)
        return Error("Invalid offset/length");
    
    if (request.Length > _config.MaxChunkBytes)
        request.Length = _config.MaxChunkBytes; // Clamp
    
    // 2. Check stream caps
    if (!_streamTracker.TryAcquireStream(context.RemotePeerId))
        return Error("Stream cap exceeded");
    
    try
    {
        // 3. Check work budget
        if (!context.WorkBudget.TryConsume(WorkCosts.ContentChunkRelay))
            return Error("Work budget exhausted");
        
        // 4. Resolve content ID to file (NO arbitrary paths!)
        var contentItem = await _virtualSoulfind.ResolveContentIdAsync(request.ContentId);
        if (contentItem == null)
            return Error("Content not found");
        
        var localFile = await _localLibrary.GetFileAsync(contentItem.LocalFileId);
        if (localFile == null || !localFile.IsVerified || !localFile.IsAdvertisable)
            return Error("Content not available");
        
        // 5. Read chunk (safe, via content system)
        var data = await _chunkReader.ReadChunkAsync(
            localFile.Path,
            request.Offset,
            request.Length,
            ct);
        
        return new ContentChunkResponse
        {
            Data = data,
            IsLastChunk = (request.Offset + data.Length >= localFile.Size),
            HashOfChunk = _config.ComputeChunkHashes ? ComputeHash(data) : null
        };
    }
    finally
    {
        _streamTracker.ReleaseStream(context.RemotePeerId);
    }
}
```

### 3.4 Trusted Relay

**`TrustedRelay.Enabled` MUST be `false` by default.**

**Requests MUST be accepted only when**:
- Origin peer ID is in `TrustedPeerIds`
- `TargetService` is in `AllowedTargetServices`
- Both checks must pass (AND, not OR)

**No host/port-level routing**:
- All routing is by **logical service name**
- Mapping to real addresses or endpoints is done locally and must be explicit
- No DNS resolution of user-supplied hostnames

**Tunnels**:
- Enforce caps:
  - `MaxTunnelsPerPeer` (e.g., 4 default)
  - `MaxTunnelsGlobal` (e.g., 20 default)
- Clean up state properly on `Close` or peer disconnect
- Implement tunnel timeout (auto-close inactive tunnels)

**Payload**:
- Treat `Payload` as opaque at the mesh level
- Still count it for budget and quotas
- Do not attempt to compress/encrypt arbitrarily
- Leave that to higher-level protocols if needed

**Example**:
```csharp
public async Task<TrustedRelayResponse> HandleRelayAsync(
    TrustedRelayRequest request,
    MeshServiceContext context,
    CancellationToken ct)
{
    // 1. Check if trusted relay enabled
    if (!_config.Enabled)
        return Error("Trusted relay disabled");
    
    // 2. Check peer trust
    if (!_config.TrustedPeerIds.Contains(context.RemotePeerId))
        return Error("Peer not trusted");
    
    // 3. Check target service allowlist
    if (!_config.AllowedTargetServices.Contains(request.TargetService))
        return Error($"Target service not allowed: {request.TargetService}");
    
    // 4. Check work budget
    var workCost = request.Command == TrustedRelayCommand.Data
        ? WorkCosts.TrustedRelayData
        : WorkCosts.TrustedRelayControl;
    
    if (!context.WorkBudget.TryConsume(workCost))
        return Error("Work budget exhausted");
    
    // 5. Handle command
    switch (request.Command)
    {
        case TrustedRelayCommand.Open:
            if (!_tunnelTracker.TryOpenTunnel(request.TunnelId, context.RemotePeerId))
                return Error("Tunnel cap exceeded");
            break;
        
        case TrustedRelayCommand.Data:
            if (!_tunnelTracker.IsOpen(request.TunnelId, context.RemotePeerId))
                return Error("Tunnel not open");
            
            // Route to internal service (NO arbitrary host:port!)
            var response = await _targetRouter.RouteAsync(
                request.TargetService,
                request.Payload,
                ct);
            
            return new TrustedRelayResponse
            {
                TunnelId = request.TunnelId,
                Success = true,
                Payload = response
            };
        
        case TrustedRelayCommand.Close:
            _tunnelTracker.CloseTunnel(request.TunnelId, context.RemotePeerId);
            break;
    }
    
    return new TrustedRelayResponse
    {
        TunnelId = request.TunnelId,
        Success = true
    };
}
```

### 3.5 Budget, Quotas, and Observability for Proxy/Relay

**All proxy/relay services MUST**:
1. Consume from the global work budget appropriate to the operation:
   - `WorkCosts.CatalogFetch`
   - `WorkCosts.ContentChunkRelay`
   - `WorkCosts.TrustedRelay` (or `TrustedRelayData`/`TrustedRelayControl`)

2. Enforce per-peer quotas (requests/minute) via configuration:
   - `ProxyQuota.MaxCatalogFetchesPerPeerPerMinute`
   - `ProxyQuota.MaxContentChunksPerPeerPerMinute`
   - `ProxyQuota.MaxTrustedRelayMessagesPerPeerPerMinute`

**Metrics**:
Counters per service:
- `proxy_catalog_fetch_requests_total{result=success|cached|quota_exceeded|denied|error}`
- `proxy_content_chunks_served_total{result=success|quota_exceeded|denied|error}`
- `proxy_trusted_relay_messages_total{result=success|quota_exceeded|denied|error}`

**Logs**:
- **MUST NOT** log full URLs with query strings by default
- **MAY** log:
  - Domain (without path/query)
  - Path without query (if needed for debugging)
  - Peer ID (opaque mesh identity)
  - Result (success/error/denied)
  - Duration/latency

**Example**:
```csharp
// Metric recording
_metrics.RecordProxyRequest("catalog_fetch", new
{
    result = success ? "success" : "error",
    from_cache = cached
});

// Logging (safe)
_logger.LogInformation(
    "[CatalogFetch] {PeerId} requested {Domain} → {Result} (cached: {Cached}, duration: {Duration}ms)",
    context.RemotePeerId,
    uri.Host, // Domain only, no path/query
    result,
    cached,
    duration.TotalMilliseconds);

// NOT this (leaks full URL):
// _logger.LogInformation("Fetching {Url}", request.Url); // FORBIDDEN!
```

---

## 4. Test & Review Requirements

Any PR or change touching VirtualSoulfind or proxy/relay services **MUST** include:

### 4.1 Unit Tests

**Cover**:
- Happy paths (basic functionality works)
- Validation failures (malformed inputs rejected)
- Budget/limit exhaustion behaviors (quotas enforced)
- Domain/mode gating (e.g., Soulseek only for music; no Soulseek for GenericFile)

**Ensure tests assert that**:
- Unsafe inputs are rejected
- Unsafe modes cannot be enabled without config flags
- Work budget is consumed correctly
- Rate limits are enforced

**Example test names**:
- `CatalogFetch_RejectsDisallowedDomain()`
- `CatalogFetch_RejectsPostMethod()`
- `CatalogFetch_EnforcesWorkBudget()`
- `ContentRelay_RejectsUnknownContentId()`
- `ContentRelay_RejectsUnverifiedContent()`
- `TrustedRelay_RejectsUntrustedPeer()`
- `TrustedRelay_RejectsDisallowedTargetService()`

### 4.2 Integration Tests (Where Feasible)

At least minimal integration tests per service:

**VirtualSoulfind**:
- Domain-aware planner behavior (Music uses Soulseek, GenericFile doesn't)
- Intent origin tagging (UserLocal vs RemoteMesh)
- Privacy mode enforcement (Normal vs Reduced)

**Catalog fetch**:
- Allowed vs disallowed domains/methods
- Cache hit behavior (second request doesn't hit HTTP client)
- SSRF protection (requests to localhost rejected)

**Content relay**:
- Serving known content ID vs unknown/unverified
- Chunk boundary handling (EOF detection)
- Stream cap enforcement

**Trusted relay**:
- Trusted vs untrusted peer (rejection)
- Allowed vs disallowed target service (rejection)
- Tunnel lifecycle (open/data/close)

### 4.3 "Security Sanity" Checklist for Each Task

For every task (T-VC*, H-1x, T-PR*, H-PR):

**Before calling it done, you MUST confirm**:

1. ☑ **Config defaults are safe**:
   - Features disabled or scoped
   - Conservative timeouts/limits
   - No weak credentials (empty API keys, etc.)

2. ☑ **No new generic proxy capability** was introduced:
   - No arbitrary host:port connections
   - No raw socket tunneling
   - No DNS resolution of user-supplied names

3. ☑ **All external calls go through**:
   - Work budget checks
   - Per-peer quotas
   - Appropriate safe client (e.g., SSRF-safe HTTP)

4. ☑ **Logging and metrics**:
   - Do not leak sensitive identifiers (usernames, IPs, secrets)
   - Stick to low-cardinality labels
   - Redact or omit query strings from URLs

5. ☑ **Input validation**:
   - All external inputs validated (length, type, format)
   - Enum values validated (reject unknown)
   - No SQL injection / path traversal / SSRF vectors

6. ☑ **Async correctness**:
   - No `.Result` or `.Wait()`
   - No locks held across awaits
   - CancellationToken plumbed through

7. ☑ **Tests pass**:
   - All unit tests pass
   - Integration tests pass (where applicable)
   - Abuse scenario tests pass

---

## 5. Enforcement & Review

### Code Review Checklist

Reviewers **MUST** verify:
- [ ] Security guidelines followed (all sections above)
- [ ] Config defaults are safe
- [ ] No generic proxy behavior introduced
- [ ] Work budget integrated
- [ ] Logging/metrics privacy-safe
- [ ] Tests cover security scenarios
- [ ] Documentation updated

### Anti-Slop Checklist

Before marking any task complete, confirm:
- [ ] No random helper scripts created
- [ ] No dead/commented code
- [ ] No TODO comments without issues filed
- [ ] No magic numbers (use config)
- [ ] No copy-paste without refactoring
- [ ] No "temporary" workarounds

### Security Mindset

When in doubt, ask:
1. **"Can this be abused?"** (Answer: probably yes, add guards)
2. **"What if this input is malicious?"** (Validate everything)
3. **"What if this peer is hostile?"** (Don't trust mesh peers)
4. **"What if work budget is exhausted?"** (Fail fast, don't retry)
5. **"What if this leaks PII?"** (Redact, sanitize, aggregate)

---

## 6. Summary: The Paranoid Bastard Contract

This document is your **security constitution**. When implementing any feature:

1. **Read this first** before writing code
2. **Reference specific sections** in your PR description
3. **Run the security checklist** before marking complete
4. **Ask for review** if anything is unclear
5. **Update this doc** if new patterns emerge

**Remember**:
- Security is not optional
- Convenience is not an excuse
- "It's just for testing" is not acceptable
- "We'll fix it later" means never

**Tagline**: *"Paranoid Bastard Mode: Where security isn't a feature, it's the foundation."*

---

**Status**: MANDATORY FOR ALL WORK  
**Last Updated**: December 11, 2025  
**Applies To**: ALL tasks, ALL features, ALL refactorings, FOREVER  
**Exceptions**: None (if you need an exception, update this doc first)

---

*"Most projects bolt security on. We build it in from line one."*

*"Not because we're paranoid. Because we're right."*

*"The paranoid bastard's way."*
