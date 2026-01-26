# ShareGroups + Collections + Shares + Streaming + Hybrid Search — Assessment & Merged Design

**Source:** Older agent-ticket instructions (not gospel).  
**Purpose:** Assess validity and feasibility; merge ideas into existing slskdn code and recent changes.  
**Principle:** Additive, feature-flagged; no client-supplied paths; capability-based access; single-range streaming; mesh search offline.

---

## 1. Mapping: Ticket vs Existing slskdn

### 1.1 Feature flags and config

| Ticket | Existing | Assessment |
|--------|----------|------------|
| `Features.CollectionsSharing` | — | **New.** Add to `Options.Feature` (we have `Feature.Swagger`). |
| `Features.Streaming` | — | **New.** |
| `Features.StreamingRelayFallback` | — | **New.** Relay exists but is controller↔agent file xfer, not streaming. |
| `Features.MeshParallelSearch` | `VirtualSoulfind.MeshSearch.Enabled` | **Exists.** Rename/alias or extend: MeshParallelSearch = MeshSearch.Enabled. |
| `Features.MeshPublishAvailability` | — | **New.** Can align with DHT/content publishing or defer. |

**Merge:** Extend `Options.Feature` (or a new `Options.Features` to avoid collision with `Feature` = Swagger) with: `CollectionsSharing`, `Streaming`, `StreamingRelayFallback`, `MeshParallelSearch` (or wire to `VirtualSoulfind.MeshSearch.Enabled`), `MeshPublishAvailability`. All default `false`. Wire controllers only when enabled; 404/403 when disabled.

---

### 1.2 Core data model: ShareGroup, Collection, Share, SharePolicy

| Ticket | Existing | Assessment |
|--------|----------|------------|
| **Share** (slskdn) | `Shares.IShareRepository` = **file/dir DB** (Soulseek shares: files, dirs, scans, `content_items`). | **Different.** Our "Share" = configured share roots + files. Ticket "Share" = granting a **Collection** to a user/group with a **SharePolicy**. |
| **ShareGroup** | — | **New.** `ShareGroup` (Id, Name, OwnerUserId, CreatedAt, UpdatedAt), `ShareGroupMember`. |
| **Collection** | — | **New.** `Collection` (Id, OwnerUserId, Title, Description, Type: ShareList|Playlist, CreatedAt, UpdatedAt). |
| **CollectionItem** | — | **New.** `CollectionId`, `Ordinal`, `ContentRef`. |
| **ContentRef** | `contentId` in `content_items` (IShareRepository); `MediaCore.ContentId`; `IContentIdRegistry`. | **Align.** Use existing ContentId (maps to library index). Add `MediaKind` (Music/TV/Movie/Book/Other) if useful; optional `ContentHash`. |
| **Share** (ticket) | — | **New.** `Share`(Id, CollectionId, AudienceType: User|ShareGroup, AudienceId, Policy, CreatedAt, UpdatedAt). |
| **SharePolicy** | — | **New.** AllowStream, AllowDownload, AllowReshare, ExpiryUtc, MaxConcurrentStreams, (optional) MaxBitrateKbps. |

**Repos:** Add `IShareGroupRepository`, `ICollectionRepository`, `IShareRepository` (ticket meaning: share **grants**). Name clash: our `IShareRepository` = files. **Rename ticket concept** to `IShareGrantRepository` or `ICollectionShareRepository` to avoid confusion.  
**Service:** `ISharingService` (or `ICollectionsSharingService`) for groups, collections, share-grants, and "list shares I can access."

---

### 1.3 Share tokens (capability access)

| Ticket | Existing | Assessment |
|--------|----------|------------|
| `IShareTokenService` | — | **New.** JWT or HMAC; claims: shareId, collectionId, audienceId?, capabilities (stream, download), exp, maxConcurrentStreams. Constant-time validation; expiry enforced. |
| Revocation | Optional store | Defer or minimal: store `ShareTokenId`+status only if needed. |

**Feasibility:** Straightforward. Use existing JWT/hmac infrastructure if any; else add `Microsoft.AspNetCore.Authentication.JwtBearer` or a minimal HMAC sign/verify. No client paths in tokens.

---

### 1.4 API: ShareGroups, Collections, Share-grants

