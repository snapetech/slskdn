# Security: Key Rotation and GitGuardian Alerts

## Issue
GitGuardian detected high entropy secrets (private keys) in git history. These were committed in previous commits and are still accessible in git history even though they've been removed from tracking.

## Actions Taken

### 1. Added `.key` files to `.gitignore`
- Pattern: `*.key` (catches all key files)
- Prevents future key files from being committed

### 2. Removed all tracked key files
- **147 `.key` files** removed from git tracking
- Includes: `mesh-overlay.key`, `mesh-overlay.key.prev`, all `peer-profile.key` files, all `overlay_cert.key` files

### 3. Removed test-artifacts directory
- **2,703 files** removed from git tracking
- Contains E2E test artifacts with private keys, certificates, and databases
- Added `/test-artifacts/` to `.gitignore`

### 4. Key Rotation
- Local key files deleted (will be regenerated at runtime)
- Keys are generated dynamically by:
  - `Mesh.Overlay.KeyStore` (Ed25519 keypairs for mesh overlay)
  - `Identity.ProfileService` (Ed25519 keypairs for peer profiles)
  - `DhtRendezvous.CertificateManager` (X509 certificates)

## Remaining Issue: Git History

**Keys are still in git history** - GitGuardian scans git history, not just current files. The following commits contain keys:

- `ac893200` - Contains `mesh-overlay.key` with private key: `icohLCKWhu1oLgPQceQXYSiG9KUd37xkuwEUlnr5VYw=`
- Multiple commits with `test-artifacts/` containing `peer-profile.key` files

### Options to Fully Resolve

1. **Git History Rewrite** (Recommended for security)
   - Use `git filter-branch` or `git filter-repo` to remove key files from history
   - **Warning**: This rewrites history and requires force push
   - All collaborators must re-clone the repository

2. **Accept Risk** (Not recommended)
   - Keys in history are rotated (old keys are invalid)
   - New keys are generated at runtime
   - GitGuardian will continue to alert on historical commits

3. **GitGuardian Suppression** (Temporary)
   - Mark historical alerts as "resolved" in GitGuardian
   - New commits won't trigger alerts (keys are now ignored)

## Current State

✅ All `.key` files removed from git tracking  
✅ All `test-artifacts/` removed from git tracking  
✅ `.gitignore` updated to prevent future commits  
✅ Local key files deleted (will regenerate at runtime)  
⚠️ Keys still exist in git history (requires history rewrite to fully remove)

## Verification

```bash
# Check for tracked key files (should be 0)
git ls-files | grep -E "\.key$" | wc -l

# Check for tracked test-artifacts (should only show docs/archive files)
git ls-files | grep "test-artifacts"
```

## Next Steps

1. **Immediate**: Keys are rotated and won't be committed going forward ✅
2. **Optional**: Rewrite git history to remove keys from past commits (requires team coordination)
3. **Monitor**: GitGuardian should stop alerting on new commits
