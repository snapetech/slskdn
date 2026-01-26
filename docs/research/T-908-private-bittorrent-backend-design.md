# T-908: Private BitTorrent Backend — Design

> **Status:** Design and options added. StubBitTorrentBackend replacement and TorrentBackend private-mode logic are follow-up implementation.

---

## One-line

Real BitTorrent-based `IContentBackend` / `IBitTorrentBackend` with **private** (invite-only or overlay-only) swarm support: no public DHT/PEX; peers from overlay or invite list only.

---

## Current state

- **`StubBitTorrentBackend`** (`Signals.Swarm`): `IsSupported() => false`, `PreparePrivateTorrentAsync` returns `""`. Used by `SwarmSignalHandlers` for `Swarm.RequestBtFallback`.
- **`IBitTorrentBackend`**: `IsSupported()`, `PreparePrivateTorrentAsync(SwarmJob job, string variantId, CancellationToken) -> string` (btFallbackId or infohash).
- **`TorrentBackend`** (`IContentBackend`): registry-based; BackendRef = infohash or magnet. No `IBitTorrentBackend`; no piece transfer.
- **MonoTorrent** (3.0.2): already in use for DHT (`DhtRendezvousService`). Provides `ClientEngine`, `TorrentManager`, piece picking, tracker/DHT.

---

## Implemented (T-908 research)

- **`PrivateTorrentModeOptions`** (in `TorrentBackend.cs`): `PrivateOnly`, `DisableDht`, `DisablePex`, `AllowedPeerSources` (enum: `Overlay`, `InviteList`, `Both`).
- **`TorrentBackendOptions.PrivateMode`**: optional `PrivateTorrentModeOptions?`. When set, a future TorrentBackend/IBitTorrentBackend impl should enforce private-only behaviour.
- **`PrivatePeerSource`** enum: `Overlay`, `InviteList`, `Both`.

---

## Replacing StubBitTorrentBackend (follow-up)

### Library

- **MonoTorrent** is the recommended .NET engine: already a dependency; supports .torrent parse, `TorrentManager`, DHT/tracker/peer lifecycle, piece I/O. For **private** mode, disable DHT and PEX in `TorrentManager.Settings` (or engine-level) and add only **manual peers** from overlay or invite list.

### IBitTorrentBackend contract (current and proposed)

- **Current:**  
  - `IsSupported() -> bool`  
  - `PreparePrivateTorrentAsync(SwarmJob job, string variantId, CancellationToken) -> string` (btFallbackId or infohash for the fallback).

- **Real impl behaviour (design):**
  - Resolve `job` + `variantId` to a .torrent (bytes) or magnet/infohash (from SwarmJob metadata, DHT, or URL—source is job-dependent).
  - Create a `TorrentManager` (or equivalent) with:
    - `DisableDht = options.PrivateMode?.DisableDht ?? true` when used for private fallback,
    - `DisablePeersFromPex = options.PrivateMode?.DisablePex ?? true`,
    - trackers: empty or private-only trackers when PrivateOnly.
  - Add peers only from:
    - **Overlay**: peers from mesh overlay (e.g. from `Swarm.RequestBtFallback` signalling or an overlay peer provider).
    - **InviteList**: configured allowlist of peer addresses.
  - Start the manager, run piece pipeline; when `PreparePrivateTorrentAsync` returns, the fallback is “ready” (e.g. return infohash or an internal id). The actual file output and progress are handled by the swarm/engine lifecycle (out of scope here).

- **Optional interface extension (design):**  
  - `Task<string?> FetchByInfoHashAsync(string infoHashOrMagnet, CancellationToken ct)` for “fetch by info_hash” used by a resolver or ITorrentClient. If added, the real impl would load by magnet/infohash, add manual peers from overlay/invite, and return a handle or path when complete. This is **not** in the current `IBitTorrentBackend`; document as a possible addition when implementing resolver fetch for `ContentBackendType.Torrent`.

---

## Private swarm behaviour

