# ADR-0003: Anti-Slop Rules

> **Includes CLI efficiency rules** - applies to terminal commands too!

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

---

## Rule 16: Don't Over-Comment Obvious Code

**Problem**: AI adds comments that describe what the code literally does.

**Slop**:
```csharp
// Get the user from the database
var user = await GetUserAsync(username);

// Check if user is null
if (user == null)
{
    // Return not found
    return NotFound();
}

// Return the user
return Ok(user);
```

**Correct**: Only comment *why*, not *what*.

```csharp
var user = await GetUserAsync(username);
if (user == null)
    return NotFound();

// Include share stats for the profile view
return Ok(new { user, shares = await GetShareStatsAsync(username) });
```

---

## Rule 17: Don't Create Unnecessary Intermediate Variables

**Problem**: AI creates variables that are used exactly once on the next line.

**Slop**:
```csharp
var userService = UserService;
var username = request.Username;
var user = await userService.GetAsync(username);
var result = Ok(user);
return result;
```

**Correct**: Chain naturally, create variables only when they add clarity.

```csharp
return Ok(await UserService.GetAsync(request.Username));
```

---

## Rule 18: Don't Add Regions

**Problem**: AI adds `#region` blocks that hide code and add noise.

**Slop**:
```csharp
#region Private Methods

private void DoSomething() { }

#endregion

#region Public Methods

public void DoSomethingPublic() { }

#endregion
```

**Correct**: Just write the code. If a file needs regions, it's too big.

---

## Rule 19: Don't Repeat Yourself in Error Messages

**Problem**: AI creates redundant error messages.

**Slop**:
```csharp
throw new InvalidOperationException($"Invalid operation: Cannot perform operation because the operation is not valid");
```

**Correct**: Be concise and specific.

```csharp
throw new InvalidOperationException("User is not online");
```

---

## Rule 20: Don't Use LINQ When a Simple Loop is Clearer

**Problem**: AI chains LINQ operations that are harder to read than a loop.

**Slop**:
```csharp
var result = items
    .Where(x => x.IsActive)
    .Select(x => new { x.Id, x.Name })
    .GroupBy(x => x.Name)
    .Select(g => new { Name = g.Key, Count = g.Count() })
    .OrderByDescending(x => x.Count)
    .Take(10)
    .ToList();
```

**Correct**: Use LINQ for simple transforms, loops for complex logic.

```csharp
// Simple LINQ is fine
var activeItems = items.Where(x => x.IsActive).ToList();

// Complex? Use a method with clear name
var topNames = GetTopNamesByFrequency(items, 10);
```

---

## Rule 21: Don't Add Empty Catch Blocks

**Problem**: AI adds catch blocks that do nothing.

**Slop**:
```csharp
try
{
    await DoSomethingAsync();
}
catch
{
    // ignore
}
```

**Correct**: Either handle the error meaningfully or don't catch it.

```csharp
// If you truly need to ignore, be explicit about why
try
{
    await TryCleanupAsync(); // Best-effort cleanup, failure is acceptable
}
catch (IOException)
{
    // Cleanup failure is non-fatal, continue with shutdown
}
```

---

## Rule 22: Don't Create "Manager" or "Helper" Classes

**Problem**: AI creates vague classes with unclear responsibilities.

**Slop**:
```csharp
public class TransferManager { }
public class UserHelper { }
public class DataProcessor { }
public class ServiceHandler { }
```

**Correct**: Name classes after what they *are* or what they *do* specifically.

```csharp
public class TransferService { }  // It's a service
public class DownloadQueue { }    // It's a queue
public class ShareScanner { }     // It scans shares
```

---

## Rule 23: Don't Add Unnecessary Async/Await

**Problem**: AI adds async/await when just returning a Task is fine.

**Slop**:
```csharp
public async Task<User> GetUserAsync(string username)
{
    return await repository.FindAsync(username);
}
```

**Correct**: Just return the Task directly when there's no additional work.

```csharp
public Task<User> GetUserAsync(string username)
{
    return repository.FindAsync(username);
}
```

(Exception: Keep async/await if you need the stack trace for debugging or have a using block)

---

## Rule 24: Don't Use String Concatenation for Paths

**Problem**: AI concatenates paths with `+` or string interpolation.

**Slop**:
```csharp
var path = baseDir + "/" + subDir + "/" + filename;
var path2 = $"{baseDir}/{subDir}/{filename}";
```

