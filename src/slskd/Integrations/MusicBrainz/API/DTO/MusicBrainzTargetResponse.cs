// <copyright file="MusicBrainzTargetResponse.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Integrations.MusicBrainz.API.DTO
{
    using slskd.Integrations.MusicBrainz.Models;

    /// <summary>
    ///     Returns the resolved MusicBrainz target(s) to the client.
    /// </summary>
    public sealed class MusicBrainzTargetResponse
    {
        /// <summary>
        ///     Gets or sets the resolved album target, if any.
        /// </summary>
        public AlbumTarget? Album { get; set; }

        /// <summary>
        ///     Gets or sets the resolved track target, if any.
        /// </summary>
        public TrackTarget? Track { get; set; }
    }
}


















