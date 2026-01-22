# MusicBrainz Mesh: Chunk Transfer Protocol

> **Status**: Experimental Protocol Design  
> **Branch**: `experimental/brainz`  
> **Version**: 1.0-draft  
> **Depends on**: [BRAINZ_PROTOCOL_SPEC.md](./BRAINZ_PROTOCOL_SPEC.md)

This document defines the **data plane** chunk transfer messages for MusicBrainz-aware mesh transfers.

These messages implement the actual byte-range transfers negotiated via the control-plane messages (`MbidSwarmDescriptor`, `FingerprintBundleAdvert`, `MeshCacheJob`).

---

## Protocol Overview

### Transport Layer

- **Connection**: TLS TCP stream (existing overlay infrastructure)
- **Framing**: Line-delimited JSON (newline-separated)
- **Character Encoding**: UTF-8
- **Binary Data**: Base64-encoded within JSON (future: binary framing)

### Design Principles

1. **Byte-range addressing**: Support arbitrary chunk sizes (not fixed blocks)
2. **Out-of-order delivery**: Allow sparse/parallel chunk requests
3. **Priority-aware**: Downloader can signal urgency
4. **Cancellable**: Free resources when swarm rebalances
5. **Error-resilient**: Explicit error codes for rate limits, missing data

---

## Message Envelope (Recap)

All chunk transfer messages use the same envelope as control-plane messages.

### JSON Structure

```jsonc
{
  "type": "chunk_request",  // or chunk_response, chunk_cancel
  "version": 1,
  "message_id": "8c6c43b8-65a8-4b0d-8f4d-7b02bdc1e2b1",
  "sender_id": "mesh-node-abc123",
  "timestamp": "2025-12-09T23:50:00.000Z",
  "payload": { /* type-specific */ }
}
```

### C# Implementation

```csharp
namespace slskd.Mesh.Brainz
{
    /// <summary>
    /// Common envelope for all mesh messages (control + data plane).
    /// </summary>
    public sealed record MeshEnvelope<TPayload>
    (
        string Type,
        int Version,
        Guid MessageId,
        string SenderId,
        DateTimeOffset Timestamp,
        TPayload Payload
    );
}
```

---

## Message Type: chunk_request

### Purpose

Downloader → Uploader: "Please send me this byte range for that audio variant."

### When to Send

- When starting a new transfer (first chunk)
- When requesting additional chunks (parallel/pipelined)
- When retrying after timeout or error
- When filling gaps in sparse download

### Design Notes

- **`transfer_id`**: Groups all chunks for one logical transfer between two peers
- **`variant_id`**: References `AudioVariant.VariantId` from `FingerprintBundleAdvert`
- **Byte-based ranges**: Support arbitrary chunk sizes (e.g., 256 KiB, 512 KiB)
- **Priority hints**: Allow swarm scheduler to prioritize critical chunks

### Payload Schema

```jsonc
{
  // Transfer identification
  "transfer_id": "c60aee88-9ac6-4726-b0cb-53cda342b11b",
  "job_id": "0f4de638-56f3-4a0f-b8fa-64f85c6b6a8f",  // optional: link to MeshCacheJob
  
  // Content identification
  "mb_recording_id": "e2f5e9b4-5852-4cd3-b1f9-29a7a4a234bc",
  "mb_release_id": "c0d0c0a4-4a26-4d74-9c02-67c9321b3b22",  // optional
  "variant_id": "local-5c1a6d13a28f",  // from AudioVariant
  
  // Byte range
  "offset_bytes": 0,
  "length_bytes": 262144,  // 256 KiB example
  
  // Scheduling hints
  "priority": 5,        // 0 (lowest) – 10 (highest)
  "deadline_ms": 5000,  // Soft deadline hint
  
  // Transfer preferences
  "hints": {
    "allow_sparse": true,         // OK with out-of-order chunks
    "prefer_contiguous": false,   // Or true for streaming
    "end_of_file_known": true     // Requester knows total file size
  }
}
```

