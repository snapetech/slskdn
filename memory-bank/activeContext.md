## Update 2026-04-17 23:05:00Z

- Current task: Package/release pipeline regressions from `build-main-0.24.5-slskdn.135` are fixed locally and validated; ready to commit, push, and retag.
- Last activity:
  - fixed the Docker release failure by moving `Dockerfile` onto real `.NET 10 noble` SDK/runtime-deps images and validating a full local Docker build
  - realigned stable package metadata, COPR/RPM inputs, and the metadata refresh script with the published `linux-glibc-*` release assets on `0.24.5-slskdn.135`
  - revalidated packaging metadata, a full Nix flake smoke build, and the Docker image path locally
- Next steps:
  1. Commit the packaging/workflow/docs fix set and push `main`.
  2. Cut a fresh stable tag to replace the failed `build-main-0.24.5-slskdn.135` run.
  3. Monitor the replacement tag for COPR, Docker, and metadata-update success before closing out the release.

## Update 2026-04-18 01:18:23Z

- Current task: None. The next round of issue `#209` follow-up fixes are implemented locally and validated.
- Last activity:
  - inspected the newer `#209` tester logs after DHT bootstrap started succeeding and separated remaining failures from misleading noise
  - fixed the real `GET /api/v0/users/notes` regression by advertising both API versions `0` and `1` on `UserNotesController`
  - removed the mesh overlay connector's bogus UDP hole-punch preflight against DHT-discovered TCP overlay endpoints, which was producing guaranteed `FAILED` logs with ephemeral local UDP ports that looked like randomized listeners
  - clarified hole-punch completion logs so operators can see the reported local port is an ephemeral UDP socket, not a configured listener port
  - added focused versioned-route integration coverage for `/api/v0/users/notes`
- Next steps:
  1. Redeploy the latest `Program` classifier fix to `kspls0` and verify that remote-declared transfer failures no longer emit fake `[FATAL] Unobserved task exception` noise on the new process.
  2. Keep sampling multi-peer downloads on `kspls0` to see whether any remaining post-`InProgress` failures cluster around one transport/peer pattern or are just normal remote-side churn.

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

- **Current Task**: Manual validation build on `kspls0`; source-ranking history race fixed locally and focused tests pass. Next step is to deploy the updated manual build and watch logs.
- **Branch**: `main`
- **Environment**: Local dev on `snapetech/slskdn`; live validation on `kspls0`; no release tags were created.
- **Last Activity**:
  - Published and installed a local manual build on `kspls0`, confirmed the running process is the manual release path/version, and verified the DHT diagnostics API-key fix live.
  - Verified Soulseek login, shares, listener ports, DHT bootstrap/discovery, broad search completion, and resumed transfer activity on the manual build.
  - Found a live transfer-load race in `SourceRankingService`: concurrent first writes to `DownloadHistory` for the same username could trip SQLite unique-key errors.
  - Replaced the source-ranking read-then-insert/update path with an atomic SQLite upsert, added concurrent regression coverage, documented the gotcha in ADR-0001, and validated `SourceRankingServiceTests`.
- **Next Steps**:
  1. Commit the source-ranking upsert fix.
  2. Publish/install a new manual build on `kspls0` and watch logs for recurrence of `DownloadHistory.Username` unique constraint errors or transfer-related fatal noise.
  3. Keep the existing follow-up on candidate filtering/deprioritization for DHT-discovered non-overlay endpoints.

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
- No active blocker from the old `.NET 10` test-tail issue; the previously hanging full-solution test run now exits after the integration harness/test fixes.

### Current focus (the rest)
- **40-fixes plan (PR-00–PR-14):** Done. slskd.Tests 46, slskd.Tests.Unit 2257 pass; Epic implemented. Deferred table: status only.
- **T-404+:** Done. t410-backfill-wire (RescueMode underperformance detector → RescueService); codec/fingerprint (T-420–T-430) done per dashboard.
- **slskd.Tests.Unit re-enablement:** ✅ **COMPLETE** (2026-01-27): All phases (0-5) done. 2430 tests passing, 0 skipped, 0 failed. No `Compile Remove` remaining. All test files enabled and passing per `docs/dev/slskd-tests-unit-completion-plan.md`.
- **New product work**: As prioritized.

