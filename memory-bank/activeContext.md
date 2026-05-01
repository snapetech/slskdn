## Update 2026-05-01 18:55:00Z

- Current task: Current repo code is built and running on `kspls0` for user testing.
- Last activity:
  - stopped `slskd.service` on `kspls0`
  - fixed stale obfuscation-only validation tests and documented ADR-0001 gotcha `0z266`
  - built and published `0.0.0-slskdn.manual.20260501185541.9d639c7d3.dirty` for Linux x64 after the vendored runtime hardening changes landed in the checkout
  - deployed `/usr/lib/slskd/releases/manual-20260501185541.9d639c7d3.dirty` on `kspls0`
  - corrected the stable launcher so it forwards `"$@"` and systemd keeps using `/etc/slskd/slskd.yml`; documented ADR-0001 gotcha `0z267`
  - verified live API runtime path, `/etc/slskd/slskd.yml`, Soulseek login, shares ready, DHT bootstrap, Web UI HTTP, and listeners on `5030`, `50300`, `50301`, and `50305`
  - validation passed: full `bin/publish --runtime linux-x64`, `bash ./bin/lint`
- Next steps:
  1. User testing on `http://kspls0:5030/`.
  2. Push local commits when ready.

## Update 2026-05-01 18:19:00Z

- Current task: Shared DHT/QUIC UDP demux and QUIC overlay restoration are complete locally and deployed to `kspls0`.
- Last activity:
  - changed the public mesh/DHT/QUIC overlay to use shared UDP `50305` with MsQuic listening on loopback backend UDP `55305`
  - restored QUIC overlay default enablement and advertised public QUIC port `50305`
  - added the shared UDP listener/proxy and tests for DHT routing, QUIC packet routing, and QUIC endpoint selection
  - fixed the multi-address wildcard UDP source-IP bug by binding per local IPv4 address and documented it as ADR-0001 gotcha `0z262`
  - updated `kspls0` host nftables and VPN ingress compact UDP configuration from stale `50306`/`50400` to shared UDP `50305`
  - deployed `/usr/lib/slskd/releases/manual-quicshare-20260501181348` on `kspls0`
  - verified live LAN QUIC handshake to `kspls0:50305`, live shared UDP sockets, DHT startup, QUIC backend startup, and overlay DHT announcement
  - live API search smoke ran but returned no responses, so no weak download candidate was queued
  - validation passed: `bash ./bin/lint`, Web UI App tests, Web UI production build, focused backend tests, full `dotnet test --no-restore`
- Next steps:
  1. Commit and push the shared DHT/QUIC port work.
  2. Trigger a tag build only if a release is intentionally requested.

## Update 2026-05-01 18:08:00Z

- Current task: Launchpad SFTP PPA upload setup is complete on GitHub and blocked on Launchpad account key registration.
- Last activity:
  - generated `~/.ssh/slskdn_launchpad_ppa_ed25519`
  - stored `LAUNCHPAD_SFTP_KEY` and `LAUNCHPAD_SFTP_USER` in `snapetech/slskdn` GitHub repository secrets
  - updated both PPA workflows to prefer IPv4-pinned SFTP when the key is configured and retain anonymous FTP as fallback
  - documented the SFTP-preferred PPA upload, SFTP IPv4-pinning, and SFTP auth-hang gotchas as separate commits
  - added non-interactive SSH options, a bounded SFTP auth probe, and a bounded `dput` upload timeout after the first IPv4 SFTP upload retry hung inside `ssh`
  - reran the standalone PPA workflow for `2026050100-slskdn.215`; GitHub reached Launchpad SFTP over IPv4 and failed fast with `Permission denied (publickey)`
  - tested obvious local private keys against Launchpad SFTP; none completed a noninteractive login
- Next steps:
  1. Register `~/.ssh/slskdn_launchpad_ppa_ed25519.pub` on the `~keefshape` Launchpad account.
  2. Rerun the standalone PPA workflow for `2026050100-slskdn.215` after Launchpad accepts the key.

## Update 2026-05-01 17:35:00Z

- Current task: slskdN runtime vendoring, static ingress notice cleanup, Docker Hub release-channel work, and `kspls0` deployment are complete.
- Last activity:
  - vendored `slskNet.Runtime` under `vendor/slskNet.Runtime` and moved app/test project references to the in-repo project
  - corrected the ingress-port migration notice to a static five-old-forwards versus two-current-forwards list with no public IPs, active/not-reported status, or obfuscation listener row
  - documented the static-notice gotcha immediately in ADR-0001 as `e83d0c396`
  - rebuilt the Web UI bundle locally and started full `bin/build`; frontend tests, frontend production build, backend build, unit tests, and API tests passed before the integration test run was interrupted by the user
  - added Docker Hub tags to release Docker jobs and configured `DOCKERHUB_USERNAME` / `DOCKERHUB_TOKEN` in `snapetech/slskdn`
  - reran full `bin/build`; Web UI tests/build, backend build, unit tests, API tests, and integration tests passed
  - deployed `/usr/lib/slskd/releases/manual-portnotice-20260501174927` to `kspls0`
  - deployed `/usr/lib/slskd/releases/manual-slsknet-repo-20260501173428` to `kspls0`
  - confirmed live asset text contains the reduced-port copy and no active/not-reported status, public endpoint text, `50301`, `PREVIOUS`, or `CURRENT`
  - confirmed live listeners on `50300/tcp`, `50301/tcp`, and `50305/tcp+udp`
  - live-smoked Soulseek search and a small download; the download completed successfully
  - updated `bin/lint` to exclude vendored runtime source from slskdN formatting enforcement while keeping the project reference for builds
- Next steps:
  1. Commit and push the corrected static ingress notice plus remaining release-channel cleanup.
  2. Trigger the next `build-main-*` tag only when a release build is intentionally wanted.

## Update 2026-05-01 17:30:00Z

- Current task: Launchpad SFTP PPA upload setup is in progress.
- Last activity:
  - generated `~/.ssh/slskdn_launchpad_ppa_ed25519`
  - stored `LAUNCHPAD_SFTP_KEY` and `LAUNCHPAD_SFTP_USER` in `snapetech/slskdn` GitHub repository secrets
  - updated both PPA workflows to prefer IPv4-pinned SFTP when the key is configured and retain anonymous FTP as fallback
  - documented the SFTP-preferred PPA upload, SFTP IPv4-pinning, and SFTP auth-hang gotchas as separate commits
  - added non-interactive SSH options, a bounded SFTP auth probe, and a bounded `dput` upload timeout after the first IPv4 SFTP upload retry hung inside `ssh`
- Next steps:
  1. Validate workflow YAML and run the standalone PPA upload workflow for `2026050100-slskdn.215`.
  2. Register the generated public key on the `~keefshape` Launchpad account if SFTP authentication rejects it.

## Update 2026-05-01 17:17:00Z

- Current task: slskdN is switched to `slskNet.Runtime` and deployed on `kspls0`.
- Last activity:
  - changed slskdN app/test project references to use the sibling `../slskNet.Runtime` runtime project
  - wired `soulseek.obfuscation` options into `SoulseekClientOptions` and `SoulseekClientOptionsPatch`
  - changed type-1 obfuscation runtime reporting from pending to active
  - published a `linux-x64` self-contained artifact and installed it as `/usr/lib/slskd/releases/manual-slsknet-runtime-20260501171217`
  - restarted `slskd.service`; confirmed Soulseek login, regular listener `50300`, obfuscated listener `50301`, and successful live search/download smoke
  - validated targeted startup/options tests, transfer tests, full unit suite, and repo lint
- Next steps:
  1. Commit the slskNet.Runtime wiring separately from the pre-existing PPA workflow changes if this branch is being prepared for review.
  2. If we want CI to build this without a sibling checkout, publish `slskNet.Runtime` to a private package feed or add a documented submodule/source-fetch step.

## Update 2026-05-01 16:09:39Z

- Current task: PPA reachability fix is complete locally.
- Last activity:
  - confirmed the successful `.197` PPA release used Launchpad anonymous FTP, so SFTP/SSH secrets were not the historical requirement
  - confirmed local and `kspls0` TCP reachability to `ppa.launchpad.net:21`
  - documented the IPv4 reachability gotcha in ADR-0001 and committed it separately
  - updated `build-on-tag.yml` and `release-ppa.yml` to pin `ppa.launchpad.net` to a resolved IPv4 address
  - added a Python TCP preflight so runner logs fail early if Launchpad FTP is unreachable
  - replaced the `dput` transfer with signed source verification plus bounded anonymous `curl` FTP uploads after IPv4-pinned `dput` reached Launchpad but failed during passive data transfer
- Validation:
  - Python TCP connect to Launchpad FTP passed locally
  - PyYAML parsed both edited workflows
  - `git diff --check`
- Next steps:
  1. Push the workflow fix and rerun the standalone PPA workflow for `2026050100-slskdn.215` to verify upload.

## Update 2026-05-01 03:30:00Z

- Current task: Obsolete slskdn-dev package channel cleanup is complete locally.
- Last activity:
  - reconciled completed QR invite display/scanning, DHT adaptive bootstrap diagnostics, native MilkDrop3 T-938, SongID parity notes, analyzer cleanup, and production placeholder burn-down tasks
  - removed the active `slskdn-dev` package channel instead of keeping the blocked re-enable task
  - stripped `build-dev-*` handling and dev package jobs from the tag release workflow
  - removed stale dev-only package manifests, docs, and release-note template surfaces
  - reduced the production placeholder scan to the allowed `FeatureNotImplementedException` gate infrastructure
  - cleared stale MeshSync test wording after the unavailable-transport message cleanup
  - restored `/tmp` test capacity by deleting generated `/tmp/slskd_*.dmp` files before rerunning the broad suite
- Validation:
  - `bash packaging/scripts/validate-release-copy.sh`
  - `bash packaging/scripts/validate-packaging-metadata.sh`
  - `git diff --check`
  - `npm test -- Contacts.test.jsx`
  - `npm run lint`
  - `bash ./bin/lint`
  - `dotnet format --verify-no-changes --no-restore --verbosity minimal`
  - `dotnet test --filter "FullyQualifiedName!~OptionalLiveAccounts_CanSearchAndDownloadHostedProbeOverOverlayMesh" --no-restore`
- Next steps:
  1. Preserve concurrent player/docs changes in the dirty tree; do not revert them during follow-up cleanup.

## Update 2026-05-01 03:20:30Z

- Current task: MilkDrop/Butterchurn compact player tile is deployed and live-smoke verified on `kspls0`.
- Last activity:
  - confirmed the live app serves `index-B9qp0NHC.js` and `index-DwvOKuMB.css`
  - added native MilkDrop WebGPU-to-WebGL2 fallback behavior for browsers without `navigator.gpu`
  - verified the live systemd process uses `/etc/slskd/slskd.yml` and listens on `0.0.0.0:5030`
  - ran headless Playwright against `http://kspls0:5030/playlist-intake`
  - confirmed Butterchurn, MilkDrop3 WebGL2, MilkDrop3 WebGPU, and WebGL2 retry all show visible visualizer canvases without analyzer fallback or error text
  - confirmed the compact visualizer controls remain a single 112x18 px row
  - captured nonblank tile screenshots for Butterchurn, native WebGL2, and native WebGPU
  - documented and committed the WebGPU fallback gotcha as `dd7f0143a`
  - validated focused Player visualizer tests and frontend lint
- Next steps:
  1. Hard refresh `/playlist-intake` if a browser tab is holding an older service-worker cache.
  2. Continue unrelated production placeholder burn-down only after preserving the current visualizer state.

## Update 2026-05-01 03:22:00Z

- Current task: Production placeholder burn-down is in progress.
- Last activity:
  - added `docs/dev/placeholder-completion-plan-2026-05-01.md`
  - scoped the burn-down to production markers while excluding UI input
    placeholders, tests, generated assets, archive docs, and historical
    memory-bank notes
  - started P1 by removing fake hard-coded swarm analytics efficiency values
  - added unit coverage for derived efficiency metrics and empty trend output
  - cleaned the consensus policy success reason and added coverage for
    consensus-required denial versus consensus-not-required allowance
- Next steps:
  1. Validate the focused consensus policy tests.
  2. Continue with intentional disabled-feature cleanup and Mesh/Service Fabric
     streaming markers after the current dirty obfuscation lane settles.

## Update 2026-05-01 02:56:49Z

- Current task: Soulseek type-1 obfuscation default posture and README coverage are complete locally.
- Last activity:
  - changed `soulseek.obfuscation.enabled` to default `true`
  - kept default mode as `compatibility`, with `advertise_regular_port: true` and `prefer_outbound: true`
  - renamed the current runtime state to `configured_pending_runtime`
  - changed startup logging from warning to informational because the default mode preserves normal fallback and is currently a no-op until runtime support lands
  - added top-level README and docs-index entries for the feature
  - updated config docs, example YAML, and the dedicated guide to document default-on compatibility semantics
  - validated focused backend and Network tab tests, repo lint, and touched-file whitespace checks
- Next steps:
  1. Runtime activation still needs Soulseek.NET hooks or a slskdN-owned peer-message transport adapter.
  2. Keep `only` mode explicit and non-default because it can break clients that ignore obfuscated metadata.

## Update 2026-05-01 03:05:00Z

- Current task: Feature-expansion documentation hygiene is complete locally.
- Last activity:
  - fixed the documentation audit so the tracked current-doc broken-link list is
    marked resolved instead of still actionable
  - added historical snapshot banners to dated test/status planning docs
  - renamed pod-scoped tunnel docs in public prose to Pod Private Service
    Gateway and distinguished them from the host VPN agent
  - added focused host VPN agent companion guides for Linux/WireGuard, external
    tunnels, Windows/macOS, and the API contract
  - added the Pod Private Service Gateway guide to the docs index
  - verified targeted stale-link and stale-audit scans
- Next steps:
  1. Keep historical phase/audit docs clearly marked when they are retained in
     current paths.

## Update 2026-05-01 02:41:29Z

- Current task: Soulseek type-1 obfuscation feature options are integrated locally.
- Last activity:
  - inspected the sibling research workspace and confirmed public-server type-1 peer-message obfuscation metadata/transport is strong enough to design slskdN feature options
  - added `soulseek.obfuscation` settings for enablement, mode, dedicated port, regular fallback advertisement, and outbound preference
  - added validation and a runtime plan that reports the current Soulseek.NET activation gap as `configured_unsupported`
  - exposed the plan through the slskdN capabilities API and System -> Network status
  - updated config docs, example YAML, and the dedicated Soulseek type-1 obfuscation guide
  - validated focused backend and Network tab tests, frontend lint, repo lint, broad non-live `.NET` tests, and touched-file whitespace checks
- Next steps:
  1. Runtime activation requires Soulseek.NET hooks or a slskdN-owned peer-message transport adapter for SetWaitPort metadata, obfuscated listener accept, and obfuscated outbound dial.
  2. Keep compatibility/prefer modes as the practical defaults; only mode remains explicit because it breaks clients that ignore obfuscated metadata.

## Update 2026-05-01 02:41:03Z

- Current task: Feature-expansion public docs and README listings are updated locally.
- Last activity:
  - updated README with Acquisition Review, System admin surfaces, unified Messages, System Policies/Experience, native MilkDrop maturity, and links to new guides
  - refreshed `docs/getting-started.md` for current ports, default credentials, System tour, manual Search vs Acquisition Review, unified Messages, and conservative multi-source wording
  - added `docs/system-surfaces.md`, `docs/pods-and-rooms.md`, and `docs/songid-discovery.md`
  - updated docs index, feature overview, advanced features, config cross-links, Web UI surface audit, documentation audit, task log, and progress log
- Next steps:
  1. Treat Essentia/MIR, cover-similarity, embedding clustering, and real preset/device visualizer measurement as research/runtime scope, not documentation gaps.
  2. Remaining documentation cleanup is broad hygiene: broken-link cleanup, VPN guide splitting, and historical snapshot banners.

## Update 2026-05-01 02:20:56Z

- Current task: Remaining admin policy and experience Web UI surfaces are implemented locally.
- Last activity:
  - added System -> Policies and System -> Experience and wired them into the System tab router
  - surfaced guided YAML controls for webhooks/scripts, transfer policy, security/access, search/network policy, and retention/storage
  - included DHT LAN-only/bootstrap/ports, Scene Pod Bridge, rescue mode, HTTP rate windows, HTTPS certificate replacement, API/JWT secret replacement, transfer retry/schedule/auto-replace, and share media-probe controls
  - surfaced browser-local Search, Discovery Inbox, Player, and Messages preferences for page-specific backfill
  - kept the surfaces passive/configuration-only with no hook execution, peer contact, provider validation, daemon restart, transfer mutation, file mutation, or page behavior changes until follow-up code consumes settings
  - documented and committed the transfer YAML alias gotcha as `8cdb31d9e`
  - validated focused AdminPolicies/ExperienceSettings tests, touched AdminPolicies/ExperienceSettings/System ESLint, full Web UI lint, frontend production build, repo lint, and touched-file whitespace checks
- Next steps:
  1. Let backend lanes backfill live action execution behind these surfaces where needed.
  2. Let page-specific frontend lanes consume the browser-local Experience preferences where those pages need live behavior.
  3. Full repo `git diff --check` is still blocked by existing trailing whitespace in `docs/design/sharegroups-collections-streaming-assessment.md`.

## Update 2026-05-01 02:39:15Z

- Current task: NixOS VM smoke automation follow-up is complete locally.
- Last activity:
  - added `packaging/scripts/run-nixos-vm-smoke.sh`
  - the script builds a minimal NixOS VM around the flake package and required slskd module options
  - it boots headless with QEMU/KVM when available, waits for `slskd.service` to become active, and reports a serial success marker
  - it skips cleanly when Nix, Linux, or KVM are unavailable, with explicit TCG opt-in for slower runs
  - validated shell syntax; local execution skipped because Nix is not installed
- Next steps:
  1. Run final whitespace, lint, and broad non-live .NET validation for the accumulated backend/middle batch.
  2. Remaining unchecked entries are active visualizer work, artifact-dependent dev flake release work, live-runtime diagnostics, repo-wide analyzer debt, QR-library placeholder work, or future SongID MIR research.

## Update 2026-05-01 02:37:44Z

- Current task: Direct mesh transport runtime dependency gate is complete locally.
- Last activity:
  - registered `DirectQuicDialer` only when `QuicRuntime.IsAvailable()` succeeds
  - added an explicit startup warning when direct mesh transport is enabled but QUIC runtime support is unavailable
  - aligned `DirectQuicDialer.IsAvailableAsync()` with the same runtime gate used by descriptor publication and DI
  - marked the non-QUIC direct transport/runtime-gate task complete
  - validated focused DirectQuicDialer, PeerDescriptorPublisher, and TransportSelector tests
- Next steps:
  1. Run final whitespace, lint, and broad non-live .NET validation for the accumulated backend/middle batch.
  2. Remaining unchecked entries are active visualizer work, packaging artifact/live-runtime work, repo-wide analyzer debt, QR-library placeholder work, or future SongID MIR research.

## Update 2026-05-01 02:22:16Z

- Current task: Collection item display metadata follow-up is complete locally.
- Last activity:
  - added persisted `fileName`, `title`, `artist`, and `album` fields for collection items
  - added best-effort startup column upgrades for existing `collections.db` files
  - wired add/update requests, share manifests, and Playlist Intake collection creation to carry safe display labels
  - validated focused CollectionsController/SharingService tests and playlist-intake Web tests
- Next steps:
  1. Run final whitespace, lint, and broad non-live .NET validation for the accumulated backend/middle batch.
  2. Treat remaining unchecked entries as active visualizer, packaging artifact, live-runtime, or future research backlog unless explicitly reprioritized.

## Update 2026-05-01 02:18:47Z

- Current task: Sharegroups streaming security checklist is complete locally.
- Last activity:
  - added constant-time application-level comparisons for signed share token collection/share binding checks
  - fixed `ContentLocator` so non-advertisable repository hits cannot fall through to allowed-root `path:` fallback resolution
  - added focused regressions for tampered signatures and non-advertisable fallback bypass
  - documented and committed the gotcha as `c411d2ca9`
  - validated focused ShareToken, ContentLocator, StreamsController, and SharesController tests
- Next steps:
  1. Run final whitespace, repo lint, and broad non-live .NET validation for this batch.
  2. Remaining unchecked work is either active in another lane, artifact/live-runtime blocked, or future research backlog unless explicitly reprioritized.

## Update 2026-05-01 02:22:30Z

- Current task: Acquisition Review is corrected so it no longer intercepts active manual Search.
- Last activity:
  - removed the Search page action that saved a manual query into Discovery Inbox/Acquisition Review
  - renamed the visible Discovery Inbox nav/page heading to Acquisition Review
  - clarified UI and docs that the queue is for passive, imported, and generated candidates only
  - documented the manual-search review-loop gotcha and committed it as `4d8eb9e7e`
  - validated focused Search/Acquisition Review/App tests, frontend lint, production build, repo lint, and live `slskd` service status on `kspls0`
- Next steps:
  1. Hard refresh `/searches` and `/discovery-inbox` to pick up the new bundle names.
  2. Keep normal Search direct; use Acquisition Review only for generated/imported/passive acquisition candidates.

## Update 2026-05-01 02:12:30Z

- Current task: Unified Messages pod room/DM experience is deployed to `kspls0`.
- Last activity:
  - hid pod direct channels from Messages so Soulseek DMs are not duplicated by pod DMs
  - gave pod room channels a room-style transcript, composer, and member rail
  - kept Listen Along as a compact room-only affordance
  - pinned message-window controls to the top-right of panel headers
  - validated focused Messaging/App tests, frontend lint, production build, repo lint, and live `slskd` service status on `kspls0`
- Next steps:
  1. Hard refresh `/messages` to pick up `Messaging-BATI-J-x.js` and `Messaging-Bh-1851M.css`.
  2. If mesh-wide broadcast needs a top-level surface, build it separately from direct messages.

## Update 2026-05-01 02:17:50Z

- Current task: Remaining active transfer refusal bug is complete locally.
- Last activity:
  - added upload-side producer coverage for Soulseek `Connection refused` during upload execution
  - fixed `UploadService.UploadAsync(...)` catch paths so stale transfer snapshots do not overwrite terminal `TryFail(...)` state
  - documented and committed the gotcha as `da3632af1`
  - marked the `#201` task complete in `tasks.md`
  - validated focused upload lifecycle and network-exception tests, touched-file whitespace checks, repo lint, and the broader non-live .NET suite
- Next steps:
  1. Treat remaining unchecked entries as active visualizer work, artifact-dependent packaging, or future backlog unless the user explicitly prioritizes one of those lanes.
  2. If more coding continues immediately, avoid T-938 visualizer files while another lane owns them.

## Update 2026-05-01 02:09:06Z

- Current task: T-1400 Unified BrainzClient is complete locally.
- Last activity:
  - replaced the placeholder unified Brainz client with a real facade over MusicBrainz and AcoustID
  - registered `IBrainzClient` in DI
  - added unified release, recording, Discogs release, recording search, and fingerprint lookup paths
  - added focused unit coverage for cache reuse, dedupe, fingerprint enrichment, and fallback metadata
  - validated focused Brainz tests, touched-file whitespace checks, repo lint, and the broader non-live .NET suite
- Next steps:
  1. Remaining unchecked items are not immediate feature-expansion implementation work: T-938 is active in another visualizer lane, packaging tasks require real published artifacts, and broad future backlog items need separate prioritization before changing product scope.
  2. If continuing in this lane, prefer small integration migrations to `IBrainzClient` only where they reduce duplicate MusicBrainz/AcoustID orchestration without changing existing API behavior.

## Update 2026-05-01 02:43:28Z

- Current task: Tiny display visualizer controls are moved out of the tile.
- Last activity:
  - removed visualizer mode/maximize controls from inside the square visual surface
  - added an external control strip under the tile with explicit spectrum, signal, Butterchurn, native WebGL2, native WebGPU, browser-window, and fullscreen buttons
  - fullscreen now requests browser fullscreen on the tile before switching layout
  - validated focused PlayerBar/Visualizer tests, frontend lint, native browser smoke, frontend production build, and touched-file whitespace checks
- Next steps:
  1. In-browser screenshot validation is the remaining UI-confidence check.
  2. Keep renderer selection explicit; do not reintroduce hidden cycling as the only route to Butterchurn/native MilkDrop.

## Update 2026-05-01 02:33:43Z

- Current task: Tile-level visualizer controls are implemented locally.
- Last activity:
  - moved compact controls to `PlayerVisualTile` so they exist for bars, signal, Butterchurn, and native MilkDrop
  - tiny tile now always shows switch visual, maximize-to-browser-window, and maximize-to-fullscreen controls
  - maximizing from bars/signal restores the saved concrete visualizer backend before opening the expanded view
  - removed duplicate nested compact controls from `Visualizer`
  - validated focused PlayerBar/Visualizer tests, frontend lint, native browser smoke, frontend production build, and touched-file whitespace checks
- Next steps:
  1. In-browser screenshot validation of the compact tile should be the next check if the visual layout still feels cramped.
  2. Keep controls that must work across analyzer and visualizer variants at the tile layer, not inside renderer-specific components.

## Update 2026-05-01 02:29:18Z

- Current task: Compact player visualizer tile controls are fixed locally.
- Last activity:
  - compact tile controls are always visible instead of hover-only
  - compact tile shows only switch-engine, next-preset, and expand controls
  - native import/editor/debug/preset-bank controls stay out of the tiny tile and remain available after expansion
  - compact fallback errors are shortened and moved to the bottom of the tile
  - validated focused PlayerBar/Visualizer tests, frontend lint, native browser smoke, frontend production build, and touched-file whitespace checks
- Next steps:
  1. Validate the square tile in-browser against the user’s exact viewport after server startup if more visual feedback is needed.
  2. Keep advanced native controls out of compact display surfaces.

## Update 2026-05-01 02:18:48Z

- Current task: Player display visualizer mode selection is fixed locally.
- Last activity:
  - display tile now cycles album art, Butterchurn, MilkDrop3 WebGL2, MilkDrop3 WebGPU, spectrum bars, and signal scope
  - eye/keyboard visualizer toggle resolves to a concrete saved backend instead of the old `milkdrop` tile token
  - Butterchurn no longer requires the native WebGL2 support preflight before engine creation
  - documented the visualizer tile-token gotcha in ADR-0001
  - validated focused PlayerBar/Visualizer tests, frontend lint, native browser smoke, frontend production build, and touched-file whitespace checks
  - full `git diff --check` is blocked by pre-existing trailing whitespace in `docs/design/sharegroups-collections-streaming-assessment.md`
- Next steps:
  1. Run the broader MilkDrop/browser smoke/build slice after any further visualizer changes.
  2. Keep tile/display modes and renderer backend modes concrete at storage boundaries.

## Update 2026-05-01 02:01:37Z

- Current task: T-938 native MilkDrop WebGPU backend selection is implemented locally.
- Last activity:
  - expanded visualizer engine storage from Butterchurn/native to Butterchurn, native WebGL2, and native WebGPU
  - added a single overlay cycle button that is present in inline, full-window, and fullscreen visualizer modes
  - wired the native engine factory to create either the WebGL2 renderer or WebGPU renderer based on the selected backend
  - kept old stored `native` values compatible by mapping them to `native-webgl2`
  - validated focused Visualizer/native/WebGPU tests, broader MilkDrop/WebGPU unit slice, frontend lint, native browser smoke, native performance sample, frontend production build, whitespace check, and repo lint through `bash ./bin/lint`
- Next steps:
  1. Use real preset-pack/device measurements to decide whether WebGPU can graduate beyond explicit opt-in.
  2. Keep adding parity gaps found by those scans to the compatibility matrix before making WebGPU the default native backend.

## Update 2026-05-01 01:58:14Z

- Current task: Guided YAML admin settings and dump smoke cleanup are implemented locally.
- Last activity:
  - added a System Integrations settings panel for Chromaprint, AcoustID, MusicBrainz, and Lidarr
  - validated the T-939 source-feed history/audit backend slice and the new Integrations UI slice
  - fixed the stale PR-06 dump endpoint smoke expectation so test environments can return `501 NotImplemented` for unsupported dump creation
  - documented the dump-test gotcha and committed the doc entry as `fd3b283c0`
  - validated focused DumpTests, full `dotnet test`, frontend production build, frontend lint, repo lint, and whitespace checks
- Next steps:
  1. Continue the feature-expansion burn-down with webhooks/scripts, identity/auth/HTTPS, transfer policy, search/network policy, or retention/storage settings if staying in the admin-surface lane.
  2. Keep live provider execution and credential validation behind explicit user-triggered actions with visible network impact.

## Update 2026-05-01 01:58:07Z

- Current task: Remaining non-visualizer front-block feature expansions are closed for this pass.
- Last activity:
  - marked T-919 Discovery Graph complete for the current substrate pass after route/atlas/evidence/export work
  - added explicit SongID forensic-matrix export/debug endpoint and Web UI copy action
  - strengthened SongID API/store tests for run creation progress shape, persistence, and forensic export
  - marked current T-917/T-918 SongID parity complete in tasks and updated `docs/dev/SONGID_INTEGRATION_MAP.md`
  - confirmed full Web UI lint passes and marked T-915 complete
  - validated focused SongID tests, focused browser SongID helper test, frontend production build, repo lint, whitespace checks, and the broader non-live .NET suite
  - validated focused SongID backend tests and browser SongID API helper tests
- Next steps:
  1. Run whitespace, repo lint, frontend build, and broader non-live .NET tests for the final batch.
  2. Leave T-938 visualizer work to the active visualizer lane; future MIR/Essentia and Unified BrainzClient are separate follow-up epics.

## Update 2026-05-01 01:44:11Z

- Current task: Guided YAML admin settings for metadata/Lidarr integrations are implemented locally.
- Last activity:
  - validated the T-939 source-feed history/audit backend slice with focused SourceFeeds tests, whitespace, and repo lint
  - added a System Integrations settings panel for Chromaprint, AcoustID, MusicBrainz, and Lidarr
  - masked existing AcoustID/Lidarr credentials while allowing explicit replacement values
  - persisted snake_case YAML for metadata/fingerprint/Lidarr settings through the existing options YAML API
  - kept the panel configuration-only with no credential tests, provider calls, peer search, peer browse, downloads, or file mutations beyond explicit YAML save
  - validated focused Integrations tests, Integrations ESLint, frontend production build, repo lint, and whitespace checks
- Next steps:
  1. Continue the feature-expansion burn-down with webhooks/scripts, identity/auth/HTTPS, transfer policy, search/network policy, or retention/storage settings if staying in the admin-surface lane.
  2. Keep live provider execution and credential validation behind explicit user-triggered actions with visible network impact.

## Update 2026-05-01 02:02:28Z

- Current task: T-917/T-918 SongID backend review/export APIs are implemented locally.
- Last activity:
  - added a SongID queue-summary model and authenticated API for queued/running counts, configured concurrency, and active run progress snapshots
  - added a SongID evidence-package model and authenticated API for capped track/album/artist candidates, segments, mix groups, plans, acquisition options, scorecard, assessments, forensic matrix, evidence strings, warnings, and artifact references
  - kept evidence packages read-only and review/export oriented; they do not start searches, browse peers, download, publish, or mutate files
  - added focused SongID service/controller coverage for queue summaries and evidence packages
  - validated focused SongID tests, whitespace, repo lint, and the full non-live test suite
- Next steps:
  1. Continue backend/middle-lane work only where it avoids active T-938/T-919 frontend and visualizer files.
  2. Keep any SongID acquisition execution or automatic search fan-out behind explicit user-triggered actions.

## Update 2026-05-01 01:25:41Z

- Current task: T-939 source-feed import history/audit backend is implemented locally.
- Last activity:
  - moved off the crowded T-934/T-936 lane and added a larger T-939 backend follow-up
  - source-feed previews now record bounded restart-safe history entries with provider/source metadata, source fingerprint, safe source preview, request options, result counts, network-request counts, skipped-row samples, and suggestion samples
  - added authenticated API endpoints to list import history and fetch a single import run
  - persisted history to an app-dir JSON file through a temp-file plus atomic replace path and reloads it on service startup
  - avoided storing per-import provider bearer tokens in history
  - validated focused SourceFeeds service/controller tests, whitespace, repo lint, and the full non-live test suite
- Next steps:
  1. Continue with middle/backend follow-ups outside the active visualizer/frontend lanes.
  2. Keep import execution/search/download automation separate from review-first source-feed previews.

## Update 2026-05-01 01:55:32Z

- Current task: Pod direct channels no longer replace deleted Soulseek DMs in Messages; deployed to `kspls0`.
- Last activity:
  - hid pod direct channels from the Messages Pod Channels list unconditionally
  - kept non-direct pod room channels visible
  - close stale workspace panels that point at pod direct channels after hydration
  - added focused regression coverage for deleting a saved Soulseek DM without revealing `peer / dm`
  - rebuilt and deployed the web bundle to `/usr/lib/slskd/current/wwwroot` on `kspls0`
  - validated focused Messaging/App tests, frontend lint, production build, repo lint, and whitespace checks
- Next steps:
  1. Hard refresh `/messages` if the browser still has an older bundle.
  2. Build any future mesh-DM surface explicitly instead of exposing pod direct channels as fallback DMs.

## Update 2026-05-01 01:46:07Z

- Current task: Unified Messages permanent delete/leave actions and dense transcript cleanup are deployed to `kspls0`.
- Last activity:
  - added confirmed permanent delete for saved DM threads from sidebar rows and chat panel headers
  - added confirmed room leave from joined-room rows and room panel headers
  - added confirmed pod leave from pod-channel rows and pod panel headers, preserving the Gold Star irreversible warning
  - moved rich user badges out of repeated chat/room message rows and into stable identity surfaces
  - rebuilt and deployed the web bundle to `/usr/lib/slskd/current/wwwroot` on `kspls0`
  - validated focused Messaging/App tests, frontend lint, production build, repo lint, and whitespace checks
- Next steps:
  1. Hard refresh `/messages` if the browser still has an older bundle.
  2. If the permanent actions should also show toast success/failure messages in Messages, add that as a small UX follow-up.

## Update 2026-05-01 01:38:27Z

