# Active Context

> What is currently being worked on in this repository.  
> Update this file when starting or finishing work.

---

## ⚠️ WORK DIRECTORY (do not use /home/keith/Code/cursor)

**Project root: `/home/keith/Documents/code/slskdn`**

All `git`, `dotnet`, and file paths in this repo are under this directory. Do not use the `cursor` folder under `~/Code/` for slskdn work — it is a separate project.

---

## 🚨 Before Ending Your Session

**Did you fix any bugs? Document them in `adr-0001-known-gotchas.md` NOW.**

This is the #1 most important thing to do before ending a session. Future AI agents (and humans) will thank you.

---

## Current Session

- **Current Task**: Continue bughunting broader helper/control-path patterns where ancillary callbacks can still poison primary runtime behavior
- **Branch**: `release-main`
- **Environment**: Local dev
- **Last Activity**:
  - Hardened another signal-system registration seam:
    - `SignalBus.RegisterChannelHandler(...)` now ignores duplicate registration for an already-registered channel instead of replacing the live handler and starting another receiver with no unsubscribe path
    - repeated signal-system initialization no longer silently orphaned the old live handler behind a dictionary overwrite
  - Extended `tests/slskd.Tests.Unit/Signals/SignalBusTests.cs` with duplicate-registration coverage
  - Confirmed the focused signal-bus slice passed (`12/12`) and the runtime release build remained green (`0 warnings / 0 errors`)
  - Added ADR-0001 gotcha `0k133` and committed it immediately per repo policy (`docs: Add gotcha for duplicate channel registration`)
  - Hardened another paired control-path race in signal channel startup:
    - `MeshSignalChannelHandler` and `BtExtensionSignalChannelHandler` now guard their one-time event subscription with a lock instead of a naked `receivingStarted` flag
    - concurrent `StartReceivingAsync(...)` calls no longer double-subscribe and duplicate every later delivery
  - Extended `tests/slskd.Tests.Unit/Signals/SignalChannelHandlerTests.cs` with concurrent-start regressions for both handlers
  - Confirmed the focused signal-channel slice passed (`4/4`) and the runtime release build remained green (`0 warnings / 0 errors`)
  - Added ADR-0001 gotcha `0k132` and committed it immediately per repo policy (`docs: Add gotcha for concurrent start subscription races`)
  - Completed the in-progress metrics/logging helper isolation batch:
    - `ExponentialMovingAverage.Update(...)` now isolates `onUpdate` observer failures after updating internal state
    - `DelegatingSink.Emit(...)` now swallows observer delegate failures so log observers cannot break the sink pipeline
  - Extended `tests/slskd.Tests.Unit/Core/CallbackInfrastructureTests.cs` with focused regressions for EMA observer failures and delegating sink observer failures
  - Confirmed the focused callback-infrastructure slice passed (`7/7`) and the runtime release build remained green (`0 warnings / 0 errors`)
  - Investigated the reported Arch `makepkg` failure for `slskdn-bin` `0.24.5.slskdn.97`:
    - cloned the live AUR `slskdn-bin` package and verified it currently pins SHA256 `ada54ed76a8e32cdbf35cbed422a62eba079c4766677ec63d384554d36da241e`
    - downloaded the live GitHub asset `slskdn-main-linux-x64.zip` for release `0.24.5-slskdn.97` and confirmed it currently hashes to the same value
    - concluded the release process is brittle because stable AUR was pinning a mutable GitHub asset checksum; failures can happen when the asset is replaced or users keep stale cached bytes under the same version
  - Hardened the stable AUR publish path:
    - `.github/workflows/build-on-tag.yml` now keeps the `slskdn-bin` ZIP on `sha256sums=('SKIP' ...)` and only rewrites static packaging-file hashes
    - `packaging/aur/README.md` now documents that both stable and dev AUR binary zips use `SKIP`
  - Added ADR-0001 gotcha `0k131` and committed it immediately per repo policy (`docs: Add gotcha for AUR mutable release asset checksums`)
  - Expanded the bughunt from single issues to a broader helper-pattern pass:
    - `Retry` now preserves the original operation exception when `onFailure` or `isRetryable` callbacks throw by surfacing both failures through `RetryException(AggregateException)`
    - `RateLimiter` timer fanout now isolates staged callback failures on the timer thread instead of letting them escape shared scheduling infrastructure
    - the non-generic `Retry.Do(Func<Task>)` adapter no longer returns a stray boxed `Task` object from the generic path
  - Added focused regression coverage in `tests/slskd.Tests.Unit/Core/RetryTests.cs` and extended `tests/slskd.Tests.Unit/Core/CallbackInfrastructureTests.cs`
  - Confirmed the focused helper slice passed (`7/7`), the runtime release build remained green (`0 warnings / 0 errors`), and `bash ./bin/lint` passed
  - Added ADR-0001 gotcha `0k129` and committed it immediately per repo policy (`docs: Add gotcha for retry callback failure masking`)
  - Continued the lifecycle bughunt into `LocalPortForwarder` stream mapping:
    - `ForwarderConnection` now launches mapping workers with a stable local CTS, not a mutable field lookup
    - natural stream-mapping completion now clears and disposes `_streamMappingCts` instead of leaving stale mapping state behind until `CloseAsync()`
  - Added focused regression coverage in `tests/slskd.Tests.Unit/Common/Security/LocalPortForwarderTests.cs`
  - Confirmed the focused local-port-forwarder slice passed (`20/20`) and the runtime release build remained green (`0 warnings / 0 errors`)
  - Added ADR-0001 gotcha `0k120` and committed it immediately per repo policy (`docs: Add gotcha for stale stream-mapping CTS state`)
  - Continued the startup-token lifecycle sweep into cover traffic:
    - `CoverTrafficGenerator.StartAsync()` now cancels the previous generation CTS before disposing/replacing it
    - restart/reinitialization no longer leaves an older generation token source in the disposed-but-uncanceled state
  - Added focused regression coverage in `tests/slskd.Tests.Unit/Common/Security/CoverTrafficGeneratorTests.cs`
  - Confirmed the focused cover-traffic slice passed (`24/24`) and the runtime release build remained green (`0 warnings / 0 errors`)
  - Extended the existing ADR-0001 startup-CTS gotcha entry and committed it immediately per repo policy (`docs: Add gotcha for startup CTS disposal races`)
  - Continued the same startup/restart lifecycle sweep into the remaining initializer surfaces:
    - `Application` and `DhtRendezvousService` now cancel prior startup/background initialization CTS instances before disposing/replacing them
    - repeated startup/restart paths no longer risk leaving previous initialization work attached to a disposed CTS
  - Extended focused regression coverage in `tests/slskd.Tests.Unit/Core/HostedServiceLifecycleTests.cs`
  - Confirmed the focused lifecycle slice passed (`4/4`) and the runtime release build remained green (`0 warnings / 0 errors`)
  - Extended the existing ADR-0001 startup-CTS gotcha entry and committed it immediately per repo policy (`docs: Add gotcha for startup CTS disposal races`)
  - Fixed another long-lived lifecycle bug cluster in startup/disposal helpers:
    - `HashDbOptimizationHostedService`, `RealmHostedService`, `MultiRealmHostedService`, and `MdnsAdvertiser` now capture stable local CTS instances for detached startup work instead of dereferencing mutable fields from background tasks
    - dispose/replacement paths now cancel before disposing so startup work does not outlive the owning service or trip disposed/null CTS races during shutdown
  - Added focused regression coverage in `tests/slskd.Tests.Unit/Core/HostedServiceLifecycleTests.cs`
  - Confirmed focused lifecycle tests passed (`3/3`), the runtime release build stayed green (`0 warnings / 0 errors`), full `dotnet test --no-restore` passed (`3587/3587`), and `bash ./bin/lint` passed
  - Added the corresponding gotcha to `adr-0001-known-gotchas.md` and committed it immediately per repo policy (`docs: Add gotcha for startup CTS disposal races`)
  - Fixed `ChannelReader<T>` failure propagation so `Completed` is the stable fault surface for detached read-loop errors
  - Added focused regression coverage in `tests/slskd.Tests.Unit/Core/ChannelReaderTests.cs`
  - Confirmed the focused slice passed and the runtime build remains green
  - Added the corresponding gotcha to `adr-0001-known-gotchas.md` and will commit it immediately per repo policy
  - Fixed `LanDiscoveryService.PeerDiscovered` so the event is no longer a no-op contract
  - Hardened `LanDiscoveryService.PeerDiscovered` fanout so one failing subscriber no longer aborts the rest of browse enumeration
  - Tightened `LanDiscoveryServiceTests` to prove subscribers are retained and invoked
  - Confirmed the focused identity slice passed (`7/7`) and the runtime build remains green
  - Added the corresponding gotcha to `adr-0001-known-gotchas.md` and will commit it immediately per repo policy
  - Hardened scene pubsub/chat event fanout:
    - `ScenePubSubService` and `SceneChatService` no longer use raw multicast invocation for `MessageReceived`
    - one failing scene subscriber no longer aborts later listeners or the rest of delivery
  - Added focused regression coverage in `tests/slskd.Tests.Unit/VirtualSoulfind/SceneServicesTests.cs`
  - Confirmed the focused scene slice passed (`6/6`) and the runtime build remains green (`0 warnings / 0 errors`)
  - Added the corresponding gotcha to `adr-0001-known-gotchas.md` and will commit it immediately per repo policy
  - Hardened `MeshNeighborRegistry` event fanout:
    - `NeighborAdded`, `FirstNeighborConnected`, and `NeighborRemoved` no longer fire under `_registrationLock`
    - one failing subscriber no longer aborts later listeners or hold registry bookkeeping under the semaphore
  - Added focused regression coverage in `tests/slskd.Tests.Unit/DhtRendezvous/MeshNeighborRegistryTests.cs`
  - Confirmed the focused registry slice passed (`3/3`) and the runtime build remains green (`0 warnings / 0 errors`)
  - Added the corresponding gotcha to `adr-0001-known-gotchas.md` and will commit it immediately per repo policy
  - Fixed duplicate signal delivery in long-lived signal handlers:
    - `MeshSignalChannelHandler` and `BtExtensionSignalChannelHandler` no longer re-subscribe their inbound handlers on repeated `StartReceivingAsync(...)` calls
    - repeated startup/reconfiguration on the same instance no longer doubles inbound signal delivery
  - Added focused regression coverage in `tests/slskd.Tests.Unit/Signals/SignalChannelHandlerTests.cs`
  - Confirmed the focused signal slice passed (`2/2`) and the runtime build remains green (`0 warnings / 0 errors`)
  - Added the corresponding gotcha to `adr-0001-known-gotchas.md` and will commit it immediately per repo policy
  - Fixed a duplicate-greeting race in `DhtPeerGreetingService`:
    - peer greetings are now reserved in `_greetedPeers` before the async send starts
    - repeated neighbor-added events for the same peer no longer launch duplicate private-message greetings while the first send is still in flight
  - Added focused regression coverage in `tests/slskd.Tests.Unit/DhtRendezvous/DhtPeerGreetingServiceTests.cs`
  - Confirmed the focused DHT greeting slice passed and the runtime build remains green (`0 warnings / 0 errors`)
  - Added the corresponding gotcha to `adr-0001-known-gotchas.md` and will commit it immediately per repo policy
  - Hardened `TimedCounter` and `RateLimiter` callback/timer state handling
  - Added focused regression coverage in `tests/slskd.Tests.Unit/Core/CallbackInfrastructureTests.cs`
  - Confirmed the focused slice passed (`2/2`) and the runtime build remains green
  - Added the corresponding gotcha to `adr-0001-known-gotchas.md` and will commit it immediately per repo policy
  - Hardened `ManagedState<T>.SetValue(...)` so change listeners are invoked outside the state lock and no longer abort on the first failing subscriber
  - Added focused regression coverage in `tests/slskd.Tests.Unit/Core/ManagedStateTests.cs`
  - Confirmed the focused slice passed (`1/1`) and the runtime build remains green
  - Added the corresponding gotcha to `adr-0001-known-gotchas.md` and will commit it immediately per repo policy
  - Hardened more shared infrastructure emitters:
    - `Program.LogEmitted`, `DisasterModeCoordinator`, and `SoulseekHealthMonitor` no longer use raw multicast fanout
    - `SoulseekClientWrapper` room-message forwarding now isolates listener faults too
  - Added focused regression coverage in `tests/slskd.Tests.Unit/Core/SharedEventEmitterTests.cs`
  - Confirmed the focused slice passed (`4/4`) and the runtime build remains green
  - Added the corresponding gotcha to `adr-0001-known-gotchas.md` and will commit it immediately per repo policy
  - Hardened the remaining shared security/event emitters:
    - `SecurityEventAggregator`, `EntropyMonitor`, `Honeypot`, and `FingerprintDetection` no longer use raw `?.Invoke(...)` multicast fanout
    - one subscriber fault is now isolated and logged instead of aborting other listeners or escaping the emitter path
  - Added focused regression coverage in `tests/slskd.Tests.Unit/Common/Security/SecurityEventEmitterTests.cs`
  - Confirmed the focused security/event slice passed (`16/16`) and the runtime build remains green (`0 warnings / 0 errors`)
  - Added the corresponding gotcha to `adr-0001-known-gotchas.md` and will commit it immediately per repo policy
  - Investigated CodeQL alerts `#2547` and `#2548` in `RelayService.cs` and confirmed they were caused by raw cached relay connection ids being written to debug logs during validation failures.
  - Hardened `RelayService` logging so relay connection ids are logged through a hashed `GetConnectionLogId(...)` helper, and direct credential/token value logging was removed from adjacent validation paths.
  - Added the corresponding gotcha to `adr-0001-known-gotchas.md` and recorded the fix in `progress.md`.
  - Continued the same relay security sweep into `RelayHub`, `RelayController`, and `RelayClient`.
  - Replaced remaining raw relay request token and SignalR connection-id logs with hashed log ids, and removed adjacent token-bearing warning/info messages from upload/share flows.
  - Added the broader relay log-sanitization gotcha to `adr-0001-known-gotchas.md` and recorded the work in `progress.md`.
  - Found and fixed a real relay behavior bug while widening the same area:
    - `RelayClient` was invoking `RelayHub.NotifyFileUploadFailed` with only the request ID instead of `(id, exception)`
    - failed relay uploads can now notify the controller immediately instead of silently degrading into timeout behavior
  - Added the corresponding hub-signature drift gotcha to `adr-0001-known-gotchas.md` and recorded the fix in `progress.md`.
  - Found another relay control-flow bug in `RelayHub.OnConnectedAsync()`:
    - the disabled/wrong-mode branch called `Context.Abort()` but still fell through into auth-challenge generation
    - added the missing `return` so rejected relay connections stop immediately
  - Added the corresponding abort-control-flow gotcha to `adr-0001-known-gotchas.md` and recorded the fix in `progress.md`.
  - Found another relay detached-task bug in `RelayClient`:
    - the fire-and-forget upload/download workers could still fault off-thread when failure-reporting or retry-exhaustion paths threw
    - added explicit top-level observation and guarded failure reporting so relay failures no longer disappear as unobserved task faults
  - Added the corresponding detached relay-task gotcha to `adr-0001-known-gotchas.md` and recorded the fix in `progress.md`.
  - Found a real relay reconnect race in `RelayClient`:
    - `HubConnection_Reconnected()` reset the login waiter after reconnection even though the auth challenge can complete before that event fires
    - removed the extra reset so successful reconnect/login signals are not discarded before share resync
  - Added the corresponding reconnect waiter-race gotcha to `adr-0001-known-gotchas.md` and recorded the fix in `progress.md`.
  - Found another relay lifecycle bug in `RelayClient.Configure()`:
    - relay option changes rebuilt `HubConnection` without disposing the previous connection instance
    - reconfiguration now disposes the previous SignalR client explicitly before replacing it
  - Added the corresponding reconfiguration/disposal gotcha to `adr-0001-known-gotchas.md` and recorded the fix in `progress.md`.
  - Found two more relay-service reconfiguration bugs:
    - `RelayService.Configure()` used `||` in its dual-hash early-return, so real top-level relay changes could be skipped if controller settings were unchanged
    - replacing the relay client instance did not dispose the previous client
  - Fixed both and added focused `RelayServiceTests` coverage.
  - Added the corresponding configuration-guard and client-disposal gotchas to `adr-0001-known-gotchas.md` and recorded the fix in `progress.md`.
  - Investigated the failed `.95` main release and confirmed all build/publish jobs succeeded except `Publish to Snap (Main/Stable)`.
  - The Snap artifact built correctly; the only failing external response was the Snap Store processing error `binary_sha3_384: Error checking upload uniqueness.`
  - Hardened `.github/workflows/build-on-tag.yml` so both Snap publish paths retry this known store-side processing failure for longer with capped backoff instead of aborting after a short fixed retry window.
  - Added the corresponding gotcha to `adr-0001-known-gotchas.md` and recorded the release-publish hardening in `progress.md`.
  - Investigated the failed `build-main-0.24.5-slskdn.94` tag build and isolated the new blocker to `CoverTrafficGeneratorTests.StopAsync_AfterStart_CancelsGenerationPromptly`.
  - Fixed `CoverTrafficGenerator.StartAsync()` to start `GenerateCoverTrafficAsync(...)` directly instead of routing it through `Task.Run`, removing the CI-only thread-pool scheduling race that delayed cancellation.
  - Added the corresponding gotcha to `adr-0001-known-gotchas.md` and recorded the fix in `progress.md`.
  - Investigated the failed morning GitHub Actions runs and confirmed the blocking compile error was the Dependabot `Serilog.Sinks.Grafana.Loki` upgrade.
  - Updated `Program.cs` to use the formatter-based `GrafanaLoki(..., textFormatter: ...)` overload required by Loki sink 8.3.2.
  - Corrected the formatter construction to pass the format provider positionally because the target `MessageTemplateTextFormatter` API does not expose a `provider:` named argument.
  - Bumped `Serilog.Sinks.Grafana.Loki` in `src/slskd/slskd.csproj` to `8.3.2` so local and CI builds use the same dependency shape.
  - Added the corresponding gotcha to `adr-0001-known-gotchas.md` and recorded the work in `progress.md`.
  - Closed the secure-release validation loop:
    - fixed the remaining release-only unit-test drift in `LibraryHealthControllerTests` and `SearchActionsControllerTests`
    - confirmed serial validation passes:
      - `dotnet build --no-restore`
      - `dotnet test --no-restore`
      - `bash ./bin/lint`
      - `bash packaging/scripts/run-release-gate.sh`
    - documented ADR-0001 gotcha `0xD9` for release-mode test compile drift
  - Restored the remaining unit compile path:
    - `SearchActionsControllerTests`, `MeshContentMeshServiceTests`, and `PodMessageBackfillControllerTests` now match current runtime contracts again
    - `MeshServiceClient` now normalizes immutable `ServiceCall` DTOs via copied instances instead of mutating init-only properties
  - Fixed another release-facing transfer-result leak cluster:
    - `MultiSourceDownloadService` no longer returns exact throughput/byte-count diagnostics in chunk errors
    - `SwarmDownloadOrchestrator` no longer returns peer IDs, transport names, or raw byte counts in chunk failure results
  - Added focused `SwarmDownloadOrchestratorTests` coverage for sanitized chunk-result contracts
  - Confirmed the runtime build is still green (`0 warnings / 0 errors`)
  - Confirmed the unit project compiles again, and the focused touched slice passed (`20/20`)
  - Added the corresponding gotcha to `adr-0001-known-gotchas.md` and committed it immediately per repo policy
  - Hardened another public validation/helper batch:
    - `EnumAttribute` no longer echoes raw invalid enum values or array contents in validation failures
    - `JobManifestValidator` now returns stable category messages for unsupported manifest versions, unknown job types, and invalid status states
    - `DhtRendezvousController` now uses a stable unblock success message instead of echoing the removed blocklist target
  - Added focused `JobManifestValidatorTests` and `EnumAttributeTests`, and extended `DhtRendezvousControllerTests`
  - Confirmed the focused helper/controller validation slice passed (`10/10`)
  - Added the corresponding gotcha to `adr-0001-known-gotchas.md` and committed it immediately per repo policy
  - Hardened another response-contract cluster:
    - `HashDbController` no longer echoes FLAC keys in hash-miss replies
    - `DhtRendezvousController` block-success replies no longer echo blocked IPs or usernames
    - `LibraryHealth` scan-status misses now return the stable `Scan not found` contract
    - `IpldController` link-add success replies no longer echo content IDs
    - `SearchActionsController` scene-download enqueue failures now collapse per-item failure strings to a stable generic detail
  - Added focused coverage in `HashDbControllerTests`, `DhtRendezvousControllerTests`, `ApiLibraryHealthControllerTests`, `IpldControllerTests`, and `SearchActionsControllerTests`
  - Confirmed the focused response-contract slice passed (`38/38`)
  - Added the corresponding gotcha to `adr-0001-known-gotchas.md` and committed it immediately per repo policy
  - Tightened a final low-traffic success payload:
    - `PortForwardingController` start/stop success replies no longer echo the exact local port
  - Confirmed the focused `PortForwardingControllerTests` slice passed (`8/8`)
  - Added the corresponding gotcha to `adr-0001-known-gotchas.md` and committed it immediately per repo policy
  - Hardened another error-object batch:
    - `CanonicalController` and `ShadowIndexController` no longer attach MBIDs to generic 500 payloads
    - `UsersCompatibilityController` no longer returns the browsed username in 500 responses
    - `PodMembershipController` membership misses no longer echo `podId` / `peerId` in the not-found payload
    - `MeshGatewayController` no longer echoes service names, payload-size thresholds, or timeout seconds in public error messages
  - Added focused coverage in `CanonicalControllerTests`, `ShadowIndexControllerTests`, `UsersCompatibilityControllerTests`, `PodMembershipControllerTests`, and `MeshGatewayControllerTests`
  - Confirmed the focused error-contract slice passed (`18/18`)
  - Added the corresponding gotcha to `adr-0001-known-gotchas.md` and committed it immediately per repo policy
  - Hardened another Pod/library response-contract batch:
    - `LibraryItemsController` no longer echoes `contentId` in item-not-found responses
    - `PodsController` join/leave/ban/unbind success payloads no longer repeat `podId`, `peerId`, or `channelId`
    - `PodDhtController` metadata misses no longer echo `podId` in the not-found payload
    - `PodMessageRoutingController` seen/registration success payloads no longer repeat `messageId` or `podId`
  - Added focused coverage in `LibraryItemsControllerTests`, `PodsControllerTests`, `PodDhtControllerTests`, and `PodMessageRoutingControllerTests`
  - Confirmed the focused Pod/library response-contract slice passed (`56/56`)
  - Added the corresponding gotcha to `adr-0001-known-gotchas.md` and committed it immediately per repo policy
  - Hardened another acknowledgement-payload batch:
    - `RoomsCompatibilityController` join/leave success payloads no longer echo the room name
    - `PodJoinLeaveController` pending-request payloads no longer repeat `podId`
    - `PodsController` bind-room success payloads no longer repeat `podId`, `channelId`, `roomName`, or `mode`
    - `PodMessageSigningController` verify-message success payloads no longer echo `messageId`
  - Added focused coverage in `RoomsCompatibilityControllerTests`, `PodJoinLeaveControllerTests`, `PodsControllerTests`, and `PodMessageSigningControllerTests`
  - Confirmed the focused acknowledgement slice passed (`40/40`)
  - Added the corresponding gotcha to `adr-0001-known-gotchas.md` and committed it immediately per repo policy
  - Tightened two remaining request-echo acknowledgements:
    - `MeshController` publish-hash success payloads no longer echo `flacKey`
    - `LibraryHealth` remediation-job success messages no longer echo the submitted issue count
  - Added focused coverage in `MeshControllerTests` and `ApiLibraryHealthControllerTests`
  - Confirmed the focused acknowledgement slice passed (`9/9`)
  - Added the corresponding gotcha to `adr-0001-known-gotchas.md` and committed it immediately per repo policy
  - Hardened another success-payload response-contract batch:
    - `ContentIdController` no longer repeats raw request fields when the canonical result is already present
    - `HashDbController` key-generation/store success payloads no longer echo `filename` or derived `flacKey`
    - `MeshController` lookup success/not-found payloads no longer echo the queried `flacKey`
  - Added focused coverage in `ContentIdControllerTests`, `HashDbControllerTests`, and `MeshControllerTests`
  - Confirmed the focused response-contract slice passed (`30/30`) and the runtime build remains green (`0 warnings / 0 errors`)
  - Added the corresponding gotcha to `adr-0001-known-gotchas.md` and will commit it immediately per repo policy
  - Hardened another lookup/list response batch:
    - `HashDbController` list envelopes no longer mirror `size` / `sinceSeq` when the returned data already carries the real result
    - `IpldController` inbound-link lookups no longer mirror `targetContentId` / `linkName`
  - Added focused coverage in `HashDbControllerTests` and `IpldControllerTests`
  - Confirmed the touched runtime build remains green (`0 warnings / 0 errors`) and the focused controller slices pass individually
  - Added the corresponding gotcha to `adr-0001-known-gotchas.md` and will commit it immediately per repo policy
  - Hardened another admin-action response contract:
    - `AnalyzerMigrationController` no longer mirrors `targetVersion` / `force` in success payloads
  - Added focused coverage in `AudioBoundaryControllerTests`
  - Confirmed the touched runtime build remains green (`0 warnings / 0 errors`) and the focused audio boundary slice passes
  - Added the corresponding gotcha to `adr-0001-known-gotchas.md` and will commit it immediately per repo policy
  - Hardened another release-facing boundary cluster:
    - `DhtRendezvousController` unblock failures no longer echo raw blocklist `type` / `target` values
    - added focused not-found / invalid-type coverage for DHT rendezvous, port forwarding, and pod-channel controller misses
  - Repaired a runtime build regression in `DhtMeshServiceDirectory` by converting init-only descriptor normalization back to immutable `with` copies
  - Updated `DhtMeshServiceDirectoryTests` to match the immutable descriptor model shape
  - Confirmed the runtime project is green again (`0 warnings / 0 errors`)
  - Narrowed the remaining unit-project compile blockers to:
    - `SearchActionsControllerTests`
    - `MeshContentMeshServiceTests`
    - `PodMessageBackfillControllerTests`
  - Updated the existing init-only record gotcha in `adr-0001-known-gotchas.md` and committed it immediately per repo policy
  - Fixed another controller-boundary leak cluster:
    - `MultiSourceController` no longer reflects usernames or exact source-count thresholds in user-miss / insufficient-source replies
    - `RelayController` no longer echoes missing relay agent names in stream lookup failures
    - `EventsController` no longer enumerates allowed values or echoes raw event-type strings in invalid event replies
    - `ReportsController` no longer exposes enum-name lists in invalid direction/sort validation failures
  - Added focused regression coverage for those release-facing controller contracts
  - Confirmed the runtime project still builds cleanly (`0 warnings / 0 errors`)
  - Confirmed the remaining unit-project compile errors are pre-existing drift in unrelated tests (`SearchActionsControllerTests`, `MeshContentMeshServiceTests`, `PodMessageBackfillControllerTests`)
  - Added the corresponding gotcha to `adr-0001-known-gotchas.md` and committed it immediately per repo policy
  - Fixed another identity/runtime drift cluster:
    - `SoulseekChatBridge` now normalizes bidirectional Soulseek username <-> Pod peer mappings before storing and looking them up
    - `MeshSyncService` now normalizes inbound response correlation IDs before completing pending waiters
  - Added focused `SoulseekChatBridgeTests` coverage for normalized bridge mapping and backward-compatible `bridge:` extraction
  - Fixed a file-safety / transfer-status result cluster:
    - `PathGuard` no longer echoes raw path exceptions in validation results
    - both `ContentSafety` implementations no longer echo raw file-read exceptions
    - `MeshTransferService` no longer copies raw exception text into transfer status
  - Added focused regression coverage for those sanitized helper/result contracts
  - Added the corresponding gotcha to `adr-0001-known-gotchas.md` and committed it immediately per repo policy
  - Fixed another runtime/status sanitization cluster:
    - `LibraryHealthService` no longer persists raw exception text into scan state or emitted corrupted-file issue reasons
    - `ContentVerificationService` no longer returns raw verification exception text in failed-source results
  - Added focused regression coverage for those sanitized scan/verification failure contracts
  - Added the corresponding gotcha to `adr-0001-known-gotchas.md` and committed it immediately per repo policy
  - Fixed another mesh/runtime status sanitization cluster:
    - `MeshContentFetcher` no longer returns raw mesh client exception text in fetch results
    - `MeshSyncService` no longer copies raw sync exceptions into `MeshSyncResult.Error`
    - `SwarmDownloadOrchestrator` no longer copies raw exception text into job/chunk status errors
  - Added focused regression coverage for the mesh fetch/sync failure contracts and documented the recurring pattern in `adr-0001-known-gotchas.md`
  - Fixed another protocol/status sanitization cluster:
    - `BridgeProxyServer` no longer sends raw exception text back over the bridge protocol for generic internal errors or failed download requests
    - `MeshHealthCheck` no longer embeds raw exception text in degraded health descriptions
    - `MeshCircuitBuilder` no longer stores raw exception text in hop status records
  - Documented the recurring pattern in `adr-0001-known-gotchas.md`
  - Fixed another helper/batch result cluster:
    - `RegressionHarness` no longer copies raw exception text into scenario/test/benchmark results
    - `AutoReplaceService` no longer puts raw exception text into per-download batch details
    - `Dumper` no longer returns raw dump-creation exception text in its result tuple
  - Added focused harness coverage and documented the recurring pattern in `adr-0001-known-gotchas.md`
  - Fixed another diagnostics/runtime contract cluster:
    - anonymity transports no longer store raw exception text in `LastError`
    - `HttpLlmModerationProvider` no longer exposes raw HTTP exception text in moderation responses or provider health
    - `SongIdService` no longer stores raw exception text in run summaries/evidence for analysis and auxiliary pipeline skips
  - Added focused coverage in transport tests, moderation tests, and `SongIdServiceTests`, and documented the recurring pattern in `adr-0001-known-gotchas.md`
  - Fixed another infrastructure boundary cluster:
    - `ValidateCsrfForCookiesOnlyAttribute` no longer copies raw exception text into `ProblemDetails.Detail`
    - `MeshServiceDescriptorValidator` no longer returns raw serializer exception text in its validation tuple
  - Folded in the adjacent dirty cleanup already in the tree for `Dumper`, `LibraryHealthService`, and `SongIdService`
  - Documented the recurring pattern in `adr-0001-known-gotchas.md`
  - Fixed a DHT/mesh security-helper result cluster:
    - `PeerVerificationService` no longer returns raw Soulseek/transport exception text in verification results
    - `DnsLeakPreventionVerifier` no longer returns raw socket/transport exception text in verification or leak-test results
  - Added focused unit coverage for those sanitized security-helper failure contracts
  - Added the corresponding gotcha to `adr-0001-known-gotchas.md` and committed it immediately per repo policy
  - Fixed a VirtualSoulfind v2 result-contract cluster:
    - `SimpleResolver` no longer copies raw exception text into `PlanExecutionState` or `StepResult`
    - `HttpBackend` and `WebDavBackend` no longer echo raw `HttpRequestException` messages in validation results
  - Added focused unit coverage for those sanitized VSF v2 failure contracts and folded in adjacent dirty mesh service test/code changes already in the tree
  - Added the corresponding gotcha to `adr-0001-known-gotchas.md` and committed it immediately per repo policy
  - Fixed another PodCore/MediaCore runtime cluster:
    - `MetadataPortability` now registers imported entries with normalized external IDs instead of raw domain values
    - `PodMessageRouter` now deduplicates/normalizes target peer IDs and no longer counts privacy-batched payloads as already routed
    - `PodOpinionService` now upserts per-sender/per-variant opinions instead of appending duplicates forever, and refresh counts now track real deltas
    - `PodJoinLeaveService` no longer fabricates empty pending-request buckets on read/cancel helpers and now compares peer IDs consistently
  - Added the corresponding gotcha to `adr-0001-known-gotchas.md` and committed it immediately per repo policy
  - Added a reusable Nix package/module smoke:
    - new `packaging/scripts/run-nix-package-smoke.sh`
    - builds `.#default`
    - launches the packaged `bin/slskd`
    - evaluates the minimal `services.slskd` NixOS module contract with the required `domain`, `environmentFile`, and `settings.shares.directories = [ ]` inputs
  - Wired the Nix smoke into both `ci.yml` and `build-on-tag.yml`, including a post-stable-metadata-update smoke in the main-channel tag workflow
  - Added a real subpath-hosted web smoke:
    - new `src/web/scripts/smoke-subpath-build.mjs`
    - wired into `packaging/scripts/run-release-gate.sh`
    - serves the built UI under `/slskd/` and verifies the relative asset URLs actually load from that mount point
  - Updated `docs/dev/release-checklist.md` and `docs/dev/testing-policy.md` so the release gate documentation includes the new subpath-hosted smoke step
  - Added an explicit release-surface integration smoke gate:
    - new `packaging/scripts/run-release-integration-smoke.sh`
    - wired into `packaging/scripts/run-release-gate.sh`
    - covers `LoadTests`, `DisasterModeIntegrationTests`, `SoulbeetAdvancedModeTests`, `CanonicalSelectionTests`, and `LibraryHealthTests`
  - Added `docs/dev/release-checklist.md` so local release readiness, tag-triggered builds, and remaining limits are documented in one place
  - Updated `docs/dev/testing-policy.md` so the documented release gate matches the actual release gate
  - Validation is green:
    - `bash packaging/scripts/run-release-gate.sh` passed
    - `bash ./bin/lint` passed

