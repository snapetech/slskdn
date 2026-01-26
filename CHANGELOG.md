# Changelog

All notable changes to slskdn are documented here. slskdn is a distribution of slskd with advanced features and experimental subsystems.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

---

## [Unreleased]

### Identity & Friends (Phases 1-4)

- **Identity & Friends System** (`feature.identity_friends`): Human-friendly peer addressing and discovery system for the sharing workflow.
  - **Peer Profiles**: Signed `PeerProfile` objects with display names, friend codes, capabilities, and endpoints. Ed25519 cryptographic signing prevents spoofing.
  - **Contact Management**: Local contact list with nicknames (petnames), verification status, and cached endpoints. SQLite-backed `IdentityDbContext`.
  - **Friend Codes**: Short, shareable Base32-encoded codes (e.g., `ABCD-EFGH-IJKL-MNOP`) derived from PeerId for easy copy/paste.
  - **Invite Links**: Self-contained `FriendInvite` payloads encoded as `slskdn://invite/...` links with QR code support (WebUI pending).
  - **mDNS LAN Discovery**: Automatic peer discovery on local networks via mDNS (`_slskdn._tcp.local`). Raw UDP socket implementation for advertising; Zeroconf library for browsing.
  - **ShareGroups Integration** (Phase 3): `ShareGroupMember` now supports optional `PeerId` for Contact-based members. API supports adding members by PeerId or UserId (legacy). Manifest includes owner contact nickname when available.
  - **WebUI Components** (Phase 4): Complete UI implementation including:
    - Contacts page (`/contacts`) with All/Nearby tabs, Add Friend modal, Create Invite modal
    - ShareGroups page (`/sharegroups`) with Contacts dropdown for adding members
    - "Shared with me" page (`/shared`) displaying incoming shares with contact nicknames and manifest viewing
    - Collections API client library for all sharing operations
  - **API Endpoints**:
    - `GET/PUT /api/v0/profile/me` - Manage own profile
    - `GET /api/v0/profile/{peerId}` - Fetch peer profiles (public)
    - `POST /api/v0/profile/invite` - Generate invite links
    - `GET/POST/PUT/DELETE /api/v0/contacts` - Contact CRUD
    - `POST /api/v0/contacts/from-invite` - Add contact from invite
    - `POST /api/v0/contacts/from-discovery` - Add contact from LAN discovery
    - `GET /api/v0/contacts/nearby` - Browse nearby peers
    - `GET /api/v0/sharegroups/{id}/members?detailed=true` - Get members with contact info
  - **Feature Flag**: `feature.identity_friends` (default: `false`). All endpoints return 404 when disabled.
  - **Dependencies**: `Zeroconf` 3.0.30 (for mDNS browsing), `Microsoft.EntityFrameworkCore.Sqlite` (for contact storage).
  - **Tests**: 90 unit tests covering ProfileService, ContactService, ContactRepository, ProfileController, ContactsController, LanDiscoveryService, and MdnsAdvertiser.

- **Identity & Friends System** (`feature.identity_friends`): Human-friendly peer addressing and discovery system for the sharing workflow.
  - **Peer Profiles**: Signed `PeerProfile` objects with display names, friend codes, capabilities, and endpoints. Ed25519 cryptographic signing prevents spoofing.
  - **Contact Management**: Local contact list with nicknames (petnames), verification status, and cached endpoints. SQLite-backed `IdentityDbContext`.
  - **Friend Codes**: Short, shareable Base32-encoded codes (e.g., `ABCD-EFGH-IJKL-MNOP`) derived from PeerId for easy copy/paste.
  - **Invite Links**: Self-contained `FriendInvite` payloads encoded as `slskdn://invite/...` links with QR code support (WebUI pending).
  - **mDNS LAN Discovery**: Automatic peer discovery on local networks via mDNS (`_slskdn._tcp.local`). Raw UDP socket implementation for advertising; Zeroconf library for browsing.
  - **API Endpoints**:
    - `GET/PUT /api/v0/profile/me` - Manage own profile
    - `GET /api/v0/profile/{peerId}` - Fetch peer profiles (public)
    - `POST /api/v0/profile/invite` - Generate invite links
    - `GET/POST/PUT/DELETE /api/v0/contacts` - Contact CRUD
    - `POST /api/v0/contacts/from-invite` - Add contact from invite
    - `POST /api/v0/contacts/from-discovery` - Add contact from LAN discovery
    - `GET /api/v0/contacts/nearby` - Browse nearby peers
  - **Feature Flag**: `feature.identity_friends` (default: `false`). All endpoints return 404 when disabled.
  - **Dependencies**: `Zeroconf` 3.0.30 (for mDNS browsing), `Microsoft.EntityFrameworkCore.Sqlite` (for contact storage).
  - **Tests**: 90 unit tests covering ProfileService, ContactService, ContactRepository, ProfileController, ContactsController, LanDiscoveryService, and MdnsAdvertiser.

