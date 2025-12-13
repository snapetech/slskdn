# Phase 8: MeshCore Foundation — Comprehensive Stub & Placeholder Audit

> **Date**: December 10, 2025  
> **Status**: ⚠️ **OUTDATED - SEE PHASE_8_STATUS_UPDATE_2025-12-11.md**  
> **Real Completion**: ~~35%~~ → **85% COMPLETE** (verified Dec 11, 2025)
> 
> **⚠️ THIS AUDIT IS OUTDATED**: All "critical blockers" identified here have been verified as already implemented.
> See `PHASE_8_STATUS_UPDATE_2025-12-11.md` for current status.

---

## Executive Summary

Phase 8 has **significant stubs and placeholders**. While basic DHT operations work, many critical components are incomplete or disabled.

**Key Findings**:
- ✅ **Working**: Basic DHT PUT/GET, peer descriptors, content directory lookups
- 🚫 **Stubs**: QUIC overlay (disabled), Ed25519 signatures (stub), NAT detection (stub), mesh routing diagnostics
- ⚠️ **Placeholders**: Route tracing, transport stats, mesh sync queries

---

## Detailed Findings by Component

### 1. NAT Detection & Traversal

#### 1.1 `NatDetector.cs` — **STUB** 🚫
**Location**: `src/slskd/Mesh/Nat/NatDetector.cs`

**Status**: Returns `NatType.Unknown` always
```csharp
public Task<NatType> DetectAsync(CancellationToken ct = default)
{
    // TODO: Implement STUN-based detection. Stub returns Unknown.
    return Task.FromResult(NatType.Unknown);
}
```

**Impact**: **HIGH** — Cannot determine NAT type, blocking proper traversal strategy selection

**Task**: T-1300 (already exists)

---

#### 1.2 `StunNatDetector.cs` — **IMPLEMENTED** ✅
**Location**: `src/slskd/Mesh/Nat/StunNatDetector.cs`

**Status**: ✅ **FULLY IMPLEMENTED** — Real STUN-based NAT detection
- Implements STUN binding request/response
- Classifies NAT types (Direct, Restricted, Symmetric)
- Handles multiple STUN servers
- Proper error handling

**Note**: This is the real implementation, but `NatDetector` (the default) is still a stub.

**Action**: Switch default to `StunNatDetector` or remove stub `NatDetector`.

---

#### 1.3 `UdpHolePuncher.cs` — **IMPLEMENTED** ✅
**Status**: ✅ **FULLY IMPLEMENTED** — Real UDP hole punching
- Sends multiple packets to open NAT mappings
- Listens for responses
- Proper error handling

---

#### 1.4 `NatTraversalService.cs` — **IMPLEMENTED** ✅
**Status**: ✅ **FULLY IMPLEMENTED** — Coordinates traversal attempts
- Tries direct UDP first
- Falls back to hole punching
- Relay fallback support

---

#### 1.5 `RelayClient.cs` — **IMPLEMENTED** ✅
**Status**: ✅ **FULLY IMPLEMENTED** — Basic relay client
- Sends payloads via relay endpoints
- Proper error handling

---

### 2. Overlay Transport

#### 2.1 `QuicOverlayServer.cs` — **STUB (DISABLED)** 🚫
**Location**: `src/slskd/Mesh/Overlay/QuicOverlayServer.cs`

**Status**: **COMPLETELY DISABLED**
```csharp
/// Stub QUIC overlay server; disabled for build.
protected override Task ExecuteAsync(CancellationToken stoppingToken)
{
    logger.LogInformation("[Overlay-QUIC] Stub server disabled (build-only)");
    return Task.CompletedTask;
}
```

**Impact**: **CRITICAL** — QUIC overlay transport is non-functional

**Task**: T-1316 (needs creation)

---

#### 2.2 `QuicOverlayClient.cs` — **STUB (DISABLED)** 🚫
**Location**: `src/slskd/Mesh/Overlay/QuicOverlayClient.cs`

