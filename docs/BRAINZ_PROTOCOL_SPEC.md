# MusicBrainz Mesh Protocol Specification

> **Status**: Experimental Protocol Design  
> **Branch**: `experimental/brainz`  
> **Version**: 1.0-draft

This document defines the wire protocol for MusicBrainz-aware mesh communication between slskdn nodes over the existing TLS overlay.

---

## Protocol Overview

### Transport Layer

- **Connection**: TLS TCP stream (existing overlay infrastructure)
- **Framing**: Line-delimited JSON (newline-separated)
- **Character Encoding**: UTF-8
- **Message Format**: JSON with top-level envelope + typed payload

### Design Principles

1. **Soulseek remains primary**: Mesh is acceleration/fallback, not replacement
2. **Backward compatible**: Non-Brainz nodes ignore unknown message types
3. **Bandwidth conscious**: Advertise capabilities, not full library
4. **Privacy preserving**: Share MBIDs/fingerprints, not file paths
5. **Fair use**: Configurable bandwidth/slot limits

---

## Message Envelope (Common Structure)

All messages use a common envelope with a type-specific payload.

### JSON Structure

```jsonc
{
  "type": "mbid_swarm_descriptor",   // Message type identifier
  "version": 1,                       // Protocol version for this type
  "message_id": "uuid-v4",            // Unique message identifier
  "sender_id": "mesh-node-abc123",    // Sender's mesh node ID
  "timestamp": "2025-12-09T23:12:45.123Z",  // ISO 8601 UTC
  "payload": { /* type-specific data */ }
}
```

### C# Implementation

```csharp
namespace slskd.Mesh.Brainz
{
    /// <summary>
    /// Common envelope for all MusicBrainz mesh messages.
    /// </summary>
    /// <typeparam name="TPayload">Type-specific payload</typeparam>
    public sealed record MeshEnvelope<TPayload>
    (
        string Type,
        int Version,
        Guid MessageId,
        string SenderId,
        DateTimeOffset Timestamp,
        TPayload Payload
    );

    /// <summary>
    /// Non-generic envelope for deserialization.
    /// </summary>
    public sealed record MeshEnvelope
    (
        string Type,
        int Version,
        Guid MessageId,
        string SenderId,
        DateTimeOffset Timestamp,
        object Payload
    );
}
```

### Message Types

| Type | Purpose | Direction |
|------|---------|-----------|
| `mbid_swarm_descriptor` | Advertise album availability | Broadcast / Response |
| `fingerprint_bundle_advert` | Share audio fingerprints | Epidemic sync |
| `mesh_cache_job` | Request/offer caching assistance | Peer-to-peer |

---

## Message Type 1: MBID Swarm Descriptor

### Purpose

Advertise what a node has for a specific MusicBrainz Release, including:
- Which tracks are available
- Audio quality/codec information
- Bandwidth/slot policies

### When to Send

- **Periodic advertisement**: Every 15 minutes for actively-seeded releases
- **Query response**: In reply to "what do you have for MBID X?"
- **On completion**: When finishing download of an album

### Payload Schema

