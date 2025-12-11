namespace slskd.VirtualSoulfind.DisasterMode;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using slskd.Mesh;
using slskd.Mesh.Dht;

/// <summary>
/// Result of a mesh search operation.
/// </summary>
public class MeshSearchResult
{
    public string Query { get; set; } = string.Empty;
    public List<MeshPeerResult> PeerResults { get; set; } = new();
    public int TotalPeerCount { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public TimeSpan SearchDuration { get; set; } = TimeSpan.Zero;
}

/// <summary>
/// Result from a single mesh peer.
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
/// Service for searching the mesh overlay network.
/// </summary>
public interface IMeshSearchService
{
    Task<MeshSearchResult> SearchAsync(string query, CancellationToken ct = default);
    Task<MeshSearchResult> SearchByMbidAsync(string mbid, CancellationToken ct = default);
}

/// <summary>
/// Implements mesh search by querying DHT and mesh peers.
/// </summary>
public class MeshSearchService : IMeshSearchService
{
    private readonly ILogger<MeshSearchService> logger;
    private readonly IMeshDirectory meshDirectory;
    private readonly IMeshSyncService meshSync;

    public MeshSearchService(
        ILogger<MeshSearchService> logger,
        IMeshDirectory meshDirectory = null,
        IMeshSyncService meshSync = null)
    {
        this.logger = logger;
        this.meshDirectory = meshDirectory;
        this.meshSync = meshSync;
    }

