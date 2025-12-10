# Phase 2B: Collection Doctor / Library Health - Detailed Design

> **Tasks**: T-403 to T-405  
> **Branch**: `experimental/brainz`  
> **Dependencies**: Phase 1 (MusicBrainz/Chromaprint), T-400 to T-402 (Canonical Scoring)

---

## Overview

The Collection Doctor ("Library Health") system scans user libraries, identifies quality issues, and provides automated remediation via multi-swarm downloads. This transforms slskdn from a download tool into a library management system.

---

## 1. Library Health Issues Taxonomy

### 1.1. Issue Types

```csharp
namespace slskd.LibraryHealth
{
    public enum LibraryIssueType
    {
        SuspectedTranscode,      // Lossless file likely encoded from lossy source
        NonCanonicalVariant,     // File exists but better canonical variant available
        TrackNotInTaggedRelease, // File's MBID doesn't match its tagged album
        MissingTrackInRelease,   // Album incomplete per MusicBrainz tracklist
        CorruptedFile,           // File can't be read or has integrity issues
        MissingMetadata,         // File has no MusicBrainz ID resolvable via fingerprint
        MultipleVariants,        // Duplicate recording with different quality levels
        WrongDuration            // File duration differs significantly from MB metadata
    }
    
    /// <summary>
    /// Represents a single library health issue.
    /// </summary>
    public class LibraryIssue
    {
        public string IssueId { get; set; }  // Unique ID
        public LibraryIssueType Type { get; set; }
        public LibraryIssueSeverity Severity { get; set; }
        
        // Affected entities
        public string FilePath { get; set; }
        public string MusicBrainzRecordingId { get; set; }
        public string MusicBrainzReleaseId { get; set; }
        public string Artist { get; set; }
        public string Album { get; set; }
        public string Title { get; set; }
        
        // Issue details
        public string Reason { get; set; }  // Human-readable explanation
        public Dictionary<string, object> Metadata { get; set; }  // Structured details
        
        // Remediation
        public bool CanAutoFix { get; set; }
        public string SuggestedAction { get; set; }
        public string RemediationJobId { get; set; }  // If fix job spawned
        
        // Status
        public LibraryIssueStatus Status { get; set; }
        public DateTimeOffset DetectedAt { get; set; }
        public DateTimeOffset? ResolvedAt { get; set; }
        public string ResolvedBy { get; set; }  // "user_dismissed" | "auto_fixed" | "manual_fix"
    }
    
    public enum LibraryIssueSeverity
    {
        Info,      // FYI, no action needed
        Low,       // Minor quality issue
        Medium,    // Noticeable quality or completeness issue
        High,      // Significant problem (transcode, corruption)
        Critical   // File unreadable or major data loss
    }
    
    public enum LibraryIssueStatus
    {
        Detected,      // New issue
        Acknowledged,  // User has seen it
        Ignored,       // User explicitly ignored
        Fixing,        // Remediation job in progress
        Resolved,      // Fixed successfully
        Failed         // Fix attempt failed
    }
}
```

### 1.2. Database Schema

```sql
CREATE TABLE LibraryHealthIssues (
    issue_id TEXT PRIMARY KEY,
    type TEXT NOT NULL,
    severity TEXT NOT NULL,
    file_path TEXT,
    mb_recording_id TEXT,
    mb_release_id TEXT,
    artist TEXT,
    album TEXT,
    title TEXT,
    reason TEXT,
    metadata TEXT,  -- JSON
    can_auto_fix BOOLEAN DEFAULT FALSE,
    suggested_action TEXT,
    remediation_job_id TEXT,
    status TEXT DEFAULT 'detected',
    detected_at INTEGER NOT NULL,
    resolved_at INTEGER,
    resolved_by TEXT,
    FOREIGN KEY (remediation_job_id) REFERENCES LibraryHealthJobs(job_id)
);

CREATE INDEX idx_issues_status ON LibraryHealthIssues(status);
CREATE INDEX idx_issues_type ON LibraryHealthIssues(type);
CREATE INDEX idx_issues_severity ON LibraryHealthIssues(severity);
CREATE INDEX idx_issues_release ON LibraryHealthIssues(mb_release_id);
CREATE INDEX idx_issues_file ON LibraryHealthIssues(file_path);

-- Scan history
CREATE TABLE LibraryHealthScans (
    scan_id TEXT PRIMARY KEY,
    library_path TEXT NOT NULL,
    started_at INTEGER NOT NULL,
    completed_at INTEGER,
    status TEXT DEFAULT 'running',  -- running | completed | failed | cancelled
    files_scanned INTEGER DEFAULT 0,
    issues_detected INTEGER DEFAULT 0,
    error_message TEXT
);

CREATE INDEX idx_scans_path ON LibraryHealthScans(library_path);
CREATE INDEX idx_scans_completed ON LibraryHealthScans(completed_at DESC);
```

