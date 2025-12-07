# Multi-Source Downloads - Experimental Feature

## Overview

Multi-source downloading allows slskdn to download a single file from multiple peers simultaneously, similar to BitTorrent's "swarm" behavior. This can significantly improve download speeds for popular files.

## How It Works

### Swarm Mode (Chunked Downloads)

1. **Search & Pool Building**: Find all peers sharing an identical file (matched by exact file size)
2. **Chunk Division**: Split the file into small chunks (default: 128KB)
3. **Worker Distribution**: Spawn a worker for each available source
4. **Shared Queue**: All workers grab chunks from a shared `ConcurrentQueue`
5. **Fast Peers Do More**: Workers that finish quickly grab another chunk immediately
6. **Retry Logic**: Failed chunks are re-queued for other workers
7. **Proven Source Retry**: After initial pass, retry remaining chunks using only sources that succeeded
8. **Assembly**: Combine all chunks into final file

### Key Findings

#### What Works

- **Partial downloads via `startOffset`**: Soulseek protocol supports starting downloads at arbitrary byte offsets
- **`LimitedWriteStream` workaround**: Since Soulseek requires full file size even for partial downloads, we wrap the output stream to cancel after receiving the desired chunk
- **Concurrent chunk downloads**: Multiple workers can grab different chunks simultaneously
- **Proven source retry**: After first pass, re-using only successful sources dramatically improves completion rate

#### CRITICAL: Source Verification Required

**Problem discovered**: Files with identical sizes can have different byte content (different encodes, masters, or releases). Mixing chunks from different source files causes **FLAC corruption** at chunk boundaries.

**Solution**: Before starting a multi-source download, verify that all sources have **identical FLAC STREAMINFO MD5 hashes**:

1. Download first 42 bytes from each candidate source
2. Parse FLAC STREAMINFO block to extract the audio MD5 hash
3. Group sources by their MD5 hash
4. Only use sources from the **largest matching group**

Example: If 34 sources have size 24,770,547 but only 28 share the same FLAC MD5, use only those 28.

**API flag**: `skipVerification: false` (default is `true` for speed, but causes corruption!)

#### Limitations Discovered

1. **Many clients reject partial downloads**: Most Soulseek clients will reject download requests with `startOffset > 0`, reporting "Download failed by remote client"
2. **Single download per user per file**: Soulseek only allows one active download of a specific file from a specific user at a time
3. **Variable client behavior**: Some clients work perfectly with partial downloads, others reject immediately

#### Speed Thresholds (Dynamic)

- **Minimum speed**: Dynamic - 15% of best observed speed (minimum floor: 5 KB/s)
- **Slow duration**: 10 seconds of sustained slow speed
- Workers downloading slower than the threshold are cycled out with a **timeout** (not blacklisted)
- Their chunk is re-queued for faster peers
- Timed-out peers can retry after 30 seconds (they may have recovered)

#### Retry Behavior

- Workers tolerate up to 3 consecutive failures before giving up
- Failed chunks are always re-queued for other workers
- After initial pass completes, up to 5 retry rounds using only "proven" sources (those that succeeded at least once)
- Slow peers get **timeouts** (temporary), not blacklists (permanent)

## API Endpoints

### POST `/api/v0/multisource/download`

Direct download with pre-verified sources:

```json
{
  "filename": "path/to/file.flac",
  "fileSize": 21721524,
  "chunkSize": 131072,
  "sources": [
    {"username": "user1", "fullPath": "their/path/file.flac"},
    {"username": "user2", "fullPath": "different/path/file.flac"}
  ]
}
```

### POST `/api/v0/multisource/swarm`

Search and download in one call:

```json
{
  "filename": "search term",
  "size": 21721524,
  "chunkSize": 131072,
  "searchTimeout": 30000
}
```

## Test Script

`test-swarm.sh` - Interactive menu-driven test tool:

```bash
export SLSK_USER=your_username
export SLSK_PASS=your_password
./test-swarm.sh
```

Features:
- Browse files by source count
- Select any file to start swarm download
- Press `/` anytime to add new artist to discovery pool
- Background discovery rotates through all artists
- Press `r` to reset partial-support flags

## Configuration

Default chunk size: 128KB (configurable per request)

Recommended settings:
- Chunk size: 64-256KB (smaller = more parallelism, larger = less overhead)
- Minimum sources: 3+ for meaningful benefit
- Speed threshold: 5 KB/s minimum, 15s tolerance

