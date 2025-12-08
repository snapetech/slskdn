# slskdn Multi-Source & DHT Implementation Roadmap

## Executive Summary

This document maps out the complete implementation path for building out the multi-source download functionality with a distributed hash table (DHT) / epidemic mesh sync protocol. The goal is to create a network of `slskdn` clients that can share FLAC hash information, enabling instant content verification without redundant header probing.

**Branch:** `experimental/multi-source-swarm`

---

## Current State Analysis

### Already Implemented ✅

| Component | Location | Status |
|-----------|----------|--------|
| Multi-source chunked downloads (SWARM mode) | `src/slskd/Transfers/MultiSource/MultiSourceDownloadService.cs` | ✅ Working |
| Content verification (SHA256 32KB) | `src/slskd/Transfers/MultiSource/ContentVerificationService.cs` | ✅ Working |
| FLAC STREAMINFO parser | `src/slskd/Transfers/MultiSource/FlacStreamInfo.cs` | ✅ Working |
| Source discovery service | `src/slskd/Transfers/MultiSource/Discovery/SourceDiscoveryService.cs` | ✅ Working |
| API endpoints for swarm downloads | `src/slskd/Transfers/MultiSource/API/MultiSourceController.cs` | ✅ Working |
| LimitedWriteStream for chunk downloads | `ContentVerificationService.cs` | ✅ Working |
| **Phase 1: Capability Discovery** | `src/slskd/Capabilities/` | ✅ **COMPLETE** |
| **Phase 2: Local Hash Database** | `src/slskd/HashDb/` | ✅ **COMPLETE** |

### Phase 1 Components (COMPLETE) ✅

| Component | Location | Description |
|-----------|----------|-------------|
| ICapabilityService | `src/slskd/Capabilities/ICapabilityService.cs` | Interface with PeerCapabilityFlags |
| CapabilityService | `src/slskd/Capabilities/CapabilityService.cs` | UserInfo tag parsing, version string parsing |
| CapabilitiesController | `src/slskd/Capabilities/API/CapabilitiesController.cs` | REST API endpoints |

### Phase 2 Components (COMPLETE) ✅

| Component | Location | Description |
|-----------|----------|-------------|
| IHashDbService | `src/slskd/HashDb/IHashDbService.cs` | Full interface with mesh sync support |
| HashDbService | `src/slskd/HashDb/HashDbService.cs` | SQLite implementation |
| HashDbController | `src/slskd/HashDb/API/HashDbController.cs` | REST API endpoints |
| Peer model | `src/slskd/HashDb/Models/Peer.cs` | Capability & backfill tracking |
| FlacInventoryEntry | `src/slskd/HashDb/Models/FlacInventoryEntry.cs` | File inventory with hash status |
| HashDbEntry | `src/slskd/HashDb/Models/HashDbEntry.cs` | Content-addressed DHT entry |

### Not Yet Implemented ❌

| Feature | Priority | Complexity |
|---------|----------|------------|
| **Phase 6: BitTorrent DHT Rendezvous** | HIGH | High |
| Capability File sharing (`__slskdn_caps__`) | MEDIUM | Low |
| Queue Reason field overloading | LOW | Low |
| Small-world neighbor optimization | LOW | Medium |
| Web UI for DHT status/hash database | LOW | Medium |

---

## Phase 1: Protocol Extensions - Capability Discovery ✅ COMPLETE

> **Status:** Implemented and tested. Commit `2847b35d`

### 1.1 UserInfo Tag Advertisement ✅

**Implementation:** `src/slskd/Capabilities/`

Capability tag format:
```
slskdn_caps:v1;dht=1;mesh=1;swarm=1;hashx=1;flacdb=1
```

### 1.2 Client Version String Extension ✅

Version string format:
```
slskdn/1.0.0+dht+mesh+swarm
```

### 1.3 API Endpoints ✅

| Endpoint | Description |
|----------|-------------|
| `GET /api/v0/capabilities` | Our capabilities (version, tag, JSON) |
| `GET /api/v0/capabilities/peers` | All known slskdn peers |
| `GET /api/v0/capabilities/peers/{username}` | Specific peer capabilities |
| `GET /api/v0/capabilities/mesh-peers` | Mesh-capable peers |
| `POST /api/v0/capabilities/parse` | Parse description/version strings |

### 1.4 PeerCapabilityFlags ✅

```csharp
[Flags]
public enum PeerCapabilityFlags
{
    None = 0,
    SupportsDHT = 1 << 0,
    SupportsHashExchange = 1 << 1,
    SupportsPartialDownload = 1 << 2,
    SupportsMeshSync = 1 << 3,
    SupportsFlacHashDb = 1 << 4,
    SupportsSwarm = 1 << 5,
}
```

### Files Created:
- `src/slskd/Capabilities/ICapabilityService.cs` ✅
- `src/slskd/Capabilities/CapabilityService.cs` ✅
- `src/slskd/Capabilities/API/CapabilitiesController.cs` ✅
- `src/slskd/Program.cs` (MODIFIED - service registration) ✅

---

## Phase 2: Local Hash Database - Enhanced Schema ✅ COMPLETE

> **Status:** Implemented and tested. Commit `b790d696`

### 2.1 Database Schema ✅

SQLite database at `{AppDirectory}/hashdb.db` with 4 tables:

```sql
-- Peer tracking with capabilities
CREATE TABLE Peers (
    peer_id TEXT PRIMARY KEY,
    caps INTEGER DEFAULT 0,            -- PeerCapabilityFlags bitfield
    client_version TEXT,
    last_seen INTEGER NOT NULL,
    last_cap_check INTEGER,
    backfills_today INTEGER DEFAULT 0,
    backfill_reset_date INTEGER
);

-- FLAC file inventory with hash tracking
CREATE TABLE FlacInventory (
    file_id TEXT PRIMARY KEY,          -- sha256(peer_id + path + size)
    peer_id TEXT NOT NULL,
    path TEXT NOT NULL,
    size INTEGER NOT NULL,
    discovered_at INTEGER NOT NULL,
    hash_status TEXT DEFAULT 'none',   -- 'none'/'known'/'pending'/'failed'
    hash_value TEXT,                   -- SHA256 of first 32KB
    hash_source TEXT,                  -- 'local_scan'/'peer_dht'/'backfill_sniff'/'mesh_sync'
    flac_audio_md5 TEXT,               -- FLAC STREAMINFO MD5 (reference only)
    sample_rate INTEGER,
    channels INTEGER,
    bit_depth INTEGER,
    duration_samples INTEGER
);

-- DHT/Mesh hash database (content-addressed)
CREATE TABLE HashDb (
    flac_key TEXT PRIMARY KEY,         -- 64-bit truncated hash (16 hex chars)
    byte_hash TEXT NOT NULL,           -- SHA256 of first 32KB
    size INTEGER NOT NULL,
    meta_flags INTEGER,                -- Packed sample_rate/channels/bit_depth
    first_seen_at INTEGER NOT NULL,
    last_updated_at INTEGER NOT NULL,
    seq_id INTEGER,                    -- Monotonic sequence for delta sync
    use_count INTEGER DEFAULT 1
);

-- Mesh sync state per peer
CREATE TABLE MeshPeerState (
    peer_id TEXT PRIMARY KEY,
    last_sync_time INTEGER,
    last_seq_seen INTEGER DEFAULT 0
);
```

### 2.2 API Endpoints ✅

| Endpoint | Description |
|----------|-------------|
| `GET /api/v0/hashdb/stats` | Database statistics |
| `GET /api/v0/hashdb/hash/{flacKey}` | Lookup hash by key |
| `GET /api/v0/hashdb/hash/by-size/{size}` | Find all hashes for a file size |
| `GET /api/v0/hashdb/key?filename=&size=` | Generate FLAC key |
| `POST /api/v0/hashdb/hash` | Store verification result |
| `GET /api/v0/hashdb/inventory/by-size/{size}` | Inventory lookup |
| `GET /api/v0/hashdb/inventory/unhashed` | Files pending verification |
| `GET /api/v0/hashdb/backfill/candidates` | Backfill candidates |
| `GET /api/v0/hashdb/peers` | Tracked peers |
| `GET /api/v0/hashdb/sync/since/{seq}` | Delta sync endpoint |
| `POST /api/v0/hashdb/sync/merge` | Receive mesh entries |

### 2.3 Key Features ✅

- **64-bit truncated FLAC keys** for compact storage
- **Monotonic seq_id** for efficient delta sync
- **Per-peer backfill rate limiting** (50/day max)
- **Conflict detection** on mesh merge
- **use_count tracking** for pruning unused entries

### Files Created:
- `src/slskd/HashDb/IHashDbService.cs` ✅
- `src/slskd/HashDb/HashDbService.cs` ✅
- `src/slskd/HashDb/API/HashDbController.cs` ✅
- `src/slskd/HashDb/Models/Peer.cs` ✅
- `src/slskd/HashDb/Models/FlacInventoryEntry.cs` ✅
- `src/slskd/HashDb/Models/HashDbEntry.cs` ✅
- `src/slskd/Program.cs` (MODIFIED - service registration) ✅

---

## Phase 3: DHT/Epidemic Mesh Sync Protocol ✅ COMPLETE

> **Status:** Implemented and tested. Commit `fba4ccab`

### 3.1 Wire Protocol Messages ✅

| Message | Purpose |
|---------|---------|
| `HELLO` | Handshake with latest_seq_id, hash_count |
| `REQ_DELTA` | Request entries since sequence ID |
| `PUSH_DELTA` | Push entries (paginated with has_more) |
| `REQ_KEY` | Lookup specific hash key |
| `RESP_KEY` | Key lookup response |
| `ACK` | Acknowledge receipt with merge count |

### 3.2 API Endpoints ✅

| Endpoint | Description |
|----------|-------------|
| `GET /api/v0/mesh/stats` | Sync statistics |
| `GET /api/v0/mesh/peers` | Mesh-capable peers |
| `GET /api/v0/mesh/hello` | Generate HELLO message |
| `GET /api/v0/mesh/delta` | Get delta entries |
| `GET /api/v0/mesh/lookup/{key}` | Lookup hash |
| `POST /api/v0/mesh/publish` | Publish new hash |
| `POST /api/v0/mesh/sync/{username}` | Trigger sync |
| `POST /api/v0/mesh/message` | Handle incoming message |
| `POST /api/v0/mesh/merge` | Merge entries from peer |

### 3.3 Sync Constraints ✅

| Parameter | Value | Description |
|-----------|-------|-------------|
| `MESH_SYNC_INTERVAL_MIN` | 1800s | Min seconds between syncs with same peer |
| `MESH_MAX_ENTRIES_PER_SYNC` | 1000 | Max entries exchanged per session |
| `MESH_MAX_PEERS_PER_CYCLE` | 5 | Max peers to sync with per time window |

### Files Created:
- `src/slskd/Mesh/IMeshSyncService.cs` ✅
- `src/slskd/Mesh/MeshSyncService.cs` ✅
- `src/slskd/Mesh/Messages/MeshMessages.cs` ✅
- `src/slskd/Mesh/API/MeshController.cs` ✅

---

## Phase 4: Backfill Scheduler Service ✅ COMPLETE

> **Status:** Implemented and tested. Commit `df3f605f`

### 4.1 Hard Constraints ✅

| Constraint | Value | Description |
|------------|-------|-------------|
| `MAX_GLOBAL_CONNECTIONS` | 2 | Max simultaneous probes |
| `MAX_PER_PEER_PER_DAY` | 10 | Max probes per peer daily |
| `MAX_HEADER_BYTES` | 64KB | Read limit per probe |
| `MIN_IDLE_TIME` | 5min | Idle time before running |
| `RUN_INTERVAL` | 10min | Cycle interval |
| `TRANSFER_TIMEOUT` | 30s | Timeout per probe |

### 4.2 API Endpoints ✅

