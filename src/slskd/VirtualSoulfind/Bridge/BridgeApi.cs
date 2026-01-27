// <copyright file="BridgeApi.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.VirtualSoulfind.Bridge;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using slskd.Integrations.MusicBrainz;
using slskd.VirtualSoulfind.DisasterMode;
using slskd.VirtualSoulfind.Scenes;
using slskd.VirtualSoulfind.ShadowIndex;

public interface IBridgeApi
{
    Task<BridgeSearchResult> SearchAsync(string query, CancellationToken ct = default);
    Task<string> DownloadAsync(string username, string filename, string? targetPath, CancellationToken ct = default);
    Task<List<BridgeRoom>> GetRoomsAsync(CancellationToken ct = default);
}

public class BridgeSearchResult
{
    public string Query { get; set; } = string.Empty;
    public List<BridgeUser> Users { get; set; } = new();
}

public class BridgeUser
{
    public string PeerId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public List<BridgeFile> Files { get; set; } = new();
}

public class BridgeRoom
{
    public string Name { get; set; } = string.Empty;
    public int MemberCount { get; set; }
}

public class BridgeFile
{
    public string Path { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string? MbRecordingId { get; set; }
    public int? BitrateKbps { get; set; }
    public string? Codec { get; set; }
    public bool IsCanonical { get; set; }
}

/// <summary>
/// Bridge API implementation - connects legacy clients to Virtual Soulfind mesh.
/// </summary>
public class BridgeApi : IBridgeApi
{
    private readonly ILogger<BridgeApi> logger;
    private readonly IShadowIndexQuery shadowIndex;
    private readonly IMusicBrainzClient musicBrainz;
    private readonly IMeshSearchService meshSearch;
    private readonly IMeshTransferService meshTransfer;
    private readonly ISceneService sceneService;
    private readonly IPeerIdAnonymizer peerAnonymizer;
    private readonly IFilenameGenerator filenameGenerator;
    private readonly IRoomSceneMapper roomSceneMapper;
    private readonly ITransferProgressProxy progressProxy;
    private readonly ConcurrentDictionary<string, string> transferIdToProxyId = new(); // Map mesh transfer ID to proxy ID

    public BridgeApi(
        ILogger<BridgeApi> logger,
        IShadowIndexQuery shadowIndex,
        IMusicBrainzClient musicBrainz,
        IMeshSearchService meshSearch,
        IMeshTransferService meshTransfer,
        ISceneService sceneService,
        IPeerIdAnonymizer peerAnonymizer,
        IFilenameGenerator filenameGenerator,
        IRoomSceneMapper roomSceneMapper,
        ITransferProgressProxy progressProxy)
    {
        this.logger = logger;
        this.shadowIndex = shadowIndex;
        this.musicBrainz = musicBrainz;
        this.meshSearch = meshSearch;
        this.meshTransfer = meshTransfer;
        this.sceneService = sceneService;
        this.peerAnonymizer = peerAnonymizer;
        this.filenameGenerator = filenameGenerator;
        this.roomSceneMapper = roomSceneMapper;
        this.progressProxy = progressProxy;
    }

    /// <summary>
    /// T-852, T-853: Bridge search - resolves query to MBIDs, queries shadow index, synthesizes filenames.
    /// </summary>
    public async Task<BridgeSearchResult> SearchAsync(string query, CancellationToken ct = default)
    {
        logger.LogInformation("[VSF-BRIDGE] Search: {Query}", query);

        var result = new BridgeSearchResult { Query = query };
        var userMap = new Dictionary<string, BridgeUser>();

        try
        {
            // Strategy 1: Try to resolve query to MBIDs (T-853)
            var mbids = await TryResolveToMBIDsAsync(query, ct);
            
            if (mbids.Count > 0)
            {
                logger.LogDebug("[VSF-BRIDGE] Resolved {Count} MBIDs for query: {Query}", mbids.Count, query);
                
                // Query shadow index for each MBID
                foreach (var mbid in mbids)
                {
                    var shadowResult = await shadowIndex.QueryAsync(mbid, ct);
                    if (shadowResult == null || shadowResult.PeerIds.Count == 0)
                    {
                        continue;
                    }

                    // For each peer, create a user entry
                    foreach (var peerId in shadowResult.PeerIds)
                    {
                        var username = await peerAnonymizer.GetAnonymizedUsernameAsync(peerId, ct);
                        
                        if (!userMap.TryGetValue(username, out var user))
                        {
                            user = new BridgeUser
                            {
                                PeerId = peerId,
                                Username = username
                            };
                            userMap[username] = user;
                        }

                        // Add files from canonical variants (T-854)
                        foreach (var variant in shadowResult.CanonicalVariants)
                        {
                            // Try to get artist/title from MusicBrainz
                            var recording = await musicBrainz.GetRecordingAsync(mbid, ct);
                            var artist = recording?.Artist ?? "Unknown Artist";
                            var title = recording?.Title ?? "Unknown Title";

                            var filename = await filenameGenerator.GenerateFilenameAsync(
                                artist,
                                title,
                                variant,
                                ct);

                            user.Files.Add(new BridgeFile
                            {
                                Path = filename,
                                SizeBytes = variant.SizeBytes,
                                MbRecordingId = mbid,
                                BitrateKbps = variant.BitrateKbps,
                                Codec = variant.Codec,
                                IsCanonical = true
                            });
                        }
                    }
                }
            }

            // Strategy 2: Fallback to mesh search if no MBID results
            if (userMap.Count == 0)
            {
                logger.LogDebug("[VSF-BRIDGE] No MBID results, trying mesh search");
                var meshResult = await meshSearch.SearchAsync(query, ct);
                
                foreach (var peerResult in meshResult.PeerResults)
                {
                    var username = await peerAnonymizer.GetAnonymizedUsernameAsync(peerResult.PeerId, ct);
                    
                    if (!userMap.TryGetValue(username, out var user))
                    {
                        user = new BridgeUser
                        {
                            PeerId = peerResult.PeerId,
                            Username = username
                        };
                        userMap[username] = user;
                    }

                    foreach (var file in peerResult.Files)
                    {
                        user.Files.Add(new BridgeFile
                        {
                            Path = file.Filename,
                            SizeBytes = file.Size,
                            MbRecordingId = file.MbRecordingId,
                            BitrateKbps = file.BitrateKbps,
                            Codec = file.Codec,
                            IsCanonical = file.QualityScore > 0.8 // High quality = likely canonical
                        });
                    }
                }
            }

            result.Users = userMap.Values.ToList();
            logger.LogInformation("[VSF-BRIDGE] Search complete: {UserCount} users, {FileCount} total files",
                result.Users.Count, result.Users.Sum(u => u.Files.Count));

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[VSF-BRIDGE] Search failed: {Message}", ex.Message);
            return result; // Return empty results on error
        }
    }