### C# Data Model

```csharp
namespace slskd.Mesh.Brainz
{
    /// <summary>
    /// Request for a specific byte range of an audio variant.
    /// </summary>
    public sealed record ChunkRequest
    (
        Guid TransferId,
        Guid? JobId,
        string? MbRecordingId,
        string? MbReleaseId,
        string VariantId,
        long OffsetBytes,
        int LengthBytes,
        int Priority,
        int? DeadlineMs,
        ChunkRequestHints Hints
    );

    /// <summary>
    /// Hints about how the requester will process the chunk.
    /// </summary>
    public sealed record ChunkRequestHints
    (
        bool AllowSparse,
        bool PreferContiguous,
        bool EndOfFileKnown
    );
}
```

### Example Message

```jsonc
{
  "type": "chunk_request",
  "version": 1,
  "message_id": "8c6c43b8-65a8-4b0d-8f4d-7b02bdc1e2b1",
  "sender_id": "mesh-node-downloader",
  "timestamp": "2025-12-09T23:50:00.123Z",
  "payload": {
    "transfer_id": "c60aee88-9ac6-4726-b0cb-53cda342b11b",
    "job_id": "0f4de638-56f3-4a0f-b8fa-64f85c6b6a8f",
    "mb_recording_id": "e2f5e9b4-5852-4cd3-b1f9-29a7a4a234bc",
    "variant_id": "local-5c1a6d13a28f",
    "offset_bytes": 0,
    "length_bytes": 262144,
    "priority": 5,
    "hints": {
      "allow_sparse": true,
      "prefer_contiguous": false,
      "end_of_file_known": true
    }
  }
}
```

### Implementation Notes

- **Max chunk size**: Enforce 256 KiB or 512 KiB limit to prevent memory exhaustion
- **Rate limiting**: Limit outstanding requests per peer (e.g., 4-8 in-flight)
- **Timeout**: If no response within `deadline_ms * 2`, retry or switch peer
- **Priority scheduling**: Uploader should service higher-priority requests first

---

## Message Type: chunk_response

### Purpose

Uploader → Downloader: "Here's the data you requested (or an error)."

### When to Send

- In response to `chunk_request`
- When chunk data is ready
- When chunk cannot be served (error condition)
- When signaling end-of-file or end-of-transfer

### Design Notes

- **Mirrors request**: Same `transfer_id`, `variant_id`, `offset_bytes`, `length_bytes`
- **Base64 encoding**: For JSON framing (future: binary framing)
- **EOF/complete flags**: Signal end conditions
- **Error codes**: Explicit failure reasons

### Payload Schema (Success)

```jsonc
{
  // Transfer identification
  "transfer_id": "c60aee88-9ac6-4726-b0cb-53cda342b11b",
  "variant_id": "local-5c1a6d13a28f",
  
  // Byte range
  "offset_bytes": 0,
  "length_bytes": 262144,
  
  // Payload data
  "data_base64": "base64-encoded-bytes-here...",
  
  // Transfer status
  "eof": false,            // true if this chunk reaches file end
  "complete": false,       // true if sender considers transfer finished
  
  // Verification
  "file_size_bytes": 34567890,      // optional, for sanity check
  "chunk_sha256": "sha256:abcd...", // optional integrity for this chunk
  
  // Error handling
  "error_code": null,
  "error_message": null
}
```

### Payload Schema (Error)

```jsonc
{
  "transfer_id": "c60aee88-9ac6-4726-b0cb-53cda342b11b",
  "variant_id": "local-5c1a6d13a28f",
  "offset_bytes": 1048576,
  "length_bytes": 262144,
  
  // No data
  "data_base64": null,
  "eof": false,
  "complete": false,
  
  // Verification
  "file_size_bytes": 34567890,
  "chunk_sha256": null,
  
  // Error details
  "error_code": "rate_limited",
  "error_message": "Overlay upload bandwidth exceeded (2000 kbps limit)"
}
```