---

## Recent Context

### Last Session Summary
- Completed Phase 1 (MusicBrainz/Chromaprint integration) T-300 through T-313
- Implemented Phase 2A tasks T-400 through T-403 (AudioVariant, canonical scoring, library health scaffolding)
- **Extended planning with critical additions**:
  - **Phase 2-Extended**: Codec-specific fingerprinting & quality heuristics (T-420 to T-430)
    - FLAC 42-byte streaminfo hash + PCM MD5
    - MP3 tag-stripped stream hash + encoder detection
    - Opus/AAC stream hashes + spectral analysis
    - Cross-codec deduplication via audio_sketch_hash
    - Heuristic versioning & recomputation system
  - **Phase 7**: Testing strategy with Soulfind & mesh simulator (T-900 to T-915)
    - L0/L1/L2/L3 test layers
    - Soulfind test harness (dev-only, never production)
    - Multi-client integration tests (Alice/Bob/Carol topology)
    - Mesh simulator for disaster mode testing
    - CI/CD integration with test categorization
- **Previously completed comprehensive planning for ALL phases (2-6)**:
  - Phase 2: 6 documents (~25,000 lines) - Canonical scoring, Library health, Swarm scheduling, Rescue mode, Codec-specific fingerprinting
  - Phase 3: 1 document (4,100 lines) - Discovery, Reputation, Fairness
  - Phase 4: 1 document (3,200 lines) - Manifests, Session traces, Advanced features
  - Phase 5: 1 document (2,800 lines) - Soulbeet integration
  - Phase 6: 4 documents (11,200 lines) - Virtual Soulfind mesh, disaster mode, compatibility bridge
  - Phase 7: 1 document (6,500 lines) - Testing strategy
