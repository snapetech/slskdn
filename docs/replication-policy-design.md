# Replication & Redundancy Policy Design

**Status**: DRAFT - Future Resilience Layer  
**Created**: December 11, 2025  
**Priority**: 🟡 MEDIUM (after core architecture)

> **Project Note**: This is a fork of [slskd](https://github.com/slskd/slskd). See [../README.md](../README.md#acknowledgments) for attribution.

---

This document defines how we handle **automatic redundancy** and **replication** in the network.

**Guiding philosophy: Principle of Least Replication**

- **Default: no automatic replication**
- Only small, high-value data is replicated initially
- Larger or more intrusive replication is always:
  - Opt-in
  - Quota-limited
  - Tied to clear use-cases (healing, governance, essential metadata)

---

## 1. Replication Classes

We define a `ReplicationClass` for content and metadata:

### `None` (Default)
- **Default for everything**
- No automatic replication beyond the original pod(s)

### `MetadataOnly`
- Replicate only small descriptors:
  - WorkRefs, tags, list definitions, indexes
- Used for:
  - Resilient discovery
  - Governance / moderation feeds

### `SmallBlob`
- Replicate small, bounded-size payloads:
  - Governance docs (F1000 registry, policy profiles)
  - Moderation lists (hash lists)
  - Small playlists/curated lists
- **Size limits MUST be strict** (configurable, conservative defaults)

### `FullCopy` (future, highly restricted)
- Replicate full content objects, usually only:
  - Small media files
  - Explicitly marked critical items
- Strong opt-in and quota mechanisms

### `Chunked` (future, experimental)
- Content is split into chunks/shards
- Individual shards are replicated, not full files
- Requires more complex integrity and privacy handling

**Initial implementation scope:**
- Start with `None`, `MetadataOnly`, and `SmallBlob`
- `FullCopy` and `Chunked` remain future experiments with explicit feature flags

---

## 2. Policy Sources

Replication policy decisions come from:

### Global config

For each domain (Music, Book, Video, Generic, Governance, Moderation, Social):
- Default replication class (initially `None`)

Maximum quotas per pod:
- Max number of replicated objects
- Max total bytes for `SmallBlob`

### Per-object policy

Certain objects can be marked as:
- `ReplicationPriority: Low | Normal | High`

Examples:
- F1000 registry → `MetadataOnly`, High
- Core governance docs → `SmallBlob`, High
- Moderation hash lists → `SmallBlob`, Normal/High

### Manual admin opt-in

Pod admins can explicitly mark:
- Specific lists or small objects as "replicated"
- Specific categories as "never replicate"

**Everything defaults to:**
- `ReplicationClass = None` unless explicitly configured otherwise

---

## 3. Replicator Service

We introduce a `ReplicatorService` with strict constraints:

### Responsibilities

- Maintain replication targets for eligible objects
- Establish secure replication relationships with other pods
- Remove stale or excess replicas over time

### Never allowed to

- Request arbitrary files by path
- Replicate large media payloads unless explicitly configured (and feature-flagged)
- Leak identity or library state beyond what is needed for replication

---

### 3.1 Replication Handshake

When replicating an eligible object to another pod:

**Pod A (source) and Pod B (replica) perform a handshake:**

**Mutual auth:**
- Each side proves pod identity (as per pod identity design)

**Capability negotiation:**
- What classes of replication B accepts (e.g., only `MetadataOnly`, `SmallBlob`)
- Quotas and per-pod limits

**Policy checks:**
- Ensure policies on both sides allow replication of this object

**All replication traffic:**
- Encrypted in transit
- Signed or MAC'd to ensure integrity

---

### 3.2 Object Lifecycle

For each replicated object:

**`ReplicationMetadata` tracks:**
- Object ID
- Replication class
- Source pod(s)
- Replica pods
- Last updated timestamp

**ReplicatorService:**
- Periodically checks health of replicas
- Recreates missing replicas up to policy limits
- Aged-out or revoked data is:
  - Garbage-collected from replicas within a reasonable timeframe

---

## 4. Initial Use-Cases (Small & High-Value)

Initial replication focus is on small, high-value items:

### 1. Governance & F1000 Registry
- `MetadataOnly` and `SmallBlob`
- Ensures governance state is not a single central point of failure
- Replicated across a small set of trusted pods (opt-in)

### 2. Moderation Lists
- Hash-based disallow lists or advisory lists
- `SmallBlob`, under strict size and update limits

### 3. Social & Discovery Metadata
- Lists of lists, small tags, curated "essential" collections
- `MetadataOnly`, so discovery still works even if the origin pod is temporarily offline

**Large user libraries and full media objects:**
- Remain non-replicated by default
- Future experimental features can introduce carefully constrained `FullCopy` or `Chunked` replication

---

## 5. Security & Abuse Prevention

### Threats

- Being used as a generic storage network for arbitrary data
- Replication flood attacks (DoS)
- Replication of disallowed content

### Mitigations

**Strict whitelisting:**
- Only specific object types and classes are eligible
- No arbitrary file path replication

**Quotas & rate limits:**
- Per-pod and per-object quotas
- Rate-limited replication operations

**MCP integration:**
- All objects considered for replication are:
  - Checked by MCP first
  - Blocked/quarantined if disallowed

**Configurable opt-out:**
- Any pod can:
  - Refuse to act as a replica for any data
  - Restrict to certain classes (e.g., only governance metadata)

**Privacy:**
- No replication of user-specific, private data without explicit consent
- Governance and moderation data are already intended to be public/advisory

---

## 6. Future: Chunked Replication (Design Hook)

We leave hooks for future `Chunked` replication:

**Splitting an object into chunks/shards:**

**Each shard:**
- Encrypted
- Stored on a subset of pods

**Reconstruction requires:**
- Enough shards + appropriate keys

**This is explicitly future work**, behind feature flags and separate design.

---

## Related Documents

- `docs/health-routing-design.md` - Health scoring and routing
- `docs/gossip-signals-design.md` - Optional gossip feeds
- `docs/security-hardening-guidelines.md` - Global security principles
- `docs/f1000-governance-design.md` - Governance replication use-case
- `docs/moderation-v1-design.md` - Moderation list replication use-case
- `TASK_STATUS_DASHBOARD.md` - T-RES-03 task

