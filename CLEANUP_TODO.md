# Slop Reduction & Hardening Plan

This document tracks the cleanup and hardening tasks identified for the `experimental/multi-source-swarm` branch.

## 🚨 Immediate High Priority

- [x] **[Security]** Verify `FilesService.DeleteFilesAsync` with a unit test using directory traversal inputs (Base64 encoded `..` paths). ✅ **COMPLETED** - Added comprehensive security tests
- [x] **[Robustness]** Add concurrency limits to `MultiSourceDownloadService` retry loop. Currently spawns unbounded `Task.Run` calls; cap at ~10-20 workers. ✅ **COMPLETED** - Uses `SemaphoreSlim(MaxConcurrentRetryWorkers = 10)`
- [ ] **[Hygiene]** Remove or replace "Simulated" logic in `BackfillSchedulerService`. Either implement real downloads or disable the feature. ⚠️ **VERIFICATION NEEDED** - Not found in current codebase, may have been removed
- [x] **[Cleanup]** Standardize `DhtRendezvousService` and `MultiSourceDownloadService` to use the same logging (`ILogger<T>`) and field naming conventions. ✅ **COMPLETED** - All services now use `ILogger<T>` consistently
- [x] **[Frontend]** Fix login screen API calls - `SlskdnStatusBar` was making authenticated API calls before login. ✅ **COMPLETED** - Only render status bar when authenticated
- [ ] **[Frontend]** Plan migration off `react-scripts` (deprecated) to `vite` or `rsbuild`.

## 🛡️ Security Findings

### High Severity
- [ ] **Filesystem Traversal:** Ensure `FilesController` validates Base64-decoded paths *before* passing them to `FilesService`, or ensure `FilesService` handles them robustly against `GetFullPath` checks.

### Medium Severity
- [x] **Unbounded Concurrency:** `MultiSourceDownloadService.SwarmDownloadAsync` retry loop needs a `SemaphoreSlim` or `Parallel.ForEachAsync`. ✅ **COMPLETED** - Implemented with `SemaphoreSlim(MaxConcurrentRetryWorkers = 10)`

### Low Severity
- [ ] **Dependencies:** Audit `package.json` for unused AI-added dependencies (e.g., `yaml`, `uuid` if not imported).

## 🧹 Hygiene & Consistency

- [ ] **Naming:** Adopt file-scoped namespaces (C# 10+) and `_privateField` injection across all new services (`MultiSourceDownloadService` vs `DhtRendezvousService`).
- [ ] **Logging:** Unified logging pattern (prefer `ILogger<T>` over `Serilog.Log.ForContext`).
- [ ] **Dead Code:** Scan for and remove unused helper classes or methods created during rapid iteration.

## 🧪 Testing & CI

- [x] **Filesystem Safety:** Add unit tests for `FileService.DeleteFilesAsync` specifically targeting traversal attempts. ✅ **COMPLETED** - Added `FileServiceSecurityTests.cs` and `FilesControllerSecurityTests.cs`
- [ ] **Swarm Logic:** Add integration/logic tests for chunk assembly and hash verification.
- [ ] **Backfill Rate Limits:** Test that the scheduler respects `MaxPerPeerPerDay` limits.
- [ ] **CI:** Enable "Warnings as Errors" for specific style rules to prevent future regression.

## 🏗️ Architecture Refactors

- [ ] **Extract DownloadWorker:** Move `RunSourceWorkerAsync` from `MultiSourceDownloadService` to a dedicated `DownloadWorker` class.
- [ ] **Unified PathGuard:** Create a `PathGuard` service for centralized path validation instead of ad-hoc checks in controllers/services.

