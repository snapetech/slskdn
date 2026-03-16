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

- **Current Task**: SongID native implementation toward ytdlpchop parity
- **Branch**: `dev/40-fixes`
- **Environment**: Local dev
- **Last Activity**:
  - Inspected current MusicBrainz lookup/UI, MetadataFacade, AcoustID/Chromaprint, release-graph/discography services, search stack, and multi-source ranking/download flow
  - Inspected `../ytdlpchop` feature surface and mapped its identification suite onto `slskdn` extension points
  - Wrote `docs/dev/SONGID_INTEGRATION_MAP.md`
  - Implemented the first `SongID` backend + UI slice, then extended it with ranked acquisition options, direct album job handoff, a fixed MB release job path, a first native `chop`-style evidence pipeline, a heavier engine pass with persistent run artifacts, Demucs stems, Panako, Audfprint, provenance, AI-audio heuristics, durable SQLite-backed run storage, SignalR-backed live progress updates, corpus-driven reranking, stage/percentage progress payloads, native canonical-quality boosts from slskdn audio stats, and initial SQLite/scoring SongID tests
  - Re-read `../ytdlpchopid` after the rename / feature expansion and refreshed the SongID parity map and task backlog around the newer split identity-vs-synthetic model and forensic-matrix outputs
  - Implemented the first native `ytdlpchopid` parity slice: split identity/synthetic outputs, forensic matrix payloads, chapter-aware clues, scorecard delta fields, C2PA/content-credentials-aware provenance hints, unobtrusive synthetic UI surfacing, targeted suppression/cap SongID scoring tests, segment-derived track plans, and top-candidate search fan-out for ambiguous identity results
  - Replaced SongID fire-and-forget execution with a durable queue + fixed worker pool, persisted queue position / worker slot, recovered queued runs on restart, added recent-run queue UI, expanded forensic payloads with descriptor-priors and generator-family lanes, and added perturbation probes that drive the native `perturbationStability`/confidence outputs
  - Added explicit SongID segment decomposition payloads with grouped segment candidates, segment-specific plans/options, segment batch-search fan-out, focused queue/service tests, and fixed a recovery-state bug so restart provenance survives queue-summary refresh in run evidence
  - Added the first Discovery Graph / Constellation slice: native graph service/API, SongID-seeded typed nodes/edges, MusicBrainz release-group expansion for artist neighborhoods, reusable graph canvas, inline SongID mini-map, modal graph filtering/recenter/queue-nearby actions, and initial backend graph tests
  - Widened Discovery Graph into the MusicBrainz lookup surface, added graph comparison overlays plus richer edge provenance/evidence payloads, and implemented pinned comparison and saved-branch behaviors in the graph UI
  - Updated README / FEATURES / CHANGELOG so SongID and Discovery Graph are reflected as visible product features
  - Widened Discovery Graph across the broader Search UI (search list rows, search detail headers, MusicBrainz, SongID, and search-response cards), added shared queue-nearby batch search handling, and introduced atlas-mode semantic zoom controls plus proper saved-branch restore in the graph modal
  - Added a persistent Discovery Graph Atlas panel on the Search page with manual seeds, saved-branch restore, semantic zoom controls, and queue-nearby handoff
  - Added a dedicated `/discovery-graph` route with modal handoff into that atlas workspace, plus SongID controller unit tests covering run creation, validation, list, and get flows
  - Propagated identity-first acquisition scoring into the remaining SongID segment option paths, reused corpus family hints in scoring, and expanded the dedicated Discovery Graph atlas with inline explainability (edge-family counts, why-near rows, provenance/score breakdowns, recenter actions, and hover context)
  - Fixed the 2026-03-16 packaging regressions for Nix/Winget/Homebrew/dev release metadata, disabled the broken dev flake output, repaired the flaky port-binding/Tor transport tests, and cleaned the touched unit tests up for current async analyzer expectations

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
1. **T-918 SongID parity pass** — Continue the remaining `../ytdlpchopid` delta from `docs/dev/SONGID_INTEGRATION_MAP.md#remaining-todo`: deeper multi-track / mix decomposition beyond chapter/comment inference, richer lane metrics/details, and broader SongID UI coverage for long-running queue and fan-out flows.
2. **T-917 SongID continue** — Push the current `SongID` slice from feature-rich parity toward production parity: larger-scale corpus persistence and evidence reuse, deeper reranking with downstream transfer-quality and peer/source-ranking signals where the data model genuinely fits, and broader backend/frontend tests.
3. **T-919 Discovery Graph continue** — Push from the dedicated atlas route into denser seed types and richer decomposed explanation lanes now that the first inline atlas explainability layer is present.
4. **slskd.Tests.Unit Re-enablement** — Historical note: plan documents still mention remaining work, but the active product priority is now new product work unless explicitly redirected.
5. **Packaging / other product work** — T-010–T-013 done; continue only as prioritized.
6. **Packaging audit follow-up** — Stable Nix/Winget compatibility fix done on 2026-03-16; `slskdn-dev` flake output is now intentionally disabled until a real `build-dev-<version>` release is published and its hashes are populated.
7. **Repo-wide analyzer/lint debt** — `bash ./bin/lint` now passes with error-only gating in `bin/lint`, but the solution still carries a broad non-blocking warning backlog. Keep that cleanup as separate work unless release policy changes.
8. **Release prep** — Packaging/test verification is green locally; if the next step is `.53`, update any remaining release-version metadata deliberately and create the release tag only when explicitly requested.

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
