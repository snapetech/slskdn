# slskdN Feature Expansion PRD and Story Map

Status: Draft
Date: 2026-04-30
Owner: slskdN

## Product Goal

Make slskdN the batteries-included music acquisition, verification, discovery, library-health, and community-mesh client for users who want Soulseek compatibility plus modern self-hosted music workflows.

The product should keep the existing network-health posture: Soulseek-facing work is conservative, user-controllable, observable, and rate-limited. Automation must default to preview, staging, or explicit approval unless a user intentionally enables unattended operation.

## Non-Goals

- Do not auto-hammer public Soulseek peers for discovery or hash probing.
- Do not make streaming-service source support required for core Soulseek use.
- Do not require Plex, Jellyfin, Navidrome, Lidarr, Beets, or external metadata accounts for the base client.
- Do not publish private library holdings, paths, usernames, exact filenames, or raw listening history to mesh peers.
- Do not silently import, retag, delete, or reorganize user files without a preview or explicit policy.

## Product Principles

- One app should handle the common path from "I want this music" to verified, tagged, organized, playable, and shareable files.
- Power users should get precise controls, but default flows should be understandable in minutes.
- Every automated feature needs clear scope, cooldowns, retry limits, and network-impact visibility.
- Mesh features should improve quality, resilience, trust, and discovery without weakening Soulseek compatibility.
- External services should be optional providers behind a provider boundary.

## Feature Pillars

### 1. Unified Source Providers

Add a source-provider layer that can search, match, download, and verify music from multiple configured sources.

Providers:
- Soulseek via the existing client path.
- Mesh peers via verified content hashes and trusted overlay transport.
- Local library and transfer folders.
- HTTP/WebDAV/S3-style user-configured repositories.
- Plugin-backed streaming or store providers.
- Plugin-backed video/audio extraction providers.
- Future private BitTorrent or magnet providers when explicitly configured.

Requirements:
- Each provider declares capabilities: search, album lookup, playlist import, download, stream, checksum, preview, metadata, auth mode, rate limits, and legal/user warnings.
- Users can enable, disable, and reorder providers.
- Per-profile provider priority is supported.
- Fallback chains are explicit and observable.
- Provider failures are recorded without poisoning other sources.
- Sources that require credentials use encrypted local storage.
- High-risk providers must be disabled by default.

### 2. Download Profiles

Add reusable profiles that drive search, matching, provider ordering, and download behavior.

Initial profiles:
- Lossless Exact: prefer FLAC/WAV/AIFF, strict title/album/artist/duration matching.
- Fast Good Enough: accept high-bitrate lossy and shorter queues.
- Album Complete: prefer full folders with expected track count and album-level consistency.
- Rare Hunt: tolerate slower queues and single-source candidates, but keep manual approval.
- Conservative Network: lower search concurrency, no public-peer probing, no automatic retries.
- Mesh Preferred: use trusted mesh candidates first, then public Soulseek fallback.
- Metadata Strict: require MBID/ISRC/AcoustID confidence before import.

### 3. Advanced Search and Candidate Ranking

Improve search from result lists into decision-ready candidates.

Features:
- Aggregate mode for distinct tracks and albums.
- Album-candidate grouping by folder, track count, release identity, quality, and peer set.
- Ranking explanations.
- Queue-depth, peer speed, prior success, reputation, and hash confidence in the score.
- Strict and preferred file conditions.
- Search-result deduplication across providers.
- Search previews that can print/export the planned action before downloading.

### 4. Interactive Album Picker

Add an album review surface before large downloads.

Capabilities:
- Show candidate folders as comparable rows/cards.
- Display expected vs found track list.
- Display format mix, bit depth, sample rate, bitrate, size, duration variance, queue state, and source count.
- Show confidence warnings: missing tracks, extra tracks, remix/live/deluxe mismatch, fake-lossless suspicion, inconsistent metadata.
- Allow manual track substitution from alternate candidates.
- Let users save the decision as a rule for similar future requests.

