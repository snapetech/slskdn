# Changelog

All notable changes to slskdn are documented here. slskdn is a distribution of slskd with advanced features and experimental subsystems.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

---

## [Unreleased]

### Solid Integration: WebID and Solid-OIDC Support

- **Solid Compatibility Layer**: Optional integration with Solid (WebID + Solid-OIDC) for decentralized identity and Pod-backed metadata:
  - **WebID Resolution**: Resolve WebID profiles and extract OIDC issuer information
  - **Solid-OIDC Client ID Document**: Serves compliant JSON-LD Client ID document at `/solid/clientid.jsonld` (dereferenceable per Solid-OIDC spec)
  - **SSRF Hardening**: Comprehensive security controls for WebID/Pod fetches:
    - Host allow-list (`AllowedHosts`) - empty list denies all remote fetches by default
    - HTTPS-only enforcement (configurable `AllowInsecureHttp` for dev/test only)
    - Private IP and localhost blocking
    - Response size limits (`MaxFetchBytes`: 1MB default)
    - Timeout enforcement (`TimeoutSeconds`: 10s default)
  - **API Endpoints**: 
    - `GET /api/v0/solid/status` - Check Solid integration status
    - `POST /api/v0/solid/resolve-webid` - Resolve a WebID and extract OIDC issuers
  - **Frontend UI**: New "Solid" navigation item and settings page for WebID resolution
  - **Configuration**: New `feature.Solid` flag (default: `true`) and `solid` options block
  - **Security by Default**: Feature enabled by default but non-functional until `AllowedHosts` is explicitly configured (SSRF safety)
  - **RDF Parsing**: Uses dotNetRDF library for parsing WebID profiles (Turtle and JSON-LD formats)
- **Future Extensions** (not in MVP):
  - Full OIDC Authorization Code + PKCE flow
  - Token storage (encrypted via Data Protection)
  - DPoP proof generation
  - Pod metadata read/write (playlists, sharelists)
  - Type Index / SAI registry discovery
  - Access control (WAC/ACP) writers

### Swarm Analytics: Advanced Metrics and Reporting

- **Swarm Analytics Service**: Comprehensive analytics and reporting for swarm behavior:
  - **Performance Metrics**: Overall swarm performance (success rate, average speed, duration, bytes downloaded, chunk metrics)
  - **Peer Rankings**: Top-performing peers ranked by reputation, RTT, throughput, chunk success rate
  - **Efficiency Metrics**: Chunk utilization, peer utilization, redundancy factor, time-to-first-byte
  - **Historical Trends**: Time-series data for success rates, speeds, durations, sources used
  - **Recommendations Engine**: Automated optimization recommendations based on current performance
  - **API Endpoints**: RESTful API for accessing all analytics data (`/api/v0/swarm/analytics/*`)
  - **Frontend Dashboard**: New "Swarm Analytics" tab in System UI with visualizations and metrics
- **Advanced Discovery Service**: Enhanced peer discovery with improved algorithms:
  - **Content-Aware Matching**: Similarity scoring based on filename, size, and metadata
  - **Match Type Classification**: Exact, variant, fuzzy, and metadata-based matching
  - **Peer Ranking**: Multi-factor ranking (similarity, performance, availability, metadata confidence)
  - **Fuzzy Matching**: Improved algorithms for finding similar content variants
- **Adaptive Scheduling**: Machine learning-inspired chunk assignment optimization:
  - **Learning from Feedback**: Records chunk completion data and adapts weights dynamically
  - **Factor Correlation Analysis**: Automatically adjusts weights for reputation, throughput, and RTT based on success correlation
  - **Performance-Based Adaptation**: Adapts scheduling strategy every N completions based on recent performance
  - **Statistics Tracking**: Tracks peer learning data and provides adaptive scheduling statistics
- **Cross-Domain Swarming**: Extended swarm capabilities to non-music content domains:
  - **Domain-Aware Swarming**: Swarm downloads now work for Movies, TV, Books, and GenericFile domains
  - **Backend Selection Rules**: Domain-specific backend selection (Soulseek only for Music, mesh/DHT/torrent/HTTP for others)
