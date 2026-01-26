# 9 Research Tasks — Design & Scope

> **✅ Research (9) order complete.**  
> **Tasks**: T-901, T-902, T-903, T-906, T-907, T-908, T-911, T-912, T-913  
> **Design/scope:** this document. **Order:** T-912 → T-911 → T-913 → T-901 → T-902 → T-903 → T-906 → T-907 → T-908. **All implemented.**  
> See `memory-bank/tasks.md` § Medium Priority (Research implementation).

---

## Overview

These 9 tasks are **research / future-enhancement** items. Implementation is optional; design and scope should be agreed before coding. This doc captures proposed scope, dependencies, and open questions grounded in the current codebase.

---

## T-901: Ed25519 Signed Identity System

**One-line**: Unify and formalize Ed25519-based identity across mesh, ActivityPub, and pods.

**Current state**:
- Ed25519 used in: `KeyedSigner`, `MeshMessageSigner`, `ActivityPubKeyStore`, `PodMembershipSigner`, `MessageSigner`, `KeyStore`, `DescriptorSigningService`, `Ed25519Signer`, `KademliaRpcClient`.
- Key lifecycle: `KeyStore` (mesh overlay), `ActivityPubKeyStore` (ActivityPub), `PodMembershipSigner` (pods). Each is separate.

**Proposed scope**:
- **Unified identity model**: One notion of “node identity” (Ed25519 keypair) that can be used for mesh overlay, ActivityPub actor, and pod membership where appropriate. Document which subsystems share vs. isolate keys.
- **Key lifecycle**: Rotation, persistence (PEM/Raw), backup. Align with `IEd25519KeyPairGenerator` and `ActivityPubKeyStore` Pkix/Raw handling.
- **Self-certifying IDs**: `Ed25519Signer.DerivePeerIdFromPublicKey` exists. Formalize PeerId = hash(pubkey) or similar for DHT and overlay.
- **Optional**: DID-like identifiers (did:key:z…) for federation; low priority.

**Dependencies**: None (builds on existing Ed25519 usage).  
**Open**: Single key vs. per-subsystem keys (security vs. simplicity). Revocation story.

**Implemented (2026-01-25):** `docs/research/T-901-ed25519-identity-design.md` — unified identity model (per-subsystem: Mesh+IKeyStore/FileKeyStore shared with Pods; ActivityPub separate); key lifecycle (FileKeyStore: JSON/Base64, KeyPath, RotateDays, backup; ActivityPubKeyStore: IEd25519KeyPairGenerator PEM PKIX/Raw, in-memory, RotateKeypairAsync); alignment of IEd25519KeyPairGenerator and ActivityPubKeyStore. Self-certifying PeerId formalized: `Ed25519Signer.DerivePeerId` — PeerId = Base32(First20(SHA256(publicKey))); XML and design doc. Revocation and DID deferred.

---

## T-902: DHT Node and Routing Table

**One-line**: Implement a proper Kademlia DHT node with routing table and RPC.

**Current state**:
- `DhtRendezvous` uses **BitTorrent DHT** for peer discovery (GET_PEERS for `slskdn-mb-v1:*` style keys).
- `KademliaRpcClient`, `KademliaRoutingTable`, `InMemoryDhtClient` exist. Shadow index / Phase 6 DHT uses custom keys and publishers.

**Proposed scope**:
- **Kademlia routing table**: NodeIds (160-bit), k-buckets, FIND_NODE. Align with `KademliaRoutingTable` if it already implements; otherwise specify and extend.
- **DHT node role**: Join the DHT as a proper node (respond to FIND_NODE, GET_PEERS, ANNOUNCE_PEER from other clients), not only as a client.
- **Interop**: BEP 5 (BitTorrent DHT) for compatibility with existing DHT; or separate “slskdn DHT” with its own wire format — design choice.

**Dependencies**: T-901 (node identity = Ed25519 or NodeId derived) helpful.  
**Open**: Reuse BT DHT vs. clean-slate slskdn DHT. Overlap with `DhtRendezvous` responsibilities.

**Implemented (2026-01-25):** `docs/research/T-902-dht-node-design.md`. KademliaRoutingTable: 160-bit NodeIds, k=20, bucket splitting, XOR, Touch, GetClosest; selfId = SHA1(Ed25519 publicKey) from IKeyStore (Program). DHT node role: DhtMeshService responds to FindNode, FindValue, Store, Ping; registered with MeshServiceRouter. KademliaRpcClient: FindNode, FindValue, Store, Ping. GET_PEERS/ANNOUNCE_PEER = BEP 5; we use slskdn DHT (FindValue≈get_peers, Store≈announce_peer). DhtRendezvous remains BEP 5 client; mesh DHT = slskdn wire (mesh overlay, JSON).

