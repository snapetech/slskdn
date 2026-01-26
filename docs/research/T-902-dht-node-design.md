# T-902: DHT Node and Routing Table — Design

> Implement a proper Kademlia DHT node with routing table and RPC.  
> **Status**: Implemented (routing table, node role, RPC). **See**: `9-research-design-scope.md` T-902.

---

## 1. Kademlia Routing Table

**Implementation**: `KademliaRoutingTable` (Mesh.Dht)

- **NodeIds**: 160-bit (20 bytes). Enforced in constructor.
- **k-buckets**: `k = 20` (BucketSize). XOR-based distance; bucket splitting when full and when we own the bucket; ping-before-evict when split not possible.
- **Operations**: `Touch` / `TouchAsync(nodeId, address, pingFunc?)`, `GetClosest(targetId, count)`.
- **Metrics**: `GetStats()` (TotalNodes, BucketCount, BucketSizes, MaxBucketSize, MinBucketSize), `GetAllNodes()`.

**NodeId derivation (T-901 alignment)**: In production, `KademliaRoutingTable` is given `selfId = SHA1(Ed25519_public_key)` from `IKeyStore.Current.PublicKey` (Program). Alternative: `SHA256(publicKey)[0..20]` for new code. `InMemoryDhtClient` uses a separate internal table with `RandomNodeId()` (20 random bytes) when used as `IDhtClient`; the main DHT node uses the Ed25519-derived `KademliaRoutingTable` from DI.

---

## 2. DHT Node Role (Respond to RPCs)

**Implementation**: `DhtMeshService` (Mesh.ServiceFabric.Services), registered as `ServiceName = "dht"` with `MeshServiceRouter` in `Application.RegisterMeshServicesAsync`.

We **respond** to:

| RPC        | Method       | Behavior |
|------------|--------------|----------|
| FIND_NODE  | `FindNode`   | Return k closest nodes to `TargetId`; `TouchAsync(RequesterId, RemotePeerId)`. |
| FIND_VALUE | `FindValue`  | If key in local `IDhtClient`: return value. Else: return k closest nodes; `TouchAsync(RequesterId, RemotePeerId)`. |
| STORE      | `Store`      | Verify Ed25519 signature; `IDhtClient.PutAsync` with TTL; `TouchAsync(RequesterId, RemotePeerId)`. |
| PING       | `Ping`       | `TouchAsync(RequesterId, RemotePeerId)`; return timestamp. |

So we **join the DHT as a proper node**: we handle FIND_NODE, FIND_VALUE, STORE, PING from other clients over the mesh overlay.

---

## 3. Kademlia RPC Client (Initiate RPCs)

**Implementation**: `KademliaRpcClient` (Mesh.Dht)

- **FindNodeAsync(targetId)**: Iterative FIND_NODE (α=3, k=20, max 20 iterations). Uses `IMeshServiceClient.CallAsync` to `dht` / `FindNode`.
- **FindValueAsync(key)**: Local `IDhtClient.GetMultipleAsync` first; else iterative FindNode.
- **StoreAsync(DhtStoreMessage)**: FindNode for key, then `dht`/`Store` to each of k closest (signed store).
- **PingAsync(KNode)**: `dht`/`Ping`.

---

## 4. GET_PEERS and ANNOUNCE_PEER (BEP 5)

**BEP 5 (BitTorrent DHT)** uses UDP and different RPCs: `get_peers` (peers for info_hash), `announce_peer` (we have a torrent). Our **mesh DHT** uses a different wire format (mesh overlay, JSON, `ServiceCall`/`ServiceReply`) and RPC set.

**Mapping**:
- `get_peers` → **FindValue(key=info_hash)**: value can be peer list.
- `announce_peer` → **Store(key=info_hash, value=peer_info)** with signature.

**DhtRendezvous** (BitTorrent DHT): separate. It uses BEP 5 UDP for peer discovery (`slskdn-mb-v1:*` style keys) and does **not** use `KademliaRoutingTable` or `DhtMeshService`. Overlap: both are “DHT” but different protocols. DhtRendezvous = bootstrap/peer discovery over public BT DHT; mesh DHT = Kademlia over mesh overlay and shadow index.

---

## 5. Interop and Wire Format

- **slskdn DHT**: Mesh overlay transport, JSON payloads, `ServiceCall`/`ServiceReply`. **Not** BEP 5 UDP.
- **BEP 5**: Implemented only in `DhtRendezvous` (client use of public BT DHT). We do not respond to BEP 5 `get_peers`/`announce_peer` in the mesh stack; we respond to `FindNode`, `FindValue`, `Store`, `Ping`.

---

## 6. Types and Flow

| Type | Role |
|------|------|
| `KademliaRoutingTable` | 160-bit k-buckets, Touch, GetClosest. selfId from SHA1(Ed25519) in Program. |
| `KademliaRpcClient` | FIND_NODE, FIND_VALUE, STORE, PING (client). |
| `DhtMeshService` | FIND_NODE, FIND_VALUE, STORE, PING (server). |
| `DhtService` | Orchestrates DHT; uses `KademliaRpcClient`, `IDhtClient`, `IMeshMessageSigner`. |
| `IDhtClient` / `InMemoryDhtClient` | PUT/GET, TTL; InMemoryDhtClient has its own routing table for AddNode/FindClosest. |
| `DhtRendezvous` | BEP 5 BT DHT client for peer discovery; separate from mesh DHT. |

---

## 7. Open (from 9-research)

- **Reuse BT DHT vs. slskdn DHT**: We use slskdn DHT for mesh; DhtRendezvous reuses BT DHT as a client. No plan to implement BEP 5 server in mesh.
- **Overlap with DhtRendezvous**: DhtRendezvous = BEP 5; mesh DHT = Kademlia over overlay. Clear separation.
