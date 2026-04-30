# Changelog

All notable changes to slskdN are documented here. GitHub release pages use
[`scripts/generate-release-notes.sh`](../scripts/generate-release-notes.sh),
which prefers the matching version section below and otherwise falls back to
the commit delta since the previous release tag. Tagged releases must never
publish the rolling `## [Unreleased]` section.

Feature and fix work belongs in `## [Unreleased]` when the commit lands. When a
release is cut, move only the shipped bullets into the new versioned section so
each release note reflects the delta from the previous release.

Use headings in this form:

```markdown
## [<version>] — YYYY-MM-DD
```

For dev or build tags, use the same logical version string embedded in the tag.

---

## [Unreleased]

- Refined browser player controls and equalizer storage behavior during the
  native MilkDrop player integration work.
- Hardened browser player visualizer storage access so blocked localStorage
  contexts fall back cleanly instead of crashing player initialization.
- Routed additional browser-local search and Discovery Graph storage reads
  through safe storage helpers for privacy-locked browser contexts.
- Routed the Network DHT exposure acknowledgement through safe browser storage
  helpers.
- Prepared the `2026042900-slskdn.204` stable release metadata.
- Made Winget release-version metadata validation opt-in so stable releases
  that intentionally skip Winget are not blocked by stale Winget URLs.
- Added the WebGL MilkDrop3 port design, making the external visualizer
  launcher an interim bridge while the long-term target becomes a
  browser-native MilkDrop3-compatible renderer inside slskdN.
- Introduced the browser visualizer engine adapter boundary used by the
  current Butterchurn implementation.
- Rejected incoming Soulseek upload requests from the daemon's own username so
  self-originated requests do not appear as uploads to yourself.
- Fixed auto-replace so missing/legacy state follows the opt-in
  `AutoReplaceStuck` setting instead of defaulting enabled, and so replacement
  candidates exclude the daemon's own Soulseek username.
- Added the browser-native MilkDrop visualizer implementation work, including
  preset parsing/expression tests, renderer smoke coverage, local preset import,
  player integration controls, selected-preset removal, and browser-local preset
  library cleanup.
- Added native MilkDrop shader-subset translation coverage for supported
  `warp_shader` and `comp_shader` return expressions.
- Added native MilkDrop textured-shape rendering, multi-fixture browser smoke
  coverage, and browser-local image texture asset imports with skipped-asset
  reporting plus path/basename/stem lookup for preset texture references.
- Fixed native `.milk2` import inspection so every preserved preset body is
  compatibility-checked before the file is accepted.
- Added the first native `.milk2` double-preset composite path, rendering the
  primary preset body normally and blending compatible secondary bodies over it.
- Added first native MilkDrop sprite/image primitive parsing and textured-quad
  rendering backed by imported image texture assets.
- Scoped imported native MilkDrop image assets per preset so multi-preset packs
  do not persist unrelated images with every imported preset.
- Added a native MilkDrop preset-folder import affordance for browsers that
  expose directory-relative file paths.
- Added first native MilkDrop renderer-set crossfades for preset/import changes
  and `.milk2` secondary composite-alpha controls.
- Added native MilkDrop `.shape` and `.wave` fragment import/export affordances
  with active-preset merge and browser-local persistence.
- Added native MilkDrop beat and timed automatic preset change modes with
  browser-local mode persistence.
- Added native MilkDrop local preset-bank controls for favorites, favorites-only
  filtering, previous-preset history, next-library cycling, and random jumps.
- Added native MilkDrop preset-bank search that persists locally and scopes
  imported-preset next/random navigation to the filtered result set.
- Added browser-local native MilkDrop preset playlists, including save-from-filter,
  select, clear-active, delete, and playlist-scoped next/random navigation.
- Added renderer-wide native MilkDrop q1-q64 initialization and q-register
  propagation across global, custom wave, shape, and sprite evaluation stages.
- Added first native MilkDrop shader uniform support for q1-q64 and
  bass/mid/treble audio variables in supported warp/comp shader expressions.
- Added shader-side native MilkDrop `get_fft()` and `get_fft_hz()` support for
  translated warp/comp shaders using a compact FFT uniform array.
- Added native MilkDrop primitive-field aliases so custom waves, shapes, and
  sprites honor common preset names such as `nSamples`, `bSpectrum`,
  `bUseDots`, `bDrawThick`, `bAdditive`, `bTextured`, and `texName`.
- Added first native MilkDrop screen-border rendering for classic `ob_*` and
  `ib_*` preset values.
- Added first classic native MilkDrop waveform modes with `wave_mode`,
  `wave_x`, `wave_y`, `wave_a`, `wave_scale`, and `wave_smoothing` support.
- Expanded the native MilkDrop shader translator to accept safe straight-line
  temp declarations plus common HLSL helper aliases including `frac`, `fmod`,
  `rsqrt`, and `atan2`.
- Added translated native MilkDrop shader viewport context with `resolution`,
  `pixelSize`, `aspect`, `texsize`, and generated `x/y/rad/ang` coordinates.
- Added native MilkDrop shader-body wrapper support so safe `shader_body { ... }`
  warp/comp shader blocks are translated instead of rejected.
- Added first translated native MilkDrop shader named-texture sampler support,
  binding up to four preset texture samplers with procedural fallback.
- Added first safe translated native MilkDrop shader conditional support for
  simple `if (...) ret = ...; else ret = ...;` bodies.
- Added safe translated native MilkDrop shader temp reassignment support for
  declared local variables.
- Added native MilkDrop compatibility matrix reporting for curated fixtures and
  local `.milk` / `.milk2` files or folders, including high-count wave/shape
  metrics for real preset-pack pressure.

## [2026042900-slskdn.204] — 2026-04-30

This supersedes `2026042900-slskdn.203`, whose tag failed the release gate
before build/test because optional Winget release-version metadata was treated
as mandatory.

- Added a MilkDrop visualizer (butterchurn) to the Web UI player bar with
  inline-thumbnail, full-browser-window, and native-fullscreen modes. The
  butterchurn engine and preset pack are loaded via dynamic imports so they
  ship as separate chunks and stay off the critical path until the user
  toggles the visualizer on.
- Expanded the Web UI player into a footer-safe drawer with collapse/expand,
  previous/next, rewind, fast-forward, local mute, Media Session handlers, and
  empty-state launchers for Collections plus shared/downloaded local audio.
- Added Winamp-style Web UI player controls: shared Web Audio graph plumbing,
  10-band persisted EQ presets, lightweight spectrum/oscilloscope rendering,
  LRCLIB synced lyrics, ListenBrainz now-playing/scrobble submission, optional
  crossfade, Document Picture-in-Picture spectrum output, and karaoke-style
  center-channel reduction.
- Replaced the player empty-state collection/file dropdowns with full modal
  browsers: a two-pane collection picker with playable collection items, and a
  searchable shared/downloaded local-audio table with explicit play actions.
- Documented the integrated player, modal pickers, local-root streaming,
  player extras, listening-party behavior, and PWA/mobile playback in the
  README and feature guides.
- Made the stream locator resolve `sha256:` and path-based local audio IDs from
  configured share/download roots when the file is allowed locally but has not
  yet been persisted into the indexed `content_items` table.
- Added stream ticket plumbing for browser playback flows and tightened related
  pod/listening-party controller behavior so the expanded player, local file
  browser, and radio/listen-along paths share the same integrated slskdN
  streaming boundary.
- Improved player visualizer fallback styling so hidden canvases and fallback
  surfaces render cleanly when MilkDrop cannot draw.
- Added an opt-in authenticated external visualizer launcher API and config
  surface so local deployments can start a configured helper such as MilkDrop3
  without allowing arbitrary browser-supplied commands.
- Added the WebGL MilkDrop3 port design and a visualizer engine boundary that
  keeps Butterchurn behind an adapter while the browser-native MilkDrop3 path
  is built incrementally.
- Added Discography Concierge planning and first implementation pieces,
  including MusicBrainz artist coverage services/API/UI, manual missing-track
  Wishlist promotion, and supporting docs/tasks for mesh/social music
  discovery work.
- Fixed Gold Star Club revocation handling to avoid nullable service access
  and ambiguous filesystem type resolution.
