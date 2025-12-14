// <copyright file="GenericFileContentDomainProvider.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.VirtualSoulfind.Core.GenericFile
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using slskd.Common.Moderation;

    /// <summary>
    ///     GenericFile domain provider implementation for VirtualSoulfind v2.
    /// </summary>
    /// <remarks>
    ///     T-VC03: GenericFile Domain Provider implementation.
    ///     Handles files that don't fit richer domain models (Music, Book, Movie, TV).
    ///     Identity based on hash + size + filename for deduplication.
    ///     Simple provider since no external services needed.
    /// </remarks>
    public sealed class GenericFileContentDomainProvider : IGenericFileContentDomainProvider
    {
        private readonly ILogger<GenericFileContentDomainProvider> _logger;

        /// <summary>
        ///     Initializes a new instance of the <see cref="GenericFileContentDomainProvider"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        public GenericFileContentDomainProvider(ILogger<GenericFileContentDomainProvider> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public async Task<GenericFileItem?> TryGetItemByLocalMetadataAsync(LocalFileMetadata fileMetadata, CancellationToken cancellationToken = default)
        {
            if (fileMetadata == null)
            {
                return null;
            }

            try
            {
                // For GenericFile domain, we create items on-demand from local metadata
                // In a full implementation, this might check a cache or database for existing items
                var item = GenericFileItem.FromLocalFileMetadata(fileMetadata, isAdvertisable: false);
                _logger.LogDebug("Created GenericFile item for {Filename} ({Size} bytes)", item.Filename, item.SizeBytes);
                return item;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create GenericFile item from local metadata: {Path}", fileMetadata.Id);
                return null;
            }
        }

        /// <inheritdoc/>
        public async Task<GenericFileItem?> TryGetItemByHashAndFilenameAsync(string primaryHash, string filename, long sizeBytes, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(primaryHash) || string.IsNullOrWhiteSpace(filename))
            {
                return null;
            }

            try
            {
                // For GenericFile domain, we create items on-demand
                // In a full implementation, this might check a cache or database for existing items
                var fileMetadata = new LocalFileMetadata(filename, sizeBytes)
                {
                    PrimaryHash = primaryHash
                };

                var item = GenericFileItem.FromLocalFileMetadata(fileMetadata, isAdvertisable: false);
                _logger.LogDebug("Created GenericFile item for {Filename} with hash {Hash}", item.Filename, item.PrimaryHash);
                return item;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create GenericFile item from hash/filename: {Hash}/{Filename}", primaryHash, filename);
                return null;
            }
        }
    }
}
