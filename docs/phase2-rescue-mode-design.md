# Phase 2D: Rescue Mode for Underperforming Soulseek Transfers - Detailed Design

> **Tasks**: T-409 to T-411  
> **Branch**: `experimental/brainz`  
> **Dependencies**: Phase 1 (MusicBrainz/Multi-source), T-406 to T-408 (Swarm Scheduling)

---

## Overview

Rescue Mode automatically detects when Soulseek transfers are underperforming (stuck in queue or crawling at <10 KB/s) and supplements them with overlay mesh sources while keeping the original transfer alive. This ensures users get their files even when Soulseek peers are slow, while maintaining Soulseek as the primary/authority network.

---

## 1. Underperformance Detection (T-409)

### 1.1. Transfer State Tracking

```csharp
namespace slskd.Transfers.Rescue
{
    /// <summary>
    /// Tracks performance state of a Soulseek transfer.
    /// </summary>
    public class TransferPerformanceState
    {
        public string TransferId { get; set; }
        public string Username { get; set; }
        public string Filename { get; set; }
        public long TotalBytes { get; set; }
        
        // Queue tracking
        public TransferState State { get; set; }  // Queued | Active | Complete | Failed
        public DateTimeOffset QueuedAt { get; set; }
        public DateTimeOffset? StartedAt { get; set; }
        public TimeSpan TimeInQueue => StartedAt.HasValue 
            ? StartedAt.Value - QueuedAt 
            : DateTimeOffset.UtcNow - QueuedAt;
        
        // Throughput tracking
        public long BytesTransferred { get; set; }
        public DateTimeOffset LastProgressAt { get; set; }
        public Queue<ThroughputSample> RecentSamples { get; set; } = new();
        public double SustainedThroughputBytesPerSec { get; set; }
        
        // Underperformance detection
        public bool IsUnderperforming { get; set; }
        public UnderperformanceReason? Reason { get; set; }
        public DateTimeOffset? UnderperformingDetectedAt { get; set; }
        
        // Rescue state
        public bool RescueModeActive { get; set; }
        public string RescueJobId { get; set; }
        public DateTimeOffset? RescueModeActivatedAt { get; set; }
    }
    
    public enum UnderperformanceReason
    {
        QueuedTooLong,          // In queue > threshold
        ThroughputTooLow,       // Active but < min speed
        Stalled,                // No progress for > timeout
        PeerDisconnected        // Peer went offline
    }
    
    public enum TransferState
    {
        Queued,
        Active,
        Complete,
        Failed,
        Cancelled
    }
}
```

### 1.2. Underperformance Detector Service