| Endpoint | Description |
|----------|-------------|
| `GET /api/v0/backfill/stats` | Scheduler statistics |
| `GET /api/v0/backfill/config` | Configuration |
| `GET /api/v0/backfill/candidates` | Files pending backfill |
| `POST /api/v0/backfill/enable` | Enable/disable scheduler |
| `POST /api/v0/backfill/trigger` | Manually trigger cycle |
| `POST /api/v0/backfill/file` | Backfill specific file |
| `POST /api/v0/backfill/idle` | Report system idle |
| `POST /api/v0/backfill/busy` | Report system busy |

### 4.3 Features ✅

- Background service with configurable interval
- Idle-time tracking before running cycles
- Per-peer rate limiting (backfills_today counter)
- Semaphore for concurrent connection limit
- FLAC header parsing → SHA256 hash
- Integration with HashDb and MeshSync
- Skips slskdn peers (use mesh sync instead)

### Files Created:
- `src/slskd/Backfill/IBackfillSchedulerService.cs` ✅
- `src/slskd/Backfill/BackfillSchedulerService.cs` ✅
- `src/slskd/Backfill/API/BackfillController.cs` ✅

**Note:** Actual Soulseek download integration pending - logic and rate limiting fully implemented.

---

## Phase 5: Integration with Existing Multi-Source System ✅ COMPLETE

> **Status:** Implemented and tested. Commit `e3f069bf`

### 5.1 Hash Resolution Pipeline ✅

```
1. Local HashDb lookup (instant)
   → If found: return hash, set ExpectedHash

2. If miss: verify sources via network download
   → Download 32KB from each source
   → Compute SHA256 hash

3. Store best hash in HashDb

4. Publish to mesh for other slskdn clients
```

### 5.2 Files Modified ✅

- `src/slskd/Transfers/MultiSource/ContentVerificationService.cs`
  - Inject `IHashDbService`, `IMeshSyncService`
  - `TryGetKnownHashAsync()` - lookup hash from database
  - `StoreVerifiedHashAsync()` - store and publish after verification
  - `VerifySourcesAsync` checks database first

- `src/slskd/Transfers/MultiSource/IContentVerificationService.cs`
  - Added `ExpectedHash` property to result
  - Added `WasCached` property

- `src/slskd/Transfers/MultiSource/MultiSourceDownloadService.cs`
  - Inject `IHashDbService`, `IMeshSyncService`
  - `PublishDownloadedHashAsync()` - stores hash after successful download

### 5.3 API Endpoints (All Complete) ✅

| Endpoint | Description | Phase |
|----------|-------------|-------|
| `GET /api/v0/capabilities/*` | Capability discovery | Phase 1 |
| `GET /api/v0/hashdb/*` | Hash database operations | Phase 2 |
| `GET /api/v0/mesh/*` | Mesh sync operations | Phase 3 |
| `GET /api/v0/backfill/*` | Backfill scheduler | Phase 4 |

---

## Phase 6: BitTorrent DHT Rendezvous Layer ⬜ PLANNED

> **Status:** Designed, implementation pending

### 6.0 Overview & Motivation

**The Cold Start Problem:** When a new slskdn client starts up, or when a client has been offline for a while, it may have no mesh neighbors. Without mesh neighbors, it can't sync FLAC hashes, coordinate multi-source downloads, or benefit from the distributed network. How does it find other slskdn clients?

**The Solution:** Use the **BitTorrent mainline DHT** as a decentralized "bulletin board" where slskdn clients advertise their presence and discover each other. This is purely a **rendezvous mechanism** - we don't store any file hashes or content in the DHT.

### 6.1 Key Concepts

| Concept | Description |
|---------|-------------|
| **Rendezvous Infohash** | A well-known SHA-1 hash derived from a magic string (e.g., `SHA1("slskdn-mesh-v1")`) that all slskdn clients agree on |
| **Beacon** | An slskdn client that is publicly reachable, announces itself to the DHT, and accepts inbound overlay connections |
| **Seeker** | An slskdn client behind NAT/firewall that queries the DHT and makes outbound connections to beacons |
| **Overlay Port** | A separate TCP port (not Soulseek) where slskdn clients perform mesh handshakes and sync |

### 6.2 How It Works

```
┌─────────────────────────────────────────────────────────────────────────┐
│                      BITTORRENT DHT (PUBLIC INFRASTRUCTURE)             │
│                                                                         │
│  ┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐      │
│  │  DHT Node       │    │  DHT Node       │    │  DHT Node       │      │
│  │  (bootstrap)    │────│                 │────│                 │      │
│  └─────────────────┘    └─────────────────┘    └─────────────────┘      │
│                                 │                                        │
│  Infohash: SHA1("slskdn-mesh-v1") = 0x1a2b3c4d...                       │
│                                 │                                        │
│           ┌─────────────────────┴─────────────────────┐                  │
│           │                                           │                  │
│      announce_peer()                            get_peers()              │
│           │                                           │                  │
└───────────┼───────────────────────────────────────────┼──────────────────┘
            │                                           │
            ▼                                           ▼
    ┌───────────────┐                           ┌───────────────┐
    │   BEACON      │◄─────TCP overlay────────────│   SEEKER      │
    │   (slskdn)    │     connect + handshake     │   (slskdn)    │
    │               │                             │               │
    │ - Public IP   │                             │ - Behind NAT  │
    │ - Overlay:50305│                            │ - Can connect │
    │ - DHT announce │                            │   outbound    │
    └───────────────┘                           └───────────────┘
            │                                           │
            │         Once connected, both use         │
            │         existing mesh sync protocol      │
            │                                           │
            ▼                                           ▼
    ┌───────────────────────────────────────────────────────────┐
    │              MESH SYNC (EXISTING PHASE 3)                 │
    │  HELLO → REQ_DELTA → PUSH_DELTA → FLAC hashes shared     │
    └───────────────────────────────────────────────────────────┘
```

### 6.3 Overlay Handshake Protocol

The overlay connection uses a simple JSON-over-TCP protocol:

**Client → Beacon (mesh_hello):**
```json
{
  "magic": "SLSKDNM1",
  "type": "mesh_hello",
  "version": 1,
  "username": "<SoulseekUsername>",
  "features": ["mesh", "flac_hash", "multipart", "swarm"],
  "soulseek_ports": {
    "peer": 50300,
    "file": 50301
  }
}
```

**Beacon → Client (mesh_hello_ack):**
```json
{
  "magic": "SLSKDNM1",
  "type": "mesh_hello_ack",
  "version": 1,
  "username": "<TheirSoulseekUsername>",
  "features": ["mesh", "flac_hash", "multipart", "swarm"],
  "soulseek_ports": {
    "peer": 50300,
    "file": 50301
  }
}
```

**Validation:**
- `magic` MUST equal `"SLSKDNM1"`
- `type` MUST be `"mesh_hello"` or `"mesh_hello_ack"`
- Reject if payload > 4KB or invalid JSON

### 6.4 Configuration

```yaml
# appsettings.yml
mesh:
  overlay:
    enabled: true
    port: 50305                    # Overlay TCP listener port
  dht:
    enabled: true
    bootstrap_nodes:              # Standard BitTorrent DHT bootstrap
      - "router.bittorrent.com:6881"
      - "dht.transmissionbt.com:6881"
      - "router.utorrent.com:6881"
    announce_interval: 900        # 15 minutes
    discovery_interval: 600       # 10 minutes
    min_neighbors: 3              # Trigger discovery below this
    rendezvous_keys:
      - "slskdn-mesh-v1"
      - "slskdn-mesh-v1-backup-1"
```

### 6.5 Components to Implement

#### Component 1: DhtRendezvousService

```csharp
public interface IDhtRendezvousService
{
    /// <summary>True if we can accept inbound connections (public IP).</summary>
    bool IsBeaconCapable { get; }
    
    /// <summary>Start DHT node and begin announce/discovery loops.</summary>
    Task StartAsync(CancellationToken ct);
    
    /// <summary>Stop DHT node.</summary>
    Task StopAsync();
    
    /// <summary>Get current list of discovered peer endpoints.</summary>
    IReadOnlyList<IPEndPoint> GetDiscoveredPeers();
    
    /// <summary>Force a discovery cycle.</summary>
    Task DiscoverPeersAsync(CancellationToken ct);
}
```

**Responsibilities:**
1. Initialize DHT node on startup
2. Determine beacon capability (NAT detection via UPnP/STUN)
3. If beacon: announce to DHT every `announce_interval`
4. If mesh neighbors < `min_neighbors`: run discovery cycle
5. Hand discovered endpoints to `MeshOverlayConnector`

#### Component 2: MeshOverlayServer

```csharp
public interface IMeshOverlayServer
{
    /// <summary>Start listening for inbound overlay connections.</summary>
    Task StartAsync(CancellationToken ct);
    
    /// <summary>Stop listening.</summary>
    Task StopAsync();
    
    /// <summary>Current active connections.</summary>
    int ActiveConnections { get; }
}
```

**Responsibilities:**
1. Listen on overlay port (if beacon capable)
2. Accept TCP connections
3. Read/validate `mesh_hello` message
4. Send `mesh_hello_ack` response
5. Register peer and hand connection to `IMeshSyncService`

#### Component 3: MeshOverlayConnector

```csharp
public interface IMeshOverlayConnector
{
    /// <summary>Attempt to connect to candidate endpoints.</summary>
    Task ConnectToCandidatesAsync(
        IEnumerable<IPEndPoint> candidates, 
        CancellationToken ct);
}
```

**Flow:**
```csharp
foreach (var endpoint in candidates.Shuffle())
{
    if (AlreadyConnected(endpoint)) continue;
    if (MeshNeighborCount >= MaxNeighbors) break;
    
    try
    {
        using var tcp = new TcpClient();
        await tcp.ConnectAsync(endpoint, TimeSpan.FromSeconds(10), ct);
        
        // Send HELLO
        await SendMeshHelloAsync(tcp.GetStream());
        
        // Read ACK (with timeout)
        var ack = await ReadMeshHelloAckAsync(tcp.GetStream(), ct);
        if (!IsValid(ack)) continue;
        
        // Success! Register and hand off
        MeshNeighborRegistry.Register(ack.Username, endpoint, tcp);
        MeshSyncService.AttachConnection(ack.Username, tcp.GetStream());
    }
    catch { /* try next candidate */ }
}
```

### 6.6 NAT/Firewall Behavior

| Client Type | DHT Actions | Overlay Actions |
|-------------|-------------|-----------------|
| **Beacon** (public IP) | `announce_peer()` + `get_peers()` | Accept inbound TCP, make outbound |
| **Seeker** (behind NAT) | `get_peers()` only | Make outbound TCP only |

**Key insight:** Even firewalled clients can:
1. Query the DHT (outbound UDP)
2. Connect to beacons (outbound TCP)
3. Maintain persistent connections for mesh sync

### 6.7 Multiple Rendezvous Keys

Support multiple infohashes for resilience and future migrations:

```csharp
static readonly byte[][] RendezvousKeys = new[]
{
    SHA1.Hash("slskdn-mesh-v1"),         // Primary
    SHA1.Hash("slskdn-mesh-v1-backup-1"), // Backup
    SHA1.Hash("slskdn-mesh-v1-backup-2"), // Backup
};
```

Beacons announce to ALL keys. Seekers query ALL keys and merge/dedupe results.

### 6.8 Security Considerations

| Risk | Mitigation |
|------|------------|
| Impersonation | Soulseek username in handshake; can verify via actual Soulseek connection |
| DoS via fake announcements | Rate-limit connections per IP; validate handshake before registering |
| Privacy (IP exposure) | Only public IPs are exposed; same as normal Soulseek |
| Spam peers | Disconnect peers that send invalid data; maintain blocklist |

### 6.9 Integration with Existing Systems