### ShareGroups, Collections & Sharing (Phases 1-2)

- **ShareGroups & Collections System** (`feature.collections_sharing`): Content organization and sharing infrastructure.
  - **ShareGroups**: User-created groups for organizing sharing audiences. `ShareGroup` and `ShareGroupMember` entities with ownership and membership management.
  - **Collections**: Content collections (ShareLists and Playlists) with `Collection` and `CollectionItem` entities. Support for ordering via `Ordinal`.
  - **Share Grants**: Capability-based sharing via `ShareGrant` with `SharePolicy` (AllowStream, AllowDownload, AllowReshare, expiry, concurrency limits).
  - **Content Resolution**: `IContentLocator` interface and implementation for resolving content IDs to local file paths and MIME types. Integrates with `IShareRepository` and `IsAdvertisable` checks.
  - **Share Tokens**: `IShareTokenService` with JWT-based capability tokens. Tokens include collection ID, capabilities, expiry, and max concurrent streams. Constant-time validation.
  - **Sharing Service**: `ISharingService` for managing groups, collections, share grants, and manifest generation.
  - **Repositories**: `IShareGroupRepository`, `ICollectionRepository`, `IShareGrantRepository` with SQLite-backed implementations via `CollectionsDbContext`.
  - **API Endpoints**:
    - `GET/POST/PUT/DELETE /api/v0/sharegroups` - ShareGroup CRUD
    - `GET/POST/PUT/DELETE /api/v0/collections` - Collection CRUD
    - `GET/POST/PUT/DELETE /api/v0/shares` - ShareGrant CRUD
    - `POST /api/v0/shares/{id}/token` - Generate share token
    - `GET /api/v0/shares/{id}/manifest` - Get collection manifest
  - **Feature Flag**: `feature.collections_sharing` (default: `false`). All endpoints return 404 when disabled. (Note: flag name in code is `CollectionsSharing`).
  - **Tests**: 67 unit tests across 5 test files covering ShareTokenService, SharingService, CollectionsController, ShareGroupsController, and SharesController.

### Streaming (Phases 3-4)

- **Streaming API** (`feature.streaming`): HTTP range request support for content streaming.
  - **Stream Session Limiting**: `IStreamSessionLimiter` with configurable concurrent stream limits per content ID.
  - **Stream Endpoint**: `GET /api/v0/streams/{contentId}` with support for HTTP range requests (`Range` header).
  - **Authentication**: Supports both token-based (share tokens) and normal user authentication.
  - **Content Resolution**: Uses `IContentLocator` (from Phase 1) to resolve content IDs to local file paths and MIME types.
  - **Session Management**: `ReleaseOnDisposeStream` wrapper ensures limiter slots are released on stream disposal.
  - **Feature Flag**: `feature.streaming` (default: `false`). Endpoint returns 404 when disabled.
  - **Tests**: Comprehensive unit tests for StreamSessionLimiter, ReleaseOnDisposeStream, ContentLocator, and StreamsController.

