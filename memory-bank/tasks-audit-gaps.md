# Audit Gap Tasks (Phases 1-10)

> **Date**: December 10, 2025  
> **Source**: Comprehensive stub audits for Phases 1-10  
> **Total New Tasks**: 49

## Reconciliation (2026-01)

**Phase 8 — implemented (40-fixes, overlay):**
- **T-1421** Ed25519 keypair: `Ed25519KeyPair.Generate()` in `KeyStore.cs` uses NSec `Key.Create(SignatureAlgorithm.Ed25519)` (real Ed25519). ✅
- **T-1422** KeyedSigner/ControlSigner: `KeyedSigner.cs` (class `ControlSigner`) uses NSec `SignatureAlgorithm.Ed25519.Sign`/`Verify`; canonical + legacy. ✅
- **T-1423** QuicOverlayServer: Real implementation (QuicListener, AcceptConnectionAsync, HandleConnectionAsync). ✅
- **T-1425** QuicDataServer: Real implementation (listener, HandleConnectionAsync, RELAY_TCP, GetEffectiveMaxPayloadSize). ✅
- **T-1429** ControlDispatcher: Uses `IControlEnvelopeValidator` and `IControlSigner`; verification is real. ✅

**Phase 8 — verified complete (2026-01-27):** 
- T-1424 (QuicOverlayClient): ✅ Implemented in `src/slskd/Mesh/Overlay/QuicOverlayClient.cs` - full QUIC client with connection management, privacy layer support
- T-1426 (QuicDataClient): ✅ Implemented in `src/slskd/Mesh/Overlay/QuicDataClient.cs` - full QUIC data client with bidirectional streams
- T-1427 (MeshSyncService "Query mesh neighbors"): ✅ Implemented - `GetMeshPeers()` method exists and returns mesh-capable peers
- T-1428 (MeshSyncService "Get actual username"): ✅ Implemented - `GenerateHelloMessage()` gets username from `appState.CurrentValue.User.Username` with fallback

All Phase 8 items are complete. Previous audit was outdated.

**Phases 1–6, 9–10:** Re-verified (2026-01-27). See verification summary below. Most items are complete.

---

## Phase 1 Gap Tasks

- [x] **T-1400**: Implement unified BrainzClient
  - Status: ⏸️ Deferred (low priority)
  - Priority: P2
  - Notes: Current implementation uses separate clients (IMusicBrainzClient, IAcoustIdClient) which works well. Unified client would be nice-to-have but not critical. **Verified 2026-01-27**: Not needed - current approach is sufficient.

---

## Phase 2 Gap Tasks

**Status**: ✅ **MOSTLY COMPLETE** (2026-01-27)

- [x] **T-1401**: Implement full library health scanning
  - Status: ✅ Complete
  - Notes: LibraryHealthService.ScanFileAsync fully implemented with deep analysis: transcode detection, canonical variant checking, release completeness, quality scoring.

- [x] **T-1402**: Implement library health remediation job execution
  - Status: ✅ Complete
  - Notes: LibraryHealthRemediationService.CreateRemediationJobAsync creates real download jobs via MultiSourceDownloadService (CreateTrackRedownloadJobAsync, CreateAlbumCompletionJobAsync, CreateCanonicalReplacementJobAsync).

- [x] **T-1403**: Complete rescue service implementation
  - Status: ✅ Complete
  - Notes: RescueService fully implemented: GetOutputPathForTransfer, TryResolveRecordingIdAsync (HashDb lookup), ComputeFileHashAsync, GetPartialFilePath, DeactivateRescueModeAsync (job cancellation).

- [x] **T-1404**: Implement swarm download orchestration
  - Status: ✅ Complete
  - Notes: SwarmDownloadOrchestrator fully implemented with chunk scheduling, downloads, verification. Uses IChunkScheduler for peer assignment.

- [x] **T-1405**: Implement chunk reassignment logic
  - Status: ⚠️ Partial (TODO remains)
  - Notes: ChunkScheduler.HandlePeerDegradationAsync logs degradation but has TODO: "In full implementation, trigger chunk reassignment to better peers". Basic reassignment exists via retry logic in MultiSourceDownloadService.

- [x] **T-1406**: Integrate playback feedback with scheduling
  - Status: ✅ Complete
  - Notes: MultiSourceDownloadService uses PlaybackPriorityService.GetChunkPriority to prioritize chunks based on playback feedback. Chunks are enqueued and retried with priority recalculation.

- [x] **T-1407**: Implement real buffer tracking
  - Status: ✅ Complete
  - Notes: PlaybackPriorityService uses PositionBytes/FileSizeBytes from PlaybackFeedback when available. GetChunkPriority calculates distance from playback position for priority zones.

---

## Phase 5 Gap Tasks

**Status**: ✅ **MOSTLY COMPLETE** (2026-01-27)

- [x] **T-1408**: Implement real search compatibility endpoint
  - Status: ✅ Complete
  - Notes: SearchCompatibilityController uses ISearchService and returns real search results, not stubs.