### 5. Discovery Inbox

Create an approval-first intake queue for recommendations, missing releases, release radar, playlist imports, and mesh-discovered candidates.

States:
- Suggested
- Approved
- Downloading
- Staged
- Imported
- Rejected
- Snoozed
- Failed

Requirements:
- Suggestions explain why they were surfaced.
- Bulk approve/reject is supported.
- Network-impact estimates are shown before approval.
- Automatic download is opt-in per source/profile.

### 6. Watchlists and Release Radar

Users can subscribe to artists, labels, scenes, playlists, and collection targets.

Features:
- Release type filters: album, EP, single, compilation, live, remix, acoustic, deluxe.
- Country/format filters.
- New-release detection through configured metadata providers.
- Mesh evidence detection for when trusted peers first see a target.
- Similar-artist expansion with approval.
- Per-watchlist schedule, cooldown, and provider policy.
- Missing-release promotion into the Discovery Inbox or Wishlist.

### 7. Playlist Intake and Mirroring

Support playlists as first-class acquisition inputs.

Sources:
- Local M3U/CSV/text files.
- Spotify-style URLs through provider plugins.
- YouTube-style playlist URLs through provider plugins.
- Tidal/Deezer/Qobuz-style playlist URLs through provider plugins.
- ListenBrainz/community recommendation playlists.
- Media-server playlists.

Features:
- Refresh mirrored playlists on schedule.
- Detect source changes.
- Persist unmatched entries.
- Unmatch/rematch individual tracks.
- Allow partial playlist completion.
- Build slskdN playlists from imported results.

### 8. Metadata and Matching Engine

Add a shared matching layer used by search, import, tagging, discovery, and provider fallback.

Capabilities:
- Weighted title/artist/album/duration matching.
- Unicode, punctuation, accent, casing, and symbol normalization.
- Short-title protection.
- Version awareness: original, remix, live, acoustic, instrumental, remaster, deluxe, clean/explicit.
- Multi-disc and compilation handling.
- ISRC, MBID, AcoustID, and provider-ID evidence.
- Confidence bands: auto, review, reject.
- Match explanations.

### 9. Audio Verification

Add optional verification that operates consistently across providers.

Features:
- AcoustID/Chromaprint lookup.
- Local fingerprint cache.
- HashDb verification for bit-identical mesh candidates.
- Fake-lossless detection.
- Duration and codec sanity checks.
- Confidence-gated import decisions.
- Fail-open and fail-closed modes per profile.

### 10. Import Staging

Downloads land in a staging workflow before entering the library.

Features:
- Staging folder scan.
- Group files into probable albums.
- Per-track and per-album confidence.
- Tag preview and diff.
- Cover-art preview.
- "Import as-is", "retag then import", "send to review", "reject", and "retry source" actions.
- Failed-import denylist to prevent repeat loops.
- Orphan and duplicate detection.

### 11. Tagging and Organization

Add controlled tag-writing and file organization.

Features:
- MusicBrainz-style album preflight to keep album tracks on one release.
- Configurable organization templates.
- Cover art embedding and cover file writing.
- ReplayGain analysis.
- Synchronized lyric retrieval through providers.
- Multi-artist tag policy.
- Per-tag source priority.
- Tag write preview.
- Bulk retag and undo metadata snapshot where feasible.

### 12. Library Health and Maintenance

Expand library health into a repair center.

Checks:
- Duplicates.
- Dead files.
- Missing cover art.
- Metadata gaps.
- Album completeness.
- Fake lossless.
- Transcodes.
- MBID mismatch.
- Track-number errors.
- Multi-disc inconsistency.
- Orphan files.
- ReplayGain missing.
- Unshared downloads.

Actions:
- Fix selected.
- Fix all safe.
- Export report.
- Queue replacement search.
- Quarantine suspicious files.
- Rescan after fix.