- **Total: 21 planning documents, ~57,000 lines of production-ready specifications**
- **Total tasks: 127 (T-300 to T-915, plus misc)**

### Blocking Issues
- None currently

### Current focus (the rest)
- **40-fixes plan (PR-00–PR-14):** Done. slskd.Tests 46, slskd.Tests.Unit 2257 pass; Epic implemented. Deferred table: status only.
- **T-404+:** Done. t410-backfill-wire (RescueMode underperformance detector → RescueService); codec/fingerprint (T-420–T-430) done per dashboard.
- **slskd.Tests.Unit re-enablement:** ✅ **COMPLETE** (2026-01-27): All phases (0-5) done. 2430 tests passing, 0 skipped, 0 failed. No `Compile Remove` remaining. All test files enabled and passing per `docs/dev/slskd-tests-unit-completion-plan.md`.
- **New product work**: As prioritized.

**Research (9) implementation:** ✅ Complete. T-901–T-913 all done per `memory-bank/tasks.md`.

### Next Steps
1. Continue bughunting adjacent long-lived startup/shutdown helpers that still launch detached work from mutable shared fields.
2. Keep future broad bughunt work behind the real validation gates (`dotnet test --no-restore`, `./bin/lint`), not just focused slices.
3. Continue broad bughunt work only from this validated green head.


