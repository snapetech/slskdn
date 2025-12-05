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
| **Wishlist/Background Search** | HIGH | [#957](https://github.com/slskd/slskd/issues/957) Open | Save searches that run periodically. Auto-download when matches found. Desktop clients have had this for 20 years. |
| **Auto-Retry Failed Downloads** | HIGH | [#959](https://github.com/slskd/slskd/issues/959) Open | Automatic retries with configurable attempts and delays. Our auto-replace goes beyond this. |
| **Scheduled Rate Limits** | MEDIUM | [#985](https://github.com/slskd/slskd/issues/985) Open | Day/night upload/download speed schedules. Like qBittorrent's scheduler. |
| **Auto-Clear Uploads/Downloads** | MEDIUM | Implemented | Already in slskd 0.21+ but we can extend with more granular controls. |
| **Download Queue Position Polling** | LOW | [#921](https://github.com/slskd/slskd/issues/921) Open | Auto-refresh queue positions for all queued files. |

---

### 🎯 Phase 3: Search Intelligence

| Feature | Priority | slskd Status | Description |
|---------|----------|--------------|-------------|
| **Smart Result Ranking** | HIGH | [#746](https://github.com/slskd/slskd/issues/746) Open | Weighted algorithm: past downloads from user, group membership, speed, queue depth. Not just simple sorts. |
| **User Download History Badge** | HIGH | [#744](https://github.com/slskd/slskd/issues/744) Open | Show how many times you've downloaded from each user in search results. Trust indicator. |
| **Consensus Track Matching** | MEDIUM | [#747](https://github.com/slskd/slskd/issues/747) Open | Compare search results against each other to find "canonical" album releases. |
| **Search by MusicBrainz/Discogs ID** | MEDIUM | [#186](https://github.com/slskd/slskd/issues/186) Open | Search for albums by metadata ID, ensure complete tracklists. |
| **Track List Matching** | MEDIUM | [#189](https://github.com/slskd/slskd/issues/189) Open | Filter results that don't match desired track list. |
| **Clear All Searches Button** | HIGH | [#1315](https://github.com/slskd/slskd/issues/1315) Open | One-click clear of accumulated searches. Simple but much-requested. |
| **Default Search Filters** | LOW | [#813](https://github.com/slskd/slskd/issues/813) Open | Save filter presets (e.g., "islossless" as default). |

---

### 👥 Phase 4: User Management

| Feature | Priority | slskd Status | Description |
|---------|----------|--------------|-------------|
| **Block Users from Search Results** | HIGH | [#1379](https://github.com/slskd/slskd/issues/1379) Open | Hide fake/scam users from ever appearing. |
| **Visual Group Indicators** | MEDIUM | [#745](https://github.com/slskd/slskd/issues/745) Open | Icons in search results for users in your groups. |
| **File Type Restrictions per Group** | MEDIUM | [#1033](https://github.com/slskd/slskd/issues/1033) Open | Only share certain file types with certain groups. |
| **Download Quotas per Group** | LOW | [#388](https://github.com/slskd/slskd/issues/388) Closed | Limit downloads per user/group by count or size. |

---

### 📊 Phase 5: Dashboard & Statistics

| Feature | Priority | slskd Status | Description |
|---------|----------|--------------|-------------|
| **Traffic Ticker** | MEDIUM | [Discussion #547](https://github.com/slskd/slskd/discussions/547) | Real-time upload/download activity feed in the UI. |
| **Transfer Statistics API** | MEDIUM | [#1023](https://github.com/slskd/slskd/issues/1023) Open | Aggregate stats endpoint for widgets (homepage dashboards). |
| **Prometheus Metrics UI** | LOW | [#609](https://github.com/slskd/slskd/issues/609) Open | View metrics graphs without external Prometheus setup. |
| **Who's Browsing/Downloading** | LOW | [#258](https://github.com/slskd/slskd/issues/258) Open | See who's viewing your profile or downloading your files. |
| **Chat Upload Context** | LOW | [#615](https://github.com/slskd/slskd/issues/615) Open | See what a user has downloaded from you when chatting. |

---

### 📁 Phase 6: Download Organization

| Feature | Priority | slskd Status | Description |
|---------|----------|--------------|-------------|
| **Multiple Destination Folders** | HIGH | [#704](https://github.com/slskd/slskd/issues/704) Open | Choose destination per download (Music/Movies/Books). |
| **Preserve Remote Path Structure** | MEDIUM | [#1362](https://github.com/slskd/slskd/issues/1362) Open | Avoid folder collisions (multiple "Artwork" folders). |
| **Recursive Folder Download** | MEDIUM | [#807](https://github.com/slskd/slskd/issues/807) Open | Download folder trees from browse, not just single-level. |
| **Delete Files on Remove** | MEDIUM | [#1361](https://github.com/slskd/slskd/issues/1361) Open | Remove failed downloads from disk when clearing. |
| **Resumable Downloads** | LOW | [#406](https://github.com/slskd/slskd/issues/406) Open | Resume partial downloads after restart. |

---

### 🔌 Phase 7: Integrations

| Feature | Priority | slskd Status | Description |
|---------|----------|--------------|-------------|
| **Native Lidarr Integration** | HIGH | [#451](https://github.com/slskd/slskd/issues/451) Closed | Built-in *ARR support without external plugins. |
| **Email Notifications** | LOW | [#814](https://github.com/slskd/slskd/issues/814) Open | SMTP alerts for private messages/mentions. |
| **Unread Message Badge** | LOW | [#270](https://github.com/slskd/slskd/issues/270) Open | Notification indicator without opening Chat. |

---

### 🎨 Phase 8: UI Polish

| Feature | Priority | slskd Status | Description |
|---------|----------|--------------|-------------|
| **Dark Mode** | HIGH | [#832](https://github.com/slskd/slskd/issues/832) Closed | Native dark theme (currently relies on browser extensions). |
| **Download Sorting** | MEDIUM | [Discussion #1534](https://github.com/slskd/slskd/discussions/1534) | Sort downloads/uploads by various criteria. |
| **Create Chat Rooms** | LOW | [#1258](https://github.com/slskd/slskd/issues/1258) Open | Create public/private rooms from UI. |
| **Predictable Search URLs** | LOW | [#1170](https://github.com/slskd/slskd/issues/1170) Open | Bookmarkable search URLs for browser integration. |

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

