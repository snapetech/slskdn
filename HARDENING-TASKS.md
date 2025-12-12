# Security Hardening Task Backlog

**Status**: 📋 Ready for Implementation  
**Priority**: HIGH - Address before production use  
**Created**: December 11, 2025

---

## Overview

This document tracks security hardening tasks for `slskdn`. These tasks focus on **preventing abuse, protecting privacy, and limiting blast radius** when things go wrong.

**Key Principles:**
- Defense in depth
- Fail secure by default
- Explicit over implicit
- Privacy-aware
- Soulseek etiquette compliance

---

## Task Prioritization (By Risk)

### 🔥 CRITICAL (Before ANY Public Deployment)
- **H-01**: HTTP Gateway Auth, CSRF, Misconfig Guards
- **H-02**: Per-Call Work Budget & Fan-Out Limits
- **H-08**: Soulseek-Specific Safety Caps

### ⚠️ HIGH (Before Multi-User / Untrusted Access)
- **H-03**: DHT & Index Privacy / Exposure Controls
- **H-04**: Identity & Correlation Hardening
- **H-05**: HTTP Envelope & SSRF Prevention

### 📊 MEDIUM (Defense in Depth)
- **H-06**: CSRF, Browser & Clickjacking Protections
- **H-07**: Pod/Chat Privacy & Retention Controls
- **H-10**: Abuse-Resistant Reputation & Trust Model

### 🔧 LOW (Operational Security)
- **H-09**: Local Key & Credential Protection

---

## H-01 – Lock Down HTTP Gateway (Auth, CSRF, Misconfig Guards)

**Risk Focus**: Remote abuse of the node via the gateway (local or exposed).  
**Goal**: Make it very hard to accidentally expose a wide-open remote-control API.

### Key Actions

#### 1. Authentication Layer (Mandatory When Not Localhost)

**Requirements:**
- Introduce explicit `MeshGateway.ApiKey` (or token) config
- Enforce: If bound to anything other than loopback, auth **must** be configured, or gateway refuses to start
- Check API key via header `X-Slskdn-ApiKey` (NEVER via query string)
- Provide CLI to generate/store key
- Log big warning if gateway enabled without auth

**Implementation Notes:**
- Store API key in secure config section
- Use constant-time comparison for key validation
- Support key rotation without restart (optional)

#### 2. CSRF Protection When on Localhost

**Requirements:**
- Require custom header `X-Slskdn-Csrf: <random>` stored in config
- Validate `Origin`/`Referer` where present
- Document: "Calling from a browser requires adding this header"

**Implementation Notes:**
- Generate CSRF token on startup, store in config
- Reject requests without valid CSRF header
- Provide example `curl` commands in docs

#### 3. Misconfiguration Warnings

**Requirements:**
- On startup:
  - If `Gateway.Enabled = true` AND `BindAddress != 127.0.0.1`:
    - Log at ERROR with explicit warning
    - Require `IUnderstandTheRisk=true` flag to proceed
  - If `Gateway.Enabled = true` AND `ApiKey` missing:
    - Fail fast or refuse to bind non-localhost

**Implementation Notes:**
- Check during `MeshServicePublisher` startup
- Provide clear error messages with remediation steps

#### 4. Route Allowlist

**Requirements:**
- Enforce per-route restrictions: Only `/mesh/http/*` and `/mesh/ws/*`
- Add central allowlist mapping: `serviceName → allowed HTTP methods / paths`
- Reject arbitrary verbs/paths before calling services

**Implementation Notes:**
- Define allowlist in `MeshGatewayOptions`
- Validate before `IMeshServiceClient.CallAsync`

---

## H-02 – Per-Call "Work Budget" and Downstream Fan-Out Limits

**Risk Focus**: Small service calls triggering large Soulseek/BT/mesh work.  
**Goal**: Bound the amount of downstream work caused by any single incoming call/peer.

### Key Actions

#### 1. Define "Work Unit" Abstraction

**Work Unit Definition:**
- 1 unit = 1 Soulseek search OR 1 torrent metadata fetch OR X mesh RPC calls

**Implementation:**
- Create `WorkBudget` utility class
- Define unit costs per operation type

#### 2. Attach `WorkContext` to `MeshServiceContext`

**Requirements:**
- Include:
  - `RemainingWorkBudget` per call
  - Peer identity
- Services decrement budget when triggering:
  - Soulseek operation
  - BT operation
  - Outbound mesh calls

**Implementation Notes:**
- Add `WorkContext` property to `MeshServiceContext`
- Provide `TryDeductWork(int units, string reason)` method

#### 3. Per-Peer and Per-Call Caps

**Configuration:**
```json
{
  "WorkBudget": {
    "MaxWorkUnitsPerCall": 10,
    "MaxWorkUnitsPerPeerPerMinute": 50
  }
}
```

**Behavior:**
- If call exceeds budget: Return `StatusCode` 429 (rate limited)
- Track per-peer budget in sliding window