```jsonc
{
  // MusicBrainz/Discogs identifiers
  "mb_release_id": "c0d0c0a4-4a26-4d74-9c02-67c9321b3b22",
  "mb_release_group_id": "6f3f9b48-f1e2-4269-9b4b-2ddc93d1ff77", // optional
  "discogs_release_id": 123456,  // optional
  
  // Basic metadata
  "title": "Loveless",
  "artist": "My Bloody Valentine",
  
  // Edition-specific details
  "edition_profile": {
    "label": "Sire",
    "catalog_number": "9 26840-2",
    "release_country": "US",
    "release_date": "1991-11-04",
    "medium_format": "CD",
    "lossless": true,
    "codec": "FLAC",
    "sample_rate_hz": 44100,
    "bit_depth": 16,
    "channels": 2
  },
  
  // What this node has
  "availability": {
    "has_full_album": true,
    "tracks": [
      {
        "track_number": 1,
        "title": "Only Shallow",
        "mb_recording_id": "e2f5e9b4-5852-4cd3-b1f9-29a7a4a234bc",
        "duration_ms": 242000,
        "file_size_bytes": 34567890,
        "codec": "FLAC",
        "bitrate_kbps": 900,
        "lossless": true,
        "quality_score": 0.96,  // 0-1, internal heuristic
        "availability_state": "complete"  // complete | partial | missing
      },
      {
        "track_number": 2,
        "title": "Loomer",
        "mb_recording_id": "4863d0b0-7920-4e1d-ba55-e00e39c6bdaa",
        "duration_ms": 180000,
        "availability_state": "missing"
      }
    ]
  },
  
  // Bandwidth/fairness policies
  "policies": {
    "prefer_soulseek_primary": true,  // Use overlay only for accel/fallback
    "max_overlay_upload_slots": 2,    // Additional to Soulseek slots
    "max_overlay_bandwidth_kbps": 2000,
    "max_concurrent_overlay_peers": 4,
    "allow_relay_from_soulseek": true  // Can act as cache/relay
  }
}
```

### C# Data Model

```csharp
namespace slskd.Mesh.Brainz
{
    /// <summary>
    /// Describes availability and policies for a MusicBrainz release.
    /// </summary>
    public sealed record MbidSwarmDescriptor
    (
        string MbReleaseId,
        string? MbReleaseGroupId,
        int? DiscogsReleaseId,
        string Title,
        string Artist,
        EditionProfile EditionProfile,
        ReleaseAvailability Availability,
        SwarmPolicy Policies
    );

    /// <summary>
    /// Edition-specific audio characteristics.
    /// </summary>
    public sealed record EditionProfile
    (
        string? Label,
        string? CatalogNumber,
        string? ReleaseCountry,
        DateOnly? ReleaseDate,
        string? MediumFormat,
        bool Lossless,
        string Codec,
        int SampleRateHz,
        int BitDepth,
        int Channels
    );

    /// <summary>
    /// What tracks are available from this node.
    /// </summary>
    public sealed record ReleaseAvailability
    (
        bool HasFullAlbum,
        IReadOnlyList<TrackAvailability> Tracks
    );

    /// <summary>
    /// Availability details for a single track.
    /// </summary>
    public sealed record TrackAvailability
    (
        int TrackNumber,
        string Title,
        string? MbRecordingId,
        int? DurationMs,
        long? FileSizeBytes,
        string? Codec,
        int? BitrateKbps,
        bool? Lossless,
        double? QualityScore,
        string AvailabilityState  // "complete" | "partial" | "missing"
    );

    /// <summary>
    /// Bandwidth and fairness policies for mesh serving.
    /// </summary>
    public sealed record SwarmPolicy
    (
        bool PreferSoulseekPrimary,
        int MaxOverlayUploadSlots,
        int MaxOverlayBandwidthKbps,
        int MaxConcurrentOverlayPeers,
        bool AllowRelayFromSoulseek
    );
}
```

### Example Message

```jsonc
{
  "type": "mbid_swarm_descriptor",
  "version": 1,
  "message_id": "b5f2ae92-0a04-4af6-8485-5e2387f72083",
  "sender_id": "mesh-node-abc123",
  "timestamp": "2025-12-09T23:15:02.001Z",
  "payload": {
    "mb_release_id": "c0d0c0a4-4a26-4d74-9c02-67c9321b3b22",
    "title": "Loveless",
    "artist": "My Bloody Valentine",
    "edition_profile": {
      "label": "Sire",
      "lossless": true,
      "codec": "FLAC",
      "sample_rate_hz": 44100,
      "bit_depth": 16,
      "channels": 2
    },
    "availability": {
      "has_full_album": true,
      "tracks": [
        {
          "track_number": 1,
          "title": "Only Shallow",
          "mb_recording_id": "e2f5e9b4-5852-4cd3-b1f9-29a7a4a234bc",
          "duration_ms": 242000,
          "codec": "FLAC",
          "lossless": true,
          "availability_state": "complete"
        }
      ]
    },
    "policies": {
      "prefer_soulseek_primary": true,
      "max_overlay_upload_slots": 2,
      "max_overlay_bandwidth_kbps": 2000
    }
  }
}
```

