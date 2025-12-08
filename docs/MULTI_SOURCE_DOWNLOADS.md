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

### 🎉 All Phases Complete!

| Phase | Name | Status |
|-------|------|--------|
| 1 | Capability Discovery | ✅ COMPLETE |
| 2 | Local Hash Database | ✅ COMPLETE |
| 3 | DHT/Mesh Sync Protocol | ✅ COMPLETE |
| 4 | Backfill Scheduler | ✅ COMPLETE |
| 5 | Integration | ✅ COMPLETE |
| 6 | BitTorrent DHT Rendezvous | ✅ COMPLETE |

---

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

### Phase 6: BitTorrent DHT Rendezvous Layer ✅ COMPLETE

Implements a decentralized peer discovery mechanism using BitTorrent DHT as a rendezvous point.

**The Cold Start Problem:**
When a new `slskdn` client starts, it has no mesh neighbors to sync with. Phase 6 solves this by using the public BitTorrent DHT as a "bulletin board" where `slskdn` clients advertise their presence.

**Key Concepts:**
- **Beacons**: Publicly reachable clients that announce to DHT and accept inbound connections
- **Seekers**: NAT'd clients that query DHT and connect outbound to beacons
- **Overlay Port**: Separate TCP port (default 50305) for mesh handshake and sync

**DHT Library: MonoTorrent 3.0.2**

Uses the full MonoTorrent DHT implementation:
- `DhtEngine` for routing table, RPC, and message loop
- `get_peers()` to discover other slskdn clients
- `announce_peer()` for beacons to advertise overlay port
- Bootstrap from public nodes (`router.bittorrent.com`, etc.)
- Saves/restores `dht_nodes.bin` for fast restarts

**Rendezvous Infohashes:**
```
SHA1("slskdn-mesh-v1")         - Primary
SHA1("slskdn-mesh-v1-backup-1") - Backup
SHA1("slskdn-mesh-v1-backup-2") - Backup
```

**Transport Decision:**
The overlay TCP connection established via DHT rendezvous is reused for Phase 3 mesh sync messages. We do NOT:
- Store hashes in the BitTorrent DHT (wrong scale)
- Tunnel mesh traffic over Soulseek (breaks protocol)
- Use HTTP between clients (unnecessary complexity)

**Security Hardening (ALL COMPLETE ✅):**
| Requirement | Implementation |
|-------------|----------------|
| TLS 1.3 mandatory | `MeshOverlayConnection.cs` |
| Length-prefixed framing | `SecureMessageFramer.cs` (4-byte header, 4KB max) |
| Strict validation | `MessageValidator.cs` (regex, bounds, hex-only) |
| Rate limiting | `OverlayRateLimiter.cs` (3/IP, 10 msg/sec) |
| Certificate pinning | `CertificatePinStore.cs` (TOFU model) |
| IP/username blocklist | `OverlayBlocklist.cs` |

**Core Components (ALL COMPLETE ✅):**
- `DhtRendezvousService` - Real MonoTorrent DHT integration
- `MeshOverlayServer` - TLS TCP listener for inbound connections
- `MeshOverlayConnector` - TLS client for outbound connections
- `MeshNeighborRegistry` - Track active mesh peers (max 10)
- `MeshOverlayConnection` - Secure connection wrapper with handshake
- All services registered in `Program.cs`

**API Endpoints:**
```
GET  /api/v0/dht/status            → DHT node status, beacon capability
GET  /api/v0/dht/peers             → Discovered overlay endpoints
POST /api/v0/dht/announce          → Force DHT announce (beacon only)
POST /api/v0/dht/discover          → Force discovery cycle
GET  /api/v0/overlay/connections   → Active mesh connections
GET  /api/v0/overlay/stats         → Server/connector/rate limiter stats
GET  /api/v0/overlay/blocklist     → Blocked IPs and usernames
POST /api/v0/overlay/blocklist/ip  → Block an IP
POST /api/v0/overlay/blocklist/username → Block a username
```