4. **Recent completions** (2026-01-27):
   - ✅ Backfill for shared collections (API + UI, supports HTTP and Soulseek)
   - ✅ Persistent tabbed interface for Chat (Rooms already had tabs)
   - ✅ E2E test completion (policy, streaming, library, search)
   - ✅ Code cleanup: TODO comments updated to reference triage document
   - ✅ Soulfind integration: CI and local build workflows integrated
   - ✅ Soulbeet compatibility tests: Fixed 2 failing tests (JSON property names, Directories config)
   - ✅ Phase 2 Multi-Swarm: Implemented Phase 2B deep library health scanning (T-403), verified Phase 2A/2C/2D complete
   - ✅ Phase 3 Multi-Swarm: Verified all 11 tasks (T-500 to T-510) complete
   - ✅ Phase 4A-4C Multi-Swarm: Verified 9 of 12 tasks (T-600 to T-608) complete
   - ✅ Phase 4D Multi-Swarm: **COMPLETED** (T-609 to T-611) - Full playback-aware chunk priority integration
   - ✅ Phase 5 Multi-Swarm: Verified all 13 tasks (T-700 to T-712) complete

**Multi-Swarm Status**: 62 of 62 tasks complete (100%). All Phases 1-5 fully implemented and verified.

5. ~~**High Priority Tasks Available** (obsolete):~~
   - **Packaging**: T-010 to T-013 (NAS/docker packaging - 4 tasks)
   - **Medium Priority**: T-003 (Download Queue Position Polling), T-004 (Visual Group Indicators)
   - ~~T-404+~~ (done)

5. ~~**Implementation Timeline**~~ (archived; Phase 14 and T-404+ done.)

6. ~~**Branch Strategy**: Phase 14 `experimental/pod-vpn`~~ (Phase 14 done.)

---

## Environment Notes

- **Backend Port**: 5030 (default)
- **Frontend Dev Port**: 3000 (CRA default)
- **.NET Version**: 8.0
- **Node Version**: Check `package.json` engines

---

## Quick Commands

```bash
# Start backend (watch mode)
./bin/watch

# Start frontend dev server
cd src/web && npm start

# Run all tests
dotnet test

# Build release
./bin/build
```

## 2026-03-22 17:28
- Folded in remaining dirty share/validator spillover and restored a clean head.
- Next: continue broad bughunt from the remaining runtime/read-side clusters.

## 2026-03-22 17:34
- Sanitized remaining config-validation and secure-framer parser leakage surfaces.
- Next: continue broad bughunt through remaining externally visible result/validation objects rather than pure logs.

## 2026-03-22 17:40
- Sanitized mesh protocol and service-fabric reply contracts that were still reflecting validator details and method/service names.
- Next: continue bughunt through remaining observable validation/result objects, especially shared validation attributes and service reply builders.

## 2026-03-22 17:46
- Sanitized shared file/directory validation attributes so they no longer expose absolute paths through validation failures.
- Next: continue through remaining shared validation and result-builder surfaces that may still leak input details.

## 2026-03-22 17:49
- Folded in dirty moderation/detail-map spillover and restored the metrics validation test path drift.
- Next: continue bughunt from the next clean head after recommitting the remaining dirty files.

## 2026-03-22 17:53

## 2026-03-22 18:47
- Fixed another older-controller normalization cluster:
  - `src/slskd/Shares/API/Controllers/SharesController.cs`
  - `src/slskd/Transfers/MultiSource/API/PlaybackController.cs`
  - `src/slskd/Transfers/MultiSource/API/TracingController.cs`
- These endpoints now trim route/body IDs before lookup/dispatch and reject blank IDs up front, which closes the older low-traffic controller path where padded IDs could miss existing records or split queue/state keys.
- Added focused regressions in:
  - `tests/slskd.Tests.Unit/Shares/API/Controllers/SharesControllerTests.cs`
  - `tests/slskd.Tests.Unit/Transfers/MultiSource/API/PlaybackControllerTests.cs`
  - `tests/slskd.Tests.Unit/Transfers/MultiSource/API/TracingControllerTests.cs`
