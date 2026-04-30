# Music Discovery Federation Plan

> Date: 2026-04-30
> Status: Planned
> Scope: Discography Concierge, Federated Taste Recommendations, Bloom-Filter Library Diff, Per-Artist Release Radar, Realm-Curated Subject Indexes, Decentralized MusicBrainz Edit Overlay, and Quarantine Jury.

## Intent

Build the next discovery layer by composing the primitives already in the tree: MusicBrainz release graphs, Wishlist, SongID, DiscoveryGraph, SocialFederation, PodCore bloom filters, Mesh realms, ShadowIndex, ContentSafety, and ByzantineConsensus.

This plan intentionally excludes friend-tier backup, file duplication, cooperative mirroring, and storage-replica management. Availability work should stay focused on making wanted music discoverable and well-sourced through normal Soulseek and mesh paths, not turning slskdN into a backup application.

## Guardrails

- Network activity must be opt-in, scheduled, or rate-limited. No aggressive automatic peer browsing or bulk probing.
- Mesh artifacts must carry only safe metadata by default: MBIDs, WorkRefs, confidence summaries, signed verdicts, and opaque peer identity references. No local paths, IPs, private hashes where a public identifier is enough, or library dumps.
- User-visible actions should promote to Wishlist, graph exploration, release radar subscriptions, or manual jury requests. Downloads still follow the existing transfer trust policy.
- Social recommendations and bloom diffs must apply k-anonymity or per-contact disclosure thresholds before surfacing private taste signals.
- Realm indexes and MB edit overlays are local overlays first. Optional upstream MusicBrainz submission stays manual.

## Phase 1: Discography Concierge

Goal: provide a per-artist coverage map and turn near-complete artists into conservative completion missions.

Existing anchors:
- `Integrations/MusicBrainz/ReleaseGraphService`
- `Integrations/MusicBrainz/DiscographyProfileService`
- `Wishlist/WishlistService`
- `SongID/SongIdService`
- `DiscoveryGraph/DiscoveryGraphService`
- `Transfers/MultiSource/MediaCoreSwarmService`

Plan:
1. Add a `DiscographyCoverageService` that resolves an artist MBID into release groups/tracks through the existing MusicBrainz release graph cache.
2. Compare release/track MBIDs against local catalog evidence, HashDb inventory, SongID run confirmations, and mesh search availability.
3. Expose a matrix API with row = release MBID, column = recording/track, and cell status = local, mesh-available, wishlist-seeded, absent, ambiguous, or ignored-by-profile.
4. Add UI entry points from MusicBrainz artist lookup and Discovery Graph artist nodes.
5. Add a manual `Promote missing tracks to Wishlist` action plus an optional threshold rule that only suggests promotion after configured coverage, initially 70%.
6. Use SongID confirmation to upgrade ambiguous candidates before marking a release complete.

Do not:
- Auto-download an artist discography without an explicit user action.
- Browse every remote peer for every missing track. Use known mesh/search evidence and existing wishlist cadence.

Acceptance:
- Given an artist MBID, the API returns deterministic release/track coverage with evidence per cell.
- Wishlist promotion deduplicates against existing wishlist rows.
- Discovery Graph can prioritize artists by neighborhood density without making network calls during graph rendering.

## Phase 2: Bloom-Filter Library Diff

Goal: let friends discover likely gaps without either side publishing an exact library list.

Existing anchors:
- `PodCore/BloomFilter.cs`
- `Identity/`
- `Wishlist/`
- `Mesh/Privacy/MessagePadder`
- `SocialFederation/LibraryActorService`

Plan:
1. Add a stable, versioned `LibraryBloomSnapshot` payload over MBID namespaces: artist MBID, release-group MBID, release MBID, and recording MBID.
2. Publish snapshots only to trusted contacts or selected pods/realms; default off until the user opts in.
3. Compare inbound snapshots locally against local MBIDs to produce "likely missing" suggestions, not assertions.
4. Add one-click promotion from diff suggestions to Wishlist entries.
5. Rotate snapshot salt on a schedule so long-lived filters cannot be correlated indefinitely.
6. Pad and batch snapshot messages through the existing privacy layer where enabled.

Do not:
- Publish filenames, paths, file hashes, exact counts by artist, or full inventory exports.
- Treat Bloom hits as proof. False positives must be presented as likely matches only.

Acceptance:
- A user can generate and inspect their outbound snapshot metadata before enabling publication.
- Inbound diffs can create wishlist suggestions without exposing the peer's exact holdings.
- Tests cover false-positive handling and snapshot version compatibility.

