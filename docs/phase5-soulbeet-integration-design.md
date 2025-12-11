# Phase 5: Soulbeet Integration - Detailed Design

> **Tasks**: T-700 to T-712 (13 tasks)  
> **Branch**: `experimental/brainz`  
> **Dependencies**: Phases 1-4  
> **Estimated Duration**: 4-6 weeks

---

## Overview

Phase 5 creates a compatibility layer so slskdn can act as a drop-in replacement for slskd in Soulbeet deployments, while also exposing slskdn-native APIs for advanced features (MBID jobs, library health, warm cache hints).

---

## Background: Soulbeet Context

**Soulbeet** is a multi-user frontend that:
- Uses slskd's HTTP API for Soulseek search/downloads
- Monitors a shared downloads directory
- Uses Beets (`beet import`) to tag and organize music per user

**Integration Goals**:
1. **Compat Mode**: slskdn impersonates slskd's API (no Soulbeet changes)
2. **Advanced Mode**: Soulbeet detects slskdn and uses MBID job APIs
3. **Shared Responsibilities**:
   - Soulbeet: UI, user management, MB search, Beets integration
   - slskdn: Soulseek connectivity, multi-swarm, quality/canonical logic

---

## Phase 5A: slskd Compatibility Layer (T-700 to T-703)

### Task T-700: Implement GET /api/info

**Purpose**: Basic health/info endpoint.

#### Specification

```http
GET /api/info HTTP/1.1
X-API-Key: <key>
```

**Response**:
```json
{
  "impl": "slskdn",
  "compat": "slskd",
  "version": "0.1.0-multi-swarm",
  "soulseek": {
    "connected": true,
    "user": "your_username"
  }
}
```

#### Implementation

```csharp
namespace slskd.API.Compatibility
{
    [ApiController]
    [Route("api")]
    public class CompatibilityController : ControllerBase
    {
        private readonly ISoulseekClient soulseek;
        
        [HttpGet("info")]
        public IActionResult GetInfo()
        {
            return Ok(new
            {
                impl = "slskdn",
                compat = "slskd",
                version = Assembly.GetExecutingAssembly().GetName().Version.ToString(),
                soulseek = new
                {
                    connected = soulseek.State == SoulseekClientStates.Connected,
                    user = soulseek.Username
                }
            });
        }
    }
}
```

#### Implementation Checklist

- [ ] Create `CompatibilityController`
- [ ] Implement `/api/info` endpoint
- [ ] Return slskdn version + slskd compat marker
- [ ] Add integration test

---

### Task T-701: Implement POST /api/search

**Purpose**: Soulseek search compatible with Soulbeet.

#### Specification

```http
POST /api/search HTTP/1.1
X-API-Key: <key>
Content-Type: application/json

{
  "query": "Radiohead Paranoid Android",
  "type": "global",
  "limit": 200
}
```

**Response**:
```json
{
  "search_id": "9c9a7d0c-1d53-4fb3-8c16-3d3ad9fabd5f",
  "query": "Radiohead Paranoid Android",
  "results": [
    {
      "user": "some_soulseek_user",
      "speed_kbps": 420,
      "files": [
        {
          "path": "Radiohead/OK Computer/Paranoid Android.flac",
          "size_bytes": 41234567,
          "bitrate": 900,
          "length_ms": 388000,
          "ext": "flac"
        }
      ]
    }
  ]
}
```

#### Implementation