---

## 2. Library Scan Service (T-403)

### 2.1. Scan Service Interface

```csharp
namespace slskd.LibraryHealth
{
    public interface ILibraryHealthService
    {
        /// <summary>
        /// Start a library health scan.
        /// </summary>
        Task<string> StartScanAsync(LibraryHealthScanRequest request, CancellationToken ct = default);
        
        /// <summary>
        /// Get scan status.
        /// </summary>
        Task<LibraryHealthScan> GetScanStatusAsync(string scanId, CancellationToken ct = default);
        
        /// <summary>
        /// Get all issues for a library path.
        /// </summary>
        Task<List<LibraryIssue>> GetIssuesAsync(LibraryHealthIssueFilter filter, CancellationToken ct = default);
        
        /// <summary>
        /// Mark issue as ignored/resolved.
        /// </summary>
        Task UpdateIssueStatusAsync(string issueId, LibraryIssueStatus newStatus, CancellationToken ct = default);
        
        /// <summary>
        /// Create remediation job for issue(s).
        /// </summary>
        Task<string> CreateRemediationJobAsync(List<string> issueIds, CancellationToken ct = default);
        
        /// <summary>
        /// Get library health summary.
        /// </summary>
        Task<LibraryHealthSummary> GetSummaryAsync(string libraryPath, CancellationToken ct = default);
    }
    
    public class LibraryHealthScanRequest
    {
        public string LibraryPath { get; set; }
        public bool IncludeSubdirectories { get; set; } = true;
        public List<string> FileExtensions { get; set; } = new() { ".flac", ".mp3", ".m4a", ".ogg" };
        public bool SkipPreviouslyScanned { get; set; } = false;
        public int MaxConcurrentFiles { get; set; } = 4;
    }
    
    public class LibraryHealthIssueFilter
    {
        public string LibraryPath { get; set; }
        public List<LibraryIssueType> Types { get; set; }
        public List<LibraryIssueSeverity> Severities { get; set; }
        public List<LibraryIssueStatus> Statuses { get; set; }
        public string MusicBrainzReleaseId { get; set; }
        public int Limit { get; set; } = 100;
        public int Offset { get; set; } = 0;
    }
}
```

### 2.2. Scan Service Implementation