---

## Message Type 2: Fingerprint Bundle Advertisement

### Purpose

Share acoustic fingerprints and audio variants for recordings, enabling:
- Semantic swarm grouping (same recording, different files)
- Transcode detection
- Quality consensus
- De-duplication

### When to Send

- **Epidemic sync**: Periodic propagation (like hash DB sync)
- **Delta updates**: When local library changes
- **Full snapshots**: On new peer connection (if requested)

### Payload Schema

```jsonc
{
  "bundle_id": "4d8dbf6b-0a49-4d53-9e36-435b0b66391d",
  "sequence": 12,           // For delta synchronization
  "full_snapshot": false,   // true = replaces all previous state
  
  "recordings": [
    {
      // Identity
      "mb_recording_id": "e2f5e9b4-5852-4cd3-b1f9-29a7a4a234bc",
      "acoustid": "5180bfc0-93b9-4c3b-9a51-0f89f7db919b",  // optional
      "fingerprint_hash": "fp:sha1:aa12bb34cc56dd78ee90ff...",
      
      // Metadata
      "title": "Only Shallow",
      "artist": "My Bloody Valentine",
      
      // Audio variants (different files of same recording)
      "variants": [
        {
          "variant_id": "local-5c1a6d13a28f",  // Opaque local ID
          "lossless": true,
          "codec": "FLAC",
          "container": "FLAC",
          "sample_rate_hz": 44100,
          "bit_depth": 16,
          "channels": 2,
          "duration_ms": 242000,
          "file_size_bytes": 34567890,
          "bitrate_kbps": 900,
          "file_hash_sha256": "sha256:19fefa1f...d0e3",
          "transcode_suspect": false,
          "quality_score": 0.97
        },
        {
          "variant_id": "local-a9c1b93bc010",
          "lossless": false,
          "codec": "MP3",
          "container": "MP3",
          "sample_rate_hz": 44100,
          "bit_depth": 16,
          "channels": 2,
          "duration_ms": 242000,
          "file_size_bytes": 8700000,
          "bitrate_kbps": 320,
          "file_hash_sha256": "sha256:77ec...aa29",
          "transcode_suspect": true,
          "quality_score": 0.70
        }
      ]
    }
  ]
}
```

### C# Data Model

```csharp
namespace slskd.Mesh.Brainz
{
    /// <summary>
    /// Bundle of fingerprint observations for epidemic sync.
    /// </summary>
    public sealed record FingerprintBundleAdvert
    (
        Guid BundleId,
        long Sequence,
        bool FullSnapshot,
        IReadOnlyList<RecordingFingerprintEntry> Recordings
    );

    /// <summary>
    /// Fingerprint and variants for a single recording.
    /// </summary>
    public sealed record RecordingFingerprintEntry
    (
        string? MbRecordingId,
        string? AcoustId,
        string FingerprintHash,  // Stable hash of Chromaprint output
        string Title,
        string Artist,
        IReadOnlyList<AudioVariant> Variants
    );

    /// <summary>
    /// A specific audio file variant of a recording.
    /// </summary>
    public sealed record AudioVariant
    (
        string VariantId,          // Opaque local identifier
        bool Lossless,
        string Codec,
        string Container,
        int SampleRateHz,
        int BitDepth,
        int Channels,
        int DurationMs,
        long FileSizeBytes,
        int BitrateKbps,
        string FileHashSha256,
        bool TranscodeSuspect,
        double QualityScore        // 0.0 - 1.0
    );
}
```

### Example Message

