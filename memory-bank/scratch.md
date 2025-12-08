# Scratch Pad

> Free-form notes, ideas, and temporary thinking space.  
> This file can be truncated when it gets too long.  
> AI agents can use this for "thinking on paper" without worrying about cleanliness.

---

## Current Thoughts

(Empty - ready for use)

---

## Quick Reference: Upstream Bug Fixes Applied

These bugs were fixed in slskdN and should NOT be reverted:

| Bug | File | Fix |
|-----|------|-----|
| async-void crash | `RoomService.cs` | Wrap in try-catch |
| undefined return | `searches.js` | Return `[]` not `undefined` |
| undefined return | `transfers.js` | Return `[]` not `undefined` |
| no pagination | `SearchService.cs` | Add limit/offset params |
| flaky test | `UploadGovernorTests.cs` | Use InlineAutoData |

---

## Ideas Parking Lot

### Feature Ideas
- Persistent Room/Chat tabs (like Browse tabs)
- Scheduled rate limits (day/night)
- Traffic ticker (real-time activity feed)

### Technical Debt
- Frontend migration from CRA to Vite (see `docs/FRONTEND_MIGRATION_PLAN.md` in cleanup branch)
- React 16 → React 18 upgrade
- react-router-dom v5 → v6

### Questions to Research
- Best approach for persistent chat tabs (localStorage vs IndexedDB)
- Rate limit scheduler UI design

---

## Common Commands

```bash
# Backend dev (hot reload)
./bin/watch

# Frontend dev
cd src/web && npm start

# Backend only (no frontend)
cd src/slskd && dotnet run

# Run tests
dotnet test

# Run specific test
dotnet test --filter "FullyQualifiedName~MyTestName"

# Lint C#
./bin/lint

# Lint frontend
cd src/web && npm run lint

# Build release (all platforms)
./bin/build

# Build single platform
./bin/publish linux-x64

# Check for .NET restore issues
dotnet restore --verbosity detailed
```

---

## File Locations Cheat Sheet

| What | Where |
|------|-------|
| Main entry | `src/slskd/Program.cs` |
| DI registration | `Program.cs:ConfigureDependencyInjectionContainer()` |
| Options/Config | `src/slskd/Core/Options.cs` |
| Application lifecycle | `src/slskd/Application.cs` |
| API routes | `src/slskd/*/API/Controllers/*.cs` |
| Frontend API calls | `src/web/src/lib/*.js` |
| React components | `src/web/src/components/` |
| Config example | `config/slskd.example.yml` |
| Test project | `tests/slskd.Tests.Unit/` |

---

## New Feature Checklist

When adding a new feature:

- [ ] Create `src/slskd/MyFeature/` directory
- [ ] Add interface `IMyService.cs`
- [ ] Add implementation `MyService.cs`
- [ ] Register in `Program.cs` DI container
- [ ] Add controller in `MyFeature/API/MyFeatureController.cs`
- [ ] Add frontend lib in `src/web/src/lib/myFeature.js`
- [ ] Add React component in `src/web/src/components/MyFeature/`
- [ ] Add route in `src/web/src/App.jsx` if needed
- [ ] Add tests in `tests/slskd.Tests.Unit/MyFeature/`

---

## API Endpoint Patterns

```
GET    /api/v0/things           → List all
GET    /api/v0/things/{id}      → Get one
POST   /api/v0/things           → Create
PUT    /api/v0/things/{id}      → Update
DELETE /api/v0/things/{id}      → Delete
POST   /api/v0/things/{id}/action → Custom action
```

---

## Quick Grep Commands

```bash
# Find all service registrations
grep -n "services.Add" src/slskd/Program.cs

# Find all API endpoints
grep -rn "\[Http" src/slskd/*/API/

# Find all event handlers
grep -rn "async void" src/slskd/

# Find all localStorage usage
grep -rn "localStorage" src/web/src/

# Find all toast notifications
grep -rn "toast\." src/web/src/

# Find IOptionsMonitor usage
grep -rn "IOptionsMonitor" src/slskd/
```

---

## Debugging Tips

### Backend
- Logs: Check `~/.local/share/slskd/logs/` or console output
- Database: SQLite files in `~/.local/share/slskd/`
- Config: `~/.local/share/slskd/slskd.yml` or env vars

### Frontend
- DevTools Network tab for API calls
- React DevTools for component state
- Console for errors from `toast.error`

### Common Issues
- **Port conflict**: Another instance running? Check `lsof -i :5030`
- **CORS errors**: Backend not running or wrong port
- **DB locked**: Close other connections, check for zombie processes

---

## Temporary Notes

(Use this section for session-specific notes that don't need to persist)

