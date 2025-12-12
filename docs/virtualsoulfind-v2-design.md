# VirtualSoulfind v2 – Design Document

**Repo:** `github.com/snapetech/slskdn`  
**Branch:** `experimental/multi-source-swarm`  
**Status:** Draft design  
**Author:** AI Assistant + User  
**Created:** December 11, 2025  
**Scope:** Extend VirtualSoulfind into a full "virtual catalogue + multi-source planner" brain, integrated with the mesh/service fabric and DHT, without hammering Soulseek or making it second-class.

---

## 1. Background & Motivation

VirtualSoulfind today sits roughly as:

* A metadata-first layer that understands releases/tracks via external catalogues (MusicBrainz, etc.).
* A way to map those IDs onto available sources (Soulseek, local files, maybe mesh/BT).
* A starting point for smarter, ID-driven search and source selection.

With the new multi-source-swarm branch, service fabric, DHT, and mesh gateway, VirtualSoulfind can evolve into the central "brain" for:

* Virtual catalogue browsing and "offline planning".
* Multi-source planning across Soulseek, Mesh/DHT, BT, HTTP, LAN, and local library.
* Precise matching, verification, and library reconciliation.
* Strict control over how and when Soulseek is touched.

Key constraints:

* **No Soulseek abuse.** Turbo behavior is allowed only via mesh/DHT/BT and other non-Soulseek paths.
* **Soulseek remains first-class but not a punching bag.**
* **Backwards compatible.** The system must not break existing flows or turn slskdn into a weird client on the Soulseek network.

---

## 2. Goals & Non-Goals

### 2.1 Goals

1. **Virtual Catalogue Mode.**
   Provide a rich, MBID-centric catalogue of artists/releases/tracks that can be browsed and planned against even if Soulseek is offline.

2. **Intent Queue & Planner.**
   Represent what the user *wants* (virtual releases/tracks) as an explicit intent queue, separate from actual network fetches.

3. **Multi-Source, Metadata-Driven Planner.**
   Use VirtualSoulfind metadata to drive multi-source planning across:

   * Soulseek
   * Mesh/DHT
   * BitTorrent / multi-swarm
   * HTTP / LAN / local mirrors

   Prefer non-Soulseek sources where possible.

4. **Precise Matching & Verification.**
   Use MBID, expected durations, hashes, fingerprints, and canonical metadata to:

   * Reduce wrong downloads and retries.
   * Normalize library naming.
   * Maintain a "verified copy registry".

5. **Soulseek-safe behavior.**
   Enforce per-backend limits (especially Soulseek) *inside* VirtualSoulfind, so anything built on top cannot spam Soulseek accidentally.

6. **Advanced library workflows.**
   Support virtual crates/smart lists, gap-finding, and "what most improves this library" advisory, all largely offline.

7. **Integrate with the service fabric & gateway.**
   Expose VirtualSoulfind capabilities as:

   * A mesh service (`virtual-soulfind` / `mbid-index` style).
   * Local HTTP gateway endpoints for tooling.

### 2.2 Non-Goals

* Replacing the Soulseek protocol or changing its wire semantics.
* Solving all possible trust/poisoning problems in DHT; we will implement *basic* heuristics and leave advanced reputation to other components.
* Building a full-blown UI in this doc; UI is out-of-scope beyond API affordances and expectations.

---

## 3. High-Level Architecture

VirtualSoulfind v2 is a module composed of several cooperating components:

* **Virtual Catalogue Store**
  Local DB of artists, releases, tracks, MBIDs, and catalogue metadata.

* **Intent Queue**
  Persistent queue of "DesiredRelease" / "DesiredTrack" objects with priorities and modes (Wanted, Backfill, etc.).

* **Source Registry**
  Internal catalogue of potential sources for each track:

  * Soulseek (when allowed)
  * Mesh/DHT (service descriptors, content keys)
  * BT/multi-swarm
  * HTTP / LAN
  * Local library (already have it)

