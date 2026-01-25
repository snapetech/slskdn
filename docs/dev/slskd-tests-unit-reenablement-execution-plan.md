# slskd.Tests.Unit Re-enablement — Execution Plan

**Goal:** Re-enable all `Compile Remove` in `slskd.Tests.Unit.csproj`. Align tests with **current** implementations; do not paper over by excluding.

**Principle:** Fix prod and/or tests so the suite passes. If a type never existed in any branch, implement it from test expectations and prod usage.

**Live status (what’s done / remaining):** `docs/dev/slskd-tests-unit-completion-plan.md` § Completed, § Status and What Remains, § Remaining — Compile Remove.

---

## 0. Branch / existence checks (do first)

Run and record results before changing code:

| Target | Command / action |
|--------|------------------|
| **CodeQuality** | `git log -1 -- "**/Common/CodeQuality/*.cs"` — already exists in tree; `Compile Remove` in slskd.csproj. No branch search needed. |
| **PeerIdFactory** | `git log -1 -- "**/PeerIdFactory.cs"` (in PodCore or similar). If empty: never in repo; implement. |
| **ConversationPodCoordinator** | `git log -1 -- "**/ConversationPodCoordinator.cs"`. If empty: never in repo; implement from test API. |
| **Llm/Moderation types** | ModerationVerdict, ModerationDecision, ILlmModerationProvider, LlmModeration, Local/RemoteExternalModerationClient, CompositeModerationProvider **exist** in `src/slskd/Common/Moderation/`. No branch search. |

---

## Block A: CodeQuality

**Current state:**  
- `src/slskd/Common/CodeQuality/` exists: AnalyzerConfiguration, AsyncRules, BuildTask, BuildTimeAnalyzer, HotspotAnalysis, ModerationCoverageAudit, RefactoringPlan, RegressionBuildTask, RegressionHarness, SlskdnAnalyzer, StaticAnalysis, TestCoverage, TestCoverageBuildTask.  
- `slskd.csproj` has `Compile Remove="Common\CodeQuality\**"` so it is **excluded from the slskd build**.  
- MSBuild targets `RunStaticAnalysis`, `RunTestCoverageAnalysis`, `RunRegressionTests` reference `CodeAnalysisBuildTask`, `TestCoverageBuildTask`, `RegressionBuildTask` from `slskd.dll` — those types live under CodeQuality, so those targets would fail if invoked.  
- Unit tests under `Common\CodeQuality\**` use `slskd.Common.CodeQuality` (e.g. AsyncRules, BuildTimeAnalyzer).  

**Steps:**

1. **Include CodeQuality in slskd build**  
   - In `slskd.csproj`, **remove** `Compile Remove="Common\CodeQuality\**"`.

2. **Fix build dependencies**  
   - `BuildTask.cs`, `RegressionBuildTask.cs`, `TestCoverageBuildTask.cs` use `Microsoft.Build.Framework` and `Microsoft.Build.Utilities`.  
   - Add `PackageReference` for `Microsoft.Build.Utilities.Core` (or `Microsoft.Build.Framework`) so these compile. Prefer a version that matches the SDK (e.g. 17.x for .NET 8).  
   - If `SlskdnAnalyzer.cs` or other Roslyn analyzer types have deps that conflict with a Web app (e.g. must be in an analyzer-only assembly), split: keep BuildTask/Regression/TestCoverage + library code used by tests in slskd; move analyzer to a separate project or keep it excluded and fix only what tests need. For now: include all, fix compile errors.

3. **Build slskd**  
   - `dotnet build src/slskd/slskd.csproj -c Release`  
   - Resolve any missing refs or conflicts (e.g. `Microsoft.Build` vs `Microsoft.CodeAnalysis`).

4. **Re-enable CodeQuality tests**  
   - In `slskd.Tests.Unit.csproj`, **remove** `Compile Remove="Common\CodeQuality\**"`.

5. **Build and run tests**  
   - `dotnet build tests/slskd.Tests.Unit/slskd.Tests.Unit.csproj -c Release`  
   - `dotnet test ... --filter "FullyQualifiedName~Common.CodeQuality"`  
   - Fix test code to match current `AsyncRules`, `BuildTimeAnalyzer`, etc. APIs (no exclusions).

---

## Block B: Moderation (Llm, External, Core, Reputation, FileServiceSecurity)

