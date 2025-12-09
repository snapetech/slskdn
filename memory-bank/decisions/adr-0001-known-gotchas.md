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

### 10b. YAML Heredocs with Special Characters

**The Bug**: GitHub Actions workflows with inline heredocs containing `${}`, `#{}`, or `\$` break YAML parsing.

**Files Affected**:
- `.github/workflows/release-homebrew.yml`
- `.github/workflows/release-packaging.yml`

**Wrong**:
```yaml
- name: Generate file
  run: |
    cat > file.nix <<EOF
    let pkgs = nixpkgs.\${system};  # 💀 YAML parser chokes on this
    EOF
```

**Correct**: Use external scripts in `packaging/scripts/`:
```yaml
- name: Generate file
  run: |
    chmod +x packaging/scripts/update-nix.sh
    packaging/scripts/update-nix.sh "${{ steps.release.outputs.tag }}"
```

**Why This Keeps Happening**: Models inline heredocs for "simplicity" without realizing Nix `${}` and Ruby `#{}` break YAML.

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

### 19. HashDb Not Populated - Missing Event Subscription

**The Bug**: HashDb was initializing but `seq_id` stayed at 0 because no code was hashing downloaded files.

**Files Affected**:
- `src/slskd/HashDb/HashDbService.cs`
- `src/slskd/Program.cs`

**Root Cause**: The `ContentVerificationService` only hashes files during multi-source downloads. Regular single-source downloads raised `DownloadFileCompleteEvent` but nothing subscribed to hash the file.

**Fix**: Subscribe `HashDbService` to `DownloadFileCompleteEvent` and hash downloaded files:
```csharp
eventBus.Subscribe<DownloadFileCompleteEvent>("HashDbService.DownloadComplete", OnDownloadCompleteAsync);
```

**Why This Happened**: The hashing logic was only implemented in the multi-source path, not the common download completion path.

---

### 20. Passive FLAC Discovery Architecture - Understanding the Design

**The Confusion**: The HashDb/FlacInventory was expected to populate "passively" but wasn't.

**The Design (Clarified)**:

The passive FLAC discovery system has **three sources** of FLAC files:

1. **Search Results** - When WE search, we see other users' files → add to `FlacInventory` with `hash_status='none'`
2. **Downloads** - When we download a FLAC → compute hash → store with `hash_status='known'`
3. **Incoming Interactions** - When users search us or download from us → track their username → optionally browse them later

**How FlacInventory Gets Populated**:

| Source | Event | Action |
|--------|-------|--------|
| Our searches | `SearchResponsesReceivedEvent` | Upsert FLAC files to FlacInventory (hash_status='none') |
| Our downloads | `DownloadFileCompleteEvent` | Hash first 32KB, store in HashDb, update FlacInventory |
| Mesh sync | `MeshSyncService` | Receive hashes from other slskdn clients |
| Backfill | `BackfillSchedulerService` | Probe files in FlacInventory where hash_status='none' |

**How Hashes Get Discovered**:

```
FlacInventory (hash_status='none')
         ↓
BackfillSchedulerService picks candidates
         ↓
Downloads first 32KB header
         ↓
Computes SHA256 hash
         ↓
Updates HashDb + FlacInventory
         ↓
Publishes to MeshSync
```

**Key Insight**: The `BackfillSchedulerService` is the "engine" that converts `hash_status='none'` entries into `hash_status='known'`. But it needs the `FlacInventory` to be populated first, which happens via search results and incoming interactions.

**Files Involved**:
- `src/slskd/HashDb/HashDbService.cs` - Subscribes to events, populates FlacInventory
- `src/slskd/Search/SearchService.cs` - Raises `SearchResponsesReceivedEvent`
- `src/slskd/Events/Types/Events.cs` - Defines `SearchResponsesReceivedEvent`
- `src/slskd/Backfill/BackfillSchedulerService.cs` - Probes FlacInventory entries
- `src/slskd/Application.cs` - Handles incoming searches/uploads (peer tracking)

---

---

### 21. API Calls Before Login - Infinite Loop Danger

