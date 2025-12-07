# BitTorrent DHT Rendezvous Layer - Design Document

## Synopsis

The slskdn mesh network faces a **cold start problem**: when a client has no mesh neighbors, it cannot discover other slskdn clients to sync FLAC hashes or coordinate multi-source downloads. 

**Solution:** Use the **BitTorrent mainline DHT** as a decentralized rendezvous mechanism. All slskdn clients agree on a magic "channel" (infohash derived from `"slskdn-mesh-v1"`). Clients that are publicly reachable ("beacons") announce their presence on this channel. Clients that need neighbors ("seekers") query the channel to find beacons, then establish direct TCP connections for mesh sync.

**Key Properties:**
- **No central infrastructure** - leverages existing BitTorrent DHT (millions of nodes)
- **Works for firewalled users** - they can still query DHT and make outbound connections
- **Separate from Soulseek** - doesn't modify the Soulseek protocol at all
- **Minimal overhead** - only used for peer discovery, not data transfer
- **Privacy-preserving** - only exposes overlay IP:port, same as normal P2P

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                         BITTORRENT MAINLINE DHT                             │
│                    (Millions of nodes, completely public)                   │
│                                                                             │
│    Well-known "channel": SHA1("slskdn-mesh-v1") = 0x1a2b3c4d5e...          │
│                                                                             │
│         BEACONS announce:          SEEKERS query:                           │
│         "I'm at IP:port"           "Who's on this channel?"                 │
└──────────────────────────────┬──────────────────────────────────────────────┘
                               │
           ┌───────────────────┼───────────────────┐
           │                   │                   │
           ▼                   ▼                   ▼
    ┌─────────────┐     ┌─────────────┐     ┌─────────────┐
    │   BEACON    │     │   BEACON    │     │   SEEKER    │
    │  (slskdn)   │     │  (slskdn)   │     │  (slskdn)   │
    │             │     │             │     │             │
    │ Public IP   │     │ Public IP   │     │ Behind NAT  │
    │ Overlay:50305│    │ Overlay:50305│    │ Can connect │
    │ DHT announce │    │ DHT announce │    │ outbound    │
    └──────┬──────┘     └──────┬──────┘     └──────┬──────┘
           │                   │                   │
           │    TCP overlay handshake (mesh_hello) │
           │◄──────────────────┼───────────────────┤
           │                   │                   │
           └───────────────────┼───────────────────┘
                               │
                               ▼
                    ┌─────────────────────┐
                    │    MESH SYNC        │
                    │  (Existing Phase 3) │
                    │                     │
                    │  HELLO, REQ_DELTA,  │
                    │  PUSH_DELTA, etc.   │
                    └─────────────────────┘
```

---

## Roles

### Beacon
A beacon is an slskdn client that:
- Has a **publicly reachable IP** (or port-forwarded)
- Runs a **DHT node** that periodically announces to the rendezvous infohash
- Listens on an **overlay TCP port** for incoming mesh connections
- Accepts `mesh_hello` handshakes and registers peers for mesh sync

### Seeker
A seeker is an slskdn client that:
- May be **behind NAT/firewall** (common for home users)
- Runs a **DHT client** that queries the rendezvous infohash
- **Does NOT announce** to the DHT (since it can't accept inbound)
- Makes **outbound TCP connections** to discovered beacons
- Initiates `mesh_hello` handshake

### Hybrid
Most clients can be both:
- Act as beacon when publicly reachable
- Fall back to seeker behavior when behind strict NAT

---

## Protocol Details

### Rendezvous Infohash

All slskdn clients agree on one or more "magic" infohashes:

```
IH_MAIN      = SHA1("slskdn-mesh-v1")
IH_BACKUP_1  = SHA1("slskdn-mesh-v1-backup-1")
IH_BACKUP_2  = SHA1("slskdn-mesh-v1-backup-2")
```

These are used like "channels" on the DHT. Beacons announce their `(IP, overlay_port)` under these infohashes. Seekers query these infohashes to get a list of beacon endpoints.

### Overlay Handshake

Once a TCP connection is established (seeker → beacon), the handshake proceeds:

**Step 1: Seeker sends `mesh_hello`**
```json
{
  "magic": "SLSKDNM1",
  "type": "mesh_hello",
  "version": 1,
  "username": "SeekersSlskUsername",
  "features": ["mesh", "flac_hash", "multipart", "swarm"],
  "soulseek_ports": {
    "peer": 50300,
    "file": 50301
  }
}
```

**Step 2: Beacon validates and responds**
```json
{
  "magic": "SLSKDNM1",
  "type": "mesh_hello_ack",
  "version": 1,
  "username": "BeaconsSlskUsername",
  "features": ["mesh", "flac_hash", "multipart", "swarm"],
  "soulseek_ports": {
    "peer": 50300,
    "file": 50301
  }
}
```

**Step 3: Connection handed to mesh sync**

Both sides now know each other's Soulseek username and features. The TCP stream is handed to the existing `MeshSyncService` for FLAC hash exchange.

### Message Validation

| Field | Validation |
|-------|------------|
| `magic` | Must equal `"SLSKDNM1"` exactly |
| `type` | Must be `"mesh_hello"` or `"mesh_hello_ack"` |
| `version` | Must be ≥ 1 |
| `username` | Non-empty string |
| `features` | Array of strings |
| Payload size | Must be < 4096 bytes |

Invalid messages → close connection immediately.

---

## Operational Flows

### Beacon Announce Flow

```
Every 15 minutes (configurable):
  1. For each rendezvous infohash (IH_MAIN, IH_BACKUP_1, ...):
     2. dht.announce_peer(infohash, overlay_port)
  3. Log: "Announced to DHT as beacon on port {overlay_port}"
