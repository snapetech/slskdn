# Comprehensive Audit Findings → Task Mapping

> **Date**: December 10, 2025  
> **Purpose**: Map all audit findings to concrete tasks

---

## Phase 1-7: New Tasks Needed

### Phase 1: MusicBrainz & Chromaprint Integration

| Finding | Task ID | Description | Priority |
|---------|---------|-------------|----------|
| Unified Brainz Client placeholder | **T-1400** | Implement unified BrainzClient (MB/AcoustID/Soulbeet) with caching and backoff | P2 |

---

### Phase 2: Canonical Scoring & Library Health

| Finding | Task ID | Description | Priority |
|---------|---------|-------------|----------|
| Library Health simplified placeholder | **T-1401** | Implement full library health scanning (deep analysis, issue detection) | P1 |
| Library Health remediation placeholder job IDs | **T-1402** | Implement library health remediation job execution (integrate with multi-source download) | P1 |
| Rescue Service multiple TODOs | **T-1403** | Complete rescue service implementation (output path, job cancellation, HashDb lookup, file hash computation, partial file path) | P1 |
| Swarm Download Orchestrator placeholder | **T-1404** | Implement actual chunk scheduling and download in swarm orchestrator | P1 |
| Chunk Scheduler reassignment TODO | **T-1405** | Implement chunk reassignment to better peers | P2 |
| Playback Feedback placeholder | **T-1406** | Integrate playback feedback with scheduling (currently placeholder) | P2 |
| Playback Priority buffer tracking placeholder | **T-1407** | Implement real buffer tracking (currently uses desired as proxy) | P2 |

---

### Phase 5: Soulbeet Integration

| Finding | Task ID | Description | Priority |
|---------|---------|-------------|----------|
| Search Compatibility Controller stub | **T-1408** | Implement real search compatibility endpoint (currently returns empty results) | P1 |
| Downloads Compatibility Controller stub | **T-1409** | Implement real downloads compatibility endpoints (create, list, get) | P1 |
| Jobs Controller missing features | **T-1410** | Add filtering, pagination, sorting to jobs API | P3 |

---

### Phase 6: Virtual Soulfind Mesh & Disaster Mode

| Finding | Task ID | Description | Priority |
|---------|---------|-------------|----------|
| Shadow Index ShardPublisher 11 TODOs | **T-1411** | Complete shadow index shard publishing implementation | P1 |
| Scene Services multiple TODOs | **T-1412** | Complete scene service implementations (SceneService, SceneChatService, ScenePubSubService, SceneAnnouncementService, SceneMembershipTracker) | P2 |
| Bridge Services TODOs | **T-1413** | Complete bridge service implementations (SoulfindBridgeService, BridgeApi, TransferProgressProxy) | P2 |
| Disaster Mode Services TODOs | **T-1414** | Complete disaster mode implementations (MeshTransferService, MeshSearchService, DisasterModeRecovery, SoulseekHealthMonitor) | P2 |
| Traffic Observer stub | **T-1415** | Implement real traffic observer (currently stub, capture disabled) | P2 |
| Normalization Pipeline stub | **T-1416** | Implement real normalization pipeline (currently stub) | P2 |
| Username Pseudonymizer stub | **T-1417** | Implement real pseudonymizer (currently returns deterministic GUIDs) | P2 |
| Mesh Search Service stub | **T-1418** | Implement real mesh search service (currently stub) | P1 |
| Disaster Mode Recovery stub | **T-1419** | Implement real disaster mode recovery (currently stub) | P2 |
| Bridge API stub | **T-1420** | Implement real bridge API (search, download, rooms - currently stubs) | P2 |

---

## Phase 8: New Tasks Needed

| Finding | Task ID | Description | Priority |
|---------|---------|-------------|----------|
| Ed25519 Key Generation stub | **T-1421** | Implement real Ed25519 keypair generation (currently generates random bytes) | P1 |
| Signature Verification stub | **T-1422** | Implement real Ed25519 signature verification (currently always returns true) | P1 |
| QUIC Overlay Server disabled | **T-1423** | Implement QUIC overlay server (currently disabled stub) | P1 |
| QUIC Overlay Client disabled | **T-1424** | Implement QUIC overlay client (currently disabled stub) | P1 |
| QUIC Data Server disabled | **T-1425** | Implement QUIC data-plane server (currently disabled stub) | P1 |
| QUIC Data Client disabled | **T-1426** | Implement QUIC data-plane client (currently disabled stub) | P1 |
| NAT Detector stub | **T-1300** | Switch default to StunNatDetector or remove stub NatDetector | P2 |
| Mesh Advanced route diagnostics | **T-1310** | Implement real route diagnostics (currently returns dummy data) | P3 |
| Mesh Advanced transport stats | **T-1311** | Wire up real transport statistics (currently returns zeros) | P3 |
| Mesh Sync neighbor queries | **T-1427** | Implement mesh neighbor queries for hash lookups | P2 |
| Mesh Sync username resolution | **T-1428** | Get actual username instead of hardcoded "slskdn" | P3 |
| Control Dispatcher verification | **T-1429** | Verify and fix control envelope signature verification | P2 |

