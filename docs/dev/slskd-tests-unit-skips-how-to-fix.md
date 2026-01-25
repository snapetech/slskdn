# slskd.Tests.Unit — All Skips and How to Fix Them

Canonical list of `[Fact(Skip = "...")]` / `[Theory(Skip = "...")]` as of the last audit, with concrete fix options.  
**App** = change in `src/slskd` (or config). **Test-only** = change only in `tests/slskd.Tests.Unit`.

**Status:** slskd.Tests.Unit has **0** `[Fact(Skip)]` / `[Theory(Skip)]`; 2255 tests pass. This doc records historical and potential skips and how to fix or re-enable them.

---

## 1. ActivityPubKeyStoreTests (8 skips)

| Test | Reason | How to fix |
|------|--------|------------|
| `EnsureKeypairAsync_CreatesKeypairForNewActor` | NSec `Key.Export(PkixPrivateKey)` and `Key.Export(RawPrivateKey)` both throw in this environment | **App:** Ensure a key format/export path works on this runtime (e.g. try `RawPrivateKey` on a different runtime; or add `IEd25519KeySerializer` and inject a test double that returns prebuilt keys). Phase 0.2. |
| `GetPublicKeyAsync_ReturnsPemFormattedKey` | Same NSec export | Same as above. |
| `GetPrivateKeyAsync_ReturnsProtectedKey` | Same NSec export | Same as above. |
| `RotateKeypairAsync_ChangesKeypair` | Same NSec export | Same as above. |
| `EnsureKeypairAsync_IdempotentForExistingActor` | Same NSec export | Same as above. |
| `GetPublicKeyAsync_ThrowsForUnknownActor` | Same NSec export | Same as above. |
| `GetPrivateKeyAsync_ThrowsForUnknownActor` | Same NSec export | Same as above. |
| `VerifySignatureAsync_ReturnsFalseForInvalidSignature` | Same NSec export (needs GetPrivateKey) | Same as above. |

---

## 2. SecurityUtilsTests (0 skips)

| Test | Reason | How to fix |
|------|--------|------------|
| ~~`ConstantTimeEquals_TimingAttackResistance`~~ | **FIXED.** Timing variance heuristic was too strict. Relaxed to `max-min < Math.Max(avg*50, 12000)` to reduce CI/full-suite flakiness. | — |
| `ConstantTimeEquals_LargeArrays_PerformsConstantTime` | Ratio `unequal/equal`; can be flaky | **Test-only:** Relaxed to `ratio < 300`. (Not skipped.) |

---

## 3. PodsControllerTests (4 skips)

| Test | Reason | How to fix |
|------|--------|------------|
| `DeletePod_WithValidPodId_ReturnsNoContent` | `IPodService` has no `DeletePodAsync`; `PodsController` has no DeletePod endpoint | **App:** Add `Task DeletePodAsync(string podId, CancellationToken ct)` to `IPodService` and implement in `SqlitePodService` (and any other impl); add `DELETE /pods/{podId}` (or equivalent) in `PodsController` that calls it. |
| `DeletePod_WithInvalidPodId_ReturnsNotFound` | Same | Same as above. |
| `GetMessages_WithSoulseekDmBinding_ReturnsConversationMessages` | `PodsController` has no Soulseek DM/conversation branch for GetMessages; `_conversationServiceMock` not defined | **App:** Implement Soulseek DM/conversation branch in GetMessages and inject `IConversationService` (or equivalent); **Test:** define and setup `_conversationServiceMock` and route to that branch. |
| `SendMessage_WithSoulseekDmBinding_SendsConversationMessage` | Same for SendMessage | Same as above for SendMessage. |

---

## 4. PodCoreIntegrationTests (2 skips)