## Validated Improvements (Dec 2025)

After extensive testing, the following improvements have stabilized the swarm behavior:

### 🎉 WORKING MULTI-SOURCE FLAC DOWNLOADS - VERIFIED!

1.  **SHA256 Byte Verification** (Critical Fix):
    *   Verify sources using **SHA256 of first 32KB bytes** (not FLAC audio MD5!)
    *   FLAC audio MD5 only verifies decoded audio - different encodes can have same MD5 but different bytes
    *   This caused corruption when mixing chunks from different encodes
    *   API: Set `skipVerification: false` to enable (REQUIRED for integrity)

2.  **Atomic Chunk Writes** (Critical Fix):
    *   Each worker writes to a unique temp file (`chunk_XXXX_<hash>.tmp`)
    *   Winner atomically moves to final path; losers delete their temp files
    *   Prevents race condition corruption with speculative execution

3.  **Chunk Size: 512KB** (Optimized):
    *   Balances connection overhead amortization vs failure recovery
    *   Typical overhead: **33-57%** (vs 80-100% with smaller chunks)
    *   TTFB: 2-5 seconds, Transfer: 1.5-6 seconds

4.  **Timing Metrics**:
    *   `TimeToFirstByteMs` - connection/handshake overhead
    *   `TransferTimeMs` - actual data transfer time
    *   `OverheadPercent` - non-transfer time percentage
    *   `TransferSpeedBps` - raw speed excluding overhead

5.  **Unlimited Retries with Stuck Detection**:
    *   Retries continue until complete or **3 consecutive rounds with zero progress**
    *   No arbitrary retry limit that causes premature failure

6.  **Dynamic Speed Threshold**:
    *   Minimum speed: **15% of best observed speed** (floor: 5 KB/s)
    *   Slow duration: **8 seconds** before cycling out
    *   Hard timeout: **10 seconds** per chunk

7.  **Peer Timeouts (not Blacklists)**:
    *   Slow/failed peers get **20-second timeout**, not permanent ban
    *   Allows recovery from temporary network issues
    *   Desperation mode clears all timeouts and retries everyone

### Successful Test Run (1MB Chunks)

```
[SWARM] Starting with 16 sources, 1024KB chunks
...
[SWARM] ✓ julesss chunk 12 @ 91 KB/s
[SWARM] ✓ brunzmeflugen chunk 2 @ 92 KB/s
[SWARM] ✗ katharsis chunk 7: User katharsis appears to be offline (fail 1/3)
...
[SWARM] ✓ YVR chunk 0 @ 125 KB/s
...
[SWARM] SUCCESS! Chunk distribution:
   YVR: 3 chunks
   bstroszek: 3 chunks
   julesss: 3 chunks
   trianine: 3 chunks
   Antarctiica: 2 chunks
   dankmolot: 2 chunks
   officejapan: 2 chunks
   Pez: 1 chunks
   brunzmeflugen: 1 chunks
   red_book: 1 chunks

✅ SUCCESS!
   Sources used: 10
   Time: 81709ms
   Output: /tmp/slskdn-test/...Within.flac
   ✅ FLAC verification PASSED
```

## Protocol Extension Proposals

The following analysis outlines potential extension points within the existing Soulseek protocol to support advanced features like capability negotiation and multi-part transfer coordination, while maintaining backward compatibility.

**Note:** In all implementation scenarios, preference and compatibility with legacy clients and the standard network protocol must be preserved. Extensions should degrade gracefully and remain invisible to non-supporting clients.

### Viable Extension Targets

#### 1. User Info Map (`UserInfoRequest` / `UserInfoResponse`)
*   **Structure:** Map of keys → values (strings, integers, etc.).
*   **Implementation:** Introduce a custom key (e.g., `"slskdn_caps"`) containing a JSON object or base64-encoded flags.
    *   *Example:* `{"mp_enabled": true, "version": "1.1"}`
*   **Compatibility:** Legacy clients typically ignore unknown keys, making this a low-risk method for capability negotiation.

#### 2. Queue Upload Request – "Reason" Field (`QueueUploadRequest`)
*   **Structure:** Contains Filename, User, and a "Reason" string intended for display.
*   **Implementation:** Overload the reason field with structured metadata.
    *   *Example:* `reason="multipart:chunk=2/4;start=1048576;length=1048576"`
