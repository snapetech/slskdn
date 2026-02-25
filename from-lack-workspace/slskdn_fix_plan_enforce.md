# slskdN (slskdn) – Enforce-mode Fix Plan (Security + Correctness + DoS)

This document converts the findings in **slskdn_code_review.md** into concrete, agent-implementable fixes. Where “do not break functionality” conflicts with “enforce security,” the approach is:
- **Default-safe + back-compat where possible** (dual-verify, feature flags, allowlists).
- **Fail-fast** when a mode is explicitly unsafe (e.g., `--no-auth` + non-loopback bind) rather than running silently insecure.
- **No silent fallbacks** that hide invalid keys, broken signatures, or misconfiguration.

> Important constraint: I cannot guarantee “100% no breaks.” What I *can* do is prescribe changes that (a) preserve existing working flows when security isn’t compromised, (b) keep backwards compatibility where feasible, and (c) come with a regression test matrix that catches the typical breaks.

---

## 0) Implementation posture: “Enforce” without surprise breakage

### 0.1 Add an explicit enforcement switch
Add an explicit switch so strict behavior is deliberate and testable.

**Option A (minimal diff):** add to `Options.Web`:

- `Web.EnforceSecurity` (bool, default `false`)
- `Web.AllowRemoteNoAuth` (bool, default `false`)
- `Web.Cors` options (below)
- `Diagnostics.AllowMemoryDump` (bool, default `false`)
- `Mesh.Security.EnforceRemotePayloadLimits` (bool, default `true`)

This prevents “stealth hardening” that might surprise existing installs. The agent can run tests with Enforce on/off.

**Files:**
- `src/slskd/Core/Options.cs` (WebOptions additions)
- `src/slskd/Program.cs` (read `OptionsAtStartup.Web.EnforceSecurity`)

### 0.2 Add a single hardening invariant checker at startup
Create a small validator that runs at startup and refuses known-dangerous combinations in Enforce mode.

**Examples of “fail fast” rules (Enforce = true):**
- If `Web.Authentication.Disabled == true`, require bind to loopback OR `AllowRemoteNoAuth == true`.
- If CORS `AllowCredentials == true`, forbid wildcard origins.
- If diagnostics memory dump is enabled, require auth enabled + admin-only + local-only access.

**Files:**
- New: `src/slskd/Common/Security/HardeningValidator.cs`
- Called from `Program.Main()` before building the host.

---

## 1) Fix: “Controllers appear public” + Default deny for API

### Problem
Many controllers have neither `[Authorize]` nor `[AllowAnonymous]`. Today they are reachable anonymously because there’s no default authorization policy at the MVC/controller layer.

### Fix (preferred: MVC-level default authorization filter)
Add a global MVC filter requiring auth, and rely on `[AllowAnonymous]` only where truly intended (e.g., `SessionController.Login` already has it).

**Implementation:**
In `Program.cs`, where controllers are registered (currently around the `AddControllers(...)` chain near line ~2018 in the repo), add:

```csharp
using Microsoft.AspNetCore.Mvc.Authorization;

// after AddAuthorization(...) and AddAuthentication(...)
builder.Services
    .AddControllers(options =>
    {
        // Default-deny for controllers
        options.Filters.Add(new AuthorizeFilter(AuthPolicy.Any));
    })
    .AddJsonOptions(...)
    .AddApplicationPart(...); // keep existing chain
```

This preserves:
- endpoints explicitly marked `[AllowAnonymous]` (already present for login and “enabled” checks),
- static file serving (middleware, not MVC),
- minimal API endpoints like metrics (separate mapping).

**Also apply to SignalR hubs explicitly (Enforce mode):**
In `Program.cs` endpoint mapping (currently `endpoints.MapHub<...>` around line ~2258), add `.RequireAuthorization(AuthPolicy.Any)` for both hubs.

```csharp
endpoints.MapHub<SearchHub>("/hub/search").RequireAuthorization(AuthPolicy.Any);
endpoints.MapHub<RelayHub>("/hub/relay").RequireAuthorization(AuthPolicy.Any);
```

