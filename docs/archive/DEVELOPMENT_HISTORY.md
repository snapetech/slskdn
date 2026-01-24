# slskdn Development History

> **Note**: slskdn is a **distribution** of slskd with advanced features and experimental subsystems. This document tracks development progress across all phases.

## Phase Summary

| Phase | Name | Status | Completion |
|-------|------|--------|------------|
| 1 | Download Reliability | ✅ Complete | 100% |
| 2 | Smart Automation | ✅ Complete | 100% |
| 3 | Search Intelligence | 🟡 Mostly Done | 60% |
| 4 | User Management | 🟡 Mostly Done | 75% |
| 5 | Dashboard & Statistics | 🟡 Partial | 20% |
| 6 | Download Organization | 🟡 Mostly Done | 75% |
| 7 | Integrations | 🟡 Partial | 30% |
| 8 | UI Polish | 🟡 Mostly Done | 85% |
| 9 | Infrastructure & Packaging | ✅ Complete | 100% |

---

## ✅ Phase 1: Download Reliability (Complete)

| Feature | Status | Release | Notes |
|---------|--------|---------|-------|
| Auto-Replace Stuck Downloads | ✅ Done | .1 | Finds alternatives for stuck/failed downloads |
| Persistent Auto-Retry State | ✅ Done | .15 | Remembers enabled state across restarts |

---

## ✅ Phase 2: Smart Automation (Complete)

| Feature | Status | Release | Notes |
|---------|--------|---------|-------|
| Wishlist/Background Search | ✅ Done | .2 | Save searches, auto-run, auto-download |
| Auto-Retry Failed Downloads | ✅ Done | .1 | Via auto-replace feature |
| Auto-Clear Uploads/Downloads | ✅ Done | upstream | Already in slskd 0.21+ |
| Scheduled Rate Limits | ✅ Done | 2025-12-12 | Day/night speed schedules (T-002) |
| Download Queue Position Polling | ✅ Done | 2025-12-12 | Auto-refresh queue positions (T-003) |

---

## 🟡 Phase 3: Search Intelligence (60% Complete)

| Feature | Status | Release | Notes |
|---------|--------|---------|-------|
| Smart Result Ranking | ✅ Done | .4 | Speed, queue, slots, history weighted |
| User Download History Badge | ✅ Done | .4 | Green/blue/orange badges |
| Clear All Searches Button | ✅ Done | .3 | One-click cleanup |
| Search API Pagination | ✅ Done | .17 | Prevents browser hang on large histories |
| Advanced Search Filters | ✅ Done | .5 | Modal with include/exclude, size, bitrate |
| Consensus Track Matching | ❌ Pending | - | Find canonical album releases |
| Search by MusicBrainz/Discogs ID | ❌ Pending | - | Metadata-based search |
| Track List Matching | ❌ Pending | - | Filter by track list |
| Default Search Filters | ❌ Pending | - | Save filter presets |

---

## 🟡 Phase 4: User Management (75% Complete)

| Feature | Status | Release | Notes |
|---------|--------|---------|-------|
| Block Users from Search Results | ✅ Done | .5 | Hide blocked users toggle |
| User Notes & Ratings | ✅ Done | .6 | Personal notes per user |
| Visual Group Indicators | ✅ Done | 2025-12-12 | Icons for group members (T-004) |
| File Type Restrictions per Group | ❌ Pending | - | Share specific types to groups |
| Download Quotas per Group | ❌ Pending | - | Limit downloads per user/group |

---

## 🟡 Phase 5: Dashboard & Statistics (20% Complete)

| Feature | Status | Notes |
|---------|--------|-------|
| Traffic Ticker | ✅ Done | Real-time activity feed (T-005, 2025-12-12) |
| Transfer Statistics API | ❌ Pending | Aggregate stats endpoint |
| Prometheus Metrics UI | ❌ Pending | Built-in metrics graphs |
| Who's Browsing/Downloading | ❌ Pending | See who's viewing you |
| Chat Upload Context | ❌ Pending | See download history in chat |

---

## 🟡 Phase 6: Download Organization (75% Complete)

| Feature | Status | Release | Notes |
|---------|--------|---------|-------|
| Multiple Destination Folders | ✅ Done | .2 | Choose per download |
| Delete Files on Disk | ✅ Done | .7 | Remove failed downloads |
| Recursive Folder Download | ✅ Done | upstream | Download folder trees |
| Preserve Remote Path Structure | ❌ Pending | - | Avoid folder collisions |
| Resumable Downloads | ❌ Pending | - | Resume after restart |

