// <copyright file="IContentLinkService.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
// </copyright>

namespace slskd.PodCore;

using System.Threading;
using System.Threading.Tasks;

/// <summary>
///     Service for validating and resolving content links for pod creation.
/// </summary>
public interface IContentLinkService
{
    /// <summary>
    ///     Validates that a content ID is properly formatted and resolvable.
    /// </summary>
    /// <param name="contentId">The content ID to validate.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>Validation result with metadata if valid.</returns>
    Task<ContentValidationResult> ValidateContentIdAsync(string contentId, CancellationToken ct = default);

    /// <summary>
    ///     Gets metadata for a validated content ID.
    /// </summary>
    /// <param name="contentId">The content ID.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>Content metadata or null if not found.</returns>
    Task<ContentMetadata?> GetContentMetadataAsync(string contentId, CancellationToken ct = default);

    /// <summary>
    ///     Searches for content that can be linked to pods.
    /// </summary>
    /// <param name="query">The search query.</param>
    /// <param name="domain">Optional domain filter (audio, video, etc.).</param>
    /// <param name="limit">Maximum number of results.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>List of searchable content items.</returns>
    Task<IReadOnlyList<ContentSearchResult>> SearchContentAsync(string query, string domain = null, int limit = 20, CancellationToken ct = default);
}

/// <summary>
///     Result of content ID validation.
/// </summary>
public record ContentValidationResult(
    bool IsValid,
    string ContentId,
    string? ErrorMessage = null,
    ContentMetadata? Metadata = null);

/// <summary>
///     Metadata for a content item.
/// </summary>
public record ContentMetadata(
    string ContentId,
    string Title,
    string Artist,
    string Type,
    string Domain,
    Dictionary<string, string> AdditionalInfo = null);

/// <summary>
///     Result of content search.
/// </summary>
public record ContentSearchResult(
    string ContentId,
    string Title,
    string Subtitle,
    string Type,
    string Domain,
    Dictionary<string, string> Metadata = null);