```csharp
namespace slskd.API.Compatibility
{
    [HttpPost("search")]
    public async Task<IActionResult> Search([FromBody] SearchRequest request, CancellationToken ct)
    {
        // Map to internal search service
        var search = await searchService.SearchAsync(new SearchOptions
        {
            Query = request.Query,
            Scope = request.Type == "global" ? SearchScope.Network : SearchScope.Room,
            FilterResponses = true,
            MaxResults = request.Limit ?? 200
        }, ct);
        
        // Optionally enrich with MBID/fingerprint rankings
        if (options.CurrentValue.Compatibility.EnrichSearchResults)
        {
            search.Responses = await EnrichWithCanonicalScoresAsync(search.Responses, ct);
        }
        
        // Map to slskd-compatible format
        return Ok(new
        {
            search_id = search.Id,
            query = request.Query,
            results = search.Responses.Select(r => new
            {
                user = r.Username,
                speed_kbps = r.UploadSpeed / 1024,
                files = r.Files.Select(f => new
                {
                    path = f.Filename,
                    size_bytes = f.Size,
                    bitrate = f.BitRate,
                    length_ms = f.Length * 1000,
                    ext = Path.GetExtension(f.Filename).TrimStart('.')
                })
            })
        });
    }
}
```

#### Implementation Checklist

- [ ] Implement `/api/search` endpoint
- [ ] Map slskdn search to slskd format
- [ ] Optional: enrich with canonical scores (reorder results)
- [ ] Add integration test with mock Soulseek

---

### Task T-702: Implement POST /api/downloads

**Purpose**: Create downloads from search results.

#### Specification

```http
POST /api/downloads HTTP/1.1
X-API-Key: <key>
Content-Type: application/json

{
  "items": [
    {
      "user": "some_soulseek_user",
      "remote_path": "Radiohead/OK Computer/Paranoid Android.flac",
      "target_dir": "/app/downloads",
      "target_filename": "Radiohead - Paranoid Android.flac"
    }
  ]
}
```

**Response**:
```json
{
  "download_ids": ["dwn_01HRQ3VJXG3E0VX6Y1D1JFGX1K"]
}
```

#### Implementation

```csharp
[HttpPost("downloads")]
public async Task<IActionResult> CreateDownloads([FromBody] DownloadRequest request, CancellationToken ct)
{
    var downloadIds = new List<string>();
    
    foreach (var item in request.Items)
    {
        // Create internal transfer
        var transfer = await transferService.EnqueueDownloadAsync(new DownloadOptions
        {
            Username = item.User,
            Filename = item.RemotePath,
            DestinationDirectory = item.TargetDir,
            DestinationFilename = item.TargetFilename
        }, ct);
        
        downloadIds.Add(transfer.Id);
        
        // Optionally: try to resolve MBID and upgrade to multi-swarm
        if (options.CurrentValue.Compatibility.AutoUpgradeToMultiSwarm)
        {
            _ = Task.Run(() => TryUpgradeToMultiSwarmAsync(transfer.Id, ct), ct);
        }
    }
    
    return Ok(new { download_ids = downloadIds });
}

private async Task TryUpgradeToMultiSwarmAsync(string transferId, CancellationToken ct)
{
    // Wait for some progress
    await Task.Delay(TimeSpan.FromSeconds(30), ct);
    
    // Try to fingerprint partial file and resolve MBID
    // If successful, convert to multi-swarm job
    // This is transparent to Soulbeet
}
```

#### Implementation Checklist

- [ ] Implement `/api/downloads` endpoint
- [ ] Map to internal transfer service
- [ ] Optional: auto-upgrade to multi-swarm (background)
- [ ] Add integration test

---

### Task T-703: Implement GET /api/downloads

**Purpose**: List active/known downloads.

#### Specification

```http
GET /api/downloads HTTP/1.1
X-API-Key: <key>
```

**Response**:
```json
{
  "downloads": [
    {
      "id": "dwn_01HRQ3VJXG3E0VX6Y1D1JFGX1K",
      "user": "some_soulseek_user",
      "remote_path": "Radiohead/OK Computer/Paranoid Android.flac",
      "local_path": "/app/downloads/Radiohead - Paranoid Android.flac",
      "status": "completed",
      "progress": 1.0,
      "bytes_total": 41234567,
      "bytes_transferred": 41234567,
      "error": null
    }
  ]
}
```

#### Implementation

