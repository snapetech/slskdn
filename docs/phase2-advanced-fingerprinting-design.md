# Phase 2-Extended: Advanced AudioVariant Fingerprinting & Quality Heuristics

> **Phase**: 2-Extended (builds on Phase 2A)  
> **Dependencies**: T-400 (AudioVariant model), T-401 (CanonicalStats)  
> **Branch**: `experimental/brainz`  
> **Estimated Duration**: 4-6 weeks  
> **Tasks**: T-420 through T-430

---

## 1. Overview & Purpose

This phase extends the basic `AudioVariant` model (from T-400) with **codec-specific fingerprints** and **sophisticated quality heuristics** for FLAC, MP3, Opus, and AAC.

### Goals

1. Provide stable identities for audio variants independent of tags/metadata.
2. Detect obvious and likely transcodes with codec-specific logic.
3. Rank variants by effective quality in a way that is good enough for:
   - Canonical edition selection.
   - Multi-swarm scheduling.
   - Collection Doctor / repair suggestions.

This is explicitly **heuristic and versioned**; we store **raw features** so we can recompute scores when heuristics change.

---

## 2. Common `AudioVariant` Model Extensions

### 2.1 Core identity & linkage (additions to T-400 model)

- `mb_release_id` (nullable; MB Release ID, string)
- `file_path` (string) - local path for library scans
- `file_hash_sha256` (binary/hex string; whole-file hash)

### 2.2 Fingerprints & sketches (codec-agnostic)

- `acoustid_id` (nullable string) – AcoustID / Chromaprint ID, if resolved
- `chromaprint_fp` (nullable blob) – optional raw fingerprint for local-only use
- `audio_sketch_hash` (nullable; short codec-agnostic PCM sketch hash, e.g. 64–128 bits)
  - Derived by:
    - Decoding 2–3 short PCM windows (start/mid/end).
    - Downsampling heavily (e.g. mono 4 kHz).
    - Hashing resulting PCM.

### 2.3 Quality & transcode judgments (already in T-400)

- `quality_score` (f32, 0.0–1.0)
- `transcode_suspect` (bool)
- `analyzer_version` (string, e.g. `audioqa-1`)

These are produced by codec-specific analyzers plus generic logic. Raw features used to compute them are stored in codec-specific sections (below).

> Important: `quality_score` is **relative** and pragmatic, not audiophile-perfect. Its purpose is to rank candidates, not to be a scientific metric.

---

## 3. FLAC-Specific Fields & Heuristics

FLAC is lossless and has especially useful header metadata. We exploit that.

### 3.1 FLAC-specific fields

#### Database schema additions

```sql
-- Add to HashDb migration (Phase 2-Extended)
ALTER TABLE HashDb ADD COLUMN flac_streaminfo_hash42 TEXT;
ALTER TABLE HashDb ADD COLUMN flac_pcm_md5 TEXT;
ALTER TABLE HashDb ADD COLUMN flac_min_block_size INTEGER;
ALTER TABLE HashDb ADD COLUMN flac_max_block_size INTEGER;
ALTER TABLE HashDb ADD COLUMN flac_min_frame_size INTEGER;
ALTER TABLE HashDb ADD COLUMN flac_max_frame_size INTEGER;
ALTER TABLE HashDb ADD COLUMN flac_total_samples INTEGER;

CREATE INDEX idx_hashdb_flac_streaminfo ON HashDb(flac_streaminfo_hash42);
CREATE INDEX idx_hashdb_flac_pcm ON HashDb(flac_pcm_md5);
```

#### Field descriptions

- `flac_streaminfo_hash42` (nullable, 64–128 bits)
  - Hash (e.g. xxHash64) of the first 42 bytes:
    - `fLaC` magic (4)
    - STREAMINFO header (4)
    - STREAMINFO body (34)
  - Purpose: fast identity of the audio stream ignoring tags/artwork.

- `flac_pcm_md5` (nullable, 16 bytes)
  - The MD5 of decoded PCM stored in STREAMINFO.
  - Strong content equality indicator across FLAC files.

