// <copyright file="IMeshContentFetcher.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Streaming;

using System.IO;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Fetches content from mesh overlay network by ContentId with size and hash validation.
/// Used as a facade for mesh content retrieval with integrity checks.
/// </summary>
public interface IMeshContentFetcher
{
    /// <summary>
    /// Fetches content from a mesh peer by ContentId.
    /// </summary>
    /// <param name="peerId">Peer ID hosting the content.</param>
    /// <param name="contentId">Content identifier.</param>
    /// <param name="expectedSize">Expected file size in bytes (optional, for validation).</param>
    /// <param name="expectedHash">Expected SHA-256 hash (optional, for validation).</param>
    /// <param name="offset">Byte offset to start reading from (for range requests).</param>
    /// <param name="length">Number of bytes to read (0 = read to end).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Fetched content result with validation status.</returns>
    Task<MeshContentFetchResult> FetchAsync(
        string peerId,
        string contentId,
        long? expectedSize = null,
        string? expectedHash = null,
        long offset = 0,
        int length = 0,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of fetching content from mesh.
/// </summary>
public sealed class MeshContentFetchResult
{
    /// <summary>
    /// Content data stream. Dispose after use.
    /// </summary>
    public Stream? Data { get; set; }

    /// <summary>
    /// Actual size of the fetched content in bytes.
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// Actual SHA-256 hash of the content (if available).
    /// </summary>
    public string? Hash { get; set; }

    /// <summary>
    /// Whether size validation passed (if expectedSize was provided).
    /// </summary>
    public bool SizeValid { get; set; }

    /// <summary>
    /// Whether hash validation passed (if expectedHash was provided).
    /// </summary>
    public bool HashValid { get; set; }

    /// <summary>
    /// Error message if fetch failed.
    /// </summary>
    public string? Error { get; set; }
}
