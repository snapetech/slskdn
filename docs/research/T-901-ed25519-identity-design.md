# T-901: Ed25519 Signed Identity System — Design

> Unify and formalize Ed25519-based identity across mesh, ActivityPub, and pods.  
> **Status**: Implemented (design + formalization). **See**: `9-research-design-scope.md` T-901.

---

## 1. Unified Identity Model

### Current subsystems and key storage

| Subsystem | Store / type | Key format | Used by |
|-----------|--------------|------------|---------|
| **Mesh overlay** | `IKeyStore` / `FileKeyStore` | Raw 32-byte, JSON file (Base64), `OverlayOptions.KeyPath` | `ControlSigner` (KeyedSigner), `MeshMessageSigner`, `DescriptorSigningService`, `PeerDescriptorPublisher` |
| **Pods** | `IPodMembershipSigner` → **uses `IKeyStore`** (mesh) | Same as mesh (Raw, Ed25519KeyPair) | Pod membership signing/verification |
| **ActivityPub** | `IActivityPubKeyStore` / `ActivityPubKeyStore` | PEM (PKIX or Raw via `IEd25519KeyPairGenerator`); DataProtection for private; in-memory `_keypairs` | `ActivityDeliveryService`, `ActivityPubController`, `FederationService`, `LibraryActorService`, HTTP signature |

### Decision: per-subsystem keys (current)

- **Mesh + Pods**: Share `IKeyStore` (FileKeyStore). One Ed25519 keypair for overlay control, DHT, descriptors, and pod membership. **Security**: Compromise of mesh key affects overlay and pods; acceptable for many deployments.
- **ActivityPub**: Separate `IActivityPubKeyStore`, per-actor keypairs. **Isolation**: ActivityPub keys are independent; mesh compromise does not expose ActivityPub.

### Future option: single node key

- A **unified node identity** (one Ed25519 keypair for mesh, ActivityPub, pods) could be introduced via a shared `INodeIdentityStore` and configuration to use it for ActivityPub instead of ActivityPubKeyStore. **Open**: key isolation vs. simplicity; revocation.

---

## 2. Key Lifecycle

### Mesh (IKeyStore / FileKeyStore)

- **Persistence**: JSON file at `OverlayOptions.KeyPath` (default `mesh-overlay.key`). Fields: `PublicKey`, `PrivateKey` (Base64), `CreatedMs`. Raw 32-byte Ed25519.
- **Rotation**: `OverlayOptions.RotateDays` (default 30). On expiry, current → `{KeyPath}.prev`, new keypair generated. `VerificationPublicKeys` includes previous for grace period.
- **Backup**: User copies `mesh-overlay.key` (and `mesh-overlay.key.prev` if present). No built-in backup.

### ActivityPub (IActivityPubKeyStore)

- **Generation**: `IEd25519KeyPairGenerator.GenerateKeypair()` → (publicPem, privatePem). `NsecEd25519KeyPairGenerator`: prefers PKIX, falls back to Raw PEM.
- **Persistence**: In-memory `ConcurrentDictionary`; private key protected with `IDataProtector`. **Ephemeral across process restarts** unless future persistence is added.
- **Rotation**: `RotateKeypairAsync(actorId)`. Old keypair removed; new one created on next `EnsureKeypairAsync`.
- **Backup**: Not applicable (in-memory). Future: export PEM for backup.

### Alignment (IEd25519KeyPairGenerator, ActivityPubKeyStore, FileKeyStore)

- **IEd25519KeyPairGenerator**: PEM (PKIX or Raw). Used only by ActivityPubKeyStore.
- **ActivityPubKeyStore**: PKIX/Raw for import in `VerifySignatureAsync`; `ConvertFromPem` for public. Aligned with generator output.
- **FileKeyStore**: Raw Base64 in JSON; does **not** use `IEd25519KeyPairGenerator`. Ed25519 32-byte raw at crypto level is the common standard; PEM is for ActivityPub’s HTTP/key distribution.

---

## 3. Self-Certifying PeerId

**Formal rule** (T-901):

> **PeerId** = `ToLower(Base32(First20(SHA256(publicKey))))`  
> where `publicKey` is the 32-byte Ed25519 raw public key,  
> Base32 uses alphabet `A–Z, 2–7`, and we take the first 20 bytes of SHA-256.

**Implementation**: `Ed25519Signer.DerivePeerId(byte[] publicKey)` (Mesh.Transport).

- Used for DHT and overlay as a self-certifying node id: anyone with `publicKey` can recompute PeerId and verify that a holder of the private key is the same entity.

---

## 4. Revocation

**Open**. No structured revocation yet. Rotation (mesh: `FileKeyStore`; ActivityPub: `RotateKeypairAsync`) invalidates the old key; old PeerId / actor linkage is not centrally revoked.

---

## 5. DID (did:key:z…)

**Deferred** (low priority). Could encode Ed25519 public key as `did:key:z6Mk...` for federation; out of scope for T-901.

---

## 6. Types and Locations

| Type | Location | Role |
|------|----------|------|
| `Ed25519Signer` | Mesh.Transport | Sign, verify, `DerivePeerId` |
| `IKeyStore`, `FileKeyStore`, `Ed25519KeyPair` | Mesh.Overlay | Mesh (+ pods) key storage |
| `IEd25519KeyPairGenerator`, `NsecEd25519KeyPairGenerator` | SocialFederation | PEM keypair generation for ActivityPub |
| `IActivityPubKeyStore`, `ActivityPubKeyStore` | SocialFederation | ActivityPub key storage and HTTP signature |
| `IPodMembershipSigner`, `PodMembershipSigner` | PodCore | Pod membership; uses `IKeyStore` |
| `ControlSigner` (KeyedSigner), `DescriptorSigningService` | Mesh.Overlay, Mesh.Transport | Control envelopes, descriptors |
| `IMeshMessageSigner`, `MeshMessageSigner` | Mesh | DHT/mesh message signing |
| `KademliaRpcClient` | Mesh.Dht | Uses `IMeshMessageSigner` |