**Tests (must add):**
- Anonymous GET to a representative controller route returns 401.
- Anonymous calls to `[AllowAnonymous]` endpoints still succeed (e.g., `GET /api/v0/session/enabled`, `POST /api/v0/session`).

---

## 2) Fix: Auth-disabled mode creates an “admin for everyone” footgun

### Problem
When `Web.Authentication.Disabled == true`, the `PassthroughAuthenticationHandler` authenticates all requests as `Role.Administrator`, and even “challenges” by authenticating, which defeats protection.

**File:** `src/slskd/Common/Authentication/PassthroughAuthentication.cs` (see its `HandleAuthenticateAsync` and `HandleChallengeAsync`)

### Fix
1) **Restrict passthrough auth to loopback by default**  
2) **Remove the custom challenge that authenticates**  
3) Keep local dev convenience while preventing accidental remote exposure.

**Implementation details:**
- Add config: `Web.AllowRemoteNoAuth` (default false).
- If `Disabled == true`:
  - If `RemoteIpAddress` is loopback OR `AllowRemoteNoAuth == true`, authenticate.
  - Else return `AuthenticateResult.Fail(...)`.

**Code sketch:**
```csharp
protected override Task<AuthenticateResult> HandleAuthenticateAsync()
{
    var remoteIp = Context.Connection.RemoteIpAddress;

    var allowRemote = _options.Value.Web.AllowRemoteNoAuth; // new option
    var isLoopback = remoteIp != null && IPAddress.IsLoopback(remoteIp);

    if (!isLoopback && !allowRemote)
        return Task.FromResult(AuthenticateResult.Fail("No-auth mode only allowed from loopback"));

    // existing principal creation is fine, but see note below re role
    ...
}

protected override Task HandleChallengeAsync(AuthenticationProperties properties)
{
    Context.Response.StatusCode = StatusCodes.Status401Unauthorized;
    return Task.CompletedTask;
}
```

**Role note:** Keeping Administrator role is acceptable if and only if loopback-only is enforced. In Enforce mode, also require the web server binds to loopback by default.

**Startup fail-fast (Enforce = true):**
- If `--no-auth` and bind address is non-loopback, refuse to start unless explicit `AllowRemoteNoAuth=true`.

**Tests:**
- With no-auth enabled, request from loopback is authorized.
- Simulated remote IP is rejected (integration test via `TestServer` + fake connection info).

---

## 3) Fix: CORS configured as “any origin + credentials” (unsafe)

### Problem
CORS policy `AllowAll` uses `AllowAnyOrigin().AllowCredentials()` which is disallowed by browsers and unsafe. This combination can enable cross-site credential replay and confuses clients.

**File:** `Program.cs` CORS registration (around where `AddCors` defines policy `AllowAll`)

### Fix
Replace with an allowlist-based policy. Keep the policy name `AllowAll` only if you must avoid other code changes; otherwise rename to `ConfiguredCors`.

**New options under `Options.Web`:**
- `Web.Cors.Enabled` (bool, default false)
- `Web.Cors.AllowedOrigins` (string[])
- `Web.Cors.AllowCredentials` (bool, default false)
- `Web.Cors.AllowedHeaders` (string[]) optional
- `Web.Cors.AllowedMethods` (string[]) optional

**Policy builder logic:**
- If disabled: do not call `UseCors`.
- If enabled:
  - Require `AllowedOrigins` non-empty (unless dev explicitly sets `AllowAnyOrigin=true` AND `AllowCredentials=false`).
  - If `AllowCredentials=true`, use `.WithOrigins(AllowedOrigins)` and DO NOT allow wildcard.
  - Set `.SetPreflightMaxAge(TimeSpan.FromHours(1))`.

**Pipeline:**
- Only call `app.UseCors("ConfiguredCors")` when enabled.

**Tests:**
- Preflight from disallowed origin has no `Access-Control-Allow-Origin`.
- Allowed origin receives correct header.

