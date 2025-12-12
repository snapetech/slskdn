# VirtualSoulfind v2 Hardening Tasks (H-11 through H-15)

**Branch**: `experimental/multi-source-swarm`  
**Created**: December 11, 2025  
**Status**: Planning Phase  
**Design Doc**: `docs/virtualsoulfind-v2-design.md`

---

## Overview

These are detailed, implementation-ready hardening tasks for VirtualSoulfind v2. They correspond to and expand upon the H-VS01 through H-VS12 tasks in the design doc, providing concrete implementation guidance in the same "paste into Cursor" format as T-SF0x and H-0x briefs.

**Priority**: These tasks are MANDATORY before VirtualSoulfind v2 deployment.

**Dependencies**:
- H-01 (Gateway Auth/CSRF) ✅ Complete
- H-02 (Work Budget) ⏳ Required - BLOCKS H-13, H-14
- H-08 (Soulseek Caps) ⏳ Required - BLOCKS H-13, H-14
- T-SF01-04 (Service Fabric) ✅ Complete

---

## H-11 – VirtualSoulfind Identity Separation & Privacy Mode

**Corresponds to**: H-VS01 (Privacy Mode), H-VS09 (Logging Hygiene)  
**Priority**: P0 (Critical for V2-P1)  
**Status**: 📋 Planned

### Repo: `https://github.com/snapetech/slskdn`
### Branch: `experimental/multi-source-swarm`

You are working on the VirtualSoulfind v2 design described in `docs/virtualsoulfind-v2-design.md`. H-11 hardens **identity separation and privacy behavior** for VirtualSoulfind.

---

### 0. Scope & Non-Goals

You **must**:

1. Ensure VirtualSoulfind's internal data model never stores raw third-party Soulseek identifiers (usernames, IPs, room names) inside its catalogue/intent/source tables.
2. Introduce a **Privacy Mode** for VirtualSoulfind that controls how much external identifier/state is stored and exposed.
3. Tighten logging for VirtualSoulfind so it does not expose sensitive identifiers or local file paths by default.

You **must NOT**:

* Break existing Soulseek integration; Soulseek-specific identifiers can still exist in the Soulseek module and its own tables.
* Redesign the mesh identity system; just interact correctly with it.
* Introduce new dependencies.

---

### 1. Recon

Before you code, locate and understand:

1. VirtualSoulfind-related code:
   * Current "VirtualSoulfind" classes (catalogue, index, MBID handling).
   * Where `SourceCandidate`-like concepts are defined (if already implemented).
   * Where Soulseek/mesh/BT sources are attached to VirtualSoulfind entities.

2. Soulseek integration:
   * How Soulseek peers and file paths are stored.
   * Where usernames, room names and IP addresses are persisted or used.

3. Logging and metrics:
   * Where VirtualSoulfind logs events.
   * Existing metric emission for VirtualSoulfind, if any.

---

### 2. Data Model Hardening

You will introduce or update VirtualSoulfind entities to enforce identity separation.

#### 2.1 `SourceCandidate` and external identifiers

Update the `SourceCandidate` (or equivalent) structure to ensure:

* For **Soulseek**:
  * `BackendRef` is a *local* ID (e.g. `SoulseekPeerInternalId` and/or `SoulseekFileInternalId`), not raw username + path.
  * The actual username, IP, and on-wire path are only accessible through the Soulseek subsystem.

* For **Mesh/DHT**:
  * `BackendRef` uses internal mesh identifiers (peer IDs, descriptor IDs) only.
  * No raw IP addresses stored in VirtualSoulfind tables.

* For **Local library**:
  * Local paths may exist but should be:
    * Stored as consistently normalized paths.
    * Not logged verbatim at INFO/WARN in VirtualSoulfind logs.

Make sure all existing uses of `SourceCandidate` are updated to follow this rule.

#### 2.2 Separate mapping tables for Soulseek identities

If you need to map:
* Soulseek username/IP/path → internal `SoulseekPeerId` / `SoulseekFileId`,