---

## T-903: DHT Storage with TTL and Signatures

**One-line**: Store values in the DHT with TTL and optional Ed25519 signatures.

**Current state**:
- Shadow index publishes to DHT (`PeerDescriptorPublisher`, `IShadowIndexBuilder`). `MeshDhtBackend` and DHT key formats exist.
- No generic DHT PUT/GET with TTL or signed payloads in the mainline.

**Proposed scope**:
- **Value storage**: PUT(key, value, ttl_sec), GET(key). Values immutable for a given key+seq or similar.
- **TTL**: Expiry; background refresh or republish. Who republishes: publisher only or any node that cached?
- **Signatures**: Optional `sign(value)` so consumers can verify publisher. Key = identity from T-901.
- **Overlap with shadow index**: Reuse same DHT or separate “generic DHT store” service. Phase 6 shadow index already has key formats; extend vs. generalize.

**Dependencies**: T-902 (DHT node). T-901 for signing keys.  
**Open**: Conflict resolution (last-write-wins vs. version vectors). Max value size.

**Implemented (2026-01-25):** `docs/research/T-903-dht-storage-design.md`. IDhtClient: PutAsync(key, value, ttlSeconds), GetAsync, GetMultipleAsync. TTL: expiry on read (InMemoryDhtClient); Store RPC 60–86400 s; no generic republish (publishers own refresh). Signatures: Store RPC requires Ed25519 (PublicKeyBase64, SignatureBase64, TimestampUnixMs); DhtStoreMessage.CreateSigned/VerifySignature; 5 min freshness. Overlap: same IDhtClient for shadow index, pods, scenes, etc.; key namespacing. Max size: _maxPayload (Store RPC). Conflict: last-write-wins; open.

---

## T-906: Native Mesh Protocol Backend

**One-line**: `IContentBackend` implementation that uses only the mesh overlay (no Soulseek, no BitTorrent).

**Current state**:
- `IContentBackend`: `SoulseekBackend`, `LocalLibraryBackend`, `HttpBackend`, `LanBackend`, `MeshTorrentBackend` (mesh + DHT + torrent). `NoopContentBackend` for tests.
- Mesh overlay: control plane, sync, transfer; `MeshTorrentBackend` uses it for discovery and data.

**Proposed scope**:
- **NativeMeshBackend**: Implements `IContentBackend`. Find candidates via mesh/DHT only (e.g. shadow index, mesh GET), fetch via overlay transfer (custom protocol or reuse mesh chunk RPC). No Soulseek, no .torrent.
- **Protocol**: Define mesh “get content by ContentId / hash” RPC and wire to existing overlay. Reuse `MeshTorrentBackend` patterns where possible.
- **Use case**: Pure mesh-only deployments; disaster mode; closed communities.

**Dependencies**: T-902, T-903 (DHT/storage) if index is in DHT. Shadow index and Phase 6 already provide some of this.  
**Open**: Chunk protocol (reuse rescue/mesh chunking or new). Incentives/ratio.

**Implemented (2026-01-25):** `ContentBackendType.NativeMesh`; `NativeMeshBackend` (IMeshDirectory, IContentIdRegistry; FindCandidatesAsync via FindPeersByContentAsync, BackendRef `mesh:{peerId}:{contentId}`; ValidateCandidateAsync format-only); `NativeMeshBackendOptions`. Design: `docs/research/T-906-native-mesh-backend-design.md` (mesh “get content by ContentId/hash” RPC, resolver fetch and overlay wiring follow-ups). DI: document only (v2 IContentBackend not wired in Program).

---

## T-907: HTTP / WebDAV / S3 Backend

**One-line**: Extend `HttpBackend` to support WebDAV and S3-compatible APIs as `IContentBackend` sources.

**Current state**:
- `HttpBackend` exists: HTTP GET by URL, domain allowlist, `HttpBackendOptions`. Used in v2 backends and tests.

