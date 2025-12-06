<p align="center">
  <img src="https://raw.githubusercontent.com/slskd/slskd/master/docs/slskd.png" width="100" height="100" alt="slskdn logo">
</p>
<h1 align="center">slskdn</h1>
<p align="center"><strong>The batteries-included Soulseek web client</strong></p>
<p align="center">
  <a href="https://github.com/snapetech/slskdn/releases">Releases</a> •
  <a href="https://github.com/snapetech/slskdn/issues">Issues</a> •
  <a href="#features">Features</a> •
  <a href="#quick-start">Quick Start</a>
</p>
<p align="center">
  <a href="https://github.com/snapetech/slskdn/actions/workflows/ci.yml"><img src="https://github.com/snapetech/slskdn/actions/workflows/ci.yml/badge.svg" alt="CI"></a>
  <a href="https://github.com/snapetech/slskdn/releases"><img src="https://img.shields.io/github/v/release/snapetech/slskdn?label=version" alt="Version"></a>
  <a href="https://ghcr.io/snapetech/slskdn"><img src="https://img.shields.io/badge/docker-ghcr.io%2Fsnapetech%2Fslskdn-blue?logo=docker" alt="Docker"></a>
  <a href="https://aur.archlinux.org/packages/slskdn-bin"><img src="https://img.shields.io/aur/version/slskdn-bin?logo=archlinux&label=AUR" alt="AUR"></a>
  <img src="https://img.shields.io/badge/base-slskd%200.24.1-purple" alt="Based on slskd">
  <a href="https://github.com/snapetech/slskdn/blob/master/LICENSE"><img src="https://img.shields.io/github/license/snapetech/slskdn" alt="License"></a>
</p>

---

<small>

## What is slskdn?
**slskdn** is a feature-rich fork of [slskd](https://github.com/slskd/slskd), the modern web-based Soulseek client. While slskd focuses on being a lean, API-first daemon that lets users implement advanced features via external scripts, **slskdn takes the opposite approach**:
> **Everything built-in. No scripts required.**

If you've ever seen a feature request closed with *"this can be done via the API with a script"* and thought *"but I just want it to work"*—slskdn is for you. We also think that when someone takes the time to contribute working code, having a conversation about it is the least one can do.

## Features

### 🔄 Auto-Replace Stuck Downloads
Downloads get stuck. Users go offline. Transfers time out. Instead of manually searching for alternatives, slskdn does it automatically.
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

### 📱 PWA & Mobile Support
Install slskdn as an app on your phone.
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

### With Docker
```bash
docker run -d \
  -p 5030:5030 -p 50300:50300 \
  -e SLSKD_SLSK_USERNAME=your_username \
  -e SLSKD_SLSK_PASSWORD=your_password \
  -v /path/to/downloads:/downloads \
  -v /path/to/app:/app \
  --name slskdn \
  ghcr.io/snapetech/slskdn:latest
```

### With Docker Compose
```yaml
version: "3"
services:
  slskdn:
    image: ghcr.io/snapetech/slskdn:latest
    container_name: slskdn
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
git clone https://github.com/snapetech/slskdn.git && cd slskdn
curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 8.0
export PATH="$HOME/.dotnet:$PATH"
dotnet run --project src/slskd/slskd.csproj
```

## Comparison with slskd

| Feature | slskd | slskdn |
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
| PWA support | ❌ | ✅ |
| Open to community feedback | 🔒 | ✅ |

## Configuration
slskdn uses the same config format as slskd, with additional options:
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
# slskdn-specific
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
slskdn follows slskd's version numbers with a suffix: `0.24.1-slskdn.1` = First slskdn release based on slskd 0.24.1

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

</small>

<p align="center"><strong>slskdn</strong> — For users who'd rather download music than learn Python.</p>
