# Multi-Source Downloads - Experimental Feature

## Overview

Multi-source downloading allows slskdn to download a single file from multiple peers simultaneously, similar to BitTorrent's "swarm" behavior. This can significantly improve download speeds for popular files.

## How It Works

### Swarm Mode (Chunked Downloads)

1. **Search & Pool Building**: Find all peers sharing an identical file (matched by exact file size)
2. **Chunk Division**: Split the file into small chunks (default: 128KB)
3. **Worker Distribution**: Spawn a worker for each available source
4. **Shared Queue**: All workers grab chunks from a shared `ConcurrentQueue`
5. **Fast Peers Do More**: Workers that finish quickly grab another chunk immediately
6. **Retry Logic**: Failed chunks are re-queued for other workers
7. **Proven Source Retry**: After initial pass, retry remaining chunks using only sources that succeeded
8. **Assembly**: Combine all chunks into final file

### Key Findings

#### What Works

- **Partial downloads via `startOffset`**: Soulseek protocol supports starting downloads at arbitrary byte offsets
- **`LimitedWriteStream` workaround**: Since Soulseek requires full file size even for partial downloads, we wrap the output stream to cancel after receiving the desired chunk
- **Concurrent chunk downloads**: Multiple workers can grab different chunks simultaneously
- **Proven source retry**: After first pass, re-using only successful sources dramatically improves completion rate

#### Limitations Discovered

1. **Many clients reject partial downloads**: Most Soulseek clients will reject download requests with `startOffset > 0`, reporting "Download failed by remote client"
2. **Single download per user per file**: Soulseek only allows one active download of a specific file from a specific user at a time
3. **Variable client behavior**: Some clients work perfectly with partial downloads, others reject immediately

#### Speed Thresholds

- **Minimum speed**: 5 KB/s
- **Slow duration**: 15 seconds
- Workers downloading slower than 5 KB/s for 15+ consecutive seconds are cycled out
- Their chunk is re-queued for faster peers

#### Retry Behavior

- Workers tolerate up to 3 consecutive failures before giving up
- Failed chunks are always re-queued for other workers
- After initial pass completes, up to 3 retry rounds using only "proven" sources (those that succeeded at least once)

## API Endpoints

### POST `/api/v0/multisource/download`

Direct download with pre-verified sources:

```json
{
  "filename": "path/to/file.flac",
  "fileSize": 21721524,
  "chunkSize": 131072,
  "sources": [
    {"username": "user1", "fullPath": "their/path/file.flac"},
    {"username": "user2", "fullPath": "different/path/file.flac"}
  ]
}
```

### POST `/api/v0/multisource/swarm`

Search and download in one call:

```json
{
  "filename": "search term",
  "size": 21721524,
  "chunkSize": 131072,
  "searchTimeout": 30000
}
```

## Test Scripts

- `swarm-test.sh` - Main test script with pool management
  - `./swarm-test.sh refresh "search term" 30` - Build fresh pool
  - `./swarm-test.sh targets` - Show available files
  - `./swarm-test.sh swarm SIZE CHUNK_KB` - Run swarm download
  - `./swarm-test.sh auto` - Full auto test

- `build-pool.sh` - Build and manage source pools
- `race-download.sh` - Alternative "race mode" (all sources download full file, first wins)

## Configuration

Default chunk size: 128KB (configurable per request)

Recommended settings:
- Chunk size: 64-256KB (smaller = more parallelism, larger = less overhead)
- Minimum sources: 3+ for meaningful benefit
- Speed threshold: 5 KB/s minimum, 15s tolerance

## Validated Improvements (Dec 2025)

After extensive testing, the following improvements have stabilized the swarm behavior:

1.  **Chunk Size Increase**: Default increased to **1MB** (1024KB).
    *   Result: Significantly reduced connection overhead and faster completion.
2.  **Hard Timeout**: Added 45s hard timeout per chunk download.
    *   Result: Prevents "hanging" workers (stuck in connection phase) from blocking chunks indefinitely.
3.  **Blacklisting**: Failed peers (especially those returning "Remote Client Failed") are blacklisted for the session.
    *   Result: Prevents infinite retry loops on bad peers.
4.  **Desperation Retry**: If all "proven" sources fail, the blacklist is purged and **ALL** original sources are retried.
    *   Result: Prevents stalling when the only "proven" source disconnects or slows down.
