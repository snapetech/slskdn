# 🚀 START HERE - Implementation Guide for Codex

> **You are here**: All planning is complete. You will implement 217+ fully-specified tasks.  
> **Timeline**: ~80 weeks conservative, ~40 weeks optimistic  
> **Your job**: Execute. No design decisions needed.

---

## 🆕 NEW: Phase 12 - Adversarial Resilience & Privacy Hardening

> **100 new tasks** for protecting users in adversarial environments (dissidents, journalists, activists).  
> **Design doc**: `docs/phase12-adversarial-resilience-design.md`  
> **All features OPTIONAL and DISABLED by default, configurable via WebGUI.**

### Phase 12 Quick Overview
- **12A**: Privacy Layer (padding, timing, batching) — hide message patterns
- **12B**: Anonymity Layer (Tor, I2P, relays) — hide IP addresses  
- **12C**: Obfuscated Transports (WebSocket, obfs4, Meek) — evade DPI
- **12D**: Native Onion Routing — build circuits within mesh
- **12E**: Censorship Resistance (bridges, domain fronting) — bypass firewalls
- **12F**: Plausible Deniability (hidden volumes, decoy pods) — legal protection
- **12G-H**: WebGUI integration, testing, documentation

---

## ✅ What's Already Done

**Phase 1 (T-300 to T-313)**: ✅ COMPLETE
- MusicBrainz integration
- Acoustic fingerprinting (Chromaprint)
- Album completion tracking
- MBID-aware multi-swarm
- **Status**: Implemented, tested, merged to `experimental/brainz`

---

## 🎯 Your Mission: Implement Phases 2-6

### Phase Order (Sequential)

```
Phase 2 → Phase 3 → Phase 4 → Phase 5 → Phase 6 → Phase 6X (optional)
 (8wk)    (10wk)     (8wk)      (6wk)     (16wk)     (4wk)
```

---

## 📖 How to Start

### Step 1: Read the Overview
```bash
cd <repo-root>/docs
cat FINAL_PLANNING_SUMMARY.md  # Big picture (10 min read)
```

### Step 2: Read Phase 2 Overview
```bash
cat phase2-implementation-guide.md  # Your first phase (20 min read)
```

### Step 3: Start Task T-400
```bash
cat phase2-canonical-scoring-design.md  # Your first task (30 min read)
```

### Step 4: Implement
```bash
# You'll see exact:
# - C# type definitions (copy/paste ready)
# - Method signatures
# - SQL schemas
# - Configuration keys
# - API routes
# - Test scenarios

# Just implement what you read!
```

---

## 📋 Task Checklist (For Each Task)

```
[ ] 1. Read task section in design doc
[ ] 2. Create required files (types, services, controllers)
[ ] 3. Add database migrations if needed
[ ] 4. Wire up dependency injection in Program.cs
[ ] 5. Implement methods using provided logic
[ ] 6. Add configuration keys to appsettings.json
[ ] 7. Write tests (specs provided)
[ ] 8. Run tests
[ ] 9. Update memory-bank/tasks.md (mark Done)
[ ] 10. Move to next task
```

---

## 📚 Documentation Map

### Must-Read (In Order)
1. **`FINAL_PLANNING_SUMMARY.md`** ← Start here (overview)
2. **`COMPLETE_PLANNING_INDEX.md`** ← Navigation map
3. **`phase2-implementation-guide.md`** ← Phase 2 overview
4. **`phase2-canonical-scoring-design.md`** ← First task (T-400)

### Then Follow This Sequence
**Phase 2** (12 tasks):
- `phase2-canonical-scoring-design.md` (T-400 to T-402)
- `phase2-library-health-design.md` (T-403 to T-405)
- `phase2-swarm-scheduling-design.md` (T-406 to T-408)
- `phase2-rescue-mode-design.md` (T-409 to T-411)

**Phase 3** (11 tasks):
- `phase3-discovery-reputation-fairness-design.md` (T-500 to T-510)

**Phase 4** (12 tasks):
- `phase4-manifests-traces-advanced-design.md` (T-600 to T-611)

**Phase 5** (13 tasks):
- `phase5-soulbeet-integration-design.md` (T-700 to T-712)

