# Hardening — 2026-04-20

Output of the red-team / bug-hunt pass against the repo and `kspls0`. This document tracks each finding, the chosen remediation, and the status. Host / network items the maintainer chose to handle out-of-band (qBittorrent on kspls0, LAN plaintext HTTP, filesystem config permissions) are not tracked here.

Each finding references the source location it affects. Follow-ups that are out of scope for this pass call out what further design work is needed.

## Summary

| # | Finding | Severity | Status |
|---|---------|----------|--------|
| H1 | `mesh-overlay.key{,.prev}` in repo working tree | Critical | ✅ fixed (files removed, relocation documented) |
| H2 | `/solid/clientid.jsonld` leaks request host (LAN IP) | Critical | ✅ fixed (require `solid.clientIdUrl`; redirect_uri derived from it) |
| H3 | `DescriptorRetrieverController` `[AllowAnonymous]` | High | ✅ fixed (gated behind `AuthPolicy.Any`) |
| H4 | `federation.verify_signatures=false` bypass | High | ✅ fixed (option is now a no-op; verification is unconditional) |
| H5 | Solid SSRF allow-list is string-based (DNS rebinding) | High | ✅ fixed (resolve host, re-check every resolved IP) |
| H6 | Gold Star Club auto-joins every tester by default | Medium | ✅ fixed (opt-in via `SLSKDN_POD_GOLD_STAR_CLUB_AUTOJOIN=true`) |
| H7 | HashDb mesh-merge accepts unsigned peer entries | High | ✅ implemented (opt-in enforcement during rollout) |
| H8 | `Constants.IgnoreCertificateErrors` / `RelayClient` TLS bypass | High | ✅ periodic warn + optional SPKI pinning shipped |
| H9 | Auto-replace + wishlist auto-download enabled from README | Medium | ℹ no change — already `false` by default in `Options.cs` |
| H10 | Profile endpoint `[AllowAnonymous]` by design | Medium | ℹ no change — documented intent; audit payload separately |
| H11 | Mesh TOFU re-pinning on pin-store loss | Medium | ✅ TOFU / re-pin workflow documented in `docs/SECURITY-GUIDELINES.md` |
| H12 | DHT publishes residential IPs | Medium | ✅ `LanOnly` toggle + periodic warn + UI first-run disclosure shipped |
| H13 | NowPlaying webhook vs Plex Bearer auth | Medium | ✅ scope-restricted API keys shipped; webhook requires `nowplaying` scope |
| H14 | MusicBrainz / AcoustID phone-home | Medium | ✅ README privacy callout added |
| H15 | Federation enabled on kspls0 exposes actors if port-forwarded | Medium | ✅ already safe — defaults are `Enabled=false` + `Mode="Hermit"` |
| H16 | Plaintext token fragments in `SECURITY-AUDIT-*.md` / validation JSON | Medium | ✅ scrubbed (paths and LAN IP redacted) |

---

## Fixed in this pass

### H1 — mesh-overlay.key relocation

**What:** `mesh-overlay.key` and `mesh-overlay.key.prev` lived at the repo root as local development artifacts. They were `.gitignore`d (never pushed), but they showed up in any `tar czf slskdn.tar.gz slskdn/` backup / archive.

**Fix in repo:** the two files are now deleted from the working tree. `.gitignore` already catches them so they won't come back by accident.

**On `kspls0` (test host):** the overlay key is resolved via `ResolveAppRelativePath(...)` off `SLSKD_APP_DIR`. Move the live key under the app-data directory, not the repo checkout:

```bash
# on kspls0
sudo install -d -m 0700 -o slskd -g slskd /var/lib/slskdn/keys
sudo mv /path/to/repo/mesh-overlay.key      /var/lib/slskdn/keys/mesh-overlay.key
sudo mv /path/to/repo/mesh-overlay.key.prev /var/lib/slskdn/keys/mesh-overlay.key.prev 2>/dev/null || true
sudo chmod 0600 /var/lib/slskdn/keys/mesh-overlay.key*
sudo chown slskd:slskd /var/lib/slskdn/keys/mesh-overlay.key*
# point slskdn at it
#   config: mesh.overlay.key_path: /var/lib/slskdn/keys/mesh-overlay.key
#   or env: SLSKD_MESH_OVERLAY_KEY_PATH=/var/lib/slskdn/keys/mesh-overlay.key
sudo systemctl restart slskd
```

**GitHub Secrets note:** the overlay key is *per-node identity*, not a build-time secret, so stashing it in a repo-level secret is the wrong shape — every deploy would share an identity. The right home is a per-node file on the node.