```jsonc
{
  "type": "fingerprint_bundle_advert",
  "version": 1,
  "message_id": "7a4ff957-1668-4a1a-bcb4-1376a0380559",
  "sender_id": "mesh-node-abc123",
  "timestamp": "2025-12-09T23:16:10.444Z",
  "payload": {
    "bundle_id": "4d8dbf6b-0a49-4d53-9e36-435b0b66391d",
    "sequence": 12,
    "full_snapshot": false,
    "recordings": [
      {
        "mb_recording_id": "e2f5e9b4-5852-4cd3-b1f9-29a7a4a234bc",
        "fingerprint_hash": "fp:sha1:aa12bb34cc56dd78ee90ff...",
        "title": "Only Shallow",
        "artist": "My Bloody Valentine",
        "variants": [
          {
            "variant_id": "local-5c1a6d13a28f",
            "lossless": true,
            "codec": "FLAC",
            "sample_rate_hz": 44100,
            "bit_depth": 16,
            "quality_score": 0.97
          }
        ]
      }
    ]
  }
}
```

---

## Message Type 3: Mesh Cache Job

### Purpose

Negotiate overlay-assisted album completion, including:
- **Requests**: "I need these tracks"
- **Offers**: "I can serve these tracks as cache/relay"

### When to Send

- **Request**: When starting album download with incomplete sources
- **Offer**: In response to request, or proactively for popular albums
- **Update**: When availability or constraints change

### Payload Schema (Request)

```jsonc
{
  "job_id": "0f4de638-56f3-4a0f-b8fa-64f85c6b6a8f",
  "role": "request",
  
  // Target album
  "mb_release_id": "c0d0c0a4-4a26-4d74-9c02-67c9321b3b22",
  "title": "Loveless",
  "artist": "My Bloody Valentine",
  
  // What requester needs
  "requested_tracks": [
    {
      "track_number": 2,
      "mb_recording_id": "4863d0b0-7920-4e1d-ba55-e00e39c6bdaa",
      "priority": 10,  // Higher = more urgent
      "desired_profile": {
        "preferred_codecs": ["FLAC"],
        "allowed_codecs": ["FLAC", "ALAC", "WAV"],
        "min_bitrate_kbps": 800,
        "lossless_required": true
      }
    }
  ],
  
  "offered_tracks": [],  // Empty for requests
  
  // Requester's constraints
  "constraints": {
    "ttl_seconds": 600,
    "max_total_overlay_download_kib": 500000,
    "max_concurrent_overlay_peers": 2,
    "max_overlay_bandwidth_kbps": 3000,
    "prefer_soulseek_primary": true,
    "allow_transitive_relay": true,
    "allow_new_soulseek_fetch": true
  }
}
```

### Payload Schema (Offer)

```jsonc
{
  "job_id": "0f4de638-56f3-4a0f-b8fa-64f85c6b6a8f",  // Same as request
  "role": "offer",
  
  "mb_release_id": "c0d0c0a4-4a26-4d74-9c02-67c9321b3b22",
  "title": "Loveless",
  "artist": "My Bloody Valentine",
  
  "requested_tracks": [],  // Empty for offers
  
  // What offerer can serve
  "offered_tracks": [
    {
      "track_number": 2,
      "mb_recording_id": "4863d0b0-7920-4e1d-ba55-e00e39c6bdaa",
      "variant_id": "local-5c1a6d13a28f",  // References AudioVariant
      "lossless": true,
      "codec": "FLAC",
      "bitrate_kbps": 900
    }
  ],
  
  // Offerer's constraints
  "constraints": {
    "ttl_seconds": 600,
    "max_total_overlay_upload_kib": 800000,
    "max_concurrent_overlay_peers": 3,
    "max_overlay_bandwidth_kbps": 5000,
    "respect_soulseek_slots": true,
    "max_soulseek_backhaul_slots": 1
  }
}
```

### C# Data Model

