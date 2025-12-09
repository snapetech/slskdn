# MusicBrainz Mesh: State Machines

> **Status**: Experimental Protocol Design  
> **Branch**: `experimental/brainz`  
> **Version**: 1.0-draft  
> **Depends on**: [BRAINZ_PROTOCOL_SPEC.md](./BRAINZ_PROTOCOL_SPEC.md), [BRAINZ_CHUNK_TRANSFER.md](./BRAINZ_CHUNK_TRANSFER.md)

This document defines the **state machines** for downloader and uploader sides of MusicBrainz-aware mesh transfers.

These state machines are implementation-ready: you can directly map states to code, hang event handlers off transitions, and wire into existing multi-source swarm infrastructure.

---

## Overview

### Three-Level Model

The mesh operates at three distinct levels:

1. **Job**: One MB release you're trying to complete → `job_id` (from `MeshCacheJob`)
2. **Variant**: One concrete audio file for a recording → `variant_id` (from `AudioVariant`)
3. **Transfer**: Data flow for a given `(job_id, variant_id, peer)` → `transfer_id` (from `ChunkRequest`/`ChunkResponse`)

**Swarm** = many transfers for the same track/release across different peers.

### Internal Data Structures

You'll typically need:

- **Job table**: Keyed by `job_id`, tracks overall completion for an album/release
- **Transfer table**: Keyed by `transfer_id`, maps to `(job_id, peer_id, variant_id)`
- **Piece map**: Per-track bitmap tracking which byte ranges are present/missing
- **Peer statistics**: Speed, reliability, quality scores per peer

### Unit of State Machine

Each state machine instance represents **one transfer** (one peer, one variant).

Swarming is achieved by running **multiple transfers in parallel** for the same track.

---

## Downloader State Machine

### States

| State | Description |
|-------|-------------|
| `D_NEW` | Created locally, no overlay messages yet |
| `D_NEGOTIATING` | `mesh_cache_job` sent, waiting for valid offer |
| `D_READY` | Have chosen peer and variant, ready to request chunks |
| `D_REQUESTING` | Actively sending `chunk_request` and receiving `chunk_response` |
| `D_DRAINING` | All pieces scheduled, waiting for outstanding responses |
| `D_VERIFYING` | File assembled, running integrity/fingerprint verification |
| `D_COMPLETED` | Transfer done and verified |
| `D_ABORTED` | Transfer terminated (error, cancel, timeout) |

### State Diagram

```
                    ┌─────────┐
                    │  D_NEW  │
                    └────┬────┘
                         │ Send mesh_cache_job (request)
                         ↓
                ┌──────────────────┐
     timeout/   │ D_NEGOTIATING    │
     no offers  └────────┬─────────┘
          ┌─────────────┤
          │              │ Receive mesh_cache_job (offer)
          │              │ Accept offer
          │              ↓
          │         ┌──────────┐
          │         │ D_READY  │
          │         └────┬─────┘
          │              │ Start requesting chunks
          │              ↓
          │      ┌───────────────┐
          │      │ D_REQUESTING  │◄────┐
          │      └───────┬───────┘     │
          │              │              │ Receive chunk_response
          │              │              │ Send more chunk_request
          │              │──────────────┘
          │              │ All ranges assigned
          │              │ to this/other peers
          │              ↓
          │      ┌───────────────┐
          │      │  D_DRAINING   │
          │      └───────┬───────┘
          │              │ All pieces received
          │              ↓
          │      ┌───────────────┐
          │      │ D_VERIFYING   │
          │      └───┬───────┬───┘
          │          │       │ Verify fail
          │          │       └──────────┐
          │          │ Verify pass      │
          │          ↓                  │
          │    ┌─────────────┐          │
          │    │ D_COMPLETED │          │
          │    └─────────────┘          │
          │                             │
          │    ┌────────────┐           │
          └───>│  D_ABORTED │◄──────────┘
               └────────────┘
                     ▲
                     │ Timeout, disconnect, cancel
                     │ (from any active state)
```

---

## Downloader Transitions

### 1. Creation & Negotiation

#### `D_NEW` (Entry Point)

**Event**: Local code decides "we want MB Recording X via peer P / variant V"

**Actions**:
1. Allocate new `transfer_id` (GUID)
2. Register in transfer table with state `D_NEW`
3. Associate with parent `job_id` and `variant_id`

```csharp
public Transfer CreateTransfer(Guid jobId, string mbRecordingId, string? targetPeerId = null)
{
    var transferId = Guid.NewGuid();
    var transfer = new Transfer
    {
        TransferId = transferId,
        JobId = jobId,
        MbRecordingId = mbRecordingId,
        TargetPeerId = targetPeerId,
        State = TransferState.D_NEW,
        CreatedAt = DateTimeOffset.UtcNow
    };
    
    _transfers[transferId] = transfer;
    return transfer;
}
```

