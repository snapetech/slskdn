# Tasks (Source of Truth)

> This file is the canonical task list for slskdN development.  
> AI agents should add/update tasks here, not invent ephemeral todos in chat.

---

## Active Development

### High Priority

### High Priority

*No high priority tasks currently active

    /^### Medium Priority$/d
### Medium Priority

### Medium Priority

*No medium priority tasks currently active

    /^### Low Priority$/d
### Low Priority

- [ ] **T-006**: Create Chat Rooms from UI
  - Status: Not started
  - Priority: Low
  - Related: slskd #1258
  - Notes: Create public/private rooms from web interface

- [ ] **T-007**: Predictable Search URLs
  - Status: Not started
  - Priority: Low
  - Related: slskd #1170
  - Notes: Bookmarkable search URLs for browser integration

---

## Packaging & Distribution

- [ ] **T-010**: TrueNAS SCALE Apps
  - Status: Not started
  - Priority: High
  - Notes: Helm chart or ix-chart format

- [ ] **T-011**: Synology Package Center
  - Status: Not started
  - Priority: High
  - Notes: SPK format, cross-compile for ARM/x86

- [ ] **T-012**: Homebrew Formula
  - Status: Not started
  - Priority: High
  - Notes: macOS package manager support

- [ ] **T-013**: Flatpak (Flathub)
  - Status: Not started
  - Priority: High
  - Notes: Universal Linux packaging

---

## Completed Tasks

- [x] **T-100**: Auto-Replace Stuck Downloads
  - Status: Done (Release .1)
  - Notes: Finds alternatives for stuck/failed downloads

- [x] **T-101**: Wishlist/Background Search
  - Status: Done (Release .2)
  - Notes: Save searches, auto-run, auto-download

- [x] **T-102**: Smart Result Ranking
  - Status: Done (Release .4)
  - Notes: Speed, queue, slots, history weighted

- [x] **T-103**: User Download History Badge
  - Status: Done (Release .4)
  - Notes: Green/blue/orange badges

- [x] **T-104**: Advanced Search Filters
  - Status: Done (Release .5)
  - Notes: Modal with include/exclude, size, bitrate

- [x] **T-105**: Block Users from Search Results
  - Status: Done (Release .5)
  - Notes: Hide blocked users toggle

- [x] **T-106**: User Notes & Ratings
  - Status: Done (Release .6)
  - Notes: Personal notes per user

- [x] **T-107**: Multiple Destination Folders
  - Status: Done (Release .2)
  - Notes: Choose destination per download

- [x] **T-108**: Tabbed Browse Sessions
  - Status: Done (Release .10)
  - Notes: Multiple browse tabs, persistent

- [x] **T-109**: Push Notifications
  - Status: Done (Release .8)
  - Notes: Ntfy, Pushover, Pushbullet

---

- [x] **T-001**: Persistent Room/Chat Tabs
  - Status: Done (2025-12-12)
  - Priority: High
  - Branch: experimental/whatAmIThinking
  - Related: `TODO.md`, Browse tabs implementation
  - Notes: Implemented tabbed interface like Browse. Reuses `Browse.jsx`/`BrowseSession.jsx` patterns.

- [x] **T-002**: Scheduled Rate Limits
  - Status: Done (2025-12-12)
  - Priority: High
  - Branch: experimental/whatAmIThinking
  - Related: slskd #985
  - Notes: Day/night upload/download speed schedules like qBittorrent

- [x] **T-003**: Download Queue Position Polling
  - Status: Done (2025-12-12)
  - Priority: Medium
  - Branch: experimental/whatAmIThinking
  - Related: slskd #921
  - Notes: Auto-refresh queue positions for queued files

- [x] **T-004**: Visual Group Indicators
  - Status: Done (2025-12-12)
  - Priority: Medium
  - Branch: experimental/whatAmIThinking
  - Related: slskd #745
  - Notes: Icons in search results for users in your groups

- [x] **T-005**: Traffic Ticker
  - Status: Done (2025-12-12)
  - Priority: Medium
  - Branch: experimental/whatAmIThinking
  - Related: slskd discussion #547
  - Notes: Real-time upload/download activity feed in UI


*Last updated: January 21, 2026*

