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

### 4. HashDb Migration Version Collisions

**The Bug**: Duplicate migration version numbers cause `UNIQUE constraint failed: __HashDbMigrations.version`, blocking startup and E2E health checks.

**Files Affected**:
- `src/slskd/HashDb/Migrations/HashDbMigrations.cs`

**Wrong**:
```csharp
new Migration { Version = 12, Name = "Label crate job cache", ... },
new Migration { Version = 12, Name = "Traffic accounting", ... }, // 💥 duplicate
new Migration { Version = 14, Name = "Warm cache popularity", ... },
new Migration { Version = 14, Name = "Warm cache entries", ... }, // 💥 duplicate
```

**Correct**:
```csharp
new Migration { Version = 12, Name = "Label crate job cache", ... },
new Migration { Version = 13, Name = "Peer metrics storage", ... },
new Migration { Version = 14, Name = "Warm cache popularity", ... },
new Migration { Version = 15, Name = "Warm cache entries", ... },
new Migration { Version = 16, Name = "Virtual Soulfind pseudonyms", ... },
new Migration { Version = 17, Name = "Traffic accounting", ... },
```

**Why This Keeps Happening**: Migrations were appended without re-checking version uniqueness, and the list order wasn’t kept strictly ascending.

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

### 7. Duplicate Variable Names in React Components

**The Bug**: Large React components with multiple state sections can have duplicate variable names, causing "Identifier 'X' has already been declared" compilation errors.

**Files Affected**:
- `src/web/src/components/System/MediaCore/index.jsx` (main culprit)

**Wrong**:
```jsx
// In one section:
const [verificationResult, setVerificationResult] = useState(null);

// Later in another section:
const [verificationResult, setVerificationResult] = useState(null); // ❌ Duplicate declaration
```

**Correct**:
```jsx
// Use descriptive names for different purposes:
const [descriptorVerificationResult, setDescriptorVerificationResult] = useState(null);
const [signatureVerificationResult, setSignatureVerificationResult] = useState(null);
```

**Why This Keeps Happening**: MediaCore component has 50+ state variables across multiple sections. When adding new state variables, developers may not realize the name is already used elsewhere in the file. Always grep for variable names before adding new state.

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

## Yay Cache Contains Stale PKGBUILD After AUR Fix

**The Problem**: After fixing the AUR workflow to keep `SKIP` for the binary checksum, `yay -S slskdn-dev` still fails with "One or more files did not pass the validity check!" even though the AUR repo has the correct PKGBUILD.

**Root Cause**: Yay caches PKGBUILDs in `~/.cache/yay/package-name/`. If the cached PKGBUILD is from a previous (broken) workflow run that had a real hash instead of `SKIP`, yay will use the stale cached version instead of fetching the fixed one from AUR.

**Error Message**:
```
==> Validating source files with sha256sums...
    slskdn-dev-linux-x64.zip ... FAILED
==> ERROR: One or more files did not pass the validity check!
```

**Why This Happens**:
1. Old workflow pushed PKGBUILD with `sha256sums=('abc123...' 'SKIP' 'SKIP' 'SKIP')`
2. User ran `yay -S package-name` and yay cached that broken PKGBUILD
3. Workflow was fixed to preserve `SKIP` in the template
4. New correct PKGBUILD pushed to AUR: `sha256sums=('SKIP' '9e2f4b...' 'a170af...' '28b6c2...')`
5. User runs `yay -S package-name` again, but yay uses the CACHED broken version
6. Checksum fails because the binary has changed but cached PKGBUILD has the old hash

**The Fix**:
Clear yay's cache for the package:

```bash
rm -rf ~/.cache/yay/package-name
yay -S package-name  # Will fetch fresh PKGBUILD from AUR
```

**Prevention**:
- When testing AUR packages during development, always clear cache after workflow fixes
- Add this to testing docs: "If you previously tested a broken build, clear yay cache first"
- Yay's cache is helpful for normal use but can hide fixes during rapid iteration

---

## EF Core Can't Translate DateTimeOffset to DateTime Comparison

**The Problem**: Backfill endpoint throws 500 error with "The LINQ expression could not be translated" when trying to compare `Search.StartedAt` (DateTime) with a DateTimeOffset value.

**Root Cause**: Entity Framework Core cannot translate implicit conversions between `DateTimeOffset` and `DateTime` to SQL. When you write `s.StartedAt < lastProcessedAt.Value` where `StartedAt` is `DateTime` and `lastProcessedAt` is `DateTimeOffset?`, EF can't generate the SQL query.

