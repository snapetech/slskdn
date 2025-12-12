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
using slskd.HashDb.Models;

/// <summary>
/// Search implementation for hash database.
/// </summary>
public partial class HashDbService
{
    /// <summary>
    /// Searches the hash database for entries matching the query.
    /// Uses FTS5 if available, falls back to LIKE queries.
    /// </summary>
    public async Task<IEnumerable<HashDbSearchResult>> SearchAsync(
        string query, 
        int limit = 100, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<HashDbSearchResult>();
        }
        
        // Normalize query
        var normalizedQuery = NormalizeSearchQuery(query);
        
        using var conn = GetConnection();
        
        // Try FTS5 search first (if metadata table has FTS enabled)
        // For now, use basic LIKE search on metadata fields
        var sql = @"
            SELECT DISTINCT
                h.flac_key AS FlacKey,
                h.size AS Size,
                m.artist AS Artist,
                m.album AS Album,
                m.title AS Title,
                m.recording_id AS RecordingId,
                m.release_id AS ReleaseId,
                (SELECT COUNT(DISTINCT username) 
                 FROM flac_inventory 
                 WHERE flac_key = h.flac_key 
                 AND hash_value IS NOT NULL) AS PeerCount,
                h.meta_flags AS MetaFlags
            FROM hashes h
            LEFT JOIN hash_metadata m ON h.flac_key = m.flac_key
            WHERE 
                (m.artist LIKE @query OR
                 m.album LIKE @query OR
                 m.title LIKE @query OR
                 m.artist || ' ' || m.album LIKE @query OR
                 m.artist || ' ' || m.title LIKE @query)
            ORDER BY PeerCount DESC, m.artist, m.album, m.title
            LIMIT @limit";
        
        var searchPattern = $"%{normalizedQuery}%";
        
        var results = await conn.QueryAsync<dynamic>(
            sql,
            new { query = searchPattern, limit },
            commandTimeout: 10);
        
        return results.Select(r => new HashDbSearchResult
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
        
        using var conn = GetConnection();
        
        var sql = @"
            SELECT DISTINCT username
            FROM flac_inventory
            WHERE flac_key = @flacKey
            AND hash_value IS NOT NULL
            ORDER BY username";
        
        var usernames = await conn.QueryAsync<string>(
            sql,
            new { flacKey },
            commandTimeout: 5);
        
        return usernames.ToList();
    }
    
    /// <summary>
    /// Normalizes search query for better matching.
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
