# Phase 3: Discovery, Reputation & Fairness - Detailed Design

> **Tasks**: T-500 to T-510 (11 tasks)  
> **Branch**: `experimental/brainz`  
> **Dependencies**: Phase 1 (MusicBrainz integration), Phase 2 (Canonical scoring, Swarm scheduling)  
> **Estimated Duration**: 8-10 weeks

---

## Overview

Phase 3 adds advanced discovery features (discographies, label crates), local-only peer reputation, and mesh-level fairness enforcement to ensure slskdn remains a net contributor to both Soulseek and overlay networks.

---

## Phase 3A: Release-Graph Guided Discovery (T-500 to T-502)

### Task T-500: MB Artist Release Graph Service

**Purpose**: Fetch and cache artist discographies from MusicBrainz for bulk download jobs.

#### Data Models

```csharp
namespace slskd.Integrations.MusicBrainz
{
    public class ArtistReleaseGraph
    {
        public string ArtistId { get; set; }
        public string Name { get; set; }
        public string SortName { get; set; }
        public List<ReleaseGroup> ReleaseGroups { get; set; }
        public DateTimeOffset CachedAt { get; set; }
        public DateTimeOffset? ExpiresAt { get; set; }
    }
    
    public class ReleaseGroup
    {
        public string ReleaseGroupId { get; set; }
        public string Title { get; set; }
        public ReleaseGroupType Type { get; set; }  // Album, EP, Single, etc.
        public string FirstReleaseDate { get; set; }
        public List<Release> Releases { get; set; }
    }
    
    public enum ReleaseGroupType
    {
        Album, Single, EP, Compilation, Soundtrack, Live, Remix, Other
    }
}
```

#### Database Schema

```sql
CREATE TABLE ArtistReleaseGraphs (
    artist_id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    cached_at INTEGER NOT NULL,
    expires_at INTEGER,
    json_data TEXT  -- Full graph serialized
);

CREATE TABLE ReleaseGroups (
    release_group_id TEXT PRIMARY KEY,
    artist_id TEXT NOT NULL,
    title TEXT NOT NULL,
    type TEXT NOT NULL,
    first_release_date TEXT,
    FOREIGN KEY (artist_id) REFERENCES ArtistReleaseGraphs(artist_id)
);

CREATE INDEX idx_rg_artist ON ReleaseGroups(artist_id);
CREATE INDEX idx_rg_type ON ReleaseGroups(type);
```

#### Implementation Checklist

- [ ] Define `ArtistReleaseGraph` and related models
- [ ] Create database schema + migration
- [ ] Implement `IArtistReleaseGraphService` interface
- [ ] Implement cache-first fetch logic (7-day cache)
- [ ] Implement MusicBrainz API integration (respect 1 req/sec limit)
- [ ] Implement artist search functionality
- [ ] Add unit tests for cache expiration
- [ ] Add integration tests with MB API

---

### Task T-501: Discography Profiles

**Purpose**: Define filters for selecting subsets of an artist's discography.

#### Profile Types

1. **Core Discography**: Studio albums only
2. **Extended Discography**: Studio albums + major EPs/live albums
3. **All Releases**: Everything (albums, EPs, singles, compilations)

#### Implementation

