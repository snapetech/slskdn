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

1. **`docs/archive/implementation/AI_START_HERE.md`** - Complete AI assistant guide
2. **`memory-bank/decisions/adr-0001-known-gotchas.md`** - Critical bugs to avoid
3. **`memory-bank/decisions/adr-0002-code-patterns.md`** - Exact patterns to follow
4. **`memory-bank/decisions/adr-0003-anti-slop-rules.md`** - What NOT to do
5. **`memory-bank/activeContext.md`** - Current session state

### Before Writing Code

```bash
# ALWAYS grep for existing patterns first
grep -rn "similar pattern" src/slskd/
grep -rn "similar component" src/web/src/components/
```

If you skip this step, you WILL generate slop.

---

## Core Principles

### Network Health First 🌐

**slskdn prioritizes Soulseek network health in ALL design decisions.**

- Features like passive FLAC discovery, backfill scheduling, and hash probing are **intentionally conservative**
- When implementing features that contact remote peers (browsing, downloading headers, probing files):
  - Always consider **rate limiting** and **bandwidth impact**
  - Be a **good network citizen** - don't overwhelm peers
  - **Manual triggers** (like buttons) are preferred over automatic aggressive scanning
  - Give users **control** over network impact

### UI Buttons Need Tooltips 💬

Every button must have a helpful mouseover tooltip (using Semantic UI's `Popup` component) explaining:
- What the button does
- Why a user might want to click it

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
- **Never introduce lint errors.** Fix any lint issues immediately before running builds/tests. Avoid disabling lint rules unless there is a documented, unavoidable reason.

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

### 1. Deferred and Follow-up (plan-driven work)

When the work comes from a plan that has a **Deferred and Follow-up Work** table (e.g. `docs/dev/40-fixes-plan.md`):

- **If you intentionally leave out** optional, out-of-scope, or “do later” items: **add a row** to that table before considering the PR/task done. Columns: **Source** (e.g. PR-08), **Item** (short name), **Action** (what to do).
- **If nothing was left out** for that PR/item, no new deferred row is needed.
- **When you later complete a deferred item:** remove that row from the Deferred table and do the usual doc updates below (progress, tasks, activeContext).

### 2. Docs to update on completion

| Doc | When / What |
|-----|-------------|
| **`docs/dev/40-fixes-plan.md`** | If doing dev/40-fixes work: Deferred table (add rows for left-out work; remove rows when deferred items are completed). Update checklists or Implementation Ticket Index if the plan has them. |
| **`config/slskd.example.yml`** | If you added or changed options: add or update the commented example. |
| **`memory-bank/tasks.md`** | Mark task complete with date; add any new follow-up tasks. |
| **`memory-bank/progress.md`** | Append a short, timestamped summary of what was done; note surprises or decisions. |
| **`memory-bank/activeContext.md`** | Clear the current task if finished; set “Next Steps”. |

### 3. Run tests and lint

- **Run tests:** `dotnet test`
- **Run lint:** `./bin/lint`

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
- ❌ **Stubs, placeholders, or `NotImplementedException`** (create tasks in `memory-bank/tasks.md` instead)
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

## 🚨 CRITICAL: Build Process - Tag-Only Builds

**DO NOT trigger builds by pushing code. Builds ONLY happen on tags.**

### Build System Rules

1. **NO automatic builds on code pushes** - The CI workflow does NOT run on pushes to master
2. **Builds ONLY trigger on tags** - You must create a tag to trigger a build
3. **DO NOT modify workflows** to add automatic builds - this is intentional
4. **DO NOT create tags** unless explicitly asked by the user

### How Builds Work

**CI Workflow (`ci.yml`):**
- Triggers: **Tags only** (version tags, `build-dev-*`, `build-main-*`)
- Also runs on: Pull requests (for testing) and manual `workflow_dispatch`
- Does: Build, test, publish binaries, Docker (only on tags)

**Build on Tag (`build-on-tag.yml`):**
- Triggers: `build-dev-*` or `build-main-*` tags
- Does: Full release build with packages (AUR, COPR, PPA, etc.)

**Dev Release (`dev-release.yml`):**
- Triggers: `dev-*` tags
- Does: Dev package builds (AUR, COPR)

### To Trigger a Build (Only if User Requests)

```bash
# Main/stable release
git tag build-main-0.24.1-slskdn.41
git push origin build-main-0.24.1-slskdn.41

# Dev release
git tag build-dev-0.24.1.dev.$(date -u +%Y%m%d.%H%M%S)
git push origin $(git describe --tags --abbrev=0)
```

**Never create tags automatically** - always wait for explicit user instruction.

---

## Quick Reference

| Action | Command |
|--------|---------|
| Run backend | `./bin/watch` |
| Run frontend | `cd src/web && npm start` |
| Run tests | `dotnet test` |
| Lint | `./bin/lint` |
| Build release | `./bin/build` |

## 🏗️ Build and Release Process

### ⚠️ CRITICAL: Builds Only Happen on Tags

**DO NOT trigger builds by pushing code. Builds ONLY happen when you create a tag.**

The CI workflow (`ci.yml`) is configured to:
- ✅ **Run on tags only**: `build-main-*`, `build-dev-*`, or version tags
- ✅ **Run on pull requests**: For testing/validation
- ✅ **Run on manual dispatch**: Via GitHub UI
- ❌ **DOES NOT run on pushes to master**: Code updates do NOT trigger builds

### How to Trigger a Build

**For stable releases (main channel):**
```bash
git tag build-main-0.24.1-slskdn.41
git push origin build-main-0.24.1-slskdn.41
```

**For dev releases:**
```bash
git tag build-dev-0.24.1.dev.$(date -u +%Y%m%d.%H%M%S)
git push origin build-dev-0.24.1.dev.$(date -u +%Y%m%d.%H%M%S)
```

### Build Workflows

1. **CI Workflow** (`ci.yml`): Builds, tests, publishes binaries, Docker (only on tags)
2. **Build on Tag** (`build-on-tag.yml`): Full release with packages (AUR, COPR, PPA, etc.)
3. **Dev Release** (`dev-release.yml`): Dev package builds (AUR, COPR)

### What NOT to Do

- ❌ **DO NOT** push code expecting a build to happen automatically
- ❌ **DO NOT** modify workflows to trigger on branch pushes
- ❌ **DO NOT** create tags unless you actually want a build/release
- ✅ **DO** create tags explicitly when you want to build/release

See `memory-bank/decisions/adr-0005-tagging-system.md` for detailed tag format and channel information.

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
| `docs/dev/40-fixes-plan.md` | dev/40-fixes security plan; **Deferred and Follow-up Work** table — add rows when leaving work out; remove when completing deferred items. See AGENTS.md § After Completing Work. |
| `docs/archive/FORK_VISION.md` | Feature roadmap |
| `docs/archive/DEVELOPMENT_HISTORY.md` | Release history |
| `TODO.md` | Human-maintained todo list |

