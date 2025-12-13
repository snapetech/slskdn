# CI Package Publishing - Complete Setup

## Status: Ready for Testing ✅

All package publishing channels have been added to `build-on-tag.yml` for **BOTH dev and main** channels.

## What Was Added

### Dev Channel (`build-dev-*` tags)
- ✅ **AUR** - `slskdn-dev`
- ✅ **COPR** - `slskdn/slskdn-dev`
- ✅ **PPA** - `ppa:keefshape/slskdn` (slskdn-dev package)
- ✅ **Docker** - `ghcr.io/snapetech/slskdn:dev-latest`
- ✅ **Chocolatey** - Pre-release channel

### Main Channel (`build-main-*` tags)
- ✅ **AUR** - `slskdn` (source) + `slskdn-bin` (binary)
- ✅ **COPR** - `slskdn/slskdn` (binary via SRPM)
- ✅ **PPA** - `ppa:keefshape/slskdn` (slskdn package, source-only)
- ✅ **Docker** - `ghcr.io/snapetech/slskdn:latest`
- ✅ **Chocolatey** - Stable release channel (binary)

### Critical Fixes from Old Workflow Patterns

1. **Archive Format**:
   - Changed to `.zip` for ALL platforms (Linux, Windows, macOS)
   - Old workflow used `.zip` everywhere; new workflow was using `.tar.gz` for Linux
   - This was causing COPR/PPA/AUR downloads to fail

2. **COPR**:
   - Downloads `.zip` not `.tar.gz`
   - Exact comment formatting: `# Copy the built zip to SOURCES`
   - Version conversion: `sed 's/-/./g'` for ALL hyphens

3. **PPA**:
   - Downloads `.zip` and extracts with `unzip -d publish-linux-x64`
   - Creates full directory structure BEFORE extraction
   - Proper debian/control modifications for `-dev` package
   - Description update for slskdn-dev

4. **AUR**:
   - Downloads `.zip` file
   - Generates `.SRCINFO` in Docker container
   - Fixes ownership after Docker (root changes permissions)

5. **Docker**:
   - Downloads from GitHub release (not artifacts)
   - Builds for `linux/amd64` and `linux/arm64`
   - Tags: `dev-VERSION` and `dev-latest`

6. **Chocolatey**:
   - Windows-only job
   - Downloads `.zip` (already correct format)
   - Pushes to pre-release channel

## Workflow Trigger

**Currently DISABLED** to prevent accidental builds during testing:

```yaml
on:
  # push:
  #   tags:
  #     - 'build-dev-*'
  #     - 'build-main-*'
  workflow_dispatch:
```

## How to Test

### Local Build Test First
```bash
cd /home/keith/Documents/Code/slskdn
./src/slskd/bin/Release/net8.0/slskd --no-logo --http --http-port=5001 --no-https
# Open http://localhost:5001 in browser
# Verify new security features work
```

### Trigger Dev Build (When Ready)
```bash
# Set version
VERSION="0.24.1.dev.$(date -u +%Y%m%d.%H%M%S)"

# Create and push tag
git tag "build-dev-${VERSION}"
git push origin "build-dev-${VERSION}"
```

### Trigger Main Build (Stable Release)
```bash
# Set version (semantic versioning)
VERSION="0.25.0"

# Create and push tag
git tag "build-main-${VERSION}"
git push origin "build-main-${VERSION}"
```

### What Happens
1. **Parse** - Extracts channel (`dev`) and version
2. **Build** - Frontend (npm) + uploads web-content artifact
3. **Publish** - 6 platform binaries (all as `.zip`)
   - `linux-x64`, `linux-musl-x64`, `linux-arm64`
   - `osx-x64`, `osx-arm64`
   - `win-x64`
4. **Release-Dev** - Creates GitHub release with all `.zip` files
5. **Package Publishing** (5 parallel jobs):
   - `aur-dev` - Publishes to AUR
   - `copr-dev` - Builds SRPM and uploads to COPR
   - `ppa-dev` - Builds source package and uploads to PPA
   - `docker-dev` - Builds and pushes Docker images
   - `chocolatey-dev` - Packs and pushes to Chocolatey

## Secrets Required

| Secret | Used By | Purpose |
|--------|---------|---------|
| `AUR_SSH_KEY` | AUR | SSH key for `aur@aur.archlinux.org` |
| `COPR_LOGIN` | COPR | COPR API login |
| `COPR_TOKEN` | COPR | COPR API token |
| `GPG_PRIVATE_KEY` | PPA | GPG key for signing Debian packages |
| `GITHUB_TOKEN` | All | Automatic (no setup needed) |
| `CHOCO_API_KEY` | Chocolatey | Chocolatey API key |

**All jobs gracefully skip if secrets are not configured.**

## Installation Commands (After Publishing)

### Dev Channel
```bash
# Arch Linux (AUR)
yay -S slskdn-dev

# Fedora/RHEL (COPR)
sudo dnf copr enable slskdn/slskdn-dev
sudo dnf install slskdn-dev

# Ubuntu/Debian (PPA)
sudo add-apt-repository ppa:keefshape/slskdn
sudo apt update
sudo apt install slskdn-dev

# Docker
docker pull ghcr.io/snapetech/slskdn:dev-latest

# Windows (Chocolatey)
choco install slskdn --pre
```

### Main Channel (Stable)
```bash
# Arch Linux (AUR - source build)
yay -S slskdn

# Arch Linux (AUR - binary)
yay -S slskdn-bin

# Fedora/RHEL (COPR)
sudo dnf copr enable slskdn/slskdn
sudo dnf install slskdn

# Ubuntu/Debian (PPA)
sudo add-apt-repository ppa:keefshape/slskdn
sudo apt update
sudo apt install slskdn

# Docker
docker pull ghcr.io/snapetech/slskdn:latest

# Windows (Chocolatey)
choco install slskdn
```

## Next Steps

1. **Test locally** - Verify security features work
2. **Enable trigger** - Uncomment `push.tags` in workflow
3. **Create build tag** - Push `build-dev-VERSION` tag
4. **Verify packages** - Check each channel after ~30-60 min
5. **Test installations** - Install from each package manager

## Differences from Main Channel

| Feature | Dev Channel | Main Channel |
|---------|-------------|--------------|
| Package names | `slskdn-dev` | `slskdn`, `slskdn-bin` (AUR) |
| AUR packages | 1 (binary only) | 2 (source + binary) |
| Docker tags | `dev-latest`, `dev-VERSION` | `latest`, `VERSION` |
| COPR project | `slskdn/slskdn-dev` | `slskdn/slskdn` |
| Release type | Pre-release | Stable release |
| Trigger | `build-dev-*` tags | `build-main-*` tags |
| Source available | Binary only | Source + Binary (AUR) |

## Gotchas Documented

Added to `memory-bank/decisions/adr-0001-known-gotchas.md`:
- Archive format mismatch (.zip vs .tar.gz)
- COPR/PPA expecting .zip files
- Version format conversions (hyphens to dots)
- PPA directory structure requirements

