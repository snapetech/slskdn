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
| **VirtualSoulfind / ModerationIntegration** | 6 | 0 | 17 | DisasterModeIntegration, LoadTests, NicotinePlus, ModerationIntegration; many `[Fact(Skip)]` |
| **BackfillIntegrationTests** | 3 | 0 | 0 | |
| **DhtRendezvousIntegrationTests** | 3 | 0 | 0 | |
| **Features** (RescueMode, CanonicalSelection, LibraryHealth) | 4 | 0 | 2 | RescueMode.Slow_Transfer, CanonicalSelection.Should_Prefer_Canonical skipped |
| **DisasterModeTests** | — | — | — | **Timeout** (run hangs) |
| **Soulbeet** | 16 | 0 | 1 | SoulbeetCompatibility.GetInfo_ShouldReturnSlskdnInfo skipped |
| **MultiClient \| MultiSource** | 9 | 0 | 0 | ~21s |
| **ProtocolContractTests** | — | — | — | **Timeout** (run hangs) |
| **CoverTrafficGeneratorIntegrationTests** | 3 | 0 | 0 | |
| **Signals** (SwarmRequestBtFallback) | 0 | 0 | 2 | both skipped |
| **PortForwardingIntegrationTests** | 3 | 0 | 0 | |
| **PerformanceBenchmarks** | — | — | — | No match in Integration (may be in other project or excluded) |

---

## Actions

1. **Mesh:** ~~Fix or skip `NatTraversal_SymmetricFallback`~~ **DONE.** Relay URL must be IP (TryParseRelay does not resolve hostnames); test now uses `relay://127.0.0.1:6000`. Mesh 29 pass.
2. **Timeouts (granular 2026-01-25):** **DisasterModeTests** and **ProtocolContractTests** hang (run with higher timeout or debug). Backfill 3, DhtRendezvous 3, Features 4 pass/2 skip, Soulbeet 16/1 skip, MultiClient|MultiSource 9, CoverTraffic 3, PortForwarding 3, Signals 2 skip — all complete when run in smaller filters.
3. **VirtualSoulfind skips:** Review 17 skipped tests; re-enable or document.

---

## Source

- `docs/dev/40-fixes-plan.md` Deferred table — slskd.Tests.Integration row.
- `memory-bank/progress.md` — 2026-01-25 Integration audit entry.