- Current task: Listen Along channel scoping and compact room-broadcast controls are deployed to `kspls0`.
- Last activity:
  - removed Listen Along from pod direct-message channels in Messages and Pods
  - kept Listen Along available for pod room channels only
  - replaced the full Listen Along panel with a compact room-broadcast strip using a glowing status icon and small icon controls
  - rebuilt and deployed the web bundle to `/usr/lib/slskd/current/wwwroot` on `kspls0`
  - validated focused Messaging/App tests, frontend lint, production build, repo lint, and whitespace checks
- Next steps:
  1. Hard refresh `/messages` if the browser still has an older bundle.
  2. Treat any future mesh-wide broadcast surface as a separate global control rather than a direct-message widget.

## Update 2026-05-01 01:22:02Z

- Current task: Unified Messages duplicate pod-DM and composer/member-list follow-up is deployed to `kspls0`.
- Last activity:
  - folded pod direct channels into matching saved DMs so `peer / DM` pod channels no longer duplicate normal saved direct-message rows
  - added cleanup for stale restored bridged pod-DM panels in workspace local storage
  - made embedded chat, room, and pod message composers visibly usable inside workspace cards
  - preserved room member lists inside room workspace panels
  - rebuilt and deployed the web bundle to `/usr/lib/slskd/current/wwwroot` on `kspls0`
  - validated focused Messaging/App tests, frontend lint, production build, repo lint, and whitespace checks
- Next steps:
  1. Check the live Messages page after a browser hard refresh; service-worker/browser cache may keep the previous bundle until refreshed.
  2. Keep deeper pod-DM/Soulseek-DM history merging as a separate behavior decision if the two transports need one combined transcript.

## Update 2026-05-01 01:38:13Z

- Current task: T-919 Discovery Graph backend evidence-lane batch is implemented locally.
- Last activity:
  - added additive Discovery Graph edge evidence lanes and graph-level evidence summaries derived from score components, provenance, and evidence strings
  - extended the browser branch report to include backend-provided evidence lanes
  - kept graph evidence passive and review-only; it does not search, browse peers, download, publish, or mutate files
  - validated focused backend Discovery Graph service tests, focused browser Discovery Graph helper tests, ESLint on touched graph JS/JSX files, whitespace, repo lint, frontend production build, and the broader non-live .NET suite
- Next steps:
  1. Continue T-919 or move to T-917/T-918 once Discovery Graph’s first-class route/evidence/export surface is sufficient for this pass.
  2. Resolve or coordinate the existing dirty `playlistIntake.js` lint issue before relying on full `npm --prefix src/web run lint`.

## Update 2026-05-01 01:24:19Z

- Current task: T-919 Discovery Graph atlas branch planning batch is implemented locally.
- Last activity:
  - added browser-local graph helpers for visible node/edge filtering, branch route suggestions, nearby search seeds, and copyable branch review reports
  - added in-page Search atlas edge-family filters, suggested branch routes, pinned comparison context, copy report, and recenter actions
  - kept the atlas review local and passive except for the existing explicit Queue Nearby action
  - validated focused graph helper tests, the Search graph component test slice, ESLint on the touched graph JS/JSX files, whitespace, and the frontend production build
  - noted full web lint is blocked by an existing dirty `playlistIntake.js` `no-control-regex` issue outside this graph batch
- Next steps:
  1. Continue T-919 or the next front-of-list feature item, avoiding active visualizer and middle-lane dirty files.
  2. Resolve or coordinate the existing dirty `playlistIntake.js` lint issue before relying on full `npm --prefix src/web run lint`.

## Update 2026-05-01 01:13:23Z

- Current task: T-934 realm subject-index persistence follow-up is implemented locally.
- Last activity:
  - added restart-safe JSON persistence for accepted realm subject indexes, governance proposals, proposal reviews, and local authority decisions
  - reloads persisted subject-index state at service startup so accepted resolver data, proposal history, and disabled authority preferences survive restarts
  - writes the app-dir state file through a temp-file plus atomic replace path with deterministic ordering
  - added focused service tests for reloading accepted indexes with disabled authorities and accepted proposal review state
  - kept the persistence local and passive with no publishing, peer contact, search, browse, download, or file mutation beyond the explicit app-dir state file
  - validated focused subject-index tests, whitespace, repo lint, and the full non-live test suite
- Next steps:
  1. Continue center-lane backend work only where it avoids dirty UI and front/end-lane files.
  2. Keep actual subject-index publication/sync as a separate explicit governance/mesh contract.

## Update 2026-05-01 01:50:52Z

- Current task: T-938 native MilkDrop WebGPU readiness reporting is implemented locally.
- Last activity:
  - added WebGPU-specific compatibility matrix reporting alongside WebGL2 support
  - report entries now identify WebGPU-only shader-section blockers
  - added coverage for curated WebGPU support counts, safe WGSL named sampler/audio-helper readiness, and a WebGPU-only shader gap
  - the main remaining WebGPU promotion work is real-pack/device measurement and follow-up on measured gaps, not another obvious known parity slice
  - validated focused compatibility/shader/WebGPU tests, broader MilkDrop/Visualizer tests, and frontend lint
- Next steps:
  1. Run broader native MilkDrop performance/compatibility scans against real preset packs when available.
  2. Keep WebGL2 as the active parity renderer until those scans show WebGPU behavior is stable across devices.

## Update 2026-05-01 01:46:13Z

- Current task: T-938 native MilkDrop WebGPU named shader texture samplers are implemented locally.
- Last activity:
  - extended safe WGSL translation for named `tex`/`tex2D` sampler references
  - added four optional shader texture slots to the WebGPU feedback bind layout
  - built separate warp/comp feedback bind groups so each shader body gets its own sampler ordering
  - resolved imported texture assets through shared native alias rules with procedural fallback
  - validated focused shader/WebGPU tests, broader MilkDrop/Visualizer tests, and frontend lint
- Next steps:
  1. Continue T-938 with broader real-pack compatibility/performance measurement and any issues that measurement exposes.
  2. Keep native MilkDrop opt-in until real-pack compatibility and device performance behavior are measured across more systems.

## Update 2026-05-01 01:40:54Z

- Current task: T-938 native MilkDrop WebGPU shader audio-bin helpers are implemented locally.
- Last activity:
  - added WGSL `get_fft`, `get_fft_hz`, and `get_waveform` helpers for the safe WebGPU shader subset
  - added explicit 64-bin FFT and waveform uniform fields to avoid WebGPU uniform-array layout ambiguity
  - populated those bins from native render-frame spectrum/waveform data
  - kept named shader texture samplers as the main remaining WebGPU shader parity gap
  - validated focused shader/WebGPU tests, broader MilkDrop/Visualizer tests, and frontend lint
- Next steps:
  1. Continue T-938 with WebGPU named shader texture samplers or broader real-pack compatibility/performance measurement.
  2. Keep native MilkDrop opt-in until real-pack compatibility and device performance behavior are measured across more systems.

## Update 2026-05-01 01:37:08Z

- Current task: T-938 native MilkDrop WebGPU safe-subset shader execution is implemented locally.
- Last activity:
  - added WGSL generation from the existing safe MilkDrop shader parser for WebGPU feedback passes
  - wired translated WebGPU warp and comp shaders when presets fit the conservative WGSL subset
  - added WebGPU uniform coverage for color, time, bass/mid/treble, attenuated audio values, viewport size, output alpha, feedback, and q1-q64
  - kept named shader texture samplers and shader audio-bin helpers on WebGL2 while WebGPU support stays conservative
  - validated focused shader/WebGPU tests, broader MilkDrop/Visualizer tests, and frontend lint
- Next steps:
  1. Continue T-938 with WebGPU named shader texture samplers, shader audio-bin helpers, or larger real-pack compatibility/performance measurement.
  2. Keep native MilkDrop opt-in until real-pack compatibility and device performance behavior are measured across more systems.

## Update 2026-05-01 01:11:58Z

- Current task: T-938 native MilkDrop WebGPU textured primitive foothold is implemented locally.
- Last activity:
  - added WebGPU texture upload for procedural and imported native MilkDrop texture assets
  - reused WebGL/native texture alias matching for WebGPU shape/sprite texture lookup
  - added textured shape and sprite UV/color vertex packing plus a textured WebGPU primitive pipeline
  - padded raw WebGPU texture rows to browser validation-safe strides
  - kept WebGL2 as active while WebGPU still needs translated warp/comp shaders and broader real-pack/device measurement
  - validated focused WebGPU tests, broader MilkDrop/Visualizer tests, and frontend lint
- Next steps:
  1. Continue T-938 with WebGPU translated warp/comp shader execution or larger real-pack compatibility/performance measurement.
  2. Keep native MilkDrop opt-in until real-pack compatibility and device performance behavior are measured across more systems.

## Update 2026-05-01 01:03:32Z

- Current task: Front-of-list DHT candidate backoff and share-scan probe control are implemented locally.
- Last activity:
  - added progressive service-level reconnect backoff for repeatedly failing DHT-discovered overlay candidates
  - added `shares.probe_media_attributes` / CLI / environment control for skipping TagLib audio metadata extraction during share scans on slow or remote storage
  - updated example/config docs and focused tests for the new media probing fast path
  - documented both gotchas in ADR-0001 and committed the doc-only entry as `1a8ec1cd9`
  - closed the matching DHT/share-scan tasks, including the already-completed share-scan harness checkbox
- Next steps:
  1. Continue the front-of-list burn-down, avoiding active visualizer and middle-lane dirty files.
  2. Re-check the optional live-account mesh smoke when live Soulseek credentials/network are healthy; local tests pass with only that smoke excluded.

## Update 2026-05-01 01:01:48Z

- Current task: T-934 realm subject-index backend authority decisions are implemented locally.
- Last activity:
  - added local authority-decision models and service methods for accepted realm subject indexes
  - added authenticated API endpoints to list decisions and disable or re-enable a realm/index authority
  - filtered disabled authorities out of recording resolution and conflict reports while preserving accepted indexes and governance history
  - added focused service/controller tests for disable, re-enable, invalid actor/index rejection, decision listing, and API responses
  - kept decisions local and passive with no governance publication, peer contact, search, browse, download, or file mutation
  - validated focused subject-index tests, whitespace, repo lint, and the full non-live test suite
- Next steps:
  1. Keep any persistent authority-preference storage as a separate follow-up if needed.
  2. Continue center-lane backend work only where it avoids dirty UI lanes.

## Update 2026-05-01 00:47:43Z

- Current task: T-936 Quarantine Jury release-package backend is implemented locally.
- Last activity:
  - added a read-only release evidence package for locally accepted release-candidate jury decisions
  - included request evidence, selected jurors, signed verdicts, route attempts, the acceptance snapshot, current aggregate state, and drift warnings for post-acceptance aggregate changes
  - added authenticated API endpoint `GET /api/v0/quarantine-jury/requests/{requestId}/release-package`
  - kept release packages observational with no quarantine enforcement change, file movement, route dispatch, publication, search, browse, or download
  - validated focused Quarantine Jury service/controller tests, whitespace, repo lint, and the full non-live test suite
  - noted that unfiltered `dotnet test` only failed the optional live Soulseek-account overlay mesh test because the beta account did not log in
- Next steps:
  1. Keep actual quarantine release/file movement behind a separate explicit enforcement contract.
  2. Continue center-lane backend work only where it avoids dirty UI lanes.

## Update 2026-05-01 00:56:02Z

- Current task: T-938 native MilkDrop WebGPU filled-shape/sprite foothold is implemented locally.
- Last activity:
  - added triangle-fan to triangle-list conversion for WebGPU filled primitive rendering
  - added native filled custom shape rendering with existing center/edge color data
  - added fallback sprite-quad rendering with existing native sprite geometry and color helpers
  - kept WebGL2 as the active native MilkDrop compatibility renderer while WebGPU still needs textured sprite/shape sampling, translated shaders, and real-pack measurement before becoming a candidate renderer
  - validated focused WebGPU renderer tests and frontend lint
- Next steps:
  1. Continue T-938 with WebGPU texture sampling, translated shader execution, or larger real-pack compatibility/performance measurement.
  2. Keep native MilkDrop opt-in until real-pack compatibility and device performance behavior are measured across more systems.

## Update 2026-05-01 00:47:46Z

- Current task: T-938 native MilkDrop WebGPU screen-border foothold is implemented locally.
- Last activity:
  - added a WebGPU triangle-list primitive pipeline and colored triangle vertex conversion
  - added classic outer/inner screen-border rendering from existing `ob_*` and `ib_*` geometry/color helpers
  - draws screen borders into the WebGPU feedback texture before the display pass
  - kept WebGL2 as the active native MilkDrop compatibility renderer while WebGPU remains isolated until filled shapes, sprites, translated shaders, and real-pack parity mature
  - validated focused WebGPU renderer tests and frontend lint
- Next steps:
  1. Continue T-938 with deeper WebGPU primitive parity, translated shader execution, or larger real-pack compatibility/performance measurement.
  2. Keep native MilkDrop opt-in until real-pack compatibility and device performance behavior are measured across more systems.

## Update 2026-05-01 00:41:28Z

- Current task: T-938 native MilkDrop WebGPU motion-vector foothold is implemented locally.
- Last activity:
  - added WebGPU motion-vector rendering using the existing native `mv_*` vertex and color helpers
  - added a reusable colored line-list conversion helper for WebGPU primitive buffers
  - kept waveform, shape-outline, and motion-vector WebGPU draws on the same feedback-texture primitive path
  - kept WebGL2 as the active native MilkDrop compatibility renderer while WebGPU remains isolated until translated shader execution and broader real-pack parity are ready
  - validated focused WebGPU renderer tests and frontend lint
- Next steps:
  1. Continue T-938 with deeper WebGPU primitive parity, translated shader execution, or larger real-pack compatibility/performance measurement.
  2. Keep native MilkDrop opt-in until real-pack compatibility and device performance behavior are measured across more systems.

## Update 2026-05-01 00:34:46Z

- Current task: T-938 native MilkDrop WebGPU primitive foothold is implemented locally.
- Last activity:
  - added WebGPU waveform primitive rendering into the feedback texture using the shared native waveform vertex mapping
  - added first WebGPU shape-outline primitive rendering for enabled MilkDrop shapes
  - reused the existing native shape outline and border-color helpers so WebGPU primitive behavior stays aligned with WebGL2
  - kept WebGL2 as the active native MilkDrop compatibility renderer while WebGPU remains isolated until it has fuller primitive, shader, and real-pack parity
  - validated focused WebGPU renderer tests and frontend lint
- Next steps:
  1. Continue T-938 with deeper WebGPU primitive parity, translated shader execution, or larger real-pack compatibility/performance measurement.
  2. Keep native MilkDrop opt-in until real-pack compatibility and device performance behavior are measured across more systems.

## Update 2026-05-01 00:29:26Z

- Current task: Approved Discovery Inbox acquisition search handoff is implemented locally.
- Last activity:
  - added bounded acquisition-plan execution that queues backend search jobs through the selected acquisition profile
  - records queued search IDs, failed execution summaries, and skipped plans when batch limits apply
  - added Discovery Inbox Execute Ready controls with visible search-only network impact
  - kept execution operator-triggered and limited to search creation; no peer browse, result selection, download, tag write, or file movement starts from the handoff
  - validated focused Acquisition Plans and Discovery Inbox tests, frontend lint, frontend production build, and whitespace checks
- Next steps:
  1. Continue beginning-lane burn-down to the next incomplete epic that does not collide with active middle/tail files.
  2. Keep actual download queueing behind search-result selection, confirmation, and existing transfer limits.

## Update 2026-05-01 00:35:31Z

- Current task: T-936 Quarantine Jury audit-report backend is implemented locally.
- Last activity:
  - added read-only audit report models for accepted, pending, stale, routed, and failed-route states
  - added service aggregation for quorum, recommendation, acceptance, route-attempt, stale, and dissent summaries
  - added authenticated API endpoint `GET /api/v0/quarantine-jury/audit`
  - kept the audit endpoint observational with no quarantine enforcement change, file movement, route dispatch, publication, search, browse, or download
  - validated focused Quarantine Jury service/controller tests, whitespace, repo lint, and full test suite
- Next steps:
  1. Keep future Quarantine Jury enforcement or file-release actions behind separate explicit confirmation contracts.
  2. Continue center-lane backend work only where it avoids dirty UI lanes.

## Update 2026-05-01 00:22:01Z

- Current task: T-935 MusicBrainz overlay persistence follow-up is implemented locally.
- Last activity:
  - added restart-safe atomic JSON persistence for signed overlay edits
  - persisted selected-peer route attempts and manual upstream export approvals
  - added explicit storage-path constructors and reload tests for edits, approvals, and route attempts
  - documented and committed the persistent-service test-storage gotcha after fixing shared app-state contamination in overlay tests
  - kept persistence passive with no publication, upstream submission, Soulseek search, peer browse, download, or cached MusicBrainz mutation
  - validated focused MusicBrainz overlay service tests, whitespace, repo lint, and full test suite
- Next steps:
  1. Continue center-lane backend follow-ups only where they avoid dirty UI lanes.
  2. Keep any future overlay federation/export actions explicit and review-first.

## Update 2026-05-01 00:24:18Z

- Current task: T-938 native MilkDrop WebGPU foothold is implemented locally.
- Last activity:
  - added a native MilkDrop WebGPU adapter probe with feature/limit/adapter metadata
  - surfaced WebGPU adapter details in the native debug snapshot and overlay
  - added an opt-in WebGPU renderer module that configures a `webgpu` canvas, maintains ping-pong feedback textures, and draws a preset-colored fullscreen display pass
  - kept WebGL2 as the active native MilkDrop compatibility renderer while WebGPU remains a scaffold for future primitives and translated shader execution
  - validated focused WebGPU renderer, native MilkDrop engine, and Visualizer tests
- Next steps:
  1. Continue T-938 with deeper WebGPU feedback-texture parity or larger real-pack compatibility/performance measurement.
  2. Keep native MilkDrop opt-in until real-pack compatibility and device performance behavior are measured across more systems.

## Update 2026-05-01 00:25:16Z

- Current task: Epic E9 Audio Verification profiles, cache, and policy review are implemented locally.
- Last activity:
  - added browser-local audio verification profiles for lossless-exact, balanced, and permissive review
  - added verification decisions with confidence, evidence, warnings, fail-open/fail-closed actions, and browser-local SHA-256 cache controls
  - wired Import Staging to show audio verification status, verify staged rows, store decisions, and explicitly apply the selected policy to Ready, Staged, or Failed review state
  - kept verification operator-controlled and local-only with no upload, import, tag write, file move, Soulseek search, peer browse, or download
  - validated focused Audio Verification, Import Staging helper/component tests, frontend lint, frontend production build, whitespace checks, `bin/lint`, and full `dotnet test`
- Next steps:
  1. Continue beginning-lane burn-down into the next feature-expansion epic after E9.
  2. Keep any future acoustic/fingerprint provider calls behind explicit enablement, rate limits, and visible Web UI state.

## Update 2026-05-01 00:11:34Z

- Current task: T-934 realm subject-index conflict-report backend is implemented locally.
- Last activity:
  - added conflict report models that preserve realm/index/revision provenance per conflicting value
  - added deterministic service reporting for external-id, recording-subject, WorkRef identity, and alias-subject conflicts
  - added authenticated read-only API endpoints for accepted indexes, recording resolutions, and conflict reports
  - kept conflict inspection passive with no publication, peer contact, Soulseek search, browse, download, or file mutation
  - validated focused Realm Subject Index service/controller tests, whitespace, repo lint, and full test suite
- Next steps:
  1. Leave the actual UI conflict display to a frontend owner unless the active UI lanes clear.
  2. Keep any future subject-index authority-disable action behind explicit user confirmation and provenance review.

## Update 2026-05-01 00:08:57Z

- Current task: Epic E8 shared Metadata Matching surface is implemented locally.
- Last activity:
  - added reusable metadata normalization and weighted candidate scoring
  - added short-title protection, version-tag awareness, identifier evidence, confidence bands, and explanation fields
  - added Import Staging manual metadata override acceptance
  - added Playlist Intake reuse for candidate scoring
  - kept matching local/deterministic with no metadata provider call, Soulseek search, peer browse, download, tag write, file move, or library mutation
  - validated focused Metadata Matcher, Import Staging, Playlist Intake, and App route tests, frontend lint, frontend production build, whitespace checks, `bin/lint`, and full `dotnet test`
- Next steps:
  1. Continue beginning-lane burn-down into Epic E9 Audio Verification.
  2. Reuse the shared metadata matcher anywhere new import/search/tagging surfaces need confidence bands or explanations.

## Update 2026-04-30 22:08:39Z

- Current task: Epic E7 provider refresh, scheduled refresh, and slskdN playlist creation are implemented locally.
- Last activity:
  - connected Playlist Intake provider refresh to the existing Source Feed Import preview API
  - added Apply Refresh for pasted rows and provider refresh application for mirrored playlist intake rows
  - added scheduled refresh enablement and Run Due Refreshes execution for mirrored playlists
  - added actual Playlist collection creation through the Collections API from matched Playlist Intake rows
  - kept refresh execution sequential and bounded by per-playlist provider limits, with no Soulseek search, peer browse, or download
  - validated focused Playlist Intake helper/component tests, App route tests, frontend lint, frontend production build, whitespace checks, `bin/lint`, and full `dotnet test`
- Next steps:
  1. Continue from the next beginning-lane epic after E7.
  2. Keep future Playlist Intake acquisition actions bounded by provider limits and explicit user controls.

## Update 2026-04-30 21:57:06Z

- Current task: Frontend Quarantine Jury review UI phase is implemented locally.
- Last activity:
  - added Quarantine Jury Web UI API client
  - added System -> Quarantine Jury request/review workspace
  - surfaced evidence, juror verdicts, dissent, route attempts, explicit route dispatch, acceptance status, and modal-gated release-candidate acceptance
  - kept local quarantine authoritative with no file movement, release broadcast, search, browse, download, or library mutation
  - documented and fixed the post-action success-message refresh gotcha
  - validated focused Quarantine Jury Web UI test, frontend lint, frontend production build, and whitespace checks
- Next steps:
  1. Continue only after checking active lanes; Playlist Intake, MilkDrop, and MusicBrainz remain concurrently edited.
  2. Keep Quarantine Jury file-release or quarantine-state changes behind separate explicit backend contracts.

## Update 2026-04-30 21:57:55Z

- Current task: Epic E7 Playlist Intake review handoff and playlist-build previews are implemented locally.
- Last activity:
  - added changed-row detection to mirror refresh previews
  - added visible refresh cadence, cooldown, and disabled automation policy to playlist intake cards
  - added bulk Discovery Inbox handoff for non-rejected playlist rows
  - added matched-row slskdN playlist text preview without creating or mutating playlists
  - kept the batch local-only with no provider fetch, Soulseek search, peer browse, download, scheduler, slskd playlist creation, or file mutation
  - validated focused Playlist Intake helper/component tests and App route tests, frontend lint, frontend production build, whitespace checks, `bin/lint`, and full `dotnet test`
- Next steps:
  1. Continue from the next beginning-lane epic after E7 unless another Playlist Intake gap appears during integration.
  2. Defer provider-backed playlist refresh, scheduled refresh execution, and actual slskd playlist mutation until credentials, rate limits, and explicit confirmation flows are implemented.

## Update 2026-04-30 21:49:21Z

- Current task: Epic E12 Library Health safe-fix manifest export is implemented locally.
- Last activity:
  - added selected auto-fixable issue safe-fix manifest generation
  - surfaced Copy Safe-Fix Manifest in the Library Health selected-issue toolbar
  - kept manifest export review-only with no remediation job, safe-fix execution, quarantine state change, search, download, or file mutation
  - validated focused Library Health helper/component tests, frontend lint, frontend production build, and whitespace checks
- Next steps:
  1. Continue tail-side work only where ownership stays clear of active Playlist Intake, MilkDrop, and MusicBrainz lanes.
  2. Keep any Library Health execution path separate from these copy/export review surfaces.

## Update 2026-04-30 21:48:49Z

- Current task: Epic E7 Playlist Intake refresh diffs and row review controls are implemented locally.
- Last activity:
  - added browser-local added/removed/unchanged refresh diff previews for mirrored playlist rows
  - added matched, unmatched, and rejected row review controls
  - added partial completion summaries for matched, unmatched, and rejected rows
  - preserved source identity and row evidence across review changes
  - kept the batch local-only with no provider fetch, Soulseek search, peer browse, download, slskd playlist creation, scheduler, or file mutation
  - validated focused Playlist Intake helper/component tests and App route tests, frontend lint, frontend production build, whitespace checks, `bin/lint`, and full `dotnet test`
- Next steps:
  1. Continue E7 with slskd playlist creation previews or Discovery Inbox bulk handoff if ownership remains clear.
  2. Defer provider-backed playlist refresh until credentials, rate limits, and per-provider preview contracts are explicit.

## Update 2026-04-30 21:45:52Z

- Current task: Epic E12 Library Health quarantine review packet export is implemented locally.
- Last activity:
  - added selected-risky-issue quarantine review packet generation
  - surfaced Copy Quarantine Packet in the Library Health selected-issue toolbar
  - kept packet export review-only with no quarantine state change, file movement, peer message, remediation job, search, download, or file mutation
  - validated focused Library Health helper/component tests, frontend lint, frontend production build, and whitespace checks
- Next steps:
  1. Keep future Library Health handoffs explicit-review-first with no automatic quarantine/remediation.
  2. Continue tail-side work only where ownership stays clear of active Playlist Intake, MilkDrop, and MusicBrainz lanes.

## Update 2026-04-30 21:43:23Z

- Current task: Epic E12 Library Health replacement search seed export is implemented locally.
- Last activity:
  - added deduped replacement search seed generation from selected Library Health issues
  - surfaced Copy Search Seeds in the Library Health selected-issue toolbar
  - kept seed export review-only with no Search navigation, peer browse, download, quarantine, remediation job, rescan, or file mutation
  - validated focused Library Health helper/component tests, frontend lint, frontend production build, and whitespace checks
- Next steps:
  1. Continue E12 only with review-first handoff surfaces that avoid automatic remediation.
  2. Keep any future replacement-search handoff behind explicit user confirmation and bounded network policy.

## Update 2026-04-30 22:04:19Z

- Current task: T-933 federated taste recommendation graph/handoff follow-up is implemented locally.
- Last activity:
  - added optional Discovery Graph evidence and proximity scoring for recommendations
  - added explicit backend handoffs to create review-only Wishlist seeds, artist release radar subscriptions, and Discovery Graph preview summaries
  - allowed MusicBrainz artist UUID external IDs in safe WorkRefs for MBID-backed radar subscriptions
  - kept service-layer k-anonymity and avoided peer/source reveal unless explicitly requested
  - kept all handoffs passive: no Soulseek search, peer browse, download, federation publish, or file-library mutation
  - validated focused `TasteRecommendationServiceTests`, `TasteRecommendationsControllerTests`, and `WorkRefTests`
- Next steps:
  1. Continue middle-lane backend work only if another non-UI federation follow-up remains clear; the remaining T-933 UI surface should stay with a UI owner.
  2. Leave concurrent System Integrations, Library Health, Playlist Intake, Quarantine Jury UI, MilkDrop/Visualizer, Discovery Inbox, Player, Watchlist, and Discovery Shelf edits untouched unless asked to integrate.

## Update 2026-04-30 21:51:38Z

- Current task: T-932 signed artist radar observation routing follow-up is implemented locally.
- Last activity:
  - added explicit selected-peer PodCore route attempts for artist-radar notifications
  - added safe opaque route metadata validation, signed local route envelopes, and route attempt API dispatch/history endpoints
  - persists route attempts with the artist radar state file so route history survives daemon restarts
  - kept routing manual-only with no automatic publication, MusicBrainz polling, Soulseek search, peer browse, download, or file-library mutation
  - validated focused `ArtistReleaseRadar` service/controller tests
- Next steps:
  1. Continue center-lane work only if another backend-only federation follow-up remains clear of active UI lanes.
  2. Leave concurrent System Integrations, Library Health, Playlist Intake, MilkDrop/Visualizer, Discovery Inbox, Player, Watchlist, and Discovery Shelf edits untouched unless asked to integrate.

## Update 2026-04-30 21:40:56Z

- Current task: Epic E7 browser-local Playlist Intake first slice is implemented locally.
- Last activity:
  - added a Playlist Intake route and navigation entry
  - added browser-local playlist intake storage and parsing for pasted text/CSV-style rows
  - retained playlist name, source identity, inferred provider, mirror-review setting, and per-row source lines
  - surfaced matched/unmatched row state and Discovery Inbox review handoff
  - kept the slice local-only with no provider fetch, Soulseek search, peer browse, download, slskd playlist creation, scheduler, or file mutation
  - validated focused Playlist Intake helper/component tests and App route tests
- Next steps:
  1. Continue E7 with refresh diff previews for mirrored playlists if ownership remains clear.
  2. Defer provider-backed playlist refresh until credentials, rate limits, and per-provider preview contracts are explicit.

## Update 2026-04-30 21:41:12Z

- Current task: T-932 artist release radar persistence follow-up is implemented locally.
- Last activity:
  - added an atomic JSON state file for radar subscriptions, muted release groups, seen-observation keys, and notifications
  - reloads persisted radar state on service startup so notification history and duplicate suppression survive daemon restarts
  - kept the feature local-only with no MusicBrainz polling, Soulseek search, peer browse, mesh routing, download, or file-library mutation
  - validated focused `ArtistReleaseRadarServiceTests`
- Next steps:
  1. Continue center-lane work with signed artist radar observation routing only if it can use explicit trusted peers/realms and avoid automatic publication.
  2. Leave concurrent System Integrations, Library Health, Playlist Intake, Discovery Inbox, Player, Watchlist, and Discovery Shelf edits untouched unless asked to integrate.

## Update 2026-04-30 21:29:48Z

- Current task: Epic E6 review-only Watchlist similar-artist expansion approval is implemented locally.
- Last activity:
  - added manually supplied similar-artist expansion candidates to browser-local watchlists
  - surfaced pending, approved, and rejected expansion evidence in Discovery Inbox watchlist rows
  - added tooltip-backed approve/reject actions for pending candidates
  - approving an expansion creates a manual Artist watchlist using the parent filter/profile policy
  - kept the slice local-only with no provider lookup, Soulseek search, peer browse, download, scheduler, automation execution, or file mutation
  - validated focused Watchlists and Discovery Inbox component tests
- Next steps:
  1. Continue beginning-lane burn-down into Epic E7 Playlist Intake only if it stays browser-local/file-first and avoids active System/Integrations work.
  2. Defer provider-backed similar-artist discovery until credentials, rate limits, and explicit expansion evidence contracts are defined.

## Update 2026-04-30 21:47Z

- Current task: T-938 native MilkDrop fragment management is implemented locally.
- Last activity:
  - added active custom shape/wave summaries to the native MilkDrop engine
  - added selected-fragment export and selected-fragment removal controls to the visualizer overlay
  - persisted edited presets back into the browser-local native preset library after shape/wave removal
  - added persisted beat-count and timed-interval settings for native automatic preset changes
  - added first native parameter editing for decay, zoom, rotation, waveform color/alpha, and full active-preset text export
  - added bounded parameter randomization, pointer-fed mouse variables, and a compact native debug snapshot overlay
  - added active native preset playlist rename support
  - added browser-local native FPS caps and a debug frame-time readout
  - added native quality presets, WebGPU capability reporting, and WebGL context loss/restore smoke coverage
  - added Chromium native render performance measurement for curated fixtures or local preset files/folders
  - added a bounded translated-shader cache for repeated native warp/comp shader bodies
  - documented and committed the DOM factory spy gotcha after fixing recursive `document.createElement` test failures
  - validated focused Visualizer, native MilkDrop engine, and preset parser tests
- Next steps:
  1. Continue T-938 with optional WebGPU renderer investigation and deeper real-pack compatibility/performance measurement.
  2. Keep native MilkDrop as opt-in until real-pack compatibility and performance behavior are measured across more devices.

## Update 2026-04-30 21:32:19Z

- Current task: T-931 Bloom-filter library diff is implemented locally.
- Last activity:
  - added serializable Bloom filter export/import support
  - added MusicBrainz library Bloom snapshot preview, inbound diff comparison, and Wishlist promotion API/service
  - compared inbound salted recording/release Bloom filters against local cached MusicBrainz candidates and local held recordings
  - kept suggestions probabilistic and review-only with no filenames, paths, file hashes, exact identifier lists, publishing, searches, browsing, or downloads
  - validated focused `LibraryBloomDiffServiceTests`
- Next steps:
  1. Continue center-lane backend work with artist radar persistence or signed radar routing if it avoids active frontend/options lanes.
  2. Leave concurrent System Integrations, OptionsOverlay, Discovery Inbox, Player, Watchlist, and Discovery Shelf edits untouched unless asked to integrate.

## Update 2026-04-30 21:20:12Z

- Current task: T-937 Discography Concierge graph-density prioritization is implemented locally.
- Last activity:
  - added optional Discovery Graph priority metadata to Discography Coverage responses
  - ranked releases by missing-track gap, graph neighborhood density, and existing HashDb/Wishlist evidence
  - kept the scoring local-only with no peer browse, Soulseek search, download, or graph publication
  - validated focused `DiscographyCoverageServiceTests`
- Next steps:
  1. Continue center-lane backend work with Bloom-filter library diff or artist radar persistence if it avoids active frontend/options lanes.
  2. Leave concurrent Discovery Inbox, Player, Watchlist, Discovery Shelf, and Options edits untouched unless asked to integrate.

## Update 2026-04-30 21:37:54Z

- Current task: Epic E12 Library Health selected action-plan previews are implemented locally.
- Last activity:
  - added selected-issue safe-fix, replacement-search, and quarantine-review preview classification
  - surfaced Copy Action Plan in the Library Health selected-issue toolbar
  - kept action plans review-only with no remediation job, replacement search, quarantine, rescan, download, or file mutation
  - validated focused Library Health report helper/component tests, frontend lint, frontend production build, and whitespace checks
- Next steps:
  1. Continue E12 with queue/quarantine handoff surfaces only after explicit backend confirmation contracts exist.
  2. Keep Library Health follow-up actions review-first and separate from automatic remediation.

## Update 2026-04-30 21:30:10Z

- Current task: Epic E12 Library Health report export is implemented locally.
- Last activity:
  - added a browser-side Library Health text report builder for loaded scan data
  - surfaced Copy Report in the Library Health overview after summary/issues are loaded
  - kept report export read-only with no scan start, remediation job, replacement search, quarantine, or file mutation
  - validated focused Library Health tests, frontend lint, frontend production build, and whitespace checks
