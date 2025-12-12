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

## 9. What Pods Share (By Realm Configuration)

This section explicitly defines what pods share under different realm configurations.

### 9.1 Pods in the Same Realm

**Assumption**: Same `realm.id`, same `governance_roots`, same `bootstrap_nodes`.

#### Always Potentially Shared (Subject to Config/ACLs)

**Transport / Overlay:**
- ✅ Share the same mesh/DHT overlay namespace
- ✅ Peer discovery within that overlay (subject to privacy constraints)
- ✅ Can find each other as peers for content routing/signaling (gated by domain, policies)

**Result**: Pods can discover and route to each other within the realm's overlay.

---

**Social / ActivityPub:**
- ✅ Can follow each other's actors
- ✅ Read/write posts across pods (like Mastodon instances in same fediverse)
- ✅ Show each other's content in timelines (subject to moderation)

**Result**: Social federation works naturally within the realm.

---

**Governance Feeds (F1000, Policy Profiles):**
- ✅ Can subscribe to the same governance feeds:
  - F1000 registry
  - Policy profiles
  - Moderation lists
- ✅ Treat those as advisory inputs
- ⚠️ Nothing forces them to apply policies—local admin still decides

**Result**: Shared governance as advisory input, not mandatory.

---

**Gossip Feeds (HealthFeed / AbuseFeed):**
- ✅ Can publish and consume realm-scoped health/abuse signals
- ✅ Use those signals as hints to adjust:
  - HealthScore
  - Routing
  - MCP heuristics

**Result**: Optional health/abuse signal sharing for network resilience.

---

**Replication (Small Stuff Only):**
- ✅ If enabled, can replicate **small, whitelisted objects**:
  - Governance docs
  - Moderation hash lists
  - Small metadata constructs (lists, tags)
- ❌ Big media remains non-replicated unless `FullCopy/Chunked` explicitly enabled (future)

**Result**: Minimal replication for high-value, small objects only.

---

**Content / File-Sharing:**
- ✅ Depending on domains and policies:
  - Music/movie/book/etc. content can be fetched across pods
  - Using allowed transports (Soulseek-like, HTTP, etc.)
- ✅ This is the whole point of the mesh: get content from multiple pods in same realm

**Result**: Distributed content access within realm.

---

#### Never Shared by Default (Same Realm)

Even in the same realm, pods **do NOT** automatically share:

**Pod-Local Secrets:**
- ❌ Private keys (pod identity, AP actors)
- ❌ API tokens
- ❌ Database credentials

**MCP Internals:**
- ❌ Model keys, provider configs
- ❌ Exact thresholds, internal logs
- ❌ LLM endpoints or credentials

**Pod-Local Admin:**
- ❌ Who is an admin on that pod
- ❌ Local ACLs/roles
- ❌ Admin passwords/credentials

**Private User Data:**
- ❌ DM content (if DMs implemented)
- ❌ Non-public library details
- ❌ Private user preferences

**Result**: Secrets, admin, and private data remain local by design. Sharing requires explicit API and policy choices.

---

### 9.2 Pods in Different Realms (No Bridge, No Peering)

**Assumption**: Different `realm.id`s, no bridge configuration.

#### By Default: Share Nothing at Protocol Level

**No Overlay:**
- ❌ DHT/mesh namespaces are different (`realm.id` used as namespace salt)
- ❌ They don't see each other as peers
- ❌ No automatic peer discovery

**No Governance:**
- ❌ Their F1000/governance roots are unrelated
- ❌ Governance docs from one are irrelevant to the other
- ❌ No shared governance feeds

**No Gossip:**
- ❌ Health/abuse feeds are realm-tagged
- ❌ Feeds from other realms ignored by default

**No Replication:**
- ❌ ReplicatorService won't consider peers outside realm
- ❌ No automatic replication across realms

**No Content Routing:**
- ❌ Planner doesn't consider peers from other realms
- ❌ No automatic content discovery

**Result**: Complete isolation at protocol level. Different realms = different universes.

---

**They MIGHT still talk as normal HTTP servers:**
- ✅ If you manually point one at the other (generic web, not realm logic)
- ✅ This is like any two web servers on the internet
- ⚠️ Not part of realm protocol, just HTTP

---

### 9.3 Pods in Different Realms (With Bridge / Multi-Realm Pod)

