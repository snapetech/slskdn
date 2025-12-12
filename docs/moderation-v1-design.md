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

---

## 10. Source Classification and Moderation Scope

Moderation applies to **all** sources of content and metadata:

- Local files (scanned from disk).
- Remote peers:
  - Mesh / DHT peers.
  - Soulseek peers.
  - Torrent peers (to the extent we identify them).
- Social sources:
  - ActivityPub instances and actors.
- HTTP/remote APIs:
  - Catalogue fetch targets and any HTTP-based metadata sources.

We classify sources into types:

- `LocalSource` – local files.
- `ProtocolSource` – per-protocol, e.g. `MeshPeer`, `SoulseekPeer`, `TorrentPeer`.
- `SocialSource` – ActivityPub instance + actor.
- `HttpSource` – remote host/domain for HTTP interactions.

Each source type can generate events in the reputation system, used by MCP for enforcement.

---

## 11. Moderation Integration Points

Moderation MUST be applied at multiple points:

1. **Scanning / Ingestion**
   - When scanning local files:
     - Run `IModerationProvider.CheckLocalFileAsync(LocalFileMetadata)` before marking a file as shareable.
   - Files with verdict `Blocked`/`Quarantined`:
     - MUST NOT be exposed as shareable content.
     - MAY be quarantined in a separate state for operator review.

2. **VirtualSoulfind Content Linking**
   - When linking local files to `ContentItemId` / `ContentWorkId`:
     - MCP MUST be consulted via `CheckContentIdAsync`.
     - Items resulting in `Blocked`/`Quarantined` MUST NOT be marked `IsAdvertisable`.

3. **DHT / Mesh / Torrent Advertisement**
   - Any advertisement of content availability on DHT/mesh/torrent MUST:
     - Be gated by MCP status.
     - Avoid advertising items that are blocked or quarantined.

4. **Content Relay / CDN**
   - `IContentRelayService` MUST:
     - Only serve chunks for items marked `IsAdvertisable`.
     - Refuse requests for blocked/quarantined items.
   - Repeated requests for blocked items from the same source MAY generate negative reputation events.

5. **Social Federation**
   - WorkRefs published via ActivityPub MUST:
     - Only refer to content for which MCP allows advertisement.
   - Inbound social content:
     - Instances and actors can produce negative events if they repeatedly reference blocked or abusive content.

6. **Planner and Recommendations**
   - VirtualSoulfind planner MUST:
     - Consider MCP verdicts and reputation when selecting:
       - Sources for acquisition.
       - Recommendations and upgrade suggestions.
   - Peers or sources with poor reputation MUST be avoided or down-ranked.

---

## 12. Reputation and Enforcement

The reputation system is shared across all protocols and social layers.

- Each source can accrue:
  - Positive events (good behavior).
  - Negative events (associations with blocked content, spam, abuse).

- MCP can use reputation to:
  - Automatically ban certain sources.
  - Tighten quotas and work budgets.
  - Influence planner decisions (avoid low-reputation sources).

- Reputation MUST NOT be used to bypass:
  - Blocklists.
  - Direct MCP `Blocked` verdicts.
  - Security policies.

When a source is banned:

- All content from that source MUST be:
  - Excluded from planner consideration.
  - Excluded from advertisement and relay.
  - Excluded from recommendation ranking.

---

## 13. Operator Controls

Operators MUST have the ability to:

- Configure blocklists and allowlists for:
  - Hashes/content IDs.
  - Protocol sources (peers).
  - Social sources (instances/actors).
  - HTTP targets (catalogue endpoints).

- Override or clear reputation state:
  - Unban a previously banned source (with appropriate warnings).
  - Manually ban a problematic source.

- Inspect moderation state:
  - Quarantined content.
  - Recent moderation events and their reasons (no PII or external handles).

All operator controls MUST respect logging/metrics hygiene rules (no raw paths, hashes, IPs, or external handles in logs where not strictly necessary for admin tools).

---

## 14. MCP Security Model & Attack Surface Analysis

### 14.1 MCP is Local, Not Distributed

**Critical Invariant**: MCP is a **local service/component inside a single pod**, not a distributed network service.

**Who Can Access MCP**:
- ✅ Only internal subsystems in your pod, via strongly typed interfaces
- ✅ Scanner, VirtualSoulfind, DHT/mesh/torrent advertising, content relay, social federation
- ❌ No remote MCP API that peers can call directly
- ❌ No public, unauthenticated network port for MCP

**Result**: A remote peer **cannot** tell your MCP what to do. At most they send traffic (files/notes/etc.) that your pod then asks MCP about.

### 14.2 Control & Authority

**Operator Control**:
- Operator controls MCP via configuration and build:
  - Which providers are registered (hash/blocklist, reputation, optional LLM)
  - How they are composed in `CompositeModerationProvider`
  - Global policies (how to handle Blocked/Quarantined/Unknown)