- Next steps:
  1. Continue E12 with replacement-search or quarantine previews only if they stay explicit-review-first.
  2. Keep Library Health remediation/export actions separately confirmed and non-automatic.

## Update 2026-04-30 21:19:35Z

- Current task: Epic E13 Discovery Shelf policy report export is implemented locally.
- Last activity:
  - added copyable shelf policy reports with expiry window, consensus requirement, policy counts, and item-level planned actions
  - surfaced Copy Report in the player Discovery Shelf modal
  - kept report generation review-only with no apply, file mutation, queue, download, publish, or backend call
  - validated broader player-focused tests, frontend lint, frontend production build, and whitespace checks
- Next steps:
  1. Move into Epic E12 Library Health only if the next slice can be read-only/report-first and avoid active System ownership.
  2. Keep Discovery Shelf apply actions disabled until backend preview, confirmation, and shared-owner consensus contracts exist.

## Update 2026-04-30 21:17:09Z

- Current task: Epic E6 visible Watchlist schedule and cooldown policy is implemented locally.
- Last activity:
  - added manual/daily/weekly/monthly schedule choices to watchlist creation
  - persisted bounded cooldown days and acquisition profile policy with watchlist targets
  - displayed schedule enablement, cooldown, profile policy, and no-execution network impact on each watchlist row
  - kept the slice local-only with no scheduler, metadata provider lookup, Soulseek search, peer browse, download, automation execution, or file mutation
  - validated focused Watchlists and Discovery Inbox component tests
- Next steps:
  1. Continue beginning-lane E6 with similar-artist expansion approval if it can stay review-only and avoid active middle/end lanes.
  2. Defer provider-backed release radar scans until credentials, cooldown enforcement, and network-impact policy are explicit.

## Update 2026-04-30 21:16:27Z

- Current task: Epic E13 Discovery Shelf policy previews are implemented locally.
- Last activity:
  - added local promote/archive/expiry/review/consensus-gated policy preview counts
  - added configurable unrated expiry window and shared-library consensus toggle to the player shelf modal
  - kept policy preview informational only with no backend apply or file mutation action
  - validated broader player-focused tests, frontend lint, frontend production build, and whitespace checks
- Next steps:
  1. Continue E13 only if the next slice can remain explicit-preview-first and avoid DiscoveryInbox ownership.
  2. Keep apply/delete/archive disabled until backend preview, confirmation, and shared-owner consensus contracts exist.

## Update 2026-04-30 21:09:16Z

- Current task: Epic E6 browser-local Watchlist release filters are implemented locally.
- Last activity:
  - added visible release type, country, and format filter controls to the Discovery Inbox Watchlists form
  - persisted normalized filter choices with watchlist targets
  - displayed saved filter context on each watchlist row and in generated Discovery Inbox review seeds
  - kept the slice local-only with no metadata provider lookup, Soulseek search, peer browse, download, scheduled automation, or file mutation
  - validated focused Watchlists and Discovery Inbox component tests
- Next steps:
  1. Continue beginning-lane E6 with schedule/cooldown/provider-policy visibility if ownership remains clear.
  2. Defer provider-backed release radar scans until credentials, cooldowns, and network-impact policy are explicit.

## Update 2026-04-30 21:12:20Z

- Current task: Epic E13 browser-local Discovery Shelf from player ratings is implemented locally.
- Last activity:
  - added browser-local Discovery Shelf storage keyed by now-playing rating identity
  - wired player ratings into promote-preview, archive-preview, keep-reviewing, and expiry-watch shelf classifications
  - surfaced a player Discovery Shelf modal with summary counts, action previews, remove, and clear controls
  - recorded live media-server, peer search, queue, download, scrobble, and file-action handoffs as follow-up scope
  - validated broader player-focused tests, frontend lint, frontend production build, and whitespace checks
- Next steps:
  1. Continue E13 with preview policy refinement only if it avoids active DiscoveryInbox ownership.
  2. Wire real promote/archive/delete only after explicit backend preview and confirmation contracts exist.

## Update 2026-04-30 21:03:57Z

- Current task: Epic E6 browser-local Watchlists panel is implemented locally.
- Last activity:
  - added local watchlist storage for artist, label, playlist, and collection targets
  - surfaced Watchlists in Discovery Inbox with add, summary, manual scan preview, and send-to-review actions
  - kept watchlists local-only with no metadata provider lookup, Soulseek search, peer browse, download, scheduled automation, or file mutation
  - validated focused watchlist, Discovery Inbox storage, Discovery Inbox component tests, and frontend lint
- Next steps:
  1. Continue beginning-lane E6 with local filter controls for release type/country/format if ownership remains clear.
  2. Later connect watchlist scans to backend radar/provider APIs only after cooldowns, credentials, and network-impact policy are explicit.

## Update 2026-04-30 21:04:16Z

- Current task: E14 Listening Intelligence browser-local media-server play-history import is implemented locally.
- Last activity:
  - added CSV/JSON import parsing and duplicate suppression for local listening history
  - added JSON/CSV local history export helpers
  - surfaced paste, local file picker, import, and export controls in the player Listening Stats modal
  - validated focused listening/player tests, frontend lint, frontend production build, and whitespace checks
- Next steps:
  1. Continue tail-side burn-down with E13 only if it can stay low-overlap and avoids active Discovery Inbox ownership.
  2. Keep live media-server API import deferred until credentials, rate limits, and per-user mapping are explicit.

## Update 2026-04-30 21:06:05Z

- Current task: Realm subject-index governance proposal flow is implemented locally.
- Last activity:
  - added proposal/review models and pending/accepted/rejected status tracking for realm subject indexes
  - stored proposal and review decisions as realm governance documents when a governance client is available
  - kept pending and rejected proposals out of recording-MBID resolution, publishing only explicitly accepted revisions
  - validated focused `RealmSubjectIndexServiceTests`
- Next steps:
  1. Continue center-lane work with realm subject-index conflict display only if frontend lane ownership is clear, otherwise stay on backend-only federation follow-ups.
  2. Leave concurrent options, source-feed, transfer-test, search, wishlist, Discovery Inbox, and player edits untouched unless asked to integrate.

## Update 2026-04-30 20:59:18Z

- Current task: E14 Listening Intelligence recommendation seed handoffs are implemented locally.
- Last activity:
  - added deterministic recommendation seeds from browser-local forgotten favorites, top artists, and top genres
  - surfaced Recommendation Seeds in the player Listening Stats modal
  - used explicit Search action buttons with tooltips so no network search starts until the user chooses a seed
  - validated focused listening/player tests, frontend lint, frontend production build, and whitespace checks
- Next steps:
  1. Continue tail-side burn-down only in low-overlap local player/listening surfaces unless lane ownership changes.
  2. Defer external recommendation/provider imports until credentials, rate limits, and dedupe policy are explicit.

## Update 2026-04-30 20:55:53Z

- Current task: Discovery Inbox snooze due dates are implemented locally.
- Last activity:
  - added persisted `snoozedUntil` metadata to Discovery Inbox items
  - surfaced Snoozed until/Snooze due labels and an Unsnooze action
  - added focused storage and component coverage for snoozing, clearing snooze metadata, and overdue status
  - kept the slice local-only with no scheduler, provider lookup, peer browse, search, download, automation, or file mutation
  - validated focused Discovery Inbox tests and frontend lint
- Next steps:
  1. Continue beginning-lane burn-down with Watchlists/Release Radar only if it avoids active source-feed and MusicBrainz backend ownership.
  2. Later connect snooze due dates to backend job scheduling only after explicit automation policy and rate limits exist.

## Update 2026-04-30 20:47:21Z

- Current task: Discovery Inbox impact review summary is implemented locally.
- Last activity:
  - added local impact classification for Discovery Inbox candidates
  - surfaced batch-readiness counts and per-candidate impact labels
  - kept the slice evidence-only with no provider lookup, peer browse, search, download, automation, or file mutation
  - validated focused Discovery Inbox review helper/component tests and frontend lint
- Next steps:
  1. Continue beginning-lane burn-down with Watchlists/Release Radar only if it avoids active source-feed and MusicBrainz backend ownership.
  2. Later replace text-derived impact classification with backend-provided provider capability metadata when acquisition jobs are wired.

## Update 2026-04-30 20:55:34Z

- Current task: E14 Listening Intelligence browser-local genre breakdowns are implemented locally.
- Last activity:
  - stored genre/tag metadata in local listening history entries
  - added Top Genres to the player Listening Stats modal
  - validated focused listening history, player auto-queue, radio, shortcut, rating, and PlayerBar tests
- Next steps:
  1. Continue E14 with stats-derived recommendation seed previews if they stay browser-local and explicit.
  2. Add label breakdowns only after real label metadata is present in now-playing or imported history.

## Update 2026-04-30 20:50:41Z

- Current task: E14 Listening Intelligence time-range stats and forgotten favorites are implemented locally.
- Last activity:
  - added time-range filtering to browser-local listening stats
  - added forgotten-favorite derivation from older repeat local plays
  - surfaced 7D/30D/90D/All range controls and Forgotten Favorites in the player stats modal
  - validated focused listening history, player auto-queue, radio, shortcut, rating, and PlayerBar tests
- Next steps:
  1. Continue E14 with genre/label breakdowns only if derivable from local metadata.
  2. Defer media-server import and external recommendation providers until explicit credentials, rate limits, and dedupe policy are scoped.

## Update 2026-04-30 20:45:37Z

- Current task: E14 Listening Intelligence first browser-local history/stats slice is implemented locally.
- Last activity:
  - added local play history storage with duplicate suppression
  - recorded plays at the existing scrobble threshold in PlayerBar
  - added player Listening Stats modal with total, recent, top artists, and top tracks
  - validated focused listening history, player auto-queue, radio, shortcut, rating, and PlayerBar tests
- Next steps:
  1. Continue E14 with time-range stats or forgotten-favorites derivation if it can remain browser-local.
  2. Later add media-server play import only behind explicit credentials and deduplication policy.

## Update 2026-04-30 20:41:25Z

- Current task: E15 Player Enhancements bounded similar-track auto-queue is implemented locally.
- Last activity:
  - added browser-local player auto-queue scoring from recent session history
  - wired Queue Manager Auto-fill Similar to append non-queued recent tracks
  - kept auto-queue bounded to already-known tracks with no network or file effects
  - validated focused player auto-queue, radio, shortcut, rating, and PlayerBar tests
- Next steps:
  1. Continue tail-side burn-down with documentation polish for completed E15 player enhancements or move to the next earlier untouched epic.
  2. Later connect similar-track queueing to real recommendation providers only behind explicit user action and rate limits.

## Update 2026-04-30 20:40:26Z

- Current task: Epic 4 album decision rule previews are implemented locally.
- Last activity:
  - added browser-local album decision rule preview storage
  - captured album/search identity, expected track count, format policy, warnings, and substitution tracks
  - added a Save Rule action on album candidates with tooltip-backed local-only behavior
  - validated focused album decision rule, album candidate, Search component tests, and frontend lint
- Next steps:
  1. Continue beginning-lane burn-down with the next untouched Discovery Inbox/watchlist item only if it avoids current Wishlist/source-feed ownership.
  2. Later wire album decision rules into a backend-backed album decision planner after the persistence and download handoff contract is explicit.

## Update 2026-04-30 20:57:04Z

- Current task: MusicBrainz overlay trust-scoped routing is implemented locally.
- Last activity:
  - added opt-in PodCore route attempts for stored signed MusicBrainz overlay edits
  - constrained routing to explicit safe peer IDs and safe route metadata
  - recorded route attempts with target, routed, and failed peer IDs
  - validated focused MusicBrainz overlay tests, whitespace, lint, and full `dotnet test`
- Next steps:
  1. Continue center-lane work with realm subject-index governance proposals once current frontend lanes settle.
  2. Leave concurrent options, source-feed, transfer-test, search, wishlist, Discovery Inbox, and player edits untouched unless asked to integrate.

## Update 2026-04-30 20:48:35Z

- Current task: MusicBrainz overlay manual export review API is implemented locally.
- Last activity:
  - added overlay export review packages for stored signed MusicBrainz overlay edits
  - added explicit local export approval records with safe approver validation
  - kept the flow manual-only with no upstream MusicBrainz submitter and no cache mutation
  - validated focused MusicBrainz overlay tests, whitespace, lint, and full `dotnet test`
- Next steps:
  1. Continue center-lane work with trust-scoped overlay gossip or frontend review surfaces once lane ownership is clear.
  2. Leave concurrent options, source-feed, transfer-test, search, wishlist, and player edits untouched unless asked to integrate.

## Update 2026-04-30 20:39:16Z

- Current task: Quarantine Jury manual review/acceptance API is implemented locally.
- Last activity:
  - added a backend review projection for request evidence, signed verdicts, route attempts, aggregate recommendations, and acceptance status
  - added persisted manual release-candidate acceptance decisions
  - kept acceptance constrained to release-candidate supermajorities and local decision recording only
  - split the browser Quarantine Jury review UI into a follow-up to avoid current frontend lane overlap
  - validated focused Quarantine Jury tests, whitespace, lint, and full `dotnet test`
- Next steps:
  1. Continue center-lane work with MusicBrainz overlay export review or Quarantine Jury frontend UI once frontend lane ownership is clear.
  2. Leave concurrent player, search, wishlist, source-feed, and transfer-test edits untouched unless asked to integrate.

## Update 2026-04-30 20:38:05Z

- Current task: E15 Player Enhancements queue manager is implemented locally.
- Last activity:
  - exposed player history and clear-upcoming queue action through `PlayerContext`
  - added a Queue Manager modal with current, upcoming, and recent session sections
  - added remove queued item, clear upcoming, previous, next, and close controls
  - validated focused player radio, shortcut, rating, and PlayerBar tests
- Next steps:
  1. Continue tail-side burn-down with bounded similar-track auto-queue only from already-known local/session candidates.
  2. Later revisit queue persistence only after deciding whether the queue should survive browser reloads.

## Update 2026-04-30 20:34:08Z

- Current task: Epic 4 album substitution option hints are implemented locally.
- Last activity:
  - preserved per-track visible source options inside album candidates
  - surfaced substitution hints for tracks with multiple visible peer/provider options
  - fixed and documented the expected-track-count gotcha for duplicate alternates
  - kept the slice review-only with no alternate selection, rule save, peer browse, provider query, download, stream, metadata lookup, or file mutation
  - validated focused album candidate and Search component tests plus frontend lint
- Next steps:
  1. Continue beginning-lane burn-down with local album decision rule previews only if they stay browser-local and do not alter downloads.
  2. Later wire actual substitution selection into the download planner after an explicit album decision persistence contract exists.

## Update 2026-04-30 20:34:06Z

- Current task: T-939 non-Spotify provider URL expansion is implemented locally.
- Last activity:
  - added Apple Music/iTunes lookup for Apple URLs with numeric track or album ids
  - added ListenBrainz public-listens import from user profile URLs
  - added metadata-page fallback for YouTube, Bandcamp, Last.fm, and Apple URLs
  - added the new providers to the Wishlist Import Feed source-type selector
  - documented and committed the Apple Music track-id gotcha as `e08e73774`
  - validated focused source-feed tests, backend build, Wishlist tests, and frontend lint
- Next steps:
  1. Add authenticated/API-key provider adapters only where needed for full playlist expansion, starting with YouTube Data API and Last.fm API if users configure keys.
  2. Keep provider URL imports review-first and bounded by the existing import limit before any Discovery Inbox handoff.

## Update 2026-04-30 20:34:39Z

- Current task: E15 Player Enhancements smart-radio seed handoff is implemented locally.
- Last activity:
  - added a browser-local player radio plan helper
  - surfaced Smart Radio Seed queries from current track, album, artist, and genre metadata
  - wired explicit Search handoff buttons while keeping modal open non-networking
  - documented and committed the empty metadata array fallback gotcha as `939914cc7`
  - validated focused player radio, shortcut, rating, and PlayerBar tests
- Next steps:
  1. Continue tail-side burn-down with queue management or a bounded similar-track auto-queue from already-known local session items.
  2. Later connect smart radio to a real recommendation source only behind explicit rate limits and user action.

## Update 2026-04-30 20:29:12Z

- Current task: E15 Player Enhancements keyboard-shortcut slice is implemented locally.
- Last activity:
  - added a tested browser-local player shortcut mapper
  - wired PlayerBar keyboard handling for play/pause, seek, previous/next, mute, equalizer, lyrics, and visualizer controls
  - ignored text inputs, textareas, selects, contenteditable controls, and modified browser/system chords
  - validated focused player shortcut, player rating, and PlayerBar tests
- Next steps:
  1. Continue tail-side burn-down with smart-radio/similar-track shell only if it can stay review-only and avoid other active Search work.
  2. Later connect keyboard shortcut help to existing tooltip surfaces without adding visible instructional clutter.

## Update 2026-04-30 20:29:15Z

- Current task: Quarantine Jury pod routing attempts are implemented locally.
- Last activity:
  - added selected-juror route attempts through PodCore
  - persisted route attempt history with routed and failed juror lists
  - exposed Quarantine Jury route dispatch and history endpoints
  - kept invalid route targets and unsafe route metadata from contacting peers
  - marked the T-936 trust-scoped routing follow-up complete in the feature plan
  - validated focused jury tests, whitespace, lint, and full `dotnet test`
- Next steps:
  1. Continue the center-lane burn-down with manual Quarantine Jury review UI and accept flow if it does not overlap active frontend lanes.
  2. Leave player, search-detail, source-feed, and changelog changes from other lanes untouched unless asked to integrate.

## Update 2026-04-30 20:26:18Z

- Current task: Epic 4 album candidate review details are implemented locally.
- Last activity:
  - added album candidate format mix, missing-track, duration-spread, and warning summaries
  - surfaced the review details in Search album candidates with local-only confidence warning tooltips
  - kept the slice non-networking with no peer browse, provider query, metadata lookup, download, stream, file mutation, or rule save
  - validated focused album candidate, search deduplication, Search component tests, and frontend lint
- Next steps:
  1. Continue beginning-lane burn-down with manual track substitution scaffolding only if it can stay local and avoid transfer/download code overlap.
  2. Later add backend-backed album decision rules after the review surface has an explicit persistence contract.

## Update 2026-04-30 20:25:56Z

- Current task: E15 Player Enhancements first tail-side slice is implemented locally.
- Last activity:
  - added browser-local player rating storage and summary labels
  - preserved source provider, match-confidence, and verification metadata through `PlayerContext`
  - surfaced compact source, match, verified, and star-rating controls in the now-playing display
  - documented and committed the empty now-playing helper gotcha as `534c4d34c`
  - validated focused player ratings and PlayerBar tests
- Next steps:
  1. Continue tail-side burn-down with the next untouched E15 slice, likely keyboard shortcuts or smart-radio shell if it does not overlap the other agent.
  2. Later connect local ratings to Discovery Shelf ranking once that shelf has an explicit import/sync contract.

## Update 2026-04-30 20:23:47Z

- Current task: T-939 Spotify account connection follow-up is implemented locally.
- Last activity:
  - added Spotify OAuth authorization/status/disconnect API endpoints
  - store Spotify refresh tokens through ASP.NET DataProtection under the app directory
  - refresh connected-account access tokens server-side for private source-feed imports
  - wired Wishlist Import Feed to connect/disconnect Spotify and use the connected account automatically
  - documented and committed the raw-string JavaScript brace gotcha as `5a4ed9907`
  - validated backend build, focused source-feed tests, Wishlist tests, and frontend lint
- Next steps:
  1. Configure the Spotify app redirect URI to match `/api/v0/integrations/spotify/callback` on the deployed host or set `integrations.spotify.redirect_uri` explicitly.
  2. Extend provider fetchers to Apple Music/YouTube/Bandcamp/ListenBrainz/Last.fm after each provider's auth/rate-limit policy is chosen.

## Update 2026-04-30 20:18:09Z

- Current task: Epic 3 search-result deduplication is implemented locally.
- Last activity:
  - added browser-local duplicate media candidate folding after filtering, ranking, and sorting
  - kept the highest-ranked candidate visible and attached folded provider/peer metadata
  - added a visible Search option to disable duplicate folding when users want to inspect every source separately
  - kept the slice non-networking with no provider query, peer browse, download, stream, or metadata lookup changes
  - validated focused search deduplication/ranking/filter/Search tests and frontend lint
- Next steps:
  1. Continue beginning-lane burn-down with the next untouched Epic 4 interactive album picker surface, avoiding Discovery Inbox/Import Staging overlap.
  2. Later connect deduplication to backend provider identity once multi-provider execution returns canonical content hashes.

## Update 2026-04-30 20:18:22Z

- Current task: Quarantine Jury persistence follow-up is implemented locally.
- Last activity:
  - added atomic JSON persistence for Quarantine Jury requests and signed verdicts
  - reload persisted jury state on service startup
  - added focused rehydration coverage for requests, verdicts, and aggregate recommendations
  - validated focused jury tests, lint, whitespace, and full `dotnet test`
- Next steps:
  1. Route minimal signed jury evidence through explicitly selected trust-scoped mesh channels.
  2. Add a manual review UI that keeps local quarantine authoritative until explicit user acceptance.

## Update 2026-04-30 20:13:04Z

- Current task: T-936 Quarantine Jury first slice is implemented locally.
- Last activity:
  - added local Quarantine Jury request, evidence, verdict, signature, and aggregate models
  - added safe opaque evidence validation and selected-juror enforcement
  - added signed verdict payload-hash validation and duplicate juror replacement
  - added two-thirds recommendation aggregation without changing quarantine state
  - documented and committed the jury quorum rounding gotcha as `ac77d55c9`
  - validated focused jury tests, lint, whitespace, and full `dotnet test`
- Next steps:
  1. Persist Quarantine Jury requests/verdicts across daemon restarts.
  2. Route minimal signed jury evidence through explicitly selected trust-scoped mesh channels.

## Update 2026-04-30 20:42:00Z

- Current task: T-939 Source Feed Imports is implemented locally.
- Last activity:
  - added backend source-feed parsing for CSV, pasted text, M3U/PLS, and RSS/OPML
  - added Spotify provider fetching for public playlist/album/track/artist/user playlist URLs with app credentials
  - added per-import Spotify bearer token support for liked/saved tracks, saved albums, followed artists, and current-user playlists
  - added the Wishlist Import Feed preview flow and Discovery Inbox handoff
  - documented and committed the `HttpClient.Timeout` reuse gotcha
- Next steps:
  1. Add first-class OAuth token acquisition/storage if we want users to connect Spotify without pasting a temporary bearer token.
  2. Extend provider fetchers to Apple Music/YouTube/Bandcamp/ListenBrainz/Last.fm after each provider's auth/rate-limit policy is chosen.

## Update 2026-04-30 20:13:21Z

- Current task: None. slskdN-native config compatibility map is implemented locally.
- Last activity:
  - evaluated upstream config layout changes as reference-only because this fork is license-limited to 0.24.5
  - extended the existing YAML provider compatibility pass so `transfers.upload.limits` hydrates current global upload limits
  - added `transfers.groups` compatibility while retaining top-level `groups` and group upload-limit compatibility
  - added startup warnings for accepted legacy config shapes and updated README/config examples to prefer the new layout
  - validated project build, focused YAML/config-warning tests, `bash bin/lint`, and whitespace checks
  - full `dotnet test` still has one unrelated failure in `ContentVerificationServiceTests.VerifySourcesAsync_WhenDownloadThrows_ReturnsSanitizedFailureReason`
- Next steps:
  1. Resolve the unrelated content-verification probe-budget test failure before using full-suite status as a release gate.
  2. Keep future config-shape work in the local compatibility provider unless a breaking options-model migration is deliberately planned.

## Update 2026-04-30 20:24:00Z

- Current task: T-939 Source Feed Imports is planned.
- Last activity:
  - audited Spotify/source-feed coverage
  - confirmed existing support is limited to SongID single Spotify track URLs and Wishlist CSV playlist import
  - added Source Feed Imports as Phase 8 in `docs/design/music-discovery-federation-plan.md`
  - added T-939 to `memory-bank/tasks.md`
- Next steps:
  1. Implement the first slice as dry-run/review import into Discovery Inbox from CSV, pasted tracklists, and local playlist files.
  2. Keep Spotify/provider URL fetching disabled until explicit configuration, credentials, and rate limits are added.

## Update 2026-04-30 20:28:00Z

- Current task: T-939 Source Feed Imports first slice is implemented locally.
- Last activity:
  - added backend source-feed preview parsing for local artist/title/album rows
  - dedupe suggestions by normalized evidence key and report skipped rows
  - expose provider-token requirements while keeping provider fetch count at zero
- Next steps:
  1. Wire preview suggestions into Discovery Inbox review.
  2. Add explicit provider fetchers only behind configuration, credentials, and rate limits.

## Update 2026-04-30 20:21:05Z

- Current task: E16 Automation first follow-up slice is implemented locally.
- Last activity:
  - added cooldown, max-runtime, and approval-gate metadata to Automation Center recipes
  - persisted dry-run preview reports with network/file impact and `executed: false`
  - surfaced bounded automation details on recipe cards
  - kept all automation behavior shell-only with no recipe execution
  - validated focused Automation Center tests
- Next steps:
  1. Continue tail-side feature-expansion burn-down with E15 Player Enhancements if it does not overlap player/MilkDrop work.
  2. Later wire real automation execution through bounded backend jobs with cooldown enforcement and dry-run output review.

## Update 2026-04-30 20:17:39Z

- Current task: E17 Wishlist and Requests first slice is implemented locally.
- Last activity:
  - added derived Wishlist request summary logic
  - surfaced total, enabled, automatic, review-load, and quota-style remaining counts on Wishlist
  - kept request behavior read-only with no backend request portal, scheduling, approval, or download changes
  - validated focused acquisition request and Wishlist tests
- Next steps:
  1. Continue tail-side feature-expansion burn-down with E16 Automation if it does not overlap existing Automation Center work.
  2. Later add real requester identity, approval roles, and enforceable per-user quotas when backend auth/request portal work is scoped.

## Update 2026-04-30 20:14:27Z

- Current task: E18 Servarr Integration first slice is implemented locally.
- Last activity:
  - added a local Servarr readiness helper
  - added a System Integrations setup checklist for base URL, API key, wanted pull, completed import, and path-map sanity
  - kept the panel diagnostic-only with no indexer/download-client registration, wanted pull, or import action
  - validated focused Servarr helper and System Integrations tests
- Next steps:
  1. Continue tail-side feature-expansion burn-down with E17 Wishlist and Requests if it does not overlap current Discovery Inbox/Wishlist edits.
  2. Later wire real Servarr indexer/download-client compatibility behind explicit API endpoints and setup wizard checks.

## Update 2026-04-30 20:11:54Z

- Current task: E19 Media Server Integration first slice is implemented locally.
- Last activity:
  - added Plex, Jellyfin/Emby, and Navidrome readiness cards to System Integrations
  - added local path diagnostics for slskdN path, media-server path, and optional remote path mappings
  - kept the slice non-networking and optional; no scan, playlist sync, play-history import, or rating sync executes
  - validated focused media-server helper and System Integrations tests
- Next steps:
  1. Continue tail-side feature-expansion burn-down with E18 Servarr Integration only if it does not overlap the backend source-feed/Lidarr work.
  2. Later wire media-server adapters behind explicit configured URLs/tokens and setup checks.

## Update 2026-04-30 20:08:47Z

- Current task: E20 Diagnostics first slice is implemented locally.
- Last activity:
  - added a browser-side diagnostic bundle builder with secret redaction
  - added a System Info modal to inspect/copy the redacted YAML bundle
  - wired the bundle to current app state/options without contacting the daemon
  - kept diagnostic tests on synthetic fixture usernames/hosts only
  - validated focused diagnostic bundle helper and modal tests
- Next steps:
  1. Continue tail-side feature-expansion burn-down with E19 Media Server Integration or E18 Servarr Integration only if it does not overlap backend source-feed work.
  2. Later add daemon-side diagnostic bundle APIs for logs/checks with server-side redaction.

## Update 2026-04-30 20:04:02Z

- Current task: E21 Mesh Metadata Federation first slice is implemented locally.
- Last activity:
  - added browser-local Mesh Evidence Policy storage with private defaults
  - added inbound trust-tier controls and outbound publication toggles to the Mesh tab
  - kept provenance required as a non-negotiable safety invariant
  - documented the provenance sanitizer gotcha and committed it immediately as `76bc580b1`
  - validated focused mesh evidence policy and component tests
- Next steps:
  1. Continue tail-side feature-expansion burn-down with E20 Diagnostics if it does not overlap the other agent.
  2. Later connect this policy to signed mesh evidence ingestion/publication APIs once backend federation is ready.

## Update 2026-04-30 19:58:08Z

- Current task: E22 Community Quality Signals first slice is implemented locally.
- Last activity:
  - added browser-local peer quality signal storage and summaries
  - wired local quality signal context into Search candidate ranking
  - added a Search result-card affordance for local suspicious-candidate reports
  - surfaced local-only quality badges for peers with stored signals
  - validated focused community quality and candidate ranking tests
- Next steps:
  1. Continue tail-side feature-expansion burn-down with E21 Mesh Metadata Federation if it does not overlap the other agent.
  2. Later add a dedicated review/clear surface for accumulated local quality signals.

## Update 2026-04-30 19:53:39Z

- Current task: E23 Mobile Workflows first slice is implemented locally.
- Last activity:
  - converted Import Staging into labeled card-style review rows on narrow screens
  - made Discovery Inbox and Import Staging actions use full-width mobile touch targets
  - added mobile cell labels to staged import table cells for responsive CSS and test coverage
  - validated focused Discovery Inbox and Import Staging component tests
- Next steps:
  1. Continue tail-side feature-expansion burn-down with E22 Community Quality Signals or E21 Mesh Metadata Federation if the opposite-side agent stays on provider/search work.
  2. Run a browser screenshot sweep for the mobile review pages when the local daemon session is stable.

## Update 2026-04-30 20:08:00Z

- Current task: T-932 Per-artist release radar first slice is implemented locally.
- Last activity:
  - added a local artist-radar service/API with artist MBID subscriptions
  - require SongID-confirmed safe music WorkRefs before accepting observations
  - dedupe notifications deterministically and honor muted release groups
  - registered the service in DI without adding polling, peer browsing, searches, or downloads
  - added focused service/controller tests and follow-up tasks for persistence, trusted observation routing, and UI controls
  - validated focused radar tests, lint, and full `dotnet test`
- Next steps:
  1. Continue the center-lane burn-down with the next unclaimed middle feature once neighboring agents' latest work is visible.
  2. Wire accepted radar notifications into Discovery Inbox or Wishlist review actions when the UI agent is ready for the next middle-adjacent slice.

## Update 2026-04-30 19:48:36Z

- Current task: None. Rooms dark-text cleanup and duplicate room navigation fix are implemented locally.
- Last activity:
  - removed the redundant joined-room recovery rail so Rooms uses hydrated tabs as the single navigation surface
  - added shared dark-mode overrides for nested Semantic UI list text and reusable `UserCard` usernames
  - gave Rooms and Chat history/input/user regions distinct dark panel tones instead of matching wireframe boxes
  - documented the gotcha and committed it immediately as `747f999b6`
  - validated frontend lint, production build, static hardcoded dark-text scan, and whitespace checks
- Next steps:
  1. Run a browser screenshot sweep against the live authenticated app when a stable local daemon session is available.
  2. Keep future dark-theme fixes in shared CSS first unless a page has a specific local surface leak.

## Update 2026-04-30 19:46:09Z

- Current task: T-935 Decentralized MusicBrainz Edit Overlay first slice is implemented locally.
- Last activity:
  - added signed MusicBrainz overlay-edit models and validation
  - added deterministic local overlay storage and read-time release-graph application without mutating cached MusicBrainz data
  - added a dedicated overlay API returning original/effective graphs plus provenance
  - validated focused MusicBrainz overlay service/controller tests
- Next steps:
  1. Gossip signed overlay edits through trust-scoped mesh/realm channels.
  2. Add manual upstream MusicBrainz export/review for selected local corrections.

## Update 2026-04-30 19:36:05Z

- Current task: None. Player local-audio file explorer redesign is implemented locally.
- Last activity:
  - added a path-aware, paged `/api/v0/library/items/browser` endpoint for local/shared library browsing
  - replaced the player flat file picker with a fullscreen explorer modal with folder navigation, breadcrumbs, recursive search, duplicate collapse, copy counts, paging, and row play actions
  - documented the flat-picker gotcha and committed it immediately as `8ed962b59`
  - validated focused PlayerBar tests, focused LibraryItemsController tests, frontend lint, production build, and whitespace checks
- Next steps:
  1. Run the full repo `dotnet test` and `./bin/lint` when the surrounding dirty workspace is ready for broad validation.
  2. Consider adding backend folder-level counts from the share repository directly if very large libraries need faster root-folder summary generation.

## Update 2026-04-30 19:48:45Z

- Current task: Import Staging failed-import denylist is implemented locally.
- Last activity:
  - added browser-local failed-import denylist storage
  - key denylist entries by staged SHA-256 when available and file signature otherwise
  - rejecting staged rows now records denylist entries
  - matching re-adds are marked Failed instead of looping as fresh staged rows
  - validated focused import staging, fingerprint, and metadata matcher tests
- Next steps:
  1. Move back toward Plan P2 Decision-Ready Search if the opposite-side agent has not already covered candidate grouping/ranking.
  2. Later connect denylisted import evidence to backend import jobs once explicit library mutation exists.

## Update 2026-04-30 19:44:02Z

- Current task: Opt-in import fingerprint verification is implemented locally.
- Last activity:
  - added browser-local SHA-256 file fingerprinting for selected staging files
  - added the Import Staging Fingerprint on add toggle
  - persisted only digest metadata on staged rows
  - kept the flow local and non-mutating with no uploads, imports, tag writes, or remote fingerprint calls
  - validated focused fingerprint/import staging tests
- Next steps:
  1. Add failed-import denylist as the next P3 Safe Acquisition Pipeline story.
  2. Later add backend Chromaprint/AcoustID verification behind explicit profile settings and rate limits.

## Update 2026-04-30 19:41:00Z

