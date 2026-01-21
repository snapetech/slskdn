# Slack-Grade Messaging Design

**Status**: DESIGN - Future Messaging Layer  
**Created**: December 12, 2025  
**Priority**: 🟡 MEDIUM (after core pod/social infrastructure)

> **Project Note**: This is a fork of [slskd](https://github.com/slskd/slskd). See [../README.md](../README.md#acknowledgments) for attribution.

---

This document defines the additional layers needed to make the system behave like a **Slack-class messaging platform** on top of the existing architecture (pods, realms, ChatModule, ForumModule, SocialFeed, MCP, realms, replication, etc.).

**It focuses on:**
- Real-time messaging semantics (ordering, delivery, presence)
- UX-level expectations (clients, notifications)
- Search & retention
- Org/workspace semantics
- Attachments (bounded "Slack Drive" behavior)

**The goal is to stay aligned with existing principles:**
- ✅ Pods are sovereign
- ✅ Realms isolate universes by default
- ✅ Federation, replication, and gossip are opt-in and hardened
- ✅ **Principle of Least Replication**: minimal by default, explicit expansion

---

## 1. Real-Time Messaging Semantics

### 1.1 Channels and Timelines

We treat each `ChatModule` channel as a **log**:

**Messages have:**
- `message_id` (monotonic per channel, locally)
- `timestamp`
- `author_id`, `channel_id`
- Optional `thread_id` (for threaded replies)

**Within a single pod:**
- Messages are written to an append-only log (physically or logically)
- Clients receive:
  - A consistent, monotonic ordering per channel from that pod's perspective

**Cross-pod / federated channels:**
- Cross-pod channels (if implemented) are logically:
  - A **merged view** from multiple pod-local logs
  - Ordering strategy:
    - Primary: `timestamp` with a pod-specific tie-breaker (e.g., `(timestamp, pod_id, message_id)`)
    - Provide best-effort monotonicity; exact global ordering is not guaranteed across pods

---

### 1.2 Delivery Semantics

We define delivery semantics per channel:

**Within a pod:**
- ✅ `At-least-once` delivery to connected clients:
  - Messages are persisted before broadcast
  - Clients acknowledge receipt via cursors/last-read markers
- ✅ Client reconnect:
  - Server can replay from last known cursor for the client

**Across pods (if federated channels exist):**
- Each pod:
  - Delivers messages to other pods via a reliable internal queue with retries/backoff
  - Records whether remote delivery succeeded; failures are:
    - Retried
    - Eventually surfaced as "degraded federation" state

**We do NOT guarantee global exactly-once delivery across pods; instead:**
- ✅ We ensure:
  - Idempotent message processing via message IDs
  - De-duplication via `(pod_id, message_id)` or similar

---

### 1.3 Presence & Typing

Presence is implemented as a **soft, advisory layer**:

**Presence states:**
- `online`, `idle`, `offline`, `dnd` (do-not-disturb) etc.

**Per pod:**
- Presence is tracked locally per user session
- Presence is fuzzed/throttled (e.g., not more than N updates per interval per user)

**Cross-pod presence:**
- ✅ By default, presence is **local-only**
- ⚠️ For federated views (e.g., shared channels across pods in the same org):
  - Pods MAY exchange coarse-grained presence info:
    - "User X is active in org shared channels"
  - This is done with explicit opt-in configuration and no fine-grained timestamps

**Typing indicators:**
- **Local pod**:
  - Short-lived ephemeral messages:
    - "User X is typing in channel Y"
- **Cross-pod typing**:
  - Either disabled or heavily throttled due to cost and privacy:
    - If implemented, flows only within the same org or tightly controlled contexts

---

## 2. UX Expectations & Clients

### 2.1 Client Capabilities

Clients (web/desktop/mobile) are expected to support:

- ✅ Real-time channel view:
  - WebSocket or similar subscription to channel updates
- ✅ Unread counts:
  - Per channel and thread
- ✅ "Jump to last read":
  - Server tracks last read per channel per user

---

### 2.2 Protocol Expectations

**Server APIs MUST:**

**Provide:**
- ✅ Efficient channel history endpoints:
  - Pagination by message_id or timestamp
- ✅ Cursors for resuming streams (for reconnects)

**Be stable and versioned:**
- ✅ Backwards-compatible changes where possible
- ✅ Version negotiation for clients

**This doc does not prescribe a specific UI; it defines server semantics to enable Slack-class UX.**

---

## 3. Search & Retention

### 3.1 Per-Pod Search Index

Each pod maintains a **local search index** for:

- ✅ Chat messages (channels and threads)
- ✅ Forum posts
- ✅ Optionally:
  - SocialFeed objects originating from that pod

**Index contents:**
- Canonical text representation (sanitized)
- Metadata:
  - Channel/board, author, timestamps, tags, WorkRefs if applicable

**Privacy:**
- ✅ Only content the pod is legitimately hosting and allowed to index is included
- ❌ No cross-pod or cross-realm indexing by default

---

### 3.2 Retention Policies

Retention is configured per pod, with optional finer-grained per-channel/board overrides:

**Time-based:**
- "Keep messages for 90 days / 1 year / forever"

**Scope-based:**
- Longer retention for compliance channels (if configured)
- Shorter retention for ephemeral channels

**Configurable per domain:**
- Chat vs Forums vs SocialFeed

**Deletion:**
- Logical delete (no longer searchable/readable)
- Physical compaction as a background process

**Compliance hooks:**
- Optional flag to:
  - Prevent deletion for designated "compliance" channels until explicit admin action
- Potential integration with export tools (for archiving)

---

### 3.3 Federated / Cross-Pod Search (Optional, Future)

For pods in the same realm or org:

**Optional federated search is allowed as a future feature:**
- Pod A can query Pod B's search API for:
  - Public channels
  - Shared org channels
- Respecting:
  - ACLs
  - Realm/peering policies

**Federated search MUST:**
- ❌ Not expose:
  - Private channels
  - DMs
- ✅ Be strictly opt-in per pod and per realm

---

## 4. Org / Workspace Semantics

Pods map naturally to **workspaces**. For Slack-like behavior we define:

### 4.1 Org Concept (Optional Layer)

An **Org** can be introduced as a logical grouping of pods:

**Org has:**
- `org_id`
- Set of pods (pod IDs)
- Shared identity providers (SSO/SCIM), if configured

**Orgs are implemented as:**
- Metadata + configuration, NOT a new protocol primitive

---

### 4.2 Org-Shared Channels

**Org-shared channels:**
- Are channels that exist logically at the org level and are mirrored into multiple pods:
  - Pod A's `#general` (org-shared) is linked to Pod B's `#general` (org-shared)
  - Messages are bridged between them via:
    - Internal message queues
    - The same health-aware routing logic and optional replication

**Mechanics:**
- Each org-shared channel has a stable `org_channel_id`
- Each pod maps `org_channel_id` to a local channel instance
- Cross-pod sync:
  - Messages in org channels are queued to other pods in the org, subject to health and ACLs

**Org policies:**
- Pods in an org share:
  - Some global ACLs
  - Shared SSO/SCIM mappings, if configured

---

## 5. Attachments (Slack-Style "Files")

Attachments are scoped to a small, bounded "file service" per pod.

### 5.1 Attachment Types and Limits

**Attachment model:**
- ✅ Small objects only:
  - Images, small docs, snippets
  - Configurable max size (small, e.g., a few MB per object, global cap per pod)
- ✅ Associated with:
  - A message or post ID
  - A channel/board context

**Attachment storage:**
- Separate from media/backends used for large content
- Subject to:
  - Quotas
  - Retention policies (potentially shorter than messages)

---

### 5.2 Access Control

**Attachments respect the ACLs of their parent message/post:**
- ✅ If a user cannot see the message, they cannot see the attachment
- ✅ If a channel becomes private or is deleted:
  - Attachments follow the same retention/deletion rules

---

### 5.3 Federation and Replication

**By default:**
- ❌ Attachments are **not** replicated or federated:
  - They remain on the originating pod

**Remote pods:**
- ✅ Access attachments via authenticated HTTP URLs (if allowed by policy)
- ✅ Optionally cache locally with:
  - Very strict size limits
  - Optional per-org mirroring for critical channels

**Any attempt to use attachment storage as a generic replication layer is prohibited by policy and quotas, in line with the Principle of Least Replication.**

---

## Related Documents

- `docs/pod-f1000-social-hub-design.md` - ChatModule, ForumModule baseline
- `docs/health-routing-design.md` - Health-aware routing for message fanout
- `docs/replication-policy-design.md` - Replication constraints (Principle of Least Replication)
- `docs/realm-design.md` - Realm isolation and cross-realm federation
- `docs/federation-security-hardening.md` - Security requirements for federation
- `docs/self-healing-messaging-design.md` - Self-healing and resilient channels
- `TASK_STATUS_DASHBOARD.md` - T-MSG-RT-01, T-MSG-RT-02, T-SEARCH-01, T-ORG-01, T-ATTACH-01 tasks
