# VirtualSoulfind v2 Quick Reference

## Architecture Layers

```
┌─────────────────────────────────────────────┐
│          User Intent Layer                  │
│  (DesiredRelease, DesiredTrack)            │
└─────────────────────────────────────────────┘
                    ↓
┌─────────────────────────────────────────────┐
│       Virtual Catalogue Layer               │
│  (Artist → ReleaseGroup → Release → Track) │
└─────────────────────────────────────────────┘
                    ↓
┌─────────────────────────────────────────────┐
│         Multi-Source Planner                │
│  - Domain rules (Music vs non-music)       │
│  - MCP filtering (CheckContentIdAsync)     │
│  - Backend ordering & selection             │
│  - Planning modes (Offline/Mesh/Soulseek)  │
└─────────────────────────────────────────────┘
                    ↓
┌─────────────────────────────────────────────┐
│           Content Backends                  │
│  LocalLibrary → Mesh → Http → Torrent      │
│  (Soulseek only for Music)                 │
└─────────────────────────────────────────────┘
                    ↓
┌─────────────────────────────────────────────┐
│      Match & Verification Engine            │
│  - Confidence levels (Weak → Strong)       │
│  - Post-download verification              │
└─────────────────────────────────────────────┘
```

## Key Files

- **Planner**: `MultiSourcePlanner.cs` (THE BRAIN)
- **Catalogue**: `InMemoryCatalogueStore.cs`
- **Backends**: `LocalLibraryBackend.cs`, `MockContentBackend.cs`
- **Matching**: `SimpleMatchEngine.cs`
- **Tests**: 152+ passing

## Status: PHASE 1 COMPLETE ✅
