# Phase 4: Job Manifests, Session Traces & Advanced Features - Detailed Design

> **Tasks**: T-600 to T-611 (12 tasks)  
> **Branch**: `experimental/brainz`  
> **Dependencies**: Phases 1-3  
> **Estimated Duration**: 6-8 weeks

---

## Overview

Phase 4 adds operational features for power users: exportable job manifests, detailed session traces for debugging, warm cache nodes, and optional playback-aware swarming.

---

## Phase 4A: YAML Job Manifests (T-600 to T-602)

### Task T-600: Define YAML Job Manifest Schema

**Purpose**: Standardize job representation for export/import/resume.

#### Schema Definition

```yaml
# Example: MB Release Job Manifest
manifest_version: "1.0"
job_id: "01HRQ4GSX9K47J7M5FK7VW8B12"
job_type: "mb_release"
created_at: "2025-12-10T15:30:00Z"

spec:
  mb_release_id: "c0d0c0a4-4a26-4d74-9c02-67c9321b3b22"
  title: "Loveless"
  artist: "My Bloody Valentine"
  target_dir: "/music/downloads/queue"
  
  tracks:
    - position: 1
      title: "Only Shallow"
      mb_recording_id: "e2f5e9b4-5852-4cd3-b1f9-29a7a4a234bc"
      duration_ms: 242000
    - position: 2
      title: "Loomer"
      mb_recording_id: "4863d0b0-7920-4e1d-ba55-e00e39c6bdaa"
      duration_ms: 178000
    # ... more tracks
  
  constraints:
    preferred_codecs: ["FLAC"]
    allow_lossy: false
    prefer_canonical: true
    use_overlay: true
    overlay_bandwidth_kbps: 3000
    max_lossy_tracks_per_album: 0

status:
  state: "running"  # pending | running | completed | failed | cancelled
  started_at: "2025-12-10T15:30:15Z"
  completed_tracks: [1]
  in_progress_tracks: [2]
  pending_tracks: [3, 4, 5, 6, 7, 8, 9, 10, 11]
  bytes_total: 512000000
  bytes_done: 45000000
```

#### C# Models

```csharp
namespace slskd.Jobs.Manifests
{
    public class JobManifest
    {
        public string ManifestVersion { get; set; } = "1.0";
        public string JobId { get; set; }
        public JobType JobType { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        
        public object Spec { get; set; }  // Polymorphic based on JobType
        public JobManifestStatus Status { get; set; }
    }
    
    public enum JobType
    {
        MbRelease,
        Discography,
        LabelCrate,
        MultiSource  // Generic multi-source download
    }
    
    public class JobManifestStatus
    {
        public string State { get; set; }
        public DateTimeOffset? StartedAt { get; set; }
        public DateTimeOffset? CompletedAt { get; set; }
        public List<int> CompletedTracks { get; set; }
        public List<int> InProgressTracks { get; set; }
        public List<int> PendingTracks { get; set; }
        public long BytesTotal { get; set; }
        public long BytesDone { get; set; }
        public string ErrorMessage { get; set; }
    }
    
    // Spec types
    public class MbReleaseJobSpec
    {
        public string MbReleaseId { get; set; }
        public string Title { get; set; }
        public string Artist { get; set; }
        public string TargetDir { get; set; }
        public List<TrackSpec> Tracks { get; set; }
        public DownloadConstraints Constraints { get; set; }
    }
    
    public class TrackSpec
    {
        public int Position { get; set; }
        public string Title { get; set; }
        public string MbRecordingId { get; set; }
        public int DurationMs { get; set; }
    }
    
    public class DiscographyJobSpec
    {
        public string ArtistId { get; set; }
        public string ArtistName { get; set; }
        public string Profile { get; set; }  // "core" | "extended" | "all"
        public string TargetDir { get; set; }
        public DownloadConstraints Constraints { get; set; }
    }
}
```

#### Schema Validation

