<p align="center">
  <img src="https://raw.githubusercontent.com/slskd/slskd/master/docs/slskd.png" width="100" height="100" alt="slskdN logo">
</p>
<h1 align="center">slskdN(OT)</h1>
<p align="center"><strong>The batteries-included Soulseek web client</strong></p>
<p align="center">
  <a href="https://github.com/snapetech/slskdn/releases">Releases</a> •
  <a href="https://github.com/snapetech/slskdn/issues">Issues</a> •
  <a href="#features">Features</a> •
  <a href="#quick-start">Quick Start</a> •
  <a href="https://discord.gg/NRzj8xycQZ">Discord</a>
</p>
<p align="center">
  <a href="https://github.com/snapetech/slskdn/actions/workflows/ci.yml"><img src="https://github.com/snapetech/slskdn/actions/workflows/ci.yml/badge.svg" alt="CI"></a>
  <a href="https://github.com/snapetech/slskdn/releases"><img src="https://img.shields.io/github/v/release/snapetech/slskdn?label=version" alt="Version"></a>
  <a href="https://ghcr.io/snapetech/slskdn"><img src="https://img.shields.io/badge/docker-ghcr.io%2Fsnapetech%2Fslskdn-blue?logo=docker" alt="Docker"></a>
  <a href="https://aur.archlinux.org/packages/slskdn-bin"><img src="https://img.shields.io/aur/version/slskdn-bin?logo=archlinux&label=AUR" alt="AUR"></a>
  <a href="https://copr.fedorainfracloud.org/coprs/slskdn/slskdn/"><img src="https://img.shields.io/badge/copr-slskdn%2Fslskdn-51A2DA?logo=fedora" alt="COPR"></a>
  <a href="https://launchpad.net/~snapetech/+archive/ubuntu/slskdn"><img src="https://img.shields.io/badge/ppa-snapetech%2Fslskdn-E95420?logo=ubuntu" alt="PPA"></a>
  <a href="https://github.com/snapetech/homebrew-slskdn"><img src="https://img.shields.io/badge/homebrew-slskdn-blue?logo=homebrew" alt="Homebrew"></a>
  <a href="https://search.nixos.org/packages?channel=unstable&query=slskdn"><img src="https://img.shields.io/badge/nix-flake-5277C3?logo=nixos" alt="Nix"></a>
  <a href="https://snapcraft.io/slskdn"><img src="https://img.shields.io/badge/snap-slskdn-E95420?logo=snapcraft" alt="Snap"></a>
  <a href="https://community.chocolatey.org/packages/slskdn"><img src="https://img.shields.io/chocolatey/v/slskdn?label=choco" alt="Chocolatey"></a>
  <a href="https://github.com/microsoft/winget-pkgs/tree/master/manifests/s/snapetech/slskdn"><img src="https://img.shields.io/badge/winget-slskdn-00D1F1?logo=windows" alt="Winget"></a>
  <img src="https://img.shields.io/badge/base-slskd%200.24.1-purple" alt="Based on slskd">
  <a href="https://github.com/snapetech/slskdn/blob/master/LICENSE"><img src="https://img.shields.io/github/license/snapetech/slskdn" alt="License"></a>
  <a href="https://discord.gg/NRzj8xycQZ"><img src="https://img.shields.io/badge/Discord-Join%20Chat-5865F2?logo=discord&logoColor=white" alt="Discord"></a>
  <a href="https://cursor.sh"><img src="https://img.shields.io/badge/Built%20with-Cursor-F7F7F7?logo=cursor&logoColor=black" alt="Built with Cursor"></a>
</p>

---

<small>

