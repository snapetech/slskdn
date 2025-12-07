# slskdn Multi-Source & DHT Implementation Roadmap

## Executive Summary

This document maps out the complete implementation path for building out the multi-source download functionality with a distributed hash table (DHT) / epidemic mesh sync protocol. The goal is to create a network of `slskdn` clients that can share FLAC hash information, enabling instant content verification without redundant header probing.

**Branch:** `experimental/multi-source-swarm`

---

## Current State Analysis

### Already Implemented ✅

| Component | Location | Status |
|-----------|----------|--------|
| Multi-source chunked downloads (SWARM mode) | `src/slskd/Transfers/MultiSource/MultiSourceDownloadService.cs` | Working |
| Content verification (FLAC MD5 + SHA256) | `src/slskd/Transfers/MultiSource/ContentVerificationService.cs` | Working |
| FLAC STREAMINFO parser | `src/slskd/Transfers/MultiSource/FlacStreamInfo.cs` | Working |
| Source discovery service | `src/slskd/Transfers/MultiSource/Discovery/SourceDiscoveryService.cs` | Working |
| API endpoints for swarm downloads | `src/slskd/Transfers/MultiSource/API/MultiSourceController.cs` | Working |
| LimitedWriteStream for chunk downloads | `ContentVerificationService.cs` | Working |
| SQLite-based discovery database | `SourceDiscoveryService.cs` | Partial |

### Not Yet Implemented ❌

| Feature | Priority | Complexity |
|---------|----------|------------|
| Protocol Extensions (UserInfo caps, Version string) | HIGH | Medium |
| Peers Table with capability tracking | HIGH | Low |
| FLAC Inventory Table (enhanced schema) | HIGH | Medium |
| DHT/Mesh Sync Protocol | HIGH | High |
| Backfill Scheduler with rate limiting | MEDIUM | Medium |
| Capability File sharing (`__slskdn_caps__`) | MEDIUM | Low |
| Queue Reason field overloading | LOW | Low |
| Small-world neighbor optimization | LOW | Medium |
| Web UI for DHT status/hash database | LOW | Medium |

---

## Phase 1: Protocol Extensions - Capability Discovery

### 1.1 UserInfo Tag Advertisement

**Purpose:** Advertise `slskdn` capabilities in the user description field.

**Implementation:**

1. Create `ICapabilityService` interface:

```csharp
// src/slskd/Capabilities/ICapabilityService.cs
public interface ICapabilityService
{
    /// <summary>Generates the capability tag string for UserInfo.</summary>
    string GetCapabilityTag();
    
    /// <summary>Parses capability tag from a peer's description.</summary>
    PeerCapabilities ParseCapabilityTag(string description);
    
    /// <summary>Gets capabilities for a known peer.</summary>
    PeerCapabilities GetPeerCapabilities(string username);
    
    /// <summary>Records discovered capabilities for a peer.</summary>
    void SetPeerCapabilities(string username, PeerCapabilities caps);
}

[Flags]
public enum PeerCapabilityFlags
{
    None = 0,
    SupportsDHT = 1 << 0,
    SupportsHashExchange = 1 << 1,
    SupportsPartialDownload = 1 << 2,
    SupportsMeshSync = 1 << 3,
    SupportsFlacHashDb = 1 << 4,
}

public class PeerCapabilities
{
    public PeerCapabilityFlags Flags { get; set; }
    public string ClientVersion { get; set; }
    public DateTime LastSeen { get; set; }
    public DateTime LastCapCheck { get; set; }
}
```

2. Modify user info response to append capability tag:

```
Format: ... existing description ... | slskdn_caps:v1;dht=1;hashx=1;mesh=1
```

**Files to create/modify:**
- `src/slskd/Capabilities/ICapabilityService.cs` (NEW)
- `src/slskd/Capabilities/CapabilityService.cs` (NEW)
- `src/slskd/Capabilities/CapabilityDbContext.cs` (NEW)
- `src/slskd/Users/UserService.cs` (MODIFY - inject capability tag)
- `src/slskd/Program.cs` (MODIFY - register services)