- Folded in adjacent unit-project compile drift by restoring `using slskd.Jobs;` in `tests/slskd.Tests.Unit/HashDb/HashDbServiceTests.cs`.
- Gotcha commit made immediately per repo policy:
  - `7e61bb61` `docs: Add gotcha for low-traffic controller id normalization`
- Validation:
  - `dotnet build src/slskd/slskd.csproj -v q` -> `0 warnings / 0 errors`
  - `dotnet build tests/slskd.Tests.Unit/slskd.Tests.Unit.csproj -v q` -> `526 warnings / 0 errors`
  - direct `vstest` slice for the 5 new controller tests -> `Passed: 5, Failed: 0`
- Next:
  1. Continue the broad controller-edge sweep through older/secondary API surfaces that still validate presence but do not canonicalize route/body strings.
  2. Prefer grouped passes where a controller family has parallel “busy” and “low-traffic” endpoints with inconsistent normalization rules.

## 2026-03-22 19:03
- Fixed a grouped MediaCore/HashDb normalization cluster:
  - `src/slskd/MediaCore/API/Controllers/DescriptorRetrieverController.cs`
  - `src/slskd/MediaCore/API/Controllers/MetadataPortabilityController.cs`
  - `src/slskd/HashDb/API/HashDbController.cs`
- These endpoints now canonicalize transport identifiers before crossing into service logic:
  - batch `ContentIds` are trimmed, blank-filtered, and deduplicated
  - `contentId`, `domain`, `type`, `flacKey`, `filename`, and `byteHash` are trimmed before lookup or hash generation
- Added focused regressions in:
  - `tests/slskd.Tests.Unit/MediaCore/DescriptorRetrieverControllerTests.cs`
  - `tests/slskd.Tests.Unit/MediaCore/MetadataPortabilityControllerTests.cs`
  - `tests/slskd.Tests.Unit/HashDb/API/HashDbControllerTests.cs`
- Gotcha commit made immediately per repo policy:
  - `8af03b5e` `docs: Add gotcha for batch and query identifier normalization`
- Validation:
  - `dotnet build tests/slskd.Tests.Unit/slskd.Tests.Unit.csproj -v q` -> `526 warnings / 0 errors`
  - direct `vstest` slice for the 8 new controller tests -> `Passed: 8, Failed: 0`
  - `dotnet build src/slskd/slskd.csproj -v q` -> `0 errors`, with the standing `PodMessageBackfill` style warning and a transient copy-retry warning because the DLL was in use
- Next:
  1. Continue grouped controller-edge passes where batch/query endpoints still pass raw identifier collections into services.
  2. Either clear the unrelated `PodMessageBackfill.cs` style warning or work around the in-use runtime DLL when taking the runtime build back to `0/0`.

## 2026-03-22 19:18
- Fixed a grouped MultiSource / VirtualSoulfind v2 controller-normalization cluster:
  - `src/slskd/Transfers/MultiSource/API/MultiSourceController.cs`
  - `src/slskd/VirtualSoulfind/v2/API/VirtualSoulfindV2Controller.cs`
- The batch normalized parallel endpoint families rather than single methods:
  - MultiSource search/verify/download/test/search-user endpoints now trim shared transport strings and normalize per-source request fields before service calls.
  - VirtualSoulfind v2 intent/catalogue endpoints now trim route and request IDs before queue/catalogue/planner/processor dispatch.
- Added focused regressions in:
  - `tests/slskd.Tests.Unit/Transfers/MultiSource/API/MultiSourceControllerTests.cs`
  - `tests/slskd.Tests.Unit/VirtualSoulfind/v2/API/VirtualSoulfindV2ControllerTests.cs`
- Gotcha commit made immediately per repo policy:
  - `7ce8a5c2` `docs: Add gotcha for parallel endpoint family normalization`
- Validation:
  - `dotnet build tests/slskd.Tests.Unit/slskd.Tests.Unit.csproj -v q` -> `556 warnings / 0 errors`
  - direct `vstest` slice for the 9 new controller tests -> `Passed: 9, Failed: 0`
  - `dotnet build src/slskd/slskd.csproj -v q` -> `0 errors`, with current warning noise coming from already-dirty nullable/style areas plus a transient copy-retry warning while the DLL is in use
- Next:
  1. Continue grouped controller-edge passes where sibling endpoints still drift on normalization rules.
  2. Decide whether to spend the next pass paying down the now-visible runtime warning floor (`HashDbService`, `SongIdService`, `PodMessageBackfill`) or keep prioritizing more request-boundary bugs first.
- Sanitized certificate validation and mesh-sync fallback messages that were still exposing internal parser/runtime state.
- Next: continue the broader bughunt from the next clean head, focusing on remaining placeholder/null-heavy runtime paths rather than message leakage.

## 2026-03-22 19:22
- Continued the placeholder/null-heavy completion pass instead of another pure sanitization pass.
- `SongIdService` now keeps conservative recognizer/corpus evidence alive when exact fingerprint/path payloads are missing:
  - SongRec can fall back to textual external IDs
  - Panako/Audfprint can emit conservative findings from textual labels
  - corpus reuse can now fall back to recording/title/artist metadata similarity when exact fingerprint files are unavailable
- `HashDbService` helper/update paths now normalize issue IDs, MusicBrainz IDs, and variant metadata before writes so older helper methods stop drifting from the hardened main lookup paths.
- `PodMessageBackfill` success-path wording now matches reality better while the transport receive seam remains a larger feature gap.
- Gotcha commit made immediately per repo policy:
  - `35c4fe52` `docs: Add gotcha for songid corpus metadata fallback`
- Next:
  1. recommit the full dirty tree, including adjacent controller/test files from parallel work
  2. continue the same three-way pass with the next dense `SongIdService` and `HashDbService` bottom-out cluster

## 2026-03-22 19:31
- Continued the same three-way pass:
  - `SongIdService` parser helpers now tolerate stringified tool payloads and alternate Spotify track forms
  - `HashDbService.TryResolveAcoustIdAsync(...)` now normalizes helper inputs before lookup/tagging
  - `PodMessageBackfill` now excludes self using canonical peer IDs and skips blank message/sender identities during response processing
- Gotcha commit made immediately per repo policy:
  - `9b38d978` `docs: Add gotcha for stringified parser payloads`
- Next:
  1. recommit the full dirty tree, including adjacent controller files from parallel work
  2. continue deeper into the remaining `SongIdService` and `HashDbService` bottom-out cluster

## 2026-03-22 19:41
- Continued the deeper `SongIdService` + `HashDbService` cluster:
  - SongID segment-query planning now uses transcript phrases and OCR text instead of ignoring those collected findings
  - HashDb album target and canonical stats write/read paths now normalize more of their key/value fields symmetrically
- Added focused unit coverage for:
  - transcript/OCR-derived segment queries
  - stringified helper payload parsing
  - trimmed album target persistence
  - trimmed canonical stats persistence
- Gotcha commit made immediately per repo policy:
  - `8ebb6a04` `docs: Add gotcha for unused songid evidence`
- Next:
  1. recommit the full tree
  2. continue the next `SongIdService` + `HashDbService` batch after this clean head

## 2026-03-22 19:49
- Continued the same `SongIdService` + `HashDbService` batch again:
  - SongID fallback search now uses transcript/OCR/comment evidence, not just top-level metadata
  - HashDb job/warm-cache row-fallback readers now trim returned values so readback matches the hardened write-side behavior
- Added focused coverage for those fallback/read-side paths.
- Next:
  1. recommit the full tree
  2. continue the remaining `SongIdService`/`HashDbService` cluster, then move back into Mesh/PodCore runtime gaps

## 2026-03-22 20:02
- Returned to the Mesh/PodCore runtime slice:
  - `PeerResolutionService` now supports trimmed hostname endpoints from DHT metadata
  - `PodDiscovery` now normalizes returned metadata before filtering and deduping
  - `MeshSyncService` now trims remote hash keys before consensus grouping
- Added focused coverage for the PodCore runtime behaviors above.
- Gotcha commit made immediately per repo policy:
  - `4bcb1cb9` `docs: Add gotcha for dht metadata normalization`
- Next:
  1. recommit the full tree
  2. continue the remaining Mesh/PodCore runtime gap after this clean head

## 2026-03-22 18:01
- Normalized PodCore peer/pod read paths and VSF source-registry reads so whitespace drift and blank persisted keys no longer under-report available state.
- Next: keep widening through remaining placeholder/null-heavy runtime paths in PodCore, VirtualSoulfind, and Mesh.

## 2026-03-22 18:08
- Replaced the reachable empty-result placeholder in `ContentLinkService` with the existing MusicBrainz audio search integration and folded in the current dirty spillover batch.
- Next: continue through the remaining placeholder/null-heavy runtime paths from a clean head.

## 2026-03-22 18:14
- Created a tracked placeholder/null-heavy inventory and grouped the remaining work into batches.
- Next: execute Batch A (`SongID` + `MetadataFacade`) or Batch B (`MeshSync` + transport/runtime fallback completion).

## 2026-03-22 18:12
- Sanitized `X509.TryValidate(...)`, the multi-source `RunTest(...)` endpoint error field, and the `SecurityMiddleware` catch-path event message so those helper/diagnostic surfaces no longer relay raw nested exception text.
- Folded in adjacent unit drift fixes in `MeshSyncSecurityTests` and `ContentLinkServiceTests`, and documented the recurring pattern in `adr-0001-known-gotchas.md` with an immediate docs-only commit.
- Next: continue the broad bughunt from the current head, prioritizing other helper/result contracts that still look diagnostic but cross an observable boundary.

## 2026-03-22 18:14
- Normalized controller-edge strings in `BackfillController`, the small audio controllers, and `UserNotesController` so whitespace-only or padded IDs no longer reach the service layer as distinct keys or bad arguments.
- Added focused regression coverage for the normalized/blank-input paths and revalidated the batch with a clean runtime build, targeted unit slice, and lint.
- Next: keep widening the bughunt through other low-traffic controllers and helper endpoints that still look like thin pass-throughs and may be skipping controller-edge normalization.

## 2026-03-22 18:23
- Normalized `SongIdController`, `StreamsController`, and `SolidController` so route/query/body values are trimmed before dispatch and blank values fail as `400`s instead of leaking into service-layer behavior.
- Fixed a separate compile blocker in `MetadataFacade` caused by reused local names during a fallback-refactor, and documented both patterns immediately in `adr-0001-known-gotchas.md`.
- Next: continue through the next thin-controller/helper batch, with emphasis on native utility controllers and other low-traffic endpoints that still bypass controller-edge canonicalization.