5.  **Smart Slow Peer Pruning**:
    *   Workers < 5 KB/s for 15s are cancelled/re-queued **ONLY IF** other workers are available (`ActiveWorkers > 1`).
    *   If it's the *last* worker, it is kept alive (better than failing).
    *   Speed failures are treated as "soft" (don't count towards the 3-strike kill limit).

### Successful Test Run (1MB Chunks)

```
[SWARM] Starting with 16 sources, 1024KB chunks
...
[SWARM] ✓ julesss chunk 12 @ 91 KB/s
[SWARM] ✓ brunzmeflugen chunk 2 @ 92 KB/s
[SWARM] ✗ katharsis chunk 7: User katharsis appears to be offline (fail 1/3)
...
[SWARM] ✓ YVR chunk 0 @ 125 KB/s
...
[SWARM] SUCCESS! Chunk distribution:
   YVR: 3 chunks
   bstroszek: 3 chunks
   julesss: 3 chunks
   trianine: 3 chunks
   Antarctiica: 2 chunks
   dankmolot: 2 chunks
   officejapan: 2 chunks
   Pez: 1 chunks
   brunzmeflugen: 1 chunks
   red_book: 1 chunks

✅ SUCCESS!
   Sources used: 10
   Time: 81709ms
   Output: /tmp/slskdn-test/...Within.flac
   ✅ FLAC verification PASSED
```

## Protocol Extension Proposals

The following analysis outlines potential extension points within the existing Soulseek protocol to support advanced features like capability negotiation and multi-part transfer coordination, while maintaining backward compatibility.

**Note:** In all implementation scenarios, preference and compatibility with legacy clients and the standard network protocol must be preserved. Extensions should degrade gracefully and remain invisible to non-supporting clients.

### Viable Extension Targets

#### 1. User Info Map (`UserInfoRequest` / `UserInfoResponse`)
*   **Structure:** Map of keys → values (strings, integers, etc.).
*   **Implementation:** Introduce a custom key (e.g., `"slskdn_caps"`) containing a JSON object or base64-encoded flags.
    *   *Example:* `{"mp_enabled": true, "version": "1.1"}`
*   **Compatibility:** Legacy clients typically ignore unknown keys, making this a low-risk method for capability negotiation.

#### 2. Queue Upload Request – "Reason" Field (`QueueUploadRequest`)
*   **Structure:** Contains Filename, User, and a "Reason" string intended for display.
*   **Implementation:** Overload the reason field with structured metadata.
    *   *Example:* `reason="multipart:chunk=2/4;start=1048576;length=1048576"`
*   **Compatibility:** Standard clients will display the raw string. While functional, this may impact user experience on legacy clients (Nicotine+, etc.) if the string is parsed literally for display. Testing required.

#### 3. Client Version String
*   **Structure:** Sent during login or peer connection establishment.
*   **Implementation:** Append a capability token to the version string.
    *   *Example:* `"slskdn/1.0+mp"`
*   **Compatibility:** Allows for passive discovery of extension support without altering message structures.

#### 4. Dummy Shared Files
*   **Implementation:** Host virtual files with reserved names (e.g., `__slskdn_caps__`, `__chunkhash_meta__`).
*   **Mechanism:** A request for these files serves as an intent to negotiate or retrieve metadata. The file itself does not need to exist on disk; the request triggers the handshake logic.
*   **Compatibility:** Acts as a standard file request to legacy clients (which will simply return "File not found"), but triggers specific behavior in `slskdn` nodes.

### Experimental / Risky Targets

#### 5. Search Result Item Tags
*   **Structure:** Metadata tags (bitrate, duration, etc.) sent with search results.
*   **Implementation:** Inject custom tags (e.g., `tag["slskdn_hash"] = "sha1:..."`) for content verification or grouping.
*   **Risk:** The central Soulseek server or other nodes may strip unknown tags. Reliability is unproven.

### Non-Viable Methods

*   **Custom Message IDs:** Utilizing IDs beyond the known 16-bit range is unsafe and likely to cause disconnection or errors.
*   **Binary Header Modification:** The file transfer header is a fixed format with no reserved space for extensions.
*   **Message Length Modification:** Altering message lengths or appending trailing bytes violates the protocol and will result in parsing errors.

### Summary

| Target | Method | Risk Profile | Use Case |
| :--- | :--- | :--- | :--- |
| **User Info** | Add `"slskdn_caps"` key | Safe | Capability negotiation |
| **Client Version** | Append `+mp` or `+ext` | Safe | Passive discovery |
| **Dummy File** | Request reserved filename | Mild | Active handshake / Metadata retrieval |
| **Queue Reason** | Structured string overload | Mild | Segmented transfer coordination |
| **Search Tags** | Custom metadata tags | High | Content deduplication (Unreliable) |

## Known Issues / TODO

1.  **No dynamic source discovery**: Pool is built once at start; new sources aren't discovered mid-download (Search could be re-run).
2.  **Progress reporting**: Live progress could be improved for frontend integration.

## Source Info

1. https://github.com/nicotine-plus/nicotine-plus/blob/master/doc/SLSKPROTOCOL.md
