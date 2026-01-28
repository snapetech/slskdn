# Solid Compatibility Layer — Implementation Map (Refined)

This document maps the **exact file paths and insertion points** for adding **minimal Solid support** (WebID resolution + Solid-OIDC Client ID Document) without breaking existing Soulseek/mesh identity flows. This is a **security-first, minimal MVP** that can be extended later.

**Reference ticket:** Add optional Solid WebID + Solid-OIDC "Pod Metadata" integration (zero breakage)

**Non-goals:** Do not change the signed structure of `PeerProfile` or `FriendInvite`; do not require Pod/WebID for core features; do not implement full OIDC flow in MVP (just Client ID doc + WebID resolution).

---

## 1. Backend: Options (Feature flag + Solid block)

### File: `src/slskd/Core/Options.cs`

**1.1 Add Feature flag**

- **Location:** Inside `FeatureOptions` class.
- **Insert after line 1445** (after `IdentityFriends` property):
  ```csharp
            /// <summary>
            /// Enable Solid / WebID / Solid-OIDC integration. When false, Solid APIs return 404.
            /// </summary>
            public bool Solid { get; init; } = false;
  ```

**1.2 Add root Solid property**

- **Insert between lines 352 and 354** (after `Feature`, before `Sharing`):
  ```csharp
        /// <summary>
        ///     Gets Solid options (WebID, Solid-OIDC, Pod metadata).
        /// </summary>
        [Validate]
        public SolidOptions Solid { get; init; } = new SolidOptions();
  ```

**1.3 Add SolidOptions class**

- **Insert between lines 1465 and 1466** (after `ScenePodBridgeOptions` ends, before `SharingOptions` begins):
  ```csharp
        /// <summary>
        ///     Solid options (WebID, Solid-OIDC, Pod metadata).
        /// </summary>
        public class SolidOptions
        {
            /// <summary>
            /// If true, allow plain http:// WebID/Pod URLs (ONLY for dev/test). Keep false in prod.
            /// </summary>
            public bool AllowInsecureHttp { get; init; } = false;

            /// <summary>Max bytes we will read from WebID profile / Pod metadata resources.</summary>
            public int MaxFetchBytes { get; init; } = 1_000_000;

            /// <summary>HTTP timeout for WebID/Pod fetches.</summary>
            public int TimeoutSeconds { get; init; } = 10;

            /// <summary>
            /// Allowed Pod/WebID hostnames (exact match). Empty = deny all remote fetches unless explicitly set.
            /// </summary>
            public string[] AllowedHosts { get; init; } = Array.Empty<string>();

            /// <summary>
            /// Where the Solid-OIDC Client ID document is served from (default: /solid/clientid.jsonld).
            /// Leave empty to auto-derive from request base URL.
            /// </summary>
            public string? ClientIdUrl { get; init; }

            /// <summary>
            /// Redirect URI path used for Solid-OIDC (default: /solid/callback). (Callback wiring can come later.)
            /// </summary>
            public string RedirectPath { get; init; } = "/solid/callback";
        }
  ```

**Do not:** Change any existing option property names or types used for binding.

---

## 2. Backend: Add dotNetRDF dependency

### File: `src/slskd/slskd.csproj`

- **Insert in existing `<ItemGroup>`** with other `PackageReference` entries (e.g. after line ~156):
  ```xml
    <PackageReference Include="dotNetRDF" Version="3.4.1" />
  ```

---

## 3. Backend: Program.cs wire-up

### File: `src/slskd/Program.cs`

**3.1 Register Solid services**

- **Insert after line 2071** (in `ConfigureDependencyInjectionContainer`, after Identity registrations):
  ```csharp
            // Solid / WebID / Solid-OIDC (optional; gated per-request by Feature.Solid)
            services.AddSingleton<slskd.Solid.ISolidClientIdDocumentService, slskd.Solid.SolidClientIdDocumentService>();
            services.AddSingleton<slskd.Solid.ISolidWebIdResolver, slskd.Solid.SolidWebIdResolver>();
            services.AddSingleton<slskd.Solid.ISolidFetchPolicy, slskd.Solid.SolidFetchPolicy>();
  ```

**3.2 Add anonymous endpoint for Client ID Document**

- **Insert between lines 2696 and 2698** (after `endpoints.MapControllers();`, before health mapping):
  ```csharp
                endpoints.MapGet("/solid/clientid.jsonld", async context =>
                {
                    var opts = context.RequestServices.GetRequiredService<IOptionsMonitor<slskd.Options>>();
                    if (!opts.CurrentValue.Feature.Solid)
                    {
                        context.Response.StatusCode = StatusCodes.Status404NotFound;
                        return;
                    }

                    var svc = context.RequestServices.GetRequiredService<slskd.Solid.ISolidClientIdDocumentService>();
                    context.Response.ContentType = "application/ld+json";
                    await svc.WriteClientIdDocumentAsync(context, context.RequestAborted).ConfigureAwait(false);
                }).AllowAnonymous();
  ```

