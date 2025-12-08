# Agent Instructions for slskdN (Experimental Branch)

> This file defines how AI coding assistants should behave in this repository.  
> **Note**: This is the experimental/multi-source-swarm branch with security hardening.

---

## Before Starting Any Work

1. **Read context files** (in order):
   - `memory-bank/projectbrief.md` - Understand experimental features and constraints
   - `memory-bank/tasks.md` - See current task list (prioritize security items)
   - `memory-bank/activeContext.md` - Know what's currently being worked on
   - `CLEANUP_TODO.md` - Review hardening tasks

2. **For non-trivial changes**, create or update a task in `memory-bank/tasks.md`

3. **Check existing code patterns** before implementing new features

4. **Security-sensitive changes** require extra scrutiny - check `docs/SECURITY_IMPLEMENTATION_STATUS.md`

---

## During Development

### Code Style

**Backend (C#)**:
- Follow existing patterns in `src/slskd/`
- Use file-scoped namespaces (C# 10+)
- Use `_privateField` naming for injected dependencies
- **Prefer `ILogger<T>` over `Serilog.Log.ForContext`** (standardization in progress)
- Run `./bin/lint` before committing

**Frontend (React/JSX)**:
- Follow patterns in `src/web/src/components/`
- Use Semantic UI React components
- Maintain compatibility with React 16.8.6 (no hooks that require newer versions)
- Keep state management simple (no Redux unless already used)

**Security Components** (`src/slskd/Common/Security/`):
- All security services are in `Common/Security/`
- Use `SecurityEventSink` for event logging
- Follow existing patterns (PathGuard, ContentSafety, etc.)
- Add unit tests for any new security logic

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
- `DhtRendezvous/`
- `Common/Security/`

---

## Experimental Branch Specific Rules

### Multi-Source Downloads
- Code is in `src/slskd/Transfers/MultiSource/`
- **Known issue**: Unbounded concurrency in retry loops (needs SemaphoreSlim)
- Test with `./swarm-download-test.sh` scripts

### Security Framework
- 30 components in `src/slskd/Common/Security/`
- 121 unit tests in `tests/slskd.Tests.Unit/Security/`
- Enable with: `builder.Services.AddSlskdnSecurity(builder.Configuration)`
- **Integration needed**: Wire into transfer handlers

### DHT Rendezvous
- Code is in `src/slskd/DhtRendezvous/`
- Still experimental, needs testing

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

6. **For security changes**: Ensure unit tests cover edge cases

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
- **Don't** bypass security validation in file handling code
- **Don't** use unbounded parallelism (use SemaphoreSlim for worker pools)

---

## Quick Reference

| Action | Command |
|--------|---------|
| Run backend | `./bin/watch` |
| Run frontend | `cd src/web && npm start` |
| Run tests | `dotnet test` |
| Run security tests | `dotnet test --filter "FullyQualifiedName~Security"` |
| Lint | `./bin/lint` |
| Build release | `./bin/build` |

| File | Purpose |
|------|---------|
| `memory-bank/projectbrief.md` | Project overview & constraints |
| `memory-bank/tasks.md` | Task backlog (source of truth) |
| `memory-bank/activeContext.md` | Current work context |
| `memory-bank/progress.md` | Work log |
| `memory-bank/scratch.md` | Temporary notes |
| `CLEANUP_TODO.md` | Hardening tasks |
| `docs/SECURITY_IMPLEMENTATION_STATUS.md` | Security component status |
| `docs/FRONTEND_MIGRATION_PLAN.md` | React/Vite migration plan |