```
┌─────────────────────────────────────────────────────────────────┐
│                    STARTUP SEQUENCE                             │
├─────────────────────────────────────────────────────────────────┤
│ 1. Start Soulseek connection (existing)                         │
│ 2. Start DhtRendezvousService                                   │
│    - Bootstrap DHT node                                         │
│    - Detect NAT/beacon capability                              │
│ 3. If beacon: Start MeshOverlayServer                          │
│ 4. If beacon: Begin DHT announce loop                          │
│ 5. If mesh neighbors < min: Begin DHT discovery loop           │
│    - Query DHT → Get endpoints → MeshOverlayConnector          │
│ 6. Hand successful connections to MeshSyncService (Phase 3)    │
└─────────────────────────────────────────────────────────────────┘
```

### 6.10 Files to Create

| File | Description |
|------|-------------|
| `src/slskd/DhtRendezvous/IDhtRendezvousService.cs` | Interface |
| `src/slskd/DhtRendezvous/DhtRendezvousService.cs` | DHT bootstrap, announce, discover |
| `src/slskd/DhtRendezvous/IMeshOverlayServer.cs` | Interface |
| `src/slskd/DhtRendezvous/MeshOverlayServer.cs` | TCP listener for overlay |
| `src/slskd/DhtRendezvous/IMeshOverlayConnector.cs` | Interface |
| `src/slskd/DhtRendezvous/MeshOverlayConnector.cs` | Outbound connection logic |
| `src/slskd/DhtRendezvous/Messages/OverlayMessages.cs` | mesh_hello/ack DTOs |
| `src/slskd/DhtRendezvous/API/DhtRendezvousController.cs` | Status endpoints |

### 6.11 Dependencies

