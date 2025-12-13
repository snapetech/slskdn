# Phase 2A: Canonical Edition Scoring - Detailed Design

> **Tasks**: T-400 to T-402  
> **Branch**: `experimental/brainz`  
> **Dependencies**: Phase 1 (MusicBrainz/Chromaprint integration)

---

## Overview

Canonical scoring enables slskdn to identify the "best" version of each recording across multiple variants observed in the network. This allows smart download decisions: prefer canonical masters, avoid transcodes, and surface upgrade opportunities in the Library Health UI.

---

## 1. AudioVariant Data Model (T-400)

### 1.1. Core Data Structure

```csharp
namespace slskd.Audio
{
    /// <summary>
    /// Represents a specific encoded variant of a recording.
    /// </summary>
    public class AudioVariant
    {
        // Identity
        public string VariantId { get; set; }  // Local unique ID
        public string MusicBrainzRecordingId { get; set; }
        public string FlacKey { get; set; }  // SHA256 of FLAC audio MD5
        
        // Technical Properties
        public string Codec { get; set; }  // "FLAC", "MP3", "AAC", "ALAC", "Opus", "Vorbis"
        public string Container { get; set; }  // "FLAC", "MP3", "M4A", "OGG", "MKA"
        public int SampleRateHz { get; set; }  // 44100, 48000, 96000, 192000
        public int? BitDepth { get; set; }  // 16, 24 (null for lossy)
        public int Channels { get; set; }  // 1 (mono), 2 (stereo), 6 (5.1)
        public int DurationMs { get; set; }
        public int BitrateKbps { get; set; }  // Average bitrate
        public long FileSizeBytes { get; set; }
        
        // Content Integrity
        public string FileSha256 { get; set; }
        public string AudioFingerprint { get; set; }  // Chromaprint
        
        // Quality Assessment
        public double QualityScore { get; set; }  // 0.0 to 1.0
        public bool TranscodeSuspect { get; set; }
        public string TranscodeReason { get; set; }  // Human-readable
        
        // Audio Analysis (optional, computed on-demand)
        public double? DynamicRangeDR { get; set; }  // DR meter
        public double? LoudnessLUFS { get; set; }  // EBU R128
        public bool? HasClipping { get; set; }
        public string EncoderSignature { get; set; }  // e.g., "LAME 3.100"
        
        // Provenance
        public DateTimeOffset FirstSeenAt { get; set; }
        public DateTimeOffset LastSeenAt { get; set; }
        public int SeenCount { get; set; }  // How many times observed
    }
}
```

### 1.2. Codec Profile Derivation

```csharp
namespace slskd.Audio
{
    /// <summary>
    /// Standardized codec profile for grouping variants.
    /// </summary>
    public class CodecProfile
    {
        public string Codec { get; set; }
        public bool IsLossless { get; set; }
        public int SampleRateHz { get; set; }
        public int? BitDepth { get; set; }
        public int Channels { get; set; }
        
        /// <summary>
        /// Generate a canonical string key for this profile.
        /// </summary>
        public string ToKey()
        {
            if (IsLossless && BitDepth.HasValue)
            {
                return $"{Codec}-{BitDepth}bit-{SampleRateHz}Hz-{Channels}ch";
            }
            return $"{Codec}-lossy-{SampleRateHz}Hz-{Channels}ch";
        }
        
        /// <summary>
        /// Parse audio file and derive codec profile.
        /// </summary>
        public static CodecProfile FromFile(string filePath)
        {
            using var file = TagLib.File.Create(filePath);
            var props = file.Properties;
            
            return new CodecProfile
            {
                Codec = GetCodecName(props),
                IsLossless = IsLosslessCodec(props),
                SampleRateHz = props.AudioSampleRate,
                BitDepth = GetBitDepth(props),
                Channels = props.AudioChannels
            };
        }
    }
}
```

### 1.3. Database Schema Extension

