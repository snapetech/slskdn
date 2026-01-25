# Active Context

> What is currently being worked on in this repository.  
> Update this file when starting or finishing work.

---

## 🚨 Before Ending Your Session

**Did you fix any bugs? Document them in `adr-0001-known-gotchas.md` NOW.**

This is the #1 most important thing to do before ending a session. Future AI agents (and humans) will thank you.

---

## Current Session

- **Current Task**: slskd.Tests.Unit Re-enablement; dev/40-fixes maintenance
- **Branch**: `dev/40-fixes`
- **Environment**: Local dev
- **Last Activity**: Re-enabled MediaCore IpldMapperTests (Phase 5); slskd.Tests.Unit 1452 pass, 18 skipped. FuzzyMatcherTests remains excluded (Score/ScorePhonetic/FindSimilar expectations differ from impl). Next: Phase 5 (FederationService, MediaCore MetadataPortability) or Phase 3 PodCore per `docs/dev/40-fixes-plan.md`.

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
1. **slskd.Tests.Unit Re-enablement** — Continue Phase 2–6 per `docs/dev/40-fixes-plan.md`: Phase 2 (Common Moderation/Security/Files), Phase 3 (PodCore), Phase 4 (Mesh), Phase 5 (SocialFederation, MediaCore, etc.), Phase 6 (VirtualSoulfind). Un-skip or fix LocalPortForwarderTests’ 6 skipped tests when internal API/JSON alignment is done.
2. **Phase 14 COMPLETED** - Tier-1 Pod-Scoped Private Service Network feature fully integrated
   - 21 tasks defined across 6 task groups (T-1400 to T-1452)
   - Comprehensive agent implementation document created
   - Security threat model and acceptance criteria documented
   - Task dashboard and progress tracking updated

3. **Immediate Priority**: Begin Phase 14 implementation with T-1400 (Pod Policy Model & Persistence)
   - Start with pod capability flags and policy fields
   - Enable private service gateway functionality
   - Establish foundation for gateway service implementation

4. **High Priority Tasks Available**:
   - **Packaging**: T-010 to T-013 (NAS/docker packaging - 4 tasks)
   - **Medium Priority**: T-003 (Download Queue Position Polling), T-004 (Visual Group Indicators)
   - **Phase 2 Continuation**: T-404+ (Advanced codec fingerprinting and quality analysis)

5. **Implementation Timeline**:
   - **Phase 14**: ~12-16 weeks (21 tasks - VPN-like utility)
   - Phase 2 + 2-Extended: ~8-10 weeks (T-404+)
   - Phase 6 + 6X: ~20-26 weeks (T-823+)
   - Phase 7: Parallel with features (4-6 weeks)
   - **Total remaining: ~44-58 weeks** (including new Phase 14)

6. **Branch Strategy**: Phase 14 should use `experimental/pod-vpn` branch for development

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

