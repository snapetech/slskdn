# Mesh Sync Security

**Created**: January 2026  
**Updated**: January 2026  
**Status**: Implemented (T-1430–T-1436, T-1434, T-1435). Unit tests (T-1437), integration tests (T-1438), WebGUI security metrics and **warnings**, **Mesh:SyncSecurity** config, **proof-of-possession**, and **consensus** are complete. See `database-poisoning-tasks.md` for full status.  
**Related**: `docs/security/database-poisoning-tasks.md`, `docs/security/database-poisoning-analysis.md`

---

## 1. Security Architecture Overview

Mesh sync distributes hash database entries (FLAC key → SHA256, size, metadata) across slskdn peers. The security model ensures that:

- **Only authenticated, trusted peers** can contribute data.
- **Invalid or abusive traffic** is rate-limited and leads to automatic quarantine.
- **All security-relevant events** are counted and exposed for monitoring.
- **Proof-of-possession (T-1434)** and **cross-peer consensus (T-1435)** can be enabled via `Mesh:SyncSecurity`.

### Implemented Layers

1. **Signature verification (T-1430)** – Every mesh message is signed with Ed25519; invalid or missing signatures are rejected.
2. **Reputation check (T-1431)** – Peers with reputation &lt; 20 are untrusted and cannot sync.
3. **Rate limiting (T-1432)** – Sliding-window limits on invalid entries and invalid messages.
4. **Automatic quarantine (T-1433)** – After 3 rate-limit violations in a 5‑minute window, a peer is quarantined for 30 minutes.
5. **Input validation** – FLAC keys, SHA256 hashes, sizes, and sequence IDs are validated before merge.
6. **Security metrics (T-1436)** – Counters for signature failures, reputation rejections, rate limits, quarantine, etc., exposed via `/api/v0/mesh/stats`.
7. **Proof-of-possession (T-1434)** – When `proof_of_possession_enabled` is true, new entries from a peer are challenged with ReqChunk (first 32KB); SHA256 must match `ByteHash`. Served via `IFlacKeyToPathResolver` (e.g. `ShareBasedFlacKeyToPathResolver`).
8. **Cross-peer consensus (T-1435)** – In `LookupHashAsync`, up to `consensus_min_peers` are queried; an entry is accepted only if ≥ `consensus_min_agreements` agree on (FlacKey, ByteHash, Size).

---

## 2. Threat Model

### In Scope (Mitigated)

| Threat | Mitigation |
|--------|------------|
| Forged mesh messages (impersonation) | Ed25519 signature verification; unsigned/invalid messages rejected |
| Untrusted peers syncing poisoned data | Reputation check; sync rejected if score &lt; 20 |
| Flood of invalid entries | Rate limit: 50 invalid entries per 5‑minute window → reject and record violation |
| Flood of invalid messages | Rate limit: 10 invalid messages per 5‑minute window → reject and record violation |
| Persistent abuser after rate limit | Quarantine: 3 violations in 5 minutes → 30‑minute quarantine |
| Fake hash entries that pass format checks | Proof-of-possession (T-1434): ReqChunk/RespChunk; reject if SHA256 of first 32KB ≠ ByteHash |
| Hash correctness (file not actually held) | Consensus (T-1435): require ≥ N peers to agree on (FlacKey, ByteHash, Size) in `LookupHashAsync` |

### Partially Addressed

| Threat | Status |
|--------|--------|
| Sybil (many low-reputation identities) | Reputation and quarantine reduce impact; no strong Sybil-resistant ID |

---

## 3. Mitigation Strategies

### 3.1 Signature Verification

- **Interface**: `IMeshMessageSigner` (sign / verify).
- **Behavior**: `MeshSyncService.HandleMessageAsync` verifies before dispatching. On failure: `SignatureVerificationFailures++`, `RejectedMessages++`, return `null`.
- **Formats**: `PublicKey`, `Signature`, `TimestampUnixMs` on `MeshMessage`; verification uses the same signer used for control messages.

### 3.2 Reputation Integration

- **Service**: `PeerReputation`; threshold for “untrusted”: score &lt; 20.
- **Where**: Start of `MergeEntriesAsync` (and effectively via `HandlePushDeltaAsync` → `MergeEntriesAsync`). Also in `HandleMessageAsync` before handling if a unified gate is used.
- **On reject**: `ReputationBasedRejections++`, `RejectedMessages++`, `RecordProtocolViolation(..., "Attempted mesh sync with untrusted reputation")`, return `0` / `null`.

### 3.3 Rate Limiting

- **Per‑peer state**: `MeshPeerState` holds `InvalidEntryTimestamps`, `InvalidMessageTimestamps`, and counts.
- **Entry limit**: 50 invalid entries in a 5‑minute sliding window.
- **Message limit**: 10 invalid messages in the same 5‑minute window.
- **On exceed**: `RateLimitViolations++`, `RecordRateLimitViolation`, `RecordProtocolViolation` (entries path), then `ShouldQuarantine` → `QuarantinePeer` if threshold reached.

### 3.4 Quarantine

- **Trigger**: 3 rate-limit violations within the 5‑minute rate-limit window.
- **Duration**: 30 minutes (extensions possible if already quarantined).
- **Effect**: `IsQuarantined` is checked at the start of `MergeEntriesAsync` and `HandleMessageAsync`; if true, reject and `RejectedMessages++`.
- **Metrics**: `QuarantineEvents` increments on each new or extended quarantine.

### 3.5 Input Validation

