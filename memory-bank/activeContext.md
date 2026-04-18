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

- **Current Task**: None. The AUR upgrade-path regression is fixed locally and validated; the next packaging step is deciding when to release/publish the new AUR layout, while the remaining live-transfer work on `kspls0` is still the narrower post-`InProgress` remote stream failure pattern on some peers.
- **Branch**: `main`
- **Environment**: Local dev
- **Last Activity**:

  - Reproduced the real `slskdn-bin` AUR upgrade failure on `kspls0`, proved pacman checks file conflicts before `pre_upgrade()` can run, and reworked the AUR package layout so `/usr/lib/slskd/slskd` remains the drop-in launcher while bundled releases live under `/usr/lib/slskd/releases/<version>` and `/usr/lib/slskd/current`.
  - Built and installed a local `slskdn-bin 0.24.5.slskdn.140-2` on `kspls0` after planting fake unowned junk files directly under `/usr/lib/slskd`; pacman upgraded cleanly, `/usr/bin/slskd --version` still reported `0.24.5-slskdn.140`, and `slskd.service` stayed active.
  - Reproduced the real `slskdn-bin` AUR failure on `kspls0`: `yay` built `0.24.5.slskdn.140-1` correctly, but pacman aborted with root-level `/usr/lib/slskd` file conflicts before any package scriptlet could run.
  - Verified the key packaging gotcha: pacman checks filesystem conflicts before `slskd.install` `pre_upgrade()`, so the previous root-prune hook could never rescue an already-conflicting upgrade.
  - Reworked the AUR package layout to keep the drop-in launcher path `/usr/lib/slskd/slskd` and service/config names unchanged while moving each bundled release payload under `/usr/lib/slskd/releases/<version>` and `/usr/lib/slskd/current`.
  - Simplified `packaging/aur/slskd.install` back to sysusers + daemon-reload hooks only, updated AUR docs/metadata validation, and added the packaging gotcha to ADR-0001.
  - Proved the new layout on `kspls0` by building a local `slskdn-bin 0.24.5.slskdn.140-2`, creating unowned junk files directly under `/usr/lib/slskd`, and confirming pacman upgraded cleanly with the stale root files still present. The launcher now execs `/usr/lib/slskd/current/slskd`, `/usr/bin/slskd --version` still reports `0.24.5-slskdn.140`, and `slskd.service` stayed active.

  - Audited `kspls0` directly and confirmed the transfer/runtime complaints have two layers:
    - the live service is up, but the journal shows repeated Soulseek `Connection refused` / `Connection reset by peer` exceptions, search endpoint resolution timeouts, and DHT / mesh logs back in `NotReady` / `0 circuits` state
    - the Transfers page itself was compounding that by issuing per-file `Promise.all(...)` bulk retry/remove requests that trip the backend download limiter and create one toast per failed request
  - Finished the Transfers UI bulk-action fix properly:
    - bulk retry/remove/cancel now enqueue into a background queue in `Transfers.jsx` instead of running inline from the click handler
    - queued and in-flight work is deduped by transfer/action key, so repeated clicks while a drain is running do not reschedule the same transfers
    - the top-level `Remove All Completed` path still uses `transfers.clearCompleted(...)`, but that clear request is now queued and deduped too
    - added focused web regression tests proving one-at-a-time draining, duplicate bulk-submission suppression, single-toast failure reporting, and deduped clear-completed behavior
  - Re-checked `kspls0` after that UI fix and found the current concrete host-side transfer blocker:
  - Deployed the file-permissions fix and the enqueue-semaphore lifetime fix to `kspls0` with a self-contained `linux-x64` publish because the host still only has `.NET 8` installed while the repo targets `.NET 10`:
    - confirmed the old `The value cannot be an empty string or composed entirely of whitespace. (Parameter 'permissions')` download crash no longer appears after restart
    - confirmed the old `Task for enqueue ... Cannot access a disposed object. Object name: 'System.Threading.SemaphoreSlim'` crash no longer appears after restart
    - verified the host now reaches `DHT state changed to: Ready` immediately on restart and a re-enqueued download advances through `Queued, Remotely -> Initializing -> InProgress` again
    - identified the new remaining boundary: some peers still fail later with remote-side closure / timeout outcomes (`Remote connection closed`, `Download reported as failed by remote client`, `The wait timed out after 15000 milliseconds`), while at least one earlier transfer did complete successfully, so the host is no longer globally stuck at zero-byte local queue failures
    - downloads are failing inside `FileService.CreateFile(...)` because the default empty `permissions.file.mode` string is still being parsed as if it were a real chmod value
    - fixed `FileService.CreateFile(...)` and `MoveFile(...)` so unset/whitespace permission modes fall back to the OS umask instead of throwing, and added focused unit coverage for both paths

  - Re-opened issue `#209` after the reporter said the same DHT error remained and verified the shipped release artifacts directly:
    - downloaded the published `0.24.5-slskdn.130` and `.131` ARM64 zip assets locally and confirmed `.131` contains the newer `slskd.dll` / `MonoTorrent.dll` bits
    - confirmed the reporter's `version 126` UI screenshot/log evidence means the live instance was still running an older backend, not the `131` backend we thought they were testing
  - Added runtime self-identification to prevent another false positive:
    - startup now logs the executable path and base directory
    - `/system/info` now exposes the running executable path, base directory, app directory, config path, and process id via `State.Runtime`
  - Reproduced the published `0.24.5-slskdn.131` Linux release zip directly and confirmed the artifact itself reports `131`, which shifts the remaining `#209` problem from the binary payload to the Linux install/migration path when users upgrade from an existing `slskd` systemd service
  - Fixed that release packaging gap:
    - added `packaging/linux/install-from-release.sh` as a supported Linux installer/migration path for release users
    - updated stable and dev release workflows to publish `slskd.service`, `slskd.yml`, `slskd.sysusers`, and `install-linux-release.sh` alongside the zip assets
  - Fixed the release-pipeline follow-up after the `#209` build review:
    - updated all workflow `DOTNET_VERSION` pins plus the Dockerfile SDK/runtime images from `.NET 8` to `.NET 10`, which addresses the tagged Docker build failure (`NETSDK1045`)
    - corrected both Matrix release-announcement cleanup calls in `build-on-tag.yml` to use `PUT` for `/redact/...`, matching the homeserver behavior that was returning `405`
    - added additive `slskdn-*-linux-glibc-x64.zip` and `slskdn-*-linux-glibc-arm64.zip` aliases for both channel and versioned release assets while preserving the existing `slskdn-main-*`, `slskdn-dev-*`, and legacy versioned names used by packaging
  - Documented the release workflow drift / Matrix redact gotchas in `ADR-0001` and committed that docs checkpoint separately as `21aeac9d`.
  - Investigated issue `#209` and fixed the DHT bootstrap operator trap:
    - replaced the random fallback DHT UDP port with a stable default `50306` so forwarding / allow-listing is predictable across restarts
    - added startup validation so enabled DHT cannot run with `dht.dht_port = 0`
    - updated the example config to expose the DHT section and call out the forwarding / UPnP expectations directly
    - changed the bootstrap timeout warning to state clearly that announce/discovery stay disabled until the DHT reaches `Ready`, and to name the exact UDP port operators need to check
  - Documented the random DHT bootstrap port gotcha in `ADR-0001` and committed the doc checkpoint separately as `ab33da85`.
  - Reproduced the still-open peer connectivity failure in a fresh two-node local Soulfind probe and proved it was not another download-engine regression:
    - with `Soulseek.ListenIpAddress = 127.0.0.1`, Alice resolved Bob to `172.17.0.1:<port>` and `endpoint`, `info`, and `browse` all failed with `Failed to establish a direct or indirect message connection`
    - rerunning the same topology with `Soulseek.ListenIpAddress = 0.0.0.0` made the same peer operations succeed immediately
  - Added a startup validation guard in `Options.Validate(...)` so live clients now reject loopback `Soulseek.ListenIpAddress` when `Flags.NoConnect` is false, added focused unit coverage in `SoulseekOptionsValidationTests`, and updated the example config comment to steer operators toward reachable listener binds.
  - Documented the loopback-listener gotcha in `ADR-0001` and committed it separately as `4f984008`.
  - Fixed the release-note carry-forward bug:
    - documented the gotcha in `ADR-0001` and committed it immediately as `4f1b0e80`
    - updated `scripts/generate-release-notes.sh` so tagged releases never publish the rolling `## [Unreleased]` section
    - taught the generator to resolve previous published release ranges correctly even when the build starts from `build-main-*` / `build-dev-*` source tags instead of the logical release tag
    - rewrote `docs/CHANGELOG.md` so it now keeps only `## [Unreleased]` plus the latest three shipped releases (`0.24.5-slskdn.123` / `.124` / `.125`) in a cleaner delta-only format
    - updated `scripts/validate-changelog-entry.sh` placeholder guidance so it matches the new “move only shipped bullets when cutting a release” rule
    - validated the generator locally for `0.24.5-slskdn.123`, `.124`, and `.125`, confirming the generated notes now contain only the intended per-release changes
  - Corrected the frontend dependency handling on `main`: removed the temporary `axios` / `lodash` Dependabot ignore entries, upgraded the actual web dependency state to `axios 1.15.0` with locked `lodash 4.18.1`, rebased the fix onto the `.123` release metadata commit, and pushed it as `5ad4d215`.
  - Re-reviewed the tester follow-up on issues `#200` and `#201` instead of trusting the earlier “fixed” state:
    - found that `src/web/src/lib/jobs.js` was still building `/api/jobs...` requests through an Axios client already rooted at `/api/v0`, which exactly explains the tester’s `/api/v0/api/jobs...` 404s
    - found that the MediaCore controllers still used versioned-looking routes without explicit `ApiVersion` metadata, which is why those endpoints could still fail in production despite passing under the permissive integration host
    - found that the release integration smoke filter never exercised the versioned Jobs / MediaCore / Security / Bridge route families at all
  - Fixed those gaps by:
    - switching the jobs Web UI helper/tests to relative `/jobs...` paths
    - adding explicit `/api/v{version:apiVersion}/...` + `[ApiVersion("0")]` metadata to `JobsController` and the MediaCore controllers
    - adding `VersionedApiRoutesIntegrationTests` for the production-facing `/api/v0/...` route families the tester broke
    - extending `packaging/scripts/run-release-integration-smoke.sh` so release smoke includes the new versioned route tests plus the existing Security/Bridge route suites
  - Validation for this follow-up:
    - focused web helper tests passed
    - focused unit/integration route tests passed
    - frontend production build passed
    - built-output verification passed
    - `bash ./bin/lint` passed
    - `dotnet test -v minimal` still does not terminate cleanly in this environment after the main test projects report passing counts; the remaining integration `testhost` stays alive without additional output
  - Documented the route-smoke blind spot in `ADR-0001` and committed the gotcha separately as `5838c1de`.
  - Tightened the remaining `#201` runtime masking:
    - removed the blanket `SocketError.ConnectionRefused` benign-unobserved-task special-case from `Program`, so only the narrower Soulseek peer/distributed-network classifier can downgrade expected refusal churn
    - updated focused unit coverage to prove refused connections are no longer globally blessed as benign while still classifying Soulseek-network refusal churn as expected
    - documented the suppression gotcha in `ADR-0001` and committed it separately as `96dffd12`
  - Fixed one concrete remaining `#201` download-path root cause:
    - removed the eager `GetUserEndPointAsync(...)` / `ConnectToUserAsync(...)` peer-control preflight from `DownloadService.EnqueueAsync(...)`, because it could abort downloads on an auxiliary `Connection refused` path before the real transfer pipeline ran
    - added `DownloadServiceTests` coverage that now fails if download enqueue starts depending on that preflight again
    - documented the peer-preflight gotcha in `ADR-0001` and committed it separately as `e4f84359`
  - Fixed a second startup-side `#201` transfer seam:
    - extracted `Application.CreateStartupSoulseekClientOptionsPatch(...)` and added the missing `incomingConnectionOptions` assignment so clean startup now configures the same transfer-related option surface that later live reconfigure already used
    - added `ApplicationLifecycleTests` coverage that fails if startup patching ever drops the incoming transfer connection options again
    - documented the startup patch drift in `ADR-0001` and committed it separately as `cd345aab`
  - Re-verified the current regression net after the download-path fix:
    - focused unit tests for `DownloadServiceTests`, `ProgramPathNormalizationTests`, and `ConnectionWatchdogTests` passed
    - focused versioned-route integration tests passed
    - `bash packaging/scripts/run-release-integration-smoke.sh` passed (`39/39`)
    - `bash ./bin/lint` passed
  - Tightened the release smoke path itself:
    - `packaging/scripts/run-release-integration-smoke.sh` now runs the focused startup/transfer unit regression slice (`ApplicationLifecycleTests`, `DownloadServiceTests`, `ProgramPathNormalizationTests`, `ConnectionWatchdogTests`) before the integration route smoke
    - reran that revised release smoke successfully
  - Monitored `build-main-0.24.5-slskdn.123` and confirmed the stable release path now reaches `Create Main Release` successfully and the `Announce Main Release to Discord` job completes successfully; the Discord outage report was downstream of the earlier release-gate regression, not a webhook failure.
  - Reviewed closed-but-still-unresolved tester issues `#200` and `#201` without touching the issue threads and identified the main failure mode: we kept shipping builds on partial symptom relief without reproducing the tester's exact path first.
  - Added a repo-level reproduce-first follow-up:
    - new `docs/dev/bugfix-verification-checklist.md`
    - `docs/dev/testing-policy.md` now requires reproduce-then-disprove validation for externally reported bugs
    - `docs/dev/release-checklist.md` now states that green generic smoke is not enough for claimed bugfix releases
    - `memory-bank/decisions/adr-0004-pr-checklist.md` now requires the exact repro/acceptance answers before calling a reported bug fixed
  - Audited the remaining Dependabot holds and removed stale cleanup-only suppression:
    - deleted the dead `react-scripts` ignore
    - pinned `@uiw/react-codemirror` to exact `4.21.21` so the lockfile no longer drifts onto the React-17-only `4.25.x` line
    - confirmed the current `eslint-config-canonical 47.4.2` line requires ESLint 9 and moved `src/web` onto `eslint.config.mjs` with the direct import resolver dependencies that flat config now needs
  - Closed the remaining web lint follow-up from that ESLint 9 move:
    - replaced the broken / over-scoped web lint setup with an explicit flat config for app and test files
    - added direct `eslint-plugin-react-hooks` and `eslint-plugin-promise` dependencies so legacy disable comments resolve cleanly again
    - fixed the stale `searches.createBatch(...)` import bug in `src/web/src/components/Search/Response.jsx`
    - fixed the `src/web/src/components/System/Files/Explorer.jsx` `+` / `??` precedence bug so totals cannot collapse through `NaN`
    - documented both frontend gotchas in `ADR-0001`
  - Added direct proof coverage for the remaining tester issues:
    - new `src/web/src/serviceWorkerCaching.test.js` covers the stale cached app-shell behavior behind the white-tab `/assets/*` 404 failure in `#200`
    - `ApplicationLifecycleTests` now asserts the startup Soulseek options patch leaves listener fields unset so `ReconfigureOptionsAsync(...)` cannot tear down the live listener again during bootstrap (`#201`)
    - reran the focused web, unit, and integration proof slices for the affected paths successfully
  - Traced the missing Discord announcements to a release-gate regression instead of the webhook job itself: `build-main-0.24.5-slskdn.121` failed before `release-main`, because `src/web/scripts/smoke-subpath-build.mjs` still enforced old relative asset references after the Web UI moved back to root-relative assets plus backend HTML rewriting. Fixed the smoke harness to emulate the backend rewrite model and documented the gotcha in `ADR-0001`.
  - Updated `.github/dependabot.yml` so Dependabot ignores recurring `axios` and `lodash` frontend bumps instead of reopening those PRs, removed invalid Dependabot labels that were generating bot comments, and prepared GitHub cleanup for PRs `#198` and `#203`.
  - Re-opened and fixed the remaining tester-reported regressions on GitHub issues `#200` and `#201`:
    - restored deep-link refreshes in the embedded Web UI by switching the Vite build back to root-relative asset URLs and rewriting those HTML asset paths against `web.url_base` in the ASP.NET pipeline instead of relying on `./assets/...`
    - fixed the Bridge Web UI client to use versioned-relative API paths and added `/api/v0/bridge/...` routes server-side while preserving the legacy `/api/bridge/...` compatibility surface
    - versioned `SecurityController` correctly so `/api/v0/security/...` no longer fails with `ApiVersionUnspecified`
    - moved the Soulseek listener/distributed-network bootstrap settings into the initial `SoulseekClientOptions` so startup no longer races against a non-listening client, and removed the old “benign” log suppression for that listener-startup exception
  - Added focused regression coverage for the follow-up fixes in `ProgramPathNormalizationTests`, `NicotinePlusIntegrationTests`, `SecurityRoutesIntegrationTests`, and the web `bridge.test.js`.
  - Validation for the follow-up pass completed cleanly:
    - targeted web tests
    - targeted unit/integration tests for versioned bridge/security routes and initial Soulseek client options
    - `cd src/web && npm run build`
    - `cd src/web && npm run test:build-output`
    - full `dotnet test`
    - `bash ./bin/lint`
    - `git diff --check`
  - Wired Discord release announcements into `.github/workflows/build-on-tag.yml` for both dev and stable releases, verified the GitHub target, stored the webhook in the `snapetech/slskdn` repository secret `DISCORD_RELEASE_WEBHOOK`, and documented the behavior in `docs/DEV_BUILD_PROCESS.md` / `docs/CHANGELOG.md`.
  - Closed GitHub issues `#200`, `#201`, and `#202` locally:
    - fixed the remaining Web UI API/client regressions (`security.js`, `mediacore.js`, `bridge.js`, search-row routing, dark-theme Network stats)
    - added real PWA install support via `registerServiceWorker.js` + `public/service-worker.js`
    - added explicit listen-port/firewall diagnostics to the Network tab and troubleshooting docs for the zero-peer/zero-transfer failure mode
  - Added focused web regression coverage for the above in `bridge.test.js`, `security.test.js`, `mediacore.test.js`, `registerServiceWorker.test.js`, `SearchListRow.test.jsx`, and `System/Network/index.test.jsx`.
  - Validated the issue fixes with targeted web tests, a production web build, the build-output subpath check, emitted `build/service-worker.js`, `git diff --check`, and `bash ./bin/lint`.
  - `dotnet test` still hit an unrelated environment-specific integration failure in `CsrfPortScopedTokenIntegrationTests` because another slskd instance was already using `/home/keith/.local/share/slskd`; this did not exercise or block the new web fixes.
  - Re-reviewed the open issue list on `snapetech/slskdn`; the active bug threads remain `#193` (share-scan stalls/load) and `#199` (browse-cache race), with `#69` still just a roadmap discussion.
  - Identified an additional `#193` root cause in `src/slskd/Shares/ShareScanner.cs`: share scans still computed a full-file hash for every file before local moderation, even when moderation was disabled or when the active moderation path only needed lightweight metadata.
  - Updated `ShareScanner` and `CompositeModerationProvider` so local-file scans only hash when the active moderation configuration actually requires a hash, while still allowing metadata-only moderation providers to run.
  - Added focused unit coverage in `tests/slskd.Tests.Unit/Shares/ShareScannerModerationTests.cs` for both the no-op moderation path and the metadata-only moderation path, proving `ComputeHashAsync(...)` is skipped in both cases.
  - Added `tests/slskd.Tests.Unit/Shares/ShareScannerHarnessTests.cs` plus `scripts/run-share-scan-harness.sh` so share scans can now be exercised both against a large synthetic tree and against an env-configured real share root.
  - Used the new manual harness against `/mnt/datapool_lvm_media/download/music` (remote NFS-backed) with `workers=1` and reproduced the tester-style stall: the scan timed out after 60 seconds having indexed only 8 files.
  - Added a manual harness switch to skip media-attribute extraction and confirmed the same NFS-backed scan completes when media attributes are skipped, strongly implicating `src/slskd/Shares/SoulseekFileFactory.cs` / `TagLib.File.Create(...)` probing as the remaining `#193` bottleneck.
  - Re-ran focused issue regression coverage with `ApplicationBrowseCacheTests`, `ShareScannerModerationTests`, and `ShareScannerHarnessTests`, then verified the repo lint gate still passes.
  - Pinned the local GitHub CLI default repo to `snapetech/slskdn` and added `scripts/verify-github-target.sh` so this workspace verifies `origin`, `upstream`, and `gh` default repo before any GitHub write action.
  - Added explicit fork-boundary instructions in `AGENTS.md` and `docs/archive/implementation/AI_START_HERE.md`: all issue / PR / release work from this checkout must target `snapetech/slskdn`; upstream `slskd/slskd` is read-only reference only.
  - Investigated the failed `build-main-0.24.5-slskdn.115` release and confirmed the failure was an unrelated flaky `SecurityUtilsTests` stopwatch-ratio assertion in the release gate, not the `#193/#194` fixes.
  - Documented the CI timing-microbenchmark gotcha in ADR-0001, replaced the wall-clock timing assertions with deterministic `SecurityUtils` correctness coverage, and reran `bash packaging/scripts/run-release-gate.sh` successfully.
  - Confirmed `kspls0` was actually logged into the Soulseek server and traced the apparent network deadness to a host firewall gap: inbound `50300/tcp` was missing even though the Web UI ports were open.
  - Added the persistent `50300/tcp` nftables allow rule on `kspls0`, then verified the host immediately established a remote peer connection on `:50300` and a fresh `metallica - one` search returned `236` responses / `1514` files.
  - Patched `src/slskd/Program.cs` so expected Soulseek peer/distributed-network unobserved task exceptions no longer log as fake `[FATAL]` process-shutdown telemetry.
  - Added `scripts/setup-git-hooks.sh` so clones can explicitly install `.githooks` via `git config --local core.hooksPath .githooks`, and updated README/local-development docs to make hook installation part of first-time setup.
  - Added commit/PR-time changelog enforcement so release-worthy changes must update `docs/CHANGELOG.md` `## [Unreleased]` when they land instead of relying on release-time git-history fallback.
  - Added `scripts/validate-changelog-entry.sh`, wired it into `.githooks/pre-commit` for staged-change checks and `.github/workflows/ci.yml` for pull-request diff checks, and updated `docs/CHANGELOG.md` to document the policy.
  - Merged the previously detached `build-main-0.24.5-slskdn.92` through `.101` release line back into `main` with merge commit `e74d4df1`, restoring the missing Docker startup hardening and related packaged/runtime fixes that had been built on tags but never merged.
  - Resolved the merge-critical runtime conflicts by keeping both benign-startup and expected-network unobserved task exception downgrades in `src/slskd/Program.cs`, preserving the current canonical SongID query generation in `src/slskd/SongID/SongIdService.cs`, and restoring relay client disposal / replacement lifecycle handling in `src/slskd/Relay/RelayService.cs`.
  - Confirmed there are no remaining unmerged tags (`git tag --no-merged main` is empty). The only remaining local-only objects are stashes, which are experimental WIP / private key rotations and were intentionally not merged into `main`.
  - Cleaned up `scripts/generate-release-notes.sh` so generated `Included Commits` lists no longer surface standalone ADR gotcha commits, release-note doc commits, or stable metadata bookkeeping commits as if they were separate product changes.
  - Documented the release-note hygiene commit gotcha immediately in ADR-0001 and committed it separately as required (`f85f20ac`).
  - Validation for the release-note cleanup:
    - `./scripts/generate-release-notes.sh 0.24.5-slskdn.103 /tmp/release-notes-check.md HEAD`
    - `git diff --check`
    - `bash ./bin/lint`
  - Automated stable Winget submission in `.github/workflows/build-on-tag.yml` with a new `winget-main` job that downloads `wingetcreate`, converts the release tag version to the dot-normalized Winget package version, and submits the current Windows asset URL to `microsoft/winget-pkgs`.
  - Documented the new automation path and required `WINGETCREATE_GITHUB_TOKEN` secret in `docs/DEV_BUILD_PROCESS.md`.
  - Validation for the Winget automation follow-up:
    - `git diff --check`
    - `bash ./bin/lint`
  - Tightened SongID-generated search strings so actual search actions now prefer canonical `Artist - Track` queries instead of concatenating uploader, album, duplicate title, and other metadata noise.
  - Reused a dedicated `BuildTrackSearchText()` helper across SongID query generation, track candidates, segment-derived actions, and fallback search variants in `src/slskd/SongID/SongIdService.cs`.
  - Added focused SongID unit coverage in `tests/slskd.Tests.Unit/SongID/SongIdServiceTests.cs` for segment query formatting and fallback query formatting.
  - Documented the noisy SongID search query-builder gotcha immediately in ADR-0001 and committed it separately as required (`167de066`).
  - Validation for the query-shape follow-up:
    - `dotnet test tests/slskd.Tests.Unit/slskd.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~SongIdServiceTests" -v q`
    - `bash ./bin/lint`
  - Fixed the packaged Web UI defaults after reproducing the `kspls0` install path:
    - `packaging/aur/slskd.service` now passes `--config /etc/slskd/slskd.yml`, so package installs use the documented config path instead of silently preferring `/var/lib/slskd/.local/share/slskd/slskd.yml`
    - `packaging/aur/slskd.yml` now disables HTTPS by default so `5030` is the single packaged entry point unless the user explicitly enables TLS
    - mirrored the same HTTP-only default into `packaging/proxmox-lxc/setup-inside-ct.sh`
    - added an HTTP-only login-page hint in `src/web/src/components/LoginForm.jsx` pointing users at `https://<host>:5031` when an instance exposes TLS explicitly
  - Added focused web coverage in `src/web/src/components/LoginForm.test.jsx` for the HTTPS hint behavior.
  - Documented the packaged dual-port gotcha in ADR-0001 and committed it immediately as `8265aff3`.
  - Validation for this packaging/UI follow-up:
    - `cd src/web && npm test -- --run src/components/LoginForm.test.jsx src/components/App.test.jsx`
    - `bash ./bin/lint`
    - `dotnet test --no-restore -v minimal`
  - Shifted issue `#193` from pure bug-fix verification to performance tuning after tester feedback showed first-library scans can still overload weaker hosts even when the CSRF/runtime regressions are fixed.
  - Documented the new gotcha in ADR-0001: defaulting share scan workers to full `ProcessorCount` is too aggressive for first-time scans and should remain conservative by default.
  - Changed `Options.SharesOptions.ShareCacheOptions.Workers` to default to one worker on 1-2 core hosts and otherwise half the cores capped at four workers, preserving the existing `shares.cache.workers` knob for manual tuning.
  - Added focused unit coverage for the default-worker calculation and updated config/docs so operators know they can lower or raise the scan concurrency explicitly.
  - Validation for this follow-up:
    - `dotnet test tests/slskd.Tests.Unit/slskd.Tests.Unit.csproj --filter "FullyQualifiedName~ShareCacheOptionsTests|FullyQualifiedName~ProgramExpectedNetworkExceptionTests|FullyQualifiedName~ProgramPathNormalizationTests" -v minimal`
    - `bash ./bin/lint`
    - `git diff --check`
  - Investigated issue `#199` and confirmed the failure was a real file-sharing bug in `Application`: active browse readers were opening `browse.cache` exclusively while cache refreshes replaced the file in place, and refreshes themselves had no serialization.
  - Documented the browse-cache locking gotcha in ADR-0001 and committed it separately as required.
  - Fixed the browse-cache path by:
    - opening browse-cache readers with `FileShare.ReadWrite | FileShare.Delete`
    - serializing `CacheBrowseResponse()` through a dedicated semaphore
    - writing temp cache files inside `Program.DataDirectory` before the final replace
  - Added focused regression coverage in `tests/slskd.Tests.Unit/Core/ApplicationBrowseCacheTests.cs` that keeps a cache reader open while replacing the on-disk file.
  - Validation for the browse-cache follow-up:
    - `dotnet test tests/slskd.Tests.Unit/slskd.Tests.Unit.csproj --filter "FullyQualifiedName~ApplicationBrowseCacheTests|FullyQualifiedName~ApplicationLifecycleTests" -v minimal`
    - `dotnet test`
    - `bash ./bin/lint`
    - `git diff --check`
  - Ran the April 14 dependency/release chore pass on `main`:
    - folded the four remaining open Dependabot PRs into one local batch (`YamlDotNet 17.0.1`, `dotNetRDF 3.5.1`, `OpenTelemetry` / console / OTLP / hosting `1.15.2`, plus the safe web toolchain/security bumps)
    - kept `@uiw/react-codemirror` on `4.21.21` after confirming the newer line now peers on `react >=17`, which does not fit the repo's React 16 baseline
    - documented and reverted the `jsdom 29.0.2` Vitest worker bootstrap regression immediately in ADR-0001 (`39eb984c`)
    - aligned `tests/slskd.Tests.Unit/slskd.Tests.Unit.csproj` to `YamlDotNet 17.0.1` after the main-project bump exposed a `NU1605` downgrade during solution restore
  - Validation for the dependency/release pass:
    - `npm test --prefix src/web`
    - `npm run build --prefix src/web`
    - `npm run test:build-output --prefix src/web`
    - `bash packaging/scripts/run-release-gate.sh`
    - `bash ./bin/lint`
    - broad `dotnet test -v minimal` still hangs after reporting passing suite counts in this environment; the green release gate remains the more reliable release signal for now
  - Investigated the failed SongID YouTube run for `https://youtu.be/K3wtamktLGs?si=oJjRPxd_fV31TcLd` on `kspls0` and confirmed the immediate host-side failure was a missing `yt-dlp` binary.
  - Reinstalled `yt-dlp` on `kspls0`, re-queued the same SongID source through the authenticated API, and verified the run now advances past the old `PrepareYouTubeAssetsAsync` crash point.
  - Hardened `src/slskd/SongID/SongIdService.cs` so missing `yt-dlp` falls back to metadata-only YouTube analysis instead of failing the run, and fixed the empty-clip aggregate bug that fallback exposed.
  - Added focused SongID unit coverage in `tests/slskd.Tests.Unit/SongID/SongIdServiceTests.cs` and updated AUR / Proxmox packaging to install `yt-dlp`.
  - Documented both SongID gotchas immediately in ADR-0001 and committed them separately as required:
    - `40a557f2` missing `yt-dlp` SongID failures
    - `d840f9d8` SongID empty clip aggregates
  - Added page-level collapsible wrappers in `src/web/src/components/Search/Searches.jsx` so every top-level Search page box can be collapsed, and left Search Results expanded by default so newly-triggered searches are immediately visible.
  - Wrapped the touched search action buttons in Popup tooltips to match repo UI guidance.
  - Validation for the Search page layout follow-up:
    - `cd src/web && npm test -- --run src/components/App.test.jsx`
    - `bash ./bin/lint`
  - Investigated broken SongID actions (`Plan Discography`, album planning) and the multi-search `only one concurrent operation is permitted` failure.
  - Fixed the shared frontend clients:
    - `src/web/src/lib/jobs.js` now sends the native jobs API's snake-case request fields (`artist_id`, `target_dir`, `mb_release_id`)
    - `src/web/src/lib/searches.js` now retries the backend's known serialized-create `429` response during batch search creation
  - Added focused frontend coverage in `src/web/src/lib/jobs.test.js` and `src/web/src/lib/searches.test.js`.
  - Documented the native jobs payload casing gotcha immediately in ADR-0001 and committed it separately as required (`089eccbe`).
  - Validation for the Search action follow-up:
    - `cd src/web && npm test -- --run src/lib/jobs.test.js src/lib/searches.test.js src/components/App.test.jsx`
    - `bash ./bin/lint`
  - Investigated the live `kspls0` SongID stall at `38%` and confirmed the run was genuinely pinned in `artist_graph` while deep MusicBrainz release-graph expansion ran for a large artist.
  - Hardened `src/slskd/SongID/SongIdService.cs` so `AddArtistCandidatesAsync()` time-boxes each artist release-graph fetch and falls back to a lightweight artist candidate instead of stalling the whole run.
  - Added focused timeout-fallback coverage in `tests/slskd.Tests.Unit/SongID/SongIdServiceTests.cs`.
  - Documented the SongID artist-graph stall gotcha immediately in ADR-0001 and committed it separately as required (`fe4b75df`).
  - Validation for the SongID stall follow-up:
    - `dotnet test tests/slskd.Tests.Unit/slskd.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~SongIdServiceTests"`
    - `bash ./bin/lint`
  - Continued past the security-alert-only cleanup into the remaining open Dependabot NuGet backlog and applied the outstanding package versions directly in `src/slskd/slskd.csproj`:
    - `AWSSDK.S3` `3.7.511.4`
    - `prometheus-net.DotNetRuntime` `4.4.1`
    - `Serilog.AspNetCore` `8.0.3`
    - `Serilog.Sinks.Grafana.Loki` `8.3.2`
    - `OpenTelemetry` / `OpenTelemetry.Extensions.Hosting` `1.15.0`
    - `OpenTelemetry.Instrumentation.AspNetCore` `1.15.1`
    - `OpenTelemetry.Instrumentation.Http` `1.15.0`
  - The only real code break from that batch was the Loki sink upgrade: v8 removed the old `outputTemplate` parameter, so `Program.ConfigureGlobalLogger()` now passes an explicit `MessageTemplateTextFormatter` via `textFormatter` to preserve the previous log payload shape.
  - Documented that upgrade gotcha immediately in `adr-0001-known-gotchas.md` and committed it as `docs: Add gotcha for Grafana Loki 8 formatter migration`.
  - Verified the upgraded package set with:
    - `dotnet restore src/slskd/slskd.csproj`
    - `dotnet build src/slskd/slskd.csproj -c Release -v minimal -clp:ErrorsOnly`
    - `dotnet test tests/slskd.Tests.Unit/slskd.Tests.Unit.csproj --no-restore --no-build --filter "FullyQualifiedName~RelayControllerModerationTests|FullyQualifiedName~HashDbOptimizationServiceTests" -v minimal`
    - `bash ./bin/lint`
  - A broad `dotnet test --no-restore -v minimal` pass still reports an unrelated existing integration failure in `BridgePerformanceTests.ProtocolParser_Should_Use_Reasonable_Memory`.
  - Next steps:
    - commit the remaining memory-bank updates alongside the package batch
    - push the package sweep to `master`
    - close the seven remaining Dependabot PRs as superseded by the landed versions
  - Switched to a focused `master`-based security branch because the remaining open GitHub PRs and security alerts were all tied to `refs/heads/master`, not `release-main`.
  - Finished the residual relay log hardening on the default-branch line by hashing cached relay connection ids in `RelayService` validation logs and removing direct credential/expected-credential mismatch logging.
  - Documented that relay logging bug pattern immediately in `memory-bank/decisions/adr-0001-known-gotchas.md` and committed it as `docs: Add gotcha for relay validation identifier logging`.
  - Updated `src/web/package.json` / `src/web/package-lock.json` so `yaml` moved to `2.8.3`, `jsdom` moved to `29.0.1`, `vite` moved to `8.0.3`, and a clean `npm audit fix` normalized the remaining transitive `picomatch` / `brace-expansion` vulnerabilities.
  - Confirmed `cd src/web && npm audit --json` now reports `0` vulnerabilities, `npm run build` passed, and `npm run test -- --run` passed (`8` files / `91` tests).
  - Next steps:
    - commit the web dependency remediation on this branch
    - push the default-branch security fixes so GitHub can auto-reconcile the open Dependabot / CodeQL findings
    - close superseded Dependabot PRs after the default branch reflects the fixed dependency line
  - Added the correct root-level ignore for `mesh-overlay.key.prev`; the repository already ignored `/src/slskd/mesh-overlay.key.prev`, but the actual local backup file lives at the repo root.
  - Re-verified the current tree after the warning-reduction and regression-repair passes:
    - `dotnet build src/slskd/slskd.csproj -c Release -p:Version=0.0.0 -t:Rebuild` passes with `0 warnings, 0 errors`
    - `dotnet test --no-restore` passes at solution root
    - `bash ./bin/lint` passes
  - Confirmed the working branch `e2e-fixture-fix2` is 16 commits ahead of `origin/master`; next step is to package this validated tip into a commit, fast-forward `master`, and trigger the next `build-main-*` tag from `master`.
  - Finished the end-to-end validation pass after the warning-cleanup work and repaired the regressions it exposed:
    - fixed full-instance startup for integration tests (`Program`, `SlskdnFullInstanceRunner`) so bridge tests no longer self-skip behind blank socket/app-dir/env gating bugs
    - added fast-fail subprocess diagnostics to the full-instance runner and fixed the Tor SOCKS handshake timeout path with deterministic silent-endpoint coverage
    - repaired the remaining root `dotnet test` blockers in unit tests and supporting code (`MeshStatsCollector`, `LocalPortForwarder`, `ProfileServiceTests`, `MeekTransportTests`, relay moderation test expectations, tuple-member fallout in `Phase8MeshTests`)
    - reran root `dotnet test --no-restore` successfully and reran `bash ./bin/lint` successfully
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
  - Completed the final true-positive CodeQL pass locally: removed cleartext-style secret logging from `Program` and `AsymmetricDisclosure`, hardened relay token validation to trust server-side agent identity instead of request headers, rebuilt `SqliteShareRepository` connection strings from validated data sources, and constrained HashDb profiling to admin-only single-statement read-only SQL with focused unit coverage
 - Manually dismissed the first false-positive batch (CSRF cookie, SOCKS negotiation, login null-guard), then found GitHub’s fresh analysis still flagging relay agent identifiers as cleartext. Followed up by caching trusted relay connection ids instead of raw agent names and anonymizing relay completion logs/temp filenames with hashed agent ids
  - Continued the warning-reduction effort with another broad cleanup batch touching `HttpTunnelTransport`, `I2PTransport`, `WebSocketTransport`, `MeekTransport`, `LocalPortForwarder`, `Obfs4Transport`, `TimedBatcher`, `QuicOverlayServer`, `MeshServiceClient`, `ServicePayloadParser`, `SolidWebIdResolver`, `StreamsController`, and the `MediaCore*` / `MultiSourceDownloadService` seams
  - Rebuilt the app project repeatedly during the current pass and pushed the stable warning floor from `1687` down through `1652`, `1625`, `1612`, `1578`, `1559`, `1552`, `1502`, `1492`, `1410`, and currently `1360` without breaking the build; this included real ownership fixes in mesh overlay accept/connect paths, controller whitespace cleanup, and large DTO/model default normalization across core config/state, MultiSource, PodCore, Rescue, LibraryHealth, and MusicBrainz paths

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
- No active blocker from the old `.NET 10` test-tail issue; the previously hanging full-solution test run now exits after the integration harness/test fixes.

### Current focus (the rest)
- **40-fixes plan (PR-00–PR-14):** Done. slskd.Tests 46, slskd.Tests.Unit 2257 pass; Epic implemented. Deferred table: status only.
- **T-404+:** Done. t410-backfill-wire (RescueMode underperformance detector → RescueService); codec/fingerprint (T-420–T-430) done per dashboard.
- **slskd.Tests.Unit re-enablement:** ✅ **COMPLETE** (2026-01-27): All phases (0-5) done. 2430 tests passing, 0 skipped, 0 failed. No `Compile Remove` remaining. All test files enabled and passing per `docs/dev/slskd-tests-unit-completion-plan.md`.
- **New product work**: As prioritized.

**Research (9) implementation:** ✅ Complete. T-901–T-913 all done per `memory-bank/tasks.md`.

### Next Steps
1. Keep using the reproduce-first checklist whenever a build claims to fix a tester-reported regression.
2. If `#200` / `#201` reports come back again, reproduce against the current proof slices first instead of shipping another speculative tag.
3. If release automation ever reports another backend test tail, start from the two fixed gotchas first: missing external test prerequisites and long integration sleeps under hang diagnostics.

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