- **Multi-Domain Support**: New content domain providers:
  - **Movie Domain**: IMDB ID matching, hash verification, backend selection (mesh/DHT/torrent/HTTP/local only)
  - **TV Domain**: TVDB ID matching, season/episode matching, series organization
  - **Book Domain**: ISBN-based matching, format detection (PDF, EPUB, MOBI, etc.)
  - **Domain Providers**: `IMovieContentDomainProvider`, `ITvContentDomainProvider`, `IBookContentDomainProvider` interfaces and implementations
  - **ContentDomain Enum**: Extended with `Movie`, `Tv`, and `Book` domains

### Distributed Tracing: OpenTelemetry Support

- **OpenTelemetry Integration**: Comprehensive distributed tracing infrastructure:
  - **Configuration**: New `telemetry.tracing` options in config:
    - `enabled`: Enable/disable tracing (default: false)
    - `exporter`: Exporter type - console, jaeger, or otlp (default: console)
    - `jaeger_endpoint` and `jaeger_port`: Jaeger agent configuration
    - `otlp_endpoint`: OTLP collector endpoint URL
  - **Activity Sources**: Dedicated activity sources for different components:
    - `slskdn.Transfers.MultiSource`: Swarm download operations
    - `slskdn.Mesh`: Mesh network operations (DHT queries, overlay transfers)
    - `slskdn.HashDb`: HashDb lookup and storage operations
    - `slskdn.Search`: Search operations
  - **Swarm Download Tracing**:
    - Traces entire swarm download lifecycle with tags for:
      - Download ID, filename, size, sources count, chunk size
      - Success/failure status, duration, sources used, speed
      - Individual chunk completion events with peer and performance data
  - **Mesh Network Tracing**:
    - DHT operations: `mesh.dht.store`, `mesh.dht.find_value`, `mesh.dht.find_node`
    - Tags include: key, value size, TTL, success status, nodes found
  - **HashDb Tracing**:
    - `hashdb.lookup` operations with cache hit/miss tracking
    - Tags include: lookup key, cache hit status, found status
  - **Search Tracing**:
    - `search.start` operations with query, scope, and provider information
  - **Automatic Instrumentation**: ASP.NET Core and HTTP client instrumentation enabled
  - **Exporters**: Support for console (default), Jaeger, and OTLP exporters
  - **Documentation**: Updated `config/slskd.example.yml` with telemetry configuration examples

### CI/CD Enhancements

- **Performance Regression Testing**: Automated performance benchmark execution in CI:
  - Runs BenchmarkDotNet suite on pull requests and scheduled runs
  - Compares results against baseline to detect performance regressions
  - Uploads benchmark results as artifacts for analysis
  - Reports significant performance degradation (>10%) in workflow summary
- **Load Testing**: Automated load testing with k6:
  - Tests API endpoints under sustained load (up to 100 concurrent users)
  - Ramp-up and ramp-down phases to simulate realistic traffic patterns
  - Performance thresholds: 95% of requests < 500ms, 99% < 1s, error rate < 1%
  - Uploads load test results as JSON artifacts
- **Security Scanning**: Comprehensive security analysis:
  - **CodeQL Analysis**: Static code analysis for C# and JavaScript:
    - Security and quality queries enabled
    - Results available in GitHub Security tab
  - **Container Security (Trivy)**: Docker image vulnerability scanning:
    - Scans for HIGH and CRITICAL vulnerabilities
    - Reports on base images and dependencies
  - **Dependency Scanning**: Automated vulnerability detection:
    - NuGet package vulnerability scanning (transitive dependencies included)
    - npm audit for frontend dependencies (moderate+ severity)
- **Workflow Configuration**: New `.github/workflows/ci-enhancements.yml`:
  - Runs on pull requests, pushes to master, tags, and weekly schedule
  - Parallel execution of performance, load, and security tests
  - Artifact retention for 30 days
  - Comprehensive reporting in workflow summaries

### Performance Benchmarking Suite

