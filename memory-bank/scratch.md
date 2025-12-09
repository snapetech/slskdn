# Scratch Pad

> Quick notes, references, and temporary information.

---

## Quick Reference

### Git Commands

```bash
# Status and diff
git status && git diff --stat

# Commit (do this often!)
git add -A && git commit -m "type: description"

# Push
git push origin $(git branch --show-current)

# Create branch
git checkout -b feature/name

# Switch branch
git checkout branch-name

# Merge
git checkout main && git merge feature/name

# Rebase (clean history)
git rebase -i HEAD~3
```

### Search Commands

```bash
# Search code (grep)
grep -rn "pattern" src/

# Search with context
grep -rn -B2 -A2 "pattern" src/

# Find files
find . -name "*.ext" -type f

# Find and search
find . -name "*.py" | xargs grep "pattern"
```

### CLI Efficiency

```bash
# Chain commands (stops on failure)
cmd1 && cmd2 && cmd3

# Parallel execution
cmd1 & cmd2 & wait

# Pipe output
cmd1 | grep "filter" | head -10

# Subshell (returns to original dir)
(cd subdir && do_stuff)

# Conditional
[ -f file ] && echo "exists"

# Loop
for f in *.txt; do echo "$f"; done
```

### GitHub CLI (gh)

```bash
# List CI runs
gh run list --limit 5

# View failed logs
gh run view <run-id> --log-failed

# Watch running workflow
gh run watch <run-id>

# Re-run failed
gh run rerun <run-id>

# Create PR
gh pr create --title "Title" --body "Body"

# View PR status
gh pr checks
```

---

## File Locations

| What | Where |
|------|-------|
| Project overview | `memory-bank/projectbrief.md` |
| Current task | `memory-bank/activeContext.md` |
| Task list | `memory-bank/tasks.md` |
| Work log | `memory-bank/progress.md` |
| Known bugs | `memory-bank/decisions/adr-0001-known-gotchas.md` |
| AI rules | `AGENTS.md` |
| Cursor rules | `.cursor/rules/*.mdc` |

---

## Notes

<!-- Add temporary notes here -->

---

## TODO (Temporary)

<!-- Quick todos that haven't been added to tasks.md yet -->