keep that mapping in a **Soulseek-specific** table/module, not in VirtualSoulfind tables.

VirtualSoulfind should only see opaque IDs, never raw usernames/IPs.

---

### 3. VirtualSoulfind Privacy Mode

Add a privacy configuration section:

```jsonc
"VirtualSoulfind": {
  "PrivacyMode": "Normal" // or "Reduced"
}
```

Implement at least two modes:

1. `Normal`:
   * Full VirtualSoulfind functionality.
   * `SourceCandidate` stores opaque backend references (as above), but may store trust scores, last seen, etc.

2. `Reduced`:
   * VirtualSoulfind **does not** persist detailed per-peer or per-source identity at all.
   * `SourceCandidate` entries may be represented as:
     * "Available via backend X" without storing individual peer IDs.
   * Trust/reputation can be aggregated per backend or per coarse group, not per peer.

Ensure:
* In `Reduced` mode:
  * Any code that tries to persist per-peer identity is either disabled, or stores only coarse-grained signals (e.g. counts, aggregated scores).

---

### 4. Logging & Metrics Hygiene

Harden VirtualSoulfind's logging:

1. **Logging rules:**
   * INFO/WARN/ERROR from VirtualSoulfind must NOT:
     * Include raw Soulseek usernames.
     * Include external IP addresses.
     * Include full local file paths (only filenames or sanitized paths at most).
   * DEBUG/TRACE may include more, but:
     * Must be clearly marked.
     * Should still avoid sensitive identifiers by default unless absolutely necessary.

2. **Implementation:**
   * Introduce small helpers:
     * `LogSafePath(string localPath)`: returns a truncated/sanitized version.
     * Methods to summarize source candidates without revealing IDs (e.g. "3 mesh candidates, 1 local, 0 Soulseek").

Update existing logs in VirtualSoulfind code to use these.

---

### 5. Testing (H-11)

Add tests to cover:

1. **Data model enforcement:**
   * When creating `SourceCandidate` for Soulseek:
     * Assert that usernames are not stored in `SourceCandidate`, only internal IDs.
   * For Mesh:
     * Assert that no IP string is persisted in VirtualSoulfind structures.

2. **Privacy mode:**
   * In `Normal` mode:
     * Verify that peer-level details are stored (opaque IDs), as expected.
   * In `Reduced` mode:
     * Verify that VirtualSoulfind avoids storing per-peer references and instead uses aggregated/abstracted representations.

3. **Logging:**
   * Use test logging sinks to ensure:
     * Logs do not contain raw usernames/paths at INFO/WARN.

---

### 6. Anti-Slop Checklist for H-11

Before considering H-11 done:

* All VirtualSoulfind persistence obeys:
  * Opaque IDs only for external peers.
  * No Soulseek usernames/IPs in its own tables.
* Privacy mode (`Normal`/`Reduced`) is implemented and respected.
* Logging for VirtualSoulfind is safe by default and doesn't leak paths or identifiers.

---

## H-12 – Catalogue & Intent Queue Hardening

**Corresponds to**: H-VS02 (Intent Queue Security), H-VS12 (Data Directory Permissions)  
**Priority**: P0 (Critical for V2-P2)  
**Status**: 📋 Planned

### Repo: `https://github.com/snapetech/slskdn`
### Branch: `experimental/multi-source-swarm`

H-12 hardens the **virtual catalogue store** and **intent queue** against privacy leaks and abuse via mesh and gateway.

---

### 0. Scope & Non-Goals

You **must**:

1. Protect VirtualSoulfind's catalogue and intent queue storage from easy local abuse.
2. Limit remote (mesh/gateway) ability to flood the intent queue.
3. Make remote intent management **optional and disabled by default**.

You **must NOT**:

* Block legitimate local usage (UI, CLI, local automation).
* Redesign the DB engine; work with whatever store the repo uses now.

---

### 1. Recon

Find:

1. Where catalogue and intent data is stored:
   * Database/schema or in-memory structures.
2. Any existing API/service that:
   * Creates `DesiredRelease` / `DesiredTrack`.
   * Reads/modifies the intent queue.
3. Gateway endpoints and mesh services that expose VirtualSoulfind intent functionality (if already exists).

---

### 2. Local Storage Hardening

1. **Data directory / file permissions:**
   * Confirm VirtualSoulfind DBs (catalogue + intents) live under a dedicated app directory (e.g. `~/.config/slskdn/virtualsoulfind`).
   * Ensure your installation/runtime instructions:
     * Run slskdn as dedicated user (from earlier H-09).
   * If any code sets permissions (e.g., creates directories):
     * Use restrictive defaults (user-only).

2. **Config documentation:**
   * Add notes in docs:
     * Where VirtualSoulfind data is stored.
     * How to secure it (filesystem permissions, disk encryption).

---

### 3. Remote Intent Management Controls

Add configuration:

```jsonc
"VirtualSoulfind": {
  "AllowRemoteIntentManagement": false,
  "MaxRemoteIntentsPerPeerPerHour": 20,
  "MaxRemoteIntentsPerIpPerHour": 100
}
```

Rules:

1. When `AllowRemoteIntentManagement = false`:
   * Mesh service methods and HTTP endpoints that create or modify intents:
     * MUST reject remote calls (return 403-equivalent or not be exposed).
   * Local usage (in-process API, CLI, UI) is still allowed.

2. When enabled:
   * Apply per-peer/per-IP limits:
     * For each mesh peer or remote IP, track how many intents they've created in the last N minutes/hour.
     * Reject further creation once `MaxRemoteIntents*` is exceeded.

---

### 4. Origin Tagging for Intents

Add an `Origin` field to `DesiredRelease` / `DesiredTrack`, e.g.:

```csharp
public enum IntentOrigin
{
    UserLocal,
    LocalAutomation,
    RemoteMesh,
    RemoteGateway
}
```

Set `Origin` when creating intents:

* UI/CLI → `UserLocal`.
* Internal automation jobs → `LocalAutomation`.
* Mesh service calls → `RemoteMesh`.
* HTTP gateway calls → `RemoteGateway`.

This will be used by the resolver (H-14) and for security/analytics.

---

### 5. Testing (H-12)

Add tests to verify:

1. **AllowRemoteIntentManagement = false:**
   * Mesh and HTTP attempts to create intents:
     * Are rejected (proper error code).
   * Local code (e.g. a test harness calling the service directly in-process) still creates intents.

2. **Rate limits:**
   * With remote management enabled:
     * Simulate remote creation of more than N intents:
       * Ensure the limit is enforced and further requests are rejected.
   * Ensure different origins are tracked separately.

3. **Origin tagging:**
   * Intents created through different code paths have the correct `Origin` value.

---

### 6. Anti-Slop Checklist for H-12

* Intent queue can no longer be silently flooded from remote sources without tripping limits.
* Remote intent management is off by default.
* Intents are tagged with origin, enabling downstream policies to treat them differently.

---

## H-13 – Backend Adapter Safety (Work Budget + SSRF Guards)

**Corresponds to**: H-VS03 (Backend Work Budget), H-VS04 (SSRF Protection)  
**Priority**: P0 (Critical for V2-P4)  
**Status**: 📋 Planned  
**DEPENDS ON**: H-02 (Work Budget), H-08 (Soulseek Caps)

### Repo: `https://github.com/snapetech/slskdn`
### Branch: `experimental/multi-source-swarm`

H-13 hardens **backend adapters** used by VirtualSoulfind (Soulseek, MeshDHT, Torrent, HTTP, LAN, Local) so they cannot bypass the work budget or be used for SSRF-style abuse.

---

### 0. Scope & Non-Goals

You **must**:

1. Ensure all `IContentBackend` implementations respect the global work budget (H-02).
2. Add network safety guards for HTTP/LAN backends (block private/loopback ranges by default, etc.).
3. Confirm Soulseek-specific safety caps (H-08) are enforced for any VirtualSoulfind-initiated calls.

You **must NOT**:

* Change existing network protocol semantics.
* Add heavy new dependencies.

---

### 1. Recon

Find:

1. `IContentBackend` (or equivalent) interface and implementations.

2. Where backend calls are triggered by VirtualSoulfind:
   * Soulseek search/browse calls.
   * Mesh/DHT queries.
   * Torrent metadata calls.
   * HTTP and LAN discovery/fetches.

3. How the work budget from H-02 is passed around (e.g. `MeshServiceContext` or other context).

---

### 2. Enforce Work Budget in Backends

For each backend implementation:

1. Inject or access the current work budget (e.g. via a context object passed into VirtualSoulfind operations).

2. Before any **external** call:
   * For Soulseek search/browse:
     * Call `WorkBudget.TryConsume(WorkCosts.SoulseekSearch)` or `.SoulseekBrowse`.
   * For MeshDHT queries:
     * Consume `WorkCosts.MeshServiceCall` or a specific DHT cost.
   * For Torrent metadata fetches:
     * Consume `WorkCosts.TorrentMetadataFetch`.
   * For HTTP/LAN:
     * Consume appropriate "HTTPFetch" or "LanQuery" costs.

3. If `TryConsume` fails:
   * Immediately abort and return an error / empty result:
     * Do not attempt the external call.

---

### 3. Soulseek Caps Integration

When VirtualSoulfind causes Soulseek calls (search/browse):

* The backend must additionally enforce the Soulseek caps from H-08:
  * `MaxSearchesPerMinute`, `MaxBrowsesPerMinute`, etc.
* If caps are exceeded:
  * Fail with a "limit exceeded" style error and log at INFO/WARN.

Ensure no direct Soulseek calls from VirtualSoulfind bypass this limiter.

---

### 4. HTTP / LAN Backend SSRF Guards

If you have HTTP/LAN backends:

1. Build a central outbound network guard wrapper, e.g.:

```csharp
public interface ISafeHttpClient
{
    Task<HttpResponseMessage> SendAsync(SafeHttpRequest request, CancellationToken ct);
}
```

Where `SafeHttpRequest` includes:
* Target URL/hostname.
* Headers/body.

**Guard logic:**

* Resolve hostnames/IPs.
* Block:
  * Loopback (127.0.0.0/8, ::1).
  * Private ranges (10.0.0.0/8, 192.168.0.0/16, 172.16.0.0/12).
  * Link-local and other special ranges.
* Unless a specific config explicitly allows those ranges (and only if you really need it).

2. Replace any direct `HttpClient` usage in VirtualSoulfind backends with `ISafeHttpClient`.

3. For LAN backends:
* Ensure they only operate on:
  * Configured LAN segments.
* Do not accept arbitrary, user-provided CIDRs without validation.

---

### 5. Testing (H-13)

Add tests to ensure:

1. **Work budget enforcement:**
   * With a small budget, attempts to call backend methods eventually hit budget limit and stop.
   * No external calls are made once budget is exhausted (mock external calls).

2. **Soulseek caps:**
   * Simulate multiple Soulseek calls via VirtualSoulfind backend.
   * Confirm the limiter from H-08 triggers as expected.

3. **SSRF guard:**
   * For HTTP backend:
     * Requests to `http://127.0.0.1/...` or `http://10.x.x.x/...` are rejected by default.
   * Requests to allowed public hosts succeed (assuming mocks).

---

### 6. Anti-Slop Checklist for H-13

* All VirtualSoulfind backend actions respect work budget and Soulseek caps.
* HTTP/LAN cannot be used to hit loopback or private IPs unless explicitly configured.
* No direct external network calls remain in VirtualSoulfind that bypass these guards.

---

## H-14 – Planner & Resolver Safety (Global Caps, Origin, Modes)