- Current task: Local import metadata matcher is implemented locally.
- Last activity:
  - added browser-side filename metadata matching for Import Staging
  - surfaced confidence, parsed identity, evidence status, and warnings in staged rows
  - added row-level and bulk match controls that do not contact metadata services or mutate files
  - fixed and documented standalone filename track-number parsing as `8e24cd666`
  - validated focused metadata matcher and import staging tests
- Next steps:
  1. Add optional fingerprint verification controls as the next P3 Safe Acquisition Pipeline story.
  2. Later connect strong metadata matches to backend MusicBrainz/fingerprint services behind explicit user action and rate limits.

## Update 2026-04-30 19:35:00Z

- Current task: Import Staging first slice is implemented locally.
- Last activity:
  - added browser-local import staging persistence
  - added `/import-staging` navigation and review UI
  - added file-picker intake that records local file metadata only
  - added staged, ready, imported, rejected, and failed state handling without library mutation
  - validated focused import staging tests
- Next steps:
  1. Add a reusable metadata matcher service as the next P3 Safe Acquisition Pipeline story.
  2. Connect staged import rows to backend library scan/import APIs only after explicit review and mutation safeguards are in place.

## Update 2026-04-30 19:33:40Z

- Current task: Wishlist/request-state unification is implemented locally.
- Last activity:
  - added shared Web UI acquisition request state mapping over Wishlist and Discovery Inbox evidence
  - added Wishlist Request State labels for disabled, wanted, automatic, review, approved, snoozed, rejected, staged, imported, and failed states
  - added a Wishlist action to send a saved search to Discovery Inbox review without starting network or download work
  - validated focused acquisition request, Discovery Inbox, and Wishlist tests
- Next steps:
  1. Add import staging scan/review as the next P3 Safe Acquisition Pipeline story.
  2. Connect Approved Discovery Inbox requests to explicit backend acquisition planning once staging exists.

## Update 2026-04-30 19:28:39Z

- Current task: Discovery Inbox first slice is implemented locally.
- Last activity:
  - added browser-local Discovery Inbox persistence and review UI
  - wired `/discovery-inbox` navigation and route
  - added a Search-page action that saves the current phrase into the inbox with the selected acquisition profile
  - kept the slice review-only; no peer search, browse, or download starts from the inbox
  - fixed and documented rejected-evidence suppression as `f6d015a80`
  - validated focused Discovery Inbox/Search tests, frontend lint, and frontend production build
- Next steps:
  1. Connect Approved inbox candidates to backend-backed acquisition planning with explicit opt-in execution.
  2. Add candidate promotion from Discography Concierge, SongID, and Discovery Graph evidence sources.

## Update 2026-04-30 19:36:08Z

- Current task: T-934 Realm-Curated Subject Indexes first slice is implemented locally.
- Last activity:
  - added signed realm subject-index artifacts and validation under `Mesh/Realm/SubjectIndex`
  - added a local registry/resolver that returns realm/index/revision provenance for recording MBID matches
  - registered the service with realm DI
  - validated focused `RealmSubjectIndexServiceTests`
- Next steps:
  1. Route subject-index revisions through realm governance proposal/review/accept/reject flows.
  2. Add UI conflict display for disagreements between realm indexes, MusicBrainz, and other subscribed realms.

## Update 2026-04-30 19:27:01Z

- Current task: T-933 Federated Taste Recommendations backend is implemented locally.
- Last activity:
  - added `TasteRecommendationService`, request/result models, DI registration, and `/api/v0/taste-recommendations`
  - recommendations are computed locally from accepted inbound ActivityPub music WorkRefs and followed remote actors only
  - service-layer k-anonymity hides candidates until two trusted sources mention the same work and omits source actor IDs by default
  - validated focused `TasteRecommendationServiceTests`
- Next steps:
  1. Add a Web UI recommendation card surface with Discovery Graph/Wishlist actions.
  2. Extend ranking with explicit Discovery Graph neighborhood proximity once the UI surface is in place.

## Update 2026-04-30 19:17:59Z

- Current task: None. Web UI affordance audit/backfill is complete.
- Last activity:
  - added shared CSS affordances for hover, focus-visible, cursor, selectable-row, dropdown, checkbox, link, disabled, and reduced-motion states
  - backfilled accessible names/titles on compact player launcher buttons and chat/room/browse/user icon action controls
  - ran headless screenshots and a DOM audit across Search, Rooms, Chat, Browse, Downloads, Uploads, Wishlist, Users, and System
  - documented the icon-only affordance gotcha and committed it as `791e91e11`
  - validated frontend lint, production build, whitespace checks, and the focused headless DOM affordance audit
- Next steps:
  1. Commit the implementation and memory-bank updates with the current dirty workspace when requested.
  2. Extend the DOM audit if new dense tool pages add custom non-Semantic interactive controls.

## Update 2026-04-30 19:10:00Z

- Current task: Acquisition profile control surface is implemented locally.
- Last activity:
  - closed the local feature-expansion roadmap decisions, including visible Web UI automation enablement
  - added persisted Search-page acquisition profiles for the first download/search decision surface
  - validated focused Search tests, frontend lint, frontend production build, and whitespace checks
- Next steps:
  1. Wire the selected acquisition profile into backend search/candidate ranking metadata.
  2. Add an Automation Center shell that lists enabled and disabled recipes visibly.

## Update 2026-04-30 19:15:00Z

- Current task: Automation Center shell is implemented locally.
- Last activity:
  - added System -> Automations with visible recipe enablement, impact labels, cadence, and dry-run checkpoints
  - low-risk local recipes default enabled; higher-impact recipes are visible but disabled until enabled
  - validated focused Automation Center/Search tests, frontend lint, frontend production build, and whitespace checks
- Next steps:
  1. Wire acquisition profile intent into search request payloads and backend DTOs.
  2. Begin candidate-ranking/dry-run search plan support.

## Update 2026-04-30 19:08:57Z

- Current task: None. Dark theme color/contrast audit is complete.
- Last activity:
  - ran headless screenshots for Search, Rooms, Chat, Browse, Downloads, Uploads, Wishlist, Users, and System
  - ran computed contrast checks against the slskdN dark theme and fixed the remaining low-contrast stock Semantic UI green action button styling
  - confirmed the shared palette and already-present page styles now lean further into purple accents, panel separation, and shadow depth
  - documented the Semantic color-variant contrast gotcha and committed it as `a5da3f54a`
  - validated frontend lint, production build, focused contrast sweep, and screenshot capture
- Next steps:
  1. Commit the implementation and memory-bank updates with the current dirty workspace when requested.
  2. Keep any further visual polish centralized in `App.css` unless a page has a genuine local surface leak.

## Update 2026-04-30 19:26:00Z

- Current task: Native MilkDrop Phase 2 shader-side audio access is implemented locally.
- Last activity:
  - expanded translated shader FFT uniforms from 32 to 64 bins
  - added signed waveform uniform bins and `get_waveform(pos)` shader helper
  - validated shader translator/renderer tests, broader native/player tests, native browser smoke, frontend lint, and frontend build
- Next steps:
  1. Run final whitespace/status checks.
  2. Commit and push the shader audio access phase.

## Update 2026-04-30 19:22:00Z

- Current task: Native MilkDrop Phase 2 transition modes are implemented locally.
- Last activity:
  - added transition alpha mapping for crossfade, cut, fade-through-black, and overlay modes
  - added preset aliases for `transition_mode` and caller option support for transition mode overrides
  - validated focused native transition tests, broader native/player tests, native browser smoke, frontend lint, and frontend build
- Next steps:
  1. Run final whitespace/status checks.
  2. Commit and push the transition-mode phase.

## Update 2026-04-30 19:16:00Z

- Current task: Native MilkDrop Phase 2 dense wave/shape validation is implemented locally.
- Last activity:
  - added a dense 40-shape/20-wave curated MilkDrop fixture
  - made compatibility reporting and browser smoke cover the dense primitive fixture
  - included dirty app-wide affordance CSS for hover/focus/disabled/clickable row states
  - included dirty System Automation Center scaffold and missing button labels/titles in chat, rooms, users, and player browser entry points
  - validated focused automation/fixture/matrix tests, broader native/player tests, compatibility CLI, native browser smoke, frontend lint, and frontend production build
- Next steps:
  1. Run final whitespace/status checks.
  2. Commit and push this dense primitive validation pass.

## Update 2026-04-30 19:10:00Z

- Current task: Native MilkDrop Phase 2 q-register compatibility coverage is implemented locally.
- Last activity:
  - added q-register pressure metrics to the native compatibility matrix
  - added a MilkDrop3-style `.milk2` q-register fixture covering globals, waves, shapes, sprites, and translated shaders
  - included that fixture in native browser smoke coverage
  - included the dirty Search acquisition-profile selector scaffold and validated its focused test
  - validated focused fixture/matrix/search tests, broader native/player tests, compatibility CLI, frontend lint/build, and native browser smoke
- Next steps:
  1. Run final whitespace/status checks.
  2. Commit and push the q-register coverage phase.

## Update 2026-04-30 19:01:00Z

- Current task: Native MilkDrop Phase 2 `.milk2` composite controls plus dirty workspace checkpoint are validated locally.
- Last activity:
  - committed the Moq async-list test gotcha immediately as `55aab0634`
  - fixed remaining Movie/TV nullable hash/filename normalization warnings in the dirty VirtualSoulfind provider work
  - validated backend build, focused VirtualSoulfind provider tests, focused AdvancedDiscovery tests, frontend lint/build, focused MilkDrop tests, compatibility CLI, and native browser smoke
- Next steps:
  1. Run final whitespace/staged checks.
  2. Verify GitHub target, commit the remaining dirty workspace, and push to `snapetech/slskdn`.

## Update 2026-04-30 18:54:00Z

- Current task: Native MilkDrop Phase 2 `.milk2` composite controls are implemented locally.
- Last activity:
  - added secondary `.milk2` blend mode aliases for alpha, additive, screen, and multiply final compositing
  - added primary preset transition-duration aliases for imported renderer-set crossfades when the caller does not override the duration
  - plumbed composite modes into the WebGL final blit blend function
  - validated focused native/frontend tests, compatibility CLI, frontend lint, frontend production build, native browser smoke, and whitespace checks
- Next steps:
  1. Commit and push the `.milk2` composite-control phase with the dirty README change.
  2. Continue the next Phase 2 MilkDrop pass, likely additional transition modes or shader-side audio/texture coverage.

## Update 2026-04-30 18:56:00Z

- Current task: None. Fixed chrome scroll-region audit is complete.
- Last activity:
  - measured nav, player, and footer chrome into CSS variables used by the app scroll container
  - fixed player/footer safe-area double counting and nav bottom-edge under-reservation
  - documented the fixed-chrome overlap gotcha and committed it as `4e7320841`, `52fb656fb`, and `73326e5a8`
  - validated Search, Rooms, Chat, Browse, and System geometry at desktop and mobile sizes in expanded/collapsed player states
  - validated frontend lint, production build, and whitespace checks
- Next steps:
  1. Commit the implementation and memory-bank updates with the current dirty workspace when requested.
  2. Ignore unrelated dirty Mesh unit test files unless the task changes to backend mesh tests.

## Update 2026-04-30 18:45:00Z

- Current task: None. Browser storage cleanup now covers older UI paths too.
- Last activity:
  - moved remaining production direct storage calls to shared storage wrappers outside `src/web/src/lib/storage.js`
  - covered App, Search, Discovery Graph, Browse, Chat, Rooms, Users, System Network, Footer, blocked-user, and user-context persistence paths
  - verified the only production direct storage calls left are inside the storage helper itself
  - validated focused App/Search/System/player tests, frontend lint, frontend production build, and whitespace checks
- Next steps:
  1. Browser-check storage-disabled/private-mode behavior against a target browser if needed.
  2. Ignore the unrelated dirty Mesh unit test files unless the current task changes to backend mesh tests.

## Update 2026-04-30 18:42:00Z

- Current task: Native MilkDrop Phase 2 compatibility matrix is implemented locally.
- Last activity:
  - added a compatibility matrix module over parsed preset bodies with supported counts, unsupported functions/shaders, and max shape/wave/sprite metrics
  - added a `npm run test:native-milkdrop-compatibility` CLI that scans curated fixtures by default or supplied `.milk` / `.milk2` files and folders
  - added first high-count shape/wave metric coverage to measure real-preset-pack pressure before closing renderer gaps
  - validated focused compatibility/native tests, compatibility CLI, frontend lint, frontend production build, native browser smoke, focused GoldStar and transfer backend tests, shell syntax checks, and whitespace checks
- Next steps:
  1. Commit and push the compatibility-matrix phase.
  2. Continue the next Phase 2 MilkDrop pass from measured real-pack gaps.

## Update 2026-04-30 18:38:00Z

- Current task: Browser storage hardening is implemented locally and documented.
- Last activity:
  - routed remaining Visualizer localStorage reads/writes through safe storage helpers
  - documented browser storage access throwing in privacy-locked contexts in ADR-0001
  - validated Visualizer/native focused tests, frontend lint, frontend production build, and whitespace checks
- Next steps:
  1. Commit and push the complete dirty workspace per the current user instruction.
  2. Continue the next large MilkDrop phase, likely real-preset compatibility expansion.

## Update 2026-04-30 18:40:00Z

- Current task: None. Browser storage cleanup is implemented locally.
- Last activity:
  - added safe local/session storage helpers
  - moved ListenBrainz, token, player toggle, equalizer, and native MilkDrop preference persistence away from direct storage API calls
  - documented the storage gotcha and committed it as `fdeb67d0d`
  - validated focused player tests, frontend lint, frontend production build, and whitespace checks
- Next steps:
  1. Consider converting remaining direct localStorage reads in older App/Search/Browse/Rooms paths to the shared helper.
  2. Browser-check with storage disabled/private mode if a target browser still misbehaves.

## Update 2026-04-30 18:37:00Z

- Current task: Native MilkDrop translated shaders accept declared temp reassignment.
- Last activity:
  - added safe `=`, `+=`, `-=`, `*=`, and `/=` handling for declared local shader temps before final `ret`
  - kept assignment to undeclared names, duplicate `ret`, and statements after `ret` rejected by the translator
  - updated shader translator coverage, changelog, design notes, and T-938 progress notes
  - validated focused MilkDrop/native tests, frontend lint, frontend production build, native browser smoke, and whitespace checks
- Next steps:
  1. Commit and push the complete dirty workspace per the current user instruction.
  2. Continue the next large MilkDrop phase, likely real-preset compatibility expansion.

## Update 2026-04-30 18:36:00Z

- Current task: None. Semantic UI cleanup pass is implemented locally.
- Last activity:
  - added shared and player-local TooltipButton behavior for consistent Semantic UI button tooltips and labels
  - switched header links to active-aware navigation styling
  - added responsive table overflow handling for Semantic UI tables
  - made every Semantic UI tab non-remounting and controlled, including nested Pods/VPN tabs
  - validated `npm run lint`, `npm run build`, and `git diff --check`
- Next steps:
  1. Browser-check representative tabs and action buttons against a live dark-mode instance.
  2. Commit the code/test/memory-bank changes with the surrounding dirty workspace when ready.

## Update 2026-04-30 18:18:59Z

- Current task: Native MilkDrop shader translator unwraps safe shader_body blocks.
- Last activity:
  - translated shader parsing now unwraps `shader_body { ... }` wrappers before statement analysis
  - safe wrapped shader bodies still use the same straight-line declaration and `ret = ...` restrictions
  - curated shader fixture now exercises wrapped warp/comp shader bodies in parser, compatibility, and browser smoke paths
  - validated focused MilkDrop/native tests, frontend lint, frontend production build, and native browser smoke
- Next steps:
  1. Run whitespace checks, commit, and push.
  2. Continue the next large MilkDrop phase, likely shader texture access or real-preset compatibility coverage.

## Update 2026-04-30 18:29:01Z

- Current task: Native MilkDrop translated shaders have first simple conditional support.
- Last activity:
  - shader translation now rewrites safe ret-only `if (...) ret = ...; else ret = ...;` bodies into ternary expressions
  - conditional branches can still use translated texture samplers and generated coordinate helpers
  - complex control flow, temp declarations inside branches, loops, and missing-else if statements remain rejected
  - validated focused MilkDrop/native tests, frontend lint, frontend production build, native browser smoke, and whitespace checks
- Next steps:
  1. Commit and push the shader conditional phase.
  2. Continue the next large MilkDrop phase, likely real-preset compatibility coverage.

## Update 2026-04-30 18:26:01Z

- Current task: Native MilkDrop translated shaders have first named-texture sampler binding.
- Last activity:
  - shader translation now detects up to four non-main `tex`/`tex2D` sampler references and maps them to `shaderTexture0..3`
  - renderer binds those sampler uniforms to imported texture assets using existing alias lookup, with procedural fallback for missing assets
  - curated shader fixture now exercises a named sampler in browser smoke coverage
  - validated focused MilkDrop/native tests, frontend lint, frontend production build, and native browser smoke
- Next steps:
  1. Run whitespace checks, commit, and push.
  2. Continue the next large MilkDrop phase, likely shader language expansion or real-preset compatibility coverage.

## Update 2026-04-30 18:22:09Z

- Current task: None. All Semantic UI tabs now preserve inactive panes locally.
- Last activity:
  - applied `renderActiveOnly={false}` to every remaining `Tab` under `src/web/src/components`
  - verified no `Tab` usages are missing the prop
  - validated `npm run lint`, `npm run build`, and `git diff --check`
- Next steps:
  1. Browser-check representative tab switches in Browse, System, Pods, and Library Health against a live instance.
  2. Commit the code/test/memory-bank changes with the surrounding dirty workspace when ready.

## Update 2026-04-30 18:19:31Z

- Current task: None. Dark-mode surface cleanup and System integrations admin tab are implemented locally.
- Last activity:
  - added central dark-mode overrides for Semantic UI nested surfaces
  - replaced light hard-coded room and port-forwarding panel colors with theme variables
  - added System -> Integrations with VPN readiness/status/config summary and Lidarr status/wanted-sync/manual-import controls
  - added focused Integrations component tests
  - documented the Semantic UI dark-surface gotcha and committed it as `c25c26de0`
  - validated `npm test -- src/components/System/Integrations/index.test.jsx`, `npm run lint`, `npm run build`, and `git diff --check`
- Next steps:
  1. Browser-check `/chat`, `/rooms`, and `/system/integrations` against a live dark-mode instance.
  2. Commit the code/test/memory-bank changes with the surrounding dirty workspace when ready.

## Update 2026-04-30 18:12:28Z

- Current task: None. Header chat/room activity dots and tab-preservation fix are implemented locally.
- Last activity:
  - added header red dots for unread chat conversations and newer joined-room messages
  - kept chat and room `Tab` panes mounted so tab changes preserve the session instead of refreshing the visible panel
  - gated chat acknowledgment and room polling to active panes to avoid hidden-tab side effects
  - documented the Semantic UI tab remount gotcha and committed it as `d0cf8a675`
  - validated `npm test -- App.test.jsx`, `npm run lint`, and `npm run build`
- Next steps:
  1. Browser-check the header dots against a live daemon with real unread chat/room activity if desired.
  2. Commit the code/test/memory-bank changes with the surrounding dirty workspace when ready.

## Update 2026-04-30 18:14:52Z

- Current task: Native MilkDrop translated shaders have viewport context.
- Last activity:
  - generated shader fragments now expose `resolution`, `pixelSize`, `aspect`, and `texsize`
  - generated shader fragments also expose MilkDrop-style per-fragment `x`, `y`, `rad`, and `ang`
  - renderer binds viewport uniforms from the current WebGL canvas before translated warp/comp passes
  - validated focused MilkDrop/native tests, frontend lint, frontend production build, and native browser smoke
- Next steps:
  1. Run whitespace checks, commit, and push.
  2. Continue the next large MilkDrop phase, likely shader texture access or real-preset compatibility coverage.

## Update 2026-04-30 18:10:20Z

- Current task: Native MilkDrop shader translator accepts more safe straight-line bodies.
- Last activity:
  - added safe `float/float2/float3/float4` temp declaration support for translated warp/comp shaders
  - added helper aliases from common HLSL names to GLSL: `frac`, `fmod`, `rsqrt`, and `atan2`
  - compatibility scanning now accepts those straight-line shader bodies while still rejecting control flow and mutable reassignment
  - validated focused MilkDrop/native tests, frontend lint, frontend production build, and native browser smoke
- Next steps:
  1. Run whitespace checks, commit, and push.
  2. Continue the next large MilkDrop phase, likely shader uniform/texture expansion or real-preset compatibility coverage.

## Update 2026-04-30 18:06:55Z

- Current task: Native MilkDrop has first classic waveform mode support.
- Last activity:
  - expanded built-in waveform geometry beyond the default centered trace
  - added first support for `wave_mode`, `wave_x`, `wave_y`, `wave_a`, `wave_scale`, and `wave_smoothing`
  - renderer now passes preset scope into waveform generation and blends semi-transparent waveform output
  - added focused waveform geometry and render-call coverage
  - validated focused MilkDrop/native tests, frontend lint, frontend production build, and native browser smoke
- Next steps:
  1. Run whitespace checks, commit, and push.
  2. Continue the next large MilkDrop phase, likely richer shader translation or real-preset compatibility expansion.

## Update 2026-04-30 18:01:27Z

- Current task: Native MilkDrop has first classic screen-border rendering.
- Last activity:
  - added filled GPU ring geometry for classic MilkDrop outer and inner borders
  - renderer now draws `ob_size/ob_r/g/b/a` and `ib_size/ib_r/g/b/a` as alpha-blended screen borders
  - added focused geometry and render-call coverage for the border path
  - validated focused MilkDrop/native tests, frontend lint, frontend production build, and native browser smoke
- Next steps:
  1. Run whitespace checks, commit, and push.
  2. Continue the next large MilkDrop phase, likely richer waveform modes or shader translation.

## Update 2026-04-30 17:56:49Z

- Current task: Native MilkDrop primitives honor common MilkDrop field aliases.
- Last activity:
  - custom waves now read native aliases such as `nSamples`, `bSpectrum`, `bUseDots`, `bDrawThick`, and `bAdditive`
  - shapes and sprites now read native aliases such as `bEnabled`, `bTextured`, `bAdditive`, `bDrawThick`, `numSides`, and `texName`
  - parser and renderer coverage locks the alias-preservation path without rewriting imported preset data
  - validated focused MilkDrop/native tests, frontend lint, frontend production build, and native browser smoke
- Next steps:
  1. Run whitespace checks, commit, and push.
  2. Continue the next large MilkDrop phase, likely real-preset compatibility expansion or richer shader translation.

## Update 2026-04-30 17:45:25Z

- Current task: Native MilkDrop translated shaders have first shader-side FFT access.
- Last activity:
  - translated warp/comp shaders now declare a normalized 32-bin `fftBins` uniform array and `sampleRate`
  - shader-side `get_fft(pos)` and `get_fft_hz(freq)` helpers are generated into supported shader fragments
  - renderer binds FFT bins from the current analyzer spectrum before translated shader passes
  - validated focused MilkDrop/native tests, frontend lint, frontend production build, native browser smoke, and whitespace checks
- Next steps:
  1. Continue the next large MilkDrop phase, likely richer shader translation or higher-fidelity wave modes.

## Update 2026-04-30 17:35:22Z

- Current task: Native MilkDrop translated shaders have first q/audio uniform binding.
- Last activity:
  - translated warp/comp fragment shaders now declare q1-q64 plus bass/mid/treble audio uniforms
  - native renderer binds those uniforms from the current frame scope before translated shader passes
  - compatibility tests now accept supported shader expressions that reference q-register and audio variables
  - validated focused MilkDrop/native tests, frontend lint, frontend production build, native browser smoke, and whitespace checks
- Next steps:
  1. Continue the next large MilkDrop phase, likely shader-side FFT texture/audio access or richer shader translation.

## Update 2026-04-30 17:31:18Z

- Current task: Native MilkDrop renderer has q1-q64 frame-scope propagation.
- Last activity:
  - added explicit q1-q64 initialization to native renderer frame scope
  - added q-register extraction/merge helpers so primitive-local q writes propagate back into the render frame
  - custom waves, shapes, and sprites now pass q-register state forward while still not leaking non-q primitive-local frame/audio values into primitive base values
  - validated focused MilkDrop/native tests, frontend lint, frontend production build, native browser smoke, and whitespace checks
- Next steps:
  1. Continue the next large MilkDrop phase, likely deeper shader/audio compatibility.

## Update 2026-04-30 17:24:08Z

- Current task: Native MilkDrop has first browser-local preset playlists.
- Last activity:
  - added saved native preset playlists stored in browser local storage
  - users can save the current visible preset bank, including search/favorites filters, as a named playlist
  - users can select, clear the active scope, or delete a playlist from the visualizer overlay
  - native next/random/previous navigation now scopes through active playlist, favorites, and search together
  - validated focused native-engine/player tests, frontend lint, frontend production build, native browser smoke, and whitespace checks
- Next steps:
  1. Continue the next large MilkDrop phase, likely richer playlist editing or deeper shader/audio compatibility.

## Update 2026-04-30 17:19:40Z

- Current task: Native MilkDrop preset-bank search is implemented locally.
- Last activity:
  - added a compact native preset-bank search input to the visualizer overlay
  - search matches imported preset title/file name, persists in browser storage, and clears with an explicit affordance
  - favorites-only mode and search compose into one visible bank
  - native next/random navigation now uses the visible filtered bank and disables next when the filter has no matches
  - validated focused native-engine/player tests, frontend lint, frontend production build, native browser smoke, and whitespace checks
- Next steps:
  1. Continue the next large MilkDrop phase, likely saved playlist sequencing or deeper shader/audio compatibility.

## Update 2026-04-30 17:10:31Z

- Current task: Native MilkDrop has first browser-local preset-bank controls.
- Last activity:
  - visualizer UI now treats imported native presets as a local bank for next-preset cycling instead of falling back to built-in presets
  - added favorite/unfavorite, favorites-only selector filtering, previous manual-selection history, and random imported-preset jumps
  - persisted favorites and library filter mode in browser local storage
  - validated focused native-engine/player tests, frontend lint, frontend production build, native browser smoke, and whitespace checks
- Next steps:
  1. Continue the next large MilkDrop phase, likely search/playlists or deeper shader/audio compatibility.

## Update 2026-04-30 16:52:16Z

- Current task: Native MilkDrop has first beat and timed automatic preset changes.
- Last activity:
  - native engine now supports `off`, `beat`, and `timed` preset automation
  - beat mode derives low-frequency spectrum energy from analyzer FFT data and advances after repeated detected bass beats
  - timed mode advances native presets on an interval
  - visualizer UI adds a tooltipped automation mode button, persists the selected mode locally, and updates the displayed preset when automation advances inside the render loop
  - validated focused native-engine/player tests, frontend lint, frontend production build, native browser smoke, and whitespace checks
- Next steps:
  1. Continue Phase 2/3 MilkDrop work, likely richer random/history preset selection or fragment library management.

## Update 2026-04-30 16:44:07Z

- Current task: Native MilkDrop has first `.shape` and `.wave` fragment import/export.
- Last activity:
  - parser now handles standalone `.shape`/`.wave` fragments and prefixed fragment-style files
  - active native presets can be reserialized after merging imported shape/wave fragments
  - native engine merges fragments into the active preset, compatibility-checks before renderer replacement, and exposes fragment export
  - visualizer UI accepts `.shape`/`.wave` imports, persists merged presets into the browser-local library, and adds tooltipped shape/wave export affordances
  - validated focused parser/native-engine/player tests, frontend lint, frontend production build, native browser smoke, and whitespace checks
- Next steps:
  1. Continue Phase 2 MilkDrop3 deltas, likely beat-driven preset changes or richer fragment library management.

## Update 2026-04-30 15:58:21Z

- Current task: Native MilkDrop has first renderer-set transitions and `.milk2` composite-alpha control.
- Last activity:
  - native preset switches and imported preset loads now crossfade renderer sets instead of immediately disposing the outgoing renderer
  - transition progress uses a bounded eased curve and disposes retired renderer sets after fade-out
  - `.milk2` secondary presets can now use `blend_alpha`, `blendalpha`, or `composite_alpha` to control overlay opacity
  - updated the curated `.milk2` fixture and WebGL MilkDrop3 port plan
  - validated focused native engine/parser/fixture/player tests, frontend lint, frontend production build, native browser smoke, and whitespace checks
- Next steps:
  1. Continue Phase 2 MilkDrop3 deltas, likely `.shape`/`.wave` import/export or beat-driven preset changes.

## Update 2026-04-30 15:38:35Z

- Current task: Native MilkDrop preset packs have a folder import affordance.
- Last activity:
  - added a separate native preset-folder import button and hidden directory input
  - directory imports reuse the existing native preset import path and browser-provided relative paths
  - added component coverage for folder input attributes, button click behavior, and relative asset path persistence
  - validated focused native/player tests, native browser smoke, frontend lint, frontend production build, and whitespace checks
- Next steps:
  1. Commit and push the folder-import phase.
  2. Continue with the next larger native MilkDrop phase, likely pack ergonomics or richer transition controls.

## Update 2026-04-30 15:26:43Z

- Current task: Native MilkDrop pack imports scope image assets per preset.
- Last activity:
  - import now indexes selected image assets by browser-provided relative path when available, plus basename and stem aliases
  - each preset source is scanned for shape/sprite texture references before it is stored
  - browser-local preset entries now persist only the referenced image assets instead of the whole selected image batch
  - added component coverage for a two-preset pack where each preset keeps only its own referenced image
  - validated focused native/player tests, native browser smoke, frontend lint, frontend production build, and whitespace checks
- Next steps:
  1. Commit and push the scoped preset-pack import phase.
  2. Continue with the next larger native MilkDrop phase, likely preset pack ergonomics or richer transition controls.

## Update 2026-04-30 15:21:59Z

- Current task: Native MilkDrop has first sprite/image primitive support.
- Last activity:
  - parser now recognizes `spriteNN_` indexed primitive entries with base values and init/frame equations
  - compatibility analysis now checks sprite equations for unsupported functions before import
  - renderer evaluates sprite init/frame equations and draws enabled sprites as textured quads
  - sprite texture lookup uses the same imported asset aliases as textured shapes and falls back to the procedural checker
  - curated classic fixture now includes a sprite, and smoke continues to render all three native fixtures
  - validated focused native/player tests, native browser smoke, frontend lint, frontend production build, and whitespace checks
- Next steps:
  1. Commit and push the sprite primitive phase.
  2. Continue with the next larger renderer phase, likely richer `.milk2` transition controls, higher shape/wave counts, or preset library controls.

## Update 2026-04-30 15:04:01Z

- Current task: Native `.milk2` double presets have a first simultaneous composite renderer.
- Last activity:
  - renderer final composite output now supports explicit alpha and optional screen clearing
  - native engine creates one renderer per compatible `.milk2` preset body
  - primary preset bodies render normally and secondary bodies blend over the primary at half opacity
  - imported `.milk2` overlay titles now include both preset names when available
  - browser smoke now renders `classic-primitives`, `shader-subset`, and `milk2-double`
  - validated focused native/player tests, native browser smoke, frontend lint, frontend production build, and whitespace checks
- Next steps:
  1. Commit and push the `.milk2` composite phase.
  2. Continue with the next larger renderer feature phase, likely sprite/image handling or richer `.milk2` transition controls.

## Update 2026-04-30 14:43:28Z

- Current task: Native `.milk2` imports now compatibility-check every preset body.
- Last activity:
  - fixed native import/inspection to analyze all parsed `.milk2` preset bodies before accepting the file
  - unsupported secondary preset bodies now reject import with a preset-indexed compatibility message
  - renderer still uses the primary body until simultaneous double-preset compositing lands
  - documented the gotcha in ADR-0001 and committed it as `124f398ef`
  - validated focused native/player tests, multi-fixture native browser smoke, frontend lint, frontend production build, and whitespace checks
- Next steps:
  1. Commit and push the `.milk2` safety slice.
  2. Start the next renderer gap after `.milk2` import safety, likely real double-preset compositing or broader sprite/image handling.

## Update 2026-04-30 14:40:57Z

- Current task: Native MilkDrop texture lookup handles common preset-pack names.
- Last activity:
  - renderer texture lookup now strips quote wrappers and normalizes Windows-style path separators
  - both imported texture asset keys and preset shape texture references expand to full path, basename, and stem aliases
  - texture disposal now deduplicates aliased texture handles
  - added renderer coverage for a quoted `textures\\cover.png` preset reference resolving against an imported `cover` asset
- Next steps:
  1. Continue fixture coverage with real-world texture-pack examples.
  2. Start the next renderer gap after texture lookup, likely broader sprite/image handling or `.milk2` simultaneous rendering.

## Update 2026-04-30 14:38:26Z

- Current task: Native MilkDrop imports now report skipped texture assets.
- Last activity:
  - native preset import now reports oversized, unreadable, or unsupported selected texture files in the visualizer overlay
  - texture assets remain capped at 1 MB and still persist only with imported browser-local preset entries
  - added component coverage for skipped texture-asset reporting
  - validated focused native/player tests, multi-fixture native browser smoke, frontend lint, and frontend production build
- Next steps:
  1. Continue expanding real-world preset fixture coverage for imported texture packs.
  2. Add richer texture lookup rules if real preset packs expose naming/layout gaps.

## Update 2026-04-30 14:31:46Z

- Current task: Native MilkDrop imports can carry local texture assets.
- Last activity:
  - native preset import now separates `.milk` / `.milk2` preset files from image files selected in the same batch
  - small image files are stored as data-URL texture assets keyed by filename, basename, and stem
  - imported preset library entries persist their texture asset maps and pass them back into the native engine on reload
  - renderer resolves parsed shape texture names against named texture assets before falling back to the procedural checker
  - added component, native engine, and renderer coverage for texture asset import and renderer plumbing
  - validated focused native/player tests, native browser smoke, frontend lint, frontend production build, and whitespace checks
- Next steps:
  1. Add user-facing feedback for skipped oversized/unsupported image assets if needed.
  2. Continue expanding real-world preset fixture coverage for imported texture packs.

## Update 2026-04-30 14:12:13Z

- Current task: Native MilkDrop browser smoke now covers multiple curated fixtures.
- Last activity:
  - native browser smoke renders both `classic-primitives` and `shader-subset`
  - smoke reports per-fixture lit-pixel and channel-total stats
  - smoke fails if either fixture renders blank, covering textured-shape and shader paths in Chromium
  - validated focused native/player tests, frontend lint, frontend production build, and whitespace checks
