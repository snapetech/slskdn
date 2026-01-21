# Video Domain Tasks (Movies & TV) - T-VID01 through T-VID05

**Status**: 📋 Planned  
**Priority**: P2 (After Books, or parallel)  
**Branch**: `experimental/whatAmIThinking`  
**Depends on**: T-VC01-04 (Multi-domain foundation), T-PR02 (Catalogue fetch), T-MCP02 (Scanner integration)

> **Project Note**: This is a fork of [slskd](https://github.com/slskd/slskd). See [README.md](../README.md#acknowledgments) for attribution.

---

## Overview

This document defines tasks for adding **Movies** and **TV Shows** as first-class content domains to VirtualSoulfind v2.

Video content requires:
- **Domain-specific metadata**: Runtime, resolution, codecs, audio layout, subtitles
- **External ID mapping**: TMDB, TVDB, IMDB integration via catalogue-fetch
- **Quality scoring**: Resolution, codec, HDR, audio channels
- **Episode tracking**: TV shows need season/episode hierarchy
- **Backend restrictions**: Video MUST NOT use Soulseek backend (mesh/torrent/HTTP/local only)

---

## 🔒 Security & Compliance

**MANDATORY for all T-VID tasks:**
- Follow `docs/CURSOR-META-INSTRUCTIONS.md`
- Follow `docs/security-hardening-guidelines.md`
- Follow `MCP-HARDENING.md` (video files subject to moderation)
- Work budgets for all external API calls (TMDB/TVDB)
- SSRF protection via catalogue-fetch service
- No Soulseek backend usage for video domains

---

## T-VID01 – Video Domain Types & Provider Interfaces

**Priority**: P2  
**Status**: 📋 Planned  
**Depends on**: T-VC01 (ContentDomain foundation)  
**Blocks**: T-VID02

### Repo: `https://github.com/snapetech/slskdn`
### Branch: `experimental/whatAmIThinking`

**Goal**: Define video domain types and provider interfaces.

---

### 0. Scope & Non-Goals

You **must**:
1. Extend `ContentDomain` enum with `Movie` and `Tv`.
2. Create video-specific work/item types implementing `IContentWork`/`IContentItem`:
   - `MovieWork` and `MovieItem`
   - `TvShowWork`, `SeasonWork`, `EpisodeItem`
3. Define provider interfaces:
   - `IMovieContentDomainProvider`
   - `ITvContentDomainProvider`
4. Register stub implementations (return null for all lookups).

You **must NOT**:
- Implement actual metadata fetching (that's T-VID03)
- Scan for video files yet (that's T-VID02)
- Integrate with planner yet (that's T-VID05)

---

### 1. Recon

Find:
1. Where `ContentDomain` enum is defined (T-VC01 created this).
2. Where `IContentWork` and `IContentItem` interfaces are defined.
3. How music domain provider is registered (T-VC02 example).

---

### 2. Extend ContentDomain Enum

In `ContentDomain.cs`:

```csharp
public enum ContentDomain
{
    Music = 0,
    GenericFile = 1,
    Book = 4,
    Movie = 2,  // NEW
    Tv = 3,     // NEW
}
```

---

### 3. Define MovieWork and MovieItem

Create `src/slskd/VirtualSoulfind/Core/Video/MovieWork.cs`:

```csharp
public sealed class MovieWork : IContentWork
{
    public ContentWorkId Id { get; init; }
    public ContentDomain Domain => ContentDomain.Movie;
    public string Title { get; init; }
    public string? Creator { get; init; }  // Director(s)
    public int? Year { get; init; }
    
    // Movie-specific:
    public string? TmdbId { get; init; }
    public string? ImdbId { get; init; }
    public TimeSpan? Runtime { get; init; }
    public string[] Genres { get; init; } = Array.Empty<string>();
}

public sealed class MovieItem : IContentItem
{
    public ContentItemId Id { get; init; }
    public ContentDomain Domain => ContentDomain.Movie;
    public ContentWorkId? WorkId { get; init; }
    public string Title { get; init; }
    public int? Position { get; init; }  // N/A for movies
    public TimeSpan? Duration { get; init; }  // Runtime
    
    // Movie-specific:
    public string? Edition { get; init; }  // "Director's Cut", "Extended", etc.
    public string? Resolution { get; init; }  // "1080p", "2160p", etc.
    public string? VideoCodec { get; init; }  // "H.264", "HEVC", "AV1"
    public string? AudioCodec { get; init; }
    public string? AudioLayout { get; init; }  // "Stereo", "5.1", "Atmos"
    public bool HasHdr { get; init; }
    public bool HasDolbyVision { get; init; }
}
```

---

### 4. Define TV Show Types

Create `src/slskd/VirtualSoulfind/Core/Video/TvShowWork.cs`:

```csharp
public sealed class TvShowWork : IContentWork
{
    public ContentWorkId Id { get; init; }
    public ContentDomain Domain => ContentDomain.Tv;
    public string Title { get; init; }
    public string? Creator { get; init; }  // Show creator(s)
    public int? Year { get; init; }  // First air year
    
    // TV-specific:
    public string? TvdbId { get; init; }
    public string? ImdbId { get; init; }
    public int TotalSeasons { get; init; }
    public string[] Genres { get; init; } = Array.Empty<string>();
}

public sealed class SeasonWork : IContentWork
{
    public ContentWorkId Id { get; init; }
    public ContentDomain Domain => ContentDomain.Tv;
    public string Title { get; init; }  // "Show Name - Season 1"
    public string? Creator { get; init; }
    public int? Year { get; init; }  // Season air year
    
    // Season-specific:
    public ContentWorkId ShowId { get; init; }
    public int SeasonNumber { get; init; }
    public int EpisodeCount { get; init; }
}

public sealed class EpisodeItem : IContentItem
{
    public ContentItemId Id { get; init; }
    public ContentDomain Domain => ContentDomain.Tv;
    public ContentWorkId? WorkId { get; init; }  // Points to SeasonWork
    public string Title { get; init; }
    public int? Position { get; init; }  // Episode number within season
    public TimeSpan? Duration { get; init; }
    
    // Episode-specific:
    public ContentWorkId ShowId { get; init; }
    public int SeasonNumber { get; init; }
    public int EpisodeNumber { get; init; }
    public string? Resolution { get; init; }
    public string? VideoCodec { get; init; }
    public string? AudioCodec { get; init; }
    public string? AudioLayout { get; init; }
}
```

---

### 5. Define Provider Interfaces

Create `src/slskd/VirtualSoulfind/Core/Video/IMovieContentDomainProvider.cs`:

```csharp
public interface IMovieContentDomainProvider
{
    /// <summary>
    ///     Get movie work by external ID (TMDB/IMDB).
    /// </summary>
    Task<MovieWork?> TryGetWorkByExternalIdAsync(
        string externalId,  // "tmdb:12345" or "imdb:tt0111161"
        CancellationToken ct);
    
    /// <summary>
    ///     Get movie work by title and year.
    /// </summary>
    Task<MovieWork?> TryGetWorkByTitleYearAsync(
        string title,
        int? year,
        CancellationToken ct);
    
    /// <summary>
    ///     Match a local file to a movie item.
    /// </summary>
    Task<MovieItem?> TryGetItemByLocalMatchAsync(
        VideoFileMetadata localFile,
        CancellationToken ct);
}
```

Create `src/slskd/VirtualSoulfind/Core/Video/ITvContentDomainProvider.cs`:

```csharp
public interface ITvContentDomainProvider
{
    /// <summary>
    ///     Get TV show by external ID.
    /// </summary>
    Task<TvShowWork?> TryGetShowByExternalIdAsync(
        string externalId,
        CancellationToken ct);
    
    /// <summary>
    ///     Get season by show ID and season number.
    /// </summary>
    Task<SeasonWork?> TryGetSeasonAsync(
        ContentWorkId showId,
        int seasonNumber,
        CancellationToken ct);
    
    /// <summary>
    ///     Match a local file to an episode item.
    /// </summary>
    Task<EpisodeItem?> TryGetEpisodeByLocalMatchAsync(
        VideoFileMetadata localFile,
        CancellationToken ct);
}
```

---

### 6. VideoFileMetadata DTO

Create `src/slskd/VirtualSoulfind/Core/Video/VideoFileMetadata.cs`:

```csharp
public sealed class VideoFileMetadata
{
    public string Filename { get; init; }
    public long SizeBytes { get; init; }
    public TimeSpan? Duration { get; init; }
    public string? Resolution { get; init; }  // "1920x1080"
    public string? VideoCodec { get; init; }
    public string? AudioCodec { get; init; }
    public string? AudioLayout { get; init; }
    public string[] SubtitleLanguages { get; init; } = Array.Empty<string>();
    
    // Parsed from filename (optional):
    public string? ParsedTitle { get; init; }
    public int? ParsedYear { get; init; }
    public int? ParsedSeason { get; init; }
    public int? ParsedEpisode { get; init; }
}
```

---

### 7. Stub Implementations

Create stub providers that return null:

```csharp
public class NoopMovieContentDomainProvider : IMovieContentDomainProvider
{
    public Task<MovieWork?> TryGetWorkByExternalIdAsync(string externalId, CancellationToken ct)
        => Task.FromResult<MovieWork?>(null);
    
    public Task<MovieWork?> TryGetWorkByTitleYearAsync(string title, int? year, CancellationToken ct)
        => Task.FromResult<MovieWork?>(null);
    
    public Task<MovieItem?> TryGetItemByLocalMatchAsync(VideoFileMetadata localFile, CancellationToken ct)
        => Task.FromResult<MovieItem?>(null);
}

// Similar for NoopTvContentDomainProvider
```

Register in DI (Program.cs):
```csharp
services.AddSingleton<IMovieContentDomainProvider, NoopMovieContentDomainProvider>();
services.AddSingleton<ITvContentDomainProvider, NoopTvContentDomainProvider>();
```

---

### 8. Tests (T-VID01)

Add tests to verify:
1. `ContentDomain.Movie` and `ContentDomain.Tv` enum values exist.
2. `MovieWork`, `MovieItem`, `TvShowWork`, `SeasonWork`, `EpisodeItem` implement interfaces correctly.
3. Stub providers compile and return null.

---

### 9. Anti-Slop Checklist

- [ ] Movie and Tv domains added without changing existing domains
- [ ] Types compile and pass tests
- [ ] Stub providers registered in DI
- [ ] No external API calls yet (T-VID03)
- [ ] No scanner integration yet (T-VID02)

---

## T-VID02 – Video Metadata Extraction & Scanner Integration

**Priority**: P2  
**Status**: 📋 Planned  
**Depends on**: T-VID01, T-MCP02 (Scanner has MCP hook)  
**Blocks**: T-VID03

**Goal**: Teach scanner to recognize video files and extract metadata.

---

### 0. Scope

You **must**:
1. Recognize video file extensions (`.mp4`, `.mkv`, `.avi`, `.mov`, `.m4v`, etc.).
2. Extract metadata using `ffprobe` or similar:
   - Duration, resolution, video codec, audio codec, audio layout
   - Subtitle tracks (languages)
3. Parse filename for title/year/season/episode using patterns.
4. Build `VideoFileMetadata` and call provider to match.
5. MCP check (already in place from T-MCP02).

You **must NOT**:
- Change existing music/audio scanning logic
- Make external metadata API calls (T-VID03)

---

### 1. Recon

Find:
1. Scanner implementation (`ShareScanner` or equivalent)
2. Where file extensions are checked
3. Where `IModerationProvider.CheckLocalFileAsync` is called (T-MCP02)

---

### 2. IVideoMetadataExtractor Interface

```csharp
public interface IVideoMetadataExtractor
{
    /// <summary>
    ///     Extract video metadata using ffprobe.
    /// </summary>
    Task<VideoFileMetadata?> ExtractAsync(
        string filePath,
        CancellationToken ct);
    
    /// <summary>
    ///     Check if extension is a video format.
    /// </summary>
    bool IsSupportedFormat(string extension);
}
```

---

### 3. Implement VideoMetadataExtractor

Use `ffprobe` (must be installed on system):

```csharp
public class FfprobeVideoMetadataExtractor : IVideoMetadataExtractor
{
    public bool IsSupportedFormat(string extension)
    {
        var videoExts = new[] { ".mp4", ".mkv", ".avi", ".mov", ".m4v", ".wmv", ".flv" };
        return videoExts.Contains(extension.ToLowerInvariant());
    }
    
    public async Task<VideoFileMetadata?> ExtractAsync(string filePath, CancellationToken ct)
    {
        // Run: ffprobe -v quiet -print_format json -show_format -show_streams "filePath"
        // Parse JSON output for:
        //   - Duration: format.duration
        //   - Resolution: streams[video].width x streams[video].height
        //   - Video codec: streams[video].codec_name
        //   - Audio codec: streams[audio].codec_name
        //   - Audio channels: streams[audio].channels
        //   - Subtitle languages: streams[subtitle].tags.language
        
        // Also parse filename for:
        //   - Movie: "Title (Year).ext" or "Title.Year.ext"
        //   - TV: "Show.S01E02.ext" or "Show.1x02.ext"
        
        return new VideoFileMetadata
        {
            Filename = Path.GetFileName(filePath),
            SizeBytes = new FileInfo(filePath).Length,
            Duration = parsedDuration,
            Resolution = $"{width}x{height}",
            VideoCodec = videoCodec,
            AudioCodec = audioCodec,
            AudioLayout = DetermineAudioLayout(channels),
            SubtitleLanguages = subtitleLangs.ToArray(),
            ParsedTitle = parsedTitle,
            ParsedYear = parsedYear,
            ParsedSeason = parsedSeason,
            ParsedEpisode = parsedEpisode
        };
    }
}
```

---

### 4. Integrate into Scanner

In scanner file loop:

```csharp
if (_videoMetadataExtractor.IsSupportedFormat(extension))
{
    var videoMetadata = await _videoMetadataExtractor.ExtractAsync(filePath, ct);
    
    if (videoMetadata != null)
    {
        // Build LocalFileMetadata for MCP
        var localFileMetadata = new LocalFileMetadata
        {
            Id = Path.GetFileName(filePath),
            SizeBytes = videoMetadata.SizeBytes,
            PrimaryHash = await ComputeHashAsync(filePath, ct),
            MediaInfo = $"Video: {videoMetadata.Resolution} {videoMetadata.VideoCodec}"
        };
        
        // MCP check (already wired from T-MCP02)
        var decision = await _moderationProvider.CheckLocalFileAsync(localFileMetadata, ct);
        
        if (decision.Verdict == ModerationVerdict.Blocked)
        {
            // Do not mark as shareable
            continue;
        }
        
        // Try to match to movie/episode
        // (Provider will return null until T-VID03/04 implemented)
        IContentItem? videoItem = null;
        
        if (videoMetadata.ParsedSeason.HasValue)
        {
            // TV episode
            videoItem = await _tvProvider.TryGetEpisodeByLocalMatchAsync(videoMetadata, ct);
        }
        else
        {
            // Movie
            videoItem = await _movieProvider.TryGetItemByLocalMatchAsync(videoMetadata, ct);
        }
        
        // Store video file record
    }
}
```

---

### 5. Tests (T-VID02)

Add tests:
1. Scanner recognizes video extensions
2. Metadata extractor returns correct `VideoFileMetadata`
3. MCP is called before marking video as shareable
4. Blocked videos are NOT added to shareable set

Use fake/test video files for testing.

---

## T-VID03 – Movie/TV Metadata Services via Catalogue Fetch

**Priority**: P2  
**Status**: 📋 Planned  
**Depends on**: T-PR02 (Catalogue fetch), T-VID01  
**Blocks**: T-VID04

**Goal**: Implement metadata services using catalogue-fetch with TMDB/TVDB.

---

### 0. Scope

You **must**:
1. Implement `MovieMetadataService` using `ICatalogFetchService`.
2. Implement `TvMetadataService` using `ICatalogFetchService`.
3. Query TMDB/TVDB APIs (whitelisted domains).
4. Normalize responses into `MovieWork`/`MovieItem` and `TvShowWork`/`EpisodeItem`.
5. Cache responses and respect work budgets.

You **must NOT**:
- Make direct HTTP calls (use catalogue-fetch)
- Add API keys to logs
- Exceed rate limits (cache + work budget)

---

### 1. Configuration

Add to `Options.cs`:

```csharp
public class VideoMetadataOptions
{
    public bool Enabled { get; init; } = false;
    public string TmdbApiKey { get; init; }
    public string TvdbApiKey { get; init; }
    public int CacheDurationMinutes { get; init; } = 60;
}
```

---

### 2. MovieMetadataService

```csharp
public class MovieMetadataService
{
    private readonly ICatalogFetchService _catalogFetch;
    private readonly IOptionsMonitor<VideoMetadataOptions> _options;
    
    public async Task<MovieWork?> GetMovieByTmdbIdAsync(string tmdbId, CancellationToken ct)
    {
        var url = $"https://api.themoviedb.org/3/movie/{tmdbId}?api_key={_options.CurrentValue.TmdbApiKey}";
        
        var response = await _catalogFetch.FetchAsync(new CatalogFetchRequest
        {
            Url = url,
            Method = "GET",
            MaxResponseBytes = 50000,
            TimeoutSeconds = 5
        }, ct);
        
        if (!response.Success)
        {
            return null;
        }
        
        var json = Encoding.UTF8.GetString(response.Body);
        var tmdbMovie = JsonSerializer.Deserialize<TmdbMovieResponse>(json);
        
        return new MovieWork
        {
            Id = ContentWorkId.NewId(),
            Title = tmdbMovie.title,
            Year = ParseYear(tmdbMovie.release_date),
            TmdbId = tmdbId,
            ImdbId = tmdbMovie.imdb_id,
            Runtime = TimeSpan.FromMinutes(tmdbMovie.runtime),
            Genres = tmdbMovie.genres.Select(g => g.name).ToArray()
        };
    }
}
```

Similar for `TvMetadataService`.

---

### 3. Tests (T-VID03)

Add tests using mock catalogue-fetch:
1. Successful TMDB lookup
2. Failed lookup returns null
3. Malformed JSON handled gracefully
4. Work budget enforced

---

## T-VID04 – Video Matching, Verification & Quality Scoring

**Priority**: P2  
**Status**: 📋 Planned  
**Depends on**: T-VID03  
**Blocks**: T-VID05

**Goal**: Implement matching logic and quality scoring for videos.

---

### 0. Scope

Implement in providers:
1. **Matching**:
   - Primary: External IDs (TMDB/TVDB)
   - Secondary: Title+Year+Runtime (movies), Show+Season+Episode (TV)
2. **Verification**:
   - Hash match
   - Runtime within tolerance (±5 minutes for movies, ±2 for episodes)
3. **Quality Scoring** (`VideoCopyQuality`):
   - Resolution: 2160p > 1080p > 720p > 480p
   - Codec: AV1 > HEVC > H.264
   - HDR/Dolby Vision bonus
   - Audio: Atmos > 7.1 > 5.1 > Stereo

---

### 1. VideoCopyQuality

```csharp
public sealed class VideoCopyQuality
{
    public int Score { get; init; }
    public string Resolution { get; init; }
    public string VideoCodec { get; init; }
    public bool HasHdr { get; init; }
    public string AudioLayout { get; init; }
    
    public static VideoCopyQuality CalculateQuality(VideoFileMetadata file)
    {
        int score = 0;
        
        // Resolution scoring
        if (file.Resolution?.Contains("3840") == true) score += 100;  // 4K
        else if (file.Resolution?.Contains("1920") == true) score += 75;  // 1080p
        else if (file.Resolution?.Contains("1280") == true) score += 50;  // 720p
        
        // Codec scoring
        if (file.VideoCodec == "av1") score += 30;
        else if (file.VideoCodec == "hevc") score += 20;
        else if (file.VideoCodec == "h264") score += 10;
        
        // HDR bonus
        if (file.HasHdr) score += 25;
        
        // Audio scoring
        if (file.AudioLayout?.Contains("Atmos") == true) score += 20;
        else if (file.AudioLayout == "7.1") score += 15;
        else if (file.AudioLayout == "5.1") score += 10;
        else if (file.AudioLayout == "Stereo") score += 5;
        
        return new VideoCopyQuality
        {
            Score = score,
            Resolution = file.Resolution,
            VideoCodec = file.VideoCodec,
            HasHdr = file.HasHdr,
            AudioLayout = file.AudioLayout
        };
    }
}
```

---

### 2. Tests (T-VID04)

Add tests:
1. Quality scoring prefers 4K over 1080p
2. Quality scoring prefers HEVC over H.264
3. Matching by TMDB ID
4. Matching by title+year+runtime
5. Runtime verification within tolerance

---

## T-VID05 – Planner & Library Reconciliation for Video

**Priority**: P2  
**Status**: 📋 Planned  
**Depends on**: T-VID04, T-VC04 (Domain-aware planner)

**Goal**: Integrate video domains into planner and library reconciliation.

---

### 0. Scope

You **must**:
1. Planner handles `ContentDomain.Movie` and `ContentDomain.Tv` intents.
2. Use quality scoring to rank candidates.
3. **ENFORCE**: Movies and TV MUST NOT use Soulseek backend.
4. Library reconciliation shows have/missing/low-quality for movies and TV shows.

You **must NOT**:
- Allow Soulseek backend for video (critical!)
- Change music domain behavior

---

### 1. Backend Enforcement

In planner:

```csharp
if (intent.Domain == ContentDomain.Movie || intent.Domain == ContentDomain.Tv)
{
    // CRITICAL: Video domains MUST NOT use Soulseek
    allowedBackends = new[] { Backend.MeshDHT, Backend.Torrent, Backend.HTTP, Backend.Local };
    
    if (allowedBackends.Contains(Backend.Soulseek))
    {
        throw new InvalidOperationException(
            $"Soulseek backend is not allowed for {intent.Domain} domain");
    }
}
```

---

### 2. Library Reconciliation

Extend reconciliation to show:
- **Movies**: Have / Missing / Low-Quality (by director, by year, by genre)
- **TV Shows**: Have / Missing episodes (by show, by season)

---

### 3. Tests (T-VID05)

Add tests:
1. Movie intent uses mesh/torrent/HTTP, NEVER Soulseek
2. TV intent uses mesh/torrent/HTTP, NEVER Soulseek
3. Quality-based ranking works
4. Library reconciliation shows correct states

---

## Execution Order

Recommended sequence:
1. **T-VID01** (types & interfaces) - Foundational
2. **T-VID02** (scanner integration) - Can happen in parallel with T-VID03
3. **T-VID03** (metadata services) - Requires T-PR02 (catalogue fetch)
4. **T-VID04** (matching & quality) - Build on T-VID03
5. **T-VID05** (planner integration) - Final integration

---

## Anti-Slop Summary

✅ **DO**:
- Add Movie and Tv as new domains
- Use catalogue-fetch for TMDB/TVDB
- Enforce no-Soulseek rule strictly
- Quality scoring based on technical specs

❌ **DON'T**:
- Break existing music domain
- Allow Soulseek for video
- Make direct HTTP calls (use catalogue-fetch)
- Log API keys or full file paths