---

#### `D_NEW` → `D_NEGOTIATING`

**Event**: Send `MeshCacheJob` with `role = "request"` to mesh

**Actions**:
1. Start negotiation timer (e.g., 30 seconds)
2. Subscribe to `MeshCacheJob` offers for this `job_id` + `recording_id`
3. Update state to `D_NEGOTIATING`

```csharp
public async Task StartNegotiationAsync(Transfer transfer, MeshCacheJob request)
{
    transfer.State = TransferState.D_NEGOTIATING;
    transfer.NegotiationStartedAt = DateTimeOffset.UtcNow;
    
    // Broadcast request to mesh neighbors
    await _meshSyncService.BroadcastAsync(request);
    
    // Start timeout timer
    _ = Task.Run(async () =>
    {
        await Task.Delay(TimeSpan.FromSeconds(30));
        if (transfer.State == TransferState.D_NEGOTIATING)
        {
            await AbortTransferAsync(transfer, "negotiation_timeout");
        }
    });
}
```

---

#### `D_NEGOTIATING` → `D_READY`

**Event**: Receive `MeshCacheJob` with `role = "offer"` from peer P that:
- References a `variant_id` matching desired profile
- Is accepted by swarm scheduler (bandwidth/slot constraints)

**Actions**:
1. Bind transfer to `(peer_id, variant_id)`
2. Allocate piece map for file
3. Choose initial chunk ranges
4. Update state to `D_READY`

```csharp
public async Task HandleOfferAsync(Transfer transfer, MeshCacheJob offer, string peerId)
{
    // Validate offer matches requirements
    var offeredTrack = offer.OfferedTracks.FirstOrDefault(t => 
        t.MbRecordingId == transfer.MbRecordingId);
    
    if (offeredTrack == null)
        return;
    
    // Check if offer meets quality requirements
    if (!MeetsQualityRequirements(offeredTrack, transfer.DesiredProfile))
        return;
    
    // Check fairness constraints
    if (!_fairnessScheduler.CanAcceptOffer(peerId, offer.Constraints))
        return;
    
    // Accept offer
    transfer.State = TransferState.D_READY;
    transfer.PeerId = peerId;
    transfer.VariantId = offeredTrack.VariantId;
    transfer.FileSize = GetFileSizeFromOffer(offeredTrack);
    
    // Initialize piece map
    transfer.PieceMap = new PieceMap(transfer.FileSize, chunkSize: 262144);
    
    Log.Information("[Brainz] Accepted offer from {Peer} for {Recording}", 
        peerId, transfer.MbRecordingId);
}
```

---

#### `D_NEGOTIATING` → `D_ABORTED`

**Event**: Negotiation timeout OR all offers rejected

**Actions**:
1. Log failure reason
2. Update state to `D_ABORTED`
3. Schedule retry with exponential backoff (optional)

```csharp
public async Task AbortNegotiationAsync(Transfer transfer, string reason)
{
    transfer.State = TransferState.D_ABORTED;
    transfer.AbortReason = reason;
    transfer.CompletedAt = DateTimeOffset.UtcNow;
    
    Log.Warning("[Brainz] Transfer {TransferId} aborted during negotiation: {Reason}",
        transfer.TransferId, reason);
    
    // Notify job-level coordinator
    await _jobCoordinator.OnTransferAbortedAsync(transfer);
}
```

---

### 2. Active Transfer

#### `D_READY` → `D_REQUESTING`

**Event**: Local scheduler decides to start pulling from this peer

**Actions**:
1. Generate first batch of `chunk_request` messages
2. Start per-transfer idle/heartbeat timer
3. Update state to `D_REQUESTING`

```csharp
public async Task StartRequestingAsync(Transfer transfer)
{
    transfer.State = TransferState.D_REQUESTING;
    transfer.RequestingStartedAt = DateTimeOffset.UtcNow;
    
    // Generate initial chunk requests (e.g., first 4 chunks for pipelining)
    var initialRanges = transfer.PieceMap.GetNextMissingRanges(count: 4);
    
    foreach (var range in initialRanges)
    {
        var request = new ChunkRequest(
            TransferId: transfer.TransferId,
            JobId: transfer.JobId,
            MbRecordingId: transfer.MbRecordingId,
            MbReleaseId: transfer.MbReleaseId,
            VariantId: transfer.VariantId,
            OffsetBytes: range.Offset,
            LengthBytes: range.Length,
            Priority: CalculatePriority(range, transfer),
            DeadlineMs: 5000,
            Hints: new ChunkRequestHints(
                AllowSparse: true,
                PreferContiguous: false,
                EndOfFileKnown: true
            )
        );
        
        await SendChunkRequestAsync(transfer.PeerId, request);
        transfer.InFlightChunks.Add(range);
    }
    
    // Start idle timer
    StartIdleTimer(transfer, timeoutSeconds: 30);
}
```

