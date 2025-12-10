namespace slskd.Audio
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using slskd.HashDb;

    public interface IAnalyzerMigrationService
    {
        /// <summary>
        ///     Recompute quality/transcode flags for all variants with stale analyzer_version.
        /// </summary>
        Task<int> MigrateAsync(string targetAnalyzerVersion, CancellationToken ct = default);
    }

    /// <summary>
    ///     Migration to bring existing variants up to the current analyzer version without re-decoding audio.
    ///     Uses stored metadata and existing heuristics to recompute quality/transcode.
    /// </summary>
    public class AnalyzerMigrationService : IAnalyzerMigrationService
    {
        private readonly IHashDbService hashDb;
        private readonly ILogger<AnalyzerMigrationService> log;
        private readonly QualityScorer qualityScorer = new();
        private readonly TranscodeDetector transcodeDetector = new();

        public AnalyzerMigrationService(IHashDbService hashDb, ILogger<AnalyzerMigrationService> log)
        {
            this.hashDb = hashDb;
            this.log = log;
        }

        public async Task<int> MigrateAsync(string targetAnalyzerVersion, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(targetAnalyzerVersion))
            {
                throw new ArgumentException("Target analyzer version is required", nameof(targetAnalyzerVersion));
            }

            var updated = 0;
            var recordingIds = await hashDb.GetRecordingIdsWithVariantsAsync(ct).ConfigureAwait(false);

            foreach (var recId in recordingIds)
            {
                var variants = await hashDb.GetVariantsByRecordingAsync(recId, ct).ConfigureAwait(false);
                if (variants == null || variants.Count == 0)
                {
                    continue;
                }

                var stale = variants.Where(v => string.IsNullOrWhiteSpace(v.AnalyzerVersion) || !string.Equals(v.AnalyzerVersion, targetAnalyzerVersion, StringComparison.OrdinalIgnoreCase)).ToList();
                if (stale.Count == 0)
                {
                    continue;
                }

                foreach (var v in stale)
                {
                    v.QualityScore = qualityScorer.ComputeQualityScore(v);
                    var (suspect, reason) = transcodeDetector.DetectTranscode(v);
                    v.TranscodeSuspect = suspect;
                    v.TranscodeReason = reason;
                    v.AnalyzerVersion = targetAnalyzerVersion;

                    await hashDb.UpdateVariantMetadataAsync(v.FlacKey, v, ct).ConfigureAwait(false);
                    updated++;
                }
            }

            log.LogInformation("[AnalyzerMigration] Updated {Count} variants to analyzer_version {Version}", updated, targetAnalyzerVersion);
            return updated;
        }
    }
}
