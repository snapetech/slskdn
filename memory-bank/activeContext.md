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

- **Current Task**: Broad runtime/read-side secure-release bughunt from the stronger green release gate, focused on helper/result contracts that still leak internal error text or caller-controlled details across runtime subsystems
- **Branch**: `release-main`
- **Environment**: Local dev
- **Last Activity**:
  - Restored the remaining unit compile path:
    - `SearchActionsControllerTests`, `MeshContentMeshServiceTests`, and `PodMessageBackfillControllerTests` now match current runtime contracts again
    - `MeshServiceClient` now normalizes immutable `ServiceCall` DTOs via copied instances instead of mutating init-only properties
  - Fixed another release-facing transfer-result leak cluster:
    - `MultiSourceDownloadService` no longer returns exact throughput/byte-count diagnostics in chunk errors
    - `SwarmDownloadOrchestrator` no longer returns peer IDs, transport names, or raw byte counts in chunk failure results
  - Added focused `SwarmDownloadOrchestratorTests` coverage for sanitized chunk-result contracts
  - Confirmed the runtime build is still green (`0 warnings / 0 errors`)
  - Confirmed the unit project compiles again, and the focused touched slice passed (`20/20`)
  - Added the corresponding gotcha to `adr-0001-known-gotchas.md` and committed it immediately per repo policy
  - Hardened another public validation/helper batch:
    - `EnumAttribute` no longer echoes raw invalid enum values or array contents in validation failures
    - `JobManifestValidator` now returns stable category messages for unsupported manifest versions, unknown job types, and invalid status states
    - `DhtRendezvousController` now uses a stable unblock success message instead of echoing the removed blocklist target
  - Added focused `JobManifestValidatorTests` and `EnumAttributeTests`, and extended `DhtRendezvousControllerTests`
  - Confirmed the focused helper/controller validation slice passed (`10/10`)
  - Added the corresponding gotcha to `adr-0001-known-gotchas.md` and committed it immediately per repo policy
  - Hardened another release-facing boundary cluster:
    - `DhtRendezvousController` unblock failures no longer echo raw blocklist `type` / `target` values
    - added focused not-found / invalid-type coverage for DHT rendezvous, port forwarding, and pod-channel controller misses
  - Repaired a runtime build regression in `DhtMeshServiceDirectory` by converting init-only descriptor normalization back to immutable `with` copies
  - Updated `DhtMeshServiceDirectoryTests` to match the immutable descriptor model shape
  - Confirmed the runtime project is green again (`0 warnings / 0 errors`)
  - Narrowed the remaining unit-project compile blockers to:
    - `SearchActionsControllerTests`
    - `MeshContentMeshServiceTests`
    - `PodMessageBackfillControllerTests`
  - Updated the existing init-only record gotcha in `adr-0001-known-gotchas.md` and committed it immediately per repo policy
  - Fixed another controller-boundary leak cluster:
    - `MultiSourceController` no longer reflects usernames or exact source-count thresholds in user-miss / insufficient-source replies
    - `RelayController` no longer echoes missing relay agent names in stream lookup failures
    - `EventsController` no longer enumerates allowed values or echoes raw event-type strings in invalid event replies
    - `ReportsController` no longer exposes enum-name lists in invalid direction/sort validation failures
  - Added focused regression coverage for those release-facing controller contracts
  - Confirmed the runtime project still builds cleanly (`0 warnings / 0 errors`)
  - Confirmed the remaining unit-project compile errors are pre-existing drift in unrelated tests (`SearchActionsControllerTests`, `MeshContentMeshServiceTests`, `PodMessageBackfillControllerTests`)
  - Added the corresponding gotcha to `adr-0001-known-gotchas.md` and committed it immediately per repo policy
  - Fixed another identity/runtime drift cluster:
    - `SoulseekChatBridge` now normalizes bidirectional Soulseek username <-> Pod peer mappings before storing and looking them up
    - `MeshSyncService` now normalizes inbound response correlation IDs before completing pending waiters
  - Added focused `SoulseekChatBridgeTests` coverage for normalized bridge mapping and backward-compatible `bridge:` extraction
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
1. Continue the secure-release boundary sweep through remaining public result/validation surfaces that still echo caller-controlled details, especially remaining controller/problem responses and service/result DTOs surfaced by HTTP or mesh APIs.
2. Start using the restored unit-project compile path to run broader focused slices instead of build-only validation where practical.
3. Keep folding in adjacent dirty files carefully so the runtime and unit build stay green between secure-release passes.

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