- Streaminfo-derived fields (some may duplicate core fields but are cheap to validate):
  - `flac_min_block_size`
  - `flac_max_block_size`
  - `flac_min_frame_size`
  - `flac_max_frame_size`
  - `flac_total_samples` (u64)

These are primarily for integrity checks and internal sanity verification.

### 3.2 FLAC quality heuristics

Because FLAC is lossless, quality is dominated by:

- Is it truly lossless source material?
- Is the sample rate / bit-depth sane for the recording?
- Is it a transcode from a lossy source saved as FLAC?

#### Baseline quality_score

- Start from:
  - 0.95 for normal "CD-quality" FLAC: `44.1 or 48 kHz`, `16 or 24 bit`.
  - 1.00 for high-res FLAC with evidence of real content above ~20 kHz and non-suspicious provenance.

- Slight penalties:
  - Downsampled weirdness (e.g. 32 kHz when MB metadata or other variants suggest 44.1 kHz): −0.05 to −0.10.
  - Unusual bit depths that don't match MB/other variants (e.g. 20-bit with nothing significant in the extra bits): −0.02 to −0.05.

#### Transcode detection (`transcode_suspect`)

We set `transcode_suspect = true` for FLAC when evidence suggests "lossy-sourced FLAC":

- Spectral analysis on short PCM windows:
  - Detect classic brickwall lowpass patterns:
    - Tight lowpass at ~16 kHz or ~18 kHz with no energy beyond, despite 44.1/48 kHz sample rate.
    - Consistency with typical MP3/AAC encoder profiles.

- If:
  - `flac_pcm_md5` matches the PCM of a known lossy variant (via decode) OR
  - `audio_sketch_hash` matches a lossy file and the spectral shape matches the lossy variant,
  - Then mark as transcode-suspect.

#### Effect on `quality_score`

- If `transcode_suspect == true`:
  - Hard cap: `quality_score <= 0.6`.
  - Typically:
    - Start from hypothetical FLAC base (0.95 / 1.0).
    - Apply penalty e.g. `score *= 0.5` (so 0.95 → 0.47).

- If not suspect:
  - Score stays at high end (0.9–1.0).

---

## 4. MP3-Specific Fields & Heuristics

MP3 is lossy with a more complex container/bitstream situation. We focus on stream identity and spectral behaviour.

### 4.1 MP3-specific fields

#### Database schema additions

```sql
ALTER TABLE HashDb ADD COLUMN mp3_stream_hash TEXT;
ALTER TABLE HashDb ADD COLUMN mp3_encoder TEXT;
ALTER TABLE HashDb ADD COLUMN mp3_encoder_preset TEXT;
ALTER TABLE HashDb ADD COLUMN mp3_frames_analyzed INTEGER;
ALTER TABLE HashDb ADD COLUMN effective_bandwidth_hz REAL;
ALTER TABLE HashDb ADD COLUMN nominal_lowpass_hz REAL;
ALTER TABLE HashDb ADD COLUMN spectral_flatness_score REAL;
ALTER TABLE HashDb ADD COLUMN hf_energy_ratio REAL;

CREATE INDEX idx_hashdb_mp3_stream ON HashDb(mp3_stream_hash);
CREATE INDEX idx_hashdb_encoder ON HashDb(mp3_encoder);
```

#### Field descriptions

- `mp3_stream_hash` (nullable, 64–128 bits)
  - Hash over a **tag-stripped** version of the file:
    - Remove ID3v2 from start and ID3v1/APEv2 from end.
    - Hash a fixed number of frames or the entire frame sequence.
  - Purpose: identify identical encodes regardless of tags.

- `mp3_encoder` (string, e.g. `LAME`, `GOGO`, `FhG`, `Unknown`)

- `mp3_encoder_preset` (string/enum when LAME/Xing present, e.g. `CBR_320`, `V0`, `V2`, etc.)

- `mp3_frames_analyzed` (u32)
  - Number of frames sampled for features.

Spectral/quality features:

- `effective_bandwidth_hz` (f32)
  - Approximate upper frequency with non-trivial energy (from short-time FFT).

- `nominal_lowpass_hz` (nullable f32; from encoder tag if exposed).

