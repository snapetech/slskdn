# Tag Naming Convention - Quick Reference

## Current Tag History

**Latest stable release:** `0.24.1-slskdn.34`

## Future Releases

### Dev Builds (Experimental)

**Trigger tag:**
```bash
VERSION="0.24.1.dev.$(date -u +%Y%m%d.%H%M%S)"
git tag "build-dev-${VERSION}"
git push origin "build-dev-${VERSION}"
```

**Example:** `build-dev-0.24.1.dev.20251213.203634`

**Release tag:** Same as trigger (no dual tag)

**Publishes to:**
- `slskdn-dev` (AUR)
- `slskdn/slskdn-dev` (COPR)
- `ghcr.io/snapetech/slskdn:dev-latest`
- PPA, Chocolatey pre-release

---

### Main Builds (Stable)

**Trigger tag:**
```bash
# Increment the BUILD number from last release (was 34, now 35)
VERSION="0.24.1-slskdn.35"
git tag "build-main-${VERSION}"
git push origin "build-main-${VERSION}"
```

**Example trigger:** `build-main-0.24.1-slskdn.35`  
**Example release:** `0.24.1-slskdn.35` ← **Backward compatible!**

**Release tag:** VERSION without `build-` prefix (maintains history)

**Publishes to:**
- `slskdn` (AUR source)
- `slskdn-bin` (AUR binary)
- `slskdn/slskdn` (COPR)
- `ghcr.io/snapetech/slskdn:latest`
- PPA, Chocolatey stable

---

## Version Format Breakdown

### Main Release Format

```
0.24.1-slskdn.35
│ │  │    │    │
│ │  │    │    └─ BUILD number (increment with each release)
│ │  │    └────── Fork identifier
│ │  └─────────── PATCH version
│ └────────────── MINOR version
└──────────────── MAJOR version
```

**Incrementing:**
- `0.24.1-slskdn.34` → `0.24.1-slskdn.35` (normal patch release)
- `0.24.1-slskdn.35` → `0.24.2-slskdn.1` (upstream merge, reset BUILD)
- `0.24.2-slskdn.50` → `0.25.0-slskdn.1` (major feature, reset BUILD)

### Dev Release Format

```
0.24.1.dev.20251213.203634
│ │  │     │       │
│ │  │     │       └─ HHMMSS (time)
│ │  │     └───────── YYYYMMDD (date)
│ │  └─────────────── Fork patch
│ └────────────────── Fork minor
└───────────────────── Fork major
```

**Automatically timestamped** - no manual incrementing needed.

---

## Tag History Compatibility

### Before (Manual releases)
```
0.24.1-slskdn.32
0.24.1-slskdn.33
0.24.1-slskdn.34  ← last manual release
```

### After (New system)
```
0.24.1-slskdn.34  ← last manual release
0.24.1-slskdn.35  ← first build-main-* release (still looks the same!)
0.24.1-slskdn.36
```

**Users see:** No change in release tag format  
**Admins see:** Explicit `build-main-*` trigger tags in git tag list

---

## Quick Commands

### Check latest release
```bash
git tag --sort=-version:refname | grep "^0\." | head -1
```

### Trigger next dev build
```bash
VERSION="0.24.1.dev.$(date -u +%Y%m%d.%H%M%S)"
git tag "build-dev-${VERSION}" && git push origin "build-dev-${VERSION}"
```

### Trigger next main build
```bash
# Get last build number
LAST=$(git tag --sort=-version:refname | grep "^0\.24\.1-slskdn\." | head -1)
# Manually increment (e.g., 34 → 35)
VERSION="0.24.1-slskdn.35"
git tag "build-main-${VERSION}" && git push origin "build-main-${VERSION}"
```

---

## Where Tags Appear

### GitHub Releases Page
- **Dev:** `build-dev-0.24.1.dev.20251213.203634` (prerelease)
- **Main:** `0.24.1-slskdn.35` (release) ← traditional format

### Git Tag List
```bash
git tag | tail -5
build-dev-0.24.1.dev.20251213.203634
build-main-0.24.1-slskdn.35
0.24.1-slskdn.35
```

### Package Managers
- AUR: `pkgver=0.24.1.slskdn.35` (dots not hyphens)
- COPR: `Version: 0.24.1.slskdn.35`
- PPA: `slskdn (0.24.1.slskdn.35-1ppa...)`
- Docker: `ghcr.io/snapetech/slskdn:latest`, `:0.24.1-slskdn.35`

---

## Summary

✅ **Backward compatible** - Release tags match existing format  
✅ **Explicit builds** - `build-*` prefix prevents accidents  
✅ **Clear separation** - Dev vs main channels isolated  
✅ **Timestamp dev builds** - No manual version management for experiments  
✅ **Incremental main builds** - Continue existing `.BUILD` sequence

