# Virtual Soulfind Mesh Architecture

## 1. Purpose & Scope

This document describes a **decentralized "Virtual Soulfind" plane** that runs on top of slskdn's existing:

- Soulseek client stack (legacy protocol, official server)
- DHT-based rendezvous
- Overlay multi-swarm layer (MBID-aware, chunk-based transfers)

The goal is to get **Soulfind-like server intelligence** (search, indexing, rooms, scenes) **without** any static servers or privileged nodes:

- No central Soulfind instance
- No permanent infra anyone has to operate
- Behaviour emerges from **peers + DHT + overlay**, not from a dedicated backend

This also defines:

- **Disaster mode**: behaviour when the official Soulseek server is unavailable (shutdown, blocked, or account banned).
- Optional **governance/fairness** primitives that remain decentralized.
- How this plane integrates with existing slskdn features (MBID jobs, multi-swarm, Collection Doctor, Soulbeet integration).

Microtasks and implementation breakdown are intentionally out-of-scope here; this is an architecture & behaviour spec.

---

## 2. Design Constraints & Assumptions

### 2.1 Hard constraints

1. **No central servers**
   - No "always-on" Soulfind instance.
   - No privileged control nodes.
   - All behaviour must be implementable by any slskdn peer using DHT + overlay.

2. **DHT and overlay as backbone**
   - DHT: rendezvous and *small* metadata.
   - Overlay: authenticated peer-to-peer tunnels for control + data (multi-swarm chunks, gossip, richer metadata).
   - "Beacons" are just peers that, by virtue of DHT partitioning and config, temporarily take on more responsibility; they are not special roles in the protocol.

3. **Soulseek is origin / legacy, not center**
   - Official Soulseek server is:
     - A feed of raw peers and filenames.
     - A compatibility surface for normal clients.
   - All "new brain" behaviour (MBIDs, scenes, governance, disaster mode) lives entirely in the Virtual Soulfind mesh.

4. **No multi-network bridging (for now)**
   - No torrent/IPFS/CDN bridging.
   - Metadata referencing to other APIs is allowed (MB/Discogs/etc.), but data movement stays Soulseek + overlay.

5. **Disaster mode is a first-class goal**
   - If the official server disappears or you are banned, enhanced slskdn clients should continue operating as a degraded-but-functional mesh.
   - This necessarily implies the mesh is **not bound** to Soulseek's ban list.

6. **Governance is local-first, optional mesh-enhanced**
   - Each peer enforces its own fairness and trust rules.
   - Any "shared governance" must be gossip- or DHT-based and optional, not authoritative.

### 2.2 Soft assumptions

- Peers can talk to MusicBrainz / Discogs (or a cache) for metadata lookups.
- Peers can maintain a persistent store (DB + on-disk logs).
- The overlay layer already supports basic message types:
  - `mbid_swarm_descriptor`
  - `fingerprint_bundle_advert`
  - `mesh_cache_job`
  - `chunk_request/response/cancel`

---

## 3. Conceptual Model

### 3.1 Three planes

We conceptually split the world into three planes:

1. **Legacy Soulseek Plane**
   - Official server, proprietary protocol.
   - Clients: slskdn, SoulseekQt, others.
   - Provides:
     - Searches over filenames.
     - Room membership & messaging.
     - Peer discovery.
     - Legacy transfers.

2. **Virtual Soulfind Mesh Plane** (this document)
   - Runs in slskdn peers only.
   - Provides:
     - MBID-aware indexing and search.
     - Scene/micro-network semantics (room-like, but via DHT topics).
     - "Shadow index" of availability derived from captured Soulseek traffic + overlay.
     - Disaster-mode mappings from high-level requests → peers & variants.

3. **Overlay Swarm Plane**
   - slskdn-only, over DHT + TLS overlay connections.
   - Provides:
     - Multi-swarm chunk transfers.
     - Canonical variant selection.
     - Rescue mode for underperforming Soulseek transfers.
     - Fairness & reputation enforcement.