**Research (9) implementation:** ✅ Complete. T-901–T-913 all done per `memory-bank/tasks.md`.

### Next Steps
1. If `#209` still reports failures after this, reproduce the next symptom from live logs before changing anything else.
2. Validate whether circuit usage beyond the current placeholder builder needs to move onto the overlay connection path instead of raw transport streams.
3. Push and tag only after this direct-mode fix is committed and the user wants another release.

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
- **.NET Version**: 10.0
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

## Update 2026-04-17 20:25:00Z

- Current task: None. The `#209` root-cause follow-up is implemented locally and validated.
- Last activity:
  - proved the failure was in the pinned upstream DHT library path, not just local logging/config wording
  - upgraded `MonoTorrent` from `3.0.2` to `3.0.3-alpha.unstable.rev0049`
  - added explicit `dht.bootstrap_routers` handling and validation so slskdn no longer depends on one hidden upstream bootstrap router
- Next steps:
  1. Run `bash ./bin/lint` and `git diff --check`, then commit the code/config/test follow-up.
  2. Push `main` and trigger a fresh stable build once the user wants the fix released.
  3. Update issue `#209` only after the fixed build is available for retest.

## Update 2026-04-17 21:59:58Z

- Current task: Stable package metadata drift fixed locally; ready to push and clean up failed tag `build-main-0.24.5-slskdn.133`.
- Last activity:
  - traced the `Nix Package Smoke` failure to stable metadata pointing at unreleased `slskdn-main-linux-glibc-*` assets while the latest published stable release (`0.24.5-slskdn.131`) still publishes `slskdn-main-linux-x64.zip` / `slskdn-main-linux-arm64.zip`
  - reverted the stable metadata consumers and the metadata updater script to the currently published stable asset names
  - validated packaging metadata successfully; local Nix smoke remains unexercised here because `nix` is not installed on this machine
- Next steps:
  1. Commit and push the stable metadata fix.
  2. Delete the stale `build-main-0.24.5-slskdn.133` tag locally and on origin.
  3. Re-run a fresh stable tag build only after the metadata fix is on `main`.


## Update 2026-04-18 03:45:00Z

- Current task: None. Issue `#209` circuit peer sync follow-up is implemented locally and validated.
- Last activity:
  - traced the latest `#209` report past DHT bootstrap into a split-brain state between `MeshNeighborRegistry` and `IMeshPeerManager`
  - reproduced the old failure in unit tests: live overlay neighbors alone left circuit peer stats at zero
  - added `MeshNeighborPeerSyncService` so DHT overlay neighbor add/remove events populate and prune the circuit peer inventory used by `CircuitMaintenanceService` and `MeshCircuitBuilder`
  - added focused unit tests proving the old empty-peer state without the sync service and the corrected populated-peer state with it
- Next steps:
  1. Commit the code/test/docs fix set if lint and diff checks stay green.
  2. If `#209` still reports zero circuits after this, inspect actual outbound overlay connection success/failure rates and remote peer feature compatibility rather than local peer inventory wiring.


## Update 2026-04-18 09:05:00Z

- Current task: None. The latest `kspls0` live transfer and DHT follow-up is implemented locally and validated on-host.
- Last activity:
  - proved that successful transfers on `kspls0` were still emitting fake fatal `Transfer failed: Transfer complete` unobserved-task noise and fixed the classifier in `Program`
  - opened `50305/tcp` and `50306/udp` in the `kspls0` host firewall, then verified DHT eventually reaches `Ready` on the live host once both router and host firewall paths are open
  - proved the old 30-second DHT bootstrap warning was too aggressive on a healthy host and extended the bootstrap grace period to 120 seconds
- Next steps:
  1. Commit and push the current `Program` / DHT bootstrap follow-up if the worktree stays clean.
  2. Watch the next `kspls0` runtime window for a successful transfer on the patched process and confirm the `Transfer complete` fatal log never returns.
  3. If DHT still shows slow `Initialising` windows on other hosts, consider making the bootstrap diagnostics adaptive instead of static.