## 2026-03-22 17:28
- Folded in remaining dirty share/validator spillover and restored a clean head.
- Next: continue broad bughunt from the remaining runtime/read-side clusters.

## 2026-03-22 17:34
- Sanitized remaining config-validation and secure-framer parser leakage surfaces.
- Next: continue broad bughunt through remaining externally visible result/validation objects rather than pure logs.

## 2026-03-22 17:40
- Sanitized mesh protocol and service-fabric reply contracts that were still reflecting validator details and method/service names.
- Next: continue bughunt through remaining observable validation/result objects, especially shared validation attributes and service reply builders.

## 2026-03-22 17:46
- Sanitized shared file/directory validation attributes so they no longer expose absolute paths through validation failures.
- Next: continue through remaining shared validation and result-builder surfaces that may still leak input details.

## 2026-03-22 17:49
- Folded in dirty moderation/detail-map spillover and restored the metrics validation test path drift.
- Next: continue bughunt from the next clean head after recommitting the remaining dirty files.

## 2026-03-22 17:53

## 2026-03-22 18:47
- Fixed another older-controller normalization cluster:
  - `src/slskd/Shares/API/Controllers/SharesController.cs`
  - `src/slskd/Transfers/MultiSource/API/PlaybackController.cs`
  - `src/slskd/Transfers/MultiSource/API/TracingController.cs`
- These endpoints now trim route/body IDs before lookup/dispatch and reject blank IDs up front, which closes the older low-traffic controller path where padded IDs could miss existing records or split queue/state keys.
- Added focused regressions in:
  - `tests/slskd.Tests.Unit/Shares/API/Controllers/SharesControllerTests.cs`
  - `tests/slskd.Tests.Unit/Transfers/MultiSource/API/PlaybackControllerTests.cs`
  - `tests/slskd.Tests.Unit/Transfers/MultiSource/API/TracingControllerTests.cs`
- Folded in adjacent unit-project compile drift by restoring `using slskd.Jobs;` in `tests/slskd.Tests.Unit/HashDb/HashDbServiceTests.cs`.
- Gotcha commit made immediately per repo policy:
  - `7e61bb61` `docs: Add gotcha for low-traffic controller id normalization`
- Validation:
  - `dotnet build src/slskd/slskd.csproj -v q` -> `0 warnings / 0 errors`
  - `dotnet build tests/slskd.Tests.Unit/slskd.Tests.Unit.csproj -v q` -> `526 warnings / 0 errors`
  - direct `vstest` slice for the 5 new controller tests -> `Passed: 5, Failed: 0`
- Next:
  1. Continue the broad controller-edge sweep through older/secondary API surfaces that still validate presence but do not canonicalize route/body strings.
  2. Prefer grouped passes where a controller family has parallel “busy” and “low-traffic” endpoints with inconsistent normalization rules.

## 2026-03-22 19:03
- Fixed a grouped MediaCore/HashDb normalization cluster:
  - `src/slskd/MediaCore/API/Controllers/DescriptorRetrieverController.cs`
  - `src/slskd/MediaCore/API/Controllers/MetadataPortabilityController.cs`
  - `src/slskd/HashDb/API/HashDbController.cs`
- These endpoints now canonicalize transport identifiers before crossing into service logic:
  - batch `ContentIds` are trimmed, blank-filtered, and deduplicated
  - `contentId`, `domain`, `type`, `flacKey`, `filename`, and `byteHash` are trimmed before lookup or hash generation
- Added focused regressions in:
  - `tests/slskd.Tests.Unit/MediaCore/DescriptorRetrieverControllerTests.cs`
  - `tests/slskd.Tests.Unit/MediaCore/MetadataPortabilityControllerTests.cs`
  - `tests/slskd.Tests.Unit/HashDb/API/HashDbControllerTests.cs`
- Gotcha commit made immediately per repo policy:
  - `8af03b5e` `docs: Add gotcha for batch and query identifier normalization`
