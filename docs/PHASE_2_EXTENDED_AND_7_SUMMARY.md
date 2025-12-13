# Phase 2-Extended & Phase 7: Critical Additions Summary

> **Date**: December 10, 2025  
> **Status**: Planning Complete, Ready for Implementation  
> **Impact**: Essential for production-grade audio quality analysis and comprehensive testing

---

## Overview

This document summarizes two critical additions to the slskdn roadmap:

1. **Phase 2-Extended**: Codec-specific fingerprinting & quality heuristics
2. **Phase 7**: Testing strategy with Soulfind & mesh simulator

These phases extend the original planning with essential features identified during implementation of Phase 2A.

---

## Phase 2-Extended: Advanced AudioVariant Fingerprinting

### Why This Matters

The basic `AudioVariant` model (T-400) provides general quality scoring, but real-world music collections require **codec-specific intelligence**:

- FLAC files that are actually lossy-sourced transcodes
- MP3s re-encoded from lower bitrates
- Opus/AAC variants with mismatched container/bitstream properties
- Cross-codec deduplication (same recording in multiple formats)

### Key Innovations

#### 1. FLAC 42-Byte Streaminfo Hash

```
Hash = xxHash64(fLaC magic + STREAMINFO header + STREAMINFO body)
      = xxHash64(first 42 bytes of FLAC file)
```

**Purpose**: Lightning-fast content identity that ignores tags/artwork.

**Benefit**: Deduplicate identical FLACs even when tags differ.

#### 2. Codec-Specific Stream Hashes

Each codec gets a **tag-stripped stream hash**:

- **MP3**: Hash over ID3-stripped frame sequence
- **Opus**: Hash over Ogg Opus ID header + first N audio pages
- **AAC**: Hash over raw AAC frames (ADTS) or mdat payload (MP4)

**Benefit**: Identify identical audio streams regardless of container metadata.

#### 3. Audio Sketch Hash (PCM-Window Hash)

```
1. Decode 2-3 short PCM windows (start, mid, end)
2. Downsample to mono 4 kHz
3. Hash resulting PCM with xxHash64
```

**Purpose**: Codec-agnostic content fingerprint for cross-format matching.

**Benefit**: Link FLAC/MP3/Opus/AAC variants of the same recording.

#### 4. Spectral Analysis for Transcode Detection

For each codec, extract spectral features:

- `effective_bandwidth_hz`: Actual upper frequency with energy
- `spectral_flatness_score`: How "alive" the high-frequency region is
- `hf_energy_ratio`: High-frequency energy vs. total energy

**FLAC transcode detection**:
- Tight lowpass at ~16 kHz despite 44.1/48 kHz sample rate
- Spectral shape matches typical MP3/AAC profile
- → Mark as `transcode_suspect = true`, cap quality at 0.6

**MP3 transcode detection**:
- CBR 320 kbps but `effective_bandwidth_hz < 16 kHz`
- Matches lower-bitrate variant via `audio_sketch_hash`
- → Mark as suspect, force quality ≤ 0.5

#### 5. Encoder Detection & Reputation

Extract encoder metadata:

- **MP3**: LAME version/preset, Xing header
- **Opus**: Application mode, bandwidth mode
- **AAC**: Profile (LC/HE/HEv2), SBR/PS flags

**Quality scoring adjustments**:
- Known good encoders (LAME recent): bonus
- Unknown/old encoders: penalty
- Mismatched container/bitstream: suspect flag

### Task Breakdown (T-420 to T-430)

| Task | Description | Priority |
|------|-------------|----------|
| T-420 | Extend AudioVariant model with codec-specific fields | P1 |
| T-421 | Implement FLAC analyzer | P1 |
| T-422 | Implement MP3 analyzer | P1 |
| T-423 | Implement Opus analyzer | P1 |
| T-424 | Implement AAC analyzer | P1 |
| T-425 | Implement audio_sketch_hash (PCM-window hash) | P1 |
| T-426 | Implement cross-codec deduplication logic | P1 |
| T-427 | Implement analyzer version migration | P2 |
| T-428 | Update CanonicalStatsService with codec-specific logic | P1 |
| T-429 | Add codec-specific stats to Library Health | P1 |
| T-430 | Unit tests for codec analyzers | P1 |

