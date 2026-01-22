# UI & Library Dashboards (Multi-Domain)

**Status**: DRAFT - Design for Phase G (UI/API Layer)  
**Created**: December 11, 2025  
**Priority**: MEDIUM (after core VirtualSoulfind implementation)

> **Project Note**: This is a fork of [slskd](https://github.com/slskd/slskd). See [../README.md](../README.md#acknowledgments) for attribution.

---

This document describes the high-level UI and API surfaces that sit on top of VirtualSoulfind for browsing and managing the library across all domains (Music, Book, Movie, Tv, GenericFile).

The goal is to provide:
- Consistent views and interactions across domains.
- Domain-specific detail where it matters.
- A clean separation between backend logic (VirtualSoulfind, MCP, backends) and UI/API consumers.

---

## Principles

- **Read-optimized, write-minimal:** The dashboards primarily display data and accept small, well-scoped mutations (e.g., pinning works, toggling quality preferences, editing lists).
- **Domain-agnostic core, domain-specific panels:** Shared patterns (search, "have vs missing", quality indicators) with domain-appropriate fields.
- **Privacy-aware:** Dashboards must not accidentally expose:
  - Local filesystem paths.
  - Peer IDs/IPs.
  - External identities (Mesh, Soulseek, ActivityPub) unless explicitly chosen by the user.
- **MCP-aware:** Any content flagged as blocked/quarantined by Moderation must not appear in normal views, except in dedicated admin/quarantine views.

---

## Common Library API Concepts

The library UI/API is expected to use a small set of common operations:

### Search
- `searchWorks(domain, query, filters)` → list of work summaries.

### Work details
- `getWorkDetails(workId)` → work metadata + items + availability + quality.

### Library reconciliation
- `getDomainOverview(domain)` → aggregate view (counts & states).
- `getWorkCompletion(workId)` → have/missing breakdown.

### Lists/Collections
- `listCollections(domain, filterByOwner)` → reading lists, playlists, watchlists.
- `getCollectionDetails(collectionId)` → items + metadata.

### Actions
- Pin/unpin a work.
- Set quality preferences.
- Add/remove items from lists.
- Mark items as "ignore" (do not try to fill/upgrade).

---

Implementation detail (REST, gRPC, etc.) is left flexible. The key is that these operations talk in terms of:

- `ContentDomain`, `ContentWorkId`, `ContentItemId`.
- Domain-specific DTOs that do not expose paths/peers.

---

## Domain-Aware Dashboards

### Music Dashboard

Key views:

**Library overview:**
- Total artists/albums/tracks.
- Breakdown by quality (lossless vs lossy, etc.).
- Recently added/updated works.

**Artist view:**
- List of albums/releases.
- Completion for each album (tracks present/missing).
- Quality summary per album.

**Album/Work view:**
- Track list with:
  - Presence/absence.
  - Quality indicators.
  - Verified flags.
- Linked metadata (MusicBrainz, etc.).

**Playlists:**
- User-defined playlists.
- "From VirtualSoulfind" lists (intent-driven).
- Optionally, "From social" lists (if federation enabled).

---

### Video Dashboard (Movies & TV)

Key views:

**Movies:**
- Grid/list of movies with:
  - Poster art (if available).
  - Quality badge (per best copy).
  - Completion (for multi-part works, if relevant).
- Filters by:
  - Genre, year, quality profile, completion state.

**Movie detail:**
- Work metadata (title, year, runtime).
- One or more items/copies:
  - Resolution, codec, HDR, audio layout.
  - Verification and quality scores.
- Planner hints:
  - Suggested upgrades (if any).

**TV:**
- Show list:
  - "Have some", "complete seasons", "missing seasons", etc.
- Show detail:
  - Season list.
  - Per-season episode grid with have/missing/quality states.
- Planner hints:
  - Episodes/seasons to fill missing pieces.

---

### Book Dashboard

Key views:

**Authors & Series:**
- Author list with:
  - Number of works in library.
  - Completion indicators (per series).
- Series view:
  - Ordered list of works in the series.
  - Have/missing/quality states.

**Work detail:**
- Work metadata (title, authors, series placement, ISBNs).
- Items:
  - Format (EPUB/PDF/MOBI).
  - Language.
  - Quality indicators (BookCopyQuality).
- Planner hints:
  - Suggest upgrading low-quality copies.

**Reading lists:**
- Local reading lists.
- Imported lists (e.g., from SocialIngestionService if enabled).
- Progress indicators per work (if tracked locally).

---

### GenericFile Dashboard

Key views:

- Minimal, non-semantic file listing by:
  - Tags or groupings (if any).
  - File type/size.

This domain should be conservative in how it's presented:
- No assumptions about content type.
- MCP still applies if content is flagged.

---

## Reconciliation & Planner Integration

Each dashboard should be powered by reconciliation/planner data rather than raw filesystem or backend state:

**Reconciliation:**
- Drives "have vs missing" counts.
- Should be cached and updated incrementally.

**Planner hints:**
- Provide "suggested actions" to UI:
  - "You might want to obtain X…"
  - "You can upgrade Y…"
- UI MUST present these as suggestions, not automatic operations.

---

The API design should:

- Expose clear distinction between:
  - **Facts** (what we have, quality, verified).
  - **Suggestions** (planner-generated).
  - **Actions** (user-triggered, may become intents).

---

## Privacy & Moderation Implications

**Dashboards for regular users:**
- MUST NOT show quarantined/blocked content.
- Must not display raw error/mismatch details from MCP.

**Admin views (optional):**
- MAY provide a quarantined/blocked content view behind authorization.
- Should show:
  - Reason codes.
  - `ModerationDecision` summaries.
  - Safe, sanitized metadata only.

**Personalization:**
- Any per-user personalization (e.g., favorites, in-progress, last viewed) MUST be local to the pod.
- Only if federation/social is enabled and explicitly configured should any of this generate outbound social signals.

---

## Extensibility

The dashboards are intended to be extensible:

**New domains:**
- Should plug into the common patterns (overview, work detail, lists).
- Must respect security, privacy, and moderation rules.

**Plugins:**
- May add new views or augment existing ones (e.g., "mood tags" or "room activity" panels).
- Must not bypass VirtualSoulfind or MCP; they consume the same APIs/views as any other UI code.

---

## Implementation Tasks

See `docs/archive/status/TASK_STATUS_DASHBOARD.md`:

- **T-UI01**: Library Overview Endpoints (Multi-Domain)
- **T-UI02**: Music Library Views
- **T-UI03**: Video Library Views (Movies & TV)
- **T-UI04**: Book Library Views
- **T-UI05**: Collections & Lists API
- **T-UI06**: Admin / Moderation Views (Optional)

---

## Dependencies

- **VirtualSoulfind v2 (V2-P1-P6)**: Core catalogue, reconciliation, planner
- **T-VC01-04** ✅: Multi-domain abstraction
- **T-MCP01-04**: Moderation (for filtering blocked content from views)
- **T-FED05**: Social signals (optional, for federated lists/collections)

---

## Design Notes

### Why Not Raw Backend Access?

UI should NEVER directly query:
- Soulseek (privacy, rate limits).
- Mesh/DHT (raw peer data).
- Torrent swarms (raw peer IPs).
- Local filesystem (security, abstraction violation).

Instead:
- UI queries VirtualSoulfind (the "brain").
- VirtualSoulfind provides sanitized, reconciled views.
- Backend details are abstracted away.

### Why Minimal Writes?

- Most operations are reads (browsing, searching, viewing).
- Writes are:
  - Adding/removing items from lists.
  - Creating intents (which go through planner).
  - Updating preferences.
- This keeps the UI layer thin and stateless where possible.

### Why Domain-Specific Panels?

- Music has concepts like albums, artists, playlists.
- Video has movies, shows, seasons, episodes.
- Books have authors, series, reading order.

Forcing a one-size-fits-all view would lose important domain semantics.

---

## Security & Privacy

All dashboard endpoints MUST:
- ✅ Not expose local filesystem paths
- ✅ Not expose peer IDs, IPs, or external identities
- ✅ Respect MCP decisions (blocked content hidden)
- ✅ Use VirtualSoulfind as sole data source (not raw backends)
- ✅ Apply authentication/authorization where needed (admin views)
- ✅ Sanitize all outputs (no raw error messages with sensitive data)

