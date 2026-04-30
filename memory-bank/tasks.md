# Tasks (Source of Truth)

> This file is the canonical task list for slskdN development.  
> AI agents should add/update tasks here, not invent ephemeral todos in chat.

---

## Active Development

### High Priority

*No high priority tasks currently active

- [x] **feature**: Add Quarantine Jury pod routing attempts.
 - Status: completed (2026-04-30)
 - Priority: P3
 - Notes: Quarantine Jury requests can now route to selected safe jurors through PodCore, persist route attempt history, and expose dispatch/history endpoints. Invalid target jurors and unavailable routing backends return failed attempts without contacting peers.

- [x] **feature**: Add browser-local listening history and stats.
 - Status: completed (2026-04-30)
 - Priority: P3
 - Notes: Added browser-local play history recording at the same playback threshold used for scrobbling, plus a player Listening Stats modal with total plays, recent plays, top artists, top tracks, and a local history clear action. No external scrobbling, media-server import, recommendation fetch, or file mutation is triggered by the stats view.

- [x] **feature**: Add listening time ranges and forgotten favorites.
 - Status: completed (2026-04-30)
 - Priority: P3
 - Notes: Listening Stats now supports 7-day, 30-day, 90-day, and all-time filters plus browser-local forgotten favorites derived from repeat plays outside the active range. This remains local-only and does not call external recommendation, scrobbling, or media-server APIs.

- [x] **feature**: Add bounded player similar-track auto-queue.
 - Status: completed (2026-04-30)
 - Priority: P3
 - Notes: Added a local auto-fill action that scores recent session tracks by artist, album, genre/tag, and title overlap, then appends similar tracks that are not already queued. It only uses already-known browser session history and does not search, browse peers, stream, download, or call metadata services.

- [x] **feature**: Add player queue manager.
 - Status: completed (2026-04-30)
 - Priority: P3
 - Notes: Added a playback queue modal with current track, full upcoming queue, recent session history, remove queued item, clear upcoming, previous, and next controls. Removing and clearing queue entries keep the current track intact and do not start searches, downloads, or recommendation work.

- [x] **feature**: Add player smart-radio seed handoff.
 - Status: completed (2026-04-30)
 - Priority: P3
 - Notes: Added a player smart-radio modal that builds similar-track, album-neighborhood, artist/genre, and artist-radio Search handoff queries from the current now-playing metadata. Opening the modal does not search, queue, browse, download, or mutate playback; network search begins only if the user explicitly opens one generated query.

- [x] **feature**: Add player keyboard shortcuts.
 - Status: completed (2026-04-30)
 - Priority: P3
 - Notes: Added browser-local player shortcuts for play/pause, seek backward/forward, previous/next with Shift+Arrow, mute, equalizer, lyrics, and visualizer toggles. Shortcut handling ignores editable controls and modified browser/system key chords.

- [x] **feature**: Add player now-playing ratings and evidence badges.
 - Status: completed (2026-04-30)
 - Priority: P3
 - Notes: Added browser-local rating storage for now-playing tracks, preserved playback source/confidence/verification metadata through PlayerContext, and surfaced compact source, match, verified, and discovery-rating controls in PlayerBar. Ratings remain local browser context and do not sync, auto-download, delete, or publish discovery evidence yet.

- [x] **feature**: Add visible acquisition profiles to Search.
 - Status: completed (2026-04-30)
 - Priority: P2
 - Notes: Added a reusable Web UI acquisition profile catalog and persisted Search-page selector for Lossless Exact, Fast Good Enough, Album Complete, Rare Hunt, Conservative Network, Mesh Preferred, and Metadata Strict. This is the first visible control surface for the competitive roadmap; backend ranking/download behavior is intentionally unchanged in this slice.

- [x] **feature**: Add visible Automation Center shell.
 - Status: completed (2026-04-30)
 - Priority: P2
 - Notes: Added a System -> Automations tab that lists every planned automation recipe, persists visible enablement toggles, shows network/file impact and cadence, keeps low-risk local recipes enabled by default, and records dry-run checkpoints without executing network or file actions.

- [x] **feature**: Carry acquisition profile intent through search create requests.
 - Status: completed (2026-04-30)
 - Priority: P2
 - Notes: Search creation now sends the selected acquisition profile id to the API, the backend trims and validates known profile ids, and focused Web UI/controller tests cover default, selected, and invalid-profile behavior. Ranking/download behavior remains unchanged until the profile policy layer is implemented.

- [x] **feature**: Apply conservative acquisition profile search defaults.
 - Status: completed (2026-04-30)
 - Priority: P2
 - Notes: Known acquisition profiles now map to bounded search option defaults for timeout, response/file limits, minimum response count, and peer queue cap where appropriate. Explicit API request options override profile defaults, keeping advanced/manual callers in control.

- [x] **feature**: Add visible source provider capability catalog.
 - Status: completed (2026-04-30)
 - Priority: P2
 - Notes: Added a read-only `/source-providers` API and System -> Source Providers tab that list all known acquisition source providers, active/disabled state, registration, risk level, capabilities, network policy, and disabled reasons. The catalog is observational only and does not start searches, downloads, peer probes, DHT work, or credential checks.

- [x] **feature**: Show acquisition profile provider priority policies.
 - Status: completed (2026-04-30)
 - Priority: P2
 - Notes: Extended the source-provider catalog with read-only provider priority chains for every acquisition profile. All profile policies currently report manual acquisition with auto-download disabled, making fallback order visible before provider execution is wired.

- [x] **feature**: Add explained search candidate ranking.
 - Status: completed (2026-04-30)
 - Priority: P2
 - Notes: Replaced the Search detail peer-only smart score with a reusable browser-side candidate scorer that ranks visible results by acquisition profile intent, filename match, audio format evidence, file-size sanity, free slot/queue/speed availability, provider hints, and past download history. Result cards now expose a score and concise reasons without starting new network activity.

- [x] **feature**: Add local album candidate picker to Search results.
 - Status: completed (2026-04-30)
 - Priority: P2
 - Notes: Search detail now groups already-returned result files into album-shaped folder candidates, scores them by visible tracks, source count, lossless evidence, folder completeness, and existing candidate rank, and provides a tooltipped local filter action. The picker does not start searches, downloads, peer browsing, or metadata lookups.

- [x] **feature**: Add album candidate review details and warnings.
 - Status: completed (2026-04-30)
 - Priority: P2
 - Notes: Album candidates now show local review metadata for format mix, missing track numbers, duration spread, source count, and confidence warnings such as mixed formats, missing tracks, large duration variance, and single-source candidates. The review surface is based only on already-returned search metadata and does not contact peers or start downloads.

- [x] **feature**: Add album candidate substitution option hints.
 - Status: completed (2026-04-30)
 - Priority: P2
 - Notes: Album candidates now retain per-track visible source options and surface manual substitution hints when multiple peers/providers offer the same track number. These hints are local review metadata only; they do not select alternates, save rules, browse peers, or start downloads.

- [x] **feature**: Add browser-local album decision rule previews.
 - Status: completed (2026-04-30)
 - Priority: P2
 - Notes: Album candidates can now save a browser-local rule preview containing normalized album/search identity, expected track count, format policy, warnings, and substitution tracks. Rule previews are capped and deduped in local storage only; they do not affect ranking, planner, downloads, peer browsing, or future searches yet.

- [x] **feature**: Add search download action previews.
 - Status: completed (2026-04-30)
 - Priority: P2
 - Notes: Search result cards now provide a selected-file action preview before downloading. The preview summarizes source, providers, file count, size, candidate score, selected paths, and local warnings, with copy/export text support. Previewing does not call the API, browse peers, stream, or start transfers.

- [x] **feature**: Add preferred search ranking conditions.
 - Status: completed (2026-04-30)
 - Priority: P2
 - Notes: Advanced Search filters now support ranking-only preferences for extensions, lossless files, and minimum bitrate. Preferred conditions influence candidate scores and reasons without hiding fallback results or starting any network work.

- [x] **feature**: Add local search-result deduplication.
 - Status: completed (2026-04-30)
 - Priority: P2
 - Notes: Search results now have a visible Fold Duplicates toggle that folds duplicate media candidates after filtering, ranking, and sorting. The best-ranked candidate remains visible with folded-source metadata and provider/peer context, and users can disable the fold to inspect every source separately. This is browser-local and does not start searches, peer browses, or downloads.

- [x] **feature**: Add browser-local Discovery Inbox review surface.
 - Status: completed (2026-04-30)
 - Priority: P2
 - Notes: Added a persistent Web UI Discovery Inbox with Suggested/Approved/Snoozed/Rejected review states, bulk approve/reject, per-item review actions, acquisition-profile context, and explicit network-impact text. Search can now save the current phrase into the inbox without starting peer network activity.

- [x] **feature**: Add Discovery Inbox impact review summary.
 - Status: completed (2026-04-30)
 - Priority: P2
 - Notes: Discovery Inbox now classifies saved candidates as Local/manual, Provider review, Network risk, or Needs estimate, shows aggregate batch-readiness counts before approval, and labels each candidate with its inferred impact class. This is evidence-only review metadata and does not start provider lookups, peer browsing, searches, downloads, or automation.

- [x] **feature**: Unify Wishlist rows with acquisition request states.
 - Status: completed (2026-04-30)
 - Priority: P2
 - Notes: Added shared Web UI request-state mapping for Wishlist entries and Discovery Inbox evidence, showing Disabled, Wanted, Automatic, Review, Approved, Snoozed, Rejected, Staged, Imported, and Failed states. Wishlist rows can now be sent to Discovery Inbox review without starting downloads.

- [x] **feature**: Add browser-local import staging review.
 - Status: completed (2026-04-30)
 - Priority: P2
 - Notes: Added an Import Staging route with a local file picker, persisted file metadata, staged/ready/imported/rejected/failed states, and review actions that do not move, upload, import, or mutate library files.

- [x] **feature**: Add local import metadata matcher.
 - Status: completed (2026-04-30)
 - Priority: P2
 - Notes: Added a browser-side filename metadata matcher that parses artist, album, title, track number, file type evidence, confidence, and warnings. Import Staging rows can be matched individually or in bulk without contacting metadata services, fingerprinting audio, or mutating files.

- [x] **feature**: Add opt-in import fingerprint verification.
 - Status: completed (2026-04-30)
 - Priority: P2
 - Notes: Import Staging now has an explicit Fingerprint on add toggle. When enabled, newly selected files are read locally in the browser and hashed with SHA-256, storing only verification metadata in the staging queue without uploading, importing, tagging, or moving files.

- [x] **feature**: Add failed-import denylist to Import Staging.
 - Status: completed (2026-04-30)
 - Priority: P2
 - Notes: Rejected staged files now create a browser-local failed-import denylist entry keyed by SHA-256 when available or file metadata signature otherwise. Matching re-adds are marked Failed with a blocked reason instead of silently returning as normal staged work, and denylist entries can be removed from the UI.

- [x] **feature**: Add mobile review layouts for Discovery Inbox and Import Staging.
 - Status: completed (2026-04-30)
 - Priority: P3
 - Notes: Discovery Inbox and Import Staging now have narrow-screen touch layouts with full-width primary actions, card-like mobile review rows, table cell labels for staged import metadata, and 44px-class touch targets without changing acquisition or import behavior.

- [x] **feature**: Add local community quality signals to Search review.
 - Status: completed (2026-04-30)
 - Priority: P3
 - Notes: Added browser-local peer quality signal storage, local caution reporting from Search result cards, Search ranking context for local quality signals, and visible local-only quality badges. Signals remain private browser-side context and do not publish global peer reputation or block candidates.

- [x] **feature**: Add browser-local Mesh Evidence Policy controls.
 - Status: completed (2026-04-30)
 - Priority: P3
 - Notes: Added a Mesh tab policy panel for inbound trust tier selection, provenance-required status, and explicit outbound publication toggles for signed hash verification, release completeness, fake-lossless warnings, metadata corrections, and realm subject indexes. Defaults are private/off and no backend publication is wired in this slice.

- [x] **feature**: Add redacted diagnostic bundle in System Info.
 - Status: completed (2026-04-30)
 - Priority: P3
 - Notes: Added a browser-side diagnostic bundle builder and System Info modal that shows/copies a YAML support snapshot with browser, route, state, and option shape while redacting sensitive keys and query-style secrets. The bundle is local-only and does not contact the server.

- [x] **feature**: Add media-server integration readiness and path diagnostics.
 - Status: completed (2026-04-30)
 - Priority: P3
 - Notes: Added Plex, Jellyfin/Emby, and Navidrome readiness cards to System Integrations plus a local path diagnostic for slskdN completed paths, media-server report paths, and optional remote path mappings. This does not connect to media servers or trigger scans yet.

- [x] **feature**: Add local Servarr setup readiness checklist.
 - Status: completed (2026-04-30)
 - Priority: P3
 - Notes: Added a System Integrations checklist for Servarr base URL, scoped API key presence, wanted pull, completed import, and remote path-map sanity. It is diagnostic only and does not register indexers, create download clients, pull wanted items, or trigger imports.

- [x] **feature**: Add Wishlist request portal summary.
 - Status: completed (2026-04-30)
 - Priority: P3
 - Notes: Added derived Wishlist request summary counts for total requests, enabled requests, automatic requests, Discovery Inbox review load, and quota-style remaining capacity. This is read-only/operator-facing and does not change request submission, approval, scheduling, or download behavior.

- [x] **feature**: Add bounded Automation Center dry-run reports.
 - Status: completed (2026-04-30)
 - Priority: P3
 - Notes: Added cooldown, max-runtime, and approval-gate metadata to visible automation recipes. Dry-run checkpoints now persist a preview report with network/file impact and explicit `executed: false`, preserving the current shell-only behavior.

- [ ] **feature**: Wire approved Discovery Inbox candidates into acquisition jobs.
 - Status: planned
 - Priority: P2
 - Notes: Follow up the local review surface by connecting Approved candidates to backend-backed acquisition planning/downloading with explicit opt-in automation, rate limits, and visible network impact before any peer contact.

- [x] **T-939**: Source feed imports.
 - Status: completed (2026-04-30)
 - Priority: P2
 - Design: `docs/design/music-discovery-federation-plan.md`
- Notes: Added backend source-feed preview for CSV, pasted text, M3U/PLS, RSS/OPML, and provider URLs. Spotify supports public playlist/album/track/artist/user playlist imports through configured app credentials or a connected account, plus liked/saved tracks, saved albums, followed artists, and current-user playlists through either a connected Spotify account or a per-import bearer token with the required scopes. Non-Spotify URL support now includes Apple Music/iTunes lookup, ListenBrainz public-listens import, optional YouTube Data API playlist expansion, optional Last.fm loved/recent/top track imports, and metadata-page fallback for YouTube, Bandcamp, Last.fm, and Apple URLs. The Wishlist UI now has an Import Feed flow that previews results, connects/disconnects Spotify, and adds selected provenance-rich suggestions to Discovery Inbox review without starting Soulseek searches, peer browses, or downloads. System Integrations now exposes source-feed provider settings for Spotify, YouTube, and Last.fm with on/off toggles, masked credential entry, validation warnings, and tooltip-backed runtime apply/reset controls.

- [ ] **T-938**: Browser-native MilkDrop3-compatible visualizer engine.
 - Status: active design
 - Priority: P1
 - Design: `docs/design/webgl-milkdrop3-port.md`
 - Notes: Build a portable WebGL2-first visualizer engine inside slskdN with MilkDrop/MilkDrop3 preset compatibility, shared Web Audio input, `.milk2` double-preset support, q1-q64, FFT shader access, beat-driven preset changes, transitions, playlists/favorites, and an extensible renderer boundary. Keep the external MilkDrop3 launcher only as an interim bridge.
 - Progress (2026-04-30): Added Phase 0 engine boundary with Butterchurn as the first adapter, then added the first parser/VM compatibility slice for `.milk`, basic `.milk2`, custom shape/wave equations, q1-q64 preservation, and deterministic equation evaluation.
 - Progress (2026-04-30): Added the first WebGL2 renderer skeleton that compiles a shader program, evaluates preset equations, and draws a full-screen GPU pass from MilkDrop color variables.
 - Progress (2026-04-30): Added ping-pong feedback texture/framebuffer targets, screen blit, target swapping, resize storage, and GPU cleanup.
 - Progress (2026-04-30): Added first fixed-function warp uniforms from evaluated `zoom`, `rot`, `dx`, and `dy` values while sampling the previous feedback frame.
 - Progress (2026-04-30): Added first waveform primitive pass that maps audio samples into WebGL line-strip vertices and draws them into the feedback target.
 - Progress (2026-04-30): Added first parsed shape primitive pass that renders enabled shape entries as closed WebGL line strips.
 - Progress (2026-04-30): Added first custom shape init/frame equation evaluation with per-shape q-register persistence and no global frame/audio scope leakage.
 - Progress (2026-04-30): Added filled, bordered, alpha-blended, and additive rendering for parsed custom shapes.
 - Progress (2026-04-30): Added first shape second-color gradient buffers and thick-outline line width handling.
 - Progress (2026-04-30): Added first custom wave init/frame/point equation rendering with audio-sample inputs, colors, alpha, additive blending, and thick line hints.
 - Progress (2026-04-30): Added custom wave dot rendering and spectrum-source sampling from frame frequency data.
 - Progress (2026-04-30): Added analyzer-backed `get_fft` and `get_fft_hz` expression helpers using renderer-provided frequency data.
 - Progress (2026-04-30): Added explicit WebGL attribute rebinding before each renderer draw path and first CPU-evaluated per-pixel warp-grid rendering.
 - Progress (2026-04-30): Added first motion-vector rendering from `mv_*` preset values as alpha-blended WebGL line segments.
 - Progress (2026-04-30): Added the native WebGL MilkDrop engine as an explicit opt-in player visualizer engine with curated smoke presets and shared Web Audio analyser input.
 - Progress (2026-04-30): Added a Vite-backed Chromium pixel smoke test for the native WebGL renderer and exposed it as `npm run test:native-milkdrop-smoke`.
 - Progress (2026-04-30): Added native-engine local `.milk`/`.milk2` preset import with overlay affordance, local persistence, and component/adapter tests.
 - Progress (2026-04-30): Added native render-loop error surfacing for unsupported imported presets and clears persisted bad imports; documented the gotcha as `bf9e51b3a`.
 - Progress (2026-04-30): Expanded native expression compatibility with common NSEEL math/constants, `rand`, and bitwise helper functions.
 - Progress (2026-04-30): Added import-time native preset compatibility reporting for unsupported equation functions and pending shader sections before replacing the active renderer.
 - Progress (2026-04-30): Added a capped browser-local native preset library with multi-file `.milk`/`.milk2` import, skipped-file reporting, and overlay preset reload selector.
 - Progress (2026-04-30): Added tooltipped native preset-library clear and remove-selected affordances so imported local presets can be pruned from browser storage.
 - Progress (2026-04-30): Added inline bitwise, shift, unary, and logical expression operator support for `&`, `|`, `^`, `~`, `!`, `<<`, `>>`, `&&`, and `||` in native MilkDrop equations.
 - Progress (2026-04-30): Added the first safe shader translation/execution subset for simple `warp_shader` and `comp_shader` `ret = ...` bodies, with unsupported HLSL/control-flow shaders still rejected during compatibility scanning.
 - Progress (2026-04-30): Added the first curated native preset fixture pack with golden parser summaries, compatibility expectations, and shader-backed browser smoke coverage.
 - Progress (2026-04-30): Added the first procedural textured-shape render path for parsed `textured`, `texture`, `tex`, and `tex_name` shape references.
 - Progress (2026-04-30): Added native import/library plumbing for small local image texture assets selected alongside `.milk`/`.milk2` presets, with named texture lookup and procedural fallback.
 - Progress (2026-04-30): Added skipped texture-asset reporting for oversized, unreadable, or unsupported files selected during native preset import.
 - Progress (2026-04-30): Improved native shape texture lookup to match imported assets by quoted path, normalized path, basename, or stem.
 - Progress (2026-04-30): Fixed `.milk2` import inspection so every preserved preset body is compatibility-checked before the file is accepted.
 - Progress (2026-04-30): Added first `.milk2` simultaneous composite rendering by drawing the primary preset body normally and blending secondary bodies over it, with native engine and browser smoke coverage.
 - Progress (2026-04-30): Added first `spriteNN_` parse, compatibility, equation-evaluation, and textured-quad render path using imported image assets or procedural fallback.
 - Progress (2026-04-30): Scoped imported native texture assets per preset by scanning texture/sprite references and indexing browser-provided relative paths, so multi-preset packs do not persist unrelated images with every preset.
 - Progress (2026-04-30): Added a separate native preset-folder import affordance using browser directory file input attributes, with relative path coverage for pack assets.
 - Progress (2026-04-30): Added the first native renderer-set crossfade scheduler for preset/import changes and first `.milk2` secondary composite-alpha controls via `blend_alpha` aliases.
 - Progress (2026-04-30): Added first standalone `.shape` and `.wave` fragment parsing, active-preset merge/persistence, and browser export affordances.
 - Progress (2026-04-30): Added first native beat and timed automatic preset change modes with low-frequency beat detection, render-loop preset updates, and browser-local mode persistence.
 - Progress (2026-04-30): Added first browser-local native preset-bank controls for favorite marking, favorites-only filtering, previous-selection history, next-library cycling, and random imported-preset jumps.
 - Progress (2026-04-30): Added first native preset-bank search that persists locally and scopes imported-preset next/random navigation to the filtered result set.
 - Progress (2026-04-30): Added first browser-local native preset playlists that save the current filtered bank, select/clear/delete named playlists, and scope navigation to the active playlist.
 - Progress (2026-04-30): Added renderer-wide q1-q64 initialization plus q-register propagation from global, custom wave, shape, and sprite evaluation stages back into the frame scope.
 - Progress (2026-04-30): Added first translated shader uniform binding for q1-q64 and bass/mid/treble audio variables in supported native warp/comp shader expressions.
 - Progress (2026-04-30): Added first shader-side `get_fft()` and `get_fft_hz()` support for translated native warp/comp shaders using a normalized 32-bin FFT uniform array.
 - Progress (2026-04-30): Added native primitive-field aliases for common MilkDrop custom wave, shape, and sprite names including `nSamples`, `bSpectrum`, `bUseDots`, `bDrawThick`, `bAdditive`, `bTextured`, `numSides`, and `texName`.
 - Progress (2026-04-30): Added first classic `ob_*` and `ib_*` native MilkDrop screen-border rendering as alpha-blended GPU rings.
 - Progress (2026-04-30): Added first classic native waveform modes with placement, alpha, scaling, and smoothing support from `wave_mode`, `wave_x`, `wave_y`, `wave_a`, `wave_scale`, and `wave_smoothing`.
 - Progress (2026-04-30): Expanded native shader translation with safe straight-line temp declarations and common HLSL helper aliases including `frac`, `fmod`, `rsqrt`, and `atan2`.
 - Progress (2026-04-30): Added translated shader viewport context with `resolution`, `pixelSize`, `aspect`, `texsize`, and generated `x/y/rad/ang` coordinate helpers.
 - Progress (2026-04-30): Added safe `shader_body { ... }` wrapper unwrapping for translated native warp/comp shaders and fixture smoke coverage.
 - Progress (2026-04-30): Added first translated shader named-texture sampler support for up to four `tex`/`tex2D` preset samplers, reusing imported texture aliases with procedural fallback.
 - Progress (2026-04-30): Added first simple ret-only translated shader conditional support for `if (...) ret = ...; else ret = ...;` bodies.
 - Progress (2026-04-30): Added safe declared-temp reassignment support in translated native shader bodies while rejecting undeclared assignment and post-`ret` statements.
 - Progress (2026-04-30): Added native MilkDrop compatibility matrix reporting for curated fixtures and local preset files/folders, including first high-count wave/shape metric coverage for real-pack pressure.
 - Progress (2026-04-30): Added richer `.milk2` transition and composite controls with preset-defined transition durations plus alpha/additive/screen/multiply secondary blend modes.
 - Progress (2026-04-30): Added q-register pressure metrics to the native MilkDrop compatibility matrix and a MilkDrop3-style fixture that exercises q1/q2/q16/q32/q48/q63/q64 across globals, primitives, and translated shaders.
 - Progress (2026-04-30): Added dense primitive-count validation with a curated 40-shape/20-wave fixture in compatibility reporting and native browser smoke coverage.
 - Progress (2026-04-30): Added native transition modes beyond the default crossfade, including cut, fade-through-black, and overlay modes selected by preset aliases or caller options.
 - Progress (2026-04-30): Expanded translated shader audio uniforms from 32 FFT bins to 64 FFT bins and added signed 64-bin waveform access via `get_waveform(pos)`.