```csharp
namespace slskd.Integrations.MusicBrainz
{
    public enum DiscographyProfile
    {
        CoreDiscography,
        ExtendedDiscography,
        AllReleases
    }
    
    public class DiscographyProfileFilter
    {
        public bool IncludeAlbums { get; set; } = true;
        public bool IncludeEPs { get; set; } = false;
        public bool IncludeSingles { get; set; } = false;
        public bool IncludeCompilations { get; set; } = false;
        public bool IncludeLive { get; set; } = false;
        public int? MinYear { get; set; }
        public int? MaxYear { get; set; }
        public List<string> PreferredCountries { get; set; }  // e.g., ["US", "GB"]
        
        public static DiscographyProfileFilter FromProfile(DiscographyProfile profile)
        {
            return profile switch
            {
                DiscographyProfile.CoreDiscography => new()
                {
                    IncludeAlbums = true,
                    IncludeEPs = false,
                    IncludeSingles = false,
                    IncludeCompilations = false,
                    IncludeLive = false
                },
                DiscographyProfile.ExtendedDiscography => new()
                {
                    IncludeAlbums = true,
                    IncludeEPs = true,
                    IncludeSingles = false,
                    IncludeCompilations = false,
                    IncludeLive = true
                },
                DiscographyProfile.AllReleases => new()
                {
                    IncludeAlbums = true,
                    IncludeEPs = true,
                    IncludeSingles = true,
                    IncludeCompilations = true,
                    IncludeLive = true
                },
                _ => throw new ArgumentException($"Unknown profile: {profile}")
            };
        }
    }
    
    public interface IDiscographyProfileService
    {
        List<string> ApplyProfile(ArtistReleaseGraph graph, DiscographyProfileFilter filter);
        Task<List<string>> GetReleaseIdsForProfileAsync(string artistId, DiscographyProfile profile, CancellationToken ct = default);
    }
}
```

#### Implementation Checklist

- [ ] Define `DiscographyProfile` enum + `DiscographyProfileFilter`
- [ ] Implement `IDiscographyProfileService` interface
- [ ] Implement profile filter logic (type, date, country)
- [ ] Implement "best release" selection per release group
- [ ] Add configuration for custom profile definitions
- [ ] Add unit tests for profile filters
- [ ] Add integration tests with sample artist graphs

---

### Task T-502: Discography Job Type

**Purpose**: Create a job that downloads an entire artist discography by spawning MB Release sub-jobs.

#### Job Model

```csharp
namespace slskd.Jobs
{
    public class DiscographyJob
    {
        public string JobId { get; set; }
        public string ArtistId { get; set; }
        public string ArtistName { get; set; }
        public DiscographyProfile Profile { get; set; }
        public string TargetDirectory { get; set; }
        
        // Sub-jobs
        public List<string> ReleaseJobIds { get; set; } = new();
        
        // Progress
        public int TotalReleases { get; set; }
        public int CompletedReleases { get; set; }
        public int FailedReleases { get; set; }
        
        public JobStatus Status { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DownloadConstraints Constraints { get; set; }
    }
}
```

#### Database Schema

```sql
CREATE TABLE DiscographyJobs (
    job_id TEXT PRIMARY KEY,
    artist_id TEXT NOT NULL,
    artist_name TEXT NOT NULL,
    profile TEXT NOT NULL,
    target_directory TEXT NOT NULL,
    total_releases INTEGER DEFAULT 0,
    completed_releases INTEGER DEFAULT 0,
    failed_releases INTEGER DEFAULT 0,
    status TEXT DEFAULT 'pending',
    created_at INTEGER NOT NULL,
    constraints_json TEXT
);

CREATE TABLE DiscographyReleaseJobs (
    discography_job_id TEXT NOT NULL,
    release_job_id TEXT NOT NULL,
    release_id TEXT NOT NULL,
    status TEXT DEFAULT 'pending',
    PRIMARY KEY (discography_job_id, release_job_id)
);
```

#### Implementation Checklist

- [ ] Define `DiscographyJob` model + database schema
- [ ] Implement `IDiscographyJobService` interface
- [ ] Implement job planning (resolve discography → create sub-jobs)
- [ ] Subscribe to release job completion events
- [ ] Aggregate progress from sub-jobs
- [ ] Add API endpoint: `POST /api/jobs/discography`
- [ ] Add API endpoint: `GET /api/jobs/{id}` (generic for all job types)
- [ ] Add unit tests for job planning
- [ ] Add integration tests for job execution

---

## Phase 3B: Label Crate Mode (T-503 to T-504)

### Task T-503: Label Presence Aggregation

**Purpose**: Track which labels' releases are popular in the mesh overlay.

#### Data Model

```csharp
namespace slskd.Mesh
{
    public class LabelPresenceStats
    {
        public string LabelId { get; set; }
        public string LabelName { get; set; }
        public int ReleaseCount { get; set; }  // Total releases seen
        public int PeerCount { get; set; }      // How many peers have releases from this label
        public Dictionary<string, int> ReleasePopularity { get; set; }  // ReleaseId → peer count
        public DateTimeOffset LastUpdated { get; set; }
    }
}
```

