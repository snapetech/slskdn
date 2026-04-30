// <copyright file="TvContentDomainProvider.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.VirtualSoulfind.Core.Tv;

using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using slskd.Common.Moderation;

/// <summary>
///     TV domain provider implementation.
/// </summary>
public class TvContentDomainProvider : ITvContentDomainProvider
{
    private readonly ILogger<TvContentDomainProvider> _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="TvContentDomainProvider"/> class.
    /// </summary>
    public TvContentDomainProvider(ILogger<TvContentDomainProvider> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task<TvWork?> TryGetWorkByTvdbIdAsync(string tvdbId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tvdbId))
        {
            return Task.FromResult<TvWork?>(null);
        }

        _logger.LogDebug("[TvDomain] Resolving TV series by TVDB ID: {TvdbId}", tvdbId);
        var work = new TvWork(TvDomainMapping.TvdbIdToContentWorkId(tvdbId), $"TVDB {tvdbId}", tvdbId: tvdbId);
        return Task.FromResult<TvWork?>(work);
    }

    /// <inheritdoc/>
    public Task<TvWork?> TryGetWorkByTitleAsync(string title, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return Task.FromResult<TvWork?>(null);
        }

        _logger.LogDebug("[TvDomain] Resolving TV series by title: {Title}", title);
        var tvdbSeed = Math.Abs(StringComparer.OrdinalIgnoreCase.GetHashCode(title)).ToString();
        var work = new TvWork(TvDomainMapping.TvdbIdToContentWorkId(tvdbSeed), title.Trim(), tvdbId: tvdbSeed);
        return Task.FromResult<TvWork?>(work);
    }

    /// <inheritdoc/>
    public Task<TvItem?> TryGetItemByEpisodeAsync(
        string seriesId,
        int season,
        int episode,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(seriesId) || season < 0 || episode <= 0)
        {
            return Task.FromResult<TvItem?>(null);
        }

        _logger.LogDebug("[TvDomain] Resolving TV episode: Series {SeriesId}, S{Season}E{Episode}", seriesId, season, episode);
        var workId = TvDomainMapping.TvdbIdToContentWorkId(seriesId);
        var itemId = TvDomainMapping.EpisodeToContentItemId(seriesId, season, episode);
        var item = new TvItem(itemId, workId, seriesId, season, episode, $"{seriesId}.S{season:D2}E{episode:D2}", 0, tvdbId: seriesId);
        return Task.FromResult<TvItem?>(item);
    }

    /// <inheritdoc/>
    public Task<TvItem?> TryGetItemByHashAsync(string hash, string filename, long sizeBytes, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(hash) || string.IsNullOrWhiteSpace(filename) || sizeBytes <= 0)
        {
            return Task.FromResult<TvItem?>(null);
        }

        var normalizedHash = hash.Trim();
        var normalizedFileName = filename.Trim();
        _logger.LogDebug("[TvDomain] Resolving TV item by hash: {Hash}", normalizedHash.Substring(0, Math.Min(16, normalizedHash.Length)));
        var parsed = ParseEpisode(Path.GetFileNameWithoutExtension(normalizedFileName) ?? normalizedFileName);
        if (parsed == null)
        {
            return Task.FromResult<TvItem?>(null);
        }

        var workId = TvDomainMapping.TvdbIdToContentWorkId(parsed.Value.Series);
        var item = new TvItem(
            TvDomainMapping.HashToContentItemId(normalizedHash, sizeBytes, normalizedFileName),
            workId,
            parsed.Value.Series,
            parsed.Value.Season,
            parsed.Value.Episode,
            normalizedFileName,
            sizeBytes,
            normalizedHash,
            isAdvertisable: true);
        return Task.FromResult<TvItem?>(item);
    }

    /// <inheritdoc/>
    public Task<TvItem?> TryGetItemByLocalMetadataAsync(LocalFileMetadata fileMetadata, CancellationToken cancellationToken = default)
    {
        if (fileMetadata == null || string.IsNullOrWhiteSpace(fileMetadata.Id) || fileMetadata.SizeBytes <= 0)
        {
            return Task.FromResult<TvItem?>(null);
        }

        _logger.LogDebug("[TvDomain] Resolving TV item by local metadata: {Id}", fileMetadata.Id);
        var hash = string.IsNullOrWhiteSpace(fileMetadata.PrimaryHash) ? fileMetadata.Id : fileMetadata.PrimaryHash;
        return TryGetItemByHashAsync(hash, fileMetadata.Id, fileMetadata.SizeBytes, cancellationToken);
    }

    private static (string Series, int Season, int Episode)? ParseEpisode(string value)
    {
        var match = Regex.Match(value, @"^(?<series>.+?)[\s._-]+S(?<season>\d{1,2})E(?<episode>\d{1,2})(\b|[\s._-])", RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            return null;
        }

        return (
            match.Groups["series"].Value.Replace('.', ' ').Replace('_', ' ').Trim(),
            int.Parse(match.Groups["season"].Value),
            int.Parse(match.Groups["episode"].Value));
    }
}