- `spectral_flatness_score` / `hf_energy_ratio` (optional floats)
  - Simple stats for how "alive" the HF region is vs noise/aliasing.

### 4.2 MP3 quality_score heuristics

We combine:

- **Nominal bitrate/preset**
- **Effective bandwidth**
- **Encoder reputation**

Pseudo approach:

1. Base score from nominal bitrate/preset (for music content):

   - `CBR_320` or `V0` → ~0.80
   - `V2` (approx 190 kbps) → ~0.70
   - `~160 kbps` → ~0.60
   - `128 kbps` → ~0.50
   - `<128 kbps` → 0.3 or lower

2. Adjust for effective bandwidth:

   - If bitrate suggests high bandwidth (e.g. 320k) but `effective_bandwidth_hz < 16 kHz`:
     - Apply penalty, e.g. `score *= 0.7`.

   - If bitrate is modest but spectrum looks "better than expected":
     - Small bump, e.g. `score *= 1.05` (capped at some max).

3. Adjust for encoder:

   - Known good encoders (LAME recent) → no change or slight bump.
   - Unknown/old encoders → slight penalty (−0.05 to −0.1).

**Final score:** clamped to [0.0, 0.85] to keep lossless/Hi-Res FLAC above.

### 4.3 MP3 transcode_suspect heuristics

Mark `transcode_suspect = true` if any of:

- Bitrate inconsistent with spectrum:
  - e.g. `CBR_320` but `effective_bandwidth_hz` looks like a typical 128k (sharp lowpass around ~16 kHz, little HF detail).

- `audio_sketch_hash` matches a **lower bitrate** MP3/Opus/AAC variant closely:
  - And the lower bitrate file existed first or is more widely seen.

- Encoder tag anomalies:
  - LAME header or other metadata declares one preset, but file metrics match another, suggesting re-encode.

Effect on score:

- Transcode-suspect:
  - Force `quality_score <= 0.5`.
  - Typically reduce by 0.2–0.3 vs non-suspect of same nominal bitrate.

---

## 5. Opus-Specific Fields & Heuristics

Opus is modern and tends to be efficient at lower bitrates.

### 5.1 Opus-specific fields

#### Database schema additions

```sql
ALTER TABLE HashDb ADD COLUMN opus_stream_hash TEXT;
ALTER TABLE HashDb ADD COLUMN opus_nominal_bitrate_kbps INTEGER;
ALTER TABLE HashDb ADD COLUMN opus_application TEXT;
ALTER TABLE HashDb ADD COLUMN opus_bandwidth_mode TEXT;

CREATE INDEX idx_hashdb_opus_stream ON HashDb(opus_stream_hash);
```

#### Field descriptions

- `opus_stream_hash` (nullable, 64–128 bits)
  - Hash of:
    - Ogg Opus ID header page + first N audio data pages.
  - Identity for the audio stream ignoring container-level metadata.

- `opus_nominal_bitrate_kbps` (nullable u32; from container or encoder tags)

- `opus_application` (enum: `VoIP`, `Audio`, `LowDelay`)

- `opus_bandwidth_mode` (enum: `Narrowband`, `Mediumband`, `Wideband`, `Superwideband`, `Fullband`)

Spectral features (reuse from MP3):

- `effective_bandwidth_hz` (f32)
- `hf_energy_ratio` (float) – similar to MP3, but tuned for Opus's typical spectral behavior.

### 5.2 Opus quality_score heuristics

For **music** content (our default assumption unless we know otherwise):

1. Base score from nominal bitrate:

   - `>= 160 kbps` → ~0.80
   - `128–160 kbps` → ~0.75
   - `96–128 kbps` → ~0.70
   - `64–96 kbps` → ~0.60
   - `<64 kbps` → 0.4 or below for music.

2. Adjust for bandwidth mode:

   - If `Fullband` with good `effective_bandwidth_hz` (≥ 18–20 kHz):
     - Slight bump, e.g. +0.05 (capped at 0.85).

   - If mode is `Narrowband`/`Wideband` but MB or other variants suggest fullband music:
     - Penalty (−0.1 or more).

