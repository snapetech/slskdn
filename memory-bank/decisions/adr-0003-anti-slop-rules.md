# ADR-0003: Anti-Slop Rules

> **Status**: Active  
> **Date**: 2025-12-08  
> **Purpose**: Explicit list of things AI models keep doing wrong

---

## Rule 1: Don't Invent New Abstractions

**Problem**: AI loves creating interfaces, factories, and wrapper classes that don't exist.

**Example of slop**:
```csharp
// AI generated this nonsense
public interface ITransferHandlerFactory
{
    ITransferHandler Create(TransferType type);
}

public class TransferHandlerFactory : ITransferHandlerFactory
{
    public ITransferHandler Create(TransferType type) => type switch
    {
        TransferType.Download => new DownloadHandler(),
        TransferType.Upload => new UploadHandler(),
        _ => throw new ArgumentException()
    };
}
```

**Correct**: Just inject `IDownloadService` and `IUploadService` directly. The factory pattern doesn't exist in this codebase.

---

## Rule 2: Don't Add Defensive Null Checks

**Problem**: AI adds null checks everywhere like it's writing enterprise Java.

**Slop**:
```csharp
public async Task ProcessAsync(string username)
{
    ArgumentNullException.ThrowIfNull(username);
    if (string.IsNullOrWhiteSpace(username))
        throw new ArgumentException("Username cannot be empty", nameof(username));
    
    // finally does something
}
```

**Correct**: Internal code trusts its callers. Only validate at API boundaries (controller actions).

```csharp
public async Task ProcessAsync(string username)
{
    var user = await GetUserAsync(username);
    // ...
}
```

---

## Rule 3: Don't Wrap Everything in Try-Catch

**Problem**: AI wraps every method in try-catch, swallowing errors.

**Slop**:
```csharp
public async Task<User> GetUserAsync(string username)
{
    try
    {
        return await repository.FindAsync(username);
    }
    catch (Exception ex)
    {
        Logger.Error(ex, "Failed to get user");
        return null;  // 🔥 Now caller has to null-check
    }
}
```

**Correct**: Let exceptions propagate. Only catch when you can actually handle it.

```csharp
public async Task<User> GetUserAsync(string username)
{
    return await repository.FindAsync(username);
}
```

---

## Rule 4: Don't Add Logging Spam

**Problem**: AI adds logging to every method entry/exit.

**Slop**:
```csharp
public async Task DoWorkAsync()
{
    Logger.Debug("Entering DoWorkAsync");
    Logger.Verbose("Parameters validated");
    
    var result = await CallApiAsync();
    
    Logger.Debug("API returned successfully");
    Logger.Verbose("Result: {Result}", result);
    Logger.Debug("Exiting DoWorkAsync");
}
```

**Correct**: Log meaningful events, not method traces.

```csharp
public async Task DoWorkAsync()
{
    var result = await CallApiAsync();
    
    if (!result.Success)
    {
        Logger.Warning("API call failed: {Error}", result.Error);
    }
}
```

---

## Rule 5: Don't Create DTOs for Everything

**Problem**: AI creates request/response DTOs when the existing types work fine.

**Slop**:
```csharp
public class GetUserRequest
{
    public string Username { get; set; }
}

public class GetUserResponse
{
    public User User { get; set; }
    public bool Success { get; set; }
    public string Error { get; set; }
}
```

**Correct**: Use route parameters and return the entity directly.

```csharp
[HttpGet("{username}")]
public async Task<IActionResult> Get([FromRoute] string username)
{
    var user = await UserService.GetAsync(username);
    return Ok(user);
}
```

---

## Rule 6: Don't Add Configuration for Everything

**Problem**: AI makes everything configurable even when it shouldn't be.

**Slop**:
```csharp
public class MyOptions
{
    public int MaxRetryCount { get; set; } = 3;
    public int RetryDelayMs { get; set; } = 1000;
    public bool EnableRetry { get; set; } = true;
    public string RetryStrategy { get; set; } = "exponential";
}
```

**Correct**: Use constants for internal implementation details. Only expose config that users actually need to change.

```csharp
private const int MaxRetries = 3;
private const int RetryDelayMs = 1000;
```

---

## Rule 7: Don't Use Dependency Injection for Everything

**Problem**: AI creates interfaces and DI registrations for simple helper classes.

