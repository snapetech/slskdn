# Book Domain Design

**Status**: DRAFT - Phase E (After MCP, Multi-Domain Core, Proxy/Relay)  
**Created**: December 11, 2025  
**Priority**: MEDIUM (alongside or after Video domain)

> **Project Note**: This is a fork of [slskd](https://github.com/slskd/slskd). See [../README.md](../README.md#acknowledgments) for attribution.

---

This document defines the Book domain model and how it integrates with VirtualSoulfind, metadata services, and backends.

## Goals

- Treat books/ebooks as first-class citizens in VirtualSoulfind.
- Provide robust identity, matching, verification, and quality scoring.
- Enforce domain-appropriate backend rules (no Soulseek).
- Maintain security, privacy, and moderation guarantees.

---

## Data Model

### BookWork

Implements `IContentWork`:

- `Id : ContentWorkId`
- `Domain : ContentDomain` = `Book`
- `Title : string`
- `Authors : string[]`
- `SeriesName : string?` (optional)
- `SeriesIndex : string?` (optional, numeric or string like "1.5")
- `PrimaryLanguage : string?`
- `PublicationYear : int?` (canonical or earliest)
- `ExternalIds : Dictionary<string, string>`:
  - ISBN-10 / ISBN-13.
  - Open Library IDs, Goodreads IDs, etc.

### BookItem

Implements `IContentItem`:

- `Id : ContentItemId`
- `Domain : ContentDomain` = `Book`
- `WorkId : ContentWorkId`
- `EditionName : string?` (publisher + year or free-form)
- `Format : string` (e.g., "EPUB", "PDF", "MOBI", "AZW3", "CBZ")
- `Language : string?`
- `PageCount : int?` (approximate)
- `FileSize : long?`
- `DrmStatus : string?` (e.g., "unknown", "likely-drm-free", "likely-drm-encumbered"; where detectable)
- `ExternalIds : Dictionary<string, string>?` (edition-specific IDs where applicable)

---

## Metadata Extraction

**`IBookMetadataExtractor`** is responsible for:

### Parsing file-level metadata from common formats

**EPUB**:

- `.opf` metadata (title, authors, series, ISBNs).
- Language.
- Page count approximation (if available, or estimated from word count).

**PDF**:

- Document metadata (title, author, etc.).
- Approximate page count.

**MOBI and others**:

- Best-effort extraction where feasible.

### Implementation Notes

Extraction MUST:

- Run with timeouts and size limits.
- Avoid logging raw extracted metadata beyond what is needed (no dumping full OPF/XML).
- Sanitize extracted data (titles/authors can contain malicious strings).

The scanner uses this extractor to build `LocalFileMetadata` + book-specific fields.

---

## Metadata Services (APIs)

**`BookMetadataService`** runs on the service fabric and uses the catalogue fetch mechanism:

- Uses SSRF-safe HTTP client.
- Uses domain allowlists (Open Library, Goodreads API (if ToS-compliant), etc.).
- Applies work budgets and rate limits.

### Responsibilities

- Given an ISBN or external ID:
  - Return normalized `BookWork` + `BookItem` candidates.
- Given a title/author(+year):
  - Return probable matches with scoring (confidence).

All results are hints and must be validated by the domain provider when matching local files.

### API Methods (Examples)

- `Task<BookWork?> LookupBookByIsbnAsync(string isbn, CancellationToken ct)`
- `Task<IEnumerable<BookWork>> LookupBookByTitleAuthorAsync(string title, string author, int? year, CancellationToken ct)`

---

## Domain Provider

**`IBookContentDomainProvider`**:

### Primary Responsibilities

- `Task<BookWork?> TryGetWorkByIsbnAsync(string isbn, CancellationToken ct)`
- `Task<IEnumerable<BookWork>> TryGetWorkByTitleAuthorAsync(string title, string author, int? year, CancellationToken ct)`
- `Task<BookItem?> TryGetItemByExternalIdAsync(string externalId, CancellationToken ct)`
- `Task<BookItem?> TryGetItemByLocalMetadataAsync(LocalFileMetadata fileMetadata, BookMetadata bookMetadata, CancellationToken ct)`

### Implements

- Work and item construction from metadata service responses.
- Matching logic combining:
  - ISBNs (when available, strongest signal).
  - Title + author + language + page count (fuzzy matching with thresholds).
- Verification and quality scoring.

---

## Verification

When verifying a local book file against a `BookItem`:

**MUST check**:

- Format compatibility (e.g., BookItem says EPUB, but file is PDF → still valid but flagged).
- Language (if known).
- Reasonable page count/size vs metadata (within tolerance, e.g., ±10% page count).

**SHOULD check**:

- Hash-based verification against known-good records (if such a registry is available).

**MUST treat metadata service results as hints, not ground truth**.

### Verification Status

Feeds into:

- `IsVerified` flags in VirtualSoulfind.
- Eligibility for advertisement and relay (subject to MCP).

---

## Quality Scoring

**`BookCopyQuality`** fields:

- `FormatScore : int`
  - Reflowable formats (EPUB, etc.) = 100
  - Fixed layout (PDF) = 50
  - Scanned images / low-quality PDF = 25
  - Others = 10
- `DrmScore : int`
  - Non-DRM (where detectable) = 100
  - DRM-encumbered = 25
  - Unknown = 50
- `MetadataScore : int`
  - Presence/absence of:
    - Proper title/author metadata.
    - Series info.
    - Table of contents.
  - Good metadata = 100, Poor = 25
- `IntegrityScore : int`
  - Pass/fail structural validation checks.
  - Partial/incomplete files should score low (25).
  - Valid, complete files = 100.
- `OverallScore : int` (normalized aggregate, e.g., weighted average)

### Planner Usage

Planner uses `BookCopyQuality` to:

- Prioritize better-quality copies when multiple options are available.
- Suggest upgrades (e.g., "upgrade from scanned PDF to high-quality EPUB").

---

## Backend Rules

For Book domain (`ContentDomain.Book`):

**Allowed**:

- Mesh overlay.
- Torrent.
- HTTP.
- Local disk.

**Disallowed**:

- **Soulseek backend** (never used for books).

Planner MUST enforce these rules when constructing plans for `ContentDomain.Book` intents.

---

## Security, Privacy, Moderation

### Privacy

Book metadata and WorkRefs MUST NOT include:

- Local paths.
- Hashes (file hashes stay internal).
- Peer IDs/IPs.

### MCP Integration

MCP MUST gate:

- Advertising of book content on DHT/mesh.
- Relay and mesh dissemination.
- Social federation references to book WorkRefs (e.g., reading lists).

### Work Budgets & SSRF Protection

All external metadata and mesh interactions MUST:

- Respect work budgets (catalogue fetch consumes budget).
- Use SSRF-safe HTTP for catalogue calls (domain allowlists, HTTPS-only).
- Conform to logging/metrics hygiene (no PII, low-cardinality labels).

---

## Implementation Tasks

See `TASK_STATUS_DASHBOARD.md`:

- **T-BK01**: Book Domain Types & Provider Interface
- **T-BK02**: Book Metadata Extraction & Scanner Integration
- **T-BK03**: Book Metadata Service via Catalogue Fetch
- **T-BK04**: Verification, BookCopyQuality & Planner Integration

---

## Dependencies

- **T-VC01** ✅: ContentDomain abstraction (Book domain defined)
- **T-MCP01-02** ✅: MCP core + scanner integration (moderation applies to books)
- **T-PR02**: Catalogue Fetch service (for Open Library API calls)
- **H-TRANSPORT01**: Transport hardening (mesh/torrent/HTTP backends)

---

## References

- [Open Library API](https://openlibrary.org/developers/api)
- [Goodreads API](https://www.goodreads.com/api) (check current ToS before use)
- [EPUB Specification](https://www.w3.org/publishing/epub3/)
- VirtualSoulfind V2 Design: `docs/virtualsoulfind-v2-design.md`
- Multi-Domain Overview: `docs/virtualsoulfind-v2-design.md#multi-domain-model-overview`