```csharp
namespace slskd.Transfers.Rescue
{
    public interface IUnderperformanceDetector
    {
        /// <summary>
        /// Check if a transfer is underperforming.
        /// </summary>
        Task<(bool isUnderperforming, UnderperformanceReason? reason)> CheckTransferAsync(
            TransferPerformanceState transfer,
            CancellationToken ct = default);
        
        /// <summary>
        /// Monitor all active transfers and emit events for underperforming ones.
        /// </summary>
        Task StartMonitoringAsync(CancellationToken ct = default);
    }
    
    public class UnderperformanceDetector : IUnderperformanceDetector
    {
        private readonly ITransferService transfers;
        private readonly IOptionsMonitor<Options> options;
        private readonly EventBus eventBus;
        private readonly ILogger<UnderperformanceDetector> log;
        
        public async Task<(bool isUnderperforming, UnderperformanceReason? reason)> CheckTransferAsync(
            TransferPerformanceState transfer,
            CancellationToken ct)
        {
            var config = options.CurrentValue.Transfers.RescueMode;
            
            if (!config.Enabled)
            {
                return (false, null);
            }
            
            // Rule 1: Queued too long
            if (transfer.State == TransferState.Queued)
            {
                if (transfer.TimeInQueue > TimeSpan.FromSeconds(config.MaxQueueTimeSeconds))
                {
                    log.Information("[RESCUE] Transfer queued for {Duration}, triggering rescue: {File}",
                        transfer.TimeInQueue, transfer.Filename);
                    return (true, UnderperformanceReason.QueuedTooLong);
                }
            }
            
            // Rule 2: Throughput too low
            if (transfer.State == TransferState.Active)
            {
                // Compute sustained throughput over last N samples
                if (transfer.RecentSamples.Count >= 5)  // Need at least 5 samples
                {
                    var avgThroughput = transfer.RecentSamples
                        .Select(s => s.BytesPerSec)
                        .Average();
                    
                    transfer.SustainedThroughputBytesPerSec = avgThroughput;
                    
                    double minThroughputBytesPerSec = config.MinThroughputKBps * 1024.0;
                    
                    if (avgThroughput < minThroughputBytesPerSec)
                    {
                        // Check how long throughput has been low
                        var lowThroughputDuration = GetLowThroughputDuration(transfer, minThroughputBytesPerSec);
                        
                        if (lowThroughputDuration > TimeSpan.FromSeconds(config.MinDurationSeconds))
                        {
                            log.Information("[RESCUE] Transfer throughput {Throughput:F1} KB/s < {Min} KB/s for {Duration}, triggering rescue: {File}",
                                avgThroughput / 1024.0,
                                config.MinThroughputKBps,
                                lowThroughputDuration,
                                transfer.Filename);
                            return (true, UnderperformanceReason.ThroughputTooLow);
                        }
                    }
                }
            }
            
            // Rule 3: Stalled (no progress)
            if (transfer.State == TransferState.Active)
            {
                var timeSinceProgress = DateTimeOffset.UtcNow - transfer.LastProgressAt;
                
                if (timeSinceProgress > TimeSpan.FromSeconds(config.StallTimeoutSeconds))
                {
                    log.Information("[RESCUE] Transfer stalled for {Duration}, triggering rescue: {File}",
                        timeSinceProgress, transfer.Filename);
                    return (true, UnderperformanceReason.Stalled);
                }
            }
            
            return (false, null);
        }
        
        private TimeSpan GetLowThroughputDuration(TransferPerformanceState transfer, double threshold)
        {
            // Find the earliest sample that's below threshold
            var lowSamples = transfer.RecentSamples
                .Where(s => s.BytesPerSec < threshold)
                .OrderBy(s => s.Timestamp)
                .ToList();
            
            if (lowSamples.Count == 0)
            {
                return TimeSpan.Zero;
            }
            
            return DateTimeOffset.UtcNow - lowSamples.First().Timestamp;
        }
        
        public async Task StartMonitoringAsync(CancellationToken ct)
        {
            log.Information("[RESCUE] Starting underperformance monitoring");
            
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    // Get all active/queued downloads
                    var activeTransfers = await transfers.GetActiveDownloadsAsync(ct);
                    
                    foreach (var transfer in activeTransfers)
                    {
                        var state = await GetTransferStateAsync(transfer.Id, ct);
                        
                        if (state.RescueModeActive)
                        {
                            continue;  // Already in rescue mode
                        }
                        
                        var (isUnderperforming, reason) = await CheckTransferAsync(state, ct);
                        
                        if (isUnderperforming)
                        {
                            // Emit event for rescue service to handle
                            eventBus.Publish(new TransferUnderperformingEvent
                            {
                                TransferId = transfer.Id,
                                Filename = transfer.Filename,
                                Reason = reason.Value,
                                State = state
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    log.Error(ex, "[RESCUE] Error during monitoring");
                }
                
                await Task.Delay(TimeSpan.FromSeconds(10), ct);  // Check every 10 seconds
            }
        }
    }
    
    public class TransferUnderperformingEvent
    {
        public string TransferId { get; set; }
        public string Filename { get; set; }
        public UnderperformanceReason Reason { get; set; }
        public TransferPerformanceState State { get; set; }
    }
}
```

### 1.3. Configuration Options

```yaml
transfers:
  rescue_mode:
    enabled: true
    
    # Underperformance thresholds
    max_queue_time_seconds: 1800       # 30 minutes
    min_throughput_kbps: 10            # 10 KB/s sustained
    min_duration_seconds: 300          # Must be low for 5 minutes
    stall_timeout_seconds: 120         # 2 minutes without progress
    
    # Sample window
    sample_window_size: 10             # Track last 10 throughput samples
    sample_interval_seconds: 30        # Sample every 30 seconds
```

