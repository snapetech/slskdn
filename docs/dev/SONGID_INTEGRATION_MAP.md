# SongID Integration Map

Date: 2026-03-16

## Goal

Add a native `SongID` feature to `slskdn` that:

- lives beside the existing MusicBrainz lookup in the Search page
- accepts arbitrary music sources, not just MBIDs
- identifies likely track / album / artist targets using fused evidence
- fans successful identification out into ranked download options
- uses `slskdn`'s existing MusicBrainz, AcoustID, search, verification, multi-source, and ranking systems instead of Python app code

This is not a wrapper plan for `ytdlpchopid`'s Python app. It is a native-in-framework design that ports the feature set into `slskdn` service, API, job, and React patterns.

## Executive Answer

Yes, most of the `ytdlpchopid` suite can be replicated inside `slskdn`.

The split is:

- Fully replicable now: source intake, clip extraction, Chromaprint/AcoustID, MusicBrainz resolution, transcript-derived MB search, scorecards, source assessment, canonical target creation, album/discography planning, ranked Soulseek acquisition.
- Replicable with native adapters: SongRec, yt-dlp, ffmpeg/ffprobe, OCR, Whisper, Demucs, Panako, Audfprint, comment extraction, Spotify page parsing, YouTube candidate search.
- Not worth doing as literal parity: filesystem report dumps as the primary UX. In `slskdn`, the primary surface should be API-first with job state, persistent results, and direct download actions.

The core insight is:

- `ytdlpchopid` is an identification-and-triage engine.
- `slskdn` is already a canonical metadata, search, verification, and acquisition engine.

`SongID` should be the bridge between those two realities.

## What Already Exists In `slskdn`

These are the leverage points that make this practical:

- `MusicBrainzLookup` UI and `/musicbrainz/targets` API already resolve release and recording targets.
- `IMusicBrainzClient`, `ReleaseGraphService`, and `DiscographyProfileService` already support release lookup, recording lookup, search, release-graph fetch, and discography shaping.
- `IAcoustIdClient`, `IChromaprintService`, and `IFingerprintExtractionService` already provide native fingerprint lookup plumbing.
- `MetadataFacade` already fuses file tags, AcoustID, Chromaprint, and MusicBrainz.
- `HashDb` already stores music-identification-adjacent data and cached MusicBrainz album targets.
- `ContentVerificationService`, `AdvancedDiscoveryService`, `MultiSourceDownloadService`, and `SourceRankingService` already verify, group, rank, and acquire sources.
- `DiscographyJobService` and `JobsController` already give us a job model for album / artist scale execution.

This means the hard backend half after "what song is this?" is mostly present.

## What `ytdlpchopid` Adds

`ytdlpchop` contributes the missing intake and evidence-fusion half:

- arbitrary source intake: YouTube URL, Spotify URL, local file
- acquisition helpers: yt-dlp, preview audio, candidate YouTube search
- clip windowing and multi-profile scans
- multiple recognizers: AcoustID, SongRec, transcript-derived MB search, local corpus, Panako, Audfprint
- contextual extraction: comments, timestamps, OCR, transcript, stem separation
- heuristic evidence: provenance scanning and AI-audio artifact scoring
- final fused outputs: scorecard, assessment, best match candidates

That is exactly the gap between current `slskdn` MusicBrainz lookup and the user's desired `SongID` experience.

## Proposed Product Shape

`SongID` should be a first-class Search-page workflow:

1. User supplies a source.
2. `SongID` analyzes the source asynchronously.
3. `SongID` emits ranked identity candidates:
   - track candidates
   - album candidates
   - artist candidates
4. User chooses scope:
   - download song
   - download album
   - download discography
5. `slskdn` converts the chosen target into canonical MusicBrainz objects.
6. Existing search, verification, ranking, and multi-source download systems produce ranked acquisition options.

Visually, `SongID` sits near the current MusicBrainz lookup, but it is conceptually upstream from it:

- MusicBrainz lookup: "I already know the canonical ID."
- SongID: "Figure out the canonical ID from messy reality."

## UX Placement

Place `SongID` above or beside `MusicBrainzLookup` in [Searches.jsx](<repo-root>/src/web/src/components/Search/Searches.jsx) and keep the current MusicBrainz panel as the manual fallback.

Recommended UI blocks:

- `SongIDPanel`
  - source input
  - source type hint
  - analyze button
  - tooltip: explains that SongID can identify a track, album, or artist from a URL or local media source
- `SongIDRunCard`
  - job state, progress, engines used, warnings
- `SongIDCandidates`
  - tabs or grouped sections: Tracks, Albums, Artists
- `SongIDDownloadActions`
  - actions for song / album / discography
  - each button wrapped in Semantic UI `Popup`
- `SongIDEvidenceDrawer`
  - transcript snippets, OCR hits, comments, recognizer hits, provenance flags, confidence breakdown

The result should feel more operational than `ytdlpchop`'s report files. It should feel like "identify, then act."

## Native Architecture

### 1. New Domain: SongID

Add a dedicated backend area:

- `src/slskd/SongID/`
- `src/slskd/SongID/API/`
- `src/slskd/SongID/Models/`
- `src/slskd/SongID/Jobs/`
- `src/slskd/SongID/Engines/`

Core services:

- `ISongIdService`
- `ISongIdRunStore`
- `ISongIdPlanner`
- `ISongIdScoringService`
- `ISongIdAcquisitionService`

Core model objects:

- `SongIdRequest`
- `SongIdRun`
- `SongIdSourceAssets`
- `SongIdClip`
- `SongIdEvidence`
- `SongIdScorecard`
- `SongIdAssessment`
- `SongIdCandidate`
- `SongIdDownloadPlan`

### 2. Engine Adapter Layer

Do not port Python code line-for-line.

Instead, create native adapters with a stable internal contract:

- `ISourceIngestEngine`
- `IRecognizerEngine`
- `ITranscriptEngine`
- `IStemSeparationEngine`
- `IOcrEngine`
- `IContextMiningEngine`

Implementations can start as process-backed adapters around installed tools:

- `YtDlpSourceIngestEngine`
- `SpotifyBridgeEngine`
- `FfmpegClipEngine`
- `SongRecRecognizerEngine`
- `WhisperTranscriptEngine`
- `TesseractOcrEngine`
- `DemucsStemEngine`
- `PanakoRecognizerEngine`
- `AudfprintRecognizerEngine`

This keeps `slskdn` native while avoiding Python app dependencies. The process boundary is an implementation detail, not the app framework.

### 3. Canonical Resolution Layer

This is where `SongID` hands off to existing `slskdn` capability.

Flow:

- raw recognizer evidence
- normalize to candidate recording / release / artist identities
- resolve through `IAcoustIdClient`, `IMusicBrainzClient`, and `MetadataFacade`
- persist canonical targets in `HashDb`
- expose actionable candidates back to UI

This layer should prefer:

- direct recording ID hits
- repeated multi-window agreement
- cross-engine agreement
- release graph support for album / discography expansion

### 4. Acquisition Planning Layer

This is the differentiator versus `ytdlpchop`.

Every accepted candidate should produce one or more `SongIdDownloadPlan`s:

- track plan
- album plan
- discography plan

Each plan should include:

- canonical identifiers
- predicted item count
- local coverage from `HashDb` / library health
- available Soulseek search coverage
- expected quality profile
- confidence and risk flags

### 5. Ranked Download Options

This is where the user request becomes uniquely `slskdn`.

For a track:

- run canonical search using resolved recording ID and text aliases
- verify candidates with `ContentVerificationService`
- cluster by semantic key
- rank by source quality and `SourceRankingService`
- optionally execute `MultiSourceDownloadService`

For an album:

- use stored / resolved `AlbumTarget`
- compute completion and local coverage
- search per track
- rank per-track candidate sets
- present album-level completion likelihood and quality summary

For a discography:

- use `ReleaseGraphService` + `DiscographyProfileService`
- build a release queue
- score each release by availability, confidence, and expected quality
- dispatch sub-jobs through the existing jobs framework

## Feature Parity Map

### Can Replicate Cleanly

- multi-profile clip scanning
- Chromaprint generation
- AcoustID lookup
- MusicBrainz text search
- transcript-derived phrase search
- Spotify metadata bridge
- YouTube candidate search
- comment mining and timestamp extraction
- OCR on sampled frames
- source-level scorecard
- source assessment labels
- album / artist expansion via MusicBrainz
- actioning directly into ranked downloads

### Can Replicate, But Via Tool Adapters

