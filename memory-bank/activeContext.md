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

- **Current Task**: Broad runtime/read-side bughunt from the stronger green release gate, focused on helper/result contracts that still leak internal error text across runtime subsystems
- **Branch**: `release-main`
- **Environment**: Local dev
- **Last Activity**:
  - Fixed a file-safety / transfer-status result cluster:
    - `PathGuard` no longer echoes raw path exceptions in validation results
    - both `ContentSafety` implementations no longer echo raw file-read exceptions
    - `MeshTransferService` no longer copies raw exception text into transfer status
  - Added focused regression coverage for those sanitized helper/result contracts
  - Added the corresponding gotcha to `adr-0001-known-gotchas.md` and committed it immediately per repo policy
  - Fixed another runtime/status sanitization cluster:
    - `LibraryHealthService` no longer persists raw exception text into scan state or emitted corrupted-file issue reasons
    - `ContentVerificationService` no longer returns raw verification exception text in failed-source results
  - Added focused regression coverage for those sanitized scan/verification failure contracts
  - Added the corresponding gotcha to `adr-0001-known-gotchas.md` and committed it immediately per repo policy
  - Fixed another mesh/runtime status sanitization cluster:
    - `MeshContentFetcher` no longer returns raw mesh client exception text in fetch results
    - `MeshSyncService` no longer copies raw sync exceptions into `MeshSyncResult.Error`
    - `SwarmDownloadOrchestrator` no longer copies raw exception text into job/chunk status errors
  - Added focused regression coverage for the mesh fetch/sync failure contracts and documented the recurring pattern in `adr-0001-known-gotchas.md`
  - Fixed another protocol/status sanitization cluster:
    - `BridgeProxyServer` no longer sends raw exception text back over the bridge protocol for generic internal errors or failed download requests
    - `MeshHealthCheck` no longer embeds raw exception text in degraded health descriptions
    - `MeshCircuitBuilder` no longer stores raw exception text in hop status records
  - Documented the recurring pattern in `adr-0001-known-gotchas.md`
  - Fixed another helper/batch result cluster:
    - `RegressionHarness` no longer copies raw exception text into scenario/test/benchmark results
    - `AutoReplaceService` no longer puts raw exception text into per-download batch details
    - `Dumper` no longer returns raw dump-creation exception text in its result tuple
  - Added focused harness coverage and documented the recurring pattern in `adr-0001-known-gotchas.md`
  - Fixed another diagnostics/runtime contract cluster:
    - anonymity transports no longer store raw exception text in `LastError`
    - `HttpLlmModerationProvider` no longer exposes raw HTTP exception text in moderation responses or provider health
    - `SongIdService` no longer stores raw exception text in run summaries/evidence for analysis and auxiliary pipeline skips
  - Added focused coverage in transport tests, moderation tests, and `SongIdServiceTests`, and documented the recurring pattern in `adr-0001-known-gotchas.md`
  - Fixed another infrastructure boundary cluster:
    - `ValidateCsrfForCookiesOnlyAttribute` no longer copies raw exception text into `ProblemDetails.Detail`
    - `MeshServiceDescriptorValidator` no longer returns raw serializer exception text in its validation tuple
  - Folded in the adjacent dirty cleanup already in the tree for `Dumper`, `LibraryHealthService`, and `SongIdService`
  - Documented the recurring pattern in `adr-0001-known-gotchas.md`
  - Fixed a DHT/mesh security-helper result cluster:
    - `PeerVerificationService` no longer returns raw Soulseek/transport exception text in verification results
    - `DnsLeakPreventionVerifier` no longer returns raw socket/transport exception text in verification or leak-test results
  - Added focused unit coverage for those sanitized security-helper failure contracts
  - Added the corresponding gotcha to `adr-0001-known-gotchas.md` and committed it immediately per repo policy
  - Fixed a VirtualSoulfind v2 result-contract cluster:
    - `SimpleResolver` no longer copies raw exception text into `PlanExecutionState` or `StepResult`
    - `HttpBackend` and `WebDavBackend` no longer echo raw `HttpRequestException` messages in validation results
  - Added focused unit coverage for those sanitized VSF v2 failure contracts and folded in adjacent dirty mesh service test/code changes already in the tree
  - Added the corresponding gotcha to `adr-0001-known-gotchas.md` and committed it immediately per repo policy
  - Fixed another PodCore/MediaCore runtime cluster:
    - `MetadataPortability` now registers imported entries with normalized external IDs instead of raw domain values
    - `PodMessageRouter` now deduplicates/normalizes target peer IDs and no longer counts privacy-batched payloads as already routed
    - `PodOpinionService` now upserts per-sender/per-variant opinions instead of appending duplicates forever, and refresh counts now track real deltas
    - `PodJoinLeaveService` no longer fabricates empty pending-request buckets on read/cancel helpers and now compares peer IDs consistently
  - Added the corresponding gotcha to `adr-0001-known-gotchas.md` and committed it immediately per repo policy
  - Added a reusable Nix package/module smoke:
    - new `packaging/scripts/run-nix-package-smoke.sh`
    - builds `.#default`
    - launches the packaged `bin/slskd`
    - evaluates the minimal `services.slskd` NixOS module contract with the required `domain`, `environmentFile`, and `settings.shares.directories = [ ]` inputs
  - Wired the Nix smoke into both `ci.yml` and `build-on-tag.yml`, including a post-stable-metadata-update smoke in the main-channel tag workflow
  - Added a real subpath-hosted web smoke:
    - new `src/web/scripts/smoke-subpath-build.mjs`
    - wired into `packaging/scripts/run-release-gate.sh`
    - serves the built UI under `/slskd/` and verifies the relative asset URLs actually load from that mount point
  - Updated `docs/dev/release-checklist.md` and `docs/dev/testing-policy.md` so the release gate documentation includes the new subpath-hosted smoke step
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
1. Continue the broad runtime/read-side bughunt from the stronger green release gate and packaging-smoke baseline.
2. Prioritize remaining places where result DTOs, background-state records, or lightweight service helpers still expose raw internal error text.
3. Keep folding in adjacent dirty files so the repo stays committed and clean between passes.

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