### 1.2 Client Version String Extension

**Purpose:** Passive capability discovery via version string.

**Implementation:**

Append capability token to the client version string sent during peer connection:

```
"slskdn/1.0.0+dht+mesh"
```

**Files to modify:**
- `src/slskd/Core/SoulseekClientFactory.cs` or equivalent

### 1.3 Capability File Sharing (Virtual File)

**Purpose:** Active capability negotiation via phantom file request.

**Implementation:**

1. Share a virtual file at path: `@@slskdn/__caps__.json`
2. When requested, return JSON with capabilities:

```json
{
  "client": "slskdn",
  "version": "1.0.0",
  "features": ["dht", "hash_exchange", "mesh_sync", "flac_hash_db"],
  "protocol_version": 1,
  "mesh_seq_id": 847291
}
```

3. Requesting this file from another peer triggers capability detection.

**Files to create:**
- `src/slskd/Capabilities/VirtualCapabilityFileHandler.cs` (NEW)

---

## Phase 2: Local Hash Database - Enhanced Schema

### 2.1 Database Schema

Replace/enhance the existing `discovery.db` with a comprehensive schema:

```sql
-- Peer tracking with capabilities
CREATE TABLE Peers (
    peer_id TEXT PRIMARY KEY,          -- Soulseek username
    caps INTEGER DEFAULT 0,            -- PeerCapabilityFlags bitfield
    client_version TEXT,               -- Detected client version
    last_seen INTEGER NOT NULL,        -- Unix timestamp
    last_cap_check INTEGER,            -- Unix timestamp of last capability probe
    backfills_today INTEGER DEFAULT 0, -- Header probes done today
    backfill_reset_date INTEGER        -- Date for backfill counter reset
);

-- FLAC file inventory with hash tracking
CREATE TABLE FlacInventory (
    file_id TEXT PRIMARY KEY,          -- sha256(peer_id + path + size)
    peer_id TEXT NOT NULL,             -- Owner username
    path TEXT NOT NULL,                 -- Full remote path
    size INTEGER NOT NULL,              -- File size in bytes
    discovered_at INTEGER NOT NULL,     -- Unix timestamp
    hash_status TEXT DEFAULT 'none',    -- 'none'/'known'/'pending'/'failed'
    hash_value TEXT,                    -- FLAC STREAMINFO MD5 (nullable)
    hash_source TEXT,                   -- 'local_scan'/'peer_dht'/'backfill_sniff'/'mesh_sync'
    sample_rate INTEGER,                -- Audio sample rate
    channels INTEGER,                   -- Number of channels
    bit_depth INTEGER,                  -- Bits per sample
    duration_samples INTEGER,           -- Total samples
    FOREIGN KEY (peer_id) REFERENCES Peers(peer_id)
);

-- DHT/Mesh hash database (content-addressed)
CREATE TABLE HashDb (
    flac_key TEXT PRIMARY KEY,         -- sha1(normalized_filename + ':' + size)
    flac_md5 BLOB NOT NULL,            -- 16-byte FLAC STREAMINFO MD5
    size INTEGER NOT NULL,              -- File size
    meta_flags INTEGER,                 -- Optional: packed sample_rate/channels/bit_depth
    first_seen_at INTEGER NOT NULL,     -- Unix timestamp
    last_updated_at INTEGER NOT NULL,   -- Unix timestamp
    seq_id INTEGER                      -- Monotonic sequence for delta sync
);

-- Mesh sync state per peer
CREATE TABLE MeshPeerState (
    peer_id TEXT PRIMARY KEY,
    caps INTEGER DEFAULT 0,
    last_sync_time INTEGER,
    last_seq_seen INTEGER DEFAULT 0,    -- Highest sequence ID received
    FOREIGN KEY (peer_id) REFERENCES Peers(peer_id)
);

-- Backfill scheduler state
CREATE TABLE BackfillState (
    key TEXT PRIMARY KEY,
    value TEXT
);

-- Indexes for performance
CREATE INDEX idx_inventory_peer ON FlacInventory(peer_id);
CREATE INDEX idx_inventory_size ON FlacInventory(size);
CREATE INDEX idx_inventory_hash ON FlacInventory(hash_value);
CREATE INDEX idx_inventory_status ON FlacInventory(hash_status);
CREATE INDEX idx_hashdb_size ON HashDb(size);
CREATE INDEX idx_hashdb_seq ON HashDb(seq_id);
```