| Ticket | Existing | Assessment |
|--------|----------|------------|
| `POST/GET /api/v0/sharegroups`, `.../members` | — | **New.** When `Features.CollectionsSharing`. |
| `POST/GET /api/v0/collections`, `.../items`, `.../reorder` | — | **New.** |
| `POST/GET /api/v0/shares`, `POST /api/v0/shares/{id}/token` | — | **New.** "shares" = share-grants. |

**Auth:** Normal auth; ownership on modify. Only recipient or group member can view a share.  
**User id:** slskdn uses Soulseek username as principal in many places; we can use `Options.Soulseek.Username` or a `UserId` if we introduce one. For v1, **OwnerUserId** = Soulseek username (single-user instance).

---

### 1.5 Content locator and streaming

| Ticket | Existing | Assessment |
|--------|----------|------------|
| `IContentLocator.Resolve(contentId)` | `IShareRepository.FindContentItem(contentId)` → `(Domain, WorkId, MaskedFilename, IsAdvertisable, …)`. `FindFileInfo(maskedFilename)` → `(Filename, Size)`. | **Need path.** `FindFileInfo` returns `Filename` (repository’s stored path). We need **MaskedFilename → absolute path** via share roots. `ShareService` / `ShareRepository` has host/path mapping. |
| **IContentLocator** | — | **New interface.** Implementation: ContentId → FindContentItem (IsAdvertisable check) → MaskedFilename → resolve to absolute path via share config; return `ResolvedContent` (Path, Length, MimeType). Never accept client path. |
| `GET /api/v0/streams/{contentId}` | — | **New.** When `Features.Streaming`. Auth: normal **or** `?token=...` / `Authorization: Bearer share:<token>`. If token: validate `IShareTokenService`, require `stream`, unexpired, share contains contentId. `File(..., enableRangeProcessing: true)`, **single-range only** (reject multi-range 416/400). Concurrency + rate limit (see 1.6). |

**Resolve path:** `IShareRepository` / `ShareService` has or can expose a "masked → absolute" resolver using `Directories` / share roots. If not, add `ISharePathResolver` or extend `IShareRepository` with `GetAbsolutePath(maskedFilename)`.

---

### 1.6 Concurrency and rate limiting for streaming

| Ticket | Existing | Assessment |
|--------|----------|------------|
| `IStreamSessionLimiter.TryAcquire(key, maxConcurrent)` / `Release(key)` | — | **New.** In-memory v1. Key: shareId+audience or token id, optionally + client IP. |
| Rate limit | ASP.NET `RateLimiter` or internal | We have rate limit middleware. Add policies: stream per-token + per-IP; manifest per-IP. 429 on excess. |

---

### 1.7 Share manifest

| Ticket | Existing | Assessment |
|--------|----------|------------|
| `GET /api/v0/shares/{shareId}/manifest` | — | **New.** Auth: token or normal. Response: collection metadata, ordered items, per-item `contentId` and `streamUrl` (template with token if token auth). If `AllowStream=false`, omit streamUrl or 403. |

---

### 1.8 Relay streaming fallback

| Ticket | Existing | Assessment |
|--------|----------|------------|
| `GET /api/v0/relay/streams/{contentId}` | `RelayController`: `DownloadFile`, `UploadFile` (controller↔agent file xfer by token+filename). No range, no contentId. | **Extend.** When `Features.StreamingRelayFallback`: new endpoint. Auth: share token (no anonymous). Proxy byte ranges to agent via new RPC: `OpenStream(contentId)`, `Read(handle, offset, length)`, `Close(handle)`. Agent must implement these. |
| Agent RPC | Relay has agent↔controller channel | **New RPC shapes.** OpenStream/Read/Close. Caps: max open streams per agent, bytes/s per handle, idle timeout. |

**Feasibility:** Moderate. Relay transport exists; needs new message types and agent-side streaming from local shares by contentId.

---

### 1.9 Offline mesh search RPC

