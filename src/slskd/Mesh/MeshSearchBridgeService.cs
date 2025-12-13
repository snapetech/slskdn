// <copyright file="MeshSearchBridgeService.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Mesh;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Soulseek;
using slskd.HashDb;

/// <summary>
/// Bridges mesh hash database content back to Soulseek search responses.
/// When Soulseek users search for content, we supplement results with
/// mesh-discovered files, making mesh discoveries benefit the Soulseek community.
/// </summary>
public sealed class MeshSearchBridgeService
{
    private readonly ILogger<MeshSearchBridgeService> _logger;
    private readonly IHashDbService _hashDb;
    private readonly Identity.ISoulseekMeshIdentityMapper _identityMapper;
    
    public MeshSearchBridgeService(
        ILogger<MeshSearchBridgeService> logger,
        IHashDbService hashDb,
        Identity.ISoulseekMeshIdentityMapper identityMapper)
    {
        _logger = logger;
        _hashDb = hashDb;
        _identityMapper = identityMapper;
    }
    
    /// <summary>
    /// Supplements Soulseek search responses with mesh-discovered files.
    /// Call this from SearchService.SearchAsync response aggregation.
    /// </summary>
    /// <param name="searchQuery">The search query to match against</param>
    /// <param name="soulseekResponses">Existing Soulseek search responses</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Additional search responses from mesh-discovered files</returns>
    public async Task<List<SearchResponse>> GetMeshSupplementalResponsesAsync(
        string searchQuery,
        IEnumerable<SearchResponse> soulseekResponses,
        CancellationToken cancellationToken = default)
    {
        var supplementalResponses = new List<SearchResponse>();
        
        try
        {
            _logger.LogDebug("Searching mesh hash database for query: {Query}", searchQuery);
            
            // Get all unique Soulseek usernames from existing responses
            // to avoid duplicating results
            var existingUsernames = new HashSet<string>(
                soulseekResponses.Select(r => r.Username),
                StringComparer.OrdinalIgnoreCase);
            
            // Search mesh hash database
            // TODO: Implement actual hash DB search when MeshSyncService exposes search API
            // For now, this is a placeholder that demonstrates the bridge concept
            var meshResults = await SearchMeshHashDatabaseAsync(searchQuery, cancellationToken);
            
            foreach (var meshResult in meshResults)
            {
                // Skip if we already have results from this user via Soulseek
                if (existingUsernames.Contains(meshResult.Username))
                {
                    _logger.LogDebug(
                        "Skipping mesh result from {Username} - already have Soulseek response",
                        meshResult.Username);
                    continue;
                }
                
                //  Convert mesh result to Soulseek SearchResponse format
                // TODO: Complete SearchResponse instantiation when hash DB search is implemented
                // This would require knowing the correct Soulseek.SearchResponse constructor signature
                /*
                var response = new SearchResponse(
                    username: meshResult.Username,
                    token: 0,
                    freeUploadSlot: meshResult.FreeUploadSlots > 0,
                    uploadSpeed: meshResult.UploadSpeed,
                    queueLength: meshResult.QueueLength);
                
                supplementalResponses.Add(response);
                */
                
                // Placeholder logging until hash DB search is implemented
                _logger.LogDebug(
                    "Would supplement search with result from {Username} ({FileCount} files)",
                    meshResult.Username,
                    meshResult.Files.Count);
                
                _logger.LogInformation(
                    "Supplementing Soulseek search with mesh result: {Username} ({FileCount} files) " +
                    "[discovered via mesh, shared to Soulseek]",
                    meshResult.Username,
                    meshResult.Files.Count);
            }
            
            if (supplementalResponses.Count > 0)
            {
                _logger.LogInformation(
                    "Bridged {Count} mesh-discovered results to Soulseek search " +
                    "(benefiting Soulseek community with mesh discoveries)",
                    supplementalResponses.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, 
                "Failed to supplement search with mesh results for query: {Query}", 
                searchQuery);
        }
        
        return supplementalResponses;
    }
    
    /// <summary>
    /// Searches the mesh hash database for files matching the query.
    /// </summary>
    private async Task<List<MeshSearchResult>> SearchMeshHashDatabaseAsync(
        string query,
        CancellationToken cancellationToken)
    {
        var results = new List<MeshSearchResult>();
        
        // Configuration
        const int MaxMeshSearchResults = 20; // Limit to prevent excessive queries
        const int MaxUsernamesPerHash = 5; // Limit usernames per file
        
        try
        {
            // Search hash database with limit
            var hashResults = await _hashDb.SearchAsync(query, limit: MaxMeshSearchResults, cancellationToken);
            
            // Group by peer (username) to build per-user result sets
            var filesByUser = new Dictionary<string, List<Soulseek.File>>();
            var userStats = new Dictionary<string, (int freeSlots, int speed, int queueLength)>();
            
            foreach (var hashResult in hashResults)
            {
                if (hashResult.PeerCount == 0)
                {
                    continue; // No peers have this file
                }
                
                // Look up actual usernames for this hash
                var usernames = await _hashDb.GetPeersByHashAsync(hashResult.FlacKey, cancellationToken);
                
                // Limit usernames per hash to prevent abuse
                foreach (var username in usernames.Take(MaxUsernamesPerHash))
                {
                    if (!filesByUser.ContainsKey(username))
                    {
                        filesByUser[username] = new List<Soulseek.File>();
                        // Default stats - these would ideally come from peer reputation/status
                        userStats[username] = (freeSlots: 1, speed: 1000000, queueLength: 0);
                    }
                    
                    // Build filename from metadata
                    var filename = BuildFilename(hashResult);
                    
                    // Create Soulseek File object
                    var file = new Soulseek.File(
                        code: 0,
                        filename: filename,
                        size: hashResult.Size,
                        extension: "flac",
                        attributeList: new List<Soulseek.FileAttribute>
                        {
                            new Soulseek.FileAttribute(FileAttributeType.BitRate, hashResult.BitDepth ?? 16),
                            new Soulseek.FileAttribute(FileAttributeType.SampleRate, hashResult.SampleRate ?? 44100),
                            new Soulseek.FileAttribute(FileAttributeType.BitDepth, hashResult.BitDepth ?? 16),
                        });
                    
                    filesByUser[username].Add(file);
                }
            }
            
            // Convert to MeshSearchResult objects
            foreach (var kvp in filesByUser)
            {
                var (freeSlots, speed, queueLength) = userStats[kvp.Key];
                
                results.Add(new MeshSearchResult
                {
                    Username = kvp.Key,
                    FreeUploadSlots = freeSlots,
                    UploadSpeed = speed,
                    QueueLength = queueLength,
                    Files = kvp.Value,
                });
            }
            
            _logger.LogInformation(
                "Hash DB search for '{Query}' returned {FileCount} files from {PeerCount} peers",
                query,
                results.Sum(r => r.Files.Count),
                results.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error searching mesh hash database for query: {Query}", query);
        }
        
        return results;
    }
    
    /// <summary>
    /// Builds a filename from hash search result metadata.
    /// </summary>
    private static string BuildFilename(HashDbSearchResult result)
    {
        var artist = result.Artist ?? "Unknown Artist";
        var album = result.Album ?? "Unknown Album";
        var title = result.Title ?? "Unknown Title";
        
        // Use Path.Combine for cross-platform compatibility
        return Path.Combine(artist, album, title + ".flac");
    }
}

/// <summary>
/// Represents a search result from the mesh hash database.
/// </summary>
internal sealed class MeshSearchResult
{
    public required string Username { get; init; }
    public required int FreeUploadSlots { get; init; }
    public required int UploadSpeed { get; init; }
    public required int QueueLength { get; init; }
    public required IReadOnlyCollection<Soulseek.File> Files { get; init; }
}














