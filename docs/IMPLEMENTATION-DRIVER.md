# Implementation Driver - experimental/whatAmIThinking

**Branch**: `experimental/whatAmIThinking`  
**Repo**: `github.com/snapetech/slskdn`  
**Status**: ACTIVE IMPLEMENTATION GUIDE  
**Last Updated**: December 11, 2025

> **Project Note**: This is a fork of [slskd](https://github.com/slskd/slskd). See [../README.md](../README.md#acknowledgments) for attribution.

---

## Overview

You are working on the **experimental/whatAmIThinking** branch. The design and task docs have just been updated with comprehensive architecture for:

- VirtualSoulfind v2 (multi-domain)
- Security hardening
- Moderation / Control Plane (MCP)
- LLM/AI integration
- Social federation
- Multi-domain support (Music, Books, Movies, TV)
- UI/Library dashboards

---

## Source of Truth Documents

**Treat THESE docs as the source of truth.** Do not invent new concepts that contradict them.

### Primary Design Docs

- `docs/security-hardening-guidelines.md` - Security/privacy requirements
- `docs/virtualsoulfind-v2-design.md` - Multi-domain architecture
- `docs/video-domain-design.md` - Movies & TV domain
- `docs/book-domain-design.md` - Books/ebooks domain
- `docs/ui-library-dashboards.md` - API/UI layer
- `docs/social-federation-design.md` - ActivityPub integration
- `docs/moderation-v1-design.md` - MCP architecture
- `docs/engineering-standards.md` - Code quality standards
- `TASK_STATUS_DASHBOARD.md` - All tasks (T-VC*, T-VID*, T-BK*, T-UI*, T-FED*, H-*, T-MCP-LM*)

### Supporting Docs

- `docs/CURSOR-META-INSTRUCTIONS.md` - Meta-rules for implementation
- `SECURITY-GUIDELINES.md` - Global security requirements
- `MCP-HARDENING.md` - Moderation layer security

---

## Guiding Principles

If there is ambiguity, prefer:

1. **Security, privacy, and anonymity** over convenience.
2. **Metadata/control-plane separation** over "just fetch the file".
3. **Domain-aware behavior** (Music vs Video vs Book vs GenericFile).
4. **MCP as hard gate** - All content paths gated by moderation.
5. **Work budgets** - All network-heavy operations consume budget.

---

## Implementation Scope for This Branch (Feature-Freeze Cut)

### Goal

A **working, secure, Music-first VirtualSoulfind v2 spine** with:

- Clean domain-aware planner.
- Clean MCP integration with LLM hooks (safely off by default).
- Social/Video/Book present as aligned scaffolding, not fully-featured.

### Do NOT Try to "Finish All Future Features"

This branch establishes the foundation. Future branches will complete:
- Full Video/Book domain implementations
- Full social federation features
- Advanced LLM-assisted moderation
- Complete UI/dashboards

---

## Phase 1: VirtualSoulfind v2 Core (Music-Focused)

### Tasks: T-VC01 (Parts 3-5), T-VC02

#### 1.1 ContentDomain Foundation

Implement as per `docs/virtualsoulfind-v2-design.md`:

- ✅ **DONE**: `ContentDomain` enum (Music, GenericFile, Book, Movie, Tv)
- ✅ **DONE**: `ContentWorkId`, `ContentItemId` (value types/record structs)
- ✅ **DONE**: `IContentWork`, `IContentItem` interfaces
- ✅ **DONE**: `MusicWork`, `MusicItem` adapters

**TODO**: Integrate into existing VirtualSoulfind code paths:
- Update existing code to use `ContentDomain.Music` explicitly
- Ensure no behavior changes to existing music flows
- Add comprehensive tests

#### 1.2 IMusicContentDomainProvider

Implement/refactor to wrap existing music logic:

```csharp
public interface IMusicContentDomainProvider : IContentDomainProvider
{
    Task<MusicWork?> TryGetWorkByMbidAsync(string mbid, CancellationToken ct);
    Task<MusicItem?> TryMatchTrackByFingerprintAsync(LocalFileMetadata file, string fingerprint, CancellationToken ct);
    Task<MusicItem?> TryMatchTrackByTagsAsync(LocalFileMetadata file, MusicTags tags, CancellationToken ct);
    Task<MusicCopyQuality> AssessQualityAsync(ContentItemId itemId, CancellationToken ct);
}
```

**Requirements**:
- Use Chromaprint + tags + MBID
- Provide `MusicWork` / `MusicItem` types implementing neutral interfaces
- Migrate existing Chromaprint usage into this provider

#### 1.3 Domain-Aware Planner

Update the planner:

- Accept `ContentDomain` on every intent
- Route `Music` intents to `IMusicContentDomainProvider`
- Enforce backend rules:
  - **Music**: Can use Soulseek, mesh, torrent, HTTP, local
  - **Video/Book/GenericFile**: Can use mesh, torrent, HTTP, local (NO Soulseek)

**Code Pattern**:

```csharp
public async Task<Plan> CreatePlanAsync(Intent intent, CancellationToken ct)
{
    var provider = intent.Domain switch
    {
        ContentDomain.Music => _musicProvider,
        ContentDomain.Book => _bookProvider,
        ContentDomain.Movie => _movieProvider,
        ContentDomain.Tv => _tvProvider,
        ContentDomain.GenericFile => _genericFileProvider,
        _ => throw new ArgumentException($"Unknown domain: {intent.Domain}")
    };
    
    // Enforce backend rules
    var allowedBackends = GetAllowedBackendsForDomain(intent.Domain);
    
    // Rest of planner logic...
}
```

#### 1.4 Music Reconciliation & Quality Scoring

Implement:

- `MusicCopyQuality` struct with:
  - `CodecScore` (lossless > lossy)
  - `BitrateScore`
  - `SampleRateScore`
  - `OverallScore` (normalized aggregate)

- Reconciliation views:
  - Per-artist: albums with completion/quality
  - Per-album: tracks with presence/quality/verified flags

---

## Phase 2: Security / Hardening Spine

### Tasks: H-ID01, H-GLOBAL01, H-VF01, H-MCP01

#### 2.1 Identity Separation

Implement `docs/security-hardening-guidelines.md` § 15:

```csharp
public class IdentityConfig
{
    public MeshIdentity Mesh { get; init; }
    public SoulseekIdentity Soulseek { get; init; }
    public ActivityPubIdentities Social { get; init; }
    
    public class MeshIdentity
    {
        public byte[] KeyPair { get; init; }
        public string PeerId { get; init; }
    }
    
    public class SoulseekIdentity
    {
        public string Username { get; init; }
        public string Password { get; init; } // protected
    }
    
    public class ActivityPubIdentities
    {
        public Dictionary<string, ActorIdentity> Actors { get; init; } // e.g., "music", "books"
    }
}
```

**Requirements**:
- Separate keypairs per identity type
- No automatic derivation across layers
- Stored in dedicated config structure

#### 2.2 Logging/Metrics Hygiene

For **all new code**, ensure:

**DO NOT LOG**:
- Full filesystem paths
- Raw hashes (SHA-256, etc.)
- IP addresses of peers
- External usernames or ActivityPub handles
- Full HTTP request headers or bodies

**DO LOG**:
- Internal IDs (ContentWorkId, ContentItemId, PeerId) in sanitized forms
- Operation results (success/failure)
- Reason codes (not detailed errors with PII)

**Metrics**:
- Use **low-cardinality labels only**: `backend`, `result`, `domain`, `privacyMode`
- **NEVER**: file names, paths, hashes, full URLs, external handles

#### 2.3 SSRF-Safe HTTP

Ensure **any new HTTP calls** use the SSRF-safe HTTP client:

```csharp
// Good
var response = await _ssrfSafeHttpClient.GetAsync(url, allowedDomains, cancellationToken);

// Bad
var response = await new HttpClient().GetAsync(url); // NEVER do this
```

---

## Phase 3: MCP Pipeline (Basic Integration)

### Tasks: T-MCP01 ✅, T-MCP02 ✅, T-MCP03, T-MCP04

#### 3.1 CompositeModerationProvider

Implement as per `docs/moderation-v1-design.md`:

```csharp
public class CompositeModerationProvider : IModerationProvider
{
    private readonly IHashBlocklistChecker? _hashBlocklist;
    private readonly IPeerReputationStore? _reputation;
    private readonly IExternalModerationClient? _llmClient;
    
    public async Task<ModerationDecision> CheckLocalFileAsync(LocalFileMetadata file, CancellationToken ct)
    {
        // 1. Hash/blocklist check (fast, deterministic)
        if (_hashBlocklist != null)
        {
            var isBlocked = await _hashBlocklist.IsBlockedHashAsync(file.PrimaryHash, ct);
            if (isBlocked)
                return ModerationDecision.Block("hash_blocklist", "hash_match");
        }
        
        // 2. Reputation check (local, deterministic)
        // TODO: Implement in T-MCP04
        
        // 3. LLM check (slow, probabilistic, OPTIONAL)
        if (_llmClient != null && _config.Llm.Mode != LlmMode.Off)
        {
            // See Phase 4 for LLM integration
        }
        
        return ModerationDecision.Unknown();
    }
}
```

**Stub Providers** (for now):

```csharp
public class StubHashBlocklistChecker : IHashBlocklistChecker
{
    public Task<bool> IsBlockedHashAsync(string hash, CancellationToken ct)
        => Task.FromResult(false); // TODO: Implement actual logic
}

public class StubPeerReputationStore : IPeerReputationStore
{
    // TODO: Implement in T-MCP04
}
```

#### 3.2 MCP Integration Points

Integrate MCP into:

**Library Scanning** (T-MCP02 ✅ DONE):
- ✅ Call `IModerationProvider.CheckLocalFileAsync` before file insertion
- ✅ Set `isBlocked`, `isQuarantined` flags in database
- ✅ Filter `ListFiles` to exclude blocked/quarantined

**VirtualSoulfind IsAdvertisable** (T-MCP03):
- Add `IsAdvertisable` flag to content items
- When linking file to `ContentItemId`:
  ```csharp
  var decision = await _moderationProvider.CheckContentIdAsync(contentId, ct);
  item.IsAdvertisable = decision.Verdict is ModerationVerdict.Allowed or ModerationVerdict.Unknown;
  ```

**Planner Source Selection** (T-MCP03):
- Skip sources/peers/content marked as blocked/quarantined
- Skip sources with poor reputation

---

## Phase 4: LLM/MCP Integration (Wired, LLM Off)

### Tasks: T-MCP-LM01, T-MCP-LM02

#### 4.1 Abstractions

Implement types from `docs/moderation-v1-design.md` + LLM extensions:

```csharp
public record ModerationRequest
{
    public ContentDomain? DomainHint { get; init; }
    public string? SourceType { get; init; } // "local_file", "social_note", etc.
    public string? Title { get; init; }
    public string? Description { get; init; }
    public string[]? Tags { get; init; }
    public string? ShortSnippet { get; init; } // Optional, <= 500 chars
}

public record ModerationResponse
{
    public Dictionary<string, float> CategoryScores { get; init; } // e.g., { "violence": 0.1, "spam": 0.8 }
    public string[] ReasonCodes { get; init; }
    public float Confidence { get; init; } // 0.0 to 1.0
}

public interface IExternalModerationClient
{
    Task<ModerationResponse> CheckAsync(ModerationRequest request, CancellationToken ct);
}

public class ModerationConfig
{
    public LlmConfig Llm { get; init; } = new();
    
    public class LlmConfig
    {
        public LlmMode Mode { get; init; } = LlmMode.Off; // Off | Local | Remote
        public DataMode DataMode { get; init; } = DataMode.MetadataOnly;
        public int MaxRequestsPerMinute { get; init; } = 10;
        public int MaxCharsPerRequest { get; init; } = 1000;
        public int TimeoutSeconds { get; init; } = 5;
    }
}

public enum LlmMode { Off, Local, Remote }
public enum DataMode { MetadataOnly, MetadataPlusShortSnippet }
```

#### 4.2 LlmModerationProvider

Implement:

```csharp
public class LlmModerationProvider : IModerationProvider
{
    private readonly IExternalModerationClient _client;
    private readonly IOptionsMonitor<ModerationConfig> _config;
    
    public async Task<ModerationDecision> CheckLocalFileAsync(LocalFileMetadata file, CancellationToken ct)
    {
        if (_config.CurrentValue.Llm.Mode == LlmMode.Off)
            return ModerationDecision.Unknown();
        
        // Data minimization
        var request = new ModerationRequest
        {
            Title = Path.GetFileName(file.Id), // Filename only, no full path
            // NO paths, hashes, peer IDs
        };
        
        var response = await _client.CheckAsync(request, ct);
        
        // Map response to verdict
        if (response.CategoryScores.Any(kvp => kvp.Value > 0.8f))
            return ModerationDecision.Block("ai_disallowed_category", response.ReasonCodes);
        
        return ModerationDecision.Unknown();
    }
}
```

#### 4.3 NoopExternalModerationClient

For now, wire **only** the noop implementation:

```csharp
public class NoopExternalModerationClient : IExternalModerationClient
{
    public Task<ModerationResponse> CheckAsync(ModerationRequest request, CancellationToken ct)
    {
        return Task.FromResult(new ModerationResponse
        {
            CategoryScores = new Dictionary<string, float>(),
            ReasonCodes = Array.Empty<string>(),
            Confidence = 0f
        });
    }
}
```

**Ensure default config is `Mode = Off`.**

---

## Phase 5: Social / Federation Skeleton

### Tasks: T-FED01, T-FED02

#### 5.1 SocialFederationService

Implement minimal ActivityPub server skeleton:

```csharp
public class SocialFederationService
{
    private readonly IOptionsMonitor<SocialFederationConfig> _config;
    
    public SocialFederationService(IOptionsMonitor<SocialFederationConfig> config)
    {
        _config = config;
    }
    
    // WebFinger
    public async Task<WebFingerResponse?> GetWebFingerAsync(string resource, CancellationToken ct)
    {
        if (_config.CurrentValue.Mode == FederationMode.Hermit)
            return null; // 404 in Hermit mode
        
        // Parse resource (e.g., "acct:music@pod.example")
        // Return actor URL if valid
    }
    
    // Actor docs
    public async Task<Actor?> GetActorAsync(string actorName, CancellationToken ct)
    {
        if (_config.CurrentValue.Mode == FederationMode.Hermit)
            return null; // 404 in Hermit mode
        
        // Return Library Actor for @music, @books, @movies, @tv
    }
    
    // Inbox (stub)
    public async Task<IActionResult> HandleInboxAsync(Activity activity, CancellationToken ct)
    {
        if (_config.CurrentValue.Mode == FederationMode.Hermit)
            return new StatusCodeResult(404);
        
        // TODO: Implement in future branch
        return new AcceptedResult();
    }
    
    // Outbox (stub)
    public async Task<Collection?> GetOutboxAsync(string actorName, CancellationToken ct)
    {
        if (_config.CurrentValue.Mode == FederationMode.Hermit)
            return null;
        
        // TODO: Implement in future branch
        return new Collection { TotalItems = 0, Items = Array.Empty<object>() };
    }
}

public class SocialFederationConfig
{
    public FederationMode Mode { get; init; } = FederationMode.Hermit; // Default: off
    public string? InstanceDomain { get; init; }
}

public enum FederationMode { Hermit, FriendsOnly, Public }
```

#### 5.2 Library Actor + WorkRef Serialization

Implement:

```csharp
public record Actor
{
    [JsonPropertyName("@context")]
    public string[] Context { get; init; } = new[] { "https://www.w3.org/ns/activitystreams" };
    
    [JsonPropertyName("type")]
    public string Type { get; init; } = "Service";
    
    [JsonPropertyName("id")]
    public string Id { get; init; }
    
    [JsonPropertyName("preferredUsername")]
    public string PreferredUsername { get; init; }
    
    [JsonPropertyName("name")]
    public string Name { get; init; }
    
    [JsonPropertyName("inbox")]
    public string Inbox { get; init; }
    
    [JsonPropertyName("outbox")]
    public string Outbox { get; init; }
    
    [JsonPropertyName("publicKey")]
    public PublicKey PublicKey { get; init; }
}

public record WorkRef
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "WorkRef";
    
    [JsonPropertyName("id")]
    public string Id { get; init; }
    
    [JsonPropertyName("domain")]
    public string Domain { get; init; } // "Music", "Book", "Movie", "Tv"
    
    [JsonPropertyName("externalIds")]
    public Dictionary<string, string> ExternalIds { get; init; }
    
    [JsonPropertyName("title")]
    public string Title { get; init; }
    
    [JsonPropertyName("creator")]
    public string? Creator { get; init; }
    
    [JsonPropertyName("year")]
    public int? Year { get; init; }
    
    // MUST NOT include: local paths, hashes, peer IDs
}
```

**Do NOT fully implement**:
- Publishing lists
- Social ingestion
- Circles, rooms, tags, complex signals

These remain TODO for future branches, but types/interfaces should be present.

---

## Phase 6: Video + Book Domains (Type-Complete, Minimal Integration)

### Tasks: T-VID01, T-BK01

#### 6.1 Video Domain Types

Implement as per `docs/video-domain-design.md`:

```csharp
public sealed class MovieWork : IContentWork
{
    public ContentWorkId Id { get; init; }
    public ContentDomain Domain => ContentDomain.Movie;
    public string Title { get; init; }
    public int? Year { get; init; }
    public Dictionary<string, string> ExternalIds { get; init; } // TMDB, IMDB, etc.
    public int? RuntimeMinutes { get; init; }
}

public sealed class MovieItem : IContentItem
{
    public ContentItemId Id { get; init; }
    public ContentDomain Domain => ContentDomain.Movie;
    public ContentWorkId WorkId { get; init; }
    public string? EditionName { get; init; }
    public string? Resolution { get; init; }
    public string? VideoCodec { get; init; }
    public string? AudioCodec { get; init; }
}

public sealed class TvShowWork : IContentWork { /* Similar */ }
public sealed class EpisodeItem : IContentItem { /* Similar */ }

public interface IMovieContentDomainProvider : IContentDomainProvider
{
    Task<MovieWork?> TryGetWorkByExternalIdAsync(string externalId, CancellationToken ct);
    // More methods...
}

public interface ITvContentDomainProvider : IContentDomainProvider
{
    Task<TvShowWork?> TryGetShowByExternalIdAsync(string externalId, CancellationToken ct);
    // More methods...
}
```

#### 6.2 Book Domain Types

Implement as per `docs/book-domain-design.md`:

```csharp
public sealed class BookWork : IContentWork
{
    public ContentWorkId Id { get; init; }
    public ContentDomain Domain => ContentDomain.Book;
    public string Title { get; init; }
    public string[] Authors { get; init; }
    public string? SeriesName { get; init; }
    public string? SeriesIndex { get; init; }
    public Dictionary<string, string> ExternalIds { get; init; } // ISBN, OpenLibrary, etc.
}

public sealed class BookItem : IContentItem
{
    public ContentItemId Id { get; init; }
    public ContentDomain Domain => ContentDomain.Book;
    public ContentWorkId WorkId { get; init; }
    public string Format { get; init; } // EPUB, PDF, MOBI
    public string? Language { get; init; }
    public int? PageCount { get; init; }
}

public interface IBookContentDomainProvider : IContentDomainProvider
{
    Task<BookWork?> TryGetWorkByIsbnAsync(string isbn, CancellationToken ct);
    // More methods...
}
```

#### 6.3 Minimal Provider Integration

For now, providers can:

- Do **minimal matching** (basic filename-based or placeholder)
- Provide **stubs for metadata services**
- Return `Task.FromException(new NotImplementedException("TODO: T-VID02"))` with clear TODOs

**Example**:

```csharp
public class StubMovieContentDomainProvider : IMovieContentDomainProvider
{
    public Task<MovieWork?> TryGetWorkByExternalIdAsync(string externalId, CancellationToken ct)
    {
        // TODO: T-VID03 - Implement TMDB integration
        return Task.FromResult<MovieWork?>(null);
    }
}
```

#### 6.4 Planner Integration

Ensure planner knows about these domains:

```csharp
ContentDomain.Movie => _movieProvider ?? throw new NotImplementedException("TODO: T-VID01"),
ContentDomain.Tv => _tvProvider ?? throw new NotImplementedException("TODO: T-VID01"),
ContentDomain.Book => _bookProvider ?? throw new NotImplementedException("TODO: T-BK01"),
```

**Enforce backend rules**:
- Video/Book **MUST NOT** use Soulseek backend
- Throw exception if attempted

---

## Phase 7: UI / Library Endpoints (Backend Only)

### Tasks: T-UI01, T-UI02

#### 7.1 Core Endpoints

Implement as per `docs/ui-library-dashboards.md`:

```csharp
[ApiController]
[Route("api/library")]
public class LibraryController : ControllerBase
{
    private readonly IVirtualSoulfindService _vsf;
    private readonly IModerationProvider _mcp;
    
    [HttpGet("overview/{domain}")]
    public async Task<DomainOverview> GetDomainOverview(ContentDomain domain, CancellationToken ct)
    {
        // Use VirtualSoulfind reconciliation data
        // Hide paths, hashes, IPs, external handles
        // Obey MCP: blocked/quarantined hidden
    }
    
    [HttpGet("work/{workId}")]
    public async Task<WorkDetails> GetWorkDetails(Guid workId, CancellationToken ct)
    {
        var work = await _vsf.GetWorkAsync(new ContentWorkId(workId), ct);
        if (work == null) return NotFound();
        
        // Check MCP
        var decision = await _mcp.CheckContentIdAsync(workId.ToString(), ct);
        if (decision.IsBlocking())
            return NotFound(); // Don't reveal existence of blocked content
        
        return MapToDto(work);
    }
}
```

#### 7.2 Music-Specific Views

Implement:

```csharp
[HttpGet("music/artists")]
public async Task<IEnumerable<ArtistSummary>> ListArtists(CancellationToken ct)
{
    // Use IMusicContentDomainProvider + VirtualSoulfind
    // No paths, hashes, IPs
}

[HttpGet("music/artist/{artistId}")]
public async Task<ArtistDetails> GetArtistSummary(Guid artistId, CancellationToken ct)
{
    // Albums, completion, quality per artist
}

[HttpGet("music/album/{albumId}")]
public async Task<AlbumDetails> GetAlbumDetails(Guid albumId, CancellationToken ct)
{
    // Track list with presence, quality, verified flags
}
```

**Requirements**:
- Use VirtualSoulfind reconciliation and quality scores
- Hide paths, hashes, IPs, external handles
- Obey MCP: blocked content does not appear in normal responses

---

## Code Quality / Anti-Slop Rules

For **all new code** in this branch, follow `docs/engineering-standards.md`:

### Async Rules (CRITICAL)

❌ **NEVER**:
```csharp
var result = SomeAsyncMethod().Result; // Deadlock risk!
Task.Run(() => SyncMethod()); // Anti-pattern
```

✅ **ALWAYS**:
```csharp
var result = await SomeAsyncMethod(cancellationToken);
await foreach (var item in GetItemsAsync(cancellationToken))
```

### Dependency Injection

✅ **DO**:
```csharp
public class MyService
{
    private readonly IDependency _dependency;
    
    public MyService(IDependency dependency)
    {
        _dependency = dependency ?? throw new ArgumentNullException(nameof(dependency));
    }
}
```

❌ **DON'T**:
```csharp
public class MyService
{
    private static readonly Dependency _dependency = new(); // No global singletons
}
```

### Testing

**Add unit tests** for:
- Domain providers (matching logic, quality scoring)
- Planner (backend selection, domain routing)
- MCP wiring (integration points, decision mapping)

**Test Pattern**:
```csharp
[Fact]
public async Task Planner_MusicIntent_CanUseSoulseek()
{
    // Arrange
    var intent = new Intent { Domain = ContentDomain.Music };
    
    // Act
    var plan = await _planner.CreatePlanAsync(intent, CancellationToken.None);
    
    // Assert
    Assert.Contains(plan.AllowedBackends, b => b.Type == BackendType.Soulseek);
}

[Fact]
public async Task Planner_VideoIntent_CannotUseSoulseek()
{
    // Arrange
    var intent = new Intent { Domain = ContentDomain.Movie };
    
    // Act & Assert
    var plan = await _planner.CreatePlanAsync(intent, CancellationToken.None);
    Assert.DoesNotContain(plan.AllowedBackends, b => b.Type == BackendType.Soulseek);
}
```

---

## Handling Conflicts with Existing Code

If any existing code conflicts with the new docs:

1. **Prefer the docs** and adjust the code.
2. **Keep behavior backward-compatible** where possible.
3. **Add adapter layers** if needed (e.g., `MusicWork` wrapping `AlbumTargetEntry`).
4. **Do not break existing tests** without good reason.

---

## What NOT to Implement in This Branch

**Do not try to finish all future features.** The following are OUT OF SCOPE for this branch:

- Full Video/Book metadata services (TMDB, TVDB, Open Library)
- Full Video/Book metadata extraction (ffprobe, EPUB parsing)
- Social federation publishing/ingestion (beyond skeleton)
- Circles, ephemeral rooms, federated tags
- LLM Local/Remote client implementations (beyond noop)
- Advanced LLM tagging/recommendations
- Complete UI/dashboards (frontend)
- Comprehensive testing suite (T-TEST series)

These will be completed in future branches. This branch establishes the **architecture and scaffolding**.

---

## Success Criteria

This branch is successful when:

✅ VirtualSoulfind v2 works for **Music domain** with:
- Domain-aware planner
- Quality scoring
- Reconciliation views

✅ MCP is integrated at all content paths:
- Library scanning
- VirtualSoulfind IsAdvertisable flags
- Planner source selection

✅ Security/hardening spine is in place:
- Identity separation
- Logging/metrics hygiene
- SSRF-safe HTTP

✅ LLM hooks are wired (but off by default)

✅ Video/Book/Social domains have **type-complete scaffolding**:
- All types/interfaces present
- Aligned with design docs
- Stubs for future implementation

✅ Code quality is maintained:
- No async anti-patterns
- Proper DI usage
- Tests for new logic
- No regressions

✅ Build is green, tests pass, linter clean

---

## Next Steps After This Branch

Future branches will:

1. **T-VID02-05**: Complete Video domain (ffprobe, TMDB, quality scoring)
2. **T-BK02-04**: Complete Book domain (EPUB parsing, Open Library, quality scoring)
3. **T-FED03-09**: Complete social federation (publishing, ingestion, circles, rooms)
4. **T-MCP-LM03-05**: Complete LLM integration (Local/Remote clients, pipeline integration)
5. **T-UI03-06**: Complete UI/dashboards (Video, Book, Collections, Admin views)
6. **T-TEST-01-07**: Comprehensive testing (network simulation, load testing, abuse scenarios)

---

**Remember**: The goal is a **solid, secure foundation**, not a complete feature set. Build the spine correctly, and the rest will follow.