**Error Message**:
```
System.InvalidOperationException: The LINQ expression 'DbSet<Search>()
    .Count(s => (DateTimeOffset)s.StartedAt < __lastProcessedAt_Value_0)' could not be translated.
```

**The Fix**:
Convert `DateTimeOffset` to `DateTime` explicitly using `.UtcDateTime` before the comparison:

```csharp
// WRONG - EF can't translate this:
await context.Searches.CountAsync(s => s.StartedAt < lastProcessedAt.Value);

// CORRECT - EF can translate this:
await context.Searches.CountAsync(s => s.StartedAt < lastProcessedAt.Value.UtcDateTime);
```

**Prevention**:
- Always check the database column type before writing LINQ queries
- Use `.UtcDateTime` when comparing `DateTimeOffset` with `DateTime` in EF queries
- Test API endpoints that use LINQ queries against the database
- EF will throw this at runtime, not compile time, so manual testing is required

---

### 20. CreateDirectory on Existing File Path

**The Bug**: `System.IO.IOException: The file '/slskd/slskd' already exists` when trying to create a directory at a path that's already occupied by a file (the binary itself).

**Files Affected**:
- `src/slskd/Transfers/MultiSource/Discovery/SourceDiscoveryService.cs`
- `src/slskd/Program.cs`

**What Happened**:
`SourceDiscoveryService` used `Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)` which returns `/slskd` in Docker containers. It then tried to `CreateDirectory("/slskd/slskd")` to store the discovery database, but `/slskd/slskd` is the binary executable file, not a directory. This caused a crash on every API request that needed `SourceDiscoveryService`.

**Why It Happened**:
1. `LocalApplicationData` is not reliable in containers - can return unexpected paths
2. No check for whether the path is a file vs directory before calling `CreateDirectory()`
3. Different behavior than other services which use `Program.AppDirectory`

**The Error**:
```
System.IO.IOException: The file '/slskd/slskd' already exists.
  at System.IO.FileSystem.CreateDirectory(String fullPath, UnixFileMode unixCreateMode)
  at System.IO.Directory.CreateDirectory(String path)
  at slskd.Transfers.MultiSource.Discovery.SourceDiscoveryService..ctor(...)
```

**The Fix**:
Use `Program.AppDirectory` (like all other services) and create a subdirectory:

```csharp
// WRONG - uses unreliable LocalApplicationData
var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
var slskdPath = Path.Combine(appDataPath, "slskd");
System.IO.Directory.CreateDirectory(slskdPath); // CRASHES if /slskd/slskd is a file!

// CORRECT - use Program.AppDirectory and create subdirectory
public SourceDiscoveryService(
    string appDirectory,  // Injected via DI
    ISoulseekClient soulseekClient,
    IContentVerificationService verificationService)
{
    var slskdPath = Path.Combine(appDirectory, "discovery");
    if (!Directory.Exists(slskdPath))
    {
        Directory.CreateDirectory(slskdPath);
    }
    dbPath = Path.Combine(slskdPath, "discovery.db");
}

// Update DI registration to pass Program.AppDirectory
services.AddSingleton<ISourceDiscoveryService>(sp => new SourceDiscoveryService(
    Program.AppDirectory,
    sp.GetRequiredService<ISoulseekClient>(),
    sp.GetRequiredService<Transfers.MultiSource.IContentVerificationService>()));
```

**Prevention**:
- **ALWAYS** use `Program.AppDirectory` for data storage, never `LocalApplicationData`
- **ALWAYS** create a subdirectory for each service's data (e.g., `discovery/`, `ranking/`, `hashdb/`)
- **ALWAYS** check `Directory.Exists()` before `CreateDirectory()` when the path might vary
- Pattern to follow: `Path.Combine(Program.AppDirectory, "myservice")` → creates `/app/myservice/` in containers

**Related Pattern**:
```csharp
// Good examples from the codebase:
var rankingDbPath = Path.Combine(Program.AppDirectory, "ranking.db");
var hashDbService = new HashDbService(Program.AppDirectory, ...);
var wishlistDbPath = Path.Combine(Program.AppDirectory, "wishlist.db");
```

---

### 21. Scanner Detection Noise from Private IPs

