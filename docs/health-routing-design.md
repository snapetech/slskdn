# Health & Routing Design (Resilience Layer)

**Status**: DRAFT - Future Resilience Layer  
**Created**: December 11, 2025  
**Priority**: 🟡 MEDIUM (after core architecture)

> **Project Note**: This is a fork of [slskd](https://github.com/slskd/slskd). See [../README.md](../README.md#acknowledgments) for attribution.

---

This document defines how pods measure **health** of peers and backends, and how they use those measurements to make **routing decisions** that adapt to failures and abuse.

This is a core part of the resilience layer: it enables the network to route around damage without central control, while preserving security and privacy.

---

## 1. Goals

**Provide a clear, local health model for:**
- Peers (pods, Soulseek peers, mesh nodes, relays)
- Backends (HTTP APIs, metadata services)
- Routes/paths (relay chains, DHT paths)

**Use health to:**
- Prefer reliable paths and peers
- Gracefully degrade and reroute on failure

**Keep everything:**
- **Local-first** (no forced global consensus)
- **Privacy-preserving** (no unnecessary leakage of metrics)
- **Resistant to manipulation** (abusive peers can't trivially poison scores)

---

## 2. Health Entities & Metrics

### 2.1 Entities

We track health for:

**PeerHealth:**
- Pods in the mesh
- Soulseek peers (where visible)
- Torrent peers (for our own purposes)
- Relays/gateways used as intermediaries

**BackendHealth:**
- HTTP metadata providers
- Search/index services (if any external)
- Governance/gossip feeds

**RouteHealth:**
- Specific relay chains (e.g., pod → relayA → relayB → peer)

---

### 2.2 Metrics

For each entity we track:

**Success/failure counts** (with decay over time)

**Latency:**
- Average
- P95

**Error types:**
- Timeouts
- Connection refused
- Protocol errors

**Abuse / policy infractions:**
- Suspicious responses (MCP flags)
- Excessive rate limit hits
- Content-policy violations

**All metrics:**
- Are maintained locally
- Use **time decay** so old data does not dominate forever

---

## 3. HealthScore Model

We define a **HealthScore** as a bounded value: **0–100**

**Score Ranges:**
```
  0–20: Bad / effectively unusable
 20–60: Degraded, last resort
 60–85: Normal
85–100: Excellent
```

**Inputs:**
- Success rate vs attempts (recent, decayed)
- Latency
- Error severity (timeouts worse than 404-like; abuse flags worse than errors)
- Manual admin overrides (e.g., local deny/allow lists)

**MCP integration:**

MCP decisions can contribute to:
- Penalizing peers that repeatedly serve abusive content
- Soft-blocking or hard-blocking peers depending on severity

**Scores are recalculated periodically** (or on major events) and cached.

---

## 4. Routing Based on Health

### 4.1 Peer/Route Selection

When choosing a source/route for an operation (e.g., fetch work, connect to peer):

**1. Gather candidate peers/routes**
- Allowed by policy (domain-aware, MCP-approved)

**2. Sort by HealthScore descending**

**3. Attempt connections in order:**
- Use backoff and exponential retry for failing candidates
- After repeated failures, lower their score

**Minimum thresholds:**

If `HealthScore < threshold` (e.g., 30), treat the peer/route as:
- "Avoid unless absolutely necessary"

If no candidate is above threshold:
- Either:
  - Fail gracefully with "no healthy path"
  - Or allow "desperation mode" that tries low-score paths with extra caution

---

### 4.2 Backend Selection

Similar for backends:

**For metadata/HTTP:**
- Prefer backends with high HealthScore
- Use fallback backends on failure

**Throttle or temporarily disable backends that:**
- Return malformed data
- Trip MCP abuse checks

---

### 4.3 Local-Only by Default

**Health-based routing changes ONLY local choices:**
- There is no global "ban this peer for everyone" at this layer

**Higher-level governance feeds can share advisory info**, but each pod decides locally.

---

## 5. Security & Privacy Considerations

**All health metrics:**
- Stored locally
- Not exposed via public APIs

**Any aggregated health info that is optionally shared** (see gossip) MUST:
- Be stripped of PII, IPs, and detailed error context
- Be coarse-grained and anonymized where possible

**Defense against manipulation:**

- Do not trust health reports from remote peers blindly
- Optionally consume governance/gossip feeds as extra hints, but always:
  - Cross-check against local observations
  - Apply caps on how much external signals can shift a local HealthScore

---

## 6. Implementation & Extension

**`HealthManager`:**

Central component that:
- Records events
- Maintains scores
- Exposes read-only interfaces for routing components

**Routing components:**

Planner, transport layers, and backends:
- Ask `HealthManager` for ranked candidates
- Report results back for continuous learning

**Future extension:**
- Aggregate health histograms for optional gossip feeds
- Per-domain health (e.g., music vs video vs book sources)

---

## Related Documents

- `docs/replication-policy-design.md` - Replication & redundancy policy
- `docs/gossip-signals-design.md` - Optional gossip feeds for health/abuse signals
- `docs/security-hardening-guidelines.md` - Global security principles
- `docs/archive/status/TASK_STATUS_DASHBOARD.md` - T-RES-01 through T-RES-05 tasks