## 2026-03-22 18:29
- Normalized nested request fields in `ProfileController` and `ContactsController` so optional strings and endpoint collections are canonicalized before the service layer sees them.
- Added focused regression coverage for trimmed invite/nickname/peerId/display-name flows and for rejecting whitespace-only contact updates.
- Next: continue through the next low-traffic controller/helper batch, especially native utility endpoints and messaging controllers that still accept raw strings in route/body pairs.

## 2026-03-22 18:36
- Aligned the specialized discography/label-crate job controllers with the native jobs API so the whole job domain now applies the same controller-edge normalization rules.
- Restored the runtime build to `0 warnings / 0 errors` after the batch by matching the existing non-nullable `TargetDirectory` contract.
- Next: continue through the next controller/helper cluster, likely messaging or telemetry endpoints that still mix route/body strings and older pass-through patterns.

## 2026-03-22 18:22
- Executed Batch A against `SongIdService`, `MetadataFacade`, and `MusicBrainzClient`.
- Replaced one major early-bottom-out pattern: metadata hits without MBIDs now continue through SongID with conservative synthetic IDs instead of being discarded.
- Added new focused unit coverage for metadata search fallback, filename-derived local-file metadata, and trimmed/deduplicated MusicBrainz search results.
- Next: continue Batch A deeper into the remaining `SongIdService` / `SongIdScoring` helper paths, then take Batch B (`MeshSync` + transport/runtime fallback completion).

## 2026-03-22 18:31
- Continued Batch A deeper into SongID helper/scoring paths.
- Fixed helper/path normalization around Panako/Audfprint discovery, corpus fingerprint resolution, excerpt start determinism, and loose-text scoring equivalence.
- Next: keep pushing through the remaining `SongIdService` bottom-out paths or switch to Batch B (`MeshSync` + transport/runtime fallback completion).

## 2026-03-22 18:42
- Started Batch B with `MeshSyncService`.
- Fixed two concrete under-reporting/runtime issues: duplicate in-flight request failure and impossible small-mesh quorum requirements.
- Next: continue Batch B through `CapabilityFileService`, `MeshOverlayConnector`, and `StunNatDetector`, then circle back for any remaining deep `SongIdService` bottom-out paths.

## 2026-03-22 18:55
- Continued Batch B through `CapabilityFileService`, `MeshOverlayConnector`, and `StunNatDetector`.
- Fixed capability/endpoint normalization, preserved partial capability identity, restored the live connection return in mesh overlay connect, and added IPv6 STUN mapped-address parsing.
- Next: inspect the remaining dirty tree, commit everything, then keep pushing through `MeshOverlayConnector` / transport fallback paths or circle back into the next dense `SongIdService` cluster.

## 2026-03-22 19:07
- Switched into the next dense HashDb completion slice.
- Fixed symmetric key normalization for HashDb recording/job identifiers and added focused readback regression coverage.
- Next: commit the current tree, then continue into the next HashDb null-heavy read path or return to the remaining Mesh/PathGuard clusters.

## 2026-03-22 19:21
- Tightened the next security/helper cluster by routing the weaker DHT rendezvous `PathGuard` through the hardened shared `Common.Security.PathGuard` instead of leaving two divergent implementations in the tree.
- Expanded `HttpSignatureKeyFetcher` so it trims `keyId`, rejects oversized responses earlier, and can extract PEM keys from either top-level key documents or actor `publicKey` arrays.
- Folded in adjacent dirty controller work for destinations/wishlist boundary validation and prepared focused regressions for the new pathguard/key-fetch behaviors.
- Next: commit the current tree including all dirty files, then continue through the next placeholder/null-heavy Mesh/PodCore runtime cluster.

## 2026-03-22 19:33
- Continued into the PodCore completion batch and replaced another enrichment-coupling bottom-out in `ContentLinkService`.
- Supported domains now return conservative metadata when enrichment is missing, and audio-artist lookups opportunistically upgrade that metadata from existing MusicBrainz search hits.
- Next: keep pushing through the remaining PodCore/Mesh placeholder and read-side under-report paths, especially backfill/opinion/runtime helpers that still return less than available local state.

## 2026-03-22 19:42
- Continued deeper into PodCore runtime helpers by normalizing opinion/backfill keys before signature parsing, membership matching, cache tracking, and targeted routing.
- Backfill now fails cleanly on blank target peers instead of pretending to route, and successful sends report an honest “awaiting response handling” status instead of the older placeholder sentence.
- Next: continue through the remaining PodCore/Mesh placeholder paths, especially the larger backfill-response/runtime completion gaps and any adjacent dirty repo spillover.

## 2026-03-22 19:51
- Shifted into the next MeshSync completion slice and normalized peer/key inputs at lookup, request, publish, and peer-state boundaries.
- Mesh hash lookups and chunk requests now reuse the same in-flight waiters even when transport strings arrive padded, instead of splitting one logical request into separate pending keys.
- Next: keep pushing through the remaining Mesh/PodCore placeholder paths, especially larger transport-response completion gaps and any new dirty spillover in the tree.

## 2026-03-22 20:02
- Returned to the Pod backfill flow and fixed the top-level fan-out contract so backfill no longer reports success when every peer request failed.
- Backfill request/response processing now trims pod/channel/peer/message identifiers, deduplicates target peers more safely, and stores normalized messages instead of dropping them on harmless whitespace drift.
- Next: continue through the remaining larger transport-response gaps, especially actual backfill response transport handling and the next Mesh/PodCore placeholder cluster.

## 2026-03-22 20:14
- The actual overlay receive seam for backfill responses is still a larger transport feature, so I pivoted to the next dense local-completion batch in `HashDbService`.
- Tightened the smaller HashDb helper/update/list methods so they use the same identifier normalization rules as the main lookup APIs, closing more whitespace-drift misses at the storage edge.
- Next: commit the current tree including dirty spillover, then continue deeper into the next HashDb/SongID/Mesh completion cluster.

## 2026-03-22 19:47
- Normalized older helper/controller boundaries in `MusicBrainzController`, `DiscoveryGraphController`, `WishlistController`, and `DestinationsController`, and trimmed decoded relative paths in `FilesController`.
- Added focused regressions for those controller/path cases and cleared the adjacent unit-project compile blockers in `HttpSignatureKeyFetcher`, `PathGuardTests`, and several stale test files so focused test slices run cleanly again.
- Validation state:
  - `dotnet build src/slskd/slskd.csproj -v q` passed with `0 warnings / 0 errors`
  - focused unit slices for wishlist/destinations and files/discovery-graph/musicbrainz passed (`4/4` and `15/15`)
  - `dotnet build tests/slskd.Tests.Unit/slskd.Tests.Unit.csproj -v q` passed with pre-existing analyzer warning noise (`521 warnings / 0 errors`)
- Next: continue the broad bughunt through remaining low-traffic controller/helper endpoints and stale unit-test drift that still hides behind the unit-project warning wall.

## 2026-03-22 20:01
- Normalized parser discriminator strings in `CapabilitiesController`, `PerceptualHashController`, and `NowPlayingController` before parser dispatch, enum parsing, and webhook event classification.
- Added focused regressions for trimmed capability tags, trimmed perceptual-hash algorithms, and trimmed generic now-playing webhook events.
- Validation state:
  - `dotnet build src/slskd/slskd.csproj -v q` passed with `0 warnings / 0 errors`
  - focused parser/controller slice passed (`8/8`)
- Next: continue through the next low-traffic helper/controller batch, especially older utility endpoints that still branch on raw strings or carry adjacent test drift.

## 2026-03-22 20:26
- Recovered the runtime release gate after an immutable-model regression in `HashDbService`.
- Album target and canonical-stats normalization now respect `init`-only records by normalizing through local `with` copies, and the adjacent SongID / MultiSource nullable DTO warnings are cleaned up too.
- Validation state:
  - `dotnet build src/slskd/slskd.csproj -v q` passed with `0 warnings / 0 errors`
- Next: continue the secure-release bughunt from externally visible mesh/result contracts rather than local compile hygiene.

## 2026-03-22 20:34
- Hardened the next release-facing mesh reply cluster:
  - service-fabric mesh services no longer echo caller-controlled method names
  - private-gateway DNS failures no longer relay downstream validator text
  - VirtualSoulfind mesh batch validation no longer echoes rejected MBID values
- Focused validation passed:
  - `dotnet build src/slskd/slskd.csproj -v q`
  - `dotnet build tests/slskd.Tests.Unit/slskd.Tests.Unit.csproj -v q` with existing analyzer warning noise only
  - `vstest` slice for `PrivateGatewayMeshServiceTests`, `PodsMeshServiceTests`, `MeshContentMeshServiceTests`, and `VirtualSoulfindMeshServiceTests` passed (`20/20`)
- Next: continue through the next secure-release surfaces, prioritizing remaining externally visible mesh/service replies and helper result DTOs over internal-only logging cleanup.

## 2026-03-22 20:43
- Continued through adjacent infrastructure-facing result surfaces after the first mesh reply batch.
- Mesh introspection and DHT method-not-found replies now use the same generic contract, mesh-content no longer exposes local file sizes in oversized-file errors, and realm migration import failures no longer return absolute filesystem paths.
- Focused validation passed:
  - `dotnet build src/slskd/slskd.csproj -v q`
  - `vstest` slice for `MeshIntrospectionServiceTests`, `DhtMeshServiceTests`, `MeshContentMeshServiceTests`, and `RealmMigrationToolTests` (`17/17`)
- Next: keep pushing toward a fixed secure release by auditing the next layer of externally visible result DTOs and mesh/service error objects before spending time on purely internal analyzer noise.

## 2026-03-22 20:51
- Continued into the next mesh reply-category pass.
- Private-gateway quota errors no longer disclose configured numeric thresholds, and DHT / hole-punch invalid-payload replies no longer teach callers the exact field names or byte-count expectations.
- Focused validation passed:
  - `dotnet build src/slskd/slskd.csproj -v q`
  - `vstest` slice for `DhtMeshServiceTests`, `HolePunchMeshServiceTests`, and `RateLimitTimeoutTests` (`16/16`)