3. Apply small bonuses for:

   - Matching expected spectral shape for Opus at that bitrate.
   - Modern encoder builds (if detectable).

Clamp:

- `quality_score` for Opus typically in [0.4, 0.85].
- Lossless still has headroom above it.

### 5.3 Opus transcode_suspect heuristics

Mark `transcode_suspect = true` if:

- Opus file appears to be re-encoded from:
  - Another lossy variant with similar `audio_sketch_hash` but lower base quality.

- Effective bandwidth is strangely low for the advertised bitrate and bandwidth mode, and spectrum looks like a known MP3/AAC artifact profile.

Effect on score:

- Similar to MP3:
  - Multiply by ~0.6–0.7 and cap at ~0.5.

---

## 6. AAC-Specific Fields & Heuristics

Includes AAC-LC and HE-AAC variants, typically stored in MP4/M4A or raw ADTS.

### 6.1 AAC-specific fields

#### Database schema additions

```sql
ALTER TABLE HashDb ADD COLUMN aac_stream_hash TEXT;
ALTER TABLE HashDb ADD COLUMN aac_profile TEXT;
ALTER TABLE HashDb ADD COLUMN aac_sbr_present BOOLEAN;
ALTER TABLE HashDb ADD COLUMN aac_ps_present BOOLEAN;
ALTER TABLE HashDb ADD COLUMN aac_nominal_bitrate_kbps INTEGER;

CREATE INDEX idx_hashdb_aac_stream ON HashDb(aac_stream_hash);
CREATE INDEX idx_hashdb_aac_profile ON HashDb(aac_profile);
```

#### Field descriptions

- `aac_stream_hash` (nullable, 64–128 bits)
  - Hash over:
    - Raw AAC frames (for ADTS) or `mdat` audio payload segments (for MP4), ignoring metadata atoms.

- `aac_profile` (enum: `LC`, `HE`, `HEv2`, `LD`, `ELD`, etc.)

- `aac_sbr_present` (bool)

- `aac_ps_present` (bool) – parametric stereo

- `aac_nominal_bitrate_kbps` (nullable u32)

Spectral features (same as others):

- `effective_bandwidth_hz` (f32)
- `hf_energy_ratio` (float)

### 6.2 AAC quality_score heuristics

For music:

1. Base score from profile + nominal bitrate:

   - **AAC-LC:**
     - `>= 256 kbps` → ~0.80
     - `192–256 kbps` → ~0.75
     - `128–192 kbps` → ~0.70
     - `96–128 kbps` → ~0.60
     - `<96 kbps` → 0.4 or below.

   - **HE-AAC / HE-AACv2:**
     - `64–96 kbps` (when used appropriately) → ~0.65
     - `48–64 kbps` → ~0.55
     - `<48 kbps` → 0.4 or below.

2. Adjust for effective bandwidth:

   - LC at 256 kbps should show robust HF content:
     - If `effective_bandwidth_hz < 16 kHz`, apply penalty (`* 0.7`).

   - HE profiles at lower bitrates:
     - Expect a different spectral shape; penalize if it looks like a downsampled MP3 transcode.

Clamp scores around [0.4, 0.85], similar to Opus.

### 6.3 AAC transcode_suspect heuristics

Set `transcode_suspect = true` if:

- Evidence suggests:
  - Re-encode of MP3 or another AAC:
    - `audio_sketch_hash` matches lower-quality file.
    - Spectral content matches typical MP3 lowpass while container says AAC-LC at high bitrate.

- Container bitrate/profile mismatch:
  - Declared 256 kbps LC but actual payload characteristics + spectrum look like 96 kbps HE.

Apply penalties and cap quality similar to MP3/Opus.

---

## 7. Cross-Codec Features & Canonical Decisions

### 7.1 Cross-codec grouping & dedupe

The following fields are used to correlate variants across codecs and containers:

- `mb_recording_id`, `mb_release_id`
- `acoustid_id`
- `audio_sketch_hash` (PCM-window hash)
- Codec-specific stream hashes:
  - `flac_streaminfo_hash42`
  - `mp3_stream_hash`
  - `opus_stream_hash`
  - `aac_stream_hash`

