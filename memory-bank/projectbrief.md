# slskdN Project Brief

> **Repository**: `slskdn` (main development branch)  
> **Purpose**: Feature-rich Soulseek web client - "Batteries Included"  
> **Upstream**: Fork of [slskd/slskd](https://github.com/slskd/slskd)

---

## What Is This Project?

slskdN is a community fork of slskd that takes the opposite approach to the upstream's "lean core + external scripts" philosophy. We build features directly into the application that power users expect from desktop clients like Nicotine+ and SoulseekQt.

**Philosophy**: No scripts. No external integrations. No assembly required.

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

### Infrastructure
- **CI/CD**: GitHub Actions (self-hosted runners: kspld0, kspls0)
- **Containers**: Docker (GHCR: `ghcr.io/snapetech/slskdn`)
- **Packages**: AUR, COPR, PPA, Unraid

---

## Key Constraints

### Must Preserve
- API compatibility with upstream slskd
- Configuration file format compatibility
- Database schema compatibility (where possible)
- Docker deployment patterns

### Must NOT Do
- Break compatibility unnecessarily
- Add bloat for edge cases
- Implement enterprise-only features
- Compromise on performance

### Copyright Headers [[memory:11969255]]
- **New slskdN files**: Use `Copyright (c) slskdN Team` with `company="slskdN Team"`
- **Existing upstream files**: Retain original `company="slskd Team"` attribution
- **Fork-specific directories**: `Capabilities/`, `HashDb/`, `Mesh/`, `Backfill/`, `Transfers/MultiSource/`, `Transfers/Ranking/`, `Users/Notes/`

---

## Current Phase Status

| Phase | Name | Status | Completion |
|-------|------|--------|------------|
| 1 | Download Reliability | ✅ Complete | 100% |
| 2 | Smart Automation | 🟡 Mostly Done | 80% |
| 3 | Search Intelligence | 🟡 Mostly Done | 60% |
| 4 | User Management | 🟡 Partial | 50% |
| 5 | Dashboard & Statistics | ❌ Pending | 0% |
| 6 | Download Organization | 🟡 Mostly Done | 75% |
| 7 | Integrations | 🟡 Partial | 30% |
| 8 | UI Polish | 🟡 Mostly Done | 70% |
| 9 | Infrastructure & Packaging | ✅ Complete | 100% |

---

## Important Docs

- `FORK_VISION.md` - Full feature roadmap and philosophy
- `DEVELOPMENT_HISTORY.md` - Release timeline and feature status
- `TODO.md` - Current pending work
- `CONTRIBUTING.md` - Contribution workflow
- `docs/` - User-facing documentation

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

## Target Users

1. **Power Users** - Want full-featured client without scripting
2. **Self-Hosters** - Run on home servers, want set-and-forget
3. **Media Collectors** - Need smart search, auto-downloads, *ARR integration
4. **Privacy-Conscious** - Want VPN-friendly, user-blocking features
5. **Nostalgic Users** - Miss desktop client features in web UI

