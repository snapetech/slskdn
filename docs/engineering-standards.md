# Engineering Standards & Code Quality

**Status**: MANDATORY FOR ALL NEW CODE  
**Created**: December 11, 2025  
**Scope**: All contributions, refactors, and new features

> **Project Note**: This is a fork of [slskd](https://github.com/slskd/slskd). See [../README.md](../README.md#acknowledgments) for attribution.

---

This document defines baseline engineering standards for this project. All new code and refactors MUST adhere to these standards.

---

## General Principles

- **Prefer clarity over cleverness.** Code is read more than written.
- **Minimize side effects.** Functions and methods should do one thing well.
- **Prefer composition over inheritance** where possible.
- **Avoid premature abstraction.** Only generalize once you have at least two clear use cases.
- **Fail fast on programming errors; fail safe on external errors.**

---

## Code Style

- **Follow the existing codebase style:**
  - Naming conventions, namespaces, file layout.
- **When in doubt:**
  - Match the style of the file you are editing.
- **Keep functions and classes small and focused:**
  - Prefer extracting helpers for complex logic.
  - Single Responsibility Principle applies.

---

## Async and IO

All network and disk IO MUST be async (`async`/`await`) end-to-end.

### DO NOT:
- Use `.Result`, `.Wait()`, or `Task.Run` to block on async work.
- Perform synchronous network or disk IO on hot paths.

### PREFER:
- Cancellation tokens on public async APIs.
- Timeouts for network calls; no unbounded waits.
- Async all the way down (no sync-over-async).

### Examples

❌ **BAD**:
```csharp
var result = SomeAsyncMethod().Result; // Deadlock risk!
```

✅ **GOOD**:
```csharp
var result = await SomeAsyncMethod(cancellationToken);
```

---

## Error Handling

- **Fail fast on programming errors** (null references, invalid state).
- **Fail safe on external errors** (network timeouts, malformed input).

### External Interactions

Wrap external interactions (network, filesystem, subprocesses) with:
- Timeouts.
- Clear, structured error types.
- Retry logic where appropriate (with exponential backoff).

### Logging

- Do not swallow exceptions silently.
- Log with sanitized context (no PII, paths, hashes, IPs).
- Return clear error codes where appropriate.

### Examples

❌ **BAD**:
```csharp
try {
    await RiskyOperation();
} catch {
    // Silent failure
}
```

✅ **GOOD**:
```csharp
try {
    await RiskyOperation(cancellationToken);
} catch (Exception ex) {
    _logger.LogError(ex, "RiskyOperation failed for reason: {Reason}", ex.Message);
    throw; // or return error response
}
```

---

## Dependency Management

- **Prefer dependency injection** for testable components.
- **Avoid global singletons** unless absolutely necessary.
- **Where possible, depend on interfaces rather than concrete types.**

### Registration

Services should be registered in `Program.cs` or a dedicated DI configuration area:

```csharp
services.AddSingleton<IMyService, MyService>();
services.AddScoped<IPerRequestService, PerRequestService>();
```

### Constructor Injection

```csharp
public class MyService
{
    private readonly IDependency _dependency;
    
    public MyService(IDependency dependency)
    {
        _dependency = dependency ?? throw new ArgumentNullException(nameof(dependency));
    }
}
```

---

## Testing

All new functionality MUST be accompanied by tests:
- **Unit tests** for pure logic.
- **Integration tests** where external systems are involved.

New code should **not reduce existing test coverage**.

### For Bug Fixes

Add a **regression test** that:
- Fails without the fix.
- Passes with it.

### Test Structure

- Arrange: Set up test data and mocks.
- Act: Execute the code under test.
- Assert: Verify the outcome.

### Examples

```csharp
[Fact]
public async Task MyMethod_WithValidInput_ReturnsExpectedResult()
{
    // Arrange
    var service = new MyService(mockDependency.Object);
    var input = new ValidInput();
    
    // Act
    var result = await service.MyMethod(input);
    
    // Assert
    Assert.Equal(expectedValue, result);
}
```

---

## Documentation

Public-facing APIs and complex internal components MUST be documented:

- **Summaries** on interfaces and key methods.
- **Short comments** for non-obvious logic.
- **XML doc comments** for public APIs.

### Design Docs

Design docs in `docs/` MUST be updated when you introduce or significantly change:
- Domain models.
- Backends and external integrations.
- Moderation and security logic.
- Multi-domain abstractions.

---

## Anti-Slop Rules

### DO NOT:

- **Introduce "god classes"** that:
  - Have too many responsibilities.
  - Know about multiple unrelated subsystems.
- **Copy-paste large blocks of code.**
  - Prefer shared helpers or abstractions.
- **Add "TODO: fix later"** without a corresponding tracked task.
- **Change behavior opportunistically** during refactors.
  - Refactor tasks should NOT change behavior unless explicitly requested.

### DO:

- **Keep diffs small and scoped.**
  - Avoid opportunistic unrelated changes.
- **Extract helpers** for repeated logic.
- **Refactor incrementally** with tests at each step.

---

## Refactoring

For refactor tasks:

- **Do not change behavior** unless explicitly requested.
- **Keep diffs small and scoped.**
  - One refactor at a time.
- **Ensure all tests pass** before and after.
- **Update design docs** if external behavior changes.

---

## Tooling

### Static Analysis

Use available analyzers and linters to catch:
- Nullability issues.
- Dead code.
- Obvious bugs.
- Misuse of async.

### Formatting

- Use a consistent formatter or `.editorconfig`.
- **Do not manually reformat unrelated code** in your PR.

### CI/CD

- Static analysis runs as part of the pipeline.
- New code cannot regress below the baseline.

---

## Specific Rules for This Project

### Async Everywhere

- All network IO (Soulseek, mesh, torrent, HTTP, catalogue fetch) MUST be async.
- All disk IO (file scanning, database operations) MUST be async.
- No `.Result`, `.Wait()`, or `Task.Run` to block on async work.

### Privacy & Security

- No full filesystem paths in logs.
- No raw hashes (SHA256, etc.) in logs or metrics.
- No IP addresses or peer IDs in logs (use sanitized forms).
- No external usernames or ActivityPub handles in logs (unless explicitly opted in).
- Low-cardinality metrics only (backend, result, domain, privacyMode).

### MCP Integration

- All content paths (scanning, VirtualSoulfind, relay, social) MUST consult MCP.
- Blocked/quarantined content NEVER appears in normal views.
- MCP events feed into reputation system.

### Work Budgets

- All network-heavy operations (catalogue fetch, external moderation, mesh/DHT queries) MUST consume work budget.
- No unbounded work (always have caps, timeouts, quotas).

### Domain Gating

- Soulseek backend ONLY for `ContentDomain.Music`.
- Non-music domains (Book, Movie, Tv, GenericFile) MUST NOT use Soulseek.
- This rule is enforced at planner level, not backend level (backends should reject invalid domains).

---

## Enforcement

Any deviation from these standards should be rare and must be justified in:
- Code review.
- Commit message.
- Design doc update.

**Code reviews MUST check for:**
- Adherence to async rules.
- Privacy/security compliance.
- Test coverage.
- Anti-slop violations (god classes, copy-paste, opportunistic changes).

---

## References

- `docs/CURSOR-META-INSTRUCTIONS.md` - Meta-rules for implementation
- `SECURITY-GUIDELINES.md` - Security and hardening requirements
- `MCP-HARDENING.md` - Moderation layer security
- `docs/virtualsoulfind-v2-design.md` - Multi-domain design
- `docs/social-federation-design.md` - Federation privacy/security