    public async Task<MeshSearchResult> SearchAsync(string query, CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();
        logger.LogInformation("[VSF-MESH-SEARCH] Searching mesh for query: {Query}", query);

        var result = new MeshSearchResult
        {
            Query = query,
            Timestamp = DateTimeOffset.UtcNow,
        };

        if (meshDirectory == null)
        {
            logger.LogWarning("[VSF-MESH-SEARCH] MeshDirectory not available, returning empty results");
            result.SearchDuration = stopwatch.Elapsed;
            return result;
        }

        try
        {
            // Strategy 1: Query mesh peers directly for content matching query
            // For text queries, we search peers' content lists
            var meshPeers = meshSync?.GetMeshPeers()?.ToList() ?? new List<MeshPeerInfo>();

            if (meshPeers.Count == 0)
            {
                logger.LogDebug("[VSF-MESH-SEARCH] No mesh peers available");
                result.SearchDuration = stopwatch.Elapsed;
                return result;
            }

            logger.LogDebug("[VSF-MESH-SEARCH] Querying {Count} mesh peers", meshPeers.Count);

            // Query each peer's content in parallel (with limit)
            var queryTasks = meshPeers
                .Take(10) // Limit to 10 peers to avoid flooding
                .Select(async peer =>
                {
                    try
                    {
                        var peerContent = await meshDirectory.FindContentByPeerAsync(peer.Username, ct);
                        return new { Peer = peer, Content = peerContent };
                    }
                    catch (Exception ex)
                    {
                        logger.LogDebug(ex, "[VSF-MESH-SEARCH] Failed to query peer {Peer}", peer.Username);
                        return null;
                    }
                });

            var peerContentResults = await Task.WhenAll(queryTasks);
            var validResults = peerContentResults.Where(r => r != null).ToList();

            // Filter content by query (simple text matching for now)
            var queryLower = query.ToLowerInvariant();
            var matchingPeers = new Dictionary<string, MeshPeerResult>();

            foreach (var peerResult in validResults)
            {
                var matchingFiles = new List<MeshFileResult>();

                foreach (var content in peerResult.Content)
                {
                    // Simple matching: check if content ID or codec contains query
                    // In a real implementation, would parse ContentDescriptor metadata
                    var contentIdLower = content.ContentId?.ToLowerInvariant() ?? "";
                    var codecLower = content.Codec?.ToLowerInvariant() ?? "";

                    if (contentIdLower.Contains(queryLower) || codecLower.Contains(queryLower))
                    {
                        matchingFiles.Add(new MeshFileResult
                        {
                            Filename = content.ContentId ?? "unknown",
                            Size = content.SizeBytes ?? 0,
                            Codec = content.Codec,
                            QualityScore = 0.5, // Default score
                        });
                    }
                }

                if (matchingFiles.Count > 0)
                {
                    if (!matchingPeers.ContainsKey(peerResult.Peer.Username))
                    {
                        matchingPeers[peerResult.Peer.Username] = new MeshPeerResult
                        {
                            PeerId = peerResult.Peer.Username,
                            Files = new List<MeshFileResult>(),
                        };
                    }

                    matchingPeers[peerResult.Peer.Username].Files.AddRange(matchingFiles);
                }
            }

            result.PeerResults = matchingPeers.Values.ToList();
            result.TotalPeerCount = matchingPeers.Count;
            result.SearchDuration = stopwatch.Elapsed;

            logger.LogInformation(
                "[VSF-MESH-SEARCH] Search completed: {PeerCount} peers, {FileCount} files in {Duration}ms",
                result.TotalPeerCount,
                result.PeerResults.Sum(p => p.Files.Count),
                result.SearchDuration.TotalMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[VSF-MESH-SEARCH] Search failed for query: {Query}", query);
            result.SearchDuration = stopwatch.Elapsed;
            return result;
        }
    }

    public async Task<MeshSearchResult> SearchByMbidAsync(string mbid, CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();
        logger.LogInformation("[VSF-MESH-SEARCH] Searching mesh for MBID: {Mbid}", mbid);

        var result = new MeshSearchResult
        {
            Query = mbid,
            Timestamp = DateTimeOffset.UtcNow,
        };

        if (meshDirectory == null)
        {
            logger.LogWarning("[VSF-MESH-SEARCH] MeshDirectory not available, returning empty results");
            result.SearchDuration = stopwatch.Elapsed;
            return result;
        }

        try
        {
            // Query DHT for peers with this MusicBrainz recording ID
            var contentId = $"mbid:recording:{mbid}";
            var peers = await meshDirectory.FindPeersByContentAsync(contentId, ct);

            logger.LogDebug("[VSF-MESH-SEARCH] Found {Count} peers with MBID {Mbid}", peers.Count, mbid);

            var peerResults = new List<MeshPeerResult>();

            // Get content details from each peer
            foreach (var peer in peers)
            {
                try
                {
                    var peerContent = await meshDirectory.FindContentByPeerAsync(peer.PeerId, ct);
                    var matchingContent = peerContent.Where(c => c.ContentId?.Contains(mbid, StringComparison.OrdinalIgnoreCase) == true).ToList();

                    if (matchingContent.Count > 0)
                    {
                        var files = matchingContent.Select(c => new MeshFileResult
                        {
                            Filename = c.ContentId ?? "unknown",
                            Size = c.SizeBytes ?? 0,
                            Codec = c.Codec,
                            MbRecordingId = mbid,
                            QualityScore = 0.8, // Higher score for MBID matches
                        }).ToList();

                        peerResults.Add(new MeshPeerResult
                        {
                            PeerId = peer.PeerId,
                            Files = files,
                        });
                    }
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "[VSF-MESH-SEARCH] Failed to get content from peer {Peer}", peer.PeerId);
                }
            }

            result.PeerResults = peerResults;
            result.TotalPeerCount = peerResults.Count;
            result.SearchDuration = stopwatch.Elapsed;

            logger.LogInformation(
                "[VSF-MESH-SEARCH] MBID search completed: {PeerCount} peers, {FileCount} files in {Duration}ms",
                result.TotalPeerCount,
                result.PeerResults.Sum(p => p.Files.Count),
                result.SearchDuration.TotalMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[VSF-MESH-SEARCH] MBID search failed: {Mbid}", mbid);
            result.SearchDuration = stopwatch.Elapsed;
            return result;
        }
    }
}