---

## 2. Overlay Rescue Logic (T-410)

### 2.1. Rescue Service

```csharp
namespace slskd.Transfers.Rescue
{
    public interface IRescueService
    {
        /// <summary>
        /// Activate rescue mode for a transfer.
        /// </summary>
        Task<RescueJob> ActivateRescueModeAsync(
            TransferPerformanceState transfer,
            UnderperformanceReason reason,
            CancellationToken ct = default);
        
        /// <summary>
        /// Deactivate rescue mode (transfer recovered or completed).
        /// </summary>
        Task DeactivateRescueModeAsync(string transferId, CancellationToken ct = default);
    }
    
    public class RescueService : IRescueService
    {
        private readonly IHashDbService hashDb;
        private readonly IFingerprintExtractionService fingerprinting;
        private readonly IAcoustIdClient acoustId;
        private readonly IMeshSyncService meshSync;
        private readonly IMultiSourceDownloadService multiSource;
        private readonly ILogger<RescueService> log;
        
        public async Task<RescueJob> ActivateRescueModeAsync(
            TransferPerformanceState transfer,
            UnderperformanceReason reason,
            CancellationToken ct)
        {
            log.Information("[RESCUE] Activating rescue mode for {File}: {Reason}",
                transfer.Filename, reason);
            
            // Step 1: Resolve MusicBrainz Recording ID
            string recordingId = await ResolveRecordingIdAsync(transfer, ct);
            
            if (recordingId == null)
            {
                log.Warning("[RESCUE] Cannot activate rescue: unable to resolve MusicBrainz Recording ID");
                return null;
            }
            
            // Step 2: Query overlay mesh for peers with this recording
            var overlayPeers = await DiscoverOverlayPeersAsync(recordingId, ct);
            
            if (overlayPeers.Count == 0)
            {
                log.Warning("[RESCUE] Cannot activate rescue: no overlay peers found for recording {RecordingId}",
                    recordingId);
                return null;
            }
            
            log.Information("[RESCUE] Found {Count} overlay peers with recording {RecordingId}",
                overlayPeers.Count, recordingId);
            
            // Step 3: Determine missing byte ranges
            var missingRanges = ComputeMissingRanges(transfer);
            
            // Step 4: Create overlay multi-source download job for missing ranges
            var rescueJob = new RescueJob
            {
                RescueJobId = Ulid.NewUlid().ToString(),
                OriginalTransferId = transfer.TransferId,
                RecordingId = recordingId,
                Filename = transfer.Filename,
                MissingRanges = missingRanges,
                OverlayPeers = overlayPeers,
                ActivatedAt = DateTimeOffset.UtcNow,
                Reason = reason
            };
            
            // Step 5: Start overlay chunk transfers
            var multiSourceJob = await multiSource.CreateJobAsync(new MultiSourceJobRequest
            {
                Filename = transfer.Filename,
                TotalSize = transfer.TotalBytes,
                Sources = overlayPeers.Select(p => new VerifiedSource
                {
                    PeerId = p.PeerId,
                    Source = PeerSource.Overlay,
                    MusicBrainzRecordingId = recordingId
                }).ToList(),
                MissingRanges = missingRanges,
                Priority = 9,  // High priority for rescue
                TargetPath = GetPartialFilePath(transfer)
            }, ct);
            
            rescueJob.MultiSourceJobId = multiSourceJob.JobId;
            
            // Step 6: Mark transfer as in rescue mode
            transfer.RescueModeActive = true;
            transfer.RescueJobId = rescueJob.RescueJobId;
            transfer.RescueModeActivatedAt = DateTimeOffset.UtcNow;
            
            await PersistRescueJobAsync(rescueJob, ct);
            
            log.Information("[RESCUE] Rescue mode activated: job {JobId}, {PeerCount} peers, {RangeCount} ranges",
                rescueJob.RescueJobId, overlayPeers.Count, missingRanges.Count);
            
            return rescueJob;
        }
        
        private async Task<string> ResolveRecordingIdAsync(TransferPerformanceState transfer, CancellationToken ct)
        {
            // Strategy 1: Check if partial file exists and has fingerprint in HashDb
            var partialPath = GetPartialFilePath(transfer);
            
            if (File.Exists(partialPath))
            {
                var hashEntry = await hashDb.GetHashByFilePathAsync(partialPath, ct);
                if (hashEntry?.MusicBrainzId != null)
                {
                    return hashEntry.MusicBrainzId;
                }
                
                // Try fingerprinting partial file (if enough data downloaded)
                if (transfer.BytesTransferred > 5 * 1024 * 1024)  // At least 5 MB
                {
                    try
                    {
                        var fingerprint = await fingerprinting.ExtractFingerprintAsync(partialPath, ct);
                        if (fingerprint != null)
                        {
                            var acoustIdResult = await acoustId.LookupAsync(
                                fingerprint.Fingerprint,
                                fingerprint.SampleRate,
                                fingerprint.DurationSeconds,
                                ct);
                            
                            return acoustIdResult?.Recordings?.FirstOrDefault()?.Id;
                        }
                    }
                    catch (Exception ex)
                    {
                        log.Warning(ex, "[RESCUE] Failed to fingerprint partial file");
                    }
                }
            }
            
            // Strategy 2: Search HashDb by filename similarity
            var candidateHashes = await hashDb.SearchByFilenameAsync(transfer.Filename, ct);
            if (candidateHashes.Any(h => h.MusicBrainzId != null))
            {
                return candidateHashes.First(h => h.MusicBrainzId != null).MusicBrainzId;
            }
            
            return null;
        }
        
        private async Task<List<OverlayPeerDescriptor>> DiscoverOverlayPeersAsync(string recordingId, CancellationToken ct)
        {
            // Query mesh for peers advertising this recording
            var meshPeers = await meshSync.QueryPeersWithRecordingAsync(recordingId, ct);
            
            // Filter to peers with verified fingerprints
            var verifiedPeers = meshPeers
                .Where(p => p.FingerprintVerified)
                .ToList();
            
            return verifiedPeers;
        }
        
        private List<ByteRange> ComputeMissingRanges(TransferPerformanceState transfer)
        {
            // Read partial file's bitmap to determine missing ranges
            var partialPath = GetPartialFilePath(transfer);
            
            if (!File.Exists(partialPath))
            {
                // No partial file, entire file is missing
                return new List<ByteRange>
                {
                    new ByteRange { Offset = 0, Length = transfer.TotalBytes }
                };
            }
            
            // Parse .partial metadata or BitTorrent-style piece map
            var bitmap = LoadPartialBitmap(partialPath);
            
            var missingRanges = new List<ByteRange>();
            long currentOffset = 0;
            
            foreach (var piece in bitmap)
            {
                if (!piece.Complete)
                {
                    missingRanges.Add(new ByteRange
                    {
                        Offset = currentOffset,
                        Length = piece.Length
                    });
                }
                currentOffset += piece.Length;
            }
            
            return missingRanges;
        }
        
        public async Task DeactivateRescueModeAsync(string transferId, CancellationToken ct)
        {
            var transfer = await GetTransferStateAsync(transferId, ct);
            
            if (!transfer.RescueModeActive)
            {
                return;
            }
            
            log.Information("[RESCUE] Deactivating rescue mode for {File}", transfer.Filename);
            
            // Cancel multi-source job
            if (transfer.RescueJobId != null)
            {
                await multiSource.CancelJobAsync(transfer.RescueJobId, ct);
            }
            
            transfer.RescueModeActive = false;
            transfer.RescueJobId = null;
            
            await PersistTransferStateAsync(transfer, ct);
        }
    }
    
    public class RescueJob
    {
        public string RescueJobId { get; set; }
        public string OriginalTransferId { get; set; }
        public string RecordingId { get; set; }
        public string Filename { get; set; }
        public List<ByteRange> MissingRanges { get; set; }
        public List<OverlayPeerDescriptor> OverlayPeers { get; set; }
        public string MultiSourceJobId { get; set; }
        public DateTimeOffset ActivatedAt { get; set; }
        public UnderperformanceReason Reason { get; set; }
        public RescueJobStatus Status { get; set; }
    }
    
    public enum RescueJobStatus
    {
        Active,
        Completed,
        Failed,
        Cancelled
    }
    
    public class ByteRange
    {
        public long Offset { get; set; }
        public long Length { get; set; }
    }
    
    public class OverlayPeerDescriptor
    {
        public string PeerId { get; set; }
        public string OverlayAddress { get; set; }
        public bool FingerprintVerified { get; set; }
        public string VariantId { get; set; }
    }
}
```