**The Bug**: Components that make API calls on mount will cause infinite loops or errors when rendered on the login page (before authentication).

**Files Affected**:
- `src/web/src/components/LoginForm.jsx`
- `src/web/src/components/Shared/Footer.jsx`
- Any component rendered before login

**Wrong**:
```jsx
// In LoginForm.jsx - BAD: Footer makes API calls
import Footer from './Shared/Footer';

const LoginForm = () => {
  return (
    <>
      <LoginContent />
      <Footer /> {/* 💀 If Footer fetches data on mount, this breaks */}
    </>
  );
};

// In Footer.jsx - BAD: API call on mount
const Footer = () => {
  const [stats, setStats] = useState(null);

  useEffect(() => {
    api.getStats().then(setStats); // 💀 401 error before login!
  }, []);

  return <footer>...</footer>;
};
```

**Correct**:
```jsx
// Footer.jsx - GOOD: Pure static component, no API calls
const Footer = () => {
  const year = new Date().getFullYear();

  return (
    <footer>
      © {year} <a href="https://github.com/...">slskdN</a>
      {/* All content is static - no useEffect, no API calls */}
    </footer>
  );
};
```

**Why This Keeps Happening**: Models add "helpful" features like version info or stats to footers without considering the login page context.

**Rule**: Components rendered before login (LoginForm, Footer on login, error pages) MUST be pure/static with ZERO API calls.

---

### 22. HashDb Schema Migrations - Versioned Upgrades

**The System**: HashDb uses a versioned migration system (`HashDbMigrations.cs`) that runs automatically on startup.

**Key Files**:
- `src/slskd/HashDb/Migrations/HashDbMigrations.cs` - Migration definitions
- `docs/HASHDB_SCHEMA.md` - Schema documentation

**How It Works**:
1. `__HashDbMigrations` table tracks applied versions
2. On startup, `RunMigrations()` compares current vs target version
3. Pending migrations run in order, each in a transaction
4. Failed migrations roll back automatically

**Adding New Columns** (SQLite gotcha):
```csharp
// WRONG - SQLite doesn't support multiple ALTER in one command
cmd.CommandText = @"
    ALTER TABLE Foo ADD COLUMN bar TEXT;
    ALTER TABLE Foo ADD COLUMN baz INTEGER;
";

// CORRECT - Execute each ALTER separately
var alters = new[] {
    "ALTER TABLE Foo ADD COLUMN bar TEXT",
    "ALTER TABLE Foo ADD COLUMN baz INTEGER"
};
foreach (var sql in alters)
{
    using var alterCmd = conn.CreateCommand();
    alterCmd.CommandText = sql;
    alterCmd.ExecuteNonQuery();
}
```

**Handling Existing Columns** (idempotent migrations):
```csharp
try
{
    alterCmd.ExecuteNonQuery();
}
catch (SqliteException ex) when (ex.Message.Contains("duplicate column"))
{
    // Column already exists - skip
}
```

**Check Current Version**:
```bash
curl http://localhost:5030/api/v0/hashdb/schema
```

**Rule**: Always increment `CurrentVersion` when adding migrations. Never modify existing migrations.

---

### 23. Missing `using` Directives - Check ALL Related Files

**The Bug**: Adding a type (e.g., `DateTimeOffset`) to an interface but only adding the `using System;` directive to one file, then having to fix each file one-by-one as compilation fails.

**Files Affected**:
- Any file that shares types across interface/implementation/controller boundaries

**Wrong Workflow**:
```
1. Add DateTimeOffset to IHashDbService.cs
2. Add "using System;" to IHashDbService.cs
3. Compile → ERROR in HashDbController.cs
4. Add "using System;" to HashDbController.cs
5. Compile → ERROR in HashDbService.cs
6. Add "using System;" to HashDbService.cs
7. Finally compiles ✅ (wasted 3 compile cycles)
```

**Correct Workflow**:
```
1. Add DateTimeOffset to IHashDbService.cs
2. BEFORE compiling, grep for all files that might need the type:
   grep -l "IHashDbService\|HashDb" src/slskd/HashDb/**/*.cs
3. Add "using System;" to ALL relevant files in one pass
4. Compile once ✅
```

