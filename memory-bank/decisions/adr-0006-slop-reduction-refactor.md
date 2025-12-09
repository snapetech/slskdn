# ADR-0006: Slop Reduction Refactor Guide

> **Status**: Active  
> **Date**: 2025-12-09  
> **Purpose**: Guide for refactoring fork-specific code to reduce AI-generated slop

This document identifies code added since the fork that needs refactoring for efficiency, security, and cleanliness.

---

## Scope: Fork-Specific Directories Only

**Only refactor these directories** (code we added, not upstream):

```
src/slskd/
├── Capabilities/           # Capability negotiation
├── HashDb/                 # File hash database
├── Mesh/                   # Mesh networking
├── Backfill/               # Backfill scheduling
├── DhtRendezvous/          # DHT peer discovery
├── Transfers/MultiSource/  # Multi-source downloads
├── Transfers/Ranking/      # Source ranking/scoring
├── Users/Notes/            # User notes feature
├── Wishlist/               # Wishlist feature
└── Common/Security/        # Security utilities (fork additions)

src/web/src/
├── components/System/Security/   # Security UI
├── components/Shared/SlskdnStatusBar.jsx
├── lib/slskdn.js
└── (any new components we added)
```

**DO NOT refactor**:
- Upstream slskd code (unless fixing bugs)
- Core services like `TransferService`, `SearchService`, etc.
- Anything in `src/slskd/` not listed above

---

## Phase 1: Identify Slop Patterns

### 1.1 Find Unnecessary Abstractions

```bash
# Find factory/wrapper patterns we added
grep -rn "Factory\|Wrapper\|Handler\|Manager" src/slskd/Capabilities/ src/slskd/HashDb/ src/slskd/Mesh/ src/slskd/Backfill/ src/slskd/DhtRendezvous/ src/slskd/Transfers/MultiSource/ src/slskd/Transfers/Ranking/ src/slskd/Users/Notes/ src/slskd/Wishlist/

# Find interfaces with only one implementation
for dir in Capabilities HashDb Mesh Backfill DhtRendezvous Transfers/MultiSource Transfers/Ranking Users/Notes Wishlist; do
  echo "=== $dir ==="
  grep -l "^public interface" src/slskd/$dir/*.cs 2>/dev/null
done
```

**Action**: Remove unnecessary interfaces. If there's only one implementation and no plans for alternatives, just use the concrete class.

### 1.2 Find Defensive Null Checks

```bash
# Find ArgumentNullException patterns
grep -rn "ArgumentNullException\|ThrowIfNull" src/slskd/Capabilities/ src/slskd/HashDb/ src/slskd/Mesh/ src/slskd/Backfill/ src/slskd/DhtRendezvous/ src/slskd/Transfers/MultiSource/ src/slskd/Transfers/Ranking/ src/slskd/Users/Notes/ src/slskd/Wishlist/
```

**Action**: Remove internal null checks. Keep only at API boundaries (controller actions).

### 1.3 Find Logging Spam

```bash
# Find entry/exit logging
grep -rn "Entering\|Exiting\|Starting\|Finished\|Begin\|End" src/slskd/Capabilities/ src/slskd/HashDb/ src/slskd/Mesh/ src/slskd/Backfill/ src/slskd/DhtRendezvous/ src/slskd/Transfers/MultiSource/ src/slskd/Transfers/Ranking/ src/slskd/Users/Notes/ src/slskd/Wishlist/

# Find verbose logging that should be Debug/Verbose
grep -rn "Logger\.Information\|Log\.Information" src/slskd/Capabilities/ src/slskd/HashDb/ src/slskd/Mesh/ src/slskd/Backfill/ src/slskd/DhtRendezvous/ src/slskd/Transfers/MultiSource/ src/slskd/Transfers/Ranking/ src/slskd/Users/Notes/ src/slskd/Wishlist/
```

**Action**: Remove entry/exit logging. Downgrade verbose logs to Debug level.

### 1.4 Find Swallowed Exceptions

