# slskdn v0.24.1-slskdn.35

> *"Batteries included, scripts not required."*

## What's New in .35

### 🔧 Fixes
- **AUR Package Checksums** - Proper hashes for static files; no more rebuild shenanigans

---

## ✨ Features (Since Fork Inception)

slskdn is a batteries-included fork of slskd. Everything below works out of the box — no Python scripts, no cron jobs, no external tooling required.

### 🔄 Download Reliability
| Feature | Description |
|---------|-------------|
| **Auto-Replace Stuck Downloads** | Automatically finds alternative sources when downloads stall or fail. Configurable size thresholds and intelligent ranking. |
| **Persistent Auto-Retry State** | Remembers your preferences across restarts. Set it and forget it. |

### 🎯 Search Intelligence
| Feature | Description |
|---------|-------------|
| **Smart Result Ranking** | Weighted algorithm considering speed, queue depth, slots, and your download history with each user. |
| **User Download History Badges** | Green/blue/orange indicators showing how often you've successfully downloaded from each user. Trust at a glance. |
| **Advanced Search Filters** | Modal with include/exclude terms, size limits, bitrate filters. Proper filtering, properly done. |
| **Clear All Searches** | One-click cleanup. Tidy desk, tidy mind. |
| **Search API Pagination** | Large search histories no longer bring your browser to its knees. |

### 📁 Download Organisation
| Feature | Description |
|---------|-------------|
| **Multiple Destination Folders** | Route downloads to different locations (Music/Films/Books) as you queue them. |
| **Delete Files on Remove** | Failed downloads actually leave your disk. Revolutionary, we know. |

### 👥 User Management
| Feature | Description |
|---------|-------------|
| **Block Users from Search Results** | Toggle to hide blocked users. Some people simply aren't worth the bandwidth. |
| **User Notes & Ratings** | Personal notes per user. Remember who had that excellent FLAC collection. |

### 🔔 Notifications
| Feature | Description |
|---------|-------------|
| **Push Notifications** | Native Ntfy, Pushover, and Pushbullet support. Get pinged when your downloads complete. |

### 🎨 UI Enhancements
| Feature | Description |
|---------|-------------|
| **Tabbed Browse Sessions** | Multiple browse tabs, persistent across sessions. Browse like a civilised person. |
| **Full-Width Room/Chat Search** | Searchable inputs for rooms and users. No more scrolling through endless lists. |
| **LRU Cache for Browse State** | Prevents localStorage bloat. Your browser will thank you. |
| **Wishlist UI** | Save searches that run periodically and auto-download matches. The feature desktop clients have had for two decades. |
| **slskdn Footer** | Links to GitHub and documentation. Subtle branding, nothing garish. |

### 🐛 Upstream Bug Fixes
We've addressed several issues that affect the upstream codebase:
- Async-void crash in RoomService on login errors
- Undefined returns in searches.js and transfers.js
- Flaky UploadGovernorTests (integer division edge case)
- Duplicate message database errors
- Version check crash on startup
- ObjectDisposedException on graceful shutdown

---

## 📦 Installation

```bash
# Arch Linux (AUR)
yay -S slskdn-bin    # Binary package
yay -S slskdn        # Build from source

# Fedora (COPR)
sudo dnf copr enable snapetech/slskdn
sudo dnf install slskdn

# Ubuntu/Debian (PPA)
sudo add-apt-repository ppa:snapetech/slskdn
sudo apt update && sudo apt install slskdn

# Docker
docker pull ghcr.io/snapetech/slskdn:latest
```

---

## 🔜 Coming in Dev Builds

Fancy living on the edge? The `experimental/multi-source-swarm` branch includes:
- **Multi-Source Downloads** — Download from multiple peers simultaneously
- **DHT Peer Discovery** — Find other slskdn users via BitTorrent DHT
- **Security Hardening** — Zero-trust framework with defence-in-depth

Install via: `yay -S slskdn-dev`

---

<p align="center"><em>"slop on top"</em> 🍦🤖✨</p>

