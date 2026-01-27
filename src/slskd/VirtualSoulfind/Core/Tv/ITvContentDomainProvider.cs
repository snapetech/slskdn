// <copyright file="ITvContentDomainProvider.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.VirtualSoulfind.Core.Tv;

using System;
using System.Threading;
using System.Threading.Tasks;
using slskd.Common.Moderation;
using slskd.VirtualSoulfind.Core;

/// <summary>
///     Interface for the TV domain provider in VirtualSoulfind v2.
/// </summary>
/// <remarks>
///     Handles TV show/episode content with TVDB ID-based matching.
///     NO Soulseek backend - uses mesh/DHT/torrent/HTTP/local only.
/// </remarks>
public interface ITvContentDomainProvider
{
    /// <summary>
    ///     Attempts to resolve a TV series by TVDB ID.
    /// </summary>
    Task<TvWork?> TryGetWorkByTvdbIdAsync(string tvdbId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Attempts to resolve a TV series by title.
    /// </summary>
    Task<TvWork?> TryGetWorkByTitleAsync(string title, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Attempts to resolve a TV episode by series, season, and episode number.
    /// </summary>
    Task<TvItem?> TryGetItemByEpisodeAsync(
        string seriesId,
        int season,
        int episode,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Attempts to resolve a TV item by hash and filename.
    /// </summary>
    Task<TvItem?> TryGetItemByHashAsync(string hash, string filename, long sizeBytes, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Attempts to resolve a TV item by local file metadata.
    /// </summary>
    Task<TvItem?> TryGetItemByLocalMetadataAsync(LocalFileMetadata fileMetadata, CancellationToken cancellationToken = default);
}

/// <summary>
///     TV work (series).
/// </summary>
public class TvWork : IContentWork
{
    public TvWork(ContentWorkId id, string title, int? year = null, string? tvdbId = null, string? creator = null)
    {
        Id = id;
        Title = title ?? throw new ArgumentNullException(nameof(title));
        Year = year;
        TvdbId = tvdbId;
        Creator = creator;
    }

    /// <inheritdoc/>
    public ContentWorkId Id { get; }

    /// <inheritdoc/>
    public ContentDomain Domain => ContentDomain.Tv;

    /// <inheritdoc/>
    public string Title { get; }

    /// <inheritdoc/>
    public string? Creator { get; }

    /// <inheritdoc/>
    public int? Year { get; }

    /// <summary>
    ///     Gets the TVDB identifier.
    /// </summary>
    public string? TvdbId { get; }

    /// <summary>
    ///     Gets the work ID as a string (for backward compatibility).
    /// </summary>
    public string WorkId => Id.Value.ToString();
}

/// <summary>
///     TV item (episode).
/// </summary>
public class TvItem : IContentItem
{
    public TvItem(ContentItemId id, ContentWorkId workId, string seriesTitle, int season, int episode, string filename, long fileSize, string? hash = null, string? tvdbId = null, bool isAdvertisable = false)
    {
        Id = id;
        WorkId = workId;
        SeriesTitle = seriesTitle ?? throw new ArgumentNullException(nameof(seriesTitle));
        Season = season;
        Episode = episode;
        Filename = filename ?? throw new ArgumentNullException(nameof(filename));
        FileSize = fileSize;
        Hash = hash;
        TvdbId = tvdbId;
        IsAdvertisable = isAdvertisable;
    }

    /// <inheritdoc/>
    public ContentItemId Id { get; }

    /// <inheritdoc/>
    public ContentDomain Domain => ContentDomain.Tv;

    /// <inheritdoc/>
    public ContentWorkId? WorkId { get; }

    /// <inheritdoc/>
    public string Title => $"{SeriesTitle} S{Season:D2}E{Episode:D2}";

    /// <inheritdoc/>
    public int? Position => Episode; // Episode number is the position

    /// <inheritdoc/>
    public TimeSpan? Duration => null; // Duration would need to be extracted from metadata

    /// <inheritdoc/>
    public bool IsAdvertisable { get; }

    /// <summary>
    ///     Gets the series title.
    /// </summary>
    public string SeriesTitle { get; }

    /// <summary>
    ///     Gets the season number.
    /// </summary>
    public int Season { get; }

    /// <summary>
    ///     Gets the episode number.
    /// </summary>
    public int Episode { get; }

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
    ///     Gets the TVDB identifier.
    /// </summary>
    public string? TvdbId { get; }

    /// <summary>
    ///     Gets the item ID as a string (for backward compatibility).
    /// </summary>
    public string ItemId => Id.Value.ToString();
}