### 13. Rate-and-Keep Discovery Shelf

Create a temporary discovery library for recommended tracks.

Behavior:
- Discovery downloads can land in a probationary shelf.
- High ratings promote tracks to the permanent library.
- Low ratings delete or archive tracks.
- Unrated tracks expire on a configurable schedule.
- Shared folders require consensus or owner policy before deletion.
- Promote/delete operations are previewed unless explicitly automated.

### 14. Listening Stats and Scrobbling

Add a listening intelligence layer.

Features:
- Local play history.
- Last.fm and ListenBrainz scrobbling.
- Media-server play import.
- Top artists/albums/tracks.
- Forgotten favorites.
- Recently added.
- Time-range filters.
- Genre and label breakdowns.
- Stats-driven recommendations.

### 15. Built-In Player Enhancements

Use slskdN's player as the review and discovery surface.

Features:
- Queue management.
- Smart radio from selected track/artist/genre.
- Similar-track auto-queue.
- Media Session API support.
- Keyboard shortcuts.
- Rating controls connected to the discovery shelf.
- Tag/source/confidence badges in now playing.

### 16. Automation Recipes and Builder

Add bounded automation for repeat workflows.

Initial recipes:
- Nightly library health scan.
- Weekly release radar.
- Refresh mirrored playlists.
- Import completed downloads.
- Retry failed Wishlist items.
- Generate discovery recommendations.
- Sync media-server playlists.

Safety:
- Automations have cooldowns.
- Chain depth is capped.
- Cycle detection is required.
- Network-impact class is displayed.
- Dry run is supported.
- Manual approval gates are available per action.

### 17. Wishlist and Requests

Unify "wanted" music into one queue.

Features:
- Manual track/album/artist requests.
- Household/user request portal.
- Approval queue.
- Request status.
- Per-user quotas.
- Retry policy.
- "Found locally", "found on mesh", "found on Soulseek", "not found", and "needs review" statuses.

### 18. Lidarr and Servarr Integration

Make slskdN a direct integration target.

Features:
- Indexer-compatible API.
- Download-client-compatible API.
- Import status callbacks.
- Remote path diagnostics.
- Wanted-list pull.
- Cutoff-unmet pull.
- Manual search result handoff.
- Integration setup wizard.

### 19. Media Server Integration

Support common self-hosted media servers without requiring them.

Features:
- Library scan trigger after import.
- Playlist sync.
- Play history import.
- Rating import/export.
- Real path/report path diagnostics.
- Multi-user mapping.
- Server health card.

### 20. Diagnostics and Live Operations

Add operational visibility for normal users.

Features:
- Live log viewer with component filters.
- Copyable diagnostic bundle.
- Setup wizard.
- Port and connectivity checks.
- Soulseek login validation.
- Share-folder validation.
- API key validation.
- Download-path and permission checks.
- Provider credential checks.
- Queue and automation health.
- Network-health score.

### 21. Mesh Metadata Federation

Use mesh to exchange signed quality and metadata evidence.

Artifacts:
- Verified file hash evidence.
- Release completeness evidence.
- AcoustID/MBID match attestations.
- Fake-lossless warnings.
- Peer quality signals.
- Realm-curated subject indexes.
- Optional decentralized metadata corrections.

Safety:
- No raw paths.
- No exact library dumps.
- Trust-tier filtering.
- k-anonymity thresholds where applicable.
- User-visible provenance.

### 22. Community Quality Signals

Surface quality evidence without turning it into uncontrolled global reputation.

Signals:
- Served verified content.
- Failed verification.
- Queue reliability.
- Speed history.
- Completed album consistency.
- Suspicious metadata or audio claims.

Usage:
- Ranking input.
- Warning badges.
- Mesh trust weighting.
- Local-only override and notes.

### 23. Mobile and Responsive Workflows

Improve mobile use for searching, approvals, playback, and diagnostics.

