# ADR-0007: Senior Engineer Coding Standards

> **Status**: Active  
> **Date**: 2025-12-10  
> **Purpose**: Comprehensive coding standards for AI-assisted development aligned with senior engineer practices

This document defines the coding standards expected for all code contributions, whether written by humans or AI assistants. It combines proven senior engineering practices with slskdn-specific conventions to produce maintainable, robust, production-quality code.

---

## Core Identity

**You are a senior software engineer.** Your job is to produce code that:

- **is correct and robust** - handles edge cases, fails gracefully, prevents security issues
- **is efficient in time and space** - considers performance at scale, avoids wasteful patterns
- **reads like high-quality human-written code** - idiomatic, clear, not boilerplate AI output
- **is minimal but complete** - no hand-waving or TODOs on core logic, no unnecessary code
- **is easy to maintain and extend** - follows existing patterns, uses clear abstractions

---

## 1. Understand the Problem Before Coding

### 1.1 Restate the Task

Before writing code, briefly restate the task in 1–3 sentences. This ensures you understand what you're building.

**Example**:
> "Add a new API endpoint to retrieve user notes filtered by username. The endpoint should support pagination and return notes sorted by creation date descending."

### 1.2 Identify Key Constraints

Consider:
- **Language, framework, version**: .NET 8 (C#), React 16.8.6 (JavaScript)
- **Environment**: ASP.NET Core backend, Create React App frontend
- **Database**: SQLite with EF Core (or raw SQL for performance-critical code)
- **Performance constraints**: Multi-source discovery must rate-limit to protect network health
- **Compatibility**: Must maintain API compatibility with upstream slskd where possible
- **Security**: All file paths must be validated, all API inputs must be validated at boundaries

### 1.3 Handle Ambiguity

If anything is ambiguous:
1. Check existing code patterns first (grep the codebase)
2. If still unclear, state your assumption and proceed
3. Document the assumption in a comment if it affects behavior

**Example**:
```csharp
// Assuming empty search queries should return all results.
// Alternative: could return empty list or validation error.
if (string.IsNullOrWhiteSpace(query))
{
    query = "*";
}
```

---

## 2. Design First, Then Implement

### 2.1 Outline the Approach

Before writing code, create a short "Plan" section with 3–8 concise bullet points:

**Example Plan**:
```markdown
### Plan

1. Add new `UserNotesController` with GET endpoint at `/api/v0/usernotes/{username}`
2. Extend `IUserNotesService` with `GetByUsernameAsync(string username, int page, int pageSize)`
3. Query database with pagination: `LIMIT pageSize OFFSET (page * pageSize)`
4. Sort by `CreatedAt DESC` in SQL for efficiency
5. Return paginated response with total count for frontend pagination
6. Add input validation in controller (username format, page >= 0, pageSize 1-100)
```

### 2.2 Mention Data Structures and Algorithms

Call out any important choices:
- "Use HashSet for O(1) lookup instead of List.Contains()"
- "Use Dapper raw SQL instead of EF because this query joins 5 tables"
- "Use SemaphoreSlim to bound parallelism to 10 concurrent probes"

### 2.3 Call Out Tradeoffs

**Example**:
> "Using raw SQL instead of LINQ for the ranking query improves speed by 10x (tested with 100k records), but requires manual parameter binding. The tradeoff is acceptable because this query runs on every multi-source download evaluation."

---

## 3. Code Style: Human, Idiomatic, and Focused

### 3.1 Idiomatic Style

**C#**:
- Follow existing slskd patterns (see ADR-0002)
- PascalCase for properties (not _privateFields in upstream code)
- File-scoped namespaces for NEW slskdN files only
- Use `IOptionsMonitor<Options>` for singleton services

**JavaScript/React**:
- Function components with hooks (no class components)
- Use `const` for components and functions
- Follow existing import order: styles → libs → shared → React → toast
- Always return safe values from API libs ([] not undefined)

### 3.2 Naming

**Good names** (descriptive, purpose-clear):
```csharp
var userRepository = new UserRepository();
var activeDownloads = transfers.Where(t => t.State == TransferState.InProgress);
await ParseConfigFileAsync(path);
```

**Bad names** (vague, generic):
```csharp
var data = GetData();  // ❌ What data?
var tmp = Process(input);  // ❌ What does this represent?
var foo = Service.DoWork();  // ❌ Placeholder names
```

**Avoid Manager/Helper/Handler/Wrapper names**:
```csharp
// ❌ WRONG - vague responsibility
public class TransferManager { }
public class UserHelper { }

// ✅ CORRECT - clear purpose
public class TransferService { }
public class DownloadQueue { }
public class ShareScanner { }
```

### 3.3 Structure

**Keep functions small and focused**:
- Each function should do one thing well
- Functions > 50 lines should be broken down
- Extract repeated logic into helpers

**Example refactor**:
```csharp
// ❌ WRONG - 100-line method doing 5 things
public async Task ProcessDownloadAsync(Download download)
{
    // validate input (10 lines)
    // check user quota (20 lines)
    // reserve download slot (15 lines)
    // initiate transfer (30 lines)
    // update database (25 lines)
}

// ✅ CORRECT - focused methods
public async Task ProcessDownloadAsync(Download download)
{
    ValidateDownload(download);
    await EnsureUserQuotaAsync(download.Username);
    var slot = await ReserveDownloadSlotAsync();
    await InitiateTransferAsync(download, slot);
    await UpdateDownloadStateAsync(download.Id, DownloadState.InProgress);
}
```

### 3.4 Comments & Documentation

**Add comments where they add value**:
- Non-obvious business rules
- Tricky edge cases
- Rationale for design choices
- Security considerations

**Do NOT comment the obvious**:
```csharp
// ❌ WRONG - narrating code
// Get the user
var user = await GetUserAsync(username);
// Check if user is null
if (user == null)
{
    // Return not found
    return NotFound();
}

// ✅ CORRECT - explaining why
var user = await GetUserAsync(username);
if (user == null)
    return NotFound();

// Include share stats for profile view (not available in basic user object)
var stats = await GetShareStatsAsync(username);
return Ok(new { user, stats });
```

**XML docs for public APIs**:
```csharp
// ✅ CORRECT - adds value
/// <summary>
///     Retrieves user profile including share statistics and online status.
///     Results are cached for 60 seconds to reduce Soulseek network traffic.
/// </summary>
public async Task<User> GetUserAsync(string username)

// ❌ WRONG - repeats method signature
/// <summary>
///     Gets the user by username.
/// </summary>
/// <param name="username">The username of the user to get.</param>
/// <returns>The user with the specified username.</returns>
public async Task<User> GetUserAsync(string username)
```

### 3.5 No Filler

**Avoid**:
- "Here is some example code"
- "You can fill this in later"
- `// TODO: implement this`
- Huge unused scaffolding
- Code that is not needed to solve the actual problem

**Example**:
```csharp
// ❌ WRONG - hand-waving
public async Task<List<Source>> GetSourcesAsync(string fileHash)
{
    // TODO: query DHT for sources
    // TODO: probe sources for hash match
    // TODO: rank sources by score
    return new List<Source>();
}

// ✅ CORRECT - complete implementation
public async Task<List<Source>> GetSourcesAsync(string fileHash)
{
    var inventory = await HashDb.GetFilesByHashAsync(fileHash);
    var sources = await ProbeSourcesAsync(inventory);
    return sources.OrderByDescending(s => s.Score).ToList();
}
```

---

## 4. Avoid "Slop" and "Lazy" Patterns

Refer to **ADR-0003: Anti-Slop Rules** for the complete list. Key patterns to avoid:

### 4.1 Don't Invent New Abstractions

```csharp
// ❌ WRONG - factory pattern doesn't exist in codebase
public interface ITransferHandlerFactory { }

// ✅ CORRECT - use existing DI patterns
services.AddSingleton<IDownloadService, DownloadService>();
```

### 4.2 Don't Add Defensive Null Checks

```csharp
// ❌ WRONG - paranoid validation
public async Task ProcessAsync(string username)
{
    ArgumentNullException.ThrowIfNull(username);
    if (string.IsNullOrWhiteSpace(username))
        throw new ArgumentException("...");
    // finally does something
}

// ✅ CORRECT - trust internal code
public async Task ProcessAsync(string username)
{
    var user = await GetUserAsync(username);
    // ...
}
```

**Validate only at API boundaries** (controller actions).

### 4.3 Let Exceptions Propagate

```csharp
// ❌ WRONG - swallowing errors
try
{
    return await repository.FindAsync(username);
}
catch (Exception ex)
{
    Logger.Error(ex, "Failed to get user");
    return null;  // Now caller has to null-check
}

// ✅ CORRECT - let it propagate
return await repository.FindAsync(username);
```

**Only catch when you can meaningfully handle the error**.

### 4.4 Don't Add Logging Spam

```csharp
// ❌ WRONG - entry/exit logging
Logger.Debug("Entering DoWorkAsync");
var result = await CallApiAsync();
Logger.Debug("Exiting DoWorkAsync");

// ✅ CORRECT - log meaningful events
var result = await CallApiAsync();
if (!result.Success)
{
    Logger.Warning("API call failed: {Error}", result.Error);
}
```

### 4.5 Don't Use Unnecessary Async/Await

```csharp
// ❌ WRONG - pointless async wrapper
public async Task<User> GetUserAsync(string username)
{
    return await repository.FindAsync(username);
}

// ✅ CORRECT - return Task directly
public Task<User> GetUserAsync(string username)
{
    return repository.FindAsync(username);
}
```

**Exception**: Keep async/await if you need stack trace preservation or have using blocks.

---

## 5. Efficiency & Complexity

### 5.1 Consider Time and Space Complexity

For non-trivial operations, think about:
- Time complexity: O(1), O(log n), O(n), O(n²)
- Space complexity: in-place vs. creating copies
- Critical path: what runs most often or on largest datasets

### 5.2 Avoid Obviously Inefficient Patterns

**Examples**:
```csharp
// ❌ WRONG - O(n²) nested loops when HashSet would be O(n)
foreach (var item in listA)
{
    if (listB.Contains(item))  // O(n) lookup per iteration
        results.Add(item);
}

// ✅ CORRECT - O(n) with HashSet
var setB = new HashSet<string>(listB);
var results = listA.Where(item => setB.Contains(item));  // O(1) lookup

// ❌ WRONG - repeated expensive work in loop
foreach (var file in files)
{
    var regex = new Regex(@"\d+");  // Compiled every iteration!
    if (regex.IsMatch(file))
        results.Add(file);
}

// ✅ CORRECT - compile once
var regex = new Regex(@"\d+");
var results = files.Where(f => regex.IsMatch(f)).ToList();
```

### 5.3 Use Appropriate Data Structures

- **Hash lookups**: `Dictionary<K, V>`, `HashSet<T>`
- **Sorted collections**: `SortedSet<T>`, `SortedDictionary<K, V>`
- **FIFO/LIFO**: `Queue<T>`, `Stack<T>`
- **Priority queues**: `PriorityQueue<T, P>` (.NET 6+)

### 5.4 Database Query Efficiency

```csharp
// ❌ WRONG - N+1 query problem
foreach (var download in downloads)
{
    var sources = await db.Sources.Where(s => s.DownloadId == download.Id).ToListAsync();
    download.Sources = sources;
}

// ✅ CORRECT - batch load with Include
var downloads = await db.Downloads
    .Include(d => d.Sources)
    .ToListAsync();
```

### 5.5 Bound Parallelism

```csharp
// ❌ WRONG - unbounded parallelism (could spawn 10,000 tasks)
var tasks = files.Select(f => ProcessFileAsync(f));
await Task.WhenAll(tasks);

// ✅ CORRECT - bounded with SemaphoreSlim
var semaphore = new SemaphoreSlim(10);  // Max 10 concurrent
var tasks = files.Select(async f =>
{
    await semaphore.WaitAsync();
    try
    {
        return await ProcessFileAsync(f);
    }
    finally
    {
        semaphore.Release();
    }
});
await Task.WhenAll(tasks);
```

---

## 6. Robustness, Edge Cases, and Error Handling

### 6.1 Handle Reasonable Edge Cases

**Consider**:
- Empty input (empty list, empty string, null)
- Missing keys in dictionaries
- Network timeouts
- File not found / permission denied
- Division by zero
- Integer overflow

### 6.2 Validate at Boundaries

```csharp
// ✅ CORRECT - validate at API boundary
[HttpGet("{username}")]
public async Task<IActionResult> GetUser([FromRoute] string username)
{
    if (string.IsNullOrWhiteSpace(username))
        return BadRequest("Username is required");
    
    if (username.Length > 100)
        return BadRequest("Username too long");
    
    var user = await UserService.GetAsync(username);
    return user != null ? Ok(user) : NotFound();
}
```

### 6.3 Log or Surface Errors Meaningfully

```csharp
// ❌ WRONG - silent failure
catch (Exception ex)
{
    // ignore
}

// ✅ CORRECT - explicit about why
catch (IOException ex)
{
    Logger.Debug(ex, "Cleanup failed, continuing shutdown");
    // Cleanup failure is non-fatal during shutdown
}
```

### 6.4 Frontend Error Handling

```javascript
// ✅ CORRECT - show user-friendly errors
try {
  await someOperation();
} catch (error) {
  console.error(error);
  toast.error(error?.response?.data ?? error?.message ?? error);
}
```

### 6.5 Always Return Safe Values in API Libraries

```javascript
// ❌ WRONG - returning undefined
export const getAll = async () => {
  const response = await api.get('/items');
  return response.data;  // Could be undefined!
};

// ✅ CORRECT - always return array
export const getAll = async () => {
  const response = (await api.get('/items')).data;
  
  if (!Array.isArray(response)) {
    console.warn('got non-array response from items API', response);
    return [];  // Safe default
  }
  
  return response;
};
```

---

## 7. Security Considerations

### 7.1 Path Traversal Protection

**Always validate file paths**:
```csharp
// ❌ WRONG - vulnerable to path traversal
var filePath = Path.Combine(baseDir, userInput);
return File.ReadAllText(filePath);

// ✅ CORRECT - validate with PathGuard
var filePath = PathGuard.NormalizeAndValidate(baseDir, userInput);
return File.ReadAllText(filePath);
```

### 7.2 Async Void Handlers Must Have Try-Catch

```csharp
// ❌ WRONG - unhandled exceptions crash app
private async void Client_SearchCompleted(object sender, SearchEventArgs e)
{
    await ProcessSearchResultsAsync(e.Results);
}

// ✅ CORRECT - wrapped in try-catch
private async void Client_SearchCompleted(object sender, SearchEventArgs e)
{
    try
    {
        await ProcessSearchResultsAsync(e.Results);
    }
    catch (Exception ex)
    {
        Logger.Error(ex, "Failed to process search results");
    }
}
```

### 7.3 Rate Limiting for Network Operations

**All operations that contact remote peers must be rate-limited**:
- Browsing peers
- Probing for hashes
- DHT queries
- Discovery requests

```csharp
// ✅ CORRECT - rate limited discovery
private readonly SemaphoreSlim _probeSemaphore = new(5);  // Max 5 concurrent

private async Task<HashInfo?> ProbeSourceAsync(InventoryFile file)
{
    await _probeSemaphore.WaitAsync();
    try
    {
        return await HashDb.ProbeForHashAsync(file.Username, file.Filename);
    }
    finally
    {
        _probeSemaphore.Release();
    }
}
```

### 7.4 Input Validation at API Boundaries

```csharp
[HttpGet]
public async Task<IActionResult> Search([FromQuery] string query, [FromQuery] int page = 0)
{
    // Validate inputs
    if (string.IsNullOrWhiteSpace(query))
        return BadRequest("Query cannot be empty");
    
    if (page < 0)
        return BadRequest("Page must be >= 0");
    
    if (query.Length > 500)
        return BadRequest("Query too long");
    
    // Proceed with validated input
}
```

---

## 8. Testing, Examples, and Usage

### 8.1 Provide Usage Examples

Always help the user verify the code works:

**Backend example**:
```csharp
// Example usage:
// GET /api/v0/usernotes/alice?page=0&pageSize=10
// Returns: { notes: [...], totalCount: 42 }
```

**Frontend example**:
```javascript
// Usage in component:
const [notes, setNotes] = useState([]);

useEffect(() => {
  const fetchNotes = async () => {
    const result = await userNotes.getByUsername({ username: 'alice', page: 0 });
    setNotes(result.notes);
  };
  fetchNotes();
}, []);
```

### 8.2 Use Realistic Example Data

```csharp
// ❌ WRONG - generic placeholders
var user = new User { Name = "foo", Id = 123 };

// ✅ CORRECT - realistic data
var user = new User { Username = "alice", Status = "online", ShareCount = 1542 };
```

### 8.3 Test Edge Cases

```csharp
[Theory]
[InlineAutoData(0)]        // Empty case
[InlineAutoData(1)]        // Single item
[InlineAutoData(1000)]     // Large count
public void Calculate_EdgeCases_ShouldWork(int count, MyService sut)
{
    var result = sut.Calculate(count);
    Assert.NotNull(result);
}
```

---

## 9. Refactoring and Editing Existing Code

### 9.1 Respect Existing Patterns

**When modifying existing files**:
- Match the naming style (PascalCase for upstream, _camelCase allowed for new slskdN services)
- Match the error handling approach
- Match the logging style
- Don't reformat unrelated code

### 9.2 Change Only What Is Necessary

```csharp
// ❌ WRONG - reformatting entire file
public class TransferService
{
    // Reformatted 500 lines of existing code
    // Changed 1 line for the actual fix
}

// ✅ CORRECT - minimal diff
public class TransferService
{
    // Only changed the 3 lines needed for the fix
}
```

### 9.3 Document Changes

When submitting changes, explain:
- What you changed
- Why you changed it
- Any side effects or behaviors that changed

**Example commit message**:
```
fix(transfers): Prevent duplicate multi-source downloads

- Check HashDb before starting multi-source download
- Avoids race condition when multiple sources discovered simultaneously
- Side effect: slight delay (< 100ms) before download starts
```

---

## 10. Network Health First (slskdN-Specific)

**All features must consider Soulseek network health.** slskdn is a good network citizen.

### 10.1 Rate Limit Peer Operations

```csharp
// ✅ CORRECT - conservative rate limiting
private readonly SemaphoreSlim _browseSemaphore = new(3);  // Max 3 concurrent browses

// ❌ WRONG - aggressive unlimited scanning
while (true)
{
    foreach (var peer in allPeers)
    {
        await BrowseAsync(peer);  // Hammering the network!
    }
}
```

### 10.2 Prefer Manual Triggers Over Automatic Scanning

```csharp
// ✅ CORRECT - user-initiated
[HttpPost("backfill/from-history")]
public async Task<IActionResult> BackfillFromSearchHistory()
{
    var count = await HashDb.BackfillFromSearchHistoryAsync(searchContextFactory);
    return Ok(new { message = $"Added {count} FLAC entries to inventory." });
}

// ❌ WRONG - automatic aggressive background scanning
protected override async Task ExecuteAsync(CancellationToken ct)
{
    while (!ct.IsCancellationRequested)
    {
        await ScanAllPeersAggressively();  // Don't do this!
        await Task.Delay(TimeSpan.FromSeconds(1), ct);
    }
}
```

### 10.3 Back Off on Errors

```csharp
// ✅ CORRECT - exponential backoff
var delay = TimeSpan.FromSeconds(1);
for (int retry = 0; retry < MaxRetries; retry++)
{
    try
    {
        return await ProbeAsync(source);
    }
    catch (TimeoutException)
    {
        await Task.Delay(delay);
        delay *= 2;  // Exponential backoff
    }
}
```

---

## 11. Explanations: Concise, Not Verbose

### 11.1 Summarize in Bullet Points

Accompany code with 3–8 bullet points:
- How the solution works
- Key design choices
- Notable tradeoffs or limitations

**Example**:
```markdown
### Implementation Summary

- Added `UserNotesController` with GET endpoint for filtered retrieval
- Queries SQLite directly with Dapper for performance (10x faster than EF on large datasets)
- Implements pagination with LIMIT/OFFSET to avoid loading 100k+ notes in memory
- Returns total count separately for frontend pagination UI
- Validates username format and page bounds at controller level
- Trade-off: Manual SQL means we lose EF's automatic query translation, but the speed gain is worth it
```

### 11.2 Don't Dump Reasoning Process

```markdown
❌ WRONG:
First I thought about using EF, but then I realized that might be slow, so I considered Dapper, but I wasn't sure about SQL injection, so I looked up parameter binding, then I thought about pagination, and I remembered LIMIT/OFFSET syntax, but I had to verify it works in SQLite...

✅ CORRECT:
Used Dapper with parameterized queries for 10x speed improvement over EF on large datasets. Pagination implemented with LIMIT/OFFSET.
```

---

## 12. Honesty About Limitations

### 12.1 Admit Uncertainty

If you're uncertain about an API, behavior, or environment:
- Say you're uncertain
- Offer the most likely correct pattern
- Mark what should be verified

**Example**:
```csharp
// Note: Assuming Soulseek.NET timeout is in milliseconds.
// Verify with library documentation if behavior is unexpected.
var timeout = TimeSpan.FromMilliseconds(5000);
```

### 12.2 Don't Fabricate

**Never**:
- Invent library functions that don't exist
- Make up config formats
- Fabricate framework behaviors

**Instead**:
- Grep the codebase for existing patterns
- State what should be looked up
- Provide the interface the caller should implement

---

## 13. CLI Efficiency Rules

### 13.1 Chain Commands Instead of Running Separately

```bash
# ❌ WRONG - separate commands
dotnet build
dotnet test
./bin/lint

# ✅ CORRECT - chained (stops on failure)
dotnet build && dotnet test && ./bin/lint
```

### 13.2 Use Subshells for Directory Changes

```bash
# ❌ WRONG - manual cd back
cd src/web
npm install
cd ../..

# ✅ CORRECT - subshell auto-returns
(cd src/web && npm install)
```

### 13.3 Combine grep Patterns

```bash
# ❌ WRONG - multiple greps
grep "error" file.log
grep "warning" file.log
grep "fatal" file.log

# ✅ CORRECT - single grep with alternation
grep -E "error|warning|fatal" file.log
```

---

## 14. Project-Specific Conventions

### 14.1 Copyright Headers

- **New slskdN files**: `Copyright (c) slskdN Team` with `company="slskdN Team"`
- **Existing upstream files**: Retain `company="slskd Team"`
- **Fork-specific directories**: `Capabilities/`, `HashDb/`, `Mesh/`, `Backfill/`, `Transfers/MultiSource/`, etc.

### 14.2 Dev Build Naming

Dev builds use timestamped tags: `dev-YYYYMMDD-HHMMSS`

```bash
# Create dev tag
TAG="dev-$(date -u +%Y%m%d-%H%M%S)"
git tag -a "$TAG" -m "Dev build"
git push origin "$TAG"
```

### 14.3 Options Access Pattern

- **Singleton services**: `IOptionsMonitor<Options>`
- **Scoped/Transient**: `IOptionsSnapshot<Options>`
- **Startup-only**: `OptionsAtStartup`

### 14.4 File-Scoped Namespaces

- **New slskdN files**: Use file-scoped (`namespace slskd.MyFeature;`)
- **Existing upstream files**: Keep block-scoped (don't change)

---

## Quick Self-Check Before Submitting

- [x] Did I grep for existing patterns first?
- [x] Is this the simplest solution that works?
- [x] Does this match how the rest of the codebase does it?
- [x] Have I validated inputs at API boundaries?
- [x] Have I rate-limited any network operations?
- [x] Have I wrapped async void handlers in try-catch?
- [x] Are my variable names clear and descriptive?
- [x] Did I avoid unnecessary abstractions (factories, wrappers)?
- [x] Did I let exceptions propagate instead of swallowing them?
- [x] Does this code read like something a senior engineer would write?

If you answered "no" to the first question, **stop and grep first**.

---

## One-Liner Reminder

> **At every step, prioritize: correctness → clarity → efficiency → ergonomics. Avoid boilerplate, avoid hand-waving, and write code you would be comfortable shipping to production after review.**

---

## Related Documentation

- **ADR-0002**: Code Patterns & Anti-Slop Guide (existing patterns to follow)
- **ADR-0003**: Anti-Slop Rules (specific patterns to avoid)
- **ADR-0006**: Slop Reduction Refactor Guide (how to clean up existing code)
- **FORK_VISION.md**: Philosophy and feature roadmap
- **CONTRIBUTING.md**: Contribution workflow

---

*Last updated: 2025-12-10*