```sql
-- Extend HashDb table with variant metadata
ALTER TABLE HashDb ADD COLUMN variant_id TEXT;
ALTER TABLE HashDb ADD COLUMN codec TEXT;
ALTER TABLE HashDb ADD COLUMN container TEXT;
ALTER TABLE HashDb ADD COLUMN sample_rate_hz INTEGER;
ALTER TABLE HashDb ADD COLUMN bit_depth INTEGER;
ALTER TABLE HashDb ADD COLUMN channels INTEGER;
ALTER TABLE HashDb ADD COLUMN duration_ms INTEGER;
ALTER TABLE HashDb ADD COLUMN bitrate_kbps INTEGER;
ALTER TABLE HashDb ADD COLUMN quality_score REAL DEFAULT 0.0;
ALTER TABLE HashDb ADD COLUMN transcode_suspect BOOLEAN DEFAULT FALSE;
ALTER TABLE HashDb ADD COLUMN transcode_reason TEXT;
ALTER TABLE HashDb ADD COLUMN dynamic_range_dr REAL;
ALTER TABLE HashDb ADD COLUMN loudness_lufs REAL;
ALTER TABLE HashDb ADD COLUMN has_clipping BOOLEAN;
ALTER TABLE HashDb ADD COLUMN encoder_signature TEXT;
ALTER TABLE HashDb ADD COLUMN seen_count INTEGER DEFAULT 1;

-- Index for variant lookups
CREATE INDEX idx_hashdb_variant ON HashDb(variant_id);
CREATE INDEX idx_hashdb_recording_codec ON HashDb(musicbrainz_id, codec, sample_rate_hz);
```

---

## 2. Quality Scoring Algorithm (T-400)

### 2.1. Scoring Components

Quality score is computed as a weighted sum of multiple factors:

```csharp
public class QualityScorer
{
    public double ComputeQualityScore(AudioVariant variant)
    {
        double score = 0.0;
        
        // Component 1: Codec fidelity (0.4 weight)
        score += 0.4 * ComputeCodecScore(variant);
        
        // Component 2: Sample rate plausibility (0.2 weight)
        score += 0.2 * ComputeSampleRateScore(variant);
        
        // Component 3: Bitrate adequacy (0.2 weight)
        score += 0.2 * ComputeBitrateScore(variant);
        
        // Component 4: Audio analysis (0.2 weight, if available)
        score += 0.2 * ComputeAnalysisScore(variant);
        
        return Math.Clamp(score, 0.0, 1.0);
    }
    
    private double ComputeCodecScore(AudioVariant v)
    {
        // Lossless codecs get full score
        if (v.Codec == "FLAC" && v.BitDepth >= 16) return 1.0;
        if (v.Codec == "ALAC" && v.BitDepth >= 16) return 0.98;
        if (v.Codec == "WAV" && v.BitDepth >= 16) return 0.95;
        
        // High-quality lossy
        if (v.Codec == "MP3" && v.BitrateKbps >= 320) return 0.75;
        if (v.Codec == "AAC" && v.BitrateKbps >= 256) return 0.73;
        if (v.Codec == "Opus" && v.BitrateKbps >= 160) return 0.70;
        if (v.Codec == "Vorbis" && v.BitrateKbps >= 256) return 0.68;
        
        // Low-quality lossy
        if (v.BitrateKbps >= 192) return 0.5;
        if (v.BitrateKbps >= 128) return 0.3;
        return 0.1;
    }
    
    private double ComputeSampleRateScore(AudioVariant v)
    {
        // Standard rates get full score
        if (v.SampleRateHz == 44100 || v.SampleRateHz == 48000) return 1.0;
        
        // High-res audio
        if (v.SampleRateHz == 88200 || v.SampleRateHz == 96000) return 0.95;
        if (v.SampleRateHz == 176400 || v.SampleRateHz == 192000) return 0.90;
        
        // Unusual/suspicious rates (possible upsampling)
        if (v.SampleRateHz == 22050 || v.SampleRateHz == 11025) return 0.4;
        
        return 0.5;  // Unknown rate
    }
    
    private double ComputeBitrateScore(AudioVariant v)
    {
        // For lossless, check if bitrate is reasonable for format
        if (v.Codec == "FLAC")
        {
            int expectedMin = EstimateFLACBitrate(v.SampleRateHz, v.BitDepth ?? 16, v.Channels);
            if (v.BitrateKbps < expectedMin * 0.5)
            {
                // Suspiciously low bitrate for lossless
                return 0.3;
            }
            return 1.0;
        }
        
        // For lossy, higher bitrate is better (up to a point)
        if (v.BitrateKbps >= 320) return 1.0;
        if (v.BitrateKbps >= 256) return 0.9;
        if (v.BitrateKbps >= 192) return 0.7;
        if (v.BitrateKbps >= 128) return 0.5;
        return 0.2;
    }
    
    private double ComputeAnalysisScore(AudioVariant v)
    {
        if (!v.DynamicRangeDR.HasValue) return 0.5;  // No data, neutral
        
        double score = 0.5;
        
        // Good dynamic range
        if (v.DynamicRangeDR >= 10) score += 0.3;
        else if (v.DynamicRangeDR >= 7) score += 0.1;
        else score -= 0.2;  // Poor DR suggests over-compression or transcode
        
        // Clipping penalty
        if (v.HasClipping == true) score -= 0.3;
        
        return Math.Clamp(score, 0.0, 1.0);
    }
    
    private int EstimateFLACBitrate(int sampleRate, int bitDepth, int channels)
    {
        // FLAC typically compresses to 50-60% of raw PCM
        int rawBitrate = (sampleRate * bitDepth * channels) / 1000;
        return (int)(rawBitrate * 0.55);
    }
}
```