### Error Codes

| Code | Meaning | Downloader Action |
|------|---------|-------------------|
| `rate_limited` | Bandwidth quota exceeded | Retry with backoff or switch peer |
| `not_found` | Variant no longer available | Switch to different peer/variant |
| `io_error` | Disk read failed | Switch to different peer |
| `policy_violation` | Request violates fairness policy | Reduce priority or cancel |
| `timeout` | Request took too long to process | Retry with smaller chunk |
| `invalid_range` | Offset/length out of bounds | Fix range and retry |

### C# Data Model

```csharp
namespace slskd.Mesh.Brainz
{
    /// <summary>
    /// Response containing chunk data or error.
    /// </summary>
    public sealed record ChunkResponse
    (
        Guid TransferId,
        string VariantId,
        long OffsetBytes,
        int LengthBytes,
        string? DataBase64,
        bool Eof,
        bool Complete,
        long? FileSizeBytes,
        string? ChunkSha256,
        string? ErrorCode,
        string? ErrorMessage
    );
}
```

### Example Message (Success)

```jsonc
{
  "type": "chunk_response",
  "version": 1,
  "message_id": "a1b2c3d4-e5f6-4789-a012-3456789abcde",
  "sender_id": "mesh-node-uploader",
  "timestamp": "2025-12-09T23:50:01.456Z",
  "payload": {
    "transfer_id": "c60aee88-9ac6-4726-b0cb-53cda342b11b",
    "variant_id": "local-5c1a6d13a28f",
    "offset_bytes": 0,
    "length_bytes": 262144,
    "data_base64": "UklGRiQAAABXQVZFZm10IBAAAA...",
    "eof": false,
    "complete": false,
    "file_size_bytes": 34567890,
    "chunk_sha256": "sha256:1a2b3c4d...",
    "error_code": null,
    "error_message": null
  }
}
```

### Implementation Notes

- **Assembly**: Downloader maintains a sparse bitmap of received ranges
- **Verification**: If `chunk_sha256` provided, verify before writing to disk
- **EOF detection**: When `eof: true` or `offset_bytes + length_bytes == file_size_bytes`
- **Completion**: When all requested ranges are filled or `complete: true` received
- **Error handling**: On error, downloader's swarm scheduler decides retry strategy

---

## Message Type: chunk_cancel

### Purpose

Downloader → Uploader: "Stop sending chunks for this transfer (or these ranges)."

### When to Send

- Swarm rebalanced (got bytes from faster peer)
- User cancelled download
- Transfer timed out
- Switching to better variant/source

### Design Notes

- **Selective cancellation**: Can cancel specific ranges or entire transfer
- **Resource cleanup**: Allows uploader to free slots/bandwidth immediately
- **Reason codes**: Help with debugging and mesh health metrics

### Payload Schema

```jsonc
{
  // Transfer identification
  "transfer_id": "c60aee88-9ac6-4726-b0cb-53cda342b11b",
  "variant_id": "local-5c1a6d13a28f",
  
  // Cancellation reason
  "reason_code": "swarm_rebalanced",  // See table below
  "reason_message": "Chunk filled by another peer (120 KB/s vs 45 KB/s)",
  
  // Optional: specific ranges to cancel
  // If null/empty, cancel entire transfer
  "ranges": [
    {
      "offset_bytes": 0,
      "length_bytes": 524288
    },
    {
      "offset_bytes": 1048576,
      "length_bytes": 262144
    }
  ]
}
```

### Reason Codes

| Code | Meaning |
|------|---------|
| `swarm_rebalanced` | Got bytes from faster/better peer |
| `user_cancelled` | User manually cancelled download |
| `timeout` | Transfer stalled, switching peer |
| `error` | Local error (disk full, etc.) |
| `quality_upgrade` | Switching to better variant (lossless, etc.) |
| `policy_change` | Local fairness policy changed |
| `complete` | Transfer completed successfully |