---

## 3. Soulseek-Primary Guardrails (T-411)

### 3.1. Guardrail Policy Service

```csharp
namespace slskd.Transfers.Rescue
{
    public interface IRescueGuardrailService
    {
        /// <summary>
        /// Check if rescue mode is allowed for a transfer.
        /// </summary>
        Task<(bool allowed, string reason)> CheckRescueAllowedAsync(
            TransferPerformanceState transfer,
            CancellationToken ct = default);
        
        /// <summary>
        /// Enforce ongoing guardrails during rescue.
        /// </summary>
        Task<bool> EnforceOngoingGuardrailsAsync(
            RescueJob rescue,
            CancellationToken ct = default);
    }
    
    public class RescueGuardrailService : IRescueGuardrailService
    {
        private readonly ITransferService transfers;
        private readonly IHashDbService hashDb;
        private readonly IOptionsMonitor<Options> options;
        private readonly ILogger<RescueGuardrailService> log;
        
        public async Task<(bool allowed, string reason)> CheckRescueAllowedAsync(
            TransferPerformanceState transfer,
            CancellationToken ct)
        {
            var config = options.CurrentValue.Transfers.RescueMode.Guardrails;
            
            // Guardrail 1: Require at least one Soulseek origin
            if (config.RequireSoulseekOrigin)
            {
                if (transfer.State != TransferState.Queued && transfer.State != TransferState.Active)
                {
                    return (false, "No active Soulseek transfer to rescue");
                }
            }
            
            // Guardrail 2: Check if file has been seen on Soulseek before
            if (config.RequireSeenOnSoulseek)
            {
                var seenBefore = await HasBeenSeenOnSoulseekAsync(transfer.Filename, ct);
                
                if (!seenBefore)
                {
                    return (false, "File has never been seen on Soulseek network");
                }
            }
            
            // Guardrail 3: Check MusicBrainz fingerprint verification
            if (config.RequireFingerprintVerification)
            {
                // This will be checked during ResolveRecordingIdAsync
                // If we can't resolve MBID, rescue will fail naturally
            }
            
            // Guardrail 4: Check maximum concurrent rescue jobs
            var activeRescueCount = await GetActiveRescueCountAsync(ct);
            
            if (activeRescueCount >= config.MaxConcurrentRescueJobs)
            {
                return (false, $"Maximum concurrent rescue jobs ({config.MaxConcurrentRescueJobs}) reached");
            }
            
            return (true, null);
        }
        
        public async Task<bool> EnforceOngoingGuardrailsAsync(RescueJob rescue, CancellationToken ct)
        {
            var config = options.CurrentValue.Transfers.RescueMode.Guardrails;
            
            // Get current transfer state
            var transfer = await transfers.GetTransferAsync(rescue.OriginalTransferId, ct);
            
            // Guardrail 1: Check overlay/Soulseek byte ratio
            long overlayBytes = await GetOverlayBytesForJobAsync(rescue.MultiSourceJobId, ct);
            long soulseekBytes = transfer.BytesTransferred;
            
            if (soulseekBytes > 0)
            {
                double overlayRatio = (double)overlayBytes / soulseekBytes;
                
                if (overlayRatio > config.MaxOverlayToSoulseekRatio)
                {
                    log.Warning("[RESCUE] Overlay/Soulseek ratio {Ratio:F2} exceeds limit {Limit:F2}, throttling overlay",
                        overlayRatio, config.MaxOverlayToSoulseekRatio);
                    
                    // Throttle overlay downloads
                    await ThrottleOverlayJobAsync(rescue.MultiSourceJobId, 0.5, ct);  // 50% speed
                    return false;
                }
            }
            
            // Guardrail 2: If Soulseek transfer recovers, deprioritize overlay
            if (transfer.State == TransferState.Active && transfer.SustainedThroughputBytesPerSec > 50 * 1024)  // > 50 KB/s
            {
                log.Information("[RESCUE] Soulseek transfer recovered, deprioritizing overlay");
                
                await DeprioritizeOverlayJobAsync(rescue.MultiSourceJobId, ct);
                
                // Don't cancel rescue entirely, but let Soulseek take over
            }
            
            // Guardrail 3: Check total overlay usage for period
            var stats = await GetOverlayUsageStatsAsync(TimeSpan.FromDays(1), ct);
            
            if (stats.TotalDownloadedMB > config.MaxOverlayMBPerDay)
            {
                log.Warning("[RESCUE] Daily overlay limit reached: {Used} MB / {Limit} MB",
                    stats.TotalDownloadedMB, config.MaxOverlayMBPerDay);
                
                // Pause rescue job
                await PauseOverlayJobAsync(rescue.MultiSourceJobId, ct);
                return false;
            }
            
            return true;
        }
        
        private async Task<bool> HasBeenSeenOnSoulseekAsync(string filename, CancellationToken ct)
        {
            // Check if any file with similar name exists in HashDb from Soulseek source
            var candidates = await hashDb.SearchByFilenameAsync(filename, ct);
            
            return candidates.Any(h => h.Source == "soulseek");
        }
    }
}
```

