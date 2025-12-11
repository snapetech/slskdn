namespace slskd.Audio
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using slskd.HashDb;

    public class CanonicalStatsService : ICanonicalStatsService
    {
        private readonly IHashDbService hashDb;
        private readonly ILogger<CanonicalStatsService> log;

        public CanonicalStatsService(IHashDbService hashDb, ILogger<CanonicalStatsService> log)
        {
            this.hashDb = hashDb;
            this.log = log;
        }

        public async Task<CanonicalStats> AggregateStatsAsync(string recordingId, string codecProfileKey, CancellationToken ct = default)
        {
            var variants = await hashDb.GetVariantsByRecordingAndProfileAsync(recordingId, codecProfileKey, ct).ConfigureAwait(false);
            if (variants == null || variants.Count == 0)
            {
                return null;
            }

            // Deduplicate identical streams within the profile using codec-specific hashes
            var distinctVariants = DeduplicateStreams(variants);

            var stats = new CanonicalStats
            {
                Id = $"{recordingId}:{codecProfileKey}",
                MusicBrainzRecordingId = recordingId,
                CodecProfileKey = codecProfileKey,
                VariantCount = distinctVariants.Count,
                TotalSeenCount = distinctVariants.Sum(v => v.SeenCount <= 0 ? 1 : v.SeenCount),
                AvgQualityScore = distinctVariants.Average(v => v.QualityScore),
                MaxQualityScore = distinctVariants.Max(v => v.QualityScore),
                PercentTranscodeSuspect = (distinctVariants.Count(v => v.TranscodeSuspect) / (double)distinctVariants.Count) * 100.0,
                LastUpdated = DateTimeOffset.UtcNow,
            };

            stats.CodecDistribution = distinctVariants.GroupBy(v => v.Codec ?? "unknown")
                .ToDictionary(g => g.Key, g => g.Count());
            stats.BitrateDistribution = distinctVariants.GroupBy(v => RoundToNearestBitrate(v.BitrateKbps))
                .ToDictionary(g => g.Key, g => g.Count());
            stats.SampleRateDistribution = distinctVariants.GroupBy(v => v.SampleRateHz)
                .ToDictionary(g => g.Key, g => g.Count());

            var bestVariant = distinctVariants
                .OrderByDescending(v => v.QualityScore)
                .ThenByDescending(v => v.SeenCount)
                .First();

            stats.BestVariantId = bestVariant.VariantId ?? bestVariant.FlacKey;
            stats.CanonicalityScore = ComputeCanonicalityScore(bestVariant, stats);

            await hashDb.UpsertCanonicalStatsAsync(stats, ct).ConfigureAwait(false);
            return stats;
        }

        public async Task<List<AudioVariant>> GetCanonicalVariantCandidatesAsync(string recordingId, CancellationToken ct = default)
        {
            var variants = await hashDb.GetVariantsByRecordingAsync(recordingId, ct).ConfigureAwait(false);
            if (variants == null || variants.Count == 0)
            {
                return new List<AudioVariant>();
            }

            // Deduplicate across codecs using stream hash or audio sketch + duration bucket
            var deduped = DeduplicateStreams(variants, crossCodec: true);

            // Group by codec profile
            var statsByProfile = new Dictionary<string, CanonicalStats>();
            foreach (var v in deduped)
            {
                var profileKey = CodecProfile.FromVariant(v).ToKey();
                if (!statsByProfile.ContainsKey(profileKey))
                {
                    var existing = await hashDb.GetCanonicalStatsAsync(recordingId, profileKey, ct).ConfigureAwait(false);
                    statsByProfile[profileKey] = existing ?? await AggregateStatsAsync(recordingId, profileKey, ct).ConfigureAwait(false);
                }
            }

            return deduped
                .OrderByDescending(v => IsLossless(v.Codec))
                .ThenByDescending(v =>
                {
                    var key = CodecProfile.FromVariant(v).ToKey();
                    return statsByProfile.GetValueOrDefault(key)?.CanonicalityScore ?? 0.0;
                })
                .ThenByDescending(v => v.QualityScore)
                .ThenByDescending(v => v.SeenCount)
                .ToList();
        }

        public async Task RecomputeAllStatsAsync(CancellationToken ct = default)
        {
            var recordingIds = await hashDb.GetRecordingIdsWithVariantsAsync(ct).ConfigureAwait(false);
            foreach (var recId in recordingIds)
            {
                var profileKeys = await hashDb.GetCodecProfilesForRecordingAsync(recId, ct).ConfigureAwait(false);
                foreach (var profile in profileKeys)
                {
                    await AggregateStatsAsync(recId, profile, ct).ConfigureAwait(false);
                }
            }
        }

        private static int RoundToNearestBitrate(int bitrate)
        {
            if (bitrate <= 0) return 0;
            // round to nearest 32 kbps bucket
            return (int)(Math.Round(bitrate / 32.0) * 32);
        }

        private static List<AudioVariant> DeduplicateStreams(List<AudioVariant> variants, bool crossCodec = false)
        {
            return variants
                .GroupBy(v => BuildDedupKey(v, crossCodec))
                .Select(g => g
                    .OrderByDescending(v => v.QualityScore)
                    .ThenByDescending(v => v.SeenCount)
                    .First())
                .ToList();
        }

        private static string BuildDedupKey(AudioVariant v, bool crossCodec)
        {
            var streamHash = v.Codec switch
            {
                "FLAC" => v.FlacStreamInfoHash42 ?? v.FlacPcmMd5 ?? v.FileSha256,
                "MP3" => v.Mp3StreamHash ?? v.FileSha256,
                "Opus" => v.OpusStreamHash ?? v.FileSha256,
                "AAC" => v.AacStreamHash ?? v.FileSha256,
                _ => v.FileSha256,
            };

            var sketch = string.IsNullOrWhiteSpace(v.AudioSketchHash) ? "nosketch" : v.AudioSketchHash;
            var durationBucket = RoundDuration(v.DurationMs);
            var codecPart = crossCodec ? string.Empty : (v.Codec ?? "unknown");
            return $"{codecPart}:{streamHash}:{sketch}:{durationBucket}";
        }

        private static int RoundDuration(int durationMs)
        {
            if (durationMs <= 0)
            {
                return 0;
            }

            const int bucketMs = 1500;
            return (int)(Math.Round(durationMs / (double)bucketMs) * bucketMs);
        }

        private static bool IsLossless(string codec)
        {
            return codec switch
            {
                "FLAC" => true,
                "ALAC" => true,
                "WAV" => true,
                "AIFF" => true,
                _ => false,
            };
        }

        private static double ComputeCanonicalityScore(AudioVariant variant, CanonicalStats stats)
        {
            double score = 0.0;

            // Factor 1: Quality score (0.4 weight)
            score += 0.4 * variant.QualityScore;

            // Factor 2: Prevalence (0.3 weight)
            double prevalence = variant.SeenCount / (double)Math.Max(1, stats.TotalSeenCount);
            score += 0.3 * prevalence;

            // Factor 3: Not transcode suspect (0.2 weight)
            score += variant.TranscodeSuspect ? 0.0 : 0.2;

            // Factor 4: Consensus (0.1 weight) - fewer competing variants increases consensus
            int similarQualityCount = stats.VariantCount;
            double consensus = 1.0 / Math.Log(similarQualityCount + 1);
            score += 0.1 * consensus;

            return Math.Clamp(score, 0.0, 1.0);
        }
    }
}