**Corresponds to**: H-VS05 (Resolver Throughput), H-VS06 (Plan Validation)  
**Priority**: P0 (Critical for V2-P5)  
**Status**: 📋 Planned  
**DEPENDS ON**: H-02 (Work Budget), H-08 (Soulseek Caps), H-12 (Origin Tagging)

### Repo: `https://github.com/snapetech/slskdn`
### Branch: `experimental/multi-source-swarm`

H-14 hardens the **multi-source planner** and **resolver** so they can't be weaponized to cause unbounded work or violate Soulseek/mode policy.

---

### 0. Scope & Non-Goals

You **must**:

1. Introduce global caps on how much work the resolver does per run.
2. Enforce **origin-based** prioritization (UserLocal vs Remote).
3. Ensure planner respects modes (`OfflinePlanning`, `MeshOnly`, `SoulseekFriendly`) and that plan execution cannot violate budgets/caps.

You **must NOT**:

* Rewrite the entire planner; incrementally add safety and enforcement.
* Break existing local (UserLocal) flows under normal load.

---

### 1. Recon

Find:

1. Planner implementation:
   * Where `TrackAcquisitionPlan` / `PlanStep` are created.
2. Resolver / executor:
   * The job/loop that pulls `DesiredRelease` / `DesiredTrack` from the queue and executes plans.
3. Where mode configuration lives, if already defined.

---

### 2. Global Resolver Caps

Add configuration:

```jsonc
"VirtualSoulfind": {
  "Resolver": {
    "MaxTracksPerRun": 50,
    "MaxConcurrentPlans": 5
  }
}
```

Implement:

* `MaxTracksPerRun`:
  * In each resolver run, limit the total number of tracks it attempts to plan/execute to this value.
* `MaxConcurrentPlans`:
  * Limit how many acquisition plans are being executed concurrently.

Enforce these before selecting intents from the queue.

---

### 3. Origin-Based Selection

Use the `IntentOrigin` field from H-12:

* Add configuration:

```jsonc
"VirtualSoulfind": {
  "Resolver": {
    "OriginPriority": [ "UserLocal", "LocalAutomation", "RemoteMesh", "RemoteGateway" ],
    "MaxRemoteOriginShare": 0.2
  }
}
```

Semantics:

* `OriginPriority`:
  * Determines which origins get picked first.
* `MaxRemoteOriginShare`:
  * Fraction of `MaxTracksPerRun` that can be allocated to `RemoteMesh` + `RemoteGateway` combined (e.g., 0.2 = at most 20%).

Implementation:

* When selecting intents for a run:
  * Fill from higher-priority origins first (UserLocal, etc.).
  * For remote origins:
    * Never exceed `MaxRemoteOriginShare * MaxTracksPerRun` tracks.

---

### 4. Planner Mode Enforcement

Ensure the planner:

* Checks the configured `PlanningMode` for VirtualSoulfind:
  * `OfflinePlanning`: no backends that require network I/O.
  * `MeshOnly`: backends limited to MeshDHT/Torrent/HTTP/LAN; Soulseek backend must not appear in plans.
  * `SoulseekFriendly`:
    * All Soulseek steps must be limited by:
      * Soulseek caps (H-08).
      * Work budget (H-02).
    * Planner should *prefer* non-Soulseek backends when feasible.

Implement mode-awareness:

* Before finalizing a `TrackAcquisitionPlan`, the planner should filter or reorder steps to comply with the current mode.

---

### 5. Plan Validation vs Budgets/Caps

Before executing a plan:

* Run a **plan validation** step that:
  * Estimates total work units for the plan:
    * Sum costs of all steps using H-02's cost model.
  * Checks:
    * Against per-call work budget.
    * Against per-peer budget (if you track them at this layer).
    * Against Soulseek per-minute caps (rough approximations).

* If validation fails:
  * Either:
    * Downgrade plan (drop lower-priority backends).
    * Or mark intent as `OnHold/Failed` with reason "Budget exceeded / caps exceeded".