- Validation:
  - `dotnet build tests/slskd.Tests.Unit/slskd.Tests.Unit.csproj -v q` -> `526 warnings / 0 errors`
  - direct `vstest` slice for the 8 new controller tests -> `Passed: 8, Failed: 0`
  - `dotnet build src/slskd/slskd.csproj -v q` -> `0 errors`, with the standing `PodMessageBackfill` style warning and a transient copy-retry warning because the DLL was in use
- Next:
  1. Continue grouped controller-edge passes where batch/query endpoints still pass raw identifier collections into services.
  2. Either clear the unrelated `PodMessageBackfill.cs` style warning or work around the in-use runtime DLL when taking the runtime build back to `0/0`.

## 2026-03-22 19:18
- Fixed a grouped MultiSource / VirtualSoulfind v2 controller-normalization cluster:
  - `src/slskd/Transfers/MultiSource/API/MultiSourceController.cs`
  - `src/slskd/VirtualSoulfind/v2/API/VirtualSoulfindV2Controller.cs`
- The batch normalized parallel endpoint families rather than single methods:
  - MultiSource search/verify/download/test/search-user endpoints now trim shared transport strings and normalize per-source request fields before service calls.
  - VirtualSoulfind v2 intent/catalogue endpoints now trim route and request IDs before queue/catalogue/planner/processor dispatch.
- Added focused regressions in:
  - `tests/slskd.Tests.Unit/Transfers/MultiSource/API/MultiSourceControllerTests.cs`
  - `tests/slskd.Tests.Unit/VirtualSoulfind/v2/API/VirtualSoulfindV2ControllerTests.cs`
- Gotcha commit made immediately per repo policy:
  - `7ce8a5c2` `docs: Add gotcha for parallel endpoint family normalization`
- Validation:
  - `dotnet build tests/slskd.Tests.Unit/slskd.Tests.Unit.csproj -v q` -> `556 warnings / 0 errors`
  - direct `vstest` slice for the 9 new controller tests -> `Passed: 9, Failed: 0`
  - `dotnet build src/slskd/slskd.csproj -v q` -> `0 errors`, with current warning noise coming from already-dirty nullable/style areas plus a transient copy-retry warning while the DLL is in use
- Next:
  1. Continue grouped controller-edge passes where sibling endpoints still drift on normalization rules.
  2. Decide whether to spend the next pass paying down the now-visible runtime warning floor (`HashDbService`, `SongIdService`, `PodMessageBackfill`) or keep prioritizing more request-boundary bugs first.
- Sanitized certificate validation and mesh-sync fallback messages that were still exposing internal parser/runtime state.
- Next: continue the broader bughunt from the next clean head, focusing on remaining placeholder/null-heavy runtime paths rather than message leakage.

## 2026-03-22 19:22
- Continued the placeholder/null-heavy completion pass instead of another pure sanitization pass.
- `SongIdService` now keeps conservative recognizer/corpus evidence alive when exact fingerprint/path payloads are missing:
  - SongRec can fall back to textual external IDs
  - Panako/Audfprint can emit conservative findings from textual labels
  - corpus reuse can now fall back to recording/title/artist metadata similarity when exact fingerprint files are unavailable
- `HashDbService` helper/update paths now normalize issue IDs, MusicBrainz IDs, and variant metadata before writes so older helper methods stop drifting from the hardened main lookup paths.
- `PodMessageBackfill` success-path wording now matches reality better while the transport receive seam remains a larger feature gap.
- Gotcha commit made immediately per repo policy:
  - `35c4fe52` `docs: Add gotcha for songid corpus metadata fallback`
- Next:
  1. recommit the full dirty tree, including adjacent controller/test files from parallel work
  2. continue the same three-way pass with the next dense `SongIdService` and `HashDbService` bottom-out cluster

## 2026-03-22 19:31
- Continued the same three-way pass:
  - `SongIdService` parser helpers now tolerate stringified tool payloads and alternate Spotify track forms
  - `HashDbService.TryResolveAcoustIdAsync(...)` now normalizes helper inputs before lookup/tagging
  - `PodMessageBackfill` now excludes self using canonical peer IDs and skips blank message/sender identities during response processing
- Gotcha commit made immediately per repo policy:
  - `9b38d978` `docs: Add gotcha for stringified parser payloads`