### H2 — `/solid/clientid.jsonld` no longer falls back to the request host

**What:** `SolidClientIdDocumentService.WriteClientIdDocumentAsync` previously derived `client_id` and `redirect_uri` from the incoming request's `Host` header when `solid.clientIdUrl` was unset. On kspls0 that meant the document published `http://192.168.50.85:5030/solid/clientid.jsonld` to any Solid IdP the host handed the URL to — LAN-IP disclosure by design.

**Fix:** if `solid.clientIdUrl` is not configured, the endpoint returns `404` and logs once. When it *is* set, both `client_id` and `redirect_uri` are derived from that public URL, not the request. This matches Solid-OIDC's requirement that the client-id document be dereferenceable by the IdP and closes the LAN-IP leak.

**Operator action:** if you want Solid on kspls0, set `solid.clientIdUrl` to your public HTTPS URL. Until then the endpoint is effectively off.

### H3 — `DescriptorRetrieverController` gated

**What:** `GET api/v{ver}/…/descriptor/{id}`, `GET …/query/domain/{domain}`, and `POST …/verify` were `[AllowAnonymous]`. Not routed on the current kspls0 build (the `MediaCore` feature flag gates the whole controller), but flipping the flag on would have exposed a public content-inventory enumeration primitive.

**Fix:** removed `[AllowAnonymous]` on the three endpoints and added `[Authorize(Policy = AuthPolicy.Any)]` matching the rest of the authenticated API. `POST /batch` and `GET /stats` were already class-default authorized; no change there.

### H4 — ActivityPub signature verification is now unconditional

**What:** `ActivityPubController.PostToInbox` had an `if (!opts.VerifySignatures) { log warning; accept; }` escape hatch (MED-03). An operator flipping `federation.verify_signatures=false` in YAML silently turned the inbox into an unauthenticated POST sink.

