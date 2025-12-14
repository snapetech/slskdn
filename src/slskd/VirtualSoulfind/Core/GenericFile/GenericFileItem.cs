// <copyright file="GenericFileItem.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.VirtualSoulfind.Core.GenericFile
{
    using System;
    using slskd.VirtualSoulfind.Core;

    /// <summary>
    ///     GenericFile domain implementation of <see cref="IContentItem"/> wrapping arbitrary files.
    /// </summary>
    /// <remarks>
    ///     T-VC03: GenericFile Domain Provider data structures.
    ///     Represents files that don't fit richer domain models (Music, Book, Movie, TV).
    ///     Identity based on hash + size + filename for deduplication.
    /// </remarks>
    public sealed class GenericFileItem : IContentItem
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="GenericFileItem"/> class.
        /// </summary>
        /// <param name="id">The domain-neutral item ID.</param>
        /// <param name="filename">The filename.</param>
        /// <param name="sizeBytes">The file size in bytes.</param>
        /// <param name="primaryHash">The primary hash (SHA256).</param>
        /// <param name="isAdvertisable">Whether this item is advertisable (T-MCP03).</param>
        public GenericFileItem(ContentItemId id, string filename, long sizeBytes, string primaryHash, bool isAdvertisable = false)
        {
            Id = id;
            Filename = filename ?? throw new ArgumentNullException(nameof(filename));
            SizeBytes = sizeBytes;
            PrimaryHash = primaryHash ?? throw new ArgumentNullException(nameof(primaryHash));
            IsAdvertisable = isAdvertisable;
        }

        /// <inheritdoc/>
        public ContentItemId Id { get; }

        /// <inheritdoc/>
        public ContentDomain Domain => ContentDomain.GenericFile;

        /// <inheritdoc/>
        public ContentWorkId? WorkId => null; // GenericFile items don't have parent works

        /// <inheritdoc/>
        public string Title => Filename;

        /// <inheritdoc/>
        public int? Position => null; // No position in GenericFile domain

        /// <inheritdoc/>
        public TimeSpan? Duration => null; // No duration for generic files

        /// <inheritdoc/>
        /// <remarks>
        ///     T-MCP03: This must be set explicitly based on MCP check results.
        ///     Default is false (conservative - require explicit MCP approval).
        /// </remarks>
        public bool IsAdvertisable { get; }

        /// <summary>
        ///     Gets the filename.
        /// </summary>
        public string Filename { get; }

        /// <summary>
        ///     Gets the file size in bytes.
        /// </summary>
        public long SizeBytes { get; }

        /// <summary>
        ///     Gets the primary hash (SHA256).
        /// </summary>
        public string PrimaryHash { get; }

        /// <summary>
        ///     Creates a <see cref="GenericFileItem"/> from local file metadata.
        /// </summary>
        /// <param name="fileMetadata">The local file metadata.</param>
        /// <param name="isAdvertisable">Whether this item is advertisable (default: false).</param>
        /// <returns>A new <see cref="GenericFileItem"/> instance.</returns>
        public static GenericFileItem FromLocalFileMetadata(LocalFileMetadata fileMetadata, bool isAdvertisable = false)
        {
            if (fileMetadata == null)
            {
                throw new ArgumentNullException(nameof(fileMetadata));
            }

            // Generate deterministic ContentItemId from hash + size + filename
            var idString = $"{fileMetadata.PrimaryHash}:{fileMetadata.SizeBytes}:{System.IO.Path.GetFileName(fileMetadata.Id)}";
            var itemId = ContentItemId.NewId(); // For now, use new ID - in practice would be deterministic

            return new GenericFileItem(
                id: itemId,
                filename: System.IO.Path.GetFileName(fileMetadata.Id),
                sizeBytes: fileMetadata.SizeBytes,
                primaryHash: fileMetadata.PrimaryHash,
                isAdvertisable: isAdvertisable);
        }
    }
}