#### 4. Wire Soulseek/BT Usage

**Requirements:**
- Wrap Soulseek search/browse calls
- Wrap BT metadata/query calls
- Check & decrement `WorkContext` before execution

**Implementation Notes:**
- Create interceptor/middleware pattern
- Ensure no service can bypass cost accounting

---

## H-03 – Strengthen DHT & Index Privacy / Exposure Controls

**Risk Focus**: DHT/VirtualSoulfind becoming a global "who has what" index.  
**Goal**: Reduce how much of a node's library and behavior gets globally exposed.

### Key Actions

#### 1. Advertised Content Policy

**Configuration:**
```json
{
  "VirtualSoulfind": {
    "AdvertiseOnlySharedContent": true,
    "AdvertiseSubsetByTag": null,
    "MaxAdvertisedContentDescriptors": 1000
  }
}
```

**Requirements:**
- Default: Only advertise content that's already explicitly shared
- NEVER default to "everything you have on disk"

#### 2. Limit Per-Node Visibility

**Requirements:**
- Cap number of distinct content descriptors a node will publish:
  - Per time window
  - Per content category (e.g., per-artist/MBID)
- Randomize subset if too many candidates

#### 3. Private Content Flag

**Requirements:**
- Allow marking content as `Private`:
  - Never publish to DHT
  - Only available via direct Soulseek or local/explicit actions

**Implementation:**
- Add `IsPrivate` flag to content descriptors
- Filter before DHT publication

#### 4. VirtualSoulfind Ingestion Guardrails

**Requirements:**
- Track ratio: "Content discovered via Soulseek" vs "local/mesh sources"
- Consider mode: "Do not re-advertise content that was only seen in Soulseek"

#### 5. DHT Poisoning Detection Heuristics

**Requirements:**
- For a given content key:
  - If N descriptors from peers with similar endpoints/behavior, downgrade trust
- Optionally:
  - Cross-check subset by attempting connections
  - Mark unresponsive nodes as low reputation

---

## H-04 – Identity & Correlation Hardening (Keys, IPs, Metrics)

**Risk Focus**: Linking Soulseek, mesh, BT, and metrics to a single persona.  
**Goal**: Give users tools to compartmentalize and minimize correlation.

### Key Actions

#### 1. Key Rotation Support

**Requirements:**
- Provide CLI/UI action: "rotate mesh identity"
  - Generate new Ed25519 key pair
  - Keep old key for grace period for existing sessions
- Document pros/cons of rotation vs stability

#### 2. Per-Channel Bind Advice / Config

**Configuration:**
```json
{
  "Mesh": { "BindAddress": "0.0.0.0" },
  "Soulseek": { "BindAddress": "0.0.0.0" },
  "Torrent": { "BindAddress": "0.0.0.0" }
}
```

**Goal**: Allow splitting across different interfaces/IPs (e.g., VPN vs non-VPN)

#### 3. Metrics Anonymization Toggles

**Configuration:**
```json
{
  "Metrics": {
    "Enabled": true,
    "RedactNodeIdentity": true,
    "TruncatePeerIds": true
  }
}
```

**Requirements:**
- Default Prometheus labels must NOT contain:
  - Hostnames that identify the user
  - Full node IDs (use truncated hashes only)

#### 4. Explicit Privacy Docs

**Document Topics:**
- Threat model: Multiple networks = more correlation risk
- Mitigation strategies:
  - VPN separation
  - Key rotation
  - Disabling metrics
  - Separate bind addresses

---

## H-05 – Harden HTTP Envelope & Prevent SSRF-Like Flows

**Risk Focus**: Generic HTTP→Service payload used to drive external requests unsafely.  
**Goal**: Make it harder to use the gateway as an SSRF launcher or injection surface.

### Key Actions

#### 1. Strict Schema for HTTP Envelope

**Requirements:**
- Define fixed set of fields with strict type validation
- Reject:
  - Unexpected fields
  - Oversized headers or path strings

#### 2. Outbound Request Guard

**Requirements:**
- If any `IMeshService` performs HTTP requests to arbitrary URLs:
  - Introduce central outbound HTTP client wrapper
  - Enforce allowlists/denylists for hostnames/IP ranges
  - Block internal address spaces (127.0.0.0/8, 10.0.0.0/8) by default
- NO direct `HttpClient` usage with user-supplied URLs

#### 3. Sanitize Headers

**Requirements:**
- If any service forwards HTTP headers:
  - Strip "dangerous" headers (e.g., `Host`, `Authorization`) unless explicitly allowed

#### 4. Method Allowlist Per Service

**Configuration:**
```json
{
  "MeshGateway": {
    "ServiceMethodAllowlist": {
      "pods": ["GET", "POST"],
      "shadow-index": ["GET"],
      "mesh-introspect": ["GET"]
    }
  }
}
```

---

## H-06 – CSRF, Browser & Clickjacking Protections for Gateway