- Next steps:
  1. Continue replacing the procedural texture placeholder with explicit user/preset asset loading once asset lookup rules are defined.
  2. Add more real-world fixture cases as shader and texture support expands.

## Update 2026-04-30 14:08:41Z

- Current task: Native MilkDrop has first procedural textured-shape rendering.
- Last activity:
  - added a WebGL2 textured-shape shader and dynamic UV buffer path
  - generated a small procedural checker texture for parsed shape texture references instead of loading external assets
  - textured shapes now render when `textured`, `texture`, `tex`, or `tex_name` shape fields are present
  - added renderer tests for textured shape detection, UV mapping, texture binding, tint, alpha, and draw calls
  - updated the curated classic fixture to include a textured shape
  - validated focused native/player tests, native browser smoke, frontend lint, frontend production build, and whitespace checks
- Next steps:
  1. Replace the procedural texture placeholder with explicit user/preset asset loading once asset lookup rules are defined.
  2. Expand smoke coverage to include the textured-shape fixture once browser pixel thresholds are stable for that pass.

## Update 2026-04-30 05:45:24Z

- Current task: Native MilkDrop has a first curated preset fixture pack.
- Last activity:
  - added `nativeMilkdropFixturePack` with classic primitive, supported shader, simple `.milk2`, and unsupported shader-control-flow fixtures
  - added golden parser summary tests and fixture compatibility expectation tests
  - changed the browser native MilkDrop smoke to render the shared shader fixture instead of an inline ad hoc preset
  - validated focused native/player tests, native browser smoke, frontend lint, frontend production build, and whitespace checks
- Next steps:
  1. Expand the fixture pack with real-world presets as shader/texture compatibility grows.
  2. Continue remaining Phase 1 renderer work, especially texture support.

## Update 2026-04-30 05:41:26Z

- Current task: Native MilkDrop has a first safe shader translation/execution subset.
- Last activity:
  - added a shader translator for simple `ret = ...` shader bodies
  - supported `tex2D(sampler_main, uv)`, `saturate`, and `lerp` substitutions into WebGL2 GLSL
  - renderer now compiles supported `warp_shader` bodies into the feedback pass and supported `comp_shader` bodies into the final composite pass
  - compatibility analysis now rejects only unsupported shader bodies instead of all shader-bearing presets
  - added translator, compatibility, and renderer tests for supported and unsupported shader bodies
  - validated focused native/player tests, native browser smoke, frontend lint, frontend production build, and whitespace checks
- Next steps:
  1. Expand shader compatibility with real preset fixtures and golden/smoke coverage.
  2. Add broader MilkDrop3 shader/HLSL constructs incrementally from those fixtures.

## Update 2026-04-30 05:36:58Z

- Current task: Native MilkDrop local preset library supports per-preset removal.
- Last activity:
  - selector state now tracks the active native preset id
  - added a tooltipped remove-selected button for deleting one imported preset from browser storage
  - removing the active imported preset clears the persisted active preset but keeps the rest of the library
  - added component coverage for removing one preset while retaining another
- Next steps:
  1. Run the focused native/player tests, native browser smoke, frontend lint, frontend production build, and whitespace checks.
  2. Continue toward richer preset-pack management or shader translation after validation.

## Update 2026-04-30 05:33:38Z

- Current task: Native MilkDrop local preset library has a first cleanup affordance.
- Last activity:
  - added a tooltipped clear-library button in native visualizer mode when imported presets exist
  - clearing removes the last imported native preset and the capped local preset library from browser storage
  - added component coverage that imports a preset, clears the library, and verifies the selector disappears
  - validated focused native/player tests, native browser smoke, frontend lint, frontend production build, and whitespace checks
- Next steps:
  1. Continue toward richer preset-pack management or shader translation.
  2. Add per-preset removal or search/filter once the overlay has enough imported presets to justify it.

## Update 2026-04-30 05:28:58Z

- Current task: Native MilkDrop expression compatibility now includes unary and shift operators.
- Last activity:
  - added token/parser support for `!`, `~`, `<<`, and `>>`
  - extended VM coverage for shift and unary expressions alongside bitwise/logical operators
  - validated focused native/player tests, native browser smoke, frontend lint, frontend production build, and whitespace checks
- Next steps:
  1. Continue toward shader translation or larger preset-pack compatibility.
  2. Add richer native preset library management once more compatibility gaps are closed.

## Update 2026-04-30 05:27:23Z

- Current task: Native MilkDrop expression compatibility now accepts inline bitwise/logical operators.
- Last activity:
  - expression tokenization now recognizes `&`, `|`, `^`, `&&`, and `||`
  - parser precedence now evaluates bitwise and logical operators after arithmetic/comparison expressions
  - added VM tests for inline bitwise/logical expressions and unsupported syntax preservation
  - validated focused native/player tests, native browser smoke, frontend lint, frontend production build, and whitespace checks
- Next steps:
  1. Continue toward shader translation or larger preset-pack compatibility.
  2. Expand native expression compatibility based on real preset syntax.

## Update 2026-04-30 05:25:10Z

- Current task: Native MilkDrop preset imports now support small local batches.
- Last activity:
  - local native preset import accepts multiple `.milk` / `.milk2` files
  - compatible presets are added to the capped browser-local library and the last compatible preset becomes active
  - incompatible presets are skipped with a visible skipped-file summary instead of aborting the whole batch
  - added component coverage for batch import, skipped shader-bearing presets, persisted active preset, and library ordering
  - validated focused native/player tests, native browser smoke, frontend lint, frontend production build, and whitespace checks
- Next steps:
  1. Continue with shader translation or richer preset-pack management.
  2. Expand native expression compatibility based on real preset syntax.

## Update 2026-04-30 05:21:39Z

- Current task: Native MilkDrop preset imports now have a small browser-local library.
- Last activity:
  - imported `.milk` / `.milk2` presets are added to a capped local library instead of only replacing the last preset
  - native mode shows a compact preset selector in the visualizer overlay when imported presets exist
  - selecting a saved import reloads it through the same compatibility and runtime safety path as a fresh import
  - updated the WebGL MilkDrop port design note and task log for the library slice
  - validated focused player/native MilkDrop tests, native browser smoke, frontend lint, frontend production build, and whitespace checks
- Next steps:
  1. Continue with shader translation or richer native preset-pack management.
  2. Add bulk preset-pack import/search once the native compatibility report is useful for real packs.

## Update 2026-04-30 05:15:41Z

- Current task: Native MilkDrop import-time compatibility reporting is implemented locally.
- Last activity:
  - exported supported-function checks from the expression VM
  - added a compatibility analyzer that scans global, shape, and wave equations for unsupported functions
  - import now rejects presets with unsupported equation functions or pending `warp_shader` / `comp_shader` sections before replacing the active renderer
  - added focused compatibility and native adapter tests
  - validated focused MilkDrop/player tests, native browser smoke, frontend lint, and frontend production build
- Next steps:
  1. Decide whether shader-bearing presets should be rejected or loaded with shader warnings once a partial shader translator exists.
  2. Add a preset library/list UI after compatibility reporting is useful enough for real preset packs.

## Update 2026-04-30 05:12:52Z

- Current task: Native MilkDrop expression compatibility is expanded locally.
- Last activity:
  - added common NSEEL helpers/constants to the expression VM: `pi`, `e`, inverse trig, `atan2`, `tan`, logs, `exp`, `sign`, `sigmoid`, `rand`, and bitwise helper functions
  - added tests for helper behavior, clamping, integer rand bounds, and constants
  - validated focused MilkDrop/player tests, native browser smoke, frontend lint, and frontend production build
- Next steps:
  1. Build an unsupported-feature report for imported presets.
  2. Add a preset library/list UI after compatibility reporting is useful.

## Update 2026-04-30 05:09:32Z

- Current task: Native MilkDrop runtime preset errors are surfaced locally.
- Last activity:
  - native/Butterchurn visualizer render loop now catches engine render failures
  - native render errors show the underlying unsupported syntax/function detail in the overlay
  - persisted imported native preset text is cleared when render fails so bad imports do not poison future sessions
  - documented the render-loop gotcha and committed it as `bf9e51b3a`
  - validated focused MilkDrop/player tests, native browser smoke, frontend lint, and frontend production build
- Next steps:
  1. Add a preset library/list UI after compatibility reporting matures.
  2. Expand the native parser/VM support matrix with prioritized unsupported functions found from real presets.

## Update 2026-04-30 05:06:48Z

- Current task: Native MilkDrop local preset import is implemented locally.
- Last activity:
  - added native engine `loadPresetText(source, fileName)`
  - added a tooltipped `.milk` / `.milk2` import button when the native engine is active
  - imported preset text is loaded into the native renderer, displayed by name, and persisted in local storage for the next native session
  - added component coverage for switching to native mode and importing a local preset
  - validated focused MilkDrop/player tests, native browser smoke, frontend lint, and frontend production build
- Next steps:
  1. Add clearer unsupported-syntax diagnostics when imported presets fail.
  2. Add a preset library/list UI after unsupported-feature reporting is useful.

## Update 2026-04-30 05:03:40Z

- Current task: Native MilkDrop browser canvas smoke coverage is implemented locally.
- Last activity:
  - added `scripts/smoke-native-milkdrop.mjs`
  - smoke script starts a Vite server, opens Chromium, imports the real native renderer modules, renders a curated preset, and reads canvas pixels
  - smoke fails on blank output using lit-pixel and channel-total thresholds
  - exposed the smoke as `npm run test:native-milkdrop-smoke`
  - validated focused MilkDrop/player tests, native browser smoke, frontend lint, and frontend production build
- Next steps:
  1. Add user preset loading/import for the native engine.
  2. Add visualizer error surfacing for unsupported native preset syntax.

## Update 2026-04-30 04:57:05Z

- Current task: Native MilkDrop engine is player-selectable locally.
- Last activity:
  - added a `createNativeMilkdropEngine` adapter around the WebGL renderer
  - native engine reads waveform and frequency data from the shared Web Audio visualizer tap through its own analyser
  - added curated native smoke presets exercising warp grid, motion vectors, shapes, waveform, spectrum, and dots
  - added a tooltipped visualizer overlay button to switch between Butterchurn and `slskdN native`, with the choice persisted in local storage
  - validated focused MilkDrop/player tests, frontend lint, and frontend production build
- Next steps:
  1. Add a Playwright/browser canvas smoke test for the native engine path.
  2. Add user preset loading once the native path has browser-level smoke coverage.

## Update 2026-04-30 04:53:44Z

- Current task: First motion-vector primitive rendering is implemented locally.
- Last activity:
  - added motion-vector geometry from `mv_x`, `mv_y`, `mv_dx`, `mv_dy`, and `mv_l`
  - added motion-vector color/alpha from `mv_r/g/b/a` with frame-color fallback
  - renderer draws motion vectors as alpha-blended WebGL line segments into the feedback target
  - validated focused MilkDrop/player tests, frontend lint, and frontend production build
- Next steps:
  1. Add renderer/player selection for the native MilkDrop engine behind an explicit opt-in control.
  2. Add a browser canvas smoke test once the native path is reachable from the player UI.

## Update 2026-04-30 04:52:13Z

- Current task: First per-pixel warp-grid renderer is implemented locally.
- Last activity:
  - fixed WebGL attribute rebinding before every fullscreen, primitive, wave, shape, and warp-grid draw path
  - documented the attribute-state gotcha and committed it as `103d4c2a0`
  - added a dedicated warp-grid shader and buffers
  - presets with global `per_pixel` equations now draw a CPU-evaluated triangle grid using `x`, `y`, `rad`, `ang`, and local `dx/dy/zoom/rot` values
  - validated focused MilkDrop/player tests, frontend lint, and frontend production build
- Next steps:
  1. Add motion-vector rendering.
  2. Add a real browser canvas smoke test for the native renderer path once it is player-selectable.

## Update 2026-04-30 04:48:19Z

- Current task: First analyzer-backed FFT expression helpers are implemented locally.
- Last activity:
  - added `get_fft(pos)` and `get_fft_hz(freq)` to the MilkDrop expression VM
  - renderer frame scope now carries frequency data and sample rate for preset equations
  - tests cover byte-frequency normalization and Hz-to-bin mapping
  - validated focused MilkDrop/player tests, frontend lint, and frontend production build
- Next steps:
  1. Start the first translated per-pixel/per-vertex warp grid slice.
  2. Add shader-side FFT access later when translated shader execution exists.

## Update 2026-04-30 04:46:19Z

- Current task: First custom wave dot and spectrum modes are implemented locally.
- Last activity:
  - added point-size support to the primitive shader
  - custom waves with `dots` now draw as WebGL points instead of line strips
  - custom waves with `spectrum` use frame frequency data when available and normalize byte-frequency bins
  - validated focused MilkDrop/player tests, frontend lint, and frontend production build
- Next steps:
  1. Start the first translated per-pixel/per-vertex warp grid slice.
  2. Add `get_fft` / `get_fft_hz` expression support once analyzer frequency data is passed through the native renderer path.

## Update 2026-04-30 04:43:31Z

- Current task: First custom wave equation rendering is implemented locally.
- Last activity:
  - added custom wave init/frame state evaluation with q-register persistence and no frame/audio scope leakage
  - evaluates custom wave point equations into WebGL line-strip vertices using audio samples as point inputs
  - draws enabled custom waves with color/alpha, additive blending, and thick line hints
  - validated focused MilkDrop/player tests, frontend lint, and frontend production build
- Next steps:
  1. Add custom wave dots and spectrum modes.
  2. Start the first translated per-pixel/per-vertex warp grid slice.

## Update 2026-04-30 04:41:36Z

- Current task: First shape gradients and thick-outline hints are implemented locally.
- Last activity:
  - changed primitive rendering to use per-vertex color buffers
  - added center-to-edge custom shape fill gradients from `r2/g2/b2/a2`
  - maps `thickoutline` to a WebGL line width hint before restoring the normal width
  - validated focused MilkDrop/player tests, frontend lint, and frontend production build
- Next steps:
  1. Add custom wave point equations and wave modes.
  2. Add textured shape support once texture loading/caching exists.

## Update 2026-04-30 04:39:15Z

- Current task: First filled/bordered custom shape rendering is implemented locally.
- Last activity:
  - added triangle-fan fill vertices for enabled custom shapes
  - added border line strips, alpha blending, and parsed `additive` blend mode handling
  - tests cover fill geometry, fill/border color clamping, normal blending, and additive blending
  - validated focused MilkDrop/player tests, frontend lint, and frontend production build
- Next steps:
  1. Add gradient second-color and thick-outline behavior for custom shapes.
  2. Add custom wave point equations after the current shape modes are stable.

## Update 2026-04-30 04:36:34Z

- Current task: First custom shape equation evaluation is implemented locally.
- Last activity:
  - added custom shape init/frame equation evaluation to the WebGL MilkDrop renderer
  - persists shape-owned values and q-registers while keeping frame/audio globals as read-only inputs
  - renderer tests cover animated shape state, init-once behavior, q-register persistence, and no global scope leakage
  - validated focused MilkDrop/player tests, frontend lint, and frontend production build
- Next steps:
  1. Add filled/bordered/additive shape render modes.
  2. Add custom wave point equations after the shape primitive path is stable.

## Update 2026-04-30 04:32:55Z

- Current task: First parsed shape primitive pass is implemented locally.
- Last activity:
  - added `createShapeVertices` and `getShapeColor`
  - renderer draws enabled parsed shape entries as closed WebGL line strips into the feedback target
  - tests cover shape vertex closure, disabled-shape handling, color clamping/fallback, and shape draw calls
  - validated renderer tests and frontend lint
- Next steps:
  1. Add custom shape frame equations so parsed shape state can animate.
  2. Add fill/border/additive shape modes after outline geometry is stable.

## Update 2026-04-30 04:29:10Z

- Current task: First waveform primitive pass is implemented locally.
- Last activity:
  - added a second lightweight WebGL program for line-strip primitives
  - maps incoming waveform samples into clip-space vertices with `wave_scale`
  - draws waveform lines into the feedback target before blitting to screen
  - tests cover waveform vertex generation, line draw calls, and program cleanup
  - validated renderer tests and frontend lint
- Next steps:
  1. Add a shape primitive pass for parsed `shapeNN_*` entries.
  2. Teach custom wave point equations to generate waveform vertices instead of only rendering raw samples.

## Update 2026-04-30 04:25:32Z

- Current task: First preset-driven warp uniforms are implemented locally.
- Last activity:
  - feedback texture sampling now uses evaluated `zoom`, `rot`, `dx`, and `dy`
  - added warp-state normalization for defaults and invalid zoom values
  - renderer tests cover warp uniform uploads and normalized defaults
  - validated renderer tests and frontend lint
- Next steps:
  1. Add the first translated warp/comp shader support path instead of only fixed-function warp uniforms.
  2. Start WebGL waveform/shape primitive rendering once the full-screen feedback path is stable.

## Update 2026-04-30 04:22:26Z

- Current task: Native MilkDrop WebGL feedback pipeline foundation is implemented locally.
- Last activity:
  - added ping-pong feedback texture/framebuffer targets to `milkdropRenderer.js`
  - renderer now writes to feedback, blits to screen, swaps targets, resizes texture storage, and disposes GPU resources
  - tests cover feedback allocation, draw/blit calls, decay-driven feedback blend, resize storage, and cleanup
  - validated renderer tests and frontend lint
- Next steps:
  1. Add the first real warp pass over the feedback texture using parsed preset variables.
  2. Start mapping MilkDrop waveform/shape primitives to WebGL draw calls after the feedback path is stable.

## Update 2026-04-30 04:19:23Z

- Current task: Minimal WebGL2 MilkDrop renderer skeleton is implemented locally.
- Last activity:
  - added `milkdropRenderer.js` behind the native MilkDrop foundation
  - renderer compiles a WebGL2 shader program, evaluates parsed preset equations, and draws a full-screen triangle from MilkDrop color variables
  - added tests for draw calls, color clamping, resize behavior, and missing-WebGL2 failures
  - validated focused MilkDrop tests and frontend lint
- Next steps:
  1. Replace the placeholder color pass with the first real feedback texture pass.
  2. Add real browser screenshot/pixel-stat smoke coverage once the renderer is player-selectable.

## Update 2026-04-30 04:17:00Z

- Current task: Browser-native MilkDrop3-compatible visualizer parser/VM foundation is implemented locally.
- Last activity:
  - added `presetParser.js` for classic `.milk` and basic `.milk2` double-preset parsing
  - added `expressionVm.js` for deterministic first-slice MilkDrop equation evaluation
  - added focused parser/VM tests for base values, equations, shaders, custom shapes/waves, q33 preservation, and unsupported syntax
  - documented and committed the custom wave/shape equation parser gotcha as `7a6bf43ef`
  - validated parser/VM tests, player tests, frontend lint/build, and diff whitespace
- Next steps:
  1. Add a WebGL2 renderer skeleton that consumes the parsed model but starts with a deliberately tiny draw path.
  2. Add curated `.milk` fixture files and golden parse snapshots instead of embedding all fixtures inline in tests.

## Update 2026-04-30 04:08:24Z

- Current task: Browser-native MilkDrop3-compatible visualizer engine design is started.
- Last activity:
  - inspected upstream MilkDrop3 source layout and license
  - confirmed the source is BSD-3-Clause but tied to C++/Win32/Direct3D rather than directly portable to browsers
  - added `docs/design/webgl-milkdrop3-port.md`
  - promoted T-938 to a P1 active design task for a WebGL2-first in-app engine
  - kept the external visualizer launcher positioned as an interim bridge only
  - moved Butterchurn behind a visualizer engine adapter as the first Phase 0 implementation slice
  - validated focused player tests, frontend lint, and diff whitespace
- Next steps:
  1. Start the parser/VM compatibility slice with `.milk` fixtures before adding MilkDrop3 `.milk2` features.
  2. Add a headless screenshot/pixel-stat smoke test around the engine boundary.

## Update 2026-04-30 03:40:00Z

- Current task: None. MilkDrop playback visualization is fixed and browser-verified locally.
- Last activity:
  - fixed Butterchurn module resolution under Vite dynamic imports
  - connected MilkDrop to the shared Web Audio graph through a stable visualizer tap
  - kept the tap on a live silent branch so Chromium processes analyzer data while normal playback stays on the main output path
  - added cleanup for Butterchurn audio connections when the visualizer unmounts
  - documented and committed the MilkDrop gotcha as `1cd281afe`
  - validated focused player tests, frontend lint/build, repo lint, full `dotnet test`, and a Playwright canvas screenshot smoke test
- Next steps:
  1. Keep future player graph rebuilds from disconnecting `graph.visualizerInput`.
  2. Use screenshot/image statistics for MilkDrop smoke checks instead of direct default-framebuffer `gl.readPixels`.

## Update 2026-04-30 03:28:00Z

- Current task: None. Player full-width layout and Web Audio playback resume fixes are implemented locally.
- Last activity:
  - made the player display, signal/spectrum tile, and controls stretch to the full fixed bar width
  - changed playback to resume the shared Web Audio graph before `audio.play()`
  - switched the media element to direct `src` updates
  - prevented analyzer AudioContext creation before a current track exists
  - replaced an invalid Semantic UI icon name in the signal/spectrum affordance
  - validated focused tests, frontend lint/build, headless Playwright layout/console checks, and diff whitespace
- Next steps:
  1. Re-test with a real streamed track in Chrome/Edge to confirm audible output with EQ/analyzer enabled.
  2. Keep future analyzer/MilkDrop changes from initializing Web Audio before user playback starts.

## Update 2026-04-30 03:23:00Z

- Current task: None. Player duplicate-title and lyrics lookup fixes are implemented locally.
- Last activity:
  - changed the queue preview to show only upcoming tracks, not the current track
  - made lyrics lookup infer `Artist - Title.ext` filenames and avoid LRCLIB requests with placeholder artists
  - added LRCLIB search fallback and plain-lyrics rendering
  - added focused player/lyrics regression tests
  - documented and committed the player queue/lyrics gotcha as `cea5eede6`
- Next steps:
  1. Add richer persisted metadata for local library items so lyrics and now-playing services do not depend on filename inference.
  2. Do a live browser check with a real tagged song that exists in LRCLIB.

## Update 2026-04-30 03:18:00Z

- Current task: None. New streaming/player/DHT pod/listening-party security hardening is implemented and validated locally.
- Last activity:
  - replaced browser audio JWT query-string playback with short-lived stream tickets
  - required party-bound tickets and per-IP limits for listed-party radio streams
  - switched listening-party DHT records to explicit JSON byte payloads
  - failed closed on invalid pod DHT signatures and stopped signing caller-supplied pod publish/update bodies
  - bounded local stream lookup to path IDs under configured roots and reduced local library path exposure
  - tightened ListenBrainz token clearing and submit-result reporting
  - validated Release build, focused backend tests, frontend lint/build, full `dotnet test`, and repo lint via `bash ./bin/lint`
- Next steps:
  1. Browser-smoke listed-party radio ticket playback across two authenticated nodes.
  2. Keep stream tickets short-lived and content-bound for any future media-element playback URLs.

## Update 2026-04-30 02:33:09Z

- Current task: None. Stable Winget publishing is now opt-in.
- Last activity:
  - removed the automatic `winget-main` job from `build-on-tag.yml`
  - kept the manual `Publish Winget` workflow as the high-value stable release path
  - documented that main release tags regenerate checked-in Winget metadata but do not open public `microsoft/winget-pkgs` PRs automatically
- Next steps:
  1. Use `Publish Winget` manually only for stable releases that should be promoted to the public Winget catalog.
  2. Let routine/package-test releases stay in GitHub/package channels without Winget PR noise.

## Update 2026-04-30 02:20:25Z

- Current task: None. Winget singleton validation failure is fixed locally.
- Last activity:
  - confirmed `microsoft/winget-pkgs` PR #366812 cleared CLA but still failed service validation because singleton manifests are not accepted
  - changed the stable release and manual Winget workflows to stage the generated multi-file manifests under `winget-submit/manifests/s/snapetech/slskdn/<version>/`
  - tightened staging to copy only the three stable manifest filenames so dev manifests cannot leak into the stable package directory
  - corrected the Winget gotchas documentation and committed those ADR fixes separately
  - locally verified the `.202` staging shape produces version, installer, and defaultLocale manifests with no singleton or dev files
  - updated the existing `microsoft/winget-pkgs` PR branch in place with commit `25ce9d0f1cdabdb03e42bded02a49317f785cf30` instead of opening a duplicate PR
  - pushed the slskdN workflow fix to `snapetech/slskdn` as `64cb9247a`
- Next steps:
  1. Wait for Winget validation run `307341` on PR #366812 to finish.
  2. If validation passes, wait for Microsoft review/merge; if it fails, inspect the new Azure timeline before making another PR change.

## Update 2026-04-30 02:07:00Z

- Current task: None. Player and listening-party documentation is updated.
- Last activity:
  - updated README with the integrated Web Player & Listening Parties section
  - documented modal collection/file browsers, footer-safe drawer behavior, local-root streaming, player extras, and PWA/mobile behavior
  - expanded `docs/listening-party.md` with Web Player source, controls, and local audio boundaries
  - corrected `docs/advanced-features.md` and `docs/FEATURES.md` so streaming is no longer described as Pod-only
- Next steps:
  1. Improve persisted collection item display metadata so playlist rows can show friendly filenames/titles instead of raw content ids.
  2. Browser-smoke the broader Winamp controls against a real streamed track, especially Document Picture-in-Picture in Chrome/Edge.

## Update 2026-04-30 02:06:00Z

- Current task: None. Player collection/file picking now uses modal browsers instead of compact dropdowns.
- Last activity:
  - replaced the player empty-state collection dropdown with a two-pane Collections modal
  - replaced the downloaded/shared audio dropdown with a searchable local-audio browser modal
  - kept playback actions explicit with per-row play buttons and kept the full Collections management path available
  - verified focused player tests, frontend lint/build, and mobile modal geometry with no horizontal overflow
- Next steps:
  1. Improve persisted collection item display metadata so playlist rows can show friendly filenames/titles instead of raw content ids.
  2. Browser-smoke the broader Winamp controls against a real streamed track, especially Document Picture-in-Picture in Chrome/Edge.

## Update 2026-04-30 02:05:00Z

- Current task: None. Winamp-style Web UI player enhancements are implemented and validated locally.
- Last activity:
  - added shared Web Audio graph plumbing for the player, MilkDrop, EQ, analyzer, karaoke, and crossfade gain control
  - added a persisted 10-band EQ with presets
  - added lightweight spectrum/oscilloscope rendering and Document Picture-in-Picture spectrum output
  - added LRCLIB synced lyrics and ListenBrainz now-playing/scrobble submission using a browser-local token
  - rebuilt the first-pass player bar into a modern Winamp-style deck after headless visual inspection
  - documented the additions in README, feature docs, advanced walkthrough, and changelog
  - validated focused player tests, frontend lint, and frontend production build
- Next steps:
  1. Browser-smoke the new controls against a real streamed track, especially Document Picture-in-Picture in Chrome/Edge.
  2. Improve persisted collection item display metadata so playlist rows can show friendly filenames/titles instead of raw content ids.

## Update 2026-04-30 01:52:00Z

- Current task: None. The Web UI player drawer, transport controls, and local audio picker are implemented and verified.
- Last activity:
  - added collapse/expand so the player can shrink into a drawer bar above the fixed footer
  - added previous/next, rewind, fast-forward, local mute, and browser Media Session transport handlers
  - surfaced Collections and shared/downloaded local audio in the player empty state
  - made the integrated stream locator resolve allowed local share/download roots without a separate streaming service
  - verified live Commons OGG streaming, player/footer geometry, focused tests, frontend lint, and frontend build
- Next steps:
  1. Improve persisted collection item display metadata so playlist rows can show friendly filenames/titles instead of raw content ids.
  2. Exercise the global radio/listening-party flow with two real authenticated mesh nodes once live credentials are loaded.

## Update 2026-04-30 02:20:00Z

- Current task: None. Gold Star Club leave/revoke copy now states that leaving is irrevocable.
- Last activity:
  - added a browser confirmation before Gold Star Club leave
  - changed the in-page Gold Star warning to say there are no rejoins
  - updated README and Gold Star design docs to document permanent revocation
  - validated frontend lint and diff whitespace
- Next steps:
  1. Keep Gold Star membership recovery/rejoin flows intentionally absent.
  2. Review any future governance UI for the same irrevocable-leave language.

## Update 2026-04-30 01:45:00Z

- Current task: None. Pod, room, chat, and contact social recovery surfaces are integrated.
- Last activity:
  - reviewed social workflows across Pods, Rooms, Chat, and Contacts
  - added server-state rehydration affordances for saved chats and joined rooms
  - made Pods support create, discover, and local-save flows from the main page
  - added Contacts actions to jump directly into chat or shared-file browsing
  - validated frontend lint and diff whitespace
- Next steps:
  1. Exercise pod discovery/save against a live mesh node to confirm remote metadata quality.
  2. Add signed remote pod join-request UI once the membership service has a user-friendly key/signature flow.
  3. Keep social discovery scans user-triggered and conservative.

## Update 2026-04-30 02:05:00Z

- Current task: None. Discography Concierge README/docs are updated.
- Last activity:
  - documented the feature in README, `docs/FEATURES.md`, and `docs/advanced-features.md`
  - clarified the changelog entry for MusicBrainz artist coverage and manual Wishlist promotion
- Next steps:
  1. Start T-931 Bloom-filter library diff.
  2. Keep unrelated pod/listening-party dirty work separate from the Discography Concierge slice.

## Update 2026-04-30 01:55:00Z

- Current task: None. T-930 Discography Concierge first slice is implemented locally.
- Last activity:
  - added backend coverage models, service, DI, and MusicBrainz API endpoints
  - added manual missing-track Wishlist promotion without immediate searches/downloads
  - added a Search-page Discography Concierge panel with button tooltips
  - added focused backend tests for coverage and promotion
  - validated backend build, focused tests, frontend lint, frontend build, full `dotnet test`, repo lint, and diff whitespace
- Next steps:
  1. Start T-931 Bloom-filter library diff.
  2. Follow up T-937 later for Discovery Graph density prioritization and richer local-vs-mesh evidence.
  3. Keep the unrelated listening-party dirty file separate.

## Update 2026-04-30 01:30:00Z

- Current task: None. Mesh/social music discovery feature plan is documented.
- Last activity:
  - added `docs/design/music-discovery-federation-plan.md`
  - planned Discography Concierge, Bloom Diff, Release Radar, Taste Recommendations, Realm Indexes, MB edit overlays, and Quarantine Jury
  - explicitly excluded backup, file duplication, mirroring, and replica-management scope
  - added T-930 through T-936 to `memory-bank/tasks.md`
- Next steps:
  1. Start with T-930 Discography Concierge coverage API.
  2. Keep Bloom Diff (T-931) privacy boundaries tight before using it as recommendation input.
  3. Defer Quarantine Jury implementation until signed evidence and trust-scoped routing are well tested.

## Update 2026-04-30 01:15:00Z

- Current task: None. Mixed-source accelerated failover no longer dials mesh-overlay sources through Soulseek.
- Last activity:
  - filtered sequential failover candidates to `IsSoulseekPeer()` before selecting sources
  - added a regression test for mixed mesh/Soulseek source lists
  - documented the gotcha in ADR-0001 and committed it separately
  - validated backend build, focused multi-source tests, and diff whitespace
- Next steps:
  1. Keep the broader listening-party/UI dirty work separate from this transfer fix.
  2. Commit the transfer fix with its test and changelog update.

## Update 2026-04-30 01:12:43Z

- Current task: None. Layer 1 listening parties are implemented locally.
- Last activity:
  - documented the metadata-only pod listen-along protocol, opt-in global radio registry, and deferred live mic/WebRTC layer
  - added a persistent Web UI player for existing `ContentId` stream playback
  - wired browser playback into Now Playing updates
  - added collection item play controls
  - added pod listen-along host/follow controls backed by stored/routed pod messages and SignalR fan-out
  - added a mesh/DHT-backed listed-party directory and integrated slskdN radio stream endpoint gated by a Mesh Streaming toggle
  - validated backend build, frontend lint/build, repo tests, and lint via `bash ./bin/lint`
- Next steps:
  1. Review the listening-party UX in a live browser with real shared audio.
  2. Add richer playlist queue controls and explicit seek publishing if the MVP feels right.
  3. Design the separate WebRTC live mic/commentary layer before implementation.

## Update 2026-04-30 00:58:00Z

- Current task: None. Security audit hardening is implemented and validated locally.
- Last activity:
  - required auth for ActivityPub outbox publishing
  - guarded HTTP share backfill against unsafe URLs, redirects, token-bearing logs, and oversized bodies
  - fixed file listing sibling-prefix authorization
  - removed unsupported query-string API-key CSRF exemptions
  - resolved SignalR API-key promotion through the request service provider
  - documented the security boundary gotcha and committed it separately
- Next steps:
  1. Push the security hardening commits if requested.
  2. Keep the pre-existing Winget/listening-party/UI dirty work separate unless explicitly batching it.

## Update 2026-04-30 00:44:00Z

- Current task: Fix fake-green stable Winget publishing.
- Last activity:
  - confirmed the `.202` Winget job skipped real submission because `WINGETCREATE_GITHUB_TOKEN` was empty
  - changed the main Winget release job to fail loudly when the token is missing
  - added a manual `Publish Winget` workflow for retrying an existing stable release tag after credentials are configured
  - confirmed `wingetcreate update` also fails for the first package submission because `snapetech.slskdn` is not yet present in `microsoft/winget-pkgs`
  - changed the stable Winget paths to submit generated manifests directly for the initial PR
  - fixed invalid stable Winget locale YAML caused by double-indented block-scalar text
  - changed WingetCreate staging from a flat scratch directory to the repository-shaped manifest path expected by `wingetcreate submit`
  - moved stable zip portable metadata to the installer manifest root to match accepted winget-pkgs layouts
- Next steps:
  1. Commit and push the Winget portable-manifest layout fix.
  2. Re-run the manual `Publish Winget` workflow for `2026042900-slskdn.202`.

## Update 2026-04-30 00:33:57Z