---

#### Stay in `D_REQUESTING`

**Events**:
1. Receive `chunk_response` with data
2. Send additional `chunk_request` for new ranges
3. Swarm rebalancing assigns new ranges to this peer

**Actions**:
- Update piece map with received data
- Write data to file/buffer
- Send next `chunk_request` if more ranges needed
- Reset idle timer

```csharp
public async Task HandleChunkResponseAsync(Transfer transfer, ChunkResponse response)
{
    // Validate response
    if (transfer.State != TransferState.D_REQUESTING && 
        transfer.State != TransferState.D_DRAINING)
        return;
    
    // Handle error response
    if (!string.IsNullOrEmpty(response.ErrorCode))
    {
        await HandleChunkErrorAsync(transfer, response);
        return;
    }
    
    // Decode data
    var data = Convert.FromBase64String(response.DataBase64);
    
    // Optional: verify chunk hash
    if (!string.IsNullOrEmpty(response.ChunkSha256))
    {
        var computed = await ComputeSha256Async(data);
        if (computed != response.ChunkSha256)
        {
            Log.Warning("[Brainz] Chunk hash mismatch from {Peer}", transfer.PeerId);
            // Retry this range
            return;
        }
    }
    
    // Update piece map
    transfer.PieceMap.MarkComplete(response.OffsetBytes, response.LengthBytes);
    
    // Write to file
    await WriteChunkToFileAsync(transfer, response.OffsetBytes, data);
    
    // Remove from in-flight
    transfer.InFlightChunks.Remove(new Range(response.OffsetBytes, response.LengthBytes));
    
    // Update statistics
    transfer.BytesReceived += response.LengthBytes;
    UpdatePeerStatistics(transfer.PeerId, response.LengthBytes);
    
    // Reset idle timer
    ResetIdleTimer(transfer);
    
    // Request next chunk if still in REQUESTING state
    if (transfer.State == TransferState.D_REQUESTING)
    {
        var nextRange = transfer.PieceMap.GetNextMissingRange();
        if (nextRange != null)
        {
            await RequestChunkAsync(transfer, nextRange);
        }
    }
    
    // Check if complete
    if (transfer.PieceMap.IsComplete())
    {
        await TransitionToVerifyingAsync(transfer);
    }
}
```

---

#### `D_REQUESTING` → `D_DRAINING`

**Event**: Local swarm policy decides "we no longer need this peer to fetch new ranges" because:
- All missing pieces are assigned to other peers
- We have enough data to complete from others

**Actions**:
1. Stop issuing new `chunk_request`
2. Optionally send `chunk_cancel` for unneeded ranges
3. Update state to `D_DRAINING`

```csharp
public async Task TransitionToDrainingAsync(Transfer transfer, string reason)
{
    transfer.State = TransferState.D_DRAINING;
    
    Log.Information("[Brainz] Transfer {TransferId} draining: {Reason}",
        transfer.TransferId, reason);
    
    // Cancel any outstanding chunks that other peers have now covered
    var redundantRanges = transfer.InFlightChunks
        .Where(r => _jobCoordinator.IsRangeCoveredByOtherPeers(transfer.JobId, r))
        .ToList();
    
    if (redundantRanges.Any())
    {
        var cancel = new ChunkCancel(
            TransferId: transfer.TransferId,
            VariantId: transfer.VariantId,
            ReasonCode: "swarm_rebalanced",
            ReasonMessage: reason,
            Ranges: redundantRanges.Select(r => new CancelledRange(r.Offset, r.Length)).ToList()
        );
        
        await SendChunkCancelAsync(transfer.PeerId, cancel);
    }
}
```

---

### 3. Completion & Verification

#### `D_REQUESTING` / `D_DRAINING` → `D_VERIFYING`

**Event**: Piece map indicates file is fully contiguous (no gaps)

**Actions**:
1. Stop all chunk requests
2. Run verification:
   - Verify `chunk_sha256` hashes (if stored)
   - Verify full-file hash
   - Verify acoustic fingerprint against MB Recording ID
3. Update state to `D_VERIFYING`

