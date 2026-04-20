## Update 2026-04-19 20:30:00Z

- Current task: None. DHT/overlay observability and bad-candidate cooldown follow-up is implemented locally and validated.
- Last activity:
  - added bounded endpoint cooldowns and top-problem endpoint stats to `MeshOverlayConnector` so repeatedly bad DHT-discovered overlay addresses stop getting hammered
  - added periodic DHT/overlay summary logging and explicit candidate rollup counters in `DhtRendezvousService`
  - added inbound/outbound mesh session-end summary logs with connection age and disconnect reason
  - exposed the new diagnostics through the existing DHT/overlay status API and covered them with focused unit tests
  - ran the local cycle: focused DHT/overlay unit tests passed, `dotnet build src/slskd/slskd.csproj --no-restore -v minimal` passed, `bash ./bin/lint` passed, and `git diff --check` passed
  - ran `./bin/build`; the build path itself succeeded through web/release compilation but the full Release unit-test phase hit a single failure in `slskd.Tests.Unit.Transfers.Downloads.DownloadServiceTests.ShutdownAsync_WaitsForCancelledDownloadsToDrain`, which then passed when rerun in isolation
- Next steps:
  1. Decide whether to treat the Release full-suite `DownloadServiceTests.ShutdownAsync_WaitsForCancelledDownloadsToDrain` failure as an existing flaky race or to debug it before the next release build.
  2. Deploy the new DHT/overlay diagnostics to `kspls0` and sample whether the summary lines clearly distinguish bad remote endpoints (`no-route`, `tls-eof`, etc.) from local capacity/backoff behavior.
  3. If remote candidate churn remains dominant, add endpoint deprioritization on top of the current cooldowns rather than increasing automatic probe volume.

## Update 2026-04-20 02:30:00Z

- Current task: None. The failed `build-main-0.24.5-slskdn.160` CI release-smoke regression is fixed locally and validated.
- Last activity:
  - traced the failed tag build to `Release Gate` compile-time integration smoke, not runtime failures
  - found that `tests/slskd.Tests.Integration/StubWebApplicationFactory.cs` still implemented the old `IDownloadService` surface after `ShutdownAsync(CancellationToken)` was added for shutdown drain sequencing
  - added the missing `StubDownloadService.ShutdownAsync` no-op implementation
  - documented the interface/test-double drift gotcha in ADR-0001 and committed that doc entry as `58c184c7f`
  - reran the exact release-smoke validation path locally: Release unit smoke passed, Release integration smoke passed, `packaging/scripts/run-release-integration-smoke.sh` passed, `bash ./bin/lint` passed, and `git diff --check` passed
- Next steps:
  1. Commit the remaining code/doc changes for the DHT/overlay diagnostics pass plus the CI smoke fix.
  2. Push `main` when desired.
  3. Create a replacement build tag only if the user explicitly wants a new release build.

## Update 2026-04-20 02:55:00Z

- Current task: None. The local release gate is green again; one non-gate full-integration interference remains in the heavier `./bin/build` pass.
- Last activity:
  - continued the local release-candidate cycle with `run-release-gate.sh` and `./bin/build`
  - fixed two release-suite test fragilities: `PortForwardingControllerTests` no longer hardcode fixed local ports, and `DownloadServiceTests.ShutdownAsync_WaitsForCancelledDownloadsToDrain` now waits for explicit tracked-work completion instead of relying on a fixed delay
  - documented the test-flakiness pattern in ADR-0001 and committed it as `c1d21e8b4`
  - reran the release bar successfully: focused Release unit regressions passed, `bash packaging/scripts/run-release-gate.sh` passed end to end, `bash ./bin/lint` passed, and `git diff --check` passed
  - identified one remaining heavier-suite issue outside the release gate: `./bin/build` still hit a single full-instance integration failure (`TwoNodeMeshFullInstanceTests.TwoFullInstances_CanFormOverlayMeshConnection` returning `502 Bad Gateway` on the initial overlay-connect call), but that exact test passed immediately when rerun in isolation