- Next:
  1. recommit the full dirty tree, including adjacent controller files from parallel work
  2. continue deeper into the remaining `SongIdService` and `HashDbService` bottom-out cluster

## 2026-03-22 19:41
- Continued the deeper `SongIdService` + `HashDbService` cluster:
  - SongID segment-query planning now uses transcript phrases and OCR text instead of ignoring those collected findings
  - HashDb album target and canonical stats write/read paths now normalize more of their key/value fields symmetrically
- Added focused unit coverage for:
  - transcript/OCR-derived segment queries
  - stringified helper payload parsing
  - trimmed album target persistence
  - trimmed canonical stats persistence
- Gotcha commit made immediately per repo policy:
  - `8ebb6a04` `docs: Add gotcha for unused songid evidence`
- Next:
  1. recommit the full tree
  2. continue the next `SongIdService` + `HashDbService` batch after this clean head

## 2026-03-22 19:49
- Continued the same `SongIdService` + `HashDbService` batch again:
  - SongID fallback search now uses transcript/OCR/comment evidence, not just top-level metadata
  - HashDb job/warm-cache row-fallback readers now trim returned values so readback matches the hardened write-side behavior
- Added focused coverage for those fallback/read-side paths.
- Next:
  1. recommit the full tree
  2. continue the remaining `SongIdService`/`HashDbService` cluster, then move back into Mesh/PodCore runtime gaps

## 2026-03-22 20:02
- Returned to the Mesh/PodCore runtime slice:
  - `PeerResolutionService` now supports trimmed hostname endpoints from DHT metadata
  - `PodDiscovery` now normalizes returned metadata before filtering and deduping
  - `MeshSyncService` now trims remote hash keys before consensus grouping
- Added focused coverage for the PodCore runtime behaviors above.
- Gotcha commit made immediately per repo policy:
  - `4bcb1cb9` `docs: Add gotcha for dht metadata normalization`
- Next:
  1. recommit the full tree
  2. continue the remaining Mesh/PodCore runtime gap after this clean head

## 2026-03-22 18:01
- Normalized PodCore peer/pod read paths and VSF source-registry reads so whitespace drift and blank persisted keys no longer under-report available state.
- Next: keep widening through remaining placeholder/null-heavy runtime paths in PodCore, VirtualSoulfind, and Mesh.

## 2026-03-22 18:08
- Replaced the reachable empty-result placeholder in `ContentLinkService` with the existing MusicBrainz audio search integration and folded in the current dirty spillover batch.
- Next: continue through the remaining placeholder/null-heavy runtime paths from a clean head.

## 2026-03-22 18:14
- Created a tracked placeholder/null-heavy inventory and grouped the remaining work into batches.
- Next: execute Batch A (`SongID` + `MetadataFacade`) or Batch B (`MeshSync` + transport/runtime fallback completion).

## 2026-03-22 18:12
- Sanitized `X509.TryValidate(...)`, the multi-source `RunTest(...)` endpoint error field, and the `SecurityMiddleware` catch-path event message so those helper/diagnostic surfaces no longer relay raw nested exception text.
- Folded in adjacent unit drift fixes in `MeshSyncSecurityTests` and `ContentLinkServiceTests`, and documented the recurring pattern in `adr-0001-known-gotchas.md` with an immediate docs-only commit.
- Next: continue the broad bughunt from the current head, prioritizing other helper/result contracts that still look diagnostic but cross an observable boundary.

## 2026-03-22 18:14
- Normalized controller-edge strings in `BackfillController`, the small audio controllers, and `UserNotesController` so whitespace-only or padded IDs no longer reach the service layer as distinct keys or bad arguments.
- Added focused regression coverage for the normalized/blank-input paths and revalidated the batch with a clean runtime build, targeted unit slice, and lint.
- Next: keep widening the bughunt through other low-traffic controllers and helper endpoints that still look like thin pass-throughs and may be skipping controller-edge normalization.

## 2026-03-22 18:23
- Normalized `SongIdController`, `StreamsController`, and `SolidController` so route/query/body values are trimmed before dispatch and blank values fail as `400`s instead of leaking into service-layer behavior.
- Fixed a separate compile blocker in `MetadataFacade` caused by reused local names during a fallback-refactor, and documented both patterns immediately in `adr-0001-known-gotchas.md`.
- Next: continue through the next thin-controller/helper batch, with emphasis on native utility controllers and other low-traffic endpoints that still bypass controller-edge canonicalization.

