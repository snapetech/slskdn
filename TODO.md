# slskdn TODO

## SongID & Discovery Graph Parity
- [x] Improve mix detection heuristics so longer gaps, overlapping chapters, and comments still form actionable mix clusters with segment plans and ranked acquisition options.
- [x] Expand the SongID panel so queue/perturbation progress, synthetic lane context, and mix/candidate fan-out flows are visible without leaving the panel.
- [x] Verify that SongID download actions (track, album, discography) are ordered by identity -> quality -> Byzantine consensus while surface-level forensic badges stay unobtrusive.
- [x] Enrich the Discovery Graph atlas and drawer with deeper provenance/evidence breakdowns, more seed types, and the semantic zoom stack so every graph action can spawn downloads or queue-nearby work.
- [x] Harden the SongID queue: persisted queue position/worker slot, restart recovery, and `songid-max-concurrent-runs` throttling remain reliable in production. The queue is intentionally bounded instead of literally unlimited to preserve host memory and network-health guarantees.

Completed 2026-05-01. See `docs/songid-discovery.md` and `docs/dev/SONGID_INTEGRATION_MAP.md`; future Essentia, cover-similarity, and embedding work is treated as research scope rather than missing baseline parity.

## ✅ Persistent Tabbed Interface for Rooms and Chat - COMPLETED

**Status**: ✅ **COMPLETED** (2026-01-27)

**Implementation**: Tabbed interface implemented for both Rooms and Chat, following the Browse pattern.

### Changes Implemented
1. **Tab-based UI** - ✅ Rooms already had tabs; Chat converted to tabs with `+` button to open new conversations
2. **Persistent state** - ✅ Tabs saved to localStorage (`slskd-room-tabs`, `slskd-chat-tabs`)
3. **Survive crashes/restarts** - ✅ Tabs restore on page reload
4. **Explicit close** - ✅ Tabs stay open until user clicks X button

### Implementation Details
- **Rooms**: Already implemented with tabs (`Rooms.jsx` uses `RoomSession.jsx` per tab)
- **Chat**: Converted from class component to functional component with hooks
  - Created `ChatSession.jsx` component (similar to `RoomSession.jsx`)
  - Tabs persist in localStorage (`slskd-chat-tabs`)
  - Supports multiple concurrent conversations
  - Each tab maintains its own conversation state

### Related Files
- Browse tabs: `src/web/src/components/Browse/Browse.jsx`
- Browse session: `src/web/src/components/Browse/BrowseSession.jsx`
- Rooms tabs: `src/web/src/components/Rooms/Rooms.jsx`
- Rooms session: `src/web/src/components/Rooms/RoomSession.jsx`
- Chat tabs: `src/web/src/components/Chat/Chat.jsx` (converted)
- Chat session: `src/web/src/components/Chat/ChatSession.jsx` (new)

---

## Additional Packaging & Distribution Channels

Expand slskdn availability beyond current channels (AUR, COPR, PPA, Docker/GHCR).

### ✅ Currently Available
- [x] **AUR** (Arch Linux) - `slskdn-bin`, `slskdn`
- [x] **COPR** (Fedora/RHEL/openSUSE) - RPM packages
- [x] **Ubuntu PPA** - Debian packages
- [x] **Docker** - `ghcr.io/snapetech/slskdn`
- [x] **GitHub Releases** - Windows, macOS, Linux binaries

### 🔴 High Priority (Large User Base)
- [x] **Unraid Community Apps** - Popular NAS/homelab OS ✅
  - Template at `packaging/unraid/slskdn.xml`
  - Users can add repo manually, pending official CA submission
- [x] **TrueNAS SCALE Apps** - Growing NAS platform ✅
  - Helm chart at `packaging/truenas-scale/charts/`
- [x] **Synology Package Center** - Huge NAS market share ✅
  - SPK format at `packaging/synology-spk/`
- [x] **Homebrew** (macOS) - Standard macOS package manager ✅
  - Formula at `packaging/homebrew/Formula/slskdn.rb`
- [x] **Flatpak** (Linux) - Universal Linux packaging ✅
  - Manifest at `packaging/flatpak/io.github.slskd.slskdn.yml`

### 🟡 Medium Priority
- [x] **Snap** (Ubuntu) - Canonical's universal format ✅
  - snapcraft.yaml at `packaging/snap/`
- [x] **NixOS/Nix** - Growing declarative Linux distro ✅
  - flake.nix at root
- [x] **Portainer Templates** - Docker GUI users
  - JSON template, easy to add
- [x] **QNAP App Center** - Another NAS vendor
  - QPKG format
- [x] **Helm Charts** (Kubernetes) - K8s deployments ✅
  - Generic chart at `packaging/helm/slskdn/` (T-014). For enterprise/homelab K8s users.
- [x] **OpenMediaVault Plugins** - Debian-based NAS
  - OMV plugin format

### 🟢 Low Priority / Nice to Have
- [x] **Chocolatey** (Windows) - Windows package manager ✅
  - nuspec at `packaging/chocolatey/slskdn.nuspec`
- [x] **Winget** (Windows) - Microsoft's package manager ✅
  - Manifests at `packaging/winget/`
- [x] **Scoop** (Windows) - Alternative Windows PM
- [x] **AppImage** (Linux) - Portable Linux apps
- [x] **FreeBSD Ports** - BSD systems
- [x] **Proxmox LXC Templates** - Proxmox users ✅
  - `packaging/proxmox-lxc/`: README, slskdn.conf.example, setup-inside-ct.sh (Debian 12/Ubuntu 22.04, .NET 8, GitHub zip, systemd)

### Implementation Notes
- Most NAS platforms use Docker underneath - leverage existing image
- Synology/QNAP may need native builds for older ARM NAS units
- Flatpak/Snap require sandboxing considerations
- Homebrew cask is easiest for macOS (just downloads release binary)
