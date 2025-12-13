# Phase 6: Virtual Soulfind Mesh - Detailed Implementation Design

> **New Phase**: T-800 to T-840 (41 tasks)  
> **Branch**: `experimental/virtual-soulfind`  
> **Dependencies**: Phases 1-5 (MusicBrainz, Multi-swarm, DHT, Overlay)  
> **Estimated Duration**: 12-16 weeks

---

## Overview

The Virtual Soulfind Mesh transforms slskdn into a **truly decentralized music sharing network** that:

1. **Enhances Soulseek** when the server is available (hybrid mode)
2. **Replaces Soulseek** when the server is unavailable (disaster mode)
3. **Never requires** central servers or privileged nodes

This is a **peer-to-peer "virtual server"** where each slskdn instance contributes to:
- **Shadow index**: Decentralized MBID→peers mapping via DHT
- **Scenes**: Decentralized rooms/communities via DHT topics
- **Disaster resilience**: Mesh-only operation when Soulseek is down

---

## What Problem Does This Solve?

### The Soulseek Server Problem

**Risk**: The official Soulseek server is a single point of failure:
- If it shuts down, the entire network dies
- If you're banned, you lose access to everything
- Centralized control over a decentralized network

### The Virtual Soulfind Solution

**Decentralized Intelligence**:
- Each slskdn peer **observes** Soulseek traffic and **learns** what's available
- Peers **share** this knowledge via DHT (shadow index)
- When Soulseek dies, the mesh **already knows** who has what
- Transfers continue via overlay, guided by the shadow index

**Think of it as**: BitTorrent DHT + MBID awareness + multi-swarm = resilient music network

---

## Phase 6A: Capture & Normalization Pipeline (T-800 to T-804)

### Task T-800: Soulseek Traffic Observer

**Purpose**: Passively monitor Soulseek traffic to build knowledge graph.

#### Data Model

```csharp
namespace slskd.VirtualSoulfind.Capture
{
    /// <summary>
    /// Observed Soulseek search result (pre-normalization).
    /// </summary>
    public class SearchObservation
    {
        public string ObservationId { get; set; }
        public DateTimeOffset Timestamp { get; set; }
        
        // Search context
        public string Query { get; set; }
        public string SoulseekUsername { get; set; }
        
        // File details
        public string FilePath { get; set; }
        public long SizeBytes { get; set; }
        public int? BitRate { get; set; }
        public int? DurationSeconds { get; set; }
        public string Extension { get; set; }
        
        // Metadata extraction (best-effort from path)
        public string Artist { get; set; }
        public string Album { get; set; }
        public string Title { get; set; }
    }
    
    /// <summary>
    /// Observed completed transfer.
    /// </summary>
    public class TransferObservation
    {
        public string TransferId { get; set; }
        public DateTimeOffset CompletedAt { get; set; }
        
        public string SoulseekUsername { get; set; }
        public string FilePath { get; set; }
        public string LocalPath { get; set; }  // Where we saved it
        
        public long SizeBytes { get; set; }
        public TimeSpan Duration { get; set; }
        public double ThroughputBytesPerSec { get; set; }
        public bool Success { get; set; }
    }
}
```

#### Implementation