---

## 4. Backend: Solid module (new files)

**Create folder:** `src/slskd/Solid/`

### 4.1 Client ID Document Service

**New file:** `src/slskd/Solid/ISolidClientIdDocumentService.cs`
```csharp
namespace slskd.Solid;

using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

public interface ISolidClientIdDocumentService
{
    Task WriteClientIdDocumentAsync(HttpContext http, CancellationToken ct);
}
```

**New file:** `src/slskd/Solid/SolidClientIdDocumentService.cs`
- Must generate compliant JSON-LD with `@context` and OIDC client metadata.
- See refined ticket section 4B for full implementation (derives base URL, uses `SolidOptions.ClientIdUrl` and `RedirectPath`).

### 4.2 SSRF hardening policy

**New file:** `src/slskd/Solid/ISolidFetchPolicy.cs`
```csharp
namespace slskd.Solid;

using System;
using System.Threading;
using System.Threading.Tasks;

public interface ISolidFetchPolicy
{
    Task ValidateAsync(Uri uri, CancellationToken ct);
}
```

**New file:** `src/slskd/Solid/SolidFetchPolicy.cs`
- Enforces: HTTPS only (unless `AllowInsecureHttp`), host allow-list (`AllowedHosts`), blocks localhost/.local/private IPs.
- See refined ticket section 4C for full implementation.

### 4.3 WebID resolver

**New file:** `src/slskd/Solid/ISolidWebIdResolver.cs`
```csharp
namespace slskd.Solid;

using System;
using System.Threading;
using System.Threading.Tasks;

public interface ISolidWebIdResolver
{
    Task<SolidWebIdProfile> ResolveAsync(Uri webId, CancellationToken ct);
}

public sealed record SolidWebIdProfile(Uri WebId, Uri[] OidcIssuers);
```

**New file:** `src/slskd/Solid/SolidWebIdResolver.cs`
- Uses `ISolidFetchPolicy` to validate URI.
- Fetches with timeout + max bytes (`MaxFetchBytes`, `TimeoutSeconds`).
- Parses RDF (dotNetRDF) to locate `solid:oidcIssuer`.
- See refined ticket section 4D for full implementation.

---

## 5. Backend: API controller

**New file:** `src/slskd/Solid/API/SolidController.cs`

- **Route:** `[Route("api/v{version:apiVersion}/solid")]`, `[ApiVersion("0")]`
- **Auth:** `[Authorize(Policy = AuthPolicy.Any)]`, `[ValidateCsrfForCookiesOnly]`
- **Feature gate:** Every action checks `if (!Enabled) return NotFound();` where `Enabled => _options.CurrentValue.Feature.Solid`
- **Endpoints:**
  - `GET /api/v0/solid/status` — returns enabled status, clientId, redirectPath
  - `POST /api/v0/solid/resolve-webid` — body `{ webId: string }`, returns `{ webId, oidcIssuers: string[] }`
- **Reference pattern:** `src/slskd/Identity/API/ContactsController.cs` (Enabled property, NotFound when disabled).

See refined ticket section 5 for full controller implementation.

---

## 6. Web UI

### 6.1 New component

**New folder:** `src/web/src/components/Solid/`

**New file:** `src/web/src/components/Solid/SolidSettings.jsx`
- Shows status (enabled/disabled), Client ID, redirect path.
- WebID input field + "Resolve WebID" button.
- Displays resolved OIDC issuers.
- Warns about AllowedHosts requirement.

See refined ticket section 6D for full component implementation.

### 6.2 Wire into App.jsx

**File:** `src/web/src/components/App.jsx`

**6.2.1 Import**
- **Insert between lines 25 and 26** (after other component imports):
  ```jsx
import SolidSettings from './Solid/SolidSettings';
  ```

**6.2.2 Navigation link**
- **Insert between lines 458 and 459** (between Contacts and Collections menu links):
  ```jsx
                <Link to="/solid">
                  <Menu.Item data-testid="nav-solid">
                    <Icon name="key" />
                    Solid
                  </Menu.Item>
                </Link>
  ```

**6.2.3 Route**
- **Insert between lines 672 and 673** (after `/collections` route closes, before `/searches` route begins):
  ```jsx
                  <Route
                    exact
                    path="/solid"
                    render={(props) =>
                      this.withTokenCheck(
                        <div className="view">
                          <SolidSettings {...props} />
                        </div>,
                      )
                    }
                  />
  ```

---

## 7. Config example

### File: `config/slskd.example.yml`