```csharp
public async Task TransitionToVerifyingAsync(Transfer transfer)
{
    transfer.State = TransferState.D_VERIFYING;
    transfer.VerificationStartedAt = DateTimeOffset.UtcNow;
    
    Log.Information("[Brainz] Transfer {TransferId} complete, verifying...",
        transfer.TransferId);
    
    // Run verification
    var verificationResult = await VerifyTransferAsync(transfer);
    
    if (verificationResult.Success)
    {
        await TransitionToCompletedAsync(transfer);
    }
    else
    {
        await AbortTransferAsync(transfer, $"verification_failed: {verificationResult.Reason}");
    }
}

private async Task<VerificationResult> VerifyTransferAsync(Transfer transfer)
{
    // 1. Verify full-file hash (if available)
    if (!string.IsNullOrEmpty(transfer.ExpectedFileHash))
    {
        var computed = await ComputeFullFileHashAsync(transfer.LocalFilePath);
        if (computed != transfer.ExpectedFileHash)
        {
            return VerificationResult.Failure("file_hash_mismatch");
        }
    }
    
    // 2. Verify acoustic fingerprint (if MB Recording ID specified)
    if (!string.IsNullOrEmpty(transfer.MbRecordingId))
    {
        var fingerprint = await ComputeAcousticFingerprintAsync(transfer.LocalFilePath);
        var matches = await _acoustIdService.MatchFingerprintAsync(fingerprint);
        
        if (!matches.Any(m => m.MbRecordingId == transfer.MbRecordingId))
        {
            return VerificationResult.Failure("fingerprint_mismatch");
        }
    }
    
    return VerificationResult.Success();
}
```

---

#### `D_VERIFYING` → `D_COMPLETED`

**Event**: Verification passes

**Actions**:
1. Send final `chunk_cancel` to uploader (cleanup signal)
2. Mark `transfer_id` as completed
3. Propagate "track complete" to job-level state
4. Update local HashDb/fingerprint inventory

```csharp
public async Task TransitionToCompletedAsync(Transfer transfer)
{
    transfer.State = TransferState.D_COMPLETED;
    transfer.CompletedAt = DateTimeOffset.UtcNow;
    
    Log.Information("[Brainz] Transfer {TransferId} completed successfully " +
                    "({Bytes} bytes in {Duration})",
        transfer.TransferId,
        transfer.BytesReceived,
        transfer.CompletedAt - transfer.RequestingStartedAt);
    
    // Send cleanup signal to uploader
    var cancel = new ChunkCancel(
        TransferId: transfer.TransferId,
        VariantId: transfer.VariantId,
        ReasonCode: "complete",
        ReasonMessage: "Transfer completed successfully",
        Ranges: null  // null = entire transfer
    );
    await SendChunkCancelAsync(transfer.PeerId, cancel);
    
    // Update job coordinator
    await _jobCoordinator.OnTransferCompletedAsync(transfer);
    
    // Update local HashDb with verified fingerprint
    if (!string.IsNullOrEmpty(transfer.MbRecordingId))
    {
        await _hashDbService.StoreVerifiedFingerprintAsync(
            transfer.MbRecordingId,
            transfer.LocalFilePath,
            transfer.VariantId);
    }
}
```

---

#### `D_VERIFYING` → `D_ABORTED`

**Event**: Verification fails (hash mismatch, wrong fingerprint, corrupted data)

**Actions**:
1. Mark peer/variant as "bad" for this job
2. Remove peer from candidate set
3. Keep job alive and reassign to other peers
4. Update state to `D_ABORTED`

```csharp
public async Task HandleVerificationFailureAsync(Transfer transfer, string reason)
{
    transfer.State = TransferState.D_ABORTED;
    transfer.AbortReason = $"verification_failed: {reason}";
    transfer.CompletedAt = DateTimeOffset.UtcNow;
    
    Log.Warning("[Brainz] Transfer {TransferId} verification failed: {Reason}",
        transfer.TransferId, reason);
    
    // Blacklist this peer/variant combination
    await _peerReputationService.RecordVerificationFailureAsync(
        transfer.PeerId,
        transfer.VariantId,
        reason);
    
    // Delete corrupted file
    if (File.Exists(transfer.LocalFilePath))
    {
        File.Delete(transfer.LocalFilePath);
    }
    
    // Notify job coordinator to reassign
    await _jobCoordinator.OnTransferFailedAsync(transfer, shouldRetry: true);
}
```

---

### 4. Failures & Cancellation

#### Any Active State → `D_ABORTED`

**Events**:
1. **Remote disconnect**: Peer drops TCP connection
2. **Timeout**: No `chunk_response` for outstanding requests
3. **Local cancel**: User cancels download or job cancelled

**Actions** (Remote Disconnect):

