# slskdn - The Rich-Featured Soulseek Client

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

### Future: Decentralized Mesh & Community Features

**Experimental directions beyond Phase 6:**

#### Mesh Transport Layer
- **Overlay DHT**: Decentralized peer discovery and metadata distribution
- **Multi-Backend Transfers**: Native mesh protocol, HTTP/WebDAV/object storage, private BitTorrent fallback (only between known peers, no public DHT/trackers), optional LAN/IPFS/WebTorrent
- **Relay-Only Partial View**: No global indexer or catalogue, preserve privacy and decentralization
- **Signed Identity**: Cryptographic peer identities for trust, reputation, and moderation

#### MediaCore & ContentID System
- **ContentID Abstraction**: Unified identifiers for audio/movies/TV across metadata sources (MusicBrainz, TMDB, TVDb, etc.)
- **MediaVariant Model**: Track different editions/masterings/cuts/encodings of the same canonical content
- **Cross-Codec Canonicality**: Smart deduplication and quality scoring across formats
- **Metadata Facade**: Pluggable metadata sources without tight coupling

#### PodCore: Taste-Based Communities
- **Pod Concept**: Small, topic- or trust-based groups (e.g. "Fans of Daft Punk", "Trusted Friends", "JP City Pop Collectors")
- **Membership & Roles**: Owner/Moderator/Member roles, signed membership records, invitation flows
- **Pod Chat**: Decentralized message routing via mesh overlay, local-only storage, optional backfill
- **Content-Linked Pods**: Associate pods with specific artists/albums/shows via ContentID
- **Pod Variant Opinions**: Share quality preferences (signed hashes + scores), feed into canonicality engine
- **Collection vs Pod Views**: "You have 8/10 canonical albums the pod likes", "Pod's favorite masterings"
- **Safety Constraints**: 
  - Pods are about **taste, recommendations, and social context**
  - **NOT** torrent indexes or magnet link feeds
  - UI shaped around discussion, not direct file sharing
  - No auto-linkifying of magnets/URLs
  - Pod membership doesn't trigger implicit swarm participation

#### SecurityCore & Moderation
- **Pod-Level Trust**: Extend reputation system with pod-specific trust scores
- **Abuse Controls**: Owner/moderator kick/ban capabilities, signed membership updates
- **Global Reputation Feed**: Pods inform global trust scoring (consistent abuse across pods → lower global trust)
- **Privacy-First**: Pod data scoped to members only, no global indexing, configurable retention

#### Domain-Specific Apps (Long-Term)
- **Soulbeet** (Music): Artist discographies, album completion, quality recommendations, pod integration
- **Moviebeet** (Movies): Collection management, edition tracking (director's cuts, etc.)
- **Tvbeet** (TV): Series tracking, episode completion, rewatch clubs
- **Unified UI**: Tabbed interface across domains, shared swarm/mesh/metadata infrastructure

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
- ContentID system operational across domains (MediaCore)
- Trust/reputation primitives mature (SecurityCore)
- Basic domain app UIs (Soulbeet artist/album views)

**Timeline:**
- Phases 1-4: ✅ Complete (experimental/brainz, Dec 2025)
- Phase 5-6: 📋 Next up (Soulbeet integration, virtual Soulfind bridge)
- Phase 7+: 🔬 Research (Mesh/MediaCore/PodCore, 2026+)

---

*slskdn: From batteries-included client to decentralized media mesh.*