### 2.2. Transcode Detection Heuristics

```csharp
public class TranscodeDetector
{
    public (bool isSuspect, string reason) DetectTranscode(AudioVariant variant)
    {
        // Rule 1: FLAC with impossibly low bitrate
        if (variant.Codec == "FLAC")
        {
            int minExpected = EstimateMinFLACBitrate(variant);
            if (variant.BitrateKbps < minExpected)
            {
                return (true, $"FLAC bitrate {variant.BitrateKbps}kbps too low for {variant.SampleRateHz}Hz/{variant.BitDepth}bit (expected ≥{minExpected}kbps)");
            }
        }
        
        // Rule 2: Lossless with poor dynamic range
        if (IsLosslessCodec(variant.Codec) && variant.DynamicRangeDR.HasValue)
        {
            if (variant.DynamicRangeDR < 5)
            {
                return (true, $"Lossless file with DR={variant.DynamicRangeDR:F1} suggests lossy source");
            }
        }
        
        // Rule 3: Upsampled audio (48kHz or 96kHz from 44.1kHz source)
        if (variant.SampleRateHz == 48000 || variant.SampleRateHz == 96000)
        {
            // Check encoder signature for clues
            if (variant.EncoderSignature != null && 
                variant.EncoderSignature.Contains("LAME"))  // MP3 encoder in FLAC
            {
                return (true, "Lossy encoder signature in lossless file");
            }
        }
        
        // Rule 4: Spectral analysis (if available)
        if (variant.BitrateKbps > 0)
        {
            double effectiveBandwidth = EstimateSpectralBandwidth(variant);
            double expectedBandwidth = variant.SampleRateHz / 2.0;
            
            if (effectiveBandwidth < expectedBandwidth * 0.6)
            {
                return (true, $"Spectral bandwidth {effectiveBandwidth/1000:F1}kHz suggests {EstimateOriginalLossyBitrate(effectiveBandwidth)}kbps lossy source");
            }
        }
        
        return (false, null);
    }
    
    private double EstimateSpectralBandwidth(AudioVariant variant)
    {
        // Placeholder: would require FFT analysis
        // For now, estimate from bitrate for lossy codecs
        if (variant.Codec == "MP3")
        {
            if (variant.BitrateKbps >= 320) return 20000;
            if (variant.BitrateKbps >= 192) return 18000;
            if (variant.BitrateKbps >= 128) return 16000;
            return 14000;
        }
        return variant.SampleRateHz / 2.0;  // Nyquist frequency
    }
}
```

---

## 3. Canonical Stats Aggregation (T-401)