#### Database Schema

```sql
CREATE TABLE LabelPresence (
    label_id TEXT PRIMARY KEY,
    label_name TEXT NOT NULL,
    release_count INTEGER DEFAULT 0,
    peer_count INTEGER DEFAULT 0,
    last_updated INTEGER NOT NULL
);

CREATE TABLE LabelReleasePopularity (
    label_id TEXT NOT NULL,
    release_id TEXT NOT NULL,
    peer_count INTEGER DEFAULT 0,
    PRIMARY KEY (label_id, release_id),
    FOREIGN KEY (label_id) REFERENCES LabelPresence(label_id)
);

CREATE INDEX idx_label_pop_release ON LabelReleasePopularity(release_id);
CREATE INDEX idx_label_pop_peers ON LabelReleasePopularity(peer_count DESC);
```

#### Implementation

```csharp
namespace slskd.Mesh
{
    public interface ILabelPresenceService
    {
        Task UpdateFromMeshAdvertsAsync(List<FingerprintBundleAdvert> adverts, CancellationToken ct = default);
        Task<LabelPresenceStats> GetLabelStatsAsync(string labelId, CancellationToken ct = default);
        Task<List<string>> GetTopReleasesForLabelAsync(string labelId, int limit, CancellationToken ct = default);
    }
    
    public class LabelPresenceService : ILabelPresenceService
    {
        // Extract label info from mesh adverts and update stats
        // Called periodically as mesh sync receives new adverts
        
        public async Task UpdateFromMeshAdvertsAsync(List<FingerprintBundleAdvert> adverts, CancellationToken ct)
        {
            // For each recording in adverts:
            // 1. Resolve MB Release ID
            // 2. Get release label info from MusicBrainz
            // 3. Increment peer count for (label, release)
        }
    }
}
```

#### Implementation Checklist

- [ ] Define `LabelPresenceStats` model
- [ ] Create database schema for label tracking
- [ ] Implement `ILabelPresenceService` interface
- [ ] Integrate with mesh sync to collect label data
- [ ] Implement label popularity computation
- [ ] Add background job to update stats from mesh
- [ ] Add API endpoint: `GET /api/labels/{labelId}/stats`
- [ ] Add unit tests for stats aggregation
- [ ] Add integration tests with mock mesh data

---

### Task T-504: Label Crate Job Type

**Purpose**: Download top N releases from a label based on popularity.

#### Job Model

```csharp
namespace slskd.Jobs
{
    public class LabelCrateJob
    {
        public string JobId { get; set; }
        public string LabelId { get; set; }
        public string LabelName { get; set; }
        public int LimitReleases { get; set; }
        public string TargetDirectory { get; set; }
        
        // Sub-jobs
        public List<string> ReleaseJobIds { get; set; } = new();
        
        // Progress (same as discography)
        public int TotalReleases { get; set; }
        public int CompletedReleases { get; set; }
        
        public JobStatus Status { get; set; }
        public DownloadConstraints Constraints { get; set; }
    }
}
```

#### Implementation

```csharp
namespace slskd.Jobs
{
    public interface ILabelCrateJobService
    {
        Task<string> CreateJobAsync(LabelCrateJobRequest request, CancellationToken ct = default);
        Task<LabelCrateJob> GetJobAsync(string jobId, CancellationToken ct = default);
    }
    
    public class LabelCrateJobService : ILabelCrateJobService
    {
        private readonly ILabelPresenceService labelPresence;
        private readonly IMusicBrainzJobService mbJobs;
        
        public async Task<string> CreateJobAsync(LabelCrateJobRequest request, CancellationToken ct)
        {
            // 1. Get top N releases from label by popularity
            var topReleases = await labelPresence.GetTopReleasesForLabelAsync(
                request.LabelId,
                request.LimitReleases,
                ct);
            
            // 2. Create MB Release sub-job for each
            var job = new LabelCrateJob
            {
                JobId = Ulid.NewUlid().ToString(),
                LabelId = request.LabelId,
                LabelName = request.LabelName,
                LimitReleases = request.LimitReleases,
                TargetDirectory = request.TargetDirectory,
                TotalReleases = topReleases.Count,
                Status = JobStatus.Running
            };
            
            foreach (var releaseId in topReleases)
            {
                var releaseJobId = await mbJobs.CreateReleaseJobAsync(new MBReleaseJobRequest
                {
                    ReleaseId = releaseId,
                    TargetDir = Path.Combine(request.TargetDirectory, SanitizeForPath(request.LabelName)),
                    Constraints = request.Constraints
                }, ct);
                
                job.ReleaseJobIds.Add(releaseJobId);
            }
            
            await SaveJobAsync(job, ct);
            return job.JobId;
        }
    }
}
```

