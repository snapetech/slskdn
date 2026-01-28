# Dev Build Process Map

## Overview
This document maps out the complete dev build process for slskdn, including versioning, checksums, and all package manager channels.

## Trigger Methods

### Method 1: `build-on-tag.yml` (Recommended)
**Tag Format**: `build-dev-<VERSION>`
- Example: `build-dev-0.24.1.dev.91769029946`
- Version format: `0.24.1.dev.<EPOCH>` (dots for package managers)
- Creates release under the same tag name

### Method 2: `dev-release.yml` (Legacy)
**Tag Format**: `dev-*`
- Automatically generates version: `0.24.1-dev-9<EPOCH>`
- EPOCH = Unix timestamp prefixed with '9' for lexicographic sorting
- Example: `0.24.1-dev-91769029946`

## Version Calculation

### Base Configuration
- **BASE_VERSION**: `0.24.1` (hardcoded in workflow)
- **EPOCH**: `date +%s` (Unix timestamp)
- **TIMESTAMP**: `9${EPOCH}` (prefixed for sorting)

### Version Formats by Context

| Context | Format | Example | Conversion |
|---------|--------|---------|------------|
| **.NET Build** | `0.24.1-dev-9EPOCH` | `0.24.1-dev-91769029946` | Hyphens (native) |
| **Arch/AUR** | `0.24.1.dev.9EPOCH` | `0.24.1.dev.91769029946` | All hyphens → dots |
| **RPM/COPR** | `0.24.1.dev.9EPOCH` | `0.24.1.dev.91769029946` | All hyphens → dots |
| **Debian/PPA** | `0.24.1.dev.9EPOCH` | `0.24.1.dev.91769029946` | All hyphens → dots |
| **Nix Flake** | `0.24.1-dev-9EPOCH` | `0.24.1-dev-91769029946` | Hyphens (kept) |
| **Chocolatey** | `0.24.1-dev-9EPOCH` | `0.24.1-dev-91769029946` | Dots → hyphens |
| **Winget** | `0.24.1-dev-9EPOCH` | `0.24.1-dev-91769029946` | Hyphens (kept) |
| **Homebrew** | `0.24.1-dev-9EPOCH` | `0.24.1-dev-91769029946` | Hyphens (kept) |
| **Docker** | `0.24.1-dev-9EPOCH` | `0.24.1-dev-91769029946` | Hyphens (kept) |

## Build Artifacts

### Platforms Built
1. **Linux x64** → `slskdn-dev-linux-x64.zip`
2. **Windows x64** → `slskdn-dev-win-x64.zip`
3. **macOS x64** (Intel) → `slskdn-dev-osx-x64.zip`
4. **macOS ARM64** (Apple Silicon) → `slskdn-dev-osx-arm64.zip`

### Build Process
1. Clean frontend build (`rm -rf build node_modules/.cache`)
2. Build frontend (`npm ci && npm run build`)
3. Build backend for each platform (`dotnet publish --self-contained`)
4. Copy frontend build to `wwwroot/` for each platform
5. Create zip archives
6. Verify `wwwroot` is present in all archives

## Checksums & Hashes

### Calculation Methods

| Package Manager | Hash Type | Format | Tool |
|----------------|-----------|--------|------|
| **AUR** | SHA256 | Hex (64 chars) | `sha256sum` |
| **COPR** | SHA256 | Hex (64 chars) | `sha256sum` |
| **PPA** | SHA256 | Hex (64 chars) | `sha256sum` |
| **Nix** | SHA256 | Base32 (52 chars) | `nix-hash --type sha256 --flat --base32` |
| **Chocolatey** | SHA256 | Hex (64 chars) | `Get-FileHash -Algorithm SHA256` |
| **Winget** | SHA256 | Hex (64 chars) | `Get-FileHash -Algorithm SHA256` |
| **Homebrew** | SHA256 | Hex (64 chars) | `sha256sum` |
| **Docker** | N/A | N/A | N/A (uses image digest) |

### Hash Updates Required

1. **AUR (PKGBUILD-dev)**: Uses `SKIP` for binary zip (changes each build)
2. **Nix (flake.nix)**: Updates `devSources.sha256` for all platforms
3. **Chocolatey**: Updates `chocolateyinstall.ps1` checksum
4. **Winget**: Updates `installer.yaml` InstallerSha256
5. **Homebrew**: Updates Formula SHA256 for all platforms

## Package Manager Channels