- SongRec / Shazam recognition
- Whisper transcription
- Demucs stem separation
- Panako
- Audfprint
- yt-dlp acquisition
- ffmpeg and ffprobe media processing
- Tesseract OCR

### Should Be Reinterpreted For `slskdn`

- synthetic / AI scoring should not decide download actions when identity is already strong
- machine-readable report files should become persistent API/UI state, not the primary UX
- Markdown summaries should become expandable evidence panels or export actions, not the main workflow

## New Delta From `../ytdlpchopid`

The renamed app is materially richer than the earlier `ytdlpchop` snapshot. The additional parity targets now include:

- split `identity_assessment` and `synthetic_assessment`
- `forensic_matrix` with lane-level scoring and confidence
- `known_family_score` / `family_label`
- `quality_class`
- `top_evidence_for` / `top_evidence_against`
- `perturbation_stability`
- `songrec_distinct_match_count`
- `raw_acoustid_hit_count`
- `playlist_request_count`
- `ai_comment_mentions`
- chapter-aware metadata capture
- C2PA/content-credentials provenance hints and verification
- a stronger "known local family / reused audio family" concept
- roadmap hooks for deeper Essentia MIR features: melody, cover similarity, embeddings

These should be treated as first-class SongID parity work, not optional stretch goals.

## Decision Rule: Identity Beats Synthetic For Downloading

This should be explicit in the product:

- if SongID has a strong identity match, download planning should be driven by identity confidence, canonical support, availability, and slskdn acquisition quality
- synthetic / AI-origin scoring should remain visible but secondary
- the UI should surface synthetic indicators unobtrusively by default: compact labels, muted badges, or tooltip/mouseover detail
- synthetic scoring becomes operational only when identity is weak, absent, or contradictory

In practice:

- `recognized_cataloged_track` or strong repeated recognizer consensus should dominate download actions
- forensic matrix output should not suppress a good song / album / discography plan
- forensic matrix output should help users interpret weird sources, not derail a clear match

## Inventive Native Integrations

This is where `slskdn` can do better than `ytdlpchopid` instead of merely matching it.

- Mix decomposition:
  - if comments, chapters, OCR, or transcript timestamps imply multiple tracks, SongID should offer `Split Into Track Plans` and generate multiple per-segment download plans instead of one ambiguous result
- Channel/original family memory:
  - if forensic matrix and corpus indicate repeated non-catalog families, SongID should remember that family locally and use it in future reranking without blocking downloads
- Provenance badges:
  - if C2PA/content credentials are detected, show a subtle provenance badge in the SongID evidence area
- Candidate-mode browsing:
  - if identity is weak but candidate evidence is nontrivial, SongID should offer `Search Top Candidates` as a multi-action fan-out instead of pretending there is one true answer
- Album/discography opportunism:
  - if a single track is strongly identified and the artist has strong canonical support, SongID should opportunistically offer album and discography plans ranked by expected completion quality
- Family-aware notes:
  - if a source looks channel-original / likely uncataloged, SongID should offer a note or watchlist-style path rather than only "no match"
- Forensic on hover:
  - lane scores, perturbation stability, generator family hints, and AI heuristics should mostly live behind mouseover/tooltips or collapsed evidence drawers unless the user expands them

## Integration Canvas

### Input & Evidence Ingestion
- `SongIdService.AnalyzeSourceAsync` routes the input to `AnalyzeLocalFileAsync`, `AnalyzeYouTubeAsync`, `AnalyzeSpotifyAsync`, or the free-text fallback so we cover the YouTube/Spotify/local/text surface that `ytdlpchopid` exposes without abandoning the .NET process.
- YouTube chapter metadata, Spotify previews, and Whisper/OCR inputs all run inside `SongIdService` (`AnalyzeYouTubeAsync`, `AnalyzeSpotifyAsync`, `AddPipelineEvidenceAsync`, `AddOcrFindingsAsync`), keeping downloads and artifact storage inside the app.
- Chromaprint + AcoustID fingerprinting, metadata fusion through `MetadataFacade`, and supplementary clues (comments, chapters, timestamps) feed `SongIdScorecard` so the evidence model mirrors the richer CLI output.

