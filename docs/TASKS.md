# Task List & Roadmap

## ✅ Completed Tasks (December 2025)

### Enhancement 1: Ed25519 Cryptography
- [x] Add NSec.Cryptography package
- [x] Implement real Ed25519 key generation in `LocalMeshIdentityService`
- [x] Implement `Sign()` method
- [x] Implement `Verify()` method
- [x] Add `CreateSignedDescriptor()` method
- [x] Update `MeshPeerDescriptor.VerifySignature()` with real implementation
- [x] Add PublicKey/Signature fields to overlay messages
- [x] Update handshake protocol to sign payloads
- [x] Sign handshakes in `MeshOverlayConnector`
- [x] Sign handshakes in `MeshOverlayServer`
- [x] Test end-to-end signature verification
- [x] Document security improvements

### Enhancement 2: Mesh-First Swarm
- [x] Verify `SwarmSource` uses `MeshPeerId` as primary key
- [x] Verify orchestrator prioritizes overlay transport
- [x] Document mesh-first design
- [x] Confirm chunk downloads use MeshPeerId lookup
- [x] Add transport prioritization logic

### Enhancement 3: BitTorrent Rendezvous
- [x] Create `BitTorrentOptions` configuration class
- [x] Implement `RendezvousTorrentService` as `IHostedService`
- [x] Create deterministic rendezvous file generation
- [x] Implement deterministic .torrent creation
- [x] Configure MonoTorrent ClientEngine
- [x] Add torrent to engine and start seeding
- [x] Register service in DI (`Program.cs`)
- [x] Add config toggle (default: disabled)
- [x] Document configuration
- [x] Test torrent creation and seeding

### Soulseek Community Bridge
- [x] Create `SoulseekMeshBridgeService`
- [x] Implement `OnNeighborAdded` event handler
- [x] Implement periodic bridging timer (5 minutes)
- [x] Call `GetUserInfoAsync()` to register with Soulseek
- [x] Call `GetUserEndPointAsync()` for connectivity
- [x] Track bridged users to avoid duplicates
- [x] Create `MeshSearchBridgeService`
- [x] Design search result supplementation
- [x] Register both services in DI
- [x] Document bridge strategy and philosophy

### Documentation
- [x] Create `ENHANCEMENTS_COMPLETE.md`
- [x] Create `SOULSEEK_BRIDGE_STRATEGY.md`
- [x] Create `DEVELOPMENT_PROGRESS_DEC_2025.md`
- [x] Create `RELEASE_SUMMARY_DEC_2025.md`
- [x] Create `QUICK_REFERENCE.md`
- [x] Create `INDEX.md`
- [x] Update `MESH_IDENTITY_PROGRESS.md`

---

## ⏳ In Progress

### Hash Database Search
- [ ] Implement `SearchMeshHashDatabaseAsync()` in `MeshSearchBridgeService`
  - Parse search query into terms/tags
  - Query hash database for matching files
  - Resolve mesh peer IDs to usernames
  - Format results for Soulseek compatibility
  - Priority: **High**
  - Est. Time: 2-3 days

### Search Service Integration
- [ ] Hook `MeshSearchBridgeService` into `SearchService.SearchAsync()`
  - Call `GetMeshSupplementalResponsesAsync()` during result aggregation
  - Merge mesh results with Soulseek results
  - Update UI to indicate supplemental sources
  - Add metrics for supplemental result counts
  - Priority: **High**
  - Est. Time: 1-2 days

---

## 📋 TODO: High Priority

### BitTorrent Extension Protocol
- [ ] Research MonoTorrent 3.x extension API
  - Study `PeerConnection` and extension registration
  - Design mesh identity exchange format
  - Document extension protocol specification
- [ ] Implement `SlskdnMeshExtension` handler
  - Register extension with TorrentManager
  - Send mesh peer info in extension handshake
  - Parse received mesh peer info
- [ ] Auto-register BT-discovered peers
  - Create `MeshPeerDescriptor` from BT peer
  - Call `IMeshPeerRegistry.RegisterOrUpdateAsync()`
  - Trigger `SoulseekMeshBridgeService` bridging
- [ ] Add BT peer metrics
  - Track BT-discovered peer count
  - Log successful extensions
  - Monitor bridge success rate
- **Priority**: High
- **Est. Time**: 4-5 days
- **Depends On**: None

### Production Testing on kspls0
- [ ] Deploy current build to kspls0
- [ ] Enable DHT and monitor for 24 hours
- [ ] Check Ed25519 signature verification in logs
- [ ] Monitor bridge activity
- [ ] Verify mesh peer discovery
- [ ] Test with BitTorrent enabled
- [ ] Collect metrics and performance data
- [ ] Document any issues found
- **Priority**: High
- **Est. Time**: 2-3 days
- **Depends On**: None

---

## 📋 TODO: Medium Priority

### Mesh Data Plane
- [ ] Design mesh chunk transfer protocol
  - Define request/response messages
  - Specify byte range requests
  - Add flow control and congestion management
- [ ] Implement mesh transport in `SwarmDownloadOrchestrator`
  - Add "mesh" case to `DownloadChunkAsync()`
  - Use `MeshNeighborRegistry` to get connections
  - Send chunk request over overlay connection
  - Receive and verify chunk data
- [ ] Add mesh transport metrics
  - Track mesh chunk downloads
  - Compare speeds: overlay vs Soulseek
  - Monitor reliability
- **Priority**: Medium
- **Est. Time**: 5-7 days
- **Depends On**: None

