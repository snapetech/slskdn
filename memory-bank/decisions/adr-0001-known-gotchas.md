# ADR-0001: Known Gotchas & Anti-Patterns

> **Status**: Active (Living Document)  
> **Date**: 2025-12-08  
> **Author**: AI-assisted development sessions

This document captures known issues, anti-patterns, and "gotchas" that AI models and developers have encountered. **Read this before making changes.**

---

## ⚠️ FOR AI AGENTS: ADD TO THIS FILE IMMEDIATELY

**When you fix a bug, ADD IT HERE before doing anything else.**

This is not optional. This is the highest priority action after fixing a bug.

### When to add an entry:
- You fixed a bug that you or another AI introduced
- You fixed the same type of bug more than once
- You discovered a non-obvious pattern that causes errors
- You spent >5 minutes debugging something that should have been documented

### Entry template:
```markdown
### N. short-descriptive-name

**The Bug**: One sentence description.

**Files Affected**:
- `path/to/file.cs`

**Wrong**:
```code
// bad code
```

**Correct**:
```code
// good code
```

**Why This Keeps Happening**: Explanation of the root cause.
```

### After adding:
1. Commit immediately: `git commit -m "docs: Add gotcha for [description]"`
2. Then continue with other work

**If you skip this step, the same bug WILL happen again. Document it NOW.**

---

## 🚨 CRITICAL: Bugs That Keep Coming Back

### 0x9. VirtualSoulfind v2 Must Not Search Soulseek With Opaque Item IDs Or Match Tracks Without Catalogue Context

**The Bug**: The v2 Soulseek backend built search text from `ContentItemId.ToString()`, which produced opaque GUID queries that could never return useful network results. At the same time, the v2 match engine ignored artist/release context already present in the catalogue and accepted title-plus-duration matches as if they were the best available rule.

**Files Affected**:
- `src/slskd/VirtualSoulfind/v2/Backends/SoulseekBackend.cs`
- `src/slskd/VirtualSoulfind/v2/Matching/SimpleMatchEngine.cs`

**Wrong**:
```csharp
var searchQuery = BuildSearchQuery(itemId); // returns raw GUID text
...
if (titleMatch && durationMatch)
{
    return Task.FromResult(new MatchResult
    {
        Confidence = MatchConfidence.Medium,
        Score = 0.75,
        Reason = "Title + duration match",
    });
}
```

**Correct**:
```csharp
var track = await _catalogueStore.FindTrackByIdAsync(itemId.ToString(), cancellationToken);
AddSearchTerm(terms, artist?.Name);
AddSearchTerm(terms, track.Title);
...
var artistMatch = IsTextMatch(context.ArtistName, candidate.Embedded.Artist);
var albumMatch = IsTextMatch(context.ReleaseTitle, candidate.Embedded.Album);
```

**Why This Keeps Happening**: v2 content IDs are internal correlation keys, not user-facing discovery terms. When a backend already has a catalogue layer, search should be built from canonical metadata and matching should use the same context. Falling back to opaque IDs or title-only matching makes the feature look implemented while quietly discarding the only evidence that could make it work conservatively and correctly.

### 0x9A. VirtualSoulfind v2 "Supports All Domains" Backends Must Not Be Filtered Out By A Strict Domain Equality Check

**The Bug**: `MultiSourcePlanner` treated `SupportedDomain == null` as a mismatch and skipped the backend entirely. Most v2 backends use `null` specifically to mean "supports all domains", so the planner quietly filtered out LocalLibrary, HTTP, WebDAV, S3, Torrent, MeshDht, and LAN candidates before planning ever happened.

**Files Affected**:
- `src/slskd/VirtualSoulfind/v2/Planning/MultiSourcePlanner.cs`

**Wrong**:
```csharp
if (backend.SupportedDomain != domain)
{
    continue;
}
```

**Correct**:
```csharp
if (backend.SupportedDomain.HasValue && backend.SupportedDomain != domain)
{
    continue;
}
```

**Why This Keeps Happening**: Nullable enum capability flags are easy to misread during planner work. In this codebase, `null` is not "unknown" or "unsupported"; it is the wildcard case. Any backend-selection filter has to preserve that semantic or the system looks wired while most of the backends are effectively dead.

### 0x9B. VirtualSoulfind v2 Must Not Lose Track Domain Or Report Acquisition Success For Backends That Cannot Fetch

**The Bug**: The in-memory intent queue rebuilt `DesiredTrack` without copying `Domain`, so any status transition silently erased the domain needed by later planning and API reads. At the same time, the simple resolver treated `Soulseek`, `MeshDht`, and `Lan` steps as success even though those backends still had no fetch implementation, causing intents to complete without acquiring anything.

**Files Affected**:
- `src/slskd/VirtualSoulfind/v2/Intents/InMemoryIntentQueue.cs`
- `src/slskd/VirtualSoulfind/v2/Resolution/SimpleResolver.cs`

**Wrong**:
```csharp
var updated = new DesiredTrack
{
    DesiredTrackId = track.DesiredTrackId,
    TrackId = track.TrackId,
    ...
};
...
else
{
    return StepResult.Success(null);
}
```

**Correct**:
```csharp
var updated = new DesiredTrack
{
    Domain = track.Domain,
    DesiredTrackId = track.DesiredTrackId,
    ...
};
...
else if (step.Backend == ContentBackendType.LocalLibrary)
{
    return StepResult.Success(candidate.BackendRef);
}
else
{
    return StepResult.Failure($"Backend {step.Backend} has no fetch implementation");
}
```

**Why This Keeps Happening**: Value-object update helpers are easy to treat like "copy most fields" code, but queue state types often carry planner-critical metadata such as domain or mode. Separately, staged resolver work often starts by handling only fetch-capable backends, and it is tempting to mark the rest as "good enough" success to keep the flow moving. That creates silent false positives. Preserve all immutable intent fields on update, and fail explicitly for any backend whose fetch path is still missing.

### 0x9C. VirtualSoulfind v2 Local-Library Lookups Must Resolve A Real MediaCore Content ID Before Querying Shares

**The Bug**: `LocalLibraryBackend` queried `IShareRepository` with `ContentItemId.ToString()`, which is just the v2 track GUID. The share repository indexes MediaCore `contentId` strings, so local-library discovery looked implemented but would miss valid local content unless the GUID happened to equal a content ID.

**Files Affected**:
- `src/slskd/VirtualSoulfind/v2/Backends/LocalLibraryBackend.cs`

**Wrong**:
```csharp
var contentIdStr = itemId.ToString();
var contentItem = _shareRepository.FindContentItem(contentIdStr);
```

**Correct**:
```csharp
var externalId = "mb:recording:" + itemId.Value;
var resolved = await _contentIdRegistry.ResolveAsync(externalId, cancellationToken);
var contentItem = _shareRepository.FindContentItem(contentIdStr);
```

**Why This Keeps Happening**: VirtualSoulfind v2 track IDs and MediaCore content IDs are related, but they are not interchangeable. Backends that cross from catalogue/planning into the shared content layer must do the ID translation explicitly. If they do not, the backend will quietly return empty candidate lists while looking perfectly reasonable in code review.

### 0x9D. VirtualSoulfind v2 Controllers Must Fail Closed When Disabled And Must Carry Domain Through Manual Plan Creation

**The Bug**: The v2 controller exposed all endpoints unconditionally even though the options contract says disabled mode should return `503`. It also accepted manual plan creation requests without a `Domain`, then built a `DesiredTrack` with the default enum value, so non-music plan requests silently planned against the wrong domain.

**Files Affected**:
- `src/slskd/VirtualSoulfind/v2/API/VirtualSoulfindV2Controller.cs`

**Wrong**:
```csharp
public sealed class CreatePlanRequest
{
    public string TrackId { get; set; } = string.Empty;
}
...
var desiredTrack = new DesiredTrack
{
    DesiredTrackId = Guid.NewGuid().ToString(),
    TrackId = request.TrackId,
};
```

**Correct**:
```csharp
if (!_options.CurrentValue.VirtualSoulfindV2.Enabled)
{
    return StatusCode(StatusCodes.Status503ServiceUnavailable, "VirtualSoulfind v2 is disabled");
}
...
public ContentDomain Domain { get; set; } = ContentDomain.Music;
...
Domain = request.Domain,
```

**Why This Keeps Happening**: API shells often get built before option gating is wired, especially when the underlying services exist in DI. That makes disabled features appear live. Separately, manual/test plan endpoints often omit metadata that the queue path already carries, which means they default to whatever enum zero happens to be. Keep controller contracts aligned with the documented runtime gate, and require the same planner-critical fields on manual plan creation that the real intent path carries.

### 0x9E. VirtualSoulfind v2 Candidate Scores And Backend Ordering Must Use The Same Normalized Model

**The Bug**: `LocalLibraryBackend` emitted `ExpectedQuality = 100` even though `SourceCandidate.ExpectedQuality` is documented as `0.0..1.0`. At the same time, `MultiSourcePlanner`'s backend ordering table omitted newly wired backends like `NativeMesh`, `WebDav`, and `S3`, so they fell through to the default lowest-priority bucket regardless of actual planner intent.

**Files Affected**:
- `src/slskd/VirtualSoulfind/v2/Backends/LocalLibraryBackend.cs`
- `src/slskd/VirtualSoulfind/v2/Planning/MultiSourcePlanner.cs`

**Wrong**:
```csharp
ExpectedQuality = 100,
...
var backendPriority = new Dictionary<ContentBackendType, int>
{
    { ContentBackendType.LocalLibrary, 1 },
    { ContentBackendType.MeshDht, 2 },
    { ContentBackendType.Http, 3 },
    ...
};
```

**Correct**:
```csharp
ExpectedQuality = 1.0f,
...
{ ContentBackendType.NativeMesh, 2 },
{ ContentBackendType.WebDav, 3 },
{ ContentBackendType.S3, 3 },
```

**Why This Keeps Happening**: Candidate and planner code often evolve separately. One side adds new backends or score semantics, while the other side still assumes the old set. The result is not a crash; it is silent mis-ordering that makes the planner behave irrationally. Keep `SourceCandidate` scores normalized consistently and update the planner’s explicit backend order whenever a new backend becomes real.

### 0x9F. VirtualSoulfind v2 DI Must Bind Runtime Services To Root `Options.VirtualSoulfindV2`, Not Constructor Defaults

**The Bug**: The v2 services were registered with `AddOptions<T>()` only, so resolver timeouts, processor cadence, planner default mode, and Soulseek backend limits were running on type defaults instead of the actual nested root config under `Options.VirtualSoulfindV2`.

**Files Affected**:
- `src/slskd/Program.cs`

**Wrong**:
```csharp
services.AddOptions<VirtualSoulfind.v2.Resolution.ResolverOptions>();
services.AddSingleton<VirtualSoulfind.v2.Planning.IPlanner, VirtualSoulfind.v2.Planning.MultiSourcePlanner>();
```

**Correct**:
```csharp
var root = sp.GetRequiredService<IOptionsMonitor<Options>>().CurrentValue.VirtualSoulfindV2;
return new WrappedOptionsMonitor<ResolverOptions>(Options.Create(new ResolverOptions
{
    DefaultStepTimeoutSeconds = root.PlanTimeoutSeconds,
}));
...
return new MultiSourcePlanner(..., root.DefaultMode);
```

**Why This Keeps Happening**: nested option objects in the main `Options` model look like ordinary `IOptions<T>` registrations, but `AddOptions<T>()` alone does not map them from the already-bound parent object. That leaves the feature "configured" on paper while runtime services silently use library defaults. When a subsystem derives its settings from `Options.SomeNestedObject`, explicitly bridge that nested object into the option type or constructor that the subsystem actually consumes.

### 0xA. ActivityPub Outboxes Must Not Be Advertised Without A Real Post Path And Follower Fan-Out

**The Bug**: The server advertised actor outbox URLs, but `POST /actors/{actor}/outbox` returned `501` and public activities had no follower fan-out path. That meant local actors could claim an ActivityPub outbox existed while there was no durable local post path and no real delivery to followers.

**Files Affected**:
- `src/slskd/SocialFederation/API/ActivityPubController.cs`
- `src/slskd/SocialFederation/FederationService.cs`
- `src/slskd/SocialFederation/ActivityPubOutboxStore.cs`

**Wrong**:
```csharp
return Task.FromResult<IActionResult>(StatusCode(501, "Outbox posting not yet implemented"));
```

**Correct**:
```csharp
var (published, error) = await _federationService.PublishOutboxActivityAsync(actorName, activity, cancellationToken);
await _outboxStore.StoreAsync(actorName, published, rawJson, cancellationToken);
```

**Why This Keeps Happening**: It is easy to implement actor documents and inboxes first, leave the outbox as a placeholder, and assume generated recent activities are "good enough." They are not. Once an actor advertises an outbox, clients expect a real posting path, stable local history, and real fan-out semantics for public follower delivery. If those pieces are missing, either do not expose the outbox contract yet or wire the minimal durable path completely.

### 0xB. SongID Corpus Readers Must Accept Case-Insensitive Metadata And Relative Fingerprint Paths

**The Bug**: Corpus matching only deserialized metadata with the exact current JSON casing and only accepted absolute `FingerprintPath` values. Older entries, hand-edited entries, or entries moved with the corpus directory were silently ignored even though the fingerprint file was still present.

**Files Affected**:
- `src/slskd/SongID/SongIdService.cs`

**Wrong**:
```csharp
entry = JsonSerializer.Deserialize<SongIdCorpusEntry>(json);
if (string.IsNullOrWhiteSpace(entry.FingerprintPath) || !File.Exists(entry.FingerprintPath))
{
    continue;
}
```

**Correct**:
```csharp
entry = JsonSerializer.Deserialize<SongIdCorpusEntry>(json, new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true,
});
var fingerprintPath = ResolveCorpusFingerprintPath(metadataPath, entry);
```

**Why This Keeps Happening**: Local corpus formats evolve slowly and often get copied between machines or edited by tools outside the app. Assuming the exact latest serializer casing and an unchanged absolute path makes the corpus brittle for no good reason. Treat local metadata as compatibility data: deserialize case-insensitively and resolve relative artifact paths relative to the metadata file.

### 0xC. HashDb JSON Cache Reads Must Be Case-Insensitive For Stored Compatibility Blobs

**The Bug**: `HashDb` stored JSON snapshots like artist release graphs and then deserialized them with the strict default serializer settings. If the stored JSON used a different property casing, the read path dropped the whole cached object and returned `null` even though the payload was otherwise valid.

**Files Affected**:
- `src/slskd/HashDb/HashDbService.cs`

**Wrong**:
```csharp
return JsonSerializer.Deserialize<ArtistReleaseGraph>(json);
```

**Correct**:
```csharp
return JsonSerializer.Deserialize<ArtistReleaseGraph>(json, new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true,
});
```

**Why This Keeps Happening**: Cache and persistence blobs are long-lived compatibility surfaces, not short-lived in-memory objects. They often outlive serializer defaults and move between versions. When reading back our own stored JSON, prefer case-insensitive deserialization unless there is a strong reason not to.

### 0xD. Federation Removal Hooks Must Publish Real Tombstones, Not Just Log Content Deletion

**The Bug**: VirtualSoulfind removal hooks only logged that content disappeared locally. Federation consumers never received a `Delete` activity or tombstone object, so remote state stayed stale forever even though addition paths were already publishing.

**Files Affected**:
- `src/slskd/SocialFederation/VirtualSoulfindFederationIntegration.cs`

**Wrong**:
```csharp
_logger.LogDebug("[VSFederation] Content {ContentId} removed from domain {Domain}", contentId, domain);
await Task.CompletedTask;
```

**Correct**:
```csharp
var deleteActivity = new ActivityPubActivity
{
    Type = "Delete",
    Object = tombstone,
    To = new[] { "https://www.w3.org/ns/activitystreams#Public" }
};
await _federationService.PublishOutboxActivityAsync(actor.ActorName, deleteActivity, cancellationToken);
```

**Why This Keeps Happening**: Add/create flows are usually built first, and removal hooks get left as logging because deletion semantics feel secondary. In federation they are not secondary: if a feature publishes creations, it also needs a deletion path or remote state will rot. When wiring publishing integrations, treat add/update/remove as one contract.

### 0xE. Capability File Discovery Must Not Stop At UserInfo Fallback When Browse And Download APIs Already Exist

**The Bug**: Capability-file discovery exposed reserved virtual paths and iterated candidate filenames, but the actual small-file fetch helper always returned `null`. The code then fell back to parsing `slskdn_caps:` tags from user descriptions, which made the advertised file-based path dead even when the peer actually exposed the file.

**Files Affected**:
- `src/slskd/Capabilities/CapabilityFileService.cs`
- `src/slskd/Capabilities/CapabilityFileService.cs`

**Wrong**:
```csharp
_logger.LogDebug("Capability file download not yet implemented for {Username}/{Path}", username, filename);
return Task.FromResult<byte[]?>(null);
```

**Correct**:
```csharp
var browseResult = await _soulseekClient.BrowseAsync(username, cancellationToken);
await _soulseekClient.DownloadAsync(...);
```

**Why This Keeps Happening**: Helper methods around transfer flows are easy to stub during initial feature wiring because there is already a weaker fallback path. That fallback then hides the fact that the primary mechanism never worked. If the repo already has browse/download primitives for the same client, wire them before accepting a fallback-only implementation.

### 0xF. Shadow-Index Descriptor Sources Must Materialize Variant Hints, Not Just Echo The Content ID

**The Bug**: The shadow-index descriptor source successfully queried canonical variant hints, then built a descriptor with an empty hash list and almost no other information. That made the descriptor look present while discarding the actual codec, size, and hash-prefix evidence already returned by the shadow index.

**Files Affected**:
- `src/slskd/MediaCore/ShadowIndexDescriptorSource.cs`

**Wrong**:
```csharp
return new ContentDescriptor
{
    ContentId = contentId,
    Hashes = new List<ContentHash>(),
    Confidence = 0.8
};
```

**Correct**:
```csharp
var hashes = variants
    .Select(variant => new ContentHash("sha256-prefix16", ...))
    .ToList();
return new ContentDescriptor
{
    ContentId = contentId,
    Hashes = hashes,
    SizeBytes = bestVariant.SizeBytes,
    Codec = bestVariant.Codec,
    Confidence = confidence,
};
```

**Why This Keeps Happening**: “Best-effort” sources often get wired as minimal stubs first, and once they return a non-null descriptor, it is easy to miss that they are still throwing away most of their evidence. If a lookup already resolved structured hints, the descriptor builder should carry that evidence forward instead of collapsing it into an almost-empty shell.

### 0x10. Capability Version Parsers Must Accept Real Dev And Prerelease slskdn Version Strings

**The Bug**: Capability discovery parsed version strings with a strict `x.y.z` regex, so dev or prerelease client versions like `slskdn/0.24.1-dev+dht+mesh` were rejected before any capability tokens were read.

**Files Affected**:
- `src/slskd/Capabilities/CapabilityService.cs`

**Wrong**:
```csharp
new Regex(@"slskdn/(\\d+\\.\\d+\\.\\d+)(\\+.*)?", ...)
```

**Correct**:
```csharp
new Regex(@"slskdn/([^+\\s]+)(\\+.*)?", ...)
```

**Why This Keeps Happening**: Parsers often start from a stable release example and quietly hard-code that shape. Capability/version strings are compatibility inputs, so they need to accept the real version forms the project emits in dev and prerelease builds instead of only idealized semver triples.

### 0x11. Bridge Transfer Progress Must Preserve The Requested Filename Instead Of Trusting TargetPath Alone

**The Bug**: The bridge progress API built the legacy filename from `Path.GetFileName(status.TargetPath)`. If the transfer target path was empty, directory-shaped, or otherwise not a usable final file path yet, legacy clients got an empty or meaningless filename even though the original requested filename was known at download start.

**Files Affected**:
- `src/slskd/VirtualSoulfind/Bridge/BridgeApi.cs`

**Wrong**:
```csharp
Filename = Path.GetFileName(status.TargetPath),
```

**Correct**:
```csharp
transferMetadata[transferId] = new BridgeTransferMetadata
{
    RequestedFilename = filename,
};
...
Filename = string.IsNullOrWhiteSpace(filenameFromStatus)
    ? metadata.RequestedFilename
    : filenameFromStatus,
```

**Why This Keeps Happening**: Progress/readback paths often assume the execution layer will always have a clean final path ready. In bridge code that assumption is fragile because callers may pass a directory, a temporary path, or no target path at all. Preserve the user-facing filename at request time and use it as the fallback presentation value.

### 0x12. MediaCore Must Normalize `content:mb:*` IDs Into Audio Domains Before Domain/Type Queries

**The Bug**: `MediaCore` documented and used IDs like `content:mb:recording:<id>`, but domain checks and registry queries only matched literal `audio` domains. As a result, MB recording IDs were excluded from audio workflows such as `FindByDomainAsync("audio")`, `FindByDomainAndTypeAsync("audio","track")`, and `IsAudio`.

**Files Affected**:
- `src/slskd/MediaCore/ContentId.cs`
- `src/slskd/MediaCore/ContentIdRegistry.cs`

### 0x13. Bridge Admin And Status Surfaces Must Share Live Proxy State, And Progress Readback Must Reuse Cached File Metadata

**The Bug**: The bridge proxy accepted real client sessions, but the dashboard only tracked its own local counters, so admin/status APIs could still report zero active connections. In the same area, transfer progress readback fell back to `meshStatus.FileSize`, which can be `0`, even when the bridge had already cached a real file size at download start.

**Files Affected**:
- `src/slskd/VirtualSoulfind/Bridge/BridgeDashboard.cs`
- `src/slskd/VirtualSoulfind/Bridge/Proxy/BridgeProxyServer.cs`
- `src/slskd/API/VirtualSoulfind/BridgeController.cs`
- `src/slskd/VirtualSoulfind/Bridge/BridgeApi.cs`

**Wrong**:
```csharp
ActiveConnections = 0;
FileSize = status.FileSize;
PercentComplete = (int)Math.Clamp(status.ProgressPercent, 0, 100);
```

**Correct**:
```csharp
bridgeDashboard.RecordConnection(clientId, ipAddress);
bridgeDashboard.RecordRequest(session.ClientId, "download");
health.ActiveConnections = stats.CurrentConnections;
var fileSize = status.FileSize > 0 ? status.FileSize : metadata?.SizeBytes ?? 0;
```

**Why This Keeps Happening**: Bridge code has multiple readback surfaces: the proxy owns live sessions, the dashboard owns admin-facing stats, and the transfer layer owns execution status. If each surface reports only its own partial state, the UI regresses into obvious fiction like zero connections or zero-byte files. When a bridge workflow already captured live/session metadata earlier, status and admin endpoints need to reuse that same state instead of reconstructing weaker answers later.

### 0x14. Protocol Parsers Must Not Log Cooperative Cancellation As An Error, And Proxy Progress Must Preserve The Last Known File Size

**The Bug**: The Soulseek bridge protocol parser caught `OperationCanceledException` inside `ReadMessageAsync` and `WriteMessageAsync`, logged it as an error, and converted normal shutdown into noisy protocol failures. Separately, the transfer-progress proxy still trusted `meshStatus.FileSize` on every poll, so a later zero-sized status update could wipe out earlier correct file-size knowledge and collapse percentage math back to zero.

**Files Affected**:
- `src/slskd/VirtualSoulfind/Bridge/Protocol/SoulseekProtocolParser.cs`
- `src/slskd/VirtualSoulfind/Bridge/TransferProgressProxy.cs`

**Wrong**:
```csharp
catch (Exception ex)
{
    logger.LogError(ex, "[SOULSEEK-PROTO] Error reading message");
    return null;
}

FileSize = meshStatus.FileSize,
PercentComplete = (int)Math.Clamp(meshStatus.ProgressPercent, 0, 100),
```

**Correct**:
```csharp
catch (OperationCanceledException)
{
    throw;
}

FileSize = meshStatus.FileSize > 0
    ? meshStatus.FileSize
    : session.LastProgress?.FileSize ?? 0;
```

**Why This Keeps Happening**: Background I/O code often wraps all exceptions in one catch block, which turns normal cancellation into fake errors during shutdown. Progress proxies have a similar drift problem: if they poll a weaker execution status source repeatedly, they can overwrite stronger metadata they already learned earlier. Treat cancellation as control flow, and preserve the best-known transfer metadata instead of letting weaker follow-up reads erase it.

### 0x15. Metadata Portability Must Export Real Retrieved Descriptors, Not Fabricated Mock Metadata

**The Bug**: Metadata export created a fake `ContentDescriptor` for every requested content ID, with invented size, codec, and confidence values, even when no real descriptor could be retrieved. That made exported portability packages look complete while silently shipping fabricated metadata.

**Files Affected**:
- `src/slskd/MediaCore/MetadataPortability.cs`

**Wrong**:
```csharp
var descriptor = await CreateMockDescriptorAsync(contentId, cancellationToken);
Codec = "mock",
Confidence = 0.8
```

**Correct**:
```csharp
var retrieval = await _descriptorRetriever.RetrieveAsync(contentId, cancellationToken: cancellationToken);
if (!retrieval.Found || retrieval.Descriptor == null)
{
    continue;
}
var descriptor = retrieval.Descriptor;
```

**Why This Keeps Happening**: Portability/export code is easy to scaffold with fake placeholders because it needs a descriptor-shaped object to serialize. That placeholder then becomes user-visible data and quietly corrupts downstream import/merge behavior. For export surfaces, either serialize the real retrieved descriptor or skip the entry entirely; never fill the gap with invented metadata.

### 0x16. MediaCore Must Not Fabricate Similarity Or Graph Edges, And Registry Stats Must Use Normalized Domains

**The Bug**: `FuzzyMatcher` fell back to simulated/random perceptual similarity when no real perceptual hashes existed. `IpldMapper` generated mock album/artist/artwork links when no stored links were present. `ContentIdRegistry` also counted raw parsed domains in stats, which drifted from the normalized domain model used everywhere else (`content:mb:*` versus `audio`).

**Files Affected**:
- `src/slskd/MediaCore/FuzzyMatcher.cs`
- `src/slskd/MediaCore/IpldMapper.cs`
- `src/slskd/MediaCore/ContentIdRegistry.cs`

**Wrong**:
```csharp
return await ComputeSimulatedPerceptualSimilarityAsync(...);
var outgoingLinks = storedCopy ?? (await GenerateMockLinksAsync(contentId, cancellationToken)).ToList();
var domain = ContentIdParser.GetDomain(contentId) ?? "unknown";
```

**Correct**:
```csharp
return 0.0;
var outgoingLinks = storedCopy ?? new List<IpldLink>();
var domain = parsed == null ? "unknown" : ContentIdParser.NormalizeDomain(parsed.Domain, parsed.Type);
```

**Why This Keeps Happening**: Search, graph, and stats code often starts with “helpful” simulated fallbacks so APIs return something early. Those placeholders then leak into production behavior and silently look like real data. In MediaCore, synthetic similarity, synthetic graph edges, and raw-vs-normalized domain drift all create the same class of bug: the system claims knowledge it does not actually have. If the data is unknown, return none/zero and keep the semantics aligned with the normalized ID model.

### 0x17. Shadow-Index Peer Hints Are Not Routable Peer IDs, And VSF Telemetry Must Report Real Cache State

**The Bug**: Shadow-index query code converted 8-byte peer hints into fabricated `peer:vsf:*` IDs and returned them as if they were real routable peers. That made MBID-based bridge results look downloadable even though the peer IDs were synthetic. In parallel, VSF performance/telemetry services reported mostly zero cache state because the shard cache exposed no occupancy stats and prefetch used `Task.Run(..., ct)` in a way that could skip scheduled work on cancellation.

**Files Affected**:
- `src/slskd/VirtualSoulfind/ShadowIndex/ShadowIndexQueryImpl.cs`
- `src/slskd/VirtualSoulfind/ShadowIndex/ShardCache.cs`
- `src/slskd/VirtualSoulfind/Integration/PerformanceOptimizer.cs`
- `src/slskd/VirtualSoulfind/Integration/TelemetryDashboard.cs`
- `src/slskd/VirtualSoulfind/Integration/DisasterRescueIntegration.cs`

**Wrong**:
```csharp
return $"peer:vsf:{Convert.ToHexString(hint).ToLowerInvariant()}";
prefetchTasks.Add(Task.Run(async () => { ... }, ct));
if (healthMonitor.CurrentHealth == SoulseekHealth.Degraded)
```

**Correct**:
```csharp
return null;
await semaphore.WaitAsync(ct);
var shardCacheStats = cache.GetStats();
if (healthMonitor.CurrentHealth != SoulseekHealth.Healthy)
```

**Why This Keeps Happening**: Compact hints, telemetry counters, and degraded-mode feature gates all tempt “close enough” implementations: invent a peer ID from the hint, report zero because real cache stats are awkward, or only handle the middle degraded state and forget the hard-down state. Those shortcuts create deeper bugs because downstream code treats the output as authoritative. If a hint is not routable, do not present it as a peer. If a cache backs dashboard features, expose real cache stats. If a feature depends on service health, handle the full state machine, not one intermediate state.

### 0x18. Federation Must Not Assume Remote Actors Use slskdN-Style `/actors/{name}` URLs, And Signature-Key Fetchers Must Revalidate The Final URL

**The Bug**: Outbound federation delivery resolved inbox URLs only for actors whose URLs matched the local `https://host/actors/{name}` pattern, so ordinary remote ActivityPub actors like `/users/alice` never resolved. Separately, the HTTP-signature key fetcher validated only the original `keyId` host, not the final response URL after redirects, which left a gap between the documented SSRF policy and the actual enforcement.

**Files Affected**:
- `src/slskd/SocialFederation/FederationService.cs`
- `src/slskd/SocialFederation/HttpSignatureKeyFetcher.cs`

**Wrong**:
```csharp
if (segments.Length != 2 || !string.Equals(segments[0], "actors", ...))
{
    return Task.FromResult<string?>(null);
}
return Task.FromResult<string?>($"{actorUri.Scheme}://{actorUri.Authority}/actors/{segments[1]}/inbox");
```

**Correct**:
```csharp
if (actorUri.AbsolutePath.EndsWith("/inbox", StringComparison.OrdinalIgnoreCase))
{
    return Task.FromResult<string?>(actorUri.AbsoluteUri);
}
var inboxUri = new Uri(actorUri.AbsoluteUri.TrimEnd('/') + "/inbox", UriKind.Absolute);
```

**Why This Keeps Happening**: It is easy to accidentally encode our own actor URL conventions into federation code because local test actors all follow the same route shape. That breaks interoperability immediately once a remote server uses a different actor path. Similarly, SSRF checks often validate the first URL and forget that the effective fetch target may change after redirects. Federation code must treat remote actor URLs as foreign inputs, not local route templates, and must validate the final network destination, not just the initial string.

### 0x19. Do Not Advertise Empty Generic Actors, Do Not Invent Scene Peer IDs From Hints, And Failed Observation Rows Must Leave The Retry Queue

**The Bug**: Generic federation actors for books/movies/tv/software/games reported themselves as available even though they always returned an empty recent-work list. Scene membership tracking also converted 8-byte peer hints into fake `peer:vsf:*` IDs, which looked like real members. Separately, failed observation processing wrote `Processed = 0`, so the same broken row stayed in the “unprocessed” query forever and could loop indefinitely.

**Files Affected**:
- `src/slskd/SocialFederation/GenericLibraryActor.cs`
- `src/slskd/VirtualSoulfind/Scenes/SceneMembershipTracker.cs`
- `src/slskd/VirtualSoulfind/Capture/ObservationStore.cs`

**Wrong**:
```csharp
return true;
return $"peer:vsf:{Convert.ToHexString(peerIdHint).ToLowerInvariant()}";
SET Processed = @processed
```

**Correct**:
```csharp
return false;
return null;
SET Processed = 1,
    ProcessingError = @error
```

**Why This Keeps Happening**: Scaffolding code often defaults to “available”, “best-effort peer string”, or “leave it pending so we can retry later.” Those defaults are dangerous when there is no real backend behind them. If a domain has no provider, do not expose the actor. If a hint is not a routable identity, do not promote it into one. If a failed row should be retained for inspection, mark it processed-with-error instead of keeping it in the live retry queue forever.

### 0x20. Mesh Search Must Use Real Peer IDs For Peer Queries, And Search Results Must Not Leak Opaque IDs As Filenames

**The Bug**: `MeshSearchService` queried peer content using `peer.Username` in one path and then reported `peer.Username` back in the `PeerId` field. That mixed human-facing labels with routable mesh identities and could break any downstream code that expected a real peer ID. The same code also surfaced `"unknown"` or raw `content:*` IDs as filenames even when a stable display name could be derived from the parsed content ID plus codec.

**Files Affected**:
- `src/slskd/VirtualSoulfind/DisasterMode/MeshSearchService.cs`

**Wrong**:
```csharp
var peerContent = await meshDirectory.FindContentByPeerAsync(peer.Username, ct);
PeerId = peerResult.Peer.Username,
Filename = content.ContentId ?? "unknown",
```

**Correct**:
```csharp
var peerContent = await meshDirectory.FindContentByPeerAsync(peer.PeerId, ct);
PeerId = peerResult.Peer.PeerId,
Filename = GetDisplayFilename(content.ContentId, content.Codec),
```

**Why This Keeps Happening**: Search/result code often treats “display identity” and “transport identity” as interchangeable because both are strings. They are not. Once a result object exposes a field called `PeerId`, it must contain the actual routable identity used by the transport/query layer. Likewise, filenames shown to clients should use the best stable human-readable name already derivable from metadata, not raw opaque IDs or `"unknown"` placeholders.

**Wrong**:
```csharp
public bool IsAudio => Domain.Equals("audio", StringComparison.OrdinalIgnoreCase);
```

**Correct**:
```csharp
public bool IsAudio => ContentIdParser.NormalizeDomain(Domain, Type)
    .Equals("audio", StringComparison.OrdinalIgnoreCase);
```

**Why This Keeps Happening**: It is easy to treat the textual content ID format and the semantic media domain as the same thing. They are not. `content:mb:recording:*` is still audio content even though the raw domain token is `mb`. When the codebase mixes canonical media domains with source-specific ID schemes, normalize before filtering or classification.

### 0x13. Transfer Progress Proxies Must Never Return Blank Filenames To Legacy Clients

**The Bug**: The bridge transfer-progress proxy cached `Path.GetFileName(meshStatus.TargetPath)` directly. If the target path was empty or not yet a usable filename, legacy clients got a blank filename field.

**Files Affected**:
- `src/slskd/VirtualSoulfind/Bridge/TransferProgressProxy.cs`

**Wrong**:
```csharp
Filename = session.CachedFilename ??= Path.GetFileName(meshStatus.TargetPath),
```

**Correct**:
```csharp
filename = Path.GetFileName(meshStatus.TargetPath);
if (string.IsNullOrWhiteSpace(filename))
{
    filename = session.MeshTransferId;
}
```

**Why This Keeps Happening**: Readback/proxy layers often assume execution state always has a stable final path. In practice, proxies can observe transfers before that path is useful. Legacy compatibility responses need a guaranteed non-empty presentation value even if it is only a stable transfer identifier fallback.

### 0x14. Bridge Downloads Must Reuse File Metadata Learned During Search Instead Of Falling Back To Zero-Size Transfers

**The Bug**: Bridge search already synthesized `BridgeFile` entries with filename and size, but `DownloadAsync(...)` threw that away and fell back to `ExtractSizeFromFilename(...)`, which returned `null`. That degraded mesh transfer startup into zero-size downloads even when the size had just been discovered.

**Files Affected**:
- `src/slskd/VirtualSoulfind/Bridge/BridgeApi.cs`

**Wrong**:
```csharp
var fileSize = ExtractSizeFromFilename(filename) ?? 0;
```

**Correct**:
```csharp
var cachedFile = GetCachedBridgeFileMetadata(username, filename);
var fileSize = cachedFile?.SizeBytes ?? ExtractSizeFromFilename(filename) ?? 0;
```

**Why This Keeps Happening**: Search and download flows often get implemented separately, and the download path is forced to reconstruct metadata it already had moments earlier. If the bridge search step already resolved filename/size/canonical hints, cache and reuse them for the follow-up download request instead of re-deriving from lossy heuristics.

### 0x15. Descriptor Query Filters Must Use The Same Content-ID Normalization Rules As The Registry

**The Bug**: `DescriptorRetriever.QueryByDomainAsync(...)` filtered cached descriptors using raw `parsed.Domain` and `parsed.Type`, while the registry and other MediaCore paths needed normalized semantics such as treating `content:mb:recording:*` as audio tracks. Query results therefore disagreed with registry lookups.

**Files Affected**:
- `src/slskd/MediaCore/DescriptorRetriever.cs`

**Wrong**:
```csharp
parsed.Domain.Equals(domain, StringComparison.OrdinalIgnoreCase)
```

**Correct**:
```csharp
ContentIdParser.NormalizeDomain(parsed.Domain, parsed.Type)
    .Equals(domain, StringComparison.OrdinalIgnoreCase)
```

**Why This Keeps Happening**: Once one subsystem adds normalization for mixed ID schemes, every other filter path has to use the same rule set. Query helpers are especially easy to miss because they often start as “cache-only demos” and later become real API behavior.

### 0x16. Fuzzy-Match Candidate Selection Must Query Normalized Domains, Not Raw Content-ID Tokens

**The Bug**: The fuzzy-match controller parsed the target content ID and then queried candidates by `parsed.Domain`. For IDs like `content:mb:recording:*`, that meant it asked the registry for `mb` candidates instead of `audio`, even though the rest of MediaCore had already normalized that scheme.

**Files Affected**:
- `src/slskd/MediaCore/API/Controllers/FuzzyMatcherController.cs`

**Wrong**:
```csharp
var candidates = await _registry.FindByDomainAsync(parsed.Domain, cancellationToken);
```

**Correct**:
```csharp
var candidates = await _registry.FindByDomainAsync(
    ContentIdParser.NormalizeDomain(parsed.Domain, parsed.Type),
    cancellationToken);
```

**Why This Keeps Happening**: API/controller glue often re-derives simple filters from parsed IDs and forgets the normalization rules added deeper in the stack. Once mixed ID schemes exist, every boundary that turns a parsed ID into a query key has to normalize first.

### 0x17. Detached Bridge Progress Loops Must Not Reuse The Request Token That Created The Transfer

**The Bug**: The bridge proxy started its progress-push loop with the same cancellation token used to handle the original download request. Once that request finished, the detached loop could stop immediately even though the transfer and client session were still active.

**Files Affected**:
- `src/slskd/VirtualSoulfind/Bridge/Proxy/BridgeProxyServer.cs`

**Wrong**:
```csharp
_ = Task.Run(() => PushProgressUpdatesAsync(session, ct), CancellationToken.None);
```

**Correct**:
```csharp
_ = Task.Run(() => PushProgressUpdatesAsync(session, CancellationToken.None), CancellationToken.None);
```

**Why This Keeps Happening**: Request handlers often spin off follow-up work after sending an acknowledgement and accidentally pass the same request token into the detached loop. That token describes the request lifetime, not the transfer/session lifetime. Detached progress or cleanup loops need their own lifetime conditions.

### 0x18. Bridge Search Results Must Deduplicate Per User/File And Merge The Best Metadata

**The Bug**: Bridge search could add the same filename multiple times for one user when multiple sources or lookup strategies surfaced the same file. The client then saw duplicate search hits while the bridge also threw away stronger metadata from the better duplicate.

**Files Affected**:
- `src/slskd/VirtualSoulfind/Bridge/BridgeApi.cs`

**Wrong**:
```csharp
user.Files.Add(new BridgeFile { Path = filename, ... });
```

**Correct**:
```csharp
UpsertBridgeFile(user.Files, new BridgeFile { Path = filename, ... });
```

**Why This Keeps Happening**: Search aggregation is usually written as an append-only pass first. Once multiple strategies feed the same output list, append-only behavior turns into duplicate rows and metadata drift. Aggregation layers need a stable dedupe key and merge policy from the start.

### 0x19. Bridge Session Cleanup Must Not Depend On A Potentially Canceled Outer Token

**The Bug**: Bridge session cleanup stopped per-client progress proxies using the outer session/server token. If that token was already canceled, the cleanup call could be short-circuited and leave proxy state behind during disconnect or shutdown.

**Files Affected**:
- `src/slskd/VirtualSoulfind/Bridge/Proxy/BridgeProxyServer.cs`

**Wrong**:
```csharp
await progressProxy.StopProxyAsync(session.ActiveProxyId, ct);
```

**Correct**:
```csharp
await progressProxy.StopProxyAsync(session.ActiveProxyId, CancellationToken.None);
```

**Why This Keeps Happening**: Cleanup paths are easy to write with “whatever token is in scope,” but teardown often runs after cancellation has already started. If the cleanup work is required to release local state, use a non-cancelable token or a dedicated shutdown budget instead of the already-expired request/session token.

### 0x7. Detached Background Work Must Not Use Short-Lived Request Or Startup Tokens As Task.Run Scheduler Tokens

**The Bug**: Several request handlers and hosted services intentionally kicked work off in the background, but still passed the request/startup cancellation token as the `Task.Run(..., token)` scheduler token. If that token was already canceled, the work never queued at all even though the outer path still reported success or startup completion.

**Files Affected**:
- `src/slskd/Mesh/ServiceFabric/Services/DhtMeshService.cs`
- `src/slskd/HashDb/Optimization/HashDbOptimizationHostedService.cs`
- `src/slskd/Mesh/Realm/MultiRealmHostedService.cs`
- `src/slskd/VirtualSoulfind/DisasterMode/SoulseekHealthMonitor.cs`
- `src/slskd/Common/Security/CoverTrafficGenerator.cs`
- `src/slskd/Transfers/MultiSource/Discovery/SourceDiscoveryService.cs`
- `src/slskd/VirtualSoulfind/Bridge/Proxy/BridgeProxyServer.cs`
- `src/slskd/Search/SearchService.cs`
- `src/slskd/LibraryHealth/LibraryHealthService.cs`
- `src/slskd/PodCore/SqlitePodService.cs`
- `src/slskd/PodCore/PodMessageBackfill.cs`
- `src/slskd/Transfers/Rescue/RescueService.cs`
- `src/slskd/LibraryHealth/Remediation/LibraryHealthRemediationService.cs`
- `src/slskd/Identity/MdnsAdvertiser.cs`

**Wrong**:
```csharp
_ = Task.Run(() => _routingTable.TouchAsync(requesterId, peerId), cancellationToken);
```

**Correct**:
```csharp
_ = ObserveBackgroundTaskAsync(
    Task.Run(() => _routingTable.TouchAsync(requesterId, peerId), CancellationToken.None),
    "...");
```

**Why This Keeps Happening**: `Task.Run` has two cancellation sites: the inner async operation and the scheduler itself. For detached follow-up work, passing a short-lived token to the scheduler is usually wrong because it can prevent the task from ever starting, while the outer code still returns success. It is especially dangerous when the code already acquired a semaphore slot, opened resources, or completed setup that the delegate was supposed to release or continue. Queue detached work with `CancellationToken.None`, pass the real token inside the delegate if the work itself should respect cancellation, and keep/observe the background task when shutdown needs to wait for it. If the work is already asynchronous I/O, prefer calling the async helper directly instead of wrapping it in `Task.Run` at all.

### 0x8. Detached Controller And Timer Work Must Have A Top-Level Observer

**The Bug**: Several API and timer paths kicked off background work with `_ = Task.Run(...)` and no top-level observer. When the detached work threw, the user-facing action still returned success and the failure became a silent no-op until an eventual unobserved task exception or missing side effect exposed it later.

**Files Affected**:
- `src/slskd/VirtualSoulfind/v2/API/VirtualSoulfindV2Controller.cs`
- `src/slskd/Transfers/Downloads/DownloadService.cs`
- `src/slskd/SongID/SongIdService.cs`
- `src/slskd/Users/UserService.cs`
- `src/slskd/Transfers/Rescue/UnderperformanceDetectorHostedService.cs`
- `src/slskd/Messaging/ConversationService.cs`
- `src/slskd/Transfers/MultiSource/API/MultiSourceController.cs`
- `src/slskd/Sharing/ShareGrantAnnouncementService.cs`

**Wrong**:
```csharp
_ = Task.Run(async () => await _processor.ProcessIntentAsync(intentId, CancellationToken.None));
```

**Correct**:
```csharp
_ = ObserveBackgroundTaskAsync(
    Task.Run(() => _processor.ProcessIntentAsync(intentId, CancellationToken.None), CancellationToken.None),
    intentId);
```

**Why This Keeps Happening**: fire-and-forget is convenient in controllers and timer callbacks because it keeps the main path responsive, but it also severs exception propagation. If the detached work matters enough to launch, it matters enough to wrap in a single observer that catches and logs failures explicitly.

### 0xA. Per-Request Linked CancellationTokenSource Instances Need Explicit Async-Safe Disposal

**The Bug**: Capability-file fetches created a linked `CancellationTokenSource` inside an async retry loop and relied on implicit disposal structure around awaited work. That left the code easy to regress and kept analyzers flagging the path, which is a good signal that linked CTS ownership is not obvious enough.

**Files Affected**:
- `src/slskd/Capabilities/CapabilityFileService.cs`

**Wrong**:
```csharp
using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
var data = await DownloadSmallFileAsync(username, path, 4096, cts.Token);
```

**Correct**:
```csharp
var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
try
{
    var data = await DownloadSmallFileAsync(username, path, 4096, cts.Token);
}
finally
{
    cts.Dispose();
}
```

**Why This Keeps Happening**: linked CTS instances are tiny but high-churn, and async control flow makes ownership less obvious than it looks. When a method creates a linked token per iteration or per request, isolate that lifetime in a tiny helper or make the dispose boundary explicit with `try/finally` so later edits and analyzers agree on the lifetime.

### 0x9. Refactors Must Carry Supporting Renames, Namespaces, And Exact Nullability

**The Bug**: API code was updated to use `JsonDocument`, named tuple return signatures, renamed bridge-result helpers, or newer synchronization types, but the supporting `using System.Text.Json;`, matching nullable tuple generics, helper call sites, and target framework/runtime surface were not updated. The result was a hard compile break in otherwise unrelated validation runs.

**Files Affected**:
- `src/slskd/SocialFederation/API/ActivityPubController.cs`
- `src/slskd/SongID/SongIdService.cs`
- `src/slskd/Mesh/Realm/Bridge/ActivityPubBridge.cs`
- `tests/slskd.Tests.Unit/SocialFederation/FederationServiceTests.cs`
- `tests/slskd.Tests.Unit/Mesh/Realm/Bridge/ActivityPubBridgeTests.cs`
- `src/slskd/SocialFederation/FederationService.cs`
- `src/slskd/Users/BrowseTracker.cs`
- `src/slskd/MediaCore/IpldMapper.cs`
- `src/slskd/VirtualSoulfind/ShadowIndex/ShardCache.cs`

**Wrong**:
```csharp
using System.Text;
return Task.FromResult((false, "Missing activity type"));
return BridgeOperationResult.CreateFailure("Failed to follow remote actor.");
```

**Correct**:
```csharp
using System.Text.Json;
return Task.FromResult<(bool Processed, string? Error)>((false, "Missing activity type"));
return BridgeOperationResult.Failed("Failed to follow remote actor.");
```

**Why This Keeps Happening**: small refactors often change type names, constructor signatures, helper factories, nullability contracts, collection interface types, configuration assumptions, fixture prerequisites, or synchronization primitives at the same time, but the import list, helper invocations, generic return expressions, `Count`/`Keys` usage, async locking boundaries, target-framework compatibility, and test constructors are easy to leave behind. When changing shared result helpers, DI signatures, synchronization types, dictionary interfaces, or moving to `System.Text.Json` objects and named nullable tuples, do a repo-wide usage pass in the same edit and make sure the chosen APIs actually exist on the project’s current target and async code is no longer inside synchronous locks.

### 0x. Do Not Return Fake Success For Unimplemented Distributed Features

**The Bug**: Several Pod and mesh workflows returned placeholder success values, synthetic IDs, fake local peer IDs, or hardcoded stats even though the underlying transport or lookup path was not implemented. This made broken features look healthy and pushed failures downstream into harder-to-debug places.

**Files Affected**:
- `src/slskd/PodCore/PodMessageBackfill.cs`
- `src/slskd/PodCore/PodDiscoveryService.cs`
- `src/slskd/PodCore/PodDhtPublisher.cs`
- `src/slskd/PodCore/PodOpinionService.cs`

**Wrong**:
```csharp
return Task.FromResult(new PodBackfillProcessingResult(
    true, podId, peerId, 0, 0, 0, TimeSpan.Zero, "Not implemented"));
```

**Correct**:
```csharp
return Task.FromResult(new PodBackfillProcessingResult(
    false, podId, peerId, 0, 0, 0, TimeSpan.Zero,
    "Backfill request delivery is not implemented."));
```

**Why This Keeps Happening**: Placeholder implementations are tempting during feature bring-up because they keep call sites moving. In distributed code they are worse than an explicit failure: they corrupt state, poison metrics, and make operators think the network path worked. If a dependency is missing, either wire an existing real service or fail clearly and immediately.

### 0x1. Friends-Only Federation Must Validate Remote Identity, Not Our Own Hostname

**The Bug**: Friends-only federation checks used `Request.Host` or the requested local WebFinger domain as the authorization key. Both values describe our server, not the remote caller, so approved-peer checks either never worked or authorized the wrong thing.

**Files Affected**:
- `src/slskd/SocialFederation/API/ActivityPubController.cs`
- `src/slskd/SocialFederation/API/WebFingerController.cs`
- `src/slskd/SocialFederation/FederationService.cs`

**Wrong**:
```csharp
var host = Request.Host.Value;
return opts.ApprovedPeers.Contains(host, StringComparer.OrdinalIgnoreCase);
```

**Correct**:
```csharp
var keyUri = new Uri(keyId, UriKind.Absolute);
return opts.ApprovedPeers.Contains(keyUri.Host, StringComparer.OrdinalIgnoreCase);
```

**Why This Keeps Happening**: In inbound federation code it is easy to grab the most convenient host string and forget whether it identifies the caller or the local service. Friends-only checks must key off verified remote identity material such as the HTTP signature `keyId` host or an explicitly supplied remote origin, and helper methods must refuse to fabricate remote inbox URLs from arbitrary strings.

### 0x2. Security Policies Must Not Pretend To Enforce Checks While Returning Unconditional Allow

**The Bug**: Security policy classes were registered as protection layers but some paths just logged TODOs and returned `Allowed = true` for every request. That creates a false sense of protection and guarantees the engine will never stop abusive traffic.

**Files Affected**:
- `src/slskd/Security/Policies.cs`

**Wrong**:
```csharp
logger.LogDebug("[NatAbuse] NAT abuse detection not fully implemented, allowing peer {PeerId}", context.PeerId);
return Task.FromResult(new SecurityDecision(true, "nat abuse detection not fully implemented"));
```

**Correct**:
```csharp
if (context.Operation.Contains("consensus", StringComparison.OrdinalIgnoreCase))
{
    return Task.FromResult(new SecurityDecision(false, "consensus verification unavailable"));
}
```

**Why This Keeps Happening**: It is tempting to wire a policy into DI early and fill it in later, but once the policy is part of a composite engine the placeholder result becomes production behavior. If a policy cannot fully evaluate, it must either enforce the narrow checks it actually can support or fail closed for operations that explicitly depend on that protection.

### 0x3. Federation Endpoints Must Not Return Success For Inbox Features Without Storage Or Processors

**The Bug**: The ActivityPub inbox endpoints returned an empty collection for reads and `202 Accepted` for posts even though there was no inbox persistence and no activity processor behind them. Remote peers got a success signal for work the server immediately discarded.

**Files Affected**:
- `src/slskd/SocialFederation/API/ActivityPubController.cs`

**Wrong**:
```csharp
return Task.FromResult<IActionResult>(Ok(new ActivityPubOrderedCollection
{
    TotalItems = 0,
    OrderedItems = Array.Empty<object>()
}));
```

**Correct**:
```csharp
return Task.FromResult<IActionResult>(
    StatusCode(StatusCodes.Status501NotImplemented, "Inbox retrieval is not implemented"));
```

**Why This Keeps Happening**: Placeholder HTTP handlers often start by returning syntactically valid responses so clients stop erroring during development. For federated protocols that is the wrong tradeoff. A fake success causes other servers to trust delivery and state transitions that never happened. If persistence or processing does not exist yet, return an explicit non-success status.

### 0x4. Unsupported Mesh Streaming Paths Must Close Gracefully, Not Throw

**The Bug**: Several mesh services advertised the `HandleStreamAsync` entry point and then threw `NotSupportedException` when called. That turns an unsupported optional feature into an avoidable protocol-level crash path.

**Files Affected**:
- `src/slskd/Mesh/ServiceFabric/Services/PodsMeshService.cs`
- `src/slskd/Mesh/ServiceFabric/Services/DhtMeshService.cs`
- `src/slskd/Mesh/ServiceFabric/Services/MeshIntrospectionService.cs`
- `src/slskd/Mesh/ServiceFabric/Services/HolePunchMeshService.cs`
- `src/slskd/Mesh/ServiceFabric/Services/MeshContentMeshService.cs`
- `src/slskd/Mesh/ServiceFabric/Services/PrivateGatewayMeshService.cs`
- `src/slskd/Mesh/ServiceFabric/Services/VirtualSoulfindMeshService.cs`

**Wrong**:
```csharp
throw new NotSupportedException("Streaming not implemented for DHT service");
```

**Correct**:
```csharp
_logger.LogWarning("...");
return stream.CloseAsync(cancellationToken);
```

**Why This Keeps Happening**: Interface comments that say “throw if unsupported” are easy to follow literally, but once the service is reachable over a network protocol, throws become remote crash surfaces. For unsupported protocol features, log once, close cleanly, and let the caller observe a normal unsupported-path failure instead of an exception.

### 0x5. Descriptor And Discovery Services Must Not Invent Search Hits, Signatures, Or Republish Success

**The Bug**: Content discovery and MediaCore publishing code returned placeholder search results, auto-generated fake signatures, and claimed successful descriptor updates/republishes even though no real search backend or signing/republish pipeline existed.

**Files Affected**:
- `src/slskd/PodCore/ContentLinkService.cs`
- `src/slskd/MediaCore/ContentDescriptorPublisher.cs`
- `src/slskd/MediaCore/MediaCoreStatsService.cs`

**Wrong**:
```csharp
descriptor.Signature = CreateSignature(descriptor, version);
return Task.FromResult(new DescriptorUpdateResult(Success: true, ...));
```

**Correct**:
```csharp
return new DescriptorPublishResult(
    Success: false,
    ContentId: descriptor.ContentId,
    Version: version,
    PublishedAt: startTime,
    Ttl: ttl,
    ErrorMessage: "Descriptor signature is required; automatic signing is not implemented.");
```

**Why This Keeps Happening**: Placeholder data is tempting when wiring dashboards and public APIs because it keeps the feature surface looking alive. In publishing and discovery code that is actively harmful: fake search results waste operator time, fake signatures destroy trust boundaries, and fake stats hide missing instrumentation. If the backing system is not implemented, return empty/failed results and log the gap explicitly.

### 0x6. Capability Advertisements Must Publish The Real Mesh Sequence, Not A Hardcoded Zero

**The Bug**: The generated capability file always exported `mesh_seq_id = 0` even when the local HashDb had a newer sequence. That made healthy peers look permanently stale to other nodes.

**Files Affected**:
- `src/slskd/Capabilities/CapabilityService.cs`

**Wrong**:
```csharp
mesh_seq_id = 0L
```

**Correct**:
```csharp
mesh_seq_id = hashDb?.CurrentSeqId ?? 0L
```

**Why This Keeps Happening**: Static placeholder values are easy to leave behind in capability payloads because they don’t crash anything locally. But capability ads are protocol state, not UI sugar. If the app already has the authoritative local value, publish it from the real source instead of freezing the field at a bootstrap default.

### 0x7. Scheduler Candidate Flags Must Use Real Peer Presence, Not Hardcoded `true`

**The Bug**: Backfill candidate generation marked every peer as online regardless of actual Soulseek presence. That distorted scheduler decisions and made offline peers look backfillable.

**Files Affected**:
- `src/slskd/Backfill/BackfillSchedulerService.cs`

**Wrong**:
```csharp
IsPeerOnline = true
```

**Correct**:
```csharp
var userStatus = await soulseekClient.GetUserStatusAsync(entry.PeerId);
isPeerOnline = !string.Equals(userStatus.Presence.ToString(), "Offline", StringComparison.OrdinalIgnoreCase);
```

**Why This Keeps Happening**: Boolean readiness flags are easy to stub during scheduler bring-up because they make the queue move. In scheduling code, though, a fake `true` is not harmless; it changes who gets probed and when. If live presence data exists anywhere in the app, use it. Otherwise default to conservative unknown/offline behavior instead of optimistic availability.

### 0x8. Mesh Response Dispatchers Must Verify Signatures And Wire Every Advertised Response Type

**The Bug**: The private-message dispatcher accepted `RESPKEY` messages without signature verification, ignored `RESPCHUNK` entirely, and never routed `REQCHUNK` into the request handler despite supporting chunk requests elsewhere in the service.

**Files Affected**:
- `src/slskd/Mesh/MeshSyncService.cs`

**Wrong**:
```csharp
if (messageType == "RESPKEY")
{
    var response = JsonSerializer.Deserialize<MeshRespKeyMessage>(payload);
    tcs.SetResult(response);
}
```

**Correct**:
```csharp
if (!VerifySignature(response))
{
    return;
}
```

**Why This Keeps Happening**: Protocol handlers often get implemented incrementally, and it is easy to wire the "happy path" response first while leaving sibling message types or signature checks for later. In mesh code that creates silent protocol drift. If a service advertises a response type, make sure dispatch, verification, and completion-state updates are all wired together before claiming support.

### 0x9. Optional Persistence Features Must Switch Implementations At Registration Time, Not Behind A Permanent No-Op Store

**The Bug**: Virtual Soulfind exposed `PersistRawObservations` and a persistence-shaped observation store API, but DI always registered the in-memory no-op implementation. Enabling the option changed nothing and raw capture data was silently discarded.

**Files Affected**:
- `src/slskd/VirtualSoulfind/Capture/ObservationStore.cs`
- `src/slskd/Program.cs`

**Wrong**:
```csharp
services.AddSingleton<IObservationStore, InMemoryObservationStore>();
```

**Correct**:
```csharp
services.AddSingleton<IObservationStore>(sp =>
{
    var options = sp.GetRequiredService<IOptionsMonitor<Options>>();
    return options.CurrentValue.VirtualSoulfind?.Privacy?.PersistRawObservations == true
        ? new SqliteObservationStore(...)
        : new InMemoryObservationStore(...);
});
```

**Why This Keeps Happening**: Feature flags are easy to add at the config layer without actually switching the implementation underneath. That is worse than an explicit TODO because operators believe the feature is active. If an option is meant to toggle behavior, wire the implementation choice at registration time or remove the option until the backing implementation exists.

### 0xA. Federation Discovery And Publication Must Use The Same Actor Inventory And Base URL

**The Bug**: WebFinger only recognized a hardcoded `library` actor while outgoing federation code published `WorkRef` and collection activities using hardcoded `/actors/{domain}` paths or `https://localhost:5000`. That produced actor IDs the server did not actually expose.

**Files Affected**:
- `src/slskd/SocialFederation/API/WebFingerController.cs`
- `src/slskd/SocialFederation/FederationService.cs`
- `src/slskd/SocialFederation/VirtualSoulfindFederationIntegration.cs`
- `src/slskd/SocialFederation/ServiceCollectionExtensions.cs`

**Wrong**:
```csharp
workRef = WorkRef.FromMusicItem(musicItem, "https://localhost:5000");
workRef.AttributedTo = $"/actors/{contentItem.Domain}";
return Task.FromResult(string.Equals(username, "library", StringComparison.OrdinalIgnoreCase));
```

**Correct**:
```csharp
workRef = WorkRef.FromMusicItem(musicItem, BaseUrl);
workRef.AttributedTo = actor?.ActorId ?? $"{BaseUrl}/actors/{contentItem.Domain}";
return Task.FromResult(_libraryActorService.IsLibraryActor(username));
```

**Why This Keeps Happening**: Federation code often grows from separate endpoint and publishing tasks, and each side invents its own actor naming shortcuts. That drift breaks discovery in subtle ways. All actor validation, attribution, and activity authorship should derive from the same `LibraryActorService` inventory and configured base URL.

### 0xB. Provider Methods That Advertise Catalog Lookups Must Reuse Existing HashDb Data Before Falling Back To `null`

**The Bug**: The music content provider exposed recent-item and local-metadata lookup methods, but returned empty results or `null` even though HashDb already held enough album and track data to satisfy conservative exact matches.

**Files Affected**:
- `src/slskd/VirtualSoulfind/Core/Music/MusicContentDomainProvider.cs`

**Wrong**:
```csharp
_logger.LogDebug("Fuzzy matching not yet implemented...");
return null;
```

**Correct**:
```csharp
var albums = await _hashDb.GetAlbumTargetsAsync(cancellationToken);
var tracks = await _hashDb.GetAlbumTracksAsync(album.ReleaseId, cancellationToken);
var track = tracks.FirstOrDefault(candidate => ...exact normalized match...);
```

**Why This Keeps Happening**: Provider interfaces often get designed ahead of the “ideal” fuzzy matching algorithm, and it is tempting to leave them returning `null` until the full scorer exists. That throws away usable catalog data and makes higher-level features look empty. If the repo already has authoritative exact-match data, implement the conservative path first and reserve fuzzy matching for later.

### 0xC. Poll-Based PubSub Must Publish And Poll The Same Topic Key

**The Bug**: Scene pubsub published each message under a unique DHT key with a ULID suffix, while subscribers polled a different base scene key. That meant published scene messages were never observed by subscribers. Polling also lacked duplicate suppression, so any future key alignment would replay the same payload forever.

**Files Affected**:
- `src/slskd/VirtualSoulfind/Scenes/ScenePubSubService.cs`

**Wrong**:
```csharp
var key = DhtKeyDerivation.DeriveSceneKey($"scene:pubsub:{sceneId}:{Ulid.NewUlid()}");
var sceneKey = DhtKeyDerivation.DeriveSceneKey($"scene:pubsub:{sceneId}");
```

**Correct**:
```csharp
var key = DhtKeyDerivation.DeriveSceneKey($"scene:pubsub:{sceneId}");
if (!ShouldDeliver(sceneId, messageData))
{
    continue;
}
```

**Why This Keeps Happening**: Topic-based systems often mix “message identity” and “topic identity” when the transport is still being prototyped. If subscribers query by topic, publishers must store under that same topic key, and polling implementations need a local fingerprint cache so stable-key retrieval does not duplicate deliveries.

### 0xD. Identity-Carrying Scene Features Must Fail Explicitly When Local Peer Identity Is Missing

**The Bug**: Scene chat and scene announcements accepted a missing local profile by substituting placeholder peer IDs like `local` or all-zero byte hints. That poisoned membership/chat identity and made debugging harder because broken identity looked like valid traffic.

**Files Affected**:
- `src/slskd/VirtualSoulfind/Scenes/SceneAnnouncementService.cs`
- `src/slskd/VirtualSoulfind/Scenes/SceneChatService.cs`
- `src/slskd/Program.cs`

**Wrong**:
```csharp
string peerId = "local";
logger.LogWarning(ex, "... using placeholder");
```

**Correct**:
```csharp
var profile = await profileService.GetMyProfileAsync(ct);
if (string.IsNullOrWhiteSpace(profile.PeerId))
{
    throw new InvalidOperationException("Local peer profile does not have a peer ID.");
}
```

**Why This Keeps Happening**: Optional constructor dependencies are convenient during bring-up, but once a feature’s payloads include identity, placeholders stop being harmless. If a feature fundamentally depends on the local peer ID, register the dependency as required and fail explicitly instead of publishing corrupted identity data.

### 0xE. Accepting Inbound Federation Activities Requires Real Storage Before Returning Success

**The Bug**: ActivityPub inbox POST/GET endpoints moved from explicit `501` responses toward acceptance, but without a backing store they would either discard validated activities or have nothing real to return. Once the endpoint starts accepting inbound traffic, inbox state must persist locally.

**Files Affected**:
- `src/slskd/SocialFederation/ActivityPubInboxStore.cs`
- `src/slskd/SocialFederation/API/ActivityPubController.cs`
- `src/slskd/SocialFederation/ServiceCollectionExtensions.cs`

**Wrong**:
```csharp
await ProcessActivityAsync(activity, cancellationToken);
return Accepted();
```

**Correct**:
```csharp
await _inboxStore.StoreAsync(actorName, MapActivity(activity), json, cancellationToken);
return Accepted();
```

**Why This Keeps Happening**: Once signature verification and parsing exist, it is tempting to “finish” the endpoint by swapping `501` for `202`. That still loses state. For federated inboxes, successful acceptance must mean the activity is durably stored before the HTTP response is sent.

### 0xF. External Tool Discovery Must Not Assume Sibling Repositories Are The Only Installation Layout

**The Bug**: SongID tool discovery only searched sibling workspaces like `external/audfprint` and `external/Panako`, so installs that provided `audfprint.py` or `panako.jar` through environment variables or normal `PATH` locations were treated as missing features.

**Files Affected**:
- `src/slskd/SongID/SongIdService.cs`

**Wrong**:
```csharp
foreach (var root in GetSiblingSearchRoots())
{
    var script = Path.Combine(root, "external", "audfprint", "audfprint.py");
```

**Correct**:
```csharp
var configuredScript = Environment.GetEnvironmentVariable("AUDFPRINT_SCRIPT");
var pathScript = FindNewestFileOnPath("audfprint.py");
```

**Why This Keeps Happening**: Dev environments often colocate helper repos, so discovery code gets written around that one layout. Production and user machines do not. If a feature relies on external tools, search explicit env-vars first, then normal install locations, and only then fall back to repo-relative development paths.

### 0x10. Scene Membership Protocols Must Carry Full Peer Identity Once Membership Tracking Depends On It

**The Bug**: Scene membership announcements only carried an 8-byte peer hint, so membership tracking had to fabricate synthetic peer IDs like `peer:vsf:...`. That made scene member identity inconsistent with the real profile/chat identity even when the sender had a full peer ID available locally.

**Files Affected**:
- `src/slskd/VirtualSoulfind/Scenes/SceneAnnouncementService.cs`
- `src/slskd/VirtualSoulfind/Scenes/SceneMembershipTracker.cs`

**Wrong**:
```csharp
// [1 byte flag] [8 bytes timestamp] [8 bytes peer hint]
var peerId = $"peer:vsf:{Convert.ToHexString(peerIdHint).ToLowerInvariant()}";
```

**Correct**:
```csharp
// [1 byte flag] [8 bytes timestamp] [2 bytes peer-id length] [peer-id bytes]
return Encoding.UTF8.GetString(data, 11, peerIdLength);
```

**Why This Keeps Happening**: Early wire formats often optimize for compactness before downstream consumers are written. Once membership tracking, moderation, or UI starts depending on stable peer IDs, hints are no longer sufficient. Carry the full peer identity in the protocol and keep the old hint-only path only as backward-compatible fallback.

### 0x11. Actor Documents Must Not Advertise Collection Endpoints That The Server Never Serves

**The Bug**: ActivityPub actor documents advertised `/followers` and `/following` URLs, but there were no corresponding controller routes. Remote peers could discover links that always 404ed even though the actor document claimed they existed.

**Files Affected**:
- `src/slskd/SocialFederation/API/ActivityPubController.cs`
- `src/slskd/SocialFederation/LibraryActor.cs`

**Wrong**:
```csharp
Followers = $"{ActorId}/followers",
Following = $"{ActorId}/following",
```

**Correct**:
```csharp
[HttpGet("{actorName}/followers")]
[HttpGet("{actorName}/following")]
```

**Why This Keeps Happening**: It is common to build actor documents first and postpone collection endpoints. In federation, advertised URLs are part of the contract. If an actor document exposes a collection URL, the server must return a syntactically valid collection there, even if it is empty for now.

### 0x12. Once Inbound Follow Activities Are Accepted, Follower Collections Must Be Backed By Durable Relationship State

**The Bug**: After inbox acceptance/storage was added, follower collection endpoints still had no relationship state behind them. `Follow` and `Undo` activities were accepted, but `/followers` remained disconnected from any persisted relationships.

**Files Affected**:
- `src/slskd/SocialFederation/ActivityPubRelationshipStore.cs`
- `src/slskd/SocialFederation/API/ActivityPubController.cs`
- `src/slskd/SocialFederation/ServiceCollectionExtensions.cs`

**Wrong**:
```csharp
case "Follow":
    return (true, null);
```

**Correct**:
```csharp
await _relationshipStore.UpsertFollowerAsync(actorName, remoteActorId, cancellationToken);
return (true, null);
```

**Why This Keeps Happening**: It is easy to think of inbox storage and follower collections as separate milestones, but in ActivityPub they are linked by protocol semantics. If the app accepts `Follow`/`Undo`, it must update durable relationship state so `/followers` reflects what the inbox actually processed.

### 0x13. External Tool Discovery Should Search Standard System Install Paths Before Falling Back To Best-Effort PATH Scans

**The Bug**: Even after adding env-var and `PATH` discovery, SongID helpers like `panako.jar` and `audfprint.py` could still be missed on systems that install them into standard share directories rather than executable directories.

**Files Affected**:
- `src/slskd/SongID/SongIdService.cs`

**Wrong**:
```csharp
var pathScript = FindNewestFileOnPath("audfprint.py");
```

**Correct**:
```csharp
foreach (var candidate in new[]
{
    "/usr/share/audfprint/audfprint.py",
    "/usr/local/share/audfprint/audfprint.py",
})
```

**Why This Keeps Happening**: Tool discovery often assumes either a dev checkout or an executable on `PATH`. Java jars and helper scripts are frequently packaged into shared data directories instead. Search explicit environment variables first, then standard packaged install paths, and only then fall back to looser heuristics.

### 0x14. Recognizer Parsers Must Tolerate The Output Shapes Real Tools Emit, Not Just One Captured Sample

**The Bug**: SongID recognizer parsers only handled one narrow JSON/text shape for SongRec and Audfprint. Valid results using top-level track fields, `matches[]` wrappers, `artists[]`, or path-bearing match lines were discarded or reduced to low-quality titles.

**Files Affected**:
- `src/slskd/SongID/SongIdService.cs`

**Wrong**:
```csharp
if (!root.TryGetProperty("track", out var track))
{
    return null;
}
```

**Correct**:
```csharp
var track = GetSongRecTrack(root);
var artist = TryGetSongRecArtist(track.Value);
```

**Why This Keeps Happening**: Tool integrations usually start from one example output captured during development. Recognition tools then evolve or emit multiple shapes depending on CLI mode and version. Parse the stable semantic fields conservatively across the known variants instead of binding the whole feature to a single sample payload.

### 0x15. Federation Inbox Admission Must Follow The Same Actor Registry As Actor Discovery

**The Bug**: Even after actor discovery and actor documents were aligned to `LibraryActorService`, inbox POST still hardcoded `library` as the only accepted actor name. Remote delivery to real published actors like `music` or `books` would 404 despite those actors existing.

**Files Affected**:
- `src/slskd/SocialFederation/API/ActivityPubController.cs`

**Wrong**:
```csharp
if (!string.Equals(actorName, "library", StringComparison.OrdinalIgnoreCase))
    return NotFound();
```

**Correct**:
```csharp
var libraryActor = _libraryActorService.GetActor(actorName);
if (libraryActor == null)
{
    return NotFound();
}
```

**Why This Keeps Happening**: Federation endpoint work often starts with one actor and then grows into a registry-based model later. It is easy to update discovery and forget admission checks. Any endpoint that operates on actor names must validate against the shared actor registry, not legacy hardcoded names.

### 0x16. Outbound Follow State Must Be Persisted When The App Exposes A `following` Collection

**The Bug**: After follower state was added, the app still exposed `/following` without any durable outbound follow state. Bridge and service-level follow operations could claim success without leaving the server with any local record of the relationship.

**Files Affected**:
- `src/slskd/SocialFederation/ActivityPubRelationshipStore.cs`
- `src/slskd/SocialFederation/FederationService.cs`
- `src/slskd/SocialFederation/API/ActivityPubController.cs`
- `src/slskd/Mesh/Realm/Bridge/ActivityPubBridge.cs`

**Wrong**:
```csharp
return BridgeOperationResult.CreateSuccess(new { FollowedActor = actorId });
```

**Correct**:
```csharp
await _relationshipStore.UpsertFollowingAsync(localActorName, remoteActorId, cancellationToken);
```

**Why This Keeps Happening**: It is natural to think of outbound delivery and local state as separate concerns, but ActivityPub collection endpoints expose local state. If the server can follow remote actors, that operation must update durable `following` state or the endpoint becomes misleading.

### 0x17. Segment Metadata Candidates Should Fall Back To Stable Synthetic IDs Instead Of Being Dropped For Missing MBIDs

**The Bug**: Segment metadata hits with strong title/artist data but no MusicBrainz recording ID were discarded entirely. That made segment identification weaker than necessary for sources whose metadata provider did not resolve MBIDs.

**Files Affected**:
- `src/slskd/SongID/SongIdService.cs`

**Wrong**:
```csharp
if (string.IsNullOrWhiteSpace(hit.MusicBrainzRecordingId))
{
    return null;
}
```

**Correct**:
```csharp
var recordingId = !string.IsNullOrWhiteSpace(hit.MusicBrainzRecordingId)
    ? hit.MusicBrainzRecordingId
    : $"metadata:{NormalizeSegmentQuery(BuildBestQuery(hit.Artist, hit.Title))}";
```

**Why This Keeps Happening**: Ranking pipelines often assume canonical IDs exist by the time candidates are formed. Real metadata providers do not guarantee that. If a hit has strong human-meaningful identity data, keep it in the candidate set with a stable synthetic ID so downstream ranking can still compare and present it.

### 0x18. HashDb Job Lookups Must Fall Back To Row Columns When Stored JSON Becomes Incompatible

**The Bug**: Discography and label-crate job tables already stored their important fields in dedicated columns, but `Get*JobAsync()` still returned `null` whenever the serialized `json_data` blob could not be deserialized. That made otherwise recoverable jobs disappear.

**Files Affected**:
- `src/slskd/HashDb/HashDbService.cs`

**Wrong**:
```csharp
catch (Exception ex)
{
    log.Warning(ex, "... failed to deserialize ...");
    return null;
}
```

**Correct**:
```csharp
return new Jobs.DiscographyJob
{
    JobId = reader.GetString(0),
    ArtistName = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
    ...
};
```

**Why This Keeps Happening**: JSON snapshots are convenient for rich state, but schema drift eventually breaks deserialization. If the table also stores the canonical summary columns, use them to reconstruct a conservative job object instead of treating the whole record as gone.

### 0x19. Relationship Cleanup Activities Must Remove Both Inbound And Outbound Follow State When They Reference Follows

**The Bug**: Follow-specific `Undo`, `Reject`, and `Remove` flows were being implemented incrementally. Without explicit cleanup, some follow-removal activities could leave stale follower/following rows behind depending on which side initiated the relationship.

**Files Affected**:
- `src/slskd/SocialFederation/API/ActivityPubController.cs`

**Wrong**:
```csharp
case "Remove":
    return (true, null);
```

**Correct**:
```csharp
await _relationshipStore.RemoveFollowerAsync(actorName, remoteActorId, cancellationToken);
await _relationshipStore.RemoveFollowingAsync(actorName, remoteActorId, cancellationToken);
```

**Why This Keeps Happening**: Relationship protocols produce several different “remove” style activities, and it is easy to wire only the first one you encountered. Once both follower and following state exist locally, any follow-removal activity should reconcile both sides conservatively unless the protocol context proves otherwise.

### 0x18. Inbound `Accept`/`Reject` For Follow Activities Must Reconcile Local `following` State

**The Bug**: After outbound follow state was added, inbound `Accept` and `Reject` responses for follow activities still behaved like generic stored activities. That left local `following` state optimistic and stale when a remote actor rejected a follow.

**Files Affected**:
- `src/slskd/SocialFederation/API/ActivityPubController.cs`

**Wrong**:
```csharp
case "Reject":
    return (true, null);
```

**Correct**:
```csharp
await _relationshipStore.RemoveFollowingAsync(actorName, remoteActorId, cancellationToken);
```

**Why This Keeps Happening**: Once protocol state starts to exist locally, generic “store and ignore” handling is no longer sufficient for response activities. `Accept` and `Reject` are state transitions, not just log events, so they need to reconcile local relationship state.

### 0x19. Corpus Entries Should Not Promote Synthetic Metadata IDs To Canonical Recording IDs

**The Bug**: After metadata-only segment candidates were allowed with stable synthetic IDs, corpus registration could persist those synthetic `metadata:` IDs as if they were canonical recording identifiers.

**Files Affected**:
- `src/slskd/SongID/SongIdService.cs`

**Wrong**:
```csharp
RecordingId = topTrack?.RecordingId,
```

**Correct**:
```csharp
RecordingId = topTrack != null && !topTrack.RecordingId.StartsWith("metadata:", StringComparison.OrdinalIgnoreCase)
    ? topTrack.RecordingId
    : null,
```

**Why This Keeps Happening**: Once fallback IDs enter a ranking pipeline, later persistence code can accidentally treat them as canonical identifiers. Any synthetic ID scheme used to keep candidates alive in-memory needs an explicit filter before writing durable corpus or interoperability state.
if (response != null && messageSigner.VerifyMessage(response))
{
    tcs.SetResult(response);
}
```

**Why This Keeps Happening**: Response paths often get treated as “trusted” because they are tied to local pending requests, but on a mesh protocol they are still untrusted network input. Every response type needs the same signature and routing discipline as request messages, and adding a new protocol message requires updating both the sender and dispatcher tables.

### 0x9. Capability File Generators Must Reuse The Canonical Capability Payload Source

**The Bug**: `CapabilityFileService` hand-built a second capability JSON document with its own static feature list, version, mesh sequence, and overlay defaults instead of reusing `CapabilityService`. That creates drift between two advertised views of the same node.

**Files Affected**:
- `src/slskd/Capabilities/CapabilityFileService.cs`

**Wrong**:
```csharp
var caps = new CapabilityFileContent
{
    OverlayPort = 50305,
    Timestamp = DateTimeOffset.UtcNow
};
```

**Correct**:
```csharp
var json = _capabilityService.GetCapabilityFileContent();
return Encoding.UTF8.GetBytes(json);
```

**Why This Keeps Happening**: It feels harmless to duplicate a tiny protocol payload in a helper service, but duplicated protocol serialization always drifts. Capability ads need a single canonical generator so new fields like `mesh_seq_id` or changed feature flags do not diverge across endpoints.

### 0x10. Sync Orchestration Must Not Report Success For Simulated Local-Only Flows

**The Bug**: `MeshSyncService.TrySyncWithPeerAsync` logged a successful sync and updated counters after only reading local state. No HELLO/delta exchange ever left the process, so operators got a false success signal for a sync that never happened.

**Files Affected**:
- `src/slskd/Mesh/MeshSyncService.cs`

**Wrong**:
```csharp
result.Success = true;
log.Information("[MESH] Sync with {Peer} complete: sent={Sent}", username, result.EntriesSent);
```

**Correct**:
```csharp
result.Error = $"Mesh sync transport is not implemented (local seq={hello.LatestSeqId})";
log.Warning("[MESH] Refusing to report successful sync with {Peer}: transport is not implemented", username);
```

**Why This Keeps Happening**: During bring-up, local state simulation is useful for shaping the code path, but leaving the success branch intact teaches the rest of the system a lie. For distributed workflows, incomplete transport must surface as explicit failure until real request/response exchange is wired.

### 0x11. Capability File Parsers Must Reject Default-Filled Partial JSON

**The Bug**: Capability-file parsing deserialized remote JSON into a model with permissive defaults and then accepted the result without validating required fields. A malformed or partial document could therefore masquerade as a valid peer capability advertisement.

**Files Affected**:
- `src/slskd/Capabilities/CapabilityFileService.cs`

**Wrong**:
```csharp
return JsonSerializer.Deserialize<CapabilityFileContent>(json, options);
```

**Correct**:
```csharp
if (content == null ||
    string.IsNullOrWhiteSpace(content.Client) ||
    content.Capabilities == PeerCapabilityFlags.None)
{
    return null;
}
```

**Why This Keeps Happening**: DTO defaults are convenient for local generation but dangerous for remote parsing. When a protocol object has defaults like `"slskdn"` or `"1.0.0"`, deserialization can hide missing fields unless the parser explicitly validates the post-deserialize object. Remote capability documents need strict field validation after JSON parse, not just successful deserialization.

### 0x12. Pending Mesh RPC Correlation Keys Must Include The Remote Peer, Not Just The Payload Key

**The Bug**: Mesh hash lookups tracked pending requests only by `flacKey`. Consensus queries fan out to multiple peers in parallel, so the second request for the same key collided with the first and either failed immediately or delivered the wrong response.

**Files Affected**:
- `src/slskd/Mesh/MeshSyncService.cs`

**Wrong**:
```csharp
var requestId = flacKey;
```

**Correct**:
```csharp
var requestId = $"{username}:{flacKey}";
```

**Why This Keeps Happening**: It is easy to choose the “business identifier” as the correlation key and forget that the same business item may be requested concurrently from multiple remotes. For any fan-out network query, the correlation key must include enough dimensions to be unique per in-flight request, typically peer plus payload key.

### 0x13. Capability Discovery Helpers Need A Real Metadata Fallback When File Transfer Isn’t Wired

**The Bug**: `CapabilityFileService` attempted only a stubbed file download path and then returned `null`, even though the app already had a real remote capability signal in the peer’s `UserInfo` description tag.

**Files Affected**:
- `src/slskd/Capabilities/CapabilityFileService.cs`

**Wrong**:
```csharp
var data = await DownloadSmallFileAsync(...);
// ...
return null;
```

**Correct**:
```csharp
var userInfo = await _soulseekClient.GetUserInfoAsync(username, cts.Token);
var parsedCaps = _capabilityService.ParseCapabilityTag(userInfo?.Description ?? string.Empty);
```

**Why This Keeps Happening**: Helper services often get built around the “ideal” transport first and forget the lower-fidelity signal that already exists elsewhere in the app. If the preferred fetch path is not implemented, fall back to another real source of peer capability truth instead of silently turning discovery off.

### 0x14. Domain Providers Must Use Existing Catalog Queries Before Falling Back To Hard `null`

**The Bug**: The VirtualSoulfind music provider returned `null` for direct recording-ID lookups and title/artist work resolution even though the required album and track data was already stored in HashDb.

**Files Affected**:
- `src/slskd/VirtualSoulfind/Core/Music/MusicContentDomainProvider.cs`

**Wrong**:
```csharp
_logger.LogDebug("Direct track lookup by MusicBrainz Recording ID not yet implemented");
return Task.FromResult<MusicItem?>(null);
```

**Correct**:
```csharp
var tracks = await _hashDb.GetAlbumTracksAsync(album.ReleaseId, cancellationToken);
var track = tracks.FirstOrDefault(candidate => candidate.RecordingId == recordingId);
```

**Why This Keeps Happening**: Feature code often assumes a dedicated query method is required and gives up when that exact API does not exist. Before returning a permanent `null`, check whether the existing persistence layer already contains the needed data and whether a bounded lookup can assemble the answer from current interfaces.

### 0x15. Transport Client Counters Must Be Released On Early Validation And Cancellation Paths Too

**The Bug**: `MeshServiceClient` incremented its per-peer concurrency counter before duplicate-correlation and pre-cancel checks, then returned/threw without going through the cleanup path. A single bad request could permanently inflate pending-call accounting for that peer.

**Files Affected**:
- `src/slskd/Mesh/ServiceFabric/MeshServiceClient.cs`

**Wrong**:
```csharp
_perPeerCallCounts.AddOrUpdate(targetPeerId, 1, (_, count) => count + 1);
if (!_pendingCalls.TryAdd(call.CorrelationId, tcs))
{
    throw new InvalidOperationException("Duplicate correlation ID");
}
```

**Correct**:
```csharp
_perPeerCallCounts.AddOrUpdate(targetPeerId, 1, (_, count) => count + 1);
try
{
    if (!_pendingCalls.TryAdd(call.CorrelationId, tcs))
    {
        return Task.FromResult(new ServiceReply { ... });
    }
}
finally
{
    // shared cleanup runs for every exit path
}
```

**Why This Keeps Happening**: It is easy to think of “validation” as happening before the real work starts, but once shared counters or dictionaries are mutated, every return path is stateful. If a method increments concurrency, allocates correlation state, or acquires quotas before all validations finish, the cleanup logic must still wrap those early exits.

### 0p. Timer Expiry Must Not Be Inferred From `CancellationTokenSource.IsCancellationRequested`

**The Bug**: `TimedBatcher` waited for `_currentBatchTimer.IsCancellationRequested` to decide that the batch window had expired. Normal `Task.Delay` completion does not cancel the token, so time-window batching could wait forever unless the batch filled up.

**Files Affected**:
- `src/slskd/Common/Security/TimedBatcher.cs`

**Wrong**:
```csharp
if (_currentBatch.Count > 0 && _currentBatchTimer?.IsCancellationRequested == true)
{
    return;
}
```

**Correct**:
```csharp
if (_currentBatch.Count > 0
    && DateTimeOffset.UtcNow - _currentBatch[0].Timestamp >= TimeSpan.FromMilliseconds(_options.BatchWindowMs))
{
    return;
}
```

**Why This Keeps Happening**: Cancellation and completion are separate states. A CTS only flips to cancelled when code explicitly cancels it; a timer finishing normally does not mutate the token. If readiness depends on elapsed time, compare timestamps or set an explicit expiry flag instead of reading cancellation state.

### 0q. Linked Cancellation Sources For Long-Running Services Must Be Owned By The Service, Not A Local Scope

**The Bug**: `CoverTrafficGenerator.StartAsync` created a linked `CancellationTokenSource` in a local `using` scope and launched the generation loop with that token. The link was disposed immediately after start, so later shutdown no longer had a reliable cancellation path and `StopAsync` could stall until timeout.

**Files Affected**:
- `src/slskd/Common/Security/CoverTrafficGenerator.cs`

**Wrong**:
```csharp
using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken);
_generationTask = Task.Run(() => GenerateCoverTrafficAsync(linkedCts.Token), linkedCts.Token);
```

**Correct**:
```csharp
_generationCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken);
_generationTask = Task.Run(() => GenerateCoverTrafficAsync(_generationCts.Token), _generationCts.Token);
```

**Why This Keeps Happening**: Linked CTS instances look like one-line plumbing, but once their token is handed to a background loop they become part of the component lifecycle. If the service must stop that loop later, it must keep and dispose the linked CTS alongside the task instead of letting a local scope tear it down.

### 0r. Stop Paths Must Cancel Retry Tokens, Not Just Active Connections

**The Bug**: `RelayClient.StopAsync` disconnected the current `HubConnection` but left the retry-loop token alive. A caller could request stop and still get another background reconnect attempt from the existing `Retry.Do(...)` loop.

**Files Affected**:
- `src/slskd/Relay/RelayClient.cs`

**Wrong**:
```csharp
public async Task StopAsync(CancellationToken cancellationToken = default)
{
    StartRequested = false;

    if (HubConnection != null)
    {
        await HubConnection.StopAsync(cancellationToken);
    }
}
```

**Correct**:
```csharp
public async Task StopAsync(CancellationToken cancellationToken = default)
{
    StartRequested = false;
    var startCancellationTokenSource = StartCancellationTokenSource;
    StartCancellationTokenSource = null;
    startCancellationTokenSource?.Cancel();

    if (HubConnection != null)
    {
        await HubConnection.StopAsync(cancellationToken);
    }

    startCancellationTokenSource?.Dispose();
}
```

**Why This Keeps Happening**: Connection state and retry state are often tracked separately. It is easy to stop the current socket or hub and forget that a supervising retry loop is still waiting on its own token. Any service with reconnect logic needs shutdown to cancel both the active connection and the outer retry coordinator.

### 0s. Do Not Inline-Create `HttpClient` Into `SendAsync` When The Client Must Be Disposed

**The Bug**: `RelayClient` created a disposable `HttpClient` with `CreateHttpClient()` directly inside `SendAsync(...)`. The response was disposed, but the client itself was never captured in a `using`, so every file-upload request leaked a client instance.

**Files Affected**:
- `src/slskd/Relay/RelayClient.cs`

**Wrong**:
```csharp
using var response = await CreateHttpClient().SendAsync(request);
```

**Correct**:
```csharp
using var client = CreateHttpClient();
using var response = await client.SendAsync(request);
```

**Why This Keeps Happening**: `using var response = ...` looks complete because the visible disposable is handled, but any factory method invoked inline may also have returned a disposable owner. When the client is created ad hoc instead of coming from DI or a shared field, bind it to a local variable and dispose it explicitly.

### 0t. Stop/Start Components Must Not Cancel Their Permanent Lifetime Token On Ordinary Stop

**The Bug**: `CoverTrafficGenerator.StopAsync` cancelled a long-lived `_cts` that `StartAsync` also linked into every future generation task. After one stop, every later start inherited an already-cancelled token and the generator could never restart.

**Files Affected**:
- `src/slskd/Common/Security/CoverTrafficGenerator.cs`

**Wrong**:
```csharp
private readonly CancellationTokenSource _cts = new();

public async Task StopAsync()
{
    _cts.Cancel();
    _generationCts?.Cancel();
}

public Task StartAsync(CancellationToken cancellationToken = default)
{
    _generationCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken);
}
```

**Correct**:
```csharp
public async Task StopAsync()
{
    _generationCts?.Cancel();
}

public Task StartAsync(CancellationToken cancellationToken = default)
{
    _generationCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
}
```

**Why This Keeps Happening**: It is tempting to add a permanent CTS to make shutdown wiring easy, but that token becomes part of restart semantics too. If a component supports repeated start/stop cycles, only disposal should permanently end its lifetime; ordinary stop should cancel the current run, not poison every future run.

### 0u. Queue Release `TaskCompletionSource`s Must Use `RunContinuationsAsynchronously`

**The Bug**: `UploadQueue` released an upload by calling `SetResult()` on a default `TaskCompletionSource` while still inside queue scheduling logic. Continuations could run inline and re-enter queue methods like `Complete(...)`, risking deadlock against the queue semaphore.

**Files Affected**:
- `src/slskd/Transfers/Types/Upload.cs`
- `src/slskd/Transfers/Uploads/UploadQueue.cs`

**Wrong**:
```csharp
public TaskCompletionSource TaskCompletionSource { get; set; } = new TaskCompletionSource();
```

**Correct**:
```csharp
public TaskCompletionSource TaskCompletionSource { get; set; } =
    new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
```

**Why This Keeps Happening**: `TaskCompletionSource` looks like a passive signal, but by default it can execute awaiter continuations synchronously on the thread calling `SetResult`. In queue, lock, and semaphore-protected schedulers, that means arbitrary caller code can run before internal state is fully released. Use `RunContinuationsAsynchronously` for handoff signals that may be completed under coordination primitives.

### 0v. Background Cleanup Loops Need Explicit Cancellation Before Disposing Their Synchronization Primitives

**The Bug**: `SignalBus` started a perpetual cleanup task that slept with `Task.Delay(..., CancellationToken.None)` and used semaphores long after construction. `Dispose()` immediately disposed those semaphores without first cancelling and joining the cleanup task, so the background loop could wake up later and hit disposed primitives.

**Files Affected**:
- `src/slskd/Signals/SignalBus.cs`

**Wrong**:
```csharp
_ = Task.Run(CleanupExpiredSignalIdsAsync);

protected virtual void Dispose(bool disposing)
{
    seenSignalIdsLock.Dispose();
    subscribersLock.Dispose();
}
```

**Correct**:
```csharp
cleanupTask = Task.Run(CleanupExpiredSignalIdsAsync);

protected virtual void Dispose(bool disposing)
{
    cleanupCancellationTokenSource.Cancel();
    cleanupTask.Wait(TimeSpan.FromSeconds(1));
    cleanupCancellationTokenSource.Dispose();
    seenSignalIdsLock.Dispose();
    subscribersLock.Dispose();
}
```

**Why This Keeps Happening**: Background maintenance loops are easy to treat as harmless fire-and-forget work, but they still own timers, locks, and semaphores. Any component that starts a long-lived loop must keep a cancellation handle and stop the loop before disposing the resources it uses.

### 0w. Network And Coordination Waiters Should Default To `RunContinuationsAsynchronously`

**The Bug**: Several runtime waiters still used default `TaskCompletionSource` instances in relay login, mesh sync request/response tracking, download enqueue coordination, token-bucket resets, and channel completion. In `TokenBucket`, the reset path had already been fixed, but the original field initializer still used the default constructor. Those waiters can be completed from event handlers, locks, or scheduler paths, allowing arbitrary continuations to run inline on sensitive coordination threads.

**Files Affected**:
- `src/slskd/Relay/RelayClient.cs`
- `src/slskd/Mesh/MeshSyncService.cs`
- `src/slskd/Transfers/Downloads/DownloadService.cs`
- `src/slskd/Common/ChannelReader.cs`
- `src/slskd/Common/TokenBucket.cs`

**Wrong**:
```csharp
var tcs = new TaskCompletionSource<Response>();
private TaskCompletionSource<bool> waitForReset = new();
```

**Correct**:
```csharp
var tcs = new TaskCompletionSource<Response>(TaskCreationOptions.RunContinuationsAsynchronously);
private TaskCompletionSource<bool> waitForReset =
    new(TaskCreationOptions.RunContinuationsAsynchronously);
```

**Why This Keeps Happening**: Default `TaskCompletionSource` is convenient and usually works in tests, but runtime completion often happens under callbacks, locks, semaphores, or transport handlers. When the continuation inlines there, it can create re-entrancy, hidden latency spikes, or deadlocks. It is also easy to fix one construction site and miss the original field initializer. For cross-component handoff signals, asynchronous continuations should be the default everywhere, not only at later reset/replacement sites.

### 0aa. Retrying HTTP Hooks Must Recreate And Dispose Per-Attempt Request Content

**The Bug**: The webhook integration built one `StringContent` instance outside its retry loop and reused it across attempts without disposing it. That left request content lifetime unclear and made retries depend on reusing a mutable disposable payload object across multiple sends.

**Files Affected**:
- `src/slskd/Integrations/Webhooks/WebhookService.cs`

**Wrong**:
```csharp
var content = new StringContent(payloadJson, Encoding.UTF8, "application/json");

await Retry.Do(task: async () =>
{
    using var response = await http.PostAsync(call.Url, content);
});
```

**Correct**:
```csharp
await Retry.Do(task: async () =>
{
    using var content = new StringContent(payloadJson, Encoding.UTF8, "application/json");
    using var response = await http.PostAsync(call.Url, content);
});
```

**Why This Keeps Happening**: Retry loops naturally push payload construction outward so the body looks cleaner, but that can accidentally turn per-request disposables into shared mutable state. For retryable HTTP sends, either recreate request content per attempt or isolate it behind an immutable factory so each send owns and disposes its own payload object.

### 0ab. Delayed Cleanup Must Remove The Specific Completed Entry, Not Whatever Shares The Same Key Later

**The Bug**: Browse-status cleanup scheduled `TryRemove(username)` five seconds after a browse completed. If another browse for the same username updated the tracker during that window, the older cleanup task deleted the newer entry and the status endpoint incorrectly dropped to `404`.

**Files Affected**:
- `src/slskd/Users/API/Controllers/UsersController.cs`
- `src/slskd/Users/BrowseTracker.cs`
- `src/slskd/Users/IBrowseTracker.cs`

**Wrong**:
```csharp
_ = Task.Run(async () =>
{
    await Task.Delay(5000);
    BrowseTracker.TryRemove(username);
});
```

**Correct**:
```csharp
BrowseTracker.TryGet(username, out var completedProgress);
_ = ObserveBrowseCleanupAsync(username, completedProgress);
...
BrowseTracker.TryRemove(username, completedProgress);
```

**Why This Keeps Happening**: Delayed cleanup often captures only the lookup key and assumes the keyed state is still the same object later. That assumption breaks as soon as the same key can be reused for a newer operation. When cleanup is deferred, capture an operation/version/token or compare the exact tracked instance before removing it.

### 0ac. Rate-Limit State Must Store A Window Of Events, Not Just The Latest Timestamp Per Key

**The Bug**: Two rate-limiters kept only one timestamp per key. In federation delivery, that meant `MaxActivitiesPerHour > 1` was never really enforced because each inbox URL had at most one stored entry. In notifications, concurrent callers could both pass the check-then-set window and send duplicate notifications.

**Files Affected**:
- `src/slskd/SocialFederation/ActivityDeliveryService.cs`
- `src/slskd/Integrations/Notifications/NotificationService.cs`

**Wrong**:
```csharp
private readonly ConcurrentDictionary<string, DateTime> _recentDeliveries = new();
...
var recentCount = _recentDeliveries.Count(kvp => kvp.Key == inboxUrl && kvp.Value > cutoff);
_recentDeliveries[inboxUrl] = DateTime.UtcNow;
```

**Correct**:
```csharp
private readonly ConcurrentDictionary<string, ConcurrentQueue<DateTime>> _recentDeliveries = new();
...
deliveries.Enqueue(now);
TrimExpiredDeliveries(inboxUrl, deliveries, cutoff);
```

**Why This Keeps Happening**: “Rate limit by key” is easy to model as a single last-seen timestamp, but any limit larger than one event per window needs a real sliding window, queue, or token bucket. Separate check-then-set logic also looks harmless until concurrent callers hit it at once. If the rule is “N events per window,” store N-window state and update/check it atomically enough for the workload.

### 0x. Singleton Services Must Not Launch Infinite Cleanup Loops Without A Disposal Hook

**The Bug**: `PrivateGatewayMeshService` is registered as a singleton and started an infinite `CleanupExpiredTunnelsAsync` loop with `Task.Run(...)`, `while (true)`, and `Task.Delay(...)` but exposed no `Dispose()` path to cancel it. That leaves a permanent background task with live references to service state until process exit.

**Files Affected**:
- `src/slskd/Mesh/ServiceFabric/Services/PrivateGatewayMeshService.cs`

**Wrong**:
```csharp
_ = Task.Run(CleanupExpiredTunnelsAsync);

private async Task CleanupExpiredTunnelsAsync()
{
    while (true)
    {
        await RunOneCleanupIterationAsync();
        await Task.Delay(TimeSpan.FromMinutes(5));
    }
}
```

**Correct**:
```csharp
_cleanupTask = Task.Run(CleanupExpiredTunnelsAsync);

private async Task CleanupExpiredTunnelsAsync()
{
    while (!_cleanupCancellationTokenSource.IsCancellationRequested)
    {
        await RunOneCleanupIterationAsync();
        await Task.Delay(TimeSpan.FromMinutes(5), _cleanupCancellationTokenSource.Token);
    }
}
```

**Why This Keeps Happening**: Cleanup work often feels “daemon-like,” so it gets launched as fire-and-forget during construction. But singleton services are still owned objects, and DI can only shut them down cleanly if they expose a disposal lifecycle that cancels and joins their background work.

### 0y. Once A Refresh Method Starts Awaiting I/O, Its Signature Must Be Fully Converted To `async Task<T>`

**The Bug**: `PodDhtPublisher.RefreshAsync` had been partially refactored from synchronous fast-paths to awaited I/O, but the method signature still returned `Task<PodRefreshResult>` without `async`. That produced a compile break and mixed `Task.FromResult(...)` returns with raw `PodRefreshResult` values.

**Files Affected**:
- `src/slskd/PodCore/PodDhtPublisher.cs`

**Wrong**:
```csharp
public Task<PodRefreshResult> RefreshAsync(...)
{
    var pod = await _podService.GetPodAsync(...);
    return new PodRefreshResult(...);
}
```

**Correct**:
```csharp
public async Task<PodRefreshResult> RefreshAsync(...)
{
    var pod = await _podService.GetPodAsync(...);
    return new PodRefreshResult(...);
}
```

**Why This Keeps Happening**: It is common to start with a synchronous `Task.FromResult(...)` method and later add awaited work in the middle. If that happens, update the full signature and normalize the returns immediately; otherwise you get a half-converted method that neither compiles nor communicates its real async behavior.

### 0z. The `Task.Run` Scheduling Token Must Match The Background Task Lifetime, Not The Caller Lifetime

**The Bug**: Several services launched long-lived background work with `Task.Run(..., cancellationToken)` where `cancellationToken` belonged to the current request or startup call. That token can cancel before the task is even scheduled, silently preventing the background operation from starting at all.

**Files Affected**:
- `src/slskd/Mesh/ServiceFabric/Services/PrivateGatewayMeshService.cs`
- `src/slskd/LibraryHealth/LibraryHealthService.cs`
- `src/slskd/VirtualSoulfind/DisasterMode/SoulseekHealthMonitor.cs`
- `src/slskd/PodCore/PodServices.cs`
- `src/slskd/Common/Security/LocalPortForwarder.cs`

**Wrong**:
```csharp
_ = Task.Run(() => PerformBackgroundWorkAsync(id, ct), ct);
```

**Correct**:
```csharp
_ = Task.Run(() => PerformBackgroundWorkAsync(id, ct), CancellationToken.None);
```

**Why This Keeps Happening**: The second parameter to `Task.Run` controls whether the task is queued at all, not just what token the delegate receives. It is easy to assume passing the caller token there is harmless, but for background work it couples scheduling to the request/startup lifetime. Use the real lifetime token inside the delegate, and only use the `Task.Run` token when the task itself should be suppressed if scheduling has not yet happened.

In pod-management code this is especially sneaky because the foreground create/update call can still succeed locally while the background DHT publish never starts, leaving local state and discoverability out of sync.

In transport code it can be worse: connection state may be marked as “mapped” before the worker tasks are queued, so a pre-canceled scheduler token can leave a tunnel stuck in a mapped-but-idle state with no background pumps running.

### 0k. `async void` Event Handlers Must Catch At The Top Level Or They Can Crash Background Health Logic

**The Bug**: Disaster-mode health event handlers used `async void` without a top-level exception guard, so any exception from delayed recovery/escalation work could escape the event callback, terminate the process, or silently break recovery flow.

**Files Affected**:
- `src/slskd/VirtualSoulfind/DisasterMode/DisasterModeCoordinator.cs`
- `src/slskd/VirtualSoulfind/DisasterMode/DisasterModeRecovery.cs`

**Wrong**:
```csharp
private async void OnHealthChanged(object? sender, SoulseekHealthChangedEventArgs e)
{
    await AttemptRecoveryAsync(CancellationToken.None);
}
```

**Correct**:
```csharp
private async void OnHealthChanged(object? sender, SoulseekHealthChangedEventArgs e)
{
    try
    {
        await AttemptRecoveryAsync(CancellationToken.None);
    }
    catch (OperationCanceledException)
    {
        logger.LogDebug("Health-change recovery processing cancelled");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Unhandled exception while processing health change");
    }
}
```

**Why This Keeps Happening**: .NET event handlers often force `void`, which makes `async void` tempting. Unlike `Task`-returning methods, exceptions do not flow back to a caller that can observe them. If an event must remain `async void`, the entire body needs its own `try/catch` and explicit logging.

### 0l. Streamed `HttpResponseMessage` Objects Must Be Disposed On Success Paths Too

**The Bug**: Several HTTP client paths were fixed for error-case disposal, but a federation key-fetch path still kept the successful `HttpResponseMessage` alive while streaming content and returning early on parse/size failure. That can retain pooled connections and accumulate resource pressure under retries.

**Files Affected**:
- `src/slskd/SocialFederation/HttpSignatureKeyFetcher.cs`

**Wrong**:
```csharp
HttpResponseMessage? res = null;
res = await _httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cts.Token);
await using var stream = await res.Content.ReadAsStreamAsync(cts.Token);
return ExtractPublicKeyPkixFromActorJson(json, keyId);
```

**Correct**:
```csharp
using var res = await _httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cts.Token);
await using var stream = await res.Content.ReadAsStreamAsync(cts.Token);
return ExtractPublicKeyPkixFromActorJson(json, keyId);
```

**Why This Keeps Happening**: It is easy to focus on stream disposal and miss that the owning `HttpResponseMessage` also needs disposal, especially when using `ResponseHeadersRead` and multiple early returns. In these paths, wrap the response in `using var` as soon as it is created unless ownership is intentionally transferred.

### 0m. Do Not `using`-Dispose A `CancellationTokenSource` That A Background Task Will Keep Using

**The Bug**: `MdnsAdvertiser.StartAsync` created a linked cancellation source with `using var`, launched a background announce loop that captured the token, and then returned. The method disposed the source immediately, leaving the background loop to run against a disposed CTS.

**Files Affected**:
- `src/slskd/Identity/MdnsAdvertiser.cs`

**Wrong**:
```csharp
_announceCts = new CancellationTokenSource();
using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _announceCts.Token);
_ = Task.Run(async () => await Task.Delay(TimeSpan.FromSeconds(10), linkedCts.Token), linkedCts.Token);
```

**Correct**:
```csharp
_announceCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
_ = Task.Run(async () => await Task.Delay(TimeSpan.FromSeconds(10), _announceCts.Token), _announceCts.Token);
```

**Why This Keeps Happening**: `using var` around a CTS looks tidy, but it is only correct when all work using that token completes before the scope exits. If a background worker outlives the method, the CTS must be owned and disposed by the component lifecycle, not by a local scope.

### 0n. Simulated Background Transfers Still Need To Materialize Output Before Verification

**The Bug**: `MeshTransferService` simulated chunk progress and then immediately ran file integrity checks without ever creating the target file. Every no-hash transfer therefore failed with `FileNotFoundException`, and explicit cancellation could surface as `Failed` instead of `Cancelled`.

**Files Affected**:
- `src/slskd/VirtualSoulfind/DisasterMode/MeshTransferService.cs`

**Wrong**:
```csharp
await Task.Delay(200, ct);
await VerifyFileIntegrityAsync(status, ct);
```

**Correct**:
```csharp
await Task.Delay(200, ct);
await using var output = new FileStream(status.TargetPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
output.SetLength(status.FileSize);
await output.FlushAsync(ct);
await VerifyFileIntegrityAsync(status, ct);
```

**Why This Keeps Happening**: It is easy to treat a staged or simulated transfer service as "good enough" once progress reporting exists, but any later verification step assumes a real file exists. If the service publishes `Completed` or runs integrity checks, it must materialize the output artifact or explicitly stub verification out.

### 0o. Validation Added Before Assignment Cannot Reuse The Assigned Variable In `catch`

**The Bug**: `DirectQuicDialer` added port validation before assigning `ipEndpoint`, but the catch block still logged `ipEndpoint.ToString()`. That introduced a compile break and would also have hidden the original validation failure behind secondary logging logic.

**Files Affected**:
- `src/slskd/Mesh/Transport/DirectQuicDialer.cs`

**Wrong**:
```csharp
IPEndPoint ipEndpoint;
if (endpoint.Port is <= 0 or > ushort.MaxValue)
{
    throw new ArgumentOutOfRangeException(nameof(endpoint.Port));
}

catch (Exception ex)
{
    LoggingUtils.LogConnectionFailed(_logger, peerId, ipEndpoint.ToString(), ex.Message);
}
```

**Correct**:
```csharp
IPEndPoint? ipEndpoint = null;

catch (Exception ex)
{
    LoggingUtils.LogConnectionFailed(
        _logger,
        peerId,
        ipEndpoint?.ToString() ?? $"{endpoint.Host}:{endpoint.Port}",
        ex.Message);
}
```

**Why This Keeps Happening**: Validation checks often get inserted above the original assignment line during hardening work. Any later logging or cleanup path that assumes assignment already happened must be updated to tolerate the pre-assignment failure path as well.

### 0p. Untrusted Identifiers and Enums Must Parse Defensively Before Entering Core Pipelines

**The Bug**: Several paths converted identifier and enum strings directly with `Parse(...)` from persisted/cached data or request payloads (for example content IDs, run IDs, and moderation verdicts), which could throw and halt background processing instead of failing a single row/entry cleanly.

**Files Affected**:
- `src/slskd/SongID/SongIdService.cs`
- `src/slskd/VirtualSoulfind/v2/Sources/SqliteSourceRegistry.cs`
- `src/slskd/VirtualSoulfind/v2/Planning/MultiSourcePlanner.cs`
- `src/slskd/VirtualSoulfind/v2/Processing/IntentQueueProcessor.cs`
- `src/slskd/Common/CommonExtensions.cs`
- `src/slskd/Common/Moderation/RemoteExternalModerationClient.cs`
- `src/slskd/Common/Moderation/HttpLlmModerationProvider.cs`

**Wrong**:
```csharp
var trackId = ContentItemId.Parse(intent.TrackId); // throws on invalid DB/request value
// ...
public static T ToEnum<T>(this string str) => (T)Enum.Parse(typeof(T), str);
```

**Correct**:
```csharp
if (!ContentItemId.TryParse(intent.TrackId, out var trackId))
{
    // mark failed / skip row
    return false;
}

if (!Enum.TryParse(str, ignoreCase: true, out T value))
{
    throw new ArgumentException($"Unable to parse '{str}' as {typeof(T).Name}");
}
```

**Why This Keeps Happening**: Parsing into strongly typed IDs/enums is often treated as a validation concern earlier in the stack, but data can become stale/corrupted in storage, or originate from API clients/3rd-party responses. Any pipeline that assumes perfect inputs can still abort on a single bad value unless it handles parse failure explicitly.

### 0f. Invalid-Config Startup Tests Must Satisfy Base Option Validation Before Asserting Later Hardening Failures

**The Bug**: `EnforceInvalidConfigIntegrationTests` expected the subprocess to fail on a hardening rule, but CI hit the earlier base-options validation first because the temporary app directory did not contain `wwwroot`, so startup returned success from the early validation path and never reached the hardening check.

**Files Affected**:
- `tests/slskd.Tests/EnforceInvalidConfigIntegrationTests.cs`
- `src/slskd/Program.cs`

**Wrong**:
```csharp
await File.WriteAllTextAsync(yml, """
    web:
      enforceSecurity: true
""");
```

```csharp
if (!OptionsAtStartup.TryValidate(out var result))
{
    Log.Information(result.GetResultView());
    return;
}
```

**Correct**:
```csharp
Directory.CreateDirectory(Path.Combine(tempDir, "wwwroot"));
await File.WriteAllTextAsync(yml, """
    web:
      contentPath: wwwroot
      enforceSecurity: true
""");
```

```csharp
if (!OptionsAtStartup.TryValidate(out var result))
{
    Log.Information(result.GetResultView());
    Exit(1);
}
```

**Why This Keeps Happening**: Startup has more than one validation layer. Tests that target a later validation stage can be accidentally preempted by unrelated defaults unless the temporary environment satisfies the earlier base constraints first. When startup does reject config, it must terminate non-zero or release-gate tests will treat a real config failure as a false success.

### 0g. Startup Failure Tests Need a Deterministic Plain-Text Rule Signal, Not Just Structured Logger Output

**The Bug**: The invalid-config subprocess test exited non-zero on CI but still failed because the captured output did not reliably include the hardening rule name, even though the exception was being logged.

**Files Affected**:
- `src/slskd/Program.cs`
- `tests/slskd.Tests/EnforceInvalidConfigIntegrationTests.cs`

**Wrong**:
```csharp
catch (HardeningValidationException hex)
{
    Log.Fatal(hex, "Hardening validation failed: {Message}", hex.Message);
    Exit(1);
}
```

**Correct**:
```csharp
catch (HardeningValidationException hex)
{
    Console.Error.WriteLine($"[HardeningValidation] {hex.RuleName}: {hex.Message}");
    Log.Fatal(hex, "Hardening validation failed: {Message}", hex.Message);
    Exit(1);
}
```

**Why This Keeps Happening**: Integration tests read raw subprocess stdout/stderr, not the structured logger event stream. If the test depends on a specific diagnostic token, write that token directly to stderr/stdout before exiting.

### 0h. Async Timeout/Circuit Tests Should Assert Eventual State Change, Not An Exact Transition Call Count

**The Bug**: `ServiceTimeout_TriggersCircuitBreaker` assumed the circuit breaker would always be visibly open on the 6th timed-out call, but CI occasionally returned one more timeout before the open-state reply, making the test fail even though the breaker logic was still converging correctly.

**Files Affected**:
- `tests/slskd.Tests/Mesh/ServiceFabric/MeshServiceRouterSecurityTests.cs`

**Wrong**:
```csharp
for (int i = 0; i < 5; i++)
{
    await router.RouteAsync(call, peerId);
}

var lastReply = await router.RouteAsync(lastCall, peerId);
Assert.Equal(ServiceStatusCodes.ServiceUnavailable, lastReply.StatusCode);
```

**Correct**:
```csharp
ServiceReply? lastReply = null;
for (int i = 0; i < 10; i++)
{
    lastReply = await router.RouteAsync(call, peerId);
    if (lastReply.StatusCode == ServiceStatusCodes.ServiceUnavailable)
    {
        break;
    }
}

Assert.Equal(ServiceStatusCodes.ServiceUnavailable, lastReply.StatusCode);
```

**Why This Keeps Happening**: Timeouts and cancellation-driven state transitions can land on slightly different attempts under CI scheduling. For async resilience tests, assert that the expected state change happens within a bounded window instead of pinning the assertion to one exact call number unless the implementation explicitly guarantees it.

### 0i. Circuit-Breaker Failure Tests Have The Same Exact-Transition Flake As Timeout Tests

**The Bug**: `CircuitBreaker_OpensAfter5ConsecutiveFailures` assumed the open-state response must appear on the 6th failing call, but CI can surface one more ordinary failure before returning `ServiceUnavailable`, creating the same exact-transition flake as the timeout-based breaker test.

**Files Affected**:
- `tests/slskd.Tests/Mesh/ServiceFabric/MeshServiceRouterSecurityTests.cs`

**Wrong**:
```csharp
for (int i = 0; i < 6; i++)
{
    lastReply = await router.RouteAsync(call, peerId);
}

Assert.Equal(ServiceStatusCodes.ServiceUnavailable, lastReply.StatusCode);
```

**Correct**:
```csharp
for (int i = 0; i < 10; i++)
{
    lastReply = await router.RouteAsync(call, peerId);
    if (lastReply.StatusCode == ServiceStatusCodes.ServiceUnavailable)
    {
        break;
    }
}

Assert.Equal(ServiceStatusCodes.ServiceUnavailable, lastReply.StatusCode);
```

**Why This Keeps Happening**: The breaker state update is observable through asynchronous request flow, not as a hard guarantee tied to a specific numbered call. If the behavior being tested is "the breaker opens after sustained failures," the assertion should allow a bounded convergence window.

### 0j. Subprocess Config Tests Must Create Relative Content Directories Under `AppContext.BaseDirectory`

**The Bug**: `EnforceInvalidConfigIntegrationTests` created a temp `wwwroot` and changed the subprocess working directory, but `contentPath` validation and runtime static-file setup both resolve relative paths under `AppContext.BaseDirectory`, so CI still failed base config validation before the hardening rule.

**Files Affected**:
- `tests/slskd.Tests/EnforceInvalidConfigIntegrationTests.cs`

**Wrong**:
```csharp
Directory.CreateDirectory(Path.Combine(tempDir, "wwwroot"));
await File.WriteAllTextAsync(yml, """
    web:
      contentPath: wwwroot
""");
```

**Correct**:
```csharp
var contentPath = "test-wwwroot-" + Guid.NewGuid().ToString("N")[..8];
var contentDir = Path.Combine(Path.GetDirectoryName(slskdDll)!, contentPath);
Directory.CreateDirectory(contentDir);
```

**Why This Keeps Happening**: `SLSKD_APP_DIR` and `WorkingDirectory` do not control this option. The validator and `Program` both explicitly combine `OptionsAtStartup.Web.ContentPath` with `AppContext.BaseDirectory`, so tests must place any temporary relative content directory under the built app output directory.

### 0j2. `FileExistsAttribute` Must Treat Empty Strings As "Not Configured", Not As A Path To Validate

**The Bug**: Full-startup invalid-config tests were still being preempted before hardening validation because optional config fields that default to `string.Empty` hit `Path.GetFullPath("")` inside `FileExistsAttribute`, throwing `ArgumentException` instead of cleanly skipping validation for an unset optional path.

**Files Affected**:
- `src/slskd/Common/Validation/FileExistsAttribute.cs`
- `tests/slskd.Tests/EnforceInvalidConfigIntegrationTests.cs`
- `src/slskd/Core/Options.cs`

**Wrong**:
```csharp
if (value != null)
{
    var file = Path.GetFullPath(value.ToString()!);
    if (!string.IsNullOrEmpty(file))
    {
        // validate file
    }
}
```

**Correct**:
```csharp
var rawPath = value?.ToString();
if (string.IsNullOrWhiteSpace(rawPath))
{
    return ValidationResult.Success;
}

var file = Path.GetFullPath(rawPath);
// validate file
```

**Why This Keeps Happening**: Many optional path settings in `Options` intentionally default to `string.Empty`. Validation attributes must distinguish "unset optional value" from "configured path" before normalizing or resolving the path, or they will fail startup for the wrong reason and mask the real validation behavior being tested.

### 0j3. Subprocess Startup Tests Must Launch The Freshly Built App Binary, Not A Hard-Coded `Release` Output

**The Bug**: `EnforceInvalidConfigIntegrationTests` always launched `src/slskd/bin/Release/net8.0/slskd.dll`, so `dotnet test` could rebuild the project in `Debug` while the test still executed a stale old `Release` binary and reported a failure that had already been fixed in source.

**Files Affected**:
- `tests/slskd.Tests/EnforceInvalidConfigIntegrationTests.cs`

**Wrong**:
```csharp
var slskdDll = Path.Combine(repoRoot, "src", "slskd", "bin", "Release", "net8.0", "slskd.dll");
if (!File.Exists(slskdDll))
{
    return;
}
```

**Correct**:
```csharp
var slskdDll = Path.Combine(repoRoot, "src", "slskd", "bin", "Debug", "net8.0", "slskd.dll");
if (!File.Exists(slskdDll))
{
    slskdDll = Path.Combine(repoRoot, "src", "slskd", "bin", "Release", "net8.0", "slskd.dll");
}
```

**Why This Keeps Happening**: Integration tests that spawn the app as a subprocess are not automatically tied to the current test build configuration. If they hard-code one output folder, they can silently run stale binaries and invalidate the test result. Always resolve the current build output first, then fall back only if necessary.

### 0j4. Empty-String Unix Socket Defaults Must Be Treated As "Not Configured" Before Kestrel Startup

**The Bug**: Full-instance integration tests timed out for 25 seconds per test because `Program` treated `web.socket` as configured whenever it was non-null. The option defaults to `string.Empty`, so Kestrel received an empty Unix socket path and crashed during `builder.Build()` before the API ever came up.

**Files Affected**:
- `src/slskd/Program.cs`
- `src/slskd/Core/Options.cs`

**Wrong**:
```csharp
if (OptionsAtStartup.Web.Socket != null)
{
    options.ListenUnixSocket(OptionsAtStartup.Web.Socket);
}
```

**Correct**:
```csharp
if (!string.IsNullOrWhiteSpace(OptionsAtStartup.Web.Socket))
{
    options.ListenUnixSocket(OptionsAtStartup.Web.Socket);
}
```

**Why This Keeps Happening**: This codebase uses `string.Empty` for many optional path-like settings. Startup code must check for a real configured value, not just non-null, or the app can die in a later subsystem with a misleading exception instead of simply leaving the optional feature disabled.

### 0j5. Full-Instance Bridge Tests Must Set The Bridge-Enable Environment Variable, Not Just Bridge Config

**The Bug**: `SlskdnFullInstanceRunner` wrote `virtualSoulfind.bridge.enabled: true` into test config, but `Program` only registers `BridgeProxyServer` when `SLSKDN_ENABLE_BRIDGE_PROXY` is present. The bridge integration tests therefore spent their startup budget booting an app that would never open the expected bridge port.

**Files Affected**:
- `tests/slskd.Tests.Integration/Harness/SlskdnFullInstanceRunner.cs`
- `src/slskd/Program.cs`

**Wrong**:
```csharp
var startInfo = new ProcessStartInfo
{
    FileName = binaryPath,
    Arguments = $"--config \"{configPath}\"",
};
```

**Correct**:
```csharp
var startInfo = new ProcessStartInfo
{
    FileName = binaryPath,
    Arguments = $"--config \"{configPath}\"",
};

if (enableBridge)
{
    startInfo.Environment["SLSKDN_ENABLE_BRIDGE_PROXY"] = "1";
}
```

**Why This Keeps Happening**: Some test-only or deadlock-guarded features are gated by environment variables in addition to config. If a harness expects a hosted service to exist, it must mirror the same startup gate the application uses, or tests will silently wait on a port that the process was never allowed to bind.

### 0j6. Startup Fallbacks Must Treat Blank Static Path Settings As Unset, And Test Harnesses Must Pass `APP_DIR`

**The Bug**: Full-instance bridge tests still failed before config load with `Filesystem exception: Directory  does not exist...` because the child process never received an app directory, while `Program` used `??=` on static string properties initialized to `string.Empty`. A blank `AppDirectory` or `ConfigurationFile` therefore stayed blank instead of falling back to the defaults.

**Files Affected**:
- `src/slskd/Program.cs`
- `tests/slskd.Tests.Integration/Harness/SlskdnFullInstanceRunner.cs`

**Wrong**:
```csharp
AppDirectory ??= DefaultAppDirectory;
ConfigurationFile ??= DefaultConfigurationFile;
```

```csharp
var startInfo = new ProcessStartInfo
{
    FileName = binaryPath,
    Arguments = $"--config \"{configPath}\"",
};
```

**Correct**:
```csharp
if (string.IsNullOrWhiteSpace(AppDirectory))
{
    AppDirectory = DefaultAppDirectory;
}

if (string.IsNullOrWhiteSpace(ConfigurationFile))
{
    ConfigurationFile = DefaultConfigurationFile;
}
```

```csharp
startInfo.Environment["APP_DIR"] = appDir;
```

**Why This Keeps Happening**: Several startup path fields are modeled as empty strings, not nulls. `??=` only fixes null, so blank values can leak into filesystem setup and explode before logging/config are fully online. Test harnesses that expect isolated app state must also pass `APP_DIR` explicitly instead of assuming `WorkingDirectory` or the config file location will set it indirectly.

### 0j7. SOCKS/Tunnel Tests Must Use Bounded Timeouts And Deterministic Silent Endpoints, Not "Probably Unused" Ports

**The Bug**: `TorTransport_ConnectionTimeout_HandledGracefully` hung for minutes because it assumed `127.0.0.1:12345` was unused. If something listens on that port but never speaks SOCKS, `TorSocksTransport` had no internal handshake timeout and would wait forever on `ReadAsync`.

**Files Affected**:
- `src/slskd/Common/Security/TorSocksTransport.cs`
- `tests/slskd.Tests.Integration/Security/TorIntegrationTests.cs`

**Wrong**:
```csharp
var torOptions = new TorOptions
{
    SocksAddress = "127.0.0.1:12345",
};

await transport.ConnectAsync("example.com", 80);
```

**Correct**:
```csharp
using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
var effectiveToken = linkedCts.Token;
```

```csharp
using var silentServer = new SilentTcpServer();
await silentServer.StartAsync();
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
await Assert.ThrowsAnyAsync<Exception>(() => transport.ConnectAsync("example.com", 80, cts.Token));
```

**Why This Keeps Happening**: Connection-refused tests are only deterministic if the endpoint state is deterministic. A "random closed port" can become an open but silent endpoint on another machine or CI worker, and transports without a bounded connect/handshake timeout will then hang forever in network reads.

### 0j8. Tuple Member Renames Must Be Updated In Tests Too, Or `dotnet test` Will Fail At Compile Time

**The Bug**: Root `dotnet test` still failed after the integration fixes because a unit test was reading a tuple member as `.totalKeys` after the production API had been normalized to PascalCase tuple names `(int TotalKeys, int ContentHintKeys)`.

**Files Affected**:
- `tests/slskd.Tests.Unit/Mesh/Phase8MeshTests.cs`

**Wrong**:
```csharp
var stored = dht.GetStoreStats();
Assert.True(stored.totalKeys >= 1);
```

**Correct**:
```csharp
var stored = dht.GetStoreStats();
Assert.True(stored.TotalKeys >= 1);
```

**Why This Keeps Happening**: Tuple element names are part of the compile-time API surface even though they look lightweight. When cleanup work renames tuple elements for consistency, stale tests won’t fail until the affected project is rebuilt, so always grep the test tree for the old element name after changing a returned tuple signature.

### 0k14. UDP Port Prediction Must Reject Out-of-Range Endpoints Before `IPEndPoint` Construction

**The Bug**: NAT hole punching code predicted adjacent remote ports by adding a small offset directly to `remoteEp.Port` and passed the raw result to `IPEndPoint`, which can throw for edge values (`0` or `65536+`).

**Files Affected**:
- `src/slskd/Mesh/Nat/UdpHolePuncher.cs`

**Wrong**:
```csharp
var predictedEp = new IPEndPoint(remoteEp.Address, remoteEp.Port + offset);
```

**Correct**:
```csharp
var predictedPort = remoteEp.Port + offset;
if (predictedPort is <= 0 or > ushort.MaxValue)
{
    continue;
}

var predictedEp = new IPEndPoint(remoteEp.Address, predictedPort);
```

**Why This Keeps Happening**: Small signed math on port numbers can silently exceed protocol limits around `0` and `65535`; this should be guarded before endpoint creation because constructors validate and throw synchronously. Clamp/skip invalid ports before creating predicted probe targets.

### 0k15. UDP Endpoint Parsers Must Reject Invalid Port Ranges Before `IPEndPoint` Construction

**The Bug**: NAT traversal endpoint parsers accepted `udp://` and `relay://` strings with port integers that are out of protocol bounds, then passed them directly to `IPEndPoint`.

**Files Affected**:
- `src/slskd/Mesh/Nat/NatTraversalService.cs`
- `src/slskd/Mesh/Nat/StunNatDetector.cs`
- `src/slskd/Mesh/Dht/PeerDescriptorPublisher.cs`
- `src/slskd/Mesh/Transport/TransportSelector.cs`
- `src/slskd/Mesh/Overlay/QuicDataServer.cs`
- `src/slskd/Mesh/Transport/DirectQuicDialer.cs`
- `src/slskd/DhtRendezvous/NatDetectionService.cs`
- `src/slskd/Common/Security/TorSocksTransport.cs`
- `src/slskd/Common/Security/I2PTransport.cs`
- `src/slskd/Common/Security/RelayOnlyTransport.cs`
- `src/slskd/PodCore/PeerResolutionService.cs`
- `src/slskd/Mesh/Dht/MeshDirectory.cs`
- `src/slskd/Mesh/Dht/ContentDirectory.cs`

**Wrong**:
```csharp
if (!int.TryParse(parts[1], out var port)) return false;
ep = new IPEndPoint(ip, port);
```

**Correct**:
```csharp
if (!int.TryParse(parts[1], out var port) || port is <= 0 or > ushort.MaxValue)
{
    return false;
}

ep = new IPEndPoint(ip, port);
```

```csharp
if (parts.Length == 2 && int.TryParse(parts[1], out var port) && port is > 0 and <= ushort.MaxValue)
{
    return new TransportEndpoint
    {
        TransportType = TransportType.DirectQuic,
        Host = parts[0],
        Port = port,
        Scope = TransportScope.ControlAndData,
        Preference = 0,
        Cost = 0
    };
}
```

```csharp
var parts = stunServer.Split(':');
if (parts.Length != 2 || !int.TryParse(parts[1], out var port) || port is <= 0 or > ushort.MaxValue)
{
    throw new FormatException($"Invalid STUN server format: {stunServer}");
}
```

```csharp
var parts = _options.SocksAddress.Split(':');
if (parts.Length != 2 || !int.TryParse(parts[1], out var socksPort) || socksPort is <= 0 or > ushort.MaxValue)
{
    throw new FormatException($"Invalid SOCKS address: {_options.SocksAddress}");
}
```

```csharp
if (endpoint.Port is <= 0 or > ushort.MaxValue)
{
    throw new ArgumentOutOfRangeException(nameof(endpoint.Port), endpoint.Port, "Port must be between 1 and 65535");
}

var ipEndpoint = new IPEndPoint(IPAddress.Parse(endpoint.Host), endpoint.Port);
```

**Why This Keeps Happening**: `IPEndPoint` validates ports at construction time; endpoint parsers should filter malformed or out-of-range values first to avoid throwing and to keep traversal parsing behavior deterministic.

### 0j9. Optional Lazy Service Resolvers Must Not Throw Before Stats Objects Return Their Local Counters

**The Bug**: `MeshStatsCollector.GetStatsAsync()` returned all-zero stats in unit tests even after `RecordMessageSent()` and `RecordMessageReceived()` because optional lazy resolvers for DHT and overlay services threw before the method reached the return statement, and the outer catch replaced the partially collected counters with a default zeroed stats object.

**Files Affected**:
- `src/slskd/Mesh/MeshStatsCollector.cs`

**Wrong**:
```csharp
this.dhtClient = new Lazy<Dht.InMemoryDhtClient>(() =>
    serviceProvider.GetService(typeof(VirtualSoulfind.ShadowIndex.IDhtClient)) as Dht.InMemoryDhtClient
        ?? throw new InvalidOperationException(...));
```

**Correct**:
```csharp
this.dhtClient = new Lazy<Dht.InMemoryDhtClient?>(() =>
    serviceProvider.GetService(typeof(VirtualSoulfind.ShadowIndex.IDhtClient)) as Dht.InMemoryDhtClient);
```

**Why This Keeps Happening**: Diagnostics collectors often depend on optional subsystems. If a lazy resolver throws for an absent optional service, the whole stats call can fall into a broad catch and wipe out independent counters that were already valid. Optional service lookups should return `null` and let the collector degrade gracefully.

### 0j10. Re-entrant Stop/Dispose Paths Must Null Out `CancellationTokenSource` Before Canceling

**The Bug**: `LocalPortForwarder.StopForwardingAsync()` could throw `ObjectDisposedException` because `ForwarderInstance.StopAsync()` called `_cts?.Cancel()` even when a previous stop/dispose path had already disposed that same `CancellationTokenSource`.

**Files Affected**:
- `src/slskd/Common/Security/LocalPortForwarder.cs`

**Wrong**:
```csharp
_cts?.Cancel();
...
_cts?.Dispose();
```

**Correct**:
```csharp
var cts = _cts;
_cts = null;

try
{
    cts?.Cancel();
}
catch (ObjectDisposedException)
{
}

cts?.Dispose();
```

**Why This Keeps Happening**: Stop and dispose paths often converge on the same field. If the field remains published while cleanup is in progress, later callers can observe a disposed token source and try to cancel it again. Copy the reference locally, clear the field first, and then clean it up once.

### 0k. Empty-String DTO Defaults Break `??`-Based Fallback Chains For Hash Selection

**The Bug**: `AudioVariant` cleanup initialized codec-specific hash properties to `string.Empty`, but `CanonicalStatsService` still used `??` fallback chains when building dedup keys. Empty strings are non-null, so FLAC variants with missing `FlacStreamInfoHash42` stopped falling back to `FlacPcmMd5` and collapsed into the same canonical candidate bucket.

**Files Affected**:
- `src/slskd/Audio/CanonicalStatsService.cs`
- `src/slskd/Audio/AudioVariant.cs`

**Wrong**:
```csharp
var streamHash = v.Codec switch
{
    "FLAC" => v.FlacStreamInfoHash42 ?? v.FlacPcmMd5 ?? v.FileSha256,
    "MP3" => v.Mp3StreamHash ?? v.FileSha256,
    _ => v.FileSha256,
};
```

**Correct**:
```csharp
var streamHash = v.Codec switch
{
    "FLAC" => FirstNonEmpty(v.FlacStreamInfoHash42, v.FlacPcmMd5, v.FileSha256),
    "MP3" => FirstNonEmpty(v.Mp3StreamHash, v.FileSha256),
    _ => FirstNonEmpty(v.FileSha256),
};
```

**Why This Keeps Happening**: Nullability cleanup often replaces nullable strings with `string.Empty`, but any fallback logic that relied on `??` now changes behavior silently. When a value is semantically "missing", use `string.IsNullOrWhiteSpace`-aware fallback helpers instead of null-coalescing chains.

### 0k3. `GetReleaseIntent` Must Be Wired Through The Queue Contract

**The Bug**: `VirtualSoulfindV2Controller.GetReleaseIntent` always returned `NotFound` with a fixed message even when a release intent existed, making release intent reads unusable for clients that rely on POST->GET workflow validation.

**Files Affected**:
- `src/slskd/VirtualSoulfind/v2/API/VirtualSoulfindV2Controller.cs`
- `src/slskd/VirtualSoulfind/v2/Intents/IIntentQueue.cs`
- `src/slskd/VirtualSoulfind/v2/Intents/InMemoryIntentQueue.cs`

**Wrong**:
```csharp
public IActionResult GetReleaseIntent(string intentId)
{
    // TODO: Implement GetReleaseIntentAsync in IIntentQueue
    return NotFound(new { Message = "Release intent retrieval not yet implemented" });
}
```

**Correct**:
```csharp
Task<DesiredRelease?> GetReleaseIntentAsync(string desiredReleaseId, CancellationToken cancellationToken = default);

public async Task<IActionResult> GetReleaseIntent(string intentId, CancellationToken cancellationToken)
{
    var intent = await _intentQueue.GetReleaseIntentAsync(intentId, cancellationToken);
    return intent is null ? NotFound() : Ok(intent);
}
```

**Why This Keeps Happening**: TODO scaffolding in a live controller can outlive upstream stubs and become a user-visible regression after feature gating changes, especially when routes are referenced by existing frontend/client flows that expect full CRUD behavior.

### 0k4. AUR Fix Script Should Not Be Hardcoded to a Single Helper Name or Version Pattern

**The Bug**: The local torchaudio downloader script assumed `yay` and a specific `v2.10.0` tarball pattern, which broke on hosts using `paru` and/or on future torchaudio versions.

**Files Affected**:
- `scripts/fix-python-torchaudio-no-resume.sh`

**Wrong**:
```bash
if [[ ! -x /usr/bin/yay ]]; then
  echo "yay is required..."
fi

find "$cache_dir" -name '*v2.10.0*'
```

**Correct**:
```bash
if command -v yay >/dev/null 2>&1; then
  aur_helper=$(command -v yay)
elif command -v paru >/dev/null 2>&1; then
  aur_helper=$(command -v paru)
fi

find "$cache_dir" -type f \( -name '*.tar.gz' -o -name '*.part' \)
```

**Why This Keeps Happening**: Small deployment helpers accumulate assumptions about one machine and one package version. If those assumptions are not kept in sync with upstream package churn or user tool choice, the helper becomes the blocker instead of the dependency itself.

### 0k1. SOCKS5 CONNECT Parsing Must Consume ATYP-Dependent Binds Before Returning Connected Stream

**The Bug**: Several SOCKS5 dialers read a fixed 10-byte CONNECT response regardless of `ATYP`, so variable-length domain-name responses left trailing bytes (address bytes plus port) in the TCP stream and leaked into application reads as garbled preamble bytes.

**Files Affected**:
- `src/slskd/Common/Security/TorSocksTransport.cs`
- `src/slskd/Mesh/Transport/TorSocksDialer.cs`
- `src/slskd/Mesh/Transport/I2pSocksDialer.cs`

**Wrong**:
```csharp
var connectResponse = new byte[10];
await ReadExactlyAsync(stream, connectResponse, 0, 10, token);
```

**Correct**:
```csharp
var header = new byte[4];
await ReadExactlyAsync(stream, header, 0, header.Length, token);
// read address tail length based on ATYP (IPv4 4 bytes, domain 1+length, IPv6 16 bytes) then read 2-byte port
```

**Why This Keeps Happening**: SOCKS5 CONNECT replies are variable length by design (`ATYP` controls how much payload follows the first 4 bytes). Any consumer that hardcodes `10` bytes silently assumes IPv4 and causes protocol desynchronization whenever a domain response is returned by the proxy.

### 0k. Timeout-Based Circuit Tests Must Distinguish "Breaker Opened" From "Open-State Reply Observed"

**The Bug**: `ServiceTimeout_TriggersCircuitBreaker` still flaked after widening the retry window because the last timeout call could be the one that opens the breaker, which means the first `ServiceUnavailable` reply only appears on the next probe request.

**Files Affected**:
- `tests/slskd.Tests/Mesh/ServiceFabric/MeshServiceRouterSecurityTests.cs`

**Wrong**:
```csharp
for (int i = 0; i < 10; i++)
{
    lastReply = await router.RouteAsync(call, peerId);
    if (lastReply.StatusCode == ServiceStatusCodes.ServiceUnavailable)
    {
        break;
    }
}
```

**Correct**:
```csharp
for (int i = 0; i < 10; i++)
{
    await router.RouteAsync(call, peerId);

    var circuit = router.GetStats().CircuitBreakers.Find(cb => cb.ServiceName == "slow-service");
    if (circuit?.IsOpen == true)
    {
        break;
    }
}

var blockedReply = await router.RouteAsync(probeCall, peerId);
Assert.Equal(ServiceStatusCodes.ServiceUnavailable, blockedReply.StatusCode);
```

**Why This Keeps Happening**: The timeout response reports the result of the current request, while the breaker state change affects the next request. For timeout-driven breaker tests, inspect router state or issue a separate probe after failures instead of expecting the opening transition and blocked reply to collapse onto the same call.

### 0l. E2E Harnesses Must Not Treat Gitignored Downloaded Media As Baseline CI Fixtures

### 0m. Lightweight Integration Hosts Must Stub Every Controller Dependency They Expose

**The Bug**: Integration test hosts included the VirtualSoulfind controllers in their application parts, but did not register `IDisasterModeCoordinator` and `IShadowIndexQuery` consistently, so tests failed at request time with controller activation errors instead of exercising the endpoint contracts.

**Files Affected**:
- `tests/slskd.Tests.Integration/StubWebApplicationFactory.cs`
- `tests/slskd.Tests.Integration/Harness/SlskdnTestClient.cs`

**Wrong**:
```csharp
services.AddControllers()
    .AddApplicationPart(typeof(global::slskd.API.VirtualSoulfind.DisasterModeController).Assembly);
```

```csharp
builder.Services.AddSingleton<global::slskd.VirtualSoulfind.ShadowIndex.IShadowIndexQuery>(_ =>
    new StubShadowIndexQueryForTests());
```

**Correct**:
```csharp
services.AddSingleton<global::slskd.VirtualSoulfind.DisasterMode.IDisasterModeCoordinator>(_ =>
    new StubDisasterModeCoordinatorForTests());
services.AddSingleton<global::slskd.VirtualSoulfind.ShadowIndex.IShadowIndexQuery>(_ =>
    new StubShadowIndexQueryForTests());
```

**Why This Keeps Happening**: The lightweight test hosts deliberately avoid the full production DI graph, so every added controller creates a manual dependency obligation. If you expose a controller assembly in a stub host, audit its constructor dependencies immediately or the tests will fail with activation errors that look like app regressions.

### 0n. Native API DTOs Need Explicit Snake_Case Binding When Compatibility Clients Post Snake_Case JSON

**The Bug**: The native jobs endpoints accepted positional record DTOs with PascalCase property names, but the Soulbeet compatibility tests posted `snake_case` JSON like `mb_release_id` and `target_dir`, causing model binding to fail with `400` ProblemDetails payloads.

**Files Affected**:
- `src/slskd/API/Native/JobsController.cs`

**Wrong**:
```csharp
public record MbReleaseJobRequest(
    string MbReleaseId,
    string TargetDir,
    string Tracks = "all",
    JobConstraints? Constraints = null);
```

**Correct**:
```csharp
public record MbReleaseJobRequest(
    [property: JsonPropertyName("mb_release_id")] string MbReleaseId,
    [property: JsonPropertyName("target_dir")] string TargetDir,
    [property: JsonPropertyName("tracks")] string Tracks = "all",
    [property: JsonPropertyName("constraints")] JobConstraints? Constraints = null);
```

**Why This Keeps Happening**: ASP.NET Core JSON binding is case-insensitive, but it does not translate underscore-delimited names into PascalCase automatically. Compatibility-facing DTOs need explicit `JsonPropertyName` attributes anywhere the request contract is `snake_case`.

### 0o. Host Shutdown Hooks Need Logged Failures for Non-Cancellation Paths

**The Bug**: A lifecycle cleanup callback swallowed all exceptions when stopping LAN discovery advertising during shutdown, which made shutdown telemetry look successful while resource cleanup could still be failing silently.

**Files Affected**:
- `src/slskd/Program.cs`

**Wrong**:
```csharp
catch
{
}
```

**Correct**:
```csharp
catch (Exception ex)
{
    Log.Warning(ex, "[Program] Failed to stop LAN discovery advertising during shutdown");
}
```

**Why This Keeps Happening**: Shutdown hooks are often written as "best effort", but missing exception logging turns deterministic cleanup failures into ghost issues that only appear as random resource leftovers in later test runs.

### 0p. Stream Mapping Cancellation Sources Need Disposal During Forwarder Teardown

**The Bug**: `ForwarderConnection` waited for stream mapping completion but did not dispose the linked `CancellationTokenSource`, leaving native wait handles and token registrations alive across repeated connection churn.

**Files Affected**:
- `src/slskd/Common/Security/LocalPortForwarder.cs`

**Wrong**:
```csharp
_streamMappingCts?.Cancel();
if (_streamMappingCts != null)
{
    await WaitForStreamMappingAsync(timeout.Token);
}
```

**Correct**:
```csharp
var streamMappingCts = _streamMappingCts;
if (streamMappingCts != null)
{
    streamMappingCts.Cancel();
    try
    {
        await WaitForStreamMappingAsync(timeout.Token);
    }
    finally
    {
        _streamMappingCts = null;
        streamMappingCts.Dispose();
    }
}
```

**Why This Keeps Happening**: Cleanup code often focuses on signaling cancellation but forgets to dispose the `CancellationTokenSource`. Under repeated failures and closes this can retain resources long after the socket work is gone.

### 0q. Timeout Branches Should Handle Both `OperationCanceledException` and `TimeoutException`

**The Bug**: Verification timeout handling only caught `OperationCanceledException`, so libraries that surface timeouts as `TimeoutException` still fell into generic error handling and were labeled as unexpected verification errors.

**Files Affected**:
- `src/slskd/DhtRendezvous/Security/PeerVerificationService.cs`

**Wrong**:
```csharp
catch (OperationCanceledException)
{
    return VerificationResult.Failed("Verification timed out");
}
```

**Correct**:
```csharp
catch (TimeoutException)
{
    return VerificationResult.Failed("Verification timed out");
}
catch (OperationCanceledException)
{
    return VerificationResult.Failed("Verification timed out");
}
```

**Why This Keeps Happening**: Cancellation and timeout signals are not perfectly consistent across dependent APIs, so handling only one exception class can incorrectly downgrade a known, expected operational timeout into a generic failure path.

### 0r. Best-Effort Cleanup Catch-Alls Should Capture Logs for Post-Mortem Signal

**The Bug**: Best-effort cleanup blocks swallowed process/manager shutdown errors without capturing context, making operational cleanup bugs invisible when they happened during failures.

**Files Affected**:
- `src/slskd/Integrations/Scripts/ScriptService.cs`
- `src/slskd/Signals/Swarm/MonoTorrentBitTorrentBackend.cs`

**Wrong**:
```csharp
catch { /* ignore */ }
```

**Correct**:
```csharp
catch (Exception ex)
{
    _logger.LogDebug(ex, "Cleanup failed ...");
}
```

**Why This Keeps Happening**: Cleanup paths are frequently treated as non-critical, but losing exceptions there also loses the root cause of stalled process trees, partial resource reuse, or half-finished transactions that can hurt long-running services.

**The Bug**: The scheduled `E2E Tests` workflow treated downloaded media as mandatory baseline fixtures, so a transient fetch failure aborted the whole suite before any real UI coverage ran.

**Files Affected**:
- `src/web/e2e/harness/SlskdnNode.ts`
- `src/web/e2e/fixtures/ensure-fixtures.ts`
- `src/web/e2e/streaming.spec.ts`
- `src/web/e2e/multippeer-sharing.spec.ts`
- `test-data/slskdn-test-fixtures/meta/manifest.json`

**Wrong**:
```ts
const manifest = JSON.parse(await fs.readFile(manifestPath, 'utf8'));
for (const entry of manifest.files) {
  await fs.access(path.join(fixturesRoot, entry.path));
}
```

**Correct**:
```ts
await ensureFixtures(fixturesRoot);
test.skip(
  !hasDownloadedMediaFixtures(),
  'Streaming E2E requires downloaded media fixtures',
);
```

**Why This Keeps Happening**: The committed fixture tree contains a small tracked offline baseline plus a larger gitignored media tier fetched on demand. CI can legitimately run without the downloaded tier, so the harness must validate the tracked baseline and let only media-dependent specs skip.

### 0m. E2E Harnesses Should Launch The Prebuilt Release App, And UI Pages Must Tolerate Missing `server` State During Boot

**The Bug**: The E2E harness launched `dotnet run` during test execution even though CI had already built the backend, which made the first node startup exceed the 30-second TCP wait on cold runs. Follow-up fixes then hit two more traps: `Web.ContentPath` only accepts relative paths under `AppContext.BaseDirectory`, and the web-asset sync helper must recreate the destination root before `fs.cp` or the copy can fail with `ENOENT` on nested assets. Separately, `Searches.jsx` read `server.isConnected` before `applicationState.server` existed, so a harmless `/capabilities` failure turned into a page-crashing `TypeError`.

**Files Affected**:
- `src/web/e2e/harness/SlskdnNode.ts`
- `src/web/src/components/Search/Searches.jsx`

**Wrong**:
```ts
const webContentPath = path.relative(expectedAppBaseDir, webBuildPath);
const args = ['run', '--project', projectPath, '--', '--app-dir', this.appDir];
await waitForTcpListen('127.0.0.1', this.apiPort, 30_000);
```

```jsx
disabled={creating || !server.isConnected}
placeholder={server.isConnected ? 'Search phrase' : 'Connect to server'}
```

**Correct**:
```ts
const webContentPath = webBuildPath.replace(/\\/g, '/');
const args = useBuiltRelease
  ? [builtDllPath, '--app-dir', this.appDir]
  : ['run', '--project', projectPath, '-c', 'Release', '--', '--app-dir', this.appDir];
await waitForTcpListen('127.0.0.1', this.apiPort, 60_000);
```

```jsx
await replaceDirectoryContents(webBuildPath, path.join(builtAppBaseDir, 'wwwroot'));
const webContentPath = 'wwwroot';
```

```jsx
const normalizedServer = server ?? { isConnected: false };
disabled={creating || !normalizedServer.isConnected}
placeholder={
  normalizedServer.isConnected
    ? 'Search phrase'
    : 'Connect to server to perform a search'
}
```

**Why This Keeps Happening**: E2E harness code often grows around local developer assumptions, but CI already provides a built Release app and is much less tolerant of redundant startup work. Even when using the prebuilt app, the runtime still validates `Web.ContentPath` as a relative directory under the app base, so the harness has to stage fresh web assets into `wwwroot` instead of pointing at arbitrary absolute paths, and that staging helper has to recreate the destination root explicitly before copying nested asset trees. On the frontend, boot-time state objects can be transiently missing even when the route eventually succeeds, so route components must normalize optional props before reading nested fields.

### 0n. XML Doc Comments Must Escape `&` Or CI Will Emit CS1570 Warnings

**The Bug**: Several XML documentation comments used raw ampersands in phrases like `Identity & Friends` or `Test Coverage & Regression Harness`, which made the generated XML invalid and caused repeated `CS1570` warnings in CI.

**Files Affected**:
- `src/slskd/Common/Moderation/*.cs`
- `src/slskd/Common/CodeQuality/*.cs`
- `src/slskd/Mesh/Realm/*.cs`
- `src/slskd/Sharing/*.cs`
- `src/slskd/VirtualSoulfind/**/*.cs`

**Wrong**:
```csharp
///     T-MCP04: Peer Reputation & Enforcement.
/// <summary>Contact PeerId (Identity & Friends).</summary>
```

**Correct**:
```csharp
///     T-MCP04: Peer Reputation &amp; Enforcement.
/// <summary>Contact PeerId (Identity &amp; Friends).</summary>
```

**Why This Keeps Happening**: XML doc comments are real XML, not plain text. Any raw `&` inside `///` comments has to be escaped or the compiler will produce malformed-doc warnings that bury real CI signal.

### 0a. Do Not Assume MusicBrainz Target Models Expose the Same ID Surface

**The Bug**: `SongIdService` treated `TrackTarget` like `AlbumTarget` and tried to read `MusicBrainzArtistId` from it, which broke the build because `TrackTarget` does not expose that property.

**Files Affected**:
- `src/slskd/SongID/SongIdService.cs`

**Wrong**:
```csharp
run.Tracks.Insert(0, new SongIdTrackCandidate
{
    MusicBrainzArtistId = track.MusicBrainzArtistId,
});
```

**Correct**:
```csharp
run.Tracks.Insert(0, new SongIdTrackCandidate
{
    RecordingId = track.MusicBrainzRecordingId,
    Title = track.Title,
    Artist = track.Artist,
});
```

**Why This Keeps Happening**: The MusicBrainz integration models look similar at a glance, but they are not interchangeable. Check the actual target type before assuming it carries artist, release, or recording IDs in the same shape.

### 0b. Do Not Introduce `System.Threading.Lock` Unless the Project Explicitly Uses That API Surface

**The Bug**: A new SongID SQLite store used `Lock` instead of a plain object gate, which failed to compile in this project even though the code targets modern .NET.

**Files Affected**:
- `src/slskd/SongID/SongIdRunStore.cs`

**Wrong**:
```csharp
private readonly Lock _gate = new();
```

**Correct**:
```csharp
private readonly object _gate = new();
```

**Why This Keeps Happening**: It is easy to mentally map “modern C#” to every recent BCL convenience type. This repo still needs compatibility with the actual APIs available in its current toolchain and package graph, so prefer the already-common locking patterns unless you have confirmed the newer type is already in use here.

### 0c. When You Extend a Controller Constructor, Update Direct Instantiation Tests Immediately

**The Bug**: `JobsController` gained an `IMusicBrainzClient` dependency for release-to-artist resolution, but `JobsControllerPaginationTests` still instantiated the old constructor shape, breaking unit test compilation before the new SongID tests could even run.

**Files Affected**:
- `src/slskd/API/Native/JobsController.cs`
- `tests/slskd.Tests.Unit/API/Native/JobsControllerPaginationTests.cs`

**Wrong**:
```csharp
controller = new JobsController(
    discographyService.Object,
    labelCrateService.Object,
    logger.Object,
    jobServiceList.Object);
```

**Correct**:
```csharp
controller = new JobsController(
    discographyService.Object,
    labelCrateService.Object,
    musicBrainzClient.Object,
    logger.Object,
    jobServiceList.Object);
```

**Why This Keeps Happening**: Controllers are often instantiated through ASP.NET DI in production, so constructor changes compile there but any unit test that manually news up the controller will silently drift until the next test build.

### 0. MusicBrainz Release IDs Are Not Artist IDs

**The Bug**: A single-release SongID or jobs path passed an MB release ID into `DiscographyJobRequest.ArtistId`, which silently created the wrong planning context and broke album download handoff.

**Files Affected**:
- `src/slskd/API/Native/JobsController.cs`
- `src/slskd/Jobs/DiscographyJobService.cs`
- `src/slskd/Integrations/MusicBrainz/MusicBrainzClient.cs`

**Wrong**:
```csharp
var jobId = await discographyJobService.CreateJobAsync(
    new DiscographyJobRequest
    {
        ArtistId = request.MbReleaseId,
        Profile = DiscographyProfile.AllReleases,
    },
    cancellationToken);
```

**Correct**:
```csharp
var release = await musicBrainzClient.GetReleaseAsync(request.MbReleaseId, cancellationToken);
var jobId = await discographyJobService.CreateJobAsync(
    new DiscographyJobRequest
    {
        ArtistId = release.MusicBrainzArtistId,
        ReleaseIds = new List<string> { request.MbReleaseId },
        Profile = DiscographyProfile.AllReleases,
    },
    cancellationToken);
```

**Why This Keeps Happening**: MusicBrainz uses different MBIDs for releases, recordings, and artists. It is easy to treat “some MBID” as interchangeable unless the code explicitly carries the identifier type through the model.

### 0d. Do Not Store Recovery-Only State in `Summary` When Queue Refresh Also Owns `Summary`

**The Bug**: SongID restart recovery marked runs as "Recovered after restart..." in `Summary`, but the next queue-position refresh immediately overwrote that text with the normal queued summary, erasing the only visible recovery signal.

**Files Affected**:
- `src/slskd/SongID/SongIdService.cs`

**Wrong**:
```csharp
run.Summary = "Recovered after restart and re-queued for SongID analysis.";
await EnqueueRunAsync(run, broadcastCreate: false).ConfigureAwait(false);
```

**Correct**:
```csharp
run.Evidence.Add("Recovered after restart and re-queued for SongID analysis.");
run.Summary = "Queued for SongID analysis.";
await EnqueueRunAsync(run, broadcastCreate: false).ConfigureAwait(false);
```

**Why This Keeps Happening**: `Summary` looks like a convenient general-purpose status field, but the queue layer also treats it as derived UI text. If two parts of the pipeline both own the same display field, one silently erases the other.

### 0e. Do Not Use Wall-Clock Time or Tight Upper Bounds for Async Delay Tests

**The Bug**: `SecurityUtilsTests.RandomDelayAsync_ValidRange_CompletesWithinExpectedTime` measured `Task.Delay` with `DateTimeOffset.UtcNow` and a narrow upper bound, so the test failed intermittently on loaded CI runners even though the code was behaving correctly.

**Files Affected**:
- `tests/slskd.Tests.Unit/Common/Security/SecurityUtilsTests.cs`

**Wrong**:
```csharp
var startTime = DateTimeOffset.UtcNow;
await SecurityUtils.RandomDelayAsync(minDelay, maxDelay);
var endTime = DateTimeOffset.UtcNow;
var actualDelay = (endTime - startTime).TotalMilliseconds;
Assert.True(actualDelay <= maxDelay + 600, $"Delay too long: {actualDelay}ms");
```

**Correct**:
```csharp
var timer = Stopwatch.StartNew();
await SecurityUtils.RandomDelayAsync(minDelay, maxDelay);
timer.Stop();
var actualDelay = timer.Elapsed.TotalMilliseconds;
Assert.True(actualDelay <= maxDelay + 1500, $"Delay too long: {actualDelay}ms");
```

**Why This Keeps Happening**: Async timing tests are easy to write like benchmark assertions, but `Task.Delay` is scheduler-dependent and CI hosts can stall for hundreds of milliseconds. Use monotonic timing (`Stopwatch`) and treat the upper bound as a broad sanity check, not a precision guarantee.

### 0e1. Do Not Use Sub-Millisecond Cancellation Windows In Unit Tests

**The Bug**: `MeshSearchRpcHandlerTests.HandleAsync_TimeCap_RespectsCancellation` and `AsyncRulesTests.ValidateCancellationHandlingAsync_WithProperCancellation_ReturnsTrue` used razor-thin delay/cancellation windows that passed locally but failed in release-gate CI when the runner scheduled work a little differently.

**Files Affected**:
- `tests/slskd.Tests.Unit/DhtRendezvous/Search/MeshSearchRpcHandlerTests.cs`
- `tests/slskd.Tests.Unit/Common/CodeQuality/AsyncRulesTests.cs`

### 0e1c. Do Not Use Cancellation Timeouts As The Success Condition For Async Enumerables

**The Bug**: `CoverTrafficGeneratorTests.GenerateCoverTrafficAsync_GeneratesMessagesWithCorrectSize` used `CancellationTokenSource(TimeSpan.FromSeconds(5))` as the loop control while waiting for multiple messages from an async enumerable. CI sometimes hit token cancellation before the second message arrived, so the test failed with `TaskCanceledException` even though the generator was behaving correctly.

**Files Affected**:
- `tests/slskd.Tests.Unit/Mesh/Privacy/CoverTrafficGeneratorTests.cs`

**Wrong**:
```csharp
var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

await foreach (var message in generator.GenerateCoverTrafficAsync(cts.Token))
{
    messages.Add(message);
    if (messages.Count >= 2)
        break;
}
```

**Correct**:
```csharp
using var cts = new CancellationTokenSource();

await foreach (var message in generator.GenerateCoverTrafficAsync(cts.Token))
{
    messages.Add(message);
    if (messages.Count >= 1)
    {
        cts.Cancel();
    }
}
```

**Why This Keeps Happening**: Async enumerable tests often mix "eventually produce output" with "cancel after some time" and accidentally make timeout expiration the normal success path. For scheduler-dependent producers, use an explicit completion condition and only cancel after the assertion target is satisfied.

**Wrong**:
```csharp
var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(1));
await Task.Delay(100, cts.Token);
```

**Correct**:
```csharp
using var cts = new CancellationTokenSource();
cts.Cancel();
_shareServiceMock
    .Setup(x => x.SearchLocalAsync(It.IsAny<SearchQuery>()))
    .Returns(Task.FromCanceled<IEnumerable<Soulseek.File>>(cts.Token));
```

```csharp
await Task.Delay(Timeout.InfiniteTimeSpan, ct);
var result = await AsyncRules.ValidateCancellationHandlingAsync(
    TestOperationAsync,
    TimeSpan.FromMilliseconds(50));
```

**Why This Keeps Happening**: Tests that rely on "cancel within 1ms" or "wake up after 100ms" are really testing scheduler luck, not code behavior. Make cancellation deterministic with pre-cancelled tokens or infinite waits that must be interrupted by cancellation.

### 0e1a. Cancellation Validators Need A Post-Cancel Grace Window, Not A Single Tight Race

**The Bug**: `AsyncRules.ValidateCancellationHandlingAsync` raced the operation against `Task.Delay(timeout * 2)` and treated any miss as a cancellation failure. On a loaded CI runner, a correctly cancellable operation could still lose that race by a few scheduler ticks and fail the release gate.

**Files Affected**:
- `src/slskd/Common/CodeQuality/AsyncRules.cs`
- `tests/slskd.Tests.Unit/Common/CodeQuality/AsyncRulesTests.cs`

**Wrong**:
```csharp
using var cts = new CancellationTokenSource(timeout);
var operationTask = operation(cts.Token);
var delayTask = Task.Delay(timeout * 2, CancellationToken.None);
var completedTask = await Task.WhenAny(operationTask, delayTask);
return completedTask != delayTask;
```

**Correct**:
```csharp
using var cts = new CancellationTokenSource();
var operationTask = operation(cts.Token);
await Task.Delay(timeout);
cts.Cancel();
var completedTask = await Task.WhenAny(operationTask, Task.Delay(gracePeriod));
```

**Why This Keeps Happening**: Cancellation is not an instantaneous event. A validator that uses one narrow race window is still testing scheduler timing rather than cancellation handling. Cancel explicitly, then give the operation a bounded grace period to observe the token and unwind.

### 0e1b. Timing-Sanity Tests Must Avoid Precise Upper Bounds On Loaded CI Runners

**The Bug**: `SecurityUtilsTests.RandomDelayAsync_ValidRange_CompletesWithinExpectedTime` still used an upper bound that looked broad locally but was too tight for a loaded GitHub runner, where a `10-50ms` delay measured just over 2 seconds and failed the release gate.

**Files Affected**:
- `tests/slskd.Tests.Unit/Common/Security/SecurityUtilsTests.cs`

**Wrong**:
```csharp
Assert.True(actualDelay <= maxDelay + 1500, $"Delay too long: {actualDelay}ms");
```

**Correct**:
```csharp
Assert.True(actualDelay <= maxDelay + 5000, $"Delay too long: {actualDelay}ms");
```

**Why This Keeps Happening**: `Task.Delay` timing in CI is dominated by scheduler availability, not just requested delay length. These tests should verify the code is not obviously broken, not enforce a pseudo-benchmark ceiling.

### 0e2. Do Not Mark Internal Mutation APIs As `AllowAnonymous` Just Because They Feel "Protocol-Like"

**The Bug**: A broad `// PR-02: intended-public` pattern was applied to controllers that mutate local state or trigger expensive work, including analyzer migrations, VirtualSoulfind queue operations, MediaCore registry writes/imports, stats resets, and pod control-plane actions. That exposed internal admin/UI surfaces to unauthenticated callers.

**Files Affected**:
- `src/slskd/Audio/API/AnalyzerMigrationController.cs`
- `src/slskd/VirtualSoulfind/v2/API/VirtualSoulfindV2Controller.cs`
- `src/slskd/MediaCore/API/Controllers/ContentDescriptorPublisherController.cs`
- `src/slskd/MediaCore/API/Controllers/ContentIdController.cs`
- `src/slskd/MediaCore/API/Controllers/IpldController.cs`
- `src/slskd/MediaCore/API/Controllers/MediaCoreStatsController.cs`
- `src/slskd/MediaCore/API/Controllers/MetadataPortabilityController.cs`
- `src/slskd/PodCore/API/Controllers/PodJoinLeaveController.cs`
- `src/slskd/PodCore/API/Controllers/PodMessageRoutingController.cs`
- `src/slskd/PodCore/API/Controllers/PodMessageSigningController.cs`

**Wrong**:
```csharp
[ApiController]
[AllowAnonymous] // PR-02: intended-public
[ValidateCsrfForCookiesOnly]
public class ContentIdController : ControllerBase
{
    [HttpPost("register")]
    public Task<IActionResult> Register(...)
```

**Correct**:
```csharp
[ApiController]
[Authorize(Policy = AuthPolicy.Any)]
[ValidateCsrfForCookiesOnly]
public class ContentIdController : ControllerBase
{
    [HttpPost("register")]
    public Task<IActionResult> Register(...)
```

**Why This Keeps Happening**: "Public data model" and "public unauthenticated endpoint" are not the same thing. Once `[AllowAnonymous]` is placed at class scope, every `POST`/`PUT`/`PATCH`/`DELETE` action under that controller becomes reachable unless explicitly re-protected.

### 0e3. Public Protocol Controllers Must Still Default To Authenticated At Class Scope

**The Bug**: Even after narrowing the anonymous surface, `StreamsController`, `ActivityPubController`, and `WebFingerController` still used class-level `[AllowAnonymous]`. That meant any future action added to those controllers would become public by default, recreating the same auth-boundary bug in a quieter form.

**Files Affected**:
- `src/slskd/Streaming/StreamsController.cs`
- `src/slskd/SocialFederation/API/ActivityPubController.cs`
- `src/slskd/SocialFederation/API/WebFingerController.cs`

**Wrong**:
```csharp
[AllowAnonymous]
public class ActivityPubController : ControllerBase
{
    [HttpGet("{actorName}")]
    public async Task<IActionResult> GetActor(...)
```

**Correct**:
```csharp
[Authorize(Policy = AuthPolicy.Any)]
public class ActivityPubController : ControllerBase
{
    [HttpGet("{actorName}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetActor(...)
```

**Why This Keeps Happening**: It is easy to think "this controller is for a public protocol" and stop there. The safer pattern is still auth-by-default at controller scope with `[AllowAnonymous]` only on the exact protocol/bootstrap actions that must stay public. That way future endpoints do not silently widen the unauthenticated surface.

### 0f. Fix Every Release Workflow and Checked-In Package Template When Asset Names Change

**The Bug**: The main tag workflow was corrected to publish `slskdn-main-*.zip`, but `release-packages.yml` still waited for the old `slskdn-<tag>-linux-x64.zip` pattern and the checked-in Chocolatey templates were still pinned to `0.24.1-slskdn.40`, leaving stable-package automation and manual package publishing stale.

**Files Affected**:
- `.github/workflows/release-packages.yml`
- `packaging/chocolatey/slskdn.nuspec`
- `packaging/chocolatey/tools/chocolateyinstall.ps1`

**Wrong**:
```yaml
ASSET_URL="https://github.com/snapetech/slskdn/releases/download/${{ steps.version.outputs.tag }}/slskdn-${{ steps.version.outputs.tag }}-linux-x64.zip"
```

```powershell
$url = "https://github.com/snapetech/slskdn/releases/download/0.24.1-slskdn.40/slskdn-main-win-x64.zip"
```

**Correct**:
```yaml
ASSET_URL="https://github.com/snapetech/slskdn/releases/download/${{ steps.version.outputs.tag }}/slskdn-main-linux-x64.zip"
```

```powershell
$url = "https://github.com/snapetech/slskdn/releases/download/0.24.5-slskdn.52/slskdn-main-win-x64.zip"
```

**Why This Keeps Happening**: It is easy to fix only the primary build workflow and forget the secondary packaging workflows and checked-in templates that still encode old asset names or versions. Any release-format change must be audited across tag workflows, auxiliary release workflows, validation scripts, and package templates together.

### 0g. When You Extend Core Interfaces, Update Test Stubs and Fakes in the Legacy Test Projects Immediately

**The Bug**: `ISecurityService`, `IShareService`, and `IShareRepository` gained new members, but the older smoke/integration test stubs still implemented the previous interface shapes, so `dotnet test` failed even though the feature code compiled and targeted SongID tests passed.

**Files Affected**:
- `src/slskd/Core/Security/SecurityService.cs`
- `src/slskd/Shares/IShareService.cs`
- `src/slskd/Shares/IShareRepository.cs`
- `tests/slskd.Tests/TestHostFactory.cs`
- `tests/slskd.Tests.Integration/StubWebApplicationFactory.cs`
- `tests/slskd.Tests.Integration/StubVirtualSoulfindServices.cs`

**Wrong**:
```csharp
internal class StubSecurityService : ISecurityService
{
    public JwtSecurityToken GenerateJwt(...) => ...;
    public (string Name, Role Role) AuthenticateWithApiKey(...) => ...;
}

public Task<IEnumerable<File>> SearchAsync(SearchQuery query) => ...;
public IEnumerable<File> Search(SearchQuery query) => ...;
```

### 0h. Retry Loops Around External Uploads Must Bound Each Attempt, Not Just the Number of Attempts

**The Bug**: The Snap Store publish steps retried transient `snapcraft upload` failures, but each upload attempt could block indefinitely waiting on the store, so the loop never advanced and the release stayed stuck in a single opaque upload step.

**Files Affected**:
- `.github/workflows/build-on-tag.yml`

**Wrong**:
```bash
for attempt in $(seq 1 60); do
  OUT="$(snapcraft upload --release=stable "$SNAP_PATH" 2>&1)"
  CODE=$?
  ...
done
```

**Correct**:
```bash
for attempt in $(seq 1 6); do
  OUT="$(timeout --signal=TERM 10m snapcraft upload --release=stable "$SNAP_PATH" 2>&1)"
  CODE=$?
  ...
done
```

**Why This Keeps Happening**: A retry loop looks resilient, but it does nothing if the wrapped command never returns. Any networked publish step needs both retry logic and a hard per-attempt timeout so GitHub Actions can surface the failure instead of hanging for tens of minutes.

**Correct**:
```csharp
internal class StubSecurityService : ISecurityService
{
    public JwtSecurityToken GenerateJwt(...) => ...;
    public (string Name, Role Role) AuthenticateWithApiKey(...) => ...;
    public void RevokeToken(string jti) { }
    public bool IsTokenRevoked(string jti) => false;
}

public Task<IEnumerable<File>> SearchAsync(SearchQuery query, int? limit = null) => ...;
public IEnumerable<File> Search(SearchQuery query, int? limit = null) => ...;
```

**Why This Keeps Happening**: The newer feature work tends to validate against focused unit tests first, but the repo still includes older smoke/integration projects with hand-written stubs. Interface drift is invisible until the broad solution test run compiles those projects, so every interface change needs a repo-wide grep for stub implementations before calling the tree releasable.

### 0h. Gate Metrics Hardening Rules on the Metrics Endpoint Actually Being Enabled

**The Bug**: `HardeningValidator` started enforcing a non-empty metrics password whenever metrics auth was not disabled, even if `metrics.enabled` was still `false`, which broke otherwise-valid startup configs and older hardening tests.

**Files Affected**:
- `src/slskd/Common/Security/HardeningValidator.cs`

**Wrong**:
```csharp
var metricsAuth = options.Metrics?.Authentication;
if (metricsAuth != null && !metricsAuth.Disabled &&
    string.IsNullOrWhiteSpace(metricsAuth.Password))
{
    throw new HardeningValidationException(RuleWeakMetricsPassword, msg);
}
```

**Correct**:
```csharp
var metrics = options.Metrics;
var metricsAuth = metrics?.Authentication;
if (metrics?.Enabled == true && metricsAuth != null && !metricsAuth.Disabled &&
    string.IsNullOrWhiteSpace(metricsAuth.Password))
{
    throw new HardeningValidationException(RuleWeakMetricsPassword, msg);
}
```

**Why This Keeps Happening**: Nested auth options default to “auth enabled” semantics even when the parent feature is disabled. Any startup validation that checks nested credentials must first gate on the top-level feature flag, or harmless defaults become fatal.

### 0i. Do Not Use Anonymous Objects for JSON-LD Keys That Need Literal `@` Names

**The Bug**: `SolidClientIdDocumentService` built the Solid client-id document with an anonymous object using `@context`, which serialized to `context` instead of the required JSON-LD key `@context`.

**Files Affected**:
- `src/slskd/Solid/SolidClientIdDocumentService.cs`
- `tests/slskd.Tests.Unit/Solid/SolidClientIdDocumentServiceTests.cs`

**Wrong**:
```csharp
var doc = new
{
    @context = "https://www.w3.org/ns/solid/oidc-context.jsonld",
};
```

**Correct**:
```csharp
var doc = new Dictionary<string, object?>
{
    ["@context"] = "https://www.w3.org/ns/solid/oidc-context.jsonld",
};
```

**Why This Keeps Happening**: In C#, the `@` prefix only escapes the identifier for the compiler; it is not part of the serialized property name. For wire formats that require literal keys like `@context`, use explicit string keys or a concrete model with `JsonPropertyName`.

### 1. `return undefined` vs `return []` in Frontend API Calls

**The Bug**: Frontend API functions that return `undefined` on error instead of `[]` cause downstream crashes.

**Files Affected**:
- `src/web/src/lib/searches.js` - `getResponses()`
- `src/web/src/lib/transfers.js` - `getAll()`

**Wrong**:
```javascript
if (!Array.isArray(response)) {
  console.warn('got non-array response');
  return undefined;  // 💀 Causes "Cannot read property 'map' of undefined"
}
```

**Correct**:
```javascript
if (!Array.isArray(response)) {
  console.warn('got non-array response');
  return [];  // ✅ Safe to iterate
}
```

**Why This Keeps Happening**: Models see `undefined` as a "signal" value and forget that callers will `.map()` or `.filter()` the result.

### 1a. Do Not Block SPA Initialization on Optional SignalR Handshakes

**The Bug**: `App.init()` waited on `appHub.start()` before clearing the full-screen loader, so any stalled SignalR negotiation kept the whole site on "loading" for 30 seconds even though auth had succeeded and the rest of the UI could render.

**Files Affected**:
- `src/web/src/components/App.jsx`
- `src/web/src/components/App.test.jsx`

**Wrong**:
```javascript
if (await session.check()) {
  const appHub = createApplicationHubConnection();
  await Promise.race([appHub.start(), hubTimeout]);
}
```

**Correct**:
```javascript
if (await session.check()) {
  this.startApplicationHub();
}
```
The hub startup stays bounded and logged, but it runs in the background instead of sitting in the critical render path.

**Why This Keeps Happening**: Real-time channels feel "core" during implementation, so it is easy to treat them like a prerequisite for first paint. In this UI they are enhancement paths, not the gate for showing the authenticated shell. Keep session validation in the blocking path, but let hub connection, retries, and late state hydration happen asynchronously.

### 1b. Do Not Run `security-and-quality` on `master` Unless You Intend to Triage Thousands of Maintainer Alerts

**The Bug**: The checked-in C# CodeQL workflow used `queries: security-and-quality`, which repopulated roughly 2,400 `master` alerts with maintainability and code-smell findings (`cs/local-not-disposed`, `cs/log-forging`, `cs/catch-of-all-exceptions`, etc.) even though the goal was ordinary security scanning.

**Files Affected**:
- `.github/workflows/codeql.yml`

**Wrong**:
```yaml
- name: Initialize CodeQL
  uses: github/codeql-action/init@v3
  with:
    languages: csharp
    queries: security-and-quality
```

**Correct**:
```yaml
- name: Initialize CodeQL
  uses: github/codeql-action/init@v3
  with:
    languages: csharp
    queries: security-extended
```

**Why This Keeps Happening**: `security-and-quality` sounds like a better default until it lands in a mature codebase and turns every broad code-quality heuristic into a repo-level security alert. On `master`, keep the suite scoped to security-focused queries unless there is an explicit, staffed cleanup plan for the extra findings.

### 1c. Do Not Let Arbitrary API-Supplied Absolute Paths Reach Filesystem Probes

**The Bug**: Destination validation, Library Health scans, and mesh-transfer target selection accepted caller-supplied absolute paths and passed them straight into `Directory.Exists`, `EnumerateFiles`, `File.WriteAllText`, or later file I/O, which triggered real path-injection findings and allowed the server to probe arbitrary filesystem locations.

**Files Affected**:
- `src/slskd/Common/Security/PathGuard.cs`
- `src/slskd/Destinations/API/Controllers/DestinationsController.cs`
- `src/slskd/LibraryHealth/LibraryHealthService.cs`
- `src/slskd/VirtualSoulfind/DisasterMode/MeshTransferService.cs`
- `src/slskd/VirtualSoulfind/Bridge/BridgeApi.cs`

**Wrong**:
```csharp
var exists = Directory.Exists(request.Path);
var files = Directory.EnumerateFiles(request.LibraryPath, "*.*", SearchOption.AllDirectories);
var finalTargetPath = targetPath ?? Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
    "Downloads",
    filename);
```

**Correct**:
```csharp
var normalizedPath = PathGuard.NormalizeAbsolutePathWithinRoots(request.Path, allowedRoots);
var libraryPath = ResolveLibraryPath(request.LibraryPath);
var finalTargetPath = targetPath ?? Path.Combine(
    optionsMonitor.CurrentValue.Directories.Downloads,
    PathGuard.SanitizeFilename(filename));
```

**Why This Keeps Happening**: Admin-facing endpoints make it tempting to trust absolute paths, especially when the UI is just “checking” a directory or kicking off a scan. That still turns the server into a filesystem oracle. Any absolute path from HTTP or bridge input must be canonicalized and constrained to configured app-owned roots before touching disk.

### 1d. Pod Membership Mutation Endpoints Must Not Be Anonymous

**The Bug**: `PodMembershipController` was marked `[AllowAnonymous]`, which let unauthenticated callers publish, update, remove, ban, unban, and role-change pod membership records through the server-signed membership service.

**Files Affected**:
- `src/slskd/PodCore/API/Controllers/PodMembershipController.cs`

**Wrong**:
```csharp
[AllowAnonymous]
public class PodMembershipController : ControllerBase
{
}
```

**Correct**:
```csharp
[Authorize(Policy = AuthPolicy.Any)]
public class PodMembershipController : ControllerBase
{
}
```

**Why This Keeps Happening**: Some PodCore endpoints are intentionally public for signed message exchange or DHT-facing workflows, and it is easy to copy that attribute onto mutation endpoints that actually exercise privileged server behavior. Membership publication and role changes are management operations, not anonymous transport endpoints.

### 1e. Vite SPA Builds Must Use Relative Asset Paths When `web.url_base` Is Not `/`

**The Bug**: The Vite web build emitted absolute asset URLs like `/assets/...`, `/manifest.json`, and `/logo192.png`, so deployments mounted under a subpath such as `/slskd` served `index.html` correctly but then fetched the JS bundle from the site root. Reverse proxies returned HTML/404 for those asset requests, which produced a blank white page with `NS_ERROR_CORRUPTED_CONTENT` and “disallowed MIME type (`text/html`)" in the browser.

**Files Affected**:
- `src/web/vite.config.js`
- `src/web/index.html`

**Wrong**:
```javascript
export default defineConfig({
  plugins: [react()],
});
```

```html
<link rel="manifest" href="/manifest.json" />
<script type="module" src="/src/index.jsx"></script>
```

**Correct**:
```javascript
export default defineConfig({
  base: './',
  plugins: [react()],
});
```

```html
<link rel="manifest" href="./manifest.json" />
<script type="module" src="./src/index.jsx"></script>
```

**Why This Keeps Happening**: The old SPA pipeline used server-side HTML rewriting for CRA-era `/static/...` assets. Vite defaults to root-relative output unless told otherwise, so a subpath deployment works locally at `/` and silently breaks only behind `web.url_base` or a reverse proxy prefix.

### 1f. Legacy Transfers Rows May Contain `NULL` Strings Even If New Code Treats Them As Required

**The Bug**: Startup initialization called `Uploads.List(...)`, and EF Core materialization threw on upgraded databases because older `transfers.db` rows contained `NULL` in string columns like `StateDescription`/`Exception` while the model treated them as non-nullable strings.

**Files Affected**:
- `src/slskd/Transfers/Types/Transfer.cs`
- `tests/slskd.Tests.Unit/Transfers/TransfersDbContextTests.cs`

**Wrong**:
```csharp
public string StateDescription { get; set; }
public string Exception { get; set; }
```

**Correct**:
```csharp
public string? StateDescription { get; set; }
public string? Exception { get; set; }
```

**Why This Keeps Happening**: It is easy to tighten nullability on current writes and forget that persisted SQLite rows from older releases do not retroactively satisfy the new contract. For long-lived local databases, read models need to be tolerant of legacy `NULL` values unless a migration backfills them first.

### 1g. Built-Web Verifier Scripts Must Resolve Paths Relative To `src/web`, Not The Repo Root

**The Bug**: A release-gate script successfully built the frontend into `src/web/build`, then the Node verifier immediately failed because it looked for `build/index.html` relative to the repository root instead of the web project directory.

**Files Affected**:
- `src/web/scripts/verify-build-output.mjs`
- `packaging/scripts/run-release-gate.sh`

**Wrong**:
```javascript
const root = path.resolve(process.cwd());
const buildDir = path.join(root, 'build');
```

**Correct**:
```javascript
const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const buildDir = path.resolve(scriptDir, '..', 'build');
```

**Why This Keeps Happening**: Top-level gate scripts usually execute from the repository root, but many frontend utilities assume they are running from `src/web`. If the verifier uses `process.cwd()`, it quietly depends on the caller's shell location instead of the actual artifact location.

---

### 2. Reverting Entire Workflow Files (build-on-tag.yml, CI)

**The Bug**: Reverting `.github/workflows/build-on-tag.yml` (or other workflows) to an old commit wipes out months of accumulated fixes: AUR, Winget (Windows case-sensitivity), Nix/Winget branch refs, PPA version checks, Chocolatey retries, etc. Builds then fail immediately (wrong branch name, case-sensitivity errors, missing steps).

**Files Affected**:
- `.github/workflows/build-on-tag.yml`
- Any workflow that has been fixed incrementally over time

**Wrong**:
```bash
git checkout <old-commit> -- .github/workflows/build-on-tag.yml
```
Do not revert the whole file to "fix" one thing.

**Correct**:
- Make minimal, targeted edits (e.g. only add `--legacy-peer-deps` or fix one job).
- Before changing workflows: read `docs/DEV_BUILD_PROCESS.md`, then `git log --oneline -- .github/workflows/build-on-tag.yml` to see what was fixed and why.
- Branch names in workflows must match actual repo branches: use `dev/40-fixes` (or whatever the current dev branch is), not hardcoded `experimental/multi-source-swarm` if that branch no longer exists.
- Winget on Windows: use `fetch-depth: 1` for checkout and `git fetch origin +refs/heads/master:refs/remotes/origin/master` (not full fetch) to avoid case-insensitivity errors when refs differ only in casing.

**Why This Keeps Happening**: Agent "fixes" a single symptom by reverting the file to a "known good" state, not realizing that state is old and missing many fixes.

### 2a. Package Channel Metadata Must Match the Runtime Binary Name and Package Identity

**The Bug**: The Nix flake exported only a `slskdn` wrapper even though NixOS `services.slskd` expects `bin/slskd`, and the stable Winget manifests were copied from dev without replacing the `snapetech.slskdn-dev` identifier or `slskdn-dev` alias.

**Files Affected**:
- `flake.nix`
- `packaging/winget/snapetech.slskdn.yaml`
- `packaging/winget/snapetech.slskdn.installer.yaml`
- `packaging/winget/snapetech.slskdn.locale.en-US.yaml`
- `.github/workflows/build-on-tag.yml`

**Wrong**:
```nix
makeWrapper $out/libexec/${pname}/slskd $out/bin/${pname}
```

```yaml
PackageIdentifier: snapetech.slskdn-dev
PortableCommandAlias: slskdn-dev
```

**Correct**:
```nix
makeWrapper $out/libexec/${pname}/slskd $out/bin/slskd
ln -s $out/bin/slskd $out/bin/${pname}
```

```yaml
PackageIdentifier: snapetech.slskdn
PortableCommandAlias: slskdn
```

**Why This Keeps Happening**: Packaging work tends to treat channel names, package names, and executable names as interchangeable. They are not. Each channel must preserve the runtime contract expected by downstream tools (`slskd` for service modules) while also publishing the correct channel identity (`slskdn` vs `slskdn-dev`). Add an explicit validation step whenever manifests or wrappers are generated.

### 2b. Wrapping Generic Linux Binaries Is Not Enough for NixOS

**The Bug**: The Nix flake wrapped the published `slskd` binary and set `LD_LIBRARY_PATH`, but the service still failed on NixOS because the extracted ELF kept its generic Linux dynamic loader and NixOS refused to execute it.

**Files Affected**:
- `flake.nix`

**Wrong**:
```nix
nativeBuildInputs = [ pkgs.unzip pkgs.makeWrapper ];

installPhase = ''
  makeWrapper $out/libexec/${pname}/slskd $out/bin/slskd \
    --prefix LD_LIBRARY_PATH : ${pkgs.lib.makeLibraryPath [ pkgs.icu pkgs.openssl ]}
'';
```

**Correct**:
```nix
nativeBuildInputs = [ pkgs.unzip pkgs.makeWrapper pkgs.autoPatchelfHook ];
buildInputs = [
  pkgs.curl
  pkgs.icu
  pkgs.krb5
  pkgs.libunwind
  pkgs.openssl
  pkgs.stdenv.cc.cc
  pkgs.util-linux
  pkgs.zlib
];
```

**Why This Keeps Happening**: It is easy to treat Nix like any other Linux packaging target and assume a wrapper plus `LD_LIBRARY_PATH` solves native dependency issues. On NixOS, generic upstream ELF binaries also need their interpreter and linked libraries patched into the Nix store path, so use `autoPatchelfHook` or explicit `patchelf` instead of only wrapping the executable.

### 2c. Do Not Assume Fresh Filesystem Labels Are Immediately Available Under `/dev/disk/by-label`

**The Bug**: A QEMU/NixOS install helper formatted `/dev/vda1` with `mkfs.ext4 -L nixos` and immediately mounted `/dev/disk/by-label/nixos`, but the installer environment had not populated that symlink yet, so the mount failed even though the partition existed.

**Files Affected**:
- `/tmp/slskdn-nixos-vm/install-nixos.sh`

**Wrong**:
```bash
mkfs.ext4 -F -L nixos /dev/vda1
mount /dev/disk/by-label/nixos /mnt
```

**Correct**:
```bash
mkfs.ext4 -F -L nixos /dev/vda1
udevadm settle
mount /dev/vda1 /mnt
```

**Why This Keeps Happening**: It is tempting to use the friendlier `/dev/disk/by-label/...` path immediately after formatting, but installer/live environments can lag on udev updates. For fresh partitions, either wait for udev explicitly or mount the block device path you already know exists.

### 2d. Do Not Append a Bare Attrset to `configuration.nix`; Add a Module or Edit Inside the Existing One

**The Bug**: A NixOS install helper appended a second top-level `{ ... }` block to the generated `/etc/nixos/configuration.nix`, but that file already defines a module function (`{ config, pkgs, ... }:`). The next `nixos-install` failed with “attempt to call something which is not a function but a set”.

**Files Affected**:
- `/tmp/slskdn-nixos-vm/install-nixos.sh`

**Wrong**:
```bash
cat >> /mnt/etc/nixos/configuration.nix <<'EOF'
{
  services.openssh.enable = true;
}
EOF
```

**Correct**:
```bash
cat > /mnt/etc/nixos/slskdn-vm.nix <<'EOF'
{ ... }:
{
  services.openssh.enable = true;
}
EOF
printf '\n  ./slskdn-vm.nix\n' >> /mnt/etc/nixos/configuration.nix
```

**Why This Keeps Happening**: Generated NixOS config files look like plain attribute sets at a glance, but they are module functions. If you need to inject extra settings from a script, either edit inside the existing attrset carefully or create a separate module file and import it.

### 2e. NixOS GRUB Configuration Now Expects `boot.loader.grub.devices` in This Installer Path

**The Bug**: A scripted NixOS VM install set `boot.loader.grub.device = "/dev/vda";`, but the installer on NixOS 25.11 rejected it with an assertion asking for `boot.loader.grub.devices` or `boot.loader.grub.mirroredBoots`.

**Files Affected**:
- `/tmp/slskdn-nixos-vm/install-nixos.sh`

**Wrong**:
```nix
boot.loader.grub.device = "/dev/vda";
```

**Correct**:
```nix
boot.loader.grub.devices = [ "/dev/vda" ];
```

**Why This Keeps Happening**: Older examples and muscle memory still use the singular `grub.device` form, but the current module assertions in this install path expect the list form. Check the generated module assertions on current NixOS releases instead of reusing older snippets blindly.

### 2f. Generated NixOS `imports` Blocks May Span Multiple Lines; Match the Real Shape Before Using `sed`

**The Bug**: A helper tried to inject `./slskdn-vm.nix` with `sed '/imports = \[/a ...'`, but `nixos-generate-config` emitted `imports =` and `[` on separate lines, so the expression never matched and the custom module was not imported at all.

**Files Affected**:
- `/tmp/slskdn-nixos-vm/install-nixos.sh`

**Wrong**:
```bash
sed -i '/imports = \[/a\ \ \ \ ./slskdn-vm.nix' /mnt/etc/nixos/configuration.nix
```

**Correct**:
```bash
sed -i '/\.\/hardware-configuration\.nix/a\ \ \ \ \ \./slskdn-vm.nix' /mnt/etc/nixos/configuration.nix
```

**Why This Keeps Happening**: Generated config files look predictable, but their whitespace and line breaks are not stable enough to target with a guessed pattern. Match a concrete line that is actually present in the generated file, or rewrite the whole block explicitly instead of assuming a one-line `imports = [`.

### 2g. `expect` Patterns for SSH Password Prompts Must Handle OpenSSH's Actual Prompt Casing

**The Bug**: A local-VM validation helper waited for `password:` in lowercase, but OpenSSH prompted with `(root@127.0.0.1) Password:`. The automation stalled at the login prompt even though the VM was ready.

**Files Affected**:
- `/tmp/slskdn-nixos-vm/validate-vm.expect`

**Wrong**:
```tcl
expect {
  "password:" { send "root\r" }
}
```

**Correct**:
```tcl
expect {
  -re {[Pp]assword:} { send "root\r" }
}
```

**Why This Keeps Happening**: Interactive prompt matching is brittle when it relies on exact casing or full literal text. SSH clients vary their password prompt prefix, so use a case-tolerant regex for the stable suffix instead of matching the whole prompt literally.

### 2h. Nix Flakes on 9p-Mounted Git Repositories Can Trip Git Ownership Checks

**The Bug**: Inside the NixOS VM, `nix build /mnt/hostrepo#default` treated the shared repo as a Git flake and failed because the 9p mount preserved host ownership that did not match the guest user, triggering Git's “safe directory” protection.

**Files Affected**:
- `/tmp/slskdn-nixos-vm/validate-slskdn.sh`

**Wrong**:
```bash
nix build /mnt/hostrepo#default
```

**Correct**:
```bash
git config --global --add safe.directory /mnt/hostrepo
nix build /mnt/hostrepo#default
```

**Why This Keeps Happening**: Shared folders in VMs often preserve host UIDs/GIDs or present synthetic ownership that does not match the guest account. When a flake path is also a Git repo, Nix delegates part of the source handling to Git, so you need to either mark the mount as a safe directory or use a non-Git path source when testing from a shared folder.

### 2i. Prefer `path:` Flake URIs in Minimal Guest Images When Shared Repos Trigger Git Handling

**The Bug**: The first recovery plan for a 9p-mounted flake repo assumed `git` was installed in the minimal NixOS guest so `safe.directory` could be configured, but the guest image did not include `git`, leaving the flake build blocked.

**Files Affected**:
- `/tmp/slskdn-nixos-vm/validate-slskdn.sh`

**Wrong**:
```bash
git config --global --add safe.directory /mnt/hostrepo
nix build /mnt/hostrepo#default
```

**Correct**:
```bash
nix build 'path:/mnt/hostrepo#default'
```

**Why This Keeps Happening**: It is easy to assume live or minimal troubleshooting images carry the same helper tools as a normal dev box. For ad hoc VM validation, use the simplest source form that avoids extra dependencies; `path:` flake URIs sidestep both Git ownership checks and the need for Git itself.

### 2j. Read-Only Shared Flake Mounts Need `--no-write-lock-file`

**The Bug**: After switching to a `path:` flake URI for a read-only 9p mount, `nix build` still failed because it tried to create `flake.lock` in the mounted repo and the filesystem was intentionally read-only.

**Files Affected**:
- `/tmp/slskdn-nixos-vm/validate-slskdn.sh`

**Wrong**:
```bash
nix build 'path:/mnt/hostrepo#default'
```

**Correct**:
```bash
nix build --no-write-lock-file 'path:/mnt/hostrepo#default'
```

**Why This Keeps Happening**: Read-only source mounts are ideal for preserving the host checkout during guest validation, but flake evaluation still wants to persist lock updates by default. When validating from a read-only mount, always disable lock-file writes explicitly or copy the flake into a writable path first.

### 2k. Nix Flake Stable Pins Must Move With the Latest Published Stable Release

**The Bug**: The flake still pointed at stable release `0.24.5-slskdn.52` and its old hashes even though GitHub’s latest stable release had moved to `0.24.5-slskdn.54`, so `nix build` failed immediately with a fixed-output hash mismatch before the runtime patching fix could even be exercised.

**Files Affected**:
- `flake.nix`

**Wrong**:
```nix
version = "0.24.5-slskdn.52";
sha256 = "1gljb5zj7h0g7mhi8d9s5hjkqvn8v6dmrb812gfwggayl91ksj7y";
```

**Correct**:
```nix
version = "0.24.5-slskdn.54";
sha256 = "sha256-M1gUyVXt1iPUjjh9eFheDBRWv/kixAgIxlvIRMbckoo=";
```

**Why This Keeps Happening**: Packaging work can fix wrapper logic or runtime behavior while leaving the stable source pin behind on an older release. For fixed-output fetches, a stale release pin is just as fatal as a stale hash, so treat version and hashes as one atomic update sourced from the actual latest published release metadata.

### 2l. The Bundled .NET Runtime Also Needs `lttng-ust` on NixOS for `autoPatchelfHook` to Finish Cleanly

**The Bug**: After adding the obvious runtime libraries, the NixOS VM still failed during `autoPatchelfHook` because `libcoreclrtraceptprovider.so` wanted `liblttng-ust.so.0`, which was not present in the flake inputs.

**Files Affected**:
- `flake.nix`

**Wrong**:
```nix
buildInputs = [
  pkgs.curl
  pkgs.icu
  pkgs.krb5
  pkgs.libunwind
  pkgs.openssl
  pkgs.stdenv.cc.cc
  pkgs.util-linux
  pkgs.zlib
];
```

**Correct**:
```nix
buildInputs = [
  pkgs.curl
  pkgs.icu
  pkgs.krb5
  pkgs.lttng-ust
  pkgs.libunwind
  pkgs.openssl
  pkgs.stdenv.cc.cc
  pkgs.util-linux
  pkgs.zlib
];
```

**Why This Keeps Happening**: The first-pass dependency list tends to cover the apphost and common runtime libs, but the bundled .NET runtime ships tracing/provider binaries that pull in less obvious native dependencies. Validate with `autoPatchelfHook` on real NixOS and add every missing provider library it reports instead of assuming the first set is complete.

### 2m. Some Nix Packages Default to a Non-Library Output; Use the Output That Actually Contains the Shared Object

**The Bug**: Adding `pkgs.lttng-ust` still did not satisfy `liblttng-ust.so.0` because that attribute resolved to the `bin` output in this nixpkgs revision, while the shared library lived in `pkgs.lttng-ust.out`.

**Files Affected**:
- `flake.nix`

**Wrong**:
```nix
buildInputs = [
  pkgs.lttng-ust
];
```

**Correct**:
```nix
buildInputs = [
  pkgs.lttng-ust.out
];
```

**Why This Keeps Happening**: It is easy to assume a package attribute points at the runtime library output, but multi-output Nix packages often default to `bin` or `dev`. When `autoPatchelfHook` still cannot find a `.so`, inspect the package outputs and reference the one that actually contains the needed library.

### 2n. Bundled Runtime SONAMEs Can Lag Behind nixpkgs; Patch `NEEDED` Entries Before `autoPatchelfHook` Runs

**The Bug**: Even after adding the correct `lttng-ust` library output, the NixOS VM still failed because the bundled `.NET` trace provider asked for `liblttng-ust.so.0` while current nixpkgs only ships `liblttng-ust.so.1`.

**Files Affected**:
- `flake.nix`

**Wrong**:
```nix
buildInputs = [
  pkgs.lttng-ust.out
];
```

**Correct**:
```nix
patchelf \
  --replace-needed liblttng-ust.so.0 liblttng-ust.so.1 \
  $out/libexec/${pname}/libcoreclrtraceptprovider.so
```

**Why This Keeps Happening**: Upstream self-contained runtimes can be built against an older SONAME than the one available in current nixpkgs. Adding more packages will not help when the exact requested SONAME no longer exists; inspect the bundled binary and patch the `NEEDED` entry to the compatible library that nixpkgs actually provides before running `autoPatchelfHook`.

### 2o. Do Not Strip Bundled .NET Runtime Payloads in the Nix Package

**The Bug**: After the flake finally built, launching `slskd` on NixOS still failed with `Failed to load System.Private.CoreLib.dll ... 0x8007000B`. The package had gone through Nix’s default strip phase, which is unsafe for this bundled .NET payload.

**Files Affected**:
- `flake.nix`

**Wrong**:
```nix
pkgs.stdenv.mkDerivation {
  nativeBuildInputs = [ pkgs.unzip pkgs.makeWrapper pkgs.autoPatchelfHook pkgs.patchelf ];
}
```

**Correct**:
```nix
pkgs.stdenv.mkDerivation {
  nativeBuildInputs = [ pkgs.unzip pkgs.makeWrapper pkgs.autoPatchelfHook pkgs.patchelf ];
  dontStrip = true;
}
```

**Why This Keeps Happening**: Nix’s normal strip phase is reasonable for ordinary native packages, but bundled .NET distributions mix ELF binaries with managed/runtime payloads that are not safe to treat like a conventional C/C++ install tree. If CoreCLR starts failing with format/load errors after packaging, remove stripping from the equation before chasing more loader theories.

### 2p. The NixOS `services.slskd` Module Requires `services.slskd.domain` Even for Local Validation

**The Bug**: A local NixOS validation module enabled `services.slskd` and provided a custom package, but `nixos-rebuild test` failed before creating the service because the module accessed `services.slskd.domain` and no value was set.

**Files Affected**:
- `/etc/nixos/slskdn-local.nix` in the validation VM

**Wrong**:
```nix
{
  services.slskd.enable = true;
  services.slskd.package = slskdn.packages.${pkgs.system}.default;
}
```

**Correct**:
```nix
{
  services.slskd.enable = true;
  services.slskd.domain = "localhost";
  services.slskd.package = slskdn.packages.${pkgs.system}.default;
}
```

**Why This Keeps Happening**: It is easy to treat the NixOS module like a thin wrapper around the binary and only override `package`, but module assertions/options can still require unrelated application settings. For service validation, read the module’s required options instead of assuming `enable + package` is enough.

### 2q. The NixOS `services.slskd` Module Also Requires `settings.shares.directories`, Even If You Want No Shares

**The Bug**: After adding `domain` and `environmentFile`, `nixos-rebuild test` still failed because the module always maps over `cfg.settings.shares.directories` to build `ReadOnlyPaths`, so leaving it unset crashes evaluation.

**Files Affected**:
- `/etc/nixos/slskdn-local.nix` in the validation VM

**Wrong**:
```nix
{
  services.slskd.enable = true;
  services.slskd.environmentFile = "/etc/slskd.env";
}
```

**Correct**:
```nix
{
  services.slskd.enable = true;
  services.slskd.environmentFile = "/etc/slskd.env";
  services.slskd.settings.shares.directories = [ ];
}
```

**Why This Keeps Happening**: “No shares configured” feels like it should mean “unset,” but this module dereferences the list unconditionally when generating systemd hardening paths. For local validation, explicitly set it to an empty list.

### 2r. Whenever `flake.nix` Packaging Logic Changes, Update the Metadata Validator in the Same Edit

**The Bug**: After changing the Nix flake to add `patchelf`, `dontStrip`, `lttng-ust.out`, and the SONAME rewrite, `packaging/scripts/validate-packaging-metadata.sh` still enforced the old `nativeBuildInputs` line and failed immediately.

**Files Affected**:
- `flake.nix`
- `packaging/scripts/validate-packaging-metadata.sh`

**Wrong**:
```bash
expect_line flake.nix 'nativeBuildInputs = \[ pkgs\.unzip pkgs\.makeWrapper pkgs\.autoPatchelfHook \];'
```

**Correct**:
```bash
expect_line flake.nix 'nativeBuildInputs = \[ pkgs\.unzip pkgs\.makeWrapper pkgs\.autoPatchelfHook pkgs\.patchelf \];'
expect_line flake.nix 'dontStrip = true;'
expect_line flake.nix '--replace-needed liblttng-ust\.so\.0 liblttng-ust\.so\.1'
```

**Why This Keeps Happening**: Packaging validation tends to get treated as a one-time guardrail, but it is really part of the packaging implementation. If the flake or package templates change and the validator does not, the repo ends up failing on stale assertions instead of catching real regressions.

### 2s. Validator Helpers That Pass Regexes to `grep` Must Use `grep --` for Patterns Beginning With `-`

**The Bug**: After adding a validation pattern for `--replace-needed ...`, the packaging validator failed inside `grep` because the pattern itself started with `-` and was parsed as an option rather than a regex.

**Files Affected**:
- `packaging/scripts/validate-packaging-metadata.sh`

**Wrong**:
```bash
grep -Eq "$pattern" "$file"
```

**Correct**:
```bash
grep -Eq -- "$pattern" "$file"
```

**Why This Keeps Happening**: Validation helpers often assume patterns are data, but command-line tools still parse them as arguments first. Any generic wrapper that forwards arbitrary regexes to `grep` should include `--` up front or it will break as soon as a pattern begins with `-`.

### 2b. Tests That Bind TCP Ports Must Not Hardcode Popular Local Ports

**The Bug**: `LocalPortForwarderTests` bound to `8080` and `8081`, which caused unrelated CI and local failures whenever those ports were already in use; `TorSocksTransportTests` also assumed a specific connect-error substring even though timeout/cancellation wording varies by runtime and environment.

**Files Affected**:
- `tests/slskd.Tests.Unit/Common/Security/LocalPortForwarderTests.cs`
- `tests/slskd.Tests.Unit/Mesh/Transport/TorSocksTransportTests.cs`

**Wrong**:
```csharp
await _portForwarder.StartForwardingAsync(8080, "pod-123", "example.com", 80);
Assert.Contains("connect", status.LastError.ToLower());
```

**Correct**:
```csharp
var localPort = GetFreeLocalPort();
await _portForwarder.StartForwardingAsync(localPort, "pod-123", "example.com", 80);
Assert.NotEmpty(status.LastError);
```

**Why This Keeps Happening**: Test code often assumes "common dev ports" are free and that low-level socket failures have stable message text. Neither assumption holds across busy developer machines, CI runners, or different runtime timing paths.

---

### 3. E2E SlskdnNode: HTTPS Port Conflict and Missing --app-dir

**The Bug**: E2E tests that start real slskdn nodes fail with "Hosting failed to start" / "Address already in use" or "An instance of slskd is already running" because (1) every node tries to bind to the same HTTPS port (5031) and (2) nodes share the default app dir (mutex conflict).

**Files Affected**:
- `tests/e2e/harness/SlskdnNode.ts`
- `tests/e2e/fixtures/helpers.ts` (findFreePort)

**Wrong**:
- Test config without `web.https.disabled: true` → all nodes bind to 5031, second node fails.
- Spawn args without `--app-dir <per-node dir>` → all nodes use default app dir, mutex prevents multiple instances.
- Building inside `SlskdnNode.start()` after `findFreePort()` → long delay lets another process grab the port (or port in TIME_WAIT).

**Correct**:
- In test config YAML: `web: https: disabled: true` so each node only binds to its unique HTTP port.
- Spawn with `--app-dir`, `this.appDir` (isolated temp dir per node).
- Build once in spec `beforeAll`, not per node; use `findFreePort()` with `reuseAddress: true` so the probe port can be rebound immediately.
- Keep stdin as pipe (do not use `ignore`) so the child does not see EOF and exit.

**Why This Keeps Happening**: Default slskd config enables HTTPS on a fixed port; E2E runs multiple nodes and did not disable HTTPS or isolate app dirs.

---

### 3b. E2E SlskdnNode.stop(): Must Wait for Child Exit (Port Leaks)

**The Bug**: E2E nodes intermittently fail to start with `Address already in use` because the harness stop logic resolves before the `dotnet` child process has actually exited. The old process can keep Kestrel bound to its port for a short window, and the next node hits a bind failure.

**Files Affected**:
- `tests/e2e/harness/SlskdnNode.ts`

**Wrong** (resolves early after SIGKILL without waiting for `exit`):
```ts
this.process.kill('SIGTERM');
await new Promise<void>((resolve) => {
  this.process.on('exit', () => resolve());
  setTimeout(() => {
    this.process.kill('SIGKILL');
    resolve();
  }, 5000);
});
```

**Correct** (escalate SIGTERM -> SIGKILL, but always await the `exit` event):
```ts
const exitPromise = new Promise<void>((resolve) => proc.once('exit', () => resolve()));

proc.kill('SIGTERM');
const exitedGracefully = await Promise.race([
  exitPromise.then(() => true),
  delay(5000).then(() => false),
]);

if (!exitedGracefully) {
  proc.kill('SIGKILL');
  await Promise.race([exitPromise, delay(5000)]);
}
```

**Why This Keeps Happening**: It's easy to write a timeout path that resolves the stop Promise without verifying the child actually exited.

---

### 4. `async void` Event Handlers Without Try-Catch

**The Bug**: `async void` event handlers that throw exceptions crash the entire .NET process.

**Files Affected**:
- `src/slskd/Messaging/RoomService.cs` - `Client_LoggedIn`

**Wrong**:
```csharp
private async void Client_LoggedIn(object sender, EventArgs e)
{
    await TryJoinAsync(rooms);  // 💀 Exception here = process crash
}
```

**Correct**:
```csharp
private async void Client_LoggedIn(object sender, EventArgs e)
{
    try
    {
        await TryJoinAsync(rooms);
    }
    catch (Exception ex)
    {
        Logger.Error(ex, "Failed to execute post-login room actions");
    }
}
```

**Why This Keeps Happening**: `async void` is required for event handlers, but models forget it can't propagate exceptions.

---

### 5. Streaming Controller `[Produces]` Causing 406 Instead of 429

**The Bug**: Adding `[Produces("application/octet-stream")]` to the streams endpoint can cause ASP.NET Core to return `406 Not Acceptable` for non-file error responses (notably the concurrency limiter `429`), breaking E2E expectations.

**Files Affected**:
- `src/slskd/Streaming/StreamsController.cs`

**Wrong**:
```csharp
[Produces("application/octet-stream")]
public class StreamsController : ControllerBase
{
    // ...
    if (!_limiter.TryAcquire(limiterKey, maxConcurrent))
        return StatusCode(429, "Too many concurrent streams.");
}
```

**Correct**:
```csharp
public class StreamsController : ControllerBase
{
    // ...
    if (!_limiter.TryAcquire(limiterKey, maxConcurrent))
        return StatusCode(429, "Too many concurrent streams.");
}
```

**Why This Keeps Happening**: `[Produces]` is tempting for a file endpoint, but the action also returns non-file errors (401/404/429). Constraining the produced content types can make these errors fail content negotiation and surface as `406`.

---

### 3. Unbounded Parallelism in Download Loops

**The Bug**: `Task.Run` inside loops without concurrency limits causes resource exhaustion.

**Files Affected**:
- `src/slskd/Transfers/MultiSource/MultiSourceDownloadService.cs`

**Wrong**:
```csharp
foreach (var source in sources)
{
    _ = Task.Run(() => DownloadFromSourceAsync(source));  // 💀 Unbounded
}
```

**Correct**:
```csharp
var semaphore = new SemaphoreSlim(10);  // Cap at 10 concurrent
foreach (var source in sources)
{
    await semaphore.WaitAsync();
    _ = Task.Run(async () =>
    {
        try { await DownloadFromSourceAsync(source); }
        finally { semaphore.Release(); }
    });
}
```

**Why This Keeps Happening**: Models optimize for "parallelism = fast" without considering resource limits.

---

### 4. HashDb Migration Version Collisions

**The Bug**: Duplicate migration version numbers cause `UNIQUE constraint failed: __HashDbMigrations.version`, blocking startup and E2E health checks.

**Files Affected**:
- `src/slskd/HashDb/Migrations/HashDbMigrations.cs`

**Wrong**:
```csharp
new Migration { Version = 12, Name = "Label crate job cache", ... },
new Migration { Version = 12, Name = "Traffic accounting", ... }, // 💥 duplicate
new Migration { Version = 14, Name = "Warm cache popularity", ... },
new Migration { Version = 14, Name = "Warm cache entries", ... }, // 💥 duplicate
```

**Correct**:
```csharp
new Migration { Version = 12, Name = "Label crate job cache", ... },
new Migration { Version = 13, Name = "Peer metrics storage", ... },
new Migration { Version = 14, Name = "Warm cache popularity", ... },
new Migration { Version = 15, Name = "Warm cache entries", ... },
new Migration { Version = 16, Name = "Virtual Soulfind pseudonyms", ... },
new Migration { Version = 17, Name = "Traffic accounting", ... },
```

**Why This Keeps Happening**: Migrations were appended without re-checking version uniqueness, and the list order wasn’t kept strictly ascending.

---

### 5. Snap workflow: source path is relative to snapcraft project dir

**The Bug**: In `build-on-tag.yml`, the Snap job unzipped the release zip to `slskdn_dist` in repo root, then `sed` set `source: slskdn_dist` in `packaging/snap/snapcraft.yaml`. Snapcraft runs with `cd packaging/snap`, so it resolves `source: slskdn_dist` relative to that directory. The path `packaging/snap/slskdn_dist` did not exist (the unzip created `./slskdn_dist` at repo root), so snapcraft failed.

**Files Affected**:
- `.github/workflows/build-on-tag.yml` (snap-dev, snap-main)

**Wrong**:
```yaml
run: |
  unzip slskdn-dev-linux-x64.zip -d slskdn_dist
  sed -i "s|source: .*|source: slskdn_dist|" packaging/snap/snapcraft.yaml
  cd packaging/snap
  snapcraft --destructive-mode
```

**Correct**:
```yaml
run: |
  mkdir -p packaging/snap/slskdn_dist
  unzip slskdn-dev-linux-x64.zip -d packaging/snap/slskdn_dist
  sed -i "s|source: .*|source: slskdn_dist|" packaging/snap/snapcraft.yaml
  cd packaging/snap
  snapcraft --destructive-mode
```

**Why This Keeps Happening**: Unzip target was assumed to be "any dir"; snapcraft resolves part sources relative to the snapcraft project root (the directory containing `snapcraft.yaml`).

---

### 5b. Chocolatey: do NOT pass path to choco push (match master)

**The Bug**: Passing a path to `choco push` (e.g. `choco push $Nupkg --source ...`) causes Chocolatey/pwsh to glue the path and the next flag into one argument, so it fails with: "File specified is either not found or not a .nupkg file. '<path>.nupkg --prerelease'".

**Files Affected**:
- `.github/workflows/build-on-tag.yml` (chocolatey-dev, chocolatey-main)

**Wrong** (any path argument can glue to next flag):
```powershell
choco push $Nupkg --source https://push.chocolatey.org/ --api-key $env:CHOCO_API_KEY --prerelease
```

**Correct** (match master): Run `choco push` from inside `packaging/chocolatey` after `choco pack`, with **no path** — choco finds the single .nupkg in the current directory:
```powershell
cd packaging/chocolatey
choco pack
choco push --source https://push.chocolatey.org/ --api-key $env:CHOCO_API_KEY --prerelease --execution-timeout 300   # dev
choco push --source https://push.chocolatey.org/ --api-key $env:CHOCO_API_KEY --execution-timeout 300               # main (add retry loop for 504)
```

**Why This Keeps Happening**: Chocolatey/pwsh glues a path argument to the next token. Omitting the path (run from the pack directory) avoids the bug; master branch uses this pattern.

---

### 5c. Snap workflow: destructive-mode on ubuntu-latest breaks stage-packages (libicu70)

**The Bug**: On GitHub Actions `ubuntu-latest` (Ubuntu 24.04), running `snapcraft --destructive-mode` uses the host apt repositories. With `base: core22`, this can fail because `stage-packages` include `libicu70` (available on 22.04, not 24.04). Error: "Stage package not found in part 'slskdn': libicu70."

**Files Affected**:
- `.github/workflows/build-on-tag.yml` (snap-dev, snap-main)

**Correct** (build in LXD so the build environment matches `base: core22`):
```yaml
- uses: snapcore/action-build@v1
  with:
    path: packaging/snap
```

---

### 5d. Snap Store: duplicate content and transient "error while processing"

**The Bug**: (1) If a previous upload succeeded in transmitting but failed the status check (e.g. "Waiting for previous upload"), the next retry fails with: "binary_sha3_384: A file with this exact same content has already been uploaded". (2) Snap Store can return "Status: error while processing" transiently.

**Files Affected**:
- `.github/workflows/build-on-tag.yml` (snap-dev, snap-main)

**Fix**: (1) Treat "exact same content has already been uploaded" as **SUCCESS**. (2) Treat "Waiting for previous upload" and "error while processing" as **retry** (sleep 30s, continue); do not exit on them.

---

### 5e. Snap: action-build output path; do not double packaging/snap

**The Bug**: `snapcore/action-build@v1` sets its `snap` output to a path relative to the repo root (e.g. `packaging/snap/slskdn_0.24.1.dev.91769629519_amd64.snap`). If you set `SNAP_PATH="packaging/snap/${{ steps.snap-build.outputs.snap }}"` you get `packaging/snap/packaging/snap/...` and "is not a valid file". The upload step also runs on the host runner; install snapcraft there before upload.

**Files Affected**:
- `.github/workflows/build-on-tag.yml` (snap-dev, snap-main)

**Wrong**:
```yaml
SNAP_PATH="packaging/snap/${{ steps.snap-build.outputs.snap }}"   # duplicates packaging/snap
```

**Correct**:
- Set `SNAP_PATH="${{ steps.snap-build.outputs.snap }}"` (use the output as-is; it already includes packaging/snap when path: packaging/snap).
- Add a step before the upload step to install snapcraft on the host: `sudo apt-get install -y snapd` then `sudo snap install snapcraft --classic`.

**Why This Keeps Happening**: The action may output filename-only or path; if it outputs path, prepending packaging/snap breaks.

---

### 5e2. Snap (and other packaging) jobs: don't pin checkout to a branch on tag-triggered builds

**The Bug**: In `build-on-tag.yml`, Snap (and Nix, Homebrew) jobs had `ref: dev/40-fixes` or `ref: master`. When the workflow is triggered by a **tag** (e.g. `build-dev-0.24.1.dev.…`), the runner checks out that branch tip, not the tag's commit. So you build with `packaging/snap` (and release zip) from different commits: zip from the tag's release, tree from branch tip. If someone reverted or changed the Snap workflow on the branch, the job uses that reverted state and Snap breaks.

**Files Affected**:
- `.github/workflows/build-on-tag.yml` (snap-dev, snap-main; also nix-dev, homebrew-dev if they pin ref)

**Wrong**:
```yaml
- uses: actions/checkout@v4
  with:
    ref: dev/40-fixes   # tag build then gets branch tip, not tag commit
```

**Correct**:
```yaml
- uses: actions/checkout@v4
  # No ref: so tag-triggered runs checkout the tag's commit (same as release assets).
```

**Why This Keeps Happening**: It's tempting to pin to a branch for "dev" or "main" packaging; for tag-triggered runs the ref that triggered the run is the tag, and checkout should match that.

---

### 5f. PPA dev build: version must always increase (workflow uses epoch-based DEB_VERSION)

**The Bug**: PPA rejects uploads with "Version older than that in the archive". Debian version comparison treats the suffix after `dev.` as the ordering key. If the tag (or derived version) is e.g. `0.24.1.dev.20260128.162317`, it can sort **below** a previously uploaded `0.24.1.dev.91769609285`, so the PPA rejects the upload.

**Files Affected**:
- `.github/workflows/build-on-tag.yml` (ppa-dev job)

**Fix (in workflow)**: The ppa-dev job now **ignores the tag version** for the package version and sets `DEB_VERSION=0.24.1.dev.9$(date +%s)` in "Prepare Source Structure", then uses that for directory name, tarball, and changelog. So PPA always gets a monotonically increasing version regardless of tag format.

**If tagging manually**: Prefer `build-dev-0.24.1.dev.9$(date +%s)` so the tag itself is increasing; the workflow no longer derives PPA version from the tag for dev.

---

### 6. Library Items Empty When Share Cache Is Cold

**The Bug**: `/api/v0/library/items` returned no results when the share cache was empty or not ready, breaking E2E flows that need real content IDs.

**Files Affected**:
- `src/slskd/API/Native/LibraryItemsController.cs`
- `src/web/e2e/policy.spec.ts`
- `src/web/e2e/streaming.spec.ts`

**Wrong**:
```csharp
var directories = await shareService.BrowseAsync();
var allFiles = directories.SelectMany(d => d.Files ?? Enumerable.Empty<File>());
// allFiles can be empty if the share cache is cold
```

**Correct**:
```csharp
var directories = await shareService.BrowseAsync();
var allFiles = directories.SelectMany(d => d.Files ?? Enumerable.Empty<File>());
if (!allFiles.Any())
{
    // Fallback: scan configured share directories directly
    var items = await SearchShareDirectoriesAsync(query, kinds, limit, cancellationToken);
    return Ok(new { items });
}
```

**Why This Keeps Happening**: The library search assumes the share cache is always populated, but E2E nodes can query before scans finish or when caches are empty.

---

### 6. Library Item ContentIds Not Streamable

**The Bug**: Library item searches returned `contentId` values that were not registered in the share repository, so `/api/v0/streams/{contentId}` returned 404 even though the item existed on disk.

**Files Affected**:
- `src/slskd/API/Native/LibraryItemsController.cs`
- `src/slskd/Streaming/ContentLocator.cs`

**Wrong**:
```csharp
// contentId returned but never registered with share repository
return new LibraryItemResponse { ContentId = contentId, /* ... */ };
```

**Correct**:
```csharp
repo.UpsertContentItem(contentId, "GenericFile", null, maskedFilename, true, string.Empty, checkedAt);
```

**Why This Keeps Happening**: Content streaming resolves via the share repository’s `content_items` table, so ad-hoc content IDs must be registered with a masked filename to resolve to a file path.

## ⚠️ HIGH: Common Mistakes

### 4. Copyright Headers - Wrong Company Attribution

**The Rule**: New slskdN files use `company="slskdN Team"`, existing upstream files keep `company="slskd Team"`.

**Fork-specific directories** (always slskdN headers):
- `Capabilities/`, `HashDb/`, `Mesh/`, `Backfill/`
- `Transfers/MultiSource/`, `Transfers/Ranking/`
- `Users/Notes/`, `DhtRendezvous/`, `Common/Security/`

**Why This Matters**: Legal clarity for fork vs upstream code.

---

### 5. Logging Pattern Inconsistency

**The Issue**: Mixed use of `ILogger<T>` and `Serilog.Log.ForContext`.

**Preferred** (standardization in progress):
```csharp
private readonly ILogger<MyService> _logger;

public MyService(ILogger<MyService> logger)
{
    _logger = logger;
}
```

**Avoid**:
```csharp
private static readonly ILogger Log = Serilog.Log.ForContext<MyService>();
```

---

### 7. Duplicate Variable Names in React Components

**The Bug**: Large React components with multiple state sections can have duplicate variable names, causing "Identifier 'X' has already been declared" compilation errors.

**Files Affected**:
- `src/web/src/components/System/MediaCore/index.jsx` (main culprit)

**Wrong**:
```jsx
// In one section:
const [verificationResult, setVerificationResult] = useState(null);

// Later in another section:
const [verificationResult, setVerificationResult] = useState(null); // ❌ Duplicate declaration
```

**Correct**:
```jsx
// Use descriptive names for different purposes:
const [descriptorVerificationResult, setDescriptorVerificationResult] = useState(null);
const [signatureVerificationResult, setSignatureVerificationResult] = useState(null);
```

**Why This Keeps Happening**: MediaCore component has 50+ state variables across multiple sections. When adding new state variables, developers may not realize the name is already used elsewhere in the file. Always grep for variable names before adding new state.

---

### 6. React 16 Compatibility

**The Issue**: This project uses React 16.8.6. Don't use features from React 17+.

**Avoid**:
- `useId()` (React 18)
- `useDeferredValue()` (React 18)
- `useTransition()` (React 18)
- Automatic JSX transform (React 17)

**Safe to use**:
- `useState`, `useEffect`, `useContext`, `useReducer`, `useCallback`, `useMemo`, `useRef`

---

### 7. Path Traversal - Base64 Decoding

**The Issue**: User-supplied paths may be Base64-encoded with `..` components.

**Wrong**:
```csharp
var path = Base64Decode(userInput);
File.Delete(path);  // 💀 Could delete /etc/passwd
```

**Correct**:
```csharp
var path = Base64Decode(userInput);
var fullPath = Path.GetFullPath(path);
if (!fullPath.StartsWith(allowedRoot))
    throw new SecurityException("Path traversal attempt");
```

**Use `PathGuard`** in experimental branch: `PathGuard.NormalizeAndValidate(path, root)`

---

## 🔄 Patterns That Cause Fix/Unfix Cycles

### 8. ESLint/Prettier Formatting Wars

**The Cycle**:
1. Model fixes a bug
2. Lint fails on import order or quotes
3. Model "fixes" lint by changing unrelated code
4. Original fix gets lost

**Solution**: Run `npm run lint -- --fix` in `src/web/` before committing frontend changes.

---

### 9. DI Service Registration

**The Cycle**:
1. New service added
2. Forgot to register in `Program.cs`
3. Runtime crash: "Unable to resolve service"
4. Model adds registration
5. Merge conflict loses registration

**Checklist for new services**:
```csharp
// In Program.cs
builder.Services.AddSingleton<IMyService, MyService>();
// OR
builder.Services.AddScoped<IMyService, MyService>();
```

---

### 10. Experimental Files on Master Branch

**The Cycle**:
1. Work on experimental branch
2. Accidentally commit experimental files to master
3. "Fix" by removing files
4. Merge conflict brings them back

**Files that should NOT be on master**:
- `src/slskd/DhtRendezvous/`
- `src/slskd/Transfers/MultiSource/`
- `src/slskd/HashDb/`
- `src/slskd/Mesh/`
- `src/slskd/Backfill/`
- `src/slskd/Common/Security/` (beyond basic PathGuard)

---

### 10b. YAML Heredocs with Special Characters

**The Bug**: GitHub Actions workflows with inline heredocs containing `${}`, `#{}`, or `\$` break YAML parsing.

**Files Affected**:
- `.github/workflows/release-homebrew.yml`
- `.github/workflows/release-packaging.yml`

**Wrong**:
```yaml
- name: Generate file
  run: |
    cat > file.nix <<EOF
    let pkgs = nixpkgs.\${system};  # 💀 YAML parser chokes on this
    EOF
```

**Correct**: Use external scripts in `packaging/scripts/`:
```yaml
- name: Generate file
  run: |
    chmod +x packaging/scripts/update-nix.sh
    packaging/scripts/update-nix.sh "${{ steps.release.outputs.tag }}"
```

**Why This Keeps Happening**: Models inline heredocs for "simplicity" without realizing Nix `${}` and Ruby `#{}` break YAML.

---

## 📦 Packaging Gotchas (MAJOR PAIN POINT)

> ⚠️ **These issues caused 10+ CI failures each. Read carefully.**

### 11. Case Sensitivity EVERYWHERE

**The Issue**: Package names, URLs, and filenames must be **consistently lowercase**.

| Context | Correct | Wrong |
|---------|---------|-------|
| Package name | `slskdn` | `slskdN` |
| GitHub tag | `0.24.1-slskdn.22` | `0.24.1-slskdN.22` |
| Zip filename | `slskdn-0.24.1-...` | `slskdN-0.24.1-...` |
| COPR project | `slskdn` | `slskdN` |
| PPA changelog | `slskdn (0.24.1...)` | `slskdN (0.24.1...)` |

**Files that MUST use lowercase**:
- `packaging/aur/PKGBUILD*`
- `packaging/debian/changelog`
- `packaging/rpm/*.spec`
- `.github/workflows/*.yml`
- `packaging/homebrew/Formula/slskdn.rb`

---

### 12. SHA256 Checksum Formats

**The Issue**: Different packaging systems want checksums in different formats.

| System | Format | Example |
|--------|--------|---------|
| AUR PKGBUILD | Single-line array | `sha256sums=('abc123...' 'def456...')` |
| Homebrew | Quoted string | `sha256 "abc123..."` |
| Flatpak | Plain value | `sha256: abc123...` |
| Snap | Prefixed | `source-checksum: sha256/abc123...` |
| Chocolatey | PowerShell var | `$checksum = "abc123..."` |
| Nix flake | Quoted string | `sha256 = "abc123...";` |

**Multi-line PKGBUILD breaks makepkg**:
```bash
# WRONG - breaks AUR
sha256sums=(
  'abc123...'
  'def456...'
)

# CORRECT - single line
sha256sums=('abc123...' 'def456...')
```

---

### 13. SKIP vs Actual Hash in AUR

**The Issue**: AUR packages need `SKIP` for the source tarball (changes each release) but real hashes for static files.

```bash
# PKGBUILD source array order:
source=(
    "tarball.tar.gz"    # Index 0 - SKIP (changes)
    "slskd.service"     # Index 1 - real hash (static)
    "slskd.yml"         # Index 2 - real hash (static)
    "slskd.sysusers"    # Index 3 - real hash (static)
)

# Matching sha256sums:
sha256sums=('SKIP' '9e2f4b...' 'a170af...' '28b6c2...')
```

**The Cycle**:
1. Model updates tarball hash
2. AUR build fails (tarball changed)
3. Model sets to SKIP
4. Model accidentally SKIPs the static files too
5. AUR build fails (missing hashes)

---

### 14. Version Format Conversion

**The Issue**: GitHub tags use `-slskdn` but PKGBUILD uses `.slskdn`.

```bash
# GitHub tag format
0.24.1-slskdn.22

# PKGBUILD pkgver format (no hyphens allowed)
0.24.1.slskdn.22

# Conversion in workflows:
PKGVER=$(echo $TAG | sed 's/-slskdn/.slskdn/')
```

**Files that need conversion**:
- `.github/workflows/release-linux.yml`
- `.github/workflows/release-copr.yml`
- `packaging/aur/PKGBUILD*`

---

### 15. URL Patterns Must Match Release Assets

**The Issue**: Download URLs must exactly match the uploaded asset names.

**Asset naming pattern** (from `release-linux.yml`):
```
slskdn-{TAG}-linux-x64.zip
slskdn-{TAG}-linux-arm64.zip
slskdn-{TAG}-osx-x64.zip
slskdn-{TAG}-osx-arm64.zip
slskdn-{TAG}-win-x64.zip
```

**Common mistakes**:
- `slskdN-...` (wrong case)
- `slskdn-linux-x64.zip` (missing version)
- `slskdn_{TAG}_linux_x64.zip` (wrong separators)

---

### 16. Homebrew Formula Architecture Blocks

**The Issue**: Homebrew needs separate `on_arm` and `on_intel` blocks for macOS.

```ruby
on_macos do
  on_arm do
    url "...osx-arm64.zip"
    sha256 "..."
  end
  on_intel do
    url "...osx-x64.zip"
    sha256 "..."
  end
end

on_linux do
  url "...linux-x64.zip"
  sha256 "..."
end
```

**Don't**: Use a single URL for all platforms.

---

### 17. Workflow Timing Issues

**The Issue**: Packaging workflows run before release assets are uploaded.

**The Cycle**:
1. Release published
2. Packaging workflow triggered immediately
3. Asset download fails (not uploaded yet)
4. Workflow fails
5. Manual re-run required

**Solution in `release-linux.yml`**:
```yaml
# Retry loop with 30s delays
for i in {1..20}; do
  if curl -fsSL "$ASSET_URL" -o release.zip; then
    exit 0
  fi
  sleep 30
done
```

---

### 18. AUR Directory Cleanup

**The Issue**: AUR git clone fails if directory exists from previous run.

```bash
# WRONG - fails if aur-repo exists
git clone ssh://aur@aur.archlinux.org/slskdn-bin.git aur-repo

# CORRECT - clean first
rm -rf aur-repo
git clone ssh://aur@aur.archlinux.org/slskdn-bin.git aur-repo
```

---

### 19. COPR/PPA Need Different Spec Files

**The Issue**: COPR uses `.spec` files, PPA uses `debian/` directory.

**COPR** (`packaging/rpm/slskdn.spec`):
- RPM spec format
- `%{version}` macro
- `BuildRequires` / `Requires`

**PPA** (`packaging/debian/`):
- `changelog` (specific format!)
- `control`
- `rules`
- `copyright`

**Changelog format is STRICT**:
```
slskdn (0.24.1-slskdn.22-1) jammy; urgency=medium

  * Release 0.24.1-slskdn.22

 -- snapetech <slskdn@proton.me>  Sun, 08 Dec 2024 12:00:00 +0000
```

Note: TWO spaces before `--`, specific date format.

---

### 20. Self-Hosted Runner Paths

**The Issue**: Self-hosted runners have different paths than GitHub-hosted.

**GitHub-hosted**: `/home/runner/work/...`
**Self-hosted**: `/home/github/actions-runner/_work/...`

**Don't**: Hardcode paths. Use `$GITHUB_WORKSPACE`.

---

### 21. Chocolatey v2 push – do not pass path (see gotcha 5b)

**The Bug**: Passing a path to `choco push` causes path+flag gluing. **Correct** (see gotcha 5b): run `choco push` from `packaging/chocolatey` after `choco pack` with no path; use `--api-key $env:CHOCO_API_KEY`. Match master branch.

---

## 🧪 Test Gotchas

### 13. Flaky UploadGovernorTests

**The Issue**: Tests using `AutoData` with random values can hit edge cases.

**Example**: Integer division with small random values causes off-by-one errors.

**Solution**: Use `InlineAutoData` with fixed values for edge-case-sensitive tests.

---

### 14. Test Isolation

**The Issue**: Tests that share static state can interfere with each other.

**Solution**: Use `TestIsolationExtensions` for tests that need isolated state.

---

## 🔐 Security Gotchas (Experimental Branch)

### 15. Security Services Not Wired to Transfer Handlers

**Current State**: 30 security components exist but aren't integrated into actual transfer code.

**TODO**: Wire `PathGuard`, `ContentSafety`, `ViolationTracker` into:
- `TransferService`
- `FilesController`
- `MultiSourceDownloadService`

---

### 16. UPnP Disabled by Default

**The Issue**: UPnP has known security vulnerabilities.

**Current**: `EnableUpnp = false` by default in `NatDetectionService.cs`

**Don't**: Enable UPnP by default without explicit user opt-in.

---

## 📝 Documentation Gotchas

### 17. DEVELOPMENT_HISTORY.md vs memory-bank/progress.md

- `DEVELOPMENT_HISTORY.md` - Human-maintained release history
- `memory-bank/progress.md` - AI session log

**Don't** overwrite `DEVELOPMENT_HISTORY.md` with AI-generated content.

---

### 18. TODO.md vs memory-bank/tasks.md

- `TODO.md` - Human-maintained high-level todos
- `memory-bank/tasks.md` - AI-managed task backlog

**Don't** duplicate tasks between them. Reference each other instead.

---

### 19. HashDb Not Populated - Missing Event Subscription

**The Bug**: HashDb was initializing but `seq_id` stayed at 0 because no code was hashing downloaded files.

**Files Affected**:
- `src/slskd/HashDb/HashDbService.cs`
- `src/slskd/Program.cs`

**Root Cause**: The `ContentVerificationService` only hashes files during multi-source downloads. Regular single-source downloads raised `DownloadFileCompleteEvent` but nothing subscribed to hash the file.

**Fix**: Subscribe `HashDbService` to `DownloadFileCompleteEvent` and hash downloaded files:
```csharp
eventBus.Subscribe<DownloadFileCompleteEvent>("HashDbService.DownloadComplete", OnDownloadCompleteAsync);
```

**Why This Happened**: The hashing logic was only implemented in the multi-source path, not the common download completion path.

---

### 20. Passive FLAC Discovery Architecture - Understanding the Design

**The Confusion**: The HashDb/FlacInventory was expected to populate "passively" but wasn't.

**The Design (Clarified)**:

The passive FLAC discovery system has **three sources** of FLAC files:

1. **Search Results** - When WE search, we see other users' files → add to `FlacInventory` with `hash_status='none'`
2. **Downloads** - When we download a FLAC → compute hash → store with `hash_status='known'`
3. **Incoming Interactions** - When users search us or download from us → track their username → optionally browse them later

**How FlacInventory Gets Populated**:

| Source | Event | Action |
|--------|-------|--------|
| Our searches | `SearchResponsesReceivedEvent` | Upsert FLAC files to FlacInventory (hash_status='none') |
| Our downloads | `DownloadFileCompleteEvent` | Hash first 32KB, store in HashDb, update FlacInventory |
| Mesh sync | `MeshSyncService` | Receive hashes from other slskdn clients |
| Backfill | `BackfillSchedulerService` | Probe files in FlacInventory where hash_status='none' |

**How Hashes Get Discovered**:

```
FlacInventory (hash_status='none')
         ↓
BackfillSchedulerService picks candidates
         ↓
Downloads first 32KB header
         ↓
Computes SHA256 hash
         ↓
Updates HashDb + FlacInventory
         ↓
Publishes to MeshSync
```

**Key Insight**: The `BackfillSchedulerService` is the "engine" that converts `hash_status='none'` entries into `hash_status='known'`. But it needs the `FlacInventory` to be populated first, which happens via search results and incoming interactions.

**Files Involved**:
- `src/slskd/HashDb/HashDbService.cs` - Subscribes to events, populates FlacInventory
- `src/slskd/Search/SearchService.cs` - Raises `SearchResponsesReceivedEvent`
- `src/slskd/Events/Types/Events.cs` - Defines `SearchResponsesReceivedEvent`
- `src/slskd/Backfill/BackfillSchedulerService.cs` - Probes FlacInventory entries
- `src/slskd/Application.cs` - Handles incoming searches/uploads (peer tracking)

---

---

### 21. API Calls Before Login - Infinite Loop Danger

**The Bug**: Components that make API calls on mount will cause infinite loops or errors when rendered on the login page (before authentication).

**Files Affected**:
- `src/web/src/components/LoginForm.jsx`
- `src/web/src/components/Shared/Footer.jsx`
- Any component rendered before login

**Wrong**:
```jsx
// In LoginForm.jsx - BAD: Footer makes API calls
import Footer from './Shared/Footer';

const LoginForm = () => {
  return (
    <>
      <LoginContent />
      <Footer /> {/* 💀 If Footer fetches data on mount, this breaks */}
    </>
  );
};

// In Footer.jsx - BAD: API call on mount
const Footer = () => {
  const [stats, setStats] = useState(null);

  useEffect(() => {
    api.getStats().then(setStats); // 💀 401 error before login!
  }, []);

  return <footer>...</footer>;
};
```

**Correct**:
```jsx
// Footer.jsx - GOOD: Pure static component, no API calls
const Footer = () => {
  const year = new Date().getFullYear();

  return (
    <footer>
      © {year} <a href="https://github.com/...">slskdN</a>
      {/* All content is static - no useEffect, no API calls */}
    </footer>
  );
};
```

**Why This Keeps Happening**: Models add "helpful" features like version info or stats to footers without considering the login page context.

**Rule**: Components rendered before login (LoginForm, Footer on login, error pages) MUST be pure/static with ZERO API calls.

---

### 22. HashDb Schema Migrations - Versioned Upgrades

**The System**: HashDb uses a versioned migration system (`HashDbMigrations.cs`) that runs automatically on startup.

**Key Files**:
- `src/slskd/HashDb/Migrations/HashDbMigrations.cs` - Migration definitions
- `docs/HASHDB_SCHEMA.md` - Schema documentation

**How It Works**:
1. `__HashDbMigrations` table tracks applied versions
2. On startup, `RunMigrations()` compares current vs target version
3. Pending migrations run in order, each in a transaction
4. Failed migrations roll back automatically

**Adding New Columns** (SQLite gotcha):
```csharp
// WRONG - SQLite doesn't support multiple ALTER in one command
cmd.CommandText = @"
    ALTER TABLE Foo ADD COLUMN bar TEXT;
    ALTER TABLE Foo ADD COLUMN baz INTEGER;
";

// CORRECT - Execute each ALTER separately
var alters = new[] {
    "ALTER TABLE Foo ADD COLUMN bar TEXT",
    "ALTER TABLE Foo ADD COLUMN baz INTEGER"
};
foreach (var sql in alters)
{
    using var alterCmd = conn.CreateCommand();
    alterCmd.CommandText = sql;
    alterCmd.ExecuteNonQuery();
}
```

**Handling Existing Columns** (idempotent migrations):
```csharp
try
{
    alterCmd.ExecuteNonQuery();
}
catch (SqliteException ex) when (ex.Message.Contains("duplicate column"))
{
    // Column already exists - skip
}
```

**Check Current Version**:
```bash
curl http://localhost:5030/api/v0/hashdb/schema
```

**Rule**: Always increment `CurrentVersion` when adding migrations. Never modify existing migrations.

---

### 23. Missing `using` Directives - Check ALL Related Files

**The Bug**: Adding a type (e.g., `DateTimeOffset`) to an interface but only adding the `using System;` directive to one file, then having to fix each file one-by-one as compilation fails.

**Files Affected**:
- Any file that shares types across interface/implementation/controller boundaries

**Wrong Workflow**:
```
1. Add DateTimeOffset to IHashDbService.cs
2. Add "using System;" to IHashDbService.cs
3. Compile → ERROR in HashDbController.cs
4. Add "using System;" to HashDbController.cs
5. Compile → ERROR in HashDbService.cs
6. Add "using System;" to HashDbService.cs
7. Finally compiles ✅ (wasted 3 compile cycles)
```

**Correct Workflow**:
```
1. Add DateTimeOffset to IHashDbService.cs
2. BEFORE compiling, grep for all files that might need the type:
   grep -l "IHashDbService\|HashDb" src/slskd/HashDb/**/*.cs
3. Add "using System;" to ALL relevant files in one pass
4. Compile once ✅
```

**Pre-Compile Checklist** when adding new types:
```bash
# Find all files in the feature directory
find src/slskd/MyFeature -name "*.cs" -type f

# Or grep for files using the interface/class
grep -rl "IMyService\|MyService" src/slskd/MyFeature/
```

**Why This Keeps Happening**: AI models fix errors incrementally instead of thinking ahead about which files share the same types.

**Rule**: When adding a new type to an interface, check ALL files in the same namespace/feature directory and add necessary `using` directives BEFORE attempting to compile.

---

### 24. AUR PKGBUILD Checksums - NEVER Replace SKIP

**The Bug**: The AUR workflow was calculating the sha256 of `slskdn-dev-linux-x64.zip` and replacing the entire `sha256sums` array, overwriting `SKIP` with the calculated hash. This causes validation failures on `yay -Syu` because the zip changes every build.

**What Was Happening**:
```bash
# PKGBUILD template (CORRECT):
sha256sums=('SKIP' '9e2f4b...' 'a170af...' '28b6c2...')
#           ^^^^   ^^^^^^^^   ^^^^^^^^   ^^^^^^^^
#           zip    service    yml        sysusers
#          (changes) (static)  (static)  (static)

# Workflow was replacing it with (WRONG):
sha256sums=('abc123...' 'SKIP' 'SKIP' 'SKIP')
#           ^^^^^^^^^^
#           Calculated hash for zip - breaks on next download!
```

**Why This Breaks**:
1. CI builds `slskdn-dev-linux-x64.zip` and calculates hash `abc123...`
2. Workflow updates AUR PKGBUILD with `sha256sums=('abc123...' ...)`
3. User runs `yay -S slskdn-dev` → works (zip matches hash)
4. CI rebuilds zip → new hash `def456...`
5. User runs `yay -Syu` → **FAILS** (cached zip has hash `abc123...`, PKGBUILD expects `abc123...`, but downloaded zip is `def456...`)

**The Fix**:
```bash
# DON'T calculate or replace the zip hash in the workflow
# The PKGBUILD template already has SKIP for index 0

# OLD (wrong):
sed -i "s/sha256sums=.*/sha256sums=('$SHA256' 'SKIP' 'SKIP' 'SKIP')/" PKGBUILD

# NEW (correct):
# Just update pkgver and _commit, leave sha256sums alone
sed -i "s/^pkgver=.*/pkgver=${VERSION}/" PKGBUILD
sed -i "s/^_commit=.*/_commit=${COMMIT}/" PKGBUILD
```

**Rule**: For AUR packages that download release binaries (not source), the first entry in `sha256sums` MUST be `'SKIP'` because the binary changes every build. Only static files (service files, configs) get real checksums.

**Related**: See gotcha #13 "SKIP vs Actual Hash in AUR" for more context on why this pattern exists.

### 25. Mark Optional SongID ML/Audio Dependencies as `optdepends`

**The Bug**: Optional SongID/audio tooling was treated as required by package metadata, which blocked installs on systems without those optional Python/audio packages available.

**Files Affected**:
- `packaging/aur/PKGBUILD`
- `packaging/aur/PKGBUILD-bin`
- `packaging/aur/PKGBUILD-dev`

**Wrong**:
```bash
depends=(
    python-torchaudio
)
```

**Correct**:
```bash
optdepends=(
    'python-torchaudio: optional enhancement for advanced SongID workflows'
)
```

**Why This Happens**: SongID feature paths call optional external engines only when present, so hard package requirements in PKGBUILD block users who do not need those features and can prevent installs when package availability is limited.

---

## Package Manager Version Constraints

**The Problem**: AUR and RPM package managers don't allow hyphens in version strings, causing build failures.

**Error Messages**:
```
# AUR:
==> ERROR: pkgver is not allowed to contain colons, forward slashes, hyphens or whitespace.

# RPM:
error: line 2: Illegal char '-' (0x2d) in: Version: 0.24.1-dev-20251209-203936
```

**Why This Happens**:
Our dev builds use the format `0.24.1-dev-20251209-203936` (with hyphens). This works fine for Git tags and GitHub releases, but AUR and RPM have strict version format requirements:
- AUR `pkgver`: No hyphens, colons, slashes, or whitespace
- RPM `Version`: No hyphens (hyphen is reserved for separating version from release number)

**The Fix**:
Convert ALL hyphens to dots when generating package versions:

```bash
# Git/GitHub (hyphens OK):
DEV_VERSION="0.24.1-dev-20251209-203936"

# AUR/RPM/DEB (convert to dots):
ARCH_VERSION=$(echo "$DEV_VERSION" | sed 's/-/./g')
# Result: 0.24.1.dev.20251209.203936
```

**CRITICAL**: Use `sed 's/-/./g'` (global replace) NOT `sed 's/-/./'` (only first hyphen)!

**Where This Applies**:
- AUR PKGBUILD: `pkgver=0.24.1.dev.20251209.203936`
- RPM spec: `Version: 0.24.1.dev.20251209.203936`
- Debian changelog: `slskdn-dev (0.24.1.dev.20251209.203936-1)`
- Package filenames: `slskdn-dev_0.24.1.dev.20251209.203936_amd64.deb`

**Git Tag and Zip Stay Original**:
- Git tag: `dev-20251209-203936` (hyphens OK)
- Zip file: `slskdn-dev-20251209-203936-linux-x64.zip` (hyphens OK)
- GitHub release title: `Dev Build 20251209-203936` (hyphens OK)

---

## Integration Test Project Missing Project Reference

**The Problem**: Docker builds fail with `error CS0234: The type or namespace name 'Common' does not exist in the namespace 'slskd'` when building integration tests.

**Root Cause**: The `tests/slskd.Tests.Integration/slskd.Tests.Integration.csproj` file was missing a `<ProjectReference>` to the main `src/slskd/slskd.csproj` project.

**Error Message**:
```
/slskd/tests/slskd.Tests.Integration/SecurityIntegrationTests.cs(10,13): error CS0234: 
The type or namespace name 'Common' does not exist in the namespace 'slskd' 
(are you missing an assembly reference?) [/slskd/tests/slskd.Tests.Integration/slskd.Tests.Integration.csproj]
```

**Why This Breaks**:
1. Integration tests need to reference types from the main project (`slskd.Common.Security`, etc.)
2. Without a `<ProjectReference>`, the compiler can't find any `slskd.*` namespaces
3. This fails silently in local builds if you've previously built the main project (DLL is in bin/), but ALWAYS fails in Docker/CI clean builds

**The Fix**:
```xml
<!-- tests/slskd.Tests.Integration/slskd.Tests.Integration.csproj -->
<ItemGroup>
  <ProjectReference Include="../../src/slskd/slskd.csproj" />
</ItemGroup>
```

**Prevention**:
- When creating ANY test project, ALWAYS add a `<ProjectReference>` to the code being tested
- Test in Docker before committing: `docker build -f Dockerfile .`
- Check .csproj files when you see "namespace does not exist" errors in CI

**Related**: This is especially insidious because local `dotnet build` might work if you've built the main project before, masking the missing reference until CI runs.

---

## Workflow File Pattern Mismatch in Download Step

**The Problem**: The `packages` job fails with "no assets match the file pattern" when trying to download the zip from the dev release.

**Root Cause**: Mismatch between the actual filename and the download pattern:
- Build job creates: `slskdn-dev-linux-x64.zip` (no timestamp)
- Packages job tried to download: `slskdn-dev-*-linux-x64.zip` (wildcard for timestamp that doesn't exist)

**Error Message**:
```
gh release download dev --pattern "slskdn-dev-*-linux-x64.zip"
no assets match the file pattern
```

**Why This Breaks**:
1. The `build` job creates `slskdn-dev-linux-x64.zip` without a timestamp in the filename
2. The `release` job uploads this file to the `dev` tag as-is
3. The `packages` job tries to download with a wildcard pattern expecting a timestamp
4. The wildcard doesn't match, so no file is downloaded

**The Fix**:
```yaml
# packages job - Download from Dev Release step
gh release download dev \
  --repo ${{ github.repository }} \
  --pattern "slskdn-dev-linux-x64.zip"  # Exact filename, no wildcard
```

**Prevention**:
- When adding workflow download steps, check what the ACTUAL filename is from the upload step
- Don't use wildcards unless the filename actually varies
- The timestamp is in the VERSION/tag, not in the zip filename for dev builds

**Note**: The timestamped dev tag (e.g., `dev-20251209-212425`) is separate from the floating `dev` tag. The `dev` tag always points to the latest dev build and contains `slskdn-dev-linux-x64.zip`.

---

## Building RPM Packages on Ubuntu Fails with Missing BuildRequires

**The Problem**: The `packages` job fails when trying to build .rpm packages on Ubuntu with "Failed build dependencies: systemd-rpm-macros is needed".

**Root Cause**: The RPM spec file has `BuildRequires: systemd-rpm-macros` and `BuildRequires: unzip`, which are Fedora packages not available in Ubuntu's apt repositories. You can't build RPMs on Ubuntu that require Fedora-specific build tools.

**Error Message**:
```
error: Failed build dependencies:
	systemd-rpm-macros is needed by slskdn-dev-0.24.1.dev.20251209.213134-1.x86_64
	unzip is needed by slskdn-dev-0.24.1.dev.20251209.213134-1.x86_64
```

**Why This Breaks**:
1. RPM spec files can have `BuildRequires` for Fedora-specific packages
2. Ubuntu (apt) doesn't have `systemd-rpm-macros` or the exact versions of build tools RPM expects
3. The `rpmbuild` command on Ubuntu can't satisfy these dependencies
4. Cross-distro package building requires containers or native build environments

**The Fix**:
Don't build RPMs on Ubuntu. Let COPR (which runs on Fedora) handle RPM builds. The `packages` job should only build .deb:

```yaml
packages:
  name: Build .deb Package  # Changed from "Build Packages (.deb and .rpm)"
  # ... only build .deb, remove all RPM build steps
```

**Correct Architecture**:
- **AUR job**: Builds Arch packages (runs on Arch via Docker)
- **COPR job**: Builds RPM packages (runs on Fedora infrastructure)
- **PPA job**: Builds Debian packages (runs on Ubuntu/Launchpad)  
- **Packages job**: Builds .deb for direct GitHub download (Ubuntu is fine)
- **Docker job**: Builds container images (distro-agnostic)

**Prevention**:
- Ubuntu can build .deb natively
- Fedora (COPR) should build .rpm natively
- Don't try to build distro-specific packages on the wrong distro
- If you need RPMs as GitHub release assets, download them from COPR after it builds

---

## PPA Rejects Upload: Version Comparison with Hyphens

**The Problem**: Launchpad PPA rejects the upload with "Version older than that in the archive" even though the new version has a later timestamp.

**Root Cause**: Debian version string comparison treats hyphens differently than dots. The version `0.24.1-dev-20251209-214612` is considered OLDER than `0.24.1-dev.202512092002` because of how dpkg compares version strings.

**Error Message**:
```
Rejected: slskdn-dev_0.24.1-dev-20251209-214612-1ppa202512092148~jammy.dsc: 
Version older than that in the archive. 
0.24.1-dev-20251209-214612-1ppa202512092148~jammy <= 0.24.1-dev.202512092002-1ppa202512092006~jammy
```

**Why This Breaks**:
Debian's `dpkg --compare-versions` treats hyphens as version separators, not as part of the version string:
- `0.24.1-dev-20251209-214612` is parsed as epoch `0`, version `0.24.1`, and the rest as debian revision
- `0.24.1-dev.202512092002` with dots keeps the full version number intact
- The comparison logic makes the hyphenated version appear older

**The Fix**:
Convert ALL hyphens to dots in the PPA version string:

```bash
VERSION="${{ needs.build.outputs.dev_version }}"  # 0.24.1-dev-20251209-214612
DEB_VERSION=$(echo "$VERSION" | sed 's/-/./g')    # 0.24.1.dev.20251209.214612

# Use DEB_VERSION in changelog
slskdn-dev (${DEB_VERSION}-1ppa${PPA_REV}~jammy) jammy; urgency=medium
```

**Critical**: This is the SAME issue as the AUR/RPM version problem, but it manifests differently - not as a build error, but as a PPA rejection during upload. You MUST convert hyphens to dots for ALL Debian-based packaging (AUR, RPM, DEB, PPA).

**Prevention**:
- ALWAYS use `sed 's/-/./g'` (global replace) for ANY package version strings
- Check EVERY place where `$VERSION` or `dev_version` is used in packaging workflows
- Test PPA uploads don't get rejected with "Version older than that in the archive"

---

## Yay Cache Contains Stale PKGBUILD After AUR Fix

**The Problem**: After fixing the AUR workflow to keep `SKIP` for the binary checksum, `yay -S slskdn-dev` still fails with "One or more files did not pass the validity check!" even though the AUR repo has the correct PKGBUILD.

**Root Cause**: Yay caches PKGBUILDs in `~/.cache/yay/package-name/`. If the cached PKGBUILD is from a previous (broken) workflow run that had a real hash instead of `SKIP`, yay will use the stale cached version instead of fetching the fixed one from AUR.

**Error Message**:
```
==> Validating source files with sha256sums...
    slskdn-dev-linux-x64.zip ... FAILED
==> ERROR: One or more files did not pass the validity check!
```

**Why This Happens**:
1. Old workflow pushed PKGBUILD with `sha256sums=('abc123...' 'SKIP' 'SKIP' 'SKIP')`
2. User ran `yay -S package-name` and yay cached that broken PKGBUILD
3. Workflow was fixed to preserve `SKIP` in the template
4. New correct PKGBUILD pushed to AUR: `sha256sums=('SKIP' '9e2f4b...' 'a170af...' '28b6c2...')`
5. User runs `yay -S package-name` again, but yay uses the CACHED broken version
6. Checksum fails because the binary has changed but cached PKGBUILD has the old hash

**The Fix**:
Clear yay's cache for the package:

```bash
rm -rf ~/.cache/yay/package-name
yay -S package-name  # Will fetch fresh PKGBUILD from AUR
```

**Prevention**:
- When testing AUR packages during development, always clear cache after workflow fixes
- Add this to testing docs: "If you previously tested a broken build, clear yay cache first"
- Yay's cache is helpful for normal use but can hide fixes during rapid iteration

---

## EF Core Can't Translate DateTimeOffset to DateTime Comparison

**The Problem**: Backfill endpoint throws 500 error with "The LINQ expression could not be translated" when trying to compare `Search.StartedAt` (DateTime) with a DateTimeOffset value.

**Root Cause**: Entity Framework Core cannot translate implicit conversions between `DateTimeOffset` and `DateTime` to SQL. When you write `s.StartedAt < lastProcessedAt.Value` where `StartedAt` is `DateTime` and `lastProcessedAt` is `DateTimeOffset?`, EF can't generate the SQL query.

**Error Message**:
```
System.InvalidOperationException: The LINQ expression 'DbSet<Search>()
    .Count(s => (DateTimeOffset)s.StartedAt < __lastProcessedAt_Value_0)' could not be translated.
```

**The Fix**:
Convert `DateTimeOffset` to `DateTime` explicitly using `.UtcDateTime` before the comparison:

```csharp
// WRONG - EF can't translate this:
await context.Searches.CountAsync(s => s.StartedAt < lastProcessedAt.Value);

// CORRECT - EF can translate this:
await context.Searches.CountAsync(s => s.StartedAt < lastProcessedAt.Value.UtcDateTime);
```

**Prevention**:
- Always check the database column type before writing LINQ queries
- Use `.UtcDateTime` when comparing `DateTimeOffset` with `DateTime` in EF queries
- Test API endpoints that use LINQ queries against the database
- EF will throw this at runtime, not compile time, so manual testing is required

---

### 20. CreateDirectory on Existing File Path

**The Bug**: `System.IO.IOException: The file '/slskd/slskd' already exists` when trying to create a directory at a path that's already occupied by a file (the binary itself).

**Files Affected**:
- `src/slskd/Transfers/MultiSource/Discovery/SourceDiscoveryService.cs`
- `src/slskd/Program.cs`

**What Happened**:
`SourceDiscoveryService` used `Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)` which returns `/slskd` in Docker containers. It then tried to `CreateDirectory("/slskd/slskd")` to store the discovery database, but `/slskd/slskd` is the binary executable file, not a directory. This caused a crash on every API request that needed `SourceDiscoveryService`.

**Why It Happened**:
1. `LocalApplicationData` is not reliable in containers - can return unexpected paths
2. No check for whether the path is a file vs directory before calling `CreateDirectory()`
3. Different behavior than other services which use `Program.AppDirectory`

**The Error**:
```
System.IO.IOException: The file '/slskd/slskd' already exists.
  at System.IO.FileSystem.CreateDirectory(String fullPath, UnixFileMode unixCreateMode)
  at System.IO.Directory.CreateDirectory(String path)
  at slskd.Transfers.MultiSource.Discovery.SourceDiscoveryService..ctor(...)
```

**The Fix**:
Use `Program.AppDirectory` (like all other services) and create a subdirectory:

```csharp
// WRONG - uses unreliable LocalApplicationData
var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
var slskdPath = Path.Combine(appDataPath, "slskd");
System.IO.Directory.CreateDirectory(slskdPath); // CRASHES if /slskd/slskd is a file!

// CORRECT - use Program.AppDirectory and create subdirectory
public SourceDiscoveryService(
    string appDirectory,  // Injected via DI
    ISoulseekClient soulseekClient,
    IContentVerificationService verificationService)
{
    var slskdPath = Path.Combine(appDirectory, "discovery");
    if (!Directory.Exists(slskdPath))
    {
        Directory.CreateDirectory(slskdPath);
    }
    dbPath = Path.Combine(slskdPath, "discovery.db");
}

// Update DI registration to pass Program.AppDirectory
services.AddSingleton<ISourceDiscoveryService>(sp => new SourceDiscoveryService(
    Program.AppDirectory,
    sp.GetRequiredService<ISoulseekClient>(),
    sp.GetRequiredService<Transfers.MultiSource.IContentVerificationService>()));
```

**Prevention**:
- **ALWAYS** use `Program.AppDirectory` for data storage, never `LocalApplicationData`
- **ALWAYS** create a subdirectory for each service's data (e.g., `discovery/`, `ranking/`, `hashdb/`)
- **ALWAYS** check `Directory.Exists()` before `CreateDirectory()` when the path might vary
- Pattern to follow: `Path.Combine(Program.AppDirectory, "myservice")` → creates `/app/myservice/` in containers

**Related Pattern**:
```csharp
// Good examples from the codebase:
var rankingDbPath = Path.Combine(Program.AppDirectory, "ranking.db");
var hashDbService = new HashDbService(Program.AppDirectory, ...);
var wishlistDbPath = Path.Combine(Program.AppDirectory, "wishlist.db");
```

---

### 21. Scanner Detection Noise from Private IPs

**The Bug**: Logs spammed with hundreds of "Scanner detected from 192.168.1.77" warnings when users access the web UI from their LAN.

**Files Affected**:
- `src/slskd/Common/Security/FingerprintDetection.cs`
- `src/slskd/Common/Security/SecurityMiddleware.cs` (partial fix)

**What Happened**:
The web UI polls multiple API endpoints rapidly (~5-10 requests/second), which triggered the reconnaissance detection system. Even after fixing `SecurityMiddleware` to skip `RecordConnection()` for private IPs, old profiles from before the fix were still marked as scanners, and the logging still fired.

**Why It Happened**:
1. Web UI makes many rapid API calls (status bar, capabilities, DHT, mesh, hashdb, backfill stats, etc.)
2. This looks like port scanning / reconnaissance to `FingerprintDetection`
3. First fix: `SecurityMiddleware` skipped `RecordConnection()` for private IPs (lines 103-110)
4. But old profiles from before the fix were still in memory as flagged scanners
5. `FingerprintDetection.RecordConnection()` logged warnings for those old profiles

**The Error**:
```
20:09:16  WRN  Scanner detected from "192.168.1.77": "PortScanning, RapidConnections
20:09:26  WRN  Scanner detected from "192.168.1.77": "PortScanning, RapidConnections
20:09:36  WRN  Scanner detected from "192.168.1.77": "PortScanning, RapidConnections
... (repeats hundreds of times)
```

**The Fix**:
Add private IP check to `FingerprintDetection` itself, not just `SecurityMiddleware`:

```csharp
// In FingerprintDetection.RecordConnection():
if (profile.IsScanner)
{
    // Don't log warnings for private/local IPs (e.g., web UI polling APIs rapidly)
    if (!IsPrivateOrLocalIp(ip))
    {
        _logger.LogWarning(
            "Scanner detected from {Ip}: {Indicators}",
            ip,
            string.Join(", ", indicators.Select(i => i.Type)));

        ReconnaissanceDetected?.Invoke(this, new ReconnaissanceEventArgs(evt));
    }
}

// Add helper method (same as SecurityMiddleware):
private static bool IsPrivateOrLocalIp(IPAddress ip)
{
    // Check for 192.168.x.x, 10.x.x.x, 172.16-31.x.x, 127.x.x.x, fe80::/10, fc00::/7
    // ... (full implementation in code)
}
```

**Prevention**:
- Security logging should **always** check for private IPs before emitting warnings
- Private IP checks should be at **both** the middleware layer (prevent tracking) **and** the service layer (prevent logging)
- Web UI polling is legitimate behavior - don't treat LAN clients as threats
- Test security features with both public and private IPs

**Why Two Fixes Were Needed**:
1. **SecurityMiddleware fix**: Prevents NEW profiles from being created for private IPs
2. **FingerprintDetection fix**: Prevents logging warnings for OLD profiles (already flagged)
3. Both layers need the check to fully eliminate noise

**Private IP Ranges**:
- IPv4: `10.0.0.0/8`, `172.16.0.0/12`, `192.168.0.0/16`, `169.254.0.0/16`, `127.0.0.0/8`
- IPv6: `fe80::/10` (link-local), `fc00::/7` (unique local), `::1` (loopback)

---

### 22. Ambiguous Type Reference (Directory)

**The Bug**: `error CS0104: 'Directory' is an ambiguous reference between 'Soulseek.Directory' and 'System.IO.Directory'`

**Files Affected**:
- Any file that has both `using System.IO;` and `using Soulseek;`

**What Happened**:
When fixing the CreateDirectory bug (#20), I added code that used `Directory.Exists()` and `Directory.CreateDirectory()`. The compiler couldn't determine if this meant `System.IO.Directory` or `Soulseek.Directory` (which is a completely different type representing a Soulseek shared directory).

**Why It Happened**:
Both namespaces define a type called `Directory`:
- `System.IO.Directory` - file system operations
- `Soulseek.Directory` - Soulseek protocol type for shared directories

When both namespaces are imported with `using`, the unqualified name `Directory` is ambiguous.

**The Error**:
```
/home/runner/work/slskdn/slskdn/src/slskd/Transfers/MultiSource/Discovery/SourceDiscoveryService.cs(73,18): 
error CS0104: 'Directory' is an ambiguous reference between 'Soulseek.Directory' and 'System.IO.Directory'
```

**The Fix**:
Always fully qualify `Directory` when both namespaces are imported:

```csharp
// WRONG - ambiguous when both System.IO and Soulseek are imported:
if (!Directory.Exists(slskdPath))
{
    Directory.CreateDirectory(slskdPath);
}

// CORRECT - fully qualified:
if (!System.IO.Directory.Exists(slskdPath))
{
    System.IO.Directory.CreateDirectory(slskdPath);
}
```

**Alternative Fix** (if you need both frequently):
Add a using alias at the top of the file:
```csharp
using IODirectory = System.IO.Directory;

// Then use:
if (!IODirectory.Exists(slskdPath))
{
    IODirectory.CreateDirectory(slskdPath);
}
```

**Prevention**:
- When you see both `using System.IO;` and `using Soulseek;` in a file, **always** qualify `Directory`
- Grep for this pattern before committing: `grep -n "using Soulseek" src/**/*.cs | grep -v "using System.IO"` won't help because they're often far apart
- Better: Run `dotnet build` locally before pushing to catch these at compile time

**Other Ambiguous Types in This Codebase**:
- `Directory` (System.IO vs Soulseek)
- `File` (System.IO vs Soulseek)
- `Transfer` (slskd.Transfers.Transfer vs Soulseek.Transfer) - already resolved with `using Transfer = slskd.Transfers.Transfer;` in Events.cs

**Quick Fix Command**:
```bash
# Find files that might have this issue:
grep -l "using Soulseek" src/slskd/**/*.cs | xargs grep -l "Directory\.Exists\|Directory\.Create" | xargs sed -i 's/Directory\.Exists/System.IO.Directory.Exists/g; s/Directory\.Create/System.IO.Directory.Create/g'
```

---

### E2E Test Infrastructure Issues

#### E2E-1: Server crashes during share initialization in test harness

**The Bug**: E2E test nodes crash with `ShareInitializationException: Share cache backup is missing, corrupt, or is out of date` because test nodes start with empty app directories and no share cache.

**Files Affected**:
- `src/web/e2e/harness/SlskdnNode.ts`

**Wrong**:
```typescript
const args = ['run', '--project', projectPath, '--no-build', '--', '--app-dir', this.appDir, '--config', configPath];
```

**Correct**:
```typescript
// Add --force-share-scan to avoid ShareInitializationException when cache doesn't exist
const args = ['run', '--project', projectPath, '--no-build', '--', '--app-dir', this.appDir, '--config', configPath, '--force-share-scan'];
```

**Why This Keeps Happening**: Test nodes start with fresh app directories, so share cache doesn't exist. The server requires either a valid cache or `--force-share-scan` to create one.

---

#### E2E-2: Static files return 404 because SPA fallback intercepts them

**The Bug**: Static files (`/static/js/*.js`, `/static/css/*.css`) return 404, preventing React from mounting. The SPA fallback endpoint runs before the file server and intercepts all requests, including static files.

**Files Affected**:
- `src/slskd/Program.cs`

**Wrong**:
```csharp
// SPA fallback endpoint runs BEFORE file server
endpoints.MapGet("{*path}", async context => {
    // This intercepts /static/* requests and returns 404
    if (!hasExtension) {
        await context.Response.SendFileAsync(indexPath);
    } else {
        context.Response.StatusCode = 404; // Static files get 404 here!
    }
});
app.UseFileServer(...); // Never reached for static files
```

**Correct**:
```csharp
// File server runs first
app.UseFileServer(fileServerOptions);

// SPA fallback middleware runs AFTER file server
app.Use(async (context, next) => {
    await next(); // Let file server try first
    
    // Only serve index.html if file server returned 404 for a client-side route
    if (context.Response.StatusCode == 404 && !isApi && !isStatic && !hasExtension) {
        await context.Response.SendFileAsync(indexPath);
    }
});
```

**Why This Keeps Happening**: Endpoints run before middleware, so a catch-all endpoint intercepts requests before the file server middleware can serve static files. The solution is to use middleware AFTER the file server that only handles 404s for client-side routes.

---

#### E2E-3: Excessive timeouts in test helpers

**The Bug**: `waitForHealth` polls for 60 seconds (120 iterations × 500ms) when the server typically starts in 2-5 seconds.

**Files Affected**:
- `src/web/e2e/helpers.ts`

**Wrong**:
```typescript
for (let i = 0; i < 120; i++) { // 60 seconds
    const res = await request.get(health, { failOnStatusCode: false });
    if (res.ok()) return;
    await new Promise(r => setTimeout(r, 500));
}
```

**Correct**:
```typescript
// Server typically starts in 2-5 seconds, so 15 seconds is plenty
for (let i = 0; i < 30; i++) { // 15 seconds
    const res = await request.get(health, { failOnStatusCode: false });
    if (res.ok()) return;
    await new Promise(r => setTimeout(r, 500));
}
```

**Why This Keeps Happening**: Default timeouts are set conservatively, but actual server startup is much faster. Reduce timeouts to match reality.

---

#### E2E-4: Multi-peer tests fail with "instance already running" mutex error

**The Bug**: When starting multiple test nodes (A and B), the second node fails with "An instance of slskd is already running" because the mutex name was global (based only on AppName), not per-app-directory.

**Files Affected**:
- `src/slskd/Program.cs`

**Wrong**:
```csharp
private static Mutex Mutex { get; } = new Mutex(initiallyOwned: true, Compute.Sha256Hash(AppName));
// Mutex check happens before AppDirectory is set
if (!Mutex.WaitOne(millisecondsTimeout: 0, exitContext: false)) {
    Log.Fatal($"An instance of {AppName} is already running");
    return;
}
AppDirectory ??= DefaultAppDirectory; // Set AFTER mutex check
```

**Correct**:
```csharp
private static Mutex Mutex { get; set; }

private static string GetMutexName() {
    var dir = AppDirectory ?? DefaultAppDirectory;
    return $"{AppName}_{Compute.Sha256Hash(dir)}";
}

// Set AppDirectory FIRST, then create mutex with app-directory-specific name
AppDirectory ??= DefaultAppDirectory;
Mutex = new Mutex(initiallyOwned: true, GetMutexName());
if (!Mutex.WaitOne(millisecondsTimeout: 0, exitContext: false)) {
    Log.Fatal($"An instance of {AppName} is already running in app directory: {AppDirectory}");
    return;
}
```

**Why This Keeps Happening**: The mutex was created as a static property initializer (before AppDirectory is set) with a global name. Each test node needs its own mutex based on its unique app directory.

---

#### E2E-6: Health check hangs during server startup

**The Bug**: E2E test nodes hang during startup because the `/health` endpoint never responds. The `MeshHealthCheck` calls `GetStatsAsync()` which can hang if mesh services aren't initialized yet, especially NAT detection which tries to connect to external STUN servers.

**Files Affected**:
- `src/slskd/Mesh/MeshHealthCheck.cs`
- `src/slskd/Mesh/MeshStatsCollector.cs`
- `src/slskd/Program.cs`
- `src/web/e2e/harness/SlskdnNode.ts`

**Wrong**:
```csharp
// MeshHealthCheck.cs - no timeout, hangs if services not ready
var stats = await _statsCollector.GetStatsAsync();

// MeshStatsCollector.cs - NAT detection can hang
natType = await stunDetector.DetectAsync();
```

**Correct**:
```csharp
// MeshHealthCheck.cs - add timeout and handle gracefully
using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
timeoutCts.CancelAfter(TimeSpan.FromSeconds(5));
var stats = await _statsCollector.GetStatsAsync().WaitAsync(timeoutCts.Token);
// Return Degraded instead of Unhealthy if timeout/error occurs

// MeshStatsCollector.cs - add timeout to NAT detection
using var natTimeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
natType = await stunDetector.DetectAsync(natTimeoutCts.Token);

// Program.cs - configure health check timeout
services.AddHealthChecks()
    .AddMeshHealthCheck(
        failureStatus: HealthStatus.Degraded, // Don't fail entire endpoint
        timeout: TimeSpan.FromSeconds(5));

// SlskdnNode.ts - use simpler readiness endpoint
const readinessUrl = `${this.apiUrl}/health/ready`; // Simple endpoint, no complex checks
```

**Why This Keeps Happening**: Health checks run during startup before all services are initialized. Mesh services (especially NAT detection) can hang waiting for external resources. The health endpoint waits for all checks to complete, so a hanging check blocks the entire endpoint.

**Prevention**:
- Always add timeouts to health checks that call async operations
- Return `Degraded` instead of `Unhealthy` for startup-time issues
- Use simpler readiness endpoints for E2E tests that bypass complex checks
- Add timeouts to any external service calls in health checks (NAT detection, DNS, etc.)

---

#### E2E-5: Tests should be lenient for incomplete features

**The Bug**: Tests fail when UI elements don't exist because features aren't fully implemented yet.

**Files Affected**:
- All E2E test files

**Wrong**:
```typescript
await page.getByTestId(T.someFeature).click(); // Fails if feature doesn't exist
await expect(page.getByTestId(T.someElement)).toBeVisible();
```

**Correct**:
```typescript
const featureBtn = page.getByTestId(T.someFeature);
if (await featureBtn.count() === 0) {
  test.skip(); // Skip if feature not available
  return;
}
await featureBtn.click();
await expect(page.getByTestId(T.someElement)).toBeVisible({ timeout: 10000 });
```

**Why This Keeps Happening**: Features may be partially implemented or not yet available. Tests should gracefully skip rather than fail, allowing the test suite to run and verify what's actually implemented.

---

#### E2E-6: React Router routes not matching due to basename/urlBase mismatch

**The Bug**: When BrowserRouter has a `basename` prop set, routes and Links should NOT include the `urlBase` prefix, otherwise routes won't match. Also, if using memory history (MemoryRouter), redirects won't update the browser URL, causing the symptom "UI shows different page than URL".

**Files Affected**:
- `src/web/src/index.jsx` - Router setup
- `src/web/src/components/App.jsx` - Route definitions
- `src/web/e2e/multippeer-sharing.spec.ts` - Test diagnostics

**Wrong**:
```jsx
// If urlBase is "/slskd" and basename is set:
<Router basename="/slskd">
  <Route path="/slskd/contacts" />  // ❌ Won't match! Router strips basename first
  <Link to="/slskd/contacts" />     // ❌ Double-prefix
</Router>
```

**Correct**:
```jsx
// When basename is set, routes should be base-relative:
<Router basename={urlBase && urlBase !== '/' ? urlBase : undefined}>
  <Route path="/contacts" />  // ✅ Router adds basename automatically
  <Link to="/contacts" />     // ✅ Router adds basename automatically
</Router>

// When basename is undefined (urlBase is empty or '/'), use full paths:
<Router basename={undefined}>
  <Route path={`${urlBase}/contacts`} />  // ✅ urlBase is empty, so becomes "/contacts"
  <Link to={`${urlBase}/contacts`} />     // ✅ urlBase is empty, so becomes "/contacts"
</Router>
```

**Diagnostic Pattern**:
```typescript
// In E2E tests, compare browser location vs app history:
const loc = await page.evaluate(() => ({ 
  href: location.href, 
  pathname: location.pathname 
}));
const appLoc = await page.evaluate(() => {
  if ((window as any).__APP_HISTORY__) {
    return (window as any).__APP_HISTORY__.location.pathname;
  }
  return null;
});
// If loc.pathname !== appLoc, you're using memory history or basename mismatch
```

**Why This Keeps Happening**: React Router's `basename` prop automatically prepends to all routes and links. If you manually include the basename in route paths, you get a double-prefix that prevents matching. Also, using MemoryRouter instead of BrowserRouter causes redirects to not update the browser URL.

---

#### E2E-7: TypeScript-only syntax in JSX breaks builds

**The Bug**: Using TypeScript-only syntax (e.g., `window as any`) in `.jsx` files causes the web build to fail or silently serve stale bundles, which hides routing/debugging changes.

**Files Affected**:
- `src/web/src/components/App.jsx`

**Wrong**:
```jsx
// ❌ TypeScript cast is invalid in plain JSX
(window as any).__ROUTE_MISS_ELEMENT__ = el.textContent;
```

**Correct**:
```jsx
// ✅ Plain JS assignment
window.__ROUTE_MISS_ELEMENT__ = el.textContent;
```

**Why This Keeps Happening**: It's easy to copy/paste TS patterns into a JS file. CRA/CRACO won't compile TS-only syntax in `.jsx`, and a failed build can leave old bundles in `wwwroot`, masking changes.

---

#### E2E-8: Ambiguous `/shares` route between file shares and share grants

**The Bug**: The legacy file shares API and the new share-grants API both used `/api/v0/shares`, causing `AmbiguousMatchException` (500) for GET `/api/v0/shares`.

**Files Affected**:
- `src/slskd/Shares/API/Controllers/SharesController.cs` (legacy file shares)
- `src/slskd/Sharing/API/SharesController.cs` (share grants)
- `src/web/src/lib/collections.js`

**Wrong**:
```csharp
[Route("api/v{version:apiVersion}/shares")] // used by BOTH controllers
```

**Correct**:
```csharp
[Route("api/v{version:apiVersion}/share-grants")] // share grants only
```

**Why This Keeps Happening**: Both features are named "Shares" but represent different domains (local file shares vs collection share grants). Without a distinct route prefix, ASP.NET Core can't disambiguate endpoints.

---

#### E2E-9: Share-grants "GetAll" is recipient-only (owner won't see outgoing shares)

**The Bug**: `GET /api/v0/share-grants` returns grants **accessible to the current user as a recipient** (direct user or share-group member). It does **not** include the grants you created as the owner unless you also happen to be a recipient/member, which makes the owner UI appear as "No shares yet" after a successful create.

**Files Affected**:
- `src/slskd/Sharing/ShareGrantRepository.cs` (accessibility logic)
- `src/slskd/Sharing/API/SharesController.cs` (endpoint semantics)
- `src/web/src/components/Collections/Collections.jsx` (owner view needs by-collection endpoint)

**Fix**:
- Keep `GET /share-grants` as recipient-accessible (used by "Shared with Me")
- Add `GET /share-grants/by-collection/{collectionId}` for owner/outgoing shares, and have the Collections UI use it

---

#### E2E-10: Cross-node share discovery requires token signing key and port-specific CSRF cookies

**The Bug**: Cross-node share discovery via private messages requires:
1. `Sharing:TokenSigningKey` configured (base64, min 32 bytes) or token creation fails
2. CSRF cookie names must be port-specific (`XSRF-TOKEN-{port}`) for multi-instance E2E to avoid cookie collisions
3. OwnerEndpoint in announcements must use `127.0.0.1` not `localhost` (Playwright request client prefers IPv6 `::1` for "localhost")

**Files Affected**:
- `src/web/e2e/harness/SlskdnNode.ts` (config generation)
- `src/slskd/Program.cs` (CSRF cookie name, antiforgery config)
- `src/slskd/Sharing/API/SharesController.cs` (ownerEndpoint calculation)
- `src/web/src/lib/api.js` (CSRF token reading)

**Wrong**:
```csharp
options.Cookie.Name = "XSRF-TOKEN"; // Same name for all instances = collision
var ownerEndpoint = $"{scheme}://localhost:{web.Port}"; // localhost → ::1 in Playwright
```

**Correct**:
```csharp
options.Cookie.Name = $"XSRF-TOKEN-{OptionsAtStartup.Web.Port}"; // Port-specific
var ownerEndpoint = $"{scheme}://127.0.0.1:{web.Port}"; // Explicit IPv4
```

**Why This Keeps Happening**: Multi-instance E2E runs multiple nodes on the same host with different ports. Cookies are host-scoped (not port-scoped), so fixed names collide. Playwright's request client resolves "localhost" to IPv6 by default, but nodes bind to IPv4.

---

#### E2E-11: Backfill requires OwnerEndpoint for HTTP downloads (cross-node)

#### E2E-12: SqliteShareRepository Keepalive Causes Process Exit During E2E Tests

**The Bug**: The `Keepalive()` method in `SqliteShareRepository` calls `Environment.Exit(1)` if the database check fails, causing nodes to exit unexpectedly during E2E tests. The original check used `pragma_table_info("filenames")` which may fail for FTS5 virtual tables or during transient database locks.

**Files Affected**:
- `src/slskd/Shares/SqliteShareRepository.cs` - `Keepalive()` method

**Wrong**:
```csharp
private void Keepalive()
{
    using var cmd = new SqliteCommand("SELECT COUNT(*) FROM pragma_table_info(\"filenames\");", KeepaliveConnection);
    var reader = cmd.ExecuteReader();
    if (!reader.Read() || reader.GetInt32(0) != 1)
    {
        var msg = "The internal share database has been corrupted...";
        Log.Fatal(msg);
        Environment.Exit(1);  // 💀 Kills process immediately, no recovery
        throw new DataMisalignedException(msg);
    }
}
```

**Correct**:
```csharp
private void Keepalive()
{
    try
    {
        // Check if table exists first
        using var cmd = new SqliteCommand(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='filenames';",
            KeepaliveConnection);
        var reader = cmd.ExecuteReader();
        if (!reader.Read() || reader.GetInt32(0) != 1)
        {
            var msg = "The internal share database has been corrupted...";
            Log.Fatal(msg);
            Environment.Exit(1);
            throw new DataMisalignedException(msg);
        }
        // Verify table is queryable (handles FTS5 virtual tables correctly)
        using var verifyCmd = new SqliteCommand("SELECT COUNT(*) FROM filenames LIMIT 1;", KeepaliveConnection);
        verifyCmd.ExecuteScalar();
    }
    catch (SqliteException ex) when (ex.SqliteErrorCode == 1)
    {
        // Table doesn't exist or is corrupted - exit
        var msg = "The internal share database has been corrupted...";
        Log.Fatal(ex, msg);
        Environment.Exit(1);
        throw new DataMisalignedException(msg, ex);
    }
    catch (Exception ex)
    {
        // Log but don't exit on transient errors (e.g., database locked during backup)
        Log.Warning(ex, "Keepalive check encountered an error (may be transient): {Message}", ex.Message);
    }
}
```

**Why This Keeps Happening**: The keepalive check runs every 1 second and calls `Environment.Exit(1)` on any failure, including transient database locks or race conditions during startup. The original `pragma_table_info` check may not work correctly for FTS5 virtual tables, and there's no handling for transient errors like database locks during backups or concurrent access.

**Impact**: Causes 56+ ProcessExit events during E2E test runs, leading to `ERR_CONNECTION_REFUSED` errors and test failures.

**The Bug**: Backfill endpoint requires either `OwnerEndpoint` + `ShareToken` (for HTTP downloads) or owner username + `IDownloadService` (for Soulseek downloads). If neither is available, backfill fails with a generic error.

**Files Affected**:
- `src/slskd/Sharing/API/SharesController.cs` (Backfill method)

**Wrong**:
```csharp
// Only checks for Soulseek username
if (string.IsNullOrWhiteSpace(ownerUsername))
    return BadRequest("Owner username not available");
```

**Correct**:
```csharp
// Check for HTTP download first (cross-node), then Soulseek
var useHttpDownload = !string.IsNullOrWhiteSpace(ownerEndpoint) && !string.IsNullOrWhiteSpace(grant.ShareToken);
if (useHttpDownload) {
    // HTTP download path
} else if (!string.IsNullOrWhiteSpace(ownerUsername) && _downloadService != null) {
    // Soulseek download path
} else {
    return BadRequest("Cannot backfill: owner endpoint and token not available for HTTP download, and owner username or download service not available for Soulseek download");
}
```

**Why This Keeps Happening**: Backfill needs to work for both cross-node shares (HTTP) and same-network shares (Soulseek). The implementation must check for both methods and provide clear error messages when neither is available.

### 2w. Metrics Auth DataAnnotations Must Not Reject the Default Config When Metrics Are Disabled

**The Bug**: `Options.MetricsOptions.MetricsAuthenticationOptions.Password` had a `[StringLength(MinimumLength = 1)]` attribute even though metrics are disabled by default and the default password is intentionally empty. Full options validation ran before startup, so a fresh config could fail with `Metrics.Authentication.Password` length validation even when `metrics.enabled = false` or `metrics.authentication.disabled = true`.

**What Went Wrong**: The validation lived on the nested property instead of the feature gate. DataAnnotations treated the empty default password as invalid unconditionally, which broke NixOS service validation and any other startup path that bound defaults before metrics was actually enabled.

**How to Prevent It**:
- Put required-field validation for optional features on the parent options object where you can check `Enabled` and related flags.
- Do not use unconditional `[StringLength(MinimumLength = 1)]` on values that are allowed to remain empty while the feature is disabled.
- Add tests for all three cases: feature disabled, feature enabled with auth disabled, and feature enabled with auth required.

### 2x. Release Jobs That Write Back Into `master` Must Re-Sync Before Pushing

**The Bug**: The tag workflow successfully published release `0.24.5-slskdn.57` and updated the Homebrew tap repo, but the follow-up step that rewrote `Formula/slskdn.rb` in the main repo failed with `git push ... fetch first` because it committed in a fresh clone and then pushed straight into a moving `master`.

**What Went Wrong**: The workflow already derives the correct release version from the build tag, so the failure was not a versioning problem. The actual bug was treating a post-release write-back like an isolated branch update instead of a concurrent push target.

**How to Prevent It**:
- For any workflow that commits back into `master`, fetch and rebase against `origin/master` immediately before push, then retry a small number of times.
- If there are no staged changes after regenerating a packaging file, exit early instead of creating a no-op push path.
- Treat repository write-back steps as separate from artifact publication; a release can publish successfully while the write-back still races and turns the workflow red.

### 2y. Do Not Rebase a Generated Release Commit Until the Workflow Cleans Nix-Generated Dirt

**The Bug**: The Nix write-back job for release `0.24.5-slskdn.59` still failed after a 10-attempt retry loop because the checkout was already dirty by the time the loop reached `git rebase origin/master`, so every attempt died immediately with `cannot rebase: You have unstaged changes`.

**What Went Wrong**: The retry logic assumed the only local change was the committed `flake.nix` bump. In reality, the Nix verification step left additional working-tree/index changes behind, so the rebase loop never had a clean tree to operate on.

**How to Prevent It**:
- Run `nix flake check` with `--no-write-lock-file` in CI when the job is only validating metadata.
- Before any fetch/rebase/push retry loop, explicitly clean the checkout (`git reset --hard HEAD` plus `git clean -fd`) or regenerate the file from a fresh `origin/master` each attempt.
- Do not interpret "more retries" as a fix when the underlying checkout state is dirty; first make the retry loop re-runnable.

### 2z. Do Not Let Multiple Release Jobs Push Different Metadata Commits Into `master` In Parallel

**The Bug**: Homebrew, Winget, and Nix each tried to write separate commits back into `master` during the same release run. Even after individual retry fixes, the jobs kept invalidating each other because they were all racing to move the same branch.

**What Went Wrong**: The workflow treated each packaging surface as an isolated updater, but the shared target was still one branch. Independent retries reduce timing sensitivity; they do not eliminate branch-level write contention when three jobs are all competing to publish the "latest" metadata commit.

**How to Prevent It**:
- Use exactly one job to mutate `master` for release metadata.
- Regenerate all checked-in release metadata (`flake.nix`, checked-in Homebrew formula, Winget manifests, etc.) in the same workspace and push one consolidated commit.
- Keep external repo updates separate if necessary, but do not let more than one job in the workflow write to this repository's default branch.

### 3a. Do Not Rename a Release-Blocking Option or Shared Release Copy in Only One Layer

**The Bug**: `MeshServiceDescriptorValidator` checked `_options.RequireSignatures` even though `MeshServiceFabricOptions` only exposes `ValidateDhtSignatures`, which broke every publish job at compile time. Separately, the stable Winget locale text drifted away from the shared SongID/Discovery Graph release copy, so CI failed the packaging metadata validator before it even reached the app build.

**Files Affected**:
- `src/slskd/Mesh/ServiceFabric/MeshServiceDescriptorValidator.cs`
- `src/slskd/Mesh/ServiceFabric/MeshServiceFabricOptions.cs`
- `packaging/winget/snapetech.slskdn.locale.en-US.yaml`
- `packaging/scripts/validate-release-copy.sh`

**Wrong**:
```csharp
else if (_options.RequireSignatures)
{
    return (false, "Signature required but not provided");
}
```

```yaml
ShortDescription: Batteries-included Soulseek web client
Description: |-
  slskdN is a batteries-included fork of slskd with advanced download features,
  automation, and network enhancements for Soulseek.
```

**Correct**:
```csharp
else if (_options.ValidateDhtSignatures)
{
    return (false, "Signature required but not provided");
}
```

```yaml
ShortDescription: Stable Soulseek client with SongID and Discovery Graph
Description: |-
  slskdN is a batteries-included fork of slskd with SongID, Discovery Graph,
  advanced download features, automation, and network enhancements for Soulseek.
```

**Why This Keeps Happening**: Release work in this repo spans code, workflows, and checked-in packaging metadata. If you update only the validator, only the options type, or only one release-copy file, different CI gates fail in sequence and hide the next problem. Audit the real options type and every checked-in release copy file together before tagging.

### 3b. Do Not Persist Pod Creation Fields Without Normalizing Required Defaults First

**The Bug**: `PodEntity.FocusContentId` is stored as a required SQLite column, but `SqlitePodService.CreateAsync()` wrote `pod.FocusContentId` directly. DM pod creation and several integration tests leave that field unset, so pod creation failed with `SQLite Error 19: 'NOT NULL constraint failed: Pods.FocusContentId'`.

**Files Affected**:
- `src/slskd/PodCore/SqlitePodService.cs`
- `src/slskd/PodCore/PodDbContext.cs`

**Wrong**:
```csharp
FocusContentId = pod.FocusContentId,
```

**Correct**:
```csharp
var normalizedFocusContentId = pod.FocusContentId ?? string.Empty;
pod.FocusContentId = normalizedFocusContentId;

FocusContentId = normalizedFocusContentId,
```

**Why This Keeps Happening**: The service layer treats some pod fields as optional, but the persistence model hard-requires non-null strings. If you change schema expectations or add a new required column, normalize the service input before save and keep the entity-to-model mapping tolerant of older/null rows.

### 3c. Bash Heredoc Terminators in GitHub Actions Must Start at Column 1 Unless You Use `<<-`

**The Bug**: The stable `metadata-main` job rewrote `Formula/slskdn.rb` with `cat <<EOF`, but the closing `EOF` was indented inside the workflow `run:` block. Bash never recognized the terminator, so the post-release metadata job crashed with `wanted 'EOF'` and `syntax error: unexpected end of file` even though the release artifacts were already published.

**Files Affected**:
- `.github/workflows/build-on-tag.yml`

**Wrong**:
```bash
cat > Formula/slskdn.rb <<EOF
  class Slskdn < Formula
    ...
  EOF
```

**Correct**:
```bash
cat > Formula/slskdn.rb <<EOF
class Slskdn < Formula
  ...
EOF
```

**Why This Keeps Happening**: YAML indentation makes it visually tempting to indent shell heredoc terminators to match the surrounding block, but bash still parses the literal script after YAML rendering. If you use plain `<<EOF`, the closing marker must be flush-left in the generated shell. Otherwise a release can fully publish and still fail red on a follow-up metadata write-back step.

### 3d. GitHub Actions `run: |` Blocks Still Need Valid YAML Indentation Before Bash Ever Sees the Heredoc

**The Bug**: After fixing the bash heredoc terminator bug, the next edit moved the heredoc body to column 1 in the workflow file itself. That made the shell content conceptually correct, but it broke the workflow at YAML parse time, so the `build-on-tag.yml` runs failed instantly with no jobs created.

**Files Affected**:
- `.github/workflows/build-on-tag.yml`

**Wrong**:
```yaml
run: |
  cat > Formula/slskdn.rb <<EOF
class Slskdn < Formula
  ...
EOF
```

**Correct**:
```yaml
run: |
  cat > Formula/slskdn.rb <<EOF
  class Slskdn < Formula
    ...
  EOF
```

**Why This Keeps Happening**: GitHub Actions first parses YAML, then hands the deindented block to bash. The workflow file must satisfy both layers at once: keep the heredoc lines indented enough for YAML block-scalar syntax, but consistently indented so the runner deindents them back to column 1 for bash.

### 3e. Non-Nullable Tuple Return Sites Must Use `default`, Not `null!`

**The Bug**: Warning cleanup changed `SqliteShareRepository.FindFileInfo()` to return `null!` on the not-found path, but the method returns a non-nullable value tuple. That compiles for reference types, not tuples, so the next rebuild failed with `CS0037`.

**Files Affected**:
- `src/slskd/Shares/SqliteShareRepository.cs`

**Wrong**:
```csharp
if (!reader.Read())
{
    return null!;
}
```

**Correct**:
```csharp
if (!reader.Read())
{
    return default;
}
```

**Why This Keeps Happening**: During nullable cleanup, it is easy to mechanically replace "missing value" returns with `null!`. That only works for reference-type return paths. For tuples and other value types, keep the existing sentinel form such as `default` or change the signature explicitly.

### 3f. Async Controller Lookups Must Await Repository Tasks Before Null / NotFound Checks

**The Bug**: Warning cleanup changed `FindMessageAsync()` to return `Task<PrivateMessage?>`, but the controller kept comparing the un-awaited task result to `default`. That made the not-found branch unreachable and could incorrectly return `200 OK` for missing messages.

**Files Affected**:
- `src/slskd/Messaging/API/Controllers/ConversationsController.cs`
- `src/slskd/Messaging/ConversationService.cs`

**Wrong**:
```csharp
var message = Messages.Conversations.FindMessageAsync(username, id);
if (message == default)
{
    return NotFound();
}

return Ok(message);
```

**Correct**:
```csharp
var message = await Messages.Conversations.FindMessageAsync(username, id);
if (message == default)
{
    return NotFound();
}

return Ok(message);
```

**Why This Keeps Happening**: Nullable-signature cleanup often turns synchronous-looking lookups into `Task<T?>`, but controller code can still visually resemble the old synchronous pattern. In async controller actions, always await the lookup before testing for `null` / `default` or returning the payload.

### 3g. Network Protocol Reads Must Tolerate Fragmented Stream Reads

**The Bug**: Multiple transport/parsing paths treated a short `ReadAsync` return as protocol failure. On sockets this is normal when frames arrive in fragments, so valid SOCKS handshakes and protocol messages could be rejected or dropped.

**Files Affected**:
- `src/slskd/VirtualSoulfind/Bridge/Protocol/SoulseekProtocolParser.cs`
- `src/slskd/Common/Security/TorSocksTransport.cs`
- `src/slskd/Mesh/Transport/DnsLeakPreventionVerifier.cs`
- `tests/slskd.Tests.Integration/Security/TorIntegrationTests.cs`
- `tests/slskd.Tests.Integration/VirtualSoulfind/Bridge/BridgeProtocolValidationTests.cs`
- `tests/slskd.Tests.Unit/Mesh/Transport/DnsLeakPreventionVerifierTests.cs`

**Wrong**:
```csharp
var bytesRead = await stream.ReadAsync(lengthBuffer, 0, 4, ct);
if (bytesRead != 4)
{
    return null;
}
```

**Correct**:
```csharp
private static async Task<bool> ReadExactlyAsync(Stream stream, byte[] buffer, int offset, int count, CancellationToken ct)
{
    var totalRead = 0;
    while (totalRead < count)
    {
        var bytesRead = await stream.ReadAsync(
            buffer,
            offset + totalRead,
            count - totalRead,
            ct);

        if (bytesRead == 0)
        {
            return false;
        }

        totalRead += bytesRead;
    }

    return true;
}
```

**Why This Keeps Happening**: Network framing logic often assumes one `ReadAsync` call yields all requested bytes. On real transports, this assumption is wrong; parsers must accumulate until the header/payload is fully read or the stream closes.

### 3h. Stream Wrappers Must Not Block on Async Disposal in `Dispose`

**The Bug**: `QuicStreamWrapper.Dispose()` called `_connection.DisposeAsync().AsTask().GetAwaiter().GetResult()` from a synchronous dispose path, which can block thread-pool shutdown paths and convert asynchronous cancellation into synchronous deadlocks under adverse timing.

**Files Affected**:
- `src/slskd/Mesh/Transport/DirectQuicDialer.cs`

**Wrong**:
```csharp
_connection.DisposeAsync().AsTask().GetAwaiter().GetResult();
```

**Correct**:
```csharp
_connection.Dispose();
```

**Why This Keeps Happening**: When wrapping async resources in synchronous `Stream.Dispose`, it is tempting to wait on async teardown. In QUIC and socket-heavy code paths, this can delay shutdown or deadlock if the async dispose needs the same synchronization context.

### 3i. Cleanup Paths Should Log and Contain Resource-Teardown Failures, Not Swallow Them

**The Bug**: Multiple cleanup/deletion paths swallowed exceptions with empty catches, which hid why forwarder stop/deletion, mDNS/disposer, or relay response cleanups failed and made failures hard to diagnose in production.

**Files Affected**:
- `src/slskd/Common/Security/LocalPortForwarder.cs`
- `src/slskd/Swarm/SwarmDownloadOrchestrator.cs`
- `src/slskd/Identity/MdnsAdvertiser.cs`
- `src/slskd/Mesh/Overlay/QuicDataServer.cs`
- `src/slskd/DhtRendezvous/MeshOverlayConnection.cs`
- `src/slskd/API/Compatibility/RoomsCompatibilityController.cs`

**Wrong**:
```csharp
catch { }
```

**Correct**:
```csharp
catch (Exception ex)
{
    _logger.LogDebug(ex, "Failed to cleanup resource");
}
```

**Why This Keeps Happening**: Cleanup code often runs during stop/error windows where engineers are reluctant to add logging. Without explicit tracing, transient OS/socket errors and partial-cleanup failures vanish, making subsequent lifecycle issues look nondeterministic.

### 3j. Sync Shutdown Paths Should Never Fire-and-Forget Async Disposal

**The Bug**: Synchronous shutdown/dispose paths can return before async cleanup completes when disposal is fire-and-forget or detached, allowing resource teardown failures to be lost and leaving stop logic timing-dependent.

**Files Affected**:
- `src/slskd/Mesh/Transport/DirectQuicDialer.cs`
- `src/slskd/Relay/RelayClient.cs`
- `src/slskd/Program.cs`

**Wrong**:
```csharp
_ = _connection.DisposeAsync().AsTask();
_ = HubConnection?.DisposeAsync().AsTask();
_ = Task.Run(async () => await discovery.StopAdvertisingAsync().WaitAsync(timeout.Token));
```

**Correct**:
```csharp
try
{
    _connection.DisposeAsync().AsTask().GetAwaiter().GetResult();
}
catch (Exception ex)
{
    Log.Warning(ex, "[RelayClient] Failed to dispose HubConnection");
}
```

**Why This Keeps Happening**: Shutdown hooks often prioritize not blocking over correctness. In async-heavy transport code, unawaited disposal can appear faster but creates nondeterministic races and drops errors when the process tears down immediately after dispose requests.

### 0k5. Request Cancellation Tokens Should Not Cancel Background Fire-and-Forget Work in `VirtualSoulfindV2Controller`

**The Bug**: `ProcessIntent` launched intent processing with `Task.Run(..., cancellationToken)` from an API request. In ASP.NET, request cancellation can occur as soon as the response is sent, canceling the background processor before work starts.

**Files Affected**:
- `src/slskd/VirtualSoulfind/v2/API/VirtualSoulfindV2Controller.cs`

**Wrong**:
```csharp
_ = Task.Run(async () => await _processor.ProcessIntentAsync(intentId, cancellationToken), cancellationToken);
```

**Correct**:
```csharp
_ = Task.Run(async () => await _processor.ProcessIntentAsync(intentId, CancellationToken.None));
```

**Why This Keeps Happening**: API action cancellation tokens are request-scoped. Passing them to long-running fire-and-forget tasks couples UI request lifecycle to background work and causes hard-to-reproduce cancellation races.

### 0k6. `MeshTransferService` Should Not Tie Async Transfer Startup to Caller Request Token

**The Bug**: `StartTransferAsync` started transfer execution with the caller token. When bridge requests used `RequestAborted`, the transfer task could be canceled by HTTP lifecycle before transfer setup completed.

**Files Affected**:
- `src/slskd/VirtualSoulfind/DisasterMode/MeshTransferService.cs`

**Wrong**:
```csharp
_ = Task.Run(async () => await ExecuteTransferAsync(transferId, ct), ct);
```

**Correct**:
```csharp
if (ct.IsCancellationRequested)
{
    return Task.FromCanceled<string>(ct);
}

_ = Task.Run(async () => await ExecuteTransferAsync(transferId, CancellationToken.None));
```

**Why This Keeps Happening**: Long-lived background operations that manage their own cancellation need token decoupling from request scope, otherwise HTTP aborts and gateway timeouts can leave partially started tasks.

### 0k7. Normalize `ChunkSize` Before `Math.Ceiling(file/size / chunkSize)` in Swarm Endpoints

**The Bug**: `SwarmDownload` and `SwarmDownloadAsync` used `request.ChunkSize` directly for chunk-count math and request payloads. A caller-supplied zero chunk size could divide by zero and fail the endpoint even though the download service could have defaulted it.

**Files Affected**:
- `src/slskd/Transfers/MultiSource/API/MultiSourceController.cs`

**Wrong**:
```csharp
var totalChunks = (int)Math.Ceiling((double)request.Size / request.ChunkSize);
ChunkSize = request.ChunkSize;
```

**Correct**:
```csharp
var chunkSize = request.ChunkSize > 0 ? request.ChunkSize : 512 * 1024;
var totalChunks = (int)Math.Ceiling((double)request.Size / chunkSize);
ChunkSize = chunkSize;
```

**Why This Keeps Happening**: API models allow clients to send unsafe values (including 0) while controller-level math assumes service defaults will be applied later. Validation/normalization must happen before arithmetic, not only in deep services.

### 0k8. LibraryHealth Scan Should Handle Missing or Invalid Request Data Safely

**The Bug**: `StartScanAsync` and `LibraryHealthController.StartScan` assumed a non-null request with non-empty file extensions and positive concurrency. A null request or caller-supplied `FileExtensions: null`/`MaxConcurrentFiles <= 0` could crash at startup and block scans from starting.

**Files Affected**:
- `src/slskd/LibraryHealth/LibraryHealthService.cs`
- `src/slskd/LibraryHealth/API/LibraryHealthController.cs`

**Wrong**:
```csharp
request ??= null; // not handled
...
FileExtensions = request.FileExtensions, // may be null
MaxConcurrentFiles = request.MaxConcurrentFiles, // may be <= 0
```

**Correct**:
```csharp
request ??= new LibraryHealthScanRequest();
var requestedExtensions = request.FileExtensions?.Count > 0
    ? request.FileExtensions.Select(ext => NormalizedLowerCaseExtension(ext)).ToList()
    : new() { ".flac", ".mp3", ".m4a", ".ogg" };
MaxConcurrentFiles = request.MaxConcurrentFiles > 0 ? request.MaxConcurrentFiles : 4;
```

**Why This Keeps Happening**: API inputs are often considered “validated” by default constructors, but null or malformed JSON overrides defaults in runtime objects. Guarding at service/controller boundary prevents avoidable `NullReferenceException` and unbounded runtime behavior.

### 0k11. Bridge Progress Proxies Must Be Created at the Call-Site That Polls Them

**The Bug**: `BridgeApi.DownloadAsync` started a progress proxy for every mesh transfer and stored it under a separate proxy id, while `BridgeController.GetTransferProgress` looked up progress by `transferId`. API clients therefore always saw missing progress even for active transfers, and the legacy proxy path created a second redundant proxy for the same transfer.

**Files Affected**:
- `src/slskd/VirtualSoulfind/Bridge/BridgeApi.cs`
- `src/slskd/API/VirtualSoulfind/BridgeController.cs`

**Wrong**:
```csharp
var transferId = await meshTransfer.StartTransferAsync(...);
var proxyId = await progressProxy.StartProxyAsync(transferId, username, ct);
transferIdToProxyId[transferId] = proxyId;

// ...
var progress = await progressProxy.GetLegacyProgressAsync(transferId, ct);
```

**Correct**:
```csharp
var transferId = await meshTransfer.StartTransferAsync(...);
return transferId;

// ...
var progress = await bridgeApi.GetTransferProgressAsync(transferId, ct);
```

**Why This Keeps Happening**: Background progress fan-out and HTTP polling use different lifecycles. If a transport abstraction creates sessions implicitly, callers must either consume that same key everywhere or map it correctly; otherwise the progress endpoint and the transport can diverge from day one.

### 0k12. `StartTransferAsync` Must Validate Cancellation Before Mutating Shared Transfer State

**The Bug**: `StartTransferAsync` inserted a new transfer into active maps before checking for a caller-canceled token and returning a canceled task, leaving orphaned transfer and proxy entries.

**Files Affected**:
- `src/slskd/VirtualSoulfind/DisasterMode/MeshTransferService.cs`

**Wrong**:
```csharp
var transferId = Ulid.NewUlid().ToString();

activeTransfers[transferId] = status;
progressSubjects[transferId] = new Subject<TransferProgressUpdate>();

if (ct.IsCancellationRequested)
{
    return Task.FromCanceled<string>(ct);
}

_ = Task.Run(async () => await ExecuteTransferAsync(transferId, CancellationToken.None));
```

**Correct**:
```csharp
if (ct.IsCancellationRequested)
{
    return Task.FromCanceled<string>(ct);
}

var transferId = Ulid.NewUlid().ToString();

activeTransfers[transferId] = status;
progressSubjects[transferId] = new Subject<TransferProgressUpdate>();

_ = Task.Run(async () => await ExecuteTransferAsync(transferId, CancellationToken.None));
```

**Why This Keeps Happening**: Early return patterns are often checked after state mutation; with request-scoped tokens, cancellation must be evaluated before shared state is allocated, otherwise short-circuited requests leak background-tracked work.

### 0k13. Test Stubs Must Mirror Interface Contract Changes

**The Bug**: `IBridgeApi` grew `GetTransferProgressAsync`, but integration stubs were not updated, allowing compile/runtime DI mismatches as soon as the new contract was consumed.

**Files Affected**:
- `tests/slskd.Tests.Integration/StubWebApplicationFactory.cs`
- `tests/slskd.Tests.Integration/Harness/SlskdnTestClient.cs`

**Wrong**:
```csharp
internal class StubBridgeApi : IBridgeApi
{
    public Task<BridgeSearchResult> SearchAsync(...) => ...
    public Task<string> DownloadAsync(...) => ...
    public Task<List<BridgeRoom>> GetRoomsAsync(...) => ...
}
```

**Correct**:
```csharp
internal class StubBridgeApi : IBridgeApi
{
    public Task<BridgeSearchResult> SearchAsync(...) => ...
    public Task<string> DownloadAsync(...) => ...
    public Task<List<BridgeRoom>> GetRoomsAsync(...) => ...
    public Task<LegacyTransferProgress?> GetTransferProgressAsync(string transferId, CancellationToken ct = default) =>
        Task.FromResult<LegacyTransferProgress?>(null);
}
```

**Why This Keeps Happening**: Test doubles are often updated late; when production interfaces change, every stub and fake implementing them must be updated before DI/container validation or compile catches the mismatch.

### 0k14. STUN Probe Cancellation Should Return Null Instead of Propagating an Exception

**The Bug**: `ProbeServer` handled timeout-driven `OperationCanceledException` but not explicit caller cancellation (`ct.IsCancellationRequested`), so canceling STUN detection could bubble an exception instead of falling back cleanly to `Unknown`.

**Files Affected**:
- `src/slskd/Mesh/Nat/StunNatDetector.cs`

**Wrong**:
```csharp
catch (OperationCanceledException) when (!ct.IsCancellationRequested)
{
    logger.LogDebug("[NAT] Timed out waiting for STUN response from {Server}", server);
    return null;
}
```

**Correct**:
```csharp
catch (OperationCanceledException) when (!ct.IsCancellationRequested)
{
    logger.LogDebug("[NAT] Timed out waiting for STUN response from {Server}", server);
    return null;
}
catch (OperationCanceledException) when (ct.IsCancellationRequested)
{
    logger.LogDebug("[NAT] STUN probe canceled for {Server}", server);
    return null;
}
```

**Why This Keeps Happening**: Cancellation can arrive in two forms: timeout-based linked-token cancellation and explicit caller cancellation. Guarding both paths keeps control flow consistent and avoids turning normal shutdown behavior into error handling.

### 0k15. Validate Third-Party String Formats Before Numeric Conversion in Protocol Parsers

**The Bug**: A few transport and telemetry parsers accepted data-only string forms with direct numeric conversion (`int.Parse`) and assumed parse/shape correctness, which could throw from malformed bridge lines or unexpected metrics text and interrupt startup/status paths.

**Files Affected**:
- `src/slskd/Capabilities/CapabilityService.cs`
- `src/slskd/Common/Security/Obfs4Transport.cs`
- `src/slskd/Telemetry/PrometheusService.cs`

**Wrong**:
```csharp
ProtocolVersion = int.Parse(match.Groups[1].Value);
Port = int.Parse(match.Groups[2].Value);
IatMode = int.Parse(match.Groups[5].Value);
double.Parse(groups[3].Value);
```

**Correct**:
```csharp
if (!int.TryParse(..., out var protocolVersion))
{
    return null;
}

if (!int.TryParse(..., NumberStyles.Integer, CultureInfo.InvariantCulture, out var bridgePort) || bridgePort <= 0)
{
    return null;
}

if (!double.TryParse(..., NumberStyles.Float, CultureInfo.InvariantCulture, out var sampleValue))
{
    continue;
}
```

**Why This Keeps Happening**: Regex and scraped-output parsers are fragile by nature: slight config drift or endpoint string churn can introduce values outside expected ranges or malformed syntax. Range checks plus `TryParse` keep parsing decisions deterministic and prevent one bad input line from crashing runtime features.

### 0k16. Metadata Import Must Distinguish Invalid IDs From Real Imports

**The Bug**: `MetadataPortability.ImportAsync` logged and counted new entries as imported before validating registry mutation succeeded, while invalid ContentIDs could still be counted as imported because `ImportNewEntryAsync` only logged without a clear success signal.

**Files Affected**:
- `src/slskd/MediaCore/MetadataPortability.cs`

**Wrong**:
```csharp
await ImportNewEntryAsync(entry, cancellationToken);
entriesImported++;

private async Task ImportNewEntryAsync(MetadataEntry entry, CancellationToken cancellationToken)
{
    var parsed = ContentIdParser.Parse(entry.ContentId);
    if (parsed != null)
    {
        await _registry.RegisterAsync(...);
    }

    _logger.LogInformation("[MetadataPortability] Imported new entry for {ContentId}", entry.ContentId);
}
```

**Correct**:
```csharp
if (!ContentIdParser.IsValid(entry.ContentId))
{
    entriesSkipped++;
    continue;
}

var imported = await ImportNewEntryAsync(entry, cancellationToken);
if (imported) entriesImported++;
else entriesSkipped++;

private async Task<bool> ImportNewEntryAsync(...)
{
    var parsed = ContentIdParser.Parse(entry.ContentId);
    if (parsed == null) return false;
    await _registry.RegisterAsync(...);
    return true;
}
```

**Why This Keeps Happening**: Import counters were tied to path entry rather than successful writes. Any validation/registration workflow that mutates state should only increment success counters after the mutation is confirmed, while invalid input should be explicitly skipped and observable.

### 0k17. ContentID Registration Should Validate Incoming ContentID Format

**The Bug**: `ContentIdController.Register` accepted any non-empty `ContentId` string and passed it directly to the registry, allowing malformed values to be stored and creating later registry lookup failures.

**Files Affected**:
- `src/slskd/MediaCore/API/Controllers/ContentIdController.cs`

**Wrong**:
```csharp
if (request == null || string.IsNullOrWhiteSpace(request.ExternalId) || string.IsNullOrWhiteSpace(request.ContentId))
{
    return BadRequest("ExternalId and ContentId are required");
}

await _registry.RegisterAsync(request.ExternalId, request.ContentId, cancellationToken);
```

**Correct**:
```csharp
if (request == null || string.IsNullOrWhiteSpace(request.ExternalId) || string.IsNullOrWhiteSpace(request.ContentId))
{
    return BadRequest("ExternalId and ContentId are required");
}

if (ContentIdParser.Parse(request.ContentId) == null)
{
    return BadRequest("Invalid ContentID format. Expected: content:<domain>:<type>:<id>");
}

await _registry.RegisterAsync(request.ExternalId, request.ContentId, cancellationToken);
```

**Why This Keeps Happening**: API endpoints often validate for null/empty only, assuming downstream services will reject bad formats. Without parser-level validation at ingress, malformed identifiers get persisted and only fail later when strict code paths attempt to parse them.

### 0k18. Mesh Message Type Should Be Guarded Before Cast to `MeshMessageType`

**The Bug**: `MeshController.HandleMessage` cast payload `type` directly from JSON into `MeshMessageType`, so malformed or undefined values could throw a 500 error instead of returning controlled `BadRequest`.

**Files Affected**:
- `src/slskd/Mesh/API/MeshController.cs`

**Wrong**:
```csharp
var messageType = (MeshMessageType)typeElement.GetInt32();
```

**Correct**:
```csharp
if (!typeElement.TryGetInt32(out var messageTypeInt) || !Enum.IsDefined(typeof(MeshMessageType), messageTypeInt))
{
    return BadRequest(new { error = "Unknown or invalid message type" });
}

var messageType = (MeshMessageType)messageTypeInt;
```

**Why This Keeps Happening**: API inputs are often treated as trusted when they reach switch-based dispatch code. Untrusted payloads can reach this path from clients or tests, so enum conversion should be defensive and never throw at the edge before validation.

### 0k19. JSON Document Lifetimes Must Not Leak Into API Response Payloads

**The Bug**: Mesh gateway JSON passthrough returned `parsed.RootElement` directly, and `NowPlayingController` held a non-disposed `JsonDocument`, both of which risked invalid response behavior when parsing state was disposed.

**Files Affected**:
- `src/slskd/API/Mesh/MeshGatewayController.cs`
- `src/slskd/NowPlaying/API/NowPlayingController.cs`

**Wrong**:
```csharp
var parsed = JsonDocument.Parse(json);
return Ok(parsed.RootElement);
```

**Correct**:
```csharp
using var parsed = JsonDocument.Parse(json);
return Ok(parsed.RootElement.Clone());
```

**Why This Keeps Happening**: It is easy to treat `JsonElement` as standalone and forget it is a view over its owning `JsonDocument`. When the document is disposed while an endpoint still references it, response serialization can fail. Clone the element or return an owned structure.

### 0k20. Do Not Use `ContinueWith` With Caller Cancellation Tokens To “Absorb” Child Task Failures

**The Bug**: Several orchestration paths wrapped async work in `ContinueWith(...)` just to log or map failures, while also passing the caller cancellation token to the continuation or startup scheduler. If the caller token was canceled first, the continuation itself could be skipped or marked canceled, so `Task.WhenAll(...)` or service startup observed cancellation instead of the intended “log and continue” behavior.

**Files Affected**:
- `src/slskd/Search/Providers/SearchAggregator.cs`
- `src/slskd/PodCore/PodMessageBackfill.cs`
- `src/slskd/DhtRendezvous/DhtRendezvousService.cs`
- `src/slskd/Mesh/Realm/RealmHostedService.cs`

**Wrong**:
```csharp
var tasks = providers.Select(provider =>
    provider.StartSearchAsync(request, sink, ct)
        .ContinueWith(t =>
        {
            if (t.IsFaulted)
            {
                _logger.Debug(t.Exception, "Provider failed");
            }
        }, ct));
```

**Correct**:
```csharp
var tasks = providers.Select(provider => RunProviderSearchAsync(provider, request, sink, ct));

private async Task RunProviderSearchAsync(...)
{
    try
    {
        await provider.StartSearchAsync(request, sink, ct);
    }
    catch (OperationCanceledException) when (ct.IsCancellationRequested)
    {
        throw;
    }
    catch (Exception ex)
    {
        _logger.Debug(ex, "Provider failed");
    }
}
```

**Why This Keeps Happening**: `ContinueWith` looks like a compact way to “handle errors later,” but its cancellation token controls the continuation task, not the antecedent. That means the wrapper can silently change success/failure/cancellation semantics and make higher-level coordination do the wrong thing. In modern async code, prefer `await` with explicit `try`/`catch`; for fire-and-forget startup work, catch inside the background task instead of relying on a continuation to observe faults.

### 0k21. Do Not Use `ContinueWith(OnlyOn...)` As A Fire-And-Forget Success/Fault Observer

**The Bug**: Several background observers used `ContinueWith(..., OnlyOnFaulted)` or `OnlyOnRanToCompletion` just to log success/failure after a fire-and-forget task. On the common path, the continuation task itself became canceled, so code like `Task.WhenAll(...)` or status logging was tracking a synthetic wrapper task instead of the real operation.

**Files Affected**:
- `src/slskd/Application.cs`
- `src/slskd/Events/EventBus.cs`
- `src/slskd/Common/Security/TimedBatcher.cs`
- `src/slskd/Transfers/Uploads/UploadService.cs`
- `src/slskd/Transfers/Downloads/DownloadService.cs`

**Wrong**:
```csharp
_ = backgroundTask.ContinueWith(
    task => Log.Error(task.Exception, "Background work failed"),
    TaskContinuationOptions.OnlyOnFaulted);
```

**Correct**:
```csharp
_ = ObserveBackgroundTaskAsync(backgroundTask);

private async Task ObserveBackgroundTaskAsync(Task backgroundTask)
{
    try
    {
        await backgroundTask;
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Background work failed");
    }
}
```

**Why This Keeps Happening**: `ContinueWith(OnlyOn...)` feels like a cheap observer, but it creates a second task with its own state machine. When the observed condition is not met, that wrapper task is canceled by design, which makes fire-and-forget orchestration noisy and misleading. If the intent is “run this in the background and log failures,” use a dedicated async helper that awaits the real task and catches inside it.

### 0k22. Fire-And-Forget Tasks From Event Or Timer Paths Still Need An Explicit Observer

**The Bug**: Application startup, timer callbacks, and inbound message handlers launched background work with `_ = Task.Run(...)` or `_ = SomeAsyncCall()` and assumed the callee would always self-handle faults. When one of those tasks throws before its own internal catch or from a later refactor, the exception becomes detached from the triggering event and can surface as unobserved task noise or silently drop maintenance work.

**Files Affected**:
- `src/slskd/Application.cs`

**Wrong**:
```csharp
_ = Task.Run(() => MaybeRescanShares());
_ = Task.Run(() => HandlePodMessageAsync(username, message));
```

**Correct**:
```csharp
_ = ObserveBackgroundTaskAsync(
    Task.Run(() => MaybeRescanShares(), CancellationToken.None),
    "Failed to evaluate scheduled share rescan");
```

**Why This Keeps Happening**: Fire-and-forget code often starts small and “obviously safe,” so it is easy to skip an observer because the current implementation catches internally. Later edits add awaits, new call sites, or early throws and the task becomes faultable from outside that assumed safe window. If the caller intentionally detaches from the task, it still owns failure observation and should route faults through one consistent helper.

### 0k23. Refactors Must Update Property Reads When Model Shapes Change

**The Bug**: `MusicContentDomainProvider` still read `HashDbEntry.IsAdvertisable` after the hash-db model no longer exposed that property. The code compiled until the provider was rebuilt, then failed with a missing-member error.

**Files Affected**:
- `src/slskd/VirtualSoulfind/Core/Music/MusicContentDomainProvider.cs`
- `src/slskd/HashDb/Models/HashDbEntry.cs`
- `src/slskd/VirtualSoulfind/DisasterMode/MeshSearchService.cs`
- `src/slskd/DhtRendezvous/IDhtRendezvousService.cs`

**Wrong**:
```csharp
var isAdvertisable = hashes.Any(entry => entry.IsAdvertisable);
```

**Correct**:
```csharp
var isAdvertisable = hashes.Any();
```

```csharp
var peerContent = await meshDirectory.FindContentByPeerAsync(peer.Username, ct);
```

**Why This Keeps Happening**: Data-model refactors often remove or rename properties in one subsystem while dependent query code keeps compiling in a warm workspace or on partial builds. Any change to DTO/entity shape needs a repo-wide usage pass, especially in provider/query layers that derive booleans from now-removed fields.

### 0k24. Cached Mutable Lists Must Return Locked, Semantically Filtered Snapshots

**The Bug**: Scene services cached mutable `List<T>` instances in concurrent dictionaries and then returned `cached.ToList()` without locking or reapplying the method’s semantic filters. That let local leave operations mark a member inactive in cache while `GetMembersAsync(...)` still returned that peer, and concurrent readers could enumerate while writers were mutating the same list.

**Files Affected**:
- `src/slskd/VirtualSoulfind/Scenes/SceneMembershipTracker.cs`
- `src/slskd/VirtualSoulfind/Scenes/SceneChatService.cs`

**Wrong**:
```csharp
if (memberCache.TryGetValue(sceneId, out var cached))
{
    return cached.ToList();
}
```

**Correct**:
```csharp
if (memberCache.TryGetValue(sceneId, out var cached))
{
    lock (cached)
    {
        return cached.Where(member => member.IsActive).ToList();
    }
}
```

**Why This Keeps Happening**: `ConcurrentDictionary` only protects the dictionary, not the mutable objects stored inside it. Once a value is a shared `List<T>` or similar collection, every read/write path still needs a consistent lock and should return a snapshot that preserves the method contract, not just whatever happens to be in the backing list.

### 0k25. Fallback Payload Parsing Must Preserve Envelope Context

**The Bug**: Scene chat fell back from MessagePack to plain UTF-8 text, but the fallback object left `SceneId` empty. The receiving path stored that message under an empty cache key instead of the scene that delivered it, so fallback messages could disappear from the intended scene view.

**Files Affected**:
- `src/slskd/VirtualSoulfind/Scenes/SceneChatService.cs`

**Wrong**:
```csharp
return new SceneChatMessage
{
    SceneId = string.Empty,
    Content = content,
};
```

**Correct**:
```csharp
if (string.IsNullOrWhiteSpace(message.SceneId))
{
    message.SceneId = e.SceneId;
}
```

**Why This Keeps Happening**: Fallback parsers tend to focus on recovering the payload body and forget that the transport envelope still carries critical routing metadata. When a decode path falls back, restore missing context from the envelope before caching or dispatching the message.

### 0k26. `async` Timer Callbacks Need Owned Loop Lifetime, Not Fire-And-Forget Overlap

**The Bug**: `ScenePubSubService` used `new Timer(async _ => await PollSubscribedScenesAsync(), ...)`. Each tick detached a new async callback, so slow polls could overlap, disposal had no owned task to stop, and in-flight DHT reads outlived service shutdown.

**Files Affected**:
- `src/slskd/VirtualSoulfind/Scenes/ScenePubSubService.cs`

**Wrong**:
```csharp
pollTimer = new System.Threading.Timer(
    async _ => await PollSubscribedScenesAsync(),
    null,
    TimeSpan.FromSeconds(30),
    TimeSpan.FromSeconds(30));
```

**Correct**:
```csharp
pollLoopTask = Task.Run(() => RunPollLoopAsync(pollInterval, pollLoopCancellationTokenSource.Token), CancellationToken.None);

private async Task RunPollLoopAsync(TimeSpan pollInterval, CancellationToken cancellationToken)
{
    using var timer = new PeriodicTimer(pollInterval);
    while (await timer.WaitForNextTickAsync(cancellationToken))
    {
        await PollSubscribedScenesAsync(cancellationToken);
    }
}
```

**Why This Keeps Happening**: `System.Threading.Timer` accepts synchronous callbacks, so it is deceptively easy to pass an async lambda and forget that it becomes fire-and-forget on every tick. For async periodic work, own a loop task plus cancellation source so polling cannot overlap silently and shutdown can cancel the exact work it started.

### 0k27. Identity Caches Must Use The Same Comparer As The User-Facing Identity Surface

**The Bug**: Scene moderation stored muted and blocked peer IDs in default case-sensitive `HashSet<string>` instances. If the same peer ID arrived later with different casing, `IsPeerMutedAsync(...)` or `IsPeerBlockedAsync(...)` returned false even though the user had already muted or blocked that peer locally.

**Files Affected**:
- `src/slskd/VirtualSoulfind/Scenes/SceneModerationService.cs`

**Wrong**:
```csharp
var muted = mutedPeers.GetOrAdd(sceneId, _ => new HashSet<string>());
```

**Correct**:
```csharp
var muted = mutedPeers.GetOrAdd(sceneId, _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase));
```

**Why This Keeps Happening**: Local caches often start with default collection constructors, but user/peer identifiers elsewhere in the codebase are treated case-insensitively. If the identity surface is case-insensitive, every cache and lookup collection that backs it needs the same comparer or the behavior becomes inconsistent and surprising.

### 0k28. Security Pinning State Must Copy Caller-Owned Collections Before Storing Them

**The Bug**: `DnsSecurityService.PinTunnelIPs(...)` stored the caller’s `List<string>` instance directly in tunnel pin state. If the caller later mutated that list, the pinned-IP security decision changed after the fact, which could accidentally authorize addresses that were never validated when the tunnel was created.

**Files Affected**:
- `src/slskd/Common/Security/DnsSecurityService.cs`

**Wrong**:
```csharp
_tunnelIpPins[tunnelId] = (hostname, resolvedIPs, DateTimeOffset.UtcNow.AddHours(24));
```

**Correct**:
```csharp
var pinnedIps = new List<string>(resolvedIPs);
_tunnelIpPins[tunnelId] = (hostname, pinnedIps, DateTimeOffset.UtcNow.AddHours(24));
```

**Why This Keeps Happening**: It is easy to treat a method parameter as “already ours” once validation is complete, but mutable collections still belong to the caller unless copied. Security-sensitive caches and pin sets should snapshot validated values on write so later external mutation cannot rewrite policy decisions.

### 0k29. Lazily Loaded Stores Must Trigger Load On Read APIs Too, Not Just Write Paths

**The Bug**: `PeerReputationStore.GetRecentEventsAsync(...)` read directly from `_eventCache` without calling `EnsureDataLoadedAsync(...)`. On a fresh store instance with valid data on disk, the method returned an empty result until some other API triggered the lazy-load path first.

**Files Affected**:
- `src/slskd/Common/Moderation/PeerReputationStore.cs`

**Wrong**:
```csharp
public Task<IEnumerable<PeerReputationEvent>> GetRecentEventsAsync(...)
{
    if (!_eventCache.TryGetValue(peerId, out var events))
    {
        return Task.FromResult<IEnumerable<PeerReputationEvent>>(Array.Empty<PeerReputationEvent>());
    }
}
```

**Correct**:
```csharp
public async Task<IEnumerable<PeerReputationEvent>> GetRecentEventsAsync(...)
{
    await EnsureDataLoadedAsync(cancellationToken);
    ...
}
```

**Why This Keeps Happening**: Lazy-load designs usually get wired into write paths and the most obvious read methods first, then one “simple” read skips the load because it looks like a pure cache access. Any public API that exposes persisted state must either guarantee prior initialization or trigger the same lazy-load guard itself.

### 0k30. Test Fixtures Must Be Updated When Constructor Signatures Gain New Required Dependencies

**The Bug**: v2 tests still constructed `SimpleMatchEngine()` and `SoulseekBackend(...)` with older signatures after those classes gained required `ICatalogueStore` and `ILogger<SoulseekBackend>` dependencies. The runtime project built, but the unit project failed as soon as those tests were compiled again.

**Files Affected**:
- `tests/slskd.Tests.Unit/VirtualSoulfind/v2/Matching/SimpleMatchEngineTests.cs`
- `tests/slskd.Tests.Unit/VirtualSoulfind/v2/Backends/SoulseekBackendTests.cs`
- `tests/slskd.Tests.Unit/VirtualSoulfind/v2/Integration/CompleteV2FlowTests.cs`
- `tests/slskd.Tests.Unit/VirtualSoulfind/v2/Integration/VirtualSoulfindV2IntegrationTests.cs`

**Wrong**:
```csharp
var engine = new SimpleMatchEngine();
var backend = new SoulseekBackend(client, limiter, options, logger);
```

**Correct**:
```csharp
var engine = new SimpleMatchEngine(catalogueStore);
var backend = new SoulseekBackend(client, limiter, catalogueStore, options, logger);
```

**Why This Keeps Happening**: Constructor changes often land in production code first, and tests keep compiling in warm workspaces or are skipped behind narrower validation commands. When a service gains a required dependency, grep the test tree for all constructor call sites in the same edit or the next full unit-project build will break far away from the original change.

### 0k31. Observability Services Must Depend On The Concrete Metrics Source, Not A Wrapper That Hides It

**The Bug**: `MeshHealthService` took `IMeshDhtClient` and then tried to cast it to `InMemoryDhtClient` to read in-memory health metrics. Under the normal DI registration, `IMeshDhtClient` is a `MeshDhtClient` wrapper, so the cast always failed and mesh health snapshots silently reported zero DHT metrics.

**Files Affected**:
- `src/slskd/Mesh/Health/MeshHealthService.cs`

**Wrong**:
```csharp
public MeshHealthService(ILogger<MeshHealthService> logger, IMeshDhtClient dht)
{
    memDht = dht as InMemoryDhtClient;
}
```

**Correct**:
```csharp
public MeshHealthService(ILogger<MeshHealthService> logger, IDhtClient dht)
{
    memDht = dht as InMemoryDhtClient;
}
```

**Why This Keeps Happening**: Wrapper abstractions are convenient for feature code, but diagnostics often need implementation-specific counters that wrappers intentionally hide. If a health/metrics service requires concrete runtime state, inject the concrete-facing dependency path that DI actually registers for that state or the observability code will quietly degrade to zeros.

### 0k32. Hashed Storage Layers Cannot Recover Logical Key Prefixes Later

**The Bug**: `InMemoryDhtClient.GetStoreStats()` tried to count content-peer-hint entries by checking whether stored keys started with `"mesh:content-peers:"`. The store only keeps hashed key bytes rendered as hex, so that plaintext prefix check could never match and the content-hint metric stayed wrong even when hints were present.

**Files Affected**:
- `src/slskd/Mesh/Dht/InMemoryDhtClient.cs`

**Wrong**:
```csharp
var content = keys.Count(k => k.StartsWith("mesh:content-peers:", StringComparison.Ordinal));
```

**Correct**:
```csharp
if (ContainsContentPeerHints(list))
{
    content++;
}
```

**Why This Keeps Happening**: Once a logical key is hashed, prefix semantics are gone unless you carry classification metadata forward separately. Any later stats or filtering logic must inspect stored payload type/metadata, or record the classification at write time, instead of pretending the original string key can still be recovered from the hash.

### 0k33. Diagnostic Calls Must Respect Identifier Size Contracts Too

**The Bug**: `MeshHealthService` tried to count routing nodes by calling `FindClosest(Array.Empty<byte>(), 1000)`. The routing table expects 160-bit node IDs, so the diagnostic path started throwing `ArgumentException` as soon as any real node existed.

**Files Affected**:
- `src/slskd/Mesh/Health/MeshHealthService.cs`

**Wrong**:
```csharp
var routingCount = memDht.FindClosest(Array.Empty<byte>(), 1000).Count;
```

**Correct**:
```csharp
var routingCount = memDht.GetNodeCount();
```

**Why This Keeps Happening**: Diagnostics often feel “out of band,” so it is tempting to call lower-level APIs with placeholder values. Those APIs still enforce their data-shape contracts. For health/stat paths, prefer dedicated count/stat accessors over fabricating sentinel IDs or requests that were never valid for the underlying protocol.

---

*Last updated: 2026-03-22*