Rules of thumb:

- If two variants share:
  - The same MB Recording ID and
  - `audio_sketch_hash` and
  - Similar duration within a small tolerance,
  then they are considered *different encodes/containers of the same logical content*.

### 7.2 Canonical selection per recording

Given a set of `AudioVariant`s for a single MB Recording:

- Prefer in this order (simplified):

  1. Non-transcode, lossless variants (FLAC/ALAC/WAV), highest `quality_score`.
  2. Non-transcode, high-quality Opus/AAC/MP3 (score-driven).
  3. Transcode-suspect variants only if nothing better exists.

- Use `quality_score` to differentiate within categories.

- Use codec-specific stream hashes to deduplicate identical copies.

---

## 8. Implementation Notes

### 8.1 Analyzer pipeline

For each new or modified file:

1. Parse container and codec.
2. Compute:
   - Common fields (duration, sample rate, bitrate, etc.).
   - Codec-specific fingerprints and features.
   - Acoustic fingerprint (if enabled).
3. Pass feature bundle to codec-specific analyzer:
   - Returns:
     - `quality_score`
     - `transcode_suspect`
4. Store:
   - Raw features (for recomputation later).
   - Final `quality_score`, `transcode_suspect`.
   - `analyzer_version`.

### 8.2 Heuristic versioning & recomputation

- When heuristics change (new version of analyzers):
  - Bump `analyzer_version`.
  - Background job:
    - Scans existing `AudioVariant` records with older version.
    - Recomputes scores/flags using stored raw features.

- This avoids re-decoding audio when possible.

### 8.3 Service architecture

```
AudioAnalyzerService (coordinator)
├── FLACAnalyzer
│   ├── ExtractStreamInfo()
│   ├── ComputeQualityScore()
│   └── DetectTranscode()
├── MP3Analyzer
│   ├── ExtractStreamHash()
│   ├── DetectEncoder()
│   ├── AnalyzeSpectrum()
│   └── ComputeQualityScore()
├── OpusAnalyzer
│   └── ...
└── AACAnalyzer
    └── ...
```

Each analyzer is registered in DI and invoked by `AudioAnalyzerService` based on codec detection.

---

## 9. Task Breakdown

### T-420: Extend AudioVariant model with codec-specific fields

**Deliverables:**

- Add all codec-specific fields to `AudioVariant.cs`
- HashDb migration (version 7) for new columns + indexes
- Update `HashDbService` to persist all new fields

**Files:**

- `src/slskd/Audio/AudioVariant.cs`
- `src/slskd/HashDb/Migrations/HashDbMigrations.cs`
- `src/slskd/HashDb/HashDbService.cs`

### T-421: Implement FLAC analyzer

**Deliverables:**

- `FLACAnalyzer` class with:
  - STREAMINFO parser (extract 42-byte hash, PCM MD5, block/frame sizes)
  - Quality scoring heuristics
  - Transcode detection via spectral analysis
- Integration with `AudioAnalyzerService`

**Files:**

- `src/slskd/Audio/Analyzers/FLACAnalyzer.cs`
- `src/slskd/Audio/AudioAnalyzerService.cs`

### T-422: Implement MP3 analyzer

**Deliverables:**

- `MP3Analyzer` class with:
  - Tag-stripped stream hash
  - Encoder detection (LAME, etc.)
  - Spectral feature extraction (bandwidth, flatness, HF energy)
  - Quality scoring with encoder/bitrate/spectrum logic
  - Transcode detection

**Files:**

- `src/slskd/Audio/Analyzers/MP3Analyzer.cs`

### T-423: Implement Opus analyzer

**Deliverables:**

- `OpusAnalyzer` class with:
  - Ogg Opus stream hash
  - Bitrate/application/bandwidth mode extraction
  - Quality scoring tuned for Opus
  - Transcode detection

**Files:**

- `src/slskd/Audio/Analyzers/OpusAnalyzer.cs`

### T-424: Implement AAC analyzer

**Deliverables:**