### C# Data Model

```csharp
namespace slskd.Mesh.Brainz
{
    /// <summary>
    /// Request to cancel chunks for a transfer.
    /// </summary>
    public sealed record ChunkCancel
    (
        Guid TransferId,
        string VariantId,
        string ReasonCode,
        string? ReasonMessage,
        IReadOnlyList<CancelledRange>? Ranges  // null/empty = cancel entire transfer
    );

    /// <summary>
    /// Specific byte range to cancel.
    /// </summary>
    public sealed record CancelledRange
    (
        long OffsetBytes,
        int LengthBytes
    );
}
```

### Example Message

```jsonc
{
  "type": "chunk_cancel",
  "version": 1,
  "message_id": "f1e2d3c4-b5a6-4798-0123-456789fedcba",
  "sender_id": "mesh-node-downloader",
  "timestamp": "2025-12-09T23:51:30.789Z",
  "payload": {
    "transfer_id": "c60aee88-9ac6-4726-b0cb-53cda342b11b",
    "variant_id": "local-5c1a6d13a28f",
    "reason_code": "swarm_rebalanced",
    "reason_message": "Chunk filled by another peer (120 KB/s vs 45 KB/s)",
    "ranges": [
      {
        "offset_bytes": 0,
        "length_bytes": 524288
      }
    ]
  }
}
```

### Implementation Notes

- **Uploader cleanup**: If `ranges` is null/empty, free entire transfer state
- **Selective cleanup**: If `ranges` provided, mark those ranges as cancelled and stop outstanding responses
- **In-flight chunks**: Complete any chunks already being sent, but don't start new ones
- **Metrics**: Track cancellation reasons for mesh health monitoring

---

## Integration with Control-Plane Messages

### Full Protocol Flow

```
┌─────────────────────────────────────────────────────────────┐
│                   Control Plane Messages                     │
├─────────────────────────────────────────────────────────────┤
│ 1. FingerprintBundleAdvert (epidemic sync)                 │
│    → "I have variant_id X for MB Recording Y"               │
│                                                              │
│ 2. MbidSwarmDescriptor (query response)                     │
│    → "This variant is lossless FLAC, quality 0.97"          │
│                                                              │
│ 3. MeshCacheJob (request)                                   │
│    → "I want tracks 2 & 7, lossless, constraints..."        │
│                                                              │
│ 4. MeshCacheJob (offer)                                     │
│    → "I can serve track 2 as variant_id X"                  │
└─────────────────────────────────────────────────────────────┘
                          ↓
┌─────────────────────────────────────────────────────────────┐
│                    Data Plane Messages                       │
├─────────────────────────────────────────────────────────────┤
│ 5. chunk_request                                            │
│    → "Send me bytes 0-262144 of variant_id X"              │
│                                                              │
│ 6. chunk_response                                           │
│    → "Here's the data (base64)"                             │
│                                                              │
│ 7. chunk_request (more ranges)                             │
│    → "Send me bytes 262144-524288"                          │
│                                                              │
│ 8. chunk_response                                           │
│    → "Here's more data"                                     │
│                                                              │
│ 9. chunk_cancel (optional)                                  │
│    → "Stop, I got it from someone else"                     │
└─────────────────────────────────────────────────────────────┘
```

### Mapping Control → Data Plane

