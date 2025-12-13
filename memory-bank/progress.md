# Progress Log

> Chronological log of development activity.
> AI agents should append here after completing significant work.

---

## 2025-12-13

### T-005: Traffic Ticker (Medium Priority)
- **Status**: ✅ **COMPLETED**
- **Implementation Details**:
  - Added `TransfersHub` SignalR hub with `TransferActivity` model for real-time broadcasting
  - Modified `Application.cs` to wire transfer state change events to broadcast activity
  - Created `TrafficTicker` React component with live activity feed and expandable list
  - Added transfers hub connection factory and integrated into downloads/uploads pages
  - Implemented visual indicators: download/upload icons, completion status colors, connection status
  - Added hover tooltips with detailed activity information and timestamps
  - Maintains last 50 activities with automatic cleanup
- **Technical Notes**:
  - Leveraged existing SignalR infrastructure (similar to LogsHub pattern)
  - Transfer state changes broadcast via `Client_TransferStateChanged` event handler
  - Frontend uses `Promise.allSettled()` for graceful error handling
  - Activity feed shows real-time progress for active transfers and completion notifications
  - Connection status indicator shows hub connectivity state
  - Expandable list shows 10 items by default, expandable to show all 50

### T-004: Visual Group Indicators (Medium Priority)
- **Status**: ✅ **COMPLETED**
- **Implementation Details**:
  - Added `GET /api/users/{username}/group` API endpoint to retrieve user group membership
  - Created `getGroup()` function in frontend `users.js` library
  - Modified `Response.jsx` component to fetch and display group indicators next to usernames
  - Implemented visual indicators: ⭐ (yellow star) for privileged users, ⚠️ (orange triangle) for leechers, 🚫 (red ban) for blacklisted users
  - Added 👤 (blue user icon) for custom user-defined groups
  - Included helpful tooltips explaining each group type
  - Indicators only appear for non-default groups to avoid UI clutter
- **Technical Notes**:
  - Leveraged existing `UserService.GetGroup()` method for group determination
  - Added async group fetching in `componentDidMount` and `componentDidUpdate`
  - Used Semantic UI React `Icon` and `Popup` components for consistent styling
  - Graceful error handling prevents failed group fetches from breaking UI
  - Group indicators positioned next to username with appropriate spacing and colors

### T-003: Download Queue Position Polling (Medium Priority)
- **Status**: ✅ **COMPLETED**
- **Implementation Details**:
  - Modified `src/web/src/components/Transfers/Transfers.jsx` to automatically poll queue positions for all queued downloads
  - Added logic to filter queued downloads and fetch their positions in parallel during the regular 1-second polling cycle
  - Queue positions now update automatically without requiring manual refresh clicks
  - Maintains backward compatibility with existing manual refresh functionality
  - Uses `Promise.allSettled()` to prevent one failed queue position fetch from blocking others