The Virtual Soulfind mesh **observes** plane 1 and **feeds** plane 3.

### 3.2 Core entities

- **Peer**
  - A running slskdn instance.
  - Has:
    - Overlay identity (keypair).
    - DHT node ID.
    - Local library + metadata.
    - Local policy (fairness, trust, disaster mode toggles).

- **Beacon**
  - Any peer that, by DHT ownership and config, temporarily stores additional metadata for certain keys (scenes, MBIDs, missions).
  - Not a special type; simply "the peers who currently own those keys".

- **Scene**
  - A conceptual "micro-network" (label, genre, crew).
  - Represented as a DHT key and associated overlay gossip.
  - Example keys:
    - `scene:label:warp-records`
    - `scene:genre:dub-techno`
    - `scene:key:<pubkey>:friends`

- **MB Object**
  - An MB Recording, Release, Release Group, Artist, or Label.
  - Identified by a stable MusicBrainz ID.
  - Used as the canonical handle for search, swarming, health checks, etc.

- **Job**
  - A high-level operation:
    - `mb_release` (album download)
    - `discography`
    - `label_crate`
    - `repair_mission` (library fix)
  - Jobs live entirely within slskdn; the mesh and shadow index help resolve them to peer lists.

---

## 4. Data Flows: Normal vs Disaster Mode

### 4.1 Normal mode (Soulseek server available)

1. **User action**
   - Soulbeet UI or CLI creates:
     - A classic search / download, or
     - A high-level MBID job (album, discography, crate, repair).

2. **Legacy Soulseek interaction**
   - slskdn:
     - Sends search to official server.
     - Receives results (users + paths).
   - Classic transfers (if used) go through the legacy protocol.

3. **Capture & normalize**
   - Virtual Soulfind component:
     - Observes search results and transfers.
     - Normalizes them into MB objects:
       - MB Release IDs, MB Recording IDs, variant hashes.
     - Updates local DB:
       - Availability information (who has what, in which variant).
       - Quality statistics.

4. **Shadow index contribution**
   - Periodically, the peer publishes tiny aggregated "shards" into the DHT and optionally gossips via overlay:
     - For MBID keys: "N peers, canonical variants A/B, recent activity timestamp."
     - For scenes: "This peer participates in scene S and has these MBIDs/labels."

5. **Multi-swarm / overlay**
   - For MBID jobs and rescue:
     - slskdn uses:
       - Live Soulseek responses (direct).
       - Shadow index (DHT).
       - Overlay adverts (swarm descriptors).
     - To choose:
       - Which peers to swarm with.
       - Which variants to prefer (canonical, pods, etc.).

### 4.2 Disaster mode (server unreachable / banned)

Trigger conditions (any):

- Cannot connect to official server for extended period.
- Explicit configuration: "force offline/mesh-only mode".
- Local detection of ban (repeated auth failures, specific error codes).

Behaviour changes:

1. **Search & discovery**
   - Legacy Soulseek plane is bypassed.
   - All discovery is:
     - MBID/metadata-based (MB/Discogs queries).
     - DHT-based:
       - Query MBID keys → get back peer sets & hints.
       - Query scene keys → find micro-networks.
     - Overlay-based:
       - Ask connected peers for additional hints and descriptors.

2. **Rooms / chats**
   - Soulseek rooms vanish.
   - Scenes become the replacement:
     - Scene DHT keys + overlay pubsub.
   - The UX can map former room names to scene keys if known.

3. **Transfers**
   - All new data transfers are:
     - Overlay multi-swarm only (no legacy Soulseek connections).
   - Multi-swarm uses:
     - Shadow index + live overlay adverts to find peers for MBIDs.

Users on legacy clients: effectively offline.  
slskdn peers: still able to search, swarm, repair, and coordinate (degraded).

---

## 5. Components

### 5.1 Capture & Normalization Pipeline

**Purpose:** Convert observed Soulseek behaviour into MB-aware, mesh-usable metadata.

Responsibilities:

1. Observe:
   - Search requests sent to the official server.
   - Search results (user + path).
   - Actual Soulseek transfers (user, path, size, speed, success/failure).

2. Normalize:
   - For each completed file:
     - Run fingerprint → MB Recording / Release ID resolution.
     - Build or update an `AudioVariant` record (codec, duration, size, quality_score, transcode_suspect).
   - For search results:
     - Map best-effort: path → likely MB Release / Recording (using heuristics and MB metadata).

3. Persist:
   - Per-MBID:
     - Known peer pseudonyms that have that MBID.
     - Variant summaries (hash, codec, size, quality_score).
     - Last-seen timestamps.

4. Privacy:
   - Store pseudonymized peer IDs:
     - Use overlay peer identity (public keys) instead of raw Soulseek usernames when possible.
     - Avoid storing cleartext usernames globally; keep mapping as local-only where needed.

5. Output:
   - Feeds:
     - Shadow index generator.
     - Canonical scoring.
     - Library health / Collection Doctor.

### 5.2 Shadow Index Over DHT

**Purpose:** Provide a decentralized, fault-tolerant index of "who appears to have what" at the MBID level, for both normal and disaster modes.

Principles:

- DHT stores **only compact, aggregated metadata**, never large payloads.
- Overlay remains the channel for richer descriptors.

Per key type:

1. **MBID key** (e.g. `mbid:release:<GUID>`):
   - Stored value includes:
     - Approximate count of peers (or small sketches).
     - Compression of peer IDs (hashed or truncated).
     - Canonical variant hints (hashes, codecs).
     - Last update time / TTL.

2. **Scene key** (e.g. `scene:label:<slug>`):
   - Stored value includes:
     - A set of peer IDs that are participants.
     - Optional summary stats:
       - How many releases known.
       - How many are canonical.

Publishing:

- Each peer periodically:
  - Chooses which MBIDs and scenes to publish shards for (e.g. a random subset).
  - Writes or merges its shard into the DHT values.
- Shards are:
  - Versioned and TTL'd.
  - Designed to be small and lossy rather than precise.

Consuming:

- To resolve a job for MB Release X:
  - Query DHT at `mbid:release:X`.
  - Get back:
    - Candidate peer IDs.
    - Canonical variant hints.
  - Use overlay to connect and obtain full descriptors.

### 5.3 Scenes / Micro-Networks via DHT Topics

**Purpose:** Replace server-managed rooms with decentralized "scenes" that provide social / semantic grouping.

Representation:

- Scene ID → DHT key:
  - Basic deterministic mapping from symbolic name to DHT key.
- Peers "join" a scene by:
  - Writing membership info to the DHT under that key.
  - Subscribing to overlay pubsub for that scene.

Overlay semantics:

- "Room chat" (optional, later):
  - Implemented as overlay pubsub where:
    - Messages are signed.
    - Each peer can throttle or mute as needed.

- "Scene metadata":
  - Peers gossip:
    - MBIDs strongly associated with the scene.
    - Canon-rings and provenance preferences of that scene.

Integration with jobs:

- Discography/label crate jobs can be:
  - Scoped to a scene ("Warp label crate from `scene:label:warp-records` only").
  - Prioritize peers who appear in the scene.

### 5.4 Overlay Swarm Integration

The Virtual Soulfind mesh does **not** replace existing overlay message types. It augments them:

- DHT gives you **who** might be interesting for an MBID or scene.
- The overlay descriptors give you **what** they have and **how** they can serve it:
  - `mbid_swarm_descriptor` – release-level availability & policies.
  - `fingerprint_bundle_advert` – recording-level variant info.
  - `mesh_cache_job` – control-plane for job collaboration.

Schedulers and job planners:

- Use:
  - Shadow index for initial candidate set.
  - Overlay descriptors for detailed scoring (quality, canonical, RTT, reputation).
- Disaster mode:
  - Skips the "ask legacy server" step and relies entirely on DHT + overlay descriptors.