**Proposed scope**:
- **WebDAV**: PROPFIND, GET on WebDAV endpoints; map collections to “directories”; optional auth (Basic, Bearer). New `WebDavBackend` or `HttpBackend` mode.
- **S3-compatible**: ListBucket (ListObjectsV2), GetObject. Support MinIO, AWS S3, Backblaze B2, etc. New `S3Backend` or `ObjectStorageBackend`. Auth: access/secret, IAM, or pre-signed URLs.
- **Unified or split**: One `IRemoteStorageBackend` with adapters (HTTP, WebDAV, S3) vs. separate `IContentBackend` impls. Latter matches existing `HttpBackend` pattern.

**Dependencies**: None.  
**Open**: Caching, range requests, checksums (ETag, S3 etag). Cost/rate limits for cloud.

**Implemented (2026-01-25):** `ContentBackendType.WebDav`, `WebDavBackend` (ISourceRegistry, IHttpClientFactory; domain allowlist, Basic/Bearer, HEAD validation); `ContentBackendType.S3`, `S3Backend` (ISourceRegistry, BackendRef `s3://bucket/key`, HeadObject via AWSSDK.S3, Endpoint/Region/AccessKey/SecretKey, bucket allowlist). Design: `docs/research/T-907-http-webdav-s3-backend-design.md`. Resolver fetch for WebDav/S3: follow-up.

---

## T-908: Private BitTorrent Backend

**One-line**: Real BitTorrent-based `IContentBackend` with private (invite-only or VPN-only) swarm support.

**Current state**:
- `StubBitTorrentBackend` in `Signals.Swarm`; `IBitTorrentBackend` used by swarm/fallback. BitTorrent DHT used for **rendezvous** (T-201), not full torrent transfer.
- `MeshTorrentBackend` uses DHT + torrent for content.

**Proposed scope**:
- **Real BT engine**: Replace stub with actual BT: .torrent parse, piece downloads, have/bitfield, unchoke. Use MonoTorrent, libtorrent bindings, or custom. Focus on “fetch by info_hash” and report to `IContentBackend` contract.
- **Private swarm**: No public DHT; only peers from invite list or from overlay. Private flag in .torrent; DHT/PEX disabled. Optional: keyed swarm (passphrase) for extra privacy.
- **Integration**: `IContentBackend` that finds .torrent (from DHT, index, or URL), joins swarm, downloads; or delegate to existing `MeshTorrentBackend` and add “private” mode there.

**Dependencies**: T-902 useful if we use DHT for .torrent discovery.  
**Open**: Which BT library. Legal risk of public torrent use; private-only reduces.

**Implemented (2026-01-25):** Design: `docs/research/T-908-private-bittorrent-backend-design.md`. `TorrentBackendOptions.PrivateMode` (`PrivateTorrentModeOptions`: PrivateOnly, DisableDht, DisablePex, `AllowedPeerSources`); `PrivatePeerSource` enum (Overlay, InviteList, Both). StubBitTorrentBackend replacement (MonoTorrent, piece transfer, fetch by info_hash) and TorrentBackend private-mode filtering: follow-up.

---

## T-911: MediaVariant Model and Storage

**One-line**: Generalize `AudioVariant` to `MediaVariant` (or equivalent) for non-audio or multi-format media; add storage.

**Current state**:
- `AudioVariant`: codec, quality, hashes, analyzer metadata. Used in `HashDb`, `CanonicalStatsService`, `MultiSourceDownloadService`, `ShadowIndexBuilderImpl`, analyzers.

**Proposed scope**:
- **MediaVariant**: Superset of `AudioVariant` with `ContentDomain` (Music, Image, Video, Generic) and domain-specific fields. Audio keeps existing; Image/Video get placeholders (e.g. dimensions, codec).
- **Storage**: Extend `IHashDbService` or new `IMediaVariantStore`. Prefer one store with `Domain` discriminator.
- **Migration**: `AudioVariant` → `MediaVariant` with `Domain=Music` and same fields. Or keep `AudioVariant` and add `MediaVariant` as a facade; design choice.

**Dependencies**: None. `ContentDomain` and `GenericFile` already exist.  
**Open**: How much to implement for Image/Video now (stub vs. real). Fuzzy matching across domains.

**Implemented (2026-01-25):** `MediaVariant` (Domain, VariantId, FirstSeenAt, LastSeenAt, SeenCount, FileSha256, FileSizeBytes; Audio/Image/Video/Generic placeholders). `IMediaVariantStore` + `HashDbMediaVariantStore` (Music→IHashDbService; Image/Video/GenericFile in-memory). `IHashDbService.GetAudioVariantByFlacKeyAsync`. `ContentDomain` Image=2, Video=3. `FromAudioVariant`/`ToAudioVariant`. DI.