**Correct**: Use `Path.Combine()`.

```csharp
var path = Path.Combine(baseDir, subDir, filename);
```

---

## Rule 25: Don't Create Constants Files

**Problem**: AI creates a `Constants.cs` file to hold random values.

**Slop**:
```csharp
public static class Constants
{
    public const int MaxRetries = 3;
    public const string DefaultUsername = "anonymous";
    public const int BufferSize = 4096;
}
```

**Correct**: Put constants where they're used.

```csharp
// In the class that uses them
public class DownloadService
{
    private const int MaxRetries = 3;
    private const int BufferSize = 4096;
}
```

---

## Rule 26: Frontend - Don't Destructure Props Then Immediately Spread

**Problem**: AI destructures props then spreads them.

**Slop**:
```jsx
const MyComponent = ({ onClick, className, ...rest }) => {
  return <button onClick={onClick} className={className} {...rest} />;
};
```

**Correct**: Either destructure what you need or spread everything.

```jsx
const MyComponent = (props) => {
  return <button {...props} />;
};

// Or if you need to modify
const MyComponent = ({ className, ...rest }) => {
  return <button className={`my-button ${className}`} {...rest} />;
};
```

---

## Rule 27: Frontend - Don't Create Wrapper Components for Styling

**Problem**: AI creates components that just add a className.

**Slop**:
```jsx
const PrimaryButton = ({ children, ...props }) => (
  <Button className="primary-button" {...props}>{children}</Button>
);

const SecondaryButton = ({ children, ...props }) => (
  <Button className="secondary-button" {...props}>{children}</Button>
);
```

**Correct**: Just use the className directly, or use CSS.

```jsx
<Button className="primary-button">Click me</Button>
```

---

## Rule 28: Don't Use Boolean Parameters

**Problem**: AI adds boolean parameters that make call sites unreadable.

**Slop**:
```csharp
await ProcessAsync(user, true, false, true);
```

**Correct**: Use named parameters, enums, or options objects.

```csharp
await ProcessAsync(user, includeHistory: true, forceRefresh: false);

// Or better, options object
await ProcessAsync(user, new ProcessOptions { IncludeHistory = true });
```

---

## Rule 29: Don't Add "Utils" Namespaces

**Problem**: AI creates catch-all utility namespaces.

**Slop**:
```csharp
namespace slskd.Utils
{
    public static class StringUtils { }
    public static class DateUtils { }
    public static class FileUtils { }
}
```

**Correct**: Put utilities in the namespace where they're used, or use extension methods.

```csharp
namespace slskd.Transfers
{
    internal static class TransferPathExtensions { }
}
```

---

## Rule 30: Don't Generate Boilerplate Equals/GetHashCode

**Problem**: AI adds Equals/GetHashCode overrides that aren't needed.

**Slop**:
```csharp
public class User
{
    public string Username { get; set; }

    public override bool Equals(object obj)
    {
        return obj is User user && Username == user.Username;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Username);
    }
}
```

**Correct**: Only override if you actually need value equality. For DTOs/entities, the default reference equality is usually fine.

---

## Summary: The Golden Rules

1. **Grep first** - search the codebase before writing new code
2. **Less is more** - don't add code that doesn't serve a purpose
3. **Trust internal code** - validate at boundaries only
4. **Let errors propagate** - don't swallow exceptions
5. **Keep it simple** - no factories, no wrappers, no abstractions without need
6. **Match the style** - if the codebase uses X, use X
7. **Name things clearly** - no Manager, Helper, Utils, Handler
8. **Comment why, not what** - code should be self-documenting
9. **One thing per commit** - atomic, focused changes
10. **Test the happy path** - don't over-test edge cases that can't happen

---

## Rule 31: CLI - Prefer Chaining Over Sequential Commands

**Problem**: AI runs commands one at a time, leaving resources hanging.

**Slop**:
```bash
# Separate commands, wasteful
dotnet build
dotnet test
./bin/lint
```

**Correct**:
```bash
# Chained - stops on first failure
dotnet build && dotnet test && ./bin/lint

# Parallel when independent
dotnet build & npm --prefix src/web run build & wait

# Piped for filtering
gh run list --limit 10 | grep -i fail | head -3
```

---

## Rule 32: CLI - Don't Repeat Commands When One Suffices