### 3.1. Data Structure

```csharp
namespace slskd.Audio
{
    /// <summary>
    /// Aggregated statistics for a (Recording ID, Codec Profile) pair.
    /// </summary>
    public class CanonicalStats
    {
        public string MusicBrainzRecordingId { get; set; }
        public string CodecProfileKey { get; set; }
        
        // Aggregated counts
        public int VariantCount { get; set; }
        public int TotalSeenCount { get; set; }  // Sum of all variant seen_counts
        
        // Quality metrics
        public double AvgQualityScore { get; set; }
        public double MaxQualityScore { get; set; }
        public double PercentTranscodeSuspect { get; set; }
        
        // Distributions
        public Dictionary<string, int> CodecDistribution { get; set; }
        public Dictionary<int, int> BitrateDistribution { get; set; }
        public Dictionary<int, int> SampleRateDistribution { get; set; }
        
        // Canonical candidate
        public string BestVariantId { get; set; }  // Variant with highest quality score
        public double CanonicalityScore { get; set; }  // 0.0 to 1.0
        
        public DateTimeOffset LastUpdated { get; set; }
    }
}
```

### 3.2. Database Schema

```sql
CREATE TABLE CanonicalStats (
    id TEXT PRIMARY KEY,
    musicbrainz_recording_id TEXT NOT NULL,
    codec_profile_key TEXT NOT NULL,
    variant_count INTEGER DEFAULT 0,
    total_seen_count INTEGER DEFAULT 0,
    avg_quality_score REAL DEFAULT 0.0,
    max_quality_score REAL DEFAULT 0.0,
    percent_transcode_suspect REAL DEFAULT 0.0,
    codec_distribution TEXT,  -- JSON
    bitrate_distribution TEXT,  -- JSON
    sample_rate_distribution TEXT,  -- JSON
    best_variant_id TEXT,
    canonicality_score REAL DEFAULT 0.0,
    last_updated INTEGER NOT NULL
);

CREATE INDEX idx_canonical_recording ON CanonicalStats(musicbrainz_recording_id);
CREATE INDEX idx_canonical_profile ON CanonicalStats(codec_profile_key);
CREATE INDEX idx_canonical_score ON CanonicalStats(canonicality_score DESC);
```

### 3.3. Aggregation Service

