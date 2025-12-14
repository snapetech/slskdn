# ADR-0002: Code Patterns & Anti-Slop Guide

> **Status**: Active  
> **Date**: 2025-12-08  
> **Purpose**: Prevent AI-generated slop by documenting exact patterns to follow

This document captures the **exact patterns** used in this codebase. Follow these precisely - don't invent new patterns.

---

## Backend (C#) Patterns

### 1. Service Class Structure

**Pattern**: Interface + Implementation in same namespace, constructor injection.

```csharp
// CORRECT - matches existing services
namespace slskd.Transfers
{
    public interface IMyService
    {
        Task DoSomethingAsync();
    }

    public class MyService : IMyService
    {
        public MyService(
            IOptionsMonitor<Options> optionsMonitor,
            ISoulseekClient soulseekClient,
            ILogger<MyService> logger)
        {
            OptionsMonitor = optionsMonitor;
            Client = soulseekClient;
            Logger = logger;
        }

        private IOptionsMonitor<Options> OptionsMonitor { get; }
        private ISoulseekClient Client { get; }
        private ILogger Logger { get; }

        public async Task DoSomethingAsync()
        {
            var options = OptionsMonitor.CurrentValue;
            // ...
        }
    }
}
```

**WRONG patterns to avoid**:
```csharp
// WRONG: Don't use _privateField for properties (upstream uses PascalCase)
private readonly ILogger _logger;  // ❌ Only for NEW slskdN services

// WRONG: Don't use field injection
[Inject] public ILogger Logger { get; set; }  // ❌ Never

// WRONG: Don't use static Log
private static readonly ILogger Log = Serilog.Log.ForContext<MyService>();  // ❌ Legacy
```

### 2. Options Access

**Singleton services**: Use `IOptionsMonitor<Options>` (tracks changes)
**Scoped/Transient services**: Use `IOptionsSnapshot<Options>` (per-request)
**Startup-only values**: Use `OptionsAtStartup` (immutable)

```csharp
// CORRECT
public class MySingletonService
{
    public MySingletonService(IOptionsMonitor<Options> optionsMonitor)
    {
        OptionsMonitor = optionsMonitor;
    }

    private IOptionsMonitor<Options> OptionsMonitor { get; }

    public void DoWork()
    {
        // Always get CurrentValue, don't cache it
        var downloadSlots = OptionsMonitor.CurrentValue.Global.Download.Slots;
    }
}
```

### 3. DI Registration in Program.cs

**Pattern**: Register in `ConfigureDependencyInjectionContainer()` method.

```csharp
// Location: Program.cs, inside ConfigureDependencyInjectionContainer()

// For singletons (most services)
services.AddSingleton<IMyService, MyService>();

// For hosted services (background workers)
services.AddSingleton<MyBackgroundService>();
services.AddHostedService(provider => provider.GetRequiredService<MyBackgroundService>());

// For DbContext with factory
var dbPath = Path.Combine(Program.AppDirectory, "myfeature.db");
services.AddDbContextFactory<MyDbContext>(options =>
{
    options.UseSqlite($"Data Source={dbPath}");
});

// Ensure database created
using (var context = new MyDbContext(
    new DbContextOptionsBuilder<MyDbContext>()
        .UseSqlite($"Data Source={dbPath}")
        .Options))
{
    context.Database.EnsureCreated();
}
```

### 4. API Controller Structure

```csharp
namespace slskd.MyFeature.API
{
    [Route("api/v0/[controller]")]
    [ApiController]
    [Produces("application/json")]
    [Consumes("application/json")]
    public class MyFeatureController : ControllerBase
    {
        public MyFeatureController(IMyService myService)
        {
            MyService = myService;
        }

        private IMyService MyService { get; }

        /// <summary>
        ///     Gets something.
        /// </summary>
        [HttpGet]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(typeof(MyResponse), 200)]
        public async Task<IActionResult> Get()
        {
            var result = await MyService.GetAsync();
            return Ok(result);
        }
    }
}
```

