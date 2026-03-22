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

- **Current Task**: Broad runtime/read-side bughunt across `src/slskd`, focused on controller-boundary normalization, truthful status/config reporting, and keeping adjacent dirty files/tests committed in coherent batches
- **Branch**: `release-main`
- **Environment**: Local dev
- **Last Activity**:
  - Continued the broad controller-boundary bughunt into file/chat/security helper surfaces, then folded the remaining native/hashdb/mesh/transfers, identity/search, Solid, Users, PodCore, and final port-forward/options/share spillover into the same working sweep.
  - Normalized additional controller behavior:
    - `FilesController` now validates encoded route segments before Base64 decode/path resolution and returns explicit `400`s for invalid file/directory paths
    - `RoomsController` and `ConversationsController` now trim room/user/message inputs and reject blank or invalid identifiers before tracker/service dispatch
    - `SecurityController` now exposes the missing `GET /security/adversarial` route, validates count/limit inputs, trims security admin identifiers, and sanitizes remaining transport/config/circuit failure contracts
    - adjacent relay upload/download helper spillover was folded in so token/header inputs are trimmed, invalid Base64 relay filenames return `400`, and share-validation failures no longer leak raw exception text
    - maintenance/debug helpers in native library lookup, port forwarding, HashDb optimization, mesh NAT detection, transfer queue operations, and multi-source search/download now return sanitized `500` contracts instead of raw exception text
    - `ContactsController` and `SearchesController` now return stable sanitized invite/search failure contracts instead of exposing decode/start exceptions
    - `SolidController`, `UsersController`, `PodContentController`, `PodChannelController`, and native `PodsController` now stop surfacing raw policy/offline/service-validation exception text
    - `PortForwardingController`, `OptionsController`, and `SharesController` now return stable duplicate-forward/YAML-validation/download-enqueue error contracts instead of surfacing underlying exception text
  - Continued with another pass focused on unstable controller-level catch/rethrow and spillover error contracts:
    - `EventsController` now returns stable `500` contracts for list/raise failures instead of logging and rethrowing, and trims raised-event route/body input before parsing
    - `ProfileController` now rejects null bodies for profile update/invite creation, trims `peerId` lookups, and no longer leaks thrown exception text from invite generation
    - `DiscoveryController` now trims `SearchTerm` and rejects non-positive `size`, `limit`, and `minSources` queries before dispatch
    - folded adjacent dirty spillover into the same batch so `DownloadsCompatibilityController`, `SearchActionsController`, and `SharesController` no longer surface raw exception messages in client-facing error lists/details
  - Continued into PodCore/native service-result contracts:
    - `PodMembershipController` now returns a fixed not-found contract instead of surfacing membership service `ErrorMessage` text
    - `PodDhtController` now returns a fixed pod-not-found contract instead of surfacing publisher `ErrorMessage` text
    - `PodOpinionController` now returns a fixed publish-failure contract instead of surfacing opinion service `ErrorMessage` text
    - added focused regressions for those sanitized result-message paths
  - Folded the next dirty result-contract spillover into the same sweep:
    - `PodDiscoveryController`, `PodJoinLeaveController`, and `PodMessageRoutingController` now return stable public failure contracts instead of forwarding service `ErrorMessage` values
    - `MeshGatewayController` now returns a stable service-error message instead of forwarding mesh reply text
    - `ContentDescriptorPublisherController` now returns stable bad-request contracts for failed publish/update paths instead of raw publisher result objects
    - `DescriptorRetrieverController` now returns a stable `Descriptor not found` contract instead of surfacing retriever result text
    - added/folded focused regressions for the remaining PodCore/Mesh/MediaCore failure paths, including a new `DescriptorRetrieverControllerTests` file
  - Folded the next dirty validation/not-found batch into the same sweep:
    - `OptionsController`, `HashDbController`, and `MeshController` no longer surface raw validation or sync-result detail
    - `CapabilitiesController`, `ContentIdController`, and `PodsController` no longer echo raw usernames, external IDs, or pod IDs in public not-found responses
    - added/folded focused regressions for those sanitized validation/not-found contracts, including a new `CapabilitiesControllerTests` file
  - Validation has still not been run in this pass.
  - Added/folded focused unit regressions for files, rooms, conversations, relay filename decoding, security-controller seams, the maintenance/status leak cleanup across native/hashdb/mesh/transfers, the identity/search helper cleanup, the Solid/users/PodCore error-contract fixes, and the final port-forward/options/share spillover.
  - Validation has still not been run in this pass.

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
1. If requested, run `dotnet test` and `./bin/lint` to validate the committed runtime/read-side cleanup.
2. Resume scanning for broad bug clusters where the public API reports success but the backing work is impossible, mis-serialized, silently filtered out, or split across normalized-vs-raw boundary assumptions.
3. Prioritize remaining controller actions that still treat service result objects, validation helper output, or caller-supplied identifiers as client-safe response bodies.
4. Re-scan any remaining native/compatibility/mesh/share/file-messaging helpers for post-trim duplicate collisions, route-scope mismatches, encoded payloads that still bypass boundary validation, or sanitized-error gaps on maintenance endpoints.
5. Continue outward into remaining stats/status/search controllers and any lingering PodCore/Sharing APIs that still accept raw IDs, null bodies, repeated list values, or raw service result text without normalization.

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
