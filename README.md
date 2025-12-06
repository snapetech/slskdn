<p align="center">
  <img src="https://raw.githubusercontent.com/slskd/slskd/master/docs/slskd.png" width="128" height="128" alt="slskdn logo">
</p>

<h1 align="center">slskdn</h1>

<p align="center">
  <strong>The batteries-included Soulseek web client</strong>
</p>

<p align="center">
  <a href="https://github.com/snapetech/slskdn/releases">Releases</a> •
  <a href="https://github.com/snapetech/slskdn/issues">Issues</a> •
  <a href="#features">Features</a> •
  <a href="#quick-start">Quick Start</a>
</p>

<p align="center">
  <a href="https://github.com/snapetech/slskdn/actions/workflows/ci.yml">
    <img src="https://github.com/snapetech/slskdn/actions/workflows/ci.yml/badge.svg" alt="CI">
  </a>
  <a href="https://github.com/snapetech/slskdn/releases">
    <img src="https://img.shields.io/github/v/release/snapetech/slskdn?label=version" alt="Version">
  </a>
  <a href="https://ghcr.io/snapetech/slskdn">
    <img src="https://img.shields.io/badge/docker-ghcr.io%2Fsnapetech%2Fslskdn-blue?logo=docker" alt="Docker">
  </a>
  <img src="https://img.shields.io/badge/base-slskd%200.24.1-purple" alt="Based on slskd">
  <a href="https://github.com/snapetech/slskdn/blob/master/LICENSE">
    <img src="https://img.shields.io/github/license/snapetech/slskdn" alt="License">
  </a>
</p>

---

## What is slskdn?

