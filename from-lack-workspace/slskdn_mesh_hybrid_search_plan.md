# slskdn — Hybrid Soulseek + Mesh Search (Code-Based Plan)

This plan is based strictly on the current code in the repo zip you provided (no documentation assumptions).

## 1) What “disaster mode” does today (code reality)

### Activation/deactivation
- `VirtualSoulfind/DisasterMode/DisasterModeCoordinator.cs` subscribes to `SoulseekHealthMonitor.HealthChanged`.
- If `Options.VirtualSoulfind.DisasterMode.Auto == true`, then:
  - On `SoulseekHealth.Unavailable`, it increments `consecutiveUnhealthyChecks` and activates disaster mode after `UnavailableThresholdMinutes` (default 10 minutes, using 30s checks).  
  - On `SoulseekHealth.Healthy`, it waits 1 minute for stability then deactivates.

### Search routing
- `Search/SearchService.cs` checks `disasterModeCoordinator.IsDisasterModeActive`.
  - If **active**, it calls `StartMeshOnlySearchAsync(...)` instead of the normal `Client.SearchAsync(...)` path.
  - If **not active**, it uses Soulseek searches only.

### What mesh-only search does today
- `SearchService.StartMeshOnlySearchAsync` currently requires MBID resolution (`mbidsResolver.ResolveAsync(...)`).
- It then calls the disaster-mode mesh search service `VirtualSoulfind/DisasterMode/MeshSearchService.cs`.
- The current `MeshSearchService.SearchAsync` is effectively a placeholder:
  - It pulls “mesh peers” from `MeshSyncService.GetMeshPeers()`.
  - For each peer, it fetches “content IDs” from DHT via `IMeshDirectory.FindContentByPeerAsync(peer.Username)`.
  - It matches by **substring on content ID** (and “codec” if present), and returns “files” whose `Filename` is set to the **content ID**. That is not a usable filename search.

**Implication:** today, user-facing mesh searching is (a) only used in disaster mode, and (b) not wired to a real text search / filename result set.

## 2) Answer to your “orthogonal vs not wired” question

Both statements can be simultaneously true:

- “Orthogonal” in architecture: the mesh subsystem *can* run alongside Soulseek.
- “Not wired in” in implementation: user-facing search in `SearchService` routes to mesh only under disaster mode; and the mesh search implementation itself is currently not a real filename/text search.

**Net:** right now, mesh does not contribute to normal searches. It is not meaningfully “working” for search outside disaster mode, and even in disaster mode it depends on MBID resolution and placeholder matching.

## 3) Your desired behavior

You want, at all times:
- Searches go out on **both** Soulseek and mesh.
- Results are **aggregated**.
- Downstream decisions can choose the best source(s) across both networks.

## 4) Minimal, surgical implementation path (MVP)

Goal: add **parallel mesh search** without breaking any existing Soulseek behavior.

### Key constraints for “don’t break existing”
- Soulseek search path stays unchanged unless a new feature flag is enabled.
- Disaster mode behavior stays unchanged unless the flag is enabled AND you explicitly opt to change it.
- All new mesh search behavior is additive, isolated, and time-bounded.

### MVP design
Implement mesh search as an overlay RPC to peers:
- Initiator sends a `mesh_search_req` over the existing TLS overlay channel.
- Each peer runs a local search against its share DB (`IShareRepository.Search(...)`), caps results, and replies with `mesh_search_resp`.
- Initiator converts peer replies to `slskd.Search.Types.Response` and merges them with Soulseek search results.

This avoids needing:
- MusicBrainz, MBID resolution, or any external services.
- A distributed KV DHT for keyword indexing (your current Kademlia-style `IDhtClient` is registered to `InMemoryDhtClient` by default).

### Why overlay RPC is the true “minimum that works”
- You already have a working peer-to-peer transport:
  - `DhtRendezvous/MeshOverlayServer.cs`
  - `DhtRendezvous/MeshOverlayConnection.cs`
  - mutual TLS, cert pinning, rate-limiter hooks, etc.
- You already have a local shares search engine:
  - `Shares/IShareRepository.Search(SearchQuery query)`
  - implemented by `Shares/SqliteShareRepository.Search(...)`.

