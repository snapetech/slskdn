# Moderation / Control Plane (MCP) – v1 Design

**Repo:** `github.com/snapetech/slskdn`  
**Branch:** `experimental/multi-source-swarm`  
**Status:** Draft design  
**Scope:** Moderation & control plane for content and peers across VirtualSoulfind, library scanning, DHT/mesh advertisement, and relay services.

> **Project Note**: This is a fork of [slskd](https://github.com/slskd/slskd). See [README.md](../../README.md#acknowledgments) for attribution.

---

## 1. Goals & Non-Goals

### Goals

1. **Centralize moderation decisions**  
   All decisions about whether content or peers are allowed, blocked, or quarantined must go through a shared Moderation / Control Plane (MCP), not scattered `if` conditions.

2. **Protect against prohibited content**  
   Prevent known illegal or harmful material and other blocked content from being:
   * Indexed into VirtualSoulfind's catalogue.
   * Advertised to other peers.
   * Served via content relay / CDN.

3. **Enable pluggable policy**  
   Allow node operators to plug in:
   * Hash-based blocklists (for known prohibited content).
   * Optional external moderation providers.
   * Reputation systems for peer behavior.

4. **Tie moderation into planner and relay behavior**  
   Ensure VirtualSoulfind, DHT advertisement, and relay services respect moderation decisions.

### Non-Goals

* MCP does **not** attempt to automatically classify arbitrary media at a deep semantic level.
* MCP does **not** ship any real-world prohibited-content lists in the repo. It only provides interfaces so operators can connect to trusted sources themselves.
* MCP is not a user-facing "reporting system UI"; UI is out of scope beyond APIs.

---

## 2. Concepts & Terminology

* **Prohibited content** – Any file or content ID that local policy or external providers deem illegal, harmful, or disallowed.
* **Blocked content** – Content that must never be advertised or served to others.
* **Quarantined content** – Content kept only for local/legal reasons, never shared or relayed.
* **Allowed content** – Content that MCP has no objection to; may still be subject to sharing preferences.
* **Unknown** – MCP has no data; caller may fall back to default policies.
* **Moderation decision** – A structured verdict about a particular file or content ID.
* **Peer reputation** – A score or flagging system about a peer's behavior (e.g., repeatedly sharing blocked content).

---

## 3. High-Level Architecture

The MCP sits beside VirtualSoulfind and service fabric, with clear APIs:

* **IModerationProvider** – primary interface for moderation decisions.
* **Hash blocklist provider** – checks hashes against configured blocked-content lists.
* **Reputation provider** – tracks peer behavior and produces peer-level decisions.
* **(Optional) External moderation provider** – for operators who want to connect external services.

Key integration points:

1. **Local library scanning / indexing** – before a file is added to catalogue.
2. **VirtualSoulfind advertisable flag** – before content is considered shareable.
3. **DHT / mesh advertisement** – before announcing "I have content X".
4. **Content relay** – before serving chunks.
5. **Peer management** – when peers are associated with blocked content.

---

## 4. Core Interfaces & Data Structures

### 4.1 Moderation verdicts

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
    public string? Reason { get; init; }             // e.g. "hash_blocklist", "peer_banned"
    public string[] EvidenceKeys { get; init; }      // e.g. IDs for logs, not raw content
}
```

### 4.2 Moderation provider

```csharp
public interface IModerationProvider
{
    // Check a local file (before indexing / sharing).
    Task<ModerationDecision> CheckLocalFileAsync(
        LocalFileMetadata file,
        CancellationToken ct);

    // Check by content ID (e.g. VirtualSoulfind item/content ID).
    Task<ModerationDecision> CheckContentIdAsync(
        string contentId,
        CancellationToken ct);