```csharp
public async Task HandlePeerDisconnectAsync(Transfer transfer)
{
    if (transfer.State == TransferState.D_COMPLETED || 
        transfer.State == TransferState.D_ABORTED)
        return;
    
    transfer.State = TransferState.D_ABORTED;
    transfer.AbortReason = "peer_disconnected";
    transfer.CompletedAt = DateTimeOffset.UtcNow;
    
    Log.Warning("[Brainz] Peer {Peer} disconnected during transfer {TransferId}",
        transfer.PeerId, transfer.TransferId);
    
    // Free reserved bandwidth/slots
    await _fairnessScheduler.ReleasePeerResourcesAsync(transfer.PeerId, transfer);
    
    // Reassign missing ranges to other peers
    await _jobCoordinator.ReassignMissingRangesAsync(
        transfer.JobId,
        transfer.PieceMap.GetMissingRanges());
    
    // Increment penalty score for peer
    await _peerReputationService.RecordDisconnectAsync(transfer.PeerId);
}
```

**Actions** (Timeout):

```csharp
public async Task HandleTimeoutAsync(Transfer transfer)
{
    transfer.State = TransferState.D_ABORTED;
    transfer.AbortReason = "timeout";
    transfer.CompletedAt = DateTimeOffset.UtcNow;
    
    Log.Warning("[Brainz] Transfer {TransferId} timed out (no response from {Peer})",
        transfer.TransferId, transfer.PeerId);
    
    // Same cleanup as disconnect
    await HandlePeerDisconnectAsync(transfer);
}
```

**Actions** (User Cancel):

```csharp
public async Task HandleUserCancelAsync(Transfer transfer)
{
    transfer.State = TransferState.D_ABORTED;
    transfer.AbortReason = "user_cancelled";
    transfer.CompletedAt = DateTimeOffset.UtcNow;
    
    // Send cancel to peer
    var cancel = new ChunkCancel(
        TransferId: transfer.TransferId,
        VariantId: transfer.VariantId,
        ReasonCode: "user_cancelled",
        ReasonMessage: "User cancelled download",
        Ranges: null
    );
    await SendChunkCancelAsync(transfer.PeerId, cancel);
    
    // Free state
    await _jobCoordinator.OnTransferCancelledAsync(transfer);
}
```

---

## Uploader State Machine

### States

| State | Description |
|-------|-------------|
| `U_IDLE` | No transfer state for this `(peer, variant_id)` yet |
| `U_PENDING` | Aware of job/offer, but no chunk requests yet |
| `U_SERVING` | Actively reading from disk and sending chunks |
| `U_THROTTLED` | Temporarily limiting due to bandwidth/slot constraints |
| `U_TEARDOWN` | Cleaning up and stopping |
| `U_DONE` | Transfer finished |
| `U_REJECTED` | Refused (policy, rate-limit, etc.) |

### State Diagram

```
       ┌──────────┐
       │  U_IDLE  │◄────────────────┐
       └────┬─────┘                 │
            │ Receive request        │
            │ Send offer             │
            ↓                        │
       ┌──────────┐      TTL expires│
       │U_PENDING │──────────────────┘
       └────┬─────┘
            │ Receive first chunk_request
            ↓
       ┌──────────┐
       │U_SERVING │◄────┐
       └────┬─────┘     │
            │            │ Serve chunks
            │            └────────────
            │ Bandwidth exceeded
            ↓
    ┌──────────────┐
    │ U_THROTTLED  │
    └──────┬───────┘
           │ Capacity freed
           └──────────────────┐
                              │
       ┌──────────┐           │
       │U_TEARDOWN│◄──────────┘
       └────┬─────┘     Complete, cancel, or error
            │
            ↓
       ┌──────────┐
       │  U_DONE  │
       └──────────┘
            │
            └──────────────────┐
                               │
       ┌──────────┐            │
       │U_REJECTED│◄───────────┘
       └──────────┘     Policy violation
```

---

## Uploader Transitions

### 1. Negotiation & Offer

#### `U_IDLE` (Entry Point)

**Event**: Initial default for any peer connecting over overlay

**Actions**: None (waiting for `mesh_cache_job` request)

---

#### `U_IDLE` → `U_PENDING`

**Event**: Receive `MeshCacheJob` with `role = "request"` for recording this peer can serve

**Actions**:
1. Check if recording/variant available locally
2. Check fairness policy (slots, bandwidth)
3. Prepare and send `MeshCacheJob` offer
4. Allocate lightweight state keyed by `(job_id, peer_id)`
5. Update state to `U_PENDING`

