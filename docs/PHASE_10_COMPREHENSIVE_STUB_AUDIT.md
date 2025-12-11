# Phase 10: PodCore & Chat Bridge — Comprehensive Stub & Placeholder Audit

> **Date**: December 10, 2025  
> **Status**: ⚠️ **OUTDATED - SEE PHASE_9_10_STATUS_UPDATE_2025-12-11.md**  
> **Real Completion**: ~~15%~~ → **75% COMPLETE** (verified Dec 11, 2025)
> 
> **⚠️ THIS AUDIT IS OUTDATED**: Almost all "stubs" identified here are actually complete implementations (1544 LOC).
> See `PHASE_9_10_STATUS_UPDATE_2025-12-11.md` for current status.

---

## Executive Summary

Phase 10 is **almost entirely stubs**. While data models exist, virtually all functionality is stubbed with explicit `// TODO` comments and "stub" labels.

**Key Findings**:
- ✅ **Working**: Data models (`Pod`, `PodMember`, `PodMessage`, `PodChannel`, etc.)
- 🚫 **Stubs**: All services (`PodService`, `PodMessaging`, `SoulseekChatBridge`) are explicitly marked as stubs
- ⚠️ **Missing**: Message routing, storage, signature validation, deduplication, chat bridge implementation, UI components

---

## Detailed Findings by Component

### 1. Pod Service

#### 1.1 `PodService.cs` — **EXPLICIT STUB** 🚫
**Location**: `src/slskd/PodCore/PodServices.cs:16`

**Status**: **EXPLICITLY MARKED AS STUB**
```csharp
/// In-memory pod service (stub).
public class PodService : IPodService
{
    private readonly Dictionary<string, Pod> pods = new();
    // ...
}
```

**1.1.1 `CreateAsync`** — **MINIMAL IMPLEMENTATION** ⚠️
```csharp
public Task<Pod> CreateAsync(Pod pod, CancellationToken ct = default)
{
    if (string.IsNullOrWhiteSpace(pod.PodId))
    {
        pod.PodId = $"pod:{Guid.NewGuid():N}";
    }
    pods[pod.PodId] = pod;
    return Task.FromResult(pod);
}
```

**Status**: ✅ **BASIC FUNCTIONALITY WORKS** — Creates pods in memory
**Missing**:
- DHT publishing of pod metadata
- Persistence (SQLite/database)
- Validation
- Access control

**Impact**: **MEDIUM** — Works for single-instance, but no persistence or discovery

---

**1.1.2 `ListAsync`** — **MINIMAL IMPLEMENTATION** ⚠️
```csharp
public Task<IReadOnlyList<Pod>> ListAsync(CancellationToken ct = default) =>
    Task.FromResult((IReadOnlyList<Pod>)pods.Values.ToList());
```

**Status**: ✅ **BASIC FUNCTIONALITY WORKS** — Lists in-memory pods
**Missing**:
- DHT querying for listed pods
- Filtering (visibility, tags, etc.)
- Pagination
- Discovery

**Impact**: **MEDIUM** — Only returns local pods, no discovery

---

**1.1.3 `JoinAsync`** — **STUB (NO-OP)** 🚫
```csharp
public Task<bool> JoinAsync(string podId, PodMember member, CancellationToken ct = default)
{
    if (!pods.TryGetValue(podId, out var pod)) return Task.FromResult(false);
    var existing = pod.Channels; // no-op for now
    return Task.FromResult(true);
}
```

**Status**: 🚫 **STUB** — Does not actually add member to pod
**Missing**:
- Add member to pod's member list
- Signature validation
- Membership record publishing
- Access control checks

**Impact**: **HIGH** — Join functionality is broken

**Task**: T-1003 (already exists - "Implement pod join/leave flows")

---

**1.1.4 `LeaveAsync`** — **STUB (NO-OP)** 🚫
```csharp
public Task<bool> LeaveAsync(string podId, string peerId, CancellationToken ct = default)
{
    if (!pods.TryGetValue(podId, out var pod)) return Task.FromResult(false);
    return Task.FromResult(true);
}
```

**Status**: 🚫 **STUB** — Does not actually remove member
**Missing**:
- Remove member from pod
- Update membership records
- Notify other members

**Impact**: **HIGH** — Leave functionality is broken

**Task**: T-1003 (already exists)

---

**1.1.5 `BanAsync`** — **STUB (NO-OP)** 🚫
```csharp
public Task<bool> BanAsync(string podId, string peerId, CancellationToken ct = default)
{
    if (!pods.TryGetValue(podId, out var pod)) return Task.FromResult(false);
    return Task.FromResult(true);
}
```