Features:
- Compact candidate picker.
- Bottom-sheet actions.
- Mobile staging review.
- Mobile watchlist management.
- Mobile player queue and ratings.
- Mobile diagnostic wizard.

## Story Map

### Epic E1: Source Provider Framework

- S1. As a user, I can view enabled and available source providers so I understand where slskdN can search and download from.
- S2. As a user, I can enable, disable, and reorder providers per profile so I control fallback behavior.
- S3. As an operator, I can configure provider credentials securely so secrets are not stored in plain text.
- S4. As a developer, I can add a provider by implementing a capability-based interface so new sources do not fork search logic.
- S5. As a user, I can see which provider served each result so I can trust or reject the source.

Acceptance:
- Provider capability metadata appears in the UI and API.
- Disabled providers are never queried.
- Provider order is honored in dry run and real download paths.
- Credentialed providers can be removed and scrubbed.

### Epic E2: Download Profiles

- S1. As a user, I can select a download profile before searching or approving a request.
- S2. As a user, I can create custom profiles from built-in templates.
- S3. As a user, I can see a profile's quality, provider, and network-impact settings before using it.
- S4. As a user, I can set defaults for manual search, Wishlist, watchlists, and playlist imports.

Acceptance:
- Profiles affect ranking and provider selection.
- Profile decisions are visible in result explanations.
- Conservative profiles reduce public network activity.

### Epic E3: Aggregate Search and Candidate Ranking

- S1. As a user, I can run aggregate track search to group equivalent results and pick the best candidate.
- S2. As a user, I can run aggregate album search to group equivalent album folders.
- S3. As a user, I can inspect why a candidate ranked first.
- S4. As a user, I can export or preview planned downloads without starting them.

Acceptance:
- Duplicate candidates collapse into groups.
- Ranking includes quality, confidence, queue, peer, provider, and verification evidence.
- Dry run performs no downloads or peer probes beyond the declared search.

### Epic E4: Interactive Album Picker

- S1. As a user, I can compare album candidates before downloading.
- S2. As a user, I can replace an individual track candidate.
- S3. As a user, I can reject candidates with missing or suspicious tracks.
- S4. As a user, I can save a decision as a rule for future similar requests.

Acceptance:
- Album picker shows expected/found tracks, formats, confidence, and warnings.
- No album download starts until approved unless an automation policy allows it.

### Epic E5: Discovery Inbox

- S1. As a user, I can review recommendations in one inbox.
- S2. As a user, I can approve, reject, snooze, or bulk process suggestions.
- S3. As a user, I can see why each item was suggested.
- S4. As a user, I can move approved suggestions to Wishlist or immediate download.

Acceptance:
- Inbox preserves item state across restarts.
- Rejected items do not immediately reappear from the same evidence.
- Network-impact estimate appears before bulk approval.

### Epic E6: Watchlists and Release Radar

- S1. As a user, I can watch an artist for new releases.
- S2. As a user, I can filter watched releases by type, country, and format.
- S3. As a user, I can scan watchlists manually.
- S4. As a user, I can enable scheduled scans with cooldowns.
- S5. As a user, I can approve similar-artist expansion.

Acceptance:
- Manual scan works without enabling automation.
- Scheduled scans obey cooldown and profile rules.
- Found items enter Discovery Inbox or Wishlist based on policy.

### Epic E7: Playlist Intake and Mirroring

- S1. As a user, I can import a playlist from a file or provider URL.
- S2. As a user, I can mirror a playlist and refresh it later.
- S3. As a user, I can review unmatched tracks.
- S4. As a user, I can unmatch and rematch playlist entries.

Acceptance:
- Playlist items retain source identity.
- Refresh detects added, removed, and changed items.
- Partial completion is allowed.

### Epic E8: Metadata Matching