## Phase 3: Federated Taste Recommendations

Goal: turn inbound trusted listening/library signals into local, privacy-aware recommendations.

Existing anchors:
- `SocialFederation/MusicLibraryActor.cs`
- `SocialFederation/LibraryActorService.cs`
- `NowPlaying/NowPlayingService`
- `Identity/`
- `DiscoveryGraph/`
- `Mesh/Privacy/MessagePadder`

Plan:
1. Add a local `TasteRecommendationService` that consumes inbound LibraryActor/NowPlaying-style WorkRefs already accepted by federation policy.
2. Weight candidates by trust tier, recency, graph proximity, and overlap with the user's Discovery Graph neighborhoods.
3. Enforce a reveal threshold before display: default to at least k trusted contacts or realm members touching a track/release before surfacing it.
4. Show recommendations as graph-aware WorkRef cards with reasons such as "near artists you complete" or "appeared in 3 trusted libraries."
5. Allow promotion to Wishlist, release radar subscription, or Discovery Graph exploration.

Do not:
- Send listening history to a central recommender.
- Show single-friend listening events as recommendations unless the user explicitly lowers the threshold for that contact.

Acceptance:
- Recommendations can be computed fully locally from inbound federated artifacts.
- k-anonymity is enforced in the service layer, not only the UI.
- The UI explains recommendation evidence without naming peers unless policy allows it.

## Phase 4: Per-Artist Release Radar

Goal: subscribe to an artist MBID and notify when trusted mesh/federation evidence first observes a SongID-confirmed recording for that artist.

Existing anchors:
- `SocialFederation/WorkRef.cs`
- `SocialFederation/LibraryActorService.cs`
- `SongID/SongIdService`
- `Mesh/Realm/`
- `DiscoveryGraph/`

Plan:
1. Add `ArtistRadarSubscription` records keyed by artist MBID and optional realm/contact scope.
2. Have SongID-confirmed recordings emit safe WorkRef observation events containing artist MBID, recording MBID, release MBID when known, confidence, and first-seen timestamp.
3. Route signed observation events through LibraryActor outboxes or realm-scoped mesh messages.
4. Deduplicate by artist/recording/source realm and suppress repeated notifications after the first confirmed network sighting.
5. Provide UI actions: subscribe, mute release group, add to Wishlist, open graph neighborhood.

Do not:
- Poll MusicBrainz as a release-calendar replacement.
- Notify on unconfirmed filename-only matches.

Acceptance:
- Subscriptions survive restart.
- A SongID-confirmed inbound observation can trigger a single notification and optional Wishlist seed.
- Realm/contact scope changes affect future notifications without rewriting historical evidence.

## Phase 5: Realm-Curated Subject Indexes

Goal: let niche scenes maintain signed subject indexes where external catalogs are incomplete.

Existing anchors:
- `Mesh/Realm/`
- `Mesh/Governance/`
- `PodCore/`
- `VirtualSoulfind/ShadowIndex/`
- `MediaCore/ContentId.cs`
- `SocialFederation/WorkRef.cs`

Plan:
1. Define a `RealmSubjectIndex` artifact containing realm id, subject namespace, WorkRefs, external ids, aliases, evidence links, and governance signature metadata.
2. Store and publish indexes through existing realm governance/gossip channels, with DHT pointers to current signed revisions.
3. Let VirtualSoulfind and ShadowIndex query indexes as an additional authority after local catalog and MusicBrainz overlays.
4. Add proposal/review/accept/reject flows using existing realm governance rather than a new moderation subsystem.
5. Add conflict display when realm indexes disagree with MusicBrainz or another subscribed realm.

Do not:
- Treat realm indexes as global truth.
- Merge realm artifacts into the main MusicBrainz cache without provenance.

Acceptance:
- A realm can publish a signed index revision and a subscriber can resolve it by realm id.
- ShadowIndex resolution can cite realm index provenance.
- Conflicting aliases or MBID mappings remain visible and reversible.

## Phase 6: Decentralized MusicBrainz Edit Overlay

Goal: capture signed local corrections to MusicBrainz results and apply them as provenance-preserving overlays.

Existing anchors:
- `Integrations/MusicBrainz/MusicBrainzClient.cs`
- `Integrations/MusicBrainz/ReleaseGraphService`
- `MediaCore/`
- `Mesh/ServiceFabric/`
- `SocialFederation/WorkRef.cs`