**Phase 6** (41 tasks):
- `virtual-soulfind-mesh-architecture.md` (architecture overview)
- `phase6-virtual-soulfind-implementation-design.md` (T-800 to T-840)

**Phase 6X** (11 tasks, OPTIONAL):
- `phase6-compatibility-bridge-design.md` (T-850 to T-860)

**Phase 7-11**: Testing, MeshCore, MediaCore, PodCore, Refactoring
- See `docs/phase7-testing-strategy-soulfind.md` through `docs/phase11-refactor-summary.md`

**Phase 12** (100 tasks, NEW):
- `phase12-adversarial-resilience-design.md` (T-1200 to T-1299)
- Privacy/anonymity/obfuscation/censorship-resistance layers
- All features optional, disabled by default, configurable via WebGUI

---

## 🎨 Example: Your First Task (T-400)

### Task: Implement Local Quality Scoring

**What you'll find in the doc**:

```csharp
// Exact type definition to create:
namespace slskd.Canonical
{
    public class AudioVariant
    {
        public string FileHash { get; set; }  // SHA256
        public string RecordingId { get; set; }  // MB Recording ID
        public string Codec { get; set; }
        public int? BitDepth { get; set; }
        // ... (20+ properties, all specified)
    }
}
```

**Your job**:
1. Create file: `src/slskd/Canonical/Models/AudioVariant.cs`
2. Copy the type definition (it's complete)
3. Create the database table (schema provided)
4. Implement `QualityScorer` (algorithm provided)
5. Write tests (scenarios provided)
6. Mark T-400 done in `memory-bank/tasks.md`

**Everything is specified. Just execute.**

---

## 🔧 Git Workflow

### Branches
- **Phase 2-5**: Use `experimental/brainz` (continue from Phase 1)
- **Phase 6**: Create new branch `experimental/virtual-soulfind`

### Commits
```bash
# Good commit messages:
git commit -m "feat(canonical): implement AudioVariant model and quality scorer (T-400)"
git commit -m "feat(library): add library health scanner service (T-403)"
git commit -m "feat(swarm): implement cost-based peer scheduler (T-406)"
```

### When to Commit
- After each task completes
- After tests pass
- Before moving to next task

---

## 🧪 Testing Strategy

### For Each Task
- **Unit tests**: Test individual components
- **Integration tests**: Test service interactions
- **API tests**: Test endpoints if added

### Test Files Location
```
tests/slskd.Tests/
├── Canonical/
│   ├── QualityScorerTests.cs
│   └── TranscodeDetectorTests.cs
├── LibraryHealth/
│   └── LibraryHealthServiceTests.cs
└── ...
```

### Running Tests
```bash
cd tests/slskd.Tests
dotnet test
```

---

## 📊 Progress Tracking

### Update Tasks File
```bash
# After completing T-400:
# Edit: memory-bank/tasks.md
# Change:
- [x] **T-400**: Implement local quality scoring
  - Status: Not started
# To:
- [x] **T-400**: Implement local quality scoring
  - Status: Done
  - Completed: 2025-12-XX
```

### Check Your Progress
```bash
# Count completed tasks:
grep -c "Status: Done" memory-bank/tasks.md

# Count remaining:
grep -c "Status: Not started" memory-bank/tasks.md
```

---

## 🚨 When You Get Stuck

### 1. Re-read the Design Doc
Every question should be answered in the spec.

### 2. Check Related Implementations
Look at Phase 1 code for patterns:
- `src/slskd/Integrations/MusicBrainz/` (API clients)
- `src/slskd/Integrations/Chromaprint/` (Services)
- `src/slskd/HashDb/` (Database patterns)

### 3. Check Conventions
See: `.cursor/rules/slskdn-conventions.mdc`

### 4. Ask User
If truly stuck (should be rare - everything is specified).

---

## ⚡ Quick Start Commands

```bash
# Navigate to project
cd <repo-root>

# Checkout correct branch
git checkout experimental/brainz

# Read overview
cat docs/FINAL_PLANNING_SUMMARY.md

# Read Phase 2 guide
cat docs/phase2-implementation-guide.md

# Read first task
cat docs/phase2-canonical-scoring-design.md

# Create first file
mkdir -p src/slskd/Canonical/Models
touch src/slskd/Canonical/Models/AudioVariant.cs

# Start coding!
```

---

## 🎯 Success Metrics

### You'll Know You're Done When:

**Phase 2 Complete**:
- [x] Quality scores computed for all audio files
- [x] Library health dashboard shows issues
- [x] Swarm scheduler uses cost-based peer selection
- [x] Rescue mode activates for stalled transfers

**Phase 3 Complete**:
- [x] Can download entire artist discographies
- [x] Can create label crates (popular releases)
- [x] Peer reputation tracked and used in scheduling
- [x] Fairness constraints enforced

**Phase 4 Complete**:
- [x] Jobs export/import as YAML
- [x] Session traces available for debugging
- [x] Warm cache nodes operational
- [x] Playback-aware swarming works

**Phase 5 Complete**:
- [x] Soulbeet connects to slskdn
- [ ] MBID jobs work via API
- [x] Integration tests pass

**Phase 6 Complete**:
- [x] Shadow index operational
- [x] Scenes functional
- [x] Disaster mode works (simulated outage)
- [x] Mesh-only operation tested

**Phase 6X Complete** (Optional):
- [x] Legacy clients connect to bridge
- [x] Nicotine+ integration test passes
- [x] MBID enhancement transparent to legacy clients

**Phase 12 Complete** (Adversarial Resilience):
- [x] Privacy layer operational (padding, timing, batching)
- [x] Tor integration working (all traffic through SOCKS5)
- [x] Obfuscated transports available (WebSocket, obfs4)
- [x] Native onion routing circuits built within mesh
- [x] Bridges operational for censored users
- [x] WebGUI settings panel complete with presets
- [x] User documentation comprehensive

---

## 🏆 The Big Picture

### You're Building:
A **next-generation P2P music network** that:
- Works with existing Soulseek today
- Survives without Soulseek tomorrow
- Makes every client smarter
- Extends benefits to legacy clients
- Remains fully decentralized

### The Vision:
```
Today:
  Your slskdn + Soulseek → Basic P2P

After Phase 2-5:
  Your slskdn + Soulseek + DHT + Overlay → Smart P2P
  (MBID-aware, quality-aware, fair, fast)

After Phase 6:
  Your slskdn + DHT + Overlay → Unstoppable P2P
  (Works without Soulseek, disaster-ready, mesh-powered)

After Phase 6X:
  Any client → Your bridge → Mesh
  (Entire ecosystem benefits)
```

---

## 📝 Notes

### Design Decisions
**You don't need to make any.** Everything is specified:
- ✅ Type definitions
- ✅ Method signatures
- ✅ Database schemas
- ✅ Configuration keys
- ✅ API routes
- ✅ UI components
- ✅ Test scenarios

### Dependencies
Already in place:
- ✅ .NET 8
- ✅ SQLite
- ✅ React frontend
- ✅ DHT library (Phase 1)
- ✅ MusicBrainz client (Phase 1)
- ✅ Chromaprint (Phase 1)

### Code Style
Follow existing patterns in the codebase:
- Dependency injection for services
- `IOptionsMonitor<Options>` for config
- Xunit + Moq for tests
- Async/await throughout
- Logging with Serilog

---

## ✅ Ready to Start?

**Your first command**:
```bash
cd <repo-root>/docs && cat FINAL_PLANNING_SUMMARY.md
```

**Then**:
```bash
cat phase2-implementation-guide.md
```

**Then**:
```bash
cat phase2-canonical-scoring-design.md
```

**Then**: Start coding Task T-400! 🚀

---

## 🎉 Final Words

You have:
- ✅ 20+ comprehensive design documents
- ✅ ~70,000 lines of specifications
- ✅ 317 fully-planned tasks (217 complete + 100 Phase 12)
- ✅ Complete type definitions
- ✅ Complete test scenarios
- ✅ Zero ambiguity

**Your job is pure execution. Everything else is done.**

**Good luck! Let's build something revolutionary.** 🚀

---

**Questions? Start here**: `docs/FINAL_PLANNING_SUMMARY.md`  
**Task list**: `memory-bank/tasks.md`  
**Task dashboard**: `docs/TASK_STATUS_DASHBOARD.md`  
**Phase 12 (new)**: `docs/phase12-adversarial-resilience-design.md`  
**Current status**: Phases 1-11 complete, Phase 12 ready to start

