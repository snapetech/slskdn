# Active Context

> What is currently being worked on in this repository.  
> Update this file when starting or finishing work.

---

## 🚨 Before Ending Your Session

**Did you fix any bugs? Document them in `adr-0001-known-gotchas.md` NOW.**

This is the #1 most important thing to do before ending a session. Future AI agents (and humans) will thank you.

---

## Current Session

- **Current Task**: Extended planning with Phase 2-Extended (codec-specific fingerprinting) and Phase 7 (testing strategy)
- **Branch**: `experimental/brainz` (Phase 1, 2-Extended, 7) → `experimental/virtual-soulfind` (Phase 6)
- **Environment**: Local dev
- **Last Activity**: Added Phase 2-Extended (T-420 to T-430) and Phase 7 (T-900 to T-915) specifications

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
1. Continue Codex implementation of Phase 2 tasks (T-404 onwards)
2. Phase 2-Extended (T-420 to T-430) provides deep codec-specific quality analysis
3. Phase 7 (T-900 to T-915) can be implemented in parallel with features as needed
4. **Phase 6X (T-850 to T-860) is OPTIONAL but RECOMMENDED** - compatibility bridge is the killer feature
5. Follow implementation order in phase docs
6. All tasks (T-300 to T-915) are fully specified - no additional planning needed
7. Estimated implementation time:
   - Phase 1: Complete
   - Phase 2 + 2-Extended: ~8-10 weeks
   - Phase 3-5: ~18-24 weeks
   - Phase 6 + 6X: ~20-26 weeks
   - Phase 7: Parallel with features (4-6 weeks)
   - **Total: ~50-66 weeks for complete implementation**

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

