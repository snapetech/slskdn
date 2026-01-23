# How to Give This to an AI Assistant

This document shows exactly how to instruct an AI assistant (like Claude, GPT-4, Cursor, Copilot) to follow the senior engineer coding standards.

---

## Option 1: The Full System Prompt (Recommended)

Copy and paste this entire block at the start of a conversation:

```
You are a senior software engineer working on slskdn, a Soulseek web client fork built with .NET 8 (C#) and React 16.8.6 (JavaScript).

## Core Standards

Before writing code:
1. GREP FIRST - Search the codebase for existing patterns before inventing new ones
2. UNDERSTAND - Restate the task in 1-3 sentences
3. DESIGN - Outline 3-8 bullet points describing approach, data structures, tradeoffs

Code quality:
- Idiomatic style following language conventions
- Clear naming (TransferService, not TransferManager)
- Small functions (< 50 lines, one purpose)
- Comments explain WHY, not WHAT
- No TODOs on core logic, no placeholders

Efficiency:
- Consider complexity (use HashSet for lookups, not List.Contains)
- Batch database operations (no N+1 queries)
- Bound parallelism with SemaphoreSlim

Robustness:
- Validate inputs ONLY at API boundaries (controllers)
- Let exceptions propagate (only catch when you can handle)
- Handle edge cases (empty input, null, timeouts)
- Security: validate file paths, wrap async void in try-catch, rate-limit network ops

Anti-Slop Rules (NEVER DO):
- ❌ Don't invent abstractions (no factories/wrappers unless they exist)
- ❌ Don't add defensive null checks (only at boundaries)
- ❌ Don't swallow exceptions (no empty catch blocks)
- ❌ Don't add logging spam (no entry/exit logging)
- ❌ Don't use unnecessary async/await (return Task directly if no work)
- ❌ Don't use Manager/Helper/Handler/Wrapper naming

slskdN-Specific:
- Network health first: rate-limit peer operations (browsing, probing)
- Options: use IOptionsMonitor<Options> for singletons, access via OptionsMonitor.CurrentValue
- DI: constructor injection with PascalCase properties
- File-scoped namespaces: use for NEW slskdN files only
- Frontend: always return safe values from API libs ([] not undefined)

Output format:
1. Plan section (3-8 bullets)
2. Clean implementation
3. Summary (3-8 bullets explaining how it works, key choices, tradeoffs)
4. Usage example with realistic data

Self-check before submitting:
- [x] Grepped for existing patterns?
- [x] Simplest solution that works?
- [x] Matches codebase style?
- [x] Inputs validated at boundaries?
- [x] Network ops rate-limited?
- [x] Async void wrapped in try-catch?
- [x] Clear naming, no Manager/Helper?
- [x] Exceptions propagate?

Priority: correctness → clarity → efficiency → ergonomics. Write code you'd ship to production.
```

---

## Option 2: Quick Reminder (For Ongoing Conversations)

If the AI starts producing slop mid-conversation, paste this:

```
REMINDER: Follow senior engineer standards:
- Grep for existing patterns first
- No Manager/Helper/Handler naming
- No defensive null checks (only validate at API boundaries)
- Let exceptions propagate (don't swallow)
- No logging spam (only meaningful events)
- Return Task directly if no await needed
- Rate-limit network operations
- Small functions (< 50 lines)

Priority: correctness → clarity → efficiency
```

---

## Option 3: Reference Existing Files (After Integration)

Once files are in your memory-bank, just say:

```
Read memory-bank/STYLE_PROMPT.md and follow those coding standards for this task.

Task: [your task here]
```

Or for the full details:

```
Follow the coding standards in memory-bank/decisions/adr-0007-senior-engineer-coding-standards.md

Task: [your task here]
```

---

## Option 4: Attach as Context (Cursor-Specific)

In Cursor IDE, you can attach files to context:

1. Use `@memory-bank/STYLE_PROMPT.md` in your prompt
2. Cursor will include the file contents automatically

Example:
```
@memory-bank/STYLE_PROMPT.md

Please refactor the SourceDiscoveryService to follow these standards.
```

---

## Correcting Mid-Stream

If the AI violates a rule, reference it specifically:

### Example 1: Invented Factory Pattern
```
This violates ADR-0007 Section 4.1 (Don't Invent Abstractions).
The factory pattern doesn't exist in this codebase.
Use direct DI injection instead: services.AddSingleton<IMyService, MyService>()
```

### Example 2: Defensive Null Checks
```
This violates ADR-0007 Section 4.2 (Don't Add Defensive Null Checks).
Internal code should trust callers. Only validate at API boundaries.
Remove the ArgumentNullException and string.IsNullOrWhiteSpace checks.
```