**Status**: 🚫 **STUB** — Does not actually ban member
**Missing**:
- Mark member as banned
- Remove member from pod
- Publish ban record
- Access control (owner/mod only)

**Impact**: **HIGH** — Ban functionality is broken

**Task**: T-1015 (already exists - "Implement owner/moderator kick/ban actions")

---

### 2. Pod Messaging

#### 2.1 `PodMessaging.cs` — **EXPLICIT STUB** 🚫
**Location**: `src/slskd/PodCore/PodServices.cs:56`

**Status**: **EXPLICITLY MARKED AS STUB**
```csharp
/// Pod messaging stub (signature/dedupe placeholder).
public interface IPodMessaging
{
    Task<bool> SendAsync(PodMessage message, CancellationToken ct = default);
}

public class PodMessaging : IPodMessaging
{
    public Task<bool> SendAsync(PodMessage message, CancellationToken ct = default)
    {
        // TODO: signature validation, membership check, dedupe
        return Task.FromResult(true);
    }
}
```

**Impact**: **CRITICAL** — No security, no deduplication, no routing

**Missing**:
- Signature validation (Ed25519)
- Membership verification (is sender a member?)
- Message deduplication (prevent replay attacks)
- Message routing (deliver to pod members)
- Message storage (persistence)
- Message backfill (retrieve missed messages)

**Tasks**:
- T-1006 (already exists - "Implement decentralized message routing")
- T-1007 (already exists - "Build local message storage and backfill")
- T-1009 (already exists - "Implement message validation and signature checks")

---

### 3. Soulseek Chat Bridge

#### 3.1 `SoulseekChatBridge.cs` — **EXPLICIT STUB** 🚫
**Location**: `src/slskd/PodCore/PodServices.cs:73`

**Status**: **EXPLICITLY MARKED AS STUB**
```csharp
/// Soulseek chat bridge stub for bound channels.
public interface ISoulseekChatBridge
{
    Task<bool> BindRoomAsync(string podId, string roomName, string mode, CancellationToken ct = default);
}

public class SoulseekChatBridge : ISoulseekChatBridge
{
    public Task<bool> BindRoomAsync(string podId, string roomName, string mode, CancellationToken ct = default)
    {
        // TODO: implement readonly/mirror wiring
        return Task.FromResult(true);
    }
}
```

**Impact**: **CRITICAL** — Chat bridge is non-functional

**Missing**:
- Readonly mode (Soulseek → Pod, one-way)
- Mirror mode (Soulseek ↔ Pod, two-way)
- Room message forwarding
- Pod message forwarding to Soulseek
- Message prefixing/formatting
- Identity mapping (Soulseek username ↔ Pod PeerId)

**Tasks**:
- T-1027 (already exists - "Implement bound channel creation and mirroring")
- T-1028 (already exists - "Add two-way mirroring (Mirror mode)")
- T-1358 (already exists - "Implement Soulseek identity mapping")

---

### 4. Pod Discovery

#### 4.1 Pod Discovery — **MISSING** 🚫
**Status**: **NO IMPLEMENTATION FOUND**

**Impact**: **HIGH** — Cannot discover listed pods

**Missing**:
- DHT querying for listed pods
- Pod metadata publishing to DHT
- Pod search/filtering
- Pod recommendation engine

**Task**: T-1004 (already exists - "Add pod discovery for listed pods")

---

### 5. Pod Metadata Publishing

#### 5.1 Pod Metadata Publishing — **MISSING** 🚫
**Status**: **NO IMPLEMENTATION FOUND**

**Impact**: **HIGH** — Pods are not discoverable

**Missing**:
- DHT publishing of pod metadata
- Metadata refresh/updates
- TTL management
- Signature generation

**Task**: T-1001 (already exists - "Implement pod creation and metadata publishing")

---

### 6. Signed Membership Records

#### 6.1 Signed Membership System — **MISSING** 🚫
**Status**: **NO IMPLEMENTATION FOUND**

**Impact**: **HIGH** — No cryptographic proof of membership

**Missing**:
- Ed25519 signature generation for membership records
- Membership record publishing
- Membership record verification
- Membership history/audit trail

**Task**: T-1002 (already exists - "Build signed membership record system")

---

### 7. Pod Channels

#### 7.1 Pod Channels — **MODEL ONLY** ⚠️
**Status**: **DATA MODEL EXISTS, NO FUNCTIONALITY**

**Existing**: `PodChannel` class with `ChannelId`, `Kind`, `Name`, `BindingInfo`

**Missing**:
- Channel creation/management
- Channel-specific messaging
- Channel permissions
- Channel binding to Soulseek rooms

**Task**: T-1008 (already exists - "Add pod channels (general, custom)")

---

### 8. Content-Linked Pods