## 2026-03-22 18:29
- Normalized nested request fields in `ProfileController` and `ContactsController` so optional strings and endpoint collections are canonicalized before the service layer sees them.
- Added focused regression coverage for trimmed invite/nickname/peerId/display-name flows and for rejecting whitespace-only contact updates.
- Next: continue through the next low-traffic controller/helper batch, especially native utility endpoints and messaging controllers that still accept raw strings in route/body pairs.

## 2026-03-22 18:36
- Aligned the specialized discography/label-crate job controllers with the native jobs API so the whole job domain now applies the same controller-edge normalization rules.
- Restored the runtime build to `0 warnings / 0 errors` after the batch by matching the existing non-nullable `TargetDirectory` contract.
- Next: continue through the next controller/helper cluster, likely messaging or telemetry endpoints that still mix route/body strings and older pass-through patterns.

## 2026-03-22 18:22
- Executed Batch A against `SongIdService`, `MetadataFacade`, and `MusicBrainzClient`.
- Replaced one major early-bottom-out pattern: metadata hits without MBIDs now continue through SongID with conservative synthetic IDs instead of being discarded.
- Added new focused unit coverage for metadata search fallback, filename-derived local-file metadata, and trimmed/deduplicated MusicBrainz search results.
- Next: continue Batch A deeper into the remaining `SongIdService` / `SongIdScoring` helper paths, then take Batch B (`MeshSync` + transport/runtime fallback completion).

## 2026-03-22 18:31
- Continued Batch A deeper into SongID helper/scoring paths.
- Fixed helper/path normalization around Panako/Audfprint discovery, corpus fingerprint resolution, excerpt start determinism, and loose-text scoring equivalence.
- Next: keep pushing through the remaining `SongIdService` bottom-out paths or switch to Batch B (`MeshSync` + transport/runtime fallback completion).

## 2026-03-22 18:42
- Started Batch B with `MeshSyncService`.
- Fixed two concrete under-reporting/runtime issues: duplicate in-flight request failure and impossible small-mesh quorum requirements.
- Next: continue Batch B through `CapabilityFileService`, `MeshOverlayConnector`, and `StunNatDetector`, then circle back for any remaining deep `SongIdService` bottom-out paths.

## 2026-03-22 18:55
- Continued Batch B through `CapabilityFileService`, `MeshOverlayConnector`, and `StunNatDetector`.
- Fixed capability/endpoint normalization, preserved partial capability identity, restored the live connection return in mesh overlay connect, and added IPv6 STUN mapped-address parsing.
- Next: inspect the remaining dirty tree, commit everything, then keep pushing through `MeshOverlayConnector` / transport fallback paths or circle back into the next dense `SongIdService` cluster.

## 2026-03-22 19:07
- Switched into the next dense HashDb completion slice.
- Fixed symmetric key normalization for HashDb recording/job identifiers and added focused readback regression coverage.
- Next: commit the current tree, then continue into the next HashDb null-heavy read path or return to the remaining Mesh/PathGuard clusters.

## 2026-03-22 19:21
- Tightened the next security/helper cluster by routing the weaker DHT rendezvous `PathGuard` through the hardened shared `Common.Security.PathGuard` instead of leaving two divergent implementations in the tree.
- Expanded `HttpSignatureKeyFetcher` so it trims `keyId`, rejects oversized responses earlier, and can extract PEM keys from either top-level key documents or actor `publicKey` arrays.
- Folded in adjacent dirty controller work for destinations/wishlist boundary validation and prepared focused regressions for the new pathguard/key-fetch behaviors.
- Next: commit the current tree including all dirty files, then continue through the next placeholder/null-heavy Mesh/PodCore runtime cluster.

## 2026-03-22 19:33
- Continued into the PodCore completion batch and replaced another enrichment-coupling bottom-out in `ContentLinkService`.
- Supported domains now return conservative metadata when enrichment is missing, and audio-artist lookups opportunistically upgrade that metadata from existing MusicBrainz search hits.
- Next: keep pushing through the remaining PodCore/Mesh placeholder and read-side under-report paths, especially backfill/opinion/runtime helpers that still return less than available local state.

