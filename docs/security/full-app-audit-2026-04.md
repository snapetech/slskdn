# Full-App Security Audit — 2026-04-18

**Scope**: whole-tree bughunt / redteam of `/home/keith/Documents/code/slskdn/src/slskd/`. Not limited to DHT/mesh (see `dht-mesh-audit-2026-04.md` for that). About 1,000 C# files across ~45 top-level subsystems.

**Method**: six parallel sweeps — HTTP API, secrets+crypto, filesystem, injection+deserialization, SSRF, native/DoS — each hunting for specific exploitable patterns. All high-severity claims verified by re-reading the code before inclusion here.

---

## 1. Executive summary

| Severity | Count | Theme |
|---|---|---|
| **HIGH** | 4 | Cert validation bypass in WebSocket transport, dead-code defense (TransferSecurity), share-scan symlink default, webhook/ActivityPub SSRF |
| **MEDIUM** | 7 | Unbounded channels, stackalloc with peer-influenced length, login lockout window, audience not validated on share tokens, etc. |
| **LOW / INFO** | several | Mostly opt-in risks or hardened-by-default patterns |
| **Refuted** | 4 | Agent claims that did not survive code verification — documented so they aren't re-raised |

**Positive findings worth noting**:
- Structured logging throughout (no log-injection surface found).
- `CryptographicOperations.FixedTimeEquals` used for MAC/token comparisons where it matters.
- `BuildTimeAnalyzer` + `SlskdnAnalyzer` compile-time gates on `ExecuteSqlRaw` / `FromSqlRaw` / `Process.Start` — every use must be justified.
- `HttpSignatureKeyFetcher` has a proper SSRF guard (HTTPS-only, IP-range check, redirect-limited, 256 KB cap) — use it as the reference pattern.
- `SolidFetchPolicy` enforces HTTPS-only + host allowlist + loopback/private-IP rejection — another reference pattern.
- Global `SocketsHttpHandler` has `AllowAutoRedirect = false` at the DI root.
- `JsonSerializerOptions.MaxDepth = 3` on pod message parsing (`Application.cs:1392`).

---

## 2. HIGH findings (verified, actionable)

### H1 — WebSocket transport disables TLS certificate validation
- **File**: `src/slskd/Common/Security/WebSocketTransport.cs:76`
- **Observed**: `client.Options.RemoteCertificateValidationCallback = (_, _, _, _) => true; // Trust server cert for testing`
- **Scenario**: when `_options.UseWss == true`, the transport accepts any TLS certificate. A network-path attacker (or a compromised DNS) terminates TLS with a self-signed cert, captures and modifies all traffic over the WebSocket. The comment says "for testing" but the code is in the production transport class and gated only on `UseWss`, not on a dev flag.
- **Fix**: remove the callback and rely on system trust, OR pin the expected cert thumbprint if this transport talks to a known server, OR gate behind an explicit `IgnoreCertificateErrors` option the way `RelayClient` does (with a loud startup warning).
- **Severity**: HIGH.

### H2 — `TransferSecurity` is dead code; download/upload paths bypass PathGuard
- **Files**: `src/slskd/Common/Security/TransferSecurity.cs` (definition), `src/slskd/Common/Security/SecurityStartup.cs` (registration). No callers anywhere else.
- **Observed**: `TransferSecurity.ValidateDownloadPath` / `ValidateSharePath` call `PathGuard.Validate` with strong checks (traversal, absolute paths, drive letters, null bytes, control chars, URL-encoded traversal) and drive honeypot, violation tracker, and peer reputation. A `grep` for either validation method finds only the defining file — they are never invoked. `DownloadService.DownloadAsync` and `UploadService.UploadAsync` do not call them.
- **Scenario**: defense-in-depth built but not wired up. Today, traversal is blunted by the implicit collapsing in `ToLocalRelativeFilename` (see §5), but that's a side effect of filename mangling, not a validated invariant. Any future code path that calls a different filename helper inherits no protection.
- **Fix**: wire `TransferSecurity.ValidateDownloadPath` into `DownloadService` before the transfer is enqueued and `ValidateSharePath` into upload serving. Alternatively, if the current implicit protection is deemed sufficient, delete `TransferSecurity` so future readers don't assume it's active.
- **Severity**: HIGH (defence-in-depth gap; not exploitable today given §5 but fragile).

