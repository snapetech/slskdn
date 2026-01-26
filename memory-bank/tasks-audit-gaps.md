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

**Phase 8 — still open:** T-1424 (QuicOverlayClient), T-1426 (QuicDataClient), T-1427 (MeshSyncService "Query mesh neighbors"), T-1428 (MeshSyncService "Get actual username").

**Phases 1–6, 9–10:** Not re-verified; treat as backlog. Promote to `memory-bank/tasks.md` when prioritizing.

---

## Phase 1 Gap Tasks

- [x] **T-1400**: Implement unified BrainzClient
  - Status: Not started
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Replace placeholder. Implement unified client for MB/AcoustID/Soulbeet with caching and backoff. Currently returns null.

---

## Phase 2 Gap Tasks

- [x] **T-1401**: Implement full library health scanning
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Replace simplified placeholder. Currently just records scan completion without deep analysis. Need to implement issue detection, codec analysis, quality checks.

- [x] **T-1402**: Implement library health remediation job execution
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Currently returns placeholder job IDs. Need to integrate with multi-source download service to create actual download jobs for recordings/releases.

- [x] **T-1403**: Complete rescue service implementation
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Multiple TODOs: output path resolution, job cancellation, HashDb lookup, file hash computation, partial file path. Currently has placeholder activation.

- [x] **T-1404**: Implement swarm download orchestration
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Replace placeholder. Currently has comment "Placeholder: actual chunk scheduling and download would go here". Core functionality missing.

- [x] **T-1405**: Implement chunk reassignment logic
  - Status: Not started
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: TODO in ChunkScheduler.cs: "In full implementation, trigger chunk reassignment to better peers". Currently not implemented.

- [x] **T-1406**: Integrate playback feedback with scheduling
  - Status: Not started
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: PlaybackFeedbackService is marked as "placeholder for future scheduling integration". Need to wire feedback into chunk scheduler.

- [x] **T-1407**: Implement real buffer tracking
  - Status: Not started
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: PlaybackPriorityService uses desired buffer as proxy. Need to track actual buffer state.

---

## Phase 5 Gap Tasks

- [x] **T-1408**: Implement real search compatibility endpoint
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: SearchCompatibilityController currently returns stub SearchId and empty Results. Need to integrate with ISearchService to return real results.

- [x] **T-1409**: Implement real downloads compatibility endpoints
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: DownloadsCompatibilityController returns stub data. Need to integrate with ITransferService to create/list/get real downloads.

- [x] **T-1410**: Add jobs API filtering/pagination/sorting
  - Status: Not started
  - Priority: P3
  - Branch: experimental/brainz
  - Notes: JobsController has TODO comment. Basic functionality works but missing advanced features.

---

## Phase 6 Gap Tasks

- [x] **T-1411**: Complete shadow index shard publishing
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: ShardPublisher.cs has 11 TODO comments. Need to complete DHT publishing implementation.

- [x] **T-1412**: Complete scene service implementations
  - Status: Not started
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Multiple scene services have TODOs: SceneService (1), SceneChatService (3), ScenePubSubService (5), SceneAnnouncementService (4), SceneMembershipTracker (2). Total 15 TODOs.

- [x] **T-1413**: Complete bridge service implementations
  - Status: Not started
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Bridge services have TODOs: SoulfindBridgeService (1), BridgeApi (3), TransferProgressProxy (1). Total 5 TODOs.

- [x] **T-1414**: Complete disaster mode implementations
  - Status: Not started
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: Disaster mode services have TODOs: MeshTransferService (2), MeshSearchService (2), DisasterModeRecovery (1), SoulseekHealthMonitor (1). Total 6 TODOs.

- [x] **T-1415**: Implement real traffic observer
  - Status: Not started
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: TrafficObserver.cs is marked as "Stub traffic observer; full capture disabled for build". Need real implementation.

- [x] **T-1416**: Implement real normalization pipeline
  - Status: Not started
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: NormalizationPipeline.cs is marked as "Stub normalization pipeline to satisfy build; real implementation pending".

- [x] **T-1417**: Implement real username pseudonymizer
  - Status: Not started
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: UsernamePseudonymizer.cs is marked as "Stub pseudonymizer; returns deterministic GUID-based IDs". Need real pseudonymization.

- [x] **T-1418**: Implement real mesh search service
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: MeshSearchService.cs has stub implementations for query and MBID search. Need real mesh search.

- [x] **T-1419**: Implement real disaster mode recovery
  - Status: Not started
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: DisasterModeRecovery.cs is marked as stub. Need real recovery logic.

- [x] **T-1420**: Implement real bridge API
  - Status: Not started
  - Priority: P2
  - Branch: experimental/brainz
  - Notes: BridgeApi.cs has stub implementations for search, download, and rooms. Need real bridge API.

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