**No External Control**:
- External peers **cannot**:
  - Add/remove MCP providers
  - Change MCP policies
  - Override MCP decisions
- These require explicit configuration changes by the operator

### 14.3 What MCP Can and Cannot Do

**MCP Can** (by design):
- Label content and sources: `Allowed`, `Blocked`, `Quarantined`, `Unknown`
- Add reason codes and confidence flags
- Influence system behavior (scanner, VirtualSoulfind, advertising, relay, social)

**MCP Cannot** (architectural guarantees):
- Execute arbitrary code in other subsystems
- Push state to other pods
- Override hard-coded invariants (when using proper verdict aggregation)
- Mutate configuration or blocklists (providers are read-only classifiers)

**Mental Model**: MCP is a **local classifier + gate**, not a distributed controller.

### 14.4 Verdict Lattice & Aggregation Semantics

**Verdict Ordering** (severity, least to most):
```
Unknown < Allowed < Quarantined < Blocked
```

**Aggregation Rule** (MANDATORY):

`CompositeModerationProvider` MUST use **max severity** across all providers:

```csharp
public async Task<ModerationDecision> CheckLocalFileAsync(LocalFileMetadata file, CancellationToken ct)
{
    var decisions = new List<ModerationDecision>();
    
    // Collect decisions from all providers
    if (_hashBlocklist != null)
        decisions.Add(await _hashBlocklist.CheckAsync(file, ct));
    if (_reputation != null)
        decisions.Add(await _reputation.CheckAsync(file, ct));
    if (_llm != null && _config.Llm.Mode != Off)
        decisions.Add(await _llm.CheckAsync(file, ct));
    
    // Take MAX severity (most restrictive wins)
    return decisions.OrderByDescending(d => d.Verdict).First();
}
```

**Guarantees**:
- If **any** provider says `Blocked`, final verdict = `Blocked`
- If highest is `Quarantined` and none say `Blocked`, final = `Quarantined`
- Only if everything is `Allowed`/`Unknown` do we get `Allowed` or `Unknown`

**Result**: No provider (including LLM) can ever **downgrade** a more-severe verdict.

### 14.5 LLM-Specific Attack Surface

#### 14.5.1 LLM's Place in the Chain

LLM provider is **one** `IModerationProvider` in the composite, called **last**:

1. **Hash/blocklists** → If `Blocked`, we stop; LLM never runs
2. **Cheap deterministic filters** / source reputation
3. **LLM provider** → Only if:
   - Enabled in `ModerationConfig.Llm`
   - Budgets/rate limits allow

#### 14.5.2 Who Can Attach an LLM

**No Dynamic Attachment**:
- Nobody on the network can dynamically attach their LLM to your MCP
- LLM is used **only** if:
  - Operator sets `ModerationConfig.Llm.Mode = Local` or `Remote`
  - Operator points it at a specific endpoint (with domain allowlists)
  - Credentials are stored in config (never logged)

**Protections**:
- SSRF-safe HTTP client (domain allowlists, HTTPS-only)
- No arbitrary URL acceptance from peers
- Credentials protected by `IDataProtectionProvider`

#### 14.5.3 Malicious LLM: Worst-Case Scenarios

A malicious or mis-tuned LLM (that **you** wired in) can cause:

**1. Over-Blocking**:
- LLM returns high disallowed scores for everything
- Scanner quarantines large parts of library
- Planner refuses to use content
- Social layer doesn't publish anything
- **Impact**: Hurts only your pod
- **Mitigation**: Set `Mode = Off` or swap endpoint

**2. Under-Blocking** (false negatives):
- LLM says `Allowed` for items that should be blocked
- Only affects items **not** in hash/blocklist
- **Impact**: Your moderation is no better than without LLM
- **Cannot**: Override hash/blocklist decisions (due to max severity rule)
- **Cannot**: Affect other pods

**3. Data Leakage** (to remote LLM):
- Misconfigured remote LLM might see more metadata than intended
- **Mitigations already in place**:
  - Data minimization modes: `MetadataOnly`, `MetadataPlusShortSnippet`
  - No file paths, peer IDs, IPs, external handles
  - No raw library dumps or entire files

#### 14.5.4 What a Malicious LLM Cannot Do

Due to architectural constraints:

❌ **Cannot** install itself into other pods  
❌ **Cannot** override hash lists or local policies  
❌ **Cannot** execute code paths outside "classify this" call  
❌ **Cannot** unblock anything that hash/blocklist has blocked  
❌ **Cannot** mutate configuration or blocklists  
❌ **Cannot** push decisions to other pods  
❌ **Cannot** cause network-wide meltdown  

✅ **Can only**: Ruin UX for its own operator (if misconfigured)

### 14.6 Network-Wide Attack Scenarios

**Q: Can an attacker use LLM+MCP to globally break the network?**

**A: No, given our design.**

**Why Not**:

1. **MCP decisions are not federated by default**:
   - No "central MCP server" that everyone trusts
   - No "push my moderation decisions to others" API