#### Implementation Checklist

- [ ] Define `LabelCrateJob` model + database schema
- [ ] Implement `ILabelCrateJobService` interface
- [ ] Implement job creation with popularity-based selection
- [ ] Subscribe to release job completion events
- [ ] Add API endpoint: `POST /api/jobs/label-crate`
- [ ] Add unit tests for label crate job planning
- [ ] Add integration tests for job execution

---

## Phase 3C: Local-Only Peer Reputation (T-505 to T-507)

### Task T-505: Peer Reputation Metric Collection

**Purpose**: Track per-peer reliability metrics (strictly local, never shared).

#### Data Model

```csharp
namespace slskd.Mesh.Reputation
{
    public class PeerReputationMetrics
    {
        public string PeerId { get; set; }
        
        // Counters
        public int ChunksSuccessful { get; set; }
        public int ChunksFailed { get; set; }
        public int ChunksCorrupted { get; set; }  // Hash mismatch
        public int ChunksTimedOut { get; set; }
        public int PeerInitiatedCancellations { get; set; }
        
        // Computed rates
        public double SuccessRate => TotalChunks > 0 ? ChunksSuccessful / (double)TotalChunks : 1.0;
        public double CorruptionRate => TotalChunks > 0 ? ChunksCorrupted / (double)TotalChunks : 0.0;
        
        public int TotalChunks => ChunksSuccessful + ChunksFailed + ChunksCorrupted + ChunksTimedOut;
        
        // Temporal decay
        public DateTimeOffset FirstInteraction { get; set; }
        public DateTimeOffset LastInteraction { get; set; }
    }
}
```

#### Database Schema

```sql
CREATE TABLE PeerReputation (
    peer_id TEXT PRIMARY KEY,
    chunks_successful INTEGER DEFAULT 0,
    chunks_failed INTEGER DEFAULT 0,
    chunks_corrupted INTEGER DEFAULT 0,
    chunks_timed_out INTEGER DEFAULT 0,
    peer_initiated_cancellations INTEGER DEFAULT 0,
    first_interaction INTEGER NOT NULL,
    last_interaction INTEGER NOT NULL
);

CREATE INDEX idx_reputation_success ON PeerReputation(chunks_successful);
```

#### Implementation Checklist

- [ ] Define `PeerReputationMetrics` model
- [ ] Create database schema
- [ ] Implement metric collection hooks in chunk transfers
- [ ] Add temporal decay (older events weighted less)
- [ ] Ensure reputation is NEVER shared/published
- [ ] Add unit tests for metric updates
- [ ] Add integration tests with mock transfers

---

### Task T-506: Reputation Scoring Algorithm

**Purpose**: Compute 0-1 reputation score from metrics with temporal decay.

#### Algorithm