**Status**: **COMPLETELY DISABLED**
```csharp
/// Stub QUIC overlay client; disabled for build.
public Task<bool> SendAsync(ControlEnvelope envelope, IPEndPoint endpoint, CancellationToken ct = default)
{
    logger.LogDebug("[Overlay-QUIC] Stub client drop message");
    return Task.FromResult(false);
}
```

**Impact**: **CRITICAL** — QUIC overlay transport is non-functional

**Task**: T-1317 (needs creation)

---

#### 2.3 `QuicDataServer.cs` — **STUB (DISABLED)** 🚫
**Location**: `src/slskd/Mesh/Overlay/QuicDataServer.cs`

**Status**: **COMPLETELY DISABLED**
```csharp
/// Stub QUIC data-plane server; disabled for build.
protected override Task ExecuteAsync(CancellationToken stoppingToken)
{
    logger.LogInformation("[Overlay-QUIC-DATA] Stub server disabled (build-only)");
    return Task.CompletedTask;
}
```

**Impact**: **HIGH** — QUIC data plane is non-functional

**Task**: T-1318 (needs creation)

---

#### 2.4 `QuicDataClient.cs` — **STUB (DISABLED)** 🚫
**Location**: `src/slskd/Mesh/Overlay/QuicDataClient.cs`

**Status**: **COMPLETELY DISABLED**
```csharp
/// Stub QUIC data-plane client; disabled for build.
public Task<bool> SendAsync(byte[] payload, IPEndPoint endpoint, CancellationToken ct = default)
{
    logger.LogDebug("[Overlay-QUIC-DATA] Stub client drop payload size={0} to {1}", payload.Length, endpoint);
    return Task.FromResult(false);
}
```

**Impact**: **HIGH** — QUIC data plane is non-functional

**Task**: T-1319 (needs creation)

---

### 3. Cryptography & Signing

#### 3.1 `KeyedSigner.cs` / `ControlSigner.cs` — **STUB VERIFICATION** 🚫
**Location**: `src/slskd/Mesh/Overlay/KeyedSigner.cs`

**Status**: **SIGNATURE VERIFICATION IS STUBBED**
```csharp
public bool Verify(ControlEnvelope envelope)
{
    if (string.IsNullOrWhiteSpace(envelope.PublicKey) || string.IsNullOrWhiteSpace(envelope.Signature))
    {
        return false;
    }

    // Stub verification always true for build
    return true;
}

private string ComputeSignature(ControlEnvelope envelope, byte[] privateKey)
{
    // Stub signature
    return Convert.ToBase64String(Encoding.UTF8.GetBytes("sig"));
}
```

**Impact**: **CRITICAL** — No actual signature verification, security is compromised

**Task**: T-1320 (needs creation)

---

#### 3.2 `KeyStore.cs` / `Ed25519KeyPair.Generate()` — **STUB KEY GENERATION** 🚫
**Location**: `src/slskd/Mesh/Overlay/KeyStore.cs:120`

**Status**: **KEY GENERATION IS STUBBED**
```csharp
public static Ed25519KeyPair Generate()
{
    // Stub: generate random bytes for pub/priv
    var priv = RandomNumberGenerator.GetBytes(32);
    var pub = RandomNumberGenerator.GetBytes(32);
    return new Ed25519KeyPair(pub, priv, DateTimeOffset.UtcNow);
}
```

**Impact**: **CRITICAL** — Not generating real Ed25519 keypairs, just random bytes

**Task**: T-1321 (needs creation)

---

### 4. Mesh Advanced Operations

#### 4.1 `MeshAdvancedImpl.cs` — **PLACEHOLDER** ⚠️
**Location**: `src/slskd/Mesh/MeshAdvancedImpl.cs`

**Status**: **PLACEHOLDER IMPLEMENTATIONS**

**4.1.1 `TraceRoutesAsync`** — Returns dummy data
```csharp
public Task<IReadOnlyList<MeshRouteDiagnostics>> TraceRoutesAsync(string peerId, CancellationToken ct = default)
{
    var diag = new List<MeshRouteDiagnostics>();
    diag.Add(new MeshRouteDiagnostics(peerId, "dht", 1, false));
    return Task.FromResult<IReadOnlyList<MeshRouteDiagnostics>>(diag);
}
```

