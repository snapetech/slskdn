# Placeholder and Null-Heavy Inventory

Generated: 2026-03-22

> Historical snapshot. This inventory may not reflect current defaults, test
> counts, or code. Prefer GitHub Actions, `docs/dev/testing-policy.md`, and
> current feature-specific docs for live status.

## Purpose

This inventory tracks the remaining placeholder-heavy, null-heavy, and explicitly incomplete runtime areas in `src/slskd/` so they can be completed in larger batches instead of one-off bugfixes.

This is a heuristic inventory based on code-pattern scanning for:

- `return null;`
- `TODO:`
- `not implemented`
- `placeholder`
- `fake`
- `synthetic`
- `FeatureNotImplementedException`

It is not a literal bug list. Some hits are legitimate parse failures or optional integrations. The useful value is identifying dense clusters where runtime behavior still bottoms out early or advertises incomplete features.

## Exclusions and false positives

- Built frontend assets under `src/slskd/wwwroot/assets/` are excluded from prioritization.
- Some `return null;` paths are valid parse/lookup semantics and not bugs by themselves.
- Some comments describe future work without affecting current runtime correctness.
- Some `synthetic` hits in `SongID` are domain terminology, not placeholder code.

## Highest-density completion clusters

### Tier 1: largest remaining completion clusters

1. `src/slskd/SongID/SongIdService.cs`
   - 28 pattern hits
   - still the single largest null-heavy service surface
   - likely next best ROI if the goal is “finish real behavior instead of early-returning”

2. `src/slskd/HashDb/HashDbService.cs`
   - 24 pattern hits
   - still a large read-side and fallback-heavy area even after prior cleanup

3. `src/slskd/Common/Security/PathGuard.cs`
   - 19 pattern hits
   - many early-null exits; likely worth a dedicated pass to separate legitimate rejects from under-reporting paths

4. `src/slskd/Mesh/MeshSyncService.cs`
   - 16 pattern hits
   - transport is still incomplete; several internal null-return paths remain

5. `src/slskd/VirtualSoulfind/Core/Music/MusicContentDomainProvider.cs`
   - 14 pattern hits
   - still a major completion surface despite earlier fallback work

6. `src/slskd/SocialFederation/HttpSignatureKeyFetcher.cs`
   - 13 pattern hits
   - many early-null exits; likely a mix of defensive filtering and incomplete observability

### Tier 2: medium clusters worth batching

1. `src/slskd/DhtRendezvous/Security/PathGuard.cs`
   - 11 hits

2. `src/slskd/VirtualSoulfind/Bridge/Protocol/SoulseekProtocolParser.cs`
   - 9 hits

3. `src/slskd/Mesh/Nat/StunNatDetector.cs`
   - 9 hits

4. `src/slskd/DhtRendezvous/MeshOverlayConnector.cs`
   - 8 hits

5. `src/slskd/Signals/Swarm/MonoTorrentBitTorrentBackend.cs`
   - 8 hits

6. `src/slskd/Capabilities/CapabilityFileService.cs`
   - 7 hits

7. `src/slskd/Integrations/MetadataFacade/MetadataFacade.cs`
   - 7 hits

8. `src/slskd/Transfers/Rescue/RescueService.cs`
   - 7 hits

### Tier 3: placeholder / explicitly incomplete feature surfaces

1. `src/slskd/MediaCore/ContentDescriptorPublisher.cs`
   - explicit not-implemented update/republish flow remains

2. `src/slskd/Swarm/SwarmDownloadOrchestrator.cs`
   - mesh chunk download still called out as TODO

3. `src/slskd/LibraryHealth/Remediation/LibraryHealthRemediationService.cs`
   - still logs/returns placeholder job behavior

4. `src/slskd/PodCore/PodMessageBackfill.cs`
   - still has explicit “response handling is not implemented”

5. `src/slskd/Security/Policies.cs`
   - consensus policy still contains an explicit “not implemented” allow-path

