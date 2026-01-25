# slskd.Tests.Unit — Future Work We Can Accomplish

**Updated:** 2026-01 (after PodIdFactory, PodModels, SignalBus, PodValidation, MessageSigner, GoldStarClubService re-enablement)

**Completed since last edit (see `docs/dev/slskd-tests-unit-completion-plan.md` § Completed):** PrivacyLayerIntegrationTests, ContentBackendTests, HttpBackendTests; MultiRealmService, BridgeFlowEnforcer, ActivityPubBridge; LanBackendTests, LocalLibraryBackendTests, MeshTorrentBackendTests (MeshDht+Torrent), SoulseekBackendTests; SourceRegistryTests; CatalogueStoreTests; IntentQueueTests.

---

## Summary

| Category | Count | Notes |
|----------|-------|-------|
| **Higher confidence (small API/type fixes)** | ~8 | PodAffinityScorer, PodPolicyEnforcement, PodMembershipSigner, PodMessagingRouting, PodsController, Transfers (UploadGovernor, UploadQueue), Shares |
| **Medium (prod or test rewrites)** | ~15 | PodCore (MembershipGate, integrations, PrivateGateway), Mesh Transport/Privacy/Realm/ServiceFabric (many), Block G (MediaCore, Audio, HashDb, etc.) |
| **Heavier / blocked** | ~50+ | CodeQuality (needs slskd build change), Mesh (Tor/RateLimiter/Options), VirtualSoulfind (IContentBackend, PlanStatus, etc.), Integration-style |

---

## 1. Higher confidence — do next

These use types that largely exist; fixes are mostly API alignment, mocks, and a few prod tweaks.

### PodCore

| File | What to do |
|------|------------|
| **PodAffinityScorerTests** | IPodService, IPodMessaging, PodAffinityScorer exist. `GetMessagesAsync(podId, channelId, since, ct)` matches. Ensure `CreateRecentMessages` sets `MessageId` on `PodMessage` if required; `ListAsync` exists. Remove `Compile Remove`, fix any threshold asserts. |
| **PodPolicyEnforcementTests** | `PodValidation` returns `(bool IsValid, string Error)` and does not throw. Rewrite tests to assert `!result.IsValid` and `result.Error` instead of `Assert.Throws<ArgumentException>`. Align `ValidatePrivateServicePolicy` / `ValidateCapabilities` the same way. Either: (a) add and use public `IsValidHostPattern`, `IsValidPort`, `IsPrivateAddress`, `IsBlockedAddress`, `IsKnownProxyPort`, or (b) remove/skip those tests. Fix `RegisteredService`: use `Host`/`Port` not `DestinationHost`/`DestinationPort`. Policy tests: `AllowedDestinations`/`RegisteredServices` must be non-null; at least one non-empty when Enabled; no wildcards in `AllowedDestinations`. |
| **PodMembershipSignerTests** | Inspect `PodMembershipSigner` and tests; align ctors, `IPodService`/`IPodMembershipVerifier`, and any options. Remove `Compile Remove`, fix compile and asserts. |
| **PodMessagingRoutingTests** | Align with `PodMessageRouter` / `IPodMessageRouter` and `IPodMessaging`; fix mocks and any `PodMessage`/`PodChannel` usage. |
| **PodsControllerTests** | Align with current `PodsController` and API (routes, `IPodService`, options). Likely need `WebApplicationFactory` or similar; if too heavy, keep deferred. |

### Transfers & shares

| File | What to do |
|------|------------|
| **UploadGovernorTests** | Match `UploadGovernor` (or equivalent) and options; fix mocks and asserts. |
| **UploadQueueTests** | Match `UploadQueue` and deps; fix mocks and asserts. |
| **ShareScannerModerationTests** | Align with `ShareScannerModeration`, `ModerationDecision`/`ModerationVerdict`; fix file/share mocks. |

---

## 2. Medium effort — need more inspection

- **MembershipGateTests** — Expects `PodServices` + `IPodRepository` and in-memory `Pod.Members`; current design uses `IPodService` and `JoinAsync` (members elsewhere). Either implement a thin adapter/stub that matches the test’s repo + `Pod.Members` contract, or rewrite tests to `IPodService` and remove `Pod.Members` assumptions.
- **PrivateGatewayMeshServiceTests** — Update to current `PrivateGatewayMeshService`, `PodPrivateServicePolicy`, and service-fabric/transport dependencies.
- **PodCoreApiIntegrationTests, PodCoreIntegrationTests, SqlitePodMessagingTests** — Integration-style; need DB/DI. Prefer running in integration project or with `WebApplicationFactory`/in-memory SQLite. Re-enable in unit project only if we add minimal integration harness.

### Mesh (by area)

- **Transport (simpler first):**  
  `CanonicalSerializationTests`, `CertificatePinManagerTests`, `DnsLeakPreventionVerifierTests`, `LoggingUtilsTests` — often fewer external deps; fix to current type names and ctors.
- **Transport (harder):**  
  `TorSocksTransportTests`, `RateLimiterTests`, `ConnectionThrottlerTests` — `RateLimiter` and options type changes.  
  `DescriptorSigningServiceTests`, `HttpTunnelTransportTests`, `MeekTransportTests`, `Obfs4TransportTests`, `WebSocketTransportTests`, `TransportDialerTests`, `TransportSelectorTests`, `TransportPolicyTests`, `AnonymityTransportSelectionTests`, `SecurityUtilsTests` (Mesh) — need current transport and options (e.g. Obfs4, Meek, HttpTunnel, WebSocket).
