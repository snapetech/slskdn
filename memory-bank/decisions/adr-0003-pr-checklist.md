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

- [ ] Grepped for existing patterns before writing new code
- [ ] No new abstractions (interfaces, factories, wrappers) without justification
- [ ] No defensive null checks in internal code
- [ ] No swallowed exceptions (catch blocks that hide errors)
- [ ] No logging spam (entry/exit logging)
- [ ] Matches existing code style

## Testing

- [ ] Code compiles/runs without errors
- [ ] Existing tests still pass
- [ ] Manual smoke test of changed functionality

## Git Hygiene

- [ ] Commit message follows format: `type: description`
- [ ] One logical change per commit
- [ ] No unrelated changes bundled together

## Documentation

- [ ] Updated `activeContext.md` if needed
- [ ] Added to `progress.md` if significant work
- [ ] Added gotcha if fixed a bug

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





















