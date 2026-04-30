// <copyright file="MovieContentDomainProvider.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.VirtualSoulfind.Core.Movie;

using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using slskd.Common.Moderation;

/// <summary>
///     Movie domain provider implementation.
/// </summary>
public class MovieContentDomainProvider : IMovieContentDomainProvider
{
    private readonly ILogger<MovieContentDomainProvider> _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="MovieContentDomainProvider"/> class.
    /// </summary>
    public MovieContentDomainProvider(ILogger<MovieContentDomainProvider> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task<MovieWork?> TryGetWorkByImdbIdAsync(string imdbId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(imdbId))
        {
            return Task.FromResult<MovieWork?>(null);
        }

        _logger.LogDebug("[MovieDomain] Resolving movie by IMDB ID: {ImdbId}", imdbId);
        var normalized = imdbId.StartsWith("tt", StringComparison.OrdinalIgnoreCase) ? imdbId : $"tt{imdbId}";
        var work = new MovieWork(MovieDomainMapping.ImdbIdToContentWorkId(normalized), normalized, imdbId: normalized);
        return Task.FromResult<MovieWork?>(work);
    }

    /// <inheritdoc/>
    public Task<MovieWork?> TryGetWorkByTitleYearAsync(string title, int? year, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return Task.FromResult<MovieWork?>(null);
        }

        _logger.LogDebug("[MovieDomain] Resolving movie by title/year: {Title} ({Year})", title, year);
        var seed = $"tt{Math.Abs(StringComparer.OrdinalIgnoreCase.GetHashCode($"{title}:{year}"))}";
        var work = new MovieWork(MovieDomainMapping.ImdbIdToContentWorkId(seed), title.Trim(), year);
        return Task.FromResult<MovieWork?>(work);
    }

    /// <inheritdoc/>
    public Task<MovieItem?> TryGetItemByHashAsync(string hash, string filename, long sizeBytes, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(hash) || string.IsNullOrWhiteSpace(filename) || sizeBytes <= 0)
        {
            return Task.FromResult<MovieItem?>(null);
        }

        var normalizedHash = hash.Trim();
        var normalizedFileName = filename.Trim();
        _logger.LogDebug("[MovieDomain] Resolving movie item by hash: {Hash}", normalizedHash.Substring(0, Math.Min(16, normalizedHash.Length)));
        var work = ParseTitleYear(Path.GetFileNameWithoutExtension(normalizedFileName) ?? normalizedFileName);
        var workId = MovieDomainMapping.ImdbIdToContentWorkId($"tt{Math.Abs(StringComparer.OrdinalIgnoreCase.GetHashCode($"{work.Title}:{work.Year}"))}");
        var item = new MovieItem(
            MovieDomainMapping.HashToContentItemId(normalizedHash, sizeBytes, normalizedFileName),
            workId,
            normalizedFileName,
            sizeBytes,
            normalizedHash,
            isAdvertisable: true);
        return Task.FromResult<MovieItem?>(item);
    }

    /// <inheritdoc/>
    public Task<MovieItem?> TryGetItemByLocalMetadataAsync(LocalFileMetadata fileMetadata, CancellationToken cancellationToken = default)
    {
        if (fileMetadata == null || string.IsNullOrWhiteSpace(fileMetadata.Id) || fileMetadata.SizeBytes <= 0)
        {
            return Task.FromResult<MovieItem?>(null);
        }

        _logger.LogDebug("[MovieDomain] Resolving movie item by local metadata: {Id}", fileMetadata.Id);
        var hash = string.IsNullOrWhiteSpace(fileMetadata.PrimaryHash) ? fileMetadata.Id : fileMetadata.PrimaryHash;
        return TryGetItemByHashAsync(hash, fileMetadata.Id, fileMetadata.SizeBytes, cancellationToken);
    }

    private static (string Title, int? Year) ParseTitleYear(string value)
    {
        var match = Regex.Match(value, @"^(?<title>.+?)[\s._-]+(?<year>(19|20)\d{2})(\b|[\s._-])");
        if (!match.Success)
        {
            return (value.Replace('.', ' ').Replace('_', ' ').Trim(), null);
        }

        return (
            match.Groups["title"].Value.Replace('.', ' ').Replace('_', ' ').Trim(),
            int.Parse(match.Groups["year"].Value));
    }
}