```csharp
// After receiving MeshCacheJob (offer):
var offer = meshCacheJob.OfferedTracks[0];  // Track 2
var variantId = offer.VariantId;            // "local-5c1a6d13a28f"

// Start chunk transfer:
var transferId = Guid.NewGuid();
var chunkSize = 262144;  // 256 KiB

for (long offset = 0; offset < fileSize; offset += chunkSize)
{
    var request = new ChunkRequest(
        TransferId: transferId,
        JobId: meshCacheJob.JobId,
        MbRecordingId: offer.MbRecordingId,
        MbReleaseId: meshCacheJob.MbReleaseId,
        VariantId: variantId,
        OffsetBytes: offset,
        LengthBytes: (int)Math.Min(chunkSize, fileSize - offset),
        Priority: 5,
        DeadlineMs: 5000,
        Hints: new ChunkRequestHints(
            AllowSparse: true,
            PreferContiguous: false,
            EndOfFileKnown: true
        )
    );
    
    await SendChunkRequestAsync(peer, request);
}
```

### Fairness Enforcement

```csharp
// Uploader enforces policies from control plane:
var descriptor = await GetSwarmDescriptorAsync(mbReleaseId);
var policy = descriptor.Policies;

// Before processing chunk_request:
if (_activeOverlaySlots >= policy.MaxOverlayUploadSlots)
{
    return new ChunkResponse(
        // ... other fields ...
        ErrorCode: "rate_limited",
        ErrorMessage: $"Max overlay slots ({policy.MaxOverlayUploadSlots}) exceeded"
    );
}

if (_currentOverlayBandwidth >= policy.MaxOverlayBandwidthKbps * 1024)
{
    return new ChunkResponse(
        // ... other fields ...
        ErrorCode: "rate_limited",
        ErrorMessage: $"Max overlay bandwidth ({policy.MaxOverlayBandwidthKbps} kbps) exceeded"
    );
}

// Serve chunk...
```

---

## State Machines

### Downloader State Machine

```
┌─────────┐
│   NEW   │
└────┬────┘
     │ Receive MeshCacheJob (offer)
     ↓
┌──────────────┐
│  NEGOTIATED  │
└──────┬───────┘
       │ Send first chunk_request
       ↓
┌──────────────┐     chunk_response (error)
│  REQUESTING  ├──────────────────────────┐
└──────┬───────┘                          │
       │ All ranges filled                │
       │ or eof/complete received         │
       ↓                                  ↓
┌──────────────┐                    ┌──────────┐
│  COMPLETING  │                    │  ABORTED │
└──────┬───────┘                    └────┬─────┘
       │ Verification done                │
       ↓                                  │
┌──────────────┐                          │
│     DONE     │◄─────────────────────────┘
└──────────────┘
```

### Uploader State Machine

```
┌──────────┐
│   IDLE   │
└────┬─────┘
     │ Send MeshCacheJob (offer)
     ↓
┌──────────────┐
│   OFFERED    │
└──────┬───────┘
       │ Receive chunk_request
       ↓
┌──────────────┐     chunk_cancel or error
│   SERVING    ├──────────────────────────┐
└──────┬───────┘                          │
       │ All requested ranges sent        │
       │ or send complete: true           │
       ↓                                  ↓
┌──────────────┐                    ┌──────────┐
│   TEARDOWN   │                    │ TEARDOWN │
└──────┬───────┘                    └────┬─────┘
       │ Cleanup state                    │
       ↓                                  │
┌──────────────┐                          │
│     IDLE     │◄─────────────────────────┘
└──────────────┘
```

---

## Performance Considerations

### Chunk Size Selection

```csharp
// Adaptive chunk size based on observed bandwidth
public int GetOptimalChunkSize(PeerStatistics stats)
{
    var avgSpeedKbps = stats.RecentSpeedKbps;
    
    // Target: each chunk should take ~1-2 seconds to transfer
    if (avgSpeedKbps < 100)        return 32768;    // 32 KiB
    else if (avgSpeedKbps < 500)   return 131072;   // 128 KiB
    else if (avgSpeedKbps < 2000)  return 262144;   // 256 KiB
    else                           return 524288;   // 512 KiB (max)
}
```

### Pipelining