### H3 — Share scan follows symlinks out of the share root
- **File**: `src/slskd/Shares/ShareScanner.cs:215-220`
- **Observed**: `new EnumerationOptions { AttributesToSkip = Hidden|System, IgnoreInaccessible = true, RecurseSubdirectories = true }` — `FollowSymlinks` is not set. The .NET default is `true`.
- **Scenario**: a user (or an attacker who writes into the share root via a previously compromised channel) drops a symlink `share/x -> /etc` or `share/x -> ~/.ssh`. The share scanner recurses into it, indexes `/etc/passwd` and `authorized_keys`, and any remote Soulseek/mesh peer can browse and download them.
- **Fix**: set `FollowSymlinks = false` explicitly on the `EnumerationOptions`. If symlink support inside shares is intentional, additionally verify each resolved target is under the share root (realpath + `StartsWith`).
- **Severity**: HIGH.

### H4 — Webhook delivery and ActivityPub inbox delivery have no SSRF guard
- **Files**: `src/slskd/Integrations/Webhooks/WebhookService.cs:116`, `src/slskd/SocialFederation/ActivityDeliveryService.cs:185`
- **Observed**:
  - `WebhookService`: `http.PostAsync(call.Url, content)` — `call.Url` comes from config, only checked for `http://`/`https://` prefix (`WebhookHttpOptions.Validate`). No IP-range check.
  - `ActivityDeliveryService`: `new HttpRequestMessage(HttpMethod.Post, inboxUrl)` — `inboxUrl` comes from `recipientUrls` which originates from federation peer data. Only rate-limited, not IP-checked.
- **Scenario**:
  - Webhook: if the config API (or a file-write primitive) lets an attacker set a webhook URL, they can target `http://169.254.169.254/latest/meta-data/iam/security-credentials/` (AWS IMDS) or `http://127.0.0.1:<internal-port>/`.
  - ActivityPub: a malicious remote actor publishes an inbox URL pointing at internal addresses; the server POSTs signed activity JSON there. Less useful for data exfil (body is fixed), but still a reachability probe into internal services.
- **Fix**: reuse `HttpSignatureKeyFetcher`'s pattern or `SolidFetchPolicy`'s IP-range check. At minimum, reject loopback, link-local, RFC1918, and cloud-metadata ranges before the request.
- **Severity**: HIGH for webhook if config is ever writable over the API; MEDIUM for ActivityPub inbox.

---

## 3. MEDIUM findings (verified, reasonable to fix soon)

### M1 — Unbounded `Channel.CreateUnbounded` in peer-driven queues
- **Files** (all `Channel.CreateUnbounded<...>()`):
  - `src/slskd/Swarm/SwarmDownloadOrchestrator.cs:35`
  - `src/slskd/Jobs/Metadata/MetadataJobRunner.cs:20`
  - `src/slskd/SongID/SongIdService.cs:78`
  - `src/slskd/Mesh/Dht/ContentPeerHintService.cs:23`
- **Scenario**: producers here can be peer-driven (downloads complete → metadata job; peer hint broadcast → content hint queue; etc.). A peer that can trigger enqueues faster than the consumer drains causes unbounded memory growth → OOM.
- **Fix**: convert to `Channel.CreateBounded` with `BoundedChannelFullMode.DropWrite` (or `Wait` if producer-backpressure is tolerable). Size appropriately per-queue.
- **Severity**: MEDIUM.