```csharp
namespace slskd.Mesh.Reputation
{
    public class ReputationScorer
    {
        // Weights
        public double SuccessWeight { get; set; } = 0.5;
        public double CorruptionPenalty { get; set; } = 0.3;
        public double TimeoutPenalty { get; set; } = 0.2;
        
        // Decay factor (newer interactions weighted more)
        public double DecayHalfLifeDays { get; set; } = 30;
        
        public double ComputeReputationScore(PeerReputationMetrics metrics)
        {
            if (metrics.TotalChunks == 0) return 0.5;  // Neutral for new peers
            
            // Base score from success rate
            double score = SuccessWeight * metrics.SuccessRate;
            
            // Penalties
            score -= CorruptionPenalty * metrics.CorruptionRate;
            score -= TimeoutPenalty * (metrics.ChunksTimedOut / (double)metrics.TotalChunks);
            
            // Temporal decay: reduce impact of old events
            var daysSinceLastInteraction = (DateTimeOffset.UtcNow - metrics.LastInteraction).TotalDays;
            var decayFactor = Math.Pow(0.5, daysSinceLastInteraction / DecayHalfLifeDays);
            
            score *= decayFactor;
            
            // Confidence boost for more data
            var confidence = Math.Min(1.0, metrics.TotalChunks / 100.0);  // Max confidence at 100 chunks
            score = (score * confidence) + (0.5 * (1 - confidence));  // Blend with neutral
            
            return Math.Clamp(score, 0.0, 1.0);
        }
    }
}
```

#### Implementation Checklist

- [ ] Implement `ReputationScorer` class
- [ ] Add temporal decay logic
- [ ] Add confidence adjustment for new peers
- [ ] Add configuration for weights + decay parameters
- [ ] Add unit tests for scoring algorithm
- [ ] Add tests for edge cases (new peer, old data)

---

### Task T-507: Scheduling Integration

**Purpose**: Use reputation scores in swarm scheduling to avoid bad peers.

#### Integration

```csharp
namespace slskd.Transfers.MultiSource
{
    public partial class SwarmScheduler
    {
        private readonly IReputationService reputation;
        
        public async Task<List<RankedPeer>> RankPeersWithReputationAsync(
            List<PeerPerformanceMetrics> peers,
            CancellationToken ct)
        {
            var rankedPeers = new List<RankedPeer>();
            
            foreach (var peer in peers)
            {
                // Get reputation score
                var repMetrics = await reputation.GetMetricsAsync(peer.PeerId, ct);
                var repScore = reputation.ComputeScore(repMetrics);
                
                // Compute combined cost (lower reputation = higher cost)
                var baseCost = costFunction.ComputeCost(peer);
                var reputationCost = (1.0 - repScore) * 5.0;  // 0-5 penalty
                
                rankedPeers.Add(new RankedPeer
                {
                    PeerId = peer.PeerId,
                    Metrics = peer,
                    ReputationScore = repScore,
                    Cost = baseCost + reputationCost
                });
            }
            
            return rankedPeers.OrderBy(p => p.Cost).ToList();
        }
        
        // Quarantine peers below threshold
        private bool ShouldQuarantinePeer(double reputationScore)
        {
            return reputationScore < 0.3;  // Configurable threshold
        }
    }
}
```

#### Implementation Checklist

- [ ] Extend `SwarmScheduler` with reputation integration
- [ ] Add reputation cost component to cost function
- [ ] Implement peer quarantine for low reputation
- [ ] Add configuration for reputation threshold
- [ ] Add logging for reputation-based decisions
- [ ] Add unit tests for reputation integration
- [ ] Add integration tests with mock reputation data

---

## Phase 3D: Mesh-Level Fairness Governor (T-508 to T-510)

### Task T-508: Traffic Accounting

**Purpose**: Track global upload/download volumes for Soulseek vs overlay.

#### Data Model

```csharp
namespace slskd.Mesh.Fairness
{
    public class TrafficStats
    {
        // Overlay mesh
        public long OverlayUploadBytes { get; set; }
        public long OverlayDownloadBytes { get; set; }
        
        // Soulseek network
        public long SoulseekUploadBytes { get; set; }
        public long SoulseekDownloadBytes { get; set; }
        
        // Time window
        public DateTimeOffset WindowStart { get; set; }
        public DateTimeOffset WindowEnd { get; set; }
        
        // Computed ratios
        public double OverlayUploadDownloadRatio =>
            OverlayDownloadBytes > 0 ? OverlayUploadBytes / (double)OverlayDownloadBytes : 0.0;
        
        public double OverlayToSoulseekUploadRatio =>
            SoulseekUploadBytes > 0 ? OverlayUploadBytes / (double)SoulseekUploadBytes : 0.0;
    }
}
```

#### Database Schema

