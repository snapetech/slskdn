# SongID And Discovery

SongID, Acquisition Review, and Discovery Graph form the current review-first
music discovery workflow.

## SongID

SongID identifies messy music sources and turns them into ranked acquisition
paths.

Supported source shapes include:

- YouTube URLs.
- Spotify URLs.
- Direct text queries.
- Server-side local files.

The evidence model includes MusicBrainz, AcoustID, SongRec, transcripts, OCR,
comments, chapters, provenance, perturbation probes, Panako, Audfprint, Demucs
stems, C2PA hints, and forensic lanes.

SongID runs in a durable queue with persisted queue position and worker slot.
The configured concurrency is controlled by `songid.max_concurrent_runs` /
`--songid-max-concurrent-runs` / `SONGID_MAX_CONCURRENT_RUNS`.

## Result Review

SongID surfaces:

- Identity and synthetic assessments.
- Top evidence for and against a result.
- Quality class and perturbation stability.
- Known-family and generator-family context.
- Forensic matrix export/debug views.
- Split track plans for mixes and long uploads.
- Candidate fan-out actions for ambiguous or multi-segment sources.

Synthetic or AI-origin signals are contextual. They should not override strong
catalog identity when ordering download actions.

## Acquisition Review

Acquisition Review is for passive, imported, and generated candidates. It is not
part of the normal manual Search path.

Use it for:

- Source-feed suggestions.
- Watchlist/release radar seeds.
- Listening-history handoffs.
- Library/discovery recommendations.
- Generated SongID or graph candidates that need human review.

Manual Search stays direct: typing a query and pressing Search should open
results without asking the user to approve the same query first.

## Discovery Graph

Discovery Graph / Constellation provides a typed graph of nearby tracks, albums,
artists, SongID runs, MusicBrainz targets, and search-result seeds.

Current surfaces include:

- SongID mini-map and modal.
- MusicBrainz graph launchers.
- Search list/detail graph launchers.
- Search-result graph glyphs.
- In-page atlas panel.
- Dedicated `/discovery-graph` route.

Current actions include recenter, queue nearby, pin, compare, save branch, copy
branch reports, and export graph/evidence context.

## Remaining Research Scope

The remaining SongID/Discovery work is research/depth work, not missing baseline
UI:

- Better mix detection for long gaps, overlaps, chapters, and comments.
- Deeper in-panel SongID queue/perturbation/fan-out UX.
- More Discovery Graph provenance/evidence drill-down and semantic zoom.
- Essentia-backed MIR where it materially improves identification.
- Melody or cover-similarity matching.
- Embedding-based local family clustering.

These items should stay review-first and network-conscious. They should not
start peer searches, browse peers, queue downloads, or mutate files unless the
user explicitly triggers an action with visible impact.