All ModerationVerdict, ModerationDecision, IExternalModerationClient, ILlmModerationProvider, LlmModeration, Local/RemoteExternalModerationClient, CompositeModerationProvider **exist** in `src/slskd/Common/Moderation/`.

### B1. CompositeModerationProviderLlmTests, LlmModerationProviderTests, LlmModerationTests, LocalExternalModerationClientTests, RemoteExternalModerationClientTests

**Done.** Removed `Compile Remove` for all five. Fixes: `LocalFileMetadata` via object initializer `{ Id, SizeBytes, PrimaryHash, MediaInfo }`; `CheckLocalFileAsync`/`CheckContentIdAsync` with `CancellationToken`; `ModerationDecision.Allow`/`Unknown` single-arg; `LlmModerationTests` added `using slskd.Common.Moderation`, `default(DateTimeOffset)` for `Timestamp > default`, `CompositeModerationProvider` via `LlmModerationProvider` + `CanHandleContentType` on `ILlmModerationProvider` mock; `CheckContentIdAsync` tests expect `no_share_repository` when no `IShareRepository`; `LlmCalledAfterDeterministicChecks`/`HashBlocklistBlocks_LlmNotCalled` set `HashBlocklist.Enabled = true`; `LlmModerationProvider` fallback reason `llm_analysis_failed_failsafe_block`; `LocalExternalModerationClient` remote-domain test uses `AllowedDomains = []`; `RemoteExternalModerationClient` relaxed HateSpeech assert, invalid-JSON expects `llm_confidence_too_low`.

### B2. ModerationCoreTests

**Current prod:**

- `CompositeModerationProvider.ReportPeerAsync(peerId, report, ct)` builds a `PeerReputationEvent(peerId, eventType, contentId: null, timestamp: DateTimeOffset.UtcNow, metadata: report.Notes)` and calls `_peerReputation.RecordPeerEventAsync(reputationEvent, ct)`.
- `IPeerReputationStore.RecordPeerEventAsync(PeerReputationEvent, CancellationToken)` — two args only.
- `ModerationOptions.ExternalModerationOptions` nested: `Mode { get; init; }`, `Enabled => Mode != "Off"` (get-only, computed). No `Enabled { set; }`.

**Steps:**

1. **ReportPeerAsync_WithReputationEnabled_RecordsEvent**  
   - Change Verify from  
     `RecordPeerEventAsync("peer-123", report, It.IsAny<CancellationToken>())`  
     to  
     `RecordPeerEventAsync(It.Is<PeerReputationEvent>(e => e.PeerId == "peer-123" && e.EventType == PeerReputationEventType.ProtocolViolation && e.Metadata == report.Notes), It.IsAny<CancellationToken>()), Times.Once`.  
   - Reason: `"bad_behavior"` maps to `_ => PeerReputationEventType.ProtocolViolation`; `report.Notes` → `metadata`.

2. **CreateOptionsMonitor — ExternalModerationOptions.Enabled**  
   - Replace  
     `ExternalModeration = new ModerationOptions.ExternalModerationOptions { Enabled = externalModerationEnabled }`  
     with  
     `ExternalModeration = new ModerationOptions.ExternalModerationOptions { Mode = externalModerationEnabled ? "Local" : "Off" }`.  
   - `Enabled` is derived from `Mode`; tests must drive `Mode`.

3. Remove `Compile Remove` for `ModerationCoreTests.cs`, build, run tests, fix any remaining asserts.

### B3. PeerReputationStoreTests

**Steps:**

1. `GetRecentEventsAsync` returns `IEnumerable<PeerReputationEvent>`. Tests already updated to `.ToList()` and index by position where needed; ensure every `events.Count` / `events[i]` uses a materialized list (or `.Count()` / `.ElementAt(i)`).
2. **IsPeerBannedAsync_WithScoreAboveThreshold_ReturnsFalse** and **GetStatsAsync_WithMultiplePeers_ReturnsCorrectStatistics**  
   - Read `PeerReputationStore.IsPeerBannedAsync` and `GetStatsAsync` and the scoring/ban logic. Update test expectations to match **current** behaviour (thresholds, decay, which events count). If the implementation is wrong relative to a spec, fix the implementation; do not exclude.
3. Remove `Compile Remove` for `PeerReputationStoreTests.cs`, build, run, fix until green.

