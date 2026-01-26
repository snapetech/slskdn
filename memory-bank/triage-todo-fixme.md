# Triage: src/ TODO, FIXME, placeholder

Triage of `// TODO`, `// todo`, `// FIXME`, and `placeholder` in `src/**/*.cs` (~143 in 83 files).  
Each is classified: **accepted** (by-design, doc only), **defer** (tech debt, no task yet), **task** (promote to [ ] in tasks.md).

---

## accepted (by-design or documented; no code change)

| File | Snippet / reason |
|------|------------------|
| `MediaCore/MediaVariant.cs` | Doc: "Image/Video have placeholders" (T-911); ImageDimensions, ImageCodec, VideoDimensions, etc. |
| `VirtualSoulfind/Core/ContentDomain.cs` | Doc: "T-911 placeholder" for Image, Video domains |
| `Common/Security/LoggingSanitizer.cs` | "placeholder" = by-design replacement for sensitive data; "generic placeholder" on path parse fail |
| `Common/CodeQuality/HotspotAnalysis.cs` | Placeholders "would need git integration", "source file mapping"; deferred by design |
| `Transfers/MultiSource/IContentVerificationService.cs` | "dummy placeholder for future use" (CodecProfile) |
| `Transfers/MultiSource/Playback/PlaybackFeedbackService.cs` | "placeholder for future scheduling integration" |
| `Transfers/MultiSource/Playback/PlaybackPriorityService.cs` | "use desired as a proxy placeholder" (no buffer tracking) |
| `Mesh/Transport/Ed25519Signer.cs` | "placeholder key" / "temporary key for placeholder signing" when key all-zeros |
| `Mesh/Dht/PeerDescriptorPublisher.cs` | "using placeholder" / "placeholder key" when KeyStore unavailable — fail gracefully |
| `Transfers/MultiSource/MediaCoreSwarmService.cs` | "Placeholder - file size not directly available" |
| `VirtualSoulfind/Scenes/SceneMembershipTracker.cs` | "Placeholder" CreatedAt when metadata parse missing |
| `VirtualSoulfind/DisasterMode/MeshTransferService.cs` | "use a placeholder" for one path; "Actual hash verification" = known gap, documented |

---

## defer (tech debt; no [ ] in tasks.md for now)

Incremental improvements, refactors, or follow-ups to be scheduled later.