*   **Compatibility:** Standard clients will display the raw string. While functional, this may impact user experience on legacy clients (Nicotine+, etc.) if the string is parsed literally for display. Testing required.

#### 3. Client Version String
*   **Structure:** Sent during login or peer connection establishment.
*   **Implementation:** Append a capability token to the version string.
    *   *Example:* `"slskdn/1.0+mp"`
*   **Compatibility:** Allows for passive discovery of extension support without altering message structures.

#### 4. Dummy Shared Files
*   **Implementation:** Host virtual files with reserved names (e.g., `__slskdn_caps__`, `__chunkhash_meta__`).
*   **Mechanism:** A request for these files serves as an intent to negotiate or retrieve metadata. The file itself does not need to exist on disk; the request triggers the handshake logic.
*   **Compatibility:** Acts as a standard file request to legacy clients (which will simply return "File not found"), but triggers specific behavior in `slskdn` nodes.

### Experimental / Risky Targets

#### 5. Search Result Item Tags
*   **Structure:** Metadata tags (bitrate, duration, etc.) sent with search results.
*   **Implementation:** Inject custom tags (e.g., `tag["slskdn_hash"] = "sha1:..."`) for content verification or grouping.
*   **Risk:** The central Soulseek server or other nodes may strip unknown tags. Reliability is unproven.

### Non-Viable Methods

*   **Custom Message IDs:** Utilizing IDs beyond the known 16-bit range is unsafe and likely to cause disconnection or errors.
*   **Binary Header Modification:** The file transfer header is a fixed format with no reserved space for extensions.
*   **Message Length Modification:** Altering message lengths or appending trailing bytes violates the protocol and will result in parsing errors.

### Summary

| Target | Method | Risk Profile | Use Case |
| :--- | :--- | :--- | :--- |
| **User Info** | Add `"slskdn_caps"` key | Safe | Capability negotiation |
| **Client Version** | Append `+mp` or `+ext` | Safe | Passive discovery |
| **Dummy File** | Request reserved filename | Mild | Active handshake / Metadata retrieval |
| **Queue Reason** | Structured string overload | Mild | Segmented transfer coordination |
| **Search Tags** | Custom metadata tags | High | Content deduplication (Unreliable) |

## Known Issues / TODO

1.  **No dynamic source discovery**: Pool is built once at start; new sources aren't discovered mid-download.
2.  **Connection overhead**: TTFB is 2-5 seconds per chunk due to Soulseek connection setup. Larger chunks amortize this better.

## Resolved Issues ✅

1.  ~~**FLAC corruption from mixed encodes**~~: Fixed with SHA256 byte verification (not FLAC audio MD5)
2.  ~~**Race condition in speculative execution**~~: Fixed with atomic chunk writes
3.  ~~**Premature failure with 5 retry limit**~~: Fixed with unlimited retries + stuck detection
4.  ~~**80-100% overhead with small chunks**~~: Fixed by increasing to 512KB chunks
5.  ~~**Discovery DB hash population**~~: Phase 2 HashDb now stores verified hashes for future lookups

---

## Implementation Progress

### Phase 1: Capability Discovery ✅ COMPLETE

Implemented peer capability discovery for `slskdn` clients to find each other on the network.

**API Endpoints:**
```
GET  /api/v0/capabilities           → Our capabilities
GET  /api/v0/capabilities/peers     → Known slskdn peers
POST /api/v0/capabilities/parse     → Parse capability strings
```

**Capability Tag Format:**
```
slskdn_caps:v1;dht=1;mesh=1;swarm=1;hashx=1;flacdb=1
```

**Version String Format:**
```
slskdn/1.0.0+dht+mesh+swarm
```

### Phase 2: Local Hash Database ✅ COMPLETE

Implemented SQLite-based hash database for tracking peers, FLAC inventory, and content-addressed hashes.

**Database Tables:**
- `Peers` - Capability tracking, backfill rate limiting
- `FlacInventory` - File inventory with hash verification status
- `HashDb` - Content-addressed hash store (64-bit keys, seq_id for sync)
- `MeshPeerState` - Delta sync state per peer

**API Endpoints:**
```
GET  /api/v0/hashdb/stats                → Database statistics
GET  /api/v0/hashdb/hash/{key}           → Lookup by FLAC key
GET  /api/v0/hashdb/hash/by-size/{size}  → Find hashes by file size
POST /api/v0/hashdb/hash                 → Store verification result
GET  /api/v0/hashdb/sync/since/{seq}     → Delta sync endpoint
POST /api/v0/hashdb/sync/merge           → Receive mesh entries
```