- **Location:** Under the existing `# feature:` block (starts ~line 225). Add commented entries:
  ```yaml
  #   Solid: false              # Enable Solid (WebID, Solid-OIDC) (default: false)
  # solid:
  #   allowedHosts: []          # Empty = deny all remote fetches (SSRF safety)
  #   timeoutSeconds: 10
  #   maxFetchBytes: 1000000
  #   allowInsecureHttp: false  # ONLY for dev/test
  #   redirectPath: "/solid/callback"
  ```

**Important:** `allowedHosts` empty = deny all remote fetches. This is intentional for SSRF safety.

---

## 8. Tests

### 8.1 Unit tests (backend)

**Project:** `tests/slskd.Tests.Unit/`

**New folder:** `tests/slskd.Tests.Unit/Solid/`

- **Client ID doc endpoint:** Returns 404 when `Feature.Solid=false`, returns `application/ld+json` when true, validates JSON-LD shape.
- **SolidFetchPolicy:** Blocks `http://` when `AllowInsecureHttp=false`, blocks `https://localhost/...`, blocks hosts not in `AllowedHosts`, blocks private IPs.
- **SolidWebIdResolver:** Response-size limit triggers for > `MaxFetchBytes`, parses Turtle/JSON-LD to extract `solid:oidcIssuer`.

### 8.2 E2E / integration (CI-safe)

- **Do not** depend on a real Pod in CI.
- Add a **fake Solid server** in test harness: serves WebID doc (Turtle) from a local allowed host.
- Playwright test: enable `Feature.Solid` + set `AllowedHosts` to include local mock host, navigate `/solid`, resolve WebID, assert issuers list renders.

---

## 9. Critical "do not" checklist

- **Do not** change `PeerProfile` or `FriendInvite` signed structure or schema.
- **Do not** log tokens, authorization codes, DPoP proofs, or full `Authorization` headers.
- **Do not** require WebID/Pod for core Soulseek or mesh features; everything works without Solid.
- **Do not** allow remote fetches without explicit `AllowedHosts` (empty list = deny all).
- **Do not** allow `http://` by default (only with `AllowInsecureHttp=true` for dev/test).
- **Do not** add automatic builds on push; builds remain tag-only per project rules.
- **Do not** implement full OIDC flow in MVP (just Client ID doc + WebID resolution).

---

## 10. File and folder summary

| Item | Path |
|------|------|
| Feature + Solid options | `src/slskd/Core/Options.cs` (lines 352-354, 1445, 1465-1466) |
| Solid module root | `src/slskd/Solid/` |
| Client ID doc service | `src/slskd/Solid/ISolidClientIdDocumentService.cs`, `SolidClientIdDocumentService.cs` |
| SSRF policy | `src/slskd/Solid/ISolidFetchPolicy.cs`, `SolidFetchPolicy.cs` |
| WebID resolver | `src/slskd/Solid/ISolidWebIdResolver.cs`, `SolidWebIdResolver.cs` |
| API controller | `src/slskd/Solid/API/SolidController.cs` |
| Client ID doc route | `Program.cs` lines 2696-2698 (`MapGet` with `AllowAnonymous`) |
| Program.cs registrations | After line 2071 (Identity services) |
| NuGet dependency | `src/slskd/slskd.csproj` (dotNetRDF ~3.4.1) |
| Web UI component | `src/web/src/components/Solid/SolidSettings.jsx` |
| App.jsx imports | Line 25-26 (import) |
| App.jsx nav link | Line 458-459 (menu item) |
| App.jsx route | Line 672-673 (Route component) |
| Example config | `config/slskd.example.yml` (feature + solid block) |
| Unit tests | `tests/slskd.Tests.Unit/Solid/` |

---

## 11. Differences from original ticket

This refined map reflects a **minimal MVP** that:

1. **Focuses on WebID resolution + Client ID Document** (not full OIDC flow yet).
2. **Adds explicit SSRF hardening** (`SolidFetchPolicy` with host allow-list, HTTPS enforcement, private IP blocking).
3. **Simplifies SolidOptions** (no `AppContainerName`, `PreferSaiRegistry`, `PublishPublicPeerCard` — those come later).
4. **Uses exact line anchors** from current codebase (352/354, 1445, 1465/1466 for Options.cs; 2071, 2696-2698 for Program.cs; 25/26, 458/459, 672/673 for App.jsx).

**Future extensions** (not in MVP):
- Full OIDC Authorization Code + PKCE flow
- Token store (encrypted via Data Protection)
- DPoP proof generation
- Pod metadata read/write (playlists, sharelists)
- Type Index / SAI registry discovery
- Access control (WAC/ACP) writers

These can be added incrementally after the MVP is working.

---

Use this map together with the refined Solid ticket for implementation. The refined ticket includes full code implementations for each service file.