```csharp
namespace slskd.LibraryHealth
{
    public class LibraryHealthService : ILibraryHealthService
    {
        private readonly IHashDbService hashDb;
        private readonly IFingerprintExtractionService fingerprintService;
        private readonly IAcoustIdClient acoustIdClient;
        private readonly IMusicBrainzClient musicBrainzClient;
        private readonly ICanonicalStatsService canonicalStats;
        private readonly ILogger<LibraryHealthService> log;
        
        private readonly ConcurrentDictionary<string, LibraryHealthScan> activeScans = new();
        
        public async Task<string> StartScanAsync(LibraryHealthScanRequest request, CancellationToken ct)
        {
            var scanId = Ulid.NewUlid().ToString();
            var scan = new LibraryHealthScan
            {
                ScanId = scanId,
                LibraryPath = request.LibraryPath,
                StartedAt = DateTimeOffset.UtcNow,
                Status = ScanStatus.Running
            };
            
            activeScans[scanId] = scan;
            
            // Persist scan record
            await PersistScanAsync(scan, ct);
            
            // Start background scan task
            _ = Task.Run(() => PerformScanAsync(scanId, request, ct), ct);
            
            return scanId;
        }
        
        private async Task PerformScanAsync(string scanId, LibraryHealthScanRequest request, CancellationToken ct)
        {
            var scan = activeScans[scanId];
            
            try
            {
                log.Information("[LH] Starting library health scan: {Path}", request.LibraryPath);
                
                // Enumerate audio files
                var files = Directory.EnumerateFiles(
                    request.LibraryPath,
                    "*.*",
                    request.IncludeSubdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
                    .Where(f => request.FileExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                    .ToList();
                
                log.Information("[LH] Found {Count} audio files to scan", files.Count);
                
                // Process files in parallel with concurrency limit
                var semaphore = new SemaphoreSlim(request.MaxConcurrentFiles);
                var tasks = files.Select(async file =>
                {
                    await semaphore.WaitAsync(ct);
                    try
                    {
                        await ScanFileAsync(file, scan, ct);
                        Interlocked.Increment(ref scan.FilesScanned);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });
                
                await Task.WhenAll(tasks);
                
                scan.Status = ScanStatus.Completed;
                scan.CompletedAt = DateTimeOffset.UtcNow;
                
                log.Information("[LH] Scan completed: {Files} files, {Issues} issues",
                    scan.FilesScanned, scan.IssuesDetected);
            }
            catch (Exception ex)
            {
                log.Error(ex, "[LH] Scan failed for {Path}", request.LibraryPath);
                scan.Status = ScanStatus.Failed;
                scan.ErrorMessage = ex.Message;
            }
            finally
            {
                await PersistScanAsync(scan, ct);
            }
        }
        
        private async Task ScanFileAsync(string filePath, LibraryHealthScan scan, CancellationToken ct)
        {
            try
            {
                // Step 1: Extract metadata and fingerprint
                using var tagFile = TagLib.File.Create(filePath);
                var props = tagFile.Properties;
                var tags = tagFile.Tag;
                
                var fingerprint = await fingerprintService.ExtractFingerprintAsync(filePath, ct);
                
                // Step 2: Resolve MusicBrainz ID
                string recordingId = null;
                if (!string.IsNullOrEmpty(fingerprint?.Fingerprint))
                {
                    var acoustIdResult = await acoustIdClient.LookupAsync(
                        fingerprint.Fingerprint,
                        fingerprint.SampleRate,
                        fingerprint.DurationSeconds,
                        ct);
                    
                    recordingId = acoustIdResult?.Recordings?.FirstOrDefault()?.Id;
                }
                
                if (recordingId == null)
                {
                    // Issue: Missing metadata (can't resolve MBID)
                    await EmitIssueAsync(new LibraryIssue
                    {
                        Type = LibraryIssueType.MissingMetadata,
                        Severity = LibraryIssueSeverity.Low,
                        FilePath = filePath,
                        Artist = tags.FirstPerformer,
                        Album = tags.Album,
                        Title = tags.Title,
                        Reason = "Unable to resolve MusicBrainz Recording ID via fingerprint",
                        CanAutoFix = false
                    }, scan, ct);
                    return;
                }
                
                // Step 3: Create AudioVariant for quality analysis
                var variant = await CreateVariantFromFileAsync(filePath, recordingId, tagFile, ct);
                
                // Step 4: Check for transcodes
                await CheckForTranscodeAsync(variant, scan, ct);
                
                // Step 5: Check for canonical variants
                await CheckForCanonicalUpgradeAsync(variant, scan, ct);
                
                // Step 6: Check release completeness
                await CheckReleaseCompletenessAsync(variant, tags, scan, ct);
                
                // Step 7: Check duration mismatch
                await CheckDurationMismatchAsync(variant, recordingId, scan, ct);
            }
            catch (Exception ex)
            {
                log.Warning(ex, "[LH] Failed to scan file: {Path}", filePath);
                
                // Issue: Corrupted file
                await EmitIssueAsync(new LibraryIssue
                {
                    Type = LibraryIssueType.CorruptedFile,
                    Severity = LibraryIssueSeverity.Critical,
                    FilePath = filePath,
                    Reason = $"File cannot be read: {ex.Message}",
                    CanAutoFix = true,
                    SuggestedAction = "Re-download from Soulseek or mesh overlay"
                }, scan, ct);
            }
        }
        
        private async Task CheckForTranscodeAsync(AudioVariant variant, LibraryHealthScan scan, CancellationToken ct)
        {
            var detector = new TranscodeDetector();
            var (isSuspect, reason) = detector.DetectTranscode(variant);
            
            if (isSuspect)
            {
                await EmitIssueAsync(new LibraryIssue
                {
                    Type = LibraryIssueType.SuspectedTranscode,
                    Severity = LibraryIssueSeverity.High,
                    FilePath = variant.FilePath,
                    MusicBrainzRecordingId = variant.MusicBrainzRecordingId,
                    Artist = variant.Artist,
                    Title = variant.Title,
                    Reason = reason,
                    Metadata = new Dictionary<string, object>
                    {
                        ["quality_score"] = variant.QualityScore,
                        ["codec"] = variant.Codec,
                        ["bitrate"] = variant.BitrateKbps,
                        ["dynamic_range"] = variant.DynamicRangeDR
                    },
                    CanAutoFix = true,
                    SuggestedAction = "Replace with verified lossless variant from canonical sources"
                }, scan, ct);
            }
        }
        
        private async Task CheckForCanonicalUpgradeAsync(AudioVariant variant, LibraryHealthScan scan, CancellationToken ct)
        {
            var candidates = await canonicalStats.GetCanonicalVariantCandidatesAsync(
                variant.MusicBrainzRecordingId,
                ct);
            
            if (candidates.Count == 0) return;
            
            var bestCanonical = candidates.First();
            
            // Check if current variant is significantly worse than canonical
            double qualityGap = bestCanonical.QualityScore - variant.QualityScore;
            
            if (qualityGap > 0.2)  // Significant quality gap
            {
                await EmitIssueAsync(new LibraryIssue
                {
                    Type = LibraryIssueType.NonCanonicalVariant,
                    Severity = qualityGap > 0.4 ? LibraryIssueSeverity.High : LibraryIssueSeverity.Medium,
                    FilePath = variant.FilePath,
                    MusicBrainzRecordingId = variant.MusicBrainzRecordingId,
                    Artist = variant.Artist,
                    Title = variant.Title,
                    Reason = $"Canonical variant available with quality score {bestCanonical.QualityScore:F2} vs current {variant.QualityScore:F2}",
                    Metadata = new Dictionary<string, object>
                    {
                        ["current_quality"] = variant.QualityScore,
                        ["canonical_quality"] = bestCanonical.QualityScore,
                        ["canonical_codec"] = bestCanonical.Codec,
                        ["canonical_bitrate"] = bestCanonical.BitrateKbps
                    },
                    CanAutoFix = true,
                    SuggestedAction = "Download canonical variant and replace current file"
                }, scan, ct);
            }
        }
        
        private async Task CheckReleaseCompletenessAsync(AudioVariant variant, TagLib.Tag tags, LibraryHealthScan scan, CancellationToken ct)
        {
            // Extract MusicBrainz Release ID from tags (if present)
            string releaseId = tags.MusicBrainzReleaseId;
            
            if (string.IsNullOrEmpty(releaseId))
            {
                // Try to infer from recording ID
                var releases = await musicBrainzClient.GetReleasesForRecordingAsync(variant.MusicBrainzRecordingId, ct);
                releaseId = releases?.FirstOrDefault();
            }
            
            if (string.IsNullOrEmpty(releaseId)) return;
            
            // Get album target from HashDb
            var albumTarget = await hashDb.GetAlbumTargetAsync(releaseId, ct);
            if (albumTarget == null) return;
            
            // Check if all tracks in release are present in library
            var tracks = await hashDb.GetAlbumTracksAsync(releaseId, ct);
            var libraryPath = Path.GetDirectoryName(variant.FilePath);
            
            var missingTracks = new List<AlbumTargetTrackEntry>();
            
            foreach (var track in tracks)
            {
                var matches = await hashDb.LookupHashesByRecordingIdAsync(track.RecordingId, ct);
                
                // Check if any match is in the same directory (same album)
                bool foundInAlbum = matches.Any(m =>
                {
                    var matchPath = GetFilePathForHash(m.FlacKey);  // Would need reverse lookup
                    return matchPath != null && Path.GetDirectoryName(matchPath) == libraryPath;
                });
                
                if (!foundInAlbum)
                {
                    missingTracks.Add(track);
                }
            }
            
            if (missingTracks.Count > 0)
            {
                await EmitIssueAsync(new LibraryIssue
                {
                    Type = LibraryIssueType.MissingTrackInRelease,
                    Severity = missingTracks.Count > tracks.Count / 2 ? LibraryIssueSeverity.High : LibraryIssueSeverity.Medium,
                    FilePath = libraryPath,  // Directory, not specific file
                    MusicBrainzReleaseId = releaseId,
                    Artist = albumTarget.Artist,
                    Album = albumTarget.Title,
                    Reason = $"Album incomplete: {missingTracks.Count}/{tracks.Count} tracks missing",
                    Metadata = new Dictionary<string, object>
                    {
                        ["missing_tracks"] = missingTracks.Select(t => new
                        {
                            position = t.Position,
                            title = t.Title,
                            recording_id = t.RecordingId
                        }).ToList()
                    },
                    CanAutoFix = true,
                    SuggestedAction = "Download missing tracks via MusicBrainz Release job"
                }, scan, ct);
            }
        }
        
        private async Task CheckDurationMismatchAsync(AudioVariant variant, string recordingId, LibraryHealthScan scan, CancellationToken ct)
        {
            // Get MusicBrainz canonical duration
            var recording = await musicBrainzClient.GetRecordingAsync(recordingId, ct);
            if (recording?.DurationMs == null) return;
            
            int durationDiff = Math.Abs(variant.DurationMs - recording.DurationMs.Value);
            
            // Allow 3 second tolerance
            if (durationDiff > 3000)
            {
                await EmitIssueAsync(new LibraryIssue
                {
                    Type = LibraryIssueType.WrongDuration,
                    Severity = LibraryIssueSeverity.Low,
                    FilePath = variant.FilePath,
                    MusicBrainzRecordingId = recordingId,
                    Artist = variant.Artist,
                    Title = variant.Title,
                    Reason = $"Duration mismatch: file {variant.DurationMs / 1000}s vs MB {recording.DurationMs / 1000}s",
                    Metadata = new Dictionary<string, object>
                    {
                        ["file_duration_ms"] = variant.DurationMs,
                        ["mb_duration_ms"] = recording.DurationMs
                    },
                    CanAutoFix = false,
                    SuggestedAction = "Verify this is the correct recording; may be live/alternate version"
                }, scan, ct);
            }
        }
        
        private async Task EmitIssueAsync(LibraryIssue issue, LibraryHealthScan scan, CancellationToken ct)
        {
            issue.IssueId = Ulid.NewUlid().ToString();
            issue.DetectedAt = DateTimeOffset.UtcNow;
            issue.Status = LibraryIssueStatus.Detected;
            
            // Persist to database
            await PersistIssueAsync(issue, ct);
            
            Interlocked.Increment(ref scan.IssuesDetected);
            
            log.Information("[LH] Detected issue: {Type} - {Reason}", issue.Type, issue.Reason);
        }
    }
}
```