### Phase 3: DHT/Mesh Sync Protocol ✅ COMPLETE

Implemented gossip-based eventual consistency for hash sharing between `slskdn` clients.

**Wire Protocol Messages:**
- `HELLO` - Handshake with latest_seq_id, hash_count
- `REQ_DELTA` - Request entries since sequence ID
- `PUSH_DELTA` - Push entries (paginated)
- `REQ_KEY` / `RESP_KEY` - Lookup specific hash

**API Endpoints:**
```
GET  /api/v0/mesh/stats              → Sync statistics
GET  /api/v0/mesh/peers              → Mesh-capable peers
GET  /api/v0/mesh/delta?sinceSeq=    → Get delta entries
POST /api/v0/mesh/publish            → Publish new hash
POST /api/v0/mesh/sync/{username}    → Trigger sync with peer
```

**Sync Constraints:**
- Min interval: 30 minutes between syncs with same peer
- Max entries: 1000 per sync session
- Max peers: 5 per sync cycle

### Phase 4: Backfill Scheduler ✅ COMPLETE

Implemented conservative header probing for long-tail content with strict rate limits.

**Hard Constraints:**
- Max 2 simultaneous probes globally
- Max 10 probes per peer per day
- 64KB header read limit
- Only runs after 5 minutes idle
- 10 minute interval between cycles

**API Endpoints:**
```
GET  /api/v0/backfill/stats       → Scheduler statistics
GET  /api/v0/backfill/config      → Configuration
GET  /api/v0/backfill/candidates  → Files pending backfill
POST /api/v0/backfill/trigger     → Manually trigger cycle
POST /api/v0/backfill/enable      → Enable/disable scheduler
```

**Features:**
- Background service with idle-time tracking
- Per-peer rate limiting
- Skips slskdn peers (use mesh sync instead)
- Publishes discovered hashes to mesh

### Phase 5: Integration ✅ COMPLETE

Integrated HashDb and MeshSync with existing multi-source download system.

**Hash Resolution Pipeline:**
```
1. Local HashDb lookup (instant cache hit)
2. If miss: verify sources via network download
3. Store best hash in HashDb
4. Publish to mesh for other slskdn clients
```

**ContentVerificationService:**
- `TryGetKnownHashAsync()` - check database before verification
- `StoreVerifiedHashAsync()` - store + publish after verification
- Sets `ExpectedHash` and `WasCached` in result

**MultiSourceDownloadService:**
- `PublishDownloadedHashAsync()` - stores hash after successful download
- Publishes to both HashDb and MeshSync

**Benefits:**
- Instant verification for previously seen files
- Hash propagation across slskdn network
- Reduced network overhead for popular files

---

## Distributed Hash Network & Non-Abusive Backfill Architecture

This section defines the complete architecture for building a distributed FLAC hash database across `slskdn` nodes, enabling instant content verification without redundant header probing.

### Design Principles

- **Prioritize DHT over sniffing**: Never header-probe a peer if a distributed hash lookup can answer the question.
- **Strict rate limiting**: No peer receives more than N header probes per day, regardless of how many files they share.
- **Passive indexing first**: Build inventory from metadata already retrieved during normal browsing/searching.
- **Graceful degradation**: Legacy clients remain fully functional; extensions are invisible to them.

### Core Data Structures

Three local tables form the foundation:

#### 1. Peers Table

| Column | Type | Description |
|--------|------|-------------|
| `peer_id` | TEXT PK | Soulseek username |
| `caps` | INTEGER | Bitfield: `supports_dht`, `supports_hash_exchange`, etc. |
| `last_seen` | INTEGER | Unix timestamp |
| `last_cap_check` | INTEGER | Unix timestamp of last capability probe |

#### 2. FLAC Inventory Table

| Column | Type | Description |
|--------|------|-------------|
| `file_id` | TEXT PK | Stable hash: `sha256(peer_id + path + size)` |
| `peer_id` | TEXT FK | Owner username |
| `path` | TEXT | Full remote path |
| `size` | INTEGER | File size in bytes |
| `discovered_at` | INTEGER | Unix timestamp |
| `hash_status` | TEXT | `none` / `known` / `pending` / `failed` |
| `hash_value` | TEXT | FLAC STREAMINFO MD5 (nullable) |
| `source` | TEXT | `local_scan` / `peer_dht` / `backfill_sniff` |