```sql
CREATE TABLE TrafficStats (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    window_start INTEGER NOT NULL,
    window_end INTEGER NOT NULL,
    overlay_upload_bytes INTEGER DEFAULT 0,
    overlay_download_bytes INTEGER DEFAULT 0,
    soulseek_upload_bytes INTEGER DEFAULT 0,
    soulseek_download_bytes INTEGER DEFAULT 0
);

CREATE INDEX idx_traffic_window ON TrafficStats(window_start, window_end);
```

#### Implementation Checklist

- [ ] Define `TrafficStats` model
- [ ] Create database schema
- [ ] Hook into transfer completion events to update stats
- [ ] Implement rolling window (last 24h, last 7d, last 30d)
- [ ] Add API endpoint: `GET /api/mesh/traffic-stats`
- [ ] Add unit tests for stat aggregation
- [ ] Add integration tests with mock transfers

---

### Task T-509: Fairness Constraint Enforcement

**Purpose**: Enforce invariants to prevent leeching behavior.

#### Constraints

```yaml
mesh:
  fairness:
    enabled: true
    
    # Minimum overlay upload/download ratio (contribute at least this much)
    min_overlay_upload_download_ratio: 0.8  # Upload ≥ 80% of what you download
    
    # Maximum overlay-to-Soulseek upload ratio (don't become overlay-only node)
    max_overlay_to_soulseek_upload_ratio: 2.0  # Overlay uploads ≤ 2× Soulseek uploads
    
    # Actions on violation
    throttle_overlay_downloads: true
    pause_overlay_downloads: false  # More aggressive
```

#### Implementation

```csharp
namespace slskd.Mesh.Fairness
{
    public interface IFairnessGovernor
    {
        Task EnforceConstraintsAsync(CancellationToken ct = default);
        Task<FairnessStatus> GetStatusAsync(CancellationToken ct = default);
    }
    
    public class FairnessGovernor : IFairnessGovernor
    {
        private readonly ITrafficStatsService trafficStats;
        private readonly IMultiSourceDownloadService downloads;
        private readonly IOptionsMonitor<Options> options;
        
        public async Task EnforceConstraintsAsync(CancellationToken ct)
        {
            var config = options.CurrentValue.Mesh.Fairness;
            if (!config.Enabled) return;
            
            var stats = await trafficStats.GetStatsAsync(TimeSpan.FromHours(24), ct);
            
            // Check constraint 1: Overlay upload/download ratio
            if (stats.OverlayUploadDownloadRatio < config.MinOverlayUploadDownloadRatio)
            {
                log.Warning("[FAIRNESS] Overlay upload/download ratio {Ratio:F2} below minimum {Min:F2}",
                    stats.OverlayUploadDownloadRatio,
                    config.MinOverlayUploadDownloadRatio);
                
                if (config.ThrottleOverlayDownloads)
                {
                    await downloads.ThrottleOverlayDownloadsAsync(0.5, ct);  // 50% speed
                }
            }
            
            // Check constraint 2: Overlay-to-Soulseek upload ratio
            if (stats.OverlayToSoulseekUploadRatio > config.MaxOverlayToSoulseekUploadRatio)
            {
                log.Warning("[FAIRNESS] Overlay/Soulseek upload ratio {Ratio:F2} exceeds maximum {Max:F2}",
                    stats.OverlayToSoulseekUploadRatio,
                    config.MaxOverlayToSoulseekUploadRatio);
                
                // Increase preference for Soulseek sources
                await downloads.BoostSoulseekPreferenceAsync(ct);
            }
        }
    }
    
    public class FairnessStatus
    {
        public bool IsHealthy { get; set; }
        public List<FairnessViolation> Violations { get; set; }
        public TrafficStats Stats { get; set; }
    }
    
    public class FairnessViolation
    {
        public string Type { get; set; }  // "low_contribution" | "excessive_overlay"
        public string Message { get; set; }
        public string Action { get; set; }  // "throttling" | "boosting_soulseek"
    }
}
```

#### Implementation Checklist

- [ ] Implement `IFairnessGovernor` interface
- [ ] Implement constraint checking logic
- [ ] Implement throttling/preference adjustment actions
- [ ] Add background enforcement task (runs every 10 minutes)
- [ ] Add configuration options
- [ ] Add unit tests for constraint enforcement
- [ ] Add integration tests with mock traffic data