---

## 4) Fix: Exception handler leaks internal exception messages

### Problem
The global exception handler returns raw exception messages in responses (information disclosure).

**File:** `Program.cs` `app.UseExceptionHandler(...)` block (around line ~2150)

### Fix
Return RFC 7807 `ProblemDetails` with a generic message, include only `traceId`, and log the exception. Show details only in Development.

**Implementation sketch:**
```csharp
app.UseExceptionHandler(appError =>
{
    appError.Run(async context =>
    {
        var feature = context.Features.Get<IExceptionHandlerFeature>();
        var ex = feature?.Error;

        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;
        Log.Error(ex, "Unhandled exception (traceId={TraceId})", traceId);

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";

        var problem = new ProblemDetails
        {
            Status = 500,
            Title = "Internal Server Error",
            Detail = env.IsDevelopment() ? ex?.ToString() : "An unexpected error occurred.",
            Extensions = { ["traceId"] = traceId }
        };

        await context.Response.WriteAsJsonAsync(problem);
    });
});
```

**Tests:**
- Trigger a known exception and assert the response does not include the original exception message when env != Development.
- Assert `traceId` present.

---

## 5) Fix: “/api/v0/application/dump” – remote code execution & data exfiltration

### Problem
The dump endpoint:
- is reachable to any authenticated user (not admin-only),
- uses `Dumper` which downloads and executes `dotnet-dump` from the internet at runtime, and shells out via `/bin/sh`.
This is high-risk RCE and data exfiltration.

**Files:**
- `src/slskd/Core/API/Controllers/ApplicationController.cs` (DumpMemory action)
- `src/slskd/Common/Dumper.cs` (runtime download + shell)

### Fix
1) **Gate behind config**: `Diagnostics.AllowMemoryDump` (default false)  
2) **Admin-only**: `[Authorize(Roles = AuthRole.AdministratorOnly)]` on the action  
3) **Local-only** in Enforce mode: require `RemoteIpAddress` loopback  
4) **Remove runtime download**:
   - Prefer: require `dotnet-dump` present on PATH, else return 501 with instructions.
   - If download must remain: make it explicit config `Diagnostics.AllowToolDownload=true` and validate HTTPS + pinned checksum.

5) **No shell**: use `ProcessStartInfo` directly with args array.

**Safer `Dumper` behavior:**
- `TryCreateDumpAsync(...)` returning `(bool ok, string error, string path)`; no throwing.
- Validate output directory is inside a configured safe directory.
- Limit dump size and require free disk space threshold.

**Tests:**
- When config disabled -> endpoint returns 404 or 403 (choose one; 404 hides).
- When enabled but non-admin -> 403.
- When enabled + admin + loopback -> attempts dump (mock process executor).

---

## 6) Implement the stubbed crypto and message integrity correctly

This is the largest bucket: ActivityPub HTTP signatures, Pod message signing, and envelope signing currently contain placeholders.

### 6.1 ActivityPub outbound signing is fake (HMAC) and algorithm label is wrong
**File:** `src/slskd/SocialFederation/ActivityDeliveryService.cs` (see `SignData` around its “HMACSHA256” and algorithm `"rsa-sha256"`)

#### Fix
Implement HTTP Signature signing aligned to the actual key type you generate in `ActivityPubKeyStore` (Ed25519 today).

**Recommended approach:**
- Implement `IHttpSignatureService`:
  - `CreateSignatureHeaders(HttpRequestMessage request, byte[] body, Key privateKey, string keyId)`
  - `VerifySignatureHeaders(HttpRequest request, byte[] body, Func<string, Task<Key>> resolveKeyById)`
- Use `NSec.Cryptography.SignatureAlgorithm.Ed25519`
- Use algorithm label `"ed25519"` (or `"hs2019"` if you implement that draft); do not claim `"rsa-sha256"`.

**Include digest verification:**
- Set `Digest: SHA-256=<base64>` and verify it on inbound.