#### 3. Backfill Scheduler State

| Parameter | Default | Description |
|-----------|---------|-------------|
| `per_peer_daily_budget` | 10 | Max header probes per peer per 24h |
| `global_concurrent_backfills` | 1 | Max simultaneous backfill connections |
| `backfill_run_interval` | 600s | Scheduler wake interval |
| `min_idle_time` | 300s | Required idle time before backfill |
| `max_header_bytes` | 65536 | Bytes to read per probe (64KB) |

---

### Phase 1: Capability Discovery

Detect whether a peer supports DHT/hash exchange before any header probing.

#### Advertisement Methods

**Option A: UserInfo Tag**

Append machine-readable capability string to user description:
```
... | slskdn_caps:v1;dht=1;hashx=1
```

**Option B: Capability File**

Share a virtual file at a known path:
```
__slskdn_caps__.json
```

Contents:
```json
{
  "client": "slskdn",
  "version": "1.0.0",
  "features": ["dht", "hash_exchange", "flac_hash_db"]
}
```

#### Discovery Logic

On any peer interaction (search result, browse, user info):

```
if peer not in peers_table:
    insert(peer_id, caps=0)

detected_caps = parse_userinfo_tag(peer) OR probe_caps_file(peer)

if detected_caps != 0:
    peers_table[peer].caps = detected_caps
    peers_table[peer].last_cap_check = now()
```

**Rule**: If `caps.supports_dht == true`, never schedule header-sniff backfill for this peer.

---

### Phase 2: FLAC Inventory Building

Index FLAC files passively from normal operations.

#### Passive Collection

When fetching any peer's share list:

```
for file in peer_shares:
    if file.extension == ".flac":
        id = stable_hash(peer_id, file.path, file.size)
        if id not in flac_inventory:
            insert(id, peer_id, file.path, file.size, now(), "none", null, "passive")
```

No connections opened. Pure metadata indexing from existing queries.

#### Local Hash Propagation

When completing any FLAC download:

```
local_hash = parse_streaminfo_md5(downloaded_file)

for entry in flac_inventory where size == file.size:
    if verify_match(entry, downloaded_file):  # Optional: fingerprint check
        entry.hash_status = "known"
        entry.hash_value = local_hash
        entry.source = "local_scan"
```

This alone fills a significant portion of the inventory without any probing.

---

### Phase 3: DHT Hash Lookup

Query the distributed network before resorting to header sniffing.

#### Content Key Format

```
flac_key = sha1(normalize(filename) + ":" + str(filesize))
```

Normalization: lowercase, strip path, remove common prefixes/suffixes.

#### Lookup Flow

For any `hash_status == "none"` entry from a non-DHT peer:

```
result = dht_lookup(flac_key)

if result.found:
    entry.hash_status = "known"
    entry.hash_value = result.flac_md5
    entry.source = "peer_dht"
    return

# Entry remains eligible for backfill
```

#### DHT Node Behavior

Each `slskdn` node:
- Joins overlay network on startup
- Stores `flac_key → { md5, duration?, flags }` for known hashes
- Responds to queries for keys in its partition
- Publishes new hashes when discovered locally

Popular releases converge within hours as multiple nodes download and share hashes.

---

### Phase 4: Backfill Scheduler

Last resort: controlled header probing for long-tail content.

#### Hard Constraints

- `MAX_GLOBAL_CONNECTIONS = 1-2`: Never more than 2 simultaneous probes
- `MAX_PER_PEER_PER_DAY = 10`: No peer hit more than 10 times daily
- `MAX_HEADER_BYTES = 64KB`: Stop reading immediately after STREAMINFO
- `MIN_IDLE_TIME = 5min`: Only run when no active transfers
- `RUN_INTERVAL = 10min`: Scheduler wakes every 10 minutes

#### Selection Algorithm

```
if active_backfills >= MAX_GLOBAL_CONNECTIONS:
    return

candidates = SELECT FROM flac_inventory
    WHERE hash_status = "none"
    AND peer_caps[peer_id].supports_dht = false
    AND backfills_today[peer_id] < MAX_PER_PEER_PER_DAY
    ORDER BY discovered_at ASC
    LIMIT 3

for candidate in candidates:
    if eligible(candidate):
        schedule_backfill(candidate)
        break
```