### Metrics Dashboard
- [ ] Design metrics data model
  - Bridge stats (peers bridged, success rate)
  - Discovery stats (DHT, BT, Soulseek)
  - Transfer stats (overlay, Soulseek, combined)
  - Crypto stats (signatures verified, failures)
- [ ] Create API endpoints
  - `/api/v0/metrics/bridge`
  - `/api/v0/metrics/discovery`
  - `/api/v0/metrics/crypto`
- [ ] Add UI components
  - Real-time dashboard
  - Historical charts
  - Performance comparisons
- **Priority**: Medium
- **Est. Time**: 4-5 days
- **Depends On**: None

### Descriptor Rotation
- [ ] Add TTL field to `MeshPeerDescriptor`
- [ ] Implement automatic re-signing in `LocalMeshIdentityService`
  - Check descriptor age on access
  - Re-sign if approaching TTL
  - Broadcast updated descriptor
- [ ] Add descriptor revocation mechanism
  - Revocation list or timestamp-based
  - Distribute revocations via DHT
  - Check revocation status before accepting
- **Priority**: Medium
- **Est. Time**: 3-4 days
- **Depends On**: None

---

## 📋 TODO: Low Priority

### Smart Bridging
- [ ] Integrate with `PeerReputationService`
  - Only bridge peers with reputation > threshold
  - Prioritize high-reputation peers
  - Skip known bad actors
- [ ] Content-based prioritization
  - Bridge peers with rare/valuable content first
  - Use hash DB to identify rare files
  - Weight by file scarcity
- [ ] Rate limiting and throttling
  - Adjust bridge rate based on server load
  - Back off on errors
  - Implement exponential backoff
- **Priority**: Low
- **Est. Time**: 3-4 days
- **Depends On**: Hash DB search, reputation system

### Bidirectional Sync
- [ ] Design Soulseek → Mesh sync
  - Hook into Soulseek search responses
  - Extract file metadata
  - Add to mesh hash database
- [ ] Implement sync service
  - Monitor Soulseek activity
  - Parse and normalize file metadata
  - Insert into hash DB
- [ ] Add sync metrics
  - Track files added from Soulseek
  - Track files added from mesh
  - Show bidirectional flow
- **Priority**: Low
- **Est. Time**: 4-5 days
- **Depends On**: Hash DB search

### Advanced NAT Traversal
- [ ] Research STUN/TURN integration
- [ ] Implement hole punching for symmetric NAT
- [ ] Add relay fallback for unreachable peers
- [ ] Test with various NAT types
- **Priority**: Low
- **Est. Time**: 5-7 days
- **Depends On**: None

---

## 🔮 Future Ideas (Backlog)

### Community Features
- [ ] Mesh peer reputation propagation
- [ ] Collaborative filtering for recommendations
- [ ] Distributed wishlist matching
- [ ] Pod-based community features integration

### Performance Optimizations
- [ ] Connection pooling for overlay
- [ ] Chunk request pipelining
- [ ] Adaptive transport selection
- [ ] Peer quality scoring

### Security Enhancements
- [ ] Certificate pinning (TOFU) for overlay
- [ ] Encrypted overlay connections with TLS 1.3
- [ ] Forward secrecy for mesh messages
- [ ] Security audit and penetration testing

### Developer Experience
- [ ] Automated integration tests
- [ ] Performance benchmarking suite
- [ ] Debug UI for mesh state
- [ ] Development environment automation

---

## 📊 Progress Summary

### Overall Status
- **Completed**: 51 tasks (3 major enhancements + bridge + docs)
- **In Progress**: 2 tasks (hash DB + search integration)
- **High Priority**: 2 tasks (BT extension + production testing)
- **Medium Priority**: 3 tasks (mesh data plane + metrics + rotation)
- **Low Priority**: 3 tasks (smart bridging + bidirectional + NAT)
- **Backlog**: 4 categories (community + perf + security + DX)

### Estimated Timeline
- **This Week**: Complete in-progress tasks
- **This Month**: High priority tasks
- **This Quarter**: Medium priority tasks
- **Future**: Low priority and backlog

---

## 🎯 Sprint Planning

### Sprint 1 (This Week)
1. Complete hash DB search implementation
2. Integrate search bridge with SearchService
3. Test on kspls0 (initial deployment)

### Sprint 2 (Week 2)
1. Implement BitTorrent extension protocol
2. Production testing on kspls0 (full 24h)
3. Begin mesh data plane design

### Sprint 3 (Week 3-4)
1. Complete mesh data plane implementation
2. Create metrics dashboard
3. Deploy to production

### Sprint 4 (Month 2)
1. Descriptor rotation
2. Smart bridging heuristics
3. Performance optimization

---

## 📝 Notes

### Dependencies
- Hash DB search → Search integration
- BT extension → Auto-registration flow
- Mesh data plane → Transport metrics
- All features → Metrics dashboard

### Risks
- MonoTorrent 3.x API may change (BT extension)
- Hash DB performance at scale (search)
- Soulseek server rate limiting (bridge)
- NAT traversal complexity (symmetric NAT)

### Success Criteria
- [ ] Hash DB search returns results in <100ms
- [ ] Bridge services maintain <1% error rate
- [ ] BT extension discovers 10+ peers per instance
- [ ] Mesh data plane achieves 80% of Soulseek speed
- [ ] Production testing shows no regressions

---

*Task List Last Updated: December 11, 2025*
