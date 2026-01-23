# Integration Guide: Senior Engineer Coding Standards

This document explains how to integrate the new ADR-0007 (Senior Engineer Coding Standards) into the slskdn memory-bank system and propagate it across all active branches.

---

## Files Created

1. **`adr-0007-senior-engineer-coding-standards.md`** (7.5KB)
   - Comprehensive coding standards combining senior engineering practices with slskdn conventions
   - Organized into 14 major sections covering design, code quality, efficiency, robustness, security
   - Includes slskdN-specific rules (network health, DI patterns, options access)
   - Cross-references existing ADRs (0002, 0003, 0006)

2. **`STYLE_PROMPT.md`** (2KB)
   - Compact version suitable for prepending to AI coding requests
   - Checklist format for quick reference
   - Contains all essential rules in condensed form

---

## Integration Steps

### Step 1: Add to Main Branch Memory-Bank

```bash
# On main branch
cd /home/keith/Documents/Code/slskdn

# Copy ADR to memory-bank
cp /tmp/tmp.f8mfTQSRH7/adr-0007-senior-engineer-coding-standards.md \
   memory-bank/decisions/

# Copy style prompt to memory-bank root
cp /tmp/tmp.f8mfTQSRH7/STYLE_PROMPT.md \
   memory-bank/

# Commit
git add memory-bank/decisions/adr-0007-senior-engineer-coding-standards.md \
        memory-bank/STYLE_PROMPT.md

git commit -m "docs: add ADR-0007 senior engineer coding standards

- Comprehensive coding standards for AI-assisted development
- Integrates senior engineering practices with slskdn conventions
- Adds compact STYLE_PROMPT.md for quick reference
- Cross-references ADR-0002, ADR-0003, ADR-0006"

git push origin main
```

### Step 2: Update projectbrief.md

Add reference to new ADR in the "Important Docs" section:

```markdown
## Important Docs

- `FORK_VISION.md` - Full feature roadmap and philosophy
- `DEVELOPMENT_HISTORY.md` - Release timeline and feature status
- `TODO.md` - Current pending work
- `CONTRIBUTING.md` - Contribution workflow
- `docs/` - User-facing documentation
- `memory-bank/decisions/adr-0007-senior-engineer-coding-standards.md` - Coding standards for all contributions
```

### Step 3: Propagate to Active Branches

Identify active branches (experimental branches with ongoing work):

```bash
# List branches
git branch -a | grep -E "(experimental|feature|dev)"

# For each active branch:
git checkout experimental/multi-source-swarm
git cherry-pick <commit-hash-from-step-1>
git push origin experimental/multi-source-swarm

# Repeat for other branches
```

**Alternative approach (if cherry-pick has conflicts)**:

```bash
# On each branch
git checkout experimental/multi-source-swarm
git merge main --no-ff -m "Merge ADR-0007 from main"
git push origin experimental/multi-source-swarm
```

### Step 4: Update activeContext.md

If you maintain an `activeContext.md` file, add a reference:

```markdown
## Active Coding Standards

When writing code, follow **ADR-0007: Senior Engineer Coding Standards** (`memory-bank/decisions/adr-0007-senior-engineer-coding-standards.md`).

Quick reference: `memory-bank/STYLE_PROMPT.md`

Key principles:
1. Grep first - search for existing patterns
2. Design first - outline approach before coding
3. Validate at boundaries only
4. Rate-limit network operations
5. Let exceptions propagate
```

---

## Using the Style Prompt

### For AI Assistants (Cursor, Copilot, etc.)

When starting a coding session, prepend the style prompt:

```
[Paste contents of STYLE_PROMPT.md]

Task: Add a new API endpoint for retrieving user notes filtered by username with pagination support.
```

### For Code Reviews

Use the ADR-0007 checklist as a review guide:

```markdown
## Code Review Checklist (from ADR-0007)

- [x] Grepped for existing patterns?
- [x] Simplest solution that works?
- [x] Matches codebase style?
- [x] Inputs validated at boundaries?
- [x] Network operations rate-limited?
- [x] Async void wrapped in try-catch?
- [x] Clear naming, no Manager/Helper?
- [x] Exceptions propagate instead of swallowed?
- [x] Functions < 50 lines?
- [x] No logging spam?
```

---

## Branch-Specific Considerations

### Main Branch
- Apply full ADR-0007 standards
- All PRs must pass checklist
- Update memory-bank as patterns evolve