- Next: continue the release-facing audit through the next layer of externally visible DTOs and controller/problem responses that still reflect raw identifiers, indexes, or object-not-found specifics.

## 2026-03-22 20:59
- Continued into the search action-routing surface.
- `SearchActionsController` no longer echoes raw search IDs, response/file indexes, item IDs, or content IDs inside `ProblemDetails.Detail` for action-routing failures.
- Focused validation passed:
  - `dotnet build src/slskd/slskd.csproj -v q`
  - `vstest` slice for `SearchActionsControllerTests` (`11/11`)
- Next: keep pushing through the next controller/result boundary layer, especially older NotFound/Problem payloads that still include raw route IDs or lookup specifics in otherwise public contracts.

- Replaced placeholder-success controller/introspection responses with real local-state answers in PodCore and Mesh Service Fabric.

- Hardened active mesh service adapters so embedded Pod/DHT IDs are canonicalized before crossing into pod or routing-table logic.

## 2026-03-22 22:05
- Continued the fastest-path Pod/Mesh runtime completion batch instead of widening back out to unrelated surfaces.
- Hardened DHT-backed discovery and peer-resolution behavior:
  - `DhtMeshServiceDirectory` now normalizes both lookup keys and returned descriptor payloads before validation/dedupe.
  - `PodDiscovery` now falls back to indexed pod IDs when direct metadata lookup misses on harmless ID drift.
  - `PeerResolutionService` now reuses cached username aliases and canonical metadata peer IDs more consistently.
  - `MeshContentMeshService` now trims content IDs and rejects invalid ranges at the adapter boundary.
- Dirty repo spillover is being committed together with this batch per user instruction.
- Next: finish the remaining Pod/Mesh runtime seams from the placeholder inventory, then rerun full validation and the release gate.

## 2026-03-22 22:18
- Finished another grouped Pod/Mesh runtime completion batch.
- `PodAffinityScorer` now consumes real membership-history signals for trust/stability instead of placeholder trust heuristics.
- `MeshStatsCollector` now resolves live in-memory DHT state through the concrete runtime service shape or its aliases.
- `MeshServiceClient` now normalizes service/method/correlation inputs and selects the freshest valid provider deterministically.
- Next: commit all dirty files in the repo, then run full validation and the release gate.

## 2026-03-22 21:49 CST
- Current task: finish release validation and stop widening scope.
- Confirmed the remaining blocker is in the unit-test project, not the runtime build or frontend gate stages.
- Fixed one concrete release blocker:
  - `tests/slskd.Tests.Unit/HashDb/API/HashDbControllerTests.cs` now uses `FlacInventoryEntry.Path` instead of removed `Filename`.
- Next steps:
  - rerun `dotnet test tests/slskd.Tests.Unit/slskd.Tests.Unit.csproj -c Release`
  - rerun `bash packaging/scripts/run-release-gate.sh`
  - fix only the next blocker if either still fails

## 2026-03-22 22:26 CST
- Release-blocker triage is complete.
- Confirmed green validation path:
  - `dotnet build src/slskd/slskd.csproj -v q`
  - `dotnet test tests/slskd.Tests.Unit/slskd.Tests.Unit.csproj -c Release`
  - `bash ./bin/lint`
  - `bash packaging/scripts/run-release-gate.sh`
- Next steps:
  - stop widening the code diff unless a new release blocker is found
  - if desired, prepare the branch for release/tagging or produce a final release checklist

## 2026-03-22 22:59 CST
- Current task: clear the GitHub-only `Nix Package Smoke` failure from the last release tag.
- Confirmed Actions run `23420738086` failed in `Nix Package Smoke`, while the main release gate job passed.
- Root cause:
  - `packaging/scripts/run-nix-package-smoke.sh` injected `${ROOT@Q}` into a multiline `nix eval --expr`
  - GitHub's Linux runner parsed that as invalid Nix syntax
- Fix applied:
  - the Nix expression now resolves the repo flake via `toString ./.`
- Next steps:
  - commit the script fix
  - push it
  - trigger the next release tag build

## 2026-03-23 09:26 CST
- Fixed the first-run Docker startup regression where VSF v2 `LocalLibraryBackend` could not resolve `IShareRepository` from DI.
- Current tree also includes the validated dependency-bump batch for Serilog/OpenTelemetry/AWS/prometheus and frontend `yaml`/`jsdom`/`vite`.
- Next steps:
  - commit the validated tree
  - if desired, rebuild/publish a new image tag from this green head

## 2026-03-23 10:02 CST
- Finished another bughunt batch in the remaining Batch B transport/runtime cluster.
- Current fixes in this batch:
  - capability discovery preserves original remote paths on transfer
  - mesh sync waiter reuse no longer duplicates outbound requests
  - STUN parsing handles padded attributes correctly
  - mesh overlay handshake trims local usernames
- Next steps:
  - commit this validated batch
  - continue into the next placeholder/null-heavy cluster from the inventory

## 2026-03-23 10:23 CST
- Finished the PathGuard / HttpSignatureKeyFetcher hardening pass and validated it green.
- Current fixes in this batch:
  - padded peer paths and extensions are normalized before PathGuard validation/classification
  - HTTP signature key fetching now fails closed for blank or unresolved hosts while still permitting literal-IP HTTPS key IDs
- Next steps:
  - continue the bughunt from the placeholder/null-heavy inventory
  - focus next on remaining `SongIdService`, `HashDbService`, and Mesh/PodCore read-side/runtime bottom-out paths

## 2026-03-23 11:03 CST
- Continued the post-relay lifecycle bughunt into retry infrastructure.
- Fixed `ConnectionWatchdog` so failed reconnect retries dispose the superseded `CancellationTokenSource` immediately instead of leaking one CTS per attempt until shutdown.
- Added focused `ConnectionWatchdogTests` coverage that waits for the second retry CTS and verifies the first one has already been disposed.
- Next steps:
  - continue bughunting adjacent long-lived lifecycle paths after relay/watchdog
  - prioritize services that replace clients, timers, or cancellation sources on option changes or retry loops

## 2026-03-23 11:15 CST
- Continued into timer-driven background callbacks after the retry-loop fix.
- Fixed `VPNService` and `ConnectionWatchdog` so timer events now go through explicit observer wrappers instead of running raw async work directly from the timer callback.
- Added focused `VPNServiceTests` coverage for the misconfigured-VPN polling path and kept the watchdog retry-lifecycle regression green in the same slice.
- Next steps:
  - continue bughunting detached callbacks and long-lived lifecycle paths outside relay/watchdog/vpn
  - prioritize remaining timer/event entry points that can still fault silently or outlive shutdown

## 2026-03-23 11:27 CST
- Continued into the shared scheduler fanout path.
- Fixed `Clock.Fire(...)` so one bad subscriber no longer aborts later subscribers on the same tick.
- Added focused `ClockTests` coverage and kept the earlier watchdog/VPN timer regressions green in the same targeted slice.
- Next steps:
  - continue bughunting shared event/timer fanout and other detached callback entry points
  - prioritize more low-level infrastructure paths where one component failure can suppress unrelated background work

## 2026-03-23 11:37 CST
- Continued into async bus fanout after the clock fix.
- Fixed `SignalBus` so one subscriber failure no longer escapes the signal-delivery path and suppresses healthy subscribers.
- Added focused `SignalBusTests` coverage and kept the surrounding infrastructure regressions green in the same slice.
- Next steps:
  - continue bughunting the remaining shared event emitters and detached callbacks
  - prioritize `SecurityEventSink`, `EntropyMonitor`, `Honeypot`, and related infrastructure fanout paths

## 2026-03-23 11:11 CST
- Finished another green runtime-hardening batch across SongID, HashDb, mesh waiters, and adjacent lifecycle spillover.
- Current fixes in this batch:
  - raw transcript/OCR/comment phrases now survive SongID fallback planning
  - deserialized HashDb job payloads are normalized before readback
  - shared mesh `REQKEY` waiters now have ownership-safe cleanup
  - `ConnectionWatchdog` retry CTS replacement leak was folded in from the dirty tree
- Next steps:
  - continue the placeholder/null-heavy bughunt from the inventory
  - prioritize remaining `HashDbService`, `SongIdService`, and Mesh/PodCore read-side/runtime bottom-out paths

## 2026-03-23 11:29 CST
- Finished another green HashDb helper normalization batch.
- Current fixes in this batch:
  - label presence/release queries now trim and deduplicate consistently
  - peer seq/backfill helpers now trim keys and fail closed on blanks
  - peer metrics now normalize peer IDs on write/readback
  - HashDb read helpers now trim more persisted string fields before returning models
- Next steps:
  - continue into the next `SongIdService` fallback/query-generation cluster
  - then take the next Mesh/PodCore runtime slice from the placeholder inventory

## 2026-03-23 10:41 CST
- Finished another green SongID fallback/query-generation batch.
- Current fixes in this batch:
  - fallback search generation now includes direct title, album+title, and uploader-derived variants
  - Audfprint normalized `0..1` scores are preserved instead of being divided again
  - more real-world comment/source hints now feed SongID fallback planning
- Next steps:
  - continue the remaining `SongIdService` bottom-out paths
  - then take the next Mesh/PodCore runtime slice from the placeholder inventory

## 2026-03-23 10:45 CST
- Finished the live PodCore message-routing fix.
- Current fixes in this batch:
  - production `SqlitePodMessaging` now routes through `IPodMessageRouter` after persistence
  - normal pod sends from API and mesh service paths no longer stop at local SQLite storage
  - direct SQLite messaging tests now cover both routed success and routed failure after persistence
- Next steps:
  - continue the remaining Pod/Mesh runtime seams from the placeholder inventory
  - then re-run the full release gate once the next dense runtime batch lands

## 2026-03-23 10:50 CST
- Finished the PodCore router identity-shape fix.
- Current fixes in this batch:
  - `PodMessageRouter` now uses canonical `PodId` and `ChannelId` fields throughout routing and result reporting
  - the live SQLite-backed pod-send path now reaches a router that understands the actual message contract
  - router tests now cover the modern separated identity shape
- Next steps:
  - continue the remaining Pod/Mesh runtime seams from the placeholder inventory
  - then rerun the release gate after the next dense runtime pass