* **Multi-Source Planner**
  Given a VirtualRelease/VirtualTrack, produces a multi-backend plan:

  * Which backends to use.
  * In what order.
  * Under what constraints.

* **Match & Verification Engine**
  Encapsulates duration checks, hashes, audio fingerprints (when available), template naming, and verified copy registry.

* **Backend Adapters**
  Abstractions for each backend:

  * `ISoulseekBackend`
  * `IMeshDhtBackend`
  * `ITorrentBackend`
  * `ILocalLibraryBackend`
  * (Optional) `IHttpBackend`, `ILanBackend`

* **Policy & Limits**
  Per-backend and per-origin (User vs Mesh) rules for:

  * Rate limiting
  * Concurrency
  * Work budget (integrates with H-02 and H-08)

* **Service & Gateway Facades**
  VirtualSoulfind as:

  * `IVirtualSoulfindService` exposed over mesh service fabric.
  * HTTP gateway endpoints for local tooling (`/virtual/*`).

---

## 4. Data Model

This section defines main domain entities and their relationships. Implementation detail (DB schema) can be SQLite/EF/whatever the repo uses now.

### 4.1 Core Entities

#### `Artist`

* `ArtistId` (internal)
* `MusicBrainzId` (optional)
* `Name`
* `SortName`
* `Tags` (genres, etc.)

#### `ReleaseGroup`

* `ReleaseGroupId`
* `MusicBrainzId`
* `ArtistId`
* `Title`
* `PrimaryType` (Album, EP, Single, etc.)

#### `Release`

Represents a specific edition of a release (year, region, etc.).

* `ReleaseId`
* `MusicBrainzId`
* `ReleaseGroupId`
* `Title`
* `Year`
* `Country`
* `Label`
* `CatalogNumber`
* `MediaCount` (discs)

#### `Track`

Canonical metadata for an audio unit.

* `TrackId`
* `MusicBrainzRecordingId` (or similar)
* `ReleaseId`
* `DiscNumber`
* `TrackNumber`
* `Title`
* `DurationSeconds` (canonical)
* `CanonicalBitDepth` / `SampleRate` (optional)
* `Tags`

### 4.2 Local Library Entities

#### `LocalFile`

* `LocalFileId`
* `Path`
* `SizeBytes`
* `DurationSeconds` (measured)
* `Codec` / `Bitrate` / `Channels`
* `HashPrimary` (e.g. SHA256)
* `HashSecondary` (e.g. xxHash)
* `AudioFingerprintId` (if used)
* `InferredTrackId` (nullable; linking to Track)
* `QualityRating` (derived)

#### `VerifiedCopy`

Represents that we have a known-good copy of a track/release.

* `VerifiedCopyId`
* `TrackId`
* `LocalFileId`
* `HashPrimary`
* `DurationSeconds`
* `VerificationSource` (manual, multi-check, etc.)

### 4.3 Intent Entities

#### `DesiredRelease`

* `DesiredReleaseId`
* `ReleaseId`
* `Priority` (High/Normal/Low)
* `Mode` (Wanted, NiceToHave, Backfill)
* `Status` (Pending, Planned, InProgress, Completed, Failed, OnHold)
* `CreatedAt`, `UpdatedAt`
* `Notes`

#### `DesiredTrack`

* `DesiredTrackId`
* `TrackId`
* `ParentDesiredReleaseId` (nullable)
* `Priority`
* `Status` (as above)
* `PlannedSources` (summary, e.g. JSON list of preferred backends and constraints)

### 4.4 Source Registry Entities

#### `SourceCandidate`

A potential way to obtain a specific track.

