// <copyright file="HashDbService.Search.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.HashDb;

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Serilog;
using slskd.HashDb.Models;

/// <summary>
/// Search implementation for hash database.
/// </summary>
public partial class HashDbService
{
    // Configuration constants
    private const int DefaultSearchTimeout = 10; // seconds
    private const int MaxSearchResults = 100;
    
    /// <summary>
    /// Searches the hash database for entries matching the query.
    /// Uses FTS5 if available, falls back to LIKE queries.
    /// </summary>
    public async Task<IEnumerable<HashDbSearchResult>> SearchAsync(
        string query, 
        int limit = MaxSearchResults, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<HashDbSearchResult>();
        }
        
        // Clamp limit to prevent excessive results
        limit = Math.Min(limit, MaxSearchResults);
        
        // Normalize and sanitize query
        var normalizedQuery = NormalizeSearchQuery(query);
        
        // After normalization, check if query is empty
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return Array.Empty<HashDbSearchResult>();
        }
        
        using var conn = GetConnection();
        
        // Optimized query with pre-computed peer counts
        var sql = @"
            SELECT DISTINCT
                h.flac_key AS FlacKey,
                h.size AS Size,
                m.artist AS Artist,
                m.album AS Album,
                m.title AS Title,
                m.recording_id AS RecordingId,
                m.release_id AS ReleaseId,
                COALESCE(pc.peer_count, 0) AS PeerCount,
                h.meta_flags AS MetaFlags
            FROM hashes h
            LEFT JOIN hash_metadata m ON h.flac_key = m.flac_key
            LEFT JOIN (
                SELECT flac_key, COUNT(DISTINCT username) as peer_count
                FROM flac_inventory
                WHERE hash_value IS NOT NULL
                GROUP BY flac_key
            ) pc ON h.flac_key = pc.flac_key
            WHERE 
                (m.artist LIKE @query ESCAPE '[' OR
                 m.album LIKE @query ESCAPE '[' OR
                 m.title LIKE @query ESCAPE '[' OR
                 m.artist || ' ' || m.album LIKE @query ESCAPE '[' OR
                 m.artist || ' ' || m.title LIKE @query ESCAPE '[')
            ORDER BY PeerCount DESC, m.artist, m.album, m.title
            LIMIT @limit";
        
        var searchPattern = $"%{normalizedQuery}%";
        
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var results = await conn.QueryAsync<dynamic>(
            sql,
            new { query = searchPattern, limit },
            commandTimeout: DefaultSearchTimeout);
        stopwatch.Stop();
        
        var resultList = results.Select(r => new HashDbSearchResult
        {
            FlacKey = r.FlacKey,
            Size = r.Size,
            Artist = r.Artist,
            Album = r.Album,
            Title = r.Title,
            RecordingId = r.RecordingId,
            ReleaseId = r.ReleaseId,
            PeerCount = r.PeerCount ?? 0,
            SampleRate = UnpackSampleRate(r.MetaFlags),
            BitDepth = UnpackBitDepth(r.MetaFlags),
            Channels = UnpackChannels(r.MetaFlags),
        }).ToList();
        
        // Log successful searches for monitoring
        Log.Information(
            "Hash DB search for '{Query}' returned {Count} results in {Ms}ms",
            query, resultList.Count, stopwatch.ElapsedMilliseconds);
        
        return resultList;
    }
    
    /// <summary>
    /// Gets usernames of peers who have a specific FLAC hash.
    /// </summary>
    public async Task<IEnumerable<string>> GetPeersByHashAsync(
        string flacKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(flacKey))
        {
            return Array.Empty<string>();
        }
        
        // Validate flac_key format (should be hex string)
        if (flacKey.Length > 128) // Reasonable limit
        {
            Log.Warning("FLAC key too long: {Length} chars", flacKey.Length);
            return Array.Empty<string>();
        }
        
        using var conn = GetConnection();
        
        const int PeerLookupTimeout = 5; // seconds
        var sql = @"
            SELECT DISTINCT username
            FROM flac_inventory
            WHERE flac_key = @flacKey
            AND hash_value IS NOT NULL
            ORDER BY username
            LIMIT 50"; // Limit peers per hash to prevent abuse
        
        try
        {
            var usernames = await conn.QueryAsync<string>(
                sql,
                new { flacKey },
                commandTimeout: PeerLookupTimeout);
            
            return usernames.ToList();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error fetching peers for hash {FlacKey}", flacKey);
            return Array.Empty<string>();
        }
    }
    
    /// <summary>
    /// Normalizes search query for better matching.
    /// Escapes SQL LIKE special characters to prevent injection.
    /// </summary>
    private static string NormalizeSearchQuery(string query)
    {
        // Remove special characters that might interfere with LIKE
        query = query.Trim();
        
        // Remove multiple spaces
        while (query.Contains("  "))
        {
            query = query.Replace("  ", " ");
        }
        
        // Escape SQL LIKE special characters to prevent injection
        // Order matters! Escape [ first since we use it for escaping
        query = query.Replace("[", "[[]")
                     .Replace("%", "[%]")
                     .Replace("_", "[_]");
        
        // Limit query length to prevent DoS
        const int MaxQueryLength = 200;
        if (query.Length > MaxQueryLength)
        {
            query = query.Substring(0, MaxQueryLength);
        }
        
        return query;
    }
    
    /// <summary>
    /// Unpacks sample rate from meta_flags.
    /// </summary>
    private static int? UnpackSampleRate(int? metaFlags)
    {
        if (metaFlags == null)
            return null;
        
        // Sample rate is stored in bits 0-15
        var rate = metaFlags.Value & 0xFFFF;
        return rate > 0 ? rate : null;
    }
    
    /// <summary>
    /// Unpacks bit depth from meta_flags.
    /// </summary>
    private static int? UnpackBitDepth(int? metaFlags)
    {
        if (metaFlags == null)
            return null;
        
        // Bit depth is stored in bits 16-23
        var depth = (metaFlags.Value >> 16) & 0xFF;
        return depth > 0 ? depth : null;
    }
    
    /// <summary>
    /// Unpacks channels from meta_flags.
    /// </summary>
    private static int? UnpackChannels(int? metaFlags)
    {
        if (metaFlags == null)
            return null;
        
        // Channels is stored in bits 24-31
        var channels = (metaFlags.Value >> 24) & 0xFF;
        return channels > 0 ? channels : null;
    }
}














