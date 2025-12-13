# Phase 8: MeshCore Foundation — Research Summary

Date: 2025-12-10  
Branch: `experimental/brainz`  
Scope: T-1032..T-1038

## Goals
- Define the decentralized MeshCore foundations: DHT, identity, routing, storage, bootstrap, overlay, NAT traversal.
- Establish guardrails and defaults for future implementation work.

## Decisions & Findings

### DHT Architecture & Keys (T-1032, T-1035)
- Use Kademlia-style DHT with SHA1-derived 160-bit IDs.
- Namespaced keys:
  - `mesh:peer:<peerId>` → peer descriptors (addr/port/transports, signed).
  - `mesh:content:<contentId>` → content descriptors (hashes, sizes, codecs, owner), signed.
  - `mesh:scene:<sceneId>` → scene membership/metadata (consistent with Phase 6 scenes).
- Values are MessagePack blobs, signed by Ed25519 (see identity).
- TTL: default 1 hour; publishers refresh at 30 minutes; consumers treat >1h as stale.
- Size cap per value: 8–10 KB; shard if larger.

### Identity (T-1033)
- Ed25519 keypairs for mesh identities.
- PeerId = `peer:mesh:<base32(sha256(pubkey))[0:16]>`.
- All DHT values and overlay control messages are signed; include `pubkey`, `sig`, `ts` fields.
- Rotation: support key rotation via `prev_pubkey` link; keep last 2 keys accepted for overlap.

### Routing (T-1034)
- Kademlia routing table with k-buckets (k=20).
- Prefer IPv6, but keep dual-stack descriptors.
- Store transport hints (DHT, overlay, mirrored) to bias selection.
- NAT awareness flag in descriptors to decide relay vs direct attempts.

### Storage & TTL (T-1035)
- Values: soft cap 8–10 KB; shard large payloads (e.g., multiple peer hints).
- TTL 1h; publisher refresh 30m; reader treats >1h as stale; eviction after 2h.
- Reject unsigned or expired (`ts` older than TTL) values.

### Bootstrap & Discovery (T-1036)
- Bootstrap nodes list in config (later replaceable with DNS seeds).
- On start: join bootstrap peers, perform self-announce, fetch closest peers.
- Periodic `FIND_NODE` for own ID to keep table warm.

### Overlay Protocol Design (T-1037)
- Control plane: small signed messages over QUIC/WebRTC data channels (future).
- Data plane: leverage existing multi-swarm overlay (Phase 6) as mirrored path; optional.
- Message envelope: `{type, payload, pubkey, sig, ts}`.

### NAT Traversal (T-1038)
- Prefer direct: UDP hole punching (STUN); fallback to TURN/relay when available.
- If NAT type symmetric → mark descriptor as relay-required; prefer mirrored transport.
- Keep-alive pings to maintain NAT bindings where applicable.

## Risks & Guardrails
- Signature verification on all DHT and overlay control messages.
- Size limits to prevent DHT spam.
- Conservative TTL to reduce stale data.
- NAT detection to avoid futile direct attempts.
- No PII in DHT values; use pseudonymous PeerIds only.

## Next Steps (implementation)
- Build DHT client abstraction with signed put/get and TTL enforcement.
- Implement peer descriptor publisher/refresh worker.
- Integrate MeshTransportService preference with DHT vs overlay selection.
- Add NAT detection utility and descriptor flagging.