### 2.2 Database Service

**Files to create:**
- `src/slskd/HashDb/IHashDbService.cs` (NEW)
- `src/slskd/HashDb/HashDbService.cs` (NEW)
- `src/slskd/HashDb/HashDbContext.cs` (NEW - EF Core or raw SQLite)
- `src/slskd/HashDb/Models/` (NEW - entity classes)

**Key methods:**

```csharp
public interface IHashDbService
{
    // Peer management
    Task<Peer> GetOrCreatePeerAsync(string username);
    Task UpdatePeerCapabilitiesAsync(string username, PeerCapabilityFlags caps);
    
    // Inventory management
    Task UpsertFlacEntryAsync(FlacInventoryEntry entry);
    Task<FlacInventoryEntry> GetFlacEntryAsync(string fileId);
    Task<IEnumerable<FlacInventoryEntry>> GetUnhashedFlacFilesAsync(int limit);
    
    // Hash database (content-addressed)
    Task<string> LookupHashAsync(string flacKey);
    Task StoreHashAsync(string flacKey, byte[] flacMd5, long size, int? metaFlags);
    
    // Mesh sync
    Task<long> GetLatestSeqIdAsync();
    Task<IEnumerable<HashDbEntry>> GetEntriesSinceSeqAsync(long sinceSeq, int limit);
    Task MergeEntriesFromMeshAsync(IEnumerable<HashDbEntry> entries);
    Task<long> GetPeerLastSeqSeenAsync(string peerId);
    Task UpdatePeerLastSeqSeenAsync(string peerId, long seqId);
    
    // Backfill scheduling
    Task<IEnumerable<FlacInventoryEntry>> GetBackfillCandidatesAsync(int limit);
    Task IncrementPeerBackfillCountAsync(string peerId);
    Task<int> GetPeerBackfillCountTodayAsync(string peerId);
}
```

---

## Phase 3: DHT/Epidemic Mesh Sync Protocol

### 3.1 Overview

Instead of traditional Kademlia DHT (requires dedicated routing, always-on supernodes), implement **gossip-based eventual consistency** where every `slskdn` client exchanges hash databases during normal Soulseek interactions.

### 3.2 Wire Protocol Messages

**Transport:** Over existing Soulseek peer connections using the "reason" field or via virtual file transfer.

#### Message Types

```csharp
public enum MeshMessageType
{
    Hello = 1,
    ReqDelta = 2,
    PushDelta = 3,
    ReqKey = 4,
    RespKey = 5,
}

public class MeshHelloMessage
{
    public string ClientId { get; set; }      // Username
    public int ProtocolVersion { get; set; }   // 1
    public long LatestSeqId { get; set; }      // Highest local seq
}

public class MeshReqDeltaMessage
{
    public long SinceSeqId { get; set; }
    public int MaxEntries { get; set; }        // Default 1000
}

public class MeshPushDeltaMessage
{
    public List<MeshHashEntry> Entries { get; set; }
}

public class MeshHashEntry
{
    public long SeqId { get; set; }
    public string FlacKey { get; set; }        // sha1(norm_filename + ':' + size)
    public byte[] FlacMd5 { get; set; }        // 16 bytes
    public long Size { get; set; }
    public int? MetaFlags { get; set; }
}

public class MeshReqKeyMessage
{
    public string FlacKey { get; set; }
}

public class MeshRespKeyMessage
{
    public string FlacKey { get; set; }
    public byte[] FlacMd5 { get; set; }        // Null if not found
    public long? Size { get; set; }
}
```

