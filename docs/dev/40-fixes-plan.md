# dev/40-fixes: Security & Hardening Plan

Branch: `dev/40-fixes`  
Sources: `~/Downloads/slskdn_code_review.md`, detailed fix blueprint, Enforce-mode Fix Plan (amendments), PR-by-PR implementation ticket set (PR-00–PR-14)  
Goal: Address stop-ship issues, crypto stubs, and DoS footguns. **Default-safe + back-compat where possible.** Fail-fast when a mode is explicitly unsafe. **No silent fallbacks** that hide invalid keys, broken signatures, or misconfiguration.

> Constraint: We cannot guarantee “100% no breaks.” We (a) preserve existing working flows when security isn’t compromised, (b) keep backwards compatibility where feasible, and (c) provide a regression test matrix.

---

## Foundation. Test Harness and CI Baseline (PR-00)

**Goal:** Create a stable integration-test foundation before behavior changes.

**Scope**

- Add `tests/slskd.Tests/` using `Microsoft.AspNetCore.Mvc.Testing`
- Provide a minimal `TestHostFactory` that boots the API with test settings
- Document CI commands (e.g. `dotnet test -c Release`); no pipeline required yet

**Grep / locate**

- `grep -R "Microsoft.AspNetCore.Mvc.Testing" -n` — ensure no duplicate/conflict
- `find . -maxdepth 3 -name "*Test*.csproj"` — avoid conflicting test projects

**Files**

- New: `tests/slskd.Tests/slskd.Tests.csproj`
- New: `tests/slskd.Tests/TestHostFactory.cs`
- New: `tests/slskd.Tests/ApiSmokeTests.cs`

**Acceptance criteria**

- `dotnet test -c Release` runs and passes with at least one smoke test: `GET /api/v0/session/enabled` returns 200 or 204 (current behavior).
- No production code behavior changes.

---

## 0. Implementation Posture: “Enforce” Without Surprise Breakage (PR-01)

### 0.1 Explicit Enforcement Switch

Add under `Options.Web` (or equivalent):

| Option | Type | Default | Purpose |
|--------|------|---------|---------|
| `Web.EnforceSecurity` | bool | `false` | When true, apply strict auth, CORS, and fail-fast startup checks. Enables repeatable “hardened” testing. |
| `Web.AllowRemoteNoAuth` | bool | `false` | When `--no-auth`, allow non-loopback. If false, passthrough is loopback-only. |
| `Web.Cors` | object | see §3 | CORS allowlist; when disabled, do not use CORS. |
| `Diagnostics.AllowMemoryDump` | bool | `false` | When false, dump endpoint returns 404/403. Replaces `EnableDumpEndpoint` naming. |
| `Mesh.Security.EnforceRemotePayloadLimits` | bool | `true` | Enforce `MaxRemotePayloadSize` and safe deserialization on overlay/transport. |

**Files:** `src/slskd/Core/Options.cs`, `src/slskd/Program.cs` (read `OptionsAtStartup.Web.EnforceSecurity` and siblings).

### 0.2 Startup Hardening Validator

**New:** `src/slskd/Common/Security/HardeningValidator.cs`  
Runs in `Program.Main()` before building the host. When `EnforceSecurity == true`:

- If `Web.Authentication.Disabled == true` and bind is non-loopback → require `AllowRemoteNoAuth == true` or **refuse to start**.
- If CORS `AllowCredentials == true` → forbid wildcard/any origin.
- If `Diagnostics.AllowMemoryDump == true` → require auth enabled, admin-only, and local-only (or explicit override); else refuse or warn.

**Files:** `src/slskd/Core/Options.cs`, `src/slskd/Common/Security/HardeningValidator.cs`, `src/slskd/Program.cs`.

**Grep targets:** `class Options` in `Core/Options.cs`; `OptionsAtStartup` in `Program.cs`.

**Acceptance criteria (PR-01)**

- With Enforce off: no startup failures for existing configs.
- With Enforce on: known-dangerous combos fail with clear messages, at minimum:
  - auth disabled + non-loopback bind + `AllowRemoteNoAuth=false`
  - CORS allow-any + credentials
  - memory dump enabled while auth disabled

**Tests:** Unit tests for `HardeningValidator` (`tests/slskd.Tests/HardeningValidatorTests.cs`). Integration: Enforce on + invalid config → host fails to start (assert exception message contains the rule name).

---

## Priority Overview

| Phase | Scope | Items |
|-------|-------|-------|
| **1 – Stop-ship** | Must fix before release | 1 (default-deny auth), 2 (passthrough + loopback), 3 (CORS), 4 (exception handler), 5 (dump) |
| **2 – Crypto & integrity** | Stubs + canonicalization | 6.1–6.4 (ActivityPub, ControlEnvelope, Pod join, Pod MessageSigner + router) |
| **3 – DoS / correctness** | Padding, transport, filters | 7 (Padding), 8 (deserialization + transport DoS), F (MeshGateway body), G (rate + size), 9 (Metrics Auth), 10 (ModelState), 11 (NotImplementedException), J (ScriptService) |

### Implementation Ticket Index (PR-set)

| Ticket | Plan section | Scope |
|--------|--------------|-------|
| PR-00 | Foundation | Test harness, TestHostFactory, ApiSmokeTests |
| PR-01 | §0 | Enforce switch, HardeningValidator |
| PR-02 | §1 | Default-deny (AuthorizeFilter + hub auth) |
| PR-03 | §2 | Passthrough loopback + HandleChallengeAsync |
| PR-04 | §3 | CORS allowlist |
| PR-05 | §4 | Exception handler (ProblemDetails, traceId) |
| PR-06 | §5 | Dumper: no download/shell; gate endpoint |
| PR-07 | §10 | ModelState (SuppressModelStateInvalidFilter) |
| PR-08 | F | MeshGatewayController body + size caps |
| PR-09 | G | Rate limiting policies |
| PR-10 | §6.3 | ControlEnvelope canonicalization + legacy verify |
| PR-11 | §7 | MessagePadder.Unpad + size limits |
| PR-12 | §6.4 | Pod MessageSigner Ed25519 + membership |
| PR-13 | §6.4 | PodMessageRouter: peer resolution + envelope signing |
| PR-14 | §6.1, §6.2 | ActivityPub HTTP signatures + SSRF |