```csharp
namespace slskd.Audio
{
    public interface ICanonicalStatsService
    {
        /// <summary>
        /// Recompute canonical stats for a recording/codec combination.
        /// </summary>
        Task<CanonicalStats> AggregateStatsAsync(string recordingId, string codecProfileKey, CancellationToken ct = default);
        
        /// <summary>
        /// Get canonical variant candidates for a recording, ranked by quality.
        /// </summary>
        Task<List<AudioVariant>> GetCanonicalVariantCandidatesAsync(string recordingId, CancellationToken ct = default);
        
        /// <summary>
        /// Background job: recompute all canonical stats.
        /// </summary>
        Task RecomputeAllStatsAsync(CancellationToken ct = default);
    }
    
    public class CanonicalStatsService : ICanonicalStatsService
    {
        private readonly IHashDbService hashDb;
        private readonly ILogger<CanonicalStatsService> log;
        
        public async Task<CanonicalStats> AggregateStatsAsync(string recordingId, string codecProfileKey, CancellationToken ct)
        {
            // Query all variants for this (recording, codec profile)
            var variants = await hashDb.GetVariantsByRecordingAndProfileAsync(recordingId, codecProfileKey, ct);
            
            if (variants.Count == 0)
            {
                return null;
            }
            
            var stats = new CanonicalStats
            {
                MusicBrainzRecordingId = recordingId,
                CodecProfileKey = codecProfileKey,
                VariantCount = variants.Count,
                TotalSeenCount = variants.Sum(v => v.SeenCount),
                AvgQualityScore = variants.Average(v => v.QualityScore),
                MaxQualityScore = variants.Max(v => v.QualityScore),
                PercentTranscodeSuspect = (variants.Count(v => v.TranscodeSuspect) / (double)variants.Count) * 100.0,
                LastUpdated = DateTimeOffset.UtcNow
            };
            
            // Build distributions
            stats.CodecDistribution = variants.GroupBy(v => v.Codec)
                .ToDictionary(g => g.Key, g => g.Count());
            stats.BitrateDistribution = variants.GroupBy(v => RoundToNearestBitrate(v.BitrateKbps))
                .ToDictionary(g => g.Key, g => g.Count());
            stats.SampleRateDistribution = variants.GroupBy(v => v.SampleRateHz)
                .ToDictionary(g => g.Key, g => g.Count());
            
            // Identify best variant (highest quality, most seen)
            var bestVariant = variants
                .OrderByDescending(v => v.QualityScore)
                .ThenByDescending(v => v.SeenCount)
                .First();
            
            stats.BestVariantId = bestVariant.VariantId;
            stats.CanonicalityScore = ComputeCanonicalityScore(bestVariant, stats);
            
            // Persist stats
            await hashDb.UpsertCanonicalStatsAsync(stats, ct);
            
            return stats;
        }
        
        private double ComputeCanonicalityScore(AudioVariant variant, CanonicalStats stats)
        {
            double score = 0.0;
            
            // Factor 1: Quality score (0.4 weight)
            score += 0.4 * variant.QualityScore;
            
            // Factor 2: Prevalence (0.3 weight)
            double prevalence = variant.SeenCount / (double)Math.Max(1, stats.TotalSeenCount);
            score += 0.3 * prevalence;
            
            // Factor 3: Not transcode suspect (0.2 weight)
            score += variant.TranscodeSuspect ? 0.0 : 0.2;
            
            // Factor 4: Consensus (0.1 weight)
            // If many variants exist with similar quality, reduce score
            int similarQualityCount = stats.VariantCount;  // Simplified
            double consensus = 1.0 / Math.Log(similarQualityCount + 1);
            score += 0.1 * consensus;
            
            return Math.Clamp(score, 0.0, 1.0);
        }
        
        public async Task<List<AudioVariant>> GetCanonicalVariantCandidatesAsync(string recordingId, CancellationToken ct)
        {
            // Get all variants for this recording across all codec profiles
            var allVariants = await hashDb.GetVariantsByRecordingAsync(recordingId, ct);
            
            // Get canonical stats for each codec profile
            var statsByProfile = new Dictionary<string, CanonicalStats>();
            foreach (var variant in allVariants)
            {
                var profileKey = CodecProfile.FromVariant(variant).ToKey();
                if (!statsByProfile.ContainsKey(profileKey))
                {
                    statsByProfile[profileKey] = await AggregateStatsAsync(recordingId, profileKey, ct);
                }
            }
            
            // Return variants sorted by canonicality score
            return allVariants
                .OrderByDescending(v => {
                    var profileKey = CodecProfile.FromVariant(v).ToKey();
                    return statsByProfile.GetValueOrDefault(profileKey)?.CanonicalityScore ?? 0.0;
                })
                .ThenByDescending(v => v.QualityScore)
                .ThenByDescending(v => v.SeenCount)
                .ToList();
        }
    }
}
```

---

## 4. Canonical-Aware Download Selection (T-402)

### 4.1. Integration with Multi-Source Downloads