```csharp
[HttpGet("downloads")]
public async Task<IActionResult> GetDownloads([FromQuery] string status, CancellationToken ct)
{
    var transfers = await transferService.GetAllDownloadsAsync(ct);
    
    // Filter by status if provided
    if (!string.IsNullOrEmpty(status))
    {
        transfers = transfers.Where(t => t.State.ToString().ToLowerInvariant() == status).ToList();
    }
    
    // Map to slskd format
    return Ok(new
    {
        downloads = transfers.Select(t => new
        {
            id = t.Id,
            user = t.Username,
            remote_path = t.Filename,
            local_path = Path.Combine(t.DestinationDirectory, t.DestinationFilename),
            status = MapStatus(t.State),
            progress = t.PercentComplete / 100.0,
            bytes_total = t.Size,
            bytes_transferred = t.BytesTransferred,
            error = t.Exception?.Message
        })
    });
}

private string MapStatus(TransferStates state)
{
    return state switch
    {
        TransferStates.Queued => "queued",
        TransferStates.InProgress => "running",
        TransferStates.Completed => "completed",
        TransferStates.Cancelled => "cancelled",
        _ => "failed"
    };
}
```

#### Implementation Checklist

- [ ] Implement `/api/downloads` endpoint
- [ ] Implement `/api/downloads/{id}` (single download)
- [ ] Map transfer states to slskd format
- [ ] Add integration test

---

## Phase 5B: slskdn-Native Job APIs (T-704 to T-708)

### Task T-704: Implement GET /api/slskdn/capabilities

**Purpose**: Allow Soulbeet to detect slskdn and enabled features.

#### Specification

```http
GET /api/slskdn/capabilities HTTP/1.1
X-API-Key: <key>
```

**Response**:
```json
{
  "impl": "slskdn",
  "version": "0.1.0-multi-swarm",
  "features": [
    "mbid_jobs",
    "discography_jobs",
    "label_crate_jobs",
    "canonical_scoring",
    "rescue_mode",
    "library_health",
    "warm_cache"
  ]
}
```

#### Implementation

```csharp
namespace slskd.API.Native
{
    [ApiController]
    [Route("api/slskdn")]
    public class CapabilitiesController : ControllerBase
    {
        [HttpGet("capabilities")]
        public IActionResult GetCapabilities()
        {
            var features = new List<string> { "mbid_jobs" };
            
            if (options.CurrentValue.Audio.CanonicalScoring.Enabled)
                features.Add("canonical_scoring");
            
            if (options.CurrentValue.Transfers.RescueMode.Enabled)
                features.Add("rescue_mode");
            
            if (options.CurrentValue.LibraryHealth.Enabled)
                features.Add("library_health");
            
            // ... more feature flags
            
            return Ok(new
            {
                impl = "slskdn",
                version = Assembly.GetExecutingAssembly().GetName().Version.ToString(),
                features
            });
        }
    }
}
```

#### Soulbeet Detection Logic

```python
# In Soulbeet client code
def detect_backend_capabilities(base_url, api_key):
    try:
        response = requests.get(
            f"{base_url}/api/slskdn/capabilities",
            headers={"X-API-Key": api_key}
        )
        
        if response.status_code == 200:
            return response.json()  # slskdn detected
        elif response.status_code == 404:
            return {"impl": "slskd", "features": []}  # Vanilla slskd
    except:
        return {"impl": "slskd", "features": []}
```

#### Implementation Checklist

- [ ] Implement `/api/slskdn/capabilities` endpoint
- [ ] Return feature flags based on config
- [ ] Document detection logic for Soulbeet developers
- [ ] Add integration test

---

### Task T-705: Implement POST /api/jobs/mb-release

**Purpose**: Download an MB Release.

(Already specified in Phase 4, just needs API endpoint)

```http
POST /api/jobs/mb-release HTTP/1.1
X-API-Key: <key>
Content-Type: application/json

{
  "mb_release_id": "c0d0c0a4-4a26-4d74-9c02-67c9321b3b22",
  "target_dir": "/app/downloads/queue/admin",
  "tracks": "all",
  "constraints": {
    "preferred_codecs": ["FLAC"],
    "allow_lossy": false,
    "prefer_canonical": true,
    "use_overlay": true
  }
}
```

