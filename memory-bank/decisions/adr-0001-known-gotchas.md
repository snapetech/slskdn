# ADR-0001: Known Gotchas & Anti-Patterns (Experimental Branch)

> **Status**: Active  
> **Date**: 2025-12-08  
> **Author**: AI-assisted development sessions

This document captures known issues, anti-patterns, and "gotchas" specific to the experimental/multi-source-swarm branch. **Read this before making changes.**

---

## 🚨 CRITICAL: Security-Sensitive Code

### 1. Unbounded Parallelism in MultiSourceDownloadService

**The Bug**: `Task.Run` inside retry loops without concurrency limits.

**Location**: `src/slskd/Transfers/MultiSource/MultiSourceDownloadService.cs`

**Current State**: Known issue, needs `SemaphoreSlim` with cap of 10-20 workers.

**Wrong**:
```csharp
foreach (var source in sources)
{
    _ = Task.Run(() => DownloadFromSourceAsync(source));  // 💀 Unbounded
}
```

**Correct**:
```csharp
await Parallel.ForEachAsync(sources, 
    new ParallelOptions { MaxDegreeOfParallelism = 10 },
    async (source, ct) => await DownloadFromSourceAsync(source, ct));
```

---

### 2. Path Traversal in FilesService

**The Bug**: Base64-decoded paths may contain `..` traversal.

**Location**: `src/slskd/Files/FilesService.cs`, `FilesController.cs`

**Current State**: Needs unit test with traversal inputs. Use `PathGuard.NormalizeAndValidate()`.

**Test Case Needed**:
```csharp
[Fact]
public void DeleteFilesAsync_RejectsTraversalPath()
{
    var maliciousPath = Convert.ToBase64String(Encoding.UTF8.GetBytes("../../../etc/passwd"));
    await Assert.ThrowsAsync<SecurityException>(() => 
        service.DeleteFilesAsync(new[] { maliciousPath }));
}
```

---

### 3. BackfillSchedulerService "Simulated" Logic

**The Bug**: Contains placeholder "Simulated" download logic that doesn't actually download.

**Location**: `src/slskd/Backfill/BackfillSchedulerService.cs`

**Current State**: Either implement real downloads via `ISoulseekClient` or disable the feature.

---

## ⚠️ HIGH: Integration Issues

### 4. Security Services Not Wired to Transfer Handlers

**The Issue**: 30 security components exist in `Common/Security/` but aren't integrated.

**What Needs Wiring**:
| Security Component | Should Be Used In |
|-------------------|-------------------|
| `PathGuard` | `FilesController`, `TransferService` |
| `ContentSafety` | Download completion handlers |
| `ViolationTracker` | All peer interaction points |
| `PeerReputation` | Source ranking in multi-source |
| `NetworkGuard` | Overlay connection handlers |

**How to Wire** (example):
```csharp
// In TransferService
public async Task<TransferResult> DownloadAsync(...)
{
    // Validate path before download
    var safePath = PathGuard.NormalizeAndValidate(remotePath, _downloadRoot);
    if (safePath == null)
    {
        _violationTracker.RecordViolation(username, ViolationType.PathTraversal);
        throw new SecurityException("Invalid path");
    }
    
    // ... proceed with download
    
    // Verify content after download
    if (!_contentSafety.VerifyMagicBytes(filePath, expectedExtension))
    {
        _violationTracker.RecordViolation(username, ViolationType.ContentMismatch);
        await _contentSafety.QuarantineFileAsync(filePath, "Extension mismatch");
    }
}
```

---

### 5. Logging Pattern Inconsistency

**The Issue**: `DhtRendezvousService` uses `Serilog.Log.ForContext`, other services use `ILogger<T>`.

**Standardization in Progress**: Prefer `ILogger<T>` injection.

**Files to Update**:
- `src/slskd/DhtRendezvous/DhtRendezvousService.cs`
- `src/slskd/Mesh/MeshSyncService.cs`

---

## 🔄 Patterns That Cause Fix/Unfix Cycles

### 6. Frontend Component Restoration After Merges

**The Cycle**:
1. Merge from master or another branch
2. Frontend components disappear (StatusBar, Security Tab)
3. "Fix" by re-adding components
4. Next merge loses them again

**Affected Components**:
- `src/web/src/components/Shared/SlskdnStatusBar.jsx`
- `src/web/src/components/System/Security/index.jsx`
- `src/web/src/components/System/Network/index.jsx`

**Solution**: Check these files after every merge.

---

### 7. DI Service Registration After Merges

**The Cycle**:
1. Experimental services registered in `Program.cs`
2. Merge from master overwrites `Program.cs`
3. Runtime crash: "Unable to resolve service"

