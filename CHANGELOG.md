# Changelog

All notable changes to slskdn are documented here. slskdn is a distribution of slskd with advanced features and experimental subsystems.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

---

## [Unreleased]

### Security & hardening (40-fixes, dev/40-fixes)

- **EnforceSecurity** (`web.enforce_security`): When `true`, enables strict auth, CORS, startup checks via `HardeningValidator`, and automatic 400 for invalid `ModelState` (`SuppressModelStateInvalidFilter = false`). Use for repeatable hardened testing.
- **Passthrough AllowedCidrs** (`web.authentication.passthrough.allowed_cidrs`): Optional CIDR allowlist for no-auth mode (e.g. `127.0.0.1/32,::1/128`) in addition to loopback. PR-03.
- **CORS** (`web.cors`): `allowed_headers`, `allowed_methods`; allowlist semantics; no `AllowAll` + `AllowCredentials`. PR-04.
- **Exception handler**: RFC 7807 `ProblemDetails`, `traceId`; in Production, generic detail (no internal leak). PR-05.
- **Dump endpoint**: Returns **501** when dump creation fails (e.g. `dotnet-dump` not on PATH, `DiagnosticsClient` failure) with instructions. `diagnostics.allow_memory_dump`, `allow_remote_dump`; admin-only, local-only when `allow_remote_dump` false. PR-06.
- **ModelState / RejectInvalidModelState**: `web.api.reject_invalid_model_state`; when Enforce, invalid payloads return 400 with consistent `ValidationProblemDetails`. PR-07.
- **MeshGateway**: Chunked POST supported; bounded body read; 413 on over-limit. PR-08.
- **Kestrel MaxRequestBodySize** (`web.max_request_body_size`): Configurable request body limit (default 10 MB). PR-09a.
- **Rate limit fed/mesh**: `Burst_federation_inbox_*`, `Burst_mesh_gateway_*` policies; `web.rate_limiting`. PR-09b.
- **QuicDataServer**: Read/limits aligned with `GetEffectiveMaxPayloadSize`. §8.
- **Metrics Basic Auth**: Constant-time comparison (`CryptographicOperations.FixedTimeEquals`); `WWW-Authenticate: Basic realm="metrics"`. §9.
- **§11 NotImplementedException gating**: Incomplete features (I2P, RelayOnly, PerceptualHasher, etc.) fail at startup or return 501 when enabled; no `NotImplementedException` crash in configured defaults.
- **ScriptService**: Async read of stdout/stderr, `WaitForExitAsync`, timeout and process kill; no `WaitForExit()` deadlock. J.

### Mesh

- **Mesh:Security** (`mesh.security`): `enforceRemotePayloadLimits`, `maxRemotePayloadSize`; safe MessagePack/JSON deserialization, overlay/transport caps.
- **Mesh:SyncSecurity** (`mesh.sync_security`): Rate limiting, quarantine, proof-of-possession, consensus, alert thresholds (T-1432–T-1435). See `docs/security/mesh-sync-security.md`.

### Anonymity / transports

- **I2PTransport**: SAM v3.1 STREAM CONNECT with `host` as I2P destination (base64 or `.b32.i2p`). `AnonymityTransportSelector` registers I2P when `AnonymityMode.I2P`. §11: enabling without SAM bridge fails at startup or 501.
- **RelayOnlyTransport**: RELAY_TCP over data overlay; `IOverlayDataPlane.OpenBidirectionalStreamAsync`; `QuicDataServer` handles `RELAY_TCP`. **`RelayPeerDataEndpoints`** (`security.adversarial.anonymity.relay_only.relay_peer_data_endpoints`): list of `host:port` for each relay’s QUIC data overlay; used when `TrustedRelayPeers` are not resolved. Required for RelayOnly until peer-id resolution. §11: enabling without endpoints/TrustedRelayPeers fails at startup or 501.

### Audio / MediaCore

- **AudioUtilities.ExtractPcmSamples**: Via ffmpeg; `ExtractPcmSamplesAsync`. Test expects `FileNotFoundException` when file missing (replacing `FeatureNotImplementedException`).

### Test infrastructure

- **test-data/slskdn-test-fixtures**: Fetch scripts, `manifest.json`, `.gitignore` for download artifacts.

### Breaking / behavior changes

- **EnforceSecurity on**: No-auth + non-loopback bind requires `allow_remote_no_auth: true` or startup fails. CORS `AllowCredentials` + wildcard origin fails startup. Dump enabled + auth disabled fails startup. `Flags.HashFromAudioFileEnabled` + Enforce fails startup (not implemented).
- **Dump**: Default `allow_memory_dump: false`; 501 when creation fails (no silent empty or 500).
- **CORS**: When enabled, require explicit `allowed_origins` when `allow_credentials: true`; no wildcard + credentials.

---

## [0.24.1-slskdn.40]

- Bump to 0.24.1-slskdn.40 (slskdn-main-linux-x64.zip).
- See `packaging/debian/changelog` and `docs/archive/DEVELOPMENT_HISTORY.md` for earlier entries.