### 5. Async Event Handlers

**Pattern**: Always wrap in try-catch, log errors.

```csharp
// CORRECT - matches Application.cs pattern
private async void Client_SomeEvent(object sender, EventArgs e)
{
    try
    {
        await DoSomethingAsync();
    }
    catch (Exception ex)
    {
        Logger.Error(ex, "Failed to handle event");
    }
}
```

### 6. File-Scoped Namespaces

**New slskdN files**: Use file-scoped namespaces (C# 10+)
**Existing upstream files**: Keep block-scoped (don't change)

```csharp
// NEW slskdN file - use file-scoped
namespace slskd.MyFeature;

public class MyClass { }

// EXISTING upstream file - keep as-is
namespace slskd.Transfers
{
    public class ExistingClass { }
}
```

---

## Core Principles

### 6. Network Health First

**All features must consider Soulseek network health.** slskdn is a good network citizen.

- **Rate limit** any operations that contact remote peers (browsing, header probing, hash discovery)
- **Prefer manual triggers** over automatic aggressive scanning (e.g., "Backfill from History" button)
- **Be conservative** with bandwidth - the BackfillScheduler intentionally throttles discovery
- **Don't overwhelm peers** - space out requests, respect failures, back off on errors

```csharp
// CORRECT - conservative, user-triggered
[HttpPost("backfill/from-history")]
public async Task<IActionResult> BackfillFromSearchHistory()
{
    // User explicitly requested this - it's okay to process history
    var count = await HashDb.BackfillFromSearchHistoryAsync(searchContextFactory);
    return Ok(new { message = $"Added {count} FLAC entries to inventory." });
}

// WRONG - aggressive automatic scanning
protected override async Task ExecuteAsync(CancellationToken ct)
{
    while (!ct.IsCancellationRequested)
    {
        await ScanAllPeersAggressively();  // ❌ Don't do this
        await Task.Delay(TimeSpan.FromSeconds(1), ct);
    }
}
```

### 6b. UI Buttons Must Have Tooltips

**Every button should have a helpful mouseover tooltip** explaining what it does and why.

```jsx
// CORRECT - helpful tooltip
<Popup
  content="Scan your search history to discover FLAC files from past searches. This populates the inventory with files that can be probed for content hashes."
  position="top right"
  trigger={
    <Button onClick={handleBackfill}>Backfill from History</Button>
  }
/>

// WRONG - no explanation
<Button onClick={handleBackfill}>Backfill</Button>
```

---

## Frontend (React/JSX) Patterns

### 7. Component Structure

```jsx
// CORRECT pattern from existing components
import './MyComponent.css';
import * as myLibrary from '../../lib/myFeature';
import { LoaderSegment, PlaceholderSegment } from '../Shared';
import React, { useEffect, useState } from 'react';
import { toast } from 'react-toastify';

const MyComponent = ({ someProp }) => {
  const [loading, setLoading] = useState(true);
  const [data, setData] = useState([]);

  const fetch = async () => {
    try {
      const response = await myLibrary.getAll();
      setData(response);
    } catch (error) {
      console.error(error);
      toast.error(error?.response?.data ?? error?.message ?? error);
    }
  };

  useEffect(() => {
    const init = async () => {
      await fetch();
      setLoading(false);
    };

    init();
    const interval = window.setInterval(fetch, 1_000);

    return () => clearInterval(interval);
  }, []); // eslint-disable-line react-hooks/exhaustive-deps

  if (loading) return <LoaderSegment />;
  if (!data.length) return <PlaceholderSegment text="No data" />;

  return (
    <div className="my-component">
      {/* ... */}
    </div>
  );
};

export default MyComponent;
```

### 8. API Library Functions

```javascript
// src/web/src/lib/myFeature.js
import api from './api';

// CORRECT - always return safe values
export const getAll = async () => {
  const response = (await api.get('/myfeature')).data;

  if (!Array.isArray(response)) {
    console.warn('got non-array response from myfeature API', response);
    return [];  // ✅ ALWAYS return [], never undefined
  }

  return response;
};

export const getById = async ({ id }) => {
  return (await api.get(`/myfeature/${encodeURIComponent(id)}`)).data;
};

export const create = async (data) => {
  return api.post('/myfeature', data);
};

export const remove = async ({ id }) => {
  return api.delete(`/myfeature/${encodeURIComponent(id)}`);
};
```

### 9. localStorage Patterns

```javascript
// CORRECT - from Browse.jsx
const STORAGE_KEY = 'slskd-myfeature-state';

const loadFromStorage = () => {
  try {
    const saved = localStorage.getItem(STORAGE_KEY);
    if (saved) {
      return JSON.parse(saved);
    }
  } catch {
    // ignore parse errors
  }
  return null;  // or default value
};

const saveToStorage = (data) => {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(data));
  } catch {
    // ignore quota errors
  }
};
```

### 10. Error Handling Pattern

```javascript
// CORRECT - from Transfers.jsx
try {
  await someAsyncOperation();
} catch (error) {
  console.error(error);
  toast.error(error?.response?.data ?? error?.message ?? error);
}
```

### 11. ESLint Rules - NEVER CREATE LINTING ERRORS

**CRITICAL**: AI agents MUST follow ALL ESLint rules. Don't disable linting to "work around" errors - FIX THE CODE.

#### Common Rules to Follow:

**prettier/prettier**: Code formatting MUST match exactly
```javascript
// WRONG - long lines must wrap
const result = this.withTokenCheck(<MyComponent prop1="value1" prop2="value2" prop3="value3" />);

// CORRECT - wrap at logical points
const result = this.withTokenCheck(
  <MyComponent prop1="value1" prop2="value2" prop3="value3" />
);
```

**padding-line-between-statements**: Blank line before return/if/etc
```javascript
// WRONG
const result = calculate();
return result;

// CORRECT
const result = calculate();

return result;
```

**react/no-access-state-in-setstate**: Use functional setState when referencing previous state
```javascript
// WRONG
this.setState({ count: this.state.count + 1 });

// CORRECT
this.setState((prevState) => ({ count: prevState.count + 1 }));
```

**react/jsx-sort-props**: Props must be alphabetically sorted
```javascript
// WRONG
<Button onClick={handleClick} disabled={false} primary />

// CORRECT
<Button disabled={false} onClick={handleClick} primary />
```

**canonical/sort-keys**: Object keys must be alphabetically sorted
```javascript
// WRONG
this.state = {
  loading: false,
  data: [],
  error: null,
};

// CORRECT
this.state = {
  data: [],
  error: null,
  loading: false,
};
```

**no-unused-vars**: Remove unused imports
```javascript
// WRONG
import { Button, Card, Icon } from 'semantic-ui-react'; // Icon unused

// CORRECT
import { Button, Card } from 'semantic-ui-react';
```

**unicorn/prevent-abbreviations**: Use full words, not abbreviations
```javascript
// WRONG
const prevState = this.state;
const prevProps = this.props;

// CORRECT
const previousState = this.state;
const previousProps = this.props;
```

**unicorn/no-lonely-if**: Don't nest single if statements
```javascript
// WRONG
if (condition1) {
  if (condition2) {
    doSomething();
  }
}

// CORRECT
if (condition1 && condition2) {
  doSomething();
}
```

**require-atomic-updates**: Avoid race conditions in async functions
```javascript
// WRONG
conversations = await fetchConversations(); // Can race

// CORRECT
const newConversations = await fetchConversations();
conversations = newConversations;
```

#### Before Committing Frontend Code:

1. **Run the build** with linting enabled: `npm run build`
2. **Fix ALL errors** - don't commit if there are linting errors
3. **Never use `DISABLE_ESLINT_PLUGIN=true`** except for temporary local testing

#### If You See Linting Errors:

1. **READ the error message** - it tells you exactly what to fix
2. **Look at existing code** - find similar patterns and copy the style
3. **Fix the code** - don't disable the rule
4. **Run build again** - verify it passes

**Remember**: Linting errors waste time. Get it right the first time by following these patterns.

---

## Directory Structure

### Backend

```
src/slskd/
├── MyFeature/
│   ├── API/
│   │   └── MyFeatureController.cs
│   ├── IMyService.cs          # Interface
│   ├── MyService.cs           # Implementation
│   └── Types/                 # DTOs, models
│       └── MyModel.cs
```

### Frontend

```
src/web/src/
├── components/
│   └── MyFeature/
│       ├── index.jsx          # Main component
│       ├── MyFeature.css      # Styles
│       └── SubComponent.jsx   # Child components
├── lib/
│   └── myFeature.js           # API functions
```

---

## Database Patterns

### SQLite with EF Core

```csharp
// DbContext
public class MyDbContext : DbContext
{
    public MyDbContext(DbContextOptions<MyDbContext> options)
        : base(options) { }

    public DbSet<MyEntity> MyEntities { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MyEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.SomeField);
        });
    }
}

// Entity
public class MyEntity
{
    public int Id { get; set; }
    public string SomeField { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
```

### Raw SQLite (for performance-critical code)

```csharp
// Use Dapper for raw SQL when EF is too slow
using var connection = new SqliteConnection(connectionString);
var results = await connection.QueryAsync<MyDto>(
    "SELECT * FROM MyTable WHERE SomeField = @Value",
    new { Value = someValue });
```

### HashDb Schema Migrations

The HashDb uses a versioned migration system. See `docs/HASHDB_SCHEMA.md` for full documentation.

```csharp
// Adding a new migration in HashDbMigrations.cs
new Migration
{
    Version = 3,  // Increment CurrentVersion too!
    Name = "Add new feature columns",
    Apply = conn =>
    {
        // SQLite: one ALTER per command
        var alters = new[]
        {
            "ALTER TABLE MyTable ADD COLUMN new_col TEXT",
            "ALTER TABLE MyTable ADD COLUMN another_col INTEGER"
        };
        foreach (var sql in alters)
        {
            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = sql;
                cmd.ExecuteNonQuery();
            }
            catch (SqliteException ex) when (ex.Message.Contains("duplicate column"))
            {
                // Column already exists - idempotent
            }
        }
    },
},
```

**Rules**:
- Never modify existing migrations
- Always increment `CurrentVersion` when adding migrations
- Handle "duplicate column" errors for idempotency
- Use transactions (automatic) for rollback on failure

---

## What NOT To Generate

### Don't Add Unnecessary Abstractions

```csharp
// WRONG - over-engineering
public interface IMyServiceFactory
{
    IMyService Create();
}

public class MyServiceFactory : IMyServiceFactory { }

// CORRECT - just use DI directly
services.AddSingleton<IMyService, MyService>();
```

### Don't Add Defensive Null Checks Everywhere

```csharp
// WRONG - paranoid nulls
public void DoWork(string input)
{
    if (input == null) throw new ArgumentNullException(nameof(input));
    if (string.IsNullOrEmpty(input)) throw new ArgumentException("...");
    if (string.IsNullOrWhiteSpace(input)) throw new ArgumentException("...");
    // ...
}

// CORRECT - trust internal code, validate at boundaries
public void DoWork(string input)
{
    // Just use it - if it's null, we'll get a clear stack trace
    var result = input.ToLower();
}
```

### Don't Add Logging Everywhere

```csharp
// WRONG - log spam
public async Task DoWorkAsync()
{
    Logger.Debug("Entering DoWorkAsync");
    Logger.Debug("About to call API");
    var result = await CallApiAsync();
    Logger.Debug("API returned {Result}", result);
    Logger.Debug("Exiting DoWorkAsync");
}

// CORRECT - log meaningful events
public async Task DoWorkAsync()
{
    var result = await CallApiAsync();
    if (result.Failed)
    {
        Logger.Warning("API call failed: {Error}", result.Error);
    }
}
```

### Don't Create Wrapper Types

```csharp
// WRONG
public class UserId
{
    public string Value { get; }
    public UserId(string value) => Value = value;
}

// CORRECT - just use string
public async Task<User> GetUserAsync(string username)
```

---

## Config & Constants

### Use Options Pattern

```csharp
// Access via OptionsMonitor.CurrentValue
var maxSlots = OptionsMonitor.CurrentValue.Global.Download.Slots;

// DON'T hardcode
var maxSlots = 10;  // ❌

// DON'T read environment directly
var maxSlots = Environment.GetEnvironmentVariable("SLSKD_DOWNLOAD_SLOTS");  // ❌
```

### Constants Location

```csharp
// Project-wide constants: Program.cs
public static readonly string AppName = "slskd";
public static readonly string DefaultAppDirectory = "...";

// Feature-specific constants: in the service
public class MyService
{
    private const int MaxRetries = 3;
    private const int TimeoutSeconds = 30;
}
```

---

## Testing Patterns

### Unit Test Structure

```csharp
public class MyServiceTests
{
    [Fact]
    public async Task DoWork_WhenCondition_ShouldBehavior()
    {
        // Arrange
        var service = new MyService(/* mocks */);

        // Act
        var result = await service.DoWorkAsync();

        // Assert
        Assert.Equal(expected, result);
    }
}
```

### Use InlineAutoData for Edge Cases

```csharp
// CORRECT - fixed values for edge cases
[Theory]
[InlineAutoData(0)]
[InlineAutoData(1)]
[InlineAutoData(int.MaxValue)]
public void Calculate_EdgeCases_ShouldWork(int value, MyService sut)
{
    var result = sut.Calculate(value);
    Assert.NotNull(result);
}
```

---

## 13. Dev Build Naming Convention

Dev builds MUST include timestamps to differentiate multiple builds on the same day.

**Format**: `dev-YYYYMMDD-HHMMSS`

**Example**: `dev-20251209-204838` (December 9, 2025 at 20:48:38 UTC)

**Where This Applies**:
- Git tag name: `dev-20251209-204838` (hyphens OK)
- Release filename: `slskdn-dev-20251209-204838-linux-x64.zip` (hyphens OK)
- GitHub release title: `Dev Build 20251209-204838` (hyphens OK)
- Docker tags: `ghcr.io/snapetech/slskdn:dev-20251209-204838` and `ghcr.io/snapetech/slskdn:dev-latest`

**Package Version Conversion** (hyphens → dots):

Package managers don't allow hyphens in versions. Convert to dots using `sed 's/-/./g'`:

```bash
# Git tag (hyphens OK)
DEV_VERSION="0.24.1-dev-20251209-204838"

# Package versions (convert ALL hyphens to dots)
PKG_VERSION=$(echo "$DEV_VERSION" | sed 's/-/./g')
# Result: 0.24.1.dev.20251209.204838
```

**Package Filenames and Versions**:
- AUR pkgver: `0.24.1.dev.20251209204838` (dots removed from timestamp)
- RPM Version: `0.24.1.dev.20251209.204838`
- DEB version: `0.24.1.dev.20251209.204838-1`
- .deb filename: `slskdn-dev_0.24.1.dev.20251209.204838_amd64.deb`
- .rpm filename: `slskdn-dev-0.24.1.dev.20251209.204838.x86_64.rpm`

**Workflow Implementation**:
```bash
# Generate timestamp (UTC)
TIMESTAMP=$(date -u +%Y%m%d-%H%M%S)
TAG="dev-${TIMESTAMP}"

# Use in filenames (hyphens OK)
ZIP="slskdn-dev-${TIMESTAMP}-linux-x64.zip"

# Convert for packages (ALL hyphens → dots)
PKG_VERSION=$(echo "0.24.1-dev-${TIMESTAMP}" | sed 's/-/./g')

# Use in AUR (also remove dots from timestamp for epoch-style number)
PKGVER="0.24.1.dev.${TIMESTAMP//-/}"  # Result: 0.24.1.dev.20251209204838
```

**Triggering Dev Builds**:
- Dev builds are triggered ONLY by pushing tags matching `dev-*`
- NOT triggered on every commit to experimental branches
- Create and push tag: `git tag -a dev-$(date -u +%Y%m%d-%H%M%S) -m "Dev build" && git push origin dev-*`

**Distribution Channels** (all automatic on tag push):
- GitHub Release (with .zip, .deb, .rpm assets)
- AUR: `slskdn-dev` package
- COPR: `slskdn/slskdn-dev` repository
- PPA: `ppa:keefshape/slskdn` (slskdn-dev package)
- Docker: `ghcr.io/snapetech/slskdn:dev-latest` and timestamped tags
- Direct .deb and .rpm downloads from GitHub release

**Why Timestamps**: Multiple dev builds can happen on the same day. Without timestamps, tags/releases collide and users can't tell which build they have.

**Why Trigger on Tags**: Triggering only on tags prevents unwanted builds on every experimental branch commit, giving explicit control over when dev releases are published.

**Why Convert Hyphens**: Package managers (AUR, RPM, DEB) have strict version format requirements that prohibit hyphens. See the "Package Manager Version Constraints" gotcha in `adr-0001-known-gotchas.md` for full details.

---

## 14. Auto-Update README with Release Links

Every dev and stable release MUST automatically update README.md installation links.

**Why**: Users visiting GitHub should always see the latest download links without manual editing.

**Pattern**: Add workflow step to update README.md before creating the release.

### Dev Build Example

```yaml
- name: Update README with Latest Dev Build
  run: |
    # Extract version and tag info
    TAG_NAME="${{ github.ref_name }}"
    VERSION="${{ needs.build.outputs.dev_version }}"
    
    # Update README.md dev build section
    sed -i '/<!-- BEGIN_DEV_BUILD -->/,/<!-- END_DEV_BUILD -->/c\
    <!-- BEGIN_DEV_BUILD -->\
    **[Development Build '"${TAG_NAME}"' →](https://github.com/snapetech/slskdn/releases/tag/'"${TAG_NAME}"')** \
    \
    Version: `'"${VERSION}"'` | Branch: `experimental/multi-source-swarm` \
    \
    ```bash\
    # Installation commands...\
    ```\
    <!-- END_DEV_BUILD -->' README.md

- name: Commit and Push README Update
  run: |
    git config user.name "github-actions[bot]"
    git config user.email "github-actions[bot]@users.noreply.github.com"
    
    if git diff --quiet README.md; then
      echo "No changes to README.md"
    else
      git add README.md
      git commit -m "docs: update README with dev build $VERSION [skip ci]"
      git push origin HEAD:experimental/multi-source-swarm
    fi
```

**README.md Format**:

```markdown
### 🧪 Latest Development Build

**⚠️ Unstable builds from experimental branches**

<!-- BEGIN_DEV_BUILD -->
**[Development Build dev-20251209-222346 →](https://github.com/snapetech/slskdn/releases/tag/dev-20251209-222346)**

Version: `0.24.1-dev-20251209-222346` | Branch: `experimental/multi-source-swarm`

```bash
# Installation commands...
```
<!-- END_DEV_BUILD -->
```

**Key Points**:
- Use HTML comments as markers for `sed` replacement
- Include `[skip ci]` in commit message to prevent workflow loop
- Always push to the source branch (`experimental/multi-source-swarm` for dev, `main` for stable)
- Verify the change was applied with `grep -A 20 "BEGIN_DEV_BUILD" README.md`

**Stable Releases**: Follow the same pattern but use `<!-- BEGIN_STABLE_RELEASE -->` and `<!-- END_STABLE_RELEASE -->` markers.

---
*Last updated: 2025-12-09*

