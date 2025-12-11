namespace slskd.Audio
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using slskd.HashDb;

    public interface IDedupeService
    {
        Task<DedupeResult> GetDedupeAsync(string recordingId, CancellationToken ct = default);
    }

    public class DedupeService : IDedupeService
    {
        private const int DurationToleranceMs = 1500;

        private readonly IHashDbService hashDb;
        private readonly ILogger<DedupeService> log;

        public DedupeService(IHashDbService hashDb, ILogger<DedupeService> log)
        {
            this.hashDb = hashDb;
            this.log = log;
        }

        public async Task<DedupeResult> GetDedupeAsync(string recordingId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(recordingId))
            {
                throw new ArgumentException("Recording ID is required", nameof(recordingId));
            }

            var variants = await hashDb.GetVariantsByRecordingAsync(recordingId, ct).ConfigureAwait(false);
            var result = new DedupeResult
            {
                RecordingId = recordingId,
                Groups = new List<DedupeGroup>(),
            };

            if (variants == null || variants.Count == 0)
            {
                return result;
            }

            var groups = new Dictionary<string, DedupeGroup>();

            foreach (var v in variants)
            {
                var sketch = string.IsNullOrWhiteSpace(v.AudioSketchHash) ? "none" : v.AudioSketchHash;
                var durationKey = RoundDuration(v.DurationMs);
                var groupKey = $"{sketch}:{durationKey}";

                if (!groups.TryGetValue(groupKey, out var group))
                {
                    group = new DedupeGroup
                    {
                        AudioSketchHash = sketch == "none" ? null : sketch,
                        RepresentativeDurationMs = durationKey,
                        Variants = new List<DedupeVariant>(),
                    };
                    groups[groupKey] = group;
                }

                var streamHash = GetStreamHash(v);
                group.Variants.Add(new DedupeVariant
                {
                    VariantId = v.VariantId ?? v.FlacKey,
                    FlacKey = v.FlacKey,
                    Codec = v.Codec,
                    Container = v.Container,
                    DurationMs = v.DurationMs,
                    BitrateKbps = v.BitrateKbps,
                    QualityScore = v.QualityScore,
                    TranscodeSuspect = v.TranscodeSuspect,
                    StreamHash = streamHash,
                    AudioSketchHash = v.AudioSketchHash,
                });
            }

            foreach (var group in groups.Values)
            {
                var dupSets = group.Variants
                    .Where(v => !string.IsNullOrWhiteSpace(v.StreamHash))
                    .GroupBy(v => v.StreamHash)
                    .Where(g => g.Count() > 1)
                    .Select(g => new DedupeDuplicateSet
                    {
                        StreamHash = g.Key,
                        Variants = g.ToList(),
                    })
                    .ToList();

                group.DuplicateSets = dupSets;
            }

            result.Groups = groups.Values
                .OrderByDescending(g => g.Variants.Count)
                .ThenBy(g => g.AudioSketchHash ?? "zzz")
                .ToList();

            return result;
        }

        private static string GetStreamHash(AudioVariant v)
        {
            return v.Codec switch
            {
                "FLAC" => v.FlacStreamInfoHash42 ?? v.FlacPcmMd5 ?? v.FileSha256,
                "MP3" => v.Mp3StreamHash ?? v.FileSha256,
                "Opus" => v.OpusStreamHash ?? v.FileSha256,
                "AAC" => v.AacStreamHash ?? v.FileSha256,
                _ => v.FileSha256,
            };
        }

        private static int RoundDuration(int durationMs)
        {
            if (durationMs <= 0)
            {
                return 0;
            }

            return (int)(Math.Round(durationMs / (double)DurationToleranceMs) * DurationToleranceMs);
        }
    }

    public class DedupeResult
    {
        public string RecordingId { get; set; }

        public List<DedupeGroup> Groups { get; set; } = new ();
    }

    public class DedupeGroup
    {
        public string AudioSketchHash { get; set; }

        public int RepresentativeDurationMs { get; set; }

        public List<DedupeVariant> Variants { get; set; } = new ();

        public List<DedupeDuplicateSet> DuplicateSets { get; set; } = new ();
    }

    public class DedupeVariant
    {
        public string VariantId { get; set; }

        public string FlacKey { get; set; }

        public string Codec { get; set; }

        public string Container { get; set; }

        public int DurationMs { get; set; }

        public int BitrateKbps { get; set; }

        public double QualityScore { get; set; }

        public bool TranscodeSuspect { get; set; }

        public string StreamHash { get; set; }

        public string AudioSketchHash { get; set; }
    }

    public class DedupeDuplicateSet
    {
        public string StreamHash { get; set; }

        public List<DedupeVariant> Variants { get; set; }
    }
}