**Future Enhancements:**
- UPnP/STUN for proper NAT detection
- Soulseek username verification (S13)
- Peer diversity checks (S14)

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

## CONNECT_ASSIST: Signaling-First NAT Traversal

A significant portion of Soulseek users cannot receive incoming connections due to NAT, firewalls, or ISP restrictions. These "second-class" users can only download from peers who happen to connect to them first. This section proposes a **signaling-first** architecture where mesh nodes help establish direct peer-to-peer connections without relaying any file data.

### The Core Insight: Signaling vs. Relay

Most NAT traversal solutions jump straight to relaying data through intermediaries. This is expensive, doesn't scale, and raises privacy concerns. The key insight is that **90%+ of "impossible" connections can be established with pure signaling**—exchanging ~200 bytes of address information and timing coordination—after which the transfer proceeds directly between peers.

| Role | What It Does | Bandwidth Cost | Success Rate |
|------|--------------|----------------|--------------|
| **Signaling** | Exchange addresses, coordinate timing | ~200 bytes | 60-95% of NAT cases |
| **Full Relay** | Forward actual file data | 100% of transfer | Last resort only |

A volunteer serving 1000 connection assists per day via signaling uses less bandwidth than relaying a single 100MB file.

### The Problem

```
User A (NAT, no forward) wants to download from User B (also NAT)

A → B: Connection attempt → BLOCKED (B can't accept incoming)
B → A: Reverse connection → BLOCKED (A can't accept incoming)

Result: Transfer impossible. Both users frustrated.
```

The Soulseek server attempts to broker this with "pierce firewall" messages, but it's unreliable and only works if both users are connected to the server at the right moment.

### Solution: Graduated Connection Strategies

Four strategies attempted in order, with signaling-only methods exhausted before any data relay:

```
┌─────────────────────────────────────────────────────────────┐
│  STRATEGY HIERARCHY (signaling-first)                       │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  1. Direct connection      → Both peers try normally        │
│         ↓ fails                                             │
│  2. Connection trigger     → Tell open peer to call back    │
│         ↓ fails               (SIGNALING ONLY: ~100 bytes) │
│  3. Coordinated hole punch → Exchange addresses + timing    │
│         ↓ fails               (SIGNALING ONLY: ~200 bytes) │
│  4. Full relay             → Last resort, actual data flow  │
│                               (FULL BANDWIDTH COST)         │
│                                                             │
│  Steps 1-3 require ZERO data relay.                         │
│  Step 4 is rarely needed.                                   │
└─────────────────────────────────────────────────────────────┘
```

#### Strategy 1: Direct Connection (No Assist Needed)

Both peers attempt normal connection. Works when at least one has an open port.

#### Strategy 2: Connection Trigger (Signaling Only)

When A can accept connections but cannot initiate (asymmetric NAT, corporate proxy, or A's outbound is blocked but inbound works):

```
┌─────────────────────────────────────────────────────────────┐
│  A (NAT) wants file from B (open port, but A can't reach)   │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│   A ──────X──────→ B    (A's outbound blocked/filtered)     │
│                                                             │
│   A ────→ C ────→ B     C tells B: "A wants you to connect" │
│           │             C tells B: "A's address is X.X.X.X" │
│           ↓                                                  │
│   A ←──────────── B     B initiates connection TO A         │
│                                                             │
│   C's job is DONE. Transfer proceeds A ↔ B directly.       │
│   C relayed ~100 bytes of signaling, zero file data.        │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

**Trick for stubborn NATs:** A pre-sends a SYN to B (gets dropped by B's firewall, but creates a NAT mapping on A's side). Then when B connects back, the incoming connection matches A's NAT mapping and is allowed through. From A's perspective, B "happened to connect."

#### Strategy 3: Coordinated Hole Punch (Signaling Only)

When both users are behind NAT, a coordinator facilitates simultaneous connection attempts:

```
┌─────────────────────────────────────────────────────────────┐
│  Rendezvous / Address Exchange                              │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│   A ────outbound────→ C ←────outbound──── B                 │
│                       │                                      │
│   C now knows:        │                                      │
│     A's public = 1.2.3.4:54321  (from A's connection)       │
│     B's public = 5.6.7.8:12345  (from B's connection)       │
│                                                             │
│   C → A: "B is at 5.6.7.8:12345"                            │
│   C → B: "A is at 1.2.3.4:54321"                            │
│   C → both: "Send SYN to each other at T+100ms"             │
│                                                             │
└─────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────┐
│  Simultaneous TCP Open (Hole Punch)                         │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│   At synchronized moment:                                   │
│     A ──SYN──→          ←──SYN── B                          │
│                                                             │
│   Both NATs see outgoing packet → create mapping            │
│   Incoming SYN from other side matches mapping              │
│   Direct TCP connection established!                         │
│                                                             │
│   C's job is DONE. Transfer proceeds A ↔ B directly.       │
│   C relayed ~200 bytes of signaling, zero file data.        │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

