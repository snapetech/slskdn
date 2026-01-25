# Coding Standards Quick Reference Card

**slskdn Project** | Keep this visible while coding | See `STYLE_PROMPT.md` for details

---

## Before You Code

```
1. GREP FIRST    → Search codebase for existing patterns
2. UNDERSTAND    → Restate task in 1-3 sentences
3. DESIGN        → Outline 3-8 bullet points of approach
4. IMPLEMENT     → Follow patterns below
```

---

## The Golden Rules

| Rule | Do This | Not This |
|------|---------|----------|
| **Naming** | `TransferService`, `DownloadQueue` | `TransferManager`, `DataHelper` |
| **Validation** | At API boundaries only | Everywhere with null checks |
| **Exceptions** | Let them propagate | Catch and return null |
| **Logging** | Meaningful events only | Entry/exit spam |
| **Async** | Return `Task` directly if no work | Wrap with `async/await` |
| **Comments** | Explain *why* | Narrate *what* |
| **Functions** | < 50 lines, one purpose | 100+ lines, many purposes |

---

## Anti-Slop Checklist

Before committing code, verify:

- [x] **Deferred and docs (if plan-driven)** - Add rows to the plan’s **Deferred and Follow-up Work** table for left-out work; remove rows when completing deferred items; update `config/slskd.example.yml` if options changed. See **AGENTS.md § After Completing Work**.
- [x] **Grepped for patterns** - No invented abstractions
- [x] **No defensive null checks** - Only validate at boundaries  
- [x] **Exceptions propagate** - No swallowed errors
- [x] **No logging spam** - Only meaningful events
- [x] **Clear names** - No Manager/Helper/Handler/Wrapper
- [x] **Async void wrapped** - try-catch on all async void
- [x] **Network rate-limited** - SemaphoreSlim on peer operations
- [x] **Small functions** - Methods < 50 lines
- [x] **Safe return values** - Frontend libs return [] not undefined
- [x] **Matches codebase style** - Consistent with existing code

---

## Common Patterns (C#)

### Service Structure
```csharp
public class MyService : IMyService
{
    public MyService(
        IOptionsMonitor<Options> optionsMonitor,
        ILogger<MyService> logger)
    {
        OptionsMonitor = optionsMonitor;
        Logger = logger;
    }

    private IOptionsMonitor<Options> OptionsMonitor { get; }
    private ILogger Logger { get; }
}
```

### Options Access
```csharp
// In singleton service
var value = OptionsMonitor.CurrentValue.Global.Download.Slots;
```

### API Controller
```csharp
[HttpGet("{username}")]
[Authorize(Policy = AuthPolicy.Any)]
public async Task<IActionResult> Get([FromRoute] string username)
{
    // Validate at boundary
    if (string.IsNullOrWhiteSpace(username))
        return BadRequest("Username required");
    
    var result = await Service.GetAsync(username);
    return result != null ? Ok(result) : NotFound();
}
```

### Async Event Handler
```csharp
private async void OnEvent(object sender, EventArgs e)
{
    try
    {
        await DoWorkAsync();
    }
    catch (Exception ex)
    {
        Logger.Error(ex, "Event handler failed");
    }
}
```

### Rate-Limited Operation
```csharp
private readonly SemaphoreSlim _semaphore = new(5);  // Max 5 concurrent

private async Task ProbeAsync(Source source)
{
    await _semaphore.WaitAsync();
    try
    {
        return await DoProbeAsync(source);
    }
    finally
    {
        _semaphore.Release();
    }
}
```

---

## Common Patterns (React/JS)

### Component Structure
```jsx
const MyComponent = ({ someProp }) => {
  const [loading, setLoading] = useState(true);
  const [data, setData] = useState([]);

  useEffect(() => {
    const init = async () => {
      try {
        const result = await myApi.getAll();
        setData(result);
      } catch (error) {
        console.error(error);
        toast.error(error?.response?.data ?? error?.message ?? error);
      } finally {
        setLoading(false);
      }
    };
    init();
  }, []);

  if (loading) return <LoaderSegment />;
  return <div>{/* ... */}</div>;
};
```

### API Library Function
```javascript
export const getAll = async () => {
  const response = (await api.get('/items')).data;
  
  // ALWAYS return safe values
  if (!Array.isArray(response)) {
    console.warn('got non-array response', response);
    return [];  // Not undefined!
  }
  
  return response;
};
```

---

## Security Checklist

- [x] **File paths validated** - Use `PathGuard.NormalizeAndValidate()`
- [x] **API inputs validated** - Check at controller boundaries
- [x] **Rate limiting added** - Network operations bounded
- [x] **Async void wrapped** - All event handlers have try-catch

---

## Efficiency Checklist

- [x] **O(n) not O(n²)** - Use HashSet for lookups
- [x] **Batch DB queries** - No N+1 problems
- [x] **Bound parallelism** - SemaphoreSlim on Task.WhenAll
- [x] **No waste** - Don't recompile regex in loops

---

## 🚨 CRITICAL: Build Process

**Builds ONLY happen on tags. NO automatic builds on code pushes.**

### Build Rules
- ❌ **DO NOT** expect builds on code pushes to master
- ❌ **DO NOT** modify workflows to trigger on branch pushes
- ✅ **DO** create tags explicitly when user requests a build
- ✅ **DO** wait for explicit user instruction before creating tags

### CI Workflow Triggers
- ✅ Tags: `build-main-*`, `build-dev-*`, version tags
- ✅ Pull requests: For testing (does not publish)
- ✅ Manual dispatch: `workflow_dispatch`
- ❌ **NOT on pushes to master**: Intentionally disabled

### To Trigger a Build (Only if User Requests)
```bash
# Main/stable release
git tag build-main-0.24.1-slskdn.41
git push origin build-main-0.24.1-slskdn.41

# Dev release
VERSION="0.24.1.dev.$(date -u +%Y%m%d.%H%M%S)"
git tag "build-dev-${VERSION}"
git push origin "build-dev-${VERSION}"
```

See `memory-bank/decisions/adr-0005-tagging-system.md` for complete details.

---

## CLI Shortcuts

```bash
# Chain commands (stops on failure)
dotnet build && dotnet test && ./bin/lint

# Subshell for directory changes
(cd src/web && npm install)

# Combined grep
grep -E "error|warning|fatal" file.log

# Parallel tool calls
rg "pattern" --type cs | head -20
```

---

## When to Ask for Help

**Uncertain about**:
- API behavior or library function
- Framework patterns
- Performance implications
- Security considerations

**Do this**:
1. State your uncertainty
2. Offer most likely correct pattern
3. Mark what should be verified
4. Continue with best assumption

---

## Output Format

Every code contribution should include:

1. **Plan** (3-8 bullets before code)
2. **Implementation** (clean, idiomatic code)
3. **Summary** (3-8 bullets after code)
4. **Usage example** (with realistic data)

---

## Remember

> **Priorities**: correctness → clarity → efficiency → ergonomics

> **Test**: "Would a senior engineer approve this in code review?"

> **Grep first. Design first. Ship quality code.**

---

**Full docs**: `memory-bank/decisions/adr-0007-senior-engineer-coding-standards.md`  
**Style prompt**: `memory-bank/STYLE_PROMPT.md`  
**Project brief**: `memory-bank/projectbrief.md`