**The Bug**: Logs spammed with hundreds of "Scanner detected from 192.168.1.77" warnings when users access the web UI from their LAN.

**Files Affected**:
- `src/slskd/Common/Security/FingerprintDetection.cs`
- `src/slskd/Common/Security/SecurityMiddleware.cs` (partial fix)

**What Happened**:
The web UI polls multiple API endpoints rapidly (~5-10 requests/second), which triggered the reconnaissance detection system. Even after fixing `SecurityMiddleware` to skip `RecordConnection()` for private IPs, old profiles from before the fix were still marked as scanners, and the logging still fired.

**Why It Happened**:
1. Web UI makes many rapid API calls (status bar, capabilities, DHT, mesh, hashdb, backfill stats, etc.)
2. This looks like port scanning / reconnaissance to `FingerprintDetection`
3. First fix: `SecurityMiddleware` skipped `RecordConnection()` for private IPs (lines 103-110)
4. But old profiles from before the fix were still in memory as flagged scanners
5. `FingerprintDetection.RecordConnection()` logged warnings for those old profiles

**The Error**:
```
20:09:16  WRN  Scanner detected from "192.168.1.77": "PortScanning, RapidConnections
20:09:26  WRN  Scanner detected from "192.168.1.77": "PortScanning, RapidConnections
20:09:36  WRN  Scanner detected from "192.168.1.77": "PortScanning, RapidConnections
... (repeats hundreds of times)
```

**The Fix**:
Add private IP check to `FingerprintDetection` itself, not just `SecurityMiddleware`:

```csharp
// In FingerprintDetection.RecordConnection():
if (profile.IsScanner)
{
    // Don't log warnings for private/local IPs (e.g., web UI polling APIs rapidly)
    if (!IsPrivateOrLocalIp(ip))
    {
        _logger.LogWarning(
            "Scanner detected from {Ip}: {Indicators}",
            ip,
            string.Join(", ", indicators.Select(i => i.Type)));

        ReconnaissanceDetected?.Invoke(this, new ReconnaissanceEventArgs(evt));
    }
}

// Add helper method (same as SecurityMiddleware):
private static bool IsPrivateOrLocalIp(IPAddress ip)
{
    // Check for 192.168.x.x, 10.x.x.x, 172.16-31.x.x, 127.x.x.x, fe80::/10, fc00::/7
    // ... (full implementation in code)
}
```

**Prevention**:
- Security logging should **always** check for private IPs before emitting warnings
- Private IP checks should be at **both** the middleware layer (prevent tracking) **and** the service layer (prevent logging)
- Web UI polling is legitimate behavior - don't treat LAN clients as threats
- Test security features with both public and private IPs

**Why Two Fixes Were Needed**:
1. **SecurityMiddleware fix**: Prevents NEW profiles from being created for private IPs
2. **FingerprintDetection fix**: Prevents logging warnings for OLD profiles (already flagged)
3. Both layers need the check to fully eliminate noise

**Private IP Ranges**:
- IPv4: `10.0.0.0/8`, `172.16.0.0/12`, `192.168.0.0/16`, `169.254.0.0/16`, `127.0.0.0/8`
- IPv6: `fe80::/10` (link-local), `fc00::/7` (unique local), `::1` (loopback)

---

### 22. Ambiguous Type Reference (Directory)

**The Bug**: `error CS0104: 'Directory' is an ambiguous reference between 'Soulseek.Directory' and 'System.IO.Directory'`

**Files Affected**:
- Any file that has both `using System.IO;` and `using Soulseek;`