- Fixed mixed-source accelerated downloads so the Soulseek sequential-failover
  loop filters out mesh-overlay sources before calling `ISoulseekClient`.
  Mesh sources now stay on the mesh-aware path, and raw Soulseek failover only
  dials raw Soulseek peers.
- Added Layer 1 listening parties: a persistent Web UI player streams
  `ContentId` values through the existing range endpoint, updates Now Playing
  from browser playback, and publishes pod-scoped listen-along metadata over
  stored/routed pod messages plus SignalR fan-out without relaying audio bytes.
- Added local browser mute and mobile/PWA safe-area handling to the Web UI
  player so listen-along streams can keep playing while muted on one device.
- Added an opt-in slskdN radio directory for listed listening parties, with a
  mesh/DHT-backed announcement index and an integrated host radio stream
  endpoint gated by a separate mesh-streaming toggle.
- Fixed listening-party startup by keeping the live registry singleton while
  resolving scoped PodCore message storage per publish operation.
- Fixed Vite dev Web UI startup so browser API calls use the same-origin proxy
  by default instead of bypassing it with CORS-blocked absolute daemon URLs.
- Fixed Gold Star Club startup so its reserved pod id and default channel id
  conform to PodCore validation instead of crashing the host.
- Clarified Gold Star Club leaving as irrevocable in the Web UI and docs, with
  a confirmation prompt before permanent revocation.
- Reworked pod, room, chat, and contact social affordances so saved
  conversations and joined rooms rehydrate from server state, pods can be
  created or discovered from the main Pods page, and discovered pods can be
  saved locally for daemon-backed retrieval after restarts.
- Added a direct save action for discovered pods so remote discovery results
  can be promoted into the local daemon-backed pod list instead of remaining
  view-only search results in the Pods sidebar.
- Hardened security boundaries by requiring authentication for ActivityPub
  outbox publishing, adding SSRF and size guards to HTTP share backfill,
  fixing file-list path prefix authorization, removing query-string API-key
  CSRF exemptions, and avoiding a secondary service-provider build during
  SignalR API-key promotion.
- Fixed AUR upgrade hooks so an already-running `slskd.service` is restarted
  after package upgrades, while fresh installs and stopped services remain
  untouched until the operator starts them.
- Fixed stable Winget publishing so main release workflows fail loudly when
  `WINGETCREATE_GITHUB_TOKEN` is missing instead of reporting a fake-green
  skipped submission, and added a manual Winget publish workflow for retrying
  an existing release tag after credentials are configured. The stable Winget
  jobs now submit generated manifests directly so the first `snapetech.slskdn`
  PR can be opened before the package exists in `microsoft/winget-pkgs`, and
  stage manifests in the repository-shaped path expected by WingetCreate. The
  stable locale description now emits valid YAML block indentation, and the
  zip portable metadata now follows accepted winget-pkgs layout. Stable Winget
  workflow staging now uses the same numeric dotted package version emitted in
  the manifests, and the initial submission path uses the generated multi-file
  version, installer, and default locale manifests instead of a temporary
  singleton manifest rejected by Winget service validation.
- Changed stable Winget publication to an optional high-value release step:
  main release tags still regenerate checked-in Winget metadata, but public
  `microsoft/winget-pkgs` PR submission now happens only through the manual
  `Publish Winget` workflow.

## [2026042900-slskdn.202] — 2026-04-30

- Reworked the fixed Web UI header and footer chrome so primary navigation,
  utility actions, brand, speeds, network counters, and transport icons align
  as distinct rails instead of crowding together in live screenshots.
- Polished transfer rows with peer browse links, throttled queued-position
  refreshes, and batch-aware delete cleanup for completed batch downloads.
- Limited automatic queue-position checks on the Downloads page to a small,
  cached refresh batch instead of asking every queued peer every second.
- Linked transfer user headers to Browse so upload/download rows provide the
  same direct peer affordance as search results.
- Fixed delete-on-remove for successful batch downloads so files stored under
  the batch completion folder are resolved correctly.
- Documented the supported advanced search filter text syntax in the README.
- Fixed stale unit-test compile blockers in the release gate by removing an
  assertion against the retired `MusicBrainz.Enabled` option and disambiguating
  `System.IO.File` / `System.IO.Directory` in tests that import `Soulseek`.
- Fixed manual-review SongID Discovery Graph expansion so weak track candidates
  can remain visible without pulling unrelated album, artist, or segment
  context into the neighborhood.
- Updated the `UserService` disposal regression test to account for the
  fixture-owned regex username matcher options listener while still verifying
  `UserService` removes its own listener and Soulseek event handlers.

## [2026042900-slskdn.199] — 2026-04-29

- Made the Search page secondary panels collapsed by default and persisted
  each panel's open/collapsed state per browser.
- Rebuilt the README showcase UI surfaces with compact desktop navigation,
  a shorter fixed footer, a one-row mobile footer rail, cleaner search result
  cards/file lists, and clearer Discovery Graph controls/sparse-state
  messaging.
- Added a first-class Downloads `Accelerated` toggle and API state endpoint,
  persistent discovery/verification probe budgeting, and related UI/docs
  polish for the guarded multi-source rescue path.
- Redesigned the Web UI footer status dock into clearer brand, speed,
  network, transport-health, and fork-note groups with responsive spacing.
- Fixed the Web UI theme picker so browser clicks open a portal-backed menu
  reliably, with the nav trigger labeled as `Theme`.
- Added upstream-compatible configuration aliases for `transfers`,
  `integrations`, nested upload group limits, username blacklist regex
  patterns, reverse-proxy-safe Web UI icon paths, and retry-delay clamping,
  while preserving slskdN's legacy config compatibility with startup warnings.
- Changed the Network dashboard public-DHT exposure warning into a dismissable
  info notice that is remembered per browser, because public rendezvous is an
  intended slskdN feature state that only needs operator awareness.
- Fixed false Network dashboard no-peer diagnostics by treating
  `/api/v0/dht/status` node, discovered-peer, and active-mesh counters as peer
  evidence when the older mesh/discovered peer list endpoints are empty.

## [2026042900-slskdn.198] — 2026-04-29

- Fixed the stable Chocolatey publish job so repeated transient push failures
  fail the workflow instead of reporting a green release with no package
  published, and added a Chocolatey-only manual publish workflow for retrying
  an existing GitHub release with the stored Chocolatey secret. The retry
  matcher now joins PowerShell command output before checking for `504` /
  timeout responses so transient Chocolatey failures are retried correctly,
  and the nuspec is written with Chocolatey's normalized package version while
  keeping installer URLs pointed at the original GitHub release tag.
- Multi-source / swarm download safety pass:
  - Added a first-class Downloads `Accelerated` toggle and transfer API state
    endpoint. Turning it off suppresses underperformance-triggered rescue;
    turning it on lets queued-too-long, slow, or stalled downloads enter the
    conservative rescue path.
  - Added `VerificationMethod.MeshOverlay` so trusted slskdN mesh peers are no
    longer conflated with size-only Soulseek matches, and tagged rescue-mode
    overlay peers accordingly.
  - Split the download policy by source type: parallel chunked downloads now
    only run when every source is mesh-overlay; Soulseek and mixed source
    sets route through a new sequential-failover path that streams from one
    peer at a time and resumes at the current byte offset on stall, producing
    at most one mid-stream cancellation per failover instead of one per
    chunk per peer.
  - Hard-floored `SelectCanonicalSourcesAsync` so multi-source is declined
    (caller falls back to single-source) unless ≥2 sources share a verified
    content hash or every source is mesh-overlay; the explicit-API endpoints
    return a clear 400 instead of silently degrading.
  - Added a persistent per-peer-per-day verification probe budget and a
    `MeshOverlaySourceCount` request flag that skips Soulseek-side 32 KB
    SHA-256 probes entirely when mesh-overlay sources already cover the
    request, capping the visible "transfer cancelled" noise on any single
    Soulseek uploader.
  - Made discovery hash probes share that same budget so discovery cannot
    create extra probe noise beyond the verification cap.
  - Added Prometheus counters for mid-stream cancellations
    (`slskd_swarm_midstream_cancellations_total`), verification probe
    outcomes (`slskd_swarm_verification_probes_total`), hard-floor fallbacks
    (`slskd_swarm_hard_floor_fallbacks_total`), and sequential-failover
    events (`slskd_swarm_sequential_failover_total`) so the network impact
    of the multi-source path is measurable directly.
  - Rewrote `docs/multipart-downloads.md` and the README multi-source
    section to be explicit about scope (default downloads use the standard
    single-source Soulseek path; acceleration is toggle-gated or explicit)
    and document the trust split, hard floor, and probe budget.