**Pre-Compile Checklist** when adding new types:
```bash
# Find all files in the feature directory
find src/slskd/MyFeature -name "*.cs" -type f

# Or grep for files using the interface/class
grep -rl "IMyService\|MyService" src/slskd/MyFeature/
```

**Why This Keeps Happening**: AI models fix errors incrementally instead of thinking ahead about which files share the same types.

**Rule**: When adding a new type to an interface, check ALL files in the same namespace/feature directory and add necessary `using` directives BEFORE attempting to compile.

---

### 24. AUR PKGBUILD Checksums - NEVER Replace SKIP

**The Bug**: The AUR workflow was calculating the sha256 of `slskdn-dev-linux-x64.zip` and replacing the entire `sha256sums` array, overwriting `SKIP` with the calculated hash. This causes validation failures on `yay -Syu` because the zip changes every build.

**What Was Happening**:
```bash
# PKGBUILD template (CORRECT):
sha256sums=('SKIP' '9e2f4b...' 'a170af...' '28b6c2...')
#           ^^^^   ^^^^^^^^   ^^^^^^^^   ^^^^^^^^
#           zip    service    yml        sysusers
#          (changes) (static)  (static)  (static)

# Workflow was replacing it with (WRONG):
sha256sums=('abc123...' 'SKIP' 'SKIP' 'SKIP')
#           ^^^^^^^^^^
#           Calculated hash for zip - breaks on next download!
```

**Why This Breaks**:
1. CI builds `slskdn-dev-linux-x64.zip` and calculates hash `abc123...`
2. Workflow updates AUR PKGBUILD with `sha256sums=('abc123...' ...)`
3. User runs `yay -S slskdn-dev` → works (zip matches hash)
4. CI rebuilds zip → new hash `def456...`
5. User runs `yay -Syu` → **FAILS** (cached zip has hash `abc123...`, PKGBUILD expects `abc123...`, but downloaded zip is `def456...`)

**The Fix**:
```bash
# DON'T calculate or replace the zip hash in the workflow
# The PKGBUILD template already has SKIP for index 0

# OLD (wrong):
sed -i "s/sha256sums=.*/sha256sums=('$SHA256' 'SKIP' 'SKIP' 'SKIP')/" PKGBUILD

# NEW (correct):
# Just update pkgver and _commit, leave sha256sums alone
sed -i "s/^pkgver=.*/pkgver=${VERSION}/" PKGBUILD
sed -i "s/^_commit=.*/_commit=${COMMIT}/" PKGBUILD
```

**Rule**: For AUR packages that download release binaries (not source), the first entry in `sha256sums` MUST be `'SKIP'` because the binary changes every build. Only static files (service files, configs) get real checksums.

**Related**: See gotcha #13 "SKIP vs Actual Hash in AUR" for more context on why this pattern exists.

---

## Package Manager Version Constraints

**The Problem**: AUR and RPM package managers don't allow hyphens in version strings, causing build failures.

**Error Messages**:
```
# AUR:
==> ERROR: pkgver is not allowed to contain colons, forward slashes, hyphens or whitespace.

# RPM:
error: line 2: Illegal char '-' (0x2d) in: Version: 0.24.1-dev-20251209-203936
```

**Why This Happens**:
Our dev builds use the format `0.24.1-dev-20251209-203936` (with hyphens). This works fine for Git tags and GitHub releases, but AUR and RPM have strict version format requirements:
- AUR `pkgver`: No hyphens, colons, slashes, or whitespace
- RPM `Version`: No hyphens (hyphen is reserved for separating version from release number)

**The Fix**:
Convert ALL hyphens to dots when generating package versions:

```bash
# Git/GitHub (hyphens OK):
DEV_VERSION="0.24.1-dev-20251209-203936"

# AUR/RPM/DEB (convert to dots):
ARCH_VERSION=$(echo "$DEV_VERSION" | sed 's/-/./g')
# Result: 0.24.1.dev.20251209.203936
```