**Risk Focus**: Websites tricking browsers into hitting localhost.  
**Goal**: Limit browser abuse of the local gateway.

### Key Actions

1. **Same-Origin / Custom Header Checks** (see H-01)
2. **CORS Defaults**: No CORS for gateway endpoints by default
3. **Clickjacking Protections**: Add `X-Frame-Options: DENY` and CSP headers

---

## H-07 – Pod/Chat Privacy & Retention Controls

**Risk Focus**: Accidental long-term logging of semi-private chatter.  
**Goal**: Give users control over what is stored and for how long.

### Key Actions

#### 1. Retention Policy

**Configuration:**
```json
{
  "Pods": {
    "MessageRetentionDays": 7,
    "MaxStoredMessagesPerPod": 1000
  }
}
```

**Requirements:**
- `MessageRetentionDays = 0` means no persistence
- Implement periodic cleanup
- Option for ephemeral pods (memory only)

#### 2. Disable Pods Entirely

**Configuration:**
```json
{
  "Pods": {
    "Enabled": false
  }
}
```

#### 3. Prevent "Room Mirroring as Default"

**Requirements:**
- Clarify in docs: Bridging Soulseek rooms→pods is NOT built-in
- If ever implemented: Opt-in with loud warnings

---

## H-08 – Soulseek-Specific Safety Caps

**Risk Focus**: Mesh/turbo features driving up Soulseek load indirectly.  
**Goal**: Clamp Soulseek-specific activity to "heavy but sane".

### Key Actions

#### 1. Global Soulseek Activity Governor

**Configuration:**
```json
{
  "Soulseek": {
    "MaxSearchesPerMinute": 10,
    "MaxBrowsesPerMinute": 5,
    "MaxDownloadSlotsUsed": 50
  }
}
```

**Requirements:**
- All Soulseek search/browse calls pass through central rate limiter
- When exceeded: Queue or drop with clear logs

#### 2. Separate Accounting for Mesh-Induced Activity

**Metrics:**
- `soulseek_searches_total{source="user"}` 
- `soulseek_searches_total{source="mesh"}`

**Requirements:**
- If mesh-induced > threshold: Log warnings, maybe auto-disable features

#### 3. Conservative Default Tuning

**Requirements:**
- Ship defaults clearly under "suspicious bot" levels
- Document how to increase with "don't be an asshole" note

---

## H-09 – Local Key & Credential Protection

**Risk Focus**: Local compromise → identity hijack.  
**Goal**: Make it harder to exfiltrate or misuse keys/creds.

### Key Actions

1. **Dedicated Runtime User**: Run under low-privilege account (e.g., `systemd User=slskdn`)
2. **Key/Credential Storage**: Single file/DB with restrictive permissions (600)
3. **Backup & Export Guidance**: Provide CLI, document identity cloning risk

---

## H-10 – Abuse-Resistant Reputation & Trust Model

**Risk Focus**: Sybil/poisoning and abuse at scale.  
**Goal**: Make it harder for single adversary to dominate the fabric.

### Key Actions

#### 1. Weighting Beyond "Valid Signature"

**Factors:**
- Uptime (observed)
- Response quality
- Historical reliability

**Use When:**
- Choosing service providers
- Choosing alt sources

#### 2. Rate-Limit Peers at DHT/Service Level

**Requirements:**
- Limit descriptors per peer per unit time
- Cap services a single peer can advertise

#### 3. Ban/Quarantine List Persistence

**Requirements:**
- Persist bans/quarantines across restarts
- Support exportable/importable ban lists

---

## Implementation Strategy

### Phase 1: CRITICAL (Before T-SF04 HTTP Gateway)
1. H-01 (Gateway Auth/CSRF) - **MUST DO BEFORE T-SF04**
2. H-08 (Soulseek Caps) - **MUST DO BEFORE ANY DEPLOYMENT**

### Phase 2: HIGH (Before Multi-User)
3. H-02 (Work Budget)
4. H-03 (DHT Privacy)
5. H-05 (SSRF Prevention)

### Phase 3: MEDIUM (Defense in Depth)
6. H-04 (Identity Hardening)
7. H-06 (Browser Protections)
8. H-07 (Pod Privacy)

### Phase 4: LOW (Ongoing)
9. H-09 (Key Protection)
10. H-10 (Reputation Model)

---

## Next Steps

1. ✅ Review and integrate into project planning
2. ⏳ Create detailed implementation briefs for H-01, H-02, H-08 (like T-SF0x format)
3. ⏳ Implement H-01 before starting T-SF04
4. ⏳ Implement H-08 in parallel with service fabric work
5. ⏳ Track progress in `SERVICE_FABRIC_TASKS.md`

---

## Notes

- These tasks are **non-negotiable** for production use
- Implement H-01 and H-08 **BEFORE** any public deployment
- Each task should have its own implementation brief (H-XX-IMPLEMENTATION-BRIEF.md)
- Test coverage required for all hardening measures
- Document security decisions and trade-offs
