# Merge to Main - Quick Start Guide

**One-Pass Merge Strategy** - No build troubleshooting needed

---

## ✅ Pre-Flight Checklist (Do This First)

```bash
# 1. Verify dev build succeeded
gh run list --workflow=dev-release.yml --limit 1

# 2. Check for merge conflicts (dry run)
git fetch origin master
git checkout -b test-merge-$(date +%s)
git merge --no-commit --no-ff origin/master
# If conflicts: resolve them, then abort and fix in experimental branch
git merge --abort && git checkout experimental/whatAmIThinking && git branch -D test-merge-*

# 3. Verify no hardcoded branch refs
grep -r "experimental/whatAmIThinking" .github/workflows/ || echo "✅ Clean"
```

---

## 🚀 Execute Merge (5 Commands)

```bash
# Step 1: Update docs (if not already done)
git checkout experimental/whatAmIThinking
# Edit README.md, remove experimental markers, update feature status
git add README.md
git commit -m "docs: Update for main branch graduation"
git push origin experimental/whatAmIThinking

# Step 2: Merge to master
git checkout master
git pull origin master
git merge --no-ff experimental/whatAmIThinking \
  -m "Merge experimental/whatAmIThinking to master - v0.25.0"
git push origin master

# Step 3: Create release tag (triggers build automatically)
VERSION="0.25.0"  # Update version as needed
git tag -a "build-main-${VERSION}" \
  -m "Release v${VERSION}: Feature graduation"
git push origin "build-main-${VERSION}"

# Step 4: Monitor build
gh run watch
```

---

## 📋 What Happens Automatically

1. **Tag push triggers**: `build-on-tag.yml` workflow
2. **Builds**: Frontend + 6 platform binaries (linux-x64, linux-arm64, osx-x64, win-x64, etc.)
3. **Creates release**: GitHub release under version tag (e.g., `0.25.0`)
4. **Publishes packages**: 
   - AUR: `slskdn` and `slskdn-bin` (source + binary)
   - COPR: `slskdn/slskdn`
   - PPA: `slskdn` package
   - Docker: `ghcr.io/snapetech/slskdn:latest` and `:0.25.0`

---

## 🎯 Version Number

**Current base**: `0.24.1` (from workflows)  
**Recommended**: `0.25.0` (minor version bump for feature graduation)

Update in:
- Tag: `build-main-0.25.0`
- Release notes
- Any version constants (if needed)

---

## ⚠️ If Build Fails

```bash
# Check workflow logs
gh run view --log

# Common issues:
# 1. Version conflict - check if tag already exists
# 2. Package build failure - check specific package workflow
# 3. Frontend build - check Node.js version compatibility

# Rollback if needed:
git checkout master
git revert -m 1 HEAD  # Revert merge commit
git push origin master
```

---

## 📝 Post-Merge Tasks

- [x] Verify release created on GitHub
- [x] Check package builds (AUR, COPR, PPA)
- [x] Update any remaining docs
- [x] Archive experimental branch (optional, after 24-48h verification)

---

## 🔍 Verification Commands

```bash
# Check build status
gh run list --workflow=build-on-tag.yml --limit 1

# Check release
gh release view 0.25.0

# Check packages
gh run list --workflow=release-linux.yml --limit 1
gh run list --workflow=release-copr.yml --limit 1
gh run list --workflow=release-ppa.yml --limit 1
```

---

**Total Time**: ~10 minutes active work + ~30-60 minutes for builds

**Risk**: Low (tag-based builds, pre-verified merge, rollback available)