```csharp
// Keep N chunks in-flight per peer
public const int MaxInFlightChunks = 4;

// Send next chunk_request before previous chunk_response arrives
while (HasMoreRanges() && _inFlightChunks.Count < MaxInFlightChunks)
{
    var range = GetNextRange();
    await SendChunkRequestAsync(peer, range);
    _inFlightChunks.Add(range);
}
```

### Priority Scheduling

```csharp
// Prioritize:
// 1. First/last chunks (for early playback / completion)
// 2. Gaps in nearly-complete files
// 3. Requests with high explicit priority
public int CalculatePriority(ChunkRequest req, TransferState state)
{
    var priority = req.Priority;  // Base priority
    
    // Boost first/last 1 MB
    if (req.OffsetBytes == 0 || 
        req.OffsetBytes + req.LengthBytes >= state.FileSize)
    {
        priority += 3;
    }
    
    // Boost if file is >90% complete
    if (state.CompletionPercent > 0.9)
    {
        priority += 2;
    }
    
    return Math.Min(priority, 10);  // Cap at 10
}
```

---

## Security & Abuse Prevention

### Rate Limiting

- **Per-peer limits**: Max 4-8 in-flight requests per peer
- **Global limits**: Total overlay bandwidth cap (from `SwarmPolicy`)
- **Request throttling**: Reject requests if queue is full

### Validation

```csharp
// Validate chunk_request before processing
public bool ValidateChunkRequest(ChunkRequest req)
{
    // 1. Variant exists and is available
    if (!_localVariants.ContainsKey(req.VariantId))
        return false;
    
    // 2. Range is valid
    var variant = _localVariants[req.VariantId];
    if (req.OffsetBytes < 0 || 
        req.OffsetBytes + req.LengthBytes > variant.FileSizeBytes)
        return false;
    
    // 3. Chunk size is reasonable
    if (req.LengthBytes > 524288)  // 512 KiB max
        return false;
    
    return true;
}
```

### Verification

```csharp
// Verify chunk_response integrity
public async Task<bool> VerifyChunkAsync(ChunkResponse resp)
{
    if (string.IsNullOrEmpty(resp.ChunkSha256))
        return true;  // No verification requested
    
    var data = Convert.FromBase64String(resp.DataBase64);
    var computed = await ComputeSha256Async(data);
    
    return computed == resp.ChunkSha256;
}
```

---

## Future Extensions

### Binary Framing

Replace JSON + base64 with length-prefixed binary protocol:

```
┌────────────────────────────────────────┐
│ Magic (4 bytes): "MBCH"                │
├────────────────────────────────────────┤
│ Version (2 bytes): 0x0001              │
├────────────────────────────────────────┤
│ Type (2 bytes): 0x0001 (chunk_request) │
├────────────────────────────────────────┤
│ Payload Length (4 bytes): N            │
├────────────────────────────────────────┤
│ Payload (N bytes): Protobuf/MessagePack│
└────────────────────────────────────────┘
```

Benefits: ~30% bandwidth reduction, faster parsing

### Compression

```jsonc
{
  "payload": {
    // ... other fields ...
    "data_base64": "...",
    "data_encoding": "zstd",  // or "lz4", "gzip"
    "data_original_length": 262144
  }
}
```

### Streaming

```csharp
// For real-time playback:
public sealed record ChunkRequestHints
(
    bool AllowSparse,
    bool PreferContiguous,
    bool EndOfFileKnown,
    bool StreamingMode  // NEW: prioritize sequential delivery
);
```

---

## References

- [BRAINZ_PROTOCOL_SPEC.md](./BRAINZ_PROTOCOL_SPEC.md) - Control-plane messages
- [MULTI_SOURCE_DOWNLOADS.md](./docs/archive/duplicates/MULTI_SOURCE_DOWNLOADS.md) - Multi-source swarm design
- [RFC 7233](https://tools.ietf.org/html/rfc7233) - HTTP Range Requests (inspiration)

---

*Last updated: 2025-12-09*