## [2026042900-slskdn.197] — 2026-04-29

- Restored the browser tab title to the short slskdN brand name instead of
  showing the release version and fork attribution.
- Fixed the Web UI theme picker so it opens reliably and applies selected
  themes through Semantic UI's controlled dropdown path.
- Smoothed transfer bulk actions by ignoring stale transfer polls and
  optimistically hiding rows after accepted retry/remove operations.
- Made footer transfer speed totals fall back to bytes-over-elapsed-time for
  active transfers when Soulseek has not populated `AverageSpeed` yet.

## [2026042900-slskdn.196] — 2026-04-29

- Removed the top slskdN status drawer and navigation toggle, and moved its
  DHT, mesh, hash, sequence, swarm, backfill, and karma counters into the
  persistent footer.
- Fixed the slskdN theme picker contrast and dropdown surface styling so the
  selector is visible in the top navigation and the default dark palette has
  clearer separation between the page, panels, inputs, and active controls.
- Upgraded Dependabot PR dependency bumps for NuGet and npm, including patched
  OpenTelemetry `1.15.3` packages and npm `uuid` `14.0.0` to clear the open
  package advisories.
- Aligned test project Microsoft package references with the `10.0.7`
  application package line so dependency submission restores do not hit NuGet
  downgrade errors.
- Fixed the CodeQL cleartext-secret alert by deleting legacy overlay
  certificate password files without reading or logging them and regenerating
  the self-signed overlay certificate when needed.
- Clarified the System Network diagnostics when DHT rendezvous is intentionally
  isolated by `dhtRendezvous.lanOnly: true`.
- Made the two-node DHT rendezvous integration coverage wait for overlay
  readiness before failing a peer-connect attempt.

## [2026042900-slskdn.195] — 2026-04-29

- Fixed the Network dashboard public DHT exposure warning so nodes with
  backend-reported `lanOnly: true` no longer get warned as though they are
  publishing to public DHT bootstrap routers.

## [2026042900-slskdn.194] — 2026-04-29

- Fixed the AUR source package build for date-versioned slskdN releases by
  mapping public versions such as `2026042900-slskdn.193` to MSBuild-safe
  package versions while preserving the public informational version.

## [2026042900-slskdn.193] — 2026-04-29

- Added a slskdN default web theme using brown, gray, and purple tones, kept
  the upstream-style dark theme as `Classic Dark`, and preserved the light
  theme as a selectable option.
- Clarified slskdN fork attribution across docs, web metadata, package
  metadata, generated release copy, service metadata, and API surfaces while
  preserving compatibility names for existing installs.
- Normalized C# source headers against upstream `0.24.5` so unchanged upstream
  files remain upstream-attributed, modified upstream files carry slskdN
  co-attribution, and slskdN-only files use slskdN-only copyright notices.
- Clarified README comparison wording so the upstream baseline is explicitly
  framed as slskd `0.24.5` instead of current upstream `master`.

## [2026042900-slskdn.192] — 2026-04-29

- Fixed Discovery Graph neighborhood building so weak SongID manual-review
  evidence no longer promotes secondary transcript/OCR/chapter/MusicBrainz
  guesses into clickable artist, album, segment, or mix neighborhoods.

## [2026042900-slskdn.191] — 2026-04-29

This supersedes `2026042900-slskdn.190`, which created GitHub release assets and
package metadata but failed Docker publishing because the runtime image assumed
UID/GID `1000:1000` was available in the .NET runtime base image. Docker now
creates the internal `slskdn` placeholder user/group with system-allocated IDs
and still remaps it at container startup when `PUID`/`PGID` are supplied.

It carries forward the `.190` runtime and transfer changes:

- Added slskdN-native Docker runtime handling for `PUID`/`PGID`, non-root
  `--user` runs, writable app-directory validation, and packaging metadata
  checks without creating release tags.
- Added configurable direct-download retry/resume behavior and batch metadata
  for multi-file queue requests, including transfer persistence, API DTO fields,
  migration support, and regression coverage.
- Normalized IPv4-mapped IPv6 addresses before CIDR/proxy/API checks and made
  option diffs tolerate null values.

## [2026042900-slskdn.190] — 2026-04-29

This release follows the corrective `2026042900-slskdn.189` date-versioned
rollback build with runtime and transfer robustness work that landed while the
`.189` Docker publish was still finishing. It keeps the same public
`YYYYMMDDmm-slskdn.###` version shape, remains on the slskd 0.24.5
license-compliance rollback base, and does not imply upstream slskd 0.26 code.

- Added slskdN-native Docker runtime handling for `PUID`/`PGID`, non-root
  `--user` runs, writable app-directory validation, and packaging metadata
  checks without creating release tags.
- Added configurable direct-download retry/resume behavior and batch metadata
  for multi-file queue requests, including transfer persistence, API DTO fields,
  migration support, and regression coverage.
- Normalized IPv4-mapped IPv6 addresses before CIDR/proxy/API checks and made
  option diffs tolerate null values.

## [2026042900-slskdn.189] — 2026-04-29

This is a corrective slskdN-versioned release for package-manager ordering. The
previous rollback build, `0.24.5-slskdn.186`, correctly restored the
license-compliant slskd 0.24.5 codebase, but it sorted older than already
published `0.25.1-slskdn.*` packages in AUR and other downstream repositories.

Starting with this build, stable slskdN releases use the independent
`YYYYMMDDmm-slskdn.###` version shape. This release is newer than the removed
`0.25.1-slskdn.*` line for package managers, but it does not claim upstream
slskd 0.26 or newer code. The application code remains on the slskd 0.24.5
AGPLv3 rollback base with slskdN-owned backports only.

The `0.24.5-slskdn.186` release is superseded by this build for the same
license-compliance reason older releases were purged: users and packagers should
resolve to the current rollback line, not the post-0.25.0 upstream-sync line.
This also supersedes `2026042900-slskdn.187` and `2026042900-slskdn.188`,
which created GitHub releases and package metadata but failed Docker publishing
while the Dockerfile was being corrected to run Bash-only build helpers inside
the Alpine web-build stage.

Included from the rollback line:

- Soulseek.NET client minor version set to the slskdN-owned range `7700000`.
- Runtime YAML alias binding for public keys such as `dht:`.
- Controlled 503 responses for expected remote directory browse timeouts.
- Shutdown-safe download cancellation classification.
- Empty cached user groups now fall back to built-in groups.
- Release-note generation fails closed if synthetic commit lists get too large.
- Tag publishing is no longer blocked by pre-publish Nix smoke checks that need
  already-published assets.

Relevant non-documentation commits preserved in this rollback line:

- `6edafc5d3` feat(wishlist): add CSV import
- `ca51715dd` fix(transfers): require reconnect for listen endpoint changes
- `1fcfbcece` feat(transfers): add upload diagnostics
- `d8df4d15c` fix(dht): use YAML option names in exposure warning
- `00742f9cd` fix(search): publish mesh results before Soulseek timeout
- `7214f310c` fix(aur): normalize release payload permissions
- `33148d54d` test: remove dns-dependent unit flakes
- `248b81981` fix(packaging): harden aur binary zip staging
- `950a87ff3` test(mesh): validate live account mesh smoke
- `f436d48f2` test(mesh): add optional live account smoke
- `fff4367d1` chore(mesh): log mesh search peer outcomes
- `73c9ee89b` fix(mesh): advertise only routable self endpoints
- `9d60cb319` fix(search): honor API timeout seconds
- `7457f4c4d` fix(search): separate auto-replace safety budget
- `5c085b3f0` fix(mesh): quiet issue 209 live maintenance noise
- `db2119ea4` fix: Improve SongID results UX
- `b72258ba4` fix: quiet entropy and auto-replace log noise
- `a17d43868` fix: clean startup identity log polish
- `dc3898c66` fix: classify Soulseek read timeout churn
- `3f901d944` fix: quiet overlay endpoint cooldown noise
- `8a1c89643` ci: remove snap publishing from releases
- `a1f105521` fix: avoid duplicate mesh descriptor publish on startup
- `defa3ee75` fix: quiet expected shutdown and Soulseek timer noise
- `abd55416d` fix: harden package startup and release announcements
- `9c1d3f14d` fix: quiet optional user info badge misses
- `2e4cc934c` fix: discover target framework in test launchers
- `15ba2a423` fix: quiet shutdown-cancelled searches
- `56a25b31d` fix: quiet controlled user info logs
- `393e2cea4` fix: make mesh QUIC opt-in
- `3e65a5778` fix: backport rollback release fixes
- `6b6dcee6e` chore: restore main to 0.24.x rollback line
- `8f597c0f5` chore: switch stable release versioning to slskdN dates