- Next steps:
  1. Commit the remaining test-hardening changes.
  2. Decide whether the isolated `TwoNodeMeshFullInstanceTests` full-suite interference should block the next release candidate, given that the documented local release bar is green.
  3. If you want a stricter candidate, debug the full `./bin/build` integration-suite interference next before tagging.

## Update 2026-04-20 03:20:00Z

- Current task: None. The local next-release-candidate cycle is clean again, including the heavier full `./bin/build` path.
- Last activity:
  - traced the remaining full-suite-only `TwoNodeMeshFullInstanceTests.TwoFullInstances_CanFormOverlayMeshConnection` `502 Bad Gateway` failure to `SlskdnFullInstanceRunner` marking instances ready after API health alone
  - hardened the full-instance harness to wait for the configured overlay TCP listener before tests issue `/api/v0/overlay/connect`, and reused the same helper shape already used for bridge readiness
  - documented the startup-readiness gotcha in ADR-0001 and committed that doc-only entry as `e26b30713`
  - reran the focused full-instance mesh test successfully, then reran the heavy local path successfully: `./bin/build` passed end to end; `bash ./bin/lint` and `git diff --check` also passed
- Next steps:
  1. Commit the remaining harness/doc updates and push `main` if you want this clean local RC state reflected on origin.
  2. Cut the next official release-candidate/build tag only if explicitly requested.
  3. After the next tag, watch CI/package jobs rather than local release-gate coverage; the local manual bar is currently green.

## Update 2026-04-20 03:45:00Z

- Current task: None. The UI/admin audit findings are fixed locally and revalidated.
- Last activity:
  - fixed `/system/mediacore` by restoring the missing `Checkbox` import
  - fixed `/pods` by putting `PodsController` on the explicit `api/v{version:apiVersion}/pods` route with `[ApiVersion("0")]` and added a unit contract test for that route/version pair
  - redirected `/` straight to `/searches`, removed the unconditional per-render `session.check()` call, and quieted the expected unauthenticated/session-expiry path in `session.js`
  - exempted authenticated requests and non-API web shell/static requests from the coarse fixed-window IP limiter so fast admin sweeps no longer self-trigger `429`
  - changed first-run share bootstrap to log a recreate/scan path instead of throwing a corruption-looking exception before recovery
  - revalidated with focused tests, `bash ./bin/lint`, `git diff --check`, and a disposable manual browser/api sweep over `/`, `/searches`, `/pods`, `/system/info`, and `/system/mediacore`; all passed with no page errors and no reproduced `429`
- Next steps:
  1. Commit and push the remaining code/test changes if you want the admin-audit fixes on `origin/main`.
  2. If you want another broad product sweep, rerun the full top-level/admin-panel Playwright crawl against this build and then triage any deeper workflow bugs that remain beyond the original hard failures.

## Update 2026-04-20 04:05:00Z

- Current task: None. The failed `build-main-0.24.5-slskdn.161` tag regression is fixed locally and the release gate is green again.
- Last activity:
  - pulled the raw `Build on Tag #228` job logs and confirmed the failure was the Release-only `DownloadServiceTests.ShutdownAsync_WaitsForCancelledDownloadsToDrain` timing out again in CI, not a packaging or product regression
  - documented the recurring startup/cancellation race in ADR-0001 and committed that docs-only entry as `22df366c6`
  - hardened the shutdown-drain test so it waits for the mocked download worker to actually start before invoking shutdown, verifies shutdown stays blocked until drain completion is permitted, then awaits shutdown completion directly
  - revalidated with the exact local release gate: the targeted Release test passed, a `5`-run Release loop of that exact test passed, `bash packaging/scripts/run-release-gate.sh` passed, and `bash ./bin/lint` passed