- **Application.cs**: docker hub canary tag; move long block; blacklisted message event; watched user status
- **UploadService.cs**, **DownloadService.cs**: broadcast / force remote scan / resume / visual indicator — UI or events
- **Transfers/API/Controllers/TransfersController.cs**: mesh vs soulseek; refactor date range
- **RescueService.cs**: "Get proper output path"; "rescue activation is placeholder only" when no overlay
- **RescueGuardrailService.cs**: "Add more guardrails"
- **ChunkScheduler.cs**: "trigger chunk reassignment to better peers"
- **MediaCoreChunkScheduler.cs**: "Pass contentId through chunk request context"
- **VirtualSoulfind/Bridge**: SoulfindBridgeService ActiveConnections; TransferProgressProxy push to legacy
- **VirtualSoulfind/Core/Music/MusicContentDomainProvider.cs**: "recent items from HashDb"
- **VirtualSoulfind/v2/Reconciliation/LibraryReconciliationService.cs**: "placeholder that shows the intent"
- **VirtualSoulfind/ShadowIndex/ShadowIndexQueryImpl.cs**: "return a placeholder"
- **VirtualSoulfind/v2/Backends/SoulseekBackend.cs**: "placeholder - in production we'd look up metadata"; "Parse itemId"
- **Mesh/MeshCircuitBuilder.cs**: "Add persistent counter"; "placeholder implementation" for connection
- **Mesh/ServiceFabric/DhtMeshServiceDirectory.cs**: "efficient FindById"
- **Mesh/ServiceFabric/Services/PrivateGatewayMeshService.cs**: "Get from service context" (localPeerId)
- **Mesh/ServiceFabric/Services/PodsMeshService.cs**: "Validate message size"
- **Mesh/Transport/TransportSelector.cs**: "Pass actual trust status"
- **Mesh/ServiceFabric/MeshServiceClient.cs**, **MeshServiceDescriptorValidator.cs**: various
- **Mesh/MeshCircuit.cs**, **Mesh/ServiceFabric/Services/MeshIntrospectionService.cs**
- **DhtRendezvous/MeshNeighborRegistry.cs**: "Add PeerVersion to MeshOverlayConnection"
- **Signals/Swarm/SwarmSignalHandlers.cs**: Look up fallback ack; Enable BT fallback; Cancel job; "Implement actual variant check"
- **Signals/MeshSignalChannelHandler.cs**: "Check if Mesh has route to peer"
- **Signals/SignalServiceExtensions.cs**: SwarmSignalHandlers localPeerId / factory
- **SocialFederation**: ActivityPubController inbox/activity processing; WebFingerController library/user actors; FederationService user actor, actor discovery; VirtualSoulfindFederationIntegration tombstone
- **PodCore**: PodDhtPublisher "placeholder implementation" / "return true as placeholder"; PodMessageBackfill avgDuration, placeholder; PodOpinionAggregator opinion count, recent activity; PodOpinionService contentIds, opinion signing; PodDiscoveryService GetPodFromPublisher "placeholder"; PodAffinityScorer reputation, verified/trusted, banned "placeholder"; PodMessageBackfillController; ContentLinkService
- **Security/Policies.cs**: PeerId→IP; mesh consensus; NAT abuse
- **Program.cs**: enableCostBasedScheduling from config
- **Search/SearchService.cs**: "MusicBrainz API for proper query resolution"
- **Signals/Swarm/MonoTorrentBitTorrentBackend.cs**: "AddPeersAsync for manual peers (InviteList)"
- **VirtualSoulfind/v2/Backends/TorrentBackend.cs**: "T-V2-P4-04 - actual torrent health check"
- **VirtualSoulfind/v2/Backends/LanBackend.cs**: "T-V2-P4-06 - SMB/NFS reachability"
- **VirtualSoulfind/v2/Backends/MeshDhtBackend.cs**: "T-V2-P4-03 - mesh node reachability"
- **Common/Security/API/SecurityController.cs**: "persistence and runtime configuration updates"
- **API/Mesh/MeshGatewayController.cs**: "load balancing, reputation-based selection"
- **VirtualSoulfind/v2/API/VirtualSoulfindV2Controller.cs**: "GetReleaseIntentAsync in IIntentQueue"
- **Wishlist** (AutoDownload in DTOs): property names, not TODO — N/A
- **ConversationService, RoomService, MediaCoreStatsService, Integrations/Brainz/BrainzClient, Jobs/Metadata/NetworkSimulationJob, LibraryHealth*, ContentDescriptorPublisher, PrivacyLayer, DownloadsCompatibilityController, JobsController, CanonicalController, DisasterModeController, ShadowIndexController, FlacAnalyzer, BackfillSchedulerService, CapabilityFileService, CapabilityService, SwarmDownloadOrchestrator**

---

## task (promote to [ ] — implement or close)

These had one follow-up [ ] in tasks.md: **Triage follow-up (task)**. Status:

| File | TODO / FIXME | Status |
|------|---------------|--------|
| `Mesh/Overlay/QuicDataServer.cs` | Process payload (deliver to mesh message handler) | **Defer**: TODO replaced with comment; IOverlayDataPayloadHandler to be designed. |
| `Core/Options.cs` | Re-enable realm validation once configuration loading works | **Done**: `Realm.Validate()` and `MultiRealm.Validate()` called in `Options.Validate`. |
| `Transfers/Rescue/RescueService.cs` | Get proper output path; rescue without overlay (placeholder) | Defer |
| `VirtualSoulfind/Scenes/SceneAnnouncementService.cs` | Iterate joined scenes, refresh; peer ID hint | Defer |
| `VirtualSoulfind/Scenes/SceneChatService.cs` | Use actual peer ID; serialization/deserialization (MessagePack) | Defer |
| `VirtualSoulfind/Scenes/ScenePubSubService.cs` | Overlay pubsub: subscribe, unsubscribe, publish | Defer |
| `VirtualSoulfind/Scenes/SceneService.cs` | DHT-based scene search | Defer |

---

## Summary

- **accepted**: 13 (documented placeholders or by-design).
- **defer**: ~100 across 70+ files (tech debt; no [ ] now).
- **task**: 7 items → 2 addressed (Options realm, QuicDataServer comment); 5 remain defer. Triage follow-up [x] in tasks.md.

*Triage done 2026-01-25. Updated 2026-01-25: Options realm, QuicDataServer.*
