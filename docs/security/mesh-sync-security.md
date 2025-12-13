# Mesh Sync Security Model (Phase 12S)

## Goals
- Prevent poisoned hash entries from entering HashDb via mesh sync.
- Authenticate mesh messages and enforce peer trust.
- Rate-limit and quarantine abusive peers.
- Require proof-of-possession and peer consensus before accepting new hashes.
- Provide operational visibility for security events.

## Threat Model
- **Message forgery / impersonation**: Attacker crafts mesh messages claiming to be trusted peers.
- **Untrusted peer poisoning**: Low-reputation peers inject bogus hashes.
- **Flood / DoS**: High-volume invalid messages or entries to exhaust resources.
- **Fake hash→file mappings**: Peers claim files they do not have.
- **Single-source trust**: Accepting hashes seen from only one peer.

## Defense Stack
1) **Cryptographic signatures (T-1430)**  
   - All mesh messages are signed (Ed25519) and verified in `MeshSyncService.HandleMessageAsync`. Unsigned/invalid signatures are rejected and counted as `SignatureVerificationFailures`.

2) **Reputation gate (T-1431)**  
   - `PeerReputation.IsUntrusted()` blocks merges and records protocol violations. Stats increment `ReputationBasedRejections`.

3) **Input validation (existing)**  
   - `MessageValidator` enforces FLAC key, SHA256, size, SeqId bounds per message type.

4) **Rate limiting (T-1432)**  
   - Sliding window (5m): max 50 invalid entries or 10 invalid messages. Violations increment `RateLimitViolations`.

5) **Automatic quarantine (T-1433)**  
   - 3 rate-limit violations → 30-minute quarantine. Quarantined peers are rejected early; `QuarantineEvents` and `QuarantinedPeers` reflect state.

6) **Proof-of-possession (T-1434)**  
   - New hashes require a challenge: requester asks peer to return the first 32KB chunk and recomputes SHA256 to match `ByteHash`. Results are cached 30 minutes to avoid repeat challenges.

7) **Peer consensus (T-1435)**  
   - New hashes require agreement from ≥3 peers (sender + at least two others from `HashDb.GetPeersByHashAsync`). Consensus is cached 60 minutes; without consensus the entry is rejected.

8) **Observability (T-1436)**  
   - Security metrics exposed via `/api/v0/mesh/stats`:
     - `SignatureVerificationFailures`
     - `ReputationBasedRejections`
     - `RateLimitViolations`
     - `QuarantinedPeers`
     - `QuarantineEvents`
     - `RejectedMessages`
     - `SkippedEntries`
     - Plus overall sync counters (`TotalSyncs`, `TotalEntriesReceived`, etc.).

## Message Types
- `HELLO`, `REQDELTA`, `PUSHDELTA`, `REQKEY`, `RESPKEY`, `ACK`
- **Proof-of-possession (new)**:
  - `CHAL` (`MeshChallengeRequestMessage`): challenge id, flac key, byte hash, offset, length.
  - `CHALRESP` (`MeshChallengeResponseMessage`): challenge id, flac key, success flag, error, data (chunk).

All messages are signed and validated before dispatch.

## Request Flow (Secure)
1) Receive signed message → signature verify → username + message validation.
2) Quarantine check → reputation check.
3) If `PUSHDELTA`/entries:
   - Validate fields.
   - Proof-of-possession challenge for new hashes.
   - Consensus check across peers.
   - Merge valid entries; update `SkippedEntries`/`RejectedMessages`/metrics as needed.

## Configuration Defaults (MeshSyncService constants)
- Rate limiting: `MaxInvalidEntriesPerWindow = 50`, `MaxInvalidMessagesPerWindow = 10`, `RateLimitWindowMinutes = 5`.
- Quarantine: `QuarantineViolationThreshold = 3`, `QuarantineDurationMinutes = 30`.
- Consensus: `ConsensusRequiredPeers = 3`, cache 60 minutes.
- Proof-of-possession: chunk length 32KB, cache 30 minutes, challenge timeout 10s.

## Monitoring & Operations
- **API**: `GET /api/v0/mesh/stats` for real-time security counters.
- **Logs**: Warnings emitted for signature failures, reputation rejections, rate limits, quarantines, failed proofs, and missing consensus.
- **Actions**:
  - Investigate peers with repeated `SignatureVerificationFailures` or `RateLimitViolations`.
  - Quarantine is automatic; reputation can be adjusted via `PeerReputation` if needed.

## Limitations / Follow-ups
- Challenge responder now attempts to read from local shares via `IShareService.ResolveFileAsync`; will fail if inventory lacks a local path for the flac key. Future improvement: wire stronger inventory → share mapping to increase hit rate.
- Consensus relies on `HashDb` inventory quality; improve peer discovery/backfill to broaden peer sets.
- Socket permissions may block automated test runs in constrained environments; rerun tests where listeners are allowed.

## Files & Components
- `MeshSyncService`: signature verification, rate limiting, quarantine, consensus gating, challenge orchestration.
- `MeshMessageSigner`: Ed25519 signing/verification.
- `ProofOfPossessionService`: challenge generation/verification and cache.
- `MeshMessages`: challenge request/response types.
- `IMeshSyncService.Stats`: security metrics surface to `/api/v0/mesh/stats`.