```bash
# Find catch blocks that might swallow errors
grep -B2 -A5 "catch.*Exception" src/slskd/Capabilities/ src/slskd/HashDb/ src/slskd/Mesh/ src/slskd/Backfill/ src/slskd/DhtRendezvous/ src/slskd/Transfers/MultiSource/ src/slskd/Transfers/Ranking/ src/slskd/Users/Notes/ src/slskd/Wishlist/ --include="*.cs" | grep -A5 "return null\|return default\|return new"
```

**Action**: Let exceptions propagate unless there's a specific recovery action.

---

## Phase 2: Security Hardening Review

### 2.1 Path Traversal Checks

```bash
# Find file path handling without validation
grep -rn "Path\.Combine\|File\.Read\|File\.Write\|Directory\." src/slskd/Capabilities/ src/slskd/HashDb/ src/slskd/Mesh/ src/slskd/Backfill/ src/slskd/DhtRendezvous/ src/slskd/Transfers/MultiSource/ src/slskd/Transfers/Ranking/ src/slskd/Users/Notes/ src/slskd/Wishlist/ --include="*.cs"
```

**Action**: Ensure all file paths are validated with `PathGuard.NormalizeAndValidate()`.

### 2.2 Input Validation at Boundaries

```bash
# Find controller actions
grep -rn "\[Http" src/slskd/Capabilities/API/ src/slskd/HashDb/API/ src/slskd/Mesh/API/ src/slskd/Backfill/API/ src/slskd/DhtRendezvous/API/ src/slskd/Transfers/MultiSource/Discovery/API/ src/slskd/Users/Notes/API/ src/slskd/Wishlist/API/ --include="*.cs"
```

**Action**: Ensure all controller actions validate input parameters.

### 2.3 Rate Limiting

Check that public-facing endpoints have rate limiting:
- DHT endpoints
- Discovery endpoints
- Any endpoint that accepts user input

### 2.4 Async Void Handlers

```bash
# Find async void without try-catch
grep -B2 -A10 "async void" src/slskd/Capabilities/ src/slskd/HashDb/ src/slskd/Mesh/ src/slskd/Backfill/ src/slskd/DhtRendezvous/ src/slskd/Transfers/MultiSource/ src/slskd/Transfers/Ranking/ src/slskd/Users/Notes/ src/slskd/Wishlist/ --include="*.cs"
```

**Action**: Wrap ALL async void handlers in try-catch.

---

## Phase 3: Efficiency Improvements

### 3.1 Database Query Optimization

```bash
# Find potential N+1 queries
grep -rn "foreach.*await\|\.Select.*await" src/slskd/Capabilities/ src/slskd/HashDb/ src/slskd/Mesh/ src/slskd/Backfill/ src/slskd/DhtRendezvous/ src/slskd/Transfers/MultiSource/ src/slskd/Transfers/Ranking/ src/slskd/Users/Notes/ src/slskd/Wishlist/ --include="*.cs"
```

**Action**: Batch database operations where possible.

### 3.2 Memory Allocation

```bash
# Find string concatenation in loops
grep -rn "for.*\+=" src/slskd/Capabilities/ src/slskd/HashDb/ src/slskd/Mesh/ src/slskd/Backfill/ src/slskd/DhtRendezvous/ src/slskd/Transfers/MultiSource/ src/slskd/Transfers/Ranking/ src/slskd/Users/Notes/ src/slskd/Wishlist/ --include="*.cs"

# Find LINQ ToList() that could be deferred
grep -rn "\.ToList()\." src/slskd/Capabilities/ src/slskd/HashDb/ src/slskd/Mesh/ src/slskd/Backfill/ src/slskd/DhtRendezvous/ src/slskd/Transfers/MultiSource/ src/slskd/Transfers/Ranking/ src/slskd/Users/Notes/ src/slskd/Wishlist/ --include="*.cs"
```

**Action**: Use StringBuilder for string building, defer materialization.

### 3.3 Concurrency Patterns

```bash
# Find unbounded parallelism
grep -rn "Task\.Run\|Parallel\.ForEach\|Task\.WhenAll" src/slskd/Capabilities/ src/slskd/HashDb/ src/slskd/Mesh/ src/slskd/Backfill/ src/slskd/DhtRendezvous/ src/slskd/Transfers/MultiSource/ src/slskd/Transfers/Ranking/ src/slskd/Users/Notes/ src/slskd/Wishlist/ --include="*.cs"
```