**Services That Need Registration**:
```csharp
// Experimental services
builder.Services.AddSingleton<IHashDbService, HashDbService>();
builder.Services.AddSingleton<ICapabilityService, CapabilityService>();
builder.Services.AddSingleton<IMeshSyncService, MeshSyncService>();
builder.Services.AddSingleton<IBackfillSchedulerService, BackfillSchedulerService>();
builder.Services.AddSingleton<IDhtRendezvousService, DhtRendezvousService>();

// Security services
builder.Services.AddSlskdnSecurity(builder.Configuration);
```

---

### 8. ESLint/Prettier Formatting in Security Component

**The Cycle**:
1. Edit `System/Security/index.jsx`
2. Lint fails on import order or quote style
3. Model "fixes" lint by changing unrelated code
4. Original changes get lost

**The Rule**: This project uses **single quotes** and specific import order.

**Run Before Commit**:
```bash
cd src/web && npm run lint -- --fix
```

---

## 🔐 Security-Specific Gotchas

### 9. UPnP Disabled by Default (Intentional)

**Location**: `src/slskd/DhtRendezvous/NatDetectionService.cs`

**Current**: `EnableUpnp = false`

**Why**: UPnP has known security vulnerabilities. STUN is enabled by default as it's safe.

**Don't**: Enable UPnP by default. Users must explicitly opt-in.

---

### 10. Security Event Sink Usage

**The Rule**: All security events should go through `SecurityEventSink`.

**Wrong**:
```csharp
_logger.LogWarning("Path traversal attempt from {User}", username);
```

**Correct**:
```csharp
_eventSink.Report(SecurityEvent.Create(
    SecurityEventType.PathTraversal,
    SecuritySeverity.High,
    "Path traversal attempt blocked",
    username: username,
    ipAddress: remoteIp.ToString()
));
```

---

### 11. ViolationTracker Ban Escalation

**How It Works**:
- 1st violation: Warning logged
- 2nd violation: 5-minute backoff
- 3rd violation: 1-hour ban
- 4th+ violation: 24-hour ban (exponential)

**Don't**: Manually ban without going through ViolationTracker (loses audit trail).

---

## 📦 Experimental Branch Specific

### 12. Files That Should NOT Go to Master

These directories are experimental-only:
- `src/slskd/DhtRendezvous/`
- `src/slskd/Transfers/MultiSource/`
- `src/slskd/HashDb/`
- `src/slskd/Mesh/`
- `src/slskd/Backfill/`
- `src/slskd/Capabilities/`
- `src/slskd/Common/Security/` (full framework)
- `src/web/src/components/System/Network/`
- `src/web/src/lib/slskdn.js`

---

### 13. Test Scripts Location

**Shell Scripts** (in repo root):
- `./swarm-download-test.sh` - Test multi-source downloads
- `./parallel-download-test.sh` - Test parallel downloads
- `./multi-source-test.sh` - Integration test
- `./test-swarm.sh` - Full swarm test

**Don't**: Commit test data or credentials in these scripts.

---

## 🧪 Test Gotchas

### 14. Security Tests Require Specific Filter

**Run Security Tests Only**:
```bash
dotnet test --filter "FullyQualifiedName~Security"
```

**Current Coverage**: 121 tests across:
- `PathGuardTests` (20+ tests)
- `ContentSafetyTests` (20+ tests)
- `ViolationTrackerTests` (20+ tests)
- `PeerReputationTests` (17 tests)
- `NetworkGuardTests` (12 tests)

---

### 15. Integration Tests Need Running Backend

**Location**: `tests/slskd.Tests.Integration/`

**Requirement**: Backend must be running for integration tests.

```bash
# Terminal 1
./bin/watch

# Terminal 2
dotnet test tests/slskd.Tests.Integration/
```

---

## 📝 Documentation Gotchas

### 16. Multiple TODO Files

| File | Purpose | Who Maintains |
|------|---------|---------------|
| `TODO.md` | High-level human todos | Human |
| `CLEANUP_TODO.md` | Hardening tasks | Human |
| `memory-bank/tasks.md` | AI task backlog | AI |

**Don't** duplicate tasks. Reference each other.

---

### 17. Security Docs Locations

| Doc | Purpose |
|-----|---------|
| `docs/SECURITY_IMPLEMENTATION_STATUS.md` | Component status |
| `docs/SECURITY_IMPLEMENTATION_SPECS.md` | Technical specs |
| `docs/SECURITY_HARDENING_ROADMAP.md` | Full roadmap |
| `docs/security-configuration.md` | User config guide |

---

*Last updated: 2025-12-08*

