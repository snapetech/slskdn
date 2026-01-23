# Federation Security & Hardening

**Status**: MANDATORY REQUIREMENTS - Security Layer  
**Created**: December 12, 2025  
**Priority**: 🔴 CRITICAL (security-sensitive)

> **Project Note**: This is a fork of [slskd](https://github.com/slskd/slskd). See [../README.md](../README.md#acknowledgments) for attribution.

---

This document defines concrete security and hardening requirements for all **federation-related features** in the project, including:

- **ActivityPub / social federation** (`SocialFeedModule`, `social-federation-design`)
- **Realm-aware peering and multi-realm bridges** (`realm-design`)
- **Gossip feeds** (HealthFeed / AbuseFeed / future ReplicationNeedFeed)
- **Replication of small objects across pods** (`replication-policy-design`)

---

## Guiding Principles

- ✅ **Isolation by default** – No unintended cross-instance or cross-realm connectivity
- ✅ **Least privilege** – Only the minimal flows and data needed are allowed
- ✅ **Explicit trust** – Federation and peering must be driven by explicit config
- ✅ **Fail-closed** – When in doubt, reject or drop remote input, not accept it

---

## 1. ActivityPub & Social Federation Hardening

**Scope:**
- All ActivityPub endpoints used by `SocialFeedModule` and related components
- Both intra-realm and cross-realm federation via AP

### 1.1 Inbound AP Requests

**Inbound AP requests MUST:**

**Authentication:**
- ✅ Be authenticated if required by the AP spec:
  - Verify HTTP signatures where applicable
  - Validate `actor` URL, host, and key association
- ✅ Be origin-checked:
  - The `actor`'s host must match the request's `Host` / `Signature` key origin
  - Requests from mismatched hosts MUST be rejected

**Validation:**

**Payload:**
- ✅ Validate JSON-LD / ActivityPub structure
- ✅ Reject malformed objects early

**Size limits:**
- ✅ Enforce strict max body size for AP requests (configurable, conservative defaults)

**Rate limiting:**
- ✅ Per remote host + per actor:
  - Limit number of inbound activities per time window
  - Block or slow down abusive senders

**Content handling:**
- ✅ All user-supplied content (HTML/Markdown/etc.) MUST be sanitized:
  - Remove scripts, inline JS, and dangerous tags/attributes
  - Prevent XSS in web UIs and API consumers

**Failure behavior (Fail closed):**
- ✅ Unknown activity types → drop or log
- ✅ Invalid signatures or origins → reject with appropriate HTTP codes
- ✅ Over-size payloads → HTTP 413 (or equivalent)

---

### 1.2 Outbound AP Requests

**Outbound AP requests MUST:**

**Target validation:**
- ✅ Only be sent to:
  - Known, validated actor endpoints
  - Hosts not present in local blocklists
- ✅ Respect per-instance policies:
  - Strongly recommended: allow/deny lists for instances
  - Per-realm and per-bridge controls for cross-realm federation

**Failure behavior:**

**No retry loops:**
- ✅ Implement capped retries + backoff

**No unbounded fanout:**
- ✅ Limit the number of remote followers/instances per activity

**Privacy:**

**Outbound activities MUST NOT:**
- ❌ Include private internal data (internal IDs, logs, secrets)
- ❌ Leak more user metadata than ActivityPub semantics require

---

### 1.3 Federation Modes & Defaults

**Federation MUST be controlled by explicit modes** (see `social-federation-design`):

- **`Off`** – No AP federation, only local
- **`Hermit`** – Minimal exposure; only selected actors or endpoints are visible
- **`Federated`** – Full AP federation within configured policies

**Defaults:**

**For early/critical pods (e.g., First Pod):**
- ✅ Default to `Hermit` or `Off` unless explicitly changed

**Mode changes:**
- ✅ MUST be explicit admin actions
- ✅ SHOULD be logged and, where possible, confirmed via UI/CLI prompts

---

## 2. Realm & Bridge Hardening (Cross-Realm Federation)

**Scope:**
- Realm definitions and multi-realm participation (`realm-design`)
- Bridge pods that join multiple realms
- Cross-realm flows: ActivityPub, metadata, gossip, replication

### 2.1 Realm Trust Model

**Trust requirements:**
- ❌ `realm.id` alone MUST NOT be considered sufficient to trust remote governance
- ✅ Pods MUST validate:
  - `realm.id` AND `governance_roots` before treating governance docs as valid

**Misconfig detection:**
- ⚠️ If `realm.id` suggests a known realm (e.g., `"slskdn-main-v1"`) but root keys do not match expectations:
  - Log a loud warning
  - Treat governance from that realm as untrusted

---

### 2.2 Bridge Configuration

**For multi-realm pods:**

**Defaults:**
- ✅ `bridge.enabled` MUST default to `false`

**When `bridge.enabled = true`:**
- ✅ Only flows listed in `bridge.allowed_flows` are permitted
- ✅ Flows in `bridge.disallowed_flows` MUST always be denied, even if misconfigured

**Dangerous flows (DENY BY DEFAULT):**
- 🚨 `governance:root` – Treat remote realm's governance as authoritative
- 🚨 `replication:fullcopy` – Allow full-copy replication across realms
- 🚨 `mcp:control` – Allow remote realm to modify MCP config

**These flows SHOULD NOT be allowed in any example configs or presets.**

---

### 2.3 Cross-Realm Flows

**Cross-realm flows MUST follow:**

**ActivityPub:**
- ✅ Same AP hardening rules as intra-realm
- ✅ Remote realms treated as untrusted instances by default

**Metadata:**
- ✅ Only public metadata exposed
- ❌ No user-specific or private data unless explicitly intended and documented

**Gossip:**
- ✅ Only advisory health/abuse signals (coarse, no PII)
- ✅ Remote feeds MUST be treated as untrusted input

**Replication:**
- ❌ No cross-realm replication of objects unless:
  - Explicitly allowed by both realms' policies
  - Covered by replication policy (object type, size, MCP checks)

**All cross-realm logic MUST:**
- ✅ Run through strict allow/deny flows configured in `RealmConfig` and `bridge` sections
- ✅ Be fully disableable via config

---

## 3. Gossip Feeds (Health/Abuse) Hardening

**Gossip feeds** (`gossip-signals-design`) **MUST obey:**

### 3.1 Content Constraints

**NO PII:**
- ❌ No IP addresses
- ❌ No usernames, emails, or other identifiers

**NO raw logs:**
- ❌ No full stack traces, HTTP error bodies, or similar

**ONLY:**
- ✅ Aggregated, coarse metrics
- ✅ Content hashes and severity for AbuseFeed

---

### 3.2 Validation & Isolation

**Inbound feeds MUST be:**
- ✅ Signed
- ✅ Schema-validated
- ✅ Size-limited

**Inbound feeds MUST be treated as untrusted hints:**
- ✅ Combined with local observations
- ✅ Capped so they cannot fully override local HealthScore or MCP decisions

**Outbound feeds MUST respect local policy:**
- ✅ Option to opt out of publishing entirely
- ✅ Option to anonymize or coarsen further

**Any feed endpoint MUST:**
- ✅ Be rate-limited
- ✅ Be protected against abuse and excessive scraping

---

## 4. Replication Security (Small Objects)

**Replication** (`replication-policy-design`) **MUST respect:**

### 4.1 Strict Whitelisting

**Only objects explicitly allowed by policy MAY be replicated:**
- ✅ Governance docs (F1000 registries, policy profiles)
- ✅ Moderation lists
- ✅ Small metadata objects

**NEVER:**
- ❌ Arbitrary filesystem paths
- ❌ Private user content, unless a dedicated, consent-based feature exists

---

### 4.2 Handshake & Policy Enforcement

**Replication handshake MUST:**
- ✅ Mutually authenticate pods via pod identity
- ✅ Negotiate capabilities:
  - Which `ReplicationClass` each side allows
  - Quotas and limits

**Before replicating any object, check:**
- ✅ Object type → in allowed whitelist
- ✅ Size → under `SmallBlob` limit
- ✅ MCP → object not disallowed or quarantined by MCP

---

### 4.3 Quotas & Abuse Protection

**ReplicatorService MUST enforce:**

**Per-pod and per-object quotas:**
- ✅ Max number of replicated objects
- ✅ Max total bytes for `SmallBlob`

**Rate limits:**
- ✅ On replication operations per peer

**Misbehaving peers:**

**If a peer:**
- Attempts to push disallowed or oversized data
- Repeatedly fails integrity checks

**Then:**
- ✅ Lower HealthScore
- ✅ Optionally block them from replication activities entirely

---

## 5. MCP & Moderation Integration

**All federation-related content (AP activities, gossip, replicated objects) MUST:**

**Be eligible for MCP checks where applicable:**
- ✅ Text/body content → moderated for abuse/illegal content
- ✅ Hash lists → cross-checked for conflicts with local policy

**MCP MUST:**
- ✅ Have the final say on local treatment of content
- ✅ Be able to:
  - Quarantine
  - Block
  - Downrank

**Federation MUST NOT:**
- ❌ Bypass MCP
- ❌ Auto-apply external moderation decisions as root without local policy

---

## 6. Logging, Monitoring, and Diagnostics

**Federation modules MUST log:**
- ✅ Suspicious inbound requests:
  - Invalid signatures
  - Malformed payloads
  - Rate-limit hits
- ✅ Misconfigurations:
  - Realm mismatch
  - Unknown governance roots
- ✅ Rejected replication/gossip attempts

**Logs MUST:**
- ❌ Exclude secrets, tokens, and private content
- ✅ Use redactable identifiers where possible

**Monitoring:**

**Add metrics for:**
- ✅ AP request rates and error codes
- ✅ Gossip feed usage
- ✅ Replication attempts and failures
- ✅ Bridge flow usage per `allowed_flow`

**These metrics are used to:**
- ✅ Detect ongoing abuse
- ✅ Tune limits and policies over time

---

## 7. Defaults & Presets

**All example configs and presets (including the First Pod preset) MUST:**

**Start with conservative defaults:**
- ✅ Federation mode ≤ `Hermit`
- ✅ Gossip/replication disabled or minimal
- ✅ Bridge disabled

**Make enabling wider federation:**
- ✅ An explicit admin choice
- ✅ Clearly documented as increasing attack surface

**This ensures:**
- ✅ Out-of-the-box deployments do not accidentally expose themselves
- ✅ Operators understand when and how they are opting into federation behavior

---

## 8. Attack Surface Summary

| Feature | Default State | Attack Vectors Mitigated | Config Required to Enable |
|---------|--------------|-------------------------|--------------------------|
| **ActivityPub Federation** | `Off` or `Hermit` | XSS, signature forgery, fanout abuse, privacy leaks | Explicit mode change to `Federated` |
| **Cross-Realm Bridge** | `disabled` | Governance takeover, unauthorized replication, MCP bypass | `bridge.enabled = true` + explicit flows |
| **Gossip Feeds** | `Off` or minimal | PII leaks, log scraping, HealthScore manipulation | Explicit publishing/subscription config |
| **Replication (Small Objects)** | Whitelist-only | Arbitrary file access, quota exhaustion, MCP bypass | Per-object-type allowlist + MCP integration |
| **Dangerous Bridge Flows** | Always denied | Realm merge, governance takeover, MCP control | Requires code change (not config) |

---

## Related Documents

- `docs/social-federation-design.md` - ActivityPub integration design
- `docs/realm-design.md` - Realm isolation and bridging
- `docs/gossip-signals-design.md` - Gossip feeds design
- `docs/replication-policy-design.md` - Replication policy and security
- `docs/moderation-v1-design.md` - MCP integration requirements
- `docs/security-hardening-guidelines.md` - Global security principles
- `docs/archive/status/TASK_STATUS_DASHBOARD.md` - T-FED-SEC-01 through T-FED-SEC-05 tasks

---

## Implementation Checklist

**Before federation features go live:**

- [x] All inbound AP requests authenticated and validated
- [x] All outbound AP requests respect blocklists and quotas
- [x] Federation mode defaults to `Hermit` or `Off`
- [x] Realm trust model enforces governance root validation
- [x] Bridge disabled by default, dangerous flows always denied
- [x] Gossip feeds strip PII, enforce size limits, treat as untrusted hints
- [x] Replication strictly whitelisted, quotas enforced, MCP-gated
- [x] All federation content eligible for MCP checks
- [x] Comprehensive logging without secrets/PII
- [x] Metrics for abuse detection in place
- [x] Default configs conservative, enabling federation requires explicit action

**Security Review Required**: Before merging any federation implementation to production branch.