This prevents executing obviously impossible/abusive plans.

---

### 6. Testing (H-14)

Add tests:

1. **Global caps:**
   * Configure small values for `MaxTracksPerRun` and `MaxConcurrentPlans`.
   * Ensure resolver:
     * Does not pick more than allowed.
     * Does not run too many in parallel.

2. **Origin priority:**
   * Create intents with different origins.
   * Ensure resolver picks `UserLocal` first, and limits remote to the configured share.

3. **Mode enforcement:**
   * In `MeshOnly` mode:
     * Plans must contain no Soulseek backend steps.
   * In `OfflinePlanning` mode:
     * No backend steps that require network.

4. **Plan validation:**
   * Create a plan that would exceed work budgets or Soulseek caps.
   * Ensure validator rejects/adjusts it and resolver does not execute it as-is.

---

### 7. Anti-Slop Checklist for H-14

* Resolver can no longer be tricked into processing unlimited tracks.
* Remote-origin intents cannot dominate resolver work.
* Planner respects modes and caps; no silent Soulseek overuse or budget-breaker plans.

---

## H-15 – VirtualSoulfind Service & Gateway Exposure + Observability

**Corresponds to**: H-VS08 (Mesh Service Restrictions), H-VS10 (Gateway Endpoint Protection), H-VS09 (Logging Hygiene)  
**Priority**: P0 (Critical for V2-P5)  
**Status**: 📋 Planned  
**DEPENDS ON**: H-01 (Gateway Auth/CSRF)

### Repo: `https://github.com/snapetech/slskdn`
### Branch: `experimental/multi-source-swarm`

H-15 hardens **how VirtualSoulfind is exposed** via:

* Mesh service fabric.
* HTTP gateway.

And ensures observability does not become a privacy/security leak.

---

### 0. Scope & Non-Goals

You **must**:

1. Restrict VirtualSoulfind methods exposed over mesh to mostly read-only, low-cost operations, with quotas.
2. Restrict VirtualSoulfind methods exposed via HTTP (gateway) and tie them into gateway auth/CSRF and method allowlists.
3. Introduce safe, low-cardinality metrics and redacted logging for VirtualSoulfind.

You **must NOT**:

* Remove the ability to use VirtualSoulfind from local tooling.
* Turn VirtualSoulfind into a generic remote code runner.

---

### 1. Recon

Find:

1. The `IMeshService` implementation for VirtualSoulfind (if already present or stubbed).
2. HTTP gateway endpoints exposing VirtualSoulfind functionality (if any).
3. Existing metrics for VirtualSoulfind.

---

### 2. Mesh Service Exposure Policy

Define and implement a clear policy:

1. **Allowed mesh methods**:
   * Read-only metadata:
     * `GetVirtualRelease`
     * `ListMissingTracksForRelease`
     * `GetVirtualTrackMetadata`
   * Light introspection:
     * `GetCapabilities`
     * `GetCatalogueStats`

2. **Restricted mesh methods**:
   * Mutations:
     * `CreateIntent`, `ExecutePlan` should either:
       * Not be exposed over mesh at all; or
       * Be guarded behind:
         * A trust policy (e.g., only for whitelisted peers).
         * Very strict quotas and work budgets.

Implement:

* A method-level allowlist inside the `VirtualSoulfindMeshService`:
  * Reject calls to non-allowed operations with a clear error.
* Per-peer quotas for mesh calls:
  * Limit frequency and type of calls.

---

### 3. HTTP Gateway Exposure Policy

For HTTP:

1. Map VirtualSoulfind endpoints into the gateway config allowlist, e.g.:

```jsonc
"MeshGateway": {
  "AllowedServices": [ "virtual-soulfind", ... ],
  "AllowedRoutes": {
    "virtual-soulfind": {
      "GET": [ "/virtual/releases/*", "/virtual/releases/*/missing", "/virtual/intents/*/status" ],
      "POST": [ "/virtual/intents/*" ] // if allowed at all
    }
  }
}
```

