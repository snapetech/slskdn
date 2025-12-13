# ADR-0005: Git Workflow & Branch Management

> **Status**: Active  
> **Date**: 2025-12-08  
> **Purpose**: Standard git operations for AI and human contributors

---

## Branch Structure

```
master              ← stable releases only
  └── dev           ← integration branch for features
       └── feature/xyz    ← individual features
       └── bugfix/xyz     ← bug fixes
       └── experimental/* ← experimental features (may diverge significantly)
```

### Branch Naming

| Type | Pattern | Example |
|------|---------|---------|
| Feature | `feature/short-description` | `feature/wishlist-ui` |
| Bugfix | `bugfix/issue-description` | `bugfix/transfer-crash` |
| Experimental | `experimental/feature-name` | `experimental/multi-source-swarm` |
| Chore | `chore/description` | `chore/slop-reduction` |
| Release | `release/vX.Y.Z` | `release/v0.22.0` |

---

## Daily Workflow

### Starting Work

```bash
# 1. Update your local branches
git fetch origin

# 2. Check which branch you're on
git branch --show-current

# 3. Switch to the appropriate branch
git checkout dev
git pull origin dev

# 4. Create feature branch (if new work)
git checkout -b feature/my-feature
```

### During Work

```bash
# Stage specific files (preferred)
git add src/slskd/MyFeature/MyService.cs

# Stage all changes in a directory
git add src/slskd/MyFeature/

# Check what's staged
git status

# Commit with descriptive message
git commit -m "feat: Add wishlist persistence"
```

### Commit Message Format

```
type: short description

[optional body with more details]

[optional footer with references]
```

**Types**:
- `feat:` - New feature
- `fix:` - Bug fix
- `chore:` - Maintenance, refactoring, docs
- `test:` - Adding/updating tests
- `docs:` - Documentation only
- `style:` - Formatting, no code change
- `refactor:` - Code change that neither fixes nor adds

**Examples**:
```
feat: Add wishlist service with SQLite persistence

fix: Prevent crash when transfer response is undefined

chore: Update memory bank with code patterns

test: Add unit tests for SourceRankingService
```

### Finishing Work

```bash
# 1. Ensure tests pass
dotnet test

# 2. Ensure lint passes
./bin/lint

# 3. Push your branch
git push origin feature/my-feature

# 4. Create PR (via GitHub) or merge locally
```

---

## Merging

### Merge Feature into Dev

```bash
git checkout dev
git pull origin dev
git merge feature/my-feature
git push origin dev
```

### Merge Dev into Master (Release)

```bash
git checkout master
git pull origin master
git merge dev
git push origin master
git tag -a v0.X.Y -m "Release v0.X.Y"
git push origin v0.X.Y
```

### Handling Merge Conflicts

```bash
# 1. Start the merge
git merge feature/conflicting-branch

# 2. See which files conflict
git status

# 3. Open conflicting files and resolve
# Look for <<<<<<< HEAD ... ======= ... >>>>>>> markers

# 4. After resolving, stage the files
git add resolved-file.cs

# 5. Complete the merge
git commit
```

---

## Propagating Changes Across Branches

When you need the same change in multiple branches (like memory bank updates):

### Method 1: Cherry-pick (preferred for small changes)

```bash
# Get the commit hash from source branch
git log --oneline -5

# Apply to each target branch
git checkout dev
git cherry-pick abc1234
git push origin dev

git checkout experimental/multi-source-swarm
git cherry-pick abc1234
git push origin experimental/multi-source-swarm
```

### Method 2: Checkout specific files

```bash
# Copy files from one branch to another
git checkout target-branch
git checkout source-branch -- path/to/file.md path/to/other/
git add .
git commit -m "chore: Sync files from source-branch"
```

### Method 3: Loop through branches

```bash
# For identical changes across branches
for branch in dev experimental/multi-source-swarm; do
  git checkout "$branch"
  git checkout master -- memory-bank/
  git add memory-bank/
  git commit -m "chore: Sync memory bank from master"
done
git checkout master
```

---

## Stashing

When you need to switch branches but have uncommitted work:

```bash
# Save current work
git stash push -m "WIP: working on feature X"

# Switch branches, do other work
git checkout other-branch
# ...work...
git checkout original-branch

# Restore your work
git stash pop

# Or list stashes and apply specific one
git stash list
git stash apply stash@{1}
```

---

## Undoing Things

### Undo last commit (keep changes)

```bash
git reset --soft HEAD~1
```

### Undo last commit (discard changes)

```bash
git reset --hard HEAD~1
```

### Discard changes to a file

```bash
git checkout -- path/to/file.cs
```

### Undo a pushed commit (creates new commit)

```bash
git revert abc1234
git push origin branch-name
```

---

## Rebasing (Advanced)

### Rebase feature onto updated dev

```bash
git checkout feature/my-feature
git fetch origin
git rebase origin/dev

# If conflicts, resolve then:
git add resolved-file.cs
git rebase --continue

# Force push (only for your own branches!)
git push --force-with-lease origin feature/my-feature
```

### Interactive rebase to clean up commits

```bash
# Squash last 3 commits
git rebase -i HEAD~3

# In editor, change 'pick' to 'squash' for commits to combine
# Save and edit the combined commit message
```

---

## Viewing History

```bash
# Simple log
git log --oneline -10

# With graph
git log --oneline --graph --all -20

# See what changed in a commit
git show abc1234

# See what changed in a file
git log -p path/to/file.cs

# Find who changed a line
git blame path/to/file.cs
```

---

## Remote Operations

```bash
# Push all branches
git push origin master dev experimental/multi-source-swarm

# Push with upstream tracking
git push -u origin feature/new-branch

# Delete remote branch
git push origin --delete feature/old-branch

# Prune deleted remote branches locally
git fetch --prune
```

---

## Common Scenarios

### "I committed to the wrong branch"

```bash
# Save the commit hash
git log --oneline -1  # note the hash

# Undo on wrong branch
git reset --hard HEAD~1

# Apply to correct branch
git checkout correct-branch
git cherry-pick abc1234
```

### "I need to update my feature branch with latest dev"

```bash
git checkout feature/my-feature
git fetch origin
git merge origin/dev
# or
git rebase origin/dev
```

### "I pushed sensitive data"

```bash
# Remove file from history (DANGEROUS - rewrites history)
git filter-branch --force --index-filter \
  "git rm --cached --ignore-unmatch path/to/sensitive-file" \
  --prune-empty --tag-name-filter cat -- --all

# Force push all branches
git push origin --force --all
git push origin --force --tags

# Better: use BFG Repo-Cleaner
# https://rtyley.github.io/bfg-repo-cleaner/
```

---

## AI Agent Instructions

When an AI agent needs to commit:

1. **Always check current branch first**: `git branch --show-current`
2. **Never force push to shared branches** (master, dev)
3. **Use descriptive commit messages** with type prefix
4. **Run tests before committing**: `dotnet test`
5. **Propagate shared changes** (like memory bank) to all relevant branches
6. **Don't commit generated files** (.dll, node_modules, etc.)

### Files to Never Commit

```gitignore
# Already in .gitignore, but be aware:
bin/
obj/
node_modules/
*.dll
*.pdb
.vs/
.idea/
*.user
*.suo
```

---

*Last updated: 2025-12-08*



















