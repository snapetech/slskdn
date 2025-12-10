// <copyright file="IAutoTaggingService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Integrations.AutoTagging
{
    using System.Threading;
    using System.Threading.Tasks;
    using slskd.Integrations.MusicBrainz.Models;

    /// <summary>
    ///     Service that writes MusicBrainz metadata back to downloaded files.
    /// </summary>
    public interface IAutoTaggingService
    {
        /// <summary>
        ///     Applies textual metadata to the given file from the supplied track details.
        /// </summary>
        Task<AutoTagResult?> TagAsync(string filePath, TrackTarget track, CancellationToken cancellationToken = default);
    }
}