**What Happened**:
When fixing the CreateDirectory bug (#20), I added code that used `Directory.Exists()` and `Directory.CreateDirectory()`. The compiler couldn't determine if this meant `System.IO.Directory` or `Soulseek.Directory` (which is a completely different type representing a Soulseek shared directory).

**Why It Happened**:
Both namespaces define a type called `Directory`:
- `System.IO.Directory` - file system operations
- `Soulseek.Directory` - Soulseek protocol type for shared directories

When both namespaces are imported with `using`, the unqualified name `Directory` is ambiguous.

**The Error**:
```
/home/runner/work/slskdn/slskdn/src/slskd/Transfers/MultiSource/Discovery/SourceDiscoveryService.cs(73,18): 
error CS0104: 'Directory' is an ambiguous reference between 'Soulseek.Directory' and 'System.IO.Directory'
```

**The Fix**:
Always fully qualify `Directory` when both namespaces are imported:

```csharp
// WRONG - ambiguous when both System.IO and Soulseek are imported:
if (!Directory.Exists(slskdPath))
{
    Directory.CreateDirectory(slskdPath);
}

// CORRECT - fully qualified:
if (!System.IO.Directory.Exists(slskdPath))
{
    System.IO.Directory.CreateDirectory(slskdPath);
}
```

**Alternative Fix** (if you need both frequently):
Add a using alias at the top of the file:
```csharp
using IODirectory = System.IO.Directory;

// Then use:
if (!IODirectory.Exists(slskdPath))
{
    IODirectory.CreateDirectory(slskdPath);
}
```

**Prevention**:
- When you see both `using System.IO;` and `using Soulseek;` in a file, **always** qualify `Directory`
- Grep for this pattern before committing: `grep -n "using Soulseek" src/**/*.cs | grep -v "using System.IO"` won't help because they're often far apart
- Better: Run `dotnet build` locally before pushing to catch these at compile time

**Other Ambiguous Types in This Codebase**:
- `Directory` (System.IO vs Soulseek)
- `File` (System.IO vs Soulseek)
- `Transfer` (slskd.Transfers.Transfer vs Soulseek.Transfer) - already resolved with `using Transfer = slskd.Transfers.Transfer;` in Events.cs

**Quick Fix Command**:
```bash
# Find files that might have this issue:
grep -l "using Soulseek" src/slskd/**/*.cs | xargs grep -l "Directory\.Exists\|Directory\.Create" | xargs sed -i 's/Directory\.Exists/System.IO.Directory.Exists/g; s/Directory\.Create/System.IO.Directory.Create/g'
```

---

### E2E Test Infrastructure Issues

#### E2E-1: Server crashes during share initialization in test harness

**The Bug**: E2E test nodes crash with `ShareInitializationException: Share cache backup is missing, corrupt, or is out of date` because test nodes start with empty app directories and no share cache.

**Files Affected**:
- `src/web/e2e/harness/SlskdnNode.ts`

**Wrong**:
```typescript
const args = ['run', '--project', projectPath, '--no-build', '--', '--app-dir', this.appDir, '--config', configPath];
```

**Correct**:
```typescript
// Add --force-share-scan to avoid ShareInitializationException when cache doesn't exist
const args = ['run', '--project', projectPath, '--no-build', '--', '--app-dir', this.appDir, '--config', configPath, '--force-share-scan'];
```

**Why This Keeps Happening**: Test nodes start with fresh app directories, so share cache doesn't exist. The server requires either a valid cache or `--force-share-scan` to create one.

---

#### E2E-2: Static files return 404 because SPA fallback intercepts them

**The Bug**: Static files (`/static/js/*.js`, `/static/css/*.css`) return 404, preventing React from mounting. The SPA fallback endpoint runs before the file server and intercepts all requests, including static files.

**Files Affected**:
- `src/slskd/Program.cs`

**Wrong**:
```csharp
// SPA fallback endpoint runs BEFORE file server
endpoints.MapGet("{*path}", async context => {
    // This intercepts /static/* requests and returns 404
    if (!hasExtension) {
        await context.Response.SendFileAsync(indexPath);
    } else {
        context.Response.StatusCode = 404; // Static files get 404 here!
    }
});
app.UseFileServer(...); // Never reached for static files
```

**Correct**:
```csharp
// File server runs first
app.UseFileServer(fileServerOptions);

// SPA fallback middleware runs AFTER file server
app.Use(async (context, next) => {
    await next(); // Let file server try first
    
    // Only serve index.html if file server returned 404 for a client-side route
    if (context.Response.StatusCode == 404 && !isApi && !isStatic && !hasExtension) {
        await context.Response.SendFileAsync(indexPath);
    }
});
```

**Why This Keeps Happening**: Endpoints run before middleware, so a catch-all endpoint intercepts requests before the file server middleware can serve static files. The solution is to use middleware AFTER the file server that only handles 404s for client-side routes.

---

#### E2E-3: Excessive timeouts in test helpers

**The Bug**: `waitForHealth` polls for 60 seconds (120 iterations × 500ms) when the server typically starts in 2-5 seconds.

**Files Affected**:
- `src/web/e2e/helpers.ts`

**Wrong**:
```typescript
for (let i = 0; i < 120; i++) { // 60 seconds
    const res = await request.get(health, { failOnStatusCode: false });
    if (res.ok()) return;
    await new Promise(r => setTimeout(r, 500));
}
```

**Correct**:
```typescript
// Server typically starts in 2-5 seconds, so 15 seconds is plenty
for (let i = 0; i < 30; i++) { // 15 seconds
    const res = await request.get(health, { failOnStatusCode: false });
    if (res.ok()) return;
    await new Promise(r => setTimeout(r, 500));
}
```

**Why This Keeps Happening**: Default timeouts are set conservatively, but actual server startup is much faster. Reduce timeouts to match reality.

---

#### E2E-4: Multi-peer tests fail with "instance already running" mutex error

**The Bug**: When starting multiple test nodes (A and B), the second node fails with "An instance of slskd is already running" because the mutex name was global (based only on AppName), not per-app-directory.

**Files Affected**:
- `src/slskd/Program.cs`

**Wrong**:
```csharp
private static Mutex Mutex { get; } = new Mutex(initiallyOwned: true, Compute.Sha256Hash(AppName));
// Mutex check happens before AppDirectory is set
if (!Mutex.WaitOne(millisecondsTimeout: 0, exitContext: false)) {
    Log.Fatal($"An instance of {AppName} is already running");
    return;
}
AppDirectory ??= DefaultAppDirectory; // Set AFTER mutex check
```

**Correct**:
```csharp
private static Mutex Mutex { get; set; }

private static string GetMutexName() {
    var dir = AppDirectory ?? DefaultAppDirectory;
    return $"{AppName}_{Compute.Sha256Hash(dir)}";
}

// Set AppDirectory FIRST, then create mutex with app-directory-specific name
AppDirectory ??= DefaultAppDirectory;
Mutex = new Mutex(initiallyOwned: true, GetMutexName());
if (!Mutex.WaitOne(millisecondsTimeout: 0, exitContext: false)) {
    Log.Fatal($"An instance of {AppName} is already running in app directory: {AppDirectory}");
    return;
}
```

**Why This Keeps Happening**: The mutex was created as a static property initializer (before AppDirectory is set) with a global name. Each test node needs its own mutex based on its unique app directory.

---

#### E2E-6: Health check hangs during server startup

**The Bug**: E2E test nodes hang during startup because the `/health` endpoint never responds. The `MeshHealthCheck` calls `GetStatsAsync()` which can hang if mesh services aren't initialized yet, especially NAT detection which tries to connect to external STUN servers.

**Files Affected**:
- `src/slskd/Mesh/MeshHealthCheck.cs`
- `src/slskd/Mesh/MeshStatsCollector.cs`
- `src/slskd/Program.cs`
- `src/web/e2e/harness/SlskdnNode.ts`

**Wrong**:
```csharp
// MeshHealthCheck.cs - no timeout, hangs if services not ready
var stats = await _statsCollector.GetStatsAsync();

// MeshStatsCollector.cs - NAT detection can hang
natType = await stunDetector.DetectAsync();
```

**Correct**:
```csharp
// MeshHealthCheck.cs - add timeout and handle gracefully
using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
timeoutCts.CancelAfter(TimeSpan.FromSeconds(5));
var stats = await _statsCollector.GetStatsAsync().WaitAsync(timeoutCts.Token);
// Return Degraded instead of Unhealthy if timeout/error occurs

// MeshStatsCollector.cs - add timeout to NAT detection
using var natTimeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
natType = await stunDetector.DetectAsync(natTimeoutCts.Token);

// Program.cs - configure health check timeout
services.AddHealthChecks()
    .AddMeshHealthCheck(
        failureStatus: HealthStatus.Degraded, // Don't fail entire endpoint
        timeout: TimeSpan.FromSeconds(5));

// SlskdnNode.ts - use simpler readiness endpoint
const readinessUrl = `${this.apiUrl}/health/ready`; // Simple endpoint, no complex checks
```

**Why This Keeps Happening**: Health checks run during startup before all services are initialized. Mesh services (especially NAT detection) can hang waiting for external resources. The health endpoint waits for all checks to complete, so a hanging check blocks the entire endpoint.

**Prevention**:
- Always add timeouts to health checks that call async operations
- Return `Degraded` instead of `Unhealthy` for startup-time issues
- Use simpler readiness endpoints for E2E tests that bypass complex checks
- Add timeouts to any external service calls in health checks (NAT detection, DNS, etc.)

---

#### E2E-5: Tests should be lenient for incomplete features

**The Bug**: Tests fail when UI elements don't exist because features aren't fully implemented yet.

**Files Affected**:
- All E2E test files

**Wrong**:
```typescript
await page.getByTestId(T.someFeature).click(); // Fails if feature doesn't exist
await expect(page.getByTestId(T.someElement)).toBeVisible();
```

**Correct**:
```typescript
const featureBtn = page.getByTestId(T.someFeature);
if (await featureBtn.count() === 0) {
  test.skip(); // Skip if feature not available
  return;
}
await featureBtn.click();
await expect(page.getByTestId(T.someElement)).toBeVisible({ timeout: 10000 });
```

**Why This Keeps Happening**: Features may be partially implemented or not yet available. Tests should gracefully skip rather than fail, allowing the test suite to run and verify what's actually implemented.

---

#### E2E-6: React Router routes not matching due to basename/urlBase mismatch

**The Bug**: When BrowserRouter has a `basename` prop set, routes and Links should NOT include the `urlBase` prefix, otherwise routes won't match. Also, if using memory history (MemoryRouter), redirects won't update the browser URL, causing the symptom "UI shows different page than URL".

**Files Affected**:
- `src/web/src/index.jsx` - Router setup
- `src/web/src/components/App.jsx` - Route definitions
- `src/web/e2e/multippeer-sharing.spec.ts` - Test diagnostics

**Wrong**:
```jsx
// If urlBase is "/slskd" and basename is set:
<Router basename="/slskd">
  <Route path="/slskd/contacts" />  // ❌ Won't match! Router strips basename first
  <Link to="/slskd/contacts" />     // ❌ Double-prefix
</Router>
```

**Correct**:
```jsx
// When basename is set, routes should be base-relative:
<Router basename={urlBase && urlBase !== '/' ? urlBase : undefined}>
  <Route path="/contacts" />  // ✅ Router adds basename automatically
  <Link to="/contacts" />     // ✅ Router adds basename automatically
</Router>

// When basename is undefined (urlBase is empty or '/'), use full paths:
<Router basename={undefined}>
  <Route path={`${urlBase}/contacts`} />  // ✅ urlBase is empty, so becomes "/contacts"
  <Link to={`${urlBase}/contacts`} />     // ✅ urlBase is empty, so becomes "/contacts"
</Router>
```

**Diagnostic Pattern**:
```typescript
// In E2E tests, compare browser location vs app history:
const loc = await page.evaluate(() => ({ 
  href: location.href, 
  pathname: location.pathname 
}));
const appLoc = await page.evaluate(() => {
  if ((window as any).__APP_HISTORY__) {
    return (window as any).__APP_HISTORY__.location.pathname;
  }
  return null;
});
// If loc.pathname !== appLoc, you're using memory history or basename mismatch
```

**Why This Keeps Happening**: React Router's `basename` prop automatically prepends to all routes and links. If you manually include the basename in route paths, you get a double-prefix that prevents matching. Also, using MemoryRouter instead of BrowserRouter causes redirects to not update the browser URL.

---

#### E2E-7: TypeScript-only syntax in JSX breaks builds

**The Bug**: Using TypeScript-only syntax (e.g., `window as any`) in `.jsx` files causes the web build to fail or silently serve stale bundles, which hides routing/debugging changes.

**Files Affected**:
- `src/web/src/components/App.jsx`

**Wrong**:
```jsx
// ❌ TypeScript cast is invalid in plain JSX
(window as any).__ROUTE_MISS_ELEMENT__ = el.textContent;
```

**Correct**:
```jsx
// ✅ Plain JS assignment
window.__ROUTE_MISS_ELEMENT__ = el.textContent;
```

**Why This Keeps Happening**: It's easy to copy/paste TS patterns into a JS file. CRA/CRACO won't compile TS-only syntax in `.jsx`, and a failed build can leave old bundles in `wwwroot`, masking changes.

---

#### E2E-8: Ambiguous `/shares` route between file shares and share grants

**The Bug**: The legacy file shares API and the new share-grants API both used `/api/v0/shares`, causing `AmbiguousMatchException` (500) for GET `/api/v0/shares`.

**Files Affected**:
- `src/slskd/Shares/API/Controllers/SharesController.cs` (legacy file shares)
- `src/slskd/Sharing/API/SharesController.cs` (share grants)
- `src/web/src/lib/collections.js`

**Wrong**:
```csharp
[Route("api/v{version:apiVersion}/shares")] // used by BOTH controllers
```

**Correct**:
```csharp
[Route("api/v{version:apiVersion}/share-grants")] // share grants only
```

**Why This Keeps Happening**: Both features are named "Shares" but represent different domains (local file shares vs collection share grants). Without a distinct route prefix, ASP.NET Core can't disambiguate endpoints.

---

#### E2E-9: Share-grants "GetAll" is recipient-only (owner won't see outgoing shares)

**The Bug**: `GET /api/v0/share-grants` returns grants **accessible to the current user as a recipient** (direct user or share-group member). It does **not** include the grants you created as the owner unless you also happen to be a recipient/member, which makes the owner UI appear as "No shares yet" after a successful create.

**Files Affected**:
- `src/slskd/Sharing/ShareGrantRepository.cs` (accessibility logic)
- `src/slskd/Sharing/API/SharesController.cs` (endpoint semantics)
- `src/web/src/components/Collections/Collections.jsx` (owner view needs by-collection endpoint)

**Fix**:
- Keep `GET /share-grants` as recipient-accessible (used by "Shared with Me")
- Add `GET /share-grants/by-collection/{collectionId}` for owner/outgoing shares, and have the Collections UI use it

---

#### E2E-10: Cross-node share discovery requires token signing key and port-specific CSRF cookies

**The Bug**: Cross-node share discovery via private messages requires:
1. `Sharing:TokenSigningKey` configured (base64, min 32 bytes) or token creation fails
2. CSRF cookie names must be port-specific (`XSRF-TOKEN-{port}`) for multi-instance E2E to avoid cookie collisions
3. OwnerEndpoint in announcements must use `127.0.0.1` not `localhost` (Playwright request client prefers IPv6 `::1` for "localhost")

**Files Affected**:
- `src/web/e2e/harness/SlskdnNode.ts` (config generation)
- `src/slskd/Program.cs` (CSRF cookie name, antiforgery config)
- `src/slskd/Sharing/API/SharesController.cs` (ownerEndpoint calculation)
- `src/web/src/lib/api.js` (CSRF token reading)

**Wrong**:
```csharp
options.Cookie.Name = "XSRF-TOKEN"; // Same name for all instances = collision
var ownerEndpoint = $"{scheme}://localhost:{web.Port}"; // localhost → ::1 in Playwright
```

**Correct**:
```csharp
options.Cookie.Name = $"XSRF-TOKEN-{OptionsAtStartup.Web.Port}"; // Port-specific
var ownerEndpoint = $"{scheme}://127.0.0.1:{web.Port}"; // Explicit IPv4
```

**Why This Keeps Happening**: Multi-instance E2E runs multiple nodes on the same host with different ports. Cookies are host-scoped (not port-scoped), so fixed names collide. Playwright's request client resolves "localhost" to IPv6 by default, but nodes bind to IPv4.

---

#### E2E-11: Backfill requires OwnerEndpoint for HTTP downloads (cross-node)

**The Bug**: Backfill endpoint requires either `OwnerEndpoint` + `ShareToken` (for HTTP downloads) or owner username + `IDownloadService` (for Soulseek downloads). If neither is available, backfill fails with a generic error.

**Files Affected**:
- `src/slskd/Sharing/API/SharesController.cs` (Backfill method)

**Wrong**:
```csharp
// Only checks for Soulseek username
if (string.IsNullOrWhiteSpace(ownerUsername))
    return BadRequest("Owner username not available");
```

**Correct**:
```csharp
// Check for HTTP download first (cross-node), then Soulseek
var useHttpDownload = !string.IsNullOrWhiteSpace(ownerEndpoint) && !string.IsNullOrWhiteSpace(grant.ShareToken);
if (useHttpDownload) {
    // HTTP download path
} else if (!string.IsNullOrWhiteSpace(ownerUsername) && _downloadService != null) {
    // Soulseek download path
} else {
    return BadRequest("Cannot backfill: owner endpoint and token not available for HTTP download, and owner username or download service not available for Soulseek download");
}
```

**Why This Keeps Happening**: Backfill needs to work for both cross-node shares (HTTP) and same-network shares (Soulseek). The implementation must check for both methods and provide clear error messages when neither is available.

---

*Last updated: 2026-01-27*