**Assumption**: Different `realm.id`s, `bridge.enabled = true`, specific `allowed_flows` configured.

#### If You Allow `activitypub:read/write`

**Bridge pod can:**
- ✅ Read AP posts from Realm A and:
  - Show them locally to users in Realm B
  - Optionally re-post/share them into Realm B's social feed
- ✅ Vice versa (depending on allowed flows)

**What IS shared:**
- ✅ Public social posts
- ✅ Public actor profiles
- ✅ Whatever AP objects bridge is configured to fetch/forward

**What is NOT shared (unless you deliberately break your own rules):**
- ❌ Governance roots / F1000 membership
- ❌ Internal MCP decisions
- ❌ Pod-local secrets
- ❌ Private user data

**Result**: Controlled social federation across realms (like bridging Mastodon instances from different servers).

---

#### If You Allow `metadata:read`

**Bridge pod can:**
- ✅ Query metadata/search endpoints in Realm A
- ✅ Use that data in Realm B (e.g., for discovery)

**What IS shared:**
- ✅ Public metadata (titles, tags, indexes, etc.) exposed via APIs

**What is NOT shared:**
- ❌ Private user-specific info
- ❌ Internal DB schemas or secrets
- ❌ Non-public library details

**Result**: Cross-realm discovery without exposing private data.

---

#### If You Later Allow `gossip:*` in Peering

**Bridge pod can:**
- ✅ Read health/abuse feeds from Realm A
- ✅ Optionally inject them as weak advisory signals in Realm B

**Still:**
- ⚠️ Realm B is free to ignore them
- ❌ This does NOT merge the realms' governance
- ❌ This does NOT make Realm A authoritative for Realm B

**Result**: Optional health hints across realms, purely advisory.

---

#### If You Were Reckless and Allowed `governance:root` or `replication:fullcopy`

**You'd be saying:**
- 💀 "Treat Realm A's governance as authoritative in B" OR
- 💀 "Allow full-copy replication of B's content into A or vice versa"

**We've already marked these as:**
- ❌ **SHOULD BE DENIED BY DEFAULT**
- ⚠️ Only allowed if you VERY explicitly decide that's what you want
- 🚨 High risk of undermining realm isolation

**Result**: Don't do this unless you're intentionally merging realms.

---

### 9.4 Summary Matrix

| Layer/Feature | Same Realm | Different Realms (No Bridge) | Different Realms (With Bridge) |
|---------------|------------|------------------------------|--------------------------------|
| **Mesh/DHT Overlay** | ✅ Shared namespace | ❌ Separate namespaces | ❌ Separate (bridge doesn't merge overlays) |
| **Peer Discovery** | ✅ Automatic | ❌ None | ❌ None (bridge is single pod, not discovery) |
| **Social/ActivityPub** | ✅ Natural federation | ❌ None | ✅ If `activitypub:*` allowed |
| **Governance (F1000, Profiles)** | ✅ Shared (advisory) | ❌ Separate roots | ❌ Separate (unless reckless `governance:root`) |
| **Gossip (Health/Abuse)** | ✅ Shared realm feeds | ❌ Realm-tagged, ignored | ✅ If `gossip:*` allowed (advisory) |
| **Replication (Small Objects)** | ✅ If enabled | ❌ Realm-scoped only | ❌ Separate (unless reckless `replication:fullcopy`) |
| **Content Routing** | ✅ Cross-pod within realm | ❌ None | ❌ None (bridge doesn't route content automatically) |
| **Metadata/Search** | ✅ Via APIs | ❌ None | ✅ If `metadata:read` allowed |
| **Pod Secrets** | ❌ Never shared | ❌ Never shared | ❌ Never shared |
| **MCP Internals** | ❌ Local only | ❌ Local only | ❌ Local only |
| **Local Admin** | ❌ Local only | ❌ Local only | ❌ Local only |
| **Private User Data** | ❌ Local only (unless explicit API) | ❌ Local only | ❌ Local only |

**Key Takeaways:**
- **Same realm**: Deep potential federation (overlay, social, gossip, small replication), but secrets/admin/private data remain local
- **Different realms (no bridge)**: Complete isolation at protocol level (air-gapped)
- **Different realms (with bridge)**: Only specified flows allowed (social, metadata, optionally gossip), governance/secrets remain isolated

---

## 10. Example Configurations

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