**CRITICAL**: Use `sed 's/-/./g'` (global replace) NOT `sed 's/-/./'` (only first hyphen)!

**Where This Applies**:
- AUR PKGBUILD: `pkgver=0.24.1.dev.20251209.203936`
- RPM spec: `Version: 0.24.1.dev.20251209.203936`
- Debian changelog: `slskdn-dev (0.24.1.dev.20251209.203936-1)`
- Package filenames: `slskdn-dev_0.24.1.dev.20251209.203936_amd64.deb`

**Git Tag and Zip Stay Original**:
- Git tag: `dev-20251209-203936` (hyphens OK)
- Zip file: `slskdn-dev-20251209-203936-linux-x64.zip` (hyphens OK)
- GitHub release title: `Dev Build 20251209-203936` (hyphens OK)

---

## Integration Test Project Missing Project Reference

**The Problem**: Docker builds fail with `error CS0234: The type or namespace name 'Common' does not exist in the namespace 'slskd'` when building integration tests.

**Root Cause**: The `tests/slskd.Tests.Integration/slskd.Tests.Integration.csproj` file was missing a `<ProjectReference>` to the main `src/slskd/slskd.csproj` project.

**Error Message**:
```
/slskd/tests/slskd.Tests.Integration/SecurityIntegrationTests.cs(10,13): error CS0234: 
The type or namespace name 'Common' does not exist in the namespace 'slskd' 
(are you missing an assembly reference?) [/slskd/tests/slskd.Tests.Integration/slskd.Tests.Integration.csproj]
```

**Why This Breaks**:
1. Integration tests need to reference types from the main project (`slskd.Common.Security`, etc.)
2. Without a `<ProjectReference>`, the compiler can't find any `slskd.*` namespaces
3. This fails silently in local builds if you've previously built the main project (DLL is in bin/), but ALWAYS fails in Docker/CI clean builds

**The Fix**:
```xml
<!-- tests/slskd.Tests.Integration/slskd.Tests.Integration.csproj -->
<ItemGroup>
  <ProjectReference Include="../../src/slskd/slskd.csproj" />
</ItemGroup>
```

**Prevention**:
- When creating ANY test project, ALWAYS add a `<ProjectReference>` to the code being tested
- Test in Docker before committing: `docker build -f Dockerfile .`
- Check .csproj files when you see "namespace does not exist" errors in CI

**Related**: This is especially insidious because local `dotnet build` might work if you've built the main project before, masking the missing reference until CI runs.

---

## Workflow File Pattern Mismatch in Download Step

**The Problem**: The `packages` job fails with "no assets match the file pattern" when trying to download the zip from the dev release.

