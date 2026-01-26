// <copyright file="IContentLocator.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Streaming;

/// <summary>
/// Resolves a content ID to a local file path and metadata. Only for files in the indexed share library.
/// Never accepts client-supplied paths. Used by streaming and share manifest.
/// </summary>
public interface IContentLocator
{
    /// <summary>
    /// Resolves contentId to a local path and metadata. Returns null if not found, not advertisable, or file missing on disk.
    /// </summary>
    /// <param name="contentId">Content identifier (must map to share repository content_items).</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Resolved content or null.</returns>
    ResolvedContent? Resolve(string contentId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of resolving a content ID: absolute path, length, and MIME type.
/// </summary>
public sealed record ResolvedContent(string AbsolutePath, long Length, string ContentType);