- [x] **T-930**: Discography Concierge coverage map.
 - Status: completed (2026-04-30)
 - Priority: P1
 - Design: `docs/design/music-discovery-federation-plan.md`
 - Notes: Added a conservative coverage API, MusicBrainz release-detail caching, HashDb/Wishlist evidence cells, manual missing-track Wishlist promotion, focused backend coverage tests, and a Search-page Discography Concierge panel with tooltipped actions. No Soulseek peer browsing, immediate searches, downloads, backup, mirroring, or file duplication.

- [ ] **T-937**: Discography Concierge graph-density prioritization.
 - Status: planned
 - Priority: P2
 - Design: `docs/design/music-discovery-federation-plan.md`
 - Notes: Follow up T-930 by feeding Discovery Graph neighborhood density and stronger local-vs-mesh evidence into artist prioritization. Keep this separate from the first coverage/Wishlist implementation.

- [ ] **T-931**: Bloom-filter library diff.
 - Status: planned
 - Priority: P1
 - Design: `docs/design/music-discovery-federation-plan.md`
 - Notes: Add versioned MBID bloom snapshots for trusted contacts/pods/realms, compare inbound filters locally, and promote likely missing items to Wishlist. Do not publish filenames, paths, file hashes, exact holdings, or library exports.

- [x] **T-932**: Per-artist release radar.
 - Status: completed (2026-04-30)
 - Priority: P1
 - Design: `docs/design/music-discovery-federation-plan.md`
 - Notes: Added a conservative local artist-radar service/API with artist MBID subscriptions, muted release-group suppression, SongID-confirmed WorkRef observation validation, deterministic notification dedupe, DI registration, and focused tests. This is network-presence radar only; it does not poll MusicBrainz, browse peers, search Soulseek, or download files.

- [ ] **feature**: Persist artist release radar subscriptions and notifications.
 - Status: planned
 - Priority: P2
 - Notes: Follow up T-932 by storing artist-radar subscriptions, muted release groups, seen-observation keys, and notifications across daemon restarts.

- [ ] **feature**: Route signed artist radar observations through trusted federation/realm channels.
 - Status: planned
 - Priority: P2
 - Notes: Follow up T-932 by emitting and accepting signed SongID-confirmed WorkRef observations through LibraryActor outboxes or realm-scoped mesh messages, preserving user-controlled trust scope and rate limits.

- [ ] **feature**: Add Web UI controls for artist release radar.
 - Status: planned
 - Priority: P2
 - Notes: Follow up T-932 with artist watch/mute controls, unread notification review, and explicit actions to promote radar hits to Wishlist or Discovery Graph.

- [x] **T-933**: Federated taste recommendations.
 - Status: completed (2026-04-30)
 - Priority: P2
 - Design: `docs/design/music-discovery-federation-plan.md`
 - Notes: Added a local recommendation service/API over accepted inbound music WorkRefs from the ActivityPub inbox. The service filters to followed federation actors, groups candidates by MusicBrainz ID or normalized artist/title/year, enforces the default two-trusted-source reveal threshold before returning recommendations, and hides source actor IDs unless explicitly requested.

- [ ] **feature**: Add a Web UI surface for federated taste recommendations.
 - Status: planned
 - Priority: P2
 - Notes: Follow up T-933 by showing privacy-filtered recommendation cards with evidence reasons and actions to explore in Discovery Graph or promote to Wishlist. Keep peer names hidden unless policy explicitly allows reveal.

- [x] **T-934**: Realm-curated subject indexes.
 - Status: completed (2026-04-30)
 - Priority: P2
 - Design: `docs/design/music-discovery-federation-plan.md`
 - Notes: Added a signed realm subject-index artifact model, trusted-governance-root validation, safe WorkRef/evidence checks, in-memory registry, and recording-MBID resolver that returns realm/index/revision provenance for ShadowIndex and VirtualSoulfind callers. Publication, proposal/review workflow, and UI conflict display remain separate follow-ups.

- [ ] **feature**: Add governance proposal flow for realm subject indexes.
 - Status: planned
 - Priority: P2
 - Notes: Follow up T-934 by routing subject-index revisions through existing realm governance proposal/review/accept/reject flows instead of storing locally authored revisions directly.

- [ ] **feature**: Add UI conflict display for realm subject indexes.
 - Status: planned
 - Priority: P2
 - Notes: Show when subscribed realm indexes disagree with MusicBrainz or another realm, preserving provenance and allowing users to disable individual realm authorities.

- [x] **T-935**: Decentralized MusicBrainz edit overlay.
 - Status: completed (2026-04-30)
 - Priority: P2
 - Design: `docs/design/music-discovery-federation-plan.md`
 - Notes: Added signed local MusicBrainz overlay-edit artifacts, evidence validation, deterministic in-memory storage, read-time overlay application for artist release graphs, and a dedicated overlay API that returns original/effective graphs plus provenance without mutating cached upstream MusicBrainz payloads. Mesh/realm gossip and upstream MusicBrainz export remain separate follow-ups.

- [ ] **feature**: Gossip MusicBrainz overlay edits through trust-scoped mesh/realm channels.
 - Status: planned
 - Priority: P2
 - Notes: Follow up T-935 by routing signed overlay edits through mesh service fabric or realm channels according to user trust scope. Keep publication opt-in and preserve source provenance.

- [x] **feature**: Add manual upstream MusicBrainz export review for overlay edits.
 - Status: completed (2026-04-30)
 - Priority: P2
 - Notes: Added a MusicBrainz overlay export review API that turns stored signed overlay edits into manual upstream submission packages with target, proposed change, and evidence. Added explicit local export approval records with safe approver validation and idempotent approvals. This does not auto-submit edits upstream or mutate cached MusicBrainz data.

- [x] **T-936**: Quarantine Jury.
 - Status: completed (2026-04-30)
 - Priority: P2
 - Design: `docs/design/music-discovery-federation-plan.md`
 - Notes: Added a local Quarantine Jury service/API for user-initiated trusted jury requests, safe opaque evidence validation, signed juror verdict intake, duplicate juror replacement, and two-thirds recommendation aggregation. This first slice does not send files, route mesh messages, release quarantined content, or involve unselected peers.

- [x] **feature**: Persist Quarantine Jury requests and verdicts.
 - Status: completed (2026-04-30)
 - Priority: P2
 - Notes: Quarantine Jury requests and signed verdicts now persist to an atomic JSON state file under the app directory and reload on service startup. Focused tests cover rehydrating requests, verdicts, and aggregate recommendations from persisted state.

- [x] **feature**: Route Quarantine Jury requests through trust-scoped mesh channels.
 - Status: completed (2026-04-30)
 - Priority: P2
 - Notes: Quarantine Jury can now dispatch minimal request evidence through PodCore only to selected safe jurors, records route attempts with routed/failed juror lists, persists the dispatch history, and exposes route dispatch/history endpoints. It does not attach raw files, expand the audience automatically, or change local quarantine state.

- [x] **feature**: Add manual Quarantine Jury review API and accept flow.
 - Status: completed (2026-04-30)
 - Priority: P2
 - Notes: Added a persisted manual review/acceptance contract that returns request evidence, signed verdicts, route attempts, aggregate recommendations, acceptance eligibility, and prior acceptance decisions. Accepting is allowed only for release-candidate supermajorities, is idempotent, validates safe operator identifiers, and records a local decision without mutating quarantine state.

- [ ] **feature**: Add frontend Quarantine Jury review UI.
 - Status: planned
 - Priority: P2
 - Notes: Build the browser review surface on top of the Quarantine Jury review/acceptance API, including verdict evidence, dissent, route attempts, acceptance status, and tooltip-backed manual accept controls. Keep local quarantine authoritative until the user explicitly accepts a release-candidate recommendation.

- [x] **bug**: Keep mesh-overlay sources out of Soulseek sequential failover.
 - Status: completed (2026-04-30)
 - Priority: P1
 - Notes: Mixed source sets now filter the sequential failover candidate list to raw Soulseek peers before calling `ISoulseekClient.DownloadAsync`, preventing mesh-overlay descriptors from being treated as Soulseek usernames. Added regression coverage and documented the gotcha in ADR-0001.

- [x] **feature**: Add Layer 1 pod listening parties and persistent web playback.
 - Status: completed (2026-04-30)
 - Priority: P2
 - Notes: Documented the metadata-only listen-along protocol and opt-in global radio registry, added a persistent Web UI player that streams existing `ContentId` values through `/api/v0/streams/{contentId}`, wired player actions into Now Playing, added collection item play controls, and added pod listen-along host/follow controls backed by stored/routed pod messages plus SignalR fan-out. Listed parties publish a mesh/DHT-backed directory entry, and the separate Mesh Streaming toggle exposes the host's integrated slskdN radio stream endpoint for the active track. Deferred live mic/WebRTC audio broadcast remains out of scope.

- [x] **ux**: Integrate pods, rooms, chats, and contacts as durable social surfaces.
 - Status: completed (2026-04-30)
 - Priority: P1
 - Notes: Chat now rehydrates saved server conversations, Rooms reopens joined daemon rooms, Contacts provide chat/browse actions, and Pods supports create/discover/save flows with daemon-backed persistence instead of browser-only dead ends.

- [x] **security**: Harden audited app security boundaries.
 - Status: completed (2026-04-30)
 - Priority: P1
 - Notes: Required auth for ActivityPub outbox publishing, guarded HTTP share backfill URLs with SSRF checks plus redirect/size controls, fixed sibling-prefix path authorization for file listing, removed unsupported query-string API-key CSRF bypasses, and stopped SignalR API-key promotion from building a secondary service provider.

- [x] **bug**: Restart already-running `slskd.service` after AUR upgrades.
 - Status: completed (2026-04-30)
 - Priority: P1
 - Notes: AUR `post_upgrade()` now runs `systemctl try-restart slskd.service` after user/systemd reload setup, so active daemons move to the upgraded payload without auto-starting fresh or stopped installs. AUR README documents the install-vs-upgrade behavior.

- [x] **ux**: Fix Web UI header and footer chrome alignment.
 - Status: completed (2026-04-29)
 - Priority: P1
 - Notes: Split the top navigation into primary-route and utility-action rails, reordered the utility cluster as Connected, Theme, System, Log Out, removed the always-highlighted Theme trigger, and rebuilt the fixed footer as brand, speed, and network/transport rails. Live `local test host` desktop and narrow viewport checks show no vertical overflow.

- [x] **feature**: Add a downloads-section toggle for conservative accelerated downloads.
 - Status: completed (2026-04-29)
 - Priority: P1
 - Notes: Added a runtime Downloads header toggle that gates underperformance-triggered rescue acceleration. Normal Soulseek downloads remain single-source unless they are slow/stalled enough for rescue; raw Soulseek alternate sets use verified sequential failover, while true multipart chunking remains limited to trusted mesh-overlay peers. Discovery hash probes now share the persistent per-peer daily verification budget, and explicit swarm downloads default to verification enabled. Updated README, multipart-downloads, and changelog documentation for the toggle and policy.

- [x] **compat**: Re-implement post-0.25 upstream compatibility gap plan without copying upstream diffs.
 - Status: completed (2026-04-29)
 - Priority: P1
 - Notes: Added dual config schema support for `transfers`/legacy `global`, `integrations`/legacy `integration`, and group upload-nested limits with startup compatibility warnings. Added regex username blacklist patterns, fixed Search Again payload mapping, made web metadata paths subpath-safe, clamped direct-download retry max delay to 30s, covered YAML reload regression behavior, verified no SignalR typed hub exception catch pattern remains, and added fork guidance.

- [x] **ux**: Make public DHT exposure notice dismissable and fix false no-peer diagnostics.
 - Status: completed (2026-04-29)
 - Priority: P1
 - Notes: `local test host` showed healthy DHT status counters (`nodes=155`, `discovered=37`, `activeMesh=1`) while the Network dashboard could still warn from empty mesh/discovered list endpoints. The dashboard now treats DHT status counters as peer evidence and shows public-DHT exposure as a one-time dismissable info notice.

- [x] **test**: Stabilize two-node DHT rendezvous full-instance overlay connect coverage.
 - Status: completed (2026-04-29)
 - Priority: P1
 - Notes: The live full-instance mesh test could fail on a transient `/api/v0/overlay/connect` `502` in full integration runs even though it passed by itself. The two-node DHT rendezvous tests now wait through transient connect readiness failures, and `TwoNodeMeshFullInstanceTests` passes `3/3`.

- [x] **security**: Resolve open dependency and CodeQL security alerts.
 - Status: completed (2026-04-29)
 - Priority: P1
 - Notes: Applied the open Dependabot bump PRs for NuGet and npm, explicitly upgraded vulnerable OpenTelemetry packages to `1.15.3`, upgraded npm `uuid` to `14.0.0`, and removed cleartext legacy overlay certificate password reads that kept CodeQL alert `2550` open.

- [x] **release**: Prepare `2026042900-slskdn.195` for LAN-only DHT warning fix.
 - Status: completed (2026-04-29)
 - Priority: P1
 - Notes: Promoted the Network dashboard LAN-only DHT warning fix into a `.195` changelog section and validated generated release notes before pushing the tag-only release.

- [x] **bug**: Fix false public DHT warning for LAN-only nodes.
 - Status: completed (2026-04-29)
 - Priority: P1
 - Notes: The backend reports `LanOnly` as `lanOnly`, while the Network dashboard checked only `isLanOnly`, causing `dhtRendezvous.lanOnly: true` nodes to show the public exposure warning. DHT status normalization and Network coverage now accept both field names.

- [x] **release**: Prepare `2026042900-slskdn.194` for AUR source build fix.
 - Status: completed (2026-04-29)
 - Priority: P1
 - Notes: Promoted the AUR source date-version build fix into a `.194` changelog section and validated generated release notes before pushing the tag-only release.

- [x] **bug**: Fix AUR source install for `2026042900-slskdn.193`.
 - Status: completed (2026-04-29)
 - Priority: P1
 - Notes: Diagnosed the visible CS8981 output as non-fatal generated-code warnings and reproduced the real failure as MSBuild rejecting generated assembly version `2026042900.193.0.0`. The source PKGBUILD now maps date-based public releases to `0.0.0-slskdn.YYYYMMDDmm.NNN` for `Version`/`PackageVersion`, keeps `InformationalVersion=YYYYMMDDmm-slskdn.NNN`, and bumps the AUR source package to `pkgrel=2`. Live AUR `slskdn` was published as commit `b14afe2`.

- [x] **ux**: Rebrand the default Web UI dark theme.
 - Status: completed (2026-04-29)
 - Priority: P2
 - Notes: Confirmed the old dark palette matched upstream `0.24.5`, added slskdN as the default brown/gray/purple theme, and kept Classic Dark plus Light selectable from the Theme menu.

- [x] **docs**: Clarify slskdN copyright, branding, and fork attribution.
 - Status: completed (2026-04-29)
 - Priority: P2
 - Notes: Added slskdN-first unofficial-fork attribution across docs, web metadata, API/package surfaces, release generators, and support links while documenting compatibility names that should remain `slskd`.

- [x] **release**: Prepare `2026042900-slskdn.191` for Docker UID/GID collision fix.
 - Status: completed (2026-04-29)
 - Priority: P1
 - Notes: `2026042900-slskdn.190` failed Docker publishing because the runtime image assumed UID/GID `1000:1000` was available. Docker now creates the internal `slskdn` placeholder user/group with system-allocated IDs and the packaging validator rejects fixed Docker `1000` user/group creation.

- [x] **release**: Prepare `2026042900-slskdn.190` for post-rollback alignment changes.
 - Status: completed (2026-04-29)
 - Priority: P1
 - Notes: Promoted the Docker runtime, packaging validation, direct-download retry/resume, transfer batch metadata, and IPv4-mapped address normalization changes into a new date-versioned stable release section. This remains on the slskd 0.24.5 license-compliance rollback base and keeps the `YYYYMMDDmm-slskdn.###` public version shape.

- [x] **release**: Switch corrective rollback release to `YYYYMMDDmm-slskdn.###` versioning.
 - Status: completed (2026-04-29)
 - Priority: P1
 - Notes: Prepared `2026042900-slskdn.189` so downstream package managers sort the license-compliance rollback newer than removed `0.25.1-slskdn.*` packages without implying upstream slskd `0.26`. Release notes, tag scanning, tag-build publishing, local build/publish scripts, and stable metadata update patterns now understand the public date-based version while mapping MSBuild/NuGet inputs to `0.0.0-slskdn.2026042900.189`.

- [x] **release**: Backport release-critical fixes onto the 0.24.x rollback branch.
 - Status: completed (2026-04-29)
 - Priority: P1
 - Notes: Selectively carried forward the post-rollback fixes needed for stable 0.24.x releases without pulling 0.25.x sync content: release-note generation now refuses oversized synthesized commit dumps, tag publishing no longer waits on pre-publish Nix smoke for unpublished assets, runtime YAML binding honors public aliases like `dht:`, directory browse peer timeouts return controlled `503` responses, shutdown-wrapped download cancellations are classified before error logging, and empty cached user groups resolve to built-in groups. Focused unit validation passed for YAML alias binding and user-group fallback.

- [x] **ux**: Publish mesh search results before Soulseek timeout completion.
 - Status: completed (2026-04-24)
 - Priority: P2
 - Notes: Issue `#209` tester follow-up showed a `beatles` mesh result at `09:22:39`, but the user-facing search completed at `09:22:54` because final result publication waited for the Soulseek timeout. `SearchService` now starts a mesh publication task that persists and broadcasts merged mesh/pod results as soon as the overlay response arrives, while the Soulseek search continues. The search detail page now refetches responses when early result counts appear instead of waiting only for `isComplete`. The gotcha is documented in ADR-0001.

- [x] **bug**: Normalize AUR release payload permissions after zip staging.
 - Status: completed (2026-04-24)
 - Priority: P1
 - Notes: AUR user feedback for `0.24.5.slskdn.177-1` showed `/usr/lib/slskd/releases/0.24.5.slskdn.177/` installed as `drwx------ root root`, preventing startup through systemd or any non-root user. The binary/dev PKGBUILDs extract into a `mktemp -d` staging directory and copy with archive-preserving semantics, so the `0700` staging mode could leak onto the release root. `PKGBUILD`, `PKGBUILD-bin`, and `PKGBUILD-dev` now normalize release payload permissions with `chmod -R u=rwX,go=rX "${release_root}"` and explicitly set the apphost to `755`; packaging metadata validation locks this in. Local package-function smokes for source, binary, and dev AUR paths all produced `0755` release roots. Published the immediate AUR `slskdn-bin` repair as `0.24.5.slskdn.177-2`.

- [x] **bug**: Stage AUR binary packages directly from the downloaded release zip.
 - Status: completed (2026-04-23)
 - Priority: P1
 - Notes: Investigated the live `slskdn-bin 0.24.5.slskdn.175-1` Manjaro report about missing `Microsoft.AspNetCore.Diagnostics.Abstractions`. The published `0.24.5-slskdn.175` Linux x64 release zip was intact and self-contained, so the bug was isolated to the AUR binary packaging path. `PKGBUILD-bin` and `PKGBUILD-dev` now mark the zip source as `noextract`, unzip the downloaded archive explicitly during `package()`, and fail the build if `slskd`, `slskd.deps.json`, or `Microsoft.AspNetCore.Diagnostics.Abstractions.dll` are missing from the staged payload. Added the gotcha to ADR-0001, updated the AUR README/changelog, and tightened packaging metadata validation to lock the new staging path in place. Validation passed for packaging metadata, `git diff --check`, and a direct smoke of the real `0.24.5-slskdn.175` release zip; repo-wide `dotnet test` still has unrelated environment-sensitive DNS/wildcard failures in `SolidFetchPolicyTests` and `DestinationAllowlistTests`.

