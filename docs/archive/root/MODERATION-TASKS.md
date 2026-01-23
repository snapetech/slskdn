# Moderation / Control Plane (MCP) Tasks

**Design Doc**: `docs/moderation-v1-design.md`  
**Hardening Doc**: `MCP-HARDENING.md` 🔒 **MANDATORY**  
**Status**: Planned (4 tasks)  
**Priority**: HIGH (legal/ethical protection for operators)

> **Project Note**: This is a fork of [slskd](https://github.com/slskd/slskd). See [README.md](README.md#acknowledgments) for attribution.

---

## ⚠️ CRITICAL: Read MCP Hardening Doc FIRST

**Before implementing ANY MCP task**, you MUST read and follow `MCP-HARDENING.md`.

The MCP layer handles:
- Hash checks against blocklists (potentially prohibited content)
- Peer reputation (behavior tracking)
- External moderation services (third-party APIs)

**Special security requirements**:
- 🔒 Never log raw hashes or full paths
- 🔒 SSRF protection for all external calls
- 🔒 Timing attack mitigation for hash checks
- 🔒 Peer ID anonymization in logs
- 🔒 Failsafe mode (block on error)
- 🔒 Encrypted reputation storage

**See `MCP-HARDENING.md` for complete requirements.**

---

## Task Summary

| Task | Description | Status | Dependencies |
|------|-------------|--------|--------------|
| **T-MCP01** | Core MCP interfaces & composite provider | 📋 Planned | None (can start immediately) |
| **T-MCP02** | Library scanning integration | 📋 Planned | T-MCP01 |
| **T-MCP03** | VirtualSoulfind + Content Relay integration | 📋 Planned | T-MCP01, V2-P4, T-PR03 |
| **T-MCP04** | Peer reputation & enforcement | 📋 Planned | T-MCP01 |

---

## T-MCP01 – Introduce Moderation Core & Interfaces

### Repo: `https://github.com/snapetech/slskdn`
### Branch: `experimental/multi-source-swarm`

**Goal**: Implement the **core moderation layer** (MCP), including the main interfaces, verdict types, and a simple composite moderation provider.

> 🔒 **MANDATORY**: Read `MCP-HARDENING.md` BEFORE starting this task!
> - Never log raw hashes or full paths
> - SSRF protection for external services
> - Failsafe mode (block on error)
> - Peer ID anonymization in logs

### 0. Scope & Non-Goals

You **must**:
1. Implement `ModerationVerdict`, `ModerationDecision`, `IModerationProvider`.
2. Implement basic sub-provider interfaces:
   * `IHashBlocklistChecker`
   * `IPeerReputationStore`
   * (Optional stub) `IExternalModerationClient`
3. Provide a `CompositeModerationProvider` that:
   * Orchestrates sub-providers.
   * Returns a single `ModerationDecision`.

You **must NOT**:
* Integrate MCP into scanning, VirtualSoulfind, or relay yet (that's later tasks).
* Hard-code any real-world blocklists or external endpoints.
* Change any existing public APIs beyond what's necessary to introduce these new interfaces.

### 1. Recon

Find:
1. Where local files and content IDs are modeled (`LocalFile`, `LocalFileMetadata`, VirtualSoulfind `ContentItemId`).
2. Where peer IDs and reputation could live (e.g., existing peer state, metrics).
3. Existing config loading infrastructure for new sections (JSON config, env mapping).

### 2. Core Types

Add:

```csharp
public enum ModerationVerdict
{
    Allowed,
    Blocked,
    Quarantined,
    Unknown
}

public sealed class ModerationDecision
{
    public ModerationVerdict Verdict { get; init; }
    public string? Reason { get; init; } = null;
    public string[] EvidenceKeys { get; init; } = Array.Empty<string>();
}
```

Define `LocalFileMetadata` if not present as a lightweight DTO used by MCP (you can wrap existing file models):

```csharp
public sealed class LocalFileMetadata
{
    public string Id { get; init; }           // internal ID or path key
    public long SizeBytes { get; init; }
    public string PrimaryHash { get; init; }  // e.g. SHA256
    public string? SecondaryHash { get; init; }
    public string? MediaInfo { get; init; }   // optional summary string
}
```

### 3. MCP Interfaces

Create `IModerationProvider`:

```csharp
public interface IModerationProvider
{
    Task<ModerationDecision> CheckLocalFileAsync(
        LocalFileMetadata file,
        CancellationToken ct);

    Task<ModerationDecision> CheckContentIdAsync(
        string contentId,
        CancellationToken ct);

    Task ReportContentAsync(
        string contentId,
        ModerationReport report,
        CancellationToken ct);

    Task ReportPeerAsync(
        string peerId,
        PeerReport report,
        CancellationToken ct);
}
```

Define minimal report types:

```csharp
public sealed class ModerationReport
{
    public string ReasonCode { get; init; }   // e.g. "user_flagged", "external_signal"
    public string? Notes { get; init; }
}

public sealed class PeerReport
{
    public string ReasonCode { get; init; }   // e.g. "associated_with_blocked_content"
    public string? Notes { get; init; }
}
```

Sub-provider interfaces:

```csharp
public interface IHashBlocklistChecker
{
    Task<bool> IsBlockedHashAsync(string hash, CancellationToken ct);
}

public interface IPeerReputationStore
{
    Task RecordPeerEventAsync(string peerId, PeerReport report, CancellationToken ct);
    Task<bool> IsPeerBannedAsync(string peerId, CancellationToken ct);
}

public interface IExternalModerationClient
{
    Task<ModerationDecision> AnalyzeFileAsync(LocalFileMetadata file, CancellationToken ct);
}
```

For now, `IExternalModerationClient` can be stubbed as needed.

### 4. Composite Moderation Provider

Implement `CompositeModerationProvider : IModerationProvider` that:

* Accepts these dependencies (some may be null):
  * `IHashBlocklistChecker? hashBlocklist`
  * `IPeerReputationStore? peerReputation`
  * `IExternalModerationClient? externalClient`

* `CheckLocalFileAsync`:
  1. If `hashBlocklist` is configured:
     * Check `PrimaryHash`.
     * If blocked → return `Blocked` with reason `"hash_blocklist"`.
  2. Optionally call `externalClient` (if enabled via config) for additional signals.
  3. If nothing flags the file:
     * Return `Unknown` (or `Allowed` depending on policy; stay conservative and prefer `Unknown`).

* `CheckContentIdAsync`:
  * For now, implement a simple placeholder that returns `Unknown`. Future tasks will wire content IDs → hash/file metadata.

* `ReportContentAsync`:
  * No-op initially or log internally for debugging.

* `ReportPeerAsync`:
  * If `peerReputation` is configured, forward the report.

Do not implement heavy logic here; detailed behavior will evolve in later tasks.

### 5. Configuration & DI

Add a basic `Moderation` config section:

```jsonc
"Moderation": {
  "Enabled": true,
  "HashBlocklist": {
    "Enabled": false,
    "Provider": "None"
  },
  "ExternalModeration": {
    "Enabled": false,
    "Provider": "None"
  },
  "Reputation": {
    "Enabled": true
  }
}
```

Wire up DI:
* If `Moderation.Enabled == false`:
  * Register a simple `NoopModerationProvider` that always returns `Unknown`.
* If enabled:
  * Register `CompositeModerationProvider` with whichever sub-providers are configured.
  * Sub-providers can be stubs for now.

### 6. Tests (T-MCP01)

Add tests to ensure:
1. `CompositeModerationProvider`:
   * Returns `Blocked` if `IHashBlocklistChecker` reports blocked hash.
   * Returns `Unknown` when no providers are configured and input is non-empty.
2. `ReportPeerAsync`:
   * Calls `IPeerReputationStore.RecordPeerEventAsync` when configured.

### 7. Anti-Slop Checklist for T-MCP01

* No real-world blocklists or endpoints are hard-coded.
* MCP is introduced as a clean, composable interface layer.
* Default provider is conservative (`Unknown` when in doubt).

### 8. Security Compliance (MCP-HARDENING.md)

**MANDATORY**: Verify these requirements from `MCP-HARDENING.md`:

- [x] No raw hashes in logs (use 8-char prefix max for debugging)
- [x] No full filesystem paths in logs (filename only)
- [x] Evidence keys are opaque identifiers (not raw data)
- [x] Failsafe mode configured (default: "block")
- [x] Configuration validation at startup
- [x] Security tests for logging (verify no leaks)

---

## T-MCP02 – Integrate Moderation into Local Library Scanning / Indexing

### Repo: `https://github.com/snapetech/slskdn`
### Branch: `experimental/multi-source-swarm`

**Goal**: Wire `IModerationProvider` into the **local library scanner / indexer**, so prohibited/blocked files are never added to VirtualSoulfind as shareable content.

> 🔒 **MANDATORY**: Follow `MCP-HARDENING.md` requirements!
> - Sanitize ALL logging (no hashes, no full paths)
> - Work budget integration for external calls
> - Encrypted persistence for blocked file records

### 0. Scope & Non-Goals

You **must**:
1. Call `IModerationProvider.CheckLocalFileAsync` for files discovered during local scanning.
2. Prevent files with `Blocked` or `Quarantined` verdicts from being indexed as shareable items.
3. Ensure this integration is robust and does not significantly regress performance.

You **must NOT**:
* Change the user-facing library layout or sharing semantics beyond excluding blocked files.
* Implement VirtualSoulfind or relay integration here; this task is **scanner-only**.

### 1. Recon

Find:
1. Library scan/refresh pipeline:
   * Where files are discovered under configured directories.
   * Where file metadata/hashes are computed.
   * Where they are persisted as `LocalFile` / equivalent.
2. Where VirtualSoulfind is told about new files (if already wired).

### 2. Construct LocalFileMetadata

In the scanner:
* After computing the primary hash and basic metadata for a file, construct `LocalFileMetadata`:

```csharp
var metadata = new LocalFileMetadata
{
    Id = localFileIdOrPath,
    SizeBytes = fileSize,
    PrimaryHash = computedHash,
    SecondaryHash = null,
    MediaInfo = computedMediaInfoSummary
};
```

Use existing hash/metadata; do not add heavy new work here.

### 3. Call IModerationProvider

Before committing the file to the library/catalog:

```csharp
var decision = await moderationProvider.CheckLocalFileAsync(metadata, cancellationToken);
```

Apply decision:
* `ModerationVerdict.Blocked`:
  * Do not persist this file as part of the shareable library.
  * Optionally mark it in a dedicated "blocked" table or log a sanitized message.
* `ModerationVerdict.Quarantined`:
  * Do not mark it shareable or pass it to VirtualSoulfind.
  * Optionally persist minimal reference (ID + decision) for internal tracing.
* `Allowed` or `Unknown`:
  * Proceed with current behavior (persist as usual).

### 4. Marking Files in the Local DB

Extend the local file schema if needed:
* Add fields such as:
  * `IsBlocked` (bool, default false).
  * `IsQuarantined` (bool, default false).

Enforce:
* When `IsBlocked` or `IsQuarantined` is true:
  * VirtualSoulfind and sharing logic must treat the file as non-shareable (later tasks will enforce this; for now, do not include such files in any "shareable" sets).

### 5. Logging & Metrics

* Log a **sanitized** entry when a file is blocked or quarantined:
  * Include:
    * Internal ID or sanitized path.
    * `decision.Reason`.
  * Do not include full paths or hashes.

* Emit metrics:
  * `mcp_file_checks_total{verdict=...}` – increment with appropriate `verdict` label.

### 6. Tests (T-MCP02)

Add tests to verify:
1. When `IModerationProvider` returns `Blocked`:
   * The scanner does not store the file as shareable.
   * `IsBlocked` flag is set if you persist the record.
2. When `Quarantined`:
   * The file is not shareable; `IsQuarantined` is set.
3. When `Allowed`/`Unknown`:
   * Existing behavior remains unchanged.

Use a fake `IModerationProvider` in tests to control verdicts.

### 7. Anti-Slop Checklist for T-MCP02

* Every scanned file routes through `IModerationProvider` before being marked shareable.
* Blocked/quarantined files are kept out of the usual shareable sets.
* No hashes or full paths are logged in cleartext.

### 8. Security Compliance (MCP-HARDENING.md)

**MANDATORY**: Verify these requirements from `MCP-HARDENING.md`:

- [x] Hash handling: Never log full hash (Section 1.1)
- [x] Path sanitization: Only filename or internal ID (Section 1.2)
- [x] Logging format: `[SECURITY] MCP blocked file | InternalId={Id} | Reason={Reason}`
- [x] Metrics: No sensitive data in labels (Section 7.1)
- [x] Work budget: External moderation consumes budget (Section 2.1)
- [x] Security tests: Verify no hash/path leaks (Section 6.1)

---

## T-MCP03 – Integrate Moderation into VirtualSoulfind Advertisable Decisions & Content Relay

### Repo: `https://github.com/snapetech/slskdn`
### Branch: `experimental/multi-source-swarm`

**Goal**: Use MCP to control what VirtualSoulfind treats as **advertisable content**, and ensure content relay never serves blocked items.

> 🔒 **MANDATORY**: Apply `MCP-HARDENING.md` to content relay!
> - Check moderation BEFORE serving any chunks
> - Report peers requesting blocked content
> - No content IDs in logs (use opaque references)

### 0. Scope & Non-Goals

You **must**:
1. Add an "advertisable" flag in VirtualSoulfind linked to moderation decisions.
2. Ensure only advertizable content is:
   * Considered for DHT/mesh advertisement.
   * Eligible to be served via `ContentRelayService`.
3. Call `IModerationProvider.CheckContentIdAsync` for relevant content IDs.

You **must NOT**:
* Change how VirtualSoulfind matches or deduplicates content beyond gating by moderation.
* Implement new blocklist providers; just use `IModerationProvider` as-is.

### 1. Recon

Find:
1. VirtualSoulfind content item model:
   * Where `ContentItemId` (or TrackId/ItemId) is defined and stored.
2. Where VirtualSoulfind:
   * Marks items as shareable / advertizable.
   * Enumerates items for DHT/mesh advertisement.
3. `ContentRelayService` implementation from T-PR03:
   * Where it maps `ContentId` to local files.

### 2. Advertisable Flag in VirtualSoulfind

Extend the VirtualSoulfind item record to include:
* `IsAdvertisable` (bool, default false).

When VirtualSoulfind processes new items (e.g., when linking a `LocalFile` to a `ContentItemId`):

1. Obtain the `contentId` string (e.g., the serialized `ContentItemId` or a canonical key).

2. Call `CheckContentIdAsync(contentId)`:
   ```csharp
   var decision = await moderationProvider.CheckContentIdAsync(contentId, ct);
   ```

3. Set `IsAdvertisable` based on verdict:
   * `Blocked` or `Quarantined` → `IsAdvertisable = false`.
   * `Allowed` or `Unknown` → `IsAdvertisable = true` (subject to user share settings).

Keep the "share/unshare" user preferences as an additional layer; `IsAdvertisable` is purely moderation-driven.

### 3. DHT / Mesh Advertisement Filtering

Wherever content IDs are selected for advertisement:
* Filter only items where:
  * `IsAdvertisable == true`, and
  * Local sharing is enabled for that item.

If VirtualSoulfind later changes `IsAdvertisable` (e.g., because MCP decisions changed):
* Ensure:
  * Newly non-advertisable items are not used in future advertisements.
  * Optionally, withdraw them from DHT if supported.

### 4. Content Relay Integration

In `ContentRelayService`:

1. Once a `ContentId` is mapped to a VirtualSoulfind item, check `IsAdvertisable`.
2. If `IsAdvertisable == false`:
   * Do not serve any chunks.
   * Return an error/result indicating the content is not available.
3. Optionally, call `CheckContentIdAsync` again **only if cheap**:
   * In most cases, rely on the stored `IsAdvertisable` flag to avoid repeated MCP calls.

Make sure this is enforced before any file I/O is performed.

### 5. Negative Signals to MCP

When `ContentRelayService` receives repeated requests from a peer for content that is not advertisable or is blocked:

* Optionally call:
  ```csharp
  await moderationProvider.ReportPeerAsync(peerId, new PeerReport
  {
      ReasonCode = "requested_blocked_content"
  }, ct);
  ```

`IPeerReputationStore` can use this to adjust peer trust (T-MCP04).

### 6. Tests (T-MCP03)

Add tests to verify:
1. VirtualSoulfind marks items as advertizable correctly based on mocked `IModerationProvider` responses.
2. DHT/mesh advertisement selection includes only `IsAdvertisable == true` items.
3. `ContentRelayService`:
   * Serves chunks only for advertizable items.
   * Rejects requests for non-advertizable items.

Use fakes for `IModerationProvider` and VirtualSoulfind's item DB in tests.

### 7. Anti-Slop Checklist for T-MCP03

* No content is advertised or served unless `IsAdvertisable == true`.
* `IsAdvertisable` is driven by moderation, not ad-hoc logic.
* Relay checks are in place before any file data is returned.

### 8. Security Compliance (MCP-HARDENING.md)

**MANDATORY**: Verify these requirements from `MCP-HARDENING.md`:

- [x] Content relay: Check `IsAdvertisable` before ANY file I/O (Section 4)
- [x] Peer reporting: Call `ReportPeerAsync` for blocked content requests (Section 5)
- [x] Logging: No content IDs that could identify files (Section 1.4)
- [x] Metrics: Track relay rejections without exposing content details
- [x] Security tests: Verify relay rejects non-advertisable content (Section 6.1)

---

## T-MCP04 – Peer Reputation & Enforcement Integration

### Repo: `https://github.com/snapetech/slskdn`
### Branch: `experimental/multi-source-swarm`

**Goal**: Add a **peer reputation** layer and tie it into MCP, VirtualSoulfind planner, and service fabric to deprioritize or ban peers associated with blocked content or repeated abuse.

> 🔒 **CRITICAL**: `MCP-HARDENING.md` Section 3 is MANDATORY!
> - Sybil resistance via rate limiting
> - Encrypted reputation storage (DataProtection API)
> - Hashed peer IDs for logging (SHA256 + salt)
> - Reputation decay to prevent permanent bans

### 0. Scope & Non-Goals

You **must**:
1. Implement a simple `IPeerReputationStore` with:
   * Recording of peer events.
   * A minimal ban/flagging mechanism.
2. Ensure MCP calls into `IPeerReputationStore` when appropriate:
   * When content associated with a peer is blocked.
   * When peers repeatedly request blocked content.
3. Integrate peer reputation into:
   * VirtualSoulfind's planner (source selection).
   * Service fabric (work budget/quotas per peer).

You **must NOT**:
* Implement complex distributed reputation sharing (keep it local to one node).
* Expose peer reputation externally beyond aggregate stats/logging.

### 1. Recon

Find:
1. Where peers are identified and tracked:
   * Mesh peer IDs.
   * Soulseek peer IDs (separate from VirtualSoulfind; you'll use opaque IDs at the MCP level).
2. VirtualSoulfind planner:
   * Where candidates are ranked/selected.
3. Work budget / quotas:
   * Where per-peer budgets are enforced.

### 2. Implement IPeerReputationStore

Backed by a simple local store (e.g., in-memory + optional persistent DB table), implement:

* `RecordPeerEventAsync(peerId, report)`:
  * Increment counters per peer and reason.
  * If certain reason codes (e.g., `associated_with_blocked_content`) exceed thresholds, mark the peer as "banned" or "high risk".

* `IsPeerBannedAsync(peerId)`:
  * Returns true if peer is marked "banned".
  * When true, higher layers should avoid using this peer as a source or deny their requests entirely.

Config example:

```jsonc
"Moderation": {
  "Reputation": {
    "Enabled": true,
    "AutoBanThreshold": 3,           // number of serious events
    "EventWeights": {
      "associated_with_blocked_content": 2,
      "requested_blocked_content": 1
    }
  }
}
```

You do not need a fancy scoring algorithm; a simple "weighted events vs threshold" is enough for v1.

### 3. Hook MCP into Reputation

Extend `CompositeModerationProvider`:
* When content associated with a specific peer is found to be blocked (this association may come from later tasks—content→source mapping):
  * Call `IPeerReputationStore.RecordPeerEventAsync(peerId, report)`.
* When a peer makes repeated requests for blocked content, `ContentRelayService` or calling code should invoke `ReportPeerAsync`, which then uses `IPeerReputationStore`.

Be careful not to leak external usernames/IPs into VirtualSoulfind; use internal peer IDs.

### 4. Planner Integration

Modify VirtualSoulfind planner:
* When ranking candidate sources (peers):
  * For each candidate peer:
    * Check `IsPeerBannedAsync(peerId)`:
      * If banned → skip this candidate.
    * Otherwise:
      * Optionally down-rank peers with poor reputation (if you expose a score; not required for v1).

* Ensure that banned peers are:
  * Not selected for new downloads.
  * Not considered for new connections (where applicable).

### 5. Service Fabric Integration

Where work budgets and quotas are enforced per peer:
* If `IsPeerBannedAsync(peerId)` is true:
  * Treat any new requests from that peer as:
    * Immediately rejected, or
    * Allowed only to a minimal, safe subset of services (e.g., error/status queries).

This prevents high-risk peers from consuming significant resources.

### 6. Tests (T-MCP04)

Add tests:
1. `IPeerReputationStore`:
   * Recording events and crossing thresholds → `IsPeerBannedAsync` returns true.
2. MCP integration:
   * `ReportPeerAsync` causes `IPeerReputationStore.RecordPeerEventAsync` to be called.
3. Planner integration:
   * Banned peers are not selected as sources.
4. Service fabric integration:
   * Requests from banned peers are rejected by relevant services.

Use fakes/mocks for planner and mesh context in tests.

### 7. Anti-Slop Checklist for T-MCP04

* Reputation is entirely local; no accidental cross-node sharing.
* Banned peers are consistently excluded from planner and service-level work.
* No external identifiers (usernames/IPs) are stored directly in VirtualSoulfind; only internal peer IDs are used in MCP and reputation.

### 8. Security Compliance (MCP-HARDENING.md)

**MANDATORY**: Verify these requirements from `MCP-HARDENING.md` Section 3:

- [x] Sybil resistance: Rate limit peer events (max 10/min) (Section 3.1)
- [x] Peer ID hashing: `HashPeerId()` for all logging (Section 3.1)
- [x] Encrypted storage: Use `IDataProtectionProvider` (Section 3.2)
- [x] Reputation decay: Exponential decay over 30 days (Section 3.1)
- [x] Auto-ban threshold: Configurable weighted score
- [x] Logging: `[SECURITY] Peer auto-banned | PeerHash={Hash} | Score={Score}`
- [x] Security tests: Verify event flooding prevention (Section 6.1)

---

## Integration with Existing Systems

### Work Budget (H-02) ✅
- External moderation calls consume work budget
- Hash blocklist checks consume minimal units (0.5 units suggested)
- Peer reputation checks are essentially free (local lookup)

### Security Logging (T-SF05) ✅
- MCP decisions logged via SecurityEventLogger
- Structured format: `[SECURITY] Moderation | Verdict: Blocked | Reason: hash_blocklist`
- No sensitive data (hashes, paths) in logs

### VirtualSoulfind v2 (V2-P1 through V2-P6)
- T-MCP03 integrates with content catalogue
- `IsAdvertisable` flag added to content items
- Planner respects moderation decisions

### Proxy/Relay (T-PR03)
- Content Relay checks moderation before serving
- Repeated blocked requests trigger reputation events

### Observability (OBSERVABILITY-CHECKLIST.md)
- MCP metrics added to introspection service
- Stats: file checks, content checks, peer reports by verdict
- GetModerationStats() API for monitoring

---

## Why This Matters

### Legal/Ethical Protection
- Operators can prevent prohibited content distribution
- Pluggable hash blocklists (connect to trusted sources)
- Quarantine capability for compliance

### Peer Reputation
- Bad actors get automatically deprioritized
- Repeated offenders banned from consuming resources
- Work budget enforcement for high-risk peers

### Operator Control
- No hard-coded lists (bring your own blocklist)
- External moderation opt-in (not default)
- Configurable ban thresholds

### Privacy-First
- No raw hashes in logs
- Sanitized paths only
- No external identifiers in metrics

---

## Future Enhancements (Post-v1)

- **Distributed Reputation**: Share reputation between trusted pods
- **Community Reporting**: User-flagged content/peers
- **ML-Based Detection**: Optional image/video classification
- **Granular Policies**: Per-library or per-namespace moderation
- **Reputation Decay**: Auto-unban after time period
- **Blocklist Sync**: Automatic updates from trusted sources

