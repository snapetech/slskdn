namespace slskd.VirtualSoulfind.Capture;

using System.Security.Cryptography;
using slskd.Audio;
using slskd.Integrations.AcoustId;
using slskd.Integrations.Chromaprint;
using slskd.Integrations.MusicBrainz;
using slskd.VirtualSoulfind.ShadowIndex;

/// <summary>
/// Interface for normalization pipeline.
/// </summary>
public interface INormalizationPipeline
{
    Task ProcessSearchObservationAsync(SearchObservation obs, CancellationToken ct = default);
    Task ProcessTransferObservationAsync(TransferObservation obs, CancellationToken ct = default);
}

/// <summary>
/// Converts observations into MB-aware AudioVariant records.
/// </summary>
public class NormalizationPipeline : INormalizationPipeline
{
    private readonly ILogger<NormalizationPipeline> logger;
    private readonly IFingerprintExtractionService fingerprinting;
    private readonly IAcoustIdClient acoustId;
    private readonly IMusicBrainzClient musicBrainz;
    private readonly IShadowIndexBuilder shadowIndex;

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
    }

    public async Task ProcessSearchObservationAsync(SearchObservation obs, CancellationToken ct)
    {
        // For search results, we only have path + heuristic metadata
        // Can't fingerprint without the file, so we do best-effort MB lookup
        
        if (string.IsNullOrEmpty(obs.Artist) || string.IsNullOrEmpty(obs.Title))
        {
            logger.LogDebug("[VSF-NORM] Skipping search observation {ObsId}: insufficient metadata",
                obs.ObservationId);
            return;
        }

        try
        {
            // Query MusicBrainz by artist + title
            var mbResults = await musicBrainz.SearchRecordingAsync(
                $"artist:\"{obs.Artist}\" AND recording:\"{obs.Title}\"",
                ct);
            
            if (mbResults == null || mbResults.Count == 0)
            {
                logger.LogDebug("[VSF-NORM] No MB matches for {Artist} - {Title}",
                    obs.Artist, obs.Title);
                return;
            }
            
            // Take best match (first result, typically highest score)
            var recording = mbResults.First();
            
            // Create provisional variant entry
            var variant = new AudioVariant
            {
                VariantId = Ulid.NewUlid().ToString(),
                MusicBrainzRecordingId = recording.Id,
                
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
                recording.Id,
                variant,
                ct);
                
            logger.LogInformation("[VSF-NORM] Normalized search observation {ObsId} to MB recording {RecordingId}",
                obs.ObservationId, recording.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[VSF-NORM] Failed to normalize search observation {ObsId}",
                obs.ObservationId);
        }
    }

    public async Task ProcessTransferObservationAsync(TransferObservation obs, CancellationToken ct)
    {
        if (!obs.Success)
        {
            return;
        }

        if (string.IsNullOrEmpty(obs.LocalPath) || !File.Exists(obs.LocalPath))
        {
            logger.LogWarning("[VSF-NORM] Transfer observation {TransferId} has no local file",
                obs.TransferId);
            return;
        }

        try
        {
            // We have the actual file! Extract fingerprint
            var fingerprint = await fingerprinting.ExtractFingerprintAsync(obs.LocalPath, ct);
            
            if (fingerprint == null)
            {
                logger.LogWarning("[VSF-NORM] Failed to fingerprint {Path}", obs.LocalPath);
                return;
            }
            
            // Resolve MusicBrainz Recording ID via AcoustID
            var acoustIdResult = await acoustId.LookupAsync(
                fingerprint.Fingerprint,
                fingerprint.DurationSeconds,
                ct);
            
            if (acoustIdResult?.Recordings == null || acoustIdResult.Recordings.Count == 0)
            {
                logger.LogWarning("[VSF-NORM] No AcoustID match for {Path}", obs.LocalPath);
                return;
            }
            
            var recordingId = acoustIdResult.Recordings.First().Id;
            
            // Build full AudioVariant with quality scoring
            var tagFile = TagLib.File.Create(obs.LocalPath);
            var props = tagFile.Properties;
            
            var variant = new AudioVariant
            {
                VariantId = Ulid.NewUlid().ToString(),
                MusicBrainzRecordingId = recordingId,
                
                // Accurate technical properties
                Codec = props.Description,
                Container = Path.GetExtension(obs.LocalPath).TrimStart('.').ToUpperInvariant(),
                SampleRateHz = props.AudioSampleRate,
                BitDepth = props.BitsPerSample,
                Channels = props.AudioChannels,
                BitrateKbps = props.AudioBitrate,
                DurationMs = (int)props.Duration.TotalMilliseconds,
                FileSizeBytes = obs.SizeBytes,
                
                AudioFingerprint = fingerprint.Fingerprint,
                FileSha256 = await ComputeFileSha256Async(obs.LocalPath, ct),
                
                FirstSeenAt = obs.CompletedAt,
                LastSeenAt = obs.CompletedAt,
                SeenCount = 1
            };
            
            // Compute quality score
            var scorer = new QualityScorer();
            variant.QualityScore = scorer.ComputeQualityScore(variant);
            
            // Detect transcodes
            var detector = new TranscodeDetector();
            var (isSuspect, reason) = detector.DetectTranscode(variant);
            variant.TranscodeSuspect = isSuspect;
            variant.TranscodeReason = reason;
            
            // Feed to shadow index
            await shadowIndex.AddVariantObservationAsync(
                obs.SoulseekUsername,
                recordingId,
                variant,
                ct);
                
            logger.LogInformation("[VSF-NORM] Normalized transfer observation {TransferId} to MB recording {RecordingId}",
                obs.TransferId, recordingId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[VSF-NORM] Failed to normalize transfer observation {TransferId}",
                obs.TransferId);
        }
    }

    private static string GuessCodecFromExtension(string? extension)
    {
        return extension?.ToLowerInvariant() switch
        {
            ".flac" => "FLAC",
            ".mp3" => "MP3",
            ".m4a" => "AAC",
            ".aac" => "AAC",
            ".opus" => "Opus",
            ".ogg" => "Vorbis",
            ".wma" => "WMA",
            ".wav" => "PCM",
            _ => "UNKNOWN"
        };
    }

    private static async Task<string> ComputeFileSha256Async(string filePath, CancellationToken ct)
    {
        using var stream = File.OpenRead(filePath);
        var hash = await SHA256.HashDataAsync(stream, ct);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
