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

*Last updated: 2025-12-08*