- Next steps:
  1. Commit and push the remaining test/doc updates.
  2. Move the failed `build-main-0.24.5-slskdn.161` tag to the fixed commit or cut `build-main-0.24.5-slskdn.162` if you want the cleanest retry path.
  3. Watch the next tag build specifically for the `Release Gate` job; that was the only failing segment on `#228`.

## Update 2026-04-20 01:52:00Z

- Current task: None. The latest `kspls0` live-debug pass is implemented, committed, deployed, and host-validated.
- Last activity:
  - kept QUIC enabled and healthy on `kspls0`, with overlay/data listeners active and mesh reconnecting after each deliberate restart
  - fixed `DownloadService` shutdown cleanup ordering by draining in-flight enqueue/download work before the shared Soulseek client is disconnected or disposed
  - fixed the remaining false-fatal shutdown edge by tolerating the third-party `SoulseekClient.Disconnect()` `Sequence contains no elements` race during expected host shutdown
  - deployed `0.24.5-slskdn.159+manual.1475cd068` to `kspls0` and validated deliberate restarts with active downloads: no global download semaphore warnings, no transfer cleanup `ObjectDisposedException`, no false fatal shutdown log, restart count `0`, DHT healthy, and one mesh peer connected immediately after restart
- Next steps:
  1. Keep sampling `kspls0` for longer-run QUIC and mesh stability, especially whether the single live peer stays connected past the keepalive windows on this final manual build.
  2. If more mesh issues surface, focus on remote candidate quality and connector failure mix (`timeout`, `no-route`, `tls-eof`) rather than the now-fixed local shutdown and framing paths.
  3. Push `main` when desired, and only create a build tag if the user explicitly wants a release build.

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

- **Current Task**: None. Live `kspls0` QUIC/mesh validation and follow-up bug fixes are implemented, committed, deployed, and host-validated.
- **Branch**: `main`
- **Environment**: Local dev on `snapetech/slskdn`; live validation on `kspls0` running `0.24.5-slskdn.159+manual.1475cd068`; no release tags were created.
- **Last Activity**:
  - Kept QUIC enabled by installing Microsoft MsQuic `v2.5.7` on `kspls0`; QUIC overlay/data listeners bind on `50402/50401` with no temporary systemd disable override.
  - Fixed live mesh compatibility with unframed JSON overlay frames; `kspls0` connected to `m***7` and held past the 2-minute keepalive threshold without `Protocol violation`, `Invalid message length`, `Unregistered`, or disconnect logs.
  - Fixed DHT rendezvous accounting so connector-capacity deferrals are not counted as real attempts or pushed into retry backoff.
  - Fixed user directory browse API handling so expected remote peer connection failures return controlled 503 responses instead of unhandled middleware stack traces.
  - Fixed service SIGTERM handling so normal `systemctl restart` stops the host cleanly; validated on `kspls0` that a deliberate restart logs expected shutdown, not status 1/failure.
  - Fixed transfer shutdown cleanup ordering so active downloads drain before Soulseek client disposal, removing restart-time semaphore/disposed-object noise.
  - Fixed the remaining third-party `SoulseekClient.Disconnect()` shutdown race so clean restarts do not emit false fatal `Sequence contains no elements` logs.
  - Deployed `0.24.5-slskdn.159+manual.1475cd068` to `kspls0`.
  - Validation passed: focused `DhtRendezvousServiceTests|SecureMessageFramerTests`, focused `UsersControllerTests`, focused `DownloadServiceTests`, repeated `dotnet build src/slskd/slskd.csproj --no-restore -v minimal`, repeated `bash ./bin/lint`, `git diff --check`, release publishes, and live API/log probes with deliberate restart validation.
- **Next Steps**:
  1. Keep sampling `kspls0` for longer-run QUIC stability and any recurrence of `libmsquic` core dumps.
  2. Keep candidate filtering/deprioritization for bad DHT-discovered overlay endpoints as the remaining DHT mesh follow-up.
  3. Push `main` when ready, then create a build tag only if the user explicitly wants a release build.

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
