# slskdN Project Brief (Experimental Branch)

> **Repository**: `slskdn-cleanup` (experimental/multi-source-swarm)  
> **Purpose**: Experimental features: multi-source downloads, security hardening, DHT rendezvous  
> **Upstream**: Fork of [slskd/slskd](https://github.com/slskd/slskd)  
> **Parent**: Branches from `slskdn` main development

---

## What Is This Repository?

This is the **experimental branch** of slskdN where we develop and test advanced features before they're merged to the main branch. Current focus areas:

1. **Multi-Source Downloads** - Swarm downloading from multiple peers
2. **Security Hardening** - Comprehensive security framework
3. **DHT Rendezvous** - Peer discovery and coordination
4. **Backfill Scheduling** - Automated hash database population

**⚠️ Warning**: This branch contains experimental code that may not be production-ready.

---

## Tech Stack

### Backend (.NET 8)
- **Framework**: ASP.NET Core 8.0
- **Database**: SQLite (via EFCore)
- **Real-time**: SignalR for WebSocket communication
- **Soulseek Protocol**: Soulseek.NET library
- **Location**: `src/slskd/`

### Frontend (React)
- **Framework**: React 16.8.6 (legacy, migration planned)
- **Build**: Create React App + CRACO
- **UI Library**: Semantic UI React
- **Routing**: react-router-dom v5
- **Location**: `src/web/`

### Experimental Components
- `src/slskd/DhtRendezvous/` - DHT peer coordination
- `src/slskd/Transfers/MultiSource/` - Multi-source download engine
- `src/slskd/HashDb/` - File hash database
- `src/slskd/Backfill/` - Automated hash population
- `src/slskd/Common/Security/` - Security hardening framework (30 components)

---

## Key Constraints

### Must Preserve
- API compatibility with upstream slskd
- Configuration file format compatibility
- Database schema compatibility (where possible)
- Docker deployment patterns

### Experimental Branch Specific
- Features may be incomplete or unstable
- Breaking changes allowed within experimental features
- Security features should be opt-in with sensible defaults

### Copyright Headers [[memory:11969255]]
- **New slskdN files**: Use `Copyright (c) slskdN Team` with `company="slskdN Team"`
- **Existing upstream files**: Retain original `company="slskd Team"` attribution
- **Fork-specific directories**: `Capabilities/`, `HashDb/`, `Mesh/`, `Backfill/`, `Transfers/MultiSource/`, `Transfers/Ranking/`, `Users/Notes/`, `DhtRendezvous/`, `Common/Security/`

---

## Current Feature Status

### Multi-Source Downloads
| Component | Status | Notes |
|-----------|--------|-------|
| MultiSourceDownloadService | 🟡 In Progress | Needs concurrency limits |
| Chunk Assembly | ✅ Working | Hash verification implemented |
| Source Ranking | 🟡 Partial | Basic ranking, needs reputation integration |

### Security Hardening (30 Components)
| Category | Status | Components |
|----------|--------|------------|
| Phase 1: Foundation | ✅ Complete | PathGuard, ContentSafety, ViolationTracker, etc. |
| Phase 2-3: Trust | ✅ Complete | CryptographicCommitment, ByzantineConsensus, etc. |
| Phase 4: Intelligence | ✅ Complete | EntropyMonitor, Honeypot, CanaryTraps, etc. |
| Integration | 🟡 Partial | Needs wiring into transfer handlers |

### DHT Rendezvous
| Component | Status | Notes |
|-----------|--------|-------|
| DhtRendezvousService | 🟡 In Progress | Basic structure, needs testing |

---

## Cleanup & Hardening Tasks

From `CLEANUP_TODO.md`:

### 🚨 High Priority
- [ ] Verify `FilesService.DeleteFilesAsync` path traversal handling
- [ ] Add concurrency limits to `MultiSourceDownloadService` retry loop
- [ ] Remove/replace "Simulated" logic in `BackfillSchedulerService`
- [ ] Standardize logging patterns across new services

### 🛡️ Security
- [ ] Filesystem traversal validation in `FilesController`
- [ ] Unbounded concurrency in swarm downloads (needs SemaphoreSlim)

### 🧹 Hygiene
- [ ] Adopt file-scoped namespaces consistently
- [ ] Unified `ILogger<T>` usage
- [ ] Remove dead code from rapid iteration

---

## Important Docs

- `CLEANUP_TODO.md` - Hardening and cleanup tasks
- `docs/SECURITY_IMPLEMENTATION_STATUS.md` - Security component status
- `docs/SECURITY_IMPLEMENTATION_SPECS.md` - Security specifications
- `docs/FRONTEND_MIGRATION_PLAN.md` - React/CRA migration plan
- `docs/MULTI_SOURCE_DOWNLOADS.md` - Multi-source feature docs
- `docs/DHT_RENDEZVOUS_DESIGN.md` - DHT design document

---

## Development Commands

```bash
# Run backend (watch mode)
./bin/watch

# Run frontend only (backend must be running)
./bin/watch --web

# Build release
./bin/build

# Run tests
dotnet test

# Lint
./bin/lint
```

---

## Security Quick Start

```csharp
// In Program.cs
builder.Services.AddSlskdnSecurity(builder.Configuration);
// ... 
app.UseSlskdnSecurity();  // Before UseAuthentication
```

```yaml
# In appsettings.yaml
Security:
  Enabled: true
  Profile: Standard  # Minimal, Standard, Maximum, or Custom
```