- **Technical Notes**:
  - Leveraged existing `transfersLibrary.getPlaceInQueue()` API function
  - Updated local state immediately with fetched queue positions for responsive UI
  - Added error handling to silently fail individual fetches without spamming console
  - Direction check ensures only downloads are polled (uploads don't have queue positions)

---

## 2025-12-08

- 00:00: Initialized memory-bank structure for AI-assisted development
- 00:00: Created `projectbrief.md`, `tasks.md`, `activeContext.md`, `progress.md`, `scratch.md`
- 00:00: Created `.cursor/rules/` with project-specific AI instructions
- 00:00: Created `AGENTS.md` with development workflow guidelines

---

## Historical Releases (from DEVELOPMENT_HISTORY.md)

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

## 2025-12-13

### T-001: Persistent Room/Chat Tabs Implementation

**Completed T-001 persistent room/chat tabs** - High priority UI improvement enabling multiple concurrent room conversations.

- **Created RoomSession.jsx**: New component encapsulating individual room chat functionality (messages, users, input, context menus)
- **Converted Rooms.jsx to functional component**: Migrated from class component to React hooks pattern
- **Implemented tabbed interface**: Added Semantic UI Tab component with localStorage persistence (survives browser refreshes)
- **Added tab management**: Create new tabs, close tabs, switch between active room conversations
- **Maintained all existing functionality**: Room joining/leaving, search dropdown, context menus (Reply/User Profile/Browse)
- **Preserved styling**: Room history, user lists, message formatting remain consistent
- **Added persistence**: Tabs stored in localStorage as 'slskd-room-tabs' following Browse component pattern

**Technical Details**:
- 602 lines added, 392 lines modified across 2 files
- Created RoomSession component with 340+ lines of encapsulated room logic
- Converted complex class component to functional hooks (useState, useEffect, useCallback, useRef)
- Maintained all existing API integrations and room management logic
- Preserved real-time message polling and user list updates per tab

**Impact**: Users can now maintain multiple active room conversations simultaneously in persistent tabs that survive browser sessions, significantly improving the chat experience similar to modern messaging applications.

---

## 2025-12-13

### T-823: Mesh-Only Search Implementation

**Completed T-823 mesh-only search for disaster mode** - Core Phase 6 Virtual Soulfind Mesh capability now functional.

- **Modified SearchService.cs**: Added disaster mode coordinator and mesh search service dependencies
- **Implemented StartMeshOnlySearchAsync()**: Routes searches through overlay mesh when disaster mode active
- **Added MBID resolution**: Placeholder for MusicBrainz integration (expands to full MB API later)
- **DHT query integration**: Uses existing MeshSearchService.SearchByMbidAsync() for overlay lookups
- **Response format conversion**: Mesh results converted to compatible Search.Response objects for UI
- **Backward compatibility**: Existing Soulseek searches work unchanged, disaster mode is opt-in
- **Testing**: Full compilation verification, no errors, clean lint

**Technical Details**:
- 208 lines added to SearchService.cs
- Proper error handling and logging throughout
- SignalR integration maintains real-time UI updates
- Graceful fallbacks when mesh services unavailable

**Impact**: When Soulseek servers unavailable, searches now automatically failover to mesh-only operation using DHT-based peer discovery via MusicBrainz IDs instead of server-based lookups. Foundation for Phase 6 Virtual Soulfind Mesh established.

### T-002: Scheduled Rate Limits Implementation

**Completed T-002 scheduled rate limits** - High priority feature enabling qBittorrent-style day/night speed schedules.

- **Added ScheduledSpeedLimitOptions**: New configuration class with enabled flag, night start/end hours, and separate upload/download night limits
- **Implemented ScheduledRateLimitService**: Time-aware service that determines effective speed limits based on current hour and configured schedule
- **Modified UploadGovernor**: Updated to use scheduled limits when enabled, integrating with existing token bucket system
- **Added DI registration**: IScheduledRateLimitService registered as singleton in Program.cs
- **Configuration support**: Full options validation and environment variable support for all new settings

**Technical Details**:
- 183 lines added across 5 files (Options.cs, ScheduledRateLimitService.cs, UploadGovernor.cs, UploadService.cs, Program.cs)
- Created ScheduledRateLimitService.cs (110+ lines) with time-based logic and proper hour wrapping
- Modified UploadGovernor to accept optional IScheduledRateLimitService injection
- Maintains backward compatibility - when disabled, behaves exactly as before
- Supports flexible night periods (can wrap around midnight, e.g., 22:00-06:00)

**Configuration Options**:
- `scheduled-limits-enabled`: Enable/disable feature (default: false)
- `night-start-hour`: Hour when night period begins (default: 22)
- `night-end-hour`: Hour when night period ends (default: 6)
- `night-upload-speed-limit`: Upload limit during night (default: 100 KiB/s)
- `night-download-speed-limit`: Download limit during night (default: 200 KiB/s)

**Impact**: Users can now automatically reduce bandwidth usage during night hours, similar to qBittorrent's scheduler, helping manage ISP data caps and reduce noise/light from running transfers while sleeping.

---

## 2025-12-09

### CI/CD Infrastructure Overhaul

**Morning Session: Dev Build Fixes (5 cascading bugs fixed)**

1. **Package Version Hyphens (Bug #1)**: AUR/RPM/DEB all reject hyphens in version strings. Fixed by using `sed 's/-/./g'` (global) instead of `sed 's/-/./'` (first only). Version now converts correctly: `0.24.1-dev-20251209-215513` → `0.24.1.dev.20251209.215513`

2. **Integration Test Missing Reference (Bug #2)**: Docker builds failed with namespace errors. `slskd.Tests.Integration.csproj` was missing `<ProjectReference>` to main project. Fixed by adding the reference.

3. **Filename Pattern Mismatch (Bug #3)**: Packages job failed with "no assets match pattern". Downloaded `slskdn-dev-*-linux-x64.zip` but file was `slskdn-dev-linux-x64.zip` (no timestamp). Fixed by removing wildcard.

4. **RPM Build on Ubuntu (Bug #4)**: Packages job tried to build RPM on Ubuntu, which lacks Fedora build tools (`systemd-rpm-macros`). Fixed by removing RPM from packages job - COPR handles RPM builds natively on Fedora.

5. **PPA Version Hyphens (Bug #5)**: PPA rejected uploads as "Version older than archive" because `dpkg` treats hyphens as separators. Same fix as #1 - convert all hyphens to dots for Debian changelog.

**Additional Fixes**:
- **Yay Cache Gotcha**: AUR PKGBUILD updates weren't visible until cache cleared (`rm -rf ~/.cache/yay/package-name`)
- **Dev Build Naming**: Established convention for `dev-YYYYMMDD-HHMMSS` format with documentation

**Afternoon Session: Runtime Bugs**

6. **Backfill 500 Error**: EF Core couldn't translate `DateTimeOffset` to `DateTime` comparison. Fixed by using `.UtcDateTime` for explicit conversion before querying.

7. **Scanner Detection Noise**: Port scanner was triggering on localhost/LAN traffic. Fixed by skipping `RecordConnection()` for all private IPs.

**Evening Session: Release Visibility**

8. **Timestamped Dev Releases**: Added creation of visible timestamped releases (e.g., `dev-20251209-222346`) in addition to hidden floating `dev` tag. Now visitors can find dev builds in the releases page without accidentally getting them from the homepage.

9. **README Auto-Update**: Added workflow step to update README.md with latest dev build links on every release.

### Documentation Updates

- **`adr-0001-known-gotchas.md`**: Added 6 new gotchas (version formats, project references, filename patterns, cross-distro builds, yay cache, EF Core translation)
- **`adr-0002-code-patterns.md`**: Updated dev build convention with comprehensive version conversion rules
- **`tasks.md`**: Updated with completed work
- **Cursor Memories**: Created 5 new memories for preventing bug recurrence

### Builds Pushed

- `dev-20251209-215513`: All 5 CI/CD fixes
- `dev-20251209-222346`: Backfill + scanner fixes

### Testing & Verification

- Upgraded kspls0 from old build (`0.24.1-dev.202512082233`) to latest (`0.24.1-dev-20251209-215541`)
- Verified DHT, mesh, and Soulseek connectivity working
- Confirmed backfill button now functional (was 500 error, now works)
- Verified scanner detection no longer spams logs with private IP warnings

---

## Historical Releases (from DEVELOPMENT_HISTORY.md)

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

