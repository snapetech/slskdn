# Building and Releasing slskdN

## New Tag-Based Build System

**No builds happen automatically**. All builds require explicit tags.

### Tag Format

```
build-<channel>-<version>
```

- **Channel**: `dev` or `main`
- **Version**: 
  - Dev: `X.Y.Z.dev.YYYYMMDD.HHMMSS` (e.g., `0.24.1.dev.20251213.140454`)
  - Main: `X.Y.Z` (e.g., `0.24.2`)

### Quick Start

**Dev Build:**
```bash
# Create timestamp version
VERSION=$(date +0.24.1.dev.%Y%m%d.%H%M%S)

# Tag and push
git tag "build-dev-$VERSION"
git push origin "build-dev-$VERSION"
```

**Main Build:**
```bash
# Use semantic version
git tag build-main-0.24.2
git push origin build-main-0.24.2
```

### What Happens

1. **Parse Tag** - Validates channel and version format
2. **Build Frontend** - Compiles web assets (React)
3. **Build Binaries** - Cross-compiles for 6 platforms:
   - `linux-x64`
   - `linux-musl-x64`
   - `linux-arm64`
   - `osx-x64`
   - `osx-arm64`
   - `win-x64`
4. **Create Release** - GitHub release (pre-release for dev, stable for main)
5. **Trigger Packaging** - AUR, COPR, PPA, Docker (if configured)

### Checking Build Status

```bash
# View workflow runs
gh run list --workflow=build-on-tag.yml

# Watch a specific run
gh run watch

# View logs if it fails
gh run view --log-failed
```

### Dev vs Main Channels

| Feature | Dev Channel | Main Channel |
|---------|-------------|--------------|
| **Trigger** | `build-dev-*` | `build-main-*` |
| **Version Format** | With timestamp | Semantic only |
| **GitHub Release** | Pre-release | Stable release |
| **Packages** | `slskdn-dev` | `slskdn` |
| **Docker Tag** | `dev` | `latest` |
| **Testing** | Minimal | Extensive |
| **Frequency** | On-demand | Monthly |

### Troubleshooting

**Tag format rejected:**
```
Error: Invalid tag format
```
- Dev tags MUST include timestamp: `0.24.1.dev.20251213.140454`
- Main tags MUST be semantic: `0.24.2`

**Build fails:**
```bash
# Check the workflow logs
gh run view --log-failed

# Common issues:
# - Web build lint errors (usually non-fatal)
# - .NET version mismatch
# - Missing Node.js dependencies
```

**Packages not updating:**
- Package workflows trigger separately
- Check `release-linux.yml`, `release-copr.yml`, `release-ppa.yml`
- May need secrets configured (AUR_SSH_KEY, COPR_TOKEN, GPG_PRIVATE_KEY)

### Safety Features

✅ **No automatic builds** - Explicit tags only  
✅ **Version validation** - Rejects invalid formats  
✅ **Channel isolation** - Dev and main are separate  
✅ **Artifact retention** - Dev: 30 days, Main: permanent  
✅ **Self-hosted fallback** - Uses GitHub runners if self-hosted unavailable

### Migration from Old System

**Old workflows (DISABLED)**:
- `ci.yml` - Only manual `workflow_dispatch`
- `dev-release.yml` - Only manual `workflow_dispatch`

**New workflow**:
- `build-on-tag.yml` - Tag-triggered only

Both systems can coexist during testing.

---

## Local Testing (Before Pushing)

**Always test locally first:**

```bash
# Build everything
./bin/build

# Test the binary
./src/slskd/bin/Release/net8.0/slskd --version

# Run tests
dotnet test

# Check for lint errors
./bin/lint
```

---

## Example Workflow

```bash
# 1. Make changes on experimental/multi-source-swarm
git checkout experimental/multi-source-swarm
git pull

# 2. Test locally
./bin/build
dotnet test

# 3. Commit and push
git add .
git commit -m "feat: Add awesome feature"
git push

# 4. Create dev build
VERSION=$(date +0.24.1.dev.%Y%m%d.%H%M%S)
git tag "build-dev-$VERSION"
git push origin "build-dev-$VERSION"

# 5. Monitor build
gh run watch

# 6. Test the release
# Download from GitHub releases or install via package manager
yay -S slskdn-dev  # Arch
# or
docker pull ghcr.io/snapetech/slskdn:dev

# 7. If all good, merge to main and create main release
git checkout main
git merge experimental/multi-source-swarm --no-ff
git tag build-main-0.24.2
git push origin main
git push origin build-main-0.24.2
```

---

## Security Checklist

Before any release (dev or main):

- [ ] All tests pass (`dotnet test`)
- [ ] No security warnings (`./bin/lint`)
- [ ] Docs updated (especially security claims)
- [ ] Changelog updated
- [ ] Local smoke test completed
- [ ] Server starts without errors
- [ ] Web UI loads correctly

---

**Questions?** Check `docs/CI_REDESIGN.md` for design rationale.