```csharp
namespace slskd.VirtualSoulfind.Capture
{
    public interface ITrafficObserver
    {
        /// <summary>
        /// Called when search results are received from Soulseek server.
        /// </summary>
        Task OnSearchResultsAsync(string query, SearchResponse response, CancellationToken ct = default);
        
        /// <summary>
        /// Called when a Soulseek transfer completes.
        /// </summary>
        Task OnTransferCompleteAsync(Transfer transfer, CancellationToken ct = default);
    }
    
    public class TrafficObserver : ITrafficObserver
    {
        private readonly ILogger<TrafficObserver> log;
        private readonly INormalizationPipeline normalization;
        
        public async Task OnSearchResultsAsync(string query, SearchResponse response, CancellationToken ct)
        {
            foreach (var user in response.Responses)
            {
                foreach (var file in user.Files)
                {
                    var observation = new SearchObservation
                    {
                        ObservationId = Ulid.NewUlid().ToString(),
                        Timestamp = DateTimeOffset.UtcNow,
                        Query = query,
                        SoulseekUsername = user.Username,
                        FilePath = file.Filename,
                        SizeBytes = file.Size,
                        BitRate = file.BitRate,
                        DurationSeconds = file.Length,
                        Extension = Path.GetExtension(file.Filename)
                    };
                    
                    // Extract metadata from path (heuristic)
                    ExtractMetadataFromPath(observation);
                    
                    // Send to normalization pipeline
                    await normalization.ProcessSearchObservationAsync(observation, ct);
                }
            }
        }
        
        public async Task OnTransferCompleteAsync(Transfer transfer, CancellationToken ct)
        {
            if (transfer.State != TransferStates.Completed) return;
            
            var observation = new TransferObservation
            {
                TransferId = transfer.Id,
                CompletedAt = DateTimeOffset.UtcNow,
                SoulseekUsername = transfer.Username,
                FilePath = transfer.Filename,
                LocalPath = Path.Combine(transfer.DestinationDirectory, transfer.DestinationFilename),
                SizeBytes = transfer.Size,
                Duration = transfer.ElapsedTime ?? TimeSpan.Zero,
                ThroughputBytesPerSec = transfer.AverageSpeed ?? 0,
                Success = true
            };
            
            // Send to normalization (includes fingerprinting)
            await normalization.ProcessTransferObservationAsync(observation, ct);
        }
        
        private void ExtractMetadataFromPath(SearchObservation obs)
        {
            // Heuristic parsing of "Artist - Album/Track.flac" style paths
            // This is best-effort; real metadata comes from fingerprinting
            var parts = obs.FilePath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            
            if (parts.Length >= 2)
            {
                // Common pattern: "Artist/Album/Track.ext"
                obs.Artist = parts[0];
                obs.Album = parts.Length > 2 ? parts[1] : null;
                obs.Title = Path.GetFileNameWithoutExtension(parts[^1]);
            }
        }
    }
}
```

#### Integration Points

```csharp
// In SearchService.cs
public async Task<SearchResponse> SearchAsync(SearchOptions options, CancellationToken ct)
{
    var response = await soulseek.SearchAsync(options.Query, ct);
    
    // Notify observer
    await trafficObserver.OnSearchResultsAsync(options.Query, response, ct);
    
    return response;
}

// In TransferService.cs
private async Task OnTransferCompletedAsync(Transfer transfer)
{
    // Existing completion logic...
    
    // Notify observer
    await trafficObserver.OnTransferCompleteAsync(transfer, CancellationToken.None);
}
```

#### Implementation Checklist

- [ ] Define `SearchObservation` and `TransferObservation` models
- [ ] Implement `ITrafficObserver` interface
- [ ] Add path metadata extraction heuristics
- [ ] Integrate with `SearchService` (hook search results)
- [ ] Integrate with `TransferService` (hook completions)
- [ ] Add database schema for raw observations (optional, for debugging)
- [ ] Add configuration toggle: `mesh.capture.enabled`
- [ ] Add unit tests for metadata extraction
- [ ] Add integration test with mock search/transfer

---

### Task T-801: MBID Normalization Pipeline

**Purpose**: Convert observations into MB-aware `AudioVariant` records.

#### Implementation