## [0.24.5-slskdn.186] — 2026-04-29

This release is the license-compliance rollback build. It intentionally returns
slskdN to the pre-0.25.0 slskd 0.24.x AGPLv3 codebase and keeps only the
fork-owned slskdN work that can ship on that base. Releases and artifacts older
than this build are being removed from GitHub to prevent accidental installation
of builds made from the post-0.25.0 upstream-sync line.

The build also carries the release-critical backports needed to make the 0.24.x
line usable: Soulseek.NET client minor-version registration for slskdN,
release-note guardrails that fail closed on oversized synthesized commit lists,
runtime YAML alias binding for public keys such as `dht:`, controlled 503
responses for expected remote directory browse timeouts, shutdown-safe download
cancellation classification, fallback handling for empty cached user groups, and
tag publishing unblocked from pre-publish Nix smoke checks that require already
published release assets.

Relevant non-documentation commits preserved in this rollback line:

- `6edafc5d3` feat(wishlist): add CSV import
- `ca51715dd` fix(transfers): require reconnect for listen endpoint changes
- `1fcfbcece` feat(transfers): add upload diagnostics
- `d8df4d15c` fix(dht): use YAML option names in exposure warning
- `00742f9cd` fix(search): publish mesh results before Soulseek timeout
- `7214f310c` fix(aur): normalize release payload permissions
- `33148d54d` test: remove dns-dependent unit flakes
- `248b81981` fix(packaging): harden aur binary zip staging
- `950a87ff3` test(mesh): validate live account mesh smoke
- `f436d48f2` test(mesh): add optional live account smoke
- `fff4367d1` chore(mesh): log mesh search peer outcomes
- `73c9ee89b` fix(mesh): advertise only routable self endpoints
- `9d60cb319` fix(search): honor API timeout seconds
- `7457f4c4d` fix(search): separate auto-replace safety budget
- `5c085b3f0` fix(mesh): quiet issue 209 live maintenance noise
- `db2119ea4` fix: Improve SongID results UX
- `b72258ba4` fix: quiet entropy and auto-replace log noise
- `a17d43868` fix: clean startup identity log polish
- `dc3898c66` fix: classify Soulseek read timeout churn
- `3f901d944` fix: quiet overlay endpoint cooldown noise
- `8a1c89643` ci: remove snap publishing from releases
- `a1f105521` fix: avoid duplicate mesh descriptor publish on startup
- `defa3ee75` fix: quiet expected shutdown and Soulseek timer noise
- `abd55416d` fix: harden package startup and release announcements
- `9c1d3f14d` fix: quiet optional user info badge misses
- `2e4cc934c` fix: discover target framework in test launchers
- `15ba2a423` fix: quiet shutdown-cancelled searches
- `56a25b31d` fix: quiet controlled user info logs
- `393e2cea4` fix: make mesh QUIC opt-in
- `5bd0e0b88` fix: return 503 for unavailable user info
- `c875206b3` fix: preserve spacing in DHT exposure copy
- `f343ca80c` fix: classify Soulseek TCP double-disconnect race
- `c26ed38c7` fix: quiet shutdown disconnect stack noise
- `139af4e8d` fix: reduce background search log noise
- `ed5a7dd9a` fix: quiet auto-replace shutdown cancellation

## [Unreleased]

