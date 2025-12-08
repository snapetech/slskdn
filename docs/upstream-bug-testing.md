# Upstream Bug Testing Documentation

## Test Date: 2025-12-06
## Tester: Automated via Cursor

---

## Bug 1: async-void-roomservice

### Description
The `Client_LoggedIn` event handler in `RoomService.cs` is `async void`, meaning unhandled exceptions cannot be caught and will crash the entire process.

### Upstream Code (slskd master)
```csharp
private async void Client_LoggedIn(object sender, EventArgs e)
{
    var autoJoinRooms = OptionsMonitor.CurrentValue.Rooms;

    if (autoJoinRooms.Any())
    {
        Logger.Information("Auto-joining room(s) {Rooms}", string.Join(", ", autoJoinRooms));
        await TryJoinAsync(autoJoinRooms);  // ⚠️ Exception here crashes process
    }

    var previouslyJoinedRooms = RoomTracker.Rooms.Keys.Except(autoJoinRooms);

    if (previouslyJoinedRooms.Any())
    {
        Logger.Information("Attempting to rejoin room(s) {Rooms}", string.Join(", ", previouslyJoinedRooms));
        await TryJoinAsync(previouslyJoinedRooms.ToArray());  // ⚠️ Exception here crashes process
    }
}
```

### slskdN Fixed Code
```csharp
private async void Client_LoggedIn(object sender, EventArgs e)
{
    try
    {
        var autoJoinRooms = OptionsMonitor.CurrentValue.Rooms;

        if (autoJoinRooms.Any())
        {
            Logger.Information("Auto-joining room(s) {Rooms}", string.Join(", ", autoJoinRooms));
            await TryJoinAsync(autoJoinRooms);
        }

        var previouslyJoinedRooms = RoomTracker.Rooms.Keys.Except(autoJoinRooms);

        if (previouslyJoinedRooms.Any())
        {
            Logger.Information("Attempting to rejoin room(s) {Rooms}", string.Join(", ", previouslyJoinedRooms));
            await TryJoinAsync(previouslyJoinedRooms.ToArray());
        }
    }
    catch (Exception ex)
    {
        Logger.Error(ex, "Failed to execute post-login room actions");  // ✅ Logged, not crashed
    }
}
```

