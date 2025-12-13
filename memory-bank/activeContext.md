# Active Context

> What is currently being worked on in this repository.  
> Update this file when starting or finishing work.

---

## 🚨 Before Ending Your Session

**Did you fix any bugs? Document them in `adr-0001-known-gotchas.md` NOW.**

This is the #1 most important thing to do before ending a session. Future AI agents (and humans) will thank you.

---

## Current Session

- **Current Task**: Phase 12S hardening plus UI polish (chat/room tab persistence), download queue polling, search group indicators, traffic ticker, predictable search URLs, adversarial config model, and privacy padding
- **Branch**: `experimental/brainz` (Phase 1, 2-Extended, 7) → `experimental/virtual-soulfind` (Phase 6)
- **Environment**: Local dev
- **Last Activity**: Added mesh sync security documentation (T-1439), consensus gate, and PoP integration (including responder using local shares); implemented persistent tabs for chat and rooms (T-001). Tests blocked by socket permissions in this environment and not run by request.

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
1. Continue Phase 12S validation once test environment permits (re-run `dotnet test` when socket/listener permissions allow).
2. Extend privacy layer tasks in Phase 12A (AdversarialOptions model, padding/jitter) when ready to resume core Phase 12 work.
3. Monitor chat/room tab persistence UX and align with Browse tab patterns if further polish is needed.
4. Validate group badges in search results against user-defined and built-in groups once more user data is available.
5. Observe traffic ticker accuracy against backend speeds when real transfers are running.
6. T-006 is blocked pending Soulseek room creation research; unblock when protocol/back-end support is clarified.
7. Confirm predictable search URLs (`?q=`) behave across sessions and when searches are cleared.
8. Wire new AdversarialOptions into Phase 12 features when implementing privacy/anonymity layers.
9. Integrate IMessagePadder into overlay messaging when privacy layer work (T-1214) begins.

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