- **Comprehensive BenchmarkDotNet Suite**: Performance benchmarks for critical components:
  - **HashDb Benchmarks**: Database query performance, caching effectiveness:
    - Lookup performance (with/without cache, cache hits)
    - Query performance (size-based queries, sequential/parallel lookups)
    - Write performance (single and batch hash storage)
    - Statistics retrieval
  - **Swarm Benchmarks**: Swarm download operations:
    - Chunk size optimization for various file sizes (100MB-1GB) and peer counts (5-20)
    - Chunk assignment performance (sequential and parallel)
    - Peer selection based on metrics (throughput, queue length, free slots, reputation)
  - **API Benchmarks**: API endpoint performance:
    - GET endpoint performance (session, application state, HashDb stats, paginated jobs)
    - POST endpoint performance (create search)
    - Concurrent request handling (10, 50, 100 concurrent requests)
  - **Transport Benchmarks**: Already existed, now part of comprehensive suite
  - **Benchmark Project**: New `tests/slskd.Tests.Performance/` project with proper BenchmarkDotNet configuration
  - **Documentation**: `README.md` with usage instructions, performance targets, and CI integration guidance

### Developer Documentation

- **Enhanced Contributing Guide**: Comprehensive developer resources:
  - **Development Setup**: Prerequisites, initial setup, build instructions
  - **Development Workflow**: Feature branch workflow, testing, committing
  - **Code Style Guidelines**: C# and React style guidelines with examples
  - **Copyright Headers**: Policy for new vs existing files, fork-specific directories
  - **Testing**: Running tests, writing tests, test organization
  - **Debugging**: Backend and frontend debugging instructions, common scenarios
  - **Project Structure**: Overview of directory layout
  - **Code Review Checklist**: Pre-PR checklist
  - **Getting Help**: Community resources
- **API Documentation Guide**: Complete API reference:
  - **Base URL and Versioning**: API structure and versioning scheme
  - **Authentication**: Cookie, JWT, and API key authentication methods
  - **Response Formats**: Success and error (ProblemDetails) response formats
  - **Complete Endpoint Reference**: All API endpoints organized by category:
    - Core APIs (Application, Server, Session)
    - Search APIs (Searches, Search Actions)
    - Transfer APIs (Downloads, Uploads)
    - Multi-Source/Swarm APIs (Swarm Downloads, Tracing, Fairness)
    - Job APIs
    - User APIs (Users, User Notes)
    - Pod APIs (Pods, Pod Messages)
    - Collections & Sharing APIs
    - Mesh APIs
    - Hash Database APIs
    - Wishlist APIs
    - Capabilities APIs
    - Streaming APIs
    - Library Health APIs
    - Options & Configuration
  - **Common Patterns**: Pagination, filtering, sorting
  - **Error Handling**: HTTP status codes and error responses
  - **Rate Limiting**: Rate limit information and headers
  - **API Discovery**: How to find endpoints in source code
  - **Frontend API Libraries**: Usage of API client libraries
  - **WebSocket/SignalR**: Real-time update mechanisms
  - **Code Examples**: curl and JavaScript examples
  - **Best Practices**: API usage guidelines

### User Documentation

- **Getting Started Guide**: Comprehensive guide for new users:
  - Installation instructions for all platforms (Linux, macOS, Windows, Docker, package managers)
  - Initial configuration (password, directories, Soulseek credentials)
  - Basic usage (searching, downloading, wishlist)
  - Security best practices
  - Next steps and community resources
- **Troubleshooting Guide**: Complete troubleshooting reference:
  - Connection issues (Soulseek, Mesh/Pod networks)
  - Download problems (stuck, slow, failing downloads)
  - Performance issues (high CPU/memory usage)
  - Configuration problems (saving, validation)
  - Web interface issues (loading, authentication)
  - Feature-specific troubleshooting (swarm, wishlist, collections, streaming)
  - Log analysis and debug techniques
  - Community support resources
- **Advanced Features Walkthrough**: Detailed guide for advanced features:
  - Swarm downloads (operation, monitoring, optimization)
  - Scene ↔ Pod bridging (unified search, privacy)
  - Collections & sharing (creation, sharing, backfill)
  - Streaming (operation, limitations)
  - Wishlist & background search
  - Auto-replace stuck downloads
  - Smart search ranking
  - Multiple download destinations
  - Job management & monitoring
  - Performance tuning and configuration tips