### Evidence, Scorecards & Forensic Layers
- Scorecard deltas like `songrec_distinct_match_count`, `raw_acoustid_hit_count`, `playlist_request_count`, and `ai_comment_mentions` live in `SongIdScorecard`, matching the CLI’s numeric signals while staying accessible via API/UI.
- `SongIdScoring` composes the forensic matrix with identity, provenance, spectral artifact, descriptor prior, lyrics/speech, structural, generator-family, and confidence lanes plus the `forensicMatrix` payload (`topEvidenceFor`, `topEvidenceAgainst`, `familyLabel`, `qualityClass`, `knownFamilyScore`, `confidenceScore`, `syntheticScore`).
- `ScanProvenanceSignalsAsync` surfaces C2PA/content credentials through `SongIdProvenanceFinding`, letting the UI badge provenance discoveries without gating actions.
- `AnalyzePerturbationsAsync` runs low-pass, resample, and pitch-shift probes so `perturbationStability` is based on actual resiliency while `SongIdForensicLane.Notes` documents penalties.

### Mix & Segment Planning
- `BuildSegmentQueries`, `BuildSegmentPlans`, `BuildSegmentOptions`, and `BuildMixGroups` translate chapters, comments, and timestamps into explicit segment groups, mix clusters, and per-segment acquisition plans, enabling the “Split Into Track Plans” behavior.
- Segment acquisition options fan out into batch searches through `BuildSegmentOptions`, exposing `Search Top Candidates` actions instead of forcing a single ambiguous result.
- Mix groups power the UI’s mix progress panels while `SongIdMixGroup` retains the grouped context for downstream reranking.

### Acquisition Surface & Ranking
- `BuildPlans` and `BuildAcquisitionOptions` spawn track, album, and discography actions that wrap `MultiSourceDownloadService`, `SourceRankingService`, and backend job handoffs (`JobsController`, `DiscographyJobService`).
- `SongIdAcquisitionOption.OverallScore` relies on `SongIdScoring.ComputeIdentityFirstOverallScore`, ensuring downloads are ordered by identity > quality > Byzantine consensus as requested.
- Album/discography options reuse `ReleaseGraphService`/`DiscographyProfileService`, canonical stats, and optional job dispatch so users can immediately download deeper catalogs.
- Identity-first ranking keeps synthetic/AI signals informational while `AddFallbackOptions` still surfaces uncataloged/channel-original paths without blocking catalog matches.

### Queue, Concurrency & Background Execution
- The durable queue combines an unbounded `Channel<Guid>`, `_queuedRunIds`, and configurable `SongIdOptions.MaxConcurrentRuns` so SongID accepts infinite queue depth while `StartWorkers` honors the `X` concurrent slots.
- `SongIdRunStore` persists `QueuePosition`, `WorkerSlot`, and restart evidence while `RecoverQueuedRunsAsync` re-prioritizes runs so the queue survives crashes.
- CLI/ENV options (`--songid-max-concurrent-runs`, `SONGID_MAX_CONCURRENT_RUNS`) let deployments throttle worker counts without sacrificing backlog capacity.

### UI & Graph Surfaces
- `SongIDPanel` stays beside MusicBrainz lookup, showing run cards, queue/perturbation flows, mix sequencing, and download actions with Semantic UI `Popup` tooltips for lanes so lanes stay explorable but unobtrusive.
- Discovery Graph surfaces (`DiscoveryGraphCanvas`, `DiscoveryGraphModal`, `DiscoveryGraphAtlasPanel`, `/discovery-graph` route) anchor SongID runs, MusicBrainz targets, and search seeds; edges carry provenance/scoring data so the mini-map, modal, and atlas connect the three zoom levels from the product pitch.
- Graph actions (`recenter`, `queue nearby`, `pin`, `compare`, `save branch`) call `DiscoveryGraphService`, reusing the same backend data instead of recomputing neighborhoods when users wander the topology.
- Graph surfaces share state so semantic zoom looks like a single substrate and the new Discovery Graph actions can produce queues or downloads directly from any neighborhood.

## Remaining TODO

### P0: Operational parity gaps

- [x] Persist full SongID evidence payloads with durable forensic-matrix fields instead of only the current subset
- [x] Add split `identity_assessment` and `synthetic_assessment` to the native SongID model and UI
- [x] Add `top_evidence_for`, `top_evidence_against`, `quality_class`, and `perturbation_stability` to the SongID result surface
- [x] Add synthetic `confidence_score`, `known_family_score`, and `family_label` to the native SongID result surface
- [x] Keep synthetic / AI-origin output informational-only when identity is strong; do not let it gate download planning