---

## 🟡 Phase 7: Integrations (30% Complete)

| Feature | Status | Release | Notes |
|---------|--------|---------|-------|
| Push Notifications | ✅ Done | .8 | Ntfy, Pushover, Pushbullet |
| Native Lidarr Integration | ⏭️ Skipped | - | tubifarry plugin covers this |
| Email Notifications | ❌ Pending | - | SMTP alerts |
| Unread Message Badge | ❌ Pending | - | Notification indicator |

---

## 🟡 Phase 8: UI Polish (70% Complete)

| Feature | Status | Release | Notes |
|---------|--------|---------|-------|
| Dark Mode Toggle | ✅ Done | upstream | Theme switcher in header |
| Tabbed Browse Sessions | ✅ Done | .10 | Multiple browse tabs, persistent |
| Full-Width Room/Chat Search | ✅ Done | .15 | Searchable room/user inputs |
| LRU Cache for Browse State | ✅ Done | .14 | Prevents localStorage bloat |
| Create Chat Rooms | ❌ Pending | - | Create rooms from UI |
| Predictable Search URLs | ❌ Pending | - | Bookmarkable searches |
| Persistent Room/Chat Tabs | ✅ Done | 2025-12-12 | Like Browse tabs (T-001) |

---

## ✅ Phase 9: Infrastructure & Packaging (Complete)

| Feature | Status | Release | Notes |
|---------|--------|---------|-------|
| AUR Binary Package | ✅ Done | .6 | `yay -S slskdN-bin` |
| AUR Source Package | ✅ Done | .7 | `yay -S slskdN` |
| Fedora COPR | ✅ Done | .13 | RPM packages |
| Ubuntu PPA | ✅ Done | .13 | Debian packages |
| Docker (GHCR) | ✅ Done | .1 | `ghcr.io/snapetech/slskdn` |
| Self-Hosted CI Runners | ✅ Done | .14 | Private runners |
| Automated Release CI | ✅ Done | .11 | Full CI/CD pipeline |
| Upstream Sync Workflow | ✅ Done | .14 | Auto-merge upstream |

---

## Upstream Bug Fixes

| Bug | Status | Notes |
|-----|--------|-------|
| Async-void in RoomService | ✅ Fixed | Prevents crash on login errors |
| Undefined returns in searches.js | ✅ Fixed | Prevents frontend errors |
| Undefined returns in transfers.js | ✅ Fixed | Prevents frontend errors |
| Flaky UploadGovernorTests | ✅ Fixed | Integer division edge case |
| Search API lacks pagination | ✅ Fixed | Prevents browser hang |
| Duplicate message DB error | ✅ Fixed | Handle replayed messages |
| Version check crash | ✅ Fixed | Suppress noisy warning |
| ObjectDisposedException on shutdown | ✅ Fixed | Graceful shutdown |

---

## Release Timeline

| Release | Date | Highlights |
|---------|------|------------|
| .1 | Dec 2 | Auto-replace stuck downloads |
| .2 | Dec 2 | Wishlist, Multiple destinations |
| .3 | Dec 2 | Clear all searches |
| .4 | Dec 3 | Smart ranking, History badges |
| .5 | Dec 3 | Search filters, Block users |
| .6 | Dec 3 | User notes, AUR binary |
| .7 | Dec 3 | Delete files, AUR source |
| .8 | Dec 3 | Push notifications |
| .9 | Dec 4 | Bug fixes |
| .10 | Dec 4 | Tabbed browse |
| .11 | Dec 4 | CI/CD automation |
| .12 | Dec 4 | Package fixes |
| .13 | Dec 5 | COPR, PPA, openSUSE |
| .14 | Dec 5 | Self-hosted runners, LRU cache |
| .15 | Dec 6 | Room/Chat UI, Bug fixes |
| .16 | Dec 6 | StyleCop cleanup |
| .17 | Dec 6 | Search pagination, Flaky test fix |
| .18 | Dec 7 | Upstream merge, Doc cleanup |

---

## Pending Features (TODO)

### High Priority
- Persistent Room/Chat Tabs (like Browse)
- Scheduled Rate Limits

### Medium Priority
- Consensus Track Matching
- Search by MusicBrainz/Discogs ID
- Track List Matching
- Visual Group Indicators
- Traffic Ticker
- Email Notifications

### Low Priority
- Prometheus Metrics UI
- Who's Browsing/Downloading
- Create Chat Rooms
- Predictable Search URLs
- Resumable Downloads

---

*Last updated: December 7, 2025 (Release .18)*