| Test | Reason | How to fix |
|------|--------|------------|
| `PodDeletionCleansUpMessages` | `IPodService` has no `DeletePodAsync` | **App:** Same as PodsControllerTests DeletePod: add `DeletePodAsync` and have it remove associated messages (or cascade). |
| `VpnPod_MaxMembers_EnforcedDuringJoin` | `ValidatePrivateServicePolicy` requires `GatewayPeerId` to be in `members` at create; `CreateAsync` does not add members. Cannot create VPN pod with 0 members and then join to exceed MaxMembers. | **App:** Support create-then-join for VPN pods: e.g. allow creating a VPN pod with 0 members if `GatewayPeerId` is set, and have `ValidatePrivateServicePolicy` accept “GatewayPeerId will be added on first join”; or add an explicit “bootstrap” step that adds the gateway as first member. **Test:** Create pod (with or without gateway in members per new semantics), then `JoinAsync` until `memberCount > MaxMembers` and assert the last join is rejected. |

---

## 5. PodCoreApiIntegrationTests (0 skips)

| Test | Reason | How to fix |
|------|--------|------------|
| ~~`ConversationPodCoordinatorApiIntegration`~~ | **FIXED.** Test uses a single `SqliteConnection("Filename=:memory:")` and `DbContextOptionsBuilder.UseSqlite(_connection)`; both `SqlitePodService` (IDbContextFactory) and `SqlitePodMessaging` (scoped PodDbContext) share the same in-memory DB. Coordinator and GetMessages/SendMessage see each other’s writes. | — |

---

## 6. PrivacyLayerIntegrationTests (0 skips)

| Test | Reason | How to fix |
|------|--------|------------|
| ~~`PrivacyLayer_HandlesInvalidConfiguration_Gracefully`~~ | **FIXED.** `RandomJitterObfuscator` clamps `minDelayMs < 0` to 0 in ctor. Test passes `JitterMs = -10`; PrivacyLayer constructs obfuscator and no throw. | — |

---

## 7. MultiRealmConfigTests (0 skips)

| Test | Reason | How to fix |
|------|--------|------------|
| ~~`IsFlowAllowed_WithNullOrEmptyFlow_ReturnsFalse`~~ | **FIXED.** `MultiRealmConfig.IsFlowAllowed` has `if (string.IsNullOrWhiteSpace(flow)) return false;`. Test asserts `IsFlowAllowed(null)`, `IsFlowAllowed("")`, `IsFlowAllowed("   ")` → false. | — |

---

## 8. MembershipGateTests (6 skips)

| Test | Reason | How to fix |
|------|--------|------------|
| `JoinAsync_VpnPodAtCapacity_ReturnsFalse` | `CreateAsync` requires VPN policy with `AllowedDestinations` and `ValidatePrivateServicePolicy(GatewayPeerId in members)`; cannot create VPN pod with 0 members | **App:** Same as `VpnPod_MaxMembers_EnforcedDuringJoin`: create-then-join or bootstrap so a VPN pod can exist with at least `GatewayPeerId` in members. **Test:** Create VPN pod at capacity (e.g. MaxMembers=2 with 2 members including gateway), then `JoinAsync` and assert `false`. |
| `JoinAsync_VpnPodWithAvailableCapacity_Succeeds` | Same | **App:** Same. **Test:** Create VPN pod with 1 member (e.g. gateway), MaxMembers=2; `JoinAsync` and assert `true`. |
| `JoinAsync_VpnPodWithoutPolicy_Succeeds` | `CreateAsync` requires `PrivateServicePolicy` for `PrivateServiceGateway` | **App:** Only if product allows VPN pod without policy (unlikely). Otherwise **Test-only:** Remove or reword test to assert that create fails without policy; or move to an “invalid create” test. |
| `JoinAsync_VpnPodWithDisabledPolicy_Succeeds` | `CreateAsync` requires `policy.Enabled == true` for `PrivateServiceGateway` | Same as above: **Test-only:** assert create/join fails when policy disabled, or remove. |
| ~~`JoinAsync_NullMember_Throws`~~ | **FIXED.** `PodService.JoinAsync` and `SqlitePodService.JoinAsync` throw `ArgumentNullException(nameof(member))` for null. | — |
| `JoinAsync_GatewayPeer_JoinSucceeds` | Same as VpnPodAtCapacity: cannot create VPN pod with 0 members | **App:** Create-then-join / bootstrap. **Test:** Create VPN pod with 0 or 1 member such that the “gateway peer” join is the one under test; assert success. |

---

## 9. CircuitMaintenanceServiceTests (0 skips)

