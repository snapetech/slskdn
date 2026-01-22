# Gossip & Signals Design (Optional Resilience Feeds)

**Status**: DRAFT - Future Resilience Layer  
**Created**: December 11, 2025  
**Priority**: 🟢 LOW (optional enhancement)

> **Project Note**: This is a fork of [slskd](https://github.com/slskd/slskd). See [../README.md](../README.md#acknowledgments) for attribution.

---

This document defines **optional, small, privacy-preserving gossip/signal feeds** that pods can publish and consume to improve routing, moderation, and healing without centralization.

**All feeds are:**
- **Optional** – pods can choose to publish, subscribe, both, or neither
- **Advisory** – pods treat them as hints, not commands
- **Heavily constrained** – no PII, minimal data, small payloads

---

## 1. Types of Feeds

We define three primary feed types:

1. **HealthFeed** – aggregated health info about peers/backends
2. **AbuseFeed** – high-level abuse/misbehavior reports
3. **ReplicationNeedFeed** (future) – hints about objects that could benefit from extra replicas

---

## 2. HealthFeed

### Purpose

Share coarse, aggregated health info so pods can:
- Preemptively downrank obviously bad peers/routes
- Discover reliable paths faster

### Content

**Aggregated, anonymized metrics:**
- Peer categories (not raw IDs/IPs)
- Score buckets (e.g., "many pods report this backend as unstable")
- Time windows (last N hours/days)

### Constraints

- ❌ No IP addresses, usernames, or exact identifiers
- ❌ No raw error logs
- ✅ Limited resolution to prevent deanonymization

### Consumption

Pods treat HealthFeed signals as:
- Weak priors in their own HealthScore calculation
- Never stronger than local observations

### Security

- Feeds are signed by pod identities or governance actors
- Pods can:
  - Choose trust levels per feed source
  - Ignore feeds entirely if policy demands

---

## 3. AbuseFeed

### Purpose

Share very **coarse signals** about abusive content or behavior, to supplement local MCP decisions.

### Content

**Hashes or identifiers of:**
- Known disallowed content (aligned with MCP policies)
- Highly abusive peers (if policy allows, and without outing innocents)

**Severity bucket:**
- `critical`, `suspicious`, `spammy`

### Constraints

- ❌ No raw content
- ❌ No user PII
- ❌ Feeds must not expose:
  - Private user activity
  - Detailed internal moderation decisions

### Consumption

**Pods:**
- Ingest AbuseFeed as an additional `ModerationProvider` input, if enabled
- Map severity into local `ModerationVerdict` according to local policy

**Local admins can:**
- Trust certain feeds (e.g., from governance actors or known pods)
- Ignore others

### Security

- Feed sources are authenticated and signed
- **MCP on each pod has final say:**
  - Local overrides always allowed
  - Feeds cannot directly modify blocklists without admin or explicit policy

---

## 4. ReplicationNeedFeed (Future)

### Purpose

Provide **hints** about objects that would improve resilience if replicated.

### Examples

- "This governance doc is central; replication recommended"
- "This moderation hash list is widely used; extra redundancy helpful"

### Content

**Object IDs for replication-eligible objects** (per `ReplicationClass` policy)

**Context flags:**
- Suggested minimum replica count
- Age and update frequency

### Constraints

- Only for objects already allowed for replication
- No user-unique/private objects

### Consumption

**Pods:**
- Can subscribe to ReplicationNeedFeed as hints for their `ReplicatorService`
- Still enforce local:
  - Quotas
  - Policies
  - MCP checks

---

## 5. Transport & Publishing

Gossip feeds can be published via:

- **HTTPS endpoints** (pull)
- **ActivityPub actors** (push/pull via AP objects)
- Other simple pub/sub mechanisms

### Requirements

- Signed payloads (pod identity or governance identity)
- Versioned schemas
- Small, bounded payload size

---

## 6. Privacy & Hardening

**No feed may contain:**
- ❌ IPs, email addresses, usernames, or personally identifiable information
- ❌ Raw content or deep error logs

**Pods MUST:**
- Treat feeds as **untrusted input**
- Validate signatures, schema, and size
- Apply rate-limiting and abuse protection for feed endpoints

**Feeds MUST NOT:**
- Be a hidden backdoor for centralized control
- Be mandatory for protocol correctness

**All self-healing behavior remains:**
- **Local-first**, with gossip as enhance-but-not-control signals

---

## 7. Example Feed Structure

### HealthFeed Example

```json
{
  "feed_type": "health",
  "version": 1,
  "publisher": "gov:<hash>",
  "timestamp": "2025-12-11T12:00:00Z",
  "entries": [
    {
      "category": "metadata_backend",
      "identifier_hash": "<hash>",
      "score_bucket": "degraded",
      "observation_count": 42,
      "time_window_hours": 24
    }
  ],
  "signature": "<sig>"
}
```

### AbuseFeed Example

```json
{
  "feed_type": "abuse",
  "version": 1,
  "publisher": "gov:<hash>",
  "timestamp": "2025-12-11T12:00:00Z",
  "entries": [
    {
      "content_hash": "<sha256>",
      "severity": "critical",
      "reason_code": "known_disallowed",
      "first_seen": "2025-12-10T08:00:00Z"
    }
  ],
  "signature": "<sig>"
}
```

### ReplicationNeedFeed Example (Future)

```json
{
  "feed_type": "replication_need",
  "version": 1,
  "publisher": "gov:<hash>",
  "timestamp": "2025-12-11T12:00:00Z",
  "entries": [
    {
      "object_id": "<uuid>",
      "object_type": "governance_registry",
      "replication_class": "SmallBlob",
      "suggested_replicas": 5,
      "priority": "high"
    }
  ],
  "signature": "<sig>"
}
```

---

## Related Documents

- `docs/health-routing-design.md` - Health scoring (HealthFeed consumer)
- `docs/replication-policy-design.md` - Replication policy (ReplicationNeedFeed consumer)
- `docs/moderation-v1-design.md` - MCP design (AbuseFeed consumer)
- `docs/f1000-governance-design.md` - Governance feeds
- `docs/security-hardening-guidelines.md` - Global security principles
- `docs/archive/status/TASK_STATUS_DASHBOARD.md` - T-RES-04, T-RES-05 tasks