This is exactly how WebRTC's ICE/STUN works. Success rates by NAT type:

| NAT Type | Hole Punch Success |
|----------|-------------------|
| Full Cone | ~95% |
| Restricted Cone | ~80% |
| Port Restricted | ~60% |
| Symmetric | ~10% |

Even 60% success helps users who currently get 0%.

#### Strategy 4: Full Data Relay (Last Resort)

Only when signaling strategies fail—typically symmetric NAT on both sides, or extremely restrictive firewalls:

```
┌─────────────────────────────────────────────────────────────┐
│  Full Relay (rare, expensive)                               │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│   A (NAT) ←────→ C (volunteer relay) ←────→ B (NAT)         │
│                                                             │
│   A connects OUT to C                                       │
│   B connects OUT to C                                       │
│   C bridges the streams                                     │
│                                                             │
│   File data: B → C → A                                      │
│   C pays full bandwidth cost.                               │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

This should be needed for <10% of connections that fail all signaling methods.

### Why This Architecture Scales

| Scenario | Old Approach (Relay Everything) | Signaling-First |
|----------|--------------------------------|-----------------|
| 1000 assists/day | 1000 × avg file size = massive bandwidth | 1000 × 200 bytes = 200KB total |
| Volunteer requirements | Few volunteers, high cost | Many volunteers, negligible cost |
| Privacy | Relay sees all data | Relay sees only addresses |
| Bottleneck | Relay bandwidth | None (direct transfers) |

### Wire Protocol

#### Signaling Messages (Strategies 1-3)

```
ASSIST_REQUEST
    requester: string      // Username requesting help
    target: string         // Username they want to reach
    reason: string         // "cannot_connect" | "timeout" | "rejected"

ASSIST_OFFER
    helper: string         // Username offering to help
    methods: string[]      // ["trigger", "hole_punch", "relay"] in preference order
    capabilities:
        can_coordinate: bool   // Can perform hole punch timing
        can_relay: bool        // Willing to relay if signaling fails
        relay_limit: int       // Max bytes willing to relay (if can_relay)

ADDRESS_INFO
    username: string
    public_addr: string    // IP:port as seen from outside NAT
    nat_type: string       // "full_cone" | "restricted" | "port_restricted" | "symmetric" | "unknown"

HOLE_PUNCH_COORDINATE
    peer_a: { user: string, public_addr: string }
    peer_b: { user: string, public_addr: string }
    sync_time: int         // Unix milliseconds - both send SYN at this moment

CONNECTION_TRIGGER
    target: string         // Username to notify
    requester_addr: string // Where requester can receive connections
    pre_syn_sent: bool     // Whether requester pre-sent SYN to create NAT mapping
```

#### Relay Messages (Strategy 4 Only)

```
RELAY_OFFER
    relay: string          // Relay node username
    session_id: string     // UUID for this relay session
    relay_addr: string     // IP:port to connect to
    expires: int           // Seconds until offer expires
    max_bytes: int         // Maximum bytes this session will relay