- [x] **bug**: Triage issue `#209` on `local test host` and quiet app-side live noise.
 - Status: completed (2026-04-22)
 - Priority: P1
 - Notes: `local test host` is now on manual diagnostic build `0.24.5-slskdn.174+manual.0214ccc8b`, active under systemd with `NRestarts=0`, Soulseek logged in, shares ready, DHT running, and overlay listening on `50305`. The mesh population is thin/unreliable rather than absent: DHT discovers peers, but overlay attempts mostly fail by timeout/no-route and the latest sample had `0` active mesh connections. Normal Soulseek search works after removing auto-replace budget contention: with `searchTimeout=10000`, user/API searches returned responses for `radiohead`, `pink floyd`, and `nirvana`, while `beatles` timed out with zero; after the timeout conversion fix, the documented `searchTimeout=10` also returned `radiohead` results. Fixed app-side defects found in the pass: common remote transfer rejections now classify as expected peer policy instead of fake fatal unobserved tasks, circuit maintenance no longer runs automatic placeholder circuit-building probes against live peers, background auto-replace uses an `auto-replace` safety source instead of the `user` bucket, search completion logs include source-specific response counts, and the API/discovery search timeout units are patched. Gotchas are documented in ADR-0001.

- [x] **ux**: Reduce SongID results duplication and diagnostic scroll fatigue.
 - Status: completed (2026-04-21)
 - Priority: P2
 - Notes: Headless UX testing against `local test host` with a YouTube URL showed repeated track/options/actions, duplicate graph/atlas controls, and low-value diagnostics dominating the result flow. The SongID panel now promotes the likely track and deduped best actions, collapses duplicate candidates with match counts, and moves raw diagnostic sections behind disclosure rows.

- [x] **test**: Isolate static event subscriber-count lifecycle tests from xUnit parallelism.
 - Status: completed (2026-04-21)
 - Priority: P1
 - Notes: Release tag `build-main-0.24.5-slskdn.173` failed `ApplicationLifecycleTests.Dispose_UnsubscribesGlobalAndSoulseekEvents` because static event invocation-count assertions can race with other tests touching the same global events. Static event tests now share a non-parallel xUnit collection, and the full Release unit suite passes.

- [x] **bug**: Match live Soulseek timer-reset stack signatures in false-fatal classifier.
 - Status: completed (2026-04-21)
 - Priority: P1
 - Notes: The `0.24.5-slskdn.169` `local test host` route/tab sweep exposed a current-process fatal unobserved `NullReferenceException` from `Soulseek.Extensions.Reset(Timer timer)` inside `Soulseek.Network.Tcp.Connection.WriteInternalAsync(...)`. The existing classifier matched the synthetic test string `Reset(Timer)`, missing the real runtime signature with parameter names. The classifier now matches the stable `Reset(` method prefix and focused tests use the live signature. The gotcha is documented in ADR-0001.

- [x] **bug**: Quiet normal systemd shutdown telemetry from package restarts.
 - Status: completed (2026-04-21)
 - Priority: P2
 - Notes: The `0.24.5-slskdn.169` package replacement on `local test host` shut down cleanly, but the old process still logged SIGTERM/host-stop warnings, duplicate expected `ProcessExit` stderr, and `app.Run() returned (this should not happen normally)`. Clean shutdown now logs at information/debug levels without duplicate fatal-looking stderr. The gotcha is documented in ADR-0001.

- [x] **bug**: Quiet optional user-info badge misses in route/tab sweeps.
 - Status: completed (2026-04-21)
 - Priority: P3
 - Notes: The post-release `local test host` route/tab sweep showed the remaining browser-visible noise was optional user badge requests for offline historical download users. `UserCard` now asks `/api/v0/users/{username}/info?quietUnavailable=true`, and expected offline/unavailable peer data returns `204 No Content` only for that optional mode; default endpoint semantics remain unchanged. The gotcha is documented in ADR-0001.

- [x] **bug**: Discover app target framework in E2E and integration launchers.
 - Status: completed (2026-04-21)
 - Priority: P2
 - Notes: The scheduled E2E run fell back to `dotnet run` because the harness hardcoded `bin/Release/net8.0` while the app targets `net10.0`, which made startup timing flaky. The Playwright harness and invalid-config integration test launcher now read `<TargetFramework>` from `src/slskd/slskd.csproj` and use the matching build output. The gotcha is documented in ADR-0001.

- [x] **bug**: Return controlled non-500 responses for unavailable Soulseek user info.
 - Status: completed (2026-04-21)
 - Priority: P2
 - Notes: Controlled Playwright crawling of live user/search links on `local test host` showed `/api/v0/users/{username}/info` returning HTTP 500 for expected peer connection failures and timeouts. The info endpoint now keeps offline users as 404 but returns a generic 503 for unavailable peer info without stack-noise logging. The gotcha is documented in ADR-0001.

- [x] **bug**: Pace auto-replace searches instead of failing whole stuck-download batches on the Soulseek safety limiter.
 - Status: completed (2026-04-21)
 - Priority: P1
 - Notes: Live `local test host` soak showed auto-replace issuing a large stuck-download batch until `Search rate limit exceeded`, then logging repeated stack traces and recording `128 failed`. Alternative searches are now paced by `Soulseek.Safety.MaxSearchesPerMinute`, search-budget exhaustion defers the current item and stops the cycle early, and focused unit coverage locks in the behavior. The gotcha is documented in ADR-0001.

- [x] **bug**: Exclude generated app publish output from future Web SDK publish artifacts.
 - Status: completed (2026-04-21)
 - Priority: P1
 - Notes: Manual publish output under `src/slskd/dist` was ignored by git but still visible to `Microsoft.NET.Sdk.Web` default item discovery, so later publish artifacts could contain stale nested `dist` payloads. Added the gotcha to ADR-0001 and excluded `dist/**` from the app project's default items.

- [x] **bug**: Demote routine auto-replace large-batch no-result progress from information logs.
 - Status: completed (2026-04-21)
 - Priority: P2
 - Notes: The live paced cycle on `local test host` fixed the rate-limit flood but still emitted per-track `Searching` / `Found 0` progress at `Information` across a 128-item stuck batch. Routine per-track search/no-result progress is now `Debug`, while successful candidate discovery and aggregate cycle summaries remain visible. The gotcha is documented in ADR-0001.

- [x] **bug**: Quiet expected remote-offline download failures during restart re-enqueue.
 - Status: completed (2026-04-21)
 - Priority: P2
 - Notes: Fresh `local test host` restart validation re-enqueued downloads from offline user `icetre` and emitted repeated `UserOfflineException` / `TransferException` stack traces. These are expected remote peer outcomes, so download and observer paths now log warning summaries without stacks while still failing the transfer records. The gotcha is documented in ADR-0001.

- [x] **bug**: Treat auto-replace shutdown cancellation as normal hosted-service stop flow.
 - Status: completed (2026-04-21)
 - Priority: P2
 - Notes: Manual deploys can stop the service while auto-replace is pacing or waiting for a search. That caller-token cancellation was caught as a generic search error and counted as failed replacement work. Auto-replace now rethrows caller-token cancellation and the background service stops cleanly without error stacks. The gotcha is documented in ADR-0001.

- [x] **bug**: Demote routine shared search progress during background auto-replace batches.
 - Status: completed (2026-04-21)
 - Priority: P2
 - Notes: The fixed `local test host` build reached a 142-item auto-replace cycle without errors, but each background search still produced `Information` progress from shared search infrastructure (`MeshSearch` no-peer fallback, search completion counts, and passive HashDb discovery). Those routine per-search progress logs are now `Debug`; aggregate auto-replace cycle logs remain visible. The gotcha is documented in ADR-0001.

- [x] **bug**: Avoid stack traces for the handled Soulseek disconnect race during shutdown.
 - Status: completed (2026-04-21)
 - Priority: P2
 - Notes: The app already caught Soulseek.NET's shutdown-time `Sequence contains no elements` disconnect race, but passed the exception object to Serilog and still printed a stack in the journal. The handled race is now logged as a debug summary without the exception object. The gotcha is documented in ADR-0001.

- [x] **bug**: Classify Soulseek TCP double-disconnect read-loop races as expected network churn.
 - Status: completed (2026-04-21)
 - Priority: P1
 - Notes: Live `local test host` monitoring caught a current-process fatal unobserved task from `Soulseek.Network.Tcp.Connection.Disconnect`: `An attempt was made to transition a task to a final state when it had already completed.` The global expected-network classifier now recognizes that Soulseek.NET read-loop teardown race and has focused unit coverage. The gotcha is documented in ADR-0001.

- [x] **bug**: Preserve spacing around inline code in the DHT exposure consent modal.
 - Status: completed (2026-04-21)
 - Priority: P3
 - Notes: Playwright inspection of `/system/network` showed the public DHT exposure consent copy rendering `dht.lan_only=truein` because JSX did not include explicit whitespace after the inline `<code>` element. The modal copy now renders with a space, and the gotcha is documented in ADR-0001.

- [x] **bug**: Treat remote Soulseek enqueue rejections as expected network churn in the unobserved-task handler.
 - Status: completed (2026-04-19)
 - Priority: P1
 - Notes: Manual `local test host` validation still showed `[FATAL] Unobserved task exception ... Enqueue failed due to internal error` after the download service had already classified the transfer as `Completed, Rejected`. Added `Soulseek.TransferRejectedException` plus the exact enqueue-failure signature to the expected Soulseek network classifier, added focused coverage, and documented the gotcha in ADR-0001.

- [x] **bug**: Make source-ranking download history updates atomic under concurrent transfer events.
 - Status: completed (2026-04-19)
 - Priority: P1
 - Notes: Live manual-build validation on `local test host` exposed `SQLite Error 19: UNIQUE constraint failed: DownloadHistory.Username` while concurrent transfer completion/failure handlers recorded source-ranking history. Replaced EF read-then-insert/update with a single SQLite `INSERT ... ON CONFLICT DO UPDATE` counter upsert, added concurrent regression coverage, and documented the gotcha in ADR-0001.

- [x] **bug**: Allow API-key access to DHT rendezvous diagnostics.
 - Status: completed (2026-04-19)
 - Priority: P1
 - Notes: Live `local test host` validation showed configured API keys worked for `/api/v0/session` and `/api/v0/searches` but not `/api/v0/dht/status` or `/api/v0/overlay/stats`, because `DhtRendezvousController` used bare `[Authorize]` and fell through to bearer-only auth. Updated the controller to `AuthPolicy.Any`, added reflection coverage, and documented the gotcha in ADR-0001.

- [x] **security**: Resolve the remaining Dependabot alert without suppressions.
 - Status: completed (2026-04-19)
 - Priority: P1
 - Notes: GitHub had no open Dependabot PRs, but one open Dependabot security alert remained for `OpenTelemetry.Exporter.Jaeger` in `src/slskd/slskd.csproj`. Removed the deprecated vulnerable Jaeger exporter package instead of ignoring it, kept `exporter: jaeger` compatibility by routing Jaeger collector exports through the supported OTLP exporter, bumped `AWSSDK.S3` to `4.0.21.2`, refreshed npm lockfiles for active Dependabot-managed ranges, and verified the main project has no vulnerable or outdated NuGet packages in current sources.

- [x] **bug**: Fix reciprocal overlay lifecycle so DHT-ready peers can answer mesh search RPCs.
 - Status: completed (2026-04-19)
 - Priority: P1
 - Notes: Issue `#209` build `152` showed DHT discovery and 9 active peers but `0 onion-capable` and `0 responses` because reciprocal overlay dialing could replace/dispose the only live read loop and outbound sockets never processed incoming pings or mesh RPCs. The registry now keeps separate inbound and outbound connections per username, outbound connections run a full message loop, and mesh search responses are correlated through `MeshOverlayRequestRouter` so only one reader owns each TLS stream. Regression coverage now proves repeated `MeshOverlaySearchService` searches work over the same real outbound overlay connection and leave it connected.

- [x] **bug**: Sanitize DHT/overlay usernames and public endpoints in logs.
 - Status: completed (2026-04-18)
 - Priority: P1
 - Notes: Issue `#209` tester logs exposed raw mesh usernames and public endpoints because DHT rendezvous used `hello.Username`, `ack.Username`, `connection.Username`, and raw `IPEndPoint` values directly in logger calls. Added `OverlayLogSanitizer`, wired DHT/overlay logging through it, and added unit coverage for username/peer-id/public-endpoint redaction.

- [x] **bug**: Keep quiet mesh neighbors connected and usable after issue `#209` build `151`.
 - Status: completed (2026-04-18)
 - Priority: P1
 - Notes: Tester logs showed DHT ready and Soulseek logged in, then an inbound mesh neighbor disconnected exactly 30 seconds later with `OperationCanceledException` from the overlay read loop. The server now treats per-read idle timeout as a keepalive interval instead of a fatal loop error, peers advertise their overlay listener in HELLO/ACK so inbound-only neighbors can be promoted through a reciprocal outbound connection with a configured-port fallback for old peers, and registry cleanup is identity-safe so stale inbound disposal cannot remove the replacement outbound connection. Focused unit coverage and the two-full-instance mesh smoke reproduce the old timing window and prove the nodes remain connected past `OverlayTimeouts.MessageRead`.

- [x] **bug**: Keep ScenePodBridge opt-in so normal searches stay Soulseek-compatible.
 - Status: completed (2026-04-18)
 - Priority: P1
 - Notes: Issue `#209` testing on build `149` showed DHT and Soulseek login healthy but popular searches returning `0` through `[ScenePodBridge]`. `Feature.ScenePodBridge` now defaults to `false`, `/api/slskdn/capabilities` only advertises `scene_pod_bridge` when explicitly enabled, and the Web UI no longer flips bridge providers on from generic capability success.

- [x] **test**: Add deterministic two-instance mesh smoke for DHT overlay validation.
 - Status: completed (2026-04-18)
 - Priority: P1
 - Notes: Added a full-process integration proof that starts two isolated `slskd` subprocesses, forces alpha to connect to beta through the real TCP/TLS/HELLO overlay stack, and waits for both overlay connections plus peer inventory to show the live neighbor. The harness now passes `--app-dir`, disables HTTPS, overrides every bound listener, emits the runtime `dhtRendezvous` binder key, and leaves a gitignored local-account env scaffold for future public Soulseek account smokes.

### Fastest Release Path

- 1. Close the last SongID backend output gaps that materially change ranking or evidence reuse.
- 2. Cover those outputs with direct API/service tests before spending more time on UI polish.
- 3. Limit Discovery Graph work to release-visible wins: dedicated atlas, stronger explanations, and seed/navigation coherence.
- 4. Treat repo-wide lint debt as separate from the SongID / Discovery Graph release path unless explicitly pulled in.
- 5. Keep release-gate regression tests deterministic; avoid real-time cancellation races that pass locally and fail on GitHub runners.
- 6. Audit every remaining `AllowAnonymous` controller individually; only true read-only or protocol-required surfaces should stay public.
- 7. Add a dedicated regression test for intentionally-public protocol endpoints (`ActivityPub`, `WebFinger`, streaming token access, session login/enabled, public profile lookup) so the allowed anonymous surface is documented in code too.

- [ ] **chore**: Align frontend peer dependency ranges after the Vite 8 security bump.
 - Status: pending
 - Priority: P3
 - Notes: The March 2026 security dependency refresh cleared `npm audit`, but `@vitejs/plugin-react` still advertises support through Vite 7 and `@vitest/coverage-v8` still peers against the older Vitest line. Follow up with a deliberate toolchain alignment pass instead of mixing peer-range churn into security-only fixes.

- [ ] **bug**: Trace the still-hanging full `dotnet test -v minimal` tail after the main suites report passing.
 - Status: pending
 - Priority: P2
 - Notes: The April 14 dependency/release chore pass restored green release-gate validation, but a broad `dotnet test -v minimal` run in this environment still stops returning output after the unit suite reports passing counts. The release gate and focused smoke slices pass, so this remains a separate harness/cleanup problem to isolate before relying on the broad solution-level test command as a hard release signal.

- [x] **bug**: Retry failed DHT overlay candidates after backoff instead of only on first discovery.
 - Status: completed (2026-04-18)
 - Priority: P1
 - Notes: `DhtRendezvousService` no longer uses `_discoveredPeers.TryAdd(...)` as the once-ever trigger for outbound overlay connect attempts. Discovery cache, in-flight tracking, and retry timing are now separate, with a 5-minute backoff before re-attempting unverified peers. Validated with focused unit tests and on `local test host`, where the same discovered-peer set advanced from `26` to `31` total connection attempts after a post-backoff forced discovery instead of remaining stuck at the first-attempt count.

- [ ] **bug**: Filter or deprioritize non-overlay DHT candidates before repeated overlay retries.
 - Status: pending
 - Priority: P1
 - Notes: Live `local test host` validation on 2026-04-18 now exposes classified outbound overlay failures. The current post-fix state is mostly `connectTimeout` plus occasional `noRoute`, and the discovered candidate set is still heavy on likely-bad `:50306` endpoints. Follow up by deciding whether to suppress obviously non-overlay candidates, add longer-lived backoff, or enrich DHT metadata so the connector does not keep retrying junk endpoints.

- [x] **security**: Add focused unit coverage for username lockout and share-token audience binding.
 - Status: completed (2026-04-18)
 - Priority: P2
 - Notes: Added focused unit coverage for per-username login lockout, share-token audience/collection binding, the Chromaprint PCM buffer cap helper, and updated stale security startup tests to the current `SecurityOptions` registration contract so the security regression slice now compiles and catches the intended abuse cases.

- [x] **chore**: Add a reproduce-first verification workflow for tester-reported bugfix releases.
 - Status: completed (2026-04-14)
 - Priority: P1
 - Notes: Added `docs/dev/bugfix-verification-checklist.md` and wired it into the release checklist, testing policy, and ADR-0004 so reported bugs must be split into concrete acceptance checks, reproduced or explicitly marked as unverified mitigations, and re-run on the same path before a tag build is described as a fix.

- [x] **chore**: Remove stale Dependabot suppressions and tighten dependency holds to real blockers only.
 - Status: completed (2026-04-14)
 - Priority: P1
 - Notes: Removed the dead `react-scripts` ignore, kept only actual framework/runtime blockers in `.github/dependabot.yml`, pinned `@uiw/react-codemirror` to the last React-16-compatible `4.21.21` so the lockfile no longer drifts to the React-17-only `4.25.x` line, and moved the web lint toolchain onto the compatible ESLint 9 flat-config path required by `eslint-config-canonical 47.4.2`.

- [x] **chore**: Restore green web lint after the ESLint 9 migration.
 - Status: completed (2026-04-15)
 - Priority: P1
 - Notes: Replaced the broken web ESLint 9 setup with an explicit flat config for app/test code, added direct `eslint-plugin-react-hooks` and `eslint-plugin-promise` deps, fixed the stale `searches.createBatch(...)` import in `Search/Response.jsx`, fixed the `Explorer.jsx` `+`/`??` precedence bug, and documented both gotchas in ADR-0001.

- [x] **chore**: Align tagged release workflows with `.NET 10` and add clearer Linux asset aliases.
 - Status: completed (2026-04-17)
 - Priority: P1
 - Notes: Updated all GitHub Actions `DOTNET_VERSION` pins and the Dockerfile build/runtime images to `.NET 10`, fixed Matrix release-message redaction in `build-on-tag.yml` to use `PUT`, and added additive `linux-glibc-*` release zip aliases while preserving the existing `slskdn-main-*` and versioned asset names used by packaging and downstream automation.

- [x] **chore**: Remove duplicate stable release zip names and standardize Linux assets on explicit glibc identifiers.
 - Status: completed (2026-04-17)
 - Priority: P1
 - Notes: Stopped publishing duplicate version-named stable zip assets, standardized Linux release artifacts on `linux-glibc-*`, and updated packaging/workflows to consume the explicit names directly while keeping limited fallback download logic only where older releases still need it.

- [x] **chore**: Repair the `build-main-0.24.5-slskdn.135` package pipeline regressions.
 - Status: completed (2026-04-17)
 - Priority: P1
 - Notes: Realigned stable metadata and packaging to the published `linux-glibc-*` assets on `0.24.5-slskdn.135`, fixed the COPR RPM spec/source filename mismatch, repaired the stable metadata updater so it no longer corrupts Flatpak/Chocolatey/Debian files, and updated `Dockerfile` to real `.NET 10 noble` base images so local Docker, Nix smoke, and packaging validation now match the tagged release workflow.

- [ ] **chore**: Add a heavier share-scan regression harness for tester issue `#193`.
 - Status: completed (2026-04-08)
 - Priority: P2
 - Notes: Added `ShareScannerHarnessTests` plus `scripts/run-share-scan-harness.sh`. The automated harness scans a large synthetic temp tree and asserts completion/index counts without hash computation. The manual harness accepts `SLSKDN_SHARE_SCAN_ROOT` so local runs can target real storage such as the tester-like NFS path.

- [ ] **bug**: Reduce or defer media-attribute probing during share scans on slow/remote storage.
 - Status: pending
 - Priority: P1
 - Notes: The new manual share-scan harness reproduces `#193` on the NFS-backed `/mnt/datapool_lvm_media/download/music` tree with `workers=1`: the scan times out after 60s having indexed only 8 files. The same harness passes when `SLSKDN_SHARE_SCAN_SKIP_MEDIA_ATTRIBUTES=1`, which strongly implicates `SoulseekFileFactory` / `TagLib.File.Create(...)` media-attribute extraction as the remaining stall source.

- [ ] **bug**: Trace and contain `#201` transfer-path `Connection refused` unobserved task exceptions.
 - Status: pending
 - Priority: P1
 - Notes: The startup listener race is fixed, the blanket benign-refusal suppression is removed, startup patching now configures `incomingConnectionOptions`, and `DownloadService.EnqueueAsync(...)` no longer aborts on an unnecessary `GetUserEndPointAsync(...)` / `ConnectToUserAsync(...)` peer preflight. The release smoke script now includes focused startup/transfer unit regressions in addition to the versioned integration slice. Remaining work is to trace any still-live upload-side or lower-level Soulseek `Connection.ConnectAsync(...)` refusals if testers can reproduce them after those fixes, and add focused coverage around the actual producer instead of the global unobserved-task handler.

- [x] **bug**: Stop empty permission defaults from hard-failing Linux downloads.
 - Status: completed (2026-04-18)
 - Priority: P1
 - Notes: `permissions.file.mode` defaults to an empty string to mean "use the OS umask", but `FileService.CreateFile(...)` and `MoveFile(...)` were still parsing that empty default as a chmod string. Both paths now only parse a configured non-whitespace mode, with focused unit coverage proving unset permissions no longer abort download file creation or move handling.

- [x] **bug**: Queue and dedupe Transfers bulk actions instead of running them inline.
 - Status: completed (2026-04-18)
 - Priority: P1
 - Notes: Transfers bulk retry/remove/cancel now enqueue work into a background queue that drains one request at a time, dedupes identical queued or in-flight operations, preserves the dedicated `clearCompleted` path for top-level remove-all-completed, and aggregates failures once per batch instead of once per file. Focused web tests cover sequential draining, duplicate bulk-submission suppression, single-toast failure reporting, and deduped clear-completed behavior.

