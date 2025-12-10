# ADR-0002: Anti-Slop Rules

> **Status**: Active  
> **Date**: [Date Created]  
> **Purpose**: Prevent AI-generated code smell

These rules prevent common AI code generation problems. Read before writing code.

---

## Core Principle: Less is More

AI models tend to over-engineer. Fight this tendency.

---

## Rule 1: Don't Invent Abstractions

**Problem**: AI creates interfaces, factories, and wrappers that don't exist in the codebase.

**Don't**:
```python
class UserServiceFactory:
    def create(self, type):
        if type == "admin":
            return AdminUserService()
        return RegularUserService()
```

**Do**: Just use the service directly. If the codebase doesn't have factories, don't add them.

---

## Rule 2: Don't Add Defensive Null Checks

**Problem**: AI adds null checks everywhere like enterprise Java.

**Don't**:
```python
def process(user):
    if user is None:
        raise ValueError("user cannot be None")
    if user.name is None:
        raise ValueError("user.name cannot be None")
    # finally does something
```

**Do**: Trust internal code. Only validate at API boundaries.

---

## Rule 3: Don't Wrap Everything in Try-Catch

**Problem**: AI swallows exceptions, hiding real errors.

**Don't**:
```python
try:
    result = do_something()
except Exception:
    return None  # 💀 Hides the real error
```

**Do**: Let exceptions propagate unless you have specific recovery logic.

---

## Rule 4: Don't Add Logging Spam

**Problem**: AI adds entry/exit logging to every function.

**Don't**:
```python
def process(data):
    logger.info("Entering process")
    logger.debug(f"Processing data: {data}")
    result = do_work(data)
    logger.info("Exiting process")
    return result
```

**Do**: Log meaningful events only (errors, important state changes).

---

## Rule 5: Don't Create DTOs for Everything

**Problem**: AI creates data transfer objects for simple data.

**Don't**: Create `UserResponseDTO`, `UserRequestDTO`, `UserUpdateDTO` for a simple user object.

**Do**: Use the domain object directly unless there's a specific serialization need.

---

## Rule 6: Don't Add Configuration for Everything

**Problem**: AI makes everything configurable "for flexibility."

**Don't**:
```python
MAX_RETRIES = config.get("max_retries", 3)
RETRY_DELAY = config.get("retry_delay", 1.0)
RETRY_BACKOFF = config.get("retry_backoff", 2.0)
```

**Do**: Use sensible defaults. Only add config for things that actually need to change.

---

## Rule 7: Don't Create Enums for Two Values

**Problem**: AI creates enums for boolean-like choices.

**Don't**:
```python
class UserStatus(Enum):
    ACTIVE = "active"
    INACTIVE = "inactive"
```

**Do**: Just use a boolean `is_active`.

---

## Rule 8: Don't Add Async When Not Needed

**Problem**: AI makes everything async "for performance."

**Don't**: Make a function async if it doesn't do I/O or call other async functions.

**Do**: Only use async for actual I/O operations.

---

## Rule 9: Match Existing Patterns

**Problem**: AI introduces new patterns instead of using existing ones.

**Do**:
1. Grep for similar functionality
2. Copy the existing pattern exactly
3. Only deviate if there's a specific reason

---

## Rule 10: One Thing Per Commit

**Problem**: AI batches unrelated changes.

**Do**:
- One bug fix = one commit
- One feature = one commit (or a focused series)
- Refactoring separate from behavior changes

---

## CLI Efficiency Rules

### Rule 11: Chain Commands

**Don't**:
```bash
npm install
npm run build
npm test
```

**Do**:
```bash
npm install && npm run build && npm test
```

### Rule 12: Use Pipes

**Don't**:
```bash
grep "error" log.txt > errors.txt
cat errors.txt | head -10
```

**Do**:
```bash
grep "error" log.txt | head -10
```

### Rule 13: Use Subshells

**Don't**:
```bash
cd subdir
npm install
cd ..
```

**Do**:
```bash
(cd subdir && npm install)
```

### Rule 14: Combine Similar Commands

**Don't**:
```bash
grep "TODO" file1.py
grep "TODO" file2.py
grep "TODO" file3.py
```

**Do**:
```bash
grep "TODO" file1.py file2.py file3.py
# or
grep -rn "TODO" src/
```

---

## Quick Self-Check

Before submitting code:

- [ ] Did I grep for existing patterns first?
- [ ] Would a senior dev ask "why did you add this?"
- [ ] Am I adding code "just in case"?
- [ ] Is there a simpler way?
- [ ] Does this match the codebase style?

If unsure, **do less**.