**Response**:
```json
{
  "job_id": "job_01HRQ4GSX9K47J7M5FK7VW8B12",
  "status": "pending"
}
```

#### Implementation Checklist

- [ ] Create `JobsController` with `/api/jobs/mb-release`
- [ ] Map request to internal `IMusicBrainzJobService`
- [ ] Return job ID
- [ ] Add integration test

---

### Task T-706: Implement POST /api/jobs/discography

(Already specified in Phase 3)

#### Implementation Checklist

- [ ] Add `/api/jobs/discography` to `JobsController`
- [ ] Map to `IDiscographyJobService`
- [ ] Add integration test

---

### Task T-707: Implement POST /api/jobs/label-crate

(Already specified in Phase 3)

#### Implementation Checklist

- [ ] Add `/api/jobs/label-crate` to `JobsController`
- [ ] Map to `ILabelCrateJobService`
- [ ] Add integration test

---

### Task T-708: Implement GET /api/jobs and GET /api/jobs/{id}

**Purpose**: List/inspect jobs with common representation.

#### Specification

```http
GET /api/jobs?type=mb_release&status=running HTTP/1.1
X-API-Key: <key>
```

**Response**:
```json
{
  "jobs": [
    {
      "id": "job_01HRQ4GSX9K47J7M5FK7VW8B12",
      "type": "mb_release",
      "status": "running",
      "spec": {
        "mb_release_id": "c0d0c0a4-4a26-4d74-9c02-67c9321b3b22",
        "target_dir": "/app/downloads/queue/admin"
      },
      "progress": {
        "tracks_total": 10,
        "tracks_done": 4,
        "bytes_total": 512000000,
        "bytes_done": 256000000
      },
      "created_at": "2025-12-10T15:30:00Z",
      "updated_at": "2025-12-10T15:35:12Z",
      "error": null
    }
  ]
}
```

#### Implementation

```csharp
[HttpGet("jobs")]
public async Task<IActionResult> GetJobs(
    [FromQuery] string type,
    [FromQuery] string status,
    CancellationToken ct)
{
    var jobs = await jobService.GetAllJobsAsync(ct);
    
    // Filter
    if (!string.IsNullOrEmpty(type))
        jobs = jobs.Where(j => j.Type.ToString().ToLowerInvariant() == type).ToList();
    
    if (!string.IsNullOrEmpty(status))
        jobs = jobs.Where(j => j.Status.ToString().ToLowerInvariant() == status).ToList();
    
    return Ok(new { jobs = jobs.Select(MapJob) });
}

[HttpGet("jobs/{id}")]
public async Task<IActionResult> GetJob(string id, CancellationToken ct)
{
    var job = await jobService.GetJobAsync(id, ct);
    if (job == null) return NotFound();
    return Ok(MapJob(job));
}
```

#### Implementation Checklist

- [ ] Implement `/api/jobs` list endpoint with filters
- [ ] Implement `/api/jobs/{id}` detail endpoint
- [ ] Map polymorphic job types to common format
- [ ] Add pagination (optional)
- [ ] Add integration test

---

## Phase 5C: Optional Advanced APIs (T-709 to T-710)

### Task T-709: Implement POST /api/slskdn/warm-cache/hints

**Purpose**: Soulbeet can hint popular content for prefetching.

```http
POST /api/slskdn/warm-cache/hints HTTP/1.1
X-API-Key: <key>
Content-Type: application/json

{
  "mb_release_ids": ["c0d0c0a4-4a26-4d74-9c02-67c9321b3b22"],
  "mb_artist_ids": ["a74b1b7f-71a5-4011-9441-d0b5e4122711"],
  "mb_label_ids": ["f5bb60d4-cc90-4e30-911b-7c0cfdff1109"]
}
```

**Response**:
```json
{
  "accepted": true
}
```

#### Implementation

