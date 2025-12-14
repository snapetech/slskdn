// <copyright file="IGenericFileContentDomainProvider.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.VirtualSoulfind.Core.GenericFile
{
    using System.Threading;
    using System.Threading.Tasks;
    using slskd.Common.Moderation;

    /// <summary>
    ///     Interface for the GenericFile domain provider in VirtualSoulfind v2.
    /// </summary>
    /// <remarks>
    ///     T-VC03: GenericFile Domain Provider implementation.
    ///     Handles files that don't fit richer domain models (Music, Book, Movie, TV).
    ///     Identity based on hash + size + filename for content deduplication.
    /// </remarks>
    public interface IGenericFileContentDomainProvider
    {
        /// <summary>
        ///     Attempts to resolve an item by local file metadata.
        /// </summary>
        /// <param name="fileMetadata">The local file metadata.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The resolved item, or null if not found.</returns>
        Task<GenericFileItem?> TryGetItemByLocalMetadataAsync(LocalFileMetadata fileMetadata, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Attempts to resolve an item by hash and filename.
        /// </summary>
        /// <param name="primaryHash">The primary hash (SHA256).</param>
        /// <param name="filename">The filename.</param>
        /// <param name="sizeBytes">The file size in bytes.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The resolved item, or null if not found.</returns>
        Task<GenericFileItem?> TryGetItemByHashAndFilenameAsync(string primaryHash, string filename, long sizeBytes, CancellationToken cancellationToken = default);
    }
}