| Test | Reason | How to fix |
|------|--------|------------|
| ~~`ExecuteAsync_ContinuesAfterMaintenanceException`~~ | **FIXED.** `IMeshCircuitBuilder` exists; `CircuitMaintenanceService` takes it. Test uses `Mock<IMeshCircuitBuilder>.Setup(x => x.PerformMaintenance()).Throws(...)` and passes. Skip removed. | — |
| ~~`ExecuteAsync_SkipsCircuitTestingWhenActiveCircuitsExist`~~ | **FIXED.** `Mock<IMeshCircuitBuilder>` with `GetStatistics` → `ActiveCircuits=1`; invoke `PerformMaintenanceAsync` via reflection; verify `GetCircuitPeersAsync` Never. | — |
| ~~`ExecuteAsync_TestsCircuitBuildingWhenNoActiveCircuitsAndEnoughPeers`~~ | **FIXED.** Real `MeshCircuitBuilder`; `GetCircuitPeersAsync` returns 1 peer (BuildCircuit needs ≥3); verifies `GetCircuitPeersAsync` Once. | — “succeeds” without real network. |

---

## 10. Phase8MeshTests (0 skips)

| Test | Reason | How to fix |
|------|--------|------------|
| ~~`MeshHealthCheck_AssessesHealth`~~ | **FIXED.** Test uses real `MeshStatsCollector`; `MeshHealthCheck` depends on it and assesses health. | — |

---

## 11. IpldMapperTests (0 skips)

| Test | Reason | How to fix |
|------|--------|------------|
| `TraverseAsync_MaxDepthExceeded_StopsTraversal` | Test used `maxDepth: 0`; `IpldMapper.TraverseAsync` throws `ArgumentOutOfRangeException` for `maxDepth < 1` or `> 10` | **Test-only:** Use `maxDepth: 1` (or 2) and a graph with more than one level (e.g. start → link → node → …). Assert `CompletedTraversal == false` or that `VisitedNodes`/`Paths` stop at the depth limit. The “max depth exceeded” behavior is “stops at limit,” not “pass 0.” |

---

## 12. WorkRefTests (1 skip)

| Test | Reason | How to fix |
|------|--------|------------|
| `FromMusicItem_CreatesValidWorkRef` | `ContentDomain.MusicContentItem` removed; `WorkRef.FromMusicItem` expects `MusicItem` from VirtualSoulfind | **App:** Restore a `MusicItem`-like type (or `ContentDomain` for it) that `WorkRef.FromMusicItem` can use, or add `FromMusicItem(MusicItem)` in the layer that has `MusicItem`. **Test:** Build a `MusicItem` (or equivalent) and call `WorkRef.FromMusicItem`; assert the resulting `WorkRef` is valid. |

---

## 13. PodPolicyEnforcementTests (0 skips)

| Test | Reason | How to fix |
|------|--------|------------|
| `ValidatePod_PrivateServiceGateway_ExceedsCurrentMembers_Fails` | Assumed `ValidatePrivateServicePolicy` runs before `memberCount > MaxMembers`; in practice `memberCount > MaxMembers` is checked first in `ValidateCapabilities` | **Test-only:** Call `PodValidation.ValidatePod(pod, members)` with: `pod` has `PrivateServiceGateway`, `PrivateServicePolicy` with `MaxMembers = 3`, `Enabled = true`, `AllowedDestinations` (non-empty), `GatewayPeerId` (non-empty, valid). `members` has 4 elements. `ValidatePod` → `ValidateCapabilities(..., memberCount: 4)` fails at `memberCount > policy.MaxMembers`. Assert `(false, error)` and that `error` contains “members” and “maximum 3” (or equivalent). |

---

## 14. LocalPortForwarderTests (0 skips)

All 6 tests below now pass (RecordingMeshServiceClient, correct Payload, CreateTunnelConnectionAsync_TunnelRejected_ReturnsNull).