### M2 — `stackalloc` sized by user-controlled `input.Length + salt.Length`
- **File**: `src/slskd/Common/Security/SecurityUtils.cs:151`
- **Observed**: `Span<byte> combined = stackalloc byte[input.Length + salt.Length];`
- **Scenario**: if a caller passes a large peer-supplied byte span, stack overflow crashes the process.
- **Fix**: cap the combined length (e.g. 16 KB) and fall back to `ArrayPool<byte>.Shared` above the cap. This pattern repeats in Kademlia (`src/slskd/Mesh/Dht/KademliaRoutingTable.cs:313`, `KademliaRpcClient.cs:442`) — those sites are safer because lengths are fixed by the Kademlia ID spec, but an explicit `if (a.Length != 20) throw` guard would make that invariant load-bearing rather than implicit.
- **Severity**: MEDIUM.

### M3 — Login lockout window is 5 minutes, 10 attempts/IP
- **File**: `src/slskd/Core/API/Controllers/SessionController.cs:45-46`
- **Scenario**: 10 password attempts per 5-minute window per IP, then 15-minute lockout. An attacker with a small IP pool can grind ~1 guess per 30 sec per IP without ever tripping the lockout. No account-level lockout.
- **Fix**: add per-username lockout (e.g. 5 failures in 15 min → 1 hour lock), independent of IP; consider tarpitting (progressively slower responses) instead of hard lockouts.
- **Severity**: MEDIUM.

### M4 — `ValidateAudience = false` on share tokens
- **File**: `src/slskd/Sharing/ShareTokenService.cs:102`
- **Scenario**: a share token issued for collection X is structurally acceptable when validated for collection Y. The service also checks the collection/content ID at use time (`StreamsController.cs:93-95`), which makes this non-exploitable today — but the JWT library defense is off, so any future code path that trusts the JWT claims without the extra content check inherits no protection.
- **Fix**: set `ValidateAudience = true` and put the collection ID in `aud`. Keep the explicit content-ID check as belt-and-suspenders.
- **Severity**: MEDIUM (defence-in-depth; not exploitable today).

### M5 — ActivityPub inbox URL no IP-range check (paired with H4)
- Already covered under H4 — listed here as the "narrower" SSRF variant: federation peer data vs. config data. Same fix pattern.

### M6 — FFmpeg output captured into unbounded `MemoryStream`
- **File**: `src/slskd/Integrations/Chromaprint/FingerprintExtractionService.cs` (agent report; file present)
- **Scenario**: a crafted audio file decodes to a very large PCM stream, filling the in-memory buffer used for fingerprinting.
- **Fix**: stream into a capped buffer; abort when the cap is hit. Cap at e.g. 100 MB PCM.
- **Severity**: MEDIUM (requires a local user to drop a malicious file or accept a download, but reachable via normal library scan).

### M7 — Overlay certificate PFX password stored in plaintext file
- **File**: `src/slskd/DhtRendezvous/Security/CertificateManager.cs:176`
- **Observed**: PFX password written to `overlay_cert.key` with Unix mode 0600. Covered in the DHT audit; repeating here for completeness.
- **Severity**: MEDIUM (file-system-level disclosure risk only; perms are correct).

---

## 4. LOW / INFO

