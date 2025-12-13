# New CI/CD Design - Tag-Based Release Channels

**Status**: ⚠️ **PLANNING PHASE** - All automatic builds DISABLED until implementation complete  
**Current State**: Workflows only trigger on manual `workflow_dispatch`  
**Date**: 2025-12-13

---

## 🚨 Critical: Test Locally First

**BEFORE implementing new CI**:
1. ✅ Security changes committed (`feef5174`)
2. 🔜 Build locally: `./bin/build`
3. 🔜 Run tests: `dotnet test`
4. 🔜 Test multi-node mesh connectivity
5. 🔜 Verify descriptor publishing/fetching
6. 🔜 Only AFTER all local tests pass → proceed with CI

---

## Design Goals

1. **Explicit Builds Only**: Nothing builds unless explicitly tagged with `build`
2. **Release Channels**: Separate flows for `main` (stable) and `dev` (experimental)
3. **Tag-Based Control**: Additional tags specify distribution channels
4. **No Surprises**: Clear, predictable behavior

---

## Tag Strategy

### Build Trigger Tag

**Format**: `build-<channel>-<version>-<targets>`

**Examples**:
```bash
# Dev build for all targets
git tag build-dev-0.24.1.dev.20251213-all
git push origin build-dev-0.24.1.dev.20251213-all

# Main build for Linux only
git tag build-main-0.24.2-linux
git push origin build-main-0.24.2-linux

# Dev build for AUR only (testing)
git tag build-dev-0.24.1.dev.20251213-aur
git push origin build-dev-0.24.1.dev.20251213-aur
```

### Tag Components

1. **`build-`**: Required prefix (triggers workflows)
2. **Channel**: `main` or `dev`
3. **Version**: Semantic version (with `.dev.YYYYMMDD` for dev)
4. **Targets**: What to build
   - `all`: Everything (binaries + packages + docker)
   - `binaries`: Just cross-platform binaries
   - `linux`: Linux packages (deb/rpm/aur)
   - `docker`: Docker images only
   - `aur`: AUR package only
   - `copr`: COPR (Fedora) only
   - `ppa`: PPA (Ubuntu) only

---

## Workflow Structure

### New Workflows

```
.github/workflows/
├── ci-build.yml               # Build binaries (triggered by build-* tags)
├── ci-test.yml                # Run tests only (manual or on PR)
├── release-main.yml           # Main channel releases
├── release-dev.yml            # Dev channel releases
├── package-linux.yml          # Linux packaging (deb/rpm)
├── package-aur.yml            # AUR package
├── package-copr.yml           # COPR (Fedora)
├── package-ppa.yml            # PPA (Ubuntu)
└── docker-publish.yml         # Docker images
```

### Workflow Triggers

#### `ci-build.yml` (Build Binaries)
```yaml
on:
  push:
    tags:
      - 'build-*'
  workflow_dispatch:
    inputs:
      channel:
        type: choice
        options: [main, dev]
      version:
        type: string
      targets:
        type: choice
        options: [all, binaries, linux, docker]
```

**Jobs**:
1. Parse tag to extract channel/version/targets
2. Build cross-platform binaries (win, linux, osx, musl)
3. Upload artifacts to GitHub Actions
4. Optionally trigger packaging workflows based on targets

#### `release-main.yml` (Main Channel)
```yaml
on:
  workflow_call:
    inputs:
      version:
        required: true
      targets:
        required: true
```