**SSRF / abuse controls in key resolution:**
If you fetch remote `keyId` URLs:
- require `https`
- deny loopback, link-local, private, and multicast IPs (resolve DNS and enforce)
- limit redirects (<= 3)
- strict timeout (e.g., 3s)
- max response size (e.g., 256 KB)

### 6.2 ActivityPub inbound verification is stubbed
**File:** `src/slskd/SocialFederation/API/ActivityPubController.cs`
- `VerifyHttpSignatureAsync` currently returns `true`.
- `IsAuthorizedRequest` currently returns `true`.

#### Fix
- `VerifyHttpSignatureAsync` should:
  1) Validate required headers (`Signature`, `Date`, `Host`, `Digest` for body methods).
  2) Enforce max clock skew (± 5 minutes).
  3) Rebuild signing string according to `headers=` list.
  4) Resolve the public key for `keyId`.
  5) Verify signature.

- `IsAuthorizedRequest` in `IsFriendsOnly` mode:
  - Minimum viable: allow only requests from allowlisted domains OR from configured CIDRs.
  - If allowlist empty, deny all non-loopback (Enforce mode).

**Compatibility:**
- Keep `OptionsAtStartup.Federation.ActivityPub.VerifySignatures` controlling enforcement:
  - If `false`, log warning and accept (for dev).
  - If `true`, reject unauthenticated inbound.

### 6.3 Control envelope signing does not match envelope validator canonicalization
**Files:**
- `src/slskd/Mesh/Overlay/ControlEnvelopeValidator.cs` uses `envelope.GetSignableData()` (canonical) for verification.
- `src/slskd/Mesh/Overlay/KeyedSigner.cs` signs with a different “legacy” string (`BuildSignablePayload`).

#### Fix with backward compatibility
Update `KeyedSigner` so:
- `Sign(envelope)` uses `envelope.GetSignableData()`.
- `Verify(envelope)` tries:
  1) canonical (`GetSignableData`)
  2) legacy (`BuildSignablePayload`) as fallback for older messages.

This prevents breaking in-flight / cached envelopes while moving to a single canonical scheme.

**Add tests:**
- New signature verifies under validator.
- Legacy signatures still verify.

### 6.4 Pod messaging signing is placeholder / unverifiable
**Files:**
- `src/slskd/PodCore/MessageSigner.cs` (placeholder “SHA256(messageData + privateKey)” and verify returns true if base64)
- `src/slskd/PodCore/PodMessageRouter.cs` (envelope signature set to empty; endpoint hardcoded; multiple TODOs)

#### Fix: make message authenticity verifiable in a way that composes with the mesh
Recommended: treat Pod messages as a *payload* that is transported inside a signed `ControlEnvelope`, and enforce:
- envelope signature must verify (sender identity),
- message signature optional but strongly preferred for stored/forwarded scenarios.

**Step 1 – Fix envelope signing in PodMessageRouter**
- Inject `IControlSigner` and sign every outgoing envelope:
  - `envelope.Signature = _controlSigner.Sign(envelope);`
- On receive (wherever envelopes are accepted), validate signature using `ControlEnvelopeValidator` before processing.

**Step 2 – Replace the hardcoded loopback endpoint**
Use existing `PeerResolutionService` (`src/slskd/PodCore/PeerResolutionService.cs`) instead of `https://127.0.0.1:5000`.

- Add method to `PeerResolutionService`:
  - `Task<IReadOnlyList<IPEndPoint>> ResolvePeerEndpointsAsync(string peerId, CancellationToken ct)`
- In `PodMessageRouter.RouteMessageToPeerAsync`, resolve peer and select best endpoint (prefer QUIC vs UDP if supported).

**Step 3 – Implement real Ed25519 message signing**
In `MessageSigner`:
- Use `NSec.Cryptography.SignatureAlgorithm.Ed25519`
- Add a strict, versioned signing payload (avoid string concatenation ambiguity):
  - Use `CanonicalSerialization` pattern: fixed field order + delimiter + hash of body.
  - Include: `SigVersion`, `PodId`, `ChannelId`, `MessageId`, `SenderPeerId`, `TimestampUnixMs`, `BodySha256`.
