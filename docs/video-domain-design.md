# Video Domain Design (Movies & TV)

**Status**: DRAFT - Phase E (After MCP, Multi-Domain Core, Proxy/Relay)  
**Created**: December 11, 2025  
**Priority**: MEDIUM (after Book domain or in parallel)

> **Project Note**: This is a fork of [slskd](https://github.com/slskd/slskd). See [../README.md](../README.md#acknowledgments) for attribution.

---

This document defines the Video domain model (Movies & TV) and its integration with VirtualSoulfind, metadata services, and backends.

## Goals

- Treat movies and TV series/episodes as first-class citizens in VirtualSoulfind.
- Provide robust identity, matching, verification, and quality scoring similar in power to the Music domain.
- Enforce domain-appropriate backend rules (no Soulseek).
- Maintain strong security, privacy, and moderation guarantees.

---

## Data Model

### Movie

**`MovieWork`** (implements `IContentWork`):

- `Id : ContentWorkId`
- `Domain : ContentDomain` = `Movie`
- `Title : string`
- `Year : int?`
- `ExternalIds : Dictionary<string, string>` (e.g., TMDB, IMDB-like IDs)
- `PrimaryLanguage : string?`
- `Genres : string[]?` (optional)
- `RuntimeMinutes : int?` (canonical runtime)

**`MovieItem`** (implements `IContentItem`):

- `Id : ContentItemId`
- `Domain : ContentDomain` = `Movie`
- `WorkId : ContentWorkId`
- `EditionName : string?` (e.g., "Director's Cut", "Theatrical")
- `Resolution : string?` (e.g., "4K", "1080p", "720p")
- `VideoCodec : string?` (e.g., "HEVC", "H.264", "AV1")
- `AudioCodec : string?` (e.g., "AAC", "AC3", "DTS")
- `AudioChannels : string?` (e.g., "5.1", "7.1", "Stereo")
- `Hdr : bool?` (or enum: SDR/HDR10/DolbyVision/etc.)
- `Source : string?` (e.g., "BluRay", "WebDL", if available)
- `ExternalIds : Dictionary<string, string>?` (edition-specific IDs if available)

### TV

**`TvShowWork`** (implements `IContentWork`):

- `Id : ContentWorkId`
- `Domain : ContentDomain` = `Tv`
- `Title : string`
- `ExternalIds : Dictionary<string, string>` (series IDs from TMDB/TVDB/etc.)
- `PrimaryLanguage : string?`
- `Seasons : SeasonInfo[]?` (optional structured list, or retrieved on demand)

**`SeasonWork`** (optional, can be a sub-work or inline):

- `Id : ContentWorkId`
- `Domain : ContentDomain` = `Tv`
- `ShowId : ContentWorkId`
- `SeasonNumber : int`
- `ExternalIds : Dictionary<string, string>?` (season-level IDs)

**`EpisodeItem`** (implements `IContentItem`):

- `Id : ContentItemId`
- `Domain : ContentDomain` = `Tv`
- `WorkId : ContentWorkId?` (episode-level work ID, or linked via season+show)
- `ShowId : ContentWorkId`
- `SeasonNumber : int`
- `EpisodeNumber : int`
- `AbsoluteEpisodeNumber : int?` (if used)
- `Title : string`
- `RuntimeMinutes : int?`
- `Resolution : string?`
- `VideoCodec : string?`
- `AudioCodec : string?`
- `AudioChannels : string?`
- `Hdr : bool?`
- `ExternalIds : Dictionary<string, string>?` (episode IDs from TMDB/TVDB/etc.)

---

## Metadata Extraction

A shared **`IVideoMetadataExtractor`** is responsible for extracting:

- Container info (format).
- Video streams:
  - Codec, resolution, approximate bitrate.
- Audio streams:
  - Codec, channels.
- Duration/runtime.
- Subtitle tracks (languages, optional).

### Implementation Notes

- Extraction should use existing tooling (e.g., **ffprobe**) via a safe, bounded integration:
  - Timeouts.
  - Output size limits.
  - No direct exposure of file paths or raw ffprobe output to logs.
- Extract only what is needed for:
  - Identity matching (runtime).
  - Quality scoring (resolution, codecs).
  - Verification (runtime tolerance).

---

## Metadata Services (APIs)

**`MovieMetadataService`** and **`TvMetadataService`** run on service fabric and use the catalogue fetch mechanism:

- Use SSRF-safe HTTP client.
- Use domain allowlists (TMDB/TVDB-like APIs only, ToS-compliant).
- Respect work budgets and rate limits.

### Responsibilities

- Given an external ID or title/year:
  - Produce a `MovieWork` and one or more `MovieItem` definitions.
- Given a series ID and season/episode numbers:
  - Produce `TvShowWork` and `EpisodeItem` definitions.

### API Methods (Examples)

**MovieMetadataService**:
- `Task<MovieWork?> LookupMovieByExternalIdAsync(string externalId, CancellationToken ct)`
- `Task<IEnumerable<MovieWork>> LookupMovieByTitleAndYearAsync(string title, int? year, CancellationToken ct)`

**TvMetadataService**:
- `Task<TvShowWork?> LookupShowByExternalIdAsync(string externalId, CancellationToken ct)`
- `Task<EpisodeItem?> LookupEpisodeAsync(string showId, int season, int episode, CancellationToken ct)`

---

## Domain Providers

### `IMovieContentDomainProvider`

Maps:

- External movie IDs → `MovieWork` + `MovieItem`s.
- Title + year → work/item candidates.
- Local file metadata (filename patterns, NFO data, extracted runtime) → probable `MovieItem`.

Implements:

- Identity mapping.
- Verification (runtime tolerance, hash checks).
- Quality scoring (`VideoCopyQuality`).

### `ITvContentDomainProvider`

Maps:

- Series ID + season/episode → `TvShowWork` + `EpisodeItem`.
- Local file naming conventions (SxxEyy patterns) → specific `EpisodeItem`.

Implements similar identity, verification, and quality scoring.

---

## Verification

When verifying a local file against a `MovieItem` or `EpisodeItem`:

**MUST check**:

- Runtime within configurable tolerance (e.g., ±5 minutes for movies, ±2 minutes for episodes).
- Basic structural sanity (valid video container, at least one video track).

**SHOULD check**:

- Hash match against known-good records (where available).

**MUST NOT**:

- Assume trust in external metadata; must treat all external data as hints.

### Verification Results

Feed into:

- VirtualSoulfind's "verified copy" registry.
- Planner's decisions about:
  - Using a file as a canonical copy.
  - Exposing it via mesh/content relay (subject to MCP).

---

## Quality Scoring

**`VideoCopyQuality`** fields:

- `ResolutionScore : int` (e.g., 4K=100, 1080p=75, 720p=50, SD=25)
- `CodecScore : int` (modern/efficient codecs > older ones; HEVC/AV1 > H.264)
- `HdrScore : int` (HDR=100, SDR=50, Unknown=25; if user prefers HDR)
- `AudioScore : int` (multichannel > stereo where appropriate; 7.1 > 5.1 > Stereo)
- `SourceScore : int` (disc-based > web-dl > unknown; config-driven)
- `OverallScore : int` (normalized aggregate, e.g., weighted average of above)

### Planner Usage

Planner uses `VideoCopyQuality` to:

- Determine whether a local copy meets the user's profile.
- Identify possible upgrades across sources (mesh/torrent/HTTP/local).
- Suggest "upgrade to better quality" when a higher-scoring copy is available.

---

## Backend Rules

For Video domain (`ContentDomain.Movie` and `ContentDomain.Tv`):

**Allowed backends**:

- Mesh overlay.
- Torrent.
- HTTP.
- Local disk.

**Disallowed backends**:

- **Soulseek** (never used for video).

VirtualSoulfind planner MUST enforce these rules when constructing plans for Video domain intents.

---

## Security, Privacy, Moderation

### Privacy

Video metadata and WorkRefs MUST NOT include:

- Local paths.
- Hashes (file hashes stay internal).
- Peer IDs/IPs.

### MCP Integration

MCP MUST gate:

- Advertising of video content on DHT/mesh.
- Serving via content relay.
- Social federation references to video WorkRefs (e.g., watchlists).

### Work Budgets & SSRF Protection

All remote API calls and mesh interactions MUST respect:

- Work budgets (catalogue fetch consumes budget).
- SSRF-safe HTTP restrictions (domain allowlists, HTTPS-only).
- Logging/metrics hygiene (no PII, low-cardinality labels).

---

## Implementation Tasks

See `TASK_STATUS_DASHBOARD.md`:

- **T-VID01**: Video Domain Types & Provider Interfaces
- **T-VID02**: Video Metadata Extraction & Scanner Integration
- **T-VID03**: Video Metadata Services via Catalogue Fetch
- **T-VID04**: Verification & VideoCopyQuality
- **T-VID05**: Planner & Library Reconciliation for Video

---

## Dependencies

- **T-VC01** ✅: ContentDomain abstraction (Video domains defined)
- **T-MCP01-02** ✅: MCP core + scanner integration (moderation applies to video)
- **T-PR02**: Catalogue Fetch service (for TMDB/TVDB API calls)
- **H-TRANSPORT01**: Transport hardening (mesh/torrent/HTTP backends)

---

## References

- [The Movie Database (TMDB) API](https://www.themoviedb.org/documentation/api)
- [TheTVDB API](https://thetvdb.com/api-information)
- [FFmpeg/FFprobe Documentation](https://ffmpeg.org/ffprobe.html)
- VirtualSoulfind V2 Design: `docs/virtualsoulfind-v2-design.md`
- Multi-Domain Overview: `docs/virtualsoulfind-v2-design.md#multi-domain-model-overview`