- **Documentation Index**: Updated `docs/README.md` with links to all new guides

### Swarm Performance Tuning

- **Adaptive Chunk Size Optimization**: Intelligent chunk sizing for swarm downloads:
  - **Automatic Optimization**: Chunk size automatically optimized based on file size, peer count, and performance metrics
  - **Heuristics**:
    - Base calculation targets 2 chunks per peer for optimal parallelism (4-200 chunks total)
    - Throughput-based adjustment: larger chunks for high throughput (>5 MB/s), smaller for low (<1 MB/s)
    - Latency-based adjustment: smaller chunks for high latency (>500ms), larger for low (<100ms)
  - **Constraints**: 64KB minimum, 10MB maximum, aligned to 64KB boundaries
  - **Integration**: Automatically used when chunk size not explicitly specified in download request
  - **Service**: `IChunkSizeOptimizer` interface with `ChunkSizeOptimizer` implementation
  - **Fallback**: Gracefully falls back to default 512KB if optimizer unavailable

### Real-time Swarm Visualization

- **Swarm Visualization Dashboard**: Comprehensive real-time visualization for active swarm downloads:
  - **Job Overview**: Real-time metrics including chunks completed/total, active workers, chunks/second rate, estimated time remaining, and overall progress bar
  - **Peer Contributions Table**: Detailed peer performance analysis:
    - Chunks completed, failed, and timed out per peer
    - Bytes served per peer
    - Success rate calculation with color-coded progress indicators (green ≥80%, yellow ≥50%, red <50%)
    - Peers sorted by contribution (bytes served, then chunks completed)
  - **Chunk Assignment Heatmap**: Visual grid representation of chunk completion:
    - Green squares for completed chunks
    - Gray squares for pending chunks
    - Tooltips showing chunk index and status
    - Auto-scaling grid layout based on total chunks
    - Legend for color coding
  - **Performance Metrics**: Trace summary data including:
    - Total events count
    - Duration calculation (parsed from TimeSpan format)
    - Rescue mode indicator (orange warning icon when rescue invoked)
    - Bytes by source/backend breakdown (sorted by contribution)
  - **Integration**: Accessible via "View Details" button on active swarm jobs in Jobs dashboard
  - **Modal Interface**: Large modal dialog for detailed visualization
  - **Auto-refresh**: Updates every 2 seconds for real-time monitoring
  - **API Integration**: Uses `/api/v0/multisource/jobs/{jobId}` for job status and `/api/v0/traces/{jobId}/summary` for detailed peer contributions

### Advanced Search UI Enhancements

- **Quality Presets**: Quick filter buttons in Advanced Filters modal:
  - "High Quality (320kbps+)" - Sets minimum bitrate to 320kbps, lossy only
  - "Lossless Only" - Filters for lossless files with min 16-bit depth and 44.1kHz sample rate
  - "Clear Quality" - Resets all quality-related filters
- **Sample Rate Filtering**: Added minimum sample rate (Hz) input field in Advanced Filters modal
  - Supports `minsr:` filter syntax (e.g., `minsr:44100`)
  - Filters files by sample rate when specified
- **Format/Codec Filtering**: Added file extension filtering in Advanced Filters modal
  - Supports filtering by file extensions (e.g., flac, mp3, wav, m4a)
  - Supports `ext:` filter syntax (e.g., `ext:flac,mp3`)
  - Space or comma-separated extensions
- **Enhanced Source Selection UI**: Improved provider selection for Scene ↔ Pod Bridging:
  - More prominent display with background highlight
  - Icons for Pod/Mesh (sitemap) and Soulseek Scene (globe)
  - Clear labels: "Pod/Mesh" and "Soulseek Scene"
  - Warning message when no sources selected
  - Better visual hierarchy and spacing

### Enhanced Job Management UI