- **License rollback to slskd 0.24.x base.** slskdN no longer incorporates changes from slskd 0.25.0 or later; the project tracks the pre-0.25.0 plain-AGPLv3 codebase only, and future development is independent of upstream slskd. See `memory-bank/license-rollback-plan.md` for the full rationale and migration plan.
- Backported release-critical fixes onto the 0.24.x rollback branch: release notes now fail closed instead of synthesizing oversized commit dumps, tag builds no longer block publishing on pre-publish Nix smoke checks for unpublished stable assets, public YAML aliases such as `dht:` bind in runtime configuration, remote directory browse timeouts return controlled 503 responses, shutdown-wrapped download cancellations stay out of error logs, and empty cached user groups fall back to built-in groups.
- Changed the Soulseek client minor version from 760 to 7700000 to comply with Soulseek.NET license §5 (unique client-version requirement). The previous value conflicted with the reserved range (760-7699999) allocated to upstream slskd. slskdN claims the adjacent range 7700000-7709999, registered via PR to the Soulseek.NET README registry.
- Removed namespace claims on the upstream `slskd` package name from AUR/RPM/deb packaging metadata (`provides`, `replaces`); slskdN packages now provide their own names only and continue to declare a file-level `conflicts` with `slskd` (both binaries install to `/usr/bin/slskd`). Drop-in compatibility at the binary path is preserved.
- Removed the upstream slskd PNG referenced in the README header for trademark hygiene; an slskdN-original logo is pending.
- Added a NOTICE file at the project root with the slskdN fork attestation.
- Added CSV playlist import for issue `#216`: TuneMyMusic-style exports can now be imported from the Wishlist page into wishlist searches, with optional album terms, filters, enabled state, max results, and auto-download settings.
- Fixed a Soulseek upload reachability bug where runtime changes to `soulseek.listen_port` or `soulseek.listen_ip_address` could restart the local listener without making the Soulseek server advertise the new endpoint; these options now correctly require a reconnect so peers do not keep trying a stale port.
- Added upload diagnostics for troubleshooting remote upload failures: `/api/v0/transfers/uploads/diagnostics` now reports configured listener state, a local TCP listener probe, share/index status, upload counters, recent upload records, and actionable warnings; inbound upload enqueue requests also emit structured `[UPLOAD-DIAG]` logs.
- Fixed the DHT public-discoverability warning and sample config to use the YAML keys operators actually set (`dht.lan_only` / `dht.enabled`) instead of internal option object names.
- Published mesh/pod search results as soon as the mesh overlay responds instead of waiting behind the normal Soulseek search timeout; the search detail view now refetches when early result counts appear.
- Fixed AUR release payload permissions for `slskdn`, `slskdn-bin`, and `slskdn-dev` so `/usr/lib/slskd/releases/<version>` remains traversable by the systemd service user after zip staging.
- Fixed AUR binary package staging for `slskdn-bin` and `slskdn-dev`: the PKGBUILDs now mark the release zips as `noextract`, unpack directly from the downloaded archive during `package()`, and fail the build if the apphost, deps file, or `Microsoft.AspNetCore.Diagnostics.Abstractions.dll` are missing from the staged self-contained .NET 10 payload.
- Added and live-validated an optional live-account mesh smoke that starts two full slskdN instances with configured Soulseek test credentials, hosts a probe file on one node, mesh-searches it from the other, downloads it through the pod path, and byte-verifies the transfer.
- Added info-level mesh-search fanout diagnostics when active overlay peers are queried, including peer count, empty peer responses, failed peers, and returned file count so `meshResponses=0` no longer hides whether the mesh path was actually exercised.
- Fixed mesh self-descriptor endpoint publication so automatic detection only advertises public-routable interfaces and no longer supplements explicitly configured self endpoints with private/container/VPN addresses.
- Fixed documented seconds-to-milliseconds timeout mapping for `/api/v0/searches` and multi-source discovery searches so callers requesting a 10-second or multi-minute search no longer get an accidental 10 ms / 270 ms search window.
- Split background auto-replace searches into an `auto-replace` Soulseek safety-limiter source instead of sharing the user/API `user` bucket, and added source-aware search completion diagnostics for manual issue `#209` retesting without reintroducing routine background log noise.
- Stopped circuit maintenance from automatically running placeholder multi-hop circuit probes against live mesh peers, removing recurring `Circuit building test failed` warnings and avoiding unsolicited peer traffic during normal maintenance.
- Classified common Soulseek remote transfer rejection reasons (`Too many megabytes`, `Too many files`) as expected peer-policy outcomes so they no longer surface as fake fatal unobserved task exceptions.
- Improved the SongID results page after a headless UX audit: queue rows now show meaningful titles/status, duplicate track/action candidates collapse, the result summary promotes the best next actions, and low-level diagnostics move behind disclosure rows.
- Stabilized the release gate by isolating unit tests that inspect process-global static event subscriptions, preventing xUnit parallelism from racing `Clock.EveryMinute`/`Program.LogEmitted` subscriber-count assertions.
- Stabilized live `local test host` log noise found during the 172 package soak: entropy health checks now use a larger RNG sample to avoid routine finite-sample false warnings, and auto-replace no-result searches stay at debug-level telemetry.
- Cleaned up startup polish found during the `local test host` 172 package soak: temporary raw security config probes now log only at debug, persisted peer profiles with blank display names are migrated to a usable fallback, and LAN discovery advertises a non-empty trimmed display name.
- Classified Soulseek.NET read-loop timeout inner exception chains as expected peer-network churn so `ConnectionReadException` plus `IOException`/`SocketException` timeout stacks no longer log as fake fatal unobserved task exceptions.
- Demoted per-endpoint DHT overlay cooldown streak logs to debug so normal remote endpoint churn stays visible through aggregate DHT/overlay summaries and API stats without repeating one line per degraded endpoint at information level.
- Removed Snap publishing from release workflows so dev/stable releases no longer build or upload Snap packages, wait on Snap Store publication, or refresh Snap metadata as part of release metadata commits.
- Avoided duplicate MeshDHT self-descriptor publication at startup by letting the bootstrap service own the initial publish while the refresh service waits until its scheduled interval or an IP-change-triggered refresh.
- Quieted normal host shutdown logs so clean systemd stops/restarts no longer report `app.Run()` returning as abnormal or duplicate expected `ProcessExit` telemetry on stderr.
- Matched live Soulseek.NET timer-reset stack signatures (`Soulseek.Extensions.Reset(Timer timer)`) in the expected network-teardown classifier so known write-loop `NullReferenceException` races no longer log as fake fatal unobserved task exceptions.
- Made release announcement webhooks retry and degrade to warnings so transient Discord/Matrix gateway failures do not mark completed tag builds failed after artifacts and GitHub releases are already published.
- Added a quiet optional user-info mode for UI badges so expected offline/unavailable Soulseek users render as missing badge data without browser console 404/503 noise; the normal `/users/{username}/info` API still preserves its 404/503 semantics.
- Made mesh QUIC explicitly opt-in after recurring native `local test host` coredumps under active MsQuic listeners: UDP overlay remains enabled by default, QUIC control/data services and clients register only when configured, and the example config now documents the opt-in keys.
- Classified Soulseek.NET listener socket disposal from `Soulseek.Network.Tcp.Listener.ListenContinuouslyAsync` as expected network teardown so it no longer logs as a fake fatal unobserved task exception.
- Reduced live journal noise by demoting verbose startup `[DI]` tracepoints, SPA fallback route serving, and per-request MediaCore CSRF processing logs to debug; controlled offline user-info responses now log concise summaries instead of `UserOfflineException` stacks, and shutdown-cancelled background searches no longer emit false error logs during manual deploys.
- Fixed user info lookups so expected Soulseek peer connection failures and timeouts return a controlled `503` instead of bubbling live peer unavailability as HTTP 500s.
- Quieted expected remote-offline download failures during restart/re-enqueue: transfers still fail normally, but `UserOfflineException` peer outcomes no longer emit repeated error stack traces.
- Quieted auto-replace shutdown cancellation during manual deploys so host-stop cancellation no longer logs search error stacks or counts interrupted items as failed replacements.
- Quieted the known Soulseek disconnect race during service shutdown so handled `Sequence contains no elements` races no longer print stack traces.
- Classified Soulseek.NET TCP double-disconnect read-loop races as expected network churn so they no longer log as fatal unobserved task exceptions.
- Fixed the `/system/network` DHT exposure consent copy so the inline `dht.lan_only=true` setting no longer runs into the following word.
- Reduced background search-batch journal noise by demoting routine per-search completion, mesh-search no-peer/fanout, and passive HashDb discovery progress to debug.
- Reduced auto-replace large-batch journal noise by demoting routine per-track search and no-result progress to debug while keeping aggregate cycle and successful-candidate logs visible.
- Excluded generated `src/slskd/dist` publish output from Web SDK publish content so manual artifacts do not recursively ship stale nested build output.
- Paced auto-replace alternative searches against the configured Soulseek search safety budget and stopped the cycle after a budget rejection, so one stuck-download batch defers cleanly instead of emitting per-track rate-limit stack traces.
- Stabilized the `local test host` manual-build follow-up set: hardened certificate handling, relay TLS pin validation, QUIC connection/task cleanup, shutdown/download draining, OpenAPI response mutation, and current-process fatal-noise classification after live soak testing.
- Broadened the latest pre-existing in-flight sweep into a release-visible changelog entry: this commit ships pending security, mesh, DHT, QUIC, API, UI, and diagnostics fixes accumulated since the last published release.
- Minimized the anonymous `/api/v0/profile/{peerId}` response payload to a public-safe shape so profile lookups no longer expose internal metadata (`PublicKey`, `Signature`, timing fields) to unauthenticated callers.
- Fixed a React hook-order regression in `/system/network` after the pre-existing DHT exposure UX changes so modal visibility state updates now run in a stable hook sequence.
- Downgraded the remaining Soulseek timer-reset teardown race on `Tcp.Connection.WriteInternalAsync(...)` so that benign third-party write-loop `NullReferenceException` noise no longer logs as fake fatal unobserved-task crashes.
- Aligned `bin/publish` with the tagged release publish profile so manual/live deploys no longer use a different self-contained single-file `ReadyToRun` runtime shape than CI ships.
- Hardened mesh QUIC lifecycle management: cached/orphaned `QuicConnection` instances are now explicitly disposed, duplicate connection-creation races no longer leak live connections, and QUIC overlay/data hosted services close and drain active connection handlers during shutdown.
- Fixed the TCP mesh overlay listener failing to rebind `50305` on fast restarts with `Address already in use` even when no live listener remained by enabling socket address reuse and fully clearing stop-state after shutdown.
- Fixed another live Soulseek teardown noise path by classifying the third-party `Soulseek.Extensions.Reset(Timer)` `NullReferenceException` from `ReadContinuouslyAsync` as expected peer/read-loop churn instead of logging it as a fake fatal unobserved-task crash.
- Fixed the `/pods` admin panel by putting `PodsController` back on the explicit versioned `api/v{version:apiVersion}/pods` surface that the frontend already calls, with a contract test to keep that route aligned.
- Fixed the `/system/mediacore` admin panel crash by restoring the missing Semantic UI `Checkbox` import.
- Fixed fast authenticated admin-panel sweeps self-triggering `429 Too Many Requests` by exempting authenticated requests and non-API web-shell/static requests from the coarse global fixed-window limiter while keeping anonymous API throttling in place.
- Fixed first-run share bootstrap log noise so missing/out-of-date cache state goes straight into the recreate/scan path instead of throwing a corruption-looking exception before recovering.
- Fixed the default `/` web route to redirect directly to `/searches` without logging a router miss, and stopped the app from probing session state on every render when no token is present.
- Added DHT/overlay operator diagnostics that roll up candidate handling (`seen`, `accepted`, skip/defer/backoff counts), expose endpoint cooldown/degradation stats, and log mesh session-end summaries so repeated bad remote endpoints stand out without flooding the logs.
- Fixed release-smoke integration compilation after the download-shutdown drain change by updating the integration test `StubDownloadService` to implement the new `IDownloadService.ShutdownAsync(CancellationToken)` member.
- Fixed download shutdown cleanup so active downloads cancelled by host shutdown stop quietly without trying to fail transfers through disposed services or release already-disposed enqueue semaphores.
- Fixed service shutdown ordering so active Soulseek downloads are drained before the shared client is disposed, avoiding restart-time global download semaphore warnings during clean shutdown.
- Fixed clean-shutdown error handling to ignore a third-party Soulseek disconnect collection race that could otherwise log a false fatal `Sequence contains no elements` during service restarts.
- Fixed service signal handling so normal `systemctl stop`/`restart` SIGTERM requests stop the generic host cleanly instead of logging fatal shutdown telemetry and exiting with status 1.
- Fixed user directory browse API handling so expected remote peer connection failures return a controlled 503 response instead of escaping as unhandled request exceptions with repeated middleware stack traces.
- Fixed DHT rendezvous connection accounting so peers deferred by the overlay connector's concurrency limit are not counted as real attempts or pushed into the five-minute retry backoff. This keeps diagnostics aligned with actual socket attempts and prevents a potentially valid candidate from being delayed behind simultaneous junk DHT endpoints.
- Fixed live mesh compatibility with peers that send unframed JSON control messages after a framed handshake. The overlay reader now recognizes raw JSON starting at a frame boundary, consumes exactly one capped JSON object, and keeps the normal length-prefixed path unchanged, preventing the deterministic two-minute `Invalid message length: 2065855609` disconnect seen on `local test host`.
- Fixed QUIC runtime gating so mesh QUIC services and direct-QUIC self-descriptors are enabled only when the current runtime/native MsQuic stack reports both connection and listener support, rather than assuming every Linux/macOS/Windows host has working QUIC.
- Fixed AudioSketch ffmpeg detection so the default `FfmpegPath: ffmpeg` resolves through `PATH` instead of being rejected by `File.Exists("ffmpeg")`, removing repeated false missing-ffmpeg warnings and allowing sketch hashes on hosts with ffmpeg installed normally.
- Downgraded another expected Soulseek peer teardown shape from fake fatal telemetry: unobserved `InvalidOperationException: The underlying Tcp connection is closed` from `Soulseek.Network.MessageConnection.ReadContinuouslyAsync` now falls into the expected network-churn classifier.
- Fixed a DHT startup race where a temporary overlay-port bind probe could decide the node was not beacon-capable during a fast restart, leaving TCP overlay `50305` offline until the next service restart. The real overlay listener start now determines beacon capability directly.
- Fixed overlay keepalive reads competing with the persistent message dispatcher: inbound mesh loops now send keepalive pings without doing a direct pong read, so all overlay frames continue through the single read loop even while mesh search or service traffic is active.
- Fixed DHT rendezvous wasting overlay connection attempts on peers that advertise the configured DHT UDP port as their endpoint. Those candidates are now ignored unless the deployment intentionally uses the same port for DHT and TCP overlay traffic, so discovery does not fill the peer cache with non-overlay `:50306` contacts on the default split-port setup.
- Fixed auto-replace missing valid alternatives when search finalization races the polling window. The live symptom was `No search responses found` followed by the same search completing with responses seconds later; auto-replace now waits for the persisted completed search state before deciding whether responses are absent.
- Fixed startup directory-browse requests racing Soulseek login: `POST /api/v0/users/{username}/directory` now returns `503 Soulseek server connection is not ready` while the client is still `Connected, LoggingIn` instead of throwing through the ASP.NET/security middleware pipeline as a noisy 500.
- Fixed mesh overlay peer churn traced from live `local test host` build `157` logs: outbound connections to remote peers were disconnecting after ~2 minutes with `Protocol violation ... Invalid message length: 2065855609` (bytes `0x7B2BF939` — JSON `{` bytes being mis-read as a 4-byte length prefix). Root cause was that `SecureMessageFramer.WriteMessageAsync` had no write lock while three separate task paths could write to the same connection concurrently: the message-loop path sending `Pong`s and responses, `MeshOverlaySearchService` sending `mesh_search_req` fan-outs, and `MeshServiceClient` sending service calls. Because `SslStream.WriteAsync` is not thread-safe for concurrent writers, the 4-byte length header and JSON payload of concurrent messages interleaved on the wire and the peer's reader desynced on the first torn frame. The framer now serializes writes with a `SemaphoreSlim`, is `IDisposable` and disposed by `MeshOverlayConnection`, and `DeserializeMessage` is now a stateless static so helper classes no longer construct a dead `SecureMessageFramer(Stream.Null)` just to get JSON options.
- Fixed three live `local test host` log issues observed on build `155`: (a) demoted the benign Soulseek.NET `System.Timers.Timer` disposed-object warning that fires when a late `SearchResponse` arrives after the search has already completed — it's now at `Debug` instead of spamming `~100/hr` of `Error handling peer message` warnings; (b) made the "JWT signing key is ephemeral" warning conditional on the raw configuration tree (previously it fired even when the operator had configured a persistent key, because `JwtOptions.Key` defaults to a freshly-generated random value that's indistinguishable from a configured one at the Options layer); and (c) split the QUIC overlay server onto its own `QuicListenPort` (default `50402`) so it can run concurrently with the legacy `UdpOverlayServer` on `50400` — previously both servers raced for exclusive UDP ownership and the loser logged `QUIC overlay port 50400 is already in use`. The DirectQuic advertisement in `PeerDescriptorPublisher` now points peers at `QuicListenPort` so inbound QUIC dials reach the new port.
- Fixed overlay mesh search fanout on live operator nodes: raised `OverlayProtocol.MaxMessageSize` from 4 KiB to 64 KiB so `mesh_search_resp` frames carrying up to `MaxResults=200` file DTOs no longer overflow the framer, which was previously mis-parsing response payload bytes as a 4-byte length header and throwing `ProtocolViolationException: Invalid message length`. Healed `FileKeyStore` so overlay identity survives restarts: `WriteToFile` was serializing `Ed25519KeyPair.CreatedAt` (DateTimeOffset) while `ReadFromFile` expected `KeyFileModel.CreatedMs` (long), causing every restart to load the saved key as epoch-0 and immediately rotate, cycling the node's overlay identity and reputation. `/api/v0/mesh/peers` now returns both the hash-sync peers and the live overlay neighbor registry so operators can see the connection the overlay layer actually holds. Mesh search fanout logs are promoted from Debug to Information with a post-aggregation "returned results from N/M peer(s)" summary.
- Fixed DHT rendezvous overlay pod transfers: mesh service-fabric calls now route over established overlay connections, inbound calls dispatch through the real mesh service router, pod search results preserve content-routing metadata, and the full-instance integration suite now proves two local slskdN nodes can connect, mesh-search, download, and byte-verify content over the overlay.
- Downgraded remote Soulseek `TransferRejectedException` enqueue failures from fake `[FATAL] Unobserved task exception` noise into the expected peer-network bucket. Downloads rejected with `Enqueue failed due to internal error` are still recorded as failed/rejected transfers, but no longer look like host-side fatal crashes.
- Fixed a live `local test host` source-ranking database race where concurrent transfer history updates could trip `SQLite Error 19: UNIQUE constraint failed: DownloadHistory.Username`. Download success/failure counters now use a single atomic SQLite upsert, with regression coverage proving concurrent first writes for the same username preserve every counter update.
- Fixed DHT rendezvous diagnostics authentication so configured API keys can access `/api/v0/dht/status`, `/api/v0/dht/peers`, and `/api/v0/overlay/stats` instead of those endpoints falling through to bearer-only auth despite the rest of the operator API accepting API keys.
- Resolved the remaining Dependabot security alert without suppressions: removed the vulnerable deprecated `OpenTelemetry.Exporter.Jaeger` package, kept `exporter: jaeger` working through the supported OTLP exporter path for Jaeger collectors, bumped `AWSSDK.S3` to `4.0.21.2`, and refreshed the npm lockfiles for the active Dependabot-managed package ranges.
- Fixed the latest issue `#209` overlay-search root cause: reciprocal mesh connections now keep independent inbound and outbound lifecycles, outbound sockets run the same message loop as inbound sockets, and mesh search responses are routed through a request router instead of competing readers on the same TLS stream. This prevents two healthy peers from disposing or starving each other's live connection after DHT discovery succeeds, and the loopback integration proof now repeats real `MeshOverlaySearchService` searches over the same outbound connection to prove the path stays usable.
- Fixed the AUR binary package source cache trap: the GitHub Linux glibc zips for `slskdn-bin` and `slskdn-dev` are now saved under versioned local source filenames, so yay/makepkg cannot build a package labeled with a newer `pkgver` while silently reusing an older cached release zip.
- Fixed the issue `#209` privacy leak in DHT/overlay logs: mesh usernames, peer ids, and public endpoints now go through `OverlayLogSanitizer` before operator logs, so pasted remote logs no longer expose raw Soulseek names like the earlier `Accepted mesh connection from ...` messages.
- Fixed the newest issue `#209` mesh failure reproduced from build `151`: quiet overlay neighbors no longer disconnect after the 30-second message-read timeout, inbound handshakes advertise the peer's overlay listener so the server can start a reciprocal outbound connection for request/response mesh RPCs, old peers fall back to the configured overlay port, and stale inbound cleanup can no longer unregister a newer outbound replacement. The two-full-instance mesh smoke now waits past the read timeout and proves both nodes stay connected.
- Added a Web UI regression test proving normal searches create requests without bridge providers unless `/api/slskdn/capabilities` explicitly advertises `scene_pod_bridge`, covering the issue `#209` zero-result failure mode from the browser path.
- Fixed the latest issue `#209` search regression by making the experimental Scene ↔ Pod bridge opt-in again. Default searches now stay on the proven upstream-compatible Soulseek path, the Web UI no longer enables bridge providers from generic capabilities, and `/api/slskdn/capabilities` advertises the bridge only when the server option is explicitly enabled.
- Added a deterministic two-full-instance mesh smoke for issue `#209`: the integration harness now launches isolated `slskd` subprocesses with unique appdirs and listener ports, forces one node to dial the other through the real overlay stack, and asserts both nodes report the live neighbor plus circuit peer inventory. Added an admin-only `/api/v0/overlay/connect` diagnostic endpoint for forced local/full-instance overlay probes and a gitignored `local-mesh-accounts.env` scaffold for optional live Soulseek account tests.
- Fixed another live issue `#209` mesh regression on `local test host`: DHT-discovered overlay endpoints are no longer one-shot connection attempts. The rendezvous service now tracks retry/backoff state separately from the discovered-peer cache, so a first timeout or refusal does not suppress all future retries for that endpoint. Host validation confirmed the fix by forcing a post-backoff discovery cycle and observing `totalConnectionsAttempted` increase for the same discovered-peer set instead of staying stranded at the first-attempt count.
- Fixed issue `#209`'s remaining direct-mode circuit failure: `AnonymityMode.Direct` now registers and prioritizes a real direct transport instead of still depending on a local Tor SOCKS proxy, so DHT-ready peers no longer immediately fail circuit establishment with `No anonymity transport is available` just because Tor is absent.
### Fixed

