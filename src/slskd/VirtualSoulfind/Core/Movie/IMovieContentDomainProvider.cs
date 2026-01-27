// <copyright file="IMovieContentDomainProvider.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.VirtualSoulfind.Core.Movie;

using System;
using System.Threading;
using System.Threading.Tasks;
using slskd.Common.Moderation;
using slskd.VirtualSoulfind.Core;

/// <summary>
///     Interface for the Movie domain provider in VirtualSoulfind v2.
/// </summary>
/// <remarks>
///     Handles movie content with IMDB ID-based matching and hash verification.
///     NO Soulseek backend - uses mesh/DHT/torrent/HTTP/local only.
/// </remarks>
public interface IMovieContentDomainProvider
{
    /// <summary>
    ///     Attempts to resolve a movie by IMDB ID.
    /// </summary>
    Task<MovieWork?> TryGetWorkByImdbIdAsync(string imdbId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Attempts to resolve a movie by title and year.
    /// </summary>
    Task<MovieWork?> TryGetWorkByTitleYearAsync(string title, int? year, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Attempts to resolve a movie item by hash and filename.
    /// </summary>
    Task<MovieItem?> TryGetItemByHashAsync(string hash, string filename, long sizeBytes, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Attempts to resolve a movie item by local file metadata.
    /// </summary>
    Task<MovieItem?> TryGetItemByLocalMetadataAsync(LocalFileMetadata fileMetadata, CancellationToken cancellationToken = default);
}

/// <summary>
///     Movie work (film).
/// </summary>
public class MovieWork : IContentWork
{
    public MovieWork(ContentWorkId id, string title, int? year = null, string? imdbId = null, string? director = null)
    {
        Id = id;
        Title = title ?? throw new ArgumentNullException(nameof(title));
        Year = year;
        ImdbId = imdbId;
        Creator = director;
    }

    /// <inheritdoc/>
    public ContentWorkId Id { get; }

    /// <inheritdoc/>
    public ContentDomain Domain => ContentDomain.Movie;

    /// <inheritdoc/>
    public string Title { get; }

    /// <inheritdoc/>
    public string? Creator { get; }

    /// <inheritdoc/>
    public int? Year { get; }

    /// <summary>
    ///     Gets the IMDB identifier.
    /// </summary>
    public string? ImdbId { get; }

    /// <summary>
    ///     Gets the work ID as a string (for backward compatibility).
    /// </summary>
    public string WorkId => Id.Value.ToString();
}

/// <summary>
///     Movie item (specific encoding/edition).
/// </summary>
public class MovieItem : IContentItem
{
    public MovieItem(ContentItemId id, ContentWorkId workId, string filename, long fileSize, string? hash = null, string? imdbId = null, bool isAdvertisable = false)
    {
        Id = id;
        WorkId = workId;
        Filename = filename ?? throw new ArgumentNullException(nameof(filename));
        FileSize = fileSize;
        Hash = hash;
        ImdbId = imdbId;
        IsAdvertisable = isAdvertisable;
    }

    /// <inheritdoc/>
    public ContentItemId Id { get; }

    /// <inheritdoc/>
    public ContentDomain Domain => ContentDomain.Movie;

    /// <inheritdoc/>
    public ContentWorkId? WorkId { get; }

    /// <inheritdoc/>
    public string Title => Filename;

    /// <inheritdoc/>
    public int? Position => null; // Movies don't have positions

    /// <inheritdoc/>
    public TimeSpan? Duration => null; // Duration would need to be extracted from metadata

    /// <inheritdoc/>
    public bool IsAdvertisable { get; }

    /// <summary>
    ///     Gets the filename.
    /// </summary>
    public string Filename { get; }

    /// <summary>
    ///     Gets the file size in bytes.
    /// </summary>
    public long FileSize { get; }

    /// <summary>
    ///     Gets the hash (SHA256).
    /// </summary>
    public string? Hash { get; }

    /// <summary>
    ///     Gets the IMDB identifier.
    /// </summary>
    public string? ImdbId { get; }

    /// <summary>
    ///     Gets the item ID as a string (for backward compatibility).
    /// </summary>
    public string ItemId => Id.Value.ToString();
}