### P1: Input and evidence parity

- [x] Capture and surface YouTube chapters as tracklist clues
- [x] Add `songrec_distinct_match_count`, `raw_acoustid_hit_count`, `playlist_request_count`, and `ai_comment_mentions` to the native scorecard
- [x] Add C2PA/content-credentials provenance detection and subtle UI badging
- [x] Persist local family labels / reused-audio-family hints from corpus and forensic evidence
- [x] Persist and expose forensic `notes` / penalty reasons so users can see why confidence was capped or reduced
- [x] Expose the deeper forensic lane blocks in expandable evidence UI: `confidence_lane`, `spectral_artifact_lane`, `lyrics_speech_lane`, and `structural_lane`
- [x] Add descriptor-priors and generator-family lanes to the native forensic matrix
- [x] Drive `perturbation_stability` from real perturbation probes instead of only static heuristic inference
- [x] Surface explicit segment decomposition groups instead of only leaking multi-track inference through generic plans/options

### P2: Download and planning integrations

- [x] Add `Split Into Track Plans` for mixes / long uploads with multiple timestamps or chapter hints
- [x] Add candidate-fanout actions when SongID has multiple plausible matches instead of one high-confidence result
- [x] Add family-aware fallback actions for likely uncataloged or channel-original sources
- [x] Add segment-level candidate fan-out actions for decomposed / ambiguous sections
- [x] Feed stronger SongID identity confidence into song / album / discography action ordering
- [x] Replace the current fire-and-forget SongID background launch with a durable queue/worker model that accepts effectively unbounded queued runs and processes only `X` concurrent runs at a time
- [x] Make SongID worker concurrency configurable instead of the current internal fixed value

### P3: Test and UX depth

- [ ] Add API tests for SongID run creation, retrieval, persistence, and progress payload shape
- [x] Add service/store coverage for queue recovery and queue-position ordering
- [ ] Add UI tests for status/progress rendering, canonical scoring, and unobtrusive synthetic evidence display
- [x] Add tests for the "identity beats synthetic for downloading" rule
- [x] Add tests for "one strong synthetic lane is not enough" confidence capping
- [ ] Add tests for "strong identity suppresses synthetic overclaiming"
- [ ] Add export or debug views for the detailed forensic matrix without making it the main UI

### P4: Future MIR parity

- [ ] Add deeper Essentia-backed MIR features where they materially improve identification
- [ ] Explore melody / cover-similarity matching for transformed or reused material
- [ ] Explore embedding-based local family clustering for repeated channel-original or synthetic families

- local filesystem report folders
  - replace with API results plus optional export
- corpus rerank as a stand-alone file output
  - replace with persisted local evidence indexes in `HashDb` or a `SongID` store
- single final "assessment" as the whole product
  - keep the assessment, but make downstream acquisition plans the main output

## Scoring Model

`SongID` should not have a single confidence number only. It should output layered scoring:

- identity confidence
- catalog confidence
- source originality / AI suspicion
- acquisition confidence
- quality confidence

Recommended composite scoring dimensions:

- recognizer agreement
- cross-window agreement
- canonical ID presence
- transcript / OCR / comment support
- provenance flags
- local library corroboration
- Soulseek result density
- verification-group strength
- expected codec / bitrate / format quality
- source reliability history

## Byzantine Scoring In `slskdn`

The user asked for "quality / byzantine scoring / other relevant factors."

In this app, "Byzantine" should mean adversarially robust ranking, not just a fancy score label.

Use a `SongIdByzantineScore` that penalizes disagreement and suspicious consensus:

- reward independent agreement across engines
- reward agreement across non-overlapping clip windows
- reward agreement between audio and non-audio evidence
- penalize a hit that appears only in one weak engine
- penalize evidence that is too metadata-only with no audio corroboration
- penalize candidates whose search results split into many incompatible semantic groups
- penalize sources whose file characteristics drift wildly from expected duration / size / codec bands
- penalize candidates with strong provenance / AI-artifact flags when the goal is catalog music acquisition

Then feed that into acquisition ranking:

- `final_download_score = identity_score + byzantine_score + source_quality_score + peer_rank_score + completeness_score`

This is where `slskdn` can surpass `ytdlpchop`.