* `SourceCandidateId`
* `TrackId`
* `Backend` (enum: Soulseek, MeshDht, Torrent, Http, Lan, Local)
* `BackendRef` (e.g. Soulseek user/file path, torrent infohash+file index, mesh service descriptor ID, HTTP URL, LocalFileId)
* `ExpectedQuality` (enum or score)
* `TrustScore` (float 0–1)
* `LastValidatedAt`, `LastSeenAt`
* `IsPreferred` (bool)

---

## 5. Backend Abstractions

We define backend interfaces so VirtualSoulfind never hardcodes how to talk to any network.

### 5.1 `IContentBackend`

At minimum:

```csharp
public enum ContentBackendType
{
    Soulseek,
    MeshDht,
    Torrent,
    Http,
    Lan,
    LocalLibrary
}

public interface IContentBackend
{
    ContentBackendType Type { get; }

    // Discover candidates for a given track.
    Task<IReadOnlyList<SourceCandidate>> FindCandidatesAsync(
        TrackId trackId,
        CancellationToken cancellationToken);

    // Optionally: evaluate/update trust or validate a candidate.
    Task<SourceCandidateValidationResult> ValidateCandidateAsync(
        SourceCandidate candidate,
        CancellationToken cancellationToken);
}
```

Concrete implementations:

* `SoulseekContentBackend`
* `MeshDhtContentBackend`
* `TorrentContentBackend`
* `HttpContentBackend`
* `LanContentBackend`
* `LocalLibraryBackend`

All of them must respect:

* Work budget (H-02).
* Soulseek-specific caps (H-08) where applicable.

---

## 6. Multi-Source Planner

The planner is a pure logic component that:

* Takes a `DesiredTrack` or `DesiredRelease`.

* Consults:

  * Virtual catalogue
  * Source registry
  * Backends
  * Policy/limits

* Produces a `Plan`:

  * Which candidates to try, in what order and concurrency.
  * Per-backend constraints.

### 6.1 Planner Input

* `DesiredTrack` (or full `DesiredRelease`).
* `PlanningMode` (Offline, SoulseekFriendly, MeshOnly).
* Current per-backend state:

  * Active work items.
  * Historical reliability for each backend / peer.

### 6.2 Planner Output (Sketch)

```csharp
public sealed class TrackAcquisitionPlan
{
    public TrackId TrackId { get; init; }
    public IReadOnlyList<PlanStep> Steps { get; init; }
}

public sealed class PlanStep
{
    public ContentBackendType Backend { get; init; }
    public IReadOnlyList<SourceCandidate> Candidates { get; init; }
    public int MaxParallel { get; init; }
    public TimeSpan Timeout { get; init; }
    public PlanStepFallbackMode FallbackMode { get; init; } // e.g. cascade or fan-out
}
```

The planner is policy-driven:

* In **MeshOnly** mode:

  * Only MeshDHT/Torrent/HTTP/LAN backends are allowed.
* In **OfflinePlanning**:

  * No network backends; only local library + catalogue.
* In **SoulseekFriendly**:

  * Soulseek can be used but with strict per-backend caps.

---

## 7. Match & Verification Engine

This component handles:

* Strict track matching (duration, MBID, track number).
* Hash/fingerprint checks.
* Verified copy registry updates.
* Canonical naming decisions.

### 7.1 Matching Criteria

For candidate `LocalFile` vs `Track`:

1. **MBID / metadata match** (if present in tags).
2. **Duration match**:

   * Accept within ±tolerance (config, e.g. ±1–2 seconds or ±0.5%).
3. **Track context**:

   * Disc/track number compatibility with release.

Scoring:

* Hard reject if outside duration tolerance unless user opts into "loose matching".
* Prefer candidates with:

  * Verified hash.
  * Matching MBID.
  * Prior successful usage.

### 7.2 Verified Copies

When a file passes all checks:

* Create/refresh `VerifiedCopy` entry.
* Use that as the truth source for:

  * Later candidate verification.
  * DHT/mesh announcements of "this node has a correct copy".

### 7.3 Canonical Naming

Use VirtualSoulfind's knowledge to generate canonical paths:

* `Artist/Year - Album/[Disc-]Track - Title (Source, Codec, Bitrate).ext`

Applied to:

* New downloads.
* Optional library normalization passes.

---

## 8. Integration with Service Fabric & HTTP Gateway

### 8.1 Mesh Service: `VirtualSoulfindService`

Expose a mesh service (via `IMeshService`) with methods like:

* `GetVirtualRelease(releaseId)`
* `ListMissingTracksForRelease(releaseId)`
* `GetPlanForRelease(releaseId)`
* `RegisterVerifiedCopy(trackId, hash, duration)`
* `QuerySourceCandidates(trackId)`

Security:

* Only expose non-sensitive metadata.
* Do not expose local file paths or OS-level details.
* Enforce work budgets and rate limits per peer.

### 8.2 HTTP Gateway

Expose a local HTTP facade (through the hardened gateway, H-01):

Examples:

* `GET /virtual/releases/{releaseId}`
* `GET /virtual/releases/{releaseId}/missing`
* `POST /virtual/intents/releases/{releaseId}` (create `DesiredRelease`)
* `GET /virtual/intents/releases` (list queue)
* `POST /virtual/plan/releases/{releaseId}/execute` (under strict config, triggers resolver)

All calls must:

* Respect gateway auth/CSRF and allowlists.
* Respect backend limits and work budgets.

---

## 9. Workflows

### 9.1 Virtual Catalogue Browsing

1. User (or tool) queries `/virtual/releases?artist=...`.
2. VirtualSoulfind returns purely metadata from catalogue store.
3. No Soulseek or other network traffic unless planner/resolver is explicitly invoked.

### 9.2 Add Release to Intent Queue

1. UI/CLI/HTTP calls `POST /virtual/intents/releases/{releaseId}` with mode (Wanted, Backfill).
2. VirtualSoulfind creates `DesiredRelease` + child `DesiredTrack` entries.
3. Status is `Pending`.

### 9.3 Plan & Execute Acquisition

1. Resolver job picks a `DesiredRelease` or `DesiredTrack` with `Pending`.

2. Calls Multi-Source Planner with current `PlanningMode` and policy.

3. Planner generates `TrackAcquisitionPlan`.

4. Execution layer:

   * For each plan step:

     * Consults backends.
     * Respects work budget and Soulseek caps.
     * Uses match/verification engine on results.

5. On success:

   * `DesiredTrack.Status` → `Completed`.
   * Verified copy updated.

6. On failures or timeouts:

   * `Status` → `Failed` / `OnHold` with reason.

### 9.4 Library Reconciliation

1. Library scanner populates `LocalFile` entities.
2. Match engine tries to map them to `Track` entities.
3. VirtualSoulfind can then:

   * Identify missing tracks.
   * Suggest upgrades (low quality → better quality).
   * Update `VerifiedCopy`.

No network traffic required.

---

## 10. Policy, Limits & Modes (Soulseek Safety)

VirtualSoulfind will own high-level policy decisions.

### 10.1 Per-Backend Config Example

```yaml
VirtualSoulfind:
  Modes:
    DefaultMode: SoulseekFriendly
  Backends:
    Soulseek:
      MaxSearchesPerMinute: 3
      MaxBrowsesPerMinute: 2
      MaxParallelSearches: 1
      MaxParallelDownloadsFromSameUser: 1
    MeshDht:
      MaxSearchesPerMinute: 50
      MaxParallelSearches: 20
    Torrent:
      MaxMetadataRequestsPerMinute: 100
    Http:
      MaxParallelFetches: 10
    LocalLibrary:
      MaxScanJobs: 4
```

### 10.2 Modes

* **SoulseekFriendly**
  Soulseek allowed, but under strict caps and concurrency limits.

* **OfflinePlanning**
  No network backends; VirtualSoulfind only uses catalogue + local library.