- **Relay cert bypass is opt-in with a warning** (`RelayClient.cs:309-313, 377`): gated on `IgnoreCertificateErrors` config and logs "This is insecure and should only be used in controlled lab environments" at startup. Acceptable.
- **`DangerousAcceptAnyServerCertificateValidator` factory** (`Program.cs:1069-1073`): used for ActivityPub key fetching where the key is verified by the signature over the body — the TLS cert does not need to be trusted because content is authenticated. Make sure no new code path reuses this factory.
- **Ephemeral JWT signing key** (`Program.cs:2581-2583`): generated fresh if not configured, logs a warning. Sessions invalidate on restart; acceptable for dev, document for prod.
- **SHA1 in Kademlia node-ID derivation** (`Program.cs:2074`): non-cryptographic use of SHA1 to produce 160-bit IDs. Acceptable under the Kademlia spec; note that collision-resistance is not a security property here.
- **`mesh-overlay.key` / `mesh-overlay.key.prev` in `src/slskd/`**: gitignored (`.gitignore:32,33,36,37`) and not tracked. Local dev artifacts only. Consider moving key material to `AppDirectory` to avoid confusion.
- **ApplicationController `/api/v0/application/gc`** (`ApplicationController.cs:147-154`): authenticated-only, no role check. Any authenticated user can trigger GC → DoS amplification. Low priority; consider requiring admin role.
- **Streaming endpoint trusts `IContentLocator`** (`StreamsController.cs:125`): `new FileStream(resolved.AbsolutePath, ...)` trusts the locator to stay within allowed roots. The locator implementation was not audited in this pass — worth a focused follow-up review.

---

## 5. Refuted / downgraded claims (do not re-raise)

These were flagged by the sub-agents but do not hold up on code verification. Recorded here so the next audit doesn't chase them again.

- **`Type.GetType(typeName)` RCE in `Common/JsonConverters/TypeConverter.cs:31`** — the `TypeConverter` class is defined but has **no references anywhere in the tree** outside its own file. It is not registered in any `JsonSerializerOptions`. Not exploitable. Consider deleting it — dead code that looks dangerous is worse than no code.
- **`Process.Start` command injection in `VirtualSoulfind/Bridge/SoulfindBridgeService.cs:114`** — `$"--port {bridgePort}"` looks scary, but `bridgePort` is a typed `int` (`options.VirtualSoulfind.Bridge.Port`) and `UseShellExecute = false`. Integer cannot contain shell metacharacters. Not exploitable.
- **`ChromaprintContext.Feed` size parameter confusion** — the agent claimed heap overflow. Verified: the code allocates `new int[samples.Length]` (4×N bytes) and passes `size = samples.Length`. If the chromaprint C API expects `int16_t*` with count-of-int16, native reads 2×N bytes from a 4×N-byte buffer — *under*-reads, no memory-safety issue. It is a **correctness bug** (fingerprints are computed from malformed sample data), not a security bug.
- **`ToLocalRelativeFilename` traversal bypass** — the agent said a peer's `../../etc/passwd` could escape. Verified: the method splits on path separator, keeps only the last two components (`parts.Last()` and `parts.Reverse().Skip(1).Take(1).Single()`), and runs `ReplaceInvalidFileNameCharacters` on each. For `../../etc/passwd` the result is `etc/passwd` *under the download root* — it never escapes. The implementation is implicit rather than validated, which is ugly, but it is traversal-safe.

---

## 6. Recommended fix order

1. **H1** — remove/gate the WebSocket cert-validation bypass. Small change, big risk reduction.
2. **H3** — set `FollowSymlinks = false` on the share scanner. One line.
3. **H4 (webhook half)** — add an IP-range SSRF check to webhook delivery. Crib from `SolidFetchPolicy`.
4. **H2** — decide: wire `TransferSecurity` into download/upload, or delete it. Either choice removes the trap.
5. **M1** — convert the four unbounded channels to bounded. Pick sensible caps per queue.
6. **H4 (ActivityPub half) + M3 + M4** — batch: inbox IP-range check, per-username lockout, share-token audience validation.
7. **M2, M6, M7** — lower-risk hardening; do when touching adjacent code.

---

## 7. Process notes

- All six parallel sweeps were run by sub-agents reading the tree independently.
- Every HIGH finding in this doc was re-verified by opening the cited files before inclusion.
- Four sub-agent claims were downgraded or refuted during verification; see §5.
- The DHT/mesh subsystem was audited separately — see `docs/security/dht-mesh-audit-2026-04.md`.
- Out of scope this pass: `IContentLocator` implementations, TLS/QUIC overlay library internals, VirtualSoulfind v2 backends (their own trust model), `Mesh/Realm/` bridge.