### 3.3 Sync Flow

```
When client A connects to mesh-capable client B:

1. A → B: MESH_HELLO { latest_seq_id: 50000 }
2. B → A: MESH_HELLO { latest_seq_id: 47000 }

3. B sees A has newer entries:
   B → A: MESH_REQ_DELTA { since_seq_id: 47000, max_entries: 1000 }
   A → B: MESH_PUSH_DELTA { entries: [...] }

4. A sees B might have entries A is missing:
   A → B: MESH_REQ_DELTA { since_seq_id: 45000, max_entries: 1000 }
   B → A: MESH_PUSH_DELTA { entries: [...] }

5. Both clients merge received entries, updating their local DBs.
```

### 3.4 Implementation Files

**Files to create:**
- `src/slskd/Mesh/IMeshSyncService.cs` (NEW)
- `src/slskd/Mesh/MeshSyncService.cs` (NEW)
- `src/slskd/Mesh/Messages/` (NEW - message classes)
- `src/slskd/Mesh/MeshProtocolHandler.cs` (NEW)

**Key service interface:**

```csharp
public interface IMeshSyncService
{
    /// <summary>Initiates mesh sync with a peer if they support it.</summary>
    Task TrySyncWithPeerAsync(string username, CancellationToken ct = default);
    
    /// <summary>Handles incoming mesh message from a peer.</summary>
    Task HandleMeshMessageAsync(string fromUser, byte[] data, CancellationToken ct = default);
    
    /// <summary>Looks up hash in local DB first, then queries mesh neighbors.</summary>
    Task<byte[]> LookupHashAsync(string flacKey, CancellationToken ct = default);
    
    /// <summary>Publishes a newly discovered hash to the mesh.</summary>
    Task PublishHashAsync(string flacKey, byte[] flacMd5, long size, CancellationToken ct = default);
    
    /// <summary>Gets mesh sync statistics.</summary>
    MeshSyncStats GetStats();
}
```

### 3.5 Sync Constraints

| Parameter | Default | Description |
|-----------|---------|-------------|
| `MESH_SYNC_INTERVAL_MIN` | 1800s | Min seconds between syncs with same peer |
| `MESH_MAX_ENTRIES_PER_SYNC` | 1000 | Max entries exchanged per session |
| `MESH_MAX_PEERS_PER_CYCLE` | 5 | Max peers to sync with per time window |

---

## Phase 4: Backfill Scheduler Service

### 4.1 Purpose

Last-resort mechanism for discovering FLAC hashes when DHT lookup fails. Must be **extremely conservative** to avoid abuse.

### 4.2 Hard Constraints

| Constraint | Value | Rationale |
|------------|-------|-----------|
| `MAX_GLOBAL_CONNECTIONS` | 1-2 | Never more than 2 simultaneous probes |
| `MAX_PER_PEER_PER_DAY` | 10 | No peer hit more than 10 times daily |
| `MAX_HEADER_BYTES` | 64KB | Stop reading immediately after STREAMINFO |
| `MIN_IDLE_TIME` | 5min | Only run when no active transfers |
| `RUN_INTERVAL` | 10min | Scheduler wakes every 10 minutes |

### 4.3 Selection Algorithm

```
if active_backfills >= MAX_GLOBAL_CONNECTIONS:
    return

candidates = SELECT FROM FlacInventory
    WHERE hash_status = 'none'
    AND peer_caps[peer_id] NOT CONTAINS supports_dht
    AND backfills_today[peer_id] < MAX_PER_PEER_PER_DAY
    ORDER BY discovered_at ASC
    LIMIT 3

for candidate in candidates:
    if eligible(candidate):  # Not in active queue, peer online, etc.
        schedule_backfill(candidate)
        break
```