## 2026-03-23 10:57 CST
- Finished the Pods mesh stream/runtime tranche.
- Current fixes in this batch:
  - `PodsMeshService` streaming is now backed by a real one-shot message stream instead of an immediate close
  - mesh callers can request current pod messages over the stream interface using the same `GetMessagesRequest` contract
  - full-suite test flake from shutdown/socket teardown was hardened while validating the batch
- Next steps:
  - continue the remaining Pod/Mesh runtime seams from the placeholder inventory
  - rerun the release gate after the next dense runtime batch

- 2026-03-23: Completed peer-resolution alias-cache hardening and ChannelReader test collision fix; repo revalidated green. Next Steps: continue bughunt from the remaining Mesh/PodCore runtime inventory.

- 2026-03-23: Completed MeshContent stream handling and repaired SceneServicesTests compile drift; full validation is green again. Next Steps: continue the remaining Mesh/PodCore runtime placeholder inventory.

- 2026-03-23: Completed shadow-index stream handling and MBID boundary normalization; repo is green again. Next Steps: continue the remaining Mesh/PodCore runtime placeholder inventory.

## 2026-03-23 12:10 CST
- Continued the infrastructure bughunt into shared wait queues.
- Fixed `Waiter.CancelAll()` so duplicate waits on the same `WaitKey` are all canceled instead of leaving trailing queued waits behind.
- Added focused `WaiterTests` coverage for duplicate-key and multi-key cancellation, and confirmed the focused slice passed (`2/2`) while the release build stayed green (`0 warnings / 0 errors`).
- Added ADR-0001 gotcha `0k121` and will commit it immediately per repo policy (`docs: Add gotcha for waiter cancel-all contract drift`).
- Next steps:
  - continue bughunting the remaining shared queue, background-lifecycle, and event-fanout helpers
  - prioritize infrastructure APIs whose public bulk-operation contracts may not match their per-key internals

## 2026-03-23 12:18 CST
- Continued the lifecycle sweep into restartable hosted services.
- Fixed `UnderperformanceDetectorHostedService.StartAsync()` so it cancels the previous loop CTS before disposal/replacement; repeated starts no longer leave the prior detector loop alive with an uncanceled token.
- Extended `HostedServiceLifecycleTests` to cover the restart path and confirmed the focused slice passed (`5/5`) while the release build stayed green (`0 warnings / 0 errors`).
- Extended the existing ADR-0001 startup-CTS gotcha entry and will commit it immediately per repo policy (`docs: Add gotcha for startup CTS disposal races`).
- Next steps:
  - continue bughunting restartable hosted/background services that replace long-lived CTS fields
  - then return to the remaining low-level callback and event-fanout helpers

## 2026-03-23 12:33 CST
- Continued the same restart-token sweep into `SoulseekHealthMonitor`.
- Fixed `StartMonitoringAsync()` so it cancels the previous monitoring CTS before disposal/replacement; repeated starts no longer leave the prior health-monitor loop alive with an uncanceled token.
- Extended `HostedServiceLifecycleTests` to cover the monitor restart path and confirmed the focused slice passed (`6/6`), the unit project still builds with `0 errors`, and the release build stayed green (`0 warnings / 0 errors`).
- Extended the existing ADR-0001 startup-CTS gotcha entry and will commit it immediately per repo policy (`docs: Add gotcha for startup CTS disposal races`).
- Next steps:
  - continue bughunting restartable hosted/background services that replace long-lived CTS fields
  - then return to the remaining low-level callback and event-fanout helpers

## 2026-03-23 12:41 CST
- Continued the same restart-token sweep into `RelayClient`.
- Fixed `StartAsync()` so it cancels the previous start/retry CTS before disposal/replacement; repeated starts no longer leave the prior reconnect loop running against a disposed-but-uncanceled token source.
- Extended `RelayClientTests` to cover the restart path and confirmed the focused slice passed (`2/2`) while the release build stayed green (`0 warnings / 0 errors`).
- Extended the existing ADR-0001 startup-CTS gotcha entry and will commit it immediately per repo policy (`docs: Add gotcha for startup CTS disposal races`).
- Next steps:
  - continue bughunting restartable hosted/background services that replace long-lived CTS fields
  - then return to the remaining low-level callback and event-fanout helpers

## 2026-03-23 12:50 CST
- Switched back to low-level timer/callback helpers after the lifecycle sweep.
- Fixed `TimedBatcher` so every path that clears/replaces `_currentBatchTimer` now cancels and disposes the old CTS instead of sometimes dropping it without disposal.
- Extended `TimedBatcherTests` with focused cleanup checks and confirmed the focused slice passed (`25/25`) while the release build stayed green (`0 warnings / 0 errors`).
- Added ADR-0001 gotcha `0k122` and will commit it immediately per repo policy (`docs: Add gotcha for timer-backed batch CTS cleanup`).
- Next steps:
  - continue bughunting the remaining low-level callback, timer, and event-fanout helpers
  - then return to any restartable services that still replace long-lived synchronization primitives unsafely

## 2026-03-23 13:00 CST
- Continued the low-level helper sweep into `TokenBucket`.
- Fixed disposal so pending depleted-bucket waiters are faulted instead of hanging forever after the bucket has been disposed.
- Extended `TokenBucketTests` with a focused blocked-waiter disposal regression and confirmed the focused slice passed (`16/16`) while the release build stayed green (`0 warnings / 0 errors`).
- Added ADR-0001 gotcha `0k123` and will commit it immediately per repo policy (`docs: Add gotcha for token bucket disposal waiters`).
- Next steps:
  - continue bughunting the remaining low-level callback, timer, and event-fanout helpers
  - prioritize helper types that expose internal wait tasks or callback hooks without explicit shutdown behavior

## 2026-03-23 13:10 CST
- Continued the low-level helper sweep into `ManagedState<T>`.
- Fixed disposed listener wrappers so they no-op if invoked from an already-snapped callback list; post-dispose delivery no longer reaches stale listeners during `SetValue(...)` fanout.
- Extended `ManagedStateTests` with a focused snapshot-after-dispose regression and confirmed the focused slice passed (`2/2`) while the release build stayed green (`0 warnings / 0 errors`).
- Added ADR-0001 gotcha `0k124` and will commit it immediately per repo policy (`docs: Add gotcha for managed-state disposed listeners`).
- Next steps:
  - continue bughunting the remaining low-level callback, timer, and event-fanout helpers
  - prioritize primitives where disposal/unsubscription can race with already-snapped background delivery

## 2026-03-23 13:20 CST
- Continued the helper sweep back through `Waiter`.
- Fixed `WaitIndefinitely(...)` so it no longer allocates timeout machinery under the hood; indefinite waits now skip timeout token creation entirely.
- Extended `WaiterTests` with a focused timeout-allocation regression and confirmed the focused slice passed (`3/3`) while the release build stayed green (`0 warnings / 0 errors`).
- Added ADR-0001 gotcha `0k125` and will commit it immediately per repo policy (`docs: Add gotcha for waiter indefinite timeout allocation`).
- Next steps:
  - continue bughunting the remaining low-level callback, timer, and event-fanout helpers
  - prioritize primitives that claim indefinite or disposal-safe behavior but still allocate hidden timers or registrations

## 2026-03-23 13:29 CST
- Continued the low-level helper sweep into `RateLimiter`.
- Fixed disposal so concurrency-limited instances now dispose their owned semaphore instead of leaking it after timer cleanup.
- Extended `CallbackInfrastructureTests` with a focused semaphore-disposal regression and confirmed the focused slice passed (`3/3`) while the release build stayed green (`0 warnings / 0 errors`).
- Added ADR-0001 gotcha `0k126` and will commit it immediately per repo policy (`docs: Add gotcha for rate limiter semaphore cleanup`).
- Next steps:
  - continue bughunting the remaining low-level callback, timer, and event-fanout helpers
  - prioritize helper types that own optional semaphores, locks, or registrations in addition to their primary timer/task

## 2026-03-23 13:39 CST
- Continued the same timer-helper sweep into `TimedCounter`.
- Fixed elapsed callback handling so timer-thread exceptions are isolated and logged instead of escaping the helper directly.
- Extended `CallbackInfrastructureTests` with a focused thrown-callback regression and confirmed the focused slice passed (`5/5`) while the release build stayed green (`0 warnings / 0 errors`).
- Added ADR-0001 gotcha `0k127` and will commit it immediately per repo policy (`docs: Add gotcha for timed counter callback isolation`).
- Next steps:
  - continue bughunting the remaining low-level callback, timer, and event-fanout helpers
  - prioritize detached callback helpers that still invoke user code without an explicit failure surface

## 2026-03-23 13:45 CST
- Closed the outstanding `RateLimiter` follow-up from the same callback/helper sweep.
- Fixed `Dispose()` so a throwing flush action no longer skips timer/semaphore cleanup; owned resources are released first, then the flush exception is rethrown.
- The focused `CallbackInfrastructureTests` slice already covers this path and remains green (`5/5`) while the release build stays green (`0 warnings / 0 errors`).
- Added ADR-0001 gotcha `0k128` and will commit it immediately per repo policy (`docs: Add gotcha for rate limiter dispose flush cleanup`).
- Next steps:
  - continue bughunting the remaining low-level callback, timer, and event-fanout helpers
  - prioritize disposal/shutdown paths that invoke user work before releasing owned infrastructure resources

## 2026-03-23 13:48 CST
- Continued the broader bughunt into manual SQLite schema ownership in `PodCore`.
- Fixed `SqlitePodMessageStorage` so first-use initialization now creates the `Messages` table/index, FTS table, and triggers idempotently instead of depending on external `EnsureCreated()` ordering.
- Initialization now rebuilds the FTS index from `Messages`, which repairs first-use startup on an empty file, recovers from an existing FTS-only partial artifact, and backfills search rows when FTS is introduced after messages already exist.
- Fixed `RebuildSearchIndexAsync()` so repeated rebuilds clear existing FTS rows before repopulating, preventing duplicate search results.
- Added focused `SqlitePodMessageStorageTests` coverage for first-use schema creation, FTS-only artifact recovery, and idempotent rebuild/backfill; confirmed the focused slice passed (`3/3`) and the release build stayed green (`0 warnings / 0 errors`).
- Next steps:
  - continue bughunting adjacent manual SQLite initialization and validation seams that mix EF-managed tables with hand-created virtual tables or triggers
  - prioritize other repository/storage types where partial schema creation can leave the database in a state that later startup code no longer repairs