```csharp
namespace slskd.VirtualSoulfind.Capture
{
    public interface INormalizationPipeline
    {
        Task ProcessSearchObservationAsync(SearchObservation obs, CancellationToken ct = default);
        Task ProcessTransferObservationAsync(TransferObservation obs, CancellationToken ct = default);
    }
    
    public class NormalizationPipeline : INormalizationPipeline
    {
        private readonly IFingerprintExtractionService fingerprinting;
        private readonly IAcoustIdClient acoustId;
        private readonly IMusicBrainzClient musicBrainz;
        private readonly IShadowIndexBuilder shadowIndex;
        
        public async Task ProcessSearchObservationAsync(SearchObservation obs, CancellationToken ct)
        {
            // For search results, we only have path + heuristic metadata
            // Can't fingerprint without the file, so we do best-effort MB lookup
            
            if (string.IsNullOrEmpty(obs.Artist) || string.IsNullOrEmpty(obs.Title))
            {
                return;  // Not enough metadata
            }
            
            // Query MusicBrainz by artist + title
            var mbResults = await musicBrainz.SearchRecordingAsync(obs.Artist, obs.Title, ct);
            
            if (mbResults.Count == 0)
            {
                log.Debug("[NORM] No MB matches for {Artist} - {Title}", obs.Artist, obs.Title);
                return;
            }
            
            // Take best match (first result, typically highest score)
            var recording = mbResults.First();
            
            // Create provisional variant entry
            var variant = new AudioVariant
            {
                VariantId = Ulid.NewUlid().ToString(),
                MusicBrainzRecordingId = recording.Id,
                
                // Technical properties (from Soulseek metadata)
                Codec = GuessCodecFromExtension(obs.Extension),
                Container = obs.Extension?.TrimStart('.').ToUpperInvariant(),
                BitrateKbps = obs.BitRate ?? 0,
                DurationMs = (obs.DurationSeconds ?? 0) * 1000,
                FileSizeBytes = obs.SizeBytes,
                
                // Placeholder quality (will be refined if we download this file)
                QualityScore = 0.5,  // Unknown
                TranscodeSuspect = false,
                
                FirstSeenAt = obs.Timestamp,
                LastSeenAt = obs.Timestamp,
                SeenCount = 1
            };
            
            // Feed to shadow index
            await shadowIndex.AddVariantObservationAsync(
                obs.SoulseekUsername,
                recording.Id,
                variant,
                ct);
        }
        
        public async Task ProcessTransferObservationAsync(TransferObservation obs, CancellationToken ct)
        {
            if (!obs.Success) return;
            if (!File.Exists(obs.LocalPath)) return;
            
            // We have the actual file! Extract fingerprint
            var fingerprint = await fingerprinting.ExtractFingerprintAsync(obs.LocalPath, ct);
            
            if (fingerprint == null)
            {
                log.Warning("[NORM] Failed to fingerprint {Path}", obs.LocalPath);
                return;
            }
            
            // Resolve MusicBrainz Recording ID via AcoustID
            var acoustIdResult = await acoustId.LookupAsync(
                fingerprint.Fingerprint,
                fingerprint.SampleRate,
                fingerprint.DurationSeconds,
                ct);
            
            if (acoustIdResult?.Recordings == null || acoustIdResult.Recordings.Count == 0)
            {
                log.Warning("[NORM] No AcoustID match for {Path}", obs.LocalPath);
                return;
            }
            
            var recordingId = acoustIdResult.Recordings.First().Id;
            
            // Build full AudioVariant with quality scoring
            using var tagFile = TagLib.File.Create(obs.LocalPath);
            var props = tagFile.Properties;
            
            var variant = new AudioVariant
            {
                VariantId = Ulid.NewUlid().ToString(),
                MusicBrainzRecordingId = recordingId,
                
                // Accurate technical properties
                Codec = props.Description,
                Container = Path.GetExtension(obs.LocalPath).TrimStart('.').ToUpperInvariant(),
                SampleRateHz = props.AudioSampleRate,
                BitDepth = props.BitsPerSample,
                Channels = props.AudioChannels,
                BitrateKbps = props.AudioBitrate,
                DurationMs = (int)props.Duration.TotalMilliseconds,
                FileSizeBytes = obs.SizeBytes,
                
                AudioFingerprint = fingerprint.Fingerprint,
                FileSha256 = await ComputeFileSha256Async(obs.LocalPath, ct),
                
                FirstSeenAt = obs.CompletedAt,
                LastSeenAt = obs.CompletedAt,
                SeenCount = 1
            };
            
            // Compute quality score
            var scorer = new QualityScorer();
            variant.QualityScore = scorer.ComputeQualityScore(variant);
            
            // Detect transcodes
            var detector = new TranscodeDetector();
            var (isSuspect, reason) = detector.DetectTranscode(variant);
            variant.TranscodeSuspect = isSuspect;
            variant.TranscodeReason = reason;
            
            // Feed to shadow index
            await shadowIndex.AddVariantObservationAsync(
                obs.SoulseekUsername,
                recordingId,
                variant,
                ct);
        }
    }
}
```

