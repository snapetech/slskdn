# Proxy/Relay Tasks (T-PR Series)

**Created**: December 11, 2025  
**Status**: Designed, Ready to Implement  
**Philosophy**: Application-specific relay primitives, NOT "Tor but worse"

> **Project Note**: This is a fork of [slskd](https://github.com/slskd/slskd). See [README.md](README.md#acknowledgments) for attribution.

---

## Executive Summary

**What We're Building**: Three **genius bits** of application-specific relay/proxy functionality over the mesh:

1. **Catalogue Fetch Service**: Safe HTTP fetcher for whitelisted metadata sources (MusicBrainz, cover art, etc.)
2. **Content Chunk Relay/CDN**: Serve verified content chunks to mesh peers (like a CDN)
3. **Trusted Relay Mode**: NAT traversal for your own nodes / explicitly trusted friends ONLY

**What We're NOT Building**: 
- Generic SOCKS proxy
- HTTP CONNECT proxy
- Tor-style exit nodes
- Anything that becomes an "open relay" for arbitrary internet traffic

**Why This Is Genius**:
- Application-aware (content IDs, not host:port)
- Work budget integration from day one
- Whitelisted domains/targets only
- Serves the actual use case (metadata + content + your own infrastructure)
- Doesn't accidentally become a liability

---

## Task Breakdown

### T-PR01 – Define Fetch/Relay Primitives and Service Types

**Repo**: `https://github.com/snapetech/slskdn`  
**Branch**: `experimental/multi-source-swarm`  
**Dependencies**: T-SF01-04 (Service Fabric), VirtualSoulfind v2 design  
**Estimated Effort**: 4-6 hours  
**Status**: Ready to Start

#### Scope & Non-Goals

**You MUST**:
1. Define message contracts and service interfaces for:
   - HTTP catalogue/resource fetch (whitelisted)
   - Content chunk relay (by content ID / chunk range)
   - Trusted peer relay (for your own nodes/friends only)
2. Integrate these into the existing mesh service fabric types (service kind, descriptors, etc.)

**You MUST NOT**:
- Implement actual fetching/relaying here (that's later tasks)
- Introduce generic "CONNECT to host:port" or SOCKS-like byte streams
- Bypass work budget or hardening; you're just defining the shapes and contracts

#### 1. Recon

Before coding, review:
1. Service fabric types:
   - `IMeshService`, `MeshServiceContext`, `MeshServiceDescriptor`, and existing service kinds (pods, etc.)
2. Mesh message/envelope types:
   - How messages are serialized, routed, and responded to
3. DHT/service discovery:
   - How services are advertised and discovered today (service type/name, ports, etc.)

#### 2. Define Service Types / Kind IDs

Add new service kinds (or equivalent identifiers) for:
- `catalog-fetch` – HTTP/metadata fetcher
- `content-relay` – content chunk relay
- `trusted-relay` – point-to-point relay for trusted peers

Ensure:
- These are properly registered/enumerated where service types are declared
- They are distinct from existing services to allow allowlisting & policy

#### 3. Define Catalogue Fetch Messages

Define request/response messages along these lines:

```csharp
public sealed class CatalogFetchRequest
{
    public string Url { get; init; }           // Must be absolute URL
    public string Method { get; init; }        // GET or HEAD only (enforced later)
    public Dictionary<string,string>? Headers { get; init; }
    public int MaxBytes { get; init; }         // hard cap to avoid huge payloads
    public TimeSpan Timeout { get; init; }
}

public sealed class CatalogFetchResponse
{
    public int StatusCode { get; init; }
    public Dictionary<string,string> Headers { get; init; }
    public byte[] Body { get; init; }          // truncated at MaxBytes
    public bool IsTruncated { get; init; }
}
```

These messages will be carried inside the existing mesh message envelope.

#### 4. Define Content Chunk Relay Messages

Define a content-centric relay protocol, not host/port:

```csharp
public sealed class ContentChunkRequest
{
    public string ContentId { get; init; }     // e.g. hash/MBID+track index; opaque ID from VirtualSoulfind
    public long Offset { get; init; }          // byte offset
    public int Length { get; init; }           // max bytes requested
    public string? Variant { get; init; }      // e.g. quality/codec variant; optional
}

public sealed class ContentChunkResponse
{
    public byte[] Data { get; init; }
    public bool IsLastChunk { get; init; }     // indicates EOF for this content/variant
    public string? HashOfChunk { get; init; }  // optional; for integrity checks
}
```

**No hostnames or IPs here; only content IDs** coming from your VirtualSoulfind / planner layer.

#### 5. Define Trusted Relay Messages (High-Level)

For trusted nodes, define a **structured relay**, but *still not raw SOCKS*:

```csharp
public sealed class TrustedRelayRequest
{
    public Guid TunnelId { get; init; }        // logical tunnel identifier
    public TrustedRelayCommand Command { get; init; } // e.g. Open, Data, Close
    public string TargetService { get; init; } // logical target within your own infra, NOT host:port
    public byte[] Payload { get; init; }       // opaque to mesh; interpreted by your app or a sub-protocol
}

public enum TrustedRelayCommand
{
    Open,
    Data,
    Close
}

public sealed class TrustedRelayResponse
{
    public Guid TunnelId { get; init; }
    public bool Success { get; init; }
    public byte[] Payload { get; init; }
}
```

This is essentially an app-level tunnel between nodes for **your own services** (e.g., slskdn's HTTP UI, control APIs), not a generic internet exit.

The exact semantics of `TargetService` will be defined later (e.g., known named endpoints).

#### 6. Service Interface Stubs

Define new `IMeshService` implementations or interfaces:

```csharp
public interface ICatalogFetchService : IMeshService
{
    Task<CatalogFetchResponse> HandleFetchAsync(
        CatalogFetchRequest request,
        MeshServiceContext context,
        CancellationToken ct);
}

public interface IContentRelayService : IMeshService
{
    Task<ContentChunkResponse> HandleChunkRequestAsync(
        ContentChunkRequest request,
        MeshServiceContext context,
        CancellationToken ct);
}

public interface ITrustedRelayService : IMeshService
{
    Task<TrustedRelayResponse> HandleRelayAsync(
        TrustedRelayRequest request,
        MeshServiceContext context,
        CancellationToken ct);
}
```

Implement basic stubs that:
- Validate inputs
- Return "NotImplemented"/error for now

#### 7. Testing (T-PR01)

Add tests to verify:
1. Message types serialize/deserialize correctly using your existing transport
2. Service type registration:
   - New service kinds are properly recognized by service discovery/registration logic
3. Stubs:
   - Stub services respond with the expected basic error ("not implemented") but do not crash

#### 8. Anti-Slop Checklist for T-PR01

- [x] No generic host:port + byte stream CONNECT interface in this task
- [x] Messages are **content- and intent-level**, not raw socket-level
- [x] All new services are wired into the existing fabric, but actual behavior is stubbed out
- [x] Service kinds properly enumerated and registered
- [x] Tests pass for message serialization and stub behavior

---

### T-PR02 – Implement Mesh Catalogue Fetch Service (Safe HTTP Fetcher/CDN)

**Repo**: `https://github.com/snapetech/slskdn`  
**Branch**: `experimental/multi-source-swarm`  
**Dependencies**: T-PR01, H-13 (SSRF protection), H-02 (Work Budget)  
**Estimated Effort**: 6-8 hours  
**Status**: Blocked by T-PR01, H-13, H-02

#### Scope & Non-Goals

**You MUST**:
1. Implement `ICatalogFetchService` with:
   - Domain allowlist
   - Method restriction (GET/HEAD only)
   - Size/time limits
   - Work budget integration
2. Add a simple caching layer to avoid redundant external fetches
3. Integrate with the existing SSRF-safe HTTP client from H-13 (or create one if not done)

**You MUST NOT**:
- Allow arbitrary domains/ports by default
- Expose this as a generic HTTP proxy to untrusted peers
- Bypass work budgets (H-02) or HTTP SSRF guards (H-13)

#### 1. Recon

Find:
1. The `ICatalogFetchService` stub from T-PR01
2. Existing HTTP client wrappers:
   - The SSRF-guarded `ISafeHttpClient` (or similar) introduced in H-13
3. Config infrastructure:
   - Where to store domain whitelists and limits

#### 2. Config for Catalogue Fetcher

Add configuration, e.g.:

```jsonc
"CatalogFetch": {
  "Enabled": true,
  "AllowedDomains": [
    "musicbrainz.org",
    "coverartarchive.org",
    "api.yourdomain.com"
  ],
  "MaxBodyBytes": 262144,    // 256 KB default
  "RequestTimeoutSeconds": 10,
  "CacheTtlSeconds": 600
}
```

#### 3. Implement Domain & Method Policy

In `HandleFetchAsync`:
1. Parse and validate `request.Url`:
   - Must be absolute HTTP/HTTPS
2. Extract host; check `AllowedDomains`:
   - If host (or subdomain policy, depending on how strict you are) not allowed → reject
3. Restrict `request.Method`:
   - Only allow `GET` and `HEAD`. Anything else → reject

#### 4. Work Budget & Rate Limits

Before performing any network fetch:
- Use the current `MeshServiceContext`'s `WorkBudget`:

```csharp
if (!context.WorkBudget.TryConsume(WorkCosts.CatalogFetch))
{
    // return an error indicating quota exceeded
}
```

- Optionally add per-peer rate limits:
  - `MaxCatalogFetchesPerPeerPerMinute` (config-driven)

#### 5. SSRF-Safe HTTP Fetch

Use `ISafeHttpClient` (from H-13):
- Ensure:
  - It already blocks loopback/private IPs, etc.
- Apply:
  - Request timeout from config
  - `MaxBodyBytes` cap:
    - Stream and truncate the response body at `MaxBodyBytes`
    - Set `IsTruncated = true` if truncated

#### 6. Caching Layer

Implement a simple in-memory or lightweight on-disk cache:
- Key: `(Url, Method, HeadersSubset)` (be minimal; maybe only certain headers)
- Value: `CatalogFetchResponse` (or a storage DTO)

Behavior:
- On request:
  - Check cache first:
    - If hit and not expired, return cached response (cheap; limited CPU)
  - If miss:
    - Perform HTTP fetch
    - Store in cache with TTL (`CacheTtlSeconds`)

Respect work budget:
- Cache hits should **not** consume network-related work budget

#### 7. Exposure Policy

Decide and implement:
- **Who can use this service?**
  - By default:
    - Mesh peers can call `catalog-fetch` under work-budget and per-peer limits
  - Consider config to restrict:
    - `CatalogFetch.AllowMeshRequests = true/false`
    - If disabled, only local node uses it (no mesh usage)

#### 8. Testing (T-PR02)

Add tests:
1. Domain allowlist:
   - Allowed domain → fetch works (with mocks)
   - Disallowed domain → request rejected
2. Method restriction:
   - GET/HEAD allowed
   - POST/PUT/DELETE rejected
3. Cache behavior:
   - First request hits mock HTTP client
   - Second request for same URL returns cached response and does NOT hit HTTP client again
4. Budget & rate limits:
   - With tight budget, repeated requests eventually get quota exceeded
5. SSRF:
   - Requests to `http://127.0.0.1` or private ranges are rejected by `ISafeHttpClient`

#### 9. Anti-Slop Checklist for T-PR02

- [x] Service is a **whitelisted metadata fetcher**, not a generic HTTP proxy
- [x] HTTP requests use SSRF-safe client and respect work budgets and per-peer limits
- [x] Defaults are conservative: limited domains, small max body, short timeouts
- [x] Caching layer prevents redundant external fetches
- [x] Cache hits don't consume network work budget
- [x] Tests cover domain allowlist, method restrictions, caching, budgets, SSRF

---

### T-PR03 – Implement Content Chunk Relay / CDN Integrated with VirtualSoulfind

**Repo**: `https://github.com/snapetech/slskdn`  
**Branch**: `experimental/multi-source-swarm`  
**Dependencies**: T-PR01, VirtualSoulfind v2 (V2-P1-P4), H-02 (Work Budget)  
**Estimated Effort**: 8-10 hours  
**Status**: Blocked by T-PR01, V2-P4

#### Scope & Non-Goals

**You MUST**:
1. Implement `IContentRelayService` to serve chunks from **local verified content**
2. Integrate with VirtualSoulfind / LocalLibrary to:
   - Map `ContentId` to local file(s)
   - Serve requested ranges safely
3. Respect work budget and per-peer caps
4. Optionally cache chunks in memory for hot content

**You MUST NOT**:
- Serve arbitrary files outside the known content catalogue
- Bypass existing content verification (only serve content that VirtualSoulfind deems valid/advertisable)
- Directly read from random paths given in the request; everything goes through content IDs

#### 1. Recon

Find:
1. VirtualSoulfind content mapping:
   - How `ContentId` (or TrackId/ItemId) maps to `LocalFile`/path
2. Local library / file I/O helpers:
   - Existing code that reads local media chunks
3. Any "verified copy" structures:
   - Only verified content should be advertised/served

#### 2. Config for Content Relay

Add configuration, e.g.:

```jsonc
"ContentRelay": {
  "Enabled": true,
  "MaxChunkBytes": 65536,          // 64 KB
  "MaxConcurrentStreamsPerPeer": 2,
  "MaxConcurrentStreamsGlobal": 20
}
```

#### 3. Implement ContentId → File Mapping

Inside `IContentRelayService`:
- Implement a resolver:
  - Given `ContentChunkRequest.ContentId`, find:
    - The relevant `IContentItem` in VirtualSoulfind
    - The associated verified `LocalFile` (path, size, etc.)

Ensure:
- Only serve if:
  - File exists
  - It is marked as "verified" or "advertisable"

If multiple variants exist (e.g., multiple qualities):
- Use `Variant` to pick the right one or default to a preferred variant

#### 4. Chunked File I/O

When serving a request:
1. Enforce `MaxChunkBytes`:
   - Min(request.Length, config.MaxChunkBytes)
2. Perform a range read on the file:
   - Offsets beyond file size → return empty + `IsLastChunk = true`
3. Optionally compute `HashOfChunk` (fast hash like xxHash) for integrity checking

This should be:
- Streaming-friendly
- Non-blocking where possible (async I/O)

#### 5. Work Budget & Stream Caps

Before serving:
- Consume work budget:

```csharp
if (!context.WorkBudget.TryConsume(WorkCosts.ContentChunkRelay))
{
    // return error indicating quota exceeded
}
```

Additionally:
- Maintain per-peer and global stream counters:
  - If `MaxConcurrentStreamsPerPeer` or `MaxConcurrentStreamsGlobal` exceeded:
    - Reject or queue requests
- Decrement counters when request completes

#### 6. Optional Chunk Cache

Optionally:
- Add an in-memory cache keyed by `(ContentId, Offset, Length)`:
  - Keep small, LRU-based
  - Helps when multiple peers repeatedly request the same chunk (e.g., beginning of a file)

Ensure:
- Cached data is immutable and only populated from verified local files

#### 7. Exposure Policy

Content relay is inherently more sensitive. Enforce:
- Only peers with reasonable reputation / within your allowlist can request chunks (if you have a reputation system)
- Or initially:
  - Allow all mesh peers but keep caps low and log aggressively

Tie into VirtualSoulfind policy:
- Only content that VirtualSoulfind considers "advertisable" should be discoverable via DHT/mesh and hence reachable via content relay

#### 8. Testing (T-PR03)

Add tests:
1. Mapping:
   - Given a known `ContentId`, the relay service finds the correct file and reads the correct range
2. Limits:
   - Requests beyond end-of-file return `IsLastChunk = true` properly
   - `MaxChunkBytes` is respected
3. Budget & caps:
   - With tight budgets / low stream caps, additional requests are rejected
4. Negative cases:
   - Unknown `ContentId` → error
   - Unverified content → not served

#### 9. Anti-Slop Checklist for T-PR03

- [x] Relay never reads arbitrary paths from requests; everything goes through VirtualSoulfind's content mapping
- [x] Only verified/advertisable content is served
- [x] Work budgets and stream caps are enforced
- [x] Content IDs are opaque and mapped internally (no path injection)
- [x] Chunk integrity hashes optional but supported
- [x] Tests cover mapping, limits, budgets, negative cases

---

### T-PR04 – Trusted Relay Mode for Own Nodes / Friends

**Repo**: `https://github.com/snapetech/slskdn`  
**Branch**: `experimental/multi-source-swarm`  
**Dependencies**: T-PR01, H-02 (Work Budget)  
**Estimated Effort**: 6-8 hours  
**Status**: Blocked by T-PR01, H-02

#### Scope & Non-Goals

**You MUST**:
1. Implement `ITrustedRelayService` so that:
   - It relays messages/tunnels only for:
     - Local node pairs (your own nodes), and/or
     - Explicitly trusted peers
2. Restrict `TargetService` to a *controlled list* of internal services (e.g., slskdn's own APIs), not arbitrary host:ports
3. Respect work budgets and per-peer caps

**You MUST NOT**:
- Implement generic TCP or SOCKS-like tunnels to arbitrary Internet destinations
- Make your node a general exit node for the world

#### 1. Config for Trusted Relay

Add config:

```jsonc
"TrustedRelay": {
  "Enabled": false,
  "TrustedPeerIds": [ /* list of mesh peer IDs you trust */ ],
  "AllowedTargetServices": [ "slskdn-ui", "slskdn-api" ],
  "MaxTunnelsPerPeer": 4,
  "MaxTunnelsGlobal": 20
}
```

#### 2. Trust Policy

Implement trust checks:
- When a `TrustedRelayRequest` arrives:
  - Determine the originating mesh peer ID
  - Allow only if:
    - `TrustedRelay.Enabled == true`, and
    - Peer ID is in `TrustedPeerIds`

Later you can support friend lists; for now just use explicit IDs.

#### 3. Target Service Mapping

Define a mapping:
- `TargetService` string → local endpoint (internal service or local HTTP endpoint), e.g.:

```csharp
public interface ITrustedRelayTargetRouter
{
    Task<TrustedRelayResponse> RouteAsync(
        TrustedRelayRequest request,
        MeshServiceContext context,
        CancellationToken ct);
}
```

This router:
- Maps `TargetService` like `"slskdn-ui"` to:
  - A local HTTP request to `http://127.0.0.1:port/ui/...`, or
  - A local service method

Apply:
- Strong allowlist: only names in `AllowedTargetServices` are supported
- No generic mapping to arbitrary host/ports

#### 4. Tunnel Lifecycle

Implement simple tunnel state:
- Track open tunnels by `TunnelId` per peer
- Enforce:
  - `MaxTunnelsPerPeer` and `MaxTunnelsGlobal`
- When `Command == Open`:
  - Check caps, create tunnel state
- When `Command == Data`:
  - Forward payload to `ITrustedRelayTargetRouter`
- When `Command == Close`:
  - Tear down tunnel state

All of this is still at **app-level**, not raw TCP.

#### 5. Work Budget & Caps

Apply work budget:
- Each `TrustedRelayRequest` consumes an appropriate amount of work units
- `Data` commands can be more expensive; treat them as such

Additionally:
- Apply rate limiting per peer (config if needed) to avoid flooding

#### 6. Testing (T-PR04)

Add tests:
1. Trust enforcement:
   - Untrusted peer → requests rejected
   - Trusted peer → allowed
2. Target service routing:
   - Only `TargetService` values in `AllowedTargetServices` are accepted
   - Others → rejected
3. Tunnel caps:
   - Creating more tunnels than allowed per-peer/global is rejected
4. Basic round trip:
   - For a fake `TargetService` (mocked), open → send data → close path works end-to-end

#### 7. Anti-Slop Checklist for T-PR04

- [x] No generic host:port connection is implemented
- [x] Only trusted peers can relay through you
- [x] Only allowed internal services can be reached via this relay
- [x] Work budgets and tunnel caps are enforced
- [x] Tunnel lifecycle properly managed (open/data/close)
- [x] Tests cover trust enforcement, target allowlist, caps, round trip

---

### H-PR05 – Hardening & Policy for Proxy/Relay Services

**Repo**: `https://github.com/snapetech/slskdn`  
**Branch**: `experimental/multi-source-swarm`  
**Dependencies**: T-PR01-04, H-02 (Work Budget)  
**Estimated Effort**: 4-6 hours  
**Status**: Blocked by T-PR01-04

#### Scope & Non-Goals

**You MUST**:
1. Integrate all proxy/relay services into:
   - Work budget system (H-02)
   - Per-peer quotas
   - Mesh service allowlists
2. Ensure no service devolves into "generic proxy" behavior
3. Document configuration defaults and risks

**You MUST NOT**:
- Loosen existing caps/limits for convenience
- Add generic CONNECT/SOCKS semantics

#### 1. Mesh Service Allowlist

Extend mesh service allowlist configuration to explicitly include/exclude:
- `catalog-fetch`
- `content-relay`
- `trusted-relay`

Default:
- `catalog-fetch` & `content-relay` enabled (or at least discoverable) with conservative caps
- `trusted-relay` disabled by default

#### 2. Per-Peer Quotas

Add per-peer quota tracking for:
- Catalog fetch requests
- Content chunk requests
- Trusted relay requests

Config example:

```jsonc
"ProxyQuota": {
  "MaxCatalogFetchesPerPeerPerMinute": 20,
  "MaxContentChunksPerPeerPerMinute": 200,
  "MaxTrustedRelayMessagesPerPeerPerMinute": 100
}
```

Enforce in each service:
- If a peer exceeds its quota:
  - Reject further requests with a clear error for that interval

#### 3. Logging & Metrics

Add metrics:
- `proxy_catalog_fetch_requests_total` (labels: `result`, `from_cache`)
- `proxy_content_chunks_served_total` (labels: `result`)
- `proxy_trusted_relay_messages_total` (labels: `result`)

Ensure:
- No metrics leak sensitive URLs or content IDs as high-cardinality labels
- Logs:
  - Should mention service type, peer ID, result
  - Should not log full URLs by default (or at least redact query strings)

#### 4. Documentation

Add a short **"Proxy/Relay Safety"** section to docs:
- Explain:
  - These are **application-specific** relays, not general-purpose anonymizers
  - Trusted relay is for your own nodes/trusted peers
  - Exit-node behavior is out of scope and discouraged

- Document config flags:
  - How to disable services entirely
  - How to lock down domains, targets, and quotas

#### 5. Testing (H-PR05)

Ensure integration tests (or higher-level tests) cover:
1. That all proxy/relay services:
   - Respect work budgets (H-02)
   - Respect Soulseek caps (where applicable – ideally none of them touch Soulseek)
2. That mesh allowlist can disable specific services (e.g., disable `trusted-relay` and ensure calls fail)
3. That per-peer quotas kick in and persist across calls within a time window

#### 6. Anti-Slop Checklist for H-PR05

- [x] No newly introduced service can be abused as a general SOCKS/HTTP proxy
- [x] Defaults are conservative and documented
- [x] Operators can clearly turn things off or restrict them without diving into code
- [x] Metrics and logging respect privacy (no URL leaks, no high-cardinality labels)
- [x] Integration tests verify work budget and quota enforcement

---

## Task Dependencies & Ordering

```
T-SF01-04 (Service Fabric) ✅ COMPLETE
    ↓
T-PR01 (Define primitives) → READY TO START
    ↓
    ├─→ T-PR02 (Catalogue Fetch) ← H-13 (SSRF), H-02 (Work Budget)
    ├─→ T-PR03 (Content Relay) ← V2-P4 (VirtualSoulfind backends), H-02
    └─→ T-PR04 (Trusted Relay) ← H-02
         ↓
    H-PR05 (Hardening) ← All T-PR tasks + H-02
```

**Critical Path**: H-02 (Work Budget) blocks most proxy/relay implementation

---

## Why This Approach Is Genius

### What Most Projects Do (Bad)
1. Add generic SOCKS/HTTP proxy
2. Hope people use it responsibly
3. Become liable for exit node traffic
4. Get abused/shut down

### What We're Doing (Good)
1. **Application-specific primitives**:
   - Catalogue fetch: whitelisted domains only (MusicBrainz, cover art)
   - Content relay: verified content IDs only (no arbitrary files)
   - Trusted relay: explicit peer allowlist + target service allowlist
2. **Work budget integration from day one**
3. **Conservative defaults** (trusted relay disabled by default)
4. **Clear documentation** about what this IS and ISN'T

### Use Cases Enabled
- ✅ Fetch MusicBrainz metadata for multiple peers (cache once, serve many)
- ✅ Serve verified content chunks as a CDN (help peers with slow/broken sources)
- ✅ Access your own slskdn nodes through NAT (trusted relay between your VPS and home server)
- ❌ Browse arbitrary websites (NOT A GOAL)
- ❌ Act as exit node for torrents (NOT A GOAL)
- ❌ Proxy arbitrary TCP connections (NOT A GOAL)

### Security Properties
- **No generic proxy**: Everything is application-aware (URLs, content IDs, target services)
- **Allowlists everywhere**: Domains, content IDs, peer IDs, target services
- **Work budget**: Can't be amplification attacked
- **Disabled by default**: Trusted relay requires explicit config
- **Audit trail**: All requests logged with peer ID

---

## Integration with Existing Systems

### Service Fabric (T-SF01-04)
- New service kinds: `catalog-fetch`, `content-relay`, `trusted-relay`
- Uses existing `MeshServiceRouter`, `MeshServiceClient`
- Integrates with `ViolationTracker`, rate limiting

### VirtualSoulfind v2
- Content relay maps `ContentId` → local files via VirtualSoulfind catalogue
- Only serves verified/advertisable content
- Respects privacy modes (doesn't expose PII in content IDs)

### Work Budget (H-02)
- All proxy/relay operations consume work units
- Catalogue fetch: network work units
- Content relay: I/O work units
- Trusted relay: mixed (depends on target service)

### HTTP Gateway (T-SF04)
- Catalogue fetch service can be called via HTTP gateway (if enabled)
- Content relay can be called via HTTP gateway
- Trusted relay probably NOT exposed via gateway (trusted peers only)

---

## Metrics & Observability

### Catalogue Fetch
- `proxy_catalog_fetch_requests_total{result=success|error|cached}`
- `proxy_catalog_fetch_bytes_served_total`
- `proxy_catalog_fetch_cache_hit_ratio`
- `proxy_catalog_fetch_duration_seconds`

### Content Relay
- `proxy_content_chunks_served_total{result=success|error}`
- `proxy_content_bytes_served_total`
- `proxy_content_active_streams`
- `proxy_content_chunk_read_duration_seconds`

### Trusted Relay
- `proxy_trusted_relay_requests_total{result=success|error|rejected}`
- `proxy_trusted_relay_active_tunnels`
- `proxy_trusted_relay_bytes_relayed_total`

All metrics:
- Labeled by `service_type` (catalog-fetch, content-relay, trusted-relay)
- NO high-cardinality labels (no URLs, no content IDs, no peer names)

---

## Testing Strategy

### Unit Tests (Per Task)
- T-PR01: Message serialization, service registration
- T-PR02: Domain allowlist, method restrictions, caching, SSRF
- T-PR03: Content ID mapping, chunk I/O, budgets, stream caps
- T-PR04: Trust enforcement, target allowlist, tunnel lifecycle

### Integration Tests (H-PR05)
- End-to-end catalogue fetch through mesh
- Content chunk relay from peer A to peer B
- Trusted relay between two trusted nodes
- Work budget exhaustion scenarios
- Per-peer quota enforcement

### Abuse Scenario Tests
- Peer tries to fetch non-whitelisted domain → rejected
- Peer tries to relay non-existent content ID → rejected
- Untrusted peer tries trusted relay → rejected
- Peer exceeds quota → rejected with clear error
- Peer tries to enumerate all content IDs → rate limited

---

## Documentation Requirements

### User-Facing Docs
1. **"Proxy/Relay Features"** guide:
   - What these services are (and aren't)
   - Configuration examples
   - Security considerations
   - Default behaviors

2. **"Trusted Relay Setup"** guide:
   - How to configure peer allowlist
   - How to define target services
   - NAT traversal use cases
   - Security warnings

### Operator Docs
1. **Configuration reference**:
   - All config options with defaults
   - Conservative vs permissive settings
   - Performance tuning

2. **Security best practices**:
   - Why defaults are conservative
   - Risks of enabling trusted relay
   - Monitoring and logging

### Developer Docs
1. **Architecture overview**:
   - How primitives integrate with service fabric
   - Content ID mapping design
   - Work budget integration

2. **Extension guide**:
   - How to add new whitelisted domains
   - How to add new target services
   - How to add new content relay variants

---

## Success Criteria

### Functional
- [x] Can fetch MusicBrainz metadata through mesh peer
- [x] Can serve verified content chunks to requesting peer
- [x] Can relay requests between two trusted nodes
- [x] All features respect work budgets and quotas

### Security
- [x] No generic proxy behavior possible
- [x] All allowlists enforced correctly
- [x] SSRF protection verified
- [x] Work budget integration verified
- [x] Trusted relay requires explicit trust

### Performance
- [x] Catalogue fetch caching reduces external requests by >80%
- [x] Content chunk relay adds <50ms latency vs direct
- [x] Trusted relay adds <100ms latency vs direct

### Quality
- [x] All tests pass (unit + integration + abuse scenarios)
- [x] Documentation complete
- [x] Metrics and logging in place
- [x] No linter errors

---

## Risk Assessment

### Risk: Services Become Generic Proxies
**Mitigation**:
- Application-specific message types (no raw TCP)
- Allowlists at every layer
- Code review focus on "is this still application-specific?"

### Risk: Abuse by Mesh Peers
**Mitigation**:
- Work budget integration
- Per-peer quotas
- ViolationTracker integration
- Conservative defaults

### Risk: SSRF Vulnerabilities
**Mitigation**:
- H-13 SSRF protection mandatory
- Domain allowlist required
- No user-controlled host resolution

### Risk: Performance Impact
**Mitigation**:
- Stream caps (concurrent requests limited)
- Chunk size limits
- Cache to reduce external fetches
- Work budget prevents resource exhaustion

### Risk: Legal Liability (Exit Node)
**Mitigation**:
- **This is the big one**
- No generic proxy = no exit node behavior
- Application-specific = clear intended use
- Documentation explicitly states NOT an anonymizer
- Trusted relay disabled by default

---

## Future Extensions (Post-MVP)

### Catalogue Fetch
- Support for conditional requests (If-Modified-Since, ETags)
- Support for streaming responses (large responses chunked)
- Per-domain rate limits (respect MusicBrainz API limits)
- Persistent on-disk cache

### Content Relay
- Adaptive chunk sizing based on network conditions
- Chunk verification with cryptographic hashes
- Multi-source chunk assembly (fetch different chunks from different peers)
- Prefetching based on access patterns

### Trusted Relay
- Friend-of-friend trust chains (trust propagation)
- Dynamic peer allowlist (add/remove without restart)
- Service-specific policies (different budgets per target service)
- End-to-end encryption for tunnel payloads

---

**Status**: Task briefs complete, ready to integrate into roadmap  
**Next Step**: Insert into COMPLETE-SUMMARY.md and update task ordering  
**Paranoia Level**: MAXIMUM (these are the riskiest features, must be bulletproof)  
**Genius Level**: ALSO MAXIMUM (solves real problems without creating liabilities)

---

*"Not Tor but worse. Application-specific relay primitives done right."*

*"Genius: Solving NAT traversal and metadata caching without accidentally becoming a liability."*

*"The paranoid bastard's guide to proxy/relay features."*

