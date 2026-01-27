# E2E File Existence Verification Options

This document explains the three options for verifying downloaded files exist on disk in E2E tests, with pros/cons analysis.

## Option 1: Test-Only Backend Endpoint

### Implementation
```csharp
// GET /api/v0/test/files?dir=downloads (only when E2E_TEST_MODE=true)
[HttpGet("test/files")]
public IActionResult ListTestFiles([FromQuery] string dir = "downloads")
{
    if (!_optionsMonitor.CurrentValue.Flags.E2ETestMode) return NotFound();
    var files = Directory.GetFiles(Path.Combine(AppDirectory, dir));
    return Ok(files.Select(f => new { name = Path.GetFileName(f), size = new FileInfo(f).Length }));
}
```

### Pros
- ✅ Simple to use from Playwright (just HTTP GET)
- ✅ Can return rich metadata (size, sha256, modification time, etc.)
- ✅ Works across platforms (no filesystem access needed in test)
- ✅ Can filter by pattern or contentId
- ✅ Can return file contents for checksum verification

### Cons
- ❌ Requires backend code change (test-only endpoint)
- ❌ Security risk if accidentally enabled in production (must be gated by flag)
- ❌ Adds maintenance burden (test infrastructure in production code)
- ❌ Must ensure flag is never set in production configs

### Security Considerations
- Must be gated by `E2ETestMode` flag that defaults to false
- Should check environment variable or explicit config setting
- Consider rate limiting or IP whitelist for extra safety

---

## Option 2: Harness Filesystem Access ⭐ **RECOMMENDED**

### Implementation
```typescript
// In SlskdnNode.ts
async getDownloadedFiles(): Promise<Array<{name: string, size: number, path: string}>> {
  const downloadsDir = path.join(this.appDir, 'downloads');
  const files = await fs.readdir(downloadsDir, { withFileTypes: true });
  // Return file metadata
}
```

### Pros
- ✅ No backend changes needed
- ✅ Direct access to test node's filesystem
- ✅ Can check file size, modification time, etc.
- ✅ Natural fit (harness already manages app directories)
- ✅ No security concerns (test-only code)
- ✅ Works for all harness-launched nodes
- ✅ Can compute sha256 for verification if needed

### Cons
- ❌ Only works for harness-launched nodes (not pre-launched via env vars)
- ❌ Requires Node.js filesystem APIs in test code
- ❌ Platform-specific path handling (though path.join handles this)

### Implementation Status
✅ **IMPLEMENTED** - See `SlskdnNode.getDownloadedFiles()`, `findDownloadedFile()`, and `waitForDownloadedFile()`

### CI Compatibility
✅ **Works in both local E2E and GitHub CI**:
- Local E2E: Uses harness-launched nodes → file verification works
- GitHub CI: Uses harness-launched nodes (no `SLSKDN_NODE_*_URL` env vars) → file verification works
- Pre-launched nodes: File verification not available (harness methods require app directory access)

The implementation includes:
- Error handling for missing directories
- Polling support via `waitForDownloadedFile()` for async downloads
- Platform-agnostic path handling (works on Linux, macOS, Windows)
- Graceful handling of race conditions (files appearing during check)

---

## Option 3: Direct Filesystem Access in Test

### Implementation
```typescript
// In test file
import * as fs from 'fs/promises';
const nodeC = harness.getNode('C');
const downloadsDir = path.join(nodeC.getAppDir(), 'downloads');
const files = await fs.readdir(downloadsDir);
```

### Pros
- ✅ No code changes needed (just test code)
- ✅ Maximum flexibility (can check anything)
- ✅ No security concerns (test-only)
- ✅ Can use any Node.js filesystem APIs

### Cons
- ❌ Only works for harness-launched nodes
- ❌ Requires knowing app directory path
- ❌ More verbose test code (repeated filesystem logic)
- ❌ Platform-specific path handling
- ❌ Duplicates logic that could be in harness

---

## Comparison Matrix

| Feature | Option 1 (Backend Endpoint) | Option 2 (Harness) | Option 3 (Direct FS) |
|---------|------------------------------|---------------------|----------------------|
| Backend changes | ❌ Required | ✅ None | ✅ None |
| Security risk | ⚠️ Medium (if misconfigured) | ✅ None | ✅ None |
| Works with pre-launched nodes | ✅ Yes | ❌ No | ❌ No |
| Code reusability | ✅ High | ✅ High | ❌ Low |
| Test simplicity | ✅ High | ✅ High | ⚠️ Medium |
| Flexibility | ✅ High | ✅ High | ✅ Very High |
| Maintenance | ⚠️ Medium | ✅ Low | ⚠️ Medium |

---

## Recommendation

**Option 2 (Harness Filesystem Access)** is the best choice because:

1. **No backend changes** - Keeps test infrastructure out of production code
2. **Natural fit** - Harness already manages app directories
3. **Clean API** - Simple methods like `getDownloadedFiles()` and `findDownloadedFile()`
4. **No security concerns** - Test-only code, no production exposure
5. **Reusable** - Other tests can use the same methods

The only limitation (not working with pre-launched nodes) is acceptable because:
- Pre-launched nodes are for manual testing/debugging
- E2E tests should use harness-launched nodes for determinism
- The warning in `env.ts` already alerts users to this

---

## Current Implementation

Option 2 is implemented in:
- `src/web/e2e/harness/SlskdnNode.ts`:
  - `getDownloadedFiles()` - Lists all files in downloads directory
  - `findDownloadedFile(searchTerm)` - Finds file by name or sha256 prefix

Used in:
- `src/web/e2e/multippeer-sharing.spec.ts` - `recipient_backfills_and_verifies_download` test