| Ticket | Existing | Assessment |
|--------|----------|------------|
| `MeshSearchRequest` (searchId, queryText, mediaKinds?, limitPerPeer, timestampUtc) | `MeshSearchRequestMessage`: RequestId, SearchText, MaxResults, Scope. | **Mostly aligned.** We have overlay `mesh_search_req` / `mesh_search_resp`. Add `mediaKinds` (optional) and `searchId`/timestamp if we want idempotency. |
| `MeshSearchResponse` / `MeshSearchHit` | `MeshSearchResponseMessage`: RequestId, Files (Filename, Size, Extension, Bitrate, Duration, Codec), Truncated, Error. `MeshSearchFileDto` ≈ MeshSearchHit. | **Aligned.** We have contentId in `content_items`; `MeshSearchRpcHandler` uses `IShareService.SearchLocalAsync` and returns Filename (masked), Size, etc. We can add `contentId` and `hash` to DTO if available. |
| `ILocalShareSearch.Search(query, kinds, limit)` | `IShareService.SearchLocalAsync(SearchQuery)`. | **Extend.** Add optional `kinds` filter; `MeshSearchRpcHandler` can pass it. |
| Reject long queries; CPU/time cap (e.g. 200ms); partial results | `MessageValidator` clamps MaxResults; handler uses ShareService. | Add query length limit and a timeout/cancellation in handler; return partial on timeout. |

**Merge:** Keep `MeshSearchRequestMessage` / `MeshSearchResponseMessage`; extend with optional `mediaKinds`, `contentId`/`hash` in hits. `MeshSearchRpcHandler` already runs local search; add `ILocalShareSearch` abstraction if we want to swap impl. **Offline:** we already run against local `IShareRepository`/`IShareService`; no external calls. Good.

---

### 1.10 Mesh search provider and SearchService integration

| Ticket | Existing | Assessment |
|--------|----------|------------|
| `ISearchProvider.StartSearchAsync(SearchRequest, IResultSink, CancellationToken)` | — | **New abstraction.** |
| `SoulseekSearchProvider` | `SearchService` calls `Client.SearchAsync` directly. | **Wrap** existing Soulseek call in a provider. |
| `MeshSearchProvider` | `IMeshOverlaySearchService.SearchAsync(query, ct)` used in `SearchService.StartAsync` when `VirtualSoulfind.MeshSearch.Enabled`. | **Wrap** `IMeshOverlaySearchService` in `MeshSearchProvider`. |
| `SearchAggregator` | `SearchResponseMerger.Deduplicate(soulseekResponses, meshResponses)` by (Username, Filename, Size). | **Extend.** Ticket: dedup by (1) hash, (2) (normalized filename, size). We don’t have hash in `Response`/`File` yet; add when available. For now keep (Username, Filename, Size); optionally (normalized filename, size) for cross-username dedup. |
| `SearchService.StartAsync` | Soulseek + mesh overlay in parallel when `MeshSearch.Enabled`; merge with `SearchResponseMerger`. | **Already aligned.** Gate with `Features.MeshParallelSearch` or keep `VirtualSoulfind.MeshSearch.Enabled`. If mesh throws, Soulseek still wins. |

**Merge:**  
- Introduce `ISearchProvider` and `SoulseekSearchProvider` / `MeshSearchProvider` as a refactor; or keep current structure and only ensure mesh never blocks Soulseek.  
- `SearchResponseMerger`: add hash when present; else (normalized filename, size).  
- `Features.MeshParallelSearch` can map to `VirtualSoulfind.MeshSearch.Enabled` so one knob.

---

### 1.11 Mesh transfer (download from mesh hit)

| Ticket | Existing | Assessment |
|--------|----------|------------|
| `IMeshContentFetcher.Fetch(contentId, peerId) -> Stream` | `SimpleResolver` + `MeshContent.GetByContentId` (IMeshServiceClient) for `mesh:{peerId}:{contentId}`; writes to temp file. `MeshContentMeshService` serves file bytes. | **Mostly done.** We have fetch-by-ContentId over mesh. Ticket wants `Stream`; we can add a streaming overload or keep file-to-stream for now. |
| Resolve peer from overlay; size caps; integrity if hash | `IMeshServiceClient.CallAsync(peerId, ...)`. Size capped by `_maxPayload`. | Add explicit size cap and optional hash check when hash in hit. |

**Merge:** Consider `IMeshContentFetcher` as a thin facade over `IMeshServiceClient` + MeshContent.GetByContentId, returning `Stream` or `async Stream` (from temp file or chunked). Or keep existing resolver path and add an API that uses it; ticket’s “download-from-peer” is satisfied by existing mesh fetch. **Feasibility:** High.