```

### Seeker Discovery Flow

```
Every 10 minutes (configurable), IF mesh_neighbors < min_threshold:
  1. candidates = []
  2. For each rendezvous infohash:
     3. peers = dht.get_peers(infohash)
     4. candidates.extend(peers)
  5. candidates = shuffle(dedupe(candidates))
  6. For each (ip, port) in candidates:
     7. If already_connected(ip, port): skip
     8. If mesh_neighbors >= max_neighbors: break
     9. Try:
        10. tcp = connect(ip, port, timeout=10s)
        11. send(mesh_hello)
        12. ack = recv(mesh_hello_ack, timeout=5s)
        13. If valid(ack):
            14. register_mesh_neighbor(ack.username, tcp)
            15. hand_to_mesh_sync(ack.username, tcp.stream)
     10. Catch: continue to next candidate
```

### NAT Detection Flow (Beacon Capability)

```
On startup:
  1. Try UPnP port mapping for overlay_port
  2. If success:
     3. is_beacon_capable = true
     4. Return
  5. Try STUN check (or self-connect test)
  6. If reachable:
     7. is_beacon_capable = true
  8. Else:
     9. is_beacon_capable = false
     10. Log: "Running as seeker only (NAT detected)"
```

---

## Configuration

```yaml
# slskdn configuration
mesh:
  overlay:
    enabled: true
    port: 50305                    # TCP port for overlay connections
    max_connections: 50            # Max simultaneous overlay peers
    handshake_timeout: 5           # Seconds to wait for handshake
    
  dht:
    enabled: true
    bootstrap_nodes:
      - "router.bittorrent.com:6881"
      - "dht.transmissionbt.com:6881"  
      - "router.utorrent.com:6881"
      - "dht.aelitis.com:6881"
    announce_interval: 900         # 15 minutes (beacon)
    discovery_interval: 600        # 10 minutes (seeker)
    min_neighbors: 3               # Trigger discovery when below
    max_neighbors: 10              # Stop discovering when reached
    rendezvous_keys:
      - "slskdn-mesh-v1"
      - "slskdn-mesh-v1-backup-1"
```

---

## Hash Database Transport - Design Decision

### The Question: How Do FLAC Hashes Travel Between Clients?

Phase 3 defines the mesh sync *protocol* (HELLO, REQ_DELTA, PUSH_DELTA, etc.), but doesn't specify the *transport*. We evaluated four options:

### Option 1: Over Soulseek Connections ❌ REJECTED

| Approach | Problem |
|----------|---------|
| Custom message type | Breaks protocol compatibility with server/other clients |
| Abuse "private message" | Hacky, fragile, could get flagged |
| Overload file transfer | Too complex, confuses other clients |

**Verdict:** Soulseek protocol is fixed. Adding custom messages risks compatibility issues and detection. We want to be invisible to the Soulseek network.

### Option 2: Over Overlay TCP Connection ✅ CHOSEN APPROACH

**This is our transport mechanism.**

```
Phase 6 DHT Rendezvous    →    Overlay TCP Connection    →    Phase 3 Mesh Sync
     (find peers)                  (persistent pipe)            (hash exchange)