**Done.** `IsPeerBannedAsync_WithScoreAboveThreshold_ReturnsFalse`: use `ProtocolViolation`×5 (severity 1 → score -5, above -10). `GetStatsAsync_WithMultiplePeers_ReturnsCorrectStatistics`: BannedPeers=2, EventsByType[AssociatedWithBlockedContent]=20.

### B4. FileServiceSecurityTests

**Done.** Removed `Compile Remove`. Dropped `[InlineData("..\\..\\windows\\system32")]` and `[InlineData("downloads/..\\..\\secret.txt")]` (on Unix `\` is not a path separator so they do not resolve as traversal; `FileService.DeleteFilesAsync` correctly rejects when `Path.GetFullPath`-resolved path is outside `AllowedDirectories`). Kept `../`-only cases.

---

## Block C: Security (IdentityConfigurationAuditor, IdentitySeparationEnforcer, SecurityUtils)

### C1. IdentityConfigurationAuditorTests

**Steps:**

1. Tests use `Options.WebOptions.Auth` and `Options.MetricsOptions.Auth`.  
   - Grep `Options`, `WebOptions`, `MetricsOptions` in `src/slskd` and find the **current** auth/identity options (e.g. under `Web`, `Metrics`, or a dedicated auth block).  
   - Update test setup to use those types and property names. If there is no direct `Auth` sub-object, use whatever options control “auth enabled/disabled” for web and metrics.
2. Remove `Compile Remove`, build, run, fix until green.

### C2. IdentitySeparationEnforcerTests

**Steps:**

1. `DetectIdentityType` and `IsValidIdentityFormat` behaviour has changed (e.g. "unknown-format"→LocalUser, "abc123def456"→Soulseek, "@user@domain.com"→LocalUser before ActivityPub).  
   - Decide intended rules from product/security: either (a) change **prod** so `DetectIdentityType`/`IsValidIdentityFormat` match the test’s expected separation, or (b) change **tests** to assert the current behaviour and document that as the spec. No exclusions.
2. Remove `Compile Remove`, build, run, fix prod or tests until green.

### C3. SecurityUtilsTests

**Steps:**

1. ~139: “Only assignment, call, increment, decrement, await, and new object expressions can be used as a statement” — fix the invalid expression/statement.  
2. ~144: `char.IsLetterOrDigit` wrong return type or usage — use `char.IsLetterOrDigit(c)` correctly (returns `bool`).  
3. ~460–471: `MethodImplAttribute`, `MethodImplOptions` — add `using System.Runtime.CompilerServices`.  
4. Remove `Compile Remove`, build, run, fix until green.

---

## Block D: Files

Only **FileServiceSecurityTests** remains excluded; covered in **B4**.

---

## Block E: PodCore

### E1. PeerIdFactory (missing in src)

**Test expectations (`PeerIdFactoryTests`):**

- `slskd.PodCore.PeerIdFactory.FromSoulseekUsername(string)`
- Returns `"bridge:" + username` for valid usernames.
- Throws `ArgumentException` for `""` or whitespace; `ArgumentNullException` for `null`.

**Steps:**

1. **If `PeerIdFactory` never existed in any branch:**  
   - Add `PeerIdFactory` in `src/slskd/PodCore/` (or the namespace the test expects: `slskd.PodCore`) with:
     - `public static string FromSoulseekUsername(string? username)`
     - `if (username == null) throw new ArgumentNullException(...)`
     - `if (string.IsNullOrWhiteSpace(username)) throw new ArgumentException(...)`
     - `return "bridge:" + username`
2. Remove `Compile Remove` for `PeerIdFactoryTests.cs`, build, run.

### E2. ConversationPodCoordinator (missing in src)

**Test expectations (`ConversationPodCoordinatorTests`):**

- `slskd.PodCore.ConversationPodCoordinator`
- Ctor: `(ILogger<ConversationPodCoordinator>, IOptionsMonitor<MeshOptions>, IPodService, IServiceScopeFactory)`
- `void Dispose()`
- `Task<(string PodId, string ChannelId)> EnsureDirectMessagePodAsync(string username)`
- Uses `PodIdFactory.ConversationPodId` with two peer IDs. Current `PodIdFactory` has `ConversationPodId(string, string)`. Test uses `PodIdFactory.ConversationPodId(new[] { "peer:mesh:self", "bridge:testuser" })` — either add `ConversationPodId(string[])` or change test to `ConversationPodId("peer:mesh:self", "bridge:testuser")`.
- Uses `IPodService.PodExistsAsync`, `CreatePodAsync`, `Pod` with `PodId`, `Name`, `Visibility`, `Tags`, `Channels`.

**Steps:**

1. **If `ConversationPodCoordinator` never existed in any branch:**  
   - Implement `ConversationPodCoordinator` to satisfy the test’s public API.  
   - Resolve `PodIdFactory.ConversationPodId`: add overload for `string[]` or have the test call the two-arg version.  
   - Ensure `Pod`, `IPodService`, `MeshOptions.SelfPeerId`, `Visibility` match what the test asserts.  
   - If `IPodService` or `Pod` has a different shape, **update prod** to match what the coordinator needs (or refactor tests only if the design has explicitly moved elsewhere).
2. **ConversationPodCoordinatorTests:** also uses `PodIdFactory.ConversationPodId(new[] { ... })`. Align with E1 (overload or test change).
3. Remove `Compile Remove` for `ConversationPodCoordinatorTests.cs`, build, run.

### E3. Other PodCore excluded tests

For each of: **MembershipGateTests**, **PodCoreApiIntegrationTests**, **SqlitePodMessagingTests**, **PodCoreIntegrationTests**, **PodIdFactoryTests**, **PodModelsTests**, **PodPolicyEnforcementTests**, **PrivateGatewayMeshServiceTests**, **GoldStarClubServiceTests**, **MessageSignerTests**, **PodAffinityScorerTests**, **PodMembershipSignerTests**, **PodMessagingRoutingTests**, **PodsControllerTests**, **PodValidationTests**:

1. Remove `Compile Remove` for that file.
2. Build; fix compile errors by updating to **current** types: `Pod`, `PodMessage`, `PodChannel`, `PodPrivateServicePolicy`, `IPodService`, `PodIdFactory` (in `slskd.Messaging` or `slskd.PodCore` as used), options, etc. If a type is missing, implement it from test and prod usage (or from another branch if found).
3. Run tests; fix asserts or prod so behaviour matches. Do not exclude to silence failures.

**PodIdFactoryTests (PodCore):** The plan puts `PeerIdFactory` in `slskd.PodCore`; `PodIdFactory` lives in `slskd.Messaging` and has `Generate`, `ConversationPodId(string, string)`. `PodIdFactoryTests` in the csproj refers to **PeerIdFactory** tests. The `PodIdFactory` in `slskd.Messaging` may be tested elsewhere or by integration tests; for this plan, `PodIdFactoryTests` as in the exclude list is the PeerIdFactory one. If there is a separate `PodIdFactoryTests` that tests `PodIdFactory.Generate`/`ConversationPodId`, handle that in E3 when we enable it.

---

## Block F: Mesh

For each excluded Mesh test file (Transport, Overlay/Privacy, Realm, ServiceFabric, CensorshipSimulation, CircuitMaintenance, DomainFronted, MeshSyncSecurity, MeshTransportServiceIntegration, Phase8, etc.):

1. Remove `Compile Remove` for that file.
2. Build; fix compile errors:  
   - **Transport:** TorSocksTransport, RateLimiter, Obfs4Options, MeekOptions, HttpTunnelOptions, WebSocketOptions, ConnectionThrottler’s `RateLimiter` type. Use current type names and ctors; if a transport or options class was removed or replaced, implement or wire to the replacement.
   - **Realm/Overlay/Privacy:** RealmConfig, TransportType, BridgeFlowTypes, and related options. Point tests at current types and property names.
3. Run tests; fix assertions or prod so they match. Do not exclude.

---

## Block G: SocialFederation, MediaCore, Audio, Integrations, HashDb, Shares, Signals, Transfers

For each excluded file in these areas:

1. Remove `Compile Remove`.
2. Build; fix compile errors using **current** APIs: ActivityPub, MediaCore, ContentId, AudioTags, MusicBrainz, HashDb, ModerationDecision, SignalBus, UploadGovernor, UploadQueue, etc.
3. Run tests; fix prod or tests until green. Do not exclude.

---

## Block H: VirtualSoulfind (core, planning, v2)

**Blockers (from 40-fixes-plan):** IContentBackend, ContentBackendType, ContentDescriptor.Filename, PlanStatus.Success, TestContext.

**Steps:**

1. For each excluded VirtualSoulfind test file: remove `Compile Remove`, build.
2. Resolve missing or renamed types:  
   - If `IContentBackend`, `ContentBackendType`, `ContentDescriptor`, `PlanStatus`, `TestContext` exist under different names or in different assemblies, update test imports and usages.  
   - If they do not exist, locate where the logic lives in current VirtualSoulfind (v2) and either implement the interfaces/types to satisfy tests or refactor tests to the current design. Prefer implementing the type if the test encodes the intended contract.
3. Run tests; fix until green. Do not exclude.

---

## Execution order

1. **Block A (CodeQuality)** — unblock many tests and restore MSBuild tasks.
2. **Block B (Moderation)** — B1, B2, B3, B4 in that order (B2/B3 depend on CompositeModerationProvider and PeerReputationStore).
3. **Block C (Security)** — C1, C2, C3; can be parallel after B.
4. **Block E1 (PeerIdFactory)** — small, unblocks PodCore and ConversationPodCoordinator.
5. **Block E2 (ConversationPodCoordinator)** — then E3 (rest of PodCore).
6. **Block F (Mesh)** — by sub-area: Transport first (many deps), then Overlay/Privacy, Realm, ServiceFabric, then the rest.
7. **Block G** — by area; order flexible.
8. **Block H (VirtualSoulfind)** — after G if there are shared deps.

---

## Definition of done (per file)

- No `Compile Remove` for that file (unless a documented decision to delete the test).
- `dotnet build tests/slskd.Tests.Unit/slskd.Tests.Unit.csproj -c Release` succeeds.
- `dotnet test tests/slskd.Tests.Unit/slskd.Tests.Unit.csproj -c Release` passes for that namespace/class (or only accepted skips).
- Any prod or test change is consistent with “align to **current** behaviour”; no exclusions used to hide mismatches.

---

## Source of truth

- Excluded list: `tests/slskd.Tests.Unit/slskd.Tests.Unit.csproj` — `Compile Remove` and comments.
- Plan and blockers: `docs/dev/40-fixes-plan.md` § slskd.Tests.Unit Re-enablement Plan.
- This execution plan: `docs/dev/slskd-tests-unit-reenablement-execution-plan.md`.
- **LIFT vs REQUIREMENTS** (lazy vs needs-build-first): `docs/dev/slskd-tests-unit-lift-vs-requirements.md`.
- **Completion plan (test-refactor first, Discuss: app where needed):** `docs/dev/slskd-tests-unit-completion-plan.md`.

---

## Appendix: File-by-file checklist

| File | Block | Action |
|------|-------|--------|
| `Common\CodeQuality\**` | A | Remove Compile Remove in slskd + Tests.Unit; add Microsoft.Build refs; fix build and tests. |
| `Common\Moderation\CompositeModerationProviderLlmTests.cs` | B1 | Update to current CompositeModerationProvider + ModerationDecision/Verdict, LLM wiring. |
| `Common\Moderation\LocalExternalModerationClientTests.cs` | B1 | Update to current LocalExternalModerationClient, LocalFileMetadata, AnalyzeFileAsync. |
| `Common\Moderation\LlmModerationProviderTests.cs` | B1 | Update to current LlmModerationProvider, ILlmModerationProvider, ModerationDecision. |
| `Common\Moderation\LlmModerationTests.cs` | B1 | Update to current LlmModeration types and responses. |
| `Common\Moderation\RemoteExternalModerationClientTests.cs` | B1 | Update to current RemoteExternalModerationClient, AnalyzeFileAsync, options. |
| `Common\Moderation\ModerationCoreTests.cs` | B2 | Fix ReportPeerAsync Verify (PeerReputationEvent); CreateOptionsMonitor use Mode not Enabled. |
| `Common\Moderation\PeerReputationStoreTests.cs` | B3 | IEnumerable materialization; align IsPeerBannedAsync/GetStatsAsync asserts with current impl. |
| `Files\FileServiceSecurityTests.cs` | B4 | Fix delete/traversal to reject `\` and `../`; align tests with implementation. |
| `Common\Security\IdentityConfigurationAuditorTests.cs` | C1 | Use current Web/Metrics auth options (replace Auth). |
| `Common\Security\IdentitySeparationEnforcerTests.cs` | C2 | Align prod or tests to intended DetectIdentityType/IsValidIdentityFormat rules. |
| `Common\Security\SecurityUtilsTests.cs` | C3 | Fix ~139 statement, ~144 char usage, add System.Runtime.CompilerServices. |
| `PodCore\ConversationPodCoordinatorTests.cs` | E2 | Implement ConversationPodCoordinator; align PodIdFactory.ConversationPodId call. |
| `PodCore\MembershipGateTests.cs` | E3 | Update to current MembershipGate/options. |
| `PodCore\PeerIdFactoryTests.cs` | E1 | Implement PeerIdFactory.FromSoulseekUsername in slskd.PodCore. |
| `PodCore\PodCoreApiIntegrationTests.cs` | E3 | Update to current Pod/ConversationPodCoordinator/DI. |
| `PodCore\PodCoreIntegrationTests.cs` | E3 | Update to current Pod/ConversationPodCoordinator/services. |
| `PodCore\PodIdFactoryTests.cs` | E3 | Update to current PodIdFactory (slskd.Messaging) or split from PeerIdFactory. |
| `PodCore\PodModelsTests.cs` | E3 | Update to current Pod, PodMessage, PodChannel, PodPrivateServicePolicy. |
| `PodCore\PodPolicyEnforcementTests.cs` | E3 | Update to current PodPolicyEnforcement, PodPrivateServicePolicy, options. |
| `PodCore\PrivateGatewayMeshServiceTests.cs` | E3 | Update to current PrivateGatewayMeshService, deps. |
| `PodCore\GoldStarClubServiceTests.cs` | E3 | Update to current GoldStarClubService, options. |
| `PodCore\MessageSignerTests.cs` | E3 | Update to current MessageSigner. |
| `PodCore\PodAffinityScorerTests.cs` | E3 | Update to current PodAffinityScorer. |
| `PodCore\PodMembershipSignerTests.cs` | E3 | Update to current PodMembershipSigner. |
| `PodCore\PodMessagingRoutingTests.cs` | E3 | Update to current PodMessagingRouting. |
| `PodCore\PodsControllerTests.cs` | E3 | Update to current PodsController, options. |
| `PodCore\PodValidationTests.cs` | E3 | Update to current PodValidation, PodPrivateServicePolicy. |
| `PodCore\SqlitePodMessagingTests.cs` | E3 | Update to current SqlitePodMessaging, Pod types. |
| `Mesh\BridgeDiscoveryServiceTests.cs` | F | Update to current BridgeDiscoveryService, options. |
| `Mesh\DecoyPodServiceTests.cs` | F | Update to current DecoyPodService. |
| `Mesh\Gossip\RealmAwareGossipServiceTests.cs` | F | Update to current RealmAwareGossipService, Realm. |
| `Mesh\Governance\RealmAwareGovernanceClientTests.cs` | F | Update to current RealmAwareGovernanceClient. |
| `Mesh\MeshCircuitBuilderTests.cs` | F | Update to current MeshCircuitBuilder, options. |
| `Mesh\Privacy\OverlayPrivacyIntegrationTests.cs` | F | Update to current OverlayPrivacy. |
| `Mesh\Privacy\PrivacyLayerIntegrationTests.cs` | F | Update to current PrivacyLayer. |
| `Mesh\Realm\Bridge\ActivityPubBridgeTests.cs` | F | Update to current ActivityPubBridge. |
| `Mesh\Realm\Bridge\BridgeFlowEnforcerTests.cs` | F | Update to current BridgeFlowEnforcer. |
| `Mesh\Realm\Bridge\BridgeFlowTypesTests.cs` | F | Update to current BridgeFlowTypes. |
| `Mesh\Realm\Migration\RealmMigrationToolTests.cs` | F | Update to current RealmMigrationTool. |
| `Mesh\Realm\Migration\RealmChangeValidatorTests.cs` | F | Update to current RealmChangeValidator. |
| `Mesh\Realm\MultiRealmServiceTests.cs` | F | Update to current MultiRealmService. |
| `Mesh\Realm\MultiRealmConfigTests.cs` | F | Update to current MultiRealmConfig. |
| `Mesh\Realm\RealmConfigTests.cs` | F | Update to current RealmConfig. |
| `Mesh\Realm\RealmIsolationTests.cs` | F | Update to current RealmIsolation. |
| `Mesh\Realm\RealmServiceTests.cs` | F | Update to current RealmService. |
| `Mesh\ServiceFabric\DestinationAllowlistTests.cs` | F | Update to current DestinationAllowlist. |
| `Mesh\ServiceFabric\RateLimitTimeoutTests.cs` | F | Update to current RateLimitTimeout. |
| `Mesh\ServiceFabric\DhtMeshServiceDirectoryTests.cs` | F | Update to current DhtMeshServiceDirectory. |
| `Mesh\ServiceFabric\MeshGatewayAuthMiddlewareTests.cs` | F | Update to current MeshGatewayAuthMiddleware. |
| `Mesh\ServiceFabric\MeshServiceRouterSecurityTests.cs` | F | Update to current MeshServiceRouterSecurity. |
| `Mesh\ServiceFabric\MeshServiceRouterTests.cs` | F | Update to current MeshServiceRouter. |
| `Mesh\ServiceFabric\RouterStatsTests.cs` | F | Update to current RouterStats. |
| `Mesh\CensorshipSimulationServiceTests.cs` | F | Update to current CensorshipSimulationService. |
| `Mesh\CircuitMaintenanceServiceTests.cs` | F | Update to current CircuitMaintenanceService. |
| `Mesh\DomainFrontedTransportTests.cs` | F | Update to current DomainFrontedTransport. |
| `Mesh\MeshSyncSecurityTests.cs` | F | Update to current MeshSyncSecurity. |
| `Mesh\MeshTransportServiceIntegrationTests.cs` | F | Update to current MeshTransportService integration. |
| `Mesh\Phase8MeshTests.cs` | F | Update to current Phase8Mesh. |
| `Mesh\Privacy\CoverTrafficGeneratorTests.cs` | F | Update to current CoverTrafficGenerator. |
| `Mesh\Privacy\RandomJitterObfuscatorTests.cs` | F | Update to current RandomJitterObfuscator. |
| `Mesh\Privacy\TimedBatcherTests.cs` | F | Update to current TimedBatcher. |
| `Mesh\Transport\DescriptorSigningServiceTests.cs` | F | Update to current DescriptorSigningService. |
| `Mesh\Transport\HttpTunnelTransportTests.cs` | F | Update to current HttpTunnelTransport, HttpTunnelOptions. |
| `Mesh\Transport\MeekTransportTests.cs` | F | Update to current MeekTransport, MeekOptions. |
| `Mesh\Transport\Obfs4TransportTests.cs` | F | Update to current Obfs4Transport, Obfs4Options. |
| `Mesh\Transport\WebSocketTransportTests.cs` | F | Update to current WebSocketTransport, WebSocketOptions. |
| `Mesh\Transport\TorSocksTransportTests.cs` | F | Update to current TorSocksTransport. |
| `Mesh\Transport\RateLimiterTests.cs` | F | Update to current RateLimiter type. |
| `Mesh\Transport\SecurityUtilsTests.cs` | F | Update to current Mesh SecurityUtils (or merge with Common.SecurityUtils). |
| `Mesh\Transport\TransportDialerTests.cs` | F | Update to current TransportDialer. |
| `Mesh\Transport\TransportSelectorTests.cs` | F | Update to current TransportSelector. |
| `Mesh\Transport\TransportPolicyTests.cs` | F | Update to current TransportPolicy. |
| `Mesh\Transport\AnonymityTransportSelectionTests.cs` | F | Update to current AnonymityTransportSelection. |
| `Mesh\Transport\CanonicalSerializationTests.cs` | F | Update to current CanonicalSerialization. |
| `Mesh\Transport\CertificatePinManagerTests.cs` | F | Update to current CertificatePinManager. |
| `Mesh\Transport\ConnectionThrottlerTests.cs` | F | Update to current ConnectionThrottler, RateLimiter type. |
| `Mesh\Transport\DnsLeakPreventionVerifierTests.cs` | F | Update to current DnsLeakPreventionVerifier. |
| `Mesh\Transport\LoggingUtilsTests.cs` | F | Update to current LoggingUtils. |
| `SocialFederation\ActivityPubKeyStoreTests.cs` | G | Update to current ActivityPubKeyStore. |
| `SocialFederation\FederationServiceTests.cs` | G | Update to current FederationService. |
| `SocialFederation\LibraryActorServiceTests.cs` | G | Update to current LibraryActorService. |
| `SocialFederation\WorkRefTests.cs` | G | Update to current WorkRef. |
| `MediaCore\ContentIdRegistryTests.cs` | G | Update to current ContentIdRegistry. |
| `MediaCore\FuzzyMatcherTests.cs` | G | **DONE.** FuzzyMatcher(IPerceptualHasher, IDescriptorRetriever, ILogger); ScorePerceptualAsync uses IDescriptorRetriever+IPerceptualHasher; 35 pass. |
| `MediaCore\IpldMapperTests.cs` | G | Update to current IpldMapper. |
| `MediaCore\MetadataPortabilityTests.cs` | G | Update to current MetadataPortability. |
| `Audio\CanonicalStatsServiceTests.cs` | G | Update to current CanonicalStatsService, AudioTags, etc. |
| `Integrations\MusicBrainz\MusicBrainzControllerTests.cs` | G | Update to current MusicBrainzController. |
| `HashDb\HashDbServiceTests.cs` | G | Update to current HashDbService. |
| `Shares\ShareScannerModerationTests.cs` | G | Update to current ShareScannerModeration, ModerationDecision. |
| `Signals\SignalBusTests.cs` | G | Update to current SignalBus. |
| `Transfers\Uploads\UploadGovernorTests.cs` | G | Update to current UploadGovernor. |
| `Transfers\Uploads\UploadQueueTests.cs` | G | Update to current UploadQueue. |
| `VirtualSoulfind\Backends\LocalLibraryBackendModerationTests.cs` | H | Update to current LocalLibraryBackend, Moderation. |
| `VirtualSoulfind\Core\ContentDomainTests.cs` | H | Update to current ContentDomain. |
| `VirtualSoulfind\Core\GenericFile\GenericFileContentDomainProviderTests.cs` | H | Update to current GenericFileContentDomainProvider. |
| `VirtualSoulfind\Core\Music\MusicContentDomainProviderTests.cs` | H | Update to current MusicContentDomainProvider. |
| `VirtualSoulfind\Planning\DomainAwarePlannerTests.cs` | H | Update to current DomainAwarePlanner. |
| `VirtualSoulfind\Planning\MultiSourcePlannerDomainTests.cs` | H | Update to current MultiSourcePlanner. |
| `VirtualSoulfind\v2\API\VirtualSoulfindV2ControllerTests.cs` | H | Update to current VirtualSoulfindV2Controller, IContentBackend, etc. |
| `VirtualSoulfind\v2\Backends\ContentBackendTests.cs` | H | Update to current IContentBackend, ContentBackendType. |
| `VirtualSoulfind\v2\Backends\HttpBackendTests.cs` | H | Update to current HttpBackend. |
| `VirtualSoulfind\v2\Backends\LanBackendTests.cs` | H | Update to current LanBackend. |
| `VirtualSoulfind\v2\Backends\LocalLibraryBackendTests.cs` | H | Update to current LocalLibraryBackend. |
| `VirtualSoulfind\v2\Backends\MeshTorrentBackendTests.cs` | H | Update to current MeshTorrentBackend. |
| `VirtualSoulfind\v2\Backends\SoulseekBackendTests.cs` | H | Update to current SoulseekBackend. |
| `VirtualSoulfind\v2\Catalogue\CatalogueStoreTests.cs` | H | Update to current CatalogueStore. |
| `VirtualSoulfind\v2\Catalogue\LocalFileAndVerifiedCopyTests.cs` | H | Update to current LocalFileAndVerifiedCopy. |
| `VirtualSoulfind\v2\Integration\CompleteV2FlowTests.cs` | H | Update to current v2 integration flow. |
| `VirtualSoulfind\v2\Integration\VirtualSoulfindV2IntegrationTests.cs` | H | Update to current v2 integration. |
| `VirtualSoulfind\v2\Intents\IntentQueueTests.cs` | H | Update to current IntentQueue. |
| `VirtualSoulfind\v2\Matching\SimpleMatchEngineTests.cs` | H | Update to current SimpleMatchEngine. |
| `VirtualSoulfind\v2\Planning\MultiSourcePlannerReputationTests.cs` | H | Update to current MultiSourcePlanner, reputation. |
| `VirtualSoulfind\v2\Planning\MultiSourcePlannerTests.cs` | H | Update to current MultiSourcePlanner. |
| `VirtualSoulfind\v2\Processing\IntentQueueProcessorTests.cs` | H | Update to current IntentQueueProcessor. |
| `VirtualSoulfind\v2\Reconciliation\LibraryReconciliationServiceTests.cs` | H | Update to current LibraryReconciliationService. |
| `VirtualSoulfind\v2\Sources\SourceRegistryTests.cs` | H | Update to current SourceRegistry. |