#### 8.1 Content-Linked Pod Creation — **MODEL ONLY** ⚠️
**Status**: **DATA MODEL EXISTS (`Pod.FocusContentId`), NO FUNCTIONALITY**

**Missing**:
- Automatic pod creation for content
- Content → Pod mapping
- Pod discovery by content

**Task**: T-1010 (already exists - "Implement content-linked pod creation")

---

#### 8.2 Variant Opinion Publishing — **MISSING** 🚫
**Status**: **DATA MODEL EXISTS (`PodVariantOpinion`), NO FUNCTIONALITY**

**Missing**:
- Opinion publishing to DHT
- Opinion retrieval
- Opinion aggregation
- Integration with canonicality engine

**Task**: T-1013 (already exists - "Implement variant opinion publishing and retrieval")

---

### 9. Pod Trust & Moderation

#### 9.1 Pod Opinions Integration — **MISSING** 🚫
**Status**: **NO IMPLEMENTATION FOUND**

**Task**: T-1014 (already exists - "Integrate pod opinions into canonicality engine")

---

#### 9.2 Pod Affinity Scoring — **MISSING** 🚫
**Status**: **NO IMPLEMENTATION FOUND**

**Task**: T-1016 (already exists - "Build PodAffinity scoring (engagement, trust)")

---

#### 9.3 Pod Trust Integration — **MISSING** 🚫
**Status**: **NO IMPLEMENTATION FOUND**

**Task**: T-1017 (already exists - "Integrate pod trust with SecurityCore")

---

#### 9.4 Global Reputation Feed — **MISSING** 🚫
**Status**: **NO IMPLEMENTATION FOUND**

**Task**: T-1018 (already exists - "Add global reputation feed from pod abuse")

---

### 10. Pod UI

#### 10.1 Pod UI Components — **MISSING** 🚫
**Status**: **ZERO JSX FILES FOUND**

**Impact**: **CRITICAL** — No user interface for pods

**Missing**:
- Pod list view
- Pod detail view
- Pod chat UI
- Pod member list
- Join/leave buttons
- Channel tabs
- Message input

**Tasks**:
- T-1020 (already exists - "Implement pod list and detail views")
- T-1021 (already exists - "Build pod chat UI with safety guardrails")
- T-1023 (already exists - "Implement pod-scoped variant opinion UI")

---

## Summary: Stub Count by Category

| Category | Stubs | Placeholders | Missing | Total Issues |
|----------|-------|--------------|---------|--------------|
| **Pod Service** | 3 | 2 | 0 | 5 |
| **Pod Messaging** | 1 | 0 | 0 | 1 |
| **Chat Bridge** | 1 | 0 | 0 | 1 |
| **Discovery** | 0 | 0 | 1 | 1 |
| **Metadata Publishing** | 0 | 0 | 1 | 1 |
| **Membership** | 0 | 0 | 1 | 1 |
| **Channels** | 0 | 1 | 0 | 1 |
| **Content-Linked** | 0 | 1 | 1 | 2 |
| **Trust/Moderation** | 0 | 0 | 4 | 4 |
| **UI** | 0 | 0 | 3 | 3 |
| **TOTAL** | **5** | **4** | **11** | **20** |

---

## Critical Issues Requiring Immediate Attention

### 🔴 **CRITICAL** (Core Functionality Broken)

1. **Pod Messaging** (T-1006, T-1007, T-1009) — No security, routing, or storage
2. **Chat Bridge** (T-1027, T-1028) — Completely non-functional
3. **Pod UI** (T-1020, T-1021, T-1023) — Zero UI components exist

### 🟡 **HIGH** (Major Features Missing)

4. **Pod Join/Leave** (T-1003) — Stub implementations
5. **Pod Ban** (T-1015) — Stub implementation
6. **Pod Discovery** (T-1004) — Not implemented
7. **Metadata Publishing** (T-1001) — Not implemented
8. **Signed Membership** (T-1002) — Not implemented

### 🟢 **MEDIUM** (Enhancement Features)

9. **Pod Channels** (T-1008) — Model only
10. **Content-Linked Pods** (T-1010, T-1013) — Model only
11. **Trust/Moderation** (T-1014, T-1016, T-1017, T-1018) — Not implemented

---

## Recommendations

1. **IMMEDIATE**: Fix pod service stubs (join/leave/ban) — core functionality broken
2. **HIGH PRIORITY**: Implement pod messaging with security (signatures, deduplication)
3. **HIGH PRIORITY**: Implement chat bridge (readonly mode first)
4. **HIGH PRIORITY**: Build pod UI components (list, detail, chat)
5. **MEDIUM**: Add pod discovery and metadata publishing
6. **LOW**: Implement trust/moderation features

---

*Audit completed: December 10, 2025*