RELAY_CONNECT
    session_id: string     // From RELAY_OFFER
    role: string           // "sender" | "receiver"
```

### UserInfo Advertisement

Capabilities advertised in standard tag format:

```
[SLSKDN:v1]
[SLSKDN:MESH:YES]
[SLSKDN:ASSIST:SIGNAL]         # Will help with signaling/coordination
[SLSKDN:ASSIST:RELAY:100MB]    # OPTIONAL: Also willing to relay up to 100MB
[SLSKDN:NAT:FULL_CONE]         # Self-detected NAT type (helps matchmaking)
```

Fields:
- `ASSIST:SIGNAL` - Core capability: will forward triggers and coordinate hole punches
- `ASSIST:RELAY:<limit>` - Optional: will also relay data if signaling fails
- `NAT:<type>` - Self-reported NAT type for hole punch success prediction

Most volunteers should offer `SIGNAL` only. Relay is opt-in for those with bandwidth to spare.

### Incentive Model

Karma reflects actual contribution:

| Action | Karma Earned | Rationale |
|--------|--------------|-----------|
| Successful signaling assist | +1 | Low cost, high value |
| Hole punch coordination | +2 | Slightly more complex |
| Relay 10MB for another user | +1 | High cost, reserved for when needed |

| Benefit | Karma Required |
|---------|----------------|
| Priority in swarm chunk allocation | 10+ |
| Relay service from volunteers | 5+ |
| Extended mesh sync batches | 0 (free) |

Signaling is cheap enough that karma requirements are easily met by participating.

### Privacy Analysis

| Strategy | What Helper Sees | Privacy Impact |
|----------|------------------|----------------|
| Connection Trigger | "A wants to reach B" | Minimal - same as seeing search results |
| Hole Punch | A and B's public IP:port | Moderate - but no content visible |
| Full Relay | All transferred bytes | High - but opt-in and rate-limited |

Signaling strategies (1-3) reveal only connection intent, not content. Full relay (4) is:
- Opt-in for the relay volunteer
- Rate-limited per session
- Optionally E2E encrypted (A and B negotiate key, relay sees ciphertext)

### Complete Assist Flow

```
┌─────────────────────────────────────────────────────────────┐
│ 1. DETECTION                                                │
│    A attempts direct connection to B → fails                │
│    A broadcasts ASSIST_REQUEST to mesh neighbors            │
└─────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────┐
│ 2. HELPER DISCOVERY                                         │
│    Mesh propagates request                                  │
│    C responds with ASSIST_OFFER { methods: [...] }          │
└─────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────┐
│ 3. SIGNALING ATTEMPTS (in order, all zero-data-cost)        │
│                                                             │
│    a) Connection trigger                                    │
│       A pre-sends SYN to B (creates NAT mapping)            │
│       C tells B: "Connect to A at X.X.X.X:Y"                │
│       B connects → SUCCESS? Done. Transfer directly.        │
│                                                             │
│    b) Coordinated hole punch                                │
│       C collects A and B's public addresses                 │
│       C sends HOLE_PUNCH_COORDINATE with sync time          │
│       A and B both send SYN at T → SUCCESS? Done.           │
│                                                             │
└─────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────┐
│ 4. RELAY FALLBACK (only if all signaling failed)            │
│                                                             │
│    C (if ASSIST:RELAY capable) sends RELAY_OFFER            │
│    A and B both connect to C's relay port                   │
│    C bridges streams until transfer complete                │
│                                                             │
└─────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────┐
│ 5. TRANSFER COMPLETE                                        │
│    Karma awarded based on method used                       │
│    NAT type info cached for future predictions              │
└─────────────────────────────────────────────────────────────┘
```

### Expected Success Rates

Based on real-world NAT distribution:

| Connection Scenario | Direct | + Trigger | + Hole Punch | + Relay |
|--------------------|--------|-----------|--------------|---------|
| A open, B open | 100% | - | - | - |
| A NAT, B open | 50% | 95% | - | - |
| A open, B NAT | 50% | 95% | - | - |
| A NAT (cone), B NAT (cone) | 0% | 30% | 85% | 100% |
| A NAT (symmetric), B NAT (symmetric) | 0% | 0% | 10% | 100% |

Most Soulseek users have cone-type NATs. The signaling-first approach should resolve 80-90% of currently-failing transfers without any data relay.

### Implementation Phases

| Phase | Scope | Bandwidth Cost | Dependencies |
|-------|-------|----------------|--------------|
| 1 | Connection triggers | Zero | Mesh sync |
| 2 | Hole punch coordination | Zero | NAT type detection, timing sync |
| 3 | Volunteer relay (opt-in) | Per-transfer | Karma system, session limits |
| 4 | E2E encryption for relay | Zero | Key exchange |

Phases 1-2 provide massive value at zero bandwidth cost. Phase 3 catches the remaining edge cases. Phase 4 adds privacy for relay transfers.

### Why Not Just Use STUN/TURN?

We could integrate standard STUN/TURN infrastructure, but:

1. **STUN servers are external dependencies** - Our mesh is self-hosted
2. **TURN servers are expensive** - Someone has to pay for relay bandwidth
3. **We already have the mesh** - Why add another overlay?

The signaling-first approach achieves STUN/ICE-equivalent functionality using infrastructure we're already building for hash sync.

### Interoperability with Legacy Clients

A common question: can we help non-slskdn clients connect to each other?

| Connection Scenario | Support Level | Mechanism |
|--------------------|---------------|-----------|
| **slskdn ↔ slskdn** | Full | Complete signaling protocol, hole punch, relay |
| **slskdn ↔ legacy** | Partial | Our side prepares NAT + standard pierce firewall |
| **legacy ↔ legacy** | None | Cannot inject into their connection flow |

**Why the limitation exists:**

Connection triggers and hole punch coordination require the receiving client to:
1. Understand our mesh signaling messages
2. Act on timing coordination
3. Participate in the protocol

Legacy clients (Nicotine+, SoulseekQt, etc.) don't speak our extended protocol. We can't make them do things they weren't programmed to do.

**What we CAN do for slskdn → legacy transfers:**

```
┌─────────────────────────────────────────────────────────────┐
│  slskdn user A wants to download from legacy user B         │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  1. A pre-sends SYN to B (creates NAT mapping on A's side)  │
│  2. A requests standard "pierce firewall" from Soulseek     │
│  3. Server sends PierceFirewall to B (legacy understands)   │
│  4. B connects back to A                                    │
│  5. A's pre-created NAT mapping accepts the connection!     │
│                                                             │
│  Result: Higher success rate than pure legacy behavior.     │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

This is a unilateral improvement—slskdn users benefit even when the other side is legacy.

**For legacy ↔ legacy:** We simply cannot help. Both clients would need to run our code to participate in signaling. This is a fundamental limitation, not a design choice.

**The long game:** As slskdn adoption grows, the percentage of connections that can use full signaling increases. Early adopters benefit immediately when connecting to each other, and partially when connecting to legacy clients.

---

## Project Vision & Roadmap

### Philosophy

slskdn takes the position that **good features belong in the client, not in external scripts**. The upstream slskd project intentionally stays minimal, directing users to implement advanced features via the API. slskdn takes the opposite approach: if a feature improves the user experience and can be implemented responsibly, it should be built-in.

This experimental branch pushes that philosophy further into **protocol enhancement territory**—not breaking compatibility, but extending capability for clients that opt in.

### Core Principles

1. **Never break legacy compatibility** - Standard Soulseek clients must remain fully functional
2. **Graceful degradation** - Extended features work when available, fall back silently when not
3. **Network citizenship** - No feature should abuse the network or burden other users
4. **Privacy by default** - Signaling over relay, encryption where data flows through third parties
5. **Decentralization** - No dedicated infrastructure required; every client contributes equally

### Feature Roadmap

#### Tier 1: Stable (Ready for Main Branch)

| Feature | Status | Description |
|---------|--------|-------------|
| Auto-replace stuck downloads | ✅ Shipped | Automatic search and replacement for failed transfers |
| Wishlist/background search | ✅ Shipped | Persistent searches that run automatically |
| Smart source ranking | ✅ Shipped | Intelligent sorting by speed, queue, history |
| User notes & ratings | ✅ Shipped | Persistent annotations on users |
| Multi-destination downloads | ✅ Shipped | Route downloads to different folders |
| Ntfy/Pushover notifications | ✅ Shipped | Mobile push for messages and mentions |

#### Tier 2: Experimental (This Branch)

| Feature | Status | Description |
|---------|--------|-------------|
| Multi-source swarm downloads | ✅ Working | Download chunks from multiple peers simultaneously |
| Source discovery service | ✅ Working | Background indexing of available sources |
| Content verification (SHA256) | ✅ Working | Verify chunk integrity before assembly |
| Partial download tracking | ✅ Working | Remember which peers support `startOffset` |

#### Tier 3: Protocol Extensions (In Development)

| Feature | Status | Description |
|---------|--------|-------------|
| Capability advertisement | ✅ Complete | UserInfo tags for feature discovery |
| Local hash database | ✅ Complete | SQLite store for verified FLAC hashes |
| Epidemic mesh sync | ✅ Complete | Gossip-based hash exchange between slskdn nodes |
| BitTorrent DHT rendezvous | ✅ Complete | Decentralized peer discovery for mesh bootstrap |
| Backfill scheduler | ✅ Complete | Conservative header probing for long-tail content |

#### Tier 4: Network Enhancement (Proposed)

| Feature | Status | Description |
|---------|--------|-------------|
| CONNECT_ASSIST signaling | 📋 Designed | Zero-bandwidth connection brokering |
| Connection triggers | 📋 Designed | Tell peers to initiate reverse connections |
| Hole punch coordination | 📋 Designed | Synchronized NAT traversal |
| Volunteer relay network | 📋 Designed | Last-resort data relay for symmetric NAT |
| Karma/reputation system | 📋 Designed | Incentivize network contribution |

#### Tier 5: Future Possibilities

| Feature | Status | Description |
|---------|--------|-------------|
| E2E encryption for relay | 💭 Concept | Privacy layer when relay is required |
| Multi-hop relay routing | 💭 Concept | Tor-lite for sensitive transfers |
| Cross-client capability negotiation | 💭 Concept | Standardized extension discovery |
| FLAC fingerprint matching | 💭 Concept | Content-based deduplication beyond size matching |

### Success Metrics

How we'll know the experimental features are working:

| Metric | Target | Measurement |
|--------|--------|-------------|
| Swarm download success rate | >90% | Completed multi-source transfers / attempts |
| Hash cache hit rate | >50% | Lookups resolved from local DB or mesh |
| Mesh peer count | >10 per node | Average connected mesh neighbors |
| Signaling success rate | >80% | Connections established without relay |
| Network abuse reports | 0 | Complaints from other users or server operators |

### Contribution to Broader Ecosystem

Features that prove stable and valuable may be:

1. **Proposed as Soulseek protocol extensions** - With buy-in from server operators
2. **Submitted as PRs to upstream slskd** - For features that fit their philosophy
3. **Documented for other client implementations** - Nicotine+, SoulseekQt, etc.

The goal is not to fragment the network, but to prove out ideas that could benefit everyone. slskdn serves as a **proving ground** for features that are too experimental for conservative clients but too valuable to ignore.

---

## Source Info

1. https://github.com/nicotine-plus/nicotine-plus/blob/master/doc/SLSKPROTOCOL.md