    /// <summary>
    /// T-852: Bridge download - resolves username to peer ID, starts mesh transfer.
    /// </summary>
    public async Task<string> DownloadAsync(string username, string filename, string? targetPath, CancellationToken ct = default)
    {
        logger.LogInformation("[VSF-BRIDGE] Download: {Username}/{Filename}", username, filename);

        try
        {
            // Resolve anonymized username to peer ID (T-855)
            var peerId = await peerAnonymizer.GetPeerIdFromUsernameAsync(username, ct);
            if (peerId == null)
            {
                throw new InvalidOperationException($"Cannot resolve username to peer ID: {username}");
            }

            // Extract file hash from filename or metadata
            // For now, we'll need to query the shadow index to find the file hash
            // This is a simplified implementation - in practice, we'd need more metadata
            var fileHash = ExtractHashFromFilename(filename);
            if (fileHash == null)
            {
                // Try to get file size from shadow index or mesh search
                // For now, use a placeholder
                fileHash = Guid.NewGuid().ToString("N");
            }

            var fileSize = ExtractSizeFromFilename(filename) ?? 0;

            // Determine target path
            var finalTargetPath = targetPath ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Downloads",
                filename);

            // Start mesh transfer
            var transferId = await meshTransfer.StartTransferAsync(
                peerId,
                fileHash,
                fileSize,
                finalTargetPath,
                ct);

            // Start progress proxy for legacy client (T-857)
            var legacyClientId = username; // Use username as client identifier
            var proxyId = await progressProxy.StartProxyAsync(transferId, legacyClientId, ct);
            transferIdToProxyId[transferId] = proxyId;

            logger.LogInformation("[VSF-BRIDGE] Download started: transfer {TransferId}, proxy {ProxyId}", transferId, proxyId);
            return transferId; // Return transfer ID (could also return proxy ID)
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[VSF-BRIDGE] Download failed: {Message}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// T-856: Bridge rooms - maps scenes to legacy room format.
    /// </summary>
    public async Task<List<BridgeRoom>> GetRoomsAsync(CancellationToken ct = default)
    {
        logger.LogDebug("[VSF-BRIDGE] Get rooms");

        try
        {
            var rooms = new List<BridgeRoom>();

            // Get joined scenes
            var scenes = await sceneService.GetJoinedScenesAsync(ct);
            
            foreach (var scene in scenes)
            {
                // Map scene to room name (T-856)
                var roomName = roomSceneMapper.MapSceneToRoom(scene.SceneId);
                
                // Get scene members for count
                var members = await sceneService.GetSceneMembersAsync(scene.SceneId, ct);
                var memberCount = members?.Count ?? scene.MemberCount;

                rooms.Add(new BridgeRoom
                {
                    Name = roomName,
                    MemberCount = memberCount
                });
            }

            logger.LogDebug("[VSF-BRIDGE] Returning {Count} rooms", rooms.Count);
            return rooms;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[VSF-BRIDGE] Get rooms failed: {Message}", ex.Message);
            return new List<BridgeRoom>();
        }
    }

    /// <summary>
    /// T-853: Try to resolve search query to MusicBrainz Recording IDs.
    /// </summary>
    private async Task<List<string>> TryResolveToMBIDsAsync(string query, CancellationToken ct)
    {
        try
        {
            var recordings = await musicBrainz.SearchRecordingsAsync(query, limit: 10, ct);
            return recordings.Select(r => r.RecordingId).ToList();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[VSF-BRIDGE] Failed to resolve query to MBIDs: {Query}", query);
            return new List<string>();
        }
    }

    private string? ExtractHashFromFilename(string filename)
    {
        // Heuristic: look for hash-like patterns in filename
        // In practice, this would come from metadata or shadow index lookup
        return null;
    }

    private long? ExtractSizeFromFilename(string filename)
    {
        // Heuristic: try to extract size from filename patterns
        // In practice, this would come from metadata or shadow index lookup
        return null;
    }
}