### Experimental Branches
- Apply full standards for new code
- Refactoring existing code: follow ADR-0006 (Slop Reduction Guide)
- Focus on security and network health rules first

### Stable Branches (v0.24.x)
- Only apply standards to bug fixes
- Avoid unnecessary refactoring
- Maintain compatibility

---

## Enforcement

### Pre-Commit Hook (Optional)

Create `.githooks/pre-commit.d/check-standards.sh`:

```bash
#!/bin/bash

# Check for common anti-patterns
CHANGED_FILES=$(git diff --cached --name-only --diff-filter=ACM | grep -E '\.(cs|jsx?)$')

if [ -n "$CHANGED_FILES" ]; then
    echo "Checking coding standards..."
    
    # Check for Manager/Helper/Handler in class names
    if echo "$CHANGED_FILES" | xargs grep -l "class.*Manager\|class.*Helper\|class.*Handler" >/dev/null 2>&1; then
        echo "❌ Found Manager/Helper/Handler class names (see ADR-0007)"
        echo "   Rename to describe purpose (e.g., TransferService, DownloadQueue)"
        exit 1
    fi
    
    # Check for entry/exit logging
    if echo "$CHANGED_FILES" | xargs grep -l "Entering\|Exiting" >/dev/null 2>&1; then
        echo "❌ Found entry/exit logging (see ADR-0007 Rule 4.4)"
        exit 1
    fi
    
    echo "✅ Coding standards check passed"
fi
```

### CI/CD Integration

Add to `.github/workflows/code-quality.yml`:

```yaml
- name: Check Coding Standards
  run: |
    # Check for anti-patterns
    ./bin/lint-standards
    
    # Run existing linters
    ./bin/lint
```

---

## Updating the Standards

### When to Update ADR-0007

- New patterns emerge in codebase
- New anti-slop patterns discovered
- Framework/library updates change best practices
- Team consensus on new conventions

### How to Update

1. Propose change in GitHub issue with rationale
2. Update ADR-0007 with new section or modification
3. Increment "Last updated" date
4. Add note about what changed at top of file
5. Propagate to active branches
6. Update STYLE_PROMPT.md if relevant

**Example update note**:

```markdown
> **Change History**:
> - 2025-12-10: Initial version
> - 2025-12-15: Added section on GraphQL patterns (Section 15)
> - 2025-12-20: Updated async/await guidance based on .NET 9 improvements
```

---

## Training AI Assistants

### Initial Context

When starting a new AI session, provide:

```
Project: slskdn (Soulseek web client fork)
Tech: .NET 8 (C#), React 16.8.6 (JavaScript), SQLite

Read these first:
1. memory-bank/projectbrief.md - Project overview
2. memory-bank/STYLE_PROMPT.md - Coding standards (quick reference)
3. memory-bank/decisions/adr-0002-code-patterns.md - Existing patterns

Before writing code:
1. Grep codebase for existing patterns
2. Ask clarifying questions if ambiguous
3. Outline approach in 3-8 bullets
4. Write code following ADR-0007 standards
```

### Ongoing Reminders

If AI produces slop, reference specific section:

```
This violates ADR-0007 Section 4.2 (Don't Add Defensive Null Checks).
Internal code should trust callers. Only validate at API boundaries.

Please refactor to remove the null checks.
```

---

## Related Documentation

- **ADR-0002**: Code Patterns & Anti-Slop Guide (existing patterns to follow)
- **ADR-0003**: Anti-Slop Rules (30+ specific patterns to avoid)
- **ADR-0006**: Slop Reduction Refactor Guide (how to clean up existing code)
- **CONTRIBUTING.md**: Contribution workflow and PR checklist

---

## Quick Start Checklist

For integrating ADR-0007 today:

- [x] Copy `adr-0007-senior-engineer-coding-standards.md` to `memory-bank/decisions/`
- [x] Copy `STYLE_PROMPT.md` to `memory-bank/`
- [x] Update `projectbrief.md` with ADR-0007 reference
- [x] Commit and push to main
- [x] Cherry-pick or merge to `experimental/multi-source-swarm`
- [x] Update any active feature branches
- [x] Add ADR-0007 checklist to PR template
- [x] Share STYLE_PROMPT.md with team/collaborators
- [x] Test with AI assistant on next coding task

---

*Created: 2025-12-10*