---

## 3. Library Health UI/API (T-404)

### 3.1. API Endpoints

```csharp
namespace slskd.LibraryHealth.API
{
    [ApiController]
    [Route("api/library/health")]
    [Produces("application/json")]
    public class LibraryHealthController : ControllerBase
    {
        private readonly ILibraryHealthService healthService;
        
        /// <summary>
        /// Start a library health scan.
        /// </summary>
        [HttpPost("scan")]
        public async Task<IActionResult> StartScan([FromBody] LibraryHealthScanRequest request, CancellationToken ct)
        {
            var scanId = await healthService.StartScanAsync(request, ct);
            return Ok(new { scan_id = scanId });
        }
        
        /// <summary>
        /// Get scan status.
        /// </summary>
        [HttpGet("scan/{scanId}")]
        public async Task<IActionResult> GetScanStatus(string scanId, CancellationToken ct)
        {
            var scan = await healthService.GetScanStatusAsync(scanId, ct);
            if (scan == null) return NotFound();
            return Ok(scan);
        }
        
        /// <summary>
        /// Get library health summary.
        /// </summary>
        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary([FromQuery] string path, CancellationToken ct)
        {
            var summary = await healthService.GetSummaryAsync(path, ct);
            return Ok(summary);
        }
        
        /// <summary>
        /// Get issues with filtering.
        /// </summary>
        [HttpGet("issues")]
        public async Task<IActionResult> GetIssues([FromQuery] LibraryHealthIssueFilter filter, CancellationToken ct)
        {
            var issues = await healthService.GetIssuesAsync(filter, ct);
            return Ok(new { issues });
        }
        
        /// <summary>
        /// Update issue status (ignore, acknowledge, etc).
        /// </summary>
        [HttpPatch("issues/{issueId}")]
        public async Task<IActionResult> UpdateIssue(string issueId, [FromBody] UpdateIssueRequest request, CancellationToken ct)
        {
            await healthService.UpdateIssueStatusAsync(issueId, request.Status, ct);
            return NoContent();
        }
        
        /// <summary>
        /// Create remediation job for one or more issues.
        /// </summary>
        [HttpPost("issues/fix")]
        public async Task<IActionResult> FixIssues([FromBody] FixIssuesRequest request, CancellationToken ct)
        {
            var jobId = await healthService.CreateRemediationJobAsync(request.IssueIds, ct);
            return Ok(new { job_id = jobId });
        }
    }
    
    public class UpdateIssueRequest
    {
        public LibraryIssueStatus Status { get; set; }
    }
    
    public class FixIssuesRequest
    {
        public List<string> IssueIds { get; set; }
    }
}
```