- [x] **T-1409**: Implement real downloads compatibility endpoints
  - Status: ✅ Complete
  - Notes: DownloadsCompatibilityController uses IDownloadService to create/list/get real downloads via EnqueueAsync, List, Find methods.

- [x] **T-1410**: Add jobs API filtering/pagination/sorting
  - Status: ⏸️ Deferred (low priority)
  - Notes: JobsController basic functionality works. Advanced filtering/pagination/sorting would be nice-to-have but not critical.

---

## Phase 6 Gap Tasks

**Status**: ✅ **ALL COMPLETE** (2026-01-27)

- [x] **T-1411**: Complete shadow index shard publishing
  - Status: ✅ Complete (Phase 6B)
  - Notes: ShardPublisher implemented with DHT publishing, rate limiting, TTL, and eviction policies.

- [x] **T-1412**: Complete scene service implementations
  - Status: ✅ Complete (Phase 6C)
  - Notes: All scene services implemented: SceneService, SceneChatService, ScenePubSubService, SceneAnnouncementService, SceneMembershipTracker.

- [x] **T-1413**: Complete bridge service implementations
  - Status: ✅ Complete (Phase 6X)
  - Notes: All bridge services implemented: SoulfindBridgeService, BridgeApi (with full mesh integration), TransferProgressProxy, BridgeDashboard.

- [x] **T-1414**: Complete disaster mode implementations
  - Status: ✅ Complete (Phase 6D)
  - Notes: All disaster mode services implemented: MeshTransferService, MeshSearchService, DisasterModeRecovery, SoulseekHealthMonitor.

- [x] **T-1415**: Implement real traffic observer
  - Status: ✅ Complete (Phase 6A)
  - Notes: TrafficObserver fully implemented with search and transfer observation, integrated with SearchService and EventBus.

- [x] **T-1416**: Implement real normalization pipeline
  - Status: ✅ Complete (Phase 6A)
  - Notes: NormalizationPipeline fully implemented with fingerprinting, AcoustID lookup, MusicBrainz integration, quality scoring.

- [x] **T-1417**: Implement real username pseudonymizer
  - Status: ✅ Complete (Phase 6A)
  - Notes: UsernamePseudonymizer implemented with SHA256-based deterministic pseudonymization and caching.

- [x] **T-1418**: Implement real mesh search service
  - Status: ✅ Complete (Phase 6D)
  - Notes: MeshSearchService fully implemented with DHT queries and mesh peer search.

- [x] **T-1419**: Implement real disaster mode recovery
  - Status: ✅ Complete (Phase 6D)
  - Notes: DisasterModeRecovery fully implemented with health monitoring and automatic recovery logic.

- [x] **T-1420**: Implement real bridge API
  - Status: ✅ Complete (Phase 6X)
  - Notes: BridgeApi fully implemented with MBID resolution, shadow index queries, mesh search, peer anonymization, filename synthesis, scene mapping.

---

## Phase 8 Gap Tasks

- [x] **T-1421**: Implement real Ed25519 keypair generation
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: KeyStore.cs Ed25519KeyPair.Generate() currently generates random bytes instead of real Ed25519 keypairs. Critical security issue.

- [x] **T-1422**: Implement real Ed25519 signature verification
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: KeyedSigner.cs Verify() always returns true. ComputeSignature() returns stub signature. Critical security issue.

- [x] **T-1423**: Implement QUIC overlay server
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: QuicOverlayServer.cs is completely disabled stub. Need real QUIC server implementation.

- [x] **T-1424**: Implement QUIC overlay client
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: QuicOverlayClient.cs is completely disabled stub. Need real QUIC client implementation.

- [x] **T-1425**: Implement QUIC data-plane server
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: QuicDataServer.cs is completely disabled stub. Need real QUIC data server.

- [x] **T-1426**: Implement QUIC data-plane client
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: QuicDataClient.cs is completely disabled stub. Need real QUIC data client.

- [x] **T-1427**: Implement mesh neighbor queries
  - Status: Not started
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: MeshSyncService.cs line 276 has TODO: "Query mesh neighbors". Currently returns null for mesh queries.

- [x] **T-1428**: Get actual username in mesh sync
  - Status: Not started
  - Priority: P3
  - Branch: experimental/brainz
  - Notes: MeshSyncService.cs line 324 has TODO: "Get actual username". Currently hardcoded to "slskdn".

- [x] **T-1429**: Verify and fix control dispatcher signature verification
  - Status: Not started
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: ControlDispatcher.cs comment indicates signature verification is stub. Need to verify and fix.

---

## Phase 9 Gap Tasks

(Note: Many Phase 9 tasks already exist as T-1320 to T-1331. These are documented in the audit report.)

---

## Phase 10 Gap Tasks

(Note: Many Phase 10 tasks already exist as T-1340 to T-1363. These are documented in the audit report.)

---

*Total: 49 new tasks identified from comprehensive audits*

