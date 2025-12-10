// <copyright file="MusicBrainzTargetRequest.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Integrations.MusicBrainz.API.DTO
{
    using System.Text.Json.Serialization;

    /// <summary>
    ///     Encapsulates the input needed to resolve MusicBrainz targets.
    /// </summary>
    public class MusicBrainzTargetRequest
    {
        /// <summary>
        ///     Gets or sets a MusicBrainz release identifier.
        /// </summary>
        public string? ReleaseId { get; set; }

        /// <summary>
        ///     Gets or sets a MusicBrainz recording identifier.
        /// </summary>
        public string? RecordingId { get; set; }

        /// <summary>
        ///     Gets or sets a Discogs release/master identifier.
        /// </summary>
        public string? DiscogsReleaseId { get; set; }

        /// <summary>
        ///     Gets a value indicating whether any identifier payload was supplied.
        /// </summary>
        [JsonIgnore]
        public bool HasIdentifier =>
            !string.IsNullOrWhiteSpace(ReleaseId) ||
            !string.IsNullOrWhiteSpace(RecordingId) ||
            !string.IsNullOrWhiteSpace(DiscogsReleaseId);
    }
}

