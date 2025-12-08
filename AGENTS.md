# Agent Instructions for slskdN

> This file defines how AI coding assistants should behave in this repository.

---

## Before Starting Any Work

1. **Read context files** (in order):
   - `memory-bank/projectbrief.md` - Understand what this project is
   - `memory-bank/tasks.md` - See current task list
   - `memory-bank/activeContext.md` - Know what's currently being worked on

2. **For non-trivial changes**, create or update a task in `memory-bank/tasks.md`

3. **Check existing code patterns** before implementing new features

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

- **Don't** silently overwrite human-written notes; append with timestamps
- **Don't** create new abstractions without checking if similar patterns exist
- **Don't** add dependencies without documenting why
- **Don't** break API compatibility with upstream slskd
- **Don't** modify upstream files unnecessarily (prefer extending)

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
| `memory-bank/projectbrief.md` | Project overview & constraints |
| `memory-bank/tasks.md` | Task backlog (source of truth) |
| `memory-bank/activeContext.md` | Current work context |
| `memory-bank/progress.md` | Work log |
| `memory-bank/scratch.md` | Temporary notes |
| `FORK_VISION.md` | Feature roadmap |
| `DEVELOPMENT_HISTORY.md` | Release history |
| `TODO.md` | Human-maintained todo list |