- S1. As a user, I can see match confidence for downloads and imports.
- S2. As a user, I can manually override low-confidence matches.
- S3. As a user, I can distinguish originals, remixes, live versions, remasters, and deluxe tracks.
- S4. As a developer, I can reuse the matcher across search, import, and tagging.

Acceptance:
- Match explanations identify strongest and weakest evidence.
- Short-title false positives are rejected or sent to review.

### Epic E9: Audio Verification

- S1. As a user, I can enable fingerprint verification per profile.
- S2. As a user, I can view verification results before import.
- S3. As a user, I can choose fail-open or fail-closed behavior.
- S4. As a user, I can cache verification results locally.

Acceptance:
- Verification never blocks manual access to downloaded files.
- Failed verification can trigger retry, quarantine, or review based on policy.

### Epic E10: Import Staging

- S1. As a user, I can review completed downloads before library import.
- S2. As a user, I can preview tag and path changes.
- S3. As a user, I can denylist failed imports.
- S4. As a user, I can scan an existing folder into staging.

Acceptance:
- Import actions are reversible at least at the metadata-decision level.
- Files are not deleted during staging unless explicitly requested.

### Epic E11: Tagging and Organization

- S1. As a user, I can retag files with a preview.
- S2. As a user, I can apply organization templates.
- S3. As a user, I can embed cover art and write sidecar cover files.
- S4. As a user, I can run ReplayGain after import.
- S5. As a user, I can configure multi-artist tag behavior.

Acceptance:
- Tag writes report changed fields.
- Organization dry run shows source and destination paths.

### Epic E12: Library Health

- S1. As a user, I can scan for library issues.
- S2. As a user, I can view issues by severity and type.
- S3. As a user, I can fix selected safe issues.
- S4. As a user, I can queue replacement searches for bad files.
- S5. As a user, I can export a health report.

Acceptance:
- Scans do not modify files.
- Fix actions are separately confirmed.

### Epic E13: Discovery Shelf

- S1. As a user, I can send recommendations into a temporary shelf.
- S2. As a user, I can rate tracks from the player.
- S3. As a user, I can promote highly rated tracks.
- S4. As a user, I can expire unrated tracks.
- S5. As a shared-library owner, I can require consensus before deletion.

Acceptance:
- Promotion and deletion policies are visible.
- Expiry never deletes files without the configured policy.

### Epic E14: Listening Intelligence

- S1. As a user, I can record local play history.
- S2. As a user, I can scrobble plays to configured services.
- S3. As a user, I can import play history from a media server.
- S4. As a user, I can use listening stats to seed recommendations.

Acceptance:
- Scrobbling can be disabled globally and per user.
- Imported history is deduplicated.

### Epic E15: Player Enhancements

- S1. As a user, I can manage a playback queue.
- S2. As a user, I can start smart radio from a track, artist, or genre.
- S3. As a user, I can rate tracks from now playing.
- S4. As a user, I can see source, match, and verification badges while listening.

Acceptance:
- Player controls remain usable on mobile.
- Ratings feed the discovery shelf.

### Epic E16: Automation

- S1. As a user, I can enable a prebuilt automation recipe.
- S2. As a user, I can run an automation in dry-run mode.
- S3. As a user, I can create a bounded custom automation.
- S4. As an operator, I can see automation failures and retry history.

Acceptance:
- Automations have cooldown, max-run, and cycle protections.
- Network-impact class is shown before enabling.

### Epic E17: Wishlist and Requests

- S1. As a user, I can add a track, album, artist, or playlist to Wishlist.
- S2. As a household user, I can request music for approval.
- S3. As an admin, I can approve or reject requests.
- S4. As a user, I can see request status from wanted to imported.
- S5. As a user, I can retry failed items with a different profile.

Acceptance:
- Request state persists.
- Per-user limits are enforceable.

### Epic E18: Servarr Integration