---

## Phase 9: New Tasks Needed

| Finding | Task ID | Description | Priority |
|---------|---------|-------------|----------|
| Fuzzy Matcher placeholder algorithm | **T-1324** | Implement sophisticated fuzzy matching (Levenshtein, phonetic, fuzzywuzzy-style) | P2 |
| Content Publisher in-memory placeholder | **T-1326** | Integrate content publishing with library scanner/HashDb | P1 |
| IPLD/IPFS integration JSON only | **T-1322** | Implement IPFS client integration and DAG-CBOR encoding | P2 |
| ContentID Registry missing | **T-1320** | Implement ContentID registry service | P1 |
| Multi-domain addressing missing | **T-1321** | Implement multi-domain content addressing | P1 |
| Perceptual hash computation missing | **T-1323** | Implement perceptual hash computation for audio | P2 |
| Metadata portability missing | **T-1325** | Implement metadata portability layer | P3 |
| Descriptor query/retrieval advanced | **T-1327** | Implement advanced descriptor querying (by hash, metadata, perceptual hash) | P2 |

---

## Phase 10: New Tasks Needed

| Finding | Task ID | Description | Priority |
|---------|---------|-------------|----------|
| Pod Service join/leave/ban stubs | **T-1344** | Implement real pod join/leave/ban (currently no-ops) | P1 |
| Pod Messaging stub | **T-1345, T-1346, T-1347, T-1348** | Implement message routing, signature verification, deduplication, storage | P1 |
| Soulseek Chat Bridge stub | **T-1356, T-1357** | Implement readonly and mirror modes for chat bridge | P1 |
| Pod Discovery missing | **T-1343** | Implement pod discovery via DHT | P1 |
| Pod Metadata Publishing missing | **T-1340** | Implement pod metadata publishing to DHT | P1 |
| Signed Membership missing | **T-1341, T-1342** | Implement signed membership records and verification | P1 |
| Pod Channels model only | **T-1350** | Implement pod channel functionality | P2 |
| Content-Linked Pods model only | **T-1351, T-1352** | Implement content-linked pod creation and variant opinion publishing | P2 |
| Pod UI missing | **T-1360, T-1361** | Create pod list/detail UI and chat UI (zero JSX files) | P1 |
| Pod Trust/Moderation missing | **T-1354, T-1355** | Implement PodAffinity scoring and kick/ban with signed updates | P2 |

---

## Phase 11: All Fixed ✅

All Phase 11 issues have been resolved (December 10, 2025).

---

## Phase 12: Not Started ⚪

Phase 12 has zero implementation (as expected, not started).

---

## Summary: New Tasks by Priority

### P1 (Critical) - 25 tasks
- T-1401, T-1402, T-1403, T-1404 (Phase 2)
- T-1408, T-1409 (Phase 5)
- T-1411, T-1418 (Phase 6)
- T-1421, T-1422, T-1423, T-1424, T-1425, T-1426 (Phase 8)
- T-1320, T-1321, T-1326 (Phase 9)
- T-1340, T-1341, T-1342, T-1343, T-1344, T-1345, T-1346, T-1347, T-1348, T-1356, T-1357, T-1360, T-1361 (Phase 10)

### P2 (High) - 20 tasks
- T-1405, T-1406, T-1407 (Phase 2)
- T-1412, T-1413, T-1414, T-1415, T-1416, T-1417, T-1419, T-1420 (Phase 6)
- T-1300, T-1427, T-1429 (Phase 8)
- T-1322, T-1323, T-1324, T-1327 (Phase 9)
- T-1350, T-1351, T-1352, T-1354, T-1355 (Phase 10)

### P3 (Medium) - 4 tasks
- T-1410 (Phase 5)
- T-1310, T-1311, T-1428 (Phase 8)

**TOTAL NEW TASKS**: **49 tasks**

---

## Existing Tasks That Need Completion

Many tasks already exist in `memory-bank/tasks.md` but are marked as "Not started". These should be prioritized based on audit findings.

---

*Last Updated: December 10, 2025*