```csharp
namespace slskd.Jobs.Manifests
{
    public interface IJobManifestValidator
    {
        (bool isValid, List<string> errors) ValidateManifest(JobManifest manifest);
    }
    
    public class JobManifestValidator : IJobManifestValidator
    {
        public (bool isValid, List<string> errors) ValidateManifest(JobManifest manifest)
        {
            var errors = new List<string>();
            
            // Version check
            if (manifest.ManifestVersion != "1.0")
            {
                errors.Add($"Unsupported manifest version: {manifest.ManifestVersion}");
            }
            
            // Job ID format
            if (string.IsNullOrEmpty(manifest.JobId) || !Ulid.TryParse(manifest.JobId, out _))
            {
                errors.Add("Invalid job ID format");
            }
            
            // Type-specific validation
            switch (manifest.JobType)
            {
                case JobType.MbRelease:
                    ValidateMbReleaseSpec(manifest.Spec as MbReleaseJobSpec, errors);
                    break;
                case JobType.Discography:
                    ValidateDiscographySpec(manifest.Spec as DiscographyJobSpec, errors);
                    break;
                // ... more types
            }
            
            return (errors.Count == 0, errors);
        }
        
        private void ValidateMbReleaseSpec(MbReleaseJobSpec spec, List<string> errors)
        {
            if (spec == null)
            {
                errors.Add("Missing spec for mb_release job");
                return;
            }
            
            if (string.IsNullOrEmpty(spec.MbReleaseId))
                errors.Add("Missing mb_release_id");
            
            if (string.IsNullOrEmpty(spec.TargetDir))
                errors.Add("Missing target_dir");
            
            if (spec.Tracks == null || spec.Tracks.Count == 0)
                errors.Add("No tracks specified");
        }
    }
}
```

#### Implementation Checklist

- [ ] Define YAML schema documentation
- [ ] Define C# manifest models (`JobManifest`, spec types)
- [ ] Implement `IJobManifestValidator` interface
- [ ] Add version compatibility checking
- [ ] Add unit tests for schema validation
- [ ] Document manifest format in `docs/JOB_MANIFEST_SPEC.md`

---

### Task T-601: Implement Job Manifest Export

**Purpose**: Serialize active jobs to YAML files.

#### Implementation

```csharp
namespace slskd.Jobs.Manifests
{
    public interface IJobManifestService
    {
        Task<string> ExportJobAsync(string jobId, string outputPath, CancellationToken ct = default);
        Task<JobManifest> SerializeJobAsync(string jobId, CancellationToken ct = default);
    }
    
    public class JobManifestService : IJobManifestService
    {
        private readonly IJobService jobs;
        private readonly ILogger<JobManifestService> log;
        
        public async Task<string> ExportJobAsync(string jobId, string outputPath, CancellationToken ct)
        {
            var manifest = await SerializeJobAsync(jobId, ct);
            
            // Serialize to YAML
            var serializer = new SerializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();
            
            var yaml = serializer.Serialize(manifest);
            
            // Determine output path
            if (string.IsNullOrEmpty(outputPath))
            {
                outputPath = Path.Combine("jobs", "exported", $"{jobId}.yml");
            }
            
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            await File.WriteAllTextAsync(outputPath, yaml, ct);
            
            log.Information("[MANIFEST] Exported job {JobId} to {Path}", jobId, outputPath);
            
            return outputPath;
        }
        
        public async Task<JobManifest> SerializeJobAsync(string jobId, CancellationToken ct)
        {
            var job = await jobs.GetJobAsync(jobId, ct);
            
            if (job == null)
            {
                throw new NotFoundException($"Job {jobId} not found");
            }
            
            var manifest = new JobManifest
            {
                ManifestVersion = "1.0",
                JobId = job.JobId,
                JobType = job.Type,
                CreatedAt = job.CreatedAt
            };
            
            // Serialize spec based on job type
            manifest.Spec = job.Type switch
            {
                JobType.MbRelease => SerializeMbReleaseJob(job as MbReleaseJob),
                JobType.Discography => SerializeDiscographyJob(job as DiscographyJob),
                JobType.LabelCrate => SerializeLabelCrateJob(job as LabelCrateJob),
                _ => throw new NotSupportedException($"Job type {job.Type} not supported for export")
            };
            
            // Serialize status
            manifest.Status = new JobManifestStatus
            {
                State = job.Status.ToString().ToLowerInvariant(),
                StartedAt = job.StartedAt,
                CompletedAt = job.CompletedAt,
                CompletedTracks = job.CompletedTrackIndices?.ToList(),
                InProgressTracks = job.InProgressTrackIndices?.ToList(),
                PendingTracks = job.PendingTrackIndices?.ToList(),
                BytesTotal = job.BytesTotal,
                BytesDone = job.BytesDone,
                ErrorMessage = job.ErrorMessage
            };
            
            return manifest;
        }
        
        private MbReleaseJobSpec SerializeMbReleaseJob(MbReleaseJob job)
        {
            return new MbReleaseJobSpec
            {
                MbReleaseId = job.ReleaseId,
                Title = job.Title,
                Artist = job.Artist,
                TargetDir = job.TargetDirectory,
                Tracks = job.Tracks.Select(t => new TrackSpec
                {
                    Position = t.Position,
                    Title = t.Title,
                    MbRecordingId = t.RecordingId,
                    DurationMs = t.DurationMs
                }).ToList(),
                Constraints = job.Constraints
            };
        }
    }
}
```

