# Global Meta-Instructions for Cursor

**Status**: MANDATORY FOR ALL TASKS  
**Created**: December 11, 2025  
**Scope**: ALL code modifications in this repository

> **Project Note**: This is a fork of [slskd](https://github.com/slskd/slskd). See [README.md](../README.md#acknowledgments) for attribution.

---

## Overview

When modifying this repo, obey the following rules. These are **non-negotiable** and take precedence over any task-specific instructions unless explicitly contradicted.

---

## 1. Do Not Renumber or Reorder Existing Tasks

**Rule**: Existing task IDs are immutable.

* ❌ Do NOT change existing task IDs (e.g. T-VC01, H-13, T-SF05)
* ❌ Do NOT "optimize" by renaming old tasks
* ✅ When adding new tasks:
  * Use new IDs (e.g. T-MCP01, T-BK01, T-PR01)
  * **APPEND** them under appropriate headings
  * Add a note if they depend on existing tasks

**Example**:
```markdown
✅ CORRECT:
## Security Hardening
- H-01: Gateway Auth ✅
- H-02: Work Budget ✅
### Moderation / Control Plane (NEW)
- T-MCP01: Moderation Core (append new section)

❌ WRONG:
## Security Hardening
- H-01: Moderation Core (renaming H-01)
- H-02: Gateway Auth (renumbering)
```

---

## 2. Minimal, Targeted Diffs

**Rule**: Keep changes surgical and focused.

### Before Editing Code:
1. **Search** for relevant symbols/types
2. **List** the files you intend to touch (in a comment or plan)
3. **Limit** changes to the smallest reasonable set of files per task

### What NOT to Do:
* ❌ "Clean up" unrelated code
* ❌ Reformat large files
* ❌ Fix unrelated warnings
* ❌ Refactor code not directly related to the task

### What to Do:
* ✅ Edit only the files necessary for the task
* ✅ Keep formatting changes to touched lines only
* ✅ Leave TODOs for unrelated issues you notice

---

## 3. Security / Privacy First

**Rule**: Treat all external input as untrusted.

### Never Introduce:
* ❌ Generic host:port TCP relays
* ❌ SOCKS proxy behavior
* ❌ HTTP CONNECT tunneling
* ❌ Arbitrary URL fetching without domain allowlist

### Never Log:
* ❌ Full filesystem paths (use filename only or internal ID)
* ❌ Raw content hashes (8-char prefix max for debugging)
* ❌ External usernames or peer IDs (use hashed/opaque IDs)
* ❌ IP addresses (use internal peer identifiers)
* ❌ Full URLs with query strings (redact query params)

### Always:
* ✅ Use domain allowlists for external HTTP calls
* ✅ Validate all mesh input, remote input, file metadata
* ✅ Use **low-cardinality** labels in metrics
* ✅ Hash or anonymize external identifiers before logging

**See**: `docs/security-hardening-guidelines.md` for full requirements.

---

## 4. Hardening and Work Budgets

**Rule**: All expensive operations must go through work budgets.

### Must Consume Work Budget:
* ✅ Network calls (HTTP, mesh, relay)
* ✅ Long-running CPU work (hashing, scanning, compression)
* ✅ External moderation API calls
* ✅ Content relay chunk serving

### Budget Exhaustion Behavior:
* ✅ **Fail fast** with clear error
* ✅ Return structured error (don't throw generic exceptions)
* ✅ Log budget exhaustion (for monitoring)
* ❌ Do NOT "try anyway" or retry without budget check

### Per-Peer Quotas:
* ✅ Enforce per-peer quotas where applicable (mesh services, relay)
* ✅ Different quotas for different operations
* ✅ Configurable thresholds

**Example**:
```csharp
// CORRECT:
if (!context.WorkBudget.TryConsume(WorkCosts.CatalogFetch))
{
    return new ServiceReply
    {
        StatusCode = ServiceStatusCodes.QuotaExceeded,
        ErrorMessage = "Work budget exhausted"
    };
}

// WRONG:
// Just do the work anyway - NO!
```

---

## 5. No Behavior Regressions Unless Explicitly Requested

**Rule**: Existing behavior is sacred unless the task says otherwise.

### For Refactor Tasks (e.g. T-VC01, T-VC02):
* ✅ Do NOT change user-visible behavior
* ✅ Existing tests MUST continue to pass unchanged
* ✅ Add new tests for new behavior
* ✅ Use adapters/wrappers instead of rewriting

### When Behavior Changes ARE Required:
* ✅ The task brief will call it out explicitly
* ✅ Document the behavior change in commit message
* ✅ Update affected tests

### Testing Requirements:
* ✅ Prove old behavior still works (regression tests)
* ✅ Prove new behavior works (new tests)

---

## 6. Async Only for Network/Disk

**Rule**: Use async correctly or not at all.

### Always:
* ✅ Use `async` all the way down for:
  * File I/O
  * Network calls
  * Database queries
* ✅ Use `await` instead of `.Result` or `.Wait()`
* ✅ Pass `CancellationToken` through async call chains

### Never:
* ❌ `.Result` or `.Wait()` on async tasks (causes deadlocks)
* ❌ Hold locks across `await` (use `SemaphoreSlim` instead)
* ❌ Fire-and-forget `Task.Run` without error handling

**Example**:
```csharp
// CORRECT:
public async Task<string> ReadFileAsync(string path, CancellationToken ct)
{
    return await File.ReadAllTextAsync(path, ct);
}

// WRONG:
public string ReadFile(string path)
{
    return File.ReadAllTextAsync(path).Result; // DEADLOCK RISK!
}
```

---

## 7. Testing Discipline

**Rule**: Every task must add or update tests.

### Each Task Must:
1. ✅ Add unit tests that directly exercise new code paths
2. ✅ Run the full test suite (or at least relevant projects) after changes
3. ✅ Ensure all tests pass before committing

### If You Adjust Public Interfaces:
* ✅ Add tests that lock in the new contract
* ✅ Test both success and failure cases
* ✅ Test boundary conditions

### Test Coverage Requirements:
* ✅ Core logic: 100%
* ✅ Error handling: Test all error paths
* ✅ Security: Test that security checks actually reject bad input

**Example Test Checklist**:
```markdown
- [ ] Happy path test
- [ ] Error path test (invalid input)
- [ ] Boundary test (empty, null, max values)
- [ ] Security test (reject malicious input)
- [ ] Regression test (old behavior still works)
```

---

## 8. Configuration Defaults

**Rule**: Defaults must be secure and conservative.

### Default Behavior:
* ✅ New features: **DISABLED** by default (opt-in)
* ✅ External services: **DISABLED** by default
* ✅ Network exposure: **LOCALHOST ONLY** by default
* ✅ Domain allowlists: **EMPTY** by default (must be configured)

### Configuration Validation:
* ✅ Validate at startup (fail fast on invalid config)
* ✅ Provide clear error messages
* ✅ Document all config options

**Example**:
```csharp
// CORRECT: Disabled by default
public class CatalogFetchOptions
{
    public bool Enabled { get; init; } = false; // OFF by default
    public string[] AllowedDomains { get; init; } = Array.Empty<string>();
}
```

---

## 9. Dependency Injection

**Rule**: Use DI properly.

### Registration:
* ✅ Register services in `Program.cs`
* ✅ Use appropriate lifetimes:
  * `AddSingleton` for stateless services
  * `AddScoped` for per-request services
  * `AddTransient` for lightweight, stateful services

### Constructor Injection:
* ✅ Inject interfaces, not concrete types
* ✅ Use `IOptionsMonitor<T>` for configuration
* ✅ Inject `ILogger<T>` for logging

### Never:
* ❌ Service locator pattern
* ❌ Static singletons (use DI instead)
* ❌ `new` for services that should be injected

---

## 10. Commit Message Format

**Rule**: Write clear, structured commit messages.

### Format:
```
<type>: <short summary> (<50 chars)

<detailed description>

Key Changes:
- Point 1
- Point 2

Tests: X/X passing
Status: <task status>
```

### Types:
* `feat`: New feature
* `fix`: Bug fix
* `refactor`: Code refactoring (no behavior change)
* `test`: Add or update tests
* `docs`: Documentation only
* `chore`: Build, dependencies, tooling

---

## Anti-Slop Checklist

Before committing any code, verify:

- [ ] No existing tasks renumbered
- [ ] Changes are minimal and targeted
- [ ] No full paths, hashes, or external IDs in logs
- [ ] Work budget integrated (if applicable)
- [ ] No behavior regressions (tests still pass)
- [ ] Async used correctly (no .Result/.Wait)
- [ ] Tests added for new code
- [ ] Configuration defaults are secure
- [ ] Commit message is clear and structured

---

## When in Doubt

1. **Read the design doc** for the relevant area
2. **Check security guidelines**: `docs/security-hardening-guidelines.md`
3. **Ask first** if you're unsure whether a change is appropriate
4. **Err on the side of caution**: Conservative changes are better than aggressive refactors

---

**These rules exist to prevent:**
- Task numbering chaos
- Behavior regressions
- Security vulnerabilities
- Privacy leaks
- Performance issues
- Deadlocks
- Untested code

**Follow them religiously.** 🔒

