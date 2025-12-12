# Book Domain Support Tasks (T-BK Series)

**Status**: Planned (Phase E - After MCP + VirtualSoulfind Core)  
**Priority**: MEDIUM (after multi-domain foundation is stable)  
**Dependencies**: T-VC01-04 (Domain abstraction), T-MCP01-03 (Moderation core)

> **Project Note**: This is a fork of [slskd](https://github.com/slskd/slskd). See [README.md](README.md#acknowledgments) for attribution.

---

## Overview

These tasks add **Book** as a first-class content domain in VirtualSoulfind, with:
- ISBN-based matching and metadata
- Book-specific quality scoring (format, completeness, language)
- Integration with moderation (MCP) for blocked content
- Peer reputation for book-specific behavior

**Key Goal**: Enable book acquisition with the same robustness as music, using existing abstractions.

---

## Prerequisites

**Do NOT start these tasks until:**
1. ✅ T-VC01-04: Multi-domain foundation complete
2. ✅ T-MCP01-03: Moderation core + VirtualSoulfind gating complete
3. ✅ Domain-aware planner operational for Music domain

**Why**: Book domain builds on domain-neutral abstractions. Starting too early will cause merge conflicts and architectural issues.

---

## Task Sequence

```
T-BK01 → T-BK02 → T-BK03 → T-BK04
```

All tasks are **appended** to the roadmap; no renumbering of existing tasks.

---

## T-BK01 – Book Domain Types & Provider Interface

**Priority**: P1 (Foundation for book support)  
**Status**: 📋 Planned  
**Depends on**: T-VC01 (ContentDomain enum)  
**Blocks**: T-BK02, T-BK03, T-BK04

### Repo: `https://github.com/snapetech/slskdn`
### Branch: `experimental/multi-source-swarm`

**Goal**: Extend `ContentDomain` to support books and define book-specific types.

---

### 0. Scope & Non-Goals

You **must**:
1. Add `Book` to the `ContentDomain` enum.
2. Create `BookWork` and `BookItem` implementing `IContentWork`/`IContentItem`.
3. Define `IBookContentDomainProvider` interface.
4. Register a **stub** provider that returns null (no external calls yet).

You **must NOT**:
* Implement full metadata fetching (that's T-BK02).
* Change existing Music or GenericFile domain logic.
* Add any external API calls.

---

### 1. Recon

Find:
1. `ContentDomain` enum location (`src/slskd/VirtualSoulfind/Core/ContentDomain.cs`).
2. Existing domain provider interfaces (`IMusicContentDomainProvider`, etc.).
3. Where domain providers are registered in DI (`Program.cs`).

---

### 2. Extend ContentDomain Enum

Add `Book` to the enum:

```csharp
public enum ContentDomain
{
    Music = 0,
    GenericFile = 1,
    Book = 2,  // NEW
}
```

---

### 3. Book Domain Types

Create `BookWork` and `BookItem`:

```csharp
public sealed class BookWork : IContentWork
{
    public ContentWorkId Id { get; init; }
    public ContentDomain Domain => ContentDomain.Book;
    public string Title { get; init; }
    public string? Creator { get; init; } // Author(s)
    public int? Year { get; init; } // Publication year
    
    // Book-specific
    public string? Isbn { get; init; } // ISBN-10 or ISBN-13
    public string? Isbn13 { get; init; } // Normalized ISBN-13
    public string? Publisher { get; init; }
    public string? Language { get; init; } // ISO 639-1 code (e.g., "en")
}

public sealed class BookItem : IContentItem
{
    public ContentItemId Id { get; init; }
    public ContentDomain Domain => ContentDomain.Book;
    public ContentWorkId? WorkId { get; init; }
    public string Title { get; init; }
    public int? Position { get; init; } // Not used for books
    public TimeSpan? Duration { get; init; } // Not used for books
    
    // Book-specific
    public string? Isbn { get; init; }
    public string? Isbn13 { get; init; }
    public string Format { get; init; } // "EPUB", "PDF", "MOBI", etc.
    public int? PageCount { get; init; }
    public long? FileSizeBytes { get; init; }
    public string? Language { get; init; }
}
```

---

### 4. IBookContentDomainProvider Interface

Define the provider interface:

```csharp
public interface IBookContentDomainProvider
{
    /// <summary>
    ///     Attempts to resolve a book work by ISBN.
    /// </summary>
    Task<BookWork?> TryGetWorkByIsbnAsync(
        string isbn, 
        CancellationToken ct);
    
    /// <summary>
    ///     Attempts to resolve a book item by ISBN.
    /// </summary>
    Task<BookItem?> TryGetItemByIsbnAsync(
        string isbn, 
        CancellationToken ct);
    
    /// <summary>
    ///     Attempts to match a local file to a book item.
    /// </summary>
    Task<BookItem?> TryGetItemByLocalMatchAsync(
        BookFileMetadata metadata, 
        CancellationToken ct);
}

public sealed class BookFileMetadata
{
    public string FilePath { get; init; }
    public string Format { get; init; } // "EPUB", "PDF", etc.
    public string? Title { get; init; }
    public string[]? Authors { get; init; }
    public string? Isbn { get; init; }
    public int? PageCount { get; init; }
    public long SizeBytes { get; init; }
    public string? Language { get; init; }
}
```

---

### 5. Stub Implementation

Create a no-op provider:

```csharp
public class StubBookContentDomainProvider : IBookContentDomainProvider
{
    public Task<BookWork?> TryGetWorkByIsbnAsync(string isbn, CancellationToken ct)
    {
        return Task.FromResult<BookWork?>(null);
    }
    
    public Task<BookItem?> TryGetItemByIsbnAsync(string isbn, CancellationToken ct)
    {
        return Task.FromResult<BookItem?>(null);
    }
    
    public Task<BookItem?> TryGetItemByLocalMatchAsync(
        BookFileMetadata metadata, 
        CancellationToken ct)
    {
        return Task.FromResult<BookItem?>(null);
    }
}
```

Register in `Program.cs`:

```csharp
services.AddSingleton<IBookContentDomainProvider, StubBookContentDomainProvider>();
```

---

### 6. Tests (T-BK01)

Add tests:
1. `BookWork` and `BookItem` implement `IContentWork`/`IContentItem` correctly.
2. `ContentDomain.Book` enum value is defined.
3. Stub provider returns null for all methods.

---

### 7. Anti-Slop Checklist for T-BK01

* `Book` enum value added without changing Music or GenericFile.
* Types compile and are tested, but no external API calls.
* Stub provider is registered in DI.

---

## T-BK02 – Book-Aware Scanner & Metadata Extraction

**Priority**: P1 (Required for book discovery)  
**Status**: 📋 Planned  
**Depends on**: T-BK01, T-MCP02 (Scanner integration)  
**Blocks**: T-BK03

### Repo: `https://github.com/snapetech/slskdn`
### Branch: `experimental/multi-source-swarm`

**Goal**: Teach the scanner to recognize book files and extract metadata.

---

### 0. Scope & Non-Goals

You **must**:
1. Recognize book file extensions (`.epub`, `.pdf`, `.mobi`, `.azw3`, etc.).
2. Extract metadata (title, author, ISBN, page count, language) from book files.
3. Build `BookFileMetadata` for each discovered book.
4. Call MCP before marking as shareable (already wired from T-MCP02).

You **must NOT**:
* Change existing music or audio file scanning logic.
* Add external metadata API calls (that's in T-BK03 via provider).

---

### 1. Recon

Find:
1. Scanner implementation (`ShareScanner` or equivalent).
2. Where file extensions are checked.
3. Where `IModerationProvider.CheckLocalFileAsync` is called (from T-MCP02).

---

### 2. IBookMetadataExtractor Interface

Define metadata extraction:

```csharp
public interface IBookMetadataExtractor
{
    /// <summary>
    ///     Extracts metadata from a book file.
    /// </summary>
    Task<BookFileMetadata?> ExtractAsync(
        string filePath, 
        CancellationToken ct);
    
    /// <summary>
    ///     Checks if a file extension is a supported book format.
    /// </summary>
    bool IsSupportedFormat(string extension);
}
```

---

### 3. Implement Metadata Extraction

Use libraries for format-specific extraction:
- **EPUB**: `EpubSharp` or `VersOne.Epub`
- **PDF**: `iTextSharp` or `PdfSharp` (read metadata only, not full text)
- **MOBI/AZW3**: `MobiMetadata` or similar

Implementation:

```csharp
public class BookMetadataExtractor : IBookMetadataExtractor
{
    private static readonly string[] SupportedExtensions = 
        { ".epub", ".pdf", ".mobi", ".azw3" };
    
    public bool IsSupportedFormat(string extension)
    {
        return SupportedExtensions.Contains(
            extension.ToLowerInvariant());
    }
    
    public async Task<BookFileMetadata?> ExtractAsync(
        string filePath, 
        CancellationToken ct)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        
        return ext switch
        {
            ".epub" => await ExtractFromEpubAsync(filePath, ct),
            ".pdf" => await ExtractFromPdfAsync(filePath, ct),
            ".mobi" or ".azw3" => await ExtractFromMobiAsync(filePath, ct),
            _ => null
        };
    }
    
    // Format-specific extraction methods...
}
```

---

### 4. Scanner Integration

In the scanner, after discovering a file:

```csharp
// Check if it's a book
if (_bookMetadataExtractor.IsSupportedFormat(extension))
{
    var bookMetadata = await _bookMetadataExtractor.ExtractAsync(
        filePath, ct);
    
    if (bookMetadata != null)
    {
        // Build LocalFileMetadata for MCP
        var localFileMetadata = new LocalFileMetadata
        {
            Id = internalId,
            SizeBytes = bookMetadata.SizeBytes,
            PrimaryHash = await ComputeHashAsync(filePath, ct),
            MediaInfo = $"Book: {bookMetadata.Format}"
        };
        
        // MCP check (already wired from T-MCP02)
        var decision = await _moderationProvider.CheckLocalFileAsync(
            localFileMetadata, ct);
        
        if (decision.Verdict == ModerationVerdict.Blocked)
        {
            // Do not mark as shareable
            continue;
        }
        
        // Try to match to BookItem via provider
        var bookItem = await _bookProvider.TryGetItemByLocalMatchAsync(
            bookMetadata, ct);
        
        // Store book file record
        // (details depend on database schema)
    }
}
```

---

### 5. Tests (T-BK02)

Add tests:
1. Scanner recognizes `.epub`, `.pdf`, `.mobi` files.
2. Metadata extractor returns correct `BookFileMetadata`.
3. MCP is called before marking book as shareable.
4. Blocked books are NOT added to shareable set.

Use fake/test book files for testing.

---

### 7. Anti-Slop Checklist for T-BK02

* Scanner handles books without breaking music scanning.
* MCP integration works (already in place from T-MCP02).
* No external API calls yet (provider is still stub).

---

## T-BK03 – Book Matching & Quality Scoring in VirtualSoulfind

**Priority**: P2 (Improves quality)  
**Status**: 📋 Planned  
**Depends on**: T-BK02, T-VC04 (Domain-aware planner)  
**Blocks**: T-BK04

### Repo: `https://github.com/snapetech/slskdn`
### Branch: `experimental/multi-source-swarm`

**Goal**: Implement book-specific matching logic and quality scoring.

---

### 0. Scope & Non-Goals

You **must**:
1. Implement book matching logic (ISBN, title/author, language, page count).
2. Define `BookCopyQuality` for scoring.
3. Integrate into planner for `ContentDomain.Book`.

You **must NOT**:
* Change music matching logic.
* Add external metadata APIs (use provider interface only).

---

### 1. Book Matching Logic

Implement matching confidence scoring:

```csharp
public class BookMatchingService
{
    public double CalculateMatchConfidence(
        BookItem expected,
        BookFileMetadata actual)
    {
        double confidence = 0.0;
        
        // ISBN match (highest confidence)
        if (!string.IsNullOrEmpty(expected.Isbn13) && 
            !string.IsNullOrEmpty(actual.Isbn))
        {
            if (NormalizeIsbn(expected.Isbn13) == NormalizeIsbn(actual.Isbn))
            {
                confidence += 0.8; // Strong match
            }
        }
        
        // Title/author fuzzy match
        if (FuzzyMatch(expected.Title, actual.Title) > 0.8)
        {
            confidence += 0.1;
        }
        
        // Language match
        if (expected.Language == actual.Language)
        {
            confidence += 0.05;
        }
        
        // Page count within tolerance
        if (expected.PageCount.HasValue && actual.PageCount.HasValue)
        {
            var pageDiff = Math.Abs(expected.PageCount.Value - actual.PageCount.Value);
            if (pageDiff < 10)
            {
                confidence += 0.05;
            }
        }
        
        return Math.Min(confidence, 1.0);
    }
}
```

---

### 2. Book Copy Quality

Define quality scoring:

```csharp
public sealed class BookCopyQuality
{
    public double Score { get; init; } // 0.0 to 1.0
    public string Format { get; init; } // "EPUB", "PDF", etc.
    public bool IsComplete { get; init; } // Full book vs partial
    public bool CorrectLanguage { get; init; }
    public int? PageCount { get; init; }
    
    public static BookCopyQuality Calculate(
        BookItem expected,
        BookFileMetadata actual)
    {
        double score = 0.5; // Base score
        
        // Format preference: EPUB > PDF > MOBI
        score += actual.Format.ToLowerInvariant() switch
        {
            "epub" => 0.3,
            "pdf" => 0.2,
            "mobi" or "azw3" => 0.1,
            _ => 0.0
        };
        
        // Page count match
        if (expected.PageCount.HasValue && actual.PageCount.HasValue)
        {
            var ratio = (double)actual.PageCount.Value / expected.PageCount.Value;
            if (ratio >= 0.95 && ratio <= 1.05)
            {
                score += 0.2; // Complete
            }
        }
        
        return new BookCopyQuality
        {
            Score = Math.Min(score, 1.0),
            Format = actual.Format,
            IsComplete = /* determine based on page count */,
            CorrectLanguage = expected.Language == actual.Language,
            PageCount = actual.PageCount
        };
    }
}
```

---

### 3. Planner Integration

Update planner to use book matching for `ContentDomain.Book`:

```csharp
if (intent.Domain == ContentDomain.Book)
{
    // Get candidates
    var candidates = await GetBookCandidatesAsync(intent.ItemId, ct);
    
    // Score each candidate
    foreach (var candidate in candidates)
    {
        var quality = BookCopyQuality.Calculate(
            intent.ExpectedItem, 
            candidate.Metadata);
        
        candidate.QualityScore = quality.Score;
    }
    
    // Rank by quality
    var rankedCandidates = candidates
        .Where(c => c.QualityScore > MinimumBookQualityThreshold)
        .OrderByDescending(c => c.QualityScore)
        .ToList();
    
    // Build plan steps...
}
```

---

### 4. Tests (T-BK03)

Add tests:
1. ISBN matching gives high confidence.
2. Title/author fuzzy matching works.
3. Quality scoring prefers EPUB over PDF over MOBI.
4. Planner selects highest-quality book copies for `Book` domain.

---

### 7. Anti-Slop Checklist for T-BK03

* Book matching is domain-isolated (no bleed into music).
* Quality scoring is based on objective criteria.
* Planner tests validate `Book` domain behavior.

---

## T-BK04 – Book-Specific Reputation & MCP Glue

**Priority**: P3 (Enhances trust)  
**Status**: 📋 Planned  
**Depends on**: T-BK03, T-MCP04 (Peer reputation)  
**Blocks**: None

### Repo: `https://github.com/snapetech/slskdn`
### Branch: `experimental/multi-source-swarm`

**Goal**: Extend peer reputation system for book-specific events.

---

### 0. Scope & Non-Goals

You **must**:
1. Add book-specific event codes to `IPeerReputationStore`.
2. Log positive/negative events based on book download validation.
3. Ensure planner avoids low-reputation peers for books.

You **must NOT**:
* Change existing reputation logic for music.
* Store external identifiers (usernames, IPs) in reputation store.

---

### 1. Book Reputation Event Codes

Extend `PeerReport` with book-specific codes:

```csharp
public static class BookReputationEventCodes
{
    public const string ServedHighQualityBook = "served_high_quality_book";
    public const string ServedBadBookCopy = "served_bad_book_copy";
    public const string AssociatedWithBlockedBook = "associated_with_blocked_book";
}
```

Configure event weights:

```json
"Moderation": {
  "Reputation": {
    "EventWeights": {
      "served_high_quality_book": +2,
      "served_bad_book_copy": -3,
      "associated_with_blocked_book": -10
    }
  }
}
```

---

### 2. Log Reputation Events on Book Downloads

After a book download completes:

```csharp
// Validate the downloaded book
var validation = await ValidateBookDownloadAsync(bookItem, localFile, ct);

if (validation.IsValid && validation.Quality > 0.8)
{
    // Positive event: High-quality book
    await _moderationProvider.ReportPeerAsync(
        sourcePeerId,
        new PeerReport
        {
            ReasonCode = BookReputationEventCodes.ServedHighQualityBook,
            Notes = $"Format: {validation.Format}, Quality: {validation.Quality}"
        },
        ct);
}
else if (!validation.IsValid || validation.Quality < 0.3)
{
    // Negative event: Bad copy
    await _moderationProvider.ReportPeerAsync(
        sourcePeerId,
        new PeerReport
        {
            ReasonCode = BookReputationEventCodes.ServedBadBookCopy,
            Notes = $"Reason: {validation.FailureReason}"
        },
        ct);
}
```

---

### 3. Planner Integration

Update planner to skip banned peers for books:

```csharp
// Before adding peer to candidate list
if (intent.Domain == ContentDomain.Book)
{
    if (await _peerReputation.IsPeerBannedAsync(peerId, ct))
    {
        _logger.LogDebug("Skipping banned peer for book: {PeerId}", 
            HashPeerId(peerId));
        continue;
    }
}
```

---

### 4. Tests (T-BK04)

Add tests:
1. High-quality book downloads increase peer reputation.
2. Bad book copies decrease peer reputation.
3. Planner avoids banned peers for `Book` domain.
4. Reputation events do not leak external identifiers.

---

### 7. Anti-Slop Checklist for T-BK04

* Reputation system works for books without affecting music.
* No external identifiers stored in reputation database.
* Banned peers are consistently skipped in planner.

---

## Integration with Existing Systems

### ContentDomain (T-VC01) ✅
- Book added as third domain value
- BookWork/BookItem implement IContentWork/IContentItem

### Moderation (T-MCP02) ✅
- Scanner calls MCP before marking books as shareable
- Blocked books never become advertizable

### Peer Reputation (T-MCP04) ✅
- Book-specific event codes
- Reputation affects planner decisions

### Work Budget (H-02) ✅
- Metadata extraction consumes work budget
- External book API calls (if any) consume budget

---

## Why Books Matter

### Complementary to Music
- Uses same infrastructure (multi-domain foundation)
- Proves domain abstraction works
- Extends user value without duplicating code

### Moderation Critical
- Books face similar content policy issues as music
- MCP integration protects operators legally/ethically

### Quality Matters
- Incomplete PDFs are common
- Format matters (EPUB > PDF for reading)
- Reputation helps surface reliable sources

---

## Future Enhancements (Post-v1)

- **Open Library API**: Fetch book metadata by ISBN
- **Goodreads Integration**: Ratings and reviews
- **Calibre Integration**: Local library management
- **Audio Books**: Extend to `.m4b`, `.mp3` audiobook collections
- **Comics/Manga**: Support `.cbz`, `.cbr` formats

---

**Status**: Task breakdown complete, ready for implementation  
**Next**: Wait for T-VC01-04 and T-MCP01-03 to complete before starting T-BK01
