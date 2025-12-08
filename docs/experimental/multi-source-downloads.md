# Multi-Source Chunked Downloads (Experimental)

## Overview

This feature enables downloading a single file from multiple Soulseek peers simultaneously by:
1. Verifying file identity through content hashing
2. Splitting the download into chunks
3. Downloading chunks in parallel from verified sources
4. Splicing chunks into the final file

This is a **replacement for auto-replace** that provides:
- Faster downloads (parallel sources)
- Resume capability (via byte offsets)
- Guaranteed exact matches (content hash verification)

## How It Works

### Phase 1: Source Discovery
```
Search: "Artist - Album - Track.flac"
        ↓
Filter: Exact match on filename, extension, AND size (0% variance)
        ↓
Result: [User1: 45.2MB, User2: 45.2MB, User3: 45.2MB, User4: 12.1MB]
                ↓                              ↓
        Candidates (same size)           Excluded (different size)
```

### Phase 2: Content Verification
```
For each candidate:
    1. Connect to peer
    2. Request 64KB chunk from offset (filesize / 2)
    3. SHA256 hash the chunk
    4. Disconnect
    
Result: {
    "a1b2c3...": [User1, User2, User3],  // 3 sources have identical content
    "d4e5f6...": [User5]                  // Different file, same name/size
}

Select largest group → [User1, User2, User3]
```

### Phase 3: Parallel Download
```
File size: 45.2MB
Chunk size: 15.1MB (size / 3)

User1: Download bytes 0 → 15,100,000
User2: Download bytes 15,100,000 → 30,200,000  
User3: Download bytes 30,200,000 → 45,200,000

All run in parallel via startOffset parameter
```

### Phase 4: Assembly
```
1. Wait for all chunks
2. Concatenate in order: chunk1 + chunk2 + chunk3
3. Verify final file hash matches expected
4. Move to completed downloads
```

## Technical Details

### Verification Strategy

#### FLAC Files (Optimal)
FLAC files contain an **MD5 hash of the raw audio** in the STREAMINFO block:
- **Location:** Bytes 8-41 of the file
- **Size needed:** Just **42 bytes**
- **What it hashes:** The unencoded audio samples (NOT metadata/tags)
- **Reliability:** 100% - two FLACs with same audio = same MD5

```
Bytes 0-3:   "fLaC" magic number
Bytes 4-7:   METADATA_BLOCK_HEADER  
Bytes 8-41:  STREAMINFO containing:
             - Audio parameters (sample rate, channels, etc.)
             - MD5 of raw audio data (16 bytes)
```

This means different tags, different encoders, different filenames - 
if the source audio is identical, the MD5 matches.

#### Other Formats (Fallback)
For MP3, WAV, OGG, M4A, etc. - no built-in audio hash:
- **Size:** 32KB (32,768 bytes)
- **Offset:** 0 (start of file)
- **Hash algorithm:** SHA256
- **Reliability:** High (32KB of audio data is unique)

### Soulseek Protocol Support
The Soulseek protocol supports `startOffset` in download requests:
```csharp
await Client.DownloadAsync(
    username: transfer.Username,
    remoteFilename: transfer.Filename,
    size: transfer.Size,
    startOffset: chunkStartByte,  // <-- THIS IS THE KEY
    ...
);
```

This is currently hardcoded to `0` but can be any byte offset.

### Matching Criteria (Strict)
For two sources to be considered "same file":
1. **Filename:** Exact match (case-sensitive)
2. **Extension:** Exact match
3. **Size:** Exact match (0 bytes variance)
4. **Content hash:** SHA256 of middle 64KB must match

### Chunk Allocation Strategy
```
N sources verified → Split file into N chunks
Each source downloads 1 chunk

If a source fails mid-download:
  - Reassign remaining bytes to other sources
  - Or fall back to single-source for that chunk
```

## Configuration Options

```yaml
multi_source:
  enabled: true
  min_sources: 2                    # Minimum sources to enable chunking
  verification_chunk_size: 65536    # 64KB
  verification_timeout_ms: 30000    # 30s to get verification chunk
  max_parallel_sources: 5           # Cap parallel connections
  fallback_to_single: true          # If verification fails, use single source
```

## API Endpoints

### GET /api/v0/transfers/downloads/{id}/sources
Get verified sources for a download.

```json
{
  "filename": "Artist - Track.flac",
  "size": 45200000,
  "verificationHash": "a1b2c3d4...",
  "sources": [
    {"username": "User1", "verified": true, "speed": 1200000},
    {"username": "User2", "verified": true, "speed": 800000},
    {"username": "User3", "verified": true, "speed": 2100000}
  ],
  "excluded": [
    {"username": "User5", "reason": "hash_mismatch"}
  ]
}
```

### POST /api/v0/transfers/downloads/{id}/multi-source
Enable multi-source for an existing queued download.

## Edge Cases

### Insufficient Sources
If only 1 source passes verification → fall back to single-source download

### Source Drops Mid-Transfer
- Track which bytes were received from each source
- Reassign incomplete ranges to remaining sources
- Support resume from exact byte offset

### Hash Mismatch
If middle chunks don't match but files have same size:
- Different encoding/transcode of same audio
- Log and exclude from multi-source
- Still allow single-source download (user's choice)

### Very Small Files
Files < 1MB: Skip verification, not worth the overhead

## Relationship to Auto-Replace

This feature **replaces** the current auto-replace system:

| Current Auto-Replace | Multi-Source Downloads |
|---------------------|------------------------|
| Finds alternative after failure | Finds alternatives proactively |
| Downloads from ONE new source | Downloads from MULTIPLE sources |
| No content verification | SHA256 verification |
| No resume capability | Full resume via offsets |
| Reactive (after stuck) | Proactive (before download) |

## Implementation Phases

### Phase 1: Verification Service
- [ ] Create `IContentVerificationService`
- [ ] Implement 64KB chunk download
- [ ] SHA256 hashing
- [ ] Source grouping by hash

### Phase 2: Chunked Download Manager
- [ ] Create `IChunkedDownloadService`
- [ ] Chunk allocation algorithm
- [ ] Parallel download orchestration
- [ ] Chunk assembly

### Phase 3: Integration
- [ ] Replace auto-replace toggle with multi-source toggle
- [ ] UI for source verification status
- [ ] Progress indicators per chunk

### Phase 4: Resilience
- [ ] Handle source failures mid-download
- [ ] Chunk reassignment
- [ ] Resume from partial state

## Risks & Mitigations

| Risk | Mitigation |
|------|------------|
| Verification overhead | Only verify if multiple sources available |
| Connection limits | Cap at 5 parallel sources |
| Bandwidth waste on verification | 64KB is tiny, abort on first mismatch |
| Protocol compatibility | Uses standard Soulseek protocol features |

## Success Metrics

- Download speed improvement (target: 2-3x with 3 sources)
- Verification accuracy (target: 99.9% correct matches)
- Resume success rate (target: 95%+)