## 2026-03-22 19:42
- Continued deeper into PodCore runtime helpers by normalizing opinion/backfill keys before signature parsing, membership matching, cache tracking, and targeted routing.
- Backfill now fails cleanly on blank target peers instead of pretending to route, and successful sends report an honest “awaiting response handling” status instead of the older placeholder sentence.
- Next: continue through the remaining PodCore/Mesh placeholder paths, especially the larger backfill-response/runtime completion gaps and any adjacent dirty repo spillover.

## 2026-03-22 19:51
- Shifted into the next MeshSync completion slice and normalized peer/key inputs at lookup, request, publish, and peer-state boundaries.
- Mesh hash lookups and chunk requests now reuse the same in-flight waiters even when transport strings arrive padded, instead of splitting one logical request into separate pending keys.
- Next: keep pushing through the remaining Mesh/PodCore placeholder paths, especially larger transport-response completion gaps and any new dirty spillover in the tree.

## 2026-03-22 20:02
- Returned to the Pod backfill flow and fixed the top-level fan-out contract so backfill no longer reports success when every peer request failed.
- Backfill request/response processing now trims pod/channel/peer/message identifiers, deduplicates target peers more safely, and stores normalized messages instead of dropping them on harmless whitespace drift.
- Next: continue through the remaining larger transport-response gaps, especially actual backfill response transport handling and the next Mesh/PodCore placeholder cluster.

## 2026-03-22 20:14
- The actual overlay receive seam for backfill responses is still a larger transport feature, so I pivoted to the next dense local-completion batch in `HashDbService`.
- Tightened the smaller HashDb helper/update/list methods so they use the same identifier normalization rules as the main lookup APIs, closing more whitespace-drift misses at the storage edge.
- Next: commit the current tree including dirty spillover, then continue deeper into the next HashDb/SongID/Mesh completion cluster.

## 2026-03-22 19:47
- Normalized older helper/controller boundaries in `MusicBrainzController`, `DiscoveryGraphController`, `WishlistController`, and `DestinationsController`, and trimmed decoded relative paths in `FilesController`.
- Added focused regressions for those controller/path cases and cleared the adjacent unit-project compile blockers in `HttpSignatureKeyFetcher`, `PathGuardTests`, and several stale test files so focused test slices run cleanly again.
- Validation state:
  - `dotnet build src/slskd/slskd.csproj -v q` passed with `0 warnings / 0 errors`
  - focused unit slices for wishlist/destinations and files/discovery-graph/musicbrainz passed (`4/4` and `15/15`)
  - `dotnet build tests/slskd.Tests.Unit/slskd.Tests.Unit.csproj -v q` passed with pre-existing analyzer warning noise (`521 warnings / 0 errors`)
- Next: continue the broad bughunt through remaining low-traffic controller/helper endpoints and stale unit-test drift that still hides behind the unit-project warning wall.

## 2026-03-22 20:01
- Normalized parser discriminator strings in `CapabilitiesController`, `PerceptualHashController`, and `NowPlayingController` before parser dispatch, enum parsing, and webhook event classification.
- Added focused regressions for trimmed capability tags, trimmed perceptual-hash algorithms, and trimmed generic now-playing webhook events.
- Validation state:
  - `dotnet build src/slskd/slskd.csproj -v q` passed with `0 warnings / 0 errors`
  - focused parser/controller slice passed (`8/8`)
- Next: continue through the next low-traffic helper/controller batch, especially older utility endpoints that still branch on raw strings or carry adjacent test drift.

## 2026-03-22 20:26
- Recovered the runtime release gate after an immutable-model regression in `HashDbService`.
- Album target and canonical-stats normalization now respect `init`-only records by normalizing through local `with` copies, and the adjacent SongID / MultiSource nullable DTO warnings are cleaned up too.
- Validation state:
  - `dotnet build src/slskd/slskd.csproj -v q` passed with `0 warnings / 0 errors`
- Next: continue the secure-release bughunt from externally visible mesh/result contracts rather than local compile hygiene.

## 2026-03-22 20:34
- Hardened the next release-facing mesh reply cluster:
  - service-fabric mesh services no longer echo caller-controlled method names
  - private-gateway DNS failures no longer relay downstream validator text
  - VirtualSoulfind mesh batch validation no longer echoes rejected MBID values
