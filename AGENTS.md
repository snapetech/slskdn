# Agent Instructions for slskdN

> This file defines how AI coding assistants should behave in this repository.

---

## 🚨 CRITICAL: Document Bugs You Fix

**If you fix a bug caused by your own implementation (or any implementation), IMMEDIATELY add it to `memory-bank/decisions/adr-0001-known-gotchas.md`.**

This is **highest priority** - do it before moving on to other work.

### When to document:
- You fixed a bug you or another AI introduced
- You fixed the same type of bug twice
- You discovered a pattern that causes crashes/errors
- You found a "gotcha" that isn't obvious

### How to document:
1. Open `memory-bank/decisions/adr-0001-known-gotchas.md`
2. Add entry under appropriate section (Critical/High/Accidental Cycles)
3. Include: What went wrong, why, and how to prevent it
4. Commit immediately with message: `docs: Add gotcha for [brief description]`

**Do NOT wait until end of session. Do NOT skip this step. Future you will thank past you.**

---

## Before Starting ANY Work

### Required Reading (in order)

1. **`memory-bank/decisions/adr-0001-known-gotchas.md`** - Critical bugs to avoid
2. **`memory-bank/decisions/adr-0002-code-patterns.md`** - Exact patterns to follow
3. **`memory-bank/decisions/adr-0003-anti-slop-rules.md`** - What NOT to do
4. **`memory-bank/activeContext.md`** - Current session state

### Before Writing Code

```bash
# ALWAYS grep for existing patterns first
grep -rn "similar pattern" src/slskd/
grep -rn "similar component" src/web/src/components/
```

If you skip this step, you WILL generate slop.

---

## During Development

### Code Style

**Backend (C#)**:
- Follow existing patterns in `src/slskd/`
- Use file-scoped namespaces (C# 10+)
- Use `_privateField` naming for injected dependencies
- Prefer `ILogger<T>` over `Serilog.Log.ForContext`
- Run `./bin/lint` before committing

**Frontend (React/JSX)**:
- Follow patterns in `src/web/src/components/`
- Use Semantic UI React components
- Maintain compatibility with React 16.8.6 (no hooks that require newer versions)
- Keep state management simple (no Redux unless already used)

### Copyright Headers [[memory:11969255]]

**New files for slskdN features**:
```csharp
// <copyright file="MyNewFeature.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
```

**Existing upstream files**: Keep original `company="slskd Team"` attribution.

**Fork-specific directories** (always use slskdN headers):
- `Capabilities/`
- `HashDb/`
- `Mesh/`
- `Backfill/`
- `Transfers/MultiSource/`
- `Transfers/Ranking/`
- `Users/Notes/`

---

## After Completing Work

1. **Update `memory-bank/tasks.md`**:
   - Mark task as complete with date
   - Add any follow-up tasks discovered

2. **Update `memory-bank/progress.md`**:
   - Append timestamped summary of what was done
   - Note any surprises or decisions made

3. **Update `memory-bank/activeContext.md`**:
   - Clear current task if finished
   - Update "Next Steps" section

4. **Run tests**: `dotnet test`

5. **Run lint**: `./bin/lint`

---

## Decision Records

For significant architectural decisions:
1. Create `memory-bank/decisions/adr-NNNN-title.md`
2. Use the ADR template format (Context, Decision, Consequences)
3. Reference the ADR in relevant code comments

---

## What NOT To Do

See `memory-bank/decisions/adr-0003-anti-slop-rules.md` for the full list.

**Critical**:
- ❌ Factory/wrapper/builder patterns (use DI directly)
- ❌ Defensive null checks on internal code
- ❌ Swallowing exceptions with catch-return-null
- ❌ Logging method entry/exit
- ❌ `async Task.FromResult()` for sync operations
- ❌ Class components in React (use function + hooks)
- ❌ Returning `undefined` from API lib functions (return `[]`)
- ❌ `async void` without try-catch (crashes the process)

**General**:
- Don't silently overwrite human-written notes; append with timestamps
- Don't create new abstractions without checking if similar patterns exist
- Don't add dependencies without documenting why
- Don't break API compatibility with upstream slskd
- Don't modify upstream files unnecessarily (prefer extending)

---

## Quick Reference

| Action | Command |
|--------|---------|
| Run backend | `./bin/watch` |
| Run frontend | `cd src/web && npm start` |
| Run tests | `dotnet test` |
| Lint | `./bin/lint` |
| Build release | `./bin/build` |

| File | Purpose |
|------|---------|
| `memory-bank/decisions/adr-0001-known-gotchas.md` | **READ FIRST** - Critical bugs |
| `memory-bank/decisions/adr-0002-code-patterns.md` | **READ FIRST** - Exact patterns |
| `memory-bank/decisions/adr-0003-anti-slop-rules.md` | **READ FIRST** - What not to do |
| `memory-bank/decisions/adr-0004-pr-checklist.md` | Pre-commit validation |
| `memory-bank/projectbrief.md` | Project overview & constraints |
| `memory-bank/tasks.md` | Task backlog (source of truth) |
| `memory-bank/activeContext.md` | Current work context |
| `memory-bank/progress.md` | Work log |
| `memory-bank/scratch.md` | Temporary notes, commands |
| `FORK_VISION.md` | Feature roadmap |
| `DEVELOPMENT_HISTORY.md` | Release history |
| `TODO.md` | Human-maintained todo list |