## 5) Concrete change list (files + responsibilities)

### 5.1 Add two overlay message types
File: `src/slskd/DhtRendezvous/Messages/OverlayMessages.cs`

1) Add constants:
- `OverlayMessageType.MeshSearchReq = "mesh_search_req"`
- `OverlayMessageType.MeshSearchResp = "mesh_search_resp"`

2) Add feature flag constant:
- `OverlayFeatures.MeshSearch = "mesh_search"`

3) Add message classes:
- `MeshSearchRequestMessage : OverlayMessage`
  - `request_id` (string GUID)
  - `search_text` (string)
  - `max_results` (int, required, clamp)
  - optional: `scope` (string) if you want later (audio-only, etc)

- `MeshSearchResponseMessage : OverlayMessage`
  - `request_id` (string GUID)
  - `files` (list of file DTOs)
  - optional: `truncated` (bool)
  - optional: `error` (string)

Define a small file DTO **owned by you** (don’t reuse Soulseek types across the wire):
- `MeshSearchFileDto`
  - `filename` (string, virtual share path only; never absolute)
  - `size` (long)
  - `extension` (string?)
  - optional: `bitrate`, `duration`, `codec` if available in your DB

### 5.2 Validate and rate-limit the new messages
File: `src/slskd/DhtRendezvous/Security/MessageValidator.cs`

Add validation rules:
- `request_id` must parse as GUID.
- `search_text` non-empty, length <= e.g. 256.
- `max_results` clamp 1..200 (or 500) and reject 0 or negative.
- enforce maximum serialized payload size (reuse existing checks if present).

File: `src/slskd/DhtRendezvous/OverlayRateLimiter.cs`
- Add a token bucket entry for `mesh_search_req` to prevent request floods.
- Add separate limit for responses to prevent amplification.

### 5.3 Add request/response send/receive support on the overlay connection
File: `src/slskd/DhtRendezvous/MeshOverlayConnection.cs`

Currently it only supports `PingAsync` and `DisconnectAsync` and `ReadRawMessageAsync`.

Add:
- `Task SendMessageAsync(OverlayMessage msg, CancellationToken ct)`
- `Task<T> RequestAsync<TReq,TResp>(TReq req, TimeSpan timeout, CancellationToken ct)`
  - maintain an internal concurrent dictionary: `pending[request_id] = TaskCompletionSource<TResp>`
  - start a background read loop on connection if not already running, dispatching messages:
    - if `mesh_search_resp` and request_id matches -> complete TCS
    - else route to server side handler (server has its own loop; client needs a reader)

Important: keep existing ping/disconnect semantics intact.
- Implement new methods without altering existing ones.
- Avoid deadlocks by using `ConfigureAwait(false)` and cancellation tokens.

### 5.4 Handle inbound search requests on the server
File: `src/slskd/DhtRendezvous/MeshOverlayServer.cs`

In the message processing loop, add:
- `case OverlayMessageType.MeshSearchReq:`
  - call `IMeshSearchRpcHandler.HandleAsync(connection, request, ct)`
  - send `MeshSearchResponseMessage` back

Create new service:
- `src/slskd/DhtRendezvous/Search/MeshSearchRpcHandler.cs`
  - depends on:
    - `IShareRepository` (or a narrower abstraction)
    - `IOptionsMonitor<Options>` for caps
  - algorithm:
    - parse request.SearchText -> `Soulseek.SearchQuery.FromText(searchText)`
    - run `_shareRepository.Search(query)` which returns `IEnumerable<Soulseek.File>`
    - take `max_results` after ordering (see below)
    - map to `MeshSearchFileDto`
    - return response

Ordering:
- Use the same ordering rules you use for “local share search responses” elsewhere if any exist.
- If none exist: stable order by filename (or a simple score: token hits count, then shorter distance).
- Keep deterministic to support tests.

Security rules:
- Never return absolute paths.
- Never return locked/private shares (ensure repository method already filters; if not, add a filter).

### 5.5 Build a “mesh peer list” to query
On the initiating side we need a list of reachable mesh peers.

