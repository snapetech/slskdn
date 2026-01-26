# Database Poisoning Protection Analysis

**Created**: Dec 2025 · **Updated**: Jan 2026  
**Status**: Original analysis (Dec 2025). Implementation status as of **Jan 2026**: gaps 1–6 are **MITIGATED**. T-1430–T-1433 (signatures, reputation, rate limiting, quarantine), T-1434 (proof-of-possession), and T-1435 (consensus) are implemented. See `database-poisoning-tasks.md` and `mesh-sync-security.md` for current state.

---

## Current Protections ✅

### 1. Input Validation
- **Format Validation**: `MessageValidator` validates:
  - FLAC keys (16 hex chars)
  - SHA256 hashes (64 hex chars)
  - File sizes (1 byte - 10 GB)
  - Sequence IDs (non-negative)
  - Usernames (alphanumeric, max 64 chars)

- **Message Structure Validation**: `MeshSyncService.ValidateIncomingMessage()` checks:
  - Entry count limits (max 2000 per sync)
  - Sequence ID ranges
  - String length limits

### 2. Conflict Resolution
- `MergeEntriesFromMeshAsync()` handles conflicts by keeping the entry with higher `use_count`
- Prevents overwriting trusted data with newer but less-verified data

### 3. Reputation System
- `PeerReputation` tracks:
  - Successful/failed transfers
  - Protocol violations
  - Content mismatches
  - Malformed messages
- Can mark peers as untrusted (score < 20)

## Critical Gaps

### 1. ~~**NO Cryptographic Signatures**~~ ✅ **MITIGATED (T-1430)**
**Risk**: HIGH → mitigated  
- Ed25519 signatures added to mesh messages; verified in `HandleMessageAsync()`.
- Unsigned or invalidly signed messages rejected; `SignatureVerificationFailures` tracked.

### 2. ~~**Reputation Not Used for Mesh Sync**~~ ✅ **MITIGATED (T-1431)**
**Risk**: HIGH → mitigated  
- `PeerReputation.IsUntrusted()` checked at start of `MergeEntriesAsync()` (and via `HandlePushDeltaAsync`).
- Sync rejected for reputation &lt; 20; `ReputationBasedRejections` tracked.

### 3. ~~**No Proof of File Ownership**~~ ✅ **MITIGATED (T-1434)**
**Risk**: MEDIUM → mitigated  
- **T-1434 implemented:** When `Mesh:SyncSecurity.proof_of_possession_enabled` is true, `MergeEntriesAsync` challenges each new entry: ReqChunk (first 32KB), SHA256 compared to `ByteHash`. Failures increment `ProofOfPossessionFailures` and the entry is skipped. `ShareBasedFlacKeyToPathResolver` resolves FlacKey→path from shares for responding to ReqChunk.

### 4. ~~**No Rate Limiting on Invalid Data**~~ ✅ **MITIGATED (T-1432)**
**Risk**: MEDIUM → mitigated
- Sliding-window rate limiting: 50 invalid entries or 10 invalid messages per 5‑minute window. `RecordInvalidEntries` / `RecordInvalidMessage`; `RateLimitViolations` tracked.
**Impact** (obsolete; see T-1432):
- Attacker can flood the network with invalid entries
- Can degrade performance even if entries are rejected

**Recommendation**:
- Track invalid entry rate per peer
- Rate limit peers sending >10% invalid entries
- Automatically reduce reputation for high invalid rates

### 5. ~~**No Automatic Quarantine**~~ ✅ **MITIGATED (T-1433)**
**Risk**: MEDIUM → mitigated  
- 3 rate-limit violations in 5 minutes → 30‑minute quarantine. `QuarantinePeer`, `IsQuarantined`; `QuarantineEvents` tracked.

### 6. ~~**No Hash Correctness Verification**~~ ✅ **MITIGATED (T-1435)**
**Risk**: LOW-MEDIUM → mitigated  
- **T-1435 implemented:** `LookupHashAsync` queries up to `consensus_min_peers`, groups by (FlacKey, ByteHash, Size), and returns only if ≥ `consensus_min_agreements` peers agree. Config: `Mesh:SyncSecurity.consensus_min_peers`, `consensus_min_agreements`.