### 3.2. Frontend UI Components (React)

```jsx
// src/web/src/components/LibraryHealth/LibraryHealthDashboard.jsx

import React, { useState, useEffect } from 'react';
import { Segment, Header, Statistic, Button, Progress, List, Label } from 'semantic-ui-react';
import api from '../../lib/api';

const LibraryHealthDashboard = ({ libraryPath }) => {
  const [summary, setSummary] = useState(null);
  const [loading, setLoading] = useState(true);
  const [scanning, setScanning] = useState(false);
  
  useEffect(() => {
    loadSummary();
  }, [libraryPath]);
  
  const loadSummary = async () => {
    try {
      const data = await api.get('/api/library/health/summary', { params: { path: libraryPath } });
      setSummary(data);
    } finally {
      setLoading(false);
    }
  };
  
  const startScan = async () => {
    setScanning(true);
    try {
      const { scan_id } = await api.post('/api/library/health/scan', {
        library_path: libraryPath,
        include_subdirectories: true
      });
      
      // Poll for scan completion
      const pollInterval = setInterval(async () => {
        const scan = await api.get(`/api/library/health/scan/${scan_id}`);
        if (scan.status === 'completed' || scan.status === 'failed') {
          clearInterval(pollInterval);
          setScanning(false);
          loadSummary();
        }
      }, 2000);
    } catch (err) {
      setScanning(false);
    }
  };
  
  if (loading) return <div>Loading...</div>;
  
  return (
    <Segment>
      <Header as="h2">Library Health Dashboard</Header>
      
      <Statistic.Group widths="four">
        <Statistic>
          <Statistic.Value>{summary?.suspected_transcodes || 0}</Statistic.Value>
          <Statistic.Label>Suspected Transcodes</Statistic.Label>
        </Statistic>
        <Statistic>
          <Statistic.Value>{summary?.non_canonical_variants || 0}</Statistic.Value>
          <Statistic.Label>Non-Canonical Variants</Statistic.Label>
        </Statistic>
        <Statistic>
          <Statistic.Value>{summary?.incomplete_releases || 0}</Statistic.Value>
          <Statistic.Label>Incomplete Albums</Statistic.Label>
        </Statistic>
        <Statistic color={summary?.health_score > 0.85 ? 'green' : 'orange'}>
          <Statistic.Value>{(summary?.health_score * 100).toFixed(0)}%</Statistic.Value>
          <Statistic.Label>Health Score</Statistic.Label>
        </Statistic>
      </Statistic.Group>
      
      <Button primary onClick={startScan} loading={scanning} disabled={scanning}>
        {scanning ? 'Scanning...' : 'Run Health Scan'}
      </Button>
      
      {summary?.last_scan && (
        <p>Last scan: {new Date(summary.last_scan).toLocaleString()}</p>
      )}
      
      <Header as="h3">Quick Actions</Header>
      <Button.Group>
        <Button onClick={() => window.location.href = '/library/health/issues?type=SuspectedTranscode'}>
          View Transcodes
        </Button>
        <Button onClick={() => window.location.href = '/library/health/issues?type=MissingTrackInRelease'}>
          View Incomplete Albums
        </Button>
        <Button onClick={() => window.location.href = '/library/health/issues?type=NonCanonicalVariant'}>
          View Upgrade Opportunities
        </Button>
      </Button.Group>
    </Segment>
  );
};

export default LibraryHealthDashboard;
```