```csharp
[HttpPost("warm-cache/hints")]
public async Task<IActionResult> SubmitWarmCacheHints([FromBody] WarmCacheHintsRequest request, CancellationToken ct)
{
    if (!options.CurrentValue.Mesh.WarmCache.Enabled)
    {
        return BadRequest(new { error = "Warm cache not enabled" });
    }
    
    await warmCacheService.ProcessHintsAsync(request, ct);
    
    return Ok(new { accepted = true });
}
```

#### Implementation Checklist

- [ ] Implement `/api/slskdn/warm-cache/hints` endpoint
- [ ] Integrate with warm cache popularity scoring
- [ ] Add rate limiting (prevent abuse)
- [ ] Add integration test

---

### Task T-710: Implement GET /api/slskdn/library/health

**Purpose**: Expose library health summary.

```http
GET /api/slskdn/library/health?path=/music/admin HTTP/1.1
X-API-Key: <key>
```

**Response**:
```json
{
  "path": "/music/admin",
  "summary": {
    "suspected_transcodes": 143,
    "non_canonical_variants": 57,
    "incomplete_releases": 27
  },
  "issues": [
    {
      "type": "SuspectedTranscode",
      "file": "/music/admin/Artist/Album/Track01.flac",
      "mb_recording_id": "e2f5e9b4-5852-4cd3-b1f9-29a7a4a234bc",
      "reason": "Spectral analysis suggests lossy source at 128kbps"
    }
  ]
}
```

#### Implementation

```csharp
[HttpGet("library/health")]
public async Task<IActionResult> GetLibraryHealth([FromQuery] string path, CancellationToken ct)
{
    if (!options.CurrentValue.LibraryHealth.Enabled)
    {
        return BadRequest(new { error = "Library health not enabled" });
    }
    
    var summary = await libraryHealthService.GetSummaryAsync(path, ct);
    var issues = await libraryHealthService.GetIssuesAsync(new LibraryHealthIssueFilter
    {
        LibraryPath = path,
        Limit = 100
    }, ct);
    
    return Ok(new
    {
        path,
        summary = new
        {
            suspected_transcodes = summary.SuspectedTranscodes,
            non_canonical_variants = summary.NonCanonicalVariants,
            incomplete_releases = summary.IncompleteReleases
        },
        issues = issues.Select(i => new
        {
            type = i.Type.ToString(),
            file = i.FilePath,
            mb_recording_id = i.MusicBrainzRecordingId,
            reason = i.Reason
        })
    });
}
```

#### Implementation Checklist

- [ ] Implement `/api/slskdn/library/health` endpoint
- [ ] Add path parameter for scoping
- [ ] Return summary + issues
- [ ] Add integration test

---

## Phase 5D: Soulbeet Client Integration (T-711 to T-712)

### Task T-711: Document Soulbeet Modifications

**Purpose**: Guide Soulbeet developers on integrating slskdn.

#### Documentation

Create `docs/SOULBEET_INTEGRATION.md`:

```markdown
# Soulbeet ↔ slskdn Integration Guide

## Detection

On startup, Soulbeet should call:

```python
GET /api/slskdn/capabilities
```

- **If 200**: slskdn detected, enable advanced mode
- **If 404**: vanilla slskd, use compat mode only

## Advanced Mode Features

When slskdn is detected:

1. **MBID Jobs**: Use `/api/jobs/mb-release` instead of search + download
2. **Discography**: Offer "Download Artist Discography" button
3. **Library Health**: Show health issues from `/api/slskdn/library/health`
4. **Warm Cache Hints**: POST popular MBIDs to `/api/slskdn/warm-cache/hints`

## Compat Mode (No Changes)

Soulbeet works unchanged with slskdn using existing slskd API endpoints.
```

#### Implementation Checklist

- [ ] Create `SOULBEET_INTEGRATION.md` documentation
- [ ] Include Python code examples for detection
- [ ] Document all slskdn-native API endpoints
- [ ] Provide request/response examples
- [ ] Submit PR to Soulbeet repo (if possible)

---

### Task T-712: Create Soulbeet Integration Test Suite

**Purpose**: Ensure compat + advanced modes work correctly.

#### Test Scenarios

1. **Compat Mode**: Vanilla Soulbeet workflow
   - Search → Download → Complete → Beets import
   