#### Auto-Export on Job Creation

```csharp
// In job creation flow
public async Task<string> CreateMbReleaseJobAsync(MBReleaseJobRequest request, CancellationToken ct)
{
    var jobId = Ulid.NewUlid().ToString();
    
    // ... create job ...
    
    // Auto-export manifest to jobs/active/
    await manifestService.ExportJobAsync(jobId, $"jobs/active/{jobId}.yml", ct);
    
    return jobId;
}

// On job completion, move to jobs/completed/
eventBus.Subscribe<JobCompletedEvent>(async e =>
{
    var activePath = $"jobs/active/{e.JobId}.yml";
    var completedPath = $"jobs/completed/{e.JobId}.yml";
    
    if (File.Exists(activePath))
    {
        // Re-export with final status
        await manifestService.ExportJobAsync(e.JobId, completedPath, CancellationToken.None);
        File.Delete(activePath);
    }
});
```

#### Implementation Checklist

- [ ] Implement `IJobManifestService.ExportJobAsync()`
- [ ] Implement `SerializeJobAsync()` for all job types
- [ ] Add YamlDotNet serialization
- [ ] Auto-export on job creation to `jobs/active/`
- [ ] Auto-move to `jobs/completed/` on completion
- [ ] Add API endpoint: `POST /api/jobs/{id}/export`
- [ ] Add unit tests for serialization
- [ ] Add integration tests with sample jobs

---

### Task T-602: Build Job Manifest Import

**Purpose**: Create jobs from YAML manifests (resume/replay).

#### Implementation