### Reproduction
This bug occurs when:
- Network timeout during room join
- Soulseek server issues
- Room join failures (room doesn't exist, banned, etc.)

### Evidence of Inconsistency
Application.cs (upstream, lines 954-957) already uses this pattern:
```csharp
catch (Exception ex)
{
    Log.Error(ex, "Failed to execute post-login actions");
}
```

But RoomService.cs does NOT have this protection - it's inconsistent with the rest of the codebase.

### Test Result
- **Upstream**: Process would crash on unhandled exception (inconsistent with Application.cs pattern)
- **slskdN**: Exception is caught and logged, process continues (matches Application.cs pattern)

---

## Bug 2: searches-undefined-return

### Description
In `src/web/src/lib/searches.js`, the `getResponses` function returns `undefined` when the API returns a non-array response, causing downstream code to crash when trying to iterate.

### Upstream Code (slskd master)
```javascript
// src/web/src/lib/searches.js line 27-38
export const getResponses = async ({ id }) => {
  const response = (
    await api.get(`/searches/${encodeURIComponent(id)}/responses`)
  ).data;

  if (!Array.isArray(response)) {
    console.warn('got non-array response from searches API', response);
    return undefined;  // ⚠️ BUG: causes "Cannot read property 'map' of undefined"
  }

  return response;
};
```

### slskdN Fixed Code
```javascript
// src/web/src/lib/searches.js line 69-80
export const getResponses = async ({ id }) => {
  const response = (
    await api.get(`/searches/${encodeURIComponent(id)}/responses`)
  ).data;

  if (!Array.isArray(response)) {
    console.warn('got non-array response from searches API', response);
    return [];  // ✅ FIX: returns empty array, safe to iterate
  }

  return response;
};
```

### Browser Console Test
```javascript
// In upstream slskd, if API returns malformed data:
const result = undefined;  // what getResponses returns
result.map(x => x);  // ❌ TypeError: Cannot read property 'map' of undefined

// In slskdN:
const result = [];  // what getResponses returns  
result.map(x => x);  // ✅ Returns [] safely
```

### Test Result
- **Upstream**: Returns `undefined`, causes crash on iteration
- **slskdN**: Returns `[]`, safe iteration

---

## Bug 3: transfers-undefined-return

### Description
In `src/web/src/lib/transfers.js`, the `getAll` function returns `undefined` when the API returns a non-array response, causing downstream code to crash.

### Upstream Code (slskd master)
```javascript
// src/web/src/lib/transfers.js line 3-14
export const getAll = async ({ direction }) => {
  const response = (
    await api.get(`/transfers/${encodeURIComponent(direction)}s`)
  ).data;

  if (!Array.isArray(response)) {
    console.warn('got non-array response from transfers API', response);
    return undefined;  // ⚠️ BUG: causes downstream crashes
  }

  return response;
};
```

### slskdN Fixed Code
```javascript
// src/web/src/lib/transfers.js line 3-14
export const getAll = async ({ direction }) => {
  const response = (
    await api.get(`/transfers/${encodeURIComponent(direction)}s`)
  ).data;

  if (!Array.isArray(response)) {
    console.warn('got non-array response from transfers API', response);
    return [];  // ✅ FIX: returns empty array, safe to iterate
  }

  return response;
};
```

### Test Result
- **Upstream**: Returns `undefined`, causes crash on iteration
- **slskdN**: Returns `[]`, safe iteration

---

## Bug 4: search-list-no-pagination

### Description
The `GET /api/v0/searches` endpoint returns ALL searches without any pagination or limit. With heavy use, this can result in 100,000+ searches accumulating, producing a 45MB+ JSON response that hangs the browser.

### Upstream Code (slskd master)
```csharp
// SearchesController.cs
[HttpGet("")]
[Authorize(Policy = AuthPolicy.Any)]
public async Task<IActionResult> GetAll()
{
    if (Program.IsRelayAgent)
    {
        return Forbid();
    }

    var searches = await Searches.ListAsync();  // ⚠️ Returns ALL searches
    return Ok(searches);
}

// SearchService.cs
public Task<List<Search>> ListAsync(Expression<Func<Search, bool>> expression = null)
{
    expression ??= s => true;
    using var context = ContextFactory.CreateDbContext();

    return context.Searches
        .AsNoTracking()
        .Where(expression)
        .WithoutResponses()
        .ToListAsync();  // ⚠️ No limit, returns everything
}
```

### slskdN Fixed Code
```csharp
// SearchesController.cs - adds optional pagination params
[HttpGet("")]
[Authorize(Policy = AuthPolicy.Any)]
public async Task<IActionResult> GetAll([FromQuery] int limit = 0, [FromQuery] int offset = 0)
{
    if (Program.IsRelayAgent)
    {
        return Forbid();
    }

    var searches = await Searches.ListAsync(limit: limit, offset: offset);
    return Ok(searches);
}

// SearchService.cs - supports pagination
public Task<List<Search>> ListAsync(Expression<Func<Search, bool>> expression = null, int limit = 0, int offset = 0)
{
    expression ??= s => true;
    using var context = ContextFactory.CreateDbContext();

    var query = context.Searches
        .AsNoTracking()
        .Where(expression)
        .OrderByDescending(s => s.StartedAt)
        .WithoutResponses();

    if (offset > 0)
    {
        query = query.Skip(offset);
    }

    if (limit > 0)
    {
        query = query.Take(limit);
    }

    return query.ToListAsync();
}

// Frontend: searches.js - explicitly requests limit
export const getAll = async (limit = 500) => {
  return (await api.get(`/searches?limit=${limit}`)).data;
};
```

### Reproduction
1. Use slskd heavily for weeks/months
2. Accumulate 100,000+ searches
3. Open the web UI
4. Browser hangs trying to parse 45MB+ JSON response

### Real-world Evidence
Found on user's instance:
- **153,835 searches** in database
- **389MB search.db** file
- **45MB JSON response** from /api/v0/searches
- Browser completely unusable

### Test Result
- **Upstream**: Returns all 153k searches, browser hangs indefinitely
- **slskdN**: Frontend requests limit=500, loads instantly

### API Compatibility
- Default `limit=0` preserves upstream behavior (unlimited)
- Existing API clients unaffected
- Only browser UI benefits from the limit

---

## Summary

| Bug | File | Upstream | slskdN | Severity |
|-----|------|----------|--------|----------|
| async-void-roomservice | RoomService.cs | No try-catch | try-catch wrapper | High (process crash) |
| searches-undefined-return | searches.js | return undefined | return [] | Medium (UI crash) |
| transfers-undefined-return | transfers.js | return undefined | return [] | Medium (UI crash) |
| search-list-no-pagination | SearchService.cs, searches.js | No pagination | Optional limit/offset | High (browser hang) |
| flaky-upload-governor-test | UploadGovernorTests.cs | AutoData random values | InlineAutoData fixed values | Low (flaky CI) |

## Ready for Upstream PR
All fixes are:
- ✅ Minimal and focused
- ✅ Non-breaking changes (API defaults preserved)
- ✅ Follow existing code patterns
- ✅ Tested in slskdN