- Focused validation passed:
  - `dotnet build src/slskd/slskd.csproj -v q`
  - `dotnet build tests/slskd.Tests.Unit/slskd.Tests.Unit.csproj -v q` with existing analyzer warning noise only
  - `vstest` slice for `PrivateGatewayMeshServiceTests`, `PodsMeshServiceTests`, `MeshContentMeshServiceTests`, and `VirtualSoulfindMeshServiceTests` passed (`20/20`)
- Next: continue through the next secure-release surfaces, prioritizing remaining externally visible mesh/service replies and helper result DTOs over internal-only logging cleanup.

## 2026-03-22 20:43
- Continued through adjacent infrastructure-facing result surfaces after the first mesh reply batch.
- Mesh introspection and DHT method-not-found replies now use the same generic contract, mesh-content no longer exposes local file sizes in oversized-file errors, and realm migration import failures no longer return absolute filesystem paths.
- Focused validation passed:
  - `dotnet build src/slskd/slskd.csproj -v q`
  - `vstest` slice for `MeshIntrospectionServiceTests`, `DhtMeshServiceTests`, `MeshContentMeshServiceTests`, and `RealmMigrationToolTests` (`17/17`)
- Next: keep pushing toward a fixed secure release by auditing the next layer of externally visible result DTOs and mesh/service error objects before spending time on purely internal analyzer noise.

## 2026-03-22 20:51
- Continued into the next mesh reply-category pass.
- Private-gateway quota errors no longer disclose configured numeric thresholds, and DHT / hole-punch invalid-payload replies no longer teach callers the exact field names or byte-count expectations.
- Focused validation passed:
  - `dotnet build src/slskd/slskd.csproj -v q`
  - `vstest` slice for `DhtMeshServiceTests`, `HolePunchMeshServiceTests`, and `RateLimitTimeoutTests` (`16/16`)
- Next: continue the release-facing audit through the next layer of externally visible DTOs and controller/problem responses that still reflect raw identifiers, indexes, or object-not-found specifics.

## 2026-03-22 20:59
- Continued into the search action-routing surface.
- `SearchActionsController` no longer echoes raw search IDs, response/file indexes, item IDs, or content IDs inside `ProblemDetails.Detail` for action-routing failures.
- Focused validation passed:
  - `dotnet build src/slskd/slskd.csproj -v q`
  - `vstest` slice for `SearchActionsControllerTests` (`11/11`)
- Next: keep pushing through the next controller/result boundary layer, especially older NotFound/Problem payloads that still include raw route IDs or lookup specifics in otherwise public contracts.

- Replaced placeholder-success controller/introspection responses with real local-state answers in PodCore and Mesh Service Fabric.

- Hardened active mesh service adapters so embedded Pod/DHT IDs are canonicalized before crossing into pod or routing-table logic.

## 2026-03-22 22:05
- Continued the fastest-path Pod/Mesh runtime completion batch instead of widening back out to unrelated surfaces.
- Hardened DHT-backed discovery and peer-resolution behavior:
  - `DhtMeshServiceDirectory` now normalizes both lookup keys and returned descriptor payloads before validation/dedupe.
  - `PodDiscovery` now falls back to indexed pod IDs when direct metadata lookup misses on harmless ID drift.
  - `PeerResolutionService` now reuses cached username aliases and canonical metadata peer IDs more consistently.
  - `MeshContentMeshService` now trims content IDs and rejects invalid ranges at the adapter boundary.
- Dirty repo spillover is being committed together with this batch per user instruction.
- Next: finish the remaining Pod/Mesh runtime seams from the placeholder inventory, then rerun full validation and the release gate.

## 2026-03-22 22:18
- Finished another grouped Pod/Mesh runtime completion batch.
- `PodAffinityScorer` now consumes real membership-history signals for trust/stability instead of placeholder trust heuristics.
- `MeshStatsCollector` now resolves live in-memory DHT state through the concrete runtime service shape or its aliases.
- `MeshServiceClient` now normalizes service/method/correlation inputs and selects the freshest valid provider deterministically.
- Next: commit all dirty files in the repo, then run full validation and the release gate.
