# How It All Works Together

**Status**: Technical Explainer - No Hype, Just Engineering  
**Created**: December 11, 2025  
**Purpose**: Show how service fabric, multi-domain, and proxy/relay synergize

> **Note**: This is a fork of [slskd](https://github.com/slskd/slskd) with experimental mesh networking features. See [README.md](README.md#acknowledgments) for full attribution.

---

## Overview

We're building three separate but complementary capabilities on top of existing slskdn infrastructure:

1. **Service Fabric**: Generic service discovery and RPC over mesh
2. **Multi-Domain VirtualSoulfind**: Content-aware acquisition that's not just for music
3. **Proxy/Relay Primitives**: Application-specific fetch/relay without becoming an exit node

Each solves a specific problem. Together, they enable patterns that weren't possible before.

This document explains how they interact and why the design choices matter.

---

## Part 1: Service Fabric (T-SF01-07, H-01)

### What It Is

A **generic service layer** on top of the existing DHT and mesh overlay:
- Services publish signed descriptors to DHT (`svc:service-name`)
- Clients discover and call services via mesh overlay
- RPC-style calls with correlation IDs, timeouts, rate limits
- HTTP gateway for external tools (localhost-only by default)

### What Problem It Solves

**Before**: Every feature required custom DHT keys, custom message types, custom routing logic.

**After**: New features are just new `IMeshService` implementations. Discovery, routing, security, and HTTP exposure are handled by the fabric.

### Key Technical Choices

1. **Signed Descriptors**: Ed25519 signatures prevent spoofing and DHT poisoning
2. **Work Budget Integration**: Every call consumes work units, preventing amplification
3. **ViolationTracker Integration**: Abuse automatically feeds into existing security system
4. **Service-Level Allowlists**: Operators control which services are exposed (mesh and HTTP)

### Why These Choices

- **Signed descriptors**: DHT is untrusted; signatures make poisoning expensive
- **Work budget**: Without it, malicious peer can flood you with requests across all services
- **ViolationTracker**: Mesh abuse should affect Soulseek reputation (unified security model)
- **Allowlists**: "Disabled by default" prevents accidental exposure

---

## Part 2: Multi-Domain VirtualSoulfind (T-VC01-04, H-11-15)

### What It Is

A **content-domain-aware** version of VirtualSoulfind that:
- Abstracts "works" and "items" instead of "releases" and "tracks"
- Supports multiple content types: Music, GenericFile, (future: Movies, TV, Books)
- **Enforces Soulseek-only-for-Music at the type level**
- Tracks intent origin (user local vs remote mesh vs gateway)

### What Problem It Solves

**Before**: VirtualSoulfind was music-only and tightly coupled to MBID matching. Adding new content types would require duplicating the entire planner/resolver/backend stack.

**After**: New content types are just new `IContentDomainProvider` implementations. The planner, resolver, and backend selection logic is domain-aware.

### Key Technical Choices

1. **Domain Gating at Type Level**: `SoulseekBackend` only accepts `ContentDomain.Music`
2. **Opaque Internal IDs**: VirtualSoulfind never stores Soulseek usernames or IPs
3. **Privacy Modes**: Normal (per-peer tracking) vs Reduced (aggregated sources only)
4. **Intent Origin Tagging**: User requests get priority; remote mesh requests are capped

### Why These Choices

- **Domain gating**: Prevents accidental Soulseek abuse for non-music content (compile-time enforcement)
- **Opaque IDs**: Privacy separation; Soulseek-specific data lives in Soulseek modules only
- **Privacy modes**: Reduced mode trades features for less correlation (user choice)
- **Origin tagging**: Remote peers can't dominate your resolver's work queue

---

## Part 3: Proxy/Relay Primitives (T-PR01-05, H-PR05)

### What They Are

Three **application-specific** relay capabilities:

1. **Catalogue Fetch**: Whitelisted HTTP fetcher for metadata (MusicBrainz, cover art)
2. **Content Relay**: Serve verified content chunks by Content ID (mesh CDN)
3. **Trusted Relay**: NAT traversal for your own nodes/friends via logical service names

### What Problems They Solve

**Catalogue Fetch**:
- **Before**: 10 peers each fetch MusicBrainz metadata → 10 API calls, rate limit risk
- **After**: 1 peer fetches and caches → other peers request from mesh cache

**Content Relay**:
- **Before**: Peer has slow/broken source, you have verified copy → they still download from slow source
- **After**: You serve chunks to them via mesh (like a mini-CDN)

**Trusted Relay**:
- **Before**: Your VPS and home server behind NAT can't talk directly
- **After**: They relay through each other's mesh connections using logical service names

### Key Technical Choices

1. **No Generic Proxy**: Everything is application-aware (domains, content IDs, service names)
2. **Domain Allowlists**: Catalogue fetch only contacts whitelisted domains
3. **Content ID Mapping**: Content relay never sees file paths, only opaque content IDs
4. **Peer + Target Allowlists**: Trusted relay requires explicit peer trust AND target service allowlist

### Why These Choices

- **No generic proxy**: Avoids liability and abuse; not trying to be Tor
- **Domain allowlists**: SSRF protection; can't be tricked into fetching internal services
- **Content ID mapping**: Can't be tricked into serving arbitrary files (no path traversal)
- **Dual allowlists**: Trusted relay is for YOUR infrastructure, not arbitrary internet

---

## How They Synergize

### Synergy 1: VirtualSoulfind Over Service Fabric

**What Happens**:
- VirtualSoulfind exposes methods as `IMeshService`:
  - `GetMissingTracks()` - query catalogue gaps
  - `QueryByMbid()` - shadow index lookups
  - `GetLibraryStats()` - introspection

**Why This Matters**:
- Other mesh peers can query your catalogue without custom protocols
- HTTP gateway can expose these methods to external tools (e.g., music apps)
- Work budget and rate limits are automatic (handled by service fabric)

**Concrete Use Case**:
```
Music App (external) → HTTP Gateway (localhost:5030) 
  → Mesh Service Client → Peer's VirtualSoulfind Service
  → Returns missing tracks for a release
```

---

### Synergy 2: Catalogue Fetch + Multi-Domain Matching

**What Happens**:
- `MusicContentDomainProvider` needs MusicBrainz metadata
- Instead of fetching directly, it calls `CatalogFetchService` (over mesh or local)
- Multiple peers benefit from cached metadata

**Why This Matters**:
- Reduces external API calls (respect MusicBrainz rate limits)
- Mesh peers collaborate on metadata without exposing what they're searching for
- Cache is application-aware (knows about MusicBrainz vs other APIs)

**Concrete Use Case**:
```
Peer A: Needs MBID metadata → CatalogFetch (cache miss) → Fetches from MusicBrainz
Peer B: Needs same MBID → CatalogFetch (cache hit) → Gets from Peer A's cache
Result: 1 external API call instead of 2
```

---

### Synergy 3: Content Relay + VirtualSoulfind Verification

**What Happens**:
- VirtualSoulfind marks local files as "verified" (duration, hash, quality checks)
- `ContentRelayService` only serves content that VirtualSoulfind deems verified
- Other peers request chunks by Content ID (not file path)

**Why This Matters**:
- Mesh becomes a **quality-filtered CDN** (only verified content propagates)
- No risk of serving incomplete/corrupted files
- Content ID prevents path traversal attacks

**Concrete Use Case**:
```
Peer A: Has verified FLAC (duration matches, hash correct)
Peer B: Downloads from Soulseek but source is slow (5 KB/s)
Peer B: Discovers Peer A has same content (via VirtualSoulfind mesh query)
Peer B: Requests chunks from Peer A (fast mesh connection)
Result: Multi-source download (Soulseek + mesh CDN)
```

---

### Synergy 4: Trusted Relay + HTTP Gateway

**What Happens**:
- You run slskdn on VPS (public IP) and home server (behind NAT)
- VPS and home server are in each other's `TrustedPeerIds`
- Home server uses trusted relay to access VPS's HTTP gateway
- Now home server can control VPS remotely

**Why This Matters**:
- NAT traversal without port forwarding or VPN
- Logical service names (not host:port) keep it application-specific
- Only your nodes can relay through each other (explicit trust)

**Concrete Use Case**:
```
Home Server: Behind strict NAT, can't receive incoming connections
VPS: Public IP, runs slskdn
Home Server: Connects to VPS mesh, establishes trusted relay tunnel
Home Server: Sends HTTP requests via tunnel to VPS's "slskdn-api" target service
VPS: Routes to localhost HTTP gateway
Result: Home server controls VPS without port forwarding
```

---

### Synergy 5: Multi-Domain + Domain-Specific Backends

**What Happens**:
- `ContentDomain.Music` uses: Soulseek, Mesh/DHT, Torrents, Local
- `ContentDomain.GenericFile` uses: Mesh/DHT, Torrents, HTTP, Local (NO Soulseek)
- Future domains can add their own backends without touching existing code

**Why This Matters**:
- Soulseek abuse is **architecturally impossible** for non-music domains
- Each domain gets appropriate matching logic (MBID for music, hash for files)
- No code duplication; planner/resolver are domain-agnostic

**Concrete Use Case**:
```
Music Intent (MBID-based):
  → MusicDomainProvider
  → Backends: Soulseek (capped), Mesh, BT, Local
  → Matching: duration ±2s, bitrate, codec

GenericFile Intent (hash-based):
  → GenericFileDomainProvider  
  → Backends: Mesh, BT, HTTP, Local (Soulseek excluded at compile time)
  → Matching: SHA256, file size

Result: Right tool for each job, no Soulseek for generic files
```

---

## The Emergent Patterns

### Pattern 1: Mesh-Collaborative Metadata

**Without these features**: Each peer fetches its own metadata from external APIs.

**With these features**: 
1. Peer fetches via `CatalogFetchService` (goes to mesh first)
2. If cache hit, returns immediately (no external call)
3. If cache miss, one peer fetches and shares via mesh
4. Result: N peers make 1 external call instead of N

**Why it works**: Service fabric + catalogue fetch + caching

---

### Pattern 2: Quality-Filtered Content Distribution

**Without these features**: Peers download from whoever claims to have it; no verification.

**With these features**:
1. VirtualSoulfind verifies local content (duration, hash, quality)
2. Only verified content gets advertised via mesh service
3. `ContentRelayService` serves chunks, but only for verified content
4. Result: Mesh becomes a curated CDN of known-good files

**Why it works**: Multi-domain VirtualSoulfind + content relay + verification

---

### Pattern 3: Multi-Source Acquisition with Domain Safety

**Without these features**: Either music-only OR risk Soulseek abuse for other content.

**With these features**:
1. Intent specifies `ContentDomain`
2. Planner selects domain-appropriate backends
3. Soulseek gated to Music domain at type level
4. Generic files use mesh/BT/HTTP/local only
5. Result: Right sources for each content type, no abuse risk

**Why it works**: Multi-domain abstraction + domain gating + work budget

---

### Pattern 4: Decentralized Control Plane

**Without these features**: Each feature needs custom DHT keys, custom protocols.

**With these features**:
1. VirtualSoulfind exposes methods as mesh service
2. HTTP gateway exposes mesh services to localhost
3. External tools call HTTP API
4. Requests route via mesh to appropriate peer
5. Result: Decentralized but accessible control plane

**Why it works**: Service fabric + HTTP gateway + authentication

---

## The Security Model

### Defense in Depth

Every layer has independent security controls:

1. **Service Fabric Layer**:
   - Signed descriptors (prevent spoofing)
   - Per-peer rate limits (100 calls/min default)
   - ViolationTracker integration (abuse logged)
   - Service allowlists (control exposure)

2. **VirtualSoulfind Layer**:
   - Domain gating (Soulseek only for music)
   - Intent origin tagging (prioritize local over remote)
   - Privacy modes (Normal vs Reduced)
   - Verification before advertisement (no bad content propagates)

3. **Proxy/Relay Layer**:
   - Domain allowlists (catalogue fetch)
   - Content ID mapping (content relay)
   - Peer + target allowlists (trusted relay)
   - No generic proxy possible (application-aware only)

4. **Cross-Cutting**:
   - Work budget (every operation consumes units)
   - SSRF protection (all HTTP via safe client)
   - No PII in logs/metrics (privacy by design)
   - Disabled by default (opt-in for risky features)

### Why Layered

If one layer fails (bug, misconfiguration), others still provide protection:

**Example**: Even if domain gating broke:
- Work budget would prevent amplification
- Rate limits would cap damage per peer
- ViolationTracker would catch abuse patterns
- Allowlists would limit which services are exposed

**Result**: No single point of failure for security.

---

## The Performance Model

### Designed for Efficiency

1. **Caching Everywhere**:
   - Catalogue fetch: in-memory cache (10min TTL)
   - Content relay: chunk cache for hot content
   - VirtualSoulfind: catalogue cache for repeated queries

2. **Work Budget Prevents Waste**:
   - Expensive operations consume units
   - Budget exhausted → fail fast (no wasted work)
   - Prevents "death by a thousand queries"

3. **Streaming Where Possible**:
   - Content relay: chunked, not full-file
   - HTTP responses: streamed, not buffered
   - DHT queries: limited result count (max 20 descriptors)

4. **Concurrency Limits**:
   - Max concurrent streams per peer (2 default)
   - Max concurrent streams global (20 default)
   - Prevents resource exhaustion

---

## What This Enables (Concrete Use Cases)

### Use Case 1: Multi-Source Music Acquisition

**Setup**: 
- Peer A has VirtualSoulfind v2 with Music domain
- Release "Abbey Road" has 3 sources: Soulseek, Mesh peer B, Local

**Flow**:
1. User adds "Abbey Road" to desired releases (local intent)
2. VirtualSoulfind queries Soulseek (capped, friendly)
3. VirtualSoulfind queries mesh services (discovers peer B has verified copy)
4. Planner creates multi-source plan: Soulseek + mesh peer B
5. Resolver downloads chunks from both simultaneously
6. Verification: checks duration, hash, MBID match
7. Result: Faster download, Soulseek-friendly (not hammering one source)

**Why it works**: Multi-domain + content relay + Soulseek gating + work budget

---

### Use Case 2: Decentralized Metadata Sharing

**Setup**:
- 5 peers all need MusicBrainz metadata for same release
- Catalogue fetch service enabled on all peers

**Flow**:
1. Peer A needs metadata → calls catalogue fetch service
2. Cache miss → fetches from MusicBrainz API
3. Peer B needs same metadata → calls catalogue fetch service  
4. Service checks mesh peers for cache
5. Finds Peer A has cached copy → returns from cache
6. Peers C, D, E also get cached copy from A or B
7. Result: 1 MusicBrainz API call instead of 5

**Why it works**: Service fabric + catalogue fetch + caching

---

### Use Case 3: NAT Traversal for Personal Infrastructure

**Setup**:
- VPS (public IP) runs slskdn
- Home server (behind strict NAT) runs slskdn
- Both are in each other's trusted peer list

**Flow**:
1. Home server connects to VPS via mesh overlay
2. Establishes persistent mesh connection (outbound from home, NAT allows)
3. Home server opens trusted relay tunnel to VPS
4. Home server sends requests via tunnel (target service: "slskdn-api")
5. VPS routes to localhost HTTP gateway
6. Home server can now control VPS remotely
7. Result: No port forwarding, no VPN, no third-party relay

**Why it works**: Trusted relay + service fabric + HTTP gateway

---

### Use Case 4: Generic File Sharing Without Soulseek

**Setup**:
- User wants to share software ISOs, ebooks, datasets
- These are `ContentDomain.GenericFile` (not music)

**Flow**:
1. User adds files to local library
2. VirtualSoulfind categorizes as GenericFile domain
3. Planner excludes Soulseek backend (domain gating)
4. Uses only: Mesh, BitTorrent, HTTP, Local
5. Matching: hash-based (SHA256)
6. Content relay serves verified chunks via mesh
7. Result: Efficient sharing without Soulseek abuse

**Why it works**: Multi-domain + domain gating + content relay

---

## Why This Approach

### Design Philosophy

1. **Application-Specific, Not Generic**:
   - No generic SOCKS/HTTP proxy
   - Everything operates on application primitives (content IDs, service names, domains)
   - Result: Can't accidentally become liability

2. **Security Baked In, Not Bolted On**:
   - Work budget from day one
   - Domain gating at type level
   - SSRF protection universal
   - Result: Abuse is architecturally difficult

3. **Opt-In for Risk**:
   - Trusted relay disabled by default
   - Remote intent management disabled by default
   - Plan execution via HTTP disabled by default
   - Result: Safe defaults, explicit choices for risk

4. **Composable, Not Monolithic**:
   - Service fabric is generic (any service type)
   - VirtualSoulfind is domain-aware (any content type)
   - Proxy/relay are task-specific (fetch, relay, tunnel)
   - Result: New features don't require core changes

---

## What We're NOT Building

To be clear about scope:

**NOT building**:
- ❌ Generic anonymization network (not Tor)
- ❌ Exit node for arbitrary internet traffic (not a proxy service)
- ❌ Generic file sharing for anything (domain-specific only)
- ❌ Cryptocurrency/blockchain features (out of scope)
- ❌ Social network features (beyond existing pod/chat)

**ARE building**:
- ✅ Application-specific service fabric
- ✅ Content-domain-aware acquisition
- ✅ Whitelisted metadata fetching
- ✅ Verified content CDN over mesh
- ✅ Personal infrastructure NAT traversal

---

## Current Status

**Implemented and Shipped** (T-SF01-04, H-01):
- Service fabric core (descriptors, directory, router, client)
- Service wrappers (pods, VirtualSoulfind, introspection)
- HTTP gateway with auth + CSRF
- 58 tests passing, build green

**Designed and Documented**:
- Multi-domain VirtualSoulfind (T-VC01-04, H-11-15)
- Proxy/relay primitives (T-PR01-05, H-PR05)
- Security guidelines (mandatory for all work)
- LLM implementation warnings (risk assessment per task)

**Next Steps**:
1. Security review of existing fabric (T-SF05)
2. Work budget implementation (H-02)
3. Soulseek caps implementation (H-08)
4. Multi-domain refactoring (T-VC01-04)
5. VirtualSoulfind v2 implementation (V2-P1-P6)
6. Proxy/relay implementation (T-PR01-05)

---

## Summary

We're building three complementary systems that work together:

1. **Service Fabric**: Makes adding new mesh features easy
2. **Multi-Domain VirtualSoulfind**: Extends content acquisition beyond music, safely
3. **Proxy/Relay**: Enables metadata caching, content CDN, and NAT traversal

Each solves specific problems. Together they enable:
- Mesh-collaborative metadata (reduces external API load)
- Quality-filtered content distribution (mesh CDN of verified files)
- Multi-source acquisition with domain safety (right tools for each content type)
- Decentralized control plane (mesh services accessible via HTTP)

Security is layered (defense in depth). Performance is considered (caching, streaming, limits). Scope is intentionally constrained (no generic proxy).

The result is a platform for building decentralized content acquisition features without compromising on security, privacy, or Soulseek etiquette.

---

**Status**: Technical Explainer Complete  
**Last Updated**: December 11, 2025  
**Audience**: Engineers who want to understand the architecture

---

*"No hype. Just engineering. Here's how the pieces fit together and why."*
