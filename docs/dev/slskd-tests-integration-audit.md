# slskd.Tests.Integration — Runtime Audit

**Date:** 2026-01-25  
**Build:** `dotnet build tests/slskd.Tests.Integration/slskd.Tests.Integration.csproj -c Release` — **0 errors.**  
**Total:** 184 pass, 0 fail, 0 skip.  
**Other:** slskd.Tests 45 pass, 1 skip (Enforce subprocess); slskd.Tests.Unit 2257 pass. See 40-fixes-plan Deferred.

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
| **VirtualSoulfind / ModerationIntegration** | 23 | 0 | 0 | NicotinePlus 6, ModerationIntegration (LocalLibraryBackend assert, no skip). **DisasterModeIntegrationTests** 6 pass (StubWebApplicationFactory: disaster-mode/status, shadow-index). **LoadTests** 5 pass (HTTP smokes). **StubVirtualSoulfindServices** (StubDescriptorPublisher, StubPeerReputationStore, StubShareRepository). |
| **BackfillIntegrationTests** | 3 | 0 | 0 | |
| **DhtRendezvousIntegrationTests** | 3 | 0 | 0 | |
| **Features** (RescueMode, CanonicalSelection, LibraryHealth) | 6 | 0 | 0 | **FIXED:** RescueMode.Slow_Transfer (assert download 2xx), Should_Prefer_Canonical_Variant (GET /api/virtualsoulfind/canonical, assert FLAC). |
| **DisasterModeTests** | 3 | 0 | 0 | IncludeOnlyControllersFeatureProvider; Kill_Soulfind un-skipped (download + status smoke). MeshOnlyTests: 3 pass. |
| **Soulbeet** | 17 | 0 | 0 | **FIXED:** GetInfo_ShouldReturnSlskdnInfo — StubWebApplicationFactory now stubs ISoulseekClient for CompatibilityController (GET /api/info). |
| **MultiClient \| MultiSource** | 9 | 0 | 0 | ~21s |
| **ProtocolContractTests** | 6 | 0 | 0 | **FIXED:** SlskdnTestClient now only loads 9 controllers; 6 pass. |
| **CoverTrafficGeneratorIntegrationTests** | 3 | 0 | 0 | |
| **Signals** (SwarmRequestBtFallback) | 2 | 0 | 0 | Un-skipped: SendAsync and OnSignalReceivedAsync smoke. |
| **PortForwardingIntegrationTests** | 3 | 0 | 0 | |
| **PerformanceBenchmarks** | 1 | 0 | 0 | RunBenchmarks: DhtQueryLatency smoke (full BenchmarkDotNet run manually). |
| **LoadTests** | 5 | 0 | 0 | HTTP smokes (disaster-mode/status, shadow-index); full load manual/nightly. |

---

## Actions

1. **Mesh:** ~~Fix or skip `NatTraversal_SymmetricFallback`~~ **DONE.** Relay URL must be IP (TryParseRelay does not resolve hostnames); test now uses `relay://127.0.0.1:6000`. Mesh 29 pass.
2. **Timeouts:** ~~DisasterModeTests and ProtocolContractTests skipped~~ **DONE.** SlskdnTestClient IncludeOnlyControllersFeatureProvider; DisasterModeTests 3 pass, ProtocolContractTests 6 pass.
3. **VirtualSoulfind skips:** ~~11 (DisasterModeIntegration 6, LoadTests 5)~~ **DONE.** All un-skipped: DisasterModeIntegrationTests 6 (StubWebApplicationFactory smoke), LoadTests 5 (HTTP smokes), NicotinePlus 6 (Bridge API). **Zero [Fact(Skip)] in slskd.Tests.Integration.**

---

## VirtualSoulfind skip breakdown (0)

| File | Count | Reason |
|------|-------|--------|
| **DisasterModeIntegrationTests** | 0 | 6 pass — StubWebApplicationFactory smoke (disaster-mode/status, shadow-index) |
| **LoadTests** | 0 | 5 pass — HTTP smokes (disaster-mode/status, shadow-index) |
| **NicotinePlusIntegrationTests** | 0 | 6 pass — Bridge API (StubBridgeApi, TestSoulfindBridgeService) |

---

## Source

- `docs/dev/40-fixes-plan.md` Deferred table — slskd.Tests.Integration row.
- `memory-bank/progress.md` — 2026-01-25 Integration audit entry.