### 3.2. Guardrail Configuration

```yaml
transfers:
  rescue_mode:
    guardrails:
      # Require at least one Soulseek transfer origin
      require_soulseek_origin: true
      
      # Only rescue files that have been seen on Soulseek before
      require_seen_on_soulseek: true
      
      # Require MusicBrainz fingerprint verification
      require_fingerprint_verification: true
      
      # Maximum overlay bytes per Soulseek byte
      max_overlay_to_soulseek_ratio: 2.0
      
      # Maximum concurrent rescue jobs
      max_concurrent_rescue_jobs: 5
      
      # Daily overlay usage limit (MB)
      max_overlay_mb_per_day: 5000  # 5 GB
      
      # Overlay-only mode (dangerous, disabled by default)
      allow_overlay_only: false
```

---

## 4. UI Integration

### 4.1. Rescue Mode Indicator

```jsx
// src/web/src/components/Transfers/TransferItem.jsx

const TransferItem = ({ transfer }) => {
  const [rescueState, setRescueState] = useState(null);
  
  useEffect(() => {
    if (transfer.rescue_mode_active) {
      api.get(`/api/rescue/jobs/${transfer.rescue_job_id}`).then(setRescueState);
    }
  }, [transfer.rescue_job_id]);
  
  return (
    <div className="transfer-item">
      <div className="transfer-header">
        <span>{transfer.filename}</span>
        {transfer.rescue_mode_active && (
          <Label color="orange" size="small">
            <Icon name="life ring" /> Rescue Mode
          </Label>
        )}
      </div>
      
      <Progress
        percent={transfer.progress * 100}
        color={transfer.rescue_mode_active ? 'orange' : 'blue'}
      />
      
      {transfer.rescue_mode_active && rescueState && (
        <div className="rescue-details">
          <p>
            <Icon name="plug" /> Soulseek: {formatSpeed(transfer.soulseek_speed)} 
            ({formatBytes(transfer.bytes_transferred)})
          </p>
          <p>
            <Icon name="sitemap" /> Overlay: {formatSpeed(rescueState.overlay_speed)} 
            ({rescueState.overlay_peers} peers)
          </p>
          <p className="rescue-reason">
            Reason: {rescueState.reason}
          </p>
        </div>
      )}
    </div>
  );
};
```