- `AACAnalyzer` class with:
  - AAC stream hash (MP4/ADTS)
  - Profile detection (LC/HE/HEv2), SBR/PS flags
  - Quality scoring for AAC variants
  - Transcode detection

**Files:**

- `src/slskd/Audio/Analyzers/AACAnalyzer.cs`

### T-425: Implement audio_sketch_hash (PCM-window hash)

**Deliverables:**

- Service to decode short PCM windows from arbitrary audio files
- Downsample to mono 4 kHz
- Hash resulting PCM (xxHash64 or similar)
- Store in `audio_sketch_hash` field

**Files:**

- `src/slskd/Audio/AudioSketchService.cs`

**Dependencies:**

- Requires ffmpeg for decoding (already used in Phase 1B)

### T-426: Implement cross-codec deduplication logic

**Deliverables:**

- Service to query variants by:
  - MB Recording ID + audio_sketch_hash
  - Codec-specific stream hashes
- Deduplicate identical variants across codec boundaries
- API endpoint for debugging: `GET /api/audio/variants/dedupe/{recordingId}`

**Files:**

- `src/slskd/Audio/VariantDeduplicationService.cs`
- `src/slskd/Audio/API/VariantDedupeController.cs`

### T-427: Implement analyzer version migration

**Deliverables:**

- Background job to detect stale `analyzer_version`
- Recompute quality scores from stored raw features
- CLI command: `slskdn audio reanalyze [--force]`

**Files:**

- `src/slskd/Audio/Jobs/ReanalyzeJob.cs`

### T-428: Update CanonicalStatsService with codec-specific logic

**Deliverables:**

- Modify `CanonicalStatsService` to:
  - Use codec-specific stream hashes for deduplication
  - Prefer lossless over lossy explicitly
  - Use audio_sketch_hash for cross-codec grouping

**Files:**

- `src/slskd/Audio/CanonicalStatsService.cs` (update existing from T-401)

### T-429: Add codec-specific stats to Library Health

**Deliverables:**

- Extend `LibraryHealthService` to:
  - Detect codec mismatches (e.g., "FLAC but spectral content looks like MP3")
  - Flag transcodes using new analyzer results
  - Suggest replacements based on codec-specific canonical variants

**Files:**

- `src/slskd/LibraryHealth/LibraryHealthService.cs` (update existing from T-403)

### T-430: Unit tests for codec analyzers

**Deliverables:**

- Test fixtures: sample FLAC, MP3, Opus, AAC files (small, deterministic)
- Unit tests for each analyzer:
  - Quality score computation
  - Transcode detection
  - Stream hash stability
- Integration tests for cross-codec deduplication

**Files:**

- `tests/slskd.Tests.Unit/Audio/FLACAnalyzerTests.cs`
- `tests/slskd.Tests.Unit/Audio/MP3AnalyzerTests.cs`
- `tests/slskd.Tests.Unit/Audio/OpusAnalyzerTests.cs`
- `tests/slskd.Tests.Unit/Audio/AACAnalyzerTests.cs`
- `tests/slskd.Tests.Unit/Audio/CrossCodecDeduplicationTests.cs`

---

## 10. Summary

- All formats share a **common AudioVariant core** and codec-agnostic fingerprints (`audio_sketch_hash`, AcoustID).

- Each codec adds:
  - A **stream-identity hash** that ignores tags.
  - Optional encoder/bitstream details.
  - Spectral and encoder-derived features.

- `quality_score` and `transcode_suspect` are computed via codec-specific analyzers, but normalized so:
  - Lossless always has headroom over lossy.
  - High-quality modern lossy (Opus/AAC/MP3 320) is clearly distinguishable from low-bitrate or transcode garbage.

- The multi-swarm engine and Collection Doctor consume just:
  - MBIDs,
  - `quality_score`,
  - `transcode_suspect`,
  - and identity hashes,
  without knowing the heuristic details.

This keeps the **architecture codec-agnostic**, while allowing **codec-specific magic** (like FLAC's 42-byte trick and MP3/Opus transcode detection) to be added and refined over time.

---

*Phase 2-Extended specification complete. Ready for implementation.*

















