# slskdn - The Rich-Featured Soulseek Distribution

> **Note**: This is a fork of [slskd](https://github.com/slskd/slskd) by jpdillingham. We deeply respect the upstream project and maintain the same AGPL-3.0 license. See the [Acknowledgments](#acknowledgments) section below for full attribution.

---

## Positioning

**slskdn** is a **distribution** of slskd—not just a fork, but a complete ecosystem with:
- Batteries-included UX (wishlist, auto-replace, smart ranking, etc.)
- Advanced research subsystems (multi-source, DHT mesh, hash DB, backfill, reputation)
- AI-assisted development governance (ADRs, memory-bank, anti-slop rules)

This is **slskd-plus with bundled opinions**—a legitimate place to land for users who want everything built-in and are okay living on the sharp edge.

## Why Fork?

**slskd** is an excellent headless Soulseek client with a clean API. However, the maintainer has a clear philosophy: keep the core lean and let users implement advanced features via external scripts and the API.

From issue discussions:
- *"This can be done via the API with a script"*
- *"I'll wait until someone asks for it to worry about it"*
- *"External plugins have emerged for this"*

**slskdn** takes the opposite approach: **Batteries Included.**

Not everyone wants to write Python scripts, set up cron jobs, or integrate third-party tools. Some users want a fully-featured client that works out of the box—like the rich desktop clients (Nicotine+, SoulseekQt) but with slskd's modern web interface.

---

## Vision Statement

> **slskdn is a feature-rich Soulseek web client for users who want everything built-in.**
> 
> No scripts. No external integrations. No assembly required.

---

## Feature Roadmap

### ✅ Phase 1: Download Reliability (DONE)

| Feature | Status | Description |
|---------|--------|-------------|
| Auto-Replace Stuck Downloads | ✅ Done | Automatically finds and replaces stuck/failed downloads with alternative sources. Size-threshold filtering, intelligent ranking. |

---

### 🔄 Phase 2: Smart Automation

| Feature | Priority | slskd Status | Description |
|---------|----------|--------------|-------------|
| **Wishlist/Background Search** | HIGH | slskd #957 Open | Save searches that run periodically. Auto-download when matches found. Desktop clients have had this for 20 years. |
| **Auto-Retry Failed Downloads** | HIGH | slskd #959 Open | Automatic retries with configurable attempts and delays. Our auto-replace goes beyond this. |
| **Scheduled Rate Limits** | MEDIUM | slskd #985 Open | Day/night upload/download speed schedules. Like qBittorrent's scheduler. |
| **Auto-Clear Uploads/Downloads** | MEDIUM | Implemented | Already in slskd 0.21+ but we can extend with more granular controls. |
| **Download Queue Position Polling** | LOW | slskd #921 Open | Auto-refresh queue positions for all queued files. |

---

### 🎯 Phase 3: Search Intelligence

| Feature | Priority | slskd Status | Description |
|---------|----------|--------------|-------------|
| **Smart Result Ranking** | HIGH | slskd #746 Open | Weighted algorithm: past downloads from user, group membership, speed, queue depth. Not just simple sorts. |
| **User Download History Badge** | HIGH | slskd #744 Open | Show how many times you've downloaded from each user in search results. Trust indicator. |
| **Consensus Track Matching** | MEDIUM | slskd #747 Open | Compare search results against each other to find "canonical" album releases. |
| **Search by MusicBrainz/Discogs ID** | MEDIUM | slskd #186 Open | Search for albums by metadata ID, ensure complete tracklists. |
| **Track List Matching** | MEDIUM | slskd #189 Open | Filter results that don't match desired track list. |
| **Clear All Searches Button** | HIGH | slskd #1315 Open | One-click clear of accumulated searches. Simple but much-requested. |
| **Default Search Filters** | LOW | slskd #813 Open | Save filter presets (e.g., "islossless" as default). |

---

### 👥 Phase 4: User Management

| Feature | Priority | slskd Status | Description |
|---------|----------|--------------|-------------|
| **Block Users from Search Results** | HIGH | slskd #1379 Open | Hide fake/scam users from ever appearing. |
| **Visual Group Indicators** | MEDIUM | slskd #745 Open | Icons in search results for users in your groups. |
| **File Type Restrictions per Group** | MEDIUM | slskd #1033 Open | Only share certain file types with certain groups. |
| **Download Quotas per Group** | LOW | slskd #388 Closed | Limit downloads per user/group by count or size. |

---

### 📊 Phase 5: Dashboard & Statistics

| Feature | Priority | slskd Status | Description |
|---------|----------|--------------|-------------|
| **Traffic Ticker** | MEDIUM | slskd discussion #547 | Real-time upload/download activity feed in the UI. |
| **Transfer Statistics API** | MEDIUM | slskd #1023 Open | Aggregate stats endpoint for widgets (homepage dashboards). |
| **Prometheus Metrics UI** | LOW | slskd #609 Open | View metrics graphs without external Prometheus setup. |
| **Who's Browsing/Downloading** | LOW | slskd #258 Open | See who's viewing your profile or downloading your files. |
| **Chat Upload Context** | LOW | slskd #615 Open | See what a user has downloaded from you when chatting. |

---

### 📁 Phase 6: Download Organization

| Feature | Priority | slskd Status | Description |
|---------|----------|--------------|-------------|
| **Multiple Destination Folders** | HIGH | slskd #704 Open | Choose destination per download (Music/Movies/Books). |
| **Preserve Remote Path Structure** | MEDIUM | slskd #1362 Open | Avoid folder collisions (multiple "Artwork" folders). |
| **Recursive Folder Download** | MEDIUM | slskd #807 Open | Download folder trees from browse, not just single-level. |
| **Delete Files on Remove** | MEDIUM | slskd #1361 Open | Remove failed downloads from disk when clearing. |
| **Resumable Downloads** | LOW | slskd #406 Open | Resume partial downloads after restart. |

---

### 🔌 Phase 7: Integrations

| Feature | Priority | slskd Status | Description |
|---------|----------|--------------|-------------|
| **Native Lidarr Integration** | HIGH | slskd #451 Closed | Built-in *ARR support without external plugins. |
| **Email Notifications** | LOW | slskd #814 Open | SMTP alerts for private messages/mentions. |
| **Unread Message Badge** | LOW | slskd #270 Open | Notification indicator without opening Chat. |

---

### 🎨 Phase 8: UI Polish

| Feature | Priority | slskd Status | Description |
|---------|----------|--------------|-------------|
| **Dark Mode** | HIGH | slskd #832 Closed | Native dark theme (currently relies on browser extensions). |
| **Download Sorting** | MEDIUM | slskd discussion #1534 | Sort downloads/uploads by various criteria. |
| **Create Chat Rooms** | LOW | slskd #1258 Open | Create public/private rooms from UI. |
| **Predictable Search URLs** | LOW | slskd #1170 Open | Bookmarkable search URLs for browser integration. |

---

## Implementation Philosophy

### What We Build In-House
- Features that require "scripting" in upstream slskd
- Automation that users expect from desktop clients
- Quality-of-life improvements for power users
- Rich UI interactions

### What We Keep Compatible
- Core API compatibility with slskd
- Configuration file format
- Database schema (where possible)
- Docker deployment patterns

### What We DON'T Do
- Break compatibility unnecessarily
- Add bloat for edge cases
- Implement enterprise features
- Compromise on performance

---

## Target Users

1. **Power Users** - Want full-featured client without scripting
2. **Self-Hosters** - Run on home servers, want set-and-forget
3. **Media Collectors** - Need smart search, auto-downloads, *ARR integration
4. **Privacy-Conscious** - Want VPN-friendly, user-blocking features
5. **Nostalgic Users** - Miss desktop client features in web UI

---

## Upstream Contribution Strategy

Features implemented in slskdn that prove popular and stable will be submitted as PRs to upstream slskd. Our auto-replace feature is the first example (PR #1553).

We aim to be a **proving ground** for features, not a permanent fork.

---

## Getting Started

```bash
# Clone slskdn
git clone https://github.com/snapetech/slskdn.git

# Run with Docker
docker-compose up -d

# Or run from source
cd src/slskd && dotnet run
```

---

## Contributing

We welcome contributions! Priority areas:
1. Features from the roadmap above
2. Bug fixes backported from upstream
3. Documentation and testing
4. UI/UX improvements

---

*slskdn - Because "just write a script" isn't always the answer.*

---

## 🚀 Phase 9+: Long-Term Vision (Experimental)

### Multi-Source & Mesh Architecture (experimental/brainz)

The `experimental/brainz` branch explores a significantly expanded architecture:

| Feature Area | Status | Description |
|--------------|--------|-------------|
| **MusicBrainz Integration** | ✅ Phase 1 Complete | Full MB API integration, album targets, Chromaprint fingerprinting, AcoustID lookups, auto-tagging pipeline |
| **Canonical Edition Scoring** | ✅ Phase 2 Complete | Quality scoring (DR, transcode detection, codec-specific analysis), library health scanning, canonical variant selection |
| **Multi-Source Chunked Downloads** | ✅ Phase 2 Complete | Parallel chunk-based downloads from multiple Soulseek peers + overlay mesh, RTT/throughput-aware scheduling, rescue mode for slow transfers |
| **Peer Reputation System** | ✅ Phase 3 Complete | Local-only peer metrics (RTT, throughput, chunk success/failure), decay-based reputation scoring, reputation-gated scheduling |
| **Traffic Accounting & Fairness** | ✅ Phase 3 Complete | Global traffic counters (overlay vs Soulseek upload/download), fairness governor with configurable ratio thresholds |
| **Discovery & Jobs** | ✅ Phase 3 Complete | Discography profiles, artist release graph queries, label crate jobs, sub-job tracking |
| **Job Manifests** | ✅ Phase 4 Complete | YAML export/import for job definitions (mb_release, discography, label_crate), version-controlled job schemas |
| **Session Traces** | ✅ Phase 4 Complete | Structured swarm event logging (chunk-level, per-peer, per-backend), file-based persistence with rotation, trace summarization API |
| **Warm Cache Nodes** | ✅ Phase 4 Complete | Popularity-based caching, LRU eviction, configurable storage limits, pinned content support |
| **Playback-Aware Swarming** | ✅ Phase 4 Complete | Real-time playback feedback API, priority zone derivation (high/mid/low), streaming diagnostics endpoint |
| **Soulbeet API Bridge** | 📋 Phase 5 Planned | Compatibility layer for external music apps, native job APIs, advanced query endpoints |
| **Virtual Soulfind Bridge** | 📋 Phase 6 Planned | Protocol-level compatibility with legacy Soulseek clients, virtual directory generation, transparent multi-source routing |

### Phase 7: Virtual Soulfind Mesh (Research)

**Vision**: Transform slskdn into a **truly decentralized music network** that:
- Enhances Soulseek when the server is available (hybrid mode)
- Replaces Soulseek when the server is unavailable (disaster mode)
- Never requires central servers or privileged nodes

#### Key Concepts

**Shadow Index**: Decentralized MBID→peers mapping via DHT
- Passive observation of Soulseek traffic builds knowledge graph
- Peers share what they've seen: "User X has Album Y"
- DHT stores mappings: `mbid:album:ABC → [peer1, peer2, peer3]`
- When Soulseek dies, mesh already knows who has what

**Scenes**: Decentralized rooms/communities via DHT topics
- DHT topics like `scene:electronic:house` for discovery
- Subscribe to topics to see what peers are sharing
- No central room servers required

**Disaster Resilience**: Mesh-only operation when Soulseek down
- Transfers continue via overlay, guided by shadow index
- Think: BitTorrent DHT + MBID awareness + multi-swarm

#### Architecture Components

1. **Capture & Normalization Pipeline**
   - Observe Soulseek search results and transfers
   - Extract metadata from filenames
   - Normalize to MBID via fingerprinting + MB lookups
   - Build local knowledge: `(username, filepath) → MBID`

2. **Shadow Index DHT**
   - Publish observations to DHT: `mbid:release:X → username`
   - TTL-based expiry (30 days default)
   - Aggregate: multiple peers contribute to index
   - Query: "Who has mbid:release:Y?" → list of usernames

3. **Virtual Directory Generator**
   - Legacy Soulseek clients browse slskdn as if it's a normal user
   - slskdn generates virtual file listings from shadow index
   - Example: `/Daft Punk/Random Access Memories [FLAC]/01-track.flac`
   - Filenames are **virtual** but download triggers multi-swarm

4. **Transparent Multi-Source Routing**
   - Legacy client requests file from virtual path
   - slskdn translates to MBID, queries shadow index for sources
   - Initiates multi-source chunked download from real sources
   - Streams result to legacy client as if single-source

#### Safety & Legal Constraints

**What Shadow Index Is NOT**:
- NOT a public torrent tracker (no magnet links)
- NOT a global catalogue (partial view, TTL expiry)
- NOT a copyright index (MBID mappings, not files)

**Positioning**:
- Shadow index is **peer reputation data** ("I've seen User X share Album Y")
- Used for **disaster recovery** when Soulseek server fails
- Enables **multi-source optimization** within private peer groups

---

### Phase 8: Decentralized Foundation (Research)

**Experimental directions for long-term decentralization:**

#### MeshCore: Transport & Identity Layer

**Overlay DHT**:
- Kademlia-style DHT for decentralized key-value storage
- Keys: pod metadata, membership, shadow index, scene topics
- Values: signed, TTL-based, small payloads only
- Relay-only partial view: no peer is "special"

**Multi-Backend Transfers**:
- **Native mesh protocol**: Direct peer-to-peer over overlay
- **HTTP/WebDAV/S3**: For cloud storage integration
- **Private BitTorrent**: Fallback between known peers only
  - No public DHT announcement
  - No public tracker registration
  - Only shared with mesh peers you trust
- **Optional**: LAN discovery, IPFS, WebTorrent

**Signed Identity**:
- Each peer has Ed25519 keypair for identity
- Public key fingerprint is PeerId (e.g., `peer:a1b2c3...`)
- Sign all DHT publications, messages, variant opinions
- Enables trust, reputation, moderation without central authority

#### MediaCore: ContentID & Variant System

**ContentID Abstraction**:
```csharp
class ContentId
{
    ContentDomain Domain;     // Audio, (future: other media types)
    string MetadataSource;    // "mb", etc.
    string ExternalId;        // "artist:12345", "album:67890"
}
```
- Unified identifiers across MusicBrainz and other metadata sources
- Domain-specific cores: AudioCore (others possible in future)
- Pluggable metadata via facade pattern

**MediaVariant Model**:
- Track different editions/masterings/cuts of same content
- Example: Daft Punk RAM (2013 CD) vs (2013 vinyl rip) vs (2024 remaster)
- Canonical scoring: quality metrics, transcode detection, community opinions
- Cross-codec deduplication: "This MP3 is transcoded from that FLAC"

**Metadata Facade**:
- Abstract interface: `IMetadataProvider`
- Implementations: MusicBrainz, local cache, extensible to other sources
- No tight coupling to specific APIs
- Graceful degradation when services unavailable

#### PodCore: Taste-Based Communities

**Vision**: Small, decentralized groups for sharing taste and recommendations **without** becoming torrent indexes.

**Adoption Strategy**: Pods can optionally **bridge to existing Soulseek chat rooms**, allowing users to:
- Participate in Soulseek rooms from within the mesh UI
- Layer pod features (collection stats, variant opinions, recommendations) on top of existing communities
- Gradually migrate users from Soulseek-only to mesh-native pods
- Bridge modes: ReadOnly (safe, one-way mirroring) or Mirror (two-way messaging)

**Pod Data Model**:
```csharp
class Pod
{
    PodId Id;                    // "pod:artist:mb:daft-punk-hash"
    string DisplayName;          // "Fans of Daft Punk"
    PodVisibility Visibility;    // Private/Unlisted/Listed
    PodFocusType FocusType;      // ContentId/TagCluster/None
    ContentId? FocusContentId;   // artist:mb:daft-punk (optional)
    List<string> Tags;           // ["electronic", "french-house"]
}

class PodMember
{
    PodId PodId;
    string PeerId;               // Mesh identity
    PodRole Role;                // Owner/Moderator/Member
    bool IsBanned;
    string Signature;            // Signed membership
}
```

**Key Features**:
1. **Pod Creation & Membership**
   - Owner creates pod, sets visibility and focus
   - Invite flows for private pods
   - Discovery for listed pods (via DHT search)
   - Signed membership records prevent spoofing

2. **Pod Chat**
   - Decentralized message routing via mesh overlay
   - Each pod has channels (at minimum: `general`)
   - Local-only storage, optional peer backfill
   - No central message server

3. **Content-Linked Pods**
   - Associate pod with ContentId (artist, album, etc.)
   - "Fans of Daft Punk" → `ContentId(AudioArtist, "mb:artist:...")`
   - UI shows "Your collection vs pod's canonical discography"

4. **Pod Variant Opinions**
   - Members share quality preferences: `(ContentId, hash, score, note)`
   - Example: "For RAM, this FLAC has best DR" → signed opinion
   - Opinions feed into canonicality engine (weighted by trust)
   - **NOT file links**: just hashes + scores

5. **Collection vs Pod Views**
   - "You have 8/10 canonical albums the pod likes"
   - "Pod's favorite masterings" for each release
   - "Missing: Discovery (2001), Homework (1997)"
   - UI shaped around **taste comparison**, not downloading

**Safety Constraints**:

What Pods **ARE**:
- Social/preference/reputation spaces
- Discussion forums with content context
- Quality recommendation engines
- Collection completion dashboards

What Pods **ARE NOT**:
- Torrent indexes or magnet link feeds
- Public "download everything" hubs
- Auto-posting bots for new releases
- Direct file-sharing mechanisms

**Implementation Guardrails**:
1. No "paste magnet and share" UI
2. No auto-linkifying of magnets/URLs in chat
3. Pod membership doesn't trigger swarm participation
4. Variant opinions are hashes + scores, not paths/links
5. Transfers still go through MediaCore → SwarmCore → MeshCore

#### SecurityCore: Trust & Moderation

**Pod-Level Trust Extensions**:
- Extend existing peer reputation with pod context
- `PodAffinity`: engagement score, pod-specific trust
- High-trust pods (friends/family) get more bandwidth/leniency
- Low-trust pods (public/anonymous) get stricter policies

**Moderation Tools**:
- Owners/Moderators can kick/ban peers from pod
- Banned peer's membership record updated, signed, published to DHT
- SecurityCore enforces bans at mesh/transfer level
- Global reputation: consistent abuse across pods → lower global trust

**Privacy Guarantees**:
- Pod data scoped to members only
- No global indexing of pods or messages
- Configurable retention (delete old messages)
- Optional E2E encryption for pod chat

#### Domain-Specific Apps (Long-Term)

**Soulbeet (Music)**:
- Artist discographies, album completion tracking
- Quality recommendations per release
- Pod integration: "Join the Daft Punk fan pod"
- Variant comparison: "Your FLAC vs pod's canonical FLAC"

**Future Domain Expansion**:
- Architecture designed to support other media domains
- Shared infrastructure: SwarmCore, MeshCore, MediaCore
- Extensible ContentID system
- Same quality/canonicality principles apply across domains

---

### Implementation Principles

**What Guides This Vision:**
1. **Privacy-First**: No central servers, no global catalogues, relay-only mesh
2. **Protocol-Agnostic**: Multi-backend transfers, graceful fallbacks, no single point of failure
3. **Legal Safety**: Private BT only between known peers, pods are taste/social layers not indexes
4. **Testable**: Simulation-friendly architecture, unit tests at every layer
5. **Incremental**: Each phase builds on previous work, can be tested/deployed independently

**Dependencies Before Advanced Features:**
- Stable overlay DHT and signed message routing (MeshCore foundation)
- ContentID system operational for music domain (MediaCore)
- Trust/reputation primitives mature (SecurityCore)
- Basic Soulbeet UI with artist/album views

**Timeline:**
- Phases 1-4: ✅ Complete (experimental/brainz)
- Phase 5-6: 📋 Next up (Soulbeet integration, virtual Soulfind bridge)
- Phase 7: 🔬 Research (Virtual Soulfind mesh, shadow index)
- Phase 8-9: 🔬 Long-term (MeshCore, MediaCore, PodCore)

---

## Acknowledgments

**slskdn** is built on the excellent work of others:

### Upstream Project

This project is a fork of **[slskd](https://github.com/slskd/slskd)** by jpdillingham and contributors.

- **slskd** is a modern, headless Soulseek client with a web interface and REST API
- Licensed under AGPL-3.0
- We maintain the same license and contribute our changes back to the community
- Philosophy: slskd focuses on a lean core with API-driven extensibility; slskdn focuses on batteries-included features

**Why we forked**: To build experimental mesh networking, decentralized discovery, and advanced automation features that go beyond slskd's core mission. We deeply respect the upstream project and its maintainer's design philosophy.

### Development Dependencies

- **[Soulfind](https://github.com/soulfind-dev/soulfind)** - Open-source Soulseek server implementation (test fixture only)
- See `docs/dev/soulfind-integration-notes.md` for details on how we use it

### Protocol & Metadata

- **Soulseek Protocol** - P2P file-sharing protocol by Nir Arbel
- **[MusicBrainz](https://musicbrainz.org/)** - Open music encyclopedia for metadata
- **[Cover Art Archive](https://coverartarchive.org/)** - Album art for verified releases

---

*slskdn: From batteries-included client to decentralized media mesh.*