### 5.5 Disaster Mode & Failover

**Trigger logic:**

- Maintain a "Soulseek health" state:
  - `healthy` / `degraded` / `unavailable`.
- Based on:
  - Connection attempts and durations.
  - Error codes (auth failures, timeouts).
- When `unavailable` persists beyond a threshold:
  - Flip `mode` from `normal` to `disaster`.

**Mode effects:**

1. **Searches**
   - Normal:
     - Query Soulseek server + optionally consult shadow index to refine.
   - Disaster:
     - Resolve query → MBIDs via metadata (MB/Discogs).
     - Query DHT (MBID keys + scenes) for candidates.
     - Bootstrap overlay descriptor exchange directly.

2. **Jobs**
   - Normal:
     - MB Release jobs:
       - Prefer Soulseek sources when cheap.
       - Use overlay for rescue and multi-swarm.
   - Disaster:
     - MB Release jobs:
       - Use overlay-only multi-swarm from peers discovered via DHT.
       - Enforce fairness/guardrails locally.

3. **UX**
   - Expose a clear indicator (for power users):
     - "Legacy connection: offline; mesh-only mode active."
   - Soulbeet stays unaware at API level; behaviour differences are inside slskdn.

### 5.6 Governance & Fairness (Optional Mesh Layer)

Governance is intentionally **local-first**. Shared information is optional and advisory.

Core local mechanisms (already part of slskdn plan):

- Per-peer reputation:
  - Successful vs failed/corrupt chunks, timeouts, cancellations.
- Traffic accounting:
  - Overlay up/down vs Soulseek up/down.
- Fairness governor:
  - Local policies like:
    - "Don't download more than X times what I upload over overlay."
    - "Throttle overlay if my upload deficit is too large."

Optional mesh-enhanced primitives:

1. **Contribution summaries**
   - Peers can publish signed summaries to DHT:
     - "I claim I uploaded U bytes and downloaded D bytes over period P."
   - Each peer chooses:
     - Whether to trust any given key.
     - How to weight that when scheduling.

2. **Soft-ban hints**
   - Peers can publish:
     - "I refuse to cooperate with peer P for behaviour reasons R."
   - Other peers:
     - May interpret this as:
       - A scheduling penalty.
       - Or ignore it entirely.

Non-goals:

- No global "ban list".
- No central authority.  
- No mandatory policy adoption.

### 5.7 Provenance & Canonicalization Integration (Optional, but Recommended)

The Virtual Soulfind mesh can feed into:

- Canon-rings (peer-signed canonical variants).
- Provenance bundles (rip logs, AccurateRip/CTDB-style validations).
- Repair missions (shared requests to fix library issues).

Mechanically:

- For each MB Release:
  - Shadow index carries:
    - Canon candidate hashes.
    - (Optionally) votes from pods or scenes.
- Collection Doctor:
  - Uses this to:
    - Flag non-canonical local variants.
    - Offer "Fix via multi-swarm" options that pick canon variants from trusted peers/scenes.

These are **value-add** features; they don't impact the core discovery/failover path.

---

## 6. Security, Privacy, Abuse

Key goals:

- Avoid leaking more information than necessary.
- Make sure "capture & shadow index" doesn't become a surveillance tool.

Guidelines:

1. **Peer identities**
   - Distinguish:
     - Soulseek usernames (legacy).
     - Overlay identities (public keys).
   - Shadow index and DHT:
     - Should use overlay IDs or hashed IDs, not raw Soulseek usernames.
   - The mapping (username → overlay ID) stays local.

2. **Content privacy**
   - Never store full filenames / paths in DHT.
   - Only publish:
     - MBIDs, variant hashes, codec/duration info.
   - Peers who want the full path can ask the peer that owns the file during overlay negotiation.

3. **Rate limiting & spam**
   - DHT and overlay message handling:
     - Must include basic rate limiting, per-peer caps, etc.
   - Scenes:
     - Each peer decides which scenes to join and which messages to accept.

