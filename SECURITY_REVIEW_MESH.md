# Mesh Overlay Security Review
**Date**: 2025-12-13  
**Branch**: `experimental/multi-source-swarm`  
**Reviewer**: AI Assistant (Claude Sonnet 4.5)  
**Status**: ⚠️ CRITICAL ISSUES FOUND

---

## Executive Summary

The mesh overlay system has **critical security vulnerabilities** that must be fixed before production use:

1. ❌ **Handshake signatures are NEVER verified** - any peer can claim any identity
2. ❌ **Handshake signature verification is IMPOSSIBLE** - missing `Timestamp` field in message
3. ⚠️  **Certificate pinning is NOT implemented** - TLS validation always returns `true`
4. ⚠️  **Replay protection is PARTIAL** - nonces exist but not enforced
5. ✅ **Rate limiting EXISTS and is active**
6. ✅ **Input validation is comprehensive**

---

## 1. Handshake Signature Validation ❌ CRITICAL

### Issue
The `MeshHelloMessage` includes `PublicKey` and `Signature` fields, but:
- The signature is **NEVER cryptographically verified**
- `MessageValidator.ValidateMeshHello()` only validates FORMAT, not authenticity
- Any attacker can connect claiming any `MeshPeerId` with a fake signature

###Files
- `src/slskd/DhtRendezvous/Security/MessageValidator.cs` (lines 120-212)
- `src/slskd/DhtRendezvous/MeshOverlayConnection.cs` (lines 279-300)

### Attack Scenario
```
Attacker → Server: HELLO {MeshPeerId="legitimate-peer-id", Signature="fake"}
Server → Attacker: ACK (accepts connection!)
```

### Root Cause
The comment on line 97 of `OverlayMessages.cs` says:
> "Signs: MeshPeerId + Features + Timestamp + Nonce"

But the `MeshHelloMessage` class **does NOT have a `Timestamp` field**! The client (connector) signs a timestamp at line 183:
```csharp
var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
var payloadToSign = BuildHandshakePayload(localMeshPeerId, timestamp);
```

But this timestamp is **never transmitted** in the HELLO message, making server-side verification impossible!

### Fix Required
1. Add `Timestamp` field to `MeshHelloMessage` (JSON property `timestamp`)
2. Client must include timestamp in the HELLO message
3. Server must call `LocalMeshIdentityService.Verify()` with:
   - Reconstructed payload: `MeshPeerId + Features + Timestamp`
   - Signature from message
   - Public key from message OR from DHT descriptor
4. Reject handshake if signature verification fails

### Recommended Implementation
```csharp
// In MeshHelloMessage
[JsonPropertyName("timestamp")]
public long TimestampUnixSeconds { get; set; }

// In MeshOverlayConnection.PerformServerHandshakeAsync()
var payloadToVerify = BuildHandshakePayload(
    hello.MeshPeerId, 
    hello.TimestampUnixSeconds);

var publicKey = Convert.FromBase64String(hello.PublicKey!);
var signature = Convert.FromBase64String(hello.Signature!);

if (!LocalMeshIdentityService.Verify(payloadToVerify, signature, publicKey))
{
    throw new SecurityException("Invalid handshake signature");
}
```

---

## 2. Certificate Pinning ⚠️ NOT IMPLEMENTED

### Issue
The connector (client) is supposed to validate server TLS certificates against SPKI pins from DHT descriptors. Currently:
- `QuicOverlayClient` and `QuicDataClient` have:
  ```csharp
  RemoteCertificateValidationCallback = (...) => true
  ```
- This **accepts ANY certificate**, making MITM attacks trivial

### Files
- `src/slskd/DhtRendezvous/MeshOverlayConnection.cs` (TLS stream creation)
- Missing: Pin verification against DHT descriptor

### Attack Scenario
```
Client → Attacker (MITM): TLS handshake
Attacker: Presents fake certificate
Client: Accepts (callback returns true)
```

### Fix Required
1. Fetch peer's `MeshPeerDescriptor` from DHT before connecting
2. Extract expected `TlsControlSpkiSha256` from descriptor
3. In TLS validation callback:
   ```csharp
   var certHash = CertificatePins.ComputeSpkiSha256(certificate);
   return CryptographicOperations.FixedTimeEquals(certHash, expectedPin);
   ```
4. Implement TOFU (Trust-On-First-Use) fallback if descriptor not found

