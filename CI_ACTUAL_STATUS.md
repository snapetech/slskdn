# CI Build - What Actually Happened

## Issues Found (Don't Skip These)

### 1. Multiple Workflows Conflicting
**Problem**: Created TWO workflows both listening to `build-*` tags:
- `build-on-tag.yml` - Simple, correct
- `build-release.yml` - Complex, **wrong regex**

**Result**: The wrong one triggered first and failed.

**Fix**: Deleted `build-release.yml`, kept simple one.

### 2. Version Format for .NET
**Problem**: `.NET/NuGet rejects versions like `0.24.1.dev.20251213.140454`

**Solution**: Convert for .NET builds:
- Tag/packages: `0.24.1.dev.20251213.140454` (dots)
- .NET build: `0.24.1-dev-20251213-140454` (hyphens)

Used: `sed 's/\.dev\./-dev-/'`

### 3. Regex Was Too Strict
**Wrong regex**: `^build-([^-]+)-(.+)-([^-]+)$`
- Required: `build-dev-VERSION-TARGETS`
- Example: `build-dev-0.24.1.dev.20251213-aur`

**Correct regex**: `^build-(dev|main)-(.+)$`
- Format: `build-dev-VERSION`
- Example: `build-dev-0.24.1.dev.20251213.141034`

## Current Status

**Latest Tag**: `build-dev-0.24.1.dev.20251213.141034`
**Workflow**: `build-on-tag.yml` only
**Status**: Triggered, waiting for results

## Test It Properly

```bash
# Wait for build
sleep 60
gh run list --workflow=build-on-tag.yml --limit 1

# If it fails
gh run view --log-failed

# Check what's actually in the workflow
cat .github/workflows/build-on-tag.yml | head -50
```

## Lessons

1. **Don't create multiple drafts** - Pick one pattern and stick to it
2. **Test regex locally** before pushing
3. **Check logs immediately** when build fails
4. **One workflow at a time** - Don't leave WIP workflows enabled

## What Should Happen

IF the build succeeds:
- Binaries for 6 platforms
- GitHub pre-release created
- Artifacts uploaded

IF it fails:
- Check logs with `gh run view --log-failed`
- Fix the actual issue
- Don't assume it worked