* **MeshOnly**
  Only MeshDHT/Torrent/HTTP/LAN backends are used; Soulseek disabled in planner.

These modes integrate with the work budget (H-02) and Soulseek limiter (H-08).

---

## 11. Security & Privacy

Key principles:

* No third-party Soulseek user data in DHT or VirtualSoulfind indices:

  * No usernames.
  * No room names.
  * No IPs.

* Source candidates for Soulseek:

  * Only represent *this node's* knowledge of "user X offers file path Y", stored locally.
  * Not published into DHT.

* DHT/mesh announcements:

  * Only expose internal peer IDs and content IDs, never Soulseek-specific identifiers.

* HTTP gateway:

  * Protected by API key and CSRF header (H-01).
  * Only exposes VirtualSoulfind metadata and high-level instructions, not arbitrary local filesystem access.

---

## 12. Backwards Compatibility & Migration

* Existing VirtualSoulfind behaviors should:

  * Continue to work with minimal changes.
  * Be gradually refactored into the new components.

* Migration steps:

  1. Introduce new data structures and store alongside existing ones.
  2. Add thin adapters to bridge old call sites into new planner/backend abstractions.
  3. Slowly move code over; once stable, deprecate old structures.

* Legacy Soulseek-only mode:

  * If `LegacySoulseekMode` is enabled:

    * VirtualSoulfind should limit itself to:

      * Local catalogue & library.
      * No mesh/DHT or turbo features.
    * Behavior should approximate plain slskd.

---

## 13. Observability

* Metrics:

  * `virtualsoulfind_intents_total` (by status/mode).
  * `virtualsoulfind_plans_total` (by backend, outcome).
  * `virtualsoulfind_matches_total` (successful vs failed matches).
  * Soulseek-side metrics (from H-08) broken out by origin (`User`, `MeshService`).

* Logs:

  * Summarize plans and outcomes without logging full file paths or PII.
  * Log when:

    * Soulseek caps are hit.
    * Work budgets are exhausted.
    * DHT/mesh poisoning heuristics trigger.

---

## 14. Open Questions / Future Work

* Audio fingerprinting:

  * Which fingerprint scheme do we standardize on (AcoustID, custom, etc.)?
  * How do we handle CPU cost for large libraries?

* Shared "verified copy registry" over mesh:

  * Do we ever share verified hashes with trusted peers?
  * How to avoid turning that into a global leak of libraries?

* Smart prioritization:

  * Use ListenBrainz/Last.fm-like data to prioritize "most value per GB" backfills.

* UI:

  * How we expose these capabilities in the slskdn UI vs just via CLI/API.

---

## 15. Implementation Phases

See `VIRTUALSOULFIND-V2-TASKS.md` for detailed task breakdown.

### Phase 1: Foundation (V2-P1)
- Data model and schema
- Backend interface abstractions
- Basic catalogue store

### Phase 2: Intent & Planning (V2-P2)
- Intent queue
- Multi-source planner
- Source registry

### Phase 3: Verification & Matching (V2-P3)
- Match engine
- Verified copy registry
- Canonical naming

### Phase 4: Backend Implementations (V2-P4)
- Soulseek backend (with H-08 caps)
- Mesh/DHT backend
- Local library backend
- Optional: Torrent/HTTP/LAN backends

### Phase 5: Integration (V2-P5)
- Mesh service facade
- HTTP gateway endpoints
- Work budget integration (H-02)

### Phase 6: Advanced Features (V2-P6)
- Library reconciliation
- Smart prioritization
- Gap analysis
- Quality upgrades

---

This design gives you a coherent architecture that:

* Works with the multi-source-swarm/service-fabric work already completed.
* Treats VirtualSoulfind as the hub for catalogue, planning, and verification.
* Shifts "turbo" behavior firmly onto mesh/DHT/BT and away from Soulseek, by design.
* Maintains backwards compatibility and Soulseek-friendly defaults.
