# T-906: Native Mesh Protocol Backend — Design

> **Status:** Implemented (backend + options + design). Resolver fetch for `mesh:{peerId}:{contentId}` and mesh RPC are follow-ups.

---

## One-line

`IContentBackend` that uses only the mesh overlay (no Soulseek, no BitTorrent). Find candidates via mesh/DHT; fetch via overlay transfer (RPC and resolver support defined here, implemented later).

---

## Current State (Post-Implementation)

- **`ContentBackendType.NativeMesh`** in `ContentBackendType.cs`.
- **`NativeMeshBackend`** in `VirtualSoulfind/v2/Backends/NativeMeshBackend.cs`:
  - `FindCandidatesAsync(ContentItemId)`: resolves `ContentItemId` → ContentId via `IContentIdRegistry` (`mb:recording:{guid}`) or fallback `content:audio:track:mb-{guid}`; calls `IMeshDirectory.FindPeersByContentAsync(contentId)`; builds `SourceCandidate` with `BackendRef = "mesh:{peerId}:{contentId}"`. Does **not** use `ISourceRegistry` (live DHT only).
  - `ValidateCandidateAsync`: checks `Backend == NativeMesh`, `BackendRef` format `mesh:{peerId}:{contentId}`, `MinimumTrustScore`; no mesh reachability check yet.
- **`NativeMeshBackendOptions`**: `Enabled` (default false), `MinimumTrustScore`, `MaxCandidatesPerItem`, `QueryTimeoutSeconds`.

---

## “Get Content by ContentId / Hash” RPC (Protocol — To Be Wired)

### Name and role

- **RPC name (proposed):** `MeshContent.GetByContentId` or `MeshContent.GetByHash`.
- **Role:** Allow a resolver (or overlay client) to request content bytes from a mesh peer by ContentId or by hash, for `BackendRef` values `mesh:{peerId}:{contentId}` (and in the future `mesh:{peerId}:{hash}`).

### Request

- **By ContentId:** `{ "contentId": "content:audio:track:mb-...", "range": { "offset": 0, "length": 65536 } | null }`. Omit `range` for full content.
- **By hash (future):** `{ "hash": "sha256:hex...", "range": ... }`.

### Response

- **Success:** stream or chunked response (reuse rescue/mesh chunk format where possible).
- **Failure:** error code + message (e.g. not found, overloaded, forbidden).

### Where it is wired

- **Overlay:** New RPC on the mesh overlay (e.g. on `MeshServiceRouter` or a dedicated `MeshContent` handler). Reuse existing chunk/transfer plumbing if present; otherwise new handler.
- **Resolver:** The v2 resolver does **not** yet implement fetch for `BackendRef` starting with `mesh:...`. That is a **follow-up**: when the resolver sees `Backend == NativeMesh` and `BackendRef` of the form `mesh:{peerId}:{contentId}`, it should call this RPC (or equivalent overlay API) to perform the transfer. Until then, NativeMeshBackend only provides **discovery**; fetch will fail at the resolver.

### Chunk protocol and incentives

- **Chunk protocol:** Reuse rescue/mesh chunking where possible; otherwise define a simple chunked stream. Exact format is **open**; see `RescueService` and mesh transfer code for patterns.
- **Incentives / ratio:** Deferred. Native mesh may use different rules than BitTorrent (e.g. tit-for-tat over overlay, or trust-based).

---

## ContentItemId → ContentId Resolution

- **Primary:** `IContentIdRegistry.ResolveAsync("mb:recording:" + itemId.Value)` (Music: ContentItemId = MB Recording ID = Guid).
- **Fallback:** `ContentIdParser.Create("audio", "track", "mb-" + itemId.Value.ToString("N"))` when the registry has no mapping. Best-effort for DHT keys that follow this convention.
- **Other domains (Video, Book, etc.):** Not implemented. A generic `ContentItemId` → ContentId or `(ContentItemId, ContentDomain)` → ContentId abstraction can be added later (e.g. `IContentIdResolver` or domain-specific registries).

---

## Dependencies

- **T-902 (DHT node):** `KademliaRoutingTable`, `DhtMeshService`, `KademliaRpcClient`.
- **T-903 (DHT storage):** `IDhtClient`, Store RPC.
- **Existing:** `IMeshDirectory` / `ContentDirectory`: `FindPeersByContentAsync(contentId)`, `GetContentDescriptorAsync(contentId)` (latter not on `IMeshDirectory`; usable for future validation). `MeshDhtClient` / `IDhtClient` used by `ContentDirectory` for `mesh:content:{contentId}`, `mesh:content-peers:{contentId}`.

---

## DI and Options

- **Options:** `IOptionsMonitor<NativeMeshBackendOptions>`. Bind from config (e.g. `VirtualSoulfindV2:NativeMesh` or `Options:NativeMesh`) when the host configures v2 backends.
- **Registration:** When the host wires the `IEnumerable<IContentBackend>` used by `MultiSourcePlanner` (and optionally `SimpleResolver`), add:
  - `services.Configure<NativeMeshBackendOptions>(configuration.GetSection("…"))` (or equivalent),
  - `services.AddSingleton<IContentBackend, NativeMeshBackend>()`.
- **Dependencies of `NativeMeshBackend`:** `IMeshDirectory`, `IContentIdRegistry`, `IOptionsMonitor<NativeMeshBackendOptions>`, `ILogger<NativeMeshBackend>`. All exist in the main app.
- **When enabled:** Gated by `NativeMeshBackendOptions.Enabled`. Can be always-on when mesh is enabled, or tied to a “mesh-only” / “disaster” mode in config.

---

## Use Cases

- Pure mesh-only deployments.
- Disaster mode (Soulseek/BitTorrent down).
- Closed communities (mesh overlay only, no public indexes).

---

## Open / Follow-ups

- Resolver: implement fetch for `BackendRef` `mesh:{peerId}:{contentId}` (and later `mesh:{peerId}:{hash}`) by calling the mesh “get content by ContentId / hash” RPC.
- Overlay: implement and register the RPC on the mesh service router (or dedicated handler).
- Chunk protocol: decide reuse vs. new format; document in overlay/mesh specs.
- Incentives/ratio for native mesh transfers.
- Optional: `GetContentDescriptorAsync` on `IMeshDirectory` or `ContentDirectory` for better `ValidateCandidateAsync` (e.g. check size/hash before fetch).
- Optional: `IContentIdResolver` or domain-aware resolution for Video/Book/GenericFile.

---

## References

- `9-research-design-scope.md` § T-906
- `IContentBackend`, `MeshDhtBackend`, `TorrentBackend`, `SourceCandidate`
- `IMeshDirectory`, `ContentDirectory`, `ContentIdParser`, `IContentIdRegistry`
- `docs/research/T-902-dht-node-design.md`, `docs/research/T-903-dht-storage-design.md`