### 1. GitHub Releases
- **Tag**: Same as build tag (e.g., `build-dev-0.24.1.dev.91769029946`)
- **Assets**: All platform zip files
- **Release Notes**: Auto-generated with commit SHA, timestamp, features list

### 2. Arch Linux (AUR)
- **Package**: `slskdn-dev`
- **Files Updated**:
  - `PKGBUILD`: `pkgver`, `_commit`, release URL
  - `.SRCINFO`: Auto-generated from PKGBUILD
- **Version Format**: `0.24.1.dev.9EPOCH` (all hyphens → dots)
- **Commit SHA**: Short SHA from build
- **Release URL**: `https://github.com/snapetech/slskdn/releases/download/<TAG>/slskdn-dev-linux-x64.zip`

### 3. Fedora/RHEL (COPR)
- **Project**: `slskdn/slskdn-dev`
- **Files Updated**:
  - `slskdn-dev.spec`: Version field
- **Version Format**: `0.24.1.dev.9EPOCH` (all hyphens → dots)
- **Source**: Downloads zip from GitHub release
- **Build**: Creates SRPM, uploads to COPR

### 4. Ubuntu/Debian (PPA)
- **PPA**: `~keefshape/ubuntu/slskdn/`
- **Package**: `slskdn-dev`
- **Files Updated**:
  - `debian/changelog`: Version with PPA revision (`0.24.1.dev.9EPOCH-1ppa<YYYYMMDDHHMM>~jammy`)
  - `debian/control`: Package name, conflicts, replaces
- **Version Format**: `0.24.1.dev.9EPOCH` (all hyphens → dots)
- **GPG Key**: Required for signing (from `GPG_PRIVATE_KEY` secret)
- **Revision**: Timestamp-based (`date +%Y%m%d%H%M`)

### 5. Docker (GHCR)
- **Registry**: `ghcr.io/snapetech/slskdn`
- **Tags**:
  - `dev-<VERSION>` (e.g., `dev-0.24.1-dev-91769029946`)
  - `dev-latest` (always points to latest dev build)
- **Platforms**: `linux/amd64`, `linux/arm64`
- **Build Args**: `VERSION=<hyphenated-version>`

### 6. Nix Flake
- **File**: `flake.nix`
- **Branch**: `experimental/multi-source-swarm` (or `experimental/whatAmIThinking`)
- **Updates**:
  - `devVersion`: `"0.24.1-dev-9EPOCH"`
  - `devSources.<platform>.sha256`: Base32-encoded SHA256
  - `devSources.<platform>.url`: Release download URL
- **Hash Calculation**: `nix-hash --type sha256 --flat --base32 <file>`
- **Commit Message**: `chore(nix): update dev flake to <VERSION> [skip ci]`

### 7. Windows Package Managers

#### Chocolatey
- **Package**: `slskdn` (pre-release channel)
- **Files Updated**:
  - `slskdn.nuspec`: Version
  - `chocolateyinstall.ps1`: URL, checksum
- **Version Format**: `0.24.1-dev-9EPOCH` (dots → hyphens: `.dev.` → `-dev-`)
- **Checksum**: SHA256 hex
- **Channel**: Pre-release (`--prerelease` flag)

#### Winget
- **Manifests**: `packaging/winget/snapetech.slskdn-dev.*.yaml`
- **Branch**: `experimental/multi-source-swarm`
- **Files Updated**:
  - `installer.yaml`: PackageVersion, InstallerUrl, InstallerSha256
  - `version.yaml`: PackageVersion
- **Version Format**: `0.24.1-dev-9EPOCH` (hyphens kept)
- **Note**: Requires manual PR to `microsoft/winget-pkgs`

### 8. macOS Package Managers

#### Homebrew Tap
- **Tap**: `snapetech/homebrew-slskdn`
- **Formula**: `Formula/slskdn-dev.rb`
- **Files Updated**:
  - Formula file with version, URLs, SHA256 for all platforms
- **Version Format**: `0.24.1-dev-9EPOCH` (hyphens kept)
- **SHA256**: Calculated for Linux, macOS x64, macOS ARM64
- **URLs**: Platform-specific release download URLs

### 9. Snap
- **Channel**: `edge` (dev builds)
- **File**: `packaging/snap/snapcraft.yaml`
- **Updates**:
  - `version`: Dev version
  - `source`: Extracted zip directory
  - `grade`: `devel` (for dev builds)
- **Credentials**: `SNAPCRAFT_STORE_CREDENTIALS` secret