### 4.4 Backfill Operation Flow

```
1. Set hash_status = 'pending'
2. Queue as lowest priority upload request
3. Use reason field: "slskdn:hdr_probe" (recognized by other slskdn nodes)
4. Wait for transfer_accepted OR timeout(30s)
5. If timeout: set hash_status = 'failed', return
6. Read up to MAX_HEADER_BYTES
7. Close connection immediately
8. Parse FLAC STREAMINFO for MD5
9. If successful:
   - Set hash_status = 'known'
   - Set hash_value = md5
   - Set hash_source = 'backfill_sniff'
   - Increment backfills_today[peer_id]
   - Publish to mesh: dht_publish(flac_key, md5)
10. Else: set hash_status = 'failed'
```

### 4.5 Implementation Files

**Files to create:**
- `src/slskd/Backfill/IBackfillSchedulerService.cs` (NEW)
- `src/slskd/Backfill/BackfillSchedulerService.cs` (NEW)
- `src/slskd/Backfill/BackfillBackgroundService.cs` (NEW - IHostedService)

---

## Phase 5: Integration with Existing Multi-Source System

### 5.1 Hash Resolution Pipeline

Modify `ContentVerificationService` to use new hash resolution order:

```
1. Local HashDb lookup (instant)
   → If found: return hash, source='local_db'

2. Mesh query to N neighbors (fast, ~1-2s)
   → If found: store locally, return hash, source='mesh'

3. If peer supports_dht:
   → Skip to step 5 (don't probe them, mesh will sync)

4. Legacy verification (current behavior):
   → Download 42-64KB, parse FLAC header
   → Store hash locally
   → Publish to mesh

5. Return hash or null
```

### 5.2 Files to Modify

- `src/slskd/Transfers/MultiSource/ContentVerificationService.cs`
  - Inject `IHashDbService`, `IMeshSyncService`
  - Add hash resolution pipeline before falling back to header download
  
- `src/slskd/Transfers/MultiSource/MultiSourceDownloadService.cs`
  - After successful download, publish hash to mesh
  
- `src/slskd/Transfers/MultiSource/Discovery/SourceDiscoveryService.cs`
  - Update to use new `FlacInventory` table instead of `DiscoveredFiles`
  - Add capability detection during discovery

### 5.3 API Endpoints to Add

```
GET  /api/v0/hashdb/stats
     → Hash database statistics

GET  /api/v0/hashdb/lookup?key={flacKey}
     → Look up hash by key

GET  /api/v0/hashdb/search?size={size}&filename={pattern}
     → Search hash database

GET  /api/v0/mesh/stats
     → Mesh sync statistics

GET  /api/v0/mesh/peers
     → List mesh-capable peers

POST /api/v0/mesh/sync/{username}
     → Trigger manual sync with peer

GET  /api/v0/backfill/stats
     → Backfill scheduler statistics

POST /api/v0/backfill/trigger
     → Manually trigger backfill cycle (for testing)
```

---

## Implementation Order

### Sprint 1: Foundation (Week 1-2)
1. ✅ Existing multi-source system (already done)
2. Create `HashDbService` with new schema
3. Create `CapabilityService` with UserInfo tag

### Sprint 2: Hash Resolution (Week 3-4)
4. Integrate hash lookup into `ContentVerificationService`
5. Add passive hash collection from downloads
6. Create `BackfillSchedulerService` (basic)

### Sprint 3: Mesh Sync (Week 5-6)
7. Create `MeshSyncService` with protocol handlers
8. Integrate mesh sync triggers into peer interactions
9. Add mesh delta sync logic

### Sprint 4: Polish & Testing (Week 7-8)
10. API endpoints for hash DB / mesh / backfill
11. Web UI integration (optional)
12. Load testing with real network
13. Documentation updates

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

