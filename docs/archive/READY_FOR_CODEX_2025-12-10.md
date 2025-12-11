# Handoff to Codex: Next Phase Instructions

**Date**: December 10, 2025  
**Session**: Phase 2C-D Complete (T-406 through T-411)  
**Current Branch**: `experimental/brainz`  
**Working Directory**: `~/Documents/Code/slskdn`

---

## 🎉 Session Summary

**Completed**: 6 major tasks (T-406 to T-411)
- ✅ Phase 2C: RTT + Throughput-Aware Swarm Scheduler (3 tasks)
- ✅ Phase 2D: Rescue Mode Foundation (3 tasks)
- ✅ Backfill: Multi-source job creation in RescueService

**Overall Progress**: 25/160 tasks complete (16%)  
**Phase 2 Progress**: 11/22 tasks complete (50%)

---

## 📍 Current State

### Repository Location
```bash
cd ~/Documents/Code/slskdn
```

### Active Branch
```bash
git branch
# Should show: * experimental/brainz
```

### Latest Commits
```
f70298f9 docs: update task dashboard (25/160 complete, Phase 2 50%)
47be2db7 feat(transfers): add Soulseek-primary guardrails for rescue mode (T-411)
8e8db8cc feat(transfers): complete multi-source job creation in RescueService (backfill)
f8a57a09 feat(transfers): add rescue service for underperforming transfers (T-410)
6a79e3b2 feat(transfers): instrument downloads with peer metrics tracking (T-409)
```

### Key Files Modified This Session
- `src/slskd/Transfers/MultiSource/Metrics/` (3 new files)
- `src/slskd/Transfers/MultiSource/Scheduling/` (2 new files)
- `src/slskd/Transfers/Rescue/` (2 new files)
- `src/slskd/Transfers/Downloads/DownloadService.cs` (metrics integration)
- `src/slskd/Program.cs` (DI registration)

---

## 🎯 Next Tasks - Two Options

You can proceed with **either** Phase 2-Extended OR Phase 3 (they can run in parallel):

### Option 1: Phase 2-Extended - Advanced AudioVariant Fingerprinting

**Start with**: **T-420** (Extend AudioVariant model with codec-specific fields)

**Design Doc**: `~/Documents/Code/slskdn/docs/phase2-advanced-fingerprinting-design.md`

**Task Spec** (from `memory-bank/tasks.md`):
```
- [ ] **T-420**: Extend AudioVariant model with codec-specific fields
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Add FLAC streaminfo hash (42 bytes), MP3 stream hash, Opus stream hash, 
    AAC stream hash, audio_sketch_hash (PCM-window), spectral_fingerprint fields. 
    Add analyzer_version for migration tracking.
```

**Next Steps**:
1. Read design doc: `docs/phase2-advanced-fingerprinting-design.md`
2. Extend `src/slskd/Audio/AudioVariant.cs` with new codec-specific fields
3. Update database schema (add columns to AudioVariants table)
4. Proceed to T-421 (FLAC analyzer), T-422 (MP3 analyzer), etc.

**Estimated Duration**: 11 tasks, ~3-4 weeks

---

### Option 2: Phase 3 - Discovery, Reputation, and Fairness

**Start with**: **T-500** (Build MB artist release graph service)

**Design Doc**: `~/Documents/Code/slskdn/docs/phase3-discovery-reputation-fairness-design.md`

**Task Spec** (from `memory-bank/tasks.md`):
```
- [ ] **T-500**: Build MB artist release graph service
  - Status: Not started
  - Priority: P1
  - Branch: experimental/brainz
  - Notes: Query MusicBrainz for artist's full discography. Build release graph 
    with relationships (recordings, release groups, labels). Cache in local DB. 
    Expose API for discography job creation.
```

**Next Steps**:
1. Read design doc: `docs/phase3-discovery-reputation-fairness-design.md`
2. Create `src/slskd/Integrations/MusicBrainz/ReleaseGraphService.cs`
3. Implement MusicBrainz artist query (paginated, rate-limited)
4. Build local cache (SQLite table for release graphs)
5. Proceed to T-501 (Discography profiles), T-502 (Discography job type)

**Estimated Duration**: 11 tasks, ~3-4 weeks

---

## 🔧 Development Environment

### Prerequisites
- .NET 8 SDK
- SQLite (for local DB)
- All dependencies already installed (MusicBrainz client, AcoustID, Chromaprint, etc.)

