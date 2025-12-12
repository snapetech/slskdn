# Soulseek Community Bridge Strategy

## Philosophy

**Our Goal**: Use mesh/DHT/BitTorrent as *discovery channels* that **enhance and benefit** the Soulseek community, not replace it.

Think of it like this:
- **Soulseek Network** = The community we love
- **Mesh/DHT/BT** = New roads to discover members of that community
- **slskdn** = A bridge that brings discoveries from new roads back to the main community

---

## The Bridge Services

### 1. SoulseekMeshBridgeService
**Purpose**: Automatically register mesh-discovered peers with the Soulseek server

**What It Does**:
1. **Listens for new mesh peer connections** (`OnNeighborAdded` event)
2. **Extracts Soulseek username** from mesh peer (if they have one mapped)
3. **Bridges to Soulseek server** by:
   - Calling `GetUserInfoAsync()` - This notifies Soulseek server about the user
   - Calling `GetUserEndPointAsync()` - This establishes connectivity metadata
   - Implicitly adding them to server's user list
4. **Periodic re-bridging** (every 5 minutes) to ensure all mesh peers are known to Soulseek

**Result**: When you discover a peer via DHT/mesh, they become visible and discoverable to the **entire Soulseek network**, not just you.

### 2. MeshSearchBridgeService
**Purpose**: Supplement Soulseek search results with mesh-discovered files

**What It Does**:
1. **Hooks into search results aggregation**
2. **Queries mesh hash database** for matching files
3. **Adds supplemental results** from mesh-only peers to Soulseek search responses
4. **Avoids duplicates** by checking existing Soulseek responses first

**Result**: When someone searches on Soulseek, they see files from mesh-discovered sources, even if those sources aren't currently connected to Soulseek server.

---

## How It Benefits Soulseek

### Direct Benefits

#### NAT Traversal Help
- When we discover a peer via DHT/mesh who has symmetric NAT
- We call `GetUserEndPointAsync()` which helps establish their IP/port
- Other Soulseek users can now connect to them more easily
- **The whole community benefits from better NAT traversal metadata**

#### User Discovery
- Mesh/DHT discovers users who might be:
  - Behind restrictive firewalls
  - In different geographic regions
  - Not well-connected in the Soulseek network graph
- By bridging them to Soulseek server, we make them discoverable to everyone
- **Increases total network connectivity**

#### Search Result Richness
- Mesh hash database contains files from sources that may not show up in traditional Soulseek searches
- By supplementing search results, we:
  - Increase file availability
  - Improve search quality
  - Make rare content more discoverable
- **Enhances the value of Soulseek searches for everyone**

### Indirect Benefits

#### Network Redundancy
- If a user is connected via both Soulseek and mesh:
  - Transfers can use whichever path is faster
  - Network failures on one path don't break the connection
  - Load balancing across transports
- **Improves reliability for the whole network**

#### Content Persistence
- Hash database synchronization means:
  - File metadata persists even when original uploader is offline
  - Other peers can advertise those files
  - Rare content is less likely to disappear
- **Strengthens the archive value of Soulseek**

#### Bootstrap Effect
- As more slskdn nodes join:
  - DHT/mesh becomes more populated
  - More peers get discovered and bridged to Soulseek
  - Soulseek network graph becomes more connected
- **Network effects compound over time**

---

## Design Principles

### 1. Soulseek-First Identity
- **Mesh ID** is the technical identifier (cryptographic, stable)
- **Soulseek username** is the community identifier (human-readable, familiar)
- When both exist, we:
  - Use mesh ID internally for routing/security
  - Show Soulseek username externally for UX
  - Bridge discoveries back to Soulseek server

### 2. Opt-In, Not Opt-Out
- BitTorrent rendezvous is **disabled by default**
- Users explicitly enable alternative discovery channels
- All discoveries are bridged to Soulseek (no separate "mesh-only" mode)

### 3. No Fragmentation
- We don't create a "mesh network" separate from Soulseek
- We use mesh/DHT/BT as **discovery mechanisms only**
- All discovered peers are integrated into Soulseek community
- **One network, multiple discovery paths**

### 4. Community Enhancement, Not Replacement
- Soulseek protocol for file transfers (compatibility)
- Soulseek server for user presence (community)
- Mesh/DHT/BT for discovery (innovation)
- **Best of all worlds**

---

## Implementation Status

### ✅ Completed
- `SoulseekMeshBridgeService` - Bridges mesh peers to Soulseek server
- `MeshSearchBridgeService` - Framework for supplementing searches
- Registered as singleton hosted services in DI
- Periodic bridging timer (every 5 minutes)

### ⏳ TODO
1. **Complete `SearchMeshHashDatabaseAsync()`**
   - Implement actual hash DB query logic
   - Parse search terms and match against hash DB
   - Return results in Soulseek-compatible format

2. **Hook `MeshSearchBridgeService` into `SearchService`**
   - Call `GetMeshSupplementalResponsesAsync()` from search result aggregation
   - Merge mesh results with Soulseek results
   - Update search response UI to show supplemental sources

3. **Enhance BitTorrent extension**
   - Exchange mesh identities over BT extension protocol
   - Auto-register BT-discovered peers in `SoulseekMeshBridgeService`
   - Bridge BT discoveries to Soulseek server

4. **Metrics and Monitoring**
   - Track bridged peer count
   - Track supplemental search result count
   - Monitor bridge success/failure rates
   - Log impact on Soulseek community connectivity

---

## Configuration

No additional configuration required! The bridge services run automatically when:
- DHT/mesh is enabled (`DhtRendezvous.Enabled = true`)
- User is logged into Soulseek

To enable BitTorrent discovery (optional):
```yaml
bittorrent:
  enableRendezvousTorrent: true
```

---

## Example Flow

### Scenario: User discovers a rare FLAC via DHT

1. **Discovery Phase**
   - User's DHT announces content hash
   - Peer B discovers the announcement
   - Peer B connects via mesh overlay

2. **Bridge to Soulseek** (Automatic)
   - `SoulseekMeshBridgeService` detects new connection
   - Extracts Peer B's Soulseek username (if mapped)
   - Calls `GetUserInfoAsync("PeerB")` → Soulseek server now knows about PeerB
   - Logs: "✓ Bridged user PeerB to Soulseek"

3. **Search Enhancement** (Future)
   - User C searches for that FLAC on Soulseek
   - `SearchService` queries both:
     - Soulseek network (traditional)
     - Mesh hash database (supplemental)
   - Results show PeerB as a source
   - User C downloads from PeerB via Soulseek protocol

4. **Community Impact**
   - PeerB is now visible to **entire Soulseek network**, not just User
   - Other users can discover PeerB through normal Soulseek searches
   - Rare FLAC is more discoverable
   - **Everyone benefits from the DHT discovery**

---

## Future Enhancements

### Smart Bridging
- Only bridge high-reputation peers (spam prevention)
- Prioritize bridging peers with rare/valuable content
- Throttle bridging rate during high load

### Bidirectional Benefits
- Not just "mesh discoveries → Soulseek"
- Also "Soulseek discoveries → mesh hash DB"
- Synchronize in both directions
- **Mesh and Soulseek strengthen each other**

### Community Metrics
- "Peers bridged to Soulseek today: 42"
- "Supplemental files discovered: 1,337"
- "Community connectivity improvement: +15%"
- **Show users the value they're creating**

---

## Key Takeaway

> **We're not building a mesh network to replace Soulseek.**  
> **We're building better roads to discover Soulseek peers.**  
> **Every discovery we make benefits the entire community.**

This is how we "improve Soulseek the community while straying from Soulseek the network."