- [ ] **T-919**: Discovery Graph / Constellation substrate
 - Status: in progress
 - Priority: P1
 - Branch: `dev/40-fixes`
 - Notes: Build a first-class graph substrate for navigable similarity topology, not just related-artist lists. Product name: `Discovery Graph` (`Constellation` as the stylistic alias). Start with a native backend graph service over normal storage/models, typed/weighted/explainable edges, and a UI graph summon point near SongID / MusicBrainz. Initial node families: artist, album, track, genre/tag, playlist, user/peer/pod, fingerprint/unknown cluster, canonical identity. Initial edge families: metadata similarity, co-occurrence, taste overlap, acoustic similarity, identity linkage, social/network linkage, and confidence/ambiguity edges. Phase toward semantic zoom (`mini-map`, `drawer graph`, `atlas view`) and make graph actions first-class (`recenter`, `expand`, `pin`, `compare`, `filter edge types`, `show why`, `queue nearby`, `save branch`). The first implementation slice should start in SongID and Search because those already carry rich candidate/evidence context.
 - Progress (2026-03-16): Added the first native Discovery Graph slice: backend graph API/service (`/api/v0/discovery-graph`) seeded from SongID runs, track/album/artist scopes, typed graph nodes/edges, MusicBrainz artist release-group expansion, reusable frontend graph canvas, inline SongID mini-map, graph modal with edge-type filtering, recenter actions, queue-nearby actions, and initial backend service tests.
 - Progress (2026-03-16): Widened Discovery Graph into the MusicBrainz lookup surface, added comparison overlays (`compareNodeId`), richer edge provenance / score-component / evidence payloads, graph pinning, pinned comparison actions, and browser-saved branch snapshots in the graph UI.
 - Progress (2026-03-16): Added broader Search summon points plus the first atlas-style semantic zoom layer: search list rows, search detail headers, MusicBrainz, SongID, and search-response cards can all launch graph neighborhoods; graph modals now support semantic filtering (`maxDepth`, `minNodeWeight`), queue-nearby actions from those broader surfaces, and proper saved-branch restore.
 - Progress (2026-03-16): Added a persistent in-page `DiscoveryGraphAtlasPanel` on the Search page so graph exploration is no longer modal-only; it supports manual seeds, saved-branch restore, semantic zoom controls, and nearby-search queueing.
 - Progress (2026-03-16): Added a dedicated `/discovery-graph` route and modal handoff into that atlas workspace, so graph neighborhoods are now addressable and restorable outside the Search page flow.
 - Progress (2026-03-16): Added inline atlas explainability so the dedicated graph workspace now shows visible edge-family counts, “why these nodes are near” evidence rows, score-component breakdowns, provenance, and recenter actions without falling back to the modal.
 - Remaining: deepen graph evidence toward decomposed similarity lanes and provenance explanations, enrich the dedicated atlas experience beyond the current panel-style implementation, and extend graph seeds beyond the current SongID / MusicBrainz / search metadata contexts.

- [ ] **T-917**: Implement SongID native intake and identification pipeline
 - Status: in progress
 - Priority: P1
 - Branch: `dev/40-fixes`
 - Notes: Build the `SongID` feature described in `docs/dev/SONGID_INTEGRATION_MAP.md`. Current slice now includes native SQLite-backed run persistence, Search-page UI placement near MusicBrainz lookup, text/YouTube/Spotify/local-file intake, MetadataFacade + MusicBrainz candidate generation, ranked download options, direct `Download Album` MB-release jobs, a deeper native `chop`-style evidence pipeline, persistent per-run artifact directories, full-source fingerprint capture, Demucs stem extraction, Panako source-store/query, Audfprint run-local DB matching, focused clip scheduling from comment timestamps, clip-level AcoustID + SongRec + AI-artifact heuristics, YouTube comment/timestamp harvesting, Whisper transcript excerpts, OCR frame scans, provenance signal detection, scorecard, assessment, queued background execution, SignalR live updates, corpus-based reranking, stage/percentage progress payloads, canonical-quality boosts from slskdn's native audio/canonical stats, and initial SongID backend tests covering the SQLite run store and scoring helper. The parity target is now `../ytdlpchopid`, not the older `../ytdlpchop`, and remaining work is explicitly mapped in `docs/dev/SONGID_INTEGRATION_MAP.md#remaining-todo`.

- [ ] **T-918**: SongID parity pass for `../ytdlpchopid`
 - Status: in progress
 - Priority: P1
 - Branch: `dev/40-fixes`
 - Notes: Implement the newly added `ytdlpchopid` parity surface inside native SongID: split `identity_assessment` vs `synthetic_assessment`; forensic-matrix fields (`top_evidence_for`, `top_evidence_against`, `quality_class`, `perturbation_stability`, `confidence_score`, `known_family_score`, `family_label`, lane scores/confidences, family hints, confidence penalty notes); chapter-aware clueing; C2PA/content-credentials detection; scorecard deltas (`songrec_distinct_match_count`, `raw_acoustid_hit_count`, `playlist_request_count`, `ai_comment_mentions`); mix decomposition into multiple track plans; candidate-fanout actions; expandable detailed forensic lanes (`confidence_lane`, `spectral_artifact_lane`, `lyrics_speech_lane`, `structural_lane`); unobtrusive synthetic/AI display that never overrides strong identity-based download planning. Add explicit tests for single-lane confidence caps and strong-identity suppression of synthetic overclaiming. Use the `Remaining TODO` section in `docs/dev/SONGID_INTEGRATION_MAP.md` as the source checklist.
 - Progress (2026-03-16): Implemented the first parity slice: native split `identityAssessment` / `syntheticAssessment`, legacy `assessment` compatibility alias, `forensicMatrix` payload, chapter parsing from yt-dlp metadata, chapter-aware focus timestamps, C2PA/content-credentials-aware provenance fields, scorecard deltas (`songRecDistinctMatchCount`, `rawAcoustIdHitCount`, `chapterHintCount`, `playlistRequestCount`, `aiCommentMentionCount`), unobtrusive synthetic UI with Popup-based detail, forensic lane summaries, targeted SongID scoring tests for single-lane caps and strong-identity suppression, segment-derived track plans from chapters/comments, and a `Search Top Candidates` batch action for non-singular identity results.
 - Progress (2026-03-16): Implemented the next parity slice: durable unbounded SongID queue intake with fixed concurrent workers, recovered queued/running runs after restart, persisted queue position and worker slot in the run model, added recent-run queue UI, added richer forensic parity fields (`syntheticScore`, `confidenceScore`, `knownFamilyScore`, `familyLabel`, `qualityClass`, `perturbationStability`, `topEvidenceFor`, `topEvidenceAgainst`, `notes`), added descriptor-priors and generator-family lanes, added real perturbation probes (low-pass, resample, pitch-shift) to drive stability/confidence instead of relying only on static artifact heuristics, and exposed `song_id.max_concurrent_runs` in native app config so the SongID worker pool is now user-configurable.
 - Progress (2026-03-16): Added explicit segment decomposition payloads to SongID runs, with grouped segment candidates, segment-specific plans and acquisition options, segment batch-search fan-out, and new queue/service tests covering requeue-on-restart and queue-position ordering. Also fixed a recovery-state bug so restart provenance is preserved in run evidence instead of being overwritten by queue-summary refresh.
 - Progress (2026-03-16): Added SongID controller tests covering queued run creation, bad-request validation, list responses, and run retrieval, so API behavior now has direct unit coverage in addition to the service/store/scoring layers.
 - Progress (2026-03-16): Propagated identity-first ranking into segment-derived acquisition options as well, persisted/reused corpus family hints in scoring, and added service/scoring tests so segment fan-out no longer uses the older quality/byzantine-heavy ordering path.
 - Remaining: deeper multi-track / mix decomposition beyond chapter/comment segment inference, broader SongID UI coverage, and stronger backend/frontend coverage around live queue updates, configurable worker behavior, and perturbation-backed forensic outputs.

