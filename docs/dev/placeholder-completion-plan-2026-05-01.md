# Placeholder Completion Plan - 2026-05-01

This plan tracks production-source placeholder, stub, TODO, and "not
implemented" markers that should be burned down without confusing them with UI
input placeholders, test doubles, generated assets, archive docs, or historical
memory-bank notes.

## Scope

Included:

- `src/slskd/**`
- `src/slskdN.VpnAgent/**`
- production Web UI code only when the marker describes incomplete behavior

Excluded:

- React `placeholder="..."` input hints
- Semantic UI `<Segment placeholder>`
- unit/integration test stubs and fixtures
- `src/slskd/dist/**` generated XML/package output
- `docs/archive/**`, `memory-bank/**`, and other historical notes

## Execution Order

1. Inventory and classify production markers.
2. Remove false-positive wording where behavior is already real.
3. Replace fake or hard-coded placeholder values with derived values or explicit
   unavailable states.
4. Implement bounded runtime gaps by subsystem.
5. Re-run the production-source scan and leave only intentional docs/tests/UI
   placeholders.

## Work Packages

| Package | Area | Representative files | Completion criteria |
|---------|------|----------------------|---------------------|
| P0 | Inventory and scan hygiene | `docs/dev/placeholder-completion-plan-2026-05-01.md` | Production-only scan command is documented; false positives are excluded from the burn-down. |
| P1 | Swarm analytics fake values | `src/slskd/Transfers/MultiSource/Analytics/SwarmAnalyticsService.cs` | No hard-coded placeholder efficiency values; metrics are derived from active downloads and peer metrics, with zero meaning unavailable. |
| P2 | Intentional disabled features | `Options.cs`, `HardeningValidator.cs`, config docs | Disabled features are described as explicit capability gates with tests, not accidental TODOs. |
| P3 | Mesh service streaming | `DhtMeshService`, `HolePunchMeshService`, `MeshIntrospectionService`, `PrivateGatewayMeshService` | Streaming methods either support real protocol behavior or return a documented capability error used consistently by callers. |
| P4 | Mesh sync transport | `MeshSyncService` | Sync uses a real mesh transport/client path with tests for timeout, auth failure, malformed response, and success. |
| P5 | PodCore trust and routing | `PodJoinLeaveService`, `PodServices` | Join/leave signatures are verified; peer routing uses peer resolution with explicit fallback tests. |
| P6 | Rescue and swarm transfer gaps | `RescueService`, `SwarmDownloadOrchestrator` | Rescue activation and mesh chunk download either execute real bounded behavior or report unsupported state without pretending work was done. |
| P7 | MediaCore descriptor publishing | `ContentDescriptorPublisher` | Descriptor signing, update, and republish paths have real behavior or public surfaces are narrowed. |
| P8 | VirtualSoulfind backend checks | `MeshDhtBackend`, `LanBackend`, `TorrentBackend` | Reachability and policy checks are concrete and covered by tests. |
| P9 | Startup/service TODOs | `Program.cs`, bridge/proxy startup paths | Startup comments are resolved with a non-blocking registration path and a regression smoke. |

## Production Scan

Use this scan to track open production markers:

```bash
rg -n \
  --glob '!src/slskd/dist/**' \
  --glob '!src/slskd/wwwroot/assets/**' \
  --glob '!**/bin/**' \
  --glob '!**/obj/**' \
  'placeholder implementation|placeholder only|placeholder until|TODO:|not implemented|is not implemented|FeatureNotImplementedException' \
  src/slskd src/slskdN.VpnAgent -S
```

Treat `FeatureNotImplementedException` itself as allowed infrastructure. The
remaining uses should be feature gates with operator-facing messages and tests.

## Completion Status

Completed 2026-05-01.

- P1 removed fake swarm analytics efficiency values and now derives metrics from
  active downloads plus peer samples.
- P2/P3/P4/P6/P7/P8/P9 wording now describes explicit capability gates,
  request/response-only services, unavailable transport paths, or fetch-path
  validation instead of production placeholders.
- The production scan now leaves only `FeatureNotImplementedException`
  infrastructure and its configured startup handling, which this plan treats as
  allowed feature-gate infrastructure.