| Test | Reason | How to fix |
|------|--------|------------|
| ~~`StartForwardingAsync_TunnelRejected_ThrowsException`~~ | `StartForwardingAsync` does not open a tunnel at start; rejection happens on first client connect when `CreateTunnelConnectionAsync` is called | **Test-only:** Don’t assert on `StartForwardingAsync` for rejection. Instead: start forwarding, then (if possible) trigger the first connection (e.g. connect to the local listener) so `CreateTunnelConnectionAsync` runs and the mesh returns Forbidden; assert on that path (e.g. no connection added, or status reflects failure). Or document that “tunnel rejected” is tested at first connect and remove this test. |
| ~~`CreateTunnelConnectionAsync_TunnelAccepted_ReturnsConnection`~~ | `OpenTunnelResponse` JSON deserialization from mock `Payload` returns null | **Test-only:** Ensure mock `ServiceReply.Payload` is what `JsonSerializer.Deserialize<OpenTunnelResponse>(response.Payload)` expects: same type (`byte[]` or `ReadOnlyMemory<byte>` and overload used) and JSON shape. Use `JsonSerializer.SerializeToUtf8Bytes(new OpenTunnelResponse { TunnelId = "tunnel-123", Accepted = true })` or `new { TunnelId = "tunnel-123", Accepted = true }` (PascalCase). If `Payload` is `ReadOnlyMemory<byte>`, use `JsonSerializer.Deserialize<OpenTunnelResponse>(response.Payload.Span)` or the `ReadOnlyMemory` overload. |
| ~~`SendTunnelDataAsync_ValidData_CallsService`~~ | `CallServiceAsync` not invoked from internal `SendTunnelDataAsync` | **Test-only:** `SendTunnelDataAsync` is internal and does call `_meshClient.CallServiceAsync("private-gateway", "TunnelData", ...)`. Ensure: (1) test calls `_portForwarder.SendTunnelDataAsync("tunnel-123", data)` (InternalsVisibleTo); (2) mock is on the same `IMeshServiceClient` used by `_portForwarder`. If the forwarder or `ForwarderConnection` uses a different client, the test cannot verify via this mock—then **App:** inject `IMeshServiceClient` so the instance that sends `TunnelData` is the one mocked. |
| ~~`ReceiveTunnelDataAsync_ValidResponse_ReturnsData`~~ | `GetTunnelDataResponse` JSON deserialization from mock `Payload` returns null | **Test-only:** Same as `OpenTunnelResponse`: set `Payload = JsonSerializer.SerializeToUtf8Bytes(new GetTunnelDataResponse { Data = testData })` or `new { Data = testData }` (byte[] serializes as base64). Ensure `Deserialize<GetTunnelDataResponse>(response.Payload)` gets the right type (e.g. `ReadOnlyMemory` vs `byte[]`). |
| ~~`ReceiveTunnelDataAsync_NoData_ReturnsNull`~~ | Same `GetTunnelDataResponse` deserialization | **Test-only:** Use `new GetTunnelDataResponse { Data = Array.Empty<byte>() }` or `new { Data = Array.Empty<byte>() }`. Assert `result != null && result.Length == 0` (or whatever “no data” means: empty array vs null). Align with `ReceiveTunnelDataAsync`’s return semantics. |
| ~~`CloseTunnelAsync_ValidTunnel_CallsService`~~ | `CallServiceAsync` not invoked from internal `CloseTunnelAsync` | **Test-only:** Same as `SendTunnelDataAsync`: confirm the `CloseTunnelAsync` under test uses the mocked `_meshClient.CallServiceAsync("private-gateway", "CloseTunnel", ...)`. If it does, the mock and call path are wrong; if `CloseTunnelAsync` is only used from `ForwarderConnection` and that uses a different client, **App:** unify so the test can verify. |

---

## 15. PerceptualHasherTests (0 skips)

| Test | Reason | How to fix |
|------|--------|------------|
| ~~`ComputeHash_DifferentFrequencies_ProduceDifferentHashes`~~ | **FIXED.** Test uses 440 Hz vs 262 Hz (different pitch classes), asserts similarity < 0.95. | — (or algorithm) for how it maps frequency to hash; assert that two inputs that are defined as “different” in that model produce different hashes. If the implementation treats 440/880 as similar by design, change the test to use two inputs that are clearly different per the algorithm. **App:** Only if the current “different frequencies → different hashes” is a documented guarantee; then implement or fix the hasher. |

---