## What is slskdN?
**slskdN(OT)** (as in, NOT slskd), is a richly feature-added fork of [slskd](https://github.com/slskd/slskd), the modern web-based Soulseek client. While slskd focuses on being a lean, API-first daemon that lets users implement advanced features via external scripts, **slskdN takes the opposite approach**:
> **Everything built-in. No scripts required.**

If you've ever seen a feature request closed with *"this can be done via the API with a script"* and thought *"but I just want it to work"*—slskdN(OT) is for you. We also think that when someone takes the time to contribute working code, having a conversation about it is the least one can do.

## Features

### 🔄 Auto-Replace Stuck Downloads
Downloads get stuck. Users go offline. Transfers time out. Instead of manually searching for alternatives, slskdN does it automatically.
- Toggle switch in Downloads header ("Auto-Replace")
- Detects stuck downloads (timed out, errored, rejected, cancelled)
- Searches network for alternatives, filters by extension and size (default 5%)
- Ranks by size match, free slots, queue depth, speed
- Auto-cancels stuck download and enqueues best alternative
```bash
--auto-replace-enabled  --auto-replace-max-size-diff-percent 5.0  --auto-replace-interval 60
```

### ⭐ Wishlist / Background Search
Save searches that run automatically in the background. Never miss rare content again.
- New **Wishlist** item in navigation sidebar
- Add searches with custom filters and max results
- Toggle auto-download, configurable interval, track matches and run history
- Manual "Run Now" button for each search
```bash
--wishlist-enabled  --wishlist-interval 60  --wishlist-auto-download  --wishlist-max-results 100
```

### 📁 Multiple Download Destinations
Configure multiple download folders and choose where files go.
```yaml
destinations:
  folders:
    - name: "Music"
      path: "/downloads/music"
      default: true
    - name: "Audiobooks"
      path: "/downloads/audiobooks"
```

### 🗑️ Clear All Searches
One-click cleanup for your search history.
- Red **"Clear All"** button in top-right of search list
- Removes all completed searches, real-time UI update via SignalR

### 🧠 Smart Search Result Ranking
Intelligent sorting that considers multiple factors to show best sources first.
- New default sort: **"⭐ Smart Ranking (Best Overall)"**
- Combines: Upload speed (40pts), Queue length (30pts), Free slot (15pts), History (+/-15pts)
- Purple badge shows smart score next to each username
- Also adds **"File Count"** sort option

### 📊 User Download History Badges
See at a glance which users you've successfully downloaded from before.
- 🟢 **Green** = 5+ successful downloads | 🔵 **Blue** = 1-4 successful | 🟠 **Orange** = More failures
- Hover for exact counts

### 🚫 Block Users from Search Results
Hide specific users from your search results.
- Click user icon (👤✕) to block, **"Hide Blocked Users (N)"** toggle
- Block list stored in localStorage, persists across sessions

### 🗑️ Delete Files on Disk
Clean up unwanted downloads directly from the UI.
- **"Remove and Delete File(s) from Disk"** button in Downloads
- Deletes file AND removes from list, cleans empty parent directories

### 💾 Save Search Filters
Set your preferred search filters once and forget them.
- Enter filters (e.g. `isLossless minbr:320`), click **Save** icon
- Filters auto-load for all future searches

### 🔍 Advanced Search Filters & Page Size
Power user filtering with a visual interface.
- **Visual Filter Editor**: Bitrate, Duration, File Size (Min/Max), CBR/VBR/Lossless toggles
- **Page Size**: 25, 50, 100, 200, 500 results per page
- Settings persist across sessions

### 📝 User Notes & Ratings
Keep track of users with persistent notes and color-coded ratings.
- Add notes from Search Results or Browse views
- Assign color ratings (Red, Green, etc.), mark as "High Priority"

### 💬 Improved Chat Rooms
Enhanced interaction in chat rooms.
- Right-click users: **Browse Files**, **Private Chat**, **Add Notes**

### 📂 Multi-Select Folder Downloads
Download multiple folders at once with checkbox selection.
- In Browse view, check folders and click "Download Selected"
- Recursively collects all files in folders/subfolders

### 📱 Ntfy & Pushover Notifications
Get notified on your phone when important things happen.
- Native support for **Ntfy** and **Pushover**
- Notifications for Private Messages and Room Mentions

### 📑 Tabbed Browsing
Browse multiple users at once.
- Open multiple users in separate tabs, state preserved per tab
- Browse data cached per-user

### 🧠 Unified Smart Source Ranking
All automatic downloads use intelligent source selection based on your history.
- Tracks success/failure rates per user, used by auto-replace and wishlist
- API endpoint at `/api/v0/ranking`

### 🚀 Multi-Source Downloads
Download files from multiple peers simultaneously for faster, more reliable transfers.
- **Parallel chunk downloads** from multiple sources
- **Automatic source discovery** finds all peers with matching files
- **Intelligent stitching** assembles chunks seamlessly
- **Failure resilience** continues from other sources if one fails
- **Network-friendly** — distributes load instead of hammering single users

📖 **[Full network impact analysis](docs/multipart-downloads.md)**

### 📱 PWA & Mobile Support
Install slskdN as an app on your phone.
- Add to Home Screen on iOS/Android, standalone mode

## Quick Start
Getting started is simple—we don't believe in gatekeeping.

### Arch Linux (AUR)
**Drop-in replacement for slskd** — preserves your existing config at `/var/lib/slskd/`.
```bash
yay -S slskdn-bin          # Binary package (recommended)
yay -S slskdn              # Or build from source
sudo systemctl enable --now slskd
```
Access at http://localhost:5030

### Homebrew (macOS/Linux)
Install the pre-built binary:
```bash
brew tap snapetech/slskdn
brew install slskdn
```

### Nix (Flake)
```bash
nix profile install github:snapetech/slskdn
```

### Snap (Linux)
```bash
sudo snap install slskdn
```

### Windows (Winget)
```powershell
winget install snapetech.slskdn
```

### Windows (Chocolatey)
```powershell
choco install slskdn
```

### With Docker
```bash
docker run -d \
  -p 5030:5030 -p 50300:50300 \
  -e SLSKD_SLSK_USERNAME=your_username \
  -e SLSKD_SLSK_PASSWORD=your_password \
  -v /path/to/downloads:/downloads \
  -v /path/to/app:/app \
  --name slskdN \
  ghcr.io/snapetech/slskdn:latest
```

### With Docker Compose
```yaml
version: "3"
services:
  slskdN:
    image: ghcr.io/snapetech/slskdn:latest
    container_name: slskdN
    ports:
      - "5030:5030"
      - "50300:50300"
    environment:
      - SLSKD_SLSK_USERNAME=your_username
      - SLSKD_SLSK_PASSWORD=your_password
    volumes:
      - ./app:/app
      - ./downloads:/downloads
      - ./music:/music:ro
    restart: unless-stopped
```

### From Source
```bash
git clone https://github.com/snapetech/slskdn.git && cd slskdN
curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 8.0
export PATH="$HOME/.dotnet:$PATH"
dotnet run --project src/slskd/slskd.csproj
```

## Comparison with slskd

| Feature | slskd | slskdN |
|---------|:-----:|:------:|
| Core Soulseek functionality | ✅ | ✅ |
| Web UI & REST API | ✅ | ✅ |
| Auto-replace stuck downloads | ❌ | ✅ |
| Wishlist/background search | ❌ | ✅ |
| Multiple download destinations | ❌ | ✅ |
| Clear all searches | ❌ | ✅ |
| Smart result ranking | ❌ | ✅ |
| User download history badges | ❌ | ✅ |
| Block users from search | ❌ | ✅ |
| Delete files on disk | ❌ | ✅ |
| Save default filters | ❌ | ✅ |
| Multi-select folder downloads | ❌ | ✅ |
| Ntfy/Pushover notifications | ❌ | ✅ |
| Tabbed browsing | ❌ | ✅ |
| Smart source ranking | ❌ | ✅ |
| Multi-source downloads | ❌ | ✅ |
| PWA support | ❌ | ✅ |
| Distributed hash mesh | ❌ | ✅ |
| DHT Beacon discovery | ❌ | ✅ |
| NAT traversal assistance | ❌ | ✅ |
| Security hardening | ❌ | ✅ |
| Open to community feedback | 🔒 | ✅ |

## 🚀 Advanced Features

The following advanced features are **fully implemented and production-ready**:

### 🌐 Multi-Source Swarm Downloads ✅
- **Swarm mode** — Download chunks from multiple peers simultaneously (BitTorrent-style)
- **Distributed hash database** — SQLite-backed content verification with mesh sync
- **Epidemic mesh protocol** — Gossip-based hash sharing between slskdN clients
- **BitTorrent DHT rendezvous** — Decentralized peer discovery (60+ bootstrap nodes)
- **Capability discovery** — Auto-detect other slskdN clients on the network
- **NAT detection** — UPnP/NAT-PMP support for firewalled users
- **Live status bar** — Real-time DHT nodes, mesh peers, and hash counts in UI



📖 **Design docs**: [Network impact analysis](docs/multipart-downloads.md)

### 🔒 Security Hardening ✅
- **NetworkGuard** — Rate limiting and connection caps
- **ViolationTracker** — Auto-escalating bans for bad actors
- **PathGuard** — Path traversal protection with sandboxing
- **ContentSafety** — Magic byte verification to detect disguised executables
- **PeerReputation** — Behavioral scoring system
- **ByzantineConsensus** — 2/3+1 voting for multi-source verification
- **EntropyMonitor** — RNG health monitoring
- **FingerprintDetection** — Reconnaissance detection
- **Honeypot & CanaryTraps** — Threat profiling
- **Security dashboard** — Real-time monitoring in Web UI


📖 **Design docs**: [Security implementation](docs/SECURITY_IMPLEMENTATION_SPECS.md) • [CSRF testing guide](docs/security/CSRF_TESTING_GUIDE.md) • [Security comparison](docs/security/SECURITY_COMPARISON_ANALYSIS.md)


## Configuration
slskdN uses the same config format as slskd, with additional options:
```yaml
soulseek:
  username: your_username
  password: your_password
  listen_port: 50300
directories:
  downloads: /downloads
  incomplete: /downloads/incomplete
shares:
  directories:
    - /music
web:
  port: 5030
  authentication:
    username: admin
    password: change_me
# slskdN-specific
global:
  download:
    auto_replace_stuck: true
    auto_replace_threshold: 5.0
    auto_replace_interval: 60
  wishlist:
    enabled: true
    interval: 60
    auto_download: false
destinations:
  folders:
    - name: "Music"
      path: "/downloads/music"
      default: true
```

## Versioning
slskdN follows slskd's version numbers with a suffix: `0.24.1-slskdN.1` = First slskdN release based on slskd 0.24.1

Detailed documentation for configuration options can be found [here](https://github.com/slskd/slskd/blob/master/docs/config.md), and an example of the YAML configuration file can be reviewed [here](https://github.com/slskd/slskd/blob/master/config/slskd.example.yml).

## Reverse Proxy
slskdN may require extra configuration when running it behind a reverse proxy. Refer [here](https://github.com/slskd/slskd/blob/master/docs/reverse_proxy.md) for a short guide.

## Documentation

Comprehensive documentation is available for all features:

### Design Documents
- [Multi-Source Downloads](docs/multipart-downloads.md) - Network impact analysis and architecture
- [DHT Rendezvous Design](docs/DHT_RENDEZVOUS_DESIGN.md) - Peer discovery architecture
- [Security Implementation](docs/SECURITY_IMPLEMENTATION_SPECS.md) - Security feature details
- [CSRF Testing Guide](docs/security/CSRF_TESTING_GUIDE.md) - CSRF protection testing

### Implementation Guides
- [How It Works](HOW-IT-WORKS.md) - Technical architecture and design
- [Features Overview](FEATURES.md) - Complete feature list

### User Guides
- [Configuration](docs/config.md) - All configuration options
- [Building](docs/build.md) - Build instructions
- [Docker](docs/docker.md) - Container deployment
- [Reverse Proxy](docs/reverse_proxy.md) - Reverse proxy setup

### Development
- [Development History](DEVELOPMENT_HISTORY.md) - Feature completion timeline
- [Fork Vision](FORK_VISION.md) - Project philosophy and roadmap
- [Contributing](CONTRIBUTING.md) - How to contribute


## Contributing
We welcome contributions from *everyone*—first-timers and veterans alike. No prior commit history required.

1. **Pick an issue** from our [Issue Tracker](https://github.com/snapetech/slskdn/issues)
2. **Fork the repo** and create a feature branch
3. **Submit a PR** with your changes
```bash
cd src/slskd && dotnet watch run     # Backend
cd src/web && npm install && npm start  # Frontend
```

## Upstream Contributions
Features that prove stable may be submitted as PRs to upstream slskd. Our auto-replace feature was first: [slskd PR #1553](https://github.com/slskd/slskd/pull/1553). We aim to be a **proving ground**, not a permanent fork. We believe good software comes from open dialogue—not just with established contributors, but with everyone who has something to offer. Our door is always open.

## License
[GNU Affero General Public License v3.0](LICENSE), same as slskd.

## Acknowledgments
- [slskd](https://github.com/slskd/slskd) - The excellent foundation
- [Soulseek.NET](https://github.com/jpdillingham/Soulseek.NET) - The .NET Soulseek library
- The Soulseek community

## Use of AI in This Project

This project was built in partnership with tools, not replacements for people. Throughout its development, we made deliberate use of AI-powered assistants—most notably [Cursor](https://cursor.sh) and several leading large language models—as part of the day-to-day engineering workflow.

**These systems helped in three main ways:**

| Area | How AI Assisted |
|------|-----------------|
| **Research & Exploration** | Quickly surfacing prior art, sketching out alternative designs, and pressure-testing edge cases that would have taken much longer to explore alone. |
| **Automation & Busywork** | Generating initial scaffolding, refactoring repetitive patterns, and handling mechanical changes that are important but rarely insightful. |
| **Thinking Partner** | Serving as a second pair of eyes on tricky problems, helping articulate trade-offs, and translating rough ideas into shapes that could be implemented and tested. |

**What these tools did not do is replace responsibility.** Every behavior that matters—protocol decisions, data flows, failure modes, and user-visible effects—was reviewed, edited, or rewritten by a human before it landed in this repository. The models accelerated the work and helped make the project possible at this scope, but accountability for the result sits squarely with the maintainer.

If you're reading this code, you should assume that:
- ✅ AI tools were used as collaborators in research, drafting, and mechanical edits
- ✅ The final form of the project reflects human judgment, testing, and ongoing maintenance

> **In other words: this is an AI-assisted project, not an AI-generated one.**

</small>

<p align="center"><strong>slskdN</strong> — For users who'd rather download music than learn Python.</p>