**BitTorrent DHT Library Options (C#):**
- `MonoTorrent` - Full-featured, well-maintained
- `BencodeNET` + custom DHT - Lighter weight
- Port from existing Python/Go implementation

### 6.12 API Endpoints (Planned)

| Endpoint | Description |
|----------|-------------|
| `GET /api/v0/dht/status` | DHT node status, beacon capability |
| `GET /api/v0/dht/peers` | Discovered overlay endpoints |
| `GET /api/v0/dht/announce` | Force announce (beacon only) |
| `POST /api/v0/dht/discover` | Force discovery cycle |
| `GET /api/v0/overlay/connections` | Active overlay connections |
| `GET /api/v0/overlay/stats` | Connection statistics |

---

## Implementation Order

### Sprint 1: Foundation ✅ COMPLETE
1. ✅ Existing multi-source system (WORKING - verified Dec 2025)
2. ✅ Create `CapabilityService` with UserInfo tag (Phase 1 - commit `2847b35d`)
3. ✅ Create `HashDbService` with new schema (Phase 2 - commit `b790d696`)

### Sprint 2: Hash Resolution ✅ COMPLETE
4. ✅ Integrate hash lookup into `ContentVerificationService` (commit `e3f069bf`)
5. ✅ Add passive hash collection from downloads (commit `e3f069bf`)
6. ✅ Create `BackfillSchedulerService` (commit `df3f605f`)

### Sprint 3: Mesh Sync ✅ COMPLETE
7. ✅ Create `MeshSyncService` with protocol handlers (commit `fba4ccab`)
8. ✅ Add mesh delta sync logic
9. ⬜ Integrate mesh sync triggers into peer interactions (needs Soulseek transport)

### Sprint 4: DHT Rendezvous (Phase 6) ⬜ IN PROGRESS

#### 🔴 SECURITY HARDENING (COMPLETE ✅)

| # | Task | Status | File(s) |
|---|------|--------|---------|
| S1 | TLS 1.3 for ALL overlay connections | ✅ | `MeshOverlayConnection.cs` |
| S2 | Length-prefixed message framing (4-byte header) | ✅ | `SecureMessageFramer.cs` |
| S3 | Message size limits (4KB max) | ✅ | `SecureMessageFramer.cs` |
| S4 | Strict JSON schema validation | ✅ | `MessageValidator.cs` |
| S5 | Username regex (`^[a-zA-Z0-9_\-\.]+$`) | ✅ | `MessageValidator.cs` |
| S6 | Hash format validation (hex only) | ✅ | `MessageValidator.cs` |
| S7 | Connection rate limiting (3/IP, 10/min) | ✅ | `OverlayRateLimiter.cs` |
| S8 | Message rate limiting (10/sec) | ✅ | `OverlayRateLimiter.cs` |
| S9 | Connection timeouts (10s/5s/30s/5min) | ✅ | `OverlayTimeouts.cs` |
| S10 | Idle timeout + keepalive | ✅ | `MeshOverlayConnection.cs` |
| S11 | IP blocklist for offenders | ✅ | `OverlayBlocklist.cs` |
| S12 | Certificate pinning (TOFU) | ✅ | `CertificatePinStore.cs` |
| S13 | Soulseek username verification | ⬜ | Future enhancement |
| S14 | Peer diversity checks (anti-eclipse) | ⬜ | Future enhancement |

#### Core DHT Implementation

| # | Task | Status | File(s) |
|---|------|--------|---------|
| D1 | Overlay message types | ✅ | `Messages/OverlayMessages.cs` |
| D2 | DhtRendezvousService interface | ✅ | `IDhtRendezvousService.cs` |
| D3 | DhtRendezvousService impl | ✅ | `DhtRendezvousService.cs` |
| D4 | MeshOverlayServer interface | ✅ | `IMeshOverlayServer.cs` |
| D5 | MeshOverlayServer impl (TLS) | ✅ | `MeshOverlayServer.cs` |
| D6 | MeshOverlayConnector interface | ✅ | `IMeshOverlayConnector.cs` |
| D7 | MeshOverlayConnector impl (TLS) | ✅ | `MeshOverlayConnector.cs` |
| D8 | NAT detection (placeholder) | ✅ | `DhtRendezvousService.cs` (basic) |
| D9 | MeshSyncService integration | 🔄 | Needs handoff logic |
| D10 | Certificate management | ✅ | `CertificateManager.cs` |
| D11 | API endpoints | ✅ | `API/DhtRendezvousController.cs` |
| D12 | BitTorrent DHT integration | ⬜ | MonoTorrent (future) |
| D13 | Service registration | ⬜ | `Program.cs` |

#### Phase 6 Files to Create

```
src/slskd/DhtRendezvous/
├── API/
│   └── DhtRendezvousController.cs
├── Messages/
│   └── OverlayMessages.cs
├── Security/
│   ├── CertificateManager.cs
│   ├── CertificatePinStore.cs
│   ├── MessageValidator.cs
│   ├── OverlayBlocklist.cs
│   ├── OverlayRateLimiter.cs
│   ├── OverlayTimeouts.cs
│   ├── OverlayTlsProvider.cs
│   ├── PeerDiversityChecker.cs
│   ├── PeerVerificationService.cs
│   └── SecureMessageFramer.cs
├── DhtRendezvousService.cs
├── IDhtRendezvousService.cs
├── IMeshOverlayConnector.cs
├── IMeshOverlayServer.cs
├── MeshOverlayConnection.cs
├── MeshOverlayConnector.cs
├── MeshOverlayServer.cs
└── NatDetectionService.cs
```

### Sprint 5: Polish & Testing
16. ✅ API endpoints for hash DB / capabilities / mesh / backfill (complete)
17. ✅ Web UI status bar and Network dashboard (complete)
18. ⬜ Load testing with real network
19. ✅ Documentation updates

---

## Testing Strategy

### Unit Tests
- Hash key generation
- FLAC header parsing (already exists)
- Capability tag parsing
- Mesh message serialization

### Integration Tests
- Hash resolution pipeline
- Backfill rate limiting
- Mesh sync between two nodes

### Network Tests (Manual)
- Run multiple `slskdn` instances on test accounts
- Verify mesh sync propagation
- Measure convergence time for popular vs rare files
- Verify backfill doesn't trigger bans

---

## Risk Mitigation

| Risk | Mitigation |
|------|------------|
| Backfill causes user bans | Strict rate limits, skip known-bad peers |
| Mesh sync overhead | Bounded sync sizes, interval limits |
| Database growth | Periodic cleanup of stale entries |
| Protocol compatibility | All extensions invisible to legacy clients |
| Privacy concerns | Only share file hashes, not browsing history |

---

## 🔴 CRITICAL: Overlay Security Requirements

**The overlay protocol (Phase 6) creates a new attack surface. These requirements are MANDATORY before any production deployment.**

### Security Checklist

| # | Requirement | Priority | Status |
|---|-------------|----------|--------|
| S1 | TLS 1.3 encryption for ALL overlay connections | 🔴 CRITICAL | ⬜ |
| S2 | Length-prefixed message framing (4-byte header) | 🔴 CRITICAL | ⬜ |
| S3 | Message size limits (4KB max per message) | 🔴 CRITICAL | ⬜ |
| S4 | Strict JSON schema validation | 🔴 CRITICAL | ⬜ |
| S5 | Username regex validation (`^[a-zA-Z0-9_\-\.]+$`) | 🔴 CRITICAL | ⬜ |
| S6 | Hash format validation (hex only, exact lengths) | 🔴 CRITICAL | ⬜ |
| S7 | Connection rate limiting (3/IP, 10/min global) | 🟠 HIGH | ⬜ |
| S8 | Message rate limiting (10/sec per connection) | 🟠 HIGH | ⬜ |
| S9 | Connection timeouts (10s connect, 5s handshake) | 🟠 HIGH | ⬜ |
| S10 | Idle timeout with keepalive (5min idle, 2min ping) | 🟠 HIGH | ⬜ |
| S11 | IP blocklist for repeat offenders | 🟠 HIGH | ⬜ |
| S12 | Certificate pinning (TOFU model) | 🟡 MEDIUM | ⬜ |
| S13 | Soulseek username verification | 🟡 MEDIUM | ⬜ |
| S14 | Peer diversity checks (anti-eclipse) | 🟡 MEDIUM | ⬜ |

### Threat Model

| Threat | Impact | Mitigation |
|--------|--------|------------|
| **Message Injection** | Remote code execution, crashes | S2, S3, S4, S5, S6 |
| **Man-in-the-Middle** | Hash poisoning, data theft | S1, S12 |
| **DoS (Connection Flood)** | Service unavailable | S7, S9, S11 |
| **DoS (Message Flood)** | CPU/memory exhaustion | S3, S8, S10 |
| **Buffer Overflow** | Memory corruption, RCE | S2, S3 |
| **Username Spoofing** | Impersonation attacks | S5, S13 |
| **Eclipse Attack** | Isolation, hash poisoning | S14 |

### Implementation Notes

```csharp
// CORRECT: Length-prefixed, bounded read
var lengthBytes = new byte[4];
await stream.ReadExactlyAsync(lengthBytes, ct);
var length = BinaryPrimitives.ReadInt32BigEndian(lengthBytes);
if (length <= 0 || length > 4096) throw new ProtocolException();
var buffer = new byte[length];
await stream.ReadExactlyAsync(buffer, ct);

// WRONG: Unbounded read - NEVER DO THIS
var json = await new StreamReader(stream).ReadToEndAsync(); // 💀 VULNERABLE
```

```csharp
// CORRECT: TLS wrapper
var sslStream = new SslStream(tcpClient.GetStream(), false);
await sslStream.AuthenticateAsServerAsync(cert, false, SslProtocols.Tls13, true);

// WRONG: Plain TCP - NEVER DO THIS
var stream = tcpClient.GetStream(); // 💀 PLAINTEXT
```

---

## Success Metrics

| Metric | Target |
|--------|--------|
| Hash lookup hit rate (popular files) | >90% after 1 week |
| Hash lookup hit rate (long tail) | >50% after 1 month |
| Backfill probes per day (network-wide) | <1000 |
| Mesh sync latency (popular hash) | <1 hour |
| Mesh sync latency (rare hash) | <1 week |

---

## Appendix A: FLAC Key Format

```
flac_key = sha1(normalize(filename) + ":" + str(filesize))

Normalization:
- Lowercase
- Strip path (basename only)
- Remove common prefixes: "01 ", "01. ", "[Various Artists] ", etc.
- Remove common suffixes: " (Remaster)", " [FLAC]", etc.
- Collapse whitespace
```

Example:
```
Input: "Music/Artist - Album/01. Track Name (2024 Remaster).flac" (45200000 bytes)
Normalized: "track name.flac"
Key: sha1("track name.flac:45200000")
```

---

## Appendix B: Protocol Extension Reference

Based on analysis of the Soulseek protocol (https://github.com/nicotine-plus/nicotine-plus/blob/master/doc/SLSKPROTOCOL.md):

| Extension Point | Method | Risk | Use Case |
|-----------------|--------|------|----------|
| User Info | Add `slskdn_caps` to description | Safe | Capability negotiation |
| Client Version | Append `+dht+mesh` | Safe | Passive discovery |
| Dummy File | Request `@@slskdn/__caps__.json` | Mild | Active handshake |
| Queue Reason | Structured string `slskdn:...` | Mild | Backfill identification |
| File Attribute 3 | Unused encoder field | Unknown | Future use |

---

## Appendix C: DHT Sizing Estimates

From research on Soulseek network size:

| Scenario | Unique FLACs | Network-wide DHT | Per-node storage |
|----------|--------------|------------------|------------------|
| Low (10k users) | 15M | ~6 GB | ~1.2 MB |
| Mid (20k users) | 64M | ~24 GB | ~5 MB |
| High (50k users) | 250M | ~96 GB | ~10 MB |

Conclusion: **DHT is absolutely tractable** from a capacity perspective.

---

## Appendix D: Storage Optimization Strategies

Even though per-node storage is manageable, we want to keep the global system lean for fast lookups. Four optimization angles:

### D.1 Shrink Each Entry Aggressively

#### Use Short Keys (Truncated Hashes)

Instead of full 160-bit SHA-1 or 128-bit UUIDs:

```
flac_key = 64-bit fingerprint (lower 64 bits of hash)
```

Keep the **full FLAC MD5** in the value for collision disambiguation.

- **Key**: 8 bytes (instead of 20)
- **Value**: 16 bytes MD5 + 5 bytes size + 2 bytes flags = ~23 bytes
- Total: **~32 bytes/entry**

#### Compact Binary Struct (Zero JSON)

```csharp
// C# equivalent of compact DHT entry
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct FlacDhtEntry
{
    public ulong Key;           // 8 bytes - truncated hash
    public ulong SizeAndFlags;  // 8 bytes - 40 bits size + 24 bits flags
    public ulong Md5Hi;         // 8 bytes - upper 64 bits of FLAC MD5
    public ulong Md5Lo;         // 8 bytes - lower 64 bits of FLAC MD5
}
// Total: 32 bytes flat
```

**Revised global estimates with 32-byte entries:**

| Unique FLACs | Raw Size (×3 replication) | Per-node (20k nodes) |
|--------------|---------------------------|----------------------|
| 15M | 1.4 GB | ~2.3 MB |
| 64M | 6.1 GB | ~9.6 MB |
| 250M | 24 GB | ~3.8 MB |

### D.2 Prune by Usefulness - Don't Index Everything

#### TTL Policy for Unused Keys

Add per-entry metrics:

```sql
ALTER TABLE HashDb ADD COLUMN use_count INTEGER DEFAULT 0;
ALTER TABLE HashDb ADD COLUMN last_request_at INTEGER;
```

Purge candidates:
- `use_count == 0` AND `age > 12 months` → DELETE
- `use_count > 0` but no requests in 2-3 years → move to cold archive

#### Only Promote "Hot" Keys to Mesh

Instead of flooding mesh with every hash:

```
Local-only table:
- Stores ALL discovered hashes

Mesh-shared table:
- Only entries where:
  - seen_on > 1 peers, OR
  - You downloaded it yourself, OR
  - Another peer requested it from you
```

This keeps the global mesh lean while preserving local knowledge.

#### Cohort-Based Selection (Optional)

Bias storage toward content that passes through your node often:
- Keep 100% of keys for genres you consume
- Aggressively prune keys outside your taste profile
- No explicit genre modeling needed - just LRU/use-count based

### D.3 Tiered Storage: Hot / Warm / Cold

```
┌─────────────────────────────────────────────────────┐
│ HOT INDEX (RAM)                                     │
│ - Compact key → offset map                          │
│ - ~100k entries max                                 │
│ - ~2-3 MB working set                               │
│ - Recently synced / recently looked up              │
└─────────────────────────────────────────────────────┘
                         ↓
┌─────────────────────────────────────────────────────┐
│ WARM STORE (On-disk, compressed)                    │
│ - SQLite/RocksDB/LMDB with zstd compression         │
│ - All "active" mesh entries                         │
│ - 2-4x compression ratio typical                    │
│ - 32 bytes → 8-16 bytes effective                   │
└─────────────────────────────────────────────────────┘
                         ↓
┌─────────────────────────────────────────────────────┐
│ COLD ARCHIVE (Optional)                             │
│ - Very old / unused entries                         │
│ - Maximum compression (zstd dictionary)             │
│ - Lazy-loaded if ever                               │
└─────────────────────────────────────────────────────┘
```

#### Hot Index Implementation

```csharp
// In-memory hot index using ConcurrentDictionary
public class HotHashIndex
{
    // Key: 64-bit truncated hash
    // Value: offset into warm store OR inline small value
    private readonly ConcurrentDictionary<ulong, HashIndexEntry> _index;
    
    // Bloom filter for fast "definitely not here" checks
    private readonly BloomFilter _bloomFilter;
    
    // LRU tracking for eviction
    private readonly LinkedList<ulong> _lruList;
    
    public const int MaxHotEntries = 100_000;
}
```

#### Warm Store with Compression

```csharp
// SQLite with page-level compression
var options = new SqliteConnectionStringBuilder
{
    DataSource = "hashdb.db",
    Mode = SqliteOpenMode.ReadWriteCreate,
};

// Enable compression via VACUUM INTO with zstd extension
// Or use RocksDB which has native compression support
```

### D.4 Fast Lookup Optimizations

#### Bloom/XOR Filter for Existence Checks

Before hitting disk, check a compact filter:

```csharp
public class HashLookupService
{
    private readonly XorFilter _existenceFilter; // 8-12 bits per key
    private readonly IWarmStore _warmStore;
    
    public async Task<byte[]> LookupAsync(ulong key)
    {
        // Fast path: definitely not here
        if (!_existenceFilter.MayContain(key))
            return null;
        
        // Slow path: check warm store
        return await _warmStore.GetAsync(key);
    }
}
```

With 100k entries at 10 bits each = ~125 KB for filter.

#### Shard by Key Prefix

Partition storage by high bits of 64-bit key:

```
hashdb_shard_00.db  (keys 0x00... - 0x0F...)
hashdb_shard_01.db  (keys 0x10... - 0x1F...)
...
hashdb_shard_0F.db  (keys 0xF0... - 0xFF...)
```

Benefits:
- Each shard has its own filter + file
- Only touch one shard per lookup
- Parallel sync possible

### D.5 On-Wire Compression & Delta Sync

#### Batch + Compress

```csharp
public class MeshSyncCompressor
{
    public byte[] CompressDelta(IEnumerable<FlacDhtEntry> entries)
    {
        // Serialize to binary
        var raw = SerializeEntries(entries);
        
        // Compress with zstd (excellent for repetitive binary data)
        return ZstdNet.Compressor.Compress(raw, CompressionLevel.Optimal);
    }
}
```

Typical compression:
- 500 entries × 32 bytes = 16 KB raw
- After zstd: ~4-6 KB (3-4x compression)

#### Delta Sync Bounds

| Constraint | Value | Rationale |
|------------|-------|-----------|
| Max entries per sync | 1000 | ~32 KB raw, ~10 KB compressed |
| Max syncs per peer per day | 10 | Prevent spam |
| Min interval between syncs | 30 min | Allow accumulation |

### D.6 Implementation Structs

#### C# Entry Definition

```csharp
namespace slskd.HashDb.Models
{
    /// <summary>
    /// Compact 32-byte DHT entry for FLAC hash storage.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public readonly struct CompactHashEntry
    {
        /// <summary>64-bit truncated hash key.</summary>
        public readonly ulong Key;
        
        /// <summary>File size (40 bits) + flags (24 bits).</summary>
        public readonly ulong SizeFlags;
        
        /// <summary>Upper 64 bits of FLAC STREAMINFO MD5.</summary>
        public readonly ulong Md5Hi;
        
        /// <summary>Lower 64 bits of FLAC STREAMINFO MD5.</summary>
        public readonly ulong Md5Lo;
        
        public long Size => (long)(SizeFlags & 0xFFFFFFFFFF); // 40 bits
        public int Flags => (int)(SizeFlags >> 40);           // 24 bits
        
        public byte[] GetMd5()
        {
            var result = new byte[16];
            BitConverter.TryWriteBytes(result.AsSpan(0, 8), Md5Hi);
            BitConverter.TryWriteBytes(result.AsSpan(8, 8), Md5Lo);
            return result;
        }
        
        public static CompactHashEntry Create(ulong key, long size, int flags, byte[] md5)
        {
            return new CompactHashEntry
            {
                Key = key,
                SizeFlags = ((ulong)size & 0xFFFFFFFFFF) | ((ulong)flags << 40),
                Md5Hi = BitConverter.ToUInt64(md5, 0),
                Md5Lo = BitConverter.ToUInt64(md5, 8),
            };
        }
    }
    
    /// <summary>
    /// Flags packed into 24 bits.
    /// </summary>
    [Flags]
    public enum HashEntryFlags : int
    {
        None = 0,
        Verified = 1 << 0,       // Hash verified by download
        MeshSynced = 1 << 1,     // Received via mesh sync
        LocalOnly = 1 << 2,      // Don't propagate to mesh
        HighConfidence = 1 << 3, // Multiple sources confirmed
        // Bits 4-7: Sample rate tier (0-15 = common rates)
        // Bits 8-10: Channel count (0-7)
        // Bits 11-15: Bit depth tier
        // Bits 16-23: Reserved
    }
}
```

#### Key Generation

```csharp
public static class FlacKeyGenerator
{
    /// <summary>
    /// Generate 64-bit DHT key from filename and size.
    /// </summary>
    public static ulong GenerateKey(string filename, long size)
    {
        var normalized = NormalizeFilename(filename);
        var input = $"{normalized}:{size}";
        
        // Use xxHash64 for speed (or lower 64 bits of SHA-1)
        return XxHash64.Hash(Encoding.UTF8.GetBytes(input));
    }
    
    private static string NormalizeFilename(string filename)
    {
        var basename = Path.GetFileName(filename).ToLowerInvariant();
        
        // Strip common prefixes
        basename = Regex.Replace(basename, @"^\d{1,3}[\.\-\s]+", ""); // "01. " or "01 - "
        basename = Regex.Replace(basename, @"^\[.*?\]\s*", "");       // "[Artist] "
        
        // Strip common suffixes
        basename = Regex.Replace(basename, @"\s*\(.*?remaster.*?\)", "", RegexOptions.IgnoreCase);
        basename = Regex.Replace(basename, @"\s*\[flac\]", "", RegexOptions.IgnoreCase);
        
        // Collapse whitespace
        basename = Regex.Replace(basename, @"\s+", " ").Trim();
        
        return basename;
    }
}
```

### D.7 Memory Footprint Summary

For a node with 100k hot entries + 1M warm entries:

| Component | Size |
|-----------|------|
| Hot index (ConcurrentDictionary) | ~4 MB |
| Bloom filter (100k keys) | ~125 KB |
| Warm store (1M × 32 bytes, 3x compression) | ~10 MB on disk |
| **Total RAM** | **~5 MB** |
| **Total Disk** | **~10 MB** |

This is extremely manageable even on low-end devices.

