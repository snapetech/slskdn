# Features

**slskdn** - Soulseek client with mesh networking, multi-domain content acquisition, and service fabric

> **Note**: This is a fork of [slskd](https://github.com/slskd/slskd) with mesh networking and advanced features. See [README.md](../README.md#acknowledgments) for full attribution.

---

## Core Features (Existing)

### Soulseek Client
- Full Soulseek protocol support
- Search, browse, download, upload
- Chat rooms and private messages
- Shares management and indexing

### Mesh Overlay Network
- DHT-based peer discovery
- QUIC/UDP with TLS encryption
- Certificate pinning for security
- Rendezvous and relay capabilities

### Multi-Source Downloads
- Chunked downloads from multiple sources
- Rescue mode for failed downloads
- Source fallback and retry logic

### Security & Privacy
- Network guard and reputation system
- Violation tracking and auto-bans
- Content safety filtering
- Ban list integration

---

## Service Fabric (T-SF01-07, H-01)

### What It Enables
Generic service discovery and RPC over the mesh overlay network. Any feature can be exposed as a service without custom protocols.

### Features

#### Service Discovery
- **DHT-Based Publishing**: Services publish signed descriptors to DHT
- **Signature Validation**: Ed25519 signatures prevent spoofing
- **TTL-Based Expiry**: Services auto-expire if not refreshed
- **Reputation Filtering**: Discovery respects peer reputation

#### Service Routing
- **Generic RPC**: Call any service method with typed request/response
- **Correlation IDs**: Track requests across network boundaries
- **Timeout Handling**: Automatic timeouts with cancellation
- **Rate Limiting**: Per-peer and per-service call limits (100 calls/min default)

#### Security & Abuse Prevention
- **Work Budget Integration**: Every call consumes work units
- **ViolationTracker Integration**: Abuse feeds into existing security system
- **Payload Size Limits**: Configurable max payload size (1MB default)
- **Service Allowlists**: Control which services are exposed

#### HTTP Gateway (H-01)
- **Localhost API**: External tools access mesh services via HTTP
- **API Key Authentication**: Required for non-localhost access
- **CSRF Protection**: Required for localhost mutating operations
- **Service Allowlist**: Control which services are HTTP-accessible
- **Request Logging**: Audit trail for all gateway requests

**Default**: Disabled (opt-in)

---

## Multi-Domain VirtualSoulfind (T-VC01-04, H-11-15)

### What It Enables
Content-aware acquisition that's not limited to music. Supports multiple content types with appropriate matching logic and backend selection for each.

### Features

#### Multi-Domain Support
- **ContentDomain Enum**: Music, GenericFile, (future: Movies, TV, Books)
- **Domain-Specific Matching**:
  - Music: MBID, duration, bitrate, codec matching
  - GenericFile: SHA256 hash matching
  - Future domains: custom matching logic per domain
- **Domain-Specific Backends**:
  - Music: Soulseek, Mesh/DHT, Torrents, Local
  - GenericFile: Mesh/DHT, Torrents, HTTP, Local (NO Soulseek)

#### Soulseek Safety
- **Compile-Time Gating**: Soulseek backend only accepts `ContentDomain.Music`
- **Rate Caps**: Configurable searches/browses per minute
- **Friendly Mode**: Respects Soulseek etiquette by default
- **Work Budget**: All Soulseek operations consume budget

**Result**: Soulseek abuse for non-music content is prevented by multiple enforcement layers (domain gating, work budget, rate caps, plan validation).

#### Intent Management
- **Origin Tracking**: Tag intents by source (UserLocal, LocalAutomation, RemoteMesh, RemoteGateway)
- **Priority Scheduling**: User intents prioritized over remote requests
- **Remote Intent Control**: Disabled by default, requires explicit config
- **Work Budget Per Intent**: Budget exhaustion fails gracefully

#### Privacy Modes
- **Normal Mode**: Per-peer tracking for smart source selection
- **Reduced Mode**: Aggregated sources only, less correlation
- **Opaque IDs**: VirtualSoulfind never stores Soulseek usernames or IPs
- **PII Separation**: Soulseek-specific data lives in Soulseek modules only

#### Verification & Advertisement
- **Content Verification**: Duration/size/hash matching before marking as "verified"
- **Selective Advertisement**: Only verified, shared, non-private content advertised to mesh
- **No Third-Party Data**: Never publishes what other Soulseek users share

**Default**: Music domain enabled, remote intent management disabled

---

## Proxy/Relay Services (T-PR01-05, H-PR05)

### What They Enable
Application-specific fetch and relay capabilities without becoming a generic proxy or exit node.

### Features

#### Catalogue Fetch Service
**Purpose**: Fetch and cache metadata from whitelisted external APIs.

- **Domain Allowlist**: Only whitelisted domains (MusicBrainz, cover art, etc.)
- **Method Restriction**: GET and HEAD only (no POST/PUT/DELETE)
- **SSRF Protection**: Blocks loopback and private IP ranges
- **Response Caching**: In-memory cache with configurable TTL (10 min default)
- **Size Limits**: Max response body size (256 KB default)
- **Request Timeout**: Configurable timeout (10 seconds default)
- **Work Budget**: Consumes units per fetch (cache hits are free)

**Use Case**: Multiple peers fetch same MusicBrainz metadata → one external API call, cached and shared via mesh.

**Default**: Enabled with conservative limits

#### Content Relay Service
**Purpose**: Serve verified content chunks to mesh peers (mesh CDN).

- **Content ID Mapping**: Accept content IDs, not file paths (no arbitrary file access)
- **Verification Required**: Only serves content marked as verified and advertisable
- **Chunked Delivery**: Configurable chunk size (64 KB default)
- **Stream Caps**: Per-peer and global concurrent stream limits
- **Integrity Hashes**: Optional chunk hashing for verification
- **Work Budget**: Consumes units per chunk served

**Use Case**: Peer has slow Soulseek source, you have verified copy → serve chunks via fast mesh connection.

**Default**: Enabled with conservative stream caps

#### Trusted Relay Service
**Purpose**: NAT traversal for your own nodes/friends via logical service names.

- **Peer Allowlist**: Explicit list of trusted peer IDs
- **Target Service Allowlist**: Explicit list of allowed internal services ("slskdn-ui", "slskdn-api")
- **Logical Routing**: Routes by service name, not host:port (no generic proxy)
- **Tunnel Management**: Track tunnels per peer, enforce caps
- **Work Budget**: Consumes units per relay message

**Use Case**: VPS and home server (behind NAT) relay through each other for remote management.

**Default**: Disabled (requires explicit trust configuration)

---

## Security & Hardening

### Global Principles
- **Default Deny**: Features disabled or scoped by default
- **Least Privilege**: Minimal permissions and visibility per service
- **No Generic Proxy**: Everything application-aware (domains, content IDs, service names)
- **Work Budget**: Universal work unit system prevents amplification
- **Input Validation**: All external inputs validated (length, type, format)

### Logging & Metrics
- **No PII**: Logs/metrics never contain Soulseek usernames, IPs, secrets
- **Low Cardinality**: Metrics use low-cardinality labels only
- **Audit Trail**: All mutating operations logged with origin

### SSRF Protection
- **Safe HTTP Client**: All outbound HTTP goes through SSRF-safe client
- **IP Blocking**: Loopback and private IP ranges blocked
- **Domain Allowlists**: Only whitelisted domains accessible

### Rate Limiting
- **Per-Peer Limits**: Calls per minute per peer (configurable)
- **Global Limits**: Total calls across all peers (configurable)
- **Service-Specific Limits**: Different limits for different services

### Violation Tracking
- **Auto-Escalating Bans**: Repeated violations trigger temporary, then permanent bans
- **Unified System**: Mesh violations affect Soulseek reputation
- **Detailed Logging**: All violations logged with type and details

---

## Configuration

### Service Fabric
```yaml
ServiceFabric:
  Enabled: true
  MaxDescriptorsPerLookup: 20
  MaxDescriptorBytes: 4096
```

### Mesh Gateway
```yaml
MeshGateway:
  Enabled: false  # Disabled by default
  BindAddress: "127.0.0.1"  # Localhost only
  Port: 5030
  ApiKey: ""  # MUST be set if enabling
  CsrfToken: ""  # Auto-generated if empty
  AllowedServices:
    - "pod-chat"
    - "shadow-index"
    - "mesh-stats"
  MaxRequestBodyBytes: 1048576  # 1 MB
  RequestTimeoutSeconds: 30
```

### VirtualSoulfind v2
```yaml
VirtualSoulfind:
  Enabled: true
  PrivacyMode: Normal  # Normal | Reduced
  AllowRemoteIntentManagement: false  # Remote intent creation
  AllowPlanExecution: false  # HTTP gateway plan execution
  SoulseekCaps:
    MaxSearchesPerMinute: 20
    MaxBrowsesPerMinute: 5
```

### Catalogue Fetch
```yaml
CatalogFetch:
  Enabled: true
  AllowedDomains:
    - "musicbrainz.org"
    - "coverartarchive.org"
  MaxBodyBytes: 262144  # 256 KB
  RequestTimeoutSeconds: 10
  CacheTtlSeconds: 600  # 10 minutes
```

### Content Relay
```yaml
ContentRelay:
  Enabled: true
  MaxChunkBytes: 65536  # 64 KB
  MaxConcurrentStreamsPerPeer: 2
  MaxConcurrentStreamsGlobal: 20
```

### Trusted Relay
```yaml
TrustedRelay:
  Enabled: false  # MUST be explicitly enabled
  TrustedPeerIds: []  # Explicit peer allowlist
  AllowedTargetServices:
    - "slskdn-ui"
    - "slskdn-api"
  MaxTunnelsPerPeer: 4
  MaxTunnelsGlobal: 20
```

---

## Use Cases

### Music Acquisition (Existing + Enhanced)
- Search Soulseek with etiquette caps
- Multi-source downloads (Soulseek + mesh + torrents)
- MBID-based matching via shadow index
- Verified content served via mesh CDN

### Generic File Sharing (New)
- Hash-based matching (SHA256)
- Mesh/DHT/torrent backends only (NO Soulseek)
- Content verification before advertisement
- Mesh CDN for verified files

### Metadata Collaboration (New)
- Fetch MusicBrainz via catalogue fetch service
- Cache and share via mesh (reduce API load)
- Multiple peers benefit from single fetch
- Respects external API rate limits

### Personal Infrastructure (New)
- NAT traversal via trusted relay
- VPS and home server communicate via mesh
- Access remote slskdn instances via HTTP gateway
- No port forwarding or VPN required

### Identity & Friends (New)

Human-friendly peer addressing and discovery system enabling the "befriend → group → share → recipient backfill download → recipient stream" workflow.

#### Features

- **Peer Profiles**: Signed identity objects with display names, friend codes, and network endpoints
- **Contact Management**: Local contact list with nicknames and verification status
- **Friend Codes**: Short, shareable codes (e.g., `ABCD-EFGH-IJKL-MNOP`) for easy addressing
- **Invite Links**: `slskdn://invite/...` links with QR code support for adding friends
- **mDNS LAN Discovery**: Automatic peer discovery on local networks
- **API Integration**: Full REST API for profile and contact management

#### Configuration
```yaml
Feature:
  IdentityFriends: true  # Enable identity and friends system
```

**Default**: Disabled (opt-in)

#### Use Cases

- **Human-Friendly Sharing**: Add friends by display name instead of raw peer IDs
- **LAN Discovery**: Automatically discover peers on the same network
- **Invite-Based Networking**: Share invite links or QR codes to connect with friends
- **Contact-Based Groups**: Create share groups using contact nicknames

### Streaming (New)

HTTP range request support for content streaming with session limiting and authentication.

#### Features

- **Range Request Support**: Standard HTTP `Range` header support for seeking
- **Session Limiting**: Configurable concurrent stream limits per content
- **Token Authentication**: Share token-based authentication for recipients
- **Content Resolution**: Automatic MIME type detection and file path resolution

#### Configuration
```yaml
Feature:
  Streaming: true  # Enable streaming API
```

**Default**: Disabled (opt-in)

### External Tool Integration (New)
- Music apps access VirtualSoulfind via HTTP gateway
- Query missing tracks, library stats, catalogue gaps
- Execute intents remotely (if enabled)
- Work budget prevents abuse

---

## Performance

### Caching
- **Catalogue Fetch**: In-memory cache (10 min TTL)
- **Content Relay**: Chunk cache for hot content (LRU)
- **VirtualSoulfind**: Catalogue cache for repeated queries

### Streaming
- **Content Relay**: Chunked delivery, not full-file buffering
- **HTTP Gateway**: Streaming responses where applicable
- **DHT Queries**: Limited result count (max 20 descriptors)

### Concurrency
- **Stream Caps**: Prevent resource exhaustion (per-peer and global)
- **Rate Limits**: Prevent DoS (per-peer and global)
- **Work Budget**: Fail fast on exhaustion (no wasted work)

---

## Roadmap

### Phase 1: Service Fabric ✅ COMPLETE
- [x] Service descriptors and directory (T-SF01)
- [x] Service routing and RPC (T-SF02)
- [x] Service wrappers for existing features (T-SF03)
- [x] HTTP gateway with auth (T-SF04 + H-01)

### Phase 2: Security Hardening 🚧 IN PROGRESS
- [x] Security review and tightening (T-SF05)
- [x] Developer docs and examples (T-SF06)
- [x] Metrics and observability (T-SF07)
- [x] Work budget implementation (H-02) **CRITICAL**
- [x] Soulseek safety caps (H-08) **CRITICAL**

### Phase 3: Multi-Domain Foundation
- [x] Domain abstraction (T-VC01)
- [x] Music domain provider (T-VC02)
- [x] GenericFile domain provider (T-VC03)
- [x] Domain-aware planner + Soulseek gating (T-VC04)

### Phase 4: VirtualSoulfind v2
- [x] Data model and catalogue store (V2-P1)
- [x] Intent queue and planner (V2-P2)
- [x] Match and verification engine (V2-P3)
- [x] Backend implementations (V2-P4)
- [x] Integration and work budget (V2-P5)
- [x] Advanced features (V2-P6)

### Phase 5: Proxy/Relay Services
- [x] Define primitives (T-PR01)
- [x] Catalogue fetch service (T-PR02)
- [x] Content relay service (T-PR03)
- [x] Trusted relay service (T-PR04)
- [x] Hardening and policy (H-PR05)

### Phase 6: Testing & Hardening
- [x] Comprehensive testing (T-TEST-01 through T-TEST-07)
- [x] Remaining hardening tasks (H-03 through H-07, H-09, H-10)
- [x] Security audit and penetration testing
- [x] Performance optimization

---

## Installation

### Requirements
- .NET 8.0 or later
- Linux, macOS, or Windows
- Network connectivity for Soulseek and mesh

### Quick Start
```bash
# Clone repository
git clone https://github.com/snapetech/slskdn.git
cd slskdn

# Checkout experimental branch
git checkout master

# Build
dotnet build

# Run
dotnet run --project src/slskd
```

### Configuration
Configuration files are in `appsettings.json` and `appsettings.Development.json`.

Default config is secure:
- Mesh gateway disabled
- Trusted relay disabled
- Remote intent management disabled
- Conservative rate limits and caps

See [SECURITY-GUIDELINES.md](SECURITY-GUIDELINES.md) for detailed security configuration.

---

## Documentation

- [HOW-IT-WORKS.md](HOW-IT-WORKS.md): Technical architecture and synergies
- [SECURITY-GUIDELINES.md](SECURITY-GUIDELINES.md): Mandatory security requirements (all tasks)
- [CURSOR-WARNINGS.md](CURSOR-WARNINGS.md): LLM implementation risk assessment
- [SERVICE_FABRIC_TASKS.md](SERVICE_FABRIC_TASKS.md): Service fabric task breakdown
- [HARDENING-TASKS.md](archive/root/HARDENING-TASKS.md): Security hardening task breakdown
- [PROXY-RELAY-TASKS.md](archive/root/PROXY-RELAY-TASKS.md): Proxy/relay task breakdown
- [VIRTUALSOULFIND-V2-TASKS.md](archive/root/VIRTUALSOULFIND-V2-TASKS.md): VirtualSoulfind v2 task breakdown
- [VIRTUALSOULFIND-CONTENT-DOMAINS.md](VIRTUALSOULFIND-CONTENT-DOMAINS.md): Multi-domain refactoring
- [TESTING-STRATEGY.md](TESTING-STRATEGY.md): Comprehensive testing strategy

---

## Contributing

### Security First
Read [SECURITY-GUIDELINES.md](SECURITY-GUIDELINES.md) before contributing. All contributions MUST follow the mandatory security requirements.

### Task Implementation
Check [CURSOR-WARNINGS.md](CURSOR-WARNINGS.md) for implementation risk levels before starting any task. High-risk tasks require strict prompts and comprehensive tests.

### Code Review
All PRs require:
- Security checklist completion
- Anti-slop checklist completion
- Comprehensive tests (unit + integration)
- No linter errors

### Philosophy
- Security is non-negotiable
- Default deny everywhere
- No generic proxy features
- Application-aware, not generic
- Composable, not monolithic

---

## License

See LICENSE file for details.

---

## Status

**Current Version**: 0.x.x (experimental)  
**Branch**: master  
**Status**: Active development  
**Production Ready**: No (experimental features)

**Implemented**:
- ✅ Service fabric core
- ✅ HTTP gateway with auth
- ✅ Service wrappers (pods, VirtualSoulfind, introspection)

**In Progress**:
- 🚧 Security review and hardening
- 🚧 Work budget implementation
- 🚧 Soulseek caps implementation

**Planned**:
- 📋 Multi-domain VirtualSoulfind
- 📋 Proxy/relay services
- 📋 Comprehensive testing

---

*slskdn - Soulseek with mesh networking, done right.*

*No hype. Just engineering.*