### Status
- `CertificateManager` exists and persists certs ✅
- `CertificatePinStore` exists ✅
- But pin VERIFICATION is NOT wired up ❌

---

## 3. Replay Protection ⚠️ PARTIAL

### Issue
- `MeshHelloMessage` has a `Nonce` field
- But the server doesn't:
  - Check if nonce was seen before
  - Enforce timestamp freshness (< 5 minutes)

### Files
- `src/slskd/DhtRendezvous/Messages/OverlayMessages.cs` (line 122-124)
- Missing: Replay cache implementation

### Fix Required
1. Add `ReplayCache` class (per-IP or per-PeerId)
2. In `PerformServerHandshakeAsync()`:
   ```csharp
   if (_replayCache.Contains(hello.Nonce))
   {
       throw new SecurityException("Replay attack detected");
   }
   _replayCache.Add(hello.Nonce, TimeSpan.FromMinutes(10));
   ```

---

## 4. Rate Limiting ✅ IMPLEMENTED

### Status
- ✅ `OverlayRateLimiter` exists and is registered
- ✅ Tracks violations per-IP
- ✅ Enforced in `MeshOverlayConnector.ConnectToEndpointAsync()`

### Files
- `src/slskd/DhtRendezvous/Security/OverlayRateLimiter.cs`
- Used in: `MeshOverlayConnector.cs` (line 258)

### Recommendations
- Consider lowering burst limits if DoS attacks occur
- Add metrics for monitoring rate limit hits

---

## 5. Input Validation ✅ COMPREHENSIVE

### Status
- ✅ `MessageValidator` validates all incoming messages
- ✅ Uses constant-time comparison for magic strings
- ✅ Enforces length limits on all fields
- ✅ Uses regex for username/feature validation
- ✅ Checks port ranges, version bounds, etc.

### Files
- `src/slskd/DhtRendezvous/Security/MessageValidator.cs`

### Recommendations
- ✅ Already uses `CryptographicOperations.FixedTimeEquals` for magic
- ✅ Limits are reasonable (64-byte username, 20 features max, etc.)

---

## 6. Additional Concerns

### A. NAT Traversal Security
- NAT pre-flight happens BEFORE authentication
- Could be abused for UDP amplification attacks
- **Recommendation**: Rate-limit NAT traversal attempts per-IP

### B. Mesh Neighbor Registry
- `MeshNeighborRegistry` tracks connections
- But doesn't enforce max connections per peer
- **Recommendation**: Add limit (e.g., max 100 connections)

### C. Key Custody
- Identity keys stored in plaintext at `mesh-identity.key`
- No file permissions enforcement (should be 0600)
- **Recommendation**: Add:
  ```csharp
  if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || 
      RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
  {
      File.SetUnixFileMode(_keyPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
  }
  ```

---

## Priority Fixes (Before Dev Release)

### P0 (CRITICAL - Ship Blocker)
1. **Add `Timestamp` field to `MeshHelloMessage`** and fix signature payload
2. **Implement handshake signature verification** in server

### P1 (HIGH - Security Weakness)
3. **Implement certificate pinning** with TOFU fallback
4. **Add replay protection** with nonce cache

### P2 (MEDIUM - Hardening)
5. **Add key file permissions** (0600)
6. **Add max connections limit** to neighbor registry

---

## Testing Recommendations

1. **Test with self-signed certs** - verify pinning works
2. **Test with mismatched signatures** - verify handshake rejects
3. **Test replay attack** - send same nonce twice
4. **Test MITM attack** - use fake certificate
5. **Load test** - verify rate limiting kicks in

---

## Summary

The mesh overlay has excellent **input validation** and **rate limiting**, but critical **authentication** and **pinning** features are incomplete. The code EXISTS but is NOT WIRED UP.

**Current State**: 🔴 **NOT PRODUCTION READY**  
**After P0 Fixes**: 🟡 **TESTABLE** (signatures work)  
**After P0+P1 Fixes**: 🟢 **PRODUCTION READY** (full security)

---

## References

- Original Security Requirements: `memory-bank/decisions/tasks_security.md`
- Identity Service: `src/slskd/Mesh/Identity/LocalMeshIdentityService.cs`
- Message Validator: `src/slskd/DhtRendezvous/Security/MessageValidator.cs`
- Connector: `src/slskd/DhtRendezvous/MeshOverlayConnector.cs`

