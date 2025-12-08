# ADR-0001: Known Gotchas & Anti-Patterns

> **Status**: Active (Living Document)  
> **Date**: 2025-12-08  
> **Author**: AI-assisted development sessions

This document captures known issues, anti-patterns, and "gotchas" that AI models and developers have encountered. **Read this before making changes.**

---

## ⚠️ FOR AI AGENTS: ADD TO THIS FILE IMMEDIATELY

**When you fix a bug, ADD IT HERE before doing anything else.**

This is not optional. This is the highest priority action after fixing a bug.

### When to add an entry:
- You fixed a bug that you or another AI introduced
- You fixed the same type of bug more than once
- You discovered a non-obvious pattern that causes errors
- You spent >5 minutes debugging something that should have been documented

### Entry template:
```markdown
### N. short-descriptive-name

**The Bug**: One sentence description.

**Files Affected**:
- `path/to/file.cs`

**Wrong**:
```code
// bad code
```

**Correct**:
```code
// good code
```

**Why This Keeps Happening**: Explanation of the root cause.
```

### After adding:
1. Commit immediately: `git commit -m "docs: Add gotcha for [description]"`
2. Then continue with other work

**If you skip this step, the same bug WILL happen again. Document it NOW.**

---

## 🚨 CRITICAL: Bugs That Keep Coming Back

### 1. `return undefined` vs `return []` in Frontend API Calls

**The Bug**: Frontend API functions that return `undefined` on error instead of `[]` cause downstream crashes.

**Files Affected**:
- `src/web/src/lib/searches.js` - `getResponses()`
- `src/web/src/lib/transfers.js` - `getAll()`

**Wrong**:
```javascript
if (!Array.isArray(response)) {
  console.warn('got non-array response');
  return undefined;  // 💀 Causes "Cannot read property 'map' of undefined"
}
```

**Correct**:
```javascript
if (!Array.isArray(response)) {
  console.warn('got non-array response');
  return [];  // ✅ Safe to iterate
}
```

**Why This Keeps Happening**: Models see `undefined` as a "signal" value and forget that callers will `.map()` or `.filter()` the result.

---

### 2. `async void` Event Handlers Without Try-Catch

**The Bug**: `async void` event handlers that throw exceptions crash the entire .NET process.

**Files Affected**:
- `src/slskd/Messaging/RoomService.cs` - `Client_LoggedIn`

**Wrong**:
```csharp
private async void Client_LoggedIn(object sender, EventArgs e)
{
    await TryJoinAsync(rooms);  // 💀 Exception here = process crash
}
```

**Correct**:
```csharp
private async void Client_LoggedIn(object sender, EventArgs e)
{
    try
    {
        await TryJoinAsync(rooms);
    }
    catch (Exception ex)
    {
        Logger.Error(ex, "Failed to execute post-login room actions");
    }
}
```

**Why This Keeps Happening**: `async void` is required for event handlers, but models forget it can't propagate exceptions.

---

### 3. Unbounded Parallelism in Download Loops

**The Bug**: `Task.Run` inside loops without concurrency limits causes resource exhaustion.

**Files Affected**:
- `src/slskd/Transfers/MultiSource/MultiSourceDownloadService.cs`

**Wrong**:
```csharp
foreach (var source in sources)
{
    _ = Task.Run(() => DownloadFromSourceAsync(source));  // 💀 Unbounded
}
```

**Correct**:
```csharp
var semaphore = new SemaphoreSlim(10);  // Cap at 10 concurrent
foreach (var source in sources)
{
    await semaphore.WaitAsync();
    _ = Task.Run(async () =>
    {
        try { await DownloadFromSourceAsync(source); }
        finally { semaphore.Release(); }
    });
}
```

**Why This Keeps Happening**: Models optimize for "parallelism = fast" without considering resource limits.

---

## ⚠️ HIGH: Common Mistakes

### 4. Copyright Headers - Wrong Company Attribution

**The Rule**: New slskdN files use `company="slskdN Team"`, existing upstream files keep `company="slskd Team"`.

**Fork-specific directories** (always slskdN headers):
- `Capabilities/`, `HashDb/`, `Mesh/`, `Backfill/`
- `Transfers/MultiSource/`, `Transfers/Ranking/`
- `Users/Notes/`, `DhtRendezvous/`, `Common/Security/`

**Why This Matters**: Legal clarity for fork vs upstream code.

---

### 5. Logging Pattern Inconsistency

**The Issue**: Mixed use of `ILogger<T>` and `Serilog.Log.ForContext`.

**Preferred** (standardization in progress):
```csharp
private readonly ILogger<MyService> _logger;

public MyService(ILogger<MyService> logger)
{
    _logger = logger;
}
```

**Avoid**:
```csharp
private static readonly ILogger Log = Serilog.Log.ForContext<MyService>();
```

---

### 6. React 16 Compatibility

**The Issue**: This project uses React 16.8.6. Don't use features from React 17+.

**Avoid**:
- `useId()` (React 18)
- `useDeferredValue()` (React 18)
- `useTransition()` (React 18)
- Automatic JSX transform (React 17)