## Workflow Dependencies

### `build-on-tag.yml` Jobs (for `build-dev-*` tags)

1. **parse** → Extracts channel and version from tag
2. **build** → Builds frontend (single job)
3. **publish** → Builds backend for each platform (matrix strategy)
4. **release-dev** → Creates GitHub release with all artifacts
5. **aur-dev** → Updates and pushes to AUR
6. **copr-dev** → Builds and uploads to COPR
7. **ppa-dev** → Builds and uploads to PPA
8. **docker-dev** → Builds and pushes Docker images
9. **chocolatey-dev** → Updates and pushes to Chocolatey
10. **nix-dev** → Updates flake.nix and commits
11. **winget-dev** → Updates Winget manifests
12. **snap-dev** → Builds and publishes Snap
13. **homebrew-dev** → Updates Homebrew tap

### `dev-release.yml` Jobs (for `dev-*` tags)

1. **build** → Builds all platforms, creates artifacts
2. **release** → Creates GitHub release, updates README
3. **aur** → Updates AUR
4. **copr** → Updates COPR
5. **ppa** → Updates PPA
6. **docker** → Builds Docker images
7. **packages** → Builds .deb package
8. **nix** → Updates Nix flake
9. **winget** → Updates Winget manifests
10. **homebrew** → Updates Homebrew tap

## Required Secrets

| Secret | Used By | Purpose |
|--------|---------|---------|
| `AUR_SSH_KEY` | AUR jobs | SSH key for AUR push |
| `COPR_LOGIN` | COPR jobs | COPR username |
| `COPR_TOKEN` | COPR jobs | COPR API token |
| `GPG_PRIVATE_KEY` | PPA jobs | GPG key for package signing |
| `CHOCO_API_KEY` | Chocolatey jobs | Chocolatey API key |
| `TAP_GITHUB_TOKEN` | Homebrew jobs | GitHub token for tap repo |
| `SNAPCRAFT_STORE_CREDENTIALS` | Snap jobs | Snap store credentials |

## Branch Strategy

- **Dev builds**: From the current dev branch (e.g. `dev/40-fixes`). **The workflow file must use the actual branch name**—if the branch was renamed from `experimental/multi-source-swarm` to `dev/40-fixes`, update all refs in `build-on-tag.yml` (nix-dev, winget-dev, snap-dev, etc.).
- **Nix updates**: Commits to same branch (with `[skip ci]`)
- **Winget updates**: Commits to same branch (with `[skip ci]`)
- **README updates**: Commits to dev branch (dev-release.yml only)
- **Do not revert** `build-on-tag.yml` to an old commit; it wipes out accumulated fixes (see `memory-bank/decisions/adr-0001-known-gotchas.md` § Reverting entire workflow files).

## Version Iteration Example

### Current State (from flake.nix)
- **devVersion**: `0.24.1-dev-91769029946`
- **Timestamp**: `91769029946` (prefixed epoch)

### Next Dev Build
1. Create tag: `build-dev-0.24.1.dev.$(date +%s | sed 's/^/9/')`
   - Example: `build-dev-0.24.1.dev.91770000000`
2. Push tag: `git push origin build-dev-0.24.1.dev.91770000000`
3. Workflow automatically:
   - Builds all platforms
   - Calculates SHA256 hashes
   - Creates GitHub release
   - Updates all package managers with new version and hashes

## Key Files Updated

1. **flake.nix**: `devVersion` and `devSources.sha256` for all platforms
2. **packaging/aur/PKGBUILD-dev**: `pkgver`, `_commit`, release URL
3. **packaging/rpm/slskdn-dev.spec**: `Version` field
4. **packaging/debian/changelog**: Version with PPA revision
5. **packaging/chocolatey/slskdn.nuspec**: Version
6. **packaging/chocolatey/tools/chocolateyinstall.ps1**: URL, checksum
7. **packaging/winget/snapetech.slskdn-dev.*.yaml**: Version, URL, SHA256
8. **Formula/slskdn-dev.rb** (Homebrew tap): Version, URLs, SHA256

## Notes

- **Version Sorting**: Prefixing epoch with '9' ensures proper lexicographic sorting
- **Hash Formats**: Nix uses base32, all others use hex
- **Version Conversions**: Package managers have different requirements (dots vs hyphens)
- **Concurrent Builds**: Retry logic with exponential backoff for git pushes
- **Branch Protection**: Updates use `[skip ci]` to avoid triggering new builds