```csharp
public async Task HandleCacheJobRequestAsync(MeshCacheJob request, string peerId)
{
    foreach (var requestedTrack in request.RequestedTracks)
    {
        // Check if we have a suitable variant
        var variant = await FindMatchingVariantAsync(
            requestedTrack.MbRecordingId,
            requestedTrack.DesiredProfile);
        
        if (variant == null)
            continue;
        
        // Check fairness policy
        if (!_fairnessScheduler.CanAcceptNewUpload(peerId, request.Constraints))
            continue;
        
        // Create pending state
        var pendingOffer = new PendingOffer
        {
            JobId = request.JobId,
            PeerId = peerId,
            MbRecordingId = requestedTrack.MbRecordingId,
            VariantId = variant.VariantId,
            State = UploadState.U_PENDING,
            CreatedAt = DateTimeOffset.UtcNow,
            TtlSeconds = 600  // 10 minutes
        };
        
        _pendingOffers[$"{request.JobId}:{peerId}"] = pendingOffer;
        
        // Send offer
        var offer = new MeshCacheJob(
            JobId: request.JobId,
            Role: "offer",
            MbReleaseId: request.MbReleaseId,
            Title: request.Title,
            Artist: request.Artist,
            RequestedTracks: new List<RequestedTrack>(),
            OfferedTracks: new List<OfferedTrack>
            {
                new OfferedTrack(
                    TrackNumber: requestedTrack.TrackNumber,
                    MbRecordingId: requestedTrack.MbRecordingId,
                    VariantId: variant.VariantId,
                    Lossless: variant.Lossless,
                    Codec: variant.Codec,
                    BitrateKbps: variant.BitrateKbps
                )
            },
            Constraints: GetUploadConstraints()
        );
        
        await _meshSyncService.SendAsync(peerId, offer);
        
        // Start TTL timer
        StartOfferTtlTimer(pendingOffer);
    }
}
```

---

#### `U_PENDING` → `U_IDLE`

**Event**: TTL expires with no `chunk_request` received

**Actions**: Drop offer state

```csharp
private void StartOfferTtlTimer(PendingOffer offer)
{
    _ = Task.Run(async () =>
    {
        await Task.Delay(TimeSpan.FromSeconds(offer.TtlSeconds));
        
        if (offer.State == UploadState.U_PENDING)
        {
            Log.Debug("[Brainz] Offer for {Job} to {Peer} expired",
                offer.JobId, offer.PeerId);
            
            _pendingOffers.Remove($"{offer.JobId}:{offer.PeerId}");
        }
    });
}
```

---

### 2. Start Serving

#### `U_PENDING` → `U_SERVING`

**Event**: Receive first `chunk_request` with new `transfer_id` and known `variant_id`

**Actions**:
1. Validate request (ranges, policy)
2. Allocate transfer state
3. Open file handle
4. Track bytes_uploaded
5. Update state to `U_SERVING`

```csharp
public async Task HandleFirstChunkRequestAsync(ChunkRequest request, string peerId)
{
    // Find pending offer
    var offerKey = $"{request.JobId}:{peerId}";
    if (!_pendingOffers.TryGetValue(offerKey, out var offer))
    {
        await SendChunkErrorAsync(peerId, request, "not_found", "No offer found");
        return;
    }
    
    // Validate request
    var validation = await ValidateChunkRequestAsync(request, offer);
    if (!validation.IsValid)
    {
        await SendChunkErrorAsync(peerId, request, validation.ErrorCode, validation.ErrorMessage);
        return;
    }
    
    // Create transfer state
    var upload = new Upload
    {
        TransferId = request.TransferId,
        JobId = request.JobId,
        PeerId = peerId,
        VariantId = request.VariantId,
        MbRecordingId = request.MbRecordingId,
        State = UploadState.U_SERVING,
        FilePath = offer.FilePath,
        FileSize = offer.FileSize,
        BytesUploaded = 0,
        StartedAt = DateTimeOffset.UtcNow
    };
    
    _uploads[request.TransferId] = upload;
    
    // Remove from pending
    _pendingOffers.Remove(offerKey);
    
    // Serve the chunk
    await ServeChunkAsync(upload, request);
}
```

---

#### Stay in `U_SERVING`

**Event**: Each incoming `chunk_request`

**Actions**:
1. Check throttling
2. If OK: read file slice and send `chunk_response`
3. If throttled: respond with `error_code = "rate_limited"` or queue