```

The overlay TCP connection established during DHT rendezvous serves dual purpose:
1. **Handshake** - Proves both sides are slskdn-capable
2. **Transport** - Becomes the pipe for all mesh sync traffic

**Benefits:**
- Completely separate from Soulseek protocol
- Single persistent connection per mesh neighbor
- Can be TLS encrypted (see Security section)
- No compatibility concerns
- Low overhead

### Option 3: Store Hashes in BitTorrent DHT ❌ REJECTED

| Problem | Why |
|---------|-----|
| Wrong scale | DHT is for `(infohash → IP:port)`, not content databases |
| Abuse | Would dump our data onto strangers' torrent clients |
| Churn | DHT entries expire; constant re-announcement needed |
| Size | Our DB could be gigabytes; DHT values are tiny |

**Verdict:** DHT is for peer discovery only, not data storage.

### Option 4: HTTP API Between Clients ❌ REJECTED

| Problem | Why |
|---------|-----|
| Extra port | Requires additional port forwarding |
| NAT harder | HTTP NAT traversal is more complex |
| Auth overhead | Need to handle API authentication |
| Overkill | Raw TCP is simpler and sufficient |

**Verdict:** Unnecessary complexity when overlay TCP works.

---

## Security Considerations

### ⚠️ CRITICAL: Security Hardening Required

**The overlay protocol creates a new attack surface. This MUST be hardened before production use.**

### Threat Model

| Threat | Severity | Description |
|--------|----------|-------------|
| **Message Injection** | CRITICAL | Malicious peer sends crafted messages to exploit parser |
| **Man-in-the-Middle** | HIGH | Attacker intercepts overlay traffic, modifies hashes |
| **DoS via Connection Flood** | HIGH | Attacker opens thousands of connections |
| **Buffer Overflow** | HIGH | Oversized messages crash or exploit client |
| **Username Spoofing** | MEDIUM | Attacker claims to be different Soulseek user |
| **Eclipse Attack** | MEDIUM | Attacker controls all your mesh neighbors |
| **DHT Poisoning** | MEDIUM | Attacker floods DHT with fake endpoints |
| **Privacy Leak** | LOW | IP addresses exposed via DHT |

### Security Requirements (MANDATORY)

#### 1. TLS Encryption (REQUIRED)

**All overlay connections MUST use TLS 1.3.**

```csharp
// Server-side (beacon)
var sslStream = new SslStream(tcpClient.GetStream(), false);
await sslStream.AuthenticateAsServerAsync(
    serverCertificate,
    clientCertificateRequired: false,
    SslProtocols.Tls13,
    checkCertificateRevocation: true);

// Client-side (seeker)
var sslStream = new SslStream(tcpClient.GetStream(), false, ValidateServerCertificate);
await sslStream.AuthenticateAsClientAsync(targetHost);
```

**Certificate Options:**
- Self-signed per-client (pin on first connect, TOFU model)
- Let's Encrypt (if clients have domain names)
- Shared CA for slskdn network (complex but strongest)

**Minimum:** Self-signed + certificate pinning after first successful handshake.

#### 2. Message Validation (REQUIRED)

**Every incoming message MUST be validated before processing.**

```csharp
public class MessageValidator
{
    public const int MAX_MESSAGE_SIZE = 4096;        // 4KB max
    public const int MAX_USERNAME_LENGTH = 64;
    public const int MAX_FEATURES_COUNT = 20;
    public const int MAX_DELTA_ENTRIES = 1000;
    