---

## 5. Implementation Checklist

### T-409: Underperformance detection

- [x] Define `TransferPerformanceState` model
- [x] Define `UnderperformanceReason` enum
- [x] Implement `IUnderperformanceDetector` interface
- [x] Implement `CheckTransferAsync()` logic for all 3 rules
- [x] Implement `StartMonitoringAsync()` background task
- [x] Add throughput sampling to transfer service
- [x] Define `TransferUnderperformingEvent`
- [x] Register detector in `Program.cs`
- [x] Add configuration options
- [x] Add unit tests for detection rules
- [x] Add integration tests with mock transfers

### T-410: Overlay rescue logic

- [x] Define `RescueJob` model
- [x] Implement `IRescueService` interface
- [x] Implement `ActivateRescueModeAsync()` logic
- [x] Implement `ResolveRecordingIdAsync()` multi-strategy resolution
- [x] Implement `DiscoverOverlayPeersAsync()` mesh query
- [x] Implement `ComputeMissingRanges()` partial file parsing
- [x] Integrate with `IMultiSourceDownloadService`
- [x] Implement `DeactivateRescueModeAsync()` cleanup
- [x] Subscribe to transfer completion events
- [x] Add database schema for rescue jobs
- [x] Add unit tests for rescue logic
- [x] Add integration tests with mock overlay

