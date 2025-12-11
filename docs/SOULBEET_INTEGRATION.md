# Soulbeet ↔ slskdn Integration Guide

## Overview

This guide explains how Soulbeet (or any similar client) can integrate with slskdn to leverage advanced features while maintaining backwards compatibility with slskd.

---

## Detection

On startup, Soulbeet should call:

```python
GET /api/slskdn/capabilities
```

**Response codes**:
- **200 OK**: slskdn detected, enable advanced mode
- **404 Not Found**: vanilla slskd, use compat mode only

**Example detection code**:

```python
import requests

def detect_backend_capabilities(base_url, api_key):
    """Detect if backend is slskdn and what features are available."""
    try:
        response = requests.get(
            f"{base_url}/api/slskdn/capabilities",
            headers={"X-API-Key": api_key},
            timeout=5
        )
        
        if response.status_code == 200:
            data = response.json()
            return {
                "impl": data["impl"],
                "version": data["version"],
                "features": data["features"]
            }
        elif response.status_code == 404:
            # Vanilla slskd
            return {
                "impl": "slskd",
                "features": []
            }
    except Exception as e:
        print(f"Detection failed: {e}")
        return {
            "impl": "slskd",
            "features": []
        }

# Usage
caps = detect_backend_capabilities("http://localhost:5030", "your-api-key")
if caps["impl"] == "slskdn":
    print(f"slskdn detected! Version: {caps['version']}")
    print(f"Features: {', '.join(caps['features'])}")
else:
    print("Vanilla slskd detected")
```

---

## Compatibility Mode (No Changes Required)

Soulbeet works unchanged with slskdn using existing slskd API endpoints:

### Search
```python
POST /api/search
Content-Type: application/json

{
  "query": "Radiohead OK Computer",
  "type": "global",
  "limit": 200
}
```

### Download
```python
POST /api/downloads
Content-Type: application/json

{
  "items": [
    {
      "user": "some_user",
      "remote_path": "path/to/file.flac",
      "target_dir": "/downloads"
    }
  ]
}
```

### Status
```python
GET /api/downloads
GET /api/downloads/{id}
```

---

## Advanced Mode (slskdn-specific)

When slskdn is detected, Soulbeet can use enhanced features:

### 1. MusicBrainz Release Jobs

Instead of searching and downloading individual tracks, request an entire release by MBID:

```python
POST /api/jobs/mb-release
Content-Type: application/json

{
  "mb_release_id": "c0d0c0a4-4a26-4d74-9c02-67c9321b3b22",
  "target_dir": "/downloads/user1",
  "tracks": "all",
  "constraints": {
    "preferred_codecs": ["FLAC"],
    "allow_lossy": false,
    "prefer_canonical": true,
    "use_overlay": true
  }
}

# Response
{
  "job_id": "job_01HRQ4GSX9K47J7M5FK7VW8B12",
  "status": "pending"
}
```

**Benefits**:
- Multi-source chunked downloads
- Automatic quality selection (canonical scoring)
- Rescue mode for slow transfers
- Progress tracking per release

### 2. Discography Jobs

Download an artist's entire discography:

```python
POST /api/jobs/discography
Content-Type: application/json

{
  "artist_id": "a74b1b7f-71a5-4011-9441-d0b5e4122711",
  "profile": "CoreDiscography",  # or "ExtendedDiscography", "AllReleases"
  "target_dir": "/downloads/user1",
  "preferred_codecs": ["FLAC"],
  "allow_lossy": false,
  "prefer_canonical": true,
  "use_overlay": true
}
```

### 3. Label Crate Jobs

Download all releases from a specific label:

```python
POST /api/jobs/label-crate
Content-Type: application/json

{
  "label_name": "Warp Records",
  "target_dir": "/downloads/user1",
  "preferred_codecs": ["FLAC"],
  "allow_lossy": false
}
```

### 4. Job Status Polling

Monitor job progress:

```python
GET /api/jobs/{job_id}

# Response
{
  "id": "job_01HRQ4GSX9K47J7M5FK7VW8B12",
  "type": "discography",
  "status": "running",
  "spec": {
    "artist_id": "a74b1b7f-71a5-4011-9441-d0b5e4122711",
    "profile": "CoreDiscography"
  },
  "progress": {
    "releases_total": 15,
    "releases_done": 8,
    "releases_failed": 1
  },
  "created_at": "2025-12-10T15:30:00Z",
  "updated_at": "2025-12-10T15:45:12Z"
}
```

### 5. Library Health

Check for quality issues in user libraries:

```python
GET /api/slskdn/library/health?path=/music/user1&limit=100

# Response
{
  "path": "/music/user1",
  "summary": {
    "suspected_transcodes": 143,
    "non_canonical_variants": 57,
    "incomplete_releases": 27,
    "total_issues": 227
  },
  "issues": [
    {
      "type": "SuspectedTranscode",
      "file": "/music/user1/Artist/Album/Track.flac",
      "mb_recording_id": "e2f5e9b4-5852-4cd3-b1f9-29a7a4a234bc",
      "reason": "Spectral analysis suggests lossy source",
      "severity": "Warning"
    }
  ]
}
```

### 6. Warm Cache Hints

Hint popular content for prefetching:

```python
POST /api/slskdn/warm-cache/hints
Content-Type: application/json

{
  "mb_release_ids": ["c0d0c0a4-4a26-4d74-9c02-67c9321b3b22"],
  "mb_artist_ids": ["a74b1b7f-71a5-4011-9441-d0b5e4122711"],
  "mb_label_ids": ["f5bb60d4-cc90-4e30-911b-7c0cfdff1109"]
}

# Response
{
  "accepted": true
}
```

---

## Feature Flags

Check which features are enabled:

```python
caps = detect_backend_capabilities(base_url, api_key)

if "mbid_jobs" in caps["features"]:
    # Can use MB release jobs
    
if "discography_jobs" in caps["features"]:
    # Can use discography jobs
    
if "canonical_scoring" in caps["features"]:
    # Search results are ranked by quality
    
if "library_health" in caps["features"]:
    # Can check for quality issues
    
if "warm_cache" in caps["features"]:
    # Can submit popularity hints
```

---

## UI Integration Examples

### Example 1: Enhanced Album Request

```python
# In Soulbeet's album detail view
if backend_caps["impl"] == "slskdn" and "mbid_jobs" in backend_caps["features"]:
    # Show "Download with slskdn" button
    button_text = "Download Album (Multi-Source)"
    on_click = lambda: create_mb_release_job(album.mb_id)
else:
    # Show traditional search + download
    button_text = "Search & Download"
    on_click = lambda: search_and_download(album.title)
```

### Example 2: Discography View

```python
# In Soulbeet's artist detail view
if backend_caps["impl"] == "slskdn" and "discography_jobs" in backend_caps["features"]:
    # Add "Download Discography" section
    render_discography_download_ui(artist.mb_id)
```

### Example 3: Library Health Dashboard

```python
# In Soulbeet's user library view
if backend_caps["impl"] == "slskdn" and "library_health" in backend_caps["features"]:
    health = get_library_health(user.library_path)
    if health["summary"]["total_issues"] > 0:
        show_health_warning(health["summary"])
```

---

## Migration Strategy

### Phase 1: Detection Only
- Implement capability detection
- Log when slskdn is detected
- Continue using compat APIs

### Phase 2: Optional Enhanced Features
- Add UI for MBID jobs (opt-in)
- Users can choose traditional or enhanced workflow

### Phase 3: Full Integration
- Default to slskdn features when available
- Fall back to compat mode for vanilla slskd

---

## Error Handling

```python
def create_mb_release_job_safe(mb_id, target_dir):
    """Safely create MB release job with fallback."""
    try:
        if backend_caps["impl"] == "slskdn":
            # Try slskdn job API
            response = requests.post(
                f"{base_url}/api/jobs/mb-release",
                json={
                    "mb_release_id": mb_id,
                    "target_dir": target_dir
                },
                headers={"X-API-Key": api_key}
            )
            
            if response.status_code == 200:
                return response.json()["job_id"]
    except Exception as e:
        print(f"slskdn job failed: {e}, falling back to search")
    
    # Fallback to traditional search + download
    return search_and_download_release(mb_id, target_dir)
```

---

## API Reference Summary

### Compatibility APIs (slskd-compatible)
- `GET /api/info` - Server info
- `POST /api/search` - Search files
- `POST /api/downloads` - Create downloads
- `GET /api/downloads` - List downloads
- `GET /api/downloads/{id}` - Get download status

### Native slskdn APIs
- `GET /api/slskdn/capabilities` - Detect features
- `POST /api/jobs/mb-release` - Download MB release
- `POST /api/jobs/discography` - Download discography
- `POST /api/jobs/label-crate` - Download label releases
- `GET /api/jobs` - List jobs
- `GET /api/jobs/{id}` - Get job status
- `POST /api/slskdn/warm-cache/hints` - Submit cache hints
- `GET /api/slskdn/library/health` - Get library health

---

## Support

For questions or issues:
- GitHub: https://github.com/snapetech/slskdn
- Ensure you're using a compatible version of slskdn (check `/api/slskdn/capabilities`)