---

## 4. Fix via Multi-Swarm (T-405)

### 4.1. Remediation Service

```csharp
namespace slskd.LibraryHealth
{
    public interface ILibraryHealthRemediationService
    {
        Task<string> CreateRemediationJobAsync(List<string> issueIds, CancellationToken ct = default);
        Task LinkJobToIssuesAsync(string jobId, List<string> issueIds, CancellationToken ct = default);
    }
    
    public class LibraryHealthRemediationService : ILibraryHealthRemediationService
    {
        private readonly ILibraryHealthService healthService;
        private readonly IMusicBrainzClient musicBrainzClient;
        private readonly IMultiSourceDownloadService multiSourceDownloads;
        private readonly ILogger<LibraryHealthRemediationService> log;
        
        public async Task<string> CreateRemediationJobAsync(List<string> issueIds, CancellationToken ct)
        {
            // Group issues by remediation type
            var issues = new List<LibraryIssue>();
            foreach (var issueId in issueIds)
            {
                var issue = await healthService.GetIssueAsync(issueId, ct);
                if (issue != null && issue.CanAutoFix)
                {
                    issues.Add(issue);
                }
            }
            
            if (issues.Count == 0)
            {
                throw new InvalidOperationException("No fixable issues provided");
            }
            
            // Determine remediation strategy
            var strategy = DetermineRemediationStrategy(issues);
            
            string jobId = null;
            
            switch (strategy)
            {
                case RemediationStrategy.RedownloadTracks:
                    jobId = await CreateTrackRedownloadJobAsync(issues, ct);
                    break;
                    
                case RemediationStrategy.CompleteAlbum:
                    jobId = await CreateAlbumCompletionJobAsync(issues, ct);
                    break;
                    
                case RemediationStrategy.ReplaceWithCanonical:
                    jobId = await CreateCanonicalReplacementJobAsync(issues, ct);
                    break;
                    
                default:
                    throw new NotSupportedException($"Remediation strategy {strategy} not supported");
            }
            
            // Link job to issues
            await LinkJobToIssuesAsync(jobId, issueIds, ct);
            
            return jobId;
        }
        
        private async Task<string> CreateTrackRedownloadJobAsync(List<LibraryIssue> issues, CancellationToken ct)
        {
            // Create multi-source download job for each recording
            var recordingIds = issues
                .Where(i => !string.IsNullOrEmpty(i.MusicBrainzRecordingId))
                .Select(i => i.MusicBrainzRecordingId)
                .Distinct()
                .ToList();
            
            // Determine target directory (parent of first issue file)
            string targetDir = Path.GetDirectoryName(issues.First().FilePath);
            
            var job = new MultiSourceDownloadJob
            {
                TargetDirectory = targetDir,
                TargetRecordingIds = recordingIds,
                Constraints = new DownloadConstraints
                {
                    PreferCanonical = true,
                    PreferredCodecs = new[] { "FLAC" },
                    AllowLossy = false
                },
                ReplaceExisting = true  // Important: replace bad files
            };
            
            var jobId = await multiSourceDownloads.CreateJobAsync(job, ct);
            
            log.Information("[LH] Created track redownload job {JobId} for {Count} recordings",
                jobId, recordingIds.Count);
            
            return jobId;
        }
        
        private async Task<string> CreateAlbumCompletionJobAsync(List<LibraryIssue> issues, CancellationToken ct)
        {
            // Extract unique release IDs
            var releaseIds = issues
                .Where(i => !string.IsNullOrEmpty(i.MusicBrainzReleaseId))
                .Select(i => i.MusicBrainzReleaseId)
                .Distinct()
                .ToList();
            
            if (releaseIds.Count != 1)
            {
                throw new InvalidOperationException("Album completion requires all issues to be from the same release");
            }
            
            string releaseId = releaseIds.First();
            
            // Get missing track IDs from issue metadata
            var missingRecordingIds = new List<string>();
            foreach (var issue in issues)
            {
                if (issue.Metadata.TryGetValue("missing_tracks", out var missingObj))
                {
                    var missing = (List<object>)missingObj;
                    foreach (var track in missing)
                    {
                        var trackDict = (Dictionary<string, object>)track;
                        if (trackDict.TryGetValue("recording_id", out var recId))
                        {
                            missingRecordingIds.Add(recId.ToString());
                        }
                    }
                }
            }
            
            // Create MusicBrainz Release job for missing tracks
            string targetDir = issues.First().FilePath;  // Directory path
            
            var jobId = await musicBrainzClient.CreateReleaseJobAsync(new MBReleaseJobRequest
            {
                ReleaseId = releaseId,
                TargetDir = targetDir,
                Tracks = missingRecordingIds,  // Only download missing tracks
                Constraints = new DownloadConstraints
                {
                    PreferCanonical = true,
                    PreferredCodecs = new[] { "FLAC" }
                }
            }, ct);
            
            log.Information("[LH] Created album completion job {JobId} for release {ReleaseId}, {Count} missing tracks",
                jobId, releaseId, missingRecordingIds.Count);
            
            return jobId;
        }
        
        private async Task<string> CreateCanonicalReplacementJobAsync(List<LibraryIssue> issues, CancellationToken ct)
        {
            // Similar to track redownload but with explicit canonical preference
            return await CreateTrackRedownloadJobAsync(issues, ct);
        }
        
        public async Task LinkJobToIssuesAsync(string jobId, List<string> issueIds, CancellationToken ct)
        {
            foreach (var issueId in issueIds)
            {
                await healthService.UpdateIssueAsync(issueId, new LibraryIssueUpdate
                {
                    Status = LibraryIssueStatus.Fixing,
                    RemediationJobId = jobId
                }, ct);
            }
            
            // Subscribe to job completion to auto-resolve issues
            // (Implementation would use event bus or polling)
        }
    }
}
```