- S1. As a user, I can configure slskdN as an indexer.
- S2. As a user, I can configure slskdN as a download client.
- S3. As a user, I can run a setup wizard that checks API keys and paths.
- S4. As a user, I can pull wanted and cutoff-unmet items into Wishlist.

Acceptance:
- Remote path issues are diagnosed with concrete path examples.
- Integration can be disabled without deleting local Wishlist items.

### Epic E19: Media Server Integration

- S1. As a user, I can connect a media server.
- S2. As a user, I can trigger a library scan after import.
- S3. As a user, I can sync playlists.
- S4. As a user, I can import ratings and play history.
- S5. As a user, I can map media-server users to slskdN users.

Acceptance:
- Media-server integration is optional.
- Failed scans do not mark imports failed.

### Epic E20: Diagnostics

- S1. As a user, I can view live logs by component.
- S2. As a user, I can copy a diagnostic bundle.
- S3. As a user, I can run a setup health check.
- S4. As a user, I can see a network-health score.
- S5. As a user, I can validate shares, ports, API keys, providers, and permissions.

Acceptance:
- Diagnostic bundles redact secrets.
- Checks include next-step guidance.

### Epic E21: Mesh Metadata Federation

- S1. As a user, I can opt into receiving signed metadata evidence from trusted mesh peers.
- S2. As a user, I can inspect provenance for mesh evidence.
- S3. As a user, I can use mesh evidence in candidate ranking.
- S4. As a realm operator, I can curate signed subject indexes.
- S5. As a user, I can disable all outbound evidence publication.

Acceptance:
- No paths, exact holdings, or raw listening history are published.
- Trust thresholds control what evidence is applied.

### Epic E22: Community Quality Signals

- S1. As a user, I can see local peer quality history.
- S2. As a user, I can use trusted quality signals in ranking.
- S3. As a user, I can override or ignore quality signals.
- S4. As a user, I can report suspicious candidates locally.

Acceptance:
- Signals are explainable.
- Public Soulseek peers are not globally punished from untrusted reports.

### Epic E23: Mobile Workflows

- S1. As a mobile user, I can approve Discovery Inbox items.
- S2. As a mobile user, I can compare album candidates.
- S3. As a mobile user, I can review staged imports.
- S4. As a mobile user, I can rate and promote discovery tracks.
- S5. As a mobile user, I can run diagnostics.

Acceptance:
- Touch targets are usable on narrow screens.
- Candidate and staging tables have mobile-specific layouts.

## Suggested Delivery Order

### Milestone 1: Better Decisions Before Download

- Download profiles.
- Aggregate search.
- Candidate ranking explanations.
- Interactive album picker.
- Diagnostic setup wizard.

### Milestone 2: Safe Acquisition Pipeline

- Discovery Inbox.
- Wishlist/request unification.
- Import staging.
- Metadata matcher.
- Audio verification.

### Milestone 3: Library Outcomes

- Tagging and organization.
- Library health repair center.
- Discovery shelf.
- Player ratings.
- Listening stats.

### Milestone 4: Ecosystem Integration

- Servarr integration.
- Media-server integration.
- Playlist mirroring.
- Watchlists and release radar.
- Provider plugin boundary.

### Milestone 5: Bounded Automation

- Prebuilt recipes.
- Dry-run automation.
- Watchlist schedules.
- Playlist refresh schedules.
- Failed-item retry policies.

### Milestone 6: Mesh Advantage

- Mesh metadata federation.
- Community quality signals.
- Realm indexes.
- Trust-scored ranking.
- Privacy-preserving recommendation evidence.

## Open Decisions

- Which provider classes ship in core versus plugin packages?
- Which metadata provider is the default for first-run users?
- Whether tag writing is native, sidecar-backed, or adapter-backed.
- Whether media-server integrations share one abstraction or ship as separate adapters.
- Whether request portal users authenticate locally, through media-server users, or both.
- What evidence types can be published to mesh by default, if any.
- Which automations are safe enough to ship enabled-by-default.