### Implementation Architecture

```
AudioAnalyzerService (coordinator)
├── FLACAnalyzer
│   ├── ExtractStreamInfo() → 42-byte hash, PCM MD5
│   ├── ComputeQualityScore() → 0.9-1.0 for clean, 0.4-0.6 for suspect
│   └── DetectTranscode() → spectral analysis
├── MP3Analyzer
│   ├── ExtractStreamHash() → tag-stripped hash
│   ├── DetectEncoder() → LAME, GOGO, FhG, etc.
│   ├── AnalyzeSpectrum() → bandwidth, flatness, HF energy
│   └── ComputeQualityScore() → 0.3-0.85 based on bitrate/spectrum
├── OpusAnalyzer
│   └── ... (similar pattern)
└── AACAnalyzer
    └── ... (similar pattern)
```

### Heuristic Versioning

All analyzers stamp results with `analyzer_version` (e.g., `audioqa-1`).

When heuristics improve:
1. Bump version to `audioqa-2`
2. Background job recomputes scores from **stored raw features**
3. No need to re-decode audio files

### Database Schema

```sql
-- Migration version 7
ALTER TABLE HashDb ADD COLUMN flac_streaminfo_hash42 TEXT;
ALTER TABLE HashDb ADD COLUMN flac_pcm_md5 TEXT;
ALTER TABLE HashDb ADD COLUMN mp3_stream_hash TEXT;
ALTER TABLE HashDb ADD COLUMN mp3_encoder TEXT;
ALTER TABLE HashDb ADD COLUMN mp3_encoder_preset TEXT;
ALTER TABLE HashDb ADD COLUMN effective_bandwidth_hz REAL;
ALTER TABLE HashDb ADD COLUMN spectral_flatness_score REAL;
ALTER TABLE HashDb ADD COLUMN hf_energy_ratio REAL;
ALTER TABLE HashDb ADD COLUMN opus_stream_hash TEXT;
ALTER TABLE HashDb ADD COLUMN opus_bandwidth_mode TEXT;
ALTER TABLE HashDb ADD COLUMN aac_stream_hash TEXT;
ALTER TABLE HashDb ADD COLUMN aac_profile TEXT;
ALTER TABLE HashDb ADD COLUMN aac_sbr_present BOOLEAN;
ALTER TABLE HashDb ADD COLUMN audio_sketch_hash TEXT;
ALTER TABLE HashDb ADD COLUMN analyzer_version TEXT;

CREATE INDEX idx_hashdb_flac_streaminfo ON HashDb(flac_streaminfo_hash42);
CREATE INDEX idx_hashdb_mp3_stream ON HashDb(mp3_stream_hash);
CREATE INDEX idx_hashdb_audio_sketch ON HashDb(audio_sketch_hash);
```

### Benefits to Other Phases

- **Collection Doctor (T-403)**: Detect codec mismatches ("FLAC but spectral content looks like MP3")
- **Canonical Selection (T-401)**: Prefer lossless over lossy explicitly, deduplicate identical variants
- **Multi-Swarm (T-402)**: Avoid downloading transcode garbage when canonical variant available
- **Virtual Soulfind Mesh (Phase 6)**: Use stream hashes for DHT shadow index keys

---

## Phase 7: Testing Strategy with Soulfind & Mesh Simulator

### Why This Matters

Complex distributed systems require **layered testing**:

- Unit tests validate logic
- Integration tests validate protocol correctness
- Disaster simulations validate failover behavior
- Mesh-only tests validate independence from Soulseek

Without this infrastructure, we cannot confidently claim:
- Soulseek protocol compliance
- Disaster mode resilience
- Mesh-only operation capability

### Testing Layers

#### L0: Pure Unit Tests (No Network)

- AudioVariant analyzers
- Quality scoring algorithms
- Transcode detection heuristics
- MBID mapping logic

**Run**: Every build, every PR

#### L1: Protocol Contract Tests (Soulfind-Assisted)

- Start local Soulfind instance
- One slskdn client connects
- Exercises: handshake, login, keepalive, search, rooms, browse

**Purpose**: Validate Soulseek protocol encoding/decoding

**Run**: Main branch merges, nightly