4. **Abuse handling**
   - There is no global ban.
   - Each peer:
     - Maintains its own blocklist/trustlist.
     - May choose to import soft-ban hints from DHT but is never forced.

---

## 7. Integration with Existing slskdn Features

### 7.1 MBID Jobs

- `mb_release`, `discography`, `label_crate` jobs:
  - Already MBID-native.
  - For source selection they:
    - In normal mode:
      - Use Soulseek + overlay + shadow index.
    - In disaster mode:
      - Use DHT + overlay only.

No API surface change is needed; only the internal resolver changes.

### 7.2 Rescue Mode

- Underperforming Soulseek transfers:
  - Can still exist while the server is available.
  - The mesh is used:
    - To locate peers with the same MB Recording.
    - To swarm missing ranges by overlay.

In disaster mode, there is nothing to rescue; all jobs are overlay-only.

### 7.3 Collection Doctor / Library Health

- Uses the same MB-aware metadata and canonical scoring as the shadow index.
- In addition:
  - Can emit **repair missions** that are:
    - Local-only (fix using your own jobs), or
    - Shared via scenes/DHT (ask pods for better variants).

Virtual Soulfind just provides:
- The MB-aware view and remote availability hints that make those missions more powerful.

### 7.4 Soulbeet Integration

Soulbeet's view stays:

- HTTP + job APIs:
  - `/api/jobs/mb-release`
  - `/api/jobs/discography`
  - `/api/jobs/label-crate`
  - `/api/slskdn/library/health`, etc.

slskdn:

- Internally decides whether:
  - To resolve sources via:
    - Soulseek + mesh, or
    - Mesh-only (disaster mode).

Soulbeet doesn't need to be aware of DHT, scenes, or shadow index internals.

---

## 8. Configuration & Modes

Recommended config knobs (names illustrative):

- `mesh.enabled` (bool)
- `mesh.shadow_index.enabled` (bool)
- `mesh.disaster_mode.auto` (bool)
- `mesh.disaster_mode.force` (bool)
- `mesh.scenes.enabled` (bool)
- `mesh.governance.enabled` (bool) – toggles optional DHT contribution hints.
- `mesh.privacy.anonymize_usernames` (bool) – controls how aggressively Soulseek usernames are abstracted in the index.

Modes:

- **Legacy-only**  
  - All `mesh.*` disabled.
  - slskdn behaves like a fancy but still mostly classic Soulseek client.

- **Hybrid (default)**  
  - `mesh.enabled = true`, `shadow_index.enabled = true`, `disaster_mode.auto = true`.
  - Uses server when present; falls back to mesh when needed.

- **Mesh-only**  
  - `disaster_mode.force = true`.
  - Never talk to the official server; purely overlay + DHT, for testing or ideological reasons.

---

## 9. Future Extensions (Non-Goals for Now)

These ideas are intentionally *deferred*:

- Multi-network bridges (Torrent, IPFS, HTTP mirrors).
- Global governance (hard ban lists, central authorities).
- Rich full-text search over arbitrary tags in the DHT (too heavy; better done via overlay or local indexing).

They can be revisited later under separate branches / docs without touching the core Virtual Soulfind mesh.

---

## 10. Summary

- We **do not** deploy Soulfind as a central server.
- We **do** steal its semantics (search, rooms, indexing) and re-express them as:
  - DHT topics (scenes, MBIDs).
  - Embedded server-like behaviours in each slskdn peer.
  - Overlay gossip and multi-swarm coordination.

The result is a **Virtual Soulfind plane**:

- In normal times:
  - Enhances Soulseek with MBID-aware search, canonicality, scenes, and mesh-based repairs.
- In disaster times:
  - Lets enhanced slskdn peers continue operating as a self-sustaining mesh, with minimal disruption to the UX for those peers.

All of this is compatible with the existing multi-swarm, MBID, Collection Doctor, and Soulbeet integration designs, and it respects the constraints:
no central nodes, DHT + overlay first, Soulseek as input rather than anchor.