**Jobs**:
1. Download binaries from `ci-build.yml`
2. Create GitHub Release (if doesn't exist)
3. Upload binaries to release
4. Trigger packaging based on targets:
   - `all` or `linux` → package-linux, package-aur, package-copr, package-ppa
   - `docker` → docker-publish

#### `release-dev.yml` (Dev Channel)
```yaml
on:
  workflow_call:
    inputs:
      version:
        required: true
      targets:
        required: true
```

**Jobs**:
1. Download binaries from `ci-build.yml`
2. Create GitHub Pre-Release with `-dev` suffix
3. Upload binaries to pre-release
4. Trigger dev packaging:
   - `all` or `aur` → package-aur (slskdn-dev)
   - `all` or `copr` → package-copr (slskdn-dev)
   - Docker: dev tag

#### `package-*.yml` (Distribution-Specific)
```yaml
on:
  workflow_call:
    inputs:
      version:
        required: true
      channel:
        required: true  # main or dev
      binaries_artifact:
        required: true  # artifact name from ci-build
```

**Each packaging workflow**:
1. Downloads binaries artifact
2. Builds distribution-specific package
3. Uploads to appropriate repository/registry

---

## Version Format

### Main Channel
- **Format**: `0.24.2` (semantic versioning)
- **Examples**:
  - `0.24.2` - patch release
  - `0.25.0` - minor release
  - `1.0.0` - major release

### Dev Channel
- **Format**: `0.24.1.dev.YYYYMMDD` (dots, not hyphens - for package managers)
- **Examples**:
  - `0.24.1.dev.20251213` - dev build from Dec 13, 2025
  - `0.25.0.dev.20251220` - dev build of upcoming 0.25.0

**Why dots?**: AUR, RPM, and PPA all reject hyphens in version strings (see memory:12054497)

---

## Release Channels

### Main Channel (`main` branch)
- **Purpose**: Stable releases
- **Testing**: Extensive testing, beta period
- **Frequency**: Monthly or as needed
- **Packages**:
  - GitHub Release (binaries for all platforms)
  - AUR: `slskdn`
  - COPR: `slskdn`
  - PPA: `slskdn`
  - Docker: `slskdn:latest`, `slskdn:0.24.2`

### Dev Channel (`experimental/multi-source-swarm` or `dev` branch)
- **Purpose**: Bleeding-edge features, testing
- **Testing**: Basic smoke tests
- **Frequency**: On-demand when features ready
- **Packages**:
  - GitHub Pre-Release (binaries)
  - AUR: `slskdn-dev`
  - COPR: `slskdn-dev`
  - Docker: `slskdn:dev`, `slskdn:dev-20251213`

---

## Example Workflows

### Scenario 1: Dev Build (Security Testing)
```bash
# After testing locally:
cd /home/keith/Documents/Code/slskdn
git checkout experimental/multi-source-swarm
git pull

# Tag for dev build (AUR only for quick testing)
git tag build-dev-0.24.1.dev.20251213-aur
git push origin build-dev-0.24.1.dev.20251213-aur

# GitHub Actions:
# 1. ci-build.yml triggers
# 2. Builds binaries for linux-x64
# 3. Calls release-dev.yml
# 4. release-dev.yml calls package-aur.yml
# 5. AUR package updated: slskdn-dev
```

### Scenario 2: Main Release
```bash
# After extensive testing on dev channel:
git checkout main
git merge experimental/multi-source-swarm --ff-only
git tag build-main-0.24.2-all
git push origin main
git push origin build-main-0.24.2-all

# GitHub Actions:
# 1. ci-build.yml builds all platforms
# 2. release-main.yml creates GitHub Release
# 3. Triggers all packaging workflows
# 4. Updates: AUR, COPR, PPA, Docker
```

### Scenario 3: Quick Docker Test
```bash
# Just want to test Docker image:
git tag build-dev-0.24.1.dev.20251213-docker
git push origin build-dev-0.24.1.dev.20251213-docker

# Only docker-publish.yml runs
```

---

## Migration Plan

### Phase 1: Disable (DONE ✅)
- [x] Disable automatic triggers in ci.yml
- [x] Disable automatic triggers in dev-release.yml
- [x] Commit and push (`feef5174`)

### Phase 2: Test Locally (IN PROGRESS 🔄)
- [ ] Build: `./bin/build`
- [ ] Test: `dotnet test`
- [ ] Multi-node mesh test
- [ ] Descriptor publishing/fetching
- [ ] Rate limiting verification

### Phase 3: Design (THIS DOCUMENT 📝)
- [x] Define tag strategy
- [x] Define workflow structure
- [x] Define version formats
- [x] Define release channels

### Phase 4: Implement (TODO 🔜)
- [ ] Create `ci-build.yml`
- [ ] Create `release-main.yml`
- [ ] Create `release-dev.yml`
- [ ] Update `package-*.yml` workflows
- [ ] Add tag parsing logic
- [ ] Add workflow dispatch inputs

### Phase 5: Test CI (TODO 🧪)
- [ ] Test with `build-dev-*-binaries` (no distribution)
- [ ] Verify binaries build correctly
- [ ] Test with `build-dev-*-aur` (one distro)
- [ ] Verify AUR package updates
- [ ] Test with `build-main-*-all`
- [ ] Verify all channels update

### Phase 6: Document (TODO 📚)
- [ ] Update README with new release process
- [ ] Add RELEASE.md with tag examples
- [ ] Update contributor docs

---

## Safety Guardrails

1. **No automatic builds** without explicit `build-*` tag
2. **Tag validation** in workflows (fail if format wrong)
3. **Version validation** (fail if not semantic or dev format)
4. **Channel validation** (must be `main` or `dev`)
5. **Target validation** (must be recognized target)
6. **Artifact verification** (check binaries before uploading)
7. **Test step** before package upload (smoke test)

---

## Files to Create/Modify

### New Files
- `.github/workflows/ci-build.yml`
- `.github/workflows/release-main.yml`
- `.github/workflows/release-dev.yml`
- `docs/RELEASE_PROCESS.md`

### Modify
- `.github/workflows/ci.yml` (simplify, remove publish jobs)
- `.github/workflows/dev-release.yml` (rename to package-aur-dev.yml)
- `.github/workflows/release-*.yml` (convert to workflow_call)
- `README.md` (update release channel info)

### Remove (After New System Working)
- Old workflows that don't fit new structure

---

## Open Questions

1. ❓ Should `experimental/multi-source-swarm` become `dev` branch?
2. ❓ Should dev builds auto-create pre-releases or just upload to existing?
3. ❓ Should we version binaries differently from packages?
4. ❓ Notification strategy for dev builds (Discord, email)?

---

**Next Steps**:
1. ✅ Get user approval on this design
2. 🔄 Test security changes locally
3. 🔜 Implement Phase 4 workflows
4. 🔜 Test with dev-only builds
5. 🔜 Gradually roll out to main channel

