// <copyright file="NormalizationPipeline.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.VirtualSoulfind.Capture;

using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using slskd.Audio;
using slskd.Integrations.AcoustId;
using slskd.Integrations.Chromaprint;
using slskd.Integrations.MusicBrainz;
using slskd.VirtualSoulfind.ShadowIndex;
using TagLib;

public interface INormalizationPipeline
{
    Task ProcessSearchObservationAsync(SearchObservation obs, CancellationToken ct = default);
    Task ProcessTransferObservationAsync(TransferObservation obs, CancellationToken ct = default);
}

/// <summary>
/// Converts observations into MBID-aware AudioVariant records.
/// Phase 6A: T-801 - Real implementation.
/// </summary>
public class NormalizationPipeline : INormalizationPipeline
{
    private readonly ILogger<NormalizationPipeline> logger;
    private readonly IFingerprintExtractionService fingerprinting;
    private readonly IAcoustIdClient acoustId;
    private readonly IMusicBrainzClient musicBrainz;
    private readonly IShadowIndexBuilder shadowIndex;
    private readonly QualityScorer qualityScorer;
    private readonly TranscodeDetector transcodeDetector;

    public NormalizationPipeline(
        ILogger<NormalizationPipeline> logger,
        IFingerprintExtractionService fingerprinting,
        IAcoustIdClient acoustId,
        IMusicBrainzClient musicBrainz,
        IShadowIndexBuilder shadowIndex)
    {
        this.logger = logger;
        this.fingerprinting = fingerprinting;
        this.acoustId = acoustId;
        this.musicBrainz = musicBrainz;
        this.shadowIndex = shadowIndex;
        this.qualityScorer = new QualityScorer();
        this.transcodeDetector = new TranscodeDetector();
    }

    public async Task ProcessSearchObservationAsync(SearchObservation obs, CancellationToken ct)
    {
        // For search results, we only have path + heuristic metadata
        // Can't fingerprint without the file, so we do best-effort MB lookup

        if (string.IsNullOrWhiteSpace(obs.Artist) || string.IsNullOrWhiteSpace(obs.Title))
        {
            logger.LogDebug("[VSF-NORM] Insufficient metadata for search observation: Artist={Artist}, Title={Title}",
                obs.Artist, obs.Title);
            return;
        }

        try
        {
            // Query MusicBrainz by artist + title
            var query = $"{obs.Artist} {obs.Title}";
            var mbResults = await musicBrainz.SearchRecordingsAsync(query, limit: 5, ct);

            if (mbResults.Count == 0)
            {
                logger.LogDebug("[VSF-NORM] No MB matches for {Artist} - {Title}", obs.Artist, obs.Title);
                return;
            }

            // Take best match (first result, typically highest score)
            var recording = mbResults.First();

            // Create provisional variant entry
            var variant = new AudioVariant
            {
                VariantId = Ulid.NewUlid().ToString(),
                MusicBrainzRecordingId = recording.RecordingId,

                // Technical properties (from Soulseek metadata)
                Codec = GuessCodecFromExtension(obs.Extension),
                Container = obs.Extension?.TrimStart('.').ToUpperInvariant() ?? "UNKNOWN",
                BitrateKbps = obs.BitRate ?? 0,
                DurationMs = (obs.DurationSeconds ?? 0) * 1000,
                FileSizeBytes = obs.SizeBytes,

                // Placeholder quality (will be refined if we download this file)
                QualityScore = 0.5,  // Unknown
                TranscodeSuspect = false,

                FirstSeenAt = obs.Timestamp,
                LastSeenAt = obs.Timestamp,
                SeenCount = 1
            };

            // Feed to shadow index
            await shadowIndex.AddVariantObservationAsync(
                obs.SoulseekUsername,
                recording.RecordingId,
                variant,
                ct);

            logger.LogInformation("[VSF-NORM] Processed search observation: {RecordingId} from {Username}",
                recording.RecordingId, obs.SoulseekUsername);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[VSF-NORM] Failed to process search observation: {ObservationId}", obs.ObservationId);
        }
    }

