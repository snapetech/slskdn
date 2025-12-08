# Active Context (Experimental Branch)

> What is currently being worked on in this repository.  
> Update this file when starting or finishing work.

---

## Current Session

- **Current Task**: None active
- **Branch**: `experimental/multi-source-swarm`
- **Environment**: Local dev
- **Last Activity**: Repository setup for memory-bank system

---

## Recent Context

### Last Session Summary
- Set up memory-bank structure for AI-assisted development
- No active feature work in progress

### Blocking Issues
- None currently

### Next Steps
1. Review `tasks.md` for next priority item (recommend T-001 or T-002 - security critical)
2. Create branch for selected task
3. Update this file with new context

---

## Branch Focus Areas

This experimental branch has three main focus areas:

### 1. Multi-Source Downloads
- **Status**: Core implementation done, needs hardening
- **Key Files**: `src/slskd/Transfers/MultiSource/`
- **Issues**: Unbounded concurrency (T-002)

### 2. Security Hardening
- **Status**: 30 components complete, needs integration
- **Key Files**: `src/slskd/Common/Security/`
- **Issues**: Not wired into transfer handlers (T-011)

### 3. DHT Rendezvous
- **Status**: Basic structure, needs testing
- **Key Files**: `src/slskd/DhtRendezvous/`

---

## Environment Notes

- **Backend Port**: 5030 (default)
- **Frontend Dev Port**: 3000 (CRA default)
- **.NET Version**: 8.0
- **Node Version**: Check `package.json` engines
- **Security Profile**: Standard (default)

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

# Run security tests only
dotnet test --filter "FullyQualifiedName~Security"
```

---

## Security Quick Reference

```csharp
// Enable security in Program.cs
builder.Services.AddSlskdnSecurity(builder.Configuration);
app.UseSlskdnSecurity();
```

```yaml
# Config profiles: Minimal, Standard, Maximum, Custom
Security:
  Enabled: true
  Profile: Standard
```

