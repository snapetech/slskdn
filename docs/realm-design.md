# Realm Design (Network Universes & Peering)

**Status**: DRAFT - Future Network Layer  
**Created**: December 12, 2025  
**Priority**: 🟡 MEDIUM (after core architecture)

> **Project Note**: This is a fork of [slskd](https://github.com/slskd/slskd). See [../README.md](../README.md#acknowledgments) for attribution.

---

This document defines **Realms**: logical universes of pods that share:

- An overlay / mesh / DHT space
- Governance roots (for advisory governance)
- Optional gossip / replication policies

Realms provide **strong default isolation** between independent deployments of the network, with opt-in mechanisms for peering and bridging.

---

## 1. Goals

**Allow multiple independent deployments ("universes") to exist:**
- Different operators
- Different governance roots
- Different bootstrap nodes and policies

**Ensure isolation by default:**
- Pods from one realm do not automatically join or interact with another

**Allow explicit meshing between realms:**
- Via:
  - Shared RealmConfig (same realm)
  - Multi-homed pods acting as bridges
  - Explicit peering policies

**Maintain:**
- Local sovereignty of pods
- Strong security and privacy boundaries
- No accidental global Borg network

---

## 2. RealmConfig & Realm Identity

Each pod has a **RealmConfig** that determines which realm(s) it belongs to.

### 2.1 RealmID

**`realm_id: string`** – a stable identifier for the realm, e.g.:
- `"slskdn-main-v1"`
- `"keith-realm-01"`
- `"testnet-2026-01"`

**`realm_id` is used as:**
- A namespace salt for mesh/DHT overlays
- A label for governance / gossip / replication scope
- A selector for bootstrap nodes and peering configuration

---

### 2.2 RealmConfig Structure (Single Realm)

For a pod that participates in a single realm:

```yaml
realm:
  id: "slskdn-main-v1"
  governance_roots:
    - gov_root:<hash(pub_root_1)>
    - gov_root:<hash(pub_root_2)> # optional council, etc.
  bootstrap_nodes:
    - "https://first-pod.example/api/bootstrap"
    - "https://other-seed.example/api/bootstrap"
  federation_defaults:
    mode: "Hermit" # or Federated, or Off, etc.
  policies:
    allow_gossip: true
    allow_replication: true
```

**`governance_roots`:**
- Governance identities whose signed governance docs (F1000 registries, policy profiles) are trusted **for this realm**

**`bootstrap_nodes`:**
- Pods or endpoints used to join the realm's overlay

**`federation_defaults`:**
- Default ActivityPub/social behavior within this realm

**`policies`:**
- High-level toggles for optional features (gossip/replication)

---

## 3. Realm Isolation

By default, realms are **mutually isolated**:

### Mesh / DHT
- Keys, Kademlia IDs, and routing tables are derived with a `realm_id` salt
- Nodes from different realms do **not** share the same overlay

### Governance
- F1000 registries and policy profiles are signed by realm-specific `governance_roots`
- Pods ignore governance docs signed by unknown roots (unless explicitly configured)

### Gossip
- HealthFeed and AbuseFeed are scoped to a realm
- `realm_id` is part of the feed metadata

### Replication
- ReplicatorService does not consider peers from other realms unless explicit peering is configured

**This means:**

If someone else clones the project and configures `realm.id = "other-realm"`:
- ✅ They create a **separate universe**
- ✅ Their pods do not connect to yours
- ✅ Their governance is distinct
- ✅ Their gossip/replication remains local to their realm

---

## 4. Joining an Existing Realm

To join an existing realm, a pod must:

1. Configure `realm.id` to match the target realm
2. Configure `governance_roots` to match the known roots for that realm
3. Configure `bootstrap_nodes` appropriate for that realm

**Once configured:**

The pod will:
- Join the realm's mesh/DHT overlay
- Treat governance docs signed by `governance_roots` as trusted for this realm
- Optionally subscribe to that realm's gossip feeds and replication policies, if allowed by local configuration

**Joining a realm should feel like:**
- Adding a new Git remote + GPG key:
  - An explicit, operator-driven choice
  - Not something that happens by accident

---

## 5. Multi-Realm Pods & Bridging

We allow pods to be **multi-homed** (participate in more than one realm) in a controlled way, primarily for bridging.

### 5.1 MultiRealmConfig

A pod MAY have:

```yaml
realms:
  - id: "slskdn-main-v1"
    governance_roots: [...]
    bootstrap_nodes: [...]
    policies: {...}
  - id: "other-realm-01"
    governance_roots: [...]
    bootstrap_nodes: [...]
    policies: {...}
bridge:
  enabled: true
  allowed_flows:
    - "activitypub:read"
    - "activitypub:write"
    - "metadata:read"
  disallowed_flows:
    - "governance:root"
    - "replication:fullcopy"
    - "mcp:control"
```

- Each realm entry is treated as an independent overlay membership
- The pod runs realm-aware components (e.g., mesh clients) for each configured realm

---

### 5.2 Bridge Behavior

A **bridge pod**:
- Connects to multiple realms simultaneously
- At higher layers (social/data), can:
  - Read from one realm and post into another (subject to policy)
  - Fetch metadata from one realm for local use
  - Optionally share limited gossip or replication hints

**Hardening rules:**

**No realm may be treated as the governance root for another realm by default**

**No realm may automatically inherit:**
- F1000 membership
- Policy profiles
- MCP configuration

**All cross-realm flows:**
- Must be explicitly listed in `bridge.allowed_flows`
- May be further constrained by per-realm policies

**Examples:**

**ActivityPub bridging:**
- A pod follows actors in Realm B and shows their posts in Realm A, respecting local MCP and social policies

**Metadata bridging:**
- A pod queries search/metadata in Realm B and uses it for discovery in Realm A

**Bridging is optional and fully controlled by operators.**

---

## 6. Explicit Peering Between Realms

Realms can establish higher-level **peering arrangements**:

Defined in config or governance docs, e.g.:

```yaml
realm_peers:
  - local_realm: "slskdn-main-v1"
    remote_realm: "other-realm-01"
    flows:
      allow:
        - "activitypub:read"
        - "activitypub:write"
        - "healthfeed:read"
      deny:
        - "governance:root"
        - "replicator:fullcopy"
```

**A pod that participates in both realms and has `realm_peers` configured:**

Acts as a **realm peer**:
- Only specified flows are allowed across realms
- Governance roots remain distinct:
  - No automatic trust in remote governance

**Other pods in each realm may consume cross-realm data via:**
- AP federation
- Gossip endpoints
- Metadata APIs

**Peering is:**
- Opt-in on both sides (symmetry strongly recommended)
- Revocable by changing config or governance docs

---

## 7. Security, Privacy, and Hardening

### Key principles

**Isolation by default:**
- New realms are isolated unless explicitly configured to peer or share

**Explicit trust:**
- Trust in governance roots, bootstrap nodes, and bridges is always a configuration choice

**Realm-aware everything:**
- Mesh/DHT, governance, gossip, replication all use `realm_id` to scope operations

---

### Threats & mitigations

**Accidental global merge:**
- ✅ Avoid using generic `realm_id`s like `"default"`; strongly recommend explicit IDs
- ✅ Document that reusing `realm_id` means joining the same universe

**Malicious realm masquerading as "main":**
- ✅ Pods must verify governance roots:
  - `realm.id` alone is not enough
  - Governance root keys must match expected values

**Bridge misconfiguration:**
- ✅ Bridges must:
  - Treat all realms as untrusted by default
  - Only allow flows explicitly configured in `bridge.allowed_flows`
- ✅ Local admins must be able to:
  - Disable bridging entirely
  - Audit which flows are active

---

### Privacy

**Cross-realm flows must not:**
- ❌ Leak private user data without consent
- ❌ Export internal MCP decisions or logs

**ActivityPub and metadata APIs already respect:**
- ✅ The social federation design
- ✅ Moderation and access-control policies

---

## 8. Migration & Realm Changes

Changing realms is a **major operation**:

**Changing `realm_id` on an existing pod should be treated like:**
- Migrating to a different universe
- Requires:
  - New `governance_roots`
  - New `bootstrap_nodes`
  - Possibly different social/federation policies

**Recommended approach:**

Prefer spinning up a new pod for a new realm and:
- Migrating data as needed via export/import tools
- Treating cross-realm flows as part of a controlled bridge

**Realm changes must be:**
- ✅ Explicit
- ✅ Logged
- ✅ Confirmed by the operator (high-friction operation)

---

## 9. Example Configurations

### Single-Realm Pod (Most Common)

```yaml
realm:
  id: "slskdn-main-v1"
  governance_roots:
    - "gov_root:a1b2c3d4..."
  bootstrap_nodes:
    - "https://first-pod.example/api/bootstrap"
  federation_defaults:
    mode: "Hermit"
  policies:
    allow_gossip: true
    allow_replication: true
```

**Result**: Pod joins `slskdn-main-v1` realm, isolated from all other realms.

---

### Multi-Realm Bridge Pod

```yaml
realms:
  - id: "slskdn-main-v1"
    governance_roots: ["gov_root:a1b2c3d4..."]
    bootstrap_nodes: ["https://first-pod.example/api/bootstrap"]
    policies: {allow_gossip: true, allow_replication: true}
  
  - id: "experimental-realm-02"
    governance_roots: ["gov_root:x9y8z7w6..."]
    bootstrap_nodes: ["https://experimental-seed.example/api/bootstrap"]
    policies: {allow_gossip: false, allow_replication: false}

bridge:
  enabled: true
  allowed_flows:
    - "activitypub:read"
    - "metadata:read"
  disallowed_flows:
    - "governance:root"
    - "replication:fullcopy"
    - "mcp:control"
```

**Result**: Pod joins both realms, can read ActivityPub/metadata across realms, but governance/replication remain isolated.

---

### Explicit Realm Peering

```yaml
realm:
  id: "slskdn-main-v1"
  governance_roots: ["gov_root:a1b2c3d4..."]
  bootstrap_nodes: ["https://first-pod.example/api/bootstrap"]
  policies: {allow_gossip: true, allow_replication: true}

realm_peers:
  - local_realm: "slskdn-main-v1"
    remote_realm: "friendly-realm-03"
    flows:
      allow:
        - "activitypub:read"
        - "activitypub:write"
        - "healthfeed:read"
      deny:
        - "governance:root"
        - "replicator:fullcopy"
```

**Result**: Pod can interact with `friendly-realm-03` via specified flows, but governance/replication remain separate.

---

## Related Documents

- `docs/pod-identity-lifecycle.md` - Pod identity and keys
- `docs/f1000-governance-design.md` - Governance roots and F1000 registries
- `docs/health-routing-design.md` - Health scoring (realm-scoped)
- `docs/replication-policy-design.md` - Replication (realm-scoped)
- `docs/gossip-signals-design.md` - Gossip feeds (realm-scoped)
- `docs/social-federation-design.md` - ActivityPub (can bridge across realms)
- `docs/security-hardening-guidelines.md` - Global security principles
- `TASK_STATUS_DASHBOARD.md` - T-REALM-01 through T-REALM-05 tasks
