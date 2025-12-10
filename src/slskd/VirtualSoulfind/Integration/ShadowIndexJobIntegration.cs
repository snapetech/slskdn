namespace slskd.VirtualSoulfind.Integration;

using slskd.VirtualSoulfind.ShadowIndex;
using slskd.VirtualSoulfind.DisasterMode;
using slskd.Jobs.Discography;

/// <summary>
/// Integrates shadow index with job resolvers.
/// </summary>
public interface IShadowIndexJobIntegration
{
    /// <summary>
    /// Get peer hints from shadow index for a recording.
    /// </summary>
    Task<List<string>> GetPeerHintsAsync(string mbRecordingId, CancellationToken ct = default);
    
    /// <summary>
    /// Get peer hints for multiple recordings (batch).
    /// </summary>
    Task<Dictionary<string, List<string>>> GetPeerHintsBatchAsync(
        List<string> mbRecordingIds,
        CancellationToken ct = default);
}

/// <summary>
/// Shadow index integration for job resolvers.
/// </summary>
public class ShadowIndexJobIntegration : IShadowIndexJobIntegration
{
    private readonly ILogger<ShadowIndexJobIntegration> logger;
    private readonly IShadowIndexQuery shadowIndex;
    private readonly IDisasterModeCoordinator disasterMode;

    public ShadowIndexJobIntegration(
        ILogger<ShadowIndexJobIntegration> logger,
        IShadowIndexQuery shadowIndex,
        IDisasterModeCoordinator disasterMode)
    {
        this.logger = logger;
        this.shadowIndex = shadowIndex;
        this.disasterMode = disasterMode;
    }

    public async Task<List<string>> GetPeerHintsAsync(string mbRecordingId, CancellationToken ct)
    {
        logger.LogDebug("[VSF-INTEGRATION] Getting peer hints for recording {RecordingId}", mbRecordingId);

        try
        {
            var result = await shadowIndex.QueryAsync(mbRecordingId, ct);
            if (result != null && result.PeerIds.Count > 0)
            {
                logger.LogInformation("[VSF-INTEGRATION] Found {PeerCount} peers for recording {RecordingId}",
                    result.PeerIds.Count, mbRecordingId);
                return result.PeerIds;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[VSF-INTEGRATION] Failed to query shadow index for {RecordingId}",
                mbRecordingId);
        }

        return new List<string>();
    }

    public async Task<Dictionary<string, List<string>>> GetPeerHintsBatchAsync(
        List<string> mbRecordingIds,
        CancellationToken ct)
    {
        logger.LogDebug("[VSF-INTEGRATION] Getting peer hints for {Count} recordings", mbRecordingIds.Count);

        var results = new Dictionary<string, List<string>>();

        foreach (var recordingId in mbRecordingIds)
        {
            var peers = await GetPeerHintsAsync(recordingId, ct);
            if (peers.Count > 0)
            {
                results[recordingId] = peers;
            }
        }

        logger.LogInformation("[VSF-INTEGRATION] Found peer hints for {Count}/{Total} recordings",
            results.Count, mbRecordingIds.Count);

        return results;
    }
}
