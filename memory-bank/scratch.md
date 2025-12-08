# Scratch Pad

> Free-form notes, ideas, and temporary thinking space.  
> This file can be truncated when it gets too long.  
> AI agents can use this for "thinking on paper" without worrying about cleanliness.

---

## Current Thoughts

(Empty - ready for use)

---

## Quick Reference: Upstream Bug Fixes Applied

These bugs were fixed in slskdN and should NOT be reverted:

| Bug | File | Fix |
|-----|------|-----|
| async-void crash | `RoomService.cs` | Wrap in try-catch |
| undefined return | `searches.js` | Return `[]` not `undefined` |
| undefined return | `transfers.js` | Return `[]` not `undefined` |
| no pagination | `SearchService.cs` | Add limit/offset params |
| flaky test | `UploadGovernorTests.cs` | Use InlineAutoData |

---

## Ideas Parking Lot

### Feature Ideas
- Persistent Room/Chat tabs (like Browse tabs)
- Scheduled rate limits (day/night)
- Traffic ticker (real-time activity feed)

### Technical Debt
- Frontend migration from CRA to Vite (see `docs/FRONTEND_MIGRATION_PLAN.md` in cleanup branch)
- React 16 → React 18 upgrade
- react-router-dom v5 → v6

### Questions to Research
- Best approach for persistent chat tabs (localStorage vs IndexedDB)
- Rate limit scheduler UI design

---

## Common Commands

```bash
# Run backend
./bin/watch

# Run frontend
cd src/web && npm start

# Run tests
dotnet test

# Lint
./bin/lint

# Build release
./bin/build
```

---

## Temporary Notes

(Use this section for session-specific notes that don't need to persist)

