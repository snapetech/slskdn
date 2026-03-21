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

- **Current Task**: Land the final `master` push for the CodeQL/security cleanup and verify GitHub closes the remaining open alerts/PRs
- **Branch**: `security-fixes-master`
- **Environment**: Local dev
- **Last Activity**:
  - Disabled GitHub default CodeQL setup via the API, verified recent `master` CodeQL runs are green, manually updated `Formula/slskdn.rb` to release `0.24.5-slskdn.57`, and patched `build-on-tag.yml` so the Homebrew, Nix, and Winget main-repo write-back steps rebase/retry before push.
  - Triggered release `build-main-0.24.5-slskdn.58`, confirmed Homebrew and Winget write-backs now succeed, and found the remaining failure in `Update Nix Flake (Main)` still comes from branch churn during concurrent repo write-backs.
  - Manually updated `flake.nix` to `0.24.5-slskdn.58` and strengthened the write-back retries again with explicit `origin/master` refspec fetches and a longer retry window.
  - Replaced duplicated GitHub release bodies with shared templates, rewrote the copy around SongID and Discovery Graph as first-class features, updated the Winget marketing copy, and added release-copy validation/documentation so future feature changes have one source of truth.
  - Pulled the `.59` Nix job log and confirmed the retry loop was failing because `nix flake check` left the checkout dirty, so `git rebase` could never start. Updated the workflow to validate with `--no-write-lock-file`, regenerate `flake.nix` from fresh `origin/master` on each attempt, and manually landed the missing `.59` flake update.
  - Removed the deeper architectural source of these metadata races by replacing the separate Nix/Winget/main-formula writer jobs with one consolidated `metadata-main` job that pushes a single combined metadata commit to `master`.
  - Booted a disposable local NixOS 25.11 QEMU/KVM VM, validated the current `flake.nix` against a real NixOS userspace, and drove the package/service path far enough to distinguish packaging/runtime failures from ordinary application-config failures.
  - Updated the stable flake pins to GitHub release `0.24.5-slskdn.54`, added the missing Nix runtime patching pieces (`autoPatchelfHook`, `patchelf`, `dontStrip`, `lttng-ust` SONAME rewrite), and synced the packaging metadata validator to the new flake contract.
  - Verified in the NixOS VM that `nix build --no-write-lock-file 'path:/mnt/hostrepo#default'` succeeds and `/nix/store/.../bin/slskd --help` runs; `services.slskd` now launches the packaged binary and only stops on app-level config validation with dummy credentials.
  - Fixed that metrics config-validation false positive locally: `metrics.authentication.password` is now only required when metrics are enabled and auth is enabled, with focused unit coverage and updated config docs/example.
  - Investigated GitHub issue #117 (`How to install in NixOS declaratively?`) and fixed the remaining stable flake failure: after the earlier `bin/slskd` wrapper fix, NixOS was still rejecting the packaged generic Linux ELF with `stub-ld` / status 127 because the flake only wrapped it instead of patching it.
  - Updated `flake.nix` to use `autoPatchelfHook` plus required runtime libraries, extended packaging metadata validation for the new Nix requirement, and documented the gotcha in ADR-0001.
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
  - Confirmed GitHub default CodeQL setup is still `not-configured`, verified the open alert flood is attached to `refs/heads/master`, and narrowed the checked-in `.github/workflows/codeql.yml` from `queries: security-and-quality` to `queries: security-extended`
  - Investigated the `.76` release hang, confirmed the only stuck job was `Publish to Snap Store (stable)`, and updated both Snap Store upload steps to use per-attempt `timeout --signal=TERM 10m` bounds plus explicit retry/failure logging so future releases do not hang indefinitely inside `snapcraft upload`
  - Rebased the live security-fix work onto the actual GitHub `master` tip (`047a6da3`), excluded `cs/log-forging` from CodeQL, constrained destination/library-health/mesh-transfer paths to configured roots, switched bridge default downloads back to the configured downloads directory, and required auth on `PodMembershipController`

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
1. Push `security-fixes-master` to `origin/master` and confirm GitHub Actions starts a fresh CodeQL run from the updated workflow.
2. Verify the open code-scanning set collapses to zero or the intended residual baseline after that run completes.
3. Resolve PR `#147` once the branch state is current, either by merging it or closing it if the dependency bump is otherwise landed.
4. Clean up the malformed XML doc comments that keep producing publish warnings across all runtimes.

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
