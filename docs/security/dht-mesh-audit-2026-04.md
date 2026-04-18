# DHT / Mesh Overlay Security Audit — 2026-04-18

**Scope**: `src/slskd/DhtRendezvous/` — the DHT rendezvous service, mesh overlay server/connector, peer verification, certificate manager, pin store, and message framer.

**Question asked**: Can these features give an attacker a highway into the host's network, local storage, or code execution?

**Short answer**: No remote-code-execution path. No filesystem escape. The whole attack surface is the network listener, and it is gated by TLS + certificate pin + per-IP/global rate limits + IP and username blocklists.

---

## 1. Attack surface mapped

| Class | Surface | Gate |
|---|---|---|
| Inbound TCP overlay | `MeshOverlayServer` binds `0.0.0.0:50305` | TLS 1.3 + self-signed cert pinning (TOFU), HELLO nonce, rate limiter, blocklists |
| Inbound UDP DHT | `DhtRendezvousService` binds `0.0.0.0:50306` | Bounded announce/discovery, discovered-peer cap (1000) |
| Legacy TCP listener | `DhtRendezvousService.cs:698` | Same protocol stack as overlay |
| Filesystem reads from peer requests | `MeshOverlaySearchService`, `MeshSearchRpcHandler` | `PathGuard` + share repository — peer strings never reach `File.*`; only virtual paths returned |
| Message deserialisation | `SecureMessageFramer` + `System.Text.Json` | 4096-byte frame cap before allocation; generic typed deserialisation only — no `TypeNameHandling`, no `BinaryFormatter`, no MessagePack typeless |
| Resource exhaustion | `OverlayRateLimiter`, `ConnectionFingerprint` | 3 conns/IP, 30/min, 100 total, 10 msg/sec; 10k events / 1k fingerprints, oldest-out |
| Code execution | n/a | No `Process.Start`, `Assembly.Load`, dynamic compilation, or `eval`-style reflection in the tree |

---

## 2. Findings and decisions

Each finding records: observation → decision → rationale.

### 2.1  DHT + overlay enabled by default — **kept as-is**

- **Observation**: `DhtRendezvousOptions.Enabled = true` at `DhtRendezvousService.cs:750`. `BootstrapRouters` defaults to the public BitTorrent routers, so config validation (`Options.cs:495-507`) passes on a fresh install and the service announces + opens both listeners.
- **Decision**: Leave `Enabled = true`.
- **Rationale**: slskdn's raison d'être is the mesh overlay — disabling it by default defeats the product. The exposure is mitigated by the stacked gates above (TLS + pin + rate limit + blocklist + HELLO nonce). Users who want to disable it can set `dht.enabled: false`.

### 2.2  Listener binds to `0.0.0.0` — **kept as-is**

- **Observation**: `MeshOverlayServer.cs:94`, `DhtRendezvousService.cs:360`, `698` all bind `IPAddress.Any`.
- **Decision**: Leave as-is.
- **Rationale**: A peer-to-peer overlay has to be reachable from other peers' networks. Binding to loopback or a single interface would prevent any real mesh function. An `OverlayBindAddress` option for LAN-only deployments is a plausible future enhancement but was not added now — scope creep, and it would need config + validation + docs.

### 2.3  UPnP auto port-mapping — **already off**

- **Observation**: `DhtRendezvousOptions.EnableUpnp = false` at `DhtRendezvousService.cs:798`.
- **Decision**: No change needed — UPnP is opt-in. `EnableStun = true` is kept (STUN is outbound-only and does not open inbound holes).
- **Rationale**: UPnP has a long history of router-side bugs; requiring explicit opt-in is the right default. This was verified during the audit, not changed.

### 2.4  TOFU certificate pinning — **kept as-is**

- **Observation**: `CertificatePinStore` pins on first contact. A MITM at first connection replaces the legitimate peer's cert permanently. Rotation is logged as `Warning` (`CertificateManager.cs:348`), first-use is logged as `Information`.
- **Decision**: Keep TOFU. Do not add a "strict mode that refuses unknown pins".
- **Rationale**: The trust model is symmetric P2P — there is no CA, no out-of-band enrolment channel. Refusing unknown pins would prevent new peers from ever connecting, breaking peer bootstrap. The first-use and rotation log lines give an operator enough signal to investigate anomalies. Out-of-band pin import could be added later but is not load-bearing for the threat model.

### 2.5  `X509RevocationMode.NoCheck` — **kept as-is**