- **Privacy:**  
  `CoverTrafficGeneratorTests`, `RandomJitterObfuscatorTests`, `TimedBatcherTests` — align with current types.  
  `OverlayPrivacyIntegrationTests`, `PrivacyLayerIntegrationTests` — more integrated; check deps.
- **Realm / ServiceFabric / other:**  
  `BridgeFlowTypesTests`, `RealmChangeValidatorTests`, `MultiRealmConfigTests`, `RealmConfigTests`, `RealmIsolationTests`, `DestinationAllowlistTests`, `RateLimitTimeoutTests`, `DhtMeshServiceDirectoryTests`, `MeshGatewayAuthMiddlewareTests`, `MeshServiceRouterSecurityTests`, `MeshServiceRouterTests`, `RouterStatsTests`, and the rest of Mesh (Bridge, Migration, Censorship, Circuit, DomainFronted, MeshSync, Phase8, etc.) — each needs a pass to current interfaces/options/APIs.

### Block G (SocialFederation, MediaCore, Audio, Integrations, HashDb)

- One-by-one: remove `Compile Remove`, fix compile (ActivityPub, ContentId, AudioTags, MusicBrainz, HashDb, etc.), then fix asserts. Order flexible.

---

## 3. Heavier / blocked

- **Common\CodeQuality\** — `slskd` has `Compile Remove="Common\CodeQuality\**"`; those types are not in the main build. Re-enabling means including CodeQuality in `slskd`, adding `Microsoft.Build` (or similar) refs, and fixing MSBuild/analyzer deps. Do as a dedicated CodeQuality pass.
- **Mesh Transport (Tor, RateLimiter, Options)** — Central `RateLimiter` and transport-specific options (TorSocks, Obfs4, Meek, HttpTunnel, WebSocket) have changed; several tests are deferred in csproj for that. Need a focused Transport/options pass.
- **VirtualSoulfind (Block H)** — Blockers: `IContentBackend`, `ContentBackendType`, `ContentDescriptor.Filename`, `PlanStatus.Success`, `TestContext`. Prefer: (1) map to existing V2 types if they exist under different names, or (2) add minimal types that match test contracts, then re-enable. Otherwise refactor tests to current V2 design.

---

## 4. Recommended order for “can accomplish” work

1. **PodAffinityScorerTests** — Low risk; types and interfaces already match.
2. **PodPolicyEnforcementTests** — Pure test rewrites to `(bool, string)` and policy/validation rules; no new prod surface if we only add optional helpers (e.g. `IsValidHostPattern` etc.) or drop those tests.
3. **PodMembershipSignerTests**, **PodMessagingRoutingTests** — After a quick check that the corresponding types and interfaces exist.
4. **Transfers:** **UploadGovernorTests**, **UploadQueueTests** — Then **ShareScannerModerationTests**.
5. **PodsControllerTests** — If we decide to support controller tests in the unit project (might need `WebApplicationFactory` or a small test host).
6. **Mesh (simpler)** — `CanonicalSerializationTests`, `CertificatePinManagerTests`, `DnsLeakPreventionVerifierTests`, `LoggingUtilsTests`, then `CoverTrafficGeneratorTests`, `RandomJitterObfuscatorTests`, `TimedBatcherTests`.
7. **Block G** — One area at a time (e.g. MediaCore, then Audio, HashDb, Integrations, SocialFederation).
8. **MembershipGateTests**, **PrivateGatewayMeshServiceTests**, **PodCore integration tests** — When we’re ready for adapter/stub or integration harness work.
9. **Mesh Transport (harder)**, **CodeQuality**, **VirtualSoulfind** — As separate, focused passes.

---

## 5. Already done (for reference)

- **PodCore:** PeerIdFactoryTests, PodIdFactoryTests, PodModelsTests, ConversationPodCoordinatorTests, PodValidationTests, MessageSignerTests, GoldStarClubServiceTests  
- **Signals:** SignalBusTests  
- **Moderation (B1–B4), Security (C1–C3), Files (B4)** — per execution plan  

---

## 6. `Compile Remove` list (as of now)

The full set is in `tests/slskd.Tests.Unit/slskd.Tests.Unit.csproj`. The following are **not** in “already done” and are in scope for future work:

- `Common\CodeQuality\**`
- `PodCore\`: MembershipGateTests, PodCoreApiIntegrationTests, SqlitePodMessagingTests, PodCoreIntegrationTests, **PodPolicyEnforcementTests**, PrivateGatewayMeshServiceTests, **PodAffinityScorerTests**, **PodMembershipSignerTests**, **PodMessagingRoutingTests**, **PodsControllerTests**
- `Mesh\`: (all Mesh Transport, Realm, ServiceFabric, Privacy, etc. in csproj)
- `SocialFederation\`, `MediaCore\`, `Audio\`, `Integrations\`, `HashDb\`
- **Shares\ShareScannerModerationTests**
- **Transfers\Uploads\UploadGovernorTests**, **Transfers\Uploads\UploadQueueTests**
- `VirtualSoulfind\` (all)

Bold = higher-confidence “we can accomplish” in the next passes.