#### Implementation Checklist

- [ ] Implement `INormalizationPipeline` interface
- [ ] Implement search observation processing (heuristic MB lookup)
- [ ] Implement transfer observation processing (fingerprinting)
- [ ] Integrate quality scoring from Phase 2
- [ ] Integrate transcode detection from Phase 2
- [ ] Add configuration for MB search strictness
- [ ] Add unit tests for normalization logic
- [ ] Add integration tests with sample files

---

### Tasks T-802 to T-804: Supporting Infrastructure

**T-802**: Username pseudonymization (map Soulseek username → overlay peer ID)  
**T-803**: Observation database schema (optional persistence for debugging)  
**T-804**: Privacy controls (anonymization settings, data retention)

---

## Phase 6B: Shadow Index Over DHT (T-805 to T-812)

### Task T-805: DHT Key Derivation

**Purpose**: Map MBIDs and scenes to DHT keys.

```csharp
namespace slskd.VirtualSoulfind.ShadowIndex
{
    public static class DhtKeyDerivation
    {
        private const string NAMESPACE_MBID_RELEASE = "slskdn-vsf-mbid-release-v1";
        private const string NAMESPACE_MBID_RECORDING = "slskdn-vsf-mbid-recording-v1";
        private const string NAMESPACE_SCENE = "slskdn-vsf-scene-v1";
        
        public static byte[] DeriveReleaseKey(string mbReleaseId)
        {
            return SHA1.HashData(Encoding.UTF8.GetBytes($"{NAMESPACE_MBID_RELEASE}:{mbReleaseId}"));
        }
        
        public static byte[] DeriveRecordingKey(string mbRecordingId)
        {
            return SHA1.HashData(Encoding.UTF8.GetBytes($"{NAMESPACE_MBID_RECORDING}:{mbRecordingId}"));
        }
        
        public static byte[] DeriveSceneKey(string sceneId)
        {
            return SHA1.HashData(Encoding.UTF8.GetBytes($"{NAMESPACE_SCENE}:{sceneId}"));
        }
    }
}
```

---

### Task T-806: Shadow Index Shard Format

**Purpose**: Define compact DHT value format.

```csharp
namespace slskd.VirtualSoulfind.ShadowIndex
{
    /// <summary>
    /// Compact shadow index shard (stored in DHT).
    /// </summary>
    public class ShadowIndexShard
    {
        public string ShardVersion { get; set; } = "1.0";
        public DateTimeOffset Timestamp { get; set; }
        public int TTLSeconds { get; set; } = 3600;  // 1 hour default
        
        // Compact peer set (hashed overlay IDs, first 8 bytes)
        public List<byte[]> PeerIdHints { get; set; }  // Each 8 bytes
        
        // Canonical variant hints
        public List<VariantHint> CanonicalVariants { get; set; }
        
        public int ApproximatePeerCount { get; set; }
    }
    
    public class VariantHint
    {
        public string Codec { get; set; }  // "FLAC", "MP3"
        public int BitrateKbps { get; set; }
        public long SizeBytes { get; set; }
        public byte[] HashPrefix { get; set; }  // First 16 bytes of SHA256
    }
}
```

**Serialization**: Use MessagePack or Protocol Buffers for compactness.

---

### Task T-807: Shadow Index Builder

