# VirtualSoulfind Content Domain Refactoring (T-VC series)

**Branch**: `experimental/multi-source-swarm`  
**Created**: December 11, 2025  
**Status**: Planning Phase  
**Priority**: Should be completed BEFORE VirtualSoulfind v2 Phase 1 (V2-P1)

> **Project Note**: This is a fork of [slskd](https://github.com/slskd/slskd). See [README.md](README.md#acknowledgments) for attribution.

---

## Overview

These tasks refactor VirtualSoulfind's core to support **multiple content domains** (Music, GenericFile, Movies, TV, Books, etc.) while maintaining Music as the primary, Soulseek-backed domain.

**Key Goal**: Generalize VirtualSoulfind architecture WITHOUT changing existing music behavior, and **gate Soulseek to Music domain only**.

**Why Before V2-P1**: This architectural change affects the data model, intent queue, source registry, and planner design. Doing it early avoids re-architecting later and makes V2 implementation cleaner.

---

## Task Sequence

These tasks should be inserted into the implementation roadmap **between T-SF07 and V2-P1**:

```
Current: T-SF05 → T-SF06 → T-SF07 → H-08 → H-02
INSERT HERE: → T-VC01 → T-VC02 → T-VC03 → T-VC04 →
Then: V2-P1 (Foundation) → V2-P2 → ...
```

**Rationale**: 
- Complete service fabric first (T-SF05-07)
- Complete critical gates (H-08, H-02)
- THEN refactor for multi-domain support (T-VC01-04)
- THEN implement V2 on top of clean domain architecture

---

## T-VC01 – Introduce Content Domain Abstraction in VirtualSoulfind Core

**Priority**: P0 (Architectural foundation)  
**Status**: 📋 Planned  
**Depends on**: T-SF07 (Metrics/observability)  
**Blocks**: V2-P1 (Foundation)

### Repo: `https://github.com/snapetech/slskdn`
### Branch: `experimental/multi-source-swarm`

You are working on the VirtualSoulfind v2 design. In this task, you'll generalize the **core** so it can support multiple content domains (Music, GenericFile, etc.), without breaking existing behavior.

---

### 0. Scope & Non-Goals

You **must**:

1. Introduce a `ContentDomain` enum and core domain-neutral types.
2. Refactor VirtualSoulfind's *core* to use domain-neutral types where appropriate.
3. Keep Music as the default and only fully supported domain for now (no behavior change yet).

You **must NOT**:

* Remove or significantly break existing music behavior.
* Implement non-music domains beyond minimal stubs (that's for later tasks).
* Change any network protocol or service fabric API signatures unless strictly necessary for domain support (and then do so in a backwards-compatible way).

---

### 1. Recon

Before coding, locate and understand:

1. VirtualSoulfind core types:
   * Classes representing artists/releases/tracks/MBIDs.
   * The "catalogue store" implementation.
   * Intent queue entities (`DesiredRelease` / `DesiredTrack` or equivalent).
   * Source registry types (`SourceCandidate` or similar).

2. Planner:
   * How it identifies "what thing" it is planning for (track vs release).

3. Any existing "domain assumptions":
   * Places where code assumes "this is always a music track".

Make notes on where music-specific assumptions live.

---

### 2. Introduce ContentDomain & Core IDs

Add a domain enum:

```csharp
public enum ContentDomain
{
    Music = 0,
    GenericFile = 1,
    // Future: Movie, Tv, Book, etc.
}
```

Introduce generic ID wrappers (names may be adjusted to match repo):

```csharp
public readonly record struct ContentItemId(Guid Value);
public readonly record struct ContentWorkId(Guid Value);
```

In the music domain:
* `ContentWorkId` will correspond to a Release/Album-like entity.
* `ContentItemId` will correspond to Track-like entities.

At this stage you're just adding these types and wiring them into VirtualSoulfind's *core* model; you will keep the existing music IDs in parallel.

---

### 3. Domain-Neutral Core Entities

Introduce domain-neutral interfaces / base types:

```csharp
public interface IContentWork
{
    ContentWorkId Id { get; }
    ContentDomain Domain { get; }
    string Title { get; }
    int? Year { get; }
}

public interface IContentItem
{
    ContentItemId Id { get; }
    ContentDomain Domain { get; }
    ContentWorkId WorkId { get; }
    string Title { get; }
    TimeSpan? Duration { get; } // optional; may be null for GenericFile
}
```

Then:
* Implement music-backed versions (these can be thin adapters around existing music-specific entities).
* Keep the existing music entities as-is, but add adapter methods to expose them as `IContentWork` / `IContentItem`.

Do not delete the music-specific types – just layer these interfaces on top.

---

### 4. Domain-Neutral Intent Entities

Refactor `DesiredRelease` / `DesiredTrack` (or equivalents):

* Add fields:

```csharp
public ContentDomain Domain { get; init; }
public ContentWorkId WorkId { get; init; } // for releases
public ContentItemId ItemId { get; init; } // for tracks
```

* Keep existing music-specific fields (e.g. `ReleaseId`, `TrackId`, MBIDs) to avoid breaking code.
* When creating new intents for music:
  * Populate both:
    * Domain = `ContentDomain.Music`
    * WorkId/ItemId mapping to music-specific IDs.

Ensure existing code still compiles and that music-specific fields are still used where needed.

---

### 5. Domain-Neutral SourceCandidate

Update `SourceCandidate` or equivalent to include domain:

```csharp
public sealed class SourceCandidate
{
    public ContentDomain Domain { get; init; }
    public ContentItemId ItemId { get; init; }
    // existing fields like Backend, BackendRef, ExpectedQuality, TrustScore...
}
```

* For now, all existing uses use `ContentDomain.Music`.
* Populate `ItemId` for music using the adapter mapping.

---

### 6. Wiring Core VirtualSoulfind APIs to Domains

Where VirtualSoulfind exposes core operations (internal methods, services, etc.):

* Ensure calls either:
  * Accept a `ContentDomain` + `ContentWorkId` / `ContentItemId`, or
  * Assume `ContentDomain.Music` if domain is not yet passed (for backward compatibility).

In this task you're mostly:
* Adding domain-aware parameters where they are obviously needed, but
* Defaulting them to `Music` so behavior doesn't change.

---

### 7. Tests (T-VC01)

Add or update tests to ensure:

1. Existing music flows:
   * Still function as before (get tracks/releases, intents, etc.).

2. Domain fields:
   * Newly created music intents have `Domain = Music`.
   * `SourceCandidate` created for music has `Domain = Music`.

3. No behavior change:
   * Test coverage for typical VirtualSoulfind music flows should still pass without semantic changes.

Use adapters in tests where needed to avoid duplicating logic.

---

### 8. Anti-Slop Checklist for T-VC01

Before finishing:

* All core VirtualSoulfind entities know their `ContentDomain`.
* Music remains the only fully supported domain; no behavior change yet.
* No magic strings for domain; use the enum.
* New domain types (interfaces/IDs) are **thin** and do not introduce heavy dependencies or coupling.

---

## T-VC02 – Implement Music Domain Adapter on Top of the New Core

**Priority**: P0 (Architectural cleanup)  
**Status**: 📋 Planned  
**Depends on**: T-VC01  
**Blocks**: V2-P1 (Foundation)

### Repo: `https://github.com/snapetech/slskdn`
### Branch: `experimental/multi-source-swarm`

You just introduced a domain-neutral core (T-VC01). Now you will move **music-specific logic** into a dedicated `Music` domain adapter, without breaking functionality.

---

### 0. Scope & Non-Goals

You **must**:

1. Implement a `MusicContentDomainProvider` that:
   * Maps existing music entities (Artist/Release/Track/MBID) to `IContentWork` and `IContentItem`.
   * Provides music-specific matching rules (duration-based, MBID-based, etc.) via a domain-specific interface.

2. Refactor VirtualSoulfind core to use this provider instead of hard-coded music assumptions.

You **must NOT**:

* Remove or significantly change existing matching/planning semantics for music.
* Implement other domains here (GenericFile, Movies, etc. come later).

---

### 1. Recon

Find:

1. The music-specific types:
   * Artist/ReleaseGroup/Release/Track/MBID classes.

2. Where VirtualSoulfind:
   * Looks up releases/tracks by MBID.
   * Computes canonical durations.
   * Applies music-specific matching (e.g., duration tolerances).

3. Any code in planner/resolver that assumes "track" always means music track.

---

### 2. Define a Music Domain Provider Interface

Create an interface like:

```csharp
public interface IMusicContentDomainProvider
{
    IContentWork? TryGetWorkByReleaseId(/* existing music ReleaseId type */ releaseId);
    IContentItem? TryGetItemByTrackId(/* existing music TrackId type */ trackId);

    IContentWork? TryGetWorkByMbid(string releaseMbid);
    IContentItem? TryGetItemByMbid(string recordingMbid);

    bool IsDurationMatch(IContentItem item, TimeSpan actualDuration, TimeSpan tolerance);
}
```

This is music-specific and knows about MBIDs and the existing music DB schema.

---

### 3. Implement MusicContentDomainProvider

Implement `MusicContentDomainProvider`:

* Use existing music catalogue store / DB to:
  * Resolve release/track IDs and MBIDs to the underlying music types.
  * Adapt them into `IContentWork` / `IContentItem` via simple wrapper classes or mapping functions.

* Implement `IsDurationMatch` using:
  * The canonical track duration.
  * A tolerance (configurable, but keep existing semantics).

---

### 4. Refactor VirtualSoulfind Core to Use the Provider

Where VirtualSoulfind currently:
* Resolves releases/tracks by MBID.
* Uses music-only types to populate intents and source candidates.
* Applies duration-based matching.

Refactor to:
* Use `IMusicContentDomainProvider` when `Domain == ContentDomain.Music`.
* Keep existing logic behaviorally identical:
  * This is essentially dependency injection:
    * "Ask the provider for the canonical work/item and matching rules" instead of reaching into music-specific DB directly.

Where VirtualSoulfind examines `TrackId` / `ReleaseId`:
* Call the provider to get `IContentItem`/`IContentWork` when domain is `Music`.

---

### 5. Config & Wiring

Make sure `MusicContentDomainProvider` is registered in DI:
* As `IMusicContentDomainProvider`.
* Used by VirtualSoulfind initialization code when `ContentDomain.Music` is relevant.

No new config needed beyond tolerances; re-use existing config if available.

---

### 6. Tests (T-VC02)

Add/update tests:

1. MBID resolution:
   * Given a known MBID for a release/track:
     * `MusicContentDomainProvider` returns appropriate `IContentWork` / `IContentItem`.

2. Duration matching:
   * Check `IsDurationMatch` behavior matches previous logic (same tolerance semantics).

3. VirtualSoulfind flows:
   * At least one end-to-end music flow using the new provider:
     * Resolve release by MBID.
     * Create intents.
     * Match local files by duration.

Ensure semantics unchanged.

---

### 7. Anti-Slop Checklist for T-VC02

* All core music-specific logic is behind `IMusicContentDomainProvider`.
* VirtualSoulfind core code no longer directly depends on music DB types in most places.
* Music remains fully functional; tests validate MBID resolution and matching.

---

## T-VC03 – Implement a GenericFile Domain Adapter (Pilot Non-Music Domain)

**Priority**: P1 (Proof of concept)  
**Status**: 📋 Planned  
**Depends on**: T-VC02  
**Blocks**: None (but validates domain architecture)

### Repo: `https://github.com/snapetech/slskdn`
### Branch: `experimental/multi-source-swarm`

Now you'll add a **minimal, non-music** domain: `GenericFile`.

This acts as a pilot for supporting arbitrary file types via VirtualSoulfind's pattern.

---

### 0. Scope & Non-Goals

You **must**:

1. Add `ContentDomain.GenericFile` support in VirtualSoulfind.
2. Implement a simple `GenericFileDomainProvider` and a minimal catalogue for generic files.
3. Allow creation of `DesiredItem`/intents for generic files (e.g., by hash + filename).
4. Do **not** integrate Soulseek as a backend for GenericFile in this task.

You **must NOT**:

* Implement complex external catalogue integrations (TMDB, ISBN, etc.).
* Change existing music behaviors.

---

### 1. Data Model for GenericFile

Define minimal data structures:

```csharp
public sealed class GenericFileWork : IContentWork
{
    public ContentWorkId Id { get; init; }
    public ContentDomain Domain => ContentDomain.GenericFile;
    public string Title { get; init; } // could be a user-defined label or folder-level name
    public int? Year => null; // usually not applicable
}

public sealed class GenericFileItem : IContentItem
{
    public ContentItemId Id { get; init; }
    public ContentDomain Domain => ContentDomain.GenericFile;
    public ContentWorkId WorkId { get; init; }
    public string Title { get; init; } // filename or logical name
    public TimeSpan? Duration => null; // not used; for GenericFile we rely on size/hash
    public long SizeBytes { get; init; }
    public string? PrimaryHash { get; init; } // e.g. SHA256
}
```

Back these with a simple local catalogue (e.g., a table keyed by hash or user-defined IDs).

---

### 2. GenericFile Domain Provider Interface & Implementation

Define:

```csharp
public interface IGenericFileContentDomainProvider
{
    IContentWork CreateWork(string label);
    IContentItem CreateItem(ContentWorkId workId, string title, long sizeBytes, string? hash);
    IContentItem? FindItemByHash(string hash);
}
```

Implement `GenericFileContentDomainProvider`:

* Use a simple DB table for `GenericFileItem` keyed by hash and/or ID.
* Optionally, treat each item as its own "work" if you don't need grouping at first.

---

### 3. Intent Creation for GenericFile

Add a way to create intents for GenericFile content:

* API/utility method (internal or via CLI/HTTP later) like:

```csharp
DesiredTrack CreateGenericFileIntent(string label, string filename, long sizeBytes, string? hash);
```

Behavior:

* Resolve/create a `GenericFileWork` for the label.
* Create a `GenericFileItem` for the file (or reuse existing by hash if found).
* Create a `DesiredTrack` (or `DesiredItem`) with:
  * `Domain = ContentDomain.GenericFile`
  * `WorkId = GenericFileWork.Id`
  * `ItemId = GenericFileItem.Id`

No Soulseek calls. The planner/backends integration will be wired later.

---

### 4. Planner & Matching Behavior (Minimal)

For GenericFile domain:

* Matching logic should rely on:
  * Primary hash (if available).
  * File size (fallback if no hash).

Stub a matching method:

```csharp
public bool IsGenericFileMatch(GenericFileItem item, long candidateSizeBytes, string? candidateHash);
```

Use:

* `candidateHash == item.PrimaryHash` when both present → strong match.
* Else if sizes match exactly → weak match (only if user allows "size-only matches" later).

Do not integrate into network fetch flows yet; focus on building the catalogue + intents + basic match functions.

---

### 5. Tests (T-VC03)

Add tests:

1. Catalogue:
   * Creating `GenericFileWork` and `GenericFileItem` results in persisted entities with correct `Domain`.

2. Intent creation:
   * Creating GenericFile intents sets:
     * `Domain = GenericFile`
     * `WorkId` and `ItemId` referencing GenericFile entities.

3. Matching:
   * Hash match → true.
   * No hash, size equal → true (if allowed).
   * Mismatched size/hash → false.

---

### 6. Anti-Slop Checklist for T-VC03

* GenericFile domain is present and working at a basic level.
* No Soulseek code paths are tied to `ContentDomain.GenericFile`.
* Music behavior is untouched.

---

## T-VC04 – Make Planner & Backends Domain-Aware, Gate Soulseek to Music Only

**Priority**: P0 (Critical security/etiquette boundary)  
**Status**: 📋 Planned  
**Depends on**: T-VC03, H-08 (Soulseek caps)  
**Blocks**: V2-P4 (Backend Implementations)

### Repo: `https://github.com/snapetech/slskdn`
### Branch: `experimental/multi-source-swarm`

Now you will wire `ContentDomain` through the planner and backends, and enforce that **Soulseek is only used for the Music domain**.

---

### 0. Scope & Non-Goals

You **must**:

1. Update the planner to consider `ContentDomain` when building plans.
2. Ensure Soulseek backend is only used when `Domain == Music`.
3. Allow other domains (e.g., GenericFile) to use only non-Soulseek backends (MeshDHT, Torrent, HTTP, Local).
4. Maintain backward compatibility for music flows.

You **must NOT**:

* Change the global Soulseek caps/limits semantics.
* Implement new backends; just route to existing ones appropriately.

---

### 1. Recon

Find:

1. Planner logic:
   * Where `TrackAcquisitionPlan` / `PlanStep` are built.

2. Backend selection:
   * How VirtualSoulfind decides which backends (Soulseek, DHT, Torrent, etc.) to include.

3. Backends:
   * `IContentBackend` implementations and how they identify themselves (type/enum).

---

### 2. Pass Domain into Planner

Ensure the planner entry point receives domain:

* For each intent (`DesiredTrack` / `DesiredItem`):
  * The planner should have access to:
    * `intent.Domain`
    * `intent.ItemId` / `intent.WorkId`

Ensure the planner uses domain-aware resolution:

* For `ContentDomain.Music`:
  * Use `IMusicContentDomainProvider`.
* For `ContentDomain.GenericFile`:
  * Use `IGenericFileContentDomainProvider`.

---

### 3. Domain-Aware Backend Selection

Implement domain-based backend selection rules:

* For `ContentDomain.Music`:
  * Allowed backends (subject to config & mode):
    * Soulseek
    * MeshDHT
    * Torrent
    * HTTP
    * Local

* For `ContentDomain.GenericFile` (for now):
  * Allowed backends:
    * MeshDHT
    * Torrent
    * HTTP
    * Local
  * Soulseek backend MUST NOT be used.

You can implement this as:

* A central mapping:

```csharp
IReadOnlyDictionary<ContentDomain, ContentBackendType[]> AllowedBackendsPerDomain;
```

or simple `switch` statements.

---

### 4. Enforce Soulseek-for-Music-Only

In whichever component selects or invokes the Soulseek backend:

* Add an explicit check:

```csharp
if (intent.Domain != ContentDomain.Music)
{
    // do not add Soulseek steps
}
```

or:

* In the Soulseek backend itself:
  * Assert or early-return if `candidate.Domain != ContentDomain.Music`.

Do this in a way that:

* Keeps music flows unchanged.
* Guarantees no Soulseek calls are done for other domains.

---

### 5. Planner Modes (Optional Minor Wiring)

If you already have `PlanningMode` (OfflinePlanning, MeshOnly, SoulseekFriendly):

* Ensure domain gating is applied **in addition** to mode:
  * `MeshOnly`:
    * Ignores Soulseek even for Music.
  * `OfflinePlanning`:
    * Ignores all network backends.
  * `SoulseekFriendly`:
    * For Music:
      * Soulseek allowed but capped.
    * For GenericFile:
      * Soulseek still disabled due to domain gating.

---

### 6. Tests (T-VC04)

Add tests:

1. Planner + Music:
   * For a music intent:
     * Planner includes Soulseek in the plan (if mode allows).

2. Planner + GenericFile:
   * For a GenericFile intent:
     * Planner never includes Soulseek steps.

3. Backends:
   * If possible, assert Soulseek backend throws or no-ops when asked to handle `ContentDomain.GenericFile`.

4. Modes:
   * In `MeshOnly` mode:
     * No plan (even for Music) includes Soulseek steps.

---

### 7. Anti-Slop Checklist for T-VC04

* Planner/backends are domain-aware.
* Soulseek is gated to `Music` only.
* Music flows still behave as before; GenericFile flows are mesh/torrent/http/local only.

---

## Integration with Overall Roadmap

### Updated Implementation Sequence

**Current roadmap**:
1. T-SF05: Security review
2. T-SF06: Developer docs
3. T-SF07: Metrics/observability
4. H-08: Soulseek caps (GATE 2)
5. H-02: Work budget (GATE 3)

**INSERT HERE - Content Domain Refactoring**:
6. **T-VC01**: Introduce domain abstraction
7. **T-VC02**: Music domain adapter
8. **T-VC03**: GenericFile domain (proof of concept)
9. **T-VC04**: Domain-aware planner + Soulseek gating

**Then continue with**:
10. V2-P1: Foundation (now builds on clean domain architecture)
11. V2-P2 through V2-P6: Rest of VirtualSoulfind v2

### Why This Order Makes Sense

1. **Complete Service Fabric First** (T-SF05-07)
   - Establishes security baseline
   - Documents patterns
   - Adds observability

2. **Clear Critical Gates** (H-08, H-02)
   - Soulseek caps needed for T-VC04
   - Work budget needed for all of V2

3. **Refactor for Domains** (T-VC01-04)
   - Clean architectural foundation
   - Proves multi-domain concept
   - Gates Soulseek to Music explicitly
   - No V2 rework needed later

4. **Implement V2 on Clean Base** (V2-P1 through V2-P6)
   - Data model already domain-aware
   - Backend selection already gated
   - Just implement the rest of the features

### Task Dependencies Diagram

```
T-SF07 → T-VC01 → T-VC02 → T-VC03 ┐
                                   ├→ T-VC04 → V2-P4
H-08 ──────────────────────────────┘
H-02 ───────────────────────────────────────→ V2-P5
```

---

## Benefits of This Approach

1. **Cleaner V2 Implementation**: Data model is already multi-domain from the start
2. **Soulseek Safety Baked In**: T-VC04 enforces Music-only Soulseek at architectural level
3. **Proof of Concept**: GenericFile proves the architecture works for non-music
4. **No Breaking Changes**: All refactoring maintains music behavior
5. **Future-Proof**: Easy to add Movie/TV/Book domains later

---

## Summary

- **When**: After T-SF07, H-08, H-02, before V2-P1
- **What**: 4 refactoring tasks (T-VC01-04)
- **Why**: Clean multi-domain architecture, Soulseek gating, future-proof
- **Impact**: No behavior change for music, foundation for multi-domain future
- **Soulseek Safety**: T-VC04 enforces Music-only at code level, not just policy