- **Mesh Search Improvements** (Phase 4): Enhanced mesh search with better deduplication and query limits.
  - **Query Limits**: Added query length cap (256 chars) and time cap (5 seconds) in `MeshSearchRpcHandler` to prevent abuse.
  - **Enhanced DTOs**: `MeshSearchFileDto` now includes optional `MediaKinds`, `ContentId`, and `Hash` fields for better content matching.
  - **Improved Deduplication**: `SearchResponseMerger` now uses normalized filenames (case-insensitive, path separator normalization) for cross-response deduplication.
  - **Media Kind Detection**: Automatic detection of media types (Music, Video, Image) from file extensions in `MeshSearchRpcHandler`.
  - **MeshParallelSearch Flag**: `feature.mesh_parallel_search` flag wired to enable parallel mesh search alongside Soulseek. Works with `VirtualSoulfind.MeshSearch.Enabled` (either flag can enable).

- **Relay Streaming Fallback** (Phase 5): ContentId-based streaming through relay agents.
  - **IMeshContentFetcher**: New interface and implementation for fetching content from mesh overlay network by ContentId with size and hash validation.
  - **Relay Streaming Endpoint**: `GET /api/v0/relay/streams/{contentId}` endpoint for streaming content through relay agents using ContentId instead of filename.
  - **Content Resolution**: Endpoint resolves ContentId to filename via `IContentLocator`, then uses existing relay file streaming mechanism.
  - **Feature Flag**: `feature.streaming_relay_fallback` (default: `false`). Endpoint returns 503 when disabled.
  - **Validation**: `MeshContentFetcher` performs size and SHA-256 hash validation when expected values are provided.

### Mesh Network Resilience

- **Fault-Tolerant UDP Overlay**: UDP overlay server now gracefully handles port binding failures, allowing mesh to operate behind firewalls.
  - **Graceful Degradation**: When UDP overlay port (default 50305) cannot be bound (e.g., already in use, firewall blocked), the mesh continues operating in degraded mode.
  - **Preserved Functionality**: DHT operations, relay/beacon services, and hole punching continue to function even without direct inbound UDP connections.
  - **Clear Logging**: Warning messages clearly explain degraded mode operation and which features remain available.
  - **Consistent Error Handling**: Matches the fault-tolerant pattern used by QUIC overlay servers.
  - **Use Case**: Enables mesh operation behind firewalls where port forwarding is not available, relying on outbound connections, DHT, and relay services for connectivity.

### User Interface Improvements

- **Logs Page Enhancements**: Improved log viewing experience with reduced noise and filtering capabilities.
  - **CSRF Logging Noise Reduction**: CSRF Debug logs for safe HTTP methods (GET, HEAD, OPTIONS, TRACE) and successful validations changed to Verbose level, reducing noise in default log views.
  - **Log Level Filtering**: Added filter buttons (All, Info, Warn, Error, Debug) to the logs page for easy filtering by log level.
  - **Log Count Display**: Shows count of filtered logs vs total logs (e.g., "Showing 50 of 500 logs").
  - **Improved Readability**: Users can now focus on specific log levels (warnings, errors) without scrolling through verbose debug information.

### Security & hardening (40-fixes, dev/40-fixes)

