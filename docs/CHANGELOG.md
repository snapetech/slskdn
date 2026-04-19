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

- Downgraded remote Soulseek `TransferRejectedException` enqueue failures from fake `[FATAL] Unobserved task exception` noise into the expected peer-network bucket. Downloads rejected with `Enqueue failed due to internal error` are still recorded as failed/rejected transfers, but no longer look like host-side fatal crashes.
- Fixed a live `kspls0` source-ranking database race where concurrent transfer history updates could trip `SQLite Error 19: UNIQUE constraint failed: DownloadHistory.Username`. Download success/failure counters now use a single atomic SQLite upsert, with regression coverage proving concurrent first writes for the same username preserve every counter update.
- Fixed DHT rendezvous diagnostics authentication so configured API keys can access `/api/v0/dht/status`, `/api/v0/dht/peers`, and `/api/v0/overlay/stats` instead of those endpoints falling through to bearer-only auth despite the rest of the operator API accepting API keys.
- Resolved the remaining Dependabot security alert without suppressions: removed the vulnerable deprecated `OpenTelemetry.Exporter.Jaeger` package, kept `exporter: jaeger` working through the supported OTLP exporter path for Jaeger collectors, bumped `AWSSDK.S3` to `4.0.21.2`, and refreshed the npm lockfiles for the active Dependabot-managed package ranges.
- Fixed the latest issue `#209` overlay-search root cause: reciprocal mesh connections now keep independent inbound and outbound lifecycles, outbound sockets run the same message loop as inbound sockets, and mesh search responses are routed through a request router instead of competing readers on the same TLS stream. This prevents two healthy peers from disposing or starving each other's live connection after DHT discovery succeeds, and the loopback integration proof now repeats real `MeshOverlaySearchService` searches over the same outbound connection to prove the path stays usable.
- Fixed the AUR binary package source cache trap: the GitHub Linux glibc zips for `slskdn-bin` and `slskdn-dev` are now saved under versioned local source filenames, so yay/makepkg cannot build a package labeled with a newer `pkgver` while silently reusing an older cached release zip.
- Fixed the issue `#209` privacy leak in DHT/overlay logs: mesh usernames, peer ids, and public endpoints now go through `OverlayLogSanitizer` before operator logs, so pasted remote logs no longer expose raw Soulseek names like the earlier `Accepted mesh connection from ...` messages.
- Fixed the newest issue `#209` mesh failure reproduced from build `151`: quiet overlay neighbors no longer disconnect after the 30-second message-read timeout, inbound handshakes advertise the peer's overlay listener so the server can start a reciprocal outbound connection for request/response mesh RPCs, old peers fall back to the configured overlay port, and stale inbound cleanup can no longer unregister a newer outbound replacement. The two-full-instance mesh smoke now waits past the read timeout and proves both nodes stay connected.
- Added a Web UI regression test proving normal searches create requests without bridge providers unless `/api/slskdn/capabilities` explicitly advertises `scene_pod_bridge`, covering the issue `#209` zero-result failure mode from the browser path.
- Fixed the latest issue `#209` search regression by making the experimental Scene ↔ Pod bridge opt-in again. Default searches now stay on the proven upstream-compatible Soulseek path, the Web UI no longer enables bridge providers from generic capabilities, and `/api/slskdn/capabilities` advertises the bridge only when the server option is explicitly enabled.
- Added a deterministic two-full-instance mesh smoke for issue `#209`: the integration harness now launches isolated `slskd` subprocesses with unique appdirs and listener ports, forces one node to dial the other through the real overlay stack, and asserts both nodes report the live neighbor plus circuit peer inventory. Added an admin-only `/api/v0/overlay/connect` diagnostic endpoint for forced local/full-instance overlay probes and a gitignored `local-mesh-accounts.env` scaffold for optional live Soulseek account tests.
- Fixed another live issue `#209` mesh regression on `kspls0`: DHT-discovered overlay endpoints are no longer one-shot connection attempts. The rendezvous service now tracks retry/backoff state separately from the discovered-peer cache, so a first timeout or refusal does not suppress all future retries for that endpoint. Host validation confirmed the fix by forcing a post-backoff discovery cycle and observing `totalConnectionsAttempted` increase for the same discovered-peer set instead of staying stranded at the first-attempt count.
- Fixed issue `#209`'s remaining direct-mode circuit failure: `AnonymityMode.Direct` now registers and prioritizes a real direct transport instead of still depending on a local Tor SOCKS proxy, so DHT-ready peers no longer immediately fail circuit establishment with `No anonymity transport is available` just because Tor is absent.
### Fixed

- Hardened web and sharing authentication: session login lockouts now track both remote IP and normalized username to blunt distributed password spray, and share tokens now bind the JWT `aud` field to `collection_id` so cross-collection replay fails audience validation instead of relying on a custom claim alone.
- Hardened Chromaprint fingerprint extraction so ffmpeg PCM output is read through a bounded buffer derived from the configured sample rate, channel count, and capture duration instead of an unbounded `MemoryStream`, preventing oversized or malformed decoder output from consuming arbitrary memory during audio fingerprinting.
- Added classified outbound overlay diagnostics for issue `#209`: `/api/v0/overlay/stats` now breaks connector failures down into stable buckets (`connectTimeouts`, `noRouteFailures`, `connectionRefusedFailures`, `connectionResetFailures`, `tlsEofFailures`, `tlsHandshakeFailures`, `protocolHandshakeFailures`, `registrationFailures`, `blockedPeerFailures`, `unknownFailures`) instead of one opaque failed-connection total. Live validation on `kspls0` showed the current post-fix failures are dominated by remote candidate quality (`7` timeouts, `1` no-route) rather than another local TLS or protocol regression.
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
