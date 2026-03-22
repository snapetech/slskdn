# Active Context

> What is currently being worked on in this repository.  
> Update this file when starting or finishing work.

---

## ⚠️ WORK DIRECTORY (do not use /home/keith/Code/cursor)

**Project root: `/home/keith/Documents/code/slskdn`**

All `git`, `dotnet`, and file paths in this repo are under this directory. Do not use the `cursor` folder under `~/Code/` for slskdn work — it is a separate project.

---

## 🚨 Before Ending Your Session

**Did you fix any bugs? Document them in `adr-0001-known-gotchas.md` NOW.**

This is the #1 most important thing to do before ending a session. Future AI agents (and humans) will thank you.

---

## Current Session

- **Current Task**: Release-hardening follow-up after the broad runtime/read-side bughunt, focused on making release readiness explicit with a repeatable gate and checklist
- **Branch**: `release-main`
- **Environment**: Local dev
- **Last Activity**:
  - Added an explicit release-surface integration smoke gate:
    - new `packaging/scripts/run-release-integration-smoke.sh`
    - wired into `packaging/scripts/run-release-gate.sh`
    - covers `LoadTests`, `DisasterModeIntegrationTests`, `SoulbeetAdvancedModeTests`, `CanonicalSelectionTests`, and `LibraryHealthTests`
  - Added `docs/dev/release-checklist.md` so local release readiness, tag-triggered builds, and remaining limits are documented in one place
  - Updated `docs/dev/testing-policy.md` so the documented release gate matches the actual release gate
  - Validation is green:
    - `bash packaging/scripts/run-release-gate.sh` passed
    - `bash ./bin/lint` passed

---

## Recent Context

### Last Session Summary
- Completed Phase 1 (MusicBrainz/Chromaprint integration) T-300 through T-313
- Implemented Phase 2A tasks T-400 through T-403 (AudioVariant, canonical scoring, library health scaffolding)
- **Extended planning with critical additions**:
  - **Phase 2-Extended**: Codec-specific fingerprinting & quality heuristics (T-420 to T-430)
    - FLAC 42-byte streaminfo hash + PCM MD5
    - MP3 tag-stripped stream hash + encoder detection
    - Opus/AAC stream hashes + spectral analysis
    - Cross-codec deduplication via audio_sketch_hash
    - Heuristic versioning & recomputation system
  - **Phase 7**: Testing strategy with Soulfind & mesh simulator (T-900 to T-915)
    - L0/L1/L2/L3 test layers
    - Soulfind test harness (dev-only, never production)
    - Multi-client integration tests (Alice/Bob/Carol topology)
    - Mesh simulator for disaster mode testing
    - CI/CD integration with test categorization
- **Previously completed comprehensive planning for ALL phases (2-6)**:
  - Phase 2: 6 documents (~25,000 lines) - Canonical scoring, Library health, Swarm scheduling, Rescue mode, Codec-specific fingerprinting
  - Phase 3: 1 document (4,100 lines) - Discovery, Reputation, Fairness
  - Phase 4: 1 document (3,200 lines) - Manifests, Session traces, Advanced features
  - Phase 5: 1 document (2,800 lines) - Soulbeet integration
  - Phase 6: 4 documents (11,200 lines) - Virtual Soulfind mesh, disaster mode, compatibility bridge
  - Phase 7: 1 document (6,500 lines) - Testing strategy
- **Total: 21 planning documents, ~57,000 lines of production-ready specifications**
- **Total tasks: 127 (T-300 to T-915, plus misc)**

### Blocking Issues
- None currently

### Current focus (the rest)
- **40-fixes plan (PR-00–PR-14):** Done. slskd.Tests 46, slskd.Tests.Unit 2257 pass; Epic implemented. Deferred table: status only.
- **T-404+:** Done. t410-backfill-wire (RescueMode underperformance detector → RescueService); codec/fingerprint (T-420–T-430) done per dashboard.
- **slskd.Tests.Unit re-enablement:** ✅ **COMPLETE** (2026-01-27): All phases (0-5) done. 2430 tests passing, 0 skipped, 0 failed. No `Compile Remove` remaining. All test files enabled and passing per `docs/dev/slskd-tests-unit-completion-plan.md`.
- **New product work**: As prioritized.

**Research (9) implementation:** ✅ Complete. T-901–T-913 all done per `memory-bank/tasks.md`.

### Next Steps
1. Resume the broad runtime/read-side bughunt from the new green release gate.
2. Prioritize remaining places where public APIs may still report success while backing work is impossible, silently filtered, or split across normalized-vs-raw boundary assumptions.
3. Consider automating the remaining packaging-specific smokes called out in `memory-bank/tasks.md`, especially the NixOS VM smoke test and subpath-hosted web smoke.

4. **Recent completions** (2026-01-27):
   - ✅ Backfill for shared collections (API + UI, supports HTTP and Soulseek)
   - ✅ Persistent tabbed interface for Chat (Rooms already had tabs)
   - ✅ E2E test completion (policy, streaming, library, search)
   - ✅ Code cleanup: TODO comments updated to reference triage document
   - ✅ Soulfind integration: CI and local build workflows integrated
   - ✅ Soulbeet compatibility tests: Fixed 2 failing tests (JSON property names, Directories config)
   - ✅ Phase 2 Multi-Swarm: Implemented Phase 2B deep library health scanning (T-403), verified Phase 2A/2C/2D complete
   - ✅ Phase 3 Multi-Swarm: Verified all 11 tasks (T-500 to T-510) complete
   - ✅ Phase 4A-4C Multi-Swarm: Verified 9 of 12 tasks (T-600 to T-608) complete
   - ✅ Phase 4D Multi-Swarm: **COMPLETED** (T-609 to T-611) - Full playback-aware chunk priority integration
   - ✅ Phase 5 Multi-Swarm: Verified all 13 tasks (T-700 to T-712) complete

**Multi-Swarm Status**: 62 of 62 tasks complete (100%). All Phases 1-5 fully implemented and verified.

5. ~~**High Priority Tasks Available** (obsolete):~~
   - **Packaging**: T-010 to T-013 (NAS/docker packaging - 4 tasks)
   - **Medium Priority**: T-003 (Download Queue Position Polling), T-004 (Visual Group Indicators)
   - ~~T-404+~~ (done)

5. ~~**Implementation Timeline**~~ (archived; Phase 14 and T-404+ done.)

6. ~~**Branch Strategy**: Phase 14 `experimental/pod-vpn`~~ (Phase 14 done.)

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