**Slop**:
```csharp
public interface IStringHelper
{
    string Sanitize(string input);
}

public class StringHelper : IStringHelper { }

// In Program.cs
services.AddSingleton<IStringHelper, StringHelper>();
```

**Correct**: Use static methods or extension methods for utilities.

```csharp
public static class StringExtensions
{
    public static string Sanitize(this string input) => // ...
}
```

---

## Rule 8: Don't Add Async When Not Needed

**Problem**: AI makes everything async even for synchronous operations.

**Slop**:
```csharp
public async Task<int> CalculateAsync(int x, int y)
{
    return await Task.FromResult(x + y);
}
```

**Correct**: Only use async when there's actual I/O.

```csharp
public int Calculate(int x, int y)
{
    return x + y;
}
```

---

## Rule 9: Don't Create Enums for Two Values

**Problem**: AI creates enums when a bool would do.

**Slop**:
```csharp
public enum TransferDirection
{
    Upload,
    Download
}

public void Process(TransferDirection direction)
{
    if (direction == TransferDirection.Upload) { }
}
```

**Correct**: This one actually IS an enum in the codebase. But for things like:

```csharp
// WRONG
public enum FeatureState { Enabled, Disabled }

// CORRECT
public bool IsEnabled { get; set; }
```

---

## Rule 10: Don't Add XML Doc Comments to Everything

**Problem**: AI adds verbose XML comments that just repeat the method name.

**Slop**:
```csharp
/// <summary>
///     Gets the user by username.
/// </summary>
/// <param name="username">The username of the user to get.</param>
/// <returns>The user with the specified username.</returns>
public async Task<User> GetUserAsync(string username)
```

**Correct**: Only add comments when they add value. Public API methods get docs, internal implementation doesn't need them.

```csharp
// For public API - yes
/// <summary>
///     Retrieves user profile including share statistics and online status.
/// </summary>
public async Task<User> GetUserAsync(string username)

// For internal - no
public async Task<User> GetUserAsync(string username)
```

---

## Rule 11: Frontend - Don't Add PropTypes or TypeScript Types

**Problem**: AI adds PropTypes or converts to TypeScript when the codebase is plain JS.

**Slop**:
```jsx
import PropTypes from 'prop-types';

MyComponent.propTypes = {
    username: PropTypes.string.isRequired,
    onSelect: PropTypes.func,
};
```

**Correct**: This codebase is plain JavaScript without PropTypes. Don't add them.

---

## Rule 12: Frontend - Don't Use Class Components

**Problem**: AI sometimes generates class components.

**Slop**:
```jsx
class MyComponent extends React.Component {
    render() {
        return <div>{this.props.name}</div>;
    }
}
```

**Correct**: Always use function components with hooks.

```jsx
const MyComponent = ({ name }) => {
    return <div>{name}</div>;
};
```

---

## Rule 13: Don't Add Backwards Compatibility Shims

**Problem**: AI adds compatibility code for old behavior that doesn't exist.

**Slop**:
```csharp
// Support both old and new format
public void Process(object input)
{
    if (input is OldFormat old)
        ProcessOld(old);
    else if (input is NewFormat newFormat)
        ProcessNew(newFormat);
}
```

**Correct**: Just change the code. This isn't a library with external consumers.

---

## Rule 14: Don't Create Extension Method Classes for One Method

**Problem**: AI creates extension classes with a single method.

**Slop**:
```csharp
public static class UserExtensions
{
    public static bool IsOnline(this User user) => user.Status == "online";
}
```

**Correct**: Put it in the User class or use it inline.

---

## Rule 15: Don't Add IDisposable When Not Needed

**Problem**: AI adds IDisposable to classes that don't hold unmanaged resources.

**Slop**:
```csharp
public class MyService : IMyService, IDisposable
{
    private bool _disposed;

    public void Dispose()
    {
        if (!_disposed)
        {
            // nothing to dispose
            _disposed = true;
        }
    }
}
```

**Correct**: Only implement IDisposable when you actually have something to dispose.

---

## Summary: The Golden Rules

1. **Follow existing patterns** - grep the codebase first
2. **Less is more** - don't add code that doesn't serve a purpose
3. **Trust internal code** - validate at boundaries only
4. **Let errors propagate** - don't swallow exceptions
5. **Keep it simple** - no factories, no wrappers, no abstractions without need
6. **Match the style** - if the codebase uses X, use X

---

*Last updated: 2025-12-08*