```csharp
public async Task HandleChunkRequestAsync(ChunkRequest request, string peerId)
{
    if (!_uploads.TryGetValue(request.TransferId, out var upload))
    {
        await SendChunkErrorAsync(peerId, request, "not_found", "Transfer not found");
        return;
    }
    
    // Check throttling
    if (_fairnessScheduler.IsThrottled(peerId))
    {
        upload.State = UploadState.U_THROTTLED;
        await SendChunkErrorAsync(peerId, request, "rate_limited", 
            "Overlay upload bandwidth exceeded");
        return;
    }
    
    // Serve chunk
    await ServeChunkAsync(upload, request);
}

private async Task ServeChunkAsync(Upload upload, ChunkRequest request)
{
    try
    {
        // Read from disk
        var data = await ReadFileRangeAsync(
            upload.FilePath,
            request.OffsetBytes,
            request.LengthBytes);
        
        // Optional: compute chunk hash
        string? chunkHash = null;
        if (_config.EnableChunkVerification)
        {
            chunkHash = await ComputeSha256Async(data);
        }
        
        // Send response
        var response = new ChunkResponse(
            TransferId: request.TransferId,
            VariantId: request.VariantId,
            OffsetBytes: request.OffsetBytes,
            LengthBytes: request.LengthBytes,
            DataBase64: Convert.ToBase64String(data),
            Eof: request.OffsetBytes + request.LengthBytes >= upload.FileSize,
            Complete: false,
            FileSizeBytes: upload.FileSize,
            ChunkSha256: chunkHash,
            ErrorCode: null,
            ErrorMessage: null
        );
        
        await _meshSyncService.SendAsync(upload.PeerId, response);
        
        // Update statistics
        upload.BytesUploaded += request.LengthBytes;
        await _fairnessScheduler.RecordBytesUploadedAsync(upload.PeerId, request.LengthBytes);
        
        Log.Debug("[Brainz] Served chunk {Offset}-{End} to {Peer}",
            request.OffsetBytes,
            request.OffsetBytes + request.LengthBytes,
            upload.PeerId);
    }
    catch (Exception ex)
    {
        Log.Error(ex, "[Brainz] Error serving chunk for transfer {TransferId}",
            upload.TransferId);
        
        await SendChunkErrorAsync(upload.PeerId, request, "io_error", ex.Message);
    }
}
```

---

### 3. Throttling

#### `U_SERVING` → `U_THROTTLED`

**Event**: Overlay upload bandwidth or slot cap exceeded

**Actions**:
1. Stop reading from disk for new requests
2. Queue requests OR respond with `error_code = "rate_limited"`
3. Update state to `U_THROTTLED`

```csharp
public async Task ThrottleUploadAsync(Upload upload)
{
    upload.State = UploadState.U_THROTTLED;
    
    Log.Information("[Brainz] Upload {TransferId} throttled (bandwidth cap reached)",
        upload.TransferId);
}
```

---

#### `U_THROTTLED` → `U_SERVING`

**Event**: Scheduler frees capacity (other transfers complete, time window resets)

**Actions**: Resume serving queued requests

```csharp
public async Task ResumeUploadAsync(Upload upload)
{
    if (upload.State != UploadState.U_THROTTLED)
        return;
    
    upload.State = UploadState.U_SERVING;
    
    Log.Information("[Brainz] Upload {TransferId} resumed", upload.TransferId);
    
    // Process any queued requests
    await ProcessQueuedRequestsAsync(upload);
}
```

---

### 4. Completion & Teardown

#### `U_SERVING` → `U_TEARDOWN` (Graceful)

**Events**:
1. Receive `chunk_cancel` with no ranges (whole-transfer cancel)
2. No more outstanding requests AND TTL expired
3. Served last requested chunk

**Actions**:
1. Close file handle
2. Free per-transfer structures
3. Optionally send final `chunk_response` with `complete = true`
4. Update state to `U_TEARDOWN`

```csharp
public async Task HandleChunkCancelAsync(ChunkCancel cancel, string peerId)
{
    if (!_uploads.TryGetValue(cancel.TransferId, out var upload))
        return;
    
    Log.Information("[Brainz] Transfer {TransferId} cancelled by peer: {Reason}",
        cancel.TransferId, cancel.ReasonCode);
    
    await TeardownUploadAsync(upload);
}

private async Task TeardownUploadAsync(Upload upload)
{
    upload.State = UploadState.U_TEARDOWN;
    upload.CompletedAt = DateTimeOffset.UtcNow;
    
    // Close file handle
    upload.FileHandle?.Dispose();
    
    // Free resources
    await _fairnessScheduler.ReleaseUploadResourcesAsync(upload.PeerId, upload);
    
    // Transition to DONE
    upload.State = UploadState.U_DONE;
    
    Log.Information("[Brainz] Upload {TransferId} completed ({Bytes} bytes uploaded)",
        upload.TransferId, upload.BytesUploaded);
}
```

---

#### `U_TEARDOWN` → `U_DONE`

**Event**: Local cleanup successful

**Actions**: Keep small "recently done" record OR revert to `U_IDLE`

```csharp
public void FinalizeUpload(Upload upload)
{
    upload.State = UploadState.U_DONE;
    
    // Keep in "recently done" cache for 5 minutes (for duplicate request handling)
    _ = Task.Run(async () =>
    {
        await Task.Delay(TimeSpan.FromMinutes(5));
        _uploads.Remove(upload.TransferId);
    });
}
```

---

#### `U_SERVING` / `U_THROTTLED` → `U_REJECTED`

**Events**:
- File disappears or unreadable
- Integrity check failure on disk
- Policy violation

**Actions**:
1. Send `chunk_response` with `error_code`
2. Move to `U_TEARDOWN` then `U_DONE`
3. Optionally mark peer/job as ineligible for cool-down period

