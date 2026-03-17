// <copyright file="AudioTags.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.VirtualSoulfind.Core.Music
{
    /// <summary>
    ///     Represents basic audio metadata tags extracted from a file.
    /// </summary>
    /// <remarks>
    ///     T-VC02: Music Domain Provider data structures.
    ///     Simple container for audio tag information used in music matching.
    /// </remarks>
    public sealed record AudioTags(
        string? Title,
        string? Artist,
        string? Album,
        string? AlbumArtist,
        int? TrackNumber,
        int? TrackCount,
        int? DiscNumber,
        int? DiscCount,
        int? Year,
        string? Genre,
        string? MusicBrainzReleaseId,
        string? MusicBrainzRecordingId,
        string? MusicBrainzArtistId,
        string? Isrc);
}