- **FLAC key**: 16 hex chars (`MessageValidator.ValidateFlacKey`).
- **SHA256 hash**: 64 hex chars (`MessageValidator.ValidateSha256Hash`).
- **File size**: 1 byte–10 GB (`MessageValidator.ValidateFileSize`).
- **SeqId**: non‑negative.
- **Structure**: `ValidateIncomingMessage` enforces entry count (e.g. max 2000), sequence ranges, and string lengths. Invalid entries are skipped; `SkippedEntries` and invalid‑entry rate limiting apply.

---

## 4. Configuration

All security parameters are configurable via **`Mesh:SyncSecurity`** (see `config/slskd.example.yml`). Defaults below.

| Option | Default | Meaning |
|--------|---------|---------|
| `max_invalid_entries_per_window` | 50 | Max invalid entries in the sliding window before rate limit |
| `max_invalid_messages_per_window` | 10 | Max invalid messages in the sliding window before rate limit |
| `rate_limit_window_minutes` | 5 | Sliding window length |
| `quarantine_violation_threshold` | 3 | Rate-limit violations in window that trigger quarantine |
| `quarantine_duration_minutes` | 30 | Quarantine duration |
| `proof_of_possession_enabled` | false | When true, run PoP (ReqChunk/RespChunk) before accepting new entries in `MergeEntriesAsync` |
| `consensus_min_peers` | 5 | Peers to query in `LookupHashAsync` for consensus |
| `consensus_min_agreements` | 3 | Min agreeing peers to accept a hash |
| `alert_threshold_signature_failures` | 50 | Add to `warnings` when exceeded; 0 = disabled |
| `alert_threshold_rate_limit_violations` | 20 | Add to `warnings` when exceeded; 0 = disabled |
| `alert_threshold_quarantine_events` | 10 | Add to `warnings` when exceeded; 0 = disabled |

---

## 5. Monitoring and Alerting

### 5.1 API: `GET /api/v0/mesh/stats`

Returns `MeshSyncStats`, including:

| Field | Description |
|-------|-------------|
| `signatureVerificationFailures` | Rejected messages due to bad/missing signature |
| `reputationBasedRejections` | Rejections because peer is untrusted |
| `rateLimitViolations` | Times the invalid-entry or invalid-message rate limit was hit |
| `quarantinedPeers` | Number of peers currently quarantined (if computed) |
| `quarantineEvents` | Total quarantine actions (new + extensions) |
| `rejectedMessages` | All rejected messages (validation, reputation, quarantine, etc.) |
| `skippedEntries` | Entries skipped in merge due to validation |
| `proofOfPossessionFailures` | Entries rejected in `MergeEntriesAsync` due to PoP failure (T-1434) |
| `warnings` | List of alert strings when `alert_threshold_*` are exceeded (signature, rate limit, quarantine) |

The WebGUI **Mesh Sync Security** block shows a warning message and list when `mesh.warnings` has items.

### 5.2 Suggested Alerts

- **signatureVerificationFailures** &gt; N in 5–15 minutes → possible impersonation or key/clock issues.
- **reputationBasedRejections** sustained ↑ → many untrusted peers attempting sync.
- **rateLimitViolations** or **quarantineEvents** sustained ↑ → abuse or misbehaving clients.
- **skippedEntries** / `totalEntriesReceived` &gt; 10% for a peer → already mitigated by rate limit and quarantine; useful for dashboards.

### 5.3 Logs

- `[MESH] Rejecting entries from quarantined peer {Peer}`
- `[MESH] Rejecting entries from untrusted peer {Peer} (score={Score})`
- `[MESH] Peer {Peer} exceeded invalid entry rate limit, rejecting remaining entries`
- `[MESH] Quarantined peer {Peer} until {Until} (reason: {...}, violations: {Count})`
- `[MESH] Skipped {Skipped}/{Total} invalid entries from {Peer}`

Search for `[MESH]` and `Rejecting`, `Quarantine`, `exceeded`, or `Skipped` to triage security events.

---

## 6. References

- `src/slskd/Mesh/MeshSyncService.cs` – Main sync and security logic; `LookupHashAsync` (consensus), `MergeEntriesAsync` (PoP), `HandleReqChunkAsync`. `QueryPeerForHashAsync` is `protected virtual` for test doubles (consensus-options tests).
- `src/slskd/Mesh/IMeshSyncService.cs` – `MeshSyncStats` and API surface.
- `src/slskd/Mesh/MeshSyncSecurityOptions.cs` – `Mesh:SyncSecurity` options.
- `src/slskd/Mesh/ProofOfPossessionService.cs`, `IProofOfPossessionService.cs`, `IChunkRequestSender.cs` – PoP (T-1434).
- `src/slskd/Mesh/IFlacKeyToPathResolver.cs`, `ShareBasedFlacKeyToPathResolver.cs`, `NoOpFlacKeyToPathResolver.cs` – Resolve FlacKey → path for ReqChunk.
- `src/slskd/Mesh/Messages/MeshMessages.cs` – `ReqChunk`, `RespChunk`, `MeshReqChunkMessage`, `MeshRespChunkMessage`.
- `src/slskd/Mesh/API/MeshController.cs` – `GET /api/v0/mesh/stats`.
- `src/slskd/Common/Security/PeerReputation.cs` – Reputation and `IsUntrusted`.
- `docs/security/database-poisoning-tasks.md` – Task status.
- `docs/security/database-poisoning-analysis.md` – Original threat analysis.