- **Private flag in .torrent:** When **creating** .torrents for private swarms, set the `private` flag (BEP 27) so clients do not use DHT/PEX. When **joining** an existing .torrent, the engine must still **disable** DHT/PEX via settings if `PrivateMode.PrivateOnly` (we cannot trust the flag alone for joined torrents).
- **No public DHT:** `DisableDht = true` in MonoTorrent (or equivalent). DhtRendezvous (BEP 5) is used for **rendezvous** (T-201) elsewhere; the BT **piece-transfer** engine should not use it for discovery in private mode.
- **No PEX:** `DisablePeersFromPex = true` (or equivalent).
- **Peer source:** Only overlay and/or invite list, as per `AllowedPeerSources`.

---

## Keyed swarm (optional, deferred)

- Passphrase or key for an extra layer of access control. Not in MonoTorrent core; would require a custom handshake or out-of-band key distribution. **Deferred.**

---

## TorrentBackend integration (follow-up)

- **Today:** TorrentBackend uses `ISourceRegistry`; BackendRef = infohash or magnet. It does **not** call `IBitTorrentBackend`. Resolver fetch for Torrent is **not** implemented.
- **When `TorrentBackendOptions.PrivateMode` is set:**
  - **FindCandidatesAsync:** Only accept candidates that are known to come from overlay or invite (e.g. a `SourceCandidate` flag or a separate “private infohash” store). If no such metadata exists yet, treat as “all candidates” and rely on validation.
  - **ValidateCandidateAsync:** Optionally check that the infohash is in an “allowed for private” set. When `IBitTorrentBackend` is real, no need to change TorrentBackend validate logic for format; the engine enforces DHT/PEX at runtime.
- **Resolver:** When fetch for `ContentBackendType.Torrent` is implemented, it must call into `IBitTorrentBackend` (or a dedicated `ITorrentFetch`) with `PrivateMode` so the engine is configured accordingly.

---

## IContentBackend vs IBitTorrentBackend

- **`TorrentBackend`** (`IContentBackend`): discovery and validation. BackendRef = infohash/magnet. Resolver performs fetch (not yet implemented).
- **`IBitTorrentBackend`**: low-level BT engine. Used by `SwarmSignalHandlers` for **BT fallback** (`PreparePrivateTorrentAsync`). A future resolver would also use it (or an adapter) to “fetch by info_hash”.
- **Private:** Configured via `TorrentBackendOptions.PrivateMode` and, in the real `IBitTorrentBackend` impl, via `IOptionsMonitor<TorrentBackendOptions>` or a dedicated `PrivateTorrentModeOptions` binding passed into the engine.

---

## DI and config

- **TorrentBackendOptions:** already bound; `PrivateMode` is an optional sub-object. Example: `VirtualSoulfindV2:Torrent:PrivateMode:PrivateOnly=true, DisableDht=true, DisablePex=true, AllowedPeerSources=Both`.
- **IBitTorrentBackend:** real impl (replacing `StubBitTorrentBackend`) should take `IOptionsMonitor<TorrentBackendOptions>` or `IOptionsMonitor<PrivateTorrentModeOptions>` to read `PrivateMode` when preparing/joining swarms.

---

## Open / follow-ups

- **Replace StubBitTorrentBackend** with a MonoTorrent-based impl: .torrent/magnet load, TorrentManager, piece downloads, have/bitfield, unchoke; honour `PrivateMode` (DisableDht, DisablePex, peer source).
- **Resolver fetch** for `ContentBackendType.Torrent` (BackendRef = infohash/magnet) using `IBitTorrentBackend` or `ITorrentFetch`.
- **TorrentBackend** logic when `PrivateMode` is set: filter candidates by “private” provenance when metadata exists.
- **Keyed swarm** (passphrase): deferred.
- **Legal/UX:** Private-only reduces exposure to public torrent legality; document that public DHT/trackers are not used when `PrivateMode.PrivateOnly` is true.

---

## References

- `9-research-design-scope.md` § T-908
- `StubBitTorrentBackend`, `IBitTorrentBackend` (`Signals.Swarm.SwarmSignalHandlers.cs`)
- `TorrentBackend`, `TorrentBackendOptions`, `PrivateTorrentModeOptions`, `PrivatePeerSource` (`VirtualSoulfind/v2/Backends/TorrentBackend.cs`)
- MonoTorrent 3.0.2, `DhtRendezvousService`, `docs/DHT_RENDEZVOUS_DESIGN.md`
