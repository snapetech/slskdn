// <copyright file="MetadataResult.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Integrations.MetadataFacade
{
    /// <summary>
    ///     Normalized metadata result from IMetadataFacade (artist, title, release, mbid, etc.).
    /// </summary>
    /// <remarks>
    ///     T-912: Metadata facade abstraction. Unifies MusicBrainz, AcoustID, file tags, Soulseek.
    /// </remarks>
    public sealed record MetadataResult(
        string? Artist,
        string? Title,
        string? Album,
        string? MusicBrainzRecordingId,
        string? MusicBrainzReleaseId,
        string? MusicBrainzArtistId,
        string? Isrc,
        int? Year,
        string? Genre,
        string? Source)
    {
        /// <summary>Source label when the result came from MusicBrainz.</summary>
        public const string SourceMusicBrainz = "MusicBrainz";

        /// <summary>Source label when the result came from AcoustID (then often MB).</summary>
        public const string SourceAcoustId = "AcoustID";

        /// <summary>Source label when the result came from file tags (TagLib).</summary>
        public const string SourceFileTags = "FileTags";

        /// <summary>Source label when the result came from Soulseek (e.g. filename).</summary>
        public const string SourceSoulseek = "Soulseek";
    }
}