- Set `PodMessage.Signature` to `ed25519:<base64sig>` (prefix prevents accepting random base64)

**Public key resolution for verify:**
- The only safe way to map `SenderPeerId` -> public key is via a verified membership record.
- Add `IPodMembershipService.TryGetMemberPublicKeyAsync(podId, peerId)`:
  - must return only keys that passed membership signature validation.
  - cache in-memory with TTL.

**Verification steps:**
1) Parse signature prefix and base64 decode.
2) Check timestamp skew (configurable).
3) Resolve sender pubkey from membership.
4) Verify signature over canonical payload.
5) Ensure derived peerId from public key equals claimed `SenderPeerId` (prevents key substitution).

**Compatibility note:** the current “verification” accepts any base64, and therefore cannot be “upgraded” without rejecting existing bogus signatures. To avoid breaking the *appearance* of success, add a config:
- `Pod.AllowLegacyUnverifiableSignatures` default `false`.
In Enforce mode, keep it `false`.

**Add tests:**
- message sign/verify roundtrip
- wrong body fails verify
- wrong senderPeerId fails verify
- stale timestamp fails verify

---

## 7) Fix: padding/unpadding is currently broken (NotImplemented) and can DoS

### Problem
`MessagePadder.Unpad` throws `NotImplementedException`, and when enabled it will crash message handling.

**File:** `src/slskd/Privacy/MessagePadder.cs` and `src/slskd/Mesh/Privacy/PrivacyLayer.cs`

### Fix: implement a versioned padding format that includes original length
Current padding cannot be reversed because it discards the original length. Implement a reversible format:

**Padding format v1:**
- 1 byte: format version (0x01)
- 4 bytes: original length (UInt32, big-endian)
- N bytes: message
- K bytes: random padding (to bucket)

**Pad(message):**
- Determine target bucket `B` >= header + message length.
- Allocate `B` bytes.
- Write header + message.
- Fill the remainder with cryptographically secure random bytes.

**Unpad(padded):**
- Validate minimum length, version, original length.
- Validate `originalLength` <= `padded.Length - headerSize`.
- Return the slice.

**Max sizes:**
- Enforce `originalLength <= OptionsAtStartup.Mesh.Transport.MaxRemotePayloadSize` (or a separate `Privacy.MaxUnpaddedBytes`).
- Enforce bucket <= configured `MaxPaddedBytes` to prevent huge allocations.

**Back-compat:** padding is currently non-functional (throws). Implementing it will not break a working feature.

**Tests:**
- Roundtrip pad/unpad for random messages across sizes
- Corrupt header fails cleanly (no exceptions leaking; return false / throw controlled)
- Oversized length rejected

---

## 8) Fix: unsafe deserialization & transport-level DoS hazards

### Problem
There are direct `MessagePackSerializer.Deserialize(...)` and `JsonSerializer.Deserialize(...)` calls on remotely supplied data without consistent size limits.

### Fix
1) **Centralize remote parsing through SecurityUtils**
Use:
- `SecurityUtils.ParseMessagePackSafely<T>(byte[] data, ...)`
- `SecurityUtils.ParseJsonSafely<T>(byte[] data, ...)`

2) **Enforce maximum read sizes for QUIC streams**
In QUIC overlay server, stop reading when `totalRead > MaxRemotePayloadSize`, abort stream.

3) **Apply connection throttling consistently**
If `ConnectionThrottler` exists, ensure every inbound connection path checks it early.

**Files (replace direct deserialize):**
- `src/slskd/Mesh/Overlay/QuicOverlayServer.cs`
- `src/slskd/Mesh/Overlay/UdpOverlayServer.cs`
- Any other site from grep: `JsonSerializer.Deserialize` / `MessagePackSerializer.Deserialize`

**Tests:**
- Fuzz test with random bytes does not crash.
- Oversized payloads are rejected quickly.