```csharp
namespace slskd.Jobs.Manifests
{
    public partial class JobManifestService
    {
        public async Task<string> ImportJobAsync(string manifestPath, CancellationToken ct = default)
        {
            log.Information("[MANIFEST] Importing job from {Path}", manifestPath);
            
            // Load YAML
            var yaml = await File.ReadAllTextAsync(manifestPath, ct);
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();
            
            var manifest = deserializer.Deserialize<JobManifest>(yaml);
            
            // Validate
            var (isValid, errors) = validator.ValidateManifest(manifest);
            if (!isValid)
            {
                throw new InvalidOperationException($"Invalid manifest: {string.Join(", ", errors)}");
            }
            
            // Check for collision
            var existing = await jobs.GetJobAsync(manifest.JobId, ct);
            if (existing != null)
            {
                log.Warning("[MANIFEST] Job {JobId} already exists, generating new ID", manifest.JobId);
                manifest.JobId = Ulid.NewUlid().ToString();
            }
            
            // Create job from manifest
            var jobId = manifest.JobType switch
            {
                JobType.MbRelease => await ImportMbReleaseJobAsync(manifest, ct),
                JobType.Discography => await ImportDiscographyJobAsync(manifest, ct),
                JobType.LabelCrate => await ImportLabelCrateJobAsync(manifest, ct),
                _ => throw new NotSupportedException($"Job type {manifest.JobType} not supported for import")
            };
            
            log.Information("[MANIFEST] Imported job {JobId} from manifest", jobId);
            
            return jobId;
        }
        
        private async Task<string> ImportMbReleaseJobAsync(JobManifest manifest, CancellationToken ct)
        {
            var spec = manifest.Spec as MbReleaseJobSpec;
            
            var request = new MBReleaseJobRequest
            {
                ReleaseId = spec.MbReleaseId,
                TargetDir = spec.TargetDir,
                Tracks = spec.Tracks.Select(t => t.Position).ToList(),  // Or "all"
                Constraints = spec.Constraints
            };
            
            return await mbJobService.CreateReleaseJobAsync(request, ct);
        }
        
        public async Task<List<string>> ImportBatchAsync(string directory, CancellationToken ct = default)
        {
            var manifestFiles = Directory.GetFiles(directory, "*.yml");
            var importedJobIds = new List<string>();
            
            foreach (var file in manifestFiles)
            {
                try
                {
                    var jobId = await ImportJobAsync(file, ct);
                    importedJobIds.Add(jobId);
                }
                catch (Exception ex)
                {
                    log.Error(ex, "[MANIFEST] Failed to import {File}", file);
                }
            }
            
            return importedJobIds;
        }
    }
}
```

#### CLI Command

```bash
# Import single manifest
slskdn job import --manifest /path/to/job.yml

# Import batch
slskdn job import --directory /path/to/manifests/

# Resume from completed jobs
slskdn job resume --from /path/to/completed/
```

#### Implementation Checklist

- [ ] Implement `ImportJobAsync()` logic
- [ ] Implement collision handling (generate new ID)
- [ ] Implement batch import
- [ ] Add CLI commands for import/resume
- [ ] Add API endpoint: `POST /api/jobs/import`
- [ ] Add unit tests for deserialization
- [ ] Add integration tests for import flow

---

## Phase 4B: Session Traces / Swarm Debugging (T-603 to T-605)

### Task T-603: Define Swarm Event Model

**Purpose**: Structured event logging for post-mortem debugging.

#### Event Model

```csharp
namespace slskd.Transfers.Diagnostics
{
    public class SwarmEvent
    {
        public string EventId { get; set; }
        public DateTimeOffset Timestamp { get; set; }
        public SwarmEventType Type { get; set; }
        
        // Context
        public string JobId { get; set; }
        public string TrackId { get; set; }
        public string VariantId { get; set; }
        public string PeerId { get; set; }
        
        // Action details
        public string Action { get; set; }  // e.g., "chunk_request", "chunk_received"
        public PeerSource Source { get; set; }  // Soulseek | Overlay
        
        // Metrics
        public long? BytesTransferred { get; set; }
        public double? DurationMs { get; set; }
        public double? ThroughputBytesPerSec { get; set; }
        
        // Error info
        public bool IsError { get; set; }
        public string ErrorCode { get; set; }
        public string ErrorMessage { get; set; }
        
        // Metadata (JSON)
        public Dictionary<string, object> Metadata { get; set; }
    }
    
    public enum SwarmEventType
    {
        JobStart,
        JobComplete,
        
        TrackStart,
        TrackComplete,
        
        PeerDiscovered,
        PeerConnected,
        PeerDisconnected,
        
        ChunkRequested,
        ChunkReceived,
        ChunkFailed,
        ChunkTimedOut,
        
        RescueModeActivated,
        RescueModeDeactivated,
        
        SwarmRebalanced,
        
        Error
    }
}
```

