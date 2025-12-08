# Scratch Pad (Experimental Branch)

> Free-form notes, ideas, and temporary thinking space.  
> This file can be truncated when it gets too long.  
> AI agents can use this for "thinking on paper" without worrying about cleanliness.

---

## Current Thoughts

(Empty - ready for use)

---

## Quick Reference: Security Components (30 total)

### Phase 1 - Foundation (8)
- `PathGuard.cs` - Path traversal prevention
- `ContentSafety.cs` - Magic byte verification
- `ViolationTracker.cs` - Auto-escalating bans
- `ConnectionFingerprint.cs` - Forensic logging
- `PeerReputation.cs` - Behavioral scoring
- `PrivacyMode.cs` - Metadata minimization
- `ParanoidMode.cs` - Server validation
- `NetworkGuard.cs` - Rate limiting

### Phase 2-3 - Trust (5)
- `CryptographicCommitment.cs` - Bait-and-switch prevention
- `ProofOfStorage.cs` - Chunk challenges
- `ByzantineConsensus.cs` - 2/3+1 voting
- `ProbabilisticVerification.cs` - Random sampling
- `TemporalConsistency.cs` - Change tracking

### Phase 4 - Intelligence (5)
- `EntropyMonitor.cs` - RNG health
- `FingerprintDetection.cs` - Port scan detection
- `Honeypot.cs` - Decoy files
- `CanaryTraps.cs` - Invisible watermarking
- `AsymmetricDisclosure.cs` - 6-tier trust model

---

## Ideas Parking Lot

### Multi-Source Improvements
- Consider using `Parallel.ForEachAsync` instead of manual Task.Run loops
- Look at how BitTorrent clients handle piece selection for inspiration
- Reputation scores should decay over time if no recent interactions

### Security Enhancements
- Consider persistent storage for ViolationTracker (currently in-memory)
- PeerReputation could use SQLite for long-term tracking
- Export security events to external SIEM (Splunk, ELK, etc.)

### Frontend Migration Notes
- Semantic UI LESS compilation is the riskiest part of Vite migration
- Test SignalR WebSocket connections after any build system changes
- Consider feature flags for gradual UI component migration

---

## Technical Debt Notes

### From CLEANUP_TODO.md
- "Simulated" logic in BackfillSchedulerService needs resolution
- Mixed logging patterns (ILogger<T> vs Serilog.Log.ForContext)
- Some AI-added npm packages may be unused (yaml, uuid)

### Architecture Questions
- Should PathGuard be a static utility or injected service? → **Answer: Static utility with optional DI wrapper**
- DownloadWorker extraction - what interface should it implement? → **Answer: IDownloadWorker with CancellationToken support**
- How to share security services between transfer handlers cleanly? → **Answer: SecurityServices aggregate class**

---

## Common Commands

```bash
# Run backend
./bin/watch

# Run frontend
cd src/web && npm start

# Run all tests
dotnet test

# Run security tests only
dotnet test --filter "FullyQualifiedName~Security"

# Lint
./bin/lint

# Build release
./bin/build
```

---

## Temporary Notes

(Use this section for session-specific notes that don't need to persist)

