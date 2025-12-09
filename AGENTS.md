# AI Agent Instructions

Read this file first. Then read the memory bank.

---

## 🚨 CRITICAL: Save Your Work Often

**Commit after every meaningful change.** Don't batch up hours of work.

```bash
git add -A && git commit -m "type: Brief description"
```

If you fix a bug, commit it immediately. If you add a feature, commit it immediately. If you refactor something, commit it immediately.

**Why?** Context windows end. Sessions crash. Work gets lost. Commit often.

---

## 🚨 CRITICAL: Document Bugs You Fix

**If you fix a bug, add it to `memory-bank/decisions/adr-0001-known-gotchas.md` BEFORE doing anything else.**

This is not optional. This prevents the same bug from happening again.

Template:
```markdown
### N. short-descriptive-name

**The Bug**: One sentence description.

**Files Affected**:
- `path/to/file`

**Wrong**:
```code
// bad code
```

**Correct**:
```code
// good code
```

**Why This Keeps Happening**: Root cause explanation.
```

---

## Before Starting Work

1. **Read context files**:
   ```
   memory-bank/projectbrief.md   # What is this project?
   memory-bank/activeContext.md  # What was I working on?
   memory-bank/tasks.md          # What needs to be done?
   ```

2. **Check for gotchas**:
   ```
   memory-bank/decisions/adr-0001-known-gotchas.md
   ```

3. **Grep before you write**:
   ```bash
   grep -rn "pattern" src/
   ```
   Don't invent new patterns. Use what exists.

---

## During Development

### Code Style
- Match existing patterns exactly
- Don't add abstractions that don't exist
- Don't add defensive null checks internally
- Don't add logging spam
- Don't create interfaces for single implementations

### Commits
- One logical change per commit
- Format: `type: Brief description`
- Types: `feat`, `fix`, `refactor`, `docs`, `chore`, `test`

### CLI Efficiency
```bash
# GOOD: Chained
dotnet build && dotnet test

# GOOD: Parallel
task1 & task2 & wait

# GOOD: Piped
find . -name "*.cs" | xargs grep "TODO"

# BAD: Sequential separate commands
dotnet build
dotnet test
```

---

## After Completing Work

1. **Update progress**:
   ```
   memory-bank/progress.md  # Add timestamped entry
   ```

2. **Update active context**:
   ```
   memory-bank/activeContext.md  # What's the current state?
   ```

3. **Commit everything**:
   ```bash
   git add -A && git commit -m "type: description"
   ```

---

## What NOT to Do

- ❌ Don't invent new abstractions or patterns
- ❌ Don't add code "just in case"
- ❌ Don't swallow exceptions silently
- ❌ Don't add configuration for everything
- ❌ Don't create factories/wrappers/managers without need
- ❌ Don't batch up work - commit often
- ❌ Don't fix bugs without documenting them

---

## File Reference

| File | Purpose | When to Update |
|------|---------|----------------|
| `projectbrief.md` | Project overview | Rarely |
| `tasks.md` | Task list | When tasks change |
| `activeContext.md` | Current state | Every session |
| `progress.md` | Work log | After completing work |
| `scratch.md` | Quick notes | Anytime |
| `decisions/*.md` | Architecture decisions | When making decisions |

---

## Quick Commands

```bash
# Check what's changed
git status && git diff --stat

# Commit current work
git add -A && git commit -m "type: description"

# Push changes
git push origin $(git branch --show-current)

# Search codebase
grep -rn "pattern" src/

# Find files
find . -name "*.ext" -type f
```
