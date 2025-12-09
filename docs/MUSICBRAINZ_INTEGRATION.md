# MusicBrainz & Discogs Integration Design

> **Status**: Experimental Design  
> **Branch**: `experimental/brainz`  
> **Parent**: `experimental/multi-source-swarm`

This document outlines the integration of MusicBrainz IDs, Discogs IDs, and acoustic fingerprinting into slskdn's multi-source swarm + DHT mesh architecture.

---

## Philosophy

**Core Principle**: Soulseek remains the canonical social/search layer and data origin. MusicBrainz/Discogs/fingerprints add **semantic intelligence** on top, while the DHT mesh becomes a **content-aware overlay** that understands real-world music identity.

**Key Insight**: Multi-swarm already abstracts away filenames/paths for content-addressed chunks. Extending this to **acoustically-addressed chunks** (via fingerprints + MBIDs) is the natural evolution.

---

## Three-Phase Approach

### Phase 1: "Boring but Shippable" (Foundation)

The baseline features any sane product manager would spec. These work entirely within a single slskdn instance with no mesh changes.

#### 1.1 ID-Aware Search & Album Targets

**Feature**: Search by MusicBrainz/Discogs ID (#53)

**UI Components**:
- Input fields for:
  - MusicBrainz Release ID (album)
  - MusicBrainz Recording ID (track)
  - Discogs Release ID / Master ID
- URL auto-detection (paste MB/Discogs URLs, extract IDs)

**Backend Flow**:
1. User submits MB Release ID
2. slskdn queries MusicBrainz API:
   - Full tracklist with recordings
   - Track durations, positions
   - Release metadata (country, format, label)
3. Creates internal "Album Target" object:
   ```csharp
   public class AlbumTarget
   {
       public string MusicBrainzReleaseId { get; set; }
       public string DiscogsReleaseId { get; set; }
       public string Title { get; set; }
       public string Artist { get; set; }
       public List<TrackTarget> Tracks { get; set; }
       public ReleaseMetadata Metadata { get; set; }
   }
   
   public class TrackTarget
   {
       public string MusicBrainzRecordingId { get; set; }
       public int Position { get; set; }
       public string Title { get; set; }
       public string Artist { get; set; }
       public TimeSpan Duration { get; set; }
       public string ISRC { get; set; }  // International Standard Recording Code
   }
   ```

4. Multi-swarm pipeline works against this target:
   - For each track: search Soulseek using title/artist/duration
   - Apply fuzzy matching with duration tolerance (±3 seconds)
   - Mark album complete only when all tracks matched

**UI Enhancements**:
- Progress: "Downloading 8/11 tracks for [Album Name]"
- Warnings: "3 extra tracks not in this release (bonus edition?)"
- Smart display: "Missing: Track 4 (Title), Track 7 (Title)"

#### 1.2 Acoustic Fingerprinting (Chromaprint + AcoustID)

**Technology**: [Chromaprint](https://acoustid.org/chromaprint) - open-source audio fingerprinting

**Local Capabilities**:
1. **Fingerprint extraction**:
   - Extract first 120 seconds of audio
   - Generate 32-bit fingerprint using Chromaprint algorithm
   - Store in HashDb alongside byte hash

2. **AcoustID lookup** (web API):
   - Submit fingerprint → get MB Recording ID(s)
   - Disambiguate using metadata hints (title, artist, duration)

3. **Use cases**:

   **Auto-tagging**:
   ```
   User drops badly-named FLAC → fingerprint → AcoustID → MB Recording
   → Pull correct title/artist/album/year tags
   ```

   **De-duplication**:
   ```
   Two files, different names/tags, same fingerprint
   → Treat as duplicate, keep best version (lossless > lossy, highest bitrate)
   ```

   **Best version picker**:
   ```
   For fingerprint group:
   - Rank by: Lossless > Lossy
   - Then by: Bitrate descending
   - Then by: Container preference (FLAC > ALAC > MP3 > AAC)
   ```

   **Strict album matching**:
   ```
   Target: MB Release X with 10 recordings
   Local file: fingerprint → MB Recording Y
   
   If Y in Release X's tracklist:
     ✓ Count towards album completion
   Else:
     ✗ Mark as "wrong version/different release"
   ```

#### 1.3 ID-Aware Multi-Swarm Downloads

**Enhancement**: Tie MBIDs into existing multi-source logic

**Download Job Structure**:
```csharp
public class AlbumDownloadJob
{
    public AlbumTarget Target { get; set; }
    public List<TrackDownloadJob> TrackJobs { get; set; }
}

public class TrackDownloadJob : MultiSourceDownloadJob
{
    // Existing multi-source fields
    public string Filename { get; set; }
    public List<SourcePeer> Sources { get; set; }
    
    // NEW: Semantic fields
    public string MusicBrainzRecordingId { get; set; }
    public TimeSpan ExpectedDuration { get; set; }
    public int TrackPosition { get; set; }
    public string ExpectedFingerprint { get; set; }  // If known
}
```

**Swarm Grouping Logic**:
- **Current**: Group by `(byte_hash, size)`
- **Phase 1**: Group by `(byte_hash, size)` OR `(mb_recording_id, codec_profile)`
  - If two sources have different filenames but fingerprint to same MBID → **same swarm**

**Verification Pipeline**:
```
1. Download completes
2. Extract fingerprint
3. Query AcoustID → get MB Recording ID
4. Compare to TrackDownloadJob.MusicBrainzRecordingId
5. If match: ✓ Accept
   If mismatch: ✗ Flag as "wrong recording" + retry
```

**UI Display**:
```
Album: Kind of Blue (MB:1234-5678-9abc)
├─ Track 1: So What [✓ Complete, verified MBID]
├─ Track 2: Freddie Freeloader [⬇ Downloading from 3 sources]
│   ├─ peer1.slsk: 45% (filename: 02-freddie.flac)
│   ├─ peer2.slsk: 78% (filename: Freddie Freeloader.mp3)
│   └─ peer3.mesh: 67% (fingerprint-verified, different source)
└─ Track 3: Blue in Green [⏸ Queued]
```

---

### Phase 2: "Distinctive but Safe" (Mesh Integration)

Extends Phase 1 to use DHT mesh for discovery and coordination, but no aggressive relaying yet.

#### 2.1 Fingerprint-Driven Swarms: Semantic Content Identity

**Concept**: Swarm key becomes **semantic** instead of purely content-addressed.

**Swarm Key Evolution**:
```
Current:  swarm_key = SHA256(first_32KB)
Phase 2:  swarm_key = DeriveKey(mb_recording_id, codec_profile)

Where codec_profile = {
  codec: "flac" | "mp3" | "aac" | "opus",
  bitrate: 320 | 256 | 128 | ...,
  sample_rate: 44100 | 48000 | 96000 | ...,
  channels: 2 | 1,
  bit_depth: 16 | 24  // for lossless
}
```

**Swarm Membership**:
- Any file where:
  - `fingerprint(file) → mb_recording_id`
  - `audio_properties(file) ≈ codec_profile`
- Can join the swarm, **even if**:
  - Filename completely different
  - Tags are wrong
  - From different album/compilation

**Example**:
```
MB Recording: "Blue in Green" (uuid: abc-123-def)
Codec Profile: FLAC, 16/44.1, stereo

Files that join this swarm:
✓ "02 - Blue in Green.flac"      (from "Kind of Blue" album)
✓ "Blue_In_Green_Live.flac"       (from "Live at Newport" compilation)
✓ "track02.flac"                  (from poorly tagged rip)

Files that DON'T:
✗ "Blue in Green 128.mp3"         (different codec_profile)
✗ "Blue in Green Alt Take.flac"   (different recording uuid)
```

**HashDb Schema Extension**:
```sql
-- Extend HashDb table
ALTER TABLE HashDb ADD COLUMN mb_recording_id TEXT;
ALTER TABLE HashDb ADD COLUMN codec_profile TEXT;  -- JSON blob
ALTER TABLE HashDb ADD COLUMN audio_fingerprint TEXT;  -- Chromaprint

-- New index for semantic lookup
CREATE INDEX idx_hashdb_semantic ON HashDb(mb_recording_id, codec_profile);

-- FileSources now grouped by semantic identity
-- Multiple byte_hashes can map to same (mb_recording_id, codec_profile)
```

#### 2.2 ID-Level DHT Overlay: Cross-Client Discovery

**DHT Announcement Strategy**:

**DO NOT** abuse BitTorrent DHT by announcing every MBID you have.

**DO** announce actively-worked items:
- Albums currently downloading
- Albums you're configured to "seed" (high-value releases)
- Limited to top N (e.g., 100) most active MBIDs per node

**DHT Key Derivation**:
```python
def mbid_dht_key(mb_release_id: str) -> bytes:
    """
    Generate Kademlia key for MB Release discovery.
    Namespace-prefixed to avoid collisions with real torrents.
    """
    return sha1(b"slskdn-mb-v1:" + mb_release_id.encode())
```

**DHT Value Format** (BEncoded):
```python
{
  "overlay_addr": "ip:port",          # TLS overlay endpoint
  "peer_id": "<20-byte-peer-id>",     # For dedup
  "caps": {
    "flac": True,     # Can serve lossless
    "mp3": True,      # Can serve lossy
    "complete": True, # Has full album
  },
  "timestamp": 1670000000,  # Unix time
  "token": "<16-byte-random>",  # Anti-abuse, rotated
}
```

**Discovery Flow**:
```
User: "Download MB Release abc-123"

1. slskdn → BitTorrent DHT: GET_PEERS(key=sha1("slskdn-mb-v1:abc-123"))
2. DHT returns: [peer1_value, peer2_value, ...]
3. For each peer:
   a. Connect to overlay_addr via TLS
   b. Handshake with peer_id + token
   c. Request: "Do you have tracks 3,7,9 of Release abc-123?"
   d. If yes: Add to multi-source swarm
4. Also search Soulseek (canonical)
5. Combine sources: Soulseek + Mesh peers

Result: Faster discovery, more sources, better NAT traversal
```

**Rate Limiting & Anti-Abuse**:
- **Announce limit**: 100 MBIDs per node
- **Announce interval**: 15 minutes per MBID
- **Token rotation**: Every 30 minutes
- **Mesh handshake**: Require valid token from DHT value
- **Respect DHT etiquette**: No flood announcements

#### 2.3 "Curated Editions" via Fingerprint Consensus

**Problem**: Soulseek has transcodes, bad rips, fake FLACs, etc.

**Solution**: Crowd-sourced quality signals using fingerprints + mesh sync

**Data Collection** (per node):
```csharp
public class FingerprintObservation
{
    public string MusicBrainzRecordingId { get; set; }
    public string CodecProfile { get; set; }  // "flac-16-44100"
    public string Fingerprint { get; set; }   // Chromaprint hash
    
    // Audio analysis (local, no upload)
    public int DynamicRange { get; set; }     // DR meter
    public bool HasClipping { get; set; }
    public float LoudnessLUFS { get; set; }
    public string EncoderSignature { get; set; }  // "LAME 3.100", "Fraunhofer", etc.
    
    // Provenance (anonymous)
    public int SeenCount { get; set; }        // How many peers observed this
    public DateTimeOffset FirstSeen { get; set; }
    public DateTimeOffset LastSeen { get; set; }
}
```

**Mesh Sync**: Propagate **aggregated stats only** (not files):
```json
{
  "mb_recording_id": "abc-123-def",
  "codec_profile": "flac-16-44100",
  "observations": [
    {
      "fingerprint_hash": "sha256:...",
      "seen_count": 87,
      "avg_dr": 11.2,
      "clipping_rate": 0.01,
      "common_encoder": "FLAC 1.3.3"
    },
    {
      "fingerprint_hash": "sha256:...",
      "seen_count": 12,
      "avg_dr": 6.1,
      "clipping_rate": 0.23,
      "common_encoder": "FLAC 1.2.1"
    }
  ]
}
```

**Quality Labeling** (in search results):
```
Search: "Kind of Blue - So What"

Results:
1. peer1: So What.flac (24MB)
   ✓ Canonical master (87 peers, DR11, no clipping)
   
2. peer2: so_what_HQ.flac (18MB)
   ⚠ Likely transcode (12 peers, DR6, clipping detected)
   
3. peer3: So What 320.mp3 (8MB)
   ✓ Good lossy (320kbps LAME, no transcode)
```

**Download Modes**:
- **"Any"**: Accept any source (current behavior)
- **"Canonical Master"**: Only accept fingerprints matching consensus "good" version
- **"No Transcodes"**: Block sources flagged as transcodes

---

### Phase 3: "Ingenious" (Advanced Mesh Behaviors)

Fully exploits DHT mesh + fingerprints to create emergent behaviors impossible in vanilla Soulseek.

#### 3.1 Mesh Caches / Super Peers for Popular MBIDs

**Concept**: Voluntary caching nodes that amplify slow Soulseek uploads into fast mesh distribution.

**Cache Node Behavior**:
1. Node opts in as cache for certain MBIDs:
   ```yaml
   mesh:
     cache_mode: true
     cached_mbids:
       - "mb:abc-123"  # Kind of Blue
       - "mb:def-456"  # Abbey Road
     cache_quota: 50GB
   ```

2. Cache node:
   - Downloads full album from Soulseek (once)
   - Verifies all tracks via fingerprints → MBIDs
   - Announces to DHT with `cache: true` capability
   - Serves chunks to other slskdn peers over TLS overlay

3. Other nodes:
   - Discover cache via DHT
   - Connect over mesh (fast, no Soulseek queue/slot limits)
   - Download chunks directly from cache
   - Cache, in turn, maintains Soulseek origin as backup

**Fan-Out Amplification**:
```
Scenario: 100 users want "Kind of Blue" (MB:abc-123)

Without cache:
- Original Soulseek uploader: 100 uploads × 500MB = 50GB uploaded
- Slow (upload slots, queues)

With 3 cache nodes:
- Cache nodes download once from Soulseek: 3 × 500MB = 1.5GB
- Caches serve 100 users over mesh: Fast TLS overlay
- Original uploader: Only 1.5GB uploaded total
- Users get 10-50x faster downloads (mesh peering vs Soulseek slots)
```

**Cache Invalidation**:
- Fingerprint mismatch → purge and re-download
- TTL: 90 days for unpopular, infinite for manually pinned
- LRU eviction when quota exceeded

#### 3.2 Mesh-Based "Album Completion Jobs" (Distributed Wishlist)

**Concept**: Propagate "what I'm looking for" across mesh, enable cooperative discovery.

**Job Message Format**:
```csharp
public class AlbumCompletionJob
{
    public string JobId { get; set; }  // UUID
    public string MusicBrainzReleaseId { get; set; }
    public List<string> MissingRecordingIds { get; set; }  // MBIDs of tracks
    public JobPriority Priority { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public int HopCount { get; set; }  // TTL for propagation
    
    // Optionally include mesh peer ID to coordinate responses
    public string RequestorPeerId { get; set; }
}
```

**Propagation via Mesh Sync**:
- Jobs propagate epidemic-style (like hash sync)
- Max 3 hops to prevent spam
- Coalesced: "20 peers want tracks 3,7 of Release X"

**Cooperative Behaviors**:

**Behavior 1: Proactive Sharing**
```
Peer A has full album, sees job for tracks 3,7
→ Ensures those tracks are shared on Soulseek
→ Maybe even uploads them pre-emptively to mesh cache
```

**Behavior 2: Coordinated Search**
```
Peer B doesn't have tracks, but sees job
→ Also starts searching Soulseek for them
→ If found, shares results back to mesh (via DHT announcement)
```

**Behavior 3: Cache Pre-Fetch**
```
Cache node sees 20 peers want tracks 3,7
→ High demand, fetch from Soulseek now
→ Add to cache for future mesh serving
```

**UI**:
```
Wishlist Item: Kind of Blue (MB:abc-123)
Status: 8/11 tracks complete
Missing: Track 3 (Blue in Green), Track 7 (All Blues), Track 9 (Flamenco Sketches)

Mesh Activity:
- 17 other peers also looking for these tracks
- 2 cache nodes attempting to locate
- Last update: 5 minutes ago
```

#### 3.3 Fingerprint-Driven NAT/CGNAT Rescue

**Problem**: CGNAT users can't accept incoming connections → can't download from many Soulseek peers.

**Current slskdn NAT traversal**: Beacons help with peer discovery but don't relay data.

**Phase 3 Enhancement**: Content-aware relaying with fingerprint verification.

**NAT Rescue Flow**:
```
Scenario: User behind CGNAT wants MB Release abc-123

1. Search Soulseek for Track 1
   → Finds peer X (also behind NAT, can't connect)
   
2. slskdn checks mesh:
   → Peer Y (beacon/relay) has this track (fingerprint verified to MBID)
   → Peer Y has good connectivity (public IP, UPnP working)
   
3. User connects to Peer Y via TLS overlay
   → Downloads chunks from Peer Y
   
4. Peer Y (relay):
   → If doesn't have full file:
     → Fetches from Peer X via Soulseek (if possible)
     → Or from another mesh peer
   → Serves chunks to user
   
5. Verification:
   → User's slskdn extracts fingerprint
   → Compares to expected MBID
   → If match: Accept
   → If mismatch: Flag relay as untrusted
```

**Relay Trust & Anti-Abuse**:
- **Fingerprint verification required**: Relay can't serve wrong file
- **Bandwidth quotas**: Relays set max relay rate (e.g., 10 Mbps, 50 GB/day)
- **Reputation**: Track relay success rate, prefer high-trust relays
- **Encryption**: TLS overlay prevents MITM

**Result**: CGNAT users can access Soulseek content via mesh, with cryptographic proof it's the right content (via MBID + fingerprint).

---

## Data Model & Schema Extensions

### HashDb Extensions

```sql
-- Add MusicBrainz/Discogs columns to HashDb
ALTER TABLE HashDb ADD COLUMN mb_recording_id TEXT;
ALTER TABLE HashDb ADD COLUMN discogs_release_id TEXT;
ALTER TABLE HashDb ADD COLUMN audio_fingerprint TEXT;  -- Chromaprint
ALTER TABLE HashDb ADD COLUMN codec_profile TEXT;  -- JSON: codec, bitrate, sample_rate, etc.
ALTER TABLE HashDb ADD COLUMN quality_score REAL;  -- Derived from consensus
ALTER TABLE HashDb ADD COLUMN is_transcode BOOLEAN DEFAULT FALSE;

-- Indexes
CREATE INDEX idx_hashdb_mbid ON HashDb(mb_recording_id);
CREATE INDEX idx_hashdb_fingerprint ON HashDb(audio_fingerprint);
CREATE INDEX idx_hashdb_semantic ON HashDb(mb_recording_id, codec_profile);
```

### New Tables

```sql
-- Album Targets (from MB/Discogs)
CREATE TABLE AlbumTargets (
    id TEXT PRIMARY KEY,
    mb_release_id TEXT,
    discogs_release_id TEXT,
    title TEXT NOT NULL,
    artist TEXT NOT NULL,
    release_date TEXT,
    total_tracks INTEGER,
    created_at INTEGER NOT NULL,
    status TEXT DEFAULT 'incomplete'  -- incomplete, in_progress, complete
);

-- Track Targets (tracklist)
CREATE TABLE TrackTargets (
    id TEXT PRIMARY KEY,
    album_target_id TEXT NOT NULL,
    mb_recording_id TEXT,
    position INTEGER NOT NULL,
    title TEXT NOT NULL,
    artist TEXT,
    duration_ms INTEGER,
    isrc TEXT,
    FOREIGN KEY (album_target_id) REFERENCES AlbumTargets(id) ON DELETE CASCADE
);

-- Fingerprint Observations (for consensus)
CREATE TABLE FingerprintObservations (
    id TEXT PRIMARY KEY,
    mb_recording_id TEXT NOT NULL,
    codec_profile TEXT NOT NULL,
    fingerprint_hash TEXT NOT NULL,
    seen_count INTEGER DEFAULT 1,
    dynamic_range REAL,
    has_clipping BOOLEAN,
    loudness_lufs REAL,
    encoder_signature TEXT,
    first_seen INTEGER NOT NULL,
    last_seen INTEGER NOT NULL
);

CREATE INDEX idx_fp_obs_mbid ON FingerprintObservations(mb_recording_id);
CREATE INDEX idx_fp_obs_hash ON FingerprintObservations(fingerprint_hash);

-- Mesh Album Jobs (distributed wishlist)
CREATE TABLE MeshAlbumJobs (
    job_id TEXT PRIMARY KEY,
    mb_release_id TEXT NOT NULL,
    missing_recording_ids TEXT NOT NULL,  -- JSON array
    priority TEXT DEFAULT 'normal',
    hop_count INTEGER DEFAULT 0,
    created_at INTEGER NOT NULL,
    expires_at INTEGER NOT NULL
);
```

---

## Message Protocol Extensions

### Mesh Messages (TLS Overlay)

```csharp
// New message types for Brainz features
public enum MeshMessageType
{
    // ... existing types ...
    
    // Phase 2
    MBID_QUERY = 20,           // "Do you have tracks for this MBID?"
    MBID_RESPONSE = 21,        // "Yes, I have tracks 1,2,4"
    FINGERPRINT_BATCH = 22,    // Sync fingerprint observations
    
    // Phase 3
    ALBUM_JOB = 30,            // "I'm looking for these tracks"
    CACHE_ANNOUNCE = 31,       // "I'm caching these MBIDs"
    RELAY_REQUEST = 32,        // "Please relay this chunk"
}

// MBID Query
public class MbidQueryMessage
{
    public string MbReleaseId { get; set; }
    public List<string> RecordingIds { get; set; }  // Specific tracks, or null = all
    public string CodecProfile { get; set; }  // Preferred codec
}

// MBID Response
public class MbidResponseMessage
{
    public string MbReleaseId { get; set; }
    public List<TrackAvailability> Tracks { get; set; }
}

public class TrackAvailability
{
    public string RecordingId { get; set; }
    public string CodecProfile { get; set; }
    public long SizeBytes { get; set; }
    public string FingerprintHash { get; set; }
    public bool FingerprintVerified { get; set; }
}

// Fingerprint Batch (for consensus)
public class FingerprintBatchMessage
{
    public List<FingerprintObservation> Observations { get; set; }
    public int BatchSequence { get; set; }
}
```

---

## Implementation Roadmap

### Phase 1: Foundation (Estimate: 4-6 weeks)

**Week 1-2**: MusicBrainz API Integration
- [ ] Create `MusicBrainzClient` service
- [ ] Implement Release/Recording ID lookups
- [ ] Create `AlbumTarget` data model
- [ ] Add UI for ID input (search bar extensions)
- [ ] Store album targets in new SQLite tables

**Week 3-4**: Chromaprint Integration
- [ ] Add Chromaprint native library
- [ ] Implement fingerprint extraction service
- [ ] Integrate AcoustID API client
- [ ] Add fingerprint column to HashDb
- [ ] Build auto-tagging pipeline

**Week 5-6**: ID-Aware Multi-Swarm
- [ ] Extend `MultiSourceDownloadJob` with MBID fields
- [ ] Implement semantic swarm grouping logic
- [ ] Add fingerprint verification to download pipeline
- [ ] Build album completion UI
- [ ] Unit tests + integration tests

### Phase 2: Mesh Integration (Estimate: 6-8 weeks)

**Week 7-9**: Semantic Swarms
- [ ] Refactor swarm key derivation
- [ ] Implement `codec_profile` abstraction
- [ ] Add `mb_recording_id` to swarm matching
- [ ] Update HashDb schema with new columns
- [ ] Test cross-file swarm grouping

**Week 10-12**: DHT MBID Discovery
- [ ] Implement DHT key derivation for MBIDs
- [ ] Build DHT announce/lookup for active albums
- [ ] Add mesh handshake token validation
- [ ] Integrate with existing overlay connector
- [ ] Rate limiting + anti-abuse

**Week 13-14**: Fingerprint Consensus
- [ ] Build `FingerprintObservation` collection
- [ ] Implement mesh sync for observations
- [ ] Create quality scoring algorithm
- [ ] Add transcode detection
- [ ] UI labels for search results

### Phase 3: Advanced Behaviors (Estimate: 8-12 weeks)

**Week 15-18**: Mesh Caches
- [ ] Design cache node configuration
- [ ] Implement cache-enabled DHT announcements
- [ ] Build chunk serving over TLS overlay
- [ ] Add LRU eviction logic
- [ ] Performance testing

**Week 19-22**: Distributed Wishlist
- [ ] Define `AlbumCompletionJob` message format
- [ ] Implement epidemic propagation
- [ ] Build job coalescing logic
- [ ] UI for mesh activity display
- [ ] Cooperative behavior triggers

**Week 23-26**: NAT Rescue
- [ ] Extend relay logic with fingerprint verification
- [ ] Implement relay trust/reputation system
- [ ] Add bandwidth quotas for relays
- [ ] Test CGNAT scenarios
- [ ] Security audit for relay path

---

## Success Metrics

### Phase 1
- **Album completion rate**: % of album targets completed successfully
- **Tag accuracy**: % of files with correct MB metadata after auto-tagging
- **De-dupe effectiveness**: % reduction in duplicate files
- **User satisfaction**: Survey: "How useful is ID-based search?"

### Phase 2
- **Mesh discovery speed**: Time to find all sources (Soulseek + Mesh) vs Soulseek-only
- **Swarm diversity**: Avg # of sources per semantic swarm
- **Quality labeling accuracy**: % of "good" labels that users agree with
- **False positive rate**: % of "transcode" labels that are incorrect

### Phase 3
- **Cache hit rate**: % of downloads served from mesh caches
- **NAT rescue success**: % of CGNAT users able to complete downloads
- **Wishlist coordination**: Avg time to complete album after job propagation
- **Bandwidth savings**: Reduction in Soulseek upload burden via mesh caching

---

## Security & Privacy Considerations

### What slskdn Shares (Mesh)
- ✓ "I have MB Release X" (for active downloads)
- ✓ Fingerprint observations (aggregated, anonymous)
- ✓ "I'm looking for Track Y of Release Z"

### What slskdn Does NOT Share
- ✗ Your full music library
- ✗ Individual file paths or names
- ✗ Play counts, listening history
- ✗ Any personally identifiable information

### Anti-Abuse Mechanisms
- **Rate limiting**: Max 100 MBID announcements per node
- **Token rotation**: DHT values expire, require valid tokens
- **Reputation**: Track relay/cache node trustworthiness
- **Fingerprint verification**: Cryptographic proof of content identity

### Privacy Options
- **Opt-out**: Disable mesh features entirely, use Soulseek-only
- **Passive mode**: Download from mesh, don't announce
- **Selective sharing**: Only announce specific MBIDs

---

## Open Questions & Future Work

1. **Chromaprint Performance**: Can we fingerprint large libraries without killing CPU?
   - Answer: Background job, throttled, skippable

2. **MusicBrainz Rate Limits**: How to handle 1 req/sec API limit?
   - Answer: Local cache, batch requests, respect 429s

3. **Discogs vs MusicBrainz**: Which to prioritize?
   - Answer: MB primary (free, more complete), Discogs secondary (for obscure stuff)

4. **Fingerprint Collisions**: How rare are false positives?
   - Answer: Very rare (~1 in 10^9), but add duration check as secondary verification

5. **Mesh Scaling**: Can this work with 10,000 nodes?
   - Answer: Yes, epidemic sync + DHT already designed for scale

6. **Soulseek Community Reaction**: Will users see this as "cheating"?
   - Answer: No data hoarding, respects slots/queues, Soulseek remains primary

---

## References

- [MusicBrainz API Documentation](https://musicbrainz.org/doc/MusicBrainz_API)
- [Chromaprint Algorithm](https://oxygene.sk/2011/01/how-does-chromaprint-work/)
- [AcoustID Web Service](https://acoustid.org/webservice)
- [Discogs API](https://www.discogs.com/developers)
- [BitTorrent DHT Protocol (BEP 5)](http://www.bittorrent.org/beps/bep_0005.html)
- [slskdn Multi-Source Design](./MULTI_SOURCE_DOWNLOADS.md)
- [slskdn DHT Rendezvous Design](./DHT_RENDEZVOUS_DESIGN.md)

---

*Last updated: 2025-12-09*