**slskdn** is a feature-rich fork of [slskd](https://github.com/slskd/slskd), the modern web-based Soulseek client.

While slskd focuses on being a lean, API-first daemon that lets users implement advanced features via external scripts, **slskdn takes the opposite approach**:

> **Everything built-in. No scripts required.**

If you've ever seen a feature request closed with *"this can be done via the API with a script"* and thought *"but I just want it to work"*—slskdn is for you.

---

## Features

### 🔄 Auto-Replace Stuck Downloads

Downloads get stuck. Users go offline. Transfers time out. Instead of manually searching for alternatives, slskdn does it automatically.

**How it works:**
- Toggle switch in the Downloads page header ("Auto-Replace")
- Detects stuck downloads (timed out, errored, rejected, cancelled)
- Searches the network for alternative sources
- Filters by file extension and size threshold (default 5%)
- Ranks alternatives by size match, free slots, queue depth, and speed
- Automatically cancels the stuck download and enqueues the best alternative

**CLI Options:**
```bash
--auto-replace-enabled                    # Enable auto-replace
--auto-replace-max-size-diff-percent 5.0  # Size threshold
--auto-replace-interval 60                # Check interval in seconds
```

---

### ⭐ Wishlist / Background Search

Save searches that run automatically in the background. Never miss rare content again.

**How it works:**
- New **Wishlist** item in the navigation sidebar
- Add searches with custom filters and max results
- Toggle auto-download for each wishlist item
- Configurable background search interval
- Track matches and run history per item
- Manual "Run Now" button for each search

**CLI Options:**
```bash
--wishlist-enabled              # Enable wishlist feature
--wishlist-interval 60          # Check interval in minutes
--wishlist-auto-download        # Auto-download found items
--wishlist-max-results 100      # Max results per search
```

---

### 📁 Multiple Download Destinations

Configure multiple download folders and choose where files go.

```yaml
# In your slskd.yml config:
destinations:
  folders:
    - name: "Music"
      path: "/downloads/music"
      default: true
    - name: "Audiobooks"
      path: "/downloads/audiobooks"
    - name: "Other"
      path: "/downloads/other"
```

---

### 🗑️ Clear All Searches

One-click cleanup for your search history.

**How it works:**
- Red **"Clear All"** button in the top-right of the search list
- Removes all completed searches at once
- Real-time UI update via SignalR
- Shows toast notification with count of cleared searches

---

### 🧠 Smart Search Result Ranking

Intelligent sorting that considers multiple factors to show you the best sources first.

**How it works:**
- New default sort: **"⭐ Smart Ranking (Best Overall)"**
- Combines multiple factors into a single score:
  - Upload speed (up to 40 points)
  - Queue length (up to 30 points, lower is better)
  - Free upload slot bonus (15 points)
  - Past download history bonus/penalty (+/- 15 points)
- Purple badge shows smart score next to each username
- Also adds **"File Count"** sort option

---

### 📊 User Download History Badges

See at a glance which users you've successfully downloaded from before.

**How it works:**
- Color-coded badges appear next to usernames in search results:
  - 🟢 **Green** = 5+ successful downloads from this user
  - 🔵 **Blue** = 1-4 successful downloads
  - 🟠 **Orange** = More failures than successes
- Hover over badge to see exact success/failure counts
- Helps identify reliable sources quickly

---

### 🚫 Block Users from Search Results

Hide specific users from your search results.

**How it works:**
- Click the user icon (👤✕) on any search result to block that user
- **"Hide Blocked Users (N)"** toggle shows count of blocked users
- Blocked users show orange ban icon - click to unblock
- Block list stored locally in browser (localStorage)
- Persists across sessions

---

### 🗑️ Delete Files on Disk

Clean up unwanted downloads directly from the UI.

**How it works:**
- New **"Remove and Delete File(s) from Disk"** button in Downloads
- Deletes the file from the filesystem AND removes it from the list
- Removes parent directory if empty
- Includes confirmation dialog to prevent accidents

---

### 💾 Save Search Filters

Set your preferred search filters once and forget them.

**How it works:**
- Enter your filters (e.g. `isLossless minbr:320`)
- Click the **Save** icon in the filter input
- Filters will auto-load for all future searches
- Great for always filtering out lossy or low-quality files

---

### 📏 Advanced Search Filters & Page Size

More control over your search results.

**How it works:**
- **Max File Size**: New `maxfilesize` filter (e.g. `maxfs:500mb`)
- **Page Size**: Configurable results per page (10, 25, 50, 100, All)
- Settings persist across sessions

---

### 📂 Recursive Folder Download

Download entire directories with a single click.

**How it works:**
- In Browse view, look for the download icon next to any folder
- Recursively collects all files in that folder and subfolders
- One-click to queue everything

---

### 📱 Ntfy & Pushover Notifications

Get notified on your phone when important things happen.

**How it works:**
- Native support for **Ntfy** and **Pushover**
- Notifications for Private Messages and Room Mentions
- Configure via `slskd.yml` or environment variables

---

### 📑 Tabbed Browsing

Browse multiple users at once.

**How it works:**
- Open multiple users in separate tabs
- Switch between them without losing your place
- State is preserved for each tab independently

---

### 📱 PWA & Mobile Support

Install slskdn as an app on your phone.

**How it works:**
- Add to Home Screen on iOS/Android
- Standalone mode (no browser UI)
- Native look and feel

---

## Quick Start

### With Docker

```bash
docker run -d \
  -p 5030:5030 \
  -p 50300:50300 \
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
      - "5030:5030"    # Web UI
      - "50300:50300"  # Soulseek listen port
    environment:
      - SLSKD_SLSK_USERNAME=your_username
      - SLSKD_SLSK_PASSWORD=your_password
    volumes:
      - ./app:/app
      - ./downloads:/downloads
      - ./music:/music:ro  # Read-only share
    restart: unless-stopped
```

### From Source

```bash
# Clone the repo
git clone https://github.com/snapetech/slskdn.git
cd slskdn

# Install .NET SDK 8.0 (if needed)
curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 8.0
export PATH="$HOME/.dotnet:$PATH"

# Run the backend
dotnet run --project src/slskd/slskd.csproj

# In another terminal, run the frontend (for development)
cd src/web
npm install
npm start
```

---

## Comparison with slskd

| Feature | slskd | slskdn |
|---------|-------|--------|
| Core Soulseek functionality | ✅ | ✅ |
| Web UI | ✅ | ✅ |
| REST API | ✅ | ✅ |
| Auto-replace stuck downloads | ❌ | ✅ |
| Wishlist/background search | ❌ | ✅ |
| Multiple download destinations | ❌ | ✅ |
| Clear all searches | ❌ | ✅ |
| Smart result ranking | ❌ | ✅ |
| User download history badges | ❌ | ✅ |
| Block users from search | ❌ | ✅ |
| Delete files on disk | ❌ | ✅ |
| Save default filters | ❌ | ✅ |
| Max filesize filter | ❌ | ✅ |
| Configurable page size | ❌ | ✅ |
| Recursive folder download | ❌ | ✅ |
| Ntfy/Pushover notifications | ❌ | ✅ |
| Tabbed browsing | ❌ | ✅ |
| PWA support | ❌ | ✅ |

---

## Configuration

slskdn uses the same configuration format as slskd, with additional options:

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

# slskdn-specific options
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
    - name: "Other"
      path: "/downloads/other"
```

---

## Versioning

slskdn follows slskd's version numbers with a suffix:

- `0.24.1-slskdn.1` = First slskdn release based on slskd 0.24.1
- `0.25.0-slskdn.1` = First slskdn release based on slskd 0.25.0

This makes it easy to see which upstream version you're based on.

---

## Contributing

We welcome contributions! Here's how to help:

1. **Pick an issue** from our [Issue Tracker](https://github.com/snapetech/slskdn/issues)
2. **Fork the repo** and create a feature branch
3. **Submit a PR** with your changes

### Development Setup

```bash
# Backend (C#/.NET 8)
cd src/slskd
dotnet watch run

# Frontend (React)
cd src/web
npm install
npm start
```

---

## Upstream Contributions

Features that prove stable in slskdn may be submitted as PRs to upstream slskd. Our auto-replace feature was the first: [slskd PR #1553](https://github.com/slskd/slskd/pull/1553).

We aim to be a **proving ground**, not a permanent fork.

---

## License

slskdn is licensed under the [GNU Affero General Public License v3.0](LICENSE), the same as slskd.

---

## Acknowledgments

- [slskd](https://github.com/slskd/slskd) - The excellent foundation we're building on
- [Soulseek.NET](https://github.com/jpdillingham/Soulseek.NET) - The .NET Soulseek library
- The Soulseek community

---

<p align="center">
  <strong>slskdn</strong> — Because "just write a script" isn't always the answer.
</p>