- **EnforceSecurity** (`web.enforce_security`): When `true`, enables strict auth, CORS, startup checks via `HardeningValidator`, and automatic 400 for invalid `ModelState` (`SuppressModelStateInvalidFilter = false`). Use for repeatable hardened testing.
- **Passthrough AllowedCidrs** (`web.authentication.passthrough.allowed_cidrs`): Optional CIDR allowlist for no-auth mode (e.g. `127.0.0.1/32,::1/128`) in addition to loopback. PR-03.
- **CORS** (`web.cors`): `allowed_headers`, `allowed_methods`; allowlist semantics; no `AllowAll` + `AllowCredentials`. PR-04.
- **Exception handler**: RFC 7807 `ProblemDetails`, `traceId`; in Production, generic detail (no internal leak). PR-05.
- **Dump endpoint**: Returns **501** when dump creation fails (e.g. `dotnet-dump` not on PATH, `DiagnosticsClient` failure) with instructions. `diagnostics.allow_memory_dump`, `allow_remote_dump`; admin-only, local-only when `allow_remote_dump` false. PR-06.
- **ModelState / RejectInvalidModelState**: `web.api.reject_invalid_model_state`; when Enforce, invalid payloads return 400 with consistent `ValidationProblemDetails`. PR-07.
- **MeshGateway**: Chunked POST supported; bounded body read; 413 on over-limit. PR-08.
- **Kestrel MaxRequestBodySize** (`web.max_request_body_size`): Configurable request body limit (default 10 MB). PR-09a.
- **Rate limit fed/mesh**: `Burst_federation_inbox_*`, `Burst_mesh_gateway_*` policies; `web.rate_limiting`. PR-09b.
- **QuicDataServer**: Read/limits aligned with `GetEffectiveMaxPayloadSize`. §8.
- **Metrics Basic Auth**: Constant-time comparison (`CryptographicOperations.FixedTimeEquals`); `WWW-Authenticate: Basic realm="metrics"`. §9.
- **§11 NotImplementedException gating**: Incomplete features (I2P, RelayOnly, PerceptualHasher, etc.) fail at startup or return 501 when enabled; no `NotImplementedException` crash in configured defaults.
- **ScriptService**: Async read of stdout/stderr, `WaitForExitAsync`, timeout and process kill; no `WaitForExit()` deadlock. J.

### Mesh

- **Mesh:Security** (`mesh.security`): `enforceRemotePayloadLimits`, `maxRemotePayloadSize`; safe MessagePack/JSON deserialization, overlay/transport caps.
- **Mesh:SyncSecurity** (`mesh.sync_security`): Rate limiting, quarantine, proof-of-possession, consensus, alert thresholds (T-1432–T-1435). See `docs/security/mesh-sync-security.md`.

### Anonymity / transports

- **I2PTransport**: SAM v3.1 STREAM CONNECT with `host` as I2P destination (base64 or `.b32.i2p`). `AnonymityTransportSelector` registers I2P when `AnonymityMode.I2P`. §11: enabling without SAM bridge fails at startup or 501.
- **RelayOnlyTransport**: RELAY_TCP over data overlay; `IOverlayDataPlane.OpenBidirectionalStreamAsync`; `QuicDataServer` handles `RELAY_TCP`. **`RelayPeerDataEndpoints`** (`security.adversarial.anonymity.relay_only.relay_peer_data_endpoints`): list of `host:port` for each relay’s QUIC data overlay; used when `TrustedRelayPeers` are not resolved. Required for RelayOnly until peer-id resolution. §11: enabling without endpoints/TrustedRelayPeers fails at startup or 501.

### Audio / MediaCore

- **AudioUtilities.ExtractPcmSamples**: Via ffmpeg; `ExtractPcmSamplesAsync`. Test expects `FileNotFoundException` when file missing (replacing `FeatureNotImplementedException`).

### Test infrastructure

- **test-data/slskdn-test-fixtures**: Fetch scripts, `manifest.json`, `.gitignore` for download artifacts.

### Breaking / behavior changes

- **EnforceSecurity on**: No-auth + non-loopback bind requires `allow_remote_no_auth: true` or startup fails. CORS `AllowCredentials` + wildcard origin fails startup. Dump enabled + auth disabled fails startup. `Flags.HashFromAudioFileEnabled` + Enforce fails startup (not implemented).
- **Dump**: Default `allow_memory_dump: false`; 501 when creation fails (no silent empty or 500).
- **CORS**: When enabled, require explicit `allowed_origins` when `allow_credentials: true`; no wildcard + credentials.

---

## [0.24.1-slskdn.40]

- Bump to 0.24.1-slskdn.40 (slskdn-main-linux-x64.zip).
- See `packaging/debian/changelog` and `docs/archive/DEVELOPMENT_HISTORY.md` for earlier entries.
