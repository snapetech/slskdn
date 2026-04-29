// <copyright file="BridgeApi.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.VirtualSoulfind.Bridge;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using slskd.Common.Security;
using slskd.Integrations.MusicBrainz;
using slskd.VirtualSoulfind.DisasterMode;
using slskd.VirtualSoulfind.Scenes;
using slskd.VirtualSoulfind.ShadowIndex;

public interface IBridgeApi
{
    Task<BridgeSearchResult> SearchAsync(string query, CancellationToken ct = default);
    Task<string> DownloadAsync(string username, string filename, string? targetPath, CancellationToken ct = default);
    Task<List<BridgeRoom>> GetRoomsAsync(CancellationToken ct = default);
    Task<LegacyTransferProgress?> GetTransferProgressAsync(string transferId, CancellationToken ct = default);
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
    private static readonly Regex FilenameHashRegex = new(
        @"(?<![0-9a-fA-F])([0-9a-fA-F]{32,128})(?![0-9a-fA-F])",
        RegexOptions.Compiled);
    private static readonly Regex FilenameBracketedSizeRegex = new(
        @"[\[\(](\d{1,3}(?:[,_ ]\d{3})+|\d{6,})[\]\)]",
        RegexOptions.Compiled);
    private static readonly Regex FilenameByteSizeRegex = new(
        @"(?<!\d)(\d{1,3}(?:[,_ ]\d{3})+|\d{6,})\s*(?:bytes?|b)(?![a-z])",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private readonly ConcurrentDictionary<string, BridgeFileMetadata> searchFileMetadata = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, BridgeTransferMetadata> transferMetadata = new();
    private readonly ILogger<BridgeApi> logger;
    private readonly IOptionsMonitor<slskd.Options> optionsMonitor;
    private readonly IShadowIndexQuery shadowIndex;
    private readonly IMusicBrainzClient musicBrainz;
    private readonly IMeshSearchService meshSearch;
    private readonly IMeshTransferService meshTransfer;
    private readonly ISceneService sceneService;
    private readonly IPeerIdAnonymizer peerAnonymizer;
    private readonly IFilenameGenerator filenameGenerator;
    private readonly IRoomSceneMapper roomSceneMapper;

    public BridgeApi(
        ILogger<BridgeApi> logger,
        IOptionsMonitor<slskd.Options> optionsMonitor,
        IShadowIndexQuery shadowIndex,
        IMusicBrainzClient musicBrainz,
        IMeshSearchService meshSearch,
        IMeshTransferService meshTransfer,
        ISceneService sceneService,
        IPeerIdAnonymizer peerAnonymizer,
        IFilenameGenerator filenameGenerator,
        IRoomSceneMapper roomSceneMapper)
    {
        this.logger = logger;
        this.optionsMonitor = optionsMonitor;
        this.shadowIndex = shadowIndex;
        this.musicBrainz = musicBrainz;
        this.meshSearch = meshSearch;
        this.meshTransfer = meshTransfer;
        this.sceneService = sceneService;
        this.peerAnonymizer = peerAnonymizer;
        this.filenameGenerator = filenameGenerator;
        this.roomSceneMapper = roomSceneMapper;
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

                            UpsertBridgeFile(
                                user.Files,
                                new BridgeFile
                                {
                                    Path = filename,
                                    SizeBytes = variant.SizeBytes,
                                    MbRecordingId = mbid,
                                    BitrateKbps = variant.BitrateKbps,
                                    Codec = variant.Codec,
                                    IsCanonical = true
                                });
                            CacheBridgeFileMetadata(username, filename, variant.SizeBytes, mbid, variant.Codec, isCanonical: true);
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
                        var isCanonical = file.QualityScore > 0.8;
                        UpsertBridgeFile(
                            user.Files,
                            new BridgeFile
                            {
                                Path = file.Filename,
                                SizeBytes = file.Size,
                                MbRecordingId = file.MbRecordingId,
                                BitrateKbps = file.BitrateKbps,
                                Codec = file.Codec,
                                IsCanonical = isCanonical
                            });
                        CacheBridgeFileMetadata(username, file.Filename, file.Size, file.MbRecordingId, file.Codec, isCanonical);
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
            var cachedFile = GetCachedBridgeFileMetadata(username, filename);
            var fileHash = ExtractHashFromFilename(filename);
            if (fileHash == null)
            {
                // Without a parsed hash, don't force integrity failures by inventing a value.
                // Verification step will skip hash comparison when empty.
                fileHash = string.Empty;
            }

            var fileSize = cachedFile?.SizeBytes ?? ExtractSizeFromFilename(filename) ?? 0;

            // Determine target path
            var finalTargetPath = ResolveTargetPath(filename, targetPath);

            // Start mesh transfer
            var transferId = await meshTransfer.StartTransferAsync(
                peerId,
                fileHash,
                fileSize,
                finalTargetPath,
                ct);

            transferMetadata[transferId] = new BridgeTransferMetadata
            {
                RequestedFilename = filename,
                TargetPath = finalTargetPath,
                PeerId = peerId,
                SizeBytes = fileSize,
            };

            logger.LogInformation("[VSF-BRIDGE] Download started: transfer {TransferId}", transferId);
            return transferId; // Return transfer ID (could also return proxy ID)
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[VSF-BRIDGE] Download failed: {Message}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// T-857: Return mesh transfer status using legacy progress format.
    /// </summary>
    public async Task<LegacyTransferProgress?> GetTransferProgressAsync(string transferId, CancellationToken ct = default)
    {
        try
        {
            transferMetadata.TryGetValue(transferId, out var metadata);
            var status = await meshTransfer.GetTransferStatusAsync(transferId, ct);
            if (status == null)
            {
                return metadata?.LastProgress;
            }

            var username = await peerAnonymizer.GetAnonymizedUsernameAsync(status.PeerId, ct);
            var filename = Path.GetFileName(status.TargetPath);
            if (string.IsNullOrWhiteSpace(filename) && metadata != null)
            {
                filename = metadata.RequestedFilename;
            }

            var fileSize = status.FileSize > 0
                ? status.FileSize
                : metadata?.SizeBytes ?? 0;
            var percentComplete = fileSize > 0
                ? (int)Math.Clamp((status.BytesTransferred * 100.0) / fileSize, 0, 100)
                : (int)Math.Clamp(status.ProgressPercent, 0, 100);

            var progress = new LegacyTransferProgress
            {
                ProxyId = transferId,
                Username = username,
                Filename = string.IsNullOrWhiteSpace(filename) ? transferId : filename,
                BytesTransferred = status.BytesTransferred,
                FileSize = fileSize,
                PercentComplete = percentComplete,
                AverageSpeed = status.TransferRateBps,
                State = MapMeshStateToLegacy(status.State),
                QueuePosition = 0
            };

            if (metadata != null)
            {
                metadata.LastProgress = progress;
            }

            return progress;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[VSF-BRIDGE] Failed to load transfer progress for {TransferId}", transferId);
            return transferMetadata.TryGetValue(transferId, out var metadata)
                ? metadata.LastProgress
                : null;
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
            return recordings
                .Select(r => r.RecordingId)
                .Where(recordingId => !string.IsNullOrWhiteSpace(recordingId))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[VSF-BRIDGE] Failed to resolve query to MBIDs: {Query}", query);
            return new List<string>();
        }
    }

    private string? ExtractHashFromFilename(string filename)
    {
        var fileNameOnly = Path.GetFileNameWithoutExtension(filename);
        if (string.IsNullOrWhiteSpace(fileNameOnly))
        {
            return null;
        }

        var match = FilenameHashRegex.Matches(fileNameOnly)
            .Select(candidate => candidate.Groups[1].Value)
            .OrderByDescending(candidate => candidate.Length)
            .FirstOrDefault();

        return string.IsNullOrWhiteSpace(match) ? null : match;
    }

    private long? ExtractSizeFromFilename(string filename)
    {
        var fileNameOnly = Path.GetFileNameWithoutExtension(filename);
        if (string.IsNullOrWhiteSpace(fileNameOnly))
        {
            return null;
        }

        var candidate = FilenameBracketedSizeRegex.Match(fileNameOnly);
        if (!candidate.Success)
        {
            candidate = FilenameByteSizeRegex.Match(fileNameOnly);
        }

        if (!candidate.Success)
        {
            return null;
        }

        var normalized = candidate.Groups[1].Value
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace(",", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal);

        if (long.TryParse(normalized, out var sizeBytes) && sizeBytes > 0)
        {
            return sizeBytes;
        }

        return null;
    }

    private void CacheBridgeFileMetadata(string username, string filename, long sizeBytes, string? mbRecordingId, string? codec, bool isCanonical)
    {
        searchFileMetadata.AddOrUpdate(
            GetBridgeFileKey(username, filename),
            _ => new BridgeFileMetadata
            {
                Username = username,
                Filename = filename,
                SizeBytes = sizeBytes,
                MbRecordingId = mbRecordingId,
                Codec = codec,
                IsCanonical = isCanonical,
            },
            (_, existing) => new BridgeFileMetadata
            {
                Username = existing.Username,
                Filename = existing.Filename,
                SizeBytes = Math.Max(existing.SizeBytes, sizeBytes),
                MbRecordingId = existing.MbRecordingId ?? mbRecordingId,
                Codec = existing.Codec ?? codec,
                IsCanonical = existing.IsCanonical || isCanonical,
            });
    }

    private BridgeFileMetadata? GetCachedBridgeFileMetadata(string username, string filename)
    {
        searchFileMetadata.TryGetValue(GetBridgeFileKey(username, filename), out var metadata);
        return metadata;
    }

    private static string GetBridgeFileKey(string username, string filename)
    {
        return $"{username}\u001f{filename}";
    }

    private static void UpsertBridgeFile(List<BridgeFile> files, BridgeFile candidate)
    {
        var existing = files.FirstOrDefault(file => string.Equals(file.Path, candidate.Path, StringComparison.OrdinalIgnoreCase));
        if (existing == null)
        {
            files.Add(candidate);
            return;
        }

        existing.SizeBytes = Math.Max(existing.SizeBytes, candidate.SizeBytes);
        existing.MbRecordingId ??= candidate.MbRecordingId;
        existing.BitrateKbps ??= candidate.BitrateKbps;
        existing.Codec ??= candidate.Codec;
        existing.IsCanonical |= candidate.IsCanonical;
    }

    private string ResolveTargetPath(string filename, string? targetPath)
    {
        var sanitizedFilename = PathGuard.SanitizeFilename(filename);
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            return Path.Combine(optionsMonitor.CurrentValue.Directories.Downloads, sanitizedFilename);
        }

        if (Directory.Exists(targetPath) ||
            targetPath.EndsWith(Path.DirectorySeparatorChar) ||
            targetPath.EndsWith(Path.AltDirectorySeparatorChar))
        {
            return Path.Combine(targetPath, sanitizedFilename);
        }

        return targetPath;
    }

    private static string MapMeshStateToLegacy(MeshTransferState meshState)
    {
        return meshState switch
        {
            MeshTransferState.Initializing => "Queued",
            MeshTransferState.DiscoveringPeers => "Connecting",
            MeshTransferState.Transferring => "Downloading",
            MeshTransferState.Verifying => "Downloading",
            MeshTransferState.Completed => "Complete",
            MeshTransferState.Failed => "Errored",
            MeshTransferState.Cancelled => "Cancelled",
            _ => "Unknown"
        };
    }

    private sealed class BridgeTransferMetadata
    {
        public string RequestedFilename { get; init; } = string.Empty;
        public string TargetPath { get; init; } = string.Empty;
        public string PeerId { get; init; } = string.Empty;
        public long SizeBytes { get; init; }
        public LegacyTransferProgress? LastProgress { get; set; }
    }

    private sealed class BridgeFileMetadata
    {
        public string Username { get; init; } = string.Empty;
        public string Filename { get; init; } = string.Empty;
        public long SizeBytes { get; init; }
        public string? MbRecordingId { get; init; }
        public string? Codec { get; init; }
        public bool IsCanonical { get; init; }
    }
}
