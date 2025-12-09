# ADR-0001: Known Gotchas & Anti-Patterns

> **Status**: Active (Living Document)  
> **Date**: [Date Created]  
> **Purpose**: Prevent repeating the same mistakes

---

## 🚨 FOR AI AGENTS: ADD TO THIS FILE IMMEDIATELY

**When you fix a bug, document it here BEFORE doing anything else.**

This is not optional. This is the highest priority action after fixing a bug.

### When to add an entry:
- You fixed a bug that you or another AI introduced
- You fixed the same type of bug more than once
- You discovered a non-obvious pattern that causes errors
- You spent >5 minutes debugging something that should have been documented

### Entry template:
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

### After adding:
1. Commit immediately: `git commit -m "docs: Add gotcha for [description]"`
2. Then continue with other work

**If you skip this step, the same bug WILL happen again. Document it NOW.**

---

## 🚨 Critical Bugs

<!-- Add critical bugs here -->

### 1. [Example] Return Empty Array, Not Undefined

**The Bug**: Functions that return `undefined` on error cause downstream crashes when callers try to iterate.

**Files Affected**:
- API/data fetching functions

**Wrong**:
```javascript
if (error) {
  console.error(error);
  return undefined;  // 💀 Caller does .map() and crashes
}
```

**Correct**:
```javascript
if (error) {
  console.error(error);
  return [];  // ✅ Safe to iterate
}
```

**Why This Keeps Happening**: Models see `undefined` as a signal value, forgetting callers will iterate.

---

## ⚠️ Common Mistakes

<!-- Add common mistakes here -->

---

## 🔄 Fix/Unfix Cycles

<!-- Add patterns that cause repeated fixes here -->

---

## 📦 Build/CI Gotchas

<!-- Add build and CI issues here -->

---

## Notes

- Keep entries concise but complete
- Include actual code examples
- Update when patterns change
- Delete entries that are no longer relevant