**Purpose**: Aggregate observations into shards.

```csharp
public interface IShadowIndexBuilder
{
    Task AddVariantObservationAsync(string username, string recordingId, AudioVariant variant, CancellationToken ct = default);
    Task<ShadowIndexShard> BuildShardAsync(string mbid, CancellationToken ct = default);
}
```

---

### Tasks T-808 to T-812: DHT Publishing & Querying

**T-808**: Shard publisher (periodic background task)  
**T-809**: DHT query interface (resolve MBID → peer hints)  
**T-810**: Shard merging logic (combine shards from multiple peers)  
**T-811**: TTL and eviction policy  
**T-812**: Rate limiting for DHT writes

---

## Phase 6C: Scenes / Micro-Networks (T-813 to T-820)

### Task T-813: Scene Management Service

```csharp
namespace slskd.VirtualSoulfind.Scenes
{
    public interface ISceneService
    {
        Task<List<Scene>> GetJoinedScenesAsync(CancellationToken ct = default);
        Task JoinSceneAsync(string sceneId, CancellationToken ct = default);
        Task LeaveSceneAsync(string sceneId, CancellationToken ct = default);
        Task<SceneMetadata> GetSceneMetadataAsync(string sceneId, CancellationToken ct = default);
    }
    
    public class Scene
    {
        public string SceneId { get; set; }
        public SceneType Type { get; set; }
        public string DisplayName { get; set; }
        public int MemberCount { get; set; }
        public DateTimeOffset JoinedAt { get; set; }
    }
    
    public enum SceneType
    {
        Label,       // e.g., "scene:label:warp-records"
        Genre,       // e.g., "scene:genre:dub-techno"
        Private      // e.g., "scene:key:<pubkey>:friends"
    }
}
```

---

### Tasks T-814 to T-820: Scene Infrastructure

**T-814**: Scene DHT announcements  
**T-815**: Scene membership tracking  
**T-816**: Overlay pubsub for scene gossip  
**T-817**: Scene-scoped job creation (label crate from scene)  
**T-818**: Scene UI (list, join, leave)  
**T-819**: Scene chat (optional, overlay pubsub messages)  
**T-820**: Scene moderation (local mute/block)

---

## Phase 6D: Disaster Mode & Failover (T-821 to T-830)

### Task T-821: Soulseek Health Monitor

```csharp
namespace slskd.VirtualSoulfind.DisasterMode
{
    public enum SoulseekHealth
    {
        Healthy,      // Connected and responsive
        Degraded,     // Slow or intermittent
        Unavailable   // Cannot connect or banned
    }
    
    public interface ISoulseekHealthMonitor
    {
        SoulseekHealth CurrentHealth { get; }
        Task StartMonitoringAsync(CancellationToken ct = default);
        event EventHandler<SoulseekHealth> HealthChanged;
    }
    
    public class SoulseekHealthMonitor : ISoulseekHealthMonitor
    {
        public SoulseekHealth CurrentHealth { get; private set; } = SoulseekHealth.Healthy;
        
        public async Task StartMonitoringAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                var health = await CheckHealthAsync(ct);
                
                if (health != CurrentHealth)
                {
                    log.Warning("[HEALTH] Soulseek health changed: {Old} → {New}",
                        CurrentHealth, health);
                    CurrentHealth = health;
                    HealthChanged?.Invoke(this, health);
                }
                
                await Task.Delay(TimeSpan.FromSeconds(30), ct);
            }
        }
        
        private async Task<SoulseekHealth> CheckHealthAsync(CancellationToken ct)
        {
            if (soulseek.State != SoulseekClientStates.Connected)
            {
                // Try to reconnect
                try
                {
                    await soulseek.ConnectAsync(ct);
                }
                catch (SoulseekClientException ex) when (ex.Message.Contains("banned"))
                {
                    return SoulseekHealth.Unavailable;  // Banned
                }
                catch
                {
                    return SoulseekHealth.Unavailable;  // Can't connect
                }
            }
            
            // Check responsiveness with ping/pong
            try
            {
                await soulseek.PingAsync(TimeSpan.FromSeconds(5), ct);
                return SoulseekHealth.Healthy;
            }
            catch (TimeoutException)
            {
                return SoulseekHealth.Degraded;
            }
        }
    }
}
```