---

### Task T-510: Contribution Summary UI (Optional)

**Purpose**: Show users their contribution stats (informational).

#### UI Component

```jsx
// src/web/src/components/Mesh/ContributionSummary.jsx

const ContributionSummary = () => {
  const [stats, setStats] = useState(null);
  
  useEffect(() => {
    api.get('/api/mesh/traffic-stats?window=7d').then(setStats);
  }, []);
  
  if (!stats) return <Loader />;
  
  const totalUp = stats.overlay_upload_bytes + stats.soulseek_upload_bytes;
  const totalDown = stats.overlay_download_bytes + stats.soulseek_download_bytes;
  const ratio = totalUp / totalDown;
  
  return (
    <Segment>
      <Header as="h3">Contribution Report (Last 7 Days)</Header>
      
      <Statistic.Group widths="two">
        <Statistic>
          <Statistic.Value>{formatBytes(totalUp)}</Statistic.Value>
          <Statistic.Label>Uploaded</Statistic.Label>
        </Statistic>
        <Statistic>
          <Statistic.Value>{formatBytes(totalDown)}</Statistic.Value>
          <Statistic.Label>Downloaded</Statistic.Label>
        </Statistic>
      </Statistic.Group>
      
      <Divider />
      
      <Table basic="very">
        <Table.Body>
          <Table.Row>
            <Table.Cell>Soulseek Uploads</Table.Cell>
            <Table.Cell>{formatBytes(stats.soulseek_upload_bytes)}</Table.Cell>
          </Table.Row>
          <Table.Row>
            <Table.Cell>Overlay Uploads</Table.Cell>
            <Table.Cell>{formatBytes(stats.overlay_upload_bytes)}</Table.Cell>
          </Table.Row>
          <Table.Row>
            <Table.Cell>Soulseek Downloads</Table.Cell>
            <Table.Cell>{formatBytes(stats.soulseek_download_bytes)}</Table.Cell>
          </Table.Row>
          <Table.Row>
            <Table.Cell>Overlay Downloads</Table.Cell>
            <Table.Cell>{formatBytes(stats.overlay_download_bytes)}</Table.Cell>
          </Table.Row>
        </Table.Body>
      </Table>
      
      <Message positive={ratio > 1.5} warning={ratio < 1.0}>
        <Message.Header>Contribution Ratio: {ratio.toFixed(2)}:1</Message.Header>
        <p>
          {ratio > 1.5 && "✓ Excellent contributor! You've uploaded more than you've downloaded."}
          {ratio >= 1.0 && ratio <= 1.5 && "✓ Healthy contributor. You're giving back to the network."}
          {ratio < 1.0 && "⚠ Consider uploading more to maintain a healthy ecosystem."}
        </p>
      </Message>
    </Segment>
  );
};
```

#### Implementation Checklist

- [ ] Create React `ContributionSummary` component
- [ ] Add to Settings or Dashboard page
- [ ] Add API endpoint: `GET /api/mesh/traffic-stats`
- [ ] Add time window selector (24h, 7d, 30d)
- [ ] Add chart visualization (optional)
- [ ] Add E2E tests for UI

---

## Configuration Summary

```yaml
mesh:
  # Discovery
  artist_graph_cache_days: 7
  
  # Reputation (local only, never shared)
  reputation:
    enabled: true
    quarantine_threshold: 0.3
    decay_half_life_days: 30
  
  # Fairness
  fairness:
    enabled: true
    min_overlay_upload_download_ratio: 0.8
    max_overlay_to_soulseek_upload_ratio: 2.0
    throttle_overlay_downloads: true
    enforcement_interval_minutes: 10
```

---

## Testing Strategy

### Unit Tests
- Artist graph cache expiration
- Discography profile filtering
- Label popularity computation
- Reputation scoring with decay
- Fairness constraint checking

### Integration Tests
- Discography job creation + execution
- Label crate job with mock mesh data
- Reputation integration with swarm scheduler
- Fairness governor enforcement

---

**Phase 3 Complete!** Ready for Phases 4 and 5 planning.
