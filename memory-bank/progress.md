# Progress Log

> Chronological log of development activity.  
> AI agents should append here after completing significant work.

---

## 2025-12-08

- 00:00: Initialized memory-bank structure for AI-assisted development
- 00:00: Created `projectbrief.md`, `tasks.md`, `activeContext.md`, `progress.md`, `scratch.md`
- 00:00: Created `.cursor/rules/` with project-specific AI instructions
- 00:00: Created `AGENTS.md` with development workflow guidelines

---

## Historical Releases (from DEVELOPMENT_HISTORY.md)

| Release | Date | Highlights |
|---------|------|------------|
| .1 | Dec 2 | Auto-replace stuck downloads |
| .2 | Dec 2 | Wishlist, Multiple destinations |
| .3 | Dec 2 | Clear all searches |
| .4 | Dec 3 | Smart ranking, History badges |
| .5 | Dec 3 | Search filters, Block users |
| .6 | Dec 3 | User notes, AUR binary |
| .7 | Dec 3 | Delete files, AUR source |
| .8 | Dec 3 | Push notifications |
| .9 | Dec 4 | Bug fixes |
| .10 | Dec 4 | Tabbed browse |
| .11 | Dec 4 | CI/CD automation |
| .12 | Dec 4 | Package fixes |
| .13 | Dec 5 | COPR, PPA, openSUSE |
| .14 | Dec 5 | Self-hosted runners, LRU cache |
| .15 | Dec 6 | Room/Chat UI, Bug fixes |
| .16 | Dec 6 | StyleCop cleanup |
| .17 | Dec 6 | Search pagination, Flaky test fix |
| .18 | Dec 7 | Upstream merge, Doc cleanup |

---

## Upstream Bug Fixes Applied

| Bug | Status | Notes |
|-----|--------|-------|
| Async-void in RoomService | ✅ Fixed | Prevents crash on login errors |
| Undefined returns in searches.js | ✅ Fixed | Prevents frontend errors |
| Undefined returns in transfers.js | ✅ Fixed | Prevents frontend errors |
| Flaky UploadGovernorTests | ✅ Fixed | Integer division edge case |
| Search API lacks pagination | ✅ Fixed | Prevents browser hang |
| Duplicate message DB error | ✅ Fixed | Handle replayed messages |
| Version check crash | ✅ Fixed | Suppress noisy warning |
| ObjectDisposedException on shutdown | ✅ Fixed | Graceful shutdown |