    public async Task ProcessTransferObservationAsync(TransferObservation obs, CancellationToken ct)
    {
        if (!obs.Success || string.IsNullOrWhiteSpace(obs.LocalPath) || !System.IO.File.Exists(obs.LocalPath))
        {
            return;
        }

        try
        {
            // We have the actual file! Extract fingerprint
            var fingerprint = await fingerprinting.ExtractFingerprintAsync(obs.LocalPath, ct);

            if (string.IsNullOrWhiteSpace(fingerprint))
            {
                logger.LogWarning("[VSF-NORM] Failed to fingerprint {Path}", obs.LocalPath);
                return;
            }

            // Get file properties for sample rate and duration
            using var tagFile = TagLib.File.Create(obs.LocalPath);
            var props = tagFile.Properties;
            var sampleRate = props.AudioSampleRate;
            var durationSeconds = (int)props.Duration.TotalSeconds;

            // Resolve MusicBrainz Recording ID via AcoustID
            var acoustIdResult = await acoustId.LookupAsync(
                fingerprint,
                sampleRate,
                durationSeconds,
                ct);

            if (acoustIdResult?.Recordings == null || acoustIdResult.Recordings.Length == 0)
            {
                logger.LogWarning("[VSF-NORM] No AcoustID match for {Path}", obs.LocalPath);
                return;
            }

            var recordingId = acoustIdResult.Recordings[0].Id;

            // Build full AudioVariant with quality scoring
            var variant = new AudioVariant
            {
                VariantId = Ulid.NewUlid().ToString(),
                MusicBrainzRecordingId = recordingId,

                // Accurate technical properties
                Codec = props.Description ?? GuessCodecFromExtension(System.IO.Path.GetExtension(obs.LocalPath)),
                Container = System.IO.Path.GetExtension(obs.LocalPath).TrimStart('.').ToUpperInvariant(),
                SampleRateHz = props.AudioSampleRate,
                BitDepth = props.BitsPerSample,
                Channels = props.AudioChannels,
                BitrateKbps = props.AudioBitrate / 1000,
                DurationMs = (int)props.Duration.TotalMilliseconds,
                FileSizeBytes = obs.SizeBytes,

                AudioFingerprint = fingerprint,
                FileSha256 = await ComputeFileSha256Async(obs.LocalPath, ct),

                FirstSeenAt = obs.CompletedAt,
                LastSeenAt = obs.CompletedAt,
                SeenCount = 1
            };

            // Compute quality score
            variant.QualityScore = qualityScorer.ComputeQualityScore(variant);

            // Detect transcodes
            var (isSuspect, reason) = transcodeDetector.DetectTranscode(variant);
            variant.TranscodeSuspect = isSuspect;
            variant.TranscodeReason = reason;

            // Feed to shadow index
            await shadowIndex.AddVariantObservationAsync(
                obs.SoulseekUsername,
                recordingId,
                variant,
                ct);

            logger.LogInformation("[VSF-NORM] Processed transfer observation: {RecordingId} from {Username} (quality={Quality:F2}, transcode={Transcode})",
                recordingId, obs.SoulseekUsername, variant.QualityScore, variant.TranscodeSuspect);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[VSF-NORM] Failed to process transfer observation: {TransferId}", obs.TransferId);
        }
    }

    private static string GuessCodecFromExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return "UNKNOWN";
        }

        return extension.ToLowerInvariant() switch
        {
            ".flac" => "FLAC",
            ".mp3" => "MP3",
            ".m4a" => "AAC",
            ".aac" => "AAC",
            ".opus" => "Opus",
            ".ogg" => "Vorbis",
            ".wav" => "WAV",
            _ => "UNKNOWN"
        };
    }

    private static async Task<string> ComputeFileSha256Async(string filePath, CancellationToken ct)
    {
        using var sha256 = SHA256.Create();
        await using var stream = System.IO.File.OpenRead(filePath);
        var hash = await sha256.ComputeHashAsync(stream, ct);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }
}