- Current task: None. AUR upgrade service refresh is implemented locally.
- Last activity:
  - confirmed AUR did not restart `slskd.service` on package upgrade
  - confirmed RPM already restarts active services through its systemd macro
  - changed AUR `post_upgrade()` to `try-restart` the service only when already running
  - documented the behavior in the AUR README, changelog, task log, progress log, and gotchas ADR
  - validated the AUR install hook syntax, diff whitespace, and repository lint
- Next steps:
  1. Commit and push the package-hook fix if requested.

## Update 2026-04-30 00:21:00Z

- Current task: Prepare replacement stable release `2026042900-slskdn.202`.
- Last activity:
  - monitored `.201` through failure in the Build release gate
  - fixed manual-review SongID Discovery Graph catalog-context expansion
  - updated the `UserService` disposal regression test for the fixture-owned regex matcher listener
  - documented and committed the SongID graph expansion gotcha separately
  - validated the affected release-gate unit-test slice
- Next steps:
  1. Run broader release-gate validation locally.
  2. Commit and push the `.202` release-gate fix plus release-note update.
  3. Push tag `build-main-2026042900-slskdn.202` and monitor the replacement release workflow.

## Update 2026-04-30 00:08:00Z

- Current task: Prepare replacement stable release `2026042900-slskdn.201`.
- Last activity:
  - monitored `.200` through failure in the Build release gate
  - fixed stale unit-test compile blockers in YAML configuration and path-normalization tests
  - documented and committed the release-gate compile gotcha separately
  - validated the affected unit-test slice
- Next steps:
  1. Commit and push the test fix plus `.201` release-note update.
  2. Push tag `build-main-2026042900-slskdn.201`.
  3. Monitor the replacement release workflow.

## Update 2026-04-30 00:01:05Z

- Current task: Prepare stable release `2026042900-slskdn.200`.
- Last activity:
  - verified GitHub writes target `snapetech/slskdn`
  - promoted the current Unreleased UI chrome and transfer polish bullets into a `.200` changelog section
- Next steps:
  1. Generate release notes for `2026042900-slskdn.200`.
  2. Commit and push the `.200` release-note update.
  3. Push tag `build-main-2026042900-slskdn.200`.

## Update 2026-04-29 23:53:57Z

- Current task: None. The low-risk upstream-request integration pass is implemented locally.
- Last activity:
  - capped automatic queue-position refresh to cached batches of five every 30 seconds
  - linked transfer peer headers to Browse from Downloads and Uploads
  - fixed successful batch download delete-on-remove path resolution
  - documented advanced search filter text syntax in README and comparison rows
  - documented and committed the batch-removal path gotcha separately
  - validated focused transfer UI tests, focused transfer backend tests, frontend lint/build, and diff checks
- Next steps:
  1. Review whether the pre-existing dirty App/Footer UI files should be included with this work or split before commit.
  2. Browser playback and large browse pagination remain larger feature work that should get separate design/testing passes.

## Update 2026-04-29 23:58:00Z

- Current task: None. Header and footer chrome alignment has been fixed and deployed to `local test host`.
- Last activity:
  - split the header into primary navigation and utility rails
  - reordered utilities to Connected, Theme, System, Log Out
  - removed the permanently-highlighted Theme trigger styling
  - rebuilt the footer as brand, speed, and network/transport rails
  - validated live logged-in screenshots at desktop, mid-width, and mobile widths with no vertical overflow
  - documented and committed the chrome grouping gotcha separately
- Next steps:
  1. Commit and push the UI chrome fix when ready.

## Update 2026-04-29 23:36:10Z

- Current task: None. The principal UI pass over the README showcase surfaces is implemented locally.
- Last activity:
  - reviewed all six README screenshots for chrome, spacing, contrast, and hierarchy problems
  - compacted the desktop nav and reduced the fixed footer footprint
  - made the mobile footer a single scrollable status rail instead of a stacked block
  - rebuilt result-card identity/action layout and file-list spacing
  - tightened Discovery Graph controls and added sparse graph messaging
  - defaulted secondary Search page panels closed and persisted their state
  - deployed the rebuilt frontend to `local test host` and recaptured the README gallery
  - documented and committed the fixed-chrome screenshot validation gotcha separately
- Next steps:
  1. Commit and push the UI pass plus README screenshot changes when ready.
  2. Keep unrelated accelerated-downloads dirty work intact unless explicitly asked to include or split it.

## Update 2026-04-29 23:22:41Z

- Current task: None. README showcase dark-mode screenshots are fixed locally.
- Last activity:
  - pulled the remote README edits with rebase/autostash
  - inspected the README showcase screenshots and identified light-theme leaks in SongID, Discovery Graph, and Network
  - added explicit dark-theme styling for the affected Semantic UI variants and nested text
  - rebuilt and deployed the web bundle to `local test host` for screenshot verification
  - recaptured `songid-cc-youtube-result.png`, `songid-discovery-graph.png`, and `network-health-dashboard.png`
  - documented and committed the dark-theme Semantic UI variant gotcha separately
- Next steps:
  1. Commit and push the README screenshot/dark-theme styling changes when ready.
  2. Keep the unrelated accelerated-downloads dirty work intact unless explicitly asked to include or split it.

## Update 2026-04-29 23:20:00Z

- Current task: None. The accelerated-downloads toggle and conservative rescue wiring are implemented locally.
- Last activity:
  - added the Downloads header `Accelerated` toggle with tooltip and runtime transfer API endpoints
  - routed the underperformance detector through the toggle so slow/stalled downloads can enter rescue acceleration only when enabled
  - preserved the trust policy: normal Soulseek downloads stay single-source, raw Soulseek alternates use verified sequential failover, and true multipart remains mesh-overlay-only
  - made discovery hash probes consume the same persistent per-peer daily verification budget as verification probes
  - changed explicit swarm download requests to default to verification enabled
  - updated README, multipart-downloads, and changelog documentation for the toggle and network-health policy
  - documented and committed the discovery probe-budget gotcha separately
- Next steps:
  1. Resolve the unrelated unit-test compile errors in `YamlConfigurationSourceTests` and `ProgramPathNormalizationTests` so the new backend tests can run in a fresh build.
  2. Decide whether the accelerated-downloads toggle should be persisted to config/UI storage or remain runtime-only.
  3. Commit and push the accelerated-downloads implementation after review.

## Update 2026-04-29 22:35:00Z

- Current task: None. The post-0.25 upstream-compatibility gap plan is implemented locally.
- Last activity:
  - added dual config schema binding and startup warnings for legacy keys
  - added regex username blacklist patterns through a cached matcher
  - fixed Search Again payload mapping and reverse-proxy-safe web metadata paths
  - verified batch retry directories were already present, clamped retry max delay to 30s, covered YAML reload, and added fork guidance
  - confirmed no SignalR typed hub exception catch pattern was present to remove
- Next steps:
  1. Run full `dotnet test` and `./bin/lint`.
  2. Commit and push the compatibility implementation.
  3. Create a tag-only release only if explicitly requested.

## Update 2026-04-29 22:02:31Z

- Current task: None. The Network dashboard public-DHT notice and false no-peer diagnostics are fixed locally.
- Last activity:
  - inspected `local test host` DHT logs and confirmed the live node is not at zero peers (`nodes=155`, `discovered=37`, `activeMesh=1`)
  - made the public-DHT exposure notice a dismissable info message instead of a persistent warning/modal
  - made connectivity diagnostics consider DHT status peer counters as well as list endpoints
  - documented the warning-counter gotcha in ADR-0001 and committed that docs entry separately
  - validation passed for focused Network tests and frontend lint
- Next steps:
  1. Commit and push the Network dashboard fix.
  2. Create a tag-only release only if explicitly requested.

## Update 2026-04-29 21:43:00Z

- Current task: Prepare stable release `2026042900-slskdn.198`.
- Last activity:
  - landed the multi-source / swarm trust-aware policy split, hard floor, probe budget, and metrics
  - rewrote `docs/multipart-downloads.md` and the README multi-source section to be explicit about scope (default downloads use the standard single-source Soulseek path; multi-source is rescue / remediation / explicit-API only)
  - promoted the unreleased Chocolatey CI fix bullets and the new swarm bullets into the `.198` changelog section
- Next steps:
  1. Commit and push the `.198` release-note update.
  2. Push tag `build-main-2026042900-slskdn.198`.
  3. Watch the release workflow.

## Update 2026-04-29 18:07:00Z

- Current task: Prepare stable release `2026042900-slskdn.197`.
- Last activity:
  - promoted Web UI fix notes into the `.197` changelog section
  - generated release notes for `2026042900-slskdn.197`
  - confirmed `.197` notes include theme picker, transfer flicker, footer speed fallback, and title fixes
- Next steps:
  1. Commit and push the `.197` release-note update.
  2. Push tag `build-main-2026042900-slskdn.197`.
  3. Watch the release workflow.

## Update 2026-04-29 17:35:00Z

- Current task: None. Browser tab title is restored to `slskdN` locally.
- Last activity:
  - changed runtime `document.title` to the short slskdN brand
  - added App test coverage for the tab title
  - updated changelog, tasks, and progress notes
- Next steps:
  1. Validate, commit, and push the title fix.
  2. Continue with the requested `.197` release after the title fix lands.

## Update 2026-04-29 17:31:00Z

- Current task: None. The Web UI theme picker, transfer bulk-action flicker, and footer speed fallback are fixed locally.
- Last activity:
  - switched the theme picker to a controlled Semantic UI dropdown and added click coverage
  - made transfer polling monotonic and hid rows after accepted bulk retry/remove operations
  - made footer speed totals use active transfer bytes-over-elapsed-time when `AverageSpeed` is still zero
  - documented the UI gotchas in ADR-0001 and committed that docs entry separately
  - validation passed for frontend lint, focused frontend/backend tests, web build, and diff checks
- Next steps:
  1. Commit and push the implementation/docs updates.

## Update 2026-04-29 17:17:00Z

- Current task: Prepare stable release `2026042900-slskdn.196`.
- Last activity:
  - promoted the current Unreleased notes into a `.196` changelog section
  - generated release notes for `2026042900-slskdn.196`
  - confirmed no non-ignored dirty files were present before release prep
- Next steps:
  1. Commit and push the `.196` release-note update.
  2. Push tag `build-main-2026042900-slskdn.196`.
  3. Watch the tag-only release workflow.

## Update 2026-04-29 17:12:30Z

- Current task: None. The slskdN top status drawer has been removed and its unique stats now render in the footer.
- Last activity:
  - deleted the status drawer component, nav toggle, visibility state, and fixed top padding
  - moved DHT, mesh, hash, sequence, swarm, backfill, and karma counters into the persistent footer
  - added footer regression coverage and validated frontend lint, focused tests, build, and diff checks
- Next steps:
  1. Commit and push the Web UI footer/status cleanup.

## Update 2026-04-29 16:56:39Z

- Current task: None. The transient full-instance overlay-connect integration failure is fixed and validated.
- Last activity:
  - reproduced `OptionalLiveAccounts_CanSearchAndDownloadHostedProbeOverOverlayMesh` passing in isolation
  - changed the DHT full-instance tests to wait through transient `/api/v0/overlay/connect` `502` responses before failing
  - validated all `TwoNodeMeshFullInstanceTests`
- Next steps:
  1. Push the integration-test stability fix to `main`.

## Update 2026-04-29 16:32:00Z

- Current task: Resolve open security warnings and Dependabot bump PRs.
- Last activity:
  - identified three open Dependabot alerts and one open CodeQL cleartext-secret alert
  - applied Dependabot PR `#222`, `#220`, and `#218` dependency upgrades into `main`
  - explicitly upgraded vulnerable OpenTelemetry packages to `1.15.3` and npm `uuid` to `14.0.0`
  - changed overlay certificate handling so legacy cleartext password files are deleted without being read
  - documented the legacy password gotcha in ADR-0001 and committed that docs entry separately
- Next steps:
  1. Run focused backend/frontend validation and security checks.
  2. Commit and push the security remediation.
  3. Confirm GitHub alerts/checks after push.

## Update 2026-04-29 16:19:50Z

- Current task: Prepare stable release `2026042900-slskdn.195` for the LAN-only DHT warning fix.
- Last activity:
  - promoted the Network dashboard LAN-only warning fix into a `.195` changelog section
  - generated release notes for `2026042900-slskdn.195`
  - validation passed for release-note generation, packaging metadata, and diff checks
- Next steps:
  1. Commit and push the `.195` release-note update.
  2. Push tag `build-main-2026042900-slskdn.195`.
  3. Watch the tag-only release workflow.

## Update 2026-04-29 16:17:24Z

- Current task: None. False Network dashboard public DHT warning for LAN-only nodes is fixed locally and ready to push.
- Last activity:
  - confirmed the tester screenshot uses `dhtRendezvous.lanOnly: true`
  - traced the false warning to frontend code checking `isLanOnly` while the backend serializes `LanOnly` as `lanOnly`
  - normalized DHT status and added Network regression coverage for the backend response shape
  - documented the gotcha in ADR-0001 and committed that docs entry separately
  - validation passed for focused Network tests, frontend lint on touched files, and diff checks
- Next steps:
  1. Commit and push the LAN-only warning fix.
  2. Prepare a new tag-only release only if explicitly requested.

## Update 2026-04-29 17:10:00Z

- Current task: Prepare stable release `2026042900-slskdn.194` for the AUR source build fix.
- Last activity:
  - promoted the AUR source date-version build fix into a `.194` changelog section
  - generated release notes for `2026042900-slskdn.194`
  - validation passed for release-note generation, packaging metadata, and diff checks
- Next steps:
  1. Commit and push the `.194` release-note update.
  2. Push tag `build-main-2026042900-slskdn.194`.
  3. Watch the tag-only release workflow.

## Update 2026-04-29 16:43:00Z

- Current task: None. `slskdn` AUR source install fix is implemented, pushed, and published to AUR.
- Last activity:
  - reproduced the fatal AUR source build failure as invalid MSBuild assembly version `2026042900.193.0.0`
  - documented the date-version packaging gotcha in ADR-0001 and committed it separately
  - updated the source PKGBUILD to pass `0.0.0-slskdn.YYYYMMDDmm.NNN` to MSBuild while keeping the public informational version unchanged
  - bumped the source AUR package template to `pkgrel=2`
  - validation passed for packaging metadata, direct fixed-version `dotnet publish`, lint, and diff checks
  - pushed live AUR `slskdn` commit `b14afe2` with `pkgrel=2` and regenerated `.SRCINFO`
- Next steps:
  1. Re-run `yay -S slskdn` on an Arch host after the AUR helper refreshes package metadata.

## Update 2026-04-29 15:46:20Z

- Current task: Prepare stable release `2026042900-slskdn.193` from `main`.
- Last activity:
  - verified GitHub write target is `snapetech/slskdn`
  - confirmed `main` is clean and current with `origin/main`
  - promoted the current Unreleased attribution/theme bullets into the `.193` changelog section
- Next steps:
  1. Validate generated `.193` release notes.
  2. Commit and push the release-note update.
  3. Push tag `build-main-2026042900-slskdn.193`.

## Update 2026-04-29 16:05:00Z

- Current task: None. slskdN Web UI theme rebrand is implemented locally and ready for validation.
- Last activity:
  - confirmed the existing dark palette matches upstream `0.24.5`
  - added slskdN as the default brown/gray/purple theme
  - kept Classic Dark and Light selectable from the Theme menu
- Next steps:
  1. Run frontend lint/tests and diff checks.
  2. Commit and push the scoped web theme update if validation is clean.

## Update 2026-04-29 15:30:45Z

- Current task: Copyright and attribution audit.
- Last activity:
  - compared tracked non-generated C# files against upstream `0.24.5`
  - preserved upstream `slskd Team` AGPL headers on upstream-derived files
  - added separate `slskdN Team` copyright blocks where upstream-derived files have slskdN changes
  - normalized slskdN-only C# files to slskdN-only attribution
  - fixed and documented the stacked-comment `SA1512` header-layout gotcha
  - validation passed for the repeat attribution audit, C# diff check, `dotnet build --no-restore`, and `bash ./bin/lint`
- Next steps:
  1. Commit the attribution normalization.
  2. Leave unrelated release/packaging metadata edits unstaged.

## Update 2026-04-29 07:45:00Z

- Current task: None. Copyright and branding attribution pass is implemented locally and ready for validation.
- Last activity:
  - clarified slskdN as an unofficial fork of slskd across docs, web metadata/footer/title text, Swagger/API metadata, default Soulseek profile text, package metadata, and generated release copy
  - changed slskdN support links to target `snapetech/slskdn` while preserving upstream slskd as an attribution/reference link
  - documented compatibility names that should remain `slskd` because they affect existing installs, config, metrics, or API expectations
- Next steps:
  1. Run packaging, frontend, backend, lint, and diff validation.
  2. Commit and push the attribution pass if validation is clean.

## Update 2026-04-29 07:11:17Z

- Current task: Prepare `2026042900-slskdn.192` for the weak SongID Discovery Graph fix.
- Last activity:
  - promoted the Discovery Graph fix note into a versioned `.192` changelog section
  - left stable package metadata on `.191`; the tag workflow updates metadata after `.192` release assets and hashes exist
- Next steps:
  1. Validate generated release notes for `2026042900-slskdn.192`.
  2. Commit and push the release-note update.
  3. Push tag `build-main-2026042900-slskdn.192`.

## Update 2026-04-29 07:07:57Z

- Current task: None. Weak SongID Discovery Graph neighborhood promotion is fixed locally and ready to commit.
- Last activity:
  - traced the bogus graph neighborhoods to secondary SongID evidence being promoted into graph nodes even when the run verdict was `needs_manual_review`
  - documented the gotcha in ADR-0001 and committed that documentation as `cbe735818`
  - gated artist/album/segment/mix graph expansion on trusted identity, while still allowing exact or high-confidence track candidates through
  - added regression tests proving weak runs do not fetch MusicBrainz artist release graphs or render secondary-evidence neighborhoods
  - validation passed for build, lint, diff check, and focused Discovery Graph tests; full `dotnet test --no-build` still failed the known unrelated full-instance overlay `502` integration cases
- Next steps:
  1. Commit the implementation/docs update.
  2. Push only if requested; do not create release/build tags.

## Update 2026-04-29 06:34:00Z

- Current task: Prepare `2026042900-slskdn.191` after `.190` Docker publish failed.
- Last activity:
  - diagnosed `.190` Docker failure: `groupadd --gid 1000 slskdn` collided with an existing GID in `mcr.microsoft.com/dotnet/runtime-deps:10.0-noble`
  - documented the fixed-ID Docker gotcha in ADR-0001 and committed it as `88e29be3d`
  - changed the Dockerfile to use system-allocated placeholder user/group IDs and added packaging validation to reject fixed `1000` Docker user/group creation
- Next steps:
  1. Validate release notes and packaging metadata for `2026042900-slskdn.191`.
  2. Push `main` and tag `build-main-2026042900-slskdn.191`.
  3. Watch `.191`; purge superseded releases after a successful replacement is live.

## Update 2026-04-29 06:25:00Z

- Current task: Prepare `2026042900-slskdn.190` after upstream-alignment changes landed on `main`.
- Last activity:
  - confirmed `origin/main` advanced past `.189` with `9b31faa4d feat: Adapt upstream packaging and transfer alignment`
  - reviewed the diff: Docker runtime user handling, packaging validation, configurable transfer retry/resume and batch metadata, IPv4-mapped address normalization, and focused tests
  - promoted the Unreleased changelog notes into `2026042900-slskdn.190` while keeping the date-versioned rollback explanation explicit
- Next steps:
  1. Validate release-note generation for `2026042900-slskdn.190`.
  2. Push `build-main-2026042900-slskdn.190`.
  3. Continue watching `.189`/`.190`; purge superseded releases after the replacement release is live.

## Update 2026-04-29 06:08:44Z

- Current task: None. Upstream-alignment items `1`, `2`, and `3` are implemented locally and validated.
- Last activity:
  - added slskdN-native low-risk upstream alignment fixes for IPv4-mapped IPv6 CIDR/proxy/API handling, null-safe config diffs, and retry callbacks/backoff controls
  - added Docker packaging/runtime support for `PUID`/`PGID`, non-root `--user` operation, app-dir access validation, corrected Docker revision metadata, and packaging validator coverage
  - added configurable direct-download retry/resume behavior plus transfer batch metadata, API batch grouping, persistence migration, DTO fields, and focused regression tests
  - compared the implementation against current upstream and refactored the retry/download retry code paths so the expression is slskdN-specific rather than structurally mirroring upstream
  - documented the IPv4-mapped IPv6 gotcha in ADR-0001 and committed it as `5f80585f4`
  - validation passed: `dotnet build --no-restore`, focused unit slices, packaging metadata validation, shell syntax checks, `bash ./bin/lint`, and `git diff --check`; full `dotnet test --no-build` passed unit/non-integration projects and hit the known transient full-instance overlay `502` once, then the exact failed integration test passed on rerun
- Next steps:
  1. Review the diff, then commit the implementation if it matches the intended copyright-safe alignment scope.
  2. Do not create release/build tags unless explicitly requested.

## Update 2026-04-29 05:35:00Z

- Current task: Corrective stable release versioning after the license-compliance rollback.
- Last activity:
  - prepared `2026042900-slskdn.189` as the public stable version format requested for slskdN, avoiding any `0.26` implication while sorting newer than removed `0.25.1-slskdn.*` packages
  - updated release-note detection, CI tag scanning, tag-build publishing, and local build/publish scripts to support `YYYYMMDDmm-slskdn.###`
  - kept .NET/NuGet build inputs valid by mapping the public release version to `0.0.0-slskdn.2026042900.189` for MSBuild and keeping `InformationalVersion=2026042900-slskdn.189`
  - generated the release notes and verified a local dotnet-only build with the new public version
- Next steps:
  1. Commit and push the versioning support to `origin/main`.
  2. Push tag `build-main-2026042900-slskdn.189` and watch the tag-only release workflow.
  3. After the new release exists, delete the superseded `0.24.5-slskdn.186` release and cleanup tag so only the corrective rollback release remains.

## Update 2026-04-29 04:47:48Z

- Current task: Final validation for the `rollback/0.24.x` release-critical backport set.
- Last activity:
  - resumed Claude's partial rollback-branch state after the Soulseek.NET PR branch was pushed but cross-repo PR creation failed due token permissions
  - kept the already-applied surgical backports for release-note size guardrails, runtime YAML alias binding, directory browse timeout handling, and shutdown cancellation classification
  - added the missing selective backports for build-on-tag publish unblocking and empty cached user-group fallback
  - updated changelog/tasks/progress for the rollback backport work
  - validation passed: focused `YamlConfigurationSourceTests`/`UserServiceTests`, release-note fail-closed smoke, `git diff --check`, and `bash ./bin/lint`
  - full `dotnet test --no-restore` passed unit/non-integration projects and failed only the known transient full-instance overlay `502` cases; rerunning the exact two failed integration tests passed
- Next steps:
  1. Commit the rollback backport set.
  2. Do not create build tags unless explicitly requested.

## Update 2026-04-26 20:08:00Z

- Current task: Issue `#216` CSV playlist import is implemented and ready to commit/push.
- Last activity:
  - fetched GitHub issue `#216` from `snapetech/slskdn`: request is batch downloading music from CSV exports like TuneMyMusic
  - added a wishlist CSV import model/parser that recognizes TuneMyMusic-style track/artist/album headers, handles quoted CSV fields, generates artist/title wishlist searches, and deduplicates against existing/imported rows
  - added authenticated `POST /api/v0/wishlist/import/csv`; import creates wishlist entries without starting a large immediate Soulseek search burst, while optional `autoDownload` uses the existing wishlist scheduler/manual run path
  - added a Wishlist page CSV import modal with file/text input, filter, max results, enabled, auto-download, and album-term controls
  - validation passed: focused `WishlistControllerTests`, frontend lint, frontend production build, `dotnet build --no-restore`, `bash ./bin/lint`, `git diff --check`, full `dotnet test --no-restore` except one known transient optional live mesh setup `502`, and the exact failed integration test passed on rerun
- Next steps:
  1. Commit and push the issue `#216` work.
  2. Create `build-main-0.24.5-slskdn.181` if an immediate main build is still desired.

## Update 2026-04-26 19:33:25Z

- Current task: None. Deep code audit of Bas upload failure found a concrete listen endpoint advertisement bug and the fix is ready to commit/push.
- Last activity:
  - re-audited listener setup, runtime listener updates, inbound upload enqueue handling, upload queue processing, share resolution, and Soulseek.NET peer/transfer connection behavior
  - found that runtime `soulseek.listen_port` / `soulseek.listen_ip_address` changes can restart the local listener without forcing the Soulseek server to advertise the new endpoint
  - documented the gotcha and committed ADR-0001 entry as `d326113fc`
  - marked listen endpoint options as reconnect-required and added focused regression coverage for connected listen-port updates setting `PendingReconnect`
  - validation passed: `git diff --check` for touched files, `bash ./bin/lint`, focused `ApplicationLifecycleTests`, and rerun of the one flaky optional live mesh integration test
  - full `dotnet test` passed unit/non-integration projects and failed once only on the known optional live mesh account setup-time overlay `502`; the exact failing test passed on rerun
- Next steps:
  1. Commit and push the upload listen endpoint fix.
  2. If this needs to supersede release `.179`, tag a new build after push.

## Update 2026-04-26 19:14:31Z

- Current task: None. Bas upload troubleshooting instrumentation is implemented, validated, committed, and pushed to `snapetech/slskdn` `main`.
- Last activity:
  - added structured `[UPLOAD-DIAG]` logs when remote peers request uploads and when those requests are rejected
  - added authenticated `/api/v0/transfers/uploads/diagnostics` with configured listener details, local TCP probe, share/index state, upload counters, recent upload records, and actionable warnings
  - added focused unit coverage for the new diagnostics endpoint
  - validation passed: `git diff --check`, `bash ./bin/lint`, focused `TransfersControllerTests`, and full `dotnet test`
  - pushed commit `1fcfbcece` to `main`
- Next steps:
  1. If this needs to ship to testers immediately, create a new build tag only after explicit release/tag confirmation.
  2. Tell Bas to collect `/api/v0/transfers/uploads/diagnostics` output and `[UPLOAD-DIAG]` log lines on the next build.

## Update 2026-04-26 17:13:39Z

- Current task: Tester upload/DHT onboarding feedback is being investigated.
- Last activity:
  - confirmed the DHT public-discoverability warning used internal option names while YAML binds the public key under `dht:`
  - added `dht.lan_only` to the example config and updated the runtime warning to mention `dht.lan_only: true` / `dht.enabled: false`
  - documented the gotcha in ADR-0001 and committed that docs-only entry as `29d4d4958`
  - confirmed upload troubleshooting should collect listener bind logs, attempted remote upload logs, `/api/v0/options`, `/api/v0/application`, `/api/v0/transfers/uploads?includeRemoved=true`, and host/container port state
- Next steps:
  1. Run validation for the small DHT warning/config change.
  2. Commit and push the implementation/docs update if validation passes.
  3. Send tester-facing upload diagnostics checklist and DHT config guidance.

## Update 2026-04-24 16:10:07Z

- Current task: None. The AUR permission fix and issue `#209` early mesh-result publication fix are committed and pushed to `snapetech/slskdn` `main`.
- Last activity:
  - rebased local `main` onto remote release metadata commit `c93cf1653`
  - pushed four commits to GitHub through `00742f9cd`: AUR permission gotcha, mesh publication-delay gotcha, AUR permission fix, and early mesh-result publication fix
  - verified local and remote `main` both resolve to `00742f9cd4f17aca09e293736be8a7f47c77c467`
  - checked Actions: no current release/tag workflow is running; only CodeQL and dependency-submission checks started from the `main` push
  - confirmed published release `0.24.5-slskdn.177` / build tag `build-main-0.24.5-slskdn.177` does not contain these fixes because the build tag points at `92fa389c`
- Next steps:
  1. If these fixes should ship immediately, create a new build tag after explicit user confirmation, likely `build-main-0.24.5-slskdn.178`.
  2. Do not cancel anything unless a new release/tag workflow is found running.

## Update 2026-04-24 15:47:21Z

- Current task: Issue `#209` mesh-result UX follow-up is implemented and locally validated.
- Last activity:
  - inspected the latest tester follow-up and confirmed the current symptom is not peer acquisition delay: DHT/overlay were ready with `activeMesh=8`, mesh search returned `beatles` results at `09:22:39`, and the combined search completed at `09:22:54` after the normal 15-second Soulseek timeout
  - documented the gotcha in ADR-0001 and committed the docs-only entry as `713fe1fcf`
  - changed `SearchService` to persist and broadcast merged mesh/pod responses as soon as the mesh overlay search returns, while preserving final Soulseek+mesh merging at search completion
  - changed the search detail page to refetch responses when early result counts appear, not only when `isComplete` flips
  - validation passed: focused backend `SearchServiceLifecycleTests`, focused frontend search tests, frontend lint, `git diff --check`, and `bash ./bin/lint`
  - full `dotnet test --no-restore` passed the unit and non-integration projects, then failed one integration case (`TwoFullInstances_CanFormOverlayMeshConnection`) with setup-time HTTP 502; rerunning that exact integration test by itself passed
- Next steps:
  1. Reply on issue `#209` after this change is pushed/released, explaining that mesh results should now appear before slow/empty Soulseek searches finish.
  2. Do not create build tags unless explicitly requested.

## Update 2026-04-24 15:43:56Z

- Current task: None. The AUR release payload permission fix is implemented, locally validated, committed, and published to the live `slskdn-bin` AUR package.
- Last activity:
  - investigated AUR user feedback for `slskdn-bin 0.24.5.slskdn.177-1`, where `/usr/lib/slskd/releases/0.24.5.slskdn.177/` installed as `drwx------ root root` and blocked systemd/non-root startup
  - identified the staging-mode leak from `mktemp -d` plus archive-preserving copy into the release root
  - documented the gotcha in ADR-0001 and committed the docs entry as `a75d5783f`
  - updated `PKGBUILD`, `PKGBUILD-bin`, and `PKGBUILD-dev` to normalize release payload permissions with `chmod -R u=rwX,go=rX "${release_root}"` and apphost mode `755`
  - updated packaging metadata validation and the changelog/memory-bank records
  - validation passed: `bash packaging/scripts/validate-packaging-metadata.sh`, `bash ./bin/lint`, `git diff --check`, and local package-function smokes proving source/binary/dev release roots install as `0755`
  - published `slskdn-bin 0.24.5.slskdn.177-2` to AUR with commit `6766f22`
- Next steps:
  1. Push the local repository commits when ready.
  2. Tell affected users to update to `slskdn-bin 0.24.5.slskdn.177-2`; if their helper reuses stale metadata, clear the helper cache and rebuild.

## Update 2026-04-23 00:00:00Z

- Current task: None. The AUR binary packaging hardening for the reported `0.24.5.slskdn.175-1` Manjaro missing-DLL failure is implemented and locally validated.
- Last activity:
  - confirmed the live `0.24.5-slskdn.175` Linux x64 release zip is self-contained and includes `Microsoft.AspNetCore.Diagnostics.Abstractions.dll`, so the issue was not an upstream/slskd or GitHub release-asset regression
  - documented the AUR binary zip-staging gotcha in ADR-0001 and committed that docs entry as `7cd2cb9e1`
  - changed `PKGBUILD-bin` and `PKGBUILD-dev` to use `noextract`, unzip the downloaded archive during `package()`, and fail if the staged bundle is missing `slskd`, `slskd.deps.json`, or `Microsoft.AspNetCore.Diagnostics.Abstractions.dll`
  - updated the AUR README, changelog, and packaging metadata validator to match the new binary staging path
  - validation passed: packaging metadata script, `git diff --check`, direct release-zip smoke, and `bash ./bin/lint`; repo-wide `dotnet test` still has unrelated DNS/wildcard-sensitive failures in existing unit tests
- Next steps:
  1. Push the AUR packaging fix when ready and let the next stable/dev AUR publish carry it.
  2. If the Manjaro reporter is still blocked before the next release, reply with a temporary workaround focused on clearing the `yay` cache and rebuilding `slskdn-bin`.

## Update 2026-04-22 18:57:24Z

- Current task: Issue `#209` live mesh proof is complete for the two-account sandbox path.
- Last activity:
  - generated two fresh short alphanumeric Soulseek test accounts and recorded them in the gitignored live mesh env file
  - stored the same credential set in OpenBao at `secret/slskdn/mesh-live-test-accounts`
  - fixed live full-instance harness config/auth gaps: explicit Soulseek server endpoint, unique child listen ports, slower login polling with 429 tolerance, and API-key auth for mutating setup endpoints
  - proved the public-network path: the live-account smoke logged both accounts into Soulseek, hosted a generated probe file on beta, mesh-searched it from alpha, downloaded it through the pod path, and byte-compared the transfer
  - validation passed: standalone live-account smoke and full `TwoNodeMeshFullInstanceTests` class (`3` tests)
- Next steps:
  1. Run lint/diff checks and commit the live harness fixes plus memory/changelog updates.
  2. Do not create a build tag unless explicitly requested.

## Update 2026-04-22 18:28:38Z

- Current task: Issue `#209` mesh proof is locally strengthened with an optional live-account smoke; public logged-in sandbox validation is pending credentials.
- Last activity:
  - confirmed the deterministic two-full-instance mesh test already proves search result discovery plus pod transfer and byte comparison over the real overlay stack
  - added `OptionalLiveAccounts_CanSearchAndDownloadHostedProbeOverOverlayMesh`, which starts two full slskdN instances with configured Soulseek test credentials, waits for both to log in, hosts a generated probe on beta, mesh-searches from alpha, downloads it, and byte-verifies the file
  - validation passed: focused `TwoNodeMeshFullInstanceTests` (`3` tests), `bash ./bin/lint`, and `git diff --check`
  - no gitignored `tests/slskd.Tests.Integration/local-mesh-accounts.env` file is present here, so the public-network live-account branch is ready but not yet exercised
- Next steps:
  1. Populate `tests/slskd.Tests.Integration/local-mesh-accounts.env` with the two test credentials or export the matching environment variables.
  2. Run the optional live-account smoke to prove public logged-in mesh search and transfer end to end.
  3. Do not create a build tag unless explicitly requested.

## Update 2026-04-22 17:25:51Z