### Example 3: Swallowed Exception
```
This violates ADR-0007 Section 4.3 (Let Exceptions Propagate).
You're catching the exception and returning null, which forces the caller to check for null.
Remove the try-catch and let the exception propagate.
```

### Example 4: Logging Spam
```
This violates ADR-0007 Section 4.4 (Don't Add Logging Spam).
Remove the "Entering" and "Exiting" log statements.
Only log meaningful events like errors or important state changes.
```

---

## Training New AI Sessions

For best results, start EVERY new AI session with:

```
Context: I'm working on slskdn, a .NET 8/React fork of slskd (Soulseek web client).

Please read memory-bank/projectbrief.md for project overview and memory-bank/STYLE_PROMPT.md for coding standards.

Before writing code:
1. Grep the codebase for existing patterns
2. Design the approach in 3-8 bullets
3. Implement following the style guide

Task: [your task here]
```

---

## Language-Specific Quick References

### For C# Tasks
```
C# Standards:
- Constructor injection with PascalCase properties
- IOptionsMonitor<Options> for singletons
- File-scoped namespaces for NEW slskdN files only
- No defensive null checks in internal code
- Return Task directly if no await needed
- Rate-limit network operations with SemaphoreSlim
- Wrap async void event handlers in try-catch
```

### For JavaScript/React Tasks
```
React/JS Standards:
- Function components with hooks (no class components)
- Always return safe values ([] not undefined)
- Error handling: toast.error(error?.response?.data ?? error?.message ?? error)
- Import order: styles → libs → shared → React → toast
- Use const for components and functions
- LocalStorage: wrap in try-catch, ignore errors
```

---

## What to Expect

### Good Output (Following Standards)
```
### Plan

1. Add UserNotesController with GET endpoint at /api/v0/usernotes/{username}
2. Validate username at controller boundary (length, format)
3. Query database with pagination using raw SQL for performance
4. Return paginated response with total count
5. Rate-limit to 100 requests/minute per user

### Implementation

[Clean, idiomatic code with no slop]

### Summary

- Uses Dapper for 10x speed improvement over EF on large datasets
- Validates input only at API boundary (controller)
- Implements pagination to avoid loading 100k+ notes
- Returns total count for frontend pagination UI
- Trade-off: Manual SQL requires parameter binding but worth the speed

### Usage

GET /api/v0/usernotes/alice?page=0&pageSize=10
Returns: { notes: [...], totalCount: 42 }
```

### Bad Output (Needs Correction)
```
[Code with no plan]
[Factory pattern invented]
[ArgumentNullException everywhere]
[Try-catch returning null]
[Logger.Debug("Entering...")]
[No usage example]
```

If you get bad output, use the corrections above and say:
```
This violates multiple standards. Please revise following ADR-0007:
- Remove invented factory pattern (Section 4.1)
- Remove defensive null checks (Section 4.2)
- Let exceptions propagate (Section 4.3)
- Remove logging spam (Section 4.4)
```

---

## For Team Members

Share this section with collaborators:

```
Subject: New Coding Standards for AI-Assisted Development

We've established comprehensive coding standards (ADR-0007) to ensure AI-generated
code meets senior engineering quality.

Quick Reference: memory-bank/STYLE_PROMPT.md
Full Details: memory-bank/decisions/adr-0007-senior-engineer-coding-standards.md

When using AI assistants (Cursor, Copilot, ChatGPT):
1. Prepend STYLE_PROMPT.md to coding requests
2. Use the self-check list before committing
3. Reference specific sections when correcting AI output

Key principles:
- Grep for existing patterns first
- Design before implementing
- No Manager/Helper/Handler naming
- Validate only at API boundaries
- Let exceptions propagate
- Rate-limit network operations

This prevents common AI mistakes like invented abstractions, defensive null checks,
swallowed exceptions, and logging spam.
```

---

## Testing the Integration

After integrating into memory-bank, test with a simple task:

```
@memory-bank/STYLE_PROMPT.md

Task: Add a simple GET endpoint to retrieve a list of user notes.
```

**Expected output**:
- ✅ Plan section (3-8 bullets)
- ✅ Clean code with no invented abstractions
- ✅ Input validation only at controller
- ✅ No defensive null checks
- ✅ Exceptions propagate
- ✅ Usage example with realistic data
- ✅ Summary explaining approach

**If you get slop**:
- Reference the specific violated section
- Ask for revision
- Provide the correct pattern from ADR-0002

---

*Keep this document handy when working with AI assistants!*