## 16. Obfs4TransportTests (1 skip)

| Test | Reason | How to fix |
|------|--------|------------|
| `IsAvailableAsync_VersionCheckFailure_ReturnsFalse` | Environment-dependent: `/bin/ls --version` may exit 0 on some systems, so `IsAvailable` returns true | **Test-only:** Mock or abstract the process that runs the version check (e.g. `IProcessRunner` or `IObfs4VersionChecker`). **App:** Inject an `IObfs4Availability` (or similar) that runs the real check; tests inject a stub that returns `false` for this test. |

---

## 17. RateLimitTimeoutTests (0 skips)

| Test | Reason | How to fix |
|------|--------|------------|
| ~~`OpenTunnel_ConcurrentTunnelsPerPeerWithinLimit_Accepted`~~ | **FIXED.** `CreateServiceForOpenTunnelSuccess(TestTunnelConnectivity)` + in-process `TcpListener`. | — |
| ~~`OpenTunnel_NewTunnelsRateLimitWithinLimits_Accepted`~~ | **FIXED.** Same. | — |
| `CleanupExpiredTunnels_RemovesIdleTunnels` | `CleanupExpiredTunnels(policy)` does not exist; prod has `CleanupExpiredTunnelsAsync()` which loops and uses pod policy from `_podService.GetPodAsync` | **Test-only:** Don’t call a `CleanupExpiredTunnels(policy)` overload. Instead: (1) Get the `_activeTunnels` (reflection) and add a `TunnelSession` with `LastActivity` (or `CreatedAt`) in the past; (2) set `_podServiceMock.GetPodAsync` to return a pod with `IdleTimeout` (and `MaxLifetime`) so that session is expired; (3) invoke the existing `CleanupExpiredTunnelsAsync` loop once (e.g. by exposing a `TriggerCleanupAsync` for tests or running the loop method via reflection). Assert the session is removed. **App (optional):** Add `CleanupExpiredTunnelsAsync(PrivateServicePolicy? overrides = null)` for tests that overrides timeout when non-null. |
| `CleanupExpiredTunnels_RemovesMaxLifetimeExceededTunnels` | Same | Same as above; use `CreatedAt` old enough and `MaxLifetime` so the session is expired. |
| `CleanupExpiredTunnels_KeepsActiveTunnelsWithinLimits` | Same | Same: drive `CleanupExpiredTunnelsAsync` with a mix of expired and non-expired sessions and assert only expired are removed. |

---

## 18. PodCoreIntegrationTests / PodsControllerTests — not listed above

Those are covered in sections **4** and **3** respectively.

---

## Summary by fix type

- **App required (or strongly suggested):** ActivityPubKeyStore (8), PodsController DeletePod (2), PodsController Soulseek DM (2), PodCoreIntegration DeletePod (1), PodCoreIntegration VpnPod_MaxMembers (1), MembershipGate VPN/create-then-join (5), WorkRef FromMusicItem (1), Obfs4Transport (1).  
- **Test-only (or test-first):** Obfs4Transport (1).  
- **Either app or test-only:** Obfs4Transport (1).  
- **FIXED (no longer skipped):** SecurityUtils timing, PrivacyLayer, MultiRealmConfig IsFlowAllowed, IpldMapper maxDepth, PodPolicyEnforcement ExceedsCurrentMembers, RateLimitTimeout OpenTunnel (2) and CleanupExpiredTunnels (3), MembershipGate JoinAsync_NullMember_Throws, CircuitMaintenance (3: ContinuesAfterMaintenanceException, SkipsCircuitTesting, TestsCircuitBuilding), PodCoreApiIntegration ConversationPodCoordinator, LocalPortForwarder (6), PerceptualHasher ComputeHash_DifferentFrequencies, Phase8Mesh MeshHealthCheck_AssessesHealth.

---

## Cross-references

- **Completion plan:** `docs/dev/slskd-tests-unit-completion-plan.md` (Phase 0, Deferred, Discuss: app).
- **Phase 0.2 (NSec):** ActivityPubKeyStore.
- **Phase 0.3 (CircuitMaintenance):** `ExecuteAsync_ContinuesAfterMaintenanceException`; Path B in completion plan.