**Safe to use**:
- `useState`, `useEffect`, `useContext`, `useReducer`, `useCallback`, `useMemo`, `useRef`

---

### 7. Path Traversal - Base64 Decoding

**The Issue**: User-supplied paths may be Base64-encoded with `..` components.

**Wrong**:
```csharp
var path = Base64Decode(userInput);
File.Delete(path);  // 💀 Could delete /etc/passwd
```

**Correct**:
```csharp
var path = Base64Decode(userInput);
var fullPath = Path.GetFullPath(path);
if (!fullPath.StartsWith(allowedRoot))
    throw new SecurityException("Path traversal attempt");
```

**Use `PathGuard`** in experimental branch: `PathGuard.NormalizeAndValidate(path, root)`

---

## 🔄 Patterns That Cause Fix/Unfix Cycles

### 8. ESLint/Prettier Formatting Wars

**The Cycle**:
1. Model fixes a bug
2. Lint fails on import order or quotes
3. Model "fixes" lint by changing unrelated code
4. Original fix gets lost

**Solution**: Run `npm run lint -- --fix` in `src/web/` before committing frontend changes.

---

### 9. DI Service Registration

**The Cycle**:
1. New service added
2. Forgot to register in `Program.cs`
3. Runtime crash: "Unable to resolve service"
4. Model adds registration
5. Merge conflict loses registration

**Checklist for new services**:
```csharp
// In Program.cs
builder.Services.AddSingleton<IMyService, MyService>();
// OR
builder.Services.AddScoped<IMyService, MyService>();
```

---

### 10. Experimental Files on Master Branch

**The Cycle**:
1. Work on experimental branch
2. Accidentally commit experimental files to master
3. "Fix" by removing files
4. Merge conflict brings them back

**Files that should NOT be on master**:
- `src/slskd/DhtRendezvous/`
- `src/slskd/Transfers/MultiSource/`
- `src/slskd/HashDb/`
- `src/slskd/Mesh/`
- `src/slskd/Backfill/`
- `src/slskd/Common/Security/` (beyond basic PathGuard)

---

## 📦 Packaging Gotchas (MAJOR PAIN POINT)

> ⚠️ **These issues caused 10+ CI failures each. Read carefully.**

### 11. Case Sensitivity EVERYWHERE

**The Issue**: Package names, URLs, and filenames must be **consistently lowercase**.

| Context | Correct | Wrong |
|---------|---------|-------|
| Package name | `slskdn` | `slskdN` |
| GitHub tag | `0.24.1-slskdn.22` | `0.24.1-slskdN.22` |
| Zip filename | `slskdn-0.24.1-...` | `slskdN-0.24.1-...` |
| COPR project | `slskdn` | `slskdN` |
| PPA changelog | `slskdn (0.24.1...)` | `slskdN (0.24.1...)` |

**Files that MUST use lowercase**:
- `packaging/aur/PKGBUILD*`
- `packaging/debian/changelog`
- `packaging/rpm/*.spec`
- `.github/workflows/*.yml`
- `packaging/homebrew/Formula/slskdn.rb`

---

### 12. SHA256 Checksum Formats

**The Issue**: Different packaging systems want checksums in different formats.

| System | Format | Example |
|--------|--------|---------|
| AUR PKGBUILD | Single-line array | `sha256sums=('abc123...' 'def456...')` |
| Homebrew | Quoted string | `sha256 "abc123..."` |
| Flatpak | Plain value | `sha256: abc123...` |
| Snap | Prefixed | `source-checksum: sha256/abc123...` |
| Chocolatey | PowerShell var | `$checksum = "abc123..."` |
| Nix flake | Quoted string | `sha256 = "abc123...";` |

**Multi-line PKGBUILD breaks makepkg**:
```bash
# WRONG - breaks AUR
sha256sums=(
  'abc123...'
  'def456...'
)

# CORRECT - single line
sha256sums=('abc123...' 'def456...')
```

---

### 13. SKIP vs Actual Hash in AUR

**The Issue**: AUR packages need `SKIP` for the source tarball (changes each release) but real hashes for static files.

```bash
# PKGBUILD source array order:
source=(
    "tarball.tar.gz"    # Index 0 - SKIP (changes)
    "slskd.service"     # Index 1 - real hash (static)
    "slskd.yml"         # Index 2 - real hash (static)
    "slskd.sysusers"    # Index 3 - real hash (static)
)

# Matching sha256sums:
sha256sums=('SKIP' '9e2f4b...' 'a170af...' '28b6c2...')
```

**The Cycle**:
1. Model updates tarball hash
2. AUR build fails (tarball changed)
3. Model sets to SKIP
4. Model accidentally SKIPs the static files too
5. AUR build fails (missing hashes)

---

### 14. Version Format Conversion

**The Issue**: GitHub tags use `-slskdn` but PKGBUILD uses `.slskdn`.

```bash
# GitHub tag format
0.24.1-slskdn.22

# PKGBUILD pkgver format (no hyphens allowed)
0.24.1.slskdn.22

# Conversion in workflows:
PKGVER=$(echo $TAG | sed 's/-slskdn/.slskdn/')
```

