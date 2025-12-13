# Active Context

> What is currently being worked on in this repository.  
> Update this file when starting or finishing work.

---

## 🚨 Before Ending Your Session

**Did you fix any bugs? Document them in `adr-0001-known-gotchas.md` NOW.**

This is the #1 most important thing to do before ending a session. Future AI agents (and humans) will thank you.

---

## Current Session

- **Current Task**: T-1300 STUN NAT Detection - ✅ **COMPLETED**
- **Branch**: `experimental/whatAmIThinking` (T-823 + T-001 + T-002 + T-003 + T-004 + T-005 + T-006 + T-007 + T-1300 implementations)
- **Environment**: Local dev
- **Last Activity**: Completed T-1300 real STUN NAT detection replacing Unknown stub

---

## Recent Context

### Last Session Summary
- Completed Phase 1 (MusicBrainz/Chromaprint integration) T-300 through T-313
- Implemented Phase 2A tasks T-400 through T-403 (AudioVariant, canonical scoring, library health scaffolding)
- **Extended planning with critical additions**:
  - **Phase 2-Extended**: Codec-specific fingerprinting & quality heuristics (T-420 to T-430)
    - FLAC 42-byte streaminfo hash + PCM MD5
    - MP3 tag-stripped stream hash + encoder detection
    - Opus/AAC stream hashes + spectral analysis
    - Cross-codec deduplication via audio_sketch_hash
    - Heuristic versioning & recomputation system
  - **Phase 7**: Testing strategy with Soulfind & mesh simulator (T-900 to T-915)
    - L0/L1/L2/L3 test layers
    - Soulfind test harness (dev-only, never production)
    - Multi-client integration tests (Alice/Bob/Carol topology)
    - Mesh simulator for disaster mode testing
    - CI/CD integration with test categorization
- **Previously completed comprehensive planning for ALL phases (2-6)**:
  - Phase 2: 6 documents (~25,000 lines) - Canonical scoring, Library health, Swarm scheduling, Rescue mode, Codec-specific fingerprinting
  - Phase 3: 1 document (4,100 lines) - Discovery, Reputation, Fairness
  - Phase 4: 1 document (3,200 lines) - Manifests, Session traces, Advanced features
  - Phase 5: 1 document (2,800 lines) - Soulbeet integration
  - Phase 6: 4 documents (11,200 lines) - Virtual Soulfind mesh, disaster mode, compatibility bridge
  - Phase 7: 1 document (6,500 lines) - Testing strategy
- **Total: 21 planning documents, ~57,000 lines of production-ready specifications**
- **Total tasks: 127 (T-300 to T-915, plus misc)**

### Blocking Issues
- None currently

### Next Steps
1. **T-823, T-001 & T-002 COMPLETED** - All immediate high-priority features delivered (mesh search, chat tabs, scheduled limits)
2. **Immediate Priority**: Continue Phase 2 implementation (T-404 onwards) for advanced codec fingerprinting and quality analysis
3. **Alternative Path**: Medium priority tasks (T-003: Download Queue Position Polling, T-004: Visual Group Indicators)
4. **Packaging**: High priority packaging tasks available (T-010 to T-013: NAS/docker packaging)
5. **Phase 6 Continuation**: T-850 to T-860 (Phase 6X) - Legacy client compatibility bridge when ready
6. **Testing**: Implement Phase 7 testing infrastructure in parallel (T-900 to T-915)
7. **Branch Strategy**: Phase 6 features should move to `experimental/virtual-soulfind` branch
8. **Implementation Timeline**:
   - Phase 2 + 2-Extended: ~8-10 weeks (T-404+)
   - Phase 6 + 6X: ~20-26 weeks (T-823+)
   - Phase 7: Parallel with features (4-6 weeks)
   - **Total remaining: ~32-46 weeks**

---

## Environment Notes

- **Backend Port**: 5030 (default)
- **Frontend Dev Port**: 3000 (CRA default)
- **.NET Version**: 8.0
- **Node Version**: Check `package.json` engines

---

## Quick Commands

```bash
# Start backend (watch mode)
./bin/watch

# Start frontend dev server
cd src/web && npm start

# Run all tests
dotnet test

# Build release
./bin/build
```

