// <copyright file="BookContentDomainProvider.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.VirtualSoulfind.Core.Book;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using slskd.Common.Moderation;

/// <summary>
///     Book domain provider implementation.
/// </summary>
public class BookContentDomainProvider : IBookContentDomainProvider
{
    private readonly ILogger<BookContentDomainProvider> _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="BookContentDomainProvider"/> class.
    /// </summary>
    public BookContentDomainProvider(ILogger<BookContentDomainProvider> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task<BookWork?> TryGetWorkByIsbnAsync(string isbn, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(isbn))
        {
            return Task.FromResult<BookWork?>(null);
        }

        _logger.LogDebug("[BookDomain] Resolving book by ISBN: {Isbn}", isbn);
        var work = new BookWork(BookDomainMapping.IsbnToContentWorkId(isbn), $"ISBN {isbn}", isbn: isbn);
        return Task.FromResult<BookWork?>(work);
    }

    /// <inheritdoc/>
    public Task<BookWork?> TryGetWorkByTitleAuthorAsync(string title, string author, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return Task.FromResult<BookWork?>(null);
        }

        _logger.LogDebug("[BookDomain] Resolving book by title/author: {Title} by {Author}", title, author);
        var isbnSeed = $"{title}:{author}".Trim();
        var work = new BookWork(BookDomainMapping.IsbnToContentWorkId(isbnSeed), title.Trim(), string.IsNullOrWhiteSpace(author) ? null : author.Trim());
        return Task.FromResult<BookWork?>(work);
    }

    /// <inheritdoc/>
    public Task<BookItem?> TryGetItemByHashAsync(string hash, string filename, long sizeBytes, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(hash) || string.IsNullOrWhiteSpace(filename) || sizeBytes <= 0)
        {
            return Task.FromResult<BookItem?>(null);
        }

        var normalizedHash = hash.Trim();
        var normalizedFileName = filename.Trim();
        _logger.LogDebug("[BookDomain] Resolving book item by hash: {Hash}", normalizedHash.Substring(0, Math.Min(16, normalizedHash.Length)));
        var workId = BookDomainMapping.IsbnToContentWorkId(Path.GetFileNameWithoutExtension(normalizedFileName) ?? normalizedFileName);
        var item = new BookItem(
            BookDomainMapping.HashToContentItemId(normalizedHash, sizeBytes, normalizedFileName),
            workId,
            normalizedFileName,
            sizeBytes,
            normalizedHash,
            format: DetectFormat(normalizedFileName),
            isAdvertisable: true);
        return Task.FromResult<BookItem?>(item);
    }

    /// <inheritdoc/>
    public Task<BookItem?> TryGetItemByLocalMetadataAsync(LocalFileMetadata fileMetadata, CancellationToken cancellationToken = default)
    {
        if (fileMetadata == null || string.IsNullOrWhiteSpace(fileMetadata.Id) || fileMetadata.SizeBytes <= 0)
        {
            return Task.FromResult<BookItem?>(null);
        }

        _logger.LogDebug("[BookDomain] Resolving book item by local metadata: {Id}", fileMetadata.Id);
        var hash = string.IsNullOrWhiteSpace(fileMetadata.PrimaryHash) ? fileMetadata.Id : fileMetadata.PrimaryHash;
        return TryGetItemByHashAsync(hash, fileMetadata.Id, fileMetadata.SizeBytes, cancellationToken);
    }

    /// <inheritdoc/>
    public BookFormat DetectFormat(string filename, string? mimeType = null)
    {
        var extension = Path.GetExtension(filename).ToLowerInvariant();
        return extension switch
        {
            ".pdf" => BookFormat.Pdf,
            ".epub" => BookFormat.Epub,
            ".mobi" => BookFormat.Mobi,
            ".azw" => BookFormat.Azw,
            ".azw3" => BookFormat.Azw,
            ".fb2" => BookFormat.Fb2,
            ".txt" => BookFormat.Txt,
            ".doc" => BookFormat.Doc,
            ".docx" => BookFormat.Docx,
            _ => BookFormat.Unknown
        };
    }
}
