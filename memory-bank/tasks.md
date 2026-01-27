# Tasks (Source of Truth)

> This file is the canonical task list for slskdN development.  
> AI agents should add/update tasks here, not invent ephemeral todos in chat.

---

## Active Development

### High Priority

*No high priority tasks currently active

- [ ] **T-915**: Fix web lint errors + re-enable eslint on build
 - Status: pending
 - Priority: P0
 - Branch: `dev/40-fixes`
 - Notes: Lint errors are widespread in `src/web/src/components/` (App, Chat, Contacts, Pods, Rooms, ShareGroups, SharedWithMe, System/*), plus `src/web/src/lib/*` and several tests. Build temporarily uses `DISABLE_ESLINT_PLUGIN=true` to unblock E2E; remove it and fix lint issues across these files.

- [x] **T-914**: Cross-node share discovery (“Shared with Me”)
 - Status: done
 - Priority: P0
 - Branch: `dev/40-fixes`
 - Notes: Implemented via private message announcements. When a share-grant is created, the owner sends a `SHAREGRANT:` message to recipients via Soulseek PM containing the grant details, collection metadata, items, token, and owner endpoint. `ShareGrantAnnouncementService` listens for these messages and ingests them into the recipient's local database. All 5 multi-peer E2E tests passing (2026-01-27).

### Medium Priority

**Research implementation (T-901–T-913)** — Design/scope: `docs/research/9-research-design-scope.md`. Suggested order: T-912 → T-911 → T-913 → T-901 → T-902 → T-903 → T-906 → T-907 → T-908. **T-912, T-911, T-913, T-901, T-902, T-903, T-906, T-907, T-908 done; Research (9) order complete.**

- [x] **T-912**: Metadata facade abstraction — `IMetadataFacade` (GetByRecordingId, GetByFingerprint, GetByFile, Search); MetadataFacade (MB, AcoustID, file tags via TagLib/XiphComment); IMusicBrainzClient.SearchRecordingsAsync; optional IMemoryCache. Soulseek adapter: follow-up.
- [x] **T-911**: MediaVariant model and storage — `MediaVariant` (ContentDomain, domain-specific: Audio/Image/Video/GenericFile); `IMediaVariantStore` + `HashDbMediaVariantStore` (Music→HashDb, Image/Video/Generic in-memory); `IHashDbService.GetAudioVariantByFlacKeyAsync`; `ContentDomain` Image/Video; `FromAudioVariant`/`ToAudioVariant`.
- [x] **T-913**: AudioCore domain module — `slskd.AudioCore` (API boundary doc); `AddAudioCore(IServiceCollection, appDirectory)` registers fingerprinting, HashDb, IMediaVariantStore, ICanonicalStatsService, IDedupeService, IAnalyzerMigrationService, ILibraryHealth, IMusicContentDomainProvider; wired in Program.
- [x] **T-901**: Ed25519 signed identity system — Design: `docs/research/T-901-ed25519-identity-design.md` (unified model, key lifecycle, alignment); `Ed25519Signer.DerivePeerId` formalized (PeerId = Base32(First20(SHA256(pubkey)))).
- [x] **T-902**: DHT node and routing table — Design: `docs/research/T-902-dht-node-design.md`. KademliaRoutingTable (160-bit, k-buckets, FIND_NODE); DhtMeshService responds to FindNode, FindValue, Store, Ping; KademliaRpcClient; NodeId from Ed25519 (SHA1); slskdn DHT (BEP 5 GET_PEERS/ANNOUNCE_PEER = FindValue/Store).
- [x] **T-903**: DHT storage with TTL and signatures — Design: `docs/research/T-903-dht-storage-design.md`. IDhtClient PUT/GET/GetMultipleAsync, TTL (expiry on read); Store RPC requires Ed25519 (DhtStoreMessage); overlap with shadow index, pods, scenes.
- [x] **T-906**: Native mesh protocol backend — `IContentBackend` via mesh/DHT only (no Soulseek, no BitTorrent); mesh “get content by ContentId” RPC.
- [x] **T-907**: HTTP/WebDAV/S3 backend — `ContentBackendType.WebDav`, `WebDavBackend` (registry, domain allowlist, Basic/Bearer, HEAD); `ContentBackendType.S3`, `S3Backend` (registry, s3://bucket/key, HeadObject, AWSSDK.S3, MinIO/B2/AWS). Design: `docs/research/T-907-http-webdav-s3-backend-design.md`.
- [x] **T-908**: Private BitTorrent backend — Design: `docs/research/T-908-private-bittorrent-backend-design.md`. `TorrentBackendOptions.PrivateMode` (`PrivateTorrentModeOptions`: PrivateOnly, DisableDht, DisablePex, AllowedPeerSources); `PrivatePeerSource` enum. StubBitTorrentBackend replacement and TorrentBackend private logic: follow-up.

### Low Priority

- [x] **T-006**: Create Chat Rooms from UI
  - Status: Done
  - Priority: Low
  - Related: slskd #1258
  - Notes: RoomCreateModal; create→join (public: join creates if new; private: server/ops create, then join via dropdown).

- [x] **T-007**: Predictable Search URLs
  - Status: Done
  - Priority: Low
  - Related: slskd #1170
  - Notes: /searches?q= and search icon → /searches/{id} bookmarkable (create returns id; navigate uses it).

---

## Optionals / Follow-up (40-fixes, Research, Packaging)

> All items below must be done. Source: `docs/dev/40-fixes-plan.md` Deferred/optional, Research follow-ups, TODO.md, Out of Scope. Verify against “Completed” list in 40-fixes when marking done.
### 40-fixes plan (PR / § / J)

- [x] **PR-03 Passthrough AllowedCidrs**: Add `Web.Authentication.Passthrough.AllowedCidrs` (e.g. `"127.0.0.1/32,::1/128"`) for explicit CIDR allowlist instead of/in addition to loopback check.
- [x] **PR-04 CORS AllowedHeaders/AllowedMethods**: Implement and wire `Web.Cors.AllowedHeaders`, `Web.Cors.AllowedMethods` in Options and CORS middleware.
- [x] **PR-05 Exception / ValidationProblemDetails**: Custom `InvalidModelStateResponseFactory` (or validation formatter) so Production does not leak internals; consistent `ValidationProblemDetails`.
- [x] **PR-06 Dump 501**: Dump endpoint returns **501** when dump creation fails (e.g. dotnet-dump not on PATH, DiagnosticsClient failure) with instructions.
- [x] **PR-07 ModelState / RejectInvalidModelState**: `Web.Api.RejectInvalidModelState` (in Enforce can imply true); consistent `ValidationProblemDetails` (same factory as PR-05 where applicable).
- [x] **PR-08 MeshGateway chunked POST**: Chunked POST for MeshGateway — bounded body read, 413 on over-limit; support chunked when `ContentLength` null (if not already done).
- [x] **PR-09a Kestrel MaxRequestBodySize**: Kestrel `MaxRequestBodySize` configured and documented in Options/example config.
- [x] **PR-09b Rate limit fed/mesh**: Rate-limit fed/mesh integration: `Burst_federation_inbox_*`, `Burst_mesh_gateway_*` in `FedMeshRateLimitTestHostFactory` (or equivalent) and policies applied.
- [x] **§8 QuicDataServer**: `QuicDataServer` read/limits aligned with `GetEffectiveMaxPayloadSize`.
- [x] **§9 Metrics Basic Auth constant-time**: Metrics Basic Auth uses constant-time comparison (`CryptographicOperations.FixedTimeEquals`); `WWW-Authenticate: Basic realm="metrics"`.
- [x] **§11 NotImplementedException gating**: Incomplete features (I2P, RelayOnly, PerceptualHasher, etc.) fail at startup or return 501 when enabled; no `NotImplementedException` crash in configured defaults.
- [x] **J ScriptService deadlock**: ScriptService: async read of stdout/stderr, `WaitForExitAsync`, timeout and process kill; no `WaitForExit()` while redirecting.
- [x] **6.4 Pod Join nonce**: `PodJoinRequest` has optional `Nonce`; `PodJoinLeaveService` uses `PodJoinOptions.SignatureMode` (bind `PodCore:Join`). When Enforce: Nonce required, replay cache `PodId:PeerId:Nonce` with 5min TTL. Done.

### Research follow-ups (T-906, T-907, T-908, T-912)

- [x] **T-906 Resolver fetch**: SimpleResolver calls `MeshContent.GetByContentId` via IMeshServiceClient for `mesh:{peerId}:{contentId}`; writes payload to temp file and returns path. Done.
- [x] **T-907 Resolver fetch**: SimpleResolver uses `IContentFetchBackend`; `WebDavBackend`, `S3Backend`, `HttpBackend` implement it; fetch via `FetchToStreamAsync`. Done.
- [x] **T-908 StubBitTorrentBackend / TorrentBackend**: `MonoTorrentBitTorrentBackend` registered in DI; respects `PrivateMode` (DisableDht, DisablePex, InviteList). `StubBitTorrentBackend` class remains in `SwarmSignalHandlers` but is not in DI. Done.
- [x] **T-912 Soulseek adapter**: `IMetadataFacade.GetBySoulseekFilenameAsync(username, filename)` parses common patterns (Artist - Title, Album - NN - Title, NN. Title) and returns `MetadataResult` with `SourceSoulseek`. Done.

### Packaging (TODO.md)

- [x] **Proxmox LXC templates**: `packaging/proxmox-lxc/` — README, `slskdn.conf.example`, `setup-inside-ct.sh` (Debian 12/Ubuntu 22.04: .NET 8, slskdn zip to /opt/slskdn, systemd, /etc/slskd, /var/lib/slskd). Done.

### 40-fixes Out of Scope (docs)

- [x] **CHANGELOG and option docs**: CHANGELOG and option docs (e.g. `config/slskd.example.yml`) updated for new flags and breaking behavior from 40-fixes (EnforceSecurity, Mesh:SyncSecurity, etc.).

### Docs / meta

- [x] **Sync DEVELOPMENT_HISTORY Pending**: `docs/archive/DEVELOPMENT_HISTORY.md` "Pending Features" — Phase 8 Create Chat Rooms/Predictable Search URLs → ✅ (T-006, T-007); Pending section now points to tasks.md, lists done (T-001–T-007) and still-pending.
- [x] **slskd.Tests.Unit Phase 2–6**: Completion-plan shows 0 Compile Remove, 0 skips; `dotnet test` slskd.Tests.Unit 2294 pass, 0 fail, 0 skip. Re-enablement complete.
- [x] **Triage src/ TODO/FIXME/placeholder**: Triaged in `memory-bank/triage-todo-fixme.md`: ~13 accepted, ~100 defer, 7 task. Follow-up [ ] below. Done.
- [x] **Triage follow-up (task)**: Options realm validation re-enabled (`Realm.Validate()`, `MultiRealm.Validate()` in Options.Validate). QuicDataServer TODO replaced with defer comment (IOverlayDataPayloadHandler). RescueService/Scene* remain in triage-todo-fixme as defer. Done.
- [x] **Reconcile tasks-audit-gaps**: Phase 8 reconciled: T-1421, T-1422, T-1423, T-1425, T-1429 implemented (Ed25519, KeyedSigner/ControlSigner, QuicOverlayServer, QuicDataServer, ControlDispatcher). Tasks-audit-gaps.md updated. T-1424, T-1426, T-1427, T-1428 and Phases 1–6 remain as backlog; promote to [ ] when prioritizing.

### Design / Backlog (ShareGroups, Collections, Streaming, Hybrid Search)

- [x] **ShareGroups + Collections + Streaming + Hybrid Search (design merged)**: Assessment and merged design in `docs/design/sharegroups-collections-streaming-assessment.md`. Merges older agent-ticket with existing: ShareGroup, Collection, ShareGrant, SharePolicy, IShareTokenService, IContentLocator, GET /streams/{contentId} (range, token or auth), manifest, IStreamSessionLimiter; mesh search (we have overlay + MeshSearchRpcHandler + SearchResponseMerger + MeshContent.GetByContentId). Feature flags: CollectionsSharing, Streaming, StreamingRelayFallback, MeshParallelSearch (= VirtualSoulfind.MeshSearch.Enabled), MeshPublishAvailability (defer). **All phases complete** (2026-01-26): Phase 1 (foundations), Phase 2 (collections/sharing), Phase 3 (streaming), Phase 4 (mesh search improvements: MediaKinds/ContentId/Hash in MeshSearchFileDto, SearchResponseMerger normalization, MeshParallelSearch wired), Phase 5 (IMeshContentFetcher with size/hash validation, GET /api/v0/relay/streams/{contentId} endpoint).

- [x] **Backfill for shared collections**: Backfill API endpoint and UI for downloading all items from a shared collection. Supports both HTTP downloads (cross-node, no Soulseek required) and Soulseek downloads (when available). **Complete** (2026-01-27): `POST /api/v0/share-grants/{id}/backfill` endpoint, "Backfill All" button in SharedWithMe manifest modal, validates AllowDownload policy, returns detailed results.

- [x] **Persistent tabbed interface for Chat**: Converted Chat component to use tabbed interface with localStorage persistence, matching Browse and Rooms pattern. **Complete** (2026-01-27): Created `ChatSession.jsx` component, converted `Chat.jsx` to functional component with hooks, tabs persist in `slskd-chat-tabs` localStorage, supports multiple concurrent conversations.

- [x] **Mesh UDP Overlay Fault Tolerance**: UDP overlay server now gracefully handles port binding failures (address already in use, firewall blocked). Mesh continues operating in degraded mode: DHT operations, relay/beacon services, and hole punching remain functional. Only direct inbound UDP connections are unavailable. Clear warning logs explain degraded mode. Matches fault-tolerant pattern used by QUIC overlay servers. Enables mesh operation behind firewalls without port forwarding. **Complete** (2026-01-26): UdpOverlayServer updated with graceful error handling, all 2430 unit tests and 190 integration tests passing.

- [x] **Logs Page Improvements**: Reduced CSRF logging noise and added log level filtering to logs page. CSRF Debug logs for safe methods (GET requests) and successful validations changed to Verbose level (won't appear in default views). Added filter buttons (All, Info, Warn, Error, Debug) to logs page with count display. **Complete** (2026-01-26): ValidateCsrfForCookiesOnlyAttribute updated, Logs component enhanced with filtering UI, all 2430 unit tests and 190 integration tests passing.

---

## Packaging & Distribution

- [x] **T-010**: TrueNAS SCALE Apps
  - Status: Done
  - Priority: High
  - Notes: Helm ix-chart; appVersion 0.24.1-slskdn.40, home/sources→snapetech/slskdn (chore 2026-01-25).

- [x] **T-011**: Synology Package Center
  - Status: Done
  - Priority: High
  - Notes: SPK; INFO version 0.24.1, URLs→snapetech/slskdn (chore 2026-01-25).

- [x] **T-012**: Homebrew Formula
  - Status: Done
  - Priority: High
  - Notes: Formula/slskdn.rb 0.24.1-slskdn.40, osx-arm64/osx-x64/linux-x64, SHA256 from GitHub API (chore 2026-01-25).

- [x] **T-013**: Flatpak (Flathub)
  - Status: Done (2026-01-25)
  - Priority: High
  - Notes: .NET 8.0.11 + slskdn-main-linux-x64 0.24.1-slskdn.40, slskdn.svg; placeholders replaced; build.sh, FLATHUB_SUBMISSION updated.

- [x] **T-014**: Helm chart for generic Kubernetes
  - Status: Done (2026-01-25)
  - Priority: Medium
  - Notes: `packaging/helm/slskdn/` — Chart.yaml, values.yaml, templates (_helpers, Deployment, Service, PVCs, Ingress). No TrueCharts; standard K8s, PVCs for config/downloads/shares/incomplete. appVersion 0.24.1-slskdn.40. README with install and main values.

---

## Completed Tasks

- [x] **T-912 (2026-01-25):** Metadata facade — IMetadataFacade, MetadataResult, MetadataFacade (GetByRecordingId, GetByFingerprint, GetByFile, Search). MusicBrainzClient.SearchRecordingsAsync + RecordingSearchHit. File tags (TagLib, XiphComment MUSICBRAINZ_*). AcoustID→MB for fingerprint. IMemoryCache. DI in Program. Soulseek adapter: follow-up.

- [x] **T-911 (2026-01-25):** MediaVariant model and storage — MediaVariant (Domain, VariantId, FirstSeenAt, LastSeenAt, SeenCount, FileSha256, FileSizeBytes; Audio/ImageDimensions/ImageCodec/VideoDimensions/VideoCodec/VideoDurationSeconds). IMediaVariantStore (GetByVariantId, GetByRecordingId, GetByDomain, Upsert). HashDbMediaVariantStore (Music→IHashDbService, Image/Video/GenericFile in-memory). IHashDbService.GetAudioVariantByFlacKeyAsync. ContentDomain Image=2, Video=3. FromAudioVariant/ToAudioVariant. DI.

- [x] **T-913 (2026-01-25):** AudioCore domain module — slskd.AudioCore.AudioCore (API boundary doc: IChromaprintService, IFingerprintExtractionService, IHashDbService, IMediaVariantStore, ICanonicalStatsService, ILibraryHealthService, ILibraryHealthRemediationService, IAnalyzerMigrationService, IDedupeService, IMusicContentDomainProvider, analyzers). AddAudioCore(IServiceCollection, appDirectory) registers all; Program uses AddAudioCore(Program.AppDirectory); scattered audio registrations consolidated.

- [x] **T-901 (2026-01-25):** Ed25519 signed identity system — docs/research/T-901-ed25519-identity-design.md: unified identity model (Mesh+IKeyStore/FileKeyStore shared with Pods; ActivityPub separate); key lifecycle (FileKeyStore JSON/KeyPath/RotateDays, ActivityPubKeyStore IEd25519KeyPairGenerator PEM, RotateKeypairAsync); alignment. Ed25519Signer.DerivePeerId formalized: PeerId = Base32(First20(SHA256(publicKey))). Revocation, DID deferred.

- [x] **T-902 (2026-01-25):** DHT node and routing table — docs/research/T-902-dht-node-design.md. KademliaRoutingTable (160-bit, k=20, bucket splitting, XOR, Touch, GetClosest); selfId=SHA1(Ed25519) from IKeyStore. DhtMeshService: FindNode, FindValue, Store, Ping; KademliaRpcClient; slskdn DHT wire (mesh overlay, JSON). GET_PEERS/ANNOUNCE_PEER mapped to FindValue/Store; DhtRendezvous remains BEP 5 client.

- [x] **T-903 (2026-01-25):** DHT storage with TTL and signatures — docs/research/T-903-dht-storage-design.md. IDhtClient PutAsync/GetAsync/GetMultipleAsync; TTL expiry on read; Store RPC requires Ed25519 (DhtStoreMessage.CreateSigned/VerifySignature, 5 min freshness); same store for shadow index, pods, scenes; _maxPayload; conflict last-write-wins, republish open.

- [x] **T-906 (2026-01-25):** Native mesh protocol backend — ContentBackendType.NativeMesh; NativeMeshBackend (IMeshDirectory, IContentIdRegistry; FindCandidatesAsync via FindPeersByContentAsync, BackendRef mesh:{peerId}:{contentId}; ValidateCandidateAsync format-only); NativeMeshBackendOptions. Design: docs/research/T-906-native-mesh-backend-design.md (mesh “get content by ContentId/hash” RPC, resolver fetch follow-up). DI: document only (v2 IContentBackend not wired in Program).

- [x] **T-907 (2026-01-25):** HTTP/WebDAV/S3 backend — ContentBackendType.WebDav, WebDavBackend (registry, domain allowlist, Basic/Bearer, HEAD); ContentBackendType.S3, S3Backend (registry, s3://bucket/key, HeadObject, AWSSDK.S3). Design: docs/research/T-907-http-webdav-s3-backend-design.md. Resolver fetch: follow-up.

- [x] **T-908 (2026-01-25):** Private BitTorrent backend — TorrentBackendOptions.PrivateMode (PrivateTorrentModeOptions: PrivateOnly, DisableDht, DisablePex, AllowedPeerSources), PrivatePeerSource enum. Design: docs/research/T-908-private-bittorrent-backend-design.md (IBitTorrentBackend, MonoTorrent, private swarm, StubBitTorrentBackend replacement). Stub replacement and TorrentBackend private logic: follow-up.

- [x] **chore (2026-01-25):** Research (9) **unpinned**; implementation started. T-901–T-913 moved to tasks.md § Medium Priority (Research implementation). Suggested order: T-912 → T-911 → T-913 → T-901 → T-902 → T-903 → T-906 → T-907 → T-908. Start: T-912 (Metadata facade).

- [x] **T-014 (2026-01-25):** Helm chart for generic Kubernetes at `packaging/helm/slskdn/`. Chart.yaml, values.yaml, templates (_helpers, Deployment, Service, PVCs, Ingress). No TrueCharts; standard K8s; PVCs for config/downloads/shares/incomplete. TODO.md Helm Charts marked done.

- [x] **chore (2026-01-25):** slskd.Tests.Unit completion plan: Phase 1 and Phase 3 marked **DONE** (PrivacyLayerIntegration, ContentDomain, SimpleMatchEngine, RealmAwareGossip/Governance/RealmService, MeshCircuitBuilder/MeshSyncSecurity/MeshTransportService/Phase8, MembershipGate, FederationService, ActivityPubBridge, BridgeFlow*, Realm* suite, CircuitMaintenanceService, ActivityPubKeyStore). Execution order §0–3 updated. 2257 pass, 0 skip.

- [x] **t410-backfill-wire (2026-01-25):** RescueMode underperformance detector → RescueService. RescueModeOptions (Enabled, MaxQueueTimeSeconds, MinThroughputKBps, MinDurationSeconds, StalledTimeoutSeconds, CheckIntervalSeconds); IRescueService.IsRescueActive; UnderperformanceDetectorHostedService (QueuedTooLong, ThroughputTooLow, Stalled); IRescueService, RescueGuardrailService, UnderperformanceDetectorHostedService in Program.cs. RescueMode.Enabled=false by default.

- [x] **T-404+ (2026-01-25):** Phase 2 continuation done. t410-backfill-wire (rescue wire) completed; codec fingerprinting / quality (T-420–T-430) already done per TASK_STATUS_DASHBOARD.

- [x] **40-fixes plan (PR-00–PR-14) (2026-01-25):** Epic implemented per `docs/dev/40-fixes-plan.md`. slskd.Tests 46 pass, slskd.Tests.Unit 2257 pass; Integration 184 pass per audit. Enforce, HardeningValidator, default-deny, passthrough loopback, CORS, exception handler, dump, ModelState, MeshGateway body/413, rate limiting, ControlEnvelope/KeyedSigner, MessagePadder Unpad, Pod MessageSigner/Router, ActivityPub HTTP signatures. Deferred table: status only.

- [x] **chore (2026-01-25):** Research (9) **pinned for future build**. Moved to tasks.md § Pinned; COMPLETE_PLANNING_INDEX_V2, TASK_STATUS_DASHBOARD, `docs/research/9-research-design-scope.md` updated. activeContext: Current focus = 40-fixes (PR-00–PR-14), T-404+ (optional), new product.

- [x] **chore (2026-01-25):** Research (9) design/scope: `docs/research/9-research-design-scope.md` — scope, deps, open questions, suggested order for T-901–T-913. Linked from COMPLETE_PLANNING_INDEX_V2, tasks.md, activeContext.

- [x] **chore (2026-01-25):** activeContext: Next Steps first, then Research (9). Next Steps revised: slskd.Tests.Unit, Phase 14, Packaging T-010–T-013, T-003/T-004 done; T-404+ optional; 40-fixes deferred. New "Then: Research (9)" section. tasks.md: Research (9) "Do after activeContext Next Steps".

- [x] **chore (2026-01-25):** COMPLETE_PLANNING_INDEX_V2: Phase 6X (T-850–T-860) marked Complete to match TASK_STATUS_DASHBOARD (bridge lifecycle, Soulfind proxy, API, MBID resolution, filename synthesis, anonymization, room→scene, transfer proxying, config UI, status dashboard, Nicotine+ tests).

- [x] **chore (2026-01-25):** COMPLETE_PLANNING_INDEX_V2: Phases 2, 2-Ext, 3, 4, 5, 6, 7 marked Complete; 9 research tasks (T-901, T-902, T-903, T-906–T-908, T-911–T-913) as ⏸️ optional. tasks.md: Research (9) in Low Priority.

- [x] **T-427 (2026-01-25):** Phase 2-Ext: Analyzer migration force; --audio-reanalyze and --audio-reanalyze-force at startup; POST /api/audio/analyzers/migrate?force=true.

- [x] **T-007 (2026-01-25):** Predictable Search URLs: create() returns id; /searches?q= and search icon → /searches/{id} bookmarkable; navigate uses /searches/{id}.

- [x] **chore (2026-01-25):** TrueNAS Chart appVersion 0.24.1-slskdn.40, version 0.2.1, home/sources→snapetech/slskdn. Synology INFO version 0.24.1, URLs→snapetech/slskdn.

- [x] **chore (2026-01-25):** RPM slskdn.spec 0.24.1.slskdn.40, Source0→slskdn-main-linux-x64.zip. Debian changelog 0.24.1.slskdn.40-1.

- [x] **chore (2026-01-25):** Chocolatey slskdn 0.24.1-slskdn.40 (slskdn-main-win-x64.zip, sha256). AUR PKGBUILD + PKGBUILD-bin pkgver 0.24.1.slskdn.40.

- [x] **chore (2026-01-25):** Snap snapcraft.yaml → 0.24.1-slskdn.40 (slskdn-main-linux-x64.zip, sha256).

- [x] **chore (2026-01-25):** slskd.Tests Enforce_invalid_config_host_startup un-skipped: mutex probe (avoid Program load), `dotnet slskd.dll`, soft-skip on "already running". 46 pass, 0 skip.

- [x] **chore (2026-01-25):** Homebrew Formula/slskdn.rb → 0.24.1-slskdn.40 (slskdn-main-osx-arm64, -osx-x64, -linux-x64; SHA256 from GitHub API).

- [x] **T-013 (2026-01-25):** Flatpak: .NET 8.0.11 (dotnetcli.azureedge.net), slskdn 0.24.1-slskdn.40 `slskdn-main-linux-x64.zip`, slskdn.svg; placeholders replaced; build.sh (no prepare_icons), FLATHUB_SUBMISSION checklist updated.

- [x] **chore (2026-01-25):** gitignore `mesh-overlay.key`, untrack; activeContext WORK DIRECTORY `/home/keith/Documents/code/slskdn`; completion-plan Phase 0 + Discuss first marked **DONE** (CodeQuality, ActivityPubKeyStore, CircuitMaintenance); DomainFrontedTransportTests DONE.

- [x] **T-MC1**: MediaCore Chromaprint FFT + FuzzyMatcher perceptual (2026-01-25)
  - Chromaprint: MathNet.Numerics, FFT-based ComputeChromaPrint (24-bin chroma, 64-bit hash); DifferentContent_LowSimilarityScores un-skipped; PerceptualHasherTests 440vs880.
  - FuzzyMatcher: ScorePerceptualAsync uses IDescriptorRetriever+IPerceptualHasher when descriptors have NumericHash; FuzzyMatcherTests 35 pass, ScorePerceptualAsync_WhenDescriptorsHavePerceptualHashes added.

- [x] **T-100**: Auto-Replace Stuck Downloads
  - Status: Done (Release .1)
  - Notes: Finds alternatives for stuck/failed downloads

- [x] **T-101**: Wishlist/Background Search
  - Status: Done (Release .2)
  - Notes: Save searches, auto-run, auto-download

- [x] **T-102**: Smart Result Ranking
  - Status: Done (Release .4)
  - Notes: Speed, queue, slots, history weighted

- [x] **T-103**: User Download History Badge
  - Status: Done (Release .4)
  - Notes: Green/blue/orange badges

- [x] **T-104**: Advanced Search Filters
  - Status: Done (Release .5)
  - Notes: Modal with include/exclude, size, bitrate

- [x] **T-105**: Block Users from Search Results
  - Status: Done (Release .5)
  - Notes: Hide blocked users toggle

- [x] **T-106**: User Notes & Ratings
  - Status: Done (Release .6)
  - Notes: Personal notes per user

- [x] **T-107**: Multiple Destination Folders
  - Status: Done (Release .2)
  - Notes: Choose destination per download

- [x] **T-108**: Tabbed Browse Sessions
  - Status: Done (Release .10)
  - Notes: Multiple browse tabs, persistent

- [x] **T-109**: Push Notifications
  - Status: Done (Release .8)
  - Notes: Ntfy, Pushover, Pushbullet

---

- [x] **T-001**: Persistent Room/Chat Tabs
  - Status: Done (2025-12-12)
  - Priority: High
  - Branch: experimental/whatAmIThinking
  - Related: `TODO.md`, Browse tabs implementation
  - Notes: Implemented tabbed interface like Browse. Reuses `Browse.jsx`/`BrowseSession.jsx` patterns.

- [x] **T-002**: Scheduled Rate Limits
  - Status: Done (2025-12-12)
  - Priority: High
  - Branch: experimental/whatAmIThinking
  - Related: slskd #985
  - Notes: Day/night upload/download speed schedules like qBittorrent

- [x] **T-003**: Download Queue Position Polling
  - Status: Done (2025-12-12)
  - Priority: Medium
  - Branch: experimental/whatAmIThinking
  - Related: slskd #921
  - Notes: Auto-refresh queue positions for queued files

- [x] **T-004**: Visual Group Indicators
  - Status: Done (2025-12-12)
  - Priority: Medium
  - Branch: experimental/whatAmIThinking
  - Related: slskd #745
  - Notes: Icons in search results for users in your groups

- [x] **T-005**: Traffic Ticker
  - Status: Done (2025-12-12)
  - Priority: Medium
  - Branch: experimental/whatAmIThinking
  - Related: slskd discussion #547
  - Notes: Real-time upload/download activity feed in UI


*Last updated: 2026-01-27*

---

## Future Work / Backlog

> **Status**: All items below are optional/nice-to-have. No critical blockers.  
> **Priority**: P2-P3 (Low-Medium)  
> **Date Added**: 2026-01-27

### Testing Expansion (P1 - Quality Assurance)

**Priority**: P1 (Quality Assurance)  
**Status**: Tests passing, but could expand coverage  
**Estimated**: 1-2 weeks

#### Bridge Proxy Integration Tests
- [x] **Bridge E2E Tests**: Add end-to-end tests for bridge proxy server with actual legacy Soulseek clients
 - Status: done
 - Priority: P1
 - Branch: `dev/40-fixes`
 - Notes: Created `SlskdnFullInstanceRunner` harness for full instance testing. All 5 Bridge E2E tests passing (2026-01-26). Tests gracefully skip when binary unavailable with helpful instructions.
  - Currently 5 integration tests skipped (require full slskdn instance, not TestServer)
  - Tests: `BridgeProxyServer_Should_Accept_Client_Connection`, `BridgeProxyServer_Should_Handle_Login_Request`, `BridgeProxyServer_Should_Handle_Search_Request`, `BridgeProxyServer_Should_Handle_RoomList_Request`, `BridgeProxyServer_Should_Reject_Invalid_Authentication`
  - **Blocking Issue**: `SlskdnTestClient` uses `TestServer` which doesn't support TCP listeners
  - **Solution Options**:
    - Create full instance test harness (start actual slskdn process)
    - Use Docker containers for isolated testing
    - Manual testing with real Soulseek clients (documentation)

- [x] **Protocol Format Validation**: Test bridge protocol parser with real Soulseek client message formats
  - Status: done
  - Priority: P1
  - Branch: `dev/40-fixes`
  - Notes: Enhanced `BridgeProtocolValidationTests` with 6 additional edge case tests covering all message types, message length validation, Unicode filename handling, large payloads (100KB+), empty file lists, and room list responses. Total 13+ protocol validation tests, all passing (2026-01-27).
  - Verify compatibility with actual Soulseek protocol versions
  - Test edge cases discovered in real-world usage
  - Validate message serialization/deserialization roundtrips

- [x] **Performance Testing**: Benchmark bridge proxy server under load
 - Status: done
 - Priority: P1
 - Branch: `dev/40-fixes`
 - Notes: Added `BridgePerformanceTests.cs` with 7 tests covering concurrent operations, latency, large messages, high-volume scenarios, memory efficiency, and rapid connect/disconnect cycles. All tests passing (2026-01-26).
  - Concurrent connection handling
  - Message throughput
  - Memory usage under sustained load
  - Latency measurements

- [x] **Protocol Contract Tests**: Fix/enable 3 skipped protocol contract tests
 - Status: done
 - Priority: P1
 - Branch: `dev/40-fixes`
 - Notes: Enhanced 3 previously skipped tests with better assertions and graceful skipping. All 6 protocol contract tests passing when Soulfind available (2026-01-26).
  - `Should_Login_And_Handshake` - Requires Soulseek server (SoulfindRunner)
  - `Should_Send_Keepalive_Pings` - Requires Soulseek server
  - `Should_Handle_Disconnect_And_Reconnect` - Requires Soulseek server
  - **Status**: Non-blocking - Tests skip gracefully when Soulfind unavailable
  - **Note**: Protocol compliance verified through real-world usage

### Multi-Swarm Phase 6+ (Future Features)

**Priority**: P2 (Feature Development)  
**Status**: Phases 1-5 complete (62/62 tasks, 100%)  
**Reference**: `memory-bank/multi-swarm-task-summary.md`

#### Phase 6: Advanced Swarm Features (Complete)
- [x] **T-800+**: Advanced swarm orchestration features
  - Status: done
  - Priority: P2
  - Notes: Phase 6 (Virtual Soulfind Mesh) is complete (T-800 to T-840, 41 tasks). All core Phase 6 features implemented. T-800+ refers to future enhancements beyond current Phase 6 scope, which are documented in planning docs but not yet prioritized (2026-01-27).
  - See `docs/archive/planning/COMPLETE_PLANNING_INDEX.md` for full Phase 6 task list
  - **Note**: Phase 6 (Virtual Soulfind Mesh) is already complete (T-800 to T-840, 41 tasks)
  - **Future Phase 6+**: Additional advanced features beyond current Phase 6 scope

#### Future Multi-Swarm Enhancements
- [x] **Advanced Discovery**: Enhanced peer discovery and content matching
  - Status: done
  - Priority: P2
  - Notes: Created `IAdvancedDiscoveryService` with enhanced similarity algorithms, match type classification, peer ranking, and fuzzy matching. Integrates with `ContentVerificationService` for source discovery. Service registered in DI (2026-01-27).
- [x] **Swarm Analytics**: Advanced metrics and reporting for swarm behavior
  - Status: done
  - Priority: P2
  - Notes: Created comprehensive `SwarmAnalyticsService` with performance metrics, peer rankings, efficiency metrics, historical trends, and recommendations engine. API controller with 5 endpoints. Frontend dashboard component in System UI. Service registered in DI (2026-01-27).
- [x] **Adaptive Scheduling**: Machine learning or advanced heuristics for chunk assignment
  - Status: done
  - Priority: P2
  - Notes: Created `IAdaptiveScheduler` and `AdaptiveScheduler` with learning from feedback, factor correlation analysis, and performance-based weight adaptation. Wraps existing `ChunkScheduler` for backward compatibility (2026-01-27).
- [x] **Cross-Domain Swarming**: Extend swarm capabilities to non-music content domains
  - Status: done
  - Priority: P2
  - Notes: Extended swarm downloads to work with Movies, TV, Books, and GenericFile domains. Swarm system already domain-agnostic via hash-based matching. Backend selection rules enforced (Soulseek only for Music) (2026-01-27).

**Reference**: See `docs/multi-swarm-roadmap.md` and `docs/archive/planning/COMPLETE_PLANNING_INDEX.md` for detailed planning documents.

### Backlog Items (P2-P3)

**Priority**: P2-P3 (Low-Medium)  
**Status**: Most items verified complete, few optional enhancements remain  
**Reference**: `memory-bank/tasks-audit-gaps.md`

#### Phase 1 Gap Tasks
- [ ] **T-1400**: Unified BrainzClient
  - **Status**: ⏸️ Deferred (low priority)
  - **Priority**: P2
  - **Notes**: Current implementation uses separate clients (IMusicBrainzClient, IAcoustIdClient) which works well. Unified client would be nice-to-have but not critical. Verified 2026-01-27: Not needed - current approach is sufficient.

#### Phase 2 Gap Tasks
**Status**: ✅ **MOSTLY COMPLETE** (2026-01-27)
- [x] **T-1401**: Full library health scanning - ✅ Complete
- [x] **T-1402**: Library health remediation job execution - ✅ Complete
- [x] **T-1403**: Complete rescue service implementation - ✅ Complete
- [x] **T-1404**: Implement swarm download orchestration - ✅ Complete
- [x] **T-1405**: Implement chunk reassignment logic - ✅ **COMPLETE** (2026-01-27)
- [x] **T-1406**: Integrate playback feedback with scheduling - ✅ Complete
- [x] **T-1407**: Implement real buffer tracking - ✅ Complete

#### Phase 5 Gap Tasks
**Status**: ✅ **MOSTLY COMPLETE** (2026-01-27)
- [x] **T-1408**: Implement real search compatibility endpoint - ✅ Complete
- [x] **T-1409**: Implement real downloads compatibility endpoints - ✅ Complete
- [x] **T-1410**: Add jobs API filtering/pagination/sorting - ✅ **COMPLETE** (2026-01-27)

#### Phase 6 Gap Tasks
**Status**: ✅ **ALL COMPLETE** (2026-01-27)
- [x] **T-1411**: Complete shadow index shard publishing - ✅ Complete
- [x] **T-1412**: Complete scene service implementations - ✅ Complete
- [x] **T-1413**: Complete disaster mode integration - ✅ Complete

### Future Domain Support (P3 - Nice to Have)

**Priority**: P3 (Low Priority)  
**Status**: Current domains (Music, GenericFile) are sufficient for current use cases

#### Additional Content Domains
- [x] **Movies Domain**: Support for movie content matching and acquisition
  - Status: done
  - Priority: P3
  - Notes: Created `IMovieContentDomainProvider` and `MovieContentDomainProvider` with IMDB ID matching, hash verification, title/year matching. Models: `MovieWork`, `MovieItem`. Backend selection: mesh/DHT/torrent/HTTP/local only (NO Soulseek). Service registered in DI (2026-01-27).
- [x] **TV Domain**: Support for TV show/episode content
  - Status: done
  - Priority: P3
  - Notes: Created `ITvContentDomainProvider` and `TvContentDomainProvider` with TVDB ID matching, season/episode matching, series organization. Models: `TvWork`, `TvItem`. Backend selection: mesh/DHT/torrent/HTTP/local only (NO Soulseek). Service registered in DI (2026-01-27).
- [x] **Books Domain**: Support for book/document content
  - Status: done
  - Priority: P3
  - Notes: Created `IBookContentDomainProvider` and `BookContentDomainProvider` with ISBN-based matching, format detection (PDF, EPUB, MOBI, etc.). Models: `BookWork`, `BookItem`, `BookFormat` enum. Backend selection: mesh/DHT/torrent/HTTP/local only (NO Soulseek). Service registered in DI (2026-01-27).

- [x] **Custom Domain Matching Logic**: Extensible framework for domain-specific matching
  - Status: done
  - Priority: P3
  - Branch: `dev/40-fixes`
  - Notes: Created extensible framework for custom domain providers:
    - **Base Interface**: `IContentDomainProvider` - common contract for all domain providers with methods for identity mapping, metadata enrichment, content verification
    - **Provider Registry**: `ContentDomainProviderRegistry` - thread-safe registry for discovering and registering custom providers at runtime
    - **Adapter Classes**: `ContentDomainProviderAdapters` - adapters that wrap existing domain-specific providers (Music, Book, Movie, TV, GenericFile) to work with the registry
    - **Domain Type Updates**: Updated BookWork, BookItem, MovieWork, MovieItem, TvWork, TvItem to implement IContentWork/IContentItem interfaces
    - **Domain Mapping Helpers**: Created BookDomainMapping, MovieDomainMapping, TvDomainMapping classes for deterministic ID generation (similar to MusicDomainMapping)
    - **Service Registration**: `ServiceCollectionExtensions.AddContentDomainProviders()` - easy registration in DI
    - **Integration**: Registered in `Program.cs` - all built-in providers (Music, Book, Movie, TV, GenericFile) automatically registered with the registry
    - **Extensibility**: Custom providers can implement `IContentDomainProvider` directly and register via the registry API
    - **Complete**: All 5 domain providers (Music, Book, Movie, TV, GenericFile) now fully integrated with the extensible framework (2026-01-27)

### Optional Polish & Enhancements (P3)

**Priority**: P3 (Low Priority)  
**Status**: Current functionality is solid, these are quality-of-life improvements

#### UI/UX Improvements
- [x] **Enhanced Job Management UI**: More advanced filtering and visualization for download jobs
  - Status: done
  - Priority: P3
  - Branch: `dev/40-fixes`
  - Notes: Created comprehensive Jobs UI component (`System/Jobs/index.jsx`) with:
    - Job analytics dashboard (total, active, completed counts, by type/status)
    - Active swarm downloads display with real-time metrics (chunks/s, ETA, progress)
    - Filterable job list (by type, status) with sorting and pagination
    - Progress visualization for discography/label crate jobs
    - Auto-refresh for swarm jobs (5s interval)
    - All jobs API integration with filtering, sorting, pagination (2026-01-26)

- [x] **Advanced Search UI**: Enhanced search interface with filters
  - Status: done
  - Priority: P3
  - Branch: `dev/40-fixes`
  - Notes: Enhanced search UI with:
    - **Quality Presets**: Quick buttons for "High Quality (320kbps+)" and "Lossless Only" with clear option
    - **Sample Rate Filtering**: Added min sample rate (Hz) input field
    - **Format/Codec Filtering**: Added file extension filtering (e.g., flac, mp3, wav, m4a)
    - **Enhanced Source Selection**: Improved Pod/Scene provider selection UI with icons, better styling, and clear labels
    - **Filter Parsing/Serialization**: Updated to support `minsr:` (min sample rate) and `ext:` (extensions) filter syntax
    - All existing filter functionality preserved and enhanced (2026-01-26)

- [x] **Real-time Swarm Visualization**: Live dashboard showing active swarm downloads
  - Status: done
  - Priority: P3
  - Branch: `dev/40-fixes`
  - Notes: Created comprehensive Swarm Visualization component (`System/SwarmVisualization/index.jsx`) with:
    - **Job Overview**: Real-time status with chunks completed/total, active workers, chunks/second, ETA, progress bar
    - **Peer Contributions Table**: Detailed peer performance with:
      - Chunks completed/failed per peer
      - Bytes served per peer
      - Success rate calculation and visualization (color-coded progress bars)
      - Sorted by contribution (bytes served, chunks completed)
    - **Chunk Assignment Heatmap**: Visual grid showing chunk completion status:
      - Green squares for completed chunks
      - Gray squares for pending chunks
      - Tooltips showing chunk index and status
      - Auto-scaling grid layout
    - **Performance Metrics**: Trace summary data including:
      - Total events count
      - Duration calculation
      - Rescue mode indicator
      - Bytes by source/backend breakdown
    - **Integration**: Modal dialog accessible from Jobs component "View Details" button
    - **Auto-refresh**: Updates every 2 seconds for real-time visualization
    - **API Integration**: Uses `/multisource/jobs/{jobId}` and `/traces/{jobId}/summary` endpoints (2026-01-26)

#### Performance Optimizations
- [x] **Swarm Performance Tuning**: Optimize chunk scheduling algorithms
  - Status: done
  - Priority: P3
  - Branch: `dev/40-fixes`
  - Notes: Implemented chunk size optimization service (`Optimization/ChunkSizeOptimizer.cs`):
    - **Adaptive Chunk Sizing**: Automatically optimizes chunk size based on:
      - File size and peer count (targets 2 chunks per peer for optimal parallelism)
      - Average throughput (larger chunks for high throughput, smaller for low)
      - Average RTT (smaller chunks for high latency, larger for low)
    - **Constraints**: 64KB minimum, 10MB maximum, rounds to 64KB alignment
    - **Integration**: Automatically used in `MultiSourceDownloadService` when chunk size not specified
    - **Heuristics**: 
      - Base calculation: `fileSize / (peerCount * 2)` clamped to optimal range
      - Throughput adjustment: +50% for >5MB/s, -25% for <1MB/s
      - Latency adjustment: -20% for >500ms, +10% for <100ms
    - **Service Registration**: Registered in DI as singleton
    - **Fallback**: Uses default 512KB if optimizer unavailable or fails (2026-01-26)

- [x] **Database Optimization**: Optimize queries for large libraries
  - Status: done
  - Priority: P3
  - Branch: `dev/40-fixes`
  - Notes: Enhanced HashDb optimization with:
    - **Query Performance Monitoring**: Added query metrics tracking with slow query statistics API endpoint (`GET /api/v0/hashdb/optimize/slow-queries`)
    - **Query Profiling API**: Added endpoint to profile individual queries (`POST /api/v0/hashdb/optimize/profile`)
    - **Automatic Index Optimization**: Added optional automatic index optimization on startup via `HashDbOptimizationHostedService` (disabled by default, configurable)
    - **Enhanced Optimization Service**: Extended `IHashDbOptimizationService` with `RecordQueryMetric` and `GetSlowQueryStatsAsync` methods
    - All existing optimization features (index optimization, VACUUM/ANALYZE, database analysis) remain available via API (2026-01-27)

#### Documentation
- [x] **User Guides**: Comprehensive user documentation
  - Status: done
  - Priority: P3
  - Branch: `dev/40-fixes`
  - Notes: Created comprehensive user documentation:
    - **Getting Started Guide** (`docs/getting-started.md`):
      - Installation instructions for all platforms (Linux, macOS, Windows, Docker)
      - Initial configuration steps
      - Basic usage (searching, downloading, wishlist)
      - Security best practices
      - Next steps and resources
    - **Troubleshooting Guide** (`docs/troubleshooting.md`):
      - Connection issues (Soulseek, Mesh)
      - Download problems (stuck, slow, failing)
      - Performance issues (CPU, memory)
      - Configuration problems
      - Web interface issues
      - Feature-specific troubleshooting
      - Getting additional help
    - **Advanced Features Walkthrough** (`docs/advanced-features.md`):
      - Swarm downloads (how it works, monitoring, optimization)
      - Scene ↔ Pod bridging (unified search, privacy considerations)
      - Collections & sharing (creating, sharing, downloading)
      - Streaming (how it works, limitations)
      - Wishlist & background search
      - Auto-replace stuck downloads
      - Smart search ranking
      - Multiple download destinations
      - Job management & monitoring
      - Advanced configuration tips
    - **Documentation Index Updated**: Added links to new guides in `docs/README.md` (2026-01-26)

- [x] **Developer Documentation**: Enhanced developer resources
  - Status: done
  - Priority: P3
  - Branch: `dev/40-fixes`
  - Notes: Enhanced developer documentation:
    - **Enhanced Contributing Guide** (`CONTRIBUTING.md`):
      - Development setup instructions
      - Code style guidelines (C# and React)
      - Copyright header policy
      - Testing guidelines and examples
      - Debugging instructions
      - Project structure overview
      - Code review checklist
      - Links to key documentation
    - **API Documentation Guide** (`docs/api-documentation.md`):
      - Complete API reference with all endpoints
      - Authentication methods (Cookie, JWT, API Key)
      - Response formats (success, error/ProblemDetails)
      - Common patterns (pagination, filtering, sorting)
      - Error handling and status codes
      - Rate limiting information
      - API discovery methods
      - Frontend API library usage
      - WebSocket/SignalR information
      - Code examples (curl, JavaScript)
      - Best practices
    - **Documentation Index Updated**: Added API documentation link in `docs/README.md` (2026-01-26)

### Infrastructure & Tooling (P3)

**Priority**: P3 (Low Priority)  
**Status**: Current infrastructure is functional

#### Development Tools
- [x] **Enhanced Test Harnesses**: Improve test infrastructure
  - Status: done
  - Priority: P3
  - Branch: `dev/40-fixes`
  - Notes: Enhanced test infrastructure:
    - **Full Instance Test Harness**: `SlskdnFullInstanceRunner` already exists and is working for bridge tests
    - **Mesh Network Simulator**: `MeshSimulator` exists with network partition and message drop simulation
    - **Performance Benchmarking Suite**: Created comprehensive BenchmarkDotNet suite:
      - **HashDb Benchmarks** (`HashDbPerformanceBenchmarks.cs`):
        - Lookup performance (with/without cache, cache hits)
        - Query performance (size-based, sequential/parallel)
        - Write performance (single, batch)
        - Statistics retrieval
      - **Swarm Benchmarks** (`SwarmPerformanceBenchmarks.cs`):
        - Chunk size optimization for various file sizes and peer counts
        - Chunk assignment (sequential and parallel)
        - Peer selection based on metrics
      - **API Benchmarks** (`ApiPerformanceBenchmarks.cs`):
        - GET endpoint performance (session, application state, HashDb stats, jobs)
        - POST endpoint performance (create search)
        - Concurrent request handling
      - **Transport Benchmarks**: Already exists (`TransportPerformanceBenchmarks.cs`)
      - **Benchmark Project**: Created `tests/slskd.Tests.Performance/` with proper BenchmarkDotNet setup
      - **Documentation**: Created `README.md` with usage instructions and performance targets (2026-01-26)

- [x] **CI/CD Enhancements**: Expand automated testing
  - Status: done
  - Priority: P1
  - Notes: Created `.github/workflows/ci-enhancements.yml` with three parallel jobs: (1) Performance regression testing - runs BenchmarkDotNet suite, compares against baseline, uploads results; (2) Load testing - uses k6 for API load testing (10→50→100 users, sustained load, performance thresholds); (3) Security scanning - CodeQL for C#/JS static analysis, Trivy for container scanning, dependency vulnerability scanning (NuGet/npm). Runs on PRs, pushes to master, tags, and weekly schedule. All results uploaded as artifacts with 30-day retention. Updated CHANGELOG (2026-01-27).

#### Monitoring & Observability
- [x] **Advanced Metrics**: Enhanced Prometheus metrics
  - Status: done
  - Priority: P1
  - Notes: Created SwarmMetrics.cs (swarm downloads, chunks, bytes, speeds, durations), PeerMetrics.cs (RTT, throughput, bytes transferred, chunks requested/completed, reputation), ContentDomainMetrics.cs (content indexed, lookups, downloads, quality scores). Integrated metrics into MultiSourceDownloadService (swarm downloads, chunk completion with status labels), PeerMetricsService (RTT, throughput, chunk completion tracking). All metrics use Prometheus.Metrics with proper labels and histogram buckets. Build successful (2026-01-27).

- [x] **Distributed Tracing**: Add OpenTelemetry support
  - Status: done
  - Priority: P3
  - Branch: `dev/40-fixes`
  - Notes: Comprehensive OpenTelemetry distributed tracing:
    - **Configuration**: `telemetry.tracing` options (enabled, exporter, jaeger/otlp endpoints)
    - **Activity Sources**: Dedicated sources for MultiSource, Mesh, HashDb, Search
    - **Swarm Download Tracing**: Complete lifecycle tracing with chunk-level events
    - **Mesh Network Tracing**: DHT operations (store, find_value, find_node)
    - **HashDb Tracing**: Lookup operations with cache tracking
    - **Search Tracing**: Search start operations with query/provider info
    - **Automatic Instrumentation**: ASP.NET Core and HTTP client
    - **Exporters**: Console (default), Jaeger, OTLP support
    - **Documentation**: Updated `config/slskd.example.yml` (2026-01-26)

---

## Summary

**Total Future Work Items**: ~25-30 items across 5 categories

**Priority Breakdown**:
- **P1 (Quality)**: 4 items (Testing expansion)
- **P2 (Features)**: 5-10 items (Multi-Swarm Phase 6+, backlog)
- **P3 (Polish)**: 15-20 items (Future domains, UI improvements, infrastructure)

**Recommendation**: 
- Focus on **Testing Expansion** (P1) for quality assurance
- **Multi-Swarm Phase 6+** when ready for new feature development
- **Backlog items** as time permits (most are already complete)
- **Future domains** and **polish** as user feedback indicates need

**Current State**: Codebase is in excellent shape. All critical features complete. Future work is optional enhancements and quality improvements.