**Fix:** the bypass branch is deleted — verification is always performed. `SocialFederationOptions.VerifySignatures` is left in place (so existing configs don't fail validation) but is now a no-op, with a one-time startup warning if it's set to `false`. Tests that previously relied on disabling verification should mock `IHttpSignatureKeyFetcher` instead.

### H5 — Solid SSRF allow-list now re-resolves DNS

**What:** `SolidFetchPolicy.Validate` checked private-IP / loopback only when `uri.Host` parsed as an IP literal. A hostile (or compromised) hostname in `AllowedHosts` that resolved to `127.0.0.1` / `10.x` / `169.254.169.254` bypassed the block. Classic DNS-rebinding-adjacent flaw — gated today by operators having to add the hostname explicitly, but there was no defense in depth.

**Fix:** after the host-name allow-list check, the policy resolves the host through `Dns.GetHostAddresses` and rejects the request if *any* resolved address is loopback, RFC1918, link-local, IPv6 ULA/link-local, or IPv4-mapped-IPv6 variants thereof. `169.254.169.254` (AWS IMDS) and `100.64.0.0/10` (CGNAT) are now explicitly blocked. Failures to resolve are treated as "block". A small TTL cache prevents DNS storms under load.

### H6 — Gold Star Club auto-join is opt-in

**What:** `GoldStarClubService.ExecuteAsync` ran at startup for every node and silently enrolled the user's Soulseek username into `pod:gold-star-club`, a shared chatroom with up to 999 other testers. Anything typed in the pod is visible to the entire cohort.

**Fix:** `TryAutoJoinAsync` and `ExecuteAsync` now short-circuit unless `SLSKDN_POD_GOLD_STAR_CLUB_AUTOJOIN=true` is set in the environment. `EnsurePodExistsAsync` still runs so admins can join manually via the Pods UI/API — just nothing happens automatically. Env-var gating avoids adding a new options tree.

**Documentation:** README should note the opt-in switch. Done separately if/when we touch README.

---

### H7 — HashDb mesh entries are now signed

`MeshSyncService` signs outbound `MeshHashEntry` records with the node's Ed25519 overlay identity and verifies inbound ones on merge. The signed payload is domain-tagged and covers only the immutable identity tuple (`FlacKey`, `ByteHash`, `Size`, `MetaFlags`) — mutable bookkeeping fields (`SeqId`, `UseCount`, timestamps) are intentionally excluded so the same file can carry a verifiable signature across observers.

**Decisions made (in consultation):**
- **Key material:** reuse the existing mesh overlay key (`IKeyStore.Current`). No new key tree; a single Ed25519 identity per node already pins peer-id = `Base32(SHA256(pk)[0:20])`.
- **Migration:** `Mesh.SyncSecurity.RequireSignedEntries` defaults to `false`. Unsigned inbound entries are accepted for one release with a once-per-peer warn log, and dropped once the flag is flipped to `true`. Entries carrying a signature that fails verification are always dropped.
- **Wire:** two new optional fields on `MeshHashEntry` (`signer_pk`, `sig`). Peers on older builds see them as ignored unknowns — no schema-negotiation bump needed.
- **Policy:** flip `RequireSignedEntries=true` after all operators are on a signing build, or earlier on fresh installs.

**Fix:** `src/slskd/Mesh/Messages/MeshHashEntrySigner.cs` (new), additions to `MeshHashEntry`, signing on the outbound path in `MeshSyncService.GenerateDeltaResponseAsync`, verification in `MeshSyncService.MergeEntriesAsync`.

### H8 — Relay TLS: periodic warn + optional SPKI pinning

Two complementary fixes shipped.

**H8 (warn, don't refuse):** certificate validation can still be disabled via `Relay.Controller.IgnoreCertificateErrors` (lab / self-signed use case), but a `RelayTlsWarningService` re-logs a loud warn every 15 minutes while the flag is set, instead of the previous once-at-startup notice that scrolls off the operator's console.

**H8-pin (mandatory SPKI pinning, optional config):** a new `Relay.Controller.PinnedSpki` option accepts a comma-separated list of base64 SHA-256 SPKI pins. When any pin is configured, both the SignalR hub connection and the file-upload `HttpClient` install a `ServerCertificateCustomValidationCallback` that requires the controller's presented certificate to match one of the pins. Pinning takes precedence over `IgnoreCertificateErrors` — a pinned cert whose SPKI doesn't match is rejected even when "ignore errors" is set, so there's no way to accidentally turn pinning off by flipping the bypass. When no pins are configured, the legacy CA-only-or-bypass behavior is preserved unchanged.

**Fix:**
- `src/slskd/Relay/RelayTlsWarningService.cs` (existing) — periodic warn.
- `src/slskd/Relay/RelayTlsPinValidator.cs` (new) — parses pins, reuses `SecurityUtils.ExtractSpkiPin` for SPKI extraction.
- `src/slskd/Core/Options.cs` — adds `RelayControllerConfigurationOptions.PinnedSpki`.
- `src/slskd/Relay/RelayClient.cs` — wires the validator into both TLS call sites (HubConnection and `CreateHttpClient`).
- `tests/slskd.Tests.Unit/Relay/RelayTlsPinValidatorTests.cs` — 9 tests covering pin match / mismatch / parser edge cases (empty, whitespace, duplicates).

### H11 — Mesh TOFU / re-pin workflow documented

`docs/SECURITY-GUIDELINES.md` now has a "Mesh Peer Pinning — TOFU and Re-pin Workflow" section that makes the TOFU window explicit (fresh install, wiped `~/.slskd`, new peer), spells out a five-step pin-before-you-trust procedure, describes what to do when a pin is lost, and calls out the known limitation that pre-seeding pins requires editing the runtime JSON file directly. No code change — this is the operator-facing companion to the runtime pin enforcement already in `CertificatePinManager`.

### H12 — DHT IP publication: LAN-only toggle + periodic warn + first-run disclosure

`Mesh.DhtRendezvous` gains a `LanOnly` option (default `false` to preserve existing behavior). When set to `true`, the DHT engine starts with an empty bootstrap router list *and* skips the saved-nodes cache, so a previously-announced IP isn't re-leaked on the next restart. A new `DhtExposureWarningService` mirrors the H8 pattern and re-logs a loud warn every 60 minutes whenever DHT is enabled and `LanOnly` is off, pointing operators at the toggle.

**Fix:**
- `src/slskd/DhtRendezvous/DhtRendezvousService.cs` — adds `LanOnly` option, honors it in engine startup (empty bootstrap list + skip saved nodes).
- `src/slskd/DhtRendezvous/DhtExposureWarningService.cs` (new) — `BackgroundService` that re-warns every 60 minutes while DHT is on and `LanOnly` is off.
- `src/slskd/Program.cs` — registers the new hosted service.

**Status:** first-run UI disclosure is now complete in `System/Network` as a one-time modal that requires explicit acknowledgment on first public-DHT startup for that browser profile.

### H13 — NowPlaying webhook: scope-restricted API keys

Operators can now issue a dedicated scope-restricted API key for their media server instead of handing Plex/Jellyfin/Tautulli a full-access key. A new `ApiKeyOptions.Scopes` property (comma-separated tags, default `"*"` = universal for backward compatibility) propagates through API key authentication into a custom `slskd:scope` claim, which the new `[RequireScope("nowplaying")]` filter enforces on the webhook endpoint. Scopes also ride on the JWT when an API key is promoted to a short-lived JWT.

The four NowPlaying endpoints split into two tiers: `GET/PUT/DELETE` remain on `AuthPolicy.Any` (admin UI), while `POST /webhook` additionally requires the `nowplaying` scope. A legacy key with `scopes="*"` continues to work unchanged.

**Fix:**
- `src/slskd/Core/Options.cs` — `ApiKeyOptions.Scopes`.
- `src/slskd/Core/Security/SecurityService.cs` — `SlskdClaims` constants, scope propagation through `AuthenticateWithApiKey` and `GenerateJwt`.
- `src/slskd/Common/Authentication/ApiKeyAuthentication.cs` — emits `slskd:scope` claims on the principal.
- `src/slskd/Common/Authentication/RequireScopeAttribute.cs` (new) — authorization filter; anonymous = no-op (upstream's job), no-scopes = universal (back-compat), wildcard/match = allow, else `403 insufficient_scope`.
- `src/slskd/NowPlaying/API/NowPlayingController.cs` — `[RequireScope("nowplaying")]` on the webhook only.
- `tests/slskd.Tests.Unit/Common/Authentication/RequireScopeAttributeTests.cs` (new) — 8 tests covering the filter's decision matrix.

### H14 — MusicBrainz / AcoustID privacy callout

Added a privacy tradeoff callout to the README immediately after the MusicBrainz Integration & Library Health section, making explicit that (a) AcoustID uploads a submitted audio fingerprint per scan, (b) MusicBrainz sees per-track MBID lookups from the node's egress IP, and (c) operators have three mitigations: leave AcoustID disabled (default), point MusicBrainz at a self-hosted mirror / VPN egress, or disable auto-tagging. No code change — these integrations are already opt-in.

### H16 — Audit docs scrubbed

The three audit artefacts tracked in the repo had hard-coded paths and one LAN IP fragment replaced with placeholders:
- `SECURITY-AUDIT-2026-03-15.md`: `/home/keith/Documents/code/slskdn` → `<repo-root>`.
- `SLSKDN-security-audit.feb26.md`: `/home/phantasm/git/slskdn` → `<slskdn-repo>`, `/home/phantasm/git/zdfinder/` → `<zdfinder-tool>/`, `192.168.1.151` → `[REDACTED-LAN-IP]`.
- `task_validation_results.json`: reviewed, clean as-is.

### H15 — Federation defaults already hermit / disabled

Verified the default state: `SocialFederationOptions.Enabled=false` and `Mode="Hermit"` out of the box. An operator has to both turn federation on *and* pick a non-hermit mode before actors become discoverable — and even then, exposure is gated on them choosing to port-forward `:5030`. No code change needed for this finding; docs corrected to reflect the real defaults.

---

## Follow-ups (not in this pass)

### H10 — Profile controller public payload audit

`ProfileController.GetProfile(peerId)` is `[AllowAnonymous]` by product intent (public peer profiles for Identity/Friends). Not a bug, but worth a pass over the `PeerProfile` DTO to confirm it doesn't expose fields that leak location, quota, or enumeration-friendly metadata. Low-effort follow-up, not in this batch.

### H12 (UI) — DHT first-run disclosure

Implemented in `System/Network` as a first-run modal when DHT is public and mesh peers are not yet discovered. The disclosure can be acknowledged once per browser.

---

## Verification

After this pass:

- `curl -s -o /dev/null -w '%{http_code}\n' http://kspls0:5030/solid/clientid.jsonld` → `404` unless `solid.clientIdUrl` is configured.
- `curl -s -o /dev/null -w '%{http_code}\n' http://kspls0:5030/api/v0/mediacore/.../descriptor/foo` → `401` anonymously.
- `POST /api/v0/federation/{actor}/inbox` without a valid HTTP signature → `401`, regardless of `federation.verify_signatures`.
- On fresh restart of an unpopulated node, `pod:gold-star-club` has zero new members unless `SLSKDN_POD_GOLD_STAR_CLUB_AUTOJOIN=true` is exported.
- `mesh-overlay.key*` not present in `git ls-files` or the repo working tree.