```csharp
namespace slskd.Mesh.Brainz
{
    /// <summary>
    /// Request or offer for mesh-assisted album completion.
    /// </summary>
    public sealed record MeshCacheJob
    (
        Guid JobId,
        string Role,  // "request" or "offer"
        string MbReleaseId,
        string? Title,
        string? Artist,
        IReadOnlyList<RequestedTrack> RequestedTracks,
        IReadOnlyList<OfferedTrack> OfferedTracks,
        MeshJobConstraints Constraints
    );

    /// <summary>
    /// Track requested by downloader.
    /// </summary>
    public sealed record RequestedTrack
    (
        int TrackNumber,
        string? MbRecordingId,
        int Priority,
        DesiredProfile DesiredProfile
    );

    /// <summary>
    /// Track offered by cache/relay.
    /// </summary>
    public sealed record OfferedTrack
    (
        int TrackNumber,
        string? MbRecordingId,
        string VariantId,  // References AudioVariant.VariantId
        bool Lossless,
        string Codec,
        int BitrateKbps
    );

    /// <summary>
    /// Desired audio characteristics for request.
    /// </summary>
    public sealed record DesiredProfile
    (
        IReadOnlyList<string> PreferredCodecs,
        IReadOnlyList<string> AllowedCodecs,
        int? MinBitrateKbps,
        bool LosslessRequired
    );

    /// <summary>
    /// Bandwidth and fairness constraints for mesh job.
    /// </summary>
    public sealed record MeshJobConstraints
    (
        int TtlSeconds,
        long? MaxTotalOverlayDownloadKib,
        long? MaxTotalOverlayUploadKib,
        int MaxConcurrentOverlayPeers,
        int MaxOverlayBandwidthKbps,
        bool PreferSoulseekPrimary,
        bool AllowTransitiveRelay,
        bool AllowNewSoulseekFetch,
        bool? RespectSoulseekSlots,
        int? MaxSoulseekBackhaulSlots
    );
}
```

### Example Message (Request)

```jsonc
{
  "type": "mesh_cache_job",
  "version": 1,
  "message_id": "f7a5cd9c-77fd-4fae-9b4b-83f0a3d5e5b9",
  "sender_id": "mesh-node-xyz789",
  "timestamp": "2025-12-09T23:17:55.000Z",
  "payload": {
    "job_id": "0f4de638-56f3-4a0f-b8fa-64f85c6b6a8f",
    "role": "request",
    "mb_release_id": "c0d0c0a4-4a26-4d74-9c02-67c9321b3b22",
    "title": "Loveless",
    "requested_tracks": [
      {
        "track_number": 2,
        "mb_recording_id": "4863d0b0-7920-4e1d-ba55-e00e39c6bdaa",
        "priority": 10,
        "desired_profile": {
          "preferred_codecs": ["FLAC"],
          "lossless_required": true
        }
      }
    ],
    "constraints": {
      "ttl_seconds": 600,
      "prefer_soulseek_primary": true
    }
  }
}
```

---

## Protocol Flow Example

### Scenario: Node B wants "Loveless" (MB Release)

```
┌──────────┐         ┌──────────┐         ┌──────────┐
│  Node A  │         │  Node B  │         │  Node C  │
│ (has it) │         │ (wants)  │         │ (cache)  │
└──────────┘         └──────────┘         └──────────┘

     │                      │                     │
     │  fingerprint_bundle  │                     │
     ├─────────────────────>│                     │
     │    (periodic sync)   │                     │
     │                      │                     │
     │                      │  mesh_cache_job     │
     │                      │  (role=request)     │
     │                      ├────────────────────>│
     │                      │                     │
     │  mbid_swarm_descriptor                    │
     │<─────────────────────┤                     │
     │   (A has track 2)    │                     │
     │                      │                     │
     │                      │  mesh_cache_job     │
     │                      │  (role=offer)       │
     │                      │<────────────────────┤
     │                      │  (C can relay)      │
     │                      │                     │
     │  ┌──────────────────┐│                     │
     │  │ B's swarm engine:││                     │
     │  │ - Download from A (Soulseek)            │
     │  │ - Download from C (overlay relay)       │
     │  │ - Verify via fingerprint                │
     │  └──────────────────┘│                     │
     │                      │                     │
     │  chunk_request       │                     │
     │<─────────────────────┤                     │
     │  chunk_response      │                     │
     ├─────────────────────>│                     │
     │                      │                     │
     │                      │  chunk_request      │
     │                      ├────────────────────>│
     │                      │  chunk_response     │
     │                      │<────────────────────┤
     │                      │                     │
```

