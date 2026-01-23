# ADR-0003: Pre-Commit Checklist

> **Status**: Active  
> **Date**: [Date Created]  
> **Purpose**: Validation before committing code

Run through this checklist before committing.

---

## 🚨 First: Did You Fix a Bug?

If yes, **document it in `adr-0001-known-gotchas.md` NOW** before continuing.

---

## Code Quality

- [x] Grepped for existing patterns before writing new code
- [x] No new abstractions (interfaces, factories, wrappers) without justification
- [x] No defensive null checks in internal code
- [x] No swallowed exceptions (catch blocks that hide errors)
- [x] No logging spam (entry/exit logging)
- [x] Matches existing code style

## Testing

- [x] Code compiles/runs without errors
- [x] Existing tests still pass
- [x] Manual smoke test of changed functionality

## Git Hygiene

- [x] Commit message follows format: `type: description`
- [x] One logical change per commit
- [x] No unrelated changes bundled together

## Documentation

- [x] Updated `activeContext.md` if needed
- [x] Added to `progress.md` if significant work
- [x] Added gotcha if fixed a bug

---

## Commit Message Types

| Type | Use For |
|------|---------|
| `feat` | New feature |
| `fix` | Bug fix |
| `refactor` | Code change that doesn't fix bug or add feature |
| `docs` | Documentation only |
| `test` | Adding or fixing tests |
| `chore` | Build, CI, dependencies |
| `style` | Formatting, whitespace |

---

## Quick Validation Commands

```bash
# Check for common issues (customize for your project)

# Uncommitted changes
git status

# What will be committed
git diff --cached --stat

# Build
[your build command]

# Test
[your test command]

# Lint
[your lint command]
```