#### Eligibility Rules

- Skip if peer has active upload queue (don't compete with real users)
- Prefer peers already in download history (less likely to complain)
- Randomize selection to avoid hammering same users

#### Backfill Operation

```
backfill_header(peer_id, path, file_id):
    set hash_status = "pending"
    
    # Queue as lowest priority
    send QueueUploadRequest(
        peer_id, 
        path, 
        reason="slskdn:hdr_probe"  # Recognized by other slskdn nodes
    )
    
    await transfer_accepted OR timeout(30s)
    
    if timeout:
        set hash_status = "failed"
        return
    
    buffer = read_bytes(MAX_HEADER_BYTES)
    close_connection()
    
    md5 = parse_flac_streaminfo(buffer)
    
    if md5:
        set hash_status = "known"
        set hash_value = md5
        set source = "backfill_sniff"
        increment backfills_today[peer_id]
        
        # Publish to DHT
        dht_publish(flac_key, md5)
    else:
        set hash_status = "failed"
```

---

### Complete Resolution Pipeline

For any FLAC file from any peer:

```
┌─────────────────────────────────────────────────────────────┐
│ 1. DISCOVERY                                                │
│    - Detect peer capabilities                               │
│    - Index FLAC metadata into inventory (hash_status=none)  │
└─────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────┐
│ 2. HASH RESOLUTION (in priority order)                      │
│                                                             │
│    a) Local download completed? → Compute hash → "known"    │
│                                                             │
│    b) DHT lookup returns result? → "known" (source=dht)     │
│                                                             │
│    c) Remains "none" → Eligible for backfill                │
└─────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────┐
│ 3. BACKFILL (last resort, strict rate limits)               │
│    - Only for non-DHT peers                                 │
│    - Max 10/day per peer                                    │
│    - Max 1-2 concurrent globally                            │
│    - 64KB read, immediate disconnect                        │
│    - Publish result to DHT on success                       │
└─────────────────────────────────────────────────────────────┘
```

### Convergence Behavior

- **Popular content**: Hashes known within hours (many nodes download → DHT fills quickly)
- **Medium popularity**: Days to weeks (fewer downloads, but eventually someone gets it)
- **Long tail**: Months (backfill slowly colors in at sustainable rate)

The key insight: DHT eliminates redundant work. Once *any* `slskdn` node downloads a file, *all* nodes benefit.

---

## Epidemic Mesh Sync Protocol

An alternative to traditional Kademlia-style DHT: **gossip-based eventual consistency** where every extended client participates symmetrically, exchanging hash databases opportunistically during normal Soulseek interactions.

### Design Rationale

Traditional DHT requires:
- Dedicated routing logic
- Always-on supernodes or bootstrap servers
- Explicit "this key lives on these nodes" plumbing

Epidemic mesh requires:
- Every client stores the same data structure
- Pairwise sync when clients interact naturally
- Eventual convergence via random/opportunistic connections

The "DHT-ness" is in the **content** (hash-indexed keyspace), not the topology. The topology is simply "gossip with bounds."

### Local State Per Client

#### FLAC Hash Database

| Column | Type | Description |
|--------|------|-------------|
| `key` | TEXT PK | `sha1(flac_md5 + size)` or similar stable identifier |
| `flac_md5` | BLOB | 16-byte FLAC STREAMINFO MD5 |
| `size` | INTEGER | File size in bytes |
| `meta_flags` | INTEGER | Optional: sample rate, channels, bit depth |
| `first_seen_at` | INTEGER | Unix timestamp |
| `last_updated_at` | INTEGER | Unix timestamp |

#### Peer Sync State

| Column | Type | Description |
|--------|------|-------------|
| `peer_id` | TEXT PK | Soulseek username |
| `caps` | INTEGER | Bitfield: `supports_mesh_sync`, etc. |
| `last_sync_time` | INTEGER | Unix timestamp of last mesh sync |
| `last_seq_seen` | INTEGER | Highest sequence ID received from this peer |

#### Sync Parameters

| Parameter | Default | Description |
|-----------|---------|-------------|
| `MESH_SYNC_INTERVAL_MIN` | 1800s | Minimum seconds between syncs with same peer |
| `MESH_MAX_ENTRIES_PER_SYNC` | 1000 | Maximum entries exchanged per session |
| `MESH_MAX_PEERS_PER_CYCLE` | 5 | Maximum peers to sync with per time window |

### Sequence-Based Delta Sync

Each client maintains a **monotonic sequence counter** for local hash insertions:

```
LocalSeqEntry {
    seq_id        // uint64, monotonically increasing
    flac_key      // hash key
    flac_md5      // the actual hash value
    size          // file size
    meta_flags    // optional metadata
}
```

On each new hash acquisition (download, backfill, or mesh receive):

```
seq_counter += 1
insert FlacEntry(...)
insert LocalSeqEntry(seq_id=seq_counter, ...)
```

Sync becomes "give me everything since seq X" rather than full database comparison.

### Wire Protocol

Four message types over side-channel (phantom file transfer, dedicated TCP, or custom peer message):

#### `MESH_HELLO`

```json
{
    "type": "MESH_HELLO",
    "client_id": "username",
    "proto_version": 1,
    "latest_seq_id": 847291
}
```

Initiates session. Receiver updates `PeerState[sender].last_seq_seen_from_them`.

#### `MESH_REQ_DELTA`

```json
{
    "type": "MESH_REQ_DELTA",
    "since_seq_id": 840000,
    "max_entries": 1000
}
```

Request entries newer than specified sequence.

#### `MESH_PUSH_DELTA`

```json
{
    "type": "MESH_PUSH_DELTA",
    "entries": [
        {"seq_id": 840001, "key": "abc123...", "flac_md5": "def456...", "size": 43586375},
        {"seq_id": 840002, "key": "xyz789...", "flac_md5": "uvw012...", "size": 31063330}
    ]
}
```

Response with requested entries. Receiver upserts and updates `last_seq_seen`.

#### `MESH_REQ_KEY` (Optional)

```json
{
    "type": "MESH_REQ_KEY",
    "flac_key": "abc123..."
}
```

Explicit lookup for a specific key. Response is single entry or null.

### Sync Flow

When client A connects to mesh-capable client B (during search, upload, chat, etc.):

```
A → B: MESH_HELLO { latest_seq_id: 50000 }
B → A: MESH_HELLO { latest_seq_id: 47000 }

// B wants A's newer entries
B → A: MESH_REQ_DELTA { since_seq_id: 47000, max_entries: 1000 }
A → B: MESH_PUSH_DELTA { entries: [...3000 entries...] }

// A wants B's entries (if any)
A → B: MESH_REQ_DELTA { since_seq_id: 45000, max_entries: 1000 }
B → A: MESH_PUSH_DELTA { entries: [...2000 entries...] }

// Session complete, both updated
```

### Bounding Propagation Cost

Per-sync limits prevent flooding:

- `max_entries` caps each direction (e.g., 1000)
- `MESH_SYNC_INTERVAL_MIN` prevents repeated syncs with same peer
- Large databases converge over multiple sessions, not single transfers

Example: 50k entries, 1k per sync = 50 sessions for full alignment. In practice, popular keys propagate rapidly; obscure keys trickle slowly.

### Small-World Neighbor Optimization

Optional enhancement for faster propagation:

1. Each client maintains N semi-sticky "neighbor" peers (random mesh-capable users)
2. Sync more frequently with neighbors
3. Sync opportunistically with random encounters

Creates a small-world graph where:
- New entries propagate quickly via neighbor edges
- Random connections prevent network fragmentation
- No single point of failure

### Integration with Backfill Strategy

Hash resolution order:

1. **Local DB**: Instant lookup
2. **Mesh query**: Send `MESH_REQ_KEY` to N neighbors
3. **Header backfill**: Last resort, only for non-mesh peers, under strict rate limits

New hashes flow into mesh automatically:
- Acquire hash (download, backfill, mesh sync)
- Insert with new `seq_id`
- Propagate via future sync sessions

### Convergence Properties

| Content Type | Convergence Time | Mechanism |
|--------------|------------------|-----------|
| Popular releases | Hours | Many downloads → many sources → rapid mesh spread |
| Medium popularity | Days-weeks | Fewer initial sources, but mesh eventually propagates |
| Long tail | Weeks-months | Backfill discovers, mesh distributes |

No dedicated infrastructure required. Every client contributes equally to network knowledge.

---

## Source Info

1. https://github.com/nicotine-plus/nicotine-plus/blob/master/doc/SLSKPROTOCOL.md
