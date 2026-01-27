// <copyright file="IContentDomainProvider.cs" company="slskdN Team">
// Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.VirtualSoulfind.Core;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
///     Base interface for all content domain providers.
/// </summary>
/// <remarks>
///     <para>
///         This interface provides a common contract for all domain providers,
///         enabling extensibility and plugin architecture for custom domains.
///     </para>
///     <para>
///         Domain providers are responsible for:
///         - Identity mapping (external IDs → ContentWorkId/ContentItemId)
///         - Metadata enrichment
///         - Content verification
///         - Quality assessment
///     </para>
///     <para>
///         Built-in providers:
///         - <see cref="Music.IMusicContentDomainProvider"/> - Music domain
///         - <see cref="Book.IBookContentDomainProvider"/> - Book domain
///         - <see cref="Movie.IMovieContentDomainProvider"/> - Movie domain
///         - <see cref="Tv.ITvContentDomainProvider"/> - TV domain
///         - <see cref="GenericFile.IGenericFileContentDomainProvider"/> - Generic file domain
///     </para>
/// </remarks>
public interface IContentDomainProvider
{
    /// <summary>
    ///     Gets the content domain this provider handles.
    /// </summary>
    ContentDomain Domain { get; }

    /// <summary>
    ///     Gets the display name of this domain provider.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    ///     Gets the version of this domain provider.
    /// </summary>
    string Version { get; }

    /// <summary>
    ///     Gets whether this is a built-in provider (true) or a custom/plugin provider (false).
    /// </summary>
    bool IsBuiltIn { get; }

    /// <summary>
    ///     Gets the supported backend types for this domain.
    /// </summary>
    /// <remarks>
    ///     Returns a list of backend types that are allowed for this domain.
    ///     For example, Music domain supports Soulseek, while Book domain does not.
    /// </remarks>
    IReadOnlyList<v2.Backends.ContentBackendType> SupportedBackendTypes { get; }

    /// <summary>
    ///     Attempts to resolve a work by domain-specific identifier.
    /// </summary>
    /// <param name="identifier">Domain-specific identifier (e.g., MBID, ISBN, IMDB ID).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The resolved work, or null if not found.</returns>
    Task<IContentWork?> TryGetWorkByIdentifierAsync(string identifier, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Attempts to resolve a work by title and creator.
    /// </summary>
    /// <param name="title">The work title.</param>
    /// <param name="creator">The creator/artist/author name.</param>
    /// <param name="year">Optional year for disambiguation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The resolved work, or null if not found.</returns>
    Task<IContentWork?> TryGetWorkByTitleCreatorAsync(string title, string? creator = null, int? year = null, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Attempts to resolve an item by domain-specific identifier.
    /// </summary>
    /// <param name="identifier">Domain-specific identifier (e.g., Recording ID, ISBN, IMDB ID).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The resolved item, or null if not found.</returns>
    Task<IContentItem?> TryGetItemByIdentifierAsync(string identifier, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Attempts to resolve an item by hash and metadata.
    /// </summary>
    /// <param name="hash">SHA256 hash of the file.</param>
    /// <param name="sizeBytes">File size in bytes.</param>
    /// <param name="filename">Filename (optional, for format detection).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The resolved item, or null if not found.</returns>
    Task<IContentItem?> TryGetItemByHashAsync(string hash, long sizeBytes, string? filename = null, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Attempts to resolve an item by local file metadata.
    /// </summary>
    /// <param name="fileMetadata">Local file metadata.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The resolved item, or null if not found.</returns>
    Task<IContentItem?> TryGetItemByLocalMetadataAsync(Common.Moderation.LocalFileMetadata fileMetadata, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Verifies whether a file matches a specific content item.
    /// </summary>
    /// <param name="itemId">The content item to verify against.</param>
    /// <param name="fileMetadata">Local file metadata.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the file matches the item, false otherwise.</returns>
    Task<bool> VerifyItemMatchAsync(ContentItemId itemId, Common.Moderation.LocalFileMetadata fileMetadata, CancellationToken cancellationToken = default);
}