**Impact**: **MEDIUM** — Route diagnostics are fake

**Task**: T-1310 (already exists)

---

**4.1.2 `GetTransportStatsAsync`** — Returns hardcoded zeros
```csharp
public Task<MeshTransportStats> GetTransportStatsAsync(CancellationToken ct = default)
{
    // Minimal counters for now (can be wired to real metrics later)
    var stats = new MeshTransportStats(
        ActiveDhtSessions: 0,
        ActiveOverlaySessions: 0,
        ActiveMirroredSessions: 0);
    return Task.FromResult(stats);
}
```

**Impact**: **MEDIUM** — No real statistics

**Task**: T-1311 (already exists)

---

### 5. Mesh Sync Service

#### 5.1 `MeshSyncService.cs` — **PARTIAL** ⚠️
**Location**: `src/slskd/Mesh/MeshSyncService.cs`

**5.1.1 Line 276: Mesh neighbor queries** — **TODO**
```csharp
// TODO: Query mesh neighbors
// For now, return null - mesh queries would be implemented
// when we have actual peer-to-peer message transport
return null;
```

**Impact**: **MEDIUM** — Cannot query mesh neighbors for hash lookups

**Task**: T-1322 (needs creation)

---

**5.1.2 Line 324: Username resolution** — **TODO**
```csharp
ClientId = "slskdn", // TODO: Get actual username
```

**Impact**: **LOW** — Hello messages use hardcoded client ID

**Task**: T-1323 (needs creation)

---

### 6. Control Dispatcher

#### 6.1 `ControlDispatcher.cs` — **STUB VERIFICATION** 🚫
**Location**: `src/slskd/Mesh/Overlay/ControlDispatcher.cs`

**Status**: Comment indicates signature verification is stub
```csharp
/// Handles overlay control envelopes (signature verification stub).
```

**Impact**: **HIGH** — Control envelope verification may be stubbed (needs verification)

**Task**: T-1324 (needs creation - verify and fix)

---

## Summary: Stub Count by Category

| Category | Stubs | Placeholders | TODOs | Total Issues |
|----------|-------|--------------|-------|--------------|
| **NAT Detection** | 1 | 0 | 1 | 2 |
| **Overlay Transport** | 4 | 0 | 0 | 4 |
| **Cryptography** | 2 | 0 | 0 | 2 |
| **Advanced Operations** | 0 | 2 | 0 | 2 |
| **Mesh Sync** | 0 | 0 | 2 | 2 |
| **Control** | 1 | 0 | 0 | 1 |
| **TOTAL** | **8** | **2** | **3** | **13** |

---

## Critical Issues Requiring Immediate Attention

### 🔴 **CRITICAL** (Security/Functionality Broken)

1. **Ed25519 Key Generation** (T-1321) — Not generating real keypairs
2. **Signature Verification** (T-1320) — Always returns true
3. **QUIC Overlay** (T-1316, T-1317, T-1318, T-1319) — Completely disabled

### 🟡 **HIGH** (Major Functionality Missing)

4. **NAT Detection** (T-1300) — Default detector is stub (but `StunNatDetector` exists)
5. **Control Dispatcher Verification** (T-1324) — May be stubbed

### 🟢 **MEDIUM** (Nice-to-Have Features)

6. **Route Diagnostics** (T-1310) — Returns dummy data
7. **Transport Stats** (T-1311) — Returns zeros
8. **Mesh Neighbor Queries** (T-1322) — Not implemented

---

## Recommendations

1. **IMMEDIATE**: Fix Ed25519 key generation and signature verification (security critical)
2. **HIGH PRIORITY**: Enable QUIC overlay transport or remove stub classes
3. **MEDIUM**: Wire up real transport statistics
4. **LOW**: Implement route diagnostics and mesh neighbor queries

---

*Audit completed: December 10, 2025*