6. `src/slskd/VirtualSoulfind/v2/Backends/MeshDhtBackend.cs`
   - actual mesh reachability check still TODO

7. `src/slskd/VirtualSoulfind/v2/Backends/LanBackend.cs`
   - actual SMB/NFS reachability check still TODO

## Current top files by pattern count

Excluding built assets:

```text
  28 src/slskd/SongID/SongIdService.cs
  24 src/slskd/HashDb/HashDbService.cs
  19 src/slskd/Common/Security/PathGuard.cs
  17 src/slskd/SongID/SongIdScoring.cs
  16 src/slskd/Mesh/MeshSyncService.cs
  14 src/slskd/VirtualSoulfind/Core/Music/MusicContentDomainProvider.cs
  13 src/slskd/SocialFederation/HttpSignatureKeyFetcher.cs
  11 src/slskd/DhtRendezvous/Security/PathGuard.cs
   9 src/slskd/VirtualSoulfind/Bridge/Protocol/SoulseekProtocolParser.cs
   9 src/slskd/Mesh/Nat/StunNatDetector.cs
   8 src/slskd/DhtRendezvous/MeshOverlayConnector.cs
   8 src/slskd/Signals/Swarm/MonoTorrentBitTorrentBackend.cs
   7 src/slskd/Capabilities/CapabilityFileService.cs
   7 src/slskd/Integrations/MetadataFacade/MetadataFacade.cs
   7 src/slskd/Transfers/Rescue/RescueService.cs
   6 src/slskd/Application.cs
   6 src/slskd/Common/Security/LocalPortForwarder.cs
   6 src/slskd/Integrations/MusicBrainz/MusicBrainzClient.cs
   6 src/slskd/SocialFederation/FederationService.cs
   5 src/slskd/Capabilities/CapabilityService.cs
   5 src/slskd/Identity/ProfileService.cs
   5 src/slskd/PodCore/ContentLinkService.cs
   5 src/slskd/VirtualSoulfind/Scenes/SceneMembershipTracker.cs
```

## Recommended completion batches

### Batch A: SongID and Metadata

- `SongIdService`
- `SongIdScoring`
- `MetadataFacade`
- `MusicBrainzClient`

Reason:

- highest remaining density
- likely biggest reduction in null-heavy behavior per batch

### Batch B: Mesh and transport fallback completion

- `MeshSyncService`
- `MeshOverlayConnector`
- `StunNatDetector`
- `CapabilityFileService`
- `MeshStatsCollector`

Reason:

- still several runtime paths that bottom out early or under-report state

### Batch C: PodCore discovery and backfill completion

- `PodMessageBackfill`
- `PodServices`
- `PodOpinionAggregator`
- `SqlitePodService`

Reason:

- PodCore still has explicit incomplete behavior and several null-heavy service paths

### Batch D: VirtualSoulfind and bridge runtime completion

- `MusicContentDomainProvider`
- `SoulseekProtocolParser`
- `BridgeApi`
- `SceneMembershipTracker`
- `RescueService`

Reason:

- runtime-facing VSF features still have many early-null exits

### Batch E: Security/path/validation semantics

- `Common/Security/PathGuard`
- `DhtRendezvous/Security/PathGuard`
- `HttpSignatureKeyFetcher`

Reason:

- high-density early-return logic
- likely mix of correct defensive behavior and over-conservative nulling

## Suggested workflow

For each batch:

1. separate legitimate parse/guard nulls from under-reporting nulls
2. replace reachable placeholder branches with conservative real behavior where dependencies already exist
3. add targeted tests for the now-completed behavior
4. update this inventory by removing or downgrading the finished cluster

## Immediate next candidates

If the goal is maximum completion per pass, the best next two batches are:

1. Batch A: `SongID` + `MetadataFacade`
2. Batch B: `MeshSync` + transport/runtime fallback completion