---

### 1.12 Fixtures and integration tests

| Ticket | Existing | Assessment |
|--------|----------|------------|
| Small deterministic fixtures (MP4, WebM, OGG, text) | `test-data/slskdn-test-fixtures/` (music, movie, tv, book). | **Exists.** Extend if needed for new cases. |
| Tests: collection+share group+share, token, manifest, stream with range, mesh search, hybrid | — | **Add** as we add features. |

---

## 2. What to adopt, defer, or drop

### Adopt (merge into codebase)

1. **Feature flags:** `CollectionsSharing`, `Streaming`, `StreamingRelayFallback`, `MeshParallelSearch` (or wire to MeshSearch.Enabled), `MeshPublishAvailability` in `Options.Feature`/`Features`.
2. **ShareGroup, Collection, CollectionItem, ContentRef, Share (grant), SharePolicy** — new model and persistence. Use `ICollectionShareRepository` (or `IShareGrantRepository`) to avoid clashing with `IShareRepository`.
3. **ISharingService** (or ICollectionsSharingService) for CRUD and “shares I can access”.
4. **IShareTokenService** — JWT or HMAC, capabilities, exp, maxConcurrentStreams.
5. **API:** `/api/v0/sharegroups`, `/api/v0/collections`, `/api/v0/shares` (grants), `POST /api/v0/shares/{id}/token`, `GET /api/v0/shares/{id}/manifest` — all behind `CollectionsSharing` or `Streaming` where relevant.
6. **IContentLocator** — ContentId → ResolvedContent (path, length, mime) via `FindContentItem` + `FindFileInfo` + share path resolution. **Never** client path.
7. **GET /api/v0/streams/{contentId}** — range-only, token or normal auth, `File(..., enableRangeProcessing: true)`, single-range only.
8. **IStreamSessionLimiter** + rate limiting for stream and manifest.
9. **Mesh search:** extend overlay types with optional `mediaKinds`, `contentId`/`hash`; add query length and CPU/time cap; keep offline.
10. **Search:** keep current “Soulseek + mesh in parallel + SearchResponseMerger”; optionally introduce `ISearchProvider` and improve dedup (hash, normalized filename+size). `MeshParallelSearch` ≈ `MeshSearch.Enabled`.
11. **Mesh fetch:** we already have it; add `IMeshContentFetcher` if we want a clear API; enforce size and optional hash check.

### Defer

- **MeshPublishAvailability:** needs product clarity (what to publish, where). Defer.
- **Relay streaming fallback** (`StreamingRelayFallback`): implement only after direct streaming and relay RPC design. Defer to Phase 2.
- **AllowReshare, MaxBitrateKbps:** policy fields can exist in schema; behavior later.
- **Distributed stream limiter:** multi-instance; v2.
- **ISearchProvider refactor:** nice-to-have; can keep current structure and still meet “mesh never blocks Soulseek”.

### Drop or reshape

- **“Share”** in ticket → **“ShareGrant”** or **“CollectionShare”** in our model to avoid confusion with `IShareRepository` (files).
- **UserId:** use Soulseek username as owner in single-user v1; introduce proper UserId when we have multi-user.

---

## 3. Merged implementation order

**Phase 1 (foundations)** ✅ COMPLETE  
1. Feature flags in Options; wire so host boots with all false.  
2. `IContentLocator` using `IShareRepository` + share path resolution (and `IsAdvertisable`).  
3. `IShareTokenService` (JWT or HMAC), no revocation store.

**Phase 2 (collections and sharing)** ✅ COMPLETE  
4. Entities: ShareGroup, ShareGroupMember, Collection, CollectionItem, ContentRef, ShareGrant, SharePolicy.  
5. `IShareGroupRepository`, `ICollectionRepository`, `ICollectionShareRepository` (or `IShareGrantRepository`).  
6. `ISharingService`.  
7. Controllers: sharegroups, collections, shares (grants), `POST /shares/{id}/token`, `GET /shares/{id}/manifest` — all behind `CollectionsSharing`.