---

## 9) Fix: Metrics endpoint Basic Auth comparison is incorrect and non-constant-time

### Problem
Metrics endpoint compares base64 credentials using case-insensitive equality and not constant-time.

**File:** `Program.cs` metrics mapping block around lines ~2289–2296

### Fix
- Require scheme exactly “Basic” (case-insensitive ok)
- Compare base64 payload case-sensitively
- Use constant-time compare (e.g., `CryptographicOperations.FixedTimeEquals`)

**Sketch:**
```csharp
using System.Security.Cryptography;

static bool FixedTimeEqualsBase64(string a, string b)
{
    if (a is null || b is null) return false;
    var aBytes = Encoding.ASCII.GetBytes(a);
    var bBytes = Encoding.ASCII.GetBytes(b);
    return aBytes.Length == bBytes.Length && CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
}
```

Also add:
- `WWW-Authenticate: Basic realm="metrics"`

**Tests:**
- wrong creds return 401
- correct creds succeed

---

## 10) Fix: ModelState invalid filter suppressed globally

### Problem
`ApiBehaviorOptions.SuppressModelStateInvalidFilter = true;` disables automatic 400s.

**File:** `Program.cs` controller registration area

### Fix
- In Enforce mode, set to `false`.
- Optionally add a custom validation response formatter to avoid leaking internals while still returning useful errors.

**Compatibility:** might reveal latent client-side bugs; include an “Enforce toggle”.

**Tests:**
- invalid login payload returns 400 with predictable error format.

---

## 11) Fix: NotImplementedException sites should never be reachable in production

### Problem
Hard `NotImplementedException` in code paths can crash runtime if misconfigured.

**Immediate policy:**
- Anything not implemented must be:
  - not registered unless enabled
  - or return a controlled `NotSupportedException` at startup (fail fast)
  - or return a controlled 501/feature-disabled response

**Examples from review:**
- `Common/Security/I2PTransport.cs`
- `Common/Security/RelayOnlyTransport.cs`
- `MediaCore/PerceptualHasher.cs` (audio extraction)

**Fix pattern:**
- Add `Options.Flags.<Feature>.Enabled` gating registration.
- If enabled but implementation incomplete, throw on startup with explicit message.

**Tests:**
- Enabling a non-implemented feature fails fast with a clear error.

---

## 12) Regression safety: required build + test matrix

Add a minimal test project `slskd.Tests` with:
- `Microsoft.AspNetCore.Mvc.Testing`
- `xunit`, `FluentAssertions` (optional)

### Must-have tests (to catch breakage from these fixes)
1) **Auth baseline**
- anonymous request to a previously public controller route -> 401
- login endpoints remain anonymous

2) **No-auth loopback restriction**
- no-auth + loopback OK
- no-auth + remote rejected

3) **Exception handler**
- generic message in Production
- includes traceId

4) **CORS**
- disallowed origin doesn’t get allow headers
- allowed origin does

5) **Crypto**
- ControlEnvelope signatures: canonical and legacy verify
- Pod message signing: roundtrip verify

6) **Padding**
- roundtrip pad/unpad
- corrupted inputs rejected

7) **Transport parsing limits**
- oversized messagepack rejected without allocation blow-up

### CI commands
```bash
dotnet restore
dotnet build -c Release
dotnet test -c Release
```

---

## 13) Suggested implementation order (minimize risk)

1) Add Enforce switch + startup validator (prevents unsafe configs).
2) Default auth for controllers + no-auth loopback restriction.
3) Exception handler fix.
4) CORS fix.
5) Dump endpoint hardening (disable by default).
6) Padding (so privacy layer can’t crash).
7) Transport size limits + safe deserialization.
8) ControlEnvelope canonical signer + legacy verify.
9) Pod message router peer resolution + envelope signing.
10) ActivityPub real signatures + SSRF hardening.

---

## Appendix: Links to the current review artifact
- The earlier findings summary is in: `slskdn_code_review.md` (this repo workspace artifact).