#### Database Schema

```sql
CREATE TABLE SwarmEvents (
    event_id TEXT PRIMARY KEY,
    timestamp INTEGER NOT NULL,
    type TEXT NOT NULL,
    job_id TEXT NOT NULL,
    track_id TEXT,
    variant_id TEXT,
    peer_id TEXT,
    action TEXT,
    source TEXT,
    bytes_transferred INTEGER,
    duration_ms REAL,
    throughput_bytes_per_sec REAL,
    is_error BOOLEAN DEFAULT FALSE,
    error_code TEXT,
    error_message TEXT,
    metadata_json TEXT
);

CREATE INDEX idx_swarm_events_job ON SwarmEvents(job_id, timestamp);
CREATE INDEX idx_swarm_events_type ON SwarmEvents(type);
CREATE INDEX idx_swarm_events_peer ON SwarmEvents(peer_id);
```

#### Implementation Checklist

- [ ] Define `SwarmEvent` model + enum
- [ ] Create database schema
- [ ] Define event emission points in swarm scheduler
- [ ] Add configuration for event logging (enable/disable)
- [ ] Add unit tests for event creation

---

### Task T-604: Implement Event Persistence and Rotation

**Purpose**: Store events per job with configurable retention.

#### Service Implementation

```csharp
namespace slskd.Transfers.Diagnostics
{
    public interface ISwarmEventService
    {
        Task RecordEventAsync(SwarmEvent evt, CancellationToken ct = default);
        Task<List<SwarmEvent>> GetEventsForJobAsync(string jobId, CancellationToken ct = default);
        Task PurgeOldEventsAsync(CancellationToken ct = default);
    }
    
    public class SwarmEventService : ISwarmEventService
    {
        private readonly ILogger<SwarmEventService> log;
        private readonly IOptionsMonitor<Options> options;
        
        public async Task RecordEventAsync(SwarmEvent evt, CancellationToken ct)
        {
            evt.EventId = Ulid.NewUlid().ToString();
            evt.Timestamp = DateTimeOffset.UtcNow;
            
            // Persist to database
            await SaveEventToDbAsync(evt, ct);
            
            // Also write to log file for this job
            var logPath = $"logs/sessions/{evt.JobId}.log";
            Directory.CreateDirectory(Path.GetDirectoryName(logPath));
            
            var logLine = $"{evt.Timestamp:O}|{evt.Type}|{evt.Action}|{evt.PeerId}|{evt.BytesTransferred}|{evt.ErrorMessage}";
            await File.AppendAllLinesAsync(logPath, new[] { logLine }, ct);
        }
        
        public async Task PurgeOldEventsAsync(CancellationToken ct)
        {
            var config = options.CurrentValue.Diagnostics.SwarmEvents;
            
            // Purge events older than TTL
            var cutoffDate = DateTimeOffset.UtcNow.AddDays(-config.RetentionDays);
            await DeleteEventsBeforeAsync(cutoffDate, ct);
            
            // Purge log files for completed jobs older than TTL
            var logDir = new DirectoryInfo("logs/sessions");
            if (logDir.Exists)
            {
                foreach (var file in logDir.GetFiles("*.log"))
                {
                    if (file.LastWriteTime < cutoffDate.DateTime)
                    {
                        file.Delete();
                    }
                }
            }
        }
    }
}
```

#### Configuration

```yaml
diagnostics:
  swarm_events:
    enabled: true
    retention_days: 30
    max_events_per_job: 10000
    log_to_file: true  # Also write to logs/sessions/
```

#### Background Rotation Task

```csharp
// In Program.cs or background service
public class SwarmEventPurgeService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await swarmEventService.PurgeOldEventsAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);  // Daily
        }
    }
}
```