    // Allow reporting for escalation / reputation.
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

`LocalFileMetadata` includes:
* Canonical path or ID.
* Size, hashes, and basic media properties (no raw content).

`ModerationReport` / `PeerReport` are structured, minimal types (reason codes, timestamps, etc.), not arbitrary text dumps.

### 4.3 Sub-providers

Internally, MCP composes multiple sub-providers:

* `IHashBlocklistChecker` – checks hashes against configured blocked-content lists:
  ```csharp
  public interface IHashBlocklistChecker
  {
      Task<bool> IsBlockedHashAsync(string hash, CancellationToken ct);
  }
  ```

* `IPeerReputationStore` – tracks peer events (e.g., associated with blocked content, spammy behavior).

* Optional `IExternalModerationClient` – for talking to external moderation APIs (image/video detectors, etc.) if operators choose to.

`IModerationProvider` orchestrates these and returns a single `ModerationDecision`.

---

## 5. Integration Points

### 5.1 Local Library Scanner / Indexer

When scanning local content:

1. Extract `LocalFileMetadata`:
   * Size, hashes, media properties.
2. Call `IModerationProvider.CheckLocalFileAsync`.
3. Behavior based on verdict:
   * `Blocked`:
     * Do not index into VirtualSoulfind.
     * Do not add to shareable sets.
     * Optionally log an internal warning for the operator (no raw content shown).
   * `Quarantined`:
     * Store only minimal internal reference if needed (e.g., for audit), but never share.
   * `Allowed` / `Unknown`:
     * Proceed with normal indexing and VirtualSoulfind mapping.

This ensures prohibited content never enters the catalogue as shareable items.

### 5.2 VirtualSoulfind – Advertisable Flag

VirtualSoulfind maintains an internal notion of whether a track/item is **advertisable**.

Before marking a `ContentItemId` as advertizable:
* Call `IModerationProvider.CheckContentIdAsync(contentId)`.

Rules:
* `Blocked` or `Quarantined` → never advertizable.
* `Allowed` / `Unknown` → allowed to be advertizable (relying on operator's share preferences).

VirtualSoulfind's planner and DHT announcement logic must only consider advertizable items when offering content to others.

### 5.3 DHT / Mesh Advertisement

When announcing "I have content X" into DHT/mesh:
* Only advertise content IDs that VirtualSoulfind has already flagged as advertizable.
* If any later MCP decision changes an item from `Allowed` to `Blocked` or `Quarantined`:
  * Stop serving / advertising that content ID.
  * If the DHT supports it, withdraw or mark that advertisement as invalid.

### 5.4 Content Relay / CDN

In `ContentRelayService`:
* When a `ContentChunkRequest` arrives:
  * Map `ContentId` to VirtualSoulfind item.
  * Confirm it is advertizable and **not** blocked by MCP.
* If content is blocked/quarantined:
  * Return an appropriate error.
  * Optionally feed a negative event into `IPeerReputationStore` for the requesting peer if they repeatedly request blocked content.

Only content that passes moderation and is marked as advertizable should ever be served over the network.

### 5.5 Peer Reputation & Enforcement

When a peer:
* Offers content that later hashes to a blocked value, or
* Is repeatedly associated with content/behavior flagged as abusive,

then:
* `ReportPeerAsync(peerId, report)` should be called with the appropriate reason.
* `IPeerReputationStore` updates the peer's score or flags.

VirtualSoulfind and the service fabric can then:
* Prefer other peers for content.
* Decrease their allowed concurrency and work quotas.
* Eventually ban or ignore them completely based on local config.

---

## 6. Configuration

Example configuration section for MCP:

```jsonc
"Moderation": {
  "Enabled": true,

  "HashBlocklist": {
    "Enabled": true,
    "Provider": "ExternalApi",            // "ExternalApi", "LocalFile", "None"
    "ApiEndpoint": "https://example.com/blocklist/check",
    "ApiKey": "env:MODERATION_HASH_API_KEY",
    "TimeoutSeconds": 3
  },

  "Reputation": {
    "Enabled": true,
    "AutoBanThreshold": 5,                // number of serious events before ban
    "DecayPeriodDays": 30
  },

  "ExternalModeration": {
    "Enabled": false,                     // off by default
    "Provider": "None",
    "MaxRequestsPerHour": 10
  }
}
```

Defaults must be conservative:
* Core MCP is on, hash-based checks are supported but require operator configuration.
* External moderation is off by default.
* Auto-bans can be configured or disabled per operator's risk appetite.

---

## 7. Observability

### Metrics

* `mcp_file_checks_total{verdict=...}` – count of file-level checks by verdict.
* `mcp_content_checks_total{verdict=...}` – count of content-ID checks by verdict.
* `mcp_peer_reports_total{reason=...}` – reports about peers.
* `mcp_blocklist_checks_total{result=hit|miss|error}` – blocklist server interactions.

No metrics should include file names, hashes, or external identifiers as labels.

### Logging

* Log high-level decisions:
  * "ContentId X blocked by moderation (reason=hash_blocklist)."
  * "Peer Y marked as high-risk (reason=repeated_blocked_content)."
* Do not log:
  * Raw hashes.
  * File paths beyond sanitized/shortened form.
  * Any actual content.

---

## 8. Security & Privacy Notes

* The repo ships **only interfaces and placeholders** for blocklist and external moderation.
* Real blocklist providers, keys, and endpoints are configured by operators, not hard-coded.
* The system is designed so that:
  * Prohibited content is never distributed further once identified.
  * Local scanning does only minimal work needed to compute hashes/metadata.
* External moderation (if enabled by an operator) must be treated as an opt-in integration, not a default feature.

---

## 9. Open Questions / Future Work

* Sharing aggregated reputation between trusted pods or friends.
* Optional "community reporting" mechanisms (allowing users to flag content/peers).
* More granular moderation policies (e.g., per-library or per-namespace).

---

## Dependencies & Integration Order

### Prerequisites
- **H-02 (Work Budget)** ✅ COMPLETE - MCP needs work budget for external calls
- **T-SF05 (Security Hardening)** ✅ COMPLETE - Security logging patterns
- **V2-P1-P4 (VirtualSoulfind v2)** - Content catalogue, planner (blocks T-MCP03)
- **T-PR03 (Content Relay)** - Content chunk serving (blocks T-MCP03)

### Implementation Order
1. **T-MCP01** (Core interfaces) - Can start immediately
2. **T-MCP02** (Library scanning) - Depends on T-MCP01
3. **T-MCP03** (VirtualSoulfind + Relay) - Depends on T-MCP01 + V2-P4 + T-PR03
4. **T-MCP04** (Peer reputation) - Depends on T-MCP01

### Critical Path
```
T-MCP01 (Core) → READY TO START
    ↓
    ├─→ T-MCP02 (Scanning) ← LOCAL LIBRARY SCANNER
    ├─→ T-MCP03 (VirtualSoulfind) ← V2-P4, T-PR03
    └─→ T-MCP04 (Reputation) ← T-MCP01
```

---

## Security Philosophy Alignment

This MCP design aligns with the project's "paranoid bastard" security philosophy:

✅ **Default-Deny**: Content is blocked unless proven safe  
✅ **Least Privilege**: Only advertize/serve content that passes moderation  
✅ **Work Budget Integration**: External moderation calls consume budget  
✅ **Structured Logging**: No sensitive data in logs (hashes, paths hidden)  
✅ **Operator Control**: Pluggable providers, no hard-coded lists  
✅ **Peer Reputation**: Bad actors get deprioritized/banned  
✅ **Privacy-First**: No external identifiers in VirtualSoulfind  

---

## Why This Is Critical

Without MCP:
- ❌ No way to prevent prohibited content from being shared
- ❌ Scattered `if` checks throughout the codebase
- ❌ No peer reputation → bad actors persist
- ❌ Legal/ethical liability for operators

With MCP:
- ✅ Centralized moderation decisions
- ✅ Pluggable policy (hash lists, external APIs)
- ✅ Content never advertized/served if blocked
- ✅ Peer reputation → bad actors get banned
- ✅ Operator-controlled (no hard-coded lists)
- ✅ Observability (metrics, structured logging)
