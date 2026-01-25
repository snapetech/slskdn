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
| **Mesh** | 28 | 1 | 0 | **Fail:** `MeshIntegrationTests.NatTraversal_SymmetricFallback` (Assert.True False @ line 305) |
| **PodCore** | 15 | 0 | 0 | PodCoreIntegration, PortForwarding |
| **Security** (folder) | 50 | 0 | 0 | Tor, Obfuscated, Censorship, Http, MeshGateway, SecurityMiddleware |
| **SecurityIntegrationTests** | 12 | 0 | 0 | |
| **VirtualSoulfind / ModerationIntegration** | 6 | 0 | 17 | DisasterModeIntegration, LoadTests, NicotinePlus, ModerationIntegration; many `[Fact(Skip)]` |
| **DisasterModeTests** | — | — | — | **Timeout** (run hangs or very slow) |
| **Features \| Backfill \| DhtRendezvous** | — | — | — | **Timeout** when run together |
| **Soulbeet \| MultiClient \| MultiSource \| Protocol \| PortForwarding \| CoverTraffic \| Signals \| PerformanceBenchmarks** | — | — | — | **Timeout** when run together |

---

## Actions

1. **Mesh:** Fix or skip `NatTraversal_SymmetricFallback` (assert at `MeshIntegrationTests.cs:305`).
2. **Timeouts:** Run DisasterMode, Features, Backfill, DhtRendezvous, Soulbeet, MultiClient, MultiSource, Protocol, CoverTraffic, Signals, PerformanceBenchmarks in smaller filters or with increased timeout; fix or document any that hang.
3. **VirtualSoulfind skips:** Review 17 skipped tests; re-enable or document.

---

## Source

- `docs/dev/40-fixes-plan.md` Deferred table — slskd.Tests.Integration row.
- `memory-bank/progress.md` — 2026-01-25 Integration audit entry.