2. **Reputation is per-node**:
   - You choose which external feeds (if any) to trust
   - Other pods do not automatically trust your MCP output

3. **No single point of failure**:
   - For network-wide impact, attacker would need many operators to voluntarily point MCP at same hostile LLM
   - Even then, operators retain:
     - Own blocklists (max severity prevents downgrades)
     - Own local policies
     - Ability to flip `Mode = Off` or switch providers

**Cross-Node Channels** (none expose raw MCP internals):
- Transport: Soulseek, mesh, torrent, HTTP (content-based, not decision-based)
- Social: ActivityPub (WorkRefs, not raw MCP verdicts)
- Future: Optional blocklist/reputation feeds (opt-in, operator-controlled)

### 14.7 Mandatory Invariants (Enforcement)

To maintain security guarantees, implementations MUST enforce:

#### Invariant 1: Verdict Lattice & Max Severity

```csharp
// REQUIRED: Use max severity across all providers
public enum ModerationVerdict
{
    Unknown = 0,    // Least severe
    Allowed = 1,
    Quarantined = 2,
    Blocked = 3     // Most severe
}

// CompositeModerationProvider MUST use:
var finalVerdict = decisions.Max(d => d.Verdict);
```

**Result**: No provider can downgrade a more-severe verdict.

#### Invariant 2: Write-Protection (Providers Are Read-Only Classifiers)

MCP providers (including LLM) MUST NOT be able to:
- Add/remove blocklist entries
- Change configuration
- Mutate reputation state directly

Providers **only** return `ModerationDecision` objects.

**Who can mutate state**:
- Admin tools (explicit operator action)
- External feeds (opt-in, with separate interfaces)
- Configuration files (operator-controlled)

```csharp
// Good: Provider returns decision
public interface IModerationProvider
{
    Task<ModerationDecision> CheckAsync(...); // Read-only classification
}

// Bad: Provider mutates state
public interface IModerationProvider
{
    Task AddToBlocklistAsync(...); // FORBIDDEN
    Task UpdateConfigAsync(...);   // FORBIDDEN
}
```

#### Invariant 3: Default-Off & Opt-In

All LLM features MUST be:
- `Mode = Off` by default
- Require explicit operator configuration to enable
- Document risks in configuration comments

```jsonc
"Llm": {
  "Mode": "Off", // REQUIRED DEFAULT: Off | Local | Remote
  // Warning: Remote mode sends metadata to external service.
  // See docs/moderation-v1-design.md § 14.5 for risk analysis.
}
```

### 14.8 Testing Security Properties

Add tests to verify security invariants:

```csharp
[Fact]
public async Task CompositeModerationProvider_BlockedVerdictCannotBeDowngraded()
{
    // Arrange: One provider says Blocked, one says Allowed
    var blockingProvider = CreateProvider(ModerationVerdict.Blocked);
    var allowingProvider = CreateProvider(ModerationVerdict.Allowed);
    var composite = new CompositeModerationProvider(blockingProvider, allowingProvider);
    
    // Act
    var decision = await composite.CheckAsync(file);
    
    // Assert: Blocked wins (max severity)
    Assert.Equal(ModerationVerdict.Blocked, decision.Verdict);
}

[Fact]
public async Task LlmModerationProvider_CannotMutateBlocklist()
{
    // Verify LLM provider only implements read-only classification interface
    var provider = new LlmModerationProvider(...);
    
    // Should not compile if provider has mutation methods
    // Assert.False(provider is IBlocklistMutator);
}

[Fact]
public async Task ModerationConfig_LlmModeDefaultsToOff()
{
    var config = new ModerationConfig();
    Assert.Equal(LlmMode.Off, config.Llm.Mode);
}
```

### 14.9 Summary: Risk Containment

**What's Protected**:
- ✅ Network cannot be globally disrupted by malicious LLM
- ✅ Hash/blocklists cannot be overridden by LLM
- ✅ LLM is opt-in, off by default
- ✅ LLM decisions aggregated with max severity (most restrictive wins)
- ✅ LLM cannot mutate configuration or blocklists
- ✅ Data minimization prevents excessive metadata exposure
- ✅ SSRF protections prevent arbitrary external calls

**What's Not Protected**:
- ⚠️ Operator can misconfigure their own pod (self-harm)
- ⚠️ Over-blocking by LLM can degrade local UX
- ⚠️ Under-blocking by LLM (for items not in hash/blocklist)

**Mental Model**:
- MCP with LLM is like having a **local, opt-in, advisory classifier**
- It can make your pod's moderation better or worse (depending on LLM quality)
- It **cannot** escape its sandbox or harm other pods
- Operator retains full control (can disable at any time)

**Recommendation**:
- Start with `Mode = Off` (default)
- If using LLM, start with `Local` mode (better privacy)
- Monitor LLM behavior (metrics, logs) before trusting it
- Maintain strong hash/blocklists (LLM is supplementary, not primary)

---
