// <copyright file="IHashDbService.Search.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.HashDb;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using slskd.HashDb.Models;

/// <summary>
/// Search-related methods for hash database.
/// </summary>
public partial interface IHashDbService
{
    /// <summary>
    /// Searches the hash database for entries matching the query.
    /// </summary>
    /// <param name="query">Search query (artist, album, track name, etc.)</param>
    /// <param name="limit">Maximum results to return</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Matching hash entries with metadata</returns>
    Task<IEnumerable<HashDbSearchResult>> SearchAsync(
        string query, 
        int limit = 100, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets usernames of peers who have a specific FLAC hash.
    /// </summary>
    /// <param name="flacKey">FLAC key hash</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of usernames who have this file</returns>
    Task<IEnumerable<string>> GetPeersByHashAsync(
        string flacKey,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result from hash database search.
/// </summary>
public sealed class HashDbSearchResult
{
    /// <summary>FLAC key (hash).</summary>
    public required string FlacKey { get; init; }
    
    /// <summary>File size in bytes.</summary>
    public required long Size { get; init; }
    
    /// <summary>Artist name (if known).</summary>
    public string? Artist { get; init; }
    
    /// <summary>Album title (if known).</summary>
    public string? Album { get; init; }
    
    /// <summary>Track title (if known).</summary>
    public string? Title { get; init; }
    
    /// <summary>Recording ID (MusicBrainz).</summary>
    public string? RecordingId { get; init; }
    
    /// <summary>Release ID (MusicBrainz).</summary>
    public string? ReleaseId { get; init; }
    
    /// <summary>Number of peers with this file.</summary>
    public int PeerCount { get; init; }
    
    /// <summary>Sample rate (if known).</summary>
    public int? SampleRate { get; init; }
    
    /// <summary>Bit depth (if known).</summary>
    public int? BitDepth { get; init; }
    
    /// <summary>Channels (if known).</summary>
    public int? Channels { get; init; }
}