#### L2: Multi-Client Integration Tests

- Soulfind + 3 slskdn instances (Alice, Bob, Carol)
- Alice: good FLACs
- Bob: mixed MP3s + suspect transcodes
- Carol: empty library, requests from Alice/Bob

**Scenarios**:
- Search & download via Soulseek
- Capture pipeline → MBID mapping
- Room interactions
- Rescue mode activation (stall Bob's transfer, verify overlay takeover)

**Purpose**: Validate capture pipeline and Soulseek compatibility

**Run**: Main branch merges, nightly

#### L3: Mesh & Disaster Simulations

**Scenario 1: Graceful Degradation**
1. Start Soulfind + 3 slskdn instances
2. Carol starts MB Release download
3. **Kill Soulfind mid-transfer**
4. Verify:
   - Disaster mode activates
   - Job completes via DHT + overlay only
   - No deadlocks or infinite retries

**Scenario 2: Pure Mesh (No Soulfind)**
1. In-process mesh simulator
2. Alice/Bob with fake libraries
3. Carol starts discography job
4. Verify:
   - DHT-only peer discovery
   - Overlay-only transfers
   - Canonical variant preference

**Purpose**: Validate disaster mode and mesh independence

**Run**: Main branch merges, can run on PRs (faster than L2)

### Test Harness Components

#### 1. SoulfindRunner

```csharp
public class SoulfindRunner : IDisposable
{
    public string Host { get; private set; }
    public int Port { get; private set; }
    
    public static SoulfindRunner Start(SoulfindConfig config = null)
    {
        // Find SOULFIND_BIN, start process, detect ephemeral port
    }
    
    public void Shutdown() { /* graceful + forced kill */ }
}
```

#### 2. SlskdnTestClient

```csharp
public class SlskdnTestClient : IDisposable
{
    public string Username { get; }
    public string ConfigDir { get; }
    
    public static SlskdnTestClient Create(string username, string shareDir = null)
    {
        // Isolated config, unique DHT identity
    }
    
    public Task<SearchResults> SearchAsync(string query) { /* ... */ }
    public Task<Transfer> DownloadAsync(string peer, string path) { /* ... */ }
    public Task WaitForCaptureFlush() { /* ... */ }
}
```

#### 3. MeshSimulator

```csharp
public class MeshSimulator
{
    public List<SimulatedNode> Nodes { get; }
    
    public SimulatedNode CreateNode(string id, Dictionary<string, AudioVariant> inventory)
    {
        // In-memory node with fake library
    }
    
    public void ConnectNodes(params SimulatedNode[] nodes)
    {
        // Loopback DHT/overlay
    }
    
    public void SimulateNetworkPartition(SimulatedNode node)
    {
        // Disconnect for disaster testing
    }
}
```

### Test Fixtures

Located in `tests/fixtures/`:

- **audio/**
  - `good-flac-44100-16bit.flac` (100 kB, known MBID)
  - `lossy-sourced-flac.flac` (obvious transcode)
  - `good-mp3-v0.mp3` (LAME V0)
  - `transcoded-mp3-320.mp3` (suspect)
  - `fixtures-metadata.json` (expected scores/flags)

- **musicbrainz/**
  - `release-xyz-789.json` (stubbed MB API response)
  - `recording-abc-123-def.json`

- **soulfind/**
  - Pre-seeded test users (alice, bob, carol)
  - Known passwords, deterministic shares

### CI/CD Integration

```yaml
# .github/workflows/tests.yml

jobs:
  unit-tests:
    runs-on: ubuntu-latest
    steps:
      - run: dotnet test --filter Category=L0

  integration-mesh:
    runs-on: ubuntu-latest
    steps:
      - run: dotnet test --filter Category=L3-Mesh

  integration-soulseek:
    runs-on: ubuntu-latest
    if: github.ref == 'refs/heads/main'
    steps:
      - name: Install Soulfind
        run: # ... download/build SOULFIND_BIN
      - run: dotnet test --filter Category=L1,Category=L2
```

### Task Breakdown (T-900 to T-915)

| Task | Description | Priority |
|------|-------------|----------|
| T-900 | Implement Soulfind test harness | P1 |
| T-901 | Implement slskdn test client harness | P1 |
| T-902 | Create audio test fixtures | P1 |
| T-903 | Create MusicBrainz stub responses | P1 |
| T-904 | Implement L1 protocol contract tests | P1 |
| T-905 | Implement L2 multi-client integration tests | P1 |
| T-906 | Implement mesh simulator | P1 |
| T-907 | Implement L3 disaster mode tests | P1 |
| T-908 | Implement L3 mesh-only tests | P1 |
| T-909 | Add CI test categorization | P1 |
| T-910 | Add test documentation | P2 |
| T-911 | Implement test result visualization | P2 |
| T-912 | Add rescue mode integration tests | P1 |
| T-913 | Add canonical selection integration tests | P1 |
| T-914 | Add library health integration tests | P1 |
| T-915 | Performance benchmarking suite | P2 |

### Soulfind Policy (Dev-Only)

**Allowed**:
- Local test harness for protocol validation
- Reference implementation for understanding Soulseek behavior
- Dev/CI environments only

**Disallowed**:
- Production deployment as shared server
- Runtime dependency for core features
- Any form of "Virtual Soulfind" implementation using actual Soulfind

See `docs/dev/soulfind-integration-notes.md` for full policy.

---

## Impact on Overall Roadmap

### Timeline Adjustments

Original estimate: ~60 weeks for Phases 2-6

Revised estimate:
- Phase 1: ✅ Complete
- Phase 2 + 2-Extended: ~8-10 weeks (was 6-8)
- Phase 3-5: ~18-24 weeks (unchanged)
- Phase 6 + 6X: ~20-26 weeks (unchanged)
- Phase 7: Parallel with features (~4-6 weeks)
- **Total: ~50-66 weeks**

### Task Count

- Original planning: T-300 to T-860 (61 tasks for Phases 2-6X)
- Phase 2-Extended: T-420 to T-430 (11 tasks)
- Phase 7: T-900 to T-915 (16 tasks)
- **New total: 127 tasks**

### Documentation Metrics

- Original: 19 documents, ~50,000 lines
- Added:
  - `docs/phase2-advanced-fingerprinting-design.md` (~4,500 lines)
  - `docs/phase7-testing-strategy-soulfind.md` (~6,500 lines)
- **New total: 21 documents, ~61,000 lines**

### Branch Strategy

- **Phase 1, 2, 2-Extended, 7**: `experimental/brainz`
- **Phase 3-5**: `experimental/brainz` (continuous)
- **Phase 6**: `experimental/virtual-soulfind` (new branch for mesh features)

---

## Key Takeaways

### Phase 2-Extended is Essential

Without codec-specific fingerprinting:
- Cannot reliably detect transcodes
- Cannot deduplicate across formats
- Quality scoring is naive and easily fooled
- Collection Doctor reports false positives

### Phase 7 is Non-Negotiable

Without comprehensive testing:
- Cannot guarantee Soulseek protocol compliance
- Cannot validate disaster mode behavior
- Cannot prove mesh independence
- High risk of production issues

### Implementation Order

1. **Continue Phase 2** (T-404, T-405) - finish library health UI
2. **Implement Phase 2-Extended** (T-420 to T-430) - codec-specific analyzers
3. **Phase 7 in parallel** - build test infrastructure as features land
4. **Phase 3-5** - discovery, reputation, fairness, jobs, Soulbeet
5. **Phase 6** - Virtual Soulfind mesh, disaster mode, compatibility bridge

### These Phases Unlock

- Production-grade audio quality analysis
- Cross-codec deduplication and canonical selection
- Confidence in disaster mode resilience
- Proof of Soulseek protocol compliance
- Foundation for mesh-only operation

---

## Next Steps

1. ✅ Planning complete for Phase 2-Extended and Phase 7
2. ✅ Task breakdown defined (T-420 to T-430, T-900 to T-915)
3. ✅ Design documents written and ready for implementation
4. 🔄 Continue Codex implementation of Phase 2 (T-404 onwards)
5. 🔜 Begin Phase 2-Extended implementation (T-420+)
6. 🔜 Build test infrastructure (Phase 7) in parallel

---

*Document complete. Phase 2-Extended and Phase 7 are production-ready specifications.*

