- Hardened web and sharing authentication: session login lockouts now track both remote IP and normalized username to blunt distributed password spray, and share tokens now bind the JWT `aud` field to `collection_id` so cross-collection replay fails audience validation instead of relying on a custom claim alone.
- Hardened Chromaprint fingerprint extraction so ffmpeg PCM output is read through a bounded buffer derived from the configured sample rate, channel count, and capture duration instead of an unbounded `MemoryStream`, preventing oversized or malformed decoder output from consuming arbitrary memory during audio fingerprinting.
- Added classified outbound overlay diagnostics for issue `#209`: `/api/v0/overlay/stats` now breaks connector failures down into stable buckets (`connectTimeouts`, `noRouteFailures`, `connectionRefusedFailures`, `connectionResetFailures`, `tlsEofFailures`, `tlsHandshakeFailures`, `protocolHandshakeFailures`, `registrationFailures`, `blockedPeerFailures`, `unknownFailures`) instead of one opaque failed-connection total. Live validation on `local test host` showed the current post-fix failures are dominated by remote candidate quality (`7` timeouts, `1` no-route) rather than another local TLS or protocol regression.
- Hardened the concurrent security follow-ups being merged alongside that diagnostics work: `WebSocketTransport` only disables WSS certificate validation when the new explicit `IgnoreCertificateErrors` lab-only option is set, `ShareScanner` now skips `ReparsePoint` symlinks/junctions so shared trees cannot index files outside the share root, and `docs/security/full-app-audit-2026-04.md` captures the broader security audit snapshot shipped with those fixes.
- Fixed the standalone distro packaging drift that was still breaking Jammy PPA and related release jobs after the main release path moved on: `release-ppa.yml`, `release-copr.yml`, and `release-linux.yml` now use `.NET 10`, validate the staged publish output, and the DEB/RPM runtime SONAME patch now discovers `libcoreclrtraceptprovider.so` dynamically inside the staged package tree instead of assuming one flat appdir path.