Sources already in code:
- `DhtRendezvous/MeshNeighborRegistry.cs` maintains neighbors discovered via rendezvous.
- `Mesh/MeshSyncService.GetMeshPeers()` returns known mesh-capable peers (likely Soulseek-derived).

MVP: query both sets and dedupe by username.
- Prefer `MeshNeighborRegistry` first, because it contains endpoints for overlay connections.

Add a new service:
- `src/slskd/DhtRendezvous/Search/MeshPeerEnumerator.cs`
  - returns `(username, endpoints[])`
  - dedupe by username.

### 5.6 Integrate mesh search into the main search pipeline (hybrid)
File: `src/slskd/Search/SearchService.cs`

Current logic:
- If disaster mode active -> `StartMeshOnlySearchAsync`
- else -> Soulseek `Client.SearchAsync`

Change to:
- Always do Soulseek search as today (unless explicitly in mesh-only mode).
- If `Options.VirtualSoulfind.MeshSearch.Enabled == true`:
  - start a mesh overlay search task in parallel:
    - `var meshResults = await meshOverlaySearch.SearchAsync(query, scope, ct)`
  - merge those results into `search.Responses` at completion.

Implementation detail to minimize risk:
- Keep the `List<Soulseek.SearchResponse> responses` list unchanged for Soulseek.
- Add `List<slskd.Search.Types.Response> meshResponses`.
- At completion:
  - `var soulseekResponses = responses.Select(Response.FromSoulseekSearchResponse);`
  - `search.Responses = Deduplicate(soulseekResponses.Concat(meshResponses));`

Deduplication key:
- `(username, filename, size)` is adequate for MVP.
- If you have hashes in either path, include them.

Also update counts:
- `search.ResponseCount`, `search.FileCount` should include mesh results.
- Do NOT broadcast full response payload over SignalR (keep existing `Responses = []` broadcast).

### 5.7 Keep disaster mode, but remove the “MBID hard dependency”
File: `src/slskd/Search/SearchService.cs`, method `StartMeshOnlySearchAsync`

Change behavior:
- If MBID resolution returns MBIDs -> keep current MBID-based mesh lookup path (good when available).
- If MBIDs are empty OR resolver is disabled/unavailable:
  - fallback to the new overlay mesh text search path (same as hybrid uses).

This satisfies “MusicBrainz helps but is not mandatory”.

## 6) Testing requirements (Enforce-level)

### 6.1 Unit tests
Add tests for:
- `MessageValidator` rejects invalid `mesh_search_req` and `mesh_search_resp`.
- `MeshSearchRpcHandler`:
  - respects max_results clamp
  - does not return locked/private files
  - deterministic ordering
- `SearchService` merge logic:
  - mesh disabled -> search outputs identical to baseline
  - mesh enabled with 0 mesh peers -> identical to baseline
  - mesh enabled with 1 mesh peer -> adds mesh responses without altering Soulseek ones

### 6.2 Integration tests (loopback)
Spin up:
- a `MeshOverlayServer` on localhost with test cert
- a `MeshOverlayConnection` client
- issue a `mesh_search_req`
- validate `mesh_search_resp` matches expected

Key: run under timeouts and ensure connection closes cleanly.

### 6.3 Regression safeguards
- Add feature flag default **false** to protect existing deployments.
- Add structured logs around mesh search start/stop and peer failures.
- Add strict per-peer timeout (e.g. 2–3 seconds) so mesh cannot stall Soulseek searches.

## 7) What this gives you immediately

- Mesh search participates **even when Soulseek is healthy** (hybrid mode).
- No external metadata dependency.
- A real mesh query returns real filenames from the peer’s share DB.
- Aggregation is real: Search API consumers see a single merged result set.

## 8) Next step after MVP (not required, but the obvious evolution)

Once MVP works:
- Add optional token->peer discovery via DHT rendezvous (announce per keyword) to avoid querying all neighbors.
- Add a “mesh download” transport so mesh results can become first-class sources, not just search hits.
- Add source-quality scoring (bandwidth, reliability, proximity) to choose “best of both”.