---

## T-912: Metadata Facade Abstraction

**One-line**: Single facade over MusicBrainz, AcoustID, file tags, and Soulseek metadata.

**Current state**:
- MusicBrainz: `BrainzClient`, `MusicBrainzService`, etc.
- AcoustID: used in fingerprinting.
- File tags: via TagLib or similar in analyzers and `AudioTags`.
- Soulseek: search/browse responses.

**Proposed scope**:
- **IMetadataFacade**: `GetByRecordingId`, `GetByFingerprint`, `GetByFile`, `Search`, etc. Returns normalized `MetadataResult` (artist, title, release, mbid, etc.).
- **Adapters**: `MusicBrainzAdapter`, `AcoustIdAdapter`, `FileTagsAdapter`, `SoulseekMetadataAdapter`. Order of fallback and caching is policy.
- **Caching**: Centralize MB/cache and tag caching behind facade. Avoid duplicate MB hits from different callers.

**Dependencies**: None.  
**Open**: Conflict when MB vs. tags disagree. Performance (latency) when chaining adapters.

---

## T-913: AudioCore Domain Module

**One-line**: Domain-specific “AudioCore” that groups audio services: fingerprinting, variants, canonical, library health, analyzers.

**Current state**:
- Audio logic is spread: `Audio/`, `HashDb/`, `MediaCore/`, `Transfers/MultiSource/`, `VirtualSoulfind/` (e.g. `MusicContentDomainProvider`). `AudioVariant`, `CanonicalStatsService`, `LibraryHealth`, analyzers, `FuzzyMatcher`, etc.

**Proposed scope**:
- **AudioCore**: Logical module or assembly: `IAudioFingerprinter`, `IAudioVariantStore`, `ICanonicalStats`, `ILibraryHealth`, `IAnalyzerMigrationService`, analyzers, `MusicContentDomainProvider`-related. Clear API boundary.
- **Benefits**: Easier testing, replacement, and future “VideoCore”/“ImageCore” symmetry. No requirement to move files; can be a “virtual” grouping and optional `AudioCore.dll`.
- **Optional**: `IDomainCore` interface and `AudioCore : IDomainCore` if we want multi-domain pluggability.

**Dependencies**: T-911 (MediaVariant) if we generalize; not required for Audio-only.  
**Open**: How much to move vs. wrap. Duplication with `MediaCore` name.

**Implemented (2026-01-25):** `slskd.AudioCore.AudioCore` (API boundary documentation). `AddAudioCore(IServiceCollection, appDirectory)` registers: IChromaprintService, IFingerprintExtractionService, IHashDbService, IMediaVariantStore, ICanonicalStatsService, IDedupeService, IAnalyzerMigrationService, ILibraryHealthService, ILibraryHealthRemediationService, IMusicContentDomainProvider. Program calls `AddAudioCore(Program.AppDirectory)` after IAcoustIdClient, IAutoTaggingService, IMusicBrainzClient; scattered audio registrations consolidated. No separate AudioCore.dll.

---

## Suggested Order (if implementing)

1. **T-912** (metadata facade) — no deps; reduces duplication and clarifies MB/tags.
2. **T-911** (MediaVariant) — no deps; unblocks T-913 and future media.
3. **T-913** (AudioCore) — improves structure; can be done without T-911.
4. **T-901** (Ed25519 identity) — improves T-902, T-903.
5. **T-902** (DHT node) — unblocks T-903, helps T-906, T-908.
6. **T-903** (DHT storage) — builds on T-902.
7. **T-906** (native mesh backend) — needs DHT/shadow; after T-902/903 or reuse existing.
8. **T-907** (HTTP/WebDAV/S3) — independent.
9. **T-908** (private BitTorrent) — can use T-902 for discovery.

---

## References

- `docs/archive/planning/COMPLETE_PLANNING_INDEX_V2.md` — ⏸️ Remaining: 9 Research
- `docs/archive/status/TASK_STATUS_DASHBOARD.md` — 9 Research/Design Tasks
- `memory-bank/tasks.md` — Research implementation (T-901–T-913) § Medium Priority
- `memory-bank/activeContext.md` — Current: T-912

---

*Last updated: 2026-01-25*