2. **Advanced Mode**: slskdn-aware workflow
   - Capabilities detection
   - MBID job creation
   - Job status polling
   - Completion handling

#### Implementation

```csharp
namespace slskd.Tests.Integration.Soulbeet
{
    public class SoulbeetCompatibilityTests
    {
        [Fact]
        public async Task CompatMode_Search_And_Download_Should_Work()
        {
            // Arrange
            var client = CreateTestClient();
            
            // Act: Search
            var searchResponse = await client.PostAsJsonAsync("/api/search", new
            {
                query = "Test Artist",
                type = "global",
                limit = 10
            });
            
            var search = await searchResponse.Content.ReadFromJsonAsync<SearchResult>();
            Assert.NotNull(search.Results);
            
            // Act: Download
            var downloadResponse = await client.PostAsJsonAsync("/api/downloads", new
            {
                items = new[]
                {
                    new
                    {
                        user = search.Results[0].User,
                        remote_path = search.Results[0].Files[0].Path,
                        target_dir = "/tmp/downloads"
                    }
                }
            });
            
            var download = await downloadResponse.Content.ReadFromJsonAsync<DownloadResult>();
            Assert.NotEmpty(download.DownloadIds);
            
            // Assert: Status
            var statusResponse = await client.GetAsync($"/api/downloads/{download.DownloadIds[0]}");
            statusResponse.EnsureSuccessStatusCode();
        }
        
        [Fact]
        public async Task AdvancedMode_MbReleaseJob_Should_Work()
        {
            // Arrange
            var client = CreateTestClient();
            
            // Act: Detect capabilities
            var capsResponse = await client.GetAsync("/api/slskdn/capabilities");
            var caps = await capsResponse.Content.ReadFromJsonAsync<Capabilities>();
            Assert.Contains("mbid_jobs", caps.Features);
            
            // Act: Create MBID job
            var jobResponse = await client.PostAsJsonAsync("/api/jobs/mb-release", new
            {
                mb_release_id = "test-release-123",
                target_dir = "/tmp/downloads",
                tracks = "all"
            });
            
            var job = await jobResponse.Content.ReadFromJsonAsync<JobResult>();
            Assert.NotNull(job.JobId);
            
            // Assert: Job status
            var statusResponse = await client.GetAsync($"/api/jobs/{job.JobId}");
            statusResponse.EnsureSuccessStatusCode();
        }
    }
}
```

#### Implementation Checklist

- [ ] Create integration test project for Soulbeet compat
- [ ] Test compat mode (search, download, status)
- [ ] Test advanced mode (capabilities, MBID jobs)
- [ ] Test error handling (404s, rate limits)
- [ ] Add E2E test with mock Soulbeet client
- [ ] Document test scenarios in `tests/README.md`

---

## Configuration Summary

```yaml
compatibility:
  enabled: true
  enrich_search_results: true  # Reorder by canonical scores
  auto_upgrade_to_multi_swarm: true  # Background upgrade
  
soulbeet:
  enabled: true
  downloads_directory: "/app/downloads"
  shared_library_paths:
    - "/music/user1"
    - "/music/user2"
```

---

## Implementation Summary

**Phase 5 enables Soulbeet integration:**
- Compat layer for drop-in slskd replacement
- Native APIs for advanced features
- Documentation for Soulbeet developers
- Integration test suite

**Total tasks**: 13 (T-700 to T-712)  
**Estimated duration**: 4-6 weeks

---

## Testing Strategy

### Unit Tests
- API endpoint request/response mapping
- Status enum mapping
- Feature flag detection

### Integration Tests
- Full compat mode workflow
- Full advanced mode workflow
- Error handling

### E2E Tests
- Deploy slskdn + Soulbeet in Docker
- Run real-world scenarios
- Verify Beets integration

---

**ALL PHASES COMPLETE!**

Total documentation: **7 files**, **~25,000 lines** of specifications covering **all 75 tasks** (T-300 to T-712).

Ready for Codex to implement!


