# T-903: DHT Storage with TTL and Signatures — Design

> Store values in the DHT with TTL and optional Ed25519 signatures.  
> **Status**: Implemented (PUT/GET, TTL, signed Store). **See**: `9-research-design-scope.md` T-903.

---

## 1. Value Storage: PUT and GET

**Interface**: `IDhtClient` (VirtualSoulfind.ShadowIndex)

- **PutAsync(key, value, ttlSeconds)**: Store `value` under `key` with TTL. Opaque bytes.
- **GetAsync(key)**: Return one value for `key` or null. Expired entries evicted on read.
- **GetMultipleAsync(key)**: Return all values for `key` (e.g. multiple peer lists). Expired evicted on read.

**Implementations**:

- **InMemoryDhtClient**: In-memory `ConcurrentDictionary`; per-key list of `DhtValue` (data + ExpiresAt). Eviction on Get/GetMultiple. TTL clamped 60–3600 s in PutAsync. `maxReplicas` (20) per key for GetMultiple.
- **DhtClientStub**: No-op (for tests / Phase 6B).

**Immutability / key+seq**: We use **key only**; no built-in key+seq. Last-write-wins in single-value usage. For keys that use GetMultiple, multiple values coexist until TTL or eviction. Callers can embed sequence or version in the value.

---

## 2. TTL (Expiry)

- **On write**: `ttlSeconds` stored; `ExpiresAt = now + ttl` (InMemoryDhtClient). Store RPC clamps 60–86400 (1 min–24 h); InMemoryDhtClient further clamps to 60–3600.
- **On read**: Get/GetMultiple remove entries where `ExpiresAt <= now` then return. No background sweeper in InMemoryDhtClient.
- **Republish**: No generic DHT-level refresh. **Publishers** are responsible: ShardPublisher (interval), PodDhtPublisher, SceneAnnouncementService, ContentPeerPublisher, DescriptorPublisher, etc. **Open**: who republishes cached copies (publisher only vs. any node).

---

## 3. Signatures (Ed25519)

**Store RPC** (DhtMeshService) **requires** Ed25519 signature:

- **Fields**: `PublicKeyBase64`, `SignatureBase64`, `TimestampUnixMs` (plus Key, Value, RequesterId, TtlSeconds).
- **Verification**: `DhtStoreMessage.VerifySignature()` before `IDhtClient.PutAsync`. Rejects if: missing sig, timestamp older than 5 minutes, or Ed25519 verify fails. Signing key = node identity (T-901; IMeshMessageSigner from IKeyStore).
- **CreateSigned**: `DhtStoreMessage.CreateSigned(key, value, requesterId, ttlSeconds, IMeshMessageSigner)` → Ed25519 over `dht-store|{TimestampUnixMs}|{json}`. Used by `KademliaRpcClient.StoreAsync` and `DhtService.StoreAsync`.

**IDhtClient.PutAsync** is raw bytes; no signature at the interface. Store RPC is the **signed** path; direct PutAsync (e.g. from ShardPublisher, PodDhtPublisher) is trusted in-process. Value may itself be a signed structure (e.g. SignedPod, SignedMembershipRecord); verification is up to the consumer.

---

## 4. Overlap with Shadow Index and Others

**Same store**: `IDhtClient` is shared. Key namespacing avoids collisions, e.g.:

- `mesh:content-peers:...`, `mesh:content:{cid}`, peer descriptors, content hints
- `pod:...`, `shard:...`, scene announcements, etc.

**Shadow index**: ShardPublisher, PeerDescriptorPublisher use `IDhtClient.PutAsync` with their key format and TTL. ShardPublisher republishes on an interval. No separate “generic DHT store” service; we reuse `IDhtClient`.

---

## 5. Max Value Size and Conflict Resolution

- **Max value size**: Store RPC uses `_maxPayload` from `MeshOptions.Security.GetEffectiveMaxPayloadSize()` (and `ServicePayloadParser`) to cap request size. InMemoryDhtClient does not enforce an extra limit.
- **Conflict resolution**: **Open**. In practice last-write-wins for single-value; no version vectors or multi-writer merge at the DHT layer.

---

## 6. Types and Flow

| Type | Role |
|------|------|
| `IDhtClient` | PutAsync(key, value, ttl), GetAsync(key), GetMultipleAsync(key). |
| `InMemoryDhtClient` | In-memory, TTL, eviction on read, maxReplicas per key. |
| `DhtStoreMessage` | CreateSigned(…, IMeshMessageSigner), VerifySignature. Ed25519, 5 min freshness. |
| `DhtMeshService.HandleStoreAsync` | Requires PublicKeyBase64, SignatureBase64, TimestampUnixMs; VerifyStoreSignature → PutAsync. TTL 60–86400. |
| `KademliaRpcClient.StoreAsync` | FindNode for key, StoreOnNodeAsync (Store RPC) to k closest with DhtStoreMessage. |
| `DhtService.StoreAsync` | CreateSigned + PutAsync (local); also FindValue + store on closest if not found locally. |
