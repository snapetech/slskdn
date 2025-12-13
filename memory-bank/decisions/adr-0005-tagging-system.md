# ADR-0005: Tag-Based Build and Release System

**Status:** Active  
**Date:** 2025-12-13  
**Context:** Multi-channel CI/CD for dev and stable releases

---

## Decision

We use **explicit build tags** to trigger CI/CD builds and control which channels packages are published to.

### Tag Format

```
build-<channel>-<version>
```

### Channels

#### Dev Channel: `build-dev-*`

**Purpose:** Experimental/unstable builds from feature branches

**Version Format:** `MAJOR.MINOR.PATCH.dev.YYYYMMDD.HHMMSS`
- Example: `0.24.1.dev.20251213.203634`
- Timestamp-based for rapid iteration

**Publishes to:**
- `slskdn-dev` (AUR - binary only)
- `slskdn/slskdn-dev` (COPR)
- `slskdn-dev` package (PPA)
- `ghcr.io/snapetech/slskdn:dev-latest` (Docker)
- `ghcr.io/snapetech/slskdn:dev-VERSION` (Docker)
- Chocolatey (pre-release with `--prerelease` flag)

**Usage:**
```bash
VERSION="0.24.1.dev.$(date -u +%Y%m%d.%H%M%S)"
git tag "build-dev-${VERSION}"
git push origin "build-dev-${VERSION}"
```

#### Main Channel: `build-main-*`

**Purpose:** Stable releases from main branch

**Version Format:** `MAJOR.MINOR.PATCH-slskdn.BUILD`
- Example: `0.24.1-slskdn.35`, `0.25.0-slskdn.1`
- Matches existing release tag history

**Trigger Tag:** `build-main-0.24.1-slskdn.35` (explicit build intent)  
**Release Tag:** `0.24.1-slskdn.35` (backward compatible, matches history)

The workflow creates the GitHub release under the traditional tag format (without `build-` prefix) to maintain backward compatibility with existing tags like `0.24.1-slskdn.34`.

**Publishes to:**
- `slskdn` (AUR - **source** build from GitHub tarball)
- `slskdn-bin` (AUR - **binary** build)
- `slskdn/slskdn` (COPR)
- `slskdn` package (PPA)
- `ghcr.io/snapetech/slskdn:latest` (Docker)
- `ghcr.io/snapetech/slskdn:VERSION` (Docker)
- Chocolatey (stable release, no `--prerelease` flag)

**Usage:**
```bash
# Next increment from 0.24.1-slskdn.34
VERSION="0.24.1-slskdn.35"
git tag "build-main-${VERSION}"
git push origin "build-main-${VERSION}"
# Creates release under: 0.24.1-slskdn.35 (without build- prefix)
```

## Workflow Logic

The CI workflow (`build-on-tag.yml`) parses the tag:

1. **Parse step** extracts `channel` and `version`
   - `build-dev-0.24.1.dev.20251213` → channel=`dev`, version=`0.24.1.dev.20251213`
   - `build-main-0.24.1-slskdn.35` → channel=`main`, version=`0.24.1-slskdn.35`

2. **Build** runs for all channels (frontend + 6 platform binaries as `.zip`)

3. **Release creation:**
   - **Dev:** Creates release under same tag (`build-dev-0.24.1.dev.20251213`)
   - **Main:** Creates release under version tag (`0.24.1-slskdn.35`) for backward compatibility

4. **Package jobs** use `if: needs.parse.outputs.channel == '<channel>'`
5. Only matching channel jobs execute; others are skipped

### Channel Isolation

- Dev jobs: `if: needs.parse.outputs.channel == 'dev'`
- Main jobs: `if: needs.parse.outputs.channel == 'main'`

**Result:** Dev builds NEVER publish to main repos, and vice versa.

## Version Conversion

Package managers have version restrictions (no hyphens allowed):

**Rule:** All hyphens → dots via `sed 's/-/./g'`

Examples:
- `0.24.1.dev.20251213.203634` → `0.24.1.dev.20251213.203634` (no change)
- `0.24.1-dev-20251213-203634` → `0.24.1.dev.20251213.203634` (converted)

## Rationale

### Why `build-*` prefix?

**Explicit intent** - Prevents accidental releases. Must explicitly tag with `build-` to trigger CI/CD.

### Why channel in tag?

**Safety** - Impossible to accidentally publish dev to production channels. Channel is encoded in the tag itself, not inferred from branch.

### Why timestamp for dev?

**Rapid iteration** - Multiple builds per day without version conflicts. Clear chronological ordering.

### Why semantic versioning for main?

**Backward compatibility** - Continues the existing `0.24.1-slskdn.BUILD` format. Matches the project's tag history where `.BUILD` increments with each release.

### Dual tag system for main?

**Explicit triggers + traditional releases:**
- **Trigger tag** (`build-main-VERSION`) - Makes build intent explicit, prevents accidents
- **Release tag** (`VERSION`) - Maintains backward compatibility, clean release history
- Users see traditional tags (`0.24.1-slskdn.35`) in releases
- Admins use explicit tags (`build-main-0.24.1-slskdn.35`) to trigger builds

### Why both source and binary for main AUR?

**User choice:**
- **Source** (`slskdn`) - Verifiable builds, native compilation, audit transparency
- **Binary** (`slskdn-bin`) - Fast installation, no build dependencies

Dev uses binary-only (`slskdn-dev`) for faster iteration.

## Consequences

### Positive

- ✅ **No accidental releases** - Must explicitly create `build-*` tag
- ✅ **Channel isolation** - Dev can't leak to main repos
- ✅ **Clear audit trail** - Every build has git tag pointing to exact commit
- ✅ **Version control** - Developer controls versions explicitly (no auto-bumping)
- ✅ **Safe parallel development** - Multiple feature branches can have dev builds

### Negative

- ❌ **Manual tagging required** - Can't auto-release on merge (but this is a feature, not a bug)
- ❌ **Tag cleanup needed** - Old `build-dev-*` tags accumulate (can be pruned periodically)

## Examples

### Trigger Dev Build

```bash
# From experimental/multi-source-swarm branch
VERSION="0.24.1.dev.$(date -u +%Y%m%d.%H%M%S)"
git tag "build-dev-${VERSION}"
git push origin "build-dev-${VERSION}"
# Publishes to: slskdn-dev, ghcr.io/snapetech/slskdn:dev-latest, etc.
```

### Trigger Main Build

```bash
# From master branch (note: it's "master" not "main")
# Continue existing version sequence (last was 0.24.1-slskdn.34)
VERSION="0.24.1-slskdn.35"
git tag "build-main-${VERSION}"
git push origin "build-main-${VERSION}"
# Publishes to: slskdn, slskdn-bin, ghcr.io/snapetech/slskdn:latest, etc.
# Release appears under: 0.24.1-slskdn.35 (traditional format)
```

## Related Files

- `.github/workflows/build-on-tag.yml` - Main CI/CD workflow
- `CI_PACKAGE_PUBLISHING.md` - Package publishing documentation
- `BUILD_RELEASE.md` - Build instructions
- `packaging/aur/PKGBUILD` - AUR source package
- `packaging/aur/PKGBUILD-bin` - AUR binary package
- `packaging/aur/PKGBUILD-dev` - AUR dev package

## Migration Notes

Old system used:
- Auto-builds on push to `experimental/multi-source-swarm`
- Separate `dev-release.yml` workflow
- No channel isolation

New system:
- Explicit `build-*` tags only
- Single `build-on-tag.yml` for both channels
- Channel encoded in tag for safety