    public static bool ValidateMeshHello(MeshHello msg)
    {
        // Magic check - EXACT match required
        if (msg.Magic != "SLSKDNM1") return false;
        
        // Type check
        if (msg.Type != "mesh_hello" && msg.Type != "mesh_hello_ack") return false;
        
        // Version bounds
        if (msg.Version < 1 || msg.Version > 100) return false;
        
        // Username validation - alphanumeric + limited special chars
        if (string.IsNullOrEmpty(msg.Username)) return false;
        if (msg.Username.Length > MAX_USERNAME_LENGTH) return false;
        if (!Regex.IsMatch(msg.Username, @"^[a-zA-Z0-9_\-\.]+$")) return false;
        
        // Features validation
        if (msg.Features?.Count > MAX_FEATURES_COUNT) return false;
        
        // Port bounds
        if (msg.SoulseekPorts?.Peer < 0 || msg.SoulseekPorts?.Peer > 65535) return false;
        if (msg.SoulseekPorts?.File < 0 || msg.SoulseekPorts?.File > 65535) return false;
        
        return true;
    }
}
```

#### 3. Length-Prefixed Framing (REQUIRED)

**Never read unbounded data. Always use length-prefixed messages.**

```csharp
// Message format: [4-byte length][JSON payload]
public async Task<T> ReadMessageAsync<T>(Stream stream, CancellationToken ct)
{
    // Read length prefix (big-endian)
    var lengthBytes = new byte[4];
    await stream.ReadExactlyAsync(lengthBytes, ct);
    var length = BinaryPrimitives.ReadInt32BigEndian(lengthBytes);
    
    // Validate length BEFORE allocating
    if (length <= 0 || length > MAX_MESSAGE_SIZE)
        throw new ProtocolViolationException($"Invalid message length: {length}");
    
    // Now safe to allocate and read
    var buffer = new byte[length];
    await stream.ReadExactlyAsync(buffer, ct);
    
    return JsonSerializer.Deserialize<T>(buffer);
}
```

#### 4. Rate Limiting (REQUIRED)

```csharp
public class OverlayRateLimiter
{
    // Connection rate limits
    public const int MAX_CONNECTIONS_PER_IP = 3;
    public const int MAX_CONNECTIONS_PER_MINUTE = 10;
    public const int MAX_TOTAL_CONNECTIONS = 100;
    
    // Message rate limits
    public const int MAX_MESSAGES_PER_SECOND = 10;
    public const int MAX_DELTA_REQUESTS_PER_HOUR = 60;
    
    // Backoff on violations
    public const int VIOLATION_BACKOFF_SECONDS = 300;  // 5 min
    public const int MAX_VIOLATIONS_BEFORE_BAN = 3;
    
    private readonly ConcurrentDictionary<IPAddress, RateLimitState> _states;
}
```

#### 5. Input Sanitization (REQUIRED)

**Never trust any data from peers.**

```csharp
public class HashDbEntry
{
    // Validate hash format - must be hex
    public static bool ValidateFlacKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return false;
        if (key.Length != 16) return false;  // 64-bit = 16 hex chars
        return Regex.IsMatch(key, @"^[a-f0-9]+$");
    }
    
    // Validate byte hash - must be SHA256 hex
    public static bool ValidateByteHash(string hash)
    {
        if (string.IsNullOrEmpty(hash)) return false;
        if (hash.Length != 64) return false;  // SHA256 = 64 hex chars
        return Regex.IsMatch(hash, @"^[a-f0-9]+$");
    }
    
    // Validate file size - reasonable bounds
    public static bool ValidateFileSize(long size)
    {
        return size > 0 && size < 10_000_000_000;  // Max 10GB
    }
}
```

#### 6. Peer Verification (RECOMMENDED)

**Verify claimed Soulseek username via actual Soulseek connection.**

```csharp
// After overlay handshake claims username "FooUser":
// 1. Connect to FooUser via normal Soulseek
// 2. Request their UserInfo
// 3. Check for slskdn_caps tag
// 4. If mismatch → disconnect overlay, add to blocklist
```

#### 7. Connection Timeouts (REQUIRED)

```csharp
public static class OverlayTimeouts
{
    public static readonly TimeSpan Connect = TimeSpan.FromSeconds(10);
    public static readonly TimeSpan Handshake = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan Read = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan Idle = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan Keepalive = TimeSpan.FromMinutes(2);
}
```

### Security Mitigations Summary

| Threat | Mitigation |
|--------|------------|
| **Message Injection** | Strict validation, length-prefixed framing, regex patterns |
| **Man-in-the-Middle** | TLS 1.3 mandatory, certificate pinning |
| **DoS (Connection)** | Rate limiting per IP, max connections, backoff |
| **DoS (Message)** | Message rate limits, max sizes, timeouts |
| **Buffer Overflow** | Length validation before allocation, bounded buffers |
| **Username Spoofing** | Soulseek verification handshake |
| **Eclipse Attack** | Multiple DHT keys, peer diversity checks |
| **DHT Poisoning** | Validate endpoints respond correctly |

---

## Implementation Components

### 1. DhtRendezvousService

**Responsibilities:**
- Manage DHT node lifecycle
- Beacon announce loop
- Seeker discovery loop
- Provide discovered endpoints to connector

**Interface:**
```csharp
public interface IDhtRendezvousService : IHostedService
{
    bool IsBeaconCapable { get; }
    IReadOnlyList<IPEndPoint> DiscoveredPeers { get; }
    Task ForceAnnounceAsync(CancellationToken ct);
    Task ForceDiscoverAsync(CancellationToken ct);
}
```

### 2. MeshOverlayServer

**Responsibilities:**
- Listen on overlay port (beacon mode)
- Accept TCP connections
- Perform handshake validation
- Hand connections to mesh sync

**Interface:**
```csharp
public interface IMeshOverlayServer : IHostedService
{
    bool IsListening { get; }
    int ActiveConnections { get; }
    int TotalAccepted { get; }
}
```

### 3. MeshOverlayConnector

**Responsibilities:**
- Connect to discovered endpoints (seeker mode)
- Send handshake
- Validate response
- Register successful connections

**Interface:**
```csharp
public interface IMeshOverlayConnector
{
    Task ConnectToCandidatesAsync(
        IEnumerable<IPEndPoint> candidates,
        CancellationToken ct);
    
