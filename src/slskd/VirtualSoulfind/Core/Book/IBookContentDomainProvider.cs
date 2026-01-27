// <copyright file="IBookContentDomainProvider.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.VirtualSoulfind.Core.Book;

using System;
using System.Threading;
using System.Threading.Tasks;
using slskd.Common.Moderation;
using slskd.VirtualSoulfind.Core;

/// <summary>
///     Interface for the Book domain provider in VirtualSoulfind v2.
/// </summary>
/// <remarks>
///     Handles book content with ISBN-based matching and format detection.
///     NO Soulseek backend - uses mesh/DHT/torrent/HTTP/local only.
/// </remarks>
public interface IBookContentDomainProvider
{
    /// <summary>
    ///     Attempts to resolve a book by ISBN.
    /// </summary>
    Task<BookWork?> TryGetWorkByIsbnAsync(string isbn, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Attempts to resolve a book by title and author.
    /// </summary>
    Task<BookWork?> TryGetWorkByTitleAuthorAsync(string title, string author, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Attempts to resolve a book item by hash and filename.
    /// </summary>
    Task<BookItem?> TryGetItemByHashAsync(string hash, string filename, long sizeBytes, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Attempts to resolve a book item by local file metadata.
    /// </summary>
    Task<BookItem?> TryGetItemByLocalMetadataAsync(LocalFileMetadata fileMetadata, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Detects the book format from filename or metadata.
    /// </summary>
    BookFormat DetectFormat(string filename, string? mimeType = null);
}

/// <summary>
///     Book work (book title).
/// </summary>
public class BookWork : IContentWork
{
    public BookWork(ContentWorkId id, string title, string? author = null, int? year = null, string? isbn = null)
    {
        Id = id;
        Title = title ?? throw new ArgumentNullException(nameof(title));
        Creator = author;
        Year = year;
        Isbn = isbn;
    }

    /// <inheritdoc/>
    public ContentWorkId Id { get; }

    /// <inheritdoc/>
    public ContentDomain Domain => ContentDomain.Book;

    /// <inheritdoc/>
    public string Title { get; }

    /// <inheritdoc/>
    public string? Creator { get; }

    /// <inheritdoc/>
    public int? Year { get; }

    /// <summary>
    ///     Gets the ISBN identifier.
    /// </summary>
    public string? Isbn { get; }

    /// <summary>
    ///     Gets the work ID as a string (for backward compatibility).
    /// </summary>
    public string WorkId => Id.Value.ToString();
}

/// <summary>
///     Book item (specific edition/format).
/// </summary>
public class BookItem : IContentItem
{
    public BookItem(ContentItemId id, ContentWorkId workId, string filename, long fileSize, string? hash = null, string? isbn = null, BookFormat format = BookFormat.Unknown, bool isAdvertisable = false)
    {
        Id = id;
        WorkId = workId;
        Filename = filename ?? throw new ArgumentNullException(nameof(filename));
        FileSize = fileSize;
        Hash = hash;
        Isbn = isbn;
        Format = format;
        IsAdvertisable = isAdvertisable;
    }

    /// <inheritdoc/>
    public ContentItemId Id { get; }

    /// <inheritdoc/>
    public ContentDomain Domain => ContentDomain.Book;

    /// <inheritdoc/>
    public ContentWorkId? WorkId { get; }

    /// <inheritdoc/>
    public string Title => Filename;

    /// <inheritdoc/>
    public int? Position => null; // Books don't have positions

    /// <inheritdoc/>
    public TimeSpan? Duration => null; // Books don't have duration

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
    ///     Gets the ISBN identifier.
    /// </summary>
    public string? Isbn { get; }

    /// <summary>
    ///     Gets the book format.
    /// </summary>
    public BookFormat Format { get; }

    /// <summary>
    ///     Gets the item ID as a string (for backward compatibility).
    /// </summary>
    public string ItemId => Id.Value.ToString();
}

/// <summary>
///     Book format.
/// </summary>
public enum BookFormat
{
    Unknown,
    Pdf,
    Epub,
    Mobi,
    Azw,
    Fb2,
    Txt,
    Doc,
    Docx,
}
