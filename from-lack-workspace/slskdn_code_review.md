# slskdn code-first review (docs treated as untrusted)

This report is generated from the ZIP you uploaded. It is **code-first**: it assumes project documentation may be outdated/incorrect and evaluates the runtime behavior implied by code.

## Stop-ship issues (security / exposure)

### 1) CORS is effectively “allow any origin + credentials”
`Program.cs` config combines `AllowCredentials()` with `SetIsOriginAllowed(_ => true)`.

- Evidence: `src/slskd/Program.cs` around lines 1816, 1820, 1817

Impact: enables credentialed cross-origin access patterns that are broadly unsafe unless strictly controlled.

Recommended fix:
- Replace with an explicit origin allowlist; avoid credentials unless required.

### 2) Many controllers appear public (no `[Authorize]`, no `[AllowAnonymous]`)
Because there is no global default-deny policy, controllers without `[Authorize]` are public by default.

Controllers missing auth attributes (22):
- src/slskd/API/Mesh/MeshGatewayController.cs
- src/slskd/Audio/API/AnalyzerMigrationController.cs
- src/slskd/Audio/API/CanonicalController.cs
- src/slskd/Audio/API/DedupeController.cs
- src/slskd/MediaCore/API/Controllers/ContentDescriptorPublisherController.cs
- src/slskd/MediaCore/API/Controllers/ContentIdController.cs
- src/slskd/MediaCore/API/Controllers/DescriptorRetrieverController.cs
- src/slskd/MediaCore/API/Controllers/FuzzyMatcherController.cs
- src/slskd/MediaCore/API/Controllers/IpldController.cs
- src/slskd/MediaCore/API/Controllers/MediaCoreStatsController.cs
- src/slskd/MediaCore/API/Controllers/MetadataPortabilityController.cs
- src/slskd/MediaCore/API/Controllers/PerceptualHashController.cs
- src/slskd/PodCore/API/Controllers/PodDhtController.cs
- src/slskd/PodCore/API/Controllers/PodDiscoveryController.cs
- src/slskd/PodCore/API/Controllers/PodJoinLeaveController.cs
- src/slskd/PodCore/API/Controllers/PodMembershipController.cs
- src/slskd/PodCore/API/Controllers/PodMessageRoutingController.cs
- src/slskd/PodCore/API/Controllers/PodMessageSigningController.cs
- src/slskd/PodCore/API/Controllers/PodVerificationController.cs
- src/slskd/SocialFederation/API/ActivityPubController.cs
- src/slskd/SocialFederation/API/WebFingerController.cs
- src/slskd/VirtualSoulfind/v2/API/VirtualSoulfindV2Controller.cs

Recommended fix:
- Add a global controller authorization requirement (fallback policy or MVC filter) and explicitly annotate intended-public endpoints with `[AllowAnonymous]`.

### 3) “Auth disabled” mode elevates all requests to admin
When authentication is disabled, the passthrough handler constructs an authenticated identity with an administrator role.

- Evidence: `src/slskd/Program.cs` around line 1989

Recommended fix:
- Fail startup if auth is disabled and binding to non-loopback. Treat no-auth as Development-only.

### 4) Memory dump endpoint + runtime download/exec of dump tooling
There is an API endpoint to dump process memory, and the dumper downloads tooling at runtime.

- Evidence: `src/slskd/Core/API/Controllers/ApplicationController.cs` line 151 (`GET .../application/dump`)
- Evidence: `src/slskd/Common/Dumper.cs` line None (runtime download)

Recommended fix:
- Remove from production builds or hard-gate: admin-only + local-only + explicit config flag.
- Do not download/execute tooling at runtime; ship pinned artifacts.

### 5) Placeholder signature verification (false security boundary)
There are code paths where “verification” is effectively a stub, e.g., checking presence/format rather than cryptographic validity.
These boundaries are unsafe if any trust or authorization depends on them.

Recommended fix:
- Disable these flows until real signing/verification is implemented and tested end-to-end.

## Logic/footguns

### 6) ModelState invalid filter suppressed
- Evidence: `src/slskd/Program.cs` around line 2021

Impact: invalid payloads can reach actions unless every action validates input explicitly.

### 7) Error handler leaks raw exception messages
- Evidence: `src/slskd/Program.cs` around line 2111

Impact: information disclosure (internal details) and better attacker feedback.

## Stubs / incomplete crash surfaces

### NotImplementedException sites (4)
- Common/Security/I2PTransport.cs:172  throw new NotImplementedException(
- Common/Security/RelayOnlyTransport.cs:110  throw new NotImplementedException(
- MediaCore/PerceptualHasher.cs:433  throw new NotImplementedException(
- Privacy/MessagePadder.cs:65  throw new NotImplementedException("Unpad is not implemented for this padder");

### Highest TODO / placeholder concentrations (top 25)
- MediaCore/MediaCoreStatsService.cs  (TODO=0, placeholder=12)
- PodCore/PodMessageRouter.cs  (TODO=3, placeholder=4)
- SocialFederation/API/ActivityPubController.cs  (TODO=6, placeholder=0)
- PodCore/MessageSigner.cs  (TODO=0, placeholder=5)
- Signals/Swarm/SwarmSignalHandlers.cs  (TODO=4, placeholder=0)
- Mesh/ServiceFabric/MeshServiceDescriptorValidator.cs  (TODO=3, placeholder=1)
- Mesh/Dht/PeerDescriptorPublisher.cs  (TODO=1, placeholder=3)
- Mesh/Realm/Bridge/ActivityPubBridge.cs  (TODO=0, placeholder=4)
- Common/CodeQuality/HotspotAnalysis.cs  (TODO=0, placeholder=4)
- VirtualSoulfind/Scenes/ScenePubSubService.cs  (TODO=3, placeholder=0)
- VirtualSoulfind/Scenes/SceneChatService.cs  (TODO=3, placeholder=0)
- Security/Policies.cs  (TODO=3, placeholder=0)
- Mesh/ServiceFabric/MeshServiceClient.cs  (TODO=3, placeholder=0)
- VirtualSoulfind/Scenes/SceneAnnouncementService.cs  (TODO=2, placeholder=1)
- PodCore/PodOpinionService.cs  (TODO=2, placeholder=1)
- PodCore/PodAffinityScorer.cs  (TODO=1, placeholder=2)
- PodCore/ContentLinkService.cs  (TODO=0, placeholder=3)
- Mesh/Realm/Bridge/MetadataBridge.cs  (TODO=0, placeholder=3)
- Common/Security/LoggingSanitizer.cs  (TODO=0, placeholder=3)
- SocialFederation/FederationService.cs  (TODO=2, placeholder=0)
- SocialFederation/API/WebFingerController.cs  (TODO=2, placeholder=0)
- PodCore/PodOpinionAggregator.cs  (TODO=2, placeholder=0)
- Capabilities/CapabilityFileService.cs  (TODO=2, placeholder=0)
- Backfill/BackfillSchedulerService.cs  (TODO=2, placeholder=0)
- API/Compatibility/DownloadsCompatibilityController.cs  (TODO=2, placeholder=0)

## Immediate hardening sequence

1) Default-deny controllers (global `[Authorize]`) and explicitly allow anonymous where intended.
2) Fix CORS to an allowlist; avoid wildcard + credentials.
3) Prevent insecure startup combos (auth disabled + non-loopback binding).
4) Remove/lock down dump endpoints and runtime download/exec helpers.
5) Disable placeholder crypto verification flows until real crypto is implemented and validated.
6) Restore ModelState auto rejection or add a global validation filter.
7) Stop returning raw exception messages to clients; log with correlation IDs.