**Files that need conversion**:
- `.github/workflows/release-linux.yml`
- `.github/workflows/release-copr.yml`
- `packaging/aur/PKGBUILD*`

---

### 15. URL Patterns Must Match Release Assets

**The Issue**: Download URLs must exactly match the uploaded asset names.

**Asset naming pattern** (from `release-linux.yml`):
```
slskdn-{TAG}-linux-x64.zip
slskdn-{TAG}-linux-arm64.zip
slskdn-{TAG}-osx-x64.zip
slskdn-{TAG}-osx-arm64.zip
slskdn-{TAG}-win-x64.zip
```

**Common mistakes**:
- `slskdN-...` (wrong case)
- `slskdn-linux-x64.zip` (missing version)
- `slskdn_{TAG}_linux_x64.zip` (wrong separators)

---

### 16. Homebrew Formula Architecture Blocks

**The Issue**: Homebrew needs separate `on_arm` and `on_intel` blocks for macOS.

```ruby
on_macos do
  on_arm do
    url "...osx-arm64.zip"
    sha256 "..."
  end
  on_intel do
    url "...osx-x64.zip"
    sha256 "..."
  end
end

on_linux do
  url "...linux-x64.zip"
  sha256 "..."
end
```

**Don't**: Use a single URL for all platforms.

---

### 17. Workflow Timing Issues

**The Issue**: Packaging workflows run before release assets are uploaded.

**The Cycle**:
1. Release published
2. Packaging workflow triggered immediately
3. Asset download fails (not uploaded yet)
4. Workflow fails
5. Manual re-run required

**Solution in `release-linux.yml`**:
```yaml
# Retry loop with 30s delays
for i in {1..20}; do
  if curl -fsSL "$ASSET_URL" -o release.zip; then
    exit 0
  fi
  sleep 30
done
```

---

### 18. AUR Directory Cleanup

**The Issue**: AUR git clone fails if directory exists from previous run.

```bash
# WRONG - fails if aur-repo exists
git clone ssh://aur@aur.archlinux.org/slskdn-bin.git aur-repo

# CORRECT - clean first
rm -rf aur-repo
git clone ssh://aur@aur.archlinux.org/slskdn-bin.git aur-repo
```

---

### 19. COPR/PPA Need Different Spec Files

**The Issue**: COPR uses `.spec` files, PPA uses `debian/` directory.

**COPR** (`packaging/rpm/slskdn.spec`):
- RPM spec format
- `%{version}` macro
- `BuildRequires` / `Requires`

**PPA** (`packaging/debian/`):
- `changelog` (specific format!)
- `control`
- `rules`
- `copyright`

**Changelog format is STRICT**:
```
slskdn (0.24.1-slskdn.22-1) jammy; urgency=medium

  * Release 0.24.1-slskdn.22

 -- snapetech <slskdn@proton.me>  Sun, 08 Dec 2024 12:00:00 +0000
```

Note: TWO spaces before `--`, specific date format.

---

### 20. Self-Hosted Runner Paths

**The Issue**: Self-hosted runners have different paths than GitHub-hosted.

**GitHub-hosted**: `/home/runner/work/...`
**Self-hosted**: `/home/github/actions-runner/_work/...`

**Don't**: Hardcode paths. Use `$GITHUB_WORKSPACE`.

---

## 🧪 Test Gotchas

### 13. Flaky UploadGovernorTests

**The Issue**: Tests using `AutoData` with random values can hit edge cases.

**Example**: Integer division with small random values causes off-by-one errors.

**Solution**: Use `InlineAutoData` with fixed values for edge-case-sensitive tests.

---

### 14. Test Isolation

**The Issue**: Tests that share static state can interfere with each other.

**Solution**: Use `TestIsolationExtensions` for tests that need isolated state.

---

## 🔐 Security Gotchas (Experimental Branch)

### 15. Security Services Not Wired to Transfer Handlers

**Current State**: 30 security components exist but aren't integrated into actual transfer code.

**TODO**: Wire `PathGuard`, `ContentSafety`, `ViolationTracker` into:
- `TransferService`
- `FilesController`
- `MultiSourceDownloadService`

---

### 16. UPnP Disabled by Default

**The Issue**: UPnP has known security vulnerabilities.

**Current**: `EnableUpnp = false` by default in `NatDetectionService.cs`

**Don't**: Enable UPnP by default without explicit user opt-in.

---

## 📝 Documentation Gotchas

### 17. DEVELOPMENT_HISTORY.md vs memory-bank/progress.md

- `DEVELOPMENT_HISTORY.md` - Human-maintained release history
- `memory-bank/progress.md` - AI session log

**Don't** overwrite `DEVELOPMENT_HISTORY.md` with AI-generated content.

---

### 18. TODO.md vs memory-bank/tasks.md

- `TODO.md` - Human-maintained high-level todos
- `memory-bank/tasks.md` - AI-managed task backlog

**Don't** duplicate tasks between them. Reference each other instead.

---

*Last updated: 2025-12-08*