- Fixed the current root cause behind the latest issue `#209` reports: DHT-discovered rendezvous peers now publish into `IMeshPeerManager` immediately instead of only triggering a one-shot overlay connect attempt, so circuit maintenance can see real onion-capable peer candidates as soon as DHT discovery succeeds. Stale antiforgery cookie recovery now also retries on any key-ring/decryption exception shape, not just `AntiforgeryValidationException`, which stops repeated stale-token decrypt spam after reinstall or key rotation.
- Fixed the remaining operator-facing stale-cookie and diagnostics fallout on issue `#209`: safe-request CSRF token minting now strips known antiforgery cookies from the incoming request before ASP.NET tries to deserialize them, which stops stale-cookie GETs from spamming decrypt stack traces in the journal, and `/api/v0/dht/status` now reports configured DHT enablement separately from live readiness so the UI no longer claims DHT is disabled during bootstrap.
- Fixed another root cause behind issue `#209`'s disappearing overlay peers: stale TOFU certificate pins no longer auto-ban reachable slskdn peers after normal cert rotation or reinstall. Inbound and outbound overlay handshakes now rotate the stored pin on mismatch instead of partitioning the mesh until an operator manually clears `cert_pins.json`.
- Cleaned up issue `#209`'s remaining peer-health diagnostics lie: DHT-discovered rendezvous endpoints are now tracked as unverified `dht-discovered` candidates until an overlay handshake succeeds, instead of being counted immediately as onion-capable peers. This keeps circuit-maintenance and security peer stats aligned with real overlay verification instead of raw DHT discovery.
- Hardened overlay TOFU pin persistence: `cert_pins.json` writes now use a temp-file + flush + atomic rename path instead of in-place rewrite, so a crash or interrupted write cannot silently corrupt the entire pin store and reset peer trust continuity on restart. Added `docs/security/dht-mesh-audit-2026-04.md` capturing the current DHT/overlay threat-surface review and the rationale for the security decisions kept as-is.

- Debian/PPA source packaging now declares `patchelf` in `Build-Depends`, so Launchpad installs the tool required by `debian/rules` when patching the bundled .NET runtime during package assembly.

- Added explicit ICU runtime dependencies to the DEB and RPM packages so clean Ubuntu/Fedora installs can actually launch `slskd` instead of dying on first start with .NET globalization errors.
- Fixed the Fedora/COPR Linux package path by patching `libcoreclrtraceptprovider.so` during DEB/RPM package assembly to replace the old `liblttng-ust.so.0` SONAME with `liblttng-ust.so.1`, and by forcing the RPM bundle back onto the project's drop-in `/usr/lib/slskd` path instead of `%{_libdir}` so the shared `slskd.service` still points at a real executable on Fedora.
- Fixed the remaining false-fatal Soulseek transfer telemetry and DHT startup diagnostics: successful downloads no longer emit `[FATAL]` `Transfer failed: Transfer complete` unobserved-task noise after completion, and the DHT bootstrap grace period is now long enough for slow-but-healthy public-router bootstrap before warning operators about forwarding/firewall problems.

- Downgraded remote peer transfer rejections (`Download reported as failed by remote client`) into the expected Soulseek-network telemetry bucket so those peer-side failures no longer surface as fake `[FATAL] Unobserved task exception` host noise.

- Fixed the live download enqueue crash on Linux hosts after transfers reached `Queued, Remotely`: `DownloadService.EnqueueAsync(...)` no longer disposes its shared per-batch `SemaphoreSlim` while background enqueue tasks still release it, which removes the host-side `Cannot access a disposed object. Object name: 'System.Threading.SemaphoreSlim'.` failure and lets transfers proceed into real `InProgress` socket work again.