- Current task: Issue `#209` troubleshooting has a live answer from `local test host`: normal Soulseek search works after removing auto-replace budget contention; mesh search still has no active peers because discovered overlay endpoints are unreachable.
- Last activity:
  - deployed `0.24.5-slskdn.174+manual.0214ccc8b` to `/usr/lib/slskd/releases/manual-0214ccc8b`; API reports the matching executable/version and systemd is active with `NRestarts=0`
  - verified source-separated logs: auto-replace spends `source=auto-replace`, user/API searches spend `source=user`
  - reran user/API searches with the correct millisecond timeout (`10000`): `radiohead` returned `2` responses / `518` files, `pink floyd` returned `6` / `628`, `nirvana` returned `3` / `1148`, and `beatles` timed out with zero
  - confirmed the earlier API zero-result repro used `searchTimeout: 10`, which the API DTO documents as seconds but the underlying Soulseek option treats as milliseconds; documented the gotcha as `47211c67`, patched API/discovery timeout conversion, and verified live that `searchTimeout: 10` now returns `radiohead` results over a real user search
  - sampled DHT/overlay after startup: DHT running with `7` discovered peers, but `0` active mesh connections and `0` successful overlay connections; failures are connect timeouts/no-route
- Next steps:
  1. Continue mesh reachability work from the overlay side, not the normal Soulseek search path.
  2. Push the local commits when ready; do not create a build tag unless explicitly requested.

## Update 2026-04-22 16:55:30Z

- Current task: Issue `#209` troubleshooting found and fixed a second local app issue: background auto-replace was sharing the user/API search safety limiter bucket. Implementation is validated locally and ready to commit/push.
- Last activity:
  - confirmed the earlier `local test host` zero-result manual searches were not a clean population test because auto-replace was repeatedly charging `SafetyLimiter.TryConsumeSearch("user")`
  - documented the background-search budget gotcha in ADR-0001 and committed it as `1a9cb7dc2`
  - added an explicit search safety source overload, preserving `user` for UI/API callers and charging auto-replace to `auto-replace`
  - added source-aware search rejection logs and completion summaries with Soulseek/mesh/merged response counts, file count, and duration
  - validation passed: focused unit search/auto-replace/API slice (`13` tests), Release app build, and Release integration test project build
- Next steps:
  1. Run lint and diff checks.
  2. Commit the implementation/docs update.
  3. Install or otherwise deploy the fixed build on `local test host`, then rerun clean manual searches while watching the new source-aware logs.

## Update 2026-04-22 16:55:00Z

- Current task: Issue `#209` triage on `local test host` is complete locally; two app-side fixes are implemented and validated, but the code changes still need commit/push if they should ship.
- Last activity:
  - confirmed `local test host` is already running `slskdn-bin 0.24.5.slskdn.174-1`, systemd `active/running`, `NRestarts=0`, Soulseek logged in, shares ready, DHT ready with `250` nodes, and overlay TCP listening on `50305`
  - sampled DHT/overlay diagnostics showing a thin verified overlay (`activeMesh=1`, `successes=1`) with most public candidates failing by timeout/no-route/TLS EOF; this supports remote endpoint quality/population as the mesh-connectivity limit, not a local DHT bootstrap failure
  - reproduced zero-result normal API searches for `radiohead` and `beatles`, but auto-replace was repeatedly exhausting the search safety budget, so persistent Soulseek zero-result behavior needs a separate focused pass
  - fixed fake fatal unobserved task noise for normal remote transfer rejections (`Too many megabytes`, `Too many files`)
  - stopped circuit maintenance from automatically running placeholder circuit-building probes against live peers
  - documented both gotchas in ADR-0001 and committed that docs entry as `95702a2dd`
  - validation passed: focused unit slice (`42` tests), Release build, `bash ./bin/lint`, and `git diff --check`
- Next steps:
  1. Commit and push the code/docs/changelog/memory updates if approved.
  2. If search zero-results remain important, pause or disable auto-replace search consumption and run a clean Soulseek search comparison.

## Update 2026-04-21 08:21:42Z

- Current task: SongID UX cleanup and the prior `173` release-gate unit failure are fixed and validated; next step is committing, pushing, and tagging the requested release.
- Last activity:
  - audited the SongID page headlessly against the `local test host` backend with a YouTube URL
  - fixed duplicate result actions/candidates, noisy diagnostic exposure, and the duplicate graph/atlas action in `SongIDPanel`
  - isolated static event subscriber-count tests in a non-parallel xUnit collection after the `173` release-gate failure
  - validated with frontend lint, focused Search UI tests, production web build, headless render pass with no HTTP 4xx/5xx responses, full Release unit suite, repo-wide `dotnet test`, and `bash ./bin/lint`
- Next steps:
  1. Commit and push the validated changes.
  2. Create the next requested build tag and watch the release gate.

## Update 2026-04-21 06:45:00Z

- Current task: Validating the `0.24.5-slskdn.171` yay install on `local test host` and fixing actionable live noise.
- Last activity:
  - confirmed `slskdn-bin 0.24.5.slskdn.171-1` was installed, but the service was still running old PID `2151618` from `/usr/lib/slskd/releases/0.24.5.slskdn.170`
  - restarted `slskd`; the service is now running PID `2269037` from `/usr/lib/slskd/releases/0.24.5.slskdn.171`, `active/running`, `NRestarts=0`, with API `/api/v0/application` reporting `0.24.5-slskdn.171`
  - verified the duplicate MeshDHT self-descriptor publish fix holds on actual `171` startup: one `[MeshDHT] No configured endpoints...` and one `[MeshDHT] Published self descriptor...`, not the previous duplicate pair
  - verified web/API, Soulseek login, shares, and overlay TCP listener are up; DHT was still in the bootstrap window on the first post-restart API sample
  - found a remaining fake fatal from the old `170` PID before restart: `Soulseek.ConnectionReadException` with inner `IOException`/`SocketException` timeout from `Soulseek.Network.MessageConnection.ReadContinuouslyAsync`
  - documented that classifier gotcha in ADR-0001 and committed it as `2d5ee08bc`
  - patched `Program.IsExpectedSoulseekNetworkException(...)` so `Connection timed out` and `Unable to read data from the transport connection` inner exceptions match expected Soulseek network churn
  - validation passed so far: focused `ProgramPathNormalizationTests` (`29` tests), Release build, `bash ./bin/lint`, and `git diff --check`
- Next steps:
  1. Re-sample `local test host` after DHT bootstrap completes and watch for fresh current-PID fatal/error/coredump/overlay cooldown noise.
  2. Commit and push the read-timeout classifier fix after changelog/memory updates.
  3. Do not create another build tag unless explicitly requested.

## Update 2026-04-21 06:22:00Z

- Current task: None. The `local test host` log-polish findings are fixed in `main` and pushed; installed `0.24.5-slskdn.170` is still running until the next requested release/package update.
- Last activity:
  - confirmed the live yay package and API still report `0.24.5-slskdn.170`, systemd is `active/running`, `NRestarts=0`, Soulseek is `Connected, LoggedIn`, shares are ready, DHT is running, and overlay TCP is listening on `50305`
  - sampled current-process logs and coredumps; no fresh fatal/error/exception/502/coredump/bind/protocol noise appeared
  - found the remaining actionable noise was normal remote overlay endpoint cooldown streaks logging one information line per endpoint even though the aggregate DHT/overlay summaries and `/api/v0/overlay/stats` already expose the degraded endpoint state
  - documented the logging gotcha in ADR-0001 and committed it as `0018e6b90`
  - changed `MeshOverlayConnector.RecordFailure` so per-endpoint cooldown streak detail logs at debug while the aggregate summaries remain information-level
  - validation passed: focused DHT/rendezvous unit slice (`105` tests), Release build, `bash ./bin/lint`, changelog validation, and `git diff --check`
  - pushed the fix as `3f901d944`
  - final API check against the real web listener on `5030` returned quickly for `/api/v0/application`, `/api/v0/dht/status`, and `/api/v0/overlay/stats`; earlier timeouts were from probing the Soulseek listener on `50300`
  - installed `170` still logs the duplicate descriptor publish and per-endpoint cooldown detail because those fixes landed after the package was built
- Next steps:
  1. Keep `local test host` on installed build `170`; do not create another build tag unless explicitly requested.
  2. Validate the duplicate descriptor and cooldown log cleanup after the next release/package install.

## Update 2026-04-21 05:59:00Z

- Current task: Removing Snap publishing from release workflows after `0.24.5-slskdn.170` got stuck waiting on Snap Store publication.
- Last activity:
  - confirmed `local test host` is still running `slskdn-bin 0.24.5.slskdn.170-1` from systemd PID `2151618`, `active/running`, `NRestarts=0`, `ExecMainStatus=0`
  - verified authenticated API state: `/api/v0/application` reports `0.24.5-slskdn.170`, Soulseek is `Connected, LoggedIn`, shares are ready, DHT is running, and the TCP overlay listener is active on `50305`
  - monitored current-process journal after the auto-replace cycle started; no fresh fatal/error/exception/502/coredump/rate-limit/search-budget noise appeared, and no new `slskd` coredumps were present
  - found one fixable startup polish issue: `MeshBootstrapService` and `PeerDescriptorRefreshService` both published the same self descriptor at startup, producing duplicate `[MeshDHT] No configured endpoints...` and `[MeshDHT] Published self descriptor...` lines
  - documented the duplicate-publish gotcha in ADR-0001 and committed it as `a4e516468`
  - changed `PeerDescriptorRefreshService` so periodic refresh scheduling starts from current time, leaving the bootstrap service as the startup publisher while still allowing IP-change-triggered refreshes
  - validation passed: focused `PeerDescriptorRefreshServiceTests`, full unit suite (`3553` tests), Release build, `bash ./bin/lint`, and `git diff --check`
  - pushed the duplicate self-descriptor publish cleanup as `a1f105521`; CodeQL and dependency submission for that push both passed
  - removed Snap publishing from the tag workflow, converted the manual dev helper workflow to Docker-only, and stopped release metadata scripts/validators from requiring Snap manifest updates
  - validation for the Snap purge passed: YAML parse for touched workflows, packaging metadata validation, changelog validation, and `git diff --check`
- Next steps:
  1. Commit and push the Snap workflow purge.
  2. Cancel the still-running `build-main-0.24.5-slskdn.170` workflow if only Snap remains in progress.
  3. Do not create another build tag unless explicitly requested.

## Update 2026-04-21 05:06:00Z

- Current task: Validating the `0.24.5-slskdn.169` yay install on `local test host` and fixing actionable live noise.
- Last activity:
  - confirmed `local test host` is running `slskdn-bin 0.24.5.slskdn.169-1` from systemd PID `2045334`, `active/running`, `NRestarts=0`, with `ExecMainStatus=0`
  - verified the installed binary reports `0.24.5-slskdn.169`; current-process startup bound the expected web, Soulseek, DHT, and TCP overlay listeners
  - ran the bounded Playwright route/tab sweep: `/tmp/local test host-route-tab-sweep-2026-04-21T04-55-23-627Z.md`, `307` visits, `1691` same-origin responses, status summary `{"200":1687,"204":4}`, `0` issues, and no 4xx/5xx/502 responses
  - triaged the sweep warnings: most are scanner false positives from `/system/options` literal `false`/disabled config text, but `/system/logs` exposed a real current-process fatal unobserved `NullReferenceException` from `Soulseek.Extensions.Reset(Timer timer)` in a Soulseek.NET write path
  - documented and patched the live stack-signature miss so expected Soulseek timer-reset read/write races match `Reset(` instead of the synthetic exact `Reset(Timer)` string
  - also quieted clean shutdown telemetry so normal SIGTERM/systemd restart paths no longer print warning/abnormal `app.Run()`/duplicate stderr lines
  - validation so far: focused `ProgramPathNormalizationTests`/expected-network unit slice passed (`30` tests)
- Next steps:
  1. Run Release build, lint, and diff checks.
  2. Commit and push the shutdown/log-noise fixes after GitHub target verification.
  3. Do not create another build tag unless explicitly requested.

## Update 2026-04-21 04:24:00Z

- Current task: Validated the `0.24.5-slskdn.168` yay install on `local test host` and fixed the actionable issues found locally.
- Last activity:
  - confirmed `pacman`, `/usr/bin/slskd --version`, the active release symlink, and the authenticated API all report `0.24.5-slskdn.168`
  - verified the service is active, Soulseek is `Connected, LoggedIn`, shares are ready, DHT is running, and the expected TCP/UDP listeners are present after a clean restart
  - found the initial package start had lost the TCP overlay listener because `50305` hit a transient `Address already in use`; fixed rendezvous startup to retry transient overlay bind failures and added focused unit coverage
  - reduced remaining live startup journal noise by demoting method-entry/constructor/security-pipeline probe logs to debug and keeping stdout/stderr probes behind explicit E2E trace environment variables
  - investigated the red `build-main-0.24.5-slskdn.168` Actions run and found the release artifacts were published; only the final Matrix announcement failed with HTTP 504, so the announcement webhooks now retry and degrade to warnings
  - validation passed: YAML parse, focused DHT rendezvous tests, full unit suite, Release build, `./bin/lint`, and `git diff --check`
- Next steps:
  1. Commit and push the current fix set after the final `local test host` log sample.
  2. Do not create another release/build tag unless explicitly requested.

## Update 2026-04-21 04:08:00Z

- Current task: Completed the GitHub Releases page audit/rewrite.
- Last activity:
  - edited every published GitHub release body currently on `snapetech/slskdn`, from `0.24.5-slskdn.123` through `0.24.5-slskdn.168`
  - each release now describes the delta from the previous published release, links the compare range, groups changes by type, and lists exact commits with direct links
  - waited for the new `0.24.5-slskdn.168` release object to appear, then rewrote that body too with the `0.24.5-slskdn.167...0.24.5-slskdn.168` delta
  - verified `37` release bodies through GitHub after editing; no required release-note sections were missing
- Next steps:
  1. Let the `build-main-0.24.5-slskdn.168` workflow finish its remaining package jobs.
  2. If the release-notes generator should keep this stricter all-commit format automatically, update `scripts/generate-release-notes.sh` in a separate code change.

## Update 2026-04-21 03:58:00Z

- Current task: Completed the requested release, push, manual `local test host` redeploy, monitoring pass, and fixable-noise cleanup.
- Last activity:
  - created and pushed `build-main-0.24.5-slskdn.167`; release run `24702224025` produced the main release artifacts and updated stable metadata
  - fixed scheduled E2E/test target-framework drift by discovering the app target framework from `src/slskd/slskd.csproj`; gotcha documented as `a50e34bf6`, fix pushed as `2e4cc934c`
  - deployed `0.24.5-slskdn.167+manual.2e4cc934c` to `local test host`, then used the route/tab sweep to isolate the remaining fixable browser-visible noise to optional user-info badge 404s for offline historical downloads
  - added quiet optional user-info lookups so badge misses return `204 No Content` without changing default API semantics; gotcha documented as `2f52e3bed`, fix pushed as `9c1d3f14d`
  - deployed `0.24.5-slskdn.167+manual.9c1d3f14d` to `/usr/lib/slskd/releases/manual-9c1d3f14d`; service is `active/running`, PID `1887195`, `NRestarts=0`, Soulseek `Connected, LoggedIn`, shares ready
  - final Playwright route/tab sweep report `/tmp/local test host-route-tab-sweep-2026-04-21T03-44-45-634Z.md`: `307` visits, `1683` same-origin responses, `{"200":1680,"204":3}`, `0` issues, `0` 4xx/5xx/502 responses
  - fresh logs/coredumps after the final sweep show no new `slskd` coredumps and no actionable current-process fatal/error/exception/502/bind/protocol noise; remaining warnings are `/system/options` scanner false positives and expected DHT churn summaries
- Next steps:
  1. Continue passive monitoring if requested.
  2. Check any still-running downstream package jobs from release run `24702224025` if release packaging status is needed.
  3. Do not create another build tag unless explicitly requested.

## Update 2026-04-21 03:12:00Z

- Current task: Completed `local test host` manual-build testing/fixing pass.
- Last activity:
  - pushed final commits through `15ba2a423` to `snapetech/slskdn`
  - deployed `0.24.5-slskdn.165+manual.15ba2a423` to `local test host`
  - verified service health: `active/running`, `NRestarts=0`, no new `slskd` coredumps, correct version from `/api/v0/application`
  - verified QUIC remains opt-in by default: `slskd` listens on UDP `50306/50400` and TCP `5030/5031/50300/50305`, with no `slskd` listener on `50401/50402`
  - ran the full bounded Playwright route/tab sweep: `/tmp/local test host-route-tab-sweep-2026-04-21T03-02-32-124Z.md`, `307` visits, `1680` responses, `0` issues, `0` HTTP 5xx/502s, three expected offline user-info `404`s
  - fresh logs show only known operational warnings and normal DHT/overlay churn; no `[DI]`, SPA fallback, CSRF, QUIC, user-info stack, fatal, coredump, or fresh search-cancellation error noise from the final build
- Next steps:
  1. Continue passive monitoring if requested.
  2. Do not tag or trigger a release build unless explicitly asked.

## Update 2026-04-21 02:20:00Z

- Current task: None. The `local test host` user-info 500 fix is committed, pushed, deployed, and retested; the route/tab Playwright pass is clean for current 5xx/502/page-error signals.
- Last activity:
  - controlled Playwright crawling against `local test host` found no real HTTP 502 responses, but did find `/api/v0/users/{username}/info` returning HTTP 500 for expected Soulseek peer connection failures and timeouts
  - documented the peer-info gotcha in ADR-0001 and committed the docs-only entry as `1699cf7b5`
  - updated `UsersController.Info` so explicit offline users remain 404, while peer connection failures and info timeouts return generic 503 responses without exception-object stack noise
  - added focused controller coverage for connection failure, direct timeout, and wrapped timeout cases; `UsersControllerTests`, Release build, lint, diff check, and GitHub target verification passed
  - committed and pushed the fix as `5bd0e0b88`
  - published and deployed `0.24.5-slskdn.165+manual.5bd0e0b88` to `local test host`; corrected live launcher drift so `/usr/lib/slskd/slskd` execs `/usr/lib/slskd/current/slskd`, documented as ADR-0001 gotcha `deafb040b`
  - verified `/api/v0/application` reports the new payload, user-info peer failures now return controlled `503`, and the current process has no 500/502/fatal/protocol/bind/coredump noise
  - ran Playwright route/tab sweep report `/tmp/local test host-route-tab-sweep-2026-04-21T02-09-22-685Z.md`: all top-level routes and System tabs were exercised, with only one expected user-info 404 and no 5xx responses
- Next steps:
  1. Keep `local test host` soaking on PID `1642135` and resample later for long-run mesh/download noise.
  2. Treat the remaining broad dynamic `/searches/{id}` link corpus separately if exhaustive historical search-detail crawling is still desired; the bounded product route/tab pass did not show current 5xx or route-miss failures.
  3. Do not create a release/build tag unless explicitly requested.

## Update 2026-04-21 00:11:00Z

- Current task: Finish, push, deploy, and monitor the auto-replace search-budget fix found during `local test host` manual-build soak.
- Last activity:
  - live monitoring found auto-replace issuing a large stuck-download batch until `Search rate limit exceeded` triggered, then logging repeated per-track stack traces and marking the cycle as failed
  - documented the gotcha in ADR-0001 and committed that docs-only entry as `138f3a6c0`
  - updated `AutoReplaceService` so alternative searches are paced by `Soulseek.Safety.MaxSearchesPerMinute`, safety-budget rejection defers the current item, and the cycle stops early instead of logging one error per remaining track
  - added unit coverage for the budget-exhaustion path and confirmed the focused auto-replace/program test slice, Release project build, lint, full `dotnet test`, and `git diff --check` pass
  - found generated `src/slskd/dist` output being included in the next publish artifact, documented it in ADR-0001 as `fe0ab5ea9`, and excluded `dist/**` from the app project's default items
  - confirmed the live paced cycle no longer hits the search limiter, then documented and fixed routine per-track no-result log noise by moving it to `Debug`
  - found restart/re-enqueue stack traces for expected remote-offline download failures, documented the gotcha as `d3bfa41cb`, and changed those failures to warning summaries without masking transfer failure state
  - found deploy-time auto-replace shutdown cancellation being logged as search errors from the previous PID, documented the gotcha as `5a10e6cdc`, and changed caller-token cancellation to stop the hosted service cleanly
  - found the next live cycle still produced routine shared search progress at `Information`, documented the gotcha as `f4191def3`, and moved per-search completion, mesh-search fallback/fanout, and passive HashDb discovery progress to `Debug`
  - confirmed the auto-replace shutdown path was fixed, then found the remaining handled Soulseek disconnect race still emitted a stack because the catch logged the exception object; documented the gotcha as `6dd4690e7` and changed it to a debug summary
  - found a current-process fatal unobserved task from a Soulseek.NET TCP double-disconnect read-loop race, documented the gotcha as `70b26eff5`, and added it to the expected network exception classifier
  - started the requested Playwright UI sweep and found `/system/network` correctly gates public DHT exposure behind a consent modal, but its inline code copy rendered `dht.lan_only=truein`; documented the gotcha as `bff3fa0fd` and fixed the spacing
- Next steps:
  1. Validate, commit, push, and redeploy the DHT exposure modal copy fix to `local test host`.
  2. Continue the controlled Playwright UI pass against the updated manual build and document real endpoint/UI issues separately from route-change request aborts.

## Update 2026-04-20 23:55:00Z

- Current task: None. The current `local test host` manual build is stable in the latest sample, and the local formatter/compile cleanup from the validation pass is complete.
- Last activity:
  - resampled the current `local test host` service process (`PID 1335511`, active since `2026-04-20 17:37:10 CST`) and confirmed it remains active with `NRestarts=0` and `ExecMainStatus=0`
  - found no current-process journal matches for fatal unobserved exceptions, the Soulseek timer-reset classifier, native crash strings, disposed-object shutdown noise, listener bind failures, protocol violations, or invalid-frame logs
  - confirmed no new `slskd` coredumps since the current process start; the noisy timer-reset/fake-fatal entries were from older PIDs
  - fixed the local lint/formatter verification loop and cleaned compile/nullability issues in OpenAPI response mutation, QUIC task cleanup, relay pin validation, and cookie header stripping
  - documented the OpenAPI `IOpenApiResponse.Content` interface gotcha in ADR-0001 and committed that docs-only entry as `04d071597`
  - revalidated locally with Release build, focused unit slice (`31` tests), `bash ./bin/lint`, and `git diff --check`
- Next steps:
  1. Keep soaking `local test host` and only redeploy if the current process shows fresh crash/log evidence or the remaining local changes need to be promoted.
  2. Treat older pre-current-PID fatal/timer-reset logs as historical unless they recur on the current PID.
  3. Do not create a build tag unless explicitly requested.

## Update 2026-04-20 15:55:00Z

- Current task: None. The live `local test host` redeploy validated the timer-reset log-noise fix and exposed native crash recurrence as the next real host issue.
- Last activity:
  - confirmed the historical `#201` signatures are still not the current live failure mode: current `main` contains the startup-listener fix, `/system/info` renders locally, and `local test host` remains reachable/listening after deploy
  - committed and deployed `ffacda09e`, publishing `0.24.5-slskdn.159+manual.ffacda09e` to `local test host`
  - verified post-deploy health: service active, Soulseek connected/logged in, DHT ready, overlay and QUIC listeners bound, and one active mesh connection after restart recovery
  - sampled the live journal and confirmed the targeted fix held: no new `[FATAL] Unobserved task exception` entries and no `Soulseek.Extensions.Reset(Timer)` teardown noise in the observed window
  - found a different live blocker during soak: the process segfaulted once (`SIGSEGV`) and systemd restarted it automatically; `coredumpctl` shows similar recent native crashes on earlier manual builds too
  - observed the new DHT/overlay summary logging working as intended on the recovered process, with explicit failure mix and degraded endpoint rollups
- Next steps:
  1. Investigate the native `SIGSEGV` path on `local test host`, starting from the recent `coredumpctl` history and any commonality with active `libmsquic` worker threads.
  2. Decide whether to add crash-oriented symbolization or host-side runtime diagnostics before another manual/live rollout.
  3. Separately decide whether `AutoReplace` search-cap noise should be tuned, but treat it as secondary to the native crash path.

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
  2. Deploy the new DHT/overlay diagnostics to `local test host` and sample whether the summary lines clearly distinguish bad remote endpoints (`no-route`, `tls-eof`, etc.) from local capacity/backoff behavior.
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

- Current task: None. The latest `local test host` live-debug pass is implemented, committed, deployed, and host-validated.
- Last activity:
  - kept QUIC enabled and healthy on `local test host`, with overlay/data listeners active and mesh reconnecting after each deliberate restart
  - fixed `DownloadService` shutdown cleanup ordering by draining in-flight enqueue/download work before the shared Soulseek client is disconnected or disposed
  - fixed the remaining false-fatal shutdown edge by tolerating the third-party `SoulseekClient.Disconnect()` `Sequence contains no elements` race during expected host shutdown
  - deployed `0.24.5-slskdn.159+manual.1475cd068` to `local test host` and validated deliberate restarts with active downloads: no global download semaphore warnings, no transfer cleanup `ObjectDisposedException`, no false fatal shutdown log, restart count `0`, DHT healthy, and one mesh peer connected immediately after restart
- Next steps:
  1. Keep sampling `local test host` for longer-run QUIC and mesh stability, especially whether the single live peer stays connected past the keepalive windows on this final manual build.
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
  1. Redeploy the latest `Program` classifier fix to `local test host` and verify that remote-declared transfer failures no longer emit fake `[FATAL] Unobserved task exception` noise on the new process.
  2. Keep sampling multi-peer downloads on `local test host` to see whether any remaining post-`InProgress` failures cluster around one transport/peer pattern or are just normal remote-side churn.

# Active Context

> What is currently being worked on in this repository.  
> Update this file when starting or finishing work.

---

## ⚠️ WORK DIRECTORY (do not use <workspace>/cursor)

**Project root: `<repo-root>`**

All `git`, `dotnet`, and file paths in this repo are under this directory. Do not use the `cursor` folder under `~/Code/` for slskdn work — it is a separate project.

---

## 🚨 Before Ending Your Session

**Did you fix any bugs? Document them in `adr-0001-known-gotchas.md` NOW.**

This is the #1 most important thing to do before ending a session. Future AI agents (and humans) will thank you.

---

## Current Session

- **Current Task**: Investigating the recurring native `SIGSEGV` on `local test host`; the latest pass narrowed it to the old manual single-file/ReadyToRun publish shape and aligned `bin/publish` with the tagged release profile now running live on the host.
- **Branch**: `main`
- **Environment**: Local dev on `snapetech/slskdn`; live validation on `local test host` currently running `0.24.5-slskdn.159+manual.ffacda09e`; no release tags were created.
- **Last Activity**:
  - Kept QUIC enabled by installing Microsoft MsQuic `v2.5.7` on `local test host`; QUIC overlay/data listeners bind on `50402/50401` with no temporary systemd disable override.
  - Fixed live mesh compatibility with unframed JSON overlay frames; `local test host` connected to `m***7` and held past the 2-minute keepalive threshold without `Protocol violation`, `Invalid message length`, `Unregistered`, or disconnect logs.
  - Fixed DHT rendezvous accounting so connector-capacity deferrals are not counted as real attempts or pushed into retry backoff.
  - Fixed user directory browse API handling so expected remote peer connection failures return controlled 503 responses instead of unhandled middleware stack traces.
  - Fixed service SIGTERM handling so normal `systemctl restart` stops the host cleanly; validated on `local test host` that a deliberate restart logs expected shutdown, not status 1/failure.
  - Fixed transfer shutdown cleanup ordering so active downloads drain before Soulseek client disposal, removing restart-time semaphore/disposed-object noise.
  - Fixed the remaining third-party `SoulseekClient.Disconnect()` shutdown race so clean restarts do not emit false fatal `Sequence contains no elements` logs.
  - Deployed `0.24.5-slskdn.159+manual.ffacda09e` to `local test host` and confirmed the targeted fake-fatal Soulseek timer-reset teardown noise no longer appeared in the observed journal window.
  - During soak, observed one native `SIGSEGV` on the deployed process; systemd restarted the service cleanly and the recovered process rejoined the mesh.
  - `coredumpctl` on `local test host` shows similar recent native crashes on earlier manual builds, so the crash path predates `ffacda09e` and is now the highest-priority live issue.
  - Investigated the crash producer and found concrete QUIC connection-lifecycle gaps: cached/orphaned `QuicConnection` instances were not always disposed, and QUIC hosted servers were not explicitly closing active connections or draining active connection tasks on stop.
  - Documented that QUIC lifecycle gotcha in ADR-0001 (`06ffdca5f`), then hardened `QuicOverlayClient`, `QuicDataClient`, `QuicOverlayServer`, and `QuicDataServer` with explicit connection gates, disposal, close, and stop-drain behavior.
  - During the first manual redeploy of that hardening, found a separate restart-time bug: `MeshOverlayServer` could fail to rebind port `50305` with `Address already in use` even though no live listener remained.
  - Documented that listener-reuse gotcha in ADR-0001 (`7a6eca0dd`), then fixed `MeshOverlayServer` to use `ReuseAddress` and to clear/dispose stop state fully on shutdown.
  - Published and manually deployed `0.24.5-slskdn.159+manual.quicfix2` to `local test host`; a deliberate restart on the recovered process now rebinds overlay TCP `50305` cleanly and restores overlay/DHT/QUIC listeners as expected.
  - `coredumpctl` still captured a new startup-time native `SIGSEGV` on PID `572060` during that rollout, but the immediate systemd retry recovered to a healthy process (`572286`) and a later deliberate restart stayed clean.
  - Pulled deeper kernel/coredump evidence and found the startup crashes were native `general protection fault` events in `.NET Server GC`, while `bin/publish` was still producing a self-contained single-file `ReadyToRun` artifact that does not match the tagged release workflows.
  - Documented that manual-publish drift gotcha in ADR-0001 (`975c754d2`), then aligned `bin/publish` with the tagged release profile by removing the single-file/native-self-extract path and explicitly disabling `PublishReadyToRun`.
  - Built and manually deployed `0.24.5-slskdn.159+manual.nor2r` to `local test host`; three consecutive startup paths (initial deploy plus two deliberate restarts) came up clean with no new `coredumpctl` entries and no new `/var/lib/slskd/.net/slskd/*` extraction directory.
  - Rechecked the earlier live journal and found one remaining app-side noise path: `Soulseek.Extensions.Reset(Timer)` could still surface as a fake fatal unobserved-task exception when it happened from `Soulseek.Network.Tcp.Connection.WriteInternalAsync(...)`, not just the already-classified read loop.
  - Documented that write-path gotcha in ADR-0001 (`b121d5da3`), extended `Program.IsExpectedSoulseekNetworkException(...)` to cover the write-loop stack shape too, and added a focused `ProgramPathNormalizationTests` regression for the exact live stack.
  - The host is still logging repeated `EXT4-fs ... checksum invalid` errors on `dm-0` from `containerd`, so host filesystem health remains a separate operational concern even though the CI-aligned publish shape is currently stable.
  - Validation passed: `dotnet build src/slskd/slskd.csproj --no-restore -v minimal`, focused QUIC/transport/program unit slice (`30` passed), `bash ./bin/lint`, `git diff --check`, manual publish/install to `local test host`, live journal sampling, listener/socket checks, and coredump inspection.
- **Next Steps**:
  1. Commit and deploy the `Program` write-loop classifier fix on top of `manual-nor2r`, then verify the host still starts cleanly and that no fresh fake fatal timer-reset logs appear.
  2. Keep the host on the CI-shaped publish profile while sampling for longer-run stability.
  3. If native crashes reappear on the CI-aligned publish shape, continue symbolization/runtime triage from the new narrower baseline instead of the old divergent manual artifact.
  4. Separately flag the repeated `EXT4-fs` checksum errors on `local test host` as an operational host-health risk outside slskd itself.

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

- Current task: None. The latest `local test host` live transfer and DHT follow-up is implemented locally and validated on-host.
- Last activity:
  - proved that successful transfers on `local test host` were still emitting fake fatal `Transfer failed: Transfer complete` unobserved-task noise and fixed the classifier in `Program`
  - opened `50305/tcp` and `50306/udp` in the `local test host` host firewall, then verified DHT eventually reaches `Ready` on the live host once both router and host firewall paths are open
  - proved the old 30-second DHT bootstrap warning was too aggressive on a healthy host and extended the bootstrap grace period to 120 seconds
- Next steps:
  1. Commit and push the current `Program` / DHT bootstrap follow-up if the worktree stays clean.
  2. Watch the next `local test host` runtime window for a successful transfer on the patched process and confirm the `Transfer complete` fatal log never returns.
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

## Update 2026-04-21 08:00:00Z

- Current task: None. The current `local test host` 172 log/UI sweep is triaged and the remaining fixable warning noise is patched locally.
- Last activity:
  - confirmed `local test host` is still running `0.24.5-slskdn.172`, systemd is active with zero restarts, Soulseek is logged in, DHT is running, overlay TCP is listening on `50305`, and no fresh fatal/error/exception/502/coredump/protocol noise appeared
  - reran focused Playwright checks with the corrected Web UI credentials; Library Health and Files nested tabs work, and a steady `/searches` hold produced no SongID hub console errors
  - documented and fixed the repeated entropy false-warning gotcha by increasing the RNG sample size to reduce finite-sample bias, and moved expected auto-replace no-result searches down to debug
  - fixed the flaky hosted-service unit test wait exposed by the full unit suite
  - validation passed so far: focused security event tests, focused circuit maintenance test, full unit suite, Release build, lint, changelog validation, and diff check
- Next steps:
  1. Commit and push the log-polish/test fix set.
  2. Do not create another build tag unless explicitly requested.

## Update 2026-04-22 18:20:00Z

- Current task: None. The latest issue `#209` live follow-up is implemented, committed locally, and deployed to `local test host` for validation.
- Last activity:
  - confirmed the current `local test host` build is `0.24.5-slskdn.174+manual.6fce6575c` from `/usr/lib/slskd/releases/manual-6fce6575c`, with systemd active and `NRestarts=0`
  - fixed mesh self-descriptor endpoint publication so automatic detection only advertises public-routable interfaces and configured self endpoints are not silently supplemented with private/container/VPN addresses
  - added info-level mesh-search fanout diagnostics when active overlay peers are queried
  - live validation showed DHT discovery and overlay connectivity are not stuck at zero: the host reconnected to one outbound `mesh_search` peer (`minimus7`), and a live `radiohead` search logged `peers=1 peersWithResults=0 emptyPeers=1 failedPeers=0` while core Soulseek returned `252` responses / `16686` files