## End-To-End Flow

### Track Flow

1. User submits source.
2. `SongID` ingests source and extracts clips.
3. Recognizers run across clip windows.
4. `SongID` resolves canonical recording candidates.
5. Top track candidate shown with evidence and confidence.
6. User clicks `Download Song`.
7. `slskdn` searches Soulseek, verifies variants, ranks peers, and starts multi-source download.

### Album Flow

1. Track candidate maps to release candidates.
2. Best album candidate is persisted as `AlbumTarget`.
3. UI shows album completion potential and missing tracks.
4. User clicks `Download Album`.
5. Existing album-target / remediation style machinery is reused for track-level planning and acquisition.

### Discography Flow

1. Artist candidate maps to MusicBrainz artist ID.
2. `ReleaseGraphService` fetches release graph.
3. User chooses discography profile.
4. User clicks `Download Discography`.
5. `DiscographyJobService` seeds release jobs.
6. Each release spins out into album plans and ranked track acquisition.

## Data Persistence

Add a persistent `SongID` store, likely SQLite-backed like the rest of the app.

Persist:

- runs
- source metadata
- extracted evidence
- candidates
- scorecards
- assessments
- chosen actions
- acquisition outcomes

Do not treat `SongID` as ephemeral request-only state. The user will want to revisit uncertain runs and promote them to album or discography actions later.

## API Surface

Suggested endpoints:

- `POST /api/v0/songid/runs`
- `GET /api/v0/songid/runs`
- `GET /api/v0/songid/runs/{id}`
- `POST /api/v0/songid/runs/{id}/cancel`
- `POST /api/v0/songid/runs/{id}/download/song`
- `POST /api/v0/songid/runs/{id}/download/album`
- `POST /api/v0/songid/runs/{id}/download/discography`
- `GET /api/v0/songid/runs/{id}/plans`
- `GET /api/v0/songid/runs/{id}/evidence`

`SongID` should also emit progress through SignalR, same style as search and jobs.

## Phased Implementation

### Phase 1: Native SongID Core

- create `SongID` domain, API, store, and job model
- support local file input first
- use `ffmpeg`, `Chromaprint`, `AcoustID`, `MusicBrainz`, `MetadataFacade`
- emit track candidates and scorecards
- connect `Download Song` into multi-source acquisition

### Phase 2: URL Intake

- add YouTube intake with yt-dlp
- add Spotify bridge with preview / candidate YouTube fallback
- add comment and timestamp mining
- add OCR sampling

### Phase 3: Deep Evidence

- add SongRec adapter
- add Whisper transcript adapter
- add transcript-derived MB search
- add provenance scanning and AI-audio heuristics

### Phase 4: Album / Artist Expansion

- add album candidate ranking
- add artist candidate ranking
- add download-album and download-discography flows
- reuse release graph and discography profile systems

### Phase 5: Adversarial Ranking

- add byzantine scoring
- add local corpus / historical evidence reranking
- add source disagreement diagnostics
- add richer quality prediction and completeness forecasting

## Main Risks

### Tooling Risk

External engines will vary by host availability. The design must degrade gracefully by engine, not fail the entire run.

### Network Health Risk

Discography-scale actions can become abusive if they trigger uncontrolled search storms. This must remain conservative and job-queued.

### Confidence Risk

`SongID` can overfit to weak metadata if audio engines are missing. The scoring model must expose uncertainty honestly.

### UX Risk

If the UI shows raw engine output instead of ranked, canonical choices, the feature will feel like a debug console. The primary output must stay action-oriented.

## Recommended First Cut

If implementation starts immediately, the highest-value slice is:

- local file + YouTube source intake
- clip extraction
- Chromaprint + AcoustID + MusicBrainz + MetadataFacade
- `SongID` run persistence
- Search-page UI panel
- track candidate list
- `Download Song` action using existing ranking and multi-source services

That gets the concept live quickly and proves the bridge from identification to acquisition.

## Bottom Line

This is feasible.

The app already has the back half of the machine. `SongID` is the missing front half.

If implemented this way, `SongID` will not just copy `ytdlpchop`. It will absorb its evidence-fusion strengths, then do something the other app cannot do on its own:

identify a messy source, resolve it to canonical music entities, and immediately turn that into ranked, verified, network-aware song, album, and discography downloads inside `slskdn`.