### T-411: Soulseek-primary guardrails

- [x] Define `IRescueGuardrailService` interface
- [x] Implement `CheckRescueAllowedAsync()` policy checks
- [x] Implement `EnforceOngoingGuardrailsAsync()` runtime checks
- [x] Add overlay/Soulseek ratio enforcement
- [x] Add daily overlay usage tracking
- [x] Implement overlay throttling/pausing
- [x] Add "seen on Soulseek" verification
- [x] Add guardrail configuration options
- [x] Add unit tests for guardrail logic
- [x] Add integration tests for ratio enforcement
- [x] Document guardrail policies

---

## 6. Testing Strategy

### Unit Tests

```csharp
[Fact]
public async Task UnderperformanceDetector_QueuedTooLong_Should_Detect()
{
    var transfer = new TransferPerformanceState
    {
        State = TransferState.Queued,
        QueuedAt = DateTimeOffset.UtcNow.AddMinutes(-45)  // 45 minutes ago
    };
    
    var detector = new UnderperformanceDetector(mockTransfers, mockOptions, mockEventBus, mockLogger);
    var (isUnderperforming, reason) = await detector.CheckTransferAsync(transfer);
    
    Assert.True(isUnderperforming);
    Assert.Equal(UnderperformanceReason.QueuedTooLong, reason);
}

[Fact]
public async Task RescueGuardrailService_HighOverlayRatio_Should_Throttle()
{
    var rescue = new RescueJob
    {
        OriginalTransferId = "transfer-123"
    };
    
    // Mock: 10 MB overlay, 2 MB soulseek = 5:1 ratio (exceeds 2:1 limit)
    mockMultiSource.Setup(x => x.GetBytesDownloadedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(10 * 1024 * 1024);
    mockTransfers.Setup(x => x.GetTransferAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(new Transfer { BytesTransferred = 2 * 1024 * 1024 });
    
    var guardrails = new RescueGuardrailService(mockTransfers, mockHashDb, mockOptions, mockLogger);
    var allowed = await guardrails.EnforceOngoingGuardrailsAsync(rescue);
    
    Assert.False(allowed);
    mockMultiSource.Verify(x => x.ThrottleJobAsync(It.IsAny<string>(), 0.5, It.IsAny<CancellationToken>()), Times.Once);
}
```

---

## 7. Monitoring and Observability

### Metrics to Track

- Rescue activations per hour/day
- Rescue success rate (completed vs failed)
- Average time to rescue activation
- Overlay vs Soulseek byte ratios
- Rescue reasons distribution
- Guardrail violations per type

### Logging

```csharp
log.Information("[RESCUE] Activated for {File}: reason={Reason}, peers={PeerCount}, ranges={RangeCount}");
log.Information("[RESCUE] Completed for {File}: soulseek={SoulseekMB}MB, overlay={OverlayMB}MB");
log.Warning("[RESCUE] Guardrail violation: {Type} - {Details}");
```

---

This comprehensive design ensures Rescue Mode works safely while maintaining Soulseek as the primary network!