    int SuccessfulConnections { get; }
    int FailedAttempts { get; }
}
```

### 4. MeshNeighborRegistry

**Responsibilities:**
- Track connected mesh neighbors
- Prevent duplicate connections
- Provide neighbor count for discovery threshold

**Interface:**
```csharp
public interface IMeshNeighborRegistry
{
    int Count { get; }
    bool IsConnected(string username);
    bool IsConnected(IPEndPoint endpoint);
    void Register(string username, IPEndPoint endpoint, Stream stream);
    void Unregister(string username);
    IReadOnlyList<MeshNeighbor> GetNeighbors();
}
```

---

## API Endpoints

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/v0/dht/status` | GET | DHT node status, beacon capability |
| `/api/v0/dht/peers` | GET | List of discovered overlay endpoints |
| `/api/v0/dht/announce` | POST | Force beacon announce |
| `/api/v0/dht/discover` | POST | Force discovery cycle |
| `/api/v0/overlay/status` | GET | Overlay server status |
| `/api/v0/overlay/connections` | GET | Active overlay connections |
| `/api/v0/overlay/neighbors` | GET | Mesh neighbors with usernames |

---

## Dependencies

### BitTorrent DHT Library Options

| Library | Language | Notes |
|---------|----------|-------|
| **MonoTorrent** | C# | Full BitTorrent client, well-maintained, includes DHT |
| **BencodeNET** | C# | Bencode only; would need custom DHT impl |
| **DhtSharp** | C# | Standalone DHT, less maintained |

**Recommendation:** Use MonoTorrent's DHT engine - it's battle-tested and handles NAT traversal, routing table management, and KRPC protocol correctly.

---

## Testing Strategy

### Unit Tests
- Handshake message serialization/parsing
- Message validation logic
- NAT detection mocking

### Integration Tests
- Two nodes: one beacon, one seeker
- Verify discovery and connection
- Verify handshake completes
- Verify mesh sync starts

### Network Tests
- Run multiple slskdn instances on real network
- Measure discovery time
- Verify beacon announcement works
- Test NAT traversal scenarios

---

## Future Enhancements

1. **Peer exchange (PEX)** - Share known mesh peers with each other
2. **Geographic routing** - Prefer nearby peers for lower latency
3. **Reputation system** - Track reliable beacons
4. **DHT-based hash lookups** - Store popular hashes directly in DHT (requires careful design)
5. **Tor/I2P support** - Anonymous mesh participation

---

## References

- [BEP 5: DHT Protocol](http://www.bittorrent.org/beps/bep_0005.html)
- [MonoTorrent GitHub](https://github.com/alanmcgovern/monotorrent)
- [slskdn Implementation Roadmap](./IMPLEMENTATION_ROADMAP.md)
- [slskdn Multi-Source Documentation](./MULTI_SOURCE.md)