---

### Task T-822: Disaster Mode Coordinator

```csharp
public interface IDisasterModeCoordinator
{
    bool IsDisasterModeActive { get; }
    Task ActivateDisasterModeAsync(CancellationToken ct = default);
    Task DeactivateDisasterModeAsync(CancellationToken ct = default);
}
```

When disaster mode activates:
1. Disable Soulseek search/transfer paths
2. Switch all resolvers to DHT + overlay only
3. Show UI indicator
4. Emit telemetry event

---

### Tasks T-823 to T-830: Disaster Mode Features

**T-823**: Mesh-only search (MBID → DHT → peers)  
**T-824**: Mesh-only transfers (overlay multi-swarm only)  
**T-825**: Scene-based peer discovery (fallback when DHT sparse)  
**T-826**: Disaster mode UI indicator  
**T-827**: Configuration: auto vs forced disaster mode  
**T-828**: Graceful degradation (partial Soulseek availability)  
**T-829**: Disaster mode telemetry  
**T-830**: Recovery logic (re-enable Soulseek when healthy)

---

## Phase 6E: Integration & Polish (T-831 to T-840)

**T-831**: Integrate shadow index with existing job resolvers  
**T-832**: Integrate scenes with label crate jobs  
**T-833**: Integrate disaster mode with rescue mode  
**T-834**: Privacy audit (ensure username anonymization)  
**T-835**: Performance optimization (DHT query caching)  
**T-836**: Configuration UI (mesh settings panel)  
**T-837**: Telemetry dashboard (shadow index stats, disaster events)  
**T-838**: Documentation (user guide for disaster mode)  
**T-839**: Integration tests (full disaster mode simulation)  
**T-840**: Load testing (DHT scalability, shard size limits)

---

## Configuration

```yaml
mesh:
  enabled: true
  
  capture:
    enabled: true
    anonymize_usernames: true
    retention_days: 30
  
  shadow_index:
    enabled: true
    publish_interval_minutes: 15
    shard_ttl_hours: 1
    max_shards_per_publish: 100
  
  scenes:
    enabled: true
    max_joined_scenes: 20
    enable_chat: false  # Opt-in for scene chat
  
  disaster_mode:
    auto: true  # Auto-detect and activate
    force: false  # Force mesh-only (for testing)
    unavailable_threshold_minutes: 10
  
  privacy:
    anonymize_usernames: true
    never_publish_paths: true
    dht_rate_limit_per_minute: 100
```

---

## Success Criteria

### Normal Mode
- ✅ Shadow index populated from Soulseek traffic
- ✅ Scenes created and joined via DHT
- ✅ MBID jobs enriched with shadow index hints
- ✅ Privacy maintained (no username leaks in DHT)

### Disaster Mode
- ✅ Auto-detection when Soulseek unreachable
- ✅ MBID jobs resolve via DHT + overlay only
- ✅ Transfers complete via overlay multi-swarm
- ✅ UI shows disaster mode indicator
- ✅ Recovery when Soulseek returns

---

## Estimated Timeline

- **Phase 6A** (Capture): 3 weeks (T-800 to T-804)
- **Phase 6B** (Shadow Index): 4 weeks (T-805 to T-812)
- **Phase 6C** (Scenes): 3 weeks (T-813 to T-820)
- **Phase 6D** (Disaster Mode): 4 weeks (T-821 to T-830)
- **Phase 6E** (Integration): 2 weeks (T-831 to T-840)

**Total**: 16 weeks (~4 months)

---

**This is the "killer feature" that makes slskdn truly revolutionary**: a decentralized music network that doesn't die when central servers do.

