**Problem**: AI runs multiple similar commands instead of combining.

**Slop**:
```bash
grep "error" file.log
grep "warning" file.log
grep "fatal" file.log
```

**Correct**:
```bash
# Single grep with alternation
grep -E "error|warning|fatal" file.log

# Or with multiple patterns
grep -e "error" -e "warning" -e "fatal" file.log
```

---

## Rule 33: CLI - Use Subshells for Directory Changes

**Problem**: AI changes directory then forgets to change back.

**Slop**:
```bash
cd src/web
npm install
npm run build
cd ../..  # Easy to forget
```

**Correct**:
```bash
# Subshell - automatically returns to original dir
(cd src/web && npm install && npm run build)

# Or use --prefix for npm
npm --prefix src/web install && npm --prefix src/web run build
```

---

## Rule 34: CLI - Combine Find/Grep Operations

**Problem**: AI iterates manually when find can do it.

**Slop**:
```bash
files=$(find . -name "*.cs")
for f in $files; do
  grep -l "TODO" "$f"
done
```

**Correct**:
```bash
# Single command
find . -name "*.cs" -exec grep -l "TODO" {} +

# Or with xargs for better performance
find . -name "*.cs" | xargs grep -l "TODO"
```

---

## Rule 35: Never Use Stubs - Create Tasks Instead

**Problem**: AI creates stub implementations (methods that return placeholder values, throw `NotImplementedException`, or have `// TODO` comments) instead of properly implementing functionality or creating tasks.

**Slop**:
```csharp
// WRONG - stub implementation
public bool Verify(ControlEnvelope envelope)
{
    // Stub verification always true for build
    return true;
}

public static Ed25519KeyPair Generate()
{
    // Stub: generate random bytes for pub/priv
    var priv = RandomNumberGenerator.GetBytes(32);
    var pub = RandomNumberGenerator.GetBytes(32);
    return new Ed25519KeyPair(pub, priv, DateTimeOffset.UtcNow);
}

public Task<string> CreateJobAsync()
{
    // TODO: Implement job creation
    return Task.FromResult(Guid.NewGuid().ToString());
}
```

**Correct**: Either implement the functionality properly, or create a task/todo in `memory-bank/tasks.md` and document what needs to be done.

```csharp
// CORRECT - real implementation
public bool Verify(ControlEnvelope envelope)
{
    if (string.IsNullOrWhiteSpace(envelope.PublicKey) || string.IsNullOrWhiteSpace(envelope.Signature))
    {
        return false;
    }

    try
    {
        var publicKeyBytes = Convert.FromBase64String(envelope.PublicKey);
        var signatureBytes = Convert.FromBase64String(envelope.Signature);
        var payload = BuildSignablePayload(envelope);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);

        var publicKey = PublicKey.Import(SignatureAlgorithm.Ed25519, publicKeyBytes, KeyBlobFormat.RawPublicKey);
        return SignatureAlgorithm.Ed25519.Verify(publicKey, payloadBytes, signatureBytes);
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Signature verification failed");
        return false;
    }
}

// OR if not ready to implement, create a task:
// In memory-bank/tasks.md:
// - [ ] **T-1421**: Implement Ed25519 keypair generation using NSec.Cryptography
//   - Status: pending
//   - Priority: P1 (security critical)
//   - Notes: Currently returns random bytes, needs real Ed25519 key generation
```

**When to create tasks instead of stubs**:
- The feature is not yet designed
- Dependencies are missing
- The implementation requires significant research
- It's part of a larger feature that isn't started

**When to implement immediately**:
- The feature is well-defined
- All dependencies are available
- It's a small, isolated change
- It's blocking other work

**Key principle**: **Stubs hide incomplete work and create technical debt. Tasks make incomplete work visible and trackable.**

---

## Quick Self-Check

Before submitting code, ask yourself:

- [x] Did I grep for existing patterns first?
- [ ] Would a senior developer look at this and say "why?"
- [x] Am I adding code just because "it might be useful"?
- [x] Is there a simpler way to do this?
- [x] Does this match how the rest of the codebase does it?
- [x] Did I leave any stubs, placeholders, or `NotImplementedException`?
- [x] If I couldn't implement something, did I create a task in `memory-bank/tasks.md`?

If you answered "no" to the first question, stop and grep first.
If you answered "yes" to the stub question, either implement it or create a task.

---

*Last updated: 2025-12-10*