- **Jobs Dashboard** (`/system/jobs`): Comprehensive job management interface with:
  - **Analytics Overview**: Total jobs, active jobs, completed jobs, and job type breakdown
  - **Active Swarm Downloads**: Real-time display of multi-source downloads with:
    - Progress bars and percentage completion
    - Active sources count
    - Download speed (chunks/second)
    - Estimated time remaining
    - Auto-refresh every 5 seconds
  - **Job List**: Filterable and sortable table of all jobs (discography, label crate) with:
    - Filter by type (discography, label crate)
    - Filter by status (pending, running, completed, failed)
    - Sort by created date, status, or ID (ascending/descending)
    - Pagination support (20 jobs per page)
    - Progress visualization for releases (completed/total/failed)
    - Color-coded status indicators
  - **API Integration**: Full integration with `/api/jobs` endpoint supporting filtering, sorting, and pagination

### Testing Expansion

- **Bridge Protocol Validation Tests**: Comprehensive protocol format validation for `SoulseekProtocolParser`:
  - Edge case handling: empty strings, Unicode characters, long queries (1000+ chars), special characters
  - Error handling: invalid message lengths, truncated messages
  - Roundtrip validation: write-then-read verification for all message types
  - Response format validation: login and search response structure
  - 13 tests covering protocol compatibility and robustness
- **Bridge Performance Tests**: Load and performance benchmarks:
  - Concurrent operations: 10 parallel streams (1000 msg/s throughput)
  - Latency measurements: average, P95, P99 percentiles (<10ms average)
  - Large message handling: 10KB queries
  - High-volume scenarios: 10,000 small messages (>5000 msg/s)
  - Memory efficiency: <5KB per message with proper cleanup
  - Rapid connect/disconnect: 100 cycles (>50 ops/s)
  - 7 performance tests validating scalability
- **Protocol Contract Tests**: Enhanced Soulseek protocol compliance tests:
  - `Should_Login_And_Handshake`: Improved assertions and graceful skipping
  - `Should_Send_Keepalive_Pings`: Reduced wait time, connection state verification
  - `Should_Handle_Disconnect_And_Reconnect`: Disconnect detection and reconnection verification
  - All 6 tests passing (gracefully skip when Soulfind unavailable)
- **Bridge E2E Test Infrastructure**: Full instance test harness:
  - `SlskdnFullInstanceRunner`: Starts actual slskdn process for TCP listener tests
  - Auto-discovers binary from build output or `SLSKDN_BINARY_PATH` environment variable
  - Generates test configuration with bridge enabled
  - Graceful degradation: tests skip with helpful instructions when binary unavailable
  - 5 Bridge E2E tests updated to use full instance when available

### Scene ↔ Pod Bridging

- **Scene ↔ Pod Bridging** (`feature.scene_pod_bridge`): Unified search experience aggregating results from Pod/Mesh and Soulseek Scene networks.
  - **Unified Search**: Single search query hits both Pod/Mesh and Soulseek Scene providers in parallel, with automatic result merging and deduplication.
  - **Provenance Badges**: Clear visual indicators (POD, SCENE, POD+SCENE) showing result source in search results.
  - **Intelligent Action Routing**: 
    - **Pod Results**: Downloads from remote mesh peers if not available locally, or streams via streaming API. Falls back to mesh directory lookup if peer ID missing.
    - **Scene Results**: Uses standard Soulseek download pipeline.
  - **Provider Selection**: UI checkboxes to select which providers to search (Pod and/or Scene).
  - **Remote Pod Downloads**: Full implementation of downloading Pod content from remote mesh peers when not available locally, with proper error handling (404 for peer not found, 502 for fetch failures).
  - **Privacy Protection**: Pod peer identities never exposed to Soulseek Scene network. No auto-advertising of Pod content to Scene.
  - **API Endpoints**:
    - `POST /api/v0/searches/{searchId}/items/{itemId}/download` - Download a search result item (routes based on source)
    - `POST /api/v0/searches/{searchId}/items/{itemId}/stream` - Stream a pod result (returns 400 for scene results)
  - **Feature Flag**: `feature.scene_pod_bridge` (default: `true`). When disabled, search behaves exactly as before.
  - **Deduplication**: Results deduplicated by hash (if available) or normalized filename + size. Pod results preferred when duplicates found.
  - **Tests**: 8 integration tests covering remote pod downloads, fallback to mesh directory, error handling, and stream URL generation. All E2E tests updated for new functionality.
  - **Documentation**: Comprehensive feature documentation in `docs/FEATURES.md` covering architecture, privacy guarantees, configuration, and use cases.

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
- **Phase 12 Database Poisoning Protection**: ✅ **100% COMPLETE** (Jan 2026). All 10 tasks (T-1430 through T-1439) implemented including Ed25519 signature verification, reputation integration, rate limiting, automatic quarantine, proof-of-possession challenges, cross-peer consensus, security metrics, comprehensive tests, and documentation. See `docs/security/mesh-sync-security.md` and `docs/security/database-poisoning-tasks.md` for details.

