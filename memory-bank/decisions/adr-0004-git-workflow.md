# ADR-0004: Git Workflow

> **Status**: Active  
> **Date**: [Date Created]  
> **Purpose**: Clean git practices

---

## Core Rule: Commit Often

**Don't batch up hours of work.** Commit after every meaningful change.

```bash
git add -A && git commit -m "type: description"
```

Why? Context windows end. Sessions crash. Internet drops. **Commit often.**

---

## Branch Naming

```
type/short-description

Examples:
  feature/user-auth
  fix/login-crash
  refactor/cleanup-api
  chore/update-deps
```

---

## Commit Messages

Format: `type: Brief description`

```
feat: Add user authentication
fix: Prevent crash on empty input
refactor: Simplify API handler
docs: Update README
chore: Upgrade dependencies
test: Add login tests
```

Keep it under 72 characters. Use imperative mood ("Add" not "Added").

---

## Workflow

### Starting Work

```bash
# Make sure you're up to date
git checkout main && git pull

# Create feature branch
git checkout -b feature/my-feature
```

### During Work

```bash
# Commit often!
git add -A && git commit -m "feat: Add thing"

# Push periodically (backup)
git push origin feature/my-feature
```

### Finishing Work

```bash
# Update from main
git checkout main && git pull
git checkout feature/my-feature
git rebase main  # or merge, depending on preference

# Push final
git push origin feature/my-feature

# Create PR (if using PRs)
gh pr create --title "feat: My feature" --body "Description"
```

---

## Common Operations

### Undo Last Commit (keep changes)
```bash
git reset --soft HEAD~1
```

### Undo Last Commit (discard changes)
```bash
git reset --hard HEAD~1
```

### Amend Last Commit
```bash
git add -A && git commit --amend --no-edit
```

### Squash Commits
```bash
git rebase -i HEAD~3  # squash last 3 commits
```

### Stash Changes
```bash
git stash
# do other stuff
git stash pop
```

### Cherry-Pick
```bash
git cherry-pick abc1234
```

---

## Branch Hygiene

```bash
# Delete merged local branches
git branch --merged | grep -v main | xargs git branch -d

# Delete remote tracking branches that no longer exist
git fetch --prune
```

---

## Conflict Resolution

```bash
# During rebase/merge conflict
git status                    # See conflicted files
# Edit files to resolve
git add resolved-file.txt
git rebase --continue         # or git merge --continue
```

---

## Golden Rules

1. **Commit often** - Don't lose work
2. **Pull before push** - Avoid conflicts
3. **One thing per commit** - Easy to review/revert
4. **Descriptive messages** - Future you will thank you
5. **Don't force push shared branches** - Unless you know what you're doing