## Update 2026-04-18 09:45:00Z

- Current task: None. The Docker and raw Linux release-installer smoke pass is complete locally.
- Last activity:
  - built the current Docker image locally and verified the shipped container reports `0.24.5-slskdn.141` when running the embedded `slskd` binary
  - smoke-tested `packaging/linux/install-from-release.sh` against the latest published stable release on a clean `ubuntu:24.04` container and found a real cleanup bug in the installer's `EXIT` trap
  - fixed the installer trap so cleanup no longer references an out-of-scope function-local `work_dir`, then reran the published-release smoke successfully and verified `/usr/bin/dotnet /opt/slskdn/slskd.dll --version` plus the generated `slskd.service`
  - confirmed this machine still lacks `flatpak-builder`, `snapcraft`, and `brew`, so those package-manager paths remain un-smoked here
- Next steps:
  1. Push the installer trap fix if you want it included in the next release.
  2. Use a machine with `flatpak-builder`, `snapcraft`, or `brew` available to add real install-smokes for those remaining ship methods.


## Update 2026-04-18 14:55:00Z

- Current task: None. The Jammy PPA / standalone packaging drift fix is implemented locally and validated.
- Last activity:
  - pulled the real Launchpad Jammy build log for `0.24.5.slskdn.144` and confirmed the build was failing in `debian/rules` because the standalone PPA path had drifted behind the main release flow: stale `.NET 8` workflow pin plus a hard-coded `libcoreclrtraceptprovider.so` patch path
  - updated `release-ppa.yml`, `release-copr.yml`, and `release-linux.yml` to use `.NET 10` and added publish-output verification steps so those workflows validate the staged Linux app bundle before packaging it
  - hardened the DEB/RPM `liblttng-ust` SONAME patching to discover `libcoreclrtraceptprovider.so` dynamically inside the staged package tree instead of assuming one flat path
  - validated the Debian staging logic locally with a real self-contained publish and confirmed the staged trace-provider library now depends on `liblttng-ust.so.1`
- Next steps:
  1. Push the packaging/workflow fix and cut a new stable build so Launchpad retries with the corrected PPA path.
  2. Watch the next Jammy build specifically; if it still fails, the next problem will be in the PPA source-package assembly or Launchpad environment rather than this runtime-path drift.

## Update 2026-04-18 11:20:00Z

- Current task: None. The latest issue `#209` root-cause follow-up is implemented locally and validated.
- Last activity:
  - traced the newest `#209` tester state past successful DHT bootstrap into a real inventory split: `DhtRendezvousService` discovered peers and attempted overlay connects, but never populated `IMeshPeerManager`, leaving `CircuitMaintenanceService` and `MeshCircuitBuilder` at `0 total peers` even when DHT had already found candidates
  - fixed that split by publishing DHT-discovered overlay endpoints into `IMeshPeerManager` immediately as onion-capable peer candidates and recording connection success/failure back onto the same peer records
  - broadened stale antiforgery token recovery so key-ring/decryption mismatches surfaced as raw `CryptographicException` (or other wrapped exception shapes) still clear the known cookies and retry token minting once
  - added focused unit coverage for both regressions and reran the DHT/circuit/hosted-service/security slices plus `./bin/lint`
- Next steps:
  1. Push the current fix set and cut a new build if you want the latest `#209` root fix in a tester-facing release.
  2. If the tester still reports trouble after this build, inspect live overlay connection success/failure rates and remote peer compatibility rather than DHT bootstrap or peer inventory wiring.

## Update 2026-04-18 10:05:00Z

- Current task: None. The Jammy PPA packaging regression is fixed locally and validated.
- Last activity:
  - inspected the Launchpad Jammy build log for `slskdn 0.24.5.slskdn.141-1ppa...` and confirmed the failure was `patchelf: No such file or directory` during `override_dh_auto_install`
  - added the missing Debian source-package dependency (`patchelf`) to `packaging/debian/control` so Launchpad installs the tool required by `debian/rules`
  - reproduced the PPA source-package build locally in a clean `ubuntu:22.04` container using the same source-tree shape as `release-ppa.yml`, and verified `dpkg-buildpackage -b` now completes successfully
