# slskd.Tests.Integration — Runtime Audit

**Date:** 2026-01-25  
**Build:** `dotnet build tests/slskd.Tests.Integration/slskd.Tests.Integration.csproj -c Release` — **0 errors.**

Run filtered subsets to avoid full-suite timeout, e.g.:
`dotnet test tests/slskd.Tests.Integration/... -c Release --no-build --filter "FullyQualifiedName~MediaCore"`

---

## Filtered subset results (2026-01-25)

| Filter | Pass | Fail | Skip | Notes |
|--------|------|------|------|-------|
| **MediaCore** | 22 | 0 | 0 | CrossCodecMatching, MediaCoreIntegration, MediaCorePerformance |
| **Mesh** | 29 | 0 | 0 | **FIXED:** `NatTraversal_SymmetricFallback` — relay URL was `relay://relay.example.com:6000`; `TryParseRelay` only accepts IPs. Switched to `relay://127.0.0.1:6000`. |
| **PodCore** | 15 | 0 | 0 | PodCoreIntegration, PortForwarding |
| **Security** (folder) | 50 | 0 | 0 | Tor, Obfuscated, Censorship, Http, MeshGateway, SecurityMiddleware |
| **SecurityIntegrationTests** | 12 | 0 | 0 | |
| **VirtualSoulfind / ModerationIntegration** | 12 | 0 | 11 | **NicotinePlusIntegrationTests:** 6 pass — real Bridge API integration tests (StubWebApplicationFactory, StubBridgeApi, TestSoulfindBridgeService). DisasterModeIntegration 6, LoadTests 5 skip. ModerationIntegration. |
| **BackfillIntegrationTests** | 3 | 0 | 0 | |
| **DhtRendezvousIntegrationTests** | 3 | 0 | 0 | |
| **Features** (RescueMode, CanonicalSelection, LibraryHealth) | 4 | 0 | 2 | RescueMode.Slow_Transfer, CanonicalSelection.Should_Prefer_Canonical skipped |
| **DisasterModeTests** | 2 | 0 | 1 | **FIXED:** IncludeOnlyControllersFeatureProvider in SlskdnTestClient; 2 pass, 1 skip (Kill_Soulfind_Mid_Transfer: "Stub host"). MeshOnlyTests: 3 pass. |
| **Soulbeet** | 16 | 0 | 1 | SoulbeetCompatibility.GetInfo_ShouldReturnSlskdnInfo skipped |
| **MultiClient \| MultiSource** | 9 | 0 | 0 | ~21s |
| **ProtocolContractTests** | 6 | 0 | 0 | **FIXED:** SlskdnTestClient now only loads 9 controllers; 6 pass. |
| **CoverTrafficGeneratorIntegrationTests** | 3 | 0 | 0 | |
| **Signals** (SwarmRequestBtFallback) | 0 | 0 | 2 | both skipped |
| **PortForwardingIntegrationTests** | 3 | 0 | 0 | |
| **PerformanceBenchmarks** | — | — | — | No match in Integration (may be in other project or excluded) |

---

## Actions

1. **Mesh:** ~~Fix or skip `NatTraversal_SymmetricFallback`~~ **DONE.** Relay URL must be IP (TryParseRelay does not resolve hostnames); test now uses `relay://127.0.0.1:6000`. Mesh 29 pass.
2. **Timeouts (granular 2026-01-25):** ~~**DisasterModeTests** (3) and **ProtocolContractTests** (6) **skipped**~~ **DONE.** SlskdnTestClient uses IncludeOnlyControllersFeatureProvider so only 9 controllers are loaded; DisasterModeTests 2 pass / 1 skip (Stub host), ProtocolContractTests 6 pass. Backfill, DhtRendezvous, Features, Soulbeet, MultiClient|MultiSource, CoverTraffic, PortForwarding, Signals complete in smaller filters.
3. **VirtualSoulfind skips (11):** DisasterModeIntegrationTests 6 (TODO: Soulseek sim, DHT, etc.); LoadTests 5 (run manually). **NicotinePlusIntegrationTests 6** — real Bridge API integration tests (search, download, rooms, status, start/stop, concurrency); no Nicotine+ or soulfind binary required.

---

## VirtualSoulfind skip breakdown (11)

| File | Count | Reason |
|------|-------|--------|
| **DisasterModeIntegrationTests** | 6 | TODO: Soulseek sim, DHT, shadow index, audit infra, health monitor, telemetry |
| **LoadTests** | 5 | Load test — run manually |
| **NicotinePlusIntegrationTests** | 0 | 6 pass — real Bridge API integration tests (StubBridgeApi, TestSoulfindBridgeService) |

---

## Source

- `docs/dev/40-fixes-plan.md` Deferred table — slskd.Tests.Integration row.
- `memory-bank/progress.md` — 2026-01-25 Integration audit entry.