**Phase 3 (streaming)** ✅ COMPLETE  
8. `IStreamSessionLimiter` + rate-limit policies for stream and manifest.  
9. `GET /api/v0/streams/{contentId}` with range, token or normal auth, single-range only.  
10. Manifest `streamUrl` template and policy (`AllowStream`).

**Phase 4 (mesh search improvements)** ✅ COMPLETE  
11. Mesh types: optional `mediaKinds`, `contentId`/`hash` in hits; query length and time cap in `MeshSearchRpcHandler`. ✅ DONE  
12. `SearchResponseMerger`: hash when present; (normalized filename, size) for dedup. ✅ DONE  
13. `MeshParallelSearch` flag: wire to `VirtualSoulfind.MeshSearch.Enabled` or replace. ✅ DONE (both flags can enable mesh search)

**Phase 5 (optional)** ✅ COMPLETE  
14. `IMeshContentFetcher` facade; size and hash checks. ✅ DONE  
15. Relay streaming fallback: OpenStream/Read/Close RPC and `GET /api/v0/relay/streams/{contentId}`. ✅ DONE

---

## 4. Namespace and file layout (conventions)

- **ShareGroups/Collections/Shares (grants):** e.g. `slskd.Collections`, `slskd.Collections.Sharing`, or `slskd.Sharing` (Groups, Collections, Grants).  
- **Streaming:** `slskd.Streaming` — `IContentLocator`, `IStreamSessionLimiter`, `StreamsController`.  
- **Tokens:** `slskd.Sharing` or `slskd.Common.Security` — `IShareTokenService`, `ShareTokenClaims`.  
- **Persistence:** SQLite via existing patterns (e.g. `DbContext` like PodCore) or dedicated `*Repository` like `IShareRepository` (files). Prefer one `CollectionsDbContext` for ShareGroup, Collection, CollectionItem, ShareGrant to keep migrations in one place.

---

## 5. Security checklist (from ticket)

- [x] No endpoint accepts filesystem paths or arbitrary URLs from clients.  
- [x] All access control capability-based (share tokens) or normal auth.  
- [x] Streaming: single-range; rate limits and concurrency caps.  
- [x] Mesh search: offline, no external lookups.  
- [ ] Share token: constant-time validation where applicable (do in impl).  
- [ ] ContentId → path only via `IContentLocator` and `IsAdvertisable` (and MCP) check.

---

## 6. Open questions

1. **Share path resolution:** `MaskedFilename` is the virtual/remote path. Shares have `LocalPath` and `RemotePath`; scan uses `originalFilename.ReplaceFirst(share.LocalPath, share.RemotePath)` to produce masked. Reverse: find Share whose `RemotePath` is a prefix of `MaskedFilename`, then `MaskedFilename.ReplaceFirst(share.RemotePath, share.LocalPath)`. Need Shares from `IShareScanner` or `Options.Shares` + `ShareService`/repository. `IShareRepository` does not store Share definitions (those come from Options + ShareScanner); consider `ISharePathResolver` that takes `(MaskedFilename, IReadOnlyList<Share>)` or gets Shares via `IShareScanner.Shares` / Options.  
2. **MimeType:** How we derive it today (extension, magic)? Reuse for `ResolvedContent`.  
3. **Multi-user:** When we add real UserId, migrate OwnerUserId and AudienceId.  
4. **Relay OpenStream/Read/Close:** Wire format (MessagePack, JSON) and how it fits into existing Relay protocol.

---

*Assessment done.*

**Status:**
- ✅ **Phase 1 (foundations)**: Complete - Feature flags, `IContentLocator`, `IShareTokenService` implemented
- ✅ **Phase 2 (collections and sharing)**: Complete - Models, repos, `ISharingService`, APIs implemented with tests
- ✅ **Phase 3 (streaming)**: Complete - `IStreamSessionLimiter`, `GET /api/v0/streams/{contentId}` endpoint, range request support, token/normal auth, comprehensive tests
- ✅ **Phase 4 (mesh search improvements)**: Complete - `MediaKinds`, `ContentId`, `Hash` in `MeshSearchFileDto`, `SearchResponseMerger` normalization, `MeshParallelSearch` flag wired
- ✅ **Phase 5 (optional)**: Complete - `IMeshContentFetcher` with size/hash validation, `GET /api/v0/relay/streams/{contentId}` endpoint for relay streaming fallback