**Root Cause**: Mismatch between the actual filename and the download pattern:
- Build job creates: `slskdn-dev-linux-x64.zip` (no timestamp)
- Packages job tried to download: `slskdn-dev-*-linux-x64.zip` (wildcard for timestamp that doesn't exist)

**Error Message**:
```
gh release download dev --pattern "slskdn-dev-*-linux-x64.zip"
no assets match the file pattern
```

**Why This Breaks**:
1. The `build` job creates `slskdn-dev-linux-x64.zip` without a timestamp in the filename
2. The `release` job uploads this file to the `dev` tag as-is
3. The `packages` job tries to download with a wildcard pattern expecting a timestamp
4. The wildcard doesn't match, so no file is downloaded

**The Fix**:
```yaml
# packages job - Download from Dev Release step
gh release download dev \
  --repo ${{ github.repository }} \
  --pattern "slskdn-dev-linux-x64.zip"  # Exact filename, no wildcard
```

**Prevention**:
- When adding workflow download steps, check what the ACTUAL filename is from the upload step
- Don't use wildcards unless the filename actually varies
- The timestamp is in the VERSION/tag, not in the zip filename for dev builds

**Note**: The timestamped dev tag (e.g., `dev-20251209-212425`) is separate from the floating `dev` tag. The `dev` tag always points to the latest dev build and contains `slskdn-dev-linux-x64.zip`.

---

## Building RPM Packages on Ubuntu Fails with Missing BuildRequires

**The Problem**: The `packages` job fails when trying to build .rpm packages on Ubuntu with "Failed build dependencies: systemd-rpm-macros is needed".

**Root Cause**: The RPM spec file has `BuildRequires: systemd-rpm-macros` and `BuildRequires: unzip`, which are Fedora packages not available in Ubuntu's apt repositories. You can't build RPMs on Ubuntu that require Fedora-specific build tools.

**Error Message**:
```
error: Failed build dependencies:
	systemd-rpm-macros is needed by slskdn-dev-0.24.1.dev.20251209.213134-1.x86_64
	unzip is needed by slskdn-dev-0.24.1.dev.20251209.213134-1.x86_64
```

**Why This Breaks**:
1. RPM spec files can have `BuildRequires` for Fedora-specific packages
2. Ubuntu (apt) doesn't have `systemd-rpm-macros` or the exact versions of build tools RPM expects
3. The `rpmbuild` command on Ubuntu can't satisfy these dependencies
4. Cross-distro package building requires containers or native build environments

**The Fix**:
Don't build RPMs on Ubuntu. Let COPR (which runs on Fedora) handle RPM builds. The `packages` job should only build .deb:

```yaml
packages:
  name: Build .deb Package  # Changed from "Build Packages (.deb and .rpm)"
  # ... only build .deb, remove all RPM build steps
```

**Correct Architecture**:
- **AUR job**: Builds Arch packages (runs on Arch via Docker)
- **COPR job**: Builds RPM packages (runs on Fedora infrastructure)
- **PPA job**: Builds Debian packages (runs on Ubuntu/Launchpad)  
- **Packages job**: Builds .deb for direct GitHub download (Ubuntu is fine)
- **Docker job**: Builds container images (distro-agnostic)

**Prevention**:
- Ubuntu can build .deb natively
- Fedora (COPR) should build .rpm natively
- Don't try to build distro-specific packages on the wrong distro
- If you need RPMs as GitHub release assets, download them from COPR after it builds

---

## PPA Rejects Upload: Version Comparison with Hyphens

**The Problem**: Launchpad PPA rejects the upload with "Version older than that in the archive" even though the new version has a later timestamp.

**Root Cause**: Debian version string comparison treats hyphens differently than dots. The version `0.24.1-dev-20251209-214612` is considered OLDER than `0.24.1-dev.202512092002` because of how dpkg compares version strings.

**Error Message**:
```
Rejected: slskdn-dev_0.24.1-dev-20251209-214612-1ppa202512092148~jammy.dsc: 
Version older than that in the archive. 
0.24.1-dev-20251209-214612-1ppa202512092148~jammy <= 0.24.1-dev.202512092002-1ppa202512092006~jammy
```

**Why This Breaks**:
Debian's `dpkg --compare-versions` treats hyphens as version separators, not as part of the version string:
- `0.24.1-dev-20251209-214612` is parsed as epoch `0`, version `0.24.1`, and the rest as debian revision
- `0.24.1-dev.202512092002` with dots keeps the full version number intact
- The comparison logic makes the hyphenated version appear older

**The Fix**:
Convert ALL hyphens to dots in the PPA version string:

```bash
VERSION="${{ needs.build.outputs.dev_version }}"  # 0.24.1-dev-20251209-214612
DEB_VERSION=$(echo "$VERSION" | sed 's/-/./g')    # 0.24.1.dev.20251209.214612

# Use DEB_VERSION in changelog
slskdn-dev (${DEB_VERSION}-1ppa${PPA_REV}~jammy) jammy; urgency=medium
```

**Critical**: This is the SAME issue as the AUR/RPM version problem, but it manifests differently - not as a build error, but as a PPA rejection during upload. You MUST convert hyphens to dots for ALL Debian-based packaging (AUR, RPM, DEB, PPA).

**Prevention**:
- ALWAYS use `sed 's/-/./g'` (global replace) for ANY package version strings
- Check EVERY place where `$VERSION` or `dev_version` is used in packaging workflows
- Test PPA uploads don't get rejected with "Version older than that in the archive"

---

*Last updated: 2025-12-09*

