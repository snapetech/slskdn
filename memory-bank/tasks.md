# Tasks (Source of Truth) - Experimental Branch

> This file is the canonical task list for slskdN experimental development.  
> AI agents should add/update tasks here, not invent ephemeral todos in chat.

---

## 🚨 Immediate High Priority (from CLEANUP_TODO.md)

- [ ] **T-001**: FilesService Path Traversal Test
  - Status: Not started
  - Priority: Critical
  - Category: Security
  - Notes: Add unit test with directory traversal inputs (Base64 encoded `..` paths)

- [ ] **T-002**: MultiSourceDownloadService Concurrency Limits
  - Status: Not started
  - Priority: Critical
  - Category: Robustness
  - Notes: Cap unbounded `Task.Run` calls at ~10-20 workers with SemaphoreSlim

- [ ] **T-003**: BackfillSchedulerService Cleanup
  - Status: Not started
  - Priority: High
  - Category: Hygiene
  - Notes: Remove or replace "Simulated" logic - implement real downloads or disable

- [ ] **T-004**: Logging Standardization
  - Status: Not started
  - Priority: High
  - Category: Hygiene
  - Notes: Standardize `DhtRendezvousService` and `MultiSourceDownloadService` to use `ILogger<T>`

---

## 🛡️ Security Tasks

- [ ] **T-010**: FilesController Path Validation
  - Status: Not started
  - Priority: High
  - Category: Security
  - Notes: Validate Base64-decoded paths before passing to FilesService

- [ ] **T-011**: Wire Security into Transfer Handlers
  - Status: Not started
  - Priority: High
  - Category: Security Integration
  - Notes: Connect security services to existing transfer handlers

- [ ] **T-012**: PeerReputation + Source Ranking Integration
  - Status: Not started
  - Priority: Medium
  - Category: Security Integration
  - Notes: Use reputation scores in multi-source download ranking

- [ ] **T-013**: PathGuard for Download Paths
  - Status: Not started
  - Priority: High
  - Category: Security Integration
  - Notes: Add PathGuard validation to all download path handling

- [ ] **T-014**: ContentSafety for Completed Downloads
  - Status: Not started
  - Priority: Medium
  - Category: Security Integration
  - Notes: Verify downloaded files with ContentSafety service

---

## 🧹 Hygiene & Architecture

- [ ] **T-020**: File-Scoped Namespaces Migration
  - Status: Not started
  - Priority: Low
  - Category: Code Style
  - Notes: Adopt C# 10+ file-scoped namespaces across new services

- [ ] **T-021**: Extract DownloadWorker Class
  - Status: Not started
  - Priority: Medium
  - Category: Architecture
  - Notes: Move `RunSourceWorkerAsync` from `MultiSourceDownloadService` to dedicated class

- [ ] **T-022**: Unified PathGuard Service
  - Status: Not started
  - Priority: Medium
  - Category: Architecture
  - Notes: Create centralized `PathGuard` service instead of ad-hoc checks

- [ ] **T-023**: Dead Code Cleanup
  - Status: Not started
  - Priority: Low
  - Category: Hygiene
  - Notes: Scan for unused helper classes from rapid iteration

- [ ] **T-024**: Package.json Audit
  - Status: Not started
  - Priority: Low
  - Category: Dependencies
  - Notes: Remove unused AI-added dependencies (yaml, uuid if not imported)

---

## 🏗️ Frontend Migration

- [ ] **T-030**: React 16 → React 18 Upgrade
  - Status: Not started
  - Priority: Medium
  - Category: Frontend
  - Notes: See `docs/FRONTEND_MIGRATION_PLAN.md` Phase 1

- [ ] **T-031**: react-router-dom v5 → v6
  - Status: Not started
  - Priority: Medium
  - Category: Frontend
  - Notes: Significant API changes, review all routes

- [ ] **T-032**: CRA → Vite Migration
  - Status: Not started
  - Priority: Medium
  - Category: Frontend
  - Notes: See `docs/FRONTEND_MIGRATION_PLAN.md` Phase 2

---

## 🧪 Testing

- [ ] **T-040**: Swarm Logic Integration Tests
  - Status: Not started
  - Priority: High
  - Category: Testing
  - Notes: Test chunk assembly and hash verification

- [ ] **T-041**: Backfill Rate Limit Tests
  - Status: Not started
  - Priority: Medium
  - Category: Testing
  - Notes: Verify scheduler respects `MaxPerPeerPerDay` limits

- [ ] **T-042**: Security Unit Tests Expansion
  - Status: Partial (121 tests exist)
  - Priority: Medium
  - Category: Testing
  - Notes: Current: PathGuard, ContentSafety, ViolationTracker, PeerReputation, NetworkGuard

---

## ✅ Completed

- [x] **T-100**: Security Framework Implementation
  - Status: Done
  - Notes: 30 components, 121 unit tests passing

- [x] **T-101**: Security Web UI Dashboard
  - Status: Done
  - Notes: `System/Security/index.jsx`, stats cards, dark theme

- [x] **T-102**: Hardened Systemd Service
  - Status: Done
  - Notes: `etc/systemd/slskd-hardened.service`

---

*Last updated: December 8, 2025*

