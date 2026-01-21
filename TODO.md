# slskdn TODO

## ✅ Persistent Tabbed Interface for Rooms and Chat - DOCUMENTED

**Status**: Documented for future implementation. Current implementation uses standard navigation.

**Future Enhancement**: Implement a tabbed interface for Rooms and Chat, similar to how Browse currently works:

### Changes (Future)
1. **Tab-based UI** - Replace the horizontal search bar with small tabs + a `+` button to open new searches
2. **Persistent state** - Remember opened rooms/chats in localStorage (like Browse sessions)
3. **Survive crashes/restarts** - Tabs should restore on page reload or app restart
4. **Explicit close** - Rooms/chats stay open until user explicitly closes them (X button on tab)

### Implementation Notes
- Can reuse the tabbed browsing logic from `Browse.jsx`/`BrowseSession.jsx`
- Use localStorage with LRU cache cleanup (already implemented for Browse)
- Consider using `lz-string` compression for larger chat histories

### Related
- Browse tabs implementation: `src/web/src/components/Browse/Browse.jsx`
- Browse session persistence: `src/web/src/components/Browse/BrowseSession.jsx`

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
- [ ] **Portainer Templates** - Docker GUI users
  - JSON template, easy to add
- [ ] **QNAP App Center** - Another NAS vendor
  - QPKG format
- [ ] **Helm Charts** (Kubernetes) - K8s deployments
  - For enterprise/homelab K8s users
- [ ] **OpenMediaVault Plugins** - Debian-based NAS
  - OMV plugin format

### 🟢 Low Priority / Nice to Have
- [x] **Chocolatey** (Windows) - Windows package manager ✅
  - nuspec at `packaging/chocolatey/slskdn.nuspec`
- [x] **Winget** (Windows) - Microsoft's package manager ✅
  - Manifests at `packaging/winget/`
- [ ] **Scoop** (Windows) - Alternative Windows PM
- [ ] **AppImage** (Linux) - Portable Linux apps
- [ ] **FreeBSD Ports** - BSD systems
- [ ] **Proxmox LXC Templates** - Proxmox users

### Implementation Notes
- Most NAS platforms use Docker underneath - leverage existing image
- Synology/QNAP may need native builds for older ARM NAS units
- Flatpak/Snap require sandboxing considerations
- Homebrew cask is easiest for macOS (just downloads release binary)