#### Implementation Checklist

- [ ] Implement `ISwarmEventService` interface
- [ ] Implement database persistence
- [ ] Implement log file writing (`logs/sessions/{job_id}.log`)
- [ ] Implement purge logic (retention policy)
- [ ] Add background purge service
- [ ] Add configuration options
- [ ] Add unit tests for rotation logic
- [ ] Add integration tests with mock events

---

### Task T-605: Build Session Trace Summaries

**Purpose**: CLI/API to summarize per-job swarm behavior.

#### Summary Model

```csharp
namespace slskd.Transfers.Diagnostics
{
    public class SwarmSessionSummary
    {
        public string JobId { get; set; }
        public DateTimeOffset StartedAt { get; set; }
        public DateTimeOffset? CompletedAt { get; set; }
        public TimeSpan Duration => (CompletedAt ?? DateTimeOffset.UtcNow) - StartedAt;
        
        // Peer contributions
        public List<PeerContribution> PeerContributions { get; set; }
        
        // Source split
        public long SoulseekBytes { get; set; }
        public long OverlayBytes { get; set; }
        public double OverlayPercentage => (SoulseekBytes + OverlayBytes) > 0
            ? OverlayBytes / (double)(SoulseekBytes + OverlayBytes) * 100.0
            : 0.0;
        
        // Key events
        public List<KeyEvent> KeyEvents { get; set; }
        
        // Performance
        public double AvgThroughputMBps { get; set; }
        public int TotalChunks { get; set; }
        public int FailedChunks { get; set; }
    }
    
    public class PeerContribution
    {
        public string PeerId { get; set; }
        public PeerSource Source { get; set; }
        public long BytesContributed { get; set; }
        public int ChunksServed { get; set; }
        public double AvgThroughputMBps { get; set; }
    }
    
    public class KeyEvent
    {
        public DateTimeOffset Timestamp { get; set; }
        public string Description { get; set; }  // "Rescue mode activated", "Peer X failed"
    }
}
```

#### Service Implementation

```csharp
namespace slskd.Transfers.Diagnostics
{
    public interface ISwarmSessionService
    {
        Task<SwarmSessionSummary> GetSessionSummaryAsync(string jobId, CancellationToken ct = default);
    }
    
    public class SwarmSessionService : ISwarmSessionService
    {
        private readonly ISwarmEventService events;
        
        public async Task<SwarmSessionSummary> GetSessionSummaryAsync(string jobId, CancellationToken ct)
        {
            var allEvents = await events.GetEventsForJobAsync(jobId, ct);
            
            if (allEvents.Count == 0)
            {
                return null;
            }
            
            var summary = new SwarmSessionSummary
            {
                JobId = jobId,
                StartedAt = allEvents.Min(e => e.Timestamp),
                CompletedAt = allEvents.Any(e => e.Type == SwarmEventType.JobComplete)
                    ? allEvents.Where(e => e.Type == SwarmEventType.JobComplete).Max(e => e.Timestamp)
                    : null
            };
            
            // Compute peer contributions
            var peerGroups = allEvents
                .Where(e => e.Type == SwarmEventType.ChunkReceived && e.BytesTransferred.HasValue)
                .GroupBy(e => e.PeerId);
            
            summary.PeerContributions = peerGroups.Select(g => new PeerContribution
            {
                PeerId = g.Key,
                Source = g.First().Source,
                BytesContributed = g.Sum(e => e.BytesTransferred ?? 0),
                ChunksServed = g.Count(),
                AvgThroughputMBps = g.Average(e => e.ThroughputBytesPerSec ?? 0) / (1024.0 * 1024.0)
            }).OrderByDescending(p => p.BytesContributed).ToList();
            
            // Source split
            summary.SoulseekBytes = summary.PeerContributions
                .Where(p => p.Source == PeerSource.Soulseek)
                .Sum(p => p.BytesContributed);
            summary.OverlayBytes = summary.PeerContributions
                .Where(p => p.Source == PeerSource.Overlay)
                .Sum(p => p.BytesContributed);
            
            // Key events
            summary.KeyEvents = allEvents
                .Where(e => e.Type == SwarmEventType.RescueModeActivated ||
                            e.Type == SwarmEventType.SwarmRebalanced ||
                            (e.Type == SwarmEventType.Error && e.IsError))
                .Select(e => new KeyEvent
                {
                    Timestamp = e.Timestamp,
                    Description = e.Action ?? e.ErrorMessage
                })
                .OrderBy(ke => ke.Timestamp)
                .ToList();
            
            return summary;
        }
    }
}
```