### Build and Test
```bash
cd ~/Documents/Code/slskdn
dotnet build src/slskd/slskd.csproj
dotnet test  # When tests exist
```

### Run Application
```bash
cd ~/Documents/Code/slskdn
dotnet run --project src/slskd/slskd.csproj
```

---

## 📚 Key Documentation

### Design Documents (in `docs/`)
- `phase2-advanced-fingerprinting-design.md` - T-420 to T-430 specs
- `phase3-discovery-reputation-fairness-design.md` - T-500 to T-510 specs
- `phase2-rescue-mode-design.md` - Just completed (reference)
- `phase8-refactoring-design.md` - Future refactoring roadmap

### Memory Bank (in `memory-bank/`)
- `tasks.md` - Canonical task list (SOURCE OF TRUTH)
- `activeContext.md` - Current project context
- `adr-0001-known-gotchas.md` - Known issues and workarounds

### Task Dashboard
- `docs/TASK_STATUS_DASHBOARD.md` - Visual progress tracking

---

## 💡 Implementation Guidelines

### Code Style
- Follow existing patterns in `src/slskd/`
- Use dependency injection (register in `Program.cs`)
- Add XML documentation comments
- Follow StyleCop rules (warnings are okay for now)

### Testing
- Write unit tests as you go (when test infrastructure exists)
- Test with real MusicBrainz API calls (use rate limiting)
- Verify database migrations work correctly

### Git Workflow
1. Work on `experimental/brainz` branch
2. Commit after each task completion
3. Use descriptive commit messages (see recent commits as examples)
4. Update `memory-bank/tasks.md` after completing tasks
5. Push regularly to backup progress

### Task Completion Checklist
For each task (e.g., T-420):
1. ✅ Read the task spec in `memory-bank/tasks.md`
2. ✅ Read the relevant design doc
3. ✅ Implement the feature
4. ✅ Build and verify no errors
5. ✅ Update `memory-bank/tasks.md` (mark as done, add completion date)
6. ✅ Commit with descriptive message
7. ✅ Push to remote

---

## 🚀 Recommended Next Steps

**If continuing Phase 2** (codec analyzers):
```bash
cd ~/Documents/Code/slskdn
# Start with T-420
# Read: docs/phase2-advanced-fingerprinting-design.md
# Edit: src/slskd/Audio/AudioVariant.cs
```

**If starting Phase 3** (discovery):
```bash
cd ~/Documents/Code/slskdn
# Start with T-500
# Read: docs/phase3-discovery-reputation-fairness-design.md
# Create: src/slskd/Integrations/MusicBrainz/ReleaseGraphService.cs
```

---

## 📊 Progress Metrics

- **Milestones Complete**: 2/8
  - ✅ Milestone 1: MusicBrainz Foundation
  - ✅ Milestone 2: Quality-Aware Downloads
  - ✅ Milestone 4: Advanced Swarm Features

- **Current Velocity**: ~6 tasks per session
- **Estimated Remaining**: ~27 sessions (at current pace)

---

## 🎯 Success Criteria

**Phase 2-Extended Complete When**:
- All codec analyzers implemented (FLAC, MP3, Opus, AAC)
- Cross-codec deduplication working
- Audio sketch hash for PCM comparison
- Canonical stats include codec-specific metrics

**Phase 3 Complete When**:
- Discography jobs download full artist catalogs
- Label crate mode discovers popular releases
- Peer reputation influences swarm scheduling
- Fairness governor enforces upload/download ratios

---

## 📝 Notes

### Outstanding Backfills (from T-410)
- `t410-backfill-resolve`: Complete MBID resolution (HashDb lookup, fingerprinting)
- `t410-backfill-discover`: Complete overlay peer discovery (needs mesh/DHT)
- `t410-backfill-wire`: Wire RescueService to underperformance detector

**These can wait** until Phase 6 (mesh infrastructure) or when you need them.

### Known Dependencies
- Phase 6 (Virtual Soulfind Mesh) needed for full rescue mode overlay discovery
- Phase 7 (Testing) can run in parallel with any phase

---

**Good luck, Codex! You've got a solid foundation to build on.** 🚀

**Working Directory**: `~/Documents/Code/slskdn`  
**Branch**: `experimental/brainz`  
**Next Task Options**: T-420 (Phase 2-Extended) OR T-500 (Phase 3)


