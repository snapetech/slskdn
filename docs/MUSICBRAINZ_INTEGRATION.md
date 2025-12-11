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

**Technology**: [Chromaprint](https://acoustid.org/chromaprint) - open-source audio fingerprinting (requires the native `chromaprint` library to be installed and accessible to slskdn; enable via `Integration.Chromaprint.Enabled`, and learn MB IDs via `Integration.AcoustId.ClientId`).

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

> AcoustID (`Integration.AcoustId.Enabled` + `Integration.AcoustId.ClientId`) is required to turn fingerprints into MusicBrainz Recording IDs stored in `HashDb.musicbrainz_id`.

**Week 5-6**: ID-Aware Multi-Swarm
- [ ] Extend `MultiSourceDownloadJob` with MBID fields
- [ ] Implement semantic swarm grouping logic
- [ ] Add fingerprint verification to download pipeline
- [ ] Build album completion UI

> The multi-source download job now tracks the target MusicBrainz recording, fingerprint, and per-source MBIDs so the swarm can recognize semantically equivalent chunks even when filenames differ.
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

## Future Enhancements (Beyond Phase 3)

The following features are speculative but implementable extensions to the core MBID/fingerprint architecture. They maintain Soulseek as the canonical data source while adding next-generation client intelligence.

### Library Health & Quality Management

#### 1. Canonical Edition Scoring

**Concept**: Per `(MB Recording ID, codec profile)`, aggregate quality metrics from mesh observations to compute a **canonicality score** for each variant.

**Scoring Factors**:
- **Positive**: Widely observed across peers, matches original release metadata, good dynamic range (DR), no clipping.
- **Negative**: Suspected transcodes (e.g., FLAC with low DR), unusual sample rates, heavy clipping, mismatched duration.

**Implementation**:
- Extend `FingerprintObservations` table with `canonicality_score REAL`.
- Compute score from mesh sync data (seen_count, avg_dr, clipping_rate, encoder_signature).
- Update score as new observations arrive via epidemic sync.

**Use Cases**:
- **Swarm Preference**: Prefer canonical variants when selecting sources for multi-swarm downloads.
- **UI Display**: 
  ```
  Track 3: Blue in Green
  ✓ Canonical master (score 0.93, 87 peers)
  ⚠ Variant A (score 0.41, 12 peers, likely transcode)
  ```

#### 2. "Collection Doctor" - Library Health Scanner

**Concept**: Background job that walks the user's local library, fingerprints each file, matches to MBIDs, and flags quality/metadata issues.

**Detection Rules**:
- **Suspected Transcodes**: File claims FLAC but fingerprint matches low DR variant in mesh consensus.
- **Wrong Tags**: File's embedded tags (album/artist) don't match the MBID's canonical release metadata.
- **Missing Tracks**: Detected MBIDs belong to a MB Release with more tracks than present locally.
- **Duplicate Variants**: Multiple files with same MBID but different quality scores.

**UI**: Library Health Dashboard
```
Library Health Report (Last Scan: 2 hours ago)

⚠ 143 suspected transcodes detected
⚠ 27 albums missing tracks relative to MusicBrainz release
✓ 89% of library matches canonical editions
✓ 1,234 tracks fingerprinted and verified

Actions:
[Fix Transcodes via Multi-Swarm] [Complete Missing Albums] [Re-Tag Mismatches]
```

**Implementation**:
- New `LibraryHealthService` background service.
- Scan triggers via manual button or scheduled job (weekly).
- Results stored in `LibraryHealthIssues` table.
- One-click remediation: creates multi-swarm jobs to replace flagged files.

---

### Swarm Intelligence & Scheduling

#### 3. RTT + Throughput-Aware Swarm Scheduler

**Concept**: Dynamic per-peer cost modeling to optimize chunk assignment in multi-source swarms.

**Peer Stats Tracked**:
- RTT (round-trip time)
- Throughput (bytes/sec over last N chunks)
- Error rate (chunk validation failures)
- Timeout rate

**Cost Model**:
```
cost(peer) = (1 / throughput) + (penalty_error_rate × error_rate) + (penalty_timeout × timeout_rate)
```

**Scheduling Strategy**:
- **High-priority chunks** (near playback head, end of job): assign to low-cost peers.
- **Low-priority chunks**: assign to slow-but-reliable peers.
- **Rebalance dynamically**: if peer performance degrades mid-job, reassign remaining chunks.

**Result**: CDN-style scheduling using Soulseek + overlay peers as backend.

**Implementation**:
- Extend `SourcePeer` model with `avg_rtt`, `avg_throughput`, `error_rate`, `timeout_rate`.
- Update `MultiSourceDownloadScheduler` to compute cost per peer before chunk assignment.
- Periodically recompute costs (every 5-10 chunks).

#### 4. "Rescue Mode" for Stalled Soulseek Transfers

**Concept**: When a Soulseek transfer is stuck in queue or crawling (<10 KB/s), use mesh to complete the file while keeping the original transfer alive.

**Detection Triggers**:
- Transfer queued for >30 minutes with no progress.
- Transfer active but throughput <10 KB/s for >5 minutes.
- Transfer stalled (no bytes received for >2 minutes).

**Rescue Flow**:
1. Mark transfer as "critical but underperforming".
2. Query mesh via DHT: "Who has MB Recording X with fingerprint Y?"
3. If mesh peers found:
   - Start overlay swarm for missing byte ranges.
   - Keep Soulseek transfer alive (deprioritized).
4. If Soulseek transfer recovers: rebalance swarm to prefer it again.
5. If Soulseek transfer dies: rely entirely on mesh.

**UI Indicator**:
```
Track 5: Downloading (Rescue Mode Active)
├─ Soulseek peer A: Stalled (2 KB/s, 12% complete)
├─ Mesh peer B: 45% (overlay)
└─ Mesh peer C: 23% (overlay)
```

**Implementation**:
- Extend `DownloadTracker` with stall detection logic.
- Raise `TransferStalledEvent` when triggers fire.
- `RescueService` subscribes to event, queries DHT for MBID-based peers.
- Integrates with existing `MultiSourceDownloadJob` to add mesh sources mid-transfer.

---

### Discovery & Crate-Digging

#### 5. Release-Graph Guided Discovery

**Concept**: Use MusicBrainz's relationship graph to suggest related albums/artists for bulk downloading.

**Graph Queries**:
- **Same Artist**: Other studio albums, EPs, live recordings.
- **Related Artists**: Shared personnel (producer, engineer, session musicians), same label, similar genre tags.
- **Release Relationships**: Remasters, deluxe editions, different regions.

**UI Workflows**:
- **"Complete Artist Discography"**: User selects artist → slskdn queries MB for all studio albums → creates multi-album job.
- **"Related Albums"**: User viewing album X → sidebar shows "Also by this producer", "Same label", "Featured artist Y" → one-click to add to queue.

**Implementation**:
- `MusicBrainzGraphService` to query MB API for relationships.
- Cache results locally to respect rate limits.
- New UI component: "Discovery Sidebar" in album detail view.
- Generates `DiscographyDownloadJob` which spawns multiple `AlbumDownloadJob` instances.

#### 6. "Label Crate" Mode

**Concept**: Discover and download curated chunks of a label's catalog based on mesh popularity.

**Discovery Logic**:
- Query mesh: "Which MBIDs on label X are most commonly present in peer fingerprint adverts?"
- Rank by:
  - Peer count (how many slskdn nodes have it).
  - Average canonicality score.
- Filter by user preferences (e.g., only studio albums, only FLAC).

**UI Workflow**:
```
Label: 4AD Records

Popular Albums (in your mesh neighbourhood):
✓ Loveless - My Bloody Valentine (87 peers, score 0.95)
✓ Surfer Rosa - Pixies (64 peers, score 0.91)
✓ Treasure - Cocteau Twins (52 peers, score 0.88)

[Download Top 10] [Download All]
```

**Implementation**:
- Extend mesh sync to include label metadata in fingerprint adverts.
- New `LabelCatalogService` to aggregate mesh data by label.
- New UI: "Label Browser" with search and popularity sorting.

---

### Privacy, Fairness & Reputation

#### 7. Lightweight Local-Only Peer Reputation

**Concept**: Track per-peer behavior locally (never shared) to avoid bad actors without centralized reputation.

**Tracked Metrics**:
- Timeout rate (% of chunk requests that timed out).
- Corruption rate (% of chunks that failed hash validation).
- Speed consistency (variance in throughput over time).
- Truthfulness (do advertised MBIDs actually match delivered content?).

**Reputation Score** (local only):
```
reputation(peer) = 1.0 - (0.3 × timeout_rate + 0.5 × corruption_rate + 0.2 × speed_variance)
```

**Scheduler Integration**:
- Prefer high-reputation peers for critical chunks.
- Deprioritize low-reputation peers (only use if no alternatives).
- Never globally share reputation scores (privacy-preserving).

**Implementation**:
- Extend `Peers` table with `local_reputation REAL`, `timeout_count`, `corruption_count`, `total_chunks`.
- Update reputation after each chunk validation.
- `MultiSourceDownloadScheduler` uses reputation as a cost factor.

#### 8. Mesh-Level "Fairness Governor"

**Concept**: Hard caps to prevent slskdn from becoming a selfish leech, ensuring it strengthens Soulseek's ecosystem.

**Enforced Quotas**:
- `max_overlay_upload_ratio`: Max overlay upload relative to Soulseek upload (e.g., 2:1).
- `min_soulseek_contribution`: Must upload at least X KB via Soulseek for every Y KB downloaded via overlay.
- `overlay_bandwidth_cap`: Max overlay bandwidth per day (e.g., 50 GB/day).

**Dashboard UI**:
```
Contribution Report (Last 7 Days)

Uploaded:
├─ Soulseek: 42.3 GB
└─ Overlay Mesh: 68.7 GB
Total: 111.0 GB

Downloaded:
├─ Soulseek: 18.5 GB
└─ Overlay Mesh: 12.1 GB
Total: 30.6 GB

Ratio: 3.6:1 (you've contributed 3.6× more than you downloaded)
Status: ✓ Healthy contributor
```

**Enforcement**:
- If quotas exceeded: temporarily throttle overlay downloads, prioritize Soulseek uploads.
- Optional "Generous Mode": user opts in to higher contribution ratios.

**Implementation**:
- New `ContributionTrackingService` to monitor upload/download stats.
- Persist stats in `HashDbState` table.
- `FairnessGovernor` service enforces caps by throttling overlay connections.

---

### Operational & UX Enhancements

#### 9. Download Job Manifests (.yaml recipes)

**Concept**: Serialize every multi-swarm job as a portable YAML manifest for export/import/resume.

**Manifest Format**:
```yaml
job_id: 0f4de638-56f3-4a0f-b8fa-64f85c6b6a8f
type: mb_release
mb_release_id: c0d0c0a4-4a26-4d74-9c02-67c9321b3b22
title: Loveless
artist: My Bloody Valentine
tracks:
  - position: 1
    mb_recording_id: abc-123-def
    title: Only Shallow
    duration_ms: 282000
  - position: 2
    mb_recording_id: def-456-ghi
    title: Loomer
    duration_ms: 178000
constraints:
  preferred_codecs: [FLAC]
  max_lossy_tracks_per_album: 0
  prefer_canonical_variants: true
  use_overlay: true
  overlay_bandwidth_kbps: 3000
status:
  started_at: 2025-12-09T15:30:00Z
  completed_tracks: [1]
  in_progress_tracks: [2]
  pending_tracks: [3, 4, 5, 6, 7, 8, 9, 10, 11]
```

**Use Cases**:
- **Export/Import**: Share job recipes between machines or users.
- **Provenance**: Commit manifests to Git alongside library for auditable download history.
- **Resume**: If database corrupted, reimport manifests to resume jobs.
- **Batch Processing**: Generate manifests programmatically (e.g., from MB collection API).

**Implementation**:
- `JobManifestService` to serialize/deserialize jobs.
- New API endpoints: `POST /api/v0/jobs/import`, `GET /api/v0/jobs/{id}/export`.
- UI: "Export Job" and "Import Jobs" buttons in downloads page.

#### 10. "Session Trace" for Debugging Swarm Behavior

**Concept**: Structured per-job trace logging for dev/power-user debugging and transparency.

**Trace Data**:
- Which peers contributed which byte ranges.
- Chunk assignment history (peer X assigned chunk Y at time Z).
- Peer performance over time (RTT, throughput, errors).
- Scheduler decisions (why peer A chosen over peer B).
- Rescue mode triggers and mesh failover events.

**Compact UI View**:
```
Job: Loveless - Track 7 (All Blues)

Sources:
├─ Soulseek peer "user123": 60% (chunks 0-23, 40-59)
│  └─ Avg speed: 450 KB/s, RTT: 120ms, 0 errors
├─ Overlay peer "abc-def-ghi": 30% (chunks 24-39)
│  └─ Avg speed: 1.2 MB/s, RTT: 45ms, 0 errors
└─ Overlay peer "ghi-jkl-mno": 10% (chunks 60-69, rescue mode)
   └─ Avg speed: 180 KB/s, RTT: 200ms, 2 retries

Timeline:
00:00 - Started, 3 sources discovered
00:12 - Soulseek peer stalled, rescue mode triggered
00:18 - Overlay peer C added, chunks 60-69 reassigned
00:24 - Complete
```

**Export Format**: JSON for programmatic analysis.

**Implementation**:
- `DownloadTraceService` to log events per job.
- Store traces in `JobTraces` table (or export to JSON file).
- New UI component: "Job Trace Viewer" (collapsible detail panel).

---

### "Definitely Cool, Maybe Later" Tier

#### 11. "Warm Cache" Overlay Nodes

**Concept**: Opt-in mode where nodes with high disk/bandwidth prefetch and cache popular MBIDs seen in mesh jobs.

**Cache Node Behavior**:
1. Monitor mesh `AlbumCompletionJob` messages for popular MBIDs.
2. When MBID seen by N peers (e.g., N > 10):
   - Fetch from Soulseek (once).
   - Verify via fingerprints.
   - Announce to DHT with `cache: true` capability.
3. Serve chunks to other slskdn users via TLS overlay (fast, no Soulseek slots).

**Cache Management**:
- **Eviction**: LRU policy, configurable quota (e.g., 50 GB).
- **Invalidation**: If fingerprint mismatch detected, purge and re-fetch.
- **TTL**: 90 days for unpopular, infinite for manually pinned.

**Fan-Out Amplification**:
```
Scenario: 100 users want "Kind of Blue" (MB:abc-123)

Without cache:
- Original Soulseek uploader: 100 uploads × 500MB = 50GB
- Slow (slots, queues, single uploader)

With 3 cache nodes:
- Caches fetch once from Soulseek: 3 × 500MB = 1.5GB
- Caches serve 100 users via overlay: Fast TLS, no slots
- Original uploader: Only 1.5GB total
- Users: 10-50× faster downloads
```

**Fairness**: Cache nodes still respect contribution quotas (counted as overlay uploads).

**Implementation**:
- New `CacheService` to manage cached MBIDs.
- Extend DHT announce to include `cache: true` flag.
- UI: "Cache Node Mode" settings panel with quota config.

#### 12. Playback-Aware Swarming (for Local Streaming)

**Concept**: If slskdn ever adds a local music player, optimize swarming for streaming playback.

**Streaming Strategy**:
- User hits Play on a track.
- Client needs sliding window of chunks around playback head (e.g., next 30 seconds).
- Swarm scheduler:
  - **Aggressively fetch**: Chunks in playback window from lowest-latency peers.
  - **Opportunistically fetch**: Rest of file in background from any available peers.

**Buffering Logic**:
- Start playback once first 5-10 seconds buffered.
- Maintain 30-second lead buffer.
- If buffer drops below 10 seconds: raise priority of next chunks.

**Result**: "Progressive streaming from Soulseek + mesh" with playback guarantees.

**Implementation**:
- Extend `MultiSourceDownloadJob` with `playback_mode: true` flag.
- New `PlaybackScheduler` to prioritize chunks by playback position.
- Integrate with hypothetical media player component.

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