```csharp
namespace slskd.Transfers.MultiSource
{
    public partial class MultiSourceDownloadService
    {
        private readonly ICanonicalStatsService canonicalStats;
        
        /// <summary>
        /// Enhanced source selection that prefers canonical variants.
        /// </summary>
        private async Task<List<VerifiedSource>> SelectCanonicalSourcesAsync(
            ContentVerificationResult verificationResult,
            CancellationToken ct)
        {
            if (verificationResult.BestSemanticRecordingId == null)
            {
                // No MBID, fall back to hash-based selection
                return verificationResult.BestSources.ToList();
            }
            
            // Get canonical variant candidates for this recording
            var candidates = await canonicalStats.GetCanonicalVariantCandidatesAsync(
                verificationResult.BestSemanticRecordingId,
                ct);
            
            if (candidates.Count == 0)
            {
                // No canonical data yet, use verification result
                return verificationResult.BestSources.ToList();
            }
            
            // Find sources that match top canonical variants
            var bestVariant = candidates.First();
            var canonicalSources = verificationResult.AllSources
                .Where(s => MatchesCanonicalVariant(s, bestVariant))
                .ToList();
            
            if (canonicalSources.Count >= 2)
            {
                log.Information("[CANONICAL] Selected {Count} sources matching canonical variant (quality={Score:F2}, seen={Seen})",
                    canonicalSources.Count, bestVariant.QualityScore, bestVariant.SeenCount);
                return canonicalSources;
            }
            
            // Not enough canonical sources, use semantic best
            log.Information("[CANONICAL] Insufficient canonical sources, using semantic best");
            return verificationResult.BestSemanticSources.ToList();
        }
        
        private bool MatchesCanonicalVariant(VerifiedSource source, AudioVariant canonical)
        {
            // Check if source's technical properties match canonical variant
            if (source.Codec != canonical.Codec) return false;
            if (source.SampleRate != canonical.SampleRateHz) return false;
            if (Math.Abs(source.Bitrate - canonical.BitrateKbps) > 50) return false;  // Allow 50kbps tolerance
            
            return true;
        }
        
        /// <summary>
        /// Check if local library already has a sufficient variant.
        /// </summary>
        private async Task<bool> ShouldSkipDownloadAsync(
            string recordingId,
            AudioVariant proposedVariant,
            CancellationToken ct)
        {
            // Get existing local variants for this recording
            var localVariants = await hashDb.GetLocalVariantsByRecordingAsync(recordingId, ct);
            
            if (localVariants.Count == 0)
            {
                return false;  // No local copy, proceed with download
            }
            
            // Find best local variant
            var bestLocal = localVariants.OrderByDescending(v => v.QualityScore).First();
            
            // Only download if proposed variant is meaningfully better
            double improvement = proposedVariant.QualityScore - bestLocal.QualityScore;
            
            if (improvement > 0.1)  // Require at least 0.1 score improvement
            {
                log.Information("[CANONICAL] Downloading better variant for {RecordingId}: {OldScore:F2} → {NewScore:F2}",
                    recordingId, bestLocal.QualityScore, proposedVariant.QualityScore);
                return false;
            }
            
            log.Information("[CANONICAL] Skipping download, local variant quality={Score:F2} is sufficient",
                bestLocal.QualityScore);
            return true;
        }
    }
}
```

### 4.2. Configuration Options

```yaml
# In slskd.yml or via Options
audio:
  canonical_scoring:
    enabled: true
    
    # Prefer canonical variants by default
    prefer_canonical: true
    
    # Minimum quality score improvement to download a new variant
    min_quality_improvement: 0.1
    
    # Skip download if local variant has quality score >= this threshold
    local_quality_threshold: 0.85
    
    # Automatically replace suspected transcodes
    auto_replace_transcodes: false
    
    # Recompute canonical stats periodically
    stats_recompute_interval_hours: 24
```

---

## 5. Implementation Checklist (T-400 to T-402)

### T-400: Local quality scoring for AudioVariant

- [ ] Define `AudioVariant` model in `src/slskd/Audio/AudioVariant.cs`
- [ ] Define `CodecProfile` model in `src/slskd/Audio/CodecProfile.cs`
- [ ] Implement `QualityScorer` in `src/slskd/Audio/QualityScorer.cs`
- [ ] Implement `TranscodeDetector` in `src/slskd/Audio/TranscodeDetector.cs`
- [ ] Extend HashDb schema with variant columns (migration)
- [ ] Update `HashDbService` to persist variant metadata
- [ ] Add unit tests for scoring algorithms
- [ ] Add integration tests with sample audio files

### T-401: Canonical stats aggregation