- **Observation**: `MeshOverlayConnection.cs:163` sets `NoCheck` on the TLS revocation mode.
- **Decision**: Keep `NoCheck`.
- **Rationale**: Certs are self-signed, so OCSP/CRL lookups would fail regardless. Trust comes from the pin, not from the chain. Enabling revocation checking here would add latency and a network dependency without any security benefit.

### 2.6  Pin store `Save()` was non-atomic — **FIXED**

- **Observation**: Original `CertificateManager.cs:412-423` did `File.WriteAllText(_pinStorePath, json)`. A crash mid-write (or a concurrent writer) would leave `cert_pins.json` truncated or garbled, `Load()` would drop it as malformed JSON, and **every pin would silently reset to unpinned** — degrading TOFU to first-use-on-every-reboot. On a reboot into a hostile network, that's the MITM window widened significantly.
- **Decision**: Rewrite `Save()` to do an atomic temp-file + rename, with `Flush(flushToDisk: true)` before rename and `0600` mode set on the temp file (so the final file inherits it on Unix). Temp file cleaned up on failure.
- **Rationale**: Standard safe-write pattern. `File.Move(..., overwrite: true)` is atomic on POSIX and NTFS. Existing unit tests (`CertificatePinStoreTests`) still pass unchanged — behaviour is equivalent on the happy path, robust on the sad path.

### 2.7  Bounded allocations — **verified, no change**

- **Observation**: `SecureMessageFramer.cs:38-80` validates length (2–4096 bytes) before allocating the buffer. `OverlayRateLimiter` caps connections at 3/IP, 30/min, 100 total, 10 msg/sec. Neighbour registry and fingerprint log are bounded.
- **Decision**: No change.
- **Rationale**: Peer-controlled lengths cannot drive unbounded allocations. Memory DoS surface is bounded and documented.

### 2.8  No dangerous deserialisation — **verified, no change**

- **Observation**: No `BinaryFormatter`, no `TypeNameHandling`, no MessagePack typeless mode. JSON uses `DeserializeMessage<T>` with static generic types.
- **Decision**: No change.
- **Rationale**: There is no known gadget surface. New message handlers should continue to deserialise into concrete types only — do not introduce `object`, `dynamic`, or polymorphic type discrimination from peer data without a signed envelope.

### 2.9  Filesystem access — **verified, no change**

- **Observation**: `MeshSearchRpcHandler.cs:83-126` only returns virtual share paths. All peer strings go through `PathGuard` (depth ≤ 50, length ≤ 4096, no control chars, no `..` traversal). Peer-supplied strings are never used to open files.
- **Decision**: No change.
- **Rationale**: The path-handling invariant is "peer strings are compared, never opened". Preserve it. New handlers that read files from peer requests must route through `PathGuard` and the share repository.

---

## 3. Invariants for future work

When adding new mesh message types or RPC handlers:

1. **Deserialise into concrete typed records**, never `object` / `JsonElement` / polymorphic discriminators from peer data.
2. **Enforce max lengths at the framer**, not at the handler. The 4096-byte cap in `SecureMessageFramer` is the backstop.
3. **Rate-limit new RPCs** via `OverlayRateLimiter` before doing any work, not after.
4. **Never let peer strings reach `File.*`** — always look up through the share repository or `PathGuard.SanitizeAndValidate` first.
5. **Pin store writes must stay atomic** — any new persistence in `CertificateManager.cs` or similar must use the temp-file + `Flush(flushToDisk: true)` + `File.Move(overwrite: true)` pattern.
6. **Do not enable UPnP by default** on any new NAT path.
7. **Do not add a "disable pin check" escape hatch** — the pin *is* the authentication.

---

## 4. What this audit did not cover

- The broader `src/slskd/Mesh/` tree (realms, bridge, service fabric) — out of scope for this pass; audit separately.
- VirtualSoulfind backends (`MeshDhtBackend`, `TorrentBackend`, etc.) — separate trust model, separate audit.
- The `/api/v0/mesh/*` HTTP surface — covered under `docs/security/mesh-sync-security.md` and the `MeshGateway` rate-limit policies in `Options.cs:2685`.
- Cryptographic review of the MonoTorrent DHT library itself (upstream dependency).

---

## 5. Change log

| Date | Change |
|---|---|
| 2026-04-18 | Initial audit. Fixed non-atomic pin store write (`CertificateManager.cs` `Save()`). All other decisions: keep as-is with documented rationale. |