---

## 5. Implementation Checklist

### T-403: Library scan service

- [ ] Define issue taxonomy enums and models
- [ ] Create database schema for `LibraryHealthIssues` and `LibraryHealthScans`
- [ ] Implement `ILibraryHealthService` interface
- [ ] Implement `LibraryHealthService.ScanFileAsync()` core logic
- [ ] Implement transcode detection in scan
- [ ] Implement canonical upgrade detection
- [ ] Implement release completeness checking
- [ ] Add background scan scheduling
- [ ] Add unit tests for scan logic
- [ ] Add integration tests with sample library

### T-404: Library health UI/API

- [ ] Create `LibraryHealthController` API endpoints
- [ ] Implement summary aggregation (issue counts, health score)
- [ ] Create React `LibraryHealthDashboard` component
- [ ] Create React `IssueListView` component with filtering
- [ ] Create React `IssueDetailView` component
- [ ] Add real-time scan progress updates (SignalR or polling)
- [ ] Add issue status management (ignore, acknowledge)
- [ ] Add unit tests for API endpoints
- [ ] Add E2E tests for UI workflows

### T-405: Fix via multi-swarm

- [ ] Implement `ILibraryHealthRemediationService` interface
- [ ] Implement `CreateTrackRedownloadJobAsync()` logic
- [ ] Implement `CreateAlbumCompletionJobAsync()` logic
- [ ] Implement `CreateCanonicalReplacementJobAsync()` logic
- [ ] Add job-to-issue linking
- [ ] Subscribe to job completion events to auto-resolve issues
- [ ] Add "Fix" buttons to UI issue views
- [ ] Add bulk fix operations
- [ ] Add unit tests for remediation logic
- [ ] Add integration tests for job creation

---

## 6. Configuration Options

```yaml
library_health:
  enabled: true
  
  # Automatic scan scheduling
  auto_scan:
    enabled: false
    interval_hours: 168  # Weekly
    library_paths:
      - "/music/library1"
      - "/music/library2"
  
  # Issue detection thresholds
  thresholds:
    quality_gap_for_upgrade: 0.2  # Min quality difference to flag non-canonical
    duration_tolerance_ms: 3000   # Max duration mismatch before flagging
  
  # Remediation defaults
  remediation:
    replace_existing: true         # Replace bad files with fixes
    backup_before_replace: true    # Move old file to .bak before replacing
    auto_fix_transcodes: false     # Automatically fix detected transcodes
```

---

This comprehensive design provides everything Codex needs to implement the Library Health system!