#### CLI Command

```bash
slskdn job trace <job_id>
```

Output:
```
Job: 01HRQ4GSX9K47J7M5FK7VW8B12
Duration: 8m 24s
Status: Completed

Peer Contributions:
  1. user123 (Soulseek): 180 MB, 45 chunks, 3.8 MB/s
  2. peer-abc (Overlay): 120 MB, 30 chunks, 5.2 MB/s
  3. peer-def (Overlay): 80 MB, 20 chunks, 4.1 MB/s

Source Split:
  Soulseek: 180 MB (47%)
  Overlay: 200 MB (53%)

Key Events:
  00:00:45 - Rescue mode activated (Soulseek transfer stalled)
  00:03:12 - Peer user456 disconnected
  00:05:30 - Swarm rebalanced (peer performance degraded)

Performance:
  Avg throughput: 4.5 MB/s
  Total chunks: 95
  Failed chunks: 2 (2.1%)
```

#### Implementation Checklist

- [ ] Define `SwarmSessionSummary` model
- [ ] Implement `ISwarmSessionService.GetSessionSummaryAsync()`
- [ ] Compute peer contributions from events
- [ ] Identify key events (rescue, rebalance, errors)
- [ ] Add CLI command: `slskdn job trace`
- [ ] Add API endpoint: `GET /api/jobs/{id}/trace`
- [ ] Add unit tests for summary computation
- [ ] Add integration tests with sample event data

---

## Phase 4C: Warm Cache Nodes (T-606 to T-608)

**Purpose**: Optional prefetching of popular content for faster serving.

### Task T-606: Warm Cache Configuration

```yaml
mesh:
  warm_cache:
    enabled: false  # Opt-in
    max_storage_gb: 50
    min_popularity_threshold: 5  # Min peer count to cache
    cache_directory: "/cache/slskdn"
```

### Task T-607: Popularity Detection

Track MBIDs seen in mesh adverts and local jobs.

### Task T-608: Cache Fetch, Serve, Evict

- Fetch popular MBIDs via multi-swarm
- Advertise in overlay descriptors
- Serve chunks within fairness limits
- LRU eviction when capacity exceeded

**Implementation**: Similar to existing multi-source, with "cache" flag in adverts.

---

## Phase 4D: Playback-Aware Swarming (T-609 to T-611)

**Purpose**: Optimize for streaming playback (optional, requires player integration).

### Task T-609: Playback Feedback API

```csharp
public class PlaybackFeedback
{
    public long CurrentPositionBytes { get; set; }
    public long DesiredBufferAheadBytes { get; set; }  // e.g., 10 MB ahead
}
```

### Task T-610: Priority Zones

- **High priority**: Next 10 MB (playback buffer)
- **Mid priority**: 10-50 MB ahead
- **Low priority**: Rest of file

### Task T-611: Streaming Diagnostics

API to show buffer status, peers serving current zone, underrun events.

---

## Implementation Summary

**Phase 4 adds operational maturity:**
- Job manifests for reproducibility
- Session traces for debugging
- Warm cache for performance (opt-in)
- Playback optimization (opt-in)

**Total tasks**: 12 (T-600 to T-611)
**Estimated duration**: 6-8 weeks

Ready for Phase 5 (Soulbeet Integration)!
