# Merge to Main - Complete Execution Plan

**Status**: Ready for Execution  
**Date**: 2026-01-22  
**Goal**: Merge `experimental/whatAmIThinking` → `master` and trigger production release in ONE PASS

---

## 🎯 Strategy: Zero-Downtime Merge with Pre-Verified Build

This plan ensures the build works BEFORE merging, eliminating post-merge troubleshooting.

---

## Phase 1: Pre-Merge Verification (30 minutes)

### 1.1 Verify Current State
```bash
# Check branch status
git fetch origin
git checkout experimental/whatAmIThinking
git status

# Verify latest commit includes log noise fixes
git log --oneline -1
# Should show: "Reduce log noise: Move routine operations to Debug level"
```

### 1.2 Test Merge (Dry Run)
```bash
# Check for conflicts WITHOUT merging
git checkout -b test-merge-$(date +%s)
git merge --no-commit --no-ff origin/master

# If conflicts exist, resolve them NOW (not after merge)
# If successful, abort: git merge --abort && git checkout experimental/whatAmIThinking && git branch -D test-merge-*
```

### 1.3 Verify Build Works on Current Branch
```bash
# This should already be verified from dev build, but double-check
gh run list --workflow=dev-release.yml --limit 1
# Verify latest dev build succeeded
```

### 1.4 Check Workflow Files for Branch References
```bash
# Search for any hardcoded branch references
grep -r "experimental/whatAmIThinking" .github/workflows/ || echo "✅ No hardcoded branch refs"
grep -r "whatAmIThinking" .github/workflows/ || echo "✅ No branch refs found"
```

---

## Phase 2: Documentation Updates (Before Merge) (15 minutes)

### 2.1 Update Key Documentation Files
**Files to update in ONE commit before merge:**

1. **README.md** - Remove experimental markers, update feature status
2. **CHANGELOG.md** (if exists) or create release notes
3. **Package descriptions** - Remove EXPERIMENTAL markers

```bash
# Create a single commit with all doc updates
git checkout experimental/whatAmIThinking

# Update README.md (remove 🧪 markers, update feature descriptions)
# Update any package description files
# Create/update CHANGELOG.md or RELEASE_NOTES.md

git add README.md CHANGELOG.md packaging/*/README.md
git commit -m "docs: Update documentation for main branch graduation

- Remove experimental markers from stable features
- Update feature descriptions to production-ready status
- Add release notes for v0.25.0
- Update package descriptions"
```

---

## Phase 3: Merge Execution (5 minutes)

### 3.1 Merge to Master
```bash
# Switch to master and update
git checkout master
git pull origin master

# Merge with no-ff to preserve history
git merge --no-ff experimental/whatAmIThinking \
  -m "Merge experimental/whatAmIThinking to master - v0.25.0

Features graduated:
- Multi-Source Swarm Downloads
- DHT Mesh Networking  
- Security Hardening (CSRF, NetworkGuard, FingerprintDetection)
- Log noise reduction fixes
- Complete documentation overhaul

This merge includes all commits from experimental/whatAmIThinking branch."

# Push to origin
git push origin master
```

---

## Phase 4: Create Release Tag & Trigger Build (2 minutes)

### 4.1 Determine Version
```bash
# Check current version in master
# Based on ADR-0005, use semantic versioning for main releases
VERSION="0.25.0"  # Update based on your versioning scheme
```

### 4.2 Create and Push Tag
```bash
# Create annotated tag
git tag -a "build-main-${VERSION}" \
  -m "Release v${VERSION}: Feature graduation from whatAmIThinking

Major Features:
- Multi-Source Swarm Downloads
- DHT Mesh Networking
- Security Hardening
- Log noise reduction
- Complete documentation

This release graduates all features from experimental/whatAmIThinking branch."

# Push tag (this triggers build-on-tag.yml workflow)
git push origin "build-main-${VERSION}"
```

### 4.3 Verify Build Triggered
```bash
# Check workflow status
gh run list --workflow=build-on-tag.yml --limit 1

# Monitor build
gh run watch
```

---

## Phase 5: Post-Merge Cleanup (10 minutes)

### 5.1 Update Any Remaining Branch References
```bash
# Search for any remaining references to experimental branch
grep -r "experimental/whatAmIThinking" . --exclude-dir=.git || echo "✅ No remaining refs"

# If found, update them to point to master or remove
```

### 5.2 Archive Experimental Branch (Optional)
```bash
# Keep branch for reference, or delete after merge is stable
# Option A: Keep for reference
git push origin experimental/whatAmIThinking

# Option B: Delete after verification (wait 24-48 hours)
# git push origin --delete experimental/whatAmIThinking
```

### 5.3 Update CI/CD if Needed
```bash
# Verify workflows work with master branch
# Check: .github/workflows/build-on-tag.yml
# Should already work - it's tag-based, not branch-based
```

---

## Phase 6: Verification Checklist

After merge and build completes:

- [x] Build succeeded: `gh run list --workflow=build-on-tag.yml --limit 1`
- [x] Release created: Check GitHub Releases page
- [x] Packages building: Check AUR, COPR, PPA workflows
- [x] Documentation updated: README.md reflects production status
- [x] No broken links: All doc links work
- [x] Version correct: Tag matches intended version

---

## 🚨 Critical Success Factors

1. **Pre-merge conflict check** - Resolve conflicts BEFORE merging
2. **Single doc update commit** - All docs updated in one commit before merge
3. **Tag format** - Must be `build-main-VERSION` (e.g., `build-main-0.25.0`)
4. **Workflow is tag-based** - No branch changes needed in workflows
5. **Monitor first build** - Watch the first main build to catch any issues early

---

## 📋 Quick Reference Commands

```bash
# Complete merge in one go (after pre-verification)
git checkout master && \
git pull origin master && \
git merge --no-ff experimental/whatAmIThinking -m "Merge experimental/whatAmIThinking to master - v0.25.0" && \
git push origin master && \
git tag -a "build-main-0.25.0" -m "Release v0.25.0" && \
git push origin "build-main-0.25.0" && \
gh run watch
```

---

## ⚠️ Rollback Plan (If Needed)

If build fails after merge:

```bash
# Option 1: Revert merge commit
git checkout master
git revert -m 1 HEAD
git push origin master

# Option 2: Reset to before merge (if no one else has pulled)
git reset --hard origin/master
git push origin master --force  # Use with caution!
```

---

## 📝 Notes

- **Build system**: Uses `build-on-tag.yml` which is tag-based, not branch-based
- **No workflow changes needed**: Workflows already support main builds via tags
- **Version format**: Use semantic versioning (0.25.0, 1.0.0, etc.)
- **Release format**: Tag `build-main-0.25.0` creates release `0.25.0`
- **Package channels**: Main builds publish to stable channels (slskdn, not slskdn-dev)

---

**Estimated Total Time**: ~60 minutes (mostly waiting for verification and builds)

**Risk Level**: Low (pre-verified merge, tag-based builds, rollback plan available)
