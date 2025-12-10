namespace slskd.VirtualSoulfind.DisasterMode;

using slskd.VirtualSoulfind.ShadowIndex;
using slskd.VirtualSoulfind.Scenes;
using slskd.Integrations.MusicBrainz;

/// <summary>
/// Interface for mesh-only search (disaster mode).
/// </summary>
public interface IMeshSearchService
{
    /// <summary>
    /// Search for content using only mesh/DHT (no Soulseek).
    /// </summary>
    Task<MeshSearchResult> SearchAsync(
        string query,
        CancellationToken ct = default);
    
    /// <summary>
    /// Search by MBID using shadow index.
    /// </summary>
    Task<MeshSearchResult> SearchByMbidAsync(
        string mbid,
        CancellationToken ct = default);
}

/// <summary>
/// Mesh search result.
/// </summary>
public class MeshSearchResult
{
    public string Query { get; set; } = string.Empty;
    public List<MeshPeerResult> PeerResults { get; set; } = new();
    public int TotalPeerCount { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public TimeSpan SearchDuration { get; set; }
}

/// <summary>
/// Peer result from mesh search.
/// </summary>
public class MeshPeerResult
{
    public string PeerId { get; set; } = string.Empty;
    public List<MeshFileResult> Files { get; set; } = new();
}

/// <summary>
/// File result from mesh search.
/// </summary>
public class MeshFileResult
{
    public string Filename { get; set; } = string.Empty;
    public long Size { get; set; }
    public string? Codec { get; set; }
    public int? BitrateKbps { get; set; }
    public double QualityScore { get; set; }
    public string? MbRecordingId { get; set; }
}

/// <summary>
/// Mesh-only search service (DHT + shadow index).
/// </summary>
public class MeshSearchService : IMeshSearchService
{
    private readonly ILogger<MeshSearchService> logger;
    private readonly IShadowIndexQuery shadowIndex;
    private readonly IMusicBrainzClient musicBrainz;
    private readonly ISceneMembershipTracker sceneTracker;

    public MeshSearchService(
        ILogger<MeshSearchService> logger,
        IShadowIndexQuery shadowIndex,
        IMusicBrainzClient musicBrainz,
        ISceneMembershipTracker sceneTracker)
    {
        this.logger = logger;
        this.shadowIndex = shadowIndex;
        this.musicBrainz = musicBrainz;
        this.sceneTracker = sceneTracker;
    }

    public async Task<MeshSearchResult> SearchAsync(string query, CancellationToken ct)
    {
        logger.LogInformation("[VSF-MESH-SEARCH] Searching mesh for: {Query}", query);

        var startTime = DateTimeOffset.UtcNow;

        // Parse query to extract artist/title
        var (artist, title) = ParseQuery(query);

        if (string.IsNullOrEmpty(artist) || string.IsNullOrEmpty(title))
        {
            logger.LogWarning("[VSF-MESH-SEARCH] Could not parse query: {Query}", query);
            return new MeshSearchResult
            {
                Query = query,
                Timestamp = DateTimeOffset.UtcNow,
                SearchDuration = TimeSpan.Zero
            };
        }

        // Search MusicBrainz for recordings
        var mbResults = await musicBrainz.SearchRecordingAsync(
            $"artist:\"{artist}\" AND recording:\"{title}\"",
            ct);

        if (mbResults == null || mbResults.Count == 0)
        {
            logger.LogDebug("[VSF-MESH-SEARCH] No MusicBrainz matches for {Query}", query);
            return new MeshSearchResult
            {
                Query = query,
                Timestamp = DateTimeOffset.UtcNow,
                SearchDuration = DateTimeOffset.UtcNow - startTime
            };
        }

        // Query shadow index for each MBID
        var allPeerResults = new Dictionary<string, MeshPeerResult>();

        foreach (var recording in mbResults.Take(5)) // Top 5 matches
        {
            var shadowResult = await shadowIndex.QueryAsync(recording.Id, ct);
            if (shadowResult != null)
            {
                foreach (var peerId in shadowResult.PeerIds)
                {
                    if (!allPeerResults.ContainsKey(peerId))
                    {
                        allPeerResults[peerId] = new MeshPeerResult { PeerId = peerId };
                    }

                    // Add variant hints as file results
                    foreach (var variant in shadowResult.CanonicalVariants)
                    {
                        allPeerResults[peerId].Files.Add(new MeshFileResult
                        {
                            Filename = $"{artist} - {title}.{variant.Codec.ToLowerInvariant()}",
                            Size = variant.SizeBytes,
                            Codec = variant.Codec,
                            BitrateKbps = variant.BitrateKbps,
                            QualityScore = variant.QualityScore,
                            MbRecordingId = recording.Id
                        });
                    }
                }
            }
        }

        var result = new MeshSearchResult
        {
            Query = query,
            PeerResults = allPeerResults.Values.ToList(),
            TotalPeerCount = allPeerResults.Count,
            Timestamp = DateTimeOffset.UtcNow,
            SearchDuration = DateTimeOffset.UtcNow - startTime
        };

        logger.LogInformation("[VSF-MESH-SEARCH] Found {PeerCount} peers with content for: {Query}",
            result.TotalPeerCount, query);

        return result;
    }

    public async Task<MeshSearchResult> SearchByMbidAsync(string mbid, CancellationToken ct)
    {
        logger.LogInformation("[VSF-MESH-SEARCH] Searching mesh by MBID: {MBID}", mbid);

        var startTime = DateTimeOffset.UtcNow;

        var shadowResult = await shadowIndex.QueryAsync(mbid, ct);
        if (shadowResult == null)
        {
            return new MeshSearchResult
            {
                Query = mbid,
                Timestamp = DateTimeOffset.UtcNow,
                SearchDuration = DateTimeOffset.UtcNow - startTime
            };
        }

        var peerResults = new List<MeshPeerResult>();

        foreach (var peerId in shadowResult.PeerIds)
        {
            var peerResult = new MeshPeerResult { PeerId = peerId };

            foreach (var variant in shadowResult.CanonicalVariants)
            {
                peerResult.Files.Add(new MeshFileResult
                {
                    Filename = $"recording-{mbid}.{variant.Codec.ToLowerInvariant()}",
                    Size = variant.SizeBytes,
                    Codec = variant.Codec,
                    BitrateKbps = variant.BitrateKbps,
                    QualityScore = variant.QualityScore,
                    MbRecordingId = mbid
                });
            }

            peerResults.Add(peerResult);
        }

        var result = new MeshSearchResult
        {
            Query = mbid,
            PeerResults = peerResults,
            TotalPeerCount = peerResults.Count,
            Timestamp = DateTimeOffset.UtcNow,
            SearchDuration = DateTimeOffset.UtcNow - startTime
        };

        logger.LogInformation("[VSF-MESH-SEARCH] Found {PeerCount} peers for MBID: {MBID}",
            result.TotalPeerCount, mbid);

        return result;
    }

    private (string artist, string title) ParseQuery(string query)
    {
        // Try to parse "Artist - Title" format
        var dashIndex = query.IndexOf(" - ", StringComparison.Ordinal);
        if (dashIndex > 0)
        {
            var artist = query.Substring(0, dashIndex).Trim();
            var title = query.Substring(dashIndex + 3).Trim();
            return (artist, title);
        }

        // Fallback: treat as title only
        return (string.Empty, query);
    }
}