- [ ] **T-915**: Fix web lint errors + re-enable eslint on build
 - Status: pending
 - Priority: P0
 - Branch: `dev/40-fixes`
 - Notes: Lint errors are widespread in `src/web/src/components/` (App, Chat, Contacts, Pods, Rooms, ShareGroups, SharedWithMe, System/*), plus `src/web/src/lib/*` and several tests. Build temporarily uses `DISABLE_ESLINT_PLUGIN=true` to unblock E2E; remove it and fix lint issues across these files.

- [x] **T-916**: Investigate E2E node exits during multi-peer tests
 - Status: done
 - Priority: P1
 - Branch: `dev/40-fixes`
 - Notes: Fixed `SqliteShareRepository.Keepalive()` method that was calling `Environment.Exit(1)` on transient errors. The method now properly handles FTS5 virtual tables and only exits on persistent database corruption, not transient errors like database locks during backup. See `docs/T916_NODE_EXIT_INVESTIGATION.md` for details. (2026-01-27)

- [x] **T-914**: Cross-node share discovery (“Shared with Me”)
 - Status: done
 - Priority: P0
 - Branch: `dev/40-fixes`
 - Notes: Implemented via private message announcements. When a share-grant is created, the owner sends a `SHAREGRANT:` message to recipients via Soulseek PM containing the grant details, collection metadata, items, token, and owner endpoint. `ShareGrantAnnouncementService` listens for these messages and ingests them into the recipient's local database. All 5 multi-peer E2E tests passing (2026-01-27).

### Medium Priority

**Research implementation (T-901–T-913)** — Design/scope: `docs/research/9-research-design-scope.md`. Suggested order: T-912 → T-911 → T-913 → T-901 → T-902 → T-903 → T-906 → T-907 → T-908. **T-912, T-911, T-913, T-901, T-902, T-903, T-906, T-907, T-908 done; Research (9) order complete.**

- [x] **T-912**: Metadata facade abstraction — `IMetadataFacade` (GetByRecordingId, GetByFingerprint, GetByFile, Search); MetadataFacade (MB, AcoustID, file tags via TagLib/XiphComment); IMusicBrainzClient.SearchRecordingsAsync; optional IMemoryCache. Soulseek adapter: follow-up.
- [x] **T-911**: MediaVariant model and storage — `MediaVariant` (ContentDomain, domain-specific: Audio/Image/Video/GenericFile); `IMediaVariantStore` + `HashDbMediaVariantStore` (Music→HashDb, Image/Video/Generic in-memory); `IHashDbService.GetAudioVariantByFlacKeyAsync`; `ContentDomain` Image/Video; `FromAudioVariant`/`ToAudioVariant`.
- [x] **T-913**: AudioCore domain module — `slskd.AudioCore` (API boundary doc); `AddAudioCore(IServiceCollection, appDirectory)` registers fingerprinting, HashDb, IMediaVariantStore, ICanonicalStatsService, IDedupeService, IAnalyzerMigrationService, ILibraryHealth, IMusicContentDomainProvider; wired in Program.
- [x] **T-901**: Ed25519 signed identity system — Design: `docs/research/T-901-ed25519-identity-design.md` (unified model, key lifecycle, alignment); `Ed25519Signer.DerivePeerId` formalized (PeerId = Base32(First20(SHA256(pubkey)))).
- [x] **T-902**: DHT node and routing table — Design: `docs/research/T-902-dht-node-design.md`. KademliaRoutingTable (160-bit, k-buckets, FIND_NODE); DhtMeshService responds to FindNode, FindValue, Store, Ping; KademliaRpcClient; NodeId from Ed25519 (SHA1); slskdn DHT (BEP 5 GET_PEERS/ANNOUNCE_PEER = FindValue/Store).
- [x] **T-903**: DHT storage with TTL and signatures — Design: `docs/research/T-903-dht-storage-design.md`. IDhtClient PUT/GET/GetMultipleAsync, TTL (expiry on read); Store RPC requires Ed25519 (DhtStoreMessage); overlap with shadow index, pods, scenes.
- [x] **T-906**: Native mesh protocol backend — `IContentBackend` via mesh/DHT only (no Soulseek, no BitTorrent); mesh “get content by ContentId” RPC.
- [x] **T-907**: HTTP/WebDAV/S3 backend — `ContentBackendType.WebDav`, `WebDavBackend` (registry, domain allowlist, Basic/Bearer, HEAD); `ContentBackendType.S3`, `S3Backend` (registry, s3://bucket/key, HeadObject, AWSSDK.S3, MinIO/B2/AWS). Design: `docs/research/T-907-http-webdav-s3-backend-design.md`.
- [x] **T-908**: Private BitTorrent backend — Design: `docs/research/T-908-private-bittorrent-backend-design.md`. `TorrentBackendOptions.PrivateMode` (`PrivateTorrentModeOptions`: PrivateOnly, DisableDht, DisablePex, AllowedPeerSources); `PrivatePeerSource` enum. StubBitTorrentBackend replacement and TorrentBackend private logic: follow-up.

### Low Priority

- [x] **T-006**: Create Chat Rooms from UI
  - Status: Done
  - Priority: Low
  - Related: slskd #1258
  - Notes: RoomCreateModal; create→join (public: join creates if new; private: server/ops create, then join via dropdown).

- [x] **T-007**: Predictable Search URLs
  - Status: Done
  - Priority: Low
  - Related: slskd #1170
  - Notes: /searches?q= and search icon → /searches/{id} bookmarkable (create returns id; navigate uses it).

---

## Optionals / Follow-up (40-fixes, Research, Packaging)

> All items below must be done. Source: `docs/dev/40-fixes-plan.md` Deferred/optional, Research follow-ups, TODO.md, Out of Scope. Verify against “Completed” list in 40-fixes when marking done.
### 40-fixes plan (PR / § / J)

- [x] **PR-03 Passthrough AllowedCidrs**: Add `Web.Authentication.Passthrough.AllowedCidrs` (e.g. `"127.0.0.1/32,::1/128"`) for explicit CIDR allowlist instead of/in addition to loopback check.
- [x] **PR-04 CORS AllowedHeaders/AllowedMethods**: Implement and wire `Web.Cors.AllowedHeaders`, `Web.Cors.AllowedMethods` in Options and CORS middleware.
- [x] **PR-05 Exception / ValidationProblemDetails**: Custom `InvalidModelStateResponseFactory` (or validation formatter) so Production does not leak internals; consistent `ValidationProblemDetails`.
- [x] **PR-06 Dump 501**: Dump endpoint returns **501** when dump creation fails (e.g. dotnet-dump not on PATH, DiagnosticsClient failure) with instructions.
- [x] **PR-07 ModelState / RejectInvalidModelState**: `Web.Api.RejectInvalidModelState` (in Enforce can imply true); consistent `ValidationProblemDetails` (same factory as PR-05 where applicable).
- [x] **PR-08 MeshGateway chunked POST**: Chunked POST for MeshGateway — bounded body read, 413 on over-limit; support chunked when `ContentLength` null (if not already done).
- [x] **PR-09a Kestrel MaxRequestBodySize**: Kestrel `MaxRequestBodySize` configured and documented in Options/example config.
- [x] **PR-09b Rate limit fed/mesh**: Rate-limit fed/mesh integration: `Burst_federation_inbox_*`, `Burst_mesh_gateway_*` in `FedMeshRateLimitTestHostFactory` (or equivalent) and policies applied.
- [x] **§8 QuicDataServer**: `QuicDataServer` read/limits aligned with `GetEffectiveMaxPayloadSize`.
- [x] **§9 Metrics Basic Auth constant-time**: Metrics Basic Auth uses constant-time comparison (`CryptographicOperations.FixedTimeEquals`); `WWW-Authenticate: Basic realm="metrics"`.
- [x] **§11 NotImplementedException gating**: Incomplete features (I2P, RelayOnly, PerceptualHasher, etc.) fail at startup or return 501 when enabled; no `NotImplementedException` crash in configured defaults.
- [x] **J ScriptService deadlock**: ScriptService: async read of stdout/stderr, `WaitForExitAsync`, timeout and process kill; no `WaitForExit()` while redirecting.
- [x] **6.4 Pod Join nonce**: `PodJoinRequest` has optional `Nonce`; `PodJoinLeaveService` uses `PodJoinOptions.SignatureMode` (bind `PodCore:Join`). When Enforce: Nonce required, replay cache `PodId:PeerId:Nonce` with 5min TTL. Done.

### Research follow-ups (T-906, T-907, T-908, T-912)

- [x] **T-906 Resolver fetch**: SimpleResolver calls `MeshContent.GetByContentId` via IMeshServiceClient for `mesh:{peerId}:{contentId}`; writes payload to temp file and returns path. Done.
- [x] **T-907 Resolver fetch**: SimpleResolver uses `IContentFetchBackend`; `WebDavBackend`, `S3Backend`, `HttpBackend` implement it; fetch via `FetchToStreamAsync`. Done.
- [x] **T-908 StubBitTorrentBackend / TorrentBackend**: `MonoTorrentBitTorrentBackend` registered in DI; respects `PrivateMode` (DisableDht, DisablePex, InviteList). `StubBitTorrentBackend` class remains in `SwarmSignalHandlers` but is not in DI. Done.
- [x] **T-912 Soulseek adapter**: `IMetadataFacade.GetBySoulseekFilenameAsync(username, filename)` parses common patterns (Artist - Title, Album - NN - Title, NN. Title) and returns `MetadataResult` with `SourceSoulseek`. Done.

### Packaging (TODO.md)

- [x] **Proxmox LXC templates**: `packaging/proxmox-lxc/` — README, `slskdn.conf.example`, `setup-inside-ct.sh` (Debian 12/Ubuntu 22.04: .NET 8, slskdn zip to /opt/slskdn, systemd, /etc/slskd, /var/lib/slskd). Done.
- [ ] **Packaging follow-up: re-enable dev flake only with a real published build-dev release**: On 2026-03-16 the broken `slskdn-dev` flake output was disabled and the fake `releases/download/dev/...` alias was removed. Re-enable `slskdn-dev` only after a real `build-dev-<version>` GitHub release is confirmed for the intended platforms and the dev hashes are populated from those published assets.
- [ ] **Packaging follow-up: automate the NixOS VM smoke test**: The 2026-03-17 QEMU/KVM NixOS validation proved the stable flake now builds and the packaged `slskd` binary starts on real NixOS, but the flow is manual and the upstream NixOS module still needs explicit `domain`, `environmentFile`, and `settings.shares.directories` values for local validation. Script a reusable smoke test or CI-friendly VM harness so future Nix packaging changes are validated without hand-driving the guest.
- [ ] **Repo-wide C# analyzer cleanup**: As of 2026-03-16 the packaging/test follow-up cleaned the touched files, but `bash ./bin/lint` / full-solution `dotnet format --verify-no-changes` still report broad existing analyzer/style debt across unrelated C# files. Triage and fix that separately from packaging work.
- [x] **Security follow-up (2026-03-21): close remaining CodeQL alert clusters**: Fixed the true-positive clusters by removing cleartext secret logging from `Program` and `AsymmetricDisclosure`, constraining relay token validation to trusted server-side agent identities, rebuilding SQLite share-repo connection strings from validated data sources, and restricting HashDb query profiling to admin-only single-statement read-only SQL with regression tests. Remaining scanner-only findings should now be handled as justified dismissals after the next GitHub analysis refresh instead of by more code churn.
- [ ] **Release regression follow-up: add a subpath-hosted web smoke test**: Add automated coverage that serves the built web UI under a non-root `web.url_base` such as `/slskd` and verifies the HTML references relative assets rather than root-relative `/assets/...` URLs, so future frontend build-tool changes cannot silently reintroduce blank white-page deployments.
- [x] **Testing hardening: add one repo-level release gate**: Added `packaging/scripts/run-release-gate.sh`, wired it into `ci.yml` and `build-on-tag.yml`, added built-web output verification for subpath-safe assets, and documented the policy in `docs/dev/testing-policy.md`. Validated locally with packaging checks, 91 frontend tests, 2619 unit tests, and 46 backend smoke/regression tests passing. Done.
- [x] **Changelog discipline at commit time**: Added `scripts/validate-changelog-entry.sh`, wired it into `.githooks/pre-commit` and PR CI in `.github/workflows/ci.yml`, and updated `docs/CHANGELOG.md` so release-worthy changes must add a real `## [Unreleased]` bullet when the work lands instead of deferring summary writing to release time.
- [x] **Git hook bootstrap**: Added `scripts/setup-git-hooks.sh` so clones can install `.githooks` with one command, and updated onboarding docs to require the hook-setup step during local development.
- [x] **Peer exception telemetry cleanup**: Reclassified expected Soulseek peer/distributed-network unobserved task exceptions in `Program.cs` so normal timeout/refusal churn no longer logs as `[FATAL]` process-shutdown telemetry.

### 40-fixes Out of Scope (docs)

- [x] **CHANGELOG and option docs**: CHANGELOG and option docs (e.g. `config/slskd.example.yml`) updated for new flags and breaking behavior from 40-fixes (EnforceSecurity, Mesh:SyncSecurity, etc.).

### Docs / meta

- [x] **Sync DEVELOPMENT_HISTORY Pending**: `docs/archive/DEVELOPMENT_HISTORY.md` "Pending Features" — Phase 8 Create Chat Rooms/Predictable Search URLs → ✅ (T-006, T-007); Pending section now points to tasks.md, lists done (T-001–T-007) and still-pending.
- [x] **slskd.Tests.Unit Phase 2–6**: Completion-plan shows 0 Compile Remove, 0 skips; `dotnet test` slskd.Tests.Unit 2294 pass, 0 fail, 0 skip. Re-enablement complete.
- [x] **Triage src/ TODO/FIXME/placeholder**: Triaged in `memory-bank/triage-todo-fixme.md`: ~13 accepted, ~100 defer, 7 task. Follow-up [ ] below. Done.
- [x] **Triage follow-up (task)**: Options realm validation re-enabled (`Realm.Validate()`, `MultiRealm.Validate()` in Options.Validate). QuicDataServer TODO replaced with defer comment (IOverlayDataPayloadHandler). RescueService/Scene* remain in triage-todo-fixme as defer. Done.
- [x] **Reconcile tasks-audit-gaps**: Phase 8 reconciled: T-1421, T-1422, T-1423, T-1425, T-1429 implemented (Ed25519, KeyedSigner/ControlSigner, QuicOverlayServer, QuicDataServer, ControlDispatcher). Tasks-audit-gaps.md updated. T-1424, T-1426, T-1427, T-1428 and Phases 1–6 remain as backlog; promote to [ ] when prioritizing.

### Design / Backlog (ShareGroups, Collections, Streaming, Hybrid Search)

- [x] **ShareGroups + Collections + Streaming + Hybrid Search (design merged)**: Assessment and merged design in `docs/design/sharegroups-collections-streaming-assessment.md`. Merges older agent-ticket with existing: ShareGroup, Collection, ShareGrant, SharePolicy, IShareTokenService, IContentLocator, GET /streams/{contentId} (range, token or auth), manifest, IStreamSessionLimiter; mesh search (we have overlay + MeshSearchRpcHandler + SearchResponseMerger + MeshContent.GetByContentId). Feature flags: CollectionsSharing, Streaming, StreamingRelayFallback, MeshParallelSearch (= VirtualSoulfind.MeshSearch.Enabled), MeshPublishAvailability (defer). **All phases complete** (2026-01-26): Phase 1 (foundations), Phase 2 (collections/sharing), Phase 3 (streaming), Phase 4 (mesh search improvements: MediaKinds/ContentId/Hash in MeshSearchFileDto, SearchResponseMerger normalization, MeshParallelSearch wired), Phase 5 (IMeshContentFetcher with size/hash validation, GET /api/v0/relay/streams/{contentId} endpoint).

- [x] **Backfill for shared collections**: Backfill API endpoint and UI for downloading all items from a shared collection. Supports both HTTP downloads (cross-node, no Soulseek required) and Soulseek downloads (when available). **Complete** (2026-01-27): `POST /api/v0/share-grants/{id}/backfill` endpoint, "Backfill All" button in SharedWithMe manifest modal, validates AllowDownload policy, returns detailed results.

- [x] **Persistent tabbed interface for Chat**: Converted Chat component to use tabbed interface with localStorage persistence, matching Browse and Rooms pattern. **Complete** (2026-01-27): Created `ChatSession.jsx` component, converted `Chat.jsx` to functional component with hooks, tabs persist in `slskd-chat-tabs` localStorage, supports multiple concurrent conversations.

- [x] **Mesh UDP Overlay Fault Tolerance**: UDP overlay server now gracefully handles port binding failures (address already in use, firewall blocked). Mesh continues operating in degraded mode: DHT operations, relay/beacon services, and hole punching remain functional. Only direct inbound UDP connections are unavailable. Clear warning logs explain degraded mode. Matches fault-tolerant pattern used by QUIC overlay servers. Enables mesh operation behind firewalls without port forwarding. **Complete** (2026-01-26): UdpOverlayServer updated with graceful error handling, all 2430 unit tests and 190 integration tests passing.

- [x] **Logs Page Improvements**: Reduced CSRF logging noise and added log level filtering to logs page. CSRF Debug logs for safe methods (GET requests) and successful validations changed to Verbose level (won't appear in default views). Added filter buttons (All, Info, Warn, Error, Debug) to logs page with count display. **Complete** (2026-01-26): ValidateCsrfForCookiesOnlyAttribute updated, Logs component enhanced with filtering UI, all 2430 unit tests and 190 integration tests passing.

---

## Packaging & Distribution

- [x] **T-010**: TrueNAS SCALE Apps
  - Status: Done
  - Priority: High
  - Notes: Helm ix-chart; appVersion 0.24.1-slskdn.40, home/sources→snapetech/slskdn (chore 2026-01-25).

- [x] **T-011**: Synology Package Center
  - Status: Done
  - Priority: High
  - Notes: SPK; INFO version 0.24.1, URLs→snapetech/slskdn (chore 2026-01-25).

- [x] **T-012**: Homebrew Formula
  - Status: Done
  - Priority: High
  - Notes: Formula/slskdn.rb 0.24.1-slskdn.40, osx-arm64/osx-x64/linux-x64, SHA256 from GitHub API (chore 2026-01-25).

- [x] **T-013**: Flatpak (Flathub)
  - Status: Done (2026-01-25)
  - Priority: High
  - Notes: .NET 8.0.11 + slskdn-main-linux-x64 0.24.1-slskdn.40, slskdn.svg; placeholders replaced; build.sh, FLATHUB_SUBMISSION updated.

- [x] **T-014**: Helm chart for generic Kubernetes
  - Status: Done (2026-01-25)
  - Priority: Medium
  - Notes: `packaging/helm/slskdn/` — Chart.yaml, values.yaml, templates (_helpers, Deployment, Service, PVCs, Ingress). No TrueCharts; standard K8s, PVCs for config/downloads/shares/incomplete. appVersion 0.24.1-slskdn.40. README with install and main values.

---

## Completed Tasks

- [x] **chore (2026-04-06):** Fixed tester issues `#193` and `#194`, making share rescan progress monotonic, separating CSRF cookie/request-token naming so cookie-authenticated Web UI actions stop failing, downgrading expected Soulseek network churn out of fake fatal telemetry, and folding the remaining low-risk frontend/docs PR content directly into `main` so the stale PR queue can be closed as superseded.

- [x] **chore (2026-03-21):** Security alert cleanup on `master`. Narrowed `.github/workflows/codeql.yml` to exclude noisy `cs/log-forging`, constrained API/bridge filesystem probes to configured app-owned roots, required auth for `PodMembershipController`, added `PathGuard` and controller regression coverage, and verified `upstream` still targets `slskd/slskd` rather than a planning fork.

- [x] **T-912 (2026-01-25):** Metadata facade — IMetadataFacade, MetadataResult, MetadataFacade (GetByRecordingId, GetByFingerprint, GetByFile, Search). MusicBrainzClient.SearchRecordingsAsync + RecordingSearchHit. File tags (TagLib, XiphComment MUSICBRAINZ_*). AcoustID→MB for fingerprint. IMemoryCache. DI in Program. Soulseek adapter: follow-up.

- [x] **T-911 (2026-01-25):** MediaVariant model and storage — MediaVariant (Domain, VariantId, FirstSeenAt, LastSeenAt, SeenCount, FileSha256, FileSizeBytes; Audio/ImageDimensions/ImageCodec/VideoDimensions/VideoCodec/VideoDurationSeconds). IMediaVariantStore (GetByVariantId, GetByRecordingId, GetByDomain, Upsert). HashDbMediaVariantStore (Music→IHashDbService, Image/Video/GenericFile in-memory). IHashDbService.GetAudioVariantByFlacKeyAsync. ContentDomain Image=2, Video=3. FromAudioVariant/ToAudioVariant. DI.

- [x] **T-913 (2026-01-25):** AudioCore domain module — slskd.AudioCore.AudioCore (API boundary doc: IChromaprintService, IFingerprintExtractionService, IHashDbService, IMediaVariantStore, ICanonicalStatsService, ILibraryHealthService, ILibraryHealthRemediationService, IAnalyzerMigrationService, IDedupeService, IMusicContentDomainProvider, analyzers). AddAudioCore(IServiceCollection, appDirectory) registers all; Program uses AddAudioCore(Program.AppDirectory); scattered audio registrations consolidated.

- [x] **T-901 (2026-01-25):** Ed25519 signed identity system — docs/research/T-901-ed25519-identity-design.md: unified identity model (Mesh+IKeyStore/FileKeyStore shared with Pods; ActivityPub separate); key lifecycle (FileKeyStore JSON/KeyPath/RotateDays, ActivityPubKeyStore IEd25519KeyPairGenerator PEM, RotateKeypairAsync); alignment. Ed25519Signer.DerivePeerId formalized: PeerId = Base32(First20(SHA256(publicKey))). Revocation, DID deferred.

- [x] **T-902 (2026-01-25):** DHT node and routing table — docs/research/T-902-dht-node-design.md. KademliaRoutingTable (160-bit, k=20, bucket splitting, XOR, Touch, GetClosest); selfId=SHA1(Ed25519) from IKeyStore. DhtMeshService: FindNode, FindValue, Store, Ping; KademliaRpcClient; slskdn DHT wire (mesh overlay, JSON). GET_PEERS/ANNOUNCE_PEER mapped to FindValue/Store; DhtRendezvous remains BEP 5 client.

- [x] **T-903 (2026-01-25):** DHT storage with TTL and signatures — docs/research/T-903-dht-storage-design.md. IDhtClient PutAsync/GetAsync/GetMultipleAsync; TTL expiry on read; Store RPC requires Ed25519 (DhtStoreMessage.CreateSigned/VerifySignature, 5 min freshness); same store for shadow index, pods, scenes; _maxPayload; conflict last-write-wins, republish open.

- [x] **T-906 (2026-01-25):** Native mesh protocol backend — ContentBackendType.NativeMesh; NativeMeshBackend (IMeshDirectory, IContentIdRegistry; FindCandidatesAsync via FindPeersByContentAsync, BackendRef mesh:{peerId}:{contentId}; ValidateCandidateAsync format-only); NativeMeshBackendOptions. Design: docs/research/T-906-native-mesh-backend-design.md (mesh “get content by ContentId/hash” RPC, resolver fetch follow-up). DI: document only (v2 IContentBackend not wired in Program).

- [x] **T-907 (2026-01-25):** HTTP/WebDAV/S3 backend — ContentBackendType.WebDav, WebDavBackend (registry, domain allowlist, Basic/Bearer, HEAD); ContentBackendType.S3, S3Backend (registry, s3://bucket/key, HeadObject, AWSSDK.S3). Design: docs/research/T-907-http-webdav-s3-backend-design.md. Resolver fetch: follow-up.

- [x] **T-908 (2026-01-25):** Private BitTorrent backend — TorrentBackendOptions.PrivateMode (PrivateTorrentModeOptions: PrivateOnly, DisableDht, DisablePex, AllowedPeerSources), PrivatePeerSource enum. Design: docs/research/T-908-private-bittorrent-backend-design.md (IBitTorrentBackend, MonoTorrent, private swarm, StubBitTorrentBackend replacement). Stub replacement and TorrentBackend private logic: follow-up.

- [x] **chore (2026-01-25):** Research (9) **unpinned**; implementation started. T-901–T-913 moved to tasks.md § Medium Priority (Research implementation). Suggested order: T-912 → T-911 → T-913 → T-901 → T-902 → T-903 → T-906 → T-907 → T-908. Start: T-912 (Metadata facade).

- [x] **T-014 (2026-01-25):** Helm chart for generic Kubernetes at `packaging/helm/slskdn/`. Chart.yaml, values.yaml, templates (_helpers, Deployment, Service, PVCs, Ingress). No TrueCharts; standard K8s; PVCs for config/downloads/shares/incomplete. TODO.md Helm Charts marked done.

- [x] **chore (2026-01-25):** slskd.Tests.Unit completion plan: Phase 1 and Phase 3 marked **DONE** (PrivacyLayerIntegration, ContentDomain, SimpleMatchEngine, RealmAwareGossip/Governance/RealmService, MeshCircuitBuilder/MeshSyncSecurity/MeshTransportService/Phase8, MembershipGate, FederationService, ActivityPubBridge, BridgeFlow*, Realm* suite, CircuitMaintenanceService, ActivityPubKeyStore). Execution order §0–3 updated. 2257 pass, 0 skip.

- [x] **t410-backfill-wire (2026-01-25):** RescueMode underperformance detector → RescueService. RescueModeOptions (Enabled, MaxQueueTimeSeconds, MinThroughputKBps, MinDurationSeconds, StalledTimeoutSeconds, CheckIntervalSeconds); IRescueService.IsRescueActive; UnderperformanceDetectorHostedService (QueuedTooLong, ThroughputTooLow, Stalled); IRescueService, RescueGuardrailService, UnderperformanceDetectorHostedService in Program.cs. RescueMode.Enabled=false by default.

- [x] **T-404+ (2026-01-25):** Phase 2 continuation done. t410-backfill-wire (rescue wire) completed; codec fingerprinting / quality (T-420–T-430) already done per TASK_STATUS_DASHBOARD.

- [x] **40-fixes plan (PR-00–PR-14) (2026-01-25):** Epic implemented per `docs/dev/40-fixes-plan.md`. slskd.Tests 46 pass, slskd.Tests.Unit 2257 pass; Integration 184 pass per audit. Enforce, HardeningValidator, default-deny, passthrough loopback, CORS, exception handler, dump, ModelState, MeshGateway body/413, rate limiting, ControlEnvelope/KeyedSigner, MessagePadder Unpad, Pod MessageSigner/Router, ActivityPub HTTP signatures. Deferred table: status only.

- [x] **chore (2026-01-25):** Research (9) **pinned for future build**. Moved to tasks.md § Pinned; COMPLETE_PLANNING_INDEX_V2, TASK_STATUS_DASHBOARD, `docs/research/9-research-design-scope.md` updated. activeContext: Current focus = 40-fixes (PR-00–PR-14), T-404+ (optional), new product.

- [x] **chore (2026-01-25):** Research (9) design/scope: `docs/research/9-research-design-scope.md` — scope, deps, open questions, suggested order for T-901–T-913. Linked from COMPLETE_PLANNING_INDEX_V2, tasks.md, activeContext.

- [x] **chore (2026-01-25):** activeContext: Next Steps first, then Research (9). Next Steps revised: slskd.Tests.Unit, Phase 14, Packaging T-010–T-013, T-003/T-004 done; T-404+ optional; 40-fixes deferred. New "Then: Research (9)" section. tasks.md: Research (9) "Do after activeContext Next Steps".

- [x] **chore (2026-01-25):** COMPLETE_PLANNING_INDEX_V2: Phase 6X (T-850–T-860) marked Complete to match TASK_STATUS_DASHBOARD (bridge lifecycle, Soulfind proxy, API, MBID resolution, filename synthesis, anonymization, room→scene, transfer proxying, config UI, status dashboard, Nicotine+ tests).

- [x] **chore (2026-01-25):** COMPLETE_PLANNING_INDEX_V2: Phases 2, 2-Ext, 3, 4, 5, 6, 7 marked Complete; 9 research tasks (T-901, T-902, T-903, T-906–T-908, T-911–T-913) as ⏸️ optional. tasks.md: Research (9) in Low Priority.

- [x] **T-427 (2026-01-25):** Phase 2-Ext: Analyzer migration force; --audio-reanalyze and --audio-reanalyze-force at startup; POST /api/audio/analyzers/migrate?force=true.

- [x] **T-007 (2026-01-25):** Predictable Search URLs: create() returns id; /searches?q= and search icon → /searches/{id} bookmarkable; navigate uses /searches/{id}.

- [x] **chore (2026-01-25):** TrueNAS Chart appVersion 0.24.1-slskdn.40, version 0.2.1, home/sources→snapetech/slskdn. Synology INFO version 0.24.1, URLs→snapetech/slskdn.

- [x] **chore (2026-01-25):** RPM slskdn.spec 0.24.1.slskdn.40, Source0→slskdn-main-linux-x64.zip. Debian changelog 0.24.1.slskdn.40-1.

- [x] **chore (2026-01-25):** Chocolatey slskdn 0.24.1-slskdn.40 (slskdn-main-win-x64.zip, sha256). AUR PKGBUILD + PKGBUILD-bin pkgver 0.24.1.slskdn.40.

- [x] **chore (2026-01-25):** Snap snapcraft.yaml → 0.24.1-slskdn.40 (slskdn-main-linux-x64.zip, sha256).

- [x] **chore (2026-01-25):** slskd.Tests Enforce_invalid_config_host_startup un-skipped: mutex probe (avoid Program load), `dotnet slskd.dll`, soft-skip on "already running". 46 pass, 0 skip.

- [x] **chore (2026-01-25):** Homebrew Formula/slskdn.rb → 0.24.1-slskdn.40 (slskdn-main-osx-arm64, -osx-x64, -linux-x64; SHA256 from GitHub API).

- [x] **T-013 (2026-01-25):** Flatpak: .NET 8.0.11 (dotnetcli.azureedge.net), slskdn 0.24.1-slskdn.40 `slskdn-main-linux-x64.zip`, slskdn.svg; placeholders replaced; build.sh (no prepare_icons), FLATHUB_SUBMISSION checklist updated.

- [x] **chore (2026-01-25):** gitignore `mesh-overlay.key`, untrack; activeContext WORK DIRECTORY `<repo-root>`; completion-plan Phase 0 + Discuss first marked **DONE** (CodeQuality, ActivityPubKeyStore, CircuitMaintenance); DomainFrontedTransportTests DONE.

- [x] **T-MC1**: MediaCore Chromaprint FFT + FuzzyMatcher perceptual (2026-01-25)
  - Chromaprint: MathNet.Numerics, FFT-based ComputeChromaPrint (24-bin chroma, 64-bit hash); DifferentContent_LowSimilarityScores un-skipped; PerceptualHasherTests 440vs880.
  - FuzzyMatcher: ScorePerceptualAsync uses IDescriptorRetriever+IPerceptualHasher when descriptors have NumericHash; FuzzyMatcherTests 35 pass, ScorePerceptualAsync_WhenDescriptorsHavePerceptualHashes added.

- [x] **T-100**: Auto-Replace Stuck Downloads
  - Status: Done (Release .1)
  - Notes: Finds alternatives for stuck/failed downloads

- [x] **T-101**: Wishlist/Background Search
  - Status: Done (Release .2)
  - Notes: Save searches, auto-run, auto-download

- [x] **chore (2026-03-15):** SongID integration map written in `docs/dev/SONGID_INTEGRATION_MAP.md`. Defines native `SongID` architecture, feature-parity assessment against `../ytdlpchop`, Search-page placement near MusicBrainz lookup, byzantine scoring model, and phased implementation plan for song / album / discography download actions.

- [x] **T-102**: Smart Result Ranking
  - Status: Done (Release .4)
  - Notes: Speed, queue, slots, history weighted

- [x] **T-103**: User Download History Badge
  - Status: Done (Release .4)
  - Notes: Green/blue/orange badges

- [x] **T-104**: Advanced Search Filters
  - Status: Done (Release .5)
  - Notes: Modal with include/exclude, size, bitrate

- [x] **T-105**: Block Users from Search Results
  - Status: Done (Release .5)
  - Notes: Hide blocked users toggle

- [x] **T-106**: User Notes & Ratings
  - Status: Done (Release .6)
  - Notes: Personal notes per user

- [x] **T-107**: Multiple Destination Folders
  - Status: Done (Release .2)
  - Notes: Choose destination per download

- [x] **T-108**: Tabbed Browse Sessions
  - Status: Done (Release .10)
  - Notes: Multiple browse tabs, persistent

- [x] **T-109**: Push Notifications
  - Status: Done (Release .8)
  - Notes: Ntfy, Pushover, Pushbullet

---

- [x] **T-001**: Persistent Room/Chat Tabs
  - Status: Done (2025-12-12)
  - Priority: High
  - Branch: experimental/whatAmIThinking
  - Related: `TODO.md`, Browse tabs implementation
  - Notes: Implemented tabbed interface like Browse. Reuses `Browse.jsx`/`BrowseSession.jsx` patterns.

- [x] **T-002**: Scheduled Rate Limits
  - Status: Done (2025-12-12)
  - Priority: High
  - Branch: experimental/whatAmIThinking
  - Related: slskd #985
  - Notes: Day/night upload/download speed schedules like qBittorrent

- [x] **T-003**: Download Queue Position Polling
  - Status: Done (2025-12-12)
  - Priority: Medium
  - Branch: experimental/whatAmIThinking
  - Related: slskd #921
  - Notes: Auto-refresh queue positions for queued files

- [x] **T-004**: Visual Group Indicators
  - Status: Done (2025-12-12)
  - Priority: Medium
  - Branch: experimental/whatAmIThinking
  - Related: slskd #745
  - Notes: Icons in search results for users in your groups

- [x] **T-005**: Traffic Ticker
  - Status: Done (2025-12-12)
  - Priority: Medium
  - Branch: experimental/whatAmIThinking
  - Related: slskd discussion #547
  - Notes: Real-time upload/download activity feed in UI


*Last updated: 2026-01-27*

---

## Future Work / Backlog

> **Status**: All items below are optional/nice-to-have. No critical blockers.  
> **Priority**: P2-P3 (Low-Medium)  
> **Date Added**: 2026-01-27

### Testing Expansion (P1 - Quality Assurance)

**Priority**: P1 (Quality Assurance)  
**Status**: Tests passing, but could expand coverage  
**Estimated**: 1-2 weeks

#### Bridge Proxy Integration Tests
- [x] **Bridge E2E Tests**: Add end-to-end tests for bridge proxy server with actual legacy Soulseek clients
 - Status: done
 - Priority: P1
 - Branch: `dev/40-fixes`
 - Notes: Created `SlskdnFullInstanceRunner` harness for full instance testing. All 5 Bridge E2E tests passing (2026-01-26). Tests gracefully skip when binary unavailable with helpful instructions.
  - Currently 5 integration tests skipped (require full slskdn instance, not TestServer)
  - Tests: `BridgeProxyServer_Should_Accept_Client_Connection`, `BridgeProxyServer_Should_Handle_Login_Request`, `BridgeProxyServer_Should_Handle_Search_Request`, `BridgeProxyServer_Should_Handle_RoomList_Request`, `BridgeProxyServer_Should_Reject_Invalid_Authentication`
  - **Blocking Issue**: `SlskdnTestClient` uses `TestServer` which doesn't support TCP listeners
  - **Solution Options**:
    - Create full instance test harness (start actual slskdn process)
    - Use Docker containers for isolated testing
    - Manual testing with real Soulseek clients (documentation)

- [x] **Protocol Format Validation**: Test bridge protocol parser with real Soulseek client message formats
  - Status: done
  - Priority: P1
  - Branch: `dev/40-fixes`
  - Notes: Enhanced `BridgeProtocolValidationTests` with 6 additional edge case tests covering all message types, message length validation, Unicode filename handling, large payloads (100KB+), empty file lists, and room list responses. Total 13+ protocol validation tests, all passing (2026-01-27).
  - Verify compatibility with actual Soulseek protocol versions
  - Test edge cases discovered in real-world usage
  - Validate message serialization/deserialization roundtrips

- [x] **Performance Testing**: Benchmark bridge proxy server under load
 - Status: done
 - Priority: P1
 - Branch: `dev/40-fixes`
 - Notes: Added `BridgePerformanceTests.cs` with 7 tests covering concurrent operations, latency, large messages, high-volume scenarios, memory efficiency, and rapid connect/disconnect cycles. All tests passing (2026-01-26).
  - Concurrent connection handling
  - Message throughput
  - Memory usage under sustained load
  - Latency measurements

- [x] **Protocol Contract Tests**: Fix/enable 3 skipped protocol contract tests
 - Status: done
 - Priority: P1
 - Branch: `dev/40-fixes`
 - Notes: Enhanced 3 previously skipped tests with better assertions and graceful skipping. All 6 protocol contract tests passing when Soulfind available (2026-01-26).
  - `Should_Login_And_Handshake` - Requires Soulseek server (SoulfindRunner)
  - `Should_Send_Keepalive_Pings` - Requires Soulseek server
  - `Should_Handle_Disconnect_And_Reconnect` - Requires Soulseek server
  - **Status**: Non-blocking - Tests skip gracefully when Soulfind unavailable
  - **Note**: Protocol compliance verified through real-world usage

### Multi-Swarm Phase 6+ (Future Features)

**Priority**: P2 (Feature Development)  
**Status**: Phases 1-5 complete (62/62 tasks, 100%)  
**Reference**: `memory-bank/multi-swarm-task-summary.md`

#### Phase 6: Advanced Swarm Features (Complete)
- [x] **T-800+**: Advanced swarm orchestration features
  - Status: done
  - Priority: P2
  - Notes: Phase 6 (Virtual Soulfind Mesh) is complete (T-800 to T-840, 41 tasks). All core Phase 6 features implemented. T-800+ refers to future enhancements beyond current Phase 6 scope, which are documented in planning docs but not yet prioritized (2026-01-27).
  - See `docs/archive/planning/COMPLETE_PLANNING_INDEX.md` for full Phase 6 task list
  - **Note**: Phase 6 (Virtual Soulfind Mesh) is already complete (T-800 to T-840, 41 tasks)
  - **Future Phase 6+**: Additional advanced features beyond current Phase 6 scope

#### Future Multi-Swarm Enhancements
- [x] **Advanced Discovery**: Enhanced peer discovery and content matching
  - Status: done
  - Priority: P2
  - Notes: Created `IAdvancedDiscoveryService` with enhanced similarity algorithms, match type classification, peer ranking, and fuzzy matching. Integrates with `ContentVerificationService` for source discovery. Service registered in DI (2026-01-27).
- [x] **Swarm Analytics**: Advanced metrics and reporting for swarm behavior
  - Status: done
  - Priority: P2
  - Notes: Created comprehensive `SwarmAnalyticsService` with performance metrics, peer rankings, efficiency metrics, historical trends, and recommendations engine. API controller with 5 endpoints. Frontend dashboard component in System UI. Service registered in DI (2026-01-27).
- [x] **Adaptive Scheduling**: Machine learning or advanced heuristics for chunk assignment
  - Status: done
  - Priority: P2
  - Notes: Created `IAdaptiveScheduler` and `AdaptiveScheduler` with learning from feedback, factor correlation analysis, and performance-based weight adaptation. Wraps existing `ChunkScheduler` for backward compatibility (2026-01-27).
- [x] **Cross-Domain Swarming**: Extend swarm capabilities to non-music content domains
  - Status: done
  - Priority: P2
  - Notes: Extended swarm downloads to work with Movies, TV, Books, and GenericFile domains. Swarm system already domain-agnostic via hash-based matching. Backend selection rules enforced (Soulseek only for Music) (2026-01-27).

**Reference**: See `docs/multi-swarm-roadmap.md` and `docs/archive/planning/COMPLETE_PLANNING_INDEX.md` for detailed planning documents.

### Backlog Items (P2-P3)

**Priority**: P2-P3 (Low-Medium)  
**Status**: Most items verified complete, few optional enhancements remain  
**Reference**: `memory-bank/tasks-audit-gaps.md`

#### Phase 1 Gap Tasks
- [ ] **T-1400**: Unified BrainzClient
  - **Status**: ⏸️ Deferred (low priority)
  - **Priority**: P2
  - **Notes**: Current implementation uses separate clients (IMusicBrainzClient, IAcoustIdClient) which works well. Unified client would be nice-to-have but not critical. Verified 2026-01-27: Not needed - current approach is sufficient.

#### Phase 2 Gap Tasks
**Status**: ✅ **MOSTLY COMPLETE** (2026-01-27)
- [x] **T-1401**: Full library health scanning - ✅ Complete
- [x] **T-1402**: Library health remediation job execution - ✅ Complete
- [x] **T-1403**: Complete rescue service implementation - ✅ Complete
- [x] **T-1404**: Implement swarm download orchestration - ✅ Complete
- [x] **T-1405**: Implement chunk reassignment logic - ✅ **COMPLETE** (2026-01-27)
- [x] **T-1406**: Integrate playback feedback with scheduling - ✅ Complete
- [x] **T-1407**: Implement real buffer tracking - ✅ Complete

#### Phase 5 Gap Tasks
**Status**: ✅ **MOSTLY COMPLETE** (2026-01-27)
- [x] **T-1408**: Implement real search compatibility endpoint - ✅ Complete
- [x] **T-1409**: Implement real downloads compatibility endpoints - ✅ Complete
- [x] **T-1410**: Add jobs API filtering/pagination/sorting - ✅ **COMPLETE** (2026-01-27)

#### Phase 6 Gap Tasks
**Status**: ✅ **ALL COMPLETE** (2026-01-27)
- [x] **T-1411**: Complete shadow index shard publishing - ✅ Complete
- [x] **T-1412**: Complete scene service implementations - ✅ Complete
- [x] **T-1413**: Complete disaster mode integration - ✅ Complete

### Future Domain Support (P3 - Nice to Have)

**Priority**: P3 (Low Priority)  
**Status**: Current domains (Music, GenericFile) are sufficient for current use cases

#### Additional Content Domains
- [x] **Movies Domain**: Support for movie content matching and acquisition
  - Status: done
  - Priority: P3
  - Notes: Created `IMovieContentDomainProvider` and `MovieContentDomainProvider` with IMDB ID matching, hash verification, title/year matching. Models: `MovieWork`, `MovieItem`. Backend selection: mesh/DHT/torrent/HTTP/local only (NO Soulseek). Service registered in DI (2026-01-27).
- [x] **TV Domain**: Support for TV show/episode content
  - Status: done
  - Priority: P3
  - Notes: Created `ITvContentDomainProvider` and `TvContentDomainProvider` with TVDB ID matching, season/episode matching, series organization. Models: `TvWork`, `TvItem`. Backend selection: mesh/DHT/torrent/HTTP/local only (NO Soulseek). Service registered in DI (2026-01-27).
- [x] **Books Domain**: Support for book/document content
  - Status: done
  - Priority: P3
  - Notes: Created `IBookContentDomainProvider` and `BookContentDomainProvider` with ISBN-based matching, format detection (PDF, EPUB, MOBI, etc.). Models: `BookWork`, `BookItem`, `BookFormat` enum. Backend selection: mesh/DHT/torrent/HTTP/local only (NO Soulseek). Service registered in DI (2026-01-27).

- [x] **Custom Domain Matching Logic**: Extensible framework for domain-specific matching
  - Status: done
  - Priority: P3
  - Branch: `dev/40-fixes`
  - Notes: Created extensible framework for custom domain providers:
    - **Base Interface**: `IContentDomainProvider` - common contract for all domain providers with methods for identity mapping, metadata enrichment, content verification
    - **Provider Registry**: `ContentDomainProviderRegistry` - thread-safe registry for discovering and registering custom providers at runtime
    - **Adapter Classes**: `ContentDomainProviderAdapters` - adapters that wrap existing domain-specific providers (Music, Book, Movie, TV, GenericFile) to work with the registry
    - **Domain Type Updates**: Updated BookWork, BookItem, MovieWork, MovieItem, TvWork, TvItem to implement IContentWork/IContentItem interfaces
    - **Domain Mapping Helpers**: Created BookDomainMapping, MovieDomainMapping, TvDomainMapping classes for deterministic ID generation (similar to MusicDomainMapping)
    - **Service Registration**: `ServiceCollectionExtensions.AddContentDomainProviders()` - easy registration in DI
    - **Integration**: Registered in `Program.cs` - all built-in providers (Music, Book, Movie, TV, GenericFile) automatically registered with the registry
    - **Extensibility**: Custom providers can implement `IContentDomainProvider` directly and register via the registry API
    - **Complete**: All 5 domain providers (Music, Book, Movie, TV, GenericFile) now fully integrated with the extensible framework (2026-01-27)

### Optional Polish & Enhancements (P3)

**Priority**: P3 (Low Priority)  
**Status**: Current functionality is solid, these are quality-of-life improvements

#### UI/UX Improvements
- [x] **Enhanced Job Management UI**: More advanced filtering and visualization for download jobs
  - Status: done
  - Priority: P3
  - Branch: `dev/40-fixes`
  - Notes: Created comprehensive Jobs UI component (`System/Jobs/index.jsx`) with:
    - Job analytics dashboard (total, active, completed counts, by type/status)
    - Active swarm downloads display with real-time metrics (chunks/s, ETA, progress)
    - Filterable job list (by type, status) with sorting and pagination
    - Progress visualization for discography/label crate jobs
    - Auto-refresh for swarm jobs (5s interval)
    - All jobs API integration with filtering, sorting, pagination (2026-01-26)

- [x] **Advanced Search UI**: Enhanced search interface with filters
  - Status: done
  - Priority: P3
  - Branch: `dev/40-fixes`
  - Notes: Enhanced search UI with:
    - **Quality Presets**: Quick buttons for "High Quality (320kbps+)" and "Lossless Only" with clear option
    - **Sample Rate Filtering**: Added min sample rate (Hz) input field
    - **Format/Codec Filtering**: Added file extension filtering (e.g., flac, mp3, wav, m4a)
    - **Enhanced Source Selection**: Improved Pod/Scene provider selection UI with icons, better styling, and clear labels
    - **Filter Parsing/Serialization**: Updated to support `minsr:` (min sample rate) and `ext:` (extensions) filter syntax
    - All existing filter functionality preserved and enhanced (2026-01-26)

- [x] **Real-time Swarm Visualization**: Live dashboard showing active swarm downloads
  - Status: done
  - Priority: P3
  - Branch: `dev/40-fixes`
  - Notes: Created comprehensive Swarm Visualization component (`System/SwarmVisualization/index.jsx`) with:
    - **Job Overview**: Real-time status with chunks completed/total, active workers, chunks/second, ETA, progress bar
    - **Peer Contributions Table**: Detailed peer performance with:
      - Chunks completed/failed per peer
      - Bytes served per peer
      - Success rate calculation and visualization (color-coded progress bars)
      - Sorted by contribution (bytes served, chunks completed)
    - **Chunk Assignment Heatmap**: Visual grid showing chunk completion status:
      - Green squares for completed chunks
      - Gray squares for pending chunks
      - Tooltips showing chunk index and status
      - Auto-scaling grid layout
    - **Performance Metrics**: Trace summary data including:
      - Total events count
      - Duration calculation
      - Rescue mode indicator
      - Bytes by source/backend breakdown
    - **Integration**: Modal dialog accessible from Jobs component "View Details" button
    - **Auto-refresh**: Updates every 2 seconds for real-time visualization
    - **API Integration**: Uses `/multisource/jobs/{jobId}` and `/traces/{jobId}/summary` endpoints (2026-01-26)

#### Performance Optimizations
- [x] **Swarm Performance Tuning**: Optimize chunk scheduling algorithms
  - Status: done
  - Priority: P3
  - Branch: `dev/40-fixes`
  - Notes: Implemented chunk size optimization service (`Optimization/ChunkSizeOptimizer.cs`):
    - **Adaptive Chunk Sizing**: Automatically optimizes chunk size based on:
      - File size and peer count (targets 2 chunks per peer for optimal parallelism)
      - Average throughput (larger chunks for high throughput, smaller for low)
      - Average RTT (smaller chunks for high latency, larger for low)
    - **Constraints**: 64KB minimum, 10MB maximum, rounds to 64KB alignment
    - **Integration**: Automatically used in `MultiSourceDownloadService` when chunk size not specified
    - **Heuristics**: 
      - Base calculation: `fileSize / (peerCount * 2)` clamped to optimal range
      - Throughput adjustment: +50% for >5MB/s, -25% for <1MB/s
      - Latency adjustment: -20% for >500ms, +10% for <100ms
    - **Service Registration**: Registered in DI as singleton
    - **Fallback**: Uses default 512KB if optimizer unavailable or fails (2026-01-26)

- [x] **Database Optimization**: Optimize queries for large libraries
  - Status: done
  - Priority: P3
  - Branch: `dev/40-fixes`
  - Notes: Enhanced HashDb optimization with:
    - **Query Performance Monitoring**: Added query metrics tracking with slow query statistics API endpoint (`GET /api/v0/hashdb/optimize/slow-queries`)
    - **Query Profiling API**: Added endpoint to profile individual queries (`POST /api/v0/hashdb/optimize/profile`)
    - **Automatic Index Optimization**: Added optional automatic index optimization on startup via `HashDbOptimizationHostedService` (disabled by default, configurable)
    - **Enhanced Optimization Service**: Extended `IHashDbOptimizationService` with `RecordQueryMetric` and `GetSlowQueryStatsAsync` methods
    - All existing optimization features (index optimization, VACUUM/ANALYZE, database analysis) remain available via API (2026-01-27)

#### Documentation
- [x] **User Guides**: Comprehensive user documentation
  - Status: done
  - Priority: P3
  - Branch: `dev/40-fixes`
  - Notes: Created comprehensive user documentation:
    - **Getting Started Guide** (`docs/getting-started.md`):
      - Installation instructions for all platforms (Linux, macOS, Windows, Docker)
      - Initial configuration steps
      - Basic usage (searching, downloading, wishlist)
      - Security best practices
      - Next steps and resources
    - **Troubleshooting Guide** (`docs/troubleshooting.md`):
      - Connection issues (Soulseek, Mesh)
      - Download problems (stuck, slow, failing)
      - Performance issues (CPU, memory)
      - Configuration problems
      - Web interface issues
      - Feature-specific troubleshooting
      - Getting additional help
    - **Advanced Features Walkthrough** (`docs/advanced-features.md`):
      - Swarm downloads (how it works, monitoring, optimization)
      - Scene ↔ Pod bridging (unified search, privacy considerations)
      - Collections & sharing (creating, sharing, downloading)
      - Streaming (how it works, limitations)
      - Wishlist & background search
      - Auto-replace stuck downloads
      - Smart search ranking
      - Multiple download destinations
      - Job management & monitoring
      - Advanced configuration tips
    - **Documentation Index Updated**: Added links to new guides in `docs/README.md` (2026-01-26)

- [x] **Developer Documentation**: Enhanced developer resources
  - Status: done
  - Priority: P3
  - Branch: `dev/40-fixes`
  - Notes: Enhanced developer documentation:
    - **Enhanced Contributing Guide** (`CONTRIBUTING.md`):
      - Development setup instructions
      - Code style guidelines (C# and React)
      - Copyright header policy
      - Testing guidelines and examples
      - Debugging instructions
      - Project structure overview
      - Code review checklist
      - Links to key documentation
    - **API Documentation Guide** (`docs/api-documentation.md`):
      - Complete API reference with all endpoints
      - Authentication methods (Cookie, JWT, API Key)
      - Response formats (success, error/ProblemDetails)
      - Common patterns (pagination, filtering, sorting)
      - Error handling and status codes
      - Rate limiting information
      - API discovery methods
      - Frontend API library usage
      - WebSocket/SignalR information
      - Code examples (curl, JavaScript)
      - Best practices
    - **Documentation Index Updated**: Added API documentation link in `docs/README.md` (2026-01-26)

### Infrastructure & Tooling (P3)

**Priority**: P3 (Low Priority)  
**Status**: Current infrastructure is functional

#### Development Tools
- [x] **Enhanced Test Harnesses**: Improve test infrastructure
  - Status: done
  - Priority: P3
  - Branch: `dev/40-fixes`
  - Notes: Enhanced test infrastructure:
    - **Full Instance Test Harness**: `SlskdnFullInstanceRunner` already exists and is working for bridge tests
    - **Mesh Network Simulator**: `MeshSimulator` exists with network partition and message drop simulation
    - **Performance Benchmarking Suite**: Created comprehensive BenchmarkDotNet suite:
      - **HashDb Benchmarks** (`HashDbPerformanceBenchmarks.cs`):
        - Lookup performance (with/without cache, cache hits)
        - Query performance (size-based, sequential/parallel)
        - Write performance (single, batch)
        - Statistics retrieval
      - **Swarm Benchmarks** (`SwarmPerformanceBenchmarks.cs`):
        - Chunk size optimization for various file sizes and peer counts
        - Chunk assignment (sequential and parallel)
        - Peer selection based on metrics
      - **API Benchmarks** (`ApiPerformanceBenchmarks.cs`):
        - GET endpoint performance (session, application state, HashDb stats, jobs)
        - POST endpoint performance (create search)
        - Concurrent request handling
      - **Transport Benchmarks**: Already exists (`TransportPerformanceBenchmarks.cs`)
      - **Benchmark Project**: Created `tests/slskd.Tests.Performance/` with proper BenchmarkDotNet setup
      - **Documentation**: Created `README.md` with usage instructions and performance targets (2026-01-26)

- [x] **CI/CD Enhancements**: Expand automated testing
  - Status: done
  - Priority: P1
  - Notes: Created `.github/workflows/ci-enhancements.yml` with three parallel jobs: (1) Performance regression testing - runs BenchmarkDotNet suite, compares against baseline, uploads results; (2) Load testing - uses k6 for API load testing (10→50→100 users, sustained load, performance thresholds); (3) Security scanning - CodeQL for C#/JS static analysis, Trivy for container scanning, dependency vulnerability scanning (NuGet/npm). Runs on PRs, pushes to master, tags, and weekly schedule. All results uploaded as artifacts with 30-day retention. Updated CHANGELOG (2026-01-27).

#### Monitoring & Observability
- [x] **Advanced Metrics**: Enhanced Prometheus metrics
  - Status: done
  - Priority: P1
  - Notes: Created SwarmMetrics.cs (swarm downloads, chunks, bytes, speeds, durations), PeerMetrics.cs (RTT, throughput, bytes transferred, chunks requested/completed, reputation), ContentDomainMetrics.cs (content indexed, lookups, downloads, quality scores). Integrated metrics into MultiSourceDownloadService (swarm downloads, chunk completion with status labels), PeerMetricsService (RTT, throughput, chunk completion tracking). All metrics use Prometheus.Metrics with proper labels and histogram buckets. Build successful (2026-01-27).

- [x] **Distributed Tracing**: Add OpenTelemetry support
  - Status: done
  - Priority: P3
  - Branch: `dev/40-fixes`
  - Notes: Comprehensive OpenTelemetry distributed tracing:
    - **Configuration**: `telemetry.tracing` options (enabled, exporter, jaeger/otlp endpoints)
    - **Activity Sources**: Dedicated sources for MultiSource, Mesh, HashDb, Search
    - **Swarm Download Tracing**: Complete lifecycle tracing with chunk-level events
    - **Mesh Network Tracing**: DHT operations (store, find_value, find_node)
    - **HashDb Tracing**: Lookup operations with cache tracking
    - **Search Tracing**: Search start operations with query/provider info
    - **Automatic Instrumentation**: ASP.NET Core and HTTP client
    - **Exporters**: Console (default), Jaeger, OTLP support
    - **Documentation**: Updated `config/slskd.example.yml` (2026-01-26)

---

## Summary

**Total Future Work Items**: ~25-30 items across 5 categories

**Priority Breakdown**:
- **P1 (Quality)**: 4 items (Testing expansion)
- **P2 (Features)**: 5-10 items (Multi-Swarm Phase 6+, backlog)
- **P3 (Polish)**: 15-20 items (Future domains, UI improvements, infrastructure)

**Recommendation**: 
- Focus on **Testing Expansion** (P1) for quality assurance
- **Multi-Swarm Phase 6+** when ready for new feature development
- **Backlog items** as time permits (most are already complete)
- **Future domains** and **polish** as user feedback indicates need

**Current State**: Codebase is in excellent shape. All critical features complete. Future work is optional enhancements and quality improvements.

## 2026-03-21 Completed Follow-up

- [x] Add explicit regression coverage for intentionally-public protocol endpoints
  - Status: done
  - Notes: Added `PublicProtocolAnonymousActionTests` to lock down the approved anonymous action set for session bootstrap, profile lookup, token-backed streaming, ActivityPub delivery, and WebFinger discovery after the controller-by-controller auth review.
- [x] Remove controller-level anonymous defaults from public protocol surfaces
  - Status: done
  - Notes: Tightened streaming and federation controllers to auth-by-default at class scope with per-action `[AllowAnonymous]`, then revalidated the exact public action set in tests.
- [x] Fix release-gate cancellation validator race
  - Status: done
  - Notes: Updated `AsyncRules.ValidateCancellationHandlingAsync` to cancel explicitly and allow a bounded grace window, which removed the flaky `.81` release-gate failure in `AsyncRulesTests`.
- [x] Fix residual `.82` release-gate timing flakes
  - Status: done
  - Notes: Reworked the remaining timing-sensitive `AsyncRulesTests` path to use deterministic task completion on cancellation and widened the `SecurityUtils.RandomDelayAsync` upper sanity bound so CI scheduler latency no longer fails the stable gate.
- [x] Fix residual `.83` cover-traffic async-enumerable test flake
  - Status: done
  - Notes: Reworked `CoverTrafficGeneratorTests.GenerateCoverTrafficAsync_GeneratesMessagesWithCorrectSize` so it cancels after collecting the first message instead of using a timeout as the normal completion path; validated with the focused mesh/privacy suite, the full release gate, and `./bin/lint`.

## 2026-03-28 Completed Follow-up

- [x] Fix packaged Web UI defaults so release installs center HTTP on `5030`
  - Status: done
  - Notes: Updated packaged `slskd.service` to pass `--config /etc/slskd/slskd.yml`, changed packaged `slskd.yml` defaults to disable HTTPS on `5031`, and added a login-page HTTPS hint that points users to `:5031` only when they are currently on HTTP.

## 2026-04-07 Completed Follow-up

- [x] Add guard rails so GitHub actions from this checkout cannot drift to upstream `slskd/slskd`
  - Status: done
  - Notes: Pinned `gh` default repo to `snapetech/slskdn`, added `scripts/verify-github-target.sh`, and updated repo AI instructions so upstream is treated as read-only reference only.
- [x] Make initial share scans less aggressive by default for issue `#193`
  - Status: done
  - Notes: Changed `shares.cache.workers` to a conservative default based on host CPU count, added focused unit coverage for the default calculation, and documented the knob more clearly in config/docs so operators can tune it further.
- [x] Fix issue `#199` browse cache rebuild collisions
  - Status: done
  - Notes: Changed browse-cache readers to allow replacement while streaming, serialized browse-cache rebuilds behind a semaphore, kept temp files in the data directory for atomic replacement, and added focused unit coverage for replacing the cache while a reader is active.

## 2026-04-13 Completed Follow-up

- [x] Clean up release notes so each published release only lists new changes
  - Status: done
  - Notes: Removed the tagged-release fallback to `docs/CHANGELOG.md` `## [Unreleased]`, taught the generator to resolve previous published release ranges even when builds start from `build-main-*` / `build-dev-*` tags, rewrote the latest three changelog sections as explicit per-release deltas, and prepared the GitHub release cleanup to keep only the newest three releases.
- [x] Block the Soulseek loopback-listener misconfiguration that makes peer ops fail after login
  - Status: done
  - Notes: Reproduced the `logged in but all peer connections fail` path against local Soulfind, proved it was caused by `Soulseek.ListenIpAddress = 127.0.0.1` advertising an unreachable external endpoint, then added startup validation plus focused unit coverage so live clients must use `0.0.0.0` or another reachable interface.

## 2026-04-15 Completed Follow-up

- [x] Eliminate the remaining Dependabot major-version holds by doing the upgrades instead of ignoring them
  - Status: done
  - Notes: Removed all major-version ignore blocks from `.github/dependabot.yml`, upgraded the web app to React 18 / React Router 7 / `uuid` 13 / `@uiw/react-codemirror` 4.25.9 / `jsdom` 29.0.2, moved the backend and test projects to `net10.0`, and updated the held NuGet major lines in `src/slskd/slskd.csproj` plus the test projects.
- [x] Fix the breakages introduced by those dependency/runtime jumps and prove the upgraded stack still works
  - Status: done
  - Notes: Migrated router usage off v5 APIs, added the missing `@testing-library/dom` peer required by the upgraded test stack, fixed the backend compile breaks from Swashbuckle / Soulseek / .NET 10 API changes, documented both upgrade gotchas in ADR-0001, and revalidated lint/build/tests on the new stack.
- [x] Isolate why full-solution backend test commands still hang after passing output under `.NET 10`
  - Status: done
  - Notes: The lingering tail was not one generic `.NET 10` harness bug. It was two integration-test-specific stalls: `BridgeProxyServerIntegrationTests` started a full bridge instance without preflighting the external `soulfind` dependency, and `DisasterModeTests.Disaster_Mode_Recovery_Should_Deactivate_When_Soulfind_Returns` burned the hang timeout on blind sleeps. After fixing those test paths, `dotnet test slskd.sln -v minimal` completed with passing counts across `slskd.Tests`, `slskd.Tests.Unit`, and `slskd.Tests.Integration`.

## 2026-04-09 Completed Follow-up

- [x] Fix GitHub issues `#200`, `#201`, and `#202`
  - Status: done
  - Notes: Cleaned up the remaining Web UI route/API regressions (`/api/v0` double-prefix helpers, Bridge payload handling, search-row navigation, dark-theme Network statistics), added service-worker registration plus a shipped worker so Android can install the app as a real PWA, and surfaced the confirmed listen-port/firewall guidance directly in the Network page and troubleshooting docs.

## 2026-04-06 Completed Follow-up

- [x] Re-verify reopened tester regressions `#193` and `#194` with live repro coverage
  - Status: done
  - Notes: Added a full-instance CSRF regression test for the Web UI rescan path, added focused expected-network-exception unit coverage, and fixed the integration harness to launch the freshly built `Debug` app binary instead of a stale `Release` executable.
- [x] Stabilize the release gate after `build-main-0.24.5-slskdn.115` failed on a flaky timing microbenchmark
  - Status: done
  - Notes: Removed the stopwatch-ratio CI assertion from `SecurityUtilsTests`, kept deterministic correctness coverage, documented the gotcha in ADR-0001, and re-ran `packaging/scripts/run-release-gate.sh` successfully.

## 2026-03-29 Completed Follow-up

- [x] Harden Launchpad PPA uploads against passive FTP / transient transport failures
  - Status: done
  - Notes: Enabled `passive_ftp = 1` and added bounded retry loops in all Launchpad upload workflows after the stable `107` tag run proved package generation/signing was fine but the FTP transfer could still fail with Launchpad-side `550` transport errors.
- [x] Sync stable package metadata to the latest published stable release and fix the auto-sync workflow
  - Status: done
  - Notes: Aligned the checked-in stable metadata baseline to `0.24.5-slskdn.105` and added `packaging/scripts/update-stable-release-metadata.sh` so future successful stable tag runs update the full metadata set on `main` instead of partially drifting on the old `master` target.
- [x] Fix Docker image HTTP binding so published ports are reachable from the host
  - Status: done
  - Notes: Reproduced the failure locally with `docker build` + `docker run` and confirmed the image was binding HTTP to container loopback only; fixed `Dockerfile` to export `SLSKD_HTTP_ADDRESS=0.0.0.0` and re-verified host-side `/health` and `/` reachability without any manual override env.
- [x] Merge the detached `build-main-0.24.5-slskdn.92` through `.101` history back into `main`
  - Status: done
  - Notes: Merged the previously tag-only side lineage with merge commit `e74d4df1` instead of cherry-picking, resolved the runtime conflicts in `Program`, `RelayService`, and `SongIdService`, updated `docs/CHANGELOG.md`, and confirmed `git tag --no-merged main` is empty afterward.
- [x] Fix SongID YouTube runs so missing `yt-dlp` degrades instead of failing
  - Status: done
  - Notes: Reproduced the `local test host` failure for `https://youtu.be/K3wtamktLGs?si=oJjRPxd_fV31TcLd`, confirmed the host was missing `yt-dlp`, hardened `SongIdService` to continue with metadata-only analysis when `yt-dlp` is absent, fixed the empty-clip scorecard aggregate crash exposed by that fallback path, added focused SongID unit coverage, and updated AUR / Proxmox packaging to install `yt-dlp`.
- [x] Make Search page boxes collapsible and keep Search Results open by default
  - Status: done
  - Notes: Added page-level collapsible wrappers around the Search, SongID, MusicBrainz Lookup, Discovery Graph Atlas, Album Completion, and Search Results panels in `src/web/src/components/Search/Searches.jsx`; Search Results now starts expanded so newly-created searches remain immediately visible.
- [x] Fix SongID job actions and multi-search batching on the Search page
  - Status: done
  - Notes: Updated `src/web/src/lib/jobs.js` to use the native jobs API's snake-case request fields so SongID actions like `Plan Discography` and album planning work again, and updated `src/web/src/lib/searches.js` to retry the backend's known serialized-create `429` response so batch search actions no longer fail when multiple searches are queued from one UI action.
- [x] Prevent SongID artist-graph stalls on large MusicBrainz discographies
  - Status: done
  - Notes: Time-boxed `AddArtistCandidatesAsync()` release-graph fetches so SongID no longer gets pinned at `38%` in `artist_graph` for large artists like Taylor Swift; the stage now falls back to a lightweight artist candidate when release-graph expansion times out or fails.
- [x] Tighten SongID-generated search strings to canonical `Artist - Track` format
  - Status: done
  - Notes: Replaced the permissive SongID query joins with a dedicated `BuildTrackSearchText()` helper so generated search actions no longer stuff uploader/album/title cruft into Soulseek searches; added focused SongID unit coverage for segment and fallback query formatting.
- [x] Automate stable Winget submission from the main release workflow
  - Status: done
  - Notes: Historical implementation added a `winget-main` job in `build-on-tag.yml`; this was later superseded by the opt-in manual `Publish Winget` workflow to avoid noisy public PRs for routine stable releases.
- [x] Fix initial stable Winget PR service validation
  - Status: done
  - Notes: Replaced the temporary singleton submission with repository-shaped multi-file manifest staging for stable Winget submissions, and tightened staging to copy only the three stable manifest files so `snapetech.slskdn-dev` manifests cannot leak into the stable PR.
- [x] Make stable Winget publication opt-in
  - Status: done
  - Notes: Removed the automatic `winget-main` job from tag-based main releases. Stable releases still regenerate local Winget metadata, but public `microsoft/winget-pkgs` PRs now use the manual `Publish Winget` workflow only for high-value releases.
- [x] Filter release-hygiene docs commits out of generated release note commit lists
  - Status: done
  - Notes: Updated `scripts/generate-release-notes.sh` so `Included Commits` excludes standalone ADR gotcha commits, release-notes doc commits, and stable metadata bookkeeping commits that otherwise make one fix appear multiple times in GitHub release output.
- [x] Fix DHT rendezvous bootstrap defaults behind issue #209
  - Status: done
  - Notes: Replaced the random fallback DHT UDP port with a stable default (`50306`), added startup validation so enabled DHT cannot run on port `0`, updated the example config, and made bootstrap timeout logs explicitly tell operators that announce/discovery remain disabled until the configured UDP port is reachable.

## 2026-04-17 Completed Follow-up

- [x] Fix issue `#209` at the actual DHT bootstrap root cause instead of adding more operator-facing logging
  - Status: done
  - Notes: Reproduced the failure in a bare MonoTorrent `3.0.2` probe, confirmed the older package stalls with `nodes=0`, upgraded to `MonoTorrent 3.0.3-alpha.unstable.rev0049`, made slskdn pass explicit `dht.bootstrap_routers`, added startup validation, and updated the example config.

- [x] Make runtime state expose executable/base/config paths for release-debugging
  - Status: done
  - Notes: Added startup/runtime self-identification after issue #209 proved a user can think they installed a new zip while the live process is still an older binary.

- [x] Ship a supported Linux release installer path with stable GitHub releases
  - Status: done
  - Notes: Stable releases now publish `install-linux-release.sh` plus the Linux service/config helper assets so release users upgrading from an existing `slskd` service do not have to hand-wire the new binary path.
- [x] Fix stable package metadata so Nix smoke fetches the currently published stable assets
  - Status: done
  - Notes: Reverted the stable metadata consumers from unreleased `slskdn-main-linux-glibc-*` names back to the real `0.24.5-slskdn.131` asset names (`slskdn-main-linux-x64.zip` / `slskdn-main-linux-arm64.zip`) and updated `packaging/scripts/update-stable-release-metadata.sh` plus packaging validation to stop jumping ahead of the published release.

- [x] Fix issue `#209` follow-on noise after DHT bootstrap succeeds
  - Status: done
  - Notes: Classified `Connection reset by peer` as expected Soulseek network churn, made safe requests clear and reissue stale antiforgery cookies after reinstall/key-ring changes, downgraded obvious non-overlay TLS garbage on the public mesh port to debug noise, and added focused unit/integration coverage for all three regressions.

- [x] Fix issue `#209` follow-up route mismatch and bogus overlay hole-punch preflight
  - Status: done
  - Notes: Restored `GET /api/v0/users/notes` by versioning `UserNotesController` for `v0`, added integration coverage for that route, removed the mesh connector's fake UDP hole-punch preflight against DHT-discovered TCP overlay endpoints, and clarified that hole-punch logs report ephemeral local UDP sockets rather than randomized configured listener ports.

- [x] Fix issue `#209` mesh split-brain where DHT neighbors never reached circuit maintenance
  - Status: done
  - Notes: Added `MeshNeighborPeerSyncService` so successful `MeshNeighborRegistry` add/remove events mirror into `IMeshPeerManager`; added unit coverage that reproduces the old empty-peer state without the sync service and proves the peer inventory populates when the service is running.

- [x] Fix `DownloadService.EnqueueAsync(...)` semaphore lifetime so live enqueue cleanup cannot crash after `Queued, Remotely`
  - Status: done
  - Notes: Stopped disposing the per-batch enqueue semaphore while background enqueue observer tasks still release it, added focused `DownloadServiceTests` regression coverage for the cancelled-transfer path, redeployed a self-contained build to `local test host`, and verified the old `ObjectDisposedException` / `SemaphoreSlim` crash is gone.
- [x] Investigate post-enqueue remote stream failures on `local test host`
  - Status: done
  - Notes: Confirmed the remaining mixed remote stream outcomes are normal peer-side churn rather than another host-wide local transfer bug; fixed the lingering fake fatal `Transfer failed: Transfer complete` unobserved-task noise, opened the missing host firewall rules for `50305/tcp` and `50306/udp`, and proved DHT reaches `Ready` on `local test host` once the host firewall is open.
- [ ] Revisit the DHT bootstrap diagnostics after more live-runtime samples
  - Status: pending
  - Notes: The startup grace period is now 120 seconds instead of 30, which matches the slow-but-healthy bootstrap observed on `local test host`. Gather more host samples before deciding whether the warning should become adaptive instead of static.

- [x] Downgrade remote peer transfer rejections from fake fatal host telemetry
  - Status: done
  - Notes: Extended the expected Soulseek-network classifier so remote-declared transfer failures (`TransferReportedFailedException` / `Download reported as failed by remote client`) are treated as expected peer churn for unobserved-task logging instead of `[FATAL]` crash noise.
- [x] **chore (2026-04-18):** Rework AUR package layout so `/usr/lib/slskd` stays the drop-in launcher path while bundled releases install under `/usr/lib/slskd/releases/<version>` with `/usr/lib/slskd/current`, preventing pacman upgrade conflicts from stale root-level payload files.
- [x] **chore (2026-04-18):** Rework the AUR package layout so `/usr/lib/slskd` remains the drop-in launcher path while packaged releases install under `/usr/lib/slskd/releases/<version>` with `/usr/lib/slskd/current`, preventing future pacman upgrades from colliding with stale root-level bundle files.
- [x] **fix (2026-04-18):** Patch the Linux DEB/RPM package builds so Fedora-family installs do not fail on `liblttng-ust.so.0`, and keep the RPM payload on the same `/usr/lib/slskd` drop-in path as the shared service file instead of drifting to `%{_libdir}`.
- [x] **fix (2026-04-18):** Add explicit ICU runtime dependencies to the DEB and RPM package metadata so clean installs can actually start `slskd` instead of failing on missing globalization libraries.

- [x] **fix (2026-04-18):** Fix `packaging/linux/install-from-release.sh` cleanup so successful installs do not exit nonzero from an out-of-scope `EXIT` trap, and re-smoke the published raw Linux release installer on a clean Ubuntu container.

- [x] **fix (2026-04-18):** Add `patchelf` to Debian `Build-Depends` so Launchpad/PPA builds install the tool required by `debian/rules` during package assembly.

- [x] Validate `local test host` yay package `0.24.5-slskdn.170` and fix duplicate startup descriptor publish noise
  - Status: completed (2026-04-21)
  - Notes: Confirmed the installed package, CLI/API version, service state, Soulseek login, shares, DHT, and overlay listener are healthy on `local test host`. Current-process logs have no fresh fatal/error/exception/502/coredump/search-rate noise after the auto-replace cycle. Fixed duplicate startup MeshDHT self-descriptor publication by letting `MeshBootstrapService` own the startup publish and starting `PeerDescriptorRefreshService` periodic scheduling from current time. Validation passed with focused and full unit tests, Release build, lint, and diff check.

- [x] Remove Snap publishing from release workflows
  - Status: completed (2026-04-21)
  - Notes: Deleted dev/stable Snap publish jobs from the tag workflow, converted the manual dev helper workflow to Docker-only, and removed Snap manifest update/validation from release metadata automation. Future tag builds should no longer wait on Snap Store publication.

- [x] Fix issue `#209` root split between DHT discovery, circuit peer inventory, and stale antiforgery recovery
  - Status: done
  - Notes: DHT-discovered rendezvous peers now publish into `IMeshPeerManager` immediately so circuit maintenance sees nonzero onion-capable peers even before overlay neighbor registration completes, connection success/failure updates now refine those peer records, and stale antiforgery cookie recovery now retries on any key-ring/decryption exception shape instead of only `AntiforgeryValidationException`.

- [x] Fix Jammy PPA and standalone distro workflow drift after the packaging/toolchain changes
  - Status: done
  - Notes: Updated the standalone PPA/COPR/Linux release workflows to use `.NET 10`, added publish-output verification for the staged Linux bundle, and hardened the DEB/RPM runtime SONAME patching so it discovers `libcoreclrtraceptprovider.so` in the staged package tree instead of assuming one flat appdir path.

- [x] Fix issue `#209` direct-mode circuit selection so DHT-ready peers do not still depend on a local Tor SOCKS proxy
  - Status: done
  - Notes: Added a real `DirectTransport`, changed `AnonymityTransportSelector` so `AnonymityMode.Direct` registers and prioritizes that transport instead of Tor, and added focused unit coverage that reproduces the old `No anonymity transport is available` failure path when Tor is absent.

- [x] Fix issue `#209` stale antiforgery GET spam and DHT enabled-status drift
  - Status: done
  - Notes: Reproduced the stale XSRF cookie spam directly on `local test host`, moved safe-request antiforgery cleanup ahead of `GetAndStoreTokens()` so ASP.NET never deserializes stale cookies on token-minting GETs, and corrected `/api/v0/dht/status` so `isEnabled` reflects configured DHT enablement instead of current readiness. Validated on `local test host`: the stale-cookie curl no longer emits decrypt stack traces, and the DHT status API now reports `isEnabled: true` during bootstrap instead of falsely claiming DHT is disabled.

- [x] Fix issue `#209` overlay pin-mismatch recovery so stale TOFU pins do not partition the mesh
  - Status: done
  - Notes: Reproduced the live failure on `local test host` with a stale stored pin for `minimus7`, proved the old behavior hard-blocked the peer after a normal cert rotation, changed inbound and outbound overlay handshakes to rotate stored TOFU pins instead of auto-banning on mismatch, added focused `CertificatePinStoreTests`, and validated on `local test host` that the stale-pin path now logs the mismatch, rotates the pin, and still registers/connects the neighbor in the same run.

- [x] Fix issue `#209` peer stats so DHT candidates do not masquerade as verified onion-capable peers
  - Status: done
  - Notes: Stopped marking DHT-discovered endpoints as `supportsOnionRouting=true` before any overlay handshake succeeds, updated DHT rendezvous tests so failed immediate connects stay tracked as `dht-discovered` candidates instead of circuit-capable peers, and validated on `local test host` that `/api/v0/security/peers/stats` now reports `onionRoutingPeers: 0` while raw DHT candidates are still visible separately.

- [x] Add upload diagnostics for Bas's failed-upload report
  - Status: completed (2026-04-26)
  - Notes: Added structured `[UPLOAD-DIAG]` logs around inbound upload enqueue requests and an authenticated `/api/v0/transfers/uploads/diagnostics` endpoint that probes the configured local Soulseek listener, summarizes share/login/upload state, and returns actionable warnings.

- [x] Investigate tester upload/DHT onboarding feedback
  - Status: completed (2026-04-26)
  - Notes: Confirmed upload failures need listener/port/share/enqueue diagnostics from the tester. Fixed the slskdN-side DHT warning/config discoverability mismatch by documenting `dht.lan_only` in the sample config and using YAML option names in the warning text.

- [x] Fix mesh self-descriptor publication on QUIC-unsupported hosts
  - Status: done
  - Notes: Reproduced on `local test host` that `PeerDescriptorPublisher` was auto-advertising fake `2234/2235` endpoints and impossible `DirectQuic` transports while `QuicListener.IsSupported` was false. Updated descriptor publication to derive legacy endpoints from the real UDP overlay listen port and to suppress direct QUIC transport advertisement when the host cannot actually accept QUIC. Validated on `local test host`: published self descriptor now logs `endpoints=4 transports=0` instead of poisoning DHT with impossible direct candidates.

- [ ] Add a non-QUIC direct mesh transport path or runtime dependency gate
  - Status: pending
  - Notes: The audit on `local test host` showed a deeper architecture gap remains after the descriptor fix: `TransportSelector` still only has `DirectQuicDialer` for clearnet mesh transport, so QUIC-unsupported hosts cannot build direct circuits at all. Either wire the existing direct TLS transport into mesh dialing or add a startup/package gate that makes this unsupported state explicit before release.

- [x] Verify DHT rendezvous overlay search and transfer between two full local slskdN instances
  - Status: done
  - Notes: Added a deterministic full-instance integration test that starts alpha/beta subprocesses, connects alpha to beta through the real overlay API, searches beta's advertised pod content over mesh search, downloads the content through `MeshContent.GetByContentId` service calls over the DHT overlay, and byte-compares the downloaded file. Fixed missing overlay service transport, pod routing metadata preservation, and service router DI registration uncovered by the test.

- [x] Fix startup directory browse noise when Soulseek is still logging in
  - Status: done
  - Notes: Live `local test host` build `0.24.5-slskdn.159` held the mesh framer fix past the keepalive window, but a frontend/API directory request during `Connected, LoggingIn` still produced a noisy 500. `UsersController.Directory` now returns 503 until the Soulseek client is connected and logged in, with focused unit coverage.

- [x] Fix auto-replace search finalization race seen on `local test host`
  - Status: done
  - Notes: Build `159` logged `No search responses found` for auto-replace searches that completed with responses seconds later. `AutoReplaceService` now waits for the persisted completed search state before treating responses as absent, with focused unit coverage for delayed finalization.

- [x] Fix AudioSketch ffmpeg PATH resolution on `local test host`
  - Status: done
  - Notes: Live build `159` repeatedly logged `[AudioSketch] ffmpeg not configured or missing: ffmpeg` even though `/usr/bin/ffmpeg` was installed. `AudioSketchService` now resolves configured command names through `PATH` before declaring the tool missing, with focused unit coverage.

- [x] Restore QUIC mesh runtime compatibility on `local test host`
  - Status: done
  - Notes: Replaced crashing AUR `msquic 2.4.11` with Microsoft MsQuic `v2.5.7`, removed the temporary systemd QUIC-disable override, and deployed `manual.90257b10d`. QUIC listeners `50401/50402`, overlay `50305`, DHT, and one mesh connection are healthy after restart. App code now gates QUIC service registration and direct-QUIC publication on `QuicRuntime.IsAvailable()`.

- [x] Fix live overlay framer compatibility with unframed JSON control messages
  - Status: done
  - Notes: Live `local test host` build `159` disconnected `m***7` at the two-minute keepalive with `Invalid message length: 2065855609` (`{"ty`). `SecureMessageFramer` now accepts capped unframed JSON objects at frame boundaries, with focused unit coverage and live validation past the keepalive threshold.

- [x] Fix DHT rendezvous connector-capacity accounting
  - Status: done
  - Notes: Live stats showed DHT attempts exceeding real connector attempts when more candidates arrived than the connector's concurrent-attempt limit. Rendezvous now defers candidates before stamping retry/backoff state when connector capacity is full, with focused unit coverage.

- [x] Fix user directory browse connection-failure API noise
  - Status: done
  - Notes: Live `local test host` logs showed remote peer directory connection failures escaping as repeated middleware stack traces. `UsersController.Directory` now returns a controlled 503 for `SoulseekClientException` wrapping `ConnectionException`, with focused unit coverage.

- [x] Fix systemd restart SIGTERM handling
  - Status: done
  - Notes: Manual deployments showed normal `systemctl restart slskd` stops recorded as `status=1/FAILURE`. POSIX signal handlers now request generic-host shutdown instead of `Environment.Exit(1)`, and `ProcessExit` logs expected shutdown as informational. Validated on `local test host` with a deliberate restart of `manual.0a542e1c9`.

- [x] Fix transfer cleanup ordering during service shutdown
  - Status: completed (2026-04-19)
  - Notes: `DownloadService` now drains in-flight download/enqueue tasks before `Application.StopAsync` disposes the shared Soulseek client, which removed the restart-time global semaphore warnings and disposed-object cleanup noise on live `local test host` restarts. A second live shutdown race in `SoulseekClient.Disconnect()` (`Sequence contains no elements`) is now caught and downgraded during expected shutdown so clean restarts do not emit false fatal termination logs.

- [x] Fix local test host QUIC/native crash mitigation and Soulseek listener fake-fatal noise
  - Status: completed (2026-04-21)
  - Notes: Live manual-build soak found a native `SIGSEGV` restart while QUIC listeners were active and a recovered-process fake fatal from Soulseek.NET listener socket disposal. QUIC control/data now require explicit operator opt-in, UDP overlay remains enabled by default, listener socket disposal is classified as expected Soulseek network teardown, and verbose startup/SPA fallback/CSRF request logs were demoted to debug. Post-deploy passes also exposed controlled offline user-info `404`s still logging `UserOfflineException` stacks and shutdown-cancelled background searches logging false errors; both now log as expected operational outcomes. Final deployed manual build `0.24.5-slskdn.165+manual.15ba2a423` passed a full bounded Playwright route/tab sweep with `307` visits, `0` issues, and no HTTP 5xx/502s.

- [x] Validate `local test host` yay package `0.24.5-slskdn.168` and fix actionable noise
  - Status: completed (2026-04-21)
  - Notes: Confirmed the installed package, CLI, release symlink, and authenticated API all report `0.24.5-slskdn.168`; service is active after a clean restart with Soulseek logged in, shares ready, DHT running, and expected listeners present. Fixed the transient overlay `50305` bind race by retrying startup binds, demoted remaining startup method-trace logs to debug, and made release Discord/Matrix announcement webhooks retry/non-fatal after the `168` run went red only on a Matrix HTTP 504 after artifacts were already published. Validation passed with YAML parse, focused DHT tests, full unit tests, Release build, lint, and diff check.

- [x] Validate `local test host` yay package `0.24.5-slskdn.170` and quiet remaining overlay log noise
  - Status: completed (2026-04-21)
  - Notes: Confirmed the installed package/API still report `0.24.5-slskdn.170`, systemd is active with zero restarts, Soulseek is logged in, shares are ready, DHT is running, overlay TCP is listening, and current-process logs/coredumps show no actionable fatal/error/exception/502/bind/protocol issues. The only fixable noise was per-endpoint overlay cooldown streak detail at information level; that detail is now debug-level while aggregate DHT/overlay summaries remain visible.

- [x] Validate `local test host` yay package `0.24.5-slskdn.171` and fix Soulseek timeout fake-fatal classifier
  - Status: completed (2026-04-21)
  - Notes: Confirmed the installed package and binary are `171`; restarted the service because systemd was still running the previous `170` PID after package installation. The real `171` process reports the correct version/path, Soulseek is logged in, shares are ready, API is responsive, and the duplicate MeshDHT descriptor publish is gone. A pre-restart fake fatal from Soulseek.NET read-loop timeout churn exposed a classifier gap; `Connection timed out` and `Unable to read data from the transport connection` inner exception messages are now treated as expected Soulseek network churn with focused coverage.

- [x] Validate `local test host` yay package `0.24.5-slskdn.172` and clean startup polish
  - Status: completed (2026-04-21)
  - Notes: Confirmed the installed package/API are `172`, service is active with zero restarts, Soulseek is logged in, DHT is ready, overlay TCP is listening, mesh counters are clean, and fresh logs/coredumps show no fatal/error/exception/502/bind/protocol issues. Fixed remaining startup polish by demoting temporary raw config probes to debug and normalizing blank identity display names before profile persistence and LAN discovery advertisement.

- [x] Sweep `local test host` 172 logs/Web UI and quiet remaining false warnings
  - Status: completed (2026-04-21)
  - Notes: Authenticated Web UI route/tab validation found no real 5xx/502/page regressions; earlier tab and SongID hub findings were crawler/navigation abort artifacts. Live logs showed no fatal/error/exception noise, but did show repeated finite-sample entropy warnings and expected auto-replace no-result searches at warning level. Entropy sampling now uses a stable 4096-byte sample, auto-replace no-result telemetry logs at debug, and the full unit-suite pass also fixed a flaky hosted-service test wait exposed during validation.

- [x] Continue issue `#209` live mesh/search diagnosis on `local test host`
  - Status: completed (2026-04-22)
  - Notes: Fixed public self-descriptor advertisement so only public-routable auto-detected interfaces are published and configured endpoints are not supplemented with private/container/VPN addresses. Added mesh-search peer outcome logging. Deployed `0.24.5-slskdn.174+manual.6fce6575c` to `local test host` and proved the current search path works: core Soulseek returned `252` responses / `16686` files for `radiohead`, while mesh fanout reached one active peer and got an empty response (`peers=1 peersWithResults=0 emptyPeers=1 failedPeers=0`).

- [x] Add optional live-account mesh search/transfer smoke
  - Status: completed (2026-04-22)
  - Notes: Added and live-validated a full-instance integration smoke that uses `tests/slskd.Tests.Integration/local-mesh-accounts.env` or matching environment variables to start two real slskdN processes with live Soulseek test credentials, wait for login, host a generated probe file on beta, mesh-search it from alpha, download it through the pod path, and byte-compare the transfer. Fresh short alphanumeric Soulseek test accounts were generated, stored in the gitignored env file and in OpenBao at `secret/slskdn/mesh-live-test-accounts`, and `TwoNodeMeshFullInstanceTests` passed with the public-network live-account path exercised.
- [x] Fix Soulseek listen endpoint reconnect semantics for upload reachability
  - Status: completed (2026-04-26)
  - Notes: Deep upload-path audit found that runtime listen endpoint changes can move the local Soulseek.NET listener without forcing server endpoint advertisement to refresh. Marked `soulseek.listen_ip_address` and `soulseek.listen_port` as reconnect-required and added regression coverage for connected option changes setting `PendingReconnect`.
- [x] Build CSV playlist import into Wishlist for issue #216
  - Status: completed (2026-04-26)
  - Notes: Added `POST /api/v0/wishlist/import/csv` and a Wishlist page import modal for TuneMyMusic-style CSV exports. Rows are imported as conservative wishlist searches with optional auto-download, filter, max results, enabled state, and album inclusion; import deduplicates against existing/imported rows and does not immediately burst-search the Soulseek network.

- [x] Adapt upstream 0.24.5-to-current packaging/runtime alignment for slskdN
  - Status: completed (2026-04-29)
  - Notes: Implemented slskdN-native IPv4-mapped IPv6 normalization, null-safe config diffs, retry callback plumbing, Docker `PUID`/`PGID`/`--user` entrypoint handling, packaging validation guards, and direct-download retry/resume/batch metadata without copying upstream implementation text.

- [x] Add transfer retry/resume and batch grouping support
  - Status: completed (2026-04-29)
  - Notes: Added configurable `global.download.retry`, transfer `BatchId`/`Attempts`/`NextAttemptAt` persistence, migration coverage, controller batch grouping for multi-file queue requests, retry state updates, incomplete-file resume behavior, and focused regression tests.

- [x] Fix weak SongID Discovery Graph neighborhood promotion
  - Status: completed (2026-04-29)
  - Notes: Discovery Graph now requires trusted SongID identity before promoting albums, artists, segments, mixes, or MusicBrainz artist release groups into graph neighborhoods. Weak manual-review runs remain centered on the SongID seed unless they have exact/high-confidence track candidates.

- [x] Prepare `2026042900-slskdn.192` stable release
  - Status: completed (2026-04-29)
  - Notes: Moved the Discovery Graph fix note from Unreleased into a versioned `.192` changelog section and pushed the matching `build-main-2026042900-slskdn.192` tag for the tag-only release workflow.

- [x] Remove the slskdN top status drawer from the Web UI
  - Status: completed (2026-04-29)
  - Notes: Deleted the top drawer/toggle UI and surfaced its DHT, mesh, hash, sequence, swarm, backfill, and karma counters in the persistent footer with focused footer regression coverage.

- [x] Prepare `2026042900-slskdn.196` stable release
  - Status: completed (2026-04-29)
  - Notes: Promoted the current Unreleased notes into a `.196` changelog section and generated release notes for the tag-only release workflow.

- [x] Fix Web UI theme picker, transfer bulk flicker, and footer speeds
  - Status: completed (2026-04-29)
  - Notes: Reworked the theme selector onto a controlled Semantic UI dropdown, made transfer polling monotonic with short-lived optimistic row hiding after accepted bulk actions, and made footer speeds use an elapsed-time fallback when active transfer average speed is still zero.

- [x] Restore short slskdN browser tab title
  - Status: completed (2026-04-29)
  - Notes: Changed the runtime document title back to `slskdN` and added App coverage so version/fork attribution stays out of the browser tab.

- [x] Prepare `2026042900-slskdn.197` stable release
  - Status: completed (2026-04-29)
  - Notes: Promoted the Web UI theme, transfer flicker, footer speed, and browser-title fixes into a `.197` changelog section and generated release notes.

- [x] Multi-source / swarm trust-aware policy and probe budget
  - Status: completed (2026-04-29)
  - Notes: Split the multi-source download path so parallel chunked downloads are reserved for trusted mesh-overlay peers; Soulseek and mixed source sets route through a new sequential-failover path that resumes at the current byte offset on stall, producing at most one mid-stream cancellation per failover instead of one per chunk per peer. Added `VerificationMethod.MeshOverlay`, hard-floored `SelectCanonicalSourcesAsync` (>=2 hash-matched OR all-mesh; otherwise fall back to single-source with a clean 400 from explicit endpoints), per-peer-per-day verification probe budget, `MeshOverlaySourceCount`-driven probe skip, and Prometheus counters for mid-stream cancellations, probe outcomes, hard-floor fallbacks, and failover events. Rewrote `docs/multipart-downloads.md` and the README multi-source section to document scope and mechanics honestly.

- [x] Prepare `2026042900-slskdn.198` stable release
  - Status: completed (2026-04-29)
  - Notes: Promoted the multi-source trust-aware policy bullets and the rolling Chocolatey publish CI fix bullets into a `.198` changelog section.

- [x] Add README showcase gallery with open-license screenshots
  - Status: completed (2026-04-29)
  - Notes: Captured and inspected a varied headless screenshot set, copied final PNGs to `docs/assets/readme-showcase/`, added a clickable thumbnail gallery to `README.md`, and replaced the Discovery Graph image with a multi-node SongID atlas from the fixed local build.

- [x] Redesign the Web UI footer status dock
  - Status: completed (2026-04-29)
  - Notes: Reorganized the footer into brand/support, speed, network/index, transport-health, and fork-note groups while keeping the same telemetry and attribution data. Rechecked against live `local test host` rendering and changed the layout from a rigid grid to a flexible dock with wrapping status pills.

- [x] Fix README showcase dark-mode screenshots
  - Status: completed (2026-04-29)
  - Notes: Pulled the remote README changes, inspected all README showcase PNGs, identified the SongID result, Discovery Graph atlas, and Network dashboard captures as carrying light-theme Semantic UI surfaces, fixed the affected dark-mode selectors, deployed the refreshed web bundle to `local test host` for verification, and recaptured the three affected README images.

- [x] Principal UI pass over README showcase surfaces
  - Status: completed (2026-04-29)
  - Notes: Re-reviewed every README screenshot for layout, spacing, chrome, and contrast issues. Compacted the desktop nav, reduced fixed footer height, made the mobile footer a one-row scroll rail, tightened search result cards and file lists, made Discovery Graph controls deliberate, added sparse graph messaging, defaulted secondary Search page panels closed with persisted state, deployed to `local test host`, and recaptured the README gallery.

- [x] Persist Search page collapsible section state
  - Status: completed (2026-04-29)
  - Notes: SongID, MusicBrainz Lookup, Discovery Graph Atlas, and Album Completion now default collapsed; every Search page collapsible section stores its last open/collapsed state in browser local storage.

- [x] Integrate low-risk upstream-request affordances
  - Status: completed (2026-04-29)
  - Notes: Added conservative queue-position refresh batching, transfer peer Browse links, batch-aware delete-on-remove path resolution, README search-filter syntax documentation, and changelog notes. Larger items such as browser playback and browse UI pagination remain design-sized work rather than safe same-turn changes.

- [x] Prepare `2026042900-slskdn.202` stable release
  - Status: completed (2026-04-30)
  - Notes: Promoted the current Unreleased UI chrome and transfer polish notes into the `.202` changelog section for the tag-only stable release workflow after `.200` failed on stale unit-test compile blockers and `.201` failed on release-gate unit regressions. Fixed the manual-review SongID graph expansion bug and aligned the `UserService` disposal test with its fixture-owned regex matcher listener.

- [x] Smoke test integrated Web UI player streaming
  - Status: completed (2026-04-30)
  - Notes: Used Wikimedia Commons `Sample2.ogg` in an isolated local slskdN instance, verified ranged `/api/v0/streams/{contentId}` playback through Vite dev servers on ports 3001 and 3002, and added the resulting player screenshot to the README showcase.

- [x] Add local mute and mobile/PWA player support
  - Status: completed (2026-04-30)
  - Notes: Added a persisted browser-local mute toggle, inline/preloaded audio attributes, safe-area-aware player/footer spacing, mobile wrapping, and larger touch targets. Verified with focused player tests, lint, build, and a 390px Playwright mobile smoke against the dev UI.

- [x] Resolve streamable local library content ids from allowed roots
  - Status: completed (2026-04-30)
  - Notes: `ContentLocator` now falls back to configured non-excluded share directories plus the downloads directory, matching `sha256:` or stable path IDs only for local audio under allowed roots. This keeps the stream server integrated in slskdN without requiring a separate media server or manual `content_items` seed for local picker results.

- [x] Add browser Media Session metadata for player/PWA
  - Status: completed (2026-04-30)
  - Notes: The Web UI player now publishes title/artist/album metadata when available and wires browser media-session actions for play, pause, previous, next, rewind, and fast-forward.

- [x] Add player transport controls, launchers, and footer-safe drawer
  - Status: completed (2026-04-30)
  - Notes: Added previous/next, rewind, fast-forward, collapse/expand, persistent local mute, and player empty-state launchers for Collections plus shared/downloaded audio. Browser geometry checks verified the expanded and collapsed player sit above the fixed footer without overlap on desktop and a 390px mobile viewport.

- [ ] Improve collection item display metadata
  - Status: follow-up
  - Notes: Collection items currently persist only content id/media kind/hash, so playlist rows and the player can show raw `sha256:` ids for local test items. Add filename/title metadata resolution for better user-facing playlist/player labels.

- [x] Add Winamp-style Web UI player enhancements
  - Status: completed (2026-04-30)
  - Notes: Added a shared Web Audio graph, 10-band persisted EQ with presets, lightweight spectrum/oscilloscope canvas, synced LRCLIB lyrics pane, ListenBrainz now-playing/scrobble submission with a browser-local token, crossfade toggle, Document Picture-in-Picture spectrum window, karaoke-style center-channel reduction, and README/features/walkthrough/changelog documentation. Follow-up design pass rebuilt the player as a modern Winamp-style deck with LCD track display, grouped transport controls, library/file browser modals, segmented analyzer controls, and modal integration settings.

- [x] Replace player dropdown pickers with modal browsers
  - Status: completed (2026-04-30)
  - Notes: The player empty state now opens a two-pane Collections browser and a searchable shared/downloaded local-audio browser instead of compact dropdowns. Both modals use explicit row-level play actions and were validated with focused player tests, lint, build, and mobile modal geometry checks.

- [x] Document integrated Web UI player and listening-party features
  - Status: completed (2026-04-30)
  - Notes: Updated README, `docs/listening-party.md`, `docs/advanced-features.md`, and `docs/FEATURES.md` to cover the integrated player, modal pickers, local-root stream resolution, footer-safe drawer controls, player extras, PWA/mobile behavior, and listening-party/radio boundaries.

- [x] Harden new streaming, player, DHT pod, and mesh-adjacent surfaces
  - Status: completed (2026-04-30)
  - Notes: Replaced browser audio JWT query strings with short-lived stream tickets, required tickets for listed-party radio, changed listening-party DHT records to explicit JSON bytes, failed closed on invalid pod DHT signatures, published only locally stored pod metadata to DHT, bounded stream root lookup to path IDs under allowed roots, reduced local library path exposure, and tightened ListenBrainz token clearing/error reporting.

- [x] Prepare `2026042900-slskdn.204` stable release
  - Status: completed (2026-04-30)
  - Notes: Promoted the current integrated player, visualizer, streaming, pod, security, docs, and external visualizer launcher release notes into the `.204` changelog section for the tag-only stable release workflow. `.203` was a failed tag attempt blocked by optional Winget release-version metadata validation.

- [x] Add chat and room header activity indicators
  - Status: completed (2026-04-30)
  - Notes: Added red-dot header activity indicators for unread private chats and joined room messages newer than the browser's last-seen room activity marker. Fixed chat and room tabs so switching tabs preserves mounted panes instead of rebuilding the session, while active-tab gating prevents hidden room panes from polling and hidden chat panes from acknowledging unread messages.

- [x] Fix dark-mode inner surfaces and surface VPN/Lidarr admin status
  - Status: completed (2026-04-30)
  - Notes: Added central dark-mode overrides for Semantic UI segments, cards, tables, modals, dropdowns, inputs, and messages; replaced remaining light inline panel colors in rooms and port-forwarding surfaces with theme variables; added a System Integrations admin tab with VPN status/config summary and Lidarr status/wanted-sync/manual-import actions.

- [x] Preserve all Semantic UI tab panes across tab switches
  - Status: completed (2026-04-30)
  - Notes: Applied `renderActiveOnly={false}` to every Semantic UI `Tab` under `src/web/src/components`, covering Browse, Contacts, port forwarding, pods, System, Files, Security, Adversarial Settings, and Library Health in addition to the existing Chat and Rooms fix.

- [x] Complete Semantic UI cleanup pass
  - Status: completed (2026-04-30)
  - Notes: Added a shared `TooltipButton` wrapper for accessible button labels and popups, gave the Media Core player its own Semantic button tooltip wrapper, switched the header to active-aware `NavLink` items, added responsive table overflow handling, and made remaining tab panes controlled so active state is stable while panes stay mounted.

- [x] Harden browser-local player preference storage
  - Status: completed (2026-04-30)
  - Notes: Added shared safe local/session storage helpers and moved ListenBrainz token storage, auth token helpers, player toggles, equalizer state, and native MilkDrop preference/library persistence away from direct storage API calls so privacy-locked browsers fall back to defaults instead of throwing.

- [x] Harden remaining browser-local UI storage
  - Status: completed (2026-04-30)
  - Notes: Converted remaining production direct local/session storage access to the shared safe storage helpers across App, Search, Discovery Graph, Browse, Chat, Rooms, Users, System Network, Footer, blocked-user storage, and user-context chat routing. Production direct storage API calls are now isolated to `src/web/src/lib/storage.js`.

- [x] Audit and fix fixed-chrome scroll regions
  - Status: completed (2026-04-30)
  - Notes: Measured nav/player/footer chrome into CSS variables used by the app scroll container; fixed safe-area double counting and nav bottom-edge under-reservation. Headless geometry audit passed Search, Rooms, Chat, Browse, and System at desktop/mobile sizes with the player expanded and collapsed.

- [x] Audit and tune dark theme color contrast
  - Status: completed (2026-04-30)
  - Notes: Ran headless screenshots and computed contrast checks across Search, Rooms, Chat, Browse, Downloads, Uploads, Wishlist, Users, and System. Tightened dark-mode Semantic UI colored button contrast, documented the gotcha, and validated frontend lint, production build, and a focused contrast sweep.

- [x] Audit and backfill Web UI affordances
  - Status: completed (2026-04-30)
  - Notes: Added shared cursor, hover, focus-visible, selectable-row, dropdown, checkbox, link, disabled, and reduced-motion affordance rules. Backfilled labels/titles for icon-only player launcher, chat, room, browse, and user action controls; headless DOM audit passed representative routes with no visible unnamed icon-only controls.

- [x] Redesign player local-audio file picker as an explorer
  - Status: completed (2026-04-30)
  - Notes: Replaced the flat player file picker with a path-aware local audio explorer backed by a paged `/library/items/browser` API. The new picker has folder navigation, breadcrumbs, recursive search, duplicate collapse for search results, paging, file locations, copy counts, and row-level play actions.

- [x] Clean up Rooms dark text and duplicate joined-room navigation
  - Status: completed (2026-04-30)
  - Notes: Removed the redundant joined-room recovery rail from Rooms because joined rooms already hydrate as tabs. Added shared and page-specific dark-mode text overrides for Semantic UI nested list headers/descriptions and user chips, plus distinct room/chat history, input, and user-panel tones.

- [x] Audit and soften overstated documentation claims
  - Status: completed (2026-04-30)
  - Notes: Updated README, feature/status/test/security docs, and the documentation audit so public-facing copy no longer claims blanket production readiness, universal SSRF coverage, live January test status, or hard guarantees where the current codebase only provides scoped guardrails.

- [x] Add slskdN-native config compatibility for upstream-style layout
  - Status: completed (2026-04-30)
  - Notes: Rebuilt the useful config-map behavior within slskdN's existing YAML provider instead of porting upstream code past the license boundary. `transfers.upload.limits` and `transfers.groups` now bind correctly, older shapes remain accepted, docs/examples prefer the new layout, and startup warnings guide migration.