- Fixed the Arch/AUR packaging path so upgrades stop failing with stale `/usr/lib/slskd` file conflicts: the drop-in launcher path stays `/usr/lib/slskd/slskd`, but packaged releases now live under `/usr/lib/slskd/releases/<version>` with `/usr/lib/slskd/current`, the shared `slskd.service` still runs the packaged apphost, and the source PKGBUILD remains aligned to `.NET 10` with correct per-arch runtime IDs.

- Fixed the live Linux download failure that was aborting transfers before any bytes could be written: an unset `permissions.file.mode` now correctly falls back to the host umask in `FileService` instead of being parsed as an empty chmod string, which was throwing `The value cannot be an empty string or composed entirely of whitespace. (Parameter 'permissions')` during download file creation and move handling.

- Fixed the Transfers page bulk-action storm that was turning queue cleanup into its own failure mode: bulk retry/remove/cancel now enqueue into a background queue that drains one request at a time, duplicate submissions are ignored while the same work is already queued or running, `Remove All Completed` still uses the dedicated bulk-clear endpoint but now goes through the same deduped queue, and bulk failures surface as one summary toast instead of one popup per file.

- Fixed the newest issue `#209` mesh follow-up where DHT bootstrap/discovery succeeded but `Circuit maintenance` still stayed at `0 circuits, 0 total peers`. Live overlay neighbors are now mirrored into the circuit peer inventory through `MeshNeighborPeerSyncService`, and unit coverage reproduces the old empty-peer state without the sync service and the corrected populated-peer state with it.

- Followed up on the newer issue `#209` feedback after DHT bootstrap started succeeding: versioned `GET /api/v0/users/notes` now resolves correctly again, and the mesh overlay connector no longer runs a guaranteed-to-fail UDP hole-punch preflight against DHT-discovered TCP overlay endpoints. Hole-punch completion logs now also label their local port as an ephemeral UDP socket so operators do not mistake it for a randomized listener port.

- Followed up on the post-bootstrap runtime fallout behind issue `#209`: `Connection reset by peer` is now treated as expected Soulseek network churn instead of `[FATAL]`, stale antiforgery cookies are cleared and reissued after reinstall/key-ring changes, and obvious non-overlay TLS garbage on the public mesh port is downgraded to debug noise instead of warning stack traces.

- Stable GitHub releases now ship the Linux service/config helper files and a supported `install-linux-release.sh` installer so raw release upgrades replace the running `slskd.service` target instead of silently leaving an older systemd-managed binary in place.

- Added runtime self-identification for release-debugging: startup now logs the running executable/base paths, and `/system/info` exposes the live executable path, base directory, app directory, config path, and process id so stale installs can be distinguished from real regressions.

- Cleaned up release asset naming: future Linux builds publish a single explicit `linux-glibc-*` asset per runtime instead of duplicating the same payload under `main`, version-specific, and alias names. Packaging and release automation now consume the explicit glibc names directly.
- Fixed the stable package metadata drift that broke `Nix Package Smoke`: stable package metadata is now aligned with the published `linux-glibc-*` release assets so flake/package smoke validates the same filenames the release workflow actually ships.

- Fixed the tagged release pipeline to match the repo's `.NET 10` target and corrected Matrix release-message redaction to use the homeserver's accepted `PUT` method.

- Fixed the real root cause behind issue `#209`: slskdn was pinned to `MonoTorrent 3.0.2`, whose DHT bootstrap path could stall forever behind a single hidden router. slskdn now uses `MonoTorrent 3.0.3-alpha.unstable.rev0049`, passes an explicit `dht.bootstrap_routers` list into DHT startup, validates that at least one bootstrap router is configured, and documents the router list in the example config.

- Bumped the remaining open dependency-update backlog on `main`: `YamlDotNet 17.0.1`, `dotNetRDF 3.5.1`, `OpenTelemetry`/console/OTLP/hosting `1.15.2`, and the web test/build toolchain updates from the open Dependabot PR set (`follow-redirects 1.16.0`, `vite 8.0.8`, `vitest 4.1.4`, `jsdom 29.0.2`, `@types/node 25.6.0`, `@vitest/coverage-v8 4.1.4`, `@microsoft/signalr 7.0.14`).
- Completed the held major-version upgrade work instead of suppressing it: the web app now runs on React 18, React Router 7, ESLint 9 flat config, and the current `@uiw/react-codemirror` line, while the backend and test projects are aligned to `.NET 10`.
- Fixed the migration fallout from that dependency work by updating router usage, restoring passing web lint/build/test runs, and tightening the integration harness so missing external `soulfind` prerequisites fail fast instead of leaving the full-solution test run hanging.
- Fixed DHT rendezvous bootstrap defaults so new installs use a stable explicit UDP port instead of a random startup port, fail validation if enabled DHT is left on port `0`, and log actionable bootstrap guidance when the DHT never reaches `Ready`.
- Rejected loopback `Soulseek.ListenIpAddress` binds for live clients so slskd fails fast instead of logging in successfully while all peer-facing operations (`info`, `browse`, transfers) silently break behind an unreachable advertised endpoint. `Flags.NoConnect = true` still permits loopback for offline/testing scenarios.
- Fixed the real root causes behind the persistent tester reports on `#200` and `#201`: the web service worker was cache-first on navigations and pre-cached the app shell, serving a stale `index.html` that pointed at asset bundle hashes no longer on disk after every rebuild (blank new tabs, 404s on `/assets/*`); it is now network-first for HTML, never caches `/assets/*`, and the shell cache name is bumped so old versions are purged on activate.
- Removed `listenIPAddress` from the startup `SoulseekClientOptionsPatch`. It is already applied via `CreateInitialSoulseekClientOptions`; re-applying it through `ReconfigureOptionsAsync` at startup tore down the `TcpListener` mid-accept and raced `Listener.ListenContinuouslyAsync`, producing the `Not listening. You must call the Start() method before calling this method.` exception and leaving the listener stopped so every inbound peer connection was refused and all transfers failed.
- Wired the existing `GET api/jobs` / `GET api/v{version}/jobs` endpoint to a real production data source. `slskd.API.Native.JobsController` depended on `IJobServiceWithList`, which had no production registration — only a test-harness one — so in production the endpoint always returned zero jobs, which is what the `System/Jobs` Web UI renders as "doesn't load." Added `HashDbJobServiceListAdapter` backed by new `ListDiscographyJobsAsync` / `ListLabelCrateJobsAsync` methods on `IHashDbService`, and registered it in DI.

- Fixed mesh self-descriptor publication so unsupported-QUIC hosts no longer advertise fake `DirectQuic` transports or legacy Soulseek-style `2234/2235` endpoints. Auto-detected mesh endpoints now use explicit `udp://...:<overlay-port>` legacy addresses derived from the real overlay listen port, and direct QUIC transport advertisement is suppressed when the running host cannot actually accept QUIC.

## [0.24.5-slskdn.125] — 2026-04-13

- Closed the remaining tester follow-up on issues `#200` and `#201` by fixing the last versioned Web UI/API route gaps, tightening MediaCore and Jobs API versioning, removing the blanket benign `Connection refused` suppression, and covering those production `/api/v0/...` paths in release smoke.
- Removed the unnecessary download enqueue peer preflight that could fail on an auxiliary `Connection refused`, and aligned startup Soulseek option patching so `incomingConnectionOptions` is configured at startup the same way later live reconfigure already does.
- Added Matrix release announcements to the tagged dev and stable release workflow.

## [0.24.5-slskdn.124] — 2026-04-09

- Updated the frontend dependency baseline to `axios 1.15.0` and locked transitive `lodash 4.18.1`, clearing the standing Dependabot bumps and returning `npm audit` in `src/web` to `0` vulnerabilities.

## [0.24.5-slskdn.123] — 2026-04-09

- Finished the earlier issues `#200` and `#201` follow-up by restoring hard-refresh support on client-side routes, versioning the Bridge and Security Web UI/API paths consistently, preserving legacy Bridge compatibility, and moving Soulseek listener bootstrap settings into the initial client options.
- Fixed the release-gate subpath smoke harness so it mirrors the backend HTML rewrite behavior for `web.url_base` deployments instead of enforcing the obsolete relative-asset build model.
- Added Discord release announcements for tagged dev and stable releases, and blocked recurring `axios` / `lodash` Dependabot churn that was reopening the same low-value dependency PRs.