```csharp
public async Task RejectUploadAsync(Upload upload, string reason)
{
    upload.State = UploadState.U_REJECTED;
    
    Log.Warning("[Brainz] Upload {TransferId} rejected: {Reason}",
        upload.TransferId, reason);
    
    // Notify peer
    var errorCode = reason.Contains("file") ? "not_found" : "policy_violation";
    // (Send error via next chunk_request handler)
    
    await TeardownUploadAsync(upload);
}
```

---

## Typical Happy Path Flow

For a single track between downloader D and uploader U:

```
1. D (job logic):
   "I want MB Recording X from Release R"
   
2. D → mesh:
   MeshCacheJob(role="request")
   [D: D_NEW → D_NEGOTIATING]
   
3. U receives request:
   [U: U_IDLE → U_PENDING]
   
4. U → D:
   MeshCacheJob(role="offer", variant_id="abc123")
   
5. D accepts offer:
   [D: D_NEGOTIATING → D_READY → D_REQUESTING]
   
6. D → U:
   chunk_request(offset=0, length=256KB)
   chunk_request(offset=256KB, length=256KB)
   ...
   
7. U starts serving:
   [U: U_PENDING → U_SERVING]
   
8. U → D:
   chunk_response(data for 0-256KB)
   chunk_response(data for 256KB-512KB)
   ...
   
9. D assembles file, all pieces received:
   [D: D_REQUESTING → D_VERIFYING]
   
10. D verifies hash/fingerprint:
    [D: D_VERIFYING → D_COMPLETED]
    
11. D → U:
    chunk_cancel(reason="complete")
    
12. U cleanup:
    [U: U_SERVING → U_TEARDOWN → U_DONE]
```

**Parallel**: Other peers do the same for other byte ranges/tracks → multi-source swarm.

---

## Integration Points

### Existing Multi-Source Infrastructure

```csharp
// Extend SourcePeer to include Brainz metadata
public class SourcePeer
{
    // Existing fields
    public string Username { get; set; }
    public string Filename { get; set; }
    
    // NEW: Brainz fields
    public string? MbRecordingId { get; set; }
    public string? VariantId { get; set; }
    public bool IsMeshSource { get; set; }
    public TransferState? MeshTransferState { get; set; }
    public double? QualityScore { get; set; }
}

// Swarm grouping now considers MBID
public string GetSwarmKey(SourcePeer peer)
{
    if (!string.IsNullOrEmpty(peer.MbRecordingId))
    {
        // Semantic swarm key
        return $"mbid:{peer.MbRecordingId}:{peer.CodecProfile}";
    }
    else
    {
        // Legacy byte-hash swarm key
        return $"hash:{peer.ByteHash}";
    }
}
```

### MeshSyncService

```csharp
// Add Brainz message handlers to existing MeshSyncService
public enum MeshMessageType
{
    // ... existing types ...
    
    // Brainz protocol (control plane)
    MbidSwarmDescriptor = 20,
    FingerprintBundleAdvert = 21,
    MeshCacheJob = 22,
    
    // Brainz protocol (data plane)
    ChunkRequest = 30,
    ChunkResponse = 31,
    ChunkCancel = 32,
}

// Message router
public async Task HandleMessageAsync(MeshEnvelope envelope)
{
    switch (envelope.Type)
    {
        case "chunk_request":
            var request = JsonSerializer.Deserialize<ChunkRequest>(envelope.Payload);
            await _brainzUploadService.HandleChunkRequestAsync(request, envelope.SenderId);
            break;
            
        case "chunk_response":
            var response = JsonSerializer.Deserialize<ChunkResponse>(envelope.Payload);
            await _brainzDownloadService.HandleChunkResponseAsync(response, envelope.SenderId);
            break;
            
        case "chunk_cancel":
            var cancel = JsonSerializer.Deserialize<ChunkCancel>(envelope.Payload);
            await _brainzUploadService.HandleChunkCancelAsync(cancel, envelope.SenderId);
            break;
            
        // ... other message types ...
    }
}
```

---

## Next Steps

To make this production-ready:

1. **Implement core types**: `Transfer`, `Upload`, `PieceMap`, `PendingOffer`
2. **Wire into MeshSyncService**: Add message handlers for chunk protocol
3. **Create BrainzDownloadService**: Encapsulate downloader state machine
4. **Create BrainzUploadService**: Encapsulate uploader state machine
5. **Extend SwarmScheduler**: Add MBID-aware swarm grouping
6. **Add FairnessScheduler**: Enforce bandwidth/slot policies
7. **Integrate with HashDb**: Store verified fingerprints
8. **Add metrics/observability**: Track transfer success rates, bandwidth usage

---

*Last updated: 2025-12-09*