### Anonymity / transports

- **I2PTransport**: SAM v3.1 STREAM CONNECT with `host` as I2P destination (base64 or `.b32.i2p`). `AnonymityTransportSelector` registers I2P when `AnonymityMode.I2P`. §11: enabling without SAM bridge fails at startup or 501.
- **RelayOnlyTransport**: RELAY_TCP over data overlay; `IOverlayDataPlane.OpenBidirectionalStreamAsync`; `QuicDataServer` handles `RELAY_TCP`. **`RelayPeerDataEndpoints`** (`security.adversarial.anonymity.relay_only.relay_peer_data_endpoints`): list of `host:port` for each relay’s QUIC data overlay; used when `TrustedRelayPeers` are not resolved. Required for RelayOnly until peer-id resolution. §11: enabling without endpoints/TrustedRelayPeers fails at startup or 501.

### Audio / MediaCore

- **AudioUtilities.ExtractPcmSamples**: Via ffmpeg; `ExtractPcmSamplesAsync`. Test expects `FileNotFoundException` when file missing (replacing `FeatureNotImplementedException`).

### Multi-Source Downloads & Swarm Scheduling

- **Chunk Reassignment Logic (T-1405)**: Enhanced swarm download orchestration with automatic chunk reassignment from degraded peers.
  - **Assignment Tracking**: `IChunkScheduler` now tracks active chunk assignments via `RegisterAssignment`/`UnregisterAssignment` methods
  - **Degradation Handling**: `HandlePeerDegradationAsync` returns list of chunk indices to reassign when peer performance degrades
  - **Automatic Re-queuing**: `SwarmDownloadOrchestrator` detects degraded peers and automatically re-queues their assigned chunks for reassignment to better peers
  - **Implementation**: Works with both `ChunkScheduler` and `MediaCoreChunkScheduler` for cost-based and content-aware scheduling
  - **Benefits**: Improves download reliability by quickly shifting work away from underperforming peers to maintain optimal swarm performance

- **Jobs API Enhancements (T-1410)**: Enhanced `/api/jobs` endpoint with pagination, sorting, and improved filtering.
  - **Pagination**: `limit` and `offset` query parameters (default limit: 100)
  - **Sorting**: `sortBy` parameter supports `status`, `created_at`, or `id`; `sortOrder` parameter supports `asc`/`desc` (default: `desc`)
  - **Default Sorting**: Jobs sorted by `created_at` descending (newest first) when no sort specified
  - **Enhanced Response**: Includes `total`, `limit`, `offset`, and `has_more` fields for pagination metadata
  - **Enhanced Job Objects**: Job objects now include `created_at` timestamp and `progress` object with `releases_total`, `releases_done`, `releases_failed` for better sorting and filtering
  - **Use Case**: Enables efficient job management in UIs with large numbers of jobs, supporting pagination and sorting by status or creation date

### Legacy Client Compatibility Bridge

- **Bridge Proxy Server Testing (T-851)**: Expanded test coverage for bridge protocol parser.
  - **Additional Unit Tests**: Added 7 new edge case tests for `SoulseekProtocolParser` covering:
    - Empty string handling (username, password, query)
    - Long filename handling (1000+ characters)
    - Invalid message length handling
    - Message roundtrip validation (write then read)
    - Empty file/room list handling
  - **Test Coverage**: All 15 protocol parser tests passing (8 original + 7 new)
  - **Benefits**: Improved confidence in protocol parser robustness and edge case handling

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