- [ ] Define `CanonicalStats` model in `src/slskd/Audio/CanonicalStats.cs`
- [ ] Create `CanonicalStats` database table (migration)
- [ ] Implement `ICanonicalStatsService` interface
- [ ] Implement `CanonicalStatsService` in `src/slskd/Audio/CanonicalStatsService.cs`
- [ ] Register service in `Program.cs` DI
- [ ] Add background job for periodic stats recomputation
- [ ] Add API endpoint: `GET /api/audio/canonical/{recordingId}` for debugging
- [ ] Add unit tests for aggregation logic
- [ ] Add integration tests with mock HashDb data

### T-402: Canonical-aware download selection

- [ ] Extend `MultiSourceDownloadService` with canonical selection
- [ ] Implement `SelectCanonicalSourcesAsync()` method
- [ ] Implement `ShouldSkipDownloadAsync()` method
- [ ] Add configuration options to `Options.cs`
- [ ] Update `ContentVerificationService` to include codec profile metadata
- [ ] Add logging for canonical selection decisions
- [ ] Update album completion UI to show canonical status
- [ ] Add unit tests for source selection logic
- [ ] Add integration tests for download skip logic

---

## 6. Testing Strategy

### Unit Tests

```csharp
[Fact]
public void QualityScorer_FLAC_16bit_44100_Should_Score_1_0()
{
    var variant = new AudioVariant
    {
        Codec = "FLAC",
        BitDepth = 16,
        SampleRateHz = 44100,
        BitrateKbps = 900,
        DynamicRangeDR = 11.2,
        HasClipping = false
    };
    
    var scorer = new QualityScorer();
    var score = scorer.ComputeQualityScore(variant);
    
    Assert.InRange(score, 0.95, 1.0);
}

[Fact]
public void TranscodeDetector_LowBitrateFLAC_Should_DetectTranscode()
{
    var variant = new AudioVariant
    {
        Codec = "FLAC",
        BitDepth = 16,
        SampleRateHz = 44100,
        BitrateKbps = 200,  // Impossibly low for lossless
        DynamicRangeDR = 6.1
    };
    
    var detector = new TranscodeDetector();
    var (isSuspect, reason) = detector.DetectTranscode(variant);
    
    Assert.True(isSuspect);
    Assert.Contains("bitrate", reason, StringComparison.OrdinalIgnoreCase);
}
```

### Integration Tests

```csharp
[Fact]
public async Task CanonicalStatsService_Should_IdentifyBestVariant()
{
    // Arrange: Insert 3 variants of same recording
    var recordingId = "test-recording-123";
    await hashDb.InsertVariantAsync(CreateVariant(recordingId, "FLAC", 16, 44100, 900, 0.95));
    await hashDb.InsertVariantAsync(CreateVariant(recordingId, "FLAC", 16, 44100, 200, 0.40));  // Transcode
    await hashDb.InsertVariantAsync(CreateVariant(recordingId, "MP3", null, 44100, 320, 0.75));
    
    // Act
    var candidates = await canonicalStats.GetCanonicalVariantCandidatesAsync(recordingId);
    
    // Assert
    Assert.Equal(3, candidates.Count);
    Assert.Equal("FLAC", candidates[0].Codec);
    Assert.Equal(900, candidates[0].BitrateKbps);
    Assert.False(candidates[0].TranscodeSuspect);
}
```

---

## 7. Performance Considerations

1. **Lazy Computation**: Compute quality scores on-demand, not on every file operation
2. **Batch Aggregation**: Recompute canonical stats in bulk during off-peak hours
3. **Caching**: Cache `CanonicalStats` in memory for hot recordings
4. **Indexing**: Ensure proper database indexes for variant lookups by recording ID
5. **Background Jobs**: Run stat recomputation as low-priority background task

---

## 8. Future Enhancements

1. **Spectral Analysis**: Integrate FFT-based transcode detection for higher accuracy
2. **Machine Learning**: Train model on known good/bad variants to improve scoring
3. **User Feedback**: Allow users to flag transcodes to improve detection
4. **Mesh Consensus**: Aggregate quality scores from multiple slskdn nodes for better canonical identification
5. **DR Meter Integration**: Automatically compute dynamic range for all FLAC files

---

This design provides a complete roadmap for implementing canonical scoring in slskdn. All types, methods, and schemas are specified in detail for Codex to implement.
