- Next steps:
  1. Push the Debian packaging fix if you want the next PPA/release build to pick it up.
  2. Monitor the next Jammy PPA build for any second-stage Launchpad-only issues after this missing `Build-Depends` fix.


## Update 2026-04-18 17:35:00Z

- Current task: None. The next issue `#209` root fix is implemented locally and validated on `kspls0`.
- Last activity:
  - stepped back from the earlier tester reports and revalidated the live overlay path on `kspls0` instead of trusting the synthetic release gates
  - proved the current blocker was stale overlay TOFU pinning rather than version-locking: `minimus7` was a real reachable slskdn overlay peer, but a stale stored thumbprint caused our side to self-partition
  - changed inbound and outbound overlay handshakes to rotate stored certificate pins on mismatch instead of auto-blocking the peer, added focused `CertificatePinStoreTests`, and validated on `kspls0` that the host now logs the mismatch, rotates the pin, and still registers/connects the neighbor in the same DHT cycle
- Next steps:
  1. Commit the pin-rotation fix set if the worktree stays clean.
  2. If another `#209` symptom appears, reproduce it on `kspls0` first and add the missing host-backed smoke before cutting another build.


## Update 2026-04-18 17:55:00Z

- Current task: None. The next issue `#209` cleanup pass is implemented locally and validated on `kspls0`.
- Last activity:
  - proved the live node was still overstating peer health after DHT discovery by counting raw DHT endpoints as onion-capable peers before overlay verification
  - changed DHT rendezvous publishing so discovered endpoints stay as `dht-discovered` candidates until a real overlay connect succeeds, and updated focused DHT rendezvous tests to cover candidate, failed-connect, and verified-connect states
  - redeployed the cleanup build to `kspls0` and verified that `/api/v0/security/peers/stats` now reports `onionRoutingPeers: 0` while `/api/v0/dht/status` still shows the raw discovered candidate count separately
- Next steps:
  1. Push this peer-stats cleanup if you want it in the next release.
  2. If `#209` continues, the next investigation should focus on why the discovered candidates are not handshaking, not on inflated peer counters.

## Update 2026-04-18 18:10:00Z

- Current task: None. The latest DHT/overlay investigation and concurrent security hardening are committed locally and ready to push.
- Last activity:
  - continued the live `#209` investigation past the peer-count cleanup and confirmed the next real gap is handshake success for raw DHT candidates, not inflated counters
  - folded in the concurrent `CertificatePinStore` durability hardening so pin-store writes are now atomic and cannot silently corrupt `cert_pins.json` on partial write
  - added a DHT / mesh overlay audit note under `docs/security/` capturing the current threat-surface review and security decisions
- Next steps:
  1. Push the latest commits if you want the peer-stat cleanup and pin-store durability fix on `origin/main`.
  2. If `#209` still persists after that, focus on why the discovered candidates fail TLS/HELLO rather than on DHT discovery, pin rotation, or peer-count reporting.


## Update 2026-04-18 19:05:00Z

- Current task: None. The live DHT rendezvous retry/backoff fix is implemented and host-validated on `kspls0`.
- Last activity:
  - traced the active mesh issue back to `DhtRendezvousService` using `_discoveredPeers.TryAdd(...)` as a once-ever outbound connect trigger, which meant the host never retried already-seen endpoints after a single timeout/refusal
  - split discovery caching from retry scheduling by adding explicit attempt timestamps and in-flight tracking with a 5-minute backoff for unverified peers
  - validated the change on `kspls0` by forcing a post-backoff discovery cycle and observing `totalConnectionsAttempted` rise from `26` to `31` while `discoveredPeerCount` stayed at `26`, proving rediscovered candidates now re-enter the connector instead of staying stranded
- Next steps:
  1. Commit/push the retry/backoff fix if the worktree stays clean.
  2. Continue narrowing the live peer pool by filtering or deprioritizing clearly bad/non-overlay endpoints before they dominate mesh retries.