**Action**: Add SemaphoreSlim to bound parallelism.

---

## Phase 4: Code Consolidation

### 4.1 Duplicate Code

Look for similar patterns across fork directories:
- Similar DTOs that could be shared
- Similar validation logic
- Similar error handling

### 4.2 Dead Code

```bash
# Find unused private methods (requires manual review)
grep -rn "private.*\(" src/slskd/Capabilities/ src/slskd/HashDb/ src/slskd/Mesh/ src/slskd/Backfill/ src/slskd/DhtRendezvous/ src/slskd/Transfers/MultiSource/ src/slskd/Transfers/Ranking/ src/slskd/Users/Notes/ src/slskd/Wishlist/ --include="*.cs" | wc -l
```

**Action**: Remove methods that are never called.

### 4.3 Overly Complex Methods

```bash
# Find methods longer than 50 lines (rough indicator)
for file in $(find src/slskd/Capabilities/ src/slskd/HashDb/ src/slskd/Mesh/ src/slskd/Backfill/ src/slskd/DhtRendezvous/ src/slskd/Transfers/MultiSource/ src/slskd/Transfers/Ranking/ src/slskd/Users/Notes/ src/slskd/Wishlist/ -name "*.cs"); do
  awk '/^[[:space:]]*(public|private|protected|internal).*\(/{start=NR} /^[[:space:]]*\}/{if(start && NR-start>50) print FILENAME":"start"-"NR" ("NR-start" lines)"; start=0}' "$file"
done
```

**Action**: Break down methods > 50 lines into smaller, focused methods.

---

## Phase 5: Frontend Refactoring

### 5.1 Find Slop Patterns

```bash
# Find class components (should be function components)
grep -rn "class.*extends.*Component" src/web/src/components/

# Find PropTypes (not used in this codebase)
grep -rn "PropTypes" src/web/src/components/

# Find undefined returns in API libs
grep -rn "return undefined\|return;" src/web/src/lib/
```

### 5.2 Component Efficiency

```bash
# Find components without useMemo/useCallback where needed
grep -rn "useState\|useEffect" src/web/src/components/System/Security/ src/web/src/components/Shared/SlskdnStatusBar.jsx
```

**Action**: Add memoization for expensive computations.

---

## Refactoring Checklist

For each file in fork-specific directories:

- [ ] Remove unnecessary interfaces (single implementation)
- [ ] Remove defensive null checks (internal code)
- [ ] Remove logging spam (entry/exit, verbose info)
- [ ] Fix swallowed exceptions
- [ ] Add path validation for file operations
- [ ] Add input validation at API boundaries
- [ ] Wrap async void in try-catch
- [ ] Batch database operations
- [ ] Bound parallel operations with SemaphoreSlim
- [ ] Remove dead code
- [ ] Break down large methods
- [ ] Use StringBuilder for string building
- [ ] Defer LINQ materialization

---

## Priority Order

1. **Security** (path traversal, async void, input validation)
2. **Correctness** (swallowed exceptions, error handling)
3. **Efficiency** (database queries, parallelism bounds)
4. **Cleanliness** (logging, null checks, dead code)

---

## Files to Start With

Based on complexity and impact, refactor in this order:

### High Priority (Security + Complexity)
1. `DhtRendezvous/` - External network input, security critical
2. `Transfers/MultiSource/` - File operations, parallelism
3. `HashDb/` - Database operations, file hashing

### Medium Priority (User-Facing)
4. `Wishlist/` - User data, database
5. `Users/Notes/` - User data
6. `Transfers/Ranking/` - Scoring logic

### Lower Priority (Internal)
7. `Capabilities/` - Protocol negotiation
8. `Mesh/` - Internal networking
9. `Backfill/` - Background processing

---

## Testing After Refactoring

After each refactoring session:

```bash
# Build
dotnet build src/slskd/slskd.csproj

# Test
dotnet test

# Lint
./bin/lint

# Manual smoke test
./bin/watch
# Test the feature you refactored
```

---

## Commit Strategy

- One commit per file or logical unit
- Commit message format: `refactor(area): Brief description`
- Example: `refactor(wishlist): Remove defensive null checks and logging spam`

---

*Last updated: 2025-12-09*

