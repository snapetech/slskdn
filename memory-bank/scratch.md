# Scratch Pad (Experimental Branch)

> Free-form notes, ideas, and temporary thinking space.  
> This file can be truncated when it gets too long.  
> AI agents can use this for "thinking on paper" without worrying about cleanliness.

---

## Current Thoughts

(Empty - ready for use)

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
- Should PathGuard be a static utility or injected service?
- DownloadWorker extraction - what interface should it implement?
- How to share security services between transfer handlers cleanly?

---

## Temporary Notes

(Use this section for session-specific notes that don't need to persist)