Plan:
1. Define `MusicBrainzOverlayEdit` artifacts for correction types: alias, title correction, artist correction, release grouping, recording linkage, missing alt-title, and duplicate marker.
2. Require source evidence: SongID run id, WorkRef, realm subject index citation, or user-authored note.
3. Sign and gossip overlay edits through mesh service fabric or realm channels according to the user's trust scope.
4. Apply overlays at read time, returning both original MusicBrainz values and effective local values.
5. Add a manual export/review path for users who want to submit corrections upstream to MusicBrainz.

Do not:
- Mutate cached upstream MusicBrainz payloads in place.
- Auto-submit edits upstream.

Acceptance:
- MusicBrainz API responses can include overlay provenance and effective values.
- Users can enable/disable overlay sources by contact, pod, or realm.
- Tests prove overlay application is deterministic and reversible.

## Phase 7: Quarantine Jury

Goal: let users request a trusted second opinion for local ContentSafety/SongID quarantine decisions without weakening local safety.

Existing anchors:
- `Common/Security/ContentSafety.cs`
- `DhtRendezvous/Security/ContentSafety.cs`
- `SongID/SongIdService`
- `Common/Security/ByzantineConsensus.cs`
- `Identity/`
- `Mesh/ServiceFabric/`

Plan:
1. Add a `QuarantineJuryRequest` that sends minimal evidence to selected trusted contacts, pod members, or realm jurors.
2. Evidence should prefer content identifiers, spectral lane summaries, perceptual hashes, and short signed SongID verdict summaries over raw files.
3. Jurors re-run local SongID/spectral checks only if they already have the content or can fetch it through normal user-approved paths.
4. Aggregate signed verdicts with ByzantineConsensus-style thresholds into `uphold`, `release candidate`, or `needs manual review`.
5. Keep the local quarantine state authoritative unless the user accepts the jury recommendation.

Do not:
- Auto-send quarantined files to jurors.
- Auto-release content because peers disagree.
- Let anonymous or untrusted peers participate in jury decisions.

Acceptance:
- Jury requests are user-initiated and scoped to selected trusted identities.
- The aggregate result shows individual signed verdict counts and dissenting evidence categories.
- Local quarantine enforcement remains active until explicit user action.

## Phase 8: Source Feed Imports

Goal: let users import music wants from external playlist/feed sources into slskdN review surfaces without starting immediate network searches or downloads.

Existing anchors:
- `Wishlist/WishlistService`
- `Wishlist/WishlistCsvImport`
- `DiscoveryInbox`
- `SongID/SongIdService`
- `API/Native/SourceProvidersController`
- `src/web/src/lib/acquisitionRequests.js`

Plan:
1. Add a `SourceFeedImportService` that normalizes imported rows into track/release wants with source provenance, confidence, and duplicate keys.
2. Support the lowest-risk inputs first: pasted text tracklists, TuneMyMusic/Spotify-style CSV exports, and manually supplied playlist/feed URLs recorded as provenance.
3. Route imported wants to Discovery Inbox review by default, with an explicit option to create disabled Wishlist searches.
4. Add provider-specific fetchers only behind explicit configuration and rate limits: Spotify playlists/albums/tracks, YouTube playlists, Bandcamp collections, Apple Music exports, ListenBrainz/Last.fm feeds, RSS/OPML, and local M3U/PLS files.
5. Use SongID and MusicBrainz as optional enrichment steps after import review, not as hidden background work.
6. Surface source/provider, row count, duplicate count, skipped rows, and network impact before committing imported wants.

Do not:
- Start Soulseek searches, peer browses, downloads, or remote provider fetches just because a feed URL was pasted.
- Require Spotify or another commercial provider account for CSV/text import.
- Store provider tokens outside existing safe credential patterns.
- Treat playlist order or provider metadata as authoritative identity without MusicBrainz/SongID confirmation.

Acceptance:
- A user can paste or upload a playlist export and get deduplicated Discovery Inbox suggestions with source provenance.
- URL-based providers remain disabled until configured and explicitly fetched.
- Imports are idempotent across repeated exports from the same source.
- Tests cover Spotify/TuneMyMusic CSV columns, headerless text rows, duplicate suppression, skipped-row reporting, and no network activity during dry-run review.

## Implementation Order

1. Discography Concierge: highest visible payoff and mostly local composition.
2. Bloom-Filter Library Diff: small primitive with strong privacy boundaries, useful input for Concierge and recommendations.
3. Per-Artist Release Radar: builds on WorkRef/SongID and feeds Wishlist.
4. Federated Taste Recommendations: depends on inbound WorkRef hygiene and reveal thresholds.
5. Realm-Curated Subject Indexes: unlocks scene-specific catalog authority.
6. MusicBrainz Edit Overlay: uses realm/index/evidence provenance once those artifacts exist.
7. Quarantine Jury: highest safety sensitivity; implement after signed evidence and trust-scoped routing are well tested.
8. Source Feed Imports: user-facing acquisition intake that should feed Discovery Inbox/Wishlist before provider execution.