## Attack Scenarios

### Scenario 1: Simple Poisoning
1. Attacker modifies client to inject fake hash entries
2. Attacker joins network and syncs with peers
3. Fake entries propagate through mesh sync
4. **Current Protection**: Format validation catches malformed entries
5. **Gap**: Well-formed fake entries pass validation

### Scenario 2: Impersonation Attack
1. Attacker forges mesh sync messages claiming to be from trusted peer
2. Sends fake hash entries with trusted peer's username
3. Other peers accept data thinking it's from trusted source
4. **Current Protection**: None - no signature verification
5. **Gap**: No message authentication

### Scenario 3: Sybil Attack
1. Attacker creates multiple accounts
2. Each account syncs fake data
3. Fake data propagates through network
4. **Current Protection**: Reputation system exists
5. **Gap**: Reputation not checked during mesh sync

## Recommended Immediate Actions

*Priority 1 and 2 below are **implemented** (T-1430–T-1433). Priority 3 remains deferred.*

### Priority 1 (Critical) ✅ Done
1. **Add signature verification to mesh sync messages**
   - Use Ed25519 signatures (already have infrastructure)
   - Verify signatures before accepting any data
   - Reject unsigned or invalidly signed messages

2. **Integrate reputation checks into mesh sync**
   - Check `PeerReputation.IsUntrusted()` before accepting sync
   - Reject sync requests from untrusted peers
   - Log all rejections for monitoring

### Priority 2 (High) ✅ Done
3. **Implement rate limiting on invalid data**
   - Track invalid entry rate per peer
   - Rate limit peers with >10% invalid entries
   - Automatically reduce reputation

4. **Add automatic quarantine**
   - Auto-quarantine peers with reputation < 10
   - Block peers sending >50% invalid entries
   - Implement temporary bans

### Priority 3 (Medium) ✅ Done
5. **Add proof-of-possession challenges** – T-1434: ReqChunk/RespChunk, `ProofOfPossessionService`, `ShareBasedFlacKeyToPathResolver`; gated by `proof_of_possession_enabled`.
6. **Cross-validate hashes** – T-1435: `LookupHashAsync` uses `consensus_min_peers` and `consensus_min_agreements`.

## Implementation Notes

### Signature Integration
```csharp
// In MeshSyncService.HandleMessageAsync()
var signer = serviceProvider.GetRequiredService<IControlSigner>();
if (!signer.Verify(envelope))
{
    log.Warning("[MESH] Rejecting unsigned/invalid message from {Peer}", fromUser);
    stats.RejectedMessages++;
    return null;
}
```

### Reputation Check
```csharp
// In MeshSyncService.MergeEntriesAsync()
var reputation = serviceProvider.GetRequiredService<PeerReputation>();
if (reputation.IsUntrusted(fromUser))
{
    log.Warning("[MESH] Rejecting sync from untrusted peer {Peer}", fromUser);
    reputation.RecordProtocolViolation(fromUser, "Attempted sync while untrusted");
    return 0;
}
```

### Rate Limiting
```csharp
// Track invalid entry rate
var invalidRate = (double)stats.SkippedEntries / stats.TotalEntriesReceived;
if (invalidRate > 0.1) // 10% invalid
{
    log.Warning("[MESH] Peer {Peer} has high invalid rate: {Rate:P2}", fromUser, invalidRate);
    reputation.RecordMalformedMessage(fromUser);
}
```

## Conclusion

**Original (Dec 2025):** Critical gaps allowed database poisoning; recommendation was to implement signatures and reputation first.

**Current State (Jan 2026):** T-1430–T-1436, T-1434 (proof-of-possession), and T-1435 (consensus) are **implemented**. Mesh:SyncSecurity options, alerting (`warnings`), and `ShareBasedFlacKeyToPathResolver` are in place.

**Risk Level**: **HIGH → MITIGATED** for all analysed vectors (impersonation, untrusted sync, flooding, quarantine, proof-of-possession, hash correctness). Residual: Sybil (many low-reputation identities) partially addressed by reputation and quarantine.

**References:** `database-poisoning-tasks.md`, `mesh-sync-security.md`.

