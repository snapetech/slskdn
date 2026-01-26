// <copyright file="IMediaVariantStore.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.MediaCore
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using slskd.VirtualSoulfind.Core;

    /// <summary>
    ///     Store for MediaVariant with Domain discriminator. T-911.
    /// </summary>
    /// <remarks>
    ///     Music delegates to IHashDbService; Image/Video/Generic use implementation-specific storage.
    /// </remarks>
    public interface IMediaVariantStore
    {
        /// <summary>
        ///     Gets a variant by VariantId (FlacKey for Music).
        /// </summary>
        Task<MediaVariant?> GetByVariantIdAsync(string variantId, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Gets variants for a Music recording (by MusicBrainz recording id). No-op for other domains.
        /// </summary>
        Task<IReadOnlyList<MediaVariant>> GetByRecordingIdAsync(string recordingId, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Gets variants by domain. For Music, a sampling; for Image/Video/Generic, from implementation storage.
        /// </summary>
        Task<IReadOnlyList<MediaVariant>> GetByDomainAsync(ContentDomain domain, int limit = 100, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Upserts a variant. For Music with Audio: updates existing HashDb entry when FlacKey exists. Image/Video/Generic: implementation storage.
        /// </summary>
        Task UpsertAsync(MediaVariant variant, CancellationToken cancellationToken = default);
    }
}