- Next steps:
  1. Push the local issue `#209` commits if you want them on `origin/main`.
  2. If testers still expect non-zero mesh search results, collect runs with more than one active mesh peer or a known shared probe query; the current single connected peer is reachable but simply returned no files for `radiohead`.

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

- Current task: None. The next issue `#209` root fix is implemented locally and validated on `local test host`.
- Last activity:
  - stepped back from the earlier tester reports and revalidated the live overlay path on `local test host` instead of trusting the synthetic release gates
  - proved the current blocker was stale overlay TOFU pinning rather than version-locking: `minimus7` was a real reachable slskdn overlay peer, but a stale stored thumbprint caused our side to self-partition
  - changed inbound and outbound overlay handshakes to rotate stored certificate pins on mismatch instead of auto-blocking the peer, added focused `CertificatePinStoreTests`, and validated on `local test host` that the host now logs the mismatch, rotates the pin, and still registers/connects the neighbor in the same DHT cycle
- Next steps:
  1. Commit the pin-rotation fix set if the worktree stays clean.
  2. If another `#209` symptom appears, reproduce it on `local test host` first and add the missing host-backed smoke before cutting another build.


## Update 2026-04-18 17:55:00Z

- Current task: None. The next issue `#209` cleanup pass is implemented locally and validated on `local test host`.
- Last activity:
  - proved the live node was still overstating peer health after DHT discovery by counting raw DHT endpoints as onion-capable peers before overlay verification
  - changed DHT rendezvous publishing so discovered endpoints stay as `dht-discovered` candidates until a real overlay connect succeeds, and updated focused DHT rendezvous tests to cover candidate, failed-connect, and verified-connect states
  - redeployed the cleanup build to `local test host` and verified that `/api/v0/security/peers/stats` now reports `onionRoutingPeers: 0` while `/api/v0/dht/status` still shows the raw discovered candidate count separately
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

- Current task: None. The live DHT rendezvous retry/backoff fix is implemented and host-validated on `local test host`.
- Last activity:
  - traced the active mesh issue back to `DhtRendezvousService` using `_discoveredPeers.TryAdd(...)` as a once-ever outbound connect trigger, which meant the host never retried already-seen endpoints after a single timeout/refusal
  - split discovery caching from retry scheduling by adding explicit attempt timestamps and in-flight tracking with a 5-minute backoff for unverified peers
  - validated the change on `local test host` by forcing a post-backoff discovery cycle and observing `totalConnectionsAttempted` rise from `26` to `31` while `discoveredPeerCount` stayed at `26`, proving rediscovered candidates now re-enter the connector instead of staying stranded
- Next steps:
  1. Commit/push the retry/backoff fix if the worktree stays clean.
  2. Continue narrowing the live peer pool by filtering or deprioritizing clearly bad/non-overlay endpoints before they dominate mesh retries.

## Update 2026-04-29 22:35:00Z

- Current task: README screenshot showcase gallery is implemented locally.
- Last activity:
  - captured a varied open-license screenshot set and copied selected PNGs into `docs/assets/readme-showcase/`
  - added a clickable thumbnail gallery to `README.md`
  - patched Discovery Graph candidate filtering so manual-review SongID runs can render useful track-candidate atlas neighborhoods instead of collapsing to a single node
  - fixed the dirty `RegexUsernameMatcher` options type reference enough for the main project to compile past that file
- Next steps:
  1. Resolve the existing dirty `YamlConfigurationSourceTests` / `MusicBrainz.Enabled` compile mismatch before relying on full unit-test status.
  2. Review the README gallery layout in GitHub-rendered markdown and adjust captions/order if desired.

## Update 2026-04-29 22:54:04Z

- Current task: Web UI footer redesign is implemented locally.
- Last activity:
  - reorganized the footer into clearer brand/support, transfer speed, network/index, transport-health, and fork-note groups
  - added responsive footer behavior so lower-priority labels and the quote do not crowd the operational counters on narrower screens
  - browser-checked mocked logged-in footer states at `1920`, `1366`, and `390` pixel widths with no detected footer overflow
- Next steps:
  1. Review the footer against a real logged-in instance if you want to tune exact spacing/colors further.
  2. Commit/push the footer redesign once the surrounding dirty worktree state is ready.

## Update 2026-04-29 23:01:14Z

- Current task: Web UI footer live-rendering correction is implemented locally.
- Last activity:
  - inspected the live `local test host` footer and confirmed the first redesign still had brittle spacing in real light-theme rendering
  - changed the footer from rigid grid columns to a flex dock with wrapping network/status content and tighter icon alignment
  - injected the fixed CSS into the live `local test host` page and rechecked desktop, mid-width, and mobile footer captures with no visible-element overflow
- Next steps:
  1. Commit/push the footer CSS/JSX and memory-bank updates when ready.
  2. Deploy or release so `local test host` gets the fixed CSS without local injection.

## Update 2026-04-29 23:39:14Z

- Current task: Search page collapsible section persistence is implemented locally.
- Last activity:
  - made SongID, MusicBrainz Lookup, Discovery Graph Atlas, and Album Completion default collapsed
  - added stable local-storage keys so Search page panels remember the user's last open/collapsed state
  - added focused Search page tests for default collapsed secondary panels and stored collapsed primary-panel state
- Next steps:
  1. Commit and push the full workspace.
  2. Release/deploy when the UI behavior should land on `local test host`.

## Update 2026-04-30 01:40:00Z

- Current task: Player/listening-party smoke test with an open Commons file is complete locally.
- Last activity:
  - used Wikimedia Commons `Sample2.ogg` as the audio fixture and did not use Soulseek network credentials
  - verified the integrated stream endpoint returns ranged `audio/ogg` responses for the local `sha256:` content id
  - verified browser playback through two Vite dev servers, `localhost:3001` and `localhost:3002`
  - fixed and documented Gold Star Club pod/channel validation startup crashes found while bringing up the isolated backend
  - added the player smoke screenshot to the README showcase
- Next steps:
  1. Decide whether to make `library/items` automatically register local `sha256:` ids as advertisable stream mappings instead of needing a content-items seed.
  2. If desired, improve collection item display metadata so playlist rows show filenames/titles instead of raw content ids.

## Update 2026-04-30 01:47:00Z

- Current task: Browser-local player mute and mobile/PWA player support are implemented locally.
- Last activity:
  - added a persisted local mute toggle to the Web UI player that mutes only the current browser/PWA audio element
  - kept stream playback browser-owned via the existing integrated `/api/v0/streams/{contentId}` endpoint
  - added mobile/PWA player safe-area handling, inline audio playback, metadata preload, and larger touch targets
  - validated the player in a 390px mobile browser viewport against the live dev instance
- Next steps:
  1. Consider adding richer lock-screen media-session metadata once collection items resolve titles/artists reliably.
  2. Keep the content-id stream registration follow-up open so local library rows are consistently playable without manual seeding.

## Update 2026-04-30 04:10:00Z

- Current task: `2026042900-slskdn.204` release handoff is ready.
- Last activity:
  - verified GitHub writes target `snapetech/slskdn`
  - promoted the release notes out of `Unreleased`
  - added release prep notes for the external visualizer launcher and player/streaming work
  - made Winget release-version metadata validation opt-in because Winget is not part of this release
- Next steps:
  1. Monitor the `build-main-2026042900-slskdn.204` tag workflow after push.
  2. Triage any release workflow failure before cutting another tag.

## Update 2026-04-30 19:20:03Z

- Current task: Feature-expansion burn-down is continuing.
- Last activity:
  - wired the Search page acquisition profile selector into search-create API requests
  - added backend DTO/controller trimming and validation for known acquisition profile ids
  - covered default, selected, and invalid acquisition-profile request behavior with focused frontend/backend tests
- Next steps:
  1. Implement the profile policy layer that maps acquisition profiles to ranking, source selection, retry, and network-impact defaults.
  2. Promote more Automation Center recipes from visible local toggles into backend-backed job definitions.

## Update 2026-04-30 19:25:59Z

- Current task: Feature-expansion burn-down is continuing.
- Last activity:
  - mapped known acquisition profiles to bounded backend search option defaults
  - kept explicit request options as overrides for all mapped profile values
  - added controller coverage for profile default application and explicit override behavior
- Next steps:
  1. Extend profile policy beyond `SearchOptions` into result ranking and source preference.
  2. Move Automation Center recipe state from browser storage into backend-backed configuration/jobs.

## Update 2026-04-30 19:36:09Z

- Current task: Feature-expansion burn-down is continuing from the top of the list.
- Last activity:
  - added a read-only native source-provider catalog API
  - added System -> Source Providers with provider enablement, registration, capabilities, risk, network policy, and disabled reasons
  - kept the provider catalog non-invasive with no peer contact or credential checks
- Next steps:
  1. Continue down the beginning of the expansion list into provider ordering/profile policy.
  2. Avoid Discovery Inbox, Taste Recommendations, and MilkDrop files while parallel agents are actively editing them.

## Update 2026-04-30 19:41:54Z

- Current task: Feature-expansion burn-down is continuing from the top of the list.
- Last activity:
  - added per-profile provider priority policies to the source-provider catalog
  - surfaced those routing policies in System -> Source Providers
  - kept every profile policy manual-only with auto-download disabled
- Next steps:
  1. Continue into candidate ranking/explanation surfaces from the start of the feature-expansion list.
  2. Keep backend execution changes explicit, rate-limited, and review-first.

## Update 2026-04-30 19:50:33Z

- Current task: Feature-expansion burn-down is continuing from the top of the list.
- Last activity:
  - added reusable browser-side ranking for returned search candidates
  - wired Search detail smart ranking to acquisition-profile-aware candidate scores
  - surfaced score reasons in result-card tooltips without adding new network activity
- Next steps:
  1. Continue from the beginning into richer candidate actions, such as shortlist/review flows or album-picker integration.
  2. Avoid Import Staging, Discovery Inbox, Transfers, and MilkDrop files while parallel agents have active edits there.

## Update 2026-04-30 19:58:42Z

- Current task: Feature-expansion burn-down is continuing from the top of the list.
- Last activity:
  - added local album candidate grouping for already-returned search results
  - surfaced Album candidates in Search detail with score, completeness, source count, evidence labels, and representative paths
  - kept the only action as a local result-filter focus with no peer or metadata activity
- Next steps:
  1. Continue from the beginning into search-result shortlist/review actions or the next album-picker refinement.
  2. Avoid Import Staging, Discovery Inbox, MusicBrainz radar, Transfers, Footer, and MilkDrop files while parallel agents have active edits there.

## Update 2026-04-30 20:08:00Z

- Current task: Documentation overstatement cleanup is complete locally.
- Last activity:
  - replaced blanket production-ready language in the README with per-feature maturity guidance
  - downgraded DHT, security hardening, multi-source, MusicBrainz, and Service Fabric status wording where appropriate
  - marked stale January status/test reports as historical snapshots
  - changed universal SSRF, guarantee, and prevention wording to scoped guardrail wording
  - recorded the findings in `docs/dev/documentation-audit-2026-04-30.md`
- Next steps:
  1. Review the documentation diff for release-copy tone.
  2. Continue feature-expansion work only after deciding whether these documentation changes should be committed separately.

## Update 2026-04-30 20:02:58Z

- Current task: Feature-expansion burn-down is continuing from the top of the list.
- Last activity:
  - added selected-file download action previews to Search results
  - exposed source, providers, file count, total size, candidate score, warnings, selected paths, and copyable text export
  - kept previews local and non-mutating with no network or transfer calls
- Next steps:
  1. Continue from the beginning into stricter/preferred file condition controls or result deduplication.
  2. Avoid Import Staging, Discovery Inbox, MusicBrainz radar, Transfers, Footer, broad docs, and MilkDrop files while parallel agents have active edits there.

## Update 2026-04-30 20:09:42Z

- Current task: Feature-expansion burn-down is continuing from the top of the list.
- Last activity:
  - added ranking-only preferred conditions for extensions, lossless files, and minimum bitrate
  - wired those preferences into Search candidate scores and reasons
  - surfaced preference controls in Advanced Search Filters without turning them into hard filters
- Next steps:
  1. Continue from the beginning into search result deduplication or richer album-picker warnings.
  2. Avoid Import Staging, Discovery Inbox, MusicBrainz radar, source-feed imports, diagnostic bundle, mesh policy, broad docs, and MilkDrop files while parallel agents have active edits there.

## Update 2026-04-30 20:41:14Z

- Current task: Source-feed import provider expansion is complete locally.
- Last activity:
  - added optional YouTube Data API playlist expansion for source-feed imports
  - added optional Last.fm loved/recent/top track imports
  - documented the new `integrations.youtube.api_key` and `integrations.lastfm.api_key` options
- Next steps:
  1. Review and merge the source-feed provider import changes.
  2. Keep any further private-provider imports behind explicit account connection/OAuth flows.

## Update 2026-04-30 20:53:19Z

- Current task: Source-feed provider Web UI settings are complete locally.
- Last activity:
  - added runtime overlay support for source-feed integration settings
  - added YouTube and Last.fm enabled toggles alongside their API keys
  - added System Integrations controls for Spotify, YouTube, and Last.fm settings with masked secrets, warnings, and tooltip-backed runtime apply, YAML save, and reset actions
- Next steps:
  1. Review and merge the source-feed provider settings changes.
  2. Keep private-library provider imports behind explicit OAuth/account connection work.

## Update 2026-04-30 21:06:41Z

- Current task: Web UI surface audit and first Messages redesign slice are implemented locally.
- Last activity:
  - added `docs/dev/webui-surface-audit-2026-04-30.md` with admin/general UI gaps and recommended follow-up order
  - added a unified Messages workspace for chats and rooms
  - kept `/chat` and `/rooms` as compatibility entry points into the new workspace
  - added multi-panel message windows, compact workspace layout, collapse-all, per-panel collapse, close, and restore dock affordances
- Next steps:
  1. Validate focused Messages/App tests, frontend lint/build, whitespace, and relevant backend gates.
  2. Start the next admin settings surface with Notifications or Transfer Policy.

## Update 2026-04-30 21:20:39Z

- Current task: Web UI notification provider settings are implemented locally.
- Last activity:
  - added System Integrations controls for Pushbullet, Ntfy, and Pushover
  - exposed enable toggles, private-message and room-mention trigger toggles, masked secret replacement, validation warnings, runtime apply, YAML save, and reset actions
  - extended `OptionsOverlay` so notification provider settings can be applied at runtime when remote configuration is enabled
  - updated `config/slskd.example.yml`, changelog, the Web UI audit, task list, and progress notes
- Next steps:
  1. Continue the audit burn-down into webhooks/scripts/FTP or Transfer Policy.
  2. Keep additional admin settings surfaces redacted, tooltip-backed, and explicit about runtime versus YAML persistence.

## Update 2026-04-30 21:27:05Z

- Current task: Web UI FTP integration settings are implemented locally.
- Last activity:
  - added System Integrations controls for FTP completed-download uploads
  - exposed enablement, address, port, username/password replacement, remote path, encryption mode, certificate handling, overwrite policy, timeout, retry attempts, runtime apply, YAML save, and reset actions
  - extended `OptionsOverlay` so FTP integration settings can be applied at runtime when remote configuration is enabled
  - updated changelog, the Web UI audit, task list, and progress notes
- Next steps:
  1. Continue the audit burn-down into webhooks/scripts or Transfer Policy.
  2. Keep runtime-editable surfaces paired with YAML persistence and clear validation warnings.

## Update 2026-04-30 22:04:33Z

- Current task: Subpath Web UI release-regression hardening is complete locally.
- Last activity:
  - changed the Vite build to emit relative asset references
  - tightened build-output verification against root-relative `/assets/...` regressions
  - expanded the subpath smoke script to serve `/slskd/system/info`, inject a mounted base href, and fetch JS/CSS assets from the mounted path
  - updated backend HTML rewrite rules and focused unit coverage for non-root `web.url_base` base-tag injection
  - closed the tracked frontend Vite/Vitest peer-alignment follow-up
- Next steps:
  1. Continue burning down the expansion list with another self-contained phase that avoids the concurrent MusicBrainz radar, Playlist Intake, and MilkDrop/native visualizer lanes.
  2. Prefer changes with deterministic smoke coverage so future release regressions fail before tagging.

## Update 2026-04-30 22:08:00Z

- Current task: Mobile setup-health diagnostics are complete locally.
- Last activity:
  - added a browser-local setup health evaluator and copyable report formatter
  - added System Info setup-health modal cards for connection, identity, shares, downloads, restart, URL base, and remote-configuration readiness
  - styled the modal for narrow screens and dark mode while keeping actions tooltip-backed
  - kept the feature observational with no peer contact, credential validation, scans, config writes, downloads, or file mutation
- Next steps:
  1. Continue tail-side feature-expansion work into more E23 mobile workflows or E22/E21 evidence surfaces, avoiding concurrent MusicBrainz radar, Playlist Intake, and MilkDrop/native visualizer files.
  2. Add deterministic focused tests for each slice before moving on.

## Update 2026-04-30 22:55:36Z

- Current task: App-facing slskd branding rename is implemented locally.
- Last activity:
  - audited remaining `slskd` references and avoided blanket replacement
  - updated visible Web UI labels/tooltips/errors to use `slskdN`
  - updated notification default prefixes, config examples, docs, and related tests
  - intentionally left compatibility-sensitive identifiers and upstream attribution unchanged
- Next steps:
  1. Run focused frontend tests and diff/grep validation.
  2. Keep future branding edits scoped to user-visible slskdN-owned copy unless compatibility migration is explicitly planned.

## Update 2026-05-01 00:11:29Z

- Current task: Tail-side E23/E22 feature-expansion slice is complete locally.
- Last activity:
  - added a Discovery Inbox mobile review tray with previous/next navigation and local approve, snooze, and reject actions
  - added per-card handoff into the mobile review tray
  - added browser-local community quality trust/caution/ignore overrides with reviewer notes
  - wired quality overrides into candidate ranking reasons and action-preview warnings while retaining the underlying evidence
  - avoided concurrent Import Staging, native MilkDrop, SubjectIndex, and Playlist Intake dirty lanes
- Next steps:
  1. Continue tail-side work into more E23 mobile review surfaces or E21/E22 evidence controls that do not overlap active dirty files.
  2. Keep each slice local/review-first unless backend confirmation, rate limit, and mutation contracts already exist.

## Update 2026-05-01 00:15:13Z

- Current task: Tail-side E21 mesh evidence review sandbox is complete locally.
- Last activity:
  - added mesh evidence confidence and witness/k-anonymity review thresholds
  - added local evidence parsing/evaluation/report formatting for provenance, trust tier, privacy blockers, confidence, and witnesses
  - added a System -> Mesh Evidence Review Sandbox with sample loading, local review, accepted/rejected results, and copyable report
  - kept the feature review-only with no peer query, backend submit, publication, ranking mutation, or file/network action
- Next steps:
  1. Continue tail-side work into E20 diagnostics or remaining E23 mobile workflows that avoid current dirty Import Staging, Playlist Intake, SubjectIndex, and native MilkDrop lanes.
  2. Keep mesh evidence publication disabled by default and require explicit backend contracts before any outbound action exists.

## Update 2026-05-01 00:21:03Z

- Current task: Tail-side E20 diagnostic wizard expansion is complete locally.
- Last activity:
  - expanded setup health with readiness scoring, grouped checks, group filters, and top next steps
  - added checks for API access, provider credential gaps, queue pressure, failed jobs, and automation visibility
  - embedded setup-health readiness into the redacted diagnostic bundle
  - kept diagnostics browser-local and observational with no peer contact, credential validation, scans, retries, config writes, or server calls for bundle generation
- Next steps:
  1. Continue tail-side work into remaining E20 operational visibility or E23 mobile workflows that avoid current dirty Import Staging, Playlist Intake, SubjectIndex, audio verification, and native MilkDrop lanes.
  2. Prefer review/report surfaces until backend mutation and rate-limit contracts are explicit.

## Update 2026-05-01 00:26:06Z

- Current task: Tail-side E20 Network Health visibility slice is complete locally.
- Last activity:
  - added browser-local DHT, mesh, discovered-peer, HashDb, backfill, swarm, and mesh security-signal scoring
  - added a System -> Network health panel with score, status, findings, action guidance, and a tooltip-backed copy report action
  - kept the panel observational, using already-loaded local state only with no peer contact, discovery start, publication, search, browse, download, or file mutation
- Next steps:
  1. Continue burning down the tail-side expansion list in larger phases while avoiding active dirty Import Staging, Playlist Intake, SubjectIndex, audio verification, and native MilkDrop lanes.
  2. Prefer operational review/reporting surfaces until backend mutation, rate-limit, and trust contracts are explicit.

## Update 2026-05-01 00:32:18Z

- Current task: Tail-side E19 media-server sync review planning is complete locally.
- Last activity:
  - added browser-local Plex, Jellyfin/Emby, and Navidrome sync readiness previews
  - added explicit adapter review actions, base URL/token/path-map checks, and copyable media-server sync reports in System Integrations
  - kept the planner non-mutating with no media-server call, scan trigger, playlist sync, play-history import, rating write, Soulseek search, peer browse, download, or file move
- Next steps:
  1. Continue burning down tail-side E18/E17/E16 follow-ups in larger self-contained phases where they avoid active dirty Import Staging, Playlist Intake, SubjectIndex, audio verification, and native MilkDrop lanes.
  2. Keep live external integrations behind explicit configuration, rate limits, and visible operator confirmation.

## Update 2026-05-01 00:38:24Z

- Current task: Front-of-list listening/radar/federated taste UI expansion batch is complete locally.
- Last activity:
  - added Listening Stats recommendation seed handoffs into Discovery Inbox with mesh-preferred acquisition review metadata
  - added Search-page Artist Release Radar controls for subscriptions, notification review, Discovery Inbox handoff, and explicit route attempts
  - added Search-page Federated Taste recommendation review with source reveal controls, Discovery Inbox, Wishlist, Release Radar, and graph preview handoffs
  - kept the batch review-first with no automatic search, peer browse, download, scrobble, media-server call, graph mutation, or file mutation
- Next steps:
  1. Continue from the next unchecked front-of-list feature-expansion item that avoids the active middle/end dirty lanes.
  2. Implement live listening/media-server execution only after backend credential, rate-limit, dedupe, and confirmation contracts are explicit and visible in the Web UI.

## Update 2026-05-01 00:53:49Z

- Current task: Front-of-list media-server contract and realm subject-index UI batch is complete locally.
- Last activity:
  - added visible live media-server execution contracts in System Integrations for play-history import, scrobble/rating export, acquisition queue handoff, completed-file scan, and confirmed file actions
  - exposed adapter readiness, user mapping, confirmation, rate limit, dedupe, blockers, and copyable contract reporting without calling external media servers or mutating files
  - added System -> Mesh realm subject-index conflict review with provenance, local authority disable/re-enable state, and report export
  - verified the stale broad `dotnet test -v minimal` hang no longer reproduces in this workspace
- Next steps:
  1. Continue front-of-list burn-down after the active visualizer lane, or take the next bug only if it avoids the DHT/share-scan transfer lanes other agents may be editing.
  2. Keep live media-server execution disabled until a backend adapter explicitly consumes the visible contract and its rate-limit/confirmation gates.

## Update 2026-05-01 00:39:04Z

- Current task: Tail-side E18/E17/E16 review/reporting batch is complete locally.
- Last activity:
  - added Servarr compatibility review reports for wanted-pull and completed-import readiness
  - added Wishlist request review packets for quota, state, enabled/manual/automatic posture, and Discovery Inbox decision review
  - added Automation Center history reports for enabled recipes and dry-run checkpoints
  - kept the batch non-mutating with no Lidarr call, download-client creation, wanted pull, import trigger, automation execution, Soulseek search, peer browse, download, or file move
- Next steps:
  1. Continue tail-side burn-down into E15/E14 follow-ups or another self-contained E18/E17/E16 phase that avoids active dirty Import Staging, Playlist Intake, SubjectIndex, audio verification, native MilkDrop, and front-of-list lanes.
  2. Keep any live external integration or automation execution behind explicit configuration, rate limits, and visible operator confirmation.

## Update 2026-05-01 00:43:07Z

- Current task: Tail-side Servarr/Wishlist live execution follow-up is complete locally.
- Last activity:
  - added Servarr compatibility Run Ready to call the configured Lidarr wanted-sync endpoint when wanted pull is ready
  - added Wishlist Run Enabled to run up to three enabled backend Wishlist searches from the request portal summary
  - kept both live actions explicit and bounded with no auto result selection, direct peer browse, download, completed import trigger, automation recipe execution, or policy bypass
- Next steps:
  1. Continue tail-side burn-down into E15/E14 live follow-ups or another self-contained E18/E17/E16 execution phase where backend APIs already exist.
  2. Do not fake live automation/media-server features client-side; wire only real APIs with visible confirmation and limits.

## Update 2026-05-01 00:49:40Z

- Current task: Port migration banner correction is complete and deployed to `kspls0`.
- Last activity:
  - replaced raw VPN/endpoint copy with a slskdN ingress-port reduction reminder
  - rendered each current mapping as service purpose, protocol/public endpoint, local destination, and related config option
  - bumped the dismissal storage key so the corrected reminder appears even if the previous unclear banner was dismissed
  - documented the gotcha in ADR-0001 and committed that doc-only entry as `d3c2dd923`
- Next steps:
  1. Add a real backend reachability probe before showing open/closed status in this banner; do not infer public reachability from VPN assignment alone.
  2. Hard-refresh `kspls0:5030` if a browser still has the older service-worker asset cached.

## Update 2026-05-01 00:55:00Z

- Current task: Pods channel chat visibility fix is complete and deployed to `kspls0`.
- Last activity:
  - clarified that `dm` is a pod channel label, not a separate unread user-message inbox
  - wired pod channel tab selection to `activeChannelId`, URL navigation, and message refresh
  - added a visible channel chat panel with history, message count, and composer below Listen Along
  - documented the gotcha in ADR-0001 and committed that doc-only entry as `9d0d66b2d`
- Next steps:
  1. Consider renaming generated/default direct pod channels from terse `dm` to `DM` or `Direct` at creation/display time.
  2. Add focused Pods component tests when this area gets another behavior change; current validation was lint/build plus live asset deployment.

## Update 2026-05-01 01:05:17Z

- Current task: Unified Messages/Pods messaging surface is complete and deployed to `kspls0`.
- Last activity:
  - added Pod Channels to the Messages sidebar
  - added pod-channel message panels with Listen Along, message history, composer, and send action
  - removed the top-level Pods nav item and routed `/pods` into Messages pod-channel mode
  - added focused Messaging test coverage for opening a pod channel in the unified workspace
- Next steps:
  1. If pod administration is still needed, expose it as a secondary panel inside Messages/System instead of a top-level Pods tab.
  2. Consider renaming `dm` display labels to `DM` or `Direct` for clarity.

## Update 2026-05-01 00:51:11Z

- Current task: Tail-side Automation Center live execution follow-up is complete locally.
- Last activity:
  - added executable Automation Center actions for Wishlist Retry and Library Health Scan
  - Wishlist Retry runs up to three enabled backend Wishlist searches and records execution history
  - Library Health Scan requires an explicit operator-entered path and starts the real read-only scan
  - unsupported recipes remain visible but disabled for execution instead of simulating backend work
- Next steps:
  1. Continue tail-side burn-down into the next feature-expansion phase with real backend actions where APIs already exist.
  2. Keep media-server and remaining automation execution gated behind explicit credentials/configuration, limits, and operator confirmation before wiring live calls.

## Update 2026-05-01 00:59:01Z

- Current task: Tail-side Library Health and Discovery Shelf handoff phase is complete locally.
- Last activity:
  - added selected Library Health replacement searches through the real Search batch API, capped at three deduped queries
  - constrained selected remediation jobs to auto-fixable issue IDs only
  - added Library Health quarantine-review handoff into Discovery Inbox
  - added Discovery Shelf promote-preview handoffs into Discovery Inbox at row and batch levels
- Next steps:
  1. Continue tail-side burn-down into the next E14/E13/E12 live handoff with existing backend APIs only.
  2. Leave destructive archive/expiry/quarantine/file actions review-gated until backend preview/confirmation contracts exist.

## Update 2026-05-01 01:04:11Z

- Current task: Tail-side Listening Stats live handoff phase is complete locally.
- Last activity:
  - added top-track recommendation seeds to listening intelligence
  - added bounded live Search API execution from listening seeds
  - added enabled manual Wishlist creation from listening seeds with auto-download disabled
  - added ListenBrainz recent-history submission for up to ten browser-local plays when a token is saved
- Next steps:
  1. Continue tail-side burn-down into the next phase that can use existing APIs without implicit background work.
  2. Keep direct downloads, peer browse, media-server calls, and destructive file actions behind explicit preview/confirmation contracts.

## Update 2026-05-01 01:09:07Z

- Current task: Tail-side E15 Smart Radio and queue handoff phase is complete locally.
- Last activity:
  - added Smart Radio batch search, Wishlist, and Discovery Inbox actions
  - added Playback Queue similar-candidate search and Wishlist actions
  - kept these actions bounded and explicit, with Wishlist auto-download disabled
  - preserved local queue behavior while adding acquisition handoffs
- Next steps:
  1. Continue tail-side burn-down into remaining E15/E14 gaps or move earlier only where existing APIs can be wired directly.
  2. Keep direct download/browse/file actions out until the UI has explicit preview and confirmation contracts.

## Update 2026-05-01 01:19:43Z

- Current task: Discovery Inbox acquisition-plan Wishlist handoff phase is complete locally.
- Last activity:
  - added a `Wishlist Ready` action beside `Execute Ready` in Acquisition Plans
  - creates bounded manual Wishlist requests with auto-download disabled
  - records the Wishlist request id on the plan and displays it in the plan card
  - skips plans that already have a Wishlist request so repeated clicks do not duplicate entries
- Next steps:
  1. Continue tail-side burn-down into adjacent acquisition/import phases, but avoid Import Staging files while another lane has active dirty edits.
  2. Keep direct download, peer browse, and file mutation behind explicit preview and confirmation contracts.

## Update 2026-05-01 01:24:58Z

- Current task: E11 Playlist Intake tag/organization dry-run phase is complete locally.
- Last activity:
  - added organization template, album title, multi-artist, cover-art, and ReplayGain controls to Playlist Intake
  - generated persisted dry-run plans with changed fields, destination paths, skipped rows, and explicit no-write impact
  - kept this phase review-only because the safe tag-write/file-move backend contract is not wired here
- Next steps:
  1. Continue into remaining E11/E10/E9 work only where it can avoid active dirty Import Staging/audio-verification lanes or where those lanes have settled.
  2. Do not add tag writes, file moves, cover writes, or ReplayGain execution until there is an explicit backend preview/confirmation and rollback contract.

## Update 2026-05-01 03:08:11Z

- Current task: Remaining middle/backlog completion pass is complete locally.
- Last activity:
  - reconciled completed QR invite display/scanning, DHT adaptive bootstrap diagnostics, native MilkDrop3 T-938, SongID parity notes, analyzer cleanup, and production placeholder burn-down tasks
  - confirmed the old dev package channel was stale; the later cleanup removed it from active release automation
  - replaced production placeholder/TODO wording with explicit capability-gate or unavailable-path language and updated the placeholder completion plan
  - verified focused Contacts QR tests, focused DHT tests, touched Contacts ESLint, and `dotnet format --verify-no-changes --no-restore --verbosity minimal`
- Next steps:
  1. Continue normal validation before merge because concurrent agents have been committing adjacent player and docs work.

## Update 2026-05-01 02:52:32Z

- Current task: MilkDrop/Butterchurn player tile deployment to `kspls0` is complete.
- Last activity:
  - moved the small tile mode/fullscreen controls out of the visualizer canvas area in the current frontend bundle
  - kept explicit player tile modes for spectrum, signal scope, Butterchurn, MilkDrop3 WebGL2, and MilkDrop3 WebGPU
  - synced the rebuilt `wwwroot` to `/usr/lib/slskd/current/wwwroot` on `kspls0` and restarted the live `slskd` service
  - verified `http://kspls0:5030` serves `index-CaQ2HqYt.js` and `index-6lV9-732.css`
  - fixed the bad restart state where the process briefly ran without `/etc/slskd/slskd.yml` and only listened on `127.0.0.1:5030`; verified it now listens on `0.0.0.0:5030`
- Next steps:
  1. If the browser still shows the old visualizer tile, hard-refresh once or unregister the old service worker; the server endpoint is now serving the new bundle.
  2. Continue debugging any runtime browser-console errors from Butterchurn or native MilkDrop after confirming the new bundle is loaded.

## Update 2026-05-01 03:02:00Z

- Current task: Player visual tile one-row controls and native visualizer retry fix are complete and deployed to `kspls0`.
- Last activity:
  - changed the visual tile buttons from wrapping flex controls to a fixed seven-column icon row
  - remount the compact visualizer when a tile visualizer mode button is clicked, including same-mode retry clicks
  - key the visualizer canvas by engine type so WebGPU/WebGL2 context changes get a fresh canvas
  - dispose partially initialized native engines when setup fails
  - verified `kspls0:5030` serves `index-BGGUa318.js` and `index-DwvOKuMB.css`
- Next steps:
  1. WebGPU on `http://kspls0:5030` may still be browser-blocked if the browser requires a secure context for `navigator.gpu`; WebGL2 should now be retryable after switching away and back.
  2. If WebGL2 still fails, capture the browser console error from the compact tile after loading `index-BGGUa318.js`.

## Update 2026-05-01 09:18:00Z

- Current task: PPA upload retry investigation is in progress.
- Last activity:
  - confirmed the `.215` PPA source package built and signed correctly before upload
  - confirmed historical `.197` PPA success used anonymous FTP with passive mode, not SFTP
  - reverted the incorrect SFTP/Launchpad SSH secret change
  - fixed standalone PPA web asset staging after the manual retry failed on a missing `publish-linux-x64/wwwroot`
- Next steps:
  1. Retry the original tag workflow's failed PPA job with the known-good FTP configuration.
  2. If FTP still fails, inspect whether this is a transient GitHub/Launchpad network issue or a changed runner/network policy.
