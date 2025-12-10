# Active Context

> What is currently being worked on in this repository.  
> Update this file when starting or finishing work.

---

## 🚨 Before Ending Your Session

**Did you fix any bugs? Document them in `adr-0001-known-gotchas.md` NOW.**

This is the #1 most important thing to do before ending a session. Future AI agents (and humans) will thank you.

---

## Current Session

- **Current Task**: ALL phases (2-6) planning complete + compatibility bridge
- **Branch**: `experimental/brainz` (Phase 1) → `experimental/virtual-soulfind` (Phase 6)
- **Environment**: Local dev
- **Last Activity**: Added Phase 6X compatibility bridge design (T-850 to T-860)

---

## Recent Context

### Last Session Summary
- Completed Phase 1 (MusicBrainz/Chromaprint integration) T-300 through T-313
- Added album completion UI that surfaces tracked MB release targets and missing tracks
- Created initial architecture docs (multi-swarm-architecture.md, multi-swarm-roadmap.md, Soulbeet docs)
- Added 62 new tasks (T-400 through T-712) covering Phases 2–5
- **Completed comprehensive planning for ALL remaining phases (2-6)**:
  - Phase 2: 5 documents (~20,000 lines) - Canonical scoring, Library health, Swarm scheduling, Rescue mode
  - Phase 3: 1 document (4,100 lines) - Discovery, Reputation, Fairness
  - Phase 4: 1 document (3,200 lines) - Manifests, Session traces, Advanced features
  - Phase 5: 1 document (2,800 lines) - Soulbeet integration
  - Phase 6: 4 documents (11,200 lines) - Virtual Soulfind mesh, disaster mode, compatibility bridge
- **Total: 19 planning documents, ~50,000 lines of production-ready specifications**
- **New killer feature**: Compatibility bridge (T-850 to T-860) - lets legacy clients benefit from mesh

### Blocking Issues
- None currently

### Next Steps
1. **Phase 6X (T-850 to T-860) is OPTIONAL but RECOMMENDED** - compatibility bridge is the killer feature
2. Hand complete specification to Codex for implementation
3. Codex starts with Phase 2, Task T-400 (Canonical Scoring)
4. Follow implementation order in `docs/COMPLETE_PLANNING_INDEX.md`
5. All tasks (T-400 to T-860) are fully specified - no additional planning needed
6. Estimated implementation time: ~60 weeks for all phases (including bridge)

---

## Environment Notes

- **Backend Port**: 5030 (default)
- **Frontend Dev Port**: 3000 (CRA default)
- **.NET Version**: 8.0
- **Node Version**: Check `package.json` engines

---

## Quick Commands

```bash
# Start backend (watch mode)
./bin/watch

# Start frontend dev server
cd src/web && npm start

# Run all tests
dotnet test

# Build release
./bin/build
```