Deferred items from completed PRs are listed in **Deferred and Follow-up Work** below.

### Deferred and Follow-up Work

Items left out of completed PRs or not yet assigned to a ticket. **Must be scheduled or folded into a PR so they are not forgotten.**

- **Adding:** When you complete a PR/plan item but intentionally leave out optional or out-of-scope work, add a row (Source, Item, Action). See **AGENTS.md § After Completing Work**.
- **Removing:** When you complete a deferred item, delete its row and update `memory-bank/progress.md`, `tasks.md`, `activeContext.md`.

**Completed (removed from table):** **slskd.Tests.Unit** re-enablement (0 `Compile Remove`, 0 `[Fact(Skip)]`; 2255 pass; completion-plan, skips-how-to-fix). PR-00 Mesh/**; PR-01 EnforceRemotePayloadLimits; PR-01 Enforce+invalid integration; PR-02 Hub tests (SearchHub/RelayHub anonymous → 401 when EnforceSecurity; HubEnforceAuthTestHostFactory, stub hubs); PR-03 Passthrough AllowedCidrs; PR-05 Exception handler custom formatter (InvalidModelStateResponseFactory: in Production no internal leak in ValidationProblemDetails); PR-06 Dump 501 when creation fails; PR-07 ModelState ValidationProblemDetails consistent (same InvalidModelStateResponseFactory); PR-08 chunked POST; PR-09 Kestrel MaxRequestBodySize; PR-09 Rate limit fed/mesh integration (Burst_federation_inbox_*, Burst_mesh_gateway_* in FedMeshRateLimitTestHostFactory); §8 QuicDataServer aligned with GetEffectiveMaxPayloadSize; §9 Metrics Basic Auth constant-time; §11 NotImplementedException gating; J ScriptService deadlock.

| Source | Item | Action |
|--------|------|--------|
| **slskd.Tests.Integration** | **Build: OK** (0 errors). **Audit 2026-01-25:** MediaCore 22; Mesh 29 pass; PodCore 15; Security 50+12; VirtualSoulfind/Moderation 6 pass / 17 skip. DisasterModeTests (3) + ProtocolContractTests (6) **skipped** (SlskdnTestClient.StartAsync hang in IAsyncLifetime). MeshOnlyTests 3 pass. See `docs/dev/slskd-tests-integration-audit.md`. | Stub more controller deps in SlskdnTestClient to re-enable skipped DisasterMode/Protocol tests; review VirtualSoulfind skips. |


---

## 1. Controllers Public (No Default-Deny Auth) [Stop-Ship] (PR-02)

**Problem**  
Many controllers have neither `[Authorize]` nor `[AllowAnonymous]`; they are reachable anonymously.

**Fix: MVC-level default authorization filter**

In `Program.cs` where `AddControllers(...)` is called (around ~2018), add:

```csharp
using Microsoft.AspNetCore.Mvc.Authorization;

builder.Services.AddControllers(options =>
{
    options.Filters.Add(new AuthorizeFilter(AuthPolicy.Any));
})
.AddJsonOptions(...)  // keep existing chain
```

- Endpoints with `[AllowAnonymous]` (e.g. `SessionController.Login`, `GET /api/v0/session/enabled`) remain public.
- Static files and minimal APIs (e.g. metrics) are unchanged.

**SignalR (when `EnforceSecurity`):**  
In endpoint mapping (~2258):

```csharp
endpoints.MapHub<SearchHub>("/hub/search").RequireAuthorization(AuthPolicy.Any);
endpoints.MapHub<RelayHub>("/hub/relay").RequireAuthorization(AuthPolicy.Any);
```

**Controllers to annotate** (add `[AllowAnonymous]` only where truly public):

- `WebFingerController`, `ActivityPubController` (inbox, webfinger, actor, etc. as needed)
- `SessionController` (login, enabled) — verify existing `[AllowAnonymous]`
- `MeshGatewayController`, Audio (`AnalyzerMigrationController`, `CanonicalController`, `DedupeController`), MediaCore (ContentDescriptorPublisher, ContentId, DescriptorRetriever, FuzzyMatcher, Ipld, MediaCoreStats, MetadataPortability, PerceptualHash), PodCore (PodDht, PodDiscovery, PodJoinLeave, PodMembership, PodMessageRouting, PodMessageSigning, PodVerification), `VirtualSoulfindV2Controller` — audit each; add `[AllowAnonymous]` only on intended-public actions.

**Grep targets:** `AddControllers` in `Program.cs`; `MapHub` in `Program.cs`; `[AllowAnonymous]` across `src/slskd`.

**Acceptance criteria (PR-02):** Anonymous request to a previously unauth controller → 401. Existing anonymous endpoints still work (session/login, webfinger). Hub endpoints reject unauthenticated (401/403).

**Tests**

- Anonymous GET to a representative protected route → 401.
- `[AllowAnonymous]` routes (e.g. `GET /api/v0/session/enabled`, `POST /api/v0/session`) → 200.
- Integration: anonymous `GET /api/v0/session/enabled` succeeds; anonymous `GET` to a protected controller → 401; hub endpoints reject unauth (401/403).

---

## 2. Auth-Disabled Mode: “Admin for Everyone” + Remote Exposure [Stop-Ship] (PR-03)

**Problem**  
`PassthroughAuthenticationHandler` authenticates all requests as `Role.Administrator` and can “challenge” by authenticating, which defeats protection. When `--no-auth`, remote exposure is a footgun.

**Config**

- `Web.AllowRemoteNoAuth` (bool, default `false`).

**Implementation**

1. **Restrict passthrough to loopback by default**  
   In `PassthroughAuthentication.cs` `HandleAuthenticateAsync`:
   - `RemoteIpAddress` loopback **or** `AllowRemoteNoAuth == true` → authenticate (keep existing principal/role if acceptable when loopback-only).
   - Else → `AuthenticateResult.Fail("No-auth mode only allowed from loopback")`.

2. **Fix `HandleChallengeAsync`**  
   Do not authenticate as a “challenge.” Return 401:

   ```csharp
   protected override Task HandleChallengeAsync(AuthenticationProperties properties)
   {
       Context.Response.StatusCode = StatusCodes.Status401Unauthorized;
       return Task.CompletedTask;
   }
   ```

3. **Startup (Enforce):**  
   If `--no-auth` and bind is non-loopback → refuse to start unless `AllowRemoteNoAuth == true`.

**Optional:** `Web.Authentication.Passthrough.AllowedCidrs` (e.g. `"127.0.0.1/32,::1/128"`) for explicit CIDR allowlist instead of/in addition to loopback check.

**Files:** `src/slskd/Common/Authentication/PassthroughAuthentication.cs`, `Program.cs`, `HardeningValidator`.

**Grep targets:** `PassthroughAuthenticationHandler` in `src/slskd`; `HandleChallengeAsync` in `src/slskd/Common/Authentication`.

**Acceptance criteria (PR-03):** With no-auth enabled: loopback requests authenticate; non-loopback requests fail (unless `AllowRemoteNoAuth=true`). Enforce: startup fails if no-auth + bind non-loopback + `AllowRemoteNoAuth=false` (validated by PR-01).

**Tests**

- No-auth + loopback → authorized.
- No-auth + simulated remote IP → 401 (via `TestServer` / connection override or unit tests with mocked context).

---

## 3. CORS “Any Origin + Credentials” [Stop-Ship] (PR-04)

**Problem**  
`AllowAll` uses `SetIsOriginAllowed(_ => true)` and `AllowCredentials()`, which is unsafe (cross-site credential replay). Browsers can reject or behave inconsistently.

**New options under `Web`**

- `Web.Cors.Enabled` (bool, default `false`)
- `Web.Cors.AllowedOrigins` (string[])
- `Web.Cors.AllowCredentials` (bool, default `false`)
- `Web.Cors.AllowedHeaders` (string[]), `Web.Cors.AllowedMethods` (string[]) — optional

**Policy logic**

- If `Enabled == false`: do **not** call `app.UseCors`.
- If `Enabled == true`:
  - Require `AllowedOrigins` non-empty, **unless** an explicit `AllowAnyOrigin=true` **and** `AllowCredentials=false` (dev-only).
  - If `AllowCredentials == true` → **must** use `.WithOrigins(AllowedOrigins)`; **no** wildcard.
  - `.SetPreflightMaxAge(TimeSpan.FromHours(1))`.
- Only `app.UseCors("ConfiguredCors")` when enabled. (Rename from `AllowAll` if desired.)

**Files:** `Core/Options.cs`, `Program.cs`, `config/slskd.example.yml`.

**Grep targets:** `AddCors` in `Program.cs`; `AllowCredentials` in `Program.cs`.

**Acceptance criteria (PR-04):** When CORS disabled: responses do not include CORS headers. When enabled with allowlist: only allowed origins get `Access-Control-Allow-Origin`. Enforce on: invalid combos (e.g. wildcard + credentials) fail startup (PR-01 validator).

**Tests**

- Preflight from disallowed origin → no `Access-Control-Allow-Origin`.
- Preflight from allowed origin → correct `Access-Control-Allow-Origin` (and no `Access-Control-Allow-Credentials` if `AllowCredentials` false).
- Integration: preflight for allowed origin succeeds; disallowed origin does not.

---

## 4. Exception Handler Leaks Internal Messages [Info Leak] (PR-05)

**Problem**  
Global exception handler returns raw exception messages (information disclosure).

**Fix**

Return RFC 7807 `ProblemDetails`, `Content-Type: application/problem+json`. Include `traceId`; in Production, `Detail` is generic. In Development, include `ex` details.

```csharp
// Program.cs UseExceptionHandler
var problem = new ProblemDetails
{
    Status = 500,
    Title = "Internal Server Error",
    Detail = env.IsDevelopment() ? ex?.ToString() : "An unexpected error occurred.",
    Extensions = { ["traceId"] = traceId }
};
await context.Response.WriteAsJsonAsync(problem);
```

Log the exception server-side with `traceId`.

**Files:** `Program.cs` (`UseExceptionHandler` block ~2111/2150).

**Grep targets:** `UseExceptionHandler` in `Program.cs`; `feature.Error.Message` in `src/slskd`.

**Acceptance criteria (PR-05):** In non-Development: thrown exception does not leak original message. `traceId` included in response.

**Tests**

- Trigger an exception; in Production, response does **not** contain the original message; does contain `traceId`.
- Integration: trigger a known endpoint exception and assert response shape (ProblemDetails, traceId).

---

## 5. Dump Endpoint: RCE, Data Exfiltration, Runtime Download [Stop-Ship] (PR-06)

**Problem**  
`/api/v0/application/dump` uses `Dumper` which downloads and executes `dotnet-dump` from the internet and may shell out. Reachable by any authenticated user (not admin-only).

**Config**

- `Diagnostics.AllowMemoryDump` (bool, default `false`) — when false, 404 or 403.
- `Diagnostics.AllowRemoteDump` (bool, default `false`) — when false, require `RemoteIpAddress` loopback when dump is allowed.
- `Diagnostics.AllowToolDownload` (bool, default `false`) — if we ever keep a download path: HTTPS only, pinned checksum.

**Implementation**

1. **Gate:** If `AllowMemoryDump == false` → 404 or 403 before calling dumper.
2. **Admin-only:** `[Authorize(Roles = "Administrator")]` or policy `AuthPolicy.Administrator` on the action.
3. **Local-only (Enforce):** When `AllowRemoteDump == false`, require loopback; else 403.
4. **Remove runtime download (preferred):**
   - Use `Microsoft.Diagnostics.NETCore.Client` → `DiagnosticsClient(pid).WriteDump(...)` to a controlled path.
   - Enforce max size and free-disk threshold; 413/507 if over.
   - If `dotnet-dump`-on-PATH is used instead: no download; if not on PATH → 501 with instructions.
5. **If download remains:** Only when `AllowToolDownload == true`; HTTPS, no shell; use `ProcessStartInfo` with args array, not `/bin/sh -c`.
6. **Dumper API:** `TryCreateDumpAsync(...) → (bool ok, string error, string path)`; validate output directory; no throwing for policy decisions.

**Files:** `ApplicationController.cs`, `Common/Dumper.cs`, `Core/Options.cs` (Diagnostics), `slskd.csproj` (DiagnosticsClient).

**Grep targets:** `HttpGet("dump")` or `HttpGet(\"dump\")` in `src/slskd`; `DownloadFileTaskAsync` in `Common/Dumper.cs`; `/bin/sh` in `Common/Dumper.cs`.

**Acceptance criteria (PR-06):** Dump endpoint disabled by default. When enabled: non-admin rejected (403); remote rejected in Enforce mode (when `AllowRemoteDump` false). No runtime downloads or `/bin/sh` usage remains.

**Tests**

- `AllowMemoryDump == false` → 404/403.
- Enabled, non-admin → 403.
- Enabled, admin, remote IP and `AllowRemoteDump == false` → 403.
- Enabled, admin, loopback → attempts dump (mock or real, no network).
- Integration: 403/404 depending on config. Unit: dumper path validation.

---

## 6. Crypto and Message Integrity

### 6.1 ActivityPub Outbound: Fake HMAC, Wrong Algorithm (PR-14)

**Problem**  
`ActivityDeliveryService.SignData` uses HMACSHA256 and labels `"rsa-sha256"`; does not match Ed25519 keys from `ActivityPubKeyStore`.

**Fix**

- **`IHttpSignatureService`**:
  - `CreateSignatureHeaders(HttpRequestMessage request, byte[] body, Key privateKey, string keyId)` — build canonical string: `(request-target)`, `host`, `date`, `digest` (SHA-256 base64) for body; sign with **NSec `SignatureAlgorithm.Ed25519`**; algorithm `"ed25519"` or `"hs2019"`.
  - `VerifySignatureHeaders(HttpRequest request, byte[] body, Func<string, Task<Key>> resolveKeyById)`.
- **Digest:** Set and verify `Digest: SHA-256=<base64>` on inbound.
- **SSRF in key resolution:** If fetching `keyId` URLs: require `https`; deny loopback, link-local, private, multicast; redirects ≤ 3; timeout ~3 s; max response 256 KB.

**Files:** `ActivityDeliveryService.cs`, new `IHttpSignatureService` impl (e.g. `SocialFederation/Security/HttpSignatureService.cs`), `ActivityPubKeyStore`.

**Grep targets (PR-14):** `VerifyHttpSignatureAsync`, `HMACSHA256`, `rsa-sha256` in `SocialFederation`.

### 6.2 ActivityPub Inbound: Verification Stubbed (PR-14)

**Problem**  
`ActivityPubController`: `VerifyHttpSignatureAsync` and `IsAuthorizedRequest` effectively always return true.

**Fix**

- **`VerifyHttpSignatureAsync`:**  
  Validate `Signature`, `Date`, `Host`; `Digest` for body methods. Enforce clock skew ±5 min. Rebuild signing string from `headers=`; resolve public key for `keyId`; verify with Ed25519.
- **`IsAuthorizedRequest` / IsFriendsOnly:**  
  Allow only allowlisted domains or configured CIDRs. If allowlist empty in Enforce → deny non-loopback.
- **Option:** `SocialFederation.ActivityPub.VerifySignatures` (or `SignatureEnforcement`: Off/Warn/Enforce). If `false`: log and accept (dev). If `true`: reject unauthenticated inbound.

**Files:** `ActivityPubController.cs`, options.

**Acceptance criteria (PR-14):** In Enforce: inbox rejects unsigned/invalid-signed POSTs. Outbound: correct Signature/Digest. Key fetch SSRF-safe (https only, no private ranges, size/time caps). **Tests:** Unit: canonical signing string, header parsing. Integration: sign request and verify against controller.

### 6.3 Control Envelope: KeyedSigner vs Validator Canonicalization (PR-10)

**Problem**  
`ControlEnvelopeValidator` uses `envelope.GetSignableData()` for verification. `KeyedSigner` signs with `BuildSignablePayload(envelope)` (different format).

**Fix**

- **KeyedSigner:** `Sign(envelope)` must use `envelope.GetSignableData()`.
- **Verify:** Try (1) canonical `GetSignableData`, (2) legacy `BuildSignablePayload` for backwards compatibility.

**Files:** `Mesh/Overlay/KeyedSigner.cs`, `ControlEnvelopeValidator.cs` (if it needs to accept legacy).

**Grep targets:** `GetSignableData`, `BuildSignablePayload` in `Mesh/Overlay`.

**Acceptance criteria (PR-10):** New envelopes validate. Old envelopes still validate (legacy fallback). **Tests:** Unit: sign→verify roundtrip canonical; legacy verify path still works.

### 6.4 Pod MessageSigner and PodMessageRouter (PR-12, PR-13)

**Problem**  
`MessageSigner`: placeholder `SHA256(message+key)` and verify only checks base64. `PodMessageRouter`: envelope often unsigned; hardcoded `127.0.0.1:5000`; `VerifyMessageAsync` cannot rely on current “signature.”

**Fix (composed with mesh envelope)**

Treat Pod message as payload inside a signed `ControlEnvelope`. Envelope signature → sender identity. Message signature → optional but preferred for stored/forwarded.

**Step 1 – PodMessageRouter envelope signing**

- Inject `IControlSigner` (or `KeyedSigner`). Sign every outgoing envelope: `envelope.Signature = _controlSigner.Sign(envelope)`.
- On receive, validate with `ControlEnvelopeValidator` before processing.

**Step 2 – Replace hardcoded endpoint**

- `PeerResolutionService.ResolvePeerEndpointsAsync(peerId)` (or add to existing). Use in `RouteMessageToPeerAsync` instead of `https://127.0.0.1:5000`.

**Step 3 – MessageSigner: real Ed25519**

- **Canonical payload (versioned):** `SigVersion`, `PodId`, `ChannelId`, `MessageId`, `SenderPeerId`, `TimestampUnixMs`, `BodySha256` — fixed order, delimited, no string-concat ambiguity.
- **Format:** `PodMessage.Signature = "ed25519:<base64(sig)>"`.
- **Verify:** Parse prefix; timestamp skew; resolve sender pubkey via `IPodMembershipService.TryGetMemberPublicKeyAsync(podId, peerId)` (only keys that passed membership validation; cache with TTL). Verify sig; ensure derived peerId from pubkey equals `SenderPeerId`.
- **Config:** `PodCore.Security.SignatureMode`: Off / Warn / Enforce. `Pod.AllowLegacyUnverifiableSignatures` (default `false`); in Enforce, keep `false`.

**Files:** `MessageSigner.cs`, `PodMessageRouter.cs`, `PeerResolutionService`, `IPodMembershipService`, `PodJoinLeaveService` (E2 join signatures — see below).

**Grep targets (PR-12):** `VerifyMessageAsync`, `sha256(messageData`, `signature.Length > 10` in `PodCore`. **(PR-13):** `127.0.0.1:5000`, `envelope.Signature = ""` in `PodCore`.

**Acceptance criteria (PR-12):** Valid signed message verifies. Wrong body, wrong sender, stale timestamp fails. Enforce rejects unsigned. **(PR-13):** No hardcoded loopback; envelopes signed and verified; explicit failure modes (no silent "sent" when not).

**E2 Pod Join (retain from original plan):**  
`PodJoinLeaveService`: require `ed25519:<base64(sig)>` when `PodCore.Join.SignatureMode` is Enforce; verify with `request.PublicKey`; timestamp skew ±5 min; optional nonce for replay. Tests: valid join passes; invalid fails; replay fails if nonce implemented.

**Tests (6.4):** Message sign/verify roundtrip; wrong body/sender/timestamp fail verify. **(PR-12):** Unit: sign/verify, deterministic payload. **(PR-13):** Unit: endpoint selection; integration if router callable via API.

---

## 7. Padding: Unpad NotImplementedException and DoS (PR-11)

**Problem**  
`MessagePadder.Unpad` throws `NotImplementedException`; when enabled it crashes. Current pad format does not preserve length → cannot reverse.

**Fix: versioned format**

- **v1 layout:** 1 byte version (0x01), 4 bytes original length (UInt32 big-endian), N bytes message, K bytes random padding to bucket.
- **Pad:** Pick bucket ≥ header + message; write header + message; fill remainder with CSPRNG.
- **Unpad:** Check min length, version, `originalLength`; ensure `originalLength <= padded.Length - 5`; return slice. Reject oversized `originalLength` (e.g. > `MaxUnpaddedBytes` / `MaxRemotePayloadSize`) and bucket > `MaxPaddedBytes`.

**Files:** `Privacy/MessagePadder.cs`, `Mesh/Privacy/PrivacyLayer.cs`, options (`Privacy.MaxUnpaddedBytes`, `MaxPaddedBytes` or re-use transport limits).

**Grep targets:** `MessagePadder` in `src/slskd`; `NotImplementedException` in `Privacy/MessagePadder.cs`.

**Acceptance criteria (PR-11):** pad→unpad roundtrip works. Corrupted inputs fail cleanly. No runtime `NotImplementedException` possible. Enforce max padded and unpadded sizes.

**Tests:** Roundtrip pad/unpad; corrupt header fails cleanly; oversized length rejected. Unit: padding roundtrip + corrupted header.

---

## 8. Unsafe Deserialization and Transport DoS

**Problem**  
`MessagePackSerializer.Deserialize` and `JsonSerializer.Deserialize` on remote data without consistent size limits. QUIC streams can be read without a cap.

**Fix**

1. **Centralize:**  
   `SecurityUtils.ParseMessagePackSafely<T>(byte[] data, maxBytes, ...)` and `ParseJsonSafely<T>(...)`; enforce max size before deserialize.
2. **QUIC (and similar):**  
   In overlay server, stop when `totalRead > MaxRemotePayloadSize`; abort stream.
3. **ConnectionThrottler:**  
   Ensure every inbound overlay/transport path checks it early.

**Files:** New or existing `SecurityUtils`; `QuicOverlayServer.cs`, `UdpOverlayServer.cs`; any `MessagePackSerializer.Deserialize` / `JsonSerializer.Deserialize` on remote input. Option: `Mesh.Security.EnforceRemotePayloadLimits`.

**Tests:** Fuzz with random bytes (no crash); oversized payload rejected quickly.

---

## 9. Metrics Endpoint: Basic Auth Not Constant-Time

**Problem**  
Metrics Basic Auth uses non–constant-time comparison and may be case-insensitive on the wrong part.

**Fix**

- Scheme “Basic” (case-insensitive). Compare base64 payload with `CryptographicOperations.FixedTimeEquals` (decode to bytes first; length must match).
- Add `WWW-Authenticate: Basic realm="metrics"`.

**Files:** `Program.cs` (metrics mapping ~2289–2296).

**Tests:** Wrong creds → 401; correct → 200.

---

## 10. ModelState Invalid Filter Suppressed (PR-07)

**Problem**  
`SuppressModelStateInvalidFilter = true` disables automatic 400 for invalid models.

**Fix**

- When `EnforceSecurity == true`: set `SuppressModelStateInvalidFilter = false`.
- Optional: custom validation formatter to avoid leaking internals.

**Files:** `Program.cs` (`ConfigureApiBehaviorOptions`).

**Grep targets:** `SuppressModelStateInvalidFilter` in `Program.cs`.

**Acceptance criteria (PR-07):** Invalid payloads return 400 with structured error response when Enforce and filter enabled. Optional: consistent `ValidationProblemDetails` formatting.

**Tests:** Invalid login (or similar) payload → 400 when Enforce and filter enabled. Integration: invalid login payload returns 400.

---

## 11. NotImplementedException and Incomplete Features

**Problem**  
`NotImplementedException` in I2P, RelayOnly, PerceptualHasher, MessagePadder, etc. can crash if misconfigured.

**Policy**

- Do **not** register incomplete features, **or** fail at startup with a clear error, **or** return 501/feature-disabled at runtime.
- Add `Options.Flags.<Feature>.Enabled` (or equivalent) so enabling an incomplete feature throws at startup with an explicit message.

**Examples:** `I2PTransport.cs`, `RelayOnlyTransport.cs`, `PerceptualHasher.cs`.

**Tests:** Enabling a non-implemented feature → fail fast with clear error.

---

## F. MeshGatewayController Body Read and DoS (PR-08)

**Problem**  
Body read only when `Request.ContentLength > 0`; chunked requests get `ContentLength == null` → empty payload. Unbounded read is DoS-prone.

**Fix**

- If `ContentLength.HasValue` and `ContentLength > Max` → 413.
- **Always** read from `Request.Body` in a bounded loop: `total += read`; if `total > Max` → 413. Support chunked. Use `ArrayPool` where appropriate.

**Files:** `API/Mesh/MeshGatewayController.cs`. Possibly `Program.cs` (Kestrel limits) and/or endpoint metadata.

**Grep targets:** `MeshGatewayController` in `src/slskd`; `Request.ContentLength` in `API/Mesh/MeshGatewayController.cs`.

**Acceptance criteria (PR-08):** Chunked request body is read and processed. Oversized request rejected with 413. No unbounded allocations. Replace `ContentLength > 0` check with bounded stream read always.

**Tests:** Chunked POST with body succeeds; over limit → 413. Integration: chunked request with small payload → success; over-limit payload → 413.

---

## G. Rate Limiting and Request Size Limits (PR-09)

- `AddRateLimiter` + `UseRateLimiter`. Policies: default API (generous), federation inbox (tighter), mesh gateway (tighter). Apply via endpoint metadata or path mapping.
- Kestrel `MaxRequestBodySize` (and per-endpoint overrides where needed).
- Defaults: generous; document for operators.

**Files:** `Program.cs`, Options. Optional: small helper for policy names.

**Grep targets:** `AddRateLimiter`, `UseRateLimiter` in `src/slskd`.

**Acceptance criteria (PR-09):** Burst requests trip 429 at expected thresholds. Normal request rate does not 429.

**Tests:** Burst over limit → 429; normal usage does not. Integration: simulate burst to a throttled endpoint and assert 429.

---

## J. ScriptService Deadlock

**Problem**  
`WaitForExit()` while redirecting stdout/stderr can deadlock when buffers fill.

**Fix**

- `ReadToEndAsync` / `CopyToAsync` for stdout/stderr; `WaitForExitAsync()`; timeout and kill process tree.

**Files:** `Integrations/Scripts/ScriptService.cs`.

**Tests:** Script that writes large stderr does not deadlock; timeout kills process.

---

## Options / Config Summary

| Path | Purpose |
|------|---------|
| `Web.EnforceSecurity` | Strict auth, CORS, startup checks, ModelState |
| `Web.AllowRemoteNoAuth` | No-auth from non-loopback |
| `Web.Cors.Enabled` | Use CORS at all |
| `Web.Cors.AllowedOrigins` | Allowlist; required if Enabled and creds |
| `Web.Cors.AllowCredentials` | Only with allowlist; no wildcard |
| `Web.Cors.AllowedHeaders`, `AllowedMethods` | Optional |
| `Diagnostics.AllowMemoryDump` | Dump 404/403 when false |
| `Diagnostics.AllowRemoteDump` | Loopback-only when false |
| `Diagnostics.AllowToolDownload` | If download path kept |
| `Mesh.Security.EnforceRemotePayloadLimits` | Overlay/transport size + safe deserialize |
| `SocialFederation.ActivityPub.VerifySignatures` or `SignatureEnforcement` | Off / Warn / Enforce |
| `PodCore.Join.SignatureMode` | Off / Warn / Enforce |
| `PodCore.Security.SignatureMode` | MessageSigner |
| `Pod.AllowLegacyUnverifiableSignatures` | Default false |
| `Web.Api.RejectInvalidModelState` | Optional; in Enforce can imply true |
| `Web.RateLimiting` | Policy limits |
| `Privacy.MaxUnpaddedBytes`, `MaxPaddedBytes` | Padding caps |
| `Flags.HashFromAudioFileEnabled` | §11: when true and EnforceSecurity, startup fails (audio hash from file not implemented) |

---

## Regression Test Matrix (CI)

**Must-have**

1. **Auth:** Anonymous to protected route → 401; login / `session/enabled` remain 200.
2. **No-auth loopback:** No-auth + loopback OK; no-auth + remote → 401.
3. **Exception handler:** Production → generic message + `traceId`; no raw exception.
4. **CORS:** Disallowed origin → no allow headers; allowed → correct.
5. **Crypto:** ControlEnvelope canonical + legacy verify; Pod message sign/verify roundtrip.
6. **Padding:** Roundtrip pad/unpad; corrupt/oversized rejected.
7. **Transport:** Oversized MessagePack/JSON rejected without allocation blow-up.
8. **Dump:** Disabled → 404/403; non-admin → 403; non-loopback when local-only → 403.
9. **Metrics:** Wrong Basic creds → 401; correct → 200.
10. **ModelState:** Invalid payload → 400 when Enforce and filter on.
11. **ScriptService:** Large stderr no deadlock; timeout kills.
12. **Rate limiting (PR-09):** Burst to throttled endpoint → 429; normal rate does not.

**CI**

```bash
dotnet restore
dotnet build -c Release
dotnet test -c Release
```

---

## Suggested Implementation Order

PR numbers (see Implementation Ticket Index) can be batched for review; order below respects dependencies. **PR merge order** from the ticket set: 00 → 01 → 02,03,04,05,07 (core platform) → 06 → 08,09 (DoS) → 10,11 (mesh integrity + padding) → 12,13 (Pod) → 14 (ActivityPub). ModelState (PR-07) can be merged with 02–05; it is listed later here because it depends on Enforce.

0. **Foundation: Test harness and CI baseline (PR-00)** — `tests/slskd.Tests/`, TestHostFactory, ApiSmokeTests; `dotnet test -c Release` passes.
1. **Enforce switch + HardeningValidator (PR-01)** — prevents unsafe configs; no behavior change until Enforce on.
2. **Default auth (PR-02) + no-auth loopback (PR-03)** — §1, §2.
3. **Exception handler (PR-05)** — §4.
4. **CORS (PR-04)** — §3.
5. **Dump hardening (PR-06)** — §5 (DiagnosticsClient, no download, gating).
6. **Padding (PR-11)** — §7 (so privacy layer cannot crash).
7. **Transport size limits + safe deserialization** — §8, F (PR-08), G (PR-09) as needed.
8. **ControlEnvelope KeyedSigner canonical + legacy verify (PR-10)** — §6.3.
9. **PodMessageRouter envelope signing + PeerResolution (PR-13)** — §6.4 Step 1–2.
10. **ActivityPub real signatures + SSRF hardening (PR-14)** — §6.1, §6.2.
11. **Pod MessageSigner real Ed25519 + membership resolution (PR-12)** — §6.4 Step 3; E2 Pod Join.
12. **Metrics Basic Auth constant-time** — §9.
13. **ModelState (PR-07)** — §10.
14. **NotImplementedException / feature gating** — §11.
15. **ScriptService** — J.

---

## Checklist Before Merge

- [x] `Web.EnforceSecurity`, `AllowRemoteNoAuth`, `Web.Cors`, `Diagnostics.AllowMemoryDump`, `Mesh.Security.EnforceRemotePayloadLimits` in Options and `slskd.example.yml`.
- [x] `HardeningValidator` runs in `Main()` and enforces Enforce rules.
- [x] AuthorizeFilter + `[AllowAnonymous]` on intended-public only; SignalR `RequireAuthorization` when Enforce.
- [x] Passthrough: loopback or `AllowRemoteNoAuth`; `HandleChallengeAsync` returns 401.
- [x] CORS: allowlist when enabled; no credentials with wildcard.
- [x] Exception: ProblemDetails, `traceId`, generic detail in prod; FeatureNotImplementedException→501.
- [x] Dump: `AllowMemoryDump` default false; admin-only; local-only when `AllowRemoteDump` false; no runtime download (or gated); Dumper uses DiagnosticsClient or dotnet-dump on PATH.
- [x] ActivityPub: Ed25519 outbound, Digest, (request-target); inbound VerifyHttpSignature (Date ±5min, Digest, Ed25519/hs2019); HttpSignatureKeyFetcher SSRF-safe (HTTPS, no loopback/private/link-local/multicast, 3s, 256KB); IsAuthorizedRequest (IsFriendsOnly: loopback or ApprovedPeers).
- [x] KeyedSigner uses `GetSignableData`; Verify supports legacy.
- [x] PodMessageRouter: envelope signing, PeerResolution; MessageSigner Ed25519 + canonical payload + membership-based pubkey. (PR-12, PR-13 done.)
- [x] Padding: versioned format, Unpad implemented, size limits.
- [x] SecurityUtils parse helpers; [x] QUIC/overlay read caps (QuicOverlay abort, Udp drop, QuicData 512KB); [x] ConnectionThrottler on all inbound (QuicOverlay, UdpOverlay, QuicData).
- [x] MeshGatewayController (PR-08/F): bounded body read, 413 on over-limit, chunked support.
- [x] Metrics: FixedTimeEquals, WWW-Authenticate.
- [x] ModelState when Enforce (SuppressModelStateInvalidFilter = !EnforceSecurity); [x] NotImplemented sites gated or fail-fast (§11: I2P/RelayOnly fail at startup; PerceptualHasher/AudioUtilities→FeatureNotImplementedException/501; Flags.HashFromAudioFileEnabled+Enforce fails startup).
- [x] ScriptService: async read + WaitForExitAsync + timeout.
- [x] Regression tests above; CI green (bin/build runs slskd.Tests.Unit, slskd.Tests, slskd.Tests.Integration; `dotnet test -c Release`).

### Definition of Done for the Epic (from PR-set)

- `dotnet test -c Release` green.
- **Enforce mode on:** default-deny works; no-auth remote blocked; CORS safe; dump disabled; real crypto verification for Pod and ActivityPub write endpoints; body size caps + rate limiting active; no `NotImplementedException` crash paths in configured defaults.

---

## Follow-up: Test re-enablement

- **slskd.Tests.Unit** (many `Compile Remove`): **In scope.** Execute **slskd.Tests.Unit Re-enablement Plan** below.
- **PR-00 Mesh/** in slskd.Tests: **Done.** `MeshServiceRouterSecurityTests` fixed (MaxDescriptorBytes; assertion “exceeds”); `Mesh/**` re-enabled in slskd.Tests.

## slskd.Tests.Unit Re-enablement Plan

**Goal:** Re-enable all `Compile Remove` in `slskd.Tests.Unit.csproj`; **in scope.**

**Source of truth:** `tests/slskd.Tests.Unit/slskd.Tests.Unit.csproj` and its `Compile Remove` entries and inline blocker comments.

**Live status (done / remaining / deferred):** `docs/dev/slskd-tests-unit-completion-plan.md` (§ Completed, § Status and What Remains, § Remaining — Compile Remove, § Deferred: Skipped and Failed Tests). Prefer that doc when deciding what to do next.

### Phase 0 – API/type audit (prerequisite)

1. Build with one `Compile Remove` commented out; capture errors.
2. Classify: missing type, renamed/removed API, ctor/options, `slskd.Common.CodeQuality`, transport/options (TorSocksTransport, RateLimiter, Obfs4Options, MeekOptions, HttpTunnelOptions, WebSocketOptions), model/DTO (LocalFileMetadata, PodMessage, PodChannel, Pod, PodPrivateServicePolicy, ModerationVerdict, ContentDescriptor.Filename, IContentBackend, ContentBackendType, PlanStatus.Success, TestContext, etc.).
3. For each: fix prod, add test shim, or change/remove test; write audit (file → error → resolution).

### Phase 1

- **CodeQuality** (`Common\CodeQuality\**`): AsyncRulesTests, BuildTimeAnalyzerTests, HotspotAnalysisTests, ModerationCoverageAuditTests, RegressionHarnessTests, StaticAnalysisTests, TestCoverageTests. *(Depend on slskd.Common.CodeQuality excluded from slskd build.)*
- **Common:** LocalPortForwarderTests.
- **MediaCore:** ContentDescriptorPublisherModerationTests.
- **Relay:** RelayControllerModerationTests.

### Phase 2 – Common

- **Moderation:** ExternalModerationClientFactory, CompositeModerationProviderLlm, LocalExternalModerationClient, LlmModerationProvider, LlmModeration, RemoteExternalModerationClient; ContentIdGating, ModerationCore, PeerReputationService, PeerReputationStore. *(Blockers: LlmModeration\*, ModerationVerdict, APIs.)*
- **Security:** DnsSecurityService, IdentityConfigurationAuditor, IdentitySeparationEnforcer, SecurityUtils, SoulseekSafetyLimiter, WorkBudget; Blacklist; TokenBucket.
- **Files:** FilesControllerSecurity, FileServiceSecurity, FileService.

### Phase 3 – PodCore

- ConversationPodCoordinator, MembershipGate.
- PodCoreApiIntegration, SqlitePodMessaging, PeerIdFactory, PodIdFactory, PodModels, PodPolicyEnforcement, PrivateGatewayMeshService.
- GoldStarClub, MessageSigner, PodAffinityScorer, PodMembershipSigner, PodMessagingRouting, PodsController, PodValidation. *(Blockers: PodIdFactory, ConversationPodCoordinator, PodValidation, PodPrivateServicePolicy, Options/APIs.)*

### Phase 4 – Mesh

- **Transport:** HttpTunnel, Meek, Obfs4, WebSocket, TorSocks, RateLimiter; DescriptorSigning, SecurityUtils, TransportDialer, TransportSelector, TransportPolicy; AnonymityTransportSelection, CanonicalSerialization, CertificatePinManager, ConnectionThrottler, DnsLeakPreventionVerifier, LoggingUtils. *(Blockers: TorSocksTransport, RateLimiter, Obfs4Options, MeekOptions, HttpTunnelOptions, WebSocketOptions, ConnectionThrottler RateLimiter type.)*
- **Overlay/Privacy:** BridgeDiscovery, DecoyPod, Gossip (RealmAware), Governance (RealmAware), MeshCircuitBuilder, OverlayPrivacy, PrivacyLayer; CoverTrafficGenerator, RandomJitterObfuscator, TimedBatcher.
- **Realm:** ActivityPubBridge, BridgeFlowEnforcer, RealmMigrationTool, MultiRealm, RealmService; BridgeFlowTypes, RealmChangeValidator, MultiRealmConfig, RealmConfig, RealmIsolation.
- **ServiceFabric:** DestinationAllowlist, RateLimitTimeout, DhtMeshServiceDirectory, MeshGatewayAuthMiddleware, MeshServiceRouterSecurity, MeshServiceRouter, RouterStats. *(Align or drop duplicates with slskd.Tests; MeshServiceRouterSecurity re-enabled in slskd.Tests.)*
- **Other:** CensorshipSimulation, CircuitMaintenance, DomainFronted, MeshSyncSecurity, MeshTransportServiceIntegration, Phase8.

### Phase 5

- **SocialFederation:** ActivityPubKeyStore, FederationService, LibraryActorService, WorkRef.
- **MediaCore:** ContentIdRegistry, FuzzyMatcher, IpldMapper, MetadataPortability.
- **Audio:** CanonicalStatsService.
- **Integrations:** MusicBrainzController.
- **HashDb:** HashDbService.
- **Shares:** ShareScannerModeration.
- **Signals:** SignalBus.
- **Transfers:** UploadGovernor, UploadQueue.

### Phase 6 – VirtualSoulfind

- **Core/Planning:** LocalLibraryBackendModeration, ContentDomain, GenericFile, Music, DomainAwarePlanner, MultiSourcePlannerDomain.
- **v2:** VirtualSoulfindV2Controller, ContentBackend, HttpBackend, LanBackend, LocalLibraryBackend, MeshTorrentBackend, SoulseekBackend, CatalogueStore, LocalFileAndVerifiedCopy, CompleteV2Flow, VirtualSoulfindV2Integration, IntentQueue, SimpleMatchEngine, MultiSourcePlannerReputation, MultiSourcePlanner, IntentQueueProcessor, LibraryReconciliationService, SourceRegistry. *(Blockers: IContentBackend, LocalLibraryBackend, ContentBackendType, ContentDescriptor.Filename, PlanStatus.Success, TestContext.)*

### Execution order

Phase 0 → 1 → 2 → 3 → 4 → 5 → 6. Within each phase: (a) no prod changes where possible, (b) prod type/API tweaks, (c) larger refactors.

### Per-file workflow

1. Remove or narrow the `Compile Remove` for the file.
2. `dotnet build tests/slskd.Tests.Unit/slskd.Tests.Unit.csproj` and fix build errors.
3. `dotnet test tests/slskd.Tests.Unit/slskd.Tests.Unit.csproj --filter "FullyQualifiedName~Namespace.Class"` and fix failing tests.
4. Repeat until `dotnet test tests/slskd.Tests.Unit/slskd.Tests.Unit.csproj` passes.

### Definition of done

- No `Compile Remove` for the listed files (or a documented decision to drop a test).
- `dotnet test tests/slskd.Tests.Unit/slskd.Tests.Unit.csproj -c Release` passes (or only accepted skips).
- Audit and any prod changes recorded.

---

## Out of Scope (this branch)

- Full feature completion for MediaCore, Mesh, VirtualSoulfind beyond the security/correctness fixes above.
- Updating all external docs; `CHANGELOG` and option docs must note new flags and breaking behavior.
- All previously deferred optionals (PR-02, PR-05, PR-07, PR-09 fed/mesh) are completed. See Deferred table Completed list.

---

## Appendix: Source Artifacts

- **Code review summary:** `~/Downloads/slskdn_code_review.md` (or `slskdn_code_review.md` in repo workspace).
- **Enforce-mode amendments:** §0, §1–2 mechanics, §3–5 revisions, §6.3–6.4, §7–9, §11–13 from the “Enforce-mode Fix Plan” amendments.
- **PR-by-PR implementation ticket set (PR-00–PR-14):** Scope, grep targets, file lists, acceptance criteria, and test requirements from the epic “Enforce-mode security hardening + crypto completion + DoS controls” are integrated into the plan sections above. The Implementation Ticket Index maps each PR to its plan section. PR numbers are used as labels only; the plan’s §0–11, F, G, J structure remains the primary layout.