## Task Breakdown

| ID | Feature | First slice |
| --- | --- | --- |
| T-930 | Discography Concierge | Coverage API over MusicBrainz release graph plus local/mesh/wishlist evidence cells |
| T-931 | Bloom-Filter Library Diff | Versioned MBID bloom snapshot and local comparison service |
| T-932 | Per-Artist Release Radar | Artist MBID subscription model and SongID-confirmed WorkRef observation events |
| T-933 | Federated Taste Recommendations | Local recommender over inbound WorkRefs with service-layer k-anonymity |
| T-934 | Realm-Curated Subject Indexes | Signed realm index artifact and ShadowIndex resolution hook |
| T-935 | MusicBrainz Edit Overlay | Signed overlay edit model and read-time MusicBrainz response overlay |
| T-936 | Quarantine Jury | User-initiated trusted jury request and signed verdict aggregation |
| T-939 | Source Feed Imports | Review-first playlist/feed import service and Discovery Inbox intake |

## Implementation Progress

- 2026-04-30: T-930 first slice implemented. It includes the backend coverage service/API, cached release-target resolution, HashDb/Wishlist evidence cells, manual missing-track Wishlist promotion, and the Search-page Discography Concierge panel. Discovery Graph density prioritization is split to T-937.
- 2026-04-30: T-932 first slice implemented. It adds a local artist-radar service/API with artist MBID subscriptions, muted release-group suppression, SongID-confirmed WorkRef observation validation, deterministic notification dedupe, DI registration, and focused service/controller tests. The first slice is network-presence radar only; it does not poll MusicBrainz, browse peers, search Soulseek, or start downloads.
- 2026-04-30: T-933 first slice implemented. It includes a local taste recommendation service/API over accepted inbound music WorkRefs, followed-actor trust filtering, MusicBrainz-or-normalized-work grouping, service-layer k-anonymity with a default threshold of two trusted sources, and opt-in source actor reveal for future policy-gated UI surfaces.
- 2026-04-30: T-934 first slice implemented. It defines signed realm subject-index artifacts with WorkRefs, external ids, aliases, evidence links, and signature metadata; validates local realm scope, trusted governance roots, payload hashes, safe WorkRefs, and safe evidence links; and adds a recording-MBID resolver that returns realm/index/revision provenance for ShadowIndex and VirtualSoulfind integration.
- 2026-04-30: T-935 first slice implemented. It defines signed MusicBrainz overlay-edit artifacts with SongID, WorkRef, realm-index, or user-note evidence; validates signatures and supported edit/target/field combinations; stores edits deterministically; and applies them at release-graph read time through a dedicated overlay API returning original/effective graphs plus provenance without mutating cached MusicBrainz data.
- 2026-04-30: T-936 first slice implemented. It adds a local Quarantine Jury service/API for user-initiated trusted jury requests, safe opaque evidence validation, signed juror verdict intake, duplicate juror replacement, and two-thirds recommendation aggregation. It does not send files, route mesh messages, release quarantined content, or involve unselected peers.
- 2026-04-30: T-936 persistence follow-up implemented. Quarantine Jury requests and signed juror verdicts now persist to an atomic JSON state file under the app directory and reload on service startup, preserving aggregate recommendations across daemon restarts.
- 2026-04-30: T-939 implemented after source-feed audit. It adds backend source-feed preview for CSV, pasted text, M3U/PLS, RSS/OPML, and Spotify provider URLs. Spotify imports cover public playlist/album/track/artist/user playlist URLs through configured app credentials or a connected account, plus liked/saved tracks, saved albums, followed artists, and current-user playlists through either a connected Spotify account or a per-import bearer token with the required user scopes. Wishlist now has an Import Feed flow that previews suggestions, connects/disconnects Spotify, and adds suggestions to Discovery Inbox review without starting Soulseek searches, peer browses, or downloads.

## Validation Strategy

- Unit tests for every artifact parser, signer, trust filter, and dedupe rule.
- Service tests proving privacy thresholds are enforced below the UI.
- Integration tests for mesh/realm publication only after local service behavior is stable.
- UI tests for every button tooltip and for no accidental peer-name disclosure in recommendation/quarantine surfaces.
- Network-health tests or fakes proving scheduled work respects existing rate limits and cancellation.