### Flow Steps

1. **Node A** periodically broadcasts `fingerprint_bundle_advert` with its library
2. **Node B** wants "Loveless", sends `mesh_cache_job` (request) to neighbors
3. **Node A** responds with `mbid_swarm_descriptor` (has track 2 available)
4. **Node C** responds with `mesh_cache_job` (offer) to act as relay
5. **Node B** starts multi-source download:
   - Opens Soulseek connection to Node A (primary)
   - Opens overlay connection to Node C (acceleration)
6. Chunks flow over both paths, verified via fingerprint
7. Node B completes album, emits own `mbid_swarm_descriptor`

---

## Integration with Existing Systems

### Mesh Sync Service

```csharp
// Add to MeshMessageType enum
public enum MeshMessageType
{
    // ... existing types ...
    
    // Brainz protocol
    MbidSwarmDescriptor = 20,
    FingerprintBundleAdvert = 21,
    MeshCacheJob = 22,
}

// Message router
public async Task HandleMessageAsync(MeshEnvelope envelope)
{
    switch (envelope.Type)
    {
        case "mbid_swarm_descriptor":
            var descriptor = JsonSerializer.Deserialize<MbidSwarmDescriptor>(envelope.Payload);
            await _brainzService.HandleSwarmDescriptorAsync(descriptor);
            break;
            
        case "fingerprint_bundle_advert":
            var bundle = JsonSerializer.Deserialize<FingerprintBundleAdvert>(envelope.Payload);
            await _brainzService.HandleFingerprintBundleAsync(bundle);
            break;
            
        case "mesh_cache_job":
            var job = JsonSerializer.Deserialize<MeshCacheJob>(envelope.Payload);
            await _brainzService.HandleCacheJobAsync(job);
            break;
    }
}
```

### Multi-Source Swarm Integration

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
    public double? QualityScore { get; set; }
}

// Swarm grouping now considers MBID
public string GetSwarmKey(SourcePeer peer)
{
    if (!string.IsNullOrEmpty(peer.MbRecordingId) && 
        !string.IsNullOrEmpty(peer.CodecProfile))
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

---

## Security & Privacy

### What is Shared
- ✓ MusicBrainz Release/Recording IDs
- ✓ Audio fingerprints (Chromaprint hashes)
- ✓ Codec/bitrate/technical metadata
- ✓ Availability status (complete/missing)

### What is NOT Shared
- ✗ File paths or names
- ✗ Directory structure
- ✗ Play counts or listening history
- ✗ Full library inventory (only active/seeded items)

### Anti-Abuse Measures
- **Rate limiting**: Max 100 active MBID announcements per node
- **TTL enforcement**: Jobs expire after `ttl_seconds`
- **Bandwidth quotas**: Configurable per-node limits
- **Token validation**: DHT values include anti-replay tokens
- **Fingerprint verification**: Prevent serving wrong content

### Opt-Out
Users can disable Brainz features entirely:
```yaml
brainz:
  enabled: false  # Disable all MusicBrainz mesh features
```

---

## Future Extensions

### Chunk Transfer Messages
Next iteration should define:
- `chunk_request`: Request byte range for a `variant_id`
- `chunk_response`: Deliver chunk data
- `chunk_cancel`: Abort in-progress transfer

### Quality Voting
- `quality_vote`: Submit/sync crowd-sourced quality ratings
- `transcode_report`: Flag suspected transcodes

### Album Metadata Sync
- `release_metadata`: Share extended MB release data
- `cover_art`: Distribute album artwork

---

## References

- [MusicBrainz API](https://musicbrainz.org/doc/MusicBrainz_API)
- [Chromaprint](https://oxygene.sk/2011/01/how-does-chromaprint-work/)
- [AcoustID](https://acoustid.org/webservice)
- [slskdn Multi-Source Design](./docs/archive/duplicates/MULTI_SOURCE_DOWNLOADS.md)
- [slskdn DHT Rendezvous](./DHT_RENDEZVOUS_DESIGN.md)

---

*Last updated: 2025-12-09*