2. Enforce this allowlist in the gateway controller:
   * Reject requests to VirtualSoulfind routes that are not allowed in config.

3. For mutating HTTP endpoints (intent creation, execute plan):
   * Require:
     * API key auth.
     * CSRF header (from H-01).
   * Consider disabling execution endpoints by default:
     * `VirtualSoulfind.Http.AllowPlanExecution = false`.

---

### 4. Observability: Metrics & Logs

Add VirtualSoulfind metrics:

* Safe, low-cardinality counters/timers, e.g.:
  * `virtualsoulfind_intents_created_total` (labels: `origin`)
  * `virtualsoulfind_plans_executed_total` (labels: `backend`, `result`)
  * `virtualsoulfind_match_attempts_total` (labels: `result`)

**Do NOT**:

* Include `ReleaseId` / `TrackId` as metric labels if they can be high-cardinality.
* Include usernames, paths, or IPs.

Logging:

* Apply redaction from H-11:
  * No raw paths, usernames.
* For service/gateway calls:
  * Log:
    * Method name.
    * Origin (mesh/HTTP).
    * Result status.
  * Do not log payload bodies.

---

### 5. Testing (H-15)

Add tests to verify:

1. **Mesh method allowlist:**
   * Calls to allowed VirtualSoulfind mesh methods succeed (with test fakes).
   * Calls to restricted methods (e.g., `ExecutePlan`) are rejected.

2. **Gateway route allowlist:**
   * Requests to allowed VirtualSoulfind HTTP routes pass through.
   * Requests to disallowed routes get rejected.

3. **Metrics:**
   * Ensure VirtualSoulfind operations increment metrics with expected labels.
   * Verify that no high-cardinality IDs are used as metric labels (unit tests can inspect registered metrics or a mock sink).

4. **Logging redaction:**
   * Use a test logger to ensure VirtualSoulfind logs do not contain file paths or usernames.

---

### 6. Anti-Slop Checklist for H-15

* VirtualSoulfind mesh service is **read-heavy, write-light**, with clear method allowlisting.
* HTTP gateway exposes VirtualSoulfind only through an explicit, secure allowlist wired to auth/CSRF.
* Observability is useful but does not leak library contents or identifiers.

---

## Task Summary

| Task | Corresponds To | Priority | Phase | Depends On | Status |
|------|---------------|----------|-------|------------|--------|
| H-11 | H-VS01, H-VS09 | P0 | V2-P1 | - | 📋 Planned |
| H-12 | H-VS02, H-VS12 | P0 | V2-P2 | - | 📋 Planned |
| H-13 | H-VS03, H-VS04 | P0 | V2-P4 | H-02, H-08 | 📋 Planned |
| H-14 | H-VS05, H-VS06 | P0 | V2-P5 | H-02, H-08, H-12 | 📋 Planned |
| H-15 | H-VS08, H-VS10, H-VS09 | P0 | V2-P5 | H-01 | 📋 Planned |

---

## Implementation Order

1. **Complete prerequisites**:
   - H-02 (Work Budget) - BLOCKS H-13, H-14
   - H-08 (Soulseek Caps) - BLOCKS H-13, H-14

2. **Foundation (V2-P1)**:
   - H-11 (Identity Separation & Privacy Mode)

3. **Intent & Planning (V2-P2)**:
   - H-12 (Catalogue & Intent Queue Hardening)

4. **Backend Implementations (V2-P4)**:
   - H-13 (Backend Adapter Safety)

5. **Integration (V2-P5)**:
   - H-14 (Planner & Resolver Safety)
   - H-15 (Service & Gateway Exposure + Observability)

---

## Notes

These tasks are the implementation-ready versions of the design doc's H-VS tasks. They provide detailed guidance in the same format as T-SF and earlier H-series tasks, ready to be fed directly to Cursor or other LLM agents for implementation.

All tasks maintain the "paranoid bastard" security philosophy established in the earlier hardening work.
